using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class SetMoodDirectAction : ActionBase
    {
        public override string ActionId => "SET_MOOD";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "set mood to", "make mood", "mood level" 
        };
        
        public override string AIDescription => 
            "Set mood to exact level. Syntax: [ACTION:SET_MOOD:0.0-1.0]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood == null) return false;
            if (parameters.Length == 0) return false;
            
            return float.TryParse(parameters[0], out float value) && value >= 0f && value <= 1f;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!float.TryParse(parameters[0], out float targetMood))
                return "Invalid mood value";
            
            targetMood = UnityEngine.Mathf.Clamp01(targetMood);
            float oldMood = pawn.needs.mood.CurLevel;
            
            pawn.needs.mood.CurLevel = targetMood;
            
            Messages.Message(
                $"{pawn.LabelShort}'s mood set to {targetMood:P0}",
                pawn,
                targetMood > oldMood ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
            
            return $"Mood set from {oldMood:P0} to {targetMood:P0}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            float mood = parameters.Length > 0 && float.TryParse(parameters[0], out float m) ? m : 0.5f;
            
            if (mood >= 0.8f)
                return $"{pawn.LabelShort} feels overwhelmingly joyful.";
            else if (mood >= 0.5f)
                return $"{pawn.LabelShort} feels content and stable.";
            else if (mood >= 0.3f)
                return $"{pawn.LabelShort} feels downcast and troubled.";
            else
                return $"{pawn.LabelShort} feels deep despair.";
        }
    }
}