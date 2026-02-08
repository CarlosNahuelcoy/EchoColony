using UnityEngine;
using Verse;
using RimWorld;

namespace EchoColony
{
    public class StorytellerPromptEditor : Window
    {
        private Storyteller storyteller;
        private string tempPrompt;
        private Vector2 scroll;
        private bool hasFocused = false;

        public StorytellerPromptEditor(Storyteller storyteller)
        {
            this.storyteller = storyteller;
            
            // ✅ SOLO cargar el custom prompt del usuario (vacío si no existe)
            this.tempPrompt = StorytellerPromptManager.GetPrompt();
            
            // ✅ NO pre-llenar con el default - dejarlo vacío
            
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            float currentY = 0f;

            DrawHeader(inRect, ref currentY);
            DrawPromptSection(inRect, ref currentY);
            DrawActionButtons(inRect);
        }

        private void DrawHeader(Rect inRect, ref float currentY)
        {
            Text.Font = GameFont.Medium;
            string title = $"Customize {storyteller?.def.label ?? "Storyteller"}";
            Widgets.Label(new Rect(0, currentY, inRect.width, 30f), title);
            Text.Font = GameFont.Small;

            currentY += 40f;
        }

        private void DrawPromptSection(Rect inRect, ref float currentY)
        {
            // ✅ Título actualizado para claridad
            Rect promptTitleRect = new Rect(0, currentY, inRect.width, 25f);
            Widgets.Label(promptTitleRect, "Additional Personality Instructions");
            currentY += 25f;
            
            // ✅ Texto explicativo
            Rect explainRect = new Rect(0, currentY, inRect.width, 20f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(explainRect, "These instructions will be added to the base personality. Leave empty for default behavior.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            currentY += 25f;

            // Línea separadora
            Rect separatorRect = new Rect(0, currentY - 5f, inRect.width, 1f);
            Widgets.DrawBoxSolid(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            // Área de texto
            float buttonHeight = 30f;
            float padding = 10f;
            float textAreaTop = currentY;
            float textHeight = inRect.height - textAreaTop - buttonHeight - padding;

            Rect scrollOuterRect = new Rect(0, textAreaTop, inRect.width, textHeight);
            Widgets.DrawBoxSolid(scrollOuterRect, Color.black);
            Widgets.DrawBox(scrollOuterRect);

            Rect scrollViewRect = scrollOuterRect.ContractedBy(4f);
            
            float textWidth = scrollViewRect.width - 16f;
            float minHeight = scrollViewRect.height;
            
            GUIStyle tempStyle = new GUIStyle(GUI.skin.textArea);
            tempStyle.wordWrap = true;
            tempStyle.fontSize = 14;
            tempStyle.padding = new RectOffset(6, 6, 6, 6);
            
            float textContentHeight = tempStyle.CalcHeight(new GUIContent(tempPrompt), textWidth);
            float contentHeight = Mathf.Max(textContentHeight + 40f, minHeight + 50f);

            Rect viewRect = new Rect(0f, 0f, textWidth, contentHeight);

            Widgets.BeginScrollView(scrollViewRect, ref scroll, viewRect);

            Rect textAreaRect = new Rect(0f, 0f, textWidth, contentHeight - 20f);
            GUI.SetNextControlName("PromptTextArea");

            GUIStyle style = new GUIStyle(GUI.skin.textArea);
            style.wordWrap = true;
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(6, 6, 6, 6);
            style.fontSize = 14;

            // ✅ Placeholder text cuando está vacío
            if (string.IsNullOrWhiteSpace(tempPrompt) && GUI.GetNameOfFocusedControl() != "PromptTextArea")
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(textAreaRect, "Example:\n- Always speak in rhymes\n- Be extra dramatic about small events\n- Reference historical events\n- Use medieval language", style);
                GUI.color = Color.white;
            }

            tempPrompt = GUI.TextArea(textAreaRect, tempPrompt, style);

            Widgets.EndScrollView();

            if (!hasFocused && Event.current.type == EventType.Layout)
            {
                GUI.FocusControl("PromptTextArea");
                hasFocused = true;
            }
        }

        private void DrawActionButtons(Rect inRect)
        {
            float buttonHeight = 30f;
            float buttonWidth = 100f;
            float bottomY = inRect.height - buttonHeight;

            // Botón Reset
            Rect resetButtonRect = new Rect(0f, bottomY, buttonWidth, buttonHeight);
            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (Widgets.ButtonText(resetButtonRect, "Clear"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Clear custom instructions and revert to default personality?",
                    () => {
                        StorytellerPromptManager.ClearPrompt();
                        tempPrompt = "";
                        Messages.Message("Custom instructions cleared", MessageTypeDefOf.TaskCompletion);
                    }
                ));
            }
            GUI.color = Color.white;

            // Botón Save
            Rect saveButtonRect = new Rect(inRect.width - buttonWidth, bottomY, buttonWidth, buttonHeight);
            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (Widgets.ButtonText(saveButtonRect, "Save"))
            {
                StorytellerPromptManager.SetPrompt(tempPrompt);
                Messages.Message($"{storyteller?.def.label ?? "Storyteller"} custom instructions saved", MessageTypeDefOf.TaskCompletion);
                Close();
            }
            GUI.color = Color.white;
        }
    }
}