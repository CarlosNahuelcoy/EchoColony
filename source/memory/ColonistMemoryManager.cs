using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst.Intrinsics;
using UnityEngine.PlayerLoop;
using Verse;
using Verse.Noise;

namespace EchoColony
{
    public class ColonistMemoryManager : GameComponent
    {
        private Dictionary<string, ColonistMemoryTracker> memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
        private DailyGroupMemoryTracker groupMemoryTracker = new DailyGroupMemoryTracker();

        private int lastRecordedDay = -1;

        // Flag to control if the system is enabled
        public static bool IsMemorySystemEnabled
        {
            get { return MyMod.Settings?.enableMemorySystem ?? false; }
        }

        // Constructor without parameters (required for RimWorld serialization)
        public ColonistMemoryManager()
        {
        }

        // Constructor with Game (maintain for compatibility)
        public ColonistMemoryManager(Game game)
        {
            // Registrarse en MyStoryModComponent si existe
            if (MyStoryModComponent.Instance != null)
            {
                MyStoryModComponent.Instance.ColonistMemoryManager = this;
                Log.Message("[EchoColony] ColonistMemoryManager registered with MyStoryModComponent");
            }
        }

        // ✅ NUEVO: Método estático para obtener o crear instancia
        public static ColonistMemoryManager GetOrCreate()
        {
            if (Current.Game == null)
            {
                Log.Error("[EchoColony] Cannot get ColonistMemoryManager - no active game");
                return null;
            }

            var existing = Current.Game.GetComponent<ColonistMemoryManager>();
            
            if (existing == null)
            {
                Log.Message("[EchoColony] Creating ColonistMemoryManager automatically");
                existing = new ColonistMemoryManager(Current.Game);
                Current.Game.components.Add(existing);
            }

            return existing;
        }

        public ColonistMemoryTracker GetTrackerFor(Pawn pawn)
        {
            // If system is disabled, return empty non-persistent tracker
            if (!IsMemorySystemEnabled)
            {
                return new ColonistMemoryTracker(pawn);
            }

            if (pawn == null)
            {
                Log.Error("[EchoColony] GetTrackerFor called with null pawn");
                return new ColonistMemoryTracker(null);
            }

            string id = pawn.ThingID;
            
            if (!memoryPerPawn.ContainsKey(id))
            {
                Log.Message($"[EchoColony] Creating new memory tracker for {pawn.LabelShort}");
                var tracker = new ColonistMemoryTracker(pawn);
                memoryPerPawn[id] = tracker;
                return tracker;
            }
            else
            {
                var tracker = memoryPerPawn[id];
                tracker.SetPawn(pawn);
                return tracker;
            }
        }

        // Getter for group memories
        public DailyGroupMemoryTracker GetGroupMemoryTracker()
        {
            if (!IsMemorySystemEnabled)
            {
                return new DailyGroupMemoryTracker();
            }

            return groupMemoryTracker;
        }

        //*furel - New Memory system*
        /// <summary>
        /// Day tracker. Every time the day changes sends all the memories created for each pawn and stores the summary.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!IsMemorySystemEnabled) return;

            // It is evaluated every 1,000 ticks (~16 seconds of real time at x1 speed).
            if (Find.TickManager.TicksGame % 1000 == 0)
            {
                int currentDay = GenDate.DaysPassed;

                if (lastRecordedDay == -1)
                {
                    lastRecordedDay = currentDay;
                    return;
                }

                // Change of day detected
                if (currentDay > lastRecordedDay)
                {
                    Log.Message($"[EchoColony] Day change detected: Day {lastRecordedDay} ended. Advancing to Day {currentDay}. Processing individual daily logs.");
                    int dayToProcess = lastRecordedDay;
                    lastRecordedDay = currentDay;

                    foreach (var kvp in memoryPerPawn)
                    {
                        Pawn pawn = kvp.Value?.Pawn;

                        if (!IsPawnActiveInColony(pawn))
                        {
                            continue;
                        }

                        // Starts an independent coroutine per colonist.
                        if (MyStoryModComponent.Instance != null)
                        {
                            MyStoryModComponent.Instance.StartCoroutine(ProcessPawnEndOfDaySequence(pawn, kvp.Value, dayToProcess));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Individual sequence per colonist: waits for its turn queue and then processes its end-of-day.
        /// </summary>
        private IEnumerator ProcessPawnEndOfDaySequence(Pawn pawn, ColonistMemoryTracker tracker, int dayToProcess)
        {
            // Waits for the API to respond and save the pending turns for the colonist (threshold = 1)
            yield return ColonistMemoryHelper.CheckAndGenerateMemoryRoutine(pawn, 1);

            // With the turn memory for the day, consolidates the day's summary
            try
            {
                tracker.ProcessEndOfDay(dayToProcess);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error al procesar fin de día para {pawn?.LabelShort}: {ex.Message}");
            }
        }

        // FIXED: Proper ExposeData implementation
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Collections.Look(ref memoryPerPawn, "memoryPerPawn", LookMode.Value, LookMode.Deep);
            Scribe_Deep.Look(ref groupMemoryTracker, "groupMemoryTracker");

            Scribe_Values.Look(ref lastRecordedDay, "lastRecordedDay", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Initialize if null
                if (memoryPerPawn == null)
                {
                    memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                    Log.Warning("[EchoColony] memoryPerPawn was null after loading");
                }
                
                if (groupMemoryTracker == null)
                {
                    groupMemoryTracker = new DailyGroupMemoryTracker();
                    Log.Warning("[EchoColony] groupMemoryTracker was null after loading");
                }

                // Load report
                int totalMemories = memoryPerPawn.Sum(kvp => kvp.Value?.GetAllMemories()?.Count ?? 0);
                int groupMemCount = groupMemoryTracker?.GetAllGroupMemories()?.Count ?? 0;
                
                Log.Message($"[EchoColony] Loaded memories: {memoryPerPawn.Count} colonists, {totalMemories} individual memories, {groupMemCount} group memories");

                // Re-assign pawn references
                ReassignPawnReferences();

                // Inform if system is disabled
                if (!IsMemorySystemEnabled)
                {
                    Log.Message("[EchoColony] Memory system disabled - existing memories preserved, new memories will not be created");
                }
                
                // Update global reference
                if (MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.ColonistMemoryManager = this;
                }
                else
                {
                    Log.Error("[EchoColony] MyStoryModComponent.Instance is NULL during load");
                }

                if (lastRecordedDay == -1)
                {
                    lastRecordedDay = GenDate.DaysPassed;
                }
            }
            
            // Save report
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int totalMemories = memoryPerPawn.Sum(kvp => kvp.Value?.GetAllMemories()?.Count ?? 0);
                int groupMemCount = groupMemoryTracker?.GetAllGroupMemories()?.Count ?? 0;
                
                Log.Message($"[EchoColony] Saving memories: {memoryPerPawn.Count} colonists, {totalMemories} individual memories, {groupMemCount} group memories");
            }
        }

        // Re-assign pawn references after loading
        private void ReassignPawnReferences()
        {
            if (memoryPerPawn == null || memoryPerPawn.Count == 0)
                return;

            var allColonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
            if (allColonists == null)
            {
                Log.Warning("[EchoColony] Cannot reassign pawn references - PawnsFinder returned null");
                return;
            }

            int reassigned = 0;
            int notFound = 0;

            foreach (var kvp in memoryPerPawn)
            {
                string id = kvp.Key;
                var colonist = allColonists.FirstOrDefault(p => p.ThingID == id);
                
                if (colonist != null)
                {
                    kvp.Value.SetPawn(colonist);
                    reassigned++;
                }
                else
                {
                    notFound++;
                }
            }

            if (reassigned > 0 || notFound > 0)
            {
                Log.Message($"[EchoColony] Reassigned {reassigned} pawns, {notFound} pawns absent (dead/other maps)");
            }
        }

        // Debug method to verify state
        public void DebugPrintMemoryState()
        {
            string worldName = Current.Game?.World?.info?.name ?? "Unknown";
            Log.Message("[EchoColony] ========================================");
            Log.Message("[EchoColony] MEMORY SYSTEM STATE");
            Log.Message("[EchoColony] ========================================");
            Log.Message($"[EchoColony] World: {worldName}");
            Log.Message($"[EchoColony] System enabled: {IsMemorySystemEnabled}");
            Log.Message($"[EchoColony] Colonists with memories: {memoryPerPawn?.Count ?? 0}");

            if (groupMemoryTracker != null)
            {
                var groupCount = groupMemoryTracker.GetAllGroupMemories()?.Count ?? 0;
                Log.Message($"[EchoColony] Groups with memories: {groupCount}");
            }

            if (memoryPerPawn != null && memoryPerPawn.Count > 0)
            {
                Log.Message("[EchoColony] Details:");
                foreach (var kvp in memoryPerPawn)
                {
                    var stats = kvp.Value?.GetMemoryStats();
                    var lastDay = kvp.Value?.GetLastMemoryDay() ?? -1;
                    Log.Message($"[EchoColony]   {kvp.Key}: {stats?.total ?? 0} memories (last: day {lastDay})");
                }
            }
            
            Log.Message($"[EchoColony] Global reference: {(MyStoryModComponent.Instance?.ColonistMemoryManager != null ? "OK" : "NULL")}");
            Log.Message("[EchoColony] ========================================");
        }

        // Method to force manual cleanup (useful for debugging)
        public void ForceCleanMemories()
        {
            int colonistCount = memoryPerPawn?.Count ?? 0;
            int groupCount = groupMemoryTracker?.GetAllGroupMemories()?.Count ?? 0;

            memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            groupMemoryTracker = new DailyGroupMemoryTracker();

            Log.Message($"[EchoColony] Forced cleanup: {colonistCount} colonists, {groupCount} groups removed");
            Messages.Message($"EchoColony: Memories cleaned ({colonistCount} colonists, {groupCount} groups)", 
                           MessageTypeDefOf.TaskCompletion);
        }
        /// <summary>
        /// *furel - pawn filtrer* Checks if the pawn is part of your colony.
        /// </summary>
        
        private bool IsPawnActiveInColony(Pawn pawn)
        {
            // 1. Si es null, está muerto o completamente destruido por el motor, no está activo.
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded)
                return false;

            // 2. Comprobar si pertenece al ecosistema de la colonia (Colono, Esclavo o Prisionero)
            // RimWorld gestiona IsQuestLodger para refugiados temporales de misiones.
            bool holdsColonyStatus = pawn.IsColonist ||
                                     pawn.IsSlaveOfColony ||
                                     pawn.IsPrisonerOfColony ||
                                     pawn.IsQuestLodger();

            if (!holdsColonyStatus)
                return false;

            // 3. Comprobar si está físicamente accesible.
            // Debe estar "Spawned" (en algún mapa activo) O viajando en una caravana del jugador.
            bool isPhysicallyPresent = pawn.Spawned || RimWorld.Planet.CaravanUtility.IsCaravanMember(pawn);

            return isPhysicallyPresent;
        }

        /// <summary>
        /// Cleans memoryies of dead/deleted pawns.
        /// </summary>
        public void CleanupOrphanedMemories()
        {
            // Obtenemos todos los IDs de peones que el juego aún reconoce (vivos o muertos en el registro)
            var validPawnIDs = new HashSet<string>(
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                    .Where(p => p != null)
                    .Select(p => p.ThingID)
            );

            List<string> keysToRemove = new List<string>();

            // Identificar memorias de peones que ya no existen
            foreach (var key in memoryPerPawn.Keys)
            {
                if (!validPawnIDs.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            // Eliminar los datos huérfanos
            foreach (var key in keysToRemove)
            {
                memoryPerPawn.Remove(key);
                Log.Message($"[EchoColony] Cleaned up orphaned memory tracker for Pawn ID: {key}");
            }
        }

        // Validate system integrity
        public bool ValidateMemoryIntegrity()
        {
            try
            {
                if (memoryPerPawn == null || groupMemoryTracker == null)
                {
                    Log.Error("[EchoColony] Null memory references detected");
                    return false;
                }

                int invalidTrackers = 0;
                int totalMemories = 0;
                
                foreach (var kvp in memoryPerPawn)
                {
                    if (kvp.Value == null)
                    {
                        invalidTrackers++;
                    }
                    else
                    {
                        var mems = kvp.Value.GetAllMemories();
                        totalMemories += mems?.Count ?? 0;
                    }
                }

                if (invalidTrackers > 0)
                {
                    Log.Warning($"[EchoColony] {invalidTrackers} invalid trackers found");
                    return false;
                }

                Log.Message($"[EchoColony] Integrity verified: {memoryPerPawn.Count} trackers, {totalMemories} total memories");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error verifying integrity: {ex.Message}");
                return false;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            CleanupOrphanedMemories();
        }
    }
}