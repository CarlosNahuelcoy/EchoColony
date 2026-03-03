using System.Collections.Generic;
using Verse;
using EchoColony.SpontaneousMessages;

namespace EchoColony
{
    public enum ModelSource
    {
        Player2,
        Gemini,
        Local,
        OpenRouter
    }

    public enum LocalModelProvider
    {
        LMStudio,
        Ollama,
        KoboldAI
    }

    public enum AnimalNarrativeStyle
    {
        ThirdPerson,  // "The dog wags its tail"
        FirstPerson   // "I wag my tail"
    }

    // Mantenido solo para migración de saves antiguos
    public class GeminiModelPreferences : IExposable
    {
        public string preferredFastModel = "";
        public string preferredAdvancedModel = "";
        public bool useAutoSelection = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref preferredFastModel, "preferredFastModel", "");
            Scribe_Values.Look(ref preferredAdvancedModel, "preferredAdvancedModel", "");
            Scribe_Values.Look(ref useAutoSelection, "useAutoSelection", true);
        }
    }

    public class GeminiSettings : ModSettings
    {
        public string apiKey = "";
        public string globalPrompt = "";
        public int maxResponseLength = 300;

        public bool enableSocialAffectsPersonality = true;
        public bool enableRoleplayResponses = true;

        // Memory system
        public bool enableMemorySystem = true;

        // Model source
        public ModelSource modelSource = ModelSource.Player2;

        // ── Campo unificado de selección de modelo Gemini ──
        // Este es el único campo que se usa ahora para determinar qué modelo usar.
        // Los campos de modelPreferences se mantienen solo para leer saves viejos y migrarlos.
        public string selectedModel = "";

        // LEGACY: mantenidos para leer saves existentes y migrar — no usar en código nuevo
        public GeminiModelPreferences modelPreferences = new GeminiModelPreferences();
        public bool useAdvancedModel = false;

        // Local model settings
        public string localModelEndpoint = "http://localhost:11434/api/generate";
        public string localModelName = "llama3.2:latest";
        public LocalModelProvider localModelProvider = LocalModelProvider.LMStudio;

        // OpenRouter settings
        public string openRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
        public string openRouterApiKey = "";
        public string openRouterModel = "mistral-7b";

        public bool debugMode = false;

        public bool enableTTS = true;
        public bool autoPlayVoice = true;

        public bool ignoreDangersInConversations = false;
        public Dictionary<string, string> colonistVoices = new Dictionary<string, string>();

        public bool enableDivineActions = true;
        public bool allowNegativeActions = false;
        public bool allowExtremeActions = false;

        public bool enableStorytellerButton = true;

        // ===== ANIMAL SETTING =====
        public AnimalNarrativeStyle defaultAnimalNarrativeStyle = AnimalNarrativeStyle.ThirdPerson;

        // ===== SPONTANEOUS MESSAGES SYSTEM =====
        public SpontaneousMessageMode spontaneousMessageMode = SpontaneousMessageMode.Disabled;
        public int defaultMaxMessagesPerColonistPerDay = 1;
        public float defaultColonistCooldownHours = 12f;
        public float randomMessageIntervalHours = 4f;
        public bool prioritizeSocialTraits = true;
        public float minConsciousnessPercent = 50f;

        // ===== STORYTELLER SPONTANEOUS MESSAGES SYSTEM =====
        public StorytellerMessageMode storytellerMessageMode = StorytellerMessageMode.Disabled;
        public float storytellerRandomIntervalMinutes = 30f;
        public float storytellerIncidentChance = 0.3f;
        public bool storytellerMessageAutoClose = true;
        public float storytellerMessageAutoCloseSeconds = 8f;
        public bool storytellerMessagePlaySound = true;

        public enum StorytellerMessageMode
        {
            Disabled,
            RandomOnly,
            IncidentsOnly,
            Full
        }

        // Configuraciones individuales por colono (usa ThingID como key)
        public Dictionary<string, ColonistMessageSettings> colonistMessageSettings = new Dictionary<string, ColonistMessageSettings>();

        // ═══════════════════════════════════════════════════════════════
        // EXPOSE DATA
        // ═══════════════════════════════════════════════════════════════

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref apiKey, "GeminiApiKey", "");
            Scribe_Values.Look(ref globalPrompt, "GlobalPrompt", "");
            Scribe_Values.Look(ref maxResponseLength, "MaxResponseLength", 300);
            Scribe_Values.Look(ref enableSocialAffectsPersonality, "EnableSocialAffectsPersonality", true);
            Scribe_Values.Look(ref enableRoleplayResponses, "EnableRoleplayResponses", true);

            Scribe_Values.Look(ref enableMemorySystem, "EnableMemorySystem", true);

            Scribe_Values.Look(ref modelSource, "ModelSource", ModelSource.Player2);

            // Campo unificado — el único que importa ahora
            Scribe_Values.Look(ref selectedModel, "selectedModel", "");

            // Legacy fields — se leen para poder migrar saves viejos en PostLoadInit
            if (modelPreferences == null) modelPreferences = new GeminiModelPreferences();
            Scribe_Deep.Look(ref modelPreferences, "modelPreferences");
            Scribe_Values.Look(ref useAdvancedModel, "UseAdvancedModel", false);

            Scribe_Values.Look(ref localModelEndpoint, "LocalModelEndpoint", "http://localhost:11434/api/generate");
            Scribe_Values.Look(ref localModelName, "LocalModelName", "llama3.2:latest");
            Scribe_Values.Look(ref localModelProvider, "localModelProvider", LocalModelProvider.LMStudio);

            Scribe_Values.Look(ref openRouterEndpoint, "OpenRouterEndpoint", "https://openrouter.ai/api/v1/chat/completions");
            Scribe_Values.Look(ref openRouterApiKey, "OpenRouterApiKey", "");
            Scribe_Values.Look(ref openRouterModel, "OpenRouterModel", "mistral-7b");

            Scribe_Values.Look(ref enableTTS, "EnableTTS", true);
            Scribe_Values.Look(ref autoPlayVoice, "AutoPlayVoice", true);
            Scribe_Collections.Look(ref colonistVoices, "ColonistVoices", LookMode.Value, LookMode.Value);

            Scribe_Values.Look(ref debugMode, "DebugMode", false);
            Scribe_Values.Look(ref ignoreDangersInConversations, "IgnoreDangersInConversations", false);

            Scribe_Values.Look(ref enableDivineActions, "enableDivineActions", true);
            Scribe_Values.Look(ref allowNegativeActions, "allowNegativeActions", false);
            Scribe_Values.Look(ref allowExtremeActions, "allowExtremeActions", false);

            Scribe_Values.Look(ref enableStorytellerButton, "enableStorytellerButton", true);

            Scribe_Values.Look(ref spontaneousMessageMode, "spontaneousMessageMode", SpontaneousMessageMode.Disabled);
            Scribe_Values.Look(ref defaultMaxMessagesPerColonistPerDay, "defaultMaxMessagesPerColonistPerDay", 1);
            Scribe_Values.Look(ref defaultColonistCooldownHours, "defaultColonistCooldownHours", 12f);
            Scribe_Values.Look(ref randomMessageIntervalHours, "randomMessageIntervalHours", 36f);
            Scribe_Values.Look(ref prioritizeSocialTraits, "prioritizeSocialTraits", true);
            Scribe_Values.Look(ref minConsciousnessPercent, "minConsciousnessPercent", 50f);

            if (Scribe.mode == LoadSaveMode.Saving)
                CleanupColonistSettings();

            Scribe_Collections.Look(ref colonistMessageSettings, "colonistMessageSettings", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars && colonistMessageSettings == null)
                colonistMessageSettings = new Dictionary<string, ColonistMessageSettings>();

            Scribe_Values.Look(ref storytellerMessageMode, "storytellerMessageMode", StorytellerMessageMode.Disabled);
            Scribe_Values.Look(ref storytellerRandomIntervalMinutes, "storytellerRandomIntervalMinutes", 30f);
            Scribe_Values.Look(ref storytellerIncidentChance, "storytellerIncidentChance", 0.3f);
            Scribe_Values.Look(ref storytellerMessageAutoClose, "storytellerMessageAutoClose", true);
            Scribe_Values.Look(ref storytellerMessageAutoCloseSeconds, "storytellerMessageAutoCloseSeconds", 8f);
            Scribe_Values.Look(ref storytellerMessagePlaySound, "storytellerMessagePlaySound", true);

            Scribe_Values.Look(ref defaultAnimalNarrativeStyle, "defaultAnimalNarrativeStyle", AnimalNarrativeStyle.ThirdPerson);

            // ── Migración de saves viejos ──
            // Si selectedModel está vacío pero había algo en los campos legacy, migrarlo.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && string.IsNullOrEmpty(selectedModel))
            {
                if (modelPreferences != null && !modelPreferences.useAutoSelection)
                {
                    if (!string.IsNullOrEmpty(modelPreferences.preferredAdvancedModel))
                    {
                        selectedModel = modelPreferences.preferredAdvancedModel;
                        Log.Message($"[EchoColony] Migrated legacy model preference to selectedModel: {selectedModel}");
                    }
                    else if (!string.IsNullOrEmpty(modelPreferences.preferredFastModel))
                    {
                        selectedModel = modelPreferences.preferredFastModel;
                        Log.Message($"[EchoColony] Migrated legacy model preference to selectedModel: {selectedModel}");
                    }
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
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                    validThingIDs.Add(pawn.ThingID);
            }

            var toRemove = new List<string>();
            foreach (var key in colonistMessageSettings.Keys)
            {
                if (!validThingIDs.Contains(key))
                    toRemove.Add(key);
            }

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

        public bool IsSpontaneousMessagesActive()
        {
            return spontaneousMessageMode != SpontaneousMessageMode.Disabled;
        }

        public bool AreIncidentMessagesEnabled()
        {
            return spontaneousMessageMode == SpontaneousMessageMode.IncidentsOnly ||
                   spontaneousMessageMode == SpontaneousMessageMode.Full;
        }

        public bool AreRandomMessagesEnabled()
        {
            return spontaneousMessageMode == SpontaneousMessageMode.RandomOnly ||
                   spontaneousMessageMode == SpontaneousMessageMode.Full;
        }

        public bool IsStorytellerMessagesActive()
        {
            return storytellerMessageMode != StorytellerMessageMode.Disabled;
        }

        public bool AreStorytellerRandomMessagesEnabled()
        {
            return storytellerMessageMode == StorytellerMessageMode.RandomOnly ||
                   storytellerMessageMode == StorytellerMessageMode.Full;
        }

        public bool AreStorytellerIncidentMessagesEnabled()
        {
            return storytellerMessageMode == StorytellerMessageMode.IncidentsOnly ||
                   storytellerMessageMode == StorytellerMessageMode.Full;
        }
    }
}