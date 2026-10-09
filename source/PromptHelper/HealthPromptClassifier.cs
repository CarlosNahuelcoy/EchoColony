using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    public static class HealthPromptClassifier
    {
        public static List<Hediff> GetHediffPawn (Pawn pawn)
        {
            if (pawn.health?.hediffSet?.hediffs == null) return new List<Hediff>();

            var hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList();
            return hediffs;
        }

        /// <summary>
        /// Appends a summary of critical health conditions and capacities to the healthStatus list, including pregnancy, health percentage, bleeding, pain, and functional impairments.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendCriticalAndCapacities(Pawn pawn, List<Hediff> hediffs, List<string> healthStatus)
        {
            var details = new List<string>();

            // 1. Pregnancy Detection (Biotech, RJW, and other mods)
            var pregnancy = hediffs.FirstOrDefault(h =>
                h.def.defName.ToLower().Contains("pregnant") ||
                h.def.defName.ToLower().Contains("pregnancy"));

            if (pregnancy != null)
            {
                //If the hediff has severity or stages (e.g., advanced),
                // it will use the official translated label from the game/mod (e.g., "pregnant" or "pregnant (advanced)")
                string pregnancyLabel = pregnancy.LabelCap.ToLower();
                details.Add(string.IsNullOrEmpty(pregnancyLabel) ? "pregnant" : pregnancyLabel);
            }

            float healthPercent = pawn.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            if (healthPercent < 0.25f) details.Add("barely clinging to life");
            else if (healthPercent < 0.5f) details.Add("badly injured and weakened");
            else if (healthPercent < 0.75f) details.Add("wounded but functional");

            float totalBleedRate = pawn.health.hediffSet.BleedRateTotal;
            if (totalBleedRate > 0.4f) details.Add("bleeding profusely");
            else if (totalBleedRate > 0.1f) details.Add("bleeding from wounds");

            float pain = pawn.health.hediffSet.PainTotal;
            if (pain > 0.4f) details.Add("in severe pain");
            else if (pain > 0.2f) details.Add("dealing with pain");

            if (pawn.health?.capacities != null)
            {
                var consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                var moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
                var manipulation = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);

                if (consciousness < 0.6f) details.Add("impaired consciousness");
                if (moving < 0.5f) details.Add("can barely walk");
                if (manipulation < 0.5f) details.Add("can barely use hands");
            }

            if (details.Any())
            {
                healthStatus.Add($"Physical state: {string.Join(", ", details)}");
            }
        }

        /// <summary>
        /// Appends a summary of injuries and scars to the healthStatus list, including severity and affected body parts.
        /// </summary>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendInjuries(List<Hediff> hediffs, List<string> healthStatus)
        {
            var injuries = hediffs.OfType<Hediff_Injury>()
                .Where(i => i.Visible)
                .ToList();

            if (!injuries.Any()) return;

            // Distinguish between open or recent wounds and permanent scars.
            var freshInjuries = injuries.Where(i => !i.IsPermanent()).ToList();
            var scars = injuries.Where(i => i.IsPermanent()).ToList();

            // Processing open wounds
            if (freshInjuries.Any())
            {
                ProcessInjuryGroup(freshInjuries, healthStatus, "Wounds", isScar: false);
            }

            // Processing permanent scars
            if (scars.Any())
            {
                ProcessInjuryGroup(scars, healthStatus, "Scars", isScar: true);
            }
        }

        /// <summary>
        /// Processes a group of injuries or scars, summarizing the most severe ones and providing a count of additional injuries/scars.
        /// </summary>
        /// <param name="injuryList"></param>
        /// <param name="healthStatus"></param>
        /// <param name="categoryName"></param>
        /// <param name="isScar"></param>
        private static void ProcessInjuryGroup(List<Hediff_Injury> injuryList, List<string> healthStatus, string categoryName, bool isScar)
        {
            var sorted = injuryList.OrderByDescending(i => i.Severity).ToList();

            //Extract the 3 most serious ones for explicit detail.
            var top3 = sorted.Take(3).ToList();

            var topDetails = top3
                .GroupBy(i => i.Part != null ? i.Part.LabelCap.ToString() : "Body")
                .Select(group =>
                {
                    int count = group.Count();
                    string part = group.Key;

                    if (count > 1)
                    {
                        string pluralTerm = isScar ? "scars" : "wounds";
                        return $"{count} {pluralTerm} on {part}";
                    }

                    var injury = group.First();
                    string sevLabel = injury.Severity > 10f ? "severe" : injury.Severity > 5f ? "serious" : "minor";

                    // If it is a scar, we specify the type of original wound in parentheses.
                    if (isScar)
                    {
                        return $"{sevLabel} scar ({injury.def.label}) on {part}";
                    }

                    return $"{sevLabel} {injury.def.label} on {part}";
                });

            string topText = string.Join(", ", topDetails);

            // 3. Evaluate the remaining wounds/scars (from the 4th onwards)
            var remaining = sorted.Skip(3).ToList();

            if (remaining.Any())
            {
                int severeCount = remaining.Count(i => i.Severity > 10f);
                int seriousCount = remaining.Count(i => i.Severity > 5f && i.Severity <= 10f);
                int minorCount = remaining.Count(i => i.Severity <= 5f);

                var summaryParts = new List<string>();
                if (severeCount > 0) summaryParts.Add($"{severeCount} other severe");
                if (seriousCount > 0) summaryParts.Add($"{seriousCount} serious");
                if (minorCount > 0) summaryParts.Add($"{minorCount} minor");

                string remainingSummary = string.Join(", ", summaryParts);
                string itemTerm = remaining.Count == 1
                    ? (isScar ? "scar" : "wound")
                    : (isScar ? "scars" : "wounds");

                healthStatus.Add($"{categoryName}: {topText} (plus {remainingSummary} additional {itemTerm} across your body).");
            }
            else
            {
                healthStatus.Add($"{categoryName}: {topText}.");
            }
        }

        /// <summary>
        /// Appends a summary of prosthetics and missing body parts to the healthStatus list, including any functional notes.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendProstheticsAndMissingParts(Pawn pawn, List<Hediff> hediffs, List<string> healthStatus)
        {
            // 1. Obtain all parts replaced by a prosthetic
            var prosthetics = hediffs.Where(h => h.def.addedPartProps != null && h.Part != null && HediffClassifier.IsRealImplantOrProsthetic(h)).ToList();
            var prostheticParts = new HashSet<BodyPartRecord>(prosthetics.Select(h => h.Part));

            // 2. Obtain actual missing parts (that don't have a prosthetic on top)
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                    .Where(h => h.Part != null && !IsPartCoveredByProsthetic(h.Part, prostheticParts))
                    .Select(h => h.Part.Label)
                    .Distinct()
                    .ToList();

            if (!prosthetics.Any() && !missingParts.Any()) return;

            var formattedItems = new List<string>();
            bool hasDetailedNotes = false;

            // 3. Process prosthetics by grouping them by (Label, Note) to unify pairs of parts
            if (prosthetics.Any())
            {
                var grouped = prosthetics
                    .Select(p => {
                        var (label, note, _) = HediffClassifier.GetResolvedPrompt(p);
                        return new { Hediff = p, Label = label, Note = note };
                    })
                    .GroupBy(x => new { x.Label, x.Note });

                foreach (var group in grouped)
                {
                    string label = group.Key.Label;
                    string note = group.Key.Note;
                    var parts = group.Select(g => g.Hediff.Part?.Label).Where(p => !string.IsNullOrEmpty(p)).Distinct();
                    string partsStr = parts.Any() ? $" ({string.Join(" and ", parts)})" : "";

                    if (!string.IsNullOrEmpty(note))
                    {
                        hasDetailedNotes = true;
                        formattedItems.Add($"{label}{partsStr}: {note.TrimEnd('.')} ");
                    }
                    else
                    {
                        formattedItems.Add($"{label}{partsStr}");
                    }
                }
            }

            // 4. Add missing parts
            if (missingParts.Any())
            {
                formattedItems.Add($"missing ({string.Join(" and ", missingParts)})");
            }

            // 5. Conditional output: Bullets if there are complex explanations, single line if simple
            if (hasDetailedNotes)
            {
                healthStatus.Add("Prosthetics & Body Modifications:\n" + string.Join("\n", formattedItems.Select(item => $"- {item.TrimEnd('.')}.")));
            }
            else
            {
                healthStatus.Add($"Body modifications: {string.Join(", ", formattedItems)}.");
            }
        }

        /// <summary>
        /// Appends a summary of implants to the healthStatus list, including any functional notes. 
        /// </summary>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendImplants(List<Hediff> hediffs, List<string> healthStatus)
        {
            var implantHediffs = hediffs.Where(h =>
                h.def.addedPartProps == null &&
                !h.def.isBad &&
                HediffClassifier.IsRealImplantOrProsthetic(h)
            ).ToList();

            if (!implantHediffs.Any()) 
                return;

            var formattedItems = new List<string>();
            bool hasDetailedNotes = false;

            // We group by (Label, Note)
            var grouped = implantHediffs
                .Select(h => {
                    var (label, note, _) = HediffClassifier.GetResolvedPrompt(h);
                    return new { Hediff = h, Label = label, Note = note };
                })
                .GroupBy(x => new { x.Label, x.Note });

            foreach (var group in grouped)
            {
                string label = group.Key.Label;
                string note = group.Key.Note;
                var parts = group.Select(g => g.Hediff.Part?.LabelCap.ToString()).Where(p => !string.IsNullOrEmpty(p)).Distinct();
                string partsStr = parts.Any() ? $" ({string.Join(" and ", parts)})" : "";

                if (!string.IsNullOrEmpty(note))
                {
                    hasDetailedNotes = true;
                    formattedItems.Add($"{label}{partsStr}: {note.TrimEnd('.')} ");
                }
                else
                {
                    formattedItems.Add($"{label}{partsStr}");
                }
            }

            if (hasDetailedNotes)
            {
                healthStatus.Add("Implants:\n" + string.Join("\n", formattedItems.Select(item => $"- {item.TrimEnd('.')}.")));
            }
            else
            {
                healthStatus.Add($"Implants: {string.Join(", ", formattedItems)}.");
            }
        }

        /// <summary>
        /// Appends a summary of addictions and withdrawals to the healthStatus list, including severity and stage information.
        /// </summary>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendAddictionsAndWithdrawals(List<Hediff> hediffs, List<string> healthStatus)
        {
            var addictions = hediffs.Where(h => h.def?.defName != null && h.def.defName.EndsWith("Addiction")).ToList();
            var withdrawals = hediffs.Where(h => h.def?.defName != null && h.def.defName.EndsWith("Withdrawal")).ToList();

            if (!addictions.Any() && !withdrawals.Any()) return;

            string GetCleanDrugName(Hediff h)
            {
                // A) If it is a native Hediff_Addiction, the chemical substance gives us the exact name.
                if (h is Hediff_Addiction addComp && addComp.Chemical != null)
                {
                    return addComp.Chemical.label;
                }

                // B) Otherwise, we trim the defName from the XML definition (e.g., "AlcoholAddiction" -> "alcohol").
                string defName = h.def.defName;
                if (defName.EndsWith("Addiction"))
                    return defName.Substring(0, defName.Length - "Addiction".Length).ToLower();
                if (defName.EndsWith("Withdrawal"))
                    return defName.Substring(0, defName.Length - "Withdrawal".Length).ToLower();

                return h.def.label;
            }

            var details = new List<string>();
            var processedDrugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process Addictions
            foreach (var add in addictions)
            {
                string drugName = GetCleanDrugName(add);
                processedDrugs.Add(drugName);

                // Check for active withdrawal by directly comparing the clean names.
                var withdrawal = withdrawals.FirstOrDefault(w =>
                    GetCleanDrugName(w).Equals(drugName, StringComparison.OrdinalIgnoreCase));

                if (withdrawal != null)
                {
                    string stageLabel = withdrawal.CurStage?.label;
                    if (string.IsNullOrEmpty(stageLabel))
                    {
                        stageLabel = withdrawal.Severity > 0.7f ? "severe" : withdrawal.Severity > 0.3f ? "moderate" : "initial";
                    }

                    details.Add($"Addicted to {drugName} — currently suffering from {stageLabel} withdrawal (experiencing intense cravings and distress)");
                }
                else
                {
                    details.Add($"Addicted to {drugName} (chemically dependent, but currently satisfied)");
                }
            }

            // 2. Process orphaned withdrawals (without the main addiction hediff)
            foreach (var wit in withdrawals)
            {
                string drugName = GetCleanDrugName(wit);

                if (!processedDrugs.Contains(drugName))
                {
                    processedDrugs.Add(drugName);

                    string stageLabel = wit.CurStage?.label;
                    if (string.IsNullOrEmpty(stageLabel))
                    {
                        stageLabel = wit.Severity > 0.7f ? "severe" : wit.Severity > 0.3f ? "moderate" : "initial";
                    }

                    details.Add($"Suffering from {drugName} withdrawal ({stageLabel})");
                }
            }

            if (details.Any())
            {
                healthStatus.Add($"Chemical dependencies: {string.Join("; ", details)}.");
            }
        }

        /// <summary>
        /// Appends a summary of acute illnesses, infections, and chronic conditions to the healthStatus list.
        /// </summary>
        /// <param name="hediffs"></param>
        /// <param name="healthStatus"></param>
        public static void AppendDiseasesAndInfections(List<Hediff> hediffs, List<string> healthStatus)
        {
            if (hediffs == null || !hediffs.Any())
                return;

            // --- A. ACUTE ILLNESSES AND INFECTIONS ---
            var activeDiseases = hediffs
                .Where(h => h.Visible && IsAcuteDisease(h))
                .ToList();

            if (activeDiseases.Any())
            {
                var formattedAcute = new List<string>();

                foreach (var disease in activeDiseases)
                {
                    var (label, note, _) = HediffClassifier.GetResolvedPrompt(disease);
                    string stage = HediffClassifier.GetStageSuffix(disease, label);

                    // If it's a local wound infection
                    if (disease.def.defName == "WoundInfection")
                    {
                        string partLabel = disease.Part != null ? disease.Part.Label : "body";
                        formattedAcute.Add($"infected wound on {partLabel}{stage}");
                        continue;
                    }

                    // Typical acute illness
                    if (!string.IsNullOrEmpty(note))
                    {
                        formattedAcute.Add($"{label}{stage}: {note.TrimEnd('.')}");
                    }
                    else
                    {
                        formattedAcute.Add($"{label}{stage}");
                    }
                }

                // If an acute illness has an explanatory note, we will use bullet points.
                bool hasDetailedNotes = activeDiseases.Any(h => !string.IsNullOrEmpty(HediffClassifier.GetResolvedPrompt(h).note));

                if (hasDetailedNotes)
                {
                    healthStatus.Add("Acute Illnesses:\n" + string.Join("\n", formattedAcute.Select(d => $"- {d}.")));
                }
                else
                {
                    healthStatus.Add($"Acute Illnesses: {string.Join(", ", formattedAcute)}.");
                }
            }

            // --- B. CHRONIC DISEASES ---
            var chronicDiseases = hediffs
                .Where(h => h.Visible && IsChronicDisease(h))
                .GroupBy(h => HediffClassifier.GetResolvedPrompt(h).label, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (chronicDiseases.Any())
            {
                var formattedChronic = new List<string>();

                foreach (var chronic in chronicDiseases)
                {
                    var (label, note, _) = HediffClassifier.GetResolvedPrompt(chronic);
                    string stage = HediffClassifier.GetStageSuffix(chronic, label);

                    if (!string.IsNullOrEmpty(note))
                    {
                        formattedChronic.Add($"{label}{stage}: {note.TrimEnd('.')}");
                    }
                    else
                    {
                        formattedChronic.Add($"{label}{stage}");
                    }
                }

                bool hasDetailedNotes = chronicDiseases.Any(h => !string.IsNullOrEmpty(HediffClassifier.GetResolvedPrompt(h).note));

                if (hasDetailedNotes)
                {
                    healthStatus.Add("Chronic Conditions:\n" + string.Join("\n", formattedChronic.Select(c => $"- {c}.")));
                }
                else
                {
                    healthStatus.Add($"Chronic Conditions: {string.Join(", ", formattedChronic)}.");
                }
            }
        }

        /// <summary>
        /// Returns true if the given body part or any of its ancestors has a prosthetic or bionic installed,
        /// meaning missing part hediffs below it should not be listed as truly missing.
        /// </summary>
        private static bool IsPartCoveredByProsthetic(BodyPartRecord part, HashSet<BodyPartRecord> prostheticParts)
        {
            var current = part;
            while (current != null)
            {
                if (prostheticParts.Contains(current)) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Determines if a given Hediff represents an acute disease or infection.
        /// </summary>
        /// <param name="hediff"></param>
        /// <returns></returns>
        public static bool IsAcuteDisease(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
                return false;

            HediffDef def = hediff.def;

            // 1. Rule out if it is not something negative or if it is a wound/missing part.
            if (!def.isBad || def.chronic)
                return false;

            if (hediff is Hediff_Injury || hediff is Hediff_MissingPart)
                return false;

            // 2. Direct filter based on infection or thoughts of illness
            if (def.isInfection || def.makesSickThought)
                return true;

            // 3. Direct filter based on typical disease components (Immunizable / Treatable by time)
            bool hasImmunizable = def.HasComp(typeof(HediffComp_Immunizable));
            bool hasTendDuration = def.HasComp(typeof(HediffComp_TendDuration));

            if (hasImmunizable || (def.tendable && hasTendDuration))
                return true;

            // 4. Specific cases of acute conditions or mental blocks (Catatonic, HeartAttack, etc.)
            if (def.defName == "HeartAttack" || def.defName == "CatatonicBreakdown" || def.defName == "FoodPoisoning")
                return true;

            return false;
        }

        public static bool IsChronicDisease(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
                return false;

            return hediff.def.isBad && hediff.def.chronic && !(hediff is Hediff_Injury);
        }

        /// <summary>
        /// Collects all behavioral directives associated with the colonist's hediffs.
        /// </summary>
        public static void AppendBehavioralDirectives(List<Hediff> hediffs, List<string> healthStatus)
        {
            if (hediffs == null || !hediffs.Any()) return;

            var directives = new List<string>();

            foreach (var h in hediffs)
            {
                if (!h.Visible) continue;

                string directive = HediffClassifier.GetResolvedPrompt(h).directive;
                if (!string.IsNullOrEmpty(directive))
                {
                    directives.Add($"  * {directive.Trim()}");
                }
            }

            if (directives.Any())
            {
                healthStatus.Add("Behavioral Directives:\n" + string.Join("\n", directives));
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class HediffClassifier
    {
        private static readonly Dictionary<string, HediffAIPromptDef> PromptDefs =
            new Dictionary<string, HediffAIPromptDef>(StringComparer.OrdinalIgnoreCase);

        private static int lastKnownDefCount = -1;

        static HediffClassifier()
        {
            EnsureDefinitionsLoaded();
        }

        /// <summary>
        /// Comprueba si la base de datos de RimWorld ha cambiado o si necesita cargar/recargar los XML.
        /// </summary>
        public static void EnsureDefinitionsLoaded()
        {
            int currentDefCount = DefDatabase<HediffAIPromptDef>.DefCount;

            // Si el recuento de Defs en RimWorld difiere del que tenemos en caché, recargamos el diccionario
            if (currentDefCount != lastKnownDefCount)
            {
                PromptDefs.Clear();
                var defs = DefDatabase<HediffAIPromptDef>.AllDefsListForReading;

                if (defs != null)
                {
                    foreach (var promptDef in defs)
                    {
                        if (!string.IsNullOrEmpty(promptDef.targetHediff))
                        {
                            PromptDefs[promptDef.targetHediff] = promptDef;
                        }
                    }
                }

                lastKnownDefCount = currentDefCount;
                Log.Message($"[EchoColony] Dynamic XML Definitions Synced -> {PromptDefs.Count} active prompt definitions mapped.");
            }
        }

        /// <summary>
        /// Returns the texts (customLabel, functionalNote, behavioralDirective), 
        /// resolving the current stage based on the hediff's severity.
        /// </summary>
        public static (string label, string note, string directive) GetResolvedPrompt(Hediff hediff)
        {
            if (hediff?.def == null) return (null, null, null);

            // Check if RimWorld's DefDatabase has fully loaded/updated.
            EnsureDefinitionsLoaded();

            if (!PromptDefs.TryGetValue(hediff.def.defName, out var promptDef))
            {
                return (hediff.def.label.ToLowerInvariant(), null, null);
            }

            string finalLabel = promptDef.customLabel ?? hediff.Label.ToLowerInvariant();
            string finalNote = promptDef.functionalNote;
            string finalDirective = promptDef.behavioralDirective;

            var stages = promptDef.stages;
            if (stages != null && stages.Count > 0)
            {
                int currentStageIndex = hediff.CurStageIndex;
                string currentStageLabel = hediff.CurStage?.label;

                HediffStagePrompt matched = null;
                HediffStagePrompt fallback = null;

                for (int i = 0; i < stages.Count; i++)
                {
                    var s = stages[i];

                    if (matched == null)
                    {
                        if (s.stageIndex == currentStageIndex)
                        {
                            matched = s;
                        }
                        else if (!string.IsNullOrEmpty(s.stageLabel)
                                 && string.Equals(s.stageLabel, currentStageLabel, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = s;
                        }
                    }

                    if (s.stageIndex != -1 && s.stageIndex <= currentStageIndex)
                    {
                        if (fallback == null || s.stageIndex > fallback.stageIndex)
                            fallback = s;
                    }
                }

                var chosen = matched ?? fallback;
                if (chosen != null)
                {
                    if (!string.IsNullOrEmpty(chosen.customLabel)) finalLabel = chosen.customLabel;
                    if (!string.IsNullOrEmpty(chosen.functionalNote)) finalNote = chosen.functionalNote;
                    if (!string.IsNullOrEmpty(chosen.behavioralDirective)) finalDirective = chosen.behavioralDirective;
                }

                
            }
            return (finalLabel, finalNote, finalDirective);
        }

        /// <summary>
        /// Returns the native stage suffix in parentheses if the label tag does not 
        /// already include it (because the XML would have added it via customLabel).
        /// </summary>
        public static string GetStageSuffix(Hediff h, string resolvedLabel)
        {
            string stageLabel = h.CurStage?.label;
            if (string.IsNullOrEmpty(stageLabel)) return string.Empty;

            // If the resolved label already contains the stage, it does not duplicate it.
            if (resolvedLabel != null &&
                resolvedLabel.IndexOf(stageLabel, StringComparison.OrdinalIgnoreCase) >= 0)
                return string.Empty;

            return $" ({stageLabel})";
        }

        /// <summary>
        /// Determines if a given Hediff represents a real implant or prosthetic.
        /// </summary>
        /// <param name="hediff"></param>
        /// <returns></returns>
        public static bool IsRealImplantOrProsthetic(Hediff hediff)
        {
            if (hediff == null || hediff.def == null) return false;

            HediffDef def = hediff.def;

            if (!def.countsAsAddedPartOrImplant)
            {
                return false;
            }

            // Check whether it inherits from the base class for implants/add-on parts or has prosthetic properties.
            return hediff is Hediff_AddedPart ||
                   hediff is Hediff_Implant ||
                   def.addedPartProps != null;
        }
    }
}
