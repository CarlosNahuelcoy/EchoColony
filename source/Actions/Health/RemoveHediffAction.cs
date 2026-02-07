using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EchoColony.Actions.Health
{
    public class RemoveHediffAction : ActionBase
    {
        public override string ActionId => "REMOVE_HEDIFF";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "remove", "cure", "eliminate", "get rid of" 
        };
        
        public override string AIDescription => 
            "Remove a specific hediff/condition. Syntax: [ACTION:REMOVE_HEDIFF:HediffDefName]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (parameters.Length == 0) return false;
            
            string hediffName = parameters[0];
            return pawn.health.hediffSet.hediffs
                .Any(h => h.def.defName.Equals(hediffName, System.StringComparison.OrdinalIgnoreCase));
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string hediffName = parameters[0];
            
            var hediff = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.Equals(hediffName, System.StringComparison.OrdinalIgnoreCase));
            
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
                return $"Removed {hediff.def.label}";
            }
            
            return "Hediff not found";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string hediffName = parameters.Length > 0 ? parameters[0] : "condition";
            return $"The {hediffName} affecting {pawn.LabelShort} vanishes as if it never existed.";
        }
    }
}