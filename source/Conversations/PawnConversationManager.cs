using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using SimpleJSON;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Orchestrates AI-generated pawn conversations:
    ///   1. Validates the pawn pair (cooldown, settings, distance).
    ///   2. Builds the prompt via PawnConversationPromptBuilder.
    ///   3. Calls the AI via GeminiAPI (same backend as colonist chat).
    ///   4. Parses the JSON response into lines.
    ///   5. Hands lines to BubbleSequencer for timed display.
    ///   6. Writes a memory note back to ColonistMemoryManager.
    /// </summary>
    public static class PawnConversationManager
    {
        // Max distance for two pawns to "converse" (tiles)
        private const float MaxConversationDistance = 20f;

        // Pairs currently being processed — prevents double-fire when cooldown = 0
        // Key: sorted "ThingID|ThingID" same as CooldownTracker
        private static readonly HashSet<string> _inProgress = new HashSet<string>();

        private static string PairKey(Pawn a, Pawn b)
        {
            string idA = a.ThingID, idB = b.ThingID;
            return string.CompareOrdinal(idA, idB) <= 0 ? idA + "|" + idB : idB + "|" + idA;
        }

        // ── Entry point ───────────────────────────────────────────────────────────

        public static void TryStartConversation(Pawn initiator, Pawn recipient, InteractionDef interactionDef)
        {
            // ── Guards ────────────────────────────────────────────────────────────
            if (!IsConversationEnabled()) return;
            if (initiator == null || recipient == null) return;
            if (!initiator.RaceProps.Humanlike) return;          // Only humanlike initiators for now
            if (initiator.Dead || recipient.Dead) return;
            if (!initiator.Spawned || !recipient.Spawned) return;
            if (initiator.Map != recipient.Map) return;
            if (initiator.Position.DistanceTo(recipient.Position) > MaxConversationDistance) return;
            if (!ConversationCooldownTracker.CanConverse(initiator, recipient)) return;

            // Per-pawn eligibility (guests, prisoners, slaves filters)
            var settings = MyMod.Settings;
            if (!settings.IsPawnEligibleForConversation(initiator)) return;
            if (!settings.IsPawnEligibleForConversation(recipient)) return;

            // ── Guard: skip if this pair already has a conversation in flight ─────
            string pairKey = PairKey(initiator, recipient);
            if (!MyMod.Settings.conversationAllowSimultaneous && _inProgress.Contains(pairKey)) return;
            _inProgress.Add(pairKey);

            // ── Record cooldown immediately (prevents double-firing) ──────────────
            ConversationCooldownTracker.RecordConversation(initiator, recipient);

            // ── Run async via MyStoryModComponent (same as every other coroutine in the mod) ──
            MyStoryModComponent.Instance?.StartCoroutine(GenerateConversationCoroutine(initiator, recipient, interactionDef));
        }

        // ── Coroutine ─────────────────────────────────────────────────────────────

        private static IEnumerator GenerateConversationCoroutine(
            Pawn initiator, Pawn recipient, InteractionDef interactionDef)
        {
            string pairKey = PairKey(initiator, recipient);
            try
            {
            int linesPerPawn = GetLinesPerPawn();

            // Build prompt
            string prompt = PawnConversationPromptBuilder.Build(initiator, recipient, interactionDef, linesPerPawn);
            if (string.IsNullOrWhiteSpace(prompt)) { _inProgress.Remove(pairKey); yield break; }

            // Call AI — dispatch to the active backend (mirrors ColonistChatWindow pattern)
            string aiResponse = null;
            yield return SendConversationRequest(prompt, r => aiResponse = r);

            if (string.IsNullOrWhiteSpace(aiResponse) || aiResponse.StartsWith("⚠") || aiResponse.StartsWith("❌"))
            {
                Log.Warning($"[EchoColony] Conversation AI error for {initiator.LabelShort}↔{recipient.LabelShort}: {aiResponse}");
                yield break;
            }

            // Parse JSON lines
            var lines = ParseConversationLines(aiResponse, initiator, recipient);
            if (lines == null || lines.Count == 0)
            {
                Log.Warning($"[EchoColony] Conversation: could not parse AI response.");
                yield break;
            }

            // Deliver to BubbleSequencer
            var sequencer = initiator.Map?.GetComponent<BubbleSequencerComponent>()?.Sequencer;
            if (sequencer == null)
            {
                Log.Warning("[EchoColony] Conversation: BubbleSequencerComponent not found on map.");
                yield break;
            }

            float delay = GetBubbleDelay();
            sequencer.Enqueue(lines, delay);

            // Feed into chat log overlay (separator first, then lines)
            ConversationChatLogFeeder.PushSeparator();
            foreach (var (speaker, text) in lines)
                ConversationChatLogFeeder.PushLine(speaker, text);

            // Write memory note back into EchoColony's memory system
            WriteConversationMemory(initiator, recipient, lines);
            } // end try
            finally { _inProgress.Remove(pairKey); }
        }

        // ── Response parsing ──────────────────────────────────────────────────────

        /// <summary>
        /// Parses the AI JSON response into (speaker, text) pairs.
        /// Handles both clean JSON and JSON embedded in prose.
        /// </summary>
        private static List<(Pawn speaker, string text)> ParseConversationLines(
            string response, Pawn initiator, Pawn recipient)
        {
            try
            {
                // Strip possible markdown fences
                string clean = response.Trim();
                if (clean.StartsWith("```")) 
                {
                    int first = clean.IndexOf('\n');
                    int last  = clean.LastIndexOf("```");
                    if (first >= 0 && last > first)
                        clean = clean.Substring(first + 1, last - first - 1).Trim();
                }

                // Find JSON array
                int arrayStart = clean.IndexOf('[');
                int arrayEnd   = clean.LastIndexOf(']');
                if (arrayStart < 0 || arrayEnd <= arrayStart) return null;
                clean = clean.Substring(arrayStart, arrayEnd - arrayStart + 1);

                var parsed = JSON.Parse(clean);
                if (parsed == null || parsed.AsArray == null) return null;

                var lines = new List<(Pawn, string)>();
                foreach (JSONNode node in parsed.AsArray)
                {
                    string speakerName = node["speaker"]?.Value?.Trim() ?? "";
                    string text        = node["text"]?.Value?.Trim()    ?? "";
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // Match speaker name to pawn (case-insensitive partial match)
                    Pawn speaker = MatchSpeaker(speakerName, initiator, recipient);
                    if (speaker == null) continue;

                    lines.Add((speaker, text));
                }

                return lines.Count > 0 ? lines : null;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Conversation parse error: {ex.Message}");
                return null;
            }
        }

        private static Pawn MatchSpeaker(string name, Pawn a, Pawn b)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string lower = name.ToLowerInvariant();

            if (a.LabelShort.ToLowerInvariant().Contains(lower) ||
                lower.Contains(a.LabelShort.ToLowerInvariant()))
                return a;

            if (b.LabelShort.ToLowerInvariant().Contains(lower) ||
                lower.Contains(b.LabelShort.ToLowerInvariant()))
                return b;

            // Fallback: alternate
            return null;
        }

        // ── Memory write-back ─────────────────────────────────────────────────────

        /// <summary>
        /// Saves a one-line summary of this conversation into each pawn's memory,
        /// so future player chats and storyteller entries know it happened.
        /// </summary>
        private static void WriteConversationMemory(
            Pawn initiator, Pawn recipient, List<(Pawn speaker, string text)> lines)
        {
            try
            {
                if (lines == null || lines.Count == 0) return;
                if (!ColonistMemoryManager.IsMemorySystemEnabled) return;

                var manager = ColonistMemoryManager.GetOrCreate();
                if (manager == null) return;

                string firstLine = lines.First().text;
                string lastLine  = lines.Count > 1 ? lines.Last().text : null;

                string note = $"[Spoke with {recipient.LabelShort}] \"{firstLine}\"" +
                              (lastLine != null ? $" ... \"{lastLine}\"" : "");

                string noteForRecipient = $"[Spoke with {initiator.LabelShort}] \"{firstLine}\"" +
                                          (lastLine != null ? $" ... \"{lastLine}\"" : "");

                int today = GenDate.DaysPassed;
                manager.GetTrackerFor(initiator)?.SaveMemoryForDay(today, note);
                manager.GetTrackerFor(recipient)?.SaveMemoryForDay(today, noteForRecipient);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Conversation memory write error: {ex.Message}");
            }
        }

        // ── Backend dispatch ─────────────────────────────────────────────────────

        /// <summary>
        /// Routes the conversation prompt to the active AI backend.
        /// Always uses the "pre-built prompt" path — the full context is already
        /// in the prompt, no pawn-specific Player2 session needed.
        /// Mirrors the dispatch logic in ColonistChatWindow.SendMessage().
        /// </summary>
        private static IEnumerator SendConversationRequest(string prompt, Action<string> onResponse)
        {
            if (MyMod.Settings == null)
            {
                onResponse?.Invoke("⚠ ERROR: Settings not loaded");
                yield break;
            }

            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Player2:
                    yield return GeminiAPI.SendRequestToPlayer2WithPrompt(prompt, onResponse);
                    break;

                case ModelSource.Local:
                    yield return GeminiAPI.SendRequestToLocalModel(prompt, onResponse);
                    break;

                case ModelSource.OpenRouter:
                    yield return GeminiAPI.SendRequestToOpenRouter(prompt, onResponse);
                    break;

                case ModelSource.Gemini:
                default:
                    yield return GeminiAPI.SendRequestToGemini(prompt, onResponse);
                    break;
            }
        }

        // ── Settings helpers ──────────────────────────────────────────────────────

        private static bool IsConversationEnabled()
        {
            // ArePawnConversationsActive checks master switch + colony size + speed limit
            return MyMod.Settings?.ArePawnConversationsActive() ?? false;
        }

        private static int GetLinesPerPawn()
        {
            return System.Math.Max(1, System.Math.Min(3, MyMod.Settings?.conversationLinesPerPawn ?? 3));
        }

        private static float GetBubbleDelay()
        {
            return MyMod.Settings?.conversationBubbleDelay ?? 1.5f;
        }
    }
}