using UnityEngine;
using Verse;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using RimWorld;

namespace EchoColony
{
    public class MyMod : Mod
    {
        public static GeminiSettings Settings;
        private Vector2 scrollPos = Vector2.zero;

        // 🧠 Guardar el modelo anterior para restaurarlo al desmarcar Player2
        private ModelSource previousModelSource = ModelSource.Gemini;

        private static bool pingInProgress = false;

        public MyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<GeminiSettings>();
        }

        public override string SettingsCategory() => "EchoColony";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.CheckboxLabeled("EchoColony.EnableSocialAffectsPersonality".Translate(), ref Settings.enableSocialAffectsPersonality);
            list.CheckboxLabeled("EchoColony.EnableRoleplayResponses".Translate(), ref Settings.enableRoleplayResponses);
            
            // 🎯 NUEVA OPCIÓN: Ignorar peligros en conversaciones
            list.CheckboxLabeled(
                "EchoColony.IgnoreDangers".Translate(), 
                ref Settings.ignoreDangersInConversations,
                "EchoColony.IgnoreDangersTooltip".Translate()
            );
            
            list.GapLine();

            // Player2 Toggle (visual) - reflejo directo del modelSource
            bool isPlayer2 = Settings.modelSource == ModelSource.Player2;
            bool checkboxState = isPlayer2;

            list.CheckboxLabeled("EchoColony.UsePlayer2Label".Translate(), ref checkboxState, "EchoColony.UsePlayer2Tooltip".Translate());

            if (checkboxState != isPlayer2)
            {
                if (checkboxState)
                {
                    previousModelSource = Settings.modelSource;
                    Settings.modelSource = ModelSource.Player2;

                    // Verificar si Player2 está activo
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
                    list.Label("EchoColony.GeminiAPIKey".Translate());
                    Settings.apiKey = list.TextEntry(Settings.apiKey);

                    list.Label("EchoColony.Model".Translate());
                    Rect modelRect = list.GetRect(25f);
                    TooltipHandler.TipRegion(modelRect, "EchoColony.GeminiTooltip".Translate());
                    if (Widgets.RadioButtonLabeled(modelRect, "gemini flash", !Settings.useAdvancedModel))
                        Settings.useAdvancedModel = false;
                    if (Widgets.RadioButtonLabeled(list.GetRect(25f), "gemini pro", Settings.useAdvancedModel))
                        Settings.useAdvancedModel = true;
                }
            }

            list.GapLine();

            list.Label("EchoColony.GlobalPrompt".Translate());
            float areaHeight = 100f;
            Rect scrollOut = list.GetRect(areaHeight);
            Rect scrollView = new Rect(0, 0, scrollOut.width - 16f, areaHeight * 2);
            Widgets.BeginScrollView(scrollOut, ref scrollPos, scrollView);
            Settings.globalPrompt = Widgets.TextArea(scrollView, Settings.globalPrompt);
            Widgets.EndScrollView();

            list.Gap();
            if (Settings.modelSource != ModelSource.Player2)
            {
                list.Label("EchoColony.MaxResponseLength".Translate(Settings.maxResponseLength));
                Settings.maxResponseLength = (int)list.Slider(Settings.maxResponseLength, 50, 1000);
            }

            list.CheckboxLabeled("EchoColony.DebugModeLabel".Translate(), ref Settings.debugMode, "EchoColony.DebugModeTooltip".Translate());

            list.End();
        }

        public static void CheckPlayer2AvailableAndWarn()
        {
            if (pingInProgress) return; // Evita múltiples llamadas simultáneas

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