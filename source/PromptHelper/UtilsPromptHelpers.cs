using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    public static class UtilsPromptHelpers
    {
        /// <summary>
        /// Builds a formatted string representing the traits of a given pawn, including their descriptions if available.
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns></returns>
        public static string BuildTraits(Pawn pawn)
        {
            if (pawn.story?.traits == null || !pawn.story.traits.allTraits.Any())
                return "*Traits:* None";

            var entries = new List<string>();

            foreach (var t in pawn.story.traits.allTraits)
            {
                // 1. Obtener la descripción nativa traducida y formateada para el peón
                string rawDesc = t.CurrentData?.description ?? t.def?.description;
                string formattedDesc = "";

                if (!string.IsNullOrEmpty(rawDesc))
                {
                    formattedDesc = FormatText(rawDesc, pawn);
                }

                string label = t.LabelCap;

                if (!string.IsNullOrEmpty(formattedDesc))
                {
                    entries.Add($"  - {label}: \"{formattedDesc}\"");
                }
                else
                {
                    entries.Add($"  - {label}");
                }
            }

            return "*Traits & Core Identity:*\n" + string.Join("\n", entries);
        }

        /// <summary>
        /// Formats a raw text string by resolving RimWorld dynamic substitutions and removing internal UI tags and XML/HTML formatting.
        /// </summary>
        /// <param name="rawText"></param>
        /// <param name="pawn"></param>
        /// <returns></returns>
        private static string FormatText(string rawText, Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

            try
            {
                //Resolves RimWorld dynamic substitutions. ([PAWN_nameDef], [PAWN_pronoun], etc.)
                string formatted = rawText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).ToString();

                // 1. Elimina etiquetas internas de UI de RimWorld como (*Name)...(/Name)
                formatted = System.Text.RegularExpressions.Regex.Replace(formatted, @"\((\*|\/).*?\)", "");

                // 2. Elimina etiquetas de formato XML/HTML (<color=...>, <i>, etc.)
                formatted = System.Text.RegularExpressions.Regex.Replace(formatted, "<.*?>", "").Trim();

                return formatted;
            }
            catch
            {
                // Fallback defensivo si el formateador del juego falla con caracteres especiales
                string clean = System.Text.RegularExpressions.Regex.Replace(rawText, @"\((\*|\/).*?\)", "");
                return System.Text.RegularExpressions.Regex.Replace(clean, "<.*?>", "").Trim();
            }
        }
    }
}
