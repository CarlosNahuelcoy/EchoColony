using System;
using System.Collections.Generic;
using Verse;

namespace EchoColony.Animals.Actions
{
    public static class AnimalActionRegistry
    {
        private static Dictionary<string, Type> registeredActions = new Dictionary<string, Type>();
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            Log.Message("[EchoColony] Initializing Animal Action Registry...");

            // Health Actions
            Register("HEAL", typeof(HealAnimalAction));
            Register("REMOVE_PAIN", typeof(RemovePainAction));
            Register("CURE_DISEASE", typeof(CureDiseaseAction));

            // Training Actions
            Register("LEARN_SKILL", typeof(LearnSkillAction));
            Register("IMPROVE_BOND", typeof(ImproveBondAction));
            Register("TAME", typeof(TameAction));

            // Needs Actions
            Register("FEED", typeof(FeedAnimalAction));
            Register("REST", typeof(RestAnimalAction));
            Register("COMFORT", typeof(ComfortAnimalAction));

            initialized = true;
            Log.Message($"[EchoColony] Registered {registeredActions.Count} animal actions");
        }

        private static void Register(string actionName, Type actionType)
        {
            if (!typeof(IAnimalAction).IsAssignableFrom(actionType))
            {
                Log.Error($"[EchoColony] Cannot register {actionType.Name} - does not implement IAnimalAction");
                return;
            }

            registeredActions[actionName.ToUpper()] = actionType;
        }

        public static IAnimalAction CreateAction(string actionName)
        {
            string upperName = actionName.ToUpper();
            
            if (registeredActions.TryGetValue(upperName, out Type actionType))
            {
                try
                {
                    return (IAnimalAction)Activator.CreateInstance(actionType);
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Failed to create animal action {actionName}: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        public static bool IsActionRegistered(string actionName)
        {
            return registeredActions.ContainsKey(actionName.ToUpper());
        }

        public static List<string> GetAllActionNames()
        {
            return new List<string>(registeredActions.Keys);
        }

        public static string GetAvailableActionsPrompt()
        {
            if (!MyMod.Settings.enableDivineActions)
                return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Available Divine Actions");
            sb.AppendLine("You can trigger real in-game effects by including action tags in your response.");
            sb.AppendLine("Format: [ACTION:ACTION_NAME]");
            sb.AppendLine();
            sb.AppendLine("Available actions:");
            sb.AppendLine();
            
            sb.AppendLine("HEALTH:");
            sb.AppendLine("- [ACTION:HEAL] - Heal injuries and wounds");
            sb.AppendLine("- [ACTION:REMOVE_PAIN] - Remove pain from injuries");
            sb.AppendLine("- [ACTION:CURE_DISEASE] - Cure diseases and infections");
            sb.AppendLine();
            
            sb.AppendLine("TRAINING:");
            sb.AppendLine("- [ACTION:LEARN_SKILL] - Learn or improve a training skill");
            sb.AppendLine("- [ACTION:IMPROVE_BOND] - Strengthen bond with master");
            sb.AppendLine("- [ACTION:TAME] - Become more domesticated");
            sb.AppendLine();
            
            sb.AppendLine("NEEDS:");
            sb.AppendLine("- [ACTION:FEED] - Restore food need");
            sb.AppendLine("- [ACTION:REST] - Restore rest need");
            sb.AppendLine("- [ACTION:COMFORT] - Improve mood/comfort");
            sb.AppendLine();
            
            sb.AppendLine("IMPORTANT RULES:");
            sb.AppendLine("- Use actions sparingly and only when narratively appropriate");
            sb.AppendLine("- Don't use multiple major actions in one response");
            sb.AppendLine("- Actions have cooldowns to prevent abuse");
            sb.AppendLine("- The action tags will be hidden from the player");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}