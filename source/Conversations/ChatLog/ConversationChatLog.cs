using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Verse;

namespace EchoColony.Conversations
{
    // ── Entry types ───────────────────────────────────────────────────────────

    public enum ChatLogEntryKind { Line, Monologue, Separator }

    public struct ChatLogEntry
    {
        public ChatLogEntryKind Kind;
        public string           Timestamp;   // "[08h] "
        public string           SpeakerName; // "Alice"
        public string           Text;        // dialogue text
        // IsSeparator entries have no meaningful text/speaker

        public static ChatLogEntry MakeLine(string timestamp, string speaker, string text)
            => new ChatLogEntry { Kind = ChatLogEntryKind.Line,      Timestamp = timestamp, SpeakerName = speaker, Text = text };

        public static ChatLogEntry MakeMonologue(string timestamp, string speaker, string text)
            => new ChatLogEntry { Kind = ChatLogEntryKind.Monologue, Timestamp = timestamp, SpeakerName = speaker, Text = text };

        public static ChatLogEntry MakeSeparator()
            => new ChatLogEntry { Kind = ChatLogEntryKind.Separator };
    }

    // ── Buffer ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thread-safe rolling buffer of structured chat log entries.
    /// Feed via Push*(); read via Entries / Revision.
    /// </summary>
    public static class ConversationChatLog
    {
        private const int MaxEntries       = 300;
        private const int DuplicateTickWin = 60;

        private static readonly Queue<ChatLogEntry>          _buf   = new Queue<ChatLogEntry>(MaxEntries);
        private static readonly object                       _lock  = new object();
        private static ReadOnlyCollection<ChatLogEntry>      _cache = null;
        private static bool   _dirty      = true;
        private static int    _revision   = 0;
        private static string _lastText   = null;
        private static int    _lastTick   = 0;

        // ── Write ─────────────────────────────────────────────────────────────

        public static void PushLine(string timestamp, string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            PushEntry(ChatLogEntry.MakeLine(timestamp, speaker ?? "?", text));
        }

        public static void PushMonologue(string timestamp, string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            PushEntry(ChatLogEntry.MakeMonologue(timestamp, speaker ?? "?", text));
        }

        /// <summary>Push a thin horizontal divider between conversation groups.</summary>
        public static void PushSeparator()
        {
            lock (_lock)
            {
                // Don't double-up separators
                if (_buf.Count > 0)
                {
                    // Peek last entry — Queue doesn't expose that directly; use cache trick
                    var arr = _buf.ToArray();
                    if (arr[arr.Length - 1].Kind == ChatLogEntryKind.Separator) return;
                }
                Enqueue(ChatLogEntry.MakeSeparator());
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _buf.Clear();
                _lastText = null;
                _lastTick = 0;
                _revision++;
                _dirty = true;
            }
        }

        // ── Read ──────────────────────────────────────────────────────────────

        public static ReadOnlyCollection<ChatLogEntry> Entries
        {
            get
            {
                lock (_lock)
                {
                    if (_dirty)
                    {
                        _cache = new ReadOnlyCollection<ChatLogEntry>(_buf.ToArray());
                        _dirty = false;
                    }
                    return _cache;
                }
            }
        }

        public static int Revision
        {
            get { lock (_lock) { return _revision; } }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static void PushEntry(ChatLogEntry entry)
        {
            if (entry.Kind != ChatLogEntryKind.Separator)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                lock (_lock)
                {
                    string key = entry.SpeakerName + entry.Text;
                    if (key == _lastText && (now - _lastTick) <= DuplicateTickWin) return;
                    _lastText = key;
                    _lastTick = now;
                    Enqueue(entry);
                }
            }
            // Separator path is handled directly in PushSeparator (already holds lock)
        }

        // Caller must hold _lock
        private static void Enqueue(ChatLogEntry entry)
        {
            if (_buf.Count >= MaxEntries) _buf.Dequeue();
            _buf.Enqueue(entry);
            _revision++;
            _dirty = true;
        }
    }
}