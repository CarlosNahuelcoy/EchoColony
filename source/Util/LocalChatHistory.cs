using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Turns a colonist's chat log into plain "Player: ..." / "Name: ..." lines for the local
    /// model prompt builders (KoboldAI, LM Studio).
    /// The raw log uses [USER] / [ASSISTANT] tags. Pasted as-is, ParsePromptToMessages splits
    /// the prompt at those tags, so every instruction after the history ended up inside the
    /// last [ASSISTANT] message, and models repeated those instructions in their reply.
    /// </summary>
    public static class LocalChatHistory
    {
        public static string Format(Pawn pawn, int maxLines, string currentUserMessage = null)
        {
            if (pawn == null || ChatGameComponent.Instance == null) return string.Empty;

            string userPrefix     = "EchoColony.UserPrefix".Translate().ToString().Trim();
            string colonistPrefix = pawn.LabelShort + ":";
            var    lines          = new List<string>();

            foreach (string raw in ChatGameComponent.Instance.GetChat(pawn))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("[DATE_SEPARATOR]") || raw.StartsWith("<color")) continue;

                if (raw.StartsWith("[USER]"))
                {
                    string text = raw.Substring(6).Trim();
                    if (!string.IsNullOrEmpty(userPrefix) && text.StartsWith(userPrefix))
                        text = text.Substring(userPrefix.Length).Trim();
                    if (text.StartsWith(":")) text = text.Substring(1).Trim();
                    if (string.IsNullOrEmpty(text)) continue;

                    // Spontaneous messages log a narration line as the "player" turn
                    lines.Add(text.StartsWith("*(") ? text : "Player: " + text);
                    continue;
                }

                string reply = raw.StartsWith("[ASSISTANT]") ? raw.Substring(11).TrimStart() : raw;
                if (!reply.StartsWith(colonistPrefix)) continue;

                string body = reply.Substring(colonistPrefix.Length).Trim();
                if (string.IsNullOrEmpty(body) || body == "...") continue; // pending-reply placeholder

                lines.Add(pawn.LabelShort + ": " + body);
            }

            // The message being answered is already in the prompt as "Now the player says"
            if (!string.IsNullOrWhiteSpace(currentUserMessage) && lines.Count > 0 &&
                lines[lines.Count - 1] == "Player: " + currentUserMessage.Trim())
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return string.Join("\n", lines.Skip(System.Math.Max(0, lines.Count - maxLines)));
        }
    }
}
