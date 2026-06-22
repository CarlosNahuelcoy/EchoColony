using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    public class ConversationCooldownTracker : GameComponent
    {
        // Per-pair cooldown: "ThingID_A|ThingID_B" → last tick
        private Dictionary<string, int> lastConversationTick = new Dictionary<string, int>();

        // Global daily limit tracking
        private int conversationsToday = 0;
        private int lastResetDay       = -1;

        private static ConversationCooldownTracker instance;
        public static ConversationCooldownTracker Instance
        {
            get
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<ConversationCooldownTracker>();
                    if (instance == null)
                    {
                        instance = new ConversationCooldownTracker(Current.Game);
                        Current.Game.components.Add(instance);
                    }
                }
                return instance;
            }
        }

        public ConversationCooldownTracker(Game game) { }

        // ── Public API ────────────────────────────────────────────────────────────

        public static bool CanConverse(Pawn a, Pawn b)
        {
            if (a == null || b == null) return false;
            var inst = Instance;
            if (inst == null) return true;

            // Check global daily limit first
            if (!inst.CheckDailyLimit()) return false;

            // Then check per-pair cooldown
            string key = MakeKey(a, b);
            if (!inst.lastConversationTick.TryGetValue(key, out int lastTick))
                return true;

            int cooldownTicks = GetCooldownTicks();
            return Find.TickManager.TicksGame - lastTick >= cooldownTicks;
        }

        public static void RecordConversation(Pawn a, Pawn b)
        {
            if (a == null || b == null) return;
            var inst = Instance;
            if (inst == null) return;

            string key = MakeKey(a, b);
            inst.lastConversationTick[key] = Find.TickManager.TicksGame;

            // Increment daily counter
            inst.EnsureDayReset();
            inst.conversationsToday++;
        }

        // Returns how many conversations have happened today, for UI display
        public static int ConversationsToday
        {
            get
            {
                var inst = Instance;
                if (inst == null) return 0;
                inst.EnsureDayReset();
                return inst.conversationsToday;
            }
        }

        // ── Daily limit logic ─────────────────────────────────────────────────────

        private bool CheckDailyLimit()
        {
            EnsureDayReset();
            int limit = MyMod.Settings?.conversationDailyLimit ?? 0;
            if (limit <= 0) return true; // 0 = unlimited
            return conversationsToday < limit;
        }

        private void EnsureDayReset()
        {
            int today = GenDate.DaysPassed;
            if (today != lastResetDay)
            {
                conversationsToday = 0;
                lastResetDay       = today;
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastConversationTick, "lastConversationTick",
                LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref conversationsToday, "conversationsToday", 0);
            Scribe_Values.Look(ref lastResetDay,       "lastResetDay",       -1);

            if (Scribe.mode == LoadSaveMode.LoadingVars && lastConversationTick == null)
                lastConversationTick = new Dictionary<string, int>();
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60000 != 0) return;

            EnsureDayReset();

            int pruneThreshold = Find.TickManager.TicksGame - (GetCooldownTicks() * 2);
            var toRemove = new List<string>();
            foreach (var kvp in lastConversationTick)
                if (kvp.Value < pruneThreshold) toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                lastConversationTick.Remove(key);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string MakeKey(Pawn a, Pawn b)
        {
            string idA = a.ThingID;
            string idB = b.ThingID;
            return string.Compare(idA, idB, System.StringComparison.Ordinal) < 0
                ? idA + "|" + idB
                : idB + "|" + idA;
        }

        private static int GetCooldownTicks()
        {
            int hours = MyMod.Settings?.conversationCooldownHours ?? 6;
            return hours * 2500;
        }
    }
}