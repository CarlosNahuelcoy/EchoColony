using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Transform
{
    public class RemoveTraitAction : ActionBase
    {
        public override string ActionId => "REMOVE_TRAIT";
        public override ActionCategory Category => ActionCategory.Transform;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "remove trait", "lose trait", "cure trait" 
        };
        
        public override string AIDescription => 
            "Remove a permanent trait. Syntax: [ACTION:REMOVE_TRAIT:TraitDefName]";
        
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
            
            return traitDef != null && pawn.story.traits.HasTrait(traitDef);
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string traitName = parameters[0];
            var traitDef = DefDatabase<TraitDef>.GetNamed(traitName);
            
            if (traitDef == null)
                return $"Trait '{traitName}' not found";
            
            var trait = pawn.story.traits.GetTrait(traitDef);
            
            if (trait == null)
                return $"{pawn.LabelShort} doesn't have trait {traitDef.label}";
            
            string traitLabel = trait.LabelCap;
            pawn.story.traits.RemoveTrait(trait);
            
            Messages.Message(
                $"{pawn.LabelShort} lost trait: {traitLabel}",
                pawn,
                MessageTypeDefOf.NeutralEvent
            );
            
            return $"Removed trait '{traitLabel}'";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"A core aspect of {pawn.LabelShort}'s personality fades away like a forgotten dream.";
        }
    }
}