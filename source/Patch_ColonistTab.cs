using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EchoColony
{
    [HarmonyPatch(typeof(ITab_Pawn_Character), "FillTab")]
    public static class Patch_CharacterTabButton
    {
        public static void Postfix()
        {
            Pawn selectedPawn = Find.Selector.SingleSelectedThing as Pawn;
            if (selectedPawn == null || selectedPawn.Faction != Faction.OfPlayer) return;

            Rect buttonRect = new Rect(540f, 60f, 130f, 30f); // Puedes ajustar posición

            if (Widgets.ButtonText(buttonRect, "Hablar con colono"))
            {
                Find.WindowStack.Add(new ColonistChatWindow(selectedPawn));
            }
        }
    }
}
