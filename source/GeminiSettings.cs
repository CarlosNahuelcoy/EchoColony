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

    // Granular model configuration
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

        // Optional memory system
        public bool enableMemorySystem = true;

        // Model source
        public ModelSource modelSource = ModelSource.Player2;

        // Gemini model preferences
        public GeminiModelPreferences modelPreferences = new GeminiModelPreferences();

        // DEPRECATED: useAdvancedModel - kept for backward compatibility
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

        // NEW: Control storyteller button visibility
        public bool enableStorytellerButton = true;

        // Determine if advanced model should be used based on preferences
        public bool ShouldUseAdvancedModel()
        {
            if (modelPreferences.useAutoSelection)
            {
                return useAdvancedModel;
            }
            
            bool hasFastPreference = !string.IsNullOrEmpty(modelPreferences.preferredFastModel);
            bool hasAdvancedPreference = !string.IsNullOrEmpty(modelPreferences.preferredAdvancedModel);
            
            if (hasAdvancedPreference && !hasFastPreference)
                return true;
            if (hasFastPreference && !hasAdvancedPreference)
                return false;
                
            return useAdvancedModel;
        }

        public string GetPreferredModel(bool isAdvanced)
        {
            if (modelPreferences.useAutoSelection)
            {
                return null;
            }
            
            return isAdvanced ? modelPreferences.preferredAdvancedModel : modelPreferences.preferredFastModel;
        }

        // ===== SPONTANEOUS MESSAGES SYSTEM =====
        public SpontaneousMessageMode spontaneousMessageMode = SpontaneousMessageMode.Disabled;
        public int defaultMaxMessagesPerColonistPerDay = 1; // 1-3
        public float defaultColonistCooldownHours = 12f; // 1-48
        public float randomMessageIntervalHours = 4f; // Para modo Random/Full
        public bool prioritizeSocialTraits = true;
        public float minConsciousnessPercent = 50f;

        // ===== STORYTELLER SPONTANEOUS MESSAGES SYSTEM =====
public StorytellerMessageMode storytellerMessageMode = StorytellerMessageMode.Disabled;
public float storytellerRandomIntervalMinutes = 30f; // Para modo Random/Full
public float storytellerIncidentChance = 0.3f; // 0-1 probabilidad de comentar en incidentes
public bool storytellerMessageAutoClose = true;
public float storytellerMessageAutoCloseSeconds = 8f;
public bool storytellerMessagePlaySound = true;

// Métodos helper
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

        public enum StorytellerMessageMode
        {
            Disabled,
            RandomOnly,
            IncidentsOnly,
            Full
        }

        // Configuraciones individuales por colono (usa ThingID como key)
        public Dictionary<string, ColonistMessageSettings> colonistMessageSettings = new Dictionary<string, ColonistMessageSettings>();

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

            if (modelPreferences == null) modelPreferences = new GeminiModelPreferences();
            Scribe_Deep.Look(ref modelPreferences, "modelPreferences");

            Scribe_Values.Look(ref useAdvancedModel, "UseAdvancedModel", false);

            Scribe_Values.Look(ref enableDivineActions, "enableDivineActions", true);
            Scribe_Values.Look(ref allowNegativeActions, "allowNegativeActions", false);
            Scribe_Values.Look(ref allowExtremeActions, "allowExtremeActions", false);
            
            Scribe_Values.Look(ref enableStorytellerButton, "enableStorytellerButton", true);
            
            // Legacy data migration
            if (Scribe.mode == LoadSaveMode.PostLoadInit && modelPreferences.useAutoSelection)
            {
                // First load with new system maintains legacy behavior via useAutoSelection = true
            }

            Scribe_Values.Look(ref spontaneousMessageMode, "spontaneousMessageMode", SpontaneousMessageMode.Disabled);
            Scribe_Values.Look(ref defaultMaxMessagesPerColonistPerDay, "defaultMaxMessagesPerColonistPerDay", 1);
            Scribe_Values.Look(ref defaultColonistCooldownHours, "defaultColonistCooldownHours", 12f);
            Scribe_Values.Look(ref randomMessageIntervalHours, "randomMessageIntervalHours", 36f);
            Scribe_Values.Look(ref prioritizeSocialTraits, "prioritizeSocialTraits", true);
            Scribe_Values.Look(ref minConsciousnessPercent, "minConsciousnessPercent", 50f);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Limpiar entradas de colonos que ya no existen
                CleanupColonistSettings();
            }
            Scribe_Collections.Look(ref colonistMessageSettings, "colonistMessageSettings", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && colonistMessageSettings == null)
            {
                colonistMessageSettings = new Dictionary<string, ColonistMessageSettings>();
            }

            Scribe_Values.Look(ref storytellerMessageMode, "storytellerMessageMode", StorytellerMessageMode.Disabled);
            Scribe_Values.Look(ref storytellerRandomIntervalMinutes, "storytellerRandomIntervalMinutes", 30f);
            Scribe_Values.Look(ref storytellerIncidentChance, "storytellerIncidentChance", 0.3f);
            Scribe_Values.Look(ref storytellerMessageAutoClose, "storytellerMessageAutoClose", true);
            Scribe_Values.Look(ref storytellerMessageAutoCloseSeconds, "storytellerMessageAutoCloseSeconds", 8f);
            Scribe_Values.Look(ref storytellerMessagePlaySound, "storytellerMessagePlaySound", true);
        }

        /// <summary>
/// Limpia configuraciones de colonos que ya no existen en el juego
/// </summary>
private void CleanupColonistSettings()
{
    if (Current.Game == null || colonistMessageSettings == null) return;
    
    var validThingIDs = new HashSet<string>();
    foreach (var map in Find.Maps)
    {
        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            validThingIDs.Add(pawn.ThingID);
        }
    }
    
    var toRemove = new List<string>();
    foreach (var key in colonistMessageSettings.Keys)
    {
        if (!validThingIDs.Contains(key))
        {
            toRemove.Add(key);
        }
    }
    
    foreach (var key in toRemove)
    {
        colonistMessageSettings.Remove(key);
    }
}

/// <summary>
/// Obtiene o crea configuración para un colono específico
/// </summary>
public ColonistMessageSettings GetOrCreateColonistSettings(Pawn pawn)
{
    if (pawn == null) return null;
    
    string key = pawn.ThingID;
    if (!colonistMessageSettings.ContainsKey(key))
    {
        colonistMessageSettings[key] = ColonistMessageSettings.CreateDefault();
    }
    
    return colonistMessageSettings[key];
}

/// <summary>
/// Verifica si el sistema de mensajes espontáneos está activo
/// </summary>
public bool IsSpontaneousMessagesActive()
{
    return spontaneousMessageMode != SpontaneousMessageMode.Disabled;
}

/// <summary>
/// Verifica si los mensajes por incidentes están habilitados
/// </summary>
public bool AreIncidentMessagesEnabled()
{
    return spontaneousMessageMode == SpontaneousMessageMode.IncidentsOnly ||
           spontaneousMessageMode == SpontaneousMessageMode.Full;
}

/// <summary>
/// Verifica si los mensajes aleatorios están habilitados
/// </summary>
public bool AreRandomMessagesEnabled()
{
    return spontaneousMessageMode == SpontaneousMessageMode.RandomOnly ||
           spontaneousMessageMode == SpontaneousMessageMode.Full;
}
    }
    
}