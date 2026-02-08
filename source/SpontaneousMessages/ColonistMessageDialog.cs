using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Diálogo personalizado para mostrar mensajes espontáneos de colonos
    /// Incluye el retrato del colono y el mensaje
    /// </summary>
    public class ColonistMessageDialog : Window
    {
        private Pawn colonist;
        private string message;
        private TriggerType triggerType;
        private Vector2 scrollPosition = Vector2.zero;

        public ColonistMessageDialog(Pawn colonist, string message, TriggerType triggerType)
        {
            this.colonist = colonist;
            this.message = message;
            this.triggerType = triggerType;
            
            this.forcePause = false;
            this.absorbInputAroundWindow = false;
            this.soundAppear = SoundDefOf.CommsWindow_Open;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(500f, 350f);

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // ═══════════════════════════════════════════════════════════════
            // HEADER - Retrato y nombre del colono
            // ═══════════════════════════════════════════════════════════════
            Rect headerRect = new Rect(inRect.x, curY, inRect.width, 80f);
            
            // Retrato del colono (80x80)
            Rect portraitRect = new Rect(headerRect.x + 10f, headerRect.y + 10f, 80f, 80f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(colonist, new Vector2(80f, 80f), Rot4.South, default, 1f));
            
            // Borde alrededor del retrato
            Widgets.DrawBox(portraitRect);
            
            // Nombre del colono
            Text.Font = GameFont.Medium;
            Rect nameRect = new Rect(portraitRect.xMax + 15f, headerRect.y + 10f, headerRect.width - portraitRect.width - 30f, 30f);
            Widgets.Label(nameRect, colonist.LabelCap);
            
            // Subtítulo (tipo de mensaje)
            Text.Font = GameFont.Small;
            GUI.color = Color.grey;
            Rect subtitleRect = new Rect(portraitRect.xMax + 15f, headerRect.y + 40f, headerRect.width - portraitRect.width - 30f, 20f);
            string subtitle = GetSubtitle();
            Widgets.Label(subtitleRect, subtitle);
            GUI.color = Color.white;
            
            // Información adicional del colono (mood, traits principales)
            Text.Font = GameFont.Tiny;
            Rect infoRect = new Rect(portraitRect.xMax + 15f, headerRect.y + 60f, headerRect.width - portraitRect.width - 30f, 20f);
            string info = GetColonistInfo();
            Widgets.Label(infoRect, info);
            Text.Font = GameFont.Small;
            
            curY += 100f;

            // ═══════════════════════════════════════════════════════════════
            // LÍNEA SEPARADORA
            // ═══════════════════════════════════════════════════════════════
            Widgets.DrawLineHorizontal(inRect.x, curY, inRect.width);
            curY += 10f;

            // ═══════════════════════════════════════════════════════════════
            // MENSAJE DEL COLONO
            // ═══════════════════════════════════════════════════════════════
            Text.Font = GameFont.Small;
            Rect textRect = new Rect(inRect.x + 15f, curY, inRect.width - 30f, inRect.height - curY - 60f);
            
            // Dibujar fondo sutil para el texto
            Widgets.DrawBoxSolid(textRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            
            // Texto con scroll si es muy largo
            Rect textInnerRect = textRect.ContractedBy(10f);
            Widgets.LabelScrollable(textInnerRect, message, ref scrollPosition, false, true);

            // ═══════════════════════════════════════════════════════════════
            // BOTONES
            // ═══════════════════════════════════════════════════════════════
            float buttonY = inRect.y + inRect.height - 40f;
            
            // Botón "Open Chat" - abre el chat con el colono
            Rect openChatRect = new Rect(inRect.x + 10f, buttonY, 120f, 35f);
            if (Widgets.ButtonText(openChatRect, "EchoColony.OpenChat".Translate()))
            {
                // Abrir el chat del colono
                Find.WindowStack.Add(new ColonistChatWindow(colonist));
                
                // Limpiar flag de pending response
                var tracker = SpontaneousMessageTracker.Instance;
                tracker?.SetPendingResponse(colonist, false);
                
                Close();
            }
            
            // Botón "Dismiss" - cierra sin abrir chat
            Rect dismissRect = new Rect(inRect.x + inRect.width - 100f, buttonY, 90f, 35f);
            if (Widgets.ButtonText(dismissRect, "Dismiss"))
            {
                // Solo marcar como no pending si el usuario dismissea
                var tracker = SpontaneousMessageTracker.Instance;
                tracker?.SetPendingResponse(colonist, false);
                
                Close();
            }
        }

        private string GetSubtitle()
        {
            switch (triggerType)
            {
                case TriggerType.Incident:
                    return "Wants to talk about an incident";
                case TriggerType.Random:
                    return "Wants to have a casual chat";
                case TriggerType.CriticalNeed:
                    return "Urgent message";
                case TriggerType.ColonySituation:
                    return "About the colony situation";
                default:
                    return "Wants to talk";
            }
        }

        private string GetColonistInfo()
        {
            string info = "";
            
            // Mood
            if (colonist.needs?.mood != null)
            {
                float mood = colonist.needs.mood.CurLevel;
                string moodDesc = mood > 0.65f ? "Happy" : mood > 0.35f ? "OK" : "Sad";
                info += $"Mood: {moodDesc}";
            }
            
            // Trait principal (primero en la lista)
            if (colonist.story?.traits?.allTraits != null && colonist.story.traits.allTraits.Count > 0)
            {
                var firstTrait = colonist.story.traits.allTraits[0];
                if (!info.NullOrEmpty())
                    info += " • ";
                info += firstTrait.LabelCap;
            }
            
            return info;
        }

        public override void PostClose()
        {
            base.PostClose();
            
            // Reproducir sonido de cierre
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }
}