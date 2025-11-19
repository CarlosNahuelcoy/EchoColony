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
            harmony.PatchAll();
        }
    }
}
