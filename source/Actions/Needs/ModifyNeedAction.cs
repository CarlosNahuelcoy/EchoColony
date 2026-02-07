using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Needs
{
    public class ModifyNeedAction : ActionBase
    {
        public override string ActionId => "MODIFY_NEED";
        public override ActionCategory Category => ActionCategory.Needs;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "adjust need", "change need", "modify need" 
        };
        
        public override string AIDescription => 
            "Modify any need by amount. Common needs: Food, Rest, Recreation, Comfort, Beauty, Mood, Outdoors. Syntax: [ACTION:MODIFY_NEED:NeedDefName:Amount]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs == null) return false;
            if (parameters.Length < 2) return false;
            
            string needName = parameters[0];
            var needDef = DefDatabase<NeedDef>.GetNamedSilentFail(needName);
            
            if (needDef == null) return false;
            if (!float.TryParse(parameters[1], out _)) return false;
            
            return pawn.needs.TryGetNeed(needDef) != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string needName = parameters[0];
            float amount = float.Parse(parameters[1]);
            
            var needDef = DefDatabase<NeedDef>.GetNamed(needName);
            var need = pawn.needs.TryGetNeed(needDef);
            
            if (need == null)
                return $"{pawn.LabelShort} doesn't have need '{needName}'";
            
            float oldLevel = need.CurLevel;
            float newLevel = UnityEngine.Mathf.Clamp01(oldLevel + amount);
            
            need.CurLevel = newLevel;
            
            string direction = amount > 0 ? "increased" : "decreased";
            
            Messages.Message(
                $"{pawn.LabelShort}'s {needDef.label} {direction}: {oldLevel:P0} → {newLevel:P0}",
                pawn,
                amount > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
            
            return $"Modified {needDef.label} from {oldLevel:P0} to {newLevel:P0}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string needName = parameters.Length > 0 ? parameters[0] : "need";
            float amount = parameters.Length > 1 && float.TryParse(parameters[1], out float a) ? a : 0f;
            
            if (amount > 0)
                return $"{pawn.LabelShort} feels their {needName} improving.";
            else
                return $"{pawn.LabelShort} feels their {needName} declining.";
        }
    }
}