using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony
{
    public class GroupChatGameComponent : GameComponent
    {
        private Dictionary<string, GroupChatSession> groupChats;

        public static GroupChatGameComponent Instance =>
            Current.Game.GetComponent<GroupChatGameComponent>();

        public GroupChatGameComponent(Game game)
        {
            groupChats = new Dictionary<string, GroupChatSession>();
        }

        // Returns an existing session whose participant set matches EXACTLY,
        // or creates a new one. Subset matching is intentionally avoided —
        // it was causing sessions with extra participants to be reused.
        //*furel - improved id creation and search* Search for a existing id whit listed pawns or crates one if there is not exist 
        public GroupChatSession GetOrCreateSession(List<Pawn> participants)
        {
            var requestedIds = participants
                .Where(p => p != null)
                .Select(p => p.ThingID.ToString())
                .OrderBy(rid => rid)
                .ToList();

            foreach (var pair in groupChats)
            {
                var session = pair.Value;
                if (session?.ParticipantIds == null) continue;

                var sessionIds = session.ParticipantIds.OrderBy(sid => sid).ToList();

                if (sessionIds.SequenceEqual(requestedIds))
                    return session;
            }

            // No matching session found — create a fresh one
            string id      = System.Guid.NewGuid().ToString();
            var newSession  = new GroupChatSession(id, participants);
            groupChats[id]  = newSession;
            return newSession;
        }

        // Updates the participant list of an existing session.
        // Called when a participant is added or removed mid-conversation.
        //*furel - improved id creation and search* The original code creates many session IDs. Every time a window opens and participants change, an ID is created, but existing IDs are never searched.
        //                                      This method searches for an existing session ID that matches the current participants; if it doesn't find one, it creates a new one.
        //                                      It doesn't record it until a message is sent to the AI.
        public GroupChatSession UpdateSessionParticipants(GroupChatSession existing, List<Pawn> newParticipants)
        {
            var oldKey = groupChats.FirstOrDefault(kv => kv.Value == existing).Key;
            if (oldKey != null)
                groupChats.Remove(oldKey);

            existing.ParticipantIds = newParticipants
                .Where(p => p != null)
                .Select(p => p.ThingID.ToString())
                .ToList();
            existing.CachedParticipants = new List<Pawn>(newParticipants);
            return existing;
        }

        //*furel - hold registration* Here is were we registrer the session in the save file.
        public void RegistingSession(GroupChatSession session)
        {
            if (!groupChats.ContainsKey(session.SessionId))
            {
                groupChats.Add(session.SessionId, session);
            }
        }

        public void AddLine(List<Pawn> participants, string line)
        {
            GetOrCreateSession(participants).AddMessage(line);
        }

        public List<string> GetChatHistory(List<Pawn> participants)
        {
            return GetOrCreateSession(participants).History;
        }

        public void ClearGroupChat(List<Pawn> participants)
        {
            GetOrCreateSession(participants).History.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref groupChats, "groupChats", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (groupChats == null)
                    groupChats = new Dictionary<string, GroupChatSession>();

                // Remove corrupt sessions that have no participants
                var invalid = groupChats
                    .Where(kv => kv.Value == null ||
                                 kv.Value.ParticipantIds == null ||
                                 kv.Value.ParticipantIds.Count == 0)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in invalid)
                {
                    Log.Warning($"[EchoColony] Removed invalid group chat session on load: {key}");
                    groupChats.Remove(key);
                }
            }
        }
    }
}