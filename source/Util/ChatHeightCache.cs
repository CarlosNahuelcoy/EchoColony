using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoColony
{
    /// <summary>
    /// Caches the heights of chat messages so windows don't call Text.CalcHeight for the whole
    /// history on every GUI event (several times per frame), which lags with long chats.
    /// Changes are detected by comparing string references: adding, removing or replacing a
    /// message invalidates from that point on. Only new or changed messages are measured.
    /// </summary>
    public sealed class ChatHeightCache
    {
        private readonly List<string> lines   = new List<string>();
        private readonly List<float>  heights = new List<float>();
        private float width   = -1f;
        private float spacing = 0f;

        public IReadOnlyList<float> Heights => heights;
        public float TotalHeight { get; private set; }

        /// <summary>
        /// Brings the cache up to date with <paramref name="log"/>. Set the font the window draws
        /// with before calling, since <paramref name="measure"/> runs under the current Text state.
        /// </summary>
        public void Update(IList<string> log, float viewWidth, float lineSpacing, Func<string, float> measure)
        {
            int firstChanged = 0;
            if (Mathf.Approximately(width, viewWidth) && Mathf.Approximately(spacing, lineSpacing))
            {
                int common = Math.Min(lines.Count, log.Count);
                while (firstChanged < common && ReferenceEquals(lines[firstChanged], log[firstChanged]))
                    firstChanged++;

                if (firstChanged == lines.Count && firstChanged == log.Count)
                    return; // nothing changed
            }

            width   = viewWidth;
            spacing = lineSpacing;

            lines.RemoveRange(firstChanged, lines.Count - firstChanged);
            heights.RemoveRange(firstChanged, heights.Count - firstChanged);

            for (int i = firstChanged; i < log.Count; i++)
            {
                lines.Add(log[i]);
                heights.Add(measure(log[i]));
            }

            float total = 0f;
            for (int i = 0; i < heights.Count; i++)
                total += heights[i] + spacing;
            TotalHeight = total;
        }

        public void Invalidate()
        {
            lines.Clear();
            heights.Clear();
            width       = -1f;
            TotalHeight = 0f;
        }

        /// <summary>True if a message at <paramref name="y"/> overlaps the visible part of the scroll view.</summary>
        public static bool IsVisible(float y, float height, Vector2 scrollPos, float viewportHeight, float margin = 100f)
        {
            return y + height >= scrollPos.y - margin && y <= scrollPos.y + viewportHeight + margin;
        }
    }
}
