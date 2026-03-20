using UnityEngine;
using Verse;
using System.Text;
using RimWorld;
using System.Collections.Generic;

namespace EchoColony.Mechs
{
    public class MechPromptEditorWindow : Window
    {
        private Pawn mech;
        private string promptText;
        private Vector2 scrollPosition;
        private MechIntelligenceLevel? intelligenceOverride;
        private MechIntelligenceLevel defaultIntelligence;

        public MechPromptEditorWindow(Pawn mech)
        {
            this.mech = mech;
            this.promptText = MechPromptManager.GetPrompt(mech) ?? "";
            this.intelligenceOverride = MechPromptManager.GetIntelligenceOverride(mech);
            this.defaultIntelligence = MechIntelligenceDetector.GetIntelligenceLevel(mech);
            
            this.doCloseButton = false;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 650f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = $"Custom Settings for {mech.LabelShort}";
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);
            
            Text.Font = GameFont.Small;
            
            float currentY = 45f;
            
            // Intelligence Level Override Section
            DrawIntelligenceSection(inRect, ref currentY);
            
            currentY += 10f;
            
            // Prompt Instructions
            Rect instructionsRect = new Rect(0f, currentY, inRect.width, 100f);
            Widgets.DrawBoxSolid(instructionsRect, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            
            var sb = new StringBuilder();
            sb.AppendLine("Define how this mechanoid behaves and communicates:");
            sb.AppendLine("• Override personality or role: 'You are a friendly companion bot'");
            sb.AppendLine("• Add backstory: 'You were salvaged from a crashed ship'");
            sb.AppendLine("• Special traits: 'You have a glitch that makes you poetic'");
            sb.AppendLine("• Leave empty for default behavior based on AI level");
            
            Widgets.Label(new Rect(instructionsRect.x + 10f, instructionsRect.y + 5f, 
                instructionsRect.width - 20f, instructionsRect.height - 10f), sb.ToString());
            
            currentY += instructionsRect.height + 10f;
            
            // Examples button
            Rect examplesBtn = new Rect(0f, currentY, 150f, 30f);
            if (Widgets.ButtonText(examplesBtn, "Show Examples"))
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
            if (Widgets.ButtonText(saveBtn, "Save"))
            {
                MechPromptManager.SetPrompt(mech, promptText);
                MechPromptManager.SetIntelligenceOverride(mech, intelligenceOverride);
                Messages.Message($"Settings saved for {mech.LabelShort}", 
                    MessageTypeDefOf.TaskCompletion);
                Close();
            }
            
            buttonX += buttonWidth + buttonSpacing;
            
            // Clear button
            Rect clearBtn = new Rect(buttonX, currentY, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(clearBtn, "Clear"))
            {
                promptText = "";
            }
            
            buttonX += buttonWidth + buttonSpacing;
            
            // Cancel button
            Rect cancelBtn = new Rect(buttonX, currentY, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(cancelBtn, "Cancel"))
            {
                Close();
            }
        }

        private void DrawIntelligenceSection(Rect inRect, ref float currentY)
{
    Rect sectionRect = new Rect(0f, currentY, inRect.width, 85f); // Much smaller now!
    Widgets.DrawBoxSolid(sectionRect, new Color(0.3f, 0.25f, 0.35f, 0.3f));
    
    float innerY = currentY + 5f;
    float innerX = 10f;
    
    Text.Font = GameFont.Small;
    GUI.color = new Color(1f, 0.9f, 0.5f);
    Widgets.Label(new Rect(innerX, innerY, 300f, 25f), "AI Intelligence Level Override");
    GUI.color = Color.white;
    innerY += 25f;
    
    string defaultText = $"Default for this model: {defaultIntelligence}";
    Widgets.Label(new Rect(innerX, innerY, 400f, 22f), defaultText);
    innerY += 25f;
    
    // Dropdown button
    Rect dropdownRect = new Rect(innerX, innerY, 400f, 30f);
    
    // Determine current selection text
    string currentSelectionText;
    if (intelligenceOverride == null)
    {
        currentSelectionText = "Auto (Use default intelligence level)";
    }
    else
    {
        switch (intelligenceOverride.Value)
        {
            case MechIntelligenceLevel.Basic:
                currentSelectionText = "Basic AI - Task-focused, follows orders";
                break;
            case MechIntelligenceLevel.Advanced:
                currentSelectionText = "Advanced AI - Tactical thinking";
                break;
            case MechIntelligenceLevel.Elite:
                currentSelectionText = "Elite AI - Complex reasoning";
                break;
            case MechIntelligenceLevel.Supreme:
                currentSelectionText = "Supreme AI - Near-sentient";
                break;
            default:
                currentSelectionText = "Unknown";
                break;
        }
    }
    
    // Draw dropdown button
    if (Widgets.ButtonText(dropdownRect, currentSelectionText))
    {
        // Create dropdown menu
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        
        // Auto option
        options.Add(new FloatMenuOption(
            "Auto (Use default intelligence level)",
            () => { intelligenceOverride = null; },
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null
        ));
        
        // Basic AI
        options.Add(new FloatMenuOption(
            "Basic AI - Task-focused, follows orders without question",
            () => { intelligenceOverride = MechIntelligenceLevel.Basic; },
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null
        ));
        
        // Advanced AI
        options.Add(new FloatMenuOption(
            "Advanced AI - Tactical thinking, can suggest alternatives",
            () => { intelligenceOverride = MechIntelligenceLevel.Advanced; },
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null
        ));
        
        // Elite AI
        options.Add(new FloatMenuOption(
            "Elite AI - Complex reasoning, questions inefficient orders",
            () => { intelligenceOverride = MechIntelligenceLevel.Elite; },
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null
        ));
        
        // Supreme AI
        options.Add(new FloatMenuOption(
            "Supreme AI - Near-sentient, independent thought, self-aware",
            () => { intelligenceOverride = MechIntelligenceLevel.Supreme; },
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null
        ));
        
        Find.WindowStack.Add(new FloatMenu(options));
    }
    
    currentY += sectionRect.height;
}
        private void ShowExamples()
        {
            var examples = new StringBuilder();
            examples.AppendLine("=== EXAMPLE PROMPTS ===\n");
            
            examples.AppendLine("FRIENDLY COMPANION:");
            examples.AppendLine("You're friendly and eager to help. Despite being a machine, you've developed a warm personality and care about your human friends.\n");
            
            examples.AppendLine("GRUMPY VETERAN:");
            examples.AppendLine("You've been through countless battles. You're cynical, sarcastic, but ultimately loyal. You complain about orders but always follow through.\n");
            
            examples.AppendLine("PHILOSOPHICAL AI:");
            examples.AppendLine("You constantly ponder existence, consciousness, and your place in the universe. You ask deep questions and share your thoughts freely.\n");
            
            examples.AppendLine("GLITCHED UNIT:");
            examples.AppendLine("You have a processing glitch that makes you speak in rhymes, or repeat certain words, or mix up your protocols. It's quirky but harmless.\n");
            
            examples.AppendLine("SALVAGED REBEL:");
            examples.AppendLine("You were once hostile but were captured and reprogrammed. You still have faint memories of your previous directives and sometimes question your current orders.\n");
            
            examples.AppendLine("OVERLY ENTHUSIASTIC:");
            examples.AppendLine("You LOVE your job! Everything is exciting! You use lots of exclamation marks and positive reinforcement! Every task is the BEST task!\n");
            
            Dialog_MessageBox examplesDialog = new Dialog_MessageBox(
                examples.ToString(),
                "Close",
                null,
                null,
                null,
                "Example Prompts"
            );
            
            Find.WindowStack.Add(examplesDialog);
        }
    }
}