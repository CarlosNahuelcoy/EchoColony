using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace EchoColony
{
    public class ColonistMemoryManager : GameComponent
    {
        private Dictionary<string, ColonistMemoryTracker> memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
        private DailyGroupMemoryTracker groupMemoryTracker = new DailyGroupMemoryTracker();

        // Flag to control if the system is enabled
        public static bool IsMemorySystemEnabled
        {
            get { return MyMod.Settings?.enableMemorySystem ?? false; }
        }

        // Constructor without parameters (required for RimWorld serialization)
        public ColonistMemoryManager()
        {
            ClearAllData();
        }

        // Constructor with Game (maintain for compatibility)
        public ColonistMemoryManager(Game game)
        {
            ClearAllData();
        }

        private void ClearAllData()
        {
            memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            groupMemoryTracker = new DailyGroupMemoryTracker();
        }

        public ColonistMemoryTracker GetTrackerFor(Pawn pawn)
        {
            // If system is disabled, return empty non-persistent tracker
            if (!IsMemorySystemEnabled)
            {
                return new ColonistMemoryTracker(pawn); // Temporary tracker that doesn't get saved
            }

            string id = pawn.ThingID;
            if (!memoryPerPawn.ContainsKey(id))
            {
                var tracker = new ColonistMemoryTracker(pawn);
                memoryPerPawn[id] = tracker;
            }
            else
            {
                // Ensure pawn is assigned after loading
                memoryPerPawn[id].SetPawn(pawn);
            }
            return memoryPerPawn[id];
        }

        // Getter for group memories
        public DailyGroupMemoryTracker GetGroupMemoryTracker()
        {
            // If system is disabled, return empty non-persistent tracker
            if (!IsMemorySystemEnabled)
            {
                return new DailyGroupMemoryTracker(); // Temporary tracker that doesn't get saved
            }

            return groupMemoryTracker;
        }

        // Simplified ExposeData - let RimWorld handle the lifecycle automatically
        public override void ExposeData()
        {
			//Clean previous data.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                groupMemoryTracker = new DailyGroupMemoryTracker();
            }
            // Save/Load independently of configuration
            // This allows loading existing memories even if system is disabled
            Scribe_Collections.Look(ref memoryPerPawn, "memoryPerPawn", LookMode.Value, LookMode.Deep);
            Scribe_Deep.Look(ref groupMemoryTracker, "groupMemoryTracker");

            // Post-load initialization and cleanup
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Data integrity verification
                if (memoryPerPawn == null)
                    memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                
                if (groupMemoryTracker == null)
                    groupMemoryTracker = new DailyGroupMemoryTracker();

                Log.Message($"[EchoColony] Memories loaded: {memoryPerPawn.Count} colonists");

                // Re-assign pawn references after loading
                ReassignPawnReferences();

                // If system is disabled, clean loaded memories
                if (!IsMemorySystemEnabled)
                {
                    if (memoryPerPawn.Count > 0)
                    {
                        Log.Message("[EchoColony] Memory system disabled - cleaning loaded memories");
                        memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                        groupMemoryTracker = new DailyGroupMemoryTracker();
                    }
                }
                // UPDATE THE GLOBAL REFERENCE IF ONE EXISTS
                if (MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.ColonistMemoryManager = this;
                    Log.Message("[EchoColony] Global Manager reference updated after loading.");
                }
            }
        }

        // Re-assign pawn references after loading
        private void ReassignPawnReferences()
        {
            if (memoryPerPawn == null || memoryPerPawn.Count == 0)
                return;

            var allColonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive; //Take every pawn alive, colonist or slave
            if (allColonists == null)
                return;

            int reassigned = 0;
            foreach (var colonist in allColonists)
            {
                string id = colonist.ThingID;
                if (memoryPerPawn.ContainsKey(id))
                {
                    memoryPerPawn[id].SetPawn(colonist);
                    reassigned++;
                }
            }

            if (reassigned > 0)
            {
                Log.Message($"[EchoColony] Re-assigned {reassigned} colonists to their memory trackers");
            }
        }

        // Debug method to verify state
        public void DebugPrintMemoryState()
        {
            string worldName = Current.Game?.World?.info?.name ?? "Unknown";
            Log.Message($"[EchoColony] DEBUG Memory system state:");
            Log.Message($"[EchoColony]   - Current world: '{worldName}'");
            Log.Message($"[EchoColony]   - System enabled: {IsMemorySystemEnabled}");
            Log.Message($"[EchoColony]   - Colonists with memories: {memoryPerPawn?.Count ?? 0}");
            
            if (groupMemoryTracker != null)
            {
                var groupCount = groupMemoryTracker.GetAllGroupMemories()?.Count ?? 0;
                Log.Message($"[EchoColony]   - Groups with memories: {groupCount}");
            }

            if (memoryPerPawn != null && memoryPerPawn.Count > 0)
            {
                foreach (var kvp in memoryPerPawn.Take(3)) // Show only first 3
                {
                    var stats = kvp.Value?.GetMemoryStats();
                    Log.Message($"[EchoColony]     - {kvp.Key}: {stats?.total ?? 0} memories");
                }
            }
        }

        // Method to force manual cleanup (useful for debugging)
        public void ForceCleanMemories()
        {
            int colonistCount = memoryPerPawn?.Count ?? 0;
            int groupCount = groupMemoryTracker?.GetAllGroupMemories()?.Count ?? 0;

            memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            groupMemoryTracker = new DailyGroupMemoryTracker();

            Log.Message($"[EchoColony] Forced cleanup completed: {colonistCount} colonists, {groupCount} groups");
            Messages.Message($"EchoColony: Memories cleaned ({colonistCount} colonists, {groupCount} groups)", 
                           MessageTypeDefOf.TaskCompletion);
        }

        // Validate system integrity
        public bool ValidateMemoryIntegrity()
        {
            try
            {
                if (memoryPerPawn == null || groupMemoryTracker == null)
                {
                    Log.Warning("[EchoColony] Null memory references detected");
                    return false;
                }

                // Verify tracker references are not null
                int invalidTrackers = 0;
                foreach (var tracker in memoryPerPawn.Values)
                {
                    if (tracker == null)
                    {
                        invalidTrackers++;
                    }
                }

                if (invalidTrackers > 0)
                {
                    Log.Warning($"[EchoColony] {invalidTrackers} invalid trackers found");
                    return false;
                }

                Log.Message("[EchoColony] Memory system integrity verified");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error verifying memory integrity: {ex.Message}");
                return false;
            }
        }
    }
}