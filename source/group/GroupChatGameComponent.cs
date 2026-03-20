using RimWorld;
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
            string id        = System.Guid.NewGuid().ToString();
            var newSession   = new GroupChatSession(id, participants);
            groupChats[id]   = newSession;
            return newSession;
        }

        // Updates the participant list of an existing session.
        // Called when a participant is added or removed mid-conversation.
        public GroupChatSession UpdateSessionParticipants(GroupChatSession existing, List<Pawn> newParticipants)
        {
            // Remove the old entry
            var oldKey = groupChats.FirstOrDefault(kv => kv.Value == existing).Key;
            if (oldKey != null)
                groupChats.Remove(oldKey);

            // Update the session's participant list in-place
            existing.ParticipantIds = newParticipants
                .Where(p => p != null)
                .Select(p => p.ThingID.ToString())
                .ToList();
            existing.CachedParticipants = new List<Pawn>(newParticipants);

            groupChats[existing.SessionId] = existing;
            return existing;
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

        //public void CleanupOrphanedGroupChats()
        //{
        //    if (groupChats == null) return;

        //    // 1. Obtener todos los IDs de peones que el juego aún reconoce como existentes
        //    var validPawnIDs = new HashSet<string>(
        //        PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
        //            .Where(p => p != null)
        //            .Select(p => p.ThingID)
        //    );

        //    // 2. Identificar sesiones a eliminar
        //    List<string> sessionsToRemove = new List<string>();

        //    foreach (var kvp in groupChats)
        //    {
        //        GroupChatSession session = kvp.Value;

        //        // Criterios de eliminación:
        //        // - La sesión es nula
        //        // - No tiene participantes
        //        // - Al menos UNO de los participantes ya no existe en el mundo
        //        if (session == null ||
        //            session.ParticipantIds == null ||
        //            session.ParticipantIds.Count == 0 ||
        //            session.ParticipantIds.Any(id => !validPawnIDs.Contains(id)))
        //        {
        //            sessionsToRemove.Add(kvp.Key);
        //        }
        //    }

        //    // 3. Ejecutar la limpieza
        //    foreach (var sessionId in sessionsToRemove)
        //    {
        //        groupChats.Remove(sessionId);
        //    }

        //    if (sessionsToRemove.Count > 0)
        //    {
        //        Log.Message($"[EchoColony] Se eliminaron {sessionsToRemove.Count} sesiones de chat grupal (participantes inexistentes o datos corruptos).");
        //    }
        //}

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

            // 3. Ejecutar la eliminación
            foreach (string sessionId in sessionsToRemove)
            {
                groupChats.Remove(sessionId);
            }

            if (sessionsToRemove.Count > 0)
            {
                Log.Message($"[EchoColony] Limpieza de Grupos: Se eliminaron {sessionsToRemove.Count} sesiones porque uno o más miembros ya no existen.");
            }
        }
    }
}