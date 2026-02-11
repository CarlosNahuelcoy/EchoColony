using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Rastrea el estado de mensajes espontáneos por colono
    /// GameComponent persistente que se guarda con el save
    /// </summary>
    public class SpontaneousMessageTracker : GameComponent
    {
        private static SpontaneousMessageTracker instance;
        public static SpontaneousMessageTracker Instance => instance;

        // Tracking por colono usando ThingID como key
        private Dictionary<string, ColonistMessageTracker> colonistTrackers = new Dictionary<string, ColonistMessageTracker>();
        
        // Timestamp para próximo chequeo de mensajes random globales
        private int nextRandomMessageCheck = 0;

        public SpontaneousMessageTracker(Game game)
        {
            instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Collections.Look(ref colonistTrackers, "colonistTrackers", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref nextRandomMessageCheck, "nextRandomMessageCheck", 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (colonistTrackers == null)
                    colonistTrackers = new Dictionary<string, ColonistMessageTracker>();
            }
        }

        public override void GameComponentTick()
{
    base.GameComponentTick();
    
    // ✅ STORYTELLER UPDATE
    StorytellerSpontaneousMessageSystem.Update();
    
    // ✅ COLONIST MESSAGES 
    if (Find.TickManager.TicksGame % GenDate.TicksPerHour != 0)
        return;

    if (!MyMod.Settings.IsSpontaneousMessagesActive())
        return;

    // Limpiar trackers de colonos que ya no existen
    CleanupOldTrackers();

    // Chequear mensajes random si está habilitado
    if (MyMod.Settings.AreRandomMessagesEnabled())
    {
        CheckRandomMessages();
    }
}

        /// <summary>
        /// Obtiene o crea el tracker para un colono específico
        /// </summary>
        public ColonistMessageTracker GetTrackerFor(Pawn pawn)
        {
            if (pawn == null) return null;

            string key = pawn.ThingID;
            if (!colonistTrackers.ContainsKey(key))
            {
                colonistTrackers[key] = new ColonistMessageTracker(pawn);
            }

            return colonistTrackers[key];
        }

        /// <summary>
        /// Verifica si un colono puede enviar un mensaje considerando todos los límites
        /// </summary>
        public bool CanSendMessage(Pawn pawn, TriggerType triggerType)
        {
            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return false;

            // Verificar que el modo permita este tipo de trigger
            if (triggerType == TriggerType.Incident && !MyMod.Settings.AreIncidentMessagesEnabled())
                return false;
            if (triggerType == TriggerType.Random && !MyMod.Settings.AreRandomMessagesEnabled())
                return false;

            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (settings == null || !settings.enabled)
                return false;

            if (!settings.IsTriggerAllowed(triggerType))
                return false;

            var tracker = GetTrackerFor(pawn);
            return tracker != null && tracker.CanSendMessage(pawn, triggerType);
        }

        /// <summary>
        /// Registra que un colono envió un mensaje
        /// </summary>
        public void RegisterMessage(Pawn pawn, TriggerType triggerType)
        {
            var tracker = GetTrackerFor(pawn);
            tracker?.RegisterMessage(triggerType);
        }

        /// <summary>
        /// Marca o desmarca que un colono tiene un mensaje pendiente de respuesta
        /// </summary>
        public void SetPendingResponse(Pawn pawn, bool isPending)
        {
            var tracker = GetTrackerFor(pawn);
            if (tracker != null)
            {
                tracker.hasPendingResponse = isPending;
            }
        }

        /// <summary>
        /// Verifica si un colono tiene un mensaje pendiente de respuesta
        /// </summary>
        public bool HasPendingResponse(Pawn pawn)
        {
            var tracker = GetTrackerFor(pawn);
            return tracker?.hasPendingResponse ?? false;
        }

        /// <summary>
        /// Chequea si es hora de evaluar mensajes random
        /// </summary>
        private void CheckRandomMessages()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            if (currentTick < nextRandomMessageCheck)
                return;

            // Establecer próximo chequeo
            float hoursToNext = MyMod.Settings.randomMessageIntervalHours;
            nextRandomMessageCheck = currentTick + (int)(hoursToNext * GenDate.TicksPerHour);

            // Intentar generar mensaje random
            RandomMessageEvaluator.EvaluateRandomMessage();
        }

        /// <summary>
        /// Limpia trackers de colonos que ya no existen
        /// </summary>
        private void CleanupOldTrackers()
        {
            if (Find.TickManager.TicksGame % (GenDate.TicksPerHour * 24) != 0)
                return; // Solo limpiar una vez al día

            var validThingIDs = new HashSet<string>();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    validThingIDs.Add(pawn.ThingID);
                }
            }

            var toRemove = colonistTrackers.Keys.Where(k => !validThingIDs.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                colonistTrackers.Remove(key);
            }
        }
    }

    /// <summary>
    /// Datos de tracking por colono individual
    /// </summary>
    public class ColonistMessageTracker : IExposable
    {
        public int messagesToday = 0;
        public int lastDayChecked = 0;
        public int lastMessageTick = 0;
        public bool hasPendingResponse = false;

        // Timestamps del último mensaje por tipo de trigger
        public Dictionary<TriggerType, int> lastTriggerTime = new Dictionary<TriggerType, int>();

        public ColonistMessageTracker()
        {
            // Constructor vacío para serialización
        }

        public ColonistMessageTracker(Pawn pawn)
        {
            lastDayChecked = GenDate.DaysPassed;
            
            // CRÍTICO: Inicializar lastMessageTick en un tiempo muy antiguo
            // para que el colono pueda hablar inmediatamente
            lastMessageTick = 0;
            
            messagesToday = 0;
            hasPendingResponse = false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref messagesToday, "messagesToday", 0);
            Scribe_Values.Look(ref lastDayChecked, "lastDayChecked", 0);
            Scribe_Values.Look(ref lastMessageTick, "lastMessageTick", 0);
            Scribe_Values.Look(ref hasPendingResponse, "hasPendingResponse", false);
            Scribe_Collections.Look(ref lastTriggerTime, "lastTriggerTime", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars && lastTriggerTime == null)
            {
                lastTriggerTime = new Dictionary<TriggerType, int>();
            }
        }

        /// <summary>
        /// Verifica si puede enviar mensaje considerando límites y cooldowns
        /// </summary>
        public bool CanSendMessage(Pawn pawn, TriggerType triggerType)
        {
            // Reset diario
            int today = GenDate.DaysPassed;
            if (today != lastDayChecked)
            {
                messagesToday = 0;
                lastDayChecked = today;
            }

            // Verificar límite diario
            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (messagesToday >= settings.maxMessagesPerDay)
                return false;

            // Verificar cooldown general
            int ticksSinceLastMessage = Find.TickManager.TicksGame - lastMessageTick;
            float hoursSinceLastMessage = ticksSinceLastMessage / (float)GenDate.TicksPerHour;
            
            if (hoursSinceLastMessage < settings.cooldownHours)
                return false;

            // Verificar cooldown específico del tipo de trigger (mínimo 2h entre mismo tipo)
            if (lastTriggerTime.ContainsKey(triggerType))
            {
                int ticksSinceTrigger = Find.TickManager.TicksGame - lastTriggerTime[triggerType];
                float hoursSinceTrigger = ticksSinceTrigger / (float)GenDate.TicksPerHour;
                
                if (hoursSinceTrigger < 2f)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Registra un mensaje enviado
        /// </summary>
        public void RegisterMessage(TriggerType triggerType)
        {
            messagesToday++;
            lastMessageTick = Find.TickManager.TicksGame;
            lastTriggerTime[triggerType] = Find.TickManager.TicksGame;
            hasPendingResponse = true;
        }
    }

    /// <summary>
    /// Evaluador de mensajes aleatorios
    /// </summary>
    public static class RandomMessageEvaluator
    {
        public static void EvaluateRandomMessage()
        {
            // Obtener colonos elegibles
            var eligible = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Random);
            
            if (!eligible.Any())
                return;

            // Seleccionar uno al azar (con priorización de traits sociales si está activo)
            var selected = SpontaneousMessageEvaluator.SelectBestCandidate(eligible, TriggerType.Random, null);
            
            if (selected == null)
                return;

            // Verificar si el colono "quiere hablar"
            if (!ColonistWillingnessEvaluator.WantsToSpeak(selected, TriggerType.Random, ""))
                return;

            // Generar mensaje
            var request = new MessageRequest(selected, TriggerType.Random, "casual conversation", 0.3f);
            MyStoryModComponent.Instance.StartCoroutine(
                SpontaneousMessageGenerator.GenerateAndSendMessage(request)
            );
        }
    }
}