using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public class ChatGameComponent : GameComponent
    {
        private Dictionary<string, List<string>> savedChats = new Dictionary<string, List<string>>();
        public static ChatGameComponent Instance => Current.Game.GetComponent<ChatGameComponent>();

        private Dictionary<string, string> pawnVoiceMap = new Dictionary<string, string>();
        
        // Track the last day a chat occurred for each pawn to insert date separators
        private Dictionary<string, int> lastChatDay = new Dictionary<string, int>();

        public class PawnInteractionInfo : IExposable
        {

            public int LastTick;
            public int StartTurn;
            public int CurrentTurn;
            public int LastSavedTurnCount;

            public void ExposeData()
            {
                Scribe_Values.Look(ref LastTick, "lastTick", 0);
                Scribe_Values.Look(ref StartTurn, "startTurn", 0);
                Scribe_Values.Look(ref CurrentTurn, "currentTurn", 0);
                Scribe_Values.Look(ref LastSavedTurnCount, "lastSavedTurnCount", 0);
            }
        }
        //*furel - New group chat reference system to store group chat history and participants.
        public class GroupChatRecord : IExposable
        {
            public List<string> ParticipantNames = new List<string>();

            public GroupChatRecord() { }

            public GroupChatRecord(IEnumerable<string> participantNames)
            {
                this.ParticipantNames = participantNames?.ToList() ?? new List<string>();
            }

            public void ExposeData()
            {
                Scribe_Collections.Look(ref ParticipantNames, "participantNames", LookMode.Value);
            }
        }

        public class GroupChatHistoryHolder : IExposable
        {
            public List<GroupChatRecord> Records = new List<GroupChatRecord>();

            public GroupChatHistoryHolder() { }

            public void ExposeData()
            {
                Scribe_Collections.Look(ref Records, "records", LookMode.Deep);

                if (Scribe.mode == LoadSaveMode.PostLoadInit && Records == null)
                {
                    Records = new List<GroupChatRecord>();
                }
            }
        }

        private Dictionary<string, GroupChatHistoryHolder> groupChatHistory = new Dictionary<string, GroupChatHistoryHolder>();
        //------------------------------------------------------------------------------------------------------------

        // Diccionario de clave string (ThingID) y valor nuestro objeto con los datos
        private Dictionary<string, PawnInteractionInfo> interactionStates = new Dictionary<string, PawnInteractionInfo>();

        public ChatGameComponent(Game game) { }

        public List<string> GetChat(Pawn pawn)
        {
            string key = pawn.ThingID;
            if (!savedChats.ContainsKey(key))
                savedChats[key] = new List<string>();

            return savedChats[key];
        }

        // Add a line to the chat log, automatically inserting date separators when needed
        public void AddLine(Pawn pawn, string line)
        {
            string key = pawn.ThingID;
            if (!savedChats.ContainsKey(key))
                savedChats[key] = new List<string>();

            int currentDay = GenDate.DaysPassed;
            
            // Check if this is a new day since the last conversation
            if (!lastChatDay.ContainsKey(key) || lastChatDay[key] != currentDay)
            {
                // Add date separator
                string dateHeader = GetFormattedDateHeader(currentDay);
                savedChats[key].Add($"[DATE_SEPARATOR] {dateHeader}");
                lastChatDay[key] = currentDay;
            }

            savedChats[key].Add(line);
        }

        // Format a date header with robust error handling for all edge cases
        private string GetFormattedDateHeader(int day)
        {
            try
            {
                // CRITICAL FIX: Check if map exists before accessing it
                if (Find.CurrentMap == null)
                {
                    // Simple fallback when no map is available
                    int ticks = day * GenDate.TicksPerDay;
                    int year = GenDate.Year(ticks, 0f);
                    Quadrum quadrum = GenDate.Quadrum(ticks, 0f);
                    int dayOfSeason = GenDate.DayOfSeason(ticks, 0f);
                    return $"--- {quadrum.Label()} {dayOfSeason}, {year} ---";
                }
                
                // Use RimWorld's native date format (already fully localized)
                string nativeDate = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
                string[] parts = nativeDate.Split(' ');
                
                // CRITICAL FIX: Verify we have enough parts before accessing array indices
                if (parts.Length >= 6)
                {
                    string yearWithoutComma = parts[5].TrimEnd(',');
                    // Format: parts[0] = Day; parts[1] = "number day"; parts[2] = of; parts[3] = Quadrum; parts[4] = of; yearWithoutComma = year
                    string dateOnly = $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {parts[4]} {yearWithoutComma}";
                    return $"--- {dateOnly} ---";
                }
                else
                {
                    // If format is different, use the full string but remove the time
                    // Typically the time is at the end after a comma
                    int commaIndex = nativeDate.LastIndexOf(',');
                    string dateOnly = commaIndex > 0 ? nativeDate.Substring(0, commaIndex) : nativeDate;
                    return $"--- {dateOnly} ---";
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error formatting date header for day {day}: {ex.Message}");
                
                // CRITICAL FIX: Safe fallback that always works
                try
                {
                    int ticks = day * GenDate.TicksPerDay;
                    int year = GenDate.Year(ticks, 0f);
                    Quadrum quadrum = GenDate.Quadrum(ticks, 0f);
                    int dayOfSeason = GenDate.DayOfSeason(ticks, 0f);
                    return $"--- {quadrum.Label()} {dayOfSeason}, {year} ---";
                }
                catch
                {
                    // Last resort: just show the day number
                    return $"--- Day {day} ---";
                }
            }
        }

        // Clear chat history and date tracking for a specific pawn
        public void ClearChat(Pawn pawn)
        {
            string key = pawn.ThingID;
            if (savedChats.ContainsKey(key))
                savedChats[key].Clear();
            
            if (lastChatDay.ContainsKey(key))
                lastChatDay.Remove(key);

            CleanInteractionInfo(pawn);
        }

        // Save and load chat data, date tracking, and voice assignments
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedChats, "savedChats", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastChatDay, "lastChatDay", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pawnVoiceMap, "pawnVoiceMap", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref interactionStates, "interactionStates", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref groupChatHistory, "groupChatHistory", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (savedChats == null)
                    savedChats = new Dictionary<string, List<string>>();
                    
                if (lastChatDay == null)
                    lastChatDay = new Dictionary<string, int>();
                    
                if (pawnVoiceMap == null)
                    pawnVoiceMap = new Dictionary<string, string>();

                if (interactionStates == null)
                    interactionStates = new Dictionary<string, PawnInteractionInfo>();

                if (groupChatHistory == null) 
                    groupChatHistory = new Dictionary<string, GroupChatHistoryHolder>();
            }
        }

        // Assign a TTS voice to a specific pawn
        public void SetVoiceForPawn(Pawn pawn, string voiceId)
        {
            pawnVoiceMap[pawn.ThingID.ToString()] = voiceId;
        }

        // Retrieve the assigned TTS voice for a pawn
        public string GetVoiceForPawn(Pawn pawn)
        {
            return pawnVoiceMap.TryGetValue(pawn.ThingID.ToString(), out var voiceId) ? voiceId : null;
        }

        public void CleanupOrphanedChats()
        {
            // Obtenemos todos los IDs de peones que el juego aún reconoce (vivos, muertos en tumbas, etc.)
            var validPawnIDs = new HashSet<string>(
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                    .Where(p => p != null)
                    .Select(p => p.ThingID)
            );

            // Listas para identificar qué claves borrar
            List<string> keysToRemove = new List<string>();

            foreach (var key in savedChats.Keys)
            {
                // Si el ID del chat no está en la lista de peones válidos del juego, marcar para borrar
                if (!validPawnIDs.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            // Ejecutar la eliminación
            foreach (var key in keysToRemove)
            {
                savedChats.Remove(key);
                lastChatDay.Remove(key);
                if (pawnVoiceMap.ContainsKey(key)) pawnVoiceMap.Remove(key);
                if (interactionStates.ContainsKey(key)) interactionStates.Remove(key);
                // if (lastInteractionTicks.ContainsKey(key)) lastInteractionTicks.Remove(key);
                if (groupChatHistory.ContainsKey(key)) groupChatHistory.Remove(key);

                Log.Message($"[EchoColony] Cleaned up orphaned chat data for Pawn ID: {key} (Pawn no longer exists in world)");
            }
        }

        // Automatically executed when loading a saved game
        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Limpiar chats de peones inexistentes al cargar
            CleanupOrphanedChats();

            // Restore TTS voice assignments if TTS is enabled
            if (MyMod.Settings.enableTTS)
            {
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
                {
                    if (ColonistVoiceManager.HasVoice(pawn))
                    {
                        string savedVoice = ColonistVoiceManager.GetVoice(pawn);
                        if (!string.IsNullOrEmpty(savedVoice))
                        {
                            SetVoiceForPawn(pawn, savedVoice);
                        }
                    }
                }
            }
        }

        //Time Tracker-------------------------------------------------------

        public PawnInteractionInfo GetInteractionInfo(Pawn pawn)
        {
            if (pawn == null) return null;

            if (!interactionStates.TryGetValue(pawn.ThingID, out var info))
            {
                info = new PawnInteractionInfo { LastTick = 0, StartTurn = 0 };
                interactionStates[pawn.ThingID] = info;
            }

            return info;
        }

        /// <summary>
        /// Stores the tick and turnof the interaction and the first valid turn.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="currentTurn"></param>
        public void RegisterInteraction(Pawn pawn, int currentTurn)
        {
            if (pawn == null) return;
            var info = GetInteractionInfo(pawn);

            info.LastTick = Find.TickManager.TicksGame;
            info.CurrentTurn = currentTurn;

            // Si es la primera interacción de una nueva sesión/bloque, fijamos el turno inicial
            if (info.StartTurn == 0)
            {
                info.StartTurn = currentTurn;
            }
        }

        /// <summary>
        /// Updates just the start turn.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="newStartTurn"></param>
        public void UpdateStartTurn(Pawn pawn, int newStartTurn)
        {
            var info = GetInteractionInfo(pawn);
            if (info != null)
            {
                info.StartTurn = newStartTurn;
            }
        }

        // Limpiar interacción (al resetear chat)
        /// <summary>
        /// Clean stored Tick and turn.
        /// </summary>
        /// <param name="pawn"></param>
        public void CleanInteractionInfo(Pawn pawn)
        {
            if (pawn == null) return;
            if (interactionStates.ContainsKey(pawn.ThingID))
            {
                interactionStates[pawn.ThingID].LastTick = 0;
                interactionStates[pawn.ThingID].StartTurn = 0;
                interactionStates[pawn.ThingID].CurrentTurn =0;
                interactionStates[pawn.ThingID].LastSavedTurnCount = 0;
            }
        }

        /// <summary>
        /// Check if the session has expired due to exceeding the time limit without speaking.
        /// </summary>
        /// <param name="pawn">El colono a comprobar.</param>
        /// <param name="timeoutTicks">Tiempo límite en ticks. Por defecto: 5 horas de juego (12,500 ticks).</param>
        public bool ShouldResetSession(Pawn pawn, int timeoutTicks = -1)
        {
            if (pawn == null) return false;

            // Si no se pasa un timeoutTicks explícito, usamos la configuración del mod
            if (timeoutTicks <= 0)
            {
                int hours = MyMod.Settings != null ? MyMod.Settings.chatSessionTimeoutHours : 5;

                // 0 = Tiempo ilimitado (nunca expira la sesión)
                if (hours <= 0) return false;

                timeoutTicks = hours * GenDate.TicksPerHour;
            }

            int lastTick = GetInteractionInfo(pawn).LastTick;
            if (lastTick <= 0) return false; // Primera conversación, no expira

            int currentTick = Find.TickManager.TicksGame;
            return (currentTick - lastTick) > timeoutTicks;
        }

        /// <summary>
        /// Updates the tick of the game in which the last interaction with the pawn occurred.
        /// </summary>
        /// <param name="pawn"></param>
        public void UpdateInteractionTick(Pawn pawn)
        {
            if (pawn == null) return;
            var info = GetInteractionInfo(pawn);
            info.LastTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Updates the current turn of the conversation for the pawn.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="turn"></param>
        public void UpdateConversationTurn(Pawn pawn, int turn)
        {
            interactionStates[pawn.ThingID].CurrentTurn = turn;
        }

        //furel - Modified method to retrieve recent chat lines, skipping the last N lines to avoid including the current prompt and placeholder.
        /// <summary>
        /// Gets the recent lines of chat for a pawn, skipping the last N lines (default 2) to avoid including the current prompt and placeholder.
        /// The lines are skipped if time has passed since the last interaction, based on the session timeout settings.
        /// </summary>
        /// <param name="pawn">The pawn for whom to retrieve chat lines.</param>
        /// <param name="skipLastLines">The number of lines to skip from the end (default: 2).</param>
        /// <returns>A list of recent chat lines. None if time has passed since the last interaction.</returns>
        public List<String> GetRecentLines(Pawn pawn, int skipLastLines = 2)
        {
            var lines = GetChat(pawn);
            var info = GetInteractionInfo(pawn);

            //furel - We calculate the number of turns since the start of the session to determine how many lines to retrieve.
            int rTurn = Math.Max(0, info.CurrentTurn - info.StartTurn);
            if (rTurn <= 0)
                return new List<string>();

            int linesToTake = Math.Min(rTurn * 2, 14);

            // 3. Filtramos los separadores de fecha y tomamos los últimos N mensajes convirtiendo a List
            var filtered = lines
                .Where(line => !line.StartsWith("[DATE_SEPARATOR]"))
                .ToList();

            // Si hay líneas suficientes, ignoramos las 2 últimas (prompt actual y placeholder)
            int validCount = Math.Max(0, filtered.Count - skipLastLines);
            var available = filtered.Take(validCount);

            if (linesToTake <= 0)
                return new List<string>();


            return available.TakeLast(linesToTake).ToList();
        }

        /// <summary>
        /// Updates the counter of turns processed and stored in memory for the pawn.
        /// </summary>
        public void UpdateLastSavedTurn(Pawn pawn, int turn)
        {
            if (pawn == null) return;
            var info = GetInteractionInfo(pawn);
            if (info != null)
            {
                info.LastSavedTurnCount = turn;
            }
        }

        //*furel- New methods for manage group chat history and participants.
        /// <summary>
        /// Registers a new group conversation for a pawn.
        /// </summary>
        public void RegisterGroupConversation(Pawn pawn, IEnumerable<string> otherParticipants)
        {
            if (pawn == null || otherParticipants == null) return;

            // Convertimos IEnumerable a List y filtramos cadenas nulas o vacías
            var participantsList = otherParticipants
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name)
                .ToList();

            if (participantsList.Count == 0) return;

            string key = pawn.ThingID;

            // TryGetValue realiza una sola búsqueda en el diccionario
            if (!groupChatHistory.TryGetValue(key, out var history))
            {
                history = new GroupChatHistoryHolder();
                groupChatHistory[key] = history;
            }

            if (history.Records == null)
                history.Records = new List<GroupChatRecord>();

            var lastRecord = history.Records.LastOrDefault();
            if (lastRecord != null && lastRecord.ParticipantNames.SequenceEqual(participantsList))
            {
                return; // Mismo grupo consecutivo, se ignora la duplicación
            }
            history.Records.Add(new GroupChatRecord(participantsList));
        }

        /// <summary>
        /// Retrieves the group history in order of participation without modifying the dictionary.
        /// </summary>
        public List<GroupChatRecord> GetGroupHistory(Pawn pawn)
        {
            if (pawn != null && groupChatHistory.TryGetValue(pawn.ThingID, out var history))
            {
                return history.Records ?? new List<GroupChatRecord>();
            }

            return new List<GroupChatRecord>();
        }

        /// <summary>
        /// Clears and removes the group record for the specified pawn to free up memory.
        /// </summary>
        public void ClearGroupHistory(Pawn pawn)
        {
            if (pawn == null) return;

            groupChatHistory.Remove(pawn.ThingID);
        }

    }
}