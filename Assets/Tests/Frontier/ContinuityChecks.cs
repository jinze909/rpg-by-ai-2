using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore Quiet(Campaign data, int profession = 0)
        { var g = Fresh(data,profession); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.invulnerable = 1000; g.save.x = 12; g.save.y = 14; return g; }
        private static Point WallStand(GameCore g)
        {
            for(int y=3;y<g.Map.rows.Length-3;y++) for(int x=3;x<g.Map.rows[y].Length-3;x++)
            { Point p=new Point(x+.24f,y+.5f); if(g.CanStand(p)&&g.Blocked(x-.1f,p.y)) return p; }
            throw new Exception("Production map must contain a walkable west wall approach");
        }
        public static void DistanceDrivenThirtySecondWalking(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession); var phases=new HashSet<int>(); float walked=0;
                Point[] route={new Point(12,14),new Point(17,14),new Point(17,16),new Point(12,16)}; int goal=1;
                foreach(Point p in route) Check(g.CanStand(p),"Thirty-second route stays on actual map ground");
                for(int frame=0;frame<1800;frame++)
                {
                    if((route[goal]-g.Position).Length<.15f) goal=(goal+1)%route.Length;
                    Point before=g.Position;g.Tick(1/60f,g.DirectionTo(g.Position,route[goal])); float distance=(g.Position-before).Length; walked+=distance;
                    Check(g.CanStand(g.Position),"Walking cannot leave the swept footprint");
                    if(distance>.001f) { Check(g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3,"Actual translation selects locomotion"); phases.Add(g.WalkFrame); }
                }
                Check(phases.Count==4&&walked>110&&Math.Abs(g.walkDistance-walked)<.02f,"All four gait phases follow 30 seconds of real travel");
                Check(g.footContacts==(int)Math.Floor(g.walkDistance/(GameCore.WalkStride/2)),"Foot contacts come from the same gait clock");
                // Previously the new SpriteRenderer animator sampled only the
                // four integer key poses, so ALL newly authored intermediate
                // walking frames were silently skipped.
                var sampling = Quiet(data,profession);
                sampling.movementDirection = new Point(1,0);
                sampling.locomotionFacing = new Point(1,0);
                for (int eighth=0;eighth<8;eighth++)
                {
                    sampling.walkDistance = GameCore.WalkStride * eighth / 8f;
                    float expected = eighth * .5f;
                    Check(sampling.HeroAnimationState==1
                        && Math.Abs(sampling.HeroAnimationPlaybackPosition(0)-expected)<.001f,
                        "SpriteRenderer distance sampler reaches midpoint " + eighth);
                    Check(sampling.WalkFrame==(int)expected,
                        "Legacy integer walk and native subframe share one stride phase");
                }
                sampling.walkDistance = GameCore.WalkStride;
                Check(Math.Abs(sampling.WalkPosePhase)<.001f,
                    "Run loop returns to the exact same foot contact after one stride");
                // Three-phase reactions derive from the existing clock; do
                // not move damage, dash distance, or persistent save schema.
                sampling.movementDirection = new Point();
                sampling.dashTime = .18f;
                Check(sampling.HeroAnimationState==4 &&
                      sampling.HeroAnimationFrameCount(0)==3 &&
                      Math.Abs(sampling.HeroAnimationPlaybackPosition(0))<.001f,
                      "Dodge begins upright on the original dash clock");
                sampling.dashTime = .09f;
                Check(Math.Abs(sampling.HeroAnimationPlaybackPosition(0)-1f)<.01f,
                      "Dodge middle cell follows the physical dash interval");
                sampling.dashTime = 0f;
                sampling.playerHitTime = .18f;
                Check(sampling.HeroAnimationState==5 &&
                      sampling.HeroAnimationFrameCount(0)==3 &&
                      Math.Abs(sampling.HeroAnimationPlaybackPosition(0))<.01f,
                      "Hit begins upright without inventing a separate damage clock");
                sampling.playerHitTime = 0f;
                sampling.deathTime = .9f;
                Check(sampling.HeroAnimationState==6 &&
                      sampling.HeroAnimationFrameCount(0)==4 &&
                      sampling.HeroAnimationFrame(0)==0,
                      "Death starts upright before recoil, crouch and grounded final frame");
                sampling.deathTime = .70f;
                Check(sampling.HeroAnimationPlaybackPosition(0)>1f,
                      "Death progresses into the existing fall pose");
                sampling.deathTime = .45f;
                Check(sampling.HeroAnimationFrame(0)==3,
                      "Death holds the original authored grounded corpse until recovery");
                g.Tick(.02f,new Point());float stopped=g.walkDistance;Advance(g,.6f);Check(!g.ActuallyMoving&&g.walkDistance==stopped,"Stopping cannot keep cycling the legs");
                Point wall=WallStand(g);g.save.x=wall.x;g.save.y=wall.y;
                for(int i=0;i<20;i++)g.Tick(.02f,new Point(-1,0));float distanceAtWall=g.walkDistance;int contacts=g.footContacts;
                for(int i=0;i<90;i++)g.Tick(.02f,new Point(-1,0));
                Check(!g.ActuallyMoving&&g.walkDistance==distanceAtWall&&g.footContacts==contacts,"Pushing a wall cannot create foot sliding or phantom footsteps");
            }
        }
        public static void UnifiedMobilityAndLockedDirections(Campaign data)
        {
            Point[] directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
            for(int weapon=0;weapon<3;weapon++) foreach(Point direction in directions)
            {
                var g=Quiet(data,weapon);Point start=g.Position;g.Attack(direction);ActorAction a=g.playerAction;
                Check(g.PlayerPhase==ActionPhase.Windup,"Attacks enter preparation first");
                while(a.elapsed<a.contact-.015f) g.Tick(.01f,new Point(0,1),direction*-1);
                Check(!a.released&&(a.direction-direction).Length<.001f&&(g.VisualFacing-direction).Length<.001f,"Turning input cannot redirect a prepared animation");
                if(weapon>0) Check((g.Position-start).Length<.001f&&g.PlayerMobility==MobilityRule.Stationary,"Draw/cast preparation plants the feet");
                else Check((g.Position-start).Length>.1f&&g.PlayerMobility==MobilityRule.AttackMovement,"Sword preparation has a real collision-aware forward step");
                while(!a.released)g.Tick(.01f,new Point(0,1),direction*-1);
                Check(g.PlayerPhase==ActionPhase.Active&&g.PlayerPose==5,"Active animation and contact share the same instant");
                if(weapon>0)
                {
                    Check(g.projectiles.Count==1&&(g.projectiles[0].velocity.Normalized-direction).Length<.001f,"Missile heading is the prepared action heading");
                    Effect release=g.effects.Find(e=>e.kind=="release");Check(release!=null&&(release.position-GameCore.WeaponOrigin(g.Position,weapon,direction)).Length<.001f&&release.height==GameCore.WeaponHeight(weapon,direction),"Release and projectile share the correct directional weapon attachment");
                }
                while(g.playerAction!=null&&g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Point recovery=g.Position;g.Tick(.01f,new Point(0,1));
                Check((g.Position-recovery).Length>.001f&&g.PlayerMobility==MobilityRule.Reduced&&
                    (g.PlayerPose==5||g.PlayerPose==7||g.PlayerPose>=2&&g.PlayerPose<=3),
                    "Recovery restores true translation with authored attack upper pose and unified leg gait");
                g.playerAction=null;g.skillCooldown=0;g.save.energy=100;g.Skill(direction);a=g.playerAction;start=g.Position;
                while(g.playerAction!=null)g.Tick(.02f,new Point(1,0),direction*-1);
                if(weapon>0)
                    Check((g.Position-start).Length>.03f,
                        "Spell and bow Q anchor the real contact but then allow reduced-speed moving recovery");
                g.Tick(.02f,new Point(1,0));Check(g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3,"A completed Q returns to locomotion without a frozen full-body pose");
            }
            var blocked=Quiet(data);Point approach=WallStand(blocked);blocked.save.x=approach.x;blocked.save.y=approach.y;blocked.Attack(new Point(-1,0));Advance(blocked,.6f);
            Check(blocked.CanStand(blocked.Position)&&blocked.Position.x>=Math.Floor(approach.x)+.219f,"Melee root movement cannot tunnel through a wall");
        }
        private static HashSet<int> ArenaReachable(GameCore g)
        {
            const int density=4;int width=g.Map.rows[0].Length*density,height=g.Map.rows.Length*density;
            int start=(int)(g.Position.y*density)*width+(int)(g.Position.x*density);var found=new HashSet<int>();var queue=new Queue<int>();found.Add(start);queue.Enqueue(start);
            int[] dx={1,-1,0,0,1,1,-1,-1},dy={0,0,1,-1,1,-1,1,-1};
            while(queue.Count>0)
            {
                int p=queue.Dequeue(),x=p%width,y=p/width;
                for(int k=0;k<8;k++)
                {
                    int xx=x+dx[k],yy=y+dy[k],next=yy*width+xx;if(xx<0||yy<0||xx>=width||yy>=height||found.Contains(next))continue;
                    Point target=new Point((xx+.5f)/density,(yy+.5f)/density);
                    if(!g.CanStand(target)||!g.CanStand(new Point((xx+.5f)/density,(y+.5f)/density))||!g.CanStand(new Point((x+.5f)/density,(yy+.5f)/density)))continue;
                    found.Add(next);queue.Enqueue(next);
                }
            }
            return found;
        }
        public static void ArenaBoundariesAndOpeningTransactions(Campaign data)
        {
            foreach(int room in new[]{1,2,3,5,6,7})
            {
                var g=Fresh(data);g.save.stage=3;g.save.frontierEnding=1;g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");g.save.flags.Add("desert_report");g.Enter(room);g.enemies.Clear();
                Check(g.save.room==room&&!g.GateOpen&&g.GateBarrierActive,"Every objective arena starts physically closed");
                HashSet<int> reached=ArenaReachable(g);int width=g.Map.rows[0].Length*4;
                foreach(int cell in reached)Check(!g.InProtectedArena(new Point((cell%width+.5f)/4,(cell/width+.5f)/4)),"Eight-direction quarter-tile flood fill cannot bypass any arena from edges or gaps");
                for(float y=2.25f;y<14;y+=.25f)for(float x=26.25f;x<34;x+=.25f)Check(!g.CanStand(new Point(x,y)),"The entire forbidden arena, not only one wall, is protected");
                for(int side=0;side<2;side++)
                {
                    bool tested=false;
                    for(float t=3;t<13&&!tested;t+=.5f)
                    {
                        Point from=side==0?new Point(25.6f,t):new Point(26+t/2,14.4f);if(!g.CanStand(from))continue;
                        g.save.x=from.x;g.save.y=from.y;g.save.energy=100;g.dashCooldown=0;g.Dash(side==0?new Point(1,0):new Point(0,-1));Advance(g,.3f);
                        Check(!g.InProtectedArena(g.Position)&&g.CanStand(g.Position),"Dodge sweeps cannot cross an objective barrier");tested=true;
                    }
                }
                if(room==5)Check(!g.Blocked(28,18)&&g.CanStand(new Point(28,18)),"Harbor castaway remains reachable outside the protected arena");
                g.save.flags.Add(room==1?"scout_saved":"gate_"+room);g.gateOpeningTime=0;Check(!g.GateBarrierActive&&g.CanStand(new Point(29,9)),"Completed gates open their real arena ground");
            }
            var forest=Quiet(data);forest.save.kills[1]=4;Action(forest,"scout","scout");forest.Close();
            Check(forest.GateOpen&&forest.GateBarrierActive&&forest.gateOpeningTime>0,"Rescue starts a visible opening before removing collision");Advance(forest,.25f);float timer=forest.gateOpeningTime;forest.mode="pause";Advance(forest,1);
            Check(forest.gateOpeningTime==timer&&forest.GateBarrierActive,"Pausing cannot silently remove a barrier");forest.Close();Advance(forest,.4f);Check(!forest.GateBarrierActive,"Collision opens when the transition finishes");
            var old=Quiet(data);old.save.x=29;old.save.y=9;int coins=old.save.coins;var loaded=new GameCore(data,old.save);
            Check(loaded.CanStand(loaded.Position)&&!loaded.InProtectedArena(loaded.Position)&&loaded.save.coins==coins&&!loaded.GateOpen,"Old illegal positions recover safely without granting objectives or losing rewards");
        }
        public static void SharedPauseAndEffectFinishingClock(Campaign data)
        {
            var g=Quiet(data);g.effects.Add(new Effect("slash",g.Position,.3f));var corpse=new FallenActor{actor=new Enemy(994,0,13,14,0),room=1,life=.65f,duration=.65f};g.fallen.Add(corpse);g.Attack(new Point(1,0));g.mode="inventory";Advance(g,1);
            Check(g.effects[0].life==.3f&&corpse.life==.65f&&g.playerAction.elapsed==0,"Menus freeze effects, deaths and action contact together");g.Close();g.hitStop=.04f;g.Tick(.02f,new Point());
            Check(g.effects[0].life==.3f&&corpse.life==.65f&&g.playerAction.elapsed==0,"Hit stop freezes the same visual clock");g.hitStop=0;Advance(g,.8f);Check(!g.fallen.Contains(corpse),"A resumed death animation naturally completes");
            g.projectiles.Clear();g.effects.Clear();g.projectiles.Add(new Projectile{position=g.Position,velocity=new Point(2,0),visual="arrow",friendly=true,life=.01f});g.Tick(.02f,new Point());
            Check(g.projectiles.Count==0&&g.effects.Exists(e=>e.kind=="projectileFade")&&!g.effects.Exists(e=>e.kind=="projectileImpact"),"Expired flight dissolves instead of fabricating a collision");
        }
        public static void AlliesAndEnemiesUseActualTravel(Campaign data)
        {
            var g=Quiet(data);g.save.flags.Add("companion_1");g.SelectCompanion(1);g.companionPosition=new Point(5,14);g.save.x=17;g.save.y=14;Point before=g.companionPosition;g.Tick(.05f,new Point());
            Check((g.companionPosition-before).Length<.35f&&g.CanStand(g.companionPosition),"An offscreen ally walks to catch up instead of teleporting across obstacles");
            g.save.companion=-1;var foe=new Enemy(995,2,13,16,999){timer=999};g.enemies.Add(foe);
            for(int frame=0;frame<150;frame++)
            { before=foe.Position;float distance=foe.moveTime;g.Tick(.02f,new Point());float moved=(foe.Position-before).Length;Check(Math.Abs(foe.moveTime-distance-moved)<.001f&&foe.moving==(moved>.001f)&&g.CanStand(foe.Position),"Enemy gait clock and facing are based on real collision-safe displacement"); }
        }
        public static void SoftMaterialFootContacts(Campaign data)
        {
            double last=0;
            foreach(string surface in new[]{"grass","stone","wood","sand","snow"})
            {
                float[] a=AudioSynthesis.Footstep(surface,0),b=AudioSynthesis.Footstep(surface,1);double square=0,mean=0,difference=0;float peak=0;
                for(int i=0;i<a.Length;i++){square+=a[i]*a[i];mean+=a[i];difference+=Math.Abs(a[i]-b[i]);peak=Math.Max(peak,Math.Abs(a[i]));}
                double rms=Math.Sqrt(square/a.Length);Check(peak<.14&&rms<.035&&Math.Abs(mean/a.Length)<.01&&difference>1,"Contacts are quiet, short, varied and free of a harsh sustained tone");Check(rms!=last,"Materials have distinct synthesized contact timbres");last=rms;
            }
        }
        public static void PointBlankWallsAndCrossEquipmentActions(Campaign data)
        {
            for(int weapon=1;weapon<3;weapon++)
            {
                var g=Quiet(data,weapon);Point stand=WallStand(g);g.save.x=stand.x;g.save.y=stand.y;g.Attack(new Point(-1,0));Advance(g,.3f);
                Check(g.projectiles.Count==0&&g.effects.Exists(e=>e.kind=="projectileImpact"||e.kind=="magicImpact"),"A muzzle extending into a wall creates a near-side impact instead of spawning behind it");
                g.effects.Clear();g.projectiles.Add(new Projectile{position=new Point((float)Math.Floor(stand.x)-.1f,stand.y),velocity=new Point(200,0),friendly=true,visual="arrow"});g.Tick(.02f,new Point());
                Check(g.projectiles.Count==0&&g.effects.Exists(e=>e.kind=="projectileImpact"),"A missile initially inside a solid cannot exit it on a fast frame");
            }
            for(int profession=0;profession<3;profession++)for(int equipped=0;equipped<3;equipped++)
            {
                var g=Quiet(data,profession);g.save.weapon=equipped;g.Skill(new Point(0,-1));ActorAction action=g.playerAction;Point before=g.Position;
                Check(action.weapon==profession&&action.equippedWeapon==equipped&&g.AppearanceWeapon==equipped,"Class technique retains actual equipped hero while skill mechanics remain discipline-owned");
                while(!action.released)g.Tick(.01f,new Point(1,0));
                Check(action.released&&(profession!=1||g.projectiles.Count==5),"A cross-equipped class Q remains functional and releases on contact");
                if(profession>0)Check((g.Position-before).Length<.001f,"Ranged class Q stays planted even when a sword is equipped");
            }
            var closed=Quiet(data);closed.save.x=25.5f;closed.save.y=7;var archer=new Enemy(993,1,28.5f,7,999){timer=0};closed.enemies.Add(archer);Advance(closed,.8f);
            Check(archer.state==0&&closed.projectiles.Count==0,"Enemies do not prepare or shoot through the closed objective boundary");
        }
        public static void ClosedCacheRoutesAndCollision(Campaign data)
        {
            foreach(int room in new[]{1,2,3,5,6,7})
            {
                var g=Fresh(data);g.save.stage=3;g.save.frontierEnding=1;
                foreach(string flag in new[]{"exp_intro","harbor_report","desert_report","scout_saved","gate_2","gate_3","gate_5","gate_6","gate_7"})g.save.flags.Add(flag);
                g.Enter(room);g.enemies.Clear();g.gateOpeningTime=0;Point cache=new Point(4.5f,19.5f);int width=g.Map.rows[0].Length*4;
                int key=(int)(cache.y*4)*width+(int)(cache.x*4);Check(!ArenaReachable(g).Contains(key),"A closed southwest cache cannot be reached around map edges or wall gaps in room "+room);
                Point approach=new Point(7.35f,19.5f);Check(g.CanStand(approach),"The existing seal approach stays walkable");g.save.x=approach.x;g.save.y=approach.y;g.Dash(new Point(-1,0));Advance(g,.3f);
                Check(g.Position.x>=7.219f&&g.CanStand(g.Position),"Fast movement cannot cross the sealed cache entrance");
                g.save.flags.Add("seal_"+room);Check(ArenaReachable(g).Contains(key),"Removing the actual seal opens the cache's real route");
            }
        }
        public static void AuthoredMuzzlesAndContinuousLaunchHeight(Campaign data)
        {
            Point[] directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
            for(int weapon=1;weapon<3;weapon++)foreach(Point direction in directions)
            {
                var g=Quiet(data,weapon);Point target=g.Position+direction*3;Check(g.CanStand(target),"Muzzle test uses real unobstructed ground");var foe=new Enemy(991,0,target.x,target.y,999){stun=999};g.enemies.Add(foe);g.Attack(direction);
                while(!g.playerAction.released)g.Tick(.01f,new Point());
                Check(g.projectiles.Count==1,"Every directional contact emits its real missile");Projectile shot=g.projectiles[0];Effect cue=g.effects.Find(e=>e.kind=="release");
                Check(cue!=null&&Math.Abs(shot.launchHeight-cue.height)<.001f&&Math.Abs(shot.height-shot.launchHeight)<.26f,"Release spark and newborn missile attach to the same actual bow/staff; no height snap");
                float previous=shot.height;while(g.projectiles.Contains(shot)&&shot.age<.28f){g.Tick(.01f,new Point());Check(Math.Abs(shot.height-previous)<2.7f,"Raised staff release settles smoothly over quarter-second flight");previous=shot.height;}
                Advance(g,.5f);Check(foe.hp<999,"Real projectile size and directional muzzle offset cannot make a straight-ahead target unhittable");
            }
            Check(GameCore.WeaponHeight(2,new Point(0,1))==82&&GameCore.WeaponHeight(2,new Point(0,-1))==65,"Authored vertical staff orbs are above the hand and head, not at the chest");
        }
        public static void RealArrowMuzzleStopsAtWallLeadingEdge(Campaign data)
        {
            var g = Quiet(data, 1);
            Point approach = WallStand(g);
            // A hero still stands entirely on open ground, but the 0.82
            // tile bow muzzle ends just 0.08 tiles from this real solid wall:
            // the old point-only check erroneously called it unobstructed.
            Point feet = new Point((float)Math.Floor(approach.x) + .90f,
                approach.y);
            Check(g.CanStand(feet) &&
                g.Blocked((float)Math.Floor(approach.x) - .1f, feet.y),
                "The authored wall-edge attack starts at legal actor footing next to actual solid cover");
            g.save.x = feet.x; g.save.y = feet.y;
            g.Attack(new Point(-1, 0));
            for (int step = 0; step < 20 && g.projectiles.Count == 0; step++)
                g.Tick(.05f, new Point());
            Check(g.projectiles.Count == 1,
                "The real ranger contact still emits exactly one arrow");
            Projectile arrow = g.projectiles[0];
            Check(arrow.blockedAtSpawn && arrow.age == 0,
                "Muzzle must register a leading-edge collision before the newborn shot enters the wall");
            Check(arrow.position.x - arrow.Radius >=
                (float)Math.Floor(approach.x) - .0001f,
                "Real arrow's physical left edge never crosses the wall during weapon attachment");
            g.Tick(.05f, new Point());
            Check(!g.projectiles.Contains(arrow) &&
                g.effects.Exists(e => e.kind == "projectileImpact") &&
                g.CanStand(g.Position),
                "A real near-side wall spark retires the blocked arrow on the following frame without moving the hero into cover");

            var mage = Quiet(data, 2);
            Point mageWall = WallStand(mage);
            // Moving outward from a solid LEFT wall is a valid cast: the
            // body fits on walkable ground and the hand is to its RIGHT.
            mage.save.x = mageWall.x; mage.save.y = mageWall.y;
            mage.Attack(new Point(1, 0));
            for (int tick = 0; tick < 20 && mage.projectiles.Count == 0; tick++)
                mage.Tick(.05f, new Point());
            Check(mage.projectiles.Count == 1 &&
                !mage.projectiles[0].blockedAtSpawn &&
                mage.projectiles[0].position.x > mage.Position.x + 1,
                "A staff cast away from cover remains playable even if its theoretical center-sized orb grazes the wall BEHIND the hero");
        }

    }
}
