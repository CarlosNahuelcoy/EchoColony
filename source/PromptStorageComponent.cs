using Verse;
using System.Collections.Generic;

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

        public override void FinalizeInit()
{
    base.FinalizeInit();

    // 🔁 Agregar TTSVoiceLoaderComponent si no está
    if (Current.Game.GetComponent<TTSVoiceLoaderComponent>() == null)
    {
        Current.Game.components.Add(new TTSVoiceLoaderComponent(Current.Game));
        Log.Message("[EchoColony] 🔁 TTSVoiceLoaderComponent agregado desde PromptStorageComponent");
    }
}
    }
}
