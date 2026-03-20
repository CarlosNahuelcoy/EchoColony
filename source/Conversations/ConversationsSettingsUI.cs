using UnityEngine;
using Verse;
using RimWorld;

namespace EchoColony
{
    /// <summary>
    /// Settings UI block for the Pawn Conversations (bubble dialogue) system.
    /// Call DrawConversationsSection(listing, settings) from your main
    /// mod settings DoWindowContents method.
    /// </summary>
    public static class ConversationsSettingsUI
    {
        private static Vector2 promptScroll = Vector2.zero;
        private const float PromptBoxHeight = 80f;

        // ── Public entry point ────────────────────────────────────────────────────

        public static void DrawConversationsSection(Listing_Standard listing, GeminiSettings s)
        {
            // ── Section header ────────────────────────────────────────────────────
            listing.GapLine();
            Text.Font = GameFont.Medium;
            listing.Label("EchoColony.ConvSectionHeader".Translate());
            Text.Font = GameFont.Small;
            listing.Label("EchoColony.ConvSectionDesc".Translate());
            listing.Gap(4f);

            // ── Master switch ─────────────────────────────────────────────────────
            listing.CheckboxLabeled(
                "EchoColony.ConvEnableLabel".Translate(),
                ref s.enablePawnConversations,
                "EchoColony.ConvEnableTooltip".Translate());

            if (!s.enablePawnConversations)
            {
                listing.Gap(4f);
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                listing.Label("EchoColony.ConvDisabledHint".Translate());
                GUI.color = Color.white;
                return; // Hide all other settings when disabled
            }

            listing.Gap(6f);

            // ── Lines per pawn ────────────────────────────────────────────────────
            listing.Label("EchoColony.ConvLinesLabel".Translate(s.conversationLinesPerPawn, s.conversationLinesPerPawn * 2),
                tooltip: "EchoColony.ConvLinesTooltip".Translate());

            s.conversationLinesPerPawn = Mathf.RoundToInt(
                listing.Slider(s.conversationLinesPerPawn, 1f, 3f));

            listing.Gap(2f);

            // ── Bubble delay ──────────────────────────────────────────────────────
            listing.Label("EchoColony.ConvDelayLabel".Translate(s.conversationBubbleDelay.ToString("F1")),
                tooltip: "EchoColony.ConvDelayTooltip".Translate());

            s.conversationBubbleDelay = listing.Slider(s.conversationBubbleDelay, 0.5f, 4f);
            s.conversationBubbleDelay = Mathf.Round(s.conversationBubbleDelay * 10f) / 10f;

            listing.Gap(2f);

            // ── Cooldown ──────────────────────────────────────────────────────────
            string cooldownLabel = s.conversationCooldownHours == 0
                ? "EchoColony.ConvCooldownNoneLabel".Translate()
                : "EchoColony.ConvCooldownLabel".Translate(s.conversationCooldownHours);
            listing.Label(cooldownLabel,
                tooltip: "EchoColony.ConvCooldownTooltip".Translate());

            s.conversationCooldownHours = Mathf.RoundToInt(
                listing.Slider(s.conversationCooldownHours, 0f, 24f));

            listing.Gap(6f);

            // ── Animal mode ───────────────────────────────────────────────────────
            listing.Label("EchoColony.ConvAnimalModeLabel".Translate(),
                tooltip: "EchoColony.ConvAnimalModeTooltip".Translate());

            Rect animalModeRect = listing.GetRect(28f);
            DrawThreeWayToggle(
                animalModeRect,
                s.conversationAnimalMode, v => s.conversationAnimalMode = v,
                ConversationAnimalMode.Disabled,        "EchoColony.ConvAnimalModeDisabled".Translate(),
                ConversationAnimalMode.IntelligentOnly, "EchoColony.ConvAnimalModeIntelligentOnly".Translate(),
                ConversationAnimalMode.All,             "EchoColony.ConvAnimalModeAll".Translate(),
                "");

            listing.Gap(8f);

            // ── Filters ───────────────────────────────────────────────────────────
            listing.Label("EchoColony.ConvFiltersLabel".Translate(), tooltip: "EchoColony.ConvFiltersLabel".Translate());

            listing.CheckboxLabeled(
                "EchoColony.ConvIncludePrisoners".Translate(),
                ref s.conversationIncludePrisoners,
                "EchoColony.ConvIncludePrisonersTooltip".Translate());

            listing.CheckboxLabeled(
                "EchoColony.ConvIncludeSlaves".Translate(),
                ref s.conversationIncludeSlaves,
                "EchoColony.ConvIncludeSlavesTooltip".Translate());

            listing.Gap(4f);

            // ── Minimum opinion ───────────────────────────────────────────────────
            string opinionLabel = s.conversationMinOpinion <= -100
                ? "EchoColony.ConvMinOpinionNoFilter".Translate()
                : "EchoColony.ConvMinOpinionLabel".Translate(s.conversationMinOpinion);
            listing.Label(opinionLabel,
                tooltip: "EchoColony.ConvMinOpinionTooltip".Translate());

            s.conversationMinOpinion = Mathf.RoundToInt(
                listing.Slider(s.conversationMinOpinion, -100f, 100f));

            listing.Gap(4f);

            // ── Max colony size ───────────────────────────────────────────────────
            string sizeLabel = s.conversationMaxColonySize == 0
                ? "EchoColony.ConvMaxColonySizeNever".Translate()
                : "EchoColony.ConvMaxColonySizeLabel".Translate(s.conversationMaxColonySize);
            listing.Label(sizeLabel,
                tooltip: "EchoColony.ConvMaxColonySizeTooltip".Translate());

            s.conversationMaxColonySize = Mathf.RoundToInt(
                listing.Slider(s.conversationMaxColonySize, 0f, 200f));

            // Snap to 0 when at the bottom to make "disabled" state clear
            if (s.conversationMaxColonySize < 5)
                s.conversationMaxColonySize = 0;

            listing.Gap(8f);

            // ── Global conversation prompt ────────────────────────────────────────
            listing.Label("EchoColony.ConvGlobalPromptLabel".Translate(),
                tooltip: "EchoColony.ConvGlobalPromptTooltip".Translate());

            Rect outerRect = listing.GetRect(PromptBoxHeight + 4f);
            Rect scrollRect = new Rect(outerRect.x, outerRect.y, outerRect.width, PromptBoxHeight);
            float innerHeight = Mathf.Max(PromptBoxHeight,
                Text.CalcHeight(s.conversationGlobalPrompt, outerRect.width - 20f));
            Rect viewRect = new Rect(0f, 0f, outerRect.width - 16f, innerHeight);

            Widgets.BeginScrollView(scrollRect, ref promptScroll, viewRect);
            s.conversationGlobalPrompt = Widgets.TextArea(
                new Rect(0f, 0f, viewRect.width, viewRect.height),
                s.conversationGlobalPrompt);
            Widgets.EndScrollView();

            listing.Gap(4f);
        }

        // ── Three-way toggle (Disabled / Option A / Option B) ─────────────────────

        private static void DrawThreeWayToggle<T>(
            Rect rect,
            T current, System.Action<T> setter,
            T valA, string labelA,
            T valB, string labelB,
            T valC, string labelC,
            string tooltip = "")
        {
            float w = rect.width / 3f;
            Rect rA = new Rect(rect.x,          rect.y, w - 2f, rect.height);
            Rect rB = new Rect(rect.x + w,      rect.y, w - 2f, rect.height);
            Rect rC = new Rect(rect.x + w * 2f, rect.y, w - 2f, rect.height);

            if (!string.IsNullOrEmpty(tooltip))
                TooltipHandler.TipRegion(rect, tooltip);

            T capturedA = valA, capturedB = valB, capturedC = valC;
            DrawToggleButton(rA, labelA, current.Equals(valA), () => setter(capturedA));
            DrawToggleButton(rB, labelB, current.Equals(valB), () => setter(capturedB));
            DrawToggleButton(rC, labelC, current.Equals(valC), () => setter(capturedC));
        }

        private static void DrawToggleButton(Rect rect, string label, bool active, System.Action onClick)
        {
            Color bg = active
                ? new Color(0.25f, 0.55f, 0.25f, 0.85f)
                : new Color(0.2f,  0.2f,  0.2f,  0.6f);

            Widgets.DrawBoxSolid(rect, bg);

            if (active)
            {
                Widgets.DrawBox(rect, 1);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color   = active ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(rect, label);
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(rect))
                onClick?.Invoke();
        }
    }
}