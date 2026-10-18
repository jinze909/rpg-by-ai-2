using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Use the same complete foot clearance as movement, including active gates and tides.
        // Preserve valid authored/saved positions; only repair positions that actually overlap.
        private Point SafeActorFoot(Point proposed)
        {
            if (!Finite(proposed.x) || !Finite(proposed.y) || proposed.x < 0 || proposed.y < 0 || proposed.x >= Map.rows[0].Length || proposed.y >= Map.rows.Length)
                proposed = new Point(Map.spawnX, Map.spawnY);
            if (Fits(proposed)) return proposed;
            Point result = new Point(); float nearest = float.MaxValue;
            for (int y = 2; y < Map.rows.Length - 2; y++)
            for (int x = 2; x < Map.rows[y].Length - 2; x++)
            {
                Point candidate = new Point(x + .5f, y + .5f), delta = candidate - proposed;
                float squared = delta.x * delta.x + delta.y * delta.y;
                if (squared >= nearest || !Fits(candidate)) continue;
                result = candidate; nearest = squared;
            }
            if (nearest == float.MaxValue) throw new InvalidOperationException("The region has no valid actor footing.");
            return result;
        }
        private Enemy CreatePlacedEnemy(int id, int kind, Point proposed, float health, bool boss = false)
        {
            Point p = SafeActorFoot(proposed);
            return new Enemy(id, kind, p.x, p.y, health, boss);
        }
        // A living Boss's reinforcements MUST emerge in the current local
        // arena. CreatePlacedEnemy is deliberately broader (recovery of valid
        // old saves, authored actors) and can relocate an invalid summon to
        // another side of the map; that is not valid gameplay telegraphing.
        // Try the legacy near-left tile first, then nearby reachable ring tiles.
        private bool TrySpawnBossReinforcement(Enemy boss)
        {
            if (boss==null || !boss.boss || boss.hp<=0 || !enemies.Contains(boss)
                || enemies.Count>=9)return false;
            Point[] candidates = {
                new Point(-2f,1f), new Point(2f,1f), new Point(-2f,-1f),
                new Point(2f,-1f), new Point(0f,2.5f), new Point(0f,-2.5f),
                new Point(-3f,0f), new Point(3f,0f),
                new Point(-3f,2f), new Point(3f,2f),
                new Point(-3f,-2f), new Point(3f,-2f),
                new Point(-1f,3f), new Point(1f,3f),
                new Point(-1f,-3f), new Point(1f,-3f)
            };
            foreach(Point offset in candidates)
            {
                Point foot=boss.Position+offset;
                if (!CanStand(foot) || (foot-Position).Length<1.25f ||
                    (foot-boss.Position).Length<1.2f ||
                    !ClearAttackPath(boss.Position,foot))continue;
                bool occupied=false;
                foreach(Enemy existing in enemies)
                    if((foot-existing.Position).Length<1.15f)
                    {occupied=true;break;}
                if(occupied)continue;
                // The first authorized third-cycle summon remains the old
                // melee troop. In later cycles, alternating *real* bow units
                // creates flank pressure instead of endlessly duplicating
                // identical close-range mobs. Citadel keeps its ranged guard.
                int recruitKind=save.room==4?1:
                    (boss.cycle>=6&&(boss.cycle/3)%2==0?1:0);
                Enemy recruit=new Enemy(++enemyId,recruitKind,
                    foot.x,foot.y,38+save.room*3);
                recruit.Face(Position-foot);
                // Give a genuine audible and visual entrance: summoning a
                // standing combatant inside a swing should not hit instantly.
                recruit.stun=.35f;
                enemies.Add(recruit);
                effects.Add(new Effect("burst",foot,.35f)
                    {radius=.65f,variant=0});
                sound="spell";revision++;
                return true;
            }
            // No legal nearby arena foot: skip only this summon, not the
            // Boss attack, normal fight or any existing opponent.
            return false;
        }
        private void TickTideTransition()
        {
            if (save.room != 5 || previousLowTide == LowTide) return;
            previousLowTide = LowTide;
            if (!Fits(Position))
            {
                Point p = SafeActorFoot(Position); save.x = p.x; save.y = p.y;
                CancelAction(); dashTime = 0; playerRecoil = new Point();
                Notify("潮水涨起，你退回了最近的岸边。闸门可固定低潮。", "The tide rises; you retreat to the nearest bank. The sluice can hold low tide.");
            }
            RecoverTideActors();
        }
        private void RecoverTideActors()
        {
            foreach (Enemy enemy in enemies) if (!Fits(enemy.Position))
            {
                // The water canceled the attack itself. Retract its
                // attacker-identity warning at the same instant: otherwise
                // the old danger circle remains visible after the forced
                // retreat despite no strike ever being released.
                RemoveEnemyPendingWarning(enemy);
                Point p = SafeActorFoot(enemy.Position); enemy.x = p.x; enemy.y = p.y;
                enemy.moving = enemy.backpedaling = false; enemy.state = 0; enemy.releaseTime = 0;
                enemy.targetingWayfarer = enemy.targetingReliefCivilian = false;
                enemy.timer = Math.Max(.35f, enemy.timer);
            }
            if (save.companion >= 0 && !Fits(companionPosition))
            {
                companionPosition = SafeActorFoot(companionPosition);
                companionAction = null; companionMoving = false;
            }
            // The real evacuee is a world actor too. Without this, the
            // outgoing tide can trap her in newly flooded shoals while the
            // hero and enemies recover to the nearest walkable bank.
            if (ReliefEscortActive && !Fits(reliefFollowerPosition))
                reliefFollowerPosition = SafeActorFoot(reliefFollowerPosition);
        }
    }
}
