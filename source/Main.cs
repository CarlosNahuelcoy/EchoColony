using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace EchoColony
{
    public class MyMapComponent : MapComponent
    {
        public MyMapComponent(Map map) : base(map) {}
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("EchoColony");

            // Clase por clase en vez de PatchAll(): si un parche falla (otro mod o una
            // versión nueva del juego cambió el método), los demás se siguen aplicando.
            // Harmony 3 (RimWorld 1.7) rechaza parches con tipos que no coinciden.
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Start).Assembly))
            {
                if (!type.GetCustomAttributes(typeof(HarmonyPatch), false).Any()) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Log.Warning($"[EchoColony] Could not apply patch {type.FullName}: {ex.Message}");
                }
            }
            // Suppress vanilla Interaction Bubbles entries when AI conversations are active
            Conversations.Patch_SuppressVanillaBubble.TryApply(harmony);
        }
    }
}
