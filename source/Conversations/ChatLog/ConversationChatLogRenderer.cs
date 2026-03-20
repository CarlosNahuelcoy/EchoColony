using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Draws a draggable, resizable chat log overlay on the map UI.
    /// Features: colored speaker names, conversation separators, font size toggle.
    /// </summary>
    public static class ConversationChatLogRenderer
    {
        // ── State ──────────────────────────────────────────────────────────────
        private static Rect    _rect       = Rect.zero;
        private static bool    _positioned = false;
        private static Vector2 _scroll     = Vector2.zero;
        private static bool    _isVisible  = true;

        private static bool    _dragging   = false;
        private static bool    _resizing   = false;
        private static Vector2 _dragOffset;
        private static Vector2 _resizeStartSize;
        private static Vector2 _resizeStartMouse;

        // ── Height cache ──────────────────────────────────────────────────────
        // Key: (entryIndex, revision, textW) — unique per layout
        private static int     _lastRevision   = -1;
        private static float   _lastTextWidth  = -1f;
        private static GameFont _lastFont      = GameFont.Tiny;
        private static float[] _lineHeights    = null;
        private static float   _totalHeight    = 0f;

        // ── Scroll anchoring ──────────────────────────────────────────────────
        private static float _prevContentH = 0f;
        private static float _prevViewH    = 0f;
        private const  float BottomSlack   = 6f;

        // ── Layout constants ──────────────────────────────────────────────────
        private const float Pad        = 6f;
        private const float TitleH     = 22f;
        private const float ResizeW    = 18f;
        private const float CloseBtnW  = 20f;
        private const float MinW       = 200f;
        private const float MinH       = 80f;
        private const float SepH       = 9f;   // height of a separator row

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color ColBg       = new Color(0f,    0f,    0f,    0.50f);
        private static readonly Color ColTitle    = new Color(0.15f, 0.15f, 0.15f, 0.75f);
        private static readonly Color ColResize   = new Color(0.5f,  0.5f,  0.5f,  0.45f);
        private static readonly Color ColClose    = new Color(0.65f, 0.20f, 0.20f, 0.85f);
        private static readonly Color ColCloseHov = new Color(0.85f, 0.28f, 0.28f, 0.95f);
        private static readonly Color ColText     = new Color(0.92f, 0.92f, 0.92f, 1.00f);
        private static readonly Color ColHeader   = new Color(0.70f, 0.85f, 1.00f, 0.90f);
        private static readonly Color ColMono     = new Color(0.80f, 0.80f, 1.00f, 0.85f);
        private static readonly Color ColSep      = new Color(1f,    1f,    1f,    0.12f);

        // ── Name color cache ──────────────────────────────────────────────────
        private static readonly Dictionary<string, Color> _nameColors = new Dictionary<string, Color>();

        // ── Public API ────────────────────────────────────────────────────────

        public static bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public static Rect CurrentRect => _rect;

        /// <summary>Call this whenever display settings change (e.g. font size toggle).</summary>
        public static void InvalidateCache()
        {
            _lineHeights   = null;
            _lastRevision  = -1;
            _lastTextWidth = -1f;
            _lastFont      = GameFont.Tiny; // reset so any font triggers rebuild
        }

        public static void LoadPosition(float x, float y, float w, float h)
        {
            if (w > MinW && h > MinH)
            {
                _rect       = new Rect(x, y, w, h);
                _positioned = true;
            }
        }

        // ── Main draw ─────────────────────────────────────────────────────────

        public static void DrawOverlay()
        {
            if (!_isVisible) return;

            if (!_positioned)
            {
                const float DefaultW = 480f;
                const float DefaultH = 280f;
                const float Margin   = 8f;
                _rect       = new Rect(UI.screenWidth - DefaultW - Margin, Margin, DefaultW, DefaultH);
                _positioned = true;
            }

            HandleInput();

            var entries  = ConversationChatLog.Entries;
            int revision = ConversationChatLog.Revision;

            // Choose font based on user preference
            GameFont bodyFont = (MyMod.Settings?.chatLogLargeFont == true)
                ? GameFont.Medium : GameFont.Tiny;

            // Background + title bar
            Widgets.DrawBoxSolid(_rect, ColBg);
            var titleBar = new Rect(_rect.x, _rect.y, _rect.width, TitleH);
            Widgets.DrawBoxSolid(titleBar, ColTitle);

            var prevFont   = Text.Font;
            var prevAnchor = Text.Anchor;
            var prevWrap   = Text.WordWrap;
            var prevColor  = GUI.color;

            Text.Font   = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color   = ColHeader;
            Widgets.Label(new Rect(_rect.x + Pad, _rect.y, _rect.width - CloseBtnW - Pad * 2f, TitleH),
                          "💬 Conversation Log");
            GUI.color = prevColor;

            DrawCloseButton();

            // Content area
            var content = new Rect(_rect.x + Pad, _rect.y + TitleH + Pad,
                                   _rect.width - Pad * 2f,
                                   _rect.height - TitleH - Pad * 2f);

            float textW = content.width - 16f;

            bool atBottom = _prevContentH <= _prevViewH + BottomSlack ||
                            _scroll.y >= (_prevContentH - _prevViewH - BottomSlack);

            bool newContent = revision != _lastRevision;
            if (newContent || Mathf.Abs(textW - _lastTextWidth) > 0.5f
                           || _lineHeights == null || bodyFont != _lastFont)
            {
                RebuildHeightCache(entries, textW, bodyFont);
                _lastTextWidth = textW;
                _lastFont      = bodyFont;
            }
            _lastRevision = revision;

            if (newContent && atBottom)
                _scroll.y = Mathf.Max(0f, _totalHeight - content.height);

            var viewRect = new Rect(0f, 0f, textW, Mathf.Max(_totalHeight, content.height));
            Widgets.BeginScrollView(content, ref _scroll, viewRect);

            Text.Font    = bodyFont;
            Text.Anchor  = TextAnchor.UpperLeft;
            Text.WordWrap = true;

            float y = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float h   = (_lineHeights != null && i < _lineHeights.Length) ? _lineHeights[i] : 14f;

                if (entry.Kind == ChatLogEntryKind.Separator)
                {
                    // Thin horizontal rule centered vertically in SepH
                    float lineY = y + SepH * 0.5f - 0.5f;
                    Widgets.DrawBoxSolid(new Rect(0f, lineY, textW * 0.9f, 1f), ColSep);
                }
                else
                {
                    DrawEntry(entry, new Rect(0f, y, textW, h), bodyFont);
                }

                y += h;
            }

            Widgets.EndScrollView();

            Text.Font     = prevFont;
            Text.Anchor   = prevAnchor;
            Text.WordWrap = prevWrap;
            GUI.color     = prevColor;

            _scroll.y     = Mathf.Clamp(_scroll.y, 0f, Mathf.Max(0f, _totalHeight - content.height));
            _prevContentH = _totalHeight;
            _prevViewH    = content.height;

            // Resize handle
            Widgets.DrawBoxSolid(new Rect(_rect.xMax - ResizeW, _rect.yMax - ResizeW, ResizeW, ResizeW),
                                 ColResize);
        }

        // ── Entry drawing ─────────────────────────────────────────────────────

        private static void DrawEntry(ChatLogEntry entry, Rect rect, GameFont bodyFont)
        {
            bool isMono = entry.Kind == ChatLogEntryKind.Monologue;

            // Single font for the whole line — no switching, no height mismatch
            Text.Font = bodyFont;

            // Timestamp (gray, same font)
            float tsW = 0f;
            if (!string.IsNullOrEmpty(entry.Timestamp))
            {
                tsW = Text.CalcSize(entry.Timestamp).x + 2f;
                GUI.color = new Color(0.50f, 0.50f, 0.50f, 0.80f);
                Widgets.Label(new Rect(rect.x, rect.y, tsW, rect.height), entry.Timestamp);
            }

            // Speaker name (colored)
            string nameLabel = isMono ? "* " + entry.SpeakerName : entry.SpeakerName;
            float  nameW     = Text.CalcSize(nameLabel + ": ").x + 2f;
            GUI.color = isMono ? ColMono : GetNameColor(entry.SpeakerName);
            Widgets.Label(new Rect(rect.x + tsW, rect.y, nameW, rect.height),
                          nameLabel + ": ");

            // Dialogue text
            string textLabel = isMono ? entry.Text + " *" : entry.Text;
            GUI.color = isMono
                ? new Color(0.88f, 0.88f, 1.00f, 0.90f)
                : new Color(0.92f, 0.92f, 0.92f, 1.00f);
            Widgets.Label(new Rect(rect.x + tsW + nameW, rect.y,
                                   rect.width - tsW - nameW, rect.height), textLabel);

            GUI.color = Color.white;
        }

        // ── Name color ────────────────────────────────────────────────────────

        private static Color GetNameColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.white;
            if (_nameColors.TryGetValue(name, out Color cached)) return cached;

            // Stable hash → hue (avoid very dark or very desaturated results)
            int hash = 17;
            foreach (char c in name) hash = hash * 31 + c;
            float hue = (Mathf.Abs(hash) % 1000) / 1000f;
            Color col = Color.HSVToRGB(hue, 0.65f, 1.00f);

            _nameColors[name] = col;
            return col;
        }

        // ── Height cache ──────────────────────────────────────────────────────

        private static void RebuildHeightCache(
            ReadOnlyCollection<ChatLogEntry> entries,
            float textW,
            GameFont bodyFont)
        {
            _lineHeights = new float[entries.Count];
            _totalHeight = 0f;

            var prevFont = Text.Font;
            Text.Font = bodyFont;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float h;

                if (entry.Kind == ChatLogEntryKind.Separator)
                {
                    h = SepH;
                }
                else
                {
                    // Single font throughout — matches DrawEntry exactly
                    // so allocated height == rendered height
                    float tsW   = string.IsNullOrEmpty(entry.Timestamp)
                                  ? 0f : Text.CalcSize(entry.Timestamp).x + 2f;
                    string name = (entry.Kind == ChatLogEntryKind.Monologue
                                  ? "* " + entry.SpeakerName : entry.SpeakerName) + ": ";
                    float nameW    = Text.CalcSize(name).x + 2f;
                    float remainW  = Mathf.Max(60f, textW - tsW - nameW);
                    h = Text.CalcHeight(entry.Text, remainW);
                    h = Mathf.Max(h, Text.LineHeight);
                }

                _lineHeights[i] = h;
                _totalHeight   += h;
            }

            Text.Font = prevFont;
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private static void HandleInput()
        {
            var ev  = Event.current;
            var pos = ev.mousePosition;

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                var resizeHandle = new Rect(_rect.xMax - ResizeW, _rect.yMax - ResizeW, ResizeW, ResizeW);
                if (resizeHandle.Contains(pos))
                {
                    _resizing         = true;
                    _resizeStartSize  = _rect.size;
                    _resizeStartMouse = pos;
                    ev.Use();
                    return;
                }
                var titleDrag = new Rect(_rect.x, _rect.y, _rect.width - CloseBtnW - Pad, TitleH);
                if (titleDrag.Contains(pos))
                {
                    _dragging   = true;
                    _dragOffset = pos - new Vector2(_rect.x, _rect.y);
                    ev.Use();
                }
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                if (_dragging || _resizing) SavePosition();
                _dragging = _resizing = false;
            }
            else if (ev.type == EventType.MouseDrag && ev.button == 0)
            {
                if (_resizing)
                {
                    var delta = pos - _resizeStartMouse;
                    _rect.width  = Mathf.Max(MinW, _resizeStartSize.x + delta.x);
                    _rect.height = Mathf.Max(MinH, _resizeStartSize.y + delta.y);
                    _lastTextWidth = -1f;
                    ev.Use();
                }
                else if (_dragging)
                {
                    _rect.position = pos - _dragOffset;
                    _rect.x = Mathf.Clamp(_rect.x, 0f, UI.screenWidth  - _rect.width);
                    _rect.y = Mathf.Clamp(_rect.y, 0f, UI.screenHeight - _rect.height);
                    ev.Use();
                }
            }
        }

        // ── Close button ──────────────────────────────────────────────────────

        private static void DrawCloseButton()
        {
            var btnRect = new Rect(_rect.xMax - CloseBtnW - Pad * 0.5f,
                                   _rect.y + (TitleH - CloseBtnW) * 0.5f,
                                   CloseBtnW, CloseBtnW);

            bool hov = btnRect.Contains(Event.current.mousePosition);
            Widgets.DrawBoxSolid(btnRect, hov ? ColCloseHov : ColClose);

            var prevAnchor = Text.Anchor;
            var prevFont   = Text.Font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font   = GameFont.Tiny;
            GUI.color   = Color.white;
            Widgets.Label(btnRect, "✕");
            Text.Anchor = prevAnchor;
            Text.Font   = prevFont;

            TooltipHandler.TipRegion(btnRect,
                "Hide conversation log\n(restore via button in EchoColony settings)");

            if (Widgets.ButtonInvisible(btnRect))
            {
                _isVisible = false;
                Event.current.Use();
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private static void SavePosition()
        {
            var s = MyMod.Settings;
            if (s == null) return;
            s.chatLogX = _rect.x;
            s.chatLogY = _rect.y;
            s.chatLogW = _rect.width;
            s.chatLogH = _rect.height;
        }
    }
}