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

    // ✅ NUEVO: Configuración granular de modelos
    public class GeminiModelPreferences : IExposable
    {
        public string preferredFastModel = "";      // Modelo rápido específico elegido
        public string preferredAdvancedModel = "";  // Modelo avanzado específico elegido
        public bool useAutoSelection = true;        // Si true, ignora las preferencias y usa auto
        
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

        // ✅ NUEVO: Sistema de memorias opcional
        public bool enableMemorySystem = true;

        // Fuente del modelo
        public ModelSource modelSource = ModelSource.Player2;

        // ✅ NUEVO: Preferencias de modelos Gemini
        public GeminiModelPreferences modelPreferences = new GeminiModelPreferences();

        // Gemini (DEPRECATED: useAdvancedModel - mantenido para compatibilidad)
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

        // ✅ CORREGIDO: Método para obtener el modelo a usar considerando preferencias
        public bool ShouldUseAdvancedModel()
        {
            if (modelPreferences.useAutoSelection)
            {
                return useAdvancedModel; // Comportamiento automático - usa el toggle Fast/Advanced
            }
            
            // En modo manual, determinar basándose en qué modelo específico tiene configurado
            bool hasFastPreference = !string.IsNullOrEmpty(modelPreferences.preferredFastModel);
            bool hasAdvancedPreference = !string.IsNullOrEmpty(modelPreferences.preferredAdvancedModel);
            
            // Si solo tiene configurado un tipo, usar ese
            if (hasAdvancedPreference && !hasFastPreference)
                return true;  // Solo tiene advanced configurado
            if (hasFastPreference && !hasAdvancedPreference)
                return false; // Solo tiene fast configurado
                
            // Si tiene ambos o ninguno configurado, usar el toggle como fallback
            return useAdvancedModel;
        }

        public string GetPreferredModel(bool isAdvanced)
        {
            if (modelPreferences.useAutoSelection)
            {
                return null; // Usar detección automática
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
            
            // ✅ NUEVO: Guardar configuración de memorias
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

            // ✅ NUEVO: Guardar preferencias de modelos
            if (modelPreferences == null) modelPreferences = new GeminiModelPreferences();
            Scribe_Deep.Look(ref modelPreferences, "modelPreferences");

            // Mantener compatibilidad con versiones anteriores
            Scribe_Values.Look(ref useAdvancedModel, "UseAdvancedModel", false);
            
            // ✅ Migración de datos legacy
            if (Scribe.mode == LoadSaveMode.PostLoadInit && modelPreferences.useAutoSelection)
            {
                // Si es la primera vez que carga con el nuevo sistema, mantener comportamiento anterior
                // No hacer nada, useAutoSelection = true mantiene el comportamiento legacy
            }
        }
    }
}