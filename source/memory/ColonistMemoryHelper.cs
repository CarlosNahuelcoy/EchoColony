using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    public static class ColonistMemoryHelper
    {
        /// <summary>
        /// Void wrapper method for calls from the UI or synchronous methods (fire-and-forget).
        /// Starts the coroutine in the background without freezing the caller's execution.
        /// </summary>
        public static void CheckAndGenerateMemory(Pawn pawn, int minTurnsThreshold = 4)
        {
            if (MyStoryModComponent.Instance != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(CheckAndGenerateMemoryRoutine(pawn, minTurnsThreshold));
            }
        }

        /// <summary>
        /// A routine that checks and generates the settler's memory.
        /// Allows it to be awaited with 'yield return' in asynchronous flows such as group chat.
        /// </summary>
        public static IEnumerator CheckAndGenerateMemoryRoutine(Pawn pawn, int minTurnsThreshold = 4)
        {
            var info = ChatGameComponent.Instance.GetInteractionInfo(pawn);
            int unsavedTurns = info.CurrentTurn - info.LastSavedTurnCount;

            if (unsavedTurns < minTurnsThreshold || unsavedTurns <= 0)
                yield break;

            string combined = GetUnprocessedConversation(pawn, info.LastSavedTurnCount);
            if (string.IsNullOrWhiteSpace(combined))
                yield break;

            string fullPrompt = ColonistMemoryPromptBuilder.BuildSummaryPrompt(pawn, combined);

            bool isDone = false;

            IEnumerator apiCoroutine = GetApiMemoryCoroutine(fullPrompt, GeminiAPI.CleanNameForFileName(pawn?.LabelShort), (summary) =>
            {
                var tracker = ColonistMemoryManager.GetOrCreate()?.GetTrackerFor(pawn);
                if (tracker != null)
                {
                    // 1. Fallback si la respuesta del LLM viene vacía
                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        summary = $"Conversation during turn {info.CurrentTurn}. " +
                                  $"{combined.Substring(0, System.Math.Min(100, combined.Length))}...";
                    }

                    try
                    {
                        // 2. Registro seguro de la memoria
                        tracker.RecordPlayerInteraction(summary.Trim());
                        ChatGameComponent.Instance.UpdateLastSavedTurn(pawn, info.CurrentTurn);

                        // 3. Notificación UI y Logs
                        Messages.Message("EchoColony.MemoriesSaved".Translate(), MessageTypeDefOf.SilentInput, false);
                        Log.Message($"[EchoColony] Memory successfully saved for {pawn.LabelShort}");
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"[EchoColony] Error saving memory for {pawn.LabelShort}: {ex.Message}");
                    }
                }

                isDone = true;
            });

            if (apiCoroutine != null)
            {
                yield return apiCoroutine;
            }

            while (!isDone)
            {
                yield return null;
            }
        }

        private static string GetUnprocessedConversation(Pawn pawn, int lastSavedTurnCount)
        {
            var chat = ChatGameComponent.Instance.GetChat(pawn);
            if (chat == null || !chat.Any()) return string.Empty;

            string pawnName = pawn?.LabelShort ?? "Colono";
            int messagesToSkip = lastSavedTurnCount * 2;

            var lines = chat
                .Where(line => line.StartsWith("[USER]") || line.StartsWith("[ASSISTANT]"))
                .Skip(messagesToSkip)
                .Select(line =>
                {
                    if (line.StartsWith("[USER]"))
                    {
                        string content = line.Substring(6).TrimStart();
                        return $"Player: {content}";
                    }
                    if (line.StartsWith("[ASSISTANT]"))
                    {
                        string content = line.Substring(11).TrimStart();

                        // Si el mensaje grabado ya empezaba con el nombre ("Sarsum: ..."), se evita duplicarlo
                        if (content.StartsWith($"{pawnName}:"))
                        {
                            return content;
                        }

                        return $"{pawnName}: {content}";
                    }
                    return line;
                });

            return string.Join("\n", lines);
        }

        private static IEnumerator GetApiMemoryCoroutine(string fullPrompt, string pawnName, System.Action<string> callback)
        {
            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Local:
                    return GeminiAPI.SendRequestToLocalModel(fullPrompt, callback);
                case ModelSource.Player2:
                    return GeminiAPI.SendRequestToPlayer2WithPrompt(fullPrompt, callback, $"TODAY_MEMORY_{pawnName}");
                case ModelSource.OpenRouter:
                    return GeminiAPI.SendRequestToOpenRouter(fullPrompt, callback);
                case ModelSource.Custom:
                    return GeminiAPI.SendRequestToCustomProvider(fullPrompt, callback);
                default:
                    return GeminiAPI.SendRequestToGemini(fullPrompt, callback);
            }
        }
    }
}
