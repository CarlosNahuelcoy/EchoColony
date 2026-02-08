using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Evalúa la situación general de la colonia para trigger mensajes
    /// STUB: Implementación futura para Phase 2
    /// </summary>
    public static class ColonySituationEvaluator
    {
        /// <summary>
        /// Evalúa si hay situaciones de colonia que requieren mensaje
        /// </summary>
        public static void EvaluateColonySituation()
        {
            // TODO: Implementar en Phase 2
            // Evaluará:
            // - Comida baja (< 5 días)
            // - Medicina agotándose
            // - Múltiples colonos heridos
            // - Moral general muy baja
            // - Falta de energía
            // - etc.

            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return;

            // Placeholder para implementación futura
        }

        /// <summary>
        /// Verifica situación de comida
        /// </summary>
        private static bool IsLowOnFood(out string description)
        {
            description = "";

            if (Find.CurrentMap == null)
                return false;

            var resourceCounter = Find.CurrentMap.resourceCounter;
            if (resourceCounter == null)
                return false;

            float totalFood = resourceCounter.TotalHumanEdibleNutrition;
            int colonistCount = Find.CurrentMap.mapPawns.FreeColonistsCount;

            if (colonistCount == 0)
                return false;

            // Calcular días de comida (asumiendo ~2 nutrition por colono por día)
            float daysOfFood = totalFood / (colonistCount * 2f);

            if (daysOfFood < 3f)
            {
                description = $"We're dangerously low on food - only {daysOfFood:F1} days left";
                return true;
            }
            else if (daysOfFood < 5f)
            {
                description = $"Our food supplies are running low - about {daysOfFood:F1} days remaining";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica situación de medicina
        /// </summary>
        private static bool IsLowOnMedicine(out string description)
        {
            description = "";

            if (Find.CurrentMap == null)
                return false;

            var resourceCounter = Find.CurrentMap.resourceCounter;
            if (resourceCounter == null)
                return false;

            int medicine = resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                          resourceCounter.GetCount(ThingDefOf.MedicineUltratech);

            int colonistCount = Find.CurrentMap.mapPawns.FreeColonistsCount;

            if (medicine < colonistCount * 2)
            {
                description = "We're running out of medicine";
                return true;
            }

            return false;
        }
    }
}