using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    [StaticConstructorOnStartup]
    public static class GenePromptClassifier
    {
        private static readonly Dictionary<string, string> CustomDescriptions = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> LabelOverrides = new Dictionary<string, string>();

        static GenePromptClassifier()
        {
            LoadXMLDefinitions();
        }

        private static void LoadXMLDefinitions()
        {
            foreach (var promptDef in DefDatabase<GeneAIPromptDef>.AllDefs)
            {
                if (string.IsNullOrEmpty(promptDef.targetGene)) continue;

                if (!string.IsNullOrEmpty(promptDef.customLabel))
                    LabelOverrides[promptDef.targetGene] = promptDef.customLabel;

                if (!string.IsNullOrEmpty(promptDef.promptDescription))
                    CustomDescriptions[promptDef.targetGene] = promptDef.promptDescription;
            }
        }

        /// <summary>
        /// Format the gene for the prompt. Prioritize the custom XML; if it does not exist, use the truncated native description.
        /// </summary>
        public static string FormatGeneForPrompt(Gene gene)
        {
            if (gene?.def == null) return string.Empty;

            string defName = gene.def.defName;

            // 1. Label (Use override if it exists, otherwise use the game's label)
            string label = LabelOverrides.TryGetValue(defName, out var customLabel)
                ? customLabel
                : gene.def.label;

            if (string.IsNullOrEmpty(label)) return string.Empty;

            // 2. If an XML-optimized description exists, we use it directly.
            if (CustomDescriptions.TryGetValue(defName, out var customDesc))
            {
                return $"{label} ({customDesc})";
            }

            // 3. FALLBACK: If there is no custom XML, we process the game's original description.
            string desc = gene.def.description;
            if (!string.IsNullOrEmpty(desc))
            {
                desc = System.Text.RegularExpressions.Regex.Replace(desc, "<.*?>", "").Trim();

                // We extract the first sentence to keep it short.
                int periodIndex = desc.IndexOf('.');
                if (periodIndex > 0)
                    desc = desc.Substring(0, periodIndex);

                if (desc.Length > 80)
                    desc = desc.Substring(0, 77) + "...";

                return $"{label} ({desc})";
            }

            return label;
        }
    }
}
