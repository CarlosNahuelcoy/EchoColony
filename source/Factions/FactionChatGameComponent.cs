using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Factions
{
    /// <summary>
    /// GameComponent that persists faction chat history and conversation metadata.
    ///
    /// Tracks two independent histories per faction:
    ///   - Colonist mode (operator pawn mediates)
    ///   - Player mode (player speaks directly)
    ///
    /// The faction leader is aware of both histories independently.
    /// </summary>
    public class FactionChatGameComponent : GameComponent
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        private static FactionChatGameComponent _instance;
        public static FactionChatGameComponent Instance
        {
            get
            {
                if (_instance == null && Current.Game != null)
                    _instance = Current.Game.GetComponent<FactionChatGameComponent>();
                return _instance;
            }
        }

        // ── Storage key helpers ───────────────────────────────────────────────────
        // We encode mode into the key so we can use flat dictionaries:
        // colonist mode → factionID * 2
        // player mode   → factionID * 2 + 1
        private static int Key(int factionLoadID, bool isPlayerMode) =>
            isPlayerMode ? factionLoadID * 2 + 1 : factionLoadID * 2;

        // ── Chat logs ─────────────────────────────────────────────────────────────
        private Dictionary<int, List<string>> chatLogs           = new Dictionary<int, List<string>>();

        // ── Conversation metadata ─────────────────────────────────────────────────
        private Dictionary<int, int> conversationCounts          = new Dictionary<int, int>();
        private Dictionary<int, int> lastConversationTick        = new Dictionary<int, int>();
        private Dictionary<int, int> firstConversationTick       = new Dictionary<int, int>();

        // ── Visitor cooldown tracking ─────────────────────────────────────────────
        // 15 days between visitor requests (same as vanilla trade/military)
        private Dictionary<int, int> lastVisitorRequestTick = new Dictionary<int, int>();
        private const int VISITOR_COOLDOWN_TICKS = 900000;

        public FactionChatGameComponent(Game game) : base() { }

        public bool IsVisitorOnCooldown(Faction faction)
        {
            if (faction == null) return false;
            lastVisitorRequestTick.TryGetValue(faction.loadID, out int lastTick);
            if (lastTick <= 0) return false;
            return (Find.TickManager.TicksGame - lastTick) < VISITOR_COOLDOWN_TICKS;
        }

        public void RecordVisitorRequest(Faction faction)
        {
            if (faction == null) return;
            lastVisitorRequestTick[faction.loadID] = Find.TickManager.TicksGame;
        }

        // ═══════════════════════════════════════════════════════════════
        // CHAT LOG
        // ═══════════════════════════════════════════════════════════════

        public List<string> GetChat(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return new List<string>();
            int k = Key(faction.loadID, isPlayerMode);
            if (!chatLogs.ContainsKey(k))
                chatLogs[k] = new List<string>();
            return chatLogs[k];
        }

        public void AddLine(Faction faction, bool isPlayerMode, string line)
        {
            if (faction == null || string.IsNullOrWhiteSpace(line)) return;
            var log = GetChat(faction, isPlayerMode);
            log.Add(line);
            if (log.Count > 200) log.RemoveAt(0);
        }

        public void ClearChat(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return;
            int k = Key(faction.loadID, isPlayerMode);
            if (chatLogs.ContainsKey(k))
                chatLogs[k].Clear();
        }

        // ═══════════════════════════════════════════════════════════════
        // CONVERSATION METADATA
        // ═══════════════════════════════════════════════════════════════

        /// True if this faction + mode combination has never been used before.
        public bool IsFirstContact(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return true;
            int k = Key(faction.loadID, isPlayerMode);
            return !conversationCounts.ContainsKey(k) || conversationCounts[k] == 0;
        }

        /// Total completed conversations for this faction + mode.
        public int GetConversationCount(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return 0;
            conversationCounts.TryGetValue(Key(faction.loadID, isPlayerMode), out int count);
            return count;
        }

        /// Game tick of the last conversation for this faction + mode, or -1 if never.
        public int GetLastConversationTick(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return -1;
            lastConversationTick.TryGetValue(Key(faction.loadID, isPlayerMode), out int tick);
            return tick == 0 ? -1 : tick;
        }

        /// Human-readable description of when the last conversation happened.
        public string GetLastConversationDescription(Faction faction, bool isPlayerMode)
        {
            int lastTick = GetLastConversationTick(faction, isPlayerMode);
            if (lastTick < 0) return null;

            int ticksAgo = Find.TickManager.TicksGame - lastTick;
            int hoursAgo = ticksAgo / 2500;

            if (hoursAgo < 1)    return "just a moment ago";
            if (hoursAgo < 3)    return "earlier today";
            if (hoursAgo < 24)   return $"about {hoursAgo} hours ago";
            int daysAgo = hoursAgo / 24;
            if (daysAgo == 1)    return "yesterday";
            if (daysAgo < 7)     return $"{daysAgo} days ago";
            if (daysAgo < 30)    return $"about {daysAgo / 7} week{(daysAgo / 7 > 1 ? "s" : "")} ago";
            int monthsAgo = daysAgo / 30;
            if (monthsAgo < 12)  return $"about {monthsAgo} month{(monthsAgo > 1 ? "s" : "")} ago";
            return "a long time ago";
        }

        /// Call this when a conversation session ends (window closes).
        public void RecordConversationEnded(Faction faction, bool isPlayerMode)
        {
            if (faction == null) return;
            int k    = Key(faction.loadID, isPlayerMode);
            int tick = Find.TickManager.TicksGame;

            if (!conversationCounts.ContainsKey(k))
                conversationCounts[k] = 0;
            conversationCounts[k]++;

            lastConversationTick[k] = tick;

            if (!firstConversationTick.ContainsKey(k) || firstConversationTick[k] == 0)
                firstConversationTick[k] = tick;
        }

        // ═══════════════════════════════════════════════════════════════
        // SAVE / LOAD
        // ═══════════════════════════════════════════════════════════════

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref chatLogs,             "factionChatLogs",             LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref conversationCounts,   "factionConversationCounts",   LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastConversationTick,  "factionLastConversationTick",  LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref firstConversationTick, "factionFirstConversationTick", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastVisitorRequestTick,"factionLastVisitorTick",       LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (chatLogs             == null) chatLogs             = new Dictionary<int, List<string>>();
                if (conversationCounts   == null) conversationCounts   = new Dictionary<int, int>();
                if (lastConversationTick  == null) lastConversationTick  = new Dictionary<int, int>();
                if (firstConversationTick == null) firstConversationTick = new Dictionary<int, int>();
                if (lastVisitorRequestTick == null) lastVisitorRequestTick = new Dictionary<int, int>();
            }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            _instance            = this;
            chatLogs             = new Dictionary<int, List<string>>();
            conversationCounts   = new Dictionary<int, int>();
            lastConversationTick  = new Dictionary<int, int>();
            firstConversationTick = new Dictionary<int, int>();
            lastVisitorRequestTick = new Dictionary<int, int>();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            _instance = this;
        }
    }
}