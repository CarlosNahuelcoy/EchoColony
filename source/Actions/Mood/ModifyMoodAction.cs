using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class ModifyMoodAction : ActionBase
    {
        public override string ActionId => "MODIFY_MOOD";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "boost mood", "improve mood", "worsen mood", "change mood" 
        };
        
        public override string AIDescription => 
            "Directly modify mood level. Syntax: [ACTION:MODIFY_MOOD:Amount] (Amount: -1.0 to 1.0)";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood == null) return false;
            if (parameters.Length == 0) return false;
            
            return float.TryParse(parameters[0], out _);
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!float.TryParse(parameters[0], out float amount))
                return "Invalid amount";
            
            // Limitar el cambio entre -1.0 y 1.0
            amount = UnityEngine.Mathf.Clamp(amount, -1f, 1f);
            
            float oldMood = pawn.needs.mood.CurLevel;
            float newMood = UnityEngine.Mathf.Clamp01(oldMood + amount);
            
            pawn.needs.mood.CurLevel = newMood;
            
            string direction = amount > 0 ? "improved" : "worsened";
            
            Messages.Message(
                $"{pawn.LabelShort}'s mood has {direction}",
                pawn,
                amount > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
            
            return $"Mood changed from {oldMood:P0} to {newMood:P0}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            float amount = parameters.Length > 0 && float.TryParse(parameters[0], out float a) ? a : 0f;
            
            if (amount > 0.3f)
                return $"{pawn.LabelShort} feels suddenly uplifted, as if a great weight has been removed.";
            else if (amount > 0)
                return $"{pawn.LabelShort} feels a bit better about things.";
            else if (amount < -0.3f)
                return $"{pawn.LabelShort} feels overwhelming despair wash over them.";
            else
                return $"{pawn.LabelShort} feels their mood shift slightly.";
        }
    }
}