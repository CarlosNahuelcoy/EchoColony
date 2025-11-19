using System.Text;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections;
using Verse;
using SimpleJSON;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;

namespace EchoColony
{
    public static class GeminiAPI
    {
        public static IEnumerator GetResponseFromModel(Pawn pawn, string prompt, Action<string> onResponse)
        {
            if (MyMod.Settings == null)
            {
                onResponse?.Invoke("⚠️ Settings not loaded.");
                yield break;
            }

            // Solo si Player2 NO está activado, usar otros modelos
            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Player2:
                    yield return SendRequestToPlayer2(pawn, prompt, onResponse);
                    yield break;
                case ModelSource.Local:
                    yield return SendRequestToLocalModel(prompt, onResponse);
                    yield break;
                case ModelSource.OpenRouter:
                    yield return SendRequestToOpenRouter(prompt, onResponse);
                    yield break;
                case ModelSource.Gemini:
                    string geminiJson = CreateGeminiRequestJson(prompt);
                    yield return SendRequestToGemini(geminiJson, onResponse);
                    yield break;
                default:
                    onResponse?.Invoke("❌ Error: Unknown model source. Please check mod settings.");
                    yield break;
            }
        }

        public static IEnumerator SendRequestToPlayer2(Pawn pawn, string userInput, Action<string> onResponse)
        {
            // ✅ Health check primero
            string healthCheckUrl = "http://127.0.0.1:4315/v1/health";
            UnityWebRequest healthRequest = UnityWebRequest.Get(healthCheckUrl);
            healthRequest.timeout = 2;

            yield return healthRequest.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
    if (healthRequest.result != UnityWebRequest.Result.Success)
#else
            if (healthRequest.isNetworkError || healthRequest.isHttpError)
#endif
            {
                onResponse?.Invoke("⚠️ Player2 is not running.\nDownload the Player2 app to power the AIs for free from https://player2.game/");
                yield break;
            }

            string healthResponse = healthRequest.downloadHandler.text;
            if (!healthResponse.Contains("client_version"))
            {
                onResponse?.Invoke("⚠️ Player2 is installed but not responding correctly.\nMake sure the app is running, or reinstall it from https://player2.game/");
                yield break;
            }

            // ✅ Preparar el request
            string endpoint = "http://127.0.0.1:4315/v1/chat/completions";

            RebuildMemoryFromChat(pawn);

            var (systemPrompt, userMessage) = ColonistPromptContextBuilder.BuildForPlayer2(pawn, userInput);
            EchoMemory.SetSystemPrompt(systemPrompt);

            var messages = new List<Dictionary<string, string>>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new Dictionary<string, string> {
            { "role", "system" },
            { "content", systemPrompt }
        });
            }

            foreach (var entry in EchoMemory.GetRecentTurns())
            {
                messages.Add(new Dictionary<string, string> {
            { "role", entry.Item1 },
            { "content", entry.Item2 }
        });
            }

            messages.Add(new Dictionary<string, string> {
        { "role", "user" },
        { "content", userMessage }
    });

            var jsonPayload = new JSONObject();
            var jsonMessages = new JSONArray();

            foreach (var msg in messages)
            {
                var jsonMsg = new JSONObject();
                jsonMsg["role"] = msg["role"];
                jsonMsg["content"] = msg["content"];
                jsonMessages.Add(jsonMsg);
            }

            jsonPayload["messages"] = jsonMessages;
            string jsonBody = jsonPayload.ToString();

            if (MyMod.Settings?.debugMode == true)
                LogPlayer2Debug("REQUEST", jsonBody);

            // ✅ Sistema de reintentos con backoff exponencial (IGUAL QUE GEMINI)
            int maxRetries = 3;
            float retryDelay = 1f; // ✅ CAMBIADO: Mismo tiempo que Gemini porque usa APIs web

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                    downloadHandler = new DownloadHandlerBuffer()
                };

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
        bool hasError = request.result != UnityWebRequest.Result.Success;
#else
                bool hasError = request.isNetworkError || request.isHttpError;
#endif

                // ✅ Si fue exitoso, procesar y retornar
                if (!hasError)
                {
                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("RESPONSE", responseText);

                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    EchoMemory.AddTurn("user", userMessage);
                    EchoMemory.AddTurn("assistant", reply);

                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("FINAL_REPLY", reply);

                    onResponse?.Invoke(reply);
                    yield break;
                }

                // ✅ Si es error 500, 503 o 429, reintentar
                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1) // No es el último intento
                    {
                        if (MyMod.Settings?.debugMode == true)
                        {
                            LogPlayer2Debug("RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\nReason: Player2 uses web APIs internally and may be rate-limited.\n{responseText}");
                        }

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f; // ✅ CAMBIADO: Backoff exponencial igual que Gemini: 1s, 2s, 4s
                        continue;
                    }
                }

                // ✅ Si llegamos aquí, falló definitivamente
                if (MyMod.Settings?.debugMode == true)
                    LogPlayer2Debug("ERROR_RESPONSE", $"Status: {request.responseCode}\n{responseText}");

                onResponse?.Invoke($"❌ Error contacting Player2 after {maxRetries} attempts: {request.error}");
                yield break;
            }
        }

        public static IEnumerator SendRequestToLocalModel(string prompt, Action<string> onResponse)
        {
            string endpoint = MyMod.Settings.localModelEndpoint;
            string modelName = MyMod.Settings.localModelName;
            string jsonBody;

            switch (MyMod.Settings.localModelProvider)
            {
                case LocalModelProvider.LMStudio:
                    jsonBody = $"{{\"model\": \"{modelName}\", \"messages\": [{{\"role\": \"user\", \"content\": \"{EscapeJson(prompt)}\"}}], \"stream\": false}}";
                    break;
                case LocalModelProvider.KoboldAI:
                    jsonBody = $"{{\"model\": \"{modelName}\", \"prompt\": \"{EscapeJson(prompt)}\", \"max_length\": 7000, \"stream\": false}}";
                    break;
                case LocalModelProvider.Ollama:
                default:
                    jsonBody = $"{{\"model\": \"{modelName}\", \"prompt\": \"{EscapeJson(prompt)}\", \"stream\": false, \"options\": {{\"num_ctx\": 16384}}}}";
                    break;
            }

            var request = new UnityWebRequest(endpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                LogDebugResponse("LocalModel_ERROR", $"Status: {request.responseCode}\n{responseText}");
                onResponse?.Invoke($"❌ Error contacting local model: {request.error}");
                yield break;
            }

            string text = ParseStandardLLMResponse(responseText);
            text = TrimTextAfterHashtags(text);
            // ✅ FIX: Reemplazado ExtractLongestQuotedSegment con CleanResponse
            text = CleanResponse(text);

            LogDebugResponse("LocalModel", text);
            onResponse?.Invoke(text);
        }

        public static IEnumerator SendRequestToGemini(string prompt, Action<string> onResponse)
        {
            if (string.IsNullOrEmpty(MyMod.Settings.apiKey))
            {
                onResponse?.Invoke("⚠️ Missing Gemini API Key. Set it in mod settings.");
                yield break;
            }

            string model = MyMod.Settings.useAdvancedModel ? "gemini-2.5-pro-preview-06-05" : "gemini-2.5-flash-preview-05-20";
            string apiKey = MyMod.Settings.apiKey;
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            string requestJson = CreateGeminiRequestJson(prompt);

            // ✅ Sistema de reintentos con backoff exponencial
            int maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
        bool hasError = request.result != UnityWebRequest.Result.Success;
#else
                bool hasError = request.isNetworkError || request.isHttpError;
#endif

                // ✅ Si fue exitoso, procesar y retornar
                if (!hasError)
                {
                    string reply = ParseGeminiReply(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    LogDebugResponse("GeminiAPI", reply);
                    onResponse?.Invoke(reply);
                    yield break;
                }

                // ✅ Si es error 429 (rate limit), 500 o 503 (server error), reintentar
                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1) // No es el último intento
                    {
                        if (MyMod.Settings?.debugMode == true)
                        {
                            LogDebugResponse("GeminiAPI_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");
                        }

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f; // Backoff exponencial: 1s, 2s, 4s
                        continue;
                    }
                }

                // ✅ Si llegamos aquí, falló definitivamente
                LogDebugResponse("GeminiAPI_ERROR", $"Status: {request.responseCode}\n{responseText}");
                onResponse?.Invoke($"❌ Failed to contact Gemini after {maxRetries} attempts: {request.error}");
                yield break;
            }
        }

// ✅ FIX: Mejorar el método CreateGeminiRequestJson para manejar caracteres especiales
private static string CreateGeminiRequestJson(string prompt)
{
    // Escapar el prompt para JSON
    string escapedPrompt = EscapeJson(prompt);
    
    // Crear el JSON manualmente para tener control total
    string json = $@"{{
  ""contents"": [
    {{
      ""parts"": [
        {{
          ""text"": ""{escapedPrompt}""
        }}
      ]
    }}
  ]
}}";

    return json;
}

        public static IEnumerator SendRequestToOpenRouter(string prompt, Action<string> onResponse)
        {
            string endpoint = MyMod.Settings.openRouterEndpoint;
            string apiKey = MyMod.Settings.openRouterApiKey;
            string jsonBody = $"{{\"model\": \"{EscapeJson(MyMod.Settings.openRouterModel)}\", \"messages\": [{{\"role\": \"user\", \"content\": \"{EscapeJson(prompt)}\"}}], \"stream\": false}}";

            var request = new UnityWebRequest(endpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                LogDebugResponse("OpenRouter_ERROR", $"Status: {request.responseCode}\n{responseText}");
                onResponse?.Invoke($"❌ Error contacting OpenRouter: {request.error}");
                yield break;
            }

            string text = ParseStandardLLMResponse(responseText);
            text = TrimTextAfterHashtags(text);
            // ✅ FIX: Reemplazado ExtractLongestQuotedSegment con CleanResponse
            text = CleanResponse(text);

            LogDebugResponse("OpenRouter", text);
            onResponse?.Invoke(text);
        }

        private static string ParseGeminiReply(string json)
        {
            try
            {
                var parsed = JSON.Parse(json);
                return parsed["candidates"][0]["content"]["parts"][0]["text"];
            }
            catch
            {
                return "❌ Error parsing Gemini response.";
            }
        }

        public static class EchoMemory
        {
            public static string LastSystemPrompt;
            private static List<Tuple<string, string>> recentTurns = new List<Tuple<string, string>>();

            public static void SetSystemPrompt(string prompt)
            {
                LastSystemPrompt = prompt;
            }

            public static void AddTurn(string role, string text)
            {
                recentTurns.Add(Tuple.Create(role, text));
                if (recentTurns.Count > 20) // Máximo 10 interacciones (10 user + 10 assistant)
                    recentTurns.RemoveAt(0);
            }

            public static List<Tuple<string, string>> GetRecentTurns()
            {
                return new List<Tuple<string, string>>(recentTurns);
            }
    
            public static void Clear()
            {
                recentTurns.Clear();
            }
        }

        private static string ParseStandardLLMResponse(string json)
        {
            try
            {
                var parsed = JSON.Parse(json);

                if (parsed["response"] != null)
                    return parsed["response"];
                if (parsed["choices"] != null)
                {
                    var choice = parsed["choices"][0];
                    if (choice["message"]?["content"] != null)
                        return choice["message"]["content"];
                    if (choice["text"] != null)
                        return choice["text"];
                }
                if (parsed["results"]?[0]["text"] != null)
                    return parsed["results"][0]["text"];
                if (parsed["text"] != null)
                {
                    string fullText = parsed["text"];
                    // ✅ FIX: No extraer automáticamente contenido entre comillas
                    // Solo hacerlo si TODA la respuesta está entre comillas Y es muy corta
                    if (fullText.StartsWith("\"") && fullText.EndsWith("\"") && fullText.Length < 50)
                    {
                        return fullText.Substring(1, fullText.Length - 2);
                    }
                    return fullText;
                }
                
                // ✅ FIX: Solo como último recurso, buscar comillas, pero con longitud mínima mayor
                var quoted = Regex.Match(json, "\"([^\"]{100,})\"");
                if (quoted.Success)
                    return quoted.Groups[1].Value;
                    
                return "❌ Unrecognized response format.";
            }
            catch
            {
                return "❌ Error parsing model response.";
            }
        }

        // ✅ NUEVO MÉTODO: Limpieza inteligente sin cortar contenido válido
        private static string CleanResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Limpiar espacios en blanco extra
            text = text.Trim();

            // Solo remover comillas si la ENTERA respuesta está envuelta en comillas
            // Y parece ser un wrapper artificial (muy corto o contiene caracteres extraños)
            if (text.StartsWith("\"") && text.EndsWith("\""))
            {
                string unwrapped = text.Substring(1, text.Length - 2);
                
                // Solo desenvolver si parece un wrapper artificial
                if (text.Length < 30 || // Muy corto, probablemente wrapped
                    !unwrapped.Contains(" ") || // Una sola palabra wrapped
                    unwrapped.Split(' ').Length < 3) // Menos de 3 palabras
                {
                    return unwrapped.Trim();
                }
                
                // Si es una frase larga entre comillas, mantener las comillas
                // porque probablemente es diálogo intencional del colono
            }

            // Remover prefijos comunes de IA que pueden aparecer
            string[] prefixesToRemove = {
                "As a colonist, ",
                "As someone who ",
                "I would say ",
                "My response would be ",
                "I think ",
                "Well, ",
                "You know, "
            };

            foreach (string prefix in prefixesToRemove)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(prefix.Length).Trim();
                    break;
                }
            }

            // Limpiar sufijos problemáticos
            string[] suffixesToRemove = {
                " #",
                " [END]",
                " </response>",
                " ```"
            };

            foreach (string suffix in suffixesToRemove)
            {
                if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(0, text.Length - suffix.Length).Trim();
                    break;
                }
            }

            return text;
        }

        public static void RebuildMemoryFromChat(Pawn pawn)
        {
            EchoMemory.Clear();
            var lines = ChatGameComponent.Instance.GetChat(pawn);
            foreach (var line in lines)
            {
                if (line.StartsWith("[USER]"))
                {
                    string text = line.Substring(6).Trim();
                    EchoMemory.AddTurn("user", text);
                }
                else if (line.StartsWith(pawn.LabelShort + ":"))
                {
                    string text = line.Substring(pawn.LabelShort.Length + 1).Trim();
                    EchoMemory.AddTurn("assistant", text);
                }
            }
        }

        private static string EscapeJson(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string TrimTextAfterHashtags(string text)
        {
            int hashtagIndex = text.IndexOf(" #");
            return hashtagIndex > 0 ? text.Substring(0, hashtagIndex).Trim() : text;
        }

        // ✅ MÉTODO PROBLEMÁTICO ELIMINADO: ExtractLongestQuotedSegment
        // Este método causaba que se cortaran las respuestas cuando tenían comillas

        private static void LogDebugResponse(string sourceName, string responseText)
        {
            if (MyMod.Settings?.debugMode != true)
                return;
            try
            {
                string safeSource = sourceName.Replace(" ", "_");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"{safeSource}_Response_{timestamp}.txt";
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fullPath = Path.Combine(desktopPath, filename);
                File.WriteAllText(fullPath, responseText);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to save debug response: {ex.Message}");
            }
        }

        // 🔧 Nueva función específica para logging de Player2
        private static void LogPlayer2Debug(string type, string content)
        {
            if (MyMod.Settings?.debugMode != true)
                return;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                string filename = $"Player2_{type}_{timestamp}.txt";
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fullPath = Path.Combine(desktopPath, filename);

                string debugContent = $"=== PLAYER2 {type} DEBUG LOG ===\n";
                debugContent += $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n";
                debugContent += $"Type: {type}\n";
                debugContent += "".PadRight(50, '=') + "\n\n";
                debugContent += content;

                File.WriteAllText(fullPath, debugContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to save Player2 debug log: {ex.Message}");
            }
        }
    }
}