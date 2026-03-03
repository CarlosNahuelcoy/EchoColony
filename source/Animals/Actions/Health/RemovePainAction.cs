using Verse;
using System.Linq;

namespace EchoColony.Animals.Actions
{
    public class RemovePainAction : BaseAnimalAction
    {
        public override string ActionName => "REMOVE_PAIN";
        public override string Description => "Remove pain from injuries";
        public override int CooldownTicks => 45000; // ~18 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            return animal.health?.hediffSet?.PainTotal > 0.1f;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                // Find pain-causing hediffs
                var painHediffs = animal.health.hediffSet.hediffs
                    .Where(h => h.PainOffset > 0.01f)
                    .ToList();

                if (!painHediffs.Any())
                    return false;

                foreach (var hediff in painHediffs)
                {
                    // Reduce severity or remove entirely
                    if (hediff.Severity > 0.5f)
                    {
                        hediff.Severity *= 0.3f; // Reduce to 30%
                    }
                    else
                    {
                        animal.health.RemoveHediff(hediff);
                    }
                }

                LogAction(animal, $"Removed/reduced pain from {painHediffs.Count} sources");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error removing pain from {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}