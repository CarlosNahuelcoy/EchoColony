using Verse;

namespace EchoColony.Animals.Actions
{
    public class FeedAnimalAction : BaseAnimalAction
    {
        public override string ActionName => "FEED";
        public override string Description => "Restore food need";
        public override int CooldownTicks => 20000; // ~8 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            var foodNeed = animal.needs?.food;
            return foodNeed != null && foodNeed.CurLevel < 0.99f;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                var foodNeed = animal.needs?.food;
                
                if (foodNeed == null)
                    return false;

                // Restore 50% of food
                float restoreAmount = foodNeed.MaxLevel * 0.5f;
                foodNeed.CurLevel = System.Math.Min(foodNeed.MaxLevel, foodNeed.CurLevel + restoreAmount);

                LogAction(animal, $"Fed - restored {restoreAmount:F1} food");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error feeding {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}