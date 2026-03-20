using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony
{
    public class PromptStorageComponent : GameComponent
    {
        public Dictionary<string, string> promptsByColonist = new Dictionary<string, string>();
        public Dictionary<string, string> voicesByColonist = new Dictionary<string, string>();

        public Dictionary<string, bool> ignoreAgeByColonist = new Dictionary<string, bool>();


        public PromptStorageComponent(Game game)
        {
            // 🔒 Seguridad por si RimWorld no llama ExposeData correctamente
            if (promptsByColonist == null)
                promptsByColonist = new Dictionary<string, string>();

            if (voicesByColonist == null)
                voicesByColonist = new Dictionary<string, string>();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref promptsByColonist, "promptsByColonist", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref voicesByColonist, "voicesByColonist", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref ignoreAgeByColonist, "ignoreAgeByColonist", LookMode.Value, LookMode.Value);

            if (ignoreAgeByColonist == null)
                ignoreAgeByColonist = new Dictionary<string, bool>();

            // 🔁 Revalidar después de cargar
            if (promptsByColonist == null)
                promptsByColonist = new Dictionary<string, string>();

            if (voicesByColonist == null)
                voicesByColonist = new Dictionary<string, string>();
        }

        public void CleanupOrphanedPrompts()
        {
            // Verificamos que los diccionarios no sean nulos antes de empezar
            if (promptsByColonist == null) return;

            // PawnsFinder requiere System.Linq para el .Select y .Where
            var validPawnIDs = new HashSet<string>(
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                    .Where(p => p != null)
                    .Select(p => p.ThingID)
            );

            List<string> keysToRemove = promptsByColonist.Keys
                .Where(key => !validPawnIDs.Contains(key))
                .ToList();

            foreach (var key in keysToRemove)
            {
                promptsByColonist.Remove(key);
                voicesByColonist?.Remove(key);
                ignoreAgeByColonist?.Remove(key);
                Log.Message($"[EchoColony] Cleaned up data for obsolete Pawn ID: {key}");
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            CleanupOrphanedPrompts();

            // 🔁 Agregar TTSVoiceLoaderComponent si no está
            if (Current.Game.GetComponent<TTSVoiceLoaderComponent>() == null)
            {
                Current.Game.components.Add(new TTSVoiceLoaderComponent(Current.Game));
                Log.Message("[EchoColony] 🔁 TTSVoiceLoaderComponent agregado desde PromptStorageComponent");
            }
        }
    }
}
