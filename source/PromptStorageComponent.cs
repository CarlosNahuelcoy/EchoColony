using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony
{
    public class PromptStorageComponent : GameComponent
    {
        // Per-colonist individual prompts and settings, keyed by ThingID
        public Dictionary<string, string> promptsByColonist  = new Dictionary<string, string>();
        public Dictionary<string, string> voicesByColonist   = new Dictionary<string, string>();
        public Dictionary<string, bool>   ignoreAgeByColonist = new Dictionary<string, bool>();

        // Group system — groups keyed by group GUID id, assignments keyed by colonist ThingID
        public Dictionary<string, ColonistGroup> groups           = new Dictionary<string, ColonistGroup>();
        public Dictionary<string, string>         groupByColonist  = new Dictionary<string, string>();

        public PromptStorageComponent(Game game)
        {
            EnsureDictionaries();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref promptsByColonist,   "promptsByColonist",   LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref voicesByColonist,    "voicesByColonist",    LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref ignoreAgeByColonist, "ignoreAgeByColonist", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref groups,              "groups",              LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref groupByColonist,     "groupByColonist",     LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
                EnsureDictionaries();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            CleanupOrphanedData();

            if (Current.Game.GetComponent<TTSVoiceLoaderComponent>() == null)
            {
                Current.Game.components.Add(new TTSVoiceLoaderComponent(Current.Game));
                Log.Message("[EchoColony] TTSVoiceLoaderComponent added from PromptStorageComponent");
            }
        }

        /// <summary>
        /// Removes data for colonists that no longer exist in any form.
        /// Also removes group assignments pointing to deleted groups.
        /// </summary>
        public void CleanupOrphanedData()
        {
            if (promptsByColonist == null) return;

            var validPawnIDs = new HashSet<string>(
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                    .Where(p => p != null)
                    .Select(p => p.ThingID)
            );

            // Clean per-colonist data for dead/missing pawns
            var deadKeys = promptsByColonist.Keys
                .Where(k => !validPawnIDs.Contains(k))
                .ToList();

            foreach (var key in deadKeys)
            {
                promptsByColonist.Remove(key);
                voicesByColonist?.Remove(key);
                ignoreAgeByColonist?.Remove(key);
                groupByColonist?.Remove(key);
                Log.Message($"[EchoColony] Cleaned up data for obsolete Pawn ID: {key}");
            }

            // Clean group assignments pointing to groups that no longer exist
            if (groupByColonist != null && groups != null)
            {
                var orphanedAssignments = groupByColonist
                    .Where(kvp => !groups.ContainsKey(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in orphanedAssignments)
                    groupByColonist.Remove(key);
            }

            // Clean groupByColonist for pawns that have a group but no individual prompt
            if (groupByColonist != null)
            {
                var deadGroupKeys = groupByColonist.Keys
                    .Where(k => !validPawnIDs.Contains(k))
                    .ToList();
                foreach (var key in deadGroupKeys)
                    groupByColonist.Remove(key);
            }
        }

        private void EnsureDictionaries()
        {
            if (promptsByColonist  == null) promptsByColonist  = new Dictionary<string, string>();
            if (voicesByColonist   == null) voicesByColonist   = new Dictionary<string, string>();
            if (ignoreAgeByColonist == null) ignoreAgeByColonist = new Dictionary<string, bool>();
            if (groups             == null) groups             = new Dictionary<string, ColonistGroup>();
            if (groupByColonist    == null) groupByColonist    = new Dictionary<string, string>();
        }
    }
}