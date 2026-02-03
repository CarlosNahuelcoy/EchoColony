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

        // Constructor without parameters (required for RimWorld serialization)
        public ColonistMemoryTracker()
        {
            this.pawn = null;
        }

        // Constructor to assign the pawn
        public ColonistMemoryTracker(Pawn pawn)
        {
            this.pawn = pawn;
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

        // Calculate simple edit distance between two strings
        private int CalculateEditDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1?.Length ?? 0;

            int[,] dp = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                dp[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                dp[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            }

            return dp[s1.Length, s2.Length];
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
        /// <summary>
        /// Improved: Saves optimized memory using AI to summarize when there's existing content
        /// CRITICAL: Only combines with AI when there's genuinely NEW content
        /// </summary>
        public void SaveMemoryForDay(int day, string newSummary)
{
    try
    {
        if (string.IsNullOrWhiteSpace(newSummary))
        {
            Log.Warning($"[EchoColony] Attempt to save empty memory for {pawn?.LabelShort ?? "Unknown"} day {day}");
            return;
        }

        string fechaSinHora = GetCurrentDateHeader();

        // If memory already exists for this day, validate before combining
        if (memories.ContainsKey(day))
        {
            string existingMemory = memories[day];

            // ✅ VALIDACIÓN SEGURA: Verificar que el string tiene contenido antes de substring
            string existingContent = "";
            if (!string.IsNullOrEmpty(existingMemory))
            {
                int separatorIndex = existingMemory.IndexOf("]\n");
                if (separatorIndex >= 0 && separatorIndex + 2 < existingMemory.Length)
                {
                    existingContent = existingMemory.Substring(separatorIndex + 2);
                }
                else
                {
                    existingContent = existingMemory;
                }
            }

            // VALIDATION 1: Identical content
            if (existingContent.Trim().Equals(newSummary.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Log.Message($"[EchoColony] Identical memory detected for {pawn?.LabelShort ?? "Unknown"} day {day}, skipping");
                return;
            }

            // VALIDATION 2: New content already included
            if (existingContent.Length > newSummary.Length && 
                existingContent.Contains(newSummary, StringComparison.OrdinalIgnoreCase))
            {
                Log.Message($"[EchoColony] New content already included in existing memory for {pawn?.LabelShort ?? "Unknown"} day {day}, skipping");
                return;
            }

            // VALIDATION 3: Content too short
            if (newSummary.Length < 30)
            {
                Log.Message($"[EchoColony] New content too short ({newSummary.Length} chars) for {pawn?.LabelShort ?? "Unknown"} day {day}, skipping");
                return;
            }

            // Check for minimal changes
            int editDistance = CalculateEditDistance(existingContent.Trim(), newSummary.Trim());
            if (editDistance < 10)
            {
                Log.Message($"[EchoColony] Minor change ({editDistance} chars) for {pawn?.LabelShort ?? "Unknown"} day {day}, updating directly");
                memories[day] = $"[{fechaSinHora}]\n{newSummary}";
                return;
            }

            // ✅ VALIDACIÓN SEGURA: Truncar solo si el string es lo suficientemente largo
            string newContentSample = newSummary.Length > 50 ? newSummary.Substring(0, 50) : newSummary;
            if (existingContent.ToLowerInvariant().Contains(newContentSample.ToLowerInvariant()))
            {
                Log.Message($"[EchoColony] Similar memory already exists for {pawn?.LabelShort ?? "Unknown"} day {day}, skipping");
                return;
            }

            // Check percentage of similarity
            double similarity = 1.0 - (double)editDistance / Math.Max(existingContent.Length, newSummary.Length);
            if (similarity > 0.85)
            {
                Log.Message($"[EchoColony] High similarity ({similarity:P0}) detected for {pawn?.LabelShort ?? "Unknown"} day {day}, updating directly");
                memories[day] = $"[{fechaSinHora}]\n{newSummary}";
                return;
            }

            // VALIDATION 4: Existing content much longer (probably viewing)
            if (existingContent.Length > newSummary.Length * 2)
            {
                Log.Message($"[EchoColony] Existing content ({existingContent.Length} chars) much longer than new ({newSummary.Length} chars), likely viewing - skipping");
                return;
            }

            Log.Message($"[EchoColony] Combining memories for {pawn?.LabelShort ?? "Unknown"} day {day} using AI (existing: {existingContent.Length} chars, new: {newSummary.Length} chars)");

            // Use AI to create optimized summary
            CombineMemoriesWithAI(day, existingContent, newSummary, fechaSinHora);
        }
        else
        {
            // First memory of the day
            memories[day] = $"[{fechaSinHora}]\n{newSummary}";
            Log.Message($"[EchoColony] New memory saved for {pawn?.LabelShort ?? "Unknown"} day {day} ({newSummary.Length} chars)");
        }
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Log.Error($"[EchoColony] Index out of range error saving memory for {pawn?.LabelShort ?? "Unknown"} day {day}: {ex.Message}");
        Log.Error($"[EchoColony] Memory length: {newSummary?.Length ?? 0}, Stack: {ex.StackTrace}");
        
        // Fallback: guardar directamente sin procesamiento
        string fechaSinHora = GetCurrentDateHeader();
        memories[day] = $"[{fechaSinHora}]\n{newSummary ?? "Error: empty summary"}";
    }
    catch (Exception ex)
    {
        Log.Error($"[EchoColony] Unexpected error saving memory for {pawn?.LabelShort ?? "Unknown"} day {day}: {ex.Message}");
        Log.Error($"[EchoColony] Stack: {ex.StackTrace}");
    }
}
        /// <summary>
        /// Combines memories using AI to create a unique and optimized summary
        /// </summary>
        private void CombineMemoriesWithAI(int day, string existingContent, string newContent, string dateHeader)
        {
            string combinedInput = $"Memoria existente del día:\n{existingContent}\n\nNueva información:\n{newContent}";

            string promptForSummary = "Combine these two memories from the same day into a single unified and natural memory. " +
                         "Keep all important events but write as if it were a single coherent experience of the day. " +
                         "Avoid redundancies and maintain a personal and intimate tone. Don't use phrases like 'New entry' or 'Additionally'. " +
                         "Maximum 200 words.";

            string fullPrompt = promptForSummary + "\n\n" + combinedInput;

            // Callback to handle AI response
            System.Action<string> summaryCallback = (aiSummary) =>
            {
                if (string.IsNullOrWhiteSpace(aiSummary))
                {
                    // Fallback: simple combination without AI
                    Log.Warning($"[EchoColony] AI returned empty summary, using simple combination for {pawn?.LabelShort ?? "Unknown"}");
                    memories[day] = $"[{dateHeader}]\n{existingContent} {newContent}";
                }
                else
                {
                    // Use AI-generated summary
                    string cleanedSummary = aiSummary.Trim();
                    memories[day] = $"[{dateHeader}]\n{cleanedSummary}";
                    Log.Message($"[EchoColony] Memory optimized by AI for {pawn?.LabelShort ?? "Unknown"} day {day} (result: {cleanedSummary.Length} chars)");
                }
            };

            // Send request to AI using configured model
            try
            {
                GenerateOptimizedMemory(fullPrompt, summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error generating optimized memory: {ex.Message}");
                // Fallback: simple combination
                memories[day] = $"[{dateHeader}]\n{existingContent} {newContent}";
            }
        }

        /// <summary>
        /// Generates a prompt for individual memories using AI
        /// </summary>
        public void OptimizeCustomMemoryWithAI(int day, string editedMem)
        {
            if (string.IsNullOrWhiteSpace(editedMem)) return;

            // Get date header
            string dateHeader = GetCurrentDateHeader();

            // Get complete colonist context
            string systemIdentity = ColonistPromptContextBuilder.BuildSystemPromptPublic(pawn);
            string currentContext = ColonistPromptContextBuilder.BuildContextPublic(pawn);

            // Build prompt
            StringBuilder prompt = new StringBuilder();
            prompt.AppendLine(systemIdentity);
            prompt.AppendLine("\n### CURRENT CONTEXT ###");
            prompt.AppendLine(currentContext);

            prompt.AppendLine("\n### TASK ###");
            prompt.AppendLine("Act like the colonist mentioned. Your user has written a draft of their memory.");
            prompt.AppendLine("REWRITE the text in FIRST PERSON so that it looks like a real personal diary.");
            prompt.AppendLine("- Use language that is appropriate to your features and health status.");
            prompt.AppendLine("- Keep the facts from the draft but make it flow smoothly.");
            prompt.AppendLine("- Do NOT include metatext or introductions.");
            prompt.AppendLine("- Maximum 200 words.");

            prompt.AppendLine("\n### DRAFT TO BE REWRITTEN ###");
            prompt.AppendLine(editedMem);

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
                GenerateOptimizedMemory(prompt.ToString(), summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error in OptimizeCustomMemoryWithAI: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates optimized memory using the configured AI model
        /// </summary>
        private void GenerateOptimizedMemory(string prompt, System.Action<string> callback)
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
                memoryCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, prompt, callback);
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

        /// <summary>
        /// Gets the last N memories, ordered by day (most recent first)
        /// </summary>
        public List<string> GetLastMemories(int count = 10)
        {
            List<string> recentMemories = new List<string>();

            List<int> sortedDays = new List<int>(memories.Keys);
            sortedDays.Sort((a, b) => b.CompareTo(a)); // Descending (most recent first)

            for (int i = 0; i < sortedDays.Count && i < count; i++)
            {
                recentMemories.Add(memories[sortedDays[i]]);
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

        // FIXED: Proper ExposeData implementation
        public void ExposeData()
        {
            // CRITICAL: NO limpiar el diccionario en LoadingVars
            // Esto era el bug que destruía las memorias antes de cargarlas
            
            Scribe_Collections.Look(ref memories, "memories", LookMode.Value, LookMode.Value);

            // Post-load initialization
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
            }
            
            // Logging durante guardado
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
    }
}