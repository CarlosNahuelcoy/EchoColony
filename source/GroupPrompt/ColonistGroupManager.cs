using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Static helper for reading and writing colonist group assignments and group data.
    /// Mirrors the ColonistPromptManager pattern — all persistence goes through PromptStorageComponent.
    /// </summary>
    public static class ColonistGroupManager
    {
        private static PromptStorageComponent Storage => Current.Game?.GetComponent<PromptStorageComponent>();

        // ── Group retrieval ────────────────────────────────────────────────────

        public static List<ColonistGroup> GetAllGroups()
        {
            return Storage?.groups.Values.ToList() ?? new List<ColonistGroup>();
        }

        public static ColonistGroup GetGroupById(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId)) return null;
            var storage = Storage;
            if (storage == null) return null;
            storage.groups.TryGetValue(groupId, out ColonistGroup group);
            return group;
        }

        // ── Per-colonist group assignment ──────────────────────────────────────

        public static ColonistGroup GetGroup(Pawn pawn)
        {
            var storage = Storage;
            if (storage == null || pawn == null) return null;
            if (!storage.groupByColonist.TryGetValue(pawn.ThingID, out string groupId)) return null;
            storage.groups.TryGetValue(groupId, out ColonistGroup group);
            return group;
        }

        public static string GetGroupPrompt(Pawn pawn)
        {
            return GetGroup(pawn)?.prompt ?? "";
        }

        public static void SetGroup(Pawn pawn, string groupId)
        {
            var storage = Storage;
            if (storage == null || pawn == null) return;

            if (string.IsNullOrWhiteSpace(groupId))
                storage.groupByColonist.Remove(pawn.ThingID);
            else
                storage.groupByColonist[pawn.ThingID] = groupId;
        }

        public static void ClearGroup(Pawn pawn)
        {
            if (pawn == null) return;
            Storage?.groupByColonist.Remove(pawn.ThingID);
        }

        // ── Group CRUD ─────────────────────────────────────────────────────────

        public static ColonistGroup CreateGroup(string name, string prompt, Color color)
        {
            var storage = Storage;
            if (storage == null) return null;

            var group = new ColonistGroup(name, prompt, color);
            storage.groups[group.id] = group;
            return group;
        }

        public static void UpdateGroup(string id, string name, string prompt, Color color)
        {
            var storage = Storage;
            if (storage == null || !storage.groups.ContainsKey(id)) return;

            storage.groups[id].name = name;
            storage.groups[id].prompt = prompt;
            storage.groups[id].color = color;
        }

        public static void DeleteGroup(string id)
        {
            var storage = Storage;
            if (storage == null) return;

            storage.groups.Remove(id);

            // Remove all colonist assignments pointing to this group
            var toRemove = storage.groupByColonist
                .Where(kvp => kvp.Value == id)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
                storage.groupByColonist.Remove(key);
        }
    }
}