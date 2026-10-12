using System;
using System.Collections.Generic;

namespace Starfall
{
    // Distant shores share the same combat, economy and save model as the original campaign.
    public sealed partial class GameCore
    {
        public readonly List<Hazard> hazards = new List<Hazard>();
        public Point companionPosition;
        public float companionCooldown, raceTime, comboTimer, riposteTime;
        public int raceCheckpoint, combo;
        public bool racing, perfectDodgeArmed;
        // Short-lived dodge escape proof, never serialized into old save files.
        private Point dashOrigin;
        private float dodgeEscapeTime;
        private float companionAttack, climateTimer, stormTimer;
        private MapData abyssMap;
        private bool previousLowTide;
        public static readonly Point[] RaceRoute = { new Point(8, 18), new Point(8, 5), new Point(23, 5), new Point(30, 18), new Point(14, 18) };
        public static readonly string[] RelicNames = { "回潮瓶", "琥珀种", "风蚀罗盘", "断钟片", "薪火徽记", "裂隙石" };
        public static readonly string[] RelicNamesEn = { "Tide Flask", "Amber Seed", "Wind Compass", "Broken Chime", "Hearth Emblem", "Rift Stone" };
        public static readonly string[] RelicText = { "每次闪避回复 3 生命。", "生命不低于 80% 时，攻击 +6。", "移动 +0.4；闪避多消耗 5 耐力。", "完美闪避后的反击机会延长至 3 秒。", "技能回复 8 生命；技能多消耗 10 耐力。", "暴击率 +12%；受到伤害 +15%。" };
        public static readonly string[] RelicTextEn = { "Dodging heals 3 health.", "+6 damage while at 80% health or above.", "+0.4 speed; dodges cost 5 more energy.", "Perfect-dodge counter window lasts 3 seconds.", "Skills heal 8; skills cost 10 more energy.", "+12% critical chance; take 15% more damage." };
        public static readonly string[] CompanionNames = { "米拉", "伊沃", "森" }, CompanionNamesEn = { "Mira", "Ivo", "Sen" };
        public static readonly string[] FactionNames = { "守灯会", "摆渡盟", "山野议会" }, FactionNamesEn = { "Lamplighters", "Ferry Union", "Wild Assembly" };
        public static readonly string[] BountyNames = { "清理商路 · 击败敌人", "矿脉巡检 · 采集矿石", "难民晚餐 · 钓获银鳍鱼", "危险信号 · 击败守护者", "拆解重甲 · 击败重型敌人", "地脉勘测 · 结算深渊楼层" };
        public static readonly string[] BountyNamesEn = { "Clear the road · defeat enemies", "Vein survey · gather ore", "Refugee supper · catch Silverfin", "Danger signal · defeat a guardian", "Break the armor · defeat heavy foes", "Root survey · bank abyss floors" };
        public static readonly int[] BountyTargets = { 6, 3, 3, 1, 4, 2 };

        public bool InAbyss { get { return save.room == 8 && save.abyssFloor > 0; } }
        public int Day { get { return 1 + (int)(save.totalTime / 240); } }
        public bool Night { get { return save.totalTime % 240 >= 150; } }
        public bool LowTide { get { return save.totalTime < save.tideHeldUntil || save.totalTime % 36 < 22; } }
        public int Proofs { get { return (Has("harbor_report") ? 1 : 0) + (Has("desert_report") ? 1 : 0) + (Has("mountain_report") ? 1 : 0); } }
        public int ProjectTiers { get { return save.projects[0] + save.projects[1] + save.projects[2]; } }
        public int CompanionCount { get { int n = 0; for (int i = 0; i < 3; i++) if (Has("companion_" + i)) n++; return n; } }
        public bool SafeTravel { get { return !enemies.Exists(e => (e.Position - Position).Length < (save.frontierEnding == 2 ? 4 : 6)); } }
        public int MasteryRank(int weapon) { return weapon < 0 || weapon > 2 ? 0 : Math.Min(5, save.mastery[weapon] / 20); }
        public bool Relic(int id) { return save.relics[0] == id || save.relics[1] == id; }
        public bool CanEnter(int room)
        {
            if (room < 0 || room >= data.maps.Length) return false;
            if (room == 9) return true;
            if (room <= 4) return room <= save.stage + 1;
            if (room == 5) return save.stage >= 1 && Has("exp_intro");
            if (room == 6) return Has("harbor_report");
            if (room == 7) return Has("desert_report");
            return room == 8 && save.frontierEnding > 0;
        }
        public string RouteHint(int room, bool en)
        {
            if (room <= 4) return en ? "Deliver the previous core to Lyra." : "先把上一片区域的星核交回莉雅。";
            if (room == 5) return en ? "Deliver the first core, then meet Lune in Emberfall (26,9)." : "交回第一枚星核后，找余烬镇的露恩（26,9）接取远岸来信。";
            if (room == 6) return en ? "Resolve the lighthouse crisis and report to Mira." : "解决灯塔危机，向米拉提交港口调查。";
            if (room == 7) return en ? "Restore the light channel and report to Ivo." : "修复沙海光路，向伊沃提交调查。";
            return en ? "Gather all three reports and settle the council at Mistport." : "完成三地调查，在雾港议事厅达成新的边境协定。";
        }
        private static int[] BoundedArray(int[] value, int length, int min, int max)
        {
            int[] result = new int[length];
            if (value != null) Array.Copy(value, result, Math.Min(length, value.Length));
            for (int i = 0; i < length; i++) result[i] = Math.Max(min, Math.Min(max, result[i]));
            return result;
        }
        private void SanitizeExpansion()
        {
            bool legacy = save.version == 1;
            if (legacy) { save.companion = save.bounty = -1; save.relics = new[] { -1, -1 }; }
            save.version = 2;
            save.reputation = BoundedArray(save.reputation, 3, 0, 10);
            save.relationships = BoundedArray(save.relationships, 3, 0, 5);
            save.projects = BoundedArray(save.projects, 3, 0, 2);
            save.mastery = BoundedArray(save.mastery, 3, 0, 100);
            save.mirrors = BoundedArray(save.mirrors, 3, 0, 3); save.bells = BoundedArray(save.bells, 3, 0, 3);
            save.boons = BoundedArray(save.boons, 3, 0, 4);
            if (save.relics == null || save.relics.Length != 2) save.relics = new[] { -1, -1 };
            for (int i = 0; i < 2; i++) if (save.relics[i] < 0 || save.relics[i] > 5 || !Has("relic_" + save.relics[i])) save.relics[i] = -1;
            if (save.relics[0] == save.relics[1]) save.relics[1] = -1;
            save.companion = (int)Clamp(save.companion, -1, 2);
            if (save.companion >= 0 && !Has("companion_" + save.companion)) save.companion = -1;
            save.harborPledge = (int)Clamp(save.harborPledge, 0, 3); save.desertChoice = (int)Clamp(save.desertChoice, 0, 2);
            save.mountainChoice = (int)Clamp(save.mountainChoice, 0, 2); save.frontierEnding = (int)Clamp(save.frontierEnding, 0, 3);
            save.shards = (int)Clamp(save.shards, 0, 99999); save.tea = (int)Clamp(save.tea, 0, 999);
            save.relicRank = (int)Clamp(save.relicRank, 0, 3);
            save.technique = (int)Clamp(save.technique, 0, 1); if (!Has("technique_" + save.profession)) save.technique = 0;
            save.bounty = (int)Clamp(save.bounty, -1, 5); save.bountyProgress = (int)Clamp(save.bountyProgress, 0, save.bounty < 0 ? 0 : BountyTargets[save.bounty]);
            save.bountyCycle = (int)Clamp(save.bountyCycle, 0, 10000);
            save.abyssFloor = (int)Clamp(save.abyssFloor, 0, 6); save.abyssRisk = (int)Clamp(save.abyssRisk, 0, 2);
            save.abyssSeed &= 0x7fffffff;
            save.abyssSnapshotFloor = (int)Clamp(save.abyssSnapshotFloor, 0, 6);
            if (save.abyssEnemies != null && save.abyssEnemies.Length > 20) save.abyssEnemies = null;
            save.runCoins = (int)Clamp(save.runCoins, 0, 9999); save.runShards = (int)Clamp(save.runShards, 0, 9999); save.runXp = (int)Clamp(save.runXp, 0, 9999);
            save.bestFloor = (int)Clamp(save.bestFloor, 0, 6); save.bestRace = (int)Clamp(save.bestRace, 0, 9999);
            save.heat = Finite(save.heat) ? Clamp(save.heat, 0, 100) : 0; save.cold = Finite(save.cold) ? Clamp(save.cold, 0, 100) : 0;
            float time = Finite(save.totalTime) ? Clamp(save.totalTime, 0, 10000000) : 0;
            save.tideHeldUntil = Finite(save.tideHeldUntil) ? Clamp(save.tideHeldUntil, 0, time + 90) : 0;
            save.teaUntil = Finite(save.teaUntil) ? Clamp(save.teaUntil, 0, time + 180) : 0;
            if (save.room == 8 && save.abyssFloor == 0) save.room = 0;
            if (save.room != 8) { save.abyssFloor = 0; save.runCoins = save.runShards = save.runXp = 0; save.abyssEnemies = null; save.abyssSnapshotFloor = 0; Array.Clear(save.boons, 0, 3); }
        }
        private void ResetExpansionTransient()
        {
            hazards.Clear(); racing = false; raceTime = 0; raceCheckpoint = 0;
            companionAction = null; companionAim = new Point(1, 0); companionMoving = false; companionMoveTime = 0;
            companionPosition = CompanionFollowTarget(); companionAttack = 2; companionCooldown = 0;
            climateTimer = 1; stormTimer = 6; ClearFrost();
            combo = 0; comboTimer = riposteTime = 0; perfectDodgeArmed = false; dashOrigin = new Point(); dodgeEscapeTime = 0; previousLowTide = LowTide;
        }
        private bool SpawnExpansionRoom()
        {
            if (save.room == 9) return true;
            if (save.room < 5) return false;
            if (save.room == 8)
            {
                GenerateAbyss();
                if (Has("abyss_clear_" + save.abyssFloor)) return true;
                if (RestoreAbyssEnemies()) return true;
                save.runCoins = save.runShards = save.runXp = 0;
                var random = new System.Random(save.abyssSeed + save.abyssFloor * 197);
                for (int i = 0; i < 5 + save.abyssFloor; i++)
                {
                    Point p = new Point(16.5f, 14.5f);
                    for (int attempt = 0; attempt < 100; attempt++)
                    { p = new Point(random.Next(13, 32) + .5f, random.Next(5, 19) + .5f); if (Fits(p) && (p - Position).Length > 5) break; }
                    enemies.Add(CreatePlacedEnemy(++enemyId, i % 3, p, 38 + save.abyssFloor * 6 + save.abyssRisk * 10));
                }
                if (save.abyssFloor == 6) enemies.Add(CreatePlacedEnemy(++enemyId, 3, new Point(29, 9), CurrentBoss.hp, true));
                PrepareSave();
                return true;
            }
            float[,] spots = { { 11, 12 }, { 15, 17 }, { 21, 15 }, { 24, 6 }, { 9, 7 }, { 30, 16 }, { 23, 19 } };
            for (int i = 0; i < 7; i++)
            {
                Point p = new Point(spots[i, 0], spots[i, 1]);
                enemies.Add(CreatePlacedEnemy(++enemyId, i % 3, p, 42 + (save.room - 5) * 12));
            }
            return true;
        }
        public void PrepareSave()
        {
            CaptureWorld();
            if (!InAbyss) return;
            save.abyssSnapshotFloor = save.abyssFloor; save.abyssEnemies = new EnemyRecord[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            { Enemy e = enemies[i]; save.abyssEnemies[i] = new EnemyRecord { id = e.id, kind = e.kind, boss = e.boss, x = e.x, y = e.y, hp = e.hp, maxHp = e.maxHp }; }
        }
        private bool RestoreAbyssEnemies()
        {
            if (save.abyssSnapshotFloor != save.abyssFloor || save.abyssEnemies == null || save.abyssEnemies.Length == 0) return false;
            var ids = new HashSet<int>();
            foreach (EnemyRecord r in save.abyssEnemies)
            {
                if (r == null || r.id < 1 || r.id > 99999 || !ids.Add(r.id) || !Finite(r.hp) || !Finite(r.maxHp) || r.hp <= 0) continue;
                Point p = SafeActorFoot(new Point(r.x, r.y));
                float maximum = Clamp(r.maxHp, 1, 5000);
                var e = new Enemy(r.id, (int)Clamp(r.kind, 0, 3), p.x, p.y, maximum, r.boss && save.abyssFloor == 6);
                e.hp = Clamp(r.hp, .1f, maximum); enemies.Add(e); enemyId = Math.Max(enemyId, r.id);
            }
            if (enemies.Count == 0) { save.runCoins = save.runShards = save.runXp = 0; return false; }
            if (save.abyssFloor == 6 && !Has("boss_8") && !enemies.Exists(e => e.boss)) enemies.Add(CreatePlacedEnemy(++enemyId, 3, new Point(29, 9), CurrentBoss.hp, true));
            return true;
        }
        private void OnExpansionKill(Enemy e, int releasedWeapon = -1)
        {
            int weapon = releasedWeapon >= 0 && releasedWeapon <= 2 ? releasedWeapon : save.weapon;
            save.mastery[weapon] = Math.Min(100, save.mastery[weapon] + (e.boss ? 10 : 2));
            BountyEvent(0, 1); if (e.boss) BountyEvent(3, 1); if (e.kind == 2) BountyEvent(4, 1);
            if (InAbyss)
            {
                save.runCoins += e.boss ? 70 : 8 + save.abyssRisk * 3;
                save.runShards += (e.boss ? 5 : 1) + save.abyssRisk;
                save.runXp += e.boss ? 100 : 14;
                if (enemies.Count == 0) { Flag("abyss_clear_" + save.abyssFloor); Notify("本层已清理。到东侧地脉门选择祝福并结算。", "Floor cleared. Choose a blessing and bank rewards at the east gate."); }
                PrepareSave();
            }
        }
        private void AddXp(int amount)
        {
            save.xp += amount;
            while (save.xp >= save.level * 40 && save.level < 30)
            { save.xp -= save.level * 40; save.level++; save.skillPoints++; save.health = save.maxHealth; }
        }
        private void Reputation(int faction, int amount) { save.reputation[faction] = Math.Min(10, save.reputation[faction] + amount); revision++; }
        private void RewardRelic(int id) { Flag("relic_" + id); sound = "reward"; }
        public void EquipRelic(int slot, int id)
        {
            if (slot < 0 || slot > 1 || id < -1 || id > 5 || (id >= 0 && !Has("relic_" + id))) return;
            if (id >= 0 && save.relics[1 - slot] == id) save.relics[1 - slot] = -1;
            save.relics[slot] = id; revision++; sound = "reward";
        }
        public void SelectCompanion(int id)
        {
            if (id < -1 || id > 2 || (id >= 0 && !Has("companion_" + id)) || !SafeTravel) return;
            save.companion = id; companionAction = null; companionPosition = CompanionFollowTarget(); companionAttack = 1; revision++;
        }
        public void CompanionStory(int id)
        {
            if (id < 0 || id > 2 || !Has("companion_" + id)) return;
            PersonalConversation(id, new WorldObject { name = CompanionNames[id], en = CompanionNamesEn[id] });
        }
        public void CommandCompanion()
        {
            if (mode != "world" || Dying || practicing || save.companion < 0 || companionCooldown > 0) return;
            companionCooldown = save.frontierEnding == 3 ? 12 : 18;
            companionAction = new ActorAction { kind = "command", weapon = save.companion, combo = save.relationships[save.companion], damage = Damage,
                direction = aimTarget.HasValue?(aimTarget.Value-companionPosition).Normalized:aim, target=aimTarget, duration = .52f, contact = .2f, impact = CaptureImpact(save.companion==1?1:2,0,false) };
            companionAim = companionAction.direction;
        }
        private void ReleaseCompanionCommand(ActorAction action)
        {
            int bond = action.combo; sound = "skill";
            if (action.weapon == 0)
            { save.health = Math.Min(save.maxHealth, save.health + 18 + bond * 3); save.energy = Math.Min(100, save.energy + 15); guardTime = Math.Max(guardTime, 3); }
            else if (action.weapon == 1)
                for (int i = -1; i <= 1; i++)
                {
                    Projectile shot = FriendlyShot(companionPosition, 1, action.direction, 13, action.damage * (.8f + bond * .05f), false, action.target, action.impact);
                    double angle = Math.Atan2(shot.velocity.y, shot.velocity.x) + i * .17;
                    shot.velocity = new Point((float)Math.Cos(angle), (float)Math.Sin(angle)) * 13; shot.piercing = true; projectiles.Add(shot);
                }
            else
            {
                BeginFrost(Position,action.damage,action.impact??CaptureImpact(2,0,false));
                foreach (Enemy e in enemies) if ((e.Position - Position).Length < 5 && ClearAttackPath(Position, e.Position)) { e.stun = 1.4f; e.state = 0; e.timer = 1.2f; }
            }
            if (action.weapon == 0) effects.Add(new Effect("heal", Position, .6f));
            else if (action.weapon == 1) effects.Add(new Effect("release", WeaponOrigin(companionPosition, 1, action.direction), .18f) { direction = action.direction, variant = 1, height = WeaponHeight(1, action.direction) });
        }
        private Point CompanionFollowTarget()
        {
            foreach (Point offset in companionOffsets) if (Fits(Position + offset)) return Position + offset;
            return Fits(companionPosition) && (companionPosition - Position).Length < 6 ? companionPosition : Position;
        }
        private void TickCompanion(float dt)
        {
            companionMoving = false;
            if (save.companion < 0 || practicing) { companionAction = null; return; }
            companionCooldown = Math.Max(0, companionCooldown - dt); companionAttack -= dt;
            if (companionAction != null)
            {
                ActorAction action = companionAction;
                if (action.weapon != save.companion) { companionAction = null; return; }
                action.elapsed += dt;
                if (!action.released && action.elapsed + .00001f >= action.contact)
                {
                    action.released = true;
                    if (action.kind == "command") ReleaseCompanionCommand(action);
                    else if (action.kind == "heal")
                    { save.health = Math.Min(save.maxHealth, save.health + 6 + action.combo); effects.Add(new Effect("number", Position, .7f, "+HP")); sound = "skill"; }
                    else
                    {
                        projectiles.Add(FriendlyShot(companionPosition, action.weapon == 1 ? 1 : 2, action.direction, 9, action.damage, action.weapon == 2, action.target, action.impact));
                        effects.Add(new Effect("release", WeaponOrigin(companionPosition, action.weapon == 1 ? 1 : 2, action.direction), .18f) { direction = action.direction, height = WeaponHeight(action.weapon == 1 ? 1 : 2, action.direction) }); sound = action.weapon == 1 ? "arrow" : "spell";
                    }
                }
                if (action.elapsed >= action.duration) companionAction = null;
                return; // Plant the feet while drawing a bow or casting.
            }
            Point follow = CompanionFollowTarget();
            Point delta = follow - companionPosition;
            if (!Fits(companionPosition)) companionPosition = follow; // Recovery only; never teleport to catch up.
            else if (delta.Length > .3f)
            {
                Point heading = DirectionTo(companionPosition, follow), before = companionPosition;
                Point next = SweepActor(before, heading * Math.Min(delta.Length, dt * (Speed + 1)));
                Point travelled = next - before;
                if (travelled.Length > .001f) { companionPosition = next; companionMoving = true; companionMoveTime += travelled.Length; companionAim = travelled.Normalized; }
            }
            if (companionAttack > 0) return;
            companionAttack = save.companion == 1 ? 2.8f : 6;
            string kind = null; Point direction = aim;Point? attackTarget=null;
            if (save.companion == 0 && save.health < save.maxHealth * .8f)
            { kind = "heal"; direction = (Position - companionPosition).Normalized; }
            else
            {
                Enemy target = enemies.Find(e => (e.Position - companionPosition).Length < 7 && ClearAttackPath(companionPosition, e.Position));
                if (target != null) { kind = "attack"; direction = (target.Position - companionPosition).Normalized;attackTarget=target.Position; }
            }
            if (kind != null)
            {
                companionAim = direction; companionMoving = false;
                companionAction = new ActorAction { kind = kind, weapon = save.companion, combo = save.relationships[save.companion], direction = direction,target=attackTarget,
                    damage = 9 + save.relationships[save.companion] * 2, duration = .42f, contact = .16f, impact = CaptureImpact(save.companion==1?1:2,0,false) };
            }
        }
        public void LearnTechnique()
        {
            if (Has("technique_" + save.profession)) { CancelAction(); save.technique = 1 - save.technique; revision++; return; }
            if (save.level < 5 || save.skillPoints < 1) return;
            CancelAction(); save.skillPoints--; Flag("technique_" + save.profession); save.technique = 1; sound = "reward";
        }
        private void AlternateSkill(ActorAction action)
        {
            if (save.profession == 0)
            {
                guardTime = 4; effects.Add(new Effect("burst", Position, .7f) { variant = 0, radius = 3.8f });
                foreach (Enemy e in enemies.ToArray()) if ((e.Position - Position).Length < 3.8f && ClearAttackPath(Position, e.Position)) { Hit(e, action.damage * 1.65f, true,null,action.impact); e.stun = e.boss ? 1 : 2.2f; }
                RetireHostileProjectiles(Position, 4);
            }
            else if (save.profession == 1)
            {
                effects.Add(new Effect("release", WeaponOrigin(Position, 1, aim), .18f) { direction = aim, variant = 1, height = WeaponHeight(1, aim) });
                Projectile shot = FriendlyShot(Position, 1, aim, 17, action.damage * 3.4f, false, action.target, action.impact); shot.life = 2.5f; shot.piercing = shot.technique = true; projectiles.Add(shot);
            }
            else { BeginFrost(Position,action.damage,action.impact??CaptureImpact(2)); }
        }
        private bool NearKind(string kind, float range)
        {
            foreach (WorldObject o in Map.objects) if (o.kind == kind && (new Point(o.x, o.y) - Position).Length < range) return true;
            return false;
        }
        private void TickExpansion(float dt)
        {
            if (mode != "world" || Dying) return;
            comboTimer -= dt; riposteTime -= dt;
            TickCompanion(dt);
            TickFrost(dt);
            if(Dying||mode!="world")return;
            bool tea = save.teaUntil > save.totalTime;
            save.heat = Clamp(save.heat + dt * (save.room == 6 && !tea && !NearKind("shade", 3) && !racing ? 1.3f : -10), 0, 100);
            bool warm = false;
            if (save.room == 7) foreach (WorldObject o in Map.objects) if (o.kind == "brazier" && Has("warm_" + o.id) && (new Point(o.x, o.y) - Position).Length < 3.5f) warm = true;
            save.cold = Clamp(save.cold + dt * (save.room == 7 && !tea && !warm ? 1.1f : -8), 0, 100);
            climateTimer -= dt;
            if (climateTimer <= 0) { climateTimer = 1; if (save.heat >= 85 || save.cold >= 90) Hurt(4); }
            if (Dying) return;
            if (racing)
            {
                raceTime += dt;
                if ((Position - RaceRoute[raceCheckpoint]).Length < 1.35f)
                {
                    raceCheckpoint++; sound = "reward";
                    if (raceCheckpoint == RaceRoute.Length) FinishRace();
                }
                if (racing && raceTime > 30) { racing = false; Notify("风向变了，本轮结束。没有损失，回到沙舟重新出发。", "The wind shifted. No loss; return to the skiff to retry."); }
            }
            if (InAbyss && !Has("abyss_clear_" + save.abyssFloor) && save.abyssFloor >= 3)
            {
                stormTimer -= dt;
                if (stormTimer <= 0) { stormTimer = 6; AddHazard(Position, 1.4f, 1.4f, 10 + save.abyssRisk * 4, "rift"); }
            }
            tickingHazards.Clear();tickingHazards.AddRange(hazards);
            foreach (Hazard h in tickingHazards)
            {
                if (Dying) break;
                h.timer -= dt;
                if (h.timer > 0) continue;
                hazards.Remove(h); effects.Add(new Effect("danger", h.position, .45f) { radius = h.radius, variant = HazardTheme(h) });
                if ((Position - h.position).Length < h.radius) Hurt(h.damage, "area", (Position - h.position).Normalized);
                else TryRewardEscapedWarning(h.position, h.radius);
            }
            tickingHazards.Clear();
        }
        private void AddHazard(Point p, float radius, float delay, float damage, string kind)
        { hazards.Add(new Hazard { position = p, radius = radius, timer = delay, damage = damage, kind = kind }); }
        public static int HazardTheme(Hazard hazard)
        { return hazard.kind == "tide" ? 5 : hazard.kind == "glass" ? 6 : hazard.kind == "bell" ? 7 : 8; }
        private void BossExpansionAttack(Enemy e)
        {
            // The harbor beast and glass guardian learn to sweep the other axis
            // as their armor breaks. Warnings remain visible Hazard objects.
            if (save.room == 5 || save.room == 6)
            {
                bool harbor = save.room == 5;
                bool horizontal = harbor ? e.BossPhase == 1 || e.cycle % 2 == 1
                                         : e.BossPhase > 1 && e.cycle % 2 == 0;
                float radius = harbor ? 1.35f : 1.1f;
                float delay = harbor ? 1.2f : .9f;
                float damage = harbor ? 17 : 18;
                string kind = harbor ? "tide" : "glass";
                for (int i = -1; i <= 1; i++)
                {
                    Point offset = horizontal ? new Point(i * 2, 0) : new Point(0, i * 2);
                    // Final-phase contacts travel along the lane in sequence;
                    // each circle is warned before its own delayed impact.
                    float sweepDelay = e.BossPhase == 3 ? (i + 1) * .22f : 0;
                    AddHazard(e.target + offset, radius, delay + sweepDelay, damage, kind);
                }
            }
            if (save.room == 7 || save.room == 8)
            {
                Point center = Position;
                AddHazard(center, 2, 1.5f, 20 + e.BossPhase * 2, "rift");
                if (save.room == 8 && InAbyss && save.abyssFloor == 6 && e.BossPhase >= 2)
                {
                    // Paired roots close on the player; alternate the axis each release.
                    Point axis = e.cycle % 2 == 1 ? new Point(1, 0) : new Point(0, 1);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Point outer = center + axis * (side * 2.8f);
                        if (Fits(outer)) AddHazard(outer, 1.05f, 1.85f, 16 + save.abyssRisk * 3, "root");
                        if (e.BossPhase == 3)
                        {
                            Point inner = center + axis * (side * 1.5f);
                            if (Fits(inner)) AddHazard(inner, .9f, 2.25f, 16 + save.abyssRisk * 3, "root");
                        }
                    }
                }
                if (save.room == 7 && e.BossPhase >= 2)
                {
                    // Three blue bell strikes surround the frost mark,
                    // leaving one rotating cardinal exit unmarked.
                    Point[] directions = { new Point(1, 0), new Point(0, 1),
                                           new Point(-1, 0), new Point(0, -1) };
                    int safeExit = (e.cycle - 1) % directions.Length;
                    int strike = 0;
                    for (int i = 0; i < directions.Length; i++)
                    {
                        if (i == safeExit) continue;
                        float delay = 1.75f + (e.BossPhase == 3 ? strike * .22f : 0);
                        AddHazard(center + directions[i] * 3.5f, 1.1f, delay, 14, "bell");
                        strike++;
                    }
                }
            }
        }
        public string Conditions()
        {
            if (InAbyss) return L("地脉 ", "ABYSS ") + save.abyssFloor + "/6  ·  " + L("未结算碎片 ", "UNBANKED SHARDS ") + save.runShards;
            if (save.room == 5) return LowTide ? L("低潮 · 潮滩可通行", "LOW TIDE · shoals open") : L("高潮 · 等待退潮或使用闸门", "HIGH TIDE · wait or use the sluice");
            if (save.room == 6) return L("热度 ", "HEAT ") + (int)save.heat + "/100 · " + L("绿洲降温 / 凉茶免疫", "shade cools / tea protects");
            if (save.room == 7) return L("寒意 ", "COLD ") + (int)save.cold + "/100 · " + L("点燃火盆 / 凉茶抗寒", "light braziers / tea protects");
            return L("第 ", "DAY ") + Day + L(" 日 · ", " · ") + (Night ? L("星夜", "STARLIT NIGHT") : L("日照", "DAYLIGHT"));
        }
        public void DrinkTea()
        {
            if (practicing || Dying) return;
            if (save.tea <= 0 || (mode != "world" && mode != "inventory" && mode != "camp")) return;
            save.tea--; save.heat = save.cold = 0; save.teaUntil = save.totalTime + 180; save.energy = 100; revision++;
            Notify("星薄荷凉茶：耐力回满，免疫暑寒并加快耐力回复 180 秒。", "Star-mint tea: full energy, climate immunity and faster energy recovery for 180 seconds.");
            sound = "reward";
        }
        public bool BuildProject(int project)
        {
            if (save.room != 0 || project < 0 || project > 2 || save.projects[project] >= 2) return false;
            int tier = save.projects[project];
            if (!Spend(4 + tier * 2, 2 + tier, 0, 0, 3 + tier, 30 + tier * 20)) return false;
            save.projects[project]++; Reputation(2, 1);
            if (project == 0 && save.projects[0] == 2) RewardRelic(1);
            Notify("城镇工程完成！新设施已投入使用。", "Town project complete. The facility is now active."); sound = "reward"; revision++; return true;
        }
        public bool RefineRelics()
        {
            int cost = (save.relicRank + 1) * 8;
            if (save.room != 0 || save.projects[2] == 0 || save.relicRank >= 3 || save.shards < cost) return false;
            if (!Spend(0, 2, 0, 0, 0, save.projects[2] == 2 ? 0 : 20)) return false;
            save.shards -= cost; save.relicRank++; revision++; sound = "reward"; return true;
        }
        public void AcceptBounty(int id)
        {
            if (id < 0 || id >= 6 || save.bounty >= 0 || (id == 5 && !CanEnter(8))) return;
            save.bounty = id; save.bountyProgress = 0; TrackQuest("bounty"); revision++;
        }
        private void BountyEvent(int id, int count)
        { if (save.bounty == id) { save.bountyProgress = Math.Min(BountyTargets[id], save.bountyProgress + count); revision++; } }
        public bool ClaimBounty()
        {
            if (save.bounty < 0 || save.bountyProgress < BountyTargets[save.bounty]) return false;
            save.coins += 35; save.shards += 3 + (save.frontierEnding == 3 ? 1 : 0); AddXp(30); save.bounty = -1; save.bountyProgress = 0; save.bountyCycle++; revision++; sound = "reward"; return true;
        }
        public void AbandonBounty() { save.bounty = -1; save.bountyProgress = 0; revision++; }
        private int RescueCount { get { int n = 0; for (int i = 0; i < 3; i++) if (Has("rescue_" + i)) n++; return n; } }
        private int RepairCount { get { int n = 0; for (int i = 0; i < 3; i++) if (Has("relay_" + i)) n++; return n; } }
        private void CheckHarborGate()
        {
            if ((save.harborPledge == 1 && RepairCount == 3) || (save.harborPledge == 2 && RescueCount == 3) || (save.harborPledge == 3 && RescueCount >= 2 && RepairCount >= 1))
            { Flag("gate_5"); Notify("灯塔航道已打开。去东北灯塔调查异常潮汐。", "The lighthouse channel opens. Investigate the northeast tower."); }
        }
        public List<LightRay> LightRays()
        {
            // This transient view is consumed immediately by the renderer and
            // mirror puzzle; recompute into retained storage without per-pass GC.
            List<LightRay> rays=lightRayBuffer;rays.Clear();if(save.room!=6)return rays;
            Point origin = new Point(10, 4), direction = new Point(0, 1);
            HashSet<int> visited=visitedMirrors;visited.Clear();
            for (int bounce = 0; bounce < 5; bounce++)
            {
                Point end = origin; int mirror = -1;
                for (int step = 1; step < 70; step++)
                {
                    end = origin + direction * (step * .5f);
                    if (end.x < 2 || end.x >= 34 || end.y < 2 || end.y >= 22 || Map.rows[(int)end.y][(int)end.x] == '#') break;
                    foreach (WorldObject o in Map.objects)
                    {
                        if (!o.kind.StartsWith("mirror") || (new Point(o.x, o.y) - origin).Length < .1f) continue;
                        if ((new Point(o.x, o.y) - end).Length < .26f) { mirror = int.Parse(o.kind.Substring(6)); end = new Point(o.x, o.y); break; }
                    }
                    if (mirror >= 0 || (end - new Point(29, 17)).Length < .3f) break;
                }
                rays.Add(new LightRay(origin, end));
                if (mirror < 0 || visited.Contains(mirror)) break;
                visited.Add(mirror); origin = end;
                int turn=save.mirrors[mirror];direction=turn==0?new Point(0,-1):turn==1?new Point(1,0):turn==2?new Point(0,1):new Point(-1,0);
            }
            return rays;
        }
        private void CheckMirrors()
        {
            foreach (LightRay ray in LightRays()) if ((ray.to - new Point(29, 17)).Length < .3f)
            { Flag("gate_6"); Flag("mirrors_lit"); Notify("光路贯通！沉睡的蓄光机重新启动。", "The light channel reaches the receiver. The reservoir wakes."); }
        }
        private void CheckBells()
        {
            if (save.bells[0] == 2 && save.bells[1] == 0 && save.bells[2] == 1)
            { Flag("gate_7"); Notify("北钟三、东钟一、南钟二。钟阵共鸣，山门开启。", "North III, east I, south II. The chimes align and open the pass."); }
        }
        private bool TalkExpansion(WorldObject o)
        {
            string kind = o.kind;
            if (kind == "envoy")
            {
                if (save.stage < 1) { Dialog(o.name, o.en, "我是雾港的信使露恩。先完成你的第一份星核委托，再来找我。有人在等一封永远送不到的回信。", "I am Lune, Mistport's courier. Finish your first core commission, then find me. Someone is waiting for a reply that will never arrive.", C("我会回来", "I will return", "close")); return true; }
                Dialog(o.name, o.en, "堡垒的水位记录被改过。雾港为堡垒守灯，却每年少一批摆渡人；沙海的机器仍在为一个死去的主人蓄光；高原的钟声压住的，也许不是暴风雪。\n\n去见船长米拉。把每一地的证词带回港口，让三方坐到同一张桌边。", "The citadel's water records were altered. Mistport loses ferrymen while keeping its lights; the desert stores light for a dead master; the mountain bells may be hiding more than storms.\n\nMeet Captain Mira. Bring each region's testimony to one council table.", C(Has("exp_intro") ? "前往雾港" : "接取远岸来信", Has("exp_intro") ? "Travel to Mistport" : "Accept Letters from Afar", Has("exp_intro") ? "goto5" : "exp_begin"), C("打开建设与委托簿", "Open town projects and contracts", "camp"), C("稍后再来", "Later", "close")); return true;
            }
            if (kind == "townboard" || kind == "contract") { mode = kind == "contract" ? "bounties" : "camp"; conversation = null; return true; }
            if (kind == "captain") { CaptainConversation(o); return true; }
            if (kind.StartsWith("castaway"))
            {
                int id = int.Parse(kind.Substring(8));
                Dialog(o.name, o.en, Has("rescue_" + id) ? "绳索结实，药也到了。我能自己回到避难船。谢谢你没有把我们当成损失数字。" : "最后一艘摆渡船已经开走。给我一段木绳和一株药草，我就能顺着潮滩回港。", Has("rescue_" + id) ? "The rope holds. I can reach the shelter boat. Thank you for treating us as people, not losses." : "The last ferry left. One timber for a rope and one herb will get me home.", C(Has("rescue_" + id) ? "一路平安" : "提供木材1、药草1", Has("rescue_" + id) ? "Safe travels" : "Give 1 timber and 1 herb", Has("rescue_" + id) ? "close" : "rescue" + id), C("稍后再来", "Later", "close")); return true;
            }
            if (kind.StartsWith("relay"))
            {
                int id = int.Parse(kind.Substring(5));
                Dialog(o.name, o.en, Has("relay_" + id) ? "灯线已经修好。海上的人能看见返航信号了。" : "旧线路烧断了。用一个零件修复信号塔，灯塔就能重新指引航船。", Has("relay_" + id) ? "The relay is repaired. Ships can see the homeward signal." : "The cable burned out. One part repairs this navigation relay.", C(Has("relay_" + id) ? "继续调查" : "修复 · 零件1", Has("relay_" + id) ? "Continue" : "Repair · 1 part", Has("relay_" + id) ? "close" : "relay" + id), C("稍后再来", "Later", "close")); return true;
            }
            if (kind == "sluice") { Dialog(o.name, o.en, "每 36 秒一轮潮汐：前 22 秒低潮。潮滩退水时可通过。投入两枚星晶，可让闸门保持低潮 90 秒。", "Each 36-second tide has 22 seconds of low water. Shoals open at low tide. Two crystals hold the sluice for 90 seconds.", C("固定低潮 · 星晶2", "Hold low tide · 2 crystals", "holdtide"), C("等待自然退潮", "Wait for the tide", "close")); return true; }
            if (kind == "manifest") { Flag("manifest_found"); Dialog("被涂改的乘客名册", "The altered manifest", "每次堡垒加压，港口就少一页乘客。名册写着米拉弟弟的名字，但最后一页背面有他的笔迹：我带孩子们去了北岸。\n\n这不是逃走的人写的告别，而是回不来的人留下的路线。把它带给米拉。", "Every shield surge cost a page of passengers. Mira's brother is listed, but his handwriting survives on the back: I took the children to the north bank.\n\nThis was a route, not a farewell. Bring it to Mira.", C("保存名册", "Keep the manifest", "close")); return true; }
            if (kind.StartsWith("mirror"))
            {
                int id = int.Parse(kind.Substring(6)); save.mirrors[id] = (save.mirrors[id] + 1) % 4; CheckMirrors(); revision++;
                Notify("镜 " + (id + 1) + " → " + new[] { "北", "东", "南", "西" }[save.mirrors[id]], "Mirror " + (id + 1) + " → " + new[] { "NORTH", "EAST", "SOUTH", "WEST" }[save.mirrors[id]]); return true;
            }
            if (kind == "receiver") { Dialog(o.name, o.en, "让北侧入射光依次连接三面镜子，再送进东侧蓄光池。镜子每按一次 E 顺时针转向。\n\n伊沃写着：第一镜向东，第二镜向南，第三镜向东。真正难的，是决定光要送给谁。", "Route the northern beam through all three mirrors into the eastern reservoir. E rotates a mirror clockwise.\n\nIvo's note: east, south, east. The harder question is who receives the light.", C("继续调试", "Keep adjusting", "close")); return true; }
            if (kind == "tinker") { TinkerConversation(o); return true; }
            if (kind == "shade") { Notify("绿洲树荫能迅速降温。凉茶可抗暑与抗寒。", "Oasis shade cools you quickly. Tea protects against heat and cold."); return true; }
            if (kind == "skiff") { Dialog(o.name, o.en, "沿金色标记依次经过五个风旗，30 秒内返回终点。沙舟竞速时移动和耐力回复更快，热度不再增加。16 秒内完成可获得风行徽章，第一次完成有额外碎片。", "Follow five golden wind flags within 30 seconds. Racing boosts speed and energy recovery, and prevents heat gain. Finish under 16 seconds for a wind badge; the first finish grants extra shards.", C("出发！", "Launch!", "race"), C("先看看路线", "Study the route", "close")); return true; }
            if (kind.StartsWith("bell"))
            {
                int id = int.Parse(kind.Substring(4)); save.bells[id] = (save.bells[id] + 1) % 4; CheckBells(); revision++;
                Notify("钟 " + (id + 1) + "：第 " + (save.bells[id] + 1) + " 音", "Chime " + (id + 1) + ": tone " + (save.bells[id] + 1)); return true;
            }
            if (kind == "brazier") { Dialog(o.name, o.en, Has("warm_" + o.id) ? "火盆已经点燃。站在附近能迅速消除寒意。" : "木材两份能点燃火盆。寒意超过 70 会让你变慢，超过 90 会持续受伤。", Has("warm_" + o.id) ? "The brazier is lit. Stand near it to warm up." : "Two timber light the brazier. Cold above 70 slows you; above 90 hurts.", C(Has("warm_" + o.id) ? "暖一会儿" : "点火 · 木材2", Has("warm_" + o.id) ? "Warm up" : "Light · 2 timber", Has("warm_" + o.id) ? "close" : "warm:" + o.id), C("继续赶路", "Keep going", "close")); return true; }
            if (kind == "keeper") { KeeperConversation(o); return true; }
            if (kind == "campfire") { Dialog(o.name, o.en, "星薄荷凉茶用药草2、星晶1制作。远行驿站建成后，可以在野外营地免费休整；完整升级的驿站还会赠送一杯凉茶。", "Star-mint tea needs 2 herbs and 1 crystal. A town waystation enables free rest at field camps; a full upgrade adds one tea per day.", C("营地休整", "Rest at camp", "fieldrest"), C("制作凉茶", "Brew tea", "tea"), C("喝凉茶", "Drink tea", "drinktea"), C("查看同伴", "Manage companions", "companions"), C("离开", "Leave", "close")); return true; }
            if (kind == "expAltar") { ExpansionAltar(o); return true; }
            if (kind == "council") { CouncilConversation(); return true; }
            if (kind.StartsWith("history")) { ReadHistory(int.Parse(kind.Substring(7))); return true; }
            if (kind == "abyssEntry") { OpenAbyssEntrance(); return true; }
            if (kind == "abyssExit") { AbyssExitConversation(); return true; }
            if (kind == "abyssGate") { AbyssGateConversation(); return true; }
            return false;
        }
        private void CaptainConversation(WorldObject o)
        {
            if (Has("harbor_report")) { PersonalConversation(0, o); return; }
            if (Has("boss_5")) { Dialog(o.name, o.en, "潮壳海兽不是在袭击港口。它在吞咽堡垒排下来的星晶废水。我的弟弟曾经提出停灯检修，名字却从船册上被抹掉了。\n\n我会带着这份水位记录，和你一起去沙海找更多证据。", "The tide beast was swallowing the citadel's crystal waste. My brother asked to stop the lights for repairs. His name was erased from the records.\n\nI will bring this tide log and travel with you to find more evidence.", C("提交港口证词 · 米拉加入", "File the tide log · recruit Mira", "harbor_report"), C("继续探索", "Keep exploring", "close")); return; }
            if (save.harborPledge == 0)
            {
                Dialog(o.name, o.en, "暴潮来了。守灯会说先修三座信号塔，免得远海的人迷路；摆渡盟说先救三名失联乘客，免得眼前的人被淹没。双方都没有错，可手里的时间和材料不够。\n\n你来决定优先顺序。两条路都能打开灯塔航道；修好的塔和救下来的人，都会留在记录里。", "A surge is coming. Lamplighters want three relays repaired for ships offshore. Ferrymen want three stranded passengers rescued now. Neither side is wrong.\n\nChoose a priority. Either route opens the lighthouse; every repair and rescue remains in the record.", C("守灯优先 · 修复三塔", "Lights first · repair 3 relays", "pledge1"), C("救人优先 · 救援三人", "People first · rescue 3 passengers", "pledge2"), C("协作路线 · 救两人、修一塔", "Coordinate · 2 rescues, 1 relay", "pledge3"), C("再考虑一下", "Think about it", "close")); return;
            }
            Dialog(o.name, o.en, "当前优先方案已经写进船册。救援 " + RescueCount + "/3，修复 " + RepairCount + "/3。\n\n进入灯塔后击败潮壳海兽，再回来提交水位记录。东北的航道会在完成方案后开放。", "Your chosen plan is in the log. Rescued " + RescueCount + "/3; repaired " + RepairCount + "/3.\n\nDefeat the Tide Maw at the northeast lighthouse, then return with its tide record.", C("继续执行方案", "Carry out the plan", "close"));
        }
        private void TinkerConversation(WorldObject o)
        {
            if (Has("desert_report")) { PersonalConversation(1, o); return; }
            if (!Has("boss_6")) { Dialog(o.name, o.en, "这台蓄光机替主人记得每一个晴天。主人死了九年，它仍把水留给一座空城。\n\n镜面依次朝东、南、东。接通光路后，东北的玻璃守卫会醒来。你得让它接受一条新的指令。", "The reservoir remembers every sunny day for its master. He died nine years ago. It still saves water for an empty city.\n\nSet the mirrors east, south, east. The Glass Custodian will wake. Help it accept a new instruction.", C("调试光路", "Restore the light channel", "close")); return; }
            Dialog(o.name, o.en, "守卫平静了。现在我们可以改写蓄光机：供给绿洲，让迁徙的人活下来；或供给灯网，让风暴中的航线不断电。\n\n我不相信一台机器的主人会永远拥有后来人的水。无论选哪条，我都会带上原始调度日志，加入你的调查。", "The custodian is quiet. We can redirect the reservoir to oasis water or storm-proof navigation lights.\n\nA dead master cannot own the water of everyone born later. Either way, I will bring the original allocation log and join your investigation.", C("供给绿洲 · 山野声望", "Water the oasis · Assembly trust", "desert1"), C("供给灯网 · 守灯声望", "Power the lights · Lamplighter trust", "desert2"), C("稍后决定", "Decide later", "close"));
        }
        private void KeeperConversation(WorldObject o)
        {
            if (Has("mountain_report")) { PersonalConversation(2, o); return; }
            if (!Has("boss_7")) { Dialog(o.name, o.en, "我叫森，负责替没能下山的人敲钟。北钟第三音，东钟第一音，南钟第二音。按 E 调整，一共四种音高。\n\n钟阵能镇住寒潮，也把冰下的求救声压住了。去山门找到霜钟执事。他说留下这段记录，会让所有人失去信心。", "I am Sen. I ring for those who never came down. North tone III, east I, south II. E cycles four tones.\n\nThe array stills the cold, and the voices beneath it. Find the Frost Prelate. He says this record would cost everyone their faith.", C("我会找到记录", "I will find the record", "close")); return; }
            Dialog(o.name, o.en, "记录是真的。统领知道水位会冲垮北岸，也知道钟阵会封住撤离口。可执事认为公开它只会让边境分裂。\n\n你要把原稿交给议事厅私下纠正，还是把每一个名字刻上山路，公开事实？我会陪你走完这一段。", "The record is real. The warden knew the surge would crush the north bank, and the chimes would seal the evacuation route.\n\nFile the original privately for reform, or carve every name on the road and expose it? I will walk the rest of the way with you.", C("保存原稿，推动纠正", "Preserve the record · seek reform", "mountain1"), C("公开名字，建立纪念碑", "Publish the names · build a memorial", "mountain2"), C("稍后决定", "Decide later", "close"));
        }
        private void PersonalConversation(int id, WorldObject o)
        {
            string[] zh = { "米拉：我总把活着带回一船人当成胜利，可有时我也得承认，没有回来的那个人不会再上船。弟弟留下的名册也许还有未写完的一页。", "伊沃：他们叫我机器的修理工。我给每台机器取名字，是因为人们给它们的命令从来只有继续工作。给我四个零件和四枚星晶，我想做一次属于自己的修理。", "森：一开始我以为，每敲一次钟，就少一个人死在雪里。后来才知道，钟也能让人听不见。带四株药草来，再让我看看密林的侦察记录。" };
            string[] en = { "Mira: A boat of survivors is a victory. But the one who never returned will not climb aboard again. My brother's manifest may hold an unfinished page.", "Ivo: They call me a repairman. I name machines because their only command is keep working. Four parts and four crystals would let me repair something for myself.", "Sen: I thought each bell saved someone from snow. Then I learned a bell could silence someone. Bring four herbs, and the forest's scouting log." };
            List<Choice> choices = new List<Choice>();
            if (!Has("personal_" + id)) choices.Add(C(new[] { "交回弟弟的名册", "帮助修复 · 零件4 星晶4", "交付药草4与密林记录" }[id], new[] { "Return the brother's manifest", "Repair · 4 parts, 4 crystals", "Bring 4 herbs and the forest log" }[id], "personal" + id));
            choices.Add(C("送一份炖汤 · 每日一次", "Share a stew · once per day", "gift" + id));
            choices.Add(C("邀请同行", "Travel together", "join" + id)); choices.Add(C("再见", "Goodbye", "close"));
            Dialog(o.name, o.en, Has("personal_" + id) ? new[] { "米拉把名册交还给幸存的孩子。她不再等一封回信，而是开始写下一条新航线。", "伊沃给新机器起名叫明天。它没有主人，只记录每个用水人的名字。", "森拆下了钟舌，铸成一块路碑。他仍守着高原，这一次是在路口听人说话。" }[id] : zh[id], Has("personal_" + id) ? new[] { "Mira gives the manifest to the surviving children. She stops waiting for a reply and starts drawing a new route.", "Ivo names the machine Tomorrow. It has no master; it records everyone who shares its water.", "Sen casts the old clapper into a road marker. He still keeps the pass, but now he listens." }[id] : en[id], choices.ToArray());
        }
        private void ExpansionAltar(WorldObject o)
        {
            if (GateOpen && GateBarrierActive) { Notify("道路正在打开，等屏障消散后再进入祭坛。", "The road is opening; wait for the barrier to dissolve before entering the altar."); return; }
            if (!GateOpen) { Notify(ExpansionGateHint(false), ExpansionGateHint(true)); return; }
            if (Has("boss_" + save.room))
            { Dialog(o.name, o.en, "异常能源已经平静。保留这里的记录，去找当地的同伴提交调查。", "The unstable energy settles. Bring its record to your local companion.", C("去提交调查", "File the regional report", save.room == 5 ? "report_mira" : save.room == 6 ? "report_ivo" : "report_sen"), C("继续探索", "Keep exploring", "close")); return; }
            if (enemies.Exists(e => e.boss)) { Notify("挑战已经开始。注意环形与分段攻击预告。", "The challenge is underway. Watch the segmented warning rings."); return; }
            Dialog(CurrentBoss.name, CurrentBoss.en, new[] { "潮壳海兽身上长满了废弃的航标。它吞下废水，再把毒潮吐回港口。避开横向三连潮击，打断它的蓄力。", "玻璃守卫仍在执行九年前的命令。它用纵向光柱封住退路。走出金色警戒区，再击破它的护甲。", "霜钟执事举起权杖：如果他们不再相信钟声，谁来让他们走过冬天？\n\n霜印会跟随你落下。不要站在原地。受伤后它会敲响三面冰钟；向没有蓝色预警的方向撤离。" }[save.room - 5], new[] { "The Tide Maw is covered in broken buoys. It swallows waste and spits poison tides. Dodge its three lateral surges and break its charge.", "The Glass Custodian still obeys a nine-year-old order. Vertical light pillars seal the retreat. Leave their warnings, then break its armor.", "The Frost Prelate raises his staff: If they stop believing in the bells, who gets them through winter?\n\nFrost marks follow you. Keep moving. When wounded, it rings three frost bells; escape through the unmarked direction." }[save.room - 5], C("开始挑战", "Challenge", "expboss"), C("准备一下", "Prepare", "close"));
        }
        private bool ChooseExpansion(string action)
        {
            if (action == "exp_begin") { if (save.stage >= 1) { Flag("exp_intro"); TrackQuest("shore"); Enter(5); } return true; }
            if (action == "camp" || action == "companions") { mode = action; return true; }
            if (action == "journal") { mode = "journal"; return true; }
            if (action == "parley")
            {
                if (save.room != 4 || Proofs != 3 || save.frontierEnding != 3 || Has("boss_4")) return true;
                enemies.Clear(); projectiles.Clear(); hazards.Clear(); Flag("boss_4"); Flag("warden_stood_down"); save.coins += 70; AddXp(80); FinalConversation(); revision++; return true;
            }
            if (action.StartsWith("pledge"))
            {
                if (save.room != 5 || save.harborPledge > 0) return true;
                save.harborPledge = int.Parse(action.Substring(6));
                if (save.harborPledge == 1) Reputation(0, 2); else if (save.harborPledge == 2) Reputation(1, 2); else { Reputation(0, 1); Reputation(1, 1); Reputation(2, 1); }
                RecordMoment("港口方案已确定，阵营支持随之改变。", "Harbor priorities agreed; faction support changed.");
                CheckHarborGate(); revision++; return true;
            }
            if (action.StartsWith("rescue") || action.StartsWith("relay"))
            {
                bool rescue = action.StartsWith("rescue"); int id = int.Parse(action.Substring(rescue ? 6 : 5)); string flag = (rescue ? "rescue_" : "relay_") + id;
                if (save.room != 5 || id < 0 || id > 2 || Has(flag)) return true;
                if (Spend(rescue ? 1 : 0, 0, rescue ? 1 : 0, 0, rescue ? 0 : 1))
                { Flag(flag); save.coins += 12; Reputation(rescue ? 1 : 0, 1); CheckHarborGate(); sound = "reward"; }
                return true;
            }
            if (action == "holdtide") { if (save.room == 5 && Spend(0, 2, 0, 0, 0)) { save.tideHeldUntil = save.totalTime + 90; Notify("闸门固定低潮 90 秒。", "Low tide held for 90 seconds."); } return true; }
            if (action == "harbor_report")
            {
                if (!Has("boss_5") || Has("harbor_report")) return true;
                Flag("harbor_report"); Flag("companion_0"); RewardRelic(0); save.relationships[0] = Math.Max(1, save.relationships[0]); save.coins += 65; save.shards += 5; save.companion = 0; AddXp(45); revision++;
                Notify("港口证词已归档。米拉加入，获得回潮瓶；玻璃沙海开放。", "Tide log filed. Mira joins; Tide Flask found. Glass Desert opens."); return true;
            }
            if (action.StartsWith("desert") && action.Length == 7)
            {
                if (!Has("boss_6") || !Has("mirrors_lit") || Has("desert_report")) return true;
                save.desertChoice = int.Parse(action.Substring(6)); Flag("desert_report"); Flag("companion_1"); RewardRelic(2); save.relationships[1] = Math.Max(1, save.relationships[1]); Reputation(save.desertChoice == 1 ? 2 : 0, 3); Reputation(1, 1); save.coins += 70; save.shards += 6; AddXp(50); revision++;
                RecordMoment(save.desertChoice == 1 ? "公开沙海调度，山野议会更支持你。" : "保留沙海机器，守灯会更支持你。", save.desertChoice == 1 ? "Published desert allocations; Assembly trust grew." : "Retained desert machines; Lamplighter trust grew.");
                Notify("调度日志已归档。伊沃加入，风蚀罗盘入手；霜钟高原开放。", "Allocation log filed. Ivo joins; Wind Compass found. Frostchime opens."); return true;
            }
            if (action.StartsWith("mountain") && action.Length == 9)
            {
                if (!Has("boss_7") || !Has("gate_7") || Has("mountain_report")) return true;
                save.mountainChoice = int.Parse(action.Substring(8)); Flag("mountain_report"); Flag("companion_2"); RewardRelic(3); save.relationships[2] = Math.Max(1, save.relationships[2]); Reputation(save.mountainChoice == 1 ? 0 : 2, 3); Reputation(1, 1); save.coins += 75; save.shards += 6; AddXp(60); revision++;
                RecordMoment(save.mountainChoice == 1 ? "封存高原记录，守灯会更支持你。" : "公开高原名字，山野议会更支持你。", save.mountainChoice == 1 ? "Sealed mountain records; Lamplighter trust grew." : "Published mountain names; Assembly trust grew.");
                Notify("高原记录已归档。森加入，断钟片入手；回雾港召开议事会。", "Mountain record filed. Sen joins; Broken Chime found. Convene Mistport's council."); return true;
            }
            if (action.StartsWith("report_"))
            {
                string kind = action == "report_mira" ? "captain" : action == "report_ivo" ? "tinker" : "keeper";
                foreach (WorldObject o in Map.objects) if (o.kind == kind) { Talk(o); break; }
                return true;
            }
            if (action == "expboss")
            {
                if (save.room < 5 || save.room > 7 || !GateOpen || GateBarrierActive || Has("boss_" + save.room) || enemies.Exists(e => e.boss)) return true;
                enemies.Add(CreatePlacedEnemy(++enemyId, 3, new Point(29, 9), CurrentBoss.hp, true)); save.health = save.maxHealth; save.energy = 100; revision++; return true;
            }
            if (action.StartsWith("warm:")) { string id = action.Substring(5); if (save.room == 7 && !Has("warm_" + id) && Spend(2, 0, 0, 0, 0)) Flag("warm_" + id); return true; }
            if (action == "tea") { if (Spend(0, 1, 2, 0, 0)) { save.tea++; revision++; Notify("星薄荷凉茶 ×1", "Star-mint tea ×1"); } return true; }
            if (action == "drinktea") { DrinkTea(); return true; }
            if (action == "fieldrest")
            {
                if (save.projects[1] == 0) { Notify("先在余烬镇建设远行驿站。按 B 查看工程。", "Build a waystation in Emberfall first. B opens projects."); return true; }
                if (!SafeTravel) { Notify("营地附近有敌人，先清理周围。", "Clear the enemies around this camp first."); return true; }
                save.health = save.maxHealth; save.energy = 100; save.heat = save.cold = 0; save.totalTime += 20;
                if (save.projects[1] == 2 && !Has("camptea_" + Day)) { Flag("camptea_" + Day); save.tea++; }
                revision++; Notify("驿站补给已送达，整装再出发。", "Waystation supplies delivered. Ready to set out."); return true;
            }
            if (action == "race") { if (save.room == 6) { racing = true; raceTime = 0; raceCheckpoint = 0; Notify("竞速开始：沿金色风旗走！", "Race started: follow the golden flags!"); } return true; }
            if (action.StartsWith("personal")) { CompletePersonal(int.Parse(action.Substring(8))); return true; }
            if (action.StartsWith("gift"))
            {
                int id = int.Parse(action.Substring(4)); if (id < 0 || id > 2 || !Has("companion_" + id)) return true;
                string flag = "gift_" + id + "_" + Day;
                if (Has(flag)) { Notify("今天已经一起吃过饭了。", "You already shared a meal today."); return true; }
                if (save.inventory[6] < 1) { Notify("需要一份炖汤。", "You need a stew."); return true; }
                save.inventory[6]--; Flag(flag); save.relationships[id] = Math.Min(5, save.relationships[id] + 1); revision++; return true;
            }
            if (action.StartsWith("join")) { SelectCompanion(int.Parse(action.Substring(4))); return true; }
            if (action.StartsWith("covenant")) { ResolveCouncil(int.Parse(action.Substring(8))); return true; }
            if (action == "council_epilogue") { CouncilEpilogue(); return true; }
            if (action.StartsWith("abyssStart")) { StartAbyss(int.Parse(action.Substring(10))); return true; }
            if (action == "abyssLeave") { EndAbyss(false); Enter(0); return true; }
            if (action.StartsWith("boon")) { TakeBoon(int.Parse(action.Substring(4))); return true; }
            if (action == "abyssBank") { BankFloor(); EndAbyss(true); Enter(0); return true; }
            return false;
        }
        private void CompletePersonal(int id)
        {
            if (id < 0 || id > 2 || !Has("companion_" + id) || Has("personal_" + id)) return;
            if (id == 0 && !Has("manifest_found")) { Notify("先调查雾港西北的乘客名册（8,5）。", "Find the passenger manifest in northwest Mistport (8,5)."); return; }
            if (id == 1 && !Spend(0, 4, 0, 0, 4)) return;
            if (id == 2 && (!Has("lore_1") || !Spend(0, 0, 4, 0, 0))) { Notify("需要密林侦察记录与药草4。", "You need the forest scouting log and 4 herbs."); return; }
            Flag("personal_" + id); save.relationships[id] = Math.Min(5, save.relationships[id] + 2); save.coins += 35; save.shards += 3; save.skillPoints++;
            if (Has("personal_0") && Has("personal_1") && Has("personal_2")) RewardRelic(4);
            Notify("同伴故事完成。羁绊提升，获得 1 技能点与记忆碎片。", "Companion story complete. Bond improved; +1 point and memory shards."); revision++; sound = "reward";
        }
        private void FinishRace()
        {
            racing = false; int tenths = Math.Max(1, (int)(raceTime * 10));
            bool first = !Has("race_finished"), record = save.bestRace == 0 || tenths < save.bestRace;
            if (first) { Flag("race_finished"); save.coins += 25; save.shards += 3; }
            if (record) save.bestRace = tenths;
            if (raceTime <= 16 && !Has("race_badge")) { Flag("race_badge"); save.shards += 5; save.skillPoints++; }
            revision++; sound = "reward"; Notify("竞速完成！用时 " + raceTime.ToString("0.0") + " 秒。首次与风行徽章奖励只发一次。", "Race finished in " + raceTime.ToString("0.0") + "s. First-finish and badge rewards are one-time.");
        }
        public bool CouncilPactReady { get { return ProjectTiers >= 3 && CompanionCount >= 2 && save.reputation[0] >= 2 && save.reputation[1] >= 2 && save.reputation[2] >= 2; } }
        private void CouncilConversation()
        {
            if (save.frontierEnding > 0) { CouncilEpilogue(); return; }
            if (Proofs < 3) { Dialog("三方议事厅", "The three-party council", "港口的水位、沙海的调度、高原的名字：三份记录必须同时摆在桌上，否则每一方都会说代价是别人的事。\n\n已提交 " + Proofs + "/3。完成各地调查后回来。", "The tide log, allocation log and mountain record must share one table. Otherwise each faction will call the cost someone else's problem.\n\nReports filed: " + Proofs + "/3. Return after the investigations.", C("继续调查", "Continue investigating", "close")); return; }
            List<Choice> choices = new List<Choice> { C("共同水渠 · 优先供水", "Shared canals · water first", "covenant1"), C("轮值灯网 · 优先航路", "Rotating lightwatch · routes first", "covenant2") };
            if (CouncilPactReady) choices.Add(C("开放星路 · 三方共同治理", "Open starroads · shared governance", "covenant3"));
            choices.Add(C("先建设与争取支持", "Build and earn more support", "close"));
            Dialog("露恩 · 最后一封信", "Lune · The final letter", "守灯会承认了水位造假；摆渡盟承认停灯也会让远海的人死去；山野议会承认恢复土地仍需要维护航路。\n\n共同水渠增加收成；轮值灯网让你更容易脱离战斗旅行。建设总等级达到3、招募两名同伴且三方声望都达到2，才足以实行共同治理。", "The Lamplighters admit falsified levels. Ferrymen admit dark lights also kill. The Assembly admits restored land still needs routes.\n\nCanals improve harvests; Lightwatch shortens the safe-travel distance. Shared governance needs 3 project tiers, 2 companions, and trust 2 with every faction.", choices.ToArray());
        }
        private void ResolveCouncil(int ending)
        {
            if (Proofs < 3 || save.frontierEnding > 0 || ending < 1 || ending > 3 || (ending == 3 && !CouncilPactReady)) return;
            save.frontierEnding = ending; Flag("frontier_settled"); save.shards += 10; save.skillPoints += 2; save.coins += 100; revision++; CouncilEpilogue();
        }
        private void CouncilEpilogue()
        {
            string[] zh = { "", "《流向每一个人》\n\n水不再为死去的主人停留。摆渡人把第一批种子带到沙海，守灯人把旧电缆铺进水渠。田地每次额外收获两株药草。\n\n灯网仍需节约使用，可再也没有一页乘客名册被丢进炉火。", "《有人接班的灯》\n\n灯塔不再为一座堡垒熬干整片土地。三个地区轮流供能，摆渡盟监督每一次切换。距离敌人四格就能快速旅行。\n\n森在每一轮换班时敲钟。这一次，钟声提醒人们：有人会接过你手里的灯。", "《没有主人的明天》\n\n共同的工具、路线和记录，让三方第一次有资格共同决定代价。米拉负责航船，伊沃维护水渠，森把所有名字刻在公开的路碑。\n\n同伴指令冷却减为12秒；委托与深渊增加碎片收益。没有谁宣布边境已经完美，但每个人都能提出下一项修理。" };
            string[] en = { "", "WATER FOR EVERYONE\n\nWater no longer waits for a dead master. Ferrymen carry seeds to the desert; old light cables become canals. Each harvest yields two extra herbs.\n\nLights remain limited, but no passenger page goes into a furnace again.", "A LIGHT WITH A NEXT KEEPER\n\nThree regions rotate power, and the Ferry Union audits every switch. Safe travel now needs only four tiles from enemies.\n\nSen rings at shift changes. The sound means someone will take the light from your tired hands.", "A TOMORROW WITHOUT A MASTER\n\nShared tools, routes and records make shared decisions possible. Mira runs ferries, Ivo keeps canals, Sen carves every name in public.\n\nCompanion commands cool down in 12 seconds; contracts and abyss runs yield extra shards. Nobody calls the frontier perfect. Everyone can propose its next repair." };
            int id = (int)Clamp(save.frontierEnding, 1, 3);
            Dialog("远岸来信 · 终章", "Letters from Afar · Epilogue", zh[id] + "\n\n地下的星根开始回应你。地脉深渊现已开放。", en[id] + "\n\nThe roots beneath the land answer. The Root Abyss is now open.", C("继续探索", "Keep exploring", "close"), C("回到余烬镇", "Return to Emberfall", "goto0")); sound = "reward";
        }
        private void ReadHistory(int id)
        {
            if (id < 0 || id >= data.lore.Length) return;
            bool first = !Has("history_" + id); Flag("history_" + id);
            if (first) { save.shards += 2; save.coins += 10; revision++; }
            LoreData book = data.lore[id]; Dialog(book.title, book.en, book.zh, book.text, C("记入手帐", "Record in journal", "close"));
        }
        public void OpenCodex(int id)
        {
            if (id < 0 || id >= data.lore.Length || !(Has("history_" + id) || (id < 3 && Has("lore_" + (id + 1))))) return;
            LoreData book = data.lore[id]; Dialog(book.title, book.en, book.zh, book.text, C("返回手帐", "Back to journal", "journal"));
        }
        private string ExpansionGateHint(bool en)
        {
            if (save.room == 5) return en ? "Choose a priority with Mira (15,16), then repair relays or rescue passengers." : "先找米拉（15,16）决定优先方案，再修塔或救人。";
            if (save.room == 6) return en ? "Turn the mirrors east, south, east; connect the eastern receiver." : "第一镜向东，第二镜向南，第三镜向东，接通蓄光池。";
            return en ? "Tune the north, east, south bells to III, I, II." : "把北钟调至第三音、东钟第一音、南钟第二音。";
        }
        private string ExpansionObjective()
        {
            if (save.room == 9) return L("休息、备好热汤，或从南门返回余烬镇。", "Rest, prepare stew, or return to Emberfall through the south door.");
            if (InAbyss) return enemies.Count > 0 ? L("清理本层敌人 · 小心地脉落印，东门可结算", "Clear this floor · avoid rift marks; bank at the east gate") : L("到东侧地脉门，选择祝福或安全撤离。", "Choose a blessing or extract at the east gate.");
            if (save.room >= 5 && save.room <= 7)
            {
                if (!GateOpen) return ExpansionGateHint(save.english);
                if (!Has("boss_" + save.room)) return L("前往东北祭坛，挑战地区守护者。", "Challenge the regional guardian at the northeast altar.");
                return L("找当地同伴提交调查，或继续个人故事。", "File the report with your local companion, or pursue their personal story.");
            }
            if (!save.frontierFocus || !Has("exp_intro")) return null;
            if (Proofs == 0) return L("远岸来信：前往雾港，找船长米拉。", "Letters from Afar: meet Captain Mira in Mistport.");
            if (Proofs == 1) return L("远岸来信：前往玻璃沙海，调查蓄光机。", "Letters from Afar: investigate the Glass Desert reservoir.");
            if (Proofs == 2) return L("远岸来信：前往霜钟高原，寻找被掩盖的记录。", "Letters from Afar: uncover Frostchime's hidden record.");
            return save.frontierEnding == 0 ? L("带三份证词回雾港议事厅，决定新的边境协定。", "Bring all three reports to Mistport's council and choose a covenant.") : L("协定已签：建设城镇、完成同伴故事或探索地脉深渊。", "Covenant signed: build, finish companion stories, or explore the Root Abyss.");
        }
        private void OpenAbyssEntrance()
        {
            if (!CanEnter(8)) { Notify(RouteHint(8, false), RouteHint(8, true)); return; }
            Dialog("地脉裂口", "The root fissure", "每次远征有六层，房间随种子改变。清层后选择一种祝福，并结算本层战利品；继续深入会保留祝福。\n\n倒下或提前离开会失去本层未结算收益，已结算物品保留。第三层之后有落印，最后一层有地脉收割者，根刺会从两侧合拢，沿垂直方向闪避。风险越高，生命与碎片收益越高。", "Six seeded floors. Clear a floor, choose a boon and bank its loot; boons stay while descending.\n\nFalling or leaving loses only the unbanked floor. Rift marks begin on floor three; the Root Reaper waits on six; its roots close from both sides, so dodge perpendicular to their line. Higher risk means tougher enemies and more shards.", C("巡行 · 风险0", "Survey · risk 0", "abyssStart0"), C("深入 · 风险1", "Delve · risk 1", "abyssStart1"), C("裂隙 · 风险2", "Rift · risk 2", "abyssStart2"), C("稍后再来", "Later", "close"));
        }
        public void StartAbyss(int risk)
        {
            if (Dying || !CanEnter(8) || InAbyss || risk < 0 || risk > 2) return;
            save.abyssFloor = 1; save.abyssRisk = risk; save.abyssSeed = (int)(Random() * 1000000) + 137;
            save.abyssEnemies = null; save.abyssSnapshotFloor = 0;
            save.runCoins = save.runShards = save.runXp = 0; Array.Clear(save.boons, 0, 3); save.flags.RemoveAll(f => f.StartsWith("abyss_clear_") || f == "boss_8");
            save.room = 8; GenerateAbyss(); save.x = 5; save.y = 14; ResetTransient(); SpawnRoom(); mode = "world"; revision++;
        }
        private void GenerateAbyss()
        {
            var random = new System.Random(save.abyssSeed + save.abyssFloor * 919);
            char[][] rows = new char[24][];
            for (int y = 0; y < 24; y++) { rows[y] = new string('#', 36).ToCharArray(); }
            int[,] rooms = { { 3, 4, 8, 16 }, { 13, 3, 9, 18 }, { 25, 4, 8, 16 } };
            for (int i = 0; i < 3; i++) for (int y = rooms[i, 1]; y < rooms[i, 1] + rooms[i, 3]; y++) for (int x = rooms[i, 0]; x < rooms[i, 0] + rooms[i, 2]; x++) rows[y][x] = '.';
            for (int y = 13; y <= 15; y++) for (int x = 2; x < 34; x++) rows[y][x] = '=';
            for (int i = 0; i < 9; i++) { int x = random.Next(13, 22), y = random.Next(4, 20); if (y < 12 || y > 16) rows[y][x] = '#'; }
            string[] strings = new string[24]; for (int y = 0; y < 24; y++) strings[y] = new string(rows[y]);
            abyssMap = new MapData { id = 8, name = "地脉深渊 · 第" + save.abyssFloor + "层", en = "ROOT ABYSS · FLOOR " + save.abyssFloor, color = save.abyssFloor % 2 == 0 ? "#4f576c" : "#425b5c", subtitle = "越深入，越要决定带什么回来。", suben = "Every descent asks what you will carry home.", spawnX = 5, spawnY = 14, rows = strings,
                objects = new[] { new WorldObject { id = "abyssExit", kind = "abyssExit", name = "放弃本层收益，返回", en = "Leave unbanked loot", x = 3, y = 14 }, new WorldObject { id = "abyssGate", kind = "abyssGate", name = "地脉门 · 祝福与结算", en = "Root gate · boons and banking", x = 31, y = 14 } },
                decor = new[] { new Decoration { kind = "crystal", x = 8, y = 6 }, new Decoration { kind = "obelisk", x = 18, y = 6 }, new Decoration { kind = "root", x = 29, y = 18 } } };
        }
        private void AbyssGateConversation()
        {
            if (!InAbyss) return;
            if (enemies.Count > 0) { Notify("先清理本层敌人，地脉门才会回应。", "Clear this floor before the root gate answers."); return; }
            Flag("abyss_clear_" + save.abyssFloor);
            List<Choice> choices = new List<Choice>();
            if (save.abyssFloor < 6)
            { choices.Add(C("利刃祝福 · 攻击+15%并深入", "Edge boon · +15% damage and descend", "boon0")); choices.Add(C("泉涌祝福 · 耐力回复+4并深入", "Spring boon · +4 energy regen and descend", "boon1")); choices.Add(C("磐石祝福 · 伤害-12%并深入", "Stone boon · -12% damage taken and descend", "boon2")); }
            choices.Add(C(save.abyssFloor == 6 ? "完成远征，结算返回" : "结算本层，安全返回", save.abyssFloor == 6 ? "Complete the expedition and bank" : "Bank this floor and extract", "abyssBank"));
            Dialog("会记住选择的地脉", "Roots that remember choices", "本层未结算：硬币 " + save.runCoins + "，碎片 " + save.runShards + "，经验 " + save.runXp + "。\n\n祝福只在本次远征有效，可叠加至四层。深入前自动结算当前楼层，回复一半最大生命。第三层与第六层会解锁永久奖励。", "Unbanked: " + save.runCoins + " coins, " + save.runShards + " shards, " + save.runXp + " XP.\n\nBoons last this run and stack four times. Descending banks the floor and heals half your maximum health. Floors three and six unlock permanent rewards.", choices.ToArray());
        }
        private void BankFloor()
        {
            if (Dying || !InAbyss || enemies.Count > 0 || !Has("abyss_clear_" + save.abyssFloor)) return;
            save.coins += save.runCoins; save.shards += save.runShards + (save.frontierEnding == 3 ? 2 : 0); AddXp(save.runXp);
            save.runCoins = save.runShards = save.runXp = 0; save.bestFloor = Math.Max(save.bestFloor, save.abyssFloor);
            BountyEvent(5, 1);
            if (save.abyssFloor >= 3) RewardRelic(5);
            if (save.abyssFloor == 6 && !Has("abyss_complete")) { Flag("abyss_complete"); save.skillPoints += 2; save.shards += 12; }
            revision++;
        }
        public void TakeBoon(int id)
        {
            if (Dying || id < 0 || id > 2 || !InAbyss || save.abyssFloor >= 6 || enemies.Count > 0 || !Has("abyss_clear_" + save.abyssFloor)) return;
            BankFloor(); save.boons[id] = Math.Min(4, save.boons[id] + 1); save.abyssFloor++;
            GenerateAbyss(); save.x = 5; save.y = 14; save.health = Math.Min(save.maxHealth, save.health + save.maxHealth * .5f); save.energy = 100;
            ResetTransient(); SpawnRoom(); mode = "world"; revision++; sound = "travel";
        }
        private void AbyssExitConversation()
        {
            if (!InAbyss) return;
            Dialog("返回的道路", "The way back", "现在返回会失去本层尚未结算的战利品。之前结算的收益不会丢失。清完本层后到东门，可以带走所有收益。", "Leaving now loses this floor's unbanked loot only. Earlier banked rewards remain. Clear the floor and use the east gate to take everything.", C("返回，放弃本层收益", "Return and leave this floor's loot", "abyssLeave"), C("继续远征", "Keep delving", "close"));
        }
        private void EndAbyss(bool banked)
        { save.abyssFloor = 0; save.runCoins = save.runShards = save.runXp = 0; save.abyssEnemies = null; save.abyssSnapshotFloor = 0; Array.Clear(save.boons, 0, 3); abyssMap = null; revision++; }
    }
}
