using System.Collections;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Generador principal de mensajes espontáneos
    /// Orquesta todo el proceso: construcción de prompt, llamada a IA, registro en chat, y creación de letter
    /// </summary>
    public static class SpontaneousMessageGenerator
    {
        /// <summary>
        /// Genera y envía un mensaje espontáneo completo
        /// Este es el método principal que coordina todo el flujo
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

            // Log para debug
            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Generating spontaneous message for {request.colonist.LabelShort} (Type: {request.triggerType}, Context: {request.contextDescription})");
            }

            // 1. Construir el prompt específico
            string prompt = MessageContextBuilder.BuildPrompt(request);

            // 2. Variable para capturar la respuesta
            string aiResponse = null;
            bool responseReceived = false;

            // 3. Llamar a la IA (usa el mismo sistema que el chat normal)
            yield return GeminiAPI.GetResponseFromModel(
                request.colonist,
                prompt,
                (response) =>
                {
                    aiResponse = response;
                    responseReceived = true;
                }
            );

            // 4. Esperar respuesta
            float timeout = 30f; // 30 segundos máximo
            float elapsed = 0f;
            while (!responseReceived && elapsed < timeout)
            {
                elapsed += 0.1f;
                yield return new UnityEngine.WaitForSeconds(0.1f);
            }

            // 5. Verificar que recibimos respuesta válida
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                Log.Warning($"[EchoColony] SpontaneousMessage: No response received for {request.colonist.LabelShort}");
                yield break;
            }

            // Verificar errores de API
            if (aiResponse.StartsWith("⚠ ERROR:") || aiResponse.StartsWith("ERROR:"))
            {
                Log.Error($"[EchoColony] SpontaneousMessage: API error for {request.colonist.LabelShort}: {aiResponse}");
                yield break;
            }

            // 6. Limpiar la respuesta
            string cleanResponse = CleanResponse(aiResponse);

            // 7. PRIMERO: Registrar el mensaje en el chat
            ChatGameComponent.Instance.AddLine(
                request.colonist,
                $"{request.colonist.LabelShort}: {cleanResponse}"
            );

            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Message registered in chat for {request.colonist.LabelShort}");
            }

            // 8. Crear y mostrar la letter de notificación
            ShowColonistMessageLetter(request.colonist, cleanResponse, request.triggerType);

            // 9. Actualizar el tracker
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker != null)
            {
                tracker.RegisterMessage(request.colonist, request.triggerType);
                tracker.SetPendingResponse(request.colonist, true);
            }

            // 10. Log final
            if (MyMod.Settings.debugMode)
            {
                Log.Message($"[EchoColony] Spontaneous message completed for {request.colonist.LabelShort}");
            }
        }

        /// <summary>
        /// Crea y muestra la letter de notificación
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
        /// Limpia la respuesta de la IA
        /// </summary>
        private static string CleanResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            string cleaned = response.Trim();

            // Remover comillas si envuelven todo el texto
            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length > 2)
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }

            // Remover prefijos comunes que la IA podría agregar
            string[] prefixesToRemove = {
                "Hey, ",
                "Well, ",
                "So, ",
                "Um, ",
                "Uh, "
            };

            foreach (string prefix in prefixesToRemove)
            {
                if (cleaned.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Solo remover si no es el inicio natural de la oración
                    // (e.g. "Hey" al inicio de un saludo es válido)
                    continue;
                }
            }

            // Asegurarse de que no exceda límite razonable (aunque el prompt dice 2-3 oraciones)
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
        /// Método helper para debug: registra el prompt generado
        /// </summary>
        public static void DebugLogPrompt(MessageRequest request)
        {
            if (!MyMod.Settings.debugMode)
                return;

            string prompt = MessageContextBuilder.BuildPrompt(request);
            Log.Message($"[EchoColony] Spontaneous Message Prompt for {request.colonist.LabelShort}:\n{prompt}");
        }
    }
}