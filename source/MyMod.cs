using UnityEngine;
using Verse;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using RimWorld;
using System.Linq;
using EchoColony.SpontaneousMessages;
using static EchoColony.GeminiSettings;

namespace EchoColony
{
    public class MyMod : Mod
    {
        public static GeminiSettings Settings;
        private Vector2 scrollPos = Vector2.zero;
        private Vector2 settingsScrollPosition = Vector2.zero;

        private ModelSource previousModelSource = ModelSource.Gemini;

        private static bool pingInProgress = false;

        // Dynamic Gemini model fetching
        private List<GeminiModelInfo> _fetchedGeminiModels = new List<GeminiModelInfo>();
        private bool _isFetchingModels = false;
        private string _fetchModelStatus = "";
        private string _modelSearchText = "";
        private Vector2 _modelListScrollPos = Vector2.zero;
        private const int MAX_VISIBLE_GEMINI_MODELS = 8;

        // Pending selection state
        private string _pendingModelSelection = "";

        public MyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<GeminiSettings>();

            // Backward compat: migrate old preferences to new selectedModel field
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
            float totalHeight = 2400f;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, totalHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            // ═══════════════════════════════════════════════════════════════
            // BASIC SETTINGS
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Basic Settings", new Color(0.7f, 0.9f, 1f));

            list.CheckboxLabeled(
                "EchoColony.EnableSocialAffectsPersonality".Translate(),
                ref Settings.enableSocialAffectsPersonality
            );

            list.CheckboxLabeled(
                "EchoColony.EnableRoleplayResponses".Translate(),
                ref Settings.enableRoleplayResponses
            );

            list.CheckboxLabeled(
                "EchoColony.IgnoreDangers".Translate(),
                ref Settings.ignoreDangersInConversations,
                "EchoColony.IgnoreDangersTooltip".Translate()
            );

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // MEMORY SYSTEM
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Memory System", new Color(0.7f, 0.9f, 1f));

            bool oldMemorySystemState = Settings.enableMemorySystem;
            list.CheckboxLabeled(
                "EchoColony.EnableMemorySystem".Translate(),
                ref Settings.enableMemorySystem,
                "EchoColony.EnableMemorySystemTooltip".Translate()
            );

            if (oldMemorySystemState != Settings.enableMemorySystem)
                OnMemorySystemToggled(Settings.enableMemorySystem);

            DrawStatusIndicator(list, Settings.enableMemorySystem, "Memory system");

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // UI SETTINGS
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "UI Settings", new Color(0.7f, 0.9f, 1f));

            list.CheckboxLabeled(
                "Show Storyteller Chat Button",
                ref Settings.enableStorytellerButton,
                "Shows or hides the Storyteller chat button in the main menu bar"
            );

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // ANIMAL SETTINGS
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Animal Chat Settings", new Color(0.8f, 1f, 0.6f));

            list.Label("Default Narrative Style for Animals:");
            if (list.ButtonText(Settings.defaultAnimalNarrativeStyle.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Third Person (The dog barks)", () =>
                        Settings.defaultAnimalNarrativeStyle = AnimalNarrativeStyle.ThirdPerson),
                    new FloatMenuOption("First Person (I bark)", () =>
                        Settings.defaultAnimalNarrativeStyle = AnimalNarrativeStyle.FirstPerson)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.Gap();
            GUI.color = Color.gray;
            list.Label("Note: You can override this per-animal using Custom Prompt");
            GUI.color = Color.white;

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // SPONTANEOUS MESSAGES SYSTEM
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Spontaneous Messages System", new Color(0.8f, 1f, 0.8f));

            list.Label("EchoColony.SpontaneousMessagesMode".Translate());
            string currentModeLabel = GetSpontaneousModeLabelTranslated(Settings.spontaneousMessageMode);
            if (list.ButtonText(currentModeLabel))
                ShowSpontaneousMessageModeMenu();

            if (Settings.IsSpontaneousMessagesActive())
            {
                list.Gap();
                DrawStatusIndicator(list, true, "System");

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
                list.CheckboxLabeled(
                    "EchoColony.PrioritizeSocialTraits".Translate(),
                    ref Settings.prioritizeSocialTraits,
                    "EchoColony.PrioritizeSocialTraitsTooltip".Translate()
                );

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
                DrawStatusIndicator(list, false, "System");
            }

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // STORYTELLER SPONTANEOUS MESSAGES
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Storyteller Spontaneous Messages", new Color(1f, 0.9f, 0.6f));

            list.Label("Storyteller Message Mode:");
            string currentStorytellerModeLabel = GetStorytellerModeLabelTranslated(Settings.storytellerMessageMode);
            if (list.ButtonText(currentStorytellerModeLabel))
                ShowStorytellerMessageModeMenu();

            if (Settings.IsStorytellerMessagesActive())
            {
                list.Gap();
                DrawStatusIndicator(list, true, "System");

                if (Settings.AreStorytellerRandomMessagesEnabled())
                {
                    list.Gap();
                    list.Label("Random Message Interval (minutes): " + Settings.storytellerRandomIntervalMinutes.ToString("F0"));
                    Settings.storytellerRandomIntervalMinutes = list.Slider(Settings.storytellerRandomIntervalMinutes, 5f, 120f);
                }

                if (Settings.AreStorytellerIncidentMessagesEnabled())
                {
                    list.Gap();
                    list.Label("Incident Comment Chance: " + (Settings.storytellerIncidentChance * 100f).ToString("F0") + "%");
                    Settings.storytellerIncidentChance = list.Slider(Settings.storytellerIncidentChance, 0f, 1f);
                }

                list.Gap();
                list.CheckboxLabeled(
                    "Auto-close message window",
                    ref Settings.storytellerMessageAutoClose,
                    "Message window will close automatically after a few seconds"
                );

                if (Settings.storytellerMessageAutoClose)
                {
                    list.Label("Auto-close delay (seconds): " + Settings.storytellerMessageAutoCloseSeconds.ToString("F0"));
                    Settings.storytellerMessageAutoCloseSeconds = list.Slider(Settings.storytellerMessageAutoCloseSeconds, 3f, 30f);
                }

                list.Gap();
                list.CheckboxLabeled(
                    "Play sound with messages",
                    ref Settings.storytellerMessagePlaySound,
                    "Play a notification sound when the storyteller sends a message"
                );

                list.Gap();
                if (list.ButtonText("🧪 Test Random Message"))
                {
                    if (Find.Storyteller == null)
                        Messages.Message("Load a game first", MessageTypeDefOf.RejectInput);
                    else
                        StorytellerSpontaneousMessageSystem.GenerateSpontaneousMessage(
                            StorytellerSpontaneousMessageSystem.MessageTriggerType.Random,
                            isTest: true
                        );
                }
            }
            else
            {
                DrawStatusIndicator(list, false, "System");
            }

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // DIVINE ACTIONS SYSTEM
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Divine Actions System", new Color(1f, 0.8f, 0.4f));

            list.CheckboxLabeled(
                "Enable Divine Actions (AI can affect colonists)",
                ref Settings.enableDivineActions,
                "Allows the AI to use actions like healing, mood changes, etc. during conversations"
            );

            if (Settings.enableDivineActions)
            {
                list.Gap();
                list.CheckboxLabeled(
                    "  → Allow Negative Actions",
                    ref Settings.allowNegativeActions,
                    "Allows AI to use negative actions (mental breaks, injuries, etc.)"
                );

                list.CheckboxLabeled(
                    "  → Allow Extreme Actions",
                    ref Settings.allowExtremeActions,
                    "Allows AI to use extreme actions (amputations, resurrections, etc.)"
                );
            }

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // GLOBAL PROMPT
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "Global Prompt", new Color(0.7f, 0.9f, 1f));

            list.Label("EchoColony.GlobalPrompt".Translate());
            float areaHeight = 80f;
            Rect scrollOut = list.GetRect(areaHeight);
            Rect scrollView = new Rect(0, 0, scrollOut.width - 16f, areaHeight * 2);
            Widgets.BeginScrollView(scrollOut, ref scrollPos, scrollView);
            Settings.globalPrompt = Widgets.TextArea(scrollView, Settings.globalPrompt);
            Widgets.EndScrollView();

            list.GapLine();

            // ═══════════════════════════════════════════════════════════════
            // AI MODEL CONFIGURATION
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "AI Model Configuration", new Color(0.6f, 1f, 0.6f));

            bool isPlayer2 = Settings.modelSource == ModelSource.Player2;
            bool checkboxState = isPlayer2;

            list.CheckboxLabeled(
                "EchoColony.UsePlayer2Label".Translate(),
                ref checkboxState,
                "EchoColony.UsePlayer2Tooltip".Translate()
            );

            if (checkboxState != isPlayer2)
            {
                if (checkboxState)
                {
                    previousModelSource = Settings.modelSource;
                    Settings.modelSource = ModelSource.Player2;
                    CheckPlayer2AvailableAndWarn();
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

            // ═══════════════════════════════════════════════════════════════
            // GENERAL SETTINGS
            // ═══════════════════════════════════════════════════════════════
            DrawSectionHeader(list, "General Settings", new Color(0.7f, 0.9f, 1f));

            if (Settings.modelSource != ModelSource.Player2)
            {
                list.Label("EchoColony.MaxResponseLength".Translate(Settings.maxResponseLength));
                Settings.maxResponseLength = (int)list.Slider(Settings.maxResponseLength, 50, 1000);
            }

            list.CheckboxLabeled(
                "EchoColony.DebugModeLabel".Translate(),
                ref Settings.debugMode,
                "EchoColony.DebugModeTooltip".Translate()
            );

            // ═══════════════════════════════════════════════════════════════
            // DEBUG TOOLS
            // ═══════════════════════════════════════════════════════════════
            if (Settings.debugMode)
            {
                list.GapLine();
                DrawSectionHeader(list, "Debug Tools", Color.cyan);

                DrawMemoryDebugTools(list);

                if (Settings.enableDivineActions)
                    DrawActionsDebugTools(list);

                if (Settings.IsSpontaneousMessagesActive())
                    DrawSpontaneousMessagesDebugTools(list);
            }

            list.End();
            Widgets.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAWING HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void DrawSectionHeader(Listing_Standard list, string title, Color color)
        {
            GUI.color = color;
            list.Label($"═══ {title} ═══");
            GUI.color = Color.white;
        }

        private void DrawStatusIndicator(Listing_Standard list, bool enabled, string systemName)
        {
            if (enabled)
            {
                GUI.color = Color.green;
                list.Label($"  ✓ {systemName}: Enabled");
            }
            else
            {
                GUI.color = Color.gray;
                list.Label($"  {systemName}: Disabled");
            }
            GUI.color = Color.white;
        }

        private void DrawBorderRect(Rect rect, Color color, int thickness)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawPlayer2Settings(Listing_Standard list)
        {
            list.Gap();
            list.Label("EchoColony.Player2Warning".Translate());
            list.CheckboxLabeled("EchoColony.EnableTTS".Translate(), ref Settings.enableTTS);
            if (Settings.enableTTS)
                list.CheckboxLabeled("EchoColony.AutoPlayVoice".Translate(), ref Settings.autoPlayVoice);
        }

        private void DrawModelSourceSelection(Listing_Standard list)
        {
            list.Gap();
            list.Label("EchoColony.ModelSource".Translate());

            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseGemini".Translate(), Settings.modelSource == ModelSource.Gemini))
                Settings.modelSource = ModelSource.Gemini;
            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseLocal".Translate(), Settings.modelSource == ModelSource.Local))
                Settings.modelSource = ModelSource.Local;
            if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseOpenRouter".Translate(), Settings.modelSource == ModelSource.OpenRouter))
                Settings.modelSource = ModelSource.OpenRouter;

            list.Gap();

            switch (Settings.modelSource)
            {
                case ModelSource.Local:
                    DrawLocalModelSettings(list);
                    break;
                case ModelSource.OpenRouter:
                    DrawOpenRouterSettings(list);
                    break;
                case ModelSource.Gemini:
                    DrawGeminiSettings(list);
                    break;
            }
        }

        private void DrawLocalModelSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("Local Model Settings:");
            GUI.color = Color.white;

            list.Label("EchoColony.LocalModelProvider".Translate());
            if (list.ButtonText(Settings.localModelProvider.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (LocalModelProvider provider in Enum.GetValues(typeof(LocalModelProvider)))
                {
                    options.Add(new FloatMenuOption(provider.ToString(), () =>
                    {
                        Settings.localModelProvider = provider;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.Label("EchoColony.LocalModelEndpoint".Translate());
            Settings.localModelEndpoint = list.TextEntry(Settings.localModelEndpoint);

            list.Label("EchoColony.LocalModelName".Translate());
            Settings.localModelName = list.TextEntry(Settings.localModelName);
        }

        private void DrawOpenRouterSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("OpenRouter Settings:");
            GUI.color = Color.white;

            list.Label("EchoColony.OpenRouterEndpoint".Translate());
            Settings.openRouterEndpoint = list.TextEntry(Settings.openRouterEndpoint);

            list.Label("EchoColony.OpenRouterAPIKey".Translate());
            Settings.openRouterApiKey = list.TextEntry(Settings.openRouterApiKey);

            list.Label("EchoColony.OpenRouterModel".Translate());
            Settings.openRouterModel = list.TextEntry(Settings.openRouterModel);
        }

        private void DrawGeminiSettings(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("Gemini Settings:");
            GUI.color = Color.white;

            list.Label("EchoColony.GeminiAPIKey".Translate());
            Settings.apiKey = list.TextEntry(Settings.apiKey);

            if (string.IsNullOrEmpty(Settings.apiKey))
            {
                GUI.color = Color.yellow;
                list.Label("⚠ Enter your Gemini API key to configure models");
                GUI.color = Color.white;
                return;
            }

            list.Gap();

            // ── Modelo activo (siempre visible) ──
            string savedModel = GetSavedModelName();

            Rect savedModelRect = list.GetRect(36f);
            Widgets.DrawBoxSolid(savedModelRect, new Color(0.1f, 0.3f, 0.1f, 0.8f));
            DrawBorderRect(savedModelRect, new Color(0.3f, 0.8f, 0.3f), 1);
            GUI.color = Color.green;
            Widgets.Label(new Rect(savedModelRect.x + 8, savedModelRect.y + 9, savedModelRect.width, 24),
                $"✅ Active model: {savedModel}");
            GUI.color = Color.white;

            list.Gap();

            // ── Selección pendiente ──
            bool hasPendingSelection = !string.IsNullOrEmpty(_pendingModelSelection)
                                       && _pendingModelSelection != savedModel;

            if (hasPendingSelection)
            {
                Rect pendingRect = list.GetRect(36f);
                Widgets.DrawBoxSolid(pendingRect, new Color(0.3f, 0.2f, 0f, 0.8f));
                DrawBorderRect(pendingRect, new Color(1f, 0.8f, 0.2f), 1);

                GUI.color = new Color(1f, 0.9f, 0.3f);
                Widgets.Label(new Rect(pendingRect.x + 8, pendingRect.y + 9, pendingRect.width - 160f, 24),
                    $"⏳ Pending: {_pendingModelSelection}");
                GUI.color = Color.white;

                Rect confirmBtn = new Rect(pendingRect.xMax - 150f, pendingRect.y + 4, 142f, 28f);
                Widgets.DrawBoxSolid(confirmBtn, new Color(0.2f, 0.6f, 0.2f));
                Widgets.DrawHighlightIfMouseover(confirmBtn);
                if (Widgets.ButtonInvisible(confirmBtn))
                    ConfirmModelSelection();
                Widgets.Label(new Rect(confirmBtn.x + 8, confirmBtn.y + 6, confirmBtn.width, 24), "✔ Confirm Model");

                list.Gap();

                if (list.ButtonText("✖ Cancel pending selection"))
                    _pendingModelSelection = "";
            }

            list.Gap();

            // ── Fetch button ──
            string fetchLabel = _isFetchingModels ? "Fetching..." : "🔄 Fetch Available Models";
            if (!_isFetchingModels && list.ButtonText(fetchLabel))
            {
                _isFetchingModels = true;
                _fetchModelStatus = "Fetching models from Gemini API...";
                _fetchedGeminiModels.Clear();

                GeminiModelFetcher.FetchModels(Settings.apiKey, (models) =>
                {
                    _isFetchingModels = false;
                    if (models == null || models.Count == 0)
                        _fetchModelStatus = "❌ Failed to fetch models. Check your API key.";
                    else
                    {
                        _fetchedGeminiModels = models;
                        _fetchModelStatus = $"✅ Found {models.Count} models — click one to select, then confirm";
                    }
                });
            }

            if (!string.IsNullOrEmpty(_fetchModelStatus))
            {
                GUI.color = _fetchModelStatus.StartsWith("✅") ? Color.green :
                            _fetchModelStatus.StartsWith("❌") ? Color.red : Color.yellow;
                list.Label(_fetchModelStatus);
                GUI.color = Color.white;
            }

            // ── Lista de modelos ──
            if (_fetchedGeminiModels.Count > 0)
            {
                list.Gap();
                list.Label("Search:");
                _modelSearchText = list.TextEntry(_modelSearchText);

                var filtered = string.IsNullOrEmpty(_modelSearchText)
                    ? _fetchedGeminiModels
                    : _fetchedGeminiModels
                        .Where(m => m.Name.Contains(_modelSearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                list.Label($"{filtered.Count} models found — click to select:");

                float rowHeight = 30f;
                float listHeight = rowHeight * Math.Min(MAX_VISIBLE_GEMINI_MODELS, filtered.Count);
                Rect outerRect = list.GetRect(listHeight + 4f);
                Rect innerRect = new Rect(0, 0, outerRect.width - 20f, rowHeight * filtered.Count);

                Widgets.BeginScrollView(outerRect, ref _modelListScrollPos, innerRect);

                for (int i = 0; i < filtered.Count; i++)
                {
                    var model = filtered[i];
                    Rect rowRect = new Rect(0, i * rowHeight, innerRect.width, rowHeight - 2f);

                    bool isSaved   = model.Name == savedModel;
                    bool isPending = model.Name == _pendingModelSelection;

                    if (isSaved)
                        Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.35f, 0.1f, 0.7f));
                    else if (isPending)
                        Widgets.DrawBoxSolid(rowRect, new Color(0.35f, 0.28f, 0f, 0.7f));
                    else if (Mouse.IsOver(rowRect))
                        Widgets.DrawHighlight(rowRect);

                    // Badge visual (solo cosmético, no afecta selección)
                    string badge = model.IsAdvanced ? "[Pro] " : "[Flash]";
                    GUI.color = model.IsAdvanced ? new Color(1f, 0.7f, 0.3f) : new Color(0.5f, 0.85f, 1f);
                    Widgets.Label(new Rect(rowRect.x + 4, rowRect.y + 7, 58f, rowHeight), badge);
                    GUI.color = Color.white;

                    // Nombre del modelo
                    GUI.color = isSaved ? Color.green : isPending ? new Color(1f, 0.9f, 0.3f) : Color.white;
                    Widgets.Label(new Rect(rowRect.x + 65f, rowRect.y + 7, rowRect.width - 120f, rowHeight), model.Name);
                    GUI.color = Color.white;

                    // Indicador derecha
                    if (isSaved)
                    {
                        GUI.color = Color.green;
                        Widgets.Label(new Rect(rowRect.xMax - 52f, rowRect.y + 7, 50f, rowHeight), "✅ Active");
                        GUI.color = Color.white;
                    }
                    else if (isPending)
                    {
                        GUI.color = new Color(1f, 0.9f, 0.3f);
                        Widgets.Label(new Rect(rowRect.xMax - 58f, rowRect.y + 7, 56f, rowHeight), "⏳ Pending");
                        GUI.color = Color.white;
                    }

                    if (Widgets.ButtonInvisible(rowRect) && !isSaved)
                        _pendingModelSelection = model.Name;
                }

                Widgets.EndScrollView();

                list.Gap();
                if (list.ButtonText("Use Default (gemini-2.0-flash-001)"))
                {
                    Settings.selectedModel = "gemini-2.0-flash-001";
                    Settings.Write();
                    _pendingModelSelection = "";
                    _fetchModelStatus = "✅ Default model saved";
                }
            }
            else if (!_isFetchingModels)
            {
                list.Gap();
                GUI.color = Color.gray;
                list.Label("Click 'Fetch Available Models' to load the current model list from Gemini.");
                GUI.color = Color.white;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MODEL SELECTION HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve el nombre del modelo actualmente guardado.
        /// </summary>
        private string GetSavedModelName()
        {
            if (!string.IsNullOrEmpty(Settings.selectedModel))
                return Settings.selectedModel;

            return "gemini-2.0-flash-001 (default)";
        }

        /// <summary>
        /// Confirma la selección pendiente, guarda en selectedModel y persiste al disco.
        /// </summary>
        private void ConfirmModelSelection()
        {
            if (string.IsNullOrEmpty(_pendingModelSelection)) return;

            Settings.selectedModel = _pendingModelSelection;
            Settings.Write();

            _fetchModelStatus = $"✅ Model saved: {_pendingModelSelection}";
            _pendingModelSelection = "";

            Messages.Message($"EchoColony: Model set to {Settings.selectedModel}", MessageTypeDefOf.PositiveEvent);

            if (Settings.debugMode)
                Log.Message($"[EchoColony] Model confirmed and saved: {Settings.selectedModel}");
        }

        // ═══════════════════════════════════════════════════════════════
        // DEBUG TOOLS
        // ═══════════════════════════════════════════════════════════════

        private void DrawMemoryDebugTools(Listing_Standard list)
        {
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("Memory System:");
            GUI.color = Color.white;

            if (list.ButtonText("Check Memory State"))
                CheckMemorySystemState();

            if (list.ButtonText("Force Clean Memories"))
                ForceCleanAllMemories();
        }

        private void DrawActionsDebugTools(Listing_Standard list)
        {
            list.Gap();
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("Actions System:");
            GUI.color = Color.white;

            if (list.ButtonText("List Registered Actions"))
                ListRegisteredActions();
        }

        private void DrawSpontaneousMessagesDebugTools(Listing_Standard list)
        {
            list.Gap();
            GUI.color = new Color(1f, 1f, 0.7f);
            list.Label("Spontaneous Messages System:");
            GUI.color = Color.white;

            if (!Settings.IsSpontaneousMessagesActive())
            {
                GUI.color = Color.yellow;
                list.Label("⚠ System is DISABLED");
                GUI.color = Color.white;
            }

            if (list.ButtonText("🔍 Check System Status"))
                SpontaneousMessages.SpontaneousMessagesDebug.CheckSystemStatus();

            if (list.ButtonText("✉️ Force Test Message"))
                SpontaneousMessages.SpontaneousMessagesDebug.ForceTestMessage();

            if (list.ButtonText("⚔️ Simulate Test Raid"))
                SpontaneousMessages.SpontaneousMessagesDebug.SimulateIncident();

            if (list.ButtonText("🔄 Reset All Cooldowns"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all colonist message cooldowns? This will allow them to send messages immediately.",
                    () => SpontaneousMessages.SpontaneousMessagesDebug.ResetAllCooldowns()
                ));
            }

            if (list.ButtonText("📋 List Colonists Status"))
                SpontaneousMessages.SpontaneousMessagesDebug.ListColonistsStatus();

            list.Gap();
            GUI.color = Color.gray;
            list.Label("Use these tools to diagnose issues");
            GUI.color = Color.white;
        }

        // ═══════════════════════════════════════════════════════════════
        // SPONTANEOUS MESSAGES HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void ShowSpontaneousMessageModeMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (SpontaneousMessageMode mode in Enum.GetValues(typeof(SpontaneousMessageMode)))
            {
                string label = GetSpontaneousModeLabelTranslated(mode);
                string desc = GetSpontaneousModeDescriptionTranslated(mode);
                options.Add(new FloatMenuOption(label + " - " + desc, () =>
                {
                    Settings.spontaneousMessageMode = mode;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetSpontaneousModeLabelTranslated(SpontaneousMessageMode mode)
        {
            switch (mode)
            {
                case SpontaneousMessageMode.Disabled:    return "EchoColony.ModeDisabled".Translate();
                case SpontaneousMessageMode.RandomOnly:  return "EchoColony.ModeRandomOnly".Translate();
                case SpontaneousMessageMode.IncidentsOnly: return "EchoColony.ModeIncidentsOnly".Translate();
                case SpontaneousMessageMode.Full:        return "EchoColony.ModeFull".Translate();
                default:                                 return mode.ToString();
            }
        }

        private string GetSpontaneousModeDescriptionTranslated(SpontaneousMessageMode mode)
        {
            switch (mode)
            {
                case SpontaneousMessageMode.Disabled:    return "EchoColony.ModeDisabledDesc".Translate();
                case SpontaneousMessageMode.RandomOnly:  return "EchoColony.ModeRandomOnlyDesc".Translate();
                case SpontaneousMessageMode.IncidentsOnly: return "EchoColony.ModeIncidentsOnlyDesc".Translate();
                case SpontaneousMessageMode.Full:        return "EchoColony.ModeFullDesc".Translate();
                default:                                 return "";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STORYTELLER HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void ShowStorytellerMessageModeMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (StorytellerMessageMode mode in Enum.GetValues(typeof(StorytellerMessageMode)))
            {
                string label = GetStorytellerModeLabelTranslated(mode);
                string desc = GetStorytellerModeDescriptionTranslated(mode);
                options.Add(new FloatMenuOption(label + " - " + desc, () =>
                {
                    Settings.storytellerMessageMode = mode;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetStorytellerModeLabelTranslated(StorytellerMessageMode mode)
        {
            switch (mode)
            {
                case StorytellerMessageMode.Disabled:      return "Disabled";
                case StorytellerMessageMode.RandomOnly:    return "Random Only";
                case StorytellerMessageMode.IncidentsOnly: return "Incidents Only";
                case StorytellerMessageMode.Full:          return "Full (Random + Incidents)";
                default:                                   return mode.ToString();
            }
        }

        private string GetStorytellerModeDescriptionTranslated(StorytellerMessageMode mode)
        {
            switch (mode)
            {
                case StorytellerMessageMode.Disabled:      return "No messages";
                case StorytellerMessageMode.RandomOnly:    return "Random observations";
                case StorytellerMessageMode.IncidentsOnly: return "React to events";
                case StorytellerMessageMode.Full:          return "Both random and events";
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
                Messages.Message("EchoColony: Memory system enabled - future conversations will be remembered", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Message("[EchoColony] Memory system disabled by user");

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.DisableMemorySystemConfirm".Translate(),
                    () =>
                    {
                        var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
                        if (memoryManager != null)
                            memoryManager.ForceCleanMemories();
                        Messages.Message("EchoColony: Existing memories deleted", MessageTypeDefOf.TaskCompletion);
                    }));
            }
        }

        private void CheckMemorySystemState()
        {
            var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
            if (memoryManager == null)
            {
                Messages.Message("MemoryManager not available", MessageTypeDefOf.RejectInput);
                return;
            }

            memoryManager.DebugPrintMemoryState();
            bool integrity = memoryManager.ValidateMemoryIntegrity();
            string status = integrity ? "System working correctly" : "Problems detected";
            Messages.Message($"EchoColony: {status}", integrity ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput);
        }

        private void ForceCleanAllMemories()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.ForceCleanMemoriesConfirm".Translate(),
                () =>
                {
                    var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
                    if (memoryManager != null)
                        memoryManager.ForceCleanMemories();
                    else
                        Messages.Message("MemoryManager not available", MessageTypeDefOf.RejectInput);
                }));
        }

        private void ListRegisteredActions()
        {
            Actions.ActionRegistry.Initialize();
            var actions = Actions.ActionRegistry.GetAllActions();
            var categorized = actions.GroupBy(a => a.Category).OrderBy(g => g.Key);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[EchoColony] Registered Actions ({actions.Count} total):");

            foreach (var group in categorized)
            {
                sb.AppendLine($"  {group.Key}: {group.Count()} actions");
                foreach (var action in group)
                    sb.AppendLine($"    - {action.ActionId}");
            }

            Log.Message(sb.ToString());
            Messages.Message($"Listed {actions.Count} actions in log", MessageTypeDefOf.TaskCompletion);
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 HELPERS
        // ═══════════════════════════════════════════════════════════════

        public static void CheckPlayer2AvailableAndWarn()
        {
            if (pingInProgress) return;

            pingInProgress = true;

            var request = UnityWebRequest.Get("http://127.0.0.1:4315/v1/health");
            request.timeout = 2;

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
                        "Player2 is not running. Download it for free from https://player2.game/",
                        MessageTypeDefOf.RejectInput,
                        false
                    );
                    return;
                }

                if (!request.downloadHandler.text.Contains("client_version"))
                {
                    Messages.Message(
                        "Player2 responded, but in an unexpected format. Try restarting the app or reinstalling.",
                        MessageTypeDefOf.RejectInput,
                        false
                    );
                }
            };
        }
    }
}