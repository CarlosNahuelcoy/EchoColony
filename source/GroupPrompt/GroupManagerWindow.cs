using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace EchoColony
{
    /// <summary>
    /// Full-screen window for creating, editing, and deleting colonist groups.
    /// Two-panel layout: group list on the left, editor on the right.
    /// </summary>
    public class GroupManagerWindow : Window
    {
        private ColonistGroup selectedGroup;

        // Editor fields — hold temporary edits before saving
        private string editName   = "";
        private string editPrompt = "";
        private Color  editColor  = Color.white;

        private Vector2 groupListScroll;
        private Vector2 colonistListScroll;
        private Vector2 promptScroll;

        private List<Pawn> allColonists = new List<Pawn>();

        private static readonly Color[] PresetColors =
        {
            new Color(0.85f, 0.35f, 0.35f), // Red
            new Color(0.35f, 0.80f, 0.45f), // Green
            new Color(0.35f, 0.55f, 0.90f), // Blue
            new Color(0.90f, 0.85f, 0.35f), // Yellow
            new Color(0.90f, 0.58f, 0.30f), // Orange
            new Color(0.65f, 0.35f, 0.90f), // Purple
            new Color(0.35f, 0.85f, 0.85f), // Cyan
            new Color(0.90f, 0.35f, 0.70f), // Pink
        };

        public GroupManagerWindow()
        {
            this.doCloseX         = true;
            this.forcePause       = true;
            this.absorbInputAroundWindow = true;
            this.closeOnAccept    = false;
            this.closeOnCancel    = true;
            this.draggable        = true;

            RefreshColonists();
        }

        private void RefreshColonists()
        {
            allColonists = Find.Maps
                .SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "Group Prompts");
            Text.Font = GameFont.Small;

            const float leftWidth = 185f;
            const float spacing   = 12f;
            float panelY = 42f;

            Rect leftRect  = new Rect(0f,                  panelY, leftWidth,                      inRect.height - panelY);
            Rect divider   = new Rect(leftWidth + 1f,      panelY, 1f,                             inRect.height - panelY);
            Rect rightRect = new Rect(leftWidth + spacing,  panelY, inRect.width - leftWidth - spacing, inRect.height - panelY);

            // Subtle vertical divider
            Widgets.DrawBoxSolid(divider, new Color(0.4f, 0.4f, 0.4f, 0.5f));

            DrawLeftPanel(leftRect);
            DrawRightPanel(rightRect);
        }

        // ── Left panel: group list + New Group button ──────────────────────────

        private void DrawLeftPanel(Rect rect)
        {
            const float newBtnHeight = 32f;
            const float btnSpacing   = 6f;

            Rect listRect   = new Rect(rect.x, rect.y, rect.width, rect.height - newBtnHeight - btnSpacing);
            Rect newBtnRect = new Rect(rect.x, listRect.yMax + btnSpacing, rect.width, newBtnHeight);

            DrawGroupList(listRect);

            GUI.color = new Color(0.75f, 1f, 0.75f);
            if (Widgets.ButtonText(newBtnRect, "+ New Group"))
            {
                var existing = ColonistGroupManager.GetAllGroups();
                var color    = PresetColors[existing.Count % PresetColors.Length];
                var newGroup = ColonistGroupManager.CreateGroup("New Group", "", color);
                SelectGroup(newGroup);
            }
            GUI.color = Color.white;
        }

        private void DrawGroupList(Rect rect)
        {
            var groups      = ColonistGroupManager.GetAllGroups();
            float rowHeight = 38f;
            float viewH     = Mathf.Max(groups.Count * rowHeight, rect.height);

            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref groupListScroll, viewRect);

            float rowY = 0f;
            foreach (var group in groups)
            {
                Rect row = new Rect(0f, rowY, viewRect.width, rowHeight - 2f);
                bool selected = selectedGroup?.id == group.id;

                if (selected)
                    Widgets.DrawHighlight(row);
                else if (Mouse.IsOver(row))
                    Widgets.DrawLightHighlight(row);

                // Color dot
                Rect dot = new Rect(row.x + 6f, row.y + (row.height - 16f) / 2f, 16f, 16f);
                Widgets.DrawBoxSolid(dot, group.color);
                Widgets.DrawBox(dot);

                // Group name
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(dot.xMax + 8f, row.y, row.width - dot.xMax - 10f, row.height), group.name);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(row))
                    SelectGroup(group);

                rowY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        // ── Right panel: group editor ──────────────────────────────────────────

        private void DrawRightPanel(Rect rect)
        {
            if (selectedGroup == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(rect, "Select a group or create a new one.");
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float curY = rect.y;

            // Name
            curY = DrawLabeledField(rect, curY, "Name", 55f, (fieldRect) =>
            {
                editName = Widgets.TextField(fieldRect, editName);
            }, 28f);

            // Color picker
            Widgets.Label(new Rect(rect.x, curY, 55f, 26f), "Color");
            float colorX = rect.x + 60f;
            foreach (var c in PresetColors)
            {
                Rect dot = new Rect(colorX, curY + 3f, 22f, 22f);
                Widgets.DrawBoxSolid(dot, c);
                if (editColor == c)
                    Widgets.DrawBox(dot, 2);
                if (Widgets.ButtonInvisible(dot))
                    editColor = c;
                colorX += 28f;
            }
            curY += 32f;

            // Prompt textarea
            Widgets.Label(new Rect(rect.x, curY, rect.width, 20f), "Group Prompt:");
            curY += 22f;

            const float colonistSectionH = 150f;
            const float saveRowH         = 36f;
            const float sectionSpacing   = 8f;
            float promptH = rect.height - (curY - rect.y) - colonistSectionH - saveRowH - sectionSpacing * 2f;

            DrawPromptArea(new Rect(rect.x, curY, rect.width, promptH));
            curY += promptH + sectionSpacing;

            // Colonist assignment
            Widgets.Label(new Rect(rect.x, curY, rect.width, 20f), "Assigned colonists:");
            curY += 22f;

            DrawColonistAssignment(new Rect(rect.x, curY, rect.width, colonistSectionH - 22f));
            curY += colonistSectionH - 22f + sectionSpacing;

            // Save / Delete
            DrawSaveDeleteRow(new Rect(rect.x, curY, rect.width, saveRowH));
        }

        private float DrawLabeledField(Rect rect, float curY, string label, float labelWidth, System.Action<Rect> fieldDrawer, float rowHeight)
        {
            Widgets.Label(new Rect(rect.x, curY, labelWidth, rowHeight), label);
            fieldDrawer(new Rect(rect.x + labelWidth + 5f, curY, rect.width - labelWidth - 5f, rowHeight - 2f));
            return curY + rowHeight;
        }

        private void DrawPromptArea(Rect outer)
        {
            Widgets.DrawBoxSolid(outer, Color.black);
            Widgets.DrawBox(outer);

            Rect inner     = outer.ContractedBy(4f);
            float txtWidth = inner.width - 16f;

            GUIStyle style = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap         = true,
                fontSize         = 14,
                normal           = { textColor = Color.white },
                padding          = new RectOffset(5, 5, 5, 5),
            };

            float textH   = style.CalcHeight(new GUIContent(editPrompt), txtWidth);
            float viewH   = Mathf.Max(textH + 20f, inner.height + 20f);
            Rect viewRect = new Rect(0f, 0f, txtWidth, viewH);

            Widgets.BeginScrollView(inner, ref promptScroll, viewRect);
            editPrompt = GUI.TextArea(new Rect(0f, 0f, txtWidth, viewH - 8f), editPrompt, style);
            Widgets.EndScrollView();
        }

        private void DrawColonistAssignment(Rect outer)
        {
            float rowH    = 26f;
            float viewH   = Mathf.Max(allColonists.Count * rowH, outer.height);
            Rect viewRect = new Rect(0f, 0f, outer.width - 16f, viewH);

            Widgets.BeginScrollView(outer, ref colonistListScroll, viewRect);

            float rowY = 0f;
            foreach (var pawn in allColonists)
            {
                Rect row           = new Rect(0f, rowY, viewRect.width, rowH - 2f);
                var  currentGroup  = ColonistGroupManager.GetGroup(pawn);
                bool isInThisGroup = currentGroup?.id == selectedGroup.id;
                bool toggle        = isInThisGroup;

                Widgets.Checkbox(new Vector2(row.x, row.y + (row.height - 24f) / 2f), ref toggle);

                // Label — gray if in another group
                string label = pawn.LabelShort;
                if (currentGroup != null && !isInThisGroup)
                {
                    GUI.color = Color.gray;
                    label    += $"  (in {currentGroup.name})";
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 28f, row.y, row.width - 28f, row.height), label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color   = Color.white;

                // Apply assignment change
                if (toggle != isInThisGroup)
                {
                    if (toggle)
                        ColonistGroupManager.SetGroup(pawn, selectedGroup.id);
                    else
                        ColonistGroupManager.ClearGroup(pawn);
                }

                rowY += rowH;
            }

            Widgets.EndScrollView();
        }

        private void DrawSaveDeleteRow(Rect rect)
        {
            const float btnW    = 100f;
            const float spacing = 10f;

            GUI.color = new Color(0.75f, 1f, 0.75f);
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, btnW, rect.height), "Save"))
            {
                ColonistGroupManager.UpdateGroup(selectedGroup.id, editName, editPrompt, editColor);
                selectedGroup.name   = editName;
                selectedGroup.prompt = editPrompt;
                selectedGroup.color  = editColor;
                Messages.Message($"Group '{editName}' saved.", MessageTypeDefOf.TaskCompletion);
            }
            GUI.color = Color.white;

            GUI.color = new Color(1f, 0.55f, 0.55f);
            if (Widgets.ButtonText(new Rect(rect.x + btnW + spacing, rect.y, btnW, rect.height), "Delete Group"))
            {
                string groupName = selectedGroup.name;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Delete group '{groupName}'? All colonist assignments will be removed.",
                    () =>
                    {
                        ColonistGroupManager.DeleteGroup(selectedGroup.id);
                        selectedGroup = null;
                        editName      = "";
                        editPrompt    = "";
                        editColor     = Color.white;
                    }
                ));
            }
            GUI.color = Color.white;
        }

        private void SelectGroup(ColonistGroup group)
        {
            selectedGroup = group;
            editName      = group.name;
            editPrompt    = group.prompt;
            editColor     = group.color;
            promptScroll  = Vector2.zero;
        }
    }
}