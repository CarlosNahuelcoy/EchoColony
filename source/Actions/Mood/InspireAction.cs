using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class InspireAction : ActionBase
    {
        public override string ActionId => "INSPIRE";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "inspire", "motivate", "enlighten", "creative burst" 
        };
        
        public override string AIDescription => 
            "Give an inspiration. Available: Inspired_Creativity (art), Inspired_Surgery (medical), Inspired_Trade (social), Frenzy_Work, or random. Syntax: [ACTION:INSPIRE:InspirationDefName] or [ACTION:INSPIRE]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.mindState == null) return false;
            
            if (pawn.mindState.inspirationHandler.Inspired)
                return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            InspirationDef inspirationDef = null;
            
            if (parameters.Length > 0)
            {
                string inspirationName = parameters[0];
                inspirationDef = DefDatabase<InspirationDef>.GetNamedSilentFail(inspirationName);
            }
            
            if (inspirationDef == null)
            {
                var availableInspirations = DefDatabase<InspirationDef>.AllDefsListForReading
                    .Where(def => def.Worker.InspirationCanOccur(pawn))
                    .ToList();
                
                if (availableInspirations.Any())
                {
                    inspirationDef = availableInspirations.RandomElement();
                }
            }
            
            if (inspirationDef == null)
                return "No suitable inspiration available";
            
            pawn.mindState.inspirationHandler.TryStartInspiration(inspirationDef);
            
            Messages.Message(
                $"{pawn.LabelShort} has been inspired: {inspirationDef.label}!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Inspired with '{inspirationDef.label}'";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"A brilliant idea strikes {pawn.LabelShort} like lightning - they feel suddenly inspired!";
        }
    }
}