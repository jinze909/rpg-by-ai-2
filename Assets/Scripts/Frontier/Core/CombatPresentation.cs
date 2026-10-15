using System;
using System.Collections.Generic;

namespace Starfall
{
    // One simulation clock drives poses and damage. Rendering never dispatches damage.
    public sealed class ActorAction
    {
        public string kind;
        public float elapsed, duration, contact, damage;
        public int weapon, equippedWeapon, profession, style, combo, technique;
        public Point direction;
        public Point? target;
        public bool released;
        public ImpactProfile impact;
        public float Progress { get { return GameCore.Clamp(elapsed / duration, 0, 1); } }
    }
    public sealed class FallenActor
    {
        public Enemy actor;
        public int room;
        public float life, duration;
    }
    public sealed partial class GameCore
    {
        // A single short-lived intent bridges the end of a swing or dodge.
        // It is never saved, and captures combat values only when the cast starts.
        public const float SkillBufferWindow = .20f;
        private sealed class SkillIntent
        {
            public float remaining;
            public Point direction;
            public Point? target;
            public int weapon, profession, style, technique;
        }
        private SkillIntent bufferedSkill;
        // A single late basic-attack press may carry through the tail of its
        // current swing. The intent is never serialized and spends nothing until
        // the next swing is actually accepted.
        public const float AttackBufferWindow = .16f;
        private sealed class AttackIntent
        {
            public float remaining;
            public Point direction;
            public Point? target;
            public int weapon, profession, style, technique;
        }
        private AttackIntent bufferedAttack;
        private void ClearBufferedAttack() { bufferedAttack = null; }
        private void QueueLateAttack(Point direction)
        {
            ActorAction action = playerAction;
            if (action == null || action.kind != "attack" || !action.released ||
                playerHitTime > 0 || dashTime > 0 || bufferedSkill != null) return;
            float untilReady = Math.Max(attackCooldown, Math.Max(0, action.duration - action.elapsed));
            if (untilReady > AttackBufferWindow + .00001f) return;
            Point facing = direction.Length > .05f ? direction.Normalized : aim;
            bufferedAttack = new AttackIntent { remaining = AttackBufferWindow,
                direction = facing, target = aimTarget, weapon = save.weapon,
                profession = save.profession, style = Style, technique = save.technique };
        }
        private void TickBufferedAttack(float dt, float readyDelay)
        {
            AttackIntent intent = bufferedAttack;
            if (intent == null) return;
            float previousRemaining = intent.remaining;
            intent.remaining -= dt;
            if (bufferedSkill != null || intent.weapon != save.weapon ||
                intent.profession != save.profession || intent.style != Style ||
                intent.technique != save.technique)
            { ClearBufferedAttack(); return; }
            // Compare readiness with the window *before* this simulation step
            // so a valid 30/50ms boundary input is neither lost nor extended.
            if (attackCooldown <= .00001f && playerAction == null &&
                !ActionTransitionBlocked && readyDelay <= previousRemaining + .00001f)
            {
                ClearBufferedAttack();
                Point? liveTarget = aimTarget;
                try { aimTarget = intent.target; Attack(intent.direction); }
                finally { aimTarget = liveTarget; }
            }
            else if (intent.remaining <= .00001f) ClearBufferedAttack();
        }
        public bool ActionTransitionBlocked
        {
            get { return dashTime > 0 || hitStop > 0 || playerHitTime > 0 || playerAction != null && PlayerPhase != ActionPhase.Recovery; }
        }
        public void ClearBufferedSkill() { bufferedSkill = null; }
        private float SkillReadyDelay()
        {
            float delay = Math.Max(dashTime, playerHitTime);
            ActorAction action = playerAction;
            if (action != null)
            {
                float actionDelay = Math.Max(0, action.contact + .1f - action.elapsed);
                if (!action.released) actionDelay += playerHitTime;
                delay = Math.Max(delay, actionDelay);
            }
            return delay;
        }
        private void TickBufferedSkill(float dt, float readyDelay)
        {
            SkillIntent intent = bufferedSkill;
            if (intent == null) return;
            // A contact reached during this tick may have started hit stop.
            // Preserve that pose; dispatch before the next held basic attack.
            float remaining = intent.remaining;
            intent.remaining -= dt;
            // Recovery may fall inside the final valid tick. Compare its actual
            // boundary with the old lifetime, rather than losing Q at 30 fps.
            if (!ActionTransitionBlocked && readyDelay <= remaining + .00001f) StartSkill(intent.direction, intent.target);
            else if (intent.remaining <= .00001f) ClearBufferedSkill();
        }
        public ActorAction playerAction;
        public ActorAction companionAction;
        public Point companionAim;
        public bool companionMoving;
        public float companionMoveTime;
        public readonly List<FallenActor> fallen = new List<FallenActor>();
        public float playerHitTime, playerFlash, deathTime, hitStop, impactShake;
        public Point playerRecoil, movementDirection;
        private float finaleDelay;
        private int fallenPlayerWeapon=-1;
        private Point fallenPlayerFacing;
        private Point playerHitFacing;
        private int playerHitWeapon = -1;
        public bool Dying { get { return deathTime > 0; } }
        public int AppearanceWeapon
        {
            get
            {
                if (Dying && fallenPlayerWeapon >= 0) return fallenPlayerWeapon;
                if (playerHitTime > 0 && playerHitWeapon >= 0) return playerHitWeapon;
                // A released action still owns the whole-body drawing while
                // its recovery permits root translation. Never snap between
                // skill armor and an unrelated equipped Run costume mid-cast.
                // Hit and death appearance overrides above remain authoritative.
                return playerAction != null ? playerAction.weapon : save.weapon;
            }
        }

        private void ResetCombatPresentation()
        {
            ClearBufferedSkill(); ClearBufferedAttack();
            playerAction = null; fallen.Clear(); playerHitTime = playerFlash = deathTime = hitStop = impactShake = finaleDelay = 0;
            playerRecoil = movementDirection = new Point();
            fallenPlayerWeapon=-1;fallenPlayerFacing=new Point();
            playerHitFacing=new Point(); playerHitWeapon = -1;
        }
        private void CancelAction() { playerAction = null; ClearBufferedSkill(); ClearBufferedAttack(); }
        private void ValidateActionProfile()
        {
            ActorAction a = playerAction;
            if (a != null && (a.equippedWeapon != save.weapon || a.profession != save.profession || a.style != Style || a.technique != save.technique)) CancelAction();
            SkillIntent intent = bufferedSkill;
            if (intent != null && (intent.weapon != save.weapon || intent.profession != save.profession || intent.style != Style || intent.technique != save.technique)) ClearBufferedSkill();
            AttackIntent attack = bufferedAttack;
            if (attack != null && (attack.weapon != save.weapon || attack.profession != save.profession ||
                attack.style != Style || attack.technique != save.technique)) ClearBufferedAttack();
        }
        private void BeginAction(string kind, float damage, float duration, float contact)
        {
            // The next visible Run always begins with a fresh, stable
            // canonical foot phase. There is no Walk raster under an action
            // and no accumulated hidden gait during moving recovery.
            // Do not reset footContacts (global material/audio event count).
            walkDistance = 0f;
            playerAction = new ActorAction { kind = kind, damage = damage, duration = duration, contact = contact,
                direction = aim, target = aimTarget, weapon = kind == "skill" ? save.profession : save.weapon, equippedWeapon = save.weapon, profession = save.profession, style = Style, combo = combo, technique = save.technique,
                impact = CaptureImpact(kind == "skill" ? save.profession : save.weapon,kind == "attack" ? combo : 0) };
        }
        private void TickAction(float dt)
        {
            ValidateActionProfile();
            ActorAction a = playerAction;
            if (a == null) return;
            // A recoil pose cannot hide a still-pending contact frame. Resume the
            // preparation after recoil, then release damage on the visible pose.
            if (!a.released && playerHitTime > 0) return;
            a.elapsed += dt;
            if (!a.released && a.elapsed + .00001f >= a.contact)
            {
                a.released = true;
                if (a.kind == "attack") ReleaseAttack(a); else ReleaseSkill(a);
            }
            if (playerAction == a && a.elapsed >= a.duration) playerAction = null;
        }
        private void ReleaseAttack(ActorAction a)
        {
            sound = a.weapon == 0 ? "attack" : a.weapon == 1 ? "arrow" : "spell";
            if (a.weapon > 0)
            {
                int spread = a.weapon == 1 && a.style == 2 ? 1 : 0;
                for (int i = -spread; i <= spread; i++)
                {
                    Projectile shot = FriendlyShot(Position, a.weapon, a.direction, a.weapon == 1 ? 12 : 8, a.damage * (spread > 0 ? .65f : 1), a.weapon == 2, a.target, a.impact);
                    double angle = Math.Atan2(shot.velocity.y, shot.velocity.x) + i * .18;
                    Point direction = new Point((float)Math.Cos(angle), (float)Math.Sin(angle));
                    shot.velocity = direction * (a.weapon == 1 ? 12 : 8);
                    // Balanced bow rewards a completed chain with one real
                    // penetrating shot; Focused already pierces every release.
                    // Flow retains its authored spread without triple piercing.
                    shot.piercing = a.weapon == 1 && (a.style == 1 || a.style == 0 && a.combo == 2);
                    // The starstaff's third connected cast briefly arrests a
                    // target using the existing .45s slow/contact mechanic.
                    shot.slowing = a.weapon == 2 && (a.style == 2 || a.combo == 2);
                    projectiles.Add(shot);
                }
                effects.Add(new Effect("release", WeaponOrigin(Position, a.weapon, a.direction), .18f) { direction = a.direction, variant = a.weapon, height = WeaponHeight(a.weapon, a.direction) });
                if (a.combo == 2)
                    effects.Add(new Effect("number", Position + new Point(0, -.75f), .6f, L("连携终结", "CHAIN FINISH")));
                return;
            }
            effects.Add(new Effect("slash", Position + a.direction * .7f, .22f) { direction = a.direction, variant = a.combo });
            foreach (Enemy e in enemies.ToArray())
            {
                Point d = e.Position - Position;
                if (d.Length < (a.combo == 2 ? 2.2f : 1.85f) && d.Normalized.x * a.direction.x + d.Normalized.y * a.direction.y > -.15f && ClearAttackPath(Position, e.Position)) Hit(e, a.damage,false,null,a.impact);
            }
        }
        private void ReleaseSkill(ActorAction a)
        {
            Point previous = aim; aim = a.direction;
            try { ResolveSkill(a); } finally { aim = previous; }
        }
        private void TickVisuals(float dt)
        {
            playerHitTime = Math.Max(0, playerHitTime - dt); playerFlash = Math.Max(0, playerFlash - dt);
            impactShake = Math.Max(0, impactShake - dt);
            for(int i=fallen.Count-1;i>=0;i--){FallenActor body=fallen[i];body.life-=dt;if(body.life<=0)fallen.RemoveAt(i);}
            // A late boss-death animation can coincide with the delayed
            // ending prompt. Never open dialogue during a death pose: doing so
            // stops the world-only recovery clock and strands the player dead.
            if (finaleDelay > 0 && mode == "world" && !Dying)
            { finaleDelay -= dt; if (finaleDelay <= 0 && save.room == 4 && Has("boss_4")) FinalConversation(); }
        }
        private void BeginDeath()
        {
            fallenPlayerWeapon=AppearanceWeapon;fallenPlayerFacing=VisualFacing;
            CancelAction(); companionAction = null; dashTime = 0; deathTime = .9f; hitStop = 0;
            Flag("recovery_pending"); // A quit during the death pose resumes safely at the inn.
        }
        private void CompleteDeath()
        {
            bool abyss = InAbyss;
            if (abyss) EndAbyss(false);
            save.flags.Remove("recovery_pending"); save.health = save.maxHealth; save.energy = 100;
            // Death is a mandatory recovery transaction, NOT voluntary
            // portal travel. Enter(0) refuses active escorts/defense tasks and
            // used to leave a dead player's body in the hostile region while
            // showing the false "returned to inn" message. Explicitly retire
            // transient combat/mission ownership and spawn at the home inn.
            save.room = 0; save.x = Map.spawnX; save.y = Map.spawnY;
            save.snapshotRoom = -1; save.worldEnemies = null; save.savedRunes = 0;
            ResetTransient(); SpawnRoom();
            mode = "world"; revision++; sound = "travel";
            Notify("镇民把你带回了旅店。失去 10 硬币，物品和任务进度保留。", "The town brought you home. Lost 10 coins; items and quests are safe.");
            if (abyss) Notify("远征中止：本层未结算收益丢失，先前结算与永久进度保留。", "Expedition ended: this floor's unbanked loot is lost; banked rewards and permanent progress remain.");
        }
        // Single authoritative action state for both the old rooted OnGUI
        // renderer and the new live SpriteRenderer/Animator bridge.
        // Seven actions: 0 idle, 1 run, 2 attack, 3 skill/cast,
        // 4 dodge, 5 hit, 6 death. No new save fields or damage clock.
        public int HeroAnimationState
        {
            get
            {
                if (Dying) return 6;
                if (dashTime > 0) return 4;
                if (playerHitTime > 0) return 5;
                int pose = PlayerPose;
                if (playerAction != null && pose >= 4 && pose <= 7)
                    return playerAction.kind == "skill" ? 3 : 2;
                if (pose == 2 || pose == 3) return 1;
                return 0;
            }
        }
        // Sprite frames are selected from the SAME clock that releases actual
        // attack damage. The GPU Animator must not invent its own release time.
        // Facing: 0 E/W, 1 north, 2 south; left mirrors the existing E art.
        public int HeroAnimationFrameCount(int facing)
        {
            int state = HeroAnimationState;
            if (state == 1) return 4;
            if (state == 2) return 4;
            // Rooted Idle -> CastWindup -> CastContact -> IdleRecovery.
            // Damage remains controlled by ActorAction.released.
            if (state == 3) return 4;
            // Reactions use stand -> impact -> stand, and death keeps a
            // distinct final grounded pose. All share GameCore's clock.
            if (state == 6) return 4;
            if (state == 4 || state == 5) return 3;
            return 1;
        }
        public int HeroAnimationFrame(int facing)
        {
            int state = HeroAnimationState, count = HeroAnimationFrameCount(facing);
            if (count <= 1) return 0;
            if (state == 1) return WalkFrame % 4;
            if (state >= 4)
                return (int)Math.Min(count - 1,
                    Math.Floor(HeroAnimationPlaybackPosition(facing)));
            ActorAction action = playerAction;
            if (action == null) return 0;
            if (!action.released)
            {
                if (count == 2) return 0;
                return action.elapsed < action.contact * .55f ? 0 : 1;
            }
            if (count == 2) return 1;
            return action.elapsed - action.contact < .12f ? 2 : 3;
        }
        // Read-only pose interpolation: contact and damage are still solely
        // determined by ActorAction. No change to SaveData or combat clock.
        public float HeroAnimationPlaybackPosition(int facing)
        {
            int state = HeroAnimationState;
            int count = HeroAnimationFrameCount(facing);
            if (state == 1) return WalkPosePhase;
            if (state == 4) return Clamp((.18f - dashTime) / .18f * 2f, 0f, 2f);
            if (state == 5) return Clamp((.18f - playerHitTime) / .18f * 2f, 0f, 2f);
            if (state == 6)
            {
                float elapsed = .9f - deathTime;
                // Standing -> recoil -> crouch -> grounded corpse; hold the
                // final actual source pose, never loop a death animation.
                if (elapsed < .10f) return Clamp(elapsed / .10f, 0f, 1f);
                if (elapsed < .24f) return 1f + Clamp((elapsed - .10f) / .14f, 0f, 1f);
                return 2f + Clamp((elapsed - .24f) / .18f, 0f, 1f);
            }
            if ((state != 2 && state != 3) || playerAction == null)
                return HeroAnimationFrame(facing);
            ActorAction action = playerAction;
            float windup = Clamp(action.elapsed / Math.Max(.001f, action.contact), 0f, 1f);
            if (!action.released)
            {
                if (count == 2) return windup * .85f;
                // Idle -> windup -> ANTICIPATION midpoint -> actual contact.
                // Previously the entire source1-to-source2 in-between was
                // skipped: the player visibly snapped at damage release.
                // Sample its non-contact, source-windup silhouette during
                // the last 40% of preparation, leaving contact Sprite #4
                // exclusively for ActorAction.released.
                if (windup <= .60f) return windup / .60f;
                return 1f + .55f * Clamp((windup - .60f) / .25f, 0f, 1f);
            }
            if (count == 2) return 1f;
            return 2f + Clamp((action.elapsed - action.contact) / .18f, 0f, 1f);
        }

        // Exposes the exact contact boundary to native capture/tests without duplicating rules.
        public int PlayerPose
        {
            get
            {
                if (Dying) return 11;
                if (dashTime > 0) return 8;
                if (playerHitTime > 0) return 10;
                if (playerAction != null)
                {
                    // Movement in Recovery is already handled by GameCore's
                    // collision-safe Move() with reduced velocity. The visual
                    // actor MUST remain one whole attack/cast body until its
                    // original action duration finishes, even when strafe
                    // input is held. Old code returned WalkFrame here and
                    // instantly teleported chest, pelvis, weapon and outfit.
                    bool ready = PlayerPhase == ActionPhase.Windup;
                    bool heavy = playerAction.weapon == 0 && (playerAction.combo == 2 || playerAction.style == 1);
                    return playerAction.kind == "skill" || heavy ? (ready ? 6 : 7) : (ready ? 4 : 5);
                }
                if (guardTime > 0 && !ActuallyMoving) return 9;
                if (ActuallyMoving) return 2 + WalkFrame % 2;
                return aim.y < -.65f ? 1 : 0;
            }
        }
    }
}
