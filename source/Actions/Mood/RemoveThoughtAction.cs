using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class RemoveThoughtAction : ActionBase
    {
        public override string ActionId => "REMOVE_THOUGHT";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "remove feeling", "forget", "erase memory", "clear mind" 
        };
        
        public override string AIDescription => 
            "Remove a specific thought/memory. Syntax: [ACTION:REMOVE_THOUGHT:ThoughtDefName]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood?.thoughts?.memories == null) return false;
            if (parameters.Length == 0) return false;
            
            string thoughtName = parameters[0];
            return pawn.needs.mood.thoughts.memories.Memories
                .Any(m => m.def.defName.Equals(thoughtName, System.StringComparison.OrdinalIgnoreCase));
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string thoughtName = parameters[0];
            
            var memories = pawn.needs.mood.thoughts.memories.Memories
                .Where(m => m.def.defName.Equals(thoughtName, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            int count = 0;
            foreach (var memory in memories)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemory(memory);
                count++;
            }
            
            if (count > 0)
            {
                Messages.Message(
                    $"{pawn.LabelShort} no longer remembers that feeling",
                    pawn,
                    MessageTypeDefOf.NeutralEvent
                );
            }
            
            return $"Removed {count} instance(s) of '{thoughtName}'";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string thoughtName = parameters.Length > 0 ? parameters[0] : "memory";
            return $"The weight of {thoughtName} lifts from {pawn.LabelShort}'s mind.";
        }
    }
}