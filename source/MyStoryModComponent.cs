using Verse;
using UnityEngine;

namespace EchoColony
{
    [StaticConstructorOnStartup]
    public class MyStoryModBootstrap
    {
        static MyStoryModBootstrap()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (MyStoryModComponent.Instance == null)
                {
                    GameObject obj = new GameObject("MyStoryModComponent");
                    Object.DontDestroyOnLoad(obj);
                    MyStoryModComponent.Instance = obj.AddComponent<MyStoryModComponent>();
                    Log.Message("[EchoColony] 🟢 MyStoryModComponent añadido al mundo tras carga.");
                    // Ya NO se llama Init() aquí
                }
            });
        }
    }

    public class MyStoryModComponent : MonoBehaviour
    {
        public static MyStoryModComponent Instance;

        public ColonistMemoryManager ColonistMemoryManager;
        public DailyGroupMemoryTracker GroupMemoryTracker;
        
        // ✅ NUEVO: Referencias a componentes dinámicos
        private Player2Heartbeat player2HeartbeatComponent;
        private bool ttsInitialized = false;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Log.Message($"[EchoColony] MyStoryModComponent.Start() ejecutado. enableTTS = {MyMod.Settings?.enableTTS}");
            Init();
        }

        public void Init()
        {
            Log.Message("[EchoColony] ✅ Start() ejecutado en MyStoryModComponent");

            ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
            if (ColonistMemoryManager == null)
            {
                ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                Current.Game.components.Add(ColonistMemoryManager);
            }

            GroupMemoryTracker = ColonistMemoryManager.GetGroupMemoryTracker();

            // ✅ CAMBIO CLAVE: Siempre añadir Player2Heartbeat, él se encarga de decidir cuándo funcionar
            EnsurePlayer2HeartbeatExists();

            // ✅ MEJORADO: TTS initialization
            if (MyMod.Settings != null && MyMod.Settings.enableTTS && !ttsInitialized)
            {
                Log.Message("[EchoColony] TTS enabled. Loading voices...");
                StartCoroutine(TTSVoiceCache.LoadVoices());
                ttsInitialized = true;
            }
        }

        // ✅ NUEVO: Método para asegurar que Player2Heartbeat existe
        private void EnsurePlayer2HeartbeatExists()
        {
            if (player2HeartbeatComponent == null)
            {
                player2HeartbeatComponent = gameObject.GetComponent<Player2Heartbeat>();
                if (player2HeartbeatComponent == null)
                {
                    player2HeartbeatComponent = gameObject.AddComponent<Player2Heartbeat>();
                    Log.Message("[EchoColony] Player2Heartbeat component added");
                }
            }
        }

        // ✅ NUEVO: Método público para forzar check de Player2 (útil para UI)
        public void ForcePlayer2Check()
        {
            EnsurePlayer2HeartbeatExists();
            player2HeartbeatComponent?.ForceCheckPlayer2();
        }

        // ✅ NUEVO: Método para verificar si Player2 está disponible
        public bool IsPlayer2Available()
        {
            // Simplificado: solo verificar si está configurado como modelo activo
            return MyMod.Settings?.modelSource == ModelSource.Player2;
        }

        // ✅ NUEVO: Update para manejar cambios dinámicos en configuración
        void Update()
        {
            // Verificar cambios en configuración TTS
            if (MyMod.Settings != null && MyMod.Settings.enableTTS && !ttsInitialized)
            {
                Log.Message("[EchoColony] TTS enabled during runtime. Loading voices...");
                StartCoroutine(TTSVoiceCache.LoadVoices());
                ttsInitialized = true;
            }
            else if (MyMod.Settings != null && !MyMod.Settings.enableTTS && ttsInitialized)
            {
                // TTS deshabilitado durante runtime
                ttsInitialized = false;
            }

            // ✅ Asegurar que Player2Heartbeat siempre esté disponible
            EnsurePlayer2HeartbeatExists();
        }
    }
}