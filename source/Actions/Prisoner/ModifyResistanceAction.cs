using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class ModifyResistanceAction : ActionBase
    {
        public override string ActionId => "MODIFY_RESISTANCE";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "reduce resistance", "break will", "convince", "persuade" 
        };
        
        public override string AIDescription => 
            "Modify prisoner's resistance to recruitment. Negative = easier to recruit. Syntax: [ACTION:MODIFY_RESISTANCE:Amount]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (!pawn.IsPrisonerOfColony) return false;
            if (parameters.Length == 0) return false;
            
            return float.TryParse(parameters[0], out _);
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            float amount = float.Parse(parameters[0]);
            
            if (pawn.guest?.resistance == null)
                return $"{pawn.LabelShort} has no resistance system";
            
            float oldResistance = pawn.guest.resistance;
            float newResistance = UnityEngine.Mathf.Max(0f, oldResistance + amount);
            
            pawn.guest.resistance = newResistance;
            
            string message = amount < 0 
                ? $"{pawn.LabelShort}'s will to resist weakens"
                : $"{pawn.LabelShort} becomes more resistant";
            
            Messages.Message(message, pawn, 
                amount < 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            
            return $"Resistance: {oldResistance:F1} → {newResistance:F1}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            float amount = parameters.Length > 0 && float.TryParse(parameters[0], out float a) ? a : 0f;
            
            if (amount < -10f)
                return $"{pawn.LabelShort}'s resistance crumbles under your influence.";
            else if (amount < 0f)
                return $"{pawn.LabelShort} feels their resolve weakening.";
            else
                return $"{pawn.LabelShort} steels their resolve against you.";
        }
    }
}