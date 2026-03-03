using Verse;

namespace EchoColony.Animals.Actions
{
    public abstract class BaseAnimalAction : IAnimalAction
    {
        public abstract string ActionName { get; }
        public abstract string Description { get; }
        public abstract int CooldownTicks { get; }

        public virtual bool CanExecute(Pawn animal)
        {
            if (animal == null || animal.Dead || animal.Destroyed)
                return false;

            return true;
        }

        public abstract bool Execute(Pawn animal);

        protected void LogAction(Pawn animal, string message)
        {
            if (MyMod.Settings?.debugMode == true)
            {
                Log.Message($"[EchoColony] {ActionName} on {animal.LabelShort}: {message}");
            }
        }
    }
}