using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace EchoColony.Actions
{
    public static class ActionRegistry
    {
        private static Dictionary<string, ActionBase> actions = new Dictionary<string, ActionBase>();
        private static bool initialized = false;
        
        public static void Initialize()
        {
            if (initialized) return;
            
            Log.Message("[EchoColony] Auto-discovering actions...");
            
            try
            {
                // Buscar todas las clases que heredan de ActionBase
                var actionTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ActionBase)));
                
                foreach (var type in actionTypes)
                {
                    try
                    {
                        var action = (ActionBase)Activator.CreateInstance(type);
                        actions[action.ActionId.ToUpper()] = action;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Failed to register action {type.Name}: {ex.Message}");
                    }
                }
                
                initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Critical error during action registration: {ex}");
            }
        }
        
        private static int GetCategoryCount()
        {
            return actions.Values
                .Select(a => a.Category)
                .Distinct()
                .Count();
        }
        
        public static ActionBase GetAction(string actionId)
        {
            if (!initialized) Initialize();
            return actions.TryGetValue(actionId.ToUpper(), out var action) ? action : null;
        }
        
        public static List<ActionBase> GetAllActions()
        {
            if (!initialized) Initialize();
            return actions.Values.ToList();
        }
        
        public static List<ActionBase> GetActionsByCategory(ActionCategory category)
        {
            if (!initialized) Initialize();
            return actions.Values.Where(a => a.Category == category).ToList();
        }
        
        public static List<ActionBase> GetAvailableActionsForPawn(Pawn pawn)
        {
            if (!initialized) Initialize();
            return actions.Values
                .Where(a => a.CanExecute(pawn, new string[0]))
                .ToList();
        }
    }
}