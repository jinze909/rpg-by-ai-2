using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ReleasedContactsSelectVisibleActivePoses(Campaign data)
        {
            foreach(float step in new[]{.02f,.04f}) for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession);g.Skill(new Point(1,0));ActorAction skill=g.playerAction;
                int frames=step==.02f?14:7;
                for(int frame=0;frame<frames-1;frame++)g.Tick(step,new Point());
                Check(!skill.released&&g.PlayerPhase==ActionPhase.Windup&&g.PlayerPose==6,"Q retains its authored preparation before contact");
                g.Tick(step,new Point());
                Check(skill.released&&g.PlayerPhase==ActionPhase.Active&&g.PlayerPose==7,"Q's actual release selects Active and the contact pose despite float residue, profession "+profession+" step "+step);
                Check(profession==1?g.projectiles.Count==5:g.effects.Exists(e=>e.kind=="burst"),"The visible Q contact corresponds to its actual volley or burst event");
            }
            for(int weapon=0;weapon<3;weapon++) for(int style=0;style<3;style++) foreach(int slices in new[]{11,17})
            {
                var g=Quiet(data,weapon);g.save.weaponStyles[weapon]=style;g.Attack(new Point(1,0));ActorAction attack=g.playerAction;
                float step=attack.contact/slices;
                for(int frame=0;frame<slices-1;frame++)g.Tick(step,new Point());
                Check(!attack.released&&g.PlayerPhase==ActionPhase.Windup,"Variable weapon/style contact stays pending before its release boundary");
                g.Tick(step,new Point());
                int pose=weapon==0&&style==1?7:5;
                Check(attack.released&&g.PlayerPhase==ActionPhase.Active&&g.PlayerPose==pose,"Actual contact governs the light/heavy release pose across variable weapon/style timestep, weapon "+weapon+" style "+style+" slices "+slices);
            }
        }
        public static void WalkingSkillRecoveryRestoresEquippedAppearance(Campaign data)
        {
            foreach(int equipped in new[]{1,2}) foreach(Point direction in new[]{new Point(1,0),new Point(0,-1),new Point(0,1)})
            {
                var g=Quiet(data);g.save.flags.Add("weapon_"+equipped);g.Equip(equipped);g.Skill(direction);
                ActorAction skill=g.playerAction;ImpactProfile impact=skill.impact;float damage=skill.damage;
                Check(g.AppearanceWeapon==0&&g.PlayerPose==6,"Cross-equipped Vanguard Q prepares with its actual class outfit");
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Check(g.AppearanceWeapon==0&&!g.ActuallyMoving&&g.PlayerPose==7,"Standing Q recovery retains its class release outfit");
                Point motion=new Point(-direction.y,direction.x),before=g.Position;
                g.Tick(.01f,motion,direction*-1);
                Check((g.Position-before).Length>.001f&&g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3,"Vanguard recovery restores collision-safe walking and its locomotion pose together");
                Check(g.AppearanceWeapon==equipped&&(g.VisualFacing-motion).Length<.001f,"Walking recovery returns body equipment and facing to the actual equipped weapon");
                Check(g.playerAction==skill&&skill.damage==damage&&(skill.direction-direction).Length<.001f&&object.ReferenceEquals(skill.impact,impact)&&skill.weapon==0&&skill.equippedWeapon==equipped,"Appearance handoff cannot mutate the prepared Q's damage, direction, impact or equipment identity");
            }
            foreach(int profession in new[]{1,2})
            {
                var g=Quiet(data,profession);g.Equip(0);g.Skill(new Point(0,-1));ActorAction skill=g.playerAction;
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Point before=g.Position;g.Tick(.01f,new Point(1,0));
                Check((g.Position-before).Length>.001f&&g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3&&g.AppearanceWeapon==0,"Ranged Q remains planted through contact, then walks during recovery with equipped outfit");
                while(g.playerAction==skill)g.Tick(.01f,new Point(1,0));
                g.Tick(.01f,new Point(1,0));Check(g.ActuallyMoving&&g.AppearanceWeapon==0,"Completed ranged Q returns to walking in the saved equipment");
            }
        }
        public static void RecoveryReactionsRetainDisplayedEquipment(Campaign data)
        {
            foreach(int equipped in new[]{1,2}) for(int state=0;state<3;state++)
            {
                var g=Quiet(data);g.save.flags.Add("weapon_"+equipped);g.Equip(equipped);g.Skill(new Point(1,0));
                ActorAction skill=g.playerAction;ImpactProfile impact=skill.impact;float damage=skill.damage;
                if(state>0)while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Point motion=state==2?new Point(0,-1):new Point(),facing=state==2?new Point(0,-1):new Point(1,0);
                if(state==2)g.Tick(.01f,motion);int displayed=state==2?equipped:0;
                g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,velocity=new Point(.01f,0),damage=1,source="shot"});g.Tick(.01f,motion,new Point(-1,0));
                Check(g.playerHitTime>0&&g.PlayerPose==10&&g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"Actual recoil keeps the outfit and direction visible at impact, equipment "+equipped+" action state "+state);
                for(int frame=0;frame<3;frame++)
                {
                    g.Tick(.01f,new Point(0,1),new Point(-1,0));
                    Check(g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"Recoil movement and live aim cannot restore an interrupted skill outfit or turn the body");
                }
                Check(g.playerAction==skill&&skill.damage==damage&&object.ReferenceEquals(skill.impact,impact)&&skill.direction.x==1,"Reaction presentation cannot rewrite the retained action's combat values");
                g.PrepareSave();SaveData saved=CloneSave(g.save);var loaded=new GameCore(data,CloneSave(saved));loaded.Start();
                Check(loaded.playerHitTime==0&&loaded.playerAction==null&&loaded.AppearanceWeapon==equipped,"Loading clears the transient reaction outfit and restores saved equipment");
                var travelled=new GameCore(data,CloneSave(saved));travelled.Start();travelled.Skill(new Point(1,0));travelled.invulnerable=0;
                travelled.projectiles.Add(new Projectile{position=travelled.Position,damage=1,source="shot"});travelled.Tick(.01f,new Point());travelled.Enter(0);
                Check(travelled.playerHitTime==0&&travelled.playerAction==null&&travelled.AppearanceWeapon==equipped,"Travel clears a new class-outfit recoil without changing saved equipment");
                g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());
                Check(g.Dying&&g.PlayerPose==11&&g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"An actual lethal followup captures the displayed recoil outfit and facing");
            }
        }
        public static void DashDistanceIsFrameStepIndependent(Campaign data)
        {
            // The same dodge must cover 0.18 * 15 world units on clear ground,
            // even when its final simulation frame is shorter than the tick.
            foreach(float step in new[]{.01f,.02f,.03f,.04f,.05f})
            {
                var g=Quiet(data);Point heading=new Point(1,0),start=g.Position;
                Check(g.CanStand(start+heading*3f),"Dash distance test needs open production ground");
                g.Dash(heading);float travelledTime=0;int frames=0;
                while(g.dashTime>0&&frames++<32)
                {
                    float remaining=g.dashTime;Point before=g.Position;
                    g.Tick(step,new Point());
                    float actual=(g.Position-before).Length;
                    float allowed=15f*Math.Min(step,remaining);
                    Check(actual<=allowed+.001f,
                        "A partial last dash tick cannot travel a full tick's distance");
                    travelledTime+=Math.Min(step,remaining);
                }
                Check(frames<32&&g.dashTime<=0&&Math.Abs(travelledTime-.18f)<.001f,
                    "Dodge consumes exactly its intended active time");
                Check(Math.Abs((g.Position-start).Length-2.7f)<.003f&&g.CanStand(g.Position),
                    "Dodge distance and collision remain consistent across simulation frame sizes");
            }
        }
        public static void DashToRunRestartsLegCadence(Campaign data)
        {
            // Dodge travel does not plant feet. On resuming a run, use a fresh
            // gait cycle instead of the unrelated frame from before the dodge.
            foreach(float step in new[]{.01f,.02f,.03f,.05f})
            {
                var g=Quiet(data);Point direction=new Point(1,0);
                Check(g.CanStand(g.Position+direction*3f),"Dodge gait test requires clear campaign ground");
                g.walkDistance=GameCore.WalkStride*.70f;g.footContacts=4;
                g.Dash(direction);
                Check(g.dashTime>0&&g.WalkFrame==0&&g.walkDistance==0,
                    "Starting a valid dodge restarts the walk leg cycle");
                int frames=0;
                while(g.dashTime>0&&frames++<32)g.Tick(step,new Point());
                Check(frames<32&&g.footContacts==4&&g.WalkFrame==0,
                    "A dodge never creates phantom footstep sounds or advances the run cycle");
                Point end=g.Position;g.Tick(step,direction);
                float distance=(g.Position-end).Length;
                Check(distance>.001f&&g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3,
                    "Holding movement after a dodge resumes the real walking pose");
                Check(Math.Abs(g.walkDistance-distance)<.001f&&
                    g.WalkFrame==GameCore.GaitFrame(distance,GameCore.WalkStride)&&g.footContacts==4,
                    "First running leg frame follows only post-dodge travel, not stale pre-dodge strides");
            }
            var rejected=Quiet(data);rejected.walkDistance=.75f;rejected.dashCooldown=1;
            rejected.Dash(new Point(1,0));
            Check(Math.Abs(rejected.walkDistance-.75f)<.001f&&rejected.dashTime<=0,
                "A failed cooldown dodge cannot reset the visible walking phase");
        }
        public static void DashExitKeepsVisibleFacing(Campaign data)
        {
            // The last moving dash tick changes PlayerPose to walking. It must not
            // briefly face the old walking/aim direction before the next tick.
            foreach(float step in new[]{.02f,.03f})
            {
                var g=Quiet(data);Point dash=new Point(1,0),old=new Point(-1,0);
                Check(g.CanStand(g.Position+dash*3),"Dodge continuity test uses open production map ground");
                g.locomotionFacing=old;g.SetAim(old);g.Dash(dash);
                Check(g.dashTime>0,"The real dodge enters its active interval");
                int ticks=0;
                while(g.dashTime>0&&ticks++<16)
                {
                    g.Tick(step,new Point(),old);
                    if(g.dashTime>0)Check((g.VisualFacing-dash).Length<.001f,
                        "Active dodge ignores the old walking and cursor direction");
                }
                Check(ticks<16&&g.dashTime<=0&&g.ActuallyMoving,
                    "Dodge finishes with a real final translation tick");
                Check((g.VisualFacing-dash).Length<.001f&&g.PlayerPose>=2&&g.PlayerPose<=3,
                    "Final moving dodge frame keeps the actual dodge heading when switching to walking");
            }
        }
        public static void EscapingTelegraphedAttacksEarnsOneCounter(Campaign data)
        {
            // A genuine escape from the enemy's marked ground should grant the
            // same one-use riposte as a contact during dodge invulnerability.
            foreach(float step in new[]{.02f,.03f,.05f})
            {
                var g=Quiet(data);Point start=g.Position;
                Check(g.CanStand(start+new Point(3,0)),"Escape test uses open campaign ground");
                g.invulnerable=0;
                var heavy=new Enemy(1510,2,10,14,999){state=1,timer=.18f,windupDuration=.65f,target=start};
                g.enemies.Add(heavy);g.Dash(new Point(1,0));
                for(int n=0;n<16&&heavy.state==1;n++)g.Tick(step,new Point());
                Check(heavy.state==0&&g.dashTime<=0&&(g.Position-start).Length>2.5f,
                    "Real heavy attack resolves after a complete collision-safe dodge");
                Check(g.riposteTime>1&&!g.perfectDodgeArmed&&g.effects.Exists(e=>e.kind=="number"&&e.text==g.L("闪避反击","COUNTER")),
                    "Escaping a marked attack grants a visible one-use counter at timestep "+step);
                Check(g.save.health==g.save.maxHealth&&g.save.energy>89,
                    "A clean escape avoids damage and restores the authored eight energy");
                float expected=g.Profile(g.save.weapon,g.Style).damage*1.5f;
                g.Attack(new Point(1,0));
                Check(g.playerAction!=null&&Math.Abs(g.playerAction.damage-expected)<.001f&&g.riposteTime==0,
                    "The next actual attack consumes the counter for exactly 50 percent bonus damage");
            }
            // Regional delayed hazards use the same visible escape rule.
            var hazard=Quiet(data);Point hazardStart=hazard.Position;
            hazard.hazards.Add(new Hazard{position=hazardStart,radius=1.5f,timer=.18f,damage=14,kind="rift"});
            hazard.Dash(new Point(1,0));
            for(int n=0;n<16&&hazard.hazards.Count>0;n++)hazard.Tick(.03f,new Point());
            Check(hazard.hazards.Count==0&&hazard.riposteTime>1&&
                hazard.effects.Exists(e=>e.kind=="number"&&e.text==hazard.L("闪避反击","COUNTER")),
                "Dodging out of a delayed regional hazard earns the same readable counter");
            // Neither an unrelated dodge nor an expired dodge may manufacture a riposte.
            var outside=Quiet(data);outside.Dash(new Point(1,0));
            outside.enemies.Add(new Enemy(1511,2,10,14,999){state=1,timer=.18f,windupDuration=.65f,target=new Point(19,14)});
            for(int n=0;n<16&&outside.enemies[0].state==1;n++)outside.Tick(.03f,new Point());
            Check(outside.riposteTime<=0,"Dashing outside the warning from the start is not a perfect escape");
            var stale=Quiet(data);Point staleStart=stale.Position;stale.Dash(new Point(1,0));
            for(int n=0;n<12;n++)stale.Tick(.05f,new Point());
            stale.enemies.Add(new Enemy(1512,2,10,14,999){state=1,timer=.001f,windupDuration=.65f,target=staleStart});
            stale.Tick(.02f,new Point());
            Check(stale.riposteTime<=0,"An old dodge cooldown cannot earn a counter after its escape window");
        }
        public static void StandingRecoveryAndCrossEquipmentDeath(Campaign data)
        {
            Point[] directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
            for(int weapon=0;weapon<3;weapon++)foreach(Point direction in directions)
            {
                var g=Quiet(data,weapon);g.Attack(direction);ActorAction action=g.playerAction;
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                g.Tick(.01f,new Point(),direction*-1);
                Check(g.playerAction==action&&(g.VisualFacing-direction).Length<.001f,"An idle release pose cannot rotate before recovery ends");
                g.Tick(.01f,direction*-1,direction*-1);
                Check(g.ActuallyMoving&&(g.VisualFacing-direction*-1).Length<.001f&&
                    g.PlayerMobility==MobilityRule.Reduced&&
                    (g.PlayerPose==5||g.PlayerPose==7||g.PlayerPose>=2&&g.PlayerPose<=3),
                    "Moving recovery keeps a rooted released upper pose while real legs resume");
                g=Quiet(data,weapon);g.save.weapon=(weapon+1)%3;g.Skill(direction);
                Check(g.AppearanceWeapon==weapon,"Q preparation shows the vocation's real equipment");
                g.invulnerable=0;g.save.health=1;g.projectiles.Add(new Projectile{position=g.Position,damage=999,visual="enemyOrb"});
                g.Tick(.01f,new Point(),direction*-1);
                Check(g.Dying&&g.AppearanceWeapon==weapon&&(g.VisualFacing-direction).Length<.001f,"A lethal hit cannot replace the active outfit or turn the corpse to the live cursor");
                g.Tick(.01f,new Point(1,0),direction*-1);Check((g.VisualFacing-direction).Length<.001f,"A fallen player ignores new movement/aim input");
                Advance(g,1.1f);Check(!g.Dying&&g.save.room==0&&g.AppearanceWeapon==g.save.weapon,"Inn recovery clears transient corpse identity without changing equipment saves");
            }
        }
        public static void StableEnemyFacingAndBackpedaling(Campaign data)
        {
            var enemy=new Enemy(1400,1,13.5f,14,999){facing=new Point(-1,0),timer=999};
            for(int i=0;i<60;i++){enemy.Face(new Point(i%2==0?.001f:-.001f,-1));Check(enemy.VisualFlip,"Near-vertical motion cannot flicker left/right body mirroring");}
            enemy.Face(new Point(1,0));for(int i=0;i<60;i++){enemy.Face(new Point(i%2==0?.001f:-.001f,1));Check(!enemy.VisualFlip,"Changing side deliberately selects a stable right silhouette");}
            var g=Quiet(data);enemy.Face(new Point(-1,0));g.enemies.Add(enemy);float start=enemy.x;
            for(int i=0;i<12;i++)g.Tick(.02f,new Point());
            Check(enemy.x>start&&enemy.backpedaling&&enemy.moving&&enemy.moveTime>0&&enemy.facing.x<-.99f&&enemy.VisualFlip,"A retreating archer moves away while keeping its bow and body toward the player");
            Point before=enemy.facing;enemy.hitTime=.15f;enemy.recoil=new Point(1,0);g.Tick(.02f,new Point());
            Check(!enemy.moving&&!enemy.backpedaling&&(enemy.facing-before).Length<.001f,"Hit recoil cannot masquerade as forward walking or redirect a prepared weapon");
        }
        public static void RecoilRetainsInterruptedBodyDirection(Campaign data)
        {
            foreach(int weapon in new[]{0,1,2})foreach(Point direction in new[]{new Point(-1,0),new Point(0,-1),new Point(0,1)})
            {
                var g=Quiet(data,weapon);g.Attack(direction);while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=1,visual="enemyOrb",velocity=new Point(.01f,0)});g.Tick(.01f,new Point(),direction*-1);
                Check(g.playerHitTime>0,"Recoil scenario takes a real hostile projectile hit");
                for(int f=0;f<5;f++){g.Tick(.01f,new Point(1,0),direction*-1);Check((g.VisualFacing-direction).Length<.001f&&g.PlayerPose==10,"Recoil translation cannot rotate the body to a different aim or old locomotion direction");}
            }
        }
        public static void MeleeWarningGeometryMatchesActualContact(Campaign data)
        {
            foreach(float heading in new[]{0f,90f,180f,270f})foreach(float offset in new[]{0f,70f,110f,180f})
            {
                var g=Quiet(data);double a=heading*Math.PI/180,b=(heading+offset)*Math.PI/180;
                var e=new Enemy(1401,0,12,14,999){state=1,timer=.001f,windupDuration=.65f};e.Face(new Point((float)Math.Cos(a),(float)Math.Sin(a)));
                Point target=e.Position+new Point((float)Math.Cos(b),(float)Math.Sin(b))*1.1f;g.save.x=target.x;g.save.y=target.y;g.invulnerable=0;
                bool expected=offset<100;Check(GameCore.InEnemyMeleeArc(e,target)==expected,"Warning sector distinguishes forward/side contact from a rear miss");
                float health=g.save.health;g.enemies.Add(e);g.Tick(.01f,new Point());
                Check((g.save.health<health)==expected&&e.releaseTime>0,"The visible sector and real strike agree for all four headings");
                Check(!GameCore.InEnemyMeleeArc(e,e.Position+e.facing*1.51f),"Warning cannot imply a strike outside the original reach");
            }
        }
        public static void CombatSnapshotsSurviveDeathsAndReinforcements(Campaign data)
        {
            var g=Quiet(data);var boss=new Enemy(1402,0,13.5f,14,1,true){timer=999};g.enemies.Add(boss);
            g.enemies.Add(new Enemy(1403,0,14,14,1){timer=999});g.enemies.Add(new Enemy(1404,0,14.5f,14,1){timer=999});
            g.projectiles.Add(new Projectile{position=boss.Position,friendly=true,explosive=true,damage=999,visual="starfire"});
            g.projectiles.Add(new Projectile{position=new Point(17,14),damage=1,visual="enemyArrow"});
            g.Tick(.01f,new Point());
            Check(g.enemies.Count==0&&g.fallen.Count==3&&g.Has("boss_1")&&g.projectiles.Count==0,"Boss death, hostile retirement and splash removals share mutation-safe snapshots");
            Check(g.effects.Exists(e=>e.kind=="projectileFade")&&g.effects.Exists(e=>e.kind=="magicImpact"),"Retired missiles and splash still finish with their original visual lifecycles");
            g=Quiet(data);boss=new Enemy(1405,0,15,14,999,true){hp=100,state=1,timer=.001f,windupDuration=1,cycle=2,target=g.Position};g.enemies.Add(boss);g.Tick(.01f,new Point());
            Check(boss.cycle==3&&g.enemies.Count==2&&g.enemies[1].state==0&&g.enemies[1].moveTime==0,"A new Boss reinforcement begins next tick rather than mutating active AI iteration");
            for(int i=0;i<120;i++)g.Tick(.02f,new Point());Check(g.enemies.Count>0&&!g.Dying,"Repeated AI, projectile and hazard iterations remain playable after spawning");
        }
    }
}
