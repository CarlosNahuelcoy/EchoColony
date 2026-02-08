using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class AddThoughtAction : ActionBase
    {
        public override string ActionId => "ADD_THOUGHT";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "make feel", "give feeling", "inspire", "depress", "cheer up" 
        };
        
        public override string AIDescription => 
            "Add a specific thought/memory. Common: Catharsis (+40), AttendedWedding (+20), AteHumanlikeMeatDirect (-20). Syntax: [ACTION:ADD_THOUGHT:ThoughtDefName]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.needs?.mood?.thoughts == null) return false;
            if (parameters.Length == 0) return false;
            
            string thoughtName = parameters[0];
            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
            
            return thoughtDef != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string thoughtName = parameters[0];
            var thoughtDef = DefDatabase<ThoughtDef>.GetNamed(thoughtName);
            
            if (thoughtDef == null)
                return $"Thought '{thoughtName}' not found";
            
            pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            
            float moodEffect = thoughtDef.stages[0].baseMoodEffect;
            string effectType = moodEffect > 0 ? "positive" : "negative";
            
            Messages.Message(
                $"{pawn.LabelShort} feels {thoughtDef.stages[0].label}",
                pawn,
                moodEffect > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
            
            return $"Added thought '{thoughtDef.label}' ({moodEffect:+0;-0})";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string thoughtName = parameters.Length > 0 ? parameters[0] : "emotion";
            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
            
            if (thoughtDef != null)
            {
                return $"{pawn.LabelShort} suddenly feels: {thoughtDef.stages[0].label}";
            }
            
            return $"{pawn.LabelShort} is overcome with a sudden feeling.";
        }
    }
}