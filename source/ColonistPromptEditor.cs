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
            this.pawn       = pawn;
            this.tempPrompt = ColonistPromptManager.GetPrompt(pawn);
            this.closeOnClickedOutside  = true;
            this.doCloseX               = true;
            this.absorbInputAroundWindow = true;
            this.forcePause             = true;
            this.draggable              = true;

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

            if (MyMod.Settings.modelSource == ModelSource.Player2 && Player2CharacterCache.Characters.Count == 0)
            {
                MyStoryModComponent.Instance.StartCoroutine(Player2Importer.LoadCharacters());
            }
        }

        // Slightly taller to accommodate the group section
        public override Vector2 InitialSize => new Vector2(500f, 545f);

        public override void DoWindowContents(Rect inRect)
        {
            float currentY = 0f;

            DrawHeader(inRect, ref currentY);

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
                DrawVoiceSection(inRect, ref currentY);

            DrawGroupSection(inRect, ref currentY);

            DrawPromptSection(inRect, ref currentY);

            DrawSaveButton(inRect);
        }

        private void DrawHeader(Rect inRect, ref float currentY)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, currentY, inRect.width - 100f, 30f),
                "EchoColony.PersonalizingTitle".Translate(pawn.LabelCap));
            Text.Font = GameFont.Small;

            Rect memoryButtonRect = new Rect(inRect.width - 90f, currentY, 85f, 30f);
            Color originalColor = GUI.color;
            GUI.color = new Color(0.8f, 0.9f, 1f, 0.8f);
            if (Widgets.ButtonText(memoryButtonRect, "EchoColony.MemoriesButton".Translate()))
                Find.WindowStack.Add(new ColonistMemoryViewer(pawn));
            GUI.color = originalColor;

            TooltipHandler.TipRegion(memoryButtonRect,
                "EchoColony.MemoriesTooltip".Translate(pawn.LabelShort));

            currentY += 40f;
        }

        private void DrawVoiceSection(Rect inRect, ref float currentY)
        {
            Widgets.Label(new Rect(0, currentY, inRect.width, 25f),
                "EchoColony.VoiceConfigurationTitle".Translate());
            currentY += 30f;

            Widgets.DrawBoxSolid(new Rect(0, currentY - 5f, inRect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.3f));

            Widgets.Label(new Rect(0f, currentY, 50f, 30f), "EchoColony.VoiceLabel".Translate());

            Rect dropdownRect = new Rect(60f, currentY, 150f, 30f);
            if (Widgets.ButtonText(dropdownRect, GetSelectedVoiceName(pawn)))
            {
                var options = new List<FloatMenuOption>();
                foreach (var voice in TTSVoiceCache.Voices)
                {
                    string voiceId = voice.id;
                    options.Add(new FloatMenuOption($"{voice.name} [{voice.language}]", () =>
                    {
                        ChatGameComponent.Instance.SetVoiceForPawn(pawn, voiceId);
                        ColonistVoiceManager.SetVoice(pawn, voiceId);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Rect importBtnRect = new Rect(220f, currentY, 100f, 30f);
            GUI.color = new Color(0.9f, 1f, 0.9f);
            if (Widgets.ButtonText(importBtnRect, "EchoColony.ImportButton".Translate()))
                HandlePlayer2Import();
            GUI.color = Color.white;

            Rect testVoiceRect = new Rect(330f, currentY, 70f, 30f);
            GUI.color = new Color(1f, 0.9f, 0.8f);
            TooltipHandler.TipRegion(testVoiceRect, "EchoColony.TestVoiceTooltip".Translate());
            if (Widgets.ButtonText(testVoiceRect, "EchoColony.TestButton".Translate()))
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (!string.IsNullOrEmpty(voiceId) && !string.IsNullOrEmpty(testText))
                    MyStoryModComponent.Instance.StartCoroutine(
                        TTSManager.Speak(testText, voiceId, "female", "en_US", 1f));
            }
            GUI.color = Color.white;

            currentY += 35f;

            Widgets.Label(new Rect(0f, currentY, 120f, 20f), "EchoColony.TestTextLabel".Translate());
            GUI.SetNextControlName("TestTextArea");
            testText = Widgets.TextField(new Rect(0f, currentY + 20f, inRect.width, 30f), testText);

            currentY += 65f;
        }

        /// <summary>
        /// Compact group row: shows current group tag + Change button + Manage Groups button.
        /// </summary>
        private void DrawGroupSection(Rect inRect, ref float currentY)
        {
            // Separator
            Widgets.DrawBoxSolid(new Rect(0, currentY, inRect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.3f));
            currentY += 6f;

            // "Group:" label
            Widgets.Label(new Rect(0f, currentY, 48f, 26f), "Group:");

            // Current group tag
            var currentGroup = ColonistGroupManager.GetGroup(pawn);
            float tagX = 52f;

            if (currentGroup != null)
            {
                // Colored tag background
                float tagW = Mathf.Min(Text.CalcSize(currentGroup.name).x + 16f, 140f);
                Rect tagBg = new Rect(tagX, currentY + 3f, tagW, 22f);
                Widgets.DrawBoxSolid(tagBg, new Color(currentGroup.color.r, currentGroup.color.g,
                    currentGroup.color.b, 0.35f));
                Widgets.DrawBox(tagBg);

                // Small color dot inside tag
                Rect dot = new Rect(tagBg.x + 4f, tagBg.y + 4f, 14f, 14f);
                Widgets.DrawBoxSolid(dot, currentGroup.color);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(dot.xMax + 4f, tagBg.y, tagBg.width - dot.width - 10f, tagBg.height),
                    currentGroup.name);
                Text.Anchor = TextAnchor.UpperLeft;

                tagX += tagW + 8f;
            }
            else
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(tagX, currentY, 80f, 26f), "None");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                tagX += 88f;
            }

            // Change button — FloatMenu with all groups + None
            Rect changeBtn = new Rect(tagX, currentY, 60f, 26f);
            if (Widgets.ButtonText(changeBtn, "Change"))
            {
                var options = new List<FloatMenuOption>();

                options.Add(new FloatMenuOption("None", () => ColonistGroupManager.ClearGroup(pawn)));

                foreach (var group in ColonistGroupManager.GetAllGroups())
                {
                    string groupId   = group.id;
                    string groupName = group.name;
                    options.Add(new FloatMenuOption(groupName, () =>
                        ColonistGroupManager.SetGroup(pawn, groupId)));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Manage Groups button — opens GroupManagerWindow
            Rect manageBtn = new Rect(inRect.width - 120f, currentY, 118f, 26f);
            GUI.color = new Color(0.85f, 0.85f, 1f);
            if (Widgets.ButtonText(manageBtn, "Manage Groups"))
                Find.WindowStack.Add(new GroupManagerWindow());
            GUI.color = Color.white;

            currentY += 34f;

            // Separator
            Widgets.DrawBoxSolid(new Rect(0, currentY, inRect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.3f));
            currentY += 6f;
        }

        private void DrawPromptSection(Rect inRect, ref float currentY)
        {
            Widgets.Label(new Rect(0, currentY, inRect.width, 25f),
                "EchoColony.CustomPromptTitle".Translate());
            currentY += 30f;

            Widgets.DrawBoxSolid(new Rect(0, currentY - 5f, inRect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.3f));

            // Age ignore checkbox
            Widgets.Label(new Rect(0f, currentY, 250f, 25f),
                "EchoColony.IgnoreAgeLabel".Translate());
            Rect ageToggle = new Rect(260f, currentY + 2f, 24f, 24f);
            bool currentValue = ColonistPromptManager.GetIgnoreAge(pawn);
            bool tempValue    = currentValue;
            Widgets.Checkbox(ageToggle.position, ref tempValue);
            if (tempValue != currentValue)
                ColonistPromptManager.SetIgnoreAge(pawn, tempValue);
            TooltipHandler.TipRegion(new Rect(0f, currentY, 300f, 25f),
                "EchoColony.IgnoreAgeTooltip".Translate());

            currentY += 35f;

            // Prompt textarea — fills remaining space above save button
            const float buttonHeight = 30f;
            const float padding      = 10f;
            float textHeight = inRect.height - currentY - buttonHeight - padding;

            Rect scrollOuterRect = new Rect(0, currentY, inRect.width, textHeight);
            Widgets.DrawBoxSolid(scrollOuterRect, Color.black);
            Widgets.DrawBox(scrollOuterRect);

            Rect scrollViewRect = scrollOuterRect.ContractedBy(4f);
            float textWidth     = scrollViewRect.width - 16f;

            GUIStyle tempStyle = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                fontSize = 14,
                padding  = new RectOffset(6, 6, 6, 6),
            };
            float textContentHeight = tempStyle.CalcHeight(new GUIContent(tempPrompt), textWidth);
            float contentHeight     = Mathf.Max(textContentHeight + 40f, scrollViewRect.height + 50f);
            Rect viewRect           = new Rect(0f, 0f, textWidth, contentHeight);

            Widgets.BeginScrollView(scrollViewRect, ref scroll, viewRect);

            GUIStyle style = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap         = true,
                normal           = { textColor = Color.white },
                padding          = new RectOffset(6, 6, 6, 6),
                fontSize         = 14,
            };
            GUI.SetNextControlName("PromptTextArea");
            tempPrompt = GUI.TextArea(new Rect(0f, 0f, textWidth, contentHeight - 20f), tempPrompt, style);

            Widgets.EndScrollView();

            if (!hasFocused && Event.current.type == EventType.Layout)
            {
                GUI.FocusControl("PromptTextArea");
                hasFocused = true;
            }
        }

        private void DrawSaveButton(Rect inRect)
        {
            const float buttonHeight = 30f;
            Rect saveButtonRect = new Rect(inRect.width - 100f, inRect.height - buttonHeight, 100f, buttonHeight);

            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (Widgets.ButtonText(saveButtonRect, "EchoColony.SaveButton".Translate()))
            {
                ColonistPromptManager.SetPrompt(pawn, tempPrompt);
                Messages.Message("EchoColony.ConfigurationSaved".Translate(pawn.LabelShort),
                    MessageTypeDefOf.TaskCompletion);
                Close();
            }
            GUI.color = Color.white;
        }

        private void HandlePlayer2Import()
        {
            if (Player2CharacterCache.Characters.Count == 0)
            {
                Messages.Message("EchoColony.NoCharactersFound".Translate(),
                    MessageTypeDefOf.RejectInput, false);
            }
            else if (Player2CharacterCache.Characters.Count == 1)
            {
                var character = Player2CharacterCache.Characters[0];
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
                var options = new List<FloatMenuOption>();
                foreach (var character in Player2CharacterCache.Characters)
                {
                    string label = $"{character.short_name} ({character.voice_id?.Substring(0, 6) ?? "No voice"})";
                    options.Add(new FloatMenuOption(label, () =>
                    {
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