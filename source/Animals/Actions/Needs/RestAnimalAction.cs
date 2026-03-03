using Verse;

namespace EchoColony.Animals.Actions
{
    public class RestAnimalAction : BaseAnimalAction
    {
        public override string ActionName => "REST";
        public override string Description => "Restore rest need";
        public override int CooldownTicks => 25000; // ~10 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            var restNeed = animal.needs?.rest;
            return restNeed != null && restNeed.CurLevel < 0.8f;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                var restNeed = animal.needs?.rest;
                
                if (restNeed == null)
                    return false;

                // Restore 60% of rest
                float restoreAmount = restNeed.MaxLevel * 0.6f;
                restNeed.CurLevel = System.Math.Min(restNeed.MaxLevel, restNeed.CurLevel + restoreAmount);

                LogAction(animal, $"Rested - restored {restoreAmount:F1} rest");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error resting {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}