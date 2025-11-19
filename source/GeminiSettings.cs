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

    public class GeminiSettings : ModSettings
    {
        public string apiKey = "";
        public string globalPrompt = "";
        public int maxResponseLength = 300;

        public bool enableSocialAffectsPersonality = true;
        public bool enableRoleplayResponses = true;

        // Fuente del modelo
        public ModelSource modelSource = ModelSource.Player2;

        // Gemini
        public bool useAdvancedModel = false;

        // Local
        public string localModelEndpoint = "http://localhost:11434/api/generate";
        public string localModelName = "llama3.2:latest";
        public LocalModelProvider localModelProvider = LocalModelProvider.LMStudio;

        // OpenRouter
        public string openRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
        public string openRouterApiKey = "";
        public string openRouterModel = "mistral-7b";

        public bool debugMode = false;

        public bool enableTTS = true;
        public bool autoPlayVoice = true;

        public bool ignoreDangersInConversations = false;
        public Dictionary<string, string> colonistVoices = new Dictionary<string, string>(); // PawnName -> VoiceId


        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref apiKey, "GeminiApiKey", "");
            Scribe_Values.Look(ref globalPrompt, "GlobalPrompt", "");
            Scribe_Values.Look(ref maxResponseLength, "MaxResponseLength", 300);
            Scribe_Values.Look(ref enableSocialAffectsPersonality, "EnableSocialAffectsPersonality", true);
            Scribe_Values.Look(ref enableRoleplayResponses, "EnableRoleplayResponses", true);
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

        }
    }
}
