using Verse;

namespace EchoColony.Mechs.Actions
{
    public interface IMechAction
    {
        string ActionName { get; }
        string Description { get; }
        bool CanExecute(Pawn mech);
        bool Execute(Pawn mech);
        int CooldownTicks { get; }
    }
}