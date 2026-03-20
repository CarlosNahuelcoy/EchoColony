using UnityEngine;
using Verse;
using System.Text;
using RimWorld;

namespace EchoColony.Animals
{
    public class AnimalPromptEditorWindow : Window
    {
        private Pawn animal;
        private string promptText;
        private bool isIntelligent;
        private Vector2 scrollPosition;

        public AnimalPromptEditorWindow(Pawn animal)
        {
            this.animal = animal;
            this.promptText = AnimalPromptManager.GetPrompt(animal) ?? "";
            this.isIntelligent = AnimalPromptManager.GetIsIntelligent(animal);

            this.doCloseButton = false;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = "EchoColony.AnimalPromptEditorTitle".Translate(animal.LabelShort);
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);

            Text.Font = GameFont.Small;
            float currentY = 45f;

            // ── Instructions ─────────────────────────────────────────────────────
            Rect instructionsRect = new Rect(0f, currentY, inRect.width, 80f);
            Widgets.DrawBoxSolid(instructionsRect, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            Widgets.Label(
                new Rect(instructionsRect.x + 10f, instructionsRect.y + 5f,
                         instructionsRect.width - 20f, instructionsRect.height - 10f),
                "EchoColony.AnimalPromptInstructions".Translate());
            currentY += instructionsRect.height + 10f;

            // ── Intelligent Animal Toggle ─────────────────────────────────────────
            Rect toggleBg = new Rect(0f, currentY, inRect.width, 62f);
            Color bgColor = isIntelligent
                ? new Color(0.15f, 0.35f, 0.15f, 0.45f)
                : new Color(0.2f, 0.2f, 0.2f, 0.3f);
            Widgets.DrawBoxSolid(toggleBg, bgColor);

            // Label + checkbox on the left
            Rect checkRect = new Rect(toggleBg.x + 10f, toggleBg.y + 10f, 24f, 24f);
            bool prevIntelligent = isIntelligent;
            Widgets.Checkbox(checkRect.x, checkRect.y, ref isIntelligent);

            Text.Font = GameFont.Small;
            GUI.color = isIntelligent ? new Color(0.5f, 1f, 0.5f) : Color.white;
            Widgets.Label(
                new Rect(checkRect.xMax + 8f, checkRect.y, inRect.width - 120f, 24f),
                isIntelligent
                    ? "Intelligent Animal  ✦  Speaks and understands human language"
                    : "Normal Animal  ·  Communicates through sounds and body language");
            GUI.color = Color.white;

            // Subtitle hint
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(
                new Rect(checkRect.x + 2f, checkRect.yMax + 4f, inRect.width - 20f, 18f),
                isIntelligent
                    ? "Colonists will treat this creature as a sentient being capable of conversation."
                    : "Enable this for dragons, ogres, uplifted creatures, or any animal that should speak.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            currentY += toggleBg.height + 10f;

            // ── Examples button ───────────────────────────────────────────────────
            Rect examplesBtn = new Rect(0f, currentY, 160f, 30f);
            if (Widgets.ButtonText(examplesBtn, "EchoColony.AnimalPromptShowExamples".Translate()))
                ShowExamples();
            currentY += 40f;

            // ── Custom prompt textarea ────────────────────────────────────────────
            float textAreaHeight = inRect.height - currentY - 55f;
            Rect scrollRect = new Rect(0f, currentY, inRect.width, textAreaHeight);
            float innerHeight = Mathf.Max(textAreaHeight,
                Text.CalcHeight(promptText, scrollRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, innerHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            promptText = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, viewRect.height), promptText);
            Widgets.EndScrollView();

            currentY += textAreaHeight + 10f;

            // ── Buttons ───────────────────────────────────────────────────────────
            float buttonWidth = 100f;
            float buttonSpacing = 15f;
            float totalButtonWidth = (buttonWidth * 3) + (buttonSpacing * 2);
            float buttonX = (inRect.width - totalButtonWidth) / 2f;
            float buttonY = inRect.height - 40f;
            float buttonHeight = 35f;

            if (Widgets.ButtonText(new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                "EchoColony.AnimalPromptSave".Translate()))
            {
                AnimalPromptManager.SetPrompt(animal, promptText);
                AnimalPromptManager.SetIsIntelligent(animal, isIntelligent);
                Messages.Message(
                    "EchoColony.AnimalPromptSaved".Translate(animal.LabelShort),
                    MessageTypeDefOf.TaskCompletion);
                Close();
            }

            buttonX += buttonWidth + buttonSpacing;

            if (Widgets.ButtonText(new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                "EchoColony.AnimalPromptClear".Translate()))
            {
                promptText = "";
                isIntelligent = false;
            }

            buttonX += buttonWidth + buttonSpacing;

            if (Widgets.ButtonText(new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                "EchoColony.AnimalPromptCancel".Translate()))
            {
                Close();
            }
        }

        private void ShowExamples()
        {
            var examples = new StringBuilder();
            examples.AppendLine("=== " + "EchoColony.AnimalPromptExamplesTitle".Translate() + " ===\n");
            examples.AppendLine("EchoColony.AnimalPromptExample1Title".Translate());
            examples.AppendLine("EchoColony.AnimalPromptExample1".Translate() + "\n");
            examples.AppendLine("EchoColony.AnimalPromptExample2Title".Translate());
            examples.AppendLine("EchoColony.AnimalPromptExample2".Translate() + "\n");
            examples.AppendLine("EchoColony.AnimalPromptExample3Title".Translate());
            examples.AppendLine("EchoColony.AnimalPromptExample3".Translate() + "\n");
            examples.AppendLine("EchoColony.AnimalPromptExample4Title".Translate());
            examples.AppendLine("EchoColony.AnimalPromptExample4".Translate() + "\n");
            examples.AppendLine("EchoColony.AnimalPromptExample5Title".Translate());
            examples.AppendLine("EchoColony.AnimalPromptExample5".Translate() + "\n");

            Find.WindowStack.Add(new Dialog_MessageBox(
                examples.ToString(), "Close", null, null, null,
                "EchoColony.AnimalPromptExamplesTitle".Translate()));
        }
    }
}