using UnityEngine;
using Verse;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using RimWorld;
using System.Linq;

namespace EchoColony
{
    public class MyMod : Mod
    {
        public static GeminiSettings Settings;
        private Vector2 scrollPos = Vector2.zero;
        private Vector2 settingsScrollPosition = Vector2.zero;

        private ModelSource previousModelSource = ModelSource.Gemini;

        private static bool pingInProgress = false;

        public MyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<GeminiSettings>();
            
            if (Settings.modelPreferences == null)
                Settings.modelPreferences = new GeminiModelPreferences();
        }

        public override string SettingsCategory() => "EchoColony";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Calculate total height needed for all settings
            float totalHeight = 2000f; // Adjust this if you add more settings
            
            // Create scroll view
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, totalHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            
            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            // ===== BASIC CONFIGURATION =====
            GUI.color = new Color(0.7f, 0.9f, 1f);
            list.Label("═══ Basic Settings ═══");
            GUI.color = Color.white;
            
            list.CheckboxLabeled("EchoColony.EnableSocialAffectsPersonality".Translate(), ref Settings.enableSocialAffectsPersonality);
            list.CheckboxLabeled("EchoColony.EnableRoleplayResponses".Translate(), ref Settings.enableRoleplayResponses);
            
            list.CheckboxLabeled(
                "EchoColony.IgnoreDangers".Translate(), 
                ref Settings.ignoreDangersInConversations,
                "EchoColony.IgnoreDangersTooltip".Translate()
            );
            
            list.GapLine();

            // ===== MEMORY SYSTEM =====
            GUI.color = new Color(0.7f, 0.9f, 1f);
            list.Label("═══ Memory System ═══");
            GUI.color = Color.white;
            
            bool oldMemorySystemState = Settings.enableMemorySystem;
            list.CheckboxLabeled(
                "EchoColony.EnableMemorySystem".Translate(), 
                ref Settings.enableMemorySystem,
                "EchoColony.EnableMemorySystemTooltip".Translate()
            );

            if (oldMemorySystemState != Settings.enableMemorySystem)
            {
                OnMemorySystemToggled(Settings.enableMemorySystem);
            }

            if (Settings.enableMemorySystem)
            {
                GUI.color = Color.green;
                list.Label("  ✓ Memory system: Enabled");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                list.Label("  ✗ Memory system: Disabled");
                GUI.color = Color.white;
            }

            list.GapLine();

            // ===== UI SETTINGS =====
            GUI.color = new Color(0.7f, 0.9f, 1f);
            list.Label("═══ UI Settings ═══");
            GUI.color = Color.white;
            
            list.CheckboxLabeled(
                "Show Storyteller Chat Button", 
                ref Settings.enableStorytellerButton,
                "Shows or hides the Storyteller chat button in the main menu bar"
            );

            list.GapLine();

            // ===== DIVINE ACTIONS SYSTEM =====
            GUI.color = new Color(1f, 0.8f, 0.4f);
            list.Label("═══ Divine Actions System ═══");
            GUI.color = Color.white;
            
            list.CheckboxLabeled(
                "Enable Divine Actions (AI can affect colonists)", 
                ref Settings.enableDivineActions,
                "Allows the AI to use actions like healing, mood changes, etc. during conversations"
            );

            if (Settings.enableDivineActions)
            {
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

            // ===== GLOBAL PROMPT =====
            GUI.color = new Color(0.7f, 0.9f, 1f);
            list.Label("═══ Global Prompt ═══");
            GUI.color = Color.white;
            
            list.Label("EchoColony.GlobalPrompt".Translate());
            float areaHeight = 80f;
            Rect scrollOut = list.GetRect(areaHeight);
            Rect scrollView = new Rect(0, 0, scrollOut.width - 16f, areaHeight * 2);
            Widgets.BeginScrollView(scrollOut, ref scrollPos, scrollView);
            Settings.globalPrompt = Widgets.TextArea(scrollView, Settings.globalPrompt);
            Widgets.EndScrollView();
            
            list.GapLine();

            // ===== MODEL SOURCE SELECTION =====
            GUI.color = new Color(0.6f, 1f, 0.6f);
            list.Label("═══ AI Model Configuration ═══");
            GUI.color = Color.white;

            // Player2 Toggle
            bool isPlayer2 = Settings.modelSource == ModelSource.Player2;
            bool checkboxState = isPlayer2;

            list.CheckboxLabeled("EchoColony.UsePlayer2Label".Translate(), ref checkboxState, "EchoColony.UsePlayer2Tooltip".Translate());

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
            {
                list.Gap();
                list.Label("EchoColony.Player2Warning".Translate());
                list.CheckboxLabeled("EchoColony.EnableTTS".Translate(), ref Settings.enableTTS);
                if (Settings.enableTTS)
                {
                    list.CheckboxLabeled("EchoColony.AutoPlayVoice".Translate(), ref Settings.autoPlayVoice);
                }
            }
            else
            {
                list.Gap();
                list.Label("EchoColony.ModelSource".Translate());

                if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseGemini".Translate(), Settings.modelSource == ModelSource.Gemini))
                {
                    Settings.modelSource = ModelSource.Gemini;
                }
                if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseLocal".Translate(), Settings.modelSource == ModelSource.Local))
                {
                    Settings.modelSource = ModelSource.Local;
                }
                if (Widgets.RadioButtonLabeled(list.GetRect(25f), "EchoColony.UseOpenRouter".Translate(), Settings.modelSource == ModelSource.OpenRouter))
                {
                    Settings.modelSource = ModelSource.OpenRouter;
                }

                list.Gap();

                // Model-specific configuration
                if (Settings.modelSource == ModelSource.Local)
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
                else if (Settings.modelSource == ModelSource.OpenRouter)
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
                else // Gemini
                {
                    DrawGeminiSettings(list);
                }
            }

            list.GapLine();

            // ===== GENERAL SETTINGS =====
            GUI.color = new Color(0.7f, 0.9f, 1f);
            list.Label("═══ General Settings ═══");
            GUI.color = Color.white;

            if (Settings.modelSource != ModelSource.Player2)
            {
                list.Label("EchoColony.MaxResponseLength".Translate(Settings.maxResponseLength));
                Settings.maxResponseLength = (int)list.Slider(Settings.maxResponseLength, 50, 1000);
            }

            list.CheckboxLabeled("EchoColony.DebugModeLabel".Translate(), ref Settings.debugMode, "EchoColony.DebugModeTooltip".Translate());

            // ===== DEBUG TOOLS =====
            if (Settings.debugMode)
            {
                list.GapLine();
                GUI.color = Color.cyan;
                list.Label("═══ Debug Tools ═══");
                GUI.color = Color.white;

                // Memory debug tools
                GUI.color = new Color(1f, 1f, 0.7f);
                list.Label("Memory System:");
                GUI.color = Color.white;
                
                if (list.ButtonText("Check Memory State"))
                {
                    CheckMemorySystemState();
                }

                if (list.ButtonText("Force Clean Memories"))
                {
                    ForceCleanAllMemories();
                }
                
                // Actions debug tools
                if (Settings.enableDivineActions)
                {
                    list.Gap();
                    GUI.color = new Color(1f, 1f, 0.7f);
                    list.Label("Actions System:");
                    GUI.color = Color.white;

                    if (list.ButtonText("List Registered Actions"))
                    {
                        ListRegisteredActions();
                    }
                }
            }

            list.End();
            Widgets.EndScrollView();
        }

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
                    () => {
                        var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
                        if (memoryManager != null)
                        {
                            memoryManager.ForceCleanMemories();
                        }
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
                () => {
                    var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
                    if (memoryManager != null)
                    {
                        memoryManager.ForceCleanMemories();
                    }
                    else
                    {
                        Messages.Message("MemoryManager not available", MessageTypeDefOf.RejectInput);
                    }
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
                {
                    sb.AppendLine($"    - {action.ActionId}");
                }
            }
            
            Log.Message(sb.ToString());
            Messages.Message($"Listed {actions.Count} actions in log", MessageTypeDefOf.TaskCompletion);
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

            GUI.color = Color.cyan;
            list.Label("Model Configuration:");
            GUI.color = Color.white;

            string currentModel = GetCurrentModelInUse();
            list.Label($"Current model: {currentModel}");

            list.Gap();

            if (list.ButtonText("Choose Specific Model"))
            {
                ShowSimpleModelSelectionMenu();
            }

            list.Gap();
            GUI.color = Color.gray;
            list.Label("• Flash models: Faster, cheaper");
            list.Label("• Pro models: Better quality, more expensive");
            list.Label($"• Available: 5 Flash, 3 Pro models");
            GUI.color = Color.white;
        }

        private void ShowSimpleModelSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("Automatic (gemini-2.0-flash-001)", () =>
            {
                Settings.modelPreferences.useAutoSelection = true;
                Settings.modelPreferences.preferredFastModel = "";
                Settings.modelPreferences.preferredAdvancedModel = "";
            }));

            options.Add(new FloatMenuOption("──────────────", null) { Disabled = true });

            options.Add(new FloatMenuOption("gemini-2.5-flash", () =>
            {
                SetSpecificModel("gemini-2.5-flash", false);
            }));

            options.Add(new FloatMenuOption("gemini-2.5-flash-lite", () =>
            {
                SetSpecificModel("gemini-2.5-flash-lite", false);
            }));

            options.Add(new FloatMenuOption("gemini-2.5-flash-preview-09-2025", () =>
            {
                SetSpecificModel("gemini-2.5-flash-preview-09-2025", false);
            }));

            options.Add(new FloatMenuOption("gemini-2.0-flash-001 (Recommended)", () =>
            {
                SetSpecificModel("gemini-2.0-flash-001", false);
            }));

            options.Add(new FloatMenuOption("gemini-2.0-flash-lite-001", () =>
            {
                SetSpecificModel("gemini-2.0-flash-lite-001", false);
            }));

            options.Add(new FloatMenuOption("gemini-2.5-pro", () =>
            {
                SetSpecificModel("gemini-2.5-pro", true);
            }));

            options.Add(new FloatMenuOption("gemini-2.0-flash-thinking-exp", () =>
            {
                SetSpecificModel("gemini-2.0-flash-thinking-exp", true);
            }));

            options.Add(new FloatMenuOption("gemini-2.0-pro-exp", () =>
            {
                SetSpecificModel("gemini-2.0-pro-exp", true);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void SetSpecificModel(string modelName, bool isAdvanced)
        {
            Settings.modelPreferences.useAutoSelection = false;
            
            if (isAdvanced)
            {
                Settings.modelPreferences.preferredAdvancedModel = modelName;
                Settings.modelPreferences.preferredFastModel = "";
                Settings.useAdvancedModel = true;
            }
            else
            {
                Settings.modelPreferences.preferredFastModel = modelName;
                Settings.modelPreferences.preferredAdvancedModel = "";
                Settings.useAdvancedModel = false;
            }
        }

        private string GetCurrentModelInUse()
        {
            try
            {
                if (Settings.modelPreferences.useAutoSelection)
                {
                    string autoModel = GeminiAPI.GetBestAvailableModel(Settings.ShouldUseAdvancedModel());
                    string type = Settings.ShouldUseAdvancedModel() ? "Pro" : "Flash";
                    return $"{autoModel} (Auto {type})";
                }
                else
                {
                    string manualModel = Settings.ShouldUseAdvancedModel() ? 
                        Settings.modelPreferences.preferredAdvancedModel : 
                        Settings.modelPreferences.preferredFastModel;
                    
                    if (!string.IsNullOrEmpty(manualModel))
                    {
                        string shortName = manualModel.Replace("gemini-", "").Replace("-preview-09-2025", "-preview");
                        return $"{shortName} (Manual)";
                    }
                    else
                        return "None selected";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

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

                string result = request.downloadHandler.text;
                if (!result.Contains("client_version"))
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