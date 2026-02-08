using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Ventana para configurar mensajes espontáneos por colono individual
    /// Muestra tabla con todos los colonos y permite ajustar settings
    /// </summary>
    public class ColonistMessageConfigWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private List<Pawn> colonists;

        private const float ROW_HEIGHT = 50f;
        private const float NAME_WIDTH = 150f;
        private const float ENABLED_WIDTH = 80f;
        private const float MAX_PER_DAY_WIDTH = 80f;
        private const float COOLDOWN_WIDTH = 100f;
        private const float TRIGGERS_WIDTH = 100f;
        private const float PADDING = 10f;

        public ColonistMessageConfigWindow()
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnAccept = false;
            this.closeOnCancel = true;

            RefreshColonistList();
        }

        private void RefreshColonistList()
        {
            colonists = new List<Pawn>();
            foreach (var map in Find.Maps)
            {
                colonists.AddRange(map.mapPawns.FreeColonistsSpawned);
            }
            colonists = colonists.OrderBy(p => p.LabelShort).ToList();
        }

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "EchoColony.ConfigWindowTitle".Translate());
            Text.Font = GameFont.Small;

            float curY = 45f;

            // Botones globales
            Rect buttonsRect = new Rect(0f, curY, inRect.width, 30f);
            DrawGlobalButtons(buttonsRect);
            curY += 35f;

            // Headers de tabla
            Rect headerRect = new Rect(0f, curY, inRect.width - 16f, 30f);
            DrawTableHeader(headerRect);
            curY += 35f;

            // Scroll view con colonos
            float scrollHeight = inRect.height - curY - 10f;
            Rect scrollOutRect = new Rect(0f, curY, inRect.width, scrollHeight);
            float viewHeight = colonists.Count * ROW_HEIGHT + 10f;
            Rect scrollViewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect);

            float rowY = 0f;
            for (int i = 0; i < colonists.Count; i++)
            {
                Rect rowRect = new Rect(0f, rowY, scrollViewRect.width, ROW_HEIGHT);
                DrawColonistRow(rowRect, colonists[i], i);
                rowY += ROW_HEIGHT;
            }

            Widgets.EndScrollView();
        }

        private void DrawGlobalButtons(Rect rect)
        {
            float buttonWidth = 150f;
            float spacing = 10f;

            // Botón "Apply to All"
            Rect applyAllRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(applyAllRect, "EchoColony.ApplyToAll".Translate()))
            {
                ApplyDefaultsToAll();
            }

            // Botón "Reset to Defaults"
            Rect resetRect = new Rect(applyAllRect.xMax + spacing, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(resetRect, "EchoColony.ResetToDefaults".Translate()))
            {
                ResetAllToDefaults();
            }

            // Info text
            GUI.color = Color.gray;
            Rect infoRect = new Rect(resetRect.xMax + spacing * 2, rect.y, rect.width - resetRect.xMax - spacing * 2, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(infoRect, $"{colonists.Count} colonists configured");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawTableHeader(Rect rect)
        {
            float curX = PADDING;

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            // Name
            Widgets.Label(new Rect(curX, rect.y, NAME_WIDTH, rect.height), "Name");
            curX += NAME_WIDTH + PADDING;

            // Enabled
            Widgets.Label(new Rect(curX, rect.y, ENABLED_WIDTH, rect.height), "EchoColony.EnabledColumn".Translate());
            curX += ENABLED_WIDTH + PADDING;

            // Max/Day
            Widgets.Label(new Rect(curX, rect.y, MAX_PER_DAY_WIDTH, rect.height), "EchoColony.MaxPerDayColumn".Translate());
            curX += MAX_PER_DAY_WIDTH + PADDING;

            // Cooldown
            Widgets.Label(new Rect(curX, rect.y, COOLDOWN_WIDTH, rect.height), "EchoColony.CooldownColumn".Translate());
            curX += COOLDOWN_WIDTH + PADDING;

            // Triggers
            Widgets.Label(new Rect(curX, rect.y, TRIGGERS_WIDTH, rect.height), "EchoColony.TriggersColumn".Translate());

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void DrawColonistRow(Rect rect, Pawn pawn, int index)
        {
            // Alternar color de fondo
            if (index % 2 == 0)
            {
                Widgets.DrawLightHighlight(rect);
            }

            var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
            if (settings == null) return;

            float curX = PADDING;

            // Portrait + Name
            Rect portraitRect = new Rect(curX, rect.y + 5f, 40f, 40f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(40f, 40f), Rot4.South, default, 1f));
            
            Rect nameRect = new Rect(portraitRect.xMax + 5f, rect.y, NAME_WIDTH - 45f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, pawn.LabelShort);
            Text.Anchor = TextAnchor.UpperLeft;
            curX += NAME_WIDTH + PADDING;

            // Enabled checkbox
            Rect enabledRect = new Rect(curX + 20f, rect.y + (rect.height - 24f) / 2f, 24f, 24f);
            bool enabled = settings.enabled;
            Widgets.Checkbox(enabledRect.position, ref enabled);
            if (enabled != settings.enabled)
            {
                settings.enabled = enabled;
            }
            curX += ENABLED_WIDTH + PADDING;

            // Max Per Day slider
            if (settings.enabled)
            {
                Rect maxDayRect = new Rect(curX, rect.y + (rect.height - 30f) / 2f, MAX_PER_DAY_WIDTH, 30f);
                int maxPerDay = settings.maxMessagesPerDay;
                settings.maxMessagesPerDay = (int)Widgets.HorizontalSlider(
                    maxDayRect,
                    settings.maxMessagesPerDay,
                    1f,
                    3f,
                    false,
                    settings.maxMessagesPerDay.ToString()
                );
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(curX, rect.y, MAX_PER_DAY_WIDTH, rect.height), "-");
                GUI.color = Color.white;
            }
            curX += MAX_PER_DAY_WIDTH + PADDING;

            // Cooldown slider
            if (settings.enabled)
            {
                Rect cooldownRect = new Rect(curX, rect.y + (rect.height - 30f) / 2f, COOLDOWN_WIDTH, 30f);
                settings.cooldownHours = Widgets.HorizontalSlider(
                    cooldownRect,
                    settings.cooldownHours,
                    1f,
                    48f,
                    false,
                    settings.cooldownHours.ToString("F0") + "EchoColony.Hours".Translate()
                );
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(curX, rect.y, COOLDOWN_WIDTH, rect.height), "-");
                GUI.color = Color.white;
            }
            curX += COOLDOWN_WIDTH + PADDING;

            // Triggers button
            if (settings.enabled)
            {
                Rect triggersRect = new Rect(curX, rect.y + (rect.height - 30f) / 2f, TRIGGERS_WIDTH, 30f);
                int enabledCount = settings.allowedTriggers.Count;
                if (Widgets.ButtonText(triggersRect, $"{enabledCount}/4"))
                {
                    ShowTriggerMenu(pawn, settings);
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(curX, rect.y, TRIGGERS_WIDTH, rect.height), "-");
                GUI.color = Color.white;
            }
        }

        private void ShowTriggerMenu(Pawn pawn, ColonistMessageSettings settings)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (TriggerType trigger in System.Enum.GetValues(typeof(TriggerType)))
            {
                bool isAllowed = settings.allowedTriggers.Contains(trigger);
                string label = trigger.ToString();
                string checkmark = isAllowed ? "✓ " : "";

                options.Add(new FloatMenuOption(checkmark + label, () =>
                {
                    settings.SetTriggerAllowed(trigger, !isAllowed);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyDefaultsToAll()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Apply default settings to all colonists?",
                () =>
                {
                    int defaults = MyMod.Settings.defaultMaxMessagesPerColonistPerDay;
                    float cooldown = MyMod.Settings.defaultColonistCooldownHours;

                    foreach (var pawn in colonists)
                    {
                        var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
                        settings.maxMessagesPerDay = defaults;
                        settings.cooldownHours = cooldown;
                    }

                    Messages.Message("Settings applied to all colonists", MessageTypeDefOf.TaskCompletion);
                }
            ));
        }

        private void ResetAllToDefaults()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Reset all colonist settings to defaults?",
                () =>
                {
                    foreach (var pawn in colonists)
                    {
                        var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
                        settings.ResetToDefaults();
                    }

                    Messages.Message("All settings reset", MessageTypeDefOf.TaskCompletion);
                }
            ));
        }
    }
}