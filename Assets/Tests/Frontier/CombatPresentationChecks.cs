using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void CanonicalHeroAnimatorTracksRealCoreActionsAcrossProfessions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                GameCore g = Quiet(data, profession);
                g.enemies.Clear();
                Check(g.HeroAnimationState == 0,
                    "Canonical blond hero must start in idle before any actual player action");
                g.Attack(new Point(1, 0));
                Check(g.playerAction != null && g.HeroAnimationState == 2,
                    "All classes enter attack state from an actual accepted combat input");
                if (profession == 0)
                {
                    // Sword heavy/combo attacks also use visual pose 6/7;
                    // pose numbers must NOT be confused with mage casting.
                    g.playerAction.combo = 2;
                    Check(g.PlayerPose >= 6 && g.HeroAnimationState == 2,
                        "Heavy sword contact remains an attack, not a fake spell state");
                }
                g.Dash(new Point(1, 0));
                Check(g.HeroAnimationState == 4 && g.playerAction == null,
                    "Real dodge overrides and cancels previous attack animation");
                Advance(g, .85f);
                Check(g.HeroAnimationState == 0,
                    "Completed dodge recovers to idle without phantom action");
                g.Skill(new Point(0, -1));
                Check(g.playerAction != null && g.playerAction.kind == "skill" &&
                      g.HeroAnimationState == 3,
                    "All class skills use the real cast action state");
                g.playerHitTime = .18f;
                Check(g.HeroAnimationState == 5,
                    "Taking damage interrupts cast with a visible hurt pose");
                g.deathTime = .9f;
                Check(g.HeroAnimationState == 6,
                    "Real player death has higher animation priority than recoil");
            }
        }

        public static void ReactionClockTraversesBothNewMotionCells(Campaign data)
        {
            // Run the existing three-original-pose GameCore clock through
            // the new FIVE-visual-Sprite response: 0,1,2,3,4.
            // Dash travel, invulnerability and knockback remain untouched.
            var game = Quiet(data, 0);
            game.enemies.Clear();
            game.Dash(new Point(1, 0));
            Check(game.HeroAnimationState == 4 &&
                game.HeroAnimationFrameCount(0) == 3,
                "Dodge keeps the authoritative three-pose gameplay clock");
            float[] timerSamples = { .18f, .135f, .09f, .045f, .001f };
            int last = -1;
            foreach (float remaining in timerSamples)
            {
                game.dashTime = remaining;
                float position = game.HeroAnimationPlaybackPosition(0);
                int spriteIndex = (int)Math.Floor(position * 2f + .20f);
                Check(spriteIndex >= last && spriteIndex >= 0 && spriteIndex <= 4,
                    "Dodge must progress through five rooted Sprite cells");
                last = spriteIndex;
            }
            Check(last == 4, "Dodge must return to accepted idle frame");
            game.dashTime = 0f;
            game.playerHitTime = .18f;
            Check(game.HeroAnimationState == 5 &&
                game.HeroAnimationFrameCount(0) == 3,
                "Hit stun keeps the authoritative three-pose gameplay clock");
            last = -1;
            foreach (float remaining in timerSamples)
            {
                game.playerHitTime = remaining;
                int spriteIndex = (int)Math.Floor(
                    game.HeroAnimationPlaybackPosition(0) * 2f + .20f);
                Check(spriteIndex >= last && spriteIndex >= 0 && spriteIndex <= 4,
                    "Hit recovery must progress through five rooted Sprite cells");
                last = spriteIndex;
            }
            Check(last == 4, "Hit recovery must return to accepted idle frame");
        }

        public static void HeroWindupActuallySamplesAnticipationBeforeContact(Campaign data)
        {
            // The production 7-cell Attack/Cast strips are arranged:
            // idle, intermediate, windup, anticipation, RELEASE, recovery, idle.
            // Contact must remain hidden until GameCore emits real damage.
            for (int profession = 0; profession < 3; profession++)
                foreach (bool skill in new[] { false, true })
                {
                    var g = Quiet(data, profession);
                    g.enemies.Clear();
                    if (skill) g.Skill(new Point(1, 0));
                    else g.Attack(new Point(1, 0));
                    ActorAction a = g.playerAction;
                    Check(a != null && !a.released, "Real action must begin in windup");
                    a.elapsed = a.contact * .88f;
                    for (int facing = 0; facing < 3; facing++)
                    {
                        float position = g.HeroAnimationPlaybackPosition(facing);
                        int nativeIndex = (int)Math.Floor(position * 2f + .20f);
                        Check(g.HeroAnimationState == (skill ? 3 : 2) &&
                            g.HeroAnimationFrame(facing) < 2 &&
                            position > 1.4f && position < 2f &&
                            nativeIndex == 3,
                            "All three facings must show anticipation cell 3, "
                            + "never contact cell 4, before release");
                    }
                    g.Tick(a.contact - a.elapsed + .005f, new Point());
                    Check(a.released && g.playerAction == a,
                        "Authoritative GameCore must release at the actual contact time");
                    for (int facing = 0; facing < 3; facing++)
                    {
                        float position = g.HeroAnimationPlaybackPosition(facing);
                        Check(position >= 2f && g.HeroAnimationFrame(facing) >= 2,
                            "Only the actual damage contact may display release cell 4");
                    }
                }
        }

        public static void RealHeroAnimatorFramesFollowActionReleaseAtAllFrameRates(Campaign data)
        {
            foreach (float dt in new[] { .01f, .02f, .05f })
                for (int profession = 0; profession < 3; profession++)
                {
                    GameCore game = Quiet(data, profession);
                    game.enemies.Clear();
                    Check(game.HeroAnimationFrameCount(0) == 1 &&
                        game.HeroAnimationFrame(0) == 0, "Idle has one rooted accepted frame");
                    game.Attack(new Point(1, 0));
                    ActorAction attack = game.playerAction;
                    Check(attack != null && game.HeroAnimationState == 2,
                        "An actual accepted attack starts production Animator's attack state");
                    Check(game.HeroAnimationFrameCount(0) == 4 &&
                        game.HeroAnimationFrameCount(1) == 4 &&
                        game.HeroAnimationFrameCount(2) == 4,
                        "All three attack facings use four phases: idle, windup, contact, recovery");
                    int windupSamples = 0, releasedSamples = 0;
                    for (int step = 0; step < 50 && game.playerAction == attack; step++)
                    {
                        if (!attack.released)
                        {
                            Check(game.HeroAnimationFrame(0) < 2 &&
                                game.HeroAnimationFrame(1) < 2,
                                "No facing may display a contact pose before real damage release");
                            windupSamples++;
                        }
                        else
                        {
                            Check(game.HeroAnimationFrame(0) >= 2 &&
                                game.HeroAnimationFrame(1) >= 2,
                                "Every attack contact/recovery frame follows GameCore damage release");
                            releasedSamples++;
                        }
                        game.Tick(dt, new Point());
                    }
                    Check(windupSamples > 0 && releasedSamples > 0,
                        "Each profession has windup AND release animation at " + dt);
                    GameCore caster = Quiet(data, profession);
                    caster.enemies.Clear();
                    caster.Skill(new Point(1, 0));
                    Check(caster.playerAction != null && caster.HeroAnimationState == 3 &&
                        caster.HeroAnimationFrameCount(0) == 4 &&
                        caster.HeroAnimationFrameCount(1) == 4 &&
                        caster.HeroAnimationFrameCount(2) == 4 &&
                        caster.HeroAnimationFrame(0) == 0,
                        "Skill begins at the rooted idle pose in all three four-phase Cast clips");
                    for (int frame = 0; frame < 50 && !caster.playerAction.released; frame++)
                    {
                        Check(caster.HeroAnimationFrame(0) < 2,
                            "Windup cannot show a skill contact before authoritative release");
                        caster.Tick(dt, new Point());
                    }
                    Check(caster.HeroAnimationState == 3 && caster.HeroAnimationFrame(0) == 2,
                        "Cast contact pose follows only the real GameCore skill release");
                    ActorAction castAction = caster.playerAction;
                    for (int frame = 0; frame < 50 && caster.playerAction == castAction &&
                        castAction.elapsed < castAction.contact + .21f; frame++)
                        caster.Tick(dt, new Point());
                    Check(caster.playerAction == castAction &&
                        caster.HeroAnimationFrame(0) == 3,
                        "Cast finishes on identical rooted idle pixels before action expiration");
                    GameCore runner = Quiet(data, profession);
                    runner.save.x = 10; runner.save.y = 14;
                    runner.Tick(.05f, new Point(1, 0));
                    Check(runner.HeroAnimationState == 1 &&
                        runner.HeroAnimationFrame(0) == runner.WalkFrame % 4,
                        "Animator run samples the real walking distance-derived frame");
                }
        }

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
        public static void AllyKillsRewardPartyButNotUnwieldedHeroMastery(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.reducedMotion=true;
            g.save.x=12;g.save.y=14;
            g.save.weapon=0;
            g.save.mastery[0]=13;g.save.mastery[1]=17;
            g.save.mastery[2]=19;
            int kills=g.save.kills[g.save.room];
            int money=g.save.coins;
            int xp=g.save.xp;
            var allyTarget=new Enemy(79711,0,14,14,1){stun=99,timer=999};
            g.enemies.Add(allyTarget);
            var allyShot=new Projectile {
                position=allyTarget.Position,velocity=new Point(1,0),
                friendly=true,visual="arrow",damage=30,life=1,
                impact=new ImpactProfile(1,0,1,0,0,false)
            };
            g.projectiles.Add(allyShot);
            g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(allyTarget) &&
                g.save.mastery[0]==13&&g.save.mastery[1]==17&&
                g.save.mastery[2]==19,
                "Companion-owned real arrow cannot train any unoperated hero weapon");
            Check(g.save.kills[g.save.room]==kills+1 &&
                g.save.coins==money+6&&g.save.xp==xp+12 &&
                g.effects.Exists(e=>e.kind=="loot"),
                "Ally killing blow must still grant existing party kill, coins, XP and loot");

            // A player-owned bow/staff flight credits the weapon whose
            // authored release spawned the projectile, not whatever
            // weapon the player switched to before impact.
            var playerTarget=new Enemy(79712,0,14.3f,14,1){
                stun=99,timer=999};
            g.enemies.Add(playerTarget);
            g.save.weapon=0;
            var playerShot=new Projectile {
                position=playerTarget.Position,
                velocity=new Point(1,0),friendly=true,
                visual="starfire",damage=30,life=1,
                impact=new ImpactProfile(2,0,2,0,0,true)
            };
            g.projectiles.Add(playerShot);g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(playerTarget)&&
                g.save.mastery[2]==21&&g.save.mastery[0]==13&&
                g.save.mastery[1]==17,
                "A real player-authored projectile kill still trains its released weapon");
            Check(g.save.kills[g.save.room]==kills+2&&
                g.save.coins==money+12&&g.save.xp==xp+24,
                "Two authentic kills retain exactly two party rewards");
        }

        public static void EscortArrowsStrikeFirstPhysicalActor(Campaign data)
        {
            // An incoming raider arrow overlaps both hit circles on ONE
            // authored 100ms/movement sweep slice. A civilian's 0.45f
            // hurt radius may enter the flight before the hero's 0.38f
            // radius even though the civilian root is farther away.
            // Hero-first list ordering used to erase this real escort hit.
            foreach(bool mirrored in new[]{false,true})
            foreach(bool civilianFirst in new[]{false,true})
            {
                var g=Fresh(data);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;
                g.save.reducedMotion=true;
                g.invulnerable=0;
                g.reliefMissionRoom=1;g.reliefMissionWave=3;
                g.reliefMissionTime=90;
                g.reliefFollowerSafety=100;
                g.reliefFollowerPosition=new Point(
                    mirrored?(civilianFirst?11.98f:11.75f):
                        (civilianFirst?12.02f:12.25f),14);
                float oldHealth=g.save.health;
                var projectile=new Projectile{
                    position=new Point(mirrored?
                        (civilianFirst?12.45f:12.25f):
                        (civilianFirst?11.55f:11.75f),14),
                    velocity=new Point(mirrored?-10:10,0),
                    life=1,damage=14,source="shot",
                    visual="enemyArrow",reliefMissionOwned=true
                };
                g.projectiles.Add(projectile);
                g.Tick(.01f,new Point());
                Check(!g.projectiles.Contains(projectile),
                    "The first actual body contact must consume the hostile arrow");
                if(civilianFirst)
                    Check(g.reliefFollowerSafety<99f&&
                        Math.Abs(g.save.health-oldHealth)<.001f,
                        "Civilian nearer along swept arrow must lose safety, not distant hero HP");
                else
                    Check(g.save.health<oldHealth&&
                        Math.Abs(g.reliefFollowerSafety-100f)<.1f,
                        "Hero physically ahead of civilian must really intercept the enemy arrow");
                Check(g.effects.FindAll(e=>e.kind=="projectileImpact").Count==1&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"&&
                        e.position.x>11f&&e.position.x<13f),
                    "One authoritative body hit must draw exactly ONE projectile impact, not duplicate civilian and generic sprites");
                Check(g.effects.Exists(e=>e.kind=="number"&&
                        e.text==g.L("护卫拦截","INTERCEPT"))==!civilianFirst,
                    "Real arrow interception feedback appears only when the hero physically protected civilians");
            }
        }

        public static void EscortGuardReflectionPreservesRealCivilianSafety(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;
            g.save.profession=0;g.save.weapon=0;
            g.save.reducedMotion=true;g.save.energy=100;
            g.SetAim(new Point(-1,0));g.guardTime=2;
            g.invulnerable=0;
            g.reliefMissionRoom=1;g.reliefMissionWave=3;
            g.reliefMissionTime=90;
            g.reliefFollowerSafety=100;
            g.reliefFollowerPosition=new Point(12.25f,14);
            float health=g.save.health;
            var arrow=new Projectile{
                position=new Point(11.75f,14),
                velocity=new Point(10,0),
                friendly=false,life=1,damage=14,
                visual="enemyArrow",source="shot",
                reliefMissionOwned=true
            };
            g.projectiles.Add(arrow);
            g.Tick(.01f,new Point());
            Check(g.projectiles.Contains(arrow)&&
                arrow.friendly&&arrow.velocity.x<-.1f,
                "Frontal sword guard must reflect the authentic inbound escort arrow");
            Check(Math.Abs(g.save.health-health)<.001f&&
                Math.Abs(g.reliefFollowerSafety-100f)<.1f&&
                g.save.energy>=93&&g.save.energy<94,
                "One real guard intercept pays seven energy but protects hero HP and civilian safety");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("护卫拦截","INTERCEPT"))&&
                g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("剑盾弹反","GUARD REFLECT")),
                "The same physical arrow must show both rescue-interception and sword-guard feedback");
            Check(g.effects.FindAll(e=>e.kind=="projectileImpact").Count==0,
                "A reflected shot stays in flight and must never show a fake contact impact");
            for(int i=0;i<4;i++)g.Tick(.02f,new Point());
            Check(g.reliefFollowerSafety>=99f,
                "Outbound friendly reflection cannot injure the escorted survivor");
        }

        public static void StarfireDetonatesOnNearSideOfRealCover(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                // Two fully traversable chambers divided by a solid wall.
                // The same 1.8-tile detonation radius overlaps targets on
                // both sides; only the visible side may receive splash.
                var rows=new string[16];
                for(int y=0;y<16;y++)
                {
                    char[] tiles=new string('.',16).ToCharArray();
                    for(int x=0;x<16;x++)
                        if(x<2||x>=14||y<2||y>=14||x==8)
                            tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{id=0,rows=rows,
                    spawnX=4.5f,spawnY=8.5f,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]};
                GameCore g=Fresh(data,2);
                g.enemies.Clear();g.save.reducedMotion=true;
                var exposed=new Enemy(77711,0,7f,9.5f,400){stun=99,timer=999};
                var sheltered=new Enemy(77712,0,9.5f,8.5f,400){stun=99,timer=999};
                g.enemies.Add(exposed);g.enemies.Add(sheltered);
                Check(g.CanStand(exposed.Position)&&g.CanStand(sheltered.Position)&&
                    g.Blocked(8.1f,8.5f),
                    "Starfire test enemies must have real safe feet on opposite sides of a solid wall");
                var bolt=new Projectile{position=new Point(6.5f,8.5f),
                    velocity=new Point(20,0),life=1f,damage=70,
                    friendly=true,explosive=true,visual="starfire"};
                g.projectiles.Add(bolt);
                for(int i=0;i<4 && g.projectiles.Contains(bolt);i++)
                    g.Tick(.05f,new Point());
                Check(!g.projectiles.Contains(bolt)&&
                    exposed.hp<400&&sheltered.hp==400,
                    "Solid cover must cause real near-side starfire splash without damaging the occluded enemy");
                Check(g.effects.Exists(e=>e.kind=="magicImpact"&&
                    Math.Abs(e.radius-1.8f)<.001f&&e.position.x<8f),
                    "Wall contact must generate the real 1.8-tile impact on the near face");
                int effects=g.effects.FindAll(e=>e.kind=="magicImpact").Count;
                for(int i=0;i<5;i++)g.Tick(.05f,new Point());
                Check(g.effects.FindAll(e=>e.kind=="magicImpact").Count<=effects,
                    "A terrain impact must not repeat damage or create duplicate explosions");

                // A projectile born in cover or one without an explosive
                // profile must not bypass walls via an area hit.
                g.enemies.Clear();g.enemies.Add(exposed);exposed.hp=400;
                g.effects.Clear();g.projectiles.Clear();
                var invalid=new Projectile{position=new Point(7.5f,8.5f),
                    velocity=new Point(20,0),life=1,damage=70,
                    friendly=true,explosive=true,visual="starfire",
                    blockedAtSpawn=true};
                g.projectiles.Add(invalid);
                g.Tick(.05f,new Point());
                Check(exposed.hp==400&&
                    !g.effects.Exists(e=>e.kind=="magicImpact")&&
                    g.effects.FindAll(e=>e.kind=="projectileImpact"&&
                        e.radius==0).Count==1,
                    "A wall-obstructed muzzle keeps one small contact spark, never a fake mage explosion");
                g.projectiles.Clear();g.effects.Clear();
                g.projectiles.Add(new Projectile{
                    position=new Point(7.5f,8.5f),
                    velocity=new Point(20,0),life=1,damage=70,
                    friendly=true,visual="arrow"});
                g.Tick(.05f,new Point());
                Check(exposed.hp==400&&
                    !g.effects.Exists(e=>e.kind=="magicImpact"),
                    "A blocked ordinary arrow must not gain the mage's area attack");
                g.projectiles.Clear();g.effects.Clear();
                g.projectiles.Add(new Projectile{
                    position=new Point(6.5f,8.5f),
                    velocity=new Point(10,0),life=0,
                    friendly=true,visual="starfire",explosive=true});
                g.Tick(.01f,new Point());
                Check(g.projectiles.Count==0&&
                    g.effects.FindAll(e=>e.kind=="projectileFade").Count==1&&
                    !g.effects.Exists(e=>e.kind=="magicImpact"),
                    "A range-expired starfire must retain one quiet fade without a phantom explosion");
            }
            finally{data.maps[0]=original;}
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
        public static void CompanionAutoFireCannotSpendCadenceOnExpiredEnemy(Campaign data)
        {
            // The real hero can kill an enemy during a companion's .16s
            // windup. The ally must keep the full-body recovery pose, not
            // release a phantom arrow and burn another 2.8-second cycle.
            var g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14;
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var enemy=new Enemy(77881,0,15,14,25){stun=99,timer=999};
            g.enemies.Add(enemy);
            var ready=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            ready.SetValue(g,0f);g.Tick(.02f,new Point());
            var attack=g.companionAction;
            Check(attack!=null&&attack.kind=="attack"&&
                attack.target.HasValue,
                "Automatic archer begins an authentic locked-target bow windup");

            // Deliver one actual player-owned projectile contact BEFORE
            // the ally's authored contact frame.
            g.projectiles.Add(new Projectile{position=enemy.Position,
                friendly=true,visual="arrow",damage=999,life=1f});
            g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(enemy)&&!attack.released,
                "Real projectile kills the locked foe before companion release");
            g.effects.Clear();g.projectiles.Clear();
            Advance(g,.18f);
            Check(attack.released&&g.projectiles.Count==0&&
                !g.effects.Exists(e=>e.kind=="release")&&
                (float)ready.GetValue(g)<=.22f,
                "A dead target cannot create phantom companion arrows or consume 2.8s cooldown");
            Check(g.companionAction==attack,
                "Canceled auto-shot retains the complete authored bow recovery pose");

            // A live foe moving behind actual stone during the same
            // windup must not receive a newly created shot through cover.
            var occluded=Fresh(data,1);occluded.Enter(1);
            occluded.enemies.Clear();occluded.save.x=9.5f;
            occluded.save.y=8.8f;occluded.save.flags.Add("companion_1");
            occluded.SelectCompanion(1);
            occluded.companionPosition=new Point(9.5f,8.8f);
            var mover=new Enemy(77882,0,10.5f,8.8f,500){
                stun=99,timer=999};
            occluded.enemies.Add(mover);
            ready.SetValue(occluded,0f);occluded.Tick(.02f,new Point());
            attack=occluded.companionAction;
            Check(attack!=null&&attack.kind=="attack",
                "Ally prepares a real visible auto-attack in the near forest lane");
            mover.x=9.5f;mover.y=10.2f;
            Check(occluded.Blocked(9.5f,9.5f)&&
                occluded.CanStand(mover.Position),
                "The target moved into an accessible tile behind genuine forest stone");
            Advance(occluded,.16f);
            Check(attack.released&&occluded.projectiles.Count==0&&
                (float)ready.GetValue(occluded)<=.22f,
                "A target behind a newly obstructed lane cannot waste the actual companion volley");
            Check(occluded.companionAction==attack,
                "Blocked late release still respects the original full-body action recovery");
        }

        public static void CompanionAutoTargetsNearestReachableThreat(Campaign data)
        {
            // Combat uses the actual, ordered roster: the original Find()
            // always chose the oldest spawned enemy, even when a closer
            // attacker was about to strike from the opposite direction.
            var g=Fresh(data);
            g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14f;
            g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var far=new Enemy(74111,0,17,14,500){stun=99,timer=99};
            var close=new Enemy(74112,0,14,14,500){stun=99,timer=99};
            Check(g.CanStand(far.Position)&&g.CanStand(close.Position),
                "Companion nearest-target scenario has genuine traversable enemy feet");
            g.enemies.Add(far);g.enemies.Add(close);
            typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance).SetValue(g,0f);
            g.Tick(.02f,new Point());
            ActorAction action=g.companionAction;
            Check(action!=null&&action.kind=="attack"&&
                action.target.HasValue&&
                (action.target.Value-close.Position).Length<.001f,
                "Ivo must select the nearest visible enemy, not roster spawn order");
            Check((action.direction-(close.Position-g.companionPosition).Normalized).Length<.001f,
                "Companion projectile wind-up and authored facing must aim at the actual closest target");
            Advance(g,.17f);
            Check(action.released,
                "Closest-target aim drives a genuine companion bow release, not a HUD-only selection");
        }

        public static void CompanionAutoHealRequiresPhysicalReachAndRetiresBlockedRelease(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("companion_0");
            g.SelectCompanion(0);
            g.save.x=9.5f;g.save.y=8.8f;
            g.companionPosition=new Point(9.5f,10.2f);
            g.save.health=30;g.save.reducedMotion=true;
            Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition)&&
                g.Blocked(9.5f,9.5f),
                "Healing line-of-sight fixture retains the genuine forest wall");
            var cooldown=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            cooldown.SetValue(g,0f);
            g.Tick(.02f,new Point());
            Check(g.companionAction==null&&g.save.health==30,
                "An ally across solid forest stone cannot start a remote automatic heal");
            Check((float)cooldown.GetValue(g)>0&&
                (float)cooldown.GetValue(g)<=.22f,
                "Invalid healing path cannot silently consume six seconds of ally action readiness");

            // Move the ally into the actual reachable side of the wall.
            g.companionPosition=new Point(10.5f,8.8f);
            Check(g.CanStand(g.companionPosition),
                "Nearby healing partner must occupy a legal foot collider");
            Advance(g,.24f);
            ActorAction heal=g.companionAction;
            Check(heal!=null&&heal.kind=="heal"&&!heal.released,
                "After regaining real line of sight the ally can start its normal heal");

            // A fast transition after cast preparation cannot pay health
            // through the old wall at the release frame.
            g.save.y=10.2f;g.save.x=9.5f;
            Advance(g,.18f);
            Check(heal.released&&g.save.health==30&&
                !g.effects.Exists(e=>e.kind=="number"&&e.text=="+HP"),
                "Healing contact is canceled if the hero crosses a wall before release");
            Check((float)cooldown.GetValue(g)<=.22f,
                "Interrupted healing release cannot pay a full six-second cooldown");
            Advance(g,.35f);
            g.save.y=8.8f;g.save.x=9.5f;
            g.companionPosition=new Point(10.5f,8.8f);
            Advance(g,.25f);
            heal=g.companionAction;
            Check(heal!=null&&heal.kind=="heal",
                "Same ally reattempts healing when the real player returns to range");
            Advance(g,.18f);
            Check(g.save.health>30&&g.effects.Exists(e=>
                e.kind=="number"&&e.text=="+HP"),
                "Genuine unobstructed ally healing still restores HP on its contact frame");
        }

        public static void CompanionIdleReadinessReactsToRealTargets(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14f;
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var timer=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            timer.SetValue(g,0f);
            g.Tick(.02f,new Point());
            Check(g.companionAction==null&&
                (float)timer.GetValue(g)>0&&
                (float)timer.GetValue(g)<=.22f,
                "Ivo cannot burn an entire 2.8-second volley cooldown without an enemy");
            var incoming=new Enemy(74114,0,15,14,800){stun=99,timer=99};
            g.enemies.Add(incoming);
            Advance(g,.24f);
            ActorAction attack=g.companionAction;
            Check(attack!=null&&attack.kind=="attack"&&
                attack.target.HasValue&&
                (attack.target.Value-incoming.Position).Length<.001f,
                "Entering a real firing lane awakens companion AI in the next 220ms");
            Check((float)timer.GetValue(g)>2.3f,
                "An accepted, real companion attack still consumes normal volley cooldown");
            Advance(g,.18f);
            Check(attack.released,
                "Newly acquired target fires from a genuine timed bow contact");
        }

        public static void CompanionNarrowBridgesKeepSeparateSafeFeet(Campaign data)
        {
            // Real collision geometry in a single-tile stone causeway:
            // original diagonal follow sockets are all blocked, but a
            // narrow horizontal standing slot is physically open.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[16];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] cells=new string('#',16).ToCharArray();
                    if(y==8)for(int x=2;x<14;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=8.5f,spawnY=8.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var g=Fresh(data);g.enemies.Clear();
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                Check(g.save.companion==1&&
                    g.CanStand(g.companionPosition)&&
                    (g.companionPosition-g.Position).Length>1.05f&&
                    Math.Abs(g.companionPosition.y-g.Position.y)<.01f,
                    "Actual ally spawned beside the hero within a narrow bridge, never inside hero feet or solid walls");

                Point root=g.companionPosition;
                var follow=typeof(GameCore).GetMethod("CompanionFollowTarget",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                for(int k=0;k<8;k++)
                {
                    Point desired=(Point)follow.Invoke(g,null);
                    Check((desired-root).Length<.001f,
                        "Existing safe foot root must not snap between fallback slots when nothing moves");
                }
                g.save.x+=.22f;
                for(int i=0;i<6;i++)g.Tick(.05f,new Point());
                Check(g.CanStand(g.companionPosition)&&
                    (g.companionPosition-g.Position).Length>.85f,
                    "Real companion follow simulation remains a separate physical actor on a tight route");
            }
            finally{data.maps[0]=original;}
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

        public static void NonpiercingProjectileContactsFrontEnemyBeforeBackRoster(Campaign data)
        {
            // The two real enemy footprints deliberately overlap within
            // ONE flight subdivision. List order must not alter the single
            // projectile's physically first contact.
            foreach(bool reverseRoster in new[]{false,true})
            {
                GameCore g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
                var front=new Enemy(77911,0,13.8f,14f,300){
                    stun=99,timer=999};
                var back=new Enemy(77912,0,14.0f,14f,300){
                    stun=99,timer=999};
                Check(g.CanStand(front.Position)&&g.CanStand(back.Position),
                    "Overlapping hit-circle regression has two actual legal enemy foot positions");
                if(reverseRoster)
                {g.enemies.Add(front);g.enemies.Add(back);}
                else
                {g.enemies.Add(back);g.enemies.Add(front);}
                var shot=new Projectile{position=new Point(13.45f,14f),
                    velocity=new Point(10f,0),life=1f,damage=22f,
                    friendly=true,visual="arrow"};
                g.projectiles.Add(shot);
                g.Tick(.01f,new Point());
                Check(!g.projectiles.Contains(shot)&&front.hp<300&&back.hp==300,
                    "One nonpiercing arrow must hit the first collision surface, not the first roster entry");
                Check(shot.hits.Count==1&&shot.hits[0]==front.id,
                    "First-contact resolver cannot count a back-row phantom hit");
                Check(g.effects.Exists(e=>e.kind=="projectileImpact"),
                    "Physical first-contact correction retains the real arrow impact effect");
            }
            // The established piercing volley intentionally contacts both;
            // front-only ordering is specific to nonpiercing projectiles.
            GameCore piercing=Fresh(data,1);piercing.Enter(1);
            piercing.enemies.Clear();piercing.save.reducedMotion=true;
            piercing.enemies.Add(new Enemy(77920,0,13.8f,14f,300){
                stun=99,timer=999});
            piercing.enemies.Add(new Enemy(77921,0,14f,14f,300){
                stun=99,timer=999});
            var bolt=new Projectile{position=new Point(13.45f,14f),
                velocity=new Point(10f,0),life=1f,damage=22f,
                friendly=true,piercing=true,visual="arrow"};
            piercing.projectiles.Add(bolt);piercing.Tick(.01f,new Point());
            Check(bolt.hits.Count==2&&piercing.enemies.TrueForAll(e=>e.hp<300),
                "Piercing arrows still contact every overlapping real enemy once");
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

        public static void ReselectingSameCompanionPreservesInFlightCommand(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("companion_0");g.save.flags.Add("companion_1");
            g.SelectCompanion(0);
            g.save.health=35;g.save.energy=100;
            g.CommandCompanion();
            ActorAction active=g.companionAction;
            Check(active!=null&&active.kind=="command"&&g.companionCooldown>0,
                "An authentic healer command has a pending release and a charged cooldown");
            int revision=g.revision;
            Point position=g.companionPosition;
            float cooldown=g.companionCooldown;
            g.SelectCompanion(0);
            Check(g.companionAction==active&&g.revision==revision&&
                  g.companionCooldown==cooldown&&
                  (g.companionPosition-position).Length<.001f,
                "Selecting the already-active healer cannot cancel a charged heal or teleport the ally");
            Advance(g,.22f);
            Check(active.released&&g.save.health>35,
                "The authentic healer release completes even after a redundant party selection");
            g.SelectCompanion(1);
            Check(g.save.companion==1&&g.companionAction==null&&
                  g.revision>revision,
                "Selecting another unlocked ally still performs a genuine party change");
            g.CommandCompanion();
            Check(g.companionAction==null,
                "An earlier command cooldown cannot be bypassed by swapping companions");
        }

        public static void InvalidOffensiveDamageNeverHealsOrCreatesPhantomHits(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.reducedMotion=true;
            var e=new Enemy(784,0,13,14,80){stun=999,timer=999};
            g.enemies.Add(e);
            int seed=g.save.seed,revision=g.revision;
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(float amount in new []{0f,-8f,float.NaN,float.NegativeInfinity})
            {
                hit.Invoke(g,new object[]{e,amount,false,null,null});
                Check(e.hp==80&&e.stagger==0&&g.save.seed==seed&&
                      g.effects.Count==0&&g.revision==revision&&
                      g.enemies.Contains(e),
                    "An invalid player damage number cannot heal or stagger foes or spend the crit seed");
            }
            hit.Invoke(g,new object[]{e,15f,false,null,null});
            Check(e.hp<80&&g.effects.Count>0&&g.save.seed!=seed,
                "A genuine subsequent positive hit still uses the complete damage and visual pipeline");
        }

        public static void RealWeaponChangesRestartWeaponComboButReselectDoesNot(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.save.flags.Add("weapon_1");
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.comboTimer>0&&g.combo==0,
                "Sword contact starts the actual uninterrupted combo window");
            g.playerAction=null;g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.combo==1&&g.comboTimer>0,
                "Two real sword attacks are counted in the same combo");
            g.Equip(0);
            Check(g.combo==1&&g.comboTimer>0,
                "Reselecting the exact same sword cannot reset earned combo progress");
            g.Equip(1);
            Check(g.save.weapon==1&&g.combo==0&&g.comboTimer==0&&
                  g.playerAction==null,
                "A genuine switch to the unlocked bow discards obsolete sword combo state");
            g.Equip(0);g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.combo==0,
                "Returning to the sword starts a first hit rather than carrying a phantom third-hit boost");
        }

        public static void StyleChangesRestartCombosButRepeatedStylesPreserveAttacks(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("core_1");
            g.save.energy=100;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.combo==0&&g.comboTimer>0,
                "A real starter sword swing begins a live combat sequence");
            ActorAction first=g.playerAction;
            int previous=g.revision;
            g.SetStyle(0);
            Check(g.playerAction==first&&g.comboTimer>0&&g.revision==previous,
                "Clicking the existing balanced style does not cancel an attack or mutate the save");
            g.playerAction=null;g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.combo==1&&g.comboTimer>0,
                "A second real sword swing advances the existing chain");
            g.SetStyle(1);
            Check(g.Style==1&&g.playerAction==null&&g.combo==0&&g.comboTimer==0,
                "Changing to unlocked focused sword style retires the old combo");
            g.SetStyle(0);g.attackCooldown=0;g.Attack(new Point(1,0));
            Check(g.combo==0&&g.playerAction!=null,
                "Returning to balanced style begins a genuine first strike, not a borrowed third-hit bonus");
        }


        public static void LateBasicAttacksBufferOnceAndRespectCombatProfiles(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, .03f, .05f })
            {
                var g = Quiet(data); g.enemies.Clear();
                g.Attack(new Point(1, 0));
                ActorAction first = g.playerAction;
                while (first.elapsed < first.duration - .10f && g.playerAction == first)
                    g.Tick(step, new Point());
                Check(g.playerAction == first && first.released,
                    "A late input still belongs to a real released sword attack");
                g.Attack(new Point(0, -1));
                Check(g.playerAction == first && g.combo == 0,
                    "Pressing during recovery never cuts off the old contact pose");
                g.SetAim(new Point(1, 0));
                ActorAction next = null;
                for (int frame = 0; frame < 50; frame++)
                {
                    g.Tick(step, new Point());
                    if (g.playerAction != first) { next = g.playerAction; break; }
                }
                Check(next != null && next.kind == "attack" && g.combo == 1 &&
                    next.direction.y < -.99f,
                    "A single late press chains one real second strike with its original facing at step " + step);
                for (int frame = 0; frame < 70; frame++) g.Tick(step, new Point());
                Check(g.playerAction == null && g.combo == 1,
                    "A buffered tap must never self-repeat as a third strike");
            }
            var early = Quiet(data); early.enemies.Clear(); early.Attack(new Point(1, 0));
            early.Tick(.05f, new Point()); early.Attack(new Point(0, -1));
            Advance(early, .7f);
            Check(early.playerAction == null && early.combo == 0,
                "An early press during the first windup cannot queue a delayed phantom attack");
            var same = Quiet(data); same.enemies.Clear(); same.Attack(new Point(1, 0));
            Advance(same, .27f); same.Attack(new Point(0, -1));
            same.Equip(0); same.SetStyle(0);
            Advance(same, .12f);
            Check(same.playerAction != null && same.combo == 1,
                "Re-selecting an unchanged weapon and style preserves the late attack");
            var changed = Quiet(data); changed.enemies.Clear();
            changed.save.flags.Add("core_1"); changed.Attack(new Point(1, 0));
            Advance(changed, .27f); changed.Attack(new Point(0, -1)); changed.SetStyle(1);
            Advance(changed, .5f);
            Check(changed.playerAction == null && changed.combo == 0 &&
                changed.Style == 1, "A genuine style change drops the obsolete buffered attack and combo");
            var equipped = Quiet(data); equipped.enemies.Clear();
            equipped.save.flags.Add("weapon_1"); equipped.Attack(new Point(1, 0));
            Advance(equipped, .27f); equipped.Attack(new Point(0, -1)); equipped.Equip(1);
            Advance(equipped, .5f);
            Check(equipped.playerAction == null && equipped.combo == 0,
                "Changing weapon cannot replay the old sword buffer as a bow attack");
            var priority = Quiet(data); priority.enemies.Clear();
            priority.Attack(new Point(1, 0)); Advance(priority, .27f);
            priority.Attack(new Point(0, -1)); priority.Skill(new Point(1, 0));
            Check(priority.playerAction != null && priority.playerAction.kind == "skill",
                "A real Q input takes priority over the pending basic strike");
            Advance(priority, 1f);
            Check(priority.playerAction == null && priority.combo == 0,
                "A queued basic strike does not fire after the skill");
            var limited = Quiet(data); limited.enemies.Clear(); limited.save.flags.Add("core_1");
            limited.SetStyle(1); limited.Attack(new Point(1, 0));
            Advance(limited, .34f); limited.Attack(new Point(0, -1));
            limited.save.energy = 0;
            Advance(limited, .4f);
            Check(limited.playerAction == null && limited.combo == 0,
                "Buffered focused strike checks energy when dispatched, not when queued");
            var loaded = Quiet(data); loaded.enemies.Clear();
            loaded.Attack(new Point(1, 0)); Advance(loaded, .27f);
            loaded.Attack(new Point(0, -1));
            loaded.PrepareSave(); loaded.Load(CloneSave(loaded.save));
            Advance(loaded, .7f);
            Check(loaded.playerAction == null && loaded.combo == 0,
                "Transient buffer is never carried into a newly loaded v2 save");
        }


        public static void RangedThirdHitsProduceDistinctRealProjectiles(Campaign data)
        {
            foreach (float frameStep in new[] { .01f, .02f, .03f, .05f })
            foreach (int weapon in new[] { 1, 2 })
            foreach (int style in new[] { 0, 1, 2 })
            {
                var g = Fresh(data, weapon); g.enemies.Clear();
                g.save.flags.Add("core_1"); g.save.energy = 100; g.SetStyle(style);
                Projectile[] shots = new Projectile[3];
                for (int index = 0; index < 3; index++)
                {
                    Check(g.attackCooldown <= .0001f && g.playerAction == null,
                        "Each ranger/mage attack starts after the actual preceding release");
                    g.projectiles.Clear();
                    g.Attack(new Point(1, 0));
                    ActorAction attack = g.playerAction;
                    Check(attack != null && attack.kind == "attack" && attack.combo == index,
                        "Real ranged chain advances through each of its three consecutive attacks");
                    for (int frame = 0; frame < 100 && !attack.released; frame++)
                        g.Tick(frameStep, new Point());
                    Check(attack.released, "Ranged attack reaches its visible contact before producing shots");
                    shots[index] = g.projectiles.Find(p => p.friendly);
                    Check(shots[index] != null && shots[index].impact != null &&
                        shots[index].impact.combo == index, "Finisher characteristics follow a real released attack");
                    Advance(g, Math.Max(0, attack.duration - attack.elapsed) + .04f);
                }
                float bonus = weapon == 1 ? 1.15f : 1.12f;
                Check(Math.Abs(shots[2].damage / shots[0].damage - bonus) < .001f &&
                    Math.Abs(shots[1].damage / shots[0].damage - 1f) < .001f,
                    "Only the true third ranged release earns the intended moderate damage payoff");
                if (weapon == 1)
                    Check(shots[0].piercing == (style == 1) &&
                        shots[2].piercing == (style != 2),
                        "Balanced third arrow pierces, Focused retains every-shot pierce, Flow keeps its spread balance");
                else
                    Check(shots[0].slowing == (style == 2) && shots[2].slowing,
                        "Staff third hit uses real projectile slow without removing Flow's existing all-hit slow");
                Check(g.effects.Exists(e => e.kind == "number" && e.text == "CHAIN FINISH") ||
                    !g.save.english, "Third-hit confirmation is emitted by the live release pipeline");
            }
        }

        public static void ArcherCompanionCannotWasteVolleyOnItsOwnFootTarget(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            g.SetAim(new Point(0,-1));
            Point ally=g.companionPosition;
            g.SetAimTarget(ally);
            g.CommandCompanion();
            ActorAction action=g.companionAction;
            Check(action!=null&&action.kind=="command"&&
                  (action.direction-new Point(0,-1)).Length<.001f&&
                  !action.target.HasValue,
                "Clicking the companion itself falls back to the facing rather than a zero-length firing direction");
            Advance(g,.23f);
            Check(g.projectiles.Count==3&&g.projectiles.TrueForAll(p=>p.velocity.Length>10&&p.velocity.y<0),
                "All actual commanded arrows fly away from the ally after an accidental self-target click");
            g.projectiles.Clear();
            g.companionCooldown=0;
            Point valid=g.companionPosition+new Point(4,0);
            g.SetAimTarget(valid);
            g.CommandCompanion();
            Check(g.companionAction!=null&&g.companionAction.target.HasValue&&
                  (g.companionAction.direction-new Point(1,0)).Length<.001f,
                "A genuinely different cursor target still controls the next aimed volley");
        }

    }
}
