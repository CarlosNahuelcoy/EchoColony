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
        public List<string> ParticipantIds  = new List<string>();
        public List<string> History         = new List<string>();
        public List<string> RecentHistory = new List<string>();
        public int lastInteractionTick = -1;

        [Unsaved] public List<Pawn>   CachedParticipants  = new List<Pawn>();
        [Unsaved] private int         lastSessionDay       = -1;

        // System message prefix — used to identify non-dialogue lines
        // so they can be rendered differently in the UI
        public const string SystemPrefix = "[SYSTEM]";

        public GroupChatSession() { }

        public GroupChatSession(string sessionId, List<Pawn> participants)
        {
            SessionId           = sessionId;
            ParticipantIds      = participants.Select(p => p.ThingID.ToString()).ToList();
            CachedParticipants  = new List<Pawn>(participants);
            lastInteractionTick = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsExpired()
        {
            if (lastInteractionTick <= 0) return false;

            int hours = MyMod.Settings != null ? MyMod.Settings.chatSessionTimeoutHours : 5;

            // 0 = Tiempo ilimitado (nunca expira)
            if (hours <= 0) return false;

            int timeoutTicks = hours * GenDate.TicksPerHour;
            int currentTick = Find.TickManager.TicksGame;

            return (currentTick - lastInteractionTick) > timeoutTicks;
        }

        public int UpdateLastInteractionTick()
        {
            lastInteractionTick = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            return lastInteractionTick;
        }

        // Adds a regular dialogue message to history.
        // Automatically inserts a date separator when the in-game day changes.
        public void AddMessage(string msg)
        {
            int currentDay = GenDate.DaysPassed;

            if (lastSessionDay == -1 || lastSessionDay != currentDay)
            {
                string dateHeader = GetFormattedDateHeader(currentDay);
                History.Add($"[DATE_SEPARATOR] {dateHeader}");
                lastSessionDay = currentDay;
            }

            History.Add(msg);
        }

        // Adds a system/event message (join, leave, clear, etc.).
        // These are stored with a [SYSTEM] prefix and rendered as subtle separators.
        public void AddSystemMessage(string text)
        {
            History.Add(SystemPrefix + text);
        }

        // Returns true if msg is a system/event line (not dialogue)
        public static bool IsSystemMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            return msg.StartsWith(SystemPrefix) ||
                   msg.StartsWith("[DATE_SEPARATOR]");
        }

        // Returns the display text for a message, stripping any internal prefixes
        public static string GetDisplayText(string msg)
        {
            if (msg.StartsWith(SystemPrefix))
                return msg.Substring(SystemPrefix.Length).Trim();
            if (msg.StartsWith("[DATE_SEPARATOR]"))
                return msg.Substring("[DATE_SEPARATOR]".Length).Trim();
            if (msg.StartsWith("[USER]"))
                return msg.Substring("[USER]".Length).Trim();
            return msg;
        }

        private string GetFormattedDateHeader(int day)
        {
            string nativeDate = GenDate.DateFullStringWithHourAt(
                GenTicks.TicksAbs,
                Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));

            string[] parts  = nativeDate.Split(' ');
            string dateOnly = parts.Length >= 6  
                ? $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {parts[4]} {parts[5]}" //*furel - display full date* {parts[0]} = Day; {parts[1]} = Number; {parts[2]} = of; {parts[3]} = Month; {parts[4]} = of; {parts[5]} = Year
                : nativeDate;

            return $"--- {dateOnly} ---";
        }

        public bool HasParticipant(Pawn p) =>
            ParticipantIds.Contains(p.ThingID.ToString());

        public List<Pawn> GetParticipantsFromMap(Map map)
        {
            if (CachedParticipants != null && CachedParticipants.Count > 0)
                return CachedParticipants;

            var found = new List<Pawn>();
            foreach (var p in map.mapPawns.AllPawns)
            {
                if (ParticipantIds.Contains(p.ThingID.ToString()))
                    found.Add(p);
            }

            CachedParticipants = found;
            return CachedParticipants;
        }

        /// <summary>
        /// Extrae las últimas líneas de diálogo real desde History para usarlas como contexto pasivo (x).
        /// Si la sesión expiró por tiempo, devuelve una lista vacía para reiniciar contexto.
        /// </summary>
        public List<string> GetRecentDialogue(int maxLines = 10)
        {
            if (IsExpired()) return new List<string>();

            // Filtra marcadores del sistema y fechas para quedarse solo con el diálogo de personajes/usuario
            return History
                .Where(msg => !IsSystemMessage(msg))
                .TakeLast(maxLines)
                .ToList();
        }

        //public void CommitRoundToRecentHistory(string userInput, List<string> roundConversation)
        //{
        //    if (!string.IsNullOrWhiteSpace(userInput))
        //    {
        //        RecentHistory.Add($"Player: \"{userInput.Trim()}\"");
        //    }

        //    if (roundConversation != null && roundConversation.Count > 0)
        //    {
        //        RecentHistory.AddRange(roundConversation);
        //    }

        //    // Mantenemos RecentHistory en un tamaño manejable (p. ej. los últimos 20 mensajes)
        //    if (RecentHistory.Count > 20)
        //    {
        //        RecentHistory = RecentHistory.TakeLast(20).ToList();
        //    }
        //}

        public void ExposeData()
        {
            Scribe_Values.Look(ref SessionId,           "SessionId");
            Scribe_Collections.Look(ref ParticipantIds, "ParticipantIds", LookMode.Value);
            Scribe_Collections.Look(ref History,        "History",        LookMode.Value);
            Scribe_Values.Look(ref lastSessionDay,      "lastSessionDay", -1);
            Scribe_Values.Look(ref lastInteractionTick, "lastInteractionTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ParticipantIds   == null) ParticipantIds   = new List<string>();
                if (History          == null) History          = new List<string>();
            }
        }

        public static string BuildGroupId(List<Pawn> participants) =>
            string.Join("-", participants
                .Select(p => p.ThingID.ToString())
                .OrderBy(id => id));
    }
}