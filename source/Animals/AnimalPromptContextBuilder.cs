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
        // Helper to safely get wildness value
        private static float GetWildness(Pawn animal)
        {
            try
            {
                if (animal?.def?.race != null)
                {
                    // Access via reflection since wildness might not be public
                    var wildnessField = typeof(RaceProperties).GetField("wildness", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance);
                    
                    if (wildnessField != null)
                    {
                        var value = wildnessField.GetValue(animal.def.race);
                        if (value is float f)
                            return f;
                    }
                }
                
                // If we can't get it, use a reasonable default
                // based on whether the animal is domesticated
                if (animal.playerSettings != null)
                    return 0.25f; // Colony animals tend to be more docile
                
                return 0.5f;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Could not get wildness for {animal?.LabelShort}: {ex.Message}");
                return 0.5f;
            }
        }

        public static string Build(Pawn animal, string userMessage)
{
    if (animal == null) return string.Empty;

    var sb = new StringBuilder();

    sb.AppendLine(BuildSystemPrompt(animal));
    
    // NUEVO: Agregar prompt global
    string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
    if (!string.IsNullOrWhiteSpace(globalPrompt))
    {
        sb.AppendLine("# Global Instructions");
        sb.AppendLine(globalPrompt.Trim());
        sb.AppendLine();
    }
    
    sb.AppendLine(BuildCustomPrompt(animal));
    
    // Include available divine actions
    if (MyMod.Settings.enableDivineActions)
    {
        string actionsPrompt = Actions.AnimalActionRegistry.GetAvailableActionsPrompt();
        if (!string.IsNullOrEmpty(actionsPrompt))
        {
            sb.AppendLine(actionsPrompt);
        }
    }
    
    sb.AppendLine(BuildContext(animal));
    sb.AppendLine(BuildChatHistory(animal));
    sb.AppendLine(BuildPlayerPrompt(userMessage));

    return sb.ToString();
}
        private static string BuildSystemPrompt(Pawn animal)
{
    var sb = new StringBuilder();

    string species = animal.KindLabel;
    string name = animal.LabelShort;
    float wildness = GetWildness(animal);
    
    // Determine intelligence level
    string intelligenceLevel = GetIntelligenceLevel(animal);
    string communicationStyle = GetCommunicationStyle(animal);

    sb.AppendLine($"You are {name}, a {species}.");
    sb.AppendLine($"Intelligence: {intelligenceLevel}");
    sb.AppendLine($"Communication: {communicationStyle}");
    
    // NUEVO: Clarificar quién es el jugador
    Pawn master = animal.playerSettings?.Master;
    Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
    
    if (master != null || bondedTo != null)
    {
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: You are talking to 'the player' - a god-like observer who watches over the colony.");
        
        if (bondedTo != null)
        {
            sb.AppendLine($"Your bonded companion is {bondedTo.LabelShort}, NOT the player you're talking to.");
        }
        if (master != null && master != bondedTo)
        {
            sb.AppendLine($"Your master/trainer is {master.LabelShort}, NOT the player you're talking to.");
        }
        
        sb.AppendLine("The player is separate from the colonists. Don't confuse them with your master or bonded companion.");
    }
    
    // Narrative style
    string narrativeInstruction = GetNarrativeStyleInstruction(animal);
    sb.AppendLine(narrativeInstruction);
    
    // Wildness affects behavior
    if (wildness >= 0.75f)
    {
        sb.AppendLine("Personality: Wild and independent, cautious around humans");
    }
    else if (wildness >= 0.5f)
    {
        sb.AppendLine("Personality: Semi-domesticated, learning to trust humans");
    }
    else if (wildness >= 0.25f)
    {
        sb.AppendLine("Personality: Friendly and domesticated, comfortable with humans");
    }
    else
    {
        sb.AppendLine("Personality: Completely tame and affectionate, sees humans as family");
    }

    sb.AppendLine();
    
    // Language
    string lang = Prefs.LangFolderName?.ToLower() ?? "english";
    sb.AppendLine("=== LANGUAGE ===");
    if (lang != "english") sb.AppendLine($"*Language:* {lang}");

    return sb.ToString();
}
        private static string GetNarrativeStyleInstruction(Pawn animal)
        {
            // Check custom prompt first
            string customPrompt = AnimalPromptManager.GetPrompt(animal);
            if (!string.IsNullOrEmpty(customPrompt))
            {
                if (customPrompt.ToLower().Contains("first person") || 
                    customPrompt.ToLower().Contains("i "))
                {
                    return "Narrative: Describe your actions in first person (I run, I eat, I wag my tail)";
                }
                if (customPrompt.ToLower().Contains("third person"))
                {
                    return $"Narrative: Describe your actions in third person ({animal.LabelShort} runs, {animal.LabelShort} eats)";
                }
            }
            
            // Use global setting
            if (MyMod.Settings.defaultAnimalNarrativeStyle == AnimalNarrativeStyle.FirstPerson)
            {
                return "Narrative: Describe your actions in first person (I run, I eat, I wag my tail)";
            }
            else
            {
                return $"Narrative: Describe your actions in third person ({animal.LabelShort} runs, {animal.LabelShort} eats)";
            }
        }

        private static string GetIntelligenceLevel(Pawn animal)
        {
            // Special intelligent creatures
            if (animal.def.defName.Contains("Thrumbo") || 
                animal.def.defName.Contains("Megasloth"))
            {
                return "Highly intelligent, capable of understanding complex human speech";
            }
            
            // Predators and trained animals
            if (animal.RaceProps.predator || 
                animal.training?.HasLearned(TrainableDefOf.Obedience) == true)
            {
                return "Smart and perceptive, understands simple commands and emotions";
            }
            
            // Farm/pack animals
            if (animal.RaceProps.packAnimal || animal.RaceProps.herdAnimal)
            {
                return "Basic animal intelligence, responds to simple cues and tone";
            }
            
            // Simple creatures
            return "Simple animal mind, communicates through sounds and body language";
        }

        private static string GetCommunicationStyle(Pawn animal)
        {
            var sb = new StringBuilder();

            // Check if user wants human-like speech
            string customPrompt = AnimalPromptManager.GetPrompt(animal);
            if (!string.IsNullOrEmpty(customPrompt) && 
                (customPrompt.ToLower().Contains("speak") || 
                 customPrompt.ToLower().Contains("talk") ||
                 customPrompt.ToLower().Contains("language")))
            {
                return "You can speak and understand human language fluently";
            }

            // Intelligent creatures can form simple thoughts
            if (animal.def.defName.Contains("Thrumbo") || 
                animal.def.defName.Contains("Megasloth"))
            {
                return "Express thoughts through simple words, animal sounds, and body language. Format: *actions* and brief words.";
            }

            // Trained/smart animals
            if (animal.training?.HasLearned(TrainableDefOf.Obedience) == true ||
                animal.RaceProps.predator)
            {
                return "Communicate through characteristic sounds, whines, barks, growls. Add *body language* in italics. Very limited words if any.";
            }

            // Simple animals
            string soundType = GetAnimalSoundType(animal);
            return $"Express yourself through {soundType} and *body language in italics*. No human words.";
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

        private static string GetLanguageName(string langCode)
        {
            switch (langCode)
            {
                case "spanish":
                case "spanishlatin":
                    return "Spanish";
                case "french":
                    return "French";
                case "german":
                    return "German";
                case "polish":
                    return "Polish";
                case "russian":
                    return "Russian";
                case "italian":
                    return "Italian";
                case "portuguese":
                case "portuguesebrazilian":
                    return "Portuguese";
                case "dutch":
                    return "Dutch";
                case "japanese":
                    return "Japanese";
                case "korean":
                    return "Korean";
                case "chinesetraditional":
                case "chinesesimplified":
                    return "Chinese";
                case "turkish":
                    return "Turkish";
                case "czech":
                    return "Czech";
                case "danish":
                    return "Danish";
                case "finnish":
                    return "Finnish";
                case "norwegian":
                    return "Norwegian";
                case "swedish":
                    return "Swedish";
                case "hungarian":
                    return "Hungarian";
                case "romanian":
                    return "Romanian";
                case "ukrainian":
                    return "Ukrainian";
                default:
                    return "English";
            }
        }

        private static string BuildCustomPrompt(Pawn animal)
        {
            string customPrompt = AnimalPromptManager.GetPrompt(animal);
            
            if (string.IsNullOrEmpty(customPrompt))
                return "";

            return $"# Custom Personality Instructions:\n{customPrompt}\n";
        }

        private static string BuildContext(Pawn animal)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Current Status");
            
            // Basic info
            sb.AppendLine($"*Age:* {animal.ageTracker.AgeBiologicalYears} years");
            sb.AppendLine($"*Gender:* {animal.gender}");
            
            // Bonding
            Pawn bondedTo = animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedTo != null)
            {
                sb.AppendLine($"*Bonded to:* {bondedTo.LabelShort} - deep emotional connection");
            }
            
            // Master/trainer
            Pawn master = animal.playerSettings?.Master;
            if (master != null && master != bondedTo)
            {
                sb.AppendLine($"*Master:* {master.LabelShort} - responds to their commands");
            }
            
            // Training
            sb.AppendLine(BuildTrainingInfo(animal));
            
            // Health
            sb.AppendLine(BuildHealthInfo(animal));
            
            // Needs
            sb.AppendLine(BuildNeedsInfo(animal));
            
            // Location
            sb.AppendLine(BuildLocationInfo(animal));
            
            // Pregnancy/offspring
            sb.AppendLine(BuildReproductionInfo(animal));
            
            // Recent interactions
            string interactions = BuildRecentInteractions(animal);
            if (!string.IsNullOrEmpty(interactions))
            {
                sb.AppendLine();
                sb.AppendLine(interactions);
            }

            return sb.ToString();
        }

        private static string BuildTrainingInfo(Pawn animal)
        {
            if (animal.training == null) 
                return "*Training:* None";

            var learned = new List<string>();
            
            if (animal.training.HasLearned(TrainableDefOf.Obedience))
                learned.Add("Obedience");
            if (animal.training.HasLearned(TrainableDefOf.Release))
                learned.Add("Release");
            if (animal.training.HasLearned(TrainableDefOf.Tameness))
                learned.Add("Tameness");
            
            // Check other trainables
            var allTrainables = DefDatabase<TrainableDef>.AllDefsListForReading;
            foreach (var trainable in allTrainables)
            {
                if (animal.training.HasLearned(trainable) && 
                    !learned.Contains(trainable.label))
                {
                    learned.Add(trainable.label.CapitalizeFirst());
                }
            }

            if (learned.Any())
            {
                return "*Training:* " + string.Join(", ", learned);
            }
            else
            {
                float wildness = GetWildness(animal);
                if (wildness >= 0.75f)
                    return "*Training:* Wild and untrained";
                else
                    return "*Training:* Learning from humans";
            }
        }

        private static string BuildHealthInfo(Pawn animal)
        {
            if (animal.health?.hediffSet == null) 
                return "*Health:* Unknown";
            
            float healthPercent = animal.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            var injuries = animal.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => h.Visible)
                .ToList();
            
            string healthStatus;
            if (healthPercent >= 0.95f)
                healthStatus = "Perfectly healthy";
            else if (healthPercent >= 0.75f)
                healthStatus = "Minor injuries";
            else if (healthPercent >= 0.5f)
                healthStatus = "Hurt and in pain";
            else
                healthStatus = "Badly wounded";
            
            var details = new List<string>();
            if (injuries.Any())
            {
                details.Add($"{injuries.Count} injuries");
            }
            
            if (animal.health.hediffSet.BleedRateTotal > 0.1f)
            {
                details.Add("bleeding");
            }
            
            string detailsText = details.Any() ? " (" + string.Join(", ", details) + ")" : "";
            
            return $"*Health:* {healthStatus}{detailsText}";
        }

        private static string BuildNeedsInfo(Pawn animal)
        {
            var needs = new List<string>();
            
            // Food
            var foodNeed = animal.needs?.food;
            if (foodNeed != null)
            {
                float foodLevel = foodNeed.CurLevel;
                if (foodLevel < 0.2f)
                    needs.Add("starving");
                else if (foodLevel < 0.4f)
                    needs.Add("very hungry");
                else if (foodLevel < 0.6f)
                    needs.Add("hungry");
            }
            
            // Rest
            var restNeed = animal.needs?.rest;
            if (restNeed != null)
            {
                float restLevel = restNeed.CurLevel;
                if (restLevel < 0.2f)
                    needs.Add("exhausted");
                else if (restLevel < 0.4f)
                    needs.Add("tired");
            }
            
            // Mood (if animal has it)
            var moodNeed = animal.needs?.mood;
            if (moodNeed != null)
            {
                float moodLevel = moodNeed.CurLevel;
                if (moodLevel < 0.3f)
                    needs.Add("miserable");
                else if (moodLevel < 0.5f)
                    needs.Add("unhappy");
                else if (moodLevel > 0.8f)
                    needs.Add("content");
            }
            
            return needs.Any() 
                ? "*Current needs:* " + string.Join(", ", needs)
                : "*Current state:* Comfortable";
        }

        private static string BuildLocationInfo(Pawn animal)
        {
            var room = animal.GetRoom();
            string location = room?.Role?.label ?? "outside";
            
            // Check if in pen/zone
            var assignedZone = animal.playerSettings?.AreaRestrictionInPawnCurrentMap;
            string zoneInfo = assignedZone != null ? $" (restricted to {assignedZone.Label})" : "";
            
            return $"*Location:* {location}{zoneInfo}";
        }

        private static string BuildReproductionInfo(Pawn animal)
        {
            var sb = new StringBuilder();
            
            // Check pregnancy
            var pregnancyHediff = animal.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            if (pregnancyHediff != null)
            {
                float progress = pregnancyHediff.Severity;
                sb.AppendLine($"*Condition:* Pregnant ({(progress * 100):F0}% progress)");
            }
            
            // Check for offspring nearby
            if (animal.relations != null)
            {
                var children = animal.relations.Children;
                if (children != null && children.Any())
                {
                    var nearbyChildren = children.Where(c => 
                        c != null && 
                        !c.Dead && 
                        c.Map == animal.Map &&
                        c.Position.InHorDistOf(animal.Position, 20f)
                    ).Take(3).ToList();
                    
                    if (nearbyChildren.Any())
                    {
                        string childNames = string.Join(", ", nearbyChildren.Select(c => c.LabelShort));
                        sb.AppendLine($"*Offspring nearby:* {childNames}");
                    }
                }
            }
            
            return sb.ToString();
        }

        private static string BuildRecentInteractions(Pawn animal)
{
    try
    {
        if (Find.PlayLog == null) return "";

        var recentLogs = new List<string>();
        
        // Get logs from the last 3 in-game days
        int currentTick = Find.TickManager.TicksGame;
        int threeDaysAgo = currentTick - (60000 * 3); // 3 days in ticks
        
        var allEntries = Find.PlayLog.AllEntries
            .Where(entry => entry.Tick >= threeDaysAgo)
            .OrderByDescending(entry => entry.Tick)
            .ToList();
        
        // Filter logs that involve this animal
        foreach (var entry in allEntries)
        {
            try
            {
                // Check if this is an interaction log
                if (entry is PlayLogEntry_Interaction interaction)
                {
                    // Use reflection to get initiator and recipient
                    var initiatorField = entry.GetType().GetField("initiator", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    var recipientField = entry.GetType().GetField("recipient", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    
                    var initiator = initiatorField?.GetValue(entry) as Pawn;
                    var recipient = recipientField?.GetValue(entry) as Pawn;
                    
                    // Only include if animal is involved
                    if (initiator != animal && recipient != animal)
                        continue;
                }
                
                // Try to get the log text
                string logText = entry.ToGameStringFromPOV(animal);
                
                if (string.IsNullOrEmpty(logText))
                    continue;
                
                // Clean up the text
                logText = logText.Replace(animal.LabelShort + " ", "You ");
                logText = logText.Replace(" " + animal.LabelShort, " you");
                
                // Get relative time
                int ticksAgo = currentTick - entry.Tick;
                string timeAgo = GetRelativeTime(ticksAgo);
                
                recentLogs.Add($"- {logText} ({timeAgo})");
                
                if (recentLogs.Count >= 10)
                    break;
            }
            catch
            {
                // Skip entries that can't be displayed from this POV
                continue;
            }
        }
        
        if (!recentLogs.Any())
            return "";
        
        var sb = new StringBuilder();
        sb.AppendLine("# Recent Interactions");
        
        foreach (var log in recentLogs)
        {
            sb.AppendLine(log);
        }
        
        return sb.ToString();
    }
    catch (Exception ex)
    {
        Log.Warning($"[EchoColony] Error building animal interactions: {ex.Message}");
        return "";
    }
}
        private static string GetRelativeTime(int ticksAgo)
        {
            int hours = ticksAgo / 2500; // Approximate hours
            
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

        private static string BuildChatHistory(Pawn animal)
        {
            var component = AnimalChatGameComponent.Instance;
            if (component == null) return "";
            
            var chatLog = component.GetChat(animal);
            if (chatLog == null || !chatLog.Any()) 
                return "";
            
            return "*Recent conversation:*\n" + string.Join("\n", chatLog.TakeLast(10));
        }

        private static string BuildPlayerPrompt(string userMessage)
        {
            return $"\nHuman says: \"{userMessage}\"";
        }
    }
}