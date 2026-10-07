using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public enum StorytellerCommentKind
    {
        Random,
        Event,
        Test
    }

    /// <summary>
    /// Único punto de entrada para pedir un comentario espontáneo del storyteller.
    ///  - Todo se prepara y se pide en el hilo principal (corrutina de Unity), nunca con Task.Run.
    ///  - Una petición a la vez + cooldown, para que una ráfaga de eventos no dispare
    ///    10 llamadas (y 10 ventanas) seguidas.
    ///  - Bloquea eventos duplicados durante el tiempo configurado.
    /// Basado en NarratorCommentService de The Storyteller's Voice.
    /// </summary>
    public static class StorytellerCommentService
    {
        private class PendingRequest
        {
            public StorytellerCommentKind Kind;
            public string Situation;
        }

        // Si una corrutina muere sin llamar al callback, no bloquear para siempre
        private const float BusyTimeoutSeconds = 180f;
        // Aunque el bloqueo de duplicados esté desactivado, el mismo suceso exacto
        // nunca se comenta dos veces seguidas
        private const float SameEventWindowSeconds = 10f;
        private const float ErrorMessageIntervalSeconds = 120f;
        private const int MaxOpenDialogs = 3;
        private const int MaxCommentLength = 600;
        private const int RecentCommentsInPrompt = 5;

        private static bool busy;
        private static float busySince;
        private static PendingRequest pending;
        private static float lastCommentTime = -9999f;
        private static float lastErrorMessageTime = -9999f;
        private static readonly Dictionary<string, float> recentEvents = new Dictionary<string, float>();

        private static readonly Regex CommandTags = new Regex(@"\[(TRIGGER|STOP):[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsBusy => busy && Time.realtimeSinceStartup - busySince < BusyTimeoutSeconds;

        /// <param name="situation">Qué pasó (vacío para observaciones aleatorias)</param>
        /// <param name="eventKey">Clave para bloquear duplicados (p. ej. "incident:RaidEnemy")</param>
        /// <param name="priority">Ignora el cooldown y se encola si hay otra petición en curso</param>
        public static bool Request(StorytellerCommentKind kind, string situation = null, string eventKey = null, bool priority = false)
        {
            try
            {
                if (MyMod.Settings == null || Current.Game == null || Find.Storyteller?.def == null)
                    return false;

                bool test = kind == StorytellerCommentKind.Test;
                if (!test && !MyMod.Settings.IsStorytellerMessagesActive())
                    return false;

                float now = Time.realtimeSinceStartup;

                if (!string.IsNullOrEmpty(eventKey))
                {
                    float window = Mathf.Max(SameEventWindowSeconds, MyMod.Settings.storytellerDuplicateBlockMinutes * 60f);
                    if (recentEvents.TryGetValue(eventKey, out float last) && now - last < window)
                    {
                        DebugMessage($"Duplicate event skipped: {eventKey}");
                        return false;
                    }
                    PruneRecentEvents(now, window);
                    recentEvents[eventKey] = now;
                }

                if (IsBusy)
                {
                    if (priority || test)
                    {
                        pending = new PendingRequest { Kind = kind, Situation = situation };
                        DebugMessage($"Queued {kind} comment while another one is generating");
                        return true;
                    }
                    DebugMessage($"Skipped {kind} comment: another one is generating");
                    return false;
                }

                float cooldown = MyMod.Settings.storytellerCommentCooldownSeconds;
                if (!test && !priority && now - lastCommentTime < cooldown)
                {
                    DebugMessage($"Skipped {kind} comment: cooldown");
                    return false;
                }

                return Start(kind, situation);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error requesting storyteller comment: {ex}");
                return false;
            }
        }

        private static void PruneRecentEvents(float now, float window)
        {
            if (recentEvents.Count < 100) return;
            foreach (var key in recentEvents.Where(kv => now - kv.Value >= window).Select(kv => kv.Key).ToList())
                recentEvents.Remove(key);
        }

        private static bool Start(StorytellerCommentKind kind, string situation)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Warning("[EchoColony] Cannot generate storyteller comment: MyStoryModComponent missing");
                return false;
            }

            var storyteller = Find.Storyteller;
            string storytellerDefName = storyteller.def.defName;

            // Todo lo que toca el estado del juego se captura aquí, en el hilo principal
            string systemPrompt = StorytellerPromptBuilder.BuildCommentContext(storyteller);
            string userPrompt = BuildUserPrompt(kind, situation, storytellerDefName);

            string reply = null;
            IEnumerator request = BuildRequest(systemPrompt, userPrompt, r => reply = r);
            if (request == null)
            {
                Log.Warning($"[EchoColony] Storyteller comments: unsupported model source {MyMod.Settings.modelSource}");
                return false;
            }

            busy = true;
            busySince = Time.realtimeSinceStartup;
            lastCommentTime = Time.realtimeSinceStartup;
            MyStoryModComponent.Instance.StartCoroutine(Generate(kind, storytellerDefName, request, () => reply, Current.Game));

            DebugMessage($"Generating {kind} comment ({systemPrompt.Length + userPrompt.Length} chars of prompt)");
            return true;
        }

        private static IEnumerator Generate(StorytellerCommentKind kind, string storytellerDefName, IEnumerator request, Func<string> getReply, Game game)
        {
            yield return MyStoryModComponent.Instance.StartCoroutine(request);

            busy = false;
            lastCommentTime = Time.realtimeSinceStartup;

            // El jugador salió al menú o cargó otra partida mientras se generaba
            if (Current.Game == null || Current.Game != game)
            {
                pending = null;
                yield break;
            }

            try
            {
                HandleReply(kind, storytellerDefName, getReply());
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error showing storyteller comment: {ex}");
            }

            if (pending != null)
            {
                var next = pending;
                pending = null;
                try { Start(next.Kind, next.Situation); }
                catch (Exception ex) { Log.Error($"[EchoColony] Error starting queued storyteller comment: {ex}"); }
            }
        }

        private static IEnumerator BuildRequest(string systemPrompt, string userPrompt, Action<string> onResponse)
        {
            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Player2:
                    var jsonPayload = new SimpleJSON.JSONObject();
                    var jsonMessages = new SimpleJSON.JSONArray();

                    var systemMsg = new SimpleJSON.JSONObject();
                    systemMsg["role"] = "system";
                    systemMsg["content"] = systemPrompt;
                    jsonMessages.Add(systemMsg);

                    var userMsg = new SimpleJSON.JSONObject();
                    userMsg["role"] = "user";
                    userMsg["content"] = userPrompt;
                    jsonMessages.Add(userMsg);

                    jsonPayload["messages"] = jsonMessages;
                    return GeminiAPI.SendRequestToPlayer2Storyteller(jsonPayload.ToString(), onResponse);
            }

            // El resto de proveedores separa los roles con los marcadores [SYSTEM]/[USER]
            string prompt = "[SYSTEM]\n" + systemPrompt.Trim() + "\n[USER]\n" + userPrompt.Trim();

            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Gemini:     return GeminiAPI.SendRequestToGemini(prompt, onResponse);
                case ModelSource.OpenRouter: return GeminiAPI.SendRequestToOpenRouter(prompt, onResponse);
                case ModelSource.Local:      return GeminiAPI.SendRequestToLocalModel(prompt, onResponse);
                case ModelSource.Custom:     return GeminiAPI.SendRequestToCustomProvider(prompt, onResponse);
                default:                     return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PROMPT
        // ═══════════════════════════════════════════════════════════════

        private static string BuildUserPrompt(StorytellerCommentKind kind, string situation, string storytellerDefName)
        {
            var sb = new StringBuilder();

            if (kind == StorytellerCommentKind.Event)
                sb.AppendLine("Something just happened in the colony. React to it briefly, in character.");
            else
                sb.AppendLine("Nothing specific just happened. Make a brief, casual observation about how the colony is doing right now.");

            if (!string.IsNullOrWhiteSpace(situation))
            {
                sb.AppendLine();
                sb.AppendLine("WHAT HAPPENED:");
                sb.AppendLine(situation.Trim());
            }

            var recent = GetRecentComments(storytellerDefName);
            if (recent.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("YOUR RECENT COMMENTS (don't repeat them):");
                foreach (var line in recent)
                    sb.AppendLine("- " + line);
            }

            sb.AppendLine();
            sb.Append("Your comment:");
            return sb.ToString();
        }

        private static List<string> GetRecentComments(string storytellerDefName)
        {
            try
            {
                var history = Current.Game?.GetComponent<StorytellerChatData>()?.GetChatHistory(storytellerDefName);
                if (history == null) return new List<string>();

                return history
                    .Where(l => l != null && l.StartsWith("[STORYTELLER] "))
                    .Select(l => l.Substring("[STORYTELLER] ".Length).Trim())
                    .Where(l => l.Length > 0 && l != "...")
                    .Reverse()
                    .Take(RecentCommentsInPrompt)
                    .Reverse()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RESPUESTA
        // ═══════════════════════════════════════════════════════════════

        private static void HandleReply(StorytellerCommentKind kind, string storytellerDefName, string reply)
        {
            if (IsErrorReply(reply))
            {
                ReportError(reply, kind == StorytellerCommentKind.Test);
                return;
            }

            string comment = FinalizeText(reply, storytellerDefName);
            if (string.IsNullOrWhiteSpace(comment))
            {
                DebugMessage("Storyteller comment was empty after cleanup");
                return;
            }

            Current.Game.GetComponent<StorytellerChatData>()?.AddMessage(storytellerDefName, "[STORYTELLER] " + comment);

            // No acumular ventanas infinitas si el jugador está lejos del teclado
            var open = Find.WindowStack.Windows.OfType<StorytellerMessageDialog>().ToList();
            for (int i = 0; i <= open.Count - MaxOpenDialogs; i++)
                open[i].Close(false);

            Find.WindowStack.Add(new StorytellerMessageDialog(comment, storytellerDefName, kind));
            DebugMessage($"Storyteller {kind} comment shown: {comment}");
        }

        private static bool IsErrorReply(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return true;
            string trimmed = reply.TrimStart();
            return trimmed.StartsWith("⚠ ERROR") || trimmed.StartsWith("ERROR:");
        }

        private static void ReportError(string reply, bool test)
        {
            string message = string.IsNullOrWhiteSpace(reply) ? "empty response" : reply.Trim();
            Log.Warning($"[EchoColony] Storyteller comment failed: {message}");

            // Avisar al jugador, pero sin spamear si falla en cada evento
            if (test || Time.realtimeSinceStartup - lastErrorMessageTime > ErrorMessageIntervalSeconds)
            {
                lastErrorMessageTime = Time.realtimeSinceStartup;
                string firstLine = message.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? message;
                Messages.Message("EchoColony.StorytellerCommentFailed".Translate(firstLine), MessageTypeDefOf.RejectInput, false);
            }
        }

        private static string FinalizeText(string text, string storytellerDefName)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Los comentarios nunca disparan eventos: quitar cualquier etiqueta de comando
            text = CommandTags.Replace(text, "");
            text = Regex.Replace(text, @"[ \t]{2,}", " ").Trim();

            // "Cassandra: ..." — el nombre ya se ve en la ventana
            var def = DefDatabase<StorytellerDef>.GetNamedSilentFail(storytellerDefName);
            var names = new List<string> { storytellerDefName };
            if (!string.IsNullOrEmpty(def?.label)) names.Add(def.label);
            var prefix = new Regex(@"^\**\s*(" + string.Join("|", names.Select(Regex.Escape)) + @")[^:\n]{0,20}:\**\s*", RegexOptions.IgnoreCase);
            text = prefix.Replace(text, "").Trim();

            // Comillas envolviendo todo el comentario
            if (text.Length > 1 && (text[0] == '"' || text[0] == '“') && (text[text.Length - 1] == '"' || text[text.Length - 1] == '”'))
                text = text.Substring(1, text.Length - 2).Trim();

            // Los modelos pequeños se pasan del límite; cortar en un final de frase razonable
            if (text.Length > MaxCommentLength)
            {
                int cut = text.LastIndexOfAny(new[] { '.', '!', '?', '。', '！', '？' }, MaxCommentLength - 1);
                text = cut >= MaxCommentLength / 3 ? text.Substring(0, cut + 1) : text.Substring(0, MaxCommentLength).TrimEnd() + "…";
            }

            return text;
        }

        private static void DebugMessage(string message)
        {
            if (MyMod.Settings?.debugMode == true)
                Log.Message($"[EchoColony] {message}");
        }
    }
}
