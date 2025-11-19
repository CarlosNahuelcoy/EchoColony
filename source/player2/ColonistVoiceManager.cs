using Verse;

namespace EchoColony
{
    public static class ColonistVoiceManager
    {
        public static string GetVoice(Pawn pawn)
        {
            var comp = Current.Game?.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.voicesByColonist.TryGetValue(pawn.ThingID, out string voice))
            {
                return voice;
            }
            return null;
        }

        public static void SetVoice(Pawn pawn, string voiceId)
        {
            var comp = Current.Game?.GetComponent<PromptStorageComponent>();
            if (comp != null)
            {
                comp.voicesByColonist[pawn.ThingID] = voiceId;
            }
        }

        public static void ClearVoice(Pawn pawn)
        {
            var comp = Current.Game?.GetComponent<PromptStorageComponent>();
            if (comp != null && comp.voicesByColonist.ContainsKey(pawn.ThingID))
            {
                comp.voicesByColonist.Remove(pawn.ThingID);
            }
        }

        public static bool HasVoice(Pawn pawn)
        {
            var comp = Current.Game?.GetComponent<PromptStorageComponent>();
            return comp != null && comp.voicesByColonist.ContainsKey(pawn.ThingID);
        }
    }
}
