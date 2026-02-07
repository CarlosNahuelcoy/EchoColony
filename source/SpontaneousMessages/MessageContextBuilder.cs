using System.Text;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Construye el contexto específico para mensajes espontáneos
    /// basándose en el contexto base del colono + situación que trigger el mensaje
    /// </summary>
    public static class MessageContextBuilder
    {
        /// <summary>
        /// Construye el prompt completo para un mensaje espontáneo
        /// </summary>
        public static string BuildPrompt(MessageRequest request)
        {
            var sb = new StringBuilder();

            // 1. Contexto base del colono (usa el builder existente)
            string baseContext = ColonistPromptContextBuilder.Build(request.colonist, "");
            sb.AppendLine(baseContext);

            // 2. Separador claro
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();

            // 3. Instrucción específica sobre iniciar la conversación
            sb.AppendLine("CRITICAL INSTRUCTION:");
            sb.AppendLine("YOU are initiating this conversation with the player.");
            sb.AppendLine("The player has NOT said anything yet - YOU are starting the chat.");
            sb.AppendLine();

            // 4. Contexto específico del trigger
            string triggerContext = BuildTriggerContext(request);
            sb.AppendLine(triggerContext);

            // 5. Restricciones de formato
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

        /// <summary>
        /// Construye el contexto específico basado en el tipo de trigger
        /// </summary>
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
                    break;

                case TriggerType.Random:
                    sb.AppendLine("SITUATION:");
                    sb.AppendLine("You want to have a casual conversation with the player.");
                    sb.AppendLine("There's no specific emergency - just something on your mind.");
                    sb.AppendLine();
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Start a natural, casual conversation.");
                    sb.AppendLine("Topics could include:");
                    sb.AppendLine("- How you're feeling about colony life");
                    sb.AppendLine("- Something interesting that happened today");
                    sb.AppendLine("- Your thoughts about another colonist");
                    sb.AppendLine("- The weather, your work, or daily observations");
                    sb.AppendLine("- A question for the player");
                    sb.AppendLine();
                    sb.AppendLine("Keep it light and natural - like texting a friend.");
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
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Guía específica por tipo de incidente
        /// </summary>
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

        /// <summary>
        /// Construye instrucción de length específica basada en settings
        /// </summary>
        private static string BuildLengthInstruction()
        {
            // Para mensajes espontáneos, siempre queremos que sean breves
            // independientemente del maxResponseLength del setting general
            return "Keep your response to 2-3 sentences maximum. Be concise.";
        }
    }
}