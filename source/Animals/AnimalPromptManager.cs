using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony.Animals
{
    /// <summary>
    /// Stores per-animal prompt and intelligence flag, saved with the game.
    /// </summary>
    public class AnimalPromptManager : GameComponent
    {
        // ── Persistent data ──────────────────────────────────────────────────────
        private Dictionary<string, AnimalSaveData> animalData = new Dictionary<string, AnimalSaveData>();

        // Legacy migration: old saves stored only a string prompt
        private Dictionary<string, string> animalPrompts_LEGACY = null;

        // Direct GetComponent pattern — no static caching, no risk of stale references or duplicate components
        public static AnimalPromptManager Instance => Current.Game?.GetComponent<AnimalPromptManager>();

        public AnimalPromptManager(Game game) { }

        // ── Public API ───────────────────────────────────────────────────────────

        public static string GetPrompt(Pawn animal)
        {
            return GetData(animal)?.customPrompt ?? "";
        }

        public static void SetPrompt(Pawn animal, string prompt)
        {
            var data = GetOrCreateData(animal);
            if (data == null) return;
            data.customPrompt = string.IsNullOrWhiteSpace(prompt) ? "" : prompt.Trim();
        }

        public static bool GetIsIntelligent(Pawn animal)
        {
            return GetData(animal)?.isIntelligent ?? false;
        }

        public static void SetIsIntelligent(Pawn animal, bool value)
        {
            var data = GetOrCreateData(animal);
            if (data == null) return;
            data.isIntelligent = value;
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private static AnimalSaveData GetData(Pawn animal)
        {
            if (animal == null) return null;
            var inst = Instance;
            if (inst == null) return null;
            inst.animalData.TryGetValue(animal.ThingID, out var data);
            return data;
        }

        private static AnimalSaveData GetOrCreateData(Pawn animal)
        {
            if (animal == null) return null;
            var inst = Instance;
            if (inst == null) return null;
            string key = animal.ThingID;
            if (!inst.animalData.ContainsKey(key))
                inst.animalData[key] = new AnimalSaveData();
            return inst.animalData[key];
        }

        // ── Persistence ──────────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();

            // Migrate old string-only saves
            Scribe_Collections.Look(ref animalPrompts_LEGACY, "animalPrompts", LookMode.Value, LookMode.Value);
            if (animalPrompts_LEGACY != null && animalPrompts_LEGACY.Count > 0)
            {
                if (animalData == null) animalData = new Dictionary<string, AnimalSaveData>();
                foreach (var kvp in animalPrompts_LEGACY)
                {
                    if (!animalData.ContainsKey(kvp.Key))
                        animalData[kvp.Key] = new AnimalSaveData { customPrompt = kvp.Value };
                }
                animalPrompts_LEGACY = null;
            }

            Scribe_Collections.Look(ref animalData, "animalData", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && animalData == null)
                animalData = new Dictionary<string, AnimalSaveData>();
        }

        // ── Cleanup ──────────────────────────────────────────────────────────────

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager.TicksGame % 2000 == 0)
                CleanupDeadAnimals();
        }

        private void CleanupDeadAnimals()
        {
            if (animalData == null || animalData.Count == 0) return;

            var toRemove = animalData.Keys
                .Where(key => !Find.Maps.Any(map =>
                    map.mapPawns.AllPawns.Any(p => p.ThingID == key && !p.Dead)))
                .ToList();

            foreach (var key in toRemove)
                animalData.Remove(key);
        }
    }

    // ── Save data class ───────────────────────────────────────────────────────────

    public class AnimalSaveData : IExposable
    {
        public string customPrompt = "";
        public bool isIntelligent = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref customPrompt, "customPrompt", "");
            Scribe_Values.Look(ref isIntelligent, "isIntelligent", false);
        }
    }
}