using System.Collections.Generic;
using Verse;

namespace EchoColony.Animals
{
    public class AnimalPromptManager : GameComponent
    {
        private Dictionary<string, string> animalPrompts = new Dictionary<string, string>();
        
        private static AnimalPromptManager instance;
        public static AnimalPromptManager Instance
        {
            get
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<AnimalPromptManager>();
                    if (instance == null)
                    {
                        instance = new AnimalPromptManager(Current.Game);
                        Current.Game.components.Add(instance);
                    }
                }
                return instance;
            }
        }

        public AnimalPromptManager(Game game) { }

        public static string GetPrompt(Pawn animal)
        {
            if (animal == null) return "";
            
            var instance = Instance;
            if (instance == null) return "";
            
            string key = animal.ThingID;
            if (instance.animalPrompts.TryGetValue(key, out string prompt))
            {
                return prompt;
            }
            
            return "";
        }

        public static void SetPrompt(Pawn animal, string prompt)
        {
            if (animal == null) return;
            
            var instance = Instance;
            if (instance == null) return;
            
            string key = animal.ThingID;
            
            if (string.IsNullOrWhiteSpace(prompt))
            {
                instance.animalPrompts.Remove(key);
            }
            else
            {
                instance.animalPrompts[key] = prompt.Trim();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animalPrompts, "animalPrompts", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && animalPrompts == null)
            {
                animalPrompts = new Dictionary<string, string>();
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // Cleanup dead animals every 2000 ticks (~1 in-game hour)
            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                CleanupDeadAnimals();
            }
        }

        private void CleanupDeadAnimals()
        {
            if (animalPrompts == null || animalPrompts.Count == 0) return;
            
            var toRemove = new List<string>();
            
            foreach (var key in animalPrompts.Keys)
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
                animalPrompts.Remove(key);
            }
        }
    }
}