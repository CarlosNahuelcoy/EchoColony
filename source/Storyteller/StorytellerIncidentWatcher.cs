using System;
using System.Text;
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
                if (!__result || __instance?.def == null || Current.ProgramState != ProgramState.Playing)
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

                // Las amenazas grandes ignoran el cooldown entre comentarios
                bool bigThreat = __instance.def.category == IncidentCategoryDefOf.ThreatBig;

                StorytellerSpontaneousMessageSystem.GenerateSpontaneousMessage(
                    StorytellerSpontaneousMessageSystem.MessageTriggerType.Incident,
                    situation: BuildIncidentSituation(__instance.def, parms),
                    eventKey: "incident:" + __instance.def.defName,
                    priority: bigThreat
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

        /// <summary>
        /// Describe el incidente para el prompt: qué fue, de qué tipo, contra quién y con qué fuerza.
        /// </summary>
        private static string BuildIncidentSituation(IncidentDef def, IncidentParms parms)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Event you just triggered: {def.LabelCap}");
            if (def.category != null)
                sb.AppendLine($"Category: {def.category.LabelCap}");

            string details = StorytellerEventText.Clean(def.description);
            if (details.Length == 0)
                details = StorytellerEventText.Clean(def.letterText);
            if (details.Length > 0)
                sb.AppendLine($"Details: {details}");

            if (parms != null)
            {
                if (parms.faction != null)
                    sb.AppendLine($"Faction involved: {parms.faction.Name}");
                if (parms.points > 0 && def.category?.defName?.Contains("Threat") == true)
                    sb.AppendLine($"Threat strength: {parms.points:F0} points");
                if (parms.target is Map map && map != Find.CurrentMap)
                    sb.AppendLine($"Location: {map.Parent?.LabelCap ?? "another map"}");
            }

            return sb.ToString();
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