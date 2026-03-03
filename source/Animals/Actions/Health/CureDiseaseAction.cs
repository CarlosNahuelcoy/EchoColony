using Verse;
using System.Linq;

namespace EchoColony.Animals.Actions
{
    public class CureDiseaseAction : BaseAnimalAction
    {
        public override string ActionName => "CURE_DISEASE";
        public override string Description => "Cure diseases and infections";
        public override int CooldownTicks => 60000; // ~1 in-game day

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            var diseases = animal.health?.hediffSet?.hediffs?
                .Where(h => h.def.makesSickThought || h.def.tendable)
                .ToList();

            return diseases != null && diseases.Any();
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                var diseases = animal.health.hediffSet.hediffs
                    .Where(h => h.def.makesSickThought || h.def.tendable)
                    .ToList();

                if (!diseases.Any())
                    return false;

                int cured = 0;
                foreach (var disease in diseases)
                {
                    // Cure infections and diseases
                    if (disease.def.lethalSeverity > 0 || disease.TendableNow())
                    {
                        animal.health.RemoveHediff(disease);
                        cured++;
                    }
                }

                LogAction(animal, $"Cured {cured} diseases/infections");
                return cured > 0;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error curing diseases on {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}