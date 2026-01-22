using Verse;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace EchoColony
{
    public class ChatGameComponent : GameComponent
    {
        private Dictionary<string, List<string>> savedChats = new Dictionary<string, List<string>>();
        public static ChatGameComponent Instance => Current.Game.GetComponent<ChatGameComponent>();

        private Dictionary<string, string> pawnVoiceMap = new Dictionary<string, string>();
        
        // Track the last day a chat occurred for each pawn to insert date separators
        private Dictionary<string, int> lastChatDay = new Dictionary<string, int>();

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
            {
                savedChats[key].Clear();
            }
            
            // Also clear date tracking
            if (lastChatDay.ContainsKey(key))
            {
                lastChatDay.Remove(key);
            }
        }

        // Save and load chat data, date tracking, and voice assignments
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedChats, "savedChats", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastChatDay, "lastChatDay", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pawnVoiceMap, "pawnVoiceMap", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (savedChats == null)
                    savedChats = new Dictionary<string, List<string>>();
                    
                if (lastChatDay == null)
                    lastChatDay = new Dictionary<string, int>();
                    
                if (pawnVoiceMap == null)
                    pawnVoiceMap = new Dictionary<string, string>();
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

        // Automatically executed when loading a saved game
        public override void FinalizeInit()
        {
            base.FinalizeInit();

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
    }
}