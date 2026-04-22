using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using System.IO;

namespace EchoColony.Factions
{
    /// <summary>
    /// Chat window for faction comms console conversations.
    ///
    /// Colonist mode: shows portraits of the operator and faction leader,
    /// styled like a real comms console between two people.
    ///
    /// Player mode: minimal UI, no portraits, plain chat interface.
    /// The player speaks directly without a colonist intermediary.
    ///
    /// Both modes maintain separate chat histories per faction.
    /// </summary>
    public class FactionChatWindow : Window
    {
        // ── Core state ────────────────────────────────────────────────────────────
        private readonly Pawn    operatorPawn;
        private readonly Faction faction;
        private readonly bool    isPlayerMode;

        private List<string> chatLog => FactionChatGameComponent.Instance.GetChat(faction, isPlayerMode);
        private List<string> cachedChatLog = null;

        private string  input               = "";
        private bool    waitingForResponse  = false;
        private bool    forceScrollToBottom = false;
        private bool    sendRequestedViaEnter = false;
        private Vector2 scrollPos           = Vector2.zero;

        // ── Layout constants ──────────────────────────────────────────────────────
        private const float PORTRAIT_SIZE      = 80f;
        private const float HEADER_HEIGHT      = 110f;
        private const float INPUT_AREA_HEIGHT  = 60f;
        private const float BOTTOM_MARGIN      = 10f;
        private const float DIVIDER_WIDTH      = 2f;

        // ── Colors ────────────────────────────────────────────────────────────────
        private static readonly Color ColorPlayerMsg    = new Color(0.80f, 0.90f, 1.00f, 1f); // blue-ish
        private static readonly Color ColorLeaderMsg    = new Color(1.00f, 0.90f, 0.70f, 1f); // warm amber
        private static readonly Color ColorSystemMsg    = new Color(0.70f, 0.85f, 0.70f, 0.8f); // muted green
        private static readonly Color ColorHeaderBg     = new Color(0.08f, 0.10f, 0.15f, 0.95f);
        private static readonly Color ColorDivider      = new Color(0.30f, 0.45f, 0.60f, 0.6f);
        private static readonly Color ColorCommsGlow    = new Color(0.20f, 0.50f, 0.80f, 0.15f);

        public FactionChatWindow(Pawn operatorPawn, Faction faction, bool isPlayerMode)
        {
            this.operatorPawn  = operatorPawn;
            this.faction       = faction;
            this.isPlayerMode  = isPlayerMode;

            this.closeOnClickedOutside  = true;
            this.doCloseX               = true;
            this.absorbInputAroundWindow = true;
            this.forcePause             = true;
            this.closeOnAccept          = false;

            forceScrollToBottom = true;
        }

        public override Vector2 InitialSize => new Vector2(900f, 580f);

        // ═══════════════════════════════════════════════════════════════
        // MAIN DRAW
        // ═══════════════════════════════════════════════════════════════

        public override void DoWindowContents(Rect inRect)
        {
            if (cachedChatLog == null || cachedChatLog.Count != chatLog.Count)
                cachedChatLog = new List<string>(chatLog);

            // Header — different for each mode
            float headerBottom = DrawHeader(inRect);

            // Chat area
            float chatAreaTop    = headerBottom + 6f;
            float chatAreaBottom = inRect.height - INPUT_AREA_HEIGHT - BOTTOM_MARGIN;
            float chatAreaHeight = chatAreaBottom - chatAreaTop;

            DrawChatArea(new Rect(0, chatAreaTop, inRect.width, chatAreaHeight));

            // Input area
            DrawInputArea(new Rect(0, chatAreaBottom + BOTTOM_MARGIN, inRect.width, INPUT_AREA_HEIGHT));
        }

        // ═══════════════════════════════════════════════════════════════
        // HEADER
        // ═══════════════════════════════════════════════════════════════

        private float DrawHeader(Rect inRect)
        {
            Rect headerRect = new Rect(0, 0, inRect.width, HEADER_HEIGHT);

            if (isPlayerMode)
                return DrawPlayerModeHeader(headerRect);
            else
                return DrawColonistModeHeader(headerRect);
        }

        /// Colonist mode: two portraits with a "comms console" style between them.
        private float DrawColonistModeHeader(Rect headerRect)
        {
            // Background glow
            Widgets.DrawBoxSolid(headerRect, ColorHeaderBg);
            Widgets.DrawBoxSolid(headerRect, ColorCommsGlow);

            float centerX = headerRect.width / 2f;
            float portraitY = headerRect.y + (headerRect.height - PORTRAIT_SIZE) / 2f;

            // ── Left portrait: operator pawn ──────────────────────────────────────
            Rect leftPortraitRect = new Rect(
                centerX - 180f - PORTRAIT_SIZE,
                portraitY,
                PORTRAIT_SIZE, PORTRAIT_SIZE);

            if (operatorPawn != null)
            {
                GUI.DrawTexture(leftPortraitRect,
                    PortraitsCache.Get(operatorPawn, new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE),
                        Rot4.South, default, 1.25f));

                // Name below portrait
                GUI.color = new Color(0.8f, 0.9f, 1f);
                Text.Anchor = TextAnchor.UpperCenter;
                Text.Font   = GameFont.Tiny;
                Widgets.Label(
                    new Rect(leftPortraitRect.x - 10f, leftPortraitRect.yMax + 2f,
                             leftPortraitRect.width + 20f, 20f),
                    operatorPawn.LabelShort);
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font   = GameFont.Small;
            }

            // ── Center: comms title ───────────────────────────────────────────────
            Rect centerRect = new Rect(centerX - 90f, headerRect.y + 10f, 180f, headerRect.height - 20f);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font   = GameFont.Tiny;

            GUI.color = ColorDivider;
            Widgets.Label(new Rect(centerRect.x, centerRect.y, centerRect.width, 16f),
                "◄ ─── COMMS ─── ►");

            GUI.color = new Color(1f, 0.85f, 0.4f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(centerRect.x, centerRect.y + 20f, centerRect.width, 24f),
                faction.Name);

            GUI.color = new Color(0.7f, 0.8f, 0.9f, 0.8f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(centerRect.x, centerRect.y + 48f, centerRect.width, 16f),
                GetConnectionStatusLine());

            string cooldownInfo = FactionRequestHandler.GetCooldownInfo(faction);
            if (cooldownInfo != null)
            {
                GUI.color = new Color(1f, 0.7f, 0.3f, 0.7f);
                Widgets.Label(new Rect(centerRect.x, centerRect.y + 64f, centerRect.width, 16f),
                    $"⏳ {cooldownInfo}");
            }

            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font   = GameFont.Small;

            // ── Right portrait: faction leader ────────────────────────────────────
            Rect rightPortraitRect = new Rect(
                centerX + 180f,
                portraitY,
                PORTRAIT_SIZE, PORTRAIT_SIZE);

            var leader = faction.leader;
            if (leader != null)
            {
                GUI.DrawTexture(rightPortraitRect,
                    PortraitsCache.Get(leader, new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE),
                        Rot4.South, default, 1.25f));

                GUI.color   = ColorLeaderMsg;
                Text.Anchor = TextAnchor.UpperCenter;
                Text.Font   = GameFont.Tiny;
                Widgets.Label(
                    new Rect(rightPortraitRect.x - 10f, rightPortraitRect.yMax + 2f,
                             rightPortraitRect.width + 20f, 20f),
                    FactionPromptContextBuilder.GetLeaderName(faction));
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font   = GameFont.Small;
            }
            else
            {
                // No leader pawn — draw a placeholder
                Widgets.DrawBoxSolid(rightPortraitRect, new Color(0.2f, 0.2f, 0.3f));
                GUI.color   = ColorLeaderMsg;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightPortraitRect, "?");
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // Bottom divider line
            Widgets.DrawLineHorizontal(0f, headerRect.yMax, headerRect.width, ColorDivider);

            return headerRect.yMax;
        }

        /// Player mode: minimal header, no portraits, just faction name and mode indicator.
        private float DrawPlayerModeHeader(Rect headerRect)
        {
            float compactHeight = 50f;
            Rect compactRect = new Rect(0, 0, headerRect.width, compactHeight);

            Widgets.DrawBoxSolid(compactRect, ColorHeaderBg);

            // Title
            Text.Font   = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color   = new Color(1f, 0.85f, 0.5f);
            Widgets.Label(new Rect(12f, 0, compactRect.width - 200f, compactHeight),
                $"Direct comms: {faction.Name}");

            // Mode badge
            GUI.color   = new Color(0.6f, 0.8f, 1f, 0.7f);
            Text.Font   = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(0, 0, compactRect.width - 12f, compactHeight),
                "[You are speaking directly]");

            GUI.color   = Color.white;
            Text.Font   = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.DrawLineHorizontal(0f, compactHeight, headerRect.width, ColorDivider);

            return compactHeight;
        }

        private string GetConnectionStatusLine()
        {
            var component = FactionChatGameComponent.Instance;
            bool isFirst  = component?.IsFirstContact(faction, isPlayerMode) ?? true;

            if (isFirst) return "First contact";

            string lastDesc = component?.GetLastConversationDescription(faction, isPlayerMode);
            int    count    = component?.GetConversationCount(faction, isPlayerMode) ?? 0;

            return lastDesc != null
                ? $"{count} call{(count != 1 ? "s" : "")} — last: {lastDesc}"
                : $"{count} call{(count != 1 ? "s" : "")}";
        }

        // ═══════════════════════════════════════════════════════════════
        // CHAT AREA
        // ═══════════════════════════════════════════════════════════════

        private void DrawChatArea(Rect rect)
        {
            float scrollBarWidth     = 16f;
            float effectiveViewWidth = rect.width - scrollBarWidth;

            // Calculate heights
            Text.WordWrap = true;
            Text.Anchor   = TextAnchor.UpperLeft;
            Text.Font     = GameFont.Small;

            var heights = new List<float>();
            float totalHeight = 0f;

            foreach (var msg in cachedChatLog)
            {
                float h = Text.CalcHeight(GetDisplayText(msg), effectiveViewWidth - 10f) + 4f;
                heights.Add(h);
                totalHeight += h + 6f;
            }

            Rect viewRect = new Rect(0, 0, effectiveViewWidth, totalHeight + 20f);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

            if (forceScrollToBottom)
            {
                scrollPos.y         = totalHeight;
                forceScrollToBottom = false;
            }

            float y = 0;
            for (int i = 0; i < cachedChatLog.Count && i < heights.Count; i++)
            {
                string msg      = cachedChatLog[i];
                Rect   lineRect = new Rect(5f, y, effectiveViewWidth - 10f, heights[i]);

                DrawMessageLine(lineRect, msg);
                y += heights[i] + 6f;
            }

            Widgets.EndScrollView();
            Text.WordWrap = false;
        }

        private void DrawMessageLine(Rect rect, string msg)
        {
            string display = GetDisplayText(msg);

            if (msg.StartsWith("[SYSTEM]"))
            {
                GUI.color = ColorSystemMsg;
                Widgets.Label(rect, $"— {display} —");
            }
            else if (msg.StartsWith("[USER]") || msg.StartsWith("You::"))
            {
                GUI.color = ColorPlayerMsg;
                Widgets.Label(rect, display);
            }
            else
            {
                // Leader message — name in accent color, body in neutral
                string leaderName = FactionPromptContextBuilder.GetLeaderName(faction);
                int    colonIdx   = display.IndexOf(": ");

                if (colonIdx > 0 && display.StartsWith(leaderName))
                {
                    string namepart = display.Substring(0, colonIdx + 2);
                    string bodypart = display.Substring(colonIdx + 2);
                    float  nameW    = Text.CalcSize(namepart).x;

                    GUI.color = ColorLeaderMsg;
                    Widgets.Label(new Rect(rect.x, rect.y, nameW + 2f, rect.height), namepart);

                    GUI.color = new Color(1f, 0.95f, 0.85f);
                    Widgets.Label(new Rect(rect.x + nameW, rect.y, rect.width - nameW, rect.height), bodypart);
                }
                else
                {
                    GUI.color = new Color(1f, 0.95f, 0.85f);
                    Widgets.Label(rect, display);
                }
            }

            GUI.color = Color.white;
        }

        private string GetDisplayText(string msg)
        {
            if (msg.StartsWith("[USER] ")) return msg.Substring(7);
            if (msg.StartsWith("[SYSTEM]")) return msg.Substring(8).Trim();
            if (msg.StartsWith("You:: ")) return msg.Substring(6);
            return msg;
        }

        // ═══════════════════════════════════════════════════════════════
        // INPUT AREA
        // ═══════════════════════════════════════════════════════════════

        private void DrawInputArea(Rect rect)
        {
            // Enter key handling
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return ||
                 Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "FactionChatInput" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            // Text field
            Rect inputRect = new Rect(rect.x, rect.y, rect.width - 110f, rect.height);
            GUI.SetNextControlName("FactionChatInput");

            if (!waitingForResponse &&
                Event.current.type == EventType.Layout &&
                input.NullOrEmpty())
                GUI.FocusControl("FactionChatInput");

            var style = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding  = new RectOffset(6, 6, 6, 6)
            };
            input = GUI.TextArea(inputRect, input, 500, style);

            // Send button
            Rect sendRect  = new Rect(rect.xMax - 100f, rect.y, 100f, rect.height / 2f);
            bool sendClick = Widgets.ButtonText(sendRect, "Send");

            if (!waitingForResponse && (sendClick || sendRequestedViaEnter))
            {
                SendMessage();
                sendRequestedViaEnter = false;
                GUI.FocusControl(null);
            }

            // Export button
            Rect exportRect = new Rect(rect.xMax - 100f, rect.y + rect.height / 2f, 100f, rect.height / 2f);
            if (Widgets.ButtonText(exportRect, "Export"))
                ExportChat();

            // Cooldown indicator
            if (FactionActions.IsOnCooldown)
            {
                GUI.color = new Color(1f, 0.7f, 0.3f, 0.8f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(
                    new Rect(rect.x, rect.y - 16f, rect.width - 110f, 16f),
                    $"⏳ Goodwill changes on cooldown — {FactionActions.CooldownDescription} remaining");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND MESSAGE
        // ═══════════════════════════════════════════════════════════════

        private void SendMessage()
        {
            if (waitingForResponse || input.NullOrEmpty()) return;

            string userMsg = input;
            input = "";

            // Display name in chat
            string senderLabel = isPlayerMode
                ? "You"
                : "You (" + operatorPawn.LabelShort + ")";

            FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode,
                $"[USER] {senderLabel}: {userMsg}");
            FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode,
                $"{FactionPromptContextBuilder.GetLeaderName(faction)}: ...");

            cachedChatLog       = null;
            waitingForResponse  = true;
            forceScrollToBottom = true;

            // Build prompt
            string prompt = FactionPromptContextBuilder.Build(operatorPawn, faction, userMsg, isPlayerMode);

            // Dispatch to AI
            IEnumerator coroutine;
            var source = MyMod.Settings?.modelSource ?? ModelSource.Gemini;

            switch (source)
            {
                case ModelSource.Local:
                    var provider = MyMod.Settings.localModelProvider;
                    if (provider == LocalModelProvider.KoboldAI)
                        coroutine = GeminiAPI.SendRequestToLocalModel(
                            KoboldPromptBuilder.BuildRaw(prompt), OnResponse);
                    else if (provider == LocalModelProvider.LMStudio)
                        coroutine = GeminiAPI.SendRequestToLocalModel(
                            LMStudioPromptBuilder.BuildRaw(prompt), OnResponse);
                    else
                        coroutine = GeminiAPI.SendRequestToLocalModel(prompt, OnResponse);
                    break;

                case ModelSource.Player2:
                    coroutine = GeminiAPI.SendRequestToPlayer2WithPrompt(prompt, OnResponse);
                    break;

                case ModelSource.OpenRouter:
                    coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, OnResponse);
                    break;

                case ModelSource.Custom:
                    coroutine = GeminiAPI.SendRequestToCustomProvider(prompt, OnResponse);
                    break;

                default: // Gemini
                    coroutine = GeminiAPI.SendRequestToGemini(prompt, OnResponse);
                    break;
            }

            MyStoryModComponent.Instance.StartCoroutine(coroutine);
        }

        // ═══════════════════════════════════════════════════════════════
        // ON RESPONSE
        // ═══════════════════════════════════════════════════════════════

        private void OnResponse(string response)
        {
            var    log          = FactionChatGameComponent.Instance.GetChat(faction, isPlayerMode);
            string leaderName   = FactionPromptContextBuilder.GetLeaderName(faction);
            string thinkingLine = $"{leaderName}: ...";

            if (log.LastOrDefault() == thinkingLine)
                log.RemoveAt(log.Count - 1);

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("⚠") || response.StartsWith("ERROR:"))
            {
                string errorLine = string.IsNullOrWhiteSpace(response)
                    ? "<color=#FF6B6B>⚠ No response received from AI</color>"
                    : $"<color=#FF6B6B>{response}</color>";
                FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode, errorLine);
                cachedChatLog      = null;
                waitingForResponse = false;
                forceScrollToBottom = true;
                return;
            }

            // Strip any "LeaderName: " prefix the AI might add
            string clean = response.Trim();
            if (clean.StartsWith(leaderName + ": ", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(leaderName.Length + 2).Trim();

            FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode,
                $"{leaderName}: {clean}");

            cachedChatLog       = null;
            waitingForResponse  = false;
            forceScrollToBottom = true;

            // ── Detect and execute real requests ──────────────────────────────────
            // Reuse the log already fetched at the top of OnResponse
            string lastPlayerMsg = log
                .LastOrDefault(l => l.StartsWith("[USER]"))
                ?.Substring(7) ?? "";

            // Use AI to classify the request — works in any language
            MyStoryModComponent.Instance.StartCoroutine(
                DetectAndExecuteRequest(lastPlayerMsg, clean));

            // ── Evaluate conversation tone for goodwill ───────────────────────────
            EvaluateAndApplyConsequences();
        }

        // ═══════════════════════════════════════════════════════════════
        // REQUEST DETECTION + EXECUTION
        // Two-step coroutine: first classify intent, then evaluate/execute.
        // ═══════════════════════════════════════════════════════════════

        private IEnumerator DetectAndExecuteRequest(string playerMessage, string leaderResponse)
        {
            FactionRequestHandler.RequestType   requestType = FactionRequestHandler.RequestType.None;
            FactionRequestHandler.OrbitalTraderType orbType = FactionRequestHandler.OrbitalTraderType.Random;
            bool classifyDone = false;

            yield return FactionRequestHandler.DetectRequestAsync(
                faction,
                playerMessage,
                (rt, ot) => { requestType = rt; orbType = ot; classifyDone = true; });

            int w = 0;
            while (!classifyDone && w < 300) { yield return null; w++; }

            if (requestType == FactionRequestHandler.RequestType.None) yield break;

            yield return FactionRequestHandler.EvaluateAndExecute(
                faction, requestType, orbType, operatorPawn, isPlayerMode,
                leaderResponse: leaderResponse,
                playerMessage: playerMessage,
                onResult: outcomeMsg =>
                {
                    if (!string.IsNullOrWhiteSpace(outcomeMsg))
                    {
                        FactionChatGameComponent.Instance.AddLine(
                            faction, isPlayerMode, $"[SYSTEM]{outcomeMsg}");
                        cachedChatLog       = null;
                        forceScrollToBottom = true;
                    }
                });
        }

        // ═══════════════════════════════════════════════════════════════
        // CONSEQUENCE EVALUATION
        // Called after each response — analyzes the conversation so far.
        // The AI is asked to assess the tone and we translate that to goodwill.
        // ═══════════════════════════════════════════════════════════════

        private void EvaluateAndApplyConsequences()
        {
            var log = FactionChatGameComponent.Instance.GetChat(faction, isPlayerMode);

            // Need at least MIN_EXCHANGES before any effect
            int playerLines = log.Count(l => l.StartsWith("[USER]"));
            if (playerLines < 4) return;

            if (FactionActions.IsOnCooldown) return;

            // Ask AI to evaluate the conversation tone
            // We send a short meta-prompt to get a sentiment score
            string transcript = string.Join("\n", log
                .Where(l => !l.StartsWith("[SYSTEM]"))
                .Select(l => GetDisplayText(l))
                .TakeLast(12));

            string evalPrompt =
                "You are evaluating the tone of this faction negotiation conversation.\n" +
                "Respond with ONLY a single integer from -3 to +3:\n" +
                "  +3 = very friendly, warm, genuinely positive for relations\n" +
                "  +2 = friendly, polite, constructive\n" +
                "  +1 = slightly positive, mostly neutral but cordial\n" +
                "   0 = neutral, no meaningful impact\n" +
                "  -1 = slightly tense, minor friction\n" +
                "  -2 = hostile, rude, damaging to relations\n" +
                "  -3 = extremely hostile, threatening, provocative — risks retaliation\n\n" +
                "Consider the OVERALL tone of the conversation, not just the last message.\n" +
                "Respond with only the number, nothing else.\n\n" +
                "CONVERSATION:\n" + transcript;

            IEnumerator evalCoroutine = RunEvaluation(evalPrompt);
            MyStoryModComponent.Instance.StartCoroutine(evalCoroutine);
        }

        private IEnumerator RunEvaluation(string evalPrompt)
        {
            bool   done   = false;
            string result = "";

            // Use the same provider as the main chat
            IEnumerator eval;
            var source = MyMod.Settings?.modelSource ?? ModelSource.Gemini;

            switch (source)
            {
                case ModelSource.Player2:
                    eval = GeminiAPI.SendRequestToPlayer2WithPrompt(evalPrompt, r => { result = r; done = true; });
                    break;
                case ModelSource.OpenRouter:
                    eval = GeminiAPI.SendRequestToOpenRouter(evalPrompt, r => { result = r; done = true; });
                    break;
                case ModelSource.Custom:
                    eval = GeminiAPI.SendRequestToCustomProvider(evalPrompt, r => { result = r; done = true; });
                    break;
                case ModelSource.Local:
                    eval = GeminiAPI.SendRequestToLocalModel(evalPrompt, r => { result = r; done = true; });
                    break;
                default:
                    eval = GeminiAPI.SendRequestToGemini(evalPrompt, r => { result = r; done = true; });
                    break;
            }

            yield return eval;
            int waited = 0;
            while (!done && waited < 200) { yield return null; waited++; }

            if (string.IsNullOrWhiteSpace(result)) yield break;

            // Parse the sentiment score
            result = result.Trim().Replace("+", "");
            if (!int.TryParse(result, out int sentiment)) yield break;
            sentiment = Mathf.Clamp(sentiment, -3, 3);

            if (sentiment == 0) yield break;

            // Apply goodwill consequences
            if (sentiment <= -3)
            {
                FactionActions.ScheduleFutureRaid(faction, operatorPawn);
                FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode,
                    "[SYSTEM] The transmission cuts off abruptly.");
                cachedChatLog = null;
            }
            else
            {
                bool changed = FactionActions.TryApplyGoodwillFromSentiment(
                    faction, sentiment, isPlayerMode ? null : operatorPawn);

                // If goodwill went up and faction was hostile, check for peace
                if (changed && sentiment > 0)
                {
                    string peaceMsg = FactionRequestHandler.TryMakePeace(faction);
                    if (peaceMsg != null)
                    {
                        FactionChatGameComponent.Instance.AddLine(faction, isPlayerMode, $"[SYSTEM]{peaceMsg}");
                        cachedChatLog       = null;
                        forceScrollToBottom = true;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST CLOSE
        // ═══════════════════════════════════════════════════════════════

        public override void PostClose()
        {
            base.PostClose();
            FactionChatGameComponent.Instance?.RecordConversationEnded(faction, isPlayerMode);
        }

        // ═══════════════════════════════════════════════════════════════
        // EXPORT
        // ═══════════════════════════════════════════════════════════════

        private void ExportChat()
        {
            try
            {
                string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "EchoColony", "FactionChats");
                Directory.CreateDirectory(folder);

                string mode     = isPlayerMode ? "direct" : $"via_{operatorPawn?.LabelShort ?? "colonist"}";
                string filename = $"{faction.Name}_{mode}_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt";
                string fullPath = Path.Combine(folder, filename);

                var lines = new List<string>
                {
                    $"=== ECHOCOLONY FACTION COMMS EXPORT ===",
                    $"Faction: {faction.Name}",
                    $"Mode: {(isPlayerMode ? "Player speaks directly" : $"Via {operatorPawn?.LabelShort}")}",
                    $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    new string('=', 40),
                    ""
                };

                lines.AddRange(chatLog.Select(l => GetDisplayText(l)));
                File.WriteAllLines(fullPath, lines);

                Messages.Message($"Comms log exported: {Path.GetFileName(fullPath)}",
                    MessageTypeDefOf.TaskCompletion);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error exporting faction chat: {ex.Message}");
                Messages.Message("Failed to export comms log.", MessageTypeDefOf.RejectInput);
            }
        }
    }
}