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
            var existing = GetSession(participants);  //*furel - improved id creation and search* Uses GetSession to search for a existing session for given pawns. Returns null if none is foud.
            if (existing != null) return existing;

            //*fuel - improved id creation and search* Crates the id for the session but is not register until a messege from the user is sended to the IA.
            return new GroupChatSession(Guid.NewGuid().ToString(), participants);
        }

        //*furel - improved id creation and search* Modified GetSession to actualy just get the session that matches the probided list and be usen in GetOrCreateSession and UpdateSessionParticipants. Returns null isf none is found.
        private GroupChatSession GetSession(List<Pawn> participants)
        {
            var requestedIds = participants
                .Where(p => p != null)
                .Select(p => p.ThingID.ToString())
                .OrderBy(id => id)
                .ToList();

            return groupChats.Values.FirstOrDefault(s =>
                s.ParticipantIds.OrderBy(id => id).SequenceEqual(requestedIds));
        }

        // Updates the participant list of an existing session.
        // Called when a participant is added or removed mid-conversation.
        //*furel - improved id creation and search* The original code creates many session IDs. Every time a window opens and participants change, an ID is created, but existing IDs are never searched.
        //                                      This method searches for an existing session ID that matches the current participants; if it doesn't find one, it creates a new one.
        //                                      It doesn't record it until a message is sent to the AI.
        public GroupChatSession UpdateSessionParticipants(GroupChatSession existing, List<Pawn> newParticipants)
        {
            var match = GetSession(newParticipants);
            if (match != null) return match;

            //*fuel - improved id creation and search* Crates the id for the session but is not register yet.
            return new GroupChatSession(Guid.NewGuid().ToString(), newParticipants);
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

            //// 3. Ejecutar la eliminación
            //foreach (string sessionId in sessionsToRemove)
            //{
            //    groupChats.Remove(sessionId);
            //}

            //if (sessionsToRemove.Count > 0)
            //{
            //    Log.Message($"[EchoColony] Limpieza de Grupos: Se eliminaron {sessionsToRemove.Count} sesiones porque uno o más miembros ya no existen.");
            //}
        }
    }
}