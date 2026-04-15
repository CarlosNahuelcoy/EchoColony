using System.Collections.Generic;
using Verse;

namespace EchoColony.Mechs
{
    public class MechChatGameComponent : GameComponent
    {
        private static MechChatGameComponent instance;
        public static MechChatGameComponent Instance => instance;

        private Dictionary<string, List<string>> mechChats = new Dictionary<string, List<string>>();
        private int lastCleanupTick = 0;
        private const int CLEANUP_INTERVAL = 120000; // Every 2 in-game days

        public MechChatGameComponent(Game game)
        {
            instance = this;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastCleanupTick > CLEANUP_INTERVAL)
            {
                CleanupDeadMechs();
                lastCleanupTick = currentTick;
            }
        }

        public List<string> GetChat(Pawn mech)
        {
            if (mech == null) return new List<string>();

            string key = mech.ThingID;
            if (!mechChats.ContainsKey(key))
            {
                mechChats[key] = new List<string>();
            }

            return mechChats[key];
        }

        public void SaveChat(Pawn mech, List<string> chat)
        {
            if (mech == null) return;
            mechChats[mech.ThingID] = new List<string>(chat);
        }

        public void ClearChat(Pawn mech)
        {
            if (mech == null) return;
            mechChats.Remove(mech.ThingID);
        }

        private void CleanupDeadMechs()
        {
            var toRemove = new List<string>();

            foreach (var key in mechChats.Keys)
            {
                bool found = false;
                foreach (var map in Find.Maps)
                {
                    var mech = map.listerThings.AllThings.Find(t => t.ThingID == key);
                    if (mech != null && !mech.Destroyed)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
            {
                mechChats.Remove(key);
            }

            if (toRemove.Count > 0)
            {
                Log.Message($"[EchoColony] Cleaned up {toRemove.Count} dead mech chat histories");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref mechChats, "mechChats", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (mechChats == null)
                {
                    mechChats = new Dictionary<string, List<string>>();
                }
                instance = this;
            }
        }
    }
}