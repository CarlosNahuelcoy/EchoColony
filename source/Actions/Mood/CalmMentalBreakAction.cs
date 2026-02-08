using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Mood
{
    public class CalmMentalBreakAction : ActionBase
    {
        public override string ActionId => "CALM_BREAK";
        public override ActionCategory Category => ActionCategory.Mood;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "calm down", "stop break", "end breakdown", "soothe" 
        };
        
        public override string AIDescription => 
            "End any ongoing mental break immediately";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.MentalState == null) return false;
            
            // Solo funciona si está en un mental break
            return pawn.InMentalState;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!pawn.InMentalState)
                return $"{pawn.LabelShort} is not in a mental break";
            
            string breakType = pawn.MentalState.def.label;
            
            // Terminar el mental break
            pawn.MentalState.RecoverFromState();
            
            // Dar un pequeño boost de mood para estabilizar
            if (pawn.needs?.mood != null)
            {
                pawn.needs.mood.CurLevel = UnityEngine.Mathf.Max(pawn.needs.mood.CurLevel, 0.5f);
            }
            
            Messages.Message(
                $"{pawn.LabelShort} has calmed down from {breakType}",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Ended mental break: {breakType}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"A wave of calm washes over {pawn.LabelShort}, bringing them back to their senses.";
        }
    }
}