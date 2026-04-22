using System.Collections.Generic;
using Verse;
using RimWorld;
using EchoColony.SpontaneousMessages;
using System.Linq;
using UnityEngine;

namespace EchoColony
{
    public enum ModelSource
    {
        Player2,
        Gemini,
        Local,
        OpenRouter,
        Custom
    }

    public enum LocalModelProvider
    {
        LMStudio,
        Ollama,
        KoboldAI
    }

    public enum AnimalNarrativeStyle
    {
        ThirdPerson,
        FirstPerson
    }

    public enum ConversationAnimalMode
    {
        Disabled,
        IntelligentOnly,
        All
    }

    public class GeminiModelPreferences : IExposable
    {
        public string preferredFastModel     = "";
        public string preferredAdvancedModel = "";
        public bool   useAutoSelection       = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref preferredFastModel,     "preferredFastModel",     "");
            Scribe_Values.Look(ref preferredAdvancedModel, "preferredAdvancedModel", "");
            Scribe_Values.Look(ref useAutoSelection,       "useAutoSelection",       true);
        }
    }

    public class GeminiSettings : ModSettings
    {
        public string apiKey        = "";
        public string globalPrompt  = "";
        public int    maxResponseLength = 300;

        public bool enableSocialAffectsPersonality = true;
        public bool enableRoleplayResponses        = true;

        public bool enableMemorySystem = true;

        public ModelSource modelSource  = ModelSource.Player2;
        public string      selectedModel = "";

        public GeminiModelPreferences modelPreferences = new GeminiModelPreferences();
        public bool useAdvancedModel = false;

        public string             localModelEndpoint = "http://localhost:11434/api/generate";
        public string             localModelName     = "llama3.2:latest";
        public LocalModelProvider localModelProvider = LocalModelProvider.LMStudio;

        public string openRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
        public string openRouterApiKey   = "";
        public string openRouterModel    = "mistral-7b";

        // ═══════════════════════════════════════════════════════════════
        // FACTION COMMS CHAT
        // ═══════════════════════════════════════════════════════════════
        // Cooldown between goodwill changes from conversation (in hours)
        public bool   enableFactionCommsChat              = true;
        public float  factionChatGoodwillCooldownHours   = 24f;

        // ═══════════════════════════════════════════════════════════════
        // CUSTOM PROVIDER (any OpenAI-compatible endpoint)
        // ═══════════════════════════════════════════════════════════════
        // Works with: LMStudio (server mode), Ollama (OpenAI compat),
        // Groq, Together AI, Mistral API, and any /v1/chat/completions endpoint.

        public string customEndpoint  = "http://localhost:1234/v1/chat/completions";
        public string customApiKey    = "";
        public string customModelName = "";

        // ═══════════════════════════════════════════════════════════════

        public bool debugMode    = false;
        public bool enableTTS    = true;
        public bool autoPlayVoice = true;

        public bool ignoreDangersInConversations = false;
        public Dictionary<string, string> colonistVoices = new Dictionary<string, string>();

        public bool enableDivineActions  = true;
        public bool allowNegativeActions = false;
        public bool allowExtremeActions  = false;

        public bool enableStorytellerButton = true;

        // ═══════════════════════════════════════════════════════════════
        // PLAYER2 WEB API AUTH
        // ═══════════════════════════════════════════════════════════════

        public string player2ApiKey = "";

        // ═══════════════════════════════════════════════════════════════
        // VISION SYSTEM
        // ═══════════════════════════════════════════════════════════════

        public bool enableVision = false;

        // ===== ANIMAL SETTING =====
        public AnimalNarrativeStyle defaultAnimalNarrativeStyle = AnimalNarrativeStyle.ThirdPerson;

        // ===== SPONTANEOUS MESSAGES SYSTEM =====
        public SpontaneousMessageMode spontaneousMessageMode             = SpontaneousMessageMode.Disabled;
        public int   defaultMaxMessagesPerColonistPerDay                 = 1;
        public float defaultColonistCooldownHours                       = 12f;
        public float randomMessageIntervalHours                         = 4f;
        public bool  prioritizeSocialTraits                             = true;
        public float minConsciousnessPercent                            = 50f;

        // ===== STORYTELLER SPONTANEOUS MESSAGES SYSTEM =====
        public StorytellerMessageMode storytellerMessageMode            = StorytellerMessageMode.Disabled;
        public float storytellerRandomIntervalMinutes                   = 30f;
        public float storytellerIncidentChance                         = 0.3f;
        public bool  storytellerMessageAutoClose                       = true;
        public float storytellerMessageAutoCloseSeconds                = 8f;
        public bool  storytellerMessagePlaySound                       = true;

        public enum StorytellerMessageMode
        {
            Disabled,
            RandomOnly,
            IncidentsOnly,
            Full
        }

        public Dictionary<string, ColonistMessageSettings> colonistMessageSettings =
            new Dictionary<string, ColonistMessageSettings>();

        // ═══════════════════════════════════════════════════════════════
        // PAWN CONVERSATIONS
        // ═══════════════════════════════════════════════════════════════

        public bool   enablePawnConversations    = false;
        public string conversationGlobalPrompt   = "";
        public int    conversationLinesPerPawn   = 1;
        public float  conversationBubbleDelay    = 1.5f;
        public int    conversationCooldownHours  = 6;
        public ConversationAnimalMode conversationAnimalMode = ConversationAnimalMode.IntelligentOnly;
        public int    conversationMinOpinion     = -100;
        public int    conversationMaxColonySize  = 0;
        public bool   conversationIncludePrisoners = true;
        public bool   conversationIncludeSlaves    = true;
        public bool   conversationIncludeGuests    = false;
        public int    conversationDisableAtSpeed   = 3;
        public bool   conversationAllowSimultaneous = false;

        // ═══════════════════════════════════════════════════════════════
        // MONOLOGUE SETTINGS
        // ═══════════════════════════════════════════════════════════════

        public bool  enableMonologues        = false;
        public float chatLogX                = 0f;
        public float chatLogY                = 0f;
        public float chatLogW                = 0f;
        public float chatLogH                = 0f;
        public bool  chatLogLargeFont        = false;
        public KeyCode chatLogHotkey         = KeyCode.None;

        [System.NonSerialized] public bool isWaitingForChatLogKey = false;

        public int   monologueCooldownHours  = 4;
        public float monologueChancePerHour  = 0.15f;
        public float monologueMinMoodImpact  = 5f;

        // ═══════════════════════════════════════════════════════════════
        // EXPOSE DATA
        // ═══════════════════════════════════════════════════════════════

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref apiKey,                         "GeminiApiKey",                   "");
            Scribe_Values.Look(ref globalPrompt,                   "GlobalPrompt",                   "");
            Scribe_Values.Look(ref maxResponseLength,              "MaxResponseLength",               300);
            Scribe_Values.Look(ref enableSocialAffectsPersonality, "EnableSocialAffectsPersonality",  true);
            Scribe_Values.Look(ref enableRoleplayResponses,        "EnableRoleplayResponses",         true);
            Scribe_Values.Look(ref enableMemorySystem,             "EnableMemorySystem",              true);
            Scribe_Values.Look(ref modelSource,                    "ModelSource",                    ModelSource.Player2);
            Scribe_Values.Look(ref selectedModel,                  "selectedModel",                  "");

            if (modelPreferences == null) modelPreferences = new GeminiModelPreferences();
            Scribe_Deep.Look(ref modelPreferences, "modelPreferences");
            Scribe_Values.Look(ref useAdvancedModel, "UseAdvancedModel", false);

            Scribe_Values.Look(ref localModelEndpoint, "LocalModelEndpoint", "http://localhost:11434/api/generate");
            Scribe_Values.Look(ref localModelName,     "LocalModelName",     "llama3.2:latest");
            Scribe_Values.Look(ref localModelProvider, "localModelProvider", LocalModelProvider.LMStudio);

            Scribe_Values.Look(ref openRouterEndpoint, "OpenRouterEndpoint", "https://openrouter.ai/api/v1/chat/completions");
            Scribe_Values.Look(ref openRouterApiKey,   "OpenRouterApiKey",   "");
            Scribe_Values.Look(ref openRouterModel,    "OpenRouterModel",    "mistral-7b");

            // Custom provider
            Scribe_Values.Look(ref customEndpoint,  "customEndpoint",  "http://localhost:1234/v1/chat/completions");
            Scribe_Values.Look(ref customApiKey,    "customApiKey",    "");
            Scribe_Values.Look(ref customModelName, "customModelName", "");

            // Faction comms chat
            Scribe_Values.Look(ref enableFactionCommsChat,           "enableFactionCommsChat",           true);
            Scribe_Values.Look(ref factionChatGoodwillCooldownHours, "factionChatGoodwillCooldownHours", 24f);

            Scribe_Values.Look(ref enableTTS,     "EnableTTS",     true);
            Scribe_Values.Look(ref autoPlayVoice, "AutoPlayVoice", true);
            Scribe_Collections.Look(ref colonistVoices, "ColonistVoices", LookMode.Value, LookMode.Value);

            Scribe_Values.Look(ref debugMode,                     "DebugMode",                     false);
            Scribe_Values.Look(ref ignoreDangersInConversations,  "IgnoreDangersInConversations",  false);
            Scribe_Values.Look(ref enableDivineActions,           "enableDivineActions",           true);
            Scribe_Values.Look(ref allowNegativeActions,          "allowNegativeActions",          false);
            Scribe_Values.Look(ref allowExtremeActions,           "allowExtremeActions",           false);
            Scribe_Values.Look(ref enableStorytellerButton,       "enableStorytellerButton",       true);

            Scribe_Values.Look(ref player2ApiKey, "player2ApiKey", "");
            Scribe_Values.Look(ref enableVision,  "enableVision",  false);

            Scribe_Values.Look(ref spontaneousMessageMode,             "spontaneousMessageMode",             SpontaneousMessageMode.Disabled);
            Scribe_Values.Look(ref defaultMaxMessagesPerColonistPerDay,"defaultMaxMessagesPerColonistPerDay", 1);
            Scribe_Values.Look(ref defaultColonistCooldownHours,       "defaultColonistCooldownHours",        12f);
            Scribe_Values.Look(ref randomMessageIntervalHours,         "randomMessageIntervalHours",          36f);
            Scribe_Values.Look(ref prioritizeSocialTraits,             "prioritizeSocialTraits",              true);
            Scribe_Values.Look(ref minConsciousnessPercent,            "minConsciousnessPercent",             50f);

            if (Scribe.mode == LoadSaveMode.Saving)
                CleanupColonistSettings();

            Scribe_Collections.Look(ref colonistMessageSettings, "colonistMessageSettings", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars && colonistMessageSettings == null)
                colonistMessageSettings = new Dictionary<string, ColonistMessageSettings>();

            Scribe_Values.Look(ref storytellerMessageMode,           "storytellerMessageMode",           StorytellerMessageMode.Disabled);
            Scribe_Values.Look(ref storytellerRandomIntervalMinutes, "storytellerRandomIntervalMinutes", 30f);
            Scribe_Values.Look(ref storytellerIncidentChance,        "storytellerIncidentChance",        0.3f);
            Scribe_Values.Look(ref storytellerMessageAutoClose,      "storytellerMessageAutoClose",      true);
            Scribe_Values.Look(ref storytellerMessageAutoCloseSeconds,"storytellerMessageAutoCloseSeconds",8f);
            Scribe_Values.Look(ref storytellerMessagePlaySound,      "storytellerMessagePlaySound",      true);

            Scribe_Values.Look(ref defaultAnimalNarrativeStyle, "defaultAnimalNarrativeStyle", AnimalNarrativeStyle.ThirdPerson);

            Scribe_Values.Look(ref enablePawnConversations,     "enablePawnConversations",     false);
            Scribe_Values.Look(ref conversationGlobalPrompt,    "conversationGlobalPrompt",    "");
            Scribe_Values.Look(ref conversationLinesPerPawn,    "conversationLinesPerPawn",    1);
            Scribe_Values.Look(ref conversationBubbleDelay,     "conversationBubbleDelay",     1.5f);
            Scribe_Values.Look(ref conversationCooldownHours,   "conversationCooldownHours",   6);
            Scribe_Values.Look(ref conversationAnimalMode,      "conversationAnimalMode",      ConversationAnimalMode.IntelligentOnly);
            Scribe_Values.Look(ref conversationMinOpinion,      "conversationMinOpinion",      -100);
            Scribe_Values.Look(ref conversationMaxColonySize,   "conversationMaxColonySize",   0);
            Scribe_Values.Look(ref conversationIncludePrisoners,"conversationIncludePrisoners",true);
            Scribe_Values.Look(ref conversationIncludeSlaves,   "conversationIncludeSlaves",   true);
            Scribe_Values.Look(ref conversationIncludeGuests,   "conversationIncludeGuests",   false);
            Scribe_Values.Look(ref conversationDisableAtSpeed,  "conversationDisableAtSpeed",  3);
            Scribe_Values.Look(ref conversationAllowSimultaneous,"conversationAllowSimultaneous",false);

            Scribe_Values.Look(ref enableMonologues,       "enableMonologues",       false);
            Scribe_Values.Look(ref monologueCooldownHours, "monologueCooldownHours", 4);
            Scribe_Values.Look(ref monologueChancePerHour, "monologueChancePerHour", 0.15f);
            Scribe_Values.Look(ref monologueMinMoodImpact, "monologueMinMoodImpact", 5f);

            Scribe_Values.Look(ref chatLogX,         "chatLogX",         0f);
            Scribe_Values.Look(ref chatLogY,         "chatLogY",         0f);
            Scribe_Values.Look(ref chatLogW,         "chatLogW",         0f);
            Scribe_Values.Look(ref chatLogH,         "chatLogH",         0f);
            Scribe_Values.Look(ref chatLogHotkey,    "chatLogHotkey",    KeyCode.None);
            Scribe_Values.Look(ref chatLogLargeFont, "chatLogLargeFont", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && string.IsNullOrEmpty(selectedModel))
            {
                if (modelPreferences != null && !modelPreferences.useAutoSelection)
                {
                    if (!string.IsNullOrEmpty(modelPreferences.preferredAdvancedModel))
                        selectedModel = modelPreferences.preferredAdvancedModel;
                    else if (!string.IsNullOrEmpty(modelPreferences.preferredFastModel))
                        selectedModel = modelPreferences.preferredFastModel;

                    if (!string.IsNullOrEmpty(selectedModel))
                        Log.Message($"[EchoColony] Migrated legacy model preference: {selectedModel}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        private void CleanupColonistSettings()
        {
            if (Current.Game == null || colonistMessageSettings == null) return;

            var validThingIDs = new HashSet<string>();
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                    validThingIDs.Add(pawn.ThingID);

            var toRemove = new List<string>();
            foreach (var key in colonistMessageSettings.Keys)
                if (!validThingIDs.Contains(key))
                    toRemove.Add(key);

            foreach (var key in toRemove)
                colonistMessageSettings.Remove(key);
        }

        public ColonistMessageSettings GetOrCreateColonistSettings(Pawn pawn)
        {
            if (pawn == null) return null;
            string key = pawn.ThingID;
            if (!colonistMessageSettings.ContainsKey(key))
                colonistMessageSettings[key] = ColonistMessageSettings.CreateDefault();
            return colonistMessageSettings[key];
        }

        public bool IsSpontaneousMessagesActive()  => spontaneousMessageMode != SpontaneousMessageMode.Disabled;
        public bool AreIncidentMessagesEnabled()    => spontaneousMessageMode == SpontaneousMessageMode.IncidentsOnly || spontaneousMessageMode == SpontaneousMessageMode.Full;
        public bool AreRandomMessagesEnabled()      => spontaneousMessageMode == SpontaneousMessageMode.RandomOnly    || spontaneousMessageMode == SpontaneousMessageMode.Full;
        public bool IsStorytellerMessagesActive()   => storytellerMessageMode != StorytellerMessageMode.Disabled;
        public bool AreStorytellerRandomMessagesEnabled()   => storytellerMessageMode == StorytellerMessageMode.RandomOnly   || storytellerMessageMode == StorytellerMessageMode.Full;
        public bool AreStorytellerIncidentMessagesEnabled() => storytellerMessageMode == StorytellerMessageMode.IncidentsOnly || storytellerMessageMode == StorytellerMessageMode.Full;

        public bool ArePawnConversationsActive()
        {
            if (!enablePawnConversations) return false;
            if (conversationDisableAtSpeed > 0 && Current.Game != null)
            {
                int speed = (int)Find.TickManager.CurTimeSpeed;
                if (speed >= conversationDisableAtSpeed) return false;
            }
            if (conversationMaxColonySize > 0 && Current.Game != null)
            {
                int colonySize = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists?.Count() ?? 0;
                if (colonySize > conversationMaxColonySize) return false;
            }
            return true;
        }

        public bool IsPawnEligibleForConversation(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
            if (pawn.IsPrisonerOfColony && !conversationIncludePrisoners) return false;
            if (pawn.IsSlaveOfColony    && !conversationIncludeSlaves)    return false;

            if (!pawn.RaceProps.Humanlike)
            {
                switch (conversationAnimalMode)
                {
                    case ConversationAnimalMode.Disabled:
                        return false;
                    case ConversationAnimalMode.IntelligentOnly:
                        if (!Animals.AnimalPromptManager.GetIsIntelligent(pawn)) return false;
                        break;
                }
            }

            if (!conversationIncludeGuests)
            {
                bool isColonyOwned = pawn.Faction == Faction.OfPlayer ||
                                     pawn.IsPrisonerOfColony           ||
                                     pawn.IsSlaveOfColony;
                if (!isColonyOwned) return false;
            }
            return true;
        }

        public bool IsVisionActive()
        {
            if (!enableVision) return false;
            return modelSource == ModelSource.Gemini    ||
                   modelSource == ModelSource.Player2   ||
                   modelSource == ModelSource.OpenRouter;
        }
    }
}