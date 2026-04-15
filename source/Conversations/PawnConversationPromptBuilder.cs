using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Builds the AI prompt for a spontaneous pawn-to-pawn conversation.
    /// 
    /// Pulls from:
    ///   • ColonistMemoryManager  — memories from direct player<->pawn chats
    ///   • DailyGroupMemoryTracker — recent colony-wide events
    ///   • RimWorld TaleManager   — verified real colony events (hunts, deaths, marriages, etc.)
    ///   • RimWorld game state    — relationship, mood, health, interaction type
    ///
    /// Returns a single prompt that asks the AI to produce a JSON array of
    /// dialogue lines, which PawnConversationManager then parses.
    /// </summary>
    public static class PawnConversationPromptBuilder
    {
        // ── Entry point ───────────────────────────────────────────────────────────

        public static string Build(
            Pawn initiator,
            Pawn recipient,
            InteractionDef interactionDef,
            int linesPerPawn = 3)
        {
            if (initiator == null || recipient == null) return null;

            int totalLines = linesPerPawn * 2;
            var sb = new StringBuilder();

            string logText = GetRecentInteractionLogText(initiator, recipient, interactionDef);

            sb.AppendLine(BuildSystemInstruction(totalLines, initiator.LabelShort, recipient.LabelShort, interactionDef, logText));
            sb.AppendLine(BuildInteractionTypeSection(interactionDef, logText));
            sb.AppendLine(BuildEnvironmentSection(initiator));
            sb.AppendLine(BuildPawnSection(initiator, "INITIATOR"));
            sb.AppendLine(BuildPawnSection(recipient, "RECIPIENT"));
            sb.AppendLine(BuildRelationshipSection(initiator, recipient));
            sb.AppendLine(BuildSharedMemorySection(initiator, recipient));
            sb.AppendLine(BuildColonyEventsSection(initiator, recipient));
            // ── NEW: Real verified events from TaleManager ────────────────────────
            sb.AppendLine(BuildVerifiedTalesSection(initiator, recipient));
            sb.AppendLine(BuildOutputInstruction(totalLines, initiator.LabelShort, recipient.LabelShort, interactionDef, logText));

            return sb.ToString();
        }

        // ── NEW: Verified real colony events from TaleManager ─────────────────────
        //
        // These are the exact same events RimWorld uses to generate art descriptions
        // ("In honor of when Morg hunted a thrumbo. Year 5.").
        // They are FACTS — not summaries, not AI-generated — and serve as the only
        // permitted source of historical references in the dialogue.

        private static string BuildVerifiedTalesSection(Pawn initiator, Pawn recipient)
        {
            var (shared, personalI) = TalesCache.GetTalesForPair(initiator, recipient,
                TalesCache.MAX_SHARED_TALES, TalesCache.MAX_PERSONAL_TALES);
            var (_, personalR) = TalesCache.GetTalesForPair(recipient, initiator,
                0, TalesCache.MAX_PERSONAL_TALES);

            if (!shared.Any() && !personalI.Any() && !personalR.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== VERIFIED COLONY HISTORY (REAL EVENTS — USE THESE, INVENT NOTHING ELSE) ===");
            sb.AppendLine("The following events ACTUALLY HAPPENED in this colony.");
            sb.AppendLine("Colonists may reference these freely. They must NEVER reference events, technology,");
            sb.AppendLine("structures, or items that do not appear here or in the game state above.");
            sb.AppendLine();

            if (shared.Any())
            {
                sb.AppendLine($"Events involving BOTH {initiator.LabelShort} and {recipient.LabelShort}:");
                foreach (var t in shared) sb.AppendLine($"  • {t}");
                sb.AppendLine();
            }

            if (personalI.Any())
            {
                sb.AppendLine($"Events involving {initiator.LabelShort}:");
                foreach (var t in personalI) sb.AppendLine($"  • {t}");
                sb.AppendLine();
            }

            if (personalR.Any())
            {
                sb.AppendLine($"Events involving {recipient.LabelShort}:");
                foreach (var t in personalR) sb.AppendLine($"  • {t}");
            }

            return sb.ToString();
        }

        // ── Extract real interaction text from play log ───────────────────────────

        private static string GetRecentInteractionLogText(Pawn a, Pawn b, InteractionDef def)
        {
            if (Find.PlayLog == null) return null;
            try
            {
                int now = Find.TickManager.TicksGame;
                int window = 600;

                foreach (var entry in Find.PlayLog.AllEntries
                    .Where(e => e.Tick >= now - window)
                    .OrderByDescending(e => e.Tick))
                {
                    if (!(entry is PlayLogEntry_Interaction)) continue;

                    if (def != null)
                    {
                        var intDefField = HarmonyLib.AccessTools.Field(entry.GetType(), "intDef");
                        var entryDef = intDefField?.GetValue(entry) as InteractionDef;
                        if (entryDef != def) continue;
                    }

                    string text = entry.ToGameStringFromPOV(a);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = System.Text.RegularExpressions.Regex.Replace(text, @"<color=#[0-9A-Fa-f]{6,8}>", "");
                    text = text.Replace("</color>", "").Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    string textLower = text.ToLowerInvariant();
                    if (!textLower.Contains(a.LabelShort.ToLowerInvariant())) continue;
                    if (!textLower.Contains(b.LabelShort.ToLowerInvariant())) continue;

                    return text;
                }
            }
            catch { }
            return null;
        }

        // ── System instruction ────────────────────────────────────────────────────
        //
        // ANTI-HALLUCINATION RULES are baked in here at the top level so the AI
        // receives them before any context — the most reliable position for hard rules.

        private static string BuildSystemInstruction(int totalLines, string nameA, string nameB,
            InteractionDef interactionDef = null, string logText = null)
        {
            string topicLine = "";
            if (!string.IsNullOrWhiteSpace(logText))
            {
                topicLine = $"PRIMARY TOPIC: Just now, {logText} — this is what they are talking about.\n" +
                            $"Write dialogue that naturally flows from this specific topic.\n" +
                            $"Character context (health, mood, traits) is background color only — " +
                            $"do NOT let it override or replace the topic unless it is extreme.\n";
            }
            else if (interactionDef != null)
            {
                string topic = !string.IsNullOrWhiteSpace(interactionDef.label)
                    ? interactionDef.label : "a general interaction";
                topicLine = $"PRIMARY TOPIC: The conversation was sparked by \"{topic}\". " +
                            $"This must be the main subject of their dialogue.\n" +
                            $"Context details (health, mood, etc.) are background — they do NOT dominate unless extreme.\n";
            }

            return
                $"You are writing natural, spontaneous dialogue between two RimWorld colonists.\n" +
                $"Write {totalLines} short lines alternating between {nameA} (starts first) and {nameB}.\n" +
                topicLine +
                $"Each line: 1–2 sentences max. Conversational and human — tone should fit their relationship, power dynamic, and traits (see context below).\n" +
                $"NO robotic phrasing. NO meta-commentary like 'That conversation was good'. " +
                $"Talk ABOUT the topic, not ABOUT having talked.\n" +
                $"IMPORTANT: If a participant is an animal, their lines must reflect their species " +
                $"and intelligence level as described in their section — sounds/body language for " +
                $"normal animals, words for intelligent ones.\n" +
                $"\n" +
                // ── ANTI-HALLUCINATION BLOCK ──────────────────────────────────────
                $"STRICT GROUNDING RULES — READ CAREFULLY:\n" +
                $"1. You will be given a section called VERIFIED COLONY HISTORY. " +
                $"   That is the ONLY source of past events you may reference.\n" +
                $"2. If a technology, building, item, animal, or event is NOT mentioned " +
                $"   in the VERIFIED COLONY HISTORY or the current game state, " +
                $"   it does NOT exist in this colony. Do NOT invent it.\n" +
                $"3. Examples of forbidden inventions: solar panels in a medieval colony, " +
                $"   a hospital if none is mentioned, research the colony never did, " +
                $"   people or animals not listed, events that never happened.\n" +
                $"4. If you have no verified history to reference, the colonists talk ONLY " +
                $"   about what is happening RIGHT NOW (the current topic, their job, the weather, " +
                $"   their mood). They do NOT recall the past at all.\n" +
                $"5. These rules override creativity. An immersion-breaking invented fact is " +
                $"   always worse than a short, grounded exchange.\n";
        }

        // ── Environment ───────────────────────────────────────────────────────────

        private static string BuildEnvironmentSection(Pawn pawn)
        {
            if (pawn?.Map == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine("=== ENVIRONMENT ===");

            var map = pawn.Map;
            int hour = GenLocalDate.HourOfDay(map);
            sb.AppendLine($"Time: {hour:D2}:00 ({GetTimeDesc(hour)})");
            sb.AppendLine($"Season: {GenLocalDate.Season(map)}");
            if (map.weatherManager?.curWeather != null)
                sb.AppendLine($"Weather: {map.weatherManager.curWeather.label}");

            float temp = pawn.AmbientTemperature;
            string tempDesc = temp < 0f ? "freezing" : temp < 15f ? "cold" : temp < 30f ? "comfortable" : "hot";
            sb.AppendLine($"Temperature: {temp:F0}°C ({tempDesc})");
            sb.AppendLine($"Location: {GetLocationDesc(pawn)}");

            return sb.ToString();
        }

        // ── Interaction type ──────────────────────────────────────────────────────

        private static string BuildInteractionTypeSection(InteractionDef def, string logText = null)
        {
            if (def == null && string.IsNullOrWhiteSpace(logText)) return "";
            var sb = new StringBuilder();
            sb.AppendLine("=== INTERACTION TRIGGER ===");

            if (!string.IsNullOrWhiteSpace(logText))
                sb.AppendLine($"What just happened: {logText}");

            if (def != null)
            {
                string nature = GetInteractionNature(def);
                sb.AppendLine($"Interaction type: {def.label} ({nature})");

                if (!IsKnownVanillaInteraction(def))
                {
                    string desc = def.description?.Trim();
                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        desc = System.Text.RegularExpressions.Regex.Replace(desc, "<.*?>", "").Trim();
                        if (desc.Length > 150) desc = desc.Substring(0, 147) + "...";
                        sb.AppendLine($"Context: {desc}");
                    }
                }
            }

            sb.AppendLine("Their dialogue must grow directly from this — not reference 'the conversation' itself.");
            return sb.ToString();
        }

        private static bool IsKnownVanillaInteraction(InteractionDef def)
        {
            string n = def.defName?.ToLower() ?? "";
            return n.Contains("chitchat")  || n.Contains("deeptalk") ||
                   n.Contains("romance")   || n.Contains("insult")   ||
                   n.Contains("slight")    || n.Contains("kind")     ||
                   n.Contains("rapport")   || n.Contains("comfort")  ||
                   n.Contains("joke")      || n.Contains("complain") ||
                   n.Contains("convert")   || n.Contains("recruit")  ||
                   n.Contains("rebelstir") || n.Contains("prisonbreak");
        }

        // ── Pawn context ──────────────────────────────────────────────────────────

        private static string BuildPawnSection(Pawn pawn, string role)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {role}: {pawn.LabelShort} ===");

            int age = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            sb.AppendLine($"Age: {age}, {pawn.gender}");
            string ageBehavior = GetAgeBehavior(age);
            if (!string.IsNullOrEmpty(ageBehavior))
                sb.AppendLine($"[AGE NOTE: {ageBehavior}]");

            sb.AppendLine(GetDetailedStatus(pawn));

            string xenoInfo = GetXenotypeInfo(pawn);
            if (!string.IsNullOrEmpty(xenoInfo))
                sb.AppendLine(xenoInfo);

            string backstory = GetBackstorySummary(pawn);
            if (!string.IsNullOrEmpty(backstory))
                sb.AppendLine($"Background: {backstory}");

            if (pawn.story?.traits?.allTraits != null && pawn.story.traits.allTraits.Any())
                sb.AppendLine($"Personality: {string.Join(", ", pawn.story.traits.allTraits.Select(t => t.LabelCap))}");

            string traitSpeech = GetTraitSpeechStyle(pawn);
            if (!string.IsNullOrEmpty(traitSpeech))
                sb.AppendLine(traitSpeech);

            if (pawn.RaceProps?.Animal == true)
            {
                bool intelligent = Animals.AnimalPromptManager.GetIsIntelligent(pawn);
                string species = pawn.kindDef?.label ?? pawn.def?.label ?? "animal";
                if (intelligent)
                    sb.AppendLine($"[ANIMAL — INTELLIGENT: {pawn.LabelShort} is a {species} capable of understanding and " +
                                  $"speaking human language. Speaks in words, with animal mannerisms.]");
                else
                    sb.AppendLine($"[ANIMAL — {species.ToUpper()}: communicates only through sounds and body language. " +
                                  $"Cannot speak human words. Express as *actions* and animal sounds.]");
            }

            if (pawn.InMentalState && pawn.MentalState?.def != null)
                sb.AppendLine($"[MENTAL STATE ACTIVE: {pawn.MentalState.def.label.ToUpper()} — " +
                              $"behavior is erratic and driven by this compulsion]");

            float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
            sb.AppendLine($"Mood: {GetMoodDesc(mood)} ({mood:P0})");

            var topThoughts = pawn.needs?.mood?.thoughts?.memories?.Memories?
                .Where(t => t != null && System.Math.Abs(t.MoodOffset()) > 3f)
                .OrderByDescending(t => System.Math.Abs(t.MoodOffset()))
                .Take(3).ToList();
            if (topThoughts != null && topThoughts.Any())
                sb.AppendLine($"Feelings: {string.Join(", ", topThoughts.Select(t => t.LabelCap))}");

            if (IsGrieving(pawn))
                sb.AppendLine("Grieving: recently lost someone from the colony");

            float food = pawn.needs?.food?.CurLevel ?? 1f;
            if (food < 0.15f)      sb.AppendLine("Hunger: starving — barely able to focus");
            else if (food < 0.3f)  sb.AppendLine("Hunger: very hungry — distracting");
            else if (food < 0.5f)  sb.AppendLine("Hunger: hungry");

            float rest = pawn.needs?.rest?.CurLevel ?? 1f;
            if (rest < 0.15f)      sb.AppendLine("Rest: exhausted — speech may be slow or irritable");
            else if (rest < 0.3f)  sb.AppendLine("Rest: very tired");

            string drugState = GetDrugState(pawn);
            if (!string.IsNullOrEmpty(drugState))
                sb.AppendLine(drugState);

            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.35f)
                sb.AppendLine($"Pain: {GetPainDesc(pain)} ({pain:P0}) — background discomfort");

            float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            if (consciousness < 0.85f)
                sb.AppendLine($"Consciousness: {consciousness:P0} — groggy, slow, impaired thinking");

            string mobilityNote = GetMobilityNote(pawn, age);
            if (!string.IsNullOrEmpty(mobilityNote))
                sb.AppendLine(mobilityNote);

            float manip = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 1f;
            if (manip < 0.5f)      sb.AppendLine("Manipulation: severely limited — can barely use hands");
            else if (manip < 0.8f) sb.AppendLine("Manipulation: reduced — limited use of hands/arms");

            string speechNote = GetSpeechImpediment(pawn);
            if (!string.IsNullOrEmpty(speechNote))
                sb.AppendLine(speechNote);

            var injuries = GetSignificantHealthConditions(pawn);
            if (injuries.Any())
                sb.AppendLine($"Health conditions: {string.Join(", ", injuries)}");

            if (pawn.CurJob?.def != null)
                sb.AppendLine($"Currently: {pawn.CurJob.def.reportString ?? pawn.CurJob.def.label}");

            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                sb.AppendLine($"Ideology: {pawn.Ideo.name}");

            return sb.ToString();
        }

        // ── Age behavior ──────────────────────────────────────────────────────────

        private static string GetAgeBehavior(int age)
        {
            if (age <= 1)  return "INFANT — only cries, coos, and babbles. Cannot form words or sentences.";
            if (age <= 3)  return "TODDLER — very simple words only, excited and curious, short attention span.";
            if (age <= 6)  return "YOUNG CHILD — simple sentences, asks lots of questions, innocent perspective.";
            if (age <= 10) return "CHILD — enthusiastic but childlike, does not fully grasp adult situations.";
            if (age <= 13) return "PRETEEN — starting to sound more mature but still young and naive.";
            if (age <= 17) return "TEENAGER — direct, sometimes emotional, wants to be taken seriously.";
            return null;
        }

        // ── Detailed status ───────────────────────────────────────────────────────

        private static string GetDetailedStatus(Pawn pawn)
        {
            if (pawn.IsSlaveOfColony)
            {
                string willDesc = "";
                if (pawn.guest != null)
                {
                    float will = pawn.guest.will;
                    if (will < 0.2f)      willDesc = " Will is broken — resigned, fearful, no resistance left.";
                    else if (will < 0.5f) willDesc = " Will is subdued — largely compliant but resentment simmers.";
                    else                  willDesc = " Will is strong — deeply resents captivity, may defy openly.";
                }
                return $"Status: SLAVE — no freedom, forced to obey, can be punished.{willDesc}";
            }

            if (pawn.IsPrisonerOfColony)
            {
                bool recruitable = pawn.guest?.Recruitable == true;
                string resistDesc = "";
                if (pawn.guest != null)
                {
                    float res = pawn.guest.resistance;
                    if (res > 20f)        resistDesc = " Highly resistant, deeply hostile.";
                    else if (res > 5f)    resistDesc = " Still resistant but starting to waver.";
                    else if (res <= 0f && recruitable) resistDesc = " Resistance broken — genuinely considering joining.";
                }
                string intent = recruitable
                    ? "Prisoner considering joining the colony — has doubts but is open."
                    : "Prisoner held against will — resents captors, wants freedom.";
                return $"Status: PRISONER — {intent}{resistDesc}";
            }

            if (pawn.IsFreeColonist) return "Status: Free colonist";
            return "Status: Colony member";
        }

        // ── Xenotype info (Biotech) ───────────────────────────────────────────────

        private static string GetXenotypeInfo(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null) return null;

            string xenoName = pawn.genes.xenotypeName ?? pawn.genes.Xenotype?.label ?? "baseline";
            if (pawn.genes.Xenotype == XenotypeDefOf.Baseliner && string.IsNullOrEmpty(pawn.genes.xenotypeName))
                return null;

            var parts = new List<string>();
            parts.Add($"Xenotype: {xenoName}");

            if (pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner)
            {
                string desc = pawn.genes.Xenotype.description ?? "";
                desc = System.Text.RegularExpressions.Regex.Replace(desc, "<.*?>", "").Trim();
                if (desc.Length > 100) desc = desc.Substring(0, 97) + "...";
                if (!string.IsNullOrEmpty(desc))
                    parts.Add($"({desc})");
            }

            if (ModsConfig.BiotechActive)
            {
                var hemogenDef = DefDatabase<NeedDef>.GetNamedSilentFail("Hemogen");
                if (hemogenDef != null)
                {
                    var hemogenNeed = pawn.needs?.TryGetNeed(hemogenDef);
                    if (hemogenNeed != null)
                    {
                        float level = hemogenNeed.CurLevelPercentage;
                        string hemDesc = level < 0.2f ? "CRITICALLY low hemogen — needs blood urgently"
                                       : level < 0.5f ? "low hemogen — needs to feed soon"
                                       : "requires blood (hemogen) to survive";
                        parts.Add(hemDesc);
                    }
                }
            }

            var appearGenes = pawn.genes?.GenesListForReading?
                .Where(g => g.Active && g.def.displayCategory != null &&
                    (g.def.displayCategory.defName == "Cosmetic" ||
                     g.def.displayCategory.defName == "Skin" ||
                     g.def.displayCategory.defName == "Body"))
                .Select(g => g.def.label)
                .Take(3).ToList();
            if (appearGenes != null && appearGenes.Any())
                parts.Add($"Appearance: {string.Join(", ", appearGenes)}");

            return string.Join(" | ", parts);
        }

        // ── Backstory summary ─────────────────────────────────────────────────────

        private static string GetBackstorySummary(Pawn pawn)
        {
            if (pawn.story == null) return null;

            var childhood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = pawn.story.AllBackstories?.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);

            var parts = new List<string>();

            if (childhood != null)
            {
                string desc = CleanBackstoryText(childhood.baseDesc ?? childhood.title ?? "", pawn);
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
                parts.Add($"Childhood — {childhood.title}: {desc}");
            }

            if (adulthood != null)
            {
                string desc = CleanBackstoryText(adulthood.baseDesc ?? adulthood.title ?? "", pawn);
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
                parts.Add($"Adulthood — {adulthood.title}: {desc}");
            }

            return parts.Any() ? string.Join(" | ", parts) : null;
        }

        private static string CleanBackstoryText(string text, Pawn pawn)
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
            text = text.Replace("[PAWN_nameDef]", pawn.LabelShort)
                       .Replace("[PAWN_pronoun]", pawn.gender == Gender.Male ? "he" : "she")
                       .Replace("[PAWN_possessive]", pawn.gender == Gender.Male ? "his" : "her")
                       .Trim();
            return text;
        }

        // ── Grief detection ───────────────────────────────────────────────────────

        private static bool IsGrieving(Pawn pawn)
        {
            try
            {
                return pawn.needs?.mood?.thoughts?.memories?.Memories?
                    .Any(t => t is Thought_MemorySocial soc &&
                              soc.def?.defName?.Contains("Died") == true &&
                              soc.otherPawn?.Dead == true) == true;
            }
            catch { return false; }
        }

        // ── Mobility ──────────────────────────────────────────────────────────────

        private static string GetMobilityNote(Pawn pawn, int age)
        {
            if (age <= 1) return null;
            if (age <= 3) return "[MOBILITY: Toddler — walks unsteadily, cannot keep up with adults]";

            float moving = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
            if (moving >= 0.5f) return null;

            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return $"[MOBILITY: {moving:P0} — severely impaired movement]";

            var abasia = hediffSet.hediffs.FirstOrDefault(h => h.def?.defName == "ParalyticAbasia");
            if (abasia != null)
                return "[MOBILITY: PARALYTIC ABASIA — neurological condition, legs work but walking is impossible. This is NOT pain. They may need to be carried or use support.]";

            var spinalInjury = hediffSet.hediffs.FirstOrDefault(h =>
                h.Visible && h.def.isBad &&
                (h.Part?.def?.defName == "Spine" || h.Part?.def?.label?.ToLower().Contains("spine") == true));
            if (spinalInjury != null)
                return $"[MOBILITY: SPINAL INJURY ({spinalInjury.def.label}) — movement severely impaired due to structural damage to the spine, not pain alone]";

            var missingLegs = hediffSet.hediffs.OfType<Hediff_MissingPart>()
                .Where(h => h.Part?.def?.defName == "Leg" || h.Part?.def?.defName == "Foot")
                .ToList();
            if (missingLegs.Count >= 2)
                return "[MOBILITY: Both legs missing — cannot walk at all without prosthetics]";
            if (missingLegs.Count == 1)
                return "[MOBILITY: One leg missing — severely impaired, hobbles with great difficulty]";

            var legProsthetics = hediffSet.hediffs
                .Where(h => h.def?.addedPartProps != null &&
                           (h.Part?.def?.defName == "Leg" || h.Part?.def?.defName == "Foot"))
                .ToList();
            if (legProsthetics.Any() && moving < 0.7f)
                return $"[MOBILITY: {moving:P0} — basic prosthetic legs limit movement significantly]";

            float pain = hediffSet.PainTotal;
            if (pain > 0.6f && moving < 0.5f)
                return $"[MOBILITY: {moving:P0} — severely impaired DUE TO EXTREME PAIN ({pain:P0}). This is pain-induced, NOT structural damage]";

            return $"[MOBILITY: {moving:P0} — movement severely impaired]";
        }

        // ── Drug state detection ──────────────────────────────────────────────────

        private static string GetDrugState(Pawn pawn)
        {
            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;

            var states = new List<string>();

            foreach (var h in hediffs)
            {
                if (h?.def == null) continue;
                string dn = h.def.defName;

                switch (dn)
                {
                    case "GoJuiceHigh":
                        states.Add("HIGH on go-juice: hyper-alert, aggressive confidence, almost reckless energy");
                        break;
                    case "YayoHigh":
                        states.Add("HIGH on yayo: euphoric, grandiose, talking fast, inflated self-worth");
                        break;
                    case "FlakeHigh":
                        states.Add("HIGH on flake: intensely euphoric, blissed-out, barely grounded in reality");
                        break;
                    case "WakeUpHigh":
                        states.Add("HIGH on wake-up: wired, sharp, no patience for slowness");
                        break;
                    case "PsychiteTea":
                    case "PsychiteTeaHigh":
                        states.Add("on psychite tea: mildly stimulated, calm alertness");
                        break;
                    case "BeerHigh":
                    {
                        float sev = h.Severity;
                        if (sev < 0.4f)
                            states.Add("slightly drunk: relaxed, a bit loose");
                        else if (sev < 0.8f)
                            states.Add("drunk: speech is slurring, inhibitions lowered, emotional");
                        else
                            states.Add("very drunk: barely coherent, stumbling words, could turn aggressive or weepy");
                        break;
                    }
                    case "SmokeleafHigh":
                        states.Add("HIGH on smokeleaf: slow, calm, easily distracted, deep philosophical tangents");
                        break;
                    case "Anesthetic":
                    {
                        float sev = h.Severity;
                        if (sev > 0.5f)
                            states.Add("under ANESTHESIA: barely conscious, speech barely possible, very confused");
                        else
                            states.Add("coming out of anesthesia: groggy, confused, slow to respond");
                        break;
                    }
                    case "PainKillerHigh":
                        states.Add("on painkillers: drowsy, detached, words come slow");
                        break;
                    case "GoJuiceWithdrawal":
                        states.Add("in go-juice WITHDRAWAL: exhausted, irritable, desperate");
                        break;
                    case "YayoWithdrawal":
                        states.Add("in yayo WITHDRAWAL: crashing hard, depressed, irritable");
                        break;
                    case "SmokeleafWithdrawal":
                        states.Add("in smokeleaf withdrawal: anxious, snappy");
                        break;
                    case "BeerWithdrawal":
                        states.Add("in alcohol withdrawal: shaky, nauseous, on edge");
                        break;
                    case "Luciferium":
                        states.Add("sustained by Luciferium: unnaturally calm, lucid, but dependent — knows it");
                        break;
                    case "LuciferiumWithdrawal":
                        states.Add("in LUCIFERIUM WITHDRAWAL: terrified, paranoid, deteriorating — will go berserk without a dose");
                        break;
                    case "NeuroquineHigh":
                        states.Add("on neuroquine: slightly sedated, muted emotions");
                        break;
                    case "PenoxycylineHigh":
                        states.Add("on penoxycyline: no noticeable high, just feels safe");
                        break;
                }
            }

            if (!states.Any()) return null;
            return $"[CHEMICAL STATE: {string.Join("; ", states)}]";
        }

        // ── Speech impediment detection ───────────────────────────────────────────

        private static string GetSpeechImpediment(Pawn pawn)
        {
            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return null;

            var notes = new List<string>();

            foreach (var h in hediffSet.hediffs)
            {
                if (h?.def == null) continue;
                string dn = h.def.defName;
                string label = h.def.label?.ToLower() ?? "";

                if (dn == "MissingBodyPart" && h.Part?.def?.defName == "Tongue")
                {
                    notes.Add("TONGUE REMOVED — speech is severely impaired, words are garbled and difficult to understand, " +
                              "very short sentences, uses gestures or nods when possible");
                    break;
                }

                if (h.Part?.def?.defName == "Jaw")
                {
                    if (dn == "MissingBodyPart")
                        notes.Add("JAW MISSING — speech barely possible, mostly grunts and single syllables");
                    else if (h.def.isBad && h.Severity > 0.3f)
                        notes.Add("jaw injured — speaking is painful, short clipped sentences");
                }

                if ((label.Contains("burn") || label.Contains("scar")) &&
                    (h.Part?.def?.defName == "Jaw" || h.Part?.def?.defName == "Head"))
                    notes.Add("facial scarring — may speak with difficulty or self-consciously");
            }

            float talking = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 1f;
            if (talking < 0.5f && !notes.Any())
                notes.Add($"talking capacity severely reduced ({talking:P0}) — speech is labored and minimal");
            else if (talking < 0.8f && !notes.Any())
                notes.Add($"talking capacity reduced ({talking:P0}) — speaks with noticeable difficulty");

            return notes.Any() ? $"[SPEECH: {string.Join("; ", notes)}]" : null;
        }

        // ── Significant health conditions ─────────────────────────────────────────

        private static List<string> GetSignificantHealthConditions(Pawn pawn)
        {
            var result = new List<string>();
            var hediffSet = pawn.health?.hediffSet;
            if (hediffSet == null) return result;

            foreach (var h in hediffSet.hediffs)
            {
                if (h?.def == null) continue;
                if (h.def.defName == "MissingBodyPart")
                {
                    string partName = h.Part?.def?.label ?? "body part";
                    if (h.Part?.def?.defName == "Tongue" || h.Part?.def?.defName == "Jaw") continue;
                    result.Add($"missing {partName}");
                }
            }

            var badHediffs = hediffSet.hediffs
                .Where(h => h.Visible && h.def.isBad &&
                            h.def.defName != "MissingBodyPart" &&
                            h.Severity > 0.25f)
                .OrderByDescending(h => h.Severity)
                .Take(3)
                .Select(h => h.def.label)
                .ToList();

            result.AddRange(badHediffs);
            return result;
        }

        // ── Pain descriptor ───────────────────────────────────────────────────────

        private static string GetPainDesc(float pain)
        {
            if (pain > 0.75f) return "agonizing";
            if (pain > 0.50f) return "severe";
            if (pain > 0.25f) return "moderate";
            return "mild";
        }

        // ── Relationship ──────────────────────────────────────────────────────────

        private static string BuildRelationshipSection(Pawn a, Pawn b)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RELATIONSHIP ===");

            bool aIsAnimal = a.RaceProps?.Animal == true;
            bool bIsAnimal = b.RaceProps?.Animal == true;

            if (aIsAnimal || bIsAnimal)
            {
                Pawn colonist = aIsAnimal ? b : a;
                Pawn animal   = aIsAnimal ? a : b;
                bool animalIntelligent = Animals.AnimalPromptManager.GetIsIntelligent(animal);

                Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
                Pawn master   = animal.playerSettings?.Master;
                bool isBonded = bondedTo == colonist;
                bool isMaster = master == colonist;

                if (isBonded)
                    sb.AppendLine($"Bond: {colonist.LabelShort} and {animal.LabelShort} share a deep emotional bond.");
                else if (isMaster)
                    sb.AppendLine($"Handler: {colonist.LabelShort} is {animal.LabelShort}'s trainer/master.");
                else
                    sb.AppendLine($"Familiarity: {colonist.LabelShort} and {animal.LabelShort} know each other as colony members.");

                string attitude = GetColonistAnimalAttitude(colonist, animal, animalIntelligent, isBonded);
                sb.AppendLine(attitude);

                int animalOpinion = animal.relations?.OpinionOf(colonist) ?? 0;
                string animalFeel = animalOpinion >= 50 ? "trusts and loves"
                                  : animalOpinion >= 10 ? "is comfortable with"
                                  : animalOpinion > -20 ? "is neutral toward"
                                  : "is wary or afraid of";
                sb.AppendLine($"{animal.LabelShort} {animalFeel} {colonist.LabelShort}.");

                return sb.ToString();
            }

            int opinionAB = a.relations?.OpinionOf(b) ?? 0;
            int opinionBA = b.relations?.OpinionOf(a) ?? 0;
            sb.AppendLine($"{a.LabelShort}→{b.LabelShort}: {opinionAB} ({GetOpinionDesc(opinionAB)})");
            sb.AppendLine($"{b.LabelShort}→{a.LabelShort}: {opinionBA} ({GetOpinionDesc(opinionBA)})");

            var relationsAB = a.relations?.DirectRelations?
                .Where(r => r.otherPawn == b && r.def != null)
                .Select(r => r.def)
                .ToList() ?? new List<PawnRelationDef>();

            var relationsBA = b.relations?.DirectRelations?
                .Where(r => r.otherPawn == a && r.def != null)
                .Select(r => r.def)
                .ToList() ?? new List<PawnRelationDef>();

            var allRelDefs = relationsAB.Concat(relationsBA).Distinct().ToList();

            if (allRelDefs.Any())
                sb.AppendLine($"Relation: {string.Join(", ", allRelDefs.Select(r => r.label))}");
            else
                sb.AppendLine("Relation: acquaintances");

            string familyTone = GetFamilyTone(a, b, allRelDefs, opinionAB, opinionBA);
            if (!string.IsNullOrEmpty(familyTone))
                sb.AppendLine(familyTone);

            string powerDynamic = GetPowerDynamic(a, b);
            if (!string.IsNullOrEmpty(powerDynamic))
                sb.AppendLine(powerDynamic);

            return sb.ToString();
        }

        // ── Family / romantic tone ────────────────────────────────────────────────

        private static string GetFamilyTone(
            Pawn a, Pawn b,
            List<PawnRelationDef> relDefs,
            int opinionAB, int opinionBA)
        {
            if (!relDefs.Any()) return null;

            bool Has(string defName) =>
                relDefs.Any(r => r.defName == defName);

            bool isSpouse  = Has("Spouse")  || Has("Wife")    || Has("Husband");
            bool isLover   = Has("Lover")   || Has("Fiance")  || Has("Fianee");
            bool isExSpouse= Has("ExSpouse")|| Has("ExWife")  || Has("ExHusband");
            bool isExLover = Has("ExLover");

            if (isSpouse)
            {
                int avgOpinion = (opinionAB + opinionBA) / 2;
                if (avgOpinion >= 50)
                    return "[TONE: Spouses — deeply loving and familiar. Pet names, inside references, " +
                           "physical warmth implied. Finish each other's thoughts.]";
                else if (avgOpinion >= 0)
                    return "[TONE: Spouses going through a rough patch — still committed but some tension. " +
                           "May be terse or passive-aggressive while still clearly caring.]";
                else
                    return "[TONE: Spouses in serious conflict — cold, guarded, or openly hostile. " +
                           "The love is strained to breaking point.]";
            }

            if (isLover)
                return "[TONE: Romantic partners — flirtatious warmth, affectionate teasing, " +
                       "private references. More physical expressiveness than friends.]";

            if (isExSpouse || isExLover)
            {
                int avgOpinion = (opinionAB + opinionBA) / 2;
                return avgOpinion >= 20
                    ? "[TONE: Ex-partners on good terms — warmth mixed with a trace of history, " +
                      "careful not to reopen old wounds but genuinely fond.]"
                    : "[TONE: Ex-partners on bad terms — loaded silences, old grievances close to the surface. " +
                      "Polite at best, bitter at worst.]";
            }

            bool aIsParent = a.relations?.DirectRelations?
                .Any(r => r.otherPawn == b &&
                    (r.def.defName == "Parent" || r.def.defName == "Father" || r.def.defName == "Mother")) == true;
            bool bIsParent = b.relations?.DirectRelations?
                .Any(r => r.otherPawn == a &&
                    (r.def.defName == "Parent" || r.def.defName == "Father" || r.def.defName == "Mother")) == true;

            bool isParentChild = Has("Parent") || Has("Father") || Has("Mother") ||
                                 Has("Child")  || Has("Son")    || Has("Daughter") ||
                                 aIsParent || bIsParent;

            if (isParentChild)
            {
                Pawn parent = aIsParent ? a : (bIsParent ? b : null);
                Pawn child  = parent == a ? b : a;
                int childAge = child?.ageTracker?.AgeBiologicalYears ?? 20;

                if (childAge <= 6)
                    return $"[TONE: Parent and very young child — {parent?.LabelShort ?? "the parent"} " +
                           $"uses simple words, gentle, nurturing. {child?.LabelShort ?? "the child"} " +
                           $"is innocent and trusting.]";
                if (childAge <= 13)
                    return $"[TONE: Parent and child — warm and protective. " +
                           $"{parent?.LabelShort ?? "the parent"} guides, {child?.LabelShort ?? "the child"} " +
                           $"seeks approval. Close bond implied.]";
                if (childAge <= 17)
                    return "[TONE: Parent and teenager — love is real but there's friction. " +
                           "Teen wants independence, parent struggles to let go. Affectionate undercurrent " +
                           "beneath potential disagreements.]";

                return "[TONE: Parent and adult child — relationship of equals with deep history. " +
                       "Respectful, warm, but not childlike. May reference shared memories or past hardship.]";
            }

            if (Has("Sibling") || Has("Brother") || Has("Sister") ||
                Has("HalfSibling") || Has("HalfBrother") || Has("HalfSister"))
            {
                int avgOpinion = (opinionAB + opinionBA) / 2;
                return avgOpinion >= 30
                    ? "[TONE: Siblings who get along — comfortable ribbing, shared references, " +
                      "protective loyalty. Can be brutally honest with each other in a loving way.]"
                    : "[TONE: Siblings with tension — old rivalries, unresolved competition. " +
                      "Still family so won't fully break ties, but friction is constant.]";
            }

            if (Has("ParentInLaw") || Has("ChildInLaw") || Has("Grandparent") ||
                Has("Grandchild")  || Has("Aunt")       || Has("Uncle") ||
                Has("Niece")       || Has("Nephew")     || Has("Cousin"))
                return "[TONE: Extended family — familiar but not as intimate as immediate family. " +
                       "Respectful warmth, shared loyalty to the larger family unit.]";

            if (Has("Friend") || Has("BestFriend"))
                return "[TONE: Close friends — easy, comfortable banter. No need to impress each other. " +
                       "Honest, relaxed, may tease affectionately.]";

            if (Has("Rival"))
                return "[TONE: Rivals — competitive undercurrent in every sentence. " +
                       "Respect mixed with the need to one-up each other.]";

            return null;
        }

        private static string GetColonistAnimalAttitude(
            Pawn colonist, Pawn animal, bool animalIntelligent, bool isBonded)
        {
            var traits = colonist.story?.traits?.allTraits;
            bool isPsycho    = traits?.Any(t => t.def.defName == "Psychopath") == true;
            bool isBloodlust = traits?.Any(t => t.def.defName == "Bloodlust") == true;
            bool isSadist    = traits?.Any(t => t.def.defName == "Sadist") == true;
            bool isKind      = traits?.Any(t => t.def.defName == "Kind") == true;
            bool isCareless  = traits?.Any(t => t.def.defName == "Careless") == true;

            if (isPsycho && !isBonded)
                return $"[{colonist.LabelShort} ATTITUDE: Psychopath — views {animal.LabelShort} as a tool or resource, " +
                       $"not a companion. No emotional investment. Speaks without warmth or care.]";
            if (isPsycho && isBonded)
                return $"[{colonist.LabelShort} ATTITUDE: Psychopath but bonded — tolerates {animal.LabelShort} " +
                       $"out of practical utility, may show faint possessiveness but no genuine warmth.]";
            if (isSadist)
                return $"[{colonist.LabelShort} ATTITUDE: Sadist — may speak to {animal.LabelShort} in a " +
                       $"controlling or subtly threatening way, enjoys having power over living things.]";
            if (isBloodlust && !isBonded)
                return $"[{colonist.LabelShort} ATTITUDE: Bloodlust — indifferent to {animal.LabelShort}'s " +
                       $"feelings, may be rough or dismissive unless it's a fighter they respect.]";

            if (animalIntelligent)
            {
                if (isKind)
                    return $"[{colonist.LabelShort} ATTITUDE: Kind — treats {animal.LabelShort} as a fully " +
                           $"sentient being deserving dignity and respect. Speaks to them as an equal.]";
                if (isBonded)
                    return $"[{colonist.LabelShort} ATTITUDE: Deeply bonded — speaks to {animal.LabelShort} " +
                           $"with warmth and respect, fully aware they can understand every word.]";
                return $"[{colonist.LabelShort} ATTITUDE: Aware {animal.LabelShort} is sentient and can speak. " +
                       $"Treats them with basic respect, like a person, not just an animal.]";
            }
            else
            {
                if (isKind || isBonded)
                    return $"[{colonist.LabelShort} ATTITUDE: Cares for {animal.LabelShort} — gentle tone, " +
                           $"speaks softly and encouragingly even knowing it's just an animal.]";
                if (isCareless)
                    return $"[{colonist.LabelShort} ATTITUDE: Careless — doesn't pay much attention to " +
                           $"{animal.LabelShort}, may barely acknowledge them.]";
                return $"[{colonist.LabelShort} ATTITUDE: Respectful but practical — treats {animal.LabelShort} " +
                       $"as a colony animal deserving basic care, not cruelty.]";
            }
        }

        // ── Shared memory ─────────────────────────────────────────────────────────

        private static string BuildSharedMemorySection(Pawn initiator, Pawn recipient)
        {
            var sb = new StringBuilder();
            bool hasContent = false;

            string initiatorMemory = GetPawnMemory(initiator);
            string recipientMemory = GetPawnMemory(recipient);

            if (!string.IsNullOrWhiteSpace(initiatorMemory) || !string.IsNullOrWhiteSpace(recipientMemory))
            {
                sb.AppendLine("=== WHAT EACH KNOWS (from past conversations with the player) ===");

                if (!string.IsNullOrWhiteSpace(initiatorMemory))
                {
                    sb.AppendLine($"{initiator.LabelShort} has discussed:");
                    sb.AppendLine(initiatorMemory);
                }

                if (!string.IsNullOrWhiteSpace(recipientMemory))
                {
                    sb.AppendLine($"{recipient.LabelShort} has discussed:");
                    sb.AppendLine(recipientMemory);
                }

                sb.AppendLine("They may reference these topics naturally if relevant.");
                hasContent = true;
            }

            string groupMemory = GetGroupMemory();
            if (!string.IsNullOrWhiteSpace(groupMemory))
            {
                sb.AppendLine("=== RECENT COLONY EVENTS (shared knowledge) ===");
                sb.AppendLine(groupMemory);
                hasContent = true;
            }

            return hasContent ? sb.ToString() : "";
        }

        // ── Colony events (PlayLog) ───────────────────────────────────────────────

        private static string BuildColonyEventsSection(Pawn initiator = null, Pawn recipient = null)
        {
            if (Find.PlayLog == null) return "";

            int now = Find.TickManager.TicksGame;
            int oneDayAgo = now - 60000;
            int justNow = now - 600;

            var lines = new List<string>();

            foreach (var entry in Find.PlayLog.AllEntries
                .Where(e => e.Tick >= oneDayAgo && e.Tick < justNow)
                .OrderByDescending(e => e.Tick)
                .Take(10))
            {
                try
                {
                    string text = entry.ToGameStringFromPOV(null);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = System.Text.RegularExpressions.Regex.Replace(text, @"<color=#[0-9A-Fa-f]{6,8}>", "");
                    text = text.Replace("</color>", "").Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                        lines.Add($"- {text}");

                    if (lines.Count >= 5) break;
                }
                catch { }
            }

            if (!lines.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== RECENT COLONY EVENTS (background context only) ===");
            foreach (var line in lines) sb.AppendLine(line);
            return sb.ToString();
        }

        // ── Output instruction ────────────────────────────────────────────────────

        private static string BuildOutputInstruction(int totalLines, string nameA, string nameB,
            InteractionDef interactionDef = null, string logText = null)
        {
            string lang = Prefs.LangFolderName?.ToLower() ?? "english";
            string langLine = lang != "english" ? $"Respond in {lang}.\n" : "";

            string reminder;
            if (!string.IsNullOrWhiteSpace(logText))
                reminder = $"FINAL RULE: The dialogue must be about \"{logText}\". " +
                           $"Do not mention 'that conversation' or 'that chat'. Speak about the actual subject.\n" +
                           $"Only mention health/pain if the pawn's pain is SEVERE (>50%). Otherwise ignore it.\n" +
                           $"NEVER reference technology, buildings, animals, or events not present in " +
                           $"VERIFIED COLONY HISTORY or the current game state. If unsure — omit it.\n";
            else if (interactionDef != null && !string.IsNullOrWhiteSpace(interactionDef.label))
                reminder = $"FINAL RULE: Stay on topic \"{interactionDef.label}\". " +
                           $"Only reference health/pain if SEVERE and truly unavoidable.\n" +
                           $"NEVER reference technology, buildings, animals, or events not present in " +
                           $"VERIFIED COLONY HISTORY or the current game state. If unsure — omit it.\n";
            else
                reminder = $"FINAL RULE: NEVER reference technology, buildings, animals, or events not present in " +
                           $"VERIFIED COLONY HISTORY or the current game state. If unsure — omit it.\n";

            return
                $"{langLine}" +
                reminder +
                $"Respond ONLY with a valid JSON array. No markdown, no preamble, no explanation.\n" +
                $"Exactly {totalLines} objects, alternating speaker starting with {nameA}.\n" +
                $"[\n" +
                $"  {{\"speaker\":\"{nameA}\",\"text\":\"...\"}},\n" +
                $"  {{\"speaker\":\"{nameB}\",\"text\":\"...\"}},\n" +
                $"  ...\n" +
                $"]\n" +
                $"Each text: 1–2 natural sentences. In-character. No stage directions.";
        }

        // ── Memory helpers ────────────────────────────────────────────────────────

        private static string GetPawnMemory(Pawn pawn)
        {
            try
            {
                var manager = ColonistMemoryManager.GetOrCreate();
                if (manager == null) return null;

                var tracker = manager.GetTrackerFor(pawn);
                if (tracker == null) return null;

                var recent = tracker.GetLastMemories(5);
                if (recent == null || recent.Count == 0) return null;

                string combined = string.Join(" | ", recent);
                const int maxChars = 400;
                if (combined.Length > maxChars)
                    combined = combined.Substring(combined.Length - maxChars);
                return combined.Trim();
            }
            catch { return null; }
        }

        private static string GetGroupMemory()
        {
            try
            {
                var manager = ColonistMemoryManager.GetOrCreate();
                if (manager == null) return null;

                var groupTracker = manager.GetGroupMemoryTracker();
                if (groupTracker == null) return null;

                var recent = groupTracker.GetAllRecentMemories(3);
                if (recent == null || recent.Count == 0) return null;

                var trimmed = recent.Count > 3 ? recent.GetRange(recent.Count - 3, 3) : recent;
                return string.Join("\n", trimmed.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            }
            catch { return null; }
        }

        // ── Descriptor helpers ────────────────────────────────────────────────────

        private static string GetTimeDesc(int hour)
        {
            if (hour < 5)  return "early morning";
            if (hour < 9)  return "morning";
            if (hour < 14) return "midday";
            if (hour < 18) return "afternoon";
            if (hour < 22) return "evening";
            return "night";
        }

        private static string GetLocationDesc(Pawn pawn)
        {
            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return "outdoors";
            if (room.ContainedBeds.Any())
                return room.ContainedBeds.Count() == 1 ? "a bedroom" : "the barracks";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b.def.defName.Contains("Stove")))
                return "the kitchen";
            if (room.ContainedAndAdjacentThings.OfType<Building>().Any(b => b is Building_WorkTable))
                return "a workshop";
            return "indoors";
        }

        private static string GetInteractionNature(InteractionDef def)
        {
            string n = def.defName?.ToLower() ?? "";
            string l = def.label?.ToLower()   ?? "";

            if (n.Contains("chitchat"))     return "Casual small talk";
            if (n.Contains("deeptalk"))     return "Deep, meaningful conversation";
            if (n.Contains("romance"))      return "Romantic or flirtatious";
            if (n.Contains("insult"))       return "Hostile or insulting exchange";
            if (n.Contains("slight"))       return "Passive-aggressive minor offense";
            if (n.Contains("rapport"))      return "Building friendship and trust";
            if (n.Contains("comfort"))      return "Offering comfort or emotional support";
            if (n.Contains("joke"))         return "Joking around, light-hearted";
            if (n.Contains("complain"))     return "Venting or complaining";
            if (n.Contains("convert"))      return "Attempting to convert ideologically";
            if (n.Contains("recruit"))      return "Attempting to recruit or persuade";
            if (n.Contains("rebelstir"))    return "Stirring rebellion or discontent";
            if (n.Contains("prisonbreak"))  return "Coordinating escape";
            if (n.Contains("kind"))         return "Kind and supportive gesture";

            if (n.Contains("teach") || l.Contains("teach") ||
                n.Contains("train") || l.Contains("train") ||
                n.Contains("instruct") || l.Contains("lesson") ||
                l.Contains("technique") || l.Contains("skill"))
                return $"Teaching or sharing knowledge — one colonist instructing the other on \"{def.label}\"";

            if (n.Contains("collab") || l.Contains("collab") ||
                l.Contains("work together") || l.Contains("help with") ||
                n.Contains("assist") || l.Contains("assist"))
                return $"Collaborative work discussion about \"{def.label}\"";

            if (n.Contains("debate") || l.Contains("debate") ||
                n.Contains("argue") || l.Contains("argue") ||
                n.Contains("disagree") || l.Contains("disagree"))
                return $"Heated debate or disagreement: \"{def.label}\"";

            if (n.Contains("gossip") || l.Contains("gossip") ||
                n.Contains("news") || l.Contains("news") ||
                n.Contains("rumor") || l.Contains("rumor"))
                return $"Sharing gossip or colony news: \"{def.label}\"";

            if (n.Contains("request") || l.Contains("request") ||
                n.Contains("favor") || l.Contains("favor") ||
                n.Contains("ask") || l.Contains("ask"))
                return $"One colonist requesting something from the other: \"{def.label}\"";

            if (n.Contains("warn") || l.Contains("warn") ||
                n.Contains("threat") || l.Contains("threat"))
                return $"Warning or threatening: \"{def.label}\"";

            if (n.Contains("greet") || l.Contains("greet") ||
                n.Contains("farewell") || l.Contains("goodbye"))
                return $"Greeting or farewell: \"{def.label}\"";

            return !string.IsNullOrWhiteSpace(def.label)
                ? $"Modded interaction: \"{def.label}\" — use this as the conversation topic"
                : "General interaction";
        }

        private static string GetMoodDesc(float mood)
        {
            if (mood > 0.8f) return "very happy";
            if (mood > 0.6f) return "content";
            if (mood > 0.4f) return "neutral";
            if (mood > 0.2f) return "stressed";
            return "on the edge of a breakdown";
        }

        private static string GetOpinionDesc(int opinion)
        {
            if (opinion >= 75)  return "deeply bonded";
            if (opinion >= 50)  return "very positive";
            if (opinion >= 20)  return "friendly";
            if (opinion > -20)  return "neutral";
            if (opinion > -50)  return "tense";
            return "hostile";
        }

        private static string GetPowerDynamic(Pawn a, Pawn b)
        {
            if (a.IsFreeColonist && b.IsPrisoner)
                return $"POWER DYNAMIC: {a.LabelShort} holds authority over {b.LabelShort}, who is a prisoner.\n" +
                       $"{a.LabelShort} does NOT need to be kind or polite — they may be curt, dismissive, " +
                       $"threatening, or transactional depending on their traits and mood.\n" +
                       $"{b.LabelShort} is in a position of weakness — they may be defiant, fearful, resigned, " +
                       $"or calculating, but they cannot freely challenge or disrespect {a.LabelShort}.\n" +
                       $"Avoid mutual warmth unless traits strongly support it.";

            if (a.IsPrisoner && b.IsFreeColonist)
                return $"POWER DYNAMIC: {a.LabelShort} is a prisoner of the colony — {b.LabelShort} controls their fate.\n" +
                       $"{a.LabelShort} speaks from a position of captivity: wary, guarded, or resentful.\n" +
                       $"{b.LabelShort} holds authority and does not need to be courteous.\n" +
                       $"Avoid mutual warmth unless traits strongly support it.";

            if (a.IsFreeColonist && b.IsSlaveOfColony)
                return $"POWER DYNAMIC: {a.LabelShort} is a free colonist; {b.LabelShort} is their slave.\n" +
                       $"{a.LabelShort} may speak with condescension, indifference, or casual authority — " +
                       $"they are not obligated to treat {b.LabelShort} as an equal.\n" +
                       $"{b.LabelShort} must respond with deference or suppressed frustration — " +
                       $"open defiance is rare and risky for them.\n" +
                       $"Avoid mutual warmth or equal-footing dialogue unless traits strongly support it.";

            if (a.IsSlaveOfColony && b.IsFreeColonist)
                return $"POWER DYNAMIC: {a.LabelShort} is enslaved by the colony — {b.LabelShort} is their master.\n" +
                       $"{a.LabelShort} speaks carefully, with deference or hidden resentment.\n" +
                       $"{b.LabelShort} may speak with indifference, authority, or casual condescension.\n" +
                       $"Avoid mutual warmth or equal-footing dialogue unless traits strongly support it.";

            if (a.IsSlaveOfColony && b.IsSlaveOfColony)
                return $"POWER DYNAMIC: Both {a.LabelShort} and {b.LabelShort} are enslaved by the same colony.\n" +
                       $"They may speak with solidarity, dark humor, mutual exhaustion, or bitter resignation.\n" +
                       $"Neither holds authority over the other.";

            return null;
        }

        // ── Trait-based speech style ──────────────────────────────────────────────

        private static string GetTraitSpeechStyle(Pawn pawn)
        {
            var traits = pawn.story?.traits?.allTraits;
            if (traits == null || !traits.Any()) return null;

            var notes = new List<string>();

            foreach (var trait in traits)
            {
                if (trait?.def == null) continue;
                switch (trait.def.defName)
                {
                    case "Psychopath":
                        notes.Add("speaks without empathy — calm, detached, transactional. " +
                                  "Never pretends to care about others' feelings.");
                        break;
                    case "Sadist":
                        notes.Add("subtly enjoys others' discomfort — may make cutting remarks " +
                                  "framed as neutral observations. Takes satisfaction from control.");
                        break;
                    case "Bloodlust":
                        notes.Add("energized by violence and danger — speaks with excitement about " +
                                  "fights and death, indifferent to suffering.");
                        break;
                    case "Cannibal":
                        notes.Add("views eating people as normal — may casually reference it " +
                                  "without shame or hiding it.");
                        break;
                    case "Abrasive":
                        notes.Add("blunt to the point of rudeness — says exactly what they think, " +
                                  "no filter, doesn't soften criticism.");
                        break;
                    case "Bully":
                        notes.Add("enjoys pushing others around — condescending, may mock or " +
                                  "belittle weaker colonists.");
                        break;
                    case "Kind":
                        notes.Add("warm and considerate — chooses words carefully to not hurt others, " +
                                  "genuinely interested in how people feel.");
                        break;
                    case "Empathic":
                        notes.Add("highly attuned to others' emotions — references feelings directly, " +
                                  "very supportive tone.");
                        break;
                    case "Charming":
                    case "Beautiful":
                        notes.Add("naturally charismatic — easy confidence in speech, people enjoy listening.");
                        break;
                    case "Neurotic":
                        notes.Add("worries out loud, overthinks — tangents about potential problems, " +
                                  "double-checks things.");
                        break;
                    case "TooSmart":
                        notes.Add("intellectually arrogant — subtly condescending, assumes others " +
                                  "won't understand complex ideas.");
                        break;
                    case "Nihilistic":
                        notes.Add("sees little point in most things — dry, fatalistic remarks, " +
                                  "not hostile but deeply cynical.");
                        break;
                    case "Loner":
                        notes.Add("uncomfortable socializing — short answers, clearly wants " +
                                  "the conversation to end.");
                        break;
                    case "Misandrist":
                        notes.Add("hostile or dismissive toward male colonists specifically.");
                        break;
                    case "Misogynist":
                        notes.Add("dismissive or condescending toward female colonists specifically.");
                        break;
                    case "Ascetic":
                        notes.Add("values simplicity — dismissive of luxury, materialism, or excess.");
                        break;
                    case "Greedy":
                        notes.Add("always thinking about wealth — references silver, goods, profit.");
                        break;
                    case "Jealous":
                        notes.Add("resentful of others' advantages — bitter undercurrent when " +
                                  "others have things they don't.");
                        break;
                }
            }

            if (!notes.Any()) return null;
            return $"[SPEECH STYLE: {string.Join(" / ", notes)}]";
        }
    }
}