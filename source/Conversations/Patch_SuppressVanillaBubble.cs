using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Patches Bubbler.Add (from the Interaction Bubbles mod) to intercept vanilla
    /// PlayLogEntry_Interaction entries when EchoColony conversations are active.
    ///
    /// Mirrors the approach used by RimTalk's Bubbler_Add patch:
    ///   - Our own PlayLogEntry_Conversations entries pass through normally
    ///   - Vanilla chitchat/deeptalk entries are blocked (we replace them with AI dialogue)
    ///   - Everything else passes through untouched
    ///
    /// Applied manually at startup via TryApply() because Interaction Bubbles may not
    /// be loaded — harmony.PatchAll() can't handle conditionally-present types.
    /// </summary>
    public static class Patch_SuppressVanillaBubble
    {
        private static bool _applied = false;

        // The field name used by RimWorld inside PlayLogEntry_Interaction (confirmed via RimTalk source)
        private const string IntDefFieldName = "intDef";

        public static void TryApply(Harmony harmony)
        {
            if (_applied) return;

            try
            {
                Type bubblerType = AccessTools.TypeByName("Bubbles.Core.Bubbler");
                if (bubblerType == null) return;  // Interaction Bubbles not loaded

                MethodInfo addMethod = AccessTools.Method(bubblerType, "Add");
                if (addMethod == null) return;

                var prefix = new HarmonyMethod(
                    AccessTools.Method(typeof(Patch_SuppressVanillaBubble), nameof(Prefix)));

                harmony.Patch(addMethod, prefix: prefix);
                _applied = true;

                Log.Message("[EchoColony] Conversations: Bubbler.Add patch applied — vanilla bubbles will be replaced by AI dialogue.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Could not patch Bubbler.Add: {ex.Message}");
            }
        }

        // ── Prefix ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns false (suppresses the vanilla bubble) when:
        ///   1. EchoColony pawn conversations are enabled
        ///   2. The entry is a vanilla PlayLogEntry_Interaction (not our subclass)
        ///   3. The interaction is chitchat or deeptalk (the only types we replace)
        ///
        /// All other entries (combat, our own bubbles, etc.) pass through unchanged.
        /// </summary>
        public static bool Prefix(LogEntry entry)
        {
            try
            {
                // Always let our own AI dialogue bubbles through
                if (entry is PlayLogEntry_Conversations) return true;

                // If feature is off, let Interaction Bubbles work normally
                if (MyMod.Settings == null || !MyMod.Settings.enablePawnConversations)
                    return true;

                // Only intercept vanilla social interaction entries
                if (!(entry is PlayLogEntry_Interaction)) return true;

                // Get the InteractionDef via reflection — same field RimTalk uses ("intDef")
                var intDefField = AccessTools.Field(entry.GetType(), IntDefFieldName);
                var interactionDef = intDefField?.GetValue(entry) as InteractionDef;
                if (interactionDef == null) return true;

                // Only replace social conversation interactions (chitchat, deeptalk, and any
                // interaction that triggers our system). Combat/insult/romance pass through.
                bool isSocialConversation =
                    interactionDef == InteractionDefOf.Chitchat  ||
                    interactionDef == InteractionDefOf.DeepTalk  ||
                    IsConversationTriggerInteraction(interactionDef);

                if (!isSocialConversation) return true;

                // Block vanilla bubble — EchoColony will show AI dialogue instead
                return false;
            }
            catch
            {
                return true; // On any error, let the vanilla bubble through
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true for any interaction type that PawnConversationListener handles.
        /// Extend this list if you add more interaction triggers.
        /// </summary>
        private static bool IsConversationTriggerInteraction(InteractionDef def)
        {
            if (def == null) return false;
            string n = def.defName?.ToLower() ?? "";

            return n.Contains("chitchat")  ||
                   n.Contains("deeptalk")  ||
                   n.Contains("kind")      ||
                   n.Contains("compliment");
        }
    }
}