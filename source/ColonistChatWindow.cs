using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;
using System.Linq;
using System.Collections;
using System.IO;
using Verse.AI.Group;
using RimWorld.Planet;
using System;

namespace EchoColony
{
    public class ColonistChatWindow : Window
    {
        private Pawn pawn;
        private Vector2 scrollPos = Vector2.zero;
        private string input = "";
        private List<string> chatLog => ChatGameComponent.Instance.GetChat(pawn);

        private bool sendRequestedViaEnter = false;

        private bool waitingForResponse = false;

        private bool forceScrollToBottom = false;

        private List<GeminiMessage> messageHistory = new List<GeminiMessage>();

        private int editingIndex = -1;
        private string editedMessage = "";

        private Dictionary<Pawn, string> selectedVoices = new Dictionary<Pawn, string>();
        private Vector2 voiceScroll = Vector2.zero;

        private int conversationTurnCount = 0;

        public ColonistChatWindow(Pawn pawn)
        {
            this.pawn = pawn;
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.closeOnAccept = false;

            forceScrollToBottom = true;

            UpdateContextPrompt();

            var rawChat = ChatGameComponent.Instance.GetChat(pawn);
            messageHistory = new List<GeminiMessage>();

            foreach (string line in rawChat)
            {
                if (line.StartsWith("[USER]"))
                {
                    string content = line.Substring(6).Trim();
                    messageHistory.Add(new GeminiMessage("user", content));
                }
                else if (line.StartsWith(pawn.LabelShort + ":"))
                {
                    string content = line.Substring(pawn.LabelShort.Length + 1).Trim();
                    messageHistory.Add(new GeminiMessage("model", content));
                }
            }


            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId))
                {
                    TryAssignVoiceToPawn(pawn);
                }
            }
        }

        private void UpdateContextPrompt()
        {
            string contextPrompt;

            if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                if (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                    contextPrompt = KoboldPromptBuilder.Build(pawn, "");
                else if (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                    contextPrompt = LMStudioPromptBuilder.Build(pawn, "");
                else
                    contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }

            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }
            else // Gemini
            {
                contextPrompt = ColonistPromptContextBuilder.Build(pawn, "");
            }

            if (messageHistory.Count > 0 && messageHistory[0].role == "user")
            {
                messageHistory[0] = new GeminiMessage("user", contextPrompt);
            }
            else
            {
                messageHistory.Insert(0, new GeminiMessage("user", contextPrompt));
            }
        }


        public class GeminiMessage
        {
            public string role;
            public string content;

            public GeminiMessage(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        private string BuildGeminiChatJson(List<GeminiMessage> history)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"contents\": [");

            for (int i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                string role = msg.role == "model" ? "model" : "user";
                string text = EscapeJson(msg.content);

                sb.Append($"{{\"role\": \"{role}\", \"parts\": [{{\"text\": \"{text}\"}}]}}");

                if (i < history.Count - 1)
                    sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }


        public static string CleanText(string input)
        {
            return
             System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        string CleanColors(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "<color=#[A-Fa-f0-9]{6,8}>|</color>", "");
        }

        private static string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }


        public override Vector2 InitialSize => new Vector2(850f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            // 🧑‍🦱 Retrato + Título
            Rect portraitRect = new Rect(0f, 0f, 60f, 60f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(60f, 60f), Rot4.South, default, 1.25f));

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(45f, 10f, inRect.width - 50f, 30f), "EchoColony.TalkingWithLabel".Translate(pawn.LabelCap));
            Text.Font = GameFont.Small;

            // 📜 Chat log scrollable
            // 📜 Chat log scrollable
            float chatHeight = inRect.height - 110f;
            Rect scrollRect = new Rect(0, 45f, inRect.width - 20f, chatHeight);

            float viewHeight = 0f;
            List<float> heights = new List<float>();
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;

            // ✅ CALCULAR alturas considerando separadores de fecha
foreach (string msg in chatLog)
{
    float width = msg.StartsWith("[DATE_SEPARATOR]") ? scrollRect.width - 16f : scrollRect.width - 200f;
    
    string actualDisplayText = GetDisplayMessage(msg);
    
    float height = Text.CalcHeight(actualDisplayText, width) + 15f; // ✅ +5f padding top + 5f extra
    heights.Add(height);
    viewHeight += height + 15f; // ✅ Mantener separación entre mensajes
}

            Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            if (forceScrollToBottom)
            {
                scrollPos.y = viewHeight;
                forceScrollToBottom = false;
            }

            float y = 0;
            for (int i = 0; i < chatLog.Count; i++)
            {
                string msg = chatLog[i];

                // ✅ RENDERIZADO ESPECIAL PARA SEPARADORES DE FECHA
                if (msg.StartsWith("[DATE_SEPARATOR]"))
                {
                    DrawDateSeparator(new Rect(0, y, viewRect.width, heights[i]), msg);
                }
                else
                {
                    DrawRegularMessage(new Rect(0, y, viewRect.width, heights[i]), msg, i, viewRect.width);
                }

                y += heights[i] + 10f;
            }

            Widgets.EndScrollView();
            Text.WordWrap = false;

            // ✅ Detectar Enter sin Shift ANTES que todo
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "ChatInputField" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            // 📝 Input grande
            Rect inputRect = new Rect(0, inRect.height - 60f, inRect.width - 110f, 50f);
            GUI.SetNextControlName("ChatInputField");
            // Solo hacer foco automático si no estamos editando
            if (editingIndex == -1 &&
                (sendRequestedViaEnter || (Event.current.type == EventType.Layout && input.NullOrEmpty())) &&
                Find.WindowStack.Windows.LastOrDefault() == this)
            {
                GUI.FocusControl("ChatInputField");
            }


            var textStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding = new RectOffset(6, 6, 6, 6)
            };
            input = GUI.TextArea(inputRect, input, 500, textStyle);

            // 📤 Botón Send
            Rect sendRect = new Rect(inRect.width - 100f, inRect.height - 60f, 100f, 30f);
            bool sendClicked = Widgets.ButtonText(sendRect, "EchoColony.SendButton".Translate());

            if (!waitingForResponse && (sendClicked || sendRequestedViaEnter))
            {
                SendMessage();
                sendRequestedViaEnter = false;
                GUI.FocusControl(null);
            }

            Rect clearRect = new Rect(inRect.width - 330f, 10f, 100f, 30f);
           if (Widgets.ButtonText(clearRect, "EchoColony.ClearAllButton".Translate()))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.ClearAllConfirm".Translate(),
                () =>
                {
                    ChatGameComponent.Instance.ClearChat(pawn);
                    messageHistory.Clear();
                    editingIndex = -1;
                    editedMessage = "";
                    ResetTurnCounter(); // ✅ NUEVO: Resetear contador
                }));
        }

            // Botón Exportar en la parte superior derecha
            Rect exportRect = new Rect(inRect.width - 110f, 10f, 100f, 30f);
            if (Widgets.ButtonText(exportRect, "EchoColony.ExportButton".Translate()))
            {
                int ticks = Find.TickManager.TicksGame;
                float longitude = Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x;

                int year = GenDate.Year(ticks, longitude);
                string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
                int day = GenDate.DayOfSeason(ticks, longitude);
                int hour = GenDate.HourOfDay(ticks, longitude);

                string date = $"Year{year}_Day{day}_{quadrum}";
                string filename = $"{pawn.Name.ToStringShort}_chat_{date}.txt";

                string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "ColonistChats");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fullPath = Path.Combine(folder, filename);
                try
                {
                    // 🔧 Limpiar formato antes de guardar
                    List<string> cleanLog = new List<string>();
                    foreach (var line in chatLog)
                    {
                        string clean = line;

                        clean = clean.Replace("<b><i>", "*").Replace("</i></b>", "*");
                        clean = clean.Replace("<b>", "").Replace("</b>", "");
                        clean = clean.Replace("<i>", "").Replace("</i>", "");
                        clean = System.Text.RegularExpressions.Regex.Replace(clean, "<color=.*?>", "");
                        clean = clean.Replace("</color>", "");

                        cleanLog.Add(clean);
                    }

                    File.WriteAllLines(fullPath, cleanLog);
                    Messages.Message($"Conversation exported to:\n{fullPath}", MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[EchoColony] Error exporting conversation: {ex.Message}");
                    Messages.Message("❌ Error exporting the conversation.", MessageTypeDefOf.RejectInput, false);
                }

            }


            // Botón Personalizar
            Rect personalizeRect = new Rect(inRect.width - 220f, 10f, 100f, 30f);
            if (Widgets.ButtonText(personalizeRect, "EchoColony.PersonalizeButton".Translate()))
            {
                Find.WindowStack.Add(new ColonistPromptEditor(pawn));
            }
        }


        private void TrySpeakLastLine(Pawn pawn)
        {
            if (!MyMod.Settings.enableTTS) return;

            string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);

            // 🔧 Si no tiene voz, asignar una basada en el género Y en idioma inglés
            if (string.IsNullOrEmpty(voiceId))
            {
                if (TTSVoiceCache.Voices != null && TTSVoiceCache.Voices.Count > 0)
                {
                    // Determinar el género del colono
                    string targetGender;
                    switch (pawn.gender)
                    {
                        case Gender.Male:
                            targetGender = "male";
                            break;
                        case Gender.Female:
                            targetGender = "female";
                            break;
                        default:
                            // Para género indefinido, elegir aleatoriamente
                            targetGender = Rand.Bool ? "male" : "female";
                            break;
                    }

                    // Filtrar voces en inglés y que coincidan con el género
                    var matchingVoices = TTSVoiceCache.Voices
                        .Where(v => v.gender == targetGender && v.language.ToLowerInvariant().Contains("english"))
                        .ToList();

                    // Si no hay voces del género en inglés, intentar cualquier voz en inglés
                    if (matchingVoices.Count == 0)
                    {
                        matchingVoices = TTSVoiceCache.Voices
                            .Where(v => v.language.ToLowerInvariant().Contains("english"))
                            .ToList();
                        Log.Warning($"[EchoColony] No English voices found for gender '{targetGender}', using any English voice for {pawn.LabelShort}.");
                    }

                    // Si aún no hay voces, usar cualquier voz disponible como fallback
                    if (matchingVoices.Count == 0)
                    {
                        matchingVoices = TTSVoiceCache.Voices.ToList();
                        Log.Warning($"[EchoColony] No English voices found at all. Using any voice for {pawn.LabelShort}.");
                    }

                    var selectedVoice = matchingVoices.RandomElement();
                    voiceId = selectedVoice.id;

                    // Guardar en ambos lugares para persistencia
                    ChatGameComponent.Instance.SetVoiceForPawn(pawn, voiceId);
                    ColonistVoiceManager.SetVoice(pawn, voiceId);

                    Log.Message($"[EchoColony] Assigned {selectedVoice.gender} voice '{selectedVoice.name}' ({selectedVoice.language}) to {pawn.LabelShort}.");
                }
                else
                {
                    Log.Warning($"[EchoColony] No TTS voices available to assign to {pawn.LabelShort}. Auto-TTS skipped.");
                    return;
                }
            }

            // Obtener la última línea del colono
            string lastLine = ChatGameComponent.Instance.GetChat(pawn)
                .LastOrDefault(l => l.StartsWith(pawn.LabelShort + ":"));

            if (!string.IsNullOrWhiteSpace(lastLine))
            {
                string text = lastLine.Substring(pawn.LabelShort.Length + 1).Trim();
                string cleanText = CleanTextForTTS(text);

                if (!string.IsNullOrWhiteSpace(cleanText))
                {
                    Log.Message($"[EchoColony] TTS will speak: '{cleanText}' with voice ID: {voiceId}");

                    string voiceGender = "female"; // default
                    var selectedVoiceInfo = TTSVoiceCache.Voices?.FirstOrDefault(v => v.id == voiceId);
                    if (selectedVoiceInfo != null)
                    {
                        voiceGender = selectedVoiceInfo.gender;
                    }

                    MyStoryModComponent.Instance.StartCoroutine(
                        TTSManager.Speak(cleanText, voiceId, voiceGender, "en_US", 1f)
                    );
                }
                else
                {
                    Log.Message($"[EchoColony] No dialogue found after removing actions for {pawn.LabelShort}");
                }
            }
        }


        public static string GetPawnCombatStatusDetailed(Pawn pawn)
        {
            var job = pawn.CurJob;
            bool isFighting = job != null &&
                (job.def == JobDefOf.AttackMelee ||
                 job.def == JobDefOf.AttackStatic ||
                 job.def == JobDefOf.Wait_Combat);

            Thing target = job?.targetA.Thing;
            string combatLine = "";

            if (isFighting && target != null && target is Pawn enemy)
            {
                string enemyLabel = enemy.LabelShort ?? "an unknown target";
                string enemyKind = enemy.kindDef?.label ?? "unknown kind";
                int distance = (int)IntVec3Utility.DistanceTo(pawn.Position, enemy.Position);
                combatLine = $"I am attacking {enemyLabel}, a {enemyKind}, located {distance} tiles away.";
            }
            else if (isFighting)
            {
                combatLine = "I am actively engaged in combat.";
            }

            var targetedBy = Find.CurrentMap.attackTargetsCache.TargetsHostileToColony
                .OfType<Pawn>()
                .Where(e => e.Spawned && e.CurJob?.targetA.Thing == pawn)
                .ToList();

            string targetedLine = "";
            if (targetedBy.Count > 0)
            {
                var grouped = targetedBy.GroupBy(p => p.kindDef.label)
                    .Select(g => $"{g.Count()} {g.Key}")
                    .ToList();

                var avgDistance = targetedBy.Average(p => IntVec3Utility.DistanceTo(p.Position, pawn.Position));
                targetedLine = $"I am currently being targeted by {string.Join(", ", grouped)}. Average distance to me: {avgDistance:F0} tiles.";
            }

            float bleed = pawn.health.hediffSet.BleedRateTotal;
            string bleedingLine = "";
            if (bleed > 0.4f)
                bleedingLine = "I am bleeding heavily from my wounds.";
            else if (bleed > 0.1f)
                bleedingLine = "I have moderate bleeding.";
            else if (bleed > 0.01f)
                bleedingLine = "I have light bleeding.";

            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(combatLine)) lines.Add(combatLine);
            if (!string.IsNullOrWhiteSpace(targetedLine)) lines.Add(targetedLine);
            if (!string.IsNullOrWhiteSpace(bleedingLine)) lines.Add(bleedingLine);

            return lines.Any()
                ? string.Join(" ", lines)
                : "I am not in combat, not being targeted, and have no bleeding.";
        }

        private void SendMessage()
        {
            if (waitingForResponse || input.NullOrEmpty()) return;

            string userMsg = input;
            input = "";

            ChatGameComponent.Instance.AddLine(pawn, "[USER] " + "EchoColony.UserPrefix".Translate() + userMsg);
            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": ...");
            UpdateContextPrompt();
            waitingForResponse = true;
            forceScrollToBottom = true;

            bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                            MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

            bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                              MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

            string prompt;

            if (isKobold)
            {
                prompt = KoboldPromptBuilder.Build(pawn, userMsg);
            }
            else if (isLMStudio)
            {
                prompt = LMStudioPromptBuilder.Build(pawn, userMsg);
            }
            else // Gemini u OpenRouter
            {
                messageHistory.Add(new GeminiMessage("user", userMsg));
                prompt = BuildGeminiChatJson(messageHistory);
            }

            // 🔁 Enviar usando el método correcto
            IEnumerator coroutine;

            if (isKobold || isLMStudio || MyMod.Settings.modelSource == ModelSource.Local)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(prompt, OnResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, userMsg, OnResponse);
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, OnResponse);
            }
            else // Gemini (por defecto)
            {
                coroutine = GeminiAPI.SendRequestToGemini(prompt, OnResponse);
            }

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId))
                {
                    TryAssignVoiceToPawn(pawn);
                }
            }

            MyStoryModComponent.Instance.StartCoroutine(coroutine);
        }

        private void OnResponse(string response)
        {
            var chat = ChatGameComponent.Instance.GetChat(pawn);
            string thinkingMsg = pawn.LabelShort + ": ...";

            if (chat.LastOrDefault() == thinkingMsg)
            {
                chat.RemoveAt(chat.Count - 1);
            }

            // Elimina correctamente el mensaje de "Pensando..." traducido
            if (chat.LastOrDefault() == thinkingMsg)
            {
                chat.RemoveAt(chat.Count - 1);
            }

            // Agrega la respuesta del colono
            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": " + response);

            waitingForResponse = false;
            input = "";
            forceScrollToBottom = true;
            messageHistory.Add(new GeminiMessage("model", response));

            // ✅ NUEVO: Incrementar contador de turnos y verificar guardado automático
            conversationTurnCount++;
            if (conversationTurnCount >= 4 && conversationTurnCount % 4 == 0)
            {
                // Guardar memoria automáticamente cada 4 turnos
                SaveMemoryAutomatically();
            }

            if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS && MyMod.Settings.autoPlayVoice)
            {
                string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                if (string.IsNullOrEmpty(voiceId))
                {
                    TryAssignVoiceToPawn(pawn);
                }

                TrySpeakLastLine(pawn);
            }
        }

        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string cleanText = text;

            // Remover el nombre del colono al inicio si está presente
            string colonistPrefix = pawn.LabelShort + ": ";
            if (cleanText.StartsWith(colonistPrefix))
            {
                cleanText = cleanText.Substring(colonistPrefix.Length);
            }

            // Remover etiquetas de roleplay <b><i>...</i></b>
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"<b><i>.*?</i></b>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Limpiar otras etiquetas HTML y colores
            cleanText = CleanText(cleanText);
            cleanText = CleanColors(cleanText);

            // Normalizar espacios
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"\s+", " ").Trim();

            return cleanText;
        }

        private void SaveMemoryAutomatically()
{
    Log.Message($"[EchoColony] 🧠 Intentando guardar memoria automática para {pawn.LabelShort} (turno {conversationTurnCount})");
    
    // Verificar condiciones mínimas
    if (messageHistory.Count < 4)
    {
        Log.Message($"[EchoColony] ⚠️ Memoria no guardada: Solo {messageHistory.Count} mensajes (mínimo 4)");
        return;
    }

    // Verificar e inicializar componentes
    if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
    {
        InitializeMemoryManager();
        if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
        {
            Log.Warning("[EchoColony] No se pudo inicializar ColonistMemoryManager para guardado automático.");
            return;
        }
    }

    // ✅ MENSAJE SUTIL AL USUARIO
    Messages.Message("EchoColony.SavingMemories".Translate(), MessageTypeDefOf.SilentInput, false);

    // Construir resumen de los últimos intercambios
    var recentMessages = messageHistory
        .Where(m => m.role == "user" || m.role == "model")
        .TakeLast(8) // Últimos 8 mensajes (4 turnos completos)
        .Select(m => (m.role == "user" ? "Jugador: " : pawn.LabelShort + ": ") + m.content);

    string combined = string.Join("\n", recentMessages);
    string promptResumen = "Summarize this part of the conversation as if it were a personal memory from the colonist's perspective. Keep it brief, intimate, and natural—avoid literal quotes.";
    string fullPrompt = promptResumen + "\n\n" + combined;

    Log.Message($"[EchoColony] 📝 Generando resumen para memoria usando {MyMod.Settings.modelSource}");

    // ✅ CALLBACK PARA GUARDAR MEMORIA
    System.Action<string> memoryCallback = (summary) =>
    {
        Log.Message($"[EchoColony] 📥 Respuesta de IA recibida para memoria");
        
        if (string.IsNullOrWhiteSpace(summary))
        {
            Log.Warning("[EchoColony] ⚠️ Resumen de memoria vacío, usando fallback");
            summary = $"Conversación con el jugador durante el turno {conversationTurnCount}. {combined.Substring(0, Math.Min(100, combined.Length))}...";
        }

        var tracker = MyStoryModComponent.Instance.ColonistMemoryManager.GetTrackerFor(pawn);
        if (tracker != null)
        {
            int today = GenDate.DaysPassed;
            string autoMemory = summary.Trim();
            
            try
            {
                tracker.SaveMemoryForDay(today, autoMemory);
                Log.Message($"[EchoColony] ✅ Memoria automática guardada para {pawn.LabelShort} (turno {conversationTurnCount})");
                
                // Mensaje de confirmación sutil
                Messages.Message("EchoColony.MemoriesSaved".Translate(), MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Error guardando memoria: {ex.Message}");
            }
        }
        else
        {
            Log.Error("[EchoColony] ❌ Tracker se volvió NULL durante el callback");
        }
    };

    // ✅ GENERAR MEMORIA USANDO EL MODELO SELECCIONADO
    try
    {
        bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                        MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

        bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                          MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

        IEnumerator memoryCoroutine;

        if (isKobold)
        {
            string koboldPrompt = KoboldPromptBuilder.Build(pawn, fullPrompt);
            memoryCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con KoboldAI");
        }
        else if (isLMStudio)
        {
            string lmPrompt = LMStudioPromptBuilder.Build(pawn, fullPrompt);
            memoryCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con LMStudio");
        }
        else if (MyMod.Settings.modelSource == ModelSource.Local)
        {
            memoryCoroutine = GeminiAPI.SendRequestToLocalModel(fullPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con modelo local");
        }
        else if (MyMod.Settings.modelSource == ModelSource.Player2)
        {
            memoryCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, fullPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con Player2");
        }
        else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
        {
            memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(fullPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con OpenRouter");
        }
        else // Gemini (por defecto)
        {
            // Para Gemini, necesitamos crear el JSON apropiado
            var tempHistory = new List<GeminiMessage>
            {
                new GeminiMessage("user", fullPrompt)
            };
            string jsonPrompt = BuildGeminiChatJson(tempHistory);
            memoryCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, memoryCallback);
            Log.Message("[EchoColony] 🚀 Iniciando memoria con Gemini");
        }

        if (memoryCoroutine != null && MyStoryModComponent.Instance != null)
        {
            MyStoryModComponent.Instance.StartCoroutine(memoryCoroutine);
        }
        else
        {
            Log.Error("[EchoColony] ❌ No se pudo iniciar coroutine para memoria");
        }
    }
    catch (Exception ex)
    {
        Log.Error($"[EchoColony] ❌ Error iniciando generación de memoria: {ex.Message}");
    }
}

        private void InitializeMemoryManager()
        {
            if (MyStoryModComponent.Instance != null)
            {
                MyStoryModComponent.Instance.ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
                if (MyStoryModComponent.Instance.ColonistMemoryManager == null)
                {
                    MyStoryModComponent.Instance.ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                    Current.Game.components.Add(MyStoryModComponent.Instance.ColonistMemoryManager);
                }
            }
        }

        private void TryAssignVoiceToPawn(Pawn pawn)
        {
            if (TTSVoiceCache.Voices == null || TTSVoiceCache.Voices.Count == 0) return;

            string targetGender = pawn.gender == Gender.Male ? "male" :
                                  pawn.gender == Gender.Female ? "female" :
                                  Rand.Bool ? "male" : "female";

            var matchingVoices = TTSVoiceCache.Voices
                .Where(v => v.gender == targetGender && v.language.ToLowerInvariant().Contains("english"))
                .ToList();

            if (matchingVoices.Count == 0)
                matchingVoices = TTSVoiceCache.Voices.Where(v => v.language.ToLowerInvariant().Contains("english")).ToList();

            if (matchingVoices.Count == 0)
                matchingVoices = TTSVoiceCache.Voices.ToList();

            var selectedVoice = matchingVoices.RandomElement();
            string voiceId = selectedVoice.id;

            ChatGameComponent.Instance.SetVoiceForPawn(pawn, voiceId);
            ColonistVoiceManager.SetVoice(pawn, voiceId);

            Log.Message($"[EchoColony] (Late) Assigned voice '{selectedVoice.name}' to {pawn.LabelShort}");
        }

        public override void PostClose()
{
    base.PostClose();
    Log.Message($"[EchoColony] 🚪 Cerrando chat con {pawn.LabelShort} (turnos totales: {conversationTurnCount})");

    // ✅ GUARDAR MEMORIA FINAL SI HAY CONTENIDO NO GUARDADO
    var remainingTurns = conversationTurnCount % 4;
    int remainingMessages = remainingTurns * 2; // 2 mensajes por turno (user + model)
    
    Log.Message($"[EchoColony] 📊 Turnos restantes sin guardar: {remainingTurns} ({remainingMessages} mensajes)");
    
    if (remainingTurns >= 1) // Al menos 1 turno completo sin guardar
    {
        Log.Message("[EchoColony] 💾 Guardando memoria final...");
        
        // Verificar e inicializar componentes
        if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
        {
            InitializeMemoryManager();
            if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
            {
                Log.Error("[EchoColony] ❌ No se pudo inicializar ColonistMemoryManager. No se guardará memoria final.");
                return;
            }
        }

        // Construir resumen de mensajes restantes
        var remainingMessagesList = messageHistory
            .Where(m => m.role == "user" || m.role == "model")
            .TakeLast(remainingMessages)
            .Select(m => (m.role == "user" ? "Jugador: " : pawn.LabelShort + ": ") + m.content);

        if (remainingMessagesList.Any())
        {
            string combined = string.Join("\n", remainingMessagesList);
            string promptResumen = "Summarize this final part of the conversation as if it were a personal memory from the colonist's perspective. Keep it brief, intimate, and natural—avoid literal quotes.";
            string fullPrompt = promptResumen + "\n\n" + combined;
            
            Log.Message($"[EchoColony] 📝 Generando memoria final usando {MyMod.Settings.modelSource} (longitud: {combined.Length})");
            
            System.Action<string> finalMemoryCallback = (summary) =>
            {
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = $"Conversación final con el jugador. {combined.Substring(0, Math.Min(100, combined.Length))}...";
                }
                
                var tracker = MyStoryModComponent.Instance.ColonistMemoryManager.GetTrackerFor(pawn);
                if (tracker != null)
                {
                    int today = GenDate.DaysPassed;
                    string finalMemory = summary.Trim();
                    tracker.SaveMemoryForDay(today, finalMemory);
                    Log.Message($"[EchoColony] ✅ Memoria final guardada para {pawn.LabelShort}");
                }
            };

            // ✅ USAR EL MODELO SELECCIONADO PARA MEMORIA FINAL
            try
            {
                bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                                MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

                bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                                  MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

                IEnumerator finalCoroutine;

                if (isKobold)
                {
                    string koboldPrompt = KoboldPromptBuilder.Build(pawn, fullPrompt);
                    finalCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, finalMemoryCallback);
                }
                else if (isLMStudio)
                {
                    string lmPrompt = LMStudioPromptBuilder.Build(pawn, fullPrompt);
                    finalCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, finalMemoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Local)
                {
                    finalCoroutine = GeminiAPI.SendRequestToLocalModel(fullPrompt, finalMemoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Player2)
                {
                    finalCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, fullPrompt, finalMemoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                {
                    finalCoroutine = GeminiAPI.SendRequestToOpenRouter(fullPrompt, finalMemoryCallback);
                }
                else // Gemini (por defecto)
                {
                    var tempHistory = new List<GeminiMessage>
                    {
                        new GeminiMessage("user", fullPrompt)
                    };
                    string jsonPrompt = BuildGeminiChatJson(tempHistory);
                    finalCoroutine = GeminiAPI.SendRequestToGemini(jsonPrompt, finalMemoryCallback);
                }

                if (finalCoroutine != null && MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.StartCoroutine(finalCoroutine);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Error generando memoria final: {ex.Message}");
            }
        }
    }
    else
    {
        Log.Message("[EchoColony] ℹ️ No hay turnos restantes para guardar como memoria final");
    }
}

      private string GetDisplayMessage(string msg)
{
    if (msg.StartsWith("[DATE_SEPARATOR]"))
        return msg.Substring("[DATE_SEPARATOR]".Length).Trim();
    else if (msg.StartsWith("[USER]"))
        return msg.Substring(6); // ✅ AGREGAR "You:" para claridad
    else
        return msg; // ✅ MANTENER nombre del colono
}

        private void DrawDateSeparator(Rect rect, string msg)
        {
            string dateText = msg.Substring("[DATE_SEPARATOR]".Length).Trim();

            // Fondo sutil para el separador
            Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.4f, 0.5f, 0.3f));

            // Texto centrado con estilo especial
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            Widgets.Label(rect, dateText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRegularMessage(Rect rect, string msg, int index, float viewWidth)
{
    string displayMsg = GetDisplayMessage(msg);
    
    // ✅ AGREGAR separación visual entre mensajes
    if (index > 0) // No dibujar línea antes del primer mensaje
    {
        // Línea sutil de separación
        Widgets.DrawLineHorizontal(rect.x, rect.y - 2f, viewWidth - 200f, new Color(0.3f, 0.3f, 0.3f, 0.5f));
    }
    
    Rect labelRect = new Rect(0, rect.y + 5f, viewWidth - 200f, rect.height - 5f); // ✅ Agregar padding top

    if (editingIndex == index)
    {
        GUI.SetNextControlName($"EditField_{index}");
        editedMessage = Widgets.TextArea(labelRect, editedMessage);
        if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y + 5f, 80f, 25f), "EchoColony.SaveButton".Translate()))
        {
            // ✅ MANTENER formato original al guardar
            chatLog[index] = msg.StartsWith("[USER]") ? "[USER] " + editedMessage.Replace("You: ", "") : 
                            pawn.LabelShort + ": " + editedMessage.Replace(pawn.LabelShort + ": ", "").TrimStart();
            editingIndex = -1;
            editedMessage = "";
        }

        if (Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y + 5f, 80f, 25f), "EchoColony.CancelButton".Translate()))
        {
            editingIndex = -1;
            editedMessage = "";
        }
    }
    else
    {
        // ✅ DIFERENTE COLOR/ESTILO para mensajes del jugador vs colono
        if (msg.StartsWith("[USER]"))
        {
            GUI.color = new Color(0.8f, 0.9f, 1f, 1f); // Azul claro para el jugador
        }
        else
        {
            GUI.color = new Color(1f, 0.95f, 0.8f, 1f); // Amarillo claro para el colono
        }
        
        Widgets.Label(labelRect, displayMsg);
        GUI.color = Color.white; // Resetear color

        bool isUserMsg = msg.StartsWith("[USER]");
        bool hasNext = index + 1 < chatLog.Count;
        bool nextIsColonist = hasNext && chatLog[index + 1].StartsWith(pawn.LabelShort + ":");
        bool isLastExchange = isUserMsg && nextIsColonist;

                if (isLastExchange)
                {
                    Rect buttonRect = new Rect(viewWidth - 180f, rect.y, 25f, 25f);
                    TooltipHandler.TipRegion(buttonRect, "EchoColony.UndoTooltip".Translate());
                    if (Widgets.ButtonText(buttonRect, "✖"))
                    {
                        // Eliminar del chat visual
                        chatLog.RemoveAt(index + 1);
                        chatLog.RemoveAt(index);

                        // Eliminar del messageHistory
                        if (messageHistory.Count >= 2 &&
                            messageHistory[messageHistory.Count - 2].role == "user" &&
                            messageHistory[messageHistory.Count - 1].role == "model")
                        {
                            messageHistory.RemoveAt(messageHistory.Count - 1);
                            messageHistory.RemoveAt(messageHistory.Count - 1);

                            // ✅ NUEVO: Ajustar contador de turnos
                            conversationTurnCount = Math.Max(0, conversationTurnCount - 1);
                        }

                        // Limpiar memoria EchoMemory de Player2
                        if (MyMod.Settings.modelSource == ModelSource.Player2)
                        {
                            GeminiAPI.RebuildMemoryFromChat(pawn);
                        }

                        Messages.Message("EchoColony.LastExchangeDeleted".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }

                if (!msg.StartsWith("[USER]"))
                {
                    if (MyMod.Settings.modelSource == ModelSource.Player2 && MyMod.Settings.enableTTS)
                    {
                        // Botón de audio
                        Rect voiceBtnRect = new Rect(viewWidth - 180f, rect.y + 25f + 2f, 80f, 25f);
                        TooltipHandler.TipRegion(voiceBtnRect, "EchoColony.PlayAudio".Translate());
                        if (Widgets.ButtonText(voiceBtnRect, "♪"))
                        {
                            string voiceId = ChatGameComponent.Instance.GetVoiceForPawn(pawn);
                            if (!string.IsNullOrEmpty(voiceId))
                            {
                                string cleanText = CleanTextForTTS(displayMsg);
                                if (!string.IsNullOrWhiteSpace(cleanText))
                                {
                                    MyStoryModComponent.Instance.StartCoroutine(
                                        TTSManager.Speak(cleanText, voiceId, "female", "en_US", 1f)
                                    );
                                }
                            }
                        }
                    }

                    if (Widgets.ButtonText(new Rect(viewWidth - 180f, rect.y, 80f, 25f), "EchoColony.EditButton".Translate()))
                    {
                        editingIndex = index;
                        editedMessage = displayMsg;
                    }

                    bool isRegenerable = false;
                    if (index >= 1 && chatLog[index - 1].StartsWith("[USER]"))
                    {
                        string userMsg = chatLog[index - 1];
                        string colonistReply = chatLog[index];
                        isRegenerable = (index == chatLog.Count - 1) ||
                            (index + 1 == chatLog.Count - 1 && chatLog[index + 1].StartsWith("[USER]") == false);
                    }
                    bool showRegen = msg.StartsWith(pawn.LabelShort + ":") && !msg.EndsWith("...") && isRegenerable;

                    if (showRegen && Widgets.ButtonText(new Rect(viewWidth - 90f, rect.y, 80f, 25f), "EchoColony.RegenerateButton".Translate()))
                    {
                        string userMsg = chatLog[index - 1].Substring(6);
                        if (!string.IsNullOrWhiteSpace(userMsg))
                        {
                            // Lógica de regeneración existente...
                            chatLog.RemoveAt(index);

                            if (messageHistory.Count >= 2 &&
                                messageHistory[messageHistory.Count - 1].role == "model" &&
                                messageHistory[messageHistory.Count - 2].role == "user")
                            {
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                                messageHistory.RemoveAt(messageHistory.Count - 1);
                            }

                            UpdateContextPrompt();
                            messageHistory.Add(new GeminiMessage("user", userMsg));

                            while (chatLog.Count > 0 && chatLog.Last().StartsWith(pawn.LabelShort + ": ..."))
                            {
                                chatLog.RemoveAt(chatLog.Count - 1);
                            }

                            ChatGameComponent.Instance.AddLine(pawn, pawn.LabelShort + ": ...");
                            waitingForResponse = true;
                            forceScrollToBottom = true;

                            IEnumerator coroutine;
                            if (MyMod.Settings.modelSource == ModelSource.Local)
                            {
                                if (MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI)
                                    coroutine = GeminiAPI.SendRequestToLocalModel(KoboldPromptBuilder.Build(pawn, userMsg), OnResponse);
                                else if (MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio)
                                    coroutine = GeminiAPI.SendRequestToLocalModel(LMStudioPromptBuilder.Build(pawn, userMsg), OnResponse);
                                else
                                    coroutine = GeminiAPI.SendRequestToLocalModel(userMsg, OnResponse);
                            }
                            else if (MyMod.Settings.modelSource == ModelSource.Player2)
                            {
                                coroutine = GeminiAPI.SendRequestToPlayer2(pawn, userMsg, OnResponse);
                            }
                            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                            {
                                coroutine = GeminiAPI.SendRequestToOpenRouter(userMsg, OnResponse);
                            }
                            else
                            {
                                string json = BuildGeminiChatJson(messageHistory);
                                coroutine = GeminiAPI.SendRequestToGemini(json, OnResponse);
                            }

                            MyStoryModComponent.Instance.StartCoroutine(coroutine);
                            return;
                        }
                    }
                }


            }


        }

private void ResetTurnCounter()
        {
            conversationTurnCount = 0;
        }
    }
    
}
