using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Evalúa qué colonos son elegibles para enviar mensajes espontáneos
    /// y selecciona los mejores candidatos
    /// </summary>
    public static class SpontaneousMessageEvaluator
    {
        /// <summary>
        /// Obtiene lista de colonos elegibles para enviar mensaje
        /// </summary>
        public static List<Pawn> GetEligibleColonists(TriggerType triggerType, IncidentTrigger? incidentTrigger = null)
        {
            if (!MyMod.Settings.IsSpontaneousMessagesActive())
                return new List<Pawn>();

            var eligible = new List<Pawn>();
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker == null) return eligible;

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (IsEligible(pawn, triggerType, tracker))
                    {
                        eligible.Add(pawn);
                    }
                }
            }

            return eligible;
        }

        /// <summary>
        /// Verifica si un colono específico es elegible
        /// </summary>
        private static bool IsEligible(Pawn pawn, TriggerType triggerType, SpontaneousMessageTracker tracker)
        {
            // 1. Verificar que sea un humanoide adulto que puede hablar
            if (!CanPawnSpeak(pawn))
                return false;

            // 2. Verificar settings básicos
            if (!tracker.CanSendMessage(pawn, triggerType))
                return false;

            // 3. Verificar consciencia mínima
            var consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            float minConsciousness = MyMod.Settings.minConsciousnessPercent / 100f;
            if (consciousness < minConsciousness)
                return false;

            // 4. Verificar que no esté en estado mental que impida hablar
            if (ColonistWillingnessEvaluator.IsInInvalidMentalState(pawn))
                return false;

            // 5. Verificar que no esté durmiendo
            if (pawn.CurJob?.def == JobDefOf.LayDown && pawn.needs?.rest?.CurLevel < 0.9f)
                return false;

            // 6. Verificar que no esté muerto o downed
            if (pawn.Dead || pawn.Downed)
                return false;

            // 7. Verificar que esté en el mapa (no en caravana)
            if (!pawn.Spawned)
                return false;

            return true;
        }

        /// <summary>
        /// Verifica si un pawn puede hablar (no es bebé o niño pequeño)
        /// </summary>
        private static bool CanPawnSpeak(Pawn pawn)
        {
            if (pawn == null) return false;

            // Verificar que tenga capacidad de hablar
            var talking = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 0f;
            if (talking < 0.3f)
                return false;

            // Verificar edad - los bebés y niños muy pequeños no pueden hablar coherentemente
            if (pawn.ageTracker != null)
            {
                // Menores de 3 años (bebés y toddlers) no pueden hablar
                if (pawn.ageTracker.AgeBiologicalYearsFloat < 3f)
                    return false;

                // Si el juego tiene definiciones de life stages, verificar
                if (pawn.ageTracker.CurLifeStageIndex < 2) // 0=baby, 1=child, 2+=teenager/adult
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Selecciona el mejor candidato de una lista de elegibles
        /// </summary>
        public static Pawn SelectBestCandidate(List<Pawn> eligible, TriggerType triggerType, IncidentTrigger? incidentTrigger)
        {
            if (!eligible.Any())
                return null;

            // Para incidentes de alta prioridad, podríamos retornar múltiples
            // Pero aquí retornamos uno y el caller puede llamar varias veces
            
            if (MyMod.Settings.prioritizeSocialTraits && triggerType == TriggerType.Random)
            {
                return SelectBySocialSkill(eligible);
            }
            else if (incidentTrigger.HasValue)
            {
                return SelectByRelevanceToIncident(eligible, incidentTrigger.Value);
            }
            else
            {
                // Selección aleatoria simple
                return eligible.RandomElement();
            }
        }

        /// <summary>
        /// Selecciona múltiples candidatos para un incidente (si se permite)
        /// </summary>
        public static List<Pawn> SelectBestCandidates(List<Pawn> eligible, IncidentTrigger incident, int maxCount)
        {
            var selected = new List<Pawn>();

            if (!eligible.Any() || maxCount <= 0)
                return selected;

            // Ordenar por relevancia al incidente
            var sorted = eligible.OrderByDescending(p => GetRelevanceScore(p, incident)).ToList();

            // Tomar los top maxCount
            for (int i = 0; i < System.Math.Min(maxCount, sorted.Count); i++)
            {
                selected.Add(sorted[i]);
            }

            return selected;
        }

        private static Pawn SelectBySocialSkill(List<Pawn> eligible)
        {
            // Weighted random basado en social skill
            var weights = eligible.Select(p =>
            {
                int socialSkill = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 5;
                return socialSkill + 5; // Base weight para que todos tengan chance
            }).ToList();

            return eligible.RandomElementByWeight(p =>
            {
                int index = eligible.IndexOf(p);
                return weights[index];
            });
        }

        private static Pawn SelectByRelevanceToIncident(List<Pawn> eligible, IncidentTrigger incident)
        {
            // Weighted random basado en relevancia
            return eligible.RandomElementByWeight(p => GetRelevanceScore(p, incident));
        }

        /// <summary>
        /// Calcula puntuación de relevancia de un colono para un incidente
        /// </summary>
        private static float GetRelevanceScore(Pawn pawn, IncidentTrigger incident)
        {
            float score = 10f; // Base score

            if (pawn.story?.traits == null)
                return score;

            // Ajustar por traits relevantes
            switch (incident)
            {
                case IncidentTrigger.Raid:
                case IncidentTrigger.MechanoidCluster:
                case IncidentTrigger.InfestationSpawned:
                    // Nervous, Brawler, Bloodlust son relevantes
                    var nervousTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Nervous");
                    if (nervousTrait != null && pawn.story.traits.HasTrait(nervousTrait))
                        score += 15f;
                    
                    if (pawn.story.traits.HasTrait(TraitDefOf.Brawler))
                        score += 10f;
                    
                    if (pawn.story.traits.HasTrait(TraitDefOf.Bloodlust))
                        score += 8f;
                    break;

                case IncidentTrigger.TraderCaravan:
                    // Greedy, Kind son relevantes
                    if (pawn.story.traits.HasTrait(TraitDefOf.Greedy))
                        score += 15f;
                    
                    if (pawn.story.traits.HasTrait(TraitDefOf.Kind))
                        score += 8f;
                    break;

                case IncidentTrigger.ToxicFallout:
                case IncidentTrigger.SolarFlare:
                    // Nervous es muy relevante
                    var nervousTraitFallout = DefDatabase<TraitDef>.GetNamedSilentFail("Nervous");
                    if (nervousTraitFallout != null && pawn.story.traits.HasTrait(nervousTraitFallout))
                        score += 20f;
                    break;

                case IncidentTrigger.WandererJoin:
                case IncidentTrigger.RefugeeChased:
                    // Kind es muy relevante
                    if (pawn.story.traits.HasTrait(TraitDefOf.Kind))
                        score += 15f;
                    
                    // Social skill alto también
                    int socialSkill = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 5;
                    score += socialSkill;
                    break;
            }

            // Bonus por social skill general
            if (MyMod.Settings.prioritizeSocialTraits)
            {
                int socialSkill = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 5;
                score += socialSkill * 0.5f;
            }

            return score;
        }

        /// <summary>
        /// Filtra colonos que tienen ya un mensaje pendiente
        /// (para evitar que un mismo colono acumule múltiples notificaciones)
        /// </summary>
        public static List<Pawn> FilterPendingResponses(List<Pawn> pawns)
        {
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker == null) return pawns;

            return pawns.Where(p => !tracker.HasPendingResponse(p)).ToList();
        }
    }
}