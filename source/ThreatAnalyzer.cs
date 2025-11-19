using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace EchoColony
{
    public static class ThreatAnalyzer
    {
        public static string GetColonyThreatStatusDetailed(Map map)
        {
            // Filtro más estricto para enemigos que representen amenaza REAL Y CONOCIDA
            var hostilePawns = map.attackTargetsCache.TargetsHostileToColony
                .OfType<Pawn>()
                .Where(p => IsActiveThreat(p, map) && IsDiscoveredThreat(p, map))
                .ToList();

            // También verificar estructuras hostiles (torretas, etc.)
            var hostileBuildings = map.attackTargetsCache.TargetsHostileToColony
                .OfType<Building>()
                .Where(b => IsActiveThreatBuilding(b, map))
                .ToList();

            if (hostilePawns.Count == 0 && hostileBuildings.Count == 0)
                return "The colony is currently calm and secure.";

            // 1. MECANOIDES
            var mechanoids = hostilePawns
                .Where(p => p.RaceProps.IsMechanoid)
                .ToList();

            if (mechanoids.Any())
            {
                var types = mechanoids.GroupBy(p => p.kindDef.label).Select(g => $"{g.Count()} {g.Key}").ToList();
                var avgDist = mechanoids.Average(p => IntVec3Utility.DistanceTo(p.Position, map.Center));
                
                // Verificar si hay estructuras mecanoide activas
                var mechBuildings = hostileBuildings.Where(b => IsMechanoidStructure(b)).ToList();
                string structureInfo = mechBuildings.Any() ? $" with {mechBuildings.Count} active structures" : "";
                
                return $"Mechanoid threat detected: {string.Join(", ", types)}{structureInfo}.\nAverage distance: {avgDist:F0} tiles.";
            }

            // 2. RAID
            var raidLord = map.lordManager.lords.FirstOrDefault(l =>
                IsHostileLordJob(l.LordJob) &&
                l.ownedPawns.Any(p => IsActiveThreat(p, map) && IsDiscoveredThreat(p, map))
            );

            if (raidLord != null)
            {
                string faction = raidLord.faction?.Name ?? "an unknown faction";
                var attackers = raidLord.ownedPawns
                    .Where(p => IsActiveThreat(p, map) && IsDiscoveredThreat(p, map))
                    .ToList();

                var grouped = attackers.GroupBy(p => p.kindDef.label).Select(g => $"{g.Count()} {g.Key}").ToList();
                
                // Analizar el estado de la raid más específicamente
                string state = GetRaidState(attackers);

                return $"Raid by {faction}: {string.Join(", ", grouped)}.\n{state}";
            }

            // 3. INSECTOS
            var insects = hostilePawns
                .Where(p => p.RaceProps.Insect)
                .ToList();

            if (insects.Any())
            {
                var types = insects.GroupBy(p => p.kindDef.label).Select(g => $"{g.Count()} {g.Key}").ToList();
                var hives = map.listerThings.AllThings
                    .Where(t => t.def == ThingDefOf.Hive && 
                               t.Faction?.HostileTo(Faction.OfPlayer) == true && 
                               !t.Position.Fogged(map))
                    .Count();
                
                string hiveInfo = hives > 0 ? $" from {hives} active hive(s)" : "";
                return $"Insect swarm detected: {string.Join(", ", types)}{hiveInfo}.";
            }

            // 4. MANHUNTERS
            var manhunters = hostilePawns
                .Where(p => p.RaceProps.Animal && 
                           p.MentalStateDef == MentalStateDefOf.ManhunterPermanent &&
                           IsActivelyHunting(p))
                .ToList();

            if (manhunters.Any())
            {
                var types = manhunters.GroupBy(p => p.LabelCapNoCount).Select(g => $"{g.Count()} {g.Key}").ToList();
                var avgDist = manhunters.Average(p => IntVec3Utility.DistanceTo(p.Position, map.Center));
                return $"Manhunter pack detected: {string.Join(", ", types)}.\nAverage distance to colony center: {avgDist:F0} tiles.";
            }

            // 5. TORRETAS HOSTILES
            var hostileTurrets = hostileBuildings
                .Where(b => b.def.building?.IsTurret == true)
                .ToList();

            if (hostileTurrets.Any())
            {
                var turretTypes = hostileTurrets.GroupBy(t => t.def.label).Select(g => $"{g.Count()} {g.Key}").ToList();
                return $"Hostile turrets detected: {string.Join(", ", turretTypes)}.";
            }

            // 6. INFESTACIÓN
            if (map.listerThings.AllThings.Any(t =>
                t.def == ThingDefOf.Hive &&
                t.Faction?.HostileTo(Faction.OfPlayer) == true &&
                !t.Position.Fogged(map)))
            {
                return "An infestation has emerged below the colony. Insect hives are active underground.";
            }

            // 7. MORTEROS
            if (map.listerThings.AllThings.Any(t =>
                t.def.building?.buildingTags?.Contains("Artillery") == true &&
                t.Faction?.HostileTo(Faction.OfPlayer) == true &&
                !t.Position.Fogged(map)))
            {
                return "Enemy mortars are being deployed nearby. Expect bombardments.";
            }

            // 8. FUEGO
            if (map.listerThings.AllThings.OfType<Fire>().Any(f => !f.Position.Fogged(map)))
            {
                return "There is an active fire in the colony. This could spread quickly if not controlled.";
            }

            return "Hostile activity detected, but source is unclear.";
        }

        // NUEVO MÉTODO: Verificar si una amenaza ha sido descubierta por el jugador
        private static bool IsDiscoveredThreat(Pawn pawn, Map map)
        {
            // Si está en fog of war, definitivamente no ha sido descubierto
            if (pawn.Position.Fogged(map))
                return false;

            // Verificar si está en un Ancient Danger sin activar
            if (IsInUndiscoveredAncientDanger(pawn, map))
                return false;

            // Verificar si está en caskets/cryptosleep sin abrir
            if (IsInUnopenedCasket(pawn))
                return false;

            // Si tiene un lord job específico de ancient danger, probablemente no ha sido activado
            if (HasUndiscoveredAncientDangerLord(pawn))
                return false;

            // Si llegamos aquí, es una amenaza conocida
            return true;
        }

        // Verificar si está en un Ancient Danger sin descubrir
        private static bool IsInUndiscoveredAncientDanger(Pawn pawn, Map map)
        {
            // Buscar edificios de ancient shrine/danger cerca del pawn
            var nearbyAncientStructures = map.listerThings.AllThings
                .Where(t => IsAncientStructure(t) && 
                           IntVec3Utility.DistanceTo(pawn.Position, t.Position) < 15f)
                .ToList();

            if (!nearbyAncientStructures.Any())
                return false;

            // Si hay estructuras antiguas cerca y el pawn no se está moviendo activamente hacia colonos,
            // probablemente aún no ha sido activado
            if (pawn.CurJob?.def == JobDefOf.Wait || 
                pawn.CurJob?.def == JobDefOf.LayDown ||
                pawn.CurJob == null)
                return true;

            return false;
        }

        // Verificar si está en un casket/cripocápsula sin abrir
        private static bool IsInUnopenedCasket(Pawn pawn)
{
    // Verificar si el pawn está en una cripocápsula
    if (pawn.ParentHolder is Building_CryptosleepCasket casket)
    {
        // Si está en la cápsula y la cápsula no ha sido abierta/dañada, no ha sido descubierto
        return casket.HitPoints > casket.MaxHitPoints * 0.95f;
    }

    return false;
}

        // Verificar si es una estructura antigua
        private static bool IsAncientStructure(Thing thing)
        {
            if (thing?.def?.label == null) return false;
            
            string label = thing.def.label.ToLower();
            return label.Contains("ancient") || 
                   label.Contains("shrine") ||
                   label.Contains("ruin") ||
                   thing.def == ThingDefOf.AncientCryptosleepCasket;
        }

        // Verificar si tiene un lord job de ancient danger sin activar
        private static bool HasUndiscoveredAncientDangerLord(Pawn pawn)
        {
            var lord = pawn.GetLord();
            if (lord?.LordJob == null) return false;

            // Algunos lord jobs específicos de ancient dangers
            string lordJobName = lord.LordJob.GetType().Name.ToLower();
            
            // Si tiene un lord job de "sleeping" o "dormant", probablemente no ha sido activado
            if (lordJobName.Contains("sleep") || 
                lordJobName.Contains("dormant") ||
                lordJobName.Contains("ancient"))
            {
                // Verificar si todos los pawns del lord están inmóviles
                var lordPawns = lord.ownedPawns.Where(p => p.Spawned).ToList();
                bool allIdle = lordPawns.All(p => 
                    p.CurJob?.def == JobDefOf.Wait || 
                    p.CurJob?.def == JobDefOf.LayDown || 
                    p.CurJob == null);
                    
                return allIdle;
            }

            return false;
        }

        // Verificar si un edificio representa una amenaza activa
        private static bool IsActiveThreatBuilding(Building building, Map map)
        {
            if (!building.Spawned || 
                building.Position.Fogged(map) || 
                !GenHostility.HostileTo(building, Faction.OfPlayer))
                return false;

            // Verificar que no esté destruido
            if (building.Destroyed || building.HitPoints <= 0)
                return false;

            // Verificar distancia razonable
            float distanceToColony = IntVec3Utility.DistanceTo(building.Position, map.Center);
            if (distanceToColony > 150f) // Edificios pueden amenazar desde más lejos
                return false;

            // Solo considerar edificios que realmente pueden atacar o generar amenazas
            if (building.def.building?.IsTurret == true ||
                building.def == ThingDefOf.Hive ||
                IsMechanoidStructure(building) ||
                building.def.building?.buildingTags?.Contains("Artillery") == true)
                return true;

            return false;
        }

        // Verificar si es una estructura mecanoide
        private static bool IsMechanoidStructure(Building building)
        {
            // Verificar si el edificio pertenece a mecanoides o tiene características mecanoide
            return building.Faction?.def?.techLevel == TechLevel.Spacer ||
                   building.def.label.ToLower().Contains("mech") ||
                   building.def.label.ToLower().Contains("mechanoid") ||
                   building.def.building?.buildingTags?.Any(tag => 
                       tag.ToLower().Contains("mech") || 
                       tag.ToLower().Contains("mechanoid")) == true;
        }

        // Método mejorado para detectar amenazas reales
        private static bool IsActiveThreat(Pawn pawn, Map map)
        {
            if (!pawn.Spawned || 
                pawn.Position.Fogged(map) || 
                !GenHostility.IsActiveThreatToPlayer(pawn) ||
                !GenHostility.HostileTo(pawn, Faction.OfPlayer))
                return false;

            // Verificar si está muerto o incapacitado
            if (pawn.Downed || pawn.Dead)
                return false;

            // MECANOIDES - comportamiento especial
            if (pawn.RaceProps.IsMechanoid)
            {
                // Los mecanoides no duermen como humanos, verificar si están activos
                if (pawn.CurJob?.def == JobDefOf.Wait && !IsNearColonyOrColonists(pawn, map))
                    return false;
                
                // Mecanoides dormidos/apagados
                if (pawn.needs?.rest?.CurLevel < 0.1f) // Muy bajo "descanso" puede indicar inactividad
                    return false;
                    
                return true; // Los mecanoides activos son siempre amenaza
            }

            // INSECTOS - comportamiento especial
            if (pawn.RaceProps.Insect)
            {
                // Los insectos pueden estar dormidos en las colmenas
                if (pawn.CurJob?.def == JobDefOf.LayDown || pawn.InBed())
                    return false;
                
                // Si está muy lejos de colmenas activas, puede no ser amenaza inmediata
                if (!IsNearActiveHive(pawn, map) && !IsNearColonyOrColonists(pawn, map))
                    return false;
                    
                return true;
            }

            // COMPORTAMIENTO ESTÁNDAR PARA HUMANOS/ANIMALES
            // Excluir pawns dormidos
            if (pawn.jobs?.curJob?.def == JobDefOf.LayDown || 
                pawn.CurJob?.def == JobDefOf.LayDown ||
                pawn.InBed())
                return false;

            // Excluir pawns en estados mentales no agresivos
            if (pawn.InMentalState && pawn.MentalStateDef != MentalStateDefOf.ManhunterPermanent)
                return false;

            // Excluir pawns que están huyendo
            if (pawn.mindState?.duty?.def == DutyDefOf.ExitMapBest ||
                pawn.mindState?.duty?.def == DutyDefOf.ExitMapRandom ||
                pawn.mindState?.duty?.def == DutyDefOf.Escort)
                return false;

            // Verificar trabajos activos
            if (pawn.CurJob != null)
            {
                var jobDef = pawn.CurJob.def;
                
                // Jobs que indican amenaza activa
                if (jobDef == JobDefOf.AttackMelee ||
                    jobDef == JobDefOf.AttackStatic ||
                    jobDef == JobDefOf.Hunt ||
                    jobDef == JobDefOf.Wait_Combat ||
                    jobDef == JobDefOf.Goto)
                    return true;

                // Excluir jobs pasivos
                if (jobDef == JobDefOf.Wait ||
                    jobDef == JobDefOf.GotoWander)
                    return IsNearColonyOrColonists(pawn, map); // Solo si están cerca
            }

            // Verificar distancia - muy lejos probablemente no es amenaza inmediata
            float distanceToColony = IntVec3Utility.DistanceTo(pawn.Position, map.Center);
            if (distanceToColony > 100f && !pawn.RaceProps.IsMechanoid) // Mecanoides pueden ser peligrosos desde lejos
                return false;

            return true;
        }

        // Verificar si está cerca de la colonia o colonos
        private static bool IsNearColonyOrColonists(Pawn pawn, Map map)
        {
            // Verificar distancia al centro de la colonia
            if (IntVec3Utility.DistanceTo(pawn.Position, map.Center) < 50f)
                return true;
                
            // Verificar si hay colonos cerca
            var nearbyColonists = map.mapPawns.FreeColonists
                .Where(c => c.Spawned && IntVec3Utility.DistanceTo(pawn.Position, c.Position) < 30f);
                
            return nearbyColonists.Any();
        }

        // Verificar si está cerca de una colmena activa
        private static bool IsNearActiveHive(Pawn insect, Map map)
        {
            var nearbyHives = map.listerThings.AllThings
                .Where(t => t.def == ThingDefOf.Hive && 
                           !t.Position.Fogged(map) &&
                           IntVec3Utility.DistanceTo(insect.Position, t.Position) < 20f);
                           
            return nearbyHives.Any();
        }

        // Verificar si un animal manhunter está cazando activamente
        private static bool IsActivelyHunting(Pawn pawn)
        {
            if (pawn.CurJob?.def == JobDefOf.Hunt || 
                pawn.CurJob?.def == JobDefOf.AttackMelee ||
                pawn.CurJob?.def == JobDefOf.Goto)
                return true;

            // Si está muy lejos y no se mueve hacia la colonia, no es amenaza inmediata
            float distanceToColony = IntVec3Utility.DistanceTo(pawn.Position, pawn.Map.Center);
            return distanceToColony < 50f; // Ajusta según necesidad
        }

        // Analizar el estado específico de una raid
        private static string GetRaidState(List<Pawn> attackers)
        {
            if (!attackers.Any()) return "The raiders have left.";

            int waiting = attackers.Count(p => p.CurJob?.def == JobDefOf.Wait || p.CurJob?.def == JobDefOf.Wait_Combat);
            int attacking = attackers.Count(p => p.CurJob?.def == JobDefOf.AttackMelee || p.CurJob?.def == JobDefOf.AttackStatic);
            int moving = attackers.Count(p => p.CurJob?.def == JobDefOf.Goto);

            if (attacking > 0)
                return "The raiders are actively attacking!";
            else if (moving > waiting)
                return "The raiders are advancing towards the colony.";
            else if (waiting > 0)
                return "The raiders are preparing their assault.";
            else
                return "The raiders' intentions are unclear.";
        }

        public static bool IsHostileLordJob(LordJob job)
        {
            return job is LordJob_AssaultColony ||
                   job is LordJob_AssaultThings ||
                   job is LordJob_BossgroupAssaultColony ||
                   job is LordJob_ChimeraAssault ||
                   job is LordJob_DevourerAssault ||
                   job is LordJob_EntitySwarm ||
                   job is LordJob_ShamblerAssault ||
                   job is LordJob_ShamblerSwarm ||
                   job is LordJob_Siege ||
                   job is LordJob_SightstealerAssault ||
                   job is LordJob_SightstealerSwarm ||
                   job is LordJob_SlaveRebellion ||
                   job is LordJob_SleepThenAssaultColony ||
                   job is LordJob_StageThenAttack;
        }
    }
}