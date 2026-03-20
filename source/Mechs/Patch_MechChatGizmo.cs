using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace EchoColony
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_MechChatGizmo
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // Only add gizmo for player mechs
            if (__instance == null || !__instance.Spawned || __instance.Dead)
                return;

            if (__instance.Faction != Faction.OfPlayer)
                return;

            if (!Mechs.MechIntelligenceDetector.IsMechanoid(__instance))
                return;

            // Verify component is initialized
            var component = Mechs.MechChatGameComponent.Instance;
            if (component == null)
            {
                Log.Warning("[EchoColony] MechChatGameComponent not initialized");
                return;
            }

            __result = __result.Concat(new Gizmo[] { CreateMechChatGizmo(__instance) });
        }

        private static Command_Action CreateMechChatGizmo(Pawn mech)
        {
            var gizmo = new Command_Action
            {
                defaultLabel = $"Terminal: {mech.LabelShort}",
                defaultDesc = "Open communication terminal with this mechanoid",
                icon = MyModTextures.ChatIcon,
                action = delegate
                {
                    var window = new Mechs.MechChatWindow(mech);
                    Find.WindowStack.Add(window);
                    
                    if (Find.TickManager.Paused == false)
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    }
                }
            };

            return gizmo;
        }
    }
}