using RimWorld;
using Verse;

namespace EchoColony.Animals.Actions
{
    public class TameAction : BaseAnimalAction
    {
        public override string ActionName => "TAME";
        public override string Description => "Become more domesticated";
        public override int CooldownTicks => 120000; // ~2 in-game days

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            // Only for wild or semi-wild animals
            return animal.Faction != Faction.OfPlayer;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                if (animal.Faction == Faction.OfPlayer)
                    return false;

                // Make the animal join the player faction
                animal.SetFaction(Faction.OfPlayer);

                // Ensure it has training
                if (animal.training == null)
                {
                    animal.training = new Pawn_TrainingTracker(animal);
                }

                LogAction(animal, "Tamed and joined colony");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error taming {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}