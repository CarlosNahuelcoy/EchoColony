using System.Collections.Generic;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Tracks when a pawn pair last had an AI-generated conversation.
    /// Prevents the same two pawns from triggering multiple API calls
    /// if RimWorld fires interactions rapidly.
    /// 
    /// Default cooldown: 6 in-game hours (~15,000 ticks).
    /// Configurable via mod settings.
    /// </summary>
    public class ConversationCooldownTracker : GameComponent
    {
        // Key: "ThingID_A|ThingID_B" (sorted so order doesn't matter)
        private Dictionary<string, int> lastConversationTick = new Dictionary<string, int>();

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

        /// <summary>
        /// Returns true if these two pawns are allowed to start a new conversation.
        /// </summary>
        public static bool CanConverse(Pawn a, Pawn b)
        {
            if (a == null || b == null) return false;
            var inst = Instance;
            if (inst == null) return true;

            string key = MakeKey(a, b);
            if (!inst.lastConversationTick.TryGetValue(key, out int lastTick))
                return true;

            int cooldownTicks = GetCooldownTicks();
            return Find.TickManager.TicksGame - lastTick >= cooldownTicks;
        }

        /// <summary>
        /// Records that these two pawns just had a conversation.
        /// </summary>
        public static void RecordConversation(Pawn a, Pawn b)
        {
            if (a == null || b == null) return;
            var inst = Instance;
            if (inst == null) return;

            string key = MakeKey(a, b);
            inst.lastConversationTick[key] = Find.TickManager.TicksGame;
        }

        // ── Persistence ───────────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastConversationTick, "lastConversationTick",
                LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && lastConversationTick == null)
                lastConversationTick = new Dictionary<string, int>();
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        public override void GameComponentTick()
        {
            // Prune entries older than 2x the cooldown every ~1 in-game day
            if (Find.TickManager.TicksGame % 60000 != 0) return;

            int pruneThreshold = Find.TickManager.TicksGame - (GetCooldownTicks() * 2);
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kvp in lastConversationTick)
                if (kvp.Value < pruneThreshold) toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                lastConversationTick.Remove(key);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string MakeKey(Pawn a, Pawn b)
        {
            // Sort so A|B == B|A
            string idA = a.ThingID;
            string idB = b.ThingID;
            return string.Compare(idA, idB, System.StringComparison.Ordinal) < 0
                ? $"{idA}|{idB}"
                : $"{idB}|{idA}";
        }

        private static int GetCooldownTicks()
        {
            // 6 in-game hours by default. Could be exposed in mod settings later.
            int hours = MyMod.Settings?.conversationCooldownHours ?? 6;
            return hours * 2500; // 2500 ticks ≈ 1 in-game hour
        }
    }
}
