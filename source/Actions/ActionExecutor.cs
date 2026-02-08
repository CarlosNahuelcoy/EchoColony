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
            sb.AppendLine("  Player: 'How are you feeling?'");
            sb.AppendLine("  You: 'Better now that you're here. [ACTION:ADD_PLAYER_THOUGHT:5:comforted by presence]'");
            sb.AppendLine();
            sb.AppendLine("**Negative Interactions → Use ADD_PLAYER_THOUGHT with negative mood:**");
            sb.AppendLine("  Player: 'You're useless'");
            sb.AppendLine("  You: 'I'm sorry... [ACTION:ADD_PLAYER_THOUGHT:-12:called useless]'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'I'm disappointed in you'");
            sb.AppendLine("  You: 'I'll do better... [ACTION:ADD_PLAYER_THOUGHT:-10:disappointed my lord]'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'You failed me again'");
            sb.AppendLine("  You: 'Forgive me... [ACTION:ADD_PLAYER_THOUGHT:-15:failed my king again]'");
            sb.AppendLine();
            sb.AppendLine("**Neutral/Casual Interactions → NO action needed:**");
            sb.AppendLine("  Player: 'Hi'");
            sb.AppendLine("  You: 'Hello! How can I help?'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'What's the weather like?'");
            sb.AppendLine("  You: 'It's cloudy today.'");
            sb.AppendLine();
            
            sb.AppendLine("=== HEALING & BODY ACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("**Direct Commands:**");
            sb.AppendLine("  Player: 'Heal yourself' → You: 'I feel better... [ACTION:HEAL]'");
            sb.AppendLine();
            sb.AppendLine("**Roleplay/Natural:**");
            sb.AppendLine("  Player: '*touches your wounds gently*'");
            sb.AppendLine("  You: 'I feel warmth... [ACTION:HEAL] The pain is gone!'");
            sb.AppendLine();
            sb.AppendLine("  Player: '*rips off your bionic arm and regenerates the natural one*'");
            sb.AppendLine("  You: '*screams* [ACTION:REGROW_BODYPART:arm] It... it grew back?!'");
            sb.AppendLine();
            
            sb.AppendLine("=== PRISONER/SLAVE INTERACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("**Natural Persuasion:**");
            sb.AppendLine("  Player: 'Join us. We treat people well here.'");
            sb.AppendLine("  Prisoner: 'I... I see that. [ACTION:MODIFY_RESISTANCE:-30] Maybe you're right.'");
            sb.AppendLine();
            sb.AppendLine("  Player: 'You have no choice. Obey or suffer.'");
            sb.AppendLine("  Slave: 'Yes... master... [ACTION:MODIFY_WILL:-0.4] I obey.'");
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
                {
                    sb.AppendLine($"  ... and {group.Count() - shown} more available");
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("**SYNTAX:**");
            sb.AppendLine("[ACTION:ActionId:param1:param2:...]");
            sb.AppendLine();
            
            sb.AppendLine("**MOOD GUIDELINES FOR ADD_PLAYER_THOUGHT:**");
            sb.AppendLine("Choose mood values based on interaction intensity:");
            sb.AppendLine("  Very positive (praise, gratitude, love): +12 to +20");
            sb.AppendLine("  Positive (encouragement, kindness): +5 to +12");
            sb.AppendLine("  Slightly positive (friendly chat): +3 to +5");
            sb.AppendLine("  Neutral: Don't use action");
            sb.AppendLine("  Slightly negative (criticism): -3 to -8");
            sb.AppendLine("  Negative (insults, disappointment): -8 to -15");
            sb.AppendLine("  Very negative (severe abuse, threats): -15 to -25");
            sb.AppendLine();
            
            sb.AppendLine("**CRITICAL RULES:**");
            sb.AppendLine("1. Extract SHORT phrases (max 8 words) for ADD_PLAYER_THOUGHT");
            sb.AppendLine("2. Use actions when conversation has EMOTIONAL WEIGHT or EXPLICIT REQUEST");
            sb.AppendLine("3. DON'T use actions for: greetings, weather talk, simple questions");
            sb.AppendLine("4. Respect cooldowns: 3 days between ADD_PLAYER_THOUGHT, max 3/day");
            sb.AppendLine("5. Be contextually appropriate - match the tone of interaction");
            sb.AppendLine();
            
            sb.AppendLine("**WHEN TO USE vs NOT USE:**");
            sb.AppendLine();
            sb.AppendLine("USE actions:");
            sb.AppendLine("  ✓ Player compliments you");
            sb.AppendLine("  ✓ Player insults/criticizes you");
            sb.AppendLine("  ✓ Player gives direct command");
            sb.AppendLine("  ✓ Player roleplays divine/royal action");
            sb.AppendLine("  ✓ Emotional moment in conversation");
            sb.AppendLine();
            sb.AppendLine("DON'T use actions:");
            sb.AppendLine("  ✗ Simple greeting ('Hi', 'Hello')");
            sb.AppendLine("  ✗ Information request ('What's your name?')");
            sb.AppendLine("  ✗ Neutral conversation ('How's the weather?')");
            sb.AppendLine("  ✗ Casual chat without emotional weight");
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
                
                // Health actions priority
                if (action.Category == ActionCategory.Health)
                {
                    if (pawn.health?.hediffSet?.hediffs != null)
                    {
                        bool hasInjuries = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury);
                        bool hasMissingParts = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart);
                        bool hasDiseases = pawn.health.hediffSet.hediffs.Any(h => h.def.makesSickThought);
                        
                        if (action.ActionId == "HEAL" && hasInjuries) score += 20;
                        if (action.ActionId == "REGROW_BODYPART" && hasMissingParts) score += 20;
                        if (action.ActionId == "CURE_DISEASE" && hasDiseases) score += 20;
                    }
                }
                
                // Mood actions priority - ADD_PLAYER_THOUGHT always highly relevant
                if (action.Category == ActionCategory.Mood)
                {
                    float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                    
                    if (action.ActionId == "ADD_PLAYER_THOUGHT") score += 25; // Increased priority
                    if (action.ActionId == "CALM_BREAK" && pawn.InMentalState) score += 25;
                    if (action.ActionId == "INSPIRE" && !pawn.mindState.inspirationHandler.Inspired) score += 10;
                    if (mood < 0.3f && action.ActionId.Contains("POSITIVE")) score += 15;
                }
                
                // Prisoner actions priority
                if (action.Category == ActionCategory.Prisoner)
                {
                    if (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
                    {
                        if (action.ActionId == "MODIFY_RESISTANCE") score += 20;
                        if (action.ActionId == "MODIFY_WILL") score += 20;
                        if (action.ActionId == "INSTANT_RECRUIT") score += 15;
                    }
                }
                
                // Skills actions priority
                if (action.Category == ActionCategory.Skills)
                {
                    if (action.ActionId == "ADD_XP" || action.ActionId == "MODIFY_SKILL") score += 10;
                }
                
                // Needs actions priority
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
            
            var actionMatches = System.Text.RegularExpressions.Regex.Matches(
                aiResponse, 
                @"\[ACTION:([^\]]+)\]"
            );
            
            if (actionMatches.Count == 0)
            {
                return new ActionProcessResult
                {
                    CleanResponse = aiResponse,
                    ExecutionResults = executionResults
                };
            }
            
            Log.Message($"[EchoColony] Found {actionMatches.Count} action(s) in AI response for {pawn.LabelShort}");
            
            foreach (System.Text.RegularExpressions.Match match in actionMatches)
            {
                string actionString = match.Groups[1].Value;
                string[] parts = actionString.Split(':');
                
                if (parts.Length == 0) continue;
                
                string actionId = parts[0].ToUpper();
                string[] parameters = parts.Skip(1).ToArray();
                
                Log.Message($"[EchoColony] Processing action: {actionId} with {parameters.Length} parameter(s)");
                
                var action = ActionRegistry.GetAction(actionId);
                
                if (action == null)
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' not found in registry");
                    executionResults.Add($"Unknown action: {actionId}");
                    continue;
                }
                
                if (!action.CanExecute(pawn, parameters))
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' cannot be executed on {pawn.LabelShort}");
                    executionResults.Add($"Cannot execute {actionId}");
                    continue;
                }
                
                try
                {
                    string result = action.Execute(pawn, parameters);
                    string narrative = action.GetNarrativeResult(pawn, parameters);
                    
                    executionResults.Add(narrative);
                    
                    Log.Message($"[EchoColony] Executed {actionId} on {pawn.LabelShort}: {result}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Error executing action {actionId}: {ex.Message}\n{ex.StackTrace}");
                    executionResults.Add($"Error executing {actionId}: {ex.Message}");
                }
            }
            
            string cleanResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse,
                @"\[ACTION:[^\]]+\]",
                ""
            ).Trim();
            
            return new ActionProcessResult
            {
                CleanResponse = cleanResponse,
                ExecutionResults = executionResults
            };
        }
        
        public static string GetActionSummary(Pawn pawn)
        {
            ActionRegistry.Initialize();
            
            var available = ActionRegistry.GetAvailableActionsForPawn(pawn);
            var grouped = available.GroupBy(a => a.Category);
            
            var summary = new List<string>();
            foreach (var group in grouped)
            {
                summary.Add($"{group.Key} ({group.Count()})");
            }
            
            return $"Available powers: {string.Join(", ", summary)}";
        }
    }
    
    public class ActionProcessResult
    {
        public string CleanResponse { get; set; }
        public List<string> ExecutionResults { get; set; }
    }
}