using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using System.Linq;

namespace EchoColony
{
    /// <summary>
    /// Notificación discreta del Storyteller en la esquina superior derecha
    /// Similar a las notificaciones del Narrator's Voice pero integrada con EchoColony
    /// </summary>
    public class StorytellerMessageDialog : Window
    {
        private string message;
        private string storytellerDefName;
        private Texture2D storytellerPortrait;
        private float creationTime;
        private bool isTest;
        private bool textureLoaded = false;
        
        private Vector2 scrollPosition = Vector2.zero;
        private const float NOTIFICATION_WIDTH = 400f;
        private const float NOTIFICATION_HEIGHT = 200f;
        private const float MARGIN = 10f;

        public StorytellerMessageDialog(string message, string storytellerDefName, bool isTest = false)
        {
            this.message = message;
            this.storytellerDefName = storytellerDefName;
            this.isTest = isTest;
            this.creationTime = Time.time;
            
            // ✅ Configuración para notificación NO invasiva
            this.forcePause = false; // NO pausar el juego
            this.preventCameraMotion = false; // Permitir movimiento de cámara
            this.absorbInputAroundWindow = false; // NO bloquear clicks
            this.soundAppear = SoundDefOf.CommsWindow_Open;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = true;
            this.draggable = false; // NO se puede arrastrar
            
            // Reproducir sonido si está habilitado
            if (MyMod.Settings?.storytellerMessagePlaySound == true)
            {
                PlayNotificationSound();
            }
        }

        private void LoadStorytellerPortrait()
        {
            try
            {
                string texturePath = $"UI/Storyteller/{storytellerDefName}";
                storytellerPortrait = ContentFinder<Texture2D>.Get(texturePath, false);
                
                if (storytellerPortrait == null)
                {
                    storytellerPortrait = ContentFinder<Texture2D>.Get("UI/Storyteller/Cassandra", false);
                }
                
                if (storytellerPortrait == null)
                {
                    storytellerPortrait = BaseContent.BadTex;
                }
                
                textureLoaded = true;
            }
            catch (System.Exception ex)
            {
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Warning($"[EchoColony] Could not load storyteller portrait: {ex.Message}");
                }
                storytellerPortrait = BaseContent.BadTex;
                textureLoaded = true;
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                SoundDef soundToPlay = SoundDefOf.TinyBell;
                soundToPlay.PlayOneShotOnCamera();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[EchoColony] Failed to play notification sound: {ex.Message}");
            }
        }

        // ✅ Posición en la esquina superior derecha
        public override Vector2 InitialSize => new Vector2(NOTIFICATION_WIDTH, NOTIFICATION_HEIGHT);

        public override void SetInitialSizeAndPosition()
        {
            // Esquina superior derecha con margen
            windowRect = new Rect(
                UI.screenWidth - NOTIFICATION_WIDTH - MARGIN,
                MARGIN,
                NOTIFICATION_WIDTH,
                NOTIFICATION_HEIGHT
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Lazy load textura
            if (!textureLoaded)
            {
                LoadStorytellerPortrait();
            }
            
            // ✅ Fondo semi-transparente oscuro
            Widgets.DrawBoxSolid(inRect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            Widgets.DrawBox(inRect);
            
            float curY = 5f;

            // Header compacto con icono y nombre
            Rect headerRect = new Rect(inRect.x + 5f, curY, inRect.width - 10f, 40f);

            // Icono pequeño del storyteller
            if (storytellerPortrait != null && storytellerPortrait != BaseContent.BadTex)
            {
                Rect iconRect = new Rect(headerRect.x, headerRect.y, 35f, 35f);
                Widgets.DrawTextureFitted(iconRect, storytellerPortrait, 1f);
            }

            // Nombre del storyteller
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(headerRect.x + 40f, headerRect.y, headerRect.width - 40f, 20f);
            string storytellerName = GetStorytellerDisplayName();
            Widgets.Label(nameRect, storytellerName);

            // Subtítulo
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect subtitleRect = new Rect(headerRect.x + 40f, headerRect.y + 20f, headerRect.width - 40f, 15f);
            string subtitle = isTest ? "Test Message" : "Spontaneous Comment";
            Widgets.Label(subtitleRect, subtitle);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            curY += 45f;

            // Línea separadora sutil
            Widgets.DrawLineHorizontal(inRect.x + 5f, curY, inRect.width - 10f, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            curY += 5f;

            // Texto del mensaje
            Text.Font = GameFont.Small;
            Rect textRect = new Rect(inRect.x + 10f, curY, inRect.width - 20f, inRect.height - curY - 45f);
            Widgets.LabelScrollable(textRect, message, ref scrollPosition, false, true);

            // Botones compactos en la parte inferior
            float buttonY = inRect.y + inRect.height - 35f;
            float buttonWidth = 100f;
            float spacing = 5f;

            // Botón "Chat" compacto
            Rect openChatRect = new Rect(inRect.x + 5f, buttonY, buttonWidth, 25f);
            if (Widgets.ButtonText(openChatRect, "Chat"))
            {
                OpenStorytellerChat();
                Close();
            }

            // Botón "X" pequeño
            Rect closeRect = new Rect(inRect.x + buttonWidth + spacing + 5f, buttonY, 60f, 25f);
            if (Widgets.ButtonText(closeRect, "Close"))
            {
                Close();
            }
            
            Text.Font = GameFont.Small;
        }

        private string GetStorytellerDisplayName()
        {
            try
            {
                var storytellerDef = DefDatabase<StorytellerDef>.GetNamedSilentFail(storytellerDefName);
                if (storytellerDef != null && !string.IsNullOrEmpty(storytellerDef.label))
                {
                    return storytellerDef.label;
                }
            }
            catch { }

            return CleanDefNameForDisplay(storytellerDefName);
        }

        private string CleanDefNameForDisplay(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "Storyteller";
            return defName.Replace("Storyteller", "").Replace("_", " ").Trim();
        }

        private void OpenStorytellerChat()
        {
            try
            {
                var existingWindow = Find.WindowStack.Windows
                    .OfType<MainTabWindow_StorytellerChat>()
                    .FirstOrDefault();

                if (existingWindow != null)
                {
                    existingWindow.Close();
                }

                Find.WindowStack.Add(new MainTabWindow_StorytellerChat());

                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message("[EchoColony] Opened storyteller chat from spontaneous message");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error opening storyteller chat: {ex.Message}");
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Auto-cerrar después del delay configurado
            if (MyMod.Settings?.storytellerMessageAutoClose == true)
            {
                float elapsedTime = Time.time - creationTime;
                float autoCloseDelay = MyMod.Settings?.storytellerMessageAutoCloseSeconds ?? 8f;

                if (elapsedTime >= autoCloseDelay)
                {
                    Close();
                }
            }
        }
    }
}