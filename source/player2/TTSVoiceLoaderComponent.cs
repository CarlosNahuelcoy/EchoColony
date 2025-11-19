using Verse;

namespace EchoColony
{
    public class TTSVoiceLoaderComponent : GameComponent
    {
        public TTSVoiceLoaderComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            if (MyMod.Settings != null && MyMod.Settings.enableTTS)
            {
                Log.Message("[EchoColony] [TTSVoiceLoaderComponent] FinalizeInit() – Iniciando carga de voces");

                if (MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.StartCoroutine(TTSVoiceCache.LoadVoices());
                }
                else
                {
                    Log.Error("[EchoColony] ❌ MyStoryModComponent no está disponible para correr coroutines.");
                }
            }
        }
    }
}
