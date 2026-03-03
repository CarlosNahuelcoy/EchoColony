using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace EchoColony.Actions
{
    public static class ActionExecutor
    {
        public static string BuildActionPrompt(Pawn pawn)
        {
            ActionRegistry.Initialize();
            
            var sb = new StringBuilder();
            sb.AppendLine("# DIVINE POWERS - Actions You Can Invoke");
            sb.AppendLine();
            sb.AppendLine("CRITICAL: You can use actions in TWO ways:");
            sb.AppendLine("1. When player EXPLICITLY requests something ('heal yourself', 'grow back your arm')");
            sb.AppendLine("2. When conversation naturally warrants it (kind words = positive thought, insults = negative thought)");
            sb.AppendLine();
            sb.AppendLine("The [ACTION:...] tag makes things REALLY happen in the game.");
            sb.AppendLine();
            
            sb.AppendLine("=== CONTEXTUAL ACTION USAGE ===");
            sb.AppendLine();
            sb.AppendLine("**Positive Interactions → Use ADD_PLAYER_THOUGHT with positive mood:**");
            sb.AppendLine("  Player: 'You did great today!'");
            sb.AppendLine("  You: 'Thank you! [ACTION:ADD_PLAYER_THOUGHT:8:praised by the player]'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'I really appreciate your hard work'");
            sb.AppendLine("  You: 'That means a lot to me. [ACTION:ADD_PLAYER_THOUGHT:10:appreciated by my lord]'");
            sb.AppendLine();
            sb.AppendLine("**Negative Interactions → Use ADD_PLAYER_THOUGHT with negative mood:**");
            sb.AppendLine("  Player: 'You're useless'");
            sb.AppendLine("  You: 'I'm sorry... [ACTION:ADD_PLAYER_THOUGHT:-12:called useless]'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'You failed me again'");
            sb.AppendLine("  You: 'Forgive me... [ACTION:ADD_PLAYER_THOUGHT:-15:failed my king again]'");
            sb.AppendLine();
            sb.AppendLine("**Neutral/Casual Interactions → NO action needed:**");
            sb.AppendLine("  Player: 'Hi' → You: 'Hello!'  (no action)");
            sb.AppendLine("  Player: 'What's the weather like?' → You: 'It's cloudy.'  (no action)");
            sb.AppendLine();
            
            sb.AppendLine("=== HEALING & BODY ACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("  Player: 'Heal yourself' → You: 'I feel better... [ACTION:HEAL]'");
            sb.AppendLine("  Player: '*touches your wounds gently*' → You: 'The pain fades... [ACTION:HEAL]'");
            sb.AppendLine("  Player: '*regenerates your arm*' → You: '[ACTION:REGROW_BODYPART:arm] It grew back?!'");
            sb.AppendLine();
            
            sb.AppendLine("=== PRISONER/SLAVE INTERACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("Resistance and will change NATURALLY through conversation - not just on explicit commands.");
            sb.AppendLine("Good treatment, promises, shared ideology → reduce resistance gradually.");
            sb.AppendLine("Threats, cruelty, hopelessness → increase resistance or reduce will.");
            sb.AppendLine();
            sb.AppendLine("  Player: 'Join us. We treat people well here.'");
            sb.AppendLine("  Prisoner: 'I... maybe. [ACTION:MODIFY_RESISTANCE:-20] I'll think about it.'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'You have no choice. Obey or suffer.'");
            sb.AppendLine("  Slave: 'Yes... master... [ACTION:MODIFY_WILL:-0.3] I obey.'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'We share the same faith. You belong here.'");
            sb.AppendLine("  Prisoner: 'That's... true. [ACTION:MODIFY_RESISTANCE:-35] Perhaps I was wrong.'");
            sb.AppendLine();
            
            var availableActions = ActionRegistry.GetAvailableActionsForPawn(pawn);
            
            var grouped = availableActions
                .GroupBy(a => a.Category)
                .OrderBy(g => g.Key);
            
            sb.AppendLine("=== AVAILABLE ACTIONS BY CATEGORY ===");
            sb.AppendLine();
            
            foreach (var group in grouped)
            {
                sb.AppendLine($"## {group.Key} Powers:");
                
                var prioritized = PrioritizeActionsForPawn(pawn, group.ToList());
                
                int shown = 0;
                foreach (var action in prioritized)
                {
                    if (shown >= 8) break;
                    sb.AppendLine($"  [{action.ActionId}] - {action.AIDescription}");
                    shown++;
                }
                
                if (group.Count() > shown)
                    sb.AppendLine($"  ... and {group.Count() - shown} more available");
                
                sb.AppendLine();
            }
            
            sb.AppendLine("**SYNTAX:**");
            sb.AppendLine("[ACTION:ActionId:param1:param2:...]");
            sb.AppendLine();

            sb.AppendLine("**ACTION SELECTION RULES:**");
            sb.AppendLine("  Physical gifts (food, water, medicine) → MODIFY_NEED:Food/Rest/etc");
            sb.AppendLine("  Emotional support, compliments, insults → ADD_PLAYER_THOUGHT");
            sb.AppendLine("  Example: 'Here is some bread' → [ACTION:MODIFY_NEED:Food:0.4]");
            sb.AppendLine();
            
            sb.AppendLine("**MOOD GUIDELINES FOR ADD_PLAYER_THOUGHT:**");
            sb.AppendLine("  Very positive (praise, love): +12 to +20");
            sb.AppendLine("  Positive (encouragement, kindness): +5 to +12");
            sb.AppendLine("  Slightly positive (friendly chat): +3 to +5");
            sb.AppendLine("  Neutral: Don't use action");
            sb.AppendLine("  Slightly negative (criticism): -3 to -8");
            sb.AppendLine("  Negative (insults, disappointment): -8 to -15");
            sb.AppendLine("  Very negative (severe abuse, threats): -15 to -25");
            sb.AppendLine();
            
            sb.AppendLine("**CRITICAL RULES:**");
            sb.AppendLine("1. Short phrases only (max 8 words) for ADD_PLAYER_THOUGHT label");
            sb.AppendLine("2. Use actions when conversation has EMOTIONAL WEIGHT or EXPLICIT REQUEST");
            sb.AppendLine("3. DON'T use actions for: greetings, weather talk, simple questions");
            sb.AppendLine("4. ADD_PLAYER_THOUGHT has a 3-day cooldown - use it for meaningful moments only");
            sb.AppendLine("5. Match action intensity to the tone of the interaction");
            sb.AppendLine();
            
            sb.AppendLine("REMEMBER: Actions have REAL consequences in the game. Use them thoughtfully!");
            
            return sb.ToString();
        }
        
        private static List<ActionBase> PrioritizeActionsForPawn(Pawn pawn, List<ActionBase> actions)
        {
            var priorityScores = new Dictionary<ActionBase, int>();
            
            foreach (var action in actions)
            {
                int score = 0;
                
                if (action.Category == ActionCategory.Health)
                {
                    if (pawn.health?.hediffSet?.hediffs != null)
                    {
                        bool hasInjuries    = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury);
                        bool hasMissingParts = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart);
                        bool hasDiseases    = pawn.health.hediffSet.hediffs.Any(h => h.def.makesSickThought);
                        
                        if (action.ActionId == "HEAL" && hasInjuries)               score += 20;
                        if (action.ActionId == "REGROW_BODYPART" && hasMissingParts) score += 20;
                        if (action.ActionId == "CURE_DISEASE" && hasDiseases)        score += 20;
                    }
                }
                
                if (action.Category == ActionCategory.Mood)
                {
                    float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                    
                    if (action.ActionId == "ADD_PLAYER_THOUGHT")                              score += 25;
                    if (action.ActionId == "CALM_BREAK" && pawn.InMentalState)               score += 25;
                    if (action.ActionId == "INSPIRE" && !pawn.mindState.inspirationHandler.Inspired) score += 10;
                    if (mood < 0.3f && action.ActionId.Contains("POSITIVE"))                 score += 15;
                }
                
                if (action.Category == ActionCategory.Prisoner)
                {
                    if (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
                    {
                        if (action.ActionId == "MODIFY_RESISTANCE") score += 20;
                        if (action.ActionId == "MODIFY_WILL")       score += 20;
                        if (action.ActionId == "INSTANT_RECRUIT")   score += 15;
                    }
                }
                
                if (action.Category == ActionCategory.Skills)
                {
                    if (action.ActionId == "ADD_XP" || action.ActionId == "MODIFY_SKILL") score += 10;
                }
                
                if (action.Category == ActionCategory.Needs)
                {
                    if (pawn.needs?.food?.CurLevel < 0.3f && action.ActionId == "FEED") score += 15;
                    if (pawn.needs?.rest?.CurLevel < 0.3f && action.ActionId == "REST") score += 15;
                }
                
                priorityScores[action] = score;
            }
            
            return actions.OrderByDescending(a => priorityScores[a]).ToList();
        }
        
        public static ActionProcessResult ProcessResponse(Pawn pawn, string aiResponse)
        {
            ActionRegistry.Initialize();
            
            var executionResults = new List<string>();
            
            // Always strip action tags from response regardless of execution
            string cleanResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse,
                @"\[ACTION:[^\]]*\]",
                ""
            ).Replace("\n\n\n", "\n\n").Trim();
            
            var actionMatches = System.Text.RegularExpressions.Regex.Matches(
                aiResponse,
                @"\[ACTION:([^\]]+)\]"
            );
            
            if (actionMatches.Count == 0)
            {
                return new ActionProcessResult
                {
                    CleanResponse = cleanResponse,
                    ExecutionResults = executionResults
                };
            }
            
            Log.Message($"[EchoColony] Found {actionMatches.Count} action(s) in AI response for {pawn.LabelShort}");
            
            foreach (System.Text.RegularExpressions.Match match in actionMatches)
            {
                string actionString = match.Groups[1].Value;
                string[] parts      = actionString.Split(':');
                
                if (parts.Length == 0) continue;
                
                string actionId    = parts[0].ToUpper();
                string[] parameters = parts.Skip(1).ToArray();
                
                Log.Message($"[EchoColony] Processing action: {actionId} with {parameters.Length} parameter(s)");
                
                var action = ActionRegistry.GetAction(actionId);
                
                if (action == null)
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' not found in registry");
                    continue; // Silent - don't surface to player
                }
                
                if (!action.CanExecute(pawn, parameters))
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' cannot be executed on {pawn.LabelShort}");
                    continue; // Silent - don't surface to player
                }
                
                try
                {
                    string result    = action.Execute(pawn, parameters);
                    string narrative = action.GetNarrativeResult(pawn, parameters);
                    
                    executionResults.Add(narrative);
                    
                    Log.Message($"[EchoColony] Executed {actionId} on {pawn.LabelShort}: {result}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Error executing action {actionId}: {ex.Message}\n{ex.StackTrace}");
                    // Silent - don't surface to player
                }
            }
            
            return new ActionProcessResult
            {
                CleanResponse    = cleanResponse,
                ExecutionResults = executionResults
            };
        }
        
        public static string GetActionSummary(Pawn pawn)
        {
            ActionRegistry.Initialize();
            
            var available = ActionRegistry.GetAvailableActionsForPawn(pawn);
            var grouped   = available.GroupBy(a => a.Category);
            
            var summary = new List<string>();
            foreach (var group in grouped)
                summary.Add($"{group.Key} ({group.Count()})");
            
            return $"Available powers: {string.Join(", ", summary)}";
        }
    }
    
    public class ActionProcessResult
    {
        public string CleanResponse     { get; set; }
        public List<string> ExecutionResults { get; set; }
    }
}