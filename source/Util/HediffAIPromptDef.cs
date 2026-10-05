using System.Collections.Generic;
using Verse;

namespace EchoColony
{
    public class HediffStagePrompt
    {
        // Stage index of the HediffStage in RimWorld (0-based).
        public int stageIndex = -1;

        //Name of the stage in RimWorld (optional: "minor", "major", "extreme")
        public string stageLabel;
        //Custom label for the stage (optional: "mild", "severe", "critical")
        public string customLabel;
        //Functional note for the stage (optional: "brain chip forcing continuous dopamine release")
        public string functionalNote;
        //Behavioral directive for the stage (optional: "You are unnaturally happy at all times.")
        public string behavioralDirective;
    }
    /// <summary>
    /// Definición XML para parchear cómo la IA interpreta prótesis, implantes y afecciones.
    /// </summary>
    public class HediffAIPromptDef : Def
    {
        // defName del Hediff al que aplica (ej: "Joywire", "PegLeg", "Alzheimers")
        public string targetHediff;

        // Nombre limpio alternativo (opcional, ej: "peg leg")
        public string customLabel;

        // Nota explicativa funcional inline (opcional, ej: "brain chip forcing continuous dopamine release")
        public string functionalNote;

        // Instrucción de rol/conducta (opcional, ej: "You are unnaturally happy at all times.")
        public string behavioralDirective;

        //Set stages for hediff prompting (optional, if not set, will use default RimWorld stage labels)
        public List<HediffStagePrompt> stages;
    }
}

