using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Health
{
    public class HealAction : ActionBase
    {
        public override string ActionId => "HEAL";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "heal", "cure", "fix", "restore health", "make better", "recover" 
        };
        
        public override string AIDescription => 
            "Fully heal all injuries and restore health to 100%";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            
            // Can always attempt healing
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (pawn.health?.hediffSet == null)
                return "No health system available";
            
            // Heal all injuries
            var injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .ToList();
            
            foreach (var injury in injuries)
            {
                pawn.health.RemoveHediff(injury);
            }
            
            // Remove bleeding
            var bleeding = pawn.health.hediffSet.hediffs
                .Where(h => h.Bleeding)
                .ToList();
            
            foreach (var bleed in bleeding)
            {
                pawn.health.RemoveHediff(bleed);
            }
            
            // Cure diseases (optional - can be separate action)
            var diseases = pawn.health.hediffSet.hediffs
                .Where(h => h.def.makesSickThought)
                .ToList();
            
            foreach (var disease in diseases)
            {
                pawn.health.RemoveHediff(disease);
            }
            
            Messages.Message(
                $"{pawn.LabelShort} has been miraculously healed!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Healed {injuries.Count} injuries";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} feels warmth spreading through their body as wounds close and pain fades away.";
        }
    }
}