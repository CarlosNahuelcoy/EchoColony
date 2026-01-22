namespace EchoColony.Actions
{
    public enum ActionCategory
    {
        Health,
        Mood,
        Social,
        Skills,
        Prisoner,
        Work,
        Needs,
        Transform,
        Inventory,
        Movement,
        Special
    }
    
    public enum ValidationLevel
    {
        Permissive,
        Moderate,
        Strict
    }
    
    public enum ActionResult
    {
        Success,
        Failed,
        PartialSuccess,
        Blocked
    }
}