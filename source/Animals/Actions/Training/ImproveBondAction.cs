using RimWorld;
using Verse;

namespace EchoColony.Animals.Actions
{
    public class ImproveBondAction : BaseAnimalAction
    {
        public override string ActionName => "IMPROVE_BOND";
        public override string Description => "Strengthen bond with master";
        public override int CooldownTicks => 30000; // ~12 in-game hours

        public override bool CanExecute(Pawn animal)
        {
            if (!base.CanExecute(animal))
                return false;

            // Must have a master or be bonded
            return animal.playerSettings?.Master != null || 
                   animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) != null;
        }

        public override bool Execute(Pawn animal)
        {
            try
            {
                Pawn master = animal.playerSettings?.Master;
                Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);

                Pawn target = bondedTo ?? master;

                if (target == null)
                    return false;

                // If not bonded yet, create bond
                if (bondedTo == null && master != null)
                {
                    animal.relations.AddDirectRelation(PawnRelationDefOf.Bond, master);
                    LogAction(animal, $"Created bond with {master.LabelShort}");
                    return true;
                }

                // If already bonded, improve opinion
                if (bondedTo != null && animal.relations != null)
                {
                    // Add positive interaction memory
                    animal.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.Nuzzled);
                    LogAction(animal, $"Strengthened bond with {bondedTo.LabelShort}");
                    return true;
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error improving bond for {animal.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}