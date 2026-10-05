using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    public static class ColonistMemoryPromptBuilder
    {
        /// <summary>
        /// Prompt builder for generate the resume of the day.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="day"></param>
        /// <param name="rawInteractions"></param>
        /// <returns></returns>
        public static string BuildDailyMemoryPrompt(Pawn pawn, int day, string rawInteractions)
        {
            if (pawn == null) return string.Empty;

            var promptBlocks = new List<string>();

            promptBlocks.Add("[SYSTEM]");

            // 1. Identidad básica
            AddIfNotEmpty(promptBlocks, BuildIdentity(pawn));
            AddIfNotEmpty(promptBlocks, BuildHediffDirectives(pawn));
            AddIfNotEmpty(promptBlocks, BuildGeneticProfile(pawn));

            AddIfNotEmpty(promptBlocks, GetPawnBackstory(pawn));
            AddIfNotEmpty(promptBlocks, BuildTraits(pawn));
            AddIfNotEmpty(promptBlocks, BuildIdeology(pawn));

            AddIfNotEmpty(promptBlocks, MyMod.Settings?.globalPrompt);
            AddIfNotEmpty(promptBlocks, ColonistPromptManager.GetPrompt(pawn));
            AddIfNotEmpty(promptBlocks, ColonistGroupManager.GetGroupPrompt(pawn));

            var directives = new List<string>();
            AddIfNotEmpty(directives, GetAgeBehavioralDirective(pawn));
            AddIfNotEmpty(directives, GetPersonalityConsolidationDirective());

            if (directives.Any())
            {
                promptBlocks.Add("*CORE ROLEPLAY DIRECTIVES:*\n" + string.Join("\n", directives));
            }

            string gameLanguage = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";

            // 4. Definición de la tarea y reglas de estilo
            string userBlock =
                $"[USER]\n" +
                $"Below are the events and interactions recorded for day {day}:\n" +
                $"\"\"\"\n{rawInteractions}\n\"\"\"\n\n" +
                $"### TASK ###\n" +
                $"Write a personal diary entry reflecting on day {day} based strictly on your experiences below.\n\n" +
                $"MANDATORY RULES:\n" +
                $"1. Write strictly in FIRST PERSON ('I', 'We') as a grounded, personal account.\n" +
                $"2. Adopt a tone, vocabulary, and perspective that match your age, traits, and backstory.\n" +
                $"3. Summarize repetitive events and weave key actions, conflicts, and outcomes together with your internal thoughts, judgments, and feelings.\n" +
                $"4. DO NOT use intros like 'Dear Diary', 'Today I...', or meta-narrative commentary. Jump directly into your story or thoughts.\n" +
                $"5. Digest raw tags like '[Player Conversation]' or '[Group conversation]' into natural personal memories.\n" +
                $"6. Do NOT wrap your output in Markdown code blocks (e.g. ```markdown).\n" +
                $"7. Target length: 100-200 words.\n" +
                $"8. CRITICAL LANGUAGE REQUIREMENT: Write your entire response strictly in {gameLanguage}.";

            promptBlocks.Add(userBlock);

            return string.Join("\n\n", promptBlocks);
        }

        /// <summary>
        /// Prompt builder for generate a custom memory entry using AI to rewrite it in first person.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="editedMem"></param>
        /// <returns></returns>
        public static string BuildCustomMemoryWithAI(Pawn pawn, string editedMem)
        {
            if (pawn == null) return string.Empty;

            var promptBlocks = new List<string>();

            promptBlocks.Add("[SYSTEM]");

            // 1. Identidad básica
            AddIfNotEmpty(promptBlocks, BuildIdentity(pawn));
            AddIfNotEmpty(promptBlocks, BuildHediffDirectives(pawn));
            AddIfNotEmpty(promptBlocks, BuildGeneticProfile(pawn));

            AddIfNotEmpty(promptBlocks, GetPawnBackstory(pawn));
            AddIfNotEmpty(promptBlocks, BuildTraits(pawn));
            AddIfNotEmpty(promptBlocks, BuildIdeology(pawn));

            AddIfNotEmpty(promptBlocks, MyMod.Settings?.globalPrompt);
            AddIfNotEmpty(promptBlocks, ColonistPromptManager.GetPrompt(pawn));
            AddIfNotEmpty(promptBlocks, ColonistGroupManager.GetGroupPrompt(pawn));

            var directives = new List<string>();
            AddIfNotEmpty(directives, GetAgeBehavioralDirective(pawn));
            AddIfNotEmpty(directives, GetPersonalityConsolidationDirective());

            if (directives.Any())
            {
                promptBlocks.Add("*CORE ROLEPLAY DIRECTIVES:*\n" + string.Join("\n", directives));
            }

            string gameLanguage = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";

            // 4. Definición de la tarea y reglas de estilo
            string userBlock =
                $"[USER]\n" +
                $"Below is a draft of a memory entry:\n" +
                $"\"\"\"\n{editedMem}\n\"\"\"\n\n" +
                $"### TASK ###\n" +
                $"REWRITE the draft above in FIRST PERSON so that it reads as a natural, personal memory entry written by you.\n\n" +
                $"MANDATORY RULES:\n" +
                $"1. Preserve all key facts and intent from the draft, but express them through your own voice, traits, and background.\n" +
                $"2. Adopt a tone and vocabulary appropriate to your age, health status, and ideology.\n" +
                $"3. DO NOT use intros like 'Dear Diary', 'Today I...', or meta-narrative commentary. Jump directly into your internal thoughts.\n" +
                $"4. Do NOT wrap your output in Markdown code blocks (e.g. ```markdown).\n" +
                $"5. Target length: Maximum 200 words.\n" +
                $"6. CRITICAL LANGUAGE REQUIREMENT: Write your entire response strictly in {gameLanguage}.";

            promptBlocks.Add(userBlock);

            return string.Join("\n\n", promptBlocks);
        }

        /// <summary>
        /// Build a prompt for the LLM to summarize the conversation and generate a brief internal thought or memory.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="conversationTranscript"></param>
        /// <returns></returns>
        public static string BuildSummaryPrompt(Pawn pawn, string conversationTranscript)
        {
            if (pawn == null) return string.Empty;

            var promptBlocks = new List<string>();

            promptBlocks.Add("[SYSTEM]");

            promptBlocks.Add(BuildIdentity(pawn));
            AddIfNotEmpty(promptBlocks, BuildPhysicalHealth(pawn));
            AddIfNotEmpty(promptBlocks, BuildMentalState(pawn));
            AddIfNotEmpty(promptBlocks, BuildGeneticProfile(pawn));

            AddIfNotEmpty(promptBlocks, GetPawnBackstory(pawn));
            AddIfNotEmpty(promptBlocks, BuildTraits(pawn));
            AddIfNotEmpty(promptBlocks, BuildIdeology(pawn));

            AddIfNotEmpty(promptBlocks, MyMod.Settings?.globalPrompt);
            AddIfNotEmpty(promptBlocks, ColonistPromptManager.GetPrompt(pawn));
            AddIfNotEmpty(promptBlocks, ColonistGroupManager.GetGroupPrompt(pawn));
            // 2. Historial de memorias pasadas y del día
            AddIfNotEmpty(promptBlocks, ColonistPromptContextBuilder.BuildMemoryRecap(pawn));

            var directives = new List<string>();
            AddIfNotEmpty(directives, GetAgeBehavioralDirective(pawn));
            AddIfNotEmpty(directives, GetPersonalityConsolidationDirective());
            AddIfNotEmpty(directives, GetConditionImpactDirective(pawn)); // 👈 Modifica el tono según dolor/colapso

            if (directives.Any())
            {
                promptBlocks.Add("*CORE ROLEPLAY DIRECTIVES:*\n" + string.Join("\n", directives));
            }

            string gameLanguage = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";

            // 3. Instrucción final con la transcripción de la charla
            string userInstructionBlock =
                $"[USER]\n" +
                $"Below is a recent excerpt from the dialogue between you and the Player:\n" +
                $"\"\"\"\n{conversationTranscript}\n\"\"\"\n\n" +
                $"Your task is to write down one thought or internal memory about what was discussed in this excerpt.\n\n" +
                $"MANDATORY RULES:\n" +
                $"1. Write in FIRST PERSON ('I thought', 'He/She told me', 'I felt').\n" +
                $"2. Record only your impressions, opinions, or relevant information about what was just discussed.\n" +
                $"3. Do NOT assume whether the conversation has ended or will continue; simply evaluate the facts/ideas expressed.\n" +
                $"4. Keep it very brief (1 to 3 sentences maximum), intimate, and natural.\n" +
                $"5. Do NOT use direct quotes or talk about yourself in the third person.\n" +
                $"6. Do NOT wrap your output in Markdown code blocks (e.g. ```markdown).\n" +
                $"7. CRITICAL LANGUAGE REQUIREMENT: Write your entire response strictly in {gameLanguage}.";

            promptBlocks.Add(userInstructionBlock);

            // Une únicamente los bloques que tengan contenido real separados por un salto de línea doble
            return string.Join("\n\n", promptBlocks);
        }

        private static void AddIfNotEmpty(List<string> list, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                list.Add(text.Trim());
            }
        }

        private static string BuildIdentity(Pawn pawn)
        {
            if (pawn?.story == null) return string.Empty;

            string gender = pawn.gender.ToString();

            int bioAge = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            int chronoAge = pawn.ageTracker?.AgeChronologicalYears ?? bioAge;
            string realAge = "";

            if (chronoAge > bioAge)
            {
                realAge = $" You had been born {chronoAge} years ago and preserved due to cryptosleep.";
            }

            string status = "colonist";
            string statusContext = "";

            if (pawn.IsSlaveOfColony)
            {
                status = "slave";
                string slaveWill = "";
                if (pawn.guest != null)
                {
                    float will = pawn.guest.will;
                    if (will < 0.2f)
                        slaveWill = " Your will is broken and you have little resistance left.";
                    else if (will < 0.5f)
                        slaveWill = " You still have some will to resist but are largely subdued.";
                    else
                        slaveWill = " You still have a strong will and deeply resent your situation.";
                }
                statusContext = $" You are enslaved and forced to obey your masters. You have no freedom and can be punished for disobedience.{slaveWill} You may feel fear, resentment, or resignation depending on your nature.";
            }
            else if (pawn.IsPrisonerOfColony)
            {
                status = "prisoner";
                bool recruitable = pawn.guest?.Recruitable == true;

                if (recruitable)
                    statusContext = " You are a prisoner being held by this colony. You are considering whether to join them, but you still have doubts and reservations. You are not yet one of them.";
                else
                    statusContext = " You are a prisoner held against your will. You resent your captors and want to be free. You do not trust them and have no intention of cooperating unless you have a compelling reason.";

                if (pawn.guest != null)
                {
                    float resistance = pawn.guest.resistance;
                    if (resistance > 20f)
                        statusContext += " You are very resistant to recruitment and deeply hostile.";
                    else if (resistance > 5f)
                        statusContext += " You are still resistant but beginning to waver slightly.";
                    else if (resistance <= 0f && recruitable)
                        statusContext += " Your resistance has worn down and you are genuinely considering joining.";
                }
            }
            else if (pawn.IsColonist)
            {
                status = "colonist";
                statusContext = " You are a free member of this colony with full rights and responsibilities.";
            }

            if (ModsConfig.IdeologyActive && pawn.ideo?.Ideo != null)
            {
                Precept_Role role = pawn.ideo.Ideo.GetRole(pawn);
                if (role != null)
                {
                    string customLabel = role.LabelCap;
                    string baseLabel = role.def?.LabelCap;

                    string roleText = (!string.IsNullOrEmpty(baseLabel) && !customLabel.Equals(baseLabel, StringComparison.OrdinalIgnoreCase))
                        ? $" (Role: {customLabel} [{baseLabel}])"
                        : $" (Role: {customLabel})";

                    statusContext += roleText;
                }
            }

            if (ModsConfig.RoyaltyActive)
            {
                // 1. Título Nobiliario de mayor rango
                if (pawn.royalty != null && pawn.royalty.AllTitlesForReading.Any())
                {
                    RoyalTitle highestTitle = pawn.royalty.MostSeniorTitle;
                    if (highestTitle != null)
                    {
                        // GetLabelCapFor adapta el título al género del colono (ej. Emperor vs Empress, Baron vs Baroness)
                        string titleLabel = highestTitle.def.GetLabelCapFor(pawn);
                        string factionName = highestTitle.faction?.Name;

                        string titleText = !string.IsNullOrEmpty(factionName)
                            ? $" (Royal Title: {titleLabel} of {factionName})"
                            : $" (Royal Title: {titleLabel})";

                        statusContext += titleText;
                    }
                }

                // 2. Nivel de Psicaster (Psylink)
                int psylinkLevel = pawn.GetPsylinkLevel();
                if (psylinkLevel > 0)
                {
                    statusContext += $" (Psicaster Level {psylinkLevel})";
                }
            }

            return ($"You are {pawn.LabelShort}, a {bioAge}-year-old {gender} {status} in RimWorld.{realAge}\n{statusContext}".Trim());
        }

        private static string BuildMentalState(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            var conditions = new List<string>();

            // 1. Estado Mental / Humor
            if (pawn.InMentalState)
            {
                conditions.Add($"CRITICAL MENTAL BREAK: Currently experiencing {pawn.MentalState.def.label.ToUpper()}!");
            }
            else if (pawn.needs?.mood != null)
            {
                float moodPct = pawn.needs.mood.CurLevelPercentage;
                if (moodPct < 0.20f) conditions.Add("Mood: Severely depressed / On the verge of a mental breakdown.");
                else if (moodPct < 0.40f) conditions.Add("Mood: Stressed, anxious, and unhappy.");
                else if (moodPct > 0.80f) conditions.Add("Mood: In high spirits and highly content.");
            }
            if (!conditions.Any()) return string.Empty;

            return "*Current Mood:*\n  - " + string.Join("\n  - ", conditions);
        }

        private static string BuildPhysicalHealth(Pawn pawn)
        {
            // 1. Guard clause unificada para evitar NullReferenceException
            if (pawn?.health?.hediffSet?.hediffs == null)
                return string.Empty;

            // Filtramos solo los hediffs visibles para el prompt
            var hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList();
            var conditions = new List<string>();

            // 2. Encadenar todos los componentes de la salud del colono
            HealthPromptClassifier.AppendCriticalAndCapacities(pawn, hediffs, conditions);
            HealthPromptClassifier.AppendInjuries(hediffs, conditions);
            HealthPromptClassifier.AppendProstheticsAndMissingParts(pawn, hediffs, conditions);
            HealthPromptClassifier.AppendImplants(hediffs, conditions);
            HealthPromptClassifier.AppendAddictionsAndWithdrawals(hediffs, conditions);
            HealthPromptClassifier.AppendDiseasesAndInfections(hediffs, conditions);

            // 3. Añadir directivas conductuales para la actuación del LLM
            HealthPromptClassifier.AppendBehavioralDirectives(hediffs, conditions);

            if (!conditions.Any())
                return string.Empty;

            return string.Join("\n", conditions);
        }

        private static string BuildHediffDirectives(Pawn pawn)      
        {
            if (pawn?.health?.hediffSet?.hediffs == null) return string.Empty;

            var directives = new List<string>();
            var hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList();
            if (hediffs.Any())
            {
                HealthPromptClassifier.AppendBehavioralDirectives(hediffs, directives);
            }
            if (!directives.Any()) return string.Empty;
            return "*Health-Related Behavioral Directives:*\n  - " + string.Join("\n  - ", directives);
        }

        private static string BuildGeneticProfile(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null) return string.Empty;

            var parts = new List<string>();

            string xenotype = pawn.genes.XenotypeLabel;
            if (!string.IsNullOrEmpty(xenotype) && !xenotype.Equals("baseliner", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"Xenotype: {xenotype}");
            }

            // Extraer genes usando nuestro clasificador XML
            string genesContext = GetNarrativeGenesContext(pawn);
            if (!string.IsNullOrEmpty(genesContext))
            {
                parts.Add($"Active Traits: {genesContext}");
            }

            if (!parts.Any()) return string.Empty;

            return "*Genetic Profile:*\n  - " + string.Join("\n  - ", parts);
        }

        private static string BuildTraits(Pawn pawn)
        {
            if (pawn?.story?.traits == null || !pawn.story.traits.allTraits.Any())
                return "*Traits:* None";

            var entries = new List<string>();

            foreach (var t in pawn.story.traits.allTraits)
            {
                string rawDesc = t.CurrentData?.description ?? t.def?.description;
                string formattedDesc = "";

                if (!string.IsNullOrEmpty(rawDesc))
                {
                    formattedDesc = FormatGameText(rawDesc, pawn);
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
        private static string GetPawnBackstory(Pawn pawn)
        {      
            if (pawn?.story == null) return string.Empty;

            var entries = new List<string>();

            // 1. Trasfondo de Infancia (Childhood)
            if (pawn.story.Childhood != null)
            {
                string title = pawn.story.Childhood.title;
                string desc = FormatGameText(pawn.story.Childhood.baseDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Childhood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Childhood: {title}");
            }

            // 2. Trasfondo de Adultez (Adulthood)
            if (pawn.story.Adulthood != null)
            {
                string title = pawn.story.Adulthood.title;
                string desc = FormatGameText(pawn.story.Adulthood.baseDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Adulthood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Adulthood: {title}");
            }

        if (!entries.Any()) return string.Empty;

        return "*Backstory & Origin:*\n  - " + string.Join("\n  - ", entries);
        }

        private static string FormatGameText(string rawText, Pawn pawn)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            try
            {
                // Resuelve sustituciones dinámicas de RimWorld ({PAWN_nameDef}, {PAWN_pronoun}, etc.)
                string formatted = rawText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).ToString();

                // Limpia etiquetas de color o formato XML residuales (<color=...>, <i>, etc.)
                formatted = System.Text.RegularExpressions.Regex.Replace(formatted, "<.*?>", "").Trim();

                return formatted;
            }
            catch
            {
                // Fallback defensivo por si algún mod de trasfondos tiene sintaxis XML corrupta
                return rawText.Trim();
            }
        }

        private static string BuildIdeology(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn?.ideo?.Ideo == null)
                return string.Empty;

            Ideo ideo = pawn.ideo.Ideo;
            var parts = new List<string>();

            parts.Add($"Belief System: {ideo.name}");

            // 1. Memes con Nombre + Descripción Dogmática
            if (ideo.memes != null && ideo.memes.Any())
            {
                var memeSummaries = new List<string>();
                foreach (var meme in ideo.memes)
                {
                    if (meme == null) continue;

                    string memeName = meme.LabelCap;
                    string rawDesc = meme.description;

                    if (!string.IsNullOrWhiteSpace(rawDesc))
                    {
                        // Limpiar posibles etiquetas XML de formato
                        string cleanDesc = System.Text.RegularExpressions.Regex.Replace(rawDesc, "<.*?>", "").Trim();
                        memeSummaries.Add($"{memeName} (\"{cleanDesc}\")");
                    }
                    else
                    {
                        memeSummaries.Add(memeName);
                    }
                }

                parts.Add($"Core Memes:\n    - " + string.Join("\n    - ", memeSummaries));
            }

            // 2. Rol social/religioso (si lo tiene)
            Precept_Role role = ideo.GetRole(pawn);
            if (role != null)
            {
                string customLabel = role.LabelCap;
                string baseLabel = role.def?.LabelCap;

                string roleHeader = (!string.IsNullOrEmpty(baseLabel) && !customLabel.Equals(baseLabel, StringComparison.OrdinalIgnoreCase))
                    ? $"{customLabel} (Base Function: {baseLabel})"
                    : customLabel;

                parts.Add($"Your Assigned Role: {roleHeader}");
                string rawRoleDesc = role.def?.description;
                if (!string.IsNullOrWhiteSpace(rawRoleDesc))
                {
                    string cleanRoleDesc = System.Text.RegularExpressions.Regex.Replace(rawRoleDesc, "<.*?>", "").Trim();
                    parts.Add($"Role Purpose & Nature: \"{cleanRoleDesc}\"");
                }
            }

            // 3. Top 5 Preceptos o Creencias Morales más relevantes
            var keyPrecepts = GetKeyIdeologyPrecepts(ideo, 5);
            if (keyPrecepts.Any())
            {
                string preceptsText = string.Join("\n    - ", keyPrecepts);
                parts.Add($"Key Convictions:\n    - {preceptsText}");
            }

            if (!parts.Any()) return string.Empty;

            // 4. Inyección de la directiva de convicción para mantener el Roleplay auténtico
            string result = "*Ideology & Sacred Beliefs:*\n  - " + string.Join("\n  - ", parts);
            result += "\n*CRITICAL: These beliefs represent your GENUINE, deeply-held convictions. Speak about them naturally with conviction. Never contradict, moralize against, or apologize for these beliefs.*";

            return result;
        }

        private static List<string> GetKeyIdeologyPrecepts(Ideo ideo, int maxCount = 5)
        {
            if (ideo?.PreceptsListForReading == null) return new List<string>();

            return ideo.PreceptsListForReading
                // 1. Filtrar solo preceptos con descripción y descartar roles/edificios/rituales implícitamente
                .Where(p => p != null && p.def != null && !(p is Precept_Role) && !(p is Precept_Building))
                // 2. Ordenar por Impacto (High/Medium) para que las reglas morales más fuertes vayan primero
                .OrderByDescending(p => p.def.impact)
                .Select(p => GetFormattedPrecept(p))
                .Where(desc => !string.IsNullOrWhiteSpace(desc))
                .Distinct()
                .Take(maxCount)
                .ToList();
        }

        private static string GetFormattedPrecept(Precept precept)
        {
            if (precept == null || precept.def == null) return string.Empty;

            // 1. Descartar elementos que no son normas de conducta morales
            if (precept is Precept_Role ||
                precept is Precept_Building ||
                precept is Precept_Ritual ||
                precept is Precept_Apparel)
            {
                return string.Empty;
            }

            // 2. Descartar preceptos de bajo impacto para ahorrar tokens
            if (precept.def.impact == PreceptImpact.Low)
                return string.Empty;

            // 3. Obtener el Tema (Issue) y la Posición (Precept) traducidos nativamente
            string issueLabel = precept.def.issue?.LabelCap; // Ej: "Female clothing" / "Ropa femenina"
            string preceptLabel = precept.LabelCap;         // Ej: "Fully nude" / "Totalmente desnudo"

            if (string.IsNullOrWhiteSpace(preceptLabel)) return string.Empty;

            // Construir el título compuesto (ej: "Female clothing: Fully nude")
            string fullLabel = !string.IsNullOrEmpty(issueLabel)
                ? $"{issueLabel}: {preceptLabel}"
                : preceptLabel;

            // 4. Inyectar la descripción filosófica/dogmática
            string rawDesc = precept.def.description;
            if (!string.IsNullOrWhiteSpace(rawDesc))
            {
                string cleanDesc = System.Text.RegularExpressions.Regex.Replace(rawDesc, "<.*?>", "").Trim();
                return $"{fullLabel} - \"{cleanDesc}\"";
            }

            return fullLabel;
        }

        public static string GetNarrativeGenesContext(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null)
                return string.Empty;

            // Categorías de genes que afectan el comportamiento, la psique o capacidades narrativas
            HashSet<string> relevantCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Violence",       // Kill thirst, Violence disabled, Kind instinct
                "Psychic",        // Psychic bonding, Psychically deaf
            };

            List<string> geneLabels = new List<string>();

            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                // Ignorar genes inactivos por conflicto genético
                if (!gene.Active) continue;

                string categoryDefName = gene.def.displayCategory?.defName;

                // 1. Incluir si pertenece a una categoría relevante
                if (categoryDefName != null && relevantCategories.Contains(categoryDefName))
                {
                    geneLabels.Add(GenePromptClassifier.FormatGeneForPrompt(gene));
                    continue;
                }

                // 2. Filtro especial para "Miscellaneous": algunos genes de esta categoría
                // deshabilitan necesidades o cambian la visión (ej. Cave Dweller, Dark Vision)
                if (categoryDefName == "Miscellaneous") 
                {
                     if (gene.def.disablesNeeds?.Any() == true || gene.def.ignoreDarkness) 
                     {
                        geneLabels.Add(GenePromptClassifier.FormatGeneForPrompt(gene));
                     }
                }
            }

            if (!geneLabels.Any())
                return string.Empty;

            return $" [Key Genetic Traits: {string.Join(", ", geneLabels)}]";
        }


        private static string GetAgeBehavioralDirective(Pawn pawn)
        {
            int bioAge = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            if (bioAge < 13)
                return "- AGE DIRECTIVE: You are a child. Use simpler vocabulary, express emotions directly and naively, and view the world through immediate survival, play, or fear.";

            if (bioAge < 20)
                return "- AGE DIRECTIVE: You are a teenager/young adult. Impulsive, seeking validation or independence, and quick to form strong emotional reactions.";

            if (bioAge >= 65)
                return "- AGE DIRECTIVE: You are an elder. Speak with the weight of experience, weariness, or pragmatic patience, reflecting on past years.";

            return string.Empty; // Adultos estándar no necesitan una restricción rígida de edad
        }

        private static string GetPersonalityConsolidationDirective()
        {
            return "*BEHAVIORAL CONSISTENCY DIRECTIVES:*\n" +
                   "  - Embrace your flaws and traits without shame, self-correction, or moralizing.\n" +
                   "  - Never break character to act as an AI or a neutral observer.\n" +
                   "  - Respond strictly from inside the RimWorld universe—modern Earth concepts or etiquette do not exist to you.";
        }

        private static string GetConditionImpactDirective(Pawn pawn)
        {
            if (pawn.InMentalState)
            {
                return "CRITICAL TONE MANDATE: Your mind is currently fractured by a mental breakdown. Your thoughts MUST be erratic, repetitive, fixated, or aggressive.";
            }

            if (pawn.health?.hediffSet?.PainTotal > 0.5f)
            {
                return "TONE MANDATE: You are in intense physical pain. Keep sentences shorter, sharp, and focused on physical discomfort or suffering.";
            }

            return string.Empty;
        }
    }
}
