using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Tracks the state of spontaneous messages per colonist.
    /// Persistent GameComponent saved with the game file.
    /// </summary>
    public class SpontaneousMessageTracker : GameComponent
    {
        private static SpontaneousMessageTracker instance;
        public static SpontaneousMessageTracker Instance => instance;

        // Per-colonist tracking keyed by ThingID
        private Dictionary<string, ColonistMessageTracker> colonistTrackers = new Dictionary<string, ColonistMessageTracker>();

        // Timestamp for the next global random message check
        private int nextRandomMessageCheck = 0;

        public SpontaneousMessageTracker(Game game)
        {
            instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref colonistTrackers, "colonistTrackers", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref nextRandomMessageCheck, "nextRandomMessageCheck", 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (colonistTrackers == null)
                    colonistTrackers = new Dictionary<string, ColonistMessageTracker>();
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            StorytellerSpontaneousMessageSystem.Update();

            if (Find.TickManager.TicksGame % GenDate.TicksPerHour != 0)
                return;

            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return;

            CleanupOldTrackers();

            if (MyMod.Settings.AreRandomMessagesEnabled())
            {
                CheckRandomMessages();
            }
        }

        /// <summary>
        /// Gets or creates the tracker for a specific colonist.
        /// </summary>
        public ColonistMessageTracker GetTrackerFor(Pawn pawn)
        {
            if (pawn == null) return null;

            string key = pawn.ThingID;
            if (!colonistTrackers.ContainsKey(key))
            {
                colonistTrackers[key] = new ColonistMessageTracker(pawn);
            }

            return colonistTrackers[key];
        }

        /// <summary>
        /// Checks whether a colonist can send a message considering all limits.
        /// </summary>
        public bool CanSendMessage(Pawn pawn, TriggerType triggerType)
        {
            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return false;

            if (triggerType == TriggerType.Incident && !MyMod.Settings.AreIncidentMessagesEnabled())
                return false;
            if (triggerType == TriggerType.Random && !MyMod.Settings.AreRandomMessagesEnabled())
                return false;

            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (settings == null || !settings.enabled)
                return false;

            if (!settings.IsTriggerAllowed(triggerType))
                return false;

            var tracker = GetTrackerFor(pawn);
            return tracker != null && tracker.CanSendMessage(pawn, triggerType);
        }

        /// <summary>
        /// Registers that a colonist sent a message.
        /// </summary>
        public void RegisterMessage(Pawn pawn, TriggerType triggerType)
        {
            var tracker = GetTrackerFor(pawn);
            tracker?.RegisterMessage(triggerType);
        }

        /// <summary>
        /// Registers that a colonist sent a message, storing the message text and topic.
        /// </summary>
        public void RegisterMessage(Pawn pawn, TriggerType triggerType, string messageText, string topic)
        {
            var tracker = GetTrackerFor(pawn);
            if (tracker == null) return;

            tracker.RegisterMessage(triggerType);

            if (!string.IsNullOrWhiteSpace(messageText))
                tracker.lastSentMessage = messageText;

            if (!string.IsNullOrWhiteSpace(topic))
                tracker.RegisterTopic(topic);
        }

        /// <summary>
        /// Marks or unmarks that a colonist has a message pending a reply.
        /// </summary>
        public void SetPendingResponse(Pawn pawn, bool isPending)
        {
            var tracker = GetTrackerFor(pawn);
            if (tracker != null)
            {
                tracker.hasPendingResponse = isPending;
                if (!isPending)
                    tracker.lastSentMessage = "";
            }
        }

        /// <summary>
        /// Checks whether a colonist has a message pending a reply.
        /// </summary>
        public bool HasPendingResponse(Pawn pawn)
        {
            var tracker = GetTrackerFor(pawn);
            return tracker?.hasPendingResponse ?? false;
        }

        /// <summary>
        /// Checks whether it is time to evaluate random messages.
        /// </summary>
        private void CheckRandomMessages()
        {
            int currentTick = Find.TickManager.TicksGame;

            if (currentTick < nextRandomMessageCheck)
                return;

            float hoursToNext = MyMod.Settings.randomMessageIntervalHours;
            nextRandomMessageCheck = currentTick + (int)(hoursToNext * GenDate.TicksPerHour);

            RandomMessageEvaluator.EvaluateRandomMessage();
        }

        /// <summary>
        /// Removes trackers for colonists that no longer exist.
        /// </summary>
        private void CleanupOldTrackers()
        {
            if (Find.TickManager.TicksGame % (GenDate.TicksPerHour * 24) != 0)
                return;

            var validThingIDs = new HashSet<string>();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    validThingIDs.Add(pawn.ThingID);
                }
            }

            var toRemove = colonistTrackers.Keys.Where(k => !validThingIDs.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                colonistTrackers.Remove(key);
            }
        }
    }

    /// <summary>
    /// Tracking data for a single colonist.
    /// </summary>
    public class ColonistMessageTracker : IExposable
    {
        public int messagesToday = 0;
        public int lastDayChecked = 0;
        public int lastMessageTick = 0;
        public bool hasPendingResponse = false;

        // Text of the last message sent without a reply, used to give follow-up context to the AI
        public string lastSentMessage = "";

        // Recent topics brought up, used to prevent the AI from repeating content
        public List<string> recentTopics = new List<string>();
        private const int MAX_RECENT_TOPICS = 4;

        // Last message timestamp per trigger type
        public Dictionary<TriggerType, int> lastTriggerTime = new Dictionary<TriggerType, int>();

        public ColonistMessageTracker()
        {
            // Empty constructor for serialization
        }

        public ColonistMessageTracker(Pawn pawn)
        {
            lastDayChecked = GenDate.DaysPassed;
            lastMessageTick = 0;
            messagesToday = 0;
            hasPendingResponse = false;
            lastSentMessage = "";
            recentTopics = new List<string>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref messagesToday, "messagesToday", 0);
            Scribe_Values.Look(ref lastDayChecked, "lastDayChecked", 0);
            Scribe_Values.Look(ref lastMessageTick, "lastMessageTick", 0);
            Scribe_Values.Look(ref hasPendingResponse, "hasPendingResponse", false);
            Scribe_Values.Look(ref lastSentMessage, "lastSentMessage", "");
            Scribe_Collections.Look(ref lastTriggerTime, "lastTriggerTime", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref recentTopics, "recentTopics", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (lastTriggerTime == null)
                    lastTriggerTime = new Dictionary<TriggerType, int>();
                if (recentTopics == null)
                    recentTopics = new List<string>();
            }
        }

        /// <summary>
        /// Checks whether a message can be sent, considering limits and cooldowns.
        /// </summary>
        public bool CanSendMessage(Pawn pawn, TriggerType triggerType)
        {
            // Daily reset
            int today = GenDate.DaysPassed;
            if (today != lastDayChecked)
            {
                messagesToday = 0;
                lastDayChecked = today;
            }

            // Daily limit
            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (messagesToday >= settings.maxMessagesPerDay)
                return false;

            // General cooldown
            int ticksSinceLastMessage = Find.TickManager.TicksGame - lastMessageTick;
            float hoursSinceLastMessage = ticksSinceLastMessage / (float)GenDate.TicksPerHour;

            if (hoursSinceLastMessage < settings.cooldownHours)
                return false;

            // Per-trigger cooldown (minimum 2h between the same trigger type)
            if (lastTriggerTime.ContainsKey(triggerType))
            {
                int ticksSinceTrigger = Find.TickManager.TicksGame - lastTriggerTime[triggerType];
                float hoursSinceTrigger = ticksSinceTrigger / (float)GenDate.TicksPerHour;

                if (hoursSinceTrigger < 2f)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a sent message.
        /// </summary>
        public void RegisterMessage(TriggerType triggerType)
        {
            messagesToday++;
            lastMessageTick = Find.TickManager.TicksGame;
            lastTriggerTime[triggerType] = Find.TickManager.TicksGame;
            hasPendingResponse = true;
        }

        /// <summary>
        /// Adds a topic to the recent topics list, evicting the oldest if over the limit.
        /// </summary>
        public void RegisterTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return;
            recentTopics.Insert(0, topic);
            if (recentTopics.Count > MAX_RECENT_TOPICS)
                recentTopics.RemoveAt(recentTopics.Count - 1);
        }
    }

    /// <summary>
    /// Evaluates and dispatches random spontaneous messages.
    /// </summary>
    public static class RandomMessageEvaluator
    {
        public static void EvaluateRandomMessage()
        {
            var eligible = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Random);

            if (!eligible.Any())
                return;

            var selected = SpontaneousMessageEvaluator.SelectBestCandidate(eligible, TriggerType.Random, null);

            if (selected == null)
                return;

            if (!ColonistWillingnessEvaluator.WantsToSpeak(selected, TriggerType.Random, ""))
                return;

            var request = new MessageRequest(selected, TriggerType.Random, "casual conversation", 0.3f);
            MyStoryModComponent.Instance.StartCoroutine(
                SpontaneousMessageGenerator.GenerateAndSendMessage(request)
            );
        }
    }
}