using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;

namespace EchoColony
{
    /// <summary>
    /// Displays all exported conversation files and lets the player
    /// choose one to import into the current pawn.
    /// </summary>
    public class ConversationImportWindow : Window
    {
        private readonly Pawn pawn;
        private List<ExportFileInfo> files;
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(680f, 460f);

        public ConversationImportWindow(Pawn pawn)
        {
            this.pawn            = pawn;
            this.doCloseX        = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
            this.forcePause      = true;

            RefreshFileList();
        }

        private void RefreshFileList()
        {
            files = ConversationPorter.GetExportFiles();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ── Title ─────────────────────────────────────────────────────────
            Text.Font   = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f),
                "EchoColony.ImportTitle".Translate(pawn.LabelCap));
            Text.Font   = GameFont.Small;

            // ── Subtitle ──────────────────────────────────────────────────────
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, 32f, inRect.width, 20f),
                "EchoColony.ImportSubtitle".Translate(ConversationPorter.ExportFolder));
            GUI.color = Color.white;

            // ── No files message ──────────────────────────────────────────────
            if (files == null || files.Count == 0)
            {
                Rect emptyRect = new Rect(0f, 60f, inRect.width, 80f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(emptyRect, "EchoColony.ImportNoFiles".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // ── Scrollable file list ──────────────────────────────────────────
            float rowHeight   = 72f;
            float listTop     = 60f;
            float listHeight  = inRect.height - listTop - 10f;
            float viewHeight  = files.Count * (rowHeight + 6f);

            Rect scrollRect = new Rect(0f, listTop, inRect.width, listHeight);
            Rect viewRect   = new Rect(0f, 0f, inRect.width - 20f, viewHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            float y = 0f;
            foreach (var file in files)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);
                DrawFileRow(rowRect, file);
                y += rowHeight + 6f;
            }

            Widgets.EndScrollView();
        }

        private void DrawFileRow(Rect row, ExportFileInfo file)
        {
            // Background
            Widgets.DrawBoxSolid(row, new Color(0.15f, 0.18f, 0.22f, 0.8f));

            // Hover highlight
            if (Mouse.IsOver(row))
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));

            float pad = 10f;

            // Pawn name
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.92f, 0.7f);
            Widgets.Label(new Rect(row.x + pad, row.y + 6f, 260f, 22f), file.displayName);
            GUI.color = Color.white;

            // Export date
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.7f, 0.8f);
            Widgets.Label(new Rect(row.x + pad, row.y + 28f, 300f, 18f), file.exportDate);
            GUI.color = Color.white;

            // Pawn summary (gender, age, backstory, traits)
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(new Rect(row.x + pad, row.y + 46f, row.width - 160f - pad * 2f, 18f),
                file.pawnSummary);
            GUI.color = Color.white;

            // Stats badge
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.85f, 0.6f, 0.9f);
            string stats = $"{file.lineCount} lines  •  {file.memoryCount} memories";
            Widgets.Label(new Rect(row.xMax - 210f, row.y + 6f, 145f, 18f), stats);
            GUI.color = Color.white;

            float btnY = row.y + row.height / 2f - 15f;

            // Import button
            Text.Font = GameFont.Small;
            Rect importBtn = new Rect(row.xMax - 200f, btnY, 90f, 30f);
            if (Widgets.ButtonText(importBtn, "EchoColony.ImportButton".Translate()))
                ConfirmImport(file);

            // Delete button
            GUI.color = new Color(1f, 0.4f, 0.4f);
            Rect deleteBtn = new Rect(row.xMax - 100f, btnY, 90f, 30f);
            if (Widgets.ButtonText(deleteBtn, "EchoColony.DeleteButton".Translate()))
                ConfirmDelete(file);
            GUI.color = Color.white;

            // Filename tooltip
            TooltipHandler.TipRegion(row, Path.GetFileName(file.filePath));

            Text.Font = GameFont.Small;
        }

        private void ConfirmDelete(ExportFileInfo file)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.DeleteConfirm".Translate(file.displayName),
                () => DoDelete(file),
                destructive: true
            ));
        }

        private void DoDelete(ExportFileInfo file)
        {
            try
            {
                File.Delete(file.filePath);
                RefreshFileList();
                Messages.Message(
                    "EchoColony.DeleteSuccess".Translate(file.displayName),
                    MessageTypeDefOf.TaskCompletion,
                    false
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Failed to delete export file: {ex.Message}");
                Messages.Message(
                    "EchoColony.DeleteFailed".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false
                );
            }
        }

        private void ConfirmImport(ExportFileInfo file)
        {
            string message = "EchoColony.ImportConfirm".Translate(
                file.displayName,
                pawn.LabelCap,
                file.lineCount,
                file.memoryCount
            );

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                message,
                () => DoImport(file),
                destructive: false
            ));
        }

        private void DoImport(ExportFileInfo file)
        {
            bool ok = ConversationPorter.Import(pawn, file.filePath);

            if (ok)
            {
                Messages.Message(
                    "EchoColony.ImportSuccess".Translate(file.displayName, pawn.LabelCap),
                    MessageTypeDefOf.TaskCompletion,
                    false
                );
                Close();
            }
            else
            {
                Messages.Message(
                    "EchoColony.ImportFailed".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false
                );
            }
        }
    }
}