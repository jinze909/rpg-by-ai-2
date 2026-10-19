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
        private void ResetContinuity()
        { walkDistance = gateOpeningTime = 0; footContacts = 0; locomotionFacing = aim; aimTarget=null; }
        private void TickMovement(float dt, Point movement, Point? explicitAim,
            float hitLockedDt)
        {
            Point start=Position;
            bool wasDashing=dashTime>0;
            if(explicitAim.HasValue)SetAim(explicitAim.Value);
            else if(movement.Length>.1f &&
                (playerAction==null||PlayerPhase==ActionPhase.Recovery))
                SetAim(movement);

            Point movementStart=start;
            if(dashTime>0)
            {
                // Use only the remaining dash interval on the final frame.
                float dashStep=Math.Min(dt,dashTime);
                dashTime=Math.Max(0f,dashTime-dt);
                if(dashTime<.00001f)dashTime=0f;
                Move(dashDirection*dashStep*15f);
            }
            else
            {
                // Hit recoil and player control are disjoint slices of the
                // SAME frame. Previous TickVisuals consumed the hit timer
                // first, granting an unearned entire tick of walking/swing
                // and suppressing the last frame of actual knockback.
                float locked=Math.Min(dt,Math.Max(0f,hitLockedDt));
                if(locked>0f)
                    Move(playerRecoil*locked*recoilSpeed);
                movementStart=Position;
                float freeDt=dt-locked;
                if(freeDt>.000001f)
                {
                    MobilityRule mobility=PlayerMobility;
                    if(mobility==MobilityRule.Full||
                       mobility==MobilityRule.Reduced)
                        Move(movement*freeDt*Speed*
                            (mobility==MobilityRule.Reduced?.7f:1f));
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
            // Only input-driven translation is locomotion. A final recoil
            // sample may move the world root, but must not make the hero
            // snap into Run, change walk facing or invent a footstep.
            Point walking=wasDashing?actual:Position-movementStart;
            movementDirection=walking;
            if(wasDashing&&dashTime<=0f&&actual.Length>.001f)
                locomotionFacing=dashDirection;
            bool translating=walking.Length>.001f&&!wasDashing&&
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
