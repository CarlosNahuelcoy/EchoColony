using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using Verse;

namespace EchoColony.Animals.Actions
{
    public class ActionExecutionResult
    {
        public List<string> ExecutedActions = new List<string>();
        public List<string> FailedActions = new List<string>();
        public string CleanResponse;
        public string NarrativeFeedback;

        public bool HasActions => ExecutedActions.Count > 0;
    }

    public static class AnimalActionParser
    {
        private static Dictionary<string, int> actionCooldowns = new Dictionary<string, int>();

        public static ActionExecutionResult ParseAndExecuteActions(Pawn animal, string response)
{
    var result = new ActionExecutionResult();

    // SIEMPRE limpiar los tags, independientemente de si las acciones están activas
    var pattern = @"\[ACTION:[^\]]*\]";
    result.CleanResponse = Regex.Replace(response, pattern, "")
                                .Replace("\n\n\n", "\n\n") // Eliminar líneas vacías extra
                                .Trim();

    // Solo ejecutar si las acciones están habilitadas
    if (!MyMod.Settings.enableDivineActions || animal == null || string.IsNullOrEmpty(response))
        return result;

    var matches = Regex.Matches(response, @"\[ACTION:([A-Z_]+)\]");
    if (matches.Count == 0)
        return result;

    var narratives = new List<string>();

    foreach (Match match in matches)
    {
        string actionName = match.Groups[1].Value;

        if (IsOnCooldown(animal, actionName)) continue;

        var action = AnimalActionRegistry.CreateAction(actionName);
        if (action == null || !action.CanExecute(animal)) continue;

        if (action.Execute(animal))
        {
            result.ExecutedActions.Add(actionName);
            narratives.Add(GetNarrativeForAction(animal, actionName));
            SetCooldown(animal, actionName, action.CooldownTicks);
        }
    }

    if (narratives.Count > 0)
        result.NarrativeFeedback = string.Join(" ", narratives);

    return result;
}
        private static string GetNarrativeForAction(Pawn animal, string actionName)
{
    string name = animal.LabelShort;
    string[] narratives;
    
    switch (actionName)
    {
        case "HEAL":
            narratives = new[]
            {
                "EchoColony.ActionHealNarrative1",
                "EchoColony.ActionHealNarrative2",
                "EchoColony.ActionHealNarrative3",
                "EchoColony.ActionHealNarrative4"
            };
            break;

        case "REMOVE_PAIN":
            narratives = new[]
            {
                "EchoColony.ActionRemovePainNarrative1",
                "EchoColony.ActionRemovePainNarrative2",
                "EchoColony.ActionRemovePainNarrative3",
                "EchoColony.ActionRemovePainNarrative4"
            };
            break;

        case "CURE_DISEASE":
            narratives = new[]
            {
                "EchoColony.ActionCureDiseaseNarrative1",
                "EchoColony.ActionCureDiseaseNarrative2",
                "EchoColony.ActionCureDiseaseNarrative3",
                "EchoColony.ActionCureDiseaseNarrative4"
            };
            break;

        case "LEARN_SKILL":
            narratives = new[]
            {
                "EchoColony.ActionLearnSkillNarrative1",
                "EchoColony.ActionLearnSkillNarrative2",
                "EchoColony.ActionLearnSkillNarrative3",
                "EchoColony.ActionLearnSkillNarrative4"
            };
            break;

        case "IMPROVE_BOND":
            narratives = new[]
            {
                "EchoColony.ActionImproveBondNarrative1",
                "EchoColony.ActionImproveBondNarrative2",
                "EchoColony.ActionImproveBondNarrative3",
                "EchoColony.ActionImproveBondNarrative4"
            };
            break;

        case "TAME":
            narratives = new[]
            {
                "EchoColony.ActionTameNarrative1",
                "EchoColony.ActionTameNarrative2",
                "EchoColony.ActionTameNarrative3",
                "EchoColony.ActionTameNarrative4"
            };
            break;

        case "FEED":
            narratives = new[]
            {
                "EchoColony.ActionFeedNarrative1",
                "EchoColony.ActionFeedNarrative2",
                "EchoColony.ActionFeedNarrative3",
                "EchoColony.ActionFeedNarrative4"
            };
            break;

        case "REST":
            narratives = new[]
            {
                "EchoColony.ActionRestNarrative1",
                "EchoColony.ActionRestNarrative2",
                "EchoColony.ActionRestNarrative3",
                "EchoColony.ActionRestNarrative4"
            };
            break;

        case "COMFORT":
            narratives = new[]
            {
                "EchoColony.ActionComfortNarrative1",
                "EchoColony.ActionComfortNarrative2",
                "EchoColony.ActionComfortNarrative3",
                "EchoColony.ActionComfortNarrative4"
            };
            break;

        default:
            return "EchoColony.ActionGenericNarrative".Translate(name);
    }
    
    string selectedKey = narratives[UnityEngine.Random.Range(0, narratives.Length)];
    return selectedKey.Translate(name);
}
        private static string GetRandomNarrative(string[] narratives)
        {
            return narratives[UnityEngine.Random.Range(0, narratives.Length)];
        }

        private static bool IsOnCooldown(Pawn animal, string actionName)
        {
            string key = GetCooldownKey(animal, actionName);
            
            if (!actionCooldowns.ContainsKey(key))
                return false;

            int cooldownEndTick = actionCooldowns[key];
            int currentTick = Find.TickManager.TicksGame;

            if (currentTick >= cooldownEndTick)
            {
                actionCooldowns.Remove(key);
                return false;
            }

            return true;
        }

        private static void SetCooldown(Pawn animal, string actionName, int ticks)
        {
            string key = GetCooldownKey(animal, actionName);
            int cooldownEndTick = Find.TickManager.TicksGame + ticks;
            actionCooldowns[key] = cooldownEndTick;
        }

        private static string GetCooldownKey(Pawn animal, string actionName)
        {
            return $"{animal.ThingID}_{actionName}";
        }

        public static void CleanupOldCooldowns()
        {
            int currentTick = Find.TickManager.TicksGame;
            var toRemove = new List<string>();

            foreach (var kvp in actionCooldowns)
            {
                if (currentTick >= kvp.Value)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                actionCooldowns.Remove(key);
            }
        }
    }
}