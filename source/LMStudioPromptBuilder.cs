using Verse;
using RimWorld;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace EchoColony
{
    public static class LMStudioPromptBuilder
    {
        public static string Build(Pawn pawn, string userMessage)
        {
            var sb = new StringBuilder();

            // ── Language ──────────────────────────────────────────────────────────
            string idiomaJuego = Prefs.LangFolderName?.ToLower() ?? "english";
            string langDisplay = idiomaJuego.StartsWith("es") ? "Spanish"
                               : idiomaJuego.StartsWith("en") ? "English"
                               : idiomaJuego.StartsWith("fr") ? "French"
                               : idiomaJuego.StartsWith("de") ? "German"
                               : idiomaJuego.StartsWith("ko") ? "Korean"
                               : idiomaJuego.StartsWith("ru") ? "Russian"
                               : "your current language";

            // ── Time ──────────────────────────────────────────────────────────────
            int tile       = Find.CurrentMap.Tile;
            Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
            float longitude = longLat.x;

            int ticks    = Find.TickManager.TicksAbs;
            int year     = GenDate.Year(ticks, longitude);
            string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
            int day      = GenDate.DayOfSeason(ticks, longitude);
            int hour     = GenDate.HourOfDay(ticks, longitude);

            string ubicacion = pawn.GetRoom()?.Role?.label ?? "an unspecified place";
            string timeInfo  = $"It is {hour:00}:00 on Day {day} of {quadrum}, Year {year}. Location: {ubicacion}.";

            // ── Pawn data ─────────────────────────────────────────────────────────
            string name     = pawn.LabelShort;
            string age      = pawn.ageTracker.AgeBiologicalYears.ToString();
            string job      = pawn.jobs?.curDriver?.GetReport() ?? "Idle";
            string location = ubicacion;
            string health   = pawn.health?.summaryHealth?.SummaryHealthPercent.ToStringPercent() ?? "unknown";

            string ideo     = pawn.Ideo?.name ?? "no ideology";
            string xenotype = pawn.genes?.Xenotype?.label ?? "standard human";

            var traits = pawn.story?.traits?.allTraits;
            string traitsText = traits?.Any() == true
                ? string.Join(", ", traits.Select(t => t.LabelCap))
                : "None";

            string personalitymod = "";
            string personalityInfo = PersonalityIntegration.GetPersonalitySummary(pawn);
            if (!string.IsNullOrEmpty(personalityInfo))
                personalitymod = $"This colonist follows the personality type {personalityInfo}. Their words and actions reflect this inner nature.";
            else if (!string.IsNullOrEmpty(traitsText))
                personalitymod = $"Based on their traits ({traitsText}), their tone and behavior should reflect this personality.";

            string threatStatus = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);

            string modoRespuesta = MyMod.Settings?.enableRoleplayResponses == true
                ? "Speak as if you truly are this colonist. Be immersive and aware of your surroundings. You may use narrative actions wrapped like <b><i>this</i></b>, but avoid overdoing it."
                : "Speak naturally from your own perspective. Be aware of your memories, mood, and surroundings. Keep it real and human.";

            // ── Verified colony history (TalesCache) ──────────────────────────────
            var tales = TalesCache.GetTalesFor(pawn, TalesCache.MAX_PERSONAL_TALES);

            // ── Custom prompts ────────────────────────────────────────────────────
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            // ── Chat history ──────────────────────────────────────────────────────
            List<string> chatLog = ChatGameComponent.Instance.GetChat(pawn);

            // ════════════════════════════════════════════════════════════════════
            // BUILD PROMPT
            // ════════════════════════════════════════════════════════════════════

            // Anti-hallucination rules first
            sb.AppendLine("STRICT GROUNDING RULES:");
            sb.AppendLine("A section called 'VERIFIED PERSONAL HISTORY' will appear below.");
            sb.AppendLine("That is the ONLY source of past events you may reference.");
            sb.AppendLine("If a technology, building, item, animal, or event is NOT in that section");
            sb.AppendLine("or in the current game state, it does NOT exist in this colony.");
            sb.AppendLine("Never invent or fabricate memories. If unsure — omit it.");
            sb.AppendLine();

            sb.AppendLine($"You are {name}, a colonist in RimWorld.");
            sb.AppendLine($"- Age: {age}");
            sb.AppendLine($"- Job: {job}");
            sb.AppendLine($"- Location: {location}");
            sb.AppendLine($"- Health: {health}");
            sb.AppendLine($"- Time: {timeInfo}");
            sb.AppendLine($"- Ideology: {ideo}");
            sb.AppendLine($"- Xenotype: {xenotype}");
            sb.AppendLine(PromptFragments.BuildEnvironmentInfo(pawn));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildMoodDescription(pawn));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildInventory(pawn));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildThoughts(pawn));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildDisabledWorkTags(pawn));
            sb.AppendLine();
            // Use the improved event summary that filters by pawn involvement
            sb.AppendLine(BuildFilteredEventSummary(pawn));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildFactionOverview());
            sb.AppendLine();
            sb.AppendLine($"- Threat status: {threatStatus}");
            sb.AppendLine($"- Combat status: {combatStatus}");
            if (!string.IsNullOrWhiteSpace(personalitymod)) sb.AppendLine(personalitymod);
            sb.AppendLine();

            // Verified history section
            if (tales.Any())
            {
                sb.AppendLine("VERIFIED PERSONAL HISTORY (REAL EVENTS — USE ONLY THESE):");
                sb.AppendLine("These events actually happened. Reference them freely.");
                sb.AppendLine("Never invent events not listed here.");
                foreach (var t in tales) sb.AppendLine($"  • {t}");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(globalPrompt)) sb.AppendLine($"[Global instructions: {globalPrompt}]");
            if (!string.IsNullOrWhiteSpace(customPrompt)) sb.AppendLine($"[Character instructions: {customPrompt}]");

            // ── Divine Actions ────────────────────────────────────────────────────
            if (MyMod.Settings?.enableDivineActions == true)
            {
                try
                {
                    string actionPrompt = Actions.ActionExecutor.BuildActionPrompt(pawn);
                    if (!string.IsNullOrWhiteSpace(actionPrompt))
                        sb.AppendLine(actionPrompt);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[EchoColony] LMStudioPromptBuilder: failed to build action prompt: {ex.Message}");
                }
            }

            sb.AppendLine();

            sb.AppendLine(modoRespuesta);
            sb.AppendLine();

            if (chatLog?.Any() == true)
            {
                sb.AppendLine("Recent conversation with the player:");
                sb.AppendLine(string.Join("\n", chatLog.TakeLast(20)));
                sb.AppendLine();
            }

            sb.AppendLine("Now the player says:");
            sb.AppendLine($"\"{userMessage}\"");
            sb.AppendLine();
            sb.AppendLine($"Respond in {langDisplay}, as yourself. Stay in character. No emojis or quotation marks unless quoting someone.");

            return sb.ToString();
        }

        /// <summary>
        /// Event summary that only includes events actually involving this pawn,
        /// avoiding the language-specific keyword problem in the original PromptFragments version.
        /// </summary>
        private static string BuildFilteredEventSummary(Pawn pawn, int maxPersonal = 10)
        {
            if (Find.PlayLog == null) return "";

            var events    = new List<string>();
            int now       = Find.TickManager.TicksGame;
            int threeDays = now - (60000 * 3);

            foreach (var entry in Find.PlayLog.AllEntries
                .Where(e => e.Tick >= threeDays)
                .OrderByDescending(e => e.Tick))
            {
                if (events.Count >= maxPersonal) break;
                try
                {
                    string text = entry.ToGameStringFromPOV(pawn);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = System.Text.RegularExpressions.Regex.Replace(text, @"<color=#[0-9A-Fa-f]{6,8}>", "");
                    text = text.Replace("</color>", "").Trim();

                    if (!text.Contains(pawn.LabelShort)) continue;

                    int ticksAgo = now - entry.Tick;
                    int hoursAgo = ticksAgo / 2500;
                    string timeAgo = hoursAgo < 1 ? "just now"
                                   : hoursAgo < 24 ? $"{hoursAgo}h ago"
                                   : $"{hoursAgo / 24}d ago";

                    events.Add($"- {text} ({timeAgo})");
                }
                catch { continue; }
            }

            if (!events.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("Recent personal events:");
            foreach (var e in events) sb.AppendLine(e);
            return sb.ToString();
        }
    }
}