using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using Verse;

namespace EchoColony.Mechs.Actions
{
    public class MechActionExecutionResult
    {
        public List<string> ExecutedActions = new List<string>();
        public List<string> FailedActions = new List<string>();
        public string CleanResponse;
        public string NarrativeFeedback;

        public bool HasActions => ExecutedActions.Count > 0;
    }

    public static class MechActionParser
    {
        private static Dictionary<string, int> actionCooldowns = new Dictionary<string, int>();

        public static MechActionExecutionResult ParseAndExecuteActions(Pawn mech, string response)
        {
            var result = new MechActionExecutionResult
            {
                CleanResponse = response
            };

            if (!MyMod.Settings.enableDivineActions)
                return result;

            if (mech == null || string.IsNullOrEmpty(response))
                return result;

            var pattern = @"\[ACTION:([A-Z_]+)\]";
            var matches = Regex.Matches(response, pattern);

            if (matches.Count == 0)
                return result;

            var narratives = new List<string>();

            foreach (Match match in matches)
            {
                string actionName = match.Groups[1].Value;
                
                if (IsOnCooldown(mech, actionName))
                {
                    Log.Message($"[EchoColony] Mech action {actionName} on cooldown for {mech.LabelShort}");
                    continue;
                }

                var action = MechActionRegistry.CreateAction(actionName);
                
                if (action == null)
                {
                    Log.Warning($"[EchoColony] Unknown mech action: {actionName}");
                    continue;
                }

                if (!action.CanExecute(mech))
                {
                    Log.Message($"[EchoColony] Cannot execute mech action {actionName} on {mech.LabelShort}");
                    continue;
                }

                if (action.Execute(mech))
                {
                    result.ExecutedActions.Add(actionName);
                    narratives.Add(GetNarrativeForAction(mech, actionName));
                    SetCooldown(mech, actionName, action.CooldownTicks);
                    Log.Message($"[EchoColony] ✓ Executed mech action {actionName} on {mech.LabelShort}"); // IMPROVED
                }
                else
                {
                    Log.Warning($"[EchoColony] ✗ Failed to execute mech action {actionName} on {mech.LabelShort}"); // NEW
                    result.FailedActions.Add(actionName); // Track failures
                }
            }

            // Remove action tags from response
            result.CleanResponse = Regex.Replace(response, pattern, "").Trim();

            // Build narrative feedback
            if (narratives.Count > 0)
            {
                result.NarrativeFeedback = string.Join(" ", narratives);
            }

            return result;
        }

        private static string GetNarrativeForAction(Pawn mech, string actionName)
        {
            string name = mech.LabelShort;
            
            switch (actionName)
            {
                case "RECHARGE":
                    return GetRandomNarrative(new[]
                    {
                        $">> POWER CELLS CHARGING... {name}'s systems hum as energy flows through circuits.",
                        $"{name} connects to a power source. Battery levels rising rapidly.",
                        $"Energy surge detected. {name}'s power core glows brighter.",
                        $">> RECHARGE SEQUENCE COMPLETE. {name} is fully energized."
                    });

                case "REPAIR":
                    return GetRandomNarrative(new[]
                    {
                        $">> SELF-REPAIR PROTOCOLS ACTIVE. {name}'s damaged components begin to mend.",
                        $"Nanobots swarm over {name}'s damaged areas, reconstructing broken systems.",
                        $"{name}'s auto-repair systems engage. Metal plates shift and weld themselves.",
                        $">> REPAIRS COMPLETE. {name} is back to optimal functionality."
                    });

                case "SELF_DESTRUCT":
                    return GetRandomNarrative(new[]
                    {
                        $">> SELF-DESTRUCT SEQUENCE INITIATED. {name} begins critical countdown...",
                        $"{name}'s core reactor overloads. A blinding flash erupts!",
                        $">> WARNING: CORE BREACH IMMINENT. {name} explodes in a devastating blast!",
                        $"Circuits overload. {name} detonates in a final act of duty."
                    });

                default:
                    return $">> SYSTEM UPDATE: {name} executes divine protocol.";
            }
        }

        private static string GetRandomNarrative(string[] narratives)
        {
            return narratives[UnityEngine.Random.Range(0, narratives.Length)];
        }

        private static bool IsOnCooldown(Pawn mech, string actionName)
        {
            string key = GetCooldownKey(mech, actionName);
            
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

        private static void SetCooldown(Pawn mech, string actionName, int ticks)
        {
            string key = GetCooldownKey(mech, actionName);
            int cooldownEndTick = Find.TickManager.TicksGame + ticks;
            actionCooldowns[key] = cooldownEndTick;
        }

        private static string GetCooldownKey(Pawn mech, string actionName)
        {
            return $"{mech.ThingID}_{actionName}";
        }

        // NUEVO: Obtener tiempo restante de cooldown
        public static int GetCooldownRemaining(Pawn mech, string actionName)
        {
            string key = GetCooldownKey(mech, actionName);
            
            if (!actionCooldowns.ContainsKey(key))
                return 0;

            int cooldownEndTick = actionCooldowns[key];
            int currentTick = Find.TickManager.TicksGame;
            int remaining = cooldownEndTick - currentTick;

            return remaining > 0 ? remaining : 0;
        }

        // NUEVO: Obtener tiempo restante en formato legible
        public static string GetCooldownRemainingFormatted(Pawn mech, string actionName)
        {
            int ticksRemaining = GetCooldownRemaining(mech, actionName);
            
            if (ticksRemaining <= 0)
                return "AVAILABLE";

            // Convertir ticks a horas
            int hours = ticksRemaining / 2500;
            
            if (hours < 1)
                return "< 1 hour";
            else if (hours == 1)
                return "1 hour";
            else if (hours < 24)
                return $"{hours} hours";
            else
            {
                int days = hours / 24;
                int remainingHours = hours % 24;
                if (remainingHours == 0)
                    return $"{days} day{(days > 1 ? "s" : "")}";
                else
                    return $"{days}d {remainingHours}h";
            }
        }

        // NUEVO: Obtener info de todos los cooldowns activos
        public static string GetAllCooldownsInfo(Pawn mech)
        {
            var sb = new StringBuilder();
            
            var actions = new[] { "RECHARGE", "REPAIR", "SELF_DESTRUCT" };
            
            foreach (var actionName in actions)
            {
                int remaining = GetCooldownRemaining(mech, actionName);
                
                if (remaining > 0)
                {
                    string formatted = GetCooldownRemainingFormatted(mech, actionName);
                    sb.AppendLine($"- {actionName}: {formatted}");
                }
            }

            return sb.ToString();
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