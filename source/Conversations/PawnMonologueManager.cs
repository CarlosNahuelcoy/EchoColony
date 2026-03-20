using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Orchestrates single-pawn AI monologues — spontaneous lines that a pawn
    /// mutters to themselves, triggered either by a significant thought or by
    /// the periodic tick component.
    ///
    /// Intentionally lean: one line, one API call, no sequencer needed.
    /// </summary>
    public static class PawnMonologueManager
    {
        // Pawns currently waiting for their API response — prevents duplicate calls
        private static readonly HashSet<string> _inProgress = new HashSet<string>();

        // Last monologue tick per pawn (ThingID → tick). Non-persistent: resets on load.
        private static readonly Dictionary<string, int> _lastMonologueTick = new Dictionary<string, int>();

        // For the hourly random tick, driven by MyStoryModComponent.Update()
        private const int MonologueCheckInterval = 2500; // 1 game hour
        private static int _nextMonologueCheckTick = 0;

        // ── Tick (called from MyStoryModComponent.Update) ────────────────────────

        /// <summary>
        /// Called every Unity Update from MyStoryModComponent.
        /// Fires the hourly random monologue check — no XML registration needed.
        /// </summary>
        public static void Tick()
        {
            if (Current.Game == null || Find.TickManager == null) return;

            int now = Find.TickManager.TicksGame;
            if (now < _nextMonologueCheckTick) return;
            _nextMonologueCheckTick = now + MonologueCheckInterval;

            var settings = MyMod.Settings;
            if (settings == null || !settings.enableMonologues) return;
            if (Faction.OfPlayer == null) return;

            // Roll each eligible pawn independently
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!Rand.Chance(settings.monologueChancePerHour)) continue;
                    TryStartMonologue(pawn, null);
                }
            }
        }

        // ── Public entry point ────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to start a monologue for the given pawn.
        /// Silently returns if any guard condition fails.
        /// </summary>
        /// <param name="pawn">The speaking pawn.</param>
        /// <param name="triggerContext">Optional: what prompted the monologue
        /// (e.g. "received bad memory: Witnessed death"). Null for random ticks.</param>
        public static void TryStartMonologue(Pawn pawn, string triggerContext = null)
        {
            if (!IsMonologueAllowed(pawn)) return;

            string id = pawn.ThingID;

            // In-progress guard
            if (_inProgress.Contains(id)) return;

            // Cooldown check
            int now = Find.TickManager.TicksGame;
            int cooldownTicks = MyMod.Settings.monologueCooldownHours * 2500;
            if (_lastMonologueTick.TryGetValue(id, out int lastTick) &&
                now - lastTick < cooldownTicks) return;

            // Register immediately to prevent race conditions
            _inProgress.Add(id);
            _lastMonologueTick[id] = now;

            MyStoryModComponent.Instance?.StartCoroutine(
                GenerateMonologueCoroutine(pawn, triggerContext));
        }

        // ── Guards ────────────────────────────────────────────────────────────────

        private static bool IsMonologueAllowed(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned) return false;

            var settings = MyMod.Settings;
            if (settings == null || !settings.enableMonologues) return false;

            // Respect speed limit (shared setting with conversations)
            if (settings.conversationDisableAtSpeed > 0)
            {
                int speed = (int)Find.TickManager.CurTimeSpeed;
                if (speed >= settings.conversationDisableAtSpeed) return false;
            }

            // Pawn must be able to speak and be conscious
            if (pawn.Dead || pawn.Downed) return false;
            if (!pawn.health.capacities.CanBeAwake) return false;

            float talking = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking);
            if (talking < 0.1f) return false;

            // Must be a colony pawn (respects same filters as conversations)
            if (!settings.IsPawnEligibleForConversation(pawn)) return false;

            // Bubble support check
            if (!BubbleController.IsAvailable()) return false;

            return true;
        }

        // ── Coroutine ─────────────────────────────────────────────────────────────

        private static IEnumerator GenerateMonologueCoroutine(Pawn pawn, string triggerContext)
        {
            string id = pawn.ThingID;
            try
            {
                string prompt = PawnMonologuePromptBuilder.Build(pawn, triggerContext);
                if (string.IsNullOrWhiteSpace(prompt)) yield break;

                string aiResponse = null;
                yield return SendMonologueRequest(prompt, r => aiResponse = r);

                if (string.IsNullOrWhiteSpace(aiResponse) ||
                    aiResponse.StartsWith("⚠") || aiResponse.StartsWith("❌"))
                {
                    Log.Warning($"[EchoColony] Monologue error for {pawn.LabelShort}: {aiResponse}");
                    yield break;
                }

                // Clean up the response — strip quotes if the AI wrapped it
                string line = aiResponse.Trim().Trim('"').Trim('\'').Trim();
                if (string.IsNullOrWhiteSpace(line)) yield break;

                // Show bubble — single pawn, no sequencer
                BubbleController.ShowBubble(pawn, line);
                ConversationChatLogFeeder.PushMonologue(pawn, line);

                Log.Message($"[EchoColony] Monologue [{pawn.LabelShort}]: {line}");
            }
            finally
            {
                _inProgress.Remove(id);
            }
        }

        // ── API dispatch ──────────────────────────────────────────────────────────
        // Mirrors PawnConversationManager.SendConversationRequest

        private static IEnumerator SendMonologueRequest(string prompt, Action<string> onResponse)
        {
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

        // ── Cleanup ───────────────────────────────────────────────────────────────

        /// <summary>Called by GameComponent on game load to clear stale state.</summary>
        public static void OnGameLoaded()
        {
            _inProgress.Clear();
            _lastMonologueTick.Clear();

            // Restore chat log overlay position from settings
            var s = MyMod.Settings;
            if (s != null)
                Conversations.ConversationChatLogRenderer.LoadPosition(s.chatLogX, s.chatLogY, s.chatLogW, s.chatLogH);
        }
    }
}