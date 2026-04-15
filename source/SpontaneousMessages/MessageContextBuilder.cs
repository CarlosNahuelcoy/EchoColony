using System.Text;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Construye el contexto específico para mensajes espontáneos
    /// basándose en el contexto base del colono + situación que trigger el mensaje.
    ///
    /// Los tales verificados (eventos reales de la colonia) llegan automáticamente
    /// a través de ColonistPromptContextBuilder.Build(), que ya los incluye.
    /// Este builder añade un nudge específico para que el colono los USE activamente
    /// al iniciar la conversación — especialmente en mensajes casuales.
    /// </summary>
    public static class MessageContextBuilder
    {
        public static string BuildPrompt(MessageRequest request)
        {
            var sb = new StringBuilder();

            // 1. Contexto base del colono — incluye tales verificados via TalesCache
            string baseContext = ColonistPromptContextBuilder.Build(request.colonist, "");
            sb.AppendLine(baseContext);

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();

            // 2. Instrucción sobre quién inicia
            sb.AppendLine("CRITICAL INSTRUCTION:");
            sb.AppendLine("YOU are initiating this conversation with the player.");
            sb.AppendLine("The player has NOT said anything yet — YOU are starting the chat.");
            sb.AppendLine();

            // 3. Contexto específico del trigger
            sb.AppendLine(BuildTriggerContext(request));

            // 4. Formato
            sb.AppendLine();
            sb.AppendLine("RESPONSE REQUIREMENTS:");
            sb.AppendLine("- Keep it BRIEF: 2-3 sentences maximum");
            sb.AppendLine("- Be NATURAL and conversational (not theatrical)");
            sb.AppendLine("- Start directly with what you want to say (no preamble)");
            sb.AppendLine("- Do NOT use actions in asterisks or roleplay formatting");
            sb.AppendLine("- Speak AS the colonist, not ABOUT the colonist");
            sb.AppendLine();
            sb.AppendLine("Example of good opening:");
            sb.AppendLine("\"Hey, I wanted to talk to you about something that's been on my mind...\"");
            sb.AppendLine();
            sb.AppendLine("Example of bad opening:");
            sb.AppendLine("\"*walks over nervously* Um, hello there commander...\"");

            return sb.ToString();
        }

        private static string BuildTriggerContext(MessageRequest request)
        {
            var sb = new StringBuilder();

            switch (request.triggerType)
            {
                case TriggerType.Incident:
                    sb.AppendLine("SITUATION:");
                    sb.AppendLine($"An incident just occurred: {request.contextDescription}");
                    sb.AppendLine();
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Reach out to the player about this situation.");
                    sb.AppendLine(GetIncidentSpecificGuidance(request.incidentTrigger));
                    sb.AppendLine();
                    sb.AppendLine("If your Verified Personal History contains a related past event,");
                    sb.AppendLine("you MAY reference it briefly — e.g. 'Last time something like this");
                    sb.AppendLine("happened, we barely made it.' Only do this if it fits naturally.");
                    break;

                case TriggerType.Random:
                    sb.AppendLine("SITUATION:");
                    sb.AppendLine("You want to have a casual conversation with the player.");
                    sb.AppendLine("There's no specific emergency — just something on your mind.");
                    sb.AppendLine();
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Start a natural, casual conversation. Choose ONE of these approaches:");
                    sb.AppendLine();
                    sb.AppendLine("APPROACH A — Reference a real past event (PREFERRED when history exists):");
                    sb.AppendLine("  Look at your Verified Personal History above.");
                    sb.AppendLine("  Pick ONE event that still feels meaningful and bring it up naturally.");
                    sb.AppendLine("  Examples:");
                    sb.AppendLine("  'Hey, I keep thinking about that hunt... [brief reference to actual event]'");
                    sb.AppendLine("  'You know, after what happened with [real event], I've been wondering...'");
                    sb.AppendLine("  'I never really told you how I felt about [real past situation].'");
                    sb.AppendLine("  RULE: ONLY reference events that appear in your Verified Personal History.");
                    sb.AppendLine("  NEVER invent past events — if the history is empty, use Approach B.");
                    sb.AppendLine();
                    sb.AppendLine("APPROACH B — Present-focused casual conversation (use when no relevant history):");
                    sb.AppendLine("  - How you're feeling about colony life right now");
                    sb.AppendLine("  - Something you noticed or thought about today");
                    sb.AppendLine("  - Your current work or the weather");
                    sb.AppendLine("  - A question for the player");
                    sb.AppendLine();
                    sb.AppendLine("Keep it light and natural — like texting a friend.");
                    break;

                case TriggerType.CriticalNeed:
                    sb.AppendLine("URGENT SITUATION:");
                    sb.AppendLine($"You urgently need to tell the player: {request.contextDescription}");
                    sb.AppendLine();
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Inform them directly but don't be overly dramatic.");
                    sb.AppendLine("Be clear and to the point.");
                    break;

                case TriggerType.ColonySituation:
                    sb.AppendLine("COLONY CONCERN:");
                    sb.AppendLine($"You've noticed: {request.contextDescription}");
                    sb.AppendLine();
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Alert the player to this situation.");
                    sb.AppendLine("Express your concern naturally.");
                    sb.AppendLine();
                    sb.AppendLine("If your Verified Personal History contains something relevant,");
                    sb.AppendLine("you may reference it to add weight — but only if it truly fits.");
                    break;
            }

            return sb.ToString();
        }

        private static string GetIncidentSpecificGuidance(IncidentTrigger trigger)
        {
            switch (trigger)
            {
                case IncidentTrigger.Raid:
                case IncidentTrigger.MechanoidCluster:
                case IncidentTrigger.InfestationSpawned:
                    return "Express your reaction to the threat. Are you scared? Ready to fight? Worried about others?";

                case IncidentTrigger.ToxicFallout:
                case IncidentTrigger.SolarFlare:
                    return "Express concern about this environmental hazard and how it might affect the colony.";

                case IncidentTrigger.TraderCaravan:
                    return "Share your thoughts about the traders arriving. Excited? Curious? Need something specific?";

                case IncidentTrigger.MeteoriteIncoming:
                    return "React to the danger of the incoming meteorite. Brief and immediate.";

                case IncidentTrigger.Eclipse:
                case IncidentTrigger.Aurora:
                    return "Comment on this astronomical event. Maybe it's beautiful, maybe concerning.";

                case IncidentTrigger.Manhunter:
                    return "Express concern about the dangerous animals. Keep it brief.";

                case IncidentTrigger.WandererJoin:
                case IncidentTrigger.RefugeeChased:
                    return "Share your thoughts about the newcomer. Welcoming? Suspicious? Curious?";

                case IncidentTrigger.TransportPodCrash:
                    return "React to the crash. What should we do about it?";

                default:
                    return "Express your natural reaction to what's happening.";
            }
        }
    }
}