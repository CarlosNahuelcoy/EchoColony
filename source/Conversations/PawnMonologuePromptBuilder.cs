using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Genera los prompts de sistema y usuario para que un colono realice un 
    /// breve comentario o reaccione en primera persona a un evento que le acaba de ocurrir.
    /// </summary>
    public static class PawnMonologuePromptBuilder
    {
        private static readonly Regex TagRegex = new Regex("<.*?>", RegexOptions.Compiled);

        // ── Entry point ───────────────────────────────────────────────────────────

        public static ConversationPromptResult? Build(
            Pawn pawn,
            string eventText,
            string eventContext = null)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(eventText)) return null;

            string systemPrompt = BuildSystemPrompt(pawn.LabelShort);
            string userPrompt = BuildUserPrompt(pawn, eventText, eventContext);

            return new ConversationPromptResult(systemPrompt, userPrompt);
        }

        // ── System Prompt Builder ─────────────────────────────────────────────────

        private static string BuildSystemPrompt(string pawnName)
        {
            string lang = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";
            string langInstruction = lang != "english" ? $"IMPORTANT: Respond in {lang}.\n" : "";

            return
                $"[SYSTEM]\n" + 
                $"You are an AI roleplay generator creating a brief, realistic 1st-person reaction/commentary from a RimWorld colonist ({pawnName}) about something that just happened to them.\n" +
                $"{langInstruction}\n" +
                $"OUTPUT FORMAT RULES:\n" +
                $"1. Respond ONLY with a valid JSON object. Do NOT use markdown code blocks (no ```json), no preambles, and no postscripts.\n" +
                $"2. Schema:\n" +
                $"{{\n" +
                $"  \"speaker\": \"{pawnName}\",\n" +
                $"  \"text\": \"...\"\n" +
                $"}}\n\n" +
                $"COMMENTARY RULES:\n" +
                $"1. Length: Exactly 1–2 short, impactful sentences maximum.\n" +
                $"2. Perspective: First-person perspective (\"I\", \"me\"), reacting in real-time or reflecting immediately after the event.\n" +
                $"3. Style: Grounded, emotional, and human. Reflect their current mood, traits, health state, and personality.\n" +
                $"4. No Meta-Talk: Never talk ABOUT the system or game mechanics. React naturally as a person living in the colony.\n\n" +
                $"STRICT GROUNDING RULES (ANTI-HALLUCINATION):\n" +
                $"1. Focus strictly on the triggered event and immediate feelings/surroundings.\n" +
                $"2. Do NOT invent unrelated historical facts, items, or structures not mentioned in the context.\n" +
                $"3. Ambient Noise Constraint: Weather and environment are strictly PASSIVE BACKGROUND unless the event specifically involves them.\n";
        }

        // ── User Prompt Builder ───────────────────────────────────────────────────

        private static string BuildUserPrompt(Pawn pawn, string eventText, string eventContext)
        {
            var sb = new StringBuilder();

            // 1. Evento desencadenante
            sb.AppendLine("[USER]\n");
            sb.AppendLine("=== EVENT THAT JUST HAPPENED ===");
            sb.AppendLine($"Event: {eventText}");
            if (!string.IsNullOrWhiteSpace(eventContext))
            {
                sb.AppendLine($"Details: {eventContext}");
            }
            sb.AppendLine($"Focus: {pawn.LabelShort} must react directly to this occurrence.");
            sb.AppendLine();

            // 2. Entorno y atmósfera
            sb.AppendLine(BuildEnvironmentSection(pawn));

            // 3. Ficha y estado del colono
            sb.AppendLine(BuildPawnSection(pawn));

            // 4. Memorias e historial personal reciente
            sb.AppendLine(BuildPawnMemorySection(pawn));

            // 5. Instrucción final
            sb.AppendLine("=== INSTRUCTION ===");
            sb.AppendLine($"Generate {pawn.LabelShort}'s 1–2 sentence reaction to the event in JSON format, strictly following all System instructions.");

            return sb.ToString();
        }

        // ── Section Builders ──────────────────────────────────────────────────────

        private static string BuildEnvironmentSection(Pawn pawn)
        {
            if (pawn?.Map == null) return "";
            var sb = new StringBuilder();

            sb.AppendLine("=== PASSIVE BACKGROUND ATMOSPHERE ===");
            var map = pawn.Map;
            int hour = GenLocalDate.HourOfDay(map);
            sb.AppendLine($"Time: {hour:D2}:00 ({GetTimeDesc(hour)})");
            sb.AppendLine($"Location: {GetLocationDesc(pawn)}");

            var curWeather = map.weatherManager?.curWeather;
            if (curWeather != null)
            {
                string wLabel = curWeather.label.ToLowerInvariant();
                bool isExtreme = wLabel.Contains("fallout") || wLabel.Contains("wave") ||
                                 wLabel.Contains("snap") || wLabel.Contains("flashstorm");

                if (isExtreme)
                    sb.AppendLine($"Extreme Weather Hazard: {curWeather.label.ToUpper()}");
                else
                    sb.AppendLine($"Weather: {curWeather.label} (atmosphere)");
            }

            return sb.ToString();
        }

        private static string BuildPawnSection(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== COLONIST: {pawn.LabelShort} ===");

            int age = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            sb.AppendLine($"Age: {age}, {pawn.gender}");

            sb.AppendLine(GetDetailedStatus(pawn));

            string backstory = GetBackstorySummary(pawn);
            if (!string.IsNullOrEmpty(backstory))
                sb.AppendLine($"Background: {backstory}");

            sb.AppendLine(BuildTraitsSection(pawn));

            if (pawn.InMentalState && pawn.MentalState?.def != null)
                sb.AppendLine($"[MENTAL STATE ACTIVE: {pawn.MentalState.def.label.ToUpper()}]");

            float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
            sb.AppendLine($"Mood: {GetMoodDesc(mood)} ({mood:P0})");

            var topThoughts = pawn.needs?.mood?.thoughts?.memories?.Memories?
                .Where(t => t != null && Math.Abs(t.MoodOffset()) > 3f)
                .OrderByDescending(t => Math.Abs(t.MoodOffset()))
                .Take(3).ToList();
            if (topThoughts != null && topThoughts.Any())
                sb.AppendLine($"Feelings: {string.Join(", ", topThoughts.Select(t => t.LabelCap))}");

            string drugState = GetDrugState(pawn);
            if (!string.IsNullOrEmpty(drugState))
                sb.AppendLine(drugState);

            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.35f)
                sb.AppendLine($"Pain Level: {GetPainDesc(pain)} ({pain:P0})");

            string speechNote = GetSpeechImpediment(pawn);
            if (!string.IsNullOrEmpty(speechNote))
                sb.AppendLine(speechNote);

            string customPrompt = ColonistPromptManager.GetPrompt(pawn);
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine("# Custom Instructions:");
                sb.AppendLine(customPrompt.Trim());
            }

            return sb.ToString();
        }

        private static string BuildPawnMemorySection(Pawn pawn)
        {
            var memoryManager = ColonistMemoryManager.GetOrCreate();
            var tracker = memoryManager?.GetTrackerFor(pawn);
            if (tracker == null) return "";

            // 1. Días anteriores: Obtener el resumen consolidado de los últimos 2 días cerrados
            var pastDaysMemories = tracker.GetLastMemories(2); // List<string> con [DateHeader] + resumen
            bool hasPast = pastDaysMemories != null && pastDaysMemories.Any(m => !string.IsNullOrWhiteSpace(m));

            // 2. Día actual: GetCurrentDayMemoryFormatted() devuelve un string continuo
            string todayRaw = tracker.GetCurrentDayMemoryFormatted();
            List<string> todayLines = null;

            if (!string.IsNullOrWhiteSpace(todayRaw))
            {
                var rawLines = todayRaw
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // Tomamos como máximo las últimas 4 interacciones de hoy para no sobrecargar el monólogo
                int takeCount = Math.Min(4, rawLines.Count);
                todayLines = rawLines.Skip(rawLines.Count - takeCount).ToList();
            }

            bool hasToday = todayLines != null && todayLines.Any();

            if (!hasPast && !hasToday) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== RECENT PERSONAL HISTORY ===");

            // A) Primero el trasfondo de días pasados (Cronológico)
            if (hasPast)
            {
                sb.AppendLine("Past Days Summary:");
                foreach (var mem in pastDaysMemories)
                {
                    if (string.IsNullOrWhiteSpace(mem)) continue;
                    sb.AppendLine(mem.Trim());
                }
            }

            // B) Luego los acontecimientos de hoy
            if (hasToday)
            {
                if (hasPast) sb.AppendLine(); // Separador entre secciones
                sb.AppendLine("Today's Events:");
                foreach (var line in todayLines)
                {
                    sb.AppendLine($"  {line.Trim()}");
                }
            }

            return sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string CleanXmlTags(string input) =>
            string.IsNullOrEmpty(input) ? "" : TagRegex.Replace(input, "").Trim();

        private static string GetTimeDesc(int hour) =>
            hour < 5 ? "early morning" : hour < 9 ? "morning" : hour < 14 ? "midday" : hour < 18 ? "afternoon" : hour < 22 ? "evening" : "night";

        private static string GetLocationDesc(Pawn pawn)
        {
            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return "outdoors";
            if (room.ContainedBeds.Any()) return room.ContainedBeds.Count() == 1 ? "a bedroom" : "the barracks";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b.def.defName.Contains("Stove"))) return "the kitchen";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b is Building_WorkTable)) return "a workshop";
            return "indoors";
        }

        private static string GetMoodDesc(float mood) =>
            mood > 0.8f ? "very happy" : mood > 0.6f ? "content" : mood > 0.4f ? "neutral" : mood > 0.2f ? "stressed" : "on the edge of breakdown";

        private static string GetPainDesc(float pain) =>
            pain > 0.75f ? "agonizing" : pain > 0.50f ? "severe" : pain > 0.25f ? "moderate" : "mild";

        private static string GetDetailedStatus(Pawn pawn)
        {
            if (pawn.IsSlaveOfColony) return "Status: SLAVE";
            if (pawn.IsPrisonerOfColony) return "Status: PRISONER";
            return pawn.IsFreeColonist ? "Status: Free colonist" : "Status: Colony member";
        }

        private static string GetBackstorySummary(Pawn pawn)
        {
            if (pawn.story == null) return null;
            var childhood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);

            var parts = new List<string>();
            if (childhood != null) parts.Add($"Childhood: {childhood.title}");
            if (adulthood != null) parts.Add($"Adulthood: {adulthood.title}");

            return parts.Any() ? string.Join(" | ", parts) : null;
        }

        private static string GetDrugState(Pawn pawn)
        {
            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;

            var states = new List<string>();
            foreach (var h in hediffs)
            {
                if (h?.def == null) continue;
                switch (h.def.defName)
                {
                    case "GoJuiceHigh": states.Add("HIGH on Go-Juice"); break;
                    case "YayoHigh": states.Add("HIGH on Yayo"); break;
                    case "BeerHigh": states.Add("Drunk"); break;
                    case "SmokeleafHigh": states.Add("HIGH on Smokeleaf"); break;
                }
            }
            return states.Any() ? $"[CHEMICAL STATE: {string.Join("; ", states)}]" : null;
        }

        private static string GetSpeechImpediment(Pawn pawn)
        {
            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return null;

            if (hediffSet.hediffs.Any(h => h.Part?.def?.defName == "Tongue" && h.def.defName == "MissingBodyPart"))
                return "[SPEECH: TONGUE REMOVED — short gestures/sounds only]";

            return null;
        }

        private static string BuildTraitsSection(Pawn pawn)
        {
            if (pawn.story?.traits == null || !pawn.story.traits.allTraits.Any())
                return "*Traits:* None";

            var entries = new List<string>();

            foreach (var t in pawn.story.traits.allTraits)
            {
                // 1. Obtener la descripción nativa traducida y formateada para el peón
                string rawDesc = t.CurrentData?.description ?? t.def?.description;
                string formattedDesc = "";

                if (!string.IsNullOrEmpty(rawDesc))
                {
                    // El método Formatted + AdjustedFor de RimWorld reemplaza {PAWN_nameDef}, {PAWN_pronoun}, etc.
                    formattedDesc = rawDesc.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).ToString();

                    // Limpieza de posibles tags de color o XML residuales
                    formattedDesc = System.Text.RegularExpressions.Regex.Replace(formattedDesc, "<.*?>", "").Trim();
                }

                string label = t.LabelCap;

                if (!string.IsNullOrEmpty(formattedDesc))
                {
                    entries.Add($"  - {label}: \"{formattedDesc}\"");
                }
                else
                {
                    entries.Add($"  - {label}");
                }
            }

            return "*Traits & Core Identity:*\n" + string.Join("\n", entries);
        }
    }
}