using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public class GroupChatSession : IExposable
    {
        public string SessionId;
        public List<string> ParticipantIds = new List<string>();
        public List<string> History = new List<string>();
        public float LastInteractionTime;

        [Unsaved]
        public List<Pawn> CachedParticipants = new List<Pawn>();

        [Unsaved]
        private int lastSessionDay = -1;

        public HashSet<Pawn> KickedOutColonists = new HashSet<Pawn>();


        public GroupChatSession() { }

        public GroupChatSession(string sessionId, List<Pawn> participants)
        {
            SessionId = sessionId;
            ParticipantIds = participants.Select(p => p.ThingID.ToString()).ToList();
            CachedParticipants = new List<Pawn>(participants);
            LastInteractionTime = Time.realtimeSinceStartup;
        }

        public void AddMessage(string msg)
        {
            int currentDay = GenDate.DaysPassed;

            // Verificar si es un nuevo día
            if (lastSessionDay == -1 || lastSessionDay != currentDay)
            {
                string dateHeader = GetFormattedDateHeader(currentDay);
                History.Add($"[DATE_SEPARATOR] {dateHeader}");
                lastSessionDay = currentDay;
            }

            History.Add(msg);
            LastInteractionTime = Time.realtimeSinceStartup;
        }

private string GetFormattedDateHeader(int day)
{
    // Usar el formato nativo de RimWorld (ya completamente localizado)
    string nativeDate = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
    string[] parts = nativeDate.Split(' ');
    string dateOnly = parts.Length >= 3 ? $"{parts[0]} {parts[1]} {parts[2]}" : nativeDate;
    
    return $"--- {dateOnly} ---";
}

        public bool HasParticipant(Pawn p)
        {
            return ParticipantIds.Contains(p.ThingID.ToString());
        }

        public List<Pawn> GetParticipantsFromMap(Map map)
        {
            if (CachedParticipants != null && CachedParticipants.Count > 0)
            {
                return CachedParticipants;
            }

            List<Pawn> found = new List<Pawn>();
            foreach (Pawn p in map.mapPawns.AllPawns)
            {
                if (ParticipantIds.Contains(p.ThingID.ToString()))
                {
                    found.Add(p);
                }
            }

            CachedParticipants = found;
            return CachedParticipants;
        }

        public void ExposeData()
{
    Scribe_Values.Look(ref SessionId, "SessionId");
    Scribe_Collections.Look(ref ParticipantIds, "ParticipantIds", LookMode.Value);
    Scribe_Collections.Look(ref History, "History", LookMode.Value);
    Scribe_Values.Look(ref lastSessionDay, "lastSessionDay", -1); // ✅ AGREGAR
    Scribe_Collections.Look(ref KickedOutColonists, "KickedOutColonists", LookMode.Reference);


    if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ParticipantIds == null)
                    ParticipantIds = new List<string>();

                if (History == null)
                    History = new List<string>();

                LastInteractionTime = Time.realtimeSinceStartup;
            }
}

        public static string BuildGroupId(List<Pawn> participants)
{
    return string.Join("-", participants
        .Select(p => p.ThingID.ToString())
        .OrderBy(id => id)); // Orden para consistencia
}

    }
}
