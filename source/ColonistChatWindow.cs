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

        // ── Layout constants ──────────────────────────────────────────────────
        private const float HeaderHeight    = 66f;  // single compact row: portrait + name + buttons
        private const float PortraitSize    = 60f;  // square crop
        private const float ButtonRowHeight = 24f;
        private const float ButtonWidth     = 92f;
        private const float ButtonGap       = 4f;

        public ColonistChatWindow(Pawn pawn)
        {
            this.pawn = pawn;
            this.closeOnClickedOutside = false;
            this.doCloseX = true;
            this.absorbInputAroundWindow = false;
            this.forcePause = true;
            this.closeOnAccept = false;
            this.preventCameraMotion = false;
            this.draggable = true;

            forceScrollToBottom = true;

            if (MyMod.Settings?.enableVision == true)
            {
                _visionBase64 = GeminiAPI.CaptureScreenshotBase64();
                if (_visionBase64 != null)
                    Log.Message($"[EchoColony] Vision ready for {pawn.LabelShort}");
                else
                    Log.Warning($"[EchoColony] Vision capture failed for {pawn.LabelShort} — will send text only");
            }

            var rawChat = ChatGameComponent.Instance.GetChat(pawn);
            messageHistory = new List<GeminiMessage>();

            //*furel - Constructor changes to support the new conversation storage logic, now the messages are stored in a list of GeminiMessage objects instead of a list of strings.
            string userPrefix = "EchoColony.UserPrefix".Translate();
            foreach (string line in rawChat)
            {
                if (line.StartsWith("[USER]"))
                {
                    string content = line.Substring(6).Trim();
                    if (content.StartsWith(userPrefix))
                        content = content.Substring(userPrefix.Length);

                    messageHistory.Add(new GeminiMessage("user", content));
                }
                //*furel - Changed the prefix for colonist lines, assistant is the current prefix.
                else if (line.StartsWith("[ASSISTANT]"))
                {
                    messageHistory.Add(new GeminiMessage("model", line.Substring(pawn.LabelShort.Length + 12).Trim()));
                }
                //*furel - Added to retrocompatibility with previous versions of EchoColony, where the colonist's name was used as a prefix for their messages.
                else if (line.StartsWith(pawn.LabelShort + ":"))
                {
                    messageHistory.Add(new GeminiMessage("model", line.Substring(pawn.LabelShort.Length + 1).Trim()));
                }
            }

            CalculateTurnCountFromHistory();
            lastSavedTurnCount = ChatGameComponent.Instance.GetInteractionInfo(pawn).LastSavedTurnCount;

            //furel - Check if the session should be reset, if so, we generate a memory for the colonist for the unproceset turns.
            if (ChatGameComponent.Instance.ShouldResetSession(pawn))
            {
                ColonistMemoryHelper.CheckAndGenerateMemory(pawn, 1);
            }

            // Si por algún motivo el turno guardado en savefile supera los turnos reales calculados, sincronizamos
            if (lastSavedTurnCount > conversationTurnCount)
            {
                lastSavedTurnCount = conversationTurnCount;
                ChatGameComponent.Instance.UpdateLastSavedTurn(pawn, lastSavedTurnCount);
            }    

            if (ChatGameComponent.Instance.GetInteractionInfo(pawn).CurrentTurn !=conversationTurnCount)
                ChatGameComponent.Instance.UpdateConversationTurn(pawn, conversationTurnCount);

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

            sb.Append("{");

            // 1. Inyectamos el System Prompt oficial de Gemini
            string systemPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                sb.Append($"\"system_instruction\": {{\"parts\": [{{\"text\": \"{EscapeJson(systemPrompt)}\"}}]}},");
            }

            sb.Append("\"contents\": [");

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
            bool savedWordWrap = Text.WordWrap; //furel - Save the current word wrap state to restore it later.
            TextAnchor savedAnchor = Text.Anchor; //furel - Save the current text anchor state to restore it later.

            if (cachedChatLog == null || cachedChatLog.Count != chatLog.Count)
                cachedChatLog = new List<string>(chatLog);

            DrawHeader(inRect);

            // ── Chat area ─────────────────────────────────────────────────────
            float chatTop    = HeaderHeight + 4f;
            float chatHeight = inRect.height - chatTop - 65f;
            Rect  scrollRect = new Rect(0f, chatTop, inRect.width - 20f, chatHeight);
            float scrollBarWidth     = 16f;
            float effectiveViewWidth = scrollRect.width - scrollBarWidth;

            float viewHeight = 0f;
            var   heights    = new List<float>();
            Text.Anchor   = TextAnchor.UpperLeft;
            Text.WordWrap = true;

            for (int i = 0; i < cachedChatLog.Count; i++)
            {
                string msg    = cachedChatLog[i];
                float  width  = msg.StartsWith("[DATE_SEPARATOR]") ? effectiveViewWidth : effectiveViewWidth - 200f;
                float  height = Text.CalcHeight(GetDisplayMessage(msg), width) + 4f;
                heights.Add(height);
                viewHeight += height + 10f;
            }

            Rect viewRect = new Rect(0f, 0f, effectiveViewWidth, viewHeight + 20f);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            if (forceScrollToBottom)
            {
                scrollPos.y         = viewHeight;
                forceScrollToBottom = false;
            }

            float y = 0f;
            int   messagesToDraw = Math.Min(cachedChatLog.Count, heights.Count);
            for (int i = 0; i < messagesToDraw; i++)
            {
                string msg         = cachedChatLog[i];
                Rect   messageRect = new Rect(0f, y, effectiveViewWidth, heights[i]);

                if (msg.StartsWith("[DATE_SEPARATOR]"))
                    DrawDateSeparator(messageRect, msg);
                else
                    DrawRegularMessage(messageRect, msg, i, effectiveViewWidth, cachedChatLog);

                y += heights[i] + 10f;
            }

            Widgets.EndScrollView();
            Text.WordWrap = false;

            // ── Input area ────────────────────────────────────────────────────
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "ChatInputField" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            Rect inputRect = new Rect(0f, inRect.height - 60f, inRect.width - 110f, 50f);
            GUI.SetNextControlName("ChatInputField");

            if (editingIndex == -1 &&
                (sendRequestedViaEnter || (Event.current.type == EventType.Layout && input.NullOrEmpty())) &&
                Find.WindowStack.Windows.LastOrDefault() == this)
            {
                GUI.FocusControl("ChatInputField");
            }

            var textStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 14, padding = new RectOffset(6, 6, 6, 6) };
            input = GUI.TextArea(inputRect, input, 500, textStyle);

            Rect sendRect    = new Rect(inRect.width - 100f, inRect.height - 60f, 100f, 30f);
            bool sendClicked = Widgets.ButtonText(sendRect, "EchoColony.SendButton".Translate());

            if (!waitingForResponse && (sendClicked || sendRequestedViaEnter))
            {
                SendMessage();
                sendRequestedViaEnter = false;
                GUI.FocusControl("ChatInputField");
            }

            Text.WordWrap = savedWordWrap;
            Text.Anchor = savedAnchor;
        }

        // ── Header ────────────────────────────────────────────────────────────
        // Row 1: portrait + pawn name + vision indicator
        // Row 2: action buttons aligned right, separated by a thin line
        private void DrawHeader(Rect inRect)
        {
            // Thin separator under the whole header
            Widgets.DrawLineHorizontal(0f, HeaderHeight, inRect.width, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            // ── Row 1: portrait + name ────────────────────────────────────────
            float row1Height = HeaderHeight - ButtonRowHeight - 6f;

            // Square portrait zoomed into face — render tall, clip square
            float renderH = PortraitSize * 2.2f;
            float renderW = PortraitSize * 1.4f;
            float faceOffsetY = renderH * 0.08f; // shift down to center on face
            float faceOffsetX = (renderW - PortraitSize) / 2f;
            GUI.BeginClip(new Rect(0f, 0f, PortraitSize, PortraitSize));
            GUI.DrawTexture(
                new Rect(-faceOffsetX, -faceOffsetY, renderW, renderH),
                PortraitsCache.Get(pawn, new Vector2(renderW, renderH), Rot4.South, default, 1.0f));
            GUI.EndClip();

            // Name + title on the same vertical center as the portrait
            float textX = PortraitSize + 8f;
            float nameY = 6f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(textX, nameY, inRect.width - textX - 200f, 26f),
                "EchoColony.TalkingWithLabel".Translate(pawn.LabelCap));
            Text.Font = GameFont.Small;

            string title = pawn.story?.title ?? pawn.kindDef?.label ?? "";
            if (!string.IsNullOrWhiteSpace(title))
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(textX, nameY + 26f, inRect.width - textX - 200f, 18f),
                    title.CapitalizeFirst());
                GUI.color = Color.white;
            }

            // Vision indicator — right side, vertically centered
            if (MyMod.Settings?.enableVision == true)
            {
                string visionLabel = _visionBase64 != null ? "👁 Vision" : "👁 ✗";
                GUI.color = _visionBase64 != null
                    ? new Color(0.5f, 1f, 0.7f, 0.8f)
                    : new Color(1f, 0.5f, 0.5f, 0.7f);
                Widgets.Label(new Rect(inRect.width - 75f, nameY + 4f, 70f, 20f), visionLabel);
                GUI.color = Color.white;
            }

            // ── Buttons — right-aligned, bottom of header ─────────────────────
            float btnY = HeaderHeight - ButtonRowHeight - 2f;

            // Buttons right-to-left: Export | Import | Personalize | Clear all
            float x = inRect.width;

            x -= ButtonWidth;
            if (Widgets.ButtonText(new Rect(x, btnY, ButtonWidth, ButtonRowHeight),
                "EchoColony.ExportButton".Translate()))
            {
                string path = ConversationPorter.Export(pawn);
                if (path != null)
                    Messages.Message("EchoColony.ExportSuccess".Translate(pawn.LabelCap), MessageTypeDefOf.TaskCompletion, false);
                else
                    Messages.Message("EchoColony.ExportFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }

            x -= ButtonWidth + ButtonGap;
            if (Widgets.ButtonText(new Rect(x, btnY, ButtonWidth, ButtonRowHeight),
                "EchoColony.ImportButtonShort".Translate()))
            {
                Find.WindowStack.Add(new ConversationImportWindow(pawn));
            }

            x -= ButtonWidth + ButtonGap;
            if (Widgets.ButtonText(new Rect(x, btnY, ButtonWidth, ButtonRowHeight),
                "EchoColony.PersonalizeButton".Translate()))
            {
                Find.WindowStack.Add(new ColonistPromptEditor(pawn));
            }

            x -= ButtonWidth + ButtonGap;
            if (Widgets.ButtonText(new Rect(x, btnY, ButtonWidth, ButtonRowHeight),
                "EchoColony.ClearAllButton".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.ClearAllConfirm".Translate(),
                    () =>
                    {
                        ChatGameComponent.Instance.ClearChat(pawn);
                        ChatGameComponent.Instance.CleanInteractionInfo(pawn);
                        messageHistory.Clear();
                        editingIndex  = -1;
                        editedMessage = "";
                        ResetTurnCounter();
                    }));
            }
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

            string lastLine = ChatGameComponent.Instance.GetChat(pawn).LastOrDefault(l => l.StartsWith("[ASSISTANT]") || l.StartsWith(pawn.LabelShort + ":"));
            if (!string.IsNullOrWhiteSpace(lastLine))
            {
                
                string textOnly = lastLine.StartsWith("[ASSISTANT]") ? lastLine.Substring(pawn.LabelShort.Length + 12).Trim() : lastLine;
                string cleanText = CleanTextForTTS(textOnly);
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

        private void SendMessage()
        {
            if (waitingForResponse || input.NullOrEmpty()) return;

            string userMsg = input;
            input = "";

            //furel - Histori message history session check, if the session is expired, we reset the conversation turn count and update the interaction tick.
            //is done here so is history messages are not present in the prompt.
            //---------------------------------
            bool sessionExpired = ChatGameComponent.Instance.ShouldResetSession(pawn);

            if (sessionExpired)
            {  
                ChatGameComponent.Instance.UpdateStartTurn(pawn, conversationTurnCount);
                ChatGameComponent.Instance.UpdateInteractionTick(pawn);
            }
            //-------------------------------------------

            //furel - grup chat context, it checks if the pawnd had a group chat before, if so, it adds the context to the prompt, if not, it sends the message without context.
            //-------------------------------------------
            string groupContext = !sessionExpired ? GroupChatGameComponent.Instance.BuildGroupChatContextString(pawn) : "";

            if (!string.IsNullOrEmpty(groupContext))
            {
                ChatGameComponent.Instance.ClearGroupHistory(pawn);
            }
            //enrichedUserMsg the group chat context + the input of the user.
            string enrichedUserMsg = string.IsNullOrEmpty(groupContext)
                ? userMsg
                : $"{groupContext}\n{userMsg}";

            //We add the user message to the message history to prevent loosing the user input and the grupo context not been shown in the UI
            //but we add the enrichedUserMsg to the prompt, so the model has the context of the group chat.
            messageHistory.Add(new GeminiMessage("user", userMsg));

            ChatGameComponent.Instance.AddLine(pawn, "[USER] " + userMsg);
            ChatGameComponent.Instance.AddLine(pawn, "[ASSISTANT]" + pawn.LabelShort + ": ...");
            cachedChatLog       = null;
            waitingForResponse  = true;
            forceScrollToBottom = true;

            bool isKobold   = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;
            bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local && MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;
            bool isCustom   = MyMod.Settings.modelSource == ModelSource.Custom;

            string prompt;

            if (isKobold)
                prompt = KoboldPromptBuilder.Build(pawn, enrichedUserMsg);
            else if (isLMStudio)
                prompt = LMStudioPromptBuilder.Build(pawn, enrichedUserMsg);
            else if (isCustom)
                prompt = ColonistPromptContextBuilder.Build(pawn, enrichedUserMsg);
            else
            {
                //Now we save the group context in the messege history so future messages can have the context of the group chat, but we don't show it in the UI.
                var apiHistory = new List<GeminiMessage>(messageHistory);
                apiHistory[apiHistory.Count - 1] = new GeminiMessage("user", enrichedUserMsg);
                prompt = BuildGeminiChatJson(apiHistory, _visionBase64);
            }

            IEnumerator coroutine;

            if (isKobold || isLMStudio || MyMod.Settings.modelSource == ModelSource.Local)
                coroutine = GeminiAPI.SendRequestToLocalModel(prompt, OnResponse);
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, enrichedUserMsg, OnResponse, _visionBase64);
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, OnResponse, _visionBase64);
            else if (isCustom)
                coroutine = GeminiAPI.SendRequestToCustomProvider(prompt, OnResponse);
            else
                coroutine = GeminiAPI.SendRequestToGemini(prompt, OnResponse);

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId)) TryAssignVoiceToPawn(pawn);
            }

            MyStoryModComponent.Instance.StartCoroutine(coroutine);
        }

        private void OnResponse(string response)
        {
            var    chat        = ChatGameComponent.Instance.GetChat(pawn);
            string thinkingMsg = "[ASSISTANT]" + pawn.LabelShort + ": ...";
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

            ChatGameComponent.Instance.AddLine(pawn, "[ASSISTANT]" + pawn.LabelShort + ": " + cleanResponse);

            if (actionResults?.Any() == true)
                foreach (var result in actionResults)
                    ChatGameComponent.Instance.AddLine(pawn, $"<color=#FFD700>{result}</color>");

            cachedChatLog = null; waitingForResponse = false; input = ""; forceScrollToBottom = true;
            messageHistory.Add(new GeminiMessage("model", response));
            conversationTurnCount++;

            Log.Message($"[EchoColony] Turn completed #{conversationTurnCount} for {pawn.LabelShort}");

            //If is the first interaction or is reset it registers the tick and the turn if not, it updates the tick of the interaction,
            //so we can track the last tick of the interaction.

            ChatGameComponent.Instance.RegisterInteraction(pawn, conversationTurnCount);

            // --- FUREL: NEW CONVERSATION STORAGE LOGIC ---
            // last saved turn now is stored in the interaction info, so we can track the number of turns since the last save to use it whit spontaneous
            // conversations and quick conversations.

            ColonistMemoryHelper.CheckAndGenerateMemory(pawn);

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

        public override void PostClose()
        {
            base.PostClose();

           ColonistMemoryHelper.CheckAndGenerateMemory(pawn, 1);
        }

        private string GetDisplayMessage(string msg)
        {
            if (msg.StartsWith("[DATE_SEPARATOR]")) return msg.Substring("[DATE_SEPARATOR]".Length).Trim();
            if (msg.StartsWith("[USER]"))
            {
                string rawText = msg.Substring(6).Trim();
                // Solo para mostrar en la interfaz:
                return "EchoColony.UserPrefix".Translate() + ": " + rawText;
            }
            if (msg.StartsWith("[ASSISTANT]"))
            {
                return msg.Substring(11).Trim();
            }   
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
                    //furel - changed the logic to remove the user prefix from the message if it exists, so that the message is stored cleanly in the chat log.
                    if (msg.StartsWith("[USER]"))
                    {
                        string userPrefix = "EchoColony.UserPrefix".Translate() + ":";
                        string cleanEdit = editedMessage;

                        if (cleanEdit.StartsWith(userPrefix))
                            cleanEdit = cleanEdit.Substring(userPrefix.Length).Trim();

                        chatLog[index] = "[USER] " + cleanEdit;
                    }
                    else
                    {
                        string cleanEdit = editedMessage;
                        string assistantPrefix = "[ASSISTANT]" + pawn.LabelShort + ":";
                        string simplePrefix = pawn.LabelShort + ":";

                        if (cleanEdit.StartsWith(assistantPrefix))
                            cleanEdit = cleanEdit.Substring(assistantPrefix.Length).Trim();
                        else if (cleanEdit.StartsWith(simplePrefix))
                            cleanEdit = cleanEdit.Substring(simplePrefix.Length).Trim();

                        chatLog[index] = "[ASSISTANT]" + pawn.LabelShort + ": " + cleanEdit;
                    }
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
                bool nextIsColonist = hasNext && (currentChatLog[index + 1].StartsWith(pawn.LabelShort + ":") || currentChatLog[index + 1].StartsWith("[ASSISTANT]"));
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
                            GeminiAPI.RebuildMemoryFromChat(pawn, 0);

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
                    bool showRegen = (msg.StartsWith("[ASSISTANT]") || msg.StartsWith(pawn.LabelShort + ":")) && !msg.EndsWith("...") && isRegenerable;

                    if (showRegen && Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y, 80f, 25f), "EchoColony.RegenerateButton".Translate()))
                    {
                        //*furel - changed process to be sure user prefix is removed from the message before sending it to the model again.
                        string rawUserLine = currentChatLog[index - 1].Substring(6).Trim();
                        string userPrefix = "EchoColony.UserPrefix".Translate() + ":";
                        string userMsg = rawUserLine.StartsWith(userPrefix)
                            ? rawUserLine.Substring(userPrefix.Length).Trim()
                            : rawUserLine;
                        if (!string.IsNullOrWhiteSpace(userMsg))
                        {
                            chatLog.RemoveAt(index);

                            if (messageHistory.Count >= 2 &&
                                messageHistory[messageHistory.Count - 1].role == "model" &&
                                messageHistory[messageHistory.Count - 2].role == "user")
                            {
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                                conversationTurnCount = Math.Max(0, conversationTurnCount - 1);
                                if (lastSavedTurnCount > conversationTurnCount) lastSavedTurnCount = conversationTurnCount;
                                ChatGameComponent.Instance.UpdateConversationTurn(pawn, conversationTurnCount);
                            }

                            messageHistory.Add(new GeminiMessage("user", userMsg));

                            while (chatLog.Count > 0 && chatLog.Last().StartsWith("[ASSISTANT]" + pawn.LabelShort + ": ..."))
                                chatLog.RemoveAt(chatLog.Count - 1);

                            ChatGameComponent.Instance.AddLine(pawn, "[ASSISTANT]" + pawn.LabelShort + ": ...");
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