using System.Text;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace EchoColony
{
    public static class PromptFragments
    {
        public static string BuildMoodDescription(Pawn pawn)
        {
            string mentalState = pawn.MentalState != null ? pawn.MentalState.def.label : "stable";
            float moodValue = pawn.needs != null && pawn.needs.mood != null ? pawn.needs.mood.CurInstantLevel : 1f;

            string moodDescription;
            if (moodValue >= 0.9f) moodDescription = "feeling great";
            else if (moodValue >= 0.7f) moodDescription = "in a good mood";
            else if (moodValue >= 0.5f) moodDescription = "somewhat okay";
            else if (moodValue >= 0.3f) moodDescription = "pretty upset";
            else moodDescription = "mentally struggling";

            return "*Mental State:* " + mentalState + ", *Mood:* " + moodDescription;
        }

        public static string BuildInventory(Pawn pawn)
        {
            List<string> items = new List<string>();
            if (pawn.inventory != null && pawn.inventory.innerContainer != null)
                items.AddRange(pawn.inventory.innerContainer.Select(i => i.LabelCap));
            if (pawn.apparel != null && pawn.apparel.WornApparel != null)
                items.AddRange(pawn.apparel.WornApparel.Select(a => a.LabelCap));
            if (pawn.equipment != null)
                items.AddRange(pawn.equipment.AllEquipmentListForReading.Select(e => e.LabelCap));
            return "*Inventory:* " + (items.Any() ? string.Join(", ", items.Distinct()) : "None");
        }

        public static string BuildEventSummary(Pawn pawn, int maxPersonal, int maxColony)
        {
            List<string> personal = new List<string>();
            List<string> colony = new List<string>();
            string name = pawn.LabelShort;

            foreach (string entry in EventLogger.events.AsEnumerable().Reverse())
            {
                string clean = CleanText(entry);
                if (entry.Contains(name) && personal.Count < maxPersonal)
                    personal.Add("- " + clean);
                else if ((entry.Contains("explosion") || entry.Contains("died") || entry.Contains("constructed")) && colony.Count < maxColony)
                    colony.Add("- " + clean);
            }

            StringBuilder sb = new StringBuilder();
            if (personal.Any())
            {
                sb.AppendLine("*Recent personal events:*");
                sb.AppendLine(string.Join("\n", personal));
            }
            if (colony.Any())
            {
                sb.AppendLine("*Recent colony events:*");
                sb.AppendLine(string.Join("\n", colony));
            }
            return sb.ToString();
        }

        public static string BuildThoughts(Pawn pawn)
        {
            List<string> thoughts = new List<string>();
            var memories = pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.thoughts != null ? pawn.needs.mood.thoughts.memories.Memories : null;

            if (memories != null)
            {
                foreach (Thought_Memory t in memories)
                {
                    if (t.VisibleInNeedsTab)
                    {
                        float offset = t.MoodOffset();
                        string impact = offset >= 10 ? "strongly uplifting"
                            : offset >= 5 ? "uplifting"
                            : offset >= 1 ? "slightly positive"
                            : offset <= -10 ? "deeply upsetting"
                            : offset <= -5 ? "upsetting"
                            : offset <= -1 ? "slightly negative"
                            : "neutral";
                        thoughts.Add(t.LabelCap + ": " + impact);
                    }
                }
            }

            return thoughts.Any()
                ? "*Thoughts affecting mood:*\n" + string.Join("\n", thoughts.Distinct())
                : "*Thoughts affecting mood:* None";
        }

        public static string BuildDisabledWorkTags(Pawn pawn)
        {
            WorkTags disabled = WorkTags.None;
            foreach (WorkTypeDef def in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if (pawn.WorkTagIsDisabled(def.workTags))
                    disabled |= def.workTags;
            }
            return disabled != WorkTags.None
                ? "*Disabled work tags:* " + disabled
                : "*Disabled work tags:* None";
        }

        public static string BuildFactionOverview()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("*Known factions:*");

            foreach (Faction fac in Find.FactionManager.AllFactionsListForReading)
            {
                if (fac != Faction.OfPlayer && !fac.Hidden && !fac.defeated)
                {
                    string name = fac.Name;
                    string relation = fac.RelationKindWith(Faction.OfPlayer).ToString();
                    string leader = fac.leader != null ? fac.leader.LabelShort : "unknown leader";

                    var settlements = Find.WorldObjects.Settlements.Where(s => s.Faction == fac).ToList();
                    string settlementNames = settlements.Any() ? string.Join(", ", settlements.Select(s => s.LabelCap)) : "no known settlements";

                    sb.AppendLine("- " + name + " (" + relation + "), led by " + leader + ", settlements: " + settlementNames);
                }
            }

            return sb.ToString();
        }

        public static string BuildEnvironmentInfo(Pawn pawn)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("*Environment & Activity:*");

            float pain = pawn.health != null && pawn.health.hediffSet != null ? pawn.health.hediffSet.PainTotal : 0f;
            if (pain > 0f)
                sb.AppendLine("- Pain level: " + pain.ToStringPercent());

            Room room = pawn.GetRoom();
            if (room != null && room.Role != null)
                sb.AppendLine("- Room role: " + room.Role.label);

            if (pawn.CurJob != null && pawn.CurJob.def != null)
                sb.AppendLine("- Current job: " + pawn.CurJob.def.label.CapitalizeFirst());

            Building_Bed bed = pawn.ownership != null ? pawn.ownership.OwnedBed : null;
            if (bed != null && bed.OwnersForReading.Count > 1)
            {
                string others = string.Join(", ", bed.OwnersForReading.Where(p => p != pawn).Select(p => p.LabelShort));
                sb.AppendLine("- Shares bed with: " + others);
            }

            Pawn bondedAnimal = pawn.relations != null ? pawn.relations.DirectRelations.FirstOrDefault(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && r.otherPawn.RaceProps.Animal)?.otherPawn : null;
            if (bondedAnimal != null)
                sb.AppendLine("- Bonded animal: " + bondedAnimal.LabelShort);

            string weather = Find.CurrentMap != null && Find.CurrentMap.weatherManager != null && Find.CurrentMap.weatherManager.curWeather != null ? Find.CurrentMap.weatherManager.curWeather.label.CapitalizeFirst() : "Unknown";
            sb.AppendLine("- Current weather: " + weather);

            Map map = Find.CurrentMap;
            if (map != null)
            {
                int medicineCount = 0;
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.IsMedicine)
                        medicineCount += map.resourceCounter.GetCount(def);
                }
                if (medicineCount < 5)
                    sb.AppendLine("\u26A0 Low on medicine");

                if (map.resourceCounter.TotalHumanEdibleNutrition < 15f)
                    sb.AppendLine("\u26A0 Low on food");

                if (map.gameConditionManager != null && map.gameConditionManager.ActiveConditions.Any(c => c.Label.Contains("heat") || c.Label.Contains("cold")))
                    sb.AppendLine("\u26A0 Unfavorable temperature conditions");
            }

            return sb.ToString();
        }

        public static string CleanText(string input)
        {
            return input.Replace("<color=", "").Replace("</color>", "").Replace(">", ": ").Replace("#", "");
        }
    }
}
