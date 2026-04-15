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

            // 5. Recent memories from individual and group chats
            AppendMemories(sb, speaker);

            // 6. NEW: Verified colony history — only source of past events
            AppendVerifiedTales(sb, speaker, group);

            // 7. The actual conversation happening right now
            AppendConversationHistory(sb, recentHistory, userMessage, isFirstTurn, speaker);

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

            var childhood = speaker.story?.AllBackstories?
                .FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = speaker.story?.AllBackstories?
                .FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);
            if (childhood != null || adulthood != null)
                sb.AppendLine($"Background: {childhood?.title ?? "unknown"} / {adulthood?.title ?? "unknown"}");

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

        private static void AppendMemories(StringBuilder sb, Pawn speaker)
        {
            var manager = ColonistMemoryManager.GetOrCreate();
            if (manager == null) return;

            var memories = manager.GetTrackerFor(speaker)?.GetLastMemories(3);
            if (memories?.Any() != true) return;

            sb.AppendLine("Recent memories:");
            foreach (var mem in memories)
            {
                string prefix = mem.StartsWith("[Conversación grupal") ? "(group)" : "(private)";
                string short_ = mem.Length > 100 ? mem.Substring(0, 100) + "..." : mem;
                sb.AppendLine($"  {prefix} {short_}");
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
            string        userMessage,
            bool          isFirstTurn,
            Pawn          speaker)
        {
            sb.AppendLine("# Group conversation so far:");

            var dialogueLines = (recentHistory ?? new List<string>())
                .Where(l => !GroupChatSession.IsSystemMessage(l))
                .Select(GroupChatSession.GetDisplayText)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(8)
                .ToList();

            if (dialogueLines.Any())
            {
                foreach (var line in dialogueLines)
                    sb.AppendLine($"  {line}");
            }
            else
            {
                sb.AppendLine("  (no messages yet)");
            }

            var lastSystemEvent = (recentHistory ?? new List<string>())
                .LastOrDefault(l => l.StartsWith(GroupChatSession.SystemPrefix));

            if (!string.IsNullOrEmpty(lastSystemEvent))
            {
                string eventText = GroupChatSession.GetDisplayText(lastSystemEvent);
                if (eventText.Contains("stepped away") ||
                    eventText.Contains("joins")        ||
                    eventText.Contains("left"))
                {
                    sb.AppendLine($"\n(Recent event: {eventText})");
                }
            }

            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                string label = isFirstTurn
                    ? "Player started the conversation with"
                    : "Player said";
                sb.AppendLine($"\n{label}: \"{userMessage}\"");
            }

            sb.AppendLine();
        }

        // ── 8. Response instruction ──────────────────────────────────────────────

        private static void AppendResponseInstruction(
            StringBuilder sb,
            Pawn          speaker,
            List<Pawn>    group,
            bool          isFirstTurn,
            bool          isLateJoiner)
        {
            string lang = Prefs.LangFolderName?.ToLower() ?? "english";

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

            if (lang != "english")
                sb.AppendLine($"Respond in {lang}.");

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
            foreach (var other in group.Where(p => p != speaker).Take(6))
            {
                int    opinion  = speaker.relations?.OpinionOf(other) ?? 0;
                string relation = speaker.GetRelations(other).FirstOrDefault()?.label;
                string desc     = relation ?? (
                    opinion >= 60  ? "very close" :
                    opinion >= 30  ? "friendly"   :
                    opinion <= -60 ? "despises"   :
                    opinion <= -30 ? "dislikes"   :
                    opinion > 10   ? "likes"       :
                    null);

                if (desc != null)
                    result.Add($"{other.LabelShort}: {desc}");
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
    }
}