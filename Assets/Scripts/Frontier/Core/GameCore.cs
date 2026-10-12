using System;
using System.Collections.Generic;

namespace Starfall
{
    // All rules live outside MonoBehaviour so the complete campaign can be tested without an editor.
    public sealed partial class GameCore
    {
        public readonly Campaign data;
        public SaveData save;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public readonly List<Effect> effects = new List<Effect>();
        public Point aim = new Point(1, 0);
        public int revision, runeProgress;
        public float attackCooldown, dashTime, dashCooldown, invulnerable, skillCooldown, guardTime;
        public float fishTime, fishCursor, toastTime;
        public int fishHits, fishMisses;
        private string currentMode = "title";
        public string mode
        {
            get { return currentMode; }
            set { currentMode = value; if (value != "world") ClearBufferedSkill(); }
        }
        public string toast = "";
        public Conversation conversation;
        private Point dashDirection;
        private int enemyId;

        public GameCore(Campaign data, SaveData saved = null)
        {
            if (data == null || data.maps == null || data.maps.Length < 5) throw new ArgumentException("Campaign requires at least five maps.");
            this.data = data; Load(saved ?? new SaveData());
        }
        public Point Position { get { return new Point(save.x, save.y); } }
        public MapData Map { get { return save.room == 8 && abyssMap != null ? abyssMap : data.maps[save.room]; } }
        public BossData CurrentBoss { get { return save.room == 8 ? new BossData { name = "地脉收割者", en = "The Root Reaper", hp = 680 + save.abyssRisk * 140 } : data.bosses[Math.Min(data.bosses.Length - 1, Math.Max(0, save.room - 1))]; } }
        public string L(string zh, string en) { return save.english ? en : zh; }
        public static float Clamp(float x, float min, float max) { return Math.Max(min, Math.Min(max, x)); }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        public bool Has(string flag) { return save.flags.Contains(flag); }
        private void Flag(string flag)
        {
            if (Has(flag)) return;
            bool closed = !GateOpen; save.flags.Add(flag); revision++;
            if (closed && GateOpen && HasArenaBoundary)
            { gateOpeningTime = .55f; sound = "quest"; effects.Add(new Effect("gateOpen", new Point(26.5f, 13.5f), .65f)); }
        }
        public int Allies { get { int n = 0; for (int i = 1; i <= 3; i++) if (Has("ally_" + i)) n++; return n; } }
        public float Damage { get { return (19 + save.upgrades * 4 + (save.level - 1) * 2 + save.talents[0] * 3 + MasteryRank(save.weapon) * 2 + (save.relics[0] >= 0 || save.relics[1] >= 0 ? save.relicRank * 2 : 0) + (save.foodUntil > save.totalTime ? 7 : 0) + (Relic(1) && save.health >= save.maxHealth * .8f ? 6 : 0)) * (InAbyss ? 1 + save.boons[0] * .15f : 1); } }
        public float Speed { get { return 4.5f + (save.profession == 1 ? .45f : 0) + save.talents[2] * .12f + (Relic(2) ? .4f : 0) + (racing ? .65f : 0) + (Has("survey_complete") ? .25f : 0) - (save.cold > 70 ? .55f : 0); } }
        public bool GateOpen
        {
            get { return save.room == 0 || save.room == 4 || save.room == 8 || save.room == 9 || (save.room == 1 && Has("scout_saved")) || Has("gate_" + save.room); }
        }
        public void Load(SaveData value)
        {
            if (value.version != 1 && value.version != 2) throw new ArgumentException("Unsupported save version.");
            // Old-world opening effects cannot participate in selected-save collision.
            gateOpeningTime = 0;
            save = value;
            if (save.flags == null) save.flags = new List<string>();
            save.flags.RemoveAll(string.IsNullOrEmpty);
            if (save.inventory == null || save.inventory.Length != 8) save.inventory = new SaveData().inventory;
            if (save.talents == null || save.talents.Length != 3) save.talents = new int[3];
            if (save.kills == null) save.kills = new int[data.maps.Length];
            if (save.kills.Length != data.maps.Length) Array.Resize(ref save.kills, data.maps.Length);
            for (int i = 0; i < 8; i++) save.inventory[i] = Math.Max(0, Math.Min(9999, save.inventory[i]));
            for (int i = 0; i < 3; i++) save.talents[i] = Math.Max(0, Math.Min(5, save.talents[i]));
            for (int i = 0; i < save.kills.Length; i++) save.kills[i] = Math.Max(0, Math.Min(99999, save.kills[i]));
            save.stage = (int)Clamp(save.stage, 0, 3); save.room = save.room < 0 || save.room >= data.maps.Length ? 0 : save.room;
            SanitizeExpansion();
            SanitizeGuidance();
            SanitizePolish();
            if (Has("recovery_pending"))
            {
                if (save.abyssFloor > 0) EndAbyss(false);
                save.flags.Remove("recovery_pending"); save.room = 0; save.x = data.maps[0].spawnX; save.y = data.maps[0].spawnY;
                save.health = 9999; save.energy = 100; save.snapshotRoom = -1; save.worldEnemies = null;
            }
            if (!CanEnter(save.room)) save.room = 0;
            if (save.room == 8) GenerateAbyss();
            save.difficulty = (int)Clamp(save.difficulty, 0, 2); save.profession = (int)Clamp(save.profession, 0, 2);
            save.upgrades = (int)Clamp(save.upgrades, 0, 5); save.level = (int)Clamp(save.level, 1, 30);
            save.coins = (int)Clamp(save.coins, 0, 999999); save.weapon = (int)Clamp(save.weapon, 0, 2);
            save.skillPoints = (int)Clamp(save.skillPoints, 0, 99); save.ending = (int)Clamp(save.ending, 0, 3);
            save.maxHealth = 100 + save.upgrades * 20 + save.talents[1] * 12 + (save.profession == 0 ? 20 : 0);
            save.health = Finite(save.health) ? Clamp(save.health, 1, save.maxHealth) : save.maxHealth;
            save.energy = Finite(save.energy) ? Clamp(save.energy, 0, 100) : 100;
            save.totalTime = Finite(save.totalTime) ? Clamp(save.totalTime, 0, 10000000) : 0;
            save.plantedAt = Finite(save.plantedAt) ? Clamp(save.plantedAt, -1, save.totalTime) : -1;
            save.foodUntil = Finite(save.foodUntil) ? Clamp(save.foodUntil, 0, save.totalTime + 120) : 0;
            if (save.weapon > 0 && !Has("weapon_" + save.weapon)) save.weapon = 0;
            if (!Finite(save.x) || !Finite(save.y) || !Fits(Position)) { save.x = Map.spawnX; save.y = Map.spawnY; }
            ResetTransient(); SpawnRoom(); RestoreWorld(); revision++;
        }
        private void ResetTransient()
        {
            projectiles.Clear(); effects.Clear();
            attackCooldown = dashCooldown = dashTime = skillCooldown = guardTime = 0;
            invulnerable = 1; conversation = null; runeProgress = 0;
            ResetExpansionTransient(); ResetCombatPresentation();
            ResetContinuity(); ResetRoadwardenTrial();
        }
        public void NewGame(int profession)
        {
            profession = (int)Clamp(profession, 0, 2);
            SaveData fresh = new SaveData { english = save.english, music = save.music, difficulty = save.difficulty, reducedMotion = save.reducedMotion, profession = profession, weapon = profession, presentationVersion = 1, showGuidance = save.showGuidance, largeText = save.largeText, effectsQuality = save.effectsQuality, polishVersion = 1, musicVolume = save.musicVolume, effectsVolume = save.effectsVolume, ambienceVolume = save.ambienceVolume };
            if (profession > 0) fresh.flags.Add("weapon_" + profession);
            fresh.health = 120; Load(fresh); mode = "world"; Intro();
        }
        public void Start() { mode = "world"; if (!Has("intro")) Intro(); }
        private Choice C(string zh, string en, string action) { return new Choice(zh, en, action); }
        private void Intro()
        {
            Flag("intro");
            Dialog("莉雅 · 公会接待员", "Lyra · Guild Steward",
                "你带着巡路员托付的旧信来到余烬镇。收信人莉雅没等到那支巡路队，却还在照顾他们留下的家人。\n\n星陨后的第七天，荆棘吞没道路。公会的侦察员仍困在北方密林，你决定先把他带回家。\n\n先找到他，再击败守护星核的巨兽，把星核交回公会。你不是一个人在冒险，镇里的药师、铁匠和旅店都会帮你。\n\nWASD 移动，鼠标左键或 J 攻击，空格闪避，Q 职业技能，E 交谈。Tab 打开任务手帐。",
                "You carry an old road warden's letter to Emberfall. Its recipient, Lyra, never saw that patrol return, but still looks after their families.\n\nSeven days after the star fell, thorns swallowed the roads. The guild scout is trapped in the woods. You decide to bring him home first.\n\nFind him, defeat the beast guarding the starcore, and return it to the guild. The herbalist, blacksmith and innkeeper will help you.\n\nWASD moves, left click or J attacks, Space dodges, Q uses your class skill, E interacts, and Tab opens your journal.", C("接下第一份委托", "Accept the first commission", "close"));
        }
        public void Notify(string zh, string en) { toast = L(zh, en); toastTime = 4; }
        private void Dialog(string speaker, string en, string text, string textEn, params Choice[] choices)
        { conversation = new Conversation(speaker, en, text, textEn, choices); mode = "dialog"; }
        public void Close() { conversation = null; mode = "world"; }
        public bool Blocked(float x, float y)
        {
            int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
            if (ty < 2 || tx < 2 || ty >= Map.rows.Length - 2 || tx >= Map.rows[0].Length - 2) return true;
            char tile = Map.rows[ty][tx];
            if (tile == '#' || tile == '~' || tile == 'f') return true;
            if ((tile == 'v' || tile == 'w' || tile == 'b') && !Has("seal_" + save.room)) return true;
            if (save.room == 5 && tile == '%' && !LowTide) return true;
            foreach (Decoration d in Map.decor)
                if (d.kind == "house" && Math.Abs(x - d.x) < 1.8f && y > d.y - 1.6f && y < d.y - .2f) return true;
            return GateBarrierActive && InProtectedArena(new Point(x, y));
        }
        private bool Fits(Point p)
        { return !Blocked(p.x - .22f, p.y - .18f) && !Blocked(p.x + .22f, p.y - .18f) && !Blocked(p.x - .22f, p.y + .18f) && !Blocked(p.x + .22f, p.y + .18f); }
        public bool CanStand(Point p) { return Finite(p.x) && Finite(p.y) && Fits(p); }
        private void Move(Point delta)
        {
            // Sweep the unchanged footprint during fast diagonal dodges, using
            // the same axis sliding as ordinary walking.
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
            Point step = delta * (1f / steps);
            for (int i = 0; i < steps; i++)
            {
                Point p = Position;
                if (Fits(new Point(p.x + step.x, p.y))) save.x += step.x;
                if (Fits(new Point(save.x, p.y + step.y))) save.y += step.y;
            }
        }
        private bool ClearAttackPath(Point from, Point to)
        {
            Point delta = to - from;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
            for (int i = 1; i <= steps; i++)
            { Point p = from + delta * (i / (float)steps); if (Blocked(p.x, p.y)) return false; }
            return true;
        }
        public Point DirectionTo(Point from, Point to)
        {
            Point direct = (to - from).Normalized;
            // A clear first step is insufficient: it can oscillate against a wall
            // instead of committing to the corridor around it (seeded abyss rooms).
            bool clear = true; float span = (to - from).Length;
            for (float step = .25f; step < span + .25f; step += .4f)
                if (!Fits(from + direct * Math.Min(step, span))) { clear = false; break; }
            if (clear) return direct;
            int width = Map.rows[0].Length, height = Map.rows.Length;
            int sx = (int)Math.Floor(from.x), sy = (int)Math.Floor(from.y);
            int tx = (int)Math.Floor(to.x), ty = (int)Math.Floor(to.y);
            if (sx < 0 || sy < 0 || tx < 0 || ty < 0 || sx >= width || tx >= width || sy >= height || ty >= height) return direct;
            int start = sy * width + sx, target = ty * width + tx;
            if (start == target) return direct;
            PreparePathSearch(width * height, -1);
            int[] parents = pathParents; int head = 0, tail = 0; pathQueue[tail++] = start; parents[start] = start;
            while (head < tail && parents[target] < 0)
            {
                int cell = pathQueue[head++], x = cell % width, y = cell / width;
                for (int i = 0; i < 4; i++)
                {
                    int xx = x + pursuitSteps[i, 0], yy = y + pursuitSteps[i, 1];
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height) continue;
                    int next = yy * width + xx;
                    if (parents[next] >= 0 || !Fits(new Point(xx + .5f, yy + .5f))) continue;
                    parents[next] = cell; pathQueue[tail++] = next;
                }
            }
            if (parents[target] < 0) return direct;
            int waypoint = target; while (parents[waypoint] != start) waypoint = parents[waypoint];
            return (new Point(waypoint % width + .5f, waypoint / width + .5f) - from).Normalized;
        }
        public void Enter(int room)
        {
            if (Dying) return;
            if (practicing) EndPractice();
            if (room < 0 || room >= data.maps.Length) return;
            if (!CanEnter(room)) { Notify(RouteHint(room, false), RouteHint(room, true)); Close(); return; }
            if (room == 8 && save.abyssFloor == 0) { OpenAbyssEntrance(); return; }
            if (InAbyss && room != 8) { AbyssExitConversation(); return; }
            save.room = room; save.x = Map.spawnX; save.y = Map.spawnY;
            save.snapshotRoom = -1; save.worldEnemies = null; save.savedRunes = 0;
            ResetTransient(); SpawnRoom(); mode = "world"; revision++; sound = "travel";
            Notify(Map.subtitle, Map.suben);
        }
        public void ReturnToTown()
        {
            if (Dying) return;
            if (save.room == 0) return;
            if (InAbyss) { AbyssExitConversation(); return; }
            if (!SafeTravel) { Notify("脱离敌人后才能快速回城。西侧出口随时可用。", "Leave combat to fast travel. The west exit is always available."); return; }
            Enter(0);
        }
        private void SpawnRoom()
        {
            enemies.Clear(); enemyId = 0;
            if (SpawnExpansionRoom()) return;
            if (save.room == 0) return;
            float[,] locations = { { 11, 12 }, { 15, 17 }, { 21, 15 }, { 24, 6 }, { 9, 7 }, { 30, 16 }, { 23, 19 }, { 14, 9 } };
            for (int i = 0; i < 6 + (save.room > 2 ? 2 : 0); i++)
            {
                float x = locations[i, 0], y = locations[i, 1];
                enemies.Add(CreatePlacedEnemy(++enemyId, i % 3, new Point(x, y), 34 + save.room * 9));
            }
        }
        public void Tick(float dt, Point movement, Point? explicitAim = null)
        {
            dt = Finite(dt) ? Clamp(dt, 0, .05f) : 0;
            if (dt <= 0) return;
            toastTime -= dt;
            if (mode == "fish") { fishTime += dt; fishCursor = .5f + .5f * (float)Math.Sin(fishTime * (2.5f + fishHits * .35f)); return; }
            if (mode != "world") return;
            if (hitStop > 0) { hitStop = Math.Max(0, hitStop - dt); return; }
            float skillReadyDelay = SkillReadyDelay();
            movementDirection = new Point();
            TickVisuals(dt); if (mode != "world") return; TickGate(dt);
            for (int i = effects.Count - 1; i >= 0; i--) { effects[i].life -= dt; if (effects[i].life <= 0) effects.RemoveAt(i); }
            if (Dying) { deathTime -= dt; if (deathTime <= 0) CompleteDeath(); return; }
            bool beganInPractice = practicing;
            save.totalTime += dt;
            // Resolve changed world collision before any body, missile or contact advances.
            TickTideTransition(); Point beforeMove = Position;
            attackCooldown -= dt; dashCooldown -= dt; skillCooldown -= dt; invulnerable -= dt; guardTime -= dt;
            dodgeEscapeTime = Math.Max(0, dodgeEscapeTime - dt);
            save.energy = Clamp(save.energy + dt * ((save.profession == 2 ? 18 : 14) + (InAbyss ? save.boons[1] * 4 : 0) + (save.teaUntil > save.totalTime ? 6 : 0) + (racing ? 8 : 0) - (save.cold > 70 ? 4 : 0)), 0, 100);
            if (movement.Length > 1) movement = movement.Normalized;
            ValidateActionProfile();
            TickMovement(dt, movement, explicitAim);
            TickAction(dt);
            if (mode != "world" || beganInPractice && !practicing) return;
            TickBufferedSkill(dt, skillReadyDelay);
            UpdateEnemies(dt); if (Dying) return;
            UpdateProjectiles(dt); if (Dying || beganInPractice && !practicing) return;
            TickExpansion(dt);
            if (Dying) return;
            TickGuidance(beforeMove);
            TickPolish(dt);
        }
        public void Dash(Point direction)
        {
            float cost = Relic(2) ? 25 : 20;
            if (mode != "world" || Dying) return;
            if (dashCooldown > 0 || save.energy < cost) { ActionUnavailable(dashCooldown, cost); return; }
            CancelAction();
            // A dodge is airborne/low-contact locomotion, not the next step of
            // the previous walk cycle. Restart its legs on the first real run
            // frame without manufacturing a footstep during the dodge.
            walkDistance = 0;
            dashDirection = direction.Length > .1f ? direction.Normalized : aim;
            dashTime = .18f; dashCooldown = .75f - save.talents[2] * .06f; invulnerable = .32f; save.energy -= cost;
            perfectDodgeArmed = true; dashOrigin = Position; dodgeEscapeTime = .30f;
            if (Relic(0)) save.health = Math.Min(save.maxHealth, save.health + 3);
            effects.Add(new Effect("dash", Position, .3f) { direction = dashDirection }); sound = "dash";
        }
        public void Attack(Point direction)
        {
            if (mode != "world" || Dying || ActionTransitionBlocked || attackCooldown > 0 || bufferedSkill != null) return;
            SetAim(direction);
            WeaponProfile profile = Profile(save.weapon, Style);
            if (save.energy < profile.energy) { ActionUnavailable(0, profile.energy); return; }
            save.energy -= profile.energy; attackCooldown = profile.interval;
            combo = comboTimer > 0 ? (combo + 1) % 3 : 0; comboTimer = 1.1f;
            float attackDamage = profile.damage * (save.weapon == 0 && combo == 2 ? 1.25f : 1) * (riposteTime > 0 ? 1.5f : 1);
            riposteTime = 0;
            BeginAction("attack", attackDamage, profile.interval, profile.interval * (save.weapon == 1 ? .55f : .45f));
        }
        public void Skill(Point? direction = null)
        {
            float cost = Relic(4) ? 50 : 40;
            if (mode != "world" || Dying) return;
            if (skillCooldown > 0 || save.energy < cost) { ClearBufferedSkill(); ActionUnavailable(skillCooldown, cost); return; }
            Point facing = direction.HasValue && Finite(direction.Value.x) && Finite(direction.Value.y) && direction.Value.Length > .05f ? direction.Value.Normalized : aim;
            if (ActionTransitionBlocked)
            {
                bufferedSkill = new SkillIntent { remaining = SkillBufferWindow, direction = facing, target = aimTarget,
                    weapon = save.weapon, profession = save.profession, style = Style, technique = save.technique };
                return;
            }
            StartSkill(facing, aimTarget);
        }
        private void StartSkill(Point direction, Point? target)
        {
            float cost = Relic(4) ? 50 : 40;
            ClearBufferedSkill();
            if (skillCooldown > 0 || save.energy < cost) { ActionUnavailable(skillCooldown, cost); return; }
            SetAim(direction);
            save.energy -= cost; skillCooldown = 3.5f;
            if (Relic(4)) save.health = Math.Min(save.maxHealth, save.health + 8);
            Point? liveTarget = aimTarget;
            try { aimTarget = target; BeginAction("skill", Damage, .62f, .28f); }
            finally { aimTarget = liveTarget; }
        }
        private void ResolveSkill(ActorAction action)
        {
            sound = "skill";
            if (save.technique == 1) { AlternateSkill(action); return; }
            if (save.profession == 1)
            {
                effects.Add(new Effect("release", WeaponOrigin(Position, 1, aim), .18f) { direction = aim, variant = 1, height = WeaponHeight(1, aim) });
                for (int i = -2; i <= 2; i++)
                {
                    Projectile shot = FriendlyShot(Position, 1, aim, 13, action.damage * 1.25f, false, action.target, action.impact);
                    double a = Math.Atan2(shot.velocity.y, shot.velocity.x) + i * .15;
                    shot.velocity = new Point((float)Math.Cos(a), (float)Math.Sin(a)) * 13; shot.technique = true; projectiles.Add(shot);
                }
                return;
            }
            if (save.profession == 0) guardTime = 3;
            float range = save.profession == 2 ? 4.5f : 3.1f;
            effects.Add(new Effect("burst", Position, .6f) { variant = save.profession, radius = range });
            foreach (Enemy e in enemies.ToArray())
                if ((e.Position - Position).Length < range && ClearAttackPath(Position, e.Position)) { e.stun = e.boss ? .5f : 1.4f; Hit(e, action.damage * (save.profession == 2 ? 2.3f : 1.9f), true,null,action.impact); }
        }
        private float Random()
        {
            save.seed = (int)(((long)save.seed * 1103515245 + 12345) & 0x7fffffff); return save.seed / 2147483647f;
        }
        private void Hit(Enemy e, float damage, bool technique = false, Point? attackDirection = null, ImpactProfile impact = null)
        {
            if (!enemies.Contains(e)) return;
            ImpactProfile profile=impact??CaptureImpact(save.weapon,combo);
            if(profile.playerOwned){TutorialAction(3);if(technique)TutorialAction(5);}
            if (practicing) { if(profile.playerOwned){if (technique) PracticeSkill(); else PracticeHit();} return; }
            Point approach = attackDirection.HasValue ? attackDirection.Value * -1 : (Position - e.Position).Normalized;
            float facingDot = approach.x * e.facing.x + approach.y * e.facing.y;
            if (!technique && !e.boss && e.kind == 2 && e.state == 0 && e.stun <= 0 && facingDot > .45f)
            { damage *= .4f; effects.Add(new Effect("number", e.Position, .7f, L("正面护甲", "GUARDED"))); sound = "guard"; }
            else
            {
                sound = "hit";
                // Flank heavy armor for bonus damage and extra stagger.
                if (profile.playerOwned && !technique && !e.boss && e.kind == 2 && e.state == 0 && e.stun <= 0 && facingDot < -.45f)
                {
                    damage *= 1.25f;
                    if (e.staggerRecovery <= 0) e.stagger += 6;
                    effects.Add(new Effect("number", e.Position, .7f, L("侧袭", "FLANK")));
                }
            }
            // The post-release recovery is a brief, player-owned punish window.
            if (profile.playerOwned && e.releaseTime > .001f && e.stun <= 0)
            {
                damage *= 1.25f;
                effects.Add(new Effect("number", e.Position, .7f, L("破绽", "OPEN")));
            }
            bool crit = Random() < profile.criticalChance;
            if (crit) damage *= 1.7f;
            if (e.staggerRecovery <= 0) e.stagger += damage * (profile.weapon == 0 ? .7f : .45f) * (profile.weapon == 0 && profile.style == 2 ? 1.4f : 1);
            if (e.stagger >= (e.boss ? 95 : 30)) { e.stagger = 0; e.staggerRecovery = e.boss ? 4 : 2; e.stun = e.boss ? 1.1f : 1.7f; e.state = 0; e.timer = 1; effects.Add(new Effect("break", e.Position, .7f, L("破势", "BREAK"))); }
            e.hp -= damage; e.flash = .09f; e.hitTime = .18f; e.recoil = attackDirection ?? (e.Position - Position).Normalized;
            effects.Add(new Effect(e.kind == 2 || e.boss ? "spark" : "impact", e.Position, .22f) { direction = e.recoil });
            if (!save.reducedMotion) { hitStop = Math.Max(hitStop, technique ? .05f : .025f); impactShake = Math.Max(impactShake, technique ? .12f : profile.weapon == 0 && profile.combo == 2 ? .07f : 0); }
            effects.Add(new Effect("number", e.Position, .7f, (crit ? "! " : "") + ((int)damage)));
            if (e.hp > 0) return;
            fallen.Add(new FallenActor { actor = e, room = save.room, life = e.boss ? 1.4f : .65f, duration = e.boss ? 1.4f : .65f });
            enemies.Remove(e);
            bool roadTrialEnemy = IsRoadwardenTrialEnemy(e);
            // Drill opponents do not advance chapter kill gates, contracts,
            // or normal loot. Clearing both waves pays the fixed trial prize.
            if (!roadTrialEnemy) { save.kills[save.room]++; OnExpansionKill(e, profile.masteryWeapon); }
            if (!roadTrialEnemy) effects.Add(new Effect("loot", e.Position, .75f) { variant = e.boss ? 5 : e.kind == 2 ? 4 : e.kind == 1 ? 1 : 2 });
            if (!InAbyss && !roadTrialEnemy)
            {
                save.coins += e.boss ? 70 : 6; save.xp += e.boss ? 80 : 12;
                save.inventory[e.boss ? 5 : (e.kind == 2 ? 4 : e.kind == 1 ? 1 : 2)]++;
            }
            effects.Add(new Effect("defeat", e.Position, .5f) { variant = e.boss ? 1 : 0 }); sound = "reward";
            while (save.xp >= save.level * 40 && save.level < 30)
            { save.xp -= save.level * 40; save.level++; save.skillPoints++; save.health = save.maxHealth; Notify("升级！生命回满，获得 1 技能点。按 K 分配。", "Level up! Health restored, +1 skill point. Open K to spend it."); }
            if (e.boss)
            {
                Flag("boss_" + save.room); RetireHostileProjectiles();
                if (save.room == 4) finaleDelay = 1.4f;
                else if (save.room >= 5) Notify("守护者倒下了。完成地区调查或清理本层剩余敌人。", "Guardian defeated. File the report, or clear remaining abyss enemies.");
                else Notify("守护者倒下了。到祭坛处收取星核。", "The guardian has fallen. Recover the starcore at the altar.");
            }
            revision++;
        }
        // Escaping a marked strike is as skillful as letting its contact graze
        // the dodge invulnerability. Reward either outcome exactly once.
        private void AwardPerfectDodge()
        {
            perfectDodgeArmed = false; dodgeEscapeTime = 0;
            TutorialAction(4); PracticeDodge();
            riposteTime = Relic(3) ? 3 : 1.5f;
            save.energy = Math.Min(100, save.energy + 8);
            effects.Add(new Effect("number", Position, .7f, L("闪避反击", "COUNTER")));
            sound = "counter";
        }
        private bool TryRewardEscapedWarning(Point center, float radius)
        {
            // A recent real dodge must carry the player from inside the actual
            // danger footprint to outside it. An old cooldown, walking away,
            // or dashing entirely outside the warning never earns a counter.
            if (!perfectDodgeArmed || dodgeEscapeTime <= 0 ||
                (dashOrigin - center).Length >= radius ||
                (Position - center).Length < radius ||
                (Position - dashOrigin).Length < .35f) return false;
            AwardPerfectDodge(); return true;
        }
        private void Hurt(float damage, string source = "environment", Point? incoming = null)
        {
            if (mode != "world" || Dying) return;
            if (invulnerable > 0) { if (dashTime > 0 && perfectDodgeArmed && source != "environment") AwardPerfectDodge(); return; }
            if (practicing) { Notify("观察红圈：在命中前闪避，或闪到圈外。练习不会受伤。", "Read the red circle: dodge before impact or leave its area. Practice cannot hurt you."); return; }
            float multiplier = save.difficulty == 0 ? .45f : save.difficulty == 2 ? 1.4f : 1;
            float armor = (guardTime > 0 ? .45f : 1) * (1 - save.talents[1] * .035f) * (InAbyss ? 1 - save.boons[2] * .12f : 1) * (Relic(5) ? 1.15f : 1);
            playerHitFacing=VisualFacing; playerHitWeapon=AppearanceWeapon;
            Point reaction = incoming ?? VisualFacing * -1;
            if (reaction.Length < .001f) reaction = VisualFacing * -1;
            save.health -= damage * multiplier * armor; invulnerable = .65f; sound = "hurt"; playerHitTime = .18f; playerFlash = .09f; playerRecoil = reaction.Normalized; recoilSpeed = damage >= 20 ? 1.8f : .75f;
            if (!save.reducedMotion) { hitStop = .045f; impactShake = source == "slam" ? .18f : 0; }
            effects.Add(new Effect("hurt", Position, .5f));
            if (save.health <= 0)
            {
                save.deaths++;
                save.lastFailureZh = source == "shot" ? "被远射击倒：侧移避开锁定射线，用墙体挡箭。" : source == "slam" || source == "area" ? "被范围攻击击倒：离开红圈，保留耐力用于闪避。" : source == "lunge" ? "被近身扑击击倒：闪到敌人侧面，再连击或破势。" : "被环境伤害击倒：准备凉茶，寻找阴影、火盆与安全营地。";
                save.lastFailureEn = source == "shot" ? "Fell to arrows: sidestep locked shots and use walls." : source == "slam" || source == "area" ? "Fell to an area strike: leave red circles and save dodge energy." : source == "lunge" ? "Fell to a lunge: dodge sideways, then combo or stagger." : "Fell to the environment: bring tea and find shade, braziers or a camp.";
                RecordMoment(save.lastFailureZh, save.lastFailureEn);
                save.health = 0; save.coins = Math.Max(0, save.coins - 10); BeginDeath();
            }
        }
        private void UpdateEnemies(float dt)
        {
            tickingEnemies.Clear();tickingEnemies.AddRange(enemies);
            foreach (Enemy e in tickingEnemies)
            {
                if (Dying) break;
                if (!enemies.Contains(e) || mode != "world") continue;
                e.moving = e.backpedaling = false; e.flash -= dt; e.hitTime -= dt; e.releaseTime -= dt; e.stun -= dt; e.staggerRecovery -= dt;
                if (e.hitTime > 0) { Point pushed = SweepActor(e.Position, e.recoil * dt * (e.boss ? .15f : .8f)); e.x = pushed.x; e.y = pushed.y; continue; }
                if (e.stun > 0 || e.releaseTime > 0) continue;
                // Beacon attackers advance on the ward instead of blindly following
                // the hero. Their normal attacks still target the hero if in range.
                bool wardAttacker = roadTrialRoom == save.room && IsRoadwardenTrialEnemy(e);
                Point pursuit = wardAttacker ? ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position) : Position;
                Point delta = pursuit - e.Position; float distance = delta.Length;
                if (e.state == 1)
                {
                    e.timer -= dt;
                    if (e.boss && save.room == 1 && e.windupDuration > 0 && e.timer < .2f)
                    {
                        Point next = e.attackOrigin + (e.attackDestination - e.attackOrigin) * Clamp(1 - e.timer / .2f, 0, 1);
                        Point swept = SweepActor(e.Position, next - e.Position); e.x = swept.x; e.y = swept.y;
                    }
                    if (e.timer <= 0)
                    {
                        e.releaseTime = e.boss ? .38f : .24f;
                        sound = e.boss || e.kind == 2 ? "slam" : e.kind == 1 ? "arrow" : "attack";
                        effects.Add(new Effect("enemyRelease", e.Position, .24f) { direction = e.facing, variant = e.boss ? 10 + save.room : e.kind });
                        if (e.boss || e.kind == 2)
                        {
                            float radius = EnemyAreaRadius(e);
                            effects.Add(new Effect("danger", e.target, .45f) { variant = e.boss ? save.room : 0, radius = radius });
                            if ((Position - e.target).Length < radius && ClearAttackPath(e.Position, Position)) Hurt(e.boss ? 22 + e.BossPhase * 3 : 14, "slam", (Position - e.Position).Normalized);
                            else TryRewardEscapedWarning(e.target, radius);
                            if (Dying) break;
                            if (e.boss)
                            {
                                e.cycle++;
                                if (save.room == 3)
                                {
                                    // The overseer alternates stomps with an aimed three-shot salvo.
                                    double heading = Math.Atan2(e.facing.y, e.facing.x);
                                    for (int shot = -1; shot <= 1; shot++) Fire(e, new Point((float)Math.Cos(heading + shot * .18), (float)Math.Sin(heading + shot * .18)), 6);
                                }
                                BossExpansionAttack(e);
                                int spokes = 4 + Math.Min(5, save.room) * 2 + e.BossPhase * 2;
                                if (save.room >= 2 || e.BossPhase >= 2)
                                    for (int k = 0; k < spokes; k++) Fire(e, new Point((float)Math.Cos(k * Math.PI * 2 / spokes + e.cycle * .2), (float)Math.Sin(k * Math.PI * 2 / spokes + e.cycle * .2)), 3.5f + e.BossPhase * .4f);
                                if ((e.BossPhase == 3 || (save.room == 4 && e.BossPhase >= 2)) && e.cycle % 3 == 0 && enemies.Count < 9)
                                    enemies.Add(CreatePlacedEnemy(++enemyId, save.room == 4 ? 1 : 0, new Point(e.x - 2, e.y + 1), 38 + save.room * 3));
                            }
                        }
                        else if (e.kind == 1) Fire(e, e.facing, 5);
                        else if (InEnemyMeleeArc(e,Position) && ClearAttackPath(e.Position, Position)) Hurt(11, "lunge", e.facing);
                        e.state = 0; e.timer = e.boss ? 2.2f - e.BossPhase * .3f : 1.4f;
                    }
                    continue;
                }
                if (distance > (e.boss ? 18 : 8)) continue;
                e.timer -= dt;
                float desired = wardAttacker ? 1.05f : e.boss ? 3.5f : e.kind == 1 ? 4.5f : 1.05f;
                bool visible = ClearAttackPath(e.Position, pursuit);
                Point before = e.Position;
                if (visible && e.kind == 1 && !e.boss && !wardAttacker && distance < 3 && !practicing) { Point back = SweepActor(e.Position, delta.Normalized * -dt * 1.6f); e.backpedaling=(back-e.Position).Length>.001f;e.x = back.x; e.y = back.y; }
                if (distance > desired || !visible)
                {
                    Point p = e.Position + DirectionTo(e.Position, pursuit) * dt * (e.boss ? 1.15f + e.BossPhase * .15f : e.kind == 0 ? 2.1f : 1.5f) * (InAbyss && save.abyssFloor % 3 == 2 ? 1.2f : 1);
                    Point swept = SweepActor(e.Position, p - e.Position); e.x = swept.x; e.y = swept.y;
                }
                Point travelled = e.Position - before; e.moving = travelled.Length > .001f;
                if (e.moving) { e.moveTime += travelled.Length; e.Face(e.backpedaling?Position-e.Position:travelled); }
                delta = Position - e.Position; distance = delta.Length;
                if (e.timer <= 0 && distance < (e.boss ? 12 : e.kind == 1 ? 7 : 2.5f) && ClearAttackPath(e.Position, Position))
                {
                    e.state = 1; e.timer = e.boss ? 1.1f - e.BossPhase * .08f : .65f; e.windupDuration = e.timer; e.target = Position; e.Face(delta);
                    if (e.boss || distance < 6) sound = "windup";
                    e.attackOrigin = e.attackDestination = e.Position;
                    if (e.boss && save.room == 1)
                        for (int step = 0; step < 12; step++)
                        {
                            Point next = e.attackDestination + e.facing * .25f;
                            if (!Fits(next) || (e.target - e.attackDestination).Length < .3f) break;
                            e.attackDestination = next;
                        }
                }
            }
            tickingEnemies.Clear();
        }
        private void Fire(Enemy e, Point dir, float speed)
        { Projectile shot=e.boss?new Projectile{position=e.Position,velocity=dir.Normalized*speed,damage=14}:WeaponShot(e.Position,1,dir,speed,10,e.target); shot.source="shot";shot.visual=e.boss?"enemyOrb":"enemyArrow";shot.height=e.boss?BossEmissionHeight(save.room):WeaponHeight(1,dir);shot.launchHeight=shot.flightHeight=shot.height;shot.theme=e.boss?save.room:0;projectiles.Add(shot);sound=e.boss?"spell":"arrow"; }
        private void UpdateProjectiles(float dt)
        {
            tickingProjectiles.Clear();tickingProjectiles.AddRange(projectiles);
            foreach (Projectile p in tickingProjectiles)
            {
                if (Dying) break;
                if (!projectiles.Contains(p)) continue;
                float flightDt = Math.Min(dt, Math.Max(0, p.life));
                p.life -= dt; p.age += flightDt;
                if(p.launchHeight>=0&&p.flightHeight>=0)
                { float t=Clamp(p.age/.25f,0,1);t=t*t*(3-2*t);p.height=p.launchHeight+(p.flightHeight-p.launchHeight)*t; }
                if (p.life <= 0 && flightDt <= 0)
                { projectiles.Remove(p); effects.Add(new Effect("projectileFade", p.position, .25f) { direction = p.velocity.Normalized, variant = p.visual == "arrow" || p.visual == "enemyArrow" ? 8 : p.explosive ? 9 : 10, height = p.height }); continue; }
                int steps = Math.Max(1, (int)Math.Ceiling(p.velocity.Length * flightDt / .1f));
                bool dead = p.blockedAtSpawn || ShotBlocked(p,p.position); float impactRadius = 0;
                for (int step = 0; step < steps && !dead; step++)
                {
                    Point next = p.position + p.velocity * (flightDt / steps);
                    // Resolve the first obstruction along flight, not its endpoint.
                    if (ShotBlocked(p,next)) { dead = true; break; }
                    p.position = next;
                    if (p.friendly)
                    {
                        projectileTargets.Clear();projectileTargets.AddRange(enemies);
                        foreach (Enemy e in projectileTargets)
                        {
                            if (p.hits.Contains(e.id) || (e.Position - p.position).Length >= (e.boss ? .9f : .5f) + p.Radius || !ClearAttackPath(p.position, e.Position)) continue;
                            p.hits.Add(e.id); Hit(e, p.damage, p.technique, p.velocity.Normalized, p.impact);
                            // Practice completion or another transaction can
                            // retire the shot. It must not leave an old-room impact.
                            if (!projectiles.Contains(p)) { dead = true; break; }
                            if (p.slowing) e.stun = Math.Max(e.stun, .45f); dead = !p.piercing;
                            if (p.explosive)
                            {
                                impactRadius = 1.8f; dead = true;
                                splashTargets.Clear();splashTargets.AddRange(enemies);
                                foreach (Enemy other in splashTargets) if (other != e && (other.Position - p.position).Length < impactRadius && ClearAttackPath(p.position, other.Position)) Hit(other, p.damage * .6f, false, (other.Position - p.position).Normalized, p.impact);
                                splashTargets.Clear();
                            }
                            if (!p.piercing || p.explosive) break;
                        }
                        projectileTargets.Clear();
                    }
                    else if ((Position - p.position).Length < .38f) { Hurt(p.damage, p.source ?? "shot", p.velocity.Normalized); dead = true; }
                }
                if (dead && projectiles.Remove(p)) effects.Add(new Effect(p.explosive ? "magicImpact" : "projectileImpact", p.position, .28f) { direction = p.velocity.Normalized, variant = p.friendly ? 0 : 1, radius = impactRadius, height = p.height });
                else if (p.life <= 0 && projectiles.Remove(p)) effects.Add(new Effect("projectileFade", p.position, .25f) { direction = p.velocity.Normalized, variant = p.visual == "arrow" || p.visual == "enemyArrow" ? 8 : p.explosive ? 9 : 10, height = p.height });
            }
            tickingProjectiles.Clear();
        }
        // Story presence is shared by interaction, world drawing and map marks.
        // Saved quest flags remain the authority; no extra persistent state.
        public bool IsObjectPresent(WorldObject o)
        {
            if (o == null) return false;
            if (o.kind == "returnscout") return Has("scout_saved");
            if (o.kind == "scout") return !Has("scout_saved");
            return true;
        }
        public WorldObject Nearest()
        {
            WorldObject best = null; float distance = 1.75f;
            foreach (WorldObject o in Map.objects)
            {
                if (!IsObjectPresent(o)) continue;
                float d = (Position - new Point(o.x, o.y)).Length;
                if (d < distance) { distance = d; best = o; }
            }
            return best;
        }
        public void Interact()
        {
            if (mode == "fish") { FishHit(); return; }
            if (mode != "world" || Dying) return;
            WorldObject o = Nearest();
            if (o == null) { Notify("靠近居民、路牌或发光物，再按 E。", "Approach a resident, sign or glowing object, then press E."); return; }
            TutorialAction(1); Talk(o);
        }
        public void Talk(WorldObject o)
        {
            if (!IsObjectPresent(o)) return;
            if (practicing) { Notify("先完成或结束训练，再与居民交谈。", "Finish or end practice before talking to residents."); return; }
            if (TalkPolish(o)) return;
            if (TalkExpansion(o)) return;
            string kind = o.kind;
            if (kind == "innGuest")
            {
                Dialog(o.name, o.en, "巡路员留下的信，最后还是找到了收信的人。你要去的那些地方，我也走过。\n\n白天看道路，夜里看灯。遇到举锤的敌人，先看它的手；遇到射手，先看弓弦。能回来讲故事，比走得快更重要。" + (Has("survey_complete") ? "\n\n你留在旅店的归乡星图，已经帮三个夜归的人绕过塌桥。谢谢你没有让巡路队的名字被遗忘。星图桌现在能连接三座信标，附近安全时可以快速往返。" : Has("survey_1") || Has("survey_2") || Has("survey_3") ? "\n\n我听说你找到旧巡路信标。三段信号都齐了，就去旅店的星图桌看看。" : "") + (Has("dispatch_complete") ? "\n\n你解开了三封旧信。旅店愿意给守路人热汤优惠两枚硬币，愿所有人都能找到归途。" : Has("dispatch_6") || Has("dispatch_7") || Has("dispatch_8") ? "\n\n你已经解开一封旧信。其余两封也许能回答它留下的问题。" : "") + RoadwardenVigilInnNote(false), "The warden's letter finally found its recipient. I once walked the roads ahead of you.\n\nWatch the roads by day, and the lamps at night. Read a hammer's lift and an archer's bowstring. Coming home with a story matters more than traveling fast." + (Has("survey_complete") ? "\n\nThe chart you left at the inn helped three late travelers avoid the broken bridge. Thank you for keeping the wardens remembered. The chart table now links the three beacons for safe travel." : Has("survey_1") || Has("survey_2") || Has("survey_3") ? "\n\nI hear you found an old road beacon. Once all three signals are recovered, visit the chart table here at the inn." : "") + (Has("dispatch_complete") ? "\n\nYou decoded all three letters. The inn now discounts stew by two coins for those who keep the road open." : Has("dispatch_6") || Has("dispatch_7") || Has("dispatch_8") ? "\n\nYou decoded one old letter. The others may answer its questions." : "") + RoadwardenVigilInnNote(true), C("坐一会儿，记住这句话", "Remember the advice", "close")); return;
            }
            if (kind.StartsWith("portal")) { Enter(int.Parse(kind.Substring(6))); return; }
            if (kind == "routes")
            {
                conversation = null; mode = "map"; return;
            }
            if (kind == "guild")
            {
                if (save.ending > 0) { Dialog(o.name, o.en, "灾变过去了，冒险还没有结束。旅店有热汤，密林里仍有星晶，大家都在等你。", "The crisis is over. The adventure is not. There is warm stew at the inn, starcrystal in the woods, and friends waiting here.", C("开始新的远征", "Another expedition", "routes"), C("回顾结局", "Revisit the ending", "epilogue"), C("再见", "Goodbye", "close")); return; }
                int next = save.stage + 1;
                if (save.stage < 3 && Has("core_" + next))
                    Dialog(o.name, o.en, "你真的做到了。统领说这些星核会重新稳住边境……可最近镇里的作物越来越难生长。\n\n也许，下一片遗迹会告诉我们原因。", "You did it. The warden says these cores will secure the frontier. Yet our crops keep withering.\n\nPerhaps the next ruins will tell us why.", C("交回星核 · 开启下一章", "Deliver the starcore · next chapter", "deliver"), C("稍后再来", "Later", "close"));
                else Dialog(o.name, o.en, save.stage == 3 ? "三枚星核汇聚后，堡垒的屏障越来越强，边境却越来越虚弱。去见厄洛，问问他究竟在保护什么。\n\n如果你帮助过药师、渔夫和铁匠，他们也许会带来另一种办法。" : "先去北方的荆棘密林，救出侦察员，挑战巨兽。收取星核后交回公会。按 Tab 查看主线和支线。", save.stage == 3 ? "The shield grows stronger as the frontier weakens. Ask Orrin what he is protecting.\n\nIf you helped An, Old Tide and Noll, they may bring another solution." : "Go north to Thornwood. Rescue the scout, defeat the beast, and return its core. Tab tracks the main quest and your side commissions.", C("查看冒险路线", "Choose a route", "routes"), C("我出发了", "On my way", "close"));
                return;
            }
            if (kind == "rest") { Dialog(o.name, o.en, "旅店的炉火永远为冒险者留着。休息推进 45 秒，回复生命与耐力，田里的作物也会继续长大。", "The inn keeps a fire for travelers. Rest for 45 seconds to restore health and energy; crops keep growing.", C("休息", "Rest", "rest"), C("继续冒险", "Keep adventuring", "close")); return; }
            if (kind == "garden")
            {
                List<Choice> list = new List<Choice>();
                if (!Has("ally_1")) list.Add(C("支线：交付月光草 ×6", "Commission: deliver 6 Moonherbs", "ally1"));
                list.Add(C(save.plantedAt < 0 ? "种植 · 种子 ×1" : "收获 / 查看生长", save.plantedAt < 0 ? "Plant · 1 seed" : "Harvest / check growth", save.plantedAt < 0 ? "plant" : "harvest"));
                list.Add(C("买种子 · 5 硬币", "Buy seeds · 5 coins", "seeds")); list.Add(C("再见", "Goodbye", "close"));
                Dialog(o.name, o.en, "我需要六株月光草，为受伤的居民配药。你可以在野外采集，也可以种植：一粒种子，60 秒后收获。\n\n人们受伤的时候，最需要的从来不是一面更高的墙。", "I need six Moonherbs for the wounded. Gather them in the wild, or grow your own: one seed, sixty seconds.\n\nWhen people are hurt, a higher wall is rarely what they need.", list.ToArray()); return;
            }
            if (kind == "fish")
            {
                List<Choice> list = new List<Choice> { C("开始钓鱼", "Cast the line", "fish") };
                if (!Has("ally_2")) list.Add(C("支线：交付银鳍鱼 ×3", "Commission: deliver 3 Silverfin", "ally2"));
                list.Add(C("再见", "Goodbye", "close"));
                Dialog(o.name, o.en, "我想给避难的孩子们煮一锅鱼汤，需要三条银鳍鱼。\n\n钓鱼时按 E 或点收线，让浮标停在绿色区，成功三次就能钓到鱼。没有鱼饵消耗，慢慢试。", "Three Silverfin would make soup for the refugee children.\n\nPress E or Reel while the marker is in the green band. Three good pulls catch a fish. No bait cost; take your time.", list.ToArray()); return;
            }
            if (kind == "forge")
            {
                List<Choice> list = new List<Choice> { C("强化装备 · 木3 晶2 零件2", "Upgrade · 3 timber / 2 crystal / 2 parts", "upgrade"), C("制作猎弓 · 木4 零件2", "Craft bow · 4 timber / 2 parts", "bow"), C("制作星火杖 · 晶4 零件3", "Craft staff · 4 crystal / 3 parts", "staff") };
                if (!Has("ally_3")) list.Add(C("支线：交付零件 ×5", "Commission: deliver 5 parts", "ally3"));
                list.Add(C("再见", "Goodbye", "close"));
                Dialog(o.name, o.en, "短剑快，猎弓远，法杖能让星火在敌人间爆开。强化装备提升生命与攻击。\n\n我还需要五个零件修好镇里的水泵。堡垒里有那么多机器，小镇却没有一口能用的井。", "Swords are quick, bows reach far, and staves burst starfire between enemies. Upgrades improve health and damage.\n\nI also need five parts to repair our water pump. So many machines in the citadel, and no working well in town.", list.ToArray()); return;
            }
            if (kind == "cook") { Dialog(o.name, o.en, "暖潮炖汤：鱼1、药草2。回复55生命与耐力，增加攻击90秒。F喝汤。\n\n星薄荷凉茶：药草2、星晶1。T喝茶，抗暑抗寒并增加耐力回复180秒。", "Stew: 1 fish, 2 herbs. Heals 55, refills energy and boosts damage for 90 seconds. F drinks it.\n\nTea: 2 herbs, 1 crystal. T drinks it for climate protection and faster energy recovery, 180 seconds.", C("制作炖汤", "Cook stew", "cook"), C("买炖汤 · " + ShopStewCost + " 硬币", "Buy stew · " + ShopStewCost + " coins", "buymeal"), C("买木材 ×4 · " + ShopTimberCost + " 硬币", "Buy timber ×4 · " + ShopTimberCost + " coins", "buywood"), C("制作凉茶", "Brew tea", "tea"), C("再见", "Goodbye", "close")); return; }
            if (kind == "scout")
            {
                if (Has("scout_saved")) { Notify("侦察员已经安全离开。", "The scout reached safety."); return; }
                if (save.kills[1] < 4) { Dialog(o.name, o.en, "路上至少还有四只狂暴的怪物。帮我清理道路，我会告诉你星核的入口。", "At least four beasts block the escape route. Clear them and I will open the path to the core.", C("我来解决", "I will clear the road", "close")); return; }
                Dialog(o.name, o.en, "谢谢你！我调查了星陨点，发现星核正被堡垒一点点抽走。我会先回去，入口已经替你打开。", "Thank you! I found the citadel drawing power from the fallen core. I will head home. The altar path is open.", C("护送他离开", "Send him home safely", "scout")); return;
            }
            if (kind.StartsWith("rune"))
            {
                if (GateOpen) { Notify("三座石碑已经共鸣。", "The tablets are already aligned."); return; }
                int id = int.Parse(kind.Substring(4)); int[] order = { 0, 2, 1 };
                if (id == order[runeProgress])
                {
                    runeProgress++; sound = "reward";
                    if (runeProgress == 3) { Flag("gate_2"); Notify("日、月、潮。遗迹入口打开了！", "Sun, moon, tide. The ruins open!"); }
                    else Notify("石碑回应了你（" + runeProgress + "/3）。", "The tablet answers (" + runeProgress + "/3).");
                }
                else { runeProgress = 0; Notify("顺序不对。调查左上方遗物，会发现线索。", "Wrong order. Investigate the northwest relic for a clue."); }
                return;
            }
            if (kind == "engine")
            {
                if (GateOpen) { Notify("升降机已经启动。", "The elevator is running."); return; }
                Dialog(o.name, o.en, "升降机损坏了。需要 3 个零件修复线路，才能进入监工的工厂。矿场的星晶矿里有备用零件。", "The elevator needs 3 parts to open the overseer's workshop. A starcrystal vein holds spare parts.", C("修复 · 零件 ×3", "Repair · 3 parts", "engine"), C("稍后回来", "Later", "close")); return;
            }
            if (kind == "lore")
            {
                if (save.room > 3) { Notify("堡垒的能源，都来自边境的星核。", "The citadel draws its power from the frontier's cores."); return; }
                int id = save.room; Flag("lore_" + id); LoreData l = data.lore[id - 1];
                if (!Has("treasure_" + id)) { Flag("treasure_" + id); save.coins += 20; save.inventory[1] += 2; }
                Dialog(l.title, l.en, l.zh + "\n\n找到遗物：硬币 ×20，星晶 ×2（首次）。", l.text + "\n\nRelic found: 20 coins, 2 crystals (first discovery).", C("记入手帐", "Record it in the journal", "close")); sound = "reward"; return;
            }
            if (kind == "herb" || kind == "wood" || kind == "ore")
            {
                if (Has("node_" + o.id)) { Notify("已经采集过了。回镇休息后资源会更新。", "Already gathered. Rest in town to renew resources."); return; }
                Flag("node_" + o.id); int item = kind == "herb" ? 2 : kind == "wood" ? 0 : 1;
                save.inventory[item] += 3; if (kind == "ore") save.inventory[4] += 3;
                if (kind == "ore") BountyEvent(1, 1);
                Notify(o.name + " +3" + (kind == "ore" ? "，零件 +3" : ""), o.en + " +3" + (kind == "ore" ? ", parts +3" : "")); sound = "reward"; return;
            }
            if (kind == "shrine")
            {
                if (GateOpen && GateBarrierActive) { Notify("道路正在打开，等屏障消散后再进入祭坛。", "The road is opening; wait for the barrier to dissolve before entering the altar."); return; }
                if (save.room == 4 && Has("boss_4")) { FinalConversation(); return; }
                if (!GateOpen) { Notify(GateHint(false), GateHint(true)); return; }
                if (Has("boss_" + save.room))
                {
                    bool newlyFound = !Has("core_" + save.room); Flag("core_" + save.room);
                    if (newlyFound) RecordMoment("获得探索能力：" + new[] { "", "藤封共鸣", "浅水接通", "断桥修复" }[Math.Min(3, save.room)] + "。旧地区出现新的支路。", "Exploration power gained: " + new[] { "", "Grove resonance", "Tidal connection", "Bridge repair" }[Math.Min(3, save.room)] + ". Revisit old regions for sealed routes.");
                    Dialog("星核祭坛", "Starcore altar", "星核的光渐渐平静。把它带回余烬镇，交给莉雅。\n\n你已经开始怀疑：这真的是一场拯救吗？", "The core grows still. Return it to Lyra in Emberfall.\n\nA question lingers: is this really a rescue?", C("返回余烬镇", "Return to Emberfall", "goto0"), C("继续探索", "Keep exploring", "close")); sound = "reward"; return;
                }
                if (enemies.Exists(e => e.boss)) { Notify("留意红色攻击预告，闪避后反击。", "Watch the red attack warnings. Dodge, then strike."); return; }
                BossData b = data.bosses[save.room - 1];
                if (save.room == 4 && Proofs == 3 && save.frontierEnding == 3)
                {
                    Dialog(b.name, b.en, "三份原始记录摆在厄洛面前：港口的水位、沙海的调度、高原被封存的名字。你也带来了一个已经开始运行的三方协定。\n\n他不再能说除此之外别无选择。你可以提出让他放下武器，也可以坚持决斗。", "The tide log, allocation ledger and sealed names lie before Orrin. You also bring a working covenant between three regions.\n\nHe can no longer claim there is no alternative. Ask him to stand down, or insist on a duel.", C("用证词与协定说服他", "Present the records and covenant", "parley"), C("仍然挑战统领", "Challenge the warden", "boss"), C("准备一下", "Prepare", "close")); return;
                }
                Dialog(b.name, b.en, save.room == 4 ? "厄洛：我见过边境失守时的样子。我不会再让任何人死在堡垒之外。\n\n你说屏障在抽走大地的生命？只要它还能撑住，我就没有别的选择。" : "守护星核的敌人苏醒了。红圈会标记攻击落点；闪避后反击。敌人生命降低后会进入新阶段，增加弹幕和援军。", save.room == 4 ? "Orrin: I saw the frontier fall. I will never again leave people to die outside these walls.\n\nYou say the shield drains the land? As long as it holds, I have no other choice." : "The guardian wakes. Red circles mark its attack; dodge and counter. As health falls, new phases add projectiles and reinforcements.", C(save.room == 4 ? "为边境而战" : "开始挑战", save.room == 4 ? "Fight for the frontier" : "Challenge the guardian", "boss"), C("准备一下", "Prepare first", "close"));
            }
        }
        private bool Spend(int wood, int crystal, int herb, int fish, int gear, int coins = 0)
        {
            int[] costs = { wood, crystal, herb, fish, gear };
            for (int i = 0; i < 5; i++) if (save.inventory[i] < costs[i]) { Notify("材料不足。查看背包 I，探索、采集或钓鱼补充。", "Not enough materials. Check I; explore, gather or fish."); return false; }
            if (save.coins < coins) { Notify("硬币还不够。", "Not enough coins."); return false; }
            for (int i = 0; i < 5; i++) save.inventory[i] -= costs[i]; save.coins -= coins; revision++; return true;
        }
        public void Choose(int index)
        {
            if (mode != "dialog" || conversation == null || index < 0 || index >= conversation.choices.Length) return;
            if(Dying){Close();return;}
            string action = conversation.choices[index].action; Close();
            if (ChoosePolish(action)) return;
            if (ChooseExpansion(action)) return;
            if (action == "close") return;
            if (action.StartsWith("goto")) { Enter(int.Parse(action.Substring(4))); return; }
            if (action == "routes") { Talk(data.maps[0].objects[5]); return; }
            if (action == "rest")
            {
                save.health = save.maxHealth; save.energy = 100; save.totalTime += 45;
                save.flags.RemoveAll(f => f.StartsWith("node_")); save.heat = save.cold = 0; Notify("炉火温暖。生命与耐力已恢复。", "Warm by the fire. Health and energy restored."); revision++; return;
            }
            if (action == "scout" && save.kills[1] >= 4) { Flag("scout_saved"); save.coins += 20; Notify("侦察员安全离开。星核入口已开启。", "Scout rescued. The altar path is open."); }
            if (action == "engine" && Spend(0, 0, 0, 0, 3)) { Flag("gate_3"); sound = "reward"; Notify("升降机启动，工厂入口开启。", "Elevator repaired. The workshop is open."); }
            if (action == "plant")
            {
                if (save.plantedAt >= 0) return;
                if (save.inventory[7] < 1) { Notify("需要一粒种子，可以向杏购买。", "You need a seed. Buy some from An."); return; }
                save.inventory[7]--; save.plantedAt = save.totalTime; Notify("种下月光草。60 秒后回来收获。", "Moonherbs planted. Return in 60 seconds."); revision++; return;
            }
            if (action == "harvest")
            {
                if (save.plantedAt < 0) return;
                if (save.totalTime - save.plantedAt < 60) { Notify("还需 " + (int)Math.Ceiling(60 - (save.totalTime - save.plantedAt)) + " 秒。", "Ready in " + (int)Math.Ceiling(60 - (save.totalTime - save.plantedAt)) + " seconds."); return; }
                int harvest = 5 + save.projects[0] * 2 + (save.frontierEnding == 1 ? 2 : 0);
                save.inventory[2] += harvest; save.inventory[7] += 2; save.plantedAt = -1; save.coins += 8; revision++; sound = "reward"; Notify("收获：月光草 ×" + harvest + "，种子 ×2，硬币 +8。", "Harvest: " + harvest + " Moonherbs, 2 seeds, 8 coins."); return;
            }
            if (action == "seeds" && Spend(0, 0, 0, 0, 0, 5)) { save.inventory[7] += 3; Notify("种子 ×3", "Seeds ×3"); }
            if (action == "upgrade")
            {
                if (save.upgrades >= 5) { Notify("装备已经满级。", "Your equipment is fully upgraded."); return; }
                if (Spend(3, 2, 0, 0, 2)) { save.upgrades++; save.maxHealth += 20; save.health = save.maxHealth; Notify("强化：最大生命 +20，攻击 +4。", "Upgraded: +20 maximum health, +4 damage."); sound = "reward"; }
            }
            if (action == "bow" || action == "staff")
            {
                int w = action == "bow" ? 1 : 2;
                if (Has("weapon_" + w)) { Equip(w); Notify("已装备。", "Equipped."); return; }
                if (Spend(w == 1 ? 4 : 0, w == 2 ? 4 : 0, 0, 0, w == 1 ? 2 : 3)) { Flag("weapon_" + w); Equip(w); sound = "reward"; Notify("制作成功！背包 I 可以切换武器。", "Crafted! Switch weapons in your inventory, I."); }
            }
            if (action == "cook" && Spend(0, 0, 2, 1, 0)) { save.inventory[6]++; sound = "reward"; Notify("暖潮炖汤 ×1", "Tide stew ×1"); }
            if (action == "buymeal" && Spend(0, 0, 0, 0, 0, ShopStewCost)) { save.inventory[6]++; Notify("暖潮炖汤 ×1", "Tide stew ×1"); }
            if (action == "buywood" && Spend(0, 0, 0, 0, 0, ShopTimberCost)) { save.inventory[0] += 4; Notify("木材 ×4", "Timber ×4"); }
            if (action == "fish") { fishTime = fishCursor = 0; fishHits = fishMisses = 0; mode = "fish"; }
            if (action == "deliver" && save.stage < 3 && Has("core_" + (save.stage + 1)))
            { save.stage++; save.coins += 40; save.inventory[6]++; save.inventory[0] += 3; save.inventory[1] += 2; revision++; sound = "reward"; Notify("新章节开启！获得硬币、材料与一份炖汤。", "New chapter! Coins, materials and a warm stew received."); }
            if (action.StartsWith("ally"))
            {
                int id = int.Parse(action.Substring(4));
                if (Has("ally_" + id)) return;
                if (Spend(0, 0, id == 1 ? 6 : 0, id == 2 ? 3 : 0, id == 3 ? 5 : 0))
                { Flag("ally_" + id); save.coins += 40; save.inventory[6]++; save.skillPoints++; sound = "reward"; Notify("支线完成！获得同伴支持、40 硬币和 1 技能点。", "Commission complete! An ally, 40 coins and 1 skill point."); }
            }
            if (action == "boss")
            {
                if (save.room == 0 || !GateOpen || GateBarrierActive || enemies.Exists(e => e.boss) || Has("boss_" + save.room)) return;
                BossData b = data.bosses[save.room - 1]; enemies.Add(CreatePlacedEnemy(++enemyId, 3, new Point(29, 9), b.hp, true));
                save.health = save.maxHealth; save.energy = 100; Notify(b.name + "：闪避红圈，抓住反击时机！", b.en + ": dodge the red circles and counter!");
            }
            if (action.StartsWith("ending"))
            {
                int ending = int.Parse(action.Substring(6));
                if (ending == 3 && Allies < 3) { FinalConversation(); return; }
                save.ending = ending; Flag("completed"); Epilogue(); revision++;
            }
            if (action == "epilogue") Epilogue();
            if (action == "continue") Enter(0);
        }
        public void Eat()
        {
            if (practicing || Dying) return;
            if (mode != "world" && mode != "inventory") return;
            if (save.inventory[6] <= 0) { Notify("没有炖汤了。旅店可以制作或出售。", "No stew left. Visit Ari at the inn."); return; }
            save.inventory[6]--; save.health = Clamp(save.health + 55, 0, save.maxHealth); save.energy = 100;
            save.foodUntil = save.totalTime + 90; revision++; sound = "reward"; Notify("生命 +55，耐力回满，攻击增强 90 秒。", "+55 health, full energy, stronger attacks for 90 seconds.");
        }
        public void Equip(int weapon)
        { if (weapon >= 0 && weapon <= 2 && (weapon == 0 || Has("weapon_" + weapon))) { CancelAction(); save.weapon = weapon; revision++; } }
        public void Learn(int talent)
        {
            if (talent < 0 || talent > 2 || save.skillPoints <= 0 || save.talents[talent] >= 5) return;
            save.skillPoints--; save.talents[talent]++;
            if (talent == 1) { save.maxHealth += 12; save.health = Math.Min(save.maxHealth, save.health + 12); }
            revision++; sound = "reward";
        }
        public void FishHit()
        {
            if (mode != "fish") return;
            if (Math.Abs(fishCursor - .5f) <= .18f) { fishHits++; sound = "reward"; } else { fishMisses++; sound = "hurt"; }
            if (fishHits >= 3) { save.inventory[3]++; save.coins += 4; BountyEvent(2, 1); Close(); revision++; Notify("钓到银鳍鱼 ×1！", "Caught a Silverfin!"); }
            else if (fishMisses >= 3) { Close(); Notify("鱼游走了。没有损失，再试一次吧。", "The fish slipped away. Nothing lost; try again."); }
        }
        private void FinalConversation()
        {
            List<Choice> choices = new List<Choice> { C("归还星核 · 让大地恢复", "Return the cores · restore the land", "ending1"), C("保留屏障 · 守住堡垒", "Keep the shield · secure the citadel", "ending2") };
            if (Allies == 3) choices.Add(C("重建边境 · 与居民一起改变", "Rebuild the frontier · unite the towns", "ending3"));
            choices.Add(C("先完成镇民的委托", "Finish the town commissions first", "goto0"));
            Dialog("厄洛 · 星卫统领", "Orrin · Starwarden", "厄洛放下武器。堡垒的安全，建立在边境不断枯竭的大地之上。他知道，却不敢停止。\n\n星核就在你手中。你会把力量还给土地，还是留下屏障？\n\n" + (Allies < 3 ? "帮助药师、渔夫和铁匠后，也许会出现第三条路。你可以回来再做决定。" : "三位镇民带来了药物、粮食和工具：真正的防线，也可以由互相帮助的人组成。"), "Orrin lowers his weapon. The shield's safety comes from a frontier slowly dying. He knew, but feared stopping.\n\nThe cores are yours. Return their power to the land, or keep the shield?\n\n" + (Allies < 3 ? "Helping An, Old Tide and Noll may open a third path. You may return to decide later." : "Three townsfolk bring medicine, food and tools. A defense can also be built from people helping each other."), choices.ToArray());
        }
        public void Epilogue()
        {
            string[] zh = { "", "《重新生长》\n\n你把星核归还给森林、河流和大地。堡垒的屏障消散了，边境的道路重新长出嫩芽。\n\n危险没有消失，但每个人终于可以走向自己的明天。厄洛离开城墙，第一次亲手种下一棵树。", "《高墙之内》\n\n屏障仍然照亮夜空。你说服统领打开城门，把边境居民接进堡垒。\n\n外面的土地仍在衰弱。安全有了新的主人，也有了新的代价。总有一天，人们还需要回答同一个问题。", "《我们的边境》\n\n药师带来了药物，渔夫带来了粮食，铁匠把机器改成了水泵。星核不再供养一座堡垒，而是慢慢回到每一个村庄。\n\n厄洛拆下第一段城墙，加入修路的人群。\n\n边境没有突然变得安全。但有人愿意守望，有人愿意建设，还有人愿意再出发。\n\n冒险者，欢迎回家。" };
            string[] en = { "", "GROWING AGAIN\n\nYou return the cores to forest, river and earth. The shield fades; shoots appear along the roads.\n\nDanger remains, but tomorrow belongs to everyone. Orrin leaves his walls and plants a tree for the first time.", "WITHIN THE WALLS\n\nThe shield still lights the sky. You persuade Orrin to open the gates to the frontier's people.\n\nThe land outside keeps fading. Safety has new owners, and a new cost. One day, the same question will return.", "OUR FRONTIER\n\nAn brings medicine. Old Tide brings food. Noll rebuilds the machines as water pumps. The cores return, little by little, to every village.\n\nOrrin removes the first wall and joins the road builders.\n\nThe frontier is not suddenly safe. But some will keep watch, some will build, and some will set out again.\n\nWelcome home, adventurer." };
            int ending = (int)Clamp(save.ending, 1, 3);
            Dialog("星陨边境 · 终章", "Starfall Frontier · Epilogue", zh[ending], en[ending], C("继续冒险与生活", "Keep adventuring", "continue")); sound = "reward";
        }
        public string GateHint(bool en)
        {
            if (save.room == 1) return en ? "Defeat 4 enemies, then rescue the scout at (21,7)." : "击败 4 个敌人，再救出右上方（21,7）的侦察员。";
            if (save.room == 2) return en ? "Activate the tablets: Sun (12,11), Moon (23,17), Tide (18,6). The northwest relic holds the clue." : "依次触碰日（12,11）、月（23,17）、潮（18,6）石碑；左上遗物有线索。";
            return en ? "Gather 3 parts, then repair the elevator at (21,10)." : "收集 3 个零件，修复右上方（21,10）的升降机。";
        }
        public string Objective()
        {
            string extra = ExpansionObjective(); if (extra != null) return extra;
            if (save.ending > 0) return L("边境迎来了新的日子。继续冒险与生活。", "A new day on the frontier. Keep exploring.");
            if (save.room == 0)
            {
                if (save.stage < 3 && Has("core_" + (save.stage + 1))) return L("把星核交给莉雅（城镇南侧的公会）。", "Deliver the core to Lyra at the south guild hall.");
                return L("前往" + data.maps[save.stage + 1].name + " · 开始下一章", "Travel to " + data.maps[save.stage + 1].en + " · next chapter");
            }
            if (!GateOpen) return GateHint(save.english);
            if (save.room == 4 && Has("boss_4")) return L("回到堡垒大门，决定边境的未来。", "Return to the citadel gate and choose the frontier's future.");
            if (Has("core_" + save.room)) return L("返回余烬镇，把星核交给莉雅。", "Return to Emberfall and deliver the starcore.");
            if (Has("boss_" + save.room)) return L("到右上角的祭坛收取星核。", "Recover the starcore at the northeast altar.");
            return L("到右上角祭坛挑战守护者 · 注意红色预告", "Challenge the guardian at the northeast altar · watch the red warnings");
        }
    }
}
