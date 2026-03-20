using Verse;

namespace EchoColony.Mechs.Actions
{
    public abstract class BaseMechAction : IMechAction
    {
        public abstract string ActionName { get; }
        public abstract string Description { get; }
        public abstract int CooldownTicks { get; }

        public virtual bool CanExecute(Pawn mech)
        {
            if (mech == null || mech.Dead || mech.Destroyed)
                return false;

            if (!MechIntelligenceDetector.IsMechanoid(mech))
                return false;

            return true;
        }

        public abstract bool Execute(Pawn mech);

        protected void LogAction(Pawn mech, string message)
        {
            if (MyMod.Settings?.debugMode == true)
            {
                Log.Message($"[EchoColony] {ActionName} on {mech.LabelShort}: {message}");
            }
        }
    }
}