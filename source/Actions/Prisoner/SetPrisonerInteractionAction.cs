using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class SetPrisonerInteractionAction : ActionBase
    {
        public override string ActionId => "SET_PRISONER_MODE";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "change prisoner mode", "set interaction" 
        };
        
        public override string AIDescription => 
            "Set prisoner interaction mode. Modes: NoInteraction, ReduceResistance, Recruit, Convert. Syntax: [ACTION:SET_PRISONER_MODE:Mode]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (!pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony) return false;
            if (parameters.Length == 0) return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string modeName = parameters[0];
            
            // Use correct def type: PrisonerInteractionModeDef
            var interactionMode = DefDatabase<PrisonerInteractionModeDef>.AllDefs
                .FirstOrDefault(def => def.defName.ToLower().Contains(modeName.ToLower()));
            
            if (interactionMode == null)
            {
                // Try direct match
                interactionMode = DefDatabase<PrisonerInteractionModeDef>.GetNamedSilentFail(modeName);
            }
            
            if (interactionMode == null)
            {
                return $"Interaction mode '{modeName}' not found. Available: NoInteraction, ReduceResistance, Recruit, Convert";
            }
            
            if (pawn.guest != null)
            {
                pawn.guest.interactionMode = interactionMode;
                
                Messages.Message(
                    $"{pawn.LabelShort}'s interaction mode set to {interactionMode.label}",
                    pawn,
                    MessageTypeDefOf.NeutralEvent
                );
                
                return $"Set mode to {interactionMode.label}";
            }
            
            return "No guest system available";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string mode = parameters.Length > 0 ? parameters[0] : "interaction";
            return $"Your orders regarding {pawn.LabelShort} have changed - new approach: {mode}.";
        }
    }
}