using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

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

            }
        }
        public override void LoadedGame()
        {
            base.LoadedGame();
            CleanupOrphanedGroupChats();
        }

        public void CleanupOrphanedGroupChats()
        {
            if (groupChats == null || groupChats.Count == 0) return;

            // 1. Crear un set de todos los IDs de peones que existen actualmente en el juego
            // Incluye colonos, prisioneros, enemigos muertos, peones en caravanas, etc.
            HashSet<string> allValidIds = new HashSet<string>();

            foreach (Pawn p in Find.WorldPawns.AllPawnsAliveOrDead)
            {
                if (p != null) allValidIds.Add(p.ThingID);
            }

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn p in map.mapPawns.AllPawns)
                {
                    if (p != null) allValidIds.Add(p.ThingID);
                }
            }

            // 2. Identificar sesiones donde FALTE algún participante
            List<string> sessionsToRemove = new List<string>();

            foreach (var kvp in groupChats)
            {
                GroupChatSession session = kvp.Value;

                if (session == null || session.ParticipantIds == null || session.ParticipantIds.Count == 0)
                {
                    sessionsToRemove.Add(kvp.Key);
                    continue;
                }

                // Si uno solo de los participantes ya no está en el set global, marcamos para borrar
                bool anyParticipantMissing = session.ParticipantIds.Any(id => !allValidIds.Contains(id));

                if (anyParticipantMissing)
                {
                    sessionsToRemove.Add(kvp.Key);
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
