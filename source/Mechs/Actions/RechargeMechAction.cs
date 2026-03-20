using Verse;
using RimWorld;

namespace EchoColony.Mechs.Actions
{
    public class RechargeMechAction : BaseMechAction
    {
        public override string ActionName => "RECHARGE";
        public override string Description => "Recharge battery to 100%";
        public override int CooldownTicks => 30000; // ~12 in-game hours

        public override bool CanExecute(Pawn mech)
        {
            if (!base.CanExecute(mech))
                return false;

            // Check if mech has energy need using proper RimWorld method
            var energyNeed = mech.needs?.TryGetNeed<Need_MechEnergy>();
            
            if (energyNeed == null)
            {
                Log.Warning($"[EchoColony] {mech.LabelShort} has no energy need");
                return false;
            }

            // Only allow if not fully charged
            return energyNeed.CurLevel < energyNeed.MaxLevel * 0.99f;
        }

        public override bool Execute(Pawn mech)
        {
            try
            {
                // Get energy need using proper RimWorld API
                var energyNeed = mech.needs?.TryGetNeed<Need_MechEnergy>();
                
                if (energyNeed == null)
                {
                    Log.Error($"[EchoColony] Cannot recharge {mech.LabelShort} - no energy need found");
                    return false;
                }

                float beforeRecharge = energyNeed.CurLevel;
                
                // Fully recharge
                energyNeed.CurLevel = energyNeed.MaxLevel;

                float afterRecharge = energyNeed.CurLevel;

                LogAction(mech, $"Recharged from {beforeRecharge:P0} to {afterRecharge:P0}");
                
                // Verify it actually changed
                if (System.Math.Abs(afterRecharge - beforeRecharge) < 0.01f)
                {
                    Log.Warning($"[EchoColony] Recharge didn't change energy level for {mech.LabelShort}");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error recharging {mech.LabelShort}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}