using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions
{
    /// <summary>
    /// Base class for all player actions that can affect colonists through conversation
    /// </summary>
    public abstract class ActionBase
    {
        /// <summary>
        /// Unique identifier for this action
        /// </summary>
        public abstract string ActionId { get; }
        
        /// <summary>
        /// Category of the action (Health, Mood, Social, Skills, Prisoner, etc.)
        /// </summary>
        public abstract ActionCategory Category { get; }
        
        /// <summary>
        /// Keywords that trigger this action in AI responses
        /// </summary>
        public abstract List<string> TriggerKeywords { get; }
        
        /// <summary>
        /// Description for the AI to understand when to use this action
        /// </summary>
        public abstract string AIDescription { get; }
        
        /// <summary>
        /// Can this action be executed on the given pawn?
        /// </summary>
        public abstract bool CanExecute(Pawn pawn, string[] parameters);
        
        /// <summary>
        /// Execute the action on the pawn
        /// </summary>
        /// <returns>Result message for the AI to incorporate in response</returns>
        public abstract string Execute(Pawn pawn, string[] parameters);
        
        /// <summary>
        /// Get a narrative description of what happened (for AI context)
        /// </summary>
        public abstract string GetNarrativeResult(Pawn pawn, string[] parameters);
        
        /// <summary>
        /// Validation level: how strict should parameter validation be?
        /// </summary>
        public virtual ValidationLevel Validation => ValidationLevel.Moderate;
    }
}