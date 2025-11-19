using System.Collections.Generic;
using Verse;

namespace EchoColony
{
    public class ColonistMemoryManager : GameComponent
    {
        private Dictionary<string, ColonistMemoryTracker> memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
        private DailyGroupMemoryTracker groupMemoryTracker = new DailyGroupMemoryTracker();

        // ✅ Constructor sin parámetros (REQUERIDO para la serialización de RimWorld)
        public ColonistMemoryManager()
        {
        }

        // Constructor con Game (mantener para compatibilidad)
        public ColonistMemoryManager(Game game)
        {
        }

        public ColonistMemoryTracker GetTrackerFor(Pawn pawn)
        {
            string id = pawn.ThingID;
            if (!memoryPerPawn.ContainsKey(id))
            {
                var tracker = new ColonistMemoryTracker(pawn); // ✅ Usar constructor con pawn
                memoryPerPawn[id] = tracker;
            }
            else
            {
                // ✅ Asegurar que el pawn esté asignado después de cargar
                memoryPerPawn[id].SetPawn(pawn);
            }
            return memoryPerPawn[id];
        }

        // Getter para las memorias grupales
        public DailyGroupMemoryTracker GetGroupMemoryTracker()
        {
            return groupMemoryTracker;
        }

        public override void ExposeData()
        {
            // ✅ Inicialización segura antes de serializar
            if (memoryPerPawn == null)
                memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            
            if (groupMemoryTracker == null)
                groupMemoryTracker = new DailyGroupMemoryTracker();

            Scribe_Collections.Look(ref memoryPerPawn, "memoryPerPawn", LookMode.Value, LookMode.Deep);
            Scribe_Deep.Look(ref groupMemoryTracker, "groupMemoryTracker");

            // ✅ Verificación post-carga
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (memoryPerPawn == null)
                    memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                
                if (groupMemoryTracker == null)
                    groupMemoryTracker = new DailyGroupMemoryTracker();

                Log.Message($"[EchoColony] 📖 ColonistMemoryManager cargado: {memoryPerPawn.Count} trackers de colonos");
            }
        }
    }
}