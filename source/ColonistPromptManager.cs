using Verse;

namespace EchoColony
{
    public static class ColonistPromptManager
    {
        // Devuelve el prompt personalizado para un colono, o "" si no tiene uno
        public static string GetPrompt(Pawn pawn)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.promptsByColonist.TryGetValue(pawn.ThingID, out string prompt))
            {
                return prompt;
            }
            return "";
        }

        // Guarda o actualiza el prompt personalizado para un colono
        public static void SetPrompt(Pawn pawn, string prompt)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null)
            {
                comp.promptsByColonist[pawn.ThingID] = prompt;
            }
        }

        // Elimina el prompt personalizado para un colono
        public static void ClearPrompt(Pawn pawn)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.promptsByColonist.ContainsKey(pawn.ThingID))
            {
                comp.promptsByColonist.Remove(pawn.ThingID);
            }
        }

        // Verifica si un colono tiene prompt personalizado
        public static bool HasCustomPrompt(Pawn pawn)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            return comp != null && comp.promptsByColonist.ContainsKey(pawn.ThingID);
        }

        public static bool GetIgnoreAge(Pawn pawn)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            return comp != null && comp.ignoreAgeByColonist.TryGetValue(pawn.ThingID, out bool value) && value;
        }

        public static void SetIgnoreAge(Pawn pawn, bool value)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            if (comp != null)
                comp.ignoreAgeByColonist[pawn.ThingID] = value;
        }
        public static void ClearIgnoreAge(Pawn pawn)
        {
            var comp = Current.Game.GetComponent<PromptStorageComponent>();
            comp?.ignoreAgeByColonist.Remove(pawn.ThingID);
        }

    }
}
