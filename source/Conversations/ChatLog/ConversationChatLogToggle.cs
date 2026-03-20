using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Handles all show/hide interactions for the conversation chat log:
    ///   • "—" button on the overlay title bar (hide)
    ///   • Restore tab on the map when overlay is hidden
    ///   • Keybinding (configurable in Options → Controls)
    /// </summary>
    public static class ConversationChatLogToggle
    {
        // ── Restore tab layout ─────────────────────────────────────────────────
        private const float TabW = 130f;
        private const float TabH =  22f;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color ColTab    = new Color(0.15f, 0.15f, 0.15f, 0.80f);
        private static readonly Color ColTabHov = new Color(0.28f, 0.28f, 0.28f, 0.92f);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call once per frame from a map-GUI Harmony patch.
        /// Draws the overlay (or restore tab) and handles the keybinding.
        /// </summary>
        public static void OnMapGUI()
        {
            // Only draw when a map is active and conversations are enabled
            if (Current.Game == null || Find.CurrentMap == null) return;
            if (MyMod.Settings?.enablePawnConversations != true) return;

            HandleKeybinding();

            if (ConversationChatLogRenderer.IsVisible)
            {
                ConversationChatLogRenderer.DrawOverlay();
            }
            else
            {
                DrawRestoreTab();
            }
        }

        // ── Keybinding ────────────────────────────────────────────────────────

        // Track previous key state to detect rising edge manually.
        // Unity GUI calls the patch multiple times per frame (Layout, Repaint, etc.)
        // so Input.GetKeyDown fires several times and causes rapid double-toggle.
        private static bool _prevHotkeyDown = false;

        private static void HandleKeybinding()
        {
            // Settings hotkey (KeyCode, set in mod settings UI)
            var settings = MyMod.Settings;
            if (settings != null &&
                !settings.isWaitingForChatLogKey &&
                settings.chatLogHotkey != KeyCode.None)
            {
                bool isDown = Input.GetKey(settings.chatLogHotkey);

                // Only toggle on the rising edge (was up, now down)
                if (isDown && !_prevHotkeyDown)
                    ConversationChatLogRenderer.IsVisible = !ConversationChatLogRenderer.IsVisible;

                _prevHotkeyDown = isDown;
                return;
            }

            _prevHotkeyDown = false;

            // Fallback: RimWorld KeyBindingDef (optional, unbound by default)
            try
            {
                if (EchoColonyKeyBindings.EchoColony_ToggleChatLog?.KeyDownEvent == true)
                    ConversationChatLogRenderer.IsVisible = !ConversationChatLogRenderer.IsVisible;
            }
            catch { /* DefOf not yet initialised — safe to skip */ }
        }

        // ── Restore tab ───────────────────────────────────────────────────────

        private static void DrawRestoreTab()
        {
            Rect overlay = ConversationChatLogRenderer.CurrentRect;

            var tab = new Rect(
                Mathf.Clamp(overlay.x, 0f, UI.screenWidth  - TabW),
                Mathf.Clamp(overlay.y - TabH - 2f, 0f, UI.screenHeight - TabH),
                TabW, TabH);

            bool hov = tab.Contains(Event.current.mousePosition);
            Widgets.DrawBoxSolid(tab, hov ? ColTabHov : ColTab);
            Widgets.DrawBox(tab, 1);

            var prevAnchor = Text.Anchor;
            var prevFont   = Text.Font;
            var prevColor  = GUI.color;

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font   = GameFont.Tiny;
            GUI.color   = new Color(0.85f, 0.85f, 0.85f, 1f);
            Widgets.Label(tab, "💬 Conversation Log");

            Text.Anchor = prevAnchor;
            Text.Font   = prevFont;
            GUI.color   = prevColor;

            TooltipHandler.TipRegion(tab, "Show conversation log");

            if (Widgets.ButtonInvisible(tab))
            {
                ConversationChatLogRenderer.IsVisible = true;
                Event.current.Use();
            }
        }
    }

    // ── Harmony patch ─────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(MapInterface), "MapInterfaceOnGUI_AfterMainTabs")]
    public static class Patch_MapGUI_ChatLog
    {
        static void Postfix()
        {
            ConversationChatLogToggle.OnMapGUI();
        }
    }

    // ── KeyBindingDef typed accessor ──────────────────────────────────────────

    [DefOf]
    public static class EchoColonyKeyBindings
    {
        public static KeyBindingDef EchoColony_ToggleChatLog;

        static EchoColonyKeyBindings()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EchoColonyKeyBindings));
        }
    }
}