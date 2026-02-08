using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Health
{
    public class RemoveBodyPartAction : ActionBase
    {
        public override string ActionId => "REMOVE_BODYPART";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "remove", "amputate", "cut off", "lose" 
        };
        
        public override string AIDescription => 
            "Remove/amputate a body part. Syntax: [ACTION:REMOVE_BODYPART:BodyPartLabel]";
        
        public override ValidationLevel Validation => ValidationLevel.Strict;
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (parameters.Length == 0) return false;
            
            // Verificar que el mod permita acciones extremas
            if (!MyMod.Settings.allowExtremeActions)
                return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string bodyPartLabel = parameters[0];
            
            // Buscar la parte del cuerpo
            BodyPartRecord bodyPart = pawn.RaceProps.body.AllParts
                .FirstOrDefault(bp => bp.Label.ToLower().Contains(bodyPartLabel.ToLower()));
            
            if (bodyPart == null)
            {
                bodyPart = pawn.RaceProps.body.AllParts
                    .FirstOrDefault(bp => bp.def.defName.ToLower().Contains(bodyPartLabel.ToLower()));
            }
            
            if (bodyPart == null)
                return $"Body part '{bodyPartLabel}' not found";
            
            // No permitir remover partes vitales esenciales
            if (bodyPart.def.defName == "Heart" || bodyPart.def.defName == "Brain")
                return "Cannot remove vital organs";
            
            // Crear hediff de parte faltante
            Hediff_MissingPart missingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(
                HediffDefOf.MissingBodyPart, 
                pawn, 
                bodyPart
            );
            
            pawn.health.AddHediff(missingPart, bodyPart);
            
            Messages.Message(
                $"{pawn.LabelShort} has lost their {bodyPart.Label}!",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );
            
            return $"Removed {bodyPart.Label}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string bodyPart = parameters.Length > 0 ? parameters[0] : "body part";
            return $"{pawn.LabelShort} feels a sudden emptiness where their {bodyPart} used to be.";
        }
    }
}