using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Detecta incidentes importantes y genera comentarios del storyteller
    /// Similar al EventInterceptor del Narrator's Voice pero integrado con EchoColony
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
    public static class StorytellerIncidentWatcher
    {
        [HarmonyPostfix]
        static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            try
            {
                // Solo procesar si el incidente fue exitoso
                if (!__result || __instance?.def == null)
                    return;

                // Verificar si el sistema está activo y configurado para incidentes
                if (!MyMod.Settings.IsStorytellerMessagesActive())
                    return;

                if (!MyMod.Settings.AreStorytellerIncidentMessagesEnabled())
                    return;

                // Verificar si debe comentar según la probabilidad configurada
                if (!ShouldCommentOnIncident())
                    return;

                // Solo comentar en incidentes importantes
                if (!IsImportantIncident(__instance.def))
                    return;

                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] Storyteller incident detected: {__instance.def.defName}");
                }

                // Generar comentario del storyteller
                StorytellerSpontaneousMessageSystem.GenerateSpontaneousMessage(
                    StorytellerSpontaneousMessageSystem.MessageTriggerType.Incident
                );
            }
            catch (Exception ex)
            {
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Error($"[EchoColony] Error in StorytellerIncidentWatcher: {ex.Message}");
                }
            }
        }

        private static bool ShouldCommentOnIncident()
        {
            float chance = MyMod.Settings?.storytellerIncidentChance ?? 0.3f;
            return UnityEngine.Random.value <= chance;
        }

        private static bool IsImportantIncident(IncidentDef incident)
        {
            if (incident == null) return false;

            // Lista de incidentes importantes
            var importantCategories = new[]
            {
                "ThreatBig",
                "ThreatSmall",
                "OrbitalVisitor",
                "FactionArrival",
                "DiseaseHuman",
                "AllyAssistance",
                "Ship_ChunkDrop",
                "Misc"
            };

            string category = incident.category?.defName ?? "";
            
            // Verificar categoría
            foreach (string importantCat in importantCategories)
            {
                if (category.Contains(importantCat))
                    return true;
            }

            // Verificar incidentes específicos por defName
            string defName = incident.defName.ToLower();
            if (defName.Contains("raid") || 
                defName.Contains("toxic") || 
                defName.Contains("eclipse") ||
                defName.Contains("manhunter") ||
                defName.Contains("trader") ||
                defName.Contains("wanderer") ||
                defName.Contains("quest"))
            {
                return true;
            }

            return false;
        }
    }
}