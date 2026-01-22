using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Needs
{
    public class SetAllNeedsAction : ActionBase
    {
        public override string ActionId => "SATISFY_ALL_NEEDS";
        public override ActionCategory Category => ActionCategory.Needs;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "satisfy everything", "full restore", "perfect condition" 
        };
        
        public override string AIDescription => 
            "Satisfy all needs to maximum (food, rest, recreation, comfort, etc.)";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            return pawn.needs != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (pawn.needs == null)
                return "No needs system";
            
            int satisfiedCount = 0;
            
            var allNeeds = pawn.needs.AllNeeds;
            foreach (var need in allNeeds)
            {
                if (need.MaxLevel > 0f && need.CurLevel < need.MaxLevel)
                {
                    need.CurLevel = need.MaxLevel;
                    satisfiedCount++;
                }
            }
            
            Messages.Message(
                $"{pawn.LabelShort} feels completely refreshed and satisfied!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Satisfied {satisfiedCount} needs";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} feels absolutely perfect - every need completely satisfied.";
        }
    }
}