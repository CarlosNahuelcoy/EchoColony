using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace EchoColony.Actions.Health
{
    public class ResurrectAction : ActionBase
    {
        public override string ActionId => "RESURRECT";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "resurrect", "revive", "bring back to life", "raise from dead" 
        };
        
        public override string AIDescription => 
            "Bring a dead colonist back to life (only works if corpse exists)";
        
        public override ValidationLevel Validation => ValidationLevel.Strict;
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null) return false;
            
            // Solo funciona si el pawn está muerto
            return pawn.Dead;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!pawn.Dead)
                return $"{pawn.LabelShort} is not dead";
            
            // Usar el método de resurrección del juego
            ResurrectionUtility.TryResurrect(pawn);
            
            // Curar todas las heridas post-resurrección
            var injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .ToList();
            
            foreach (var injury in injuries)
            {
                pawn.health.RemoveHediff(injury);
            }
            
            Messages.Message(
                $"{pawn.LabelShort} has been resurrected!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Resurrected {pawn.LabelShort}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} gasps as life floods back into their body, eyes opening in wonder.";
        }
    }
}