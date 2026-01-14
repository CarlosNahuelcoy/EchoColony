using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections;
using System;

namespace EchoColony
{
    public class DailyGroupMemoryTracker : IExposable
    {
        // Key: día, Value: diccionario grupoId → resumen
        private Dictionary<int, Dictionary<string, string>> dailyMemories = new Dictionary<int, Dictionary<string, string>>();

        /// <summary>
        /// ✅ MEJORADO: Guarda memoria grupal con optimización IA cuando hay contenido previo
        /// </summary>
        public void SaveGroupMemory(int day, string groupId, string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                Log.Warning($"[EchoColony] ⚠️ Intento de guardar memoria grupal vacía para día {day}, grupo {groupId}");
                return;
            }

            if (!dailyMemories.ContainsKey(day))
                dailyMemories[day] = new Dictionary<string, string>();

            string fechaCompleta = GenDate.DateFullStringWithHourAt(GenTicks.TicksGame, new Vector2(0, 0));
            string[] partes = fechaCompleta.Split(' ');
            string fechaSinHora = partes.Length >= 3
                ? partes[0] + " " + partes[1] + " " + partes[2]
                : fechaCompleta;

            // ✅ NUEVO: Si ya existe memoria para este grupo en este día, optimizar con IA
            if (dailyMemories[day].ContainsKey(groupId))
            {
                string existingMemory = dailyMemories[day][groupId];
                
                // Extraer contenido sin fecha
                string existingContent = existingMemory.Contains("]\n") 
                    ? existingMemory.Substring(existingMemory.IndexOf("]\n") + 2)
                    : existingMemory;

                // Verificar duplicados
                string summaryTruncated = summary.Length > 50 ? summary.Substring(0, 50) : summary;
                if (existingContent.ToLowerInvariant().Contains(summaryTruncated.ToLowerInvariant()))
                {
                    Log.Message($"[EchoColony] ⚠️ Memoria grupal similar ya existe para día {day}, grupo {groupId}, omitiendo");
                    return;
                }

                Log.Message($"[EchoColony] 🧠 Combinando memorias grupales para día {day}, grupo {groupId} usando IA...");
                
                // ✅ Usar IA para optimizar memoria grupal
                CombineGroupMemoriesWithAI(day, groupId, existingContent, summary, fechaSinHora);
            }
            else
            {
                // Primera memoria del día para este grupo
                dailyMemories[day][groupId] = $"[{fechaSinHora}]\n{summary}";
                Log.Message($"[EchoColony] 💾 Guardada memoria grupal día {day}, grupo {groupId}: {summary.Substring(0, System.Math.Min(50, summary.Length))}...");
            }
        }

        /// <summary>
        /// ✅ NUEVO: Combina memorias grupales usando IA para crear un resumen único
        /// </summary>
        private void CombineGroupMemoriesWithAI(int day, string groupId, string existingContent, string newContent, string dateHeader)
        {
            string combinedInput = $"Existing group memory:\n{existingContent}\n\nNew group information:\n{newContent}";
           
            string promptForSummary = "Combine these two group conversation memories from the same day into a single unified and natural memory. " +
                                    "Keep all important events from the group conversation but write as if it were a single coherent experience. " +
                                    "Avoid redundancies and maintain a tone that reflects the group dynamics. Don't use phrases like 'New entry' or 'Additionally'. " +
                                    "Maximum 250 words for group conversations.";
            
            string fullPrompt = promptForSummary + "\n\n" + combinedInput;

            // Callback para manejar la respuesta de la IA
            System.Action<string> summaryCallback = (aiSummary) =>
            {
                if (string.IsNullOrWhiteSpace(aiSummary))
                {
                    // Fallback: combinación simple
                    Log.Warning($"[EchoColony] ⚠️ IA devolvió resumen vacío para memoria grupal, usando combinación simple");
                    dailyMemories[day][groupId] = $"[{dateHeader}]\n{existingContent} {newContent}";
                }
                else
                {
                    // ✅ Usar el resumen generado por IA
                    string cleanedSummary = aiSummary.Trim();
                    dailyMemories[day][groupId] = $"[{dateHeader}]\n{cleanedSummary}";
                    Log.Message($"[EchoColony] ✅ Memoria grupal optimizada por IA para día {day}, grupo {groupId}");
                }
            };

            // ✅ Enviar solicitud a IA usando el modelo configurado
            try
            {
                GenerateOptimizedGroupMemory(fullPrompt, summaryCallback);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Error generando memoria grupal optimizada: {ex.Message}");
                // Fallback: combinación simple
                dailyMemories[day][groupId] = $"[{dateHeader}]\n{existingContent} {newContent}";
            }
        }

        /// <summary>
        /// ✅ NUEVO: Genera memoria grupal optimizada usando el modelo de IA configurado
        /// </summary>
        private void GenerateOptimizedGroupMemory(string prompt, System.Action<string> callback)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Error("[EchoColony] ❌ MyStoryModComponent.Instance es null, no se puede optimizar memoria grupal");
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
                // Para memorias grupales, usar un pawn representativo o null
                string koboldPrompt = KoboldPromptBuilder.Build(null, prompt);
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con KoboldAI");
            }
            else if (isLMStudio)
            {
                string lmPrompt = LMStudioPromptBuilder.Build(null, prompt);
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con LMStudio");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                memoryCoroutine = GeminiAPI.SendRequestToLocalModel(prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con modelo local");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                memoryCoroutine = GeminiAPI.SendRequestToPlayer2(null, prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con Player2");
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(prompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con OpenRouter");
            }
            else // Gemini (por defecto)
            {
                // Para Gemini, crear JSON apropiado
                var tempHistory = new List<GeminiMessage>
                {
                    new GeminiMessage("user", prompt)
                };
                string jsonPrompt = BuildGeminiChatJson(tempHistory);
                memoryCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, callback);
                Log.Message("[EchoColony] 🚀 Optimizando memoria grupal con Gemini");
            }

            if (memoryCoroutine != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(memoryCoroutine);
            }
            else
            {
                Log.Error("[EchoColony] ❌ No se pudo crear coroutine para optimizar memoria grupal");
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

        public string GetMemoryForGroupOnDay(int day, string groupId)
        {
            Dictionary<string, string> groupDict;
            if (dailyMemories.TryGetValue(day, out groupDict))
            {
                string summary;
                if (groupDict.TryGetValue(groupId, out summary))
                    return summary;
            }
            return null;
        }

        public List<string> GetAllRecentMemories(int lastNDays)
        {
            int today = GenDate.DaysPassed;
            List<string> summaries = new List<string>();

            foreach (var kvp in dailyMemories)
            {
                int day = kvp.Key;
                if (today - day <= lastNDays)
                    summaries.AddRange(kvp.Value.Values);
            }

            return summaries;
        }

        // MÉTODO FALTANTE PARA EL VISOR DE MEMORIAS
        public Dictionary<string, Dictionary<int, string>> GetAllGroupMemories()
        {
            var result = new Dictionary<string, Dictionary<int, string>>();

            // Reorganizar: de [día][grupoId] -> a [grupoId][día]
            foreach (var dayData in dailyMemories)
            {
                int day = dayData.Key;
                var groupsForDay = dayData.Value;

                foreach (var groupData in groupsForDay)
                {
                    string groupId = groupData.Key;
                    string memory = groupData.Value;

                    if (!result.ContainsKey(groupId))
                        result[groupId] = new Dictionary<int, string>();

                    result[groupId][day] = memory;
                }
            }

            Log.Message($"[EchoColony] 📖 GetAllGroupMemories devolvió {result.Count} grupos únicos");
            return result;
        }

        public Dictionary<int, string> GetAllMemoriesForGroup(string groupId)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();

            foreach (var kvp in dailyMemories)
            {
                int day = kvp.Key;
                Dictionary<string, string> groupDict = kvp.Value;

                if (groupDict.ContainsKey(groupId))
                {
                    result[day] = groupDict[groupId];
                }
            }

            Log.Message($"[EchoColony] 📖 GetAllMemoriesForGroup({groupId}) devolvió {result.Count} entradas");
            return result;
        }

        // MÉTODO ADICIONAL ÚTIL PARA DEBUGGING
        public void PrintAllMemories()
        {
            Log.Message($"[EchoColony] 🗂️ === DUMP DE TODAS LAS MEMORIAS GRUPALES ===");
            Log.Message($"[EchoColony] Total días con memorias: {dailyMemories.Count}");
            
            foreach (var dayData in dailyMemories)
            {
                int day = dayData.Key;
                var groups = dayData.Value;
                Log.Message($"[EchoColony] Día {day}: {groups.Count} grupos");
                
                foreach (var groupData in groups)
                {
                    string groupId = groupData.Key;
                    string memory = groupData.Value;
                    string preview = memory.Length > 100 ? memory.Substring(0, 100) + "..." : memory;
                    Log.Message($"[EchoColony]   - Grupo {groupId}: {preview}");
                }
            }
            Log.Message($"[EchoColony] 🗂️ === FIN DUMP ===");
        }

        // MÉTODO PARA LIMPIAR MEMORIAS ANTIGUAS (OPCIONAL)
        public void CleanOldMemories(int keepLastNDays = 30)
        {
            int today = GenDate.DaysPassed;
            var keysToRemove = new List<int>();

            foreach (var day in dailyMemories.Keys)
            {
                if (today - day > keepLastNDays)
                    keysToRemove.Add(day);
            }

            foreach (var key in keysToRemove)
            {
                dailyMemories.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                Log.Message($"[EchoColony] 🧹 Limpiadas {keysToRemove.Count} memorias grupales antiguas");
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                dailyMemories = new Dictionary<int, Dictionary<string, string>>();
            }

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<int> Sdays = new List<int>();
                List<List<string>> SgroupKeys = new List<List<string>>();
                List<List<string>> SgroupValues = new List<List<string>>();

                foreach (var kvp in dailyMemories)
                {
                    Sdays.Add(kvp.Key);
                    List<string> keys = new List<string>();
                    List<string> values = new List<string>();

                    foreach (var pair in kvp.Value)
                    {
                        keys.Add(pair.Key);
                        values.Add(pair.Value);
                    }

                    SgroupKeys.Add(keys);
                    SgroupValues.Add(values);
                }

                Scribe_Collections.Look(ref Sdays, "days", LookMode.Value);
                Scribe_Collections.Look(ref SgroupKeys, "groupKeys", LookMode.Value, LookMode.Value);
                Scribe_Collections.Look(ref SgroupValues, "groupValues", LookMode.Value, LookMode.Value);
            }

            
            List<int> days = null;
            List<List<string>> groupKeys = null;
            List<List<string>> groupValues = null;

            Scribe_Collections.Look(ref days, "days", LookMode.Value);
            Scribe_Collections.Look(ref groupKeys, "groupKeys", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref groupValues, "groupValues", LookMode.Value, LookMode.Value);


            if (Scribe.mode == LoadSaveMode.LoadingVars && days != null)
            {
                dailyMemories.Clear();
                for (int i = 0; i < days.Count; i++)
                {
                    Dictionary<string, string> groupData = new Dictionary<string, string>();
                        
                    // ✅ VERIFICACIÓN DE BOUNDS PARA EVITAR CRASHES
                    if (i < groupKeys.Count && i < groupValues.Count)
                    {
                        for (int j = 0; j < groupKeys[i].Count && j < groupValues[i].Count; j++)
                        {
                            string key = groupKeys[i][j];
                            string val = groupValues[i][j];
                            groupData[key] = val;
                        }
                    }
                        
                    dailyMemories[days[i]] = groupData;
                }
                    
                Log.Message($"[EchoColony] 📖 Cargadas {dailyMemories.Count} días de memorias grupales desde save");
            }
            else
            {
                // ✅ INICIALIZAR SI NO HAY DATOS GUARDADOS
                dailyMemories = new Dictionary<int, Dictionary<string, string>>();
                Log.Message("[EchoColony] 📖 Inicializadas memorias grupales vacías (primera vez)");
            }
            
            // ✅ INICIALIZACIÓN POST-CARGA
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (dailyMemories == null)
                    dailyMemories = new Dictionary<int, Dictionary<string, string>>();
            }
        }
    }
}