using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Minimal PlayLogEntry that carries a custom text string.
    /// Passed directly to Bubbler.Add() — never inserted into Find.PlayLog.
    /// </summary>
    public class PlayLogEntry_Conversations : PlayLogEntry_Interaction
    {
        private string displayText;

        // Required by RimWorld serialisation
        public PlayLogEntry_Conversations() { }

        public PlayLogEntry_Conversations(Pawn pawn, string text)
            : base(GetOrCreateInteractionDef(), pawn, null, null)
        {
            displayText = text;
        }

        public override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
            => displayText ?? "";

        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
            => displayText ?? "";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref displayText, "displayText");
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private static InteractionDef GetOrCreateInteractionDef()
        {
            var def = DefDatabase<InteractionDef>.GetNamedSilentFail("EchoColony_ConversationBubble");
            if (def == null)
            {
                def = new InteractionDef
                {
                    defName = "EchoColony_ConversationBubble",
                    label   = "talking"
                };
            }
            return def;
        }
    }
}
