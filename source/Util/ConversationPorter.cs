using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using SimpleJSON;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Handles export and import of pawn conversation history and memories.
    /// Files are stored as JSON in SaveData/EchoColony/Exports/.
    /// The player freely chooses which file to load into any pawn.
    /// </summary>
    public static class ConversationPorter
    {
        public static string ExportFolder =>
            Path.Combine(GenFilePaths.SaveDataFolderPath, "EchoColony", "Exports");

        // ── Export ────────────────────────────────────────────────────────────

        public static string Export(Pawn pawn)
        {
            try
            {
                EnsureExportFolder();

                var root = new JSONObject();
                root["echoColonyVersion"] = "1.0";
                root["exportDate"]        = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                // Fingerprint
                var fp = new JSONObject();
                fp["name"]      = pawn.LabelShort;
                fp["gender"]    = pawn.gender.ToString();
                fp["age"]       = pawn.ageTracker?.AgeBiologicalYears ?? 0;
                fp["birthTick"] = pawn.ageTracker?.BirthAbsTicks ?? 0;

                var traits = new JSONArray();
                foreach (var t in pawn.story?.traits?.allTraits ?? new List<Trait>())
                    traits.Add(t.LabelCap);
                fp["traits"] = traits;

                var backstories = new JSONArray();
                foreach (var b in pawn.story?.AllBackstories ?? Enumerable.Empty<BackstoryDef>())
                    if (!string.IsNullOrEmpty(b.title)) backstories.Add(b.title);
                fp["backstories"] = backstories;

                root["fingerprint"] = fp;

                // Chat log
                var chatArray = new JSONArray();
                var chatLog   = ChatGameComponent.Instance?.GetChat(pawn) ?? new List<string>();
                foreach (var line in chatLog)
                    chatArray.Add(line);
                root["chatLog"] = chatArray;

                // Memories
                var memObj    = new JSONObject();
                var memManager = ColonistMemoryManager.GetOrCreate();
                if (memManager != null)
                {
                    var tracker = memManager.GetTrackerFor(pawn);
                    if (tracker != null)
                        foreach (var kvp in tracker.GetAllMemories())
                            memObj[kvp.Key.ToString()] = kvp.Value;
                }
                root["memories"] = memObj;

                string json     = root.ToString(2); // indented
                string filename = BuildFilename(pawn);
                string fullPath = Path.Combine(ExportFolder, filename);
                File.WriteAllText(fullPath, json, Encoding.UTF8);

                Log.Message($"[EchoColony] Exported conversation for {pawn.LabelShort} → {fullPath}");
                return fullPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Export failed for {pawn?.LabelShort}: {ex.Message}");
                return null;
            }
        }

        // ── Import ────────────────────────────────────────────────────────────

        public static bool Import(Pawn pawn, string filePath)
        {
            try
            {
                string raw  = File.ReadAllText(filePath, Encoding.UTF8);
                var    root = JSON.Parse(raw);

                if (root == null)
                {
                    Log.Error($"[EchoColony] Failed to parse export file: {filePath}");
                    return false;
                }

                // Chat log
                var chatComp = ChatGameComponent.Instance;
                if (chatComp != null)
                {
                    chatComp.ClearChat(pawn);
                    var chat      = chatComp.GetChat(pawn);
                    var chatArray = root["chatLog"].AsArray;
                    if (chatArray != null)
                        foreach (JSONNode node in chatArray)
                            chat.Add(node.Value);
                }

                // Memories
                var memObj = root["memories"].AsObject;
                if (memObj != null && memObj.Count > 0)
                {
                    var memManager = ColonistMemoryManager.GetOrCreate();
                    if (memManager != null)
                    {
                        var tracker = memManager.GetTrackerFor(pawn);
                        if (tracker != null)
                        {
                            tracker.ClearAllMemories();
                            foreach (var kvp in memObj)
                                if (int.TryParse(kvp.Key, out int day))
                                    tracker.SetMemoryForDay(day, kvp.Value.Value);
                        }
                    }
                }

                int lines = root["chatLog"].AsArray?.Count ?? 0;
                int mems  = root["memories"].AsObject?.Count ?? 0;
                Log.Message($"[EchoColony] Imported {lines} chat lines and {mems} memories into {pawn.LabelShort}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Import failed for {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
        }

        // ── File listing ──────────────────────────────────────────────────────

        public static List<ExportFileInfo> GetExportFiles()
        {
            EnsureExportFolder();
            var results = new List<ExportFileInfo>();

            foreach (string path in Directory.GetFiles(ExportFolder, "*.json")
                                             .OrderByDescending(f => File.GetLastWriteTime(f)))
            {
                try
                {
                    string raw  = File.ReadAllText(path, Encoding.UTF8);
                    var    root = JSON.Parse(raw);
                    if (root == null) continue;

                    var fp = root["fingerprint"];

                    var traitsList = new List<string>();
                    var traitsArr  = fp["traits"].AsArray;
                    if (traitsArr != null)
                        foreach (JSONNode n in traitsArr)
                            traitsList.Add(n.Value);

                    var backstoryList = new List<string>();
                    var backstoryArr  = fp["backstories"].AsArray;
                    if (backstoryArr != null)
                        foreach (JSONNode n in backstoryArr)
                            backstoryList.Add(n.Value);

                    string name      = fp["name"].Value;
                    string gender    = fp["gender"].Value;
                    int    age       = fp["age"].AsInt;
                    string backstory = backstoryList.FirstOrDefault() ?? "unknown origin";
                    string traits    = traitsList.Any() ? string.Join(", ", traitsList.Take(3)) : "no traits";

                    results.Add(new ExportFileInfo
                    {
                        filePath    = path,
                        displayName = !string.IsNullOrEmpty(name) ? name : Path.GetFileNameWithoutExtension(path),
                        exportDate  = root["exportDate"].Value ?? "Unknown",
                        pawnSummary = $"{gender}, {age}y — {backstory} — {traits}",
                        lineCount   = root["chatLog"].AsArray?.Count   ?? 0,
                        memoryCount = root["memories"].AsObject?.Count ?? 0
                    });
                }
                catch
                {
                    // Skip malformed files silently
                }
            }

            return results;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string BuildFilename(Pawn pawn)
        {
            string name = pawn.LabelShort.Replace(" ", "_");
            string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            return $"{name}_{date}.json";
        }

        private static void EnsureExportFolder()
        {
            if (!Directory.Exists(ExportFolder))
                Directory.CreateDirectory(ExportFolder);
        }
    }

    // ── Data classes ──────────────────────────────────────────────────────────

    public class ExportFileInfo
    {
        public string filePath;
        public string displayName;
        public string exportDate;
        public string pawnSummary;
        public int    lineCount;
        public int    memoryCount;
    }
}