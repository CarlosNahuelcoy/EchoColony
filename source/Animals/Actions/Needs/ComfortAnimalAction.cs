using Verse;
using RimWorld;

namespace EchoColony.Animals.Actions
{
    public class ComfortAnimalAction : BaseAnimalAction
    {
        public override string ActionName => "COMFORT";
        public override string Description => "Improve mood/comfort";
        public override int CooldownTicks => 35000; // ~14 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            var moodNeed = animal.needs?.mood;
            return moodNeed != null && moodNeed.CurLevel < 0.7f;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                var moodNeed = animal.needs?.mood;
                
                if (moodNeed == null)
                    return false;

                // Add positive thought
                if (animal.needs?.mood?.thoughts?.memories != null)
                {
                    animal.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.Nuzzled);
                }

                // Directly improve mood
                float improvement = 0.2f;
                moodNeed.CurLevel = System.Math.Min(1f, moodNeed.CurLevel + improvement);

                LogAction(animal, $"Comforted - improved mood by {improvement:F1}");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error comforting {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}