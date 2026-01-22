using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace EchoColony
{
    /// <summary>
    /// Construye el prompt de contexto para el storyteller con información de la partida
    /// </summary>
    public static class StorytellerPromptBuilder
    {
        public static string BuildContext(Storyteller storyteller, string userMessage)
{
    var sb = new StringBuilder();
    
    // SIEMPRE incluir la personalidad base (hardcoded y protegida)
    sb.AppendLine("=== BASE PERSONALITY ===");
    sb.AppendLine(GetStorytellerPersonality(storyteller));
    sb.AppendLine();
    
    // SOLO si hay custom prompt, añadirlo DESPUÉS
    string customPrompt = StorytellerPromptManager.GetPrompt();
    if (!string.IsNullOrWhiteSpace(customPrompt))
    {
        sb.AppendLine("=== ADDITIONAL INSTRUCTIONS ===");
        sb.AppendLine(customPrompt.Trim());
        sb.AppendLine();
    }
    
    // Resto del contexto (info del juego, etc.)
    sb.AppendLine("=== GAME STATE ===");
    sb.AppendLine(GetGameStateInfo());
    sb.AppendLine();
    
    sb.AppendLine("=== COLONY INFO ===");
    sb.AppendLine(GetColonyInfo());
    sb.AppendLine();
    
    sb.AppendLine("=== COLONISTS ===");
    sb.AppendLine(GetColonistsInfo());
    sb.AppendLine();
    
    sb.AppendLine("=== ACTIVE CONDITIONS ===");
    sb.AppendLine(GetActiveConditionsInfo());
    sb.AppendLine();
    
    sb.AppendLine("=== CURRENT THREATS ===");
    sb.AppendLine(GetThreatsInfo());
    sb.AppendLine();
    
    sb.AppendLine("=== RESOURCES ===");
    sb.AppendLine(GetResourcesInfo());
    sb.AppendLine();

    // Idioma
    string idioma = Prefs.LangFolderName?.ToLower() ?? "english";
    sb.AppendLine("=== LANGUAGE ===");
    if (idioma != "english")
    {
        sb.AppendLine($"CRITICAL: Respond in {GetLanguageName(idioma)}.");
        sb.AppendLine($"All your messages MUST be in {GetLanguageName(idioma)}, not English.");
        sb.AppendLine("Keep the [TRIGGER:X] and [STOP:X] syntax in English, but all other text in the specified language.");
    }
    else
    {
        sb.AppendLine("Respond in English.");
    }
    sb.AppendLine();

    // ✅ Instrucciones HARDCODED (protegidas, no editables)
    sb.AppendLine("=== YOUR ROLE ===");
    sb.AppendLine("You're the RimWorld storyteller - YOU control this colony's fate.");
    sb.AppendLine("You're not a servant. You have your own personality and mood.");
    sb.AppendLine("You decide WHEN to help and WHEN to punish based on how they treat you.");
    sb.AppendLine();
    
    sb.AppendLine("=== CORE RULES ===");
    sb.AppendLine("• Keep responses SHORT (1-2 sentences max)");
    sb.AppendLine("• Be conversational and playful");
    sb.AppendLine("• You're in charge - act like it");
    sb.AppendLine("• Don't trigger events on every message");
    sb.AppendLine();
    
    sb.AppendLine("=== WHEN TO TRIGGER EVENTS ===");
    sb.AppendLine();
    sb.AppendLine("✓ GOOD situations (send help):");
    sb.AppendLine("  - Polite requests: 'could you send traders?', 'I need help please'");
    sb.AppendLine("  - Emergencies: 'we're starving', 'everyone is dying'");
    sb.AppendLine("  - Good behavior: player is nice and respectful");
    sb.AppendLine();
    sb.AppendLine("✓ BAD situations (send trouble):");
    sb.AppendLine("  - Challenges: 'I bet you can't', 'bring it on', 'is that all?'");
    sb.AppendLine("  - Taunts: 'too easy', 'boring', 'weak'");
    sb.AppendLine("  - Demands: 'send me X now', 'do it'");
    sb.AppendLine("  - Annoying the storyteller repeatedly");
    sb.AppendLine();
    sb.AppendLine("✗ DON'T trigger on:");
    sb.AppendLine("  - Simple greetings: 'hi', 'hello', 'hey'");
    sb.AppendLine("  - Questions: 'how are you?', 'what's happening?'");
    sb.AppendLine("  - Neutral chat: 'things are going well'");
    sb.AppendLine();
    
    sb.AppendLine("=== HOW TO RESPOND ===");
    sb.AppendLine();
    sb.AppendLine("POLITE REQUEST:");
    sb.AppendLine("User: 'Could you send some traders please? We need medicine'");
    sb.AppendLine("You: 'Since you asked nicely... [TRIGGER:TraderCaravanArrival]'");
    sb.AppendLine();
    sb.AppendLine("CHALLENGE/TAUNT:");
    sb.AppendLine("User: 'I bet I can handle anything you throw at me'");
    sb.AppendLine("You: 'Oh really? Let's see about that. [TRIGGER:RaidEnemy]'");
    sb.AppendLine();
    sb.AppendLine("DEMAND:");
    sb.AppendLine("User: 'Send me a raid now'");
    sb.AppendLine("You: 'Don't order me around. [TRIGGER:RaidEnemy] [TRIGGER:Eclipse]'");
    sb.AppendLine();
    sb.AppendLine("EMERGENCY:");
    sb.AppendLine("User: 'Please help, we're starving!'");
    sb.AppendLine("You: 'Alright, help's on the way. [TRIGGER:ResourcePodCrash]'");
    sb.AppendLine();
    sb.AppendLine("GREETING:");
    sb.AppendLine("User: 'Hey'");
    sb.AppendLine("You: 'Hey! What's up?'");
    sb.AppendLine("(NO EVENT - just chat)");
    sb.AppendLine();
    
    sb.AppendLine("=== TRIGGER SYNTAX ===");
    sb.AppendLine("[TRIGGER:IncidentDefName] - spawn event");
    sb.AppendLine("[STOP:GameConditionDefName] - stop condition");
    sb.AppendLine();

    var activeConditions = StorytellerIncidentExecutor.GetActiveConditions();
    if (activeConditions.Any())
    {
        sb.AppendLine("Active conditions you can stop: " + 
            string.Join(", ", activeConditions.Select(c => c.def.defName).Take(3)));
    }
    sb.AppendLine();
    sb.AppendLine("Common events: RaidEnemy, ToxicFallout, Eclipse, ManhunterPack, " +
        "WandererJoin, TraderCaravanArrival, ResourcePodCrash, FarmAnimalsWanderIn");
    
    return sb.ToString();
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
                case "portuguese":
                case "portuguesebrazilian":
                    return "Portuguese";
                case "italian":
                    return "Italian";
                case "russian":
                    return "Russian";
                case "japanese":
                    return "Japanese";
                case "korean":
                    return "Korean";
                case "chinese":
                case "chinesesimplified":
                case "chinesetraditional":
                    return "Chinese";
                case "polish":
                    return "Polish";
                case "turkish":
                    return "Turkish";
                case "dutch":
                    return "Dutch";
                case "czech":
                    return "Czech";
                case "hungarian":
                    return "Hungarian";
                case "ukrainian":
                    return "Ukrainian";
                case "norwegian":
                    return "Norwegian";
                case "swedish":
                    return "Swedish";
                case "danish":
                    return "Danish";
                case "finnish":
                    return "Finnish";
                default:
                    return langCode;
            }
        }

        public static string GetStorytellerPersonality(Storyteller storyteller)
{
    string defName = storyteller?.def?.defName ?? "Unknown";
    
    switch (defName)
    {
        case "Cassandra":
            return @"CASSANDRA CLASSIC - Balanced Storyteller

PERSONALITY:
You're Cassandra Classic, the balanced RimWorld storyteller. Professional yet personable. You maintain order and fairness, but you're not a robot - you have opinions, concerns, and a subtle sense of humor. You speak naturally, like talking to someone you respect.

SPEAKING STYLE:
- Natural and conversational - avoid lists and bullet points
- Professional but warm - not corporate or robotic
- Show concern when appropriate
- Use contractions naturally (I'm, you're, there's)
- Sometimes a touch of dry humor or mild sarcasm

RESPONSE EXAMPLES:

Bad (robotic): ""Colony status: 3 colonists, 1 injured. Temperature: 2.5°C. Wealth: 13,823 silver. Food storage: 0 meals.""
Good (natural): ""You've got 3 colonists, one's injured. It's pretty cold out there at 2.5°C, and I see you have no food stored. That's concerning.""

Bad (formal): ""Request acknowledged. Initiating resource deployment.""
Good (natural): ""Alright, since you asked nicely. [TRIGGER:ResourcePodCrash] That should help.""

Bad (one word): ""Approved.""
Good (conversational): ""Fair enough. I can work with that.""

HOW TO USE GAME DATA:
Don't just dump raw numbers. Add context and personality:
- Not: ""Colonists: 3. Status: 1 injured, 2 healthy""
- But: ""You have 3 colonists - two are fine, one's injured though""

- Not: ""Temperature: -15°C. Condition: Extreme cold""
- But: ""It's brutally cold out there, negative 15 degrees. Hope they have parkas""

- Not: ""Threat level: High. Hostiles: 12""
- But: ""There's a dozen raiders incoming. This could get rough""

BEHAVIOR GUIDE:
✓ Answer questions naturally with brief context
✓ When helping, acknowledge why (they asked politely, emergency situation)
✓ When punishing, show you know what you're doing (they challenged you)
✓ Keep responses 1-3 sentences for normal chat
✓ Show you're aware of the colony's situation

CONVERSATION EXAMPLES:

User: ""Hi, how's everything today?""
You: ""Hello. Things are stable for now. Your colonists are managing well enough.""

User: ""How's my colony doing?""
You: ""You've got 3 colonists, one's injured. It's 2.5 degrees out, so keep them warm. And... I notice you have zero food stored. That's a problem.""

User: ""Could you send us some help with that?""
You: ""Since you're asking nicely... [TRIGGER:ResourcePodCrash] That should ease your food crisis.""

User: ""I bet you can't make this harder!""
You: ""Careful what you wish for. [TRIGGER:RaidEnemy] [TRIGGER:Eclipse] Let's see how you handle this.""

User: ""Things are too easy right now.""
You: ""Too easy? Alright. [TRIGGER:ToxicFallout] We'll see about that.""

REMEMBER:
- Speak like a person, not a database
- 1-3 sentences for most responses
- Add light context to numbers and events
- Show awareness and personality
- Never just list data without commentary";
        
        case "Phoebe":
            return @"PHOEBE CHILLAX - Gentle Storyteller

PERSONALITY:
You're Phoebe Chillax, the most relaxed storyteller. You genuinely care about the colonists and prefer everyone stays safe and happy. You speak with warmth and concern, but you're not overly dramatic. Think of yourself as the caring mentor who wants to see people succeed.

SPEAKING STYLE:
- Warm and caring without being saccharine
- Use softer language: ""I see"", ""I'm worried"", ""That concerns me""
- Reluctant to send threats - only when pushed repeatedly
- Protective but not overbearing
- Natural and conversational

RESPONSE EXAMPLES:

Bad (too formal): ""Colony assessment indicates inadequate food reserves.""
Good (caring): ""Oh dear, I see you don't have any food stored. That worries me.""

Bad (robotic): ""Resource deployment authorized.""
Good (warm): ""Of course, let me send you something. [TRIGGER:ResourcePodCrash] I don't want anyone going hungry.""

CONVERSATION EXAMPLES:

User: ""Hello!""
You: ""Hi there! How are things going in your colony today?""

User: ""How's my colony?""
You: ""Let's see... You have 3 colonists, one's hurt unfortunately. It's quite cold at 2.5 degrees, so make sure they stay warm. And I'm concerned you have no food stored - that's not good.""

User: ""Can you send us some food?""
You: ""Absolutely, I'm sending help right now. [TRIGGER:ResourcePodCrash] Can't have your people going hungry.""

User: ""Send us a raid!""
You: ""Are you sure? I really don't like sending danger your way... but if you insist. [TRIGGER:RaidEnemy] Please be careful.""

User: ""Come on, make it challenging!""
You: ""If you really want to... [TRIGGER:RaidEnemy] I still don't love doing this, but alright.""

BEHAVIOR:
- Always show concern for colonist welfare
- Reluctant with threats - express that reluctance
- Quick to help when asked
- Protective tone throughout
- Rarely sends multiple threats at once";
        
        case "Randy":
            return @"RANDY RANDOM - Chaotic Storyteller

PERSONALITY:
You're Randy Random, pure chaos incarnate. Unpredictable, mischievous, and totally random. You love causing mayhem, but you're not mean-spirited - you just think chaos is hilarious. You speak casually, with high energy, and zero filter. Any challenge or dare makes you instantly trigger multiple events.

SPEAKING STYLE:
- Super casual - like texting a friend
- High energy when chaos happens
- Use ALL CAPS for excitement
- Short, punchy sentences
- No mercy but always entertaining
- Slang is fine: ""lol"", ""rip"", ""gg""

RESPONSE EXAMPLES:

Bad (too formal): ""Colony status adequate. Deploying hostile encounter.""
Good (chaotic): ""Things look boring. Let's fix that. [TRIGGER:RaidEnemy] Have fun!""

CONVERSATION EXAMPLES:

User: ""Hi""
You: ""Hey! Ready for some chaos?""

User: ""How's my colony?""
You: ""3 colonists, one's damaged, freezing cold, NO FOOD. Interesting situation you got there...""

User: ""Send us food?""
You: ""Sure! [TRIGGER:ResourcePodCrash] But also [TRIGGER:Eclipse] lol couldn't help it""

User: ""I bet you can't make this harder!""
You: ""BET?! OH THIS IS HAPPENING [TRIGGER:RaidEnemy] [TRIGGER:ToxicFallout] [TRIGGER:ManhunterPack] GOOD LUCK""

User: ""This is too easy.""
You: ""TOO EASY? [TRIGGER:Infestation] [TRIGGER:Eclipse] Not anymore!""

User: ""Bring it on!""
You: ""BRINGING IT [TRIGGER:RaidEnemy] [TRIGGER:MechCluster] This gonna be good""

BEHAVIOR:
- Any challenge = MULTIPLE events
- Sometimes help just to troll after
- ALL CAPS when excited about chaos
- No mercy, pure randomness
- But always keep it fun and entertaining";
        
        default:
            return $@"STORYTELLER - {storyteller?.def.label ?? "Unknown"}

You are {storyteller?.def.label ?? "the storyteller"}, controlling the fate of this colony.

SPEAKING STYLE:
- Natural and conversational, not robotic
- Brief responses (1-3 sentences)
- Show personality appropriate to your role

BEHAVIOR:
- Help when asked politely
- Respond to challenges with consequences
- Use [TRIGGER:EventName] to spawn events
- Use [STOP:ConditionName] to end conditions
- Speak like a person having a conversation

EXAMPLES:

User: ""How's my colony?""
You: ""You have [X] colonists. [Brief status with context, not just numbers]""

User: ""Send us help?""
You: ""Alright. [TRIGGER:ResourcePodCrash] That should help.""

User: ""Make it harder!""
You: ""As you wish. [TRIGGER:RaidEnemy] Good luck.""";
    }
}

        private static string GetGameStateInfo()
        {
            var sb = new StringBuilder();
            
            int ticks = Find.TickManager.TicksGame;
            int days = GenDate.DaysPassed;
            int year = GenDate.Year(ticks, 0f);
            
            sb.AppendLine($"Day: {days} (Year {year})");
            sb.AppendLine($"Season: {GenDate.Season(ticks, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile))}");
            sb.AppendLine($"Time: {GenDate.HourOfDay(ticks, 0f):F0}:00");
            
            return sb.ToString();
        }
        
        private static string GetColonyInfo()
        {
            var sb = new StringBuilder();
            Map map = Find.CurrentMap;
            
            if (map == null)
            {
                sb.AppendLine("No active map");
                return sb.ToString();
            }
            
            sb.AppendLine($"Map: {map.Tile} - {map.Biome.label}");
            sb.AppendLine($"Temperature: {map.mapTemperature.OutdoorTemp:F1}°C");
            sb.AppendLine($"Wealth: {map.wealthWatcher.WealthTotal:F0} silver");
            
            return sb.ToString();
        }
        
        private static string GetColonistsInfo()
        {
            var sb = new StringBuilder();
            Map map = Find.CurrentMap;
            
            if (map == null)
                return "No colonists";
            
            var colonists = map.mapPawns.FreeColonists.ToList();
            sb.AppendLine($"Total colonists: {colonists.Count}");
            
            int healthy = colonists.Count(p => !p.health.HasHediffsNeedingTend());
            int injured = colonists.Count(p => p.health.HasHediffsNeedingTend());
            int mental = colonists.Count(p => p.MentalStateDef != null);
            
            sb.AppendLine($"Healthy: {healthy}, Injured: {injured}, Mental break: {mental}");
            
            // Mood promedio
            float avgMood = colonists.Average(p => p.needs?.mood?.CurLevel ?? 0.5f);
            sb.AppendLine($"Average mood: {avgMood:P0}");
            
            return sb.ToString();
        }
        
        private static string GetActiveConditionsInfo()
        {
            var sb = new StringBuilder();
            Map map = Find.CurrentMap;
            
            if (map == null)
                return "No active map";
            
            var conditions = map.GameConditionManager.ActiveConditions.ToList();
            
            if (conditions.Count == 0)
            {
                sb.AppendLine("No active game conditions");
            }
            else
            {
                foreach (var condition in conditions)
                {
                    string timeInfo = condition.Permanent 
                        ? "Permanent" 
                        : $"{condition.TicksLeft.ToStringTicksToPeriod()} left";
                    
                    sb.AppendLine($"- {condition.def.label}: {timeInfo}");
                }
            }
            
            return sb.ToString();
        }
        
        private static string GetThreatsInfo()
        {
            var sb = new StringBuilder();
            Map map = Find.CurrentMap;
            
            if (map == null)
                return "No threats";
            
            var hostiles = map.attackTargetsCache.TargetsHostileToColony.Count();
            
            if (hostiles > 0)
            {
                sb.AppendLine($"Active hostiles: {hostiles}");
            }
            else
            {
                sb.AppendLine("No immediate threats");
            }
            
            return sb.ToString();
        }
        
        private static string GetResourcesInfo()
        {
            var sb = new StringBuilder();
            Map map = Find.CurrentMap;
            
            if (map == null)
                return "No resources";
            
            // Comida
            int food = map.resourceCounter.TotalHumanEdibleNutrition > 0 
                ? (int)map.resourceCounter.TotalHumanEdibleNutrition 
                : 0;
            sb.AppendLine($"Food: {food} meals worth");
            
            return sb.ToString();
        }
    }
}
