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

        // Guardar el modelo anterior para restaurarlo al desmarcar Player2
        private ModelSource previousModelSource = ModelSource.Gemini;

        private static bool pingInProgress = false;

        public MyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<GeminiSettings>();
            
            // Asegurar que modelPreferences existe
            if (Settings.modelPreferences == null)
                Settings.modelPreferences = new GeminiModelPreferences();
        }

        public override string SettingsCategory() => "EchoColony";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            // Configuración básica (siempre visible)
            list.CheckboxLabeled("EchoColony.EnableSocialAffectsPersonality".Translate(), ref Settings.enableSocialAffectsPersonality);
            list.CheckboxLabeled("EchoColony.EnableRoleplayResponses".Translate(), ref Settings.enableRoleplayResponses);
            
            list.CheckboxLabeled(
                "EchoColony.IgnoreDangers".Translate(), 
                ref Settings.ignoreDangersInConversations,
                "EchoColony.IgnoreDangersTooltip".Translate()
            );
            
            // ✅ NUEVO: Toggle para sistema de memorias
            bool oldMemorySystemState = Settings.enableMemorySystem;
            list.CheckboxLabeled(
                "EchoColony.EnableMemorySystem".Translate(), 
                ref Settings.enableMemorySystem,
                "EchoColony.EnableMemorySystemTooltip".Translate()
            );

            // ✅ NUEVO: Verificar si cambió el estado y aplicar cambios
            if (oldMemorySystemState != Settings.enableMemorySystem)
            {
                OnMemorySystemToggled(Settings.enableMemorySystem);
            }

            // ✅ NUEVO: Información sobre el estado del sistema de memorias
            if (Settings.enableMemorySystem)
            {
                GUI.color = Color.green;
                list.Label("💾 " + "EchoColony.MemorySystemEnabled".Translate());
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                list.Label("🚫 " + "EchoColony.MemorySystemDisabled".Translate());
                GUI.color = Color.white;
            }

            list.GapLine();

            // Global Prompt - siempre visible en la parte superior
            list.Label("EchoColony.GlobalPrompt".Translate());
            float areaHeight = 80f;
            Rect scrollOut = list.GetRect(areaHeight);
            Rect scrollView = new Rect(0, 0, scrollOut.width - 16f, areaHeight * 2);
            Widgets.BeginScrollView(scrollOut, ref scrollPos, scrollView);
            Settings.globalPrompt = Widgets.TextArea(scrollView, Settings.globalPrompt);
            Widgets.EndScrollView();
            
            list.GapLine();

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
            list.GapLine();

            if (Settings.modelSource == ModelSource.Player2)
            {
                list.Label("EchoColony.Player2Warning".Translate());
                list.CheckboxLabeled("EchoColony.EnableTTS".Translate(), ref Settings.enableTTS);
                if (Settings.enableTTS)
                {
                    list.CheckboxLabeled("EchoColony.AutoPlayVoice".Translate(), ref Settings.autoPlayVoice);
                }
            }
            else
            {
                // Selección de fuente del modelo
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

                // Configuración específica según modelo
                if (Settings.modelSource == ModelSource.Local)
                {
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

            // Controles finales (compactos)
            if (Settings.modelSource != ModelSource.Player2)
            {
                list.Label("EchoColony.MaxResponseLength".Translate(Settings.maxResponseLength));
                Settings.maxResponseLength = (int)list.Slider(Settings.maxResponseLength, 50, 1000);
            }

            list.CheckboxLabeled("EchoColony.DebugModeLabel".Translate(), ref Settings.debugMode, "EchoColony.DebugModeTooltip".Translate());

            // ✅ NUEVO: Botones de debug para memorias (solo en modo debug)
            if (Settings.debugMode)
            {
                list.GapLine();
                GUI.color = Color.cyan;
                list.Label("🔧 " + "EchoColony.MemoryDebugTools".Translate());
                GUI.color = Color.white;

                if (list.ButtonText("🔍 " + "EchoColony.CheckMemoryState".Translate()))
                {
                    CheckMemorySystemState();
                }

                if (list.ButtonText("🗑️ " + "EchoColony.ForceCleanMemories".Translate()))
                {
                    ForceCleanAllMemories();
                }
            }

            list.End();
        }

        // ✅ NUEVO: Maneja el cambio en el toggle de memorias
        private void OnMemorySystemToggled(bool newState)
        {
            if (newState)
            {
                Log.Message("[EchoColony] 💾 Sistema de memorias habilitado por usuario");
                Messages.Message("EchoColony: Sistema de memorias habilitado - las conversaciones futuras se recordarán", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Message("[EchoColony] 🚫 Sistema de memorias deshabilitado por usuario");
                
                // Preguntar si quiere limpiar memorias existentes
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EchoColony.DisableMemorySystemConfirm".Translate(),
                    () => {
                        // Limpiar memorias existentes
                        var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
                        if (memoryManager != null)
                        {
                            memoryManager.ForceCleanMemories();
                        }
                        Messages.Message("EchoColony: Memorias existentes eliminadas", MessageTypeDefOf.TaskCompletion);
                    }));
            }
        }

        // ✅ NUEVO: Método de debug para verificar estado del sistema de memorias
        private void CheckMemorySystemState()
        {
            var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
            if (memoryManager == null)
            {
                Messages.Message("❌ MemoryManager no disponible", MessageTypeDefOf.RejectInput);
                return;
            }

            memoryManager.DebugPrintMemoryState();
            bool integrity = memoryManager.ValidateMemoryIntegrity();
            
            string status = integrity ? "✅ Sistema funcionando correctamente" : "⚠️ Problemas detectados";
            Messages.Message($"EchoColony: {status}", integrity ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.CautionInput);
        }

        // ✅ NUEVO: Método de debug para limpiar todas las memorias
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
                        Messages.Message("❌ MemoryManager no disponible", MessageTypeDefOf.RejectInput);
                    }
                }));
        }

        // ✅ SIMPLIFICADO: Configuración directa de modelos Gemini (sin refresh)
        private void DrawGeminiSettings(Listing_Standard list)
        {
            // API Key
            list.Label("EchoColony.GeminiAPIKey".Translate());
            Settings.apiKey = list.TextEntry(Settings.apiKey);

            if (string.IsNullOrEmpty(Settings.apiKey))
            {
                GUI.color = Color.yellow;
                list.Label("⚠️ Enter your Gemini API key to configure models");
                GUI.color = Color.white;
                return;
            }

            list.Gap();

            // Configuración directa y simple
            GUI.color = Color.cyan;
            list.Label("🎯 Model Configuration:");
            GUI.color = Color.white;

            // Mostrar modelo actual
            string currentModel = GetCurrentModelInUse();
            list.Label($"Current model: {currentModel}");

            list.Gap();

            // Botón principal para elegir modelo
            if (list.ButtonText("📋 Choose Specific Model"))
            {
                ShowSimpleModelSelectionMenu();
            }

            // Información útil
            GUI.color = Color.gray;
            list.Label("💡 Flash models: Faster, cheaper");
            list.Label("💡 Pro models: Better quality, more expensive");
            list.Label($"💎 Available: 5 Flash, 3 Pro models");
            GUI.color = Color.white;
        }

        // ✅ FINAL: Menú organizado con los 8 modelos más relevantes (hardcodeado)
        private void ShowSimpleModelSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Opción automática
            options.Add(new FloatMenuOption("🤖 Automatic (gemini-2.0-flash-001)", () =>
            {
                Settings.modelPreferences.useAutoSelection = true;
                Settings.modelPreferences.preferredFastModel = "";
                Settings.modelPreferences.preferredAdvancedModel = "";
            }));

            options.Add(new FloatMenuOption("──────────────", null) { Disabled = true });

            // Los 8 modelos directos
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

            options.Add(new FloatMenuOption("gemini-2.0-flash-001 ⭐", () =>
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
        
        // Método para configurar un modelo específico
        private void SetSpecificModel(string modelName, bool isAdvanced)
        {
            Settings.modelPreferences.useAutoSelection = false;
            
            if (isAdvanced)
            {
                Settings.modelPreferences.preferredAdvancedModel = modelName;
                Settings.modelPreferences.preferredFastModel = ""; // Limpiar el otro
                Settings.useAdvancedModel = true;
            }
            else
            {
                Settings.modelPreferences.preferredFastModel = modelName;
                Settings.modelPreferences.preferredAdvancedModel = ""; // Limpiar el otro
                Settings.useAdvancedModel = false;
            }
        }

        // Método para mostrar el modelo actual
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
                        // Mostrar versión acortada para que sea legible
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
                        "⚠️ Player2 is not running. Download it for free from https://player2.game/",
                        MessageTypeDefOf.RejectInput,
                        false
                    );
                    return;
                }

                string result = request.downloadHandler.text;
                if (!result.Contains("client_version"))
                {
                    Messages.Message(
                        "⚠️ Player2 responded, but in an unexpected format. Try restarting the app or reinstalling.",
                        MessageTypeDefOf.RejectInput,
                        false
                    );
                }
            };
        }
    }
}