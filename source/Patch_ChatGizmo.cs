using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System;
using System.Linq;

namespace EchoColony
{
    [HarmonyPatch(typeof(Pawn_DraftController))]
    public static class Patch_ChatGizmo
    {
        [HarmonyPatch("GetGizmos")]
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_DraftController __instance)
        {
            // Devolver todos los gizmos originales primero
            foreach (var g in __result) 
                yield return g;

            // VERIFICACIONES TEMPRANAS
            if (__instance == null)
            {
                Log.Warning("[EchoColony] DraftController is null");
                yield break;
            }

            // OBTENER EL PAWN
            Pawn pawn = GetPawnFromDraftController(__instance);
            
            if (pawn == null)
            {
                yield break;
            }

            // VERIFICACIÓN DE MAPA
            if (pawn.Map == null || !pawn.Spawned)
            {
                yield break;
            }

            // VALIDACIONES BÁSICAS
            if (!IsValidChatPawn(pawn))
                yield break;

            // VERIFICAR COMPONENTES
            if (!AreComponentsInitialized())
            {
                Log.Warning("[EchoColony] Components not initialized, skipping gizmos");
                yield break;
            }

            List<Gizmo> extraGizmos = new List<Gizmo>();
            try
            {
                // OBTENER COLONOS CERCANOS
                var nearbyColonists = GetNearbyColonists(pawn);

                // CHAT INDIVIDUAL - siempre disponible
                extraGizmos.Add(CreateIndividualChatGizmo(pawn));

                // CHAT GRUPAL - solo para colonos libres y si hay otros cerca
                if (nearbyColonists.Count >= 1 && 
                    IsGroupChatAllowedForCurrentModel() &&
                    IsFreeColonist(pawn)) // Solo colonos libres pueden iniciar chat grupal
                {
                    extraGizmos.Add(CreateGroupChatGizmo(pawn, nearbyColonists));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error en Patch_ChatGizmo para {pawn?.LabelShort}: {ex.Message}");
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
                Log.Warning($"[EchoColony] Error verificando componentes: {ex.Message}");
                return false;
            }
        }

        private static Pawn GetPawnFromDraftController(Pawn_DraftController controller)
        {
            try
            {
                // Método 1: Propiedad directa
                if (controller.pawn != null)
                    return controller.pawn;

                // Método 2: Traverse
                var traverse = Traverse.Create(controller);
                if (traverse != null)
                {
                    var pawnFromTraverse = traverse.Field("pawn").GetValue<Pawn>();
                    if (pawnFromTraverse != null)
                        return pawnFromTraverse;
                }

                // Método 3: Reflexión
                var type = controller.GetType();
                var pawnField = type.GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pawnField != null)
                {
                    return pawnField.GetValue(controller) as Pawn;
                }

                var pawnProperty = type.GetProperty("pawn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (pawnProperty != null)
                {
                    return pawnProperty.GetValue(controller) as Pawn;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is NullReferenceException))
                {
                    Log.Warning($"[EchoColony] Error obteniendo pawn desde DraftController: {ex.Message}");
                }
            }

            return null;
        }

        // ✅ ACTUALIZADO: Validación para incluir colonos, esclavos y prisioneros
        private static bool IsValidChatPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Dead) return false;
                if (pawn.Destroyed) return false;
                if (!pawn.RaceProps.Humanlike) return false;
                
                // ✅ Permitir colonos de la facción del jugador
                if (pawn.Faction == Faction.OfPlayer)
                    return true;
                
                // ✅ Permitir esclavos
                if (pawn.IsSlave && pawn.SlaveFaction == Faction.OfPlayer)
                    return true;
                
                // ✅ Permitir prisioneros
                if (pawn.IsPrisoner && pawn.guest != null && pawn.guest.HostFaction == Faction.OfPlayer)
                    return true;
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error validando pawn {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
        }

        // ✅ NUEVO: Verificar si es un colono libre (no esclavo ni prisionero)
        private static bool IsFreeColonist(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.IsPrisoner) return false;
            if (pawn.IsSlave) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;
            return true;
        }

        // ✅ ACTUALIZADO: Obtener colonos cercanos (incluye esclavos, excluye prisioneros)
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
                               IsValidForGroupChat(p)) // ✅ Validación específica para grupo
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error obteniendo colonos cercanos para {pawn?.LabelShort}: {ex.Message}");
                return new List<Pawn>();
            }
        }

        // ✅ NUEVO: Validar si un pawn puede participar en chat grupal
        private static bool IsValidForGroupChat(Pawn pawn)
        {
            // Solo colonos libres y esclavos pueden participar en chats grupales
            // Prisioneros NO pueden participar en chats grupales
            if (pawn.IsPrisoner) return false;
            
            // Colonos libres
            if (pawn.Faction == Faction.OfPlayer && !pawn.IsSlave)
                return true;
            
            // Esclavos
            if (pawn.IsSlave && pawn.SlaveFaction == Faction.OfPlayer)
                return true;
            
            return false;
        }

        // ✅ ACTUALIZADO: Gizmo individual con texto adaptado
        private static Command_Action CreateIndividualChatGizmo(Pawn pawn)
        {
            // Adaptar el label según el tipo de pawn
            string label = "EchoColony.ChatGizmoLabel".Translate();
            string desc = "EchoColony.ChatGizmoDesc".Translate();
            
            if (pawn.IsPrisoner)
            {
                label = "Talk to Prisoner";
                desc = "Have a conversation with this prisoner";
            }
            else if (pawn.IsSlave)
            {
                label = "Talk to Slave";
                desc = "Have a conversation with this slave";
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
                        Log.Error($"[EchoColony] Error abriendo chat individual para {pawn?.LabelShort}: {ex.Message}\n{ex.StackTrace}");
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

                        // Filtrar participantes válidos
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
                        Log.Error($"[EchoColony] Error abriendo chat grupal para {pawn?.LabelShort}: {ex.Message}\n{ex.StackTrace}");
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
                Log.Warning($"[EchoColony] Error verificando modelo para chat grupal: {ex.Message}");
                return false;
            }
        }
    }
}