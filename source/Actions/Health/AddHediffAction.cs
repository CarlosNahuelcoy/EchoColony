using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Health
{
    public class AddHediffAction : ActionBase
    {
        public override string ActionId => "ADD_HEDIFF";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "give", "inflict", "cause", "add condition" 
        };
        
        public override string AIDescription => 
            "Add a specific hediff/condition to the colonist. Syntax: [ACTION:ADD_HEDIFF:HediffDefName:Severity]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (parameters.Length == 0) return false;
            
            string hediffName = parameters[0];
            var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffName);
            
            return hediffDef != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string hediffName = parameters[0];
            float severity = parameters.Length > 1 && float.TryParse(parameters[1], out float s) ? s : 1f;
            
            var hediffDef = DefDatabase<HediffDef>.GetNamed(hediffName);
            
            if (hediffDef == null)
                return $"Hediff '{hediffName}' not found";
            
            // Agregar el hediff
            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            hediff.Severity = severity;
            
            pawn.health.AddHediff(hediff);
            
            Messages.Message(
                $"{pawn.LabelShort} has been affected by {hediffDef.label}",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );
            
            return $"Added {hediffDef.label} with severity {severity}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string hediffName = parameters.Length > 0 ? parameters[0] : "condition";
            return $"{pawn.LabelShort} suddenly feels the effects of {hediffName} taking hold.";
        }
    }
}