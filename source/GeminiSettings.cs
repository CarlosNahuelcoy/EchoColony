using System.Collections.Generic;
using Verse;

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
        }
    }
}