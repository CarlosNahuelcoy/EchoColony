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
    public class GeminiModelInfo
    {
        public string Name { get; set; }
        public bool IsAdvanced { get; set; }

        public GeminiModelInfo(string name, bool isAdvanced = false)
        {
            Name = name;
            IsAdvanced = isAdvanced;
        }
    }

    public static class GeminiAPI
    {
        // ═══════════════════════════════════════════════════════════════
        // MODEL SELECTION — usa directamente lo que el usuario eligió
        // ═══════════════════════════════════════════════════════════════

        public static string GetSelectedModel()
        {
            if (MyMod.Settings == null)
                return "gemini-2.0-flash-001";

            // Si el usuario eligió un modelo explícitamente, usarlo sin validar nada más
            if (!string.IsNullOrEmpty(MyMod.Settings.selectedModel))
            {
                if (MyMod.Settings.debugMode)
                    LogDebugResponse("ModelSelection", $"Using user-selected model: {MyMod.Settings.selectedModel}");

                return MyMod.Settings.selectedModel;
            }

            // Fallback solo si nunca se eligió nada
            if (MyMod.Settings.debugMode)
                LogDebugResponse("ModelSelection", "No model selected, using default: gemini-2.0-flash-001");

            return "gemini-2.0-flash-001";
        }

        // Mantener firma antigua para no romper otras partes del código que la llamen
        public static string GetBestAvailableModel(bool useAdvanced = false)
        {
            return GetSelectedModel();
        }

        // ═══════════════════════════════════════════════════════════════
        // FETCH MODELS FROM API
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator FetchAvailableModels(string apiKey, Action<List<GeminiModelInfo>> onComplete)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";

            var request = UnityWebRequest.Get(endpoint);
            request.timeout = 10;

            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool hasError = request.result != UnityWebRequest.Result.Success;
#else
            bool hasError = request.isNetworkError || request.isHttpError;
#endif

            if (hasError)
            {
                Log.Warning($"[EchoColony] Failed to fetch Gemini models: {request.error}");
                onComplete?.Invoke(null);
                yield break;
            }

            try
            {
                var parsed = JSON.Parse(request.downloadHandler.text);
                var modelsArray = parsed["models"]?.AsArray;

                if (modelsArray == null)
                {
                    onComplete?.Invoke(null);
                    yield break;
                }

                var result = new List<GeminiModelInfo>();

                foreach (JSONNode modelNode in modelsArray)
                {
                    // Solo modelos que soporten generateContent
                    bool supportsGenerate = false;
                    var methods = modelNode["supportedGenerationMethods"]?.AsArray;
                    if (methods != null)
                    {
                        foreach (JSONNode method in methods)
                        {
                            if (method.Value == "generateContent")
                            {
                                supportsGenerate = true;
                                break;
                            }
                        }
                    }

                    if (!supportsGenerate) continue;

                    // "models/gemini-2.0-flash-001" → "gemini-2.0-flash-001"
                    string fullName = modelNode["name"]?.Value ?? "";
                    string modelId = fullName.StartsWith("models/") ? fullName.Substring(7) : fullName;

                    if (string.IsNullOrEmpty(modelId)) continue;

                    // Badge visual únicamente — no afecta la selección
                    bool isAdvanced = modelId.Contains("-pro") || modelId.Contains("thinking");

                    result.Add(new GeminiModelInfo(modelId, isAdvanced));
                }

                // Ordenar: más recientes primero
                result = result.OrderByDescending(m => m.Name).ToList();

                Log.Message($"[EchoColony] Fetched {result.Count} Gemini models");
                onComplete?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error parsing Gemini models response: {ex.Message}");
                onComplete?.Invoke(null);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MAIN DISPATCH
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator GetResponseFromModel(Pawn pawn, string prompt, Action<string> onResponse)
        {
            if (MyMod.Settings == null)
            {
                onResponse?.Invoke("⚠ ERROR: Settings not loaded");
                yield break;
            }

            bool isAnimal = pawn != null && pawn.RaceProps.Animal;

            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Player2:
                    if (isAnimal)
                        yield return SendRequestToPlayer2WithPrompt(prompt, onResponse);
                    else
                        yield return SendRequestToPlayer2(pawn, prompt, onResponse);
                    yield break;

                case ModelSource.Local:
                    yield return SendRequestToLocalModel(prompt, onResponse);
                    yield break;

                case ModelSource.OpenRouter:
                    yield return SendRequestToOpenRouter(prompt, onResponse);
                    yield break;

                case ModelSource.Gemini:
                    yield return SendRequestToGemini(prompt, onResponse);
                    yield break;

                default:
                    onResponse?.Invoke("⚠ ERROR: Unknown model source - Check mod settings");
                    yield break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GEMINI REQUEST
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToGemini(string prompt, Action<string> onResponse)
        {
            if (string.IsNullOrEmpty(MyMod.Settings.apiKey))
            {
                onResponse?.Invoke("⚠ ERROR: Missing Gemini API Key\n\nSet your API key in mod settings\nGet one free at: https://ai.google.dev/");
                yield break;
            }

            string model = GetSelectedModel();
            string apiKey = MyMod.Settings.apiKey;
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            string requestJson = CreateGeminiRequestJson(prompt);

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

                if (!hasError)
                {
                    string reply = ParseGeminiReply(responseText);

                    if (reply.StartsWith("⚠ ERROR:") || reply.StartsWith("ERROR:"))
                    {
                        onResponse?.Invoke(reply);
                        yield break;
                    }

                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    LogDebugResponse("GeminiAPI", $"Used model: {model}\nReply: {reply}");
                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogDebugResponse("GeminiAPI_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                LogDebugResponse("GeminiAPI_ERROR", $"Status: {request.responseCode}\nModel: {model}\nResponse: {responseText}");
                onResponse?.Invoke($"⚠ ERROR: Gemini API connection failed\n\nModel: {model}\nAttempts: {maxRetries}\nError: {request.error}");
                yield break;
            }
        }

        private static string CreateGeminiRequestJson(string prompt)
        {
            string escapedPrompt = EscapeJson(prompt);

            return $@"{{
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
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 REQUESTS
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToPlayer2(Pawn pawn, string userInput, Action<string> onResponse)
        {
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
                onResponse?.Invoke("⚠ ERROR: Player2 is not running\n\nDownload Player2 for free from: https://player2.game/");
                yield break;
            }

            if (!healthRequest.downloadHandler.text.Contains("client_version"))
            {
                onResponse?.Invoke("⚠ ERROR: Player2 not responding correctly\n\nMake sure the app is running, or reinstall from: https://player2.game/");
                yield break;
            }

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

            string jsonBody = BuildMessagesJson(messages);

            if (MyMod.Settings?.debugMode == true)
                LogPlayer2Debug("REQUEST", jsonBody);

            int maxRetries = 3;
            float retryDelay = 1f;

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

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogPlayer2Debug("RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                if (MyMod.Settings?.debugMode == true)
                    LogPlayer2Debug("ERROR_RESPONSE", $"Status: {request.responseCode}\n{responseText}");

                onResponse?.Invoke($"⚠ ERROR: Player2 connection failed after {maxRetries} attempts\n\nError: {request.error}");
                yield break;
            }
        }

        public static IEnumerator SendRequestToPlayer2WithPrompt(string fullPrompt, Action<string> onResponse)
        {
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
                onResponse?.Invoke("⚠ ERROR: Player2 is not running\n\nDownload Player2 for free from: https://player2.game/");
                yield break;
            }

            if (!healthRequest.downloadHandler.text.Contains("client_version"))
            {
                onResponse?.Invoke("⚠ ERROR: Player2 not responding correctly\n\nMake sure the app is running, or reinstall from: https://player2.game/");
                yield break;
            }

            string endpoint = "http://127.0.0.1:4315/v1/chat/completions";

            var messages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "role", "user" }, { "content", fullPrompt } }
            };

            string jsonBody = BuildMessagesJson(messages);

            if (MyMod.Settings?.debugMode == true)
                LogPlayer2Debug("PROMPT_REQUEST", jsonBody);

            int maxRetries = 3;
            float retryDelay = 1f;

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

                if (!hasError)
                {
                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("PROMPT_RESPONSE", responseText);

                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("PROMPT_FINAL", reply);

                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogPlayer2Debug("PROMPT_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                if (MyMod.Settings?.debugMode == true)
                    LogPlayer2Debug("PROMPT_ERROR", $"Status: {request.responseCode}\n{responseText}");

                onResponse?.Invoke($"⚠ ERROR: Player2 connection failed after {maxRetries} attempts\n\nError: {request.error}");
                yield break;
            }
        }

        public static IEnumerator SendRequestToPlayer2Storyteller(string jsonPrompt, Action<string> onResponse)
        {
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
                onResponse?.Invoke("⚠ ERROR: Player2 is not running\n\nDownload Player2 for free from: https://player2.game/");
                yield break;
            }

            if (!healthRequest.downloadHandler.text.Contains("client_version"))
            {
                onResponse?.Invoke("⚠ ERROR: Player2 not responding correctly\n\nMake sure the app is running, or reinstall from: https://player2.game/");
                yield break;
            }

            string endpoint = "http://127.0.0.1:4315/v1/chat/completions";

            if (MyMod.Settings?.debugMode == true)
                LogPlayer2Debug("STORYTELLER_REQUEST", jsonPrompt);

            int maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPrompt)),
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

                if (!hasError)
                {
                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("STORYTELLER_RESPONSE", responseText);

                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    if (MyMod.Settings?.debugMode == true)
                        LogPlayer2Debug("STORYTELLER_FINAL", reply);

                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogPlayer2Debug("STORYTELLER_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");

                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                if (MyMod.Settings?.debugMode == true)
                    LogPlayer2Debug("STORYTELLER_ERROR", $"Status: {request.responseCode}\n{responseText}");

                onResponse?.Invoke($"⚠ ERROR: Player2 storyteller connection failed\n\nAttempts: {maxRetries}\nError: {request.error}");
                yield break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LOCAL MODEL
        // ═══════════════════════════════════════════════════════════════

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
                onResponse?.Invoke($"⚠ ERROR: Local model connection failed\n\nEndpoint: {endpoint}\nModel: {modelName}\nError: {request.error}");
                yield break;
            }

            string text = ParseStandardLLMResponse(responseText);

            if (text.StartsWith("⚠ ERROR:") || text.StartsWith("ERROR:"))
            {
                onResponse?.Invoke(text);
                yield break;
            }

            text = TrimTextAfterHashtags(text);
            text = CleanResponse(text);

            LogDebugResponse("LocalModel", text);
            onResponse?.Invoke(text);
        }

        // ═══════════════════════════════════════════════════════════════
        // OPENROUTER
        // ═══════════════════════════════════════════════════════════════

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
                onResponse?.Invoke($"⚠ ERROR: OpenRouter connection failed\n\nEndpoint: {endpoint}\nError: {request.error}");
                yield break;
            }

            string text = ParseStandardLLMResponse(responseText);

            if (text.StartsWith("⚠ ERROR:") || text.StartsWith("ERROR:"))
            {
                onResponse?.Invoke(text);
                yield break;
            }

            text = TrimTextAfterHashtags(text);
            text = CleanResponse(text);

            LogDebugResponse("OpenRouter", text);
            onResponse?.Invoke(text);
        }

        // ═══════════════════════════════════════════════════════════════
        // MEMORY
        // ═══════════════════════════════════════════════════════════════

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
                if (recentTurns.Count > 20)
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

        // ═══════════════════════════════════════════════════════════════
        // PARSING
        // ═══════════════════════════════════════════════════════════════

        private static string ParseGeminiReply(string json)
        {
            try
            {
                var parsed = JSON.Parse(json);

                if (parsed["candidates"] != null && parsed["candidates"].AsArray.Count > 0)
                {
                    var candidate = parsed["candidates"][0];
                    if (candidate["content"] != null && candidate["content"]["parts"] != null)
                    {
                        var parts = candidate["content"]["parts"].AsArray;
                        if (parts.Count > 0 && parts[0]["text"] != null)
                            return parts[0]["text"].Value;
                    }
                }

                if (parsed["text"] != null)
                    return parsed["text"].Value;

                var textNodes = FindTextInJSON(parsed);
                if (textNodes.Count > 0)
                    return textNodes[0];

                return "⚠ ERROR: No text found in Gemini response\n\nThe API returned an unexpected format";
            }
            catch (Exception ex)
            {
                LogDebugResponse("ParseGemini_ERROR", $"JSON: {json}\nError: {ex.Message}");
                return "⚠ ERROR: Failed to parse Gemini response\n\nTry again or check debug logs";
            }
        }

        private static List<string> FindTextInJSON(JSONNode node)
        {
            var results = new List<string>();

            if (node.IsString && !string.IsNullOrEmpty(node.Value) && node.Value.Length > 10)
            {
                results.Add(node.Value);
            }
            else if (node.IsArray)
            {
                foreach (JSONNode item in node.AsArray)
                    results.AddRange(FindTextInJSON(item));
            }
            else if (node.IsObject)
            {
                foreach (var kvp in node.AsObject)
                {
                    if (kvp.Key == "text" && kvp.Value.IsString && !string.IsNullOrEmpty(kvp.Value.Value))
                        results.Add(kvp.Value.Value);
                    else
                        results.AddRange(FindTextInJSON(kvp.Value));
                }
            }

            return results;
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
                    if (fullText.StartsWith("\"") && fullText.EndsWith("\"") && fullText.Length < 50)
                        return fullText.Substring(1, fullText.Length - 2);
                    return fullText;
                }

                var quoted = Regex.Match(json, "\"([^\"]{100,})\"");
                if (quoted.Success)
                    return quoted.Groups[1].Value;

                return "⚠ ERROR: Unrecognized response format\n\nThe API returned an unexpected structure";
            }
            catch
            {
                return "⚠ ERROR: Failed to parse API response\n\nCheck debug logs for details";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════════════════

        private static string BuildMessagesJson(List<Dictionary<string, string>> messages)
        {
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
            return jsonPayload.ToString();
        }

        private static string CleanResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            text = text.Trim();

            var colonistNamePattern = @"^([A-Za-z]+):\s*\1:\s*(.*)$";
            var match = Regex.Match(text, colonistNamePattern);
            if (match.Success)
            {
                text = match.Groups[2].Value.Trim();
            }
            else
            {
                var simpleNamePattern = @"^([A-Za-z]+):\s*(.*)$";
                var simpleMatch = Regex.Match(text, simpleNamePattern);
                if (simpleMatch.Success)
                    text = simpleMatch.Groups[2].Value.Trim();
            }

            if (text.StartsWith("\"") && text.EndsWith("\""))
            {
                string unwrapped = text.Substring(1, text.Length - 2);
                if (text.Length < 30 || !unwrapped.Contains(" ") || unwrapped.Split(' ').Length < 3)
                    return unwrapped.Trim();
            }

            string[] prefixesToRemove = {
                "As a colonist, ", "As someone who ", "I would say ",
                "My response would be ", "I think ", "Well, ", "You know, "
            };

            foreach (string prefix in prefixesToRemove)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(prefix.Length).Trim();
                    break;
                }
            }

            string[] suffixesToRemove = { " #", " [END]", " </response>", " ```" };

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

        private static string EscapeJson(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string TrimTextAfterHashtags(string text)
        {
            int hashtagIndex = text.IndexOf(" #");
            return hashtagIndex > 0 ? text.Substring(0, hashtagIndex).Trim() : text;
        }

        private static void LogDebugResponse(string sourceName, string responseText)
        {
            if (MyMod.Settings?.debugMode != true) return;
            try
            {
                string safeSource = sourceName.Replace(" ", "_");
                string filename = $"{safeSource}_Response_LATEST.txt";
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderPath = Path.Combine(desktopPath, "EchoColony_Debug");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, filename);
                string debugContent = $"=== {sourceName} RESPONSE DEBUG LOG ===\n";
                debugContent += $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                debugContent += "".PadRight(50, '=') + "\n\n";
                debugContent += responseText;

                File.WriteAllText(fullPath, debugContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to save debug response: {ex.Message}");
            }
        }

        private static void LogPlayer2Debug(string type, string content)
        {
            if (MyMod.Settings?.debugMode != true) return;
            try
            {
                string filename = $"Player2_{type}_LATEST.txt";
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderPath = Path.Combine(desktopPath, "EchoColony_Debug");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, filename);
                string debugContent = $"=== PLAYER2 {type} DEBUG LOG ===\n";
                debugContent += $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
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