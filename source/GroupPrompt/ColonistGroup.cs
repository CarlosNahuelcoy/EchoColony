using System;
using UnityEngine;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Represents a named group of colonists that share a common prompt.
    /// Stored in PromptStorageComponent and keyed by a GUID string id.
    /// </summary>
    public class ColonistGroup : IExposable
    {
        public string id = "";
        public string name = "";
        public string prompt = "";
        public Color color = Color.white;

        public ColonistGroup() { }

        public ColonistGroup(string name, string prompt, Color color)
        {
            this.id = Guid.NewGuid().ToString();
            this.name = name;
            this.prompt = prompt;
            this.color = color;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", "");
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref prompt, "prompt", "");
            Scribe_Values.Look(ref color, "color", Color.white);
        }
    }
}