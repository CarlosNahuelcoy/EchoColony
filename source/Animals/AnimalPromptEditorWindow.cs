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
        private Vector2 scrollPosition;

        public AnimalPromptEditorWindow(Pawn animal)
        {
            this.animal = animal;
            this.promptText = AnimalPromptManager.GetPrompt(animal) ?? "";
            
            this.doCloseButton = false; // CAMBIADO: Evita el botón automático que se superpone
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public override void DoWindowContents(Rect inRect)
{
    Text.Font = GameFont.Medium;
    string title = "EchoColony.AnimalPromptEditorTitle".Translate(animal.LabelShort);
    Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);
    
    Text.Font = GameFont.Small;
    
    float currentY = 45f;
    
    // Instructions
    Rect instructionsRect = new Rect(0f, currentY, inRect.width, 120f);
    Widgets.DrawBoxSolid(instructionsRect, new Color(0.2f, 0.3f, 0.4f, 0.3f));
    
    Widgets.Label(new Rect(instructionsRect.x + 10f, instructionsRect.y + 5f, 
        instructionsRect.width - 20f, instructionsRect.height - 10f), 
        "EchoColony.AnimalPromptInstructions".Translate());
    
    currentY += instructionsRect.height + 10f;
    
    // Examples button
    Rect examplesBtn = new Rect(0f, currentY, 150f, 30f);
    if (Widgets.ButtonText(examplesBtn, "EchoColony.AnimalPromptShowExamples".Translate()))
    {
        ShowExamples();
    }
    
    currentY += 40f;
    
    // Text area
    float textAreaHeight = inRect.height - currentY - 60f;
    Rect scrollRect = new Rect(0f, currentY, inRect.width, textAreaHeight);
    Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Mathf.Max(textAreaHeight, Text.CalcHeight(promptText, scrollRect.width - 16f) + 10f));

    Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
    Rect textRect = new Rect(0f, 0f, viewRect.width, viewRect.height);
    promptText = Widgets.TextArea(textRect, promptText);
    Widgets.EndScrollView();

    currentY += scrollRect.height + 10f;
    
    // Buttons
    float buttonWidth = 100f;
    float buttonSpacing = 15f;
    float totalButtonWidth = (buttonWidth * 3) + (buttonSpacing * 2);
    float buttonX = (inRect.width - totalButtonWidth) / 2f;
    float buttonHeight = 35f;
    
    // Save button
    Rect saveBtn = new Rect(buttonX, currentY, buttonWidth, buttonHeight);
    if (Widgets.ButtonText(saveBtn, "EchoColony.AnimalPromptSave".Translate()))
    {
        AnimalPromptManager.SetPrompt(animal, promptText);
        Messages.Message("EchoColony.AnimalPromptSaved".Translate(animal.LabelShort), 
            MessageTypeDefOf.TaskCompletion);
        Close();
    }
    
    buttonX += buttonWidth + buttonSpacing;
    
    // Clear button
    Rect clearBtn = new Rect(buttonX, currentY, buttonWidth, buttonHeight);
    if (Widgets.ButtonText(clearBtn, "EchoColony.AnimalPromptClear".Translate()))
    {
        promptText = "";
    }
    
    buttonX += buttonWidth + buttonSpacing;
    
    // Cancel button
    Rect cancelBtn = new Rect(buttonX, currentY, buttonWidth, buttonHeight);
    if (Widgets.ButtonText(cancelBtn, "EchoColony.AnimalPromptCancel".Translate()))
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
    
    Dialog_MessageBox examplesDialog = new Dialog_MessageBox(
        examples.ToString(),
        "Close",
        null,
        null,
        null,
        "EchoColony.AnimalPromptExamplesTitle".Translate()
    );
    
    Find.WindowStack.Add(examplesDialog);
}
    
    }
}