using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class ConvertIdeologyAction : ActionBase
    {
        public override string ActionId => "CONVERT_IDEOLOGY";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "convert beliefs", "change ideology", "adopt faith" 
        };
        
        public override string AIDescription => 
            "Convert prisoner/slave to colony's ideology";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (!ModsConfig.IdeologyActive) return false;
            if (pawn.Ideo == null) return false;
            
            return pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!ModsConfig.IdeologyActive)
                return "Ideology DLC not active";
            
            var playerIdeo = Faction.OfPlayer.ideos?.PrimaryIdeo;
            
            if (playerIdeo == null)
                return "Colony has no ideology";
            
            if (pawn.Ideo == playerIdeo)
                return $"{pawn.LabelShort} already follows this ideology";
            
            var oldIdeo = pawn.Ideo;
            
            pawn.ideo.SetIdeo(playerIdeo);
            
            Messages.Message(
                $"{pawn.LabelShort} has converted from {oldIdeo.name} to {playerIdeo.name}!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
            
            return $"Converted to {playerIdeo.name}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort}'s beliefs shift fundamentally - they embrace a new faith.";
        }
    }
}