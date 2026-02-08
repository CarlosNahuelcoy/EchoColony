using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Health
{
    public class CureDiseaseAction : ActionBase
    {
        public override string ActionId => "CURE_DISEASE";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "cure disease", "heal sickness", "remove illness" 
        };
        
        public override string AIDescription => 
            "Cure all diseases, infections, and illnesses";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            
            // Verificar que tenga enfermedades
            return pawn.health.hediffSet.hediffs
                .Any(h => h.def.makesSickThought || 
                         h.def.defName.Contains("Infection") ||
                         h.def.defName.Contains("Plague") ||
                         h.def.defName.Contains("Flu") ||
                         h.def.defName.Contains("Malaria"));
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            var diseases = pawn.health.hediffSet.hediffs
                .Where(h => h.def.makesSickThought || 
                           h.def.defName.Contains("Infection") ||
                           h.def.defName.Contains("Plague") ||
                           h.def.defName.Contains("Flu") ||
                           h.def.defName.Contains("Malaria") ||
                           h.def.defName.Contains("FoodPoisoning") ||
                           h.def.defName.Contains("ToxicBuildup"))
                .ToList();
            
            int count = diseases.Count;
            
            foreach (var disease in diseases)
            {
                pawn.health.RemoveHediff(disease);
            }
            
            if (count > 0)
            {
                Messages.Message(
                    $"{pawn.LabelShort} has been cured of all diseases!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            }
            
            return $"Cured {count} disease(s)";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} feels strength returning as illness leaves their body.";
        }
    }
}