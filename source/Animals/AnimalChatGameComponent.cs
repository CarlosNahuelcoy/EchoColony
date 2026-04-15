using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony.Animals
{
    public class AnimalChatGameComponent : GameComponent
    {
        private Dictionary<string, List<string>> animalChats = new Dictionary<string, List<string>>();

        private static AnimalChatGameComponent instance;
        public static AnimalChatGameComponent Instance
        {
            get
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<AnimalChatGameComponent>();
                    if (instance == null)
                    {
                        instance = new AnimalChatGameComponent(Current.Game);
                        Current.Game.components.Add(instance);
                    }
                }
                return instance;
            }
        }

        public AnimalChatGameComponent(Game game) { }

        public List<string> GetChat(Pawn animal)
        {
            if (animal == null) return new List<string>();

            string key = animal.ThingID;
            if (!animalChats.ContainsKey(key))
            {
                animalChats[key] = new List<string>();
            }

            return animalChats[key];
        }

        public void SaveChat(Pawn animal, List<string> chat)
        {
            if (animal == null) return;

            string key = animal.ThingID;
            animalChats[key] = new List<string>(chat);
        }

        public void ClearChat(Pawn animal)
        {
            if (animal == null) return;

            string key = animal.ThingID;
            if (animalChats.ContainsKey(key))
            {
                animalChats[key].Clear();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animalChats, "animalChats", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars && animalChats == null)
            {
                animalChats = new Dictionary<string, List<string>>();
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Cleanup every in-game hour
            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                CleanupDeadAnimals();
            }
        }

        private void CleanupDeadAnimals()
        {
            if (animalChats == null || animalChats.Count == 0) return;

            var toRemove = new List<string>();

            foreach (var key in animalChats.Keys)
            {
                bool stillExists = false;

                foreach (var map in Find.Maps)
                {
                    var animal = map.mapPawns.AllPawns.FirstOrDefault(p => p.ThingID == key);
                    if (animal != null && !animal.Dead)
                    {
                        stillExists = true;
                        break;
                    }
                }

                if (!stillExists)
                {
                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
            {
                animalChats.Remove(key);
            }
        }
    }
}