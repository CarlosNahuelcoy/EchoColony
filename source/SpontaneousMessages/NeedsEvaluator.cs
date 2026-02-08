using RimWorld;
using Verse;
using System.Linq;
using System.Collections.Generic;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Evalúa necesidades críticas de colonos para trigger mensajes urgentes
    /// LÓGICA INTELIGENTE: Rango 15-40% = quieren ayuda y PUEDEN hablar
    /// Debajo de 15% = demasiado débiles para hablar
    /// </summary>
    public static class NeedsEvaluator
    {
        // Tracking de última vez que cada colono envió un mensaje de need
        private static Dictionary<string, int> lastNeedMessageTick = new Dictionary<string, int>();
        
        // Cooldown mínimo entre mensajes de needs (4 horas)
        private const float MIN_HOURS_BETWEEN_NEED_MESSAGES = 4f;

        /// <summary>
        /// Evalúa todos los colonos y genera mensajes para necesidades críticas
        /// Se llama cada hora desde SpontaneousMessageTracker.GameComponentTick()
        /// </summary>
        public static void EvaluateCriticalNeeds()
        {
            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return;

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    // Verificar si el colono puede hablar
                    if (!CanColonistExpressNeed(pawn))
                        continue;

                    // Verificar cooldown específico de needs
                    if (IsOnNeedCooldown(pawn))
                        continue;

                    // Chequear si tiene alguna necesidad crítica
                    if (HasCriticalNeed(pawn, out string needDescription, out float severity))
                    {
                        // Verificar que el sistema de tracking permita el mensaje
                        var tracker = SpontaneousMessageTracker.Instance;
                        if (tracker != null && !tracker.CanSendMessage(pawn, TriggerType.CriticalNeed))
                            continue;

                        // Verificar willingness (con boost por need urgente)
                        if (!ColonistWillingnessEvaluator.WantsToSpeak(pawn, TriggerType.CriticalNeed, needDescription))
                            continue;

                        // Generar mensaje urgente
                        GenerateCriticalNeedMessage(pawn, needDescription, severity);
                        
                        // Registrar que enviamos mensaje
                        RegisterNeedMessage(pawn);
                        
                        // Solo un colono por chequeo para evitar spam
                        return;
                    }
                }
            }

            // Cleanup de tracking antiguo (cada 24h)
            if (Find.TickManager.TicksGame % (GenDate.TicksPerHour * 24) == 0)
            {
                CleanupOldTracking();
            }
        }

        /// <summary>
        /// Verifica si el colono está en condiciones de expresar una necesidad
        /// </summary>
        private static bool CanColonistExpressNeed(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed)
                return false;

            // No puede hablar si está en coma
            var consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            if (consciousness < 0.3f)
                return false;

            // No puede hablar si está en estado mental inválido
            if (ColonistWillingnessEvaluator.IsInInvalidMentalState(pawn))
                return false;

            // Verificar settings del colono
            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (!settings.enabled || !settings.IsTriggerAllowed(TriggerType.CriticalNeed))
                return false;

            return true;
        }

        /// <summary>
        /// Verifica si el colono está en cooldown para mensajes de needs
        /// </summary>
        private static bool IsOnNeedCooldown(Pawn pawn)
        {
            string key = pawn.ThingID;
            
            if (!lastNeedMessageTick.ContainsKey(key))
                return false;

            int ticksSinceLast = Find.TickManager.TicksGame - lastNeedMessageTick[key];
            float hoursSinceLast = ticksSinceLast / (float)GenDate.TicksPerHour;

            return hoursSinceLast < MIN_HOURS_BETWEEN_NEED_MESSAGES;
        }

        /// <summary>
        /// Registra que el colono envió un mensaje de need
        /// </summary>
        private static void RegisterNeedMessage(Pawn pawn)
        {
            lastNeedMessageTick[pawn.ThingID] = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Limpia tracking de colonos que ya no existen
        /// </summary>
        private static void CleanupOldTracking()
        {
            var validThingIDs = new HashSet<string>();
            
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    validThingIDs.Add(pawn.ThingID);
                }
            }

            var toRemove = lastNeedMessageTick.Keys.Where(k => !validThingIDs.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                lastNeedMessageTick.Remove(key);
            }
        }

        /// <summary>
        /// Verifica si un colono tiene una necesidad crítica
        /// NUEVA LÓGICA: Rango 15-40% = crítico pero puede hablar
        /// Retorna true si tiene, junto con descripción y severidad (0-1)
        /// </summary>
        private static bool HasCriticalNeed(Pawn pawn, out string needDescription, out float severity)
        {
            needDescription = "";
            severity = 0f;

            if (pawn?.needs == null)
                return false;

            // Prioridad 1: HAMBRE CRÍTICA (15-40%)
            // Rango donde QUIEREN ayuda y PUEDEN hablar
            var food = pawn.needs.food;
            if (food != null && food.CurLevel < 0.40f && food.CurLevel > 0.15f)
            {
                severity = 1f - (food.CurLevel / 0.40f); // 0.0 a 1.0
                
                if (food.CurLevel < 0.20f)
                    needDescription = "I'm starving and need food urgently";
                else if (food.CurLevel < 0.30f)
                    needDescription = "I'm getting really hungry";
                else
                    needDescription = "I should probably eat something soon";
                
                return true;
            }

            // Prioridad 2: CANSANCIO EXTREMO (10-30%)
            var rest = pawn.needs.rest;
            if (rest != null && rest.CurLevel < 0.30f && rest.CurLevel > 0.10f)
            {
                severity = 1f - (rest.CurLevel / 0.30f);
                
                if (rest.CurLevel < 0.15f)
                    needDescription = "I'm exhausted and about to collapse";
                else if (rest.CurLevel < 0.20f)
                    needDescription = "I really need to sleep";
                else
                    needDescription = "I'm getting very tired";
                
                return true;
            }

            // Prioridad 3: MOOD MUY BAJO (15-30%)
            // Solo si está bajo pero NO al borde del colapso
            var mood = pawn.needs.mood;
            if (mood != null && mood.CurLevel < 0.30f && mood.CurLevel > 0.15f)
            {
                severity = 1f - (mood.CurLevel / 0.30f);
                
                if (mood.CurLevel < 0.20f)
                    needDescription = "I'm feeling terrible and close to breaking";
                else
                    needDescription = "I'm not doing well mentally";
                
                return true;
            }

            // Prioridad 4: FRÍO/CALOR EXTREMO
            // Chequear hipotermia/calor extremo via hediffs
            if (pawn.health?.hediffSet != null)
            {
                // Hipotermia moderada
                var hypothermia = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (hypothermia != null && hypothermia.Severity > 0.3f && hypothermia.Severity < 0.7f)
                {
                    severity = hypothermia.Severity;
                    needDescription = "I'm freezing and need warmth";
                    return true;
                }

                // Calor extremo moderado
                var heatstroke = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
                if (heatstroke != null && heatstroke.Severity > 0.3f && heatstroke.Severity < 0.7f)
                {
                    severity = heatstroke.Severity;
                    needDescription = "It's unbearably hot";
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Genera y envía mensaje de necesidad crítica
        /// </summary>
        private static void GenerateCriticalNeedMessage(Pawn pawn, string needDescription, float severity)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Warning($"[EchoColony] Cannot generate need message: MyStoryModComponent not available");
                return;
            }

            // Calcular urgencia basada en severidad
            float urgency = 0.8f + (severity * 0.2f); // 0.8 - 1.0

            // Crear request
            var request = new MessageRequest(
                pawn, 
                TriggerType.CriticalNeed, 
                needDescription, 
                urgency
            );

            // Generar mensaje (async via coroutine)
            MyStoryModComponent.Instance.StartCoroutine(
                SpontaneousMessageGenerator.GenerateAndSendMessage(request)
            );

            if (MyMod.Settings?.debugMode == true)
            {
                Log.Message($"[EchoColony] Critical need message triggered for {pawn.LabelShort}: {needDescription} (severity: {severity:F2})");
            }
        }
    }
}