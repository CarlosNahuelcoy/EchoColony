using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EchoColony.Factions
{
    /// <summary>
    /// Detects player requests in faction chat and executes real RimWorld consequences.
    ///
    /// Request detection is AI-based — works in any language.
    ///
    /// Supported:
    ///   - Trade caravan      (IncidentDefOf.TradeCaravan)       requires goodwill >= 0
    ///   - Orbital trader     (IncidentDefOf.OrbitalTraderArrival) requires goodwill >= 0
    ///   - Military aid       (IncidentDefOf.RaidFriendly)        requires goodwill >= 20
    ///   - Visitors           (IncidentDefOf.VisitorGroup)        requires goodwill >= 0, not hostile
    ///   - Declare war        (sets faction hostile + immediate raid)
    ///
    /// Cooldown: actions cost more goodwill if they are on cooldown.
    /// Peace: handled automatically in FactionActions when goodwill rises above threshold.
    /// </summary>
    public static class FactionRequestHandler
    {
        // ── Goodwill costs ────────────────────────────────────────────────────────
        private const int COST_CARAVAN_NORMAL    = -8;
        private const int COST_CARAVAN_COOLDOWN  = -18;
        private const int COST_ORBITAL_NORMAL    = -10;
        private const int COST_ORBITAL_COOLDOWN  = -22;
        private const int COST_MILITARY_NORMAL   = -12;
        private const int COST_MILITARY_COOLDOWN = -25;
        private const int COST_VISITORS_NORMAL   = -5;
        private const int COST_VISITORS_COOLDOWN = -12;

        // ── Goodwill gates — minimum required for each action ─────────────────────
        private const int GATE_CARAVAN  = 0;
        private const int GATE_ORBITAL  = 0;
        private const int GATE_MILITARY = 20;
        private const int GATE_VISITORS = 0;

        // ── Request types ─────────────────────────────────────────────────────────
        public enum RequestType { None, TradeCaravan, OrbitalTrader, MilitaryAid, Visitors, DeclareWar }

        // ── Orbital trader sub-type ───────────────────────────────────────────────
        public enum OrbitalTraderType { Random, Bulk, Weapons, Exotic }

        // ═══════════════════════════════════════════════════════════════
        // GATE CHECKS — can this action be done right now?
        // ═══════════════════════════════════════════════════════════════

        public static bool CanRequestCaravan(Faction faction) =>
            faction != null && !faction.HostileTo(Faction.OfPlayer) &&
            faction.PlayerGoodwill >= GATE_CARAVAN;

        public static bool CanRequestOrbital(Faction faction) =>
            faction != null && !faction.HostileTo(Faction.OfPlayer) &&
            faction.PlayerGoodwill >= GATE_ORBITAL;

        public static bool CanRequestMilitary(Faction faction) =>
            faction != null && !faction.HostileTo(Faction.OfPlayer) &&
            faction.PlayerGoodwill >= GATE_MILITARY;

        public static bool CanRequestVisitors(Faction faction) =>
            faction != null && !faction.HostileTo(Faction.OfPlayer) &&
            faction.PlayerGoodwill >= GATE_VISITORS;

        // War can always be declared (even if already hostile — just makes it worse)
        public static bool CanDeclareWar(Faction faction) =>
            faction != null && !faction.def.permanentEnemy;

        // ═══════════════════════════════════════════════════════════════
        // COOLDOWN CHECKS
        // ═══════════════════════════════════════════════════════════════

        public static bool IsCaravanOnCooldown(Faction faction)
        {
            if (faction == null) return false;
            return faction.lastTraderRequestTick > 0 &&
                   (Find.TickManager.TicksGame - faction.lastTraderRequestTick) < 900000;
        }

        public static bool IsOrbitalOnCooldown(Faction faction)
        {
            if (faction == null) return false;
            return faction.lastOrbitalTraderRequestTick > 0 &&
                   (Find.TickManager.TicksGame - faction.lastOrbitalTraderRequestTick) < 900000;
        }

        public static bool IsMilitaryOnCooldown(Faction faction)
        {
            if (faction == null) return false;
            return faction.lastMilitaryAidRequestTick > 0 &&
                   (Find.TickManager.TicksGame - faction.lastMilitaryAidRequestTick) < 900000;
        }

        public static bool IsVisitorsOnCooldown(Faction faction)
        {
            // Visitors use a separate tracker we maintain ourselves
            if (faction == null) return false;
            return FactionChatGameComponent.Instance?.IsVisitorOnCooldown(faction) ?? false;
        }

        private static int DaysLeft(int lastTick, int cooldownTicks)
        {
            int left = cooldownTicks - (Find.TickManager.TicksGame - lastTick);
            return Mathf.CeilToInt(left / 60000f);
        }

        // ═══════════════════════════════════════════════════════════════
        // AI-BASED INTENT DETECTION
        // Works in any language. High threshold — only classifies explicit requests.
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator DetectRequestAsync(
            Faction faction,
            string playerMessage,
            Action<RequestType, OrbitalTraderType> onResult)
        {
            if (string.IsNullOrWhiteSpace(playerMessage))
            {
                onResult?.Invoke(RequestType.None, OrbitalTraderType.Random);
                yield break;
            }

            // Build a list of what's available so the AI doesn't classify
            // impossible requests (e.g. caravan while hostile)
            var available = new List<string>();
            if (CanRequestCaravan(faction))  available.Add("CARAVAN — ground trade caravan");
            if (CanRequestOrbital(faction))  available.Add("ORBITAL — orbital/space trade ship (generic)");
            if (CanRequestOrbital(faction))  available.Add("ORBITAL_BULK — bulk goods orbital trader");
            if (CanRequestOrbital(faction))  available.Add("ORBITAL_WEAPONS — weapons orbital trader");
            if (CanRequestOrbital(faction))  available.Add("ORBITAL_EXOTIC — exotic goods orbital trader");
            if (CanRequestMilitary(faction)) available.Add("MILITARY — military troops or combat support");
            if (CanRequestVisitors(faction)) available.Add("VISITORS — send a friendly visiting group");
            if (CanDeclareWar(faction))      available.Add("WAR — declare war or express extreme hostility");
            available.Add("NONE — not a specific request, just conversation");

            string availableStr = string.Join("\n  ", available);

            string classifyPrompt =
                "You are classifying a player's message in a strategy game.\n" +
                "Only classify as a REQUEST if the player is EXPLICITLY and DIRECTLY asking for something.\n" +
                "If the player is just talking, asking questions, negotiating in general, or making small talk — classify as NONE.\n\n" +
                $"Player message: \"{playerMessage}\"\n\n" +
                $"Available actions right now:\n  {availableStr}\n\n" +
                "Reply with ONLY the code (e.g. CARAVAN, MILITARY, NONE). Nothing else.";

            bool   done   = false;
            string result = "";

            IEnumerator classify = RunAICall(classifyPrompt, r => { result = r; done = true; });
            yield return classify;

            int waited = 0;
            while (!done && waited < 200) { yield return null; waited++; }

            string code = result?.Trim().ToUpperInvariant() ?? "NONE";

            RequestType       reqType = RequestType.None;
            OrbitalTraderType orbType = OrbitalTraderType.Random;

            if      (code.StartsWith("CARAVAN"))         reqType = RequestType.TradeCaravan;
            else if (code.StartsWith("ORBITAL_BULK"))    { reqType = RequestType.OrbitalTrader; orbType = OrbitalTraderType.Bulk; }
            else if (code.StartsWith("ORBITAL_WEAPON"))  { reqType = RequestType.OrbitalTrader; orbType = OrbitalTraderType.Weapons; }
            else if (code.StartsWith("ORBITAL_EXOTIC"))  { reqType = RequestType.OrbitalTrader; orbType = OrbitalTraderType.Exotic; }
            else if (code.StartsWith("ORBITAL"))         { reqType = RequestType.OrbitalTrader; orbType = OrbitalTraderType.Random; }
            else if (code.StartsWith("MILITARY"))        reqType = RequestType.MilitaryAid;
            else if (code.StartsWith("VISITOR"))         reqType = RequestType.Visitors;
            else if (code.StartsWith("WAR"))             reqType = RequestType.DeclareWar;

            Log.Message($"[EchoColony] Request classified: \"{code}\" → {reqType}/{orbType}");
            onResult?.Invoke(reqType, orbType);
        }

        // ═══════════════════════════════════════════════════════════════
        // EVALUATE AND EXECUTE
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator EvaluateAndExecute(
            Faction faction,
            RequestType requestType,
            OrbitalTraderType orbitalType,
            Pawn operatorPawn,
            bool isPlayerMode,
            string leaderResponse,
            string playerMessage,
            Action<string> onResult)
        {
            if (requestType == RequestType.None || faction == null)
            { onResult?.Invoke(null); yield break; }

            // Gate check — double safety even if the classifier already filtered
            bool gateOk = true;
            string gateReason = null;
            switch (requestType)
            {
                case RequestType.TradeCaravan:
                    if (!CanRequestCaravan(faction)) { gateOk = false; gateReason = "not possible with current relations"; }
                    break;
                case RequestType.OrbitalTrader:
                    if (!CanRequestOrbital(faction)) { gateOk = false; gateReason = "not possible with current relations"; }
                    break;
                case RequestType.MilitaryAid:
                    if (!CanRequestMilitary(faction)) { gateOk = false; gateReason = $"requires at least {GATE_MILITARY} goodwill"; }
                    break;
                case RequestType.Visitors:
                    if (!CanRequestVisitors(faction)) { gateOk = false; gateReason = "not possible with current relations"; }
                    break;
            }

            if (!gateOk)
            {
                Log.Message($"[EchoColony] Request {requestType} blocked by gate: {gateReason}");
                onResult?.Invoke(null);
                yield break;
            }

            bool onCooldown = false;
            switch (requestType)
            {
                case RequestType.TradeCaravan:  onCooldown = IsCaravanOnCooldown(faction);  break;
                case RequestType.OrbitalTrader: onCooldown = IsOrbitalOnCooldown(faction);  break;
                case RequestType.MilitaryAid:   onCooldown = IsMilitaryOnCooldown(faction); break;
                case RequestType.Visitors:      onCooldown = IsVisitorsOnCooldown(faction); break;
            }

            // For war, no ACCEPT/REFUSE evaluation needed —
            // if the classifier said WAR, the conversation tone already supports it.
            // We just apply consequences directly.
            if (requestType == RequestType.DeclareWar)
            {
                string warOutcome = ExecuteDeclareWar(faction, operatorPawn, isPlayerMode);
                onResult?.Invoke(warOutcome);
                yield break;
            }

            // All other requests: ask the AI if the leader agreed
            string requestLabel;
            switch (requestType)
            {
                case RequestType.TradeCaravan:  requestLabel = "a trade caravan";    break;
                case RequestType.OrbitalTrader: requestLabel = "an orbital trader";  break;
                case RequestType.MilitaryAid:   requestLabel = "military aid";       break;
                case RequestType.Visitors:      requestLabel = "a visiting group";   break;
                default: requestLabel = "the request"; break;
            }

            string cooldownContext = onCooldown
                ? "Note: this is outside the normal request window — doing this would be a special favor that costs more goodwill."
                : "This is within normal parameters.";

            string evalPrompt =
                $"The player asked for {requestLabel}.\n" +
                $"The leader responded: \"{leaderResponse}\"\n" +
                $"{cooldownContext}\n\n" +
                $"Based ONLY on what the leader actually said, did they AGREE to send {requestLabel}?\n" +
                $"A vague or non-committal response is REFUSE. Only clear agreement is ACCEPT.\n" +
                $"Reply with only: ACCEPT or REFUSE";

            bool   done   = false;
            string result = "";

            yield return RunAICall(evalPrompt, r => { result = r; done = true; });
            int waited = 0;
            while (!done && waited < 200) { yield return null; waited++; }

            bool accepted = result?.Trim().ToUpperInvariant().StartsWith("ACCEPT") == true;
            if (!accepted) { onResult?.Invoke(null); yield break; }

            string outcomeMessage = null;
            switch (requestType)
            {
                case RequestType.TradeCaravan:
                    outcomeMessage = ExecuteTradeCaravan(faction, operatorPawn, isPlayerMode, onCooldown);
                    break;
                case RequestType.OrbitalTrader:
                    outcomeMessage = ExecuteOrbitalTrader(faction, operatorPawn, isPlayerMode, onCooldown, orbitalType);
                    break;
                case RequestType.MilitaryAid:
                    outcomeMessage = ExecuteMilitaryAid(faction, operatorPawn, isPlayerMode, onCooldown);
                    break;
                case RequestType.Visitors:
                    outcomeMessage = ExecuteVisitors(faction, operatorPawn, isPlayerMode, onCooldown);
                    break;
            }

            onResult?.Invoke(outcomeMessage);
        }

        // ═══════════════════════════════════════════════════════════════
        // EXECUTE — TRADE CARAVAN
        // ═══════════════════════════════════════════════════════════════

        private static string ExecuteTradeCaravan(
            Faction faction, Pawn operatorPawn, bool isPlayerMode, bool onCooldown)
        {
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return null;

            int  cost = onCooldown ? COST_CARAVAN_COOLDOWN : COST_CARAVAN_NORMAL;
            ApplyGoodwill(faction, cost, isPlayerMode, operatorPawn, HistoryEventDefOf.RequestedTrader);

            var parms     = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.faction = faction;
            var worker    = IncidentDefOf.TraderCaravanArrival.Worker;

            if (!worker.CanFireNow(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return $"[System] {FactionPromptContextBuilder.GetLeaderName(faction)} agreed but the caravan couldn't depart.";
            }

            if (!worker.TryExecute(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return null;
            }

            faction.lastTraderRequestTick = Find.TickManager.TicksGame;
            return $"[System] {faction.Name} is sending a trade caravan." +
                   (onCooldown ? " (Special favor — goodwill cost was higher.)" : "");
        }

        // ═══════════════════════════════════════════════════════════════
        // EXECUTE — ORBITAL TRADER
        // ═══════════════════════════════════════════════════════════════

        private static string ExecuteOrbitalTrader(
            Faction faction, Pawn operatorPawn, bool isPlayerMode, bool onCooldown, OrbitalTraderType orbType)
        {
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return null;

            int cost = onCooldown ? COST_ORBITAL_COOLDOWN : COST_ORBITAL_NORMAL;
            ApplyGoodwill(faction, cost, isPlayerMode, operatorPawn, HistoryEventDefOf.RequestedOrbitalTrader);

            var parms     = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.faction = faction;

            TraderKindDef traderKind = GetTraderKindDef(orbType);
            if (traderKind != null) parms.traderKind = traderKind;

            var worker = IncidentDefOf.OrbitalTraderArrival.Worker;

            if (!worker.CanFireNow(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return $"[System] {FactionPromptContextBuilder.GetLeaderName(faction)} agreed but no orbital trader is available right now.";
            }

            if (!worker.TryExecute(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return null;
            }

            faction.lastOrbitalTraderRequestTick = Find.TickManager.TicksGame;
            string typeNote = traderKind != null ? $" ({traderKind.label})" : "";
            return $"[System] {faction.Name} is calling in an orbital trader{typeNote}." +
                   (onCooldown ? " (Special favor — goodwill cost was higher.)" : "");
        }

        // ═══════════════════════════════════════════════════════════════
        // EXECUTE — MILITARY AID
        // ═══════════════════════════════════════════════════════════════

        private static string ExecuteMilitaryAid(
            Faction faction, Pawn operatorPawn, bool isPlayerMode, bool onCooldown)
        {
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return null;

            int cost = onCooldown ? COST_MILITARY_COOLDOWN : COST_MILITARY_NORMAL;
            ApplyGoodwill(faction, cost, isPlayerMode, operatorPawn, HistoryEventDefOf.RequestedMilitaryAid);

            var parms     = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.faction = faction;
            var worker    = IncidentDefOf.RaidFriendly.Worker;

            if (!worker.CanFireNow(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return $"[System] {FactionPromptContextBuilder.GetLeaderName(faction)} agreed but their forces couldn't mobilize.";
            }

            if (!worker.TryExecute(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return null;
            }

            faction.lastMilitaryAidRequestTick = Find.TickManager.TicksGame;
            return $"[System] {faction.Name} is sending military support." +
                   (onCooldown ? " (Special favor — goodwill cost was higher.)" : "");
        }

        // ═══════════════════════════════════════════════════════════════
        // EXECUTE — VISITORS
        // ═══════════════════════════════════════════════════════════════

        private static string ExecuteVisitors(
            Faction faction, Pawn operatorPawn, bool isPlayerMode, bool onCooldown)
        {
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return null;

            int cost = onCooldown ? COST_VISITORS_COOLDOWN : COST_VISITORS_NORMAL;
            ApplyGoodwill(faction, cost, isPlayerMode, operatorPawn, HistoryEventDefOf.GaveGift);

            var parms     = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.faction = faction;
            var worker    = IncidentDefOf.VisitorGroup.Worker;

            if (!worker.CanFireNow(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return $"[System] {FactionPromptContextBuilder.GetLeaderName(faction)} agreed to send visitors but they couldn't make the journey right now.";
            }

            if (!worker.TryExecute(parms))
            {
                ApplyGoodwill(faction, -cost, isPlayerMode, operatorPawn, null);
                return null;
            }

            // Record cooldown for visitors
            FactionChatGameComponent.Instance?.RecordVisitorRequest(faction);

            return $"[System] {faction.Name} is sending a group of visitors." +
                   (onCooldown ? " (Special favor — goodwill cost was higher.)" : "");
        }

        // ═══════════════════════════════════════════════════════════════
        // EXECUTE — DECLARE WAR
        // The player's extreme hostility pushes the faction to attack.
        // This fires immediately — no ACCEPT/REFUSE evaluation.
        // ═══════════════════════════════════════════════════════════════

        private static string ExecuteDeclareWar(
            Faction faction, Pawn operatorPawn, bool isPlayerMode)
        {
            // Heavy goodwill penalty first
            ApplyGoodwill(faction, -50, isPlayerMode, operatorPawn, HistoryEventDefOf.AttackedMember);

            string leaderName = FactionPromptContextBuilder.GetLeaderName(faction);

            // Set faction hostile if not already
            if (!faction.HostileTo(Faction.OfPlayer))
            {
                faction.SetRelationDirect(
                    Faction.OfPlayer,
                    FactionRelationKind.Hostile,
                    canSendHostilityLetter: true,
                    reason: $"{leaderName} declared hostilities after a breakdown in negotiations.",
                    lookTarget: null);
            }

            // Schedule immediate raid
            var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map != null)
            {
                var parms     = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                parms.faction = faction;
                parms.forced  = true;

                var worker = IncidentDefOf.RaidEnemy.Worker;
                if (worker.CanFireNow(parms))
                    worker.TryExecute(parms);
            }

            Log.Message($"[EchoColony] War declared with {faction.Name} via faction chat.");
            return $"[System] {leaderName} has cut off communications. {faction.Name} is now hostile.";
        }

        // ═══════════════════════════════════════════════════════════════
        // PEACE CHECK — called from FactionActions after goodwill changes
        // If a hostile faction's goodwill rises above threshold, offer peace.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Call this after any goodwill increase during a faction chat.
        /// If the faction was hostile and goodwill has risen enough,
        /// automatically transitions them back to neutral.
        /// Returns a message to display if peace was made, null otherwise.
        /// </summary>
        public static string TryMakePeace(Faction faction)
        {
            if (faction == null) return null;
            if (!faction.HostileTo(Faction.OfPlayer)) return null;

            // Peace threshold: goodwill must reach 10+
            if (faction.PlayerGoodwill < 10) return null;

            faction.SetRelationDirect(
                Faction.OfPlayer,
                FactionRelationKind.Neutral,
                canSendHostilityLetter: false,
                reason: "Relations improved through negotiation.",
                lookTarget: null);

            Log.Message($"[EchoColony] Peace made with {faction.Name} via faction chat.");
            return $"[System] Relations with {faction.Name} have improved enough to end hostilities. They are no longer at war with you.";
        }

        // ═══════════════════════════════════════════════════════════════
        // COOLDOWN INFO STRING (for UI)
        // ═══════════════════════════════════════════════════════════════

        public static string GetCooldownInfo(Faction faction)
        {
            var parts = new List<string>();
            if (IsCaravanOnCooldown(faction))
                parts.Add($"caravan: {DaysLeft(faction.lastTraderRequestTick, 900000)}d");
            if (IsMilitaryOnCooldown(faction))
                parts.Add($"military: {DaysLeft(faction.lastMilitaryAidRequestTick, 900000)}d");
            if (IsOrbitalOnCooldown(faction))
                parts.Add($"orbital: {DaysLeft(faction.lastOrbitalTraderRequestTick, 900000)}d");
            if (IsVisitorsOnCooldown(faction))
                parts.Add("visitors: cooling down");
            return parts.Any() ? string.Join(" · ", parts) : null;
        }

        // ═══════════════════════════════════════════════════════════════
        // STATUS STRING (for prompt context — tells AI what's possible)
        // ═══════════════════════════════════════════════════════════════

        public static string GetAvailableActionsForPrompt(Faction faction)
        {
            if (faction == null) return "";
            var parts = new List<string>();

            bool hostile = faction.HostileTo(Faction.OfPlayer);

            if (hostile)
            {
                parts.Add("STATUS: Currently at war with the player colony.");
                parts.Add("Possible through negotiation: peace (if goodwill rises above 10).");
                parts.Add("NOT possible while hostile: trade caravan, orbital trader, military aid, visitors.");
            }
            else
            {
                int gw = faction.PlayerGoodwill;
                parts.Add($"Current goodwill: {gw}.");
                if (CanRequestCaravan(faction))  parts.Add("Trade caravan: available.");
                if (CanRequestOrbital(faction))  parts.Add("Orbital trader: available.");
                if (CanRequestMilitary(faction)) parts.Add("Military aid: available (goodwill >= 20).");
                else parts.Add($"Military aid: not available yet (need goodwill >= 20, current: {gw}).");
                if (CanRequestVisitors(faction)) parts.Add("Visitors: available.");
            }

            return string.Join(" ", parts);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void ApplyGoodwill(
            Faction faction, int amount, bool isPlayerMode,
            Pawn operatorPawn, HistoryEventDef reason)
        {
            faction.TryAffectGoodwillWith(
                Faction.OfPlayer,
                amount,
                canSendMessage: amount < 0, // only show message for penalties
                canSendHostilityLetter: amount <= -40,
                reason: reason,
                lookTarget: isPlayerMode ? (GlobalTargetInfo?)null : operatorPawn);
        }

        private static TraderKindDef GetTraderKindDef(OrbitalTraderType orbType)
        {
            if (orbType == OrbitalTraderType.Random) return null;
            string search;
            switch (orbType)
            {
                case OrbitalTraderType.Bulk:    search = "bulk";    break;
                case OrbitalTraderType.Weapons: search = "weapon";  break;
                case OrbitalTraderType.Exotic:  search = "exotic";  break;
                default: return null;
            }
            return DefDatabase<TraderKindDef>.AllDefsListForReading
                .FirstOrDefault(t =>
                    t.defName.ToLowerInvariant().Contains(search) ||
                    t.label?.ToLowerInvariant().Contains(search) == true);
        }

        private static IEnumerator RunAICall(string prompt, Action<string> callback)
        {
            switch (MyMod.Settings?.modelSource ?? ModelSource.Gemini)
            {
                case ModelSource.Player2:
                    yield return GeminiAPI.SendRequestToPlayer2WithPrompt(prompt, callback);
                    break;
                case ModelSource.OpenRouter:
                    yield return GeminiAPI.SendRequestToOpenRouter(prompt, callback);
                    break;
                case ModelSource.Custom:
                    yield return GeminiAPI.SendRequestToCustomProvider(prompt, callback);
                    break;
                case ModelSource.Local:
                    yield return GeminiAPI.SendRequestToLocalModel(prompt, callback);
                    break;
                default:
                    yield return GeminiAPI.SendRequestToGemini(prompt, callback);
                    break;
            }
        }
    }
}