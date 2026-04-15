using Verse;
using RimWorld;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text.RegularExpressions;

namespace EchoColony
{
    public static class KoboldPromptBuilder
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

            // ── Time & location ───────────────────────────────────────────────────
            int tile = Find.CurrentMap.Tile;
            Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
            float longitude = longLat.x;

            int ticks    = Find.TickManager.TicksAbs;
            int year     = GenDate.Year(ticks, longitude);
            string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
            int day      = GenDate.DayOfSeason(ticks, longitude);
            int hour     = GenDate.HourOfDay(ticks, longitude);

            Room currentRoom = pawn.GetRoom();
            string ubicacion = currentRoom?.Role?.label ?? "an unspecified place";
            string timeInfo  = $"It is {hour:00}:00 on Day {day} of {quadrum}, Year {year}. Location: {ubicacion}.";
            string bedroomStatus = pawn.ownership?.OwnedBed?.GetRoom()?.Role?.label ?? "no personal room";

            // ── Pawn data ─────────────────────────────────────────────────────────
            string name     = pawn.LabelShort;
            string age      = pawn.ageTracker.AgeBiologicalYears.ToString();
            string job      = pawn.jobs?.curDriver?.GetReport() ?? "Idle";
            string health   = pawn.health?.summaryHealth?.SummaryHealthPercent.ToStringPercent() ?? "unknown";
            string mood     = pawn.needs?.mood?.CurInstantLevel.ToStringPercent() ?? "unknown";
            string activity = pawn.CurJob?.def?.label ?? "Idle";
            string weather  = pawn.Map != null ? pawn.Map.weatherManager.curWeather.label : "unknown";

            var inventory  = pawn.inventory?.innerContainer.Select(i => i.LabelCap).ToList()  ?? new List<string>();
            var apparel    = pawn.apparel?.WornApparel.Select(i => i.LabelCap).ToList()        ?? new List<string>();
            var equipment  = pawn.equipment?.AllEquipmentListForReading.Select(i => i.LabelCap).ToList() ?? new List<string>();
            string items   = inventory.Concat(apparel).Concat(equipment).Distinct().Any()
                           ? string.Join(", ", inventory.Concat(apparel).Concat(equipment).Distinct())
                           : "nothing equipped or carried";

            string childhood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Childhood)?.title.CapitalizeFirst() ?? "Unknown childhood";
            string adulthood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood)?.title.CapitalizeFirst() ?? "Unknown adulthood";

            Pawn partner = pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse)
                        ?? pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
            string partnerInfo = partner != null ? $"I share my life with {partner.LabelShort}." : "I live alone.";

            var socialLines = pawn.relations?.DirectRelations
                ?.Where(r => r.otherPawn != null && r.otherPawn != pawn)
                ?.Select(r => $"- {r.def.label.CapitalizeFirst()} of {r.otherPawn.LabelShort}")
                ?.ToList();
            string socialText = socialLines?.Any() == true ? string.Join("\n", socialLines) : "None";

            string bonded  = pawn.relations?.DirectRelations
                ?.Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn.RaceProps.Animal)
                ?.Select(r => r.otherPawn.LabelShort).FirstOrDefault() ?? "";
            string bondLine = bonded != "" ? $"Bonded animal: {bonded}" : "";

            string ideo     = pawn.Ideo?.name ?? "no ideology";
            string xenotype = pawn.genes?.Xenotype?.label ?? "standard human";

            var traits = pawn.story?.traits?.allTraits;
            string traitsText = traits?.Any() == true
                ? string.Join(", ", traits.Select(t => t.LabelCap))
                : "None";

            var skills = pawn.skills?.skills?.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).Take(6);
            string skillsText = skills != null
                ? string.Join(", ", skills.Select(s => $"{s.def.label}: {s.Level}"))
                : "No notable skills.";

            string personalitymod = "";
            string personalityInfo = PersonalityIntegration.GetPersonalitySummary(pawn);
            if (!string.IsNullOrEmpty(personalityInfo))
                personalitymod = $"This colonist follows the personality type {personalityInfo}. Their words and actions are shaped by this inner nature.";
            else if (!string.IsNullOrEmpty(traitsText))
                personalitymod = $"Based on their traits ({traitsText}), their tone and behavior should reflect this personality.";

            string threatStatus = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);

            // ── Recent events (language-agnostic filtering) ───────────────────────
            var personalEvents = new List<string>();
            var colonyEvents   = new List<string>();
            const int maxPersonal = 10;
            const int maxColony   = 6;

            try
            {
                if (Find.PlayLog != null)
                {
                    int currentTick  = Find.TickManager.TicksGame;
                    int threeDaysAgo = currentTick - (60000 * 3);

                    foreach (var entry in Find.PlayLog.AllEntries
                        .Where(e => e.Tick >= threeDaysAgo)
                        .OrderByDescending(e => e.Tick))
                    {
                        if (personalEvents.Count >= maxPersonal && colonyEvents.Count >= maxColony) break;

                        try
                        {
                            string logText = entry.ToGameStringFromPOV(pawn);
                            if (string.IsNullOrWhiteSpace(logText)) continue;

                            // Strip color tags properly
                            string clean = Regex.Replace(logText, @"<color=#[0-9A-Fa-f]{6,8}>", "");
                            clean = clean.Replace("</color>", "").Trim();
                            if (string.IsNullOrWhiteSpace(clean)) continue;

                            if (logText.Contains(pawn.LabelShort) && personalEvents.Count < maxPersonal)
                                personalEvents.Add("- " + clean);
                            else if (colonyEvents.Count < maxColony &&
                                     !(entry is PlayLogEntry_Interaction)) // only non-interaction colony events
                                colonyEvents.Add("- " + clean);
                        }
                        catch { continue; }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] KoboldPromptBuilder event error: {ex.Message}");
            }

            // ── Verified colony history (TalesCache) ──────────────────────────────
            var tales = TalesCache.GetTalesFor(pawn, TalesCache.MAX_PERSONAL_TALES);

            // ── Custom prompts ────────────────────────────────────────────────────
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            // ── Chat history ──────────────────────────────────────────────────────
            List<string> chatLog = ChatGameComponent.Instance.GetChat(pawn);
            string chatHistory   = string.Join("\n", chatLog.TakeLast(10));

            // ════════════════════════════════════════════════════════════════════
            // BUILD PROMPT
            // ════════════════════════════════════════════════════════════════════

            // Anti-hallucination rules first — most impactful position
            sb.AppendLine("STRICT GROUNDING RULES:");
            sb.AppendLine("A section called 'VERIFIED PERSONAL HISTORY' will appear below.");
            sb.AppendLine("That is the ONLY source of past events you may reference.");
            sb.AppendLine("If a technology, building, item, animal, or event is NOT in that section");
            sb.AppendLine("or in the current game state, it does NOT exist in this colony.");
            sb.AppendLine("Never invent or fabricate memories. If unsure — omit it.");
            sb.AppendLine();

            sb.AppendLine($"My name is {name}. I am {age} years old, currently in {ubicacion}.");
            sb.AppendLine($"I'm {job}. Health: {health}. Mood: {mood}.");
            sb.AppendLine($"Activity: {activity}. Weather: {weather}.");
            sb.AppendLine(timeInfo);
            sb.AppendLine($"Sleeping conditions: {bedroomStatus}.");
            sb.AppendLine($"I carry or wear: {items}.");
            sb.AppendLine($"Background: {childhood}, then {adulthood}.");
            sb.AppendLine(partnerInfo);
            sb.AppendLine($"Ideology: {ideo}. Xenotype: {xenotype}.");
            sb.AppendLine($"Traits: {traitsText}.");
            sb.AppendLine($"Skills: {skillsText}.");
            if (!string.IsNullOrWhiteSpace(bondLine)) sb.AppendLine(bondLine);
            sb.AppendLine($"Social relations:\n{socialText}");
            sb.AppendLine($"Threat status: {threatStatus}");
            sb.AppendLine($"Combat status: {combatStatus}");
            if (!string.IsNullOrWhiteSpace(personalitymod)) sb.AppendLine(personalitymod);
            sb.AppendLine();

            if (personalEvents.Any() || colonyEvents.Any())
            {
                sb.AppendLine("Recent events (from game log):");
                if (personalEvents.Any())
                {
                    sb.AppendLine("Personal:");
                    foreach (var e in personalEvents) sb.AppendLine(e);
                }
                if (colonyEvents.Any())
                {
                    sb.AppendLine("Colony:");
                    foreach (var e in colonyEvents) sb.AppendLine(e);
                }
                sb.AppendLine();
            }

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
            // Must be included for [ACTION:...] tags to work with local models.
            // ColonistPromptContextBuilder adds this automatically for cloud providers;
            // local model builders need to add it explicitly.
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
                    Log.Warning($"[EchoColony] KoboldPromptBuilder: failed to build action prompt: {ex.Message}");
                }
            }

            if (chatLog.Any())
            {
                sb.AppendLine("Recent conversation with the player:");
                sb.AppendLine(chatHistory);
                sb.AppendLine();
            }

            sb.AppendLine("Now the player says:");
            sb.AppendLine($"\"{userMessage}\"");
            sb.AppendLine();
            sb.AppendLine($"Respond in {langDisplay}, as yourself. Stay in character. No emojis or quotation marks unless quoting someone.");

            return sb.ToString();
        }
    }
}