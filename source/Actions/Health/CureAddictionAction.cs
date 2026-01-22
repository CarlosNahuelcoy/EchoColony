using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Health
{
    public class CureAddictionAction : ActionBase
    {
        public override string ActionId => "CURE_ADDICTION";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "cure addiction", "remove addiction", "sober up", "cleanse" 
        };
        
        public override string AIDescription => 
            "Cure all addictions instantly";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            
            // Verificar que tenga adicciones
            return pawn.health.hediffSet.hediffs
                .Any(h => h.def.defName.Contains("Addiction") || h.def.defName.Contains("Withdrawal"));
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            var addictions = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.Contains("Addiction"))
                .ToList();
            
            var withdrawals = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.Contains("Withdrawal"))
                .ToList();
            
            int count = addictions.Count + withdrawals.Count;
            
            foreach (var addiction in addictions)
            {
                pawn.health.RemoveHediff(addiction);
            }
            
            foreach (var withdrawal in withdrawals)
            {
                pawn.health.RemoveHediff(withdrawal);
            }
            
            if (count > 0)
            {
                Messages.Message(
                    $"{pawn.LabelShort} has been cured of all addictions!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            }
            
            return $"Cured {count} addiction(s)";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} feels clarity returning as the grip of addiction releases them.";
        }
    }
}