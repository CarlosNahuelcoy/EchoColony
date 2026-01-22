using Verse;

namespace EchoColony
{
    /// <summary>
    /// Gestiona prompts personalizados para el storyteller
    /// </summary>
    public static class StorytellerPromptManager
    {
        private const string STORYTELLER_KEY = "storyteller_custom_prompt";
        
        // Devuelve el prompt personalizado para el storyteller actual
        public static string GetPrompt()
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.promptsByColonist.TryGetValue(STORYTELLER_KEY, out string prompt))
            {
                return prompt;
            }
            return "";
        }

        // Guarda o actualiza el prompt personalizado para el storyteller
        public static void SetPrompt(string prompt)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null)
            {
                comp.promptsByColonist[STORYTELLER_KEY] = prompt;
            }
        }

        // Elimina el prompt personalizado
        public static void ClearPrompt()
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.promptsByColonist.ContainsKey(STORYTELLER_KEY))
            {
                comp.promptsByColonist.Remove(STORYTELLER_KEY);
            }
        }

        // Verifica si hay prompt personalizado
        public static bool HasCustomPrompt()
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            return comp != null && comp.promptsByColonist.ContainsKey(STORYTELLER_KEY);
        }
    }
}