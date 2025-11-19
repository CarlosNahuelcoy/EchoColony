using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System;
using System.Linq;
using Verse.AI;
using UnityEngine;

namespace EchoColony
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
public static class Patch_DamageDealt
{
    public static void Postfix(Pawn_HealthTracker __instance, DamageInfo dinfo)
    {
        // Accedemos al pawn afectado
        Pawn target = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

        if (!target.Spawned) return;

        Pawn attacker = dinfo.Instigator as Pawn;
        float damage = dinfo.Amount;

        if (attacker != null && attacker.Faction == Faction.OfPlayer && target.Faction != Faction.OfPlayer && damage > 0)
        {
            string targetType = GetCreatureType(target);
            EventLogger.LogEvent($"{attacker.LabelShortCap} injured {target.LabelShortCap} ({targetType}) dealing {damage} damage.");
        }
    }

    private static string GetCreatureType(Pawn pawn)
    {
        if (pawn.RaceProps.IsMechanoid) return "mechanoid";
        if (pawn.RaceProps.Humanlike) return "humanoid";
        if (pawn.RaceProps.Animal) return "animal";
        if (pawn.RaceProps.Insect) return "insect";
        return "creature";
    }
}


    // 🦅 CAZA DE ANIMALES
    [HarmonyPatch(typeof(JobDriver_Hunt), "MakeNewToils")]
    public static class Patch_Hunting
    {
        public static void Postfix(JobDriver_Hunt __instance)
        {
            var pawn = __instance.pawn;
            var target = __instance.job.targetA.Thing as Pawn;
            
            if (pawn.Faction == Faction.OfPlayer && target != null)
            {
                EventLogger.LogEvent($"{pawn.LabelShortCap} is hunting {target.LabelShortCap}.");
            }
        }
    }

    // 🔫 DISPAROS/ATAQUES A DISTANCIA
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), 
        new Type[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })]
    public static class Patch_ProjectileLaunch
    {
        public static void Postfix(Projectile __instance, Thing launcher)
        {
            Pawn shooter = launcher as Pawn;
            if (shooter?.Faction == Faction.OfPlayer)
            {
                EventLogger.LogEvent($"{shooter.LabelShortCap} fired {__instance.def.label}.");
            }
        }
    }

    // 🐛 MUERTE DE INSECTOS ESPECÍFICAMENTE
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_InsectKill
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!__instance.Spawned || __instance.Faction == null) return;

            Pawn killer = dinfo?.Instigator as Pawn;
            if (killer?.Faction == Faction.OfPlayer && __instance.RaceProps.Insect)
            {
                EventLogger.LogEvent($"{killer.LabelShortCap} killed the insect {__instance.LabelShortCap}.");
            }
        }
    }

    // 🤖 DESTRUCCIÓN DE MECANOIDES
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    public static class Patch_MechanoidDestroy
    {
        public static void Postfix(Pawn __instance, DestroyMode mode)
        {
            if (__instance.RaceProps.IsMechanoid && mode == DestroyMode.KillFinalize)
            {
                // Buscar quién causó la destrucción
                var recentDamage = __instance.health?.hediffSet?.hediffs?.LastOrDefault();
                if (recentDamage != null)
                {
                    EventLogger.LogEvent($"Mechanoid {__instance.LabelShortCap} was destroyed.");
                }
            }
        }
    }

    // 🏃 DERRIBOS/KNOCKDOWNS
    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherFailed")]
    public static class Patch_PawnDowned
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn.Faction == Faction.OfPlayer && ___pawn.Downed)
            {
                EventLogger.LogEvent($"{___pawn.LabelShortCap} has been downed in combat.");
            }
        }
    }

    // 💀 CAPTURA DE ENEMIGOS
    [HarmonyPatch(typeof(Pawn), "SetFaction")]
    public static class Patch_PrisonerCapture
    {
        public static void Postfix(Pawn __instance, Faction newFaction, Pawn recruiter)
        {
            if (newFaction == Faction.OfPlayer && __instance.IsPrisoner)
            {
                EventLogger.LogEvent($"{__instance.LabelShortCap} has been captured as prisoner.");
            }
        }
    }

    // 🦴 HERIDAS GRAVES/PÉRDIDA DE EXTREMIDADES
    [HarmonyPatch(typeof(Hediff_MissingPart), "PostAdd")]
    public static class Patch_MissingPart
    {
        public static void Postfix(Hediff_MissingPart __instance)
        {
            if (__instance.pawn.Faction == Faction.OfPlayer)
            {
                EventLogger.LogEvent($"{__instance.pawn.LabelShortCap} lost {__instance.Part.Label}.");
            }
        }
    }

    // 🔥 INCENDIOS
    [HarmonyPatch(typeof(Fire), "DoComplexCalcs")]
    public static class Patch_Fire
    {
        public static void Postfix(Fire __instance)
        {
            // Verificar que el fuego y el mapa existan antes de acceder a sus propiedades
            if (__instance?.Map != null && 
                __instance.Spawned && 
                __instance.Map.IsPlayerHome && 
                __instance.fireSize > 0.5f)
            {
                EventLogger.LogEvent($"Fire detected at {__instance.Position}!");
            }
        }
    }

    // 🍖 MATANZA DE ANIMALES PARA COMIDA
    [HarmonyPatch(typeof(JobDriver_Slaughter), "MakeNewToils")]
    public static class Patch_Slaughter
    {
        public static void Postfix(JobDriver_Slaughter __instance)
        {
            var butcher = __instance.pawn;
            var animal = __instance.job.targetA.Thing as Pawn;
            
            if (butcher.Faction == Faction.OfPlayer && animal != null)
            {
                EventLogger.LogEvent($"{butcher.LabelShortCap} slaughtered {animal.LabelShortCap} for meat.");
            }
        }
    }

    // 🎭 CRISIS NERVIOSAS
    [HarmonyPatch(typeof(MentalState), "PostStart")]
    public static class Patch_MentalBreak
    {
        public static void Postfix(MentalState __instance)
        {
            if (__instance.pawn.Faction == Faction.OfPlayer)
            {
                EventLogger.LogEvent($"{__instance.pawn.LabelShortCap} had a mental break: {__instance.def.label}.");
            }
        }
    }

    // 🎪 INSPIRACIONES
    [HarmonyPatch(typeof(Inspiration), "PostStart")]
    public static class Patch_Inspiration
    {
        public static void Postfix(Inspiration __instance)
        {
            if (__instance.pawn.Faction == Faction.OfPlayer)
            {
                EventLogger.LogEvent($"{__instance.pawn.LabelShortCap} se sintió inspirado: {__instance.def.label}.");
            }
        }
    }
}