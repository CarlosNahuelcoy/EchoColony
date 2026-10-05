using System.Text;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Builds the specific context for spontaneous messages
    /// based on the colonist's base context + the situation that triggered the message.
    ///
    /// Verified tales (real colony events) arrive automatically
    /// through ColonistPromptContextBuilder.Build(), which already includes them.
    /// This builder adds a specific nudge for the colonist to USE them actively
    /// when initiating the conversation — especially in casual messages.
    /// </summary>
    public static class MessageContextBuilder
    {
        public static string BuildPrompt(MessageRequest request)
        {
            var sb = new StringBuilder();

            //furel - Check if the session has expired for the colonist. If so, reset the session to don't include chat history to the prompt.
            bool sessionExpired = ChatGameComponent.Instance.ShouldResetSession(request.colonist);
            var info = ChatGameComponent.Instance.GetInteractionInfo(request.colonist);

            if (sessionExpired)
            {
                ChatGameComponent.Instance.UpdateStartTurn(request.colonist, info.CurrentTurn);
                ChatGameComponent.Instance.UpdateInteractionTick(request.colonist);
            }

            string groupContext = !sessionExpired ? GroupChatGameComponent.Instance.BuildGroupChatContextString(request.colonist) : "";

            if (!string.IsNullOrEmpty(groupContext))
            {
                ChatGameComponent.Instance.ClearGroupHistory(request.colonist);
            }

            // 1. Base colonist context — includes verified tales via TalesCache
            string baseContext = ColonistPromptContextBuilder.Build(request.colonist, "");
            sb.AppendLine(baseContext);

            //furel - User opening message is now included in the base context.
            // 2. ABRIR EXPLÍCITAMENTE EL NUEVO TURNO DE USUARIO
            // Esto evita que las instrucciones se peguen al último [ASSISTANT] del historial.
            sb.AppendLine("[USER]");
            sb.AppendLine();

            // INYECCIÓN DEL CHAT GRUPAL: Se coloca aquí para dar contexto previo a la iniciativa del colono
            if (!string.IsNullOrEmpty(groupContext))
            {
                sb.AppendLine(groupContext);
                sb.AppendLine();
            }

            // 3. Inyectar la entrada sintética en 2ª persona que describe la iniciativa del colono
            string eventLogText = SpontaneousMessageGenerator.TextEventRegistration(request);
            sb.AppendLine(eventLogText);
            sb.AppendLine();

            // 2. Pending response context — inject awareness if a previous message went unanswered
            var colonistTracker = SpontaneousMessageTracker.Instance?.GetTrackerFor(request.colonist);
            if (colonistTracker?.hasPendingResponse == true && !string.IsNullOrWhiteSpace(colonistTracker.lastSentMessage))
            {
                sb.AppendLine("IMPORTANT CONTEXT:");
                sb.AppendLine($"You already reached out and said: \"{colonistTracker.lastSentMessage}\"");
                sb.AppendLine("You have not received a response yet.");
                sb.AppendLine("You may follow up on that, or bring up something new — but do not pretend the previous message never happened.");
                sb.AppendLine();
            }

            // 3. Recent topics — prevent the AI from repeating content already brought up
            if (colonistTracker?.recentTopics?.Count > 0)
            {
                sb.AppendLine("TOPICS YOU HAVE ALREADY BROUGHT UP RECENTLY:");
                foreach (var topic in colonistTracker.recentTopics)
                    sb.AppendLine($"  - {topic}");
                sb.AppendLine("Do NOT repeat or revisit these topics. Choose something different.");
                sb.AppendLine();
            }

            // 5. Contexto específico del evento/desencadenante
            sb.AppendLine(BuildTriggerContext(request));

            //furel - Nullify the "who initiates" section for now, since the user message is now included in the base context.
            // 4. Who initiates
            //sb.AppendLine("CRITICAL INSTRUCTION:");
            //sb.AppendLine("YOU are initiating this conversation.");
            //sb.AppendLine("No one has said anything yet — YOU are starting this.");
            //sb.AppendLine();

            // 6. Format requirements
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


            return sb.ToString();
        }

        private static string BuildTriggerContext(MessageRequest request)
        {
            var sb = new StringBuilder();

            switch (request.triggerType)
            {
                case TriggerType.Incident:
                    sb.AppendLine("GUIDANCE FOR THIS INCIDENT:");
                    sb.AppendLine(GetIncidentSpecificGuidance(request.incidentTrigger));
                    sb.AppendLine();
                    sb.AppendLine("If your Verified Personal History contains a related past event,");
                    sb.AppendLine("you MAY reference it briefly — e.g. 'Last time something like this happened...'");
                    break;

                case TriggerType.Random:
                    sb.AppendLine("YOUR TASK:");
                    sb.AppendLine("Start a natural, casual conversation. Choose ONE of these approaches:");
                    sb.AppendLine();
                    sb.AppendLine("APPROACH A — Reference a real past event (PREFERRED when history exists):");
                    sb.AppendLine("  Look at your Verified Personal History above.");
                    sb.AppendLine("  Pick ONE event that still feels meaningful and bring it up naturally.");
                    sb.AppendLine("  RULE: ONLY reference events that appear in your Verified Personal History.");
                    sb.AppendLine("  NEVER invent past events — if history is empty, use Approach B.");
                    sb.AppendLine();
                    sb.AppendLine("APPROACH B — Present-focused casual conversation:");
                    sb.AppendLine("  - How you're feeling about colony life right now");
                    sb.AppendLine("  - Something you noticed today, your current work, or the weather");
                    break;

                case TriggerType.CriticalNeed:
                    sb.AppendLine("GUIDANCE FOR THIS NEED:");
                    sb.AppendLine("Inform them directly about your condition. Be clear, urgent, but avoid theatrical drama.");
                    break;

                case TriggerType.ColonySituation:
                    sb.AppendLine("GUIDANCE FOR THIS CONCERN:");
                    sb.AppendLine("Alert the player about this colony situation. Express your concern naturally.");
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