using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public class ColonistMemoryTracker : IExposable
    {
        private Dictionary<int, string> memories = new Dictionary<int, string>();
        private Pawn pawn; // Reference for logging
        public Pawn Pawn => this.pawn;
        private List<RawInteraction> currentDayInteractions = new List<RawInteraction>(); //*furel - new memory system* New component, currentDayInteraction has to stack just one day of memories.

        // Constructor without parameters (required for RimWorld serialization)
        public ColonistMemoryTracker()
        {
            this.pawn = null;
            this.currentDayInteractions = new List<RawInteraction>(); 
        }

        // Constructor to assign the pawn
        public ColonistMemoryTracker(Pawn pawn)
        {
            this.pawn = pawn;
            this.currentDayInteractions = new List<RawInteraction>();
        }

        /// <summary>
        /// Updates memory for a specific day (used by memory viewer)
        /// </summary>
        public void UpdateMemory(int day, string newText)
        {
            if (memories.ContainsKey(day))
            {
                memories[day] = newText;
                Log.Message($"[EchoColony] Memory for day {day} updated locally for {pawn?.LabelShort ?? "Unknown"}");
            }
            else
            {
                Log.Warning($"[EchoColony] Tried to update non-existent memory for day {day} for {pawn?.LabelShort ?? "Unknown"}");
            }
        }

        private string GetCurrentDateHeader()
        {
            try
            {
                long ticks = GenTicks.TicksAbs;
                Vector2 location = (pawn != null && pawn.Tile >= 0)
                    ? Find.WorldGrid.LongLatOf(pawn.Tile)
                    : Find.WorldGrid.LongLatOf(Find.CurrentMap?.Tile ?? 0);

                string nativeDate = GenDate.DateFullStringWithHourAt(ticks, location);

                // ✅ VALIDACIÓN SEGURA: Verificar que tenemos suficientes partes
                string[] parts = nativeDate.Split(' ');
                if (parts.Length >= 6)
                {
                    string yearWithoutComma = parts[5].TrimEnd(',');
                    return $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {parts[4]} {yearWithoutComma}";
                }
                else
                {
                    // Fallback: devolver la fecha completa si el formato es diferente
                    Log.Warning($"[EchoColony] Unexpected date format: {nativeDate}");
                    return nativeDate;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error getting date header: {ex.Message}");
                return $"Day {GenDate.DaysPassed}";
            }
        }

        //*furel - new memory system*
        /// <summary>
        /// It retrieves all interactions from the current day formatted into a single string for the prompt.
        /// </summary>
        public string GetCurrentDayMemoryFormatted()
        {
            if (currentDayInteractions == null || !currentDayInteractions.Any())
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var interaction in currentDayInteractions)
            {
                if (interaction != null && !string.IsNullOrWhiteSpace(interaction.text))
                {
                    sb.AppendLine($"- {interaction.ToPromptFormat()}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns the raw list of interactions for the current day in read-only mode.
        /// </summary>
        public IReadOnlyList<RawInteraction> GetCurrentDayInteractions()
        {
            return (IReadOnlyList<RawInteraction>)currentDayInteractions ?? System.Array.Empty<RawInteraction>();
        }

        //*furel - Memory system*
        /// <summary>
        /// Builds the string from the raw interactions to be added to the prompt to create the day memory. 
        /// </summary>
        /// <param name="dayToProcess">The day which is gonna be processed</param>
        public void ProcessEndOfDay(int dayToProcess)
        {
            if (currentDayInteractions == null || currentDayInteractions.Count == 0)
            {
                Log.Message($"[EchoColony] No raw interactions to summarize for {pawn?.LabelShort ?? "Unknown"} on day {dayToProcess}.");
                return;
            }

            // We're compiling all the raw data collected today
            string rawJoinedText = string.Join("\n", currentDayInteractions);

            // Important: We immediately clear the buffer so that the new day starts fresh 
            // and to prevent data duplication if the AI callback takes a while.
            ClearDayMemBuffer();

            Log.Message($"[EchoColony] Triggering AI summary for {pawn?.LabelShort ?? "Unknown"} (Day {dayToProcess}) with {rawJoinedText.Length} chars of raw logs.");

            // We send the block of raw text to the AI to generate the final daily summary
            SummarizeRawDayWithAI(dayToProcess, rawJoinedText);
        }

        //*furel - improvement* Specific language.
        /// <summary>
        /// Sends the raw interactions of a specific day to the AI for summarization and saves the result in the memories dictionary.
        /// </summary>
        /// <param name="day"></param>
        /// <param name="rawInteractions"></param>
        private void SummarizeRawDayWithAI(int day, string rawInteractions)
        {
            if (pawn == null)
            {
                Log.Warning($"[EchoColony] Cannot summarize day {day}: Pawn is null.");
                return;
            }
            string safeRawInteractions = rawInteractions ?? string.Empty;
            string dateHeader = GetCurrentDateHeader();

            string promptText = ColonistMemoryPromptBuilder.BuildDailyMemoryPrompt(pawn, day, safeRawInteractions);


            System.Action<string> summaryCallback = (aiSummary) =>
            {

                if (!string.IsNullOrWhiteSpace(aiSummary))
                {
                    // We save the optimized summary in the historical entry for the processed day
                    memories[day] = $"[{dateHeader}]\n{aiSummary.Trim()}";

                    Log.Message($"[EchoColony] Saved permanent AI summary for {pawn?.LabelShort} for day {day}");
                }
                else
                {
                    // Emergency fallback if the AI fails due to a timeout or network error:
                    // We save a truncated excerpt so that the information isn't lost entirely
                    string fallbackText = rawInteractions.Length > 300 ? rawInteractions.Substring(0, 300) + "..." : rawInteractions;
                    memories[day] = $"[{dateHeader}]\n[Automatic Summary Failed - Raw Record]:\n{fallbackText}";
                    Log.Warning($"[EchoColony] AI summary failed or returned empty for {pawn?.LabelShort} day {day}. Fallback saved.");
                }
            };

            try
            {
                GenerateOptimizedMemory(promptText, $"DAILY_SUMMARY_{GeminiAPI.CleanNameForFileName(pawn?.LabelShort)}", summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error generating daily summary via AI: {ex.Message}");
                memories[day] = $"[{dateHeader}]\n{rawInteractions}";
            }
        }

        /// <summary>
        /// Directly sets or overwrites a day's raw data without AI processing or date recalculation.
        /// Designed for JSON file imports, backup restores, and debugging.
        /// </summary>
        public void SetMemoryForDay(int day, string fullMemoryText)
        {
            if (string.IsNullOrWhiteSpace(fullMemoryText)) return;

            memories[day] = fullMemoryText;
        }

        /// <summary>
        /// Sends a request to optimize edited memories using AI
        /// </summary>
        public void OptimizeCustomMemoryWithAI(int day, string editedMem, Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(editedMem)) return;

            // Get date header
            string dateHeader = GetCurrentDateHeader();

            // Build prompt
            string prompt = ColonistMemoryPromptBuilder.BuildCustomMemoryWithAI(pawn, editedMem);

            // Callback to save result
            System.Action<string> summaryCallback = (aiResponse) =>
            {
                if (!string.IsNullOrWhiteSpace(aiResponse))
                {
                    memories[day] = $"[{dateHeader}]\n{aiResponse.Trim()}";
                    Log.Message($"[EchoColony] AI rewrote the memory of day {day} for {pawn?.LabelShort}");
                }
            };

            // Send to AI
            try
            {
                GenerateOptimizedMemory(prompt, $"EDITED_MEMORY_OPTIMIZATION_{GeminiAPI.CleanNameForFileName(pawn?.LabelShort)}", summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error in OptimizeCustomMemoryWithAI: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates optimized memory using the configured AI model
        /// </summary>
        private void GenerateOptimizedMemory(string prompt, string type, System.Action<string> callback)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Error("[EchoColony] MyStoryModComponent.Instance is null, cannot optimize memory");
                callback?.Invoke("");
                return;
            }

            bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                            MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

            bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                              MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

            IEnumerator memoryCoroutine;

            if (isKobold)
            {
                string koboldPrompt = KoboldPromptBuilder.Build(pawn, prompt);
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, callback);
                Log.Message("[EchoColony] Optimizing memory with KoboldAI");
            }
            else if (isLMStudio)
            {
                string lmPrompt = LMStudioPromptBuilder.Build(pawn, prompt);
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, callback);
                Log.Message("[EchoColony] Optimizing memory with LMStudio");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(prompt, callback);
                Log.Message("[EchoColony] Optimizing memory with local model");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                memoryCoroutine = GeminiAPI.SendRequestToPlayer2WithPrompt(prompt, callback, $"{type}");
                Log.Message("[EchoColony] Optimizing memory with Player2");
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(prompt, callback);
                Log.Message("[EchoColony] Optimizing memory with OpenRouter");
            }
            else // Gemini (default)
            {
                // For Gemini, need to create appropriate JSON
                var tempHistory = new List<GeminiMessage>
                {
                    new GeminiMessage("user", prompt)
                };
                string jsonPrompt = BuildGeminiChatJson(tempHistory);
                memoryCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, callback);
                Log.Message("[EchoColony] Optimizing memory with Gemini");
            }

            if (memoryCoroutine != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(memoryCoroutine);
            }
            else
            {
                Log.Error("[EchoColony] Could not create coroutine to optimize memory");
                callback?.Invoke("");
            }
        }

        /// <summary>
        /// Class for Gemini messages (local to avoid dependencies)
        /// </summary>
        public class GeminiMessage
        {
            public string role;
            public string content;

            public GeminiMessage(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        /// <summary>
        /// Helper to build Gemini JSON
        /// </summary>
        private string BuildGeminiChatJson(List<GeminiMessage> history)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"contents\": [");

            for (int i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                string role = msg.role == "model" ? "model" : "user";
                string text = EscapeJson(msg.content);

                sb.Append($"{{\"role\": \"{role}\", \"parts\": [{{\"text\": \"{text}\"}}]}}");

                if (i < history.Count - 1)
                    sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Helper to escape JSON
        /// </summary>
        private static string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        /// <summary>
        /// Gets memory for a specific day
        /// </summary>
        public string GetMemoryForDay(int day)
        {
            string result;
            return memories.TryGetValue(day, out result) ? result : null;
        }

        /// <summary>
        /// Removes memory for a specific day
        /// </summary>
        public bool RemoveMemoryForDay(int day)
        {
            if (memories.ContainsKey(day))
            {
                memories.Remove(day);
                Log.Message($"[EchoColony] Memory for day {day} removed for {pawn?.LabelShort ?? "Unknown"}");
                return true;
            }
            else
            {
                Log.Warning($"[EchoColony] No memory found for day {day} to remove for {pawn?.LabelShort ?? "Unknown"}");
                return false;
            }
        }

        /// <summary>
        /// Gets all memories of the colonist
        /// </summary>
        public Dictionary<int, string> GetAllMemories()
        {
            return new Dictionary<int, string>(memories);
        }
        //*furel - new memory system* Order changed, from most recent to oldest, to oldest to newest.
        /// <summary>
        /// Gets the last N memories, ordered by day (oldest first)
        /// </summary>
        public List<string> GetLastMemories(int count = 10)
        {
            List<string> recentMemories = new List<string>();

            List<int> sortedDays = new List<int>(memories.Keys);
            sortedDays.Sort((a, b) => b.CompareTo(a));

            List<int> targetDays = new List<int>();
            for (int i = 0; i < sortedDays.Count && i < count; i++)
            {
                targetDays.Add(sortedDays[i]);
            }

            targetDays.Reverse();

            foreach (int day in targetDays)
            {
                recentMemories.Add(memories[day]);
            }

            return recentMemories;
        }

        /// <summary>
        /// Gets memories from the last N days
        /// </summary>
        public List<string> GetRecentMemories(int lastNDays = 7)
        {
            int currentDay = GenDate.DaysPassed;
            List<string> recentMemories = new List<string>();

            foreach (var kvp in memories)
            {
                int day = kvp.Key;
                if (currentDay - day <= lastNDays)
                {
                    recentMemories.Add(kvp.Value);
                }
            }

            // Sort by day (most recent first)
            recentMemories = recentMemories
                .OrderByDescending(m => ExtractDayFromMemory(m))
                .ToList();

            return recentMemories;
        }

        /// <summary>
        /// Extracts day number from a formatted memory
        /// </summary>
        private int ExtractDayFromMemory(string memory)
        {
            // Look in memories.Keys for the memory that matches
            foreach (var kvp in memories)
            {
                if (kvp.Value == memory)
                    return kvp.Key;
            }
            return 0; // Fallback
        }

        /// <summary>
        /// Removes all memories of the colonist
        /// </summary>
        public void ClearAllMemories()
        {
            int count = memories.Count;
            memories.Clear();
            ChatGameComponent.Instance.ClearGroupHistory(pawn);
            Log.Message($"[EchoColony] {count} memories removed for {pawn?.LabelShort ?? "Unknown"}");
        }

        /// <summary>
        /// Removes memories older than a specific date
        /// </summary>
        public void ClearOldMemories(int keepLastNDays = 30)
        {
            int currentDay = GenDate.DaysPassed;
            var keysToRemove = new List<int>();

            foreach (var day in memories.Keys)
            {
                if (currentDay - day > keepLastNDays)
                    keysToRemove.Add(day);
            }

            foreach (var key in keysToRemove)
            {
                memories.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                Log.Message($"[EchoColony] {keysToRemove.Count} old memories removed for {pawn?.LabelShort ?? "Unknown"}");
            }
        }

        /// <summary>
        /// Gets the day of the most recent memory
        /// </summary>
        public int GetLastMemoryDay()
        {
            if (memories == null || memories.Count == 0) return -1;
            return memories.Keys.Max();
        }

        /// <summary>
        /// Gets memory statistics
        /// </summary>
        public (int total, int individual, int grupal, int recent) GetMemoryStats()
        {
            int total = memories.Count;
            int individual = 0;
            int grupal = 0;
            int recent = 0;
            int currentDay = GenDate.DaysPassed;

            foreach (var memory in memories.Values)
            {
                // Count types
                if (memory.StartsWith("[Conversación grupal") || memory.Contains("conversación grupal"))
                    grupal++;
                else
                    individual++;
            }

            // Count recent (last 7 days)
            foreach (var day in memories.Keys)
            {
                if (currentDay - day <= 7)
                    recent++;
            }

            return (total, individual, grupal, recent);
        }

        /// <summary>
        /// Searches for memories containing specific text
        /// </summary>
        public List<(int day, string memory)> SearchMemories(string searchText)
        {
            var results = new List<(int day, string memory)>();

            if (string.IsNullOrWhiteSpace(searchText))
                return results;

            string searchLower = searchText.ToLowerInvariant();

            foreach (var kvp in memories)
            {
                if (kvp.Value.ToLowerInvariant().Contains(searchLower))
                {
                    results.Add((kvp.Key, kvp.Value));
                }
            }

            return results.OrderByDescending(r => r.day).ToList();
        }

        /// <summary>
        /// Debug: Prints all memories in logs
        /// </summary>
        public void PrintAllMemories()
        {
            Log.Message($"[EchoColony] === MEMORIES OF {pawn?.LabelShort ?? "Unknown"} ===");
            Log.Message($"[EchoColony] Total: {memories.Count} memories");

            foreach (var kvp in memories.OrderByDescending(m => m.Key))
            {
                int day = kvp.Key;
                string memory = kvp.Value;
                string preview = memory.Length > 100 ? memory.Substring(0, 100) + "..." : memory;
                string type = memory.StartsWith("[Conversación grupal") ? "GROUP" : "INDIVIDUAL";

                Log.Message($"[EchoColony] Day {day} ({type}): {preview}");
            }

            Log.Message($"[EchoColony] === END MEMORIES ===");
        }

        /// <summary>
        /// Registra una conversación directa con el jugador.
        /// </summary>
        public void RecordPlayerInteraction(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            currentDayInteractions.Add(new RawInteraction(InteractionType.Player, text));
        }

        /// <summary>
        /// Registra una conversación grupal especificando los participantes.
        /// </summary>
        public void RecordGroupInteraction(string text, IEnumerable<string> participantNames)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            List<string> participantsList = participantNames != null
                    ? participantNames.ToList()
                    : new List<string>();

            currentDayInteractions.Add(new RawInteraction(InteractionType.Group, text, participantsList));
        }

        public void RecordSmallTalkInteraction(string text, string otherColonistName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var participants = new List<string>();
            if (!string.IsNullOrWhiteSpace(otherColonistName))
                participants.Add(otherColonistName);

            currentDayInteractions.Add(new RawInteraction(InteractionType.SmallTalk, text, participants));
        }

        /// <summary>
        /// Registra un evento de la colonia o acción individual del colono.
        /// </summary>
        public void RecordEventInteraction(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            currentDayInteractions.Add(new RawInteraction(InteractionType.Event, text));
        }

        // FIXED: Proper ExposeData implementation
        public void ExposeData()
        {
            
            Scribe_Collections.Look(ref memories, "memories", LookMode.Value, LookMode.Value);

            //*furel - memory system* save the imputs.
            Scribe_Collections.Look(ref currentDayInteractions, "currentDayInteractions", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {

                if (memories == null)
                {
                    memories = new Dictionary<int, string>();
                    Log.Warning($"[EchoColony] memories was null after loading for {pawn?.LabelShort ?? "Unknown"}, initialized empty");
                }
                else
                {
                    Log.Message($"[EchoColony] Loaded {memories.Count} memories for {pawn?.LabelShort ?? "Unknown"}");
                    
                    if (memories.Count > 0)
                    {
                        var sortedDays = memories.Keys.OrderBy(d => d).ToList();
                        Log.Message($"[EchoColony]   Days range: {sortedDays.First()} to {sortedDays.Last()}");
                        
                        // Show first memory as sample
                        var firstDay = sortedDays.First();
                        var preview = memories[firstDay].Length > 50 
                            ? memories[firstDay].Substring(0, 50) + "..." 
                            : memories[firstDay];
                        Log.Message($"[EchoColony]   Sample (day {firstDay}): {preview}");
                    }
                }
                //*furel - new memory system* initialization of currenDayInteraction after loading
                if (currentDayInteractions == null)
                {
                    currentDayInteractions = new List<RawInteraction>();
                }
            }

            //Log while saving
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Log.Message($"[EchoColony] Saving {memories?.Count ?? 0} memories for {pawn?.LabelShort ?? "Unknown"}");
                
                if (memories != null && memories.Count > 0)
                {
                    var sortedDays = memories.Keys.OrderBy(d => d).ToList();
                    Log.Message($"[EchoColony]   Days being saved: {sortedDays.First()} to {sortedDays.Last()}");
                }
            }
        }

        /// <summary>
        /// Assigns the pawn reference (useful after loading)
        /// </summary>
        public void SetPawn(Pawn pawn)
        {
            this.pawn = pawn;
        }

        //*furel - memory system* Clean memory day buffer
        public void ClearDayMemBuffer()
        {
            currentDayInteractions.Clear();
        }

    }

    /// <summary>
    /// Defines the type of interaction for memory tracking.
    /// </summary>
    public enum InteractionType
    {
        Player,
        Group,
        SmallTalk,
        Event
        

    }

    public class RawInteraction : IExposable
    {
        public InteractionType type;
        public string text;
        public List<string> participants = new List<string>();

        // Constructor sin parámetros necesario para Scribe
        public RawInteraction() { }

        public RawInteraction(InteractionType type, string text, List<string> participants = null)
        {
            this.type = type;
            this.text = text;
            this.participants = participants ?? new List<string>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref text, "text");
            Scribe_Collections.Look(ref participants, "participants", LookMode.Value);
        }

        /// <summary>
        /// Generates the label format needed only when sending to the AI prompt
        /// </summary>
        public string ToPromptFormat()
        {
            switch (type)
            {
                case InteractionType.Group:
                    string others = participants.Count > 0 ? string.Join(", ", participants) : "others";
                    return $"[Group conversation with {others}]: {text}";

                case InteractionType.Player:
                    return $"[Player Conversation]: {text}";
                
                case InteractionType.SmallTalk:
                    string targetPawn = participants.FirstOrDefault() ?? "another colonist";
                    return $"[Conversation with {targetPawn}]: {text}";

                case InteractionType.Event:
                default:
                    return text;
            }
        }

    }
}