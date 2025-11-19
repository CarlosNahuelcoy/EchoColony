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
            // ✅ Devolver todos los gizmos originales primero
            foreach (var g in __result) 
                yield return g;

            // ✅ VERIFICACIONES TEMPRANAS para evitar errores
            if (__instance == null)
            {
                Log.Warning("[EchoColony] DraftController is null");
                yield break;
            }

            // ✅ OBTENER EL PAWN de forma más robusta
            Pawn pawn = GetPawnFromDraftController(__instance);
            
            if (pawn == null)
            {
                yield break; // Sin log para evitar spam
            }

            // ✅ VERIFICACIÓN DE MAPA y contexto
            if (pawn.Map == null || !pawn.Spawned)
            {
                yield break;
            }

            // ✅ VALIDACIONES BÁSICAS más robustas
            if (!IsValidChatPawn(pawn))
                yield break;

            // ✅ VERIFICAR SI LOS COMPONENTES ESTÁN INICIALIZADOS
            if (!AreComponentsInitialized())
            {
                Log.Warning("[EchoColony] Components not initialized, skipping gizmos");
                yield break;
            }

            List<Gizmo> extraGizmos = new List<Gizmo>();
            try
            {
                // ✅ OBTENER COLONOS CERCANOS de forma segura
                var nearbyColonists = GetNearbyColonists(pawn);

                // ✅ CHAT INDIVIDUAL - siempre disponible para colonos válidos
                extraGizmos.Add(CreateIndividualChatGizmo(pawn));

                // ✅ CHAT GRUPAL - solo si hay colonos cercanos y el modelo lo permite
                if (nearbyColonists.Count >= 1 && IsGroupChatAllowedForCurrentModel())
                {
                    extraGizmos.Add(CreateGroupChatGizmo(pawn, nearbyColonists));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error en Patch_ChatGizmo para {pawn?.LabelShort}: {ex.Message}");
                // No hacer yield break aquí, solo logear el error
            }

            foreach (var g in extraGizmos)
                yield return g;
        }

        // ✅ NUEVO: Verificar si los componentes del mod están inicializados
        private static bool AreComponentsInitialized()
        {
            try
            {
                // Verificar si el juego y sus componentes están listos
                if (Current.Game == null) return false;
                if (Find.CurrentMap == null) return false;
                
                // Verificar si MyStoryModComponent está inicializado
                if (MyStoryModComponent.Instance == null) return false;
                
                // Verificar si ChatGameComponent está disponible
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

        // ✅ MÉTODO ROBUSTO para obtener el pawn
        private static Pawn GetPawnFromDraftController(Pawn_DraftController controller)
        {
            try
            {
                // Método 1: Propiedad directa (más común)
                if (controller.pawn != null)
                    return controller.pawn;

                // Método 2: Traverse como fallback
                var traverse = Traverse.Create(controller);
                if (traverse != null)
                {
                    var pawnFromTraverse = traverse.Field("pawn").GetValue<Pawn>();
                    if (pawnFromTraverse != null)
                        return pawnFromTraverse;
                }

                // Método 3: Reflexión como último recurso
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
                // Solo logear si es un error inesperado
                if (!(ex is NullReferenceException))
                {
                    Log.Warning($"[EchoColony] Error obteniendo pawn desde DraftController: {ex.Message}");
                }
            }

            return null;
        }

        // ✅ VALIDACIÓN MÁS ROBUSTA del pawn
        private static bool IsValidChatPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null) return false;
                if (pawn.Dead) return false;
                if (pawn.Destroyed) return false;
                if (pawn.Faction != Faction.OfPlayer) return false;
                if (!pawn.RaceProps.Humanlike) return false;
                
                // ✅ Verificación más específica para RimWorld 1.5
                bool isColonist = pawn.IsColonistPlayerControlled;
                
                // ✅ Fallback para casos edge
                if (!isColonist)
                {
                    isColonist = pawn.Faction == Faction.OfPlayer && 
                               pawn.RaceProps.Humanlike && 
                               !pawn.IsQuestLodger() &&
                               pawn.HomeFaction == Faction.OfPlayer;
                }
                
                return isColonist;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error validando pawn {pawn?.LabelShort}: {ex.Message}");
                return false;
            }
        }

        // ✅ MÉTODO SEGURO para obtener colonos cercanos
        private static List<Pawn> GetNearbyColonists(Pawn pawn)
        {
            try
            {
                if (pawn?.Map == null) return new List<Pawn>();

                var freeColonists = pawn.Map.mapPawns?.FreeColonistsSpawned;
                if (freeColonists == null) return new List<Pawn>();

                return freeColonists
                    .Where(p => p != null &&
                               p != pawn && 
                               !p.Dead && 
                               !p.Destroyed &&
                               p.RaceProps.Humanlike &&
                               p.Faction == Faction.OfPlayer &&
                               p.Spawned &&
                               p.Position.IsValid &&
                               pawn.Position.IsValid &&
                               p.Position.InHorDistOf(pawn.Position, 10f))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Error obteniendo colonos cercanos para {pawn?.LabelShort}: {ex.Message}");
                return new List<Pawn>();
            }
        }

        // ✅ CREAR GIZMO DE CHAT INDIVIDUAL con manejo de errores
        private static Command_Action CreateIndividualChatGizmo(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "EchoColony.ChatGizmoLabel".Translate(),
                defaultDesc = "EchoColony.ChatGizmoDesc".Translate(),
                icon = MyModTextures.ChatIcon,
                action = () =>
                {
                    try
                    {
                        // ✅ Verificaciones adicionales antes de abrir ventana
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

        // ✅ CREAR GIZMO DE CHAT GRUPAL con manejo de errores
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
                        // ✅ Verificaciones adicionales
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

                        // ✅ Filtrar participantes válidos
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

        // ✅ VERIFICAR SI EL CHAT GRUPAL ESTÁ PERMITIDO
        private static bool IsGroupChatAllowedForCurrentModel()
        {
            try
            {
                // ✅ Verificar que MyMod.Settings no sea null
                if (MyMod.Settings == null) return false;

                switch (MyMod.Settings.modelSource)
                {
                    case ModelSource.Player2:    // Player
                    case ModelSource.OpenRouter: // OpenRouter
                    case ModelSource.Gemini:     // Gemini
                        return true;
                    case ModelSource.Local:      // Modelo local - no permitido
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