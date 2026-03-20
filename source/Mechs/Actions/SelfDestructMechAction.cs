using Verse;
using RimWorld;

namespace EchoColony.Mechs.Actions
{
    public class SelfDestructMechAction : BaseMechAction
    {
        public override string ActionName => "SELF_DESTRUCT";
        public override string Description => "Self-destruct (permanent)";
        public override int CooldownTicks => 0; // No cooldown - it's permanent

        public override bool CanExecute(Pawn mech)
        {
            return base.CanExecute(mech); // Always can execute if alive
        }

        public override bool Execute(Pawn mech)
        {
            try
            {
                // Get intelligence level for different reactions
                var intelligence = MechIntelligenceDetector.GetIntelligenceLevel(mech);

                // Create explosion at mech location
                GenExplosion.DoExplosion(
                    center: mech.Position,
                    map: mech.Map,
                    radius: 3.9f,
                    damType: DamageDefOf.Bomb,
                    instigator: mech,
                    damAmount: 50,
                    armorPenetration: 0.5f,
                    explosionSound: null,
                    weapon: null,
                    projectile: null,
                    intendedTarget: null,
                    postExplosionSpawnThingDef: null,
                    postExplosionSpawnChance: 0f,
                    postExplosionSpawnThingCount: 1,
                    applyDamageToExplosionCellsNeighbors: true,
                    preExplosionSpawnThingDef: null,
                    preExplosionSpawnChance: 0f,
                    preExplosionSpawnThingCount: 1,
                    chanceToStartFire: 0.5f,
                    damageFalloff: true
                );

                // Kill the mech
                mech.Kill(null);

                LogAction(mech, "Self-destructed");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error self-destructing {mech.LabelShort}: {ex.Message}");
                return false;
            }
        }
    }
}