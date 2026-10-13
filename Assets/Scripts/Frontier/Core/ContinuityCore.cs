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
                if (Dying || playerHitTime > 0 || playerAction != null && playerAction.kind == "skill" && playerAction.weapon != 0) return MobilityRule.Stationary;
                if (playerAction == null) return MobilityRule.Full;
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
                if(playerAction!=null&&!RecoveryWalking)return playerAction.direction;
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
        private void TickMovement(float dt, Point movement, Point? explicitAim)
        {
            Point start = Position; bool wasDashing = dashTime > 0;
            if (explicitAim.HasValue) SetAim(explicitAim.Value);
            else if (movement.Length > .1f && (playerAction == null || PlayerPhase == ActionPhase.Recovery)) SetAim(movement);
            if (dashTime > 0)
            {
                // Consume only the remaining dash interval: a coarse final tick
                // must not add extra travel beyond the authored 0.18 seconds.
                float dashStep = Math.Min(dt, dashTime);
                dashTime = Math.Max(0, dashTime - dt);
                // Avoid a phantom sub-microsecond dash tick from float rounding:
                // the final visible translation must also finish the dodge.
                if (dashTime < .00001f) dashTime = 0;
                Move(dashDirection * dashStep * 15);
            }
            else if (playerHitTime > 0) Move(playerRecoil * dt * recoilSpeed);
            else
            {
                MobilityRule mobility = PlayerMobility;
                if (mobility == MobilityRule.Full || mobility == MobilityRule.Reduced) Move(movement * dt * Speed * (mobility == MobilityRule.Reduced ? .7f : 1));
                if (mobility == MobilityRule.AttackMovement)
                {
                    ActorAction a = playerAction;
                    // The authored sword contact moves the body forward. Sweep
                    // every step; it cannot cross gates, trees or wall corners.
                    float previous = Clamp(a.elapsed / Math.Max(.01f, a.contact), 0, 1);
                    float next = Clamp((a.elapsed + dt) / Math.Max(.01f, a.contact), 0, 1);
                    float step = (a.kind == "skill" ? .45f : a.combo == 2 ? .42f : .34f) * (next * next - previous * previous);
                    MoveAttackStep(a.direction * step);
                }
            }
            Point actual = Position - start;
            movementDirection = actual;
            // The final dodge tick still draws moving legs after dashTime reaches zero.
            // Carry the dodge heading into that frame instead of an older walk heading.
            if (wasDashing && dashTime <= 0 && actual.Length > .001f)
                locomotionFacing = dashDirection;
            bool walking = actual.Length > .001f && !wasDashing && playerHitTime <= 0 && (playerAction == null || PlayerPhase == ActionPhase.Recovery);
            if (walking)
            {
                locomotionFacing = actual.Normalized;
                int before = (int)Math.Floor(walkDistance / (WalkStride / 2));
                walkDistance += actual.Length;
                footContacts += (int)Math.Floor(walkDistance / (WalkStride / 2)) - before;
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
        public static float BossEmissionHeight(int room)
        { return room==1?64:room==2?68:room==3?80:room==4?72:room==5?50:room==6?94:room==7?78:102; }
        public static Point AimFromWeapon(Point feet,int weapon,Point target)
        { Point direction=(target-feet).Normalized;return (target-WeaponOrigin(feet,weapon,direction)).Normalized; }
        private Projectile WeaponShot(Point feet, int weapon, Point direction, float speed, float damage, Point? target=null)
        {
            var shot = new Projectile { position = feet, velocity = direction.Normalized * speed, damage = damage, height = WeaponHeight(weapon, direction) };
            shot.launchHeight=shot.height;shot.flightHeight=weapon==2?39:shot.height;
            Point attachment = WeaponOrigin(feet, weapon, direction), delta = attachment - feet;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .04f));
            // An extended bow/staff cannot spawn a missile beyond a thin wall.
            // Keep its first wall contact for the impact, even at point blank.
            for (int i = 1; i <= steps; i++)
            {
                Point next = feet + delta * (i / (float)steps);
                if (Blocked(next.x, next.y)) { shot.blockedAtSpawn = true; break; }
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
