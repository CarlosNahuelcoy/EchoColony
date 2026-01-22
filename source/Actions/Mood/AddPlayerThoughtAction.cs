using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class AddPlayerThoughtAction : ActionBase
    {
        // Cooldown tracking per pawn
        private static Dictionary<int, int> lastThoughtTick = new Dictionary<int, int>();
        private const int COOLDOWN_TICKS = 180000; // 3 in-game days (60000 ticks per day)
        
        // Limits
        private const int MAX_THOUGHTS_PER_DAY = 3;
        private const float MAX_TOTAL_MOOD_EFFECT = 40f;
        private const int MAX_MESSAGE_LENGTH = 60;
        
        public override string ActionId => "ADD_PLAYER_THOUGHT";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "player thought", "custom message", "player message", "make feel" 
        };
        
        public override string AIDescription => 
            "Add custom thought from player interaction. IMPORTANT: Extract SHORT phrase (max 60 chars) from conversation for label. Syntax: [ACTION:ADD_PLAYER_THOUGHT:MoodEffect:ShortLabel]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood?.thoughts == null) return false;
            if (parameters.Length < 2) return false;
            
            if (!float.TryParse(parameters[0], out float moodEffect)) return false;
            
            // Check cooldown
            if (IsOnCooldown(pawn))
            {
                Log.Message($"[EchoColony] {pawn.LabelShort} player thought on cooldown");
                return false;
            }
            
            // Check daily limit
            if (HasReachedDailyLimit(pawn))
            {
                Log.Message($"[EchoColony] {pawn.LabelShort} reached daily player thought limit");
                return false;
            }
            
            // Check total mood cap
            if (WouldExceedMoodCap(pawn, moodEffect))
            {
                Log.Message($"[EchoColony] Adding thought would exceed mood cap for {pawn.LabelShort}");
                return false;
            }
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            float moodEffect = float.Parse(parameters[0]);
            string rawMessage = parameters[1];
            
            // Sanitize and limit message length
            string cleanMessage = SanitizeMessage(rawMessage);
            
            if (cleanMessage.Length > MAX_MESSAGE_LENGTH)
            {
                cleanMessage = cleanMessage.Substring(0, MAX_MESSAGE_LENGTH) + "...";
            }
            
            // Clamp mood effect
            moodEffect = UnityEngine.Mathf.Clamp(moodEffect, -30f, 30f);
            
            // Calculate duration based on mood effect magnitude
            float durationDays = CalculateDuration(moodEffect);
            
            // Create thought
            ThoughtDef thoughtDef = CreatePlayerThoughtDef(cleanMessage, moodEffect, durationDays);
            
            // Add to pawn
            pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            
            // Update cooldown
            UpdateCooldown(pawn);
            
            // Visual feedback
            Messages.Message(
                $"{pawn.LabelShort}: \"{cleanMessage}\" ({moodEffect:+0;-0})",
                pawn,
                moodEffect > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
            
            Log.Message($"[EchoColony] Added player thought to {pawn.LabelShort}: '{cleanMessage}' ({moodEffect:+0}) for {durationDays} days");
            
            return $"Added player thought: '{cleanMessage}' ({moodEffect:+0;-0})";
        }
        
        private string SanitizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "player's words";
            
            // Remove tags, special characters
            message = message.Trim();
            message = System.Text.RegularExpressions.Regex.Replace(message, @"<.*?>", "");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\[.*?\]", "");
            
            // Remove quotes if present
            message = message.Trim('"', '\'', '`');
            
            // Lowercase first letter for better grammar integration
            if (message.Length > 0 && char.IsUpper(message[0]))
            {
                message = char.ToLower(message[0]) + message.Substring(1);
            }
            
            // Remove trailing punctuation for consistency
            message = message.TrimEnd('.', '!', '?', ',', ';', ':');
            
            return message;
        }
        
        private float CalculateDuration(float moodEffect)
        {
            // Stronger effects last longer
            float magnitude = UnityEngine.Mathf.Abs(moodEffect);
            
            if (magnitude >= 20f) return 3f;      // 3 days for strong effects
            if (magnitude >= 10f) return 2f;      // 2 days for moderate
            return 1f;                             // 1 day for mild
        }
        
        private bool IsOnCooldown(Pawn pawn)
        {
            int pawnId = pawn.thingIDNumber;
            
            if (!lastThoughtTick.ContainsKey(pawnId))
                return false;
            
            int ticksSinceLastThought = Find.TickManager.TicksGame - lastThoughtTick[pawnId];
            return ticksSinceLastThought < COOLDOWN_TICKS;
        }
        
        private void UpdateCooldown(Pawn pawn)
        {
            int pawnId = pawn.thingIDNumber;
            lastThoughtTick[pawnId] = Find.TickManager.TicksGame;
        }
        
        private bool HasReachedDailyLimit(Pawn pawn)
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return false;
            
            // Count player thoughts from today
            int currentDay = GenDate.DaysPassed;
            
            int todaysThoughts = pawn.needs.mood.thoughts.memories.Memories
                .Where(m => m.def.defName.StartsWith("EchoColony_PlayerThought_"))
                .Where(m => m.age < 60000) // Less than 1 day old
                .Count();
            
            return todaysThoughts >= MAX_THOUGHTS_PER_DAY;
        }
        
        private bool WouldExceedMoodCap(Pawn pawn, float newMoodEffect)
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return false;
            
            // Calculate total mood from player thoughts
            float currentPlayerMood = pawn.needs.mood.thoughts.memories.Memories
                .Where(m => m.def.defName.StartsWith("EchoColony_PlayerThought_"))
                .Sum(m => m.MoodOffset());
            
            float projectedTotal = currentPlayerMood + newMoodEffect;
            
            // Allow negative to balance positive
            if (newMoodEffect < 0)
                return false;
            
            return projectedTotal > MAX_TOTAL_MOOD_EFFECT;
        }
        
        private ThoughtDef CreatePlayerThoughtDef(string message, float moodEffect, float durationDays)
        {
            string uniqueId = $"EchoColony_PlayerThought_{Find.TickManager.TicksGame}_{Rand.Range(1000, 9999)}";
            
            ThoughtDef thoughtDef = new ThoughtDef
            {
                defName = uniqueId,
                thoughtClass = typeof(Thought_Memory),
                durationDays = durationDays,
                stackLimit = 1,
                stackedEffectMultiplier = 0f, // Prevent stacking
                nullifyingTraits = new List<TraitDef>(), // Empty list
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage
                    {
                        label = message,
                        description = GenerateDescription(message, moodEffect),
                        baseMoodEffect = moodEffect
                    }
                }
            };
            
            // Add to DefDatabase
            DefDatabase<ThoughtDef>.Add(thoughtDef);
            
            return thoughtDef;
        }
        
        private string GenerateDescription(string message, float moodEffect)
        {
            if (moodEffect > 15f)
                return $"The player's words uplifted me: \"{message}\"";
            else if (moodEffect > 5f)
                return $"The player said something encouraging: \"{message}\"";
            else if (moodEffect > -5f)
                return $"The player mentioned: \"{message}\"";
            else if (moodEffect > -15f)
                return $"The player's words troubled me: \"{message}\"";
            else
                return $"The player's harsh words hurt: \"{message}\"";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string message = parameters.Length > 1 ? SanitizeMessage(parameters[1]) : "something meaningful";
            float mood = parameters.Length > 0 && float.TryParse(parameters[0], out float m) ? m : 0f;
            
            if (mood > 10f)
                return $"Your words warm {pawn.LabelShort}'s heart.";
            else if (mood > 0f)
                return $"{pawn.LabelShort} appreciates your sentiment.";
            else if (mood > -10f)
                return $"Your words trouble {pawn.LabelShort} slightly.";
            else
                return $"{pawn.LabelShort} feels hurt by your words.";
        }
        
        // Cleanup old entries periodically
        public static void CleanupOldCooldowns()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            var keysToRemove = lastThoughtTick
                .Where(kvp => currentTick - kvp.Value > COOLDOWN_TICKS * 2)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                lastThoughtTick.Remove(key);
            }
        }
    }
}