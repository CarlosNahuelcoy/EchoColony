using System.Collections.Generic;
using Verse;

namespace EchoColony
{
    public class EventLogComponent : GameComponent
    {
        public List<string> savedEvents = new List<string>();

        public EventLogComponent(Game game) {}

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedEvents, "savedEvents", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Restaurar en RAM para EventLogger
                EventLogger.events = new List<string>(savedEvents);
            }

            // Cortar si supera el límite
            const int MaxEvents = 1000;
            if (savedEvents.Count > MaxEvents)
            {
                savedEvents.RemoveRange(0, savedEvents.Count - MaxEvents);
            }
        }
    }
}
