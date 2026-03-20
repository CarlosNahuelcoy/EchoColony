using Verse;
using System.Linq;

namespace EchoColony.Mechs.Actions
{
    public class RepairMechAction : BaseMechAction
    {
        public override string ActionName => "REPAIR";
        public override string Description => "Repair damaged components";
        public override int CooldownTicks => 60000; // ~1 in-game day

        public override bool CanExecute(Pawn mech)
        {
            if (!base.CanExecute(mech))
                return false;

            // Check if mech has injuries/damage
            if (mech.health?.hediffSet?.hediffs == null)
                return false;

            var injuries = mech.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => !h.IsPermanent())
                .ToList();

            return injuries.Any();
        }

        public override bool Execute(Pawn mech)
        {
            try
            {
                if (mech.health?.hediffSet?.hediffs == null)
                {
                    Log.Error($"[EchoColony] Cannot repair {mech.LabelShort} - no health system");
                    return false;
                }

                var injuries = mech.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(h => !h.IsPermanent())
                    .OrderByDescending(h => h.Severity)
                    .ToList();

                if (!injuries.Any())
                {
                    LogAction(mech, "No damage to repair");
                    return false;
                }

                // Repair the most severe injury
                var targetInjury = injuries.First();
                string injuryLabel = targetInjury.def.label;
                string partLabel = targetInjury.Part?.Label ?? "unknown part";
                float severityBefore = targetInjury.Severity;
                
                // Heal 70% of the damage
                float healAmount = targetInjury.Severity * 0.7f;
                targetInjury.Heal(healAmount);

                float severityAfter = targetInjury.Severity;

                LogAction(mech, $"Repaired {injuryLabel} on {partLabel}: {severityBefore:F1} → {severityAfter:F1}");
                
                // Verify healing actually happened
                if (System.Math.Abs(severityBefore - severityAfter) < 0.1f)
                {
                    Log.Warning($"[EchoColony] Repair didn't reduce injury severity for {mech.LabelShort}");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error repairing {mech.LabelShort}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}