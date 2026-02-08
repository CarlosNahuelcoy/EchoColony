using RimWorld;
using Verse;
using System.Linq;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Determina si un colono "tiene ganas" de iniciar una conversación
    /// basándose en personalidad, estado de ánimo, y factores situacionales
    /// </summary>
    public static class ColonistWillingnessEvaluator
    {
        /// <summary>
        /// Evalúa si el colono quiere hablar en este momento
        /// Retorna false aproximadamente 30-70% del tiempo dependiendo de factores
        /// </summary>
        public static bool WantsToSpeak(Pawn pawn, TriggerType trigger, string context)
        {
            if (pawn == null) return false;

            float willingnessScore = 1f;

            // 1. ESTADO DE ÁNIMO - Impacto fuerte
            willingnessScore *= GetMoodFactor(pawn, trigger);

            // 2. TRAITS DE PERSONALIDAD
            willingnessScore *= GetTraitsFactor(pawn, trigger);

            // 3. HABILIDAD SOCIAL
            willingnessScore *= GetSocialSkillFactor(pawn, trigger);

            // 4. CONVERSACIÓN RECIENTE
            willingnessScore *= GetRecentChatFactor(pawn);

            // 5. ESTADO FÍSICO
            willingnessScore *= GetPhysicalStateFactor(pawn);

            // 6. FACTOR ALEATORIO BASE (siempre hay incertidumbre)
            willingnessScore *= Rand.Range(0.5f, 1.2f);

            // Threshold final: si la puntuación es menor a 0.7, no quiere hablar
            return willingnessScore >= 0.7f;
        }

        private static float GetMoodFactor(Pawn pawn, TriggerType trigger)
        {
            float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;

            // NUEVA LÓGICA INTELIGENTE: Necesidades urgentes AUMENTAN willingness
            // pero colapso total la REDUCE
            bool hasUrgentNeed = HasUrgentNeed(pawn);

            if (hasUrgentNeed && mood > 0.15f)
            {
                // Tiene hambre/frío/etc pero AÚN está consciente → QUIERE ayuda
                // Esto hace que colonos con necesidades hablen MÁS
                return trigger == TriggerType.CriticalNeed ? 1.8f : 1.5f;
            }

            // Colapso total - demasiado débil para hablar
            if (mood < 0.15f) 
                return 0.1f; // Casi nunca habla si está al borde del colapso

            // Para mensajes random, el mood afecta mucho
            if (trigger == TriggerType.Random)
            {
                if (mood < 0.30f) return 0.4f; // Poco probable si está triste
                if (mood > 0.80f) return 1.3f; // Más probable si está feliz
            }
            // Para incidentes, el mood afecta menos (reaccionar es natural)
            else if (trigger == TriggerType.Incident)
            {
                if (mood < 0.20f) return 0.7f;  // Aún así puede reaccionar
                if (mood > 0.80f) return 1.1f;  // Ligeramente más probable
            }

            return 1f;
        }

        /// <summary>
        /// Verifica si el pawn tiene una necesidad urgente pero NO crítica
        /// (Rango donde puede hablar y QUIERE ayuda)
        /// </summary>
        private static bool HasUrgentNeed(Pawn pawn)
        {
            if (pawn?.needs == null)
                return false;

            // Hambre urgente: 15-40% (quiere comida, puede hablar)
            var food = pawn.needs.food;
            if (food != null && food.CurLevel < 0.40f && food.CurLevel > 0.15f)
                return true;

            // Cansancio urgente: 10-30% (muy cansado, puede hablar)
            var rest = pawn.needs.rest;
            if (rest != null && rest.CurLevel < 0.30f && rest.CurLevel > 0.10f)
                return true;

            // Frío/Calor extremo
            var comfort = pawn.needs.comfort;
            if (comfort != null && comfort.CurLevel < 0.20f)
                return true;

            return false;
        }

        private static float GetTraitsFactor(Pawn pawn, TriggerType trigger)
        {
            if (pawn.story?.traits == null) return 1f;

            float factor = 1f;

            // Traits que REDUCEN ganas de hablar
            if (pawn.story.traits.HasTrait(TraitDefOf.Bloodlust) && trigger == TriggerType.Random)
                factor *= 0.7f;

            if (pawn.story.traits.HasTrait(TraitDefOf.Psychopath))
                factor *= 0.8f; // Menos motivado socialmente

            // Trait personalizado "Shy" si existe
            var shyTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Shy");
            if (shyTrait != null && pawn.story.traits.HasTrait(shyTrait))
                factor *= 0.5f;

            // Traits que AUMENTAN ganas de hablar
            if (pawn.story.traits.HasTrait(TraitDefOf.Kind))
                factor *= 1.3f;

            if (pawn.story.traits.HasTrait(TraitDefOf.Greedy) && trigger == TriggerType.Incident)
                factor *= 1.2f; // Se queja de cosas

            // Nervous = más probable hablar durante incidentes (stress)
            var nervousTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Nervous");
            if (nervousTrait != null && pawn.story.traits.HasTrait(nervousTrait) && trigger == TriggerType.Incident)
                factor *= 1.4f;

            return factor;
        }

        private static float GetSocialSkillFactor(Pawn pawn, TriggerType trigger)
        {
            if (!MyMod.Settings.prioritizeSocialTraits)
                return 1f;

            int socialSkill = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 5;

            // Para mensajes random, el social skill importa mucho
            if (trigger == TriggerType.Random)
            {
                if (socialSkill <= 2) return 0.5f;
                if (socialSkill <= 5) return 0.8f;
                if (socialSkill >= 12) return 1.3f;
            }
            // Para incidentes, importa menos
            else
            {
                if (socialSkill <= 2) return 0.8f;
                if (socialSkill >= 12) return 1.1f;
            }

            return 1f;
        }

        private static float GetRecentChatFactor(Pawn pawn)
        {
            // Si chateó recientemente (últimas 6h), menos probable que hable de nuevo
            var chatLog = ChatGameComponent.Instance?.GetChat(pawn);
            if (chatLog == null || !chatLog.Any())
                return 1f;

            // Buscar último mensaje del jugador en el chat
            var lastPlayerMessage = chatLog.LastOrDefault(line => line.StartsWith("[USER]"));
            if (lastPlayerMessage == null)
                return 1f; // No hay chat previo, OK para hablar

            // TODO: Necesitaríamos timestamp del último chat para calcular tiempo real
            // Por ahora, si hay chat reciente, reducir probabilidad levemente
            if (chatLog.Count > 5) // Si hay bastante historial reciente
                return 0.8f;

            return 1f;
        }

        private static float GetPhysicalStateFactor(Pawn pawn)
        {
            float factor = 1f;

            // Consciencia baja
            var consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
            if (consciousness < 0.5f)
                return 0.2f; // Muy difícil hablar
            if (consciousness < 0.7f)
                factor *= 0.6f;

            // Dolor extremo
            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.6f)
                factor *= 0.4f;
            else if (pain > 0.3f)
                factor *= 0.7f;

            // Cansancio extremo
            var rest = pawn.needs?.rest?.CurLevel ?? 1f;
            if (rest < 0.15f)
                factor *= 0.5f; // Muy cansado para hablar casual

            return factor;
        }

        /// <summary>
        /// Verifica si el colono está en estado mental que impide conversación
        /// </summary>
        public static bool IsInInvalidMentalState(Pawn pawn)
        {
            if (pawn.InMentalState)
            {
                var state = pawn.MentalState?.def;
                if (state == null) return false;

                // Estados que definitivamente impiden conversación
                if (state == MentalStateDefOf.Berserk ||
                    state == MentalStateDefOf.Manhunter ||
                    state == MentalStateDefOf.PanicFlee)
                {
                    return true;
                }

                // Otros estados mentales podrían aún permitir hablar
                // (ej: Sad Wander, Binging, etc. - pueden tener sentido contextual)
            }

            return false;
        }
    }
}