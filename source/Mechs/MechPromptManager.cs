using Verse;
using System.Collections.Generic;

namespace EchoColony.Mechs
{
    public class MechPromptManager : GameComponent
    {
        private Dictionary<string, string> mechPrompts = new Dictionary<string, string>();
        private Dictionary<string, MechIntelligenceLevel> mechIntelligenceOverrides = new Dictionary<string, MechIntelligenceLevel>();

        public MechPromptManager(Game game)
        {
        }

        public static string GetPrompt(Pawn mech)
        {
            if (mech == null) return "";

            var component = Current.Game.GetComponent<MechPromptManager>();
            if (component == null) return "";

            string key = mech.ThingID;
            if (component.mechPrompts.TryGetValue(key, out string prompt))
            {
                return prompt;
            }

            return "";
        }

        public static void SetPrompt(Pawn mech, string prompt)
        {
            if (mech == null) return;

            var component = Current.Game.GetComponent<MechPromptManager>();
            if (component == null)
            {
                component = new MechPromptManager(Current.Game);
                Current.Game.components.Add(component);
            }

            string key = mech.ThingID;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                component.mechPrompts.Remove(key);
            }
            else
            {
                component.mechPrompts[key] = prompt;
            }
        }

        public static MechIntelligenceLevel? GetIntelligenceOverride(Pawn mech)
        {
            if (mech == null) return null;

            var component = Current.Game.GetComponent<MechPromptManager>();
            if (component == null) return null;

            string key = mech.ThingID;
            if (component.mechIntelligenceOverrides.TryGetValue(key, out MechIntelligenceLevel level))
            {
                return level;
            }

            return null;
        }

        public static void SetIntelligenceOverride(Pawn mech, MechIntelligenceLevel? level)
        {
            if (mech == null) return;

            var component = Current.Game.GetComponent<MechPromptManager>();
            if (component == null)
            {
                component = new MechPromptManager(Current.Game);
                Current.Game.components.Add(component);
            }

            string key = mech.ThingID;
            if (level == null)
            {
                component.mechIntelligenceOverrides.Remove(key);
            }
            else
            {
                component.mechIntelligenceOverrides[key] = level.Value;
            }
        }

        public static void ClearIntelligenceOverride(Pawn mech)
        {
            SetIntelligenceOverride(mech, null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref mechPrompts, "mechPrompts", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref mechIntelligenceOverrides, "mechIntelligenceOverrides", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (mechPrompts == null)
                {
                    mechPrompts = new Dictionary<string, string>();
                }
                if (mechIntelligenceOverrides == null)
                {
                    mechIntelligenceOverrides = new Dictionary<string, MechIntelligenceLevel>();
                }
            }
        }
    }
}