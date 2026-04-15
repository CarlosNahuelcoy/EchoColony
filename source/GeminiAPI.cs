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
            Name       = name;
            IsAdvanced = isAdvanced;
        }
    }

    public static class GeminiAPI
    {
        // ═══════════════════════════════════════════════════════════════
        // MODEL SELECTION
        // ═══════════════════════════════════════════════════════════════

        public static string GetSelectedModel()
        {
            if (MyMod.Settings == null)
                return "gemini-2.0-flash-001";

            if (!string.IsNullOrEmpty(MyMod.Settings.selectedModel))
            {
                if (MyMod.Settings.debugMode)
                    LogDebugResponse("ModelSelection", $"Using user-selected model: {MyMod.Settings.selectedModel}");
                return MyMod.Settings.selectedModel;
            }

            if (MyMod.Settings.debugMode)
                LogDebugResponse("ModelSelection", "No model selected, using default: gemini-2.0-flash-001");

            return "gemini-2.0-flash-001";
        }

        public static string GetBestAvailableModel(bool useAdvanced = false) => GetSelectedModel();

        // ═══════════════════════════════════════════════════════════════
        // VISION — Screenshot capture and resize
        // ═══════════════════════════════════════════════════════════════

        public static string CaptureScreenshotBase64()
        {
            if (MyMod.Settings?.enableVision != true)
                return null;

            try
            {
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                {
                    Log.Warning("[EchoColony] Vision: CaptureScreenshotAsTexture returned null");
                    return null;
                }

                Texture2D resized = ResizeTexture(screenshot, 800, 450);
                UnityEngine.Object.Destroy(screenshot);

                byte[] jpegBytes = resized.EncodeToJPG(75);
                UnityEngine.Object.Destroy(resized);

                string base64 = Convert.ToBase64String(jpegBytes);
                int sizeKB    = jpegBytes.Length / 1024;

                Log.Message($"[EchoColony] Vision: Screenshot captured — {sizeKB} KB JPEG at 800×450");

                if (MyMod.Settings?.debugMode == true)
                    LogDebugResponse("Vision_Screenshot", $"Screenshot captured: {sizeKB} KB ({jpegBytes.Length} bytes)");

                return base64;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Vision: Screenshot capture failed: {ex.Message}");
                return null;
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public static IEnumerator DebugDescribeScreenshot(string imageBase64)
        {
            if (MyMod.Settings?.debugMode != true || string.IsNullOrEmpty(imageBase64))
                yield break;

            const string descPrompt =
                "You are a visual analysis assistant. Describe EXACTLY and in full detail " +
                "everything you see in this screenshot: terrain, structures, characters, " +
                "animals, corpses, blood, weather, items on the ground, any UI elements. " +
                "Be exhaustive and literal. Do not interpret or narrate — just list and " +
                "describe what is visible.";

            string result = null;

            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Gemini:
                {
                    string body = BuildGeminiVisionBodyFromText(descPrompt, imageBase64);
                    yield return SendRequestToGemini(body, r => result = r);
                    break;
                }
                case ModelSource.Player2:
                {
                    var messages = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "role", "user" }, { "content", descPrompt } }
                    };
                    string jsonBody  = BuildMessagesJsonWithVision(messages, imageBase64);
                    string endpoint  = Player2AuthManager.WebApiBase + "/chat/completions";
                    var request      = new UnityWebRequest(endpoint, "POST")
                    {
                        uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
                    string authHeader = Player2AuthManager.GetAuthHeader();
                    if (!string.IsNullOrEmpty(authHeader))
                        request.SetRequestHeader("Authorization", authHeader);
                    yield return request.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                    if (request.result == UnityWebRequest.Result.Success)
#else
                    if (!request.isNetworkError && !request.isHttpError)
#endif
                        result = ParseStandardLLMResponse(request.downloadHandler.text);
                    break;
                }
                case ModelSource.OpenRouter:
                {
                    string jsonBody = BuildOpenRouterVisionJson(descPrompt, imageBase64);
                    var request     = new UnityWebRequest(MyMod.Settings.openRouterEndpoint, "POST")
                    {
                        uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {MyMod.Settings.openRouterApiKey}");
                    yield return request.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                    if (request.result == UnityWebRequest.Result.Success)
#else
                    if (!request.isNetworkError && !request.isHttpError)
#endif
                        result = ParseStandardLLMResponse(request.downloadHandler.text);
                    break;
                }
                default:
                    yield break;
            }

            if (string.IsNullOrWhiteSpace(result)) yield break;

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "EchoColony_Debug");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path    = Path.Combine(folder, "Vision_Description_LATEST.txt");
                string content = $"=== VISION DESCRIPTION DEBUG ===\n" +
                                 $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"Provider: {MyMod.Settings.modelSource}\n" +
                                 $"Resolution: 800x450 JPEG\n" +
                                 "".PadRight(50, '=') + "\n\n" +
                                 result;

                File.WriteAllText(path, content);
                Log.Message($"[EchoColony] Vision debug description saved to: {path}");
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to save vision debug description: {ex.Message}");
            }
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
            var request     = UnityWebRequest.Get(endpoint);
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
                var parsed      = JSON.Parse(request.downloadHandler.text);
                var modelsArray = parsed["models"]?.AsArray;

                if (modelsArray == null) { onComplete?.Invoke(null); yield break; }

                var result = new List<GeminiModelInfo>();

                foreach (JSONNode modelNode in modelsArray)
                {
                    bool supportsGenerate = false;
                    var methods = modelNode["supportedGenerationMethods"]?.AsArray;
                    if (methods != null)
                        foreach (JSONNode method in methods)
                            if (method.Value == "generateContent") { supportsGenerate = true; break; }

                    if (!supportsGenerate) continue;

                    string fullName = modelNode["name"]?.Value ?? "";
                    string modelId  = fullName.StartsWith("models/") ? fullName.Substring(7) : fullName;

                    if (string.IsNullOrEmpty(modelId)) continue;

                    bool isAdvanced = modelId.Contains("-pro") || modelId.Contains("thinking");
                    result.Add(new GeminiModelInfo(modelId, isAdvanced));
                }

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

        public static IEnumerator GetResponseFromModel(Pawn pawn, string prompt, Action<string> onResponse, string imageBase64 = null)
        {
            if (MyMod.Settings == null)
            {
                onResponse?.Invoke("⚠ ERROR: Settings not loaded");
                yield break;
            }

            bool isAnimal = pawn != null && pawn.RaceProps.Animal;
            bool isMech   = pawn != null && pawn.RaceProps.IsMechanoid;

            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Player2:
                    if (isAnimal || isMech)
                        yield return SendRequestToPlayer2WithPrompt(prompt, onResponse);
                    else
                        yield return SendRequestToPlayer2(pawn, prompt, onResponse, imageBase64);
                    yield break;

                case ModelSource.Local:
                    yield return SendRequestToLocalModel(prompt, onResponse);
                    yield break;

                case ModelSource.OpenRouter:
                    yield return SendRequestToOpenRouter(prompt, onResponse, imageBase64);
                    yield break;

                case ModelSource.Gemini:
                    yield return SendRequestToGemini(prompt, onResponse, imageBase64);
                    yield break;

                case ModelSource.Custom:
                    yield return SendRequestToCustomProvider(prompt, onResponse);
                    yield break;

                default:
                    onResponse?.Invoke("⚠ ERROR: Unknown model source - Check mod settings");
                    yield break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GEMINI REQUEST
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToGemini(string prompt, Action<string> onResponse, string imageBase64 = null)
        {
            if (string.IsNullOrEmpty(MyMod.Settings.apiKey))
            {
                onResponse?.Invoke("⚠ ERROR: Missing Gemini API Key\n\nSet your API key in mod settings\nGet one free at: https://ai.google.dev/");
                yield break;
            }

            string model    = GetSelectedModel();
            string apiKey   = MyMod.Settings.apiKey;
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            string requestJson = !string.IsNullOrEmpty(imageBase64)
                ? InjectImageIntoGeminiJson(prompt, imageBase64)
                : CreateGeminiRequestJson(prompt);

            if (MyMod.Settings?.debugMode == true)
                LogDebugResponse("Gemini_Vision_Request", requestJson.Length > 200
                    ? requestJson.Substring(0, 200) + "... [truncated]"
                    : requestJson);

            int   maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson)),
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
                    LogDebugResponse("GeminiAPI", $"Used model: {model}\nVision: {!string.IsNullOrEmpty(imageBase64)}\nReply: {reply}");
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

        private static string InjectImageIntoGeminiJson(string existingJson, string imageBase64)
        {
            try
            {
                var parsed   = JSON.Parse(existingJson);
                var contents = parsed["contents"]?.AsArray;

                if (contents != null && contents.Count > 0)
                {
                    JSONNode lastUserNode = null;
                    for (int i = contents.Count - 1; i >= 0; i--)
                    {
                        string role = contents[i]["role"]?.Value ?? "";
                        if (role == "user" || string.IsNullOrEmpty(role))
                        {
                            lastUserNode = contents[i];
                            break;
                        }
                    }

                    if (lastUserNode != null)
                    {
                        var parts = lastUserNode["parts"]?.AsArray;
                        if (parts != null)
                        {
                            var inlineDataNode       = new JSONObject();
                            var innerData            = new JSONObject();
                            innerData["mimeType"]    = "image/jpeg";
                            innerData["data"]        = imageBase64;
                            inlineDataNode["inlineData"] = innerData;
                            parts.Add(inlineDataNode);
                            Log.Message("[EchoColony] Vision: Injected inlineData into existing Gemini JSON");
                            return parsed.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Vision: Could not inject into Gemini JSON, using fallback: {ex.Message}");
            }
            return BuildGeminiVisionBodyFromText(existingJson, imageBase64);
        }

        private static string BuildGeminiVisionBodyFromText(string textContent, string imageBase64)
        {
            var body     = new JSONObject();
            var contents = new JSONArray();
            var userTurn = new JSONObject();
            var parts    = new JSONArray();

            var textPart     = new JSONObject();
            textPart["text"] = textContent;
            parts.Add(textPart);

            var imagePart            = new JSONObject();
            var inlineData           = new JSONObject();
            inlineData["mimeType"]   = "image/jpeg";
            inlineData["data"]       = imageBase64;
            imagePart["inlineData"]  = inlineData;
            parts.Add(imagePart);

            userTurn["role"]    = "user";
            userTurn["parts"]   = parts;
            contents.Add(userTurn);
            body["contents"]    = contents;
            return body.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 REQUESTS
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToPlayer2(Pawn pawn, string userInput, Action<string> onResponse, string imageBase64 = null)
        {
            if (!Player2AuthManager.IsAuthenticated)
            {
                onResponse?.Invoke("⚠ ERROR: Not connected to Player2\n\nGo to Mod Settings → AI Model Configuration and connect your account.");
                yield break;
            }

            string healthCheckUrl = Player2AuthManager.WebApiBase + "/health";
            var healthRequest     = UnityWebRequest.Get(healthCheckUrl);
            healthRequest.timeout = 4;
            string healthAuth     = Player2AuthManager.GetAuthHeader();
            if (!string.IsNullOrEmpty(healthAuth))
                healthRequest.SetRequestHeader("Authorization", healthAuth);
            healthRequest.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

            yield return healthRequest.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (healthRequest.result != UnityWebRequest.Result.Success)
#else
            if (healthRequest.isNetworkError || healthRequest.isHttpError)
#endif
            {
                onResponse?.Invoke("⚠ ERROR: Could not reach Player2 servers\n\nCheck your internet connection.");
                yield break;
            }

            string endpoint = Player2AuthManager.WebApiBase + "/chat/completions";

            RebuildMemoryFromChat(pawn);

            var (systemPrompt, userMessage) = ColonistPromptContextBuilder.BuildForPlayer2(pawn, userInput);
            EchoMemory.SetSystemPrompt(systemPrompt);

            var messages = new List<Dictionary<string, string>>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(new Dictionary<string, string> { { "role", "system" }, { "content", systemPrompt } });

            foreach (var entry in EchoMemory.GetRecentTurns())
                messages.Add(new Dictionary<string, string> { { "role", entry.Item1 }, { "content", entry.Item2 } });

            if (!string.IsNullOrEmpty(imageBase64) && MyMod.Settings?.enableVision == true)
            {
                if (messages.Count > 0 && messages[0]["role"] == "system")
                    messages[0]["content"] += "\n\n[VISION] You have been given a screenshot of the current game state. " +
                        "Use it to describe your surroundings and what's happening around you when relevant. " +
                        "Reference visual details naturally as if you can see them.";
            }

            messages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", userMessage } });

            bool   useVision = !string.IsNullOrEmpty(imageBase64) && MyMod.Settings?.enableVision == true;
            string jsonBody  = useVision
                ? BuildMessagesJsonWithVision(messages, imageBase64)
                : BuildMessagesJson(messages);

            if (MyMod.Settings?.debugMode == true)
                LogPlayer2Debug("REQUEST", useVision ? "[VISION REQUEST — image payload omitted from log]" : jsonBody);

            int   maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
                string auth = Player2AuthManager.GetAuthHeader();
                if (!string.IsNullOrEmpty(auth))
                    request.SetRequestHeader("Authorization", auth);

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
                bool hasError = request.result != UnityWebRequest.Result.Success;
#else
                bool hasError = request.isNetworkError || request.isHttpError;
#endif

                if (!hasError)
                {
                    Log.Message($"[EchoColony] Player2 Web API raw response: {responseText}");
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("RESPONSE", responseText);
                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);
                    EchoMemory.AddTurn("user", userMessage);
                    EchoMemory.AddTurn("assistant", reply);
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("FINAL_REPLY", reply);
                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogPlayer2Debug("RETRY", $"Attempt {attempt + 1}/{maxRetries} failed. Retrying in {retryDelay}s...\n{responseText}");
                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("ERROR_RESPONSE", $"Status: {request.responseCode}\n{responseText}");
                onResponse?.Invoke($"⚠ ERROR: Player2 connection failed after {maxRetries} attempts\n\nError: {request.error}");
                yield break;
            }
        }

        public static IEnumerator SendRequestToPlayer2WithPrompt(string fullPrompt, Action<string> onResponse)
        {
            if (!Player2AuthManager.IsAuthenticated)
            {
                onResponse?.Invoke("⚠ ERROR: Not connected to Player2\n\nGo to Mod Settings → AI Model Configuration and connect your account.");
                yield break;
            }

            string endpoint = Player2AuthManager.WebApiBase + "/chat/completions";

            var messages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "role", "user" }, { "content", fullPrompt } }
            };
            string jsonBody = BuildMessagesJson(messages);

            if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("PROMPT_REQUEST", jsonBody);

            int   maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
                string auth = Player2AuthManager.GetAuthHeader();
                if (!string.IsNullOrEmpty(auth))
                    request.SetRequestHeader("Authorization", auth);

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
                bool hasError = request.result != UnityWebRequest.Result.Success;
#else
                bool hasError = request.isNetworkError || request.isHttpError;
#endif

                if (!hasError)
                {
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("PROMPT_RESPONSE", responseText);
                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("PROMPT_FINAL", reply);
                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                onResponse?.Invoke($"⚠ ERROR: Player2 connection failed after {maxRetries} attempts\n\nError: {request.error}");
                yield break;
            }
        }

        public static IEnumerator SendRequestToPlayer2Storyteller(string jsonPrompt, Action<string> onResponse)
        {
            if (!Player2AuthManager.IsAuthenticated)
            {
                onResponse?.Invoke("⚠ ERROR: Not connected to Player2\n\nGo to Mod Settings → AI Model Configuration and connect your account.");
                yield break;
            }

            string endpoint = Player2AuthManager.WebApiBase + "/chat/completions";

            if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("STORYTELLER_REQUEST", jsonPrompt);

            int   maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPrompt)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
                string auth = Player2AuthManager.GetAuthHeader();
                if (!string.IsNullOrEmpty(auth))
                    request.SetRequestHeader("Authorization", auth);

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

#if UNITY_2020_2_OR_NEWER
                bool hasError = request.result != UnityWebRequest.Result.Success;
#else
                bool hasError = request.isNetworkError || request.isHttpError;
#endif

                if (!hasError)
                {
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("STORYTELLER_RESPONSE", responseText);
                    string reply = ParseStandardLLMResponse(responseText);
                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);
                    if (MyMod.Settings?.debugMode == true) LogPlayer2Debug("STORYTELLER_FINAL", reply);
                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                onResponse?.Invoke($"⚠ ERROR: Player2 storyteller connection failed\n\nError: {request.error}");
                yield break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LOCAL MODEL
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToLocalModel(string prompt, Action<string> onResponse)
        {
            string endpoint  = MyMod.Settings.localModelEndpoint;
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
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
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
            if (text.StartsWith("⚠ ERROR:") || text.StartsWith("ERROR:")) { onResponse?.Invoke(text); yield break; }
            text = TrimTextAfterHashtags(text);
            text = CleanResponse(text);
            LogDebugResponse("LocalModel", text);
            onResponse?.Invoke(text);
        }

        // ═══════════════════════════════════════════════════════════════
        // OPENROUTER
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToOpenRouter(string prompt, Action<string> onResponse, string imageBase64 = null)
        {
            string endpoint  = MyMod.Settings.openRouterEndpoint;
            string apiKey    = MyMod.Settings.openRouterApiKey;
            bool   useVision = !string.IsNullOrEmpty(imageBase64) && MyMod.Settings?.enableVision == true;

            string jsonBody = useVision
                ? BuildOpenRouterVisionJson(prompt, imageBase64)
                : $"{{\"model\": \"{EscapeJson(MyMod.Settings.openRouterModel)}\", \"messages\": [{{\"role\": \"user\", \"content\": \"{EscapeJson(prompt)}\"}}], \"stream\": false}}";

            if (useVision) Log.Message("[EchoColony] Vision: Sending screenshot to OpenRouter");

            var request = new UnityWebRequest(endpoint, "POST")
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
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
            if (text.StartsWith("⚠ ERROR:") || text.StartsWith("ERROR:")) { onResponse?.Invoke(text); yield break; }
            text = TrimTextAfterHashtags(text);
            text = CleanResponse(text);
            LogDebugResponse("OpenRouter", text);
            onResponse?.Invoke(text);
        }

        private static string BuildOpenRouterVisionJson(string textContent, string imageBase64)
        {
            var payload       = new JSONObject();
            payload["model"]  = EscapeJson(MyMod.Settings.openRouterModel);
            payload["stream"] = false;

            var messages    = new JSONArray();
            var userMsg     = new JSONObject();
            userMsg["role"] = "user";

            var contentArr   = new JSONArray();
            var textPart     = new JSONObject();
            textPart["type"] = "text";
            textPart["text"] = textContent;
            contentArr.Add(textPart);

            var imagePart           = new JSONObject();
            imagePart["type"]       = "image_url";
            var imageUrlNode        = new JSONObject();
            imageUrlNode["url"]     = $"data:image/jpeg;base64,{imageBase64}";
            imagePart["image_url"]  = imageUrlNode;
            contentArr.Add(imagePart);

            userMsg["content"]   = contentArr;
            messages.Add(userMsg);
            payload["messages"]  = messages;
            return payload.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // CUSTOM PROVIDER (any OpenAI-compatible endpoint)
        //
        // Works with: LMStudio (server mode), Ollama (OpenAI compat),
        // Groq, Together AI, Mistral API, and any /v1/chat/completions.
        // ═══════════════════════════════════════════════════════════════

        public static IEnumerator SendRequestToCustomProvider(string prompt, Action<string> onResponse)
        {
            string endpoint = MyMod.Settings.customEndpoint;
            string apiKey   = MyMod.Settings.customApiKey;
            string model    = MyMod.Settings.customModelName;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                onResponse?.Invoke("⚠ ERROR: Custom provider endpoint not configured.\n" +
                                   "Go to Mod Settings → AI Model Configuration → Custom Provider.");
                yield break;
            }

            var payload  = new JSONObject();
            if (!string.IsNullOrWhiteSpace(model))
                payload["model"] = model;
            payload["stream"] = false;

            var messages    = new JSONArray();
            var userMsg     = new JSONObject();
            userMsg["role"]    = "user";
            userMsg["content"] = prompt;
            messages.Add(userMsg);
            payload["messages"] = messages;

            string jsonBody = payload.ToString();

            if (MyMod.Settings?.debugMode == true)
                LogDebugResponse("CustomProvider_REQUEST",
                    $"Endpoint: {endpoint}\nModel: {(string.IsNullOrWhiteSpace(model) ? "(server default)" : model)}\n" +
                    $"Body (first 300 chars): {jsonBody.Substring(0, Math.Min(300, jsonBody.Length))}...");

            int   maxRetries = 3;
            float retryDelay = 1f;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var request = new UnityWebRequest(endpoint, "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrWhiteSpace(apiKey))
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

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
                        LogDebugResponse("CustomProvider_RESPONSE", responseText);

                    string reply = ParseStandardLLMResponse(responseText);
                    if (reply.StartsWith("⚠ ERROR:") || reply.StartsWith("ERROR:"))
                    { onResponse?.Invoke(reply); yield break; }

                    reply = TrimTextAfterHashtags(reply);
                    reply = CleanResponse(reply);

                    if (MyMod.Settings?.debugMode == true)
                        LogDebugResponse("CustomProvider_FINAL", reply);

                    onResponse?.Invoke(reply);
                    yield break;
                }

                if (request.responseCode == 429 || request.responseCode == 500 || request.responseCode == 503)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            LogDebugResponse("CustomProvider_RETRY",
                                $"Attempt {attempt + 1}/{maxRetries} failed ({request.responseCode}). " +
                                $"Retrying in {retryDelay}s...\n{responseText}");
                        yield return new WaitForSeconds(retryDelay);
                        retryDelay *= 2f;
                        continue;
                    }
                }

                if (MyMod.Settings?.debugMode == true)
                    LogDebugResponse("CustomProvider_ERROR", $"Status: {request.responseCode}\n{responseText}");

                onResponse?.Invoke(
                    $"⚠ ERROR: Custom provider connection failed after {maxRetries} attempts\n\n" +
                    $"Endpoint: {endpoint}\n" +
                    $"Model: {(string.IsNullOrWhiteSpace(model) ? "(server default)" : model)}\n" +
                    $"HTTP {request.responseCode}: {request.error}\n\n" +
                    $"Check that your endpoint is running and the API key is correct.");
                yield break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MEMORY
        // ═══════════════════════════════════════════════════════════════

        public static class EchoMemory
        {
            public static string LastSystemPrompt;
            private static List<Tuple<string, string>> recentTurns = new List<Tuple<string, string>>();

            public static void SetSystemPrompt(string prompt) { LastSystemPrompt = prompt; }

            public static void AddTurn(string role, string text)
            {
                recentTurns.Add(Tuple.Create(role, text));
                if (recentTurns.Count > 20) recentTurns.RemoveAt(0);
            }

            public static List<Tuple<string, string>> GetRecentTurns() =>
                new List<Tuple<string, string>>(recentTurns);

            public static void Clear() { recentTurns.Clear(); }
        }

        public static void RebuildMemoryFromChat(Pawn pawn)
        {
            EchoMemory.Clear();
            var lines = ChatGameComponent.Instance.GetChat(pawn);
            foreach (var line in lines)
            {
                if (line.StartsWith("[USER]"))
                    EchoMemory.AddTurn("user", line.Substring(6).Trim());
                else if (line.StartsWith(pawn.LabelShort + ":"))
                    EchoMemory.AddTurn("assistant", line.Substring(pawn.LabelShort.Length + 1).Trim());
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
                if (parsed["text"] != null) return parsed["text"].Value;
                var textNodes = FindTextInJSON(parsed);
                if (textNodes.Count > 0) return textNodes[0];
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
                results.Add(node.Value);
            else if (node.IsArray)
                foreach (JSONNode item in node.AsArray)
                    results.AddRange(FindTextInJSON(item));
            else if (node.IsObject)
                foreach (var kvp in node.AsObject)
                {
                    if (kvp.Key == "text" && kvp.Value.IsString && !string.IsNullOrEmpty(kvp.Value.Value))
                        results.Add(kvp.Value.Value);
                    else
                        results.AddRange(FindTextInJSON(kvp.Value));
                }
            return results;
        }

        private static string ParseStandardLLMResponse(string json)
        {
            try
            {
                var parsed = JSON.Parse(json);
                if (parsed["response"] != null) return parsed["response"];
                if (parsed["choices"] != null)
                {
                    var choice = parsed["choices"][0];
                    if (choice["message"]?["content"] != null) return choice["message"]["content"];
                    if (choice["text"] != null) return choice["text"];
                }
                if (parsed["results"]?[0]["text"] != null) return parsed["results"][0]["text"];
                if (parsed["text"] != null)
                {
                    string fullText = parsed["text"];
                    if (fullText.StartsWith("\"") && fullText.EndsWith("\"") && fullText.Length < 50)
                        return fullText.Substring(1, fullText.Length - 2);
                    return fullText;
                }
                var quoted = Regex.Match(json, "\"([^\"]{100,})\"");
                if (quoted.Success) return quoted.Groups[1].Value;
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
            var jsonPayload  = new JSONObject();
            var jsonMessages = new JSONArray();
            foreach (var msg in messages)
            {
                var jsonMsg        = new JSONObject();
                jsonMsg["role"]    = msg["role"];
                jsonMsg["content"] = msg["content"];
                jsonMessages.Add(jsonMsg);
            }
            jsonPayload["messages"] = jsonMessages;
            jsonPayload["stream"]   = false;
            return jsonPayload.ToString();
        }

        private static string BuildMessagesJsonWithVision(List<Dictionary<string, string>> messages, string imageBase64)
        {
            var jsonPayload  = new JSONObject();
            var jsonMessages = new JSONArray();

            for (int i = 0; i < messages.Count; i++)
            {
                var msg     = messages[i];
                var jsonMsg = new JSONObject();
                jsonMsg["role"] = msg["role"];

                bool isLastUserMsg = (i == messages.Count - 1) && msg["role"] == "user";

                if (isLastUserMsg)
                {
                    var contentArr   = new JSONArray();
                    var textPart     = new JSONObject();
                    textPart["type"] = "text";
                    textPart["text"] = msg["content"];
                    contentArr.Add(textPart);

                    var imagePart          = new JSONObject();
                    imagePart["type"]      = "image_url";
                    var imageUrlNode       = new JSONObject();
                    imageUrlNode["url"]    = $"data:image/jpeg;base64,{imageBase64}";
                    imagePart["image_url"] = imageUrlNode;
                    contentArr.Add(imagePart);

                    jsonMsg["content"] = contentArr;
                }
                else
                {
                    jsonMsg["content"] = msg["content"];
                }

                jsonMessages.Add(jsonMsg);
            }

            jsonPayload["messages"] = jsonMessages;
            jsonPayload["stream"]   = false;
            return jsonPayload.ToString();
        }

        private static string CleanResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            text = text.Trim();

            var colonistNamePattern = @"^([A-Za-z]+):\s*\1:\s*(.*)$";
            var match = Regex.Match(text, colonistNamePattern);
            if (match.Success) text = match.Groups[2].Value.Trim();
            else
            {
                var simpleMatch = Regex.Match(text, @"^([A-Za-z]+):\s*(.*)$");
                if (simpleMatch.Success) text = simpleMatch.Groups[2].Value.Trim();
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
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { text = text.Substring(prefix.Length).Trim(); break; }

            string[] suffixesToRemove = { " #", " [END]", " </response>", " ```" };
            foreach (string suffix in suffixesToRemove)
                if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                { text = text.Substring(0, text.Length - suffix.Length).Trim(); break; }

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
                string safeSource  = sourceName.Replace(" ", "_");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderPath  = Path.Combine(desktopPath, "EchoColony_Debug");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                string fullPath     = Path.Combine(folderPath, $"{safeSource}_Response_LATEST.txt");
                string debugContent = $"=== {sourceName} RESPONSE DEBUG LOG ===\n" +
                                      $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                      "".PadRight(50, '=') + "\n\n" + responseText;
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
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderPath  = Path.Combine(desktopPath, "EchoColony_Debug");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                string latestPath = Path.Combine(folderPath, $"Player2_{type}_LATEST.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string historyPath = Path.Combine(folderPath, $"Player2_{type}_{timestamp}.txt");
                string debugContent = $"=== PLAYER2 {type} DEBUG LOG ===\n" +
                                      $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                      $"Type: {type}\n" +
                                      "".PadRight(50, '=') + "\n\n" + content;
                File.WriteAllText(latestPath, debugContent);
                File.WriteAllText(historyPath, debugContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to save Player2 debug log: {ex.Message}");
            }
        }
    }
}