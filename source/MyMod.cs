using UnityEngine;
using Verse;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using RimWorld;
using System.Linq;
using EchoColony.SpontaneousMessages;
using static EchoColony.GeminiSettings;
using EchoColony.Conversations;

namespace EchoColony
{
    public class MyMod : Mod
    {
        public static GeminiSettings Settings;
        private Vector2 scrollPos              = Vector2.zero;
        private Vector2 settingsScrollPosition = Vector2.zero;
        private Vector2 convPromptScroll       = Vector2.zero;

        private ModelSource previousModelSource = ModelSource.Gemini;

        private static bool pingInProgress = false;

        private List<GeminiModelInfo> _fetchedGeminiModels = new List<GeminiModelInfo>();
        private bool    _isFetchingModels   = false;
        private string  _fetchModelStatus   = "";
        private string  _modelSearchText    = "";
        private Vector2 _modelListScrollPos = Vector2.zero;
        private const int MAX_VISIBLE_GEMINI_MODELS = 8;

        private string _pendingModelSelection = "";

        public MyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<GeminiSettings>();

            if (string.IsNullOrEmpty(Settings.selectedModel) && Settings.modelPreferences != null)
            {
                if (!Settings.modelPreferences.useAutoSelection)
                {
                    if (!string.IsNullOrEmpty(Settings.modelPreferences.preferredAdvancedModel))
                        Settings.selectedModel = Settings.modelPreferences.preferredAdvancedModel;
                    else if (!string.IsNullOrEmpty(Settings.modelPreferences.preferredFastModel))
                        Settings.selectedModel = Settings.modelPreferences.preferredFastModel;
                }
            }
        }

        public override string SettingsCategory() => "EchoColony";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float totalHeight = 4200f;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, totalHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            DrawSectionHeader(list, "EchoColony.Header_BasicSettings".Translate(), new Color(0.7f, 0.9f, 1f));

            list.CheckboxLabeled("EchoColony.EnableSocialAffectsPersonality".Translate(), ref Settings.enableSocialAffectsPersonality);
            list.CheckboxLabeled("EchoColony.EnableRoleplayResponses".Translate(), ref Settings.enableRoleplayResponses);
            list.CheckboxLabeled("EchoColony.IgnoreDangers".Translate(), ref Settings.ignoreDangersInConversations, "EchoColony.IgnoreDangersTooltip".Translate());

            list.Gap(4f);

            string timeoutDisplay = Settings.chatSessionTimeoutHours == 0
                ? "EchoColony.ChatSessionTimeoutUnlimited".Translate().ToString()
                : Settings.chatSessionTimeoutHours + "h";

            list.Label("EchoColony.ChatSessionTimeoutHours".Translate() + ": " + timeoutDisplay,
                       tooltip: "EchoColony.ChatSessionTimeoutHoursTooltip".Translate());

            // Slider con rango de 0 a 48 horas (0 = la conversación nunca se reinicia)
            Settings.chatSessionTimeoutHours = Mathf.RoundToInt(list.Slider(Settings.chatSessionTimeoutHours, 0f, 48f));

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_MemorySystem".Translate(), new Color(0.7f, 0.9f, 1f));

            bool oldMemoryState = Settings.enableMemorySystem;
            list.CheckboxLabeled("EchoColony.EnableMemorySystem".Translate(), ref Settings.enableMemorySystem, "EchoColony.EnableMemorySystemTooltip".Translate());
            if (oldMemoryState != Settings.enableMemorySystem)
                OnMemorySystemToggled(Settings.enableMemorySystem);

            DrawStatusIndicator(list, Settings.enableMemorySystem, "EchoColony.StatusName_Memory".Translate());

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_UISettings".Translate(), new Color(0.7f, 0.9f, 1f));

            list.CheckboxLabeled("EchoColony.ShowStorytellerButton".Translate(), ref Settings.enableStorytellerButton,
                "EchoColony.ShowStorytellerButtonTooltip".Translate());

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_AnimalChat".Translate(), new Color(0.8f, 1f, 0.6f));

            list.Label("EchoColony.AnimalDefaultStyle".Translate());
            if (list.ButtonText((Settings.defaultAnimalNarrativeStyle == AnimalNarrativeStyle.FirstPerson
                    ? "EchoColony.AnimalStyleFirstPerson" : "EchoColony.AnimalStyleThirdPerson").Translate()))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("EchoColony.AnimalStyleThirdPersonExample".Translate(), () =>
                        Settings.defaultAnimalNarrativeStyle = AnimalNarrativeStyle.ThirdPerson),
                    new FloatMenuOption("EchoColony.AnimalStyleFirstPersonExample".Translate(), () =>
                        Settings.defaultAnimalNarrativeStyle = AnimalNarrativeStyle.FirstPerson)
                }));
            }

            list.Gap();
            GUI.color = Color.gray;
            list.Label("EchoColony.AnimalStyleNote".Translate());
            GUI.color = Color.white;

            list.GapLine();

            DrawConversationsSection(list);
            list.GapLine();

            DrawMonologuesSection(list);
            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_SpontaneousMessages".Translate(), new Color(0.8f, 1f, 0.8f));

            list.Label("EchoColony.SpontaneousMessagesMode".Translate());
            if (list.ButtonText(GetSpontaneousModeLabelTranslated(Settings.spontaneousMessageMode)))
                ShowSpontaneousMessageModeMenu();

            if (Settings.IsSpontaneousMessagesActive())
            {
                list.Gap();
                DrawStatusIndicator(list, true, "EchoColony.StatusName_System".Translate());
                list.Gap();
                list.Label("EchoColony.DefaultMaxMessagesPerDay".Translate() + ": " + Settings.defaultMaxMessagesPerColonistPerDay);
                Settings.defaultMaxMessagesPerColonistPerDay = (int)list.Slider(Settings.defaultMaxMessagesPerColonistPerDay, 1, 3);
                list.Label("EchoColony.DefaultCooldownHours".Translate() + ": " + Settings.defaultColonistCooldownHours.ToString("F1") + "h");
                Settings.defaultColonistCooldownHours = list.Slider(Settings.defaultColonistCooldownHours, 1f, 48f);
                if (Settings.AreRandomMessagesEnabled())
                {
                    list.Label("EchoColony.RandomMessageInterval".Translate() + ": " + Settings.randomMessageIntervalHours.ToString("F1") + "h");
                    Settings.randomMessageIntervalHours = list.Slider(Settings.randomMessageIntervalHours, 2f, 72f);
                }
                list.Gap();
                list.CheckboxLabeled("EchoColony.PrioritizeSocialTraits".Translate(), ref Settings.prioritizeSocialTraits, "EchoColony.PrioritizeSocialTraitsTooltip".Translate());
                list.Label("EchoColony.MinConsciousness".Translate() + ": " + Settings.minConsciousnessPercent.ToString("F0") + "%");
                Settings.minConsciousnessPercent = list.Slider(Settings.minConsciousnessPercent, 0f, 100f);
                list.Gap();
                if (list.ButtonText("EchoColony.ConfigureIndividualColonists".Translate()))
                {
                    if (Current.Game == null)
                        Messages.Message("EchoColony.NeedActiveGame".Translate(), MessageTypeDefOf.RejectInput);
                    else
                        Find.WindowStack.Add(new ColonistMessageConfigWindow());
                }
            }
            else
            {
                DrawStatusIndicator(list, false, "EchoColony.StatusName_System".Translate());
            }

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.StorytellerSectionHeader".Translate(), new Color(1f, 0.9f, 0.6f));

            list.Label("EchoColony.StorytellerMessagesMode".Translate());
            if (list.ButtonText(GetStorytellerModeLabelTranslated(Settings.storytellerMessageMode)))
                ShowStorytellerMessageModeMenu();

            if (Settings.IsStorytellerMessagesActive())
            {
                list.Gap();
                DrawStatusIndicator(list, true, "EchoColony.StatusName_System".Translate());

                if (Settings.AreStorytellerRandomMessagesEnabled())
                {
                    list.Gap();
                    list.Label("EchoColony.StorytellerRandomInterval".Translate() + ": " + Settings.storytellerRandomIntervalMinutes.ToString("F0"));
                    Settings.storytellerRandomIntervalMinutes = list.Slider(Settings.storytellerRandomIntervalMinutes, 5f, 120f);
                }

                if (Settings.AreStorytellerIncidentMessagesEnabled())
                {
                    list.Gap();
                    list.Label("EchoColony.StorytellerIncidentChance".Translate() + ": " + (Settings.storytellerIncidentChance * 100f).ToString("F0") + "%");
                    Settings.storytellerIncidentChance = list.Slider(Settings.storytellerIncidentChance, 0f, 1f);

                    list.Gap();
                    list.Label("EchoColony.StorytellerReactTo".Translate());
                    list.CheckboxLabeled("  → " + "EchoColony.StorytellerCommentOnDeaths".Translate(), ref Settings.storytellerCommentOnDeaths, "EchoColony.StorytellerCommentOnDeathsTooltip".Translate());
                    list.CheckboxLabeled("  → " + "EchoColony.StorytellerCommentOnMentalBreaks".Translate(), ref Settings.storytellerCommentOnMentalBreaks, "EchoColony.StorytellerCommentOnMentalBreaksTooltip".Translate());
                    list.CheckboxLabeled("  → " + "EchoColony.StorytellerCommentOnRelationships".Translate(), ref Settings.storytellerCommentOnRelationships, "EchoColony.StorytellerCommentOnRelationshipsTooltip".Translate());
                    list.CheckboxLabeled("  → " + "EchoColony.StorytellerCommentOnResearch".Translate(), ref Settings.storytellerCommentOnResearch, "EchoColony.StorytellerCommentOnResearchTooltip".Translate());
                }

                list.Gap();
                list.Label("EchoColony.StorytellerCommentCooldown".Translate() + ": " + Settings.storytellerCommentCooldownSeconds.ToString("F0") + "s",
                    tooltip: "EchoColony.StorytellerCommentCooldownTooltip".Translate());
                Settings.storytellerCommentCooldownSeconds = list.Slider(Settings.storytellerCommentCooldownSeconds, 0f, 600f);

                list.Label("EchoColony.StorytellerDuplicateBlock".Translate() + ": " +
                    (Settings.storytellerDuplicateBlockMinutes < 1f ? "EchoColony.StorytellerDuplicateBlockOff".Translate().ToString() : Settings.storytellerDuplicateBlockMinutes.ToString("F0") + " min"),
                    tooltip: "EchoColony.StorytellerDuplicateBlockTooltip".Translate());
                Settings.storytellerDuplicateBlockMinutes = list.Slider(Settings.storytellerDuplicateBlockMinutes, 0f, 60f);

                list.Gap();
                list.CheckboxLabeled("EchoColony.StorytellerMessageAutoClose".Translate(), ref Settings.storytellerMessageAutoClose, "EchoColony.StorytellerMessageAutoCloseTooltip".Translate());
                if (Settings.storytellerMessageAutoClose)
                {
                    list.Label("EchoColony.StorytellerMessageAutoCloseDelay".Translate() + ": " + Settings.storytellerMessageAutoCloseSeconds.ToString("F0"));
                    Settings.storytellerMessageAutoCloseSeconds = list.Slider(Settings.storytellerMessageAutoCloseSeconds, 3f, 30f);
                }

                list.Gap();
                list.CheckboxLabeled("EchoColony.StorytellerMessagePlaySound".Translate(), ref Settings.storytellerMessagePlaySound, "EchoColony.StorytellerMessagePlaySoundTooltip".Translate());

                list.Gap();
                if (list.ButtonText("EchoColony.StorytellerMessageTest".Translate()))
                {
                    if (Find.Storyteller == null)
                        Messages.Message("EchoColony.LoadGameFirst".Translate(), MessageTypeDefOf.RejectInput);
                    else
                        StorytellerSpontaneousMessageSystem.GenerateSpontaneousMessage(
                            StorytellerSpontaneousMessageSystem.MessageTriggerType.Random, isTest: true);
                }
            }
            else
            {
                DrawStatusIndicator(list, false, "EchoColony.StatusName_System".Translate());
            }

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_DivineActions".Translate(), new Color(1f, 0.8f, 0.4f));

            list.CheckboxLabeled("EchoColony.EnableDivineActions".Translate(), ref Settings.enableDivineActions,
                "EchoColony.EnableDivineActionsTooltip".Translate());

            if (Settings.enableDivineActions)
            {
                list.Gap();
                list.CheckboxLabeled("  → " + "EchoColony.AllowNegativeActions".Translate(), ref Settings.allowNegativeActions, "EchoColony.AllowNegativeActionsTooltip".Translate());
                list.CheckboxLabeled("  → " + "EchoColony.AllowExtremeActions".Translate(), ref Settings.allowExtremeActions, "EchoColony.AllowExtremeActionsTooltip".Translate());
            }

            list.GapLine();

            DrawVisionSection(list);
            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // FACTION COMMS CHAT
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "EchoColony.Header_FactionComms".Translate(), new Color(0.7f, 1f, 0.85f));

            list.CheckboxLabeled(
                "EchoColony.EnableFactionComms".Translate(),
                ref Settings.enableFactionCommsChat,
                "EchoColony.EnableFactionCommsTooltip".Translate());

            if (Settings.enableFactionCommsChat)
            {
                list.Gap(4f);
                GUI.color = Color.gray;
                list.Label("EchoColony.FactionGoodwillCooldown".Translate());
                GUI.color = Color.white;
                list.Label("EchoColony.FactionGoodwillCooldownValue".Translate(Settings.factionChatGoodwillCooldownHours.ToString("F0")));
                Settings.factionChatGoodwillCooldownHours = list.Slider(
                    Settings.factionChatGoodwillCooldownHours, 1f, 72f);

                list.Gap(2f);
                GUI.color = Color.gray;
                list.Label("EchoColony.FactionGoodwillCooldownNote".Translate());
                GUI.color = Color.white;
            }

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_GlobalPrompt".Translate(), new Color(0.7f, 0.9f, 1f));

            list.Label("EchoColony.GlobalPrompt".Translate());
            float areaHeight = 80f;
            Rect  scrollOut  = list.GetRect(areaHeight);
            Rect  scrollView = new Rect(0, 0, scrollOut.width - 16f, areaHeight * 2);
            Widgets.BeginScrollView(scrollOut, ref scrollPos, scrollView);
            Settings.globalPrompt = PromptTextArea.Draw(scrollView, Settings.globalPrompt, "EchoGlobalPromptArea");
            Widgets.EndScrollView();

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_AIModel".Translate(), new Color(0.6f, 1f, 0.6f));

            bool isPlayer2     = Settings.modelSource == ModelSource.Player2;
            bool checkboxState = isPlayer2;
            list.CheckboxLabeled("EchoColony.UsePlayer2Label".Translate(), ref checkboxState, "EchoColony.UsePlayer2Tooltip".Translate());

            if (checkboxState != isPlayer2)
            {
                if (checkboxState)
                {
                    previousModelSource  = Settings.modelSource;
                    Settings.modelSource = ModelSource.Player2;
                }
                else
                {
                    Settings.modelSource = previousModelSource;
                }
            }

            if (Settings.modelSource == ModelSource.Player2)
                DrawPlayer2Settings(list);
            else
                DrawModelSourceSelection(list);

            list.GapLine();

            DrawSectionHeader(list, "EchoColony.Header_General".Translate(), new Color(0.7f, 0.9f, 1f));

            if (Settings.modelSource != ModelSource.Player2)
            {
                list.Label("EchoColony.MaxResponseLength".Translate(Settings.maxResponseLength));
                Settings.maxResponseLength = (int)list.Slider(Settings.maxResponseLength, 50, 1000);
            }

            list.CheckboxLabeled("EchoColony.DebugModeLabel".Translate(), ref Settings.debugMode, "EchoColony.DebugModeTooltip".Translate());

            if (Settings.debugMode)
            {
                list.GapLine();
                DrawSectionHeader(list, "EchoColony.Header_DebugTools".Translate(), Color.cyan);
                DrawMemoryDebugTools(list);
                if (Settings.enableDivineActions)           DrawActionsDebugTools(list);
                if (Settings.IsSpontaneousMessagesActive())  DrawSpontaneousMessagesDebugTools(list);
                DrawTalesDebugTools(list);
            }

            list.End();
            Widgets.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 SETTINGS
        // ═══════════════════════════════════════════════════════════════

        private void DrawPlayer2Settings(Listing_Standard list)
        {
            list.Gap();

            bool authenticated = Player2AuthManager.IsAuthenticated;

            if (authenticated)
            {
                GUI.color = Color.green;
                list.Label("  " + "EchoColony.Player2Connected".Translate());
                GUI.color = Color.gray;
                if (!string.IsNullOrEmpty(Player2AuthManager.ConnectionMethod))
                    list.Label("  " + "EchoColony.Player2Via".Translate(Player2AuthManager.ConnectionMethod));
                list.Label("  " + Player2AuthManager.WebApiBase);
                GUI.color = Color.white;

                list.Gap(4f);
                if (list.ButtonText("EchoColony.Player2Disconnect".Translate()))
                    Player2AuthManager.Disconnect();
            }
            else
            {
                GUI.color = Color.yellow;
                list.Label("  " + "EchoColony.Player2NotConnected".Translate());
                GUI.color = Color.white;

                list.Gap(4f);

                bool authenticating = Player2AuthManager.IsAuthenticating;

                if (authenticating)
                {
                    GUI.color = new Color(1f, 0.9f, 0.3f);
                    if (!string.IsNullOrEmpty(Player2AuthManager.PendingUserCode))
                        list.Label("  " + "EchoColony.Player2WaitingBrowser".Translate(Player2AuthManager.PendingUserCode));
                    else
                        list.Label("  " + "EchoColony.Player2Connecting".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    Rect row  = list.GetRect(32f);
                    float half = (row.width - 8f) / 2f;

                    if (Widgets.ButtonText(new Rect(row.x, row.y, half, row.height), "EchoColony.Player2ConnectApp".Translate()))
                    {
                        MyStoryModComponent.Instance.StartCoroutine(
                            Player2AuthManager.AuthenticateViaLocalApp(success =>
                            {
                                if (!success)
                                    Messages.Message("EchoColony.Player2AppNotFound".Translate(),
                                        MessageTypeDefOf.RejectInput, false);
                            }));
                    }

                    if (Widgets.ButtonText(new Rect(row.x + half + 8f, row.y, half, row.height), "EchoColony.Player2ConnectBrowser".Translate()))
                    {
                        MyStoryModComponent.Instance.StartCoroutine(
                            Player2AuthManager.AuthenticateViaBrowser());
                    }

                    list.Gap(2f);
                    GUI.color = Color.gray;
                    list.Label("  " + "EchoColony.Player2ConnectHelp".Translate());
                    GUI.color = Color.white;
                }
            }

            list.GapLine();

            list.CheckboxLabeled("EchoColony.EnableTTS".Translate(), ref Settings.enableTTS);
            if (Settings.enableTTS)
                list.CheckboxLabeled("EchoColony.AutoPlayVoice".Translate(), ref Settings.autoPlayVoice);
        }

        // ═══════════════════════════════════════════════════════════════
        // VISION SECTION
        // ═══════════════════════════════════════════════════════════════

        private void DrawVisionSection(Listing_Standard list)
        {
            DrawSectionHeader(list, "EchoColony.Header_Vision".Translate(), new Color(0.6f, 0.9f, 1f));

            GUI.color = Color.gray;
            list.Label("EchoColony.VisionDescription".Translate());
            GUI.color = Color.white;

            list.Gap(4f);

            if (Settings.modelSource == ModelSource.Local || Settings.modelSource == ModelSource.Custom)
            {
                GUI.color = new Color(1f, 0.75f, 0.3f);
                list.Label("EchoColony.VisionNotAvailable".Translate());
                GUI.color = Color.white;
                list.Gap(2f);
            }

            list.CheckboxLabeled(
                "EchoColony.EnableVision".Translate(),
                ref Settings.enableVision,
                "EchoColony.EnableVisionTooltip".Translate());

            if (Settings.enableVision)
            {
                list.Gap(4f);
                bool supported = Settings.IsVisionActive();
                GUI.color = supported ? Color.green : Color.gray;
                list.Label("  " + (supported ? "EchoColony.VisionActive" : "EchoColony.VisionUnsupported").Translate(Settings.modelSource.ToString()));
                GUI.color = Color.white;

                if (supported)
                {
                    list.Gap(2f);
                    GUI.color = Color.gray;
                    list.Label("EchoColony.VisionScreenshotInfo".Translate());
                    GUI.color = Color.white;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MODEL SOURCE SELECTION
        // ═══════════════════════════════════════════════════════════════

        private void DrawModelSourceSelection(Listing_Standard list)
        {
            list.Gap();
            list.Label("EchoColony.ModelSource".Translate());

            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseGemini".Translate(),     Settings.modelSource == ModelSource.Gemini))
                Settings.modelSource = ModelSource.Gemini;
            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseLocal".Translate(),      Settings.modelSource == ModelSource.Local))
                Settings.modelSource = ModelSource.Local;
            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseOpenRouter".Translate(), Settings.modelSource == ModelSource.OpenRouter))
                Settings.modelSource = ModelSource.OpenRouter;
            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseCustomProvider".Translate(),  Settings.modelSource == ModelSource.Custom))
                Settings.modelSource = ModelSource.Custom;

            list.Gap();

            switch (Settings.modelSource)
            {
                case ModelSource.Local:      DrawLocalModelSettings(list);    break;
                case ModelSource.OpenRouter: DrawOpenRouterSettings(list);    break;
                case ModelSource.Gemini:     DrawGeminiSettings(list);        break;
                case ModelSource.Custom:     DrawCustomProviderSettings(list); break;
            }
        }

        private void DrawLocalModelSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.LocalModelSettings".Translate());
            GUI.color = Color.white;

            list.Label("EchoColony.LocalModelProvider".Translate());
            if (list.ButtonText(Settings.localModelProvider.ToString()))
            {
                var options = new List<FloatMenuOption>();
                foreach (LocalModelProvider p in Enum.GetValues(typeof(LocalModelProvider)))
                    options.Add(new FloatMenuOption(p.ToString(), () => Settings.localModelProvider = p));
                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.Label("EchoColony.LocalModelEndpoint".Translate());
            Settings.localModelEndpoint = list.TextEntry(Settings.localModelEndpoint);
            list.Label("EchoColony.LocalModelName".Translate());
            Settings.localModelName = list.TextEntry(Settings.localModelName);

            if (Settings.localModelProvider == LocalModelProvider.Ollama)
            {
                list.Gap(4f);
                list.CheckboxLabeled(
                    "EchoColony.OllamaDisableThinking".Translate(),
                    ref Settings.ollamaDisableThinking,
                    "EchoColony.OllamaDisableThinkingTooltip".Translate());
            }
        }

        private void DrawOpenRouterSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.OpenRouterSettings".Translate());
            GUI.color = Color.white;

            list.Label("EchoColony.OpenRouterEndpoint".Translate());
            Settings.openRouterEndpoint = list.TextEntry(Settings.openRouterEndpoint);
            list.Label("EchoColony.OpenRouterAPIKey".Translate());
            Settings.openRouterApiKey = list.TextEntry(Settings.openRouterApiKey);
            list.Label("EchoColony.OpenRouterModel".Translate());
            Settings.openRouterModel = list.TextEntry(Settings.openRouterModel);
        }

        // ═══════════════════════════════════════════════════════════════
        // CUSTOM PROVIDER SETTINGS
        // ═══════════════════════════════════════════════════════════════

        private void DrawCustomProviderSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.CustomProviderSettings".Translate());
            GUI.color = Color.white;

            list.Gap(2f);
            GUI.color = Color.gray;
            list.Label("EchoColony.CustomProviderWorksWith".Translate());
            GUI.color = Color.white;

            list.Gap(4f);

            list.Label("EchoColony.CustomEndpoint".Translate());
            Settings.customEndpoint = list.TextEntry(Settings.customEndpoint);

            list.Gap(2f);
            list.Label("EchoColony.CustomApiKey".Translate());
            Settings.customApiKey = list.TextEntry(Settings.customApiKey);

            list.Gap(2f);
            list.Label("EchoColony.CustomModelName".Translate());
            Settings.customModelName = list.TextEntry(Settings.customModelName);

            list.Gap(6f);

            if (list.ButtonText("EchoColony.TestConnection".Translate()))
            {
                if (MyStoryModComponent.Instance != null)
                    MyStoryModComponent.Instance.StartCoroutine(TestCustomProviderConnection());
                else
                    Messages.Message("EchoColony.LoadGameToTest".Translate(), MessageTypeDefOf.RejectInput);
            }

            list.Gap(4f);
            GUI.color = Color.gray;
            list.Label("EchoColony.Examples".Translate());
            list.Label("  LMStudio:   http://localhost:1234/v1/chat/completions");
            list.Label("  Ollama:     http://localhost:11434/v1/chat/completions");
            list.Label("  Groq:       https://api.groq.com/openai/v1/chat/completions");
            list.Label("  Together:   https://api.together.xyz/v1/chat/completions");
            GUI.color = Color.white;
        }

        private System.Collections.IEnumerator TestCustomProviderConnection()
        {
            Messages.Message("EchoColony.CustomTesting".Translate(), MessageTypeDefOf.SilentInput);

            bool   done   = false;
            string result = "";

            yield return GeminiAPI.SendRequestToCustomProvider(
                "Reply with exactly: 'EchoColony connection test OK'",
                r => { result = r; done = true; });

            int waited = 0;
            while (!done && waited < 300) { yield return null; waited++; }

            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("⚠"))
                Messages.Message(
                    "EchoColony.CustomConnected".Translate(result.Substring(0, Math.Min(80, result.Length))),
                    MessageTypeDefOf.PositiveEvent);
            else
                Messages.Message(
                    "EchoColony.CustomFailed".Translate(result ?? ""),
                    MessageTypeDefOf.RejectInput);
        }

        // ═══════════════════════════════════════════════════════════════
        // GEMINI SETTINGS
        // ═══════════════════════════════════════════════════════════════

        private void DrawGeminiSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.GeminiSettings".Translate());
            GUI.color = Color.white;

            list.Label("EchoColony.GeminiAPIKey".Translate());
            Settings.apiKey = list.TextEntry(Settings.apiKey);

            if (string.IsNullOrEmpty(Settings.apiKey))
            {
                GUI.color = Color.yellow;
                list.Label("EchoColony.GeminiEnterKey".Translate());
                GUI.color = Color.white;
                return;
            }

            list.Gap();

            string savedModel   = GetSavedModelName();
            Rect savedModelRect = list.GetRect(36f);
            Widgets.DrawBoxSolid(savedModelRect, new Color(0.1f, 0.3f, 0.1f, 0.8f));
            DrawBorderRect(savedModelRect, new Color(0.3f, 0.8f, 0.3f), 1);
            GUI.color = Color.green;
            Widgets.Label(new Rect(savedModelRect.x + 8, savedModelRect.y + 9, savedModelRect.width, 24), "EchoColony.GeminiActiveModel".Translate(savedModel));
            GUI.color = Color.white;

            list.Gap();

            bool hasPending = !string.IsNullOrEmpty(_pendingModelSelection) && _pendingModelSelection != savedModel;
            if (hasPending)
            {
                Rect pendingRect = list.GetRect(36f);
                Widgets.DrawBoxSolid(pendingRect, new Color(0.3f, 0.2f, 0f, 0.8f));
                DrawBorderRect(pendingRect, new Color(1f, 0.8f, 0.2f), 1);
                GUI.color = new Color(1f, 0.9f, 0.3f);
                Widgets.Label(new Rect(pendingRect.x + 8, pendingRect.y + 9, pendingRect.width - 160f, 24), "EchoColony.GeminiPendingModel".Translate(_pendingModelSelection));
                GUI.color = Color.white;
                Rect confirmBtn = new Rect(pendingRect.xMax - 150f, pendingRect.y + 4, 142f, 28f);
                Widgets.DrawBoxSolid(confirmBtn, new Color(0.2f, 0.6f, 0.2f));
                Widgets.DrawHighlightIfMouseover(confirmBtn);
                if (Widgets.ButtonInvisible(confirmBtn)) ConfirmModelSelection();
                Widgets.Label(new Rect(confirmBtn.x + 8, confirmBtn.y + 6, confirmBtn.width, 24), "EchoColony.GeminiConfirmModel".Translate());
                list.Gap();
                if (list.ButtonText("EchoColony.GeminiCancelPending".Translate())) _pendingModelSelection = "";
            }

            list.Gap();

            if (!_isFetchingModels && list.ButtonText("EchoColony.GeminiFetchModels".Translate()))
            {
                _isFetchingModels = true;
                _fetchModelStatus = "EchoColony.GeminiFetching".Translate();
                _fetchedGeminiModels.Clear();
                GeminiModelFetcher.FetchModels(Settings.apiKey, (models) =>
                {
                    _isFetchingModels = false;
                    _fetchModelStatus = (models == null || models.Count == 0)
                        ? "EchoColony.GeminiFetchFailed".Translate().ToString()
                        : "EchoColony.GeminiFetchFound".Translate(models.Count).ToString();
                    if (models != null) _fetchedGeminiModels = models;
                });
            }
            else if (_isFetchingModels) list.Label("EchoColony.GeminiFetchingShort".Translate());

            if (!string.IsNullOrEmpty(_fetchModelStatus))
            {
                GUI.color = _fetchModelStatus.StartsWith("✅") ? Color.green :
                            _fetchModelStatus.StartsWith("❌") ? Color.red : Color.yellow;
                list.Label(_fetchModelStatus);
                GUI.color = Color.white;
            }

            if (_fetchedGeminiModels.Count > 0)
            {
                list.Gap();
                list.Label("EchoColony.GeminiSearch".Translate());
                _modelSearchText = list.TextEntry(_modelSearchText);

                var filtered = string.IsNullOrEmpty(_modelSearchText)
                    ? _fetchedGeminiModels
                    : _fetchedGeminiModels.Where(m => m.Name.Contains(_modelSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

                list.Label("EchoColony.GeminiModelsFound".Translate(filtered.Count));

                float rowHeight  = 30f;
                float listHeight = rowHeight * Math.Min(MAX_VISIBLE_GEMINI_MODELS, filtered.Count);
                Rect  outerRect  = list.GetRect(listHeight + 4f);
                Rect  innerRect  = new Rect(0, 0, outerRect.width - 20f, rowHeight * filtered.Count);

                Widgets.BeginScrollView(outerRect, ref _modelListScrollPos, innerRect);
                for (int i = 0; i < filtered.Count; i++)
                {
                    var  model   = filtered[i];
                    Rect rowRect = new Rect(0, i * rowHeight, innerRect.width, rowHeight - 2f);
                    bool isSaved   = model.Name == savedModel;
                    bool isPending = model.Name == _pendingModelSelection;

                    if (isSaved)                    Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.35f, 0.1f, 0.7f));
                    else if (isPending)             Widgets.DrawBoxSolid(rowRect, new Color(0.35f, 0.28f, 0f, 0.7f));
                    else if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

                    GUI.color = model.IsAdvanced ? new Color(1f, 0.7f, 0.3f) : new Color(0.5f, 0.85f, 1f);
                    Widgets.Label(new Rect(rowRect.x + 4,   rowRect.y + 7, 58f,                  rowHeight), model.IsAdvanced ? "[Pro] " : "[Flash]");
                    GUI.color = isSaved ? Color.green : isPending ? new Color(1f, 0.9f, 0.3f) : Color.white;
                    Widgets.Label(new Rect(rowRect.x + 65f, rowRect.y + 7, rowRect.width - 120f, rowHeight), model.Name);
                    GUI.color = Color.white;

                    if (isSaved)
                    { GUI.color = Color.green; Widgets.Label(new Rect(rowRect.xMax - 52f, rowRect.y + 7, 50f, rowHeight), "EchoColony.GeminiActive".Translate()); GUI.color = Color.white; }
                    else if (isPending)
                    { GUI.color = new Color(1f, 0.9f, 0.3f); Widgets.Label(new Rect(rowRect.xMax - 58f, rowRect.y + 7, 56f, rowHeight), "EchoColony.GeminiPending".Translate()); GUI.color = Color.white; }

                    if (Widgets.ButtonInvisible(rowRect) && !isSaved) _pendingModelSelection = model.Name;
                }
                Widgets.EndScrollView();

                list.Gap();
                if (list.ButtonText("EchoColony.GeminiUseDefault".Translate(GeminiAPI.DefaultModel)))
                {
                    Settings.selectedModel = GeminiAPI.DefaultModel;
                    Settings.Write();
                    _pendingModelSelection = "";
                    _fetchModelStatus      = "EchoColony.GeminiDefaultSaved".Translate();
                }
            }
            else if (!_isFetchingModels)
            {
                list.Gap();
                GUI.color = Color.gray;
                list.Label("EchoColony.GeminiFetchHint".Translate());
                GUI.color = Color.white;
            }
        }

        private string GetSavedModelName() =>
            !string.IsNullOrEmpty(Settings.selectedModel)
                ? Settings.selectedModel
                : "EchoColony.GeminiDefaultSuffix".Translate(GeminiAPI.DefaultModel).ToString();

        private void ConfirmModelSelection()
        {
            if (string.IsNullOrEmpty(_pendingModelSelection)) return;
            Settings.selectedModel = _pendingModelSelection;
            Settings.Write();
            _fetchModelStatus      = "EchoColony.GeminiModelSaved".Translate(_pendingModelSelection);
            _pendingModelSelection = "";
            Messages.Message("EchoColony.GeminiModelSet".Translate(Settings.selectedModel), MessageTypeDefOf.PositiveEvent);
            if (Settings.debugMode) Log.Message($"[EchoColony] Model confirmed: {Settings.selectedModel}");
        }

        // ═══════════════════════════════════════════════════════════════
        // PAWN CONVERSATIONS SECTION
        // ═══════════════════════════════════════════════════════════════

        private void DrawConversationsSection(Listing_Standard list)
        {
            DrawSectionHeader(list, "EchoColony.ConvSectionHeader".Translate(), new Color(0.9f, 0.75f, 1f));
            list.Label("EchoColony.ConvSectionDesc".Translate(), tooltip: "EchoColony.ConvSectionDescTooltip".Translate());
            list.Gap(2f);
            list.CheckboxLabeled("EchoColony.ConvEnableLabel".Translate(), ref Settings.enablePawnConversations, "EchoColony.ConvEnableTooltip".Translate());

            if (!Settings.enablePawnConversations)
            {
                GUI.color = Color.gray;
                list.Label("EchoColony.ConvDisabledHint".Translate());
                GUI.color = Color.white;
                return;
            }

            list.Gap(6f);

            list.Label("EchoColony.ConvLinesLabel".Translate(Settings.conversationLinesPerPawn, Settings.conversationLinesPerPawn * 2), tooltip: "EchoColony.ConvLinesTooltip".Translate());
            Settings.conversationLinesPerPawn = Mathf.RoundToInt(list.Slider(Settings.conversationLinesPerPawn, 1f, 3f));
            list.Gap(2f);

            list.Label("EchoColony.ConvDelayLabel".Translate(Settings.conversationBubbleDelay.ToString("F1")), tooltip: "EchoColony.ConvDelayTooltip".Translate());
            Settings.conversationBubbleDelay = Mathf.Round(list.Slider(Settings.conversationBubbleDelay, 0.5f, 4f) * 10f) / 10f;
            list.Gap(2f);

            list.Label(
                Settings.conversationCooldownHours == 0
                    ? "EchoColony.ConvCooldownNoneLabel".Translate()
                    : "EchoColony.ConvCooldownLabel".Translate(Settings.conversationCooldownHours),
                tooltip: "EchoColony.ConvCooldownTooltip".Translate());
            Settings.conversationCooldownHours = Mathf.RoundToInt(list.Slider(Settings.conversationCooldownHours, 0f, 24f));
            list.Gap(8f); // esta línea ya existe, el bloque nuevo va ANTES de ella

            // ── Daily conversation limit ──────────────────────────────────────────
            int today = ConversationCooldownTracker.ConversationsToday;
            string dailyLimitLabel = Settings.conversationDailyLimit <= 0
                ? "EchoColony.ConvDailyLimitUnlimited".Translate(today)
                : "EchoColony.ConvDailyLimitLabel".Translate(Settings.conversationDailyLimit, today);
            list.Label(dailyLimitLabel,
                tooltip: "EchoColony.ConvDailyLimitTooltip".Translate());
            Settings.conversationDailyLimit = Mathf.RoundToInt(list.Slider(Settings.conversationDailyLimit, 0f, 50f));
            if (Settings.conversationDailyLimit < 2)
                Settings.conversationDailyLimit = 0;
            list.Gap(8f);

            list.Label("EchoColony.ConvAnimalModeLabel".Translate(), tooltip: "EchoColony.ConvAnimalModeTooltip".Translate());
            Rect animalRect = list.GetRect(28f);
            DrawThreeWayToggle(
                animalRect,
                Settings.conversationAnimalMode,
                v => Settings.conversationAnimalMode = v,
                ConversationAnimalMode.Disabled,        "EchoColony.ConvAnimalModeDisabled".Translate(),
                ConversationAnimalMode.IntelligentOnly, "EchoColony.ConvAnimalModeIntelligentOnly".Translate(),
                ConversationAnimalMode.All,             "EchoColony.ConvAnimalModeAll".Translate());

            list.Gap(2f);
            GUI.color = Color.gray;
            string animalModeHint;
            switch (Settings.conversationAnimalMode)
            {
                case ConversationAnimalMode.Disabled:        animalModeHint = "EchoColony.ConvAnimalModeHintDisabled".Translate(); break;
                case ConversationAnimalMode.IntelligentOnly: animalModeHint = "EchoColony.ConvAnimalModeHintIntelligentOnly".Translate(); break;
                case ConversationAnimalMode.All:             animalModeHint = "EchoColony.ConvAnimalModeHintAll".Translate(); break;
                default:                                     animalModeHint = ""; break;
            }
            list.Label(animalModeHint);
            GUI.color = Color.white;
            list.Gap(8f);

            list.Label("EchoColony.ConvFiltersLabel".Translate());
            list.CheckboxLabeled("EchoColony.ConvIncludePrisoners".Translate(), ref Settings.conversationIncludePrisoners, "EchoColony.ConvIncludePrisonersTooltip".Translate());
            list.CheckboxLabeled("EchoColony.ConvIncludeSlaves".Translate(),    ref Settings.conversationIncludeSlaves,    "EchoColony.ConvIncludeSlavesTooltip".Translate());
            list.CheckboxLabeled("EchoColony.ConvIncludeGuests".Translate(),    ref Settings.conversationIncludeGuests,    "EchoColony.ConvIncludeGuestsTooltip".Translate());
            list.Gap(4f);

            list.Label(
                Settings.conversationMinOpinion <= -100
                    ? "EchoColony.ConvMinOpinionNoFilter".Translate()
                    : "EchoColony.ConvMinOpinionLabel".Translate(Settings.conversationMinOpinion),
                tooltip: "EchoColony.ConvMinOpinionTooltip".Translate());
            Settings.conversationMinOpinion = Mathf.RoundToInt(list.Slider(Settings.conversationMinOpinion, -100f, 100f));
            list.Gap(4f);

            list.Label(
                Settings.conversationMaxColonySize == 0
                    ? "EchoColony.ConvMaxColonySizeNever".Translate()
                    : "EchoColony.ConvMaxColonySizeLabel".Translate(Settings.conversationMaxColonySize),
                tooltip: "EchoColony.ConvMaxColonySizeTooltip".Translate());
            int rawSize = Mathf.RoundToInt(list.Slider(Settings.conversationMaxColonySize, 0f, 200f));
            Settings.conversationMaxColonySize = rawSize < 5 ? 0 : rawSize;
            list.Gap(8f);

            string speedLabel = Settings.conversationDisableAtSpeed == 0
                ? "EchoColony.ConvDisableAtSpeedNever".Translate()
                : "EchoColony.ConvDisableAtSpeedLabel".Translate(Settings.conversationDisableAtSpeed);
            list.Label(speedLabel, tooltip: "EchoColony.ConvDisableAtSpeedTooltip".Translate());
            Settings.conversationDisableAtSpeed = Mathf.RoundToInt(list.Slider(Settings.conversationDisableAtSpeed, 0f, 4f));
            list.Gap(4f);

            list.CheckboxLabeled("EchoColony.ConvAllowSimultaneous".Translate(), ref Settings.conversationAllowSimultaneous, "EchoColony.ConvAllowSimultaneousTooltip".Translate());
            list.Gap(8f);

            list.Label("EchoColony.ConvGlobalPromptLabel".Translate(), tooltip: "EchoColony.ConvGlobalPromptTooltip".Translate());
            const float promptHeight = 72f;
            Rect promptOuter = list.GetRect(promptHeight + 4f);
            Rect promptView  = new Rect(0f, 0f, promptOuter.width - 16f,
                Mathf.Max(promptHeight, Text.CalcHeight(Settings.conversationGlobalPrompt, promptOuter.width - 20f)));
            Widgets.BeginScrollView(promptOuter, ref convPromptScroll, promptView);
            Settings.conversationGlobalPrompt = PromptTextArea.Draw(new Rect(0f, 0f, promptView.width, promptView.height), Settings.conversationGlobalPrompt, "EchoConvGlobalPromptArea");
            Widgets.EndScrollView();
            list.Gap(8f);

            bool prevLargeFont = Settings.chatLogLargeFont;
            list.CheckboxLabeled((string)"EchoColony.ChatLogLargeFont".Translate(), ref Settings.chatLogLargeFont);
            if (Settings.chatLogLargeFont != prevLargeFont)
                Conversations.ConversationChatLogRenderer.InvalidateCache();

            list.Gap(4f);
            bool logVisible = Conversations.ConversationChatLogRenderer.IsVisible;
            if (list.ButtonText(logVisible ? (string)"EchoColony.ChatLogHide".Translate() : (string)"EchoColony.ChatLogShow".Translate()))
                Conversations.ConversationChatLogRenderer.IsVisible = !logVisible;

            list.Gap(4f);
            string hotkeyDisplay = Settings.chatLogHotkey == KeyCode.None
                ? (string)"EchoColony.ChatLogHotkeyNone".Translate()
                : Settings.chatLogHotkey.ToString();

            Rect hotkeyRow = list.GetRect(28f);
            float labelW   = hotkeyRow.width * 0.52f;
            float btnW     = hotkeyRow.width - labelW - 4f;

            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(hotkeyRow.x, hotkeyRow.y, labelW, hotkeyRow.height), "EchoColony.ChatLogHotkeyLabel".Translate());
            Text.Anchor = prevAnchor;

            if (Settings.isWaitingForChatLogKey) GUI.color = Color.yellow;
            string keyBtnText = Settings.isWaitingForChatLogKey ? (string)"EchoColony.ChatLogHotkeyPress".Translate() : hotkeyDisplay;
            if (Widgets.ButtonText(new Rect(hotkeyRow.x + labelW + 4f, hotkeyRow.y, btnW, hotkeyRow.height), keyBtnText))
                Settings.isWaitingForChatLogKey = !Settings.isWaitingForChatLogKey;
            GUI.color = Color.white;

            if (Settings.isWaitingForChatLogKey) ProcessChatLogKeyCapture();

            list.Gap(2f);
            GUI.color = Color.gray;
            list.Label("EchoColony.ChatLogHint".Translate());
            GUI.color = Color.white;
        }

        private void ProcessChatLogKeyCapture()
        {
            var ev = Event.current;
            if (ev == null) return;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            { Settings.isWaitingForChatLogKey = false; ev.Use(); return; }
            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Delete || ev.keyCode == KeyCode.Backspace))
            { Settings.chatLogHotkey = KeyCode.None; Settings.isWaitingForChatLogKey = false; ev.Use(); return; }
            if (ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
            { Settings.chatLogHotkey = ev.keyCode; Settings.isWaitingForChatLogKey = false; ev.Use(); }
        }

        private Vector2 _monoScroll = Vector2.zero;

        private void DrawMonologuesSection(Listing_Standard list)
        {
            DrawSectionHeader(list, "EchoColony.MonoSectionHeader".Translate(), new Color(1f, 0.85f, 0.5f));
            list.Label("EchoColony.MonoSectionDesc".Translate());
            list.CheckboxLabeled("EchoColony.MonoEnableLabel".Translate(), ref Settings.enableMonologues, "EchoColony.MonoEnableTooltip".Translate());
            if (!Settings.enableMonologues) return;

            list.Gap(4f);
            list.Label("EchoColony.MonoCooldownLabel".Translate(Settings.monologueCooldownHours), tooltip: "EchoColony.MonoCooldownTooltip".Translate());
            Settings.monologueCooldownHours = Mathf.RoundToInt(list.Slider(Settings.monologueCooldownHours, 1f, 24f));
            list.Gap(4f);

            float chanceDisplay = Mathf.Round(Settings.monologueChancePerHour * 100f);
            list.Label(
                chanceDisplay < 1f
                    ? "EchoColony.MonoChanceOffLabel".Translate()
                    : "EchoColony.MonoChanceLabel".Translate(chanceDisplay.ToString("F0")),
                tooltip: "EchoColony.MonoChanceTooltip".Translate());
            Settings.monologueChancePerHour = Mathf.Round(list.Slider(Settings.monologueChancePerHour, 0f, 1f) * 100f) / 100f;
            list.Gap(4f);

            list.Label("EchoColony.MonoThoughtImpactLabel".Translate(Settings.monologueMinMoodImpact.ToString("F0")), tooltip: "EchoColony.MonoThoughtImpactTooltip".Translate());
            Settings.monologueMinMoodImpact = Mathf.Round(list.Slider(Settings.monologueMinMoodImpact, 1f, 20f));
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void DrawThreeWayToggle<T>(Rect rect, T current, Action<T> setter,
            T valA, string labelA, T valB, string labelB, T valC, string labelC)
        {
            float w = rect.width / 3f;
            T cA = valA; T cB = valB; T cC = valC;
            DrawToggleButton(new Rect(rect.x,          rect.y, w - 2f, rect.height), labelA, current.Equals(valA), () => setter(cA));
            DrawToggleButton(new Rect(rect.x + w,      rect.y, w - 2f, rect.height), labelB, current.Equals(valB), () => setter(cB));
            DrawToggleButton(new Rect(rect.x + w * 2f, rect.y, w - 2f, rect.height), labelC, current.Equals(valC), () => setter(cC));
        }

        private static void DrawToggleButton(Rect rect, string label, bool active, Action onClick)
        {
            Widgets.DrawBoxSolid(rect, active ? new Color(0.25f, 0.50f, 0.35f, 0.9f) : new Color(0.18f, 0.18f, 0.18f, 0.7f));
            if (active) Widgets.DrawBox(rect, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color   = active ? Color.white : new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(rect, label);
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect)) onClick?.Invoke();
        }

        private void DrawSectionHeader(Listing_Standard list, string title, Color color)
        {
            GUI.color = color;
            list.Label($"═══ {title} ═══");
            GUI.color = Color.white;
        }

        private void DrawStatusIndicator(Listing_Standard list, bool enabled, string systemName)
        {
            GUI.color = enabled ? Color.green : Color.gray;
            list.Label("  " + (enabled ? "EchoColony.StatusEnabled" : "EchoColony.StatusDisabled").Translate(systemName));
            GUI.color = Color.white;
        }

        private void DrawBorderRect(Rect rect, Color color, int thickness)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x,                rect.y,              rect.width, thickness), color);
            Widgets.DrawBoxSolid(new Rect(rect.x,                rect.yMax-thickness, rect.width, thickness), color);
            Widgets.DrawBoxSolid(new Rect(rect.x,                rect.y,              thickness,  rect.height), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - thickness, rect.y,              thickness,  rect.height), color);
        }

        // ═══════════════════════════════════════════════════════════════
        // DEBUG TOOLS
        // ═══════════════════════════════════════════════════════════════

        private void DrawMemoryDebugTools(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.DebugMemorySystem".Translate());
            GUI.color = Color.white;
            if (list.ButtonText("EchoColony.DebugCheckMemory".Translate()))   CheckMemorySystemState();
            if (list.ButtonText("EchoColony.DebugForceCleanMemories".Translate())) ForceCleanAllMemories();
        }

        private void DrawActionsDebugTools(Listing_Standard list)
        {
            list.Gap();
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.DebugActionsSystem".Translate());
            GUI.color = Color.white;
            if (list.ButtonText("EchoColony.DebugListActions".Translate())) ListRegisteredActions();
        }

        private void DrawSpontaneousMessagesDebugTools(Listing_Standard list)
        {
            list.Gap();
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("EchoColony.DebugSpontaneousSystem".Translate());
            GUI.color = Color.white;

            if (!Settings.IsSpontaneousMessagesActive())
            {
                GUI.color = Color.yellow;
                list.Label("EchoColony.DebugSystemDisabled".Translate());
                GUI.color = Color.white;
            }

            if (list.ButtonText("EchoColony.DebugCheckStatus".Translate()))  SpontaneousMessages.SpontaneousMessagesDebug.CheckSystemStatus();
            if (list.ButtonText("EchoColony.DebugForceTestMessage".Translate()))   SpontaneousMessages.SpontaneousMessagesDebug.ForceTestMessage();
            if (list.ButtonText("EchoColony.DebugSimulateRaid".Translate()))   SpontaneousMessages.SpontaneousMessagesDebug.SimulateIncident();
            if (list.ButtonText("EchoColony.DebugListColonists".Translate())) SpontaneousMessages.SpontaneousMessagesDebug.ListColonistsStatus();

            if (list.ButtonText("EchoColony.DebugResetCooldowns".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.DebugResetCooldownsConfirm".Translate(),
                    () => SpontaneousMessages.SpontaneousMessagesDebug.ResetAllCooldowns()));
            }

            list.Gap();
            GUI.color = Color.gray;
            list.Label("EchoColony.DebugToolsHint".Translate());
            GUI.color = Color.white;
        }

        private void DrawTalesDebugTools(Listing_Standard list)
        {
            list.Gap();
            GUI.color = new Color(0.8f, 1f, 0.8f);
            list.Label("EchoColony.DebugTalesCache".Translate());
            GUI.color = Color.white;

            if (list.ButtonText("EchoColony.DebugExportTales".Translate()))
            {
                if (Current.Game == null)
                {
                    Messages.Message("EchoColony.LoadGameFirst".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== ECHOCOLONY TALES DEBUG ===");
                sb.AppendLine($"Generated: {System.DateTime.Now}");
                sb.AppendLine();

                sb.AppendLine("─── RAW TALEMANAGER (all tales in game) ───");
                var allTales = Find.TaleManager?.AllTalesListForReading ?? new System.Collections.Generic.List<RimWorld.Tale>();
                sb.AppendLine($"Total tales in TaleManager: {allTales.Count}");
                sb.AppendLine();

                int rawIdx = 0;
                foreach (var tale in allTales.OrderByDescending(t => t.date).Take(30))
                {
                    rawIdx++;
                    sb.AppendLine($"[{rawIdx}] def={tale.def?.defName ?? "null"} date={tale.date} id={tale.id}");
                    try
                    {
                        string summary = TalesCache.CleanTaleText(tale.ShortSummary ?? "");
                        sb.AppendLine($"     ShortSummary: {(string.IsNullOrWhiteSpace(summary) ? "(vacío)" : summary)}");
                    }
                    catch (System.Exception ex)
                    {
                        sb.AppendLine($"     ERROR: {ex.Message}");
                    }
                    sb.AppendLine();
                }

                if (allTales.Count == 0)
                    sb.AppendLine("  (empty — no tales recorded yet in this colony)");

                sb.AppendLine();
                sb.AppendLine("─── TALESCACHE PER COLONIST ───");
                TalesCache.Clear();

                var colonists = Find.CurrentMap?.mapPawns?.FreeColonists?.ToList()
                             ?? new System.Collections.Generic.List<Pawn>();

                sb.AppendLine($"Colonists on map: {colonists.Count}");
                sb.AppendLine();

                foreach (var pawn in colonists)
                {
                    sb.AppendLine($"── {pawn.LabelShort} (id={pawn.thingIDNumber}) ──");
                    int concernCount = allTales.Count(t => t.Concerns(pawn));
                    sb.AppendLine($"  Tales that Concerns(pawn): {concernCount}");

                    var cached = TalesCache.GetTalesFor(pawn, 8);
                    sb.AppendLine($"  TalesCache returned: {cached.Count} tales");
                    if (cached.Any())
                        foreach (var t in cached) sb.AppendLine($"    • {t}");
                    else
                        sb.AppendLine("    (none)");

                    sb.AppendLine();
                }

                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(GenFilePaths.SaveDataFolderPath),
                    "EchoColony_TalesDebug.txt");

                try
                {
                    System.IO.File.WriteAllText(path, sb.ToString());
                    Messages.Message("EchoColony.DebugTalesExported".Translate(path), MessageTypeDefOf.PositiveEvent);
                    Log.Message($"[EchoColony] Tales debug written to: {path}");
                }
                catch (System.Exception ex)
                {
                    Messages.Message("EchoColony.DebugTalesWriteError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
                    Log.Error($"[EchoColony] Tales debug write error: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SPONTANEOUS MESSAGES HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void ShowSpontaneousMessageModeMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (SpontaneousMessageMode mode in Enum.GetValues(typeof(SpontaneousMessageMode)))
            {
                string l = GetSpontaneousModeLabelTranslated(mode);
                string d = GetSpontaneousModeDescriptionTranslated(mode);
                options.Add(new FloatMenuOption(l + " - " + d, () => Settings.spontaneousMessageMode = mode));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetSpontaneousModeLabelTranslated(SpontaneousMessageMode mode)
        {
            switch (mode)
            {
                case SpontaneousMessageMode.Disabled:      return "EchoColony.ModeDisabled".Translate();
                case SpontaneousMessageMode.RandomOnly:    return "EchoColony.ModeRandomOnly".Translate();
                case SpontaneousMessageMode.IncidentsOnly: return "EchoColony.ModeIncidentsOnly".Translate();
                case SpontaneousMessageMode.Full:          return "EchoColony.ModeFull".Translate();
                default:                                   return mode.ToString();
            }
        }

        private string GetSpontaneousModeDescriptionTranslated(SpontaneousMessageMode mode)
        {
            switch (mode)
            {
                case SpontaneousMessageMode.Disabled:      return "EchoColony.ModeDisabledDesc".Translate();
                case SpontaneousMessageMode.RandomOnly:    return "EchoColony.ModeRandomOnlyDesc".Translate();
                case SpontaneousMessageMode.IncidentsOnly: return "EchoColony.ModeIncidentsOnlyDesc".Translate();
                case SpontaneousMessageMode.Full:          return "EchoColony.ModeFullDesc".Translate();
                default:                                   return "";
            }
        }

        private void ShowStorytellerMessageModeMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (StorytellerMessageMode mode in Enum.GetValues(typeof(StorytellerMessageMode)))
            {
                string l = GetStorytellerModeLabelTranslated(mode);
                string d = GetStorytellerModeDescriptionTranslated(mode);
                options.Add(new FloatMenuOption(l + " - " + d, () => Settings.storytellerMessageMode = mode));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetStorytellerModeLabelTranslated(StorytellerMessageMode mode)
        {
            switch (mode)
            {
                case StorytellerMessageMode.Disabled:      return "EchoColony.StorytellerModeDisabled".Translate();
                case StorytellerMessageMode.RandomOnly:    return "EchoColony.StorytellerModeRandomOnly".Translate();
                case StorytellerMessageMode.IncidentsOnly: return "EchoColony.StorytellerModeIncidentsOnly".Translate();
                case StorytellerMessageMode.Full:          return "EchoColony.StorytellerModeFull".Translate();
                default:                                   return mode.ToString();
            }
        }

        private string GetStorytellerModeDescriptionTranslated(StorytellerMessageMode mode)
        {
            switch (mode)
            {
                case StorytellerMessageMode.Disabled:      return "EchoColony.StorytellerModeDisabledDesc".Translate();
                case StorytellerMessageMode.RandomOnly:    return "EchoColony.StorytellerModeRandomOnlyDesc".Translate();
                case StorytellerMessageMode.IncidentsOnly: return "EchoColony.StorytellerModeIncidentsOnlyDesc".Translate();
                case StorytellerMessageMode.Full:          return "EchoColony.StorytellerModeFullDesc".Translate();
                default:                                   return "";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MEMORY SYSTEM HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void OnMemorySystemToggled(bool newState)
        {
            if (newState)
            {
                Log.Message("[EchoColony] Memory system enabled by user");
                Messages.Message("EchoColony.MemoryEnabledMessage".Translate(), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Message("[EchoColony] Memory system disabled by user");
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.DisableMemorySystemConfirm".Translate(),
                    () =>
                    {
                        var mm = MyStoryModComponent.Instance?.ColonistMemoryManager;
                        if (mm != null) mm.ForceCleanMemories();
                        Messages.Message("EchoColony.AllMemoriesDeleted".Translate(), MessageTypeDefOf.TaskCompletion);
                    }));
            }
        }

        private void CheckMemorySystemState()
        {
            var mm = MyStoryModComponent.Instance?.ColonistMemoryManager;
            if (mm == null) { Messages.Message("EchoColony.MemoryManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput); return; }
            mm.DebugPrintMemoryState();
            bool ok = mm.ValidateMemoryIntegrity();
            Messages.Message((ok ? "EchoColony.MemoryCheckOk" : "EchoColony.MemoryCheckProblems").Translate(),
                ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput);
        }

        private void ForceCleanAllMemories()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.ForceCleanMemoriesConfirm".Translate(),
                () =>
                {
                    var mm = MyStoryModComponent.Instance?.ColonistMemoryManager;
                    if (mm != null) mm.ForceCleanMemories();
                    else Messages.Message("EchoColony.MemoryManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                }));
        }

        private void ListRegisteredActions()
        {
            Actions.ActionRegistry.Initialize();
            var actions     = Actions.ActionRegistry.GetAllActions();
            var categorized = actions.GroupBy(a => a.Category).OrderBy(g => g.Key);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[EchoColony] Registered Actions ({actions.Count} total):");
            foreach (var group in categorized)
            {
                sb.AppendLine($"  {group.Key}: {group.Count()} actions");
                foreach (var action in group) sb.AppendLine($"    - {action.ActionId}");
            }
            Log.Message(sb.ToString());
            Messages.Message("EchoColony.DebugActionsListed".Translate(actions.Count), MessageTypeDefOf.TaskCompletion);
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 HELPERS
        // ═══════════════════════════════════════════════════════════════

        public static void CheckPlayer2AvailableAndWarn()
        {
            if (pingInProgress) return;
            pingInProgress = true;

            var request   = UnityWebRequest.Get(Player2AuthManager.WebApiBase + "/health");
            request.timeout = 4;
            string authHeader = Player2AuthManager.GetAuthHeader();
            if (!string.IsNullOrEmpty(authHeader))
                request.SetRequestHeader("Authorization", authHeader);

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                pingInProgress = false;
#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Messages.Message(
                        "EchoColony.Player2Unreachable".Translate(),
                        MessageTypeDefOf.RejectInput, false);
                }
            };
        }
    }
}