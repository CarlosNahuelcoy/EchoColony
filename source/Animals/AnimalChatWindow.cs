using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using RimWorld;
using System.Text;
using System.IO;
using System;

namespace EchoColony.Animals
{
    public class AnimalChatWindow : Window
    {
        private Pawn animal;
        private List<string> chatLog = new List<string>();
        private string userMessage = "";
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 chatScrollPosition = Vector2.zero;
        private bool isWaitingForResponse = false;
        private bool sendRequestedViaEnter = false;

        private int messageIndexToRegenerate = -1;
        private int messageIndexToEdit = -1;
        private string editBuffer = "";

        // Colors
        private static readonly Color userMessageColor = new Color(0.7f, 0.9f, 1f);
        private static readonly Color animalMessageColor = new Color(0.9f, 1f, 0.7f);
        private static readonly Color errorMessageColor = new Color(1f, 0.5f, 0.5f);
        private static readonly Color userBgColor = new Color(0.2f, 0.3f, 0.4f, 0.3f);
        private static readonly Color animalBgColor = new Color(0.3f, 0.35f, 0.25f, 0.3f);
        private static readonly Color errorBgColor = new Color(0.4f, 0.2f, 0.2f, 0.3f);

        public AnimalChatWindow(Pawn animal)
        {
            this.animal = animal;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            this.closeOnAccept = false;

            LoadChatHistory();
        }

        public override Vector2 InitialSize => new Vector2(650f, 750f);

        private void LoadChatHistory()
        {
            try
            {
                var component = AnimalChatGameComponent.Instance;
                if (component != null)
                {
                    chatLog = component.GetChat(animal);
                    Log.Message($"[EchoColony] Loaded {chatLog.Count} messages for {animal.LabelShort}");
                }
                else
                {
                    Log.Error("[EchoColony] AnimalChatGameComponent.Instance is NULL!");
                    chatLog.Add("[ERROR] Chat system not initialized. Try reloading the game.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error loading chat history: {ex.Message}\n{ex.StackTrace}");
                chatLog.Add($"[ERROR] Failed to load chat: {ex.Message}");
            }
        }

        private void SaveChatHistory()
        {
            try
            {
                var component = AnimalChatGameComponent.Instance;
                if (component != null)
                {
                    component.SaveChat(animal, chatLog);
                }
                else
                {
                    Log.Error("[EchoColony] Cannot save chat - AnimalChatGameComponent is NULL");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error saving chat: {ex.Message}");
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float currentY = 10f;

            DrawHeader(inRect, ref currentY);
            DrawChatArea(inRect, ref currentY);
            DrawInputArea(inRect, currentY);
        }

        private void DrawHeader(Rect inRect, ref float currentY)
        {
            float buttonWidth = 120f;
            float buttonSpacing = 5f;
            float buttonX = 10f;

            Rect exportBtn = new Rect(buttonX, currentY, buttonWidth, 25f);
            if (Widgets.ButtonText(exportBtn, "Export Chat"))
            {
                ExportChat();
            }
            buttonX += buttonWidth + buttonSpacing;

            Rect clearBtn = new Rect(buttonX, currentY, buttonWidth, 25f);
            GUI.color = new Color(1f, 0.7f, 0.7f);
            if (Widgets.ButtonText(clearBtn, "Clear Chat"))
            {
                ClearChat();
            }
            GUI.color = Color.white;
            buttonX += buttonWidth + buttonSpacing;

            Rect promptBtn = new Rect(buttonX, currentY, buttonWidth, 25f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            if (Widgets.ButtonText(promptBtn, "Custom Prompt"))
            {
                Find.WindowStack.Add(new AnimalPromptEditorWindow(animal));
            }
            GUI.color = Color.white;

            currentY += 30f;

            float portraitSize = 80f;
            Rect portraitRect = new Rect((inRect.width - portraitSize) / 2f, currentY, portraitSize, portraitSize);
            
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(
                animal, 
                new Vector2(portraitSize, portraitSize), 
                Rot4.South, 
                default, 
                1.25f
            ));

            currentY += portraitSize + 5f;

            Text.Font = GameFont.Medium;
            string label = $"{animal.LabelShort} ({animal.KindLabel})";
            float labelWidth = Text.CalcSize(label).x;
            Widgets.Label(new Rect((inRect.width - labelWidth) / 2f, currentY, labelWidth, 30f), label);
            currentY += 35f;

            Text.Font = GameFont.Small;
            
            Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedTo != null)
            {
                GUI.color = new Color(1f, 0.8f, 0.5f);
                string bondLabel = $"Bonded to {bondedTo.LabelShort}";
                float bondWidth = Text.CalcSize(bondLabel).x;
                Widgets.Label(new Rect((inRect.width - bondWidth) / 2f, currentY, bondWidth, 20f), bondLabel);
                GUI.color = Color.white;
                currentY += 22f;
            }
        }

        private void DrawChatArea(Rect inRect, ref float currentY)
        {
            float chatHeight = inRect.height - currentY - 90f;
            Rect chatRect = new Rect(10f, currentY, inRect.width - 20f, chatHeight);
            Widgets.DrawBoxSolid(chatRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            List<float> messageHeights = new List<float>();
            float totalHeight = 0f;
            
            for (int i = 0; i < chatLog.Count; i++)
            {
                string msg = chatLog[i];
                float width = chatRect.width - 80f;
                float height = Text.CalcHeight(msg, width) + 15f;
                messageHeights.Add(height);
                totalHeight += height;
            }

            Rect viewRect = new Rect(0, 0, chatRect.width - 16f, Mathf.Max(totalHeight, chatRect.height));
            
            Widgets.BeginScrollView(chatRect, ref chatScrollPosition, viewRect);
            
            float y = 0f;
            for (int i = 0; i < chatLog.Count; i++)
            {
                string msg = chatLog[i];
                bool isUserMessage = msg.StartsWith("You:");
                bool isAnimalMessage = msg.StartsWith(animal.LabelShort + ":");
                bool isErrorMessage = msg.StartsWith("[ERROR]");
                bool isNarrative = !isUserMessage && !isAnimalMessage && !isErrorMessage;

                float msgHeight = messageHeights[i];
                Rect msgRect = new Rect(5f, y, viewRect.width - 70f, msgHeight);

                // Background color
                if (isErrorMessage)
                {
                    Widgets.DrawBoxSolid(msgRect, errorBgColor);
                }
                else if (isNarrative)
                {
                    Widgets.DrawBoxSolid(msgRect, new Color(0.25f, 0.25f, 0.3f, 0.3f)); // Subtle purple-ish
                }
                else if (isUserMessage)
                {
                    Widgets.DrawBoxSolid(msgRect, userBgColor);
                }
                else if (isAnimalMessage)
                {
                    Widgets.DrawBoxSolid(msgRect, animalBgColor);
                }
                else if (i % 2 == 0)
                {
                    Widgets.DrawBoxSolid(msgRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                }

                // Message text
                if (messageIndexToEdit == i)
                {
                    editBuffer = GUI.TextField(msgRect, editBuffer);
                }
                else
                {
                    Color textColor = Color.white;
                    
                    if (isErrorMessage)
                        textColor = errorMessageColor;
                    else if (isNarrative)
                        textColor = new Color(0.8f, 0.8f, 1f); // Soft blue-ish for narrative
                    else if (isUserMessage)
                        textColor = userMessageColor;
                    else if (isAnimalMessage)
                        textColor = animalMessageColor;
                    
                    GUI.color = textColor;
                    Widgets.Label(msgRect, msg);
                    GUI.color = Color.white;
                }

                // Action buttons (only for animal messages)
                if (isAnimalMessage && messageIndexToEdit != i)
                {
                    float buttonX = msgRect.xMax + 5f;
                    float buttonY = y + 5f;
                    float btnSize = 18f;

                    Rect regenBtn = new Rect(buttonX, buttonY, btnSize, btnSize);
                    if (Widgets.ButtonText(regenBtn, "↻"))
                    {
                        RegenerateMessage(i);
                    }

                    buttonY += btnSize + 2f;
                    Rect editBtn = new Rect(buttonX, buttonY, btnSize, btnSize);
                    if (Widgets.ButtonText(editBtn, "✎"))
                    {
                        StartEditingMessage(i);
                    }

                    buttonY += btnSize + 2f;
                    Rect deleteBtn = new Rect(buttonX, buttonY, btnSize, btnSize);
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(deleteBtn, "×"))
                    {
                        DeleteMessage(i);
                    }
                    GUI.color = Color.white;
                }
                else if (messageIndexToEdit == i)
                {
                    float buttonX = msgRect.xMax + 5f;
                    float buttonY = y + 5f;
                    float btnSize = 18f;

                    Rect saveBtn = new Rect(buttonX, buttonY, btnSize, btnSize);
                    GUI.color = Color.green;
                    if (Widgets.ButtonText(saveBtn, "✓"))
                    {
                        SaveEditedMessage(i);
                    }

                    buttonY += btnSize + 2f;
                    Rect cancelBtn = new Rect(buttonX, buttonY, btnSize, btnSize);
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(cancelBtn, "×"))
                    {
                        CancelEdit();
                    }
                    GUI.color = Color.white;
                }

                y += msgHeight;
            }

            Widgets.EndScrollView();
            
            currentY += chatHeight + 5f;
        }
        private void DrawInputArea(Rect inRect, float currentY)
        {
            float inputHeight = 60f;
            float buttonWidth = 80f;
            float spacing = 5f;

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "AnimalChatInput" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            Rect inputRect = new Rect(10f, currentY, inRect.width - buttonWidth - spacing - 20f, inputHeight);
            GUI.SetNextControlName("AnimalChatInput");
            
            var textStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding = new RectOffset(6, 6, 6, 6)
            };

            if (!isWaitingForResponse)
            {
                userMessage = GUI.TextArea(inputRect, userMessage, 500, textStyle);
            }
            else
            {
                GUI.enabled = false;
                GUI.TextArea(inputRect, "Waiting for response...", textStyle);
                GUI.enabled = true;
            }

            Rect sendBtn = new Rect(inputRect.xMax + spacing, currentY, buttonWidth, inputHeight);
            
            if (!isWaitingForResponse)
            {
                bool sendClicked = Widgets.ButtonText(sendBtn, "Send");
                
                if ((sendClicked || sendRequestedViaEnter) && !string.IsNullOrEmpty(userMessage))
                {
                    SendMessage();
                    sendRequestedViaEnter = false;
                }
            }
            else
            {
                GUI.enabled = false;
                Widgets.ButtonText(sendBtn, "...");
                GUI.enabled = true;
            }
        }

        private void SendMessage()
        {
            string msg = userMessage.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            chatLog.Add("You: " + msg);
            SaveChatHistory();
            
            MyStoryModComponent.Instance.StartCoroutine(GetAnimalResponse(msg));
            
            userMessage = "";
            GUI.FocusControl(null);
            scrollPosition.y = float.MaxValue;
            chatScrollPosition.y = float.MaxValue;
        }

        private IEnumerator GetAnimalResponse(string userInput)
        {
            isWaitingForResponse = true;
            bool responseReceived = false;
            string response = "";
            string error = "";

            Log.Message($"[EchoColony] Building prompt for {animal.LabelShort}...");
            
            string prompt = "";
            bool promptBuilt = false;
            
            try
            {
                prompt = AnimalPromptContextBuilder.Build(animal, userInput);
                promptBuilt = true;
                Log.Message($"[EchoColony] Prompt built. Length: {prompt.Length} characters");
            }
            catch (Exception ex)
            {
                error = $"Failed to build prompt: {ex.Message}";
                Log.Error($"[EchoColony] Exception building prompt: {ex.Message}\n{ex.StackTrace}");
            }

            if (!promptBuilt)
            {
                chatLog.Add($"[ERROR] {error}");
                SaveChatHistory();
                Messages.Message($"Animal chat error: {error}", MessageTypeDefOf.RejectInput);
                isWaitingForResponse = false;
                yield break;
            }

            Log.Message($"[EchoColony] Sending request to AI model...");

            yield return GeminiAPI.GetResponseFromModel(animal, prompt, (result) => {
                response = result;
                responseReceived = true;
                Log.Message($"[EchoColony] Response received: {(string.IsNullOrEmpty(result) ? "EMPTY" : result.Substring(0, Math.Min(50, result.Length)))}...");
            });

            float timeout = 30f;
            float elapsed = 0f;
            
            while (!responseReceived && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!responseReceived)
            {
                error = "Request timed out after 30 seconds. Check your API settings and internet connection.";
                Log.Error($"[EchoColony] Request timeout for animal {animal.LabelShort}");
            }
            else if (string.IsNullOrWhiteSpace(response))
            {
                error = "AI returned an empty response. Check the debug logs for more information.";
                Log.Warning($"[EchoColony] Empty response from AI for animal {animal.LabelShort}");
            }
            else if (response.StartsWith("⚠") || response.StartsWith("ERROR"))
            {
                error = response;
                Log.Error($"[EchoColony] API error: {response}");
            }
            else
            {
                // Parse and execute divine actions
                var actionResult = Actions.AnimalActionParser.ParseAndExecuteActions(animal, response);
                
                // Add the clean response (without action tags)
                chatLog.Add($"{animal.LabelShort}: {actionResult.CleanResponse}");
                
                // Add narrative feedback if any actions were executed
                if (actionResult.HasActions && !string.IsNullOrEmpty(actionResult.NarrativeFeedback))
                {
                    chatLog.Add($"{actionResult.NarrativeFeedback}");
                }
                
                SaveChatHistory();
            }

            if (!string.IsNullOrEmpty(error))
            {
                chatLog.Add($"[ERROR] {error}");
                SaveChatHistory();
                Messages.Message($"Animal chat error: {error}", MessageTypeDefOf.RejectInput);
            }

            isWaitingForResponse = false;
            chatScrollPosition.y = float.MaxValue;
        }
        private void RegenerateMessage(int index)
        {
            if (index <= 0 || index >= chatLog.Count) return;

            string lastUserMsg = "";
            for (int i = index - 1; i >= 0; i--)
            {
                if (chatLog[i].StartsWith("You:"))
                {
                    lastUserMsg = chatLog[i].Substring(4).Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(lastUserMsg)) return;

            messageIndexToRegenerate = index;
            MyStoryModComponent.Instance.StartCoroutine(RegenerateCoroutine(lastUserMsg, index));
        }

        private IEnumerator RegenerateCoroutine(string userInput, int index)
{
    isWaitingForResponse = true;
    bool responseReceived = false;
    string response = "";

    string prompt = "";
    bool promptBuilt = false;
    
    try
    {
        prompt = AnimalPromptContextBuilder.Build(animal, userInput);
        promptBuilt = true;
    }
    catch (Exception ex)
    {
        Log.Error($"[EchoColony] Error building prompt for regeneration: {ex.Message}");
    }

    if (!promptBuilt)
    {
        isWaitingForResponse = false;
        messageIndexToRegenerate = -1;
        yield break;
    }

    yield return GeminiAPI.GetResponseFromModel(animal, prompt, (result) => {
        response = result;
        responseReceived = true;
    });

    float timeout = 30f;
    float elapsed = 0f;
    
    while (!responseReceived && elapsed < timeout)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }

    if (responseReceived && !string.IsNullOrWhiteSpace(response) && index < chatLog.Count)
    {
        var actionResult = Actions.AnimalActionParser.ParseAndExecuteActions(animal, response);
        chatLog[index] = $"{animal.LabelShort}: {actionResult.CleanResponse}";
        if (actionResult.HasActions && !string.IsNullOrEmpty(actionResult.NarrativeFeedback))
            chatLog.Add(actionResult.NarrativeFeedback);
        SaveChatHistory();
    }

    isWaitingForResponse = false;
    messageIndexToRegenerate = -1;
}
        private void StartEditingMessage(int index)
        {
            if (index < 0 || index >= chatLog.Count) return;
            
            messageIndexToEdit = index;
            editBuffer = chatLog[index];
        }

        private void SaveEditedMessage(int index)
        {
            if (index < 0 || index >= chatLog.Count) return;
            
            chatLog[index] = editBuffer;
            SaveChatHistory();
            
            messageIndexToEdit = -1;
            editBuffer = "";
        }

        private void CancelEdit()
        {
            messageIndexToEdit = -1;
            editBuffer = "";
        }

        private void DeleteMessage(int index)
        {
            if (index < 0 || index >= chatLog.Count) return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Delete this message?",
                () => {
                    chatLog.RemoveAt(index);
                    SaveChatHistory();
                }
            ));
        }

        private void ExportChat()
        {
            try
            {
                string fileName = $"AnimalChat_{animal.LabelShort}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                string folderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "EchoColony", "ChatExports");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("=== ECHOCOLONY ANIMAL CHAT EXPORT ===");
                sb.AppendLine($"Animal: {animal.LabelShort} ({animal.KindLabel})");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=" + new string('=', 40));
                sb.AppendLine();

                foreach (var message in chatLog)
                {
                    sb.AppendLine(message);
                }

                File.WriteAllText(filePath, sb.ToString());
                Messages.Message($"Chat exported to: {fileName}", MessageTypeDefOf.PositiveEvent);

                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error exporting animal chat: {ex.Message}");
                Messages.Message("Failed to export chat", MessageTypeDefOf.NegativeEvent);
            }
        }

        private void ClearChat()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Clear all chat history with this animal?",
                () => {
                    chatLog.Clear();
                    SaveChatHistory();
                    Messages.Message("Chat history cleared", MessageTypeDefOf.NeutralEvent);
                }
            ));
        }

        public override void PostClose()
        {
            base.PostClose();
            SaveChatHistory();
        }
    }
}