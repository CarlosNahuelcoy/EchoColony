using System;
using System.Linq;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace EchoColony
{
    /// <summary>
    /// Ejecutor de incidentes del storyteller
    /// Maneja la lógica de ejecutar Y detener eventos en el juego
    /// </summary>
    public static class StorytellerIncidentExecutor
    {
        /// <summary>
        /// Intenta ejecutar un incidente en el mapa actual
        /// </summary>
        public static bool TryExecuteIncident(IncidentDef incidentDef)
        {
            if (incidentDef == null)
            {
                Log.Error("[EchoColony] Tried to execute null incident");
                return false;
            }

            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[EchoColony] No current map to execute incident");
                return false;
            }

            try
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                    incidentDef.category,
                    map
                );

                bool success = incidentDef.Worker.TryExecute(parms);

                if (success)
                {
                    Log.Message($"[EchoColony] Successfully executed incident: {incidentDef.defName}");
                    
                    Messages.Message(
                        $"The storyteller has triggered: {incidentDef.label}",
                        MessageTypeDefOf.NeutralEvent,
                        false
                    );
                }
                else
                {
                    Log.Warning($"[EchoColony] Failed to execute incident: {incidentDef.defName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error executing incident {incidentDef.defName}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Ejecuta un incidente con parámetros personalizados
        /// </summary>
        public static bool TryExecuteIncident(IncidentDef incidentDef, IncidentParms customParms)
        {
            if (incidentDef == null)
            {
                Log.Error("[EchoColony] Tried to execute null incident");
                return false;
            }

            try
            {
                bool success = incidentDef.Worker.TryExecute(customParms);

                if (success)
                {
                    Log.Message($"[EchoColony] Successfully executed custom incident: {incidentDef.defName}");
                }
                else
                {
                    Log.Warning($"[EchoColony] Failed to execute custom incident: {incidentDef.defName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error executing custom incident {incidentDef.defName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detiene una condición del juego activa (como lluvia tóxica, eclipse, etc)
        /// </summary>
        public static bool TryStopGameCondition(string conditionDefName)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[EchoColony] No current map to stop condition");
                return false;
            }

            var condition = map.GameConditionManager.ActiveConditions
                .FirstOrDefault(gc => gc.def.defName.Equals(conditionDefName, StringComparison.OrdinalIgnoreCase));

            if (condition != null)
            {
                condition.End();
                Log.Message($"[EchoColony] Stopped game condition: {conditionDefName}");
                Messages.Message(
                    $"The storyteller has ended: {condition.def.label}",
                    MessageTypeDefOf.PositiveEvent,
                    false
                );
                return true;
            }
            else
            {
                Log.Warning($"[EchoColony] Game condition not found or not active: {conditionDefName}");
                return false;
            }
        }

        /// <summary>
        /// Detiene TODAS las condiciones del tipo especificado (útil para "stop all toxic")
        /// </summary>
        public static int StopAllConditionsOfType(string searchTerm)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return 0;

            var matchingConditions = map.GameConditionManager.ActiveConditions
                .Where(gc => gc.def.defName.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()) ||
                            gc.def.label.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()))
                .ToList();

            int count = 0;
            foreach (var condition in matchingConditions)
            {
                condition.End();
                Log.Message($"[EchoColony] Stopped condition: {condition.def.label}");
                count++;
            }

            if (count > 0)
            {
                Messages.Message(
                    $"The storyteller has ended {count} condition(s)",
                    MessageTypeDefOf.PositiveEvent,
                    false
                );
            }

            return count;
        }

        /// <summary>
        /// Obtiene una lista de todas las condiciones activas
        /// </summary>
        public static List<GameCondition> GetActiveConditions()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return new List<GameCondition>();

            return map.GameConditionManager.ActiveConditions.ToList();
        }

        /// <summary>
        /// Verifica si una condición específica está activa
        /// </summary>
        public static bool IsConditionActive(string conditionDefName)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return false;

            return map.GameConditionManager.ActiveConditions
                .Any(gc => gc.def.defName.Equals(conditionDefName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifica si un incidente puede ejecutarse en el mapa actual
        /// </summary>
        public static bool CanExecuteIncident(IncidentDef incidentDef)
        {
            if (incidentDef == null)
                return false;

            Map map = Find.CurrentMap;
            if (map == null)
                return false;

            if (incidentDef.workerClass == null)
                return false;

            IncidentParms testParms = StorytellerUtility.DefaultParmsNow(
                incidentDef.category,
                map
            );

            return incidentDef.Worker.CanFireNow(testParms);
        }

        /// <summary>
        /// Obtiene información sobre por qué un incidente no puede ejecutarse
        /// </summary>
        public static string GetIncidentBlockedReason(IncidentDef incidentDef)
        {
            if (incidentDef == null)
                return "Incident is null";

            Map map = Find.CurrentMap;
            if (map == null)
                return "No current map";

            if (incidentDef.workerClass == null)
                return "Incident has no worker class";

            IncidentParms testParms = StorytellerUtility.DefaultParmsNow(
                incidentDef.category,
                map
            );

            if (!incidentDef.Worker.CanFireNow(testParms))
                return "Conditions not met for this incident";

            return "Unknown reason";
        }
    }
}