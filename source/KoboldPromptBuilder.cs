using Verse;
using RimWorld;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace EchoColony
{
    public static class KoboldPromptBuilder
    {
        public static string Build(Pawn pawn, string userMessage)
        {
            var sb = new StringBuilder();

            // Idioma
            string idiomaJuego = Prefs.LangFolderName?.ToLower() ?? "english";
string langDisplay = idiomaJuego.StartsWith("es") ? "Spanish"
                    : idiomaJuego.StartsWith("en") ? "English"
                    : idiomaJuego.StartsWith("fr") ? "French"
                    : idiomaJuego.StartsWith("de") ? "German"
                    : idiomaJuego.StartsWith("ko") ? "Korean"
                    : idiomaJuego.StartsWith("ru") ? "Russian"
                    : "your current language";

            // Room actual
            Room currentRoom = pawn.GetRoom();
            string ubicacion = currentRoom?.Role?.label ?? "an unspecified place";                  

                        // Tiempo actual
             int tile = Find.CurrentMap.Tile;
            Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
            float longitude = longLat.x;

            int ticks = Find.TickManager.TicksAbs;
            int year = GenDate.Year(ticks, longitude);
            string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
            int day = GenDate.DayOfSeason(ticks, longitude);
            int hour = GenDate.HourOfDay(ticks, longitude);
            string timeInfo = $"It is currently {hour:00}:00 on Day {day} of {quadrum}, Year {year}. You are located in {ubicacion}. You may refer to this context in your responses.\n";

            // Estado de habitación/cama
            string bedroomStatus = pawn.ownership?.OwnedBed?.GetRoom()?.Role?.label ?? "no personal room";
            string roomInfo = $"Sleeping conditions: {bedroomStatus}.";

            // Eventos recientes
            const int maxPersonal = 10;
            const int maxColony = 6;
            List<string> personalEvents = new List<string>();
            List<string> colonyEvents = new List<string>();

            foreach (var e in EventLogger.events.AsEnumerable().Reverse())
            {
                string cleanEvent = CleanText(e);

                if (e.Contains(pawn.LabelShort))
                {
                    personalEvents.Add("- " + cleanEvent);
                    if (personalEvents.Count >= maxPersonal) continue;
                }
                else if (e.Contains("explosion") || e.Contains("constructed") || e.Contains("born") || e.Contains("died") || e.Contains("relationship"))
                {
                    colonyEvents.Add("- " + cleanEvent);
                    if (colonyEvents.Count >= maxColony) continue;
                }
            }

            string summary = "";
            if (personalEvents.Count > 0)
                summary += "Recent personal events:\n" + string.Join("\n", personalEvents) + "\n";

            if (colonyEvents.Count > 0)
                summary += "Recent colony events:\n" + string.Join("\n", colonyEvents) + "\n";

            // Prompts personalizados
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            // Datos del colono
            string name = pawn.LabelShort;
            string age = pawn.ageTracker.AgeBiologicalYears.ToString();
            string job = pawn.jobs?.curDriver?.GetReport() ?? "Idle";
            string location = pawn.GetRoom()?.Role?.label ?? "an unspecified place";
            string health = pawn.health?.summaryHealth?.SummaryHealthPercent.ToStringPercent() ?? "unknown";
            string mood = pawn.needs?.mood?.CurInstantLevel.ToStringPercent() ?? "unknown mood";
            string activity = pawn.CurJob?.def?.label ?? "Idle";
            string weather = pawn.Map != null ? pawn.Map.weatherManager.curWeather.label : "unknown";
            string room = pawn.GetRoom()?.Role?.label ?? "unknown room";

            List<string> inventory = pawn.inventory?.innerContainer.Select(i => i.LabelCap).ToList() ?? new List<string>();
            List<string> apparel = pawn.apparel?.WornApparel.Select(i => i.LabelCap).ToList() ?? new List<string>();
            List<string> equipment = pawn.equipment?.AllEquipmentListForReading.Select(i => i.LabelCap).ToList() ?? new List<string>();
            List<string> allItems = inventory.Concat(apparel).Concat(equipment).Distinct().ToList();
            string items = allItems.Any() ? string.Join(", ", allItems) : "nothing equipped or carried";

            string childhood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Childhood)?.title.CapitalizeFirst() ?? "Unknown childhood";
            string adulthood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood)?.title.CapitalizeFirst() ?? "Unknown adulthood";

            Pawn partner = pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse)
                             ?? pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
            string partnerInfo = partner != null ? $"I share my life with {partner.LabelShort}." : "I live alone.";

            var socialLines = pawn.relations?.DirectRelations
                ?.Where(r => r.otherPawn != null && r.otherPawn != pawn)
                ?.Select(r => $"- {r.def.label.CapitalizeFirst()} of {r.otherPawn.LabelShort}")
                ?.ToList();
            string socialText = (socialLines != null && socialLines.Any()) ? string.Join("\n", socialLines) : "None";

            string bonded = pawn.relations?.DirectRelations
                ?.Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn.RaceProps.Animal)
                ?.Select(r => r.otherPawn.LabelShort).FirstOrDefault() ?? "";
            string bondLine = bonded != "" ? $"Bonded animal: {bonded}" : "";

            string ideo = pawn.Ideo?.name ?? "no ideology";
            string xenotype = pawn.genes?.Xenotype?.label ?? "standard human";

            var traits = pawn.story?.traits?.allTraits;
            string traitsText = traits != null && traits.Any()
                ? string.Join(", ", traits.Select(t => t.LabelCap))
                : "None";

            var skills = pawn.skills?.skills?.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).Take(6);
            string skillsText = skills != null
                ? string.Join(", ", skills.Select(s => $"{s.def.label}: {s.Level}"))
                : "No notable skills.";

            string personalitymod = "";
            string personalityInfo = PersonalityIntegration.GetPersonalitySummary(pawn);
            if (!string.IsNullOrEmpty(personalityInfo))
            {
                personalitymod = $"\n\nThis colonist follows the personality type {personalityInfo}. Their actions and words are often guided by this inner nature—revealing itself in how they bond, argue, comfort, or lead.";
            }
            else if (!string.IsNullOrEmpty(traitsText))
            {
                personalitymod = $"\n\nBased on their traits ({traitsText}), this colonist has a specific personality. Their responses should reflect these traits—affecting their tone, behavior, and emotional expression.";
            }

            string threatStatus = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);

            List<string> chatLog = ChatGameComponent.Instance.GetChat(pawn);
            string chatHistory = string.Join("\n", chatLog.TakeLast(10));

            sb.AppendLine($"My name is {name}. I am {age} years old and I currently am in {location} (room: {room}). I'm {job}. My health is {health} and I feel {mood}.");
            sb.AppendLine($"Currently, I'm: {activity}. Weather: {weather}.");
            sb.AppendLine(timeInfo);
            sb.AppendLine(roomInfo);
            sb.AppendLine($"I carry or wear: {items}.");
            sb.AppendLine($"My background is: {childhood}, then {adulthood}.");
            sb.AppendLine(partnerInfo);
            sb.AppendLine($"Ideology: {ideo}, Xenotype: {xenotype}.");
            sb.AppendLine($"Traits: {traitsText}.");
            sb.AppendLine($"Skills: {skillsText}.");
            if (!string.IsNullOrWhiteSpace(bondLine)) sb.AppendLine(bondLine);
            sb.AppendLine($"Social relations:\n{socialText}");
            sb.AppendLine($"Threat status: {threatStatus}");
            sb.AppendLine($"Combat status: {combatStatus}");
            sb.AppendLine(personalitymod);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(summary))
            {
                sb.AppendLine(summary);
            }

            if (!string.IsNullOrWhiteSpace(globalPrompt)) sb.AppendLine($"[Global instructions: {globalPrompt}]");
            if (!string.IsNullOrWhiteSpace(customPrompt)) sb.AppendLine($"[Character instructions: {customPrompt}]");

            if (chatLog.Any())
            {
                sb.AppendLine("Recent conversation with the player:");
                sb.AppendLine(chatHistory);
                sb.AppendLine();
            }

            sb.AppendLine("Now the player says:");
            sb.AppendLine($"\"{userMessage}\"");
            sb.AppendLine();
            sb.AppendLine($"Respond in {idiomaJuego}, as yourself. Stay in character and avoid emojis or quotation marks unless quoting someone.");

            return sb.ToString();
        }

        private static string CleanText(string input)
        {
            return input.Replace("<color=", "").Replace("</color>", "").Replace(">", ": ").Replace("#", "");
        }
    }
}
