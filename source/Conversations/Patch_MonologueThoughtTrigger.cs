using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EchoColony.Conversations
{
    /// <summary>
    /// Harmony patch on MemoryThoughtHandler.TryGainMemory.
    ///
    /// When a pawn receives a memory thought with significant mood impact
    /// (e.g. "Witnessed death", "Ate fine meal", "Bonded animal died"),
    /// we trigger a monologue so they react out loud.
    ///
    /// Mirrors the approach in RimTalk's ThoughtTracker but simplified:
    /// we don't need deduplication state since PawnMonologueManager's
    /// cooldown already prevents spam.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory))]
    [HarmonyPatch(new Type[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_MonologueThoughtTrigger
    {
        public static void Postfix(Thought_Memory newThought, Pawn otherPawn)
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing) return;
                if (newThought?.pawn == null) return;

                var settings = MyMod.Settings;
                if (settings == null || !settings.enableMonologues) return;

                Pawn pawn = newThought.pawn;

                // Skip social thoughts — those are handled by conversations
                if (newThought is Thought_MemorySocial) return;

                // Check mood impact threshold
                float impact;
                try { impact = newThought.MoodOffset(); }
                catch { return; }

                if (System.Math.Abs(impact) < settings.monologueMinMoodImpact) return;

                // Don't react positively while in a mental break
                if (impact > 0 && pawn.InMentalState) return;

                // Build a short human-readable trigger description
                string triggerContext = BuildTriggerContext(newThought, impact);

                PawnMonologueManager.TryStartMonologue(pawn, triggerContext);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] MonologueThoughtTrigger error: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string BuildTriggerContext(Thought_Memory thought, float impact)
        {
            string label = thought.LabelCap ?? thought.def?.label ?? "something";

            if (impact >= 10f)
                return $"something wonderful just happened: {label}";
            if (impact >= 4f)
                return $"something good just happened: {label}";
            if (impact <= -10f)
                return $"something terrible just happened: {label}";
            if (impact <= -4f)
                return $"something upsetting just happened: {label}";

            return $"something just affected them: {label}";
        }
    }
}