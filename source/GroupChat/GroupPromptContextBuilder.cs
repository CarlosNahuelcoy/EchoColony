using System.Text;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using System;

namespace EchoColony
{
    /// <summary>
    /// Builds the AI prompt for a single pawn's turn in a group conversation.
    ///
    /// Design principles:
    /// - Crystal-clear identity: "You are [Name] and only [Name]."
    /// - The model must never generate dialogue for other participants.
    /// - Context is kept tight — enough for immersion, not so much it confuses the model.
    /// - System messages (join/leave/separator lines) are stripped from dialogue history
    ///   but surfaced as a contextual note when relevant.
    /// - Verified colony history from TaleManager prevents hallucinated past events.
    /// </summary>
    public static class GroupPromptContextBuilder
    {
        /// <param name="isFirstTurn">True when no colonist has spoken yet this round.</param>
        /// <param name="isLateJoiner">True when this pawn is speaking for the first time
        ///   but the conversation was already in progress.</param>
        public static string Build(
            Pawn         speaker,
            List<Pawn>   group,
            List<string> recentHistory,
            List<string> currentConversation,
            string       userMessage,
            bool         isFirstTurn,
            bool         isLateJoiner = false)
        {
            var sb = new StringBuilder();

            // 1. Identity — must be the very first thing the model sees
            AppendIdentity(sb, speaker, group);

            // 2. Character context (compact — groups don't need the full individual prompt)
            AppendCharacterContext(sb, speaker, group);

            // 3. Ideology (important — colonists should reflect their beliefs)
            AppendIdeology(sb, speaker);

            // 4. Custom prompts (global + per-pawn overrides)
            AppendCustomPrompts(sb, speaker);

            // 5. NEW: Verified colony history — only source of past events
            AppendVerifiedTales(sb, speaker, group);

            // 6. Recent memories from individual and group chats
            AppendMemories(sb, speaker);

            // 7. The actual conversation happening right now
            AppendConversationHistory(sb, recentHistory, currentConversation, userMessage, isFirstTurn, speaker);

            // 8. Final instruction — explicit, no room for misinterpretation
            AppendResponseInstruction(sb, speaker, group, isFirstTurn, isLateJoiner);

            return sb.ToString().Trim();
        }

        // ── 1. Identity ──────────────────────────────────────────────────────────

        private static void AppendIdentity(StringBuilder sb, Pawn speaker, List<Pawn> group)
        {
            string others = string.Join(", ", group
                .Where(p => p != speaker)
                .Select(p => p.LabelShort));

            sb.AppendLine("[SYSTEM]");
            sb.AppendLine($"You are {speaker.LabelShort}. You are ONLY {speaker.LabelShort}.");
            sb.AppendLine($"You are having a group conversation with: {others}.");
            sb.AppendLine("Everyone can hear each other.");
            sb.AppendLine();

            // ── Anti-hallucination rules — placed right after identity so they
            // are the first constraint the model receives before any context.
            sb.AppendLine("STRICT GROUNDING RULES:");
            sb.AppendLine("1. A section called 'VERIFIED COLONY HISTORY' will be provided below.");
            sb.AppendLine("   That is the ONLY source of past events you may reference.");
            sb.AppendLine("2. If a technology, building, item, animal, or event is NOT in that section");
            sb.AppendLine("   or in the current game state, it does NOT exist in this colony. Do not invent it.");
            sb.AppendLine("3. If you have no relevant history for a topic, stay in the present or say");
            sb.AppendLine("   you don't recall — never fabricate a memory.");
            sb.AppendLine("4. These rules override creativity. An invented fact that breaks immersion");
            sb.AppendLine("   is always worse than a short, honest answer.");
            sb.AppendLine();
        }

        // ── 2. Character context ─────────────────────────────────────────────────

        private static void AppendCharacterContext(StringBuilder sb, Pawn speaker, List<Pawn> group)
        {
            sb.AppendLine("# Your character:");

            sb.AppendLine(ColonistPromptContextBuilder.BuildSystemPromptPublic(speaker));

            int age = speaker.ageTracker?.AgeBiologicalYears ?? 0;
            string ageGuidance = GetAgeGuidance(age);
            if (!string.IsNullOrEmpty(ageGuidance))
                sb.AppendLine($"Age behavior: {ageGuidance}");

            sb.AppendLine(BuildBackstory(speaker));

            var traits = speaker.story?.traits?.allTraits?.Select(t => t.LabelCap).ToList();
            if (traits?.Any() == true)
                sb.AppendLine($"Traits: {string.Join(", ", traits.Take(5))}");

            float health = speaker.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            float mood   = speaker.needs?.mood?.CurInstantLevel ?? 1f;
            sb.AppendLine($"Health: {GetHealthLabel(health)}, Mood: {GetMoodLabel(mood)}");

            var thoughts = GetSignificantThoughts(speaker);
            if (thoughts.Any())
                sb.AppendLine($"Mood factors: {string.Join(", ", thoughts)}");

            string activity = speaker.jobs?.curDriver?.GetReport() ?? "idle";
            sb.AppendLine($"Currently: {activity}");

            var relations = GetGroupRelations(speaker, group);
            if (relations.Any())
                sb.AppendLine($"Relations in group: {string.Join("; ", relations)}");

            sb.AppendLine();
        }

        // ── 3. Ideology ──────────────────────────────────────────────────────────

        private static void AppendIdeology(StringBuilder sb, Pawn speaker)
        {
            if (!ModsConfig.IdeologyActive || speaker.Ideo == null) return;

            var    ideo     = speaker.Ideo;
            var    beliefs  = GetKeyBeliefs(ideo.PreceptsListForReading);
            string role     = ideo.GetRole(speaker)?.def.label;
            string roleText = role != null ? $" (role: {role})" : "";

            sb.AppendLine($"Your sacred ideology: {ideo.name}{roleText}");
            if (beliefs.Any())
                sb.AppendLine($"Core beliefs: {string.Join("; ", beliefs)}");
            sb.AppendLine("Speak with genuine conviction about these beliefs. Do not contradict them.");
            sb.AppendLine();
        }

        // ── 4. Custom prompts ────────────────────────────────────────────────────

        private static void AppendCustomPrompts(StringBuilder sb, Pawn speaker)
        {
            string global = MyMod.Settings?.globalPrompt ?? "";
            string custom = ColonistPromptManager.GetPrompt(speaker);

            if (!string.IsNullOrWhiteSpace(global))
                sb.AppendLine($"Global guidance: {global.Trim()}");
            if (!string.IsNullOrWhiteSpace(custom))
                sb.AppendLine($"Personal instructions: {custom.Trim()}");

            if (!string.IsNullOrWhiteSpace(global) || !string.IsNullOrWhiteSpace(custom))
                sb.AppendLine();
        }

        // ── 5. Memories ──────────────────────────────────────────────────────────
        //*Furel - new memory system* Old system get te last 3 memorys and trimed to 100 characters, wich make it unusable. Now share the same system and that individual chats,
        //however it will consume some more tokens but responses has to be more ritch. 
        private static void AppendMemories(StringBuilder sb, Pawn speaker)
        {
            var memoryManager = ColonistMemoryManager.GetOrCreate();
            if (memoryManager == null) return;

            var tracker = memoryManager.GetTrackerFor(speaker);
            if (tracker == null) return;

            sb.AppendLine("# CHARACTER HISTORY & CONTINUITY");

            // 1. Memorias de días anteriores
            var pastMemories = tracker.GetLastMemories(3);
            if (pastMemories != null && pastMemories.Any())
            {
                sb.AppendLine("## Long-Term Memories:");
                foreach (var mem in pastMemories)
                {
                    sb.AppendLine($"{mem}");
                }
            }

            // 2. Interacciones del día actual
            string todayInteractions = tracker.GetCurrentDayMemoryFormatted();
            if (!string.IsNullOrWhiteSpace(todayInteractions))
            {
                sb.AppendLine("## Today's Ongoing Interactions:");
                sb.AppendLine(todayInteractions);
            }

            sb.AppendLine();
        }

        // ── 6. NEW: Verified colony history ──────────────────────────────────────
        //
        // Pulls real events from TaleManager for the speaker AND the other group
        // members. Shared events (involving multiple participants) are listed first
        // since they're the most natural conversation material.

        private static void AppendVerifiedTales(StringBuilder sb, Pawn speaker, List<Pawn> group)
        {
            // For groups we use the first non-speaker as the "other" pawn to check
            // shared tales — a reasonable approximation that avoids iterating the
            // full tale list once per group member.
            Pawn firstOther = group.FirstOrDefault(p => p != speaker);

            var (shared, personal) = TalesCache.GetTalesForPair(
                speaker, firstOther,
                TalesCache.MAX_SHARED_TALES,
                TalesCache.MAX_PERSONAL_TALES);

            if (!shared.Any() && !personal.Any()) return;

            sb.AppendLine("# VERIFIED COLONY HISTORY (REAL EVENTS — USE ONLY THESE)");
            sb.AppendLine("These events ACTUALLY HAPPENED. Reference them freely.");
            sb.AppendLine("NEVER invent events, technology, or items not listed here.");
            sb.AppendLine();

            if (shared.Any())
            {
                sb.AppendLine("Shared events (you and others in this group were involved):");
                foreach (var t in shared) sb.AppendLine($"  • {t}");
                sb.AppendLine();
            }

            if (personal.Any())
            {
                sb.AppendLine("Your personal events:");
                foreach (var t in personal) sb.AppendLine($"  • {t}");
                sb.AppendLine();
            }
        }

        // ── 7. Conversation history ──────────────────────────────────────────────

        private static void AppendConversationHistory(
            StringBuilder sb,
            List<string>  recentHistory,
            List<string>  currentConversation,
            string        userMessage,
            bool          isFirstTurn,
            Pawn          speaker)
        {
            sb.AppendLine("# CONVERSATION HISTORY");

            const int MAX_TOTAL_INPUTS = 8;

            // 1. Calculamos cuántas líneas ocupa la ronda actual (Y)
            var currentLines = currentConversation ?? new List<string>();
            int yCount = currentLines.Count;

            int xBudget = Math.Max(0, MAX_TOTAL_INPUTS - yCount);

            // if (GroupChatSession.Instance.IsExpired())
            sb.AppendLine("# Group conversation so far:");

            var pastLines = (recentHistory ?? new List<string>())
                .Where(l => !GroupChatSession.IsSystemMessage(l))
                .Select(GroupChatSession.GetDisplayText)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(xBudget)
                .ToList();

            // ── 1. INTERACCIONES PREVIAS (Pasado - Snapshot X) ─────────────────────
            if (pastLines.Any())
            {
                sb.AppendLine("## Previous Interactions (Past context):");
                foreach (var line in pastLines)
                {
                    sb.AppendLine($"  {line}");
                }
                sb.AppendLine();
            }

            // ── 2. INTERACCIÓN ACTUAL (Detonante del jugador + Ronda activa Y) ──────
            sb.AppendLine("[USER]");
            sb.AppendLine("## Current Interaction:");

            // Detonador del jugador
            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                sb.AppendLine($"  {userMessage.Trim()}");
            }

            // Respuestas en tiempo real de los colonos durante esta ronda
            if (currentLines.Any())
            {
                if (!string.IsNullOrWhiteSpace(userMessage))
                    sb.AppendLine("Responses in this round so far:");

                foreach (var line in currentLines)
                {
                    sb.AppendLine($"  {line}");
                }
            }
            else if (string.IsNullOrWhiteSpace(userMessage) && !pastLines.Any())
            {
                sb.AppendLine("  (no messages yet)");
            }

            sb.AppendLine();
        }

        // ── 8. Response instruction ──────────────────────────────────────────────
        //*furel - improvement* Changed were it takes language from, now is from the game system, not the name folder.

        private static void AppendResponseInstruction(
            StringBuilder sb,
            Pawn          speaker,
            List<Pawn>    group,
            bool          isFirstTurn,
            bool          isLateJoiner)
        {
            string gameLanguage = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";

            sb.AppendLine("# Your response:");
            sb.AppendLine($"Write ONLY {speaker.LabelShort}'s response. ONE short paragraph.");
            sb.AppendLine($"Do NOT write dialogue for {string.Join(", ", group.Where(p => p != speaker).Select(p => p.LabelShort))}.");
            sb.AppendLine("Do NOT prefix your response with your name — just write what you say.");
            sb.AppendLine("Stay in character. Use casual, natural language.");
            sb.AppendLine("Keep your response SHORT — 1 to 3 sentences maximum, like a real casual conversation.");
            sb.AppendLine("Do NOT write long paragraphs. If you have more to say, save it for your next turn.");
            sb.AppendLine("NEVER reference technology, buildings, animals, or events not in VERIFIED COLONY HISTORY.");

            if (isLateJoiner)
            {
                sb.AppendLine();
                sb.AppendLine("You just joined this conversation that was already in progress.");
                sb.AppendLine("You can see from the history what was being discussed.");
                sb.AppendLine("Do NOT ask 'what are you talking about' — react naturally based on what you heard.");
                sb.AppendLine("A brief acknowledgment is fine, but dive into the topic.");
            }

            sb.AppendLine($"CRITICAL RULE: Respond in {gameLanguage}.");

            if (MyMod.Settings?.enableRoleplayResponses == true)
                sb.AppendLine("You may use *brief actions* for important moments, but keep them short.");

            if (MyMod.Settings?.ignoreDangersInConversations == true)
                sb.AppendLine("Do NOT mention dangers, enemies, threats, raids, or combat. Act as if everything is peaceful.");

            if (MyMod.Settings?.ignoreDangersInConversations != true)
            {
                string threats = GetThreatContext(speaker);
                if (!string.IsNullOrEmpty(threats))
                    sb.AppendLine($"Current situation: {threats}");
            }
        }

        // ── Support methods ──────────────────────────────────────────────────────

        private static string GetAgeGuidance(int age)
        {
            if (age <= 1)  return "Baby: only cries, coos, 'goo', 'maa' — no real words";
            if (age <= 3)  return "Toddler: 1-4 simple words only";
            if (age <= 6)  return "Young child: simple sentences, asks questions";
            if (age <= 10) return "Child: enthusiastic, childlike speech";
            if (age <= 13) return "Pre-teen: starting to sound more mature";
            if (age <= 17) return "Teen: emotional, direct, peer-focused";
            return "";
        }

        private static string GetHealthLabel(float h)
        {
            if (h >= 0.95f) return "fine";
            if (h >= 0.75f) return "mostly okay";
            if (h >= 0.5f)  return "injured";
            if (h >= 0.3f)  return "badly wounded";
            return "critical";
        }

        private static string GetMoodLabel(float m)
        {
            if (m >= 0.9f) return "great";
            if (m >= 0.7f) return "good";
            if (m >= 0.5f) return "okay";
            if (m >= 0.3f) return "upset";
            return "struggling";
        }

        private static List<string> GetSignificantThoughts(Pawn speaker)
        {
            return (speaker.needs?.mood?.thoughts?.memories?.Memories ?? new List<Thought_Memory>())
                .Where(t => t.VisibleInNeedsTab && Math.Abs(t.MoodOffset()) >= 7f)
                .Take(3)
                .Select(t => $"{t.LabelCap} ({(t.MoodOffset() > 0 ? "+" : "")}{t.MoodOffset():F0})")
                .ToList();
        }

        private static List<string> GetGroupRelations(Pawn speaker, List<Pawn> group)
        {
            var result = new List<string>();
            if (speaker.relations == null) return result;

            foreach (var other in group.Where(p => p != speaker).Take(6))
            {
                int opinionAB = speaker.relations.OpinionOf(other);
                int opinionBA = other.relations != null ? other.relations.OpinionOf(speaker) : 0;

                var relDefs = speaker.GetRelations(other).ToList();

                // 1. Llamada a la clase unificada
                string familyTone = RelationsPromptClassifier.GetFamilyTone(speaker, other, relDefs, opinionAB, opinionBA);
                string opinionLabel = RelationsPromptClassifier.GetOpinionLabel(opinionAB);

                // 2. Formato limpio para la lista de miembros del grupo
                if (!string.IsNullOrEmpty(familyTone))
                {
                    string cleanTone = familyTone.Replace("[TONE: ", "").Replace("]", "").TrimEnd('.');
                    result.Add($"{other.LabelShort}: {cleanTone}, opinion is {opinionLabel}");
                }
                else
                {
                    result.Add($"{other.LabelShort}: opinion is {opinionLabel}");
                }
            }
            return result;
        }

        private static List<string> GetKeyBeliefs(List<Precept> precepts)
        {
            var beliefs = new List<string>();
            foreach (var precept in precepts.Take(10))
            {
                if (precept?.def == null) continue;
                string desc = precept.def.description ?? precept.def.label;
                if (string.IsNullOrEmpty(desc)) continue;
                desc = System.Text.RegularExpressions.Regex.Replace(desc, "<.*?>", "");
                desc = desc.Trim();
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
                if (!string.IsNullOrEmpty(desc))
                    beliefs.Add(desc);
            }
            return beliefs.Take(3).ToList();
        }

        private static string GetThreatContext(Pawn speaker)
        {
            var parts = new List<string>();

            string colony = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            if (!colony.Contains("calm and secure"))
                parts.Add(colony.Split('.')[0]);

            string combat = ColonistChatWindow.GetPawnCombatStatusDetailed(speaker);
            if (!combat.Contains("not in combat"))
                parts.Add(combat.Split('.')[0]);

            return parts.Any() ? string.Join("; ", parts) : "";
        }

        private static string BuildBackstory(Pawn pawn)
        {
            if (pawn?.story == null) return string.Empty;

            var entries = new List<string>();

            // 1. Trasfondo de Infancia (Childhood)
            if (pawn.story.Childhood != null)
            {
                var childhood = pawn.story.Childhood;

                // Obtiene el título ajustado al género del colono o su etiqueta por defecto
                string title = childhood.TitleFor(pawn.gender);
                if (string.IsNullOrWhiteSpace(title)) title = childhood.title;
                if (string.IsNullOrWhiteSpace(title)) title = childhood.LabelCap;

                // Prioriza 'description' (campo XML nativo) sobre 'baseDesc'
                string rawDesc = !string.IsNullOrWhiteSpace(childhood.description)
                    ? childhood.description
                    : childhood.baseDesc;

                string desc = FormatBackstoryText(rawDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Childhood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Childhood: {title}");
            }

            // 2. Trasfondo de Adultez (Adulthood)
            if (pawn.story.Adulthood != null)
            {
                var adulthood = pawn.story.Adulthood;

                string title = adulthood.TitleFor(pawn.gender);
                if (string.IsNullOrWhiteSpace(title)) title = adulthood.title;
                if (string.IsNullOrWhiteSpace(title)) title = adulthood.LabelCap;

                string rawDesc = !string.IsNullOrWhiteSpace(adulthood.description)
                    ? adulthood.description
                    : adulthood.baseDesc;

                string desc = FormatBackstoryText(rawDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Adulthood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Adulthood: {title}");
            }

            if (!entries.Any()) return string.Empty;

            return "*Backstory & Origin:*\n  - " + string.Join("\n  - ", entries);
        }

        private static string FormatBackstoryText(string rawText, Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

            try
            {
                // Resuelve sustituciones dinámicas de RimWorld ([PAWN_nameDef], [PAWN_pronoun], etc.)
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

        //*furel - New memory cration prompt - just taking the minimum necesary to a more personified a memory.

        public static class PawnMemoryPromptBuilder
        {
            public static string Build(
            Pawn speaker,
            List<Pawn> group,
            string summary,
            string pawnDialogue,
            string gameLanguage)
            {
                var sb = new StringBuilder();

                // ── SECCIÓN SYSTEM ──────────────────────────────────────────────────
                sb.AppendLine("[SYSTEM]\n");
                sb.AppendLine($"You are {speaker.LabelShort}. Write a brief 1-2 sentence personal memory (max 50 words, first-person 'I') about a group conversation you just had.");

                // Se incluyen TODOS los rasgos del colono sin restringir la cantidad
                var traits = speaker.story?.traits?.allTraits?.Select(t => t.LabelCap);
                if (traits != null && traits.Any())
                {
                    sb.AppendLine($"Personality Traits: {string.Join(", ", traits)}");
                }

                // Estado de ánimo simplificado
                float mood = speaker.needs?.mood?.CurInstantLevel ?? 0.5f;
                string moodLabel = mood >= 0.7f ? "happy" : mood <= 0.35f ? "upset/irritable" : "neutral";
                sb.AppendLine($"Mood: {moodLabel}");

                // Relaciones relevantes con los presentes
                var keyRelations = GetKeyGroupRelations(speaker, group);
                if (keyRelations.Any())
                {
                    sb.AppendLine($"Opinions on participants: {string.Join(", ", keyRelations)}");
                }

                sb.AppendLine("Instruction: Write your internal reflection in a tone that naturally reflects your traits and mood.");
                sb.AppendLine($"CRITICAL RULE: Write strictly in {gameLanguage}.");
                sb.AppendLine();

                // ── SECCIÓN USER ────────────────────────────────────────────────────
                sb.AppendLine("[USER]\n");
                sb.AppendLine($"Group Chat Summary:\n{summary}");

                if (!string.IsNullOrWhiteSpace(pawnDialogue))
                {
                    sb.AppendLine($"\nWhat you said during the conversation:\n{pawnDialogue}");
                }

                return sb.ToString().Trim();
            }

            private static List<string> GetKeyGroupRelations(Pawn speaker, List<Pawn> group)
            {
                var result = new List<string>();
                if (speaker.relations == null) return result;

                foreach (var other in group.Where(p => p != speaker))
                {
                    int opinion = speaker.relations.OpinionOf(other);
                    // Solo incluimos si le cae muy bien o muy mal
                    if (opinion >= 40) result.Add($"likes {other.LabelShort}");
                    else if (opinion <= -30) result.Add($"dislikes {other.LabelShort}");
                }
                return result;
            }
        }
    }
}