using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimWorld.Planet;
using UnityEngine;

namespace EchoColony.Factions
{
    /// <summary>
    /// Handles all real in-game consequences of faction conversations.
    ///
    /// Goodwill changes use RimWorld's native TryAffectGoodwillWith so the
    /// standard message ("Relations with X: +5") appears exactly as in vanilla.
    ///
    /// All changes go through a cooldown system to prevent abuse.
    /// Cooldown is global across all factions (one active cooldown at a time).
    /// </summary>
    public static class FactionActions
    {
        // ── Cooldown tracking ─────────────────────────────────────────────────────
        // Stored in ticks. One global cooldown shared across all factions.
        private static int _lastGoodwillChangeTick = -1;

        // Minimum ticks between goodwill changes from conversation.
        // Default: 60000 = 1 in-game day. Configurable via settings.
        private static int CooldownTicks =>
            (int)(MyMod.Settings?.factionChatGoodwillCooldownHours ?? 24f) * 2500;

        public static bool IsOnCooldown =>
            _lastGoodwillChangeTick > 0 &&
            (Find.TickManager.TicksGame - _lastGoodwillChangeTick) < CooldownTicks;

        public static int TicksUntilCooldownExpires =>
            IsOnCooldown
                ? CooldownTicks - (Find.TickManager.TicksGame - _lastGoodwillChangeTick)
                : 0;

        public static string CooldownDescription
        {
            get
            {
                int ticks = TicksUntilCooldownExpires;
                if (ticks <= 0) return null;
                int hours = ticks / 2500;
                if (hours < 1)    return "less than an hour";
                if (hours < 24)   return $"{hours}h";
                return $"{hours / 24}d {hours % 24}h";
            }
        }

        // ── Goodwill thresholds ───────────────────────────────────────────────────
        // How much a conversation needs to accumulate before it triggers a change.
        // This prevents a single "hello" from having any effect.
        private const int MIN_EXCHANGES_FOR_EFFECT = 4;
        private const int GOODWILL_POSITIVE_SMALL  =  3;
        private const int GOODWILL_POSITIVE_MEDIUM =  7;
        private const int GOODWILL_POSITIVE_LARGE  = 15;
        private const int GOODWILL_NEGATIVE_SMALL  = -3;
        private const int GOODWILL_NEGATIVE_MEDIUM = -8;
        private const int GOODWILL_NEGATIVE_LARGE  = -20;
        private const int GOODWILL_HOSTILE_ATTACK  = -40; // triggers future raid

        // ═══════════════════════════════════════════════════════════════
        // MAIN EVALUATION
        // Called by FactionChatWindow after each AI response.
        // Analyzes the conversation tone and decides what (if anything) happens.
        // ═══════════════════════════════════════════════════════════════

        public static FactionActionResult EvaluateConversation(
            Faction faction,
            List<string> conversationHistory,
            Pawn operatorPawn,
            bool isPlayerMode)
        {
            if (faction == null || conversationHistory == null)
                return FactionActionResult.None;

            // Need minimum exchanges before anything can happen
            int playerLines = conversationHistory.Count(l =>
                l.StartsWith("[USER]") || l.StartsWith("You::"));
            if (playerLines < MIN_EXCHANGES_FOR_EFFECT)
                return FactionActionResult.None;

            if (IsOnCooldown)
                return FactionActionResult.OnCooldown;

            // Social skill modifier — only applies in colonist mode
            int socialBonus = 0;
            if (!isPlayerMode && operatorPawn != null)
            {
                int socialLevel = operatorPawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                // Social 20 = +3 bonus, Social 0 = -2 penalty
                socialBonus = Mathf.RoundToInt((socialLevel - 10) * 0.25f);
            }

            return FactionActionResult.None;
        }

        // ═══════════════════════════════════════════════════════════════
        // GOODWILL CHANGES
        // ═══════════════════════════════════════════════════════════════

        /// Apply a goodwill change using RimWorld's native system.
        /// Shows the standard vanilla message: "Relations with X: +5"
        public static bool TryApplyGoodwillChange(
            Faction faction,
            int amount,
            Pawn operatorPawn,
            string reason = null)
        {
            if (faction == null) return false;
            if (IsOnCooldown) return false;

            // Social skill modifier in colonist mode
            if (operatorPawn != null)
            {
                int socialLevel = operatorPawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                float modifier  = 1f + ((socialLevel - 10) * 0.05f); // ±50% at extremes
                modifier        = Math.Max(0.3f, Math.Min(2f, modifier));
                amount          = Mathf.RoundToInt(amount * modifier);
                if (amount == 0) amount = Math.Sign(amount) * 1; // minimum ±1
            }

            // Cap changes to prevent instant alliance/war
            amount = Math.Max(-25, Math.Min(20, amount));

            // Use RimWorld's native goodwill system — shows the standard message
            faction.TryAffectGoodwillWith(
                Faction.OfPlayer,
                amount,
                canSendMessage: true,
                canSendHostilityLetter: amount < -15,
                reason: HistoryEventDefOf.GaveGift,
                lookTarget: operatorPawn
            );

            _lastGoodwillChangeTick = Find.TickManager.TicksGame;

            Log.Message($"[EchoColony] Faction goodwill change: {faction.Name} {(amount >= 0 ? "+" : "")}{amount} " +
                        $"(operator: {operatorPawn?.LabelShort ?? "player"}, reason: {reason ?? "conversation"})");

            return true;
        }

        /// Simplified version that just takes a sentiment level.
        /// sentiment: -3 (very hostile) to +3 (very friendly)
        public static bool TryApplyGoodwillFromSentiment(
            Faction faction,
            int sentiment,
            Pawn operatorPawn)
        {
            int amount;
            if      (sentiment ==  3) amount = GOODWILL_POSITIVE_LARGE;
            else if (sentiment ==  2) amount = GOODWILL_POSITIVE_MEDIUM;
            else if (sentiment ==  1) amount = GOODWILL_POSITIVE_SMALL;
            else if (sentiment == -1) amount = GOODWILL_NEGATIVE_SMALL;
            else if (sentiment == -2) amount = GOODWILL_NEGATIVE_MEDIUM;
            else if (sentiment == -3) amount = GOODWILL_NEGATIVE_LARGE;
            else                      amount = 0;

            if (amount == 0) return false;
            return TryApplyGoodwillChange(faction, amount, operatorPawn, "EchoColony conversation");
        }

        // ═══════════════════════════════════════════════════════════════
        // FUTURE RAID THREAT
        // Extreme hostility doesn't trigger immediate attack —
        // it schedules a future raid (more realistic and less punishing).
        // ═══════════════════════════════════════════════════════════════

        public static void ScheduleFutureRaid(Faction faction, Pawn operatorPawn)
        {
            if (faction == null) return;
            if (faction.HostileTo(Faction.OfPlayer)) return; // already hostile

            // First apply a heavy goodwill penalty
            TryApplyGoodwillChange(faction, GOODWILL_HOSTILE_ATTACK, operatorPawn,
                "Extreme hostility during comms");

            // Queue a raid via the storyteller ~1-3 days from now
            int delayTicks = Rand.Range(60000, 180000); // 1-3 days
            int raidTick   = Find.TickManager.TicksGame + delayTicks;

            FactionRaidScheduler.ScheduleRaid(faction, raidTick);

            string leaderName = FactionPromptContextBuilder.GetLeaderName(faction);
            Messages.Message(
                $"[EchoColony] {leaderName} of {faction.Name} ended the transmission abruptly. Something tells you this isn't over.",
                MessageTypeDefOf.ThreatBig,
                false);

            Log.Message($"[EchoColony] Future raid scheduled from {faction.Name} in ~{delayTicks / 60000} days due to extreme hostility.");
        }

        // ═══════════════════════════════════════════════════════════════
        // SAVE / LOAD
        // ═══════════════════════════════════════════════════════════════

        public static void ExposeData()
        {
            Scribe_Values.Look(ref _lastGoodwillChangeTick, "factionChatLastGoodwillTick", -1);
        }

        public static void ResetCooldown()
        {
            _lastGoodwillChangeTick = -1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // RESULT TYPE
    // ═══════════════════════════════════════════════════════════════════

    public enum FactionActionResult
    {
        None,
        OnCooldown,
        GoodwillIncreased,
        GoodwillDecreased,
        RaidScheduled
    }

    // ═══════════════════════════════════════════════════════════════════
    // RAID SCHEDULER
    // Stores pending raids and fires them via the storyteller.
    // ═══════════════════════════════════════════════════════════════════

    public static class FactionRaidScheduler
    {
        private static List<ScheduledRaid> _pending = new List<ScheduledRaid>();

        public class ScheduledRaid : IExposable
        {
            public int    factionLoadID;
            public int    scheduledTick;
            public bool   executed;

            public void ExposeData()
            {
                Scribe_Values.Look(ref factionLoadID,  "factionLoadID",  0);
                Scribe_Values.Look(ref scheduledTick,  "scheduledTick",  0);
                Scribe_Values.Look(ref executed,       "executed",       false);
            }
        }

        public static void ScheduleRaid(Faction faction, int tick)
        {
            _pending.Add(new ScheduledRaid
            {
                factionLoadID = faction.loadID,
                scheduledTick = tick,
                executed      = false
            });
        }

        /// Called from MyStoryModComponent.Update() each tick.
        public static void Tick()
        {
            if (_pending == null || !_pending.Any()) return;

            int now = Find.TickManager.TicksGame;

            foreach (var raid in _pending.Where(r => !r.executed && r.scheduledTick <= now))
            {
                try
                {
                    var faction = Find.FactionManager.AllFactions
                        .FirstOrDefault(f => f.loadID == raid.factionLoadID);

                    if (faction == null || faction.HostileTo(Faction.OfPlayer))
                    {
                        raid.executed = true;
                        continue;
                    }

                    var map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
                    if (map == null) continue;

                    var parms = StorytellerUtility.DefaultParmsNow(
                        IncidentCategoryDefOf.ThreatBig, map);
                    parms.faction = faction;

                    var worker = IncidentDefOf.RaidEnemy.Worker;
                    if (worker.CanFireNow(parms))
                    {
                        worker.TryExecute(parms);
                        raid.executed = true;
                        Log.Message($"[EchoColony] Scheduled raid from {faction.Name} executed.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Error executing scheduled raid: {ex.Message}");
                    raid.executed = true;
                }
            }

            // Clean up executed raids
            _pending.RemoveAll(r => r.executed);
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref _pending, "factionScheduledRaids", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                if (_pending == null) _pending = new List<ScheduledRaid>();
        }
    }
}