using System.Collections.Generic;
using Verse;
using RimWorld;

namespace EchoColony.Actions.Prisoner
{
    public class ModifyWillAction : ActionBase
    {
        public override string ActionId => "MODIFY_WILL";
        public override ActionCategory Category => ActionCategory.Prisoner;
        
        public override List<string> TriggerKeywords => new List<string> 
        { 
            "suppress", "obey", "obedience", "submit", "compliance",
            "obedecer", "someterse", "suprimir", "reprimir"
        };
        
        public override string AIDescription => 
            "Modify slave's will/suppression to prevent rebellion. Syntax: [ACTION:MODIFY_WILL:amount] (negative = more suppressed/obedient, -0.1 to -0.5 recommended for harsh treatment)";
        
        public override bool CanExecute(Pawn pawn, string[] parameters)
        {
            if (pawn == null || pawn.Dead) return false;
            
            // Solo funciona en esclavos
            if (!pawn.IsSlaveOfColony) return false;
            
            // Verificar que tenemos el parámetro de cantidad
            if (parameters == null || parameters.Length < 1) return false;
            
            // Verificar que es un número válido
            if (!float.TryParse(parameters[0], System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return false;
            
            return true;
        }
        
        public override string Execute(Pawn pawn, string[] parameters)
        {
            if (!pawn.IsSlaveOfColony)
                return $"{pawn.LabelShort} is not a slave";
            
            if (pawn.guest == null)
                return $"{pawn.LabelShort} has no guest system";
            
            // Parsear el valor de modificación
            if (!float.TryParse(parameters[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float amount))
                return "Invalid amount parameter";
            
            // Obtener will actual
            float currentWill = pawn.guest.will;
            float newWill = currentWill + amount;
            
            // Clampear entre 0 y 1
            newWill = UnityEngine.Mathf.Clamp01(newWill);
            
            // Aplicar nuevo valor
            pawn.guest.will = newWill;
            
            string direction = amount < 0 ? "suppressed" : "strengthened";
            
            Messages.Message(
                $"{pawn.LabelShort}'s will {direction}: {currentWill:F2} → {newWill:F2}",
                pawn,
                amount < 0 ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.NegativeEvent
            );
            
            Log.Message($"[EchoColony] Modified will for {pawn.LabelShort}: {currentWill:F2} → {newWill:F2} (change: {amount:F2})");
            
            return $"Modified {pawn.LabelShort}'s will by {amount:F2}";
        }
        
        public override string GetNarrativeResult(Pawn pawn, string[] parameters)
        {
            if (!float.TryParse(parameters[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float amount))
                return "";
            
            if (amount < -0.3f)
                return $"{pawn.LabelShort}'s spirit is thoroughly crushed - rebellion seems impossible now.";
            else if (amount < -0.1f)
                return $"{pawn.LabelShort}'s will to resist weakens significantly.";
            else if (amount < 0f)
                return $"{pawn.LabelShort} feels more compliant, their defiance fading.";
            else if (amount > 0.3f)
                return $"{pawn.LabelShort}'s will to resist surges - they're ready to fight back!";
            else if (amount > 0.1f)
                return $"{pawn.LabelShort} feels a renewed sense of defiance.";
            else
                return $"{pawn.LabelShort}'s spirit strengthens slightly.";
        }
    }
}