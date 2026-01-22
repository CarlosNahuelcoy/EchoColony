using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class EnslaveAction : ActionBase
    {
        public override string ActionId => "ENSLAVE";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "enslave", "make slave", "become slave" 
        };
        
        public override string AIDescription => 
            "Convert prisoner to slave instantly";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (!pawn.IsPrisonerOfColony) return false;
            
            return ModsConfig.IdeologyActive;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!pawn.IsPrisonerOfColony)
                return $"{pawn.LabelShort} is not a prisoner";
            
            if (pawn.guest == null)
                return $"{pawn.LabelShort} has no guest system";
            
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            
            Messages.Message(
                $"{pawn.LabelShort} has been enslaved",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );
            
            return $"Enslaved {pawn.LabelShort}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} is now bound in servitude - their freedom stripped away.";
        }
    }
}