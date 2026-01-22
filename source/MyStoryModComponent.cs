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
                    Log.Message("[EchoColony] MyStoryModComponent added to world after load");
                }
            });
        }
    }

    public class MyStoryModComponent : MonoBehaviour
    {
        public static MyStoryModComponent Instance;

        public ColonistMemoryManager ColonistMemoryManager;
        public DailyGroupMemoryTracker GroupMemoryTracker;
        
        private Player2Heartbeat player2HeartbeatComponent;
        private bool ttsInitialized = false;
        private bool actionsInitialized = false;
        
        // Cleanup tracking
        private int lastCleanupTick = 0;
        private const int CLEANUP_INTERVAL = 60000; // Every in-game day

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Log.Message($"[EchoColony] MyStoryModComponent.Start() executed. enableTTS = {MyMod.Settings?.enableTTS}");
            Init();
        }

        public void Init()
        {
            Log.Message("[EchoColony] Start() executed in MyStoryModComponent");

            ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
            if (ColonistMemoryManager == null)
            {
                ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                Current.Game.components.Add(ColonistMemoryManager);
            }

            GroupMemoryTracker = ColonistMemoryManager.GetGroupMemoryTracker();

            EnsurePlayer2HeartbeatExists();

            if (MyMod.Settings != null && MyMod.Settings.enableTTS && !ttsInitialized)
            {
                Log.Message("[EchoColony] TTS enabled. Loading voices...");
                StartCoroutine(TTSVoiceCache.LoadVoices());
                ttsInitialized = true;
            }
            
            if (MyMod.Settings != null && MyMod.Settings.enableDivineActions && !actionsInitialized)
            {
                Log.Message("[EchoColony] Divine Actions enabled. Initializing action system...");
                Actions.ActionRegistry.Initialize();
                actionsInitialized = true;
            }
        }

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

        public void ForcePlayer2Check()
        {
            EnsurePlayer2HeartbeatExists();
            player2HeartbeatComponent?.ForceCheckPlayer2();
        }

        public bool IsPlayer2Available()
        {
            return MyMod.Settings?.modelSource == ModelSource.Player2;
        }

        void Update()
        {
            if (MyMod.Settings != null && MyMod.Settings.enableTTS && !ttsInitialized)
            {
                Log.Message("[EchoColony] TTS enabled during runtime. Loading voices...");
                StartCoroutine(TTSVoiceCache.LoadVoices());
                ttsInitialized = true;
            }
            else if (MyMod.Settings != null && !MyMod.Settings.enableTTS && ttsInitialized)
            {
                ttsInitialized = false;
            }
            
            if (MyMod.Settings != null && MyMod.Settings.enableDivineActions && !actionsInitialized)
            {
                Log.Message("[EchoColony] Divine Actions enabled during runtime. Initializing...");
                Actions.ActionRegistry.Initialize();
                actionsInitialized = true;
            }
            else if (MyMod.Settings != null && !MyMod.Settings.enableDivineActions && actionsInitialized)
            {
                actionsInitialized = false;
            }

            EnsurePlayer2HeartbeatExists();
            
            // Periodic cleanup of action cooldowns
            if (Find.TickManager != null && MyMod.Settings != null && MyMod.Settings.enableDivineActions)
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - lastCleanupTick > CLEANUP_INTERVAL)
                {
                    Actions.Mood.AddPlayerThoughtAction.CleanupOldCooldowns();
                    lastCleanupTick = currentTick;
                    Log.Message("[EchoColony] Cleaned up old action cooldowns");
                }
            }
        }
    }
}