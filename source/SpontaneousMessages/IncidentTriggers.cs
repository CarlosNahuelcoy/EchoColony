using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Harmony patches para interceptar incidentes específicos del juego
    /// y generar mensajes espontáneos de colonos en respuesta
    /// </summary>
    [StaticConstructorOnStartup]
    public static class IncidentTriggers
    {
        static IncidentTriggers()
        {
            var harmony = new Harmony("rimworld.echocolony.spontaneousmessages");
            harmony.PatchAll();
            Log.Message("[EchoColony] SpontaneousMessages: Incident triggers patched");
        }

        /// <summary>
        /// Patch base para IncidentWorker.TryExecuteWorker
        /// Captura TODOS los incidentes y los procesa según tipo
        /// </summary>
        [HarmonyPatch(typeof(IncidentWorker), "TryExecuteWorker")]
        public static class IncidentWorker_TryExecuteWorker_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
            {
                if (!__result) return; // Incidente falló

                try
                {
                    // Verificar que el sistema esté activo
                    if (!MyMod.Settings?.IsSpontaneousMessagesActive() ?? true)
                        return;

                    if (!MyMod.Settings?.AreIncidentMessagesEnabled() ?? true)
                        return;

                    // Identificar el tipo de incidente y procesarlo
                    ProcessIncident(__instance.def, parms);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[EchoColony] Error processing incident trigger: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Procesa un incidente y determina si debe generar mensaje
        /// </summary>
        private static void ProcessIncident(IncidentDef incidentDef, IncidentParms parms)
        {
            if (incidentDef == null) return;

            // Mapear IncidentDef a nuestro IncidentTrigger enum
            IncidentTrigger trigger = MapIncidentToTrigger(incidentDef);
            
            if (trigger == IncidentTrigger.Other)
            {
                // Incidente no relevante o no mapeado
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] Incident not mapped: {incidentDef.defName}");
                }
                return;
            }

            // Construir descripción contextual
            string description = BuildIncidentDescription(incidentDef, parms);

            // Generar mensaje de colono(s)
            TriggerSpontaneousMessage(trigger, description);
        }

        /// <summary>
        /// Mapea IncidentDef del juego a nuestro enum IncidentTrigger
        /// </summary>
        private static IncidentTrigger MapIncidentToTrigger(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName.ToLower();

            // RAIDS
            if (defName.Contains("raid") || defName == "raidenemybegin")
                return IncidentTrigger.Raid;

            // MECHANOIDS
            if (defName.Contains("mechanoid") || defName.Contains("mechcluster"))
                return IncidentTrigger.MechanoidCluster;

            // INFESTATION
            if (defName.Contains("infestation"))
                return IncidentTrigger.InfestationSpawned;

            // TOXIC FALLOUT
            if (defName.Contains("toxicfallout"))
                return IncidentTrigger.ToxicFallout;

            // METEORITE
            if (defName.Contains("meteorite"))
                return IncidentTrigger.MeteoriteIncoming;

            // TRADER CARAVAN
            if (defName.Contains("tradercaravan") || defName.Contains("orbitaltraderr"))
                return IncidentTrigger.TraderCaravan;

            // SOLAR FLARE
            if (defName.Contains("solarflare"))
                return IncidentTrigger.SolarFlare;

            // ECLIPSE
            if (defName.Contains("eclipse"))
                return IncidentTrigger.Eclipse;

            // MANHUNTER PACK
            if (defName.Contains("manhunter"))
                return IncidentTrigger.Manhunter;

            // WANDERER JOIN
            if (defName.Contains("wandererjoin"))
                return IncidentTrigger.WandererJoin;

            // REFUGEE CHASED
            if (defName.Contains("refugeechased"))
                return IncidentTrigger.RefugeeChased;

            // AURORA
            if (defName.Contains("aurora"))
                return IncidentTrigger.Aurora;

            // TRANSPORT POD CRASH
            if (defName.Contains("transportpod") && defName.Contains("crash"))
                return IncidentTrigger.TransportPodCrash;

            return IncidentTrigger.Other;
        }

        /// <summary>
        /// Construye descripción legible del incidente
        /// </summary>
        private static string BuildIncidentDescription(IncidentDef incidentDef, IncidentParms parms)
        {
            string desc = incidentDef.letterLabel ?? incidentDef.label ?? incidentDef.defName;

            // Agregar detalles específicos según el tipo
            if (parms != null)
            {
                // Si hay faction, agregar info
                if (parms.faction != null)
                {
                    desc += $" from {parms.faction.Name}";
                }

                // Si hay puntos, mencionar la amenaza
                if (parms.points > 0)
                {
                    if (parms.points > 5000)
                        desc += " (major threat)";
                    else if (parms.points > 2000)
                        desc += " (significant threat)";
                }
            }

            return desc;
        }

        /// <summary>
        /// Trigger principal: genera mensaje(s) espontáneo(s) en respuesta al incidente
        /// </summary>
        public static void TriggerSpontaneousMessage(IncidentTrigger incident, string description)
        {
            if (MyMod.Settings?.debugMode == true)
            {
                Log.Message($"[EchoColony] Incident triggered: {incident} - {description}");
            }

            // Obtener colonos elegibles
            var eligible = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Incident, incident);

            if (!eligible.Any())
            {
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] No eligible colonists for incident {incident}");
                }
                return;
            }

            // Filtrar los que ya tienen mensajes pendientes (evitar spam)
            eligible = SpontaneousMessageEvaluator.FilterPendingResponses(eligible);

            if (!eligible.Any())
            {
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] All eligible colonists have pending responses");
                }
                return;
            }

            // Determinar cuántos colonos pueden responder
            int maxResponders = incident.MaxResponders();

            // Seleccionar mejores candidatos
            var selected = SpontaneousMessageEvaluator.SelectBestCandidates(eligible, incident, maxResponders);

            if (MyMod.Settings?.debugMode == true)
            {
                Log.Message($"[EchoColony] Selected {selected.Count} colonists from {eligible.Count} eligible for {incident}");
            }

            // Generar mensajes para cada colono seleccionado
            foreach (var colonist in selected)
            {
                // Verificar si el colono "quiere hablar"
                if (!ColonistWillingnessEvaluator.WantsToSpeak(colonist, TriggerType.Incident, description))
                {
                    if (MyMod.Settings?.debugMode == true)
                    {
                        Log.Message($"[EchoColony] {colonist.LabelShort} doesn't want to speak about {incident}");
                    }
                    continue;
                }

                // Crear request
                float urgency = incident.IsHighPriority() ? 0.9f : incident.IsMediumPriority() ? 0.6f : 0.4f;
                var request = new MessageRequest(colonist, incident, description, urgency);

                // Generar mensaje (async via coroutine)
                if (MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.StartCoroutine(
                        SpontaneousMessageGenerator.GenerateAndSendMessage(request)
                    );
                }
                else
                {
                    Log.Warning($"[EchoColony] MyStoryModComponent not available for message generation");
                }
            }
        }
    }
}