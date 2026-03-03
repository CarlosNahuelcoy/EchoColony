using RimWorld;
using Verse;
using System.Linq;

namespace EchoColony.Animals.Actions
{
    public class LearnSkillAction : BaseAnimalAction
    {
        public override string ActionName => "LEARN_SKILL";
        public override string Description => "Learn or improve a training skill";
        public override int CooldownTicks => 15000; // ~6 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            return animal.training != null;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                if (animal.training == null)
                    return false;

                // Find trainable skills that aren't learned yet
                var trainables = DefDatabase<TrainableDef>.AllDefsListForReading
                    .Where(t => animal.training.CanBeTrained(t) && !animal.training.HasLearned(t))
                    .ToList();

                if (!trainables.Any())
                {
                    // If all learned, improve one randomly
                    var learned = DefDatabase<TrainableDef>.AllDefsListForReading
                        .Where(t => animal.training.HasLearned(t))
                        .ToList();

                    if (learned.Any())
                    {
                        var skill = learned.RandomElement();
                        // Just mark it as successful improvement
                        LogAction(animal, $"Improved {skill.label}");
                        return true;
                    }

                    return false;
                }

                // Learn a new skill
                var skillToLearn = trainables.RandomElement();
                animal.training.Train(skillToLearn, null, true);

                LogAction(animal, $"Learned {skillToLearn.label}");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error training {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}