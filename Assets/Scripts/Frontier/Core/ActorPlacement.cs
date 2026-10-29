using System;
using System.Collections.Generic;
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
        // Several authored spawn points can collapse to the same safe
        // footing after a tide/bridge change, and interrupted Abyss saves
        // may contain two living enemies on one cell. Keep every valid
        // non-overlapping original foot unchanged. Only separate genuinely
        // overlapping actors, into a nearby tile in the SAME reachable
        // pocket; never move one through a wall to fill a different island.
        // Recovering a wide guardian or placing a recruit beside one must
        // respect BOTH bodies. Keep the original .95 ordinary/ordinary
        // authored spacing; do not shrink or reroute existing safe spawns.
        private static float SpawnEnemyClearance(Enemy existing,bool wideNewBody)
        {
            return existing.boss ? (wideNewBody?1.8f:1.4f) :
                (wideNewBody?1.4f:.95f);
        }
        private Point UncrowdedEnemyFoot(Point proposed,
            Enemy relocatingEnemy=null,bool relocatingCompanion=false,
            bool placingBoss=false)
        {
            Point safe=SafeActorFoot(proposed);
            bool wide=placingBoss||
                (relocatingEnemy!=null&&relocatingEnemy.boss);
            // A newly spawned foe must not materialize INSIDE the hero's
            // or an active companion's hurtbox after a save restoration.
            // This applies only to overlapping feet, not normal nearby
            // enemies; legitimate close-range battles stay unchanged.
            bool occupied=(safe-Position).Length<.95f||
                save.companion>=0&&!relocatingCompanion&&
                (safe-companionPosition).Length<.95f||
                ReliefEscortActive&&reliefMissionRoom==save.room&&
                (safe-reliefFollowerPosition).Length<.95f;
            foreach(Enemy other in enemies)
                if(other!=null&&other!=relocatingEnemy&&other.hp>0&&
                    (other.Position-safe).Length<
                        SpawnEnemyClearance(other,wide))
                {occupied=true;break;}
            if(!occupied)return safe;
            for(int radius=1;radius<=4;radius++)
            for(int dy=-radius;dy<=radius;dy++)
            for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point option=new Point(
                    (float)Math.Floor(safe.x)+dx+.5f,
                    (float)Math.Floor(safe.y)+dy+.5f);
                if(!Fits(option)||!ActorHasLocalRoute(safe,option,4)||
                    (option-Position).Length<1.05f||
                    save.companion>=0&&!relocatingCompanion&&
                    (option-companionPosition).Length<1.05f||
                    ReliefEscortActive&&reliefMissionRoom==save.room&&
                    (option-reliefFollowerPosition).Length<1.05f)
                    continue;
                bool clear=true;
                foreach(Enemy other in enemies)
                    if(other!=null&&other!=relocatingEnemy&&other.hp>0&&
                       (other.Position-option).Length<
                           SpawnEnemyClearance(other,wide))
                    {clear=false;break;}
                if(clear)return option;
            }
            // The first four rings are normally enough for combat groups,
            // but a narrow connected corridor can be occupied for five or
            // more cells during a legacy load or repeated evacuation. Extend
            // ONLY the crowded-case search using the same stamped 4-way
            // collision graph as enemy movement, bounded to twelve physical
            // walking steps. Never jump through a wall or search the entire
            // 120x80 world looking for a distant spawn.
            int w=Map.rows[0].Length,h=Map.rows.Length;
            int sx=(int)Math.Floor(safe.x),sy=(int)Math.Floor(safe.y);
            if(sx>=0&&sx<w&&sy>=0&&sy<h)
            {
                PreparePathSearch(w*h);
                int begin=sy*w+sx,head=0,tail=0;
                pathQueue[tail++]=begin;
                VisitPath(begin,0); // parent buffer stores BFS depth here.
                while(head<tail)
                {
                    int node=pathQueue[head++],x=node%w,y=node/w;
                    int depth=pathParents[node];
                    if(depth>=5)
                    {
                        Point p=new Point(x+.5f,y+.5f);
                        bool free=Fits(p)&&
                            (p-Position).Length>=1.05f&&
                            (save.companion<0||relocatingCompanion||
                                (p-companionPosition).Length>=1.05f)&&
                            (!ReliefEscortActive||reliefMissionRoom!=save.room||
                                (p-reliefFollowerPosition).Length>=1.05f);
                        if(free)
                            foreach(Enemy foe in enemies)
                                if(foe!=null&&foe!=relocatingEnemy&&
                                    foe.hp>0&&(foe.Position-p).Length<
                                        SpawnEnemyClearance(foe,wide))
                                {free=false;break;}
                        if(free)return p;
                    }
                    if(depth>=12)continue;
                    for(int i=0;i<4;i++)
                    {
                        int nx=x+pursuitSteps[i,0],ny=y+pursuitSteps[i,1];
                        if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int next=ny*w+nx;
                        if(PathVisited(next)||
                            !Fits(new Point(nx+.5f,ny+.5f)))continue;
                        VisitPath(next,depth+1);
                        pathQueue[tail++]=next;
                    }
                }
            }
            // An entirely packed/sealed refuge must not silently delete a
            // saved monster or transport it through a closed flood gate.
            return safe;
        }
        // The opening battle is still a compact story encounter, not a
        // mandatory hunt across the full 120x80 region. Each region uses its
        // own authored sightlines, flanks and road shoulders. All recruits
        // remain on the same real, currently walkable component as the hero.
        // Anchors are deliberately not saved: SaveData v1/v2 keeps its exact
        // historical combat serialization and restored enemies take priority.
        private static readonly int[,,] regionalBattleAnchors = {
            { {11,10},{14,17},{21,11},{25,18},{28,7},{30,16},{15,7},{26,20} },
            { {11,12},{15,16},{22,12},{26,10},{28,16},{30,20},{15,20},{24,18} },
            { {11,10},{15,17},{23,12},{28,8},{26,18},{10,20},{16,7},{30,19} },
            { {12,9},{15,17},{23,9},{29,15},{27,19},{12,20},{19,7},{30,6} },
            { {11,11},{16,15},{23,11},{28,10},{19,20},{30,15},{14,6},{28,20} },
            { {12,11},{15,16},{25,8},{29,12},{25,20},{13,20},{22,6},{30,19} },
            { {11,12},{14,16},{24,11},{30,10},{28,20},{11,20},{26,7},{30,16} }
        };
        private bool[] RegionalBattleReachableTiles()
        {
            int width=Map.rows[0].Length,height=Map.rows.Length;
            var reachable=new bool[width*height];
            // Use the identical 4-neighbor, full-foot Fits terrain test
            // used by real movement; no line-of-sight or tile-glyph shortcut.
            if(!Fits(Position))return reachable;
            var queue=new int[width*height];
            int start=(int)Math.Floor(Position.y)*width+
                (int)Math.Floor(Position.x),head=0,tail=0;
            reachable[start]=true;queue[tail++]=start;
            while(head<tail)
            {
                int node=queue[head++],x=node%width,y=node/width;
                for(int d=0;d<4;d++)
                {
                    int xx=x+pursuitSteps[d,0],yy=y+pursuitSteps[d,1];
                    if(xx<0||xx>=width||yy<0||yy>=height)continue;
                    int next=yy*width+xx;
                    if(reachable[next]||
                        !Fits(new Point(xx+.5f,yy+.5f)))continue;
                    reachable[next]=true;queue[tail++]=next;
                }
            }
            return reachable;
        }
        private bool RegionalBattleFootFree(Point foot,bool[] connected)
        {
            int x=(int)Math.Floor(foot.x),y=(int)Math.Floor(foot.y);
            int width=Map.rows[0].Length;
            if(x<4||x>33||y<3||y>21||
                x>=width||y>=Map.rows.Length||
                !connected[y*width+x]||!Fits(foot)||
                (foot-Position).Length<4.5f)return false;
            // Leave conversations, resources, exits and quest machinery
            // readable rather than turning their interaction pads into
            // unavoidable body-blocking piles.
            foreach(WorldObject obj in Map.objects)
                if(obj!=null&&(foot-new Point(obj.x,obj.y)).Length<2f)
                    return false;
            foreach(Enemy other in enemies)
                if(other!=null&&other.hp>0&&
                    (foot-other.Position).Length<2.5f)return false;
            return true;
        }
        private Point RegionalBattleFoot(int rank,bool[] connected)
        {
            int room=save.room;
            int ax=regionalBattleAnchors[room-1,rank,0],
                ay=regionalBattleAnchors[room-1,rank,1];
            for(int radius=0;radius<=6;radius++)
            for(int dy=-radius;dy<=radius;dy++)
            for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point p=new Point(ax+dx+.5f,ay+dy+.5f);
                if(RegionalBattleFootFree(p,connected))return p;
            }
            // A future modded map can remove an entire opening lane. Keep
            // the legacy repair behavior rather than dropping mandatory
            // enemy count or mutating the campaign/save format.
            int[,] original={
                {11,12},{15,17},{21,15},{24,6},
                {9,7},{30,16},{23,19},{14,9}
            };
            return new Point(original[rank,0],original[rank,1]);
        }
        private void SpawnRegionalBaselineEnemies(int count,float health)
        {
            bool[] connected=RegionalBattleReachableTiles();
            for(int i=0;i<count;i++)
                enemies.Add(CreatePlacedEnemy(++enemyId,i%3,
                    RegionalBattleFoot(i,connected),health));
        }

        // Only the baseline authored overworld roster sleeps when the
        // player is in a DIFFERENT local encounter district. Special
        // escorts, patrols, Bosses, alert sound searches and event raiders
        // are never tagged. Exact actor identity is session-only.
        private readonly HashSet<Enemy> regionalAmbientEnemies=
            new HashSet<Enemy>();
        private bool RegionalAmbientEncounterDormant(Enemy e)
        {
            return e!=null&&!e.boss&&regionalAmbientEnemies.Contains(e)&&
                save.room>=1&&save.room<=7&&
                (e.Position-Position).Length>24f&&
                e.state==0&&e.hitTime<=0f&&e.stun<=0f&&
                e.releaseTime<=0f&&
                !IsEnemyInvestigatingImpact(e)&&
                !IsEnemyReturningFromImpact(e);
        }
        // Distant wilderness patrols are OPTIONAL local challenges invoked
        // only by the actual field-survey transaction. They must not move
        // the old campaign's mandatory room roster out of reach, nor turn
        // a distant core-Boss gate into a whole-map hunting chore.
        private int SpawnRegionalScoutEncounter(WorldObject site)
        {
            if(site==null||site.kind!="biomeSurvey"||save.room<1||
               save.room>7||!SourceBelongsToActiveMap(site)||
               !HasPhysicalInteractionReach(site))return 0;
            int room=save.room;
            int count=(room==2||room==3||room==6)?3:2;
            int[,] directions={
                {1,0},{0,1},{-1,0},{0,-1},
                {1,1},{-1,1},{-1,-1},{1,-1}
            };
            int spawned=0;
            Point source=new Point(site.x,site.y);
            for(int i=0;i<count;i++)
            {
                bool found=false;
                for(int radius=7;radius<=12&&!found;radius++)
                for(int shift=0;shift<8&&!found;shift++)
                {
                    int direction=(room*2+i*3+shift)%8;
                    float dx=directions[direction,0],dy=directions[direction,1];
                    Point p=new Point((float)Math.Floor(source.x)+.5f+dx*radius,
                        (float)Math.Floor(source.y)+.5f+dy*radius);
                    if(!CanStand(p)||!ClearAttackPath(source,p)||
                       (p-Position).Length<5f)continue;
                    bool obstructed=false;
                    foreach(WorldObject o in Map.objects)
                        if((new Point(o.x,o.y)-p).Length<4.5f)
                        {obstructed=true;break;}
                    foreach(Enemy e in enemies)
                        if(e!=null&&e.hp>0&&(e.Position-p).Length<5f)
                        {obstructed=true;break;}
                    if(obstructed)continue;
                    Enemy patrol=CreatePlacedEnemy(++enemyId,
                        (room+i*2)%3,p,30+room*10);
                    enemies.Add(patrol);
                    // Only optional physically sourced side patrols can
                    // sleep outside their local combat district.
                    regionalAmbientEnemies.Add(patrol);
                    spawned++;found=true;
                }
            }
            if(spawned>0)
            {
                revision++;sound="alert";
                Notify("远端地标的动静惊动了本地巡逻敌人。它们只守这片区域，不会横穿整张地图追击。",
                    "Your fieldwork draws a local patrol. These raiders hold this district rather than chasing you across the whole region.");
            }
            return spawned;
        }
        private Enemy CreatePlacedEnemy(int id, int kind, Point proposed, float health, bool boss = false)
        {
            Point p = UncrowdedEnemyFoot(proposed,null,false,boss);
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
            if(!CanStand(Position)||!CanStand(boss.Position))
                return false;
            // A nearby fighter is NOT proof of an open arena: two feet
            // 1.5 tiles apart can stand on opposite sides of a sealed wall.
            // Zero path steps are legitimate only for a genuine nearby
            // connected footprint, never for a thin-wall summon exploit.
            bool heroAccessible=(boss.Position-Position).Length<=1.8f?
                ActorHasLocalRoute(boss.Position,Position,5):
                NavigationPathFrom(boss.Position,Position,true).Count>0;
            if(!heroAccessible)return false;
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
                    (foot-boss.Position).Length<1.4f ||
                    (save.companion>=0 &&
                        (foot-companionPosition).Length<1.2f) ||
                    (ReliefEscortActive&&reliefMissionRoom==save.room&&
                        (foot-reliefFollowerPosition).Length<1.25f) ||
                    !ClearAttackPath(boss.Position,foot) ||
                    !BossSummonHasLocalRoute(boss.Position,foot))continue;
                bool occupied=false;
                foreach(Enemy existing in enemies)
                    if((foot-existing.Position).Length<
                        (existing.boss?1.4f:1.15f))
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
                // The water may strand several living enemies on the same
                // former shoal; evacuate each to a distinct connected dry
                // foot without counting its own invalid old position.
                Point p = UncrowdedEnemyFoot(enemy.Position,enemy);
                enemy.x = p.x; enemy.y = p.y;
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
                // Evacuate last, after enemies have claimed real dry feet.
                // Ignore only the ally's OWN submerged former position;
                // still avoid the hero and all live combatants.
                companionPosition = UncrowdedEnemyFoot(
                    companionPosition,null,true);
                companionAction = null; companionAutoTarget = null;
                companionMoving = false; companionMoveTime = 0f;
                // Forced surf relocation is not a step along the previous
                // path: stale queue/corner goals can otherwise walk the ally
                // straight back into the shoal on the following tick.
                // Preserve actual hold orders and the party identity.
                ClearCompanionFollowRoute();
                ClearCompanionCornerPlan();
                companionUnreachableGoal=false;
                companionBlockedRouteRetry=0f;
                if (unspentCommand) companionCooldown = 0f;
                if (unspentAuto)
                    companionAttack = Math.Min(companionAttack, .22f);
            }
            // The real evacuee is a world actor too. Without this, the
            // outgoing tide can trap her in newly flooded shoals while the
            // hero and enemies recover to the nearest walkable bank.
            if (ReliefEscortActive && !Fits(reliefFollowerPosition))
            {
                // Civilians also occupy real collision space. An evacuation
                // into the same dry-bank tile as a living enemy or an ally
                // would strand the resident inside an active combat body.
                // Reuse the physically connected, uncrowded relocation;
                // this is NOT walking, a reward or a teleport to the hub.
                reliefFollowerPosition =
                    UncrowdedEnemyFoot(reliefFollowerPosition);
                // This shoreline relocation is not a walked animation
                // frame. Drop old gait distance and movement state while
                // preserving the civilian's tactical WAIT/FOLLOW choice,
                // safety, mission time and saved rescue checkpoint format.
                reliefFollowerMoving = false;
                reliefFollowerWalkDistance = 0f;
                // The survivor's pre-flood route and stalled-route timer
                // are no longer anchored at the current physical feet.
                // Recompute next tick, but retain explicit WAIT/FOLLOW and
                // the exact protected mission clock and safety.
                ClearReliefFollowerRoute();
                reliefFollowerBlockedSeconds=0f;
                reliefFollowerBlockedNotice=false;
            }
        }
    }
}
