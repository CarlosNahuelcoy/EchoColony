using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Sends text bubbles to the Interaction Bubbles mod without touching the PlayLog.
    /// Interaction Bubbles is a hard soft-dependency: the feature simply won't show
    /// visual bubbles if the mod is absent, but nothing breaks.
    /// </summary>
    public static class BubbleController
    {
        private static Type   bubblerType = null;
        private static MethodInfo addMethod = null;
        private static bool   initialized  = false;

        // ── Initialization ────────────────────────────────────────────────────────

        private static bool Initialize()
        {
            if (initialized) return bubblerType != null;
            initialized = true;

            try
            {
                bubblerType = AccessTools.TypeByName("Bubbles.Core.Bubbler");
                if (bubblerType == null)
                {
                    Log.Warning("[EchoColony] Conversations: Interaction Bubbles not found — visual bubbles disabled.");
                    return false;
                }

                addMethod = AccessTools.Method(bubblerType, "Add");
                if (addMethod == null)
                {
                    Log.Error("[EchoColony] Conversations: Bubbler.Add method not found.");
                    return false;
                }

                Log.Message("[EchoColony] Conversations: Connected to Interaction Bubbles.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Conversations: Error initializing Bubbles: {ex.Message}");
                return false;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public static bool IsAvailable()
        {
            if (!initialized) Initialize();
            return bubblerType != null && addMethod != null;
        }

        /// <summary>
        /// Shows a speech bubble above <paramref name="pawn"/> with the given text.
        /// Does NOT add anything to the RimWorld PlayLog.
        /// </summary>
        public static void ShowBubble(Pawn pawn, string text)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(text)) return;
            if (!Initialize()) return;

            try
            {
                var entry = new PlayLogEntry_Conversations(pawn, text);
                addMethod.Invoke(null, new object[] { entry });
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Conversations: Error showing bubble for {pawn.LabelShort}: {ex.Message}");
            }
        }
    }
}
