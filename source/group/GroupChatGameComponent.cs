using Verse;
using System.Collections.Generic;
using System.Linq;

namespace EchoColony
{
    public class GroupChatGameComponent : GameComponent
    {
        private Dictionary<string, GroupChatSession> groupChats;

        public static GroupChatGameComponent Instance
        {
            get { return Current.Game.GetComponent<GroupChatGameComponent>(); }
        }

        public GroupChatGameComponent(Game game)
        {
            groupChats = new Dictionary<string, GroupChatSession>();
        }

        public GroupChatSession GetOrCreateSession(List<Pawn> participants)
        {
            foreach (KeyValuePair<string, GroupChatSession> pair in groupChats)
            {
                GroupChatSession session = pair.Value;
                bool allIncluded = true;
                foreach (Pawn p in participants)
                {
                    if (!session.HasParticipant(p))
                    {
                        allIncluded = false;
                        break;
                    }
                }

                if (allIncluded)
                {
                    return session;
                }
            }

            string id = System.Guid.NewGuid().ToString();
            GroupChatSession newSession = new GroupChatSession(id, participants);
            groupChats[id] = newSession;
            return newSession;
        }

        public void AddLine(List<Pawn> participants, string line)
        {
            GroupChatSession session = GetOrCreateSession(participants);
            session.AddMessage(line);
        }

        public List<string> GetChatHistory(List<Pawn> participants)
        {
            return GetOrCreateSession(participants).History;
        }

        public void ClearGroupChat(List<Pawn> participants)
        {
            GroupChatSession session = GetOrCreateSession(participants);
            session.History.Clear();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref groupChats, "groupChats", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (groupChats == null)
                    groupChats = new Dictionary<string, GroupChatSession>();

                // Filtrar sesiones corruptas que no tienen participantes
                var invalidSessions = groupChats
                    .Where(kv => kv.Value == null || kv.Value.ParticipantIds == null || kv.Value.ParticipantIds.Count == 0)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var id in invalidSessions)
                {
                    Log.Warning($"[EchoColony] Sesión inválida eliminada al cargar: {id}");
                    groupChats.Remove(id);
                }
            }
        }
    }
}
