using Verse;

namespace EchoColony
{
    public class RelationToneAIPromptDef : Def
    {
        // El defName de la relación que queremos mapear (ej: "MasterPupilRelation")
        public string relationDefName;

        // El texto que se inyectará en el prompt
        public string tone;
    }
}
