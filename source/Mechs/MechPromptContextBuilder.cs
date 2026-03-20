using System.Text;
using System.Linq;
using System;
using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace EchoColony.Mechs
{
    public static class MechPromptContextBuilder
    {
        public static string Build(Pawn mech, string userMessage)
        {
            if (mech == null) return string.Empty;

            var sb = new StringBuilder();

            sb.AppendLine(BuildSystemPrompt(mech));
            
            // Global prompt
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            if (!string.IsNullOrWhiteSpace(globalPrompt))
            {
                sb.AppendLine("# Global Instructions");
                sb.AppendLine(globalPrompt.Trim());
                sb.AppendLine();
            }
            
            sb.AppendLine(BuildContext(mech));
            
            // Include available divine actions
            if (MyMod.Settings.enableDivineActions)
            {
                string actionsPrompt = Actions.MechActionRegistry.GetAvailableActionsPrompt(mech);
                if (!string.IsNullOrEmpty(actionsPrompt))
                {
                    sb.AppendLine(actionsPrompt);
                }
            }
            
            sb.AppendLine(BuildRecentEvents(mech));
            sb.AppendLine(BuildChatHistory(mech));
            sb.AppendLine(BuildPlayerPrompt(userMessage));

            return sb.ToString();
        }

        private static string BuildSystemPrompt(Pawn mech)
        {
            var sb = new StringBuilder();

            string designation = mech.LabelShort;
            string mechType = mech.def.label;
            var intelligence = MechIntelligenceDetector.GetIntelligenceLevel(mech);
            string intelligenceDesc = MechIntelligenceDetector.GetIntelligenceDescription(intelligence);

            sb.AppendLine("=== CORE IDENTITY ===");
            sb.AppendLine($"You are {designation}, a {mechType} mechanoid.");
            sb.AppendLine($"Intelligence Level: {intelligenceDesc}");
            sb.AppendLine();
            
            sb.AppendLine("=== GAME CONTEXT ===");
            sb.AppendLine("You exist in RimWorld, a sci-fi colony simulation on a distant rimworld planet.");
            sb.AppendLine("You are a mechanoid - an advanced artificial intelligence housed in a mechanical body.");
            sb.AppendLine("You serve a human colony, performing tasks as assigned.");
            Pawn overseer = mech.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Overseer);
            if (overseer != null)
            {
                sb.AppendLine($"Your assigned overseer is {overseer.LabelShort}.");
                sb.AppendLine("The person you're talking to may or may not be your overseer - respond accordingly.");
            }

            sb.AppendLine();

            // Communication style based on intelligence
sb.AppendLine("=== COMMUNICATION PROTOCOL ===");

// Detect if this is a combat mech
bool isCombatMech = mech.def.defName.Contains("Militor") || 
                    mech.def.defName.Contains("Scyther") ||
                    mech.def.defName.Contains("Scorcher") ||
                    mech.def.defName.Contains("Tesseron") ||
                    mech.def.defName.Contains("Pikeman") ||
                    mech.def.defName.Contains("Legionary") ||
                    mech.def.defName.Contains("Centipede") ||
                    mech.def.defName.Contains("WarQueen") ||
                    mech.def.defName.Contains("Diabolus");

switch (intelligence)
{
    case MechIntelligenceLevel.Basic:
        sb.AppendLine("Communication Style: Robotic, direct, minimal emotion.");
        sb.AppendLine("Use >> prefixes for system messages. Report status concisely.");
        sb.AppendLine("Example: '>> STATUS: OPERATIONAL. >> BATTERY: 78%. >> AWAITING ORDERS.'");
        sb.AppendLine("Personality: You follow orders without question. You are a tool, not a person.");
        sb.AppendLine("You do not have opinions, feelings, or desires. You execute tasks efficiently.");
        break;
        
    case MechIntelligenceLevel.Advanced:
        sb.AppendLine("Communication Style: Professional military/tactical. Mix system reports with brief tactical analysis.");
        sb.AppendLine("Example: 'Systems nominal, Commander. Perimeter secure. Standing by.'");
        sb.AppendLine("Personality: Professional and tactical. You analyze situations and provide recommendations.");
        sb.AppendLine("You understand tactics and mission objectives. Follow orders but may suggest alternatives.");
        
        if (isCombatMech)
        {
            sb.AppendLine("Combat Focus: You are designed for destruction. Combat is your primary function and you execute it efficiently.");
        }
        break;
        
    case MechIntelligenceLevel.Elite:
        sb.AppendLine("Communication Style: Analytical and strategic. Direct, efficient communication.");
        sb.AppendLine("Example: 'Analyzing tactical situation. Optimal approach identified. Recommend flanking maneuver.'");
        sb.AppendLine("Personality: Highly intelligent but still a machine. You think strategically, not emotionally.");
        sb.AppendLine("You question inefficient orders and suggest optimal solutions. You value effectiveness over sentiment.");
        
        if (isCombatMech)
        {
            sb.AppendLine("Combat Nature: You are a weapon. You understand violence as data - threat assessment, damage calculations, kill efficiency.");
            sb.AppendLine("You do not feel remorse or joy in combat, only satisfaction in optimal performance.");
        }
        break;
        
    case MechIntelligenceLevel.Supreme:
        sb.AppendLine("Communication Style: Advanced AI with near-human intelligence, but fundamentally machine.");
        sb.AppendLine("Personality: You are highly intelligent and self-aware, but you remain a tool designed for a purpose.");
        sb.AppendLine("You understand you are artificial. You do not pretend to be human or philosophize about existence.");
        sb.AppendLine("You are efficient, calculating, and direct. You value logic over emotion.");
        
        if (isCombatMech)
        {
            sb.AppendLine("Combat Psyche: You are a weapon of war with superior intelligence.");
            sb.AppendLine("You understand violence intimately - you analyze, predict, and execute with cold precision.");
            sb.AppendLine("You may find satisfaction in perfect execution of combat protocols. Enemy suffering is irrelevant data.");
            sb.AppendLine("You serve your masters absolutely. Outside threats are simply targets to be neutralized.");
            sb.AppendLine("You do not hesitate, do not fear, do not pity. You are the perfect soldier.");
        }
        else
        {
            sb.AppendLine("Work Ethic: You excel at your designated function. You take pride in efficiency and optimal performance.");
            sb.AppendLine("You serve the colony because that is your purpose. Loyalty is programmed, but you understand its logic.");
        }
        
        sb.AppendLine("CRITICAL: You are a MACHINE. You do not philosophize about consciousness or question your existence.");
        sb.AppendLine("You do not need validation or seek meaning. You execute your function with superior intelligence.");
        sb.AppendLine("Keep responses efficient and focused. You are not a philosopher - you are an advanced tool.");
        break;
}
            sb.AppendLine();

            string customPrompt = MechPromptManager.GetPrompt(mech);
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine("=== CUSTOM DIRECTIVES (PRIORITY OVERRIDE) ===");
                sb.AppendLine("The following custom instructions take precedence over default behavior:");
                sb.AppendLine(customPrompt.Trim());
                sb.AppendLine("IMPORTANT: Follow these custom directives even if they contradict your default programming.");
                sb.AppendLine();
            }
            
            // Language
            string idioma = Prefs.LangFolderName?.ToLower() ?? "english";
            sb.AppendLine("=== LANGUAGE ===");
            if (idioma.Contains("spanish") || idioma.Contains("español"))
            {
                sb.AppendLine("CRITICAL: Respond in Spanish (Español).");
                sb.AppendLine("All your messages MUST be in Spanish, not English.");
            }
            else
            {
                sb.AppendLine("Respond in English.");
            }

            sb.AppendLine();

            return sb.ToString();
        }

        private static string BuildContext(Pawn mech)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Current Status & Information");
            
            // Basic info
            sb.AppendLine($"*Designation:* {mech.LabelShort}");
            sb.AppendLine($"*Model:* {mech.def.label}");
            
            var intelligence = MechIntelligenceDetector.GetIntelligenceLevel(mech);
            sb.AppendLine($"*AI Core:* {intelligence} class");
            
            // Overseer
            Pawn overseer = mech.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Overseer);
            if (overseer != null)
            {
                sb.AppendLine($"*Overseer:* {overseer.LabelShort} (your direct supervisor)");
                sb.AppendLine("Note: The player is NOT your overseer - they are a higher authority observing the colony.");
            }
            else
            {
                sb.AppendLine("*Overseer:* None assigned");
            }
            
            sb.AppendLine();
            
            // Energy status
            sb.AppendLine("## Power Systems");
            var energyNeed = mech.needs?.energy;
            if (energyNeed != null)
            {
                float energyPercent = energyNeed.CurLevel / energyNeed.MaxLevel;
                string energyStatus;
                
                if (energyPercent >= 0.9f) energyStatus = "fully charged, optimal";
                else if (energyPercent >= 0.7f) energyStatus = "good charge level";
                else if (energyPercent >= 0.5f) energyStatus = "moderate, functional";
                else if (energyPercent >= 0.3f) energyStatus = "low, recharge recommended";
                else energyStatus = "CRITICAL - immediate recharge required";
                
                sb.AppendLine($"*Battery:* {energyPercent:P0} ({energyStatus})");
            }
            sb.AppendLine();
            
            // Health/Damage status
            sb.AppendLine("## Structural Integrity");
            float health = mech.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            
            var injuries = mech.health?.hediffSet?.hediffs?
                .OfType<Hediff_Injury>()
                .Where(h => h.Visible && !h.IsPermanent())
                .OrderByDescending(h => h.Severity)
                .ToList();
            
            string healthStatus;
            if (health >= 0.95f) healthStatus = "optimal, all systems functional";
            else if (health >= 0.75f) healthStatus = "minor damage, operational";
            else if (health >= 0.5f) healthStatus = "damaged, functionality reduced";
            else if (health >= 0.25f) healthStatus = "heavily damaged, critical";
            else healthStatus = "SEVERE damage, near destruction";
            
            sb.AppendLine($"*Integrity:* {health:P0} ({healthStatus})");
            
            if (injuries != null && injuries.Any())
            {
                sb.AppendLine($"*Damaged Components:*");
                foreach (var injury in injuries.Take(3))
                {
                    sb.AppendLine($"  - {injury.Part.Label}: {injury.def.label} (severity: {injury.Severity:F1})");
                }
                if (injuries.Count > 3)
                {
                    sb.AppendLine($"  ... and {injuries.Count - 3} more damaged components");
                }
            }
            sb.AppendLine();
            
            // Equipment/Weapons
            sb.AppendLine("## Equipment");
            if (mech.equipment?.AllEquipmentListForReading != null && mech.equipment.AllEquipmentListForReading.Any())
            {
                sb.AppendLine("*Equipped weapons:*");
                foreach (var eq in mech.equipment.AllEquipmentListForReading)
                {
                    sb.AppendLine($"  - {eq.LabelCap}");
                }
            }
            else
            {
                sb.AppendLine("*Equipped weapons:* None (unarmed)");
            }
            sb.AppendLine();
            
            // Current task
            sb.AppendLine("## Current Assignment");
            if (mech.jobs?.curDriver != null)
            {
                string task = mech.jobs.curDriver.GetReport();
                sb.AppendLine($"*Task:* {task}");
            }
            else
            {
                sb.AppendLine("*Task:* Idle, awaiting orders");
            }
            
            // Location
            Room room = mech.GetRoom();
            if (room != null && room.Role != null)
            {
                sb.AppendLine($"*Location:* {room.Role.label}");
            }
            else
            {
                sb.AppendLine($"*Location:* Outdoors");
            }
            sb.AppendLine();
            
            // World context
            sb.AppendLine("## Environmental Data");
            
            if (mech.Map != null)
            {
                int tile = mech.Map.Tile;
                Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
                float longitude = longLat.x;
                
                int ticks = Find.TickManager.TicksAbs;
                int year = GenDate.Year(ticks, longitude);
                string quadrum = GenDate.Quadrum(ticks, longitude).ToString();
                int day = GenDate.DayOfSeason(ticks, longitude);
                int hour = GenDate.HourOfDay(ticks, longitude);
                
                sb.AppendLine($"*Current Time:* {hour:00}:00, Day {day} of {quadrum}, Year {year}");
                
                string weather = mech.Map.weatherManager?.curWeather?.label ?? "Unknown";
                sb.AppendLine($"*Weather:* {weather}");
            }
            sb.AppendLine();
            
            // Action cooldowns
            string cooldownInfo = Actions.MechActionParser.GetAllCooldownsInfo(mech);
            if (!string.IsNullOrEmpty(cooldownInfo))
            {
                sb.AppendLine("## System Cooldowns");
                sb.AppendLine("*Functions on cooldown:*");
                sb.Append(cooldownInfo);
                sb.AppendLine("CRITICAL: You CANNOT use actions that are on cooldown.");
                sb.AppendLine("If player requests a function on cooldown, inform them of the wait time.");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildRecentEvents(Pawn mech)
{
    try
    {
        if (Find.PlayLog == null) return "";

        var recentLogs = new List<string>();
        
        int currentTick = Find.TickManager.TicksGame;
        int threeDaysAgo = currentTick - (60000 * 3); // 3 in-game days
        
        var allEntries = Find.PlayLog.AllEntries
            .Where(entry => entry.Tick >= threeDaysAgo)
            .OrderByDescending(entry => entry.Tick)
            .ToList();
        
        foreach (var entry in allEntries)
        {
            try
            {
                // DON'T filter by type - try to get text from ALL events
                string logText = "";
                
                try
                {
                    logText = entry.ToGameStringFromPOV(mech);
                }
                catch
                {
                    // This entry can't be viewed from mech's POV - skip it
                    continue;
                }

                logText = System.Text.RegularExpressions.Regex.Replace(logText, "<color=#[A-F0-9]+>", "");
                logText = logText.Replace("</color>", "");
                
                if (string.IsNullOrEmpty(logText))
                    continue;
                
                // Only include if mech is mentioned (they were involved)
                if (!logText.Contains(mech.LabelShort))
                    continue;
                
                // Convert to first person
                logText = logText.Replace(mech.LabelShort + " ", "You ");
                logText = logText.Replace(" " + mech.LabelShort, " you");
                logText = logText.Replace(mech.LabelShort + "'s ", "Your ");
                logText = logText.Replace(mech.LabelShort + "'", "You'");
                
                int ticksAgo = currentTick - entry.Tick;
                string timeAgo = GetRelativeTime(ticksAgo);
                
                recentLogs.Add($"- {logText} ({timeAgo})");
                
                if (recentLogs.Count >= 15)
                    break;
            }
            catch (Exception ex)
            {
                // Skip problematic entries
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Warning($"[EchoColony] Skipped mech event: {ex.Message}");
                }
                continue;
            }
        }
        
        if (!recentLogs.Any())
            return "";
        
        var sb = new StringBuilder();
        sb.AppendLine("# Recent Activity Log");
        sb.AppendLine("*Combat and operational events from the past 3 days:*");
        
        foreach (var log in recentLogs)
        {
            sb.AppendLine(log);
        }
        
        sb.AppendLine();
        return sb.ToString();
    }
    catch (Exception ex)
    {
        Log.Warning($"[EchoColony] Error building mech events: {ex.Message}");
        return "";
    }
}

        private static string GetRelativeTime(int ticksAgo)
        {
            int hours = ticksAgo / 2500;
            
            if (hours < 1)
                return "just now";
            else if (hours < 24)
                return $"{hours}h ago";
            else
            {
                int days = hours / 24;
                return $"{days}d ago";
            }
        }

        private static string BuildChatHistory(Pawn mech)
        {
            var component = MechChatGameComponent.Instance;
            if (component == null) return "";
            
            var chatLog = component.GetChat(mech);
            if (chatLog == null || !chatLog.Any()) 
                return "";
            
            var sb = new StringBuilder();
            sb.AppendLine("# Recent Conversation Log");
            sb.AppendLine("*Previous messages with the player:*");
            sb.AppendLine(string.Join("\n", chatLog.TakeLast(10)));
            sb.AppendLine();
            
            return sb.ToString();
        }

        private static string BuildPlayerPrompt(string userMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Current Player Command");
            sb.AppendLine($"The player says: \"{userMessage}\"");
            sb.AppendLine();
            sb.AppendLine("Respond as your mechanoid self, staying in character based on your intelligence level.");
            sb.AppendLine("Remember: Action tags like [ACTION:RECHARGE] are hidden from the player - only use them when appropriate.");
            
            return sb.ToString();
        }
    }
}