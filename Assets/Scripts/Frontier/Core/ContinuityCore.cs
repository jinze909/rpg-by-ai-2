using System;

namespace Starfall
{
    public enum MobilityRule { Full, Reduced, AttackMovement, Stationary }
    public enum ActionPhase { None, Windup, Active, Recovery }

    // Positions, animation contacts, aim and barriers share simulation truth.
    // These values are transient; old saves retain their original schema.
    public sealed partial class GameCore
    {
        public const float WalkStride = 2f;
        public float walkDistance, gateOpeningTime, recoilSpeed;
        public int footContacts;
        public Point locomotionFacing = new Point(1, 0);
        // Target, body direction and muzzle are captured together at action start.
        // Continuous flight aim must not select a second body row near 45 degrees.
        public Point? aimTarget;
        public void SetAimTarget(Point? target)
        { aimTarget=target.HasValue&&Finite(target.Value.x)&&Finite(target.Value.y)?target:null; }

        // Gamepad ranged attacks need physical, rather than screen-space,
        // target acquisition. This only picks a LIVE foe inside the stick
        // direction's narrow cone; it never damages, teleports or persists
        // a target. Actual release/impact still use WeaponShot's muzzle,
        // projectile sweep and GameCore's canonical collision authority.
        public Enemy AcquireRangedStickTarget(Point heading,Enemy previous=null)
        {
            if(mode!="world"||Dying||save==null||
               (save.weapon==0&&save.profession!=1)||
               !Finite(heading.x)||!Finite(heading.y)||
               heading.Length<.28f||!CanStand(Position))
                return null;
            Point look=heading.Normalized;
            int weapon=save.weapon==0?1:save.weapon;
            Enemy selected=null;
            float bestScore=float.MaxValue;
            foreach(Enemy enemy in enemies)
            {
                if(enemy==null||enemy.hp<=0||!CanStand(enemy.Position))
                    continue;
                Point vector=enemy.Position-Position;
                float distance=vector.Length;
                if(!Finite(distance)||distance<.6f||distance>12f)
                    continue;
                float alignment=(vector.x*look.x+vector.y*look.y)/distance;
                if(alignment<.96f||
                   !ClearAttackPath(Position,enemy.Position))
                    continue;
                // A centerline through open terrain may still bury the
                // bow's actual hand against an adjacent wall. Check the
                // same swept attachment as the real live shot.
                Projectile probe=WeaponShot(Position,weapon,
                    vector.Normalized,13f,1f,enemy.Position);
                if(probe.blockedAtSpawn||
                   !ClearAttackPath(probe.position,enemy.Position))
                    continue;
                // A farther enemy can be perfectly aligned with the stick
                // while a nearer hostile body would absorb the actual
                // arrow. Never promise a rear lock through an intervening
                // living hurt circle. The real missile keeps all its own
                // damage and collision rules.
                Point ray=probe.velocity.Normalized;
                float targetEntry=ProjectileCircleEntry(
                    probe.position,probe.velocity,enemy.Position,
                    (enemy.boss?.9f:.5f)+probe.Radius);
                bool occludedByBody=false;
                foreach(Enemy other in enemies)
                {
                    if(other==null||other.hp<=0f||
                       System.Object.ReferenceEquals(other,enemy))
                        continue;
                    Point offset=other.Position-probe.position;
                    float along=offset.x*ray.x+offset.y*ray.y;
                    if(along<=0f)continue;
                    float side2=Math.Max(0f,offset.x*offset.x+
                        offset.y*offset.y-along*along);
                    float radius=(other.boss?.9f:.5f)+probe.Radius;
                    if(side2>=radius*radius)continue;
                    float entry=ProjectileCircleEntry(
                        probe.position,probe.velocity,other.Position,radius);
                    if(entry+.015f<targetEntry&&
                       ClearAttackPath(probe.position,other.Position))
                    {occludedByBody=true;break;}
                }
                if(occludedByBody)continue;
                // A slight frame-to-frame stick tremor should not make
                // the world reticle jitter between equally valid foes.
                // Preserve the old live reference only while it still
                // passes ALL physical checks above; a decisive new stick
                // direction still immediately switches targets.
                float score=(1f-alignment)*12f+distance*.015f;
                if(System.Object.ReferenceEquals(enemy,previous))
                    score-=.07f;
                if(score<bestScore-.00001f)
                { bestScore=score;selected=enemy; }
            }
            return selected;
        }
        // Aim ahead of an actually MOVING enemy without widening its
        // collision circle or altering an authored attack's on-contact
        // authority. Estimated projectile speed is the real held weapon's
        // flight speed (bow 12, staff 8; Ranger volley 13); the preparation
        // delay matches the release pose rather than instantaneous hitscan.
        // If a predicted landing foot or *any* muzzle/ray sample is sealed,
        // revert to the enemy's visible current center, never fire through
        // world cover on the strength of an aim-assist estimate.
        public Point PredictRangedTargetPoint(Enemy target,
            float speed,float windupSeconds)
        {
            if(target==null)return Position;
            Point current=target.Position;
            if(mode!="world"||Dying||save==null||
               !enemies.Contains(target)||target.hp<=0f||
               save.weapon==0||!CanStand(Position)||
               !Finite(speed)||speed<1f||
               !Finite(windupSeconds)||
               !target.moving||
               !Finite(target.moveVelocity.x)||
               !Finite(target.moveVelocity.y))
                return current;
            Point velocity=target.moveVelocity;
            float enemySpeed=velocity.Length;
            if(!Finite(enemySpeed)||enemySpeed<.2f||enemySpeed>4.5f)
                return current;
            int weapon=save.weapon;
            Point muzzle=WeaponOrigin(Position,weapon,
                (current-Position).Normalized);
            float flight=(current-muzzle).Length/speed;
            if(!Finite(flight))return current;
            float horizon=Clamp(windupSeconds,0,.45f)+
                Clamp(flight,0,1.5f);
            Point offset=velocity*horizon;
            float maxLead=weapon==1?1.7f:1.45f;
            if(offset.Length>maxLead)
                offset=offset.Normalized*maxLead;
            Point predicted=current+offset;
            if(!CanStand(predicted)||
               !ClearAttackPath(current,predicted)||
               !ClearAttackPath(Position,predicted))
                return current;
            Point direction=(predicted-Position).Normalized;
            Projectile shot=WeaponShot(Position,weapon,direction,
                speed,1f,predicted);
            if(shot.blockedAtSpawn||ShotBlocked(shot,shot.position))
                return current;
            Point flightRay=predicted-shot.position;
            float flightDistance=flightRay.Length;
            if(!Finite(flightDistance)||flightDistance<.05f||
                flightDistance>speed*shot.life)
                return current;
            int samples=Math.Max(1,
                (int)Math.Ceiling(flightDistance/.1f));
            for(int i=1;i<=samples;i++)
            {
                Point p=shot.position+flightRay*(i/(float)samples);
                if(ShotBlocked(shot,p))return current;
            }
            return predicted;
        }

        public bool ActuallyMoving { get { return movementDirection.Length > .001f; } }
        public int WalkFrame { get { return GaitFrame(walkDistance, WalkStride); } }
        // Native sprites need the fraction BETWEEN the four distance-driven
        // poses. This is read-only and uses the same accumulated distance as
        // OnGUI, footsteps, collision and the original integer walk frame.
        // No free-running Animator or save schema is introduced.
        public float WalkPosePhase
        {
            get
            {
                double distance = Math.Max(0, walkDistance);
                double stride = Math.Max(.1, WalkStride);
                double cycle = (distance / stride * 4.0) % 4.0;
                return (float)cycle;
            }
        }
        public static int GaitFrame(float distance, float stride)
        { return ((int)Math.Floor(Math.Max(0, distance) / Math.Max(.1f, stride) * 4)) % 4; }
        public static int DirectionRow(Point direction)
        { return Math.Abs(direction.y) > Math.Abs(direction.x) ? direction.y < 0 ? 1 : 2 : 0; }
        public ActionPhase PlayerPhase
        {
            // TickAction's release event is the shared truth for both damage and
            // animation, including contacts reached through its float tolerance.
            get { return playerAction == null ? ActionPhase.None : !playerAction.released ? ActionPhase.Windup : playerAction.elapsed + .00001f < playerAction.contact + .1f ? ActionPhase.Active : ActionPhase.Recovery; }
        }
        public bool RecoveryWalking
        { get { return playerAction != null && PlayerPhase == ActionPhase.Recovery && ActuallyMoving && !Dying && dashTime <= 0 && playerHitTime <= 0; } }
        public MobilityRule PlayerMobility
        {
            get
            {
                if (Dying || playerHitTime > 0) return MobilityRule.Stationary;
                if (playerAction == null) return MobilityRule.Full;
                // Spell/bow windups and actual contact still plant the feet.
                // Only after release may the HERO move and finish the cast
                // with its ONE complete action sprite moving at the root;
                // never graft Walk legs onto the active attack/skill body.
                if(playerAction.kind=="skill"&&playerAction.weapon!=0)
                    return PlayerPhase==ActionPhase.Recovery ?
                        MobilityRule.Reduced : MobilityRule.Stationary;
                if (playerAction.weapon == 0) return PlayerPhase == ActionPhase.Recovery ? MobilityRule.Reduced : MobilityRule.AttackMovement;
                return PlayerPhase == ActionPhase.Recovery ? MobilityRule.Reduced : MobilityRule.Stationary;
            }
        }
        public Point VisualFacing
        {
            get
            {
                if(Dying&&fallenPlayerWeapon>=0)return fallenPlayerFacing;
                if(playerHitTime>0&&playerHitFacing.Length>.05f)return playerHitFacing;
                if(dashTime>0)return dashDirection;
                // The released weapon/bow/cast keypose faces its locked
                // contact direction throughout moving recovery. Root movement
                // may strafe independently; no mid-cast facing snap.
                if(playerAction!=null)return playerAction.direction;
                return ActuallyMoving?locomotionFacing:aim;
            }
        }
        public const float EnemyMeleeRadius=1.5f;
        public const float EnemyMeleeCosine=-.1f;
        public static float EnemyAreaRadius(Enemy enemy)
        { return enemy.boss?2.2f+enemy.BossPhase*.2f:EnemyMeleeRadius; }
        public static bool InEnemyMeleeArc(Enemy enemy,Point target)
        { Point delta=target-enemy.Position;return delta.Length<EnemyMeleeRadius&&delta.Normalized.x*enemy.facing.x+delta.Normalized.y*enemy.facing.y>EnemyMeleeCosine; }
        public void SetAim(Point direction)
        { if (Finite(direction.x) && Finite(direction.y) && direction.Length > .05f) aim = direction.Normalized; }
        // Hold Shift to move faster at an actual energy cost. Fatigue is a
        // transient latch: once exhausted, recovery to 35 energy is required
        // before another burst. No new field or migration in SaveData v2.
        public bool Sprinting { get; private set; }
        public bool SprintFatigued { get; private set; }
        public const float SprintEnergyRate=22f;
        public const float SprintSpeedFactor=1.5f;
        public const float SprintRestartEnergy=35f;
        private void ResetContinuity()
        {
            walkDistance = gateOpeningTime = 0; footContacts = 0;
            locomotionFacing = aim; aimTarget=null;
            Sprinting=false;SprintFatigued=false;
        }
        private void TickMovement(float dt, Point movement, Point? explicitAim,
            float hitLockedDt,bool sprintHeld)
        {
            Point start=Position;
            Sprinting=false;
            if(SprintFatigued&&save.energy>=SprintRestartEnergy)
                SprintFatigued=false;
            bool wasDashing=dashTime>0;
            if(explicitAim.HasValue)SetAim(explicitAim.Value);
            else if(movement.Length>.1f &&
                (playerAction==null||PlayerPhase==ActionPhase.Recovery))
                SetAim(movement);

            // A dodge may finish partway through the current simulation
            // slice. Spend exactly its remaining interval at dash speed and
            // return the unused time to regular player locomotion, rather
            // than discarding up to 50ms of a held walk or sprint command.
            float available=dt, dashSlice=0f;
            if(wasDashing)
            {
                dashSlice=Math.Min(dt,dashTime);
                dashTime=Math.Max(0f,dashTime-dashSlice);
                if(dashTime<.00001f)dashTime=0f;
                Move(dashDirection*dashSlice*15f);
                available-=dashSlice;
            }
            Point movementStart=Position;
            if(available>.000001f)
            {
                // Recoil and control are likewise separate time slices.
                // A corrupt/transient hit lock must not be double-counted
                // over time already spent in the dodge phase.
                float locked=Math.Min(available,
                    Math.Max(0f,hitLockedDt-dashSlice));
                if(locked>0f)
                    Move(playerRecoil*locked*recoilSpeed);
                movementStart=Position;
                float freeDt=available-locked;
                if(freeDt>.000001f)
                {
                    MobilityRule mobility=PlayerMobility;
                    if(mobility==MobilityRule.Full||
                       mobility==MobilityRule.Reduced)
                    {
                        // Sprint never speeds up a cast, planted combat
                        // recovery, recoil, dash, or an invalid actor root.
                        bool sprint=sprintHeld&&!practicing&&
                            playerAction==null&&
                            mobility==MobilityRule.Full&&
                            movement.Length>.1f&&!SprintFatigued&&
                            save.energy>=10f&&CanStand(Position);
                        Point beforeWalk=Position;
                        float factor=sprint?SprintSpeedFactor:
                            mobility==MobilityRule.Reduced?.7f:1f;
                        Move(movement*freeDt*Speed*factor);
                        if(sprint)
                        {
                            float walked=(Position-beforeWalk).Length;
                            float planned=movement.Length*freeDt*Speed*factor;
                            // Charge for actual footsteps only: a sealed
                            // gate, body obstacle or blocked wall cannot
                            // siphon energy while the player is stationary.
                            if(walked>.0001f&&planned>.0001f)
                            {
                                Sprinting=true;
                                float portion=Clamp(walked/planned,0f,1f);
                                save.energy=Math.Max(0f,save.energy-
                                    SprintEnergyRate*freeDt*portion);
                                if(save.energy<10f)SprintFatigued=true;
                            }
                        }
                    }
                    if(mobility==MobilityRule.AttackMovement)
                    {
                        ActorAction action=playerAction;
                        float previous=Clamp(action.elapsed/
                            Math.Max(.01f,action.contact),0,1);
                        float next=Clamp((action.elapsed+freeDt)/
                            Math.Max(.01f,action.contact),0,1);
                        float stride=(action.kind=="skill"?.45f:
                            action.combo==2?.42f:.34f)*
                            (next*next-previous*previous);
                        MoveAttackStep(action.direction*stride);
                    }
                }
            }
            Point actual=Position-start;
            // Distinguish the physical dash (never a planted footstep)
            // from authentic walking during its final partial frame.
            // Only the latter advances gait, sprint cost and walk facing.
            Point controlled=Position-movementStart;
            bool walkedAfterDash=wasDashing&&available>.000001f&&
                controlled.Length>.001f;
            Point walking=wasDashing&&!walkedAfterDash?actual:controlled;
            movementDirection=walking;
            if(wasDashing&&!walkedAfterDash&&dashTime<=0f&&actual.Length>.001f)
                locomotionFacing=dashDirection;
            bool translating=walking.Length>.001f&&
                (!wasDashing||walkedAfterDash)&&
                playerHitTime<=0f&&(playerAction==null||
                    PlayerPhase==ActionPhase.Recovery);
            if(translating)
            {
                locomotionFacing=walking.Normalized;
                if(playerAction==null)
                {
                    int before=(int)Math.Floor(
                        walkDistance/(WalkStride/2f));
                    walkDistance+=walking.Length;
                    footContacts+=(int)Math.Floor(
                        walkDistance/(WalkStride/2f))-before;
                }
            }
        }
        private void MoveAttackStep(Point delta)
        {
            // A sword step stops as a whole at an obstruction. Axis sliding
            // here can repeatedly pull a navigating player back into a corner.
            int steps = Math.Max(1,(int)Math.Ceiling(delta.Length/.08f)); Point step = delta*(1f/steps);
            for(int i=0;i<steps;i++) { if(!Fits(Position+step)) break; save.x += step.x; save.y += step.y; }
        }
        public bool HasArenaBoundary { get { return save.room == 1 || save.room == 2 || save.room == 3 || save.room == 5 || save.room == 6 || save.room == 7; } }
        public bool GateBarrierActive { get { return HasArenaBoundary && (!GateOpen || gateOpeningTime > 0); } }
        public bool InProtectedArena(Point p)
        { return HasArenaBoundary && p.x >= 26 && p.x < 34 && p.y >= 2 && p.y < 14; }
        private void TickGate(float dt) { if (gateOpeningTime > 0) gateOpeningTime = Math.Max(0, gateOpeningTime - dt); }

        // Ground-space origin plus a fixed draw height. Both release sparks and
        // missiles use this exact attachment, including vertical bow poses.
        public static Point WeaponOrigin(Point feet, int weapon, Point direction, bool enemy = false)
        {
            Point d = direction.Length > .05f ? direction.Normalized : new Point(1, 0);
            int row = DirectionRow(d);
            if (row == 1) return feet + new Point(weapon == 1 ? -.6f : -.48f, -.48f);
            if (row == 2) return feet + new Point(weapon == 1 ? .23f : .66f, .42f);
            // Continuous missile aim cannot move a four-direction body's muzzle.
            return feet+new Point((d.x<0?-1:1)*(weapon==1?.82f:1.35f),0);
        }
        public static float WeaponHeight(int weapon, Point direction)
        { return DirectionRow(direction) == 0 ? weapon == 1 ? 36 : 35 : DirectionRow(direction) == 1 ? weapon == 1 ? 35 : 65 : weapon == 1 ? 32 : 82; }
        // A public hearth/field refuge is a physical service, not a
        // radius-only global buff through a wall. Share E's near-side
        // .55-tile endpoint margin so authored wall-mounted instruments
        // still work when approached from their actual usable side.
        private bool PhysicalRefugeReach(Point site,float radius)
        {
            Point delta=site-Position;
            float distance=delta.Length;
            if(distance>=radius)return false;
            Point contact=distance>.55f?
                site-delta.Normalized*.55f:Position;
            return ClearAttackPath(Position,contact);
        }
        public static float BossEmissionHeight(int room)
        { return room==1?64:room==2?68:room==3?80:room==4?72:room==5?50:room==6?94:room==7?78:102; }
        public static Point AimFromWeapon(Point feet,int weapon,Point target)
        { Point direction=(target-feet).Normalized;return (target-WeaponOrigin(feet,weapon,direction)).Normalized; }
        private Projectile WeaponShot(Point feet, int weapon, Point direction, float speed, float damage, Point? target=null)
        {
            var shot = new Projectile { position = feet, velocity = direction.Normalized * speed, damage = damage,
                height = WeaponHeight(weapon, direction),
                // Select the actual collision radius before muzzle sweep,
                // instead of treating every not-yet-named projectile as a
                // point and making the arrow head overlap solid scenery.
                visual = weapon == 1 ? "arrow" : "starfire" };
            shot.launchHeight=shot.height;shot.flightHeight=weapon==2?39:shot.height;
            Point attachment = WeaponOrigin(feet, weapon, direction), delta = attachment - feet;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .04f));
            // The muzzle and the later flight MUST share the same full
            // projectile footprint. Thin wall edges must stop the leading
            // edge, not just the center after it has pierced the wall.
            // The origin is the HERO'S center; the actual bow/staff release
            // is farther forward at the hand. A nearby wall *behind* the
            // character can overlap a staff-sized orb at its theoretical
            // feet and wrongly cancel a perfectly clear outward cast.
            // Sweep the physical radius after half a tile of attachment
            // travel. Always check the center from the FIRST step so a
            // wall directly ahead cannot be tunneled through.
            for (int i = 1; i <= steps && !shot.blockedAtSpawn; i++)
            {
                Point next = feet + delta * (i / (float)steps);
                bool pastBody = (next - feet).Length >= .5f;
                if (Blocked(next.x, next.y) ||
                    pastBody && ShotBlocked(shot, next))
                { shot.blockedAtSpawn = true; break; }
                shot.position = next;
            }
            if(target.HasValue&&(target.Value-shot.position).Length>.01f)shot.velocity=(target.Value-shot.position).Normalized*speed;
            return shot;
        }
        private Projectile FriendlyShot(Point feet, int weapon, Point direction, float speed, float damage, bool explosive = false, Point? target=null, ImpactProfile impact=null)
        {
            Projectile shot = WeaponShot(feet, weapon, direction, speed, damage, target);
            shot.friendly = true; shot.explosive = explosive; shot.visual = weapon == 1 ? "arrow" : "starfire";
            shot.impact = impact ?? CaptureImpact(weapon); return shot;
        }
        // Killing one real guardian retracts ONLY missiles actually
        // released by that precise live actor. Another raider, a second
        // Boss with a recycled spawn ID, or a sword-reflected bolt remains
        // an honest combat event. Never use a numeric ID to determine owner.
        private void RetireDefeatedGuardianProjectiles(Enemy defeated)
        {
            if(defeated==null)return;
            // Legacy/scripted missiles may not have a shooter reference.
            // After the LAST living foe has truly fallen, those orphan
            // hostile contacts can no longer be attributed to an active
            // encounter and must not haunt the cleared arena. With a
            // surviving attacker, retain them; its own live shots certainly
            // must not disappear when another actor's Boss is defeated.
            bool noLivingFoes=!enemies.Exists(e=>e!=null&&e.hp>0f);
            foreach(Projectile shot in projectiles.ToArray())
            {
                if(shot==null||shot.friendly||
                    !(Object.ReferenceEquals(shot.hostileShooter,defeated)||
                      noLivingFoes&&shot.hostileShooter==null))
                    continue;
                projectiles.Remove(shot);
                effects.Add(new Effect("projectileFade",shot.position,.25f)
                { direction=shot.velocity.Normalized,
                  variant=shot.visual=="enemyArrow"?8:10,
                  height=shot.height });
            }
        }
        // A single starfire explosion may kill the Boss first and its
        // supporting raiders later in the SAME projectile contact loop.
        // At the final true death, retire only legacy unowned hostile shots:
        // no live combatant remains to make their presence meaningful.
        // Already-fired arrows with a real shooter identity are not erased.
        private void RetireOrphanShotsAfterMainBossArenaClears()
        {
            if(enemies.Exists(e=>e!=null&&e.hp>0f))return;
            foreach(Projectile shot in projectiles.ToArray())
            {
                if(shot==null||shot.friendly||shot.hostileShooter!=null)
                    continue;
                projectiles.Remove(shot);
                effects.Add(new Effect("projectileFade",shot.position,.25f)
                {direction=shot.velocity.Normalized,
                 variant=shot.visual=="enemyArrow"?8:10,
                 height=shot.height});
            }
        }
        private void RetireHostileProjectiles(Point? center = null, float radius = 0)
        {
            foreach (Projectile p in projectiles.ToArray())
                if (!p.friendly && (!center.HasValue || (p.position - center.Value).Length < radius))
                { projectiles.Remove(p); effects.Add(new Effect("projectileFade", p.position, .25f) { direction = p.velocity.Normalized, variant = p.visual == "enemyArrow" ? 8 : 10, height = p.height }); }
        }
        private bool ShotBlocked(Projectile shot,Point point)
        {
            float r=shot.Radius,s=r*.7071f;
            return Blocked(point.x,point.y)||Blocked(point.x-r,point.y)||Blocked(point.x+r,point.y)||Blocked(point.x,point.y-r)||Blocked(point.x,point.y+r)||Blocked(point.x-s,point.y-s)||Blocked(point.x+s,point.y-s)||Blocked(point.x-s,point.y+s)||Blocked(point.x+s,point.y+s);
        }
        private Point SweepActor(Point start, Point delta)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .1f)); Point p = start, step = delta * (1f / steps);
            for (int i = 0; i < steps; i++)
            { if (Fits(p + new Point(step.x, 0))) p.x += step.x; if (Fits(p + new Point(0, step.y))) p.y += step.y; }
            return p;
        }
    }
}
