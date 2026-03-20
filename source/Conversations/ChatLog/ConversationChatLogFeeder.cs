using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Feeds conversation bubble lines into ConversationChatLog.
    /// Call PushLine() / PushMonologue() from the managers.
    /// Call PushSeparator() once before each new conversation group.
    /// </summary>
    public static class ConversationChatLogFeeder
    {
        public static void PushLine(Pawn speaker, string text)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(text)) return;
            if (MyMod.Settings?.enablePawnConversations != true) return;

            ConversationChatLog.PushLine(GetTimestamp(speaker), speaker.LabelShortCap ?? "?", text);
        }

        public static void PushMonologue(Pawn speaker, string text)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(text)) return;
            if (MyMod.Settings?.enableMonologues != true) return;

            ConversationChatLog.PushMonologue(GetTimestamp(speaker), speaker.LabelShortCap ?? "?", text);
        }

        /// <summary>
        /// Insert a separator line between conversation groups.
        /// Call once per conversation, before pushing any lines.
        /// </summary>
        public static void PushSeparator() => ConversationChatLog.PushSeparator();

        // ── Helper ────────────────────────────────────────────────────────────

        private static string GetTimestamp(Pawn pawn)
        {
            try
            {
                var map = pawn?.Map ?? Find.CurrentMap;
                if (map != null)
                {
                    int hour = GenLocalDate.HourOfDay(map);
                    return $"[{hour:D2}h] ";
                }
            }
            catch { }
            return "";
        }
    }
}