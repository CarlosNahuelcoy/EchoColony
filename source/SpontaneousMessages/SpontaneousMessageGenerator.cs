using System.Collections;
using Verse;
using RimWorld;
using System.Runtime.InteropServices.WindowsRuntime;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Main spontaneous message generator.
    /// Orchestrates the full flow: prompt building, AI call, chat registration, and letter creation.
    /// </summary>
    public static class SpontaneousMessageGenerator
    {
        /// <summary>
        /// Generates and sends a complete spontaneous message.
        /// This is the main method that coordinates the entire flow.
        /// </summary>
        public static IEnumerator GenerateAndSendMessage(MessageRequest request)
        {
            if (request.colonist == null)
            {
                Log.Warning("[EchoColony] SpontaneousMessage: Null colonist in request");
                yield break;
            }

            if (!MyMod.Settings.IsSpontaneousMessagesActive())
            {
                Log.Warning("[EchoColony] SpontaneousMessage: System not active");
                yield break;
            }

            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Generating spontaneous message for {request.colonist.LabelShort} (Type: {request.triggerType}, Context: {request.contextDescription})");
            }

            if(ChatGameComponent.Instance.ShouldResetSession(request.colonist))
            {
                yield return ColonistMemoryHelper.CheckAndGenerateMemoryRoutine(request.colonist, 1);
            }

            var info = ChatGameComponent.Instance.GetInteractionInfo(request.colonist);

            // 1. Build the specific prompt
            string prompt = MessageContextBuilder.BuildPrompt(request);

            // 2. Variable to capture the response
            string aiResponse = null;
            bool responseReceived = false;

            // 3. Call the AI (uses the same system as normal chat)
            yield return GeminiAPI.GetResponseFromModel(
                request.colonist,
                prompt,
                (response) =>
                {
                    aiResponse = response;
                    responseReceived = true;
                }
            );

            // 4. Wait for response
            float timeout = 30f;
            float elapsed = 0f;
            while (!responseReceived && elapsed < timeout)
            {
                elapsed += 0.1f;
                yield return new UnityEngine.WaitForSeconds(0.1f);
            }

            // 5. Validate response
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                Log.Warning($"[EchoColony] SpontaneousMessage: No response received for {request.colonist.LabelShort}");
                yield break;
            }

            if (aiResponse.StartsWith("⚠ ERROR:") || aiResponse.StartsWith("ERROR:"))
            {
                Log.Error($"[EchoColony] SpontaneousMessage: API error for {request.colonist.LabelShort}: {aiResponse}");
                yield break;
            }

            //furel - Event registration user entry.

            string eventLogEntry = TextEventRegistration(request);
            ChatGameComponent.Instance.AddLine(request.colonist, $"[USER]{eventLogEntry}");


            // 6. Clean the response
            string cleanResponse = CleanResponse(aiResponse);

            // 7. Register the message in chat
            ChatGameComponent.Instance.AddLine(
                request.colonist,
                $"[ASSISTANT]{request.colonist.LabelShort}: {cleanResponse}"
            );

            //furel - Updated conversation turn and interaction tick for the colonist, so it now counts to mantein the conversation flow. 
            //ChatGameComponent.Instance.UpdateConversationTurn(request.colonist, info.CurrentTurn + 1);
            //if (ChatGameComponent.Instance.GetInteractionInfo(request.colonist).LastTick == 0)
            //{
                ChatGameComponent.Instance.RegisterInteraction(request.colonist, info.CurrentTurn + 1);
            //}
            //else
            //{
            //    ChatGameComponent.Instance.UpdateInteractionTick(request.colonist);
            //}

            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Message registered in chat for {request.colonist.LabelShort}");
            }

            // 8. Create and show the notification letter
            ShowColonistMessageLetter(request.colonist, cleanResponse, request.triggerType);

            // 9. Update the tracker — store message text and topic to avoid repetition
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker != null)
            {
                tracker.RegisterMessage(
                    request.colonist,
                    request.triggerType,
                    cleanResponse,
                    request.contextDescription
                );
                tracker.SetPendingResponse(request.colonist, true);
            }

            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Spontaneous message completed for {request.colonist.LabelShort}");
            }
        }

        /// <summary>
        /// Creates and shows the notification letter.
        /// </summary>
        private static void ShowColonistMessageLetter(Pawn colonist, string message, TriggerType triggerType)
        {
            try
            {
                var letter = new ColonistMessageLetter(colonist, message, triggerType);
                Find.LetterStack.ReceiveLetter(letter);

                if (MyMod.Settings.debugMode)
                {
                    Log.Message($"[EchoColony] Letter created and shown for {colonist.LabelShort}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Failed to show colonist message letter: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Cleans the AI response of common artifacts.
        /// </summary>
        private static string CleanResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            string cleaned = response.Trim();

            // Remove surrounding quotes if they wrap the entire text
            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length > 2)
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }

            // Truncate if it exceeds a reasonable length
            if (cleaned.Length > 500)
            {
                int lastPeriod = cleaned.LastIndexOf('.', 500);
                if (lastPeriod > 200)
                {
                    cleaned = cleaned.Substring(0, lastPeriod + 1);
                }
            }

            return cleaned;
        }

        /// <summary>
        /// Debug helper: logs the generated prompt for a given request.
        /// </summary>
        public static void DebugLogPrompt(MessageRequest request)
        {
            if (!MyMod.Settings.debugMode)
                return;

            bool sessionExpired = ChatGameComponent.Instance.ShouldResetSession(request.colonist);
            string groupContext = !sessionExpired ? GroupChatGameComponent.Instance.BuildGroupChatContextString(request.colonist) : "";

            string prompt = MessageContextBuilder.BuildPrompt(request);
            Log.Message($"[EchoColony] Spontaneous Message Prompt for {request.colonist.LabelShort}:\n{prompt}");
        }

        //furel - Mesage event registration text generation based on the type of trigger and urgency.
        public static string TextEventRegistration(MessageRequest request)
        {
            if (request.colonist == null)
                return "*(You approach the player to start a conversation.)*";

            string details = !string.IsNullOrWhiteSpace(request.contextDescription)
                ? request.contextDescription
                : "recent events";

            switch (request.triggerType)
            {
                case TriggerType.Incident:
                    return GetIncidentLogText(request.incidentTrigger, details, request.urgency);

                case TriggerType.CriticalNeed:
                    return GetNeedLogText(details, request.urgency);

                case TriggerType.Random:
                default:
                    return GetRandomLogText(details);
            }
        }

        // 3. Redacción desde la perspectiva del colono que toma la iniciativa hacia el jugador
        private static string GetIncidentLogText(IncidentTrigger trigger, string details, float urgency)
        {
            string mood = urgency >= 0.8f ? "with evident urgency" : "to comment on the situation";

            switch (trigger)
            {
                case IncidentTrigger.Raid:
                case IncidentTrigger.MechanoidCluster:
                case IncidentTrigger.InfestationSpawned:
                    return $"*(You notice the threat of {details} and immediately approach the player {mood})*";

                case IncidentTrigger.ToxicFallout:
                case IncidentTrigger.SolarFlare:
                    return $"*(You approach the player concerned about the environmental danger: {details})*";

                case IncidentTrigger.TraderCaravan:
                    return $"*(You head towards the player {mood} about the arrival of the traders ({details}))*";

                case IncidentTrigger.WandererJoin:
                case IncidentTrigger.RefugeeChased:
                    return $"*(You decide to discuss the arrival of the new member with the player. ({details}))*";

                default:
                    return $"*(You approach the player {mood} about what happened: {details})*";
            }
        }

        private static string GetNeedLogText(string details, float urgency)
        {
            if (urgency >= 0.85f)
            {
                return $"*(You are in a critical state ({details}) and seek the player's help)*";
            }
            return $"*(You approach the player to express how you feel: {details})*";
        }

        private static string GetRandomLogText(string details)
        {
            return $"*(You take advantage of a moment during your tasks ({details}) to start a casual conversation with the player)*";
        }
    }
}