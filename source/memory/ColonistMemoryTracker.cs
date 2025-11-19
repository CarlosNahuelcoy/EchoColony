using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections;
using System;

namespace EchoColony
{
    public class ColonistMemoryTracker : IExposable
    {
        private Dictionary<int, string> memories = new Dictionary<int, string>();
        private Pawn pawn; // Referencia para logging

        // ✅ Constructor sin parámetros (REQUERIDO para la serialización de RimWorld)
        public ColonistMemoryTracker()
        {
            this.pawn = null;
        }

        // Constructor para asignar el pawn
        public ColonistMemoryTracker(Pawn pawn)
        {
            this.pawn = pawn;
        }

        /// <summary>
        /// ✅ MEJORADO: Guarda una memoria optimizada usando IA para resumir cuando hay contenido previo
        /// </summary>
        public void SaveMemoryForDay(int day, string newSummary)
        {
            if (string.IsNullOrWhiteSpace(newSummary))
            {
                Log.Warning($"[EchoColony] ⚠️ Intento de guardar memoria vacía para {pawn?.LabelShort ?? "Unknown"} día {day}");
                return;
            }

            string fechaCompleta = GenDate.DateFullStringWithHourAt(GenTicks.TicksGame, new Vector2(0, 0));
            string[] partes = fechaCompleta.Split(' ');

            // Nos aseguramos de no fallar si el formato cambia
            string fechaSinHora = partes.Length >= 3
                ? partes[0] + " " + partes[1] + " " + partes[2]
                : fechaCompleta;

            // Si ya existe una memoria para este día, usar IA para combinar y resumir
            if (memories.ContainsKey(day))
            {
                string existingMemory = memories[day];
                
                // Extraer el contenido sin fecha de la memoria existente
                string existingContent = existingMemory.Contains("]\n") 
                    ? existingMemory.Substring(existingMemory.IndexOf("]\n") + 2)
                    : existingMemory;

                // ✅ Verificar si el contenido nuevo ya está incluido (evitar duplicados)
                string newContentTruncated = newSummary.Length > 50 ? newSummary.Substring(0, 50) : newSummary;
                if (existingContent.ToLowerInvariant().Contains(newContentTruncated.ToLowerInvariant()))
                {
                    Log.Message($"[EchoColony] ⚠️ Memoria similar ya existe para {pawn?.LabelShort ?? "Unknown"} día {day}, omitiendo");
                    return;
                }

                Log.Message($"[EchoColony] 🧠 Combinando memorias para {pawn?.LabelShort ?? "Unknown"} día {day} usando IA...");
                
                // ✅ Usar IA para crear un resumen único optimizado
                CombineMemoriesWithAI(day, existingContent, newSummary, fechaSinHora);
            }
            else
            {
                // Primera memoria del día
                memories[day] = $"[{fechaSinHora}]\n{newSummary}";
                Log.Message($"[EchoColony] 💾 Nueva memoria guardada para {pawn?.LabelShort ?? "Unknown"} día {day}");
            }
        }

        /// <summary>
        /// ✅ NUEVO: Combina memorias usando IA para crear un resumen único y optimizado
        /// </summary>
        private void CombineMemoriesWithAI(int day, string existingContent, string newContent, string dateHeader)
        {
            string combinedInput = $"Memoria existente del día:\n{existingContent}\n\nNueva información:\n{newContent}";
            
            string promptForSummary = "Combine these two memories from the same day into a single unified and natural memory. " +
                         "Keep all important events but write as if it were a single coherent experience of the day. " +
                         "Avoid redundancies and maintain a personal and intimate tone. Don't use phrases like 'New entry' or 'Additionally'. " +
                         "Maximum 200 words.";
            
            string fullPrompt = promptForSummary + "\n\n" + combinedInput;

            // Callback para manejar la respuesta de la IA
            System.Action<string> summaryCallback = (aiSummary) =>
            {
                if (string.IsNullOrWhiteSpace(aiSummary))
                {
                    // Fallback: combinación simple sin IA
                    Log.Warning($"[EchoColony] ⚠️ IA devolvió resumen vacío, usando combinación simple para {pawn?.LabelShort ?? "Unknown"}");
                    memories[day] = $"[{dateHeader}]\n{existingContent} {newContent}";
                }
                else
                {
                    // ✅ Usar el resumen generado por IA
                    string cleanedSummary = aiSummary.Trim();
                    memories[day] = $"[{dateHeader}]\n{cleanedSummary}";
                    Log.Message($"[EchoColony] ✅ Memoria optimizada por IA para {pawn?.LabelShort ?? "Unknown"} día {day}");
                }
            };

            // ✅ Enviar solicitud a IA usando el modelo configurado
            try
            {
                GenerateOptimizedMemory(fullPrompt, summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Error generando memoria optimizada: {ex.Message}");
                // Fallback: combinación simple
                memories[day] = $"[{dateHeader}]\n{existingContent} {newContent}";
            }
        }

        /// <summary>
        /// ✅ NUEVO: Genera memoria optimizada usando el modelo de IA configurado
        /// </summary>
        private void GenerateOptimizedMemory(string prompt, System.Action<string> callback)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Error("[EchoColony] ❌ MyStoryModComponent.Instance es null, no se puede optimizar memoria");
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
                Log.Message("[EchoColony] 🚀 Optimizando memoria con KoboldAI");
            }
            else if (isLMStudio)
            {
                string lmPrompt = LMStudioPromptBuilder.Build(pawn, prompt);
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria con LMStudio");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria con modelo local");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                memoryCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria con Player2");
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria con OpenRouter");
            }
            else // Gemini (por defecto)
            {
                // Para Gemini, necesitamos crear el JSON apropiado
                var tempHistory = new List<GeminiMessage>
                {
                    new GeminiMessage("user", prompt)
                };
                string jsonPrompt = BuildGeminiChatJson(tempHistory);
                memoryCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria con Gemini");
            }

            if (memoryCoroutine != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(memoryCoroutine);
            }
            else
            {
                Log.Error("[EchoColony] ❌ No se pudo crear coroutine para optimizar memoria");
                callback?.Invoke("");
            }
        }

        /// <summary>
        /// ✅ NUEVO: Clase para mensajes de Gemini (local para evitar dependencias)
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
        /// ✅ NUEVO: Helper para construir JSON de Gemini
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
        /// ✅ NUEVO: Helper para escapar JSON
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
        /// Obtiene la memoria de un día específico
        /// </summary>
        public string GetMemoryForDay(int day)
        {
            string result;
            return memories.TryGetValue(day, out result) ? result : null;
        }

        /// <summary>
        /// Elimina la memoria de un día específico
        /// </summary>
        public bool RemoveMemoryForDay(int day)
        {
            if (memories.ContainsKey(day))
            {
                memories.Remove(day);
                Log.Message($"[EchoColony] 🗑️ Memoria del día {day} eliminada para {pawn?.LabelShort ?? "Unknown"}");
                return true;
            }
            else
            {
                Log.Warning($"[EchoColony] ⚠️ No se encontró memoria del día {day} para eliminar para {pawn?.LabelShort ?? "Unknown"}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene todas las memorias del colono
        /// </summary>
        public Dictionary<int, string> GetAllMemories()
        {
            return new Dictionary<int, string>(memories);
        }

        /// <summary>
        /// Obtiene las últimas N memorias, ordenadas por día (más recientes primero)
        /// </summary>
        public List<string> GetLastMemories(int count = 10)
        {
            List<string> recentMemories = new List<string>();

            List<int> sortedDays = new List<int>(memories.Keys);
            sortedDays.Sort((a, b) => b.CompareTo(a)); // Descendente (más reciente primero)

            for (int i = 0; i < sortedDays.Count && i < count; i++)
            {
                recentMemories.Add(memories[sortedDays[i]]);
            }

            return recentMemories;
        }

        /// <summary>
        /// Obtiene memorias de los últimos N días
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

            // Ordenar por día (más reciente primero)
            recentMemories = recentMemories
                .OrderByDescending(m => ExtractDayFromMemory(m))
                .ToList();

            return recentMemories;
        }

        /// <summary>
        /// Extrae el número de día de una memoria formateada
        /// </summary>
        private int ExtractDayFromMemory(string memory)
        {
            // Buscar en memories.Keys la memoria que coincida
            foreach (var kvp in memories)
            {
                if (kvp.Value == memory)
                    return kvp.Key;
            }
            return 0; // Fallback
        }

        /// <summary>
        /// Elimina todas las memorias del colono
        /// </summary>
        public void ClearAllMemories()
        {
            int count = memories.Count;
            memories.Clear();
            Log.Message($"[EchoColony] 🗑️ {count} memorias eliminadas para {pawn?.LabelShort ?? "Unknown"}");
        }

        /// <summary>
        /// Elimina memorias anteriores a una fecha específica
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
                Log.Message($"[EchoColony] 🧹 {keysToRemove.Count} memorias antiguas eliminadas para {pawn?.LabelShort ?? "Unknown"}");
            }
        }

        /// <summary>
        /// Obtiene el día de la memoria más reciente
        /// </summary>
        public int GetLastMemoryDay()
        {
            if (memories == null || memories.Count == 0) return -1;
            return memories.Keys.Max();
        }

        /// <summary>
        /// Obtiene estadísticas de las memorias
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
                // Contar tipos
                if (memory.StartsWith("[Conversación grupal") || memory.Contains("conversación grupal"))
                    grupal++;
                else
                    individual++;
            }

            // Contar recientes (últimos 7 días)
            foreach (var day in memories.Keys)
            {
                if (currentDay - day <= 7)
                    recent++;
            }

            return (total, individual, grupal, recent);
        }

        /// <summary>
        /// Busca memorias que contengan un texto específico
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
        /// Debug: Imprime todas las memorias en los logs
        /// </summary>
        public void PrintAllMemories()
        {
            Log.Message($"[EchoColony] 🗂️ === MEMORIAS DE {pawn?.LabelShort ?? "Unknown"} ===");
            Log.Message($"[EchoColony] Total: {memories.Count} memorias");

            foreach (var kvp in memories.OrderByDescending(m => m.Key))
            {
                int day = kvp.Key;
                string memory = kvp.Value;
                string preview = memory.Length > 100 ? memory.Substring(0, 100) + "..." : memory;
                string type = memory.StartsWith("[Conversación grupal") ? "GRUPAL" : "INDIVIDUAL";
                
                Log.Message($"[EchoColony] Día {day} ({type}): {preview}");
            }
            
            Log.Message($"[EchoColony] 🗂️ === FIN MEMORIAS ===");
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref memories, "memories", LookMode.Value, LookMode.Value);
            }

            // Inicialización segura
            if (memories == null)
            {
                memories = new Dictionary<int, string>();
                Log.Message($"[EchoColony] 📖 Inicializadas memorias para {pawn?.LabelShort ?? "Unknown"}");
            }
            
            // Log de carga
            if (Scribe.mode == LoadSaveMode.LoadingVars && memories.Count > 0)
            {
                Log.Message($"[EchoColony] 📖 Cargadas {memories.Count} memorias para {pawn?.LabelShort ?? "Unknown"}");
            }
        }

        /// <summary>
        /// Asigna la referencia del pawn (útil después de la carga)
        /// </summary>
        public void SetPawn(Pawn pawn)
        {
            this.pawn = pawn;
        }
    }
}