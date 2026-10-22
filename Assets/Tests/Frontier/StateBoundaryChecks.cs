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
                if(change>=3)
                {
                    g.Enter(0);
                    Check(PlaceWithinTalkReach(g,Object(g,"forge")),
                        "Equipment-change fixture starts at the actual forge contact point");
                }
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

        public static void StaleChainedDialogueChoicesCannotContinueRemotely(Campaign data)
        {
            var g = Fresh(data);
            WorldObject chronicle = Object(g, "worldchronicle");
            g.save.x = chronicle.x; g.save.y = chronicle.y;
            Check(g.CanStand(g.Position),
                "The public chronicle's interaction foot is walkable");

            g.Talk(chronicle);
            int section = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "story_section_origins");
            Check(section >= 0, "The chronicle opens its real section menu");
            g.Choose(section);
            Check(g.mode == "dialog" && g.conversation != null,
                "Choosing a section opens its follow-up menu");
            int page = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "story_read_origin");
            Check(page >= 0, "The unlocked origin page is available in the section");

            Point outside = StandableOutsideTalkReach(g, chronicle);
            g.save.x = outside.x; g.save.y = outside.y;
            g.Choose(page);
            Check(g.mode == "world" && g.conversation == null &&
                g.toast.Contains("交互范围"),
                "A chained menu cannot continue reading after leaving its physical source");

            g.save.x = chronicle.x; g.save.y = chronicle.y;
            g.Talk(chronicle);
            section = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "story_section_origins");
            g.Choose(section);
            page = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "story_read_origin");
            g.Choose(page);
            Check(g.mode == "dialog" && g.conversation != null,
                "The same multi-page chronicle works normally while the player remains in reach");
        }

        public static void CrossMapObjectReferencesCannotStartInteractions(Campaign data)
        {
            var g = Fresh(data);
            g.save.room = 0;
            g.mode = "world";
            WorldObject foreign = Array.Find(data.maps[1].objects,
                candidate => candidate.kind == "scout");
            Check(foreign != null,
                "The second map contains the authored scout reference");
            g.save.x = foreign.x; g.save.y = foreign.y;
            g.Talk(foreign);
            Check(g.mode == "world" && g.conversation == null,
                "A foreign-room object cannot start a dialogue at matching coordinates");

            WorldObject guild = Object(g, "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild);
            Check(g.mode == "dialog" && g.conversation != null,
                "A genuine current-map resident remains interactable");
            int routes = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "routes");
            Check(routes >= 0,
                "The guild's existing route-menu action remains available");
            g.Choose(routes);
            Check(g.mode == "map" && g.conversation == null,
                "Opening the world map is a UI action, not a fabricated cross-map interaction");
        }

        public static void RemoteWorldDialogueCannotExecuteChoices(Campaign data)
        {
            var g = Fresh(data);
            WorldObject smith = Object(g, "forge");
            Point remote = StandableOutsideTalkReach(g, smith);
            g.save.x = remote.x; g.save.y = remote.y;
            g.save.inventory[1] = 4;
            g.save.inventory[4] = 3;
            Check(!g.Has("weapon_2") && g.save.weapon == 0,
                "Remote forge test begins without an existing staff");

            g.Talk(smith);
            Check(g.mode == "dialog" && g.conversation != null,
                "A world-source dialogue may open before a choice is submitted");
            int staff = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "staff");
            Check(staff >= 0, "The real forge menu exposes its normal staff craft");
            g.Choose(staff);
            Check(g.mode == "world" && !g.Has("weapon_2") &&
                g.save.weapon == 0 && g.save.inventory[1] == 4 &&
                g.save.inventory[4] == 3 &&
                g.toast.Contains("走近"),
                "A remotely opened forge dialogue cannot craft, equip, or consume materials");

            // Leaving reach must not trap the player in a modal: the explicit
            // Close action remains available even when its world source is far.
            g.Talk(smith);
            int close = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "close");
            Check(close >= 0, "The forge menu keeps its ordinary close action");
            g.Choose(close);
            Check(g.mode == "world", "Remote dialogue can always be dismissed");

            var reachMethod = typeof(GameCore).GetMethod(
                "HasPhysicalInteractionReach",
                System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                null, new Type[] { typeof(WorldObject) }, null);
            Check(reachMethod != null,
                "Tests can query the same authoritative physical-reach predicate");
            Point[] offsets = {
                new Point(-1,0), new Point(1,0), new Point(0,-1),
                new Point(0,1), new Point(-1,-1), new Point(-1,1),
                new Point(1,-1), new Point(1,1), new Point(0.5f,0),
                new Point(-0.5f,0), new Point(0,0.5f), new Point(0,-0.5f)
            };
            bool foundNear = false;
            foreach (Point offset in offsets)
            {
                Point candidate = new Point(smith.x + offset.x,
                    smith.y + offset.y);
                if (!g.CanStand(candidate)) continue;
                g.save.x = candidate.x; g.save.y = candidate.y;
                if ((bool)reachMethod.Invoke(g, new object[] { smith }))
                { foundNear = true; break; }
            }
            Check(foundNear,
                "The authored forge has a standable, physically reachable interaction point");
            g.Talk(smith);
            staff = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "staff");
            Check(staff >= 0, "The close-range forge menu retains staff crafting");
            g.Choose(staff);
            Check(g.Has("weapon_2") && g.save.weapon == 2 &&
                g.save.inventory[1] == 0 && g.save.inventory[4] == 0,
                "The same staff craft succeeds normally inside genuine physical reach");
        }

        public static void RemoteImmediateWorldInteractionsRequireReach(Campaign data)
        {
            var home = Fresh(data);
            WorldObject portal = Array.Find(home.Map.objects,
                candidate => candidate.kind == "portal1");
            Check(portal != null, "The home route retains its authored forest portal");
            home.Talk(portal);
            Check(home.save.room == 0 && home.mode == "world" &&
                home.toast.Contains("走近"),
                "A remote portal reference cannot transition the player to another region");

            var field = Fresh(data);
            field.Enter(1);
            field.enemies.Clear();
            WorldObject herb = Array.Find(field.Map.objects,
                candidate => candidate.id == "herb1");
            WorldObject lore = Array.Find(field.Map.objects,
                candidate => candidate.id == "cache1");
            Check(herb != null && lore != null,
                "The real first-region herb and archive cache remain authored");
            Point remoteHerb = StandableOutsideTalkReach(field, herb);
            field.save.x = remoteHerb.x; field.save.y = remoteHerb.y;
            int herbs = field.save.inventory[2];
            field.Talk(herb);
            Check(!field.Has("node_" + herb.id) &&
                field.save.inventory[2] == herbs &&
                field.mode == "world" && field.toast.Contains("走近"),
                "A distant gather-node reference cannot consume the node or award herbs");

            Point remoteLore = StandableOutsideTalkReach(field, lore);
            field.save.x = remoteLore.x; field.save.y = remoteLore.y;
            int crystals = field.save.inventory[1];
            int coins = field.save.coins;
            field.Talk(lore);
            Check(!field.Has("lore_1") && !field.Has("treasure_1") &&
                field.save.inventory[1] == crystals && field.save.coins == coins &&
                field.mode == "world" && field.toast.Contains("走近"),
                "A distant archive reference cannot record or collect its one-time treasure");

            Point[] offsets = {
                new Point(-1,0), new Point(1,0), new Point(0,-1),
                new Point(0,1), new Point(-1,-1), new Point(-1,1),
                new Point(1,-1), new Point(1,1), new Point(.5f,0),
                new Point(-.5f,0), new Point(0,.5f), new Point(0,-.5f)
            };
            var reachMethod = typeof(GameCore).GetMethod(
                "HasPhysicalInteractionReach",
                System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                null, new Type[] { typeof(WorldObject) }, null);
            Check(reachMethod != null,
                "The same authoritative reach predicate remains available to runtime and tests");
            bool nearHerb = false;
            foreach (Point offset in offsets)
            {
                Point p = new Point(herb.x + offset.x, herb.y + offset.y);
                if (!field.CanStand(p)) continue;
                field.save.x = p.x; field.save.y = p.y;
                if ((bool)reachMethod.Invoke(field, new object[] { herb }))
                { nearHerb = true; break; }
            }
            Check(nearHerb, "The authored herb has a legal physical interaction point");
            field.Talk(herb);
            Check(field.Has("node_" + herb.id) &&
                field.save.inventory[2] == herbs + 3,
                "The same herb remains collectible at real physical reach");
        }

        private static bool PlaceWithinTalkReach(GameCore g, WorldObject o)
        {
            var reach = typeof(GameCore).GetMethod(
                "HasPhysicalInteractionReach",
                System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                null, new Type[] { typeof(WorldObject) }, null);
            if (reach == null) return false;
            if ((bool)reach.Invoke(g, new object[] { o })) return true;
            Point[] offsets = {
                new Point(-1,0), new Point(1,0), new Point(0,-1),
                new Point(0,1), new Point(-1,-1), new Point(-1,1),
                new Point(1,-1), new Point(1,1), new Point(.5f,0),
                new Point(-.5f,0), new Point(0,.5f), new Point(0,-.5f)
            };
            foreach (Point offset in offsets)
            {
                Point p = new Point(o.x + offset.x, o.y + offset.y);
                if (!g.CanStand(p)) continue;
                g.save.x = p.x; g.save.y = p.y;
                if ((bool)reach.Invoke(g, new object[] { o })) return true;
            }
            return false;
        }

        public static void RemoteSpecializedWorldInteractionsRequireReach(Campaign data)
        {
            var g = Fresh(data);
            g.Enter(1); g.enemies.Clear();
            g.save.flags.Add("seal_1");
            WorldObject signal = Array.Find(g.Map.objects,
                candidate => candidate.kind == "survey1");
            WorldObject cache = Array.Find(g.Map.objects,
                candidate => candidate.kind == "cache1");
            Check(signal != null && cache != null,
                "The first region retains its original survey signal and sealed supply cache");

            Point remote = StandableOutsideTalkReach(g, signal);
            g.save.x = remote.x; g.save.y = remote.y;
            int coins = g.save.coins, shards = g.save.shards;
            g.Talk(signal);
            Check(!g.Has("survey_1") && g.save.coins == coins &&
                g.save.shards == shards && g.mode == "world" &&
                g.toast.Contains("走近"),
                "A distant road-signal reference cannot claim its one-time field reward");
            Check(PlaceWithinTalkReach(g, signal),
                "The original survey signal has a legal reachable interaction point");
            g.Talk(signal);
            Check(g.Has("survey_1") && g.save.coins == coins + 12 &&
                g.save.shards == shards + 1 && g.mode == "dialog",
                "The same survey reward is earned normally at physical reach");
            g.Close();

            remote = StandableOutsideTalkReach(g, cache);
            g.save.x = remote.x; g.save.y = remote.y;
            coins = g.save.coins; shards = g.save.shards;
            int stew = g.save.inventory[6];
            g.Talk(cache);
            Check(!g.Has("cache_1") && g.save.coins == coins &&
                g.save.shards == shards && g.save.inventory[6] == stew &&
                g.mode == "world" && g.toast.Contains("走近"),
                "A distant sealed-cache reference cannot claim treasure or mastery");
            Check(PlaceWithinTalkReach(g, cache),
                "The original cache has a legal reachable interaction point");
            g.Talk(cache);
            Check(g.Has("cache_1") && g.save.coins == coins + 30 &&
                g.save.shards == shards + 3 &&
                g.save.inventory[6] == stew + 1 && g.mode == "dialog",
                "The same sealed cache still grants its authored reward at physical reach");
            g.Close();

            g.save.flags.Add("harbor_report");
            g.Enter(6); g.enemies.Clear();
            WorldObject mirror = Array.Find(g.Map.objects,
                candidate => candidate.kind == "mirror0");
            Check(mirror != null, "The desert's first authored mirror remains present");
            remote = StandableOutsideTalkReach(g, mirror);
            g.save.x = remote.x; g.save.y = remote.y;
            int before = g.save.mirrors[0];
            g.Talk(mirror);
            Check(g.save.mirrors[0] == before && g.mode == "world" &&
                g.toast.Contains("走近"),
                "A distant mirror reference cannot rotate the light puzzle");
            Check(PlaceWithinTalkReach(g, mirror),
                "The mirror has a legal reachable interaction point");
            g.Talk(mirror);
            Check(g.save.mirrors[0] == (before + 1) % 4,
                "A nearby mirror still advances exactly one real puzzle state");
        }

        public static void RemoteExpansionChoicesCannotCommitFromOutOfReachSources(Campaign data)
        {
            var envoy = Fresh(data);
            envoy.save.stage = 1;
            WorldObject envoyObject = Object(envoy, "envoy");
            Point remote = StandableOutsideTalkReach(envoy, envoyObject);
            envoy.save.x = remote.x; envoy.save.y = remote.y;
            envoy.Talk(envoyObject);
            int begin = Array.FindIndex(envoy.conversation.choices,
                choice => choice.action == "exp_begin");
            Check(begin >= 0, "The envoy exposes the real Letters from Afar start choice");
            envoy.Choose(begin);
            Check(envoy.save.room == 0 && !envoy.Has("exp_intro") &&
                envoy.mode == "world" && envoy.toast.Contains("走近"),
                "A remote envoy cannot start the expansion or teleport to Mistport");
            envoy.Talk(envoyObject);
            int projects = Array.FindIndex(envoy.conversation.choices,
                choice => choice.action == "camp");
            Check(projects >= 0, "The envoy retains its normal town-project shortcut");
            envoy.Choose(projects);
            Check(envoy.mode == "world" && envoy.toast.Contains("走近"),
                "A remote envoy cannot open a world-source project menu");

            var camp = Fresh(data);
            camp.save.room = 5; camp.enemies.Clear();
            camp.save.projects[1] = 1;
            camp.save.health = 37; camp.save.energy = 29; camp.save.totalTime = 123f;
            camp.save.tea = 1;
            WorldObject fire = Object(camp, "campfire");
            remote = StandableOutsideTalkReach(camp, fire);
            camp.save.x = remote.x; camp.save.y = remote.y;
            camp.Talk(fire);
            int rest = Array.FindIndex(camp.conversation.choices,
                choice => choice.action == "fieldrest");
            Check(rest >= 0, "A built waystation offers its ordinary safe field-rest choice");
            camp.Choose(rest);
            Check(camp.save.health == 37 && camp.save.energy == 29 &&
                camp.save.totalTime == 123f && camp.mode == "world" &&
                camp.toast.Contains("走近"),
                "A remote campfire cannot heal, refill energy or advance rest time");
            camp.Talk(fire);
            int drink = Array.FindIndex(camp.conversation.choices,
                choice => choice.action == "drinktea");
            Check(drink >= 0, "The campfire retains its ordinary drink-tea choice");
            int teaBefore = camp.save.tea;
            camp.Choose(drink);
            Check(camp.save.tea == teaBefore && camp.mode == "world" &&
                camp.toast.Contains("走近"),
                "A remote campfire cannot consume tea or grant its warming benefit");

            camp.Talk(fire);
            int companions = Array.FindIndex(camp.conversation.choices,
                choice => choice.action == "companions");
            Check(companions >= 0, "The campfire retains its companion-management shortcut");
            camp.Choose(companions);
            Check(camp.mode == "world" && camp.toast.Contains("走近"),
                "A remote campfire cannot open the companion-management menu");

            envoy.Talk(envoyObject);
            int approachThenChoose = Array.FindIndex(envoy.conversation.choices,
                choice => choice.action == "exp_begin");
            Check(approachThenChoose >= 0,
                "The same envoy dialogue can remain open while the player approaches");
            Check(PlaceWithinTalkReach(envoy, envoyObject),
                "The player can restore actual reach without discarding the open dialogue");
            envoy.Choose(approachThenChoose);
            Check(envoy.save.room == 5 && envoy.Has("exp_intro"),
                "An open remote dialogue becomes actionable only after physical reach is restored");

            var rescue = Fresh(data);
            rescue.save.room = 5; rescue.enemies.Clear();
            rescue.save.inventory[0] = 2; rescue.save.inventory[2] = 2;
            WorldObject castaway = Object(rescue, "castaway0");
            remote = StandableOutsideTalkReach(rescue, castaway);
            rescue.save.x = remote.x; rescue.save.y = remote.y;
            int timber = rescue.save.inventory[0], herbs = rescue.save.inventory[2];
            int coins = rescue.save.coins;
            rescue.Talk(castaway);
            int rescueChoice = Array.FindIndex(rescue.conversation.choices,
                choice => choice.action == "rescue0");
            Check(rescueChoice >= 0, "The stranded passenger offers the real supply-backed rescue");
            rescue.Choose(rescueChoice);
            Check(!rescue.Has("rescue_0") && rescue.save.inventory[0] == timber &&
                rescue.save.inventory[2] == herbs && rescue.save.coins == coins &&
                rescue.mode == "world" && rescue.toast.Contains("走近"),
                "A remote castaway cannot consume rescue supplies or claim a rescue");

            var sluiceGame = Fresh(data);
            sluiceGame.save.room = 5; sluiceGame.enemies.Clear();
            sluiceGame.save.inventory[1] = 3;
            WorldObject sluice = Object(sluiceGame, "sluice");
            remote = StandableOutsideTalkReach(sluiceGame, sluice);
            sluiceGame.save.x = remote.x; sluiceGame.save.y = remote.y;
            int crystals = sluiceGame.save.inventory[1];
            float tide = sluiceGame.save.tideHeldUntil;
            sluiceGame.Talk(sluice);
            int hold = Array.FindIndex(sluiceGame.conversation.choices,
                choice => choice.action == "holdtide");
            Check(hold >= 0, "The sluice exposes its normal two-crystal tide-hold choice");
            sluiceGame.Choose(hold);
            Check(sluiceGame.save.inventory[1] == crystals &&
                sluiceGame.save.tideHeldUntil == tide &&
                sluiceGame.mode == "world" && sluiceGame.toast.Contains("走近"),
                "A remote sluice cannot spend crystals or change the tide timer");

            var race = Fresh(data);
            race.save.room = 6; race.enemies.Clear();
            WorldObject skiff = Object(race, "skiff");
            remote = StandableOutsideTalkReach(race, skiff);
            race.save.x = remote.x; race.save.y = remote.y;
            race.Talk(skiff);
            int launch = Array.FindIndex(race.conversation.choices,
                choice => choice.action == "race");
            Check(launch >= 0, "The skiff retains its authored race-start option");
            race.Choose(launch);
            Check(!race.racing && race.mode == "world" &&
                race.toast.Contains("走近"),
                "A remote skiff cannot begin a race from anywhere in the desert");

            var council = Fresh(data);
            council.save.room = 5; council.enemies.Clear();
            council.save.flags.Add("harbor_report");
            council.save.flags.Add("desert_report");
            council.save.flags.Add("mountain_report");
            WorldObject councilObject = Object(council, "council");
            remote = StandableOutsideTalkReach(council, councilObject);
            council.save.x = remote.x; council.save.y = remote.y;
            int beforeCoins = council.save.coins, beforeShards = council.save.shards;
            int beforePoints = council.save.skillPoints;
            council.Talk(councilObject);
            int covenant = Array.FindIndex(council.conversation.choices,
                choice => choice.action == "covenant1");
            Check(covenant >= 0, "The completed three-region record unlocks the first governance covenant");
            council.Choose(covenant);
            Check(council.save.frontierEnding == 0 &&
                !council.Has("frontier_settled") && council.save.coins == beforeCoins &&
                council.save.shards == beforeShards && council.save.skillPoints == beforePoints &&
                council.mode == "world" && council.toast.Contains("走近"),
                "A remote council cannot settle governance or award its permanent rewards");

            var directReport = Fresh(data);
            directReport.save.room = 5; directReport.enemies.Clear();
            directReport.save.flags.Add("gate_5");
            directReport.save.flags.Add("boss_5");
            WorldObject captain = Object(directReport, "captain");
            remote = StandableOutsideTalkReach(directReport, captain);
            directReport.save.x = remote.x; directReport.save.y = remote.y;
            directReport.Talk(captain);
            int remoteReport = Array.FindIndex(directReport.conversation.choices,
                choice => choice.action == "harbor_report");
            Check(remoteReport >= 0, "A distant captain can display the authored report dialogue");
            directReport.Choose(remoteReport);
            Check(!directReport.Has("harbor_report") &&
                !directReport.Has("companion_0") && directReport.mode == "world" &&
                directReport.toast.Contains("走近"),
                "A direct remote captain dialogue cannot submit a report or recruit Mira");

            var reportShortcut = Fresh(data);
            reportShortcut.save.room = 5; reportShortcut.enemies.Clear();
            reportShortcut.save.flags.Add("gate_5");
            reportShortcut.save.flags.Add("boss_5");
            WorldObject altar = Object(reportShortcut, "expAltar");
            Check(PlaceWithinTalkReach(reportShortcut, altar),
                "The completed harbor altar retains a reachable report shortcut");
            reportShortcut.Talk(altar);
            int report = Array.FindIndex(reportShortcut.conversation.choices,
                choice => choice.action == "report_mira");
            Check(report >= 0, "The altar exposes its authored regional-report shortcut");
            reportShortcut.Choose(report);
            Check(reportShortcut.mode == "dialog" &&
                reportShortcut.conversation != null,
                "The altar shortcut opens Mira's follow-up report dialogue");
            int fileReport = Array.FindIndex(reportShortcut.conversation.choices,
                choice => choice.action == "harbor_report");
            int unrelated = Array.FindIndex(reportShortcut.conversation.choices,
                choice => choice.action == "personal0" || choice.action == "gift0" ||
                    choice.action == "join0");
            Check(fileReport >= 0 && unrelated < 0,
                "The remote report shortcut exposes only its intended report action and close");
            reportShortcut.Choose(fileReport);
            Check(reportShortcut.Has("harbor_report") &&
                reportShortcut.Has("companion_0") && reportShortcut.Has("relic_0") &&
                reportShortcut.mode == "world",
                "The one-shot report shortcut still files its report and grants authored rewards");
            reportShortcut.Talk(altar);
            Check(reportShortcut.mode == "dialog" &&
                !Array.Exists(reportShortcut.conversation.choices,
                    choice => choice.action == "report_mira") &&
                reportShortcut.conversation.text.Contains("已经归档"),
                "The altar no longer offers a useless report shortcut after filing; companion actions require visiting Mira");
        }

        public static void RemotePolishChoicesRequireLiveSourceReach(Campaign data)
        {
            var training = Fresh(data);
            WorldObject mentor = Object(training, "mentor");
            Point remote = StandableOutsideTalkReach(training, mentor);
            training.save.x = remote.x; training.save.y = remote.y;
            training.Talk(mentor);
            int practice = Array.FindIndex(training.conversation.choices,
                choice => choice.action == "practice");
            Check(practice >= 0, "The mentor retains the real practice option");
            training.Choose(practice);
            Check(!training.practicing && training.mode == "world" &&
                training.toast.Contains("走近"),
                "A remote mentor cannot start combat practice");
            training.Talk(mentor);
            practice = Array.FindIndex(training.conversation.choices,
                choice => choice.action == "practice");
            Check(PlaceWithinTalkReach(training, mentor),
                "The mentor has a genuine standable contact point");
            training.Choose(practice);
            Check(training.practicing,
                "The same practice option works after live physical reach is restored");

            var scoutGame = Fresh(data);
            scoutGame.save.room = 1;
            scoutGame.save.flags.Add("scout_saved");
            WorldObject returnedScout = Object(scoutGame, "returnscout");
            remote = StandableOutsideTalkReach(scoutGame, returnedScout);
            scoutGame.save.x = remote.x; scoutGame.save.y = remote.y;
            int stewBefore = scoutGame.save.inventory[6];
            scoutGame.Talk(returnedScout);
            int gift = Array.FindIndex(scoutGame.conversation.choices,
                choice => choice.action == "scoutgift");
            Check(gift >= 0, "The rescued scout retains the authored one-time stew gift");
            scoutGame.Choose(gift);
            Check(!scoutGame.Has("scout_gift") &&
                scoutGame.save.inventory[6] == stewBefore &&
                scoutGame.toast.Contains("走近"),
                "A remote rescued-scout dialogue cannot claim the stew reward");
            scoutGame.Talk(returnedScout);
            gift = Array.FindIndex(scoutGame.conversation.choices,
                choice => choice.action == "scoutgift");
            Check(PlaceWithinTalkReach(scoutGame, returnedScout),
                "The rescued scout has a genuine reachable interaction point");
            scoutGame.Choose(gift);
            Check(scoutGame.Has("scout_gift") &&
                scoutGame.save.inventory[6] == stewBefore + 1,
                "The stew gift remains available at physical reach");

            var chart = Fresh(data);
            chart.save.room = 9; chart.save.stage = 1;
            chart.save.flags.Add("scout_saved");
            chart.save.flags.Add("survey_1");
            chart.save.flags.Add("survey_2");
            chart.save.flags.Add("survey_3");
            WorldObject desk = Object(chart, "surveyDesk");
            remote = StandableOutsideTalkReach(chart, desk);
            chart.save.x = remote.x; chart.save.y = remote.y;
            int coins = chart.save.coins, shards = chart.save.shards;
            int points = chart.save.skillPoints;
            chart.Talk(desk);
            int finish = Array.FindIndex(chart.conversation.choices,
                choice => choice.action == "survey_finish");
            Check(finish >= 0, "The complete three-signal chart offers its real completion reward");
            chart.Choose(finish);
            Check(!chart.Has("survey_complete") && chart.save.coins == coins &&
                chart.save.shards == shards && chart.save.skillPoints == points &&
                chart.toast.Contains("走近"),
                "A remote survey desk cannot claim chart-completion rewards");
            chart.Talk(desk);
            finish = Array.FindIndex(chart.conversation.choices,
                choice => choice.action == "survey_finish");
            Check(PlaceWithinTalkReach(chart, desk),
                "The chart desk has a genuine reachable interaction point");
            chart.Choose(finish);
            Check(chart.Has("survey_complete") && chart.save.coins == coins + 60 &&
                chart.save.shards == shards + 5 && chart.save.skillPoints == points + 1,
                "The same chart reward succeeds at physical reach");

            remote = StandableOutsideTalkReach(chart, desk);
            chart.save.x = remote.x; chart.save.y = remote.y;
            chart.Talk(desk);
            int travel = Array.FindIndex(chart.conversation.choices,
                choice => choice.action == "survey_to_1");
            Check(travel >= 0, "The completed chart exposes the real regional travel option");
            chart.Choose(travel);
            Check(chart.save.room == 9 && chart.toast.Contains("走近"),
                "A remote survey desk cannot teleport the player to a beacon");
            chart.Talk(desk);
            travel = Array.FindIndex(chart.conversation.choices,
                choice => choice.action == "survey_to_1");
            Check(PlaceWithinTalkReach(chart, desk),
                "The same chart can be approached without losing its open menu");
            chart.Choose(travel);
            Check(chart.save.room == 1 && chart.CanStand(chart.Position),
                "The chart travel option still lands on valid walkable ground when reached");
        }

    }
}
