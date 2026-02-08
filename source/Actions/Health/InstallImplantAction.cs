using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using EchoColony.Actions.Helpers;  // NUEVO: Importar el helper

namespace EchoColony.Actions.Health
{
    public class InstallImplantAction : ActionBase
    {
        public override string ActionId => "INSTALL_IMPLANT";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "install", "implant", "give bionic", "add prosthetic", "enhance" 
        };
        
        public override string AIDescription => 
            "Install a bionic or prosthetic implant. Syntax: [ACTION:INSTALL_IMPLANT:HediffDefName:BodyPartLabel]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            if (parameters.Length < 2) return false;
            
            string hediffName = parameters[0];
            var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffName);
            
            return hediffDef != null && hediffDef.addedPartProps != null;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            string hediffName = parameters[0];
            string bodyPartLabel = parameters[1];
            
            var hediffDef = DefDatabase<HediffDef>.GetNamed(hediffName);
            
            // Find the body part
            BodyPartRecord bodyPart = pawn.RaceProps.body.AllParts
                .FirstOrDefault(bp => bp.Label.ToLower().Contains(bodyPartLabel.ToLower()));
            
            if (bodyPart == null)
            {
                bodyPart = pawn.RaceProps.body.AllParts
                    .FirstOrDefault(bp => bp.def.defName.ToLower().Contains(bodyPartLabel.ToLower()));
            }
            
            if (bodyPart == null)
                return $"Body part '{bodyPartLabel}' not found on {pawn.LabelShort}";
            
            // Check if part is missing
            var missingPart = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == bodyPart);
            
            if (missingPart != null)
            {
                return $"Cannot install on missing {bodyPart.Label}. Regenerate it first with REGROW_BODYPART";
            }
            
            // MODIFICADO: Ahora dropea prótesis existentes
            var existingHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Part == bodyPart)
                .ToList();
            
            var droppedItems = new List<string>();
            
            foreach (var existing in existingHediffs)
            {
                // If it's a prosthetic, try to drop it
                if (existing.def.addedPartProps != null)
                {
                    // NUEVO: Usar el helper para dropear la prótesis
                    Thing droppedItem = ProstheticHelper.TrySpawnProstheticItem(pawn, existing);
                    if (droppedItem != null)
                    {
                        droppedItems.Add(existing.def.label);
                    }
                }
                
                pawn.health.RemoveHediff(existing);
            }
            
            // Install the new implant
            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn, bodyPart);
            pawn.health.AddHediff(hediff, bodyPart);
            
            string message = $"{pawn.LabelShort} received {hediffDef.label} on their {bodyPart.Label}";
            
            // NUEVO: Informar sobre prótesis dropeadas
            if (droppedItems.Any())
            {
                message += $" ({string.Join(", ", droppedItems)} removed and dropped)";
            }
            
            Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
            
            return $"Installed {hediffDef.label} on {bodyPart.Label}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string implantName = parameters.Length > 0 ? parameters[0] : "implant";
            string bodyPart = parameters.Length > 1 ? parameters[1] : "body";
            return $"{pawn.LabelShort} feels a strange sensation as {implantName} integrates with their {bodyPart}.";
        }
    }
}