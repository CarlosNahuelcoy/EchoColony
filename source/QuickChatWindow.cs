using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EchoColony.Actions;
using EchoColony.Conversations;
using RimWorld;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public class QuickChatWindow : Window
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Pawn pawn;
        private string input = "";
        private bool waitingForResponse = false;
        private bool sendRequestedViaEnter = false;

        private List<GeminiMessage> messageHistory = new List<GeminiMessage>();

        private string _visionBase64 = null;

        // ── Public accessor used by Patch_ChatGizmo ───────────────────────────────

        public Pawn Pawn => pawn;

        // ── Window setup ──────────────────────────────────────────────────────────

        public override Vector2 InitialSize => new Vector2(360f, 52f);
        public override float Margin => 4f;

        public QuickChatWindow(Pawn pawn)
        {
            this.pawn = pawn;

            doWindowBackground      = true;
            forcePause              = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside   = true;
            doCloseX                = false;
            resizeable              = false;
            draggable               = true;
            closeOnAccept           = false;

            windowRect = new Rect(
                (UI.screenWidth  - InitialSize.x) / 2f,
                UI.screenHeight  - 120f,
                InitialSize.x,
                InitialSize.y
            );

            // Vision: capture before any overlay appears (same logic as ColonistChatWindow).
            // Blocked for local models — they don't support image input.
            bool visionEnabled = MyMod.Settings?.enableVision == true
                && MyMod.Settings.modelSource != ModelSource.Local;

            if (visionEnabled)
            {
                _visionBase64 = GeminiAPI.CaptureScreenshotBase64();
                if (_visionBase64 != null)
                    Log.Message($"[EchoColony] QuickChat: Vision ready for {pawn.LabelShort}");
                else
                    Log.Warning($"[EchoColony] QuickChat: Vision capture failed for {pawn.LabelShort} — will send text only");
            }

            RebuildHistoryFromLog();
        }

        // ── History ───────────────────────────────────────────────────────────────

        private void RebuildHistoryFromLog()
        {
            messageHistory = new List<GeminiMessage>
            {
                new GeminiMessage("user", BuildContextPrompt())
            };

            foreach (string line in ChatGameComponent.Instance.GetChat(pawn))
            {
                if (line.StartsWith("[USER]"))
                    messageHistory.Add(new GeminiMessage("user", line.Substring(6).Trim()));
                else if (line.StartsWith(pawn.LabelShort + ":"))
                    messageHistory.Add(new GeminiMessage("model", line.Substring(pawn.LabelShort.Length + 1).Trim()));
            }
        }

        private string BuildContextPrompt()
        {
            string basePrompt;

            if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                if (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                    basePrompt = KoboldPromptBuilder.Build(pawn, "");
                else if (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                    basePrompt = LMStudioPromptBuilder.Build(pawn, "");
                else
                    basePrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }
            else
            {
                basePrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }

            return basePrompt +
                "\n\n[QUICK CHAT] Someone caught your attention in passing. " +
                "Respond in one short sentence — casual, natural, in character. Do not elaborate.";
        }

        // ── UI ────────────────────────────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            // Placeholder text
            if (string.IsNullOrEmpty(input) && GUI.GetNameOfFocusedControl() != "QuickChatInput")
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(
                    new Rect(8f, 0f, inRect.width - 44f, inRect.height),
                    "Quick Chat: " + pawn.LabelShort + "...");
                GUI.color = Color.white;
            }

            // Enter key
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "QuickChatInput" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            // Input field
            var inputStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 13,
                padding  = new RectOffset(8, 6, 8, 6)
            };
            GUI.SetNextControlName("QuickChatInput");
            input = GUI.TextArea(new Rect(0f, 0f, inRect.width - 36f, inRect.height), input, 500, inputStyle);

            // Auto-focus on open
            if (Event.current.type == EventType.Layout)
                GUI.FocusControl("QuickChatInput");

            // Right side: waiting indicator or send button
            Rect rightRect = new Rect(inRect.width - 32f, (inRect.height - 28f) / 2f, 28f, 28f);

            if (waitingForResponse)
            {
                GUI.color   = new Color(1f, 0.85f, 0.4f, 0.9f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightRect, "...");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color   = Color.white;
            }
            else
            {
                if (Widgets.ButtonImage(rightRect, MyModTextures.QuickChatIcon) || sendRequestedViaEnter)
                {
                    SendMessage();
                    sendRequestedViaEnter = false;
                }
            }

            // Small green dot when vision is active
            if (_visionBase64 != null)
            {
                GUI.color = new Color(0.5f, 1f, 0.7f, 0.6f);
                Widgets.Label(new Rect(inRect.width - 32f, 2f, 10f, 10f), "●");
                GUI.color = Color.white;
            }
        }

        // ── Send ──────────────────────────────────────────────────────────────────

        private void SendMessage()
        {
            if (waitingForResponse || string.IsNullOrWhiteSpace(input)) return;

            string userMsg = input.Trim();
            input = "";

            ChatGameComponent.Instance.AddLine(pawn, "[USER] " + "EchoColony.UserPrefix".Translate() + userMsg);
            waitingForResponse = true;

            // Rebuild history to include the message just logged.
            RebuildHistoryFromLog();

            // Brevity constraint appended to the actual API message — not the context —
            // so the model sees it immediately before generating its reply.
            string apiMsg = userMsg + " [Answer in one sentence, 100 characters max. Be brief.]";

            MyStoryModComponent.Instance.StartCoroutine(BuildCoroutine(userMsg, apiMsg));
            Close();
        }

        private IEnumerator BuildCoroutine(string userMsg, string apiMsg)
        {
            bool isKobold   = MyMod.Settings.modelSource == ModelSource.Local
                              && MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;
            bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local
                              && MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;
            bool isLocal    = MyMod.Settings.modelSource == ModelSource.Local;
            bool isCustom   = MyMod.Settings.modelSource == ModelSource.Custom;

            IEnumerator coroutine;

            if (isKobold)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(KoboldPromptBuilder.Build(pawn, apiMsg), OnResponse);
            }
            else if (isLMStudio)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(LMStudioPromptBuilder.Build(pawn, apiMsg), OnResponse);
            }
            else if (isLocal)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(apiMsg, OnResponse);
            }
            else if (isCustom)
            {
                coroutine = GeminiAPI.SendRequestToCustomProvider(ColonistPromptContextBuilder.Build(pawn, apiMsg), OnResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, apiMsg, OnResponse, _visionBase64);
            }
            else
            {
                messageHistory.Add(new GeminiMessage("user", apiMsg));
                string json = BuildGeminiJson(messageHistory);

                coroutine = MyMod.Settings.modelSource == ModelSource.OpenRouter
                    ? GeminiAPI.SendRequestToOpenRouter(json, OnResponse, _visionBase64)
                    : GeminiAPI.SendRequestToGemini(json, OnResponse, _visionBase64);
            }

            return coroutine;
        }

        // ── Response ──────────────────────────────────────────────────────────────

        private void OnResponse(string response)
        {
            waitingForResponse = false;

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("ERROR:") || response.StartsWith("⚠"))
            {
                Log.Error($"[EchoColony] QuickChat error for {pawn.LabelShort}: {response}");
                BubbleController.ShowBubble(pawn, "...");
                return;
            }

            string cleanResponse = response;

            if (MyMod.Settings.enableDivineActions)
            {
                try
                {
                    var processed = ActionExecutor.ProcessResponse(pawn, response);
                    if (processed != null)
                    {
                        cleanResponse = processed.CleanResponse ?? response;

                        if (processed.ExecutionResults?.Any() == true)
                            foreach (var result in processed.ExecutionResults)
                                ChatGameComponent.Instance.AddLine(pawn, $"<color=#FFD700>{result}</color>");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] QuickChat action error: {ex.Message}");
                }
            }

            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": " + cleanResponse);

            if (pawn.Spawned && !pawn.Dead)
                ShowBubbleSequence(pawn, cleanResponse);

            messageHistory.Add(new GeminiMessage("model", cleanResponse));
        }

        // ── Bubble sequence ───────────────────────────────────────────────────────

        // Splits the response into at most 3 chunks of ~80 chars each at word
        // boundaries, then fires them through BubbleSequencer so they appear
        // one after another without cutting mid-word.
        private static void ShowBubbleSequence(Pawn pawn, string text)
        {
            const int chunkSize = 35;
            const int maxChunks = 3;

            var chunks    = new List<string>();
            string remaining = text.Trim();

            while (remaining.Length > 0 && chunks.Count < maxChunks)
            {
                if (remaining.Length <= chunkSize)
                {
                    chunks.Add(remaining);
                    break;
                }

                // Find last space within chunkSize to avoid cutting mid-word.
                int cut = remaining.LastIndexOf(' ', chunkSize);
                if (cut <= 0) cut = chunkSize;

                chunks.Add(remaining.Substring(0, cut).TrimEnd());
                remaining = remaining.Substring(cut).TrimStart();
            }

            if (chunks.Count == 0) return;

            // Single chunk: fire directly.
            if (chunks.Count == 1)
            {
                BubbleController.ShowBubble(pawn, chunks[0]);
                return;
            }

            // Multiple chunks: use BubbleSequencer for timed display.
            var component = pawn.Map?.GetComponent<BubbleSequencerComponent>();
            if (component == null)
            {
                BubbleController.ShowBubble(pawn, chunks[0]);
                return;
            }

            var lines = chunks.Select(c => (pawn, c)).ToList();
            component.Sequencer.Enqueue(lines, delaySeconds: 2.5f);
        }

        private static string BuildGeminiJson(List<GeminiMessage> history)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"contents\": [");
            for (int i = 0; i < history.Count; i++)
            {
                string role = history[i].role == "model" ? "model" : "user";
                string text = EscapeJson(history[i].content);
                sb.Append($"{{\"role\": \"{role}\", \"parts\": [{{\"text\": \"{text}\"}}]}}");
                if (i < history.Count - 1) sb.Append(",");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string EscapeJson(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        // ── Inner types ───────────────────────────────────────────────────────────

        public class GeminiMessage
        {
            public string role;
            public string content;
            public GeminiMessage(string role, string content) { this.role = role; this.content = content; }
        }
    }
}