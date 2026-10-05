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

        public static void AppendInjuries(List<Hediff> hediffs, List<string> healthStatus)
        {
            var injuries = hediffs.OfType<Hediff_Injury>()
                .Where(i => i.Visible)
                .ToList();

            if (!injuries.Any()) return;

            // Separar heridas abiertas/recientes de cicatrices permanentes
            var freshInjuries = injuries.Where(i => !i.IsPermanent()).ToList();
            var scars = injuries.Where(i => i.IsPermanent()).ToList();

            // Procesar heridas abiertas
            if (freshInjuries.Any())
            {
                ProcessInjuryGroup(freshInjuries, healthStatus, "Wounds", isScar: false);
            }

            // Procesar cicatrices permanentes
            if (scars.Any())
            {
                ProcessInjuryGroup(scars, healthStatus, "Scars", isScar: true);
            }
        }

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

            // 3. Evaluar las heridas/cicatrices restantes (de la 4ª en adelante)
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

        public static void AppendProstheticsAndMissingParts(Pawn pawn, List<Hediff> hediffs, List<string> healthStatus)
        {
            // 1. Obtener todas las partes sustituidas por una prótesis
            var prosthetics = hediffs.Where(h => h.def.addedPartProps != null && h.Part != null && HediffClassifier.IsRealImplantOrProsthetic(h)).ToList();
            var prostheticParts = new HashSet<BodyPartRecord>(prosthetics.Select(h => h.Part));

            // 2. Obtener partes faltantes reales (que no tengan una prótesis encima)
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                    .Where(h => h.Part != null && !IsPartCoveredByProsthetic(h.Part, prostheticParts))
                    .Select(h => h.Part.Label)
                    .Distinct()
                    .ToList();

            if (!prosthetics.Any() && !missingParts.Any()) return;

            var formattedItems = new List<string>();
            bool hasDetailedNotes = false;

            // 3. Procesar las prótesis agrupando por (Label, Note) para unificar partes pares
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

            // 4. Agregar partes faltantes
            if (missingParts.Any())
            {
                formattedItems.Add($"missing ({string.Join(" and ", missingParts)})");
            }

            // 5. Salida condicional: Viñetas si hay explicaciones complejas, línea única si es simple
            if (hasDetailedNotes)
            {
                healthStatus.Add("Prosthetics & Body Modifications:\n" + string.Join("\n", formattedItems.Select(item => $"- {item.TrimEnd('.')}.")));
            }
            else
            {
                healthStatus.Add($"Body modifications: {string.Join(", ", formattedItems)}.");
            }
        }

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

            // Agrupamos por (Label, Note)
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

        public static void AppendAddictionsAndWithdrawals(List<Hediff> hediffs, List<string> healthStatus)
        {
            var addictions = hediffs.Where(h => h.def?.defName != null && h.def.defName.EndsWith("Addiction")).ToList();
            var withdrawals = hediffs.Where(h => h.def?.defName != null && h.def.defName.EndsWith("Withdrawal")).ToList();

            if (!addictions.Any() && !withdrawals.Any()) return;

            string GetCleanDrugName(Hediff h)
            {
                // A) Si es un Hediff_Addiction nativo, la sustancia química nos da el nombre exacto
                if (h is Hediff_Addiction addComp && addComp.Chemical != null)
                {
                    return addComp.Chemical.label;
                }

                // B) Si no, recortamos el defName de la definición XML (ej: "AlcoholAddiction" -> "alcohol")
                string defName = h.def.defName;
                if (defName.EndsWith("Addiction"))
                    return defName.Substring(0, defName.Length - "Addiction".Length).ToLower();
                if (defName.EndsWith("Withdrawal"))
                    return defName.Substring(0, defName.Length - "Withdrawal".Length).ToLower();

                return h.def.label;
            }

            var details = new List<string>();
            var processedDrugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Procesar Adicciones
            foreach (var add in addictions)
            {
                string drugName = GetCleanDrugName(add);
                processedDrugs.Add(drugName);

                // Buscar si existe una abstinencia activa comparando directamente los nombres limpios
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

            // 2. Procesar Abstinencias huérfanas (sin el hediff de adicción principal)
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

        public static void AppendDiseasesAndInfections(List<Hediff> hediffs, List<string> healthStatus)
        {
            if (hediffs == null || !hediffs.Any())
                return;

            // --- A. ENFERMEDADES AGUDAS E INFECCIONES ---
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

                    // Si es una infección local de herida
                    if (disease.def.defName == "WoundInfection")
                    {
                        string partLabel = disease.Part != null ? disease.Part.Label : "body";
                        formattedAcute.Add($"infected wound on {partLabel}{stage}");
                        continue;
                    }

                    // Enfermedad aguda normal
                    if (!string.IsNullOrEmpty(note))
                    {
                        formattedAcute.Add($"{label}{stage}: {note.TrimEnd('.')}");
                    }
                    else
                    {
                        formattedAcute.Add($"{label}{stage}");
                    }
                }

                // Si alguna enfermedad aguda tiene nota explicativa, usaremos viñetas
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

            // --- B. ENFERMEDADES CRÓNICAS ---
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

        public static bool IsAcuteDisease(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
                return false;

            HediffDef def = hediff.def;

            // 1. Descartar si no es algo negativo o si es una herida/parte faltante
            if (!def.isBad || def.chronic)
                return false;

            if (hediff is Hediff_Injury || hediff is Hediff_MissingPart)
                return false;

            // 2. Filtro directo por infección o pensamiento de enfermedad
            if (def.isInfection || def.makesSickThought)
                return true;

            // 3. Filtro por componentes típicos de enfermedades (Inmunizables / Tratable por tiempo)
            bool hasImmunizable = def.HasComp(typeof(HediffComp_Immunizable));
            bool hasTendDuration = def.HasComp(typeof(HediffComp_TendDuration));

            if (hasImmunizable || (def.tendable && hasTendDuration))
                return true;

            // 4. Casos específicos de afecciones agudas o bloqueos mentales (Catatonic, HeartAttack, etc.)
            if (def.defName == "HeartAttack" || def.defName == "CatatonicBreakdown" || def.defName == "FoodPoisoning")
                return true;

            return false;
        }

        public static bool IsChronicDisease(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
                return false;

            // Afección negativa marcada explícitamente como crónica en el XML
            return hediff.def.isBad && hediff.def.chronic && !(hediff is Hediff_Injury);
        }

        /// <summary>
        /// Recopila todas las directivas de comportamiento (behavioralDirective) asociadas a los hediffs del colono.
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
                    directives.Add($"- {directive}");
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
        private static readonly Dictionary<string, HediffAIPromptDef> PromptDefs = new Dictionary<string, HediffAIPromptDef>();
        static HediffClassifier()
        {
            // Cargar automáticamente todos los XMLs de tipo HediffAIPromptDef al iniciar el juego
            LoadXMLDefinitions();
        }

        private static void LoadXMLDefinitions()
        {
            foreach (var promptDef in DefDatabase<HediffAIPromptDef>.AllDefs)
            {
                if (!string.IsNullOrEmpty(promptDef.targetHediff))
                {
                    PromptDefs[promptDef.targetHediff] = promptDef;
                }
            }
        }

        /// <summary>
        /// Devuelve los textos (customLabel, functionalNote, behavioralDirective)
        /// resolviendo la etapa actual según la severidad del hediff.
        /// </summary>
        public static (string label, string note, string directive) GetResolvedPrompt(Hediff hediff)
        {
            if (hediff?.def == null) return (null, null, null);

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

                // Una sola pasada: busca coincidencia exacta y, en paralelo,
                // el mejor fallback (mayor stageIndex <= actual).
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
        /// Devuelve el sufijo de etapa nativa entre paréntesis si la etiqueta del label
        /// no la incluye ya (porque el XML la habría puesto vía customLabel).
        /// </summary>
        public static string GetStageSuffix(Hediff h, string resolvedLabel)
        {
            string stageLabel = h.CurStage?.label;
            if (string.IsNullOrEmpty(stageLabel)) return string.Empty;

            // Si el label resuelto ya contiene la etapa, no la dupliques.
            if (resolvedLabel != null &&
                resolvedLabel.IndexOf(stageLabel, StringComparison.OrdinalIgnoreCase) >= 0)
                return string.Empty;

            return $" ({stageLabel})";
        }

        /// <summary>
        /// Obtiene el nombre de la prótesis o implante y, si existe, añade su aclaración técnica.
        /// </summary>
        /// <param name="hediff">El Hediff del colono.</param>
        /// <param name="includeDescription">Si es true, buscará si hay una nota aclaratoria registrada y la adjuntará.</param>
        /// <param name="customDescription">Permite pasar una nota ad-hoc directamente en la llamada si fuera necesario.</param>
        public static string FormatBodyModification(Hediff hediff, bool includeDescription = true, string customDescription = null)
        {
            if (hediff?.def == null) return "prosthetic";

            var (label, note, _) = GetResolvedPrompt(hediff);

            if (!includeDescription) return label;
            if (!string.IsNullOrEmpty(customDescription)) return $"{label} ({customDescription})";
            return string.IsNullOrEmpty(note) ? label : $"{label} ({note})";       
        }


        private static string GetFirstSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            int periodIndex = text.IndexOf('.');
            if (periodIndex > 0)
            {
                return text.Substring(0, periodIndex).Trim().ToLower();
            }
            return text.Trim().ToLowerInvariant();
        }

        public static bool IsRealImplantOrProsthetic(Hediff hediff)
        {
            if (hediff == null || hediff.def == null) return false;

            HediffDef def = hediff.def;

            // 1. RimWorld expone este booleano directamente en HediffDef.
            // RJW marca sus partes naturales explícitamente con <countsAsAddedPartOrImplant>false</countsAsAddedPartOrImplant>
            if (!def.countsAsAddedPartOrImplant)
            {
                return false;
            }

            // 2. Verificación estándar de RimWorld:
            // Comprueba si hereda de la clase base de implantes/partes añadidas o si tiene propiedades de prótesis
            return hediff is Hediff_AddedPart ||
                   hediff is Hediff_Implant ||
                   def.addedPartProps != null;
        }

        public static string ExtractShortDescription(HediffDef hediffDef)
        {
            if(!string.IsNullOrEmpty(hediffDef.description))
            {
                string shortDesc = GetFirstSentence(hediffDef.description);
                // Solo si es una frase corta y útil (menos de 65 caracteres)
                if (!string.IsNullOrEmpty(shortDesc) && shortDesc.Length < 65)
                {
                    return shortDesc;
                }
            }
            return string.Empty;
        }
    }
}
