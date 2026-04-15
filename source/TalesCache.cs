using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Session-scoped cache for verified colony tales.
    ///
    /// Problem it solves:
    ///   TaleManager.AllTalesListForReading can contain hundreds of entries in a long
    ///   colony. Calling Concerns(pawn) on every entry is expensive and redundant —
    ///   tales don't change during a conversation.
    ///
    /// Strategy:
    ///   - Results are cached per pawn (keyed by thingIDNumber) for TTL_Ticks.
    ///   - TTL is ~1 in-game hour (2500 ticks). New tales generated in that window
    ///     are rare enough that staleness is acceptable.
    ///   - Max tale counts are deliberately conservative to keep prompts lean.
    ///   - The cache is static but bounded — never grows beyond MAX_CACHED_PAWNS.
    ///
    /// Why ShortSummary instead of TaleTextGenerator:
    ///   TaleTextGenerator.GenerateTextFromTale with ArtDescription purpose requires
    ///   a TaleSurroundings context that only exists during RimWorld's art generation
    ///   pipeline. Outside that context it throws NullReferenceException on every call.
    ///   tale.ShortSummary is a precomputed plain-text string on the Tale object itself,
    ///   requires no external context, and never throws.
    /// </summary>
    public static class TalesCache
    {
        // ── Tuning constants ──────────────────────────────────────────────────────

        /// How many ticks before a cache entry is considered stale (~1 in-game hour).
        private const int TTL_Ticks = 2500;

        /// Max tales per pawn for direct colonist chat (prompt is already heavy).
        public const int MAX_PERSONAL_TALES = 4;

        /// Max shared tales for pawn-to-pawn conversation prompts.
        public const int MAX_SHARED_TALES = 3;

        /// Safety cap on cached pawns.
        private const int MAX_CACHED_PAWNS = 20;

        /// Special key for colony-wide tale cache (not tied to a specific pawn).
        private const int COLONY_KEY = -1;

        // ── Cache storage ─────────────────────────────────────────────────────────

        private static readonly Dictionary<int, CacheEntry> _cache =
            new Dictionary<int, CacheEntry>();

        private struct CacheEntry
        {
            public List<string> Tales;
            public int          CachedAtTick;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns verified tale strings for <paramref name="pawn"/>, from cache if
        /// available and fresh, otherwise generated fresh and cached.
        /// </summary>
        public static List<string> GetTalesFor(Pawn pawn, int maxCount = MAX_PERSONAL_TALES)
        {
            if (pawn == null || Find.TaleManager == null) return new List<string>();

            int id = pawn.thingIDNumber;

            if (TryGetFromCache(id, out var cached))
                return cached.Take(maxCount).ToList();

            var tales = GenerateTalesFor(pawn, maxCount);
            Store(id, tales);
            return tales;
        }

        /// <summary>
        /// Returns tales involving <paramref name="pawn"/> split into two buckets:
        /// shared (also concern <paramref name="other"/>) and personal (pawn only).
        /// Used by PawnConversationPromptBuilder where both pawns are known upfront.
        /// </summary>
        public static (List<string> shared, List<string> personal)
            GetTalesForPair(Pawn pawn, Pawn other,
                            int maxShared   = MAX_SHARED_TALES,
                            int maxPersonal = MAX_PERSONAL_TALES)
        {
            if (pawn == null || Find.TaleManager == null)
                return (new List<string>(), new List<string>());

            var shared   = new List<string>();
            var personal = new List<string>();

            try
            {
                var rawTales = Find.TaleManager.AllTalesListForReading
                    .Where(t => t != null && t.Concerns(pawn))
                    .OrderByDescending(t => t.date)
                    .ToList();

                foreach (var tale in rawTales)
                {
                    if (shared.Count >= maxShared && personal.Count >= maxPersonal) break;

                    string text = null;
                    try { text = tale.ShortSummary; }
                    catch { continue; }

                    text = CleanTaleText(text);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    bool isShared = other != null && tale.Concerns(other);
                    if (isShared && shared.Count < maxShared)
                        shared.Add(text);
                    else if (!isShared && personal.Count < maxPersonal)
                        personal.Add(text);
                }
            }
            catch { }

            return (shared, personal);
        }

        /// <summary>
        /// Returns recent colony-wide tales — used by the Storyteller prompt and as
        /// a fallback context for colonists with no personal tales yet.
        /// Cached under a shared key (not per pawn).
        /// </summary>
        public static List<string> GetColonyTales(int maxCount = 5)
        {
            if (Find.TaleManager == null) return new List<string>();

            if (TryGetFromCache(COLONY_KEY, out var cached))
                return cached.Take(maxCount).ToList();

            var result = new List<string>();
            try
            {
                foreach (var tale in Find.TaleManager.AllTalesListForReading
                    .Where(t => t != null)
                    .OrderByDescending(t => t.date))
                {
                    if (result.Count >= maxCount) break;

                    string text = null;
                    try { text = tale.ShortSummary; }
                    catch { continue; }

                    text = CleanTaleText(text);
                    if (!string.IsNullOrWhiteSpace(text))
                        result.Add(text);
                }
            }
            catch { }

            Store(COLONY_KEY, result);
            return result;
        }

        /// <summary>Clears stale entries. Safe to call every ~300 ticks.</summary>
        public static void PruneStale()
        {
            if (Find.TickManager == null) return;
            int now = Find.TickManager.TicksGame;

            var stale = _cache
                .Where(kv => now - kv.Value.CachedAtTick > TTL_Ticks)
                .Select(kv => kv.Key)
                .ToList();

            foreach (int key in stale)
                _cache.Remove(key);
        }

        /// <summary>Wipes the entire cache. Call on game load/new game.</summary>
        public static void Clear() => _cache.Clear();

        // ── Name/text cleanup ─────────────────────────────────────────────────────

        /// <summary>
        /// Cleans a ShortSummary string into something readable for the AI.
        ///
        /// ShortSummary format: "Action: Firstname 'Nickname' Lastname, ..."
        ///
        /// Problems solved:
        ///   1. "Gissel 'Gudhmarson' Gudhmarson"           → "Gudhmarson"
        ///   2. Pirahã '"Cobra" Nivacle' "Cobra" Nivacle   → "Cobra Nivacle"
        ///   3. "Inconsciente: , say"                      → "Inconsciente: say"
        ///   4. Double spaces, trailing commas, XML tags
        /// </summary>
        public static string CleanTaleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // 1. Strip XML/color tags
            text = Regex.Replace(text, @"<[^>]+>", "").Trim();

            // 2. Reduce full name to nickname.
            //    Matches: word(s) 'anything inside single quotes' word(s)
            //    Works for plain and quoted nicknames:
            //      Gissel 'Gudhmarson' Gudhmarson       → Gudhmarson
            //      Pirahã '"Cobra" Nivacle' "Cobra"...  → "Cobra" Nivacle
            //    Unicode range \u00C0-\u024F covers accented chars (Pirahã, etc.)
            text = Regex.Replace(
                text,
                @"[\w\u00C0-\u024F][\w\s\u00C0-\u024F]*'([^']+)'[\w\s\u00C0-\u024F]*",
                "$1");

            // 3. Remove surrounding double-quotes left from quoted nicknames:
            //    "Cobra" Nivacle → Cobra Nivacle
            text = Regex.Replace(text, "\"([^\"]+)\"", "$1");

            // 4. Remove leading commas/spaces (from empty pawn slots)
            text = Regex.Replace(text, @"^[\s,]+", "");

            // 5. Collapse multiple spaces
            text = Regex.Replace(text, @"\s{2,}", " ");

            // 6. Remove trailing commas/spaces
            text = text.TrimEnd(',', ' ').Trim();

            return text;
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private static bool TryGetFromCache(int key, out List<string> tales)
        {
            tales = null;
            if (!_cache.TryGetValue(key, out var entry)) return false;

            if (Find.TickManager == null) return false;
            if (Find.TickManager.TicksGame - entry.CachedAtTick > TTL_Ticks)
            {
                _cache.Remove(key);
                return false;
            }

            tales = entry.Tales;
            return true;
        }

        private static void Store(int key, List<string> tales)
        {
            if (_cache.Count >= MAX_CACHED_PAWNS && !_cache.ContainsKey(key))
            {
                int oldest = _cache.OrderBy(kv => kv.Value.CachedAtTick).First().Key;
                _cache.Remove(oldest);
            }

            if (Find.TickManager == null) return;
            _cache[key] = new CacheEntry
            {
                Tales        = tales,
                CachedAtTick = Find.TickManager.TicksGame
            };
        }

        private static List<string> GenerateTalesFor(Pawn pawn, int maxCount)
        {
            var result = new List<string>();
            try
            {
                var rawTales = Find.TaleManager.AllTalesListForReading
                    .Where(t => t != null && t.Concerns(pawn))
                    .OrderByDescending(t => t.date)
                    .ToList();

                foreach (var tale in rawTales)
                {
                    if (result.Count >= maxCount) break;

                    string text = null;
                    try { text = tale.ShortSummary; }
                    catch { continue; }

                    text = CleanTaleText(text);
                    if (!string.IsNullOrWhiteSpace(text))
                        result.Add(text);
                }
            }
            catch { }

            return result;
        }
    }
}