using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void RejectedAbyssSlotsRetireOnlyUnbankedProgress(Campaign data)
        {
            var g = Fresh(data);
            g.save.room = 8; g.save.abyssFloor = 4;
            g.save.abyssRisk = 2; g.save.runCoins = 51;
            g.save.runShards = 8; g.save.runXp = 33;
            g.save.boons[0] = 3; g.save.bestFloor = 3;
            g.save.coins = 147; g.save.shards = 19;
            g.save.x = 12; g.save.y = 14;
            g.save.snapshotRoom = 8;
            g.save.worldEnemies = new[] { new EnemyRecord { id=410, hp=10, maxHp=10 } };
            // No covenant: the recorded Abyss is not a legal destination.
            var rejected = new GameCore(data, CloneSave(g.save));
            Check(rejected.save.room == 0 &&
                Math.Abs(rejected.save.x-data.maps[0].spawnX) < .001f &&
                Math.Abs(rejected.save.y-data.maps[0].spawnY) < .001f,
                "Unauthorized Abyss resumes at the actual home spawn");
            Check(rejected.save.abyssFloor == 0 && rejected.save.runCoins == 0 &&
                rejected.save.runShards == 0 && rejected.save.runXp == 0 &&
                rejected.save.boons[0] == 0 && rejected.save.abyssEnemies == null,
                "Rejected expedition cannot retain transient loot or blessings");
            Check(rejected.save.coins == 147 && rejected.save.shards == 19 &&
                rejected.save.bestFloor == 3 && rejected.save.snapshotRoom == -1 &&
                rejected.save.worldEnemies == null,
                "Rejected expedition preserves banked wealth and permanent records");
            // Earlier expansion sanitization also relocates an empty Abyss.
            var empty = CloneSave(g.save); empty.abyssFloor = 0;
            var restored = new GameCore(data, empty);
            Check(restored.save.room == 0 &&
                Math.Abs(restored.save.x-data.maps[0].spawnX) < .001f &&
                Math.Abs(restored.save.y-data.maps[0].spawnY) < .001f,
                "Zero-floor Abyss slot does not strand hero at old coordinates");
            // A genuine v2 active expedition remains precisely resumable.
            var allowed = CloneSave(g.save); allowed.frontierEnding = 1;
            allowed.x = 5; allowed.y = 14; allowed.abyssFloor = 1;
            allowed.abyssRisk = 0; allowed.abyssSeed = 137;
            var active = new GameCore(data, allowed);
            Check(active.save.room == 8 && active.save.abyssFloor == 1 &&
                active.save.frontierEnding == 1 && active.CanStand(active.Position),
                "Authorized v2 Abyss expedition is not discarded during load");
        }

        public static void ReusedLoadsMatchFreshWorldState(Campaign data)
        {
            foreach(int room in new[]{1,2,3,5,6,7})
            {
                var previous=Unlocked(data);previous.save.flags.Add("scout_saved");previous.save.flags.Add("gate_2");previous.save.flags.Add("gate_3");previous.Enter(room);
                Point inside=new Point();bool found=false;
                for(int y=3;y<13&&!found;y++)for(int x=27;x<previous.Map.rows[0].Length-2;x++)
                { Point p=new Point(x+.5f,y+.5f);if(previous.CanStand(p)){inside=p;found=true;break;} }
                Check(found,"Every unlocked arena contains a valid selected-save position");
                var selected=CloneSave(previous.save);selected.x=inside.x;selected.y=inside.y;previous.gateOpeningTime=.55f;
                var fresh=new GameCore(data,CloneSave(selected));previous.Load(CloneSave(selected));
                Check((previous.Position-fresh.Position).Length<.001f&&previous.CanStand(previous.Position),"A prior world's gate-opening timer cannot relocate a valid position in selected room "+room);
                Check(previous.playerAction==null&&previous.companionAction==null&&previous.projectiles.Count==0&&previous.gateOpeningTime==0,"Load clears the old world's transient action and collision state");
            }
        }
        public static void ChangedActionProfilesCancelBeforeMovement(Campaign data)
        {
            for(int change=0;change<5;change++)
            {
                var g=Quiet(data);g.save.flags.Add("core_1");g.save.flags.Add("weapon_1");g.save.flags.Add("technique_"+g.save.profession);
                if(change>=3)g.Enter(0);
                g.Attack(new Point(1,0));Point before=g.Position;g.mode="inventory";
                if(change==0)g.SetStyle(1);else if(change==1)g.LearnTechnique();else if(change==2)g.save.weapon=1;
                else if(change==3)Action(g,"forge","bow");
                else {g.save.inventory[1]=4;g.save.inventory[4]=3;Action(g,"forge","staff");}
                g.Close();g.Tick(.05f,new Point());
                Check((g.Position-before).Length<.001f&&g.playerAction==null&&g.projectiles.Count==0,"Changing style, technique or a legacy equipment field cancels the stored action before its movement or contact");
            }
        }
        public static void FlyingShotsRetainReleaseProfile(Campaign data)
        {
            var g=Quiet(data,1);g.save.flags.Add("core_1");g.save.reducedMotion=false;
            var target=new Enemy(1900,0,18,14,999){stun=99};g.enemies.Add(target);g.Attack(new Point(1,0));
            for(int i=0;i<20&&g.projectiles.Count==0;i++)g.Tick(.02f,new Point());
            Check(g.projectiles.Count==1,"The real bow action must launch a single projectile");
            g.Equip(0);g.SetStyle(2);g.combo=2;
            for(int i=0;i<80&&target.hp==999;i++)g.Tick(.02f,new Point());float damage=999-target.hp;
            Check(damage>0&&Math.Abs(target.stagger-damage*.45f)<.001f&&g.impactShake==0,"A flying arrow cannot borrow a later sword's stagger style or heavy-combo shake");
            g=Quiet(data,1);target=new Enemy(1901,0,18,14,1){stun=99};g.enemies.Add(target);g.Attack(new Point(1,0));
            for(int i=0;i<20&&g.projectiles.Count==0;i++)g.Tick(.02f,new Point());g.Equip(0);
            for(int i=0;i<80&&g.enemies.Count>0;i++)g.Tick(.02f,new Point());
            Check(g.enemies.Count==0&&g.save.mastery[1]==2&&g.save.mastery[0]==0,"Projectile kills credit the equipped weapon captured at launch, even after a menu equipment change");
            g=Quiet(data,2);g.save.flags.Add("core_1");g.save.reducedMotion=false;
            target=new Enemy(1903,0,18,14,999){stun=99};var neighbor=new Enemy(1904,0,18.6f,14,999){stun=99};g.enemies.Add(target);g.enemies.Add(neighbor);
            g.Attack(new Point(1,0));for(int i=0;i<30&&g.projectiles.Count==0;i++)g.Tick(.02f,new Point());
            Check(g.projectiles.Count==1&&g.projectiles[0].explosive,"The real staff action launches one explosive projectile");
            g.Equip(0);g.SetStyle(2);g.combo=2;
            for(int i=0;i<80&&neighbor.hp==999;i++)g.Tick(.02f,new Point());
            Check(target.hp<999&&neighbor.hp<999&&Math.Abs(target.stagger-(999-target.hp)*.45f)<.001f&&Math.Abs(neighbor.stagger-(999-neighbor.hp)*.45f)<.001f&&g.impactShake==0,"Primary and splash contacts retain the same captured staff profile without borrowing the later sword's stagger or shake");
        }
        public static void ProjectileLastPartialFrameStillCollides(Campaign data)
        {
            var g=Quiet(data);var target=new Enemy(1902,0,15,14,999){stun=99};g.enemies.Add(target);
            var shot=new Projectile{position=new Point(14.3f,14),velocity=new Point(10,0),life=.025f,damage=10,friendly=true,visual="arrow"};g.projectiles.Add(shot);
            g.Tick(.05f,new Point());Check(target.hp<999&&g.effects.Exists(e=>e.kind=="projectileImpact"),"The valid final partial flight segment must resolve contact before lifetime retirement");
            g=Quiet(data);Point start=new Point(13,14);shot=new Projectile{position=start,velocity=new Point(10,0),life=.015f,damage=10,friendly=true,visual="arrow"};g.projectiles.Add(shot);
            g.Tick(.05f,new Point());Effect fade=g.effects.Find(e=>e.kind=="projectileFade");
            Check(g.projectiles.Count==0&&fade!=null&&Math.Abs(fade.position.x-start.x-.15f)<.001f&&Math.Abs(shot.age-.015f)<.001f,"An expired projectile finishes at its precise last flight position without overshoot or an early disappearance");
        }
        public static void ZeroDurationTicksPreserveLiveFlights(Campaign data)
        {
            var g=Quiet(data);var shot=new Projectile{position=new Point(13,14),velocity=new Point(10,0),life=1,friendly=true,visual="arrow"};g.projectiles.Add(shot);
            g.Tick(0,new Point());
            Check(g.projectiles.Count==1&&shot.age==0&&shot.life==1&&shot.position.x==13&&g.effects.Count==0,"A zero-duration frame cannot retire, advance or fade a live projectile");
            var target=new Enemy(1905,0,13.4f,14,999){stun=99};g.enemies.Add(target);shot.damage=10;float time=g.save.totalTime;
            foreach(float invalidStep in new[]{0f,-.01f,float.NaN,float.PositiveInfinity})g.Tick(invalidStep,new Point(1,0));
            Check(target.hp==999&&g.save.totalTime==time&&g.projectiles.Count==1&&shot.age==0,"Zero, negative or nonfinite frame intervals cannot resolve contact or contaminate simulation state");
        }
        public static void DeathCannotEnterPractice(Campaign data)
        {
            var g=Unlocked(data);g.Enter(0);g.invulnerable=0;g.save.health=1;g.save.reducedMotion=true;
            g.projectiles.Add(new Projectile{position=g.Position-new Point(.2f,0),velocity=new Point(10,0),damage=999,source="shot"});g.Tick(.02f,new Point());
            Check(g.Dying&&g.Has("recovery_pending"),"An actual lethal projectile begins the recovery transaction");
            g.mode="help";g.StartPractice();
            Check(g.Dying&&!g.practicing&&g.save.health==0,"The practice entry cannot cancel a fallen player's recovery or create a living zero-health actor");
            g.Close();for(int i=0;i<25;i++)g.Tick(.05f,new Point());
            Check(!g.Dying&&!g.Has("recovery_pending")&&g.save.health==g.save.maxHealth&&g.save.room==0,"Normal recovery remains completable after a rejected practice entry");
        }
        public static void MixedStateTransitionsMaintainWorldInvariants(Campaign data)
        {
            for(int seed=1;seed<=6;seed++)
            {
                var random=new Random(seed*733);var g=Unlocked(data);g.save.flags.Add("scout_saved");g.save.flags.Add("gate_2");g.save.flags.Add("gate_3");
                g.save.flags.Add("core_1");g.save.flags.Add("weapon_1");g.save.flags.Add("weapon_2");g.save.flags.Add("technique_"+g.save.profession);g.save.reducedMotion=true;
                g.save.profession=(seed-1)%3;g.save.weapon=g.save.profession;g.save.flags.Add("technique_"+g.save.profession);g.Load(CloneSave(g.save));g.Start();
                for(int frame=0;frame<3000;frame++)
                {
                    Point direction=new Point(random.Next(-1,2),random.Next(-1,2));int action=random.Next(80);
                    if(action==0){g.PrepareSave();g.Load(CloneSave(g.save));g.Start();}
                    else if(action==1)g.Enter(random.Next(10));
                    else if(action==2)g.Equip(random.Next(3));
                    else if(action==3)g.SetStyle(random.Next(3));
                    else if(action==4)g.LearnTechnique();
                    else if(action==5){g.mode="inventory";g.Tick(.04f,direction);g.Close();}
                    else if(action<16)g.Skill(direction);
                    else if(action<29)g.Attack(direction);
                    else if(action<34)g.Dash(direction);
                    if(g.mode=="dialog")g.Close();g.Tick(.02f,direction);
                    Check(g.CanStand(g.Position),"Mixed transitions keep the player on valid ground: seed "+seed+" frame "+frame);
                    Check(!float.IsNaN(g.save.health)&&g.save.health>=0&&g.save.health<=g.save.maxHealth&&g.save.energy>=0&&g.save.energy<=100,"Health and resources stay bounded across mixed transitions");
                    var ids=new HashSet<int>();foreach(Enemy enemy in g.enemies)Check(ids.Add(enemy.id)&&g.CanStand(enemy.Position)&&enemy.hp>0,"Live actors have distinct identities, positive health and full foot clearance");
                    foreach(Projectile p in g.projectiles)Check(p.life>0&&!float.IsNaN(p.position.x)&&!float.IsNaN(p.position.y)&&!float.IsInfinity(p.velocity.Length),"Active projectile state stays finite and alive");
                    if(g.playerAction!=null)Check(g.playerAction.contact>0&&g.playerAction.contact<=g.playerAction.duration&&g.playerAction.equippedWeapon==g.save.weapon&&g.playerAction.style==g.Style,"Only valid current-equipment actions survive a mixed transition");
                }
            }
        }
        public static void FreshProjectilesCannotTravelBeforeTheirContactFrame(Campaign data)
        {
            var ranger = Fresh(data, 1);
            ranger.enemies.Clear();
            ranger.save.x = 12; ranger.save.y = 14;
            ranger.Attack(new Point(1, 0));
            for (int frame = 0; frame < 24 && ranger.projectiles.Count == 0; frame++)
                ranger.Tick(.05f, new Point());
            Check(ranger.projectiles.Count == 1,
                "The actual ranger action releases a single arrow at its animation contact");
            Projectile arrow = ranger.projectiles[0];
            Point muzzle = arrow.position;
            float lifetime = arrow.life;
            Check(arrow.age == 0 && lifetime > 1,
                "A shot emitted inside the current 50ms action frame must still be at its muzzle with its full flight life");
            ranger.Tick(.05f, new Point());
            Check(ranger.projectiles.Contains(arrow) && arrow.age > 0 &&
                (arrow.position - muzzle).Length > .1f && arrow.life < lifetime,
                "That same arrow must advance, collide and age normally on the next simulation frame");

            var foe = Fresh(data);
            foe.enemies.Clear(); foe.projectiles.Clear();
            foe.save.x = 18; foe.save.y = 14;
            var sniper = new Enemy(99801, 1, 21, 14, 100);
            sniper.state = 1; sniper.timer = .005f;
            sniper.facing = new Point(-1, 0);
            sniper.target = foe.Position;
            foe.enemies.Add(sniper);
            foe.Tick(.05f, new Point());
            Projectile hostile = foe.projectiles.Find(p => !p.friendly);
            Check(hostile != null && hostile.age == 0 &&
                hostile.life > 1,
                "An enemy archer releasing during this frame cannot age or fly its hostile projectile before that release");
            Point spawn = hostile.position;
            foe.Tick(.05f, new Point());
            Check(hostile.age > 0 && (hostile.position - spawn).Length > .1f,
                "Hostile shots resume actual swept flight on the next world step");
        }

        public static void RealHeavyAndBossGroundWarningsPrecedeImpactAndRetractOnStun(Campaign data)
        {
            var g = Fresh(data);
            g.enemies.Clear(); g.effects.Clear();
            g.save.x = 18.5f; g.save.y = 14.5f;
            g.invulnerable = 0;
            var armored = new Enemy(92501, 2, 20.5f, 14.5f, 200);
            armored.timer = 0; g.enemies.Add(armored);
            float health = g.save.health;
            g.Tick(.05f, new Point());
            Check(armored.state == 1 && g.save.health == health,
                "An actual heavy enemy begins its locked AoE windup before making contact");
            Effect preview = g.effects.Find(e => e.kind == "danger" &&
                (e.position - armored.target).Length < .01f && e.radius > 0 &&
                e.life > .5f);
            Check(preview != null,
                "Heavy's true targeted ground circle is visible for its 650ms windup, never first appearing after damage");
            var stop = typeof(GameCore).GetMethod("ApplyEnemyControl",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(stop != null, "Real combat interruption method must remain available");
            stop.Invoke(g, new object[] { armored, 1.4f });
            Check(!g.effects.Contains(preview) && armored.state == 0,
                "Interrupting a heavy during preparation retracts exactly its unspent danger warning");

            var boss = Fresh(data);
            boss.save.stage = 1; boss.Enter(1);
            boss.enemies.Clear(); boss.effects.Clear();
            boss.save.x = 18.5f; boss.save.y = 14.5f;
            var guardian = new Enemy(92502, 3, 21f, 14.5f, 500, true);
            guardian.timer = 0; boss.enemies.Add(guardian);
            boss.Tick(.05f, new Point());
            Check(guardian.state == 1 &&
                boss.effects.Exists(e => e.kind == "danger" &&
                    (e.position - guardian.target).Length < .01f &&
                    e.life > .7f && e.radius > 0),
                "Real regional Boss advertises its attack footprint throughout windup instead of drawing the red ring after contact");
        }

        public static void HeavySlamContactClearsWarningAndShowsActualImpact(Campaign data)
        {
            var g = Fresh(data);
            g.enemies.Clear(); g.effects.Clear();
            g.save.x = 18.5f; g.save.y = 14.5f;
            g.invulnerable = 0;
            var heavy = new Enemy(92507, 2, 20f, 14.5f, 200);
            heavy.timer = 0;
            g.enemies.Add(heavy);
            g.Tick(.05f, new Point());
            Effect warning = g.effects.Find(e => e.kind == "danger" &&
                (e.position - heavy.target).Length < .01f);
            Check(warning != null && warning.life > .5f,
                "A genuine heavy slam first paints a readable circle before impact");
            for (int tick = 0; tick < 18 && heavy.state == 1; tick++)
                g.Tick(.05f, new Point());
            Check(heavy.state == 0, "The unaltered heavy action delivers its real contact");
            Check(!g.effects.Exists(e => e.kind == "danger" &&
                (e.position - heavy.target).Length < .01f),
                "The completed strike never creates a phantom red warning AFTER its contact");
            Check(g.effects.Exists(e => e.kind == "burst" &&
                (e.position - heavy.target).Length < .01f &&
                e.radius > 0),
                "Completed heavy impact shows a separate short real contact flash");
        }

        public static void OptionalDefenseCleanupRetiresItsOwnFullAttackTimeline(Campaign data)
        {
            foreach (bool glasswind in new[] { true, false })
            {
                var g = Fresh(data);
                g.enemies.Clear(); g.effects.Clear();
                g.save.x = 18.5f; g.save.y = 14.5f;
                g.invulnerable = 100;
                var unrelated = new Effect("discover", new Point(12, 14), 5);
                g.effects.Add(unrelated);
                // Both ids are actual authored optional-defense raider ranges.
                var attacker = new Enemy(glasswind ? 89822 : 89511,
                    2, 20.5f, 14.5f, 200);
                attacker.timer = 0;
                g.enemies.Add(attacker);
                g.Tick(.05f, new Point());
                Effect circle = g.effects.Find(e => e.kind == "danger" &&
                    (glasswind ? e.glasswindOwned : e.commonsRelayOwned));
                Check(circle != null && circle.life > .5f,
                    "An actual authored defense attacker has an owned target circle before releasing damage");
                bool delivered = false;
                for (int n = 0; n < 18; n++)
                {
                    g.Tick(.05f, new Point());
                    if (g.effects.Exists(e => e.kind == "burst" &&
                        (glasswind ? e.glasswindOwned : e.commonsRelayOwned)))
                    { delivered = true; break; }
                }
                Check(delivered && g.effects.Exists(e => e.kind == "enemyRelease" &&
                    (glasswind ? e.glasswindOwned : e.commonsRelayOwned)),
                    "The same enemy's contact burst and attack-release visual inherit the original mission's owner");
                string reset = glasswind ? "ResetGlasswind" : "ResetCommonsRelayTrial";
                var retire = typeof(GameCore).GetMethod(reset,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Check(retire != null, "Original mission cleanup must remain callable");
                retire.Invoke(g, new object[0]);
                Check(!g.effects.Exists(e => glasswind ? e.glasswindOwned : e.commonsRelayOwned) &&
                    !g.enemies.Contains(attacker) && g.effects.Contains(unrelated),
                    "Real mission cancellation removes only its windup, release and contact effects while preserving unrelated world visuals");
            }
        }

        public static void RetiredDefenseNeverLeavesLiveArcherShotsBehind(Campaign data)
        {
            foreach (bool glasswind in new[] { true, false })
            {
                var g = Fresh(data);
                g.enemies.Clear(); g.projectiles.Clear();
                g.save.x = 18.5f; g.save.y = 14.5f;
                g.invulnerable = 100;
                var raider = new Enemy(glasswind ? 89822 : 89512, 1, 21.5f, 14.5f, 90);
                raider.state = 1; raider.timer = .005f;
                raider.target = g.Position; raider.Face(new Point(-1, 0));
                g.enemies.Add(raider);
                var unrelated = new Projectile { position = new Point(12, 14),
                    velocity = new Point(1, 0), damage = 0, life = 3, friendly = true };
                g.projectiles.Add(unrelated);
                g.Tick(.05f, new Point());
                Projectile shot = g.projectiles.Find(p => !p.friendly);
                Check(shot != null && shot.age == 0 &&
                    (glasswind ? shot.glasswindOwned : shot.commonsRelayOwned),
                    "Actual defense archer fires a frozen-at-contact arrow with its mission's true ownership");
                var reset = typeof(GameCore).GetMethod(glasswind ?
                    "ResetGlasswind" : "ResetCommonsRelayTrial",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Check(reset != null, "The original mission cleanup exists");
                reset.Invoke(g, new object[0]);
                Check(!g.projectiles.Contains(shot) &&
                    !g.enemies.Contains(raider) &&
                    g.projectiles.Contains(unrelated),
                    "Stopping the mission retires its hostile projectile but preserves unrelated player shots");
                g.Tick(.05f, new Point());
                Check(!g.projectiles.Contains(shot) &&
                    g.projectiles.Contains(unrelated),
                    "A retired defense cannot resurrect its mid-flight arrow on the next gameplay frame");
            }
        }

        public static void SkywatchBossCancellationRetiresRealOutgoingOrbVolley(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            g.Enter(7); g.enemies.Clear(); g.projectiles.Clear(); g.effects.Clear();
            g.save.x = 18.5f; g.save.y = 14.5f;
            g.invulnerable = 100;
            var guardian = new Enemy(89701, 3, 21.5f, 14.5f, 240, true);
            guardian.state = 1; guardian.timer = .005f;
            guardian.target = g.Position; guardian.Face(new Point(-1, 0));
            g.enemies.Add(guardian);
            var unrelated = new Projectile {
                position = new Point(12, 14), velocity = new Point(1, 0),
                friendly = true, life = 3, damage = 0 };
            g.projectiles.Add(unrelated);
            g.Tick(.05f, new Point());
            Check(g.projectiles.Exists(p => p.skywatchOwned) &&
                g.projectiles.Exists(p => p.skywatchOwned && !p.friendly),
                "Real Frostchime guardian attack emits genuine mission-owned airborne hostile orbs");
            var reset = typeof(GameCore).GetMethod("ResetSkywatch",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Check(reset != null, "Observatory uses its real original cleanup");
            reset.Invoke(g, new object[0]);
            Check(!g.enemies.Contains(guardian) &&
                !g.projectiles.Exists(p => p.skywatchOwned) &&
                g.projectiles.Contains(unrelated),
                "Retiring the observatory boss removes all its outgoing orbs without deleting unrelated friendly projectiles");
        }

        public static void AtlasEchoAndRoadwardenWarningsRetireWithoutGhosts(Campaign data)
        {
            foreach (bool atlas in new[] { true, false })
            {
                var g = Fresh(data); g.enemies.Clear(); g.effects.Clear();
                g.save.x = 18.5f; g.save.y = 14.5f;
                g.invulnerable = 100;
                var unrelated = new Effect("discover", new Point(12, 14), 3);
                g.effects.Add(unrelated);
                var attacker = new Enemy(atlas ? 83001 : 81011, 2,
                    20.5f, 14.5f, 220);
                attacker.timer = 0; g.enemies.Add(attacker);
                g.Tick(.05f, new Point());
                Check(g.effects.Exists(e => e.kind == "danger" &&
                    (atlas ? e.atlasEchoOwned : e.roadTrialOwned)),
                    "Actual elite windup is tagged to its Atlas Echo or roadwarden defense owner");
                var reset = typeof(GameCore).GetMethod(atlas ?
                    "ResetAtlasEcho" : "ResetRoadwardenTrial",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Check(reset != null, "Original encounter reset is callable");
                reset.Invoke(g, new object[0]);
                Check(!g.effects.Exists(e => atlas ? e.atlasEchoOwned : e.roadTrialOwned) &&
                    g.effects.Contains(unrelated),
                    "Retiring a mission removes only its pending danger circle, never unrelated exploration effects");
            }
        }

        public static void AbortedTravelerAmbushCannotKeepRaiderArrowsAlive(Campaign data)
        {
            var g = Fresh(data);
            g.enemies.Clear(); g.projectiles.Clear(); g.effects.Clear();
            g.save.x = 18.5f; g.save.y = 14.5f; g.invulnerable = 100;
            var archer = new Enemy(82011, 1, 21.5f, 14.5f, 100);
            archer.state = 1; archer.timer = .005f;
            archer.target = g.Position; archer.Face(new Point(-1, 0));
            g.enemies.Add(archer);
            var playerShot = new Projectile { position = new Point(12, 14),
                velocity = new Point(1, 0), friendly = true, damage = 0, life = 3 };
            var ambient = new Effect("discover", new Point(12, 14), 3);
            g.projectiles.Add(playerShot); g.effects.Add(ambient);
            g.Tick(.05f, new Point());
            Projectile arrow = g.projectiles.Find(p => !p.friendly);
            Check(arrow != null && arrow.wayfarerOwned && arrow.age == 0,
                "Actual traveler raider projectile preserves its rescue mission ownership from launch");
            var cleanup = typeof(GameCore).GetMethod("ResetWayfarerEncounter",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Check(cleanup != null, "Original escort cleanup function exists");
            cleanup.Invoke(g, new object[0]);
            Check(!g.enemies.Contains(archer) &&
                !g.projectiles.Contains(arrow) &&
                g.projectiles.Contains(playerShot) && g.effects.Contains(ambient),
                "A failed ambush retires only its attackers and outgoing arrows, not unrelated player combat");
            g.Tick(.05f, new Point());
            Check(!g.projectiles.Contains(arrow) && g.projectiles.Contains(playerShot),
                "The canceled ambush cannot resurrect its hostile shot on the next gameplay frame");
        }

        public static void StaleDialogueChoicesCannotResolveWorldActionsRemotely(Campaign data)
        {
            var g = Fresh(data);
            g.save.room = 1;
            WorldObject scout = Object(g, "scout");
            g.save.kills[1] = 4;
            g.save.x = scout.x; g.save.y = scout.y;
            g.enemies.Clear();
            Check(g.CanStand(g.Position),
                "The scout's authored interaction foot is walkable");
            int coins = g.save.coins;

            g.Talk(scout);
            Check(g.mode == "dialog" && g.conversation != null,
                "A reachable scout opens the real rescue dialogue");
            int rescue = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "scout");
            Check(rescue >= 0, "The rescue dialogue exposes its authentic action");
            Point outside = StandableOutsideTalkReach(g, scout);
            g.save.x = outside.x; g.save.y = outside.y;
            g.Choose(rescue);
            Check(!g.Has("scout_saved") && g.save.coins == coins,
                "A stale dialogue cannot rescue the scout or award coins after leaving reach");

            g.save.x = scout.x; g.save.y = scout.y;
            g.Talk(scout);
            rescue = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "scout");
            Check(rescue >= 0,
                "Returning to the scout restores the legitimate rescue choice");
            g.Choose(rescue);
            Check(g.Has("scout_saved") && g.save.coins == coins + 20,
                "A fresh in-range dialogue still resolves the rescue and one-time reward");
        }

    }
}
