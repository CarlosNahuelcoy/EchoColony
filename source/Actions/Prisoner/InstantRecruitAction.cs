using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class InstantRecruitAction : ActionBase
    {
        public override string ActionId => "INSTANT_RECRUIT";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "recruit instantly", "join now", "convince immediately" 
        };
        
        public override string AIDescription => 
            "Instantly recruit prisoner/slave to colony (bypasses resistance)";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            return pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
{
    if (!pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony)
        return $"{pawn.LabelShort} is not captive";
    
    bool wasSlave = pawn.IsSlaveOfColony;
    
    // Set resistance to 0 to ensure recruitment
    if (pawn.guest != null)
    {
        pawn.guest.resistance = 0f;
    }
    
    // Change faction to player
    if (pawn.Faction != Faction.OfPlayer)
    {
        pawn.SetFaction(Faction.OfPlayer);
    }
    
    // Clear guest status - this makes them a colonist
    if (pawn.guest != null)
    {
        pawn.guest.SetGuestStatus(null);
    }
    
    string status = wasSlave ? "slave" : "prisoner";
    
    Messages.Message(
        $"{pawn.LabelShort} (former {status}) has joined the colony!",
        pawn,
        MessageTypeDefOf.PositiveEvent
    );
    
    return $"Recruited {pawn.LabelShort}";
}

        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            return $"{pawn.LabelShort} feels a profound change - this colony is now their home, their family.";
        }
    }
}