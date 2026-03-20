using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Builds the AI prompt for a single-pawn monologue.
    /// Much leaner than PawnConversationPromptBuilder — only what the pawn
    /// knows from their own perspective: state, job, mood, environment, self.
    /// Always requests exactly ONE line of dialogue.
    /// </summary>
    public static class PawnMonologuePromptBuilder
    {
        public static string Build(Pawn pawn, string triggerContext = null)
        {
            if (pawn == null) return null;

            var sb = new StringBuilder();

            sb.AppendLine(BuildSystemInstruction(pawn, triggerContext));
            sb.AppendLine(BuildPawnSection(pawn));
            sb.AppendLine(BuildMoodSection(pawn));
            sb.AppendLine(BuildJobSection(pawn));
            sb.AppendLine(BuildEnvironmentSection(pawn));
            sb.AppendLine(BuildOutputInstruction());

            return sb.ToString();
        }

        // ── System instruction ────────────────────────────────────────────────────

        private static string BuildSystemInstruction(Pawn pawn, string triggerContext)
        {
            string lang = Prefs.LangFolderName?.ToLower() ?? "english";
            string langLine = lang != "english" ? $"Respond in {lang}.\n" : "";

            string triggerLine = "";
            if (!string.IsNullOrWhiteSpace(triggerContext))
                triggerLine = $"Something just happened to {pawn.LabelShort}: {triggerContext}\n" +
                              $"This is what prompted them to speak.\n";

            return
                $"{langLine}" +
                $"Write ONE line of spontaneous self-talk for a RimWorld colonist named {pawn.LabelShort}.\n" +
                triggerLine +
                $"The line is something they mutter under their breath, think aloud, or say to no one in particular.\n" +
                $"It must feel natural and human — not robotic, not poetic, not meta.\n" +
                $"It should reflect their personality, what they're doing right now, or what's on their mind.\n";
        }

        // ── Pawn section ──────────────────────────────────────────────────────────

        private static string BuildPawnSection(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {pawn.LabelShort.ToUpper()} ===");

            // Age & gender
            string gender = pawn.gender == Gender.Male ? "male" : pawn.gender == Gender.Female ? "female" : "nonbinary";
            sb.AppendLine($"{pawn.ageTracker?.AgeBiologicalYears ?? 0}yo {gender}");

            // Backstory
            string child = pawn.story?.Childhood?.TitleShortFor(pawn.gender);
            string adult = pawn.story?.Adulthood?.TitleShortFor(pawn.gender);
            if (!string.IsNullOrWhiteSpace(child) || !string.IsNullOrWhiteSpace(adult))
            {
                string backstory = string.Join(" / ", new[] { child, adult }.Where(s => !string.IsNullOrWhiteSpace(s)));
                sb.AppendLine($"Backstory: {backstory}");
            }

            // Traits (max 3 most relevant)
            var traits = pawn.story?.traits?.allTraits;
            if (traits != null && traits.Count > 0)
            {
                var traitLabels = traits.Take(3).Select(t => t.LabelCap).ToList();
                sb.AppendLine($"Traits: {string.Join(", ", traitLabels)}");
            }

            // Top skill
            var skills = pawn.skills?.skills;
            if (skills != null)
            {
                var topSkill = skills.OrderByDescending(s => s.Level).FirstOrDefault();
                if (topSkill != null && topSkill.Level >= 8)
                    sb.AppendLine($"Expert in: {topSkill.def.label} (level {topSkill.Level})");
            }

            return sb.ToString();
        }

        // ── Mood section ──────────────────────────────────────────────────────────

        private static string BuildMoodSection(Pawn pawn)
        {
            var needs = pawn.needs;
            if (needs?.mood == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== STATE OF MIND ===");

            float moodPct = needs.mood.CurLevelPercentage;
            string moodDesc = moodPct > 0.75f ? "content"
                            : moodPct > 0.50f ? "okay"
                            : moodPct > 0.25f ? "stressed"
                            : "miserable";
            sb.AppendLine($"Mood: {moodDesc} ({moodPct:P0})");

            // Dominant thoughts (max 3, significant ones only)
            var thoughts = new List<Thought>();
            pawn.needs.mood.thoughts?.GetAllMoodThoughts(thoughts);
            var significant = thoughts
                .Where(t => System.Math.Abs(t.MoodOffset()) >= 4f)
                .OrderByDescending(t => System.Math.Abs(t.MoodOffset()))
                .Take(3)
                .ToList();

            if (significant.Any())
            {
                var thoughtLines = significant.Select(t =>
                {
                    float offset = t.MoodOffset();
                    string sign = offset > 0 ? "+" : "";
                    return $"{t.LabelCap} ({sign}{offset:F0})";
                });
                sb.AppendLine($"On their mind: {string.Join(", ", thoughtLines)}");
            }

            // Basic needs
            var hunger = needs.food;
            if (hunger != null && hunger.CurLevelPercentage < 0.3f)
                sb.AppendLine($"Hunger: {(hunger.CurLevelPercentage < 0.1f ? "starving" : "hungry")}");

            var rest = needs.rest;
            if (rest != null && rest.CurLevelPercentage < 0.3f)
                sb.AppendLine($"Fatigue: {(rest.CurLevelPercentage < 0.1f ? "exhausted" : "tired")}");

            // Pain
            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.35f)
                sb.AppendLine($"Pain: {(pain > 0.75f ? "agonizing" : pain > 0.5f ? "severe" : "moderate")} ({pain:P0})");

            // Mental state
            if (pawn.InMentalState)
                sb.AppendLine($"Mental state: {pawn.MentalStateDef?.label ?? "breakdown"}");

            return sb.ToString();
        }

        // ── Job section ───────────────────────────────────────────────────────────

        private static string BuildJobSection(Pawn pawn)
        {
            var job = pawn.CurJob;
            if (job == null) return "";

            string jobLabel = job.def?.reportString ?? job.def?.label ?? "working";

            // Try to get a human-readable description
            try
            {
                string report = pawn.jobs?.curDriver?.GetReport();
                if (!string.IsNullOrWhiteSpace(report))
                    jobLabel = report.TrimEnd('.');
            }
            catch { }

            return $"=== CURRENT ACTIVITY ===\n{jobLabel}\n";
        }

        // ── Environment section ───────────────────────────────────────────────────

        private static string BuildEnvironmentSection(Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== ENVIRONMENT ===");

            // Time of day
            float hour = GenLocalDate.HourFloat(map);
            string timeDesc = hour < 5f ? "middle of the night"
                            : hour < 8f ? "early morning"
                            : hour < 12f ? "morning"
                            : hour < 14f ? "midday"
                            : hour < 18f ? "afternoon"
                            : hour < 21f ? "evening"
                            : "night";
            sb.AppendLine($"Time: {timeDesc}");

            // Weather
            if (map.weatherManager?.curWeather != null)
                sb.AppendLine($"Weather: {map.weatherManager.curWeather.label}");

            // Indoors/outdoors
            bool isRoofed = pawn.Position.Roofed(map);
            sb.AppendLine(isRoofed ? "Location: indoors" : "Location: outdoors");

            // Temperature extreme
            float temp = pawn.AmbientTemperature;
            if (temp < -10f) sb.AppendLine($"Temperature: freezing ({temp:F0}°C)");
            else if (temp > 40f) sb.AppendLine($"Temperature: dangerously hot ({temp:F0}°C)");

            return sb.ToString();
        }

        // ── Output instruction ────────────────────────────────────────────────────

        private static string BuildOutputInstruction()
        {
            return
                "Write ONLY the single line of dialogue — nothing else.\n" +
                "No speaker label, no quotes around it, no stage directions, no explanation.\n" +
                "1–2 sentences maximum. Natural, in-character, grounded in what they're experiencing.";
        }
    }
}