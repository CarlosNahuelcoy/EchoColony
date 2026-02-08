using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Letter que notifica cuando un colono quiere hablar
    /// Abre un diálogo personalizado en lugar del chat directo
    /// </summary>
    public class ColonistMessageLetter : Letter
    {
        public Pawn colonist;
        public string messageText;
        public TriggerType triggerType;
        public float creationTime;

        public ColonistMessageLetter()
        {
            // Constructor vacío requerido para serialización
        }

        public ColonistMessageLetter(Pawn colonist, string message, TriggerType triggerType)
        {
            this.colonist = colonist;
            this.messageText = message;
            this.triggerType = triggerType;
            this.creationTime = Time.time;

            // Configurar la letter base
            this.def = LetterDefOf.NeutralEvent;
            this.lookTargets = new LookTargets(colonist);

            // SOLUCIÓN: Usar reflexión para establecer el label (igual que NarratorCommentLetter)
            try
            {
                var labelField = typeof(Letter).GetField("label", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (labelField != null)
                {
                    // Crear el TaggedString primero, LUEGO asignarlo
                    TaggedString labelText = colonist.LabelCap;
                    labelField.SetValue(this, labelText);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[EchoColony] Could not set letter label: {ex.Message}");
            }
        }

        public override void OpenLetter()
        {
            // Reproducir sonido
            SoundDefOf.Click.PlayOneShotOnCamera();

            // Abrir el diálogo personalizado
            Find.WindowStack.Add(new ColonistMessageDialog(colonist, messageText, triggerType));

            // Remover la letter
            Find.LetterStack.RemoveLetter(this);
        }

        public override string GetMouseoverText()
        {
            // Preview del mensaje en el hover
            string preview = messageText;
            if (preview.Length > 80)
            {
                preview = preview.Substring(0, 77) + "...";
            }

            return $"{colonist.LabelCap}: {preview}";
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref colonist, "colonist");
            Scribe_Values.Look(ref messageText, "messageText");
            Scribe_Values.Look(ref triggerType, "triggerType");
            Scribe_Values.Look(ref creationTime, "creationTime");
        }

        public override bool CanDismissWithRightClick => true;

        public override bool CanShowInLetterStack => true;

        // Cleanup cuando se descarta sin abrir
        public override void Removed()
        {
            base.Removed();

            // Si el jugador descartó la letter sin abrir el diálogo,
            // limpiamos el flag de pending response
            var tracker = SpontaneousMessageTracker.Instance;
            tracker?.SetPendingResponse(colonist, false);
        }
    }
}