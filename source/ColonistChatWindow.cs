using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;
using System.Linq;
using System.Collections;
using System.IO;
using Verse.AI.Group;
using RimWorld.Planet;
using System;
using EchoColony.Actions;

namespace EchoColony
{
    public class ColonistChatWindow : Window
    {
        private Pawn pawn;
        private Vector2 scrollPos = Vector2.zero;
        private string input = "";
        private List<string> chatLog => ChatGameComponent.Instance.GetChat(pawn);

        private List<string> cachedChatLog = null;

        private bool sendRequestedViaEnter = false;
        private bool waitingForResponse = false;
        private bool forceScrollToBottom = false;

        private List<GeminiMessage> messageHistory = new List<GeminiMessage>();

        private int editingIndex = -1;
        private string editedMessage = "";

        private Dictionary<Pawn, string> selectedVoices = new Dictionary<Pawn, string>();
        private Vector2 voiceScroll = Vector2.zero;

        private int conversationTurnCount = 0;
        private int lastSavedTurnCount = 0;

        // Captured once when the window opens (game is paused, overlay not yet visible).
        private string _visionBase64 = null;

        public ColonistChatWindow(Pawn pawn)
        {
            this.pawn = pawn;
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.closeOnAccept = false;

            forceScrollToBottom = true;

            if (MyMod.Settings?.enableVision == true)
            {
                _visionBase64 = GeminiAPI.CaptureScreenshotBase64();
                if (_visionBase64 != null)
                    Log.Message($"[EchoColony] Vision ready for {pawn.LabelShort}");
                else
                    Log.Warning($"[EchoColony] Vision capture failed for {pawn.LabelShort} — will send text only");
            }

            UpdateContextPrompt();

            var rawChat = ChatGameComponent.Instance.GetChat(pawn);
            messageHistory = new List<GeminiMessage>();

            foreach (string line in rawChat)
            {
                if (line.StartsWith("[USER]"))
                    messageHistory.Add(new GeminiMessage("user", line.Substring(6).Trim()));
                else if (line.StartsWith(pawn.LabelShort + ":"))
                    messageHistory.Add(new GeminiMessage("model", line.Substring(pawn.LabelShort.Length + 1).Trim()));
            }

            CalculateTurnCountFromHistory();
            lastSavedTurnCount = conversationTurnCount;

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId))
                    TryAssignVoiceToPawn(pawn);
            }
        }

        private void CalculateTurnCountFromHistory()
        {
            int userMessages  = messageHistory.Count(m => m.role == "user");
            int modelMessages = messageHistory.Count(m => m.role == "model");
            conversationTurnCount = Math.Min(userMessages, modelMessages);
            Log.Message($"[EchoColony] Calculated turn count from history: {conversationTurnCount} turns ({userMessages} user, {modelMessages} model messages)");
        }

        private void UpdateContextPrompt()
        {
            string contextPrompt;

            if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                if (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                    contextPrompt = KoboldPromptBuilder.Build(pawn, "");
                else if (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                    contextPrompt = LMStudioPromptBuilder.Build(pawn, "");
                else
                    contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }
            else
            {
                // Gemini, OpenRouter, Player2, Custom — all use the standard context builder
                contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }

            if (messageHistory.Count > 0 && messageHistory[0].role == "user")
                messageHistory[0] = new GeminiMessage("user", contextPrompt);
            else
                messageHistory.Insert(0, new GeminiMessage("user", contextPrompt));
        }

        public class GeminiMessage
        {
            public string role;
            public string content;

            public GeminiMessage(string role, string content)
            {
                this.role    = role;
                this.content = content;
            }
        }

        private string BuildGeminiChatJson(List<GeminiMessage> history, string imageBase64 = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"contents\": [");

            bool useVision = !string.IsNullOrEmpty(imageBase64) && MyMod.Settings?.enableVision == true;

            for (int i = 0; i < history.Count; i++)
            {
                var    msg  = history[i];
                string role = msg.role == "model" ? "model" : "user";
                string text = EscapeJson(msg.content);
                bool isLastUserMsg = (i == history.Count - 1) && msg.role == "user";

                if (useVision && isLastUserMsg)
                {
                    sb.Append($"{{\"role\": \"{role}\", \"parts\": [" +
                              $"{{\"text\": \"{text}\"}}," +
                              $"{{\"inlineData\": {{\"mimeType\": \"image/jpeg\", \"data\": \"{imageBase64}\"}}}}" +
                              $"]}}");
                }
                else
                {
                    sb.Append($"{{\"role\": \"{role}\", \"parts\": [{{\"text\": \"{text}\"}}]}}");
                }

                if (i < history.Count - 1) sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string CleanText(string input) =>
            System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);

        string CleanColors(string input) =>
            System.Text.RegularExpressions.Regex.Replace(input, "<color=#[A-Fa-f0-9]{6,8}>|</color>", "");

        private static string EscapeJson(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        public override Vector2 InitialSize => new Vector2(850f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            if (cachedChatLog == null || cachedChatLog.Count != chatLog.Count)
                cachedChatLog = new List<string>(chatLog);

            Rect portraitRect = new Rect(0f, 0f, 60f, 60f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(60f, 60f), Rot4.South, default, 1.25f));

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(45f, 10f, inRect.width - 50f, 30f), "EchoColony.TalkingWithLabel".Translate(pawn.LabelCap));
            Text.Font = GameFont.Small;

            if (MyMod.Settings?.enableVision == true)
            {
                string visionLabel = _visionBase64 != null ? "👁 Vision" : "👁 ✗";
                GUI.color = _visionBase64 != null ? new Color(0.5f, 1f, 0.7f, 0.8f) : new Color(1f, 0.5f, 0.5f, 0.7f);
                Widgets.Label(new Rect(inRect.width - 430f, 14f, 70f, 20f), visionLabel);
                GUI.color = Color.white;
            }

            float chatHeight       = inRect.height - 110f;
            Rect  scrollRect       = new Rect(0, 45f, inRect.width - 20f, chatHeight);
            float scrollBarWidth   = 16f;
            float effectiveViewWidth = scrollRect.width - scrollBarWidth;

            float viewHeight = 0f;
            List<float> heights = new List<float>();
            Text.Anchor  = TextAnchor.UpperLeft;
            Text.WordWrap = true;

            for (int i = 0; i < cachedChatLog.Count; i++)
            {
                string msg   = cachedChatLog[i];
                float  width = msg.StartsWith("[DATE_SEPARATOR]") ? effectiveViewWidth : effectiveViewWidth - 200f;
                float  height = Text.CalcHeight(GetDisplayMessage(msg), width) + 4f;
                heights.Add(height);
                viewHeight += height + 10f;
            }

            Rect viewRect = new Rect(0, 0, effectiveViewWidth, viewHeight + 20f);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            if (forceScrollToBottom)
            {
                scrollPos.y         = viewHeight;
                forceScrollToBottom = false;
            }

            float y = 0;
            int   messagesToDraw = Math.Min(cachedChatLog.Count, heights.Count);
            for (int i = 0; i < messagesToDraw; i++)
            {
                string msg         = cachedChatLog[i];
                Rect   messageRect = new Rect(0, y, effectiveViewWidth, heights[i]);

                if (msg.StartsWith("[DATE_SEPARATOR]"))
                    DrawDateSeparator(messageRect, msg);
                else
                    DrawRegularMessage(messageRect, msg, i, effectiveViewWidth, cachedChatLog);

                y += heights[i] + 10f;
            }

            Widgets.EndScrollView();
            Text.WordWrap = false;

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "ChatInputField" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            Rect inputRect = new Rect(0, inRect.height - 60f, inRect.width - 110f, 50f);
            GUI.SetNextControlName("ChatInputField");

            if (editingIndex == -1 &&
                (sendRequestedViaEnter || (Event.current.type == EventType.Layout && input.NullOrEmpty())) &&
                Find.WindowStack.Windows.LastOrDefault() == this)
            {
                GUI.FocusControl("ChatInputField");
            }

            var textStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 14, padding = new RectOffset(6, 6, 6, 6) };
            input = GUI.TextArea(inputRect, input, 500, textStyle);

            Rect sendRect   = new Rect(inRect.width - 100f, inRect.height - 60f, 100f, 30f);
            bool sendClicked = Widgets.ButtonText(sendRect, "EchoColony.SendButton".Translate());

            if (!waitingForResponse && (sendClicked || sendRequestedViaEnter))
            {
                SendMessage();
                sendRequestedViaEnter = false;
                GUI.FocusControl("ChatInputField");
            }

            Rect clearRect = new Rect(inRect.width - 330f, 10f, 100f, 30f);
            if (Widgets.ButtonText(clearRect, "EchoColony.ClearAllButton".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.ClearAllConfirm".Translate(),
                    () =>
                    {
                        ChatGameComponent.Instance.ClearChat(pawn);
                        messageHistory.Clear();
                        editingIndex  = -1;
                        editedMessage = "";
                        ResetTurnCounter();
                    }));
            }

            Rect exportRect = new Rect(inRect.width - 110f, 10f, 100f, 30f);
            if (Widgets.ButtonText(exportRect, "EchoColony.ExportButton".Translate()))
            {
                int    ticks     = Find.TickManager.TicksGame;
                float  longitude = Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x;
                int    year      = GenDate.Year(ticks, longitude);
                string quadrum   = GenDate.Quadrum(ticks, longitude).ToString();
                int    day       = GenDate.DayOfSeason(ticks, longitude);
                string filename  = $"{pawn.Name.ToStringShort}_chat_Year{year}_Day{day}_{quadrum}.txt";
                string folder    = Path.Combine(GenFilePaths.SaveDataFolderPath, "ColonistChats");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string fullPath = Path.Combine(folder, filename);
                try
                {
                    var cleanLog = new List<string>();
                    foreach (var line in chatLog)
                    {
                        string clean = line;
                        clean = clean.Replace("<b><i>", "*").Replace("</i></b>", "*");
                        clean = clean.Replace("<b>", "").Replace("</b>", "");
                        clean = clean.Replace("<i>", "").Replace("</i>", "");
                        clean = System.Text.RegularExpressions.Regex.Replace(clean, "<color=.*?>", "");
                        clean = clean.Replace("</color>", "");
                        cleanLog.Add(clean);
                    }
                    File.WriteAllLines(fullPath, cleanLog);
                    Messages.Message($"Conversation exported to:\n{fullPath}", MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[EchoColony] Error exporting conversation: {ex.Message}");
                    Messages.Message("❌ Error exporting the conversation.", MessageTypeDefOf.RejectInput, false);
                }
            }

            Rect personalizeRect = new Rect(inRect.width - 220f, 10f, 100f, 30f);
            if (Widgets.ButtonText(personalizeRect, "EchoColony.PersonalizeButton".Translate()))
                Find.WindowStack.Add(new ColonistPromptEditor(pawn));
        }

        private void TrySpeakLastLine(Pawn pawn)
        {
            if (!MyMod.Settings.enableTTS) return;

            string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);

            if (string.IsNullOrEmpty(voiceId))
            {
                if (TTSVoiceCache.Voices == null || TTSVoiceCache.Voices.Count == 0)
                {
                    Log.Warning($"[EchoColony] No TTS voices available. Auto-TTS skipped.");
                    return;
                }

                string targetGender = pawn.gender == Gender.Male ? "male" : pawn.gender == Gender.Female ? "female" : Rand.Bool ? "male" : "female";
                var matchingVoices  = TTSVoiceCache.Voices.Where(v => v.gender == targetGender && v.language.ToLowerInvariant().Contains("english")).ToList();
                if (matchingVoices.Count == 0) matchingVoices = TTSVoiceCache.Voices.Where(v => v.language.ToLowerInvariant().Contains("english")).ToList();
                if (matchingVoices.Count == 0) matchingVoices = TTSVoiceCache.Voices.ToList();

                var selectedVoice = matchingVoices.RandomElement();
                voiceId = selectedVoice.id;
                ChatGameComponent.Instance.SetVoiceForPawn(pawn, voiceId);
                ColonistVoiceManager.SetVoice(pawn, voiceId);
                Log.Message($"[EchoColony] Assigned {selectedVoice.gender} voice '{selectedVoice.name}' to {pawn.LabelShort}.");
            }

            string lastLine = ChatGameComponent.Instance.GetChat(pawn).LastOrDefault(l => l.StartsWith(pawn.LabelShort + ":"));
            if (!string.IsNullOrWhiteSpace(lastLine))
            {
                string cleanText = CleanTextForTTS(lastLine.Substring(pawn.LabelShort.Length + 1).Trim());
                if (!string.IsNullOrWhiteSpace(cleanText))
                {
                    string voiceGender = TTSVoiceCache.Voices?.FirstOrDefault(v => v.id == voiceId)?.gender ?? "female";
                    MyStoryModComponent.Instance.StartCoroutine(TTSManager.Speak(cleanText, voiceId, voiceGender, "en_US", 1f));
                }
            }
        }

        public static string GetPawnCombatStatusDetailed(Pawn pawn)
        {
            var  job        = pawn.CurJob;
            bool isFighting = job != null && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic || job.def == JobDefOf.Wait_Combat);
            Thing  target   = job?.targetA.Thing;
            string combatLine = "";

            if (isFighting && target is Pawn enemy)
                combatLine = $"I am attacking {enemy.LabelShort ?? "an unknown target"}, a {enemy.kindDef?.label ?? "unknown kind"}, located {(int)IntVec3Utility.DistanceTo(pawn.Position, enemy.Position)} tiles away.";
            else if (isFighting)
                combatLine = "I am actively engaged in combat.";

            var targetedBy = Find.CurrentMap.attackTargetsCache.TargetsHostileToColony.OfType<Pawn>()
                .Where(e => e.Spawned && e.CurJob?.targetA.Thing == pawn).ToList();
            string targetedLine = "";
            if (targetedBy.Count > 0)
            {
                var grouped = targetedBy.GroupBy(p => p.kindDef.label).Select(g => $"{g.Count()} {g.Key}").ToList();
                targetedLine = $"I am currently being targeted by {string.Join(", ", grouped)}. Average distance: {targetedBy.Average(p => IntVec3Utility.DistanceTo(p.Position, pawn.Position)):F0} tiles.";
            }

            float  bleed       = pawn.health.hediffSet.BleedRateTotal;
            string bleedingLine = bleed > 0.4f ? "I am bleeding heavily." : bleed > 0.1f ? "I have moderate bleeding." : bleed > 0.01f ? "I have light bleeding." : "";

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(combatLine))   lines.Add(combatLine);
            if (!string.IsNullOrWhiteSpace(targetedLine)) lines.Add(targetedLine);
            if (!string.IsNullOrWhiteSpace(bleedingLine)) lines.Add(bleedingLine);
            return lines.Any() ? string.Join(" ", lines) : "I am not in combat, not being targeted, and have no bleeding.";
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND MESSAGE — change 1 of 5
        // ═══════════════════════════════════════════════════════════════

        private void SendMessage()
        {
            if (waitingForResponse || input.NullOrEmpty()) return;

            string userMsg = input;
            input = "";

            ChatGameComponent.Instance.AddLine(pawn, "[USER] " + "EchoColony.UserPrefix".Translate() + userMsg);
            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": ...");
            cachedChatLog       = null;
            UpdateContextPrompt();
            waitingForResponse  = true;
            forceScrollToBottom = true;

            bool isKobold   = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;
            bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;
            bool isCustom   = MyMod.Settings.modelSource == ModelSource.Custom;

            string prompt;

            if (isKobold)
            {
                prompt = KoboldPromptBuilder.Build(pawn, userMsg);
            }
            else if (isLMStudio)
            {
                prompt = LMStudioPromptBuilder.Build(pawn, userMsg);
            }
            else if (isCustom)
            {
                // Custom provider receives plain text — no Gemini JSON wrapping
                prompt = ColonistPromptContextBuilder.Build(pawn, userMsg);
            }
            else
            {
                messageHistory.Add(new GeminiMessage("user", userMsg));
                prompt = BuildGeminiChatJson(messageHistory, _visionBase64);
            }

            IEnumerator coroutine;

            if (isKobold || isLMStudio || MyMod.Settings.modelSource == ModelSource.Local)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(prompt, OnResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, userMsg, OnResponse, _visionBase64);
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, OnResponse, _visionBase64);
            }
            else if (isCustom)
            {
                // Custom: no vision support
                coroutine = GeminiAPI.SendRequestToCustomProvider(prompt, OnResponse);
            }
            else
            {
                // Gemini: prompt already has inlineData if vision is on
                coroutine = GeminiAPI.SendRequestToGemini(prompt, OnResponse);
            }

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId)) TryAssignVoiceToPawn(pawn);
            }

            MyStoryModComponent.Instance.StartCoroutine(coroutine);
        }

        // ═══════════════════════════════════════════════════════════════
        // ON RESPONSE — change 2 of 5
        // ═══════════════════════════════════════════════════════════════

        private void OnResponse(string response)
        {
            var    chat        = ChatGameComponent.Instance.GetChat(pawn);
            string thinkingMsg = pawn.LabelShort + ": ...";
            if (chat.LastOrDefault() == thinkingMsg) chat.RemoveAt(chat.Count - 1);

            if (string.IsNullOrWhiteSpace(response))
            {
                Log.Error("[EchoColony] Received empty response from API");
                string errorMsg = "<color=#FF6B6B>⚠ ERROR: No response received from AI</color>\n<color=#FFAA00>Check your settings:</color>\n";

                switch (MyMod.Settings.modelSource)
                {
                    case ModelSource.Gemini:
                        errorMsg += "• Verify your Gemini API key is correct\n";
                        errorMsg += "• Check your internet connection\n";
                        errorMsg += $"• Current model: {MyMod.Settings.modelPreferences?.preferredFastModel ?? "auto"}";
                        break;
                    case ModelSource.Player2:
                        errorMsg += "• Make sure Player2 is running\n";
                        errorMsg += "• Check http://127.0.0.1:4315/v1/health";
                        break;
                    case ModelSource.OpenRouter:
                        errorMsg += "• Verify your OpenRouter API key\n";
                        errorMsg += $"• Check endpoint: {MyMod.Settings.openRouterEndpoint}";
                        break;
                    case ModelSource.Local:
                        errorMsg += $"• Check if local model is running at: {MyMod.Settings.localModelEndpoint}\n";
                        errorMsg += $"• Model: {MyMod.Settings.localModelName}";
                        break;
                    case ModelSource.Custom:
                        errorMsg += $"• Check your custom endpoint: {MyMod.Settings.customEndpoint}\n";
                        errorMsg += $"• Model: {(string.IsNullOrEmpty(MyMod.Settings.customModelName) ? "(server default)" : MyMod.Settings.customModelName)}";
                        break;
                }

                ChatGameComponent.Instance.AddLine(pawn, errorMsg);
                cachedChatLog = null; waitingForResponse = false; forceScrollToBottom = true;
                return;
            }

            if (response.StartsWith("ERROR:") || response.StartsWith("⚠"))
            {
                Log.Error($"[EchoColony] API returned error: {response}");
                ChatGameComponent.Instance.AddLine(pawn, $"<color=#FF6B6B>{response}</color>");
                cachedChatLog = null; waitingForResponse = false; forceScrollToBottom = true;
                return;
            }

            string       cleanResponse = response;
            List<string> actionResults = new List<string>();

            if (MyMod.Settings.enableDivineActions)
            {
                try
                {
                    var processed = ActionExecutor.ProcessResponse(pawn, response);
                    if (processed != null)
                    {
                        cleanResponse = processed.CleanResponse ?? response;
                        actionResults = processed.ExecutionResults ?? new List<string>();
                        Log.Message($"[EchoColony] Processed response for {pawn.LabelShort}: {actionResults.Count} action(s) executed");
                    }
                    else cleanResponse = response;
                }
                catch (TypeLoadException) { cleanResponse = response; }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Error processing actions: {ex.Message}\nStack: {ex.StackTrace}");
                    cleanResponse = response;
                }
            }

            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": " + cleanResponse);

            if (actionResults?.Any() == true)
                foreach (var result in actionResults)
                    ChatGameComponent.Instance.AddLine(pawn, $"<color=#FFD700>{result}</color>");

            cachedChatLog = null; waitingForResponse = false; input = ""; forceScrollToBottom = true;
            messageHistory.Add(new GeminiMessage("model", response));
            conversationTurnCount++;
            Log.Message($"[EchoColony] Turn completed #{conversationTurnCount} for {pawn.LabelShort}");

            int turnsSinceLastSave = conversationTurnCount - lastSavedTurnCount;
            if (turnsSinceLastSave >= 4 && turnsSinceLastSave % 4 == 0)
                SaveMemoryAutomatically();

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS && MyMod.Settings.autoPlayVoice)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId)) TryAssignVoiceToPawn(pawn);
                TrySpeakLastLine(pawn);
            }
        }

        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleanText = text;
            string colonistPrefix = pawn.LabelShort + ": ";
            if (cleanText.StartsWith(colonistPrefix)) cleanText = cleanText.Substring(colonistPrefix.Length);
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"<b><i>.*?</i></b>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleanText = CleanText(cleanText);
            cleanText = CleanColors(cleanText);
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"\s+", " ").Trim();
            return cleanText;
        }

        // ═══════════════════════════════════════════════════════════════
        // SAVE MEMORY AUTOMATICALLY — change 3 of 5
        // ═══════════════════════════════════════════════════════════════

        private void SaveMemoryAutomatically()
        {
            int turnsSinceLastSave = conversationTurnCount - lastSavedTurnCount;
            if (turnsSinceLastSave < 4 || messageHistory.Count < 4) return;

            var manager = ColonistMemoryManager.GetOrCreate();
            if (manager == null) return;

            Messages.Message("EchoColony.SavingMemories".Translate(), MessageTypeDefOf.SilentInput, false);

            int messagesToSkip = lastSavedTurnCount * 2;
            var recentMessages = messageHistory
                .Skip(messagesToSkip)
                .Where(m => m.role == "user" || m.role == "model")
                .TakeLast(8)
                .Select(m => (m.role == "user" ? "Jugador: " : pawn.LabelShort + ": ") + m.content);

            if (!recentMessages.Any()) return;

            string combined      = string.Join("\n", recentMessages);
            string fullPrompt    = "Summarize this part of the conversation as if it were a personal memory from the colonist's perspective. Keep it brief, intimate, and natural—avoid literal quotes.\n\n" + combined;

            System.Action<string> memoryCallback = (summary) =>
            {
                if (string.IsNullOrWhiteSpace(summary))
                    summary = $"Conversation with the player during turn {conversationTurnCount}. {combined.Substring(0, Math.Min(100, combined.Length))}...";

                var tracker = manager.GetTrackerFor(pawn);
                if (tracker != null)
                {
                    try
                    {
                        tracker.SaveMemoryForDay(GenDate.DaysPassed, summary.Trim());
                        lastSavedTurnCount = conversationTurnCount;
                        Messages.Message("EchoColony.MemoriesSaved".Translate(), MessageTypeDefOf.SilentInput, false);
                    }
                    catch (Exception ex) { Log.Error($"[EchoColony] Error saving memory: {ex.Message}"); }
                }
            };

            try
            {
                bool isKobold   = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;
                bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

                IEnumerator memoryCoroutine;

                if (isKobold)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(KoboldPromptBuilder.Build(pawn, fullPrompt), memoryCallback);
                    Log.Message("[EchoColony] Starting memory with KoboldAI");
                }
                else if (isLMStudio)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(LMStudioPromptBuilder.Build(pawn, fullPrompt), memoryCallback);
                    Log.Message("[EchoColony] Starting memory with LMStudio");
                }
                else if (MyMod.Settings.modelSource == ModelSource.Local)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(fullPrompt, memoryCallback);
                    Log.Message("[EchoColony] Starting memory with local model");
                }
                else if (MyMod.Settings.modelSource == ModelSource.Player2)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, fullPrompt, memoryCallback);
                    Log.Message("[EchoColony] Starting memory with Player2");
                }
                else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(fullPrompt, memoryCallback);
                    Log.Message("[EchoColony] Starting memory with OpenRouter");
                }
                else if (MyMod.Settings.modelSource == ModelSource.Custom)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToCustomProvider(fullPrompt, memoryCallback);
                    Log.Message("[EchoColony] Starting memory with Custom Provider");
                }
                else
                {
                    string jsonPrompt = BuildGeminiChatJson(new List<GeminiMessage> { new GeminiMessage("user", fullPrompt) });
                    memoryCoroutine   = GeminiAPI.SendRequestToGemini(jsonPrompt, memoryCallback);
                    Log.Message("[EchoColony] Starting memory with Gemini");
                }

                if (memoryCoroutine != null && MyStoryModComponent.Instance != null)
                    MyStoryModComponent.Instance.StartCoroutine(memoryCoroutine);
            }
            catch (Exception ex) { Log.Error($"[EchoColony] Error starting memory generation: {ex.Message}"); }
        }

        private void TryAssignVoiceToPawn(Pawn pawn)
        {
            if (TTSVoiceCache.Voices == null || TTSVoiceCache.Voices.Count == 0) return;
            string targetGender = pawn.gender == Gender.Male ? "male" : pawn.gender == Gender.Female ? "female" : Rand.Bool ? "male" : "female";
            var matchingVoices  = TTSVoiceCache.Voices.Where(v => v.gender == targetGender && v.language.ToLowerInvariant().Contains("english")).ToList();
            if (matchingVoices.Count == 0) matchingVoices = TTSVoiceCache.Voices.Where(v => v.language.ToLowerInvariant().Contains("english")).ToList();
            if (matchingVoices.Count == 0) matchingVoices = TTSVoiceCache.Voices.ToList();
            var selectedVoice = matchingVoices.RandomElement();
            ChatGameComponent.Instance.SetVoiceForPawn(pawn, selectedVoice.id);
            ColonistVoiceManager.SetVoice(pawn, selectedVoice.id);
            Log.Message($"[EchoColony] (Late) Assigned voice '{selectedVoice.name}' to {pawn.LabelShort}");
        }

        // ═══════════════════════════════════════════════════════════════
        // POST CLOSE — change 4 of 5
        // ═══════════════════════════════════════════════════════════════

        public override void PostClose()
        {
            base.PostClose();

            if (lastSavedTurnCount > conversationTurnCount) lastSavedTurnCount = conversationTurnCount;
            int unsavedTurns = conversationTurnCount - lastSavedTurnCount;

            if (messageHistory == null || messageHistory.Count <= 1) return;

            int messagesToSkip = lastSavedTurnCount * 2;
            var remainingMessagesList = messageHistory
                .Skip(Math.Max(1, messagesToSkip))
                .Where(m => (m.role == "user" || m.role == "model") && !string.IsNullOrWhiteSpace(m.content))
                .Select(m => (m.role == "user" ? "Jugador: " : pawn.LabelShort + ": ") + m.content)
                .ToList();

            if (!remainingMessagesList.Any() || unsavedTurns < 1) return;

            string combined = string.Join("\n", remainingMessagesList);
            if (combined.Length < 30) { lastSavedTurnCount = conversationTurnCount; return; }

            var manager = ColonistMemoryManager.GetOrCreate();
            var tracker = manager?.GetTrackerFor(pawn);
            if (tracker == null) return;

            int today = GenDate.DaysPassed;
            var existingMemory = tracker.GetMemoryForDay(today);
            if (!string.IsNullOrEmpty(existingMemory))
            {
                string sample = combined.Length > 40 ? combined.Substring(0, 40) : combined;
                if (existingMemory.Contains(sample, System.StringComparison.OrdinalIgnoreCase))
                { lastSavedTurnCount = conversationTurnCount; return; }
            }

            Messages.Message("EchoColony.SavingMemories".Translate(), MessageTypeDefOf.SilentInput, false);

            string fullPrompt = "Summarize this final part of the conversation as if it were a personal memory from the colonist's perspective. Keep it brief, intimate, and natural—avoid literal quotes.\n\n" + combined;

            System.Action<string> finalMemoryCallback = (summary) =>
            {
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    tracker.SaveMemoryForDay(today, summary.Trim());
                    lastSavedTurnCount = conversationTurnCount;
                    Log.Message($"[EchoColony] Final memory saved for {pawn.LabelShort} (day {today})");
                }
            };

            try
            {
                IEnumerator finalCoroutine = null;
                bool isLocal = MyMod.Settings.modelSource == ModelSource.Local;

                if (isLocal)
                {
                    string localPrompt = (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                        ? KoboldPromptBuilder.Build(pawn, fullPrompt)
                        : (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                        ? LMStudioPromptBuilder.Build(pawn, fullPrompt)
                        : fullPrompt;
                    finalCoroutine = GeminiAPI.SendRequestToLocalModel(localPrompt, finalMemoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Player2)
                    finalCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, fullPrompt, finalMemoryCallback);
                else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                    finalCoroutine = GeminiAPI.SendRequestToOpenRouter(fullPrompt, finalMemoryCallback);
                else if (MyMod.Settings.modelSource == ModelSource.Custom)
                    finalCoroutine = GeminiAPI.SendRequestToCustomProvider(fullPrompt, finalMemoryCallback);
                else
                {
                    string jsonPrompt = BuildGeminiChatJson(new List<GeminiMessage> { new GeminiMessage("user", fullPrompt) });
                    finalCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, finalMemoryCallback);
                }

                if (finalCoroutine != null)
                    MyStoryModComponent.Instance.StartCoroutine(finalCoroutine);
            }
            catch (Exception ex) { Log.Error($"[EchoColony] Error generating final memory: {ex.Message}"); }
        }

        private string GetDisplayMessage(string msg)
        {
            if (msg.StartsWith("[DATE_SEPARATOR]")) return msg.Substring("[DATE_SEPARATOR]".Length).Trim();
            if (msg.StartsWith("[USER]"))           return msg.Substring(6);
            return msg;
        }

        private void DrawDateSeparator(Rect rect, string msg)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.4f, 0.5f, 0.3f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font   = GameFont.Small;
            GUI.color   = new Color(0.8f, 0.9f, 1f, 0.9f);
            Widgets.Label(rect, msg.Substring("[DATE_SEPARATOR]".Length).Trim());
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAW REGULAR MESSAGE — change 5 of 5 (regeneration)
        // ═══════════════════════════════════════════════════════════════

        private void DrawRegularMessage(Rect rect, string msg, int index, float viewWidth, List<string> currentChatLog)
        {
            string displayMsg = GetDisplayMessage(msg);

            if (index > 0)
                Widgets.DrawLineHorizontal(rect.x, rect.y - 2f, viewWidth - 200f, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            Rect labelRect = new Rect(0, rect.y + 5f, viewWidth - 200f, rect.height);

            if (editingIndex == index)
            {
                GUI.SetNextControlName($"EditField_{index}");
                editedMessage = Widgets.TextArea(labelRect, editedMessage);
                if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y + 5f, 80f, 25f), "EchoColony.SaveButton".Translate()))
                {
                    chatLog[index] = msg.StartsWith("[USER]")
                        ? "[USER] " + editedMessage.Replace("You: ", "")
                        : pawn.LabelShort + ": " + editedMessage.Replace(pawn.LabelShort + ": ", "").TrimStart();
                    editingIndex = -1; editedMessage = ""; cachedChatLog = null;
                }
                if (Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y + 5f, 80f, 25f), "EchoColony.CancelButton".Translate()))
                { editingIndex = -1; editedMessage = ""; }
            }
            else
            {
                GUI.color = msg.StartsWith("[USER]") ? new Color(0.8f, 0.9f, 1f, 1f) : new Color(1f, 0.95f, 0.8f, 1f);
                Widgets.Label(labelRect, displayMsg);
                GUI.color = Color.white;

                bool isUserMsg      = msg.StartsWith("[USER]");
                bool hasNext        = index + 1 < currentChatLog.Count;
                bool nextIsColonist = hasNext && currentChatLog[index + 1].StartsWith(pawn.LabelShort + ":");
                bool isLastExchange = isUserMsg && nextIsColonist;

                if (isLastExchange)
                {
                    Rect buttonRect = new Rect(viewWidth - 180f, rect.y, 25f, 25f);
                    TooltipHandler.TipRegion(buttonRect, "EchoColony.UndoTooltip".Translate());
                    if (Widgets.ButtonText(buttonRect, "✖"))
                    {
                        chatLog.RemoveAt(index + 1);
                        chatLog.RemoveAt(index);

                        if (messageHistory.Count >= 2 &&
                            messageHistory[messageHistory.Count - 2].role == "user" &&
                            messageHistory[messageHistory.Count - 1].role == "model")
                        {
                            messageHistory.RemoveAt(messageHistory.Count - 1);
                            messageHistory.RemoveAt(messageHistory.Count - 1);
                            conversationTurnCount = Math.Max(0, conversationTurnCount - 1);
                            if (lastSavedTurnCount > conversationTurnCount) lastSavedTurnCount = conversationTurnCount;
                        }

                        if (MyMod.Settings.modelSource == ModelSource.Player2)
                            GeminiAPI.RebuildMemoryFromChat(pawn);

                        cachedChatLog = null;
                        Messages.Message("EchoColony.LastExchangeDeleted".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }

                if (!msg.StartsWith("[USER]"))
                {
                    if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
                    {
                        Rect voiceBtnRect = new Rect(viewWidth - 180f, rect.y + 25f + 2f, 80f, 25f);
                        TooltipHandler.TipRegion(voiceBtnRect, "EchoColony.PlayAudio".Translate());
                        if (Widgets.ButtonText(voiceBtnRect, "♪"))
                        {
                            string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                            if (!string.IsNullOrEmpty(voiceId))
                            {
                                string cleanText = CleanTextForTTS(displayMsg);
                                if (!string.IsNullOrWhiteSpace(cleanText))
                                    MyStoryModComponent.Instance.StartCoroutine(TTSManager.Speak(cleanText, voiceId, "female", "en_US", 1f));
                            }
                        }
                    }

                    if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y, 80f, 25f), "EchoColony.EditButton".Translate()))
                    { editingIndex = index; editedMessage = displayMsg; }

                    bool isRegenerable = false;
                    if (index >= 1 && currentChatLog[index - 1].StartsWith("[USER]"))
                    {
                        isRegenerable = (index == currentChatLog.Count - 1) ||
                            (index + 1 == currentChatLog.Count - 1 && !currentChatLog[index + 1].StartsWith("[USER]"));
                    }
                    bool showRegen = msg.StartsWith(pawn.LabelShort + ":") && !msg.EndsWith("...") && isRegenerable;

                    if (showRegen && Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y, 80f, 25f), "EchoColony.RegenerateButton".Translate()))
                    {
                        string userMsg = currentChatLog[index - 1].Substring(6);
                        if (!string.IsNullOrWhiteSpace(userMsg))
                        {
                            chatLog.RemoveAt(index);

                            if (messageHistory.Count >= 2 &&
                                messageHistory[messageHistory.Count - 1].role == "model" &&
                                messageHistory[messageHistory.Count - 2].role == "user")
                            {
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                            }

                            UpdateContextPrompt();
                            messageHistory.Add(new GeminiMessage("user", userMsg));

                            while (chatLog.Count > 0 && chatLog.Last().StartsWith(pawn.LabelShort + ": ..."))
                                chatLog.RemoveAt(chatLog.Count - 1);

                            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": ...");
                            cachedChatLog = null; waitingForResponse = true; forceScrollToBottom = true;

                            IEnumerator coroutine;

                            if (MyMod.Settings.modelSource == ModelSource.Local)
                            {
                                if (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                                    coroutine = GeminiAPI.SendRequestToLocalModel(KoboldPromptBuilder.Build(pawn, userMsg), OnResponse);
                                else if (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                                    coroutine = GeminiAPI.SendRequestToLocalModel(LMStudioPromptBuilder.Build(pawn, userMsg), OnResponse);
                                else
                                    coroutine = GeminiAPI.SendRequestToLocalModel(userMsg, OnResponse);
                            }
                            else if (MyMod.Settings.modelSource == ModelSource.Player2)
                                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, userMsg, OnResponse, _visionBase64);
                            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                                coroutine = GeminiAPI.SendRequestToOpenRouter(userMsg, OnResponse, _visionBase64);
                            else if (MyMod.Settings.modelSource == ModelSource.Custom)
                                // Regeneration: rebuild plain text prompt
                                coroutine = GeminiAPI.SendRequestToCustomProvider(ColonistPromptContextBuilder.Build(pawn, userMsg), OnResponse);
                            else
                            {
                                string json = BuildGeminiChatJson(messageHistory, _visionBase64);
                                coroutine   = GeminiAPI.SendRequestToGemini(json, OnResponse);
                            }

                            MyStoryModComponent.Instance.StartCoroutine(coroutine);
                            cachedChatLog = null;
                            return;
                        }
                    }
                }
            }
        }

        private void ResetTurnCounter()
        {
            conversationTurnCount = 0;
            lastSavedTurnCount    = 0;
            Log.Message($"[EchoColony] Conversation counters reset to zero for {pawn.LabelShort}");
        }
    }
}