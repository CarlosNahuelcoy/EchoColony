using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class AddPositiveThoughtAction : ActionBase
    {
        public override string ActionId => "ADD_POSITIVE_THOUGHT";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "make happy", "cheer up", "brighten mood", "feel joy" 
        };
        
        public override string AIDescription => 
            "Add a random strong positive thought";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            return pawn.needs?.mood?.thoughts != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            // Lista de pensamientos positivos comunes
            List<string> positiveThoughts = new List<string>
            {
                "GotMarried",
                "AttendedWedding", 
                "AteFineMeal",
                "AteLavishMeal",
                "RecreationSatisfied",
                "PsychicHarmonizer",
                "Catharsis"
            };
            
            // Intentar agregar un pensamiento positivo aleatorio
            string chosenThought = positiveThoughts.RandomElement();
            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(chosenThought);
            
            if (thoughtDef != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                
                Messages.Message(
                    $"{pawn.LabelShort} feels {thoughtDef.stages[0].label}",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
                
                return $"Added positive thought: {thoughtDef.label}";
            }
            
            return "No suitable positive thought found";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"A wave of happiness washes over {pawn.LabelShort}.";
        }
    }
}