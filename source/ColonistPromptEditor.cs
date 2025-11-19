using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace EchoColony
{
    public class ColonistPromptEditor : Window
    {
        private Pawn pawn;
        private string tempPrompt;
        private Vector2 scroll;
        private bool hasFocused = false;
        private string testText = "Hello, this is how I sound!";

        public ColonistPromptEditor(Pawn pawn)
        {
            this.pawn = pawn;
            this.tempPrompt = ColonistPromptManager.GetPrompt(pawn);
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.draggable = true;

            // 🧠 Restaurar voz guardada solo si Player2 está activo y TTS habilitado
            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string savedVoice = ColonistVoiceManager.GetVoice(pawn);
                if (!string.IsNullOrEmpty(savedVoice) &&
                    string.IsNullOrEmpty(ChatGameComponent.Instance.GetVoiceForPawn(pawn)))
                {
                    ChatGameComponent.Instance.SetVoiceForPawn(pawn, savedVoice);
                    Log.Message($"[EchoColony] Loaded stored voice '{savedVoice}' for {pawn.LabelShort} into memory.");
                }
            }
    
            // Solo cargar caracteres de Player2 si está activo
            if (MyMod.Settings.modelSource == ModelSource.Player2 && Player2CharacterCache.Characters.Count == 0)
            {
                MyStoryModComponent.Instance.StartCoroutine(Player2Importer.LoadCharacters());
            }
        }

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            float currentY = 0f;

            // ✅ HEADER CON TÍTULO Y BOTÓN DE MEMORIAS (SIN CHECKBOX)
            DrawHeader(inRect, ref currentY);

            // Selector de voz (solo si Player2 está activo Y TTS está habilitado)
            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                DrawVoiceSection(inRect, ref currentY);
            }

            // Sección de prompt personalizado (CON CHECKBOX)
            DrawPromptSection(inRect, ref currentY);

            // Botón de guardar
            DrawSaveButton(inRect);
        }

        private void DrawHeader(Rect inRect, ref float currentY)
        {
            // ✅ ÁREA DEL HEADER LIMPIA
            Rect headerRect = new Rect(0, currentY, inRect.width, 35f);

            // Título principal
            Text.Font = GameFont.Medium;
            string title = "EchoColony.PersonalizingTitle".Translate(pawn.LabelCap);
            Widgets.Label(new Rect(0, currentY, inRect.width - 100f, 30f), title);
            Text.Font = GameFont.Small;

            // ✅ BOTÓN DE MEMORIAS MEJORADO (esquina superior derecha)
            Rect memoryButtonRect = new Rect(inRect.width - 90f, currentY, 85f, 30f);

            // Fondo sutil para el botón
            Color originalColor = GUI.color;
            GUI.color = new Color(0.8f, 0.9f, 1f, 0.8f); // Azul claro sutil

            if (Widgets.ButtonText(memoryButtonRect, "EchoColony.MemoriesButton".Translate()))
            {
                Find.WindowStack.Add(new ColonistMemoryViewer(pawn));
            }

            GUI.color = originalColor;

            // Tooltip informativo
            TooltipHandler.TipRegion(memoryButtonRect,
                "EchoColony.MemoriesTooltip".Translate(pawn.LabelShort));

            currentY += 40f; // Espacio después del header
        }

        private void DrawVoiceSection(Rect inRect, ref float currentY)
        {
            // ✅ SECCIÓN DE VOZ CON MEJOR ORGANIZACIÓN
            
            // Título de la sección con línea separadora
            Rect voiceTitleRect = new Rect(0, currentY, inRect.width, 25f);
            Widgets.Label(voiceTitleRect, "EchoColony.VoiceConfigurationTitle".Translate());
            currentY += 30f;

            // Línea separadora sutil
            Rect separatorRect = new Rect(0, currentY - 5f, inRect.width, 1f);
            Widgets.DrawBoxSolid(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            // Selector de voz
            Rect voiceLabelRect = new Rect(0f, currentY, 50f, 30f);
            Widgets.Label(voiceLabelRect, "EchoColony.VoiceLabel".Translate());

            Rect dropdownRect = new Rect(60f, currentY, 150f, 30f);
            if (Widgets.ButtonText(dropdownRect, GetSelectedVoiceName(pawn)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var voice in TTSVoiceCache.Voices)
                {
                    string voiceId = voice.id;
                    string displayName = $"{voice.name} [{voice.language}]";
                    options.Add(new FloatMenuOption(displayName, () =>
                    {
                        ChatGameComponent.Instance.SetVoiceForPawn(pawn, voiceId);
                        ColonistVoiceManager.SetVoice(pawn, voiceId);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Botón de importar de Player2
            Rect importBtnRect = new Rect(220f, currentY, 100f, 30f);
            GUI.color = new Color(0.9f, 1f, 0.9f); // Verde claro
            if (Widgets.ButtonText(importBtnRect, "EchoColony.ImportButton".Translate()))
            {
                HandlePlayer2Import();
            }
            GUI.color = Color.white;

            // Botón de prueba de voz
            Rect testVoiceRect = new Rect(330f, currentY, 70f, 30f);
            GUI.color = new Color(1f, 0.9f, 0.8f); // Naranja claro
            TooltipHandler.TipRegion(testVoiceRect, "EchoColony.TestVoiceTooltip".Translate());
            if (Widgets.ButtonText(testVoiceRect, "EchoColony.TestButton".Translate()))
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (!string.IsNullOrEmpty(voiceId) && !string.IsNullOrEmpty(testText))
                {
                    MyStoryModComponent.Instance.StartCoroutine(
                        TTSManager.Speak(testText, voiceId, "female", "en_US", 1f)
                    );
                }
            }
            GUI.color = Color.white;

            currentY += 35f;

            // Campo de texto para prueba de voz
            Rect testTextLabelRect = new Rect(0f, currentY, 120f, 20f);
            Widgets.Label(testTextLabelRect, "EchoColony.TestTextLabel".Translate());

            Rect testTextRect = new Rect(0f, currentY + 20f, inRect.width, 30f);
            GUI.SetNextControlName("TestTextArea");
            testText = Widgets.TextField(testTextRect, testText);

            currentY += 65f; // Espacio después de la sección de voz
        }

        private void DrawPromptSection(Rect inRect, ref float currentY)
        {
            // ✅ TÍTULO DE SECCIÓN CON LÍNEA SEPARADORA
            Rect promptTitleRect = new Rect(0, currentY, inRect.width, 25f);
            Widgets.Label(promptTitleRect, "EchoColony.CustomPromptTitle".Translate());
            currentY += 30f;

            // Línea separadora sutil
            Rect separatorRect = new Rect(0, currentY - 5f, inRect.width, 1f);
            Widgets.DrawBoxSolid(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            // ✅ CHECKBOX MOVIDO AQUÍ - mejor ubicación contextual
            Rect ageToggleLabel = new Rect(0f, currentY, 250f, 25f);
            Widgets.Label(ageToggleLabel, "EchoColony.IgnoreAgeLabel".Translate());

            Rect ageToggle = new Rect(260f, currentY + 2f, 24f, 24f);
            bool currentValue = ColonistPromptManager.GetIgnoreAge(pawn);
            bool tempValue = currentValue;
            Widgets.Checkbox(ageToggle.position, ref tempValue);
            if (tempValue != currentValue)
            {
                ColonistPromptManager.SetIgnoreAge(pawn, tempValue);
            }
            TooltipHandler.TipRegion(new Rect(0f, currentY, 300f, 25f), "EchoColony.IgnoreAgeTooltip".Translate());

            currentY += 35f; // Espacio después del checkbox

            // ✅ ÁREA DE TEXTO CORREGIDA CON SCROLL FUNCIONAL
            float buttonHeight = 30f;
            float padding = 10f;
            float textAreaTop = currentY;
            float textHeight = inRect.height - textAreaTop - buttonHeight - padding;

            Rect scrollOuterRect = new Rect(0, textAreaTop, inRect.width, textHeight);

            // Fondo negro y borde blanco
            Widgets.DrawBoxSolid(scrollOuterRect, Color.black);
            Widgets.DrawBox(scrollOuterRect);

            Rect scrollViewRect = scrollOuterRect.ContractedBy(4f);

            // ✅ CALCULAR ALTURA CORRECTA DEL CONTENIDO
            float textWidth = scrollViewRect.width - 16f; // Espacio para scrollbar
            
            // Usar una altura mínima que permita scroll
            float minHeight = scrollViewRect.height;
            
            // Calcular altura real del texto
            GUIStyle tempStyle = new GUIStyle(GUI.skin.textArea);
            tempStyle.wordWrap = true;
            tempStyle.fontSize = 14;
            tempStyle.padding = new RectOffset(6, 6, 6, 6);
            
            float textContentHeight = tempStyle.CalcHeight(new GUIContent(tempPrompt), textWidth);
            
            // Asegurar que siempre haya scroll disponible
            float contentHeight = Mathf.Max(textContentHeight + 40f, minHeight + 50f);

            Rect viewRect = new Rect(0f, 0f, textWidth, contentHeight);

            Widgets.BeginScrollView(scrollViewRect, ref scroll, viewRect);

            // ✅ TEXTAREA CON TAMAÑO CORRECTO
            Rect textAreaRect = new Rect(0f, 0f, textWidth, contentHeight - 20f);
            GUI.SetNextControlName("PromptTextArea");

            GUIStyle style = new GUIStyle(GUI.skin.textArea);
            style.wordWrap = true;
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(6, 6, 6, 6);
            style.fontSize = 14;

            tempPrompt = GUI.TextArea(textAreaRect, tempPrompt, style);

            Widgets.EndScrollView();

            // Enfocar el área de texto al abrir la ventana
            if (!hasFocused && Event.current.type == EventType.Layout)
            {
                GUI.FocusControl("PromptTextArea");
                hasFocused = true;
            }
        }

        private void DrawSaveButton(Rect inRect)
        {
            // ✅ BOTÓN DE GUARDAR CON MEJOR ESTILO
            float buttonHeight = 30f;
            Rect saveButtonRect = new Rect(inRect.width - 100f, inRect.height - buttonHeight, 100f, buttonHeight);
            
            GUI.color = new Color(0.8f, 1f, 0.8f); // Verde claro
            if (Widgets.ButtonText(saveButtonRect, "EchoColony.SaveButton".Translate()))
            {
                ColonistPromptManager.SetPrompt(pawn, tempPrompt);
                Messages.Message("EchoColony.ConfigurationSaved".Translate(pawn.LabelShort), MessageTypeDefOf.TaskCompletion);
                Close();
            }
            GUI.color = Color.white;
        }

        private void HandlePlayer2Import()
        {
            if (Player2CharacterCache.Characters.Count == 0)
            {
                // No hay personajes - mostrar advertencia
                Messages.Message("EchoColony.NoCharactersFound".Translate(),
                                MessageTypeDefOf.RejectInput, false);
            }
            else if (Player2CharacterCache.Characters.Count == 1)
            {
                // Solo un personaje - importar automáticamente
                var character = Player2CharacterCache.Characters[0];

                // Aplicar descripción y voz
                tempPrompt = character.description;
                if (!string.IsNullOrEmpty(character.voice_id))
                {
                    ChatGameComponent.Instance.SetVoiceForPawn(pawn, character.voice_id);
                    ColonistVoiceManager.SetVoice(pawn, character.voice_id);
                }

                Messages.Message("EchoColony.CharacterImportedAuto".Translate(character.short_name),
                                MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                // Múltiples personajes - mostrar menú de selección
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var character in Player2CharacterCache.Characters)
                {
                    string label = $"{character.short_name} ({character.voice_id?.Substring(0, 6) ?? "No voice"})";
                    options.Add(new FloatMenuOption(label, () =>
                    {
                        // Aplicar descripción y voz
                        tempPrompt = character.description;
                        if (!string.IsNullOrEmpty(character.voice_id))
                        {
                            ChatGameComponent.Instance.SetVoiceForPawn(pawn, character.voice_id);
                            ColonistVoiceManager.SetVoice(pawn, character.voice_id);
                        }
                        Messages.Message("EchoColony.CharacterImported".Translate(character.short_name),
                                        MessageTypeDefOf.TaskCompletion, false);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private string GetSelectedVoiceName(Pawn pawn)
        {
            string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
            var voice = TTSVoiceCache.Voices.FirstOrDefault(v => v.id == voiceId);
            return voice != null
                ? $"{voice.name} [{voice.language}]"
                : "EchoColony.SelectVoicePlaceholder".Translate().ToString();
        }
    }
}