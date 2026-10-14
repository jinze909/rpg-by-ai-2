using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static float OpeningStrike(Campaign data, float step, bool boss, Point facing, float recovery, out GameCore game, out Enemy target)
        {
            game = Quiet(data);
            game.save.seed = 1; game.save.english = true;
            target = new Enemy(88001, boss ? 0 : 2, 13, 14, 900, boss)
            { facing = facing, timer = 999, releaseTime = recovery };
            game.enemies.Add(target);
            game.Attack(new Point(1, 0));
            for (int frame = 0; frame < 60 && target.hp >= 900; frame++)
                game.Tick(step, new Point());
            Check(target.hp < 900, "Real sword contact must hit the production enemy");
            return 900 - target.hp;
        }

        public static void TimedRecoveryAndArmoredFlanksCreateCombatOpenings(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, .05f })
            {
                GameCore normalGame, flankGame, guardedGame, bossGame, openGame, staleGame;
                Enemy normal, flank, guarded, boss, opening, stale;
                float neutral = OpeningStrike(data, step, false, new Point(0, 1), 0, out normalGame, out normal);
                float rear = OpeningStrike(data, step, false, new Point(1, 0), 0, out flankGame, out flank);
                float front = OpeningStrike(data, step, false, new Point(-1, 0), 0, out guardedGame, out guarded);
                Check(rear > neutral * 1.24f && rear < neutral * 1.27f && front < neutral * .42f,
                    "Heavy armor must reward a rear flank without weakening frontal guard");
                Check(flank.stagger >= normal.stagger + 5.9f && flankGame.effects.Exists(e => e.text == "FLANK"),
                    "Flanking adds poise pressure and player feedback");
                float ordinaryBoss = OpeningStrike(data, step, true, new Point(0, 1), 0, out bossGame, out boss);
                float recoveryBoss = OpeningStrike(data, step, true, new Point(0, 1), .5f, out openGame, out opening);
                float expiredBoss = OpeningStrike(data, step, true, new Point(0, 1), .04f, out staleGame, out stale);
                Check(recoveryBoss > ordinaryBoss * 1.24f && recoveryBoss < ordinaryBoss * 1.27f &&
                    Math.Abs(expiredBoss - ordinaryBoss) < .001f,
                    "Only a contact during live post-attack recovery earns the opening");
                Check(openGame.effects.Exists(e => e.text == "OPEN") &&
                    !bossGame.effects.Exists(e => e.text == "OPEN") &&
                    !staleGame.effects.Exists(e => e.text == "OPEN"),
                    "Opening feedback is never awarded to stale contacts");
            }
        }

        public static void HeldAttacksPreserveSkillContactPoses(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f}) for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession);g.Skill(new Point(1,0));ActorAction skill=g.playerAction;
                bool sawContact=false,sawFollowup=false;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(step,new Point());bool active=g.playerAction==skill&&g.PlayerPhase==ActionPhase.Active;
                    g.Attack(new Point(0,-1)); // Production input samples held attack after Tick.
                    if(active)
                    {
                        sawContact=true;
                        Check(g.playerAction==skill&&g.PlayerPose==7,"Held attack cannot replace Q's visible contact for profession "+profession+" at timestep "+step);
                    }
                    if(g.playerAction!=skill)
                    {
                        Check(skill.released&&skill.elapsed+.00001f>=skill.contact+.1f,"A followup waits for the complete skill contact interval");
                        sawFollowup=g.playerAction!=null&&g.playerAction.kind=="attack";break;
                    }
                }
                Check(sawContact&&sawFollowup,"Q shows its release pose, then continuous attack resumes without another button press");
            }
            var sameFrame=Quiet(data);sameFrame.Skill(new Point(0,-1));sameFrame.Attack(new Point(1,0));
            Check(sameFrame.playerAction.kind=="skill"&&sameFrame.save.energy==60,"Discrete Q has priority over the held attack sampled in the same frame");
        }
        public static void BufferedSkillsRetainIntentAndJoinRecovery(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f}) for(int profession=0;profession<3;profession++) foreach(bool afterContact in new[]{false,true})
            {
                var g=Quiet(data,profession);g.Attack(new Point(1,0));ActorAction attack=g.playerAction;
                Advance(g,attack.contact+(afterContact?.02f:-.04f));
                Point target=new Point(21,12),direction=(target-g.Position).Normalized;
                g.SetAimTarget(target);g.Skill(direction);
                Check(g.playerAction==attack&&g.skillCooldown<=0&&g.save.energy==100,"Queuing Q during preparation or contact cannot cancel the visible attack or spend energy early");
                g.SetAimTarget(new Point(5,18));g.SetAim(new Point(-1,0));g.save.talents[0]++;
                ActorAction skill=null;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(step,new Point(),new Point(-1,0));g.Attack(new Point(-1,0));
                    if(g.playerAction!=attack)
                    {
                        skill=g.playerAction;
                        Check(attack.elapsed+.00001f>=attack.contact+.1f,"A buffered Q preserves the preceding attack's complete contact interval");break;
                    }
                }
                Check(skill!=null&&skill.kind=="skill","One discrete Q near contact starts at recovery despite continuously held attack");
                Check((skill.direction-direction).Length<.001f&&skill.target.HasValue&&(skill.target.Value-target).Length<.001f,"Buffered Q keeps the original direction and pointer target");
                Check(Math.Abs(skill.damage-g.Damage)<.001f&&Math.Abs(skill.impact.criticalChance-.085f)<.001f,"Combat values are captured when Q actually starts, after the queued intent");
                Check(Math.Abs(g.save.energy-60)<.001f&&Math.Abs(g.skillCooldown-3.5f)<.001f,"Buffered Q pays its original cost and starts cooldown exactly once");
                if(profession==1)
                {
                    while(!skill.released)g.Tick(.01f,new Point(),new Point(-1,0));
                    var volley=g.projectiles.FindAll(p=>p.technique);
                    Point expected=(target-GameCore.WeaponOrigin(g.Position,1,direction)).Normalized;
                    Check(volley.Count==5&&(volley[2].velocity.Normalized-expected).Length<.001f,"The actual queued volley uses the retained target and authored muzzle");
                }
            }
            var latest=Quiet(data,1);latest.Dash(new Point(1,0));latest.Skill(new Point(0,-1));
            Point lastTarget=new Point(20,16);latest.SetAimTarget(lastTarget);latest.Skill(new Point(0,1));
            Advance(latest,.2f);
            Check(latest.playerAction!=null&&latest.playerAction.kind=="skill"&&latest.playerAction.direction.y==1&&latest.playerAction.target.HasValue&&(latest.playerAction.target.Value-lastTarget).Length<.001f,"Repeated Q replaces one pending intent with the latest direction and target");
            Advance(latest,4.5f);Check(latest.playerAction==null&&latest.skillCooldown<=0,"A consumed input cannot recast after cooldown");
        }
        public static void BufferedSkillsHaveBoundedLifetimeAndResourceChecks(Campaign data)
        {
            var early=Quiet(data,2);early.Attack(new Point(1,0));early.Skill(new Point(0,-1));Advance(early,1);
            Check(early.skillCooldown<=0&&early.save.energy==100&&!early.effects.Exists(e=>e.kind=="burst"),"Q pressed at the start of long staff preparation expires instead of autocasting much later");
            var frozen=Quiet(data,1);frozen.hitStop=.5f;frozen.Skill(new Point(0,-1));
            for(int frame=0;frame<9;frame++)frozen.Tick(.05f,new Point());
            Check(frozen.playerAction==null&&frozen.skillCooldown==0&&frozen.save.energy==100,"Hitstop freezes a ready Q intent without spending or advancing its short lifetime");
            for(int frame=0;frame<5&&frozen.playerAction==null;frame++)frozen.Tick(.05f,new Point());
            Check(frozen.playerAction!=null&&frozen.playerAction.kind=="skill"&&frozen.save.energy==60,"The retained Q starts once when hitstop ends");
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                var dodge=Quiet(data);dodge.Dash(new Point(1,0));dodge.Skill(new Point(0,-1));
                for(int frame=0;frame<30&&dodge.playerAction==null;frame++)dodge.Tick(step,new Point());
                Check(dodge.dashTime==0&&dodge.playerAction!=null&&dodge.playerAction.kind=="skill"&&Math.Abs(dodge.save.energy-42.8f)<1,"Same-frame Dash then Q waits for the dodge and spends once at timestep "+step);
            }
            var cancelled=Quiet(data);cancelled.Attack(new Point(1,0));Advance(cancelled,.12f);cancelled.Skill();cancelled.Dash(new Point(1,0));Advance(cancelled,.5f);
            Check(cancelled.skillCooldown<=0&&cancelled.playerAction==null,"A new dodge clears the earlier queued Q");
            foreach(bool cooldownFailure in new[]{false,true})
            {
                var rejected=Quiet(data);rejected.Attack(new Point(1,0));Advance(rejected,.12f);
                rejected.skillCooldown=cooldownFailure?.01f:0;rejected.save.energy=cooldownFailure?100:39;rejected.toast="";float energy=rejected.save.energy;
                rejected.Skill();Check(rejected.save.energy==energy&&rejected.toast.Length>0,"Unavailable Q gives feedback and does not spend while an attack blocks it");
                Advance(rejected,.7f);
                Check(rejected.skillCooldown<=0&&!rejected.effects.Exists(e=>e.kind=="burst"),"Cooldown expiry or later energy regeneration cannot revive a rejected Q");
            }
            var spent=Quiet(data);spent.Attack(new Point(1,0));Advance(spent,.12f);spent.Skill();spent.save.energy=0;Advance(spent,.5f);
            Check(spent.skillCooldown<=0&&!spent.effects.Exists(e=>e.kind=="burst"),"Buffered Q rechecks affordability at dispatch after other spending");
        }
        public static void BufferedSkillsRespectVariableFrameDeadlines(Campaign data)
        {
            float[][] timesteps={new[]{.033f},new[]{.016f,.033f,.05f},new[]{.01f}};
            for(int cadence=0;cadence<timesteps.Length;cadence++) foreach(bool withinWindow in new[]{true,false})
            {
                var g=Quiet(data,2);g.Attack(new Point(1,0));
                // Staff recovery starts at .379 seconds. .18 leaves .199,
                // while .17 leaves .209 and must outlive the .20 input window.
                Advance(g,withinWindow?.18f:.17f);g.Skill(new Point(0,-1));bool cast=false;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(timesteps[cadence][frame%timesteps[cadence].Length],new Point());
                    if(g.playerAction!=null&&g.playerAction.kind=="skill") {cast=true;break;}
                }
                Check(cast==withinWindow,"Buffered Q deadline follows the actual readiness time within a variable frame, cadence "+cadence+" eligible "+withinWindow);
                if(cast)Check(g.save.energy==60&&g.skillCooldown==3.5f,"Crossing the deadline in a long frame still dispatches the eligible Q exactly once");
                else Check(g.skillCooldown<=0&&g.save.energy==100,"A readiness time beyond the window cannot gain extra grace from a long frame");
            }
            var boundary=Quiet(data,2);boundary.Attack(new Point(1,0));Advance(boundary,.179f);boundary.Skill(new Point(0,-1));
            for(int frame=0;frame<20;frame++)boundary.Tick(.01f,new Point());
            Check(boundary.playerAction!=null&&boundary.playerAction.kind=="skill"&&boundary.save.energy==60,"An exact .20-second deadline tolerates accumulated .01-step float residue without dropping Q");
        }
        public static void BufferedSkillsClearAtWorldAndProfileBoundaries(Campaign data)
        {
            System.Action<GameCore>[] transitions={
                g=>{g.mode="pause";g.Close();},
                g=>{g.mode="dialog";g.Close();},
                g=>g.Equip(0),
                g=>{g.save.weaponStyles[1]=1;},
                g=>{g.save.technique=1;},
                g=>{g.save.profession=2;},
                g=>g.Enter(0),
                g=>{g.PrepareSave();g.Load(CloneSave(g.save));},
                g=>{g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());Check(g.Dying,"Boundary test takes an actual lethal contact");}
            };
            for(int boundary=0;boundary<transitions.Length;boundary++)
            {
                var g=Quiet(data,1);g.Attack(new Point(1,0));Advance(g,.2f);g.Skill(new Point(0,-1));
                Check(g.skillCooldown<=0&&g.save.energy==100,"Boundary scenario first has an unspent short Q intent");
                transitions[boundary](g);Advance(g,1.3f);
                Check(g.skillCooldown<=0&&g.playerAction==null,"Menu, equipment, profile, travel, load or death cannot release a stale Q, boundary "+boundary);
                Check(!g.projectiles.Exists(p=>p.technique),"No queued volley escapes into another profile or world, boundary "+boundary);
            }
        }
        public static void EffectOriginsAndSources(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; g.Skill();
                Check(g.effects.Count == 0 && g.projectiles.Count == 0, "Preparing Q cannot create an early effect or projectile");
                Advance(g, .3f);
                if (profession == 1)
                    Check(g.projectiles.Count == 5 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.Position,1,g.aim)).Length < .01f) && !g.effects.Exists(e => e.kind == "burst"), "Archer volley releases from its bow, with no unrelated ground explosion");
                else
                {
                    Effect burst = g.effects.Find(e => e.kind == "burst");
                    Check(burst != null && burst.variant == profession && (burst.position-g.Position).Length < .01f && Math.Abs(burst.radius-(profession == 0 ? 3.1f : 4.5f)) < .001f, "AoE carries its actual class, origin and unchanged damage radius to the renderer");
                }
            }
            var guard = Fresh(data); guard.Enter(1); guard.enemies.Clear(); guard.save.technique = 1; guard.Skill(); Advance(guard,.3f);
            Check(guard.effects.Exists(e => e.kind == "burst" && e.variant == 0 && e.radius == 3.8f), "Alternate guard skill retains its own AoE size");
            var marksman = Fresh(data,1); marksman.Enter(1); marksman.enemies.Clear(); marksman.save.technique = 1; marksman.Skill(); Advance(marksman,.3f);
            Check(marksman.projectiles.Count == 1 && marksman.effects.Exists(e => e.kind == "release") && !marksman.effects.Exists(e => e.kind == "burst"), "Alternate shot has a projectile release rather than a class-independent explosion");

            var killer = Fresh(data,2); killer.Enter(1); killer.enemies.Clear(); killer.save.reducedMotion = true;
            killer.save.x = 12; killer.save.y = 14; var foe = new Enemy(931,0,14,14,1) {timer=9999}; killer.enemies.Add(foe);
            killer.projectiles.Add(new Projectile {position=foe.Position,friendly=true,visual="arrow",damage=50}); killer.Tick(.01f,new Point());
            Check(killer.effects.Exists(e => e.kind == "defeat" && (e.position-foe.Position).Length < .01f) && !killer.effects.Exists(e => e.kind == "burst"), "Enemy death remains at the enemy and cannot impersonate the player's vocation skill");
            Check(killer.fallen.Count == 1 && killer.effects.FindAll(e => e.kind == "loot").Count == 1, "Death art still awards a corpse and loot once");

            for (int companion = 0; companion < 3; companion++)
            {
                var g = Fresh(data,2); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.x = 12; g.save.y = 14;
                g.save.flags.Add("companion_" + companion); g.SelectCompanion(companion); g.save.health = 20; g.CommandCompanion(); Advance(g,.22f);
                Check(!g.effects.Exists(e => e.kind == "burst"), "Companion command cannot borrow the mage player's attack explosion");
                if (companion == 0) Check(g.save.health == 38 && g.effects.Exists(e => e.kind == "heal" && (e.position-g.Position).Length < .01f), "Mira heals with a healing effect at the recipient");
                if (companion == 1) Check(g.projectiles.Count == 3 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.companionPosition,1,g.companionAim)).Length < .01f), "Ivo's volley releases from Ivo's bow");
                if (companion == 2) Check(g.effects.Exists(e => e.kind == "frost" && (e.position-g.Position).Length < .01f), "Sen's field retains its gameplay center");
            }
            var magic = Fresh(data,2); magic.Enter(1); magic.enemies.Clear(); magic.save.reducedMotion = true; magic.save.x = 12; magic.save.y = 14;
            Point collision = new Point(14,14); magic.enemies.Add(new Enemy(932,0,14,14,999) {stun=20,timer=9999});
            magic.projectiles.Add(new Projectile {position=collision,friendly=true,explosive=true,visual="starfire",damage=20}); magic.Tick(.01f,new Point());
            Check(magic.projectiles.Count == 0 && magic.effects.FindAll(e => e.kind == "magicImpact").Count == 1 && !magic.effects.Exists(e => e.kind == "burst"), "Starfire collision emits exactly one impact, without a duplicate class explosion");
            Effect impact = magic.effects.Find(e => e.kind == "magicImpact"); Check((impact.position-collision).Length < .01f && impact.radius == 1.8f, "Explosive impact carries the actual collision and splash footprint");
            var dodge = Fresh(data); dodge.Dash(new Point(0,-1)); Check(dodge.effects.Exists(e => e.kind == "dash" && e.direction.y == -1 && e.direction.x == 0), "Dodge trail remembers the actual dodge direction");
        }
        public static void InnRoomAndCollisionPersistence(Campaign data)
        {
            var g = Fresh(data); int stage = g.save.stage;
            Check(data.maps.Length == 10 && data.maps[9].id == 9, "Inn is appended without renumbering existing regions");
            WorldObject door = Object(g, "portal9"); g.save.x = door.x; g.save.y = door.y; g.Interact();
            Check(g.save.room == 9 && g.enemies.Count == 0 && !g.Blocked(g.save.x, g.save.y), "The actual town door enters a safe, navigable interior");
            Check(g.Blocked(15.5f, 8.5f) && !g.Blocked(15.5f, 10.5f), "Inn furniture blocks its footprint while the aisle remains open");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.save.room == 9 && loaded.save.stage == stage, "Interior saves restore without advancing or resetting quests");
            g.save.health = 10; Action(g, "rest", "rest");
            Check(g.save.health == g.save.maxHealth, "Interior rest uses the existing recovery transaction");
            g.Close(); g.Enter(9); Action(g, "history16", "close"); Check(g.Has("history_16"), "Inn ledger is recorded in the existing codex");
            g.Close(); WorldObject exit = Object(g, "portal0"); g.save.x = exit.x; g.save.y = exit.y; g.Interact();
            Check(g.save.room == 0 && g.save.stage == stage, "The inn exit returns to the same town journey");
        }
        public static void ContactFramesAndCancellation(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var g = Fresh(data, weapon); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; var foe = new Enemy(41, 0, weapon == 0 ? 13 : 17, 14, 999) { timer = 9999 }; g.enemies.Add(foe);
                g.Attack(new Point(1, 0)); var action = g.playerAction;
                Check(action != null && g.PlayerPose == 4 && foe.hp == 999 && g.projectiles.Count == 0, "Input starts wind-up without early damage or missiles");
                Advance(g, action.contact - .01f); Check(!action.released && foe.hp == 999 && g.projectiles.Count == 0, "No contact before the actual release boundary");
                g.mode = "pause"; Advance(g, 1); Check(!action.released, "Pausing freezes the action and its damage"); g.Close();
                g.Tick(.02f, new Point()); Check(action.released && g.PlayerPose == 5, "Contact and release pose share the same clock");
                if (weapon == 0) { float hp = foe.hp; Check(hp < 999, "Sword contacts at the strike frame"); Advance(g, .1f); Check(foe.hp == hp, "A swing cannot repeatedly damage the same enemy"); }
                else Check(g.projectiles.Count > 0, "Ranged weapons spawn only at their release frame");
                g.Enter(0); g.Close(); g.attackCooldown = 0; g.Attack(new Point(1, 0));
                // Weapon 1 is already equipped for the ranger. Exercise a
                // genuine loadout change, not re-selecting the same icon.
                int alternateWeapon = weapon == 1 ? 0 : 1;
                g.save.flags.Add("weapon_1"); g.Equip(alternateWeapon); Advance(g, .8f);
                Check(g.playerAction == null && g.projectiles.Count == 0, "Changing to a different weapon cancels the stored attack");
                g.Attack(new Point(1, 0)); g.Enter(1); Advance(g, .8f); Check(g.projectiles.Count == 0, "Travel does not release an old attack in the new region");
            }
            var caster = Fresh(data, 2); caster.Enter(1); caster.enemies.Clear(); caster.save.reducedMotion = true;
            caster.save.x = 12; caster.save.y = 14; var target = new Enemy(50, 0, 13, 14, 999) { timer = 9999 }; caster.enemies.Add(target);
            caster.Skill(); Check(target.hp == 999 && caster.PlayerPose == 6, "Q first channels visibly"); Advance(caster, .3f);
            Check(target.hp < 999 && caster.PlayerPose == 7, "Q damage follows its release pose");
            var struck = Fresh(data); struck.Enter(1); struck.enemies.Clear(); struck.save.x = 12; struck.save.y = 14; struck.save.reducedMotion = true;
            var foeHit = new Enemy(51, 0, 13, 14, 999) { timer = 9999 }; struck.enemies.Add(foeHit);
            struck.Attack(new Point(1, 0)); struck.Tick(.04f, new Point());
            struck.invulnerable = 0; struck.projectiles.Add(new Projectile { position = struck.Position, damage = 5, source = "shot" }); struck.Tick(.01f, new Point());
            Advance(struck, .1f); Check(foeHit.hp == 999 && struck.PlayerPose == 10 && !struck.playerAction.released, "Recoil cannot conceal a pending damage event");
            Advance(struck, .25f); Check(foeHit.hp < 999 && struck.playerAction.released && struck.PlayerPose == 5, "After recoil the visible swing resumes before contact");
        }
        public static void ProjectilesAndEnemyContact(Campaign data)
        {
            var g = Fresh(data, 1); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 12; g.save.y = 14; var archer = new Enemy(60, 1, 17, 14, 999) { timer = 9999 }; g.enemies.Add(archer);
            g.Attack(new Point(1, 0)); Advance(g, .26f); Check(g.projectiles.Count == 1 && archer.hp == 999, "An arrow has a visible flight before a distant hit");
            float first = g.projectiles[0].position.x; Advance(g, .12f); Check(g.projectiles[0].position.x > first && archer.hp == 999, "The arrow travels instead of applying instant ranged damage");
            for (int i = 0; i < 50 && archer.hp == 999; i++) g.Tick(.02f, new Point()); Check(archer.hp < 999 && g.effects.Exists(e => e.kind == "projectileImpact"), "Arrow collision gives damage and a disappearance effect");
            g.projectiles.Clear(); g.invulnerable = 0; var heavy = new Enemy(61, 2, 13, 14, 999) { state = 1, timer = .4f, windupDuration = .4f, target = g.Position }; g.enemies.Clear(); g.enemies.Add(heavy);
            float health = g.save.health; Advance(g, .38f); Check(g.save.health == health && heavy.state == 1, "Readable enemy preparation cannot damage early");
            g.Tick(.03f, new Point()); Check(g.save.health < health && heavy.releaseTime > 0 && g.PlayerPose == 10, "Enemy contact triggers the strike and player recoil together");
        }
        public static void DeathPresentationAndResume(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.health = 1; g.invulnerable = 0;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point());
            Check(g.Dying && g.save.room == 1 && g.PlayerPose == 11, "The player falls before being returned home");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); Check(resumed.save.room == 0 && resumed.save.health == resumed.save.maxHealth && resumed.save.deaths == 1, "Saving during death safely resumes at the inn without charging death twice");
            Advance(g, 1); Check(!g.Dying && g.save.room == 0 && g.save.deaths == 1, "Death completion restores control exactly once");
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14; var foe = new Enemy(71, 0, 13, 14, 1) { timer = 9999 }; g.enemies.Add(foe);
            int kills = g.save.kills[1]; g.Attack(new Point(1, 0)); Advance(g, .2f);
            Check(g.enemies.Count == 0 && g.fallen.Count == 1 && g.save.kills[1] == kills + 1, "Death removes combat participation but preserves a visible corpse");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); Check(loaded.enemies.Count == 0, "A visual corpse cannot respawn as a living loot source");
            Advance(g, 1); Check(g.fallen.Count == 0 && g.save.kills[1] == kills + 1, "The corpse fades without awarding twice");
        }
        public static void PhysicalAttacksAndDodgeRespectWalls(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 9.5f; g.save.y = 8.8f;
            var foe = new Enemy(90, 0, 9.5f, 10.2f, 999) { timer = 9999 }; g.enemies.Add(foe);
            Check(!g.Blocked(g.save.x, g.save.y) && !g.Blocked(foe.x, foe.y) && g.Blocked(9.5f, 9.5f), "A real forest wall separates two otherwise valid positions");
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(foe.hp == 999, "A visible sword strike cannot damage through an intervening wall");
            foe.state = 1; foe.timer = .04f; foe.target = g.Position; foe.facing = new Point(0, -1);
            float health = g.save.health; Advance(g, .06f);
            Check(g.save.health == health && foe.releaseTime > 0, "A creature can finish its lunge without biting through stone");

            g.Enter(0); g.enemies.Clear(); g.save.x = 7; g.save.y = 6.3f;
            g.Dash(new Point(0, -1));
            for (int i = 0; i < 5; i++)
            {
                g.Tick(.05f, new Point());
                Check(g.save.y >= 5.97f && !g.Blocked(g.save.x, g.save.y), "A fast dodge stops outside the unchanged house footprint");
            }
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14;
            var open = new Enemy(91, 0, 12, 15.3f, 999) { timer = 9999 }; g.enemies.Add(open);
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(open.hp < 999, "The obstruction fix preserves normal open-lane melee contacts");
        }
        public static void CompanionPreparationAndSafeFollowing(Campaign data)
        {
            for (int id = 0; id < 3; id++)
            {
                var g = Fresh(data); g.save.flags.Add("companion_" + id); g.SelectCompanion(id);
                Check((g.companionPosition - g.Position).Length > 1.4f && g.CanStand(g.companionPosition), "A companion starts beside the hero on a valid footprint");
                g.save.health = 20; g.aim = new Point(0, 1); g.CommandCompanion();
                var action = g.companionAction; Check(action != null && !action.released && g.projectiles.Count == 0 && g.save.health == 20, "Companion command prepares before applying its effect");
                g.mode = "pause"; Advance(g, 1); Check(action.elapsed == 0, "Companion casting pauses with the same world clock"); g.Close();
                Advance(g, .22f); Check(action.released, "Companion pose and command release use the same boundary");
                if (id == 0) Check(g.save.health == 38, "Mira retains the original 18 HP command strength");
                if (id == 1) Check(g.projectiles.Count == 3 && g.projectiles.TrueForAll(p => p.velocity.y > 0 && p.piercing), "Ivo's actual volley flies in the locked south direction");
                if (id == 2) Check(g.effects.Exists(e => e.kind == "frost"), "Sen's command starts its frost field on release");
                g.Enter(1); Check(g.companionAction == null && g.CanStand(g.companionPosition), "Travel cancels a previous companion action and restores safe following");
            }
        }
        public static void ShortHitStopReleasesUnusedFrameTime(Campaign data)
        {
            var reference=Fresh(data);
            var stopped=Fresh(data);
            reference.enemies.Clear();stopped.enemies.Clear();
            reference.save.x=stopped.save.x=12;
            reference.save.y=stopped.save.y=14;
            stopped.hitStop=.012f;
            float before=stopped.save.totalTime;
            reference.Tick(.038f,new Point(1,0));
            stopped.Tick(.05f,new Point(1,0));
            Check(Math.Abs(stopped.save.totalTime-before-.038f)<.0002f&&
                  Math.Abs(stopped.save.x-reference.save.x)<.001f&&
                  Math.Abs(stopped.save.y-reference.save.y)<.001f&&stopped.hitStop==0,
                "A 12ms impact freeze releases the unused 38ms for normal movement and world time");
            var multi=Fresh(data);
            reference=Fresh(data);
            multi.enemies.Clear();reference.enemies.Clear();
            multi.save.x=reference.save.x=12;multi.save.y=reference.save.y=14;
            multi.hitStop=.07f;
            before=multi.save.totalTime;
            multi.Tick(.05f,new Point(1,0));
            Check(Math.Abs(multi.save.totalTime-before)<.00001f&&multi.save.x==12&&
                  Math.Abs(multi.hitStop-.02f)<.00001f,
                "A longer hit-stop completely freezes its first frame and retains exactly the remainder");
            multi.Tick(.05f,new Point(1,0));
            reference.Tick(.03f,new Point(1,0));
            Check(Math.Abs(multi.save.totalTime-before-.03f)<.0002f&&
                  Math.Abs(multi.save.x-reference.save.x)<.001f&&multi.hitStop==0,
                "The next frame uses only the un-frozen 30ms; damage feedback never grants a free slow-motion penalty");
        }


        public static void BossFinaleCannotInterruptDeathRecovery(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;
            g.Enter(4);g.enemies.Clear();
            g.save.flags.Add("boss_4");
            var delay=typeof(GameCore).GetField("finaleDelay",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            delay.SetValue(g,.025f);
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            g.save.health=1;g.invulnerable=0;
            hurt.Invoke(g,new object[]{999f,"shot",null});
            Check(g.Dying && g.mode=="world",
                "Real lethal damage enters the death state with a pending final-boss epilogue");
            g.Tick(.05f,new Point());
            Check(g.Dying && g.mode=="world" && (float)delay.GetValue(g)>.001f,
                "Boss finale must not open dialogue while the death clock is active");
            for(int i=0;i<28&&g.Dying;i++)g.Tick(.05f,new Point());
            Check(!g.Dying&&g.save.room==0&&g.mode=="world"&&g.save.health>0,
                "The player always reaches a safe town respawn even when the epilogue delay was pending");
            Check((float)delay.GetValue(g)==0,
                "Dead-player finale delay is retired on respawn, not fired inside the old region");
        }

        public static void EquippingIdenticalWeaponCannotEraseActiveAttack(Campaign data)
        {
            var g=Fresh(data);
            g.enemies.Clear();
            g.save.energy=100;
            g.Attack(new Point(1,0));
            ActorAction ongoing=g.playerAction;
            Check(ongoing!=null&&ongoing.kind=="attack",
                "An authentic sword attack starts its visible windup");
            int beforeRevision=g.revision, originalWeapon=g.save.weapon;
            g.Equip(originalWeapon);
            Check(g.playerAction==ongoing&&g.revision==beforeRevision,
                "Re-selecting the same equipped weapon preserves the prepared attack and does not dirty the save");
            for(int i=0;i<8;i++)g.Tick(.05f,new Point());
            Check(ongoing.released,
                "Real attack contact still completes after same-weapon selection");
            // Dash offers a deterministic real input-buffer window independent
            // of weapon windup frame boundaries.
            var queued=Fresh(data);
            queued.save.energy=100;queued.enemies.Clear();
            queued.Dash(new Point(1,0));
            queued.Skill(new Point(0,1));
            Check(queued.playerAction==null&&queued.skillCooldown==0&&queued.dashTime>0,
                "A real dodge buffers Q without starting or paying for its skill yet");
            int queuedRevision=queued.revision;
            queued.Equip(queued.save.weapon);
            Check(queued.revision==queuedRevision&&queued.skillCooldown==0,
                "Same-weapon selection preserves buffered Q through the dodge");
            bool cast=false;
            for(int i=0;i<16;i++)
            {
                queued.Tick(.05f,new Point());
                if(queued.playerAction!=null&&queued.playerAction.kind=="skill")
                    cast=true;
            }
            Check(cast&&queued.skillCooldown>0,
                "The queued class technique still executes exactly once after the real dodge");
            // An actual loadout change must continue to cancel both old
            // windups and buffered intent; it is not an idempotent click.
            var changed=Fresh(data);
            changed.save.flags.Add("weapon_1");
            changed.save.energy=100;
            changed.Attack(new Point(1,0));
            Check(changed.playerAction!=null,"Old loadout has an attack in preparation");
            changed.Equip(1);
            Check(changed.save.weapon==1&&changed.playerAction==null,
                "Genuine weapon swap retires the old sword windup");
            var canceled=Fresh(data);
            canceled.save.flags.Add("weapon_1");
            canceled.save.energy=100;
            canceled.Dash(new Point(1,0));
            canceled.Skill(new Point(0,1));
            canceled.Equip(1);
            for(int i=0;i<16;i++)canceled.Tick(.05f,new Point());
            Check(canceled.save.weapon==1&&canceled.playerAction==null&&
                  canceled.skillCooldown<=0,
                "Genuine equipment swap discards previously buffered dodge Q rather than replaying it");
        }

        public static void SwitchingSaveSlotsDoesNotInheritOldAimOrFishingState(Campaign data)
        {
            var g=Fresh(data,1);
            g.enemies.Clear();
            g.SetAim(new Point(0,-1));
            g.SetAimTarget(new Point(77,16));
            g.fishTime=3.2f;g.fishCursor=.72f;g.fishHits=2;g.fishMisses=1;
            g.Notify("旧旅程的消息","Prior journey notification");
            g.sound="attack";g.sound="hurt";
            Check(g.toastTime>0&&g.sounds.Count>=2,
                "Prior journey has a pending notification and authentic queued combat audio");
            SaveData replacement=new SaveData();
            replacement.flags.Add("intro");
            g.Load(replacement);g.Start();
            Check((g.aim-new Point(1,0)).Length<.001f&&
                  g.fishTime==0&&g.fishCursor==0&&g.fishHits==0&&g.fishMisses==0&&
                  g.toastTime==0&&g.toast==""&&g.sounds.Count==0&&g.sound=="",
                "A loaded slot starts without inherited aiming, fishing, HUD or combat audio state");
            g.sound="travel";
            Check(g.sounds.Count==1,
                "New journey audio continues to enqueue its own distinct travel cue");
            g.sounds.Clear();
            g.Skill();
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                  (g.playerAction.direction-new Point(1,0)).Length<.001f&&
                  !g.playerAction.target.HasValue,
                "The newly loaded hero's first authentic skill cannot reuse a prior slot's cursor target");
            g.SetAimTarget(new Point(23,20));g.SetAim(new Point(-1,0));
            g.NewGame(2);g.Close();g.Skill();
            Check(g.playerAction!=null&&
                  (g.playerAction.direction-new Point(1,0)).Length<.001f&&
                  !g.playerAction.target.HasValue,
                "Starting a new class also clears the previous hero's targeting state");
        }

        public static void PiercingVolleysKeepEveryContactWithReusableTargetSnapshot(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            var front=new Enemy(6671,0,12.5f,14,20){stun=99,timer=999};
            var middle=new Enemy(6672,0,13.35f,14,20){stun=99,timer=999};
            var rear=new Enemy(6673,0,14.7f,14,999){stun=99,timer=999};
            g.enemies.Add(front);g.enemies.Add(middle);g.enemies.Add(rear);
            var arrow=new Projectile { position=new Point(12,14),
                velocity=new Point(20,0),life=3,damage=60,
                friendly=true,piercing=true,visual="arrow" };
            g.projectiles.Add(arrow);
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(!g.enemies.Contains(front)&&!g.enemies.Contains(middle)&&
                  g.enemies.Contains(rear)&&rear.hp<999,
                "Piercing sweep still kills both front targets and damages the survivor across multiple substeps");
            Check(arrow.hits.Contains(front.id)&&arrow.hits.Contains(middle.id)&&
                  arrow.hits.Contains(rear.id)&&arrow.hits.Count==3,
                "A piercing projectile never double-hits a dead/recoiling enemy when target snapshots are reused");
            float remaining=rear.hp;
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(rear.hp==remaining&&arrow.hits.Count==3,
                "Already-hit targets are not damaged again by later arrow flight frames");
        }

        public static void NonDamagingContactsCannotGrantFreeHitImmunity(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.reducedMotion=true;
            g.save.health=50;g.invulnerable=0;g.playerHitTime=0;
            int revision=g.revision;int effects=g.effects.Count;
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(float amount in new []{0f,-10f,float.NaN,float.PositiveInfinity})
            {
                hurt.Invoke(g,new object[]{amount,"area",null});
                Check(g.save.health==50&&g.invulnerable==0&&
                      g.playerHitTime==0&&g.effects.Count==effects&&!g.Dying&&
                      g.revision==revision,
                    "Invalid and cosmetic zero-area contacts do not stagger or grant invulnerability");
            }
            // Real damage remains effective on the very next contact; the
            // inert preview must not have consumed the hero's vulnerability.
            hurt.Invoke(g,new object[]{12f,"area",null});
            Check(g.save.health<50&&g.invulnerable>0&&g.playerHitTime>0&&
                  g.effects.Count>effects,
                "Authentic positive strike still deals damage and shows the existing hit reaction");
        }

    }
}
