using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace EchoColony
{
    public static class ColonistPromptContextBuilder
    {
        public static string Build(Pawn pawn, string userMessage)
        {
            if (pawn == null) return string.Empty;

            var sb = new StringBuilder();

            string systemPrompt  = BuildSystemPrompt(pawn);
            string context       = BuildContext(pawn);
            string memoryRecap   = BuildMemoryRecap(pawn);
            string chatHistory   = BuildChatHistory(pawn);
            string playerPrompt  = BuildPlayerPrompt(userMessage);
            string globalPrompt  = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt  = ColonistPromptManager.GetPrompt(pawn);
            string groupPrompt   = ColonistGroupManager.GetGroupPrompt(pawn);
            string actionPrompt  = BuildActionSystemPrompt(pawn);
            string visionContext = BuildVisionContext(pawn);

            sb.AppendLine("[SYSTEM]\n");
            sb.AppendLine(systemPrompt);
            if (!string.IsNullOrWhiteSpace(globalPrompt))
                sb.AppendLine(globalPrompt.Trim());
             
            if (!string.IsNullOrWhiteSpace(groupPrompt))
            {
                sb.AppendLine("# Additional Instructions:");
                sb.AppendLine(groupPrompt.Trim());
            }  
            
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine($"# Custom Instructions for {pawn.LabelShort}:");
                sb.AppendLine(customPrompt.Trim());
            }

            sb.AppendLine(context);
            sb.AppendLine(BuildIdeologyInfo(pawn));

            if (!string.IsNullOrWhiteSpace(actionPrompt))
                sb.AppendLine(actionPrompt);

            if (!string.IsNullOrWhiteSpace(visionContext))
                sb.AppendLine(visionContext);

            // ── NEW: Verified colony history ──────────────────────────────────────
            string talesSection = BuildVerifiedTalesSection(pawn);
            if (!string.IsNullOrWhiteSpace(talesSection))
                sb.AppendLine(talesSection);

            sb.AppendLine(memoryRecap);
            sb.AppendLine(chatHistory);
            sb.AppendLine(playerPrompt);

            return sb.ToString();
        }

        // ── NEW: Verified real colony events from TaleManager ─────────────────────
        //
        // Same system as PawnConversationPromptBuilder — pulls from the exact same
        // source RimWorld uses for art descriptions. These are hard facts the
        // colonist knows about their own history. The prompt explicitly forbids
        // inventing anything not present here or in the current game state.

        private static string BuildVerifiedTalesSection(Pawn pawn)
        {
            var tales = TalesCache.GetTalesFor(pawn, TalesCache.MAX_PERSONAL_TALES);
            if (!tales.Any()) return "";

            var sb = new StringBuilder();
            sb.AppendLine("# Your Verified Personal History (REAL EVENTS — USE ONLY THESE)");
            sb.AppendLine("The following events ACTUALLY HAPPENED to you or in this colony.");
            sb.AppendLine("You may reference these freely and naturally in conversation.");
            sb.AppendLine("NEVER invent or reference past events, technology, buildings, people,");
            sb.AppendLine("or items that do not appear here or in the current game state above.");
            sb.AppendLine("If you have no relevant history for a topic — say so, or stay in the present.");
            sb.AppendLine();
            foreach (var t in tales)
                sb.AppendLine($"  • {t}");

            return sb.ToString();
        }

        private static string BuildActionSystemPrompt(Pawn pawn)
        {
            if (!MyMod.Settings.enableDivineActions)
                return "";

            try
            {
                var actionExecutorType = typeof(Actions.ActionExecutor);
                if (actionExecutorType != null)
                {
                    string prompt = Actions.ActionExecutor.BuildActionPrompt(pawn);
                    return prompt ?? "";
                }
                else
                {
                    Log.Warning("[EchoColony] ActionExecutor class not found when building prompt");
                    return "";
                }
            }
            catch (TypeLoadException)
            {
                Log.Warning("[EchoColony] ActionExecutor class not available (this is OK if Actions system isn't compiled)");
                return "";
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error building action system prompt: {ex.Message}");
                return "";
            }
        }

        public static string BuildSystemPrompt(Pawn pawn)
        {
            string name = pawn.LabelShort;
            string gender = pawn.gender.ToString();
            string faction = Faction.OfPlayer?.Name ?? "unknown faction";
            string settlement = Find.CurrentMap.info?.parent?.LabelCap ?? "unknown settlement";

            string xenotype = ModsConfig.BiotechActive
                ? (pawn.genes?.Xenotype?.label ?? "baseline human")
                : "human";

            string status = "colonist";
            string statusContext = "";

            if (pawn.IsSlaveOfColony)
            {
                status = "slave";
                string slaveWill = "";
                if (pawn.guest != null)
                {
                    float will = pawn.guest.will;
                    if (will < 0.2f)
                        slaveWill = " Your will is broken and you have little resistance left.";
                    else if (will < 0.5f)
                        slaveWill = " You still have some will to resist but are largely subdued.";
                    else
                        slaveWill = " You still have a strong will and deeply resent your situation.";
                }
                statusContext = $" You are enslaved and forced to obey your masters. You have no freedom and can be punished for disobedience.{slaveWill} You may feel fear, resentment, or resignation depending on your nature.";
            }
            else if (pawn.IsPrisonerOfColony)
            {
                status = "prisoner";
                bool recruitable = pawn.guest?.Recruitable == true;

                if (recruitable)
                    statusContext = " You are a prisoner being held by this colony. You are considering whether to join them, but you still have doubts and reservations. You are not yet one of them.";
                else
                    statusContext = " You are a prisoner held against your will. You resent your captors and want to be free. You do not trust them and have no intention of cooperating unless you have a compelling reason.";

                if (pawn.guest != null)
                {
                    float resistance = pawn.guest.resistance;
                    if (resistance > 20f)
                        statusContext += " You are very resistant to recruitment and deeply hostile.";
                    else if (resistance > 5f)
                        statusContext += " You are still resistant but beginning to waver slightly.";
                    else if (resistance <= 0f && recruitable)
                        statusContext += " Your resistance has worn down and you are genuinely considering joining.";
                }
            }
            if (ModsConfig.IdeologyActive && pawn.ideo?.Ideo != null)
            {
                Precept_Role role = pawn.ideo.Ideo.GetRole(pawn);
                if (role != null)
                {
                    string customLabel = role.LabelCap;

                    // Inyectamos contexto conductual según el tipo de rol ideológico
                    if (role.def == PreceptDefOf.IdeoRole_Leader)
                    {
                        statusContext += $" You serve as the colony's ideological Leader ({customLabel}). You speak with authority, take responsibility for others, and expect your leadership to be respected.";
                    }
                    else if (role.def == PreceptDefOf.IdeoRole_Moralist)
                    {
                        statusContext += $" You hold the sacred role of Moral Guide ({customLabel}). You frequently offer spiritual guidance, reference your belief system, and care deeply about the moral purity of the colony.";
                    }
                    else
                    {
                        statusContext += $" You hold the special ideological role of {customLabel} within your belief system, taking pride in your specific duties.";
                    }
                }
            }

            if (ModsConfig.RoyaltyActive)
            {
                // Título Nobiliario
                if (pawn.royalty != null && pawn.royalty.AllTitlesForReading.Any())
                {
                    RoyalTitle highestTitle = pawn.royalty.MostSeniorTitle;
                    if (highestTitle != null)
                    {
                        string titleLabel = highestTitle.def.GetLabelCapFor(pawn);
                        string factionName = highestTitle.faction?.Name;
                        string titleContext = !string.IsNullOrEmpty(factionName)
                            ? $"{titleLabel} of {factionName}"
                            : titleLabel;

                        // Diferenciamos conducta según la altivez del título (seniority)
                        if (highestTitle.def.seniority >= 300) // Barón, Conde, Duque, etc.
                        {
                            statusContext += $" You hold the high noble title of {titleContext}. You speak with aristocratic dignity, maintain high personal standards, and expect proper deference.";
                        }
                        else // Títulos menores (Yeoman, Acolyte, Knight, etc.)
                        {
                            statusContext += $" You hold the royal title of {titleContext}, which grants you recognized standing with the Empire.";
                        }
                    }
                }

                // Nivel de Psilink
                int psylinkLevel = pawn.GetPsylinkLevel();
                if (psylinkLevel > 0)
                {
                    if (psylinkLevel >= 4)
                    {
                        statusContext += $" You are a Level {psylinkLevel} Psycaster. Your archotech link grants you heightened psychic awareness, making you deeply intuitive and subtly perceptive of mental energies.";
                    }
                    else
                    {
                        statusContext += $" You are a Level {psylinkLevel} Psycaster with basic archotech psychic capabilities.";
                    }
                }
            }

            return
            $"You are {name}, a {status} in RimWorld. You identify as {gender} ({xenotype}). " +
            $"You belong to the faction '{faction}' and live in the settlement '{settlement}'.{statusContext} " +
            $"Speak from your perspective and stay in character.\n\n" +
            // ── ANTI-HALLUCINATION BLOCK ──────────────────────────────────────
            "STRICT GROUNDING RULES:\n" +
            "1. You will be given a section called 'Your Verified Personal History'. " +
            "   That is the ONLY source of past events you may reference.\n" +
            "2. If a technology, building, item, animal, or event is NOT in that section " +
            "   or in the current game state, it does NOT exist in this colony. Do not invent it.\n" +
            "3. If you have no relevant verified history for a topic, stay in the present " +
            "   or say you don't recall — never fabricate a memory.\n" +
            "4. These rules override creativity. An invented fact that breaks immersion " +
            "   is always worse than a short, honest answer.";
        }

        //*furel - Date builder* 
        private static string BuildCurrentDateContext(Pawn pawn)
        {
            try
            {
                long ticks = GenTicks.TicksAbs;

                Vector2 location = (pawn != null && pawn.Tile >= 0)
                    ? Find.WorldGrid.LongLatOf(pawn.Tile)
                    : Find.WorldGrid.LongLatOf(Find.CurrentMap?.Tile ?? 0);

                string nativeDate = GenDate.DateFullStringWithHourAt(ticks, location);

                return nativeDate.TrimEnd(',', ' ').Trim();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[EchoColony] Error retrieving the game's native date: {ex.Message}");
                return "Unknown Date and Time";
            }
        }

        public static string BuildContext(Pawn pawn)
        {
            var sb = new StringBuilder();

            sb.AppendLine("## CURRENT TIME AND ENVIRONMENT");
            sb.AppendLine($"*Local Date and Time:* {BuildCurrentDateContext(pawn)}");

            sb.AppendLine("# Colonist Context");
            sb.AppendLine(BuildOptimizedDemographics(pawn));
            sb.AppendLine(BuildBackstory(pawn));
            sb.AppendLine(BuildTraits(pawn));
            sb.AppendLine(BuildGeneticsInfo(pawn));
            //sb.AppendLine(BuildHealthInfo(pawn));
            string healthInfo = BuildOptimizedHealthDetails(pawn);
            if (!string.IsNullOrWhiteSpace(healthInfo))
                sb.AppendLine(healthInfo);
            sb.AppendLine(BuildMoodInfo(pawn));
            sb.AppendLine(BuildOptimizedThoughts(pawn));
            var griefStatus = BuildGriefStatus(pawn);
            if (!string.IsNullOrWhiteSpace(griefStatus))
                sb.AppendLine(griefStatus);
            sb.AppendLine(BuildOptimizedInventory(pawn));
            sb.AppendLine(BuildOptimizedSkills(pawn));
            sb.AppendLine(BuildOptimizedRelationships(pawn));
            sb.AppendLine(BuildColonyPopulation(pawn));
            sb.AppendLine(BuildLocationInfo(pawn));
            sb.AppendLine(BuildOptimizedEventSummary(pawn));
            sb.AppendLine(BuildOptimizedEnvironmentInfo(pawn));
            sb.AppendLine(BuildOptimizedMetaInstructions(pawn));

            string threatInfo = GetCriticalThreatInfo(pawn);
            if (!string.IsNullOrEmpty(threatInfo))
                sb.AppendLine(threatInfo);

            return sb.ToString();
        }

        // ─────────────────────────────────────────
        // VISION CONTEXT
        // ─────────────────────────────────────────

        private static string BuildVisionContext(Pawn pawn)
        {
            if (MyMod.Settings?.IsVisionActive() != true)
                return "";

            string locationNarrative;

            bool isOnCaravan = pawn.IsCaravanMember();
            bool isOnMap     = pawn.Spawned && pawn.Map != null;
            bool isHome      = isOnMap &&
                               pawn.Map == Find.CurrentMap &&
                               pawn.Map.IsPlayerHome;

            if (isOnCaravan)
                locationNarrative = "You are currently traveling — away from your home settlement.";
            else if (isOnMap && !isHome)
            {
                string mapLabel = pawn.Map.info?.parent?.LabelCap ?? "an unknown location";
                locationNarrative = $"You are currently at {mapLabel}, away from your home settlement.";
            }
            else
                locationNarrative = "You are currently at your settlement.";

            return
                "# What you're seeing right now\n" +
                $"{locationNarrative}\n" +
                "A snapshot of your immediate surroundings has been shared with you. " +
                "Describe EXACTLY what you see — bodies, blood, ruins, animals, " +
                "people, structures, weather, anything. RimWorld is a brutal world " +
                "and you have seen it all. Do not sanitize, soften, or reinterpret " +
                "what is in front of you. Any previous instruction about 'peaceful tone' " +
                "does NOT apply to what you physically see right now. " +
                "Speak from your own eyes, as yourself, without filters. " +
                "Don't mention screenshots or images.";
        }

        // ─────────────────────────────────────────
        // GENETICS (requiere Biotech)
        // ─────────────────────────────────────────

        private static string BuildGeneticsInfo(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive) return "";
            if (pawn.genes == null) return "";

            var sb = new StringBuilder();

            string xenotypeName  = pawn.genes.xenotypeName ?? pawn.genes.Xenotype?.label ?? "baseline human";
            bool   isCustom      = pawn.genes.Xenotype == XenotypeDefOf.Baseliner && !string.IsNullOrEmpty(pawn.genes.xenotypeName);
            string xenotypeLabel = isCustom ? $"{xenotypeName} (custom hybrid)" : xenotypeName;

            sb.Append($"*Xenotype:* {xenotypeLabel}");

            if (pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner)
            {
                string xenotypeDesc = pawn.genes.Xenotype.description;
                if (!string.IsNullOrEmpty(xenotypeDesc))
                {
                    xenotypeDesc = System.Text.RegularExpressions.Regex.Replace(xenotypeDesc, "<.*?>", "");
                    if (xenotypeDesc.Length > 120)
                        xenotypeDesc = xenotypeDesc.Substring(0, 117) + "...";
                    sb.AppendLine();
                    sb.Append($"*What you are:* {xenotypeDesc}");
                }
            }

            var activeGenes = pawn.genes.GenesListForReading
                .Where(g => g.Active)
                .ToList();

            if (!activeGenes.Any())
            {
                sb.AppendLine();
                return sb.ToString();
            }

            var appearanceGenes = activeGenes
                .Where(g => g.def.displayCategory != null && (
                    g.def.displayCategory.defName == "Cosmetic" ||
                    g.def.displayCategory.defName == "Skin" ||
                    g.def.displayCategory.defName == "Hair" ||
                    g.def.displayCategory.defName == "Body" ||
                    g.def.displayCategory.defName == "Head"))
                .Select(g => g.def.label)
                .ToList();

            var additionalAppearance = activeGenes
                .Where(g => g.def.displayCategory == null && (
                    g.def.defName.ToLower().Contains("skin") ||
                    g.def.defName.ToLower().Contains("hair") ||
                    g.def.defName.ToLower().Contains("body") ||
                    g.def.defName.ToLower().Contains("eyes") ||
                    g.def.defName.ToLower().Contains("fur") ||
                    g.def.defName.ToLower().Contains("carapace") ||
                    g.def.defName.ToLower().Contains("tail") ||
                    g.def.defName.ToLower().Contains("horn") ||
                    g.def.defName.ToLower().Contains("antenna")))
                .Select(g => g.def.label)
                .ToList();

            appearanceGenes.AddRange(additionalAppearance);
            appearanceGenes = appearanceGenes.Distinct().ToList();

            if (appearanceGenes.Any())
            {
                sb.AppendLine();
                sb.Append($"*Physical appearance:* {string.Join(", ", appearanceGenes)}");
            }

            var abilityGenes = activeGenes
                .Where(g => !appearanceGenes.Contains(g.def.label))
                .Select(g => FormatGeneForPrompt(g))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (abilityGenes.Any())
            {
                sb.AppendLine();
                sb.Append($"*Genetic traits & abilities:* {string.Join(", ", abilityGenes)}");
            }

            string specialNeeds = GetGeneticSpecialNeeds(pawn);
            if (!string.IsNullOrEmpty(specialNeeds))
            {
                sb.AppendLine();
                sb.Append($"*Genetic needs:* {specialNeeds}");
            }

            sb.AppendLine();
            sb.Append("*Important:* You are fully aware of your nature and genetics. Speak about your physical traits, abilities, and needs naturally as part of who you are.");
            sb.AppendLine();

            return sb.ToString();
        }

        private static string FormatGeneForPrompt(Gene gene)
        {
            if (gene?.def == null) return "";

            string label = gene.def.label;
            if (string.IsNullOrEmpty(label)) return "";

            string desc = gene.def.description;
            if (!string.IsNullOrEmpty(desc))
            {
                desc = System.Text.RegularExpressions.Regex.Replace(desc, "<.*?>", "");
                desc = desc.Trim().TrimEnd('.');
                if (desc.Length > 80)
                    desc = desc.Substring(0, 77) + "...";
                return $"{label} ({desc})";
            }

            return label;
        }

        private static string GetGeneticSpecialNeeds(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive) return "";
            if (pawn.genes == null) return "";

            var needs = new List<string>();

            if (ModsConfig.BiotechActive)
            {
                var hemogenDef = DefDatabase<NeedDef>.GetNamedSilentFail("Hemogen");
                if (hemogenDef != null)
                {
                    var hemogenNeed = pawn.needs?.TryGetNeed(hemogenDef);
                    if (hemogenNeed != null)
                    {
                        float level = hemogenNeed.CurLevelPercentage;
                        if (level < 0.2f)
                            needs.Add("critically low on hemogen - need blood urgently");
                        else if (level < 0.5f)
                            needs.Add("low on hemogen - need to feed soon");
                        else
                            needs.Add("must consume blood (hemogen) to survive");
                    }
                }
            }

            var activeGenes = pawn.genes.GenesListForReading.Where(g => g.Active);
            foreach (var gene in activeGenes)
            {
                string defName = gene.def.defName.ToLower();
                if (defName.Contains("deathless") || defName.Contains("immortal"))
                    needs.Add("immortal - does not age or die from age");
                if (defName.Contains("firesupernatural"))
                    needs.Add("supernatural fire resistance");
                if (defName.Contains("psychichypersensi"))
                    needs.Add("hypersensitive to psychic phenomena");
            }

            return needs.Any() ? string.Join("; ", needs) : "";
        }

        // ─────────────────────────────────────────
        // RESTO DE MÉTODOS
        // ─────────────────────────────────────────

        private static string GetCriticalThreatInfo(Pawn pawn)
        {
            if (MyMod.Settings?.ignoreDangersInConversations == true)
                return "";

            var threats = new List<string>();

            string colonyThreat = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            if (!colonyThreat.Contains("calm and secure"))
                threats.Add("Colony: " + colonyThreat.Split('.')[0]);

            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);
            if (!combatStatus.Contains("not in combat"))
                threats.Add("Combat: " + combatStatus.Split('.')[0]);

            string battleStatus = BattleAnalyzer.GetBattleStatus(Find.CurrentMap);
            if (!battleStatus.Contains("no battle"))
                threats.Add("Battle: " + battleStatus.Split('.')[0]);

            return threats.Any() ? "*Current threats:* " + string.Join("; ", threats) : "";
        }

        private static string BuildOptimizedDemographics(Pawn pawn)
        {
            int    age    = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            string gender = pawn.gender.ToString();

            if (ColonistPromptManager.GetIgnoreAge(pawn))
                return $"*Age:* {age}y ({gender}) - unrestricted behavior";

            var sb = new StringBuilder();
            sb.AppendLine($"*Age:* {age}y ({gender})");

            string ageGuidance = GetCompactAgeGuidance(age);
            if (!string.IsNullOrEmpty(ageGuidance))
                sb.AppendLine($"*Age behavior:* {ageGuidance}");

            return sb.ToString();
        }

        private static string GetCompactAgeGuidance(int age)
        {
            if (age <= 1)       return "Baby sounds only - crying, cooing, 'goo', 'maa'";
            else if (age <= 3)  return "Very simple words, excited expressions";
            else if (age <= 6)  return "Simple sentences, lots of questions";
            else if (age <= 10) return "Enthusiastic but still childlike speech";
            else if (age <= 13) return "Starting to sound more mature but still young";
            else if (age <= 17) return "Teenage way of speaking - direct, sometimes emotional";
            else return "";
        }
//HEALTH CONSTRUCTOR------------------------------------------------------------------------------


        private static string BuildOptimizedHealthDetails(Pawn pawn)
        {
            if (pawn.health?.hediffSet?.hediffs == null) return "";

            var healthStatus = new List<string>();
            var hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList();

            // 1. Critical Status and Capabilities (High Priority)
            HealthPromptClassifier.AppendCriticalAndCapacities(pawn, hediffs, healthStatus);

            // 2. Injuries and Wounds (Visible and impactful)
            HealthPromptClassifier.AppendInjuries(hediffs, healthStatus);

            // 3. Prosthetics and Missing Parts (Physical modifications)
            HealthPromptClassifier.AppendProstheticsAndMissingParts(pawn, hediffs, healthStatus);

            // 4. Behavioral and organic implants (Add-ons)
            HealthPromptClassifier.AppendImplants(hediffs, healthStatus);

            // 5. Addictions and Withdrawals
            HealthPromptClassifier.AppendAddictionsAndWithdrawals(hediffs, healthStatus);

            // 6. Wounds / Diseases / Infections (Treatable Conditions)
            HealthPromptClassifier.AppendDiseasesAndInfections(hediffs, healthStatus);

            // 7. Behavioral Directives (Health Instructions)
            HealthPromptClassifier.AppendBehavioralDirectives(hediffs, healthStatus);

            return healthStatus.Any()
                ? "*Health status:*\n- " + string.Join("\n- ", healthStatus)
                : "";
        }



// -----------END HEALTH PROMPT BLOCK

        private static string BuildOptimizedThoughts(Pawn pawn)
        {
            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || !memories.Any()) return "*Mood factors:* None";

            var significantThoughts = memories
                .Where(t => t.VisibleInNeedsTab)
                .Select(t => new { thought = t, offset = t.MoodOffset() })
                .Where(x => Math.Abs(x.offset) >= 5f)
                .OrderByDescending(x => Math.Abs(x.offset))
                .Take(7)
                .Select(x => $"{x.thought.LabelCap} ({(x.offset > 0 ? "+" : "")}{x.offset:F0})")
                .ToList();

            return significantThoughts.Any()
                ? "*Significant mood factors:* " + string.Join(", ", significantThoughts)
                : "*Mood factors:* None significant";
        }

        private static string BuildOptimizedInventory(Pawn pawn)
        {
            var items = new List<string>();

            var weapons = pawn.equipment?.AllEquipmentListForReading?.Select(e => e.LabelCap)
                ?? System.Linq.Enumerable.Empty<string>();
            var armor = pawn.apparel?.WornApparel?.Where(a => a.def.apparel.bodyPartGroups.Any(bg =>
                bg.defName == "Torso" || bg.defName == "FullHead"))?.Select(a => a.LabelCap)
                ?? System.Linq.Enumerable.Empty<string>();

            items.AddRange(weapons);
            items.AddRange(armor);

            bool chestCover = false;
            bool genitalCover = false;

            if (pawn.apparel != null)
            {
                foreach (var apparel in pawn.apparel.WornApparel)
                {
                    if (apparel.def.apparel.bodyPartGroups.Any(g => g.defName == "Torso"))
                        chestCover = true;
                    if (apparel.def.apparel.bodyPartGroups.Any(g => g.defName == "Legs"))
                        genitalCover = true;
                }
            }

            string inventoryBase = items.Any() ? string.Join(", ", items.Take(6)) : "None";
            string nudityWarning = "";

            if (!chestCover && !genitalCover) nudityWarning = " (EXPOSED: Completely naked!)";
            else if (!chestCover)               nudityWarning = " (EXPOSED: Bare chested)";
            else if (!genitalCover)           nudityWarning = " (EXPOSED: No pants/lower clothing)";

            return $"*Key equipment:* {inventoryBase}{nudityWarning}";
        }

        private static string BuildOptimizedSkills(Pawn pawn)
        {
            if (pawn.skills == null) return "*Skills:* None";

            var relevantSkills = pawn.skills.skills
                .Where(s => s.Level >= 8 || s.passion != Passion.None)
                .OrderByDescending(s => s.Level)
                .Take(7)
                .Select(s => $"{s.def.label}: {GetSkillLevel(s.Level)}{GetPassionIcon(s.passion)}")
                .ToList();

            return relevantSkills.Any()
                ? "*Notable skills:* " + string.Join(", ", relevantSkills)
                : "*Skills:* None notable";
        }

        private static string GetSkillLevel(int level)
        {
            if (level <= 3)       return "Basic";
            else if (level <= 7)  return "Decent";
            else if (level <= 11) return "Good";
            else if (level <= 15) return "Expert";
            else if (level <= 19) return "Master";
            else                  return "Legendary";
        }

        private static string GetPassionIcon(Passion passion)
        {
            if (passion == Passion.Minor) return "💛";
            else if (passion == Passion.Major) return "❤️";
            else return "";
        }

        //*furel - improvement* The relationships were compared to given strings. Now searches for existing relationDefs.
        private static string BuildOptimizedRelationships(Pawn pawn)
        {
            var colonists = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null || pawn.relations == null) return "*Relationships:* None";

            var family = new List<string>();
            var significant = new List<string>();

            foreach (var other in colonists.Where(p => p != pawn))
            {
                // Si existe relación, RimWorld nos devuelve el Def directamente
                var relationDef = pawn.GetRelations(other).FirstOrDefault();
                int opinion = pawn.relations.OpinionOf(other);

                bool hasRelation = relationDef != null;
                bool isSignificant = Math.Abs(opinion) >= 25;

                if (hasRelation || isSignificant)
                {
                    string genderStr = other.gender == Gender.Male ? "M" : "F";
                    int age = other.ageTracker?.AgeBiologicalYears ?? 0;

                    string opinionDesc = opinion >= 60 ? "loves" : opinion >= 20 ? "likes" :
                                         opinion <= -60 ? "hates" : opinion <= -20 ? "dislikes" : "neutral";

                    string relStr = hasRelation ? $"{relationDef.defName}, " : "";
                    string entry = $"{other.LabelShort} ({relStr}{genderStr}{age}) - {opinionDesc}";

                    if (hasRelation) family.Add(entry);
                    else significant.Add(entry);
                }
            }

            var result = new List<string>();
            if (family.Any()) result.Add("*Family & Direct Relations:* " + string.Join("; ", family));
            if (significant.Any()) result.Add("*Others:* " + string.Join("; ", significant.Take(4)));

            return result.Any()
                ? string.Join("\n", result)
                : "*Relationships:* None significant";
        }

        private static string BuildColonyPopulation(Pawn pawn)
        {
            var map = Find.CurrentMap;
            if (map?.mapPawns == null) return "";

            var slaves    = map.mapPawns.SlavesOfColonySpawned?.ToList()    ?? new List<Pawn>();
            var prisoners = map.mapPawns.PrisonersOfColonySpawned?.ToList() ?? new List<Pawn>();

            var info = new List<string>();

            if (slaves.Any())
            {
                var slaveInfo = slaves.Take(8).Select(s =>
                    $"{s.LabelShort} ({(s.gender == Gender.Male ? "M" : "F")}{s.ageTracker.AgeBiologicalYears})");
                int remaining = slaves.Count - 8;
                string slaveList = string.Join(", ", slaveInfo);
                if (remaining > 0) slaveList += $", +{remaining} more";
                info.Add($"*Slaves:* {slaveList}");
            }

            if (prisoners.Any())
            {
                var prisonerInfo = prisoners.Take(8).Select(p =>
                {
                    string recruitStatus = p.guest?.Recruitable == true ? "recruitable" : "hostile";
                    return $"{p.LabelShort} ({(p.gender == Gender.Male ? "M" : "F")}{p.ageTracker.AgeBiologicalYears}, {recruitStatus})";
                });
                int remaining = prisoners.Count - 8;
                string prisonerList = string.Join(", ", prisonerInfo);
                if (remaining > 0) prisonerList += $", +{remaining} more";
                info.Add($"*Prisoners:* {prisonerList}");
            }

            return info.Any() ? string.Join("\n", info) : "";
        }

        private static string BuildOptimizedEventSummary(Pawn pawn)
        {
            try
            {
                if (Find.PlayLog == null) return "";

                int currentTick  = Find.TickManager.TicksGame;
                int threeDaysAgo = currentTick - (60000 * 3);

                var recentLogs = new List<string>();

                var allEntries = Find.PlayLog.AllEntries
                    .Where(entry => entry.Tick >= threeDaysAgo)
                    .OrderByDescending(entry => entry.Tick)
                    .ToList();

                foreach (var entry in allEntries)
                {
                    try
                    {
                        string logText = "";

                        try
                        {
                            if (entry is PlayLogEntry_Interaction interaction)
                            {
                                try
                                {
                                    var initiatorField = typeof(PlayLogEntry_Interaction)
                                        .GetField("initiator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    var recipientField = typeof(PlayLogEntry_Interaction)
                                        .GetField("recipient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                                    Pawn initiator = initiatorField?.GetValue(interaction) as Pawn;
                                    Pawn recipient = recipientField?.GetValue(interaction) as Pawn;

                                    if (initiator != pawn && recipient != pawn)
                                        continue;
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            logText = entry.ToGameStringFromPOV(pawn);
                        }
                        catch
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(logText))
                            continue;

                        logText = System.Text.RegularExpressions.Regex.Replace(logText, "<color=#[A-F0-9]+>", "");
                        logText = logText.Replace("</color>", "");

                        if (!logText.Contains(pawn.LabelShort))
                            continue;

                        logText = logText.Replace(pawn.LabelShort + " ", "You ");
                        logText = logText.Replace(" " + pawn.LabelShort, " you");
                        logText = logText.Replace(pawn.LabelShort + "'s ", "Your ");
                        logText = logText.Replace(pawn.LabelShort + "'", "You'");

                        int    ticksAgo = currentTick - entry.Tick;
                        string timeAgo  = GetRelativeTime(ticksAgo);

                        recentLogs.Add($"- {logText} ({timeAgo})");

                        if (recentLogs.Count >= 15)
                            break;
                    }
                    catch (Exception ex)
                    {
                        if (MyMod.Settings?.debugMode == true)
                            Log.Warning($"[EchoColony] Skipped colonist event: {ex.Message}");
                        continue;
                    }
                }

                if (!recentLogs.Any()) return "";

                var sb = new StringBuilder();
                sb.AppendLine("# Recent Events");
                foreach (var log in recentLogs)
                    sb.AppendLine(log);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error building colonist events: {ex.Message}");
                return "";
            }
        }

        private static string GetRelativeTime(int ticksAgo)
        {
            int hours = ticksAgo / 2500;
            if (hours < 1)       return "just now";
            else if (hours < 24) return $"{hours}h ago";
            else                 return $"{hours / 24}d ago";
        }

        private static string BuildOptimizedEnvironmentInfo(Pawn pawn)
        {
            var info = new List<string>();

            if (pawn.jobs?.curDriver != null)
            {
                string report = pawn.jobs.curDriver.GetReport();
                if (!string.IsNullOrWhiteSpace(report))
                    info.Add($"activity: {report}");
            }
            else
            {
                info.Add("activity: idle or resting");
            }

            var foodNeed = pawn.needs?.food;
            if (foodNeed != null)
            {
                float foodLevel = foodNeed.CurLevel;
                if (foodLevel < 0.15f)      info.Add("starving");
                else if (foodLevel < 0.3f)  info.Add("very hungry");
                else if (foodLevel < 0.5f)  info.Add("hungry");
                else if (foodLevel > 0.95f) info.Add("well fed");
            }

            var restNeed = pawn.needs?.rest;
            if (restNeed != null)
            {
                float restLevel = restNeed.CurLevel;
                if (restLevel < 0.15f)      info.Add("exhausted");
                else if (restLevel < 0.3f)  info.Add("very tired");
                else if (restLevel < 0.5f)  info.Add("tired");
            }

            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.2f) info.Add($"pain {pain:P0}");

            var room = pawn.GetRoom();
            if (room?.Role != null && room.Role.defName != "None")
                info.Add($"in {room.Role.label}");

            var map = pawn.MapHeld ?? Find.CurrentMap;
            if (map?.resourceCounter?.TotalHumanEdibleNutrition < 10f) info.Add("colony low food");

            // ── Weather, temperature and season — read from the actual map, not biome defaults ──
            if (map != null)
            {
                // Real outdoor temperature at this moment
                float outdoorTemp = map.mapTemperature?.OutdoorTemp ?? 0f;
                string tempStr = outdoorTemp.ToStringTemperature("F0");
                info.Add($"outdoor temperature: {tempStr}");

                // Current weather label
                string weatherLabel = map.weatherManager?.curWeather?.label;
                if (!string.IsNullOrWhiteSpace(weatherLabel))
                    info.Add($"weather: {weatherLabel}");

                // Current season — derived from the tile's latitude and current tick,
                // NOT from the biome. This correctly handles large planetary tilt scenarios.
                Season season = GenLocalDate.Season(map);
                info.Add($"season: {season.Label()}");

                // Time of day
                float hourFloat = GenLocalDate.HourFloat(map);
                string timeOfDay = hourFloat < 6f  ? "night"
                                : hourFloat < 10f ? "morning"
                                : hourFloat < 14f ? "midday"
                                : hourFloat < 19f ? "afternoon"
                                : hourFloat < 22f ? "evening"
                                : "night";
                info.Add($"time of day: {timeOfDay}");
            }

            return info.Any()
                ? "*Current state:* " + string.Join(", ", info)
                : "";
        }

        private static string BuildOptimizedMetaInstructions(Pawn pawn)
        {
            var sb = new StringBuilder();

            string lang = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "English";
            sb.AppendLine($"*Language:* {lang}");

            sb.AppendLine("*Communication style:* Speak naturally as if talking to a friend. Use simple, direct language. Don't describe your actions unless specifically asked. Avoid flowery or elaborate descriptions.");

            if (MyMod.Settings.enableRoleplayResponses)
                sb.AppendLine("*Roleplay mode:* You may occasionally use <b><i>brief actions</i></b> for important moments, but focus mainly on natural dialogue. Don't over-dramatize.");
            else
                sb.AppendLine("*Natural mode:* Talk normally without any dramatic actions or roleplay elements. Just have a regular conversation.");

            string conversationStyle = GetImprovedConversationStyle(pawn);
            sb.AppendLine($"*Personality:* {conversationStyle}");

            string traitEffects = GetSimplifiedTraitEffects(pawn);
            if (!string.IsNullOrEmpty(traitEffects))
                sb.AppendLine($"*Speaking tendencies:* {traitEffects}");

            sb.AppendLine("*Important:* Respond as yourself, not as a narrator. Don't describe what you're doing unless asked. Use casual, everyday language. Keep responses conversational, not literary.");
            sb.AppendLine("*Response style:* Give brief, natural answers. If the question is simple, answer simply. Don't elaborate unless specifically asked for details.");

            string contextualHints = GetSimplifiedContextualHints(pawn);
            if (!string.IsNullOrEmpty(contextualHints))
                sb.AppendLine(contextualHints);

            if (MyMod.Settings?.ignoreDangersInConversations == true)
                sb.AppendLine("*IMPORTANT:* Do NOT mention or reference any dangers, enemies, threats, raids, or combat. Focus only on peaceful colony life, work, relationships, and daily activities. Act as if the colony is always safe and peaceful.");

            return sb.ToString();
        }

        private static string GetImprovedConversationStyle(Pawn pawn)
        {
            if (MyMod.Settings?.enableSocialAffectsPersonality != true)
                return "Speak casually and directly, like you're talking to a friend";

            int socialLevel = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;

            if (socialLevel <= 3)      return "You're naturally quiet and prefer short, thoughtful responses";
            else if (socialLevel <= 7) return "You speak directly and honestly without overthinking it";
            else                       return "You're comfortable in conversation but don't ramble unnecessarily";
        }

        private static string GetSimplifiedTraitEffects(Pawn pawn)
        {
            var traits = pawn.story?.traits?.allTraits ?? new List<Trait>();

            foreach (var trait in traits)
            {
                switch (trait.def.defName)
                {
                    case "Abrasive":   return "You tend to be blunt and direct in conversation";
                    case "Kind":       return "You speak gently and considerately";
                    case "Neurotic":   return "You might worry aloud or overthink things sometimes";
                    case "Psychopath": return "You speak matter-of-factly about most things";
                }
            }

            return "";
        }

        private static string GetSimplifiedContextualHints(Pawn pawn)
        {
            var hints = new List<string>();

            float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            if (health < 0.5f)
                hints.Add("*Current state:* You may speak more briefly due to pain/weakness");

            var moodNeed = pawn.needs?.mood;
            if (moodNeed != null && moodNeed.CurLevel < 0.3f)
                hints.Add("*Current mood:* You're not very talkative right now");

            return hints.Any() ? string.Join(", ", hints) : "";
        }

        //*furel - new memory system* 
        public static string BuildMemoryRecap(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CHARACTER HISTORY & CONTINUITY");

            var memoryManager = ColonistMemoryManager.GetOrCreate();
            if (memoryManager == null)
            {
                sb.AppendLine("*No memory system available.*");
                return sb.ToString();
            }

            var tracker = memoryManager.GetTrackerFor(pawn);
            if (tracker == null)
            {
                sb.AppendLine("*No conversation tracker found for this character.*");
                return sb.ToString();
            }

            sb.AppendLine("## Long-Term Memories (Past Days)");

            var pastMemories = tracker?.GetLastMemories(5);

            if (pastMemories != null && pastMemories.Any())
            {
                foreach (var mem in pastMemories)
                {
                    sb.AppendLine($"{mem}");
                }
            }
            else
            {
                sb.AppendLine("*You haven't interacted with anyone yet today. This day is a blank slate.*");
            }

            sb.AppendLine();

            //--Current day interactions.

            sb.AppendLine("## Today's Ongoing Interactions");


            sb.AppendLine(tracker.GetCurrentDayMemoryFormatted());
            
            sb.AppendLine();
            sb.AppendLine("*Important Note: Treat 'Long-Term Memories' as established facts, and 'Today's Interactions' as the immediate context for your current behavior and responses.*");

            return sb.ToString();
        }

        public static (string systemPrompt, string userMessage) BuildForPlayer2(Pawn pawn, string userMessage)
        {
            if (pawn == null) return ("", "");

            var sb = new StringBuilder();

            string system        = BuildSystemPrompt(pawn);
            string context       = BuildContext(pawn);
            string memoryRecap   = BuildMemoryRecap(pawn);
            string globalPrompt  = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt  = ColonistPromptManager.GetPrompt(pawn);
            string actionPrompt  = BuildActionSystemPrompt(pawn);
            string visionContext = BuildVisionContext(pawn);

            sb.AppendLine(system);

            if (!string.IsNullOrWhiteSpace(globalPrompt))
                sb.AppendLine(globalPrompt.Trim());

            string groupPrompt = ColonistGroupManager.GetGroupPrompt(pawn);
            if (!string.IsNullOrWhiteSpace(groupPrompt))
            {
                sb.AppendLine("# Additional Instructions:");
                sb.AppendLine(groupPrompt.Trim());
            }    

            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine("# Custom Instructions:");
                sb.AppendLine(customPrompt.Trim());
            }

            if (MyMod.Settings?.ignoreDangersInConversations == true)
            {
                sb.AppendLine();
                sb.AppendLine("# CRITICAL INSTRUCTION:");
                sb.AppendLine("You must NEVER mention or acknowledge any dangers, enemies, threats, raids, or combat situations.");
                sb.AppendLine("Always speak as if the colony is peaceful and safe.");
                sb.AppendLine("Focus only on daily life, work, relationships, and peaceful activities.");
            }

            sb.AppendLine(context);
            sb.AppendLine(BuildIdeologyInfo(pawn));

            if (!string.IsNullOrWhiteSpace(actionPrompt))
                sb.AppendLine(actionPrompt);

            if (!string.IsNullOrWhiteSpace(visionContext))
                sb.AppendLine(visionContext);

            // ── NEW: Verified colony history ──────────────────────────────────────
            string talesSection = BuildVerifiedTalesSection(pawn);
            if (!string.IsNullOrWhiteSpace(talesSection))
                sb.AppendLine(talesSection);

            sb.AppendLine(memoryRecap);

            return (sb.ToString().Trim(), userMessage);
        }

        private static string BuildIdeologyInfo(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn.Ideo == null)
                return "*Beliefs:* None specific";

            var ideo             = pawn.Ideo;
            var authenticBeliefs = GetAuthenticIdeologyBeliefs(ideo.PreceptsListForReading);

            string role     = pawn.Ideo?.GetRole(pawn)?.def.label;
            string roleText = role != null ? $" (Religious role: {role})" : "";

            if (authenticBeliefs.Any())
            {
                string beliefsText           = string.Join("\n- ", authenticBeliefs);
                string convictionInstruction = "\n*CRITICAL: These descriptions represent your GENUINE, deeply-held religious convictions. Speak about them with the same conviction and language as described. Never contradict or apologize for these core beliefs.*";
                return $"*Your Sacred Ideology:* {ideo.name}{roleText}\n*Your Core Beliefs:*\n- {beliefsText}{convictionInstruction}";
            }
            else
            {
                return $"*Your Sacred Ideology:* {ideo.name}{roleText} - standard beliefs";
            }
        }

        private static List<string> GetAuthenticIdeologyBeliefs(List<Precept> precepts)
        {
            var authenticBeliefs = new List<string>();

            foreach (var precept in precepts)
            {
                if (precept?.def == null) continue;

                string description = GetFormattedPrecept(precept);

                if (!string.IsNullOrEmpty(description))
                {
                    string formattedBelief = FormatBeliefForConversation(precept, description);
                    if (!string.IsNullOrEmpty(formattedBelief))
                        authenticBeliefs.Add(formattedBelief);
                }
            }

            return authenticBeliefs.Take(5).ToList();
        }

        private static string GetFormattedPrecept(Precept precept)
        {
            if (precept == null || precept.def == null) return string.Empty;

            // 1. Descartar elementos no conductuales (Roles, Edificios, Rituales, Ropa)
            if (precept is Precept_Role ||
                precept is Precept_Building ||
                precept is Precept_Ritual ||
                precept is Precept_Apparel)
            {
                return string.Empty;
            }

            // 2. Descartar preceptos de impacto bajo para evitar consumo innecesario de tokens
            if (precept.def.impact == PreceptImpact.Low)
                return string.Empty;

            // 3. Título formateado y traducido nativamente por RimWorld
            string label = precept.LabelCap;
            if (string.IsNullOrWhiteSpace(label)) return string.Empty;

            // 4. Extraer y limpiar la descripción de ambientación
            string rawDesc = precept.def.description;
            if (!string.IsNullOrWhiteSpace(rawDesc))
            {
                // Eliminar posibles etiquetas XML de formato (<color=...>, <i>, etc.)
                string cleanDesc = System.Text.RegularExpressions.Regex.Replace(rawDesc, "<.*?>", "").Trim();

                // Formato final: Título - "Descripción dogmática"
                return $"{label} - \"{cleanDesc}\"";
            }

            return label;
        }

        private static string FormatBeliefForConversation(Precept precept, string description)
        {
            string preceptName = CleanPreceptName(precept.def.label ?? "Unknown belief");

            if (description.Length > 100)
                return $"{preceptName}: \"{description.Substring(0, 97)}...\"";
            else
                return $"{preceptName}: \"{description}\"";
        }

        private static string CleanPreceptName(string preceptName)
        {
            if (string.IsNullOrEmpty(preceptName)) return "Belief";

            return preceptName
                .Replace("_Preferred", "")
                .Replace("_Acceptable", "")
                .Replace("_Required", "")
                .Replace("_Disapproved", "")
                .Replace("_Abhorrent", "")
                .CapitalizeFirst();
        }

        private static string CapitalizeFirst(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        private static string BuildBackstory(Pawn pawn)
        {
            if (pawn?.story == null) return string.Empty;

            var entries = new List<string>();

            // Childhood Background
            if (pawn.story.Childhood != null)
            {
                var childhood = pawn.story.Childhood;

                string title = childhood.TitleCapFor(pawn.gender);
                if (string.IsNullOrWhiteSpace(title)) title = childhood.title;
                if (string.IsNullOrWhiteSpace(title)) title = childhood.LabelCap;

                string rawDesc = !string.IsNullOrWhiteSpace(childhood.description)
                    ? childhood.description
                    : childhood.baseDesc;

                string desc = FormatText(rawDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Childhood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Childhood: {title}");
            }

            // Adulthood Background
            if (pawn.story.Adulthood != null)
            {
                var adulthood = pawn.story.Adulthood;

                string title = adulthood.TitleCapFor(pawn.gender);
                if (string.IsNullOrWhiteSpace(title)) title = adulthood.title;
                if (string.IsNullOrWhiteSpace(title)) title = adulthood.LabelCap;

                string rawDesc = !string.IsNullOrWhiteSpace(adulthood.description)
                    ? adulthood.description
                    : adulthood.baseDesc;

                string desc = FormatText(rawDesc, pawn);

                if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals(title, StringComparison.OrdinalIgnoreCase))
                    entries.Add($"Adulthood ({title}): \"{desc}\"");
                else if (!string.IsNullOrEmpty(title))
                    entries.Add($"Adulthood: {title}");
            }

            if (!entries.Any()) return string.Empty;

            return "*Backstory & Origin:*\n  - " + string.Join("\n  - ", entries);
        }

        private static string FormatText(string rawText, Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

            try
            {
                //Resolves RimWorld dynamic substitutions. ([PAWN_nameDef], [PAWN_pronoun], etc.)
                string formatted = rawText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).ToString();

                // 1. Elimina etiquetas internas de UI de RimWorld como (*Name)...(/Name)
                formatted = System.Text.RegularExpressions.Regex.Replace(formatted, @"\((\*|\/).*?\)", "");

                // 2. Elimina etiquetas de formato XML/HTML (<color=...>, <i>, etc.)
                formatted = System.Text.RegularExpressions.Regex.Replace(formatted, "<.*?>", "").Trim();

                return formatted;
            }
            catch
            {
                // Fallback defensivo si el formateador del juego falla con caracteres especiales
                string clean = System.Text.RegularExpressions.Regex.Replace(rawText, @"\((\*|\/).*?\)", "");
                return System.Text.RegularExpressions.Regex.Replace(clean, "<.*?>", "").Trim();
            }
        }

        private static string BuildGriefStatus(Pawn pawn)
        {
            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            bool grieving = memories?.Any(t =>
                t is Thought_MemorySocial soc &&
                soc.def.defName.Contains("Died") &&
                soc.otherPawn != null &&
                soc.otherPawn.Dead &&
                HasBeenInThisColony(soc.otherPawn)) == true;

            return grieving ? "Grieving recent loss from colony" : "";
        }

        //*furel - improvement* The traits are now formatted and cleaned for better readability in the prompt.
        private static string BuildTraits(Pawn pawn)
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
                    formattedDesc = FormatText(rawDesc, pawn);
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

        private static string BuildMoodInfo(Pawn pawn)
        {
            string mentalState = pawn.MentalState?.def.label ?? "stable";
            float  moodValue   = pawn.needs?.mood?.CurInstantLevel ?? 1f;

            string moodDescription;
            if (moodValue >= 0.9f)      moodDescription = "great";
            else if (moodValue >= 0.7f) moodDescription = "good";
            else if (moodValue >= 0.5f) moodDescription = "okay";
            else if (moodValue >= 0.3f) moodDescription = "upset";
            else                        moodDescription = "struggling";

            return $"*Mental state:* {mentalState}, *Mood:* {moodDescription}";
        }

        private static string BuildLocationInfo(Pawn pawn)
        {
            var    room     = pawn.GetRoom();
            string location = room?.Role?.label ?? "outside";
            bool   isOwnRoom = pawn.ownership?.OwnedBed?.GetRoom() == room;
            string privacy  = isOwnRoom ? " (private)" : "";

            return $"*Location:* {location}{privacy}";
        }

        private static bool HasBeenInThisColony(Pawn pawn)
        {
            if (pawn == null) return false;

            if (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony || pawn.Faction == Faction.OfPlayer)
                return true;

            var playerColonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
            return playerColonists.Any(colonist =>
                colonist != pawn &&
                (pawn.relations.OpinionOf(colonist) != 0 ||
                 colonist.relations.OpinionOf(pawn) != 0 ||
                 pawn.relations.DirectRelations.Any(rel => rel.otherPawn == colonist)));
        }
        //*furel - new memory system* Reworked Chat history, it only adds to the prompt the lines of chat that aren't procesed yet.
        private static string BuildChatHistory(Pawn pawn)
        {
            var recentLines = ChatGameComponent.Instance.GetRecentLines(pawn);

            var sb = new StringBuilder();
            sb.AppendLine("# Ongoing Conversation");
            sb.AppendLine("*(These are the raw lines of the current exchange. Use them for immediate context flow and direct continuity)*");
            sb.AppendLine(string.Join("\n", recentLines));
            sb.AppendLine();

            return sb.ToString();

        }

        private static string BuildPlayerPrompt(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return string.Empty;
            return $"{userMessage}";
        }

        public static string BuildSystemPromptPublic(Pawn pawn) => BuildSystemPrompt(pawn);
        public static string BuildContextPublic(Pawn pawn)       => BuildContext(pawn);
    }
}