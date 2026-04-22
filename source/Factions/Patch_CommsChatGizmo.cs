using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EchoColony.Factions
{
    /// <summary>
    /// Patches FactionDialogMaker.FactionDialogFor to inject two EchoColony
    /// options into the comms console dialog:
    ///
    ///   1. [Colonist speaks] — operator pawn mediates, Social skill applies
    ///   2. [You speak directly] — player speaks, no skill modifier
    ///
    /// Each mode has its own separate chat history per faction.
    /// </summary>
    [HarmonyPatch(typeof(FactionDialogMaker))]
    [HarmonyPatch("FactionDialogFor")]
    public static class Patch_CommsChatGizmo
    {
        // Tracks which DiaNode instances we've already injected into,
        // preventing double-injection if RimWorld calls FactionDialogFor twice.
        private static readonly HashSet<int> _processedNodes = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            try
            {
                if (__result == null || negotiator == null || faction == null)
                    return;

                if (!IsEchoColonyReady())
                    return;

                if (MyMod.Settings?.enableFactionCommsChat != true)
                    return;

                if (faction.def.permanentEnemy)
                    return;

                // Guard against double-injection using the node's runtime identity
                int nodeId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(__result);
                if (_processedNodes.Contains(nodeId))
                    return;
                _processedNodes.Add(nodeId);

                // Keep the set small — clear old entries periodically
                if (_processedNodes.Count > 50)
                    _processedNodes.Clear();

                var options = __result.options;
                if (options == null) return;

                // Insert both options before the last one (always "Hang up")
                int insertAt = Math.Max(0, options.Count - 1);

                // Insert in reverse so colonist option ends up above player option
                options.Insert(insertAt, BuildOption(negotiator, faction, isPlayerMode: true));
                options.Insert(insertAt, BuildOption(negotiator, faction, isPlayerMode: false));
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Patch_CommsChatGizmo error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // OPTION BUILDER
        // ═══════════════════════════════════════════════════════════════

        private static DiaOption BuildOption(Pawn negotiator, Faction faction, bool isPlayerMode)
        {
            var    component   = FactionChatGameComponent.Instance;
            string leaderName  = FactionPromptContextBuilder.GetLeaderName(faction);
            bool   isFirst     = component?.IsFirstContact(faction, isPlayerMode) ?? true;
            int    convCount   = component?.GetConversationCount(faction, isPlayerMode) ?? 0;
            bool   onCooldown  = FactionActions.IsOnCooldown;

            string label;

            if (isPlayerMode)
            {
                string history = isFirst
                    ? $"Open direct channel with {leaderName}"
                    : $"Call {leaderName} directly ({convCount} call{(convCount != 1 ? "s" : "")})";

                label = $"[EchoColony] {history} [You speak]";
            }
            else
            {
                string history    = isFirst
                    ? $"Open comms channel with {leaderName}"
                    : $"Call {leaderName} via {negotiator.LabelShort} ({convCount} call{(convCount != 1 ? "s" : "")})";

                label = $"[EchoColony] {history} [{negotiator.LabelShort} speaks]";
            }

            if (onCooldown)
                label += $" — cooldown: {FactionActions.CooldownDescription}";

            return new DiaOption(label)
            {
                resolveTree = true,
                action = () =>
                {
                    try
                    {
                        Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), doCloseSound: false);
                        Find.WindowStack.Add(new FactionChatWindow(negotiator, faction, isPlayerMode));
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;

                        Log.Message($"[EchoColony] Faction chat opened: {faction.Name} " +
                                    $"operator={negotiator.LabelShort} " +
                                    $"mode={(isPlayerMode ? "player" : "colonist")}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Error opening faction chat: {ex.Message}");
                        Messages.Message("Error opening comms channel. Check logs.",
                            MessageTypeDefOf.RejectInput);
                    }
                }
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static bool IsEchoColonyReady()
        {
            try
            {
                if (Current.Game == null)                      return false;
                if (Find.CurrentMap == null)                   return false;
                if (MyStoryModComponent.Instance == null)      return false;
                if (FactionChatGameComponent.Instance == null) return false;
                return true;
            }
            catch { return false; }
        }
    }
}