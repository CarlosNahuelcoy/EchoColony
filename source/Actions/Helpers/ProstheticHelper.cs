using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Helpers
{
    public static class ProstheticHelper
    {
        public static Thing TrySpawnProstheticItem(Pawn pawn, Hediff prosthetic)
        {
            ThingDef thingDef = FindProstheticThingDef(prosthetic.def);
            
            if (thingDef == null)
            {
                Log.Warning($"[EchoColony] No ThingDef found for prosthetic {prosthetic.def.defName}");
                return null;
            }
            
            Thing item = ThingMaker.MakeThing(thingDef);
            
            if (item == null)
                return null;
            
            IntVec3 dropPos = pawn.Position;
            
            if (!GenPlace.TryPlaceThing(item, dropPos, pawn.Map, ThingPlaceMode.Near))
            {
                Log.Warning($"[EchoColony] Failed to place {thingDef.label} near {pawn.LabelShort}");
                item.Destroy();
                return null;
            }
            
            return item;
        }
        
        private static ThingDef FindProstheticThingDef(HediffDef hediffDef)
        {
            var mappings = new Dictionary<string, string>
            {
                { "SimpleProstheticLeg", "SimpleProstheticLeg" },
                { "SimpleProstheticArm", "SimpleProstheticArm" },
                { "SimpleProstheticHeart", "SimpleProstheticHeart" },
                { "BionicEye", "BionicEye" },
                { "BionicArm", "BionicArm" },
                { "BionicLeg", "BionicLeg" },
                { "BionicSpine", "BionicSpine" },
                { "BionicHeart", "BionicHeart" },
                { "BionicStomach", "BionicStomach" },
                { "BionicEar", "BionicEar" },
                { "PowerClaw", "PowerClaw" },
                { "ArchotechEye", "ArchotechEye" },
                { "ArchotechArm", "ArchotechArm" },
                { "ArchotechLeg", "ArchotechLeg" },
                { "PegLeg", "PegLeg" },
                { "WoodenFoot", "WoodenFoot" },
                { "Denture", "Denture" }
            };
            
            if (mappings.TryGetValue(hediffDef.defName, out string thingDefName))
            {
                return DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            }
            
            ThingDef directMatch = DefDatabase<ThingDef>.GetNamedSilentFail(hediffDef.defName);
            if (directMatch != null)
                return directMatch;
            
            foreach (var recipeDef in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipeDef.addsHediff == hediffDef && recipeDef.ingredients != null)
                {
                    var ingredient = recipeDef.ingredients.FirstOrDefault();
                    if (ingredient?.filter?.AnyAllowedDef != null)
                    {
                        return ingredient.filter.AnyAllowedDef;
                    }
                }
            }
            
            return null;
        }
    }
}