using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class RemoveAllNegativeThoughtsAction : ActionBase
    {
        public override string ActionId => "REMOVE_NEGATIVE_THOUGHTS";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "clear bad thoughts", "remove negativity", "cleanse mind", "peace of mind" 
        };
        
        public override string AIDescription => 
            "Remove all negative thoughts and memories";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood?.thoughts?.memories == null) return false;
            
            // Verificar que tenga pensamientos negativos
            return pawn.needs.mood.thoughts.memories.Memories
                .Any(m => m.MoodOffset() < 0);
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            var negativeThoughts = pawn.needs.mood.thoughts.memories.Memories
                .Where(m => m.MoodOffset() < 0)
                .ToList();
            
            int count = negativeThoughts.Count;
            
            foreach (var thought in negativeThoughts)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemory(thought);
            }
            
            if (count > 0)
            {
                Messages.Message(
                    $"{pawn.LabelShort}'s negative thoughts have been cleansed",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            }
            
            return $"Removed {count} negative thought(s)";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"All negativity drains away from {pawn.LabelShort}'s mind, leaving only peace.";
        }
    }
}