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
        private static readonly List<GeminiModelInfo> availableModels = new List<GeminiModelInfo>
        {
            new GeminiModelInfo("gemini-2.5-flash", false),
            new GeminiModelInfo("gemini-2.5-flash-lite", false),
            new GeminiModelInfo("gemini-2.5-flash-preview-09-2025", false),
            new GeminiModelInfo("gemini-2.0-flash-001", false),
            new GeminiModelInfo("gemini-2.0-flash-lite-001", false),
            new GeminiModelInfo("gemini-2.5-pro", true),
            new GeminiModelInfo("gemini-2.0-flash-thinking-exp", true),
            new GeminiModelInfo("gemini-2.0-pro-exp", true)
        };

        public static IEnumerator GetResponseFromModel(Pawn pawn, string prompt, Action<string> onResponse)
        {
            if (MyMod.Settings == null)
            {
                onResponse?.Invoke("⚠ ERROR: Settings not loaded");
                yield break;
            }

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
                    onResponse?.Invoke("⚠ ERROR: Unknown model source - Check mod settings");
                    yield break;
            }
        }

        public static string GetBestAvailableModel(bool useAdvanced)
        {
            if (MyMod.Settings?.modelPreferences != null && !MyMod.Settings.modelPreferences.useAutoSelection)
            {
                string preferredModel = useAdvanced ? 
                    MyMod.Settings.modelPreferences.preferredAdvancedModel : 
                    MyMod.Settings.modelPreferences.preferredFastModel;
                    
                if (!string.IsNullOrEmpty(preferredModel))
                {
                    var preferredExists = availableModels.Any(m => m.Name == preferredModel && m.IsAdvanced == useAdvanced);
                    
                    if (preferredExists)
                    {
                        if (MyMod.Settings?.debugMode == true)
                        {
                            LogDebugResponse("ModelSelection", $"Using user preferred model: {preferredModel}");
                        }
                        return preferredModel;
                    }
                }
            }

            var candidateModels = availableModels.Where(m => m.IsAdvanced == useAdvanced).ToList();
            
            if (candidateModels.Count == 0)
            {
                candidateModels = availableModels.ToList();
            }

            return SelectBestModel(candidateModels, useAdvanced);
        }

        private static string SelectBestModel(List<GeminiModelInfo> candidates, bool preferAdvanced)
        {
            var modelPriorities = new Dictionary<string, int>
            {
                ["gemini-2.0-flash-001"] = 105,
                ["gemini-2.5-flash"] = 100,
                ["gemini-2.5-flash-lite"] = 95,
                ["gemini-2.5-flash-preview-09-2025"] = 90,
                ["gemini-2.0-flash-lite-001"] = 80,
                ["gemini-2.5-pro"] = 100,
                ["gemini-2.0-flash-thinking-exp"] = 95,
                ["gemini-2.0-pro-exp"] = 90,
            };

            var sortedCandidates = candidates
                .OrderByDescending(m => modelPriorities.ContainsKey(m.Name) ? modelPriorities[m.Name] : 0)
                .ThenByDescending(m => m.Name)
                .ToList();

            string selectedModel = sortedCandidates.First().Name;
            
            if (MyMod.Settings?.debugMode == true)
            {
                LogDebugResponse("ModelSelection", $"Auto-selected model: {selectedModel} (Advanced: {preferAdvanced}) from {candidates.Count} candidates");
            }
            
            return selectedModel;
        }

        public static List<GeminiModelInfo> GetAvailableModels()
        {
            return new List<GeminiModelInfo>(availableModels);
        }

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

            string healthResponse = healthRequest.downloadHandler.text;
            if (!healthResponse.Contains("client_version"))
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
                        {
                            LogPlayer2Debug("RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");
                        }

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

        public static IEnumerator SendRequestToGemini(string prompt, Action<string> onResponse)
        {
            if (string.IsNullOrEmpty(MyMod.Settings.apiKey))
            {
                onResponse?.Invoke("⚠ ERROR: Missing Gemini API Key\n\nSet your API key in mod settings\nGet one free at: https://ai.google.dev/");
                yield break;
            }

            string model = GetBestAvailableModel(MyMod.Settings.ShouldUseAdvancedModel());
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
                        {
                            LogDebugResponse("GeminiAPI_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");
                        }

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

            string healthResponse = healthRequest.downloadHandler.text;
            if (!healthResponse.Contains("client_version"))
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
                        {
                            LogPlayer2Debug("STORYTELLER_RETRY", $"Attempt {attempt + 1}/{maxRetries} failed with code {request.responseCode}. Retrying in {retryDelay}s...\n{responseText}");
                        }

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
                        {
                            return parts[0]["text"].Value;
                        }
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
                {
                    results.AddRange(FindTextInJSON(item));
                }
            }
            else if (node.IsObject)
            {
                foreach (var kvp in node.AsObject)
                {
                    if (kvp.Key == "text" && kvp.Value.IsString && !string.IsNullOrEmpty(kvp.Value.Value))
                    {
                        results.Add(kvp.Value.Value);
                    }
                    else
                    {
                        results.AddRange(FindTextInJSON(kvp.Value));
                    }
                }
            }
            
            return results;
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
                    {
                        return fullText.Substring(1, fullText.Length - 2);
                    }
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

        private static string CleanResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            text = text.Trim();

            var colonistNamePattern = @"^([A-Za-z]+):\s*\1:\s*(.*)$";
            var match = System.Text.RegularExpressions.Regex.Match(text, colonistNamePattern);
            if (match.Success)
            {
                text = match.Groups[2].Value.Trim();
            }
            else
            {
                var simpleNamePattern = @"^([A-Za-z]+):\s*(.*)$";
                var simpleMatch = System.Text.RegularExpressions.Regex.Match(text, simpleNamePattern);
                if (simpleMatch.Success)
                {
                    text = simpleMatch.Groups[2].Value.Trim();
                }
            }

            if (text.StartsWith("\"") && text.EndsWith("\""))
            {
                string unwrapped = text.Substring(1, text.Length - 2);
                
                if (text.Length < 30 || 
                    !unwrapped.Contains(" ") || 
                    unwrapped.Split(' ').Length < 3)
                {
                    return unwrapped.Trim();
                }
            }

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

        private static void LogDebugResponse(string sourceName, string responseText)
        {
            if (MyMod.Settings?.debugMode != true)
                return;
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
            if (MyMod.Settings?.debugMode != true)
                return;
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