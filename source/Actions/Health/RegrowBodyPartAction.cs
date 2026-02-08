using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using EchoColony.Actions.Helpers;

namespace EchoColony.Actions.Health
{
    public class RegrowBodyPartAction : ActionBase
    {
        public override string ActionId => "REGROW_BODYPART";
        public override ActionCategory Category => ActionCategory.Health;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "regrow", "restore", "regenerate", "grow back" 
        };
        
        public override string AIDescription => 
            "Regenerate a missing body part (removes prosthetics first). Syntax: [ACTION:REGROW_BODYPART:BodyPartLabel]";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            
            if (parameters.Length == 0)
            {
                // Can execute if has missing parts OR prosthetics
                bool hasMissingParts = pawn.health?.hediffSet?.hediffs
                    ?.OfType<Hediff_MissingPart>()
                    ?.Any() ?? false;
                
                bool hasAddedParts = pawn.health?.hediffSet?.hediffs
                    ?.Any(h => h.def.addedPartProps != null) ?? false;
                
                return hasMissingParts || hasAddedParts;
            }
            
            return FindPartsToRegenerate(pawn, parameters[0]).Any();
        }
        
       private List<BodyPartToRegenerate> FindPartsToRegenerate(Pawn pawn, string searchTerm)
{
    if (pawn?.health?.hediffSet == null) 
        return new List<BodyPartToRegenerate>();
    
    var results = new List<BodyPartToRegenerate>();
    
    // Find missing parts
    var allMissingParts = pawn.health.hediffSet.hediffs
        .OfType<Hediff_MissingPart>()
        .ToList();
    
    // Find prosthetics/bionics (added parts)
    var allProsthetics = pawn.health.hediffSet.hediffs
        .Where(h => h.def.addedPartProps != null)
        .ToList();
    
    Log.Message($"[EchoColony DEBUG] {pawn.LabelShort} has {allMissingParts.Count} missing parts and {allProsthetics.Count} prosthetics");
    
    if (!allMissingParts.Any() && !allProsthetics.Any())
    {
        Log.Warning($"[EchoColony] {pawn.LabelShort} has no missing parts or prosthetics to regenerate");
        return results;
    }
    
    if (string.IsNullOrWhiteSpace(searchTerm))
    {
        // Regenerate all
        foreach (var missing in allMissingParts)
        {
            results.Add(new BodyPartToRegenerate 
            { 
                BodyPart = missing.Part, 
                MissingPartHediff = missing 
            });
        }
        
        foreach (var prosthetic in allProsthetics)
        {
            results.Add(new BodyPartToRegenerate 
            { 
                BodyPart = prosthetic.Part, 
                ProstheticHediff = prosthetic 
            });
        }
        
        Log.Message($"[EchoColony DEBUG] No search term, regenerating all {results.Count} parts");
        return results;
    }
    
    string search = searchTerm.ToLower().Trim();
    Log.Message($"[EchoColony DEBUG] Searching for parts matching: '{search}'");
    
    // Find matching missing parts
    foreach (var part in allMissingParts)
    {
        Log.Message($"[EchoColony DEBUG] Checking missing part: {part.Part.Label} (defName: {part.Part.def.defName})");
        
        if (DoesPartMatch(part.Part, search))
        {
            Log.Message($"[EchoColony DEBUG] ✓ Matched missing part: {part.Part.Label}");
            results.Add(new BodyPartToRegenerate 
            { 
                BodyPart = part.Part, 
                MissingPartHediff = part 
            });
        }
    }
    
    // Find matching prosthetics
    foreach (var prosthetic in allProsthetics)
    {
        Log.Message($"[EchoColony DEBUG] Checking prosthetic: {prosthetic.def.label} on {prosthetic.Part?.Label ?? "null"} (defName: {prosthetic.Part?.def.defName ?? "null"})");
        
        if (prosthetic.Part != null && DoesPartMatch(prosthetic.Part, search))
        {
            Log.Message($"[EchoColony DEBUG] ✓ Matched prosthetic: {prosthetic.def.label} on {prosthetic.Part.Label}");
            results.Add(new BodyPartToRegenerate 
            { 
                BodyPart = prosthetic.Part, 
                ProstheticHediff = prosthetic 
            });
        }
    }
    
    Log.Message($"[EchoColony DEBUG] Found {results.Count} parts matching '{search}'");
    
    return results;
}
        
        private bool DoesPartMatch(BodyPartRecord part, string search)
{
    // Normalize search term - remove "left" and "right" prefixes
    string normalizedSearch = NormalizeSearchTerm(search);
    
    // Match defName (English)
    if (part.def.defName.ToLower().Contains(normalizedSearch))
        return true;
    
    // Match label (localized)
    string normalizedLabel = NormalizeBodyPartLabel(part.Label.ToLower());
    if (normalizedLabel.Contains(normalizedSearch))
        return true;
    
    // Also try original search without normalization
    if (part.def.defName.ToLower().Contains(search))
        return true;
    
    if (part.Label.ToLower().Contains(search))
        return true;
    
    // Match any parent parts (for hierarchical search)
    var currentPart = part;
    while (currentPart.parent != null)
    {
        currentPart = currentPart.parent;
        
        if (currentPart.def.defName.ToLower().Contains(normalizedSearch))
            return true;
        
        string normalizedParentLabel = NormalizeBodyPartLabel(currentPart.Label.ToLower());
        if (normalizedParentLabel.Contains(normalizedSearch))
            return true;
    }
    
    // Partial word matching
    var searchWords = search.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var word in searchWords)
    {
        if (word.Length < 3) continue;
        
        string normalizedWord = NormalizeSearchTerm(word);
        
        if (part.def.defName.ToLower().Contains(normalizedWord))
            return true;
        
        if (normalizedLabel.Contains(normalizedWord))
            return true;
    }
    
    // Common body part relationships
    if (IsRelatedBodyPart(part.def.defName.ToLower(), normalizedSearch))
        return true;
    
    return false;
}

private string NormalizeSearchTerm(string search)
{
    if (string.IsNullOrEmpty(search))
        return search;
    
    search = search.ToLower().Trim();
    
    // Remove directional words (left/right) in multiple languages
    var directionalWords = new[]
    {
        "left", "right",           // English
        "izquierdo", "izquierda", "derecho", "derecha",  // Spanish
        "gauche", "droit", "droite",  // French
        "links", "rechts",         // German
        "sinistra", "destra",      // Italian
        "esquerdo", "esquerda", "direito", "direita",  // Portuguese
        "левый", "правый",         // Russian
        "左", "右"                  // Chinese/Japanese
    };
    
    foreach (var dir in directionalWords)
    {
        // Remove as whole word
        if (search == dir)
            return "";
        
        // Remove as prefix
        if (search.StartsWith(dir))
            search = search.Substring(dir.Length).Trim();
        
        // Remove as suffix
        if (search.EndsWith(dir))
            search = search.Substring(0, search.Length - dir.Length).Trim();
        
        // Remove with space
        search = search.Replace(" " + dir + " ", " ");
        search = search.Replace(dir + " ", "");
        search = search.Replace(" " + dir, "");
    }
    
    return search.Trim();
}

private string NormalizeBodyPartLabel(string label)
{
    if (string.IsNullOrEmpty(label))
        return label;
    
    // Same normalization for body part labels
    return NormalizeSearchTerm(label);
}

        private bool IsRelatedBodyPart(string partDefName, string search)
        {
            var relationships = new Dictionary<string, string[]>
            {
                { "shoulder", new[] { "arm", "hand", "finger" } },
                { "arm", new[] { "shoulder", "hand", "finger" } },
                { "hand", new[] { "arm", "finger", "shoulder" } },
                { "finger", new[] { "hand", "arm" } },
                { "leg", new[] { "foot", "toe" } },
                { "foot", new[] { "leg", "toe" } },
                { "toe", new[] { "foot", "leg" } },
                { "head", new[] { "eye", "ear", "nose", "jaw", "skull" } },
                { "eye", new[] { "head" } },
                { "ear", new[] { "head" } },
                { "nose", new[] { "head" } },
                { "jaw", new[] { "head" } }
            };
            
            foreach (var kvp in relationships)
            {
                if (partDefName.Contains(kvp.Key))
                {
                    if (kvp.Value.Any(term => search.Contains(term)))
                        return true;
                }
                
                if (search.Contains(kvp.Key))
                {
                    if (kvp.Value.Any(term => partDefName.Contains(term)))
                        return true;
                }
            }
            
            return false;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (pawn.health?.hediffSet == null)
                return "No health system available";
            
            string searchTerm = parameters.Length > 0 ? parameters[0] : "";
            
            var partsToRegenerate = FindPartsToRegenerate(pawn, searchTerm);
            
            if (!partsToRegenerate.Any())
                return $"No parts to regenerate found";
            
            var regeneratedParts = new List<string>();
            var droppedProsthetics = new List<string>();
            
            foreach (var partInfo in partsToRegenerate)
            {
                BodyPartRecord bodyPart = partInfo.BodyPart;
                string partLabel = bodyPart.Label;
                
                // Handle prosthetic removal
                if (partInfo.ProstheticHediff != null)
                {
                    string prostheticName = partInfo.ProstheticHediff.def.label ?? partInfo.ProstheticHediff.def.defName;
                    
                    // Try to spawn the prosthetic as an item
                    Thing droppedItem = ProstheticHelper.TrySpawnProstheticItem(pawn, partInfo.ProstheticHediff);
                    
                    // Remove the prosthetic hediff
                    pawn.health.RemoveHediff(partInfo.ProstheticHediff);
                    
                    if (droppedItem != null)
                    {
                        droppedProsthetics.Add(prostheticName);
                        Log.Message($"[EchoColony] Removed and dropped {prostheticName} for {pawn.LabelShort}");
                    }
                    else
                    {
                        Log.Warning($"[EchoColony] Removed {prostheticName} but couldn't spawn item");
                    }
                }
                
                // Handle missing part regeneration
                if (partInfo.MissingPartHediff != null)
                {
                    pawn.health.RemoveHediff(partInfo.MissingPartHediff);
                    
                    // Remove scars on that part
                    var scars = pawn.health.hediffSet.hediffs
                        .Where(h => h.Part == bodyPart && h.IsPermanent())
                        .ToList();
                    
                    foreach (var scar in scars)
                    {
                        pawn.health.RemoveHediff(scar);
                    }
                }
                
                regeneratedParts.Add(partLabel);
                Log.Message($"[EchoColony] Regenerated {partLabel} for {pawn.LabelShort}");
            }
            
            string partsText = regeneratedParts.Count == 1 
                ? regeneratedParts[0] 
                : string.Join(", ", regeneratedParts);
            
            string message = $"{pawn.LabelShort}'s {partsText} regenerated!";
            
            if (droppedProsthetics.Any())
            {
                string prostheticText = droppedProsthetics.Count == 1
                    ? droppedProsthetics[0]
                    : string.Join(", ", droppedProsthetics);
                
                message += $" ({prostheticText} removed and dropped nearby)";
            }
            
            Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
            
            return $"Regenerated {partsText}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            string bodyPart = parameters.Length > 0 ? parameters[0] : "body part";
            return $"{pawn.LabelShort} feels tingling warmth as their {bodyPart} miraculously regenerates.";
        }
        
        private class BodyPartToRegenerate
        {
            public BodyPartRecord BodyPart;
            public Hediff_MissingPart MissingPartHediff;
            public Hediff ProstheticHediff;
        }
    }
}