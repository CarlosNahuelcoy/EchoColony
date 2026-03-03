using Verse;
using System.Linq;

namespace EchoColony.Animals.Actions
{
    public class HealAnimalAction : BaseAnimalAction
    {
        public override string ActionName => "HEAL";
        public override string Description => "Heal injuries and wounds";
        public override int CooldownTicks => 30000; // ~12 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            // Check if animal has any injuries
            var injuries = animal.health?.hediffSet?.hediffs?
                .OfType<Hediff_Injury>()
                .Where(h => !h.IsPermanent())
                .ToList();

            return injuries != null && injuries.Any();
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                var injuries = animal.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(h => !h.IsPermanent())
                    .OrderByDescending(h => h.Severity)
                    .ToList();

                if (!injuries.Any())
                {
                    LogAction(animal, "No injuries to heal");
                    return false;
                }

                // Heal the most severe injury
                var targetInjury = injuries.First();
                float healAmount = targetInjury.Severity * 0.5f; // Heal 50% of the injury

                targetInjury.Heal(healAmount);

                LogAction(animal, $"Healed {targetInjury.def.label} by {healAmount:F1}");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error healing {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}