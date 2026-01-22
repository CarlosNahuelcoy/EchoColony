using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class TriggerMentalBreakAction : ActionBase
    {
        public override string ActionId => "TRIGGER_BREAK";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "mental break", "break down", "go berserk", "snap" 
        };
        
        public override string AIDescription => 
            "Trigger a mental break. Minor: Wander_Sad, Binging_Food. Major: Tantrum, Berserk. Extreme: GiveUpExit, MurderousRage. Syntax: [ACTION:TRIGGER_BREAK:MentalBreakDefName]";
        
        public override ValidationLevel Validation => ValidationLevel.Moderate;
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.InMentalState) return false;
            
            if (!MyMod.Settings.allowNegativeActions)
                return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            MentalBreakDef breakDef = null;
            
            if (parameters.Length > 0)
            {
                string breakName = parameters[0];
                breakDef = DefDatabase<MentalBreakDef>.GetNamedSilentFail(breakName);
            }
            
            if (breakDef == null)
            {
                var possibleBreaks = DefDatabase<MentalBreakDef>.AllDefsListForReading
                    .Where(def => def.Worker.BreakCanOccur(pawn))
                    .ToList();
                
                if (possibleBreaks.Any())
                {
                    breakDef = possibleBreaks.RandomElement();
                }
            }
            
            if (breakDef == null)
                return "No suitable mental break available";
            
            breakDef.Worker.TryStart(pawn, "divine intervention", false);
            
            Messages.Message(
                $"{pawn.LabelShort} has suffered a mental break: {breakDef.label}!",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );
            
            return $"Triggered mental break: {breakDef.label}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort}'s mind suddenly fractures under unseen pressure.";
        }
    }
}