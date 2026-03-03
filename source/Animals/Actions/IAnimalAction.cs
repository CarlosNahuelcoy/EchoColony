using Verse;

namespace EchoColony.Animals.Actions
{
    public interface IAnimalAction
    {
        string ActionName { get; }
        string Description { get; }
        bool CanExecute(Pawn animal);
        bool Execute(Pawn animal);
        int CooldownTicks { get; }
    }
}