using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;
using System.Linq;
using System.Collections;

namespace EchoColony
{
    public class MainButtonWorker_StorytellerChat : MainButtonWorker
    {
        // Control button visibility based on user settings
        public override bool Visible => MyMod.Settings.enableStorytellerButton;

        public override void Activate()
        {
            var existingWindow = Find.WindowStack.Windows
                .OfType<MainTabWindow_StorytellerChat>()
                .FirstOrDefault();
            
            if (existingWindow != null)
            {
                existingWindow.Close();
            }
            else
            {
                Find.WindowStack.Add(new MainTabWindow_StorytellerChat());
            }
        }
    }

    public class MainTabWindow_StorytellerChat : Window
    {
        private Vector2 scrollPos = Vector2.zero;
        private string input = "";
        private Storyteller currentStoryteller;
        private string storytellerDefName => currentStoryteller?.def?.defName ?? "Unknown";
        
        private List<string> chatLog => GetChatData()?.GetChatHistory(storytellerDefName) ?? new List<string>();
        
        private bool sendRequestedViaEnter = false;
        private bool waitingForResponse = false;
        private bool forceScrollToBottom = false;
        
        private List<GeminiMessage> messageHistory = new List<GeminiMessage>();
        
        private int editingIndex = -1;
        private string editedMessage = "";
        
        private Texture2D storytellerPortrait = null;

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

        public MainTabWindow_StorytellerChat()
        {
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.closeOnAccept = false;

            forceScrollToBottom = true;
            currentStoryteller = Find.Storyteller;
            
            if (currentStoryteller == null)
            {
                Log.Error("[EchoColony] No storyteller found!");
                return;
            }

            LoadStorytellerPortrait();
            EnsureChatDataExists();
            RebuildMessageHistory();
            
            if (chatLog.Count == 0)
            {
                AddSystemMessage($"=== Connected to {currentStoryteller.def.label} ===");
            }

            Log.Message($"[EchoColony] Opened storyteller chat with {currentStoryteller.def.label}");
        }

        private void LoadStorytellerPortrait()
        {
            if (currentStoryteller == null) return;
            
            string defName = currentStoryteller.def.defName;
            string texturePath = $"UI/Storyteller/{defName}";
            
            storytellerPortrait = ContentFinder<Texture2D>.Get(texturePath, false);
            
            if (storytellerPortrait == null)
            {
                Log.Message($"[EchoColony] No portrait found for {defName}, using fallback");
                storytellerPortrait = ContentFinder<Texture2D>.Get("UI/Storyteller/Cassandra", false);
            }
        }

        private void RebuildMessageHistory()
        {
            messageHistory.Clear();
            
            foreach (string line in chatLog)
            {
                if (line.StartsWith("[USER]"))
                {
                    string content = line.Substring(6).Trim();
                    messageHistory.Add(new GeminiMessage("user", content));
                }
                else if (line.StartsWith("[STORYTELLER]"))
                {
                    string content = line.Substring(13).Trim();
                    messageHistory.Add(new GeminiMessage("model", content));
                }
            }
            
            if (messageHistory.Count > 20)
            {
                messageHistory = messageHistory.Skip(messageHistory.Count - 20).ToList();
            }
        }

        public override Vector2 InitialSize => new Vector2(850f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            // Portrait and title
            Rect portraitRect = new Rect(0f, 0f, 60f, 60f);
            if (storytellerPortrait != null)
            {
                GUI.DrawTexture(portraitRect, storytellerPortrait);
            }
            else
            {
                // Fallback: gray box
                Widgets.DrawBoxSolid(portraitRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(portraitRect, currentStoryteller?.def.label.Substring(0, 1) ?? "?");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(70f, 10f, inRect.width - 240f, 30f), 
                $"Storyteller: {currentStoryteller?.def.label ?? "Unknown"}");
            Text.Font = GameFont.Small;

            // Scrollable chat log
            float chatHeight = inRect.height - 110f;
            Rect scrollRect = new Rect(0, 45f, inRect.width - 20f, chatHeight);

            float viewHeight = 0f;
            List<float> heights = new List<float>();
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;

            foreach (string msg in chatLog)
            {
                float width = scrollRect.width - 200f;
                string displayText = GetDisplayMessage(msg);
                float height = Text.CalcHeight(displayText, width) + 10f;
                heights.Add(height);
                viewHeight += height + 10f;
            }

            Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            if (forceScrollToBottom)
            {
                scrollPos.y = viewHeight;
                forceScrollToBottom = false;
            }

            float y = 0;
            for (int i = 0; i < chatLog.Count; i++)
            {
                string msg = chatLog[i];
                DrawMessage(new Rect(0, y, viewRect.width, heights[i]), msg, i, viewRect.width);
                y += heights[i] + 10f;
            }

            Widgets.EndScrollView();
            Text.WordWrap = false;

            // Detect Enter without Shift
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "StorytellerInput" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            // Input field
            Rect inputRect = new Rect(0, inRect.height - 60f, inRect.width - 110f, 50f);
            GUI.SetNextControlName("StorytellerInput");
            
            if (editingIndex == -1 && !waitingForResponse && 
                (sendRequestedViaEnter || (Event.current.type == EventType.Layout && input.NullOrEmpty())))
            {
                GUI.FocusControl("StorytellerInput");
            }

            var textStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding = new RectOffset(6, 6, 6, 6)
            };
            input = GUI.TextArea(inputRect, input, 500, textStyle);

            // Send button
            Rect sendRect = new Rect(inRect.width - 100f, inRect.height - 60f, 100f, 30f);
            bool sendClicked = Widgets.ButtonText(sendRect, waitingForResponse ? "..." : "Send");

            if (!waitingForResponse && (sendClicked || sendRequestedViaEnter))
            {
                SendMessage();
                sendRequestedViaEnter = false;
                GUI.FocusControl(null);
            }

            // Clear button
            Rect clearRect = new Rect(inRect.width - 330f, 10f, 100f, 30f);
            if (Widgets.ButtonText(clearRect, "Clear"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Clear chat history with {currentStoryteller.def.label}?",
                    () => {
                        ClearAllChat();
                    }
                ));
            }

            // Customize button
            Rect customizeRect = new Rect(inRect.width - 220f, 10f, 100f, 30f);
            if (Widgets.ButtonText(customizeRect, "Customize"))
            {
                Find.WindowStack.Add(new StorytellerPromptEditor(currentStoryteller));
            }
        }

        private void DrawMessage(Rect rect, string msg, int index, float viewWidth)
        {
            string displayMsg = GetDisplayMessage(msg);
            
            // Separator between messages (except first)
            if (index > 0)
            {
                Widgets.DrawLineHorizontal(rect.x, rect.y - 2f, viewWidth - 200f, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            }
            
            Rect labelRect = new Rect(10f, rect.y + 5f, viewWidth - 200f, rect.height - 5f);

            // Edit mode
            if (editingIndex == index)
            {
                GUI.SetNextControlName($"EditField_{index}");
                editedMessage = Widgets.TextArea(labelRect, editedMessage);
                
                if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y + 5f, 80f, 25f), "Save"))
                {
                    SaveEditedMessage(index, msg);
                }

                if (Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y + 5f, 80f, 25f), "Cancel"))
                {
                    editingIndex = -1;
                    editedMessage = "";
                }
                return;
            }

            // Display message with improved colors
            if (msg.StartsWith("[SYSTEM]"))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.9f, 1f);
            }
            else if (msg.StartsWith("[USER]"))
            {
                GUI.color = new Color(0.8f, 0.9f, 1f, 1f);
                displayMsg = "You: " + displayMsg;
            }
            else if (msg.StartsWith("[STORYTELLER]"))
            {
                GUI.color = new Color(1f, 0.95f, 0.8f, 1f);
                displayMsg = currentStoryteller?.def.label + ": " + displayMsg;
            }
            else if (msg.StartsWith("[SUCCESS]"))
            {
                GUI.color = new Color(0.6f, 1f, 0.6f, 1f);
                displayMsg = "[OK] " + displayMsg;
            }
            else if (msg.StartsWith("[ERROR]"))
            {
                GUI.color = new Color(1f, 0.6f, 0.6f, 1f);
                displayMsg = "[ERROR] " + displayMsg;
            }
            
            Widgets.Label(labelRect, displayMsg);
            GUI.color = Color.white;

            // Action buttons for storyteller messages
            if (msg.StartsWith("[STORYTELLER]") && !msg.EndsWith("..."))
            {
                // Edit button
                if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y, 80f, 25f), "Edit"))
                {
                    editingIndex = index;
                    editedMessage = displayMsg.Replace(currentStoryteller?.def.label + ": ", "");
                }

                // Regenerate button
                bool canRegenerate = index >= 1 && chatLog[index - 1].StartsWith("[USER]");
                if (canRegenerate && Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y, 80f, 25f), "Regen"))
                {
                    RegenerateResponse(index);
                }
            }

            // Delete exchange button (User + Storyteller)
            if (msg.StartsWith("[USER]"))
            {
                bool hasNext = index + 1 < chatLog.Count;
                bool nextIsStoryteller = hasNext && chatLog[index + 1].StartsWith("[STORYTELLER]");
                
                if (nextIsStoryteller)
                {
                    Rect deleteRect = new Rect(viewWidth - 180f, rect.y, 25f, 25f);
                    TooltipHandler.TipRegion(deleteRect, "Delete this exchange");
                    if (Widgets.ButtonText(deleteRect, "X"))
                    {
                        DeleteExchange(index);
                    }
                }
            }
        }

        private void SaveEditedMessage(int index, string originalMsg)
        {
            if (originalMsg.StartsWith("[USER]"))
            {
                chatLog[index] = "[USER] " + editedMessage.Replace("You: ", "");
            }
            else if (originalMsg.StartsWith("[STORYTELLER]"))
            {
                chatLog[index] = "[STORYTELLER] " + editedMessage.Replace(currentStoryteller?.def.label + ": ", "");
            }
            
            editingIndex = -1;
            editedMessage = "";
            RebuildMessageHistory();
        }

        private void RegenerateResponse(int index)
        {
            if (index < 1) return;
            
            string userMsg = chatLog[index - 1].Substring(6).Trim();
            
            // Remove old response
            chatLog.RemoveAt(index);
            
            // Update history
            if (messageHistory.Count >= 2 &&
                messageHistory[messageHistory.Count - 1].role == "model" &&
                messageHistory[messageHistory.Count - 2].role == "user")
            {
                messageHistory.RemoveAt(messageHistory.Count - 1);
                messageHistory.RemoveAt(messageHistory.Count - 1);
            }
            
            // Resend to AI
            input = userMsg;
            SendMessage();
        }

        private void DeleteExchange(int index)
        {
            chatLog.RemoveAt(index + 1); // Storyteller
            chatLog.RemoveAt(index);     // User
            
            if (messageHistory.Count >= 2 &&
                messageHistory[messageHistory.Count - 2].role == "user" &&
                messageHistory[messageHistory.Count - 1].role == "model")
            {
                messageHistory.RemoveAt(messageHistory.Count - 1);
                messageHistory.RemoveAt(messageHistory.Count - 1);
            }
            
            Messages.Message("Exchange deleted", MessageTypeDefOf.RejectInput, false);
        }

        private void ClearAllChat()
        {
            GetChatData()?.ClearHistory(storytellerDefName);
            messageHistory.Clear();
            editingIndex = -1;
            editedMessage = "";
            AddSystemMessage("Chat cleared");
        }

        private void SendMessage()
        {
            if (waitingForResponse || string.IsNullOrWhiteSpace(input))
                return;
                
            string userMessage = input.Trim();
            input = "";
            
            AddUserMessage(userMessage);
            
            // Try as command first
            if (userMessage.StartsWith("/"))
            {
                if (StorytellerCommandParser.TryParseCommand(userMessage, out string response))
                {
                    if (response.StartsWith("[OK]"))
                        AddSuccessMessage(response);
                    else if (response.StartsWith("[ERROR]"))
                        AddErrorMessage(response);
                    else
                        AddStorytellerMessage(response);
                    
                    forceScrollToBottom = true;
                    return;
                }
            }
            
            // If not command, use AI
            SendToAI(userMessage);
        }

        private void SendToAI(string userMessage)
        {
            Log.Message($"[EchoColony-Storyteller] SendToAI called with message: {userMessage}");
            
            waitingForResponse = true;
            AddStorytellerMessage("...");
            forceScrollToBottom = true;
            
            Log.Message($"[EchoColony-Storyteller] Building context prompt...");
            string contextPrompt = StorytellerPromptBuilder.BuildContext(currentStoryteller, userMessage);
            Log.Message($"[EchoColony-Storyteller] Context prompt length: {contextPrompt.Length}");
            
            messageHistory.Add(new GeminiMessage("user", userMessage));
            Log.Message($"[EchoColony-Storyteller] Message history count: {messageHistory.Count}");
            
            string prompt = BuildPromptForAPI(contextPrompt, messageHistory);
            Log.Message($"[EchoColony-Storyteller] Built API prompt, length: {prompt.Length}");
            
            IEnumerator coroutine = null;
            
            Log.Message($"[EchoColony-Storyteller] Model source: {MyMod.Settings.modelSource}");
            
            if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                Log.Message("[EchoColony-Storyteller] Using Player2 Storyteller API");
                coroutine = GeminiAPI.SendRequestToPlayer2Storyteller(prompt, OnAIResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.Gemini)
            {
                Log.Message("[EchoColony-Storyteller] Using Gemini API");
                coroutine = GeminiAPI.SendRequestToGemini(prompt, OnAIResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                Log.Message("[EchoColony-Storyteller] Using OpenRouter API");
                coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, OnAIResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                Log.Message("[EchoColony-Storyteller] Using Local Model");
                coroutine = GeminiAPI.SendRequestToLocalModel(prompt, OnAIResponse);
            }
            
            if (coroutine == null)
            {
                Log.Error("[EchoColony-Storyteller] Coroutine is null!");
                AddErrorMessage("Could not create API request");
                waitingForResponse = false;
                return;
            }
            
            if (MyStoryModComponent.Instance == null)
            {
                Log.Error("[EchoColony-Storyteller] MyStoryModComponent.Instance is null!");
                AddErrorMessage("Could not connect to AI service - MyStoryModComponent missing");
                waitingForResponse = false;
                return;
            }
            
            Log.Message("[EchoColony-Storyteller] Starting coroutine...");
            MyStoryModComponent.Instance.StartCoroutine(coroutine);
            Log.Message("[EchoColony-Storyteller] Coroutine started successfully");
        }

        private string BuildPromptForAPI(string context, List<GeminiMessage> history)
        {
            Log.Message("[EchoColony-Storyteller] BuildPromptForAPI called");
            
            // For Player2, use standard chat format
            if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                var messages = new List<Dictionary<string, string>>();
                
                // System message with context
                messages.Add(new Dictionary<string, string> {
                    { "role", "system" },
                    { "content", context }
                });
                
                // Recent history
                var recentHistory = history.TakeLast(20).ToList();
                foreach (var msg in recentHistory)
                {
                    messages.Add(new Dictionary<string, string> {
                        { "role", msg.role == "model" ? "assistant" : "user" },
                        { "content", msg.content }
                    });
                }
                
                // Create JSON for Player2
                var jsonPayload = new SimpleJSON.JSONObject();
                var jsonMessages = new SimpleJSON.JSONArray();
                
                foreach (var msg in messages)
                {
                    var jsonMsg = new SimpleJSON.JSONObject();
                    jsonMsg["role"] = msg["role"];
                    jsonMsg["content"] = msg["content"];
                    jsonMessages.Add(jsonMsg);
                }
                
                jsonPayload["messages"] = jsonMessages;
                string result = jsonPayload.ToString();
                
                Log.Message($"[EchoColony-Storyteller] Built Player2 JSON: {result.Length} chars");
                return result;
            }
            
            // For Gemini and others, use contents format
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"contents\": [");
            
            sb.Append($"{{\"role\": \"user\", \"parts\": [{{\"text\": \"{EscapeJson(context)}\"}}]}},");
            
            var recentHistory2 = history.TakeLast(20).ToList();
            Log.Message($"[EchoColony-Storyteller] Processing {recentHistory2.Count} recent messages");
            
            for (int i = 0; i < recentHistory2.Count; i++)
            {
                var msg = recentHistory2[i];
                string role = msg.role == "model" ? "model" : "user";
                string text = EscapeJson(msg.content);
                
                sb.Append($"{{\"role\": \"{role}\", \"parts\": [{{\"text\": \"{text}\"}}]}}");
                
                if (i < recentHistory2.Count - 1)
                    sb.Append(",");
            }
            
            sb.Append("]}");
            
            string result2 = sb.ToString();
            Log.Message($"[EchoColony-Storyteller] Built Gemini JSON: {result2.Length} chars");
            
            return result2;
        }

        private static string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private void OnAIResponse(string response)
        {
            Log.Message($"[EchoColony-Storyteller] OnAIResponse called with response length: {response?.Length ?? 0}");
            
            var chat = GetChatData()?.GetChatHistory(storytellerDefName);
            if (chat != null && chat.LastOrDefault()?.StartsWith("[STORYTELLER] ...") == true)
            {
                Log.Message("[EchoColony-Storyteller] Removing '...' placeholder");
                chat.RemoveAt(chat.Count - 1);
            }
            
            ProcessAIResponse(response);
            waitingForResponse = false;
            forceScrollToBottom = true;
            
            Log.Message("[EchoColony-Storyteller] OnAIResponse completed");
        }

        private void ProcessAIResponse(string response)
        {
            // Search for triggers to START events
            var triggerPattern = @"\[TRIGGER:(\w+)\]";
            var triggerMatches = System.Text.RegularExpressions.Regex.Matches(response, triggerPattern);
            
            // Search for commands to STOP events
            var stopPattern = @"\[STOP:(\w+)\]";
            var stopMatches = System.Text.RegularExpressions.Regex.Matches(response, stopPattern);
            
            // Remove tags from visible text
            string cleanResponse = response;
            cleanResponse = System.Text.RegularExpressions.Regex.Replace(cleanResponse, triggerPattern, "");
            cleanResponse = System.Text.RegularExpressions.Regex.Replace(cleanResponse, stopPattern, "");
            cleanResponse = cleanResponse.Trim();
            
            // Display storyteller response
            AddStorytellerMessage(cleanResponse);
            messageHistory.Add(new GeminiMessage("model", cleanResponse));
            
            // Execute found incidents
            foreach (System.Text.RegularExpressions.Match match in triggerMatches)
            {
                string incidentName = match.Groups[1].Value;
                ExecuteIncidentByName(incidentName);
            }
            
            // Stop found conditions
            foreach (System.Text.RegularExpressions.Match match in stopMatches)
            {
                string conditionName = match.Groups[1].Value;
                StopConditionByName(conditionName);
            }
        }

        private void ExecuteIncidentByName(string defName)
        {
            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            
            if (incident != null)
            {
                bool success = StorytellerIncidentExecutor.TryExecuteIncident(incident);
                if (success)
                {
                    AddSuccessMessage($"Event triggered: {incident.label}");
                }
                else
                {
                    AddErrorMessage($"Could not trigger: {incident.label}");
                }
            }
            else
            {
                Log.Warning($"[EchoColony] AI tried to trigger unknown incident: {defName}");
            }
        }

        private void StopConditionByName(string conditionName)
        {
            bool success = StorytellerIncidentExecutor.TryStopGameCondition(conditionName);
            
            if (success)
            {
                AddSuccessMessage($"Condition stopped: {conditionName}");
            }
            else
            {
                // Try partial search
                int count = StorytellerIncidentExecutor.StopAllConditionsOfType(conditionName);
                if (count > 0)
                {
                    AddSuccessMessage($"Stopped {count} condition(s)");
                }
                else
                {
                    AddErrorMessage($"Could not stop: {conditionName}");
                }
            }
        }

        private string GetDisplayMessage(string msg)
        {
            if (msg.StartsWith("[SYSTEM]"))
                return msg.Substring(8).Trim();
            else if (msg.StartsWith("[USER]"))
                return msg.Substring(6).Trim();
            else if (msg.StartsWith("[STORYTELLER]"))
                return msg.Substring(13).Trim();
            else if (msg.StartsWith("[SUCCESS]"))
                return msg.Substring(9).Trim();
            else if (msg.StartsWith("[ERROR]"))
                return msg.Substring(7).Trim();
            else
                return msg;
        }

        private void AddUserMessage(string message)
        {
            GetChatData()?.AddMessage(storytellerDefName, "[USER] " + message);
        }

        private void AddStorytellerMessage(string message)
        {
            GetChatData()?.AddMessage(storytellerDefName, "[STORYTELLER] " + message);
        }

        private void AddSystemMessage(string message)
        {
            GetChatData()?.AddMessage(storytellerDefName, "[SYSTEM] " + message);
        }

        private void AddSuccessMessage(string message)
        {
            GetChatData()?.AddMessage(storytellerDefName, "[SUCCESS] " + message);
        }

        private void AddErrorMessage(string message)
        {
            GetChatData()?.AddMessage(storytellerDefName, "[ERROR] " + message);
        }

        private StorytellerChatData GetChatData()
        {
            if (Current.Game == null)
                return null;
                
            return Current.Game.GetComponent<StorytellerChatData>();
        }

        private void EnsureChatDataExists()
        {
            if (Current.Game == null)
                return;
                
            var data = Current.Game.GetComponent<StorytellerChatData>();
            if (data == null)
            {
                data = new StorytellerChatData(Current.Game);
                Current.Game.components.Add(data);
                Log.Message("[EchoColony] Created StorytellerChatData component");
            }
        }
    }
}