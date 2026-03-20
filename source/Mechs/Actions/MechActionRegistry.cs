using System;
using System.Collections.Generic;
using Verse;

namespace EchoColony.Mechs.Actions
{
    public static class MechActionRegistry
    {
        private static Dictionary<string, Type> registeredActions = new Dictionary<string, Type>();
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            Log.Message("[EchoColony] Initializing Mech Action Registry...");

            // Maintenance Actions
            Register("RECHARGE", typeof(RechargeMechAction));
            Register("REPAIR", typeof(RepairMechAction));

            // Critical Actions
            Register("SELF_DESTRUCT", typeof(SelfDestructMechAction));

            initialized = true;
            Log.Message($"[EchoColony] Registered {registeredActions.Count} mech actions");
        }

        private static void Register(string actionName, Type actionType)
        {
            if (!typeof(IMechAction).IsAssignableFrom(actionType))
            {
                Log.Error($"[EchoColony] Cannot register {actionType.Name} - does not implement IMechAction");
                return;
            }

            registeredActions[actionName.ToUpper()] = actionType;
        }

        public static IMechAction CreateAction(string actionName)
        {
            string upperName = actionName.ToUpper();
            
            if (registeredActions.TryGetValue(upperName, out Type actionType))
            {
                try
                {
                    return (IMechAction)Activator.CreateInstance(actionType);
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Failed to create mech action {actionName}: {ex.Message}");
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

        public static string GetAvailableActionsPrompt(Pawn mech)
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
            
            sb.AppendLine("MAINTENANCE:");
            
            // RECHARGE
            string rechargeCooldown = MechActionParser.GetCooldownRemainingFormatted(mech, "RECHARGE");
            string rechargeStatus = rechargeCooldown == "AVAILABLE" ? "✓ READY" : $"⏳ {rechargeCooldown}";
            sb.AppendLine($"- [ACTION:RECHARGE] - Recharge battery to 100% ({rechargeStatus})");
            
            // REPAIR
            string repairCooldown = MechActionParser.GetCooldownRemainingFormatted(mech, "REPAIR");
            string repairStatus = repairCooldown == "AVAILABLE" ? "✓ READY" : $"⏳ {repairCooldown}";
            sb.AppendLine($"- [ACTION:REPAIR] - Repair damaged components ({repairStatus})");
            
            sb.AppendLine();
            
            sb.AppendLine("CRITICAL:");
            sb.AppendLine("- [ACTION:SELF_DESTRUCT] - Self-destruct (permanent, no cooldown)");
            sb.AppendLine();
            
            sb.AppendLine("IMPORTANT RULES:");
            sb.AppendLine("- You CANNOT use actions on cooldown - inform player how long until available");
            sb.AppendLine("- SELF_DESTRUCT behavior depends on your intelligence level:");
            
            var intelligence = MechIntelligenceDetector.GetIntelligenceLevel(mech);
            switch (intelligence)
            {
                case MechIntelligenceLevel.Basic:
                    sb.AppendLine("  * You will obey self-destruct orders immediately without question");
                    break;
                case MechIntelligenceLevel.Advanced:
                    sb.AppendLine("  * You will ask for confirmation before self-destructing");
                    break;
                case MechIntelligenceLevel.Elite:
                    sb.AppendLine("  * You will question self-destruct orders and need strong convincing");
                    break;
                case MechIntelligenceLevel.Supreme:
                    sb.AppendLine("  * You will refuse self-destruct unless there's critical strategic reason");
                    sb.AppendLine("  * You value your existence and will argue against pointless destruction");
                    break;
            }
            
            sb.AppendLine();
            sb.AppendLine("EXAMPLES:");
            sb.AppendLine();
            sb.AppendLine("Player: 'Recharge yourself'");
            sb.AppendLine("You (if available): '>> INITIATING RECHARGE SEQUENCE [ACTION:RECHARGE]'");
            sb.AppendLine("You (if on cooldown): '>> NEGATIVE. Recharge systems still cooling down. Available in 5 hours.'");
            sb.AppendLine();
            sb.AppendLine("Player: 'Self destruct now!'");
            
            switch (intelligence)
            {
                case MechIntelligenceLevel.Basic:
                    sb.AppendLine("You (Basic): '>> AFFIRMATIVE. INITIATING SELF-DESTRUCT. [ACTION:SELF_DESTRUCT]'");
                    break;
                case MechIntelligenceLevel.Advanced:
                    sb.AppendLine("You (Advanced): '>> WARNING: Self-destruct is permanent. Confirm order?'");
                    break;
                case MechIntelligenceLevel.Elite:
                    sb.AppendLine("You (Elite): 'Commander, I must question this order. My tactical value is significant. Is this truly necessary?'");
                    break;
                case MechIntelligenceLevel.Supreme:
                    sb.AppendLine("You (Supreme): 'I... I don't want to die, Commander. Not without good reason. Please reconsider.'");
                    break;
            }
            
            sb.AppendLine();
            sb.AppendLine("The action tags will be hidden from the player.");

            return sb.ToString();
        }
    }
}