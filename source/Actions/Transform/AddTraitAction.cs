using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Transform
{
    public class AddTraitAction : ActionBase
    {
        public override string ActionId => "ADD_TRAIT";
        public override ActionCategory Category => ActionCategory.Transform;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "gain trait", "become", "develop trait" 
        };
        
        public override string AIDescription => 
            "Add a permanent trait. Common: Kind, Bloodlust, Psychopath, Cannibal, Brawler, Nudist. Syntax: [ACTION:ADD_TRAIT:TraitDefName:Degree]";
        
        public override ValidationLevel Validation => ValidationLevel.Strict;
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.story?.traits == null) return false;
            if (parameters.Length == 0) return false;
            
            if (!MyMod.Settings.allowExtremeActions)
                return false;
            
            string traitName = parameters[0];
            var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitName);
            
            if (traitDef == null) return false;
            
            if (pawn.story.traits.HasTrait(traitDef))
                return false;
            
            if (pawn.story.traits.allTraits.Count >= 3)
                return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string traitName = parameters[0];
            int requestedDegree = parameters.Length > 1 && int.TryParse(parameters[1], out int parsed) ? parsed : 0;

            var traitDef = DefDatabase<TraitDef>.GetNamed(traitName);

            if (traitDef == null)
                return $"Trait '{traitName}' not found";

            if (pawn.story.traits.HasTrait(traitDef))
                return $"{pawn.LabelShort} already has trait {traitDef.label}";

            var availableDegrees = traitDef.degreeDatas;
            int finalDegree = requestedDegree;

            if (!availableDegrees.Any(degreeData => degreeData.degree == requestedDegree))
            {
                finalDegree = availableDegrees.FirstOrDefault()?.degree ?? 0;
            }

            Trait newTrait = new Trait(traitDef, finalDegree, true);
            pawn.story.traits.GainTrait(newTrait);
            
            Messages.Message(
                $"{pawn.LabelShort} gained trait: {newTrait.LabelCap}",
                pawn,
                MessageTypeDefOf.NeutralEvent
            );
            
            return $"Added trait '{newTrait.LabelCap}'";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string traitName = parameters.Length > 0 ? parameters[0] : "personality trait";
            return $"Something fundamental shifts within {pawn.LabelShort} - a new aspect of their personality emerges.";
        }
    }
}