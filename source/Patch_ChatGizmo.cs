using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System;
using System.Linq;

namespace EchoColony
{
    [HarmonyPatch(typeof(Pawn))]
    public static class Patch_ChatGizmo
    {
        [HarmonyPatch("GetGizmos")]
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            // Return all original gizmos first
            foreach (var g in __result)
                yield return g;

            Pawn pawn = __instance;

            // Early checks
            if (pawn == null || pawn.Map == null || !pawn.Spawned)
                yield break;

            //if (!IsValidChatPawn(pawn))
            //    yield break;

            if (!AreComponentsInitialized())
            {
                Log.Warning("[EchoColony] Components not initialized, skipping gizmos");
                yield break;
            }
            List<Gizmo> extraGizmos = new List<Gizmo>();
            try
            {
                //*furel - selection improvments* Make a list for valid selected pawns for a group chat 
                var selectedForGroup = Find.Selector.SelectedObjects
                    .OfType<Pawn>()
                    .Where(p => p != null && !p.Dead && p.Spawned && IsValidForGroupChat(p))
                    .ToList();
                //Solo chat
                if (Find.Selector.SingleSelectedThing == pawn && IsValidChatPawn(pawn))
                {
                    extraGizmos.Add(CreateIndividualChatGizmo(pawn));
                }
                
                if (IsGroupChatAllowedForCurrentModel())
                {
                    //Selected group chat
                    if (selectedForGroup.Count > 1)
                    {
                        if (pawn == selectedForGroup[0])
                        {
                            extraGizmos.Add(CreateGroupChatGizmo(pawn, selectedForGroup));
                        }
                    }
                    //Solo opening group chat
                    else if (IsValidForGroupChat(pawn))
                    {
                        var nearbyColonists = GetNearbyColonists(pawn);
                        if (nearbyColonists.Count >= 1)
                        {
                            extraGizmos.Add(CreateGroupChatGizmo(pawn, nearbyColonists));
                        }
                    }
                }  
            }
            //{
            //    // Individual chat - always available for valid pawns
            //    extraGizmos.Add(CreateIndividualChatGizmo(pawn));

            //    // Group chat - only for free colonists with others nearby
            //    if (IsFreeColonist(pawn) && IsGroupChatAllowedForCurrentModel())
            //    {
            //        var nearbyColonists = GetNearbyColonists(pawn);
            //        if (nearbyColonists.Count >= 1)
            //            extraGizmos.Add(CreateGroupChatGizmo(pawn, nearbyColonists));
            //    }
            //}
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error in Patch_ChatGizmo for {pawn?.LabelShort}: {ex.Message}");
            }

            foreach (var g in extraGizmos)
                yield return g;
        }

        private static bool AreComponentsInitialized()
        {
            try
            {
                if (Current.Game == null) return false;
                if (Find.CurrentMap == null) return false;
                if (MyStoryModComponent.Instance == null) return false;

                var chatComponent = ChatGameComponent.Instance;
                if (chatComponent == null) return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error checking components: {ex.Message}");
                return false;
            }
        }

        // Valid chat pawns: free colonists, slaves, and prisoners belonging to the player
        private static bool IsValidChatPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Dead) return false;
                if (pawn.Destroyed) return false;
                if (!pawn.RaceProps.Humanlike) return false;

                // Free colonists
                if (pawn.Faction == Faction.OfPlayer)
                    return true;

                // Slaves
                if (pawn.IsSlave && pawn.SlaveFaction == Faction.OfPlayer)
                    return true;

                // Prisoners
                if (pawn.IsPrisoner && pawn.guest != null && pawn.guest.HostFaction == Faction.OfPlayer)
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error validating pawn {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
        }

        // Only free colonists (not slaves, not prisoners) can initiate group chats
        private static bool IsFreeColonist(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.IsPrisoner) return false;
            if (pawn.IsSlave) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;
            return true;
        }

        // Nearby colonists for group chat: free colonists and slaves, NOT prisoners
        private static List<Pawn> GetNearbyColonists(Pawn pawn)
        {
            try
            {
                if (pawn?.Map == null) return new List<Pawn>();

                var allPawns = pawn.Map.mapPawns?.AllPawnsSpawned;
                if (allPawns == null) return new List<Pawn>();

                return allPawns
                    .Where(p => p != null &&
                                p != pawn &&
                                !p.Dead &&
                                !p.Destroyed &&
                                p.RaceProps.Humanlike &&
                                p.Spawned &&
                                p.Position.IsValid &&
                                pawn.Position.IsValid &&
                                p.Position.InHorDistOf(pawn.Position, 10f) &&
                                IsValidForGroupChat(p))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error getting nearby colonists for {pawn?.LabelShort}: {ex.Message}");
                return new List<Pawn>();
            }
        }

        // Free colonists and slaves can participate in group chat, NOT prisoners
        private static bool IsValidForGroupChat(Pawn pawn)
        {
            if (pawn.IsPrisoner) return false;

            if (pawn.Faction == Faction.OfPlayer && !pawn.IsSlave)
                return true;

            if (pawn.IsSlave && pawn.SlaveFaction == Faction.OfPlayer)
                return true;

            return false;
        }

        private static Command_Action CreateIndividualChatGizmo(Pawn pawn)
        {
            string label = "EchoColony.ChatGizmoLabel".Translate();
            string desc = "EchoColony.ChatGizmoDesc".Translate();

            if (pawn.IsPrisoner)
            {
                label = "EchoColony.ChatGizmoChatPris".Translate();
                desc = "EchoColony.ChatGizmoChatPDes".Translate();
            }
            else if (pawn.IsSlave)
            {
                label = "EchoColony.ChatGizmoChatSlave".Translate();
                desc = "EchoColony.ChatGizmoChatSDes".Translate();
            }

            return new Command_Action
            {
                defaultLabel = label,
                defaultDesc = desc,
                icon = MyModTextures.ChatIcon,
                action = () =>
                {
                    try
                    {
                        if (pawn == null || pawn.Dead || pawn.Destroyed)
                        {
                            Messages.Message("Cannot chat with invalid colonist.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        if (!AreComponentsInitialized())
                        {
                            Messages.Message("Chat system not ready. Please try again.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        Find.WindowStack.Add(new ColonistChatWindow(pawn));
                        Find.TickManager.slower.SignalForceNormalSpeedShort();
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Error opening individual chat for {pawn?.LabelShort}: {ex.Message}\n{ex.StackTrace}");
                        Messages.Message("Error opening individual chat. Check logs for details.", MessageTypeDefOf.RejectInput);
                    }
                }
            };
        }

        private static Command_Action CreateGroupChatGizmo(Pawn pawn, List<Pawn> nearbyColonists)
        {
            return new Command_Action
            {
                defaultLabel = "EchoColony.GroupChat".Translate(),
                defaultDesc = "EchoColony.GroupChatDesc".Translate(),
                icon = MyModTextures.ChatIcon,
                action = () =>
                {
                    try
                    {
                        if (pawn == null || pawn.Dead || pawn.Destroyed)
                        {
                            Messages.Message("Cannot start group chat with invalid colonist.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        if (!AreComponentsInitialized())
                        {
                            Messages.Message("Chat system not ready. Please try again.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        var validParticipants = nearbyColonists
                            .Where(p => p != null && !p.Dead && !p.Destroyed && p.Spawned)
                            .ToList();

                        if (validParticipants.Count == 0)
                        {
                            Messages.Message("No valid colonists nearby for group chat.", MessageTypeDefOf.RejectInput);
                            return;
                        }

                        validParticipants.Insert(0, pawn);
                        Find.WindowStack.Add(new ColonistGroupChatWindow(validParticipants));
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Error opening group chat for {pawn?.LabelShort}: {ex.Message}\n{ex.StackTrace}");
                        Messages.Message("Error opening group chat. Check logs for details.", MessageTypeDefOf.RejectInput);
                    }
                }
            };
        }

        private static bool IsGroupChatAllowedForCurrentModel()
        {
            try
            {
                if (MyMod.Settings == null) return false;

                switch (MyMod.Settings.modelSource)
                {
                    case ModelSource.Player2:
                    case ModelSource.OpenRouter:
                    case ModelSource.Gemini:
                        return true;
                    case ModelSource.Local:
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error checking model for group chat: {ex.Message}");
                return false;
            }
        }
    }
}