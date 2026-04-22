using System.Text;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using RimWorld.Planet;
using System;

namespace EchoColony.Factions
{
    /// <summary>
    /// Builds the AI prompt for a conversation between the player's colony
    /// and a faction leader via the comms console.
    ///
    /// The leader knows they are speaking over comms — no in-person roleplay.
    /// The operator's Social skill affects how persuasive the conversation can be.
    /// </summary>
    public static class FactionPromptContextBuilder
    {
        public static string Build(Pawn operatorPawn, Faction targetFaction, string userMessage, bool isPlayerMode = false)
        {
            if (targetFaction == null) return string.Empty;

            var sb = new StringBuilder();

            sb.AppendLine(BuildSystemPrompt(operatorPawn, targetFaction, isPlayerMode));
            sb.AppendLine(BuildFactionContext(targetFaction));
            sb.AppendLine(BuildRelationshipContext(operatorPawn, targetFaction, isPlayerMode));
            // In player mode we don't expose the colonist's stats — the player speaks as themselves
            if (!isPlayerMode)
                sb.AppendLine(BuildOperatorContext(operatorPawn));
            sb.AppendLine(BuildColonyContext(operatorPawn));
            sb.AppendLine(BuildChatHistory(targetFaction, isPlayerMode));
            sb.AppendLine(BuildPlayerPrompt(operatorPawn, userMessage, isPlayerMode));

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // SYSTEM PROMPT
        // ═══════════════════════════════════════════════════════════════

        private static string BuildSystemPrompt(Pawn operatorPawn, Faction targetFaction, bool isPlayerMode)
        {
            string leaderName   = GetLeaderName(targetFaction);
            string factionName  = targetFaction.Name;
            string factionKind  = GetFactionKindDescription(targetFaction);
            string goodwillDesc = GetGoodwillDescription(targetFaction.PlayerGoodwill);
            string colonyName   = Faction.OfPlayer?.Name ?? "their colony";

            // Who is on the other end of the line
            string callerIdentity;
            if (isPlayerMode)
            {
                callerIdentity =
                    $"The person calling is the OVERSEER of {colonyName} — the one who makes the real decisions. " +
                    $"Not a colonist. Not an envoy. The actual decision-maker contacting you directly. " +
                    $"Treat them with appropriate weight — more direct, less small talk.";
            }
            else
            {
                string operatorName = operatorPawn?.LabelShort ?? "an envoy";
                int    age          = operatorPawn?.ageTracker?.AgeBiologicalYears ?? 20;
                int    social       = operatorPawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                callerIdentity =
                    $"The caller is {operatorName}, an envoy from {colonyName} (age {age}, Social skill {social}/20). " +
                    $"They speak on behalf of their colony but are NOT the final authority.";
            }

            string lang     = Prefs.LangFolderName?.ToLower() ?? "english";
            string langLine = lang != "english" ? $"Respond in {lang}.\n" : "";

            var component    = FactionChatGameComponent.Instance;
            bool isFirstContact = component?.IsFirstContact(targetFaction, isPlayerMode) ?? true;
            int  convCount      = component?.GetConversationCount(targetFaction, isPlayerMode) ?? 0;
            string lastConvDesc = component?.GetLastConversationDescription(targetFaction, isPlayerMode);

            string historyLine;
            if (isFirstContact)
            {
                historyLine =
                    $"This is the FIRST TIME {colonyName} has contacted you. " +
                    $"You don't know them yet. React with measured caution — not hostility, not warmth. " +
                    $"Size them up.\n";
            }
            else if (convCount < 5)
            {
                historyLine =
                    $"You've spoken with {colonyName} {convCount} time{(convCount > 1 ? "s" : "")}" +
                    (lastConvDesc != null ? $", last {lastConvDesc}" : "") + ". " +
                    $"You have a basic impression of them.\n";
            }
            else
            {
                historyLine =
                    $"You have an established history with {colonyName} ({convCount} contacts" +
                    (lastConvDesc != null ? $", last {lastConvDesc}" : "") + "). " +
                    $"Speak with the familiarity — or wariness — that history warrants.\n";
            }

            return
                $"{langLine}" +
                $"You are {leaderName}, leader of {factionName} — {factionKind}.\n" +
                $"{callerIdentity}\n" +
                $"Current standing with {colonyName}: {goodwillDesc}.\n" +
                $"{historyLine}\n" +
                $"COMMUNICATION FORMAT:\n" +
                $"- This is a long-distance transmission. Voice only. No visuals.\n" +
                $"- NEVER describe physical actions or gestures — you cannot be seen.\n" +
                $"- You may briefly reference background sounds or your current situation.\n" +
                $"- Match response length to the moment: a simple greeting gets a simple reply,\n" +
                $"  a serious negotiation can be more substantive. Never ramble.\n" +
                $"\n" +
                $"TONE — based on current relations ({goodwillDesc}):\n" +
                $"{GetToneGuidance(targetFaction)}\n" +
                $"\n" +
                $"CHARACTER:\n" +
                $"- You are {leaderName}. You have your own agenda. You don't bend easily.\n" +
                $"- Your personality reflects your faction's culture and values.\n" +
                $"- You do not perform warmth you don't feel. You do not hide hostility you do.\n";
        }

        private static string GetToneGuidance(Faction faction)
        {
            if (faction.HostileTo(Faction.OfPlayer))
                return
                    "You are at WAR with them. Be cold, dismissive, or openly hostile.\n" +
                    "Short answers. No pleasantries. You have nothing friendly to say.";

            int gw = faction.PlayerGoodwill;

            if (gw >= 75)
                return
                    "You have strong, trusted relations. Be warm, open, and genuinely engaged.\n" +
                    "You can make small talk, show humor, and speak with real familiarity.\n" +
                    "This is someone you respect and are glad to hear from.";

            if (gw >= 40)
                return
                    "Relations are good. Be friendly and cooperative, but still professional.\n" +
                    "You're comfortable with them — cordial, not cold. Show some personality.";

            if (gw >= 10)
                return
                    "Relations are neutral to cautiously positive. Be polite but measured.\n" +
                    "You'll listen and engage, but you're not going out of your way.";

            if (gw >= -25)
                return
                    "Relations are tense. Keep it brief and transactional.\n" +
                    "You're not hostile, but you're not warm either. Choose words carefully.";

            return
                "Relations are poor. You're guarded and skeptical.\n" +
                "Short answers. You're not interested in pleasantries.";
        }

        // ═══════════════════════════════════════════════════════════════
        // FACTION CONTEXT
        // ═══════════════════════════════════════════════════════════════

        private static string BuildFactionContext(Faction faction)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Your Faction");

            string leaderName  = GetLeaderName(faction);
            string factionKind = GetFactionKindDescription(faction);
            string techLevel   = faction.def.techLevel.ToString();

            sb.AppendLine($"*Name:* {faction.Name}");
            sb.AppendLine($"*Your name:* {leaderName}");
            sb.AppendLine($"*Type:* {factionKind}");
            sb.AppendLine($"*Tech level:* {techLevel}");

            // Faction personality from def
            string personality = GetFactionPersonality(faction);
            if (!string.IsNullOrEmpty(personality))
                sb.AppendLine($"*Your people's nature:* {personality}");

            // Hostile factions
            var hostileTo = Find.FactionManager.AllFactionsVisible
                .Where(f => f != faction && f != Faction.OfPlayer && faction.RelationWith(f)?.kind == FactionRelationKind.Hostile)
                .Select(f => f.Name)
                .Take(3)
                .ToList();
            if (hostileTo.Any())
                sb.AppendLine($"*Currently at war with:* {string.Join(", ", hostileTo)}");

            // Allied factions
            var alliedTo = Find.FactionManager.AllFactionsVisible
                .Where(f => f != faction && f != Faction.OfPlayer && faction.RelationWith(f)?.kind == FactionRelationKind.Ally)
                .Select(f => f.Name)
                .Take(3)
                .ToList();
            if (alliedTo.Any())
                sb.AppendLine($"*Allied with:* {string.Join(", ", alliedTo)}");

            // What the caller can actually request right now — shapes how you respond
            string actionsContext = FactionRequestHandler.GetAvailableActionsForPrompt(faction);
            if (!string.IsNullOrEmpty(actionsContext))
            {
                sb.AppendLine();
                sb.AppendLine("# What You Can Do For Them Right Now");
                sb.AppendLine(actionsContext);
                sb.AppendLine("Shape your responses accordingly — don't promise things you can't deliver.");
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // RELATIONSHIP CONTEXT
        // ═══════════════════════════════════════════════════════════════

        private static string BuildRelationshipContext(Pawn operatorPawn, Faction faction, bool isPlayerMode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Relationship with the Player Colony");

            int    goodwill     = faction.PlayerGoodwill;
            string goodwillDesc = GetGoodwillDescription(goodwill);
            string kindStr      = faction.PlayerRelationKind.ToString();

            sb.AppendLine($"*Current goodwill:* {goodwill} ({goodwillDesc})");
            sb.AppendLine($"*Relation status:* {kindStr}");

            // How much you trust them based on goodwill
            string trustLine = goodwill >= 75  ? "You consider them a reliable partner and speak openly." :
                               goodwill >= 40  ? "You are cautiously friendly — willing to deal but not to trust blindly." :
                               goodwill >= 0   ? "Relations are neutral. You'll listen, but you're watching them." :
                               goodwill >= -25 ? "You have reservations about them. Be polite but guarded." :
                                                 "You have little goodwill toward them. Keep it short and transactional.";
            sb.AppendLine($"*Your attitude toward them:* {trustLine}");

            // Recent history from play log
            string recentEvents = GetRecentFactionEvents(faction);
            if (!string.IsNullOrEmpty(recentEvents))
                sb.AppendLine($"*Recent history:* {recentEvents}");

            // Social skill of operator — affects persuasion
            if (operatorPawn != null)
            {
                int socialLevel = operatorPawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                string socialDesc = socialLevel >= 15 ? "highly skilled diplomat — you find them unusually persuasive and worth listening to" :
                                    socialLevel >= 10 ? "competent communicator — you take their words seriously" :
                                    socialLevel >= 5  ? "average speaker — standard consideration" :
                                                        "poor communicator — you may be slightly impatient or dismissive";
                sb.AppendLine($"*Your read on the caller:* {socialDesc} (Social {socialLevel})");
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // OPERATOR CONTEXT (who is calling)
        // ═══════════════════════════════════════════════════════════════

        private static string BuildOperatorContext(Pawn pawn)
        {
            if (pawn == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("# Who Is Calling");

            string name       = pawn.LabelShort;
            int    age        = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            string gender     = pawn.gender.ToString().ToLower();
            int    socialSkill = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;

            sb.AppendLine($"*Caller:* {name}, {age}y old, {gender}");
            sb.AppendLine($"*Social skill:* {socialSkill}/20");

            string xenotype = ModsConfig.BiotechActive
                ? (pawn.genes?.Xenotype?.label ?? "baseline human")
                : "human";
            if (xenotype != "baseline human" && xenotype != "human")
                sb.AppendLine($"*Species/xenotype:* {xenotype}");

            // Traits that might affect how you perceive them
            var notableTraits = pawn.story?.traits?.allTraits
                .Where(t => t.def.defName == "Abrasive" ||
                            t.def.defName == "Kind" ||
                            t.def.defName == "Psychopath" ||
                            t.def.defName == "TooSmart" ||
                            t.def.defName == "Charming")
                .Select(t => t.LabelCap)
                .ToList();

            if (notableTraits != null && notableTraits.Any())
                sb.AppendLine($"*Comes across as:* {string.Join(", ", notableTraits)}");

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // COLONY CONTEXT (what you know about them)
        // ═══════════════════════════════════════════════════════════════

        private static string BuildColonyContext(Pawn operatorPawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# What You Know About Their Colony");

            string colonyName = Faction.OfPlayer?.Name ?? "their colony";
            sb.AppendLine($"*Colony name:* {colonyName}");

            var map = Find.CurrentMap ?? (operatorPawn?.Map);
            if (map != null)
            {
                int colonistCount = map.mapPawns.FreeColonistsSpawned.Count();
                sb.AppendLine($"*Approximate size:* {colonistCount} known colonists");
            }

            // Wealth gives you an idea of their power
            float wealth = Faction.OfPlayer?.def != null ? WealthUtility.PlayerWealth : 0f;
            string wealthDesc = wealth > 500000 ? "a wealthy and powerful colony" :
                                wealth > 150000 ? "a mid-sized established colony" :
                                wealth > 50000  ? "a developing colony" :
                                                  "a small or struggling colony";
            sb.AppendLine($"*Your impression:* {wealthDesc}");

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // CHAT HISTORY
        // ═══════════════════════════════════════════════════════════════

        private static string BuildChatHistory(Faction faction, bool isPlayerMode)
        {
            var chatLog = FactionChatGameComponent.Instance?.GetChat(faction, isPlayerMode);
            if (chatLog == null || !chatLog.Any()) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("# This Comms Session");
            sb.AppendLine(string.Join("\n", chatLog.TakeLast(20)));
            return sb.ToString();
        }

        private static string BuildPlayerPrompt(Pawn operatorPawn, string userMessage, bool isPlayerMode)
        {
            string name = isPlayerMode
                ? "You (direct)"
                : operatorPawn?.LabelShort ?? "Colonist";
            return $"{name} (via comms): \"{userMessage}\"";
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        public static string GetLeaderName(Faction faction)
        {
            var leader = faction.leader;
            if (leader != null) return leader.LabelShort;

            // Fallback: generate a plausible title
            string kind = faction.def.defName.ToLower();
            if (kind.Contains("tribe"))   return "the Chief";
            if (kind.Contains("empire"))  return "the Stellarch";
            if (kind.Contains("pirate"))  return "the Boss";
            return "the Leader";
        }

        private static string GetFactionKindDescription(Faction faction)
        {
            string defName = faction.def.defName.ToLower();
            string label   = faction.def.label?.ToLower() ?? "";

            if (defName.Contains("tribe") || label.Contains("tribe"))
                return "a tribal people with traditional values and strong community bonds";
            if (defName.Contains("empire") || label.Contains("empire"))
                return "an imperial civilization with strict hierarchy and honor codes";
            if (defName.Contains("pirate") || label.Contains("pirate") || defName.Contains("outlander_rough"))
                return "a rough outlaw faction that values strength and profit above all";
            if (defName.Contains("outlander") || label.Contains("outlander"))
                return "an outlander settlement — pragmatic traders and survivors";
            if (defName.Contains("mech") || label.Contains("mechanoid"))
                return "a mechanoid collective — cold, logical, non-human intelligence";

            return faction.def.label ?? "an unknown faction";
        }

        private static string GetFactionPersonality(Faction faction)
        {
            string defName = faction.def.defName.ToLower();

            if (defName.Contains("tribe"))
                return "Spiritual and communal. Deeply suspicious of outsiders but fiercely loyal to allies. Honor-bound.";
            if (defName.Contains("empire"))
                return "Formal, proud, and hierarchical. Expect proper respect. Slow to trust but powerful allies.";
            if (defName.Contains("pirate") || defName.Contains("rough"))
                return "Blunt, self-interested, and intimidating. Respect power and profit. Don't waste their time.";
            if (defName.Contains("outlander"))
                return "Practical and trade-oriented. They'll deal if there's something in it for them.";

            return "";
        }

        private static string GetGoodwillDescription(int goodwill)
        {
            if (goodwill >= 75)  return "strong allies";
            if (goodwill >= 40)  return "friendly";
            if (goodwill >= 10)  return "cautiously positive";
            if (goodwill >= -10) return "neutral";
            if (goodwill >= -40) return "tense";
            if (goodwill >= -70) return "hostile";
            return "at war";
        }

        private static string GetRecentFactionEvents(Faction faction)
        {
            if (Find.PlayLog == null) return "";

            int now      = Find.TickManager.TicksGame;
            int twoDays  = now - (60000 * 2);

            var events = new List<string>();

            foreach (var entry in Find.PlayLog.AllEntries
                .Where(e => e.Tick >= twoDays)
                .OrderByDescending(e => e.Tick)
                .Take(20))
            {
                try
                {
                    string text = entry.ToGameStringFromPOV(null);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    text = System.Text.RegularExpressions.Regex.Replace(text, @"<color=#[0-9A-Fa-f]{6,8}>", "");
                    text = text.Replace("</color>", "").Trim();

                    string lower = text.ToLowerInvariant();
                    string factionLower = faction.Name.ToLowerInvariant();

                    if (!lower.Contains(factionLower)) continue;

                    events.Add(text);
                    if (events.Count >= 3) break;
                }
                catch { }
            }

            return events.Any() ? string.Join("; ", events) : "";
        }
    }
}