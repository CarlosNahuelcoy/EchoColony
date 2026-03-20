using HarmonyLib;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Postfix patch on Pawn_InteractionsTracker.TryInteractWith.
    /// 
    /// Every time two pawns interact in RimWorld, we check whether EchoColony
    /// should generate an AI conversation and show it as speech bubbles.
    /// The original interaction still proceeds normally — we only add bubbles on top.
    /// 
    /// Fires AFTER the interaction so the log entry already exists and
    /// the interaction type is confirmed valid.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class PawnConversationListener
    {
        [HarmonyPostfix]
        public static void Postfix(
            Pawn_InteractionsTracker __instance,
            Pawn recipient,
            InteractionDef intDef,
            bool __result)          // true = interaction actually happened
        {
            // Only act when the interaction was confirmed
            if (!__result) return;

            // Safety: needs a game and a valid map
            if (Current.Game == null || Find.CurrentMap == null) return;
            if (Faction.OfPlayer == null) return;

            // Get the initiator pawn from the tracker
            Pawn initiator = GetPawnFromTracker(__instance);
            if (initiator == null) return;

            // Only trigger for player colony members
            if (!IsPlayerColonyMember(initiator) && !IsPlayerColonyMember(recipient)) return;

            // Early-exit: respect speed limit and master switch HERE in the patch,
            // so Dubs Analyzer doesn't see us burning cycles inside the manager.
            if (MyMod.Settings?.ArePawnConversationsActive() != true) return;

            // Hand off — PawnConversationManager handles all further guards
            PawnConversationManager.TryStartConversation(initiator, recipient, intDef);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Pawn GetPawnFromTracker(Pawn_InteractionsTracker tracker)
        {
            // Pawn_InteractionsTracker stores its pawn in a private field "pawn"
            try
            {
                var field = AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn");
                return field?.GetValue(tracker) as Pawn;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPlayerColonyMember(Pawn pawn)
        {
            if (pawn == null) return false;
            return pawn.Faction == Faction.OfPlayer ||
                   pawn.IsPrisonerOfColony          ||
                   pawn.IsSlaveOfColony;
        }
    }
}