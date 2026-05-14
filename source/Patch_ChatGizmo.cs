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
            foreach (var g in __result)
                yield return g;

            Pawn pawn = __instance;

            if (pawn == null || pawn.Map == null || !pawn.Spawned)
                yield break;

            if (!AreComponentsInitialized())
            {
                Log.Warning("[EchoColony] Components not initialized, skipping gizmos");
                yield break;
            }

            List<Gizmo> extraGizmos = new List<Gizmo>();
            try
            {
                var selectedForGroup = Find.Selector.SelectedObjects
                    .OfType<Pawn>()
                    .Where(p => IsValidForGroupChat(p)) //*furel* Simplified selection, IsValidForGruoupChat will check every thing.
                    .ToList();
				//Individual Gizmos
                if (Find.Selector.SingleSelectedThing == pawn && IsValidChatPawn(pawn))
                {
                    extraGizmos.Add(CreateIndividualChatGizmo(pawn));
                    extraGizmos.Add(CreateQuickChatGizmo(pawn));
                }
				//-------- Group Gizmos ------------
                if (IsGroupChatAllowedForCurrentModel())
                {
					//--------- Open Group Chat with Pawns selected --------------							
					ColonistGroupChatWindow.GroupSelectionMode InitialMode = ColonistGroupChatWindow.GroupSelectionMode.Room; ;	//*furel* Group open menú selection. This is used to select the mode of group chat when is open.												   		 
                    if (selectedForGroup.Count > 1)
                    {
                        if (pawn == selectedForGroup[0])
							InitialMode = ColonistGroupChatWindow.GroupSelectionMode.MapWide; //*furel* If pawns are selected the Group Chat opens in "Map" mode															 
                            extraGizmos.Add(CreateGroupChatGizmo(pawn, selectedForGroup, InitialMode)); //*furel* Added a new imput when creating the chat Windows
                    }
					//--------- Open Group Chat with one pawn selected --------------						 
                    else if (IsValidForGroupChat(pawn))
                    {
						//*furel* New way to select participansts. GetNearbyColonists is deprecated.
                        var nearbyColonists = new List<Pawn> ();
                        nearbyColonists = ColonistGroupChatWindow.GetPawnsInRoom(pawn); //*furel* Getting the list of pawns with the new funtion.
                        InitialMode = ColonistGroupChatWindow.GroupSelectionMode.Room;  //*furel* Open Group Chat Windos in Room mode.
                        if (nearbyColonists.Count >= 1)
                            extraGizmos.Add(CreateGroupChatGizmo(pawn, nearbyColonists, InitialMode)); //*furel* Added a new imput when creating the chat Windows
                    }
                }
            }
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

        private static bool IsValidChatPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Dead) return false;
                if (pawn.Destroyed) return false;
                if (!pawn.RaceProps.Humanlike) return false;

                if (pawn.Faction == Faction.OfPlayer) return true;
                if (pawn.IsSlave && pawn.SlaveFaction == Faction.OfPlayer) return true;
                if (pawn.IsPrisoner && pawn.guest != null && pawn.guest.HostFaction == Faction.OfPlayer) return true;

                return false;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error validating pawn {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
        }

		//*furel* No necessary any more with the method used now.																			
        //private static List<Pawn> GetNearbyColonists(Pawn pawn)
        //{
        //    try
        //    {
        //        if (pawn?.Map == null) return new List<Pawn>();

        //        var allPawns = pawn.Map.mapPawns?.AllPawnsSpawned;
        //        if (allPawns == null) return new List<Pawn>();

        //        return allPawns
        //            .Where(p => p != null &&
        //                        p != pawn &&
        //                        !p.Dead &&
        //                        !p.Destroyed &&
        //                        p.RaceProps.Humanlike &&
        //                        p.Spawned &&
        //                        p.Position.IsValid &&
        //                        pawn.Position.IsValid &&
        //                        p.Position.InHorDistOf(pawn.Position, 10f) &&
        //                        IsValidForGroupChat(p))
        //            .ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Warning($"[EchoColony] Error getting nearby colonists for {pawn?.LabelShort}: {ex.Message}");
        //        return new List<Pawn>();
        //    }
        //}

		//*furel* Changed private to public so it can be used in ColonistGroupChatWindows.cs. It can be moved to a separate Utility.cs with other utility functions																		 
        public static bool IsValidForGroupChat(Pawn pawn) 
        {
            //*furel* Added new filters to prevent assaign Chat Group for invalid pawns or animals
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Destroyed || !pawn.RaceProps.Humanlike) 
				return false;

            if (pawn.IsPrisoner) 
				return false;

            if (pawn.Faction == Faction.OfPlayer && !pawn.IsSlave)
                return true;

			if (pawn.IsSlaveOfColony)
				return true;

            return false;
        }

        private static Command_Action CreateIndividualChatGizmo(Pawn pawn)
        {
            string label = "EchoColony.ChatGizmoLabel".Translate();
            string desc  = "EchoColony.ChatGizmoDesc".Translate();

            if (pawn.IsPrisoner)
            {
                label = "EchoColony.ChatGizmoChatPris".Translate();
                desc  = "EchoColony.ChatGizmoChatPDes".Translate();
            }
            else if (pawn.IsSlave)
            {
                label = "EchoColony.ChatGizmoChatSlave".Translate();
                desc  = "EchoColony.ChatGizmoChatSDes".Translate();
            }

            return new Command_Action
            {
                defaultLabel = label,
                defaultDesc  = desc,
                icon         = MyModTextures.ChatIcon,
                action       = () =>
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

        private static Command_Action CreateQuickChatGizmo(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "Quick Chat",
                defaultDesc  = "Send a quick message to " + pawn.LabelShort + " without pausing the game.",
                icon         = MyModTextures.QuickChatIcon,
                action       = () =>
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

                        // Close any existing QuickChatWindow for this pawn before opening a new one.
                        var existing = Find.WindowStack.Windows
                            .OfType<QuickChatWindow>()
                            .FirstOrDefault(w => w.Pawn == pawn);
                        if (existing != null)
                            Find.WindowStack.TryRemove(existing, false);

                        Find.WindowStack.Add(new QuickChatWindow(pawn));
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[EchoColony] Error opening quick chat for {pawn?.LabelShort}: {ex.Message}\n{ex.StackTrace}");
                        Messages.Message("Error opening quick chat. Check logs for details.", MessageTypeDefOf.RejectInput);
                    }
                }
            };
        }
		//*furel* Added open window mode as new variable entry so chat window can open, modes are defined in ColonistChatWindow.cs.
        private static Command_Action CreateGroupChatGizmo(Pawn pawn, List<Pawn> nearbyColonists, ColonistGroupChatWindow.GroupSelectionMode mode)
        {
            return new Command_Action
            {
                defaultLabel = "EchoColony.GroupChat".Translate(),
                defaultDesc  = "EchoColony.GroupChatDesc".Translate(),
                icon         = MyModTextures.ChatIcon,
                action       = () =>
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
                        Find.WindowStack.Add(new ColonistGroupChatWindow(validParticipants, mode)); 
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