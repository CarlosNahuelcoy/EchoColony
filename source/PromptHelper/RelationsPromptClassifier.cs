using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EchoColony
{
    public static class RelationsPromptClassifier
    {
        // ── Campos estáticos en memoria (0 alocaciones durante ejecuciones) ──
        private static readonly HashSet<string> HandledDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Spouse", "Fiance", "Lover", "ExSpouse", "ExLover",
            "Parent", "Child", "Sibling", "HalfSibling", "ParentBirth",
            "Grandparent", "Grandchild", "UncleOrAunt", "NephewOrNiece", "Cousin",
            "Stepparent", "Stepchild", "ParentInLaw", "ChildInLaw"
        };

        private static readonly (string name, string tone)[] ExtendedFamilyRelations = new[]
        {
            ("Grandparent", "Grandparent/Grandchild bond"),
            ("Grandchild", "Grandparent/Grandchild bond"),
            ("UncleOrAunt", "Extended blood family"),
            ("NephewOrNiece", "Extended blood family"),
            ("Cousin", "Extended blood family"),
            ("Stepparent", "Step-family bond"),
            ("Stepchild", "Step-family bond"),
            ("ParentInLaw", "In-laws by marriage"),
            ("ChildInLaw", "In-laws by marriage")
        };

        /// <summary>
        /// Calculate the complete narrative tone block [TONE: ...] for a pair of colonists.
        /// </summary>
        public static string GetFamilyTone(Pawn a, Pawn b, List<PawnRelationDef> relDefs, int opinionAB, int opinionBA)
        {
            if (relDefs == null || !relDefs.Any()) return null;

            var tones = new List<string>();
            var relSet = new HashSet<PawnRelationDef>(relDefs);

            bool Has(PawnRelationDef def) => relSet.Contains(def);
            bool HasName(string name) => relDefs.Any(r => r.defName.Equals(name, StringComparison.OrdinalIgnoreCase));

            // Romantic relationships
            AddIfExists(relSet, PawnRelationDefOf.Spouse, tones, "Spouses (intimate connection, marital warmth or tension)");
            AddIfExists(relSet, PawnRelationDefOf.Fiance, tones, "Fiancés (engaged, romantic anticipation)");
            AddIfExists(relSet, PawnRelationDefOf.Lover, tones, "Romantic partners (affectionate, flirtatious)");

            if (Has(PawnRelationDefOf.ExSpouse) || Has(PawnRelationDefOf.ExLover))
            {
                string dynamic = (opinionAB + opinionBA) / 2 < -10
                    ? "lingering bitterness or resentment"
                    : "awkward past intimacy or polite distance";
                AddUniqueTone(tones, $"Ex-partners ({dynamic})");
            }

            // Immediate family
            AddIfNameExists("ParentBirth", relDefs, tones, "Birth Parent (biological surrogate/growth vat origin)");
            if (Has(PawnRelationDefOf.Parent) || Has(PawnRelationDefOf.Child))
                AddUniqueTone(tones, "Parent and Child (protective, guiding, or generational dynamic)");
            if (Has(PawnRelationDefOf.Sibling) || Has(PawnRelationDefOf.HalfSibling))
                AddUniqueTone(tones, "Siblings (shared upbringing, sibling banter, or loyalty)");

            // Extended family
            foreach (var (name, tone) in ExtendedFamilyRelations)
                AddIfNameExists(name, relDefs, tones, tone);

            // Custom mod relationships
            foreach (var rel in relDefs)
            {
                if (rel != null && !HandledDefNames.Contains(rel.defName))
                {
                    string customTone = GetCustomRelationTone(rel, a, b);
                    if (!string.IsNullOrWhiteSpace(customTone))
                        AddUniqueTone(tones, customTone);
                }
            }

            if (!tones.Any()) return null;

            // Overlap detection / Incest
            bool isRomantic = Has(PawnRelationDefOf.Spouse) || Has(PawnRelationDefOf.Fiance) || Has(PawnRelationDefOf.Lover);
            bool isBloodFamily = Has(PawnRelationDefOf.Parent) || Has(PawnRelationDefOf.Child) ||
                                 Has(PawnRelationDefOf.Sibling) || Has(PawnRelationDefOf.HalfSibling) ||
                                 HasName("ParentBirth");

            string overlap = isRomantic && isBloodFamily
                ? " [INCESTUOUS/OVERLAPPING RELATIONSHIP: These pawns share both blood family and romantic ties. Reflect this unique dynamic naturally.]"
                : "";

            return $"[TONE: {string.Join(" AND ", tones)}.{overlap}]";
        }

        /// <summary>
        /// Gets the tone for a non-vanilla relationship (mod definition or custom XML).
        /// </summary>
        public static string GetCustomRelationTone(PawnRelationDef rel, Pawn a, Pawn b)
        {
            if (rel == null) return null;

            var customToneDef = DefDatabase<RelationToneAIPromptDef>.AllDefsListForReading
                .FirstOrDefault(d => d.relationDefName.Equals(rel.defName, StringComparison.OrdinalIgnoreCase));

            if (customToneDef != null && !string.IsNullOrWhiteSpace(customToneDef.tone))
                return customToneDef.tone;

            string customLabel = rel.GetGenderSpecificLabelCap(b);
            if (customLabel.NullOrEmpty())
            {
                if(rel.LabelCap.NullOrEmpty())
                    customLabel = rel.LabelCap;
                else customLabel = rel.defName;
            }

            return $"Custom relationship: {customLabel}";
        }

        /// <summary>
        /// Maps a numerical opinion value (-100 to 100) to a qualitative term for the prompt.
        /// </summary>
        public static string GetOpinionLabel(int opinion)
        {
            if (opinion >= 60) return "very close";
            if (opinion >= 30) return "friendly";
            if (opinion <= -60) return "despises";
            if (opinion <= -30) return "dislikes";
            return "neutral";
        }

        // ── Métodos Auxiliares ───────────────────────────────────────────────────
        private static void AddUniqueTone(List<string> tones, string tone)
        {
            if (!tones.Contains(tone))
                tones.Add(tone);
        }

        private static void AddIfExists(HashSet<PawnRelationDef> relSet, PawnRelationDef def, List<string> tones, string tone)
        {
            if (relSet.Contains(def))
                AddUniqueTone(tones, tone);
        }

        private static void AddIfNameExists(string name, List<PawnRelationDef> relDefs, List<string> tones, string tone)
        {
            if (relDefs.Any(r => r.defName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                AddUniqueTone(tones, tone);
        }
    }
}
