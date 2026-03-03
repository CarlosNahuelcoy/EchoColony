using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System;

namespace EchoColony.Animals
{
    [HarmonyPatch(typeof(Pawn))]
    public static class Patch_AnimalChatGizmo
    {
        [HarmonyPatch("GetGizmos")]
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            // Return original gizmos first
            foreach (var g in __result)
                yield return g;

            // Validations
            if (__instance == null) yield break;
            if (__instance.Dead) yield break;
            if (__instance.Map == null) yield break;
            if (!__instance.Spawned) yield break;

            // Only for animals
            if (!__instance.RaceProps.Animal) yield break;

            // Only for player-owned animals
            if (__instance.Faction != Faction.OfPlayer) yield break;

            // Check components are initialized
            if (Current.Game == null) yield break;
            if (MyStoryModComponent.Instance == null) yield break;

            // Create chat gizmo
            yield return new Command_Action
            {
                defaultLabel = $"Talk to {__instance.LabelShort}",
                defaultDesc = $"Have a conversation with this {__instance.KindLabel}",
                icon = MyModTextures.ChatIcon,
                action = () =>
                {
                    try
                    {
                        if (__instance == null || __instance.Dead || __instance.Destroyed)
                        {
                            Messages.Message("Cannot chat with this animal.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        Find.WindowStack.Add(new AnimalChatWindow(__instance));
                        Find.TickManager.slower.SignalForceNormalSpeedShort();
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Error opening animal chat for {__instance?.LabelShort}: {ex.Message}");
                        Messages.Message("Error opening animal chat. Check logs.", MessageTypeDefOf.RejectInput);
                    }
                }
            };
        }
    }
}