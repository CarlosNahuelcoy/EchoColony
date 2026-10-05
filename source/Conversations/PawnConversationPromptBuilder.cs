using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Representa el par de prompts estructurados para el LLM.
    /// </summary>
    public readonly struct ConversationPromptResult
    {
        public string SystemPrompt { get; }
        public string UserPrompt { get; }

        public ConversationPromptResult(string systemPrompt, string userPrompt)
        {
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
        }
    }

    /// <summary>
    /// Genera los prompts de sistema y usuario para conversaciones espontáneas entre colonos.
    /// </summary>
    public static class PawnConversationPromptBuilder
    {
        private static readonly Regex TagRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex ColorTagRegex = new Regex(@"<color=#[0-9A-Fa-f]{6,8}>", RegexOptions.Compiled);

        // Definir límites según el presupuesto de tokens
        private const int MaxDirectInteractionsToday = 8;  // Interacciones entre A y B
        private const int MaxGeneralInteractionsToday = 4; // Interacciones de A/B con terceros

        // ── Entry point ───────────────────────────────────────────────────────────

        public static ConversationPromptResult? Build(
            Pawn initiator,
            Pawn recipient,
            InteractionDef interactionDef,
            int linesPerPawn = 3)
        {
            if (initiator == null || recipient == null) return null;

            int totalLines = linesPerPawn * 2;
            string logText = GetRecentInteractionLogText(initiator, recipient, interactionDef);

            string systemPrompt = BuildSystemPrompt(totalLines, initiator.LabelShort, recipient.LabelShort);
            string userPrompt = BuildUserPrompt(initiator, recipient, interactionDef, logText, totalLines);

            return new ConversationPromptResult(systemPrompt, userPrompt);
        }

        // ── System Prompt Builder ─────────────────────────────────────────────────

        private static string BuildSystemPrompt(int totalLines, string nameA, string nameB)
        {
            string lang = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";
            string langInstruction = lang != "english" ? $"IMPORTANT: Respond in {lang}.\n" : "";

            return
                $"You are an AI roleplay generator creating natural, spontaneous dialogue between two RimWorld colonists.\n" +
                $"{langInstruction}\n" +
                $"OUTPUT FORMAT RULES:\n" +
                $"1. Respond ONLY with a valid JSON array. Do NOT use markdown code blocks (no ```json), no preambles, and no postscripts.\n" +
                $"2. Generate exactly {totalLines} dialogue entries, strictly alternating speaker starting with \"{nameA}\" then \"{nameB}\".\n" +
                $"3. Schema:\n" +
                $"[\n" +
                $"  {{\"speaker\": \"{nameA}\", \"text\": \"...\"}},\n" +
                $"  {{\"speaker\": \"{nameB}\", \"text\": \"...\"}}\n" +
                $"]\n\n" +
                $"DIALOGUE RULES:\n" +
                $"1. Line Length: Each text must be 1–2 short sentences maximum.\n" +
                $"2. Style: Conversational, grounded, and human. Reflect their relationship, power dynamics, health, and traits.\n" +
                $"3. No Meta-Talk: Never talk ABOUT the conversation itself (e.g., do NOT say 'That was a nice talk'). Focus on the topic.\n" +
                $"4. Animals: If a participant is an animal, follow species limitations specified in context (sounds/actions vs words).\n\n" +
                $"STRICT GROUNDING RULES (ANTI-HALLUCINATION):\n" +
                $"1. The section 'VERIFIED COLONY HISTORY' is the ONLY permitted source for historical facts/events.\n" +
                $"2. Never reference technology, structures, items, animals, or events NOT mentioned in context or history.\n" +
                $"3. If history is missing, colonists talk ONLY about current actions, immediate surroundings, weather, or feelings.\n" +
                $"4. Immersion is priority: A short, grounded exchange is infinitely better than an invented lore fact."+
				"5. Ambient Noise Constraint: Weather, time of day, and location are strictly PASSIVE BACKGROUND.\n" +
				"   Never make weather the main topic of conversation unless the primary trigger event specifically demands it " +
				"   or it poses an immediate life-threatening hazard (e.g., Extreme Heatwave / Toxic Fallout).\n";	
        }

        // ── User Prompt Builder ───────────────────────────────────────────────────

        private static string BuildUserPrompt(
            Pawn initiator,
            Pawn recipient,
            InteractionDef interactionDef,
            string logText,
            int totalLines)
        {
            var sb = new StringBuilder();

            string convGlobal = MyMod.Settings?.conversationGlobalPrompt;
            if (!string.IsNullOrWhiteSpace(convGlobal))
            {
                sb.AppendLine("=== PLAYER INSTRUCTIONS (always follow) ===");
                sb.AppendLine(convGlobal.Trim());
                sb.AppendLine();
            }

            sb.AppendLine(BuildInteractionTypeSection(interactionDef, logText));
            sb.AppendLine(BuildEnvironmentSection(initiator));
            sb.AppendLine(BuildPawnSection(initiator, "INITIATOR"));
            sb.AppendLine(BuildPawnSection(recipient, "RECIPIENT"));
            sb.AppendLine(BuildRelationshipSection(initiator, recipient));
            sb.AppendLine(BuildSharedMemorySection(initiator, recipient));
            sb.AppendLine(BuildColonyEventsSection());
            sb.AppendLine(BuildVerifiedTalesSection(initiator, recipient));

            sb.AppendLine($"=== INSTRUCTION ===");
            sb.AppendLine($"Generate the {totalLines}-line dialogue array between {initiator.LabelShort} and {recipient.LabelShort} following all System instructions and strictly staying on the primary topic above.");

            return sb.ToString();
        }

        // ── Verified History (Tales) ──────────────────────────────────────────────

        private static string BuildVerifiedTalesSection(Pawn initiator, Pawn recipient)
        {
            var (shared, personalI) = TalesCache.GetTalesForPair(initiator, recipient,
                TalesCache.MAX_SHARED_TALES, TalesCache.MAX_PERSONAL_TALES);
            var (_, personalR) = TalesCache.GetTalesForPair(recipient, initiator,
                0, TalesCache.MAX_PERSONAL_TALES);

            if (!shared.Any() && !personalI.Any() && !personalR.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== VERIFIED COLONY HISTORY (FACTS — INVENT NOTHING ELSE) ===");
            sb.AppendLine("The following events ACTUALLY happened in this colony:");

            if (shared.Any())
            {
                sb.AppendLine($"Events involving BOTH {initiator.LabelShort} and {recipient.LabelShort}:");
                foreach (var t in shared) sb.AppendLine($"  • {t}");
            }

            if (personalI.Any())
            {
                sb.AppendLine($"Events involving {initiator.LabelShort}:");
                foreach (var t in personalI) sb.AppendLine($"  • {t}");
            }

            if (personalR.Any())
            {
                sb.AppendLine($"Events involving {recipient.LabelShort}:");
                foreach (var t in personalR) sb.AppendLine($"  • {t}");
            }

            return sb.ToString();
        }

        // ── Interaction Trigger & Topic ───────────────────────────────────────────

        private static string BuildInteractionTypeSection(InteractionDef def, string logText = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PRIMARY CONVERSATION TOPIC ===");

            if (!string.IsNullOrWhiteSpace(logText))
            {
                sb.AppendLine($"Trigger Event: {logText}");
                sb.AppendLine("Focus: The dialogue must flow directly from this specific event.");
            }
            else if (def != null)
            {
                string nature = GetInteractionNature(def);
                sb.AppendLine($"Trigger: {def.label} ({nature})");

                if (!IsKnownVanillaInteraction(def))
                {
                    string desc = def.description?.Trim();
                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        desc = CleanXmlTags(desc);
                        if (desc.Length > 150) desc = desc.Substring(0, 147) + "...";
                        sb.AppendLine($"Context: {desc}");
                    }
                }
            }
            else
            {
                sb.AppendLine("Trigger: General interaction / casual chat.");
            }

            return sb.ToString();
        }

        private static string GetRecentInteractionLogText(Pawn a, Pawn b, InteractionDef def)
        {
            if (Find.PlayLog == null) return null;
            try
            {
                int now = Find.TickManager.TicksGame;
                int window = 600;

                foreach (var entry in Find.PlayLog.AllEntries
                    .Where(e => e.Tick >= now - window)
                    .OrderByDescending(e => e.Tick))
                {
                    if (!(entry is PlayLogEntry_Interaction)) continue;

                    if (def != null)
                    {
                        var intDefField = HarmonyLib.AccessTools.Field(entry.GetType(), "intDef");
                        var entryDef = intDefField?.GetValue(entry) as InteractionDef;
                        if (entryDef != def) continue;
                    }

                    string text = entry.ToGameStringFromPOV(a);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = ColorTagRegex.Replace(text, "");
                    text = text.Replace("</color>", "").Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    string textLower = text.ToLowerInvariant();
                    if (!textLower.Contains(a.LabelShort.ToLowerInvariant())) continue;
                    if (!textLower.Contains(b.LabelShort.ToLowerInvariant())) continue;

                    return text;
                }
            }
            catch { }
            return null;
        }

        // ── Environment Section ───────────────────────────────────────────────────

		private static string BuildEnvironmentSection(Pawn pawn)
		{
			if (pawn?.Map == null) return "";
			var sb = new StringBuilder();
			
			// Etiquetamos el bloque para dejar claro su carácter secundario
			sb.AppendLine("=== PASSIVE BACKGROUND ATMOSPHERE (DO NOT TALK ABOUT THIS UNLESS DIRE) ===");

			var map = pawn.Map;
			int hour = GenLocalDate.HourOfDay(map);
			sb.AppendLine($"Time: {hour:D2}:00 ({GetTimeDesc(hour)})");
			sb.AppendLine($"Season: {GenLocalDate.Season(map).Label()}");
			float temp = pawn.AmbientTemperature;
			string tempDesc = temp < 0f ? "freezing" : temp < 15f ? "cold" : temp < 30f ? "comfortable" : "hot";
			sb.AppendLine($"Temperature: {temp:F0}°C ({tempDesc})");
			sb.AppendLine($"Location: {GetLocationDesc(pawn)}");

			// Solo destacamos el clima si es un evento extremo real
			var curWeather = map.weatherManager?.curWeather;
			if (curWeather != null)
			{
				string wLabel = curWeather.label.ToLowerInvariant();
				bool isExtreme = wLabel.Contains("fallout") || wLabel.Contains("wave") || 
								 wLabel.Contains("snap") || wLabel.Contains("flashstorm");

				if (isExtreme)
				{
					sb.AppendLine($"Extreme Weather Hazard: {curWeather.label.ToUpper()}");
				}
				else
				{
					// Para climas normales (niebla, lluvia, despejado), lo marcamos explícitamente como irrelevante para el diálogo
					sb.AppendLine($"Weather: {curWeather.label} (purely visual atmosphere — ignore in conversation)");
				}
			}

			return sb.ToString();
		}

        // ── Pawn Context Section ──────────────────────────────────────────────────

        private static string BuildPawnSection(Pawn pawn, string role)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {role}: {pawn.LabelShort} ===");

            int age = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            bool ignoreAge = ColonistPromptManager.GetIgnoreAge(pawn);
            sb.AppendLine($"Age: {age}, {pawn.gender}");
            if (ignoreAge)
            {
                sb.AppendLine("[AGE NOTE: Biological age is irrelevant for this character. " +
                              "Do NOT write them as a child — follow their custom instructions instead.]");
            }
            else
            {
                string ageBehavior = GetAgeBehavior(age);
                if (!string.IsNullOrEmpty(ageBehavior))
                    sb.AppendLine($"[AGE NOTE: {ageBehavior}]");
            }

            sb.AppendLine(GetDetailedStatus(pawn));

            string xenoInfo = GetXenotypeInfo(pawn);
            if (!string.IsNullOrEmpty(xenoInfo))
                sb.AppendLine(xenoInfo);

            string backstory = GetBackstorySummary(pawn);
            if (!string.IsNullOrEmpty(backstory))
                sb.AppendLine($"Background: {backstory}");

            if (pawn.story?.traits?.allTraits != null && pawn.story.traits.allTraits.Any())
            {
                var traitEntries = pawn.story.traits.allTraits.Select(t =>
                {
                    string desc = t.def.description;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        desc = CleanXmlTags(desc);
                        if (desc.Length > 100) desc = desc.Substring(0, 97) + "...";
                        return $"{t.LabelCap}: {desc}";
                    }
                    return t.LabelCap;
                });
                sb.AppendLine($"Personality:\n  - {string.Join("\n  - ", traitEntries)}");
            }

            string traitSpeech = GetTraitSpeechStyle(pawn);
            if (!string.IsNullOrEmpty(traitSpeech))
                sb.AppendLine(traitSpeech);

            if (pawn.RaceProps?.Animal == true)
            {
                bool intelligent = Animals.AnimalPromptManager.GetIsIntelligent(pawn);
                string species = pawn.kindDef?.label ?? pawn.def?.label ?? "animal";
                if (intelligent)
                    sb.AppendLine($"[ANIMAL — INTELLIGENT: {pawn.LabelShort} is a {species} capable of speech. Speaks in words with animal mannerisms.]");
                else
                    sb.AppendLine($"[ANIMAL — {species.ToUpper()}: Communicates ONLY through sounds and body language. Express as *actions* or sounds.]");
            }

            if (pawn.InMentalState && pawn.MentalState?.def != null)
                sb.AppendLine($"[MENTAL STATE ACTIVE: {pawn.MentalState.def.label.ToUpper()} — behavior is driven by this compulsion]");

            float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
            sb.AppendLine($"Mood: {GetMoodDesc(mood)} ({mood:P0})");

            var topThoughts = pawn.needs?.mood?.thoughts?.memories?.Memories?
                .Where(t => t != null && System.Math.Abs(t.MoodOffset()) > 3f)
                .OrderByDescending(t => System.Math.Abs(t.MoodOffset()))
                .Take(3).ToList();
            if (topThoughts != null && topThoughts.Any())
                sb.AppendLine($"Feelings: {string.Join(", ", topThoughts.Select(t => t.LabelCap))}");

            if (IsGrieving(pawn))
                sb.AppendLine("Grieving: Recently lost someone from the colony.");

            float food = pawn.needs?.food?.CurLevel ?? 1f;
            if (food < 0.15f)      sb.AppendLine("Hunger: Starving — barely able to focus.");
            else if (food < 0.3f)  sb.AppendLine("Hunger: Very hungry — distracting.");
            else if (food < 0.5f)  sb.AppendLine("Hunger: Hungry.");

            float rest = pawn.needs?.rest?.CurLevel ?? 1f;
            if (rest < 0.15f)      sb.AppendLine("Rest: Exhausted — slow or irritable.");
            else if (rest < 0.3f)  sb.AppendLine("Rest: Very tired.");

            string drugState = GetDrugState(pawn);
            if (!string.IsNullOrEmpty(drugState))
                sb.AppendLine(drugState);

            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.35f)
                sb.AppendLine($"Pain: {GetPainDesc(pain)} ({pain:P0})");

            float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            if (consciousness < 0.85f)
                sb.AppendLine($"Consciousness: {consciousness:P0} — groggy, impaired thinking.");

            string mobilityNote = GetMobilityNote(pawn, ignoreAge ? 20 : age);
            if (!string.IsNullOrEmpty(mobilityNote))
                sb.AppendLine(mobilityNote);

            float manip = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 1f;
            if (manip < 0.5f)      sb.AppendLine("Manipulation: Severely limited.");
            else if (manip < 0.8f) sb.AppendLine("Manipulation: Reduced.");

            string speechNote = GetSpeechImpediment(pawn);
            if (!string.IsNullOrEmpty(speechNote))
                sb.AppendLine(speechNote);

            var injuries = GetSignificantHealthConditions(pawn);
            if (injuries.Any())
                sb.AppendLine($"Health conditions: {string.Join(", ", injuries)}");

            if (pawn.CurJob?.def != null)
                sb.AppendLine($"Currently: {pawn.CurJob.def.reportString ?? pawn.CurJob.def.label}");

            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                sb.AppendLine($"Ideology: {pawn.Ideo.name}");

            string customPrompt = ColonistPromptManager.GetPrompt(pawn);
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine("# Custom Instructions:");
                sb.AppendLine(customPrompt.Trim());
            }

            return sb.ToString();
        }

        // ── Relationship Section ──────────────────────────────────────────────────

        private static string BuildRelationshipSection(Pawn a, Pawn b)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATIONSHIP & POWER DYNAMIC ===");

            bool aIsAnimal = a.RaceProps?.Animal == true;
            bool bIsAnimal = b.RaceProps?.Animal == true;

            if (aIsAnimal || bIsAnimal)
            {
                Pawn colonist = aIsAnimal ? b : a;
                Pawn animal = aIsAnimal ? a : b;
                bool animalIntelligent = Animals.AnimalPromptManager.GetIsIntelligent(animal);

                Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
                Pawn master = animal.playerSettings?.Master;
                bool isBonded = bondedTo == colonist;
                bool isMaster = master == colonist;

                if (isBonded)
                    sb.AppendLine($"Bond: {colonist.LabelShort} and {animal.LabelShort} share a deep emotional bond.");
                else if (isMaster)
                    sb.AppendLine($"Handler: {colonist.LabelShort} is {animal.LabelShort}'s trainer/master.");
                else
                    sb.AppendLine($"Familiarity: {colonist.LabelShort} and {animal.LabelShort} know each other as colony members.");

                string attitude = GetColonistAnimalAttitude(colonist, animal, animalIntelligent, isBonded);
                sb.AppendLine(attitude);

                int animalOpinion = animal.relations?.OpinionOf(colonist) ?? 0;
                string animalFeel = animalOpinion >= 50 ? "trusts and loves"
                                  : animalOpinion >= 10 ? "is comfortable with"
                                  : animalOpinion > -20 ? "is neutral toward"
                                  : "is wary or afraid of";
                sb.AppendLine($"{animal.LabelShort} {animalFeel} {colonist.LabelShort}.");

                return sb.ToString();
            }

            int opinionAB = a.relations?.OpinionOf(b) ?? 0;
            int opinionBA = b.relations?.OpinionOf(a) ?? 0;
            sb.AppendLine($"{a.LabelShort} → {b.LabelShort}: {opinionAB} ({GetOpinionDesc(opinionAB)})");
            sb.AppendLine($"{b.LabelShort} → {a.LabelShort}: {opinionBA} ({GetOpinionDesc(opinionBA)})");

            var relationsAB = a.relations?.DirectRelations?.Where(r => r.otherPawn == b && r.def != null).Select(r => r.def) ?? Enumerable.Empty<PawnRelationDef>();
            var relationsBA = b.relations?.DirectRelations?.Where(r => r.otherPawn == a && r.def != null).Select(r => r.def) ?? Enumerable.Empty<PawnRelationDef>();
            var allRelDefs = relationsAB.Concat(relationsBA).Distinct().ToList();

            if (allRelDefs.Any())
                sb.AppendLine($"Relation: {string.Join(", ", allRelDefs.Select(r => r.label))}");
            else
                sb.AppendLine("Relation: Acquaintances");

            // LLAMADA CENTRALIZADA
            string familyTone = RelationsPromptClassifier.GetFamilyTone(a, b, allRelDefs, opinionAB, opinionBA);
            if (!string.IsNullOrEmpty(familyTone))
                sb.AppendLine(familyTone);

            string powerDynamic = GetPowerDynamic(a, b);
            if (!string.IsNullOrEmpty(powerDynamic))
                sb.AppendLine(powerDynamic);

            return sb.ToString();
        }

        // ── Shared Memory & Events ────────────────────────────────────────────────

        private static string BuildSharedMemorySection(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
                return string.Empty;

            var memoryManager = ColonistMemoryManager.GetOrCreate();
            if (memoryManager == null)
                return string.Empty;

            var initTracker = memoryManager.GetTrackerFor(initiator);
            var recTracker = memoryManager.GetTrackerFor(recipient);

            // 1. Obtener interacciones del día
            var initInteractions = initTracker?.GetCurrentDayInteractions();
            var recInteractions = recTracker?.GetCurrentDayInteractions();

            string initName = initiator.LabelShort;
            string recName = recipient.LabelShort;

            // Listas predimensionadas para evitar redimensionar memoria dinámicamente
            var directInteractions = new List<RawInteraction>(MaxDirectInteractionsToday);
            var initGeneral = new List<RawInteraction>(MaxGeneralInteractionsToday);
            var recGeneral = new List<RawInteraction>(MaxGeneralInteractionsToday);

            // 2 y 3. Recorrido inverso (de más reciente a más antigua) con interrupción temprana
            if (initInteractions != null)
            {
                for (int i = initInteractions.Count - 1; i >= 0; i--)
                {
                    var interaction = initInteractions[i];
                    if (interaction == null || string.IsNullOrWhiteSpace(interaction.text)) continue;

                    if (IsDirectInteractionWith(interaction, recName))
                    {
                        if (directInteractions.Count < MaxDirectInteractionsToday)
                            directInteractions.Add(interaction);
                    }
                    else
                    {
                        if (initGeneral.Count < MaxGeneralInteractionsToday)
                            initGeneral.Add(interaction);
                    }

                    if (directInteractions.Count >= MaxDirectInteractionsToday && initGeneral.Count >= MaxGeneralInteractionsToday)
                        break;
                }
            }

            if (recInteractions != null)
            {
                for (int i = recInteractions.Count - 1; i >= 0; i--)
                {
                    var interaction = recInteractions[i];
                    if (interaction == null || string.IsNullOrWhiteSpace(interaction.text)) continue;

                    if (IsDirectInteractionWith(interaction, initName))
                    {
                        if (directInteractions.Count < MaxDirectInteractionsToday && !directInteractions.Contains(interaction))
                            directInteractions.Add(interaction);
                    }
                    else
                    {
                        if (recGeneral.Count < MaxGeneralInteractionsToday)
                            recGeneral.Add(interaction);
                    }

                    if (directInteractions.Count >= MaxDirectInteractionsToday && recGeneral.Count >= MaxGeneralInteractionsToday)
                        break;
                }
            }

            // Invertir para restaurar el orden cronológico original (antiguo -> reciente) para el LLM
            directInteractions.Reverse();
            initGeneral.Reverse();
            recGeneral.Reverse();

            // 4. Memorias pasadas
            string initPast = GetFormattedPastMemories(initTracker, days: 2);
            string recPast = GetFormattedPastMemories(recTracker, days: 2);

            // StringBuilder predimensionado a 1KB para evitar expansiones de buffer interno
            var sb = new StringBuilder(1024);

            // ==========================================
            // BLOQUE 1: CONTEXTO INDIVIDUAL DE COLONO A
            // ==========================================
            bool hasInitPast = !string.IsNullOrWhiteSpace(initPast);
            bool hasInitGeneral = initGeneral.Count > 0;

            if (hasInitPast || hasInitGeneral)
            {
                sb.AppendLine($"=== BACKGROUND & TODAY'S CONTEXT ({initName.ToUpper()}) ===");

                if (hasInitPast)
                {
                    sb.AppendLine("# Past Memories:");
                    sb.AppendLine(initPast.Trim());
                }

                if (hasInitGeneral)
                {
                    if (hasInitPast) sb.AppendLine();
                    sb.AppendLine("# Other Recent Interactions Today:");
                    for (int i = 0; i < initGeneral.Count; i++)
                    {
                        var interaction = initGeneral[i];
                        if (!string.IsNullOrWhiteSpace(interaction.text))
                            sb.AppendLine($"- {interaction.ToPromptFormat()}");
                    }
                }
                sb.AppendLine();
            }

            // ==========================================
            // BLOQUE 2: CONTEXTO INDIVIDUAL DE COLONO B
            // ==========================================
            bool hasRecPast = !string.IsNullOrWhiteSpace(recPast);
            bool hasRecGeneral = recGeneral.Count > 0;

            if (hasRecPast || hasRecGeneral)
            {
                sb.AppendLine($"=== BACKGROUND & TODAY'S CONTEXT ({recName.ToUpper()}) ===");

                if (hasRecPast)
                {
                    sb.AppendLine("# Past Memories:");
                    sb.AppendLine(recPast.Trim());
                }

                if (hasRecGeneral)
                {
                    if (hasRecPast) sb.AppendLine();
                    sb.AppendLine("# Other Recent Interactions Today:");
                    for (int i = 0; i < recGeneral.Count; i++)
                    {
                        var interaction = recGeneral[i];
                        if (!string.IsNullOrWhiteSpace(interaction.text))
                            sb.AppendLine($"- {interaction.ToPromptFormat()}");
                    }
                }
                sb.AppendLine();
            }

            // ==========================================
            // BLOQUE 3: INTERACCIONES DIRECTAS ENTRE AMBOS
            // ==========================================
            if (directInteractions.Count > 0)
            {
                sb.AppendLine($"=== DIRECT INTERACTIONS TODAY ({initName.ToUpper()} <-> {recName.ToUpper()}) ===");
                for (int i = 0; i < directInteractions.Count; i++)
                {
                    var interaction = directInteractions[i];
                    if (!string.IsNullOrWhiteSpace(interaction.text))
                    {
                        sb.AppendLine($"- {interaction.ToPromptFormat()}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ── Métodos Auxiliares ───────────────────────────────────────────────────

        /// <summary>
        /// Comprueba si en los participantes de la interacción figura el colono objetivo.
        /// </summary>
        private static bool IsDirectInteractionWith(RawInteraction interaction, string targetName)
        {
            if (interaction == null || string.IsNullOrWhiteSpace(targetName) || interaction.participants == null)
                return false;

            return interaction.participants.Any(p => p.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        }
        private static string GetFormattedPastMemories(ColonistMemoryTracker tracker, int days)
        {
            if (tracker == null) return null;
            var past = tracker.GetLastMemories(days);
            if (past == null || !past.Any()) return null;

            return string.Join("\n", past.Select(m => $"• {m.Trim()}"));
        }

        private static string BuildColonyEventsSection()
        {
            if (Find.PlayLog == null) return "";

            int now = Find.TickManager.TicksGame;
            int oneDayAgo = now - 60000;
            int justNow = now - 600;

            var lines = new List<string>();

            foreach (var entry in Find.PlayLog.AllEntries
                .Where(e => e.Tick >= oneDayAgo && e.Tick < justNow)
                .OrderByDescending(e => e.Tick)
                .Take(10))
            {
                try
                {
                    string text = entry.ToGameStringFromPOV(null);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = CleanXmlTags(text);
                    if (!string.IsNullOrWhiteSpace(text))
                        lines.Add($"- {text}");

                    if (lines.Count >= 5) break;
                }
                catch { }
            }

            if (!lines.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== RECENT RECENT LOGGED EVENTS ===");
            foreach (var line in lines) sb.AppendLine(line);
            return sb.ToString();
        }

        // ── Helpers & Calculations ────────────────────────────────────────────────

        private static string CleanXmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return TagRegex.Replace(input, "").Trim();
        }

        private static bool IsKnownVanillaInteraction(InteractionDef def)
        {
            string n = def.defName?.ToLower() ?? "";
            return n.Contains("chitchat")  || n.Contains("deeptalk") ||
                   n.Contains("romance")   || n.Contains("insult")   ||
                   n.Contains("slight")    || n.Contains("kind")     ||
                   n.Contains("rapport")   || n.Contains("comfort")  ||
                   n.Contains("joke")      || n.Contains("complain") ||
                   n.Contains("convert")   || n.Contains("recruit")  ||
                   n.Contains("rebelstir") || n.Contains("prisonbreak");
        }

        private static string GetAgeBehavior(int age)
        {
            if (age <= 1)  return "INFANT — Cries, coos, babbles. Cannot form sentences.";
            if (age <= 3)  return "TODDLER — Very simple words, short attention span.";
            if (age <= 6)  return "YOUNG CHILD — Simple sentences, innocent, asks questions.";
            if (age <= 10) return "CHILD — Enthusiastic, childlike perspective.";
            if (age <= 13) return "PRETEEN — Naive but trying to sound mature.";
            if (age <= 17) return "TEENAGER — Direct, emotional, wants respect.";
            return null;
        }

        private static string GetDetailedStatus(Pawn pawn)
        {
            if (pawn.IsSlaveOfColony)
            {
                string willDesc = "";
                if (pawn.guest != null)
                {
                    float will = pawn.guest.will;
                    if (will < 0.2f)      willDesc = " Will broken — resigned, fearful.";
                    else if (will < 0.5f) willDesc = " Will subdued — compliant, secret resentment.";
                    else                  willDesc = " Will strong — openly resents captivity.";
                }
                return $"Status: SLAVE — forced obedience.{willDesc}";
            }

            if (pawn.IsPrisonerOfColony)
            {
                bool recruitable = pawn.guest?.Recruitable == true;
                string resistDesc = "";
                if (pawn.guest != null)
                {
                    float res = pawn.guest.resistance;
                    if (res > 20f)        resistDesc = " Highly resistant, deeply hostile.";
                    else if (res > 5f)    resistDesc = " Resistance wavering.";
                    else if (res <= 0f && recruitable) resistDesc = " Resistance broken — considering joining.";
                }
                string intent = recruitable ? "Prisoner open to recruitment." : "Prisoner held against will.";
                return $"Status: PRISONER — {intent}{resistDesc}";
            }

            return pawn.IsFreeColonist ? "Status: Free colonist" : "Status: Colony member";
        }

        private static string GetXenotypeInfo(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null) return null;

            string xenoName = pawn.genes.xenotypeName ?? pawn.genes.Xenotype?.label ?? "baseline";
            if (pawn.genes.Xenotype == XenotypeDefOf.Baseliner && string.IsNullOrEmpty(pawn.genes.xenotypeName))
                return null;

            var parts = new List<string> { $"Xenotype: {xenoName}" };

            if (pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner)
            {
                string desc = CleanXmlTags(pawn.genes.Xenotype.description ?? "");
                if (desc.Length > 100) desc = desc.Substring(0, 97) + "...";
                if (!string.IsNullOrEmpty(desc)) parts.Add($"({desc})");
            }

            var hemogenDef = DefDatabase<NeedDef>.GetNamedSilentFail("Hemogen");
            if (hemogenDef != null)
            {
                var hemogenNeed = pawn.needs?.TryGetNeed(hemogenDef);
                if (hemogenNeed != null)
                {
                    float level = hemogenNeed.CurLevelPercentage;
                    string hemDesc = level < 0.2f ? "CRITICALLY low hemogen — needs blood urgently"
                                   : level < 0.5f ? "low hemogen — needs blood soon"
                                   : "requires blood (hemogen) to survive";
                    parts.Add(hemDesc);
                }
            }

            var appearGenes = pawn.genes?.GenesListForReading?
                .Where(g => g.Active && g.def.displayCategory != null &&
                    (g.def.displayCategory.defName == "Cosmetic" ||
                     g.def.displayCategory.defName == "Skin" ||
                     g.def.displayCategory.defName == "Body"))
                .Select(g => g.def.label)
                .Take(3).ToList();

            if (appearGenes != null && appearGenes.Any())
                parts.Add($"Appearance: {string.Join(", ", appearGenes)}");

            return string.Join(" | ", parts);
        }

        private static string GetBackstorySummary(Pawn pawn)
        {
            if (pawn.story == null) return null;

            var childhood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);

            var parts = new List<string>();

            if (childhood != null)
            {
                string desc = CleanBackstoryText(childhood.baseDesc ?? childhood.title ?? "", pawn);
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
                parts.Add($"Childhood ({childhood.title}): {desc}");
            }

            if (adulthood != null)
            {
                string desc = CleanBackstoryText(adulthood.baseDesc ?? adulthood.title ?? "", pawn);
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
                parts.Add($"Adulthood ({adulthood.title}): {desc}");
            }

            return parts.Any() ? string.Join(" | ", parts) : null;
        }

        private static string CleanBackstoryText(string text, Pawn pawn)
        {
            text = CleanXmlTags(text);
            return text.Replace("[PAWN_nameDef]", pawn.LabelShort)
                       .Replace("[PAWN_pronoun]", pawn.gender == Gender.Male ? "he" : "she")
                       .Replace("[PAWN_possessive]", pawn.gender == Gender.Male ? "his" : "her")
                       .Trim();
        }

        private static bool IsGrieving(Pawn pawn)
        {
            try
            {
                return pawn.needs?.mood?.thoughts?.memories?.Memories?
                    .Any(t => t is Thought_MemorySocial soc &&
                              soc.def?.defName?.Contains("Died") == true &&
                              soc.otherPawn?.Dead == true) == true;
            }
            catch { return false; }
        }

        private static string GetMobilityNote(Pawn pawn, int age)
        {
            if (age <= 1) return null;
            if (age <= 3) return "[MOBILITY: Toddler — walks unsteadily]";

            float moving = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
            if (moving >= 0.5f) return null;

            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return $"[MOBILITY: {moving:P0} — severely impaired]";

            if (hediffSet.hediffs.Any(h => h.def?.defName == "ParalyticAbasia"))
                return "[MOBILITY: PARALYTIC ABASIA — cannot walk due to neurological condition]";

            var missingLegs = hediffSet.hediffs.OfType<Hediff_MissingPart>()
                .Where(h => h.Part?.def?.defName == "Leg" || h.Part?.def?.defName == "Foot")
                .ToList();

            if (missingLegs.Count >= 2) return "[MOBILITY: Both legs missing]";
            if (missingLegs.Count == 1) return "[MOBILITY: One leg missing]";

            return $"[MOBILITY: {moving:P0} — movement severely impaired]";
        }

        private static string GetDrugState(Pawn pawn)
        {
            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;

            var states = new List<string>();

            foreach (var h in hediffs)
            {
                if (h?.def == null) continue;
                switch (h.def.defName)
                {
                    case "GoJuiceHigh": states.Add("HIGH on Go-Juice: hyper-alert, aggressive energy"); break;
                    case "YayoHigh": states.Add("HIGH on Yayo: euphoric, grandiose, talking fast"); break;
                    case "FlakeHigh": states.Add("HIGH on Flake: intense euphoria"); break;
                    case "WakeUpHigh": states.Add("HIGH on Wake-Up: sharp, impatient"); break;
                    case "BeerHigh":
                        states.Add(h.Severity < 0.4f ? "Slightly drunk" : h.Severity < 0.8f ? "Drunk (slurring)" : "Very drunk");
                        break;
                    case "SmokeleafHigh": states.Add("HIGH on Smokeleaf: relaxed, slow, distracted"); break;
                    case "Anesthetic": states.Add("Under ANESTHESIA: groggy, confused"); break;
                }
            }

            return states.Any() ? $"[CHEMICAL STATE: {string.Join("; ", states)}]" : null;
        }

        private static string GetSpeechImpediment(Pawn pawn)
        {
            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return null;

            var notes = new List<string>();
            foreach (var h in hediffSet.hediffs)
            {
                if (h?.def == null) continue;
                if (h.def.defName == "MissingBodyPart" && h.Part?.def?.defName == "Tongue")
                    notes.Add("TONGUE REMOVED — garbled speech, short words/gestures only");
                else if (h.Part?.def?.defName == "Jaw" && h.def.defName == "MissingBodyPart")
                    notes.Add("JAW MISSING — speech mostly grunts/single syllables");
            }

            float talking = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 1f;
            if (talking < 0.5f && !notes.Any())
                notes.Add($"Talking capacity severely reduced ({talking:P0})");

            return notes.Any() ? $"[SPEECH: {string.Join("; ", notes)}]" : null;
        }

        private static List<string> GetSignificantHealthConditions(Pawn pawn)
        {
            var result = new List<string>();
            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return result;

            var badHediffs = hediffSet.hediffs
                .Where(h => h.Visible && h.def.isBad && h.def.defName != "MissingBodyPart" && h.Severity > 0.25f)
                .OrderByDescending(h => h.Severity)
                .Take(3)
                .Select(h => h.def.label);

            result.AddRange(badHediffs);
            return result;
        }

        private static string GetPainDesc(float pain)
        {
            if (pain > 0.75f) return "agonizing";
            if (pain > 0.50f) return "severe";
            if (pain > 0.25f) return "moderate";
            return "mild";
        }

        


        private static string GetColonistAnimalAttitude(Pawn colonist, Pawn animal, bool animalIntelligent, bool isBonded)
        {
            var traits = colonist.story?.traits?.allTraits;
            bool isPsycho = traits?.Any(t => t.def.defName == "Psychopath") == true;

            if (isPsycho && !isBonded)
                return $"[{colonist.LabelShort} ATTITUDE: Psychopath — sees {animal.LabelShort} purely as a tool.]";

            if (animalIntelligent)
                return $"[{colonist.LabelShort} ATTITUDE: Aware {animal.LabelShort} is fully sentient and capable of speech.]";

            return $"[{colonist.LabelShort} ATTITUDE: Treats {animal.LabelShort} as a regular animal companion.]";
        }

        private static string GetPawnMemory(Pawn pawn)
        {
            try
            {
                var manager = ColonistMemoryManager.GetOrCreate();
                var tracker = manager?.GetTrackerFor(pawn);
                var recent = tracker?.GetLastMemories(5);
                if (recent == null || recent.Count == 0) return null;

                string combined = string.Join(" | ", recent);
                return combined.Length > 400 ? combined.Substring(combined.Length - 400).Trim() : combined.Trim();
            }
            catch { return null; }
        }

        private static string GetGroupMemory()
        {
            try
            {
                var manager = ColonistMemoryManager.GetOrCreate();
                var groupTracker = manager?.GetGroupMemoryTracker();
                var recent = groupTracker?.GetAllRecentMemories(3);
                if (recent == null || recent.Count == 0) return null;

                return string.Join("\n", recent.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            }
            catch { return null; }
        }

        private static string GetTimeDesc(int hour)
        {
            if (hour < 5)  return "early morning";
            if (hour < 9)  return "morning";
            if (hour < 14) return "midday";
            if (hour < 18) return "afternoon";
            if (hour < 22) return "evening";
            return "night";
        }

        private static string GetLocationDesc(Pawn pawn)
        {
            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return "outdoors";
            if (room.ContainedBeds.Any()) return room.ContainedBeds.Count() == 1 ? "a bedroom" : "the barracks";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b.def.defName.Contains("Stove"))) return "the kitchen";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b is Building_WorkTable)) return "a workshop";
            return "indoors";
        }

        private static string GetInteractionNature(InteractionDef def)
        {
            string n = def.defName?.ToLower() ?? "";
            if (n.Contains("chitchat")) return "Casual small talk";
            if (n.Contains("deeptalk")) return "Deep conversation";
            if (n.Contains("romance"))  return "Flirtatious exchange";
            if (n.Contains("insult"))   return "Hostile insult";
            if (n.Contains("slight"))   return "Minor slight";
            return "General interaction";
        }

        private static string GetMoodDesc(float mood)
        {
            if (mood > 0.8f) return "very happy";
            if (mood > 0.6f) return "content";
            if (mood > 0.4f) return "neutral";
            if (mood > 0.2f) return "stressed";
            return "on the edge of breakdown";
        }

        private static string GetOpinionDesc(int opinion)
        {
            if (opinion >= 75)  return "deeply bonded";
            if (opinion >= 50)  return "very positive";
            if (opinion >= 20)  return "friendly";
            if (opinion > -20)  return "neutral";
            if (opinion > -50)  return "tense";
            return "hostile";
        }

        private static string GetPowerDynamic(Pawn a, Pawn b)
        {
            if (a.IsFreeColonist && b.IsPrisoner)
                return $"POWER DYNAMIC: {a.LabelShort} is free; {b.LabelShort} is a prisoner under authority.";
            if (a.IsPrisoner && b.IsFreeColonist)
                return $"POWER DYNAMIC: {a.LabelShort} is captive; {b.LabelShort} holds authority.";
            if (a.IsFreeColonist && b.IsSlaveOfColony)
                return $"POWER DYNAMIC: {a.LabelShort} is a colonist; {b.LabelShort} is enslaved.";
            if (a.IsSlaveOfColony && b.IsSlaveOfColony)
                return $"POWER DYNAMIC: Both are enslaved — mutual solidarity or shared struggle.";

            return null;
        }

        private static string GetTraitSpeechStyle(Pawn pawn)
        {
            var traits = pawn.story?.traits?.allTraits;
            if (traits == null || !traits.Any()) return null;

            var notes = new List<string>();
            foreach (var trait in traits)
            {
                if (trait?.def == null) continue;
                switch (trait.def.defName)
                {
                    case "Psychopath": notes.Add("speaks without empathy, transactional"); break;
                    case "Abrasive":   notes.Add("blunt and direct, no filter"); break;
                    case "Kind":       notes.Add("considerate and warm"); break;
                    case "Neurotic":   notes.Add("worries out loud, overthinks"); break;
                    case "Loner":      notes.Add("short answers, uncomfortable socializing"); break;
                }
            }

            return notes.Any() ? $"[SPEECH STYLE: {string.Join(" / ", notes)}]" : null;
        }

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
    }
}