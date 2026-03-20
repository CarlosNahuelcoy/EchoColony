using Verse;
using System.Collections.Generic;

namespace EchoColony.Mechs
{
    public static class MechIntelligenceDetector
    {
        // Mapeo de mecanoides a nivel de inteligencia
        private static Dictionary<string, MechIntelligenceLevel> mechIntelligence = new Dictionary<string, MechIntelligenceLevel>
        {
            // Standard Subcore - Básicos (utilidad y combate ligero)
            { "Mech_Lifter", MechIntelligenceLevel.Basic },
            { "Mech_Constructoid", MechIntelligenceLevel.Basic },
            { "Mech_Agrihand", MechIntelligenceLevel.Basic },
            { "Mech_Cleansweeper", MechIntelligenceLevel.Basic },
            { "Mech_Paramedic", MechIntelligenceLevel.Basic },
            { "Mech_Fabricor", MechIntelligenceLevel.Basic },
            { "Mech_Militor", MechIntelligenceLevel.Basic }, // ← CORREGIDO: Militor es básico
            
            // High Subcore - Avanzados (combate especializado)
            { "Mech_Scorcher", MechIntelligenceLevel.Advanced },
            { "Mech_Tesseron", MechIntelligenceLevel.Advanced },
            { "Mech_Pikeman", MechIntelligenceLevel.Advanced },
            { "Mech_Scyther", MechIntelligenceLevel.Advanced },
            { "Mech_Legionary", MechIntelligenceLevel.Advanced },
            
            // Ultra Subcore - Pesados/Elite
            { "Mech_Centipede", MechIntelligenceLevel.Elite },
            { "Mech_Tunneler", MechIntelligenceLevel.Elite },
            { "Mech_Apocriton", MechIntelligenceLevel.Elite },
            
            // Self-Perpetuating - Supremos
            { "Mech_Diabolus", MechIntelligenceLevel.Supreme },
            { "Mech_WarQueen", MechIntelligenceLevel.Supreme },
        };

        public static MechIntelligenceLevel GetIntelligenceLevel(Pawn mech)
        {
            if (mech == null || mech.def == null)
                return MechIntelligenceLevel.Basic;

            // Check for override first
            var intelligenceOverride = MechPromptManager.GetIntelligenceOverride(mech);
            if (intelligenceOverride.HasValue)
            {
                return intelligenceOverride.Value;
            }

            // Otherwise use default detection
            string defName = mech.def.defName;

            if (mechIntelligence.TryGetValue(defName, out MechIntelligenceLevel level))
            {
                return level;
            }

            // Fallback detection by name
            string lowerName = defName.ToLower();
            
            if (lowerName.Contains("lifter") || lowerName.Contains("constructoid") || 
                lowerName.Contains("agrihand") || lowerName.Contains("cleansweeper") ||
                lowerName.Contains("militor") || lowerName.Contains("paramedic") ||
                lowerName.Contains("fabricor"))
            {
                return MechIntelligenceLevel.Basic;
            }
            
            if (lowerName.Contains("scyther") || lowerName.Contains("tesseron") || 
                lowerName.Contains("scorcher") || lowerName.Contains("pikeman") ||
                lowerName.Contains("legionary"))
            {
                return MechIntelligenceLevel.Advanced;
            }
            
            if (lowerName.Contains("centipede") || lowerName.Contains("apocriton") ||
                lowerName.Contains("tunneler"))
            {
                return MechIntelligenceLevel.Elite;
            }
            
            if (lowerName.Contains("queen") || lowerName.Contains("diabolus"))
            {
                return MechIntelligenceLevel.Supreme;
            }

            return MechIntelligenceLevel.Basic;
        }
        public static string GetIntelligenceDescription(MechIntelligenceLevel level)
        {
            switch (level)
            {
                case MechIntelligenceLevel.Basic:
                    return "Basic AI - Task-focused, no initiative";
                case MechIntelligenceLevel.Advanced:
                    return "Advanced AI - Tactical thinking, combat analysis";
                case MechIntelligenceLevel.Elite:
                    return "Elite AI - Complex reasoning, specialized expertise";
                case MechIntelligenceLevel.Supreme:
                    return "Supreme AI - Near-sentient, independent thought";
                default:
                    return "Unknown AI level";
            }
        }

        public static bool IsMechanoid(Pawn pawn)
        {
            return pawn?.RaceProps?.IsMechanoid == true;
        }
    }

    public enum MechIntelligenceLevel
    {
        Basic = 0,      // Standard Subcore
        Advanced = 1,   // High Subcore
        Elite = 2,      // Ultra Subcore
        Supreme = 3     // Self-Perpetuating
    }
}