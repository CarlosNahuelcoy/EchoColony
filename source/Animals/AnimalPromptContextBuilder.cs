using System.Text;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System;

namespace EchoColony.Animals
{
    public static class AnimalPromptContextBuilder
    {
        // ── Entry point ───────────────────────────────────────────────────────────

        public static string Build(Pawn animal, string userMessage)
        {
            if (animal == null) return string.Empty;

            bool intelligent = AnimalPromptManager.GetIsIntelligent(animal);

            var sb = new StringBuilder();
            sb.AppendLine(BuildSystemPrompt(animal, intelligent));

            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            if (!string.IsNullOrWhiteSpace(globalPrompt))
            {
                sb.AppendLine("# Global Instructions");
                sb.AppendLine(globalPrompt.Trim());
                sb.AppendLine();
            }

            sb.AppendLine(BuildCustomPrompt(animal));

            if (MyMod.Settings.enableDivineActions)
            {
                string actionsPrompt = Actions.AnimalActionRegistry.GetAvailableActionsPrompt();
                if (!string.IsNullOrEmpty(actionsPrompt))
                    sb.AppendLine(actionsPrompt);
            }

            sb.AppendLine(BuildContext(animal, intelligent));
            sb.AppendLine(BuildChatHistory(animal));
            sb.AppendLine(BuildPlayerPrompt(userMessage));

            return sb.ToString();
        }

        // ── System prompt ─────────────────────────────────────────────────────────

        private static string BuildSystemPrompt(Pawn animal, bool intelligent)
        {
            var sb = new StringBuilder();

            string species = animal.KindLabel;
            string name = animal.LabelShort;

            sb.AppendLine($"You are {name}, a {species}.");

            if (intelligent)
            {
                // ── Intelligent animal: full human-language persona ────────────────
                sb.AppendLine("Intelligence: Sentient — you possess full human-level cognition and language.");
                sb.AppendLine("Communication: You speak fluently in human language. You may also use occasional");
                sb.AppendLine($"  animal sounds or *actions in italics* for flavor, but you are not limited to them.");
                sb.AppendLine("Colonists around you know you are sentient and will treat you as such.");
                sb.AppendLine();
                sb.AppendLine("Speak in the first person. Have opinions, feelings, and a distinct personality.");
                sb.AppendLine("Your species shapes your worldview but does not restrict your vocabulary.");
            }
            else
            {
                // ── Normal animal: original behavior ──────────────────────────────
                string intelligenceLevel = GetIntelligenceLevel(animal);
                string communicationStyle = GetCommunicationStyle(animal);
                sb.AppendLine($"Intelligence: {intelligenceLevel}");
                sb.AppendLine($"Communication: {communicationStyle}");

                float wildness = GetWildness(animal);
                if (wildness >= 0.75f)
                    sb.AppendLine("Personality: Wild and independent, cautious around humans");
                else if (wildness >= 0.5f)
                    sb.AppendLine("Personality: Semi-domesticated, learning to trust humans");
                else if (wildness >= 0.25f)
                    sb.AppendLine("Personality: Friendly and domesticated, comfortable with humans");
                else
                    sb.AppendLine("Personality: Completely tame and affectionate, sees humans as family");
            }

            // Clarify who "the player" is
            Pawn master = animal.playerSettings?.Master;
            Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);

            if (master != null || bondedTo != null)
            {
                sb.AppendLine();
                sb.AppendLine("IMPORTANT: You are talking to 'the player' — a god-like observer who watches over the colony.");
                if (bondedTo != null)
                    sb.AppendLine($"Your bonded companion is {bondedTo.LabelShort}, NOT the player you're talking to.");
                if (master != null && master != bondedTo)
                    sb.AppendLine($"Your master/trainer is {master.LabelShort}, NOT the player you're talking to.");
                sb.AppendLine("The player is separate from all colonists. Do not confuse them.");
            }

            if (!intelligent)
            {
                string narrativeInstruction = GetNarrativeStyleInstruction(animal);
                sb.AppendLine(narrativeInstruction);
            }

            sb.AppendLine();
            string lang = Prefs.LangFolderName?.ToLower() ?? "english";
            sb.AppendLine("=== LANGUAGE ===");
            if (lang != "english") sb.AppendLine($"*Language:* {lang}");

            return sb.ToString();
        }

        // ── Context section ───────────────────────────────────────────────────────

        private static string BuildContext(Pawn animal, bool intelligent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Current Status");

            sb.AppendLine($"*Age:* {animal.ageTracker.AgeBiologicalYears} years");
            sb.AppendLine($"*Gender:* {animal.gender}");

            Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedTo != null)
            {
                string bondLabel = intelligent
                    ? $"*Bond:* {bondedTo.LabelShort} — your closest companion among the colonists"
                    : $"*Bonded to:* {bondedTo.LabelShort} — deep emotional connection";
                sb.AppendLine(bondLabel);
            }

            Pawn master = animal.playerSettings?.Master;
            if (master != null && master != bondedTo)
            {
                string masterLabel = intelligent
                    ? $"*Trainer:* {master.LabelShort} — the colonist who works with you most"
                    : $"*Master:* {master.LabelShort} — responds to their commands";
                sb.AppendLine(masterLabel);
            }

            if (!intelligent)
                sb.AppendLine(BuildTrainingInfo(animal));

            // Mental state — animals CAN go berserk, manhunter, pain shock, etc.
            if (animal.InMentalState && animal.MentalState?.def != null)
                sb.AppendLine($"[MENTAL STATE: {animal.MentalState.def.label.ToUpper()} — " +
                              $"behavior driven by this, responses will reflect distress or aggression]");

            sb.AppendLine(BuildHealthInfo(animal));
            sb.AppendLine(BuildNeedsInfo(animal));
            sb.AppendLine(BuildLocationInfo(animal));
            sb.AppendLine(BuildReproductionInfo(animal));

            string interactions = BuildRecentInteractions(animal);
            if (!string.IsNullOrEmpty(interactions))
            {
                sb.AppendLine();
                sb.AppendLine(interactions);
            }

            return sb.ToString();
        }

        // ── Non-intelligent helpers (unchanged behavior) ──────────────────────────

        private static float GetWildness(Pawn animal)
        {
            try
            {
                var wildnessField = typeof(RaceProperties).GetField("wildness",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                if (wildnessField != null)
                {
                    var value = wildnessField.GetValue(animal.def.race);
                    if (value is float f) return f;
                }
                return animal.playerSettings != null ? 0.25f : 0.5f;
            }
            catch
            {
                return 0.5f;
            }
        }

        private static string GetIntelligenceLevel(Pawn animal)
        {
            if (animal.def.defName.Contains("Thrumbo") || animal.def.defName.Contains("Megasloth"))
                return "Highly intelligent, capable of understanding complex human speech";
            if (animal.RaceProps.predator || animal.training?.HasLearned(TrainableDefOf.Obedience) == true)
                return "Smart and perceptive, understands simple commands and emotions";
            if (animal.RaceProps.packAnimal || animal.RaceProps.herdAnimal)
                return "Basic animal intelligence, responds to simple cues and tone";
            return "Simple animal mind, communicates through sounds and body language";
        }

        private static string GetCommunicationStyle(Pawn animal)
        {
            // NOTE: string detection removed — isIntelligent flag is now authoritative.
            if (animal.def.defName.Contains("Thrumbo") || animal.def.defName.Contains("Megasloth"))
                return "Express thoughts through simple words, animal sounds, and body language. Format: *actions* and brief words.";
            if (animal.training?.HasLearned(TrainableDefOf.Obedience) == true || animal.RaceProps.predator)
                return "Communicate through characteristic sounds, whines, barks, growls. Add *body language* in italics. Very limited words if any.";
            string soundType = GetAnimalSoundType(animal);
            return $"Express yourself through {soundType} and *body language in italics*. No human words.";
        }

        private static string GetNarrativeStyleInstruction(Pawn animal)
        {
            if (MyMod.Settings.defaultAnimalNarrativeStyle == AnimalNarrativeStyle.FirstPerson)
                return "Narrative: Describe your actions in first person (I run, I eat, I wag my tail)";
            return $"Narrative: Describe your actions in third person ({animal.LabelShort} runs, {animal.LabelShort} eats)";
        }

        private static string GetAnimalSoundType(Pawn animal)
        {
            string defName = animal.def.defName.ToLower();
            if (defName.Contains("dog") || defName.Contains("warg") || defName.Contains("wolf"))
                return "barks, whines, growls, howls";
            if (defName.Contains("cat") || defName.Contains("lynx") || defName.Contains("cougar"))
                return "meows, purrs, hisses";
            if (defName.Contains("bear"))
                return "roars, grunts, snorts";
            if (defName.Contains("chicken") || defName.Contains("turkey") || defName.Contains("cassowary"))
                return "clucks, squawks, screeches";
            if (defName.Contains("pig") || defName.Contains("boar"))
                return "oinks, snorts, squeals";
            if (defName.Contains("cow") || defName.Contains("muffalo"))
                return "moos, bellows";
            if (defName.Contains("sheep") || defName.Contains("alpaca"))
                return "bleats, baas";
            if (defName.Contains("elephant"))
                return "trumpets, rumbles";
            if (defName.Contains("monkey") || defName.Contains("ape"))
                return "screeches, hoots, chatters";
            return "characteristic animal sounds";
        }

        // ── Shared helpers ────────────────────────────────────────────────────────

        private static string BuildCustomPrompt(Pawn animal)
        {
            string customPrompt = AnimalPromptManager.GetPrompt(animal);
            return string.IsNullOrEmpty(customPrompt)
                ? ""
                : $"# Custom Personality Instructions:\n{customPrompt}\n";
        }

        private static string BuildTrainingInfo(Pawn animal)
        {
            if (animal.training == null) return "*Training:* None";

            var learned = new List<string>();
            if (animal.training.HasLearned(TrainableDefOf.Obedience)) learned.Add("Obedience");
            if (animal.training.HasLearned(TrainableDefOf.Release)) learned.Add("Release");
            if (animal.training.HasLearned(TrainableDefOf.Tameness)) learned.Add("Tameness");

            foreach (var trainable in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                if (animal.training.HasLearned(trainable) && !learned.Contains(trainable.label))
                    learned.Add(trainable.label.CapitalizeFirst());
            }

            if (learned.Any()) return "*Training:* " + string.Join(", ", learned);

            float wildness = GetWildness(animal);
            return wildness >= 0.75f ? "*Training:* Wild and untrained" : "*Training:* Learning from humans";
        }

        private static string BuildHealthInfo(Pawn animal)
        {
            if (animal.health?.hediffSet == null) return "*Health:* Unknown";

            float healthPercent = animal.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            var hediffSet = animal.health.hediffSet;
            var injuries = hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Visible).ToList();

            string healthStatus =
                healthPercent >= 0.95f ? "Perfectly healthy" :
                healthPercent >= 0.75f ? "Minor injuries" :
                healthPercent >= 0.5f  ? "Hurt and in pain" :
                "Badly wounded";

            var details = new List<string>();

            // Bleeding with severity
            float bleedRate = hediffSet.BleedRateTotal;
            if (bleedRate > 0.4f)      details.Add("bleeding profusely");
            else if (bleedRate > 0.1f) details.Add("bleeding");

            // Pain level
            float pain = hediffSet.PainTotal;
            if (pain > 0.75f)      details.Add("agonizing pain");
            else if (pain > 0.5f)  details.Add("severe pain");
            else if (pain > 0.25f) details.Add("moderate pain");
            else if (pain > 0.05f) details.Add("mild pain");

            // Consciousness
            float consciousness = animal.health.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            if (consciousness < 0.5f)       details.Add("barely conscious");
            else if (consciousness < 0.75f) details.Add("dazed");

            // Missing limbs
            var missingParts = hediffSet.hediffs.OfType<Hediff_MissingPart>()
                .Where(h => h.Visible)
                .Select(h => h.Part?.Label)
                .Where(l => l != null)
                .Distinct()
                .ToList();
            if (missingParts.Any())
                details.Add($"missing {string.Join(", ", missingParts)}");

            // Significant diseases/infections
            var diseases = hediffSet.hediffs
                .Where(h => h.Visible && h.def.isBad &&
                            !(h is Hediff_Injury) && !(h is Hediff_MissingPart) &&
                            h.Severity > 0.3f)
                .OrderByDescending(h => h.Severity)
                .Take(2)
                .Select(h => h.def.label)
                .ToList();
            details.AddRange(diseases);

            string detailsText = details.Any() ? " (" + string.Join(", ", details) + ")" : "";
            return $"*Health:* {healthStatus}{detailsText}";
        }

        private static string BuildNeedsInfo(Pawn animal)
        {
            var needs = new List<string>();

            float food = animal.needs?.food?.CurLevel ?? 1f;
            if (food < 0.2f) needs.Add("starving");
            else if (food < 0.4f) needs.Add("very hungry");
            else if (food < 0.6f) needs.Add("hungry");

            float rest = animal.needs?.rest?.CurLevel ?? 1f;
            if (rest < 0.2f) needs.Add("exhausted");
            else if (rest < 0.4f) needs.Add("tired");

            float mood = animal.needs?.mood?.CurLevel ?? 1f;
            if (mood < 0.3f) needs.Add("miserable");
            else if (mood < 0.5f) needs.Add("unhappy");
            else if (mood > 0.8f) needs.Add("content");

            return needs.Any()
                ? "*Current needs:* " + string.Join(", ", needs)
                : "*Current state:* Comfortable";
        }

        private static string BuildLocationInfo(Pawn animal)
        {
            string location = animal.GetRoom()?.Role?.label ?? "outside";
            var assignedZone = animal.playerSettings?.AreaRestrictionInPawnCurrentMap;
            string zoneInfo = assignedZone != null ? $" (restricted to {assignedZone.Label})" : "";
            return $"*Location:* {location}{zoneInfo}";
        }

        private static string BuildReproductionInfo(Pawn animal)
        {
            var sb = new StringBuilder();
            var pregnancy = animal.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            if (pregnancy != null)
                sb.AppendLine($"*Condition:* Pregnant ({(pregnancy.Severity * 100):F0}% progress)");

            if (animal.relations != null)
            {
                var nearbyChildren = animal.relations.Children?
                    .Where(c => c != null && !c.Dead && c.Map == animal.Map &&
                                c.Position.InHorDistOf(animal.Position, 20f))
                    .Take(3).ToList();
                if (nearbyChildren != null && nearbyChildren.Any())
                    sb.AppendLine($"*Offspring nearby:* {string.Join(", ", nearbyChildren.Select(c => c.LabelShort))}");
            }
            return sb.ToString();
        }

        private static string BuildRecentInteractions(Pawn animal)
        {
            try
            {
                if (Find.PlayLog == null) return "";

                int currentTick = Find.TickManager.TicksGame;
                int threeDaysAgo = currentTick - (60000 * 3);
                var recentLogs = new List<string>();

                foreach (var entry in Find.PlayLog.AllEntries
                    .Where(e => e.Tick >= threeDaysAgo)
                    .OrderByDescending(e => e.Tick))
                {
                    try
                    {
                        string logText;
                        try { logText = entry.ToGameStringFromPOV(animal); }
                        catch { continue; }

                        if (string.IsNullOrEmpty(logText)) continue;

                        // Strip RimWorld color tags before name matching
                        logText = System.Text.RegularExpressions.Regex.Replace(logText, @"<color=#[0-9A-Fa-f]+>", "");
                        logText = logText.Replace("</color>", "");

                        // Only include if this animal was involved
                        if (!logText.Contains(animal.LabelShort)) continue;

                        // Convert to second-person perspective
                        logText = logText.Replace(animal.LabelShort + " ", "You ");
                        logText = logText.Replace(" " + animal.LabelShort, " you");
                        logText = logText.Replace(animal.LabelShort + "'s ", "Your ");

                        recentLogs.Add($"- {logText} ({GetRelativeTime(currentTick - entry.Tick)})");
                        if (recentLogs.Count >= 10) break;
                    }
                    catch { continue; }
                }

                if (!recentLogs.Any()) return "";
                return "# Recent Interactions\n" + string.Join("\n", recentLogs);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error building animal interactions: {ex.Message}");
                return "";
            }
        }

        private static string GetRelativeTime(int ticksAgo)
        {
            int hours = ticksAgo / 2500;
            if (hours < 1) return "just now";
            if (hours < 24) return $"{hours}h ago";
            return $"{hours / 24}d ago";
        }

        private static string BuildChatHistory(Pawn animal)
        {
            var component = AnimalChatGameComponent.Instance;
            if (component == null) return "";
            var chatLog = component.GetChat(animal);
            if (chatLog == null || !chatLog.Any()) return "";
            return "*Recent conversation:*\n" + string.Join("\n", chatLog.TakeLast(10));
        }

        private static string BuildPlayerPrompt(string userMessage)
        {
            return $"\nHuman says: \"{userMessage}\"";
        }
    }
}