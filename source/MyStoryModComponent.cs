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

            // Guard: Current.Game puede ser null durante generación de mundo nuevo
            if (Current.Game == null)
            {
                Log.Warning("[EchoColony] Init() called but Current.Game is null — skipping until game is ready");
                return;
            }

            // Initialize ColonistMemoryManager
            ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
            if (ColonistMemoryManager == null)
            {
                ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                Current.Game.components.Add(ColonistMemoryManager);
            }

            GroupMemoryTracker = ColonistMemoryManager.GetGroupMemoryTracker();

            // Reset monologue state on game load
            Conversations.PawnMonologueManager.OnGameLoaded();
            TalesCache.Clear();

            // Initialize SpontaneousMessageTracker
            var spontaneousTracker = Current.Game.GetComponent<SpontaneousMessages.SpontaneousMessageTracker>();
            if (spontaneousTracker == null)
            {
                spontaneousTracker = new SpontaneousMessages.SpontaneousMessageTracker(Current.Game);
                Current.Game.components.Add(spontaneousTracker);
                Log.Message("[EchoColony] SpontaneousMessageTracker added to game");
            }
            else
            {
                Log.Message("[EchoColony] SpontaneousMessageTracker already exists");
            }

            // Initialize Animal Chat Components
            var animalChatComponent = Current.Game.GetComponent<Animals.AnimalChatGameComponent>();
            if (animalChatComponent == null)
            {
                animalChatComponent = new Animals.AnimalChatGameComponent(Current.Game);
                Current.Game.components.Add(animalChatComponent);
                Log.Message("[EchoColony] AnimalChatGameComponent added to game");
            }
            else
            {
                Log.Message("[EchoColony] AnimalChatGameComponent already exists");
            }

            var animalPromptManager = Current.Game.GetComponent<Animals.AnimalPromptManager>();
            if (animalPromptManager == null)
            {
                animalPromptManager = new Animals.AnimalPromptManager(Current.Game);
                Current.Game.components.Add(animalPromptManager);
                Log.Message("[EchoColony] AnimalPromptManager added to game");
            }
            else
            {
                Log.Message("[EchoColony] AnimalPromptManager already exists");
            }

            // Initialize Animal Action Registry (always, even if actions disabled - for safety)
            Animals.Actions.AnimalActionRegistry.Initialize();

            // Initialize Mech Action Registry
            Mechs.Actions.MechActionRegistry.Initialize();

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
                Animals.Actions.AnimalActionRegistry.Initialize();
                Mechs.Actions.MechActionRegistry.Initialize();
                actionsInitialized = true;
            }

            // Initialize storyteller spontaneous message system
            if (MyMod.Settings != null && MyMod.Settings.IsStorytellerMessagesActive())
            {
                StorytellerSpontaneousMessageSystem.StartSystem();
                Log.Message("[EchoColony] Storyteller spontaneous message system started");
            }

            // Initialize Mech Chat Component
            var mechChatComponent = Current.Game.GetComponent<Mechs.MechChatGameComponent>();
            if (mechChatComponent == null)
            {
                mechChatComponent = new Mechs.MechChatGameComponent(Current.Game);
                Current.Game.components.Add(mechChatComponent);
                Log.Message("[EchoColony] MechChatGameComponent added to game");
            }
            else
            {
                Log.Message("[EchoColony] MechChatGameComponent already exists");
            }

            // Initialize Mech Prompt Manager
            var mechPromptManager = Current.Game.GetComponent<Mechs.MechPromptManager>();
            if (mechPromptManager == null)
            {
                mechPromptManager = new Mechs.MechPromptManager(Current.Game);
                Current.Game.components.Add(mechPromptManager);
                Log.Message("[EchoColony] MechPromptManager added to game");
            }
            else
            {
                Log.Message("[EchoColony] MechPromptManager already exists");
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
                Animals.Actions.AnimalActionRegistry.Initialize();
                Mechs.Actions.MechActionRegistry.Initialize();
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
                    Animals.Actions.AnimalActionParser.CleanupOldCooldowns();
                    Mechs.Actions.MechActionParser.CleanupOldCooldowns();
                    TalesCache.PruneStale();   // ← agregás esta línea
                    lastCleanupTick = currentTick;
                    Conversations.PawnMonologueManager.Tick();
                    Log.Message("[EchoColony] Cleaned up old action cooldowns");
                }
            }
        }
    }
}