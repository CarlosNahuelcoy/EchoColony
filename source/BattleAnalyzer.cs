using RimWorld;
using System.Linq;
using Verse;
using Verse.AI.Group;
using System.Text;

namespace EchoColony
{
    public static class BattleAnalyzer
    {
        public static string GetBattleStatus(Map map)
        {
            // Solo buscar enemigos que estén EN COMBATE REAL AHORA MISMO
            var hostilePawns = map.mapPawns.AllPawnsSpawned
                .Where(p => 
                    p.Faction != null && 
                    p.Faction.HostileTo(Faction.OfPlayer) &&
                    !p.Dead &&
                    IsInActiveBattle(p, map))
                .ToList();

            // Si no hay enemigos en combate activo, no hay batalla
            if (!hostilePawns.Any())
                return "There is no battle currently happening.";

            int totalEnemies = hostilePawns.Count;
            int downedEnemies = hostilePawns.Count(p => p.Downed);
            int activeEnemies = totalEnemies - downedEnemies;

            var ourColonists = map.mapPawns.FreeColonists.ToList();

            int ourDowned = ourColonists.Count(p =>
                p.Downed &&
                !IsHealthyBaby(p) &&
                HasRecentCombatInjury(p)
            );

            int ourFighters = ourColonists.Count(p => !p.Downed && p.Drafted);

            // Verificar si hay un Lord de asalto activo con enemigos visibles y en combate
            var hostileLord = map.lordManager.lords.FirstOrDefault(l => 
                (l.LordJob is LordJob_AssaultColony || l.LordJob is LordJob_AssaultThings) &&
                l.ownedPawns.Any(p => hostilePawns.Contains(p))
            );
            
            bool enemyRetreating = hostileLord?.CurLordToil?.GetType().Name.ToLower().Contains("flee") == true;

            if (enemyRetreating)
                return "The enemy is retreating. The worst seems to be over.";

            StringBuilder result = new StringBuilder("Battle ongoing.");

            if (downedEnemies > 0)
                result.Append($" {downedEnemies} enemies are downed.");

            if (activeEnemies > 0)
                result.Append($" {activeEnemies} enemies remain fighting.");

            if (ourDowned > 0)
                result.Append($" {ourDowned} colonist(s) are downed.");

            if (ourFighters > 0 && activeEnemies > 0)
            {
                if (ourFighters > activeEnemies * 1.5)
                    result.Append(" We're clearly winning the fight.");
                else if (activeEnemies > ourFighters * 1.5)
                    result.Append(" We're heavily outnumbered!");
                else
                    result.Append(" The fight is fairly balanced.");
            }

            // Tipos de enemigos (solo los que están peleando activamente)
            var enemyTypes = hostilePawns
                .Where(p => !p.Downed)
                .GroupBy(p => p.kindDef.label)
                .Select(g => $"{g.Count()} {g.Key}")
                .ToList();

            if (enemyTypes.Any())
                result.Append(" Enemy types: " + string.Join(", ", enemyTypes) + ".");

            // Solo mostrar armas de enemigos activos en combate
            var weapons = hostilePawns
                .Where(p => !p.Downed)
                .Select(p => p.equipment?.Primary?.LabelCap)
                .Where(label => !string.IsNullOrEmpty(label))
                .GroupBy(w => w)
                .Select(g => $"{g.Count()}x {g.Key}")
                .ToList();

            if (weapons.Any())
                result.Append(" They are equipped with: " + string.Join(", ", weapons) + ".");

            return result.ToString();
        }

        private static bool IsInActiveBattle(Pawn pawn, Map map)
        {
            // Los caídos en batalla sí cuentan para el recuento
            if (pawn.Downed)
                return true;
            
            // CRITERIO 1: Estar ejecutando un job de combate AHORA MISMO
            if (pawn.CurJob != null)
            {
                var jobName = pawn.CurJob.def.defName;
                if (jobName == "AttackMelee" || 
                    jobName == "AttackStatic" ||
                    jobName == "Wait_Combat" ||
                    jobName == "FleeAndCower" ||
                    jobName.Contains("Attack"))
                {
                    return true;
                }
            }

            // CRITERIO 2: Tener un objetivo de combate actual y estar cerca
            if (pawn.mindState?.enemyTarget != null && pawn.mindState.enemyTarget.Spawned)
            {
                // Solo si el objetivo está cerca (combate activo)
                if (pawn.Position.DistanceTo(pawn.mindState.enemyTarget.Position) < 40f)
                    return true;
            }

            // CRITERIO 3: Estar siendo atacado por un colonista AHORA
            foreach (var colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (!colonist.Downed && colonist.Drafted)
                {
                    // El colonista lo está atacando activamente
                    if (colonist.CurJob?.targetA.Thing == pawn)
                        return true;
                        
                    // El colonista lo tiene como objetivo y está cerca
                    if (colonist.mindState?.enemyTarget == pawn && 
                        colonist.Position.DistanceTo(pawn.Position) < 40f)
                        return true;
                }
            }

            // CRITERIO 4: Intercambio de disparos MUY reciente (últimos 3 segundos)
            int currentTick = Find.TickManager.TicksGame;
            const int combatVeryRecentThreshold = 180; // 3 segundos
            
            if (pawn.mindState?.lastAttackTargetTick != null && 
                currentTick - pawn.mindState.lastAttackTargetTick < combatVeryRecentThreshold)
                return true;

            // CRITERIO 5: Heridas de combate MUY recientes (últimos 2 segundos)
            if (HasActiveCombatInjury(pawn))
                return true;

            // CRITERIO 6: Verificar si es parte de un raid ACTIVO (no dormido)
            // Solo si el pawn está despierto y moviéndose
            if (!IsSleepingOrInactive(pawn))
            {
                var lord = pawn.GetLord();
                if (lord != null && 
                    (lord.LordJob is LordJob_AssaultColony || lord.LordJob is LordJob_AssaultThings))
                {
                    // Solo si el Lord es reciente (formado en la última hora)
                    int lordAge = Find.TickManager.TicksGame - lord.ticksInToil;
                    if (lordAge < 2500) // Menos de 1 hora
                        return true;
                }
            }

            return false;
        }

        private static bool IsSleepingOrInactive(Pawn pawn)
        {
            // Verificar si está dormido o inactivo
            if (pawn.CurJob?.def == JobDefOf.LayDown || 
                pawn.CurJob?.def.defName == "LayDownAwake" ||
                pawn.CurJob?.def.defName == "SleepMechanoid" ||
                pawn.CurJob?.def.defName == "Deactivated")
                return true;

            // Verificar dormancia de mechanoids
            if (pawn.RaceProps?.IsMechanoid == true)
            {
                float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
                if (consciousness <= 0.01f)
                    return true;
            }

            // Verificar si está en un estado mental de vagabundeo o similar
            if (pawn.MentalState != null && 
                (pawn.MentalState.def == MentalStateDefOf.Wander_Sad ||
                 pawn.MentalState.def == MentalStateDefOf.Wander_Psychotic))
                return true;

            return false;
        }

        private static bool HasActiveCombatInjury(Pawn p)
        {
            if (p.health?.hediffSet == null) return false;

            int now = Find.TickManager.TicksGame;
            const int combatInjuryThreshold = 120; // 2 segundos

            // Solo heridas MUY recientes y significativas de combate
            return p.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Any(h => 
                    now - h.ageTicks < combatInjuryThreshold && 
                    h.Severity > 2f && // Heridas significativas
                    IsCombatRelatedInjury(h)
                );
        }

        private static bool IsCombatRelatedInjury(Hediff_Injury injury)
        {
            var defName = injury.def.defName.ToLower();
            return defName.Contains("gunshot") || 
                   defName.Contains("cut") || 
                   defName.Contains("stab") ||
                   defName.Contains("bite") ||
                   defName.Contains("scratch") ||
                   defName.Contains("blunt") ||
                   defName.Contains("burn") ||
                   defName.Contains("crush");
        }

        private static bool HasRecentCombatInjury(Pawn p)
        {
            if (p.health?.hediffSet == null) return false;

            int now = Find.TickManager.TicksGame;
            const int recentThreshold = 2500; // ~1 hora para colonistas caídos

            return p.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Any(h => 
                    now - h.ageTicks < recentThreshold && 
                    IsCombatRelatedInjury(h)
                );
        }

        private static bool IsHealthyBaby(Pawn p)
        {
            return p.ageTracker.CurLifeStage?.developmentalStage == DevelopmentalStage.Baby &&
                   p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) &&
                   !p.health.InPainShock;
        }

        // Método helper obsoleto - mantenido por compatibilidad pero ya no usado
        private static bool HasRecentInjury(Pawn p)
        {
            return HasRecentCombatInjury(p);
        }
    }
}