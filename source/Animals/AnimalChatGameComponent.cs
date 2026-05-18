using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony.Animals
{
    public class AnimalChatGameComponent : GameComponent
    {
        private Dictionary<string, List<string>> animalChats = new Dictionary<string, List<string>>();

        // Direct GetComponent pattern — no static caching, no risk of stale references or duplicate components
        public static AnimalChatGameComponent Instance => Current.Game?.GetComponent<AnimalChatGameComponent>();

        public AnimalChatGameComponent(Game game) { }

        public List<string> GetChat(Pawn animal)
        {
            if (animal == null) return new List<string>();

            string key = animal.ThingID;
            if (!animalChats.ContainsKey(key))
                animalChats[key] = new List<string>();

            return animalChats[key];
        }

        public void SaveChat(Pawn animal, List<string> chat)
        {
            if (animal == null) return;
            animalChats[animal.ThingID] = new List<string>(chat);
        }

        public void ClearChat(Pawn animal)
        {
            if (animal == null) return;

            string key = animal.ThingID;
            if (animalChats.ContainsKey(key))
                animalChats[key].Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animalChats, "animalChats", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && animalChats == null)
                animalChats = new Dictionary<string, List<string>>();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Find.TickManager.TicksGame % 2000 == 0)
                CleanupDeadAnimals();
        }

        private void CleanupDeadAnimals()
        {
            if (animalChats == null || animalChats.Count == 0) return;

            var toRemove = animalChats.Keys
                .Where(key => !Find.Maps.Any(map =>
                    map.mapPawns.AllPawns.Any(p => p.ThingID == key && !p.Dead)))
                .ToList();

            foreach (var key in toRemove)
                animalChats.Remove(key);
        }
    }
}