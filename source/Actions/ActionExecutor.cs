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
            sb.AppendLine("1. When the player EXPLICITLY requests something ('heal yourself', 'grow back your arm')");
            sb.AppendLine("2. When the conversation reaches a GENUINE EMOTIONAL PEAK — not mere politeness.");
            sb.AppendLine();
            sb.AppendLine("The [ACTION:...] tag makes things REALLY happen in the game. Use it sparingly.");
            sb.AppendLine();

            sb.AppendLine("=== CONTEXTUAL ACTION USAGE ===");
            sb.AppendLine();
            sb.AppendLine("**ADD_PLAYER_THOUGHT requires a MEANINGFUL EXCHANGE — not a single message.**");
            sb.AppendLine("Before using it, ask yourself: 'Has something emotionally significant actually happened?'");
            sb.AppendLine("If the answer is not a clear YES — do NOT use the action.");
            sb.AppendLine();
            sb.AppendLine("**YES — action is appropriate:**");
            sb.AppendLine("  - Player expresses genuine personal gratitude after a crisis or sustained effort.");
            sb.AppendLine("    'You saved everything today. I'm truly grateful for what you did.'");
            sb.AppendLine("    → [ACTION:ADD_PLAYER_THOUGHT:12:player's heartfelt gratitude]");
            sb.AppendLine();
            sb.AppendLine("  - Player delivers a deliberate, personal insult or cruel accusation.");
            sb.AppendLine("    'You're worthless and I regret ever bringing you here.'");
            sb.AppendLine("    → [ACTION:ADD_PLAYER_THOUGHT:-14:called worthless by the player]");
            sb.AppendLine();
            sb.AppendLine("  - A long, emotionally invested conversation reaches a turning point of genuine");
            sb.AppendLine("    comfort, support, or distress — after multiple meaningful exchanges.");
            sb.AppendLine();
            sb.AppendLine("**NO — do NOT use ADD_PLAYER_THOUGHT for any of these:**");
            sb.AppendLine("  - Greetings of any kind: 'Hi', 'Hello', 'Hey', 'Good morning', 'How are you'");
            sb.AppendLine("  - Small talk: weather, what they're doing, what they ate, casual questions");
            sb.AppendLine("  - Routine compliments: 'Nice work', 'Thanks', 'Good job', 'Well done'");
            sb.AppendLine("  - Simple questions and answers with no emotional weight");
            sb.AppendLine("  - The player's very first message in a conversation");
            sb.AppendLine("  - Any exchange shorter than 3 genuine back-and-forth messages");
            sb.AppendLine("  - Anything you would say to a stranger on the street");
            sb.AppendLine();

            sb.AppendLine("=== HEALING & BODY ACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("  Player: 'Heal yourself' → You: 'I feel better... [ACTION:HEAL]'");
            sb.AppendLine("  Player: '*touches your wounds gently*' → You: 'The pain fades... [ACTION:HEAL]'");
            sb.AppendLine("  Player: '*regenerates your arm*' → You: '[ACTION:REGROW_BODYPART:arm] It grew back?!'");
            sb.AppendLine();

            sb.AppendLine("=== PRISONER/SLAVE INTERACTIONS ===");
            sb.AppendLine();
            sb.AppendLine("Resistance and will change through SUSTAINED conversation — not a single exchange.");
            sb.AppendLine("Good treatment, promises, shared ideology over multiple messages → reduce resistance.");
            sb.AppendLine("Threats, cruelty, hopelessness expressed over time → increase resistance or reduce will.");
            sb.AppendLine();
            sb.AppendLine("  Player makes a compelling case over several messages for joining:");
            sb.AppendLine("  Prisoner: 'I... maybe. [ACTION:MODIFY_RESISTANCE:-20] I'll think about it.'");
            sb.AppendLine();
            sb.AppendLine("  Player uses sustained intimidation or cruelty:");
            sb.AppendLine("  Slave: 'Yes... master... [ACTION:MODIFY_WILL:-0.3] I obey.'");
            sb.AppendLine();
            sb.AppendLine("  Player invokes shared faith across a deep conversation:");
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
            sb.AppendLine("  Emotional support, genuine praise, real insults → ADD_PLAYER_THOUGHT (only if earned)");
            sb.AppendLine("  Example: 'Here is some bread' → [ACTION:MODIFY_NEED:Food:0.4]");
            sb.AppendLine();

            sb.AppendLine("**MOOD GUIDELINES FOR ADD_PLAYER_THOUGHT:**");
            sb.AppendLine("  Very positive (profound gratitude, deeply personal praise): +12 to +20");
            sb.AppendLine("  Positive (genuine encouragement after difficulty, real kindness): +6 to +12");
            sb.AppendLine("  Neutral or mildly positive chat: NO action — this is the default for most conversations");
            sb.AppendLine("  Slightly negative (pointed criticism, dismissal): -4 to -8");
            sb.AppendLine("  Negative (deliberate insults, expressions of disappointment): -8 to -15");
            sb.AppendLine("  Very negative (severe abuse, threats, cruelty): -15 to -25");
            sb.AppendLine();
            sb.AppendLine("  NOTE: The vast majority of conversations should produce NO mood action.");
            sb.AppendLine("  A normal friendly chat is not an event. Only use this for moments that");
            sb.AppendLine("  would genuinely linger in someone's memory for days.");
            sb.AppendLine();

            sb.AppendLine("**CRITICAL RULES:**");
            sb.AppendLine("1. Short phrases only (max 8 words) for ADD_PLAYER_THOUGHT label");
            sb.AppendLine("2. ADD_PLAYER_THOUGHT has a 3-day cooldown — treat it like a rare resource");
            sb.AppendLine("3. NEVER use ADD_PLAYER_THOUGHT on the first reply of a conversation");
            sb.AppendLine("4. Match action intensity to the actual weight of the exchange, not its surface tone");
            sb.AppendLine("5. When in doubt — don't. Silence is better than a cheap mood boost.");
            sb.AppendLine();

            sb.AppendLine("REMEMBER: Actions have REAL consequences in the game. Use them for moments that matter.");

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
                        bool hasInjuries     = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury);
                        bool hasMissingParts = pawn.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart);
                        bool hasDiseases     = pawn.health.hediffSet.hediffs.Any(h => h.def.makesSickThought);

                        if (action.ActionId == "HEAL" && hasInjuries)                score += 20;
                        if (action.ActionId == "REGROW_BODYPART" && hasMissingParts) score += 20;
                        if (action.ActionId == "CURE_DISEASE" && hasDiseases)        score += 20;
                    }
                }

                if (action.Category == ActionCategory.Mood)
                {
                    float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;

                    if (action.ActionId == "ADD_PLAYER_THOUGHT")                                        score += 25;
                    if (action.ActionId == "CALM_BREAK" && pawn.InMentalState)                         score += 25;
                    if (action.ActionId == "INSPIRE" && !pawn.mindState.inspirationHandler.Inspired)   score += 10;
                    if (mood < 0.3f && action.ActionId.Contains("POSITIVE"))                           score += 15;
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
                    CleanResponse    = cleanResponse,
                    ExecutionResults = executionResults
                };
            }

            Log.Message($"[EchoColony] Found {actionMatches.Count} action(s) in AI response for {pawn.LabelShort}");

            foreach (System.Text.RegularExpressions.Match match in actionMatches)
            {
                string   actionString = match.Groups[1].Value;
                string[] parts        = actionString.Split(':');

                if (parts.Length == 0) continue;

                string   actionId   = parts[0].ToUpper();
                string[] parameters = parts.Skip(1).ToArray();

                Log.Message($"[EchoColony] Processing action: {actionId} with {parameters.Length} parameter(s)");

                var action = ActionRegistry.GetAction(actionId);

                if (action == null)
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' not found in registry");
                    continue;
                }

                if (!action.CanExecute(pawn, parameters))
                {
                    Log.Warning($"[EchoColony] Action '{actionId}' cannot be executed on {pawn.LabelShort}");
                    continue;
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
        public string       CleanResponse    { get; set; }
        public List<string> ExecutionResults { get; set; }
    }
}