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

        // ✅ NUEVO: Flag para controlar si el sistema está habilitado
        public static bool IsMemorySystemEnabled
        {
            get { return MyMod.Settings?.enableMemorySystem ?? false; }
        }

        // ✅ NUEVO: Tracking para detectar cambios de partida
        private string lastGameWorldName = "";

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
            // ✅ Si el sistema está deshabilitado, devolver tracker vacío NO persistente
            if (!IsMemorySystemEnabled)
            {
                return new ColonistMemoryTracker(pawn); // Tracker temporal que no se guarda
            }

            string id = pawn.ThingID;
            if (!memoryPerPawn.ContainsKey(id))
            {
                var tracker = new ColonistMemoryTracker(pawn);
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
            // ✅ Si el sistema está deshabilitado, devolver tracker vacío NO persistente
            if (!IsMemorySystemEnabled)
            {
                return new DailyGroupMemoryTracker(); // Tracker temporal que no se guarda
            }

            return groupMemoryTracker;
        }

        // ✅ CRÍTICO: GameComponentTick para detectar cambios de partida
        public override void GameComponentTick()
        {
            // Solo verificar cada 60 ticks (1 segundo) para no impactar rendimiento
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                CheckForGameChange();
            }
        }

        private void CheckForGameChange()
        {
            string currentWorldName = Current.Game?.World?.info?.name ?? "";
            
            // ✅ DETECTAR cambio de partida por nombre del mundo
            if (!string.IsNullOrEmpty(lastGameWorldName) && lastGameWorldName != currentWorldName)
            {
                Log.Message($"[EchoColony] 🔄 Cambio de partida detectado: '{lastGameWorldName}' -> '{currentWorldName}'");
                
                // ✅ LIMPIAR memorias de la partida anterior
                memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                groupMemoryTracker = new DailyGroupMemoryTracker();
                
                Log.Message("[EchoColony] 🧹 Memorias limpiadas para nueva partida");
            }
            
            lastGameWorldName = currentWorldName;
        }

        // ✅ CORREGIDO: ExposeData que maneja correctamente la persistencia
        public override void ExposeData()
        {
            // ✅ TRACKING: Guardar nombre del mundo actual
            string currentWorldName = Current.Game?.World?.info?.name ?? "";
            
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Log.Message($"[EchoColony] 💾 Guardando memorias para mundo '{currentWorldName}'");
            }

            // ✅ Inicialización segura siempre
            if (memoryPerPawn == null)
                memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            
            if (groupMemoryTracker == null)
                groupMemoryTracker = new DailyGroupMemoryTracker();

            // ✅ GUARDAR/CARGAR independientemente de configuración
            // Esto permite cargar memorias existentes incluso si el sistema está deshabilitado
            Scribe_Collections.Look(ref memoryPerPawn, "memoryPerPawn", LookMode.Value, LookMode.Deep);
            Scribe_Deep.Look(ref groupMemoryTracker, "groupMemoryTracker");
            Scribe_Values.Look(ref lastGameWorldName, "lastGameWorldName", "");

            // ✅ POST-LOAD: Verificación y limpieza condicional
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Verificar integridad de datos
                if (memoryPerPawn == null)
                    memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                
                if (groupMemoryTracker == null)
                    groupMemoryTracker = new DailyGroupMemoryTracker();

                Log.Message($"[EchoColony] 📖 Memorias cargadas para '{currentWorldName}': {memoryPerPawn.Count} colonos");

                // ✅ Re-asignar referencias de pawns después de cargar
                ReassignPawnReferences();

                // ✅ Si el sistema está deshabilitado, limpiar memorias cargadas
                if (!IsMemorySystemEnabled)
                {
                    if (memoryPerPawn.Count > 0)
                    {
                        Log.Message("[EchoColony] 🚫 Sistema de memorias deshabilitado - limpiando memorias cargadas");
                        memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
                        groupMemoryTracker = new DailyGroupMemoryTracker();
                    }
                }

                // ✅ Actualizar tracking de mundo
                lastGameWorldName = currentWorldName;
            }
        }

        // ✅ NUEVO: Método para re-asignar referencias de pawns
        private void ReassignPawnReferences()
        {
            if (memoryPerPawn == null || memoryPerPawn.Count == 0)
                return;

            var allColonists = Find.CurrentMap?.mapPawns?.FreeColonists;
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
                Log.Message($"[EchoColony] 🔗 Re-asignados {reassigned} colonos a sus trackers de memoria");
            }
        }

        // ✅ NUEVO: Método de debug para verificar estado
        public void DebugPrintMemoryState()
        {
            string worldName = Current.Game?.World?.info?.name ?? "Unknown";
            Log.Message($"[EchoColony] 🔍 DEBUG Estado del sistema de memorias:");
            Log.Message($"[EchoColony]   - Mundo actual: '{worldName}'");
            Log.Message($"[EchoColony]   - Último mundo conocido: '{lastGameWorldName}'");
            Log.Message($"[EchoColony]   - Sistema habilitado: {IsMemorySystemEnabled}");
            Log.Message($"[EchoColony]   - Colonos con memorias: {memoryPerPawn?.Count ?? 0}");
            
            if (groupMemoryTracker != null)
            {
                var groupCount = groupMemoryTracker.GetAllGroupMemories()?.Count ?? 0;
                Log.Message($"[EchoColony]   - Grupos con memorias: {groupCount}");
            }

            if (memoryPerPawn != null && memoryPerPawn.Count > 0)
            {
                foreach (var kvp in memoryPerPawn.Take(3)) // Mostrar solo los primeros 3
                {
                    var stats = kvp.Value?.GetMemoryStats();
                    Log.Message($"[EchoColony]     - {kvp.Key}: {stats?.total ?? 0} memorias");
                }
            }
        }

        // ✅ NUEVO: Método para forzar limpieza manual (útil para debugging)
        public void ForceCleanMemories()
        {
            int colonistCount = memoryPerPawn?.Count ?? 0;
            int groupCount = groupMemoryTracker?.GetAllGroupMemories()?.Count ?? 0;

            memoryPerPawn = new Dictionary<string, ColonistMemoryTracker>();
            groupMemoryTracker = new DailyGroupMemoryTracker();
            lastGameWorldName = "";

            Log.Message($"[EchoColony] 🗑️ Limpieza forzada completada: {colonistCount} colonos, {groupCount} grupos");
            Messages.Message($"EchoColony: Memorias limpiadas ({colonistCount} colonos, {groupCount} grupos)", 
                           MessageTypeDefOf.TaskCompletion);
        }

        // ✅ NUEVO: Validar integridad del sistema
        public bool ValidateMemoryIntegrity()
        {
            try
            {
                if (memoryPerPawn == null || groupMemoryTracker == null)
                {
                    Log.Warning("[EchoColony] ⚠️ Referencias de memoria nulas detectadas");
                    return false;
                }

                // Verificar que las referencias de pawn no sean nulas
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
                    Log.Warning($"[EchoColony] ⚠️ {invalidTrackers} trackers inválidos encontrados");
                    return false;
                }

                Log.Message("[EchoColony] ✅ Integridad del sistema de memorias verificada");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Error verificando integridad de memorias: {ex.Message}");
                return false;
            }
        }
    }
}