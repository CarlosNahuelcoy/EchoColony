using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace EchoColony
{
    public class GeneAIPromptDef : Def
    {
        public string targetGene;        // defName del GeneDef en RimWorld
        public string customLabel;       // Nombre simplificado (opcional)
        public string promptDescription; // Explicación concisa y optimizada para la IA
    }
}
