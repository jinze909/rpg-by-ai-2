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
        // LOS alone is not a walkable route: a slit in a wall, tidewater
        // or a sealed gate can let an effect be visible while a summoned
        // melee enemy can NEVER reach the current fight. Use the SAME
        // four-neighbor footprint graph as real enemy pursuit, bounded to
        // the actual local summoning arena. Shared generation-stamped path
        // workspace avoids allocations on repeated Boss phases.
        private bool BossSummonHasLocalRoute(Point from, Point target)
        { return ActorHasLocalRoute(from,target,5); }
        // Keep the established two-argument boss interface (used by
        // existing Unity reflection checks) while allowing public ferry
        // encounters to check a wider pier path without partial routing.
        private bool ActorHasLocalRoute(Point from,Point target,int radius)
        {
            if(!CanStand(from)||!CanStand(target))return false;
            int width=Map.rows[0].Length,height=Map.rows.Length;
            int sx=(int)Math.Floor(from.x),sy=(int)Math.Floor(from.y);
            int tx=(int)Math.Floor(target.x),ty=(int)Math.Floor(target.y);
            if(sx<0||sy<0||tx<0||ty<0||
                sx>=width||tx>=width||sy>=height||ty>=height||
                Math.Abs(tx-sx)>radius||Math.Abs(ty-sy)>radius)return false;
            int start=sy*width+sx,goal=ty*width+tx;
            PreparePathSearch(width*height);
            int head=0,tail=0;
            pathQueue[tail++]=start;
            VisitPath(start,start);
            while(head<tail)
            {
                int node=pathQueue[head++],x=node%width,y=node/width;
                if(node==goal)return true;
                for(int i=0;i<4;i++)
                {
                    int nx=x+pursuitSteps[i,0],ny=y+pursuitSteps[i,1];
                    if(nx<0||ny<0||nx>=width||ny>=height||
                        Math.Abs(nx-sx)>radius||Math.Abs(ny-sy)>radius)continue;
                    int next=ny*width+nx;
                    if(PathVisited(next)||!Fits(new Point(nx+.5f,ny+.5f)))continue;
                    VisitPath(next,node);pathQueue[tail++]=next;
                }
            }
            return false;
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
            // The summon must reinforce a fight that physically connects
            // to the hero, not fill an isolated pocket on the other side
            // of a closed floodgate or a wall. The older local boss-to-spawn
            // check alone could greenlight an unreachable entire arena.
            // A FULL physical finish is required; a closest-reachable BFS
            // frontier does not prove an enemy can ever engage the hero.
            if(!CanStand(Position)||!CanStand(boss.Position)||
                (boss.Position-Position).Length>1.8f&&
                NavigationPathFrom(boss.Position,Position,true).Count==0)
                return false;
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
                    (save.companion>=0 &&
                        (foot-companionPosition).Length<1.2f) ||
                    !ClearAttackPath(boss.Position,foot) ||
                    !BossSummonHasLocalRoute(boss.Position,foot))continue;
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
                // The escape to shore is a safety teleport, not traveled
                // footsteps. Restart the next physical walk from its first
                // coherent planted sprite pose, without adding/replaying
                // footstep events already counted earlier in the journey.
                walkDistance = 0f;
                // An involuntary shoreline relocation is not a player
                // dodge. A dash started on the outgoing shoal must not
                // claim a counter from a later warning after its actual
                // traveled movement was canceled by the tide.
                perfectDodgeArmed = false;
                dodgeEscapeTime = 0f;
                dashOrigin = Position;
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
                enemy.targetingWayfarer = enemy.targetingReliefCivilian =
                    enemy.targetingAfterlightStock = enemy.targetingOuterPatrolStock = false;
                enemy.timer = Math.Max(.35f, enemy.timer);
            }
            if (save.companion >= 0 && !Fits(companionPosition))
            {
                // Tide-forced evacuation cancels a planted ally's
                // prepared action. If the player never received its
                // physical contact, do not charge the complete manual
                // command cooldown for a spell the world canceled.
                // A volley/heal/frost that ALREADY released remains paid.
                bool unspentCommand = companionAction != null &&
                    companionAction.kind == "command" &&
                    !companionAction.released;
                bool unspentAuto = companionAction != null &&
                    companionAction.kind != "command" &&
                    !companionAction.released;
                companionPosition = SafeActorFoot(companionPosition);
                companionAction = null; companionAutoTarget = null;
                companionMoving = false; companionMoveTime = 0f;
                if (unspentCommand) companionCooldown = 0f;
                if (unspentAuto)
                    companionAttack = Math.Min(companionAttack, .22f);
            }
            // The real evacuee is a world actor too. Without this, the
            // outgoing tide can trap her in newly flooded shoals while the
            // hero and enemies recover to the nearest walkable bank.
            if (ReliefEscortActive && !Fits(reliefFollowerPosition))
            {
                reliefFollowerPosition = SafeActorFoot(reliefFollowerPosition);
                // This shoreline relocation is not a walked animation
                // frame. Drop old gait distance and movement state while
                // preserving the civilian's tactical WAIT/FOLLOW choice,
                // safety, mission time and saved rescue checkpoint format.
                reliefFollowerMoving = false;
                reliefFollowerWalkDistance = 0f;
            }
        }
    }
}
