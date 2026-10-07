using System;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EchoColony
{
    /// <summary>
    /// Limpieza de textos del juego para meterlos en un prompt.
    /// </summary>
    public static class StorytellerEventText
    {
        // Quita etiquetas de gramática ("[PAWN_nameDef]", "{0}") de textos de cartas
        private static readonly Regex Placeholders = new Regex(@"\[[^\]]*\]|\{[^}]*\}", RegexOptions.Compiled);

        public static string Clean(string text, int maxLength = 250)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = Placeholders.Replace(text.StripTags(), "").Replace("\n", " ").Replace("  ", " ").Trim();
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        public static string PawnSummary(Pawn pawn)
        {
            if (pawn == null) return "someone";
            var sb = new StringBuilder(pawn.LabelShortCap);
            if (pawn.ageTracker != null) sb.Append($", age {pawn.ageTracker.AgeBiologicalYears}");
            if (pawn.story?.TitleShort != null) sb.Append($", {pawn.story.TitleShort}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Reacciones del storyteller a sucesos que no son incidentes: muertes, crisis mentales
    /// extremas, relaciones y proyectos de investigación terminados.
    /// Se aplican parche por parche (no con PatchAll): si otro mod cambió uno de estos
    /// métodos, los demás siguen funcionando.
    /// Basado en EventInterceptor de The Storyteller's Voice.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class StorytellerEventPatches
    {
        static StorytellerEventPatches()
        {
            var harmony = new Harmony("EchoColony.StorytellerEvents");

            TryPatch(harmony, AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
                nameof(Pawn_Kill_Prefix), nameof(Pawn_Kill_Postfix));
            TryPatch(harmony, AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
                null, nameof(MentalState_Postfix));
            TryPatch(harmony, AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation)),
                null, nameof(AddDirectRelation_Postfix));
            TryPatch(harmony, AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.FinishProject)),
                null, nameof(FinishProject_Postfix));
        }

        private static void TryPatch(Harmony harmony, System.Reflection.MethodInfo original, string prefix, string postfix)
        {
            try
            {
                if (original == null)
                    throw new Exception("method not found");

                harmony.Patch(original,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(StorytellerEventPatches), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(StorytellerEventPatches), postfix));
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Could not apply storyteller event patch {postfix ?? prefix}: {ex.Message}");
            }
        }

        // Los comentarios de eventos dependen del modo "Solo incidentes" / "Completo"
        private static bool EventCommentsEnabled =>
            MyMod.Settings != null &&
            MyMod.Settings.AreStorytellerIncidentMessagesEnabled() &&
            Current.ProgramState == ProgramState.Playing;

        private static bool PassesChance() =>
            Rand.Chance(MyMod.Settings?.storytellerIncidentChance ?? 0.3f);

        private static void Request(string situation, string eventKey, bool priority)
        {
            StorytellerSpontaneousMessageSystem.GenerateSpontaneousMessage(
                StorytellerSpontaneousMessageSystem.MessageTriggerType.Incident,
                situation: situation, eventKey: eventKey, priority: priority);
        }

        private static void DebugWarning(string what, Exception ex)
        {
            if (MyMod.Settings?.debugMode == true)
                Log.Warning($"[EchoColony] Storyteller {what} patch error: {ex.Message}");
        }

        // ═══════════════════════════════════════════════════════════════
        // MUERTE DE UN COLONO (siempre, ignora probabilidad y cooldown)
        // ═══════════════════════════════════════════════════════════════

        public static void Pawn_Kill_Prefix(Pawn __instance, out bool __state)
        {
            __state = false;
            try
            {
                __state = EventCommentsEnabled && MyMod.Settings.storytellerCommentOnDeaths &&
                          !__instance.Dead && __instance.IsColonist && __instance.RaceProps.Humanlike;
            }
            catch { }
        }

        public static void Pawn_Kill_Postfix(Pawn __instance, DamageInfo? dinfo, bool __state)
        {
            try
            {
                if (!__state || !__instance.Dead) return;

                var sb = new StringBuilder();
                sb.AppendLine($"A colonist just died: {StorytellerEventText.PawnSummary(__instance)}.");

                if (dinfo.HasValue)
                {
                    var info = dinfo.Value;
                    if (info.Def != null) sb.AppendLine($"Cause: {info.Def.label}");
                    if (info.Instigator != null && info.Instigator != __instance)
                        sb.AppendLine($"Killed by: {info.Instigator.LabelShort}");
                }

                int remaining = __instance.MapHeld?.mapPawns?.FreeColonistsCount ?? 0;
                sb.AppendLine($"Colonists left alive on that map: {remaining}");
                sb.AppendLine("This is a sad moment; stay in character, but don't mock the loss.");

                Request(sb.ToString(), "death:" + __instance.ThingID, priority: true);
            }
            catch (Exception ex)
            {
                DebugWarning("death", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CRISIS MENTALES EXTREMAS (las leves no se comentan)
        // ═══════════════════════════════════════════════════════════════

        public static void MentalState_Postfix(Pawn ___pawn, MentalStateDef stateDef, bool __result)
        {
            try
            {
                if (!__result || stateDef == null || !EventCommentsEnabled || !MyMod.Settings.storytellerCommentOnMentalBreaks)
                    return;

                Pawn pawn = ___pawn;
                if (pawn == null || !pawn.IsColonist) return;
                if (!stateDef.IsExtreme && !stateDef.IsAggro) return;

                var sb = new StringBuilder();
                sb.AppendLine($"{StorytellerEventText.PawnSummary(pawn)} just had an extreme mental break: {stateDef.label}.");
                if (pawn.needs?.mood != null)
                    sb.AppendLine($"Their mood: {pawn.needs.mood.CurLevelPercentage:P0}");

                Request(sb.ToString(), $"mental:{pawn.ThingID}:{stateDef.defName}", priority: true);
            }
            catch (Exception ex)
            {
                DebugWarning("mental break", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RELACIONES (pareja, compromiso, boda)
        // ═══════════════════════════════════════════════════════════════

        public static void AddDirectRelation_Postfix(Pawn ___pawn, PawnRelationDef def, Pawn otherPawn)
        {
            try
            {
                if (def == null || otherPawn == null || !EventCommentsEnabled || !MyMod.Settings.storytellerCommentOnRelationships)
                    return;
                if (def != PawnRelationDefOf.Lover && def != PawnRelationDefOf.Fiance && def != PawnRelationDefOf.Spouse)
                    return;

                Pawn pawn = ___pawn;
                if (pawn == null || !pawn.IsColonist || !otherPawn.IsColonist) return;
                if (!PassesChance()) return;

                // Mismo par en cualquier orden = mismo evento
                string a = pawn.ThingID, b = otherPawn.ThingID;
                string key = $"relation:{def.defName}:{(string.CompareOrdinal(a, b) < 0 ? a + b : b + a)}";

                Request($"{StorytellerEventText.PawnSummary(pawn)} and {StorytellerEventText.PawnSummary(otherPawn)} just became {def.label}s.",
                    key, priority: false);
            }
            catch (Exception ex)
            {
                DebugWarning("relationship", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INVESTIGACIÓN COMPLETADA
        // ═══════════════════════════════════════════════════════════════

        public static void FinishProject_Postfix(ResearchProjectDef proj)
        {
            try
            {
                if (proj == null || !EventCommentsEnabled || !MyMod.Settings.storytellerCommentOnResearch)
                    return;
                if (!PassesChance()) return;

                string desc = StorytellerEventText.Clean(proj.description, 200);
                Request($"The colony just finished researching: {proj.LabelCap}." + (desc.Length > 0 ? $"\nDescription: {desc}" : ""),
                    "research:" + proj.defName, priority: false);
            }
            catch (Exception ex)
            {
                DebugWarning("research", ex);
            }
        }
    }
}
