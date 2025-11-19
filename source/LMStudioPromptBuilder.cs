using Verse;
using RimWorld;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace EchoColony
{
    public static class LMStudioPromptBuilder
    {
        public static string Build(Pawn pawn, string userMessage)
        {
            StringBuilder sb = new StringBuilder();

            string idiomaJuego = Prefs.LangFolderName != null ? Prefs.LangFolderName.ToLower() : "english";
            string langDisplay = idiomaJuego.StartsWith("es") ? "Spanish"
                                : idiomaJuego.StartsWith("en") ? "English"
                                : idiomaJuego.StartsWith("fr") ? "French"
                                : idiomaJuego.StartsWith("de") ? "German"
                                : idiomaJuego.StartsWith("ko") ? "Korean"
                                : idiomaJuego.StartsWith("ru") ? "Russian"
                                : "your current language";

            int tile = Find.CurrentMap.Tile;
            Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
            float longitude = longLat.x;

            int ticks = Find.TickManager.TicksAbs;
            int year = GenDate.Year(ticks, longitude);
            string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
            int day = GenDate.DayOfSeason(ticks, longitude);
            int hour = GenDate.HourOfDay(ticks, longitude);

            string ubicacion = pawn.GetRoom() != null && pawn.GetRoom().Role != null ? pawn.GetRoom().Role.label : "an unspecified place";
            string timeInfo = "It is currently " + hour.ToString("00") + ":00 on Day " + day + " of " + quadrum + ", Year " + year + ". You are located in " + ubicacion + ".";

            string globalPrompt = MyMod.Settings != null ? MyMod.Settings.globalPrompt : "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            string modoRespuesta = MyMod.Settings != null && MyMod.Settings.enableRoleplayResponses
                ? "Speak as if you truly are this colonist. Be immersive, aware of your surroundings, and describe things naturally. You may use narrative actions wrapped like <b><i>this</i></b>, but avoid overdoing it."
                : "Speak naturally, from your own perspective. Be aware of your memories, mood, and what's happening around you. Keep it real and human.";

            string name = pawn.LabelShort;
            string age = pawn.ageTracker.AgeBiologicalYears.ToString();
            string job = pawn.jobs?.curDriver?.GetReport() ?? "Idle";
            string location = ubicacion;
            string health = pawn.health != null && pawn.health.summaryHealth != null ? pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent() : "unknown";

            string ideo = pawn.Ideo != null ? pawn.Ideo.name : "no ideology";
            string xenotype = pawn.genes != null && pawn.genes.Xenotype != null ? pawn.genes.Xenotype.label : "standard human";
            var traits = pawn.story?.traits?.allTraits;
            string traitsText = traits != null && traits.Any()
                ? string.Join(", ", traits.Select(t => t.LabelCap))
                : "None";
            string personalitymod = "";
            string personalityInfo = PersonalityIntegration.GetPersonalitySummary(pawn);
            if (!string.IsNullOrEmpty(personalityInfo))
            {
                personalitymod = "\n\nThis colonist follows the personality type " + personalityInfo + ". Their actions and words are often guided by this inner nature—revealing itself in how they bond, argue, comfort, or lead.";
            }
            else if (!string.IsNullOrEmpty(traitsText))
            {
                personalitymod = "\n\nBased on their traits (" + traitsText + "), this colonist has a specific personality. Their responses should reflect these traits—affecting their tone, behavior, and emotional expression.";
            }

            string threatStatus = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);

            sb.AppendLine("You are " + name + ", a colonist in RimWorld.");
            sb.AppendLine("- Age: " + age);
            sb.AppendLine("- Job: " + job);
            sb.AppendLine("- Location: " + location);
            sb.AppendLine("- Health: " + health);
            sb.AppendLine("- Time: " + timeInfo);
            sb.AppendLine("- Ideology: " + ideo);
            sb.AppendLine("- Xenotype: " + xenotype);
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
            sb.AppendLine(PromptFragments.BuildEventSummary(pawn, 10, 6));
            sb.AppendLine();
            sb.AppendLine(PromptFragments.BuildFactionOverview());
            sb.AppendLine();
            sb.AppendLine("- Threat status: " + threatStatus);
            sb.AppendLine("- Combat status: " + combatStatus);
            sb.AppendLine(personalitymod);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(globalPrompt)) sb.AppendLine("[Global instructions: " + globalPrompt + "]");
            if (!string.IsNullOrWhiteSpace(customPrompt)) sb.AppendLine("[Character instructions: " + customPrompt + "]");
            sb.AppendLine();

            sb.AppendLine(modoRespuesta);
            sb.AppendLine();

            List<string> chatLog = ChatGameComponent.Instance.GetChat(pawn);
            if (chatLog != null && chatLog.Any())
            {
                sb.AppendLine("Recent conversation with the player:");
                sb.AppendLine(string.Join("\n", chatLog.TakeLast(20)));
                sb.AppendLine();
            }

            sb.AppendLine("Now the player says:");
            sb.AppendLine("\"" + userMessage + "\"");
            sb.AppendLine();
            sb.AppendLine("Respond in " + langDisplay + ", as yourself. Stay in character and avoid emojis or quotation marks unless quoting someone.");

            return sb.ToString();
        }
    }
}
