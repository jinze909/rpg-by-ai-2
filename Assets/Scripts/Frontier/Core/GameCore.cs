using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        // A rescue warning is an individual enemy's pending attack, not
        // shared scenery. Identity-based weak ownership lets a real stun
        // retract ONLY that enemy's unspent hazard without removing
        // overlapping alerts from other raiders or persistent world hazards.
        private readonly ConditionalWeakTable<Enemy,Effect>
            rescueWindupEffects=new ConditionalWeakTable<Enemy,Effect>();
        // Shots are born at a contact/release pose, sometimes late inside
        // Tick(). Only shots already flying at this tick's start receive a
        // FULL tick of travel. Newly emitted missiles become visible at the
        // muzzle immediately, then advance next tick rather than traveling
        // through time before their attack was released.
        private readonly HashSet<Projectile> flightReadyAtFrameStart =
            new HashSet<Projectile>();
        public Point aim = new Point(1, 0);
        public int revision, runeProgress;
        public float attackCooldown, dashTime, dashCooldown, invulnerable, skillCooldown, guardTime;
        public float fishTime, fishCursor, toastTime;
        public int fishHits, fishMisses;
        private string currentMode = "title";
        public string mode
        {
            get { return currentMode; }
            set { currentMode = value; if (value != "world") { ClearBufferedSkill(); ClearBufferedAttack(); ClearBufferedDodge(); } }
        }
        public string toast = "";
        public Conversation conversation;
        // Transient identity of the interacted NPC, not the nearest NPC on each render.
        public WorldObject conversationSource;
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
        public float Speed { get { return 4.5f + (Has("roadcharter_public") ? .25f : 0) + (save.profession == 1 ? .45f : 0) + save.talents[2] * .12f + (Relic(2) ? .4f : 0) + (racing ? .65f : 0) + (Has("survey_complete") ? .25f : 0) - (FarCourierActive ? .7f : 0) - (save.cold > 70 ? .55f : 0) - (TideSalvagePhase==4 ? .55f : 0); } }
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
            save.stage = (int)Clamp(save.stage, 0, 3);
            // An out-of-range region is not an authorized town location.
            // Preserve the fact that this slot needs safe respawning even
            // when its old coordinates happen to be walkable in Emberfall.
            int requestedRoom = save.room;
            bool invalidSavedRoom = save.room < 0 || save.room >= data.maps.Length;
            if (invalidSavedRoom) save.room = 0;
            // Expansion sanitization checks "technique_<profession>" before
            // any further class handling. Repair the profession first so a
            // valid unlocked second technique is not erased on load.
            save.profession = (int)Clamp(save.profession, 0, 2);
            SanitizeExpansion();
            SanitizeGuidance();
            SanitizePolish();
            if (Has("recovery_pending"))
            {
                if (save.abyssFloor > 0) EndAbyss(false);
                save.flags.Remove("recovery_pending"); save.room = 0; save.x = data.maps[0].spawnX; save.y = data.maps[0].spawnY;
                save.health = 9999; save.energy = 100; save.snapshotRoom = -1; save.worldEnemies = null;
            }
            if (invalidSavedRoom || save.room != requestedRoom || !CanEnter(save.room))
            {
                // A zero-floor or no-longer-authorized Abyss save cannot
                // secretly keep a live expedition after its region is rejected.
                // Preserve permanent/banked achievements, not run loot/boons.
                if (save.abyssFloor > 0) EndAbyss(false);
                save.room = 0;
                save.x = data.maps[0].spawnX; save.y = data.maps[0].spawnY;
                save.snapshotRoom = -1; save.worldEnemies = null;
            }
            // Only construct the procedural Abyss for an actually authorized
            // active expedition. Never generate a rejected run first.
            if (save.room == 8) GenerateAbyss();
            save.difficulty = (int)Clamp(save.difficulty, 0, 2);
            save.upgrades = (int)Clamp(save.upgrades, 0, 5); save.level = (int)Clamp(save.level, 1, 30);
            // The level-30 cap has no experience sink; saved XP beyond the
            // next level is invalid legacy state, not extra progression.
            save.xp = save.level == 30 ? 0 : (int)Clamp(save.xp, 0, save.level * 40 - 1);
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
            // SaveData belongs to the loaded slot. Keep only the optional
            // validated in-progress escort while clearing runtime combat.
            RescueCheckpoint pendingRescue = save.rescueCheckpoint;
            ResetTransient(); SpawnRoom(); RestoreWorld();
            RestoreReliefCheckpoint(pendingRescue);
            revision++;
        }
        private void ResetTransient()
        {
            projectiles.Clear(); effects.Clear();
            flightReadyAtFrameStart.Clear();
            // A new journey/slot must never inherit pointer targeting, facing,
            // fishing results or HUD notifications from the previous hero.
            // None of these transient inputs belong to persisted SaveData.
            aim = dashDirection = new Point(1, 0);
            aimTarget = null;
            fishTime = fishCursor = 0;
            fishHits = fishMisses = 0;
            toast = ""; toastTime = 0;
            // Runtime audio consumes a pending queue, not just the current
            // sound name. Neither old combat cues nor dialogue clicks may
            // play after loading a different character or starting fresh.
            sounds.Clear(); lastSound = "";
            attackCooldown = dashCooldown = dashTime = skillCooldown = guardTime = 0;
            invulnerable = 1; conversation = null; conversationSource = null; runeProgress = 0;
            ResetExpansionTransient(); ResetCombatPresentation();
            RootsongPulseCooldown=0;
            ResetMineConvoy(); ResetFrostSignal(); ResetSwordGuard(); ResetSwordLesson();
            ResetContinuity(); ResetRoadwardenTrial(); ResetWayfarerEncounter(); ResetAtlasEcho(); ResetReliefMission(); ResetRootHarmonyTrial(); ResetCommonsRelayTrial(); ResetSkywatch(); ResetGlasswind(); ResetTideSalvage(); ResetTemper(); ResetTriadDefense(); ResetWitnessDefense(); ResetBlackout(); ResetAfterlightCharge(); ResetAfterlightWatch(); ResetStormTransient(); ResetOuterPatrol(); ResetFarCourier(); ResetCivicResponse(); ResetLastDirective(); ResetCivicConduit(); ResetFloodRelief();
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
                "你带着巡路队没能送出的信来到余烬镇。莉雅认得信上的名字：巡路员曾为公会测绘道路，却在星陨的那一夜失踪了。\n\n七天前，夜空与地下的星根一起裂开。荆棘封路，潮水吞桥，矿机仍在不停抽取星晶。堡垒要求回收星核来维持屏障；莉雅却开始担心，城墙外的人正在替城内付出代价。\n\n先救回荆棘密林的失踪侦察员，弄清黑荆巨兽守护什么，再把第一枚星核带回镇里。杏、老潮和诺尔也在尝试让每一个人有路可回。\n\nWASD 移动，鼠标左键或 J 攻击，空格闪避，Q 职业技能，E 交谈，Tab 打开任务手帐。公会北侧的《边境编年册》会随着你的调查展开。",
                "You arrive in Emberfall with an undelivered roadwarden letter. Lyra recognizes the seal: the patrol mapped roads for the guild before disappearing on the night of Starfall.\n\nSeven days ago the sky and the starroot underground fractured together. Thorns sealed the roads, tides swallowed bridges, and machines kept draining starcrystals. The citadel demands the starcores to sustain its shield, yet people outside its walls pay for that safety.\n\nFirst rescue the missing scout in Thornwood, find out what the Briar Beast is defending and bring the first core to town. An, Old Tide and Noll are working on ways for everyone to come home.\n\nWASD moves, left click or J attacks, Space dodges, Q uses your class skill, E interacts, and Tab opens the journal. The Frontier Chronicle north of the guild will unfold as you investigate.", C("接下第一份委托", "Accept the first commission", "close"));
        }
        public void Notify(string zh, string en) { toast = L(zh, en); toastTime = 4; }
        private void Dialog(string speaker, string en, string text, string textEn, params Choice[] choices)
        { conversation = new Conversation(speaker, en, text, textEn, choices); mode = "dialog"; }
        public void Close() { conversation = null; conversationSource = null; mode = "world"; }
        public bool Blocked(float x, float y)
        {
            int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
            if (ty < 2 || tx < 2 || ty >= Map.rows.Length - 2 || tx >= Map.rows[0].Length - 2) return true;
            char tile = Map.rows[ty][tx];
            if (tile == '#' || tile == '~' || tile == 'f') return true;
            if (tile == '?' && !Has("far_bridge_"+save.room)) return true;
            if ((tile == 'v' || tile == 'w' || tile == 'b') && !Has("seal_" + save.room)) return true;
            if (save.room == 5 && tile == '%' && !LowTide) return true;
            foreach (Decoration d in StaticHouseColliders())
                if (Math.Abs(x - d.x) < 1.8f && y > d.y - 1.6f && y < d.y - .2f) return true;
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
            PreparePathSearch(width * height);
            int[] parents = pathParents; int head = 0, tail = 0; pathQueue[tail++] = start; VisitPath(start, start);
            while (head < tail && !PathVisited(target))
            {
                int cell = pathQueue[head++], x = cell % width, y = cell / width;
                for (int i = 0; i < 4; i++)
                {
                    int xx = x + pursuitSteps[i, 0], yy = y + pursuitSteps[i, 1];
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height) continue;
                    int next = yy * width + xx;
                    if (PathVisited(next) || !Fits(new Point(xx + .5f, yy + .5f))) continue;
                    VisitPath(next, cell); pathQueue[tail++] = next;
                }
            }
            if (!PathVisited(target))
            {
                // The player, an escort or a live objective can be sealed behind
                // terrain, tide or a story gate. Chasing the straight vector
                // forever makes enemies walk into the wall and jitter in place.
                // Stop at the nearest REACHABLE frontier instead. Do not cache:
                // a repaired bridge / newly opened gate must take effect now.
                int closest = start;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < tail; i++)
                {
                    int cell = pathQueue[i], x = cell % width, y = cell / width;
                    float dx = x + .5f - to.x, dy = y + .5f - to.y;
                    float distance = dx * dx + dy * dy;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        closest = cell;
                    }
                }
                // Already at the frontier: stand guard rather than pushing
                // continuously into an impassable tile.
                if (closest == start) return new Point();
                target = closest;
            }
            int waypoint = target; while (parents[waypoint] != start) waypoint = parents[waypoint];
            return (new Point(waypoint % width + .5f, waypoint / width + .5f) - from).Normalized;
        }
        // A carried parcel or live civic response is an actual local objective.
        // Menus, scripted portals and the home shortcut must not silently
        // discard its combat actors or timer through ResetTransient().
        private bool CivilianFieldMissionBlocksTravel()
        {
            if(SwordLessonActive)
            {
                Notify("诺尔的实弹练习正在进行，完成或在铁匠铺安全中止后才能旅行。",
                    "Noll's live drill is active. Finish or abort at his forge before traveling.");
                return true;
            }
            if(FrostSignalActive)
            {
                Notify("高原火种尚在运送。必须送往公共暖站，或回西部站主动归还。",
                    "A highland ember is in transit. Deliver it or return it to the western signal.");
                return true;
            }
            if(MineConvoyActive)
            {
                Notify("矿工撤离仍在路上！完成护送或返回西北发车岗主动取消。",
                    "Minecart evacuation active. Finish the escort or cancel at the western dispatch.");
                return true;
            }
            if (FarCourierActive)
            {
                Notify("救援包仍在运送中。请送到避险棚，或回桥头主动放回物资。",
                    "Parcel in transit: deliver it at the refuge or explicitly return it at the bridge.");
                return true;
            }
            if (CivicResponseActive)
            {
                Notify("社区服务行动仍在进行。请完成现场工作，或在原避险棚明确取消。",
                    "Civilian service is active: finish the local task or explicitly cancel at its refuge.");
                return true;
            }
            return false;
        }
        public void Enter(int room)
        {
            if (Dying) return;
            if (CivilianFieldMissionBlocksTravel()) return;
            // A live mission cannot disappear through a menu, portal or warp.
            if(CivicConduitActive)
            {
                Notify("导体仍在堡垒南路通电！完成阀门或回西端控制台明确中止。",
                    "Conductor active: finish the valves or explicitly abort at the western desk before travel.");
                return;
            }
            if(LastDirectiveWave>0)
            {
                Notify("堡垒保密终端正在实战或结算，请完成行动或亲自回终端撤销。",
                    "Citadel security remains active. Finish or withdraw at its terminal before travel.");
                return;
            }
            // No world travel while carrying live afterlight charge.
            // The original region, site plan and remote receiver must matter.
            if(stormTuning)
            {
                Notify("公约星灯正在进行实地调谐，必须完成或返回议事台放弃，不能切换地图。",
                    "Live civic-grid tuning must finish or be canceled at the council before travel.");
                return;
            }
            if(afterlightChargeRegion!=0)
            {
                Notify("余光输送仍在进行。先走到本地区公共接收台，或返回原锚点主动取消。",
                    "Afterlight transfer is active. Reach the real receiver or return to the source to cancel before traveling.");
                return;
            }
            if(AfterlightWatchActive)
            {
                Notify("公共储备防卫进行中！击退袭击者，或返回原接收台主动终止。",
                    "Civic reserve defense active. Defeat the attackers or withdraw at the receiver.");
                return;
            }
            // Portal objects, map routes, scripted choices and travel menus
            // all reach this method. Preserve an active rescue instead of
            // silently erasing its civilian on ResetTransient().
            if (blackoutActive)
            {
                Notify("屏障长夜撤离仍在进行。先把居民送到避难所或明确中止，不能直接换地区。",
                    "Blackout evacuation is active. Bring your civilians to shelter or abort the mission before traveling.");
                return;
            }
            if (witnessDefenseRoom!=0)
            {
                Notify("本章居民证词正在战斗保全中，请击败守卫或在证据台撤出。",
                    "A live testimony-defense is active. Defeat its guards or withdraw at the field seal before traveling.");
                return;
            }
            if (TriadDefenseActive)
            {
                Notify("第四章审计守卫仍在行动。先返回中央控制台放弃或击败它。",
                    "Chapter Four security is still active. Defeat it or withdraw at the central console before leaving.");
                return;
            }
            if (TemperActive)
            {
                Notify("工房仍在过热运行。先返回锻炉结束或安全停机。",
                    "The foundry is still active. Finish or safely shut it down at the furnace before traveling.");
                return;
            }
            if (TideSalvageActive)
            {
                Notify("打捞行动还在进行，先回码头交付或放弃货物。",
                    "Salvage is underway. Deliver or abandon at the dock before traveling.");
                return;
            }
            if (GlasswindSite != 0)
            {
                Notify("风脉中继正在争夺中。请封存或返回中继台撤出后再旅行。",
                    "Glasswind relay is contested. Seal or withdraw at its console before traveling.");
                return;
            }
            if (commonsRelayRoom != 0)
            {
                Notify("公共信标正在重连。先完成三段线路或返回控制台结束操作。",
                    "Commons Relay is live. Complete its three circuits or withdraw at a console before traveling.");
                return;
            }
            if (reliefMissionRoom != 0)
            {
                Notify("正在救援居民。先完成撤离或到避难站明确取消，不能直接切换地区。",
                    "Rescue in progress. Finish the evacuation or cancel it at the station before traveling.");
                return;
            }
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
            if (CivilianFieldMissionBlocksTravel()) return;
            if(CivicConduitActive||LastDirectiveWave>0)
            {
                Notify("先完成现场任务，或在原始控制台明确中止，才能快速回城。",
                    "Finish or explicitly abort the live Citadel mission at its physical terminal before fast travel.");
                return;
            }
            if(stormTuning)
            {
                Notify("公共星灯调谐仍在进行，不能使用快捷回城绕过城市节点。",
                    "Public lamp tuning is active; finish or cancel before fast travel.");
                return;
            }
            if(afterlightChargeRegion!=0)
            {
                Notify("第五章余光传输中，不能直接快速回城。请到原锚点取消或完成通电。",
                    "A public afterlight charge is in transit. Finish at the receiver or cancel at the original anchor.");
                return;
            }
            if(AfterlightWatchActive)
            {
                Notify("守护公共储备的战斗尚未结束，请返回接收台撤销或实际完成防卫。",
                    "Finish civic reserve defense or withdraw at its receiver before returning to town.");
                return;
            }
            if (blackoutActive)
            { Notify("堡垒居民仍在撤离中。先回避难所完成或取消救援。",
                "Citadel evacuation is active. Complete or cancel at the shelter."); return; }
            if (reliefMissionRoom != 0)
            { Notify("正在护送居民，先返回站台或者取消行动。", "You are escorting civilians. Return to the station or cancel the rescue first."); return; }
            if (Dying) return;
            if (save.room == 0) return;
            if (InAbyss) { AbyssExitConversation(); return; }
            if (!SafeTravel) { Notify("脱离敌人后才能快速回城。西侧出口随时可用。", "Leave combat to fast travel. The west exit is always available."); return; }
            Enter(0);
        }
        private void SpawnRoom()
        {
            enemies.Clear(); enemyId = 0;
            if (SpawnExpansionRoom())
            {
                SpawnRidgeWildlife();
                return;
            }
            if (save.room == 0) return;
            float[,] locations = { { 11, 12 }, { 15, 17 }, { 21, 15 }, { 24, 6 }, { 9, 7 }, { 30, 16 }, { 23, 19 }, { 14, 9 } };
            for (int i = 0; i < 6 + (save.room > 2 ? 2 : 0); i++)
            {
                float x = locations[i, 0], y = locations[i, 1];
                enemies.Add(CreatePlacedEnemy(++enemyId, i % 3, new Point(x, y), 34 + save.room * 9));
            }
            SpawnRidgeWildlife();
        }
        public void Tick(float dt, Point movement, Point? explicitAim = null)
        {
            dt = Finite(dt) ? Clamp(dt, 0, .05f) : 0;
            if (dt <= 0) return;
            // Defensive input boundary: malformed controller/aim events must
            // never poison persistent coordinates with NaN or infinities.
            if(!Finite(movement.x)||!Finite(movement.y))movement=new Point();
            if(explicitAim.HasValue&&
                (!Finite(explicitAim.Value.x)||!Finite(explicitAim.Value.y)))
                explicitAim=null;
            toastTime -= dt;
            if (mode == "fish") { fishTime += dt; fishCursor = .5f + .5f * (float)Math.Sin(fishTime * (2.5f + fishHits * .35f)); return; }
            if (mode != "world") return;
            if (hitStop > 0)
            {
                // Freeze for the *actual* remaining impact time; a short
                // 12ms hit-stop in a 50ms frame should not steal the other
                // 38ms of walking, enemy windups, cooldowns or quest clocks.
                float frozen = Math.Min(dt, hitStop);
                hitStop -= frozen;
                dt -= frozen;
                if (dt <= .00001f) return;
            }
            flightReadyAtFrameStart.Clear();
            foreach (Projectile existingShot in projectiles)
                flightReadyAtFrameStart.Add(existingShot);
            float skillReadyDelay = SkillReadyDelay();
            float dodgeReadyDelay = Math.Max(0, dashCooldown);
            float attackReadyDelay = Math.Max(0, attackCooldown);
            if (playerAction != null)
                attackReadyDelay = Math.Max(attackReadyDelay,
                    Math.Max(0, playerAction.duration - playerAction.elapsed));
            // TickVisuals drains hitTime before locomotion and action
            // release. Preserve the START-OF-FRAME stun slice so a final
            // 40ms hit in a 50ms frame cannot grant 50ms of free movement
            // or release an attack ten milliseconds before its pose.
            float hitLockedDt=Math.Min(dt,Math.Max(0f,playerHitTime));
            movementDirection = new Point();
            TickVisuals(dt); if (mode != "world") return; TickGate(dt);
            for (int i = effects.Count - 1; i >= 0; i--) { effects[i].life -= dt; if (effects[i].life <= 0) effects.RemoveAt(i); }
            if (Dying) { deathTime -= dt; if (deathTime <= 0) CompleteDeath(); return; }
            bool beganInPractice = practicing;
            save.totalTime += dt;
            // Resolve changed world collision before any body, missile or contact advances.
            TickTideTransition(); Point beforeMove = Position;
            attackCooldown -= dt; dashCooldown -= dt; skillCooldown -= dt; invulnerable -= dt; guardTime -= dt;
            TickSwordGuard(dt);
            TickSwordLesson(dt);
            dodgeEscapeTime = Math.Max(0, dodgeEscapeTime - dt);
            save.energy = Clamp(save.energy + dt * ((save.profession == 2 ? 18 : 14) + (InAbyss ? save.boons[1] * 4 : 0) + (save.teaUntil > save.totalTime ? 6 : 0) +
                (Has("commons_relay_complete") ? 2 : 0) + (Has("roadcharter_reserve") ? 3 : 0) +
                (save.room<8 && (save.x>=36 || save.y>=24) &&
                    TrailCircuitComplete(save.room) ? 2 : 0) + (racing ? 8 : 0) - (save.cold > 70 ? 4 : 0)), 0, 100);
            if (movement.Length > 1) movement = movement.Normalized;
            ValidateActionProfile();
            TickMovement(dt, movement, explicitAim, hitLockedDt);
            // An UNRELEASED action is planted through hit recoil. Only
            // the unlocked remainder may advance its contact/damage clock;
            // already-released animation recovery keeps its old duration.
            TickAction(playerAction!=null&&!playerAction.released?
                Math.Max(0f,dt-hitLockedDt):dt);
            if (mode != "world" || beganInPractice && !practicing) return;
            // A buffered dodge wins the newly available movement frame.
            // Fresh avoidance never fires a queued Q or swing in that tick.
            TickBufferedDodge(dt, dodgeReadyDelay);
            TickBufferedSkill(dt, skillReadyDelay);
            TickBufferedAttack(dt, attackReadyDelay);
            UpdateEnemies(dt); if (Dying) return;
            UpdateProjectiles(dt); if (Dying || beganInPractice && !practicing) return;
            TickOuterPatrol();
            TickAfterlightWatch(dt);
            TickFarCourier(dt);
            TickCivicResponse(dt);
            TickMineConvoy(dt);
            TickFrostSignal(dt);
            TickFloodRelief(dt);
            TickLastDirective(dt);
            TickCivicConduit(dt);
            TickExpansion(dt);
            if (Dying) return;
            TickGuidance(beforeMove);
            TickPolish(dt);
        }
        public void Dash(Point direction)
        {
            if(!Finite(direction.x)||!Finite(direction.y))
                direction=new Point();
            float cost = Relic(2) ? 25 : 20;
            if (mode != "world" || Dying) return;
            if (dashCooldown > 0 || save.energy < cost)
            {
                if(dashCooldown>0&&dashCooldown<=DodgeBufferWindow+.00001f&&
                   save.energy>=cost)
                    QueueLateDodge(direction);
                else
                {
                    ClearBufferedDodge();
                    ActionUnavailable(dashCooldown,cost);
                }
                return;
            }
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
            if(!Finite(direction.x)||!Finite(direction.y))
                direction=new Point();
            if (mode != "world" || Dying || bufferedSkill != null ||
                bufferedDodge != null) return;
            if (ActionTransitionBlocked || attackCooldown > 0)
            { QueueLateAttack(direction); return; }
            ClearBufferedAttack();
            SetAim(direction);
            WeaponProfile profile = Profile(save.weapon, Style);
            if (save.energy < profile.energy) { ActionUnavailable(0, profile.energy); return; }
            save.energy -= profile.energy; attackCooldown = profile.interval;
            combo = comboTimer > 0 ? (combo + 1) % 3 : 0; comboTimer = 1.1f;
            // Completing an uninterrupted three-hit chain matters for ranged
            // loadouts too: the bow earns a measured 15% release bonus, while
            // the starstaff's slower third cast earns 12%. Sword keeps its
            // established 25% finisher and larger contact reach.
            float finisher = combo != 2 ? 1f : save.weapon == 0 ? 1.25f :
                save.weapon == 1 ? 1.15f : 1.12f;
            // Queue a counter on the accepted action, but spend it only
            // when a genuine hit/projectile is RELEASED. Dodge, hit stun or
            // an equipment change before contact must not silently waste it.
            float attackDamage = profile.damage * finisher;
            BeginAction("attack", attackDamage, profile.interval,
                profile.interval * (save.weapon == 1 ? .55f : .45f));
        }
        public void Skill(Point? direction = null)
        {
            float cost = Relic(4) ? 50 : 40;
            if (mode != "world" || Dying) return;
            if (skillCooldown > 0 || save.energy < cost) { ClearBufferedSkill(); ActionUnavailable(skillCooldown, cost); return; }
            // Repeated Q intentionally updates the currently queued aim and
            // world target (the existing tactical retarget contract). NEVER
            // create a fresh intent or reset its original 200ms expiry: rapid
            // presses cannot prolong a hit-stun queue indefinitely.
            if (bufferedSkill != null)
            {
                bufferedSkill.direction = direction.HasValue &&
                    Finite(direction.Value.x) && Finite(direction.Value.y) &&
                    direction.Value.Length>.05f ?
                    direction.Value.Normalized : aim;
                bufferedSkill.target = aimTarget;
                return;
            }
            // A later explicit Q cancels a still-pending dodge; do not
            // charge both actions or discard the newer deliberate input.
            ClearBufferedDodge();
            ClearBufferedAttack(); // A requested Q outranks a queued ordinary swing.
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
                // Mechanics remain Ranger volley (five aimed projectiles)
                // while art/launch height follow the weapon the actor is
                // actually holding. A staff fires arcane arrows rather than
                // abruptly switching the protagonist into an old Ranger.
                int visualWeapon = action.equippedWeapon;
                int released = 0;
                Projectile blocked = null;
                for (int i = -2; i <= 2; i++)
                {
                    Projectile shot = FriendlyShot(Position, visualWeapon,
                        action.direction, 13, action.damage * 1.25f, false,
                        action.target, action.impact);
                    // The release at the hand must be physically valid.
                    // A neighboring wall can enclose the muzzle while the
                    // actor's feet and the target centerline remain clear.
                    if (shot.blockedAtSpawn) { blocked = shot; continue; }
                    double a = Math.Atan2(shot.velocity.y, shot.velocity.x) + i * .15;
                    shot.velocity = new Point((float)Math.Cos(a), (float)Math.Sin(a)) * 13;
                    shot.technique = true; projectiles.Add(shot); released++;
                }
                if (released > 0)
                    effects.Add(new Effect("release",
                        WeaponOrigin(Position, visualWeapon, action.direction), .18f)
                        { direction = action.direction, variant = visualWeapon,
                          height = WeaponHeight(visualWeapon, action.direction) });
                else if (blocked != null)
                {
                    effects.Add(new Effect("projectileImpact", blocked.position, .22f)
                        { direction = action.direction, height = blocked.height });
                    sound = "guard";
                }
                return;
            }
            if (save.profession == 0) guardTime = 3;
            float range = save.profession == 2 ? 4.5f : 3.1f;
            effects.Add(new Effect("burst", Position, .6f) { variant = save.profession, radius = range });
            foreach (Enemy e in enemies.ToArray())
                if ((e.Position - Position).Length < range && ClearAttackPath(Position, e.Position)) { ApplyEnemyControl(e,e.boss ? .5f : 1.4f); Hit(e, action.damage * (save.profession == 2 ? 2.3f : 1.9f), true,null,action.impact); }
        }
        private float Random()
        {
            save.seed = (int)(((long)save.seed * 1103515245 + 12345) & 0x7fffffff); return save.seed / 2147483647f;
        }
        private void Hit(Enemy e, float damage, bool technique = false, Point? attackDirection = null, ImpactProfile impact = null)
        {
            // Reject malformed or cosmetic attack power before recording a
            // tutorial strike, RNG roll, stagger, loot, death or hit-stop.
            // Negative power previously healed enemies and polluted combat.
            if (!Finite(damage) || damage <= 0 || e == null || !enemies.Contains(e)) return;
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
            if (e.stagger >= (e.boss ? 95 : 30))
            {
                e.stagger = 0;
                e.staggerRecovery = e.boss ? 4 : 2;
                // A damage-earned Break is a REAL hard crowd control,
                // exactly like frost, not just a status label. Reuse its
                // authoritative interruption/cue cleanup before publishing
                // the stagger reward. Preserve the original 1s recovery.
                ApplyEnemyControl(e,e.boss ? 1.1f : 1.7f);
                // The earned Break creates at least a full second of
                // recovery; it must not REDUCE an already longer idle,
                // stagger or support-call recovery. Keep the shared
                // interrupt path responsible for live warning cleanup.
                e.state = 0; e.timer = Math.Max(e.timer, 1f);
                effects.Add(new Effect("break",e.Position,.7f,
                    L("破势","BREAK")));
            }
            e.hp -= damage; e.flash = .09f; e.hitTime = .18f; e.recoil = attackDirection ?? (e.Position - Position).Normalized;
            effects.Add(new Effect(e.kind == 2 || e.boss ? "spark" : "impact", e.Position, .22f) { direction = e.recoil });
            if (!save.reducedMotion) { hitStop = Math.Max(hitStop, technique ? .05f : .025f); impactShake = Math.Max(impactShake, technique ? .12f : profile.weapon == 0 && profile.combo == 2 ? .07f : 0); }
            effects.Add(new Effect("number", e.Position, .7f, (crit ? "! " : "") + ((int)damage)));
            if (e.hp > 0) return;
            fallen.Add(new FallenActor { actor = e, room = save.room, life = e.boss ? 1.4f : .65f, duration = e.boss ? 1.4f : .65f });
            enemies.Remove(e);
            OnAtlasEchoEnemyDefeated(e);
            OnCommonsRelayEnemyDefeated(e);
            OnWitnessEnemyDefeated(e);
            OnSkywatchEnemyDefeated(e);
            OnTemperEnemyDefeated(e);
            OnFrostSignalEnemyDefeated(e);
            OnMineConvoyEnemyDefeated(e);
            OnGlasswindEnemyDefeated(e);
            OnTriadSecurityEnemyDefeated(e);
            OnTideSalvageEnemyDefeated(e);
            OnWayfarerEnemyDefeated(e);
            OnRoadwardenTrialEnemyDefeated(e);
            OnFarCourierEnemyDefeated(e);
            OnFloodReliefEnemyDefeated(e);
            // A lethal strike may bypass the stagger threshold entirely.
            // Its unspent civilian windup must disappear with the attacker,
            // not keep warning players about an attack that can no longer land.
            RemoveEnemyPendingWarning(e);
            // Patrol progress records a combat death, never list disappearance.
            OnOuterPatrolEnemyDefeated(e);
            OnAfterlightWatchEnemyDefeated(e);
            OnCivicResponseEnemyDefeated(e);
            OnLastDirectiveEnemyDefeated(e);
            if (IsRootHarmonyEnemy(e) && !enemies.Exists(IsRootHarmonyEnemy))
                hazards.RemoveAll(h => h.rootHarmonyOwned);
            if (IsCommonsRelayEnemy(e) && !enemies.Exists(IsCommonsRelayEnemy))
                hazards.RemoveAll(h => h.commonsRelayOwned);
            if (IsSkywatchEnemy(e))
                hazards.RemoveAll(h => h.skywatchOwned);
            if (IsAtlasEchoEnemy(e))
            {
                // Killing the echo can happen before TickExpansion processes
                // lethal pending hazards in this exact frame. Retire them at
                // the contact transaction, not a later TickPolish phase.
                hazards.RemoveAll(h => h.atlasEchoOwned);
                atlasEchoOpenTime = 0;
            }
            bool specialEncounterEnemy = IsRoadwardenTrialEnemy(e) || IsWayfarerEnemy(e) ||
                IsAtlasEchoEnemy(e) || IsReliefMissionEnemy(e) || IsRootHarmonyEnemy(e) ||
                IsCommonsRelayEnemy(e) || IsSkywatchEnemy(e) || IsOuterPatrolEnemy(e) || IsAfterlightWatchEnemy(e) || IsFarCourierRaider(e) || IsCivicResponseEnemy(e) || IsLastDirectiveEnemy(e) || IsGlasswindEnemy(e) || IsTideSalvageEnemy(e) || IsTemperEnemy(e) || IsMineConvoyEnemy(e) || IsFrostSignalEnemy(e) || IsTriadEnemy(e) || IsWitnessEnemy(e) || IsFloodReliefEnemy(e);
            // Authored encounter combat pays only its first-clear quest reward,
            // never normal chapter kills, bounty credits, or loot drops.
            if (!specialEncounterEnemy)
            {
                // Gameplay can keep running for hours after a full bag/
                // journal. Apply the SAME caps as SaveData.Load at the
                // original combat reward, rather than silently trimming
                // progress or overflowing counters on the next reload.
                save.kills[save.room] = Math.Min(99999, save.kills[save.room] + 1);
                OnExpansionKill(e, profile.masteryWeapon, profile.playerOwned);
            }
            if (!specialEncounterEnemy) effects.Add(new Effect("loot", e.Position, .75f) { variant = e.boss ? 5 : e.kind == 2 ? 4 : e.kind == 1 ? 1 : 2 });
            if (!InAbyss && !specialEncounterEnemy)
            {
                save.coins = Math.Min(999999,
                    save.coins + (e.boss ? 70 : 6));
                GrantXp(e.boss ? 80 : 12, true);
                int lootSlot = e.boss ? 5 : e.kind == 2 ? 4 : e.kind == 1 ? 1 : 2;
                save.inventory[lootSlot] = Math.Min(9999, save.inventory[lootSlot] + 1);
            }
            effects.Add(new Effect("defeat", e.Position, .5f) { variant = e.boss ? 1 : 0 }); sound = "reward";
            // Optional Skywatch's armored elite is NOT a main chapter boss.
            // Its victory is accounted for by TickSkywatch only, never by
            // the original regional boss/core or gate progression flags.
            if (e.boss && !IsSkywatchEnemy(e))
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
        // Genuine crowd-control effects INTERRUPT a still-telegraphed
        // attack; cosmetic hit flashes do not. A 450ms frozen windup whose
        // timer resumes at 1ms would otherwise damage the hero immediately
        // after the freeze, with no readable new warning. Give the opponent
        // a real, bounded recovery before it can select another attack.
        // Retain long stuns and existing contact/release animation clocks.
        private void ApplyEnemyControl(Enemy enemy,float seconds)
        {
            if(enemy==null||enemy.hp<=0f||seconds<=0f)return;
            enemy.stun=Math.Max(enemy.stun,seconds);
            if(enemy.state!=1)return;
            enemy.state=0;
            enemy.timer=Math.Max(enemy.timer,enemy.boss?.65f:.45f);
            enemy.targetingWayfarer=false;
            enemy.targetingReliefCivilian=false;
            enemy.targetingAfterlightStock=false;
            RemoveEnemyPendingWarning(enemy);
        }
        private void RemoveEnemyPendingWarning(Enemy enemy)
        {
            if(enemy==null)return;
            // A canceled Boss support call must retract only its own warning,
            // not a second guardian's cue or unrelated player combat effects.
            if(enemy.id!=0)
                effects.RemoveAll(fx=>fx.bossSummonCueOwnerId!=0&&
                    fx.bossSummonCueOwnerId==enemy.id);
            Effect pending;
            if(enemy!=null&&
                rescueWindupEffects.TryGetValue(enemy,out pending))
            {
                // Remove by object identity, not target coordinate or
                // mission tag: two overlapping NPC lanes can both be live.
                // This is shared by stun, Break and lethal attacks.
                effects.Remove(pending);
                rescueWindupEffects.Remove(enemy);
            }
        }

        private void Hurt(float damage, string source = "environment", Point? incoming = null)
        {
            // A zero-damage warning or malformed negative/non-finite combat
            // value must not stagger the hero, grant free i-frames, trigger
            // courier damage or produce misleading hurt/death feedback.
            if (!Finite(damage) || damage <= 0) return;
            if (mode != "world" || Dying) return;
            if (invulnerable > 0) { if (dashTime > 0 && perfectDodgeArmed && source != "environment") AwardPerfectDodge(); return; }
            if (practicing) { Notify("观察红圈：在命中前闪避，或闪到圈外。练习不会受伤。", "Read the red circle: dodge before impact or leave its area. Practice cannot hurt you."); return; }
            // Timed frontal sword contact opens the original real riposte
            // damage window. Area slams and behind-the-back lunges cannot
            // be parried by an unrelated global defensive buff.
            if(TryParryRealMelee(source,incoming))return;
            float multiplier = save.difficulty == 0 ? .45f : save.difficulty == 2 ? 1.4f : 1;
            float armor = (guardTime > 0 ? .45f : 1) * (1 - save.talents[1] * .035f) * (InAbyss ? 1 - save.boons[2] * .12f : 1) * (Relic(5) ? 1.15f : 1);
            playerHitFacing=VisualFacing; playerHitWeapon=AppearanceWeapon;
            Point reaction = incoming ?? VisualFacing * -1;
            if (reaction.Length < .001f) reaction = VisualFacing * -1;
            float actualDamage=Math.Max(0,damage*multiplier*armor);
            save.health -= actualDamage;
            DamageFarCourierCargo(actualDamage,source);
            DamageCivicResponseMedicine(actualDamage,source);
            DamageCivicConduit(actualDamage,source);
            DamageFloodReliefParcel(actualDamage,source);
            DamageAfterlightCharge(actualDamage,source);
            invulnerable = .65f; sound = "hurt"; playerHitTime = .18f; playerFlash = .09f; playerRecoil = reaction.Normalized; recoilSpeed = damage >= 20 ? 1.8f : .75f;
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
        // Every effect created by an authored enemy attack belongs to the
        // SAME live encounter as its projectile. When that encounter ends,
        // retiring its threats must also retire pending circles, release
        // poses and contact flashes. Untagged ambient/world effects survive.
        private void TagEnemyCombatEffect(Effect effect, Enemy attacker,
            bool targetsCivilian = false)
        {
            effect.reliefMissionOwned = IsReliefMissionEnemy(attacker) || targetsCivilian;
            effect.roadTrialOwned = IsRoadwardenTrialEnemy(attacker);
            effect.skywatchOwned = IsSkywatchEnemy(attacker);
            effect.atlasEchoOwned = IsAtlasEchoEnemy(attacker);
            effect.wayfarerOwned = IsWayfarerEnemy(attacker);
            effect.rootHarmonyOwned = IsRootHarmonyEnemy(attacker);
            effect.commonsRelayOwned = IsCommonsRelayEnemy(attacker);
            effect.glasswindOwned = IsGlasswindEnemy(attacker);
            effect.tideSalvageOwned = IsTideSalvageEnemy(attacker);
            effect.temperOwned = IsTemperEnemy(attacker);
            effect.mineConvoyOwned = IsMineConvoyEnemy(attacker);
            effect.frostSignalOwned = IsFrostSignalEnemy(attacker);
            effect.triadOwned = IsTriadEnemy(attacker);
            effect.witnessOwned = IsWitnessEnemy(attacker);
            effect.courierOwned = IsFarCourierRaider(attacker);
            effect.floodReliefOwned = IsFloodReliefEnemy(attacker);
            effect.civicResponseOwned = IsCivicResponseEnemy(attacker);
            effect.lastDirectiveOwned = IsLastDirectiveEnemy(attacker);
            effect.afterlightWatchOwned = IsAfterlightWatchEnemy(attacker);
        }
        private void UpdateEnemies(float dt)
        {
            tickingEnemies.Clear();tickingEnemies.AddRange(enemies);
            foreach (Enemy e in tickingEnemies)
            {
                if (Dying) break;
                if (!enemies.Contains(e) || mode != "world") continue;
                // Reactions overlap on one shared simulation clock. When a
                // 40ms stun ends inside a 50ms tick, only the UNLOCKED 10ms
                // may advance pursuit or a telegraphed attack. Previously all
                // 50ms advanced at once, giving freed enemies an early hit.
                float hitBudget=Math.Max(0f,e.hitTime);
                float lockBudget=Math.Max(hitBudget,
                    Math.Max(Math.Max(0f,e.stun),Math.Max(0f,e.releaseTime)));
                float freeDt=Math.Max(0f,dt-Math.Min(dt,lockBudget));
                float recoilDt=Math.Min(dt,hitBudget);
                e.moving = e.backpedaling = false;
                e.flash -= dt; e.hitTime -= dt; e.releaseTime -= dt;
                e.stun -= dt; e.staggerRecovery -= dt;
                // Honor the LAST partial recoil slice even if the hit pose
                // expires this frame. Keep wall-safe movement and original
                // move-speed / weapon clocks.
                if(recoilDt>0f)
                {
                    Point pushed=SweepActor(e.Position,
                        e.recoil*recoilDt*(e.boss?.15f:.8f));
                    e.x=pushed.x;e.y=pushed.y;
                }
                if(freeDt<=.000001f)continue;
                // Beacon attackers advance on the ward instead of blindly following
                // the hero. Their normal attacks still target the hero if in range.
                bool wardAttacker = roadTrialRoom == save.room && IsRoadwardenTrialEnemy(e);
                bool travelerAttacker = wayfarerEncounterRoom == save.room && IsWayfarerEnemy(e);
                bool reliefAttacker = reliefMissionRoom == save.room &&
                    (reliefMissionWave == 1 || reliefMissionWave == 2) && IsReliefMissionEnemy(e);
                bool escortPatrol = reliefMissionRoom == save.room &&
                    reliefMissionWave == 3 && !IsReliefMissionEnemy(e) && !e.boss &&
                    (e.Position - reliefFollowerPosition).Length < 8f;
                // The public station is an actual defense objective: melee
                // raiders advance toward its receiver, ranged threats still
                // pressure the hero with ordinary traveling projectiles.
                bool stockRaider=IsAfterlightWatchEnemy(e)&&e.kind!=1;
                Point pursuit = stockRaider ? AfterlightPoint(save.room,"outlet") :
                    wardAttacker ? ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position) :
                    travelerAttacker ? ObjectPoint(wayfarerEncounterRoom, "wayfarer" + wayfarerEncounterRoom, Position) :
                    reliefAttacker || escortPatrol ? reliefFollowerPosition : Position;
                Point delta = pursuit - e.Position; float distance = delta.Length;
                if (e.state == 1)
                {
                    // A gate, rising tide or collapsing floor may close
                    // UNDER an enemy after the attack warning was issued.
                    // A sealed attacker cannot complete its old contact,
                    // fire missiles or call Boss support from solid ground.
                    if (!CanStand(e.Position))
                    {
                        e.state = 0;
                        e.timer = Math.Max(e.timer, .22f);
                        e.targetingWayfarer = e.targetingReliefCivilian =
                            e.targetingAfterlightStock = false;
                        RemoveEnemyPendingWarning(e);
                        continue;
                    }
                    // Validate the locked release lane against CURRENT
                    // topology. An obstacle may have closed after a real
                    // windup began, without burying either actor's feet.
                    // Cancel only this attack and its still-pending cues:
                    // no invisible arrows, false contact or Boss summons
                    // from behind a newly raised gate.
                    // Ordinary archers require an actual physical
                    // firing lane. Melee attacks intentionally finish their
                    // telegraphed swing even against cover (contact itself
                    // checks LOS); bosses retain their authored area/support
                    // cycles and must not be canceled by an obsolete target
                    // point unrelated to their radial missile pattern.
                    if (!e.boss && e.kind == 1 &&
                        !ClearAttackPath(e.Position,e.target))
                    {
                        e.state=0;
                        // Cancel the missed *long* draw: once cover opens,
                        // reacquire the lane on the same responsive 220ms
                        // polling cadence as companion ranged combat.
                        e.timer=.22f;
                        e.targetingWayfarer=e.targetingReliefCivilian=
                            e.targetingAfterlightStock=false;
                        RemoveEnemyPendingWarning(e);
                        continue;
                    }
                    e.timer -= freeDt;
                    if (e.boss && save.room == 1 && e.windupDuration > 0 && e.timer < .2f)
                    {
                        Point next = e.attackOrigin + (e.attackDestination - e.attackOrigin) * Clamp(1 - e.timer / .2f, 0, 1);
                        Point swept = SweepActor(e.Position, next - e.Position); e.x = swept.x; e.y = swept.y;
                    }
                    if (e.timer <= 0)
                    {
                        e.releaseTime = e.boss ? .38f : .24f;
                        sound = e.boss || e.kind == 2 ? "slam" : e.kind == 1 ? "arrow" : "attack";
                        var releaseEffect = new Effect("enemyRelease", e.Position, .24f) {
                            direction = e.facing, variant = e.boss ? 10 + save.room : e.kind };
                        TagEnemyCombatEffect(releaseEffect, e,
                            escortPatrol && e.targetingReliefCivilian);
                        effects.Add(releaseEffect);
                        if (e.boss || e.kind == 2)
                        {
                            float radius = EnemyAreaRadius(e);
                            // The red ring warned during windup. At the
                            // actual contact it becomes a short impact flash,
                            // not another 450ms "incoming" danger telegraph.
                            // This feedback shares the real damage target.
                            var strikeEffect = new Effect("burst", e.target, .24f) {
                                variant = e.boss ? save.room : 0, radius = radius };
                            TagEnemyCombatEffect(strikeEffect, e,
                                escortPatrol && e.targetingReliefCivilian);
                            effects.Add(strikeEffect);
                            if ((Position - e.target).Length < radius && ClearAttackPath(e.Position, Position)) Hurt(e.boss ? 22 + e.BossPhase * 3 : 14, "slam", (Position - e.Position).Normalized);
                            else TryRewardEscapedWarning(e.target, radius);
                            if (Dying) break;
                            if (e.boss)
                            {
                                // A released call's warning ends precisely on
                                // the real contact frame, before support appears.
                                if(e.id!=0)
                                    effects.RemoveAll(fx=>fx.bossSummonCueOwnerId==e.id);
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
                                if ((e.BossPhase == 3 || (save.room == 4 && e.BossPhase >= 2)) &&
                                    e.cycle % 3 == 0 && enemies.Count < 9)
                                    TrySpawnBossReinforcement(e);
                            }
                        }
                        else if (e.kind == 1) Fire(e, e.facing, 5);
                        else if (InEnemyMeleeArc(e,Position) && ClearAttackPath(e.Position, Position)) Hurt(11, "lunge", e.facing);
                        // Only an uninterrupted genuine enemy release can
                        // strike the receiver. Nearby bodyguards draw the
                        // enemy's locked intent and prevent stock damage.
                        if(stockRaider&&e.targetingAfterlightStock)
                            DamageAfterlightWatchStock(e);
                        if (travelerAttacker && e.targetingWayfarer) DamageWayfarer(e);
                        if (reliefMissionRoom != 0 &&
                            (IsReliefMissionEnemy(e) || escortPatrol) &&
                            e.targetingReliefCivilian && e.kind != 1)
                            DamageReliefCivilian(e);
                        // Rescue archers only injure civilians where their real
                        // arrow actually flies. No hitscan at the windup release.
                        e.targetingWayfarer = e.targetingReliefCivilian =
                            e.targetingAfterlightStock = false;
                        rescueWindupEffects.Remove(e);
                        e.state = 0; e.timer = e.boss ? 2.2f - e.BossPhase * .3f : 1.4f;
                    }
                    continue;
                }
                // Defense attackers cannot abandon the beacon after knockback
                // or while navigating around a distant obstruction.
                if (!wardAttacker && !travelerAttacker && !reliefAttacker &&
                    !escortPatrol && !stockRaider &&
                    distance > (e.boss ? 18 : 8)) continue;
                e.timer -= freeDt;
                // A rescue archer is a ranged threat, not another melee saboteur.
                // Hold a genuine firing lane outside the civilian's reach.
                float desired = (reliefAttacker || escortPatrol) && e.kind == 1 ? 4.3f :
                    wardAttacker || travelerAttacker || reliefAttacker || escortPatrol || stockRaider ? 1.05f :
                    e.boss ? 3.5f : e.kind == 1 ? 4.5f : 1.05f;
                bool visible = ClearAttackPath(e.Position, pursuit);
                Point before = e.Position;
                if (e.kind == 1 && !e.boss && !wardAttacker &&
                    !travelerAttacker && !practicing &&
                    (reliefAttacker || escortPatrol ? distance < 3.4f : distance < 3f) &&
                    (visible || reliefAttacker || escortPatrol))
                {
                    // Backstep even with blocked LOS. Never counteract an
                    // actual retreat by walking toward the target this tick.
                    Point away = delta.Length > .001f ?
                        delta.Normalized * -freeDt * (reliefAttacker || escortPatrol ? 2.2f : 1.6f) :
                        new Point(-freeDt * 1.6f, 0);
                    Point back = SweepActor(e.Position, away);
                    e.backpedaling = (back - e.Position).Length > .001f;
                    e.x = back.x; e.y = back.y;
                }
                if (!e.backpedaling && (distance > desired || !visible))
                {
                    Point p = e.Position + DirectionTo(e.Position, pursuit) * freeDt * (e.boss ? 1.15f + e.BossPhase * .15f : e.kind == 0 ? 2.1f : 1.5f) * (InAbyss && save.abyssFloor % 3 == 2 ? 1.2f : 1);
                    Point swept = SweepActor(e.Position, p - e.Position); e.x = swept.x; e.y = swept.y;
                }
                Point travelled = e.Position - before; e.moving = travelled.Length > .001f;
                if (e.moving) { e.moveTime += travelled.Length; e.Face(e.backpedaling?Position-e.Position:travelled); }
                delta = Position - e.Position; distance = delta.Length;
                bool heroInRange = distance < (e.boss ? 12 : e.kind == 1 ? 7 : 2.5f) &&
                    ClearAttackPath(e.Position, Position);
                bool nearTraveler = travelerAttacker && (pursuit - e.Position).Length < 2.2f &&
                    ClearAttackPath(e.Position, pursuit);
                // Standing in the raider's reach lets the hero draw its attention.
                // Otherwise the telegraphed strike lands on the vulnerable traveler.
                bool strikeTraveler = nearTraveler && (!heroInRange || distance > 2.2f);
                // A rescue raider must visibly commit to striking the civilians.
                // Archers still attack the playable hero; melee and breakers can
                // target civilians when the hero does not hold the frontline.
                bool nearCivilian = (reliefAttacker || escortPatrol) &&
                    (pursuit - e.Position).Length < (e.kind == 1 ? 6.5f : 2.1f) &&
                    ClearAttackPath(e.Position, pursuit);
                bool strikeCivilian = nearCivilian &&
                    (!heroInRange || distance > 2.2f);
                bool nearStock=stockRaider&&(pursuit-e.Position).Length<2.2f &&
                    ClearAttackPath(e.Position,pursuit);
                bool strikeStock=nearStock&&(!heroInRange||distance>2.2f);
                // A still-buried enemy may recover its footing when the
                // world opens again, but must not start a new windup from an
                // invalid tile while waiting for that transition.
                if (e.timer <= 0 && CanStand(e.Position) &&
                    (heroInRange || strikeTraveler || strikeCivilian || strikeStock))
                {
                    e.state = 1; e.timer = e.boss ? 1.1f - e.BossPhase * .08f : .65f; e.windupDuration = e.timer;
                    e.targetingWayfarer = strikeTraveler;
                    e.targetingReliefCivilian = strikeCivilian;
                    e.targetingAfterlightStock = strikeStock;
                    e.target = strikeTraveler || strikeCivilian || strikeStock ? pursuit : Position;
                    e.Face(e.target - e.Position);
                    // A ground warning must precede the REAL damage, not
                    // appear for the first time on its contact frame.
                    // Heavy slams and Boss AoE target a locked circle; rescue
                    // melee has the same contract around its civilian.
                    // Archers still use actual traveling arrows, not fake
                    // melee rings. Keep this effect tied to its attacker so
                    // stun, stagger or death can retract an unspent warning.
                    bool areaAttack = e.boss || e.kind == 2;
                    if (areaAttack || (strikeCivilian && e.kind != 1) ||
                        (strikeStock && e.kind != 1))
                    {
                        var telegraph = new Effect("danger", e.target, e.timer)
                        {
                            radius = areaAttack ? EnemyAreaRadius(e) : EnemyMeleeRadius,
                            variant = e.boss ? save.room : 0
                        };
                        TagEnemyCombatEffect(telegraph, e, strikeCivilian);
                        effects.Add(telegraph);
                        rescueWindupEffects.Remove(e);
                        rescueWindupEffects.Add(e, telegraph);
                    }
                    if (e.boss || distance < 6 || strikeCivilian || strikeStock) sound = "windup";
                    if(e.boss && e.id!=0 && enemies.Count<9 &&
                        (e.BossPhase==3||(save.room==4&&e.BossPhase>=2)) &&
                        (e.cycle+1)%3==0)
                    {
                        // Announce the ACTUAL upcoming support cycle before
                        // the enemy's existing timed attack. Staggering,
                        // freezing or defeating its owner retracts this cue.
                        effects.Add(new Effect("number",
                            e.Position+new Point(0,-1f),e.timer+.06f,
                            L("正在呼叫增援！","CALLING REINFORCEMENTS!"))
                            {bossSummonCueOwnerId=e.id});
                    }
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
        { Projectile shot=e.boss?new Projectile{position=e.Position,velocity=dir.Normalized*speed,damage=14}:WeaponShot(e.Position,1,dir,speed,10,e.target); shot.source="shot";shot.roadTrialOwned = IsRoadwardenTrialEnemy(e);
            shot.skywatchOwned = IsSkywatchEnemy(e);
            shot.atlasEchoOwned = IsAtlasEchoEnemy(e);
            shot.wayfarerOwned = IsWayfarerEnemy(e);
            shot.courierOwned=IsFarCourierRaider(e);shot.floodReliefOwned=IsFloodReliefEnemy(e);shot.civicResponseOwned=IsCivicResponseEnemy(e);shot.lastDirectiveOwned=IsLastDirectiveEnemy(e);shot.rootHarmonyOwned = IsRootHarmonyEnemy(e);shot.reliefMissionOwned = IsReliefMissionEnemy(e) ||
                reliefMissionWave == 3 && reliefMissionRoom == save.room &&
                e.targetingReliefCivilian;shot.tideSalvageOwned=IsTideSalvageEnemy(e);shot.temperOwned=IsTemperEnemy(e);shot.mineConvoyOwned=IsMineConvoyEnemy(e);shot.frostSignalOwned=IsFrostSignalEnemy(e);shot.triadOwned=IsTriadEnemy(e);shot.witnessOwned=IsWitnessEnemy(e);
            shot.commonsRelayOwned=IsCommonsRelayEnemy(e);
            shot.glasswindOwned=IsGlasswindEnemy(e);
            shot.afterlightWatchOwned=IsAfterlightWatchEnemy(e);
            shot.visual=e.boss?"enemyOrb":"enemyArrow";shot.height=e.boss?BossEmissionHeight(save.room):WeaponHeight(1,dir);shot.launchHeight=shot.flightHeight=shot.height;shot.theme=e.boss?save.room:0;projectiles.Add(shot);sound=e.boss?"spell":"arrow"; }
        // Signed ray-circle entry measured from a shared flight sample.
        // A negative value means the projectile has already crossed that
        // actor's front edge. Comparing entries is still well-defined when
        // two hurt circles overlap on the same <=0.1-tile sweep step.
        private static float ProjectileCircleEntry(Point point, Point velocity,
            Point center, float radius)
        {
            Point heading=velocity.Normalized;
            Point offset=center-point;
            float along=offset.x*heading.x+offset.y*heading.y;
            float dist2=offset.x*offset.x+offset.y*offset.y;
            float sideways2=Math.Max(0f,dist2-along*along);
            return along-(float)Math.Sqrt(Math.Max(0f,
                radius*radius-sideways2));
        }
        private void AddProjectileFade(Projectile p)
        {
            // Flight expiration or invalid spawn has no genuine contact
            // radius. Keep existing source/mission-owned fade metadata.
            effects.Add(new Effect("projectileFade", p.position, .25f) {
                        direction = p.velocity.Normalized,
                        variant = p.visual == "arrow" || p.visual == "enemyArrow" ? 8 : p.explosive ? 9 : 10,
                        height = p.height, reliefMissionOwned = p.reliefMissionOwned, tideSalvageOwned = p.tideSalvageOwned, temperOwned = p.temperOwned, triadOwned = p.triadOwned, witnessOwned = p.witnessOwned,
                        rootHarmonyOwned = p.rootHarmonyOwned, courierOwned = p.courierOwned, civicResponseOwned = p.civicResponseOwned,
                        mineConvoyOwned = p.mineConvoyOwned, frostSignalOwned = p.frostSignalOwned, swordLessonOwned = p.swordLessonOwned,
                        commonsRelayOwned = p.commonsRelayOwned, glasswindOwned = p.glasswindOwned, blackoutOwned = p.blackoutOwned,
                        roadTrialOwned = p.roadTrialOwned, skywatchOwned = p.skywatchOwned, atlasEchoOwned = p.atlasEchoOwned, wayfarerOwned = p.wayfarerOwned,
                        stormOwned = p.stormOwned, lastDirectiveOwned = p.lastDirectiveOwned,
                        civicConduitOwned = p.civicConduitOwned,
                        afterlightWatchOwned = p.afterlightWatchOwned });
        }
        private void UpdateProjectiles(float dt)
        {
            tickingProjectiles.Clear();tickingProjectiles.AddRange(projectiles);
            foreach (Projectile p in tickingProjectiles)
            {
                if (Dying) break;
                if (!projectiles.Contains(p)) continue;
                // TickAction and UpdateEnemies can release mid-frame. They
                // must not borrow the entire earlier world-time interval for
                // projectile velocity, expiry, contact or reflection.
                if (!flightReadyAtFrameStart.Contains(p)) continue;
                float flightDt = Math.Min(dt, Math.Max(0, p.life));
                p.life -= dt; p.age += flightDt;
                if(p.launchHeight>=0&&p.flightHeight>=0)
                { float t=Clamp(p.age/.25f,0,1);t=t*t*(3-2*t);p.height=p.launchHeight+(p.flightHeight-p.launchHeight)*t; }
                if (p.life <= 0 && flightDt <= 0)
                { projectiles.Remove(p); AddProjectileFade(p); continue; }
                int steps = Math.Max(1, (int)Math.Ceiling(p.velocity.Length * flightDt / .1f));
                bool startedBlocked = p.blockedAtSpawn ||
                    ShotBlocked(p,p.position);
                bool dead = startedBlocked, civilianImpactProduced = false;
                float impactRadius = 0;
                // Capture collision candidates once per flying projectile,
                // not on every 0.1-tile sweep subdivision. Refresh only
                // after a real hit changes the possible target roster.
                if (p.friendly) { projectileTargets.Clear(); projectileTargets.AddRange(enemies); }
                for (int step = 0; step < steps && !dead; step++)
                {
                    Point next = p.position + p.velocity * (flightDt / steps);
                    // Resolve the first obstruction along flight, not its endpoint.
                    // A starfire bolt striking solid cover must detonate on
                    // the NEAR side of the wall. Its 1.8-tile blast can hit
                    // visible nearby enemies, but never a target shielded
                    // behind the same wall. Spawn-blocked shots, normal arrows
                    // and range-expired projectiles do not create free blasts.
                    if (ShotBlocked(p,next))
                    {
                        dead = true;
                        if (p.friendly && p.explosive && !p.blockedAtSpawn)
                        {
                            impactRadius = 1.8f;
                            splashTargets.Clear();
                            splashTargets.AddRange(enemies);
                            foreach (Enemy other in splashTargets)
                                if ((other.Position-p.position).Length<impactRadius &&
                                    ClearAttackPath(p.position,other.Position))
                                    Hit(other,p.damage*.6f,false,
                                        (other.Position-p.position).Normalized,p.impact);
                            splashTargets.Clear();
                        }
                        break;
                    }
                    p.position = next;
                    if (p.friendly)
                    {
                        // When enemy hit-circles overlap within one 0.1-tile
                        // flight slice, the old roster-order loop let a
                        // back-row actor swallow a non-piercing arrow before
                        // the front actor. Select the earliest ray/circle
                        // entry on the actual flight heading, independently
                        // of enemy spawn/serialization order.
                        if (!p.piercing)
                        {
                            Enemy firstContact = null;
                            float firstEntry = float.MaxValue;
                            Point direction = p.velocity.Normalized;
                            foreach (Enemy candidate in projectileTargets)
                            {
                                if (candidate.hp <= 0 || p.hits.Contains(candidate.id))
                                    continue;
                                Point offset = candidate.Position - p.position;
                                float radius = (candidate.boss ? .9f : .5f) + p.Radius;
                                float dist2 = offset.x*offset.x + offset.y*offset.y;
                                if (dist2 >= radius*radius ||
                                    !ClearAttackPath(p.position,candidate.Position))
                                    continue;
                                float along = offset.x*direction.x+offset.y*direction.y;
                                float perp2 = Math.Max(0f,dist2-along*along);
                                float entry = along -
                                    (float)Math.Sqrt(Math.Max(0f,radius*radius-perp2));
                                if (entry < firstEntry)
                                {
                                    firstEntry=entry;
                                    firstContact=candidate;
                                }
                            }
                            if (firstContact != null)
                            {
                                projectileTargets.Clear();
                                projectileTargets.Add(firstContact);
                            }
                        }
                        bool hitAny = false;
                        foreach (Enemy e in projectileTargets)
                        {
                            if (p.hits.Contains(e.id) || (e.Position - p.position).Length >= (e.boss ? .9f : .5f) + p.Radius || !ClearAttackPath(p.position, e.Position)) continue;
                            p.hits.Add(e.id);
                            hitAny = true;
                            Hit(e, p.damage, p.technique, p.velocity.Normalized, p.impact);
                            // Practice completion or another transaction can
                            // retire the shot. It must not leave an old-room impact.
                            if (!projectiles.Contains(p)) { dead = true; break; }
                            if (p.slowing) ApplyEnemyControl(e,.45f); dead = !p.piercing;
                            if (p.explosive)
                            {
                                impactRadius = 1.8f; dead = true;
                                splashTargets.Clear();splashTargets.AddRange(enemies);
                                foreach (Enemy other in splashTargets) if (other != e && (other.Position - p.position).Length < impactRadius && ClearAttackPath(p.position, other.Position)) Hit(other, p.damage * .6f, false, (other.Position - p.position).Normalized, p.impact);
                                splashTargets.Clear();
                            }
                            if (!p.piercing || p.explosive) break;
                        }
                        // Death can remove/spawn enemies during Hit(). Keep
                        // pierce targets current for subsequent flight slices,
                        // without repeating AddRange for every empty slice.
                        if (hitAny && !dead && step + 1 < steps)
                        {
                            projectileTargets.Clear(); projectileTargets.AddRange(enemies);
                        }
                    }
                    else
                    {
                        bool heroContact=(Position-p.position).Length<.38f;
                        bool civilianContact=p.reliefMissionOwned &&
                            reliefMissionRoom==save.room &&
                            reliefMissionWave>=1 && reliefMissionWave<=3 &&
                            (p.position-reliefFollowerPosition).Length<.45f;
                        // When the hero and escort occupy overlapping
                        // hit circles, the old "hero first" if/else let the
                        // hero absorb a shot that physically met the escort
                        // first. Sort by front-circle intersection, while
                        // preserving the player's real interception and
                        // existing reflection when the hero is closer.
                        bool civilianFirst=civilianContact&&(!heroContact||
                            ProjectileCircleEntry(p.position,p.velocity,
                                reliefFollowerPosition,.45f)+.00001f <
                            ProjectileCircleEntry(p.position,p.velocity,
                                Position,.38f));
                        if(civilianFirst)
                        {
                            DamageReliefCivilianArrow(p);
                            civilianImpactProduced = true;
                            dead=true;
                        }
                        else if(heroContact)
                        {
                            // The melee intercept already shows a readable
                            // protective cue. Give a real body-blocked OR
                            // shield-reflected escort arrow the same feedback,
                            // but only if it was physically en route to the
                            // civilian. Never award an intercept for a stray
                            // arrow, or when the survivor was hit first.
                            if(civilianContact)
                            {
                                effects.Add(new Effect("number",Position,.7f,
                                    L("护卫拦截","INTERCEPT")){
                                        reliefMissionOwned=true});
                                effects.Add(new Effect("discover",
                                    reliefFollowerPosition,.35f){
                                        reliefMissionOwned=true});
                            }
                            // Reflection keeps the same swept projectile;
                            // a real frontal interception still wins.
                            if(TryDeflectHostileProjectile(p))break;
                            Hurt(p.damage,p.source??"shot",
                                p.velocity.Normalized);
                            dead=true;
                        }
                    }
                }
                if (p.friendly) projectileTargets.Clear();
                if (dead && projectiles.Remove(p))
                {
                    // The original muzzle/wall contract requires a small
                    // near-side CONTACT spark even if a projectile is born
                    // obstructed. That is not a full mage-area explosion.
                    // A civilian hit already emitted its rooted contact.
                    if (!civilianImpactProduced || reliefMissionRoom==0)
                        effects.Add(new Effect(!startedBlocked && p.explosive ?
                            "magicImpact" : "projectileImpact",
                        p.position, .28f) { direction = p.velocity.Normalized,
                        variant = p.friendly ? 0 : 1, radius = impactRadius, height = p.height,
                        reliefMissionOwned = p.reliefMissionOwned, tideSalvageOwned = p.tideSalvageOwned, temperOwned = p.temperOwned, triadOwned = p.triadOwned, witnessOwned = p.witnessOwned,
                        rootHarmonyOwned = p.rootHarmonyOwned, courierOwned = p.courierOwned, civicResponseOwned = p.civicResponseOwned,
                        mineConvoyOwned = p.mineConvoyOwned, frostSignalOwned = p.frostSignalOwned, swordLessonOwned = p.swordLessonOwned,
                        commonsRelayOwned = p.commonsRelayOwned, glasswindOwned = p.glasswindOwned, blackoutOwned = p.blackoutOwned,
                        roadTrialOwned = p.roadTrialOwned, skywatchOwned = p.skywatchOwned, atlasEchoOwned = p.atlasEchoOwned, wayfarerOwned = p.wayfarerOwned,
                        stormOwned = p.stormOwned, lastDirectiveOwned = p.lastDirectiveOwned,
                        civicConduitOwned = p.civicConduitOwned,
                        afterlightWatchOwned = p.afterlightWatchOwned });
                }
                else if (p.life <= 0 && projectiles.Remove(p))
                    AddProjectileFade(p);
            }
            tickingProjectiles.Clear();
        }
        // Story presence is shared by interaction, world drawing and map marks.
        // Saved quest flags remain the authority; no extra persistent state.
        public bool IsObjectPresent(WorldObject o)
        {
            if (o == null) return false;
            if(o.kind.StartsWith("stormnode")) return Has("storm_started");
            if(o.kind.StartsWith("hearingplaque"))
            {
                int region;
                return int.TryParse(o.kind.Substring("hearingplaque".Length),out region) &&
                    HearingPlaqueVisible(region);
            }
            if(o.kind.StartsWith("afterlightwaypoint"))
            {
                int region;
                return int.TryParse(o.kind.Substring("afterlightwaypoint".Length),out region) &&
                    region>=1 && region<=3 && Has("afterlight_done_"+region) &&
                    Has("afterlight_steward_"+region);
            }
            if (o.kind.StartsWith("blackoutcivilian"))
            {
                int id;
                if(int.TryParse(o.kind.Substring("blackoutcivilian".Length),out id) &&
                    id>=1 && id<=3)
                    return !Has("blackout_complete") &&
                        (blackoutFollowing!=id) && !BlackoutCitizenSaved(id);
            }
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
            if (reliefMissionRoom == save.room &&
                (reliefMissionWave == 1 || reliefMissionWave == 2) &&
                (Position - reliefFollowerPosition).Length < 1.7f)
            { TalkReliefFieldGuard(); return; }
            if (ReliefEscortActive &&
                (Position - reliefFollowerPosition).Length < 1.7f &&
                (o == null ||
                 (Position - reliefFollowerPosition).Length <=
                    (Position - new Point(o.x, o.y)).Length + .05f))
            { TalkRescuedCivilian(); return; }
            if (o == null) { Notify("靠近居民、路牌或发光物，再按 E。", "Approach a resident, sign or glowing object, then press E."); return; }
            TutorialAction(1); Talk(o);
        }

        private bool TryTalkWaytrail(WorldObject o)
        {
            if (o.kind != "waytrail") return false;
            int region = save.room;
            if (region < 0 || region > 7) return false;
            bool eastern = o.id.EndsWith("e", StringComparison.Ordinal);
            string flag = "waytrail_" + o.id;
            bool first = !Has(flag);
            if (first)
            {
                Flag(flag); CreditCoins(8); CreditShards(1); revision++;
                sound = "reward";
                bool completed = true;
                for (int i = 0; i < 8; i++)
                    if (!Has("waytrail_waytrail" + i + "s") || !Has("waytrail_waytrail" + i + "e"))
                        completed = false;
                if (completed && !Has("waytrail_complete"))
                { Flag("waytrail_complete"); CreditSkillPoints(1); revision++; }
            }
            string[] southZh = { "南侧坐标还留着一条朝旅店走的小路。补给不多，但曾足够把迷路的孩子送回镇里。", "树根下藏着巡路人的行程：先找失踪的侦察员，再问为什么林地成了禁区。", "沉桥岸边写着一长串无名者。每次潮水上涨，名单都比货单长。", "工棚旧门上有一道划痕，工人们在这里商量停机，拒绝把伤亡当作损耗。", "堡垒南侧没有城门，只有一条隐蔽的疏散坡道。有人从这里带孩子逃过星雨。", "旧码头的补给栏记着一艘未能返航的船；每一盏亮起的灯都是给它留下的位置。", "遮阴石板背面记录了绿洲水份额。这个数字和仓库的配给账本并不相同。", "雪路旁的余烬已经熄灭，留下的柴枝整齐地指向最近的有人居住的院落。" };
            string[] southEn = { "A southern bearing leads to the inn. Small provisions once guided lost children home.", "A road-warden's note lies beneath the roots: find the scout, then ask why the forest was sealed.", "A line of unnamed people runs along the drowned bridge. With each tide it grew longer than the cargo manifest.", "Workers marked this shed when they voted to stop the machines, refusing to call casualties mere wastage.", "A hidden southern evacuation slope leads away from the citadel: someone led children through the Starfall here.", "An old dock log names a boat that never returned. Every lit lamp kept a place for its crew.", "The shade marker lists the oasis water share, different from the numbers in the depot ledger.", "The fire has gone out, but neatly arranged twigs point toward the nearest inhabited shelter." };
            string[] eastZh = { "渠水绕过镇墙，在石缝里留下更早的测绘线。巡路队标明：真正的归途，必须允许平民使用。", "荆棘从一棵巨树分岔向两条路。这里的根系曾替旅人遮风，并不是天生的路障。", "退潮露出沉在泥下的旧桥钉，说明小孩逃难时所走的桥曾被人为移走过。", "运矿轨道一路伸向城墙，废弃的工人餐牌上却只剩一句“先撤人，再开机”。", "防护墙的边缘刻着两种日期：一份记录星雨，另一份记录城外第一次停电。", "船夫在岩面留下潮位：低潮能行船，高水位必须留下灯与足够的救援木板。", "透光的沙脊仍有九年前的分配刻度。旧秤量过晶石，却没有量过绿洲的需要。", "背风处的一块旧钟锤保持温暖。守路人把它交给所有找不到炉火的旅客。" };
            string[] eastEn = { "Water runs past the town wall. A survey line says the road home must remain open to ordinary families.", "Briar roots divide at an ancient tree. This shelter once protected travelers; it was not born a barricade.", "Old bridge rivets emerge at ebb tide. Someone rerouted the crossing used by escaping children.", "The abandoned mine railway carries a worker's last instruction: evacuate people before restarting machines.", "Two dates are carved outside the shield: the Starfall itself and the first night the outer settlements went dark.", "A ferryman marked two tides here: boats at ebb, warning lamps and rescue planks when the water rises.", "A glass ridge retains the nine-year allocation marks. They counted crystals, not the oasis's need.", "An old bell hammer is still warm on the lee slope, left for travelers unable to reach a brazier." };
            bool charterEvidence = RecordRoadCharterWaypoint(o);
            string charterZh = charterEvidence ? "\n\n这份实地测绘已封存到第八章的公开证据，进度 " + RoadCharterReports + "/3。" : "";
            string charterEn = charterEvidence ? "\n\nRecorded as public Chapter Eight field evidence (" + RoadCharterReports + "/3)." : "";
            string closingZh = save.ending > 0
                ? "\n\n星核的结局已经写下，但路标仍属于每一位需要它的人。"
                : "\n\n这条路线没有写进堡垒的正式地图。先记下它，未来也许能让更多人安全归来。";
            string closingEn = save.ending > 0
                ? "\n\nThe starcores have an ending. This waypoint still belongs to everyone who needs it."
                : "\n\nThis route is absent from the citadel's official chart. Keep it for those who may yet need a way home.";
            string rewardZh = first ? "\n\n首次绘图：硬币 +8，碎片 +1。" +
                (Has("waytrail_complete") ? " 所有旧路已记录：额外技能点 +1。" : "") : "";
            string rewardEn = first ? "\n\nFirst survey: +8 coins, +1 shard." +
                (Has("waytrail_complete") ? " All old routes charted: +1 talent point." : "") : "";
            Dialog(o.name, o.en,
                (eastern ? eastZh[region] : southZh[region]) + closingZh + rewardZh + charterZh,
                (eastern ? eastEn[region] : southEn[region]) + closingEn + rewardEn + charterEn,
                C("记入旧路星图", "Record in the road chart", "close"));
            return true;
        }

        public void Talk(WorldObject o)
        {
            if (!IsObjectPresent(o)) return;
            if (practicing) { Notify("先完成或结束训练，再与居民交谈。", "Finish or end practice before talking to residents."); return; }
            conversationSource = o;
            if (TalkPolish(o)) return;
            if (TalkExpansion(o)) return;
            if (TryTalkWaytrail(o)) return;
            if (TalkFrostSignal(o)) return;
            if (TalkMineConvoy(o)) return;
            if (TalkFloodArchive(o)) return;
            if (TalkRootsong(o)) return;
            if (TalkTrailCircuit(o)) return;
            if (TalkOuterRescue(o)) return;
            if (TalkOuterPatrol(o)) return;
            if (TalkRidge(o)) return;
            if (TalkCivicConduit(o)) return;
            if (TalkLastDirective(o)) return;
            if (TalkFarStation(o)) return;
            if (TalkAnnexExploration(o)) return;
            string kind = o.kind;
            if (kind == "innGuest")
            {
                Dialog(o.name, o.en, "巡路员留下的信，最后还是找到了收信的人。你要去的那些地方，我也走过。\n\n白天看道路，夜里看灯。遇到举锤的敌人，先看它的手；遇到射手，先看弓弦。能回来讲故事，比走得快更重要。" + (Has("survey_complete") ? "\n\n你留在旅店的归乡星图，已经帮三个夜归的人绕过塌桥。谢谢你没有让巡路队的名字被遗忘。星图桌现在能连接三座信标，附近安全时可以快速往返。" : Has("survey_1") || Has("survey_2") || Has("survey_3") ? "\n\n我听说你找到旧巡路信标。三段信号都齐了，就去旅店的星图桌看看。" : "") + (Has("dispatch_complete") ? "\n\n你解开了三封旧信。旅店愿意给守路人热汤优惠两枚硬币，愿所有人都能找到归途。" : Has("dispatch_6") || Has("dispatch_7") || Has("dispatch_8") ? "\n\n你已经解开一封旧信。其余两封也许能回答它留下的问题。" : "") + RoadwardenVigilInnNote(false) +
                (RootsongComplete?"\n\n密林古树重新合唱了。侦察员说这是孩子们曾用来找到庇护的三声归途，恢复的树荫还能让旅人安全休息。":"") +
                (FloodArchiveComplete?(FloodArchiveChoice==1?
                    "\n\n沉桥那些被遗漏的名字终于写进公用渡口名册；摆渡人现在在潮标旁照顾伤者。":
                    "\n\n摆渡人用你保存的原始水图训练学徒，退潮时能更安全地带旅人回岸。"):"") +
                (LastDirectiveComplete?
                    (LastDirectiveChoice==1?
                        "\n\n旅店的新留言板写着公开审议流程：旅人可以质疑过载命令，而巡路队不用再偷偷保留证据。":
                        "\n\n堡垒已承诺无条件的紧急庇护：先让迷路的人有地方过夜，再让居民决定如何修补归途。"):""), "The warden's letter finally found its recipient. I once walked the roads ahead of you.\n\nWatch the roads by day, and the lamps at night. Read a hammer's lift and an archer's bowstring. Coming home with a story matters more than traveling fast." + (Has("survey_complete") ? "\n\nThe chart you left at the inn helped three late travelers avoid the broken bridge. Thank you for keeping the wardens remembered. The chart table now links the three beacons for safe travel." : Has("survey_1") || Has("survey_2") || Has("survey_3") ? "\n\nI hear you found an old road beacon. Once all three signals are recovered, visit the chart table here at the inn." : "") + (Has("dispatch_complete") ? "\n\nYou decoded all three letters. The inn now discounts stew by two coins for those who keep the road open." : Has("dispatch_6") || Has("dispatch_7") || Has("dispatch_8") ? "\n\nYou decoded one old letter. The others may answer its questions." : "") + RoadwardenVigilInnNote(true) +
                (RootsongComplete?"\n\nThe ancient Thornwood trunks are singing again. The scout remembers their three notes guiding children home; the repaired roots now offer travelers a safe pause.":"") +
                (FloodArchiveComplete?(FloodArchiveChoice==1?
                    "\n\nThe once-erased ferry families are now on public rolls. The ferrymen tend the wounded at the restored tideposts.":
                    "\n\nThe ferrymen now teach apprentices using the original tide charts you preserved, making the ebb crossing safer."):"") +
                (LastDirectiveComplete?
                    (LastDirectiveChoice==1?
                        "\n\nThe inn displays the new public review process. Any traveler can challenge an overload order without hiding the evidence.":
                        "\n\nThe citadel has confirmed unconditional emergency refuge: shelter the stranded first, then let residents decide how to rebuild."):""), C("坐一会儿，记住这句话", "Remember the advice", "close")); return;
            }
            if (kind.StartsWith("portal")) { Enter(int.Parse(kind.Substring(6))); return; }
            if (kind == "routes")
            {
                conversation = null; mode = "map"; return;
            }
            if (kind == "guild")
            {
                if (TalkTriadGuild(o)) return;
                if (save.ending > 0) { Dialog(o.name, o.en,
                    (Has("storm_complete")?
                        "莉雅：第一场星雨终于证明了一个事实：三个地区真的可以自己决定如何分配公共电力。我已经把庇护与生产的选择、每座星灯的调谐顺序和剩余储备全部公开。" +
                        (StormReliefAll?"三位原来的证人还把你捐出的热汤送到了受寒的居民手中。":"") + "\n\n" :
                        Has("storm_started")?"莉雅：星雨还没有结束。三片地区的选择不由城墙自动替代；请到公约议事台确认每一笔公共储备。\n\n":"") +
                    (Has("afterlight_complete") ?
                    "莉雅：你没有让终章变成另一本落满灰尘的历史书。三地公共站点已经接通，普通人可以使用它们，也知道谁来维护。\n\n" :
                    "莉雅：厄洛的决定过去了，村庄仍然需要真正的公共道路。公会南侧设立了余光重建议事台；把星根设施接回居民身边吧。\n\n") +
                    "冒险尚未结束。旅店有热汤，密林里仍有星晶，大家都在等你。",
                    (Has("storm_complete")?
                        "Lyra: The first storm proved three living regions can choose how to distribute public power. The shelter and utility allocations, physical lamp sequence and remaining reserve are all on the public record." +
                        (StormReliefAll?" The original three witnesses also delivered your hot meals to neighbors suffering in the cold.":"") + "\n\n" :
                        Has("storm_started")?"Lyra: The starstorm is not over. The citadel cannot decide each region's power needs by decree. Inspect the civic allocations at the council.\n\n":"") +
                    (Has("afterlight_complete") ?
                    "Lyra: You did not let the ending become another dusty history book. All three public sites now work, and ordinary people know who maintains them.\n\n" :
                    "Lyra: Orrin's decision is behind us, but villages still need real public routes. Find the afterlight reconstruction council south of the guild and rebuild those civic sites.\n\n") +
                    "There is warm stew at the inn, starcrystal in the woods, and more living to do.",
                    C("开始新的远征", "Another expedition", "routes"),
                    C("回顾结局", "Revisit the ending", "epilogue"),
                    C("再见", "Goodbye", "close")); AddRoadCharterGuildChoice(); return; }
                int next = save.stage + 1;
                if (save.stage < 3 && Has("core_" + next))
                    Dialog(o.name, o.en, (Has("witness_complete_"+next) ?
                        new[]{"","莫芽的证词已经到我手里：森林不是敌人，它失去了本该属于自己的自愈力。\n\n","阿菱记录的渡口孩子不再是货运损失的空白项。\n\n","维恩证明矿工停机是救命，而不是违反配额。\n\n"}[next] : "") +
                        "星核暂存进公会封库，堡垒却已经催促下一批供能。镇里的井水比昨天更浅。把下一片遗迹中的原始记录带回来吧：我们需要的不只是能源，还要知道失衡始于何处。", (Has("witness_complete_"+next) ?
                        new[]{"","Moya's testimony is here. The forest was wounded, not our enemy.\n\n","Arin's children at the crossing will no longer be counted as missing freight.\n\n","Venn proves the miners' shutdown was a safety measure, not misconduct.\n\n"}[next] : "") +
                        "The core is safe in the guild vault, but the citadel is already demanding more power. Our town's wells are lower than yesterday. Bring back the next region's records, not just its core. We must learn where the balance broke.", C("交回星核 · 开启下一章", "Deliver the starcore · next chapter", "deliver"), C("稍后再来", "Later", "close"));
                else Dialog(o.name, o.en, save.stage == 3 ? "三枚星核已送入堡垒的调谐网。屏障越亮，城外的树林与河道就越虚弱。去见厄洛，问问他究竟在保护谁。\n\n杏、老潮和诺尔能提供药品、食物与共用水泵；远岸的原始证词还可能证明，星核不止有筑墙一种用法。" : "先去荆棘密林，把巡路队的侦察员平安带回来。那里的黑荆巨兽正守着第一处星根伤口；查清原因，再取回星核。按 Tab 查看任务手帐。", save.stage == 3 ? "Three cores now feed the citadel's tuning network. Its shield brightens as rivers and trees beyond the wall weaken. Confront Orrin and ask whom he is protecting.\n\nAn, Old Tide and Noll can offer medicine, food and shared pumps. Original farshore records might prove that walls are not the only use for starcores." : "Start in Thornwood and bring the missing scout home. The Briar Beast guards the first wounded starroot anchor. Find out why, then recover the core. Tab opens your quest journal.", C("查看冒险路线", "Choose a route", "routes"), C("我出发了", "On my way", "close"));
                return;
            }
            if (kind == "rest")
            {
                // Completing an actual timed escort must not allow a free
                // 45-second world jump without advancing the mission clock.
                if (FieldWorkPreventsRest || !SafeTravel)
                {
                    Dialog(o.name, o.en,
                        FieldWorkPreventsRest ?
                            "仍有现场救援、调谐或运送行动进行中。先完成或到原处主动取消，再来炉火旁休息。" :
                            "敌人或攻击仍在附近！确认周围安全后，才可以安心休息。",
                        FieldWorkPreventsRest ?
                            "A field rescue, circuit or delivery is active. Finish or abort it at the station before resting." :
                            "Hostiles or incoming attacks are too close. Clear the area before resting.",
                        C("继续现场行动", "Return to field duty", "close"));
                    return;
                }
                Dialog(o.name, o.en, "旅店的炉火永远为冒险者留着。休息推进 45 秒，回复生命与耐力，田里的作物也会继续长大。", "The inn keeps a fire for travelers. Rest for 45 seconds to restore health and energy; crops keep growing.", C("休息", "Rest", "rest"), C("继续冒险", "Keep adventuring", "close")); return;
            }
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
                List<Choice> list = new List<Choice> { C("强化装备 · 木"+TemperUpgradeTimber+" 晶"+TemperUpgradeCrystal+" 零件"+TemperUpgradeParts, "Upgrade · "+TemperUpgradeTimber+" timber / "+TemperUpgradeCrystal+" crystal / "+TemperUpgradeParts+" parts", "upgrade"), C("制作猎弓 · 木4 零件2", "Craft bow · 4 timber / 2 parts", "bow"), C("制作星火杖 · 晶4 零件3", "Craft staff · 4 crystal / 3 parts", "staff") };
                if (!Has("ally_3")) list.Add(C("支线：交付零件 ×5", "Commission: deliver 5 parts", "ally3"));
                if(save.profession==0&&save.weapon==0)
                {
                    if(SwordLessonActive)
                        list.Add(C(SwordLessonVeteran?
                            "中止五箭精英训练":"中止三箭练习",
                            SwordLessonVeteran?
                            "Abort five-arrow drill":"Abort three-arrow practice",
                            "swordlesson_abort"));
                    else if(!SwordLessonComplete)
                        list.Add(C("剑盾弹反试炼 · 三箭","Sword-guard trial · three arrows","swordlesson_start"));
                    else list.Add(C(SwordLessonVeteranComplete?
                        "五箭精英复练 · 无重复奖励":"五箭精英考核 · 首通技能点+1",
                        SwordLessonVeteranComplete?
                        "Replay five-arrow drill · no rewards":"Advanced five-arrow drill · first-clear talent",
                        "swordlesson_veteran_start"));
                }
                list.Add(C("再见", "Goodbye", "close"));
                Dialog(o.name, o.en, "短剑快，猎弓远，法杖能让星火在敌人间爆开。强化装备提升生命与攻击。\n\n我还需要五个零件修好镇里的水泵。堡垒里有那么多机器，小镇却没有一口能用的井。"+(MineConvoyComplete?"\n\n最后一班矿工安全归来，还带回能再次使用的木材；每次装备强化已额外节省一份木料。":"")+
                    (save.profession==0&&save.weapon==0?"\n\n战士的Q技能会举起剑盾；正面迎接飞箭，消耗"+SwordGuardArrowEnergy+"耐力可弹回。正面挡下敌人的近身扑击则需"+SwordGuardMeleeEnergy+"耐力，并获得一次强化反击；侧后方和冷却期间只有通常的减伤效果。":""),

                    "Swords are quick, bows reach far, and staves burst starfire between enemies. Upgrades improve health and damage.\n\nI also need five parts to repair our water pump. So many machines in the citadel, and no working well in town."+(MineConvoyComplete?"\n\nThe rescued miners brought reusable timber home. Every equipment upgrade now needs one less timber.":"")+
                    (save.profession==0&&save.weapon==0?"\n\nYour Q raises a sword guard: face arrows to reflect them for 7 energy, or parry a frontal melee lunge for 9 energy and a powered riposte. Rear hits and contacts during cooldown only receive ordinary guard mitigation.":""), list.ToArray()); return;
            }
            if (kind == "cook") { Dialog(o.name, o.en, "暖潮炖汤：鱼1、药草2。回复55生命与耐力，增加攻击90秒。F喝汤。\n\n星薄荷凉茶：药草2、星晶1。T喝茶，抗暑抗寒并增加耐力回复180秒。", "Stew: 1 fish, 2 herbs. Heals 55, refills energy and boosts damage for 90 seconds. F drinks it.\n\nTea: 2 herbs, 1 crystal. T drinks it for climate protection and faster energy recovery, 180 seconds.", C("制作炖汤", "Cook stew", "cook"), C("买炖汤 · " + ShopStewCost + " 硬币", "Buy stew · " + ShopStewCost + " coins", "buymeal"), C("买木材 ×4 · " + ShopTimberCost + " 硬币", "Buy timber ×4 · " + ShopTimberCost + " coins", "buywood"), C("制作凉茶", "Brew tea", "tea"), C("再见", "Goodbye", "close")); return; }
            if (kind == "scout")
            {
                if (Has("scout_saved")) { Notify("侦察员已经安全离开。", "The scout reached safety."); return; }
                if (save.kills[1] < 4) { Dialog(o.name, o.en, "路上至少还有四只狂暴的怪物。帮我清理道路，我会告诉你星核的入口。", "At least four beasts block the escape route. Clear them and I will open the path to the core.", C("我来解决", "I will clear the road", "close")); return; }
                Dialog(o.name, o.en, "谢谢你，我跟着荆棘下的光找到了星根的伤口。星核本该稳住森林，如今却被堡垒的屏障不断抽走能量。我不能靠近祭坛，但会把找到的记录交给莉雅。你也要平安回来。", "Thank you. I followed the light under the thorns to a wounded starroot anchor. Its core once balanced this forest; now the citadel's shield is draining it. I cannot reach the altar, but I will bring these notes to Lyra. Please come home safely.", C("护送他离开", "Send him home safely", "scout")); return;
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
                // Record the region's history even when full, but keep its
                // one-time physical treasure available until both resources
                // can actually fit. The lore flag is unrelated to pickup.
                bool unclaimed = !Has("treasure_" + id);
                bool collected = unclaimed && save.inventory[1] <= 9997 &&
                    save.coins <= 999979;
                if (collected)
                {
                    CreditCoins(20); CreditInventory(1, 2);
                    Flag("treasure_" + id); sound = "reward";
                }
                string rewardZh = collected ? "\n\n首次获得：硬币 ×20，星晶 ×2。" :
                    unclaimed ? "\n\n遗物补给还在这里。留出20硬币和2星晶的容量后回来领取。" :
                    "\n\n遗物补给已领取。";
                string rewardEn = collected ? "\n\nFirst discovery: +20 coins, +2 crystals." :
                    unclaimed ? "\n\nRelic supplies remain here. Free room for 20 coins and 2 crystals, then return." :
                    "\n\nRelic supplies already collected.";
                Dialog(l.title, l.en, l.zh + rewardZh, l.text + rewardEn,
                    C("记入手帐", "Record it in the journal", "close")); return;
            }
            if (kind == "herb" || kind == "wood" || kind == "ore")
            {
                if (Has("node_" + o.id)) { Notify("已经采集过了。回镇休息后资源会更新。", "Already gathered. Rest in town to renew resources."); return; }
                int item = kind == "herb" ? 2 : kind == "wood" ? 0 : 1;
                // One-time gathering yields exactly three items (and ore
                // grants three parts). Do not permanently consume this node
                // unless BOTH resource stacks can accept the entire pickup.
                if (!HasInventoryRoom(item, 3) ||
                    kind == "ore" && !HasInventoryRoom(4, 3)) return;
                Flag("node_" + o.id);
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
                    Dialog("星核祭坛", "Starcore altar", "星核的光安静下来。它不是怪物身上的战利品，而是旧星根网络的一处调谐核心。先交给莉雅保管；它最终要为城墙还是土地供能，仍需要你找到更多证据。", "The starcore grows still. It is not a trophy, but a regulator of the old living lattice. Bring it to Lyra for safekeeping. Its final purpose—shield or soil—must wait until you know more.", C("返回余烬镇", "Return to Emberfall", "goto0"), C("继续探索", "Keep exploring", "close")); sound = "reward"; return;
                }
                if (enemies.Exists(e => e.boss)) { Notify("留意红色攻击预告，闪避后反击。", "Watch the red attack warnings. Dodge, then strike."); return; }
                BossData b = data.bosses[save.room - 1];
                if (save.room == 4 && LocalCeasefireEligible)
                {
                    Dialog(b.name,b.en,
                        "厄洛低头看着三份原始星核账、三位居民签署的证词，还有莱娜、伊恩、奥姆刚刚回到灯下的避难名单。你没有要求他让城市在黑暗里等待。\n\n“如果你真的能把三个星核供给接入公共避难所，也说服了镇民共同维护，那么我愿意停止这道旧指令。但决定权必须归还给活着的人。”",
                        "Orrin reads the original core audit, three signed civilian testimonies, and the emergency roster of Lena, Ian and Orme. You did not ask his children to wait alone in darkness.\n\n“If you can keep the cores serving shared shelters, and the townspeople will maintain them, I can retire this obsolete order. The decision must return to those living with its cost.”",
                        C("出示撤离名单与同伴承诺 · 民用停战",
                            "Present evacuation roster and allied support · civilian ceasefire","blackout_ceasefire"),
                        C("仍然挑战统领","Fight Orrin","boss"),
                        C("再做准备","Prepare first","close"));return;
                }
                if (save.room == 4 && Proofs == 3 && save.frontierEnding == 3)
                {
                    Dialog(b.name, b.en, "三份原始记录摆在厄洛面前：港口的水位、沙海的调度、高原被封存的名字。你也带来了一个已经开始运行的三方协定。\n\n他不再能说除此之外别无选择。你可以提出让他放下武器，也可以坚持决斗。", "The tide log, allocation ledger and sealed names lie before Orrin. You also bring a working covenant between three regions.\n\nHe can no longer claim there is no alternative. Ask him to stand down, or insist on a duel.", C("用证词与协定说服他", "Present the records and covenant", "parley"), C("仍然挑战统领", "Challenge the warden", "boss"), C("准备一下", "Prepare", "close")); return;
                }
                Dialog(b.name, b.en, save.room == 4 ? (Has("triad_complete") ? "你带来了三核审计的原始记录。厄洛终于看到森林、河流与矿工分别失去的份额。他沉默了一阵，却仍握住武器：如果停下这道屏障，谁来保护里面的孩子？让我看看你的勇气，而不是另一道命令。\n\n" : "") + "厄洛：我见过屏障熄灭的那一晚。孩子们冻得发抖，我一个一个把他们抱进避难室。我发誓再也不让城墙倒下。\n\n你说星核在抽干城外的村庄？我知道。但现在停机，又该让谁在下一场灾害里死去？如果你有能真正运转的办法，就让我看见；否则先打败我。" : "守护者仍依照失控的星根旧指令驱赶靠近者。地面预警标出它即将攻击的区域。闪避后反击，并查清这一带的机关为何不能停止。", save.room == 4 ? (Has("triad_complete") ? "Your original audit lists the forests, rivers and miners stripped of their share. Orrin sees the accounts and falls silent, but grips his weapon: Who protects the children inside if the shield stops? Show me courage, not another command.\n\n" : "") + "Orrin: I remember the night the lights died. I carried freezing children into shelter, one by one. I swore no wall would fall again.\n\nYou say the cores are draining the villages? I know. But if I stop the shield now, who dies in the next storm? Show me an alternative that actually works—or defeat me first." : "The guardian still obeys an unstable ancient starroot command. Ground warnings reveal its next real contacts. Dodge and counter, then investigate why the regional machinery cannot stop.", C(save.room == 4 ? "为边境而战" : "开始挑战", save.room == 4 ? "Fight for the frontier" : "Challenge the guardian", "boss"), C("准备一下", "Prepare first", "close"));
            }
        }
        // Progress and rewards must already match v1/v2 save caps BEFORE a
        // save is written, not be quietly truncated at the next Load().
        // Use long arithmetic for large accumulated Abyss payouts.
        private static int CappedReward(int current, int reward, int maximum)
        {
            return (int)Math.Max(0L, Math.Min((long)maximum,
                (long)current + Math.Max(0L, (long)reward)));
        }
        private void CreditCoins(int amount) { save.coins = CappedReward(save.coins, amount, 999999); }
        private void CreditShards(int amount) { save.shards = CappedReward(save.shards, amount, 99999); }
        private void CreditSkillPoints(int amount) { save.skillPoints = CappedReward(save.skillPoints, amount, 99); }
        private void CreditTea(int amount) { save.tea = CappedReward(save.tea, amount, 999); }
        private void CreditInventory(int index, int amount)
        { save.inventory[index] = CappedReward(save.inventory[index], amount, 9999); }
        private bool Spend(int wood, int crystal, int herb, int fish, int gear, int coins = 0)
        {
            int[] costs = { wood, crystal, herb, fish, gear };
            for (int i = 0; i < 5; i++) if (save.inventory[i] < costs[i]) { Notify("材料不足。查看背包 I，探索、采集或钓鱼补充。", "Not enough materials. Check I; explore, gather or fish."); return false; }
            if (save.coins < coins) { Notify("硬币还不够。", "Not enough coins."); return false; }
            for (int i = 0; i < 5; i++) save.inventory[i] -= costs[i]; save.coins -= coins; revision++; return true;
        }
        // Inventory quantities are capped in SaveData v2 on reload. Reject a
        // repeatable purchase before Spend so a full stack never silently
        // consumes gold or crafting materials for discarded stock.
        private bool HasInventoryRoom(int index, int quantity)
        {
            if (save.inventory[index] <= 9999 - quantity) return true;
            Notify("背包数量已达上限。请先腾出物品空间后再领取。",
                "Inventory stack is full. Free space before collecting more.");
            return false;
        }
        // Inn and upgraded field camps must advance the same transient
        // simulation clocks during a time skip. A single long rest is not
        // twenty/forty-five free frames of ongoing attacks or spell casts.
        private void ResolveRestTimeSkip(float elapsed)
        {
            save.totalTime += elapsed;
            // Twenty minutes of camp rest (or forty-five at the inn) is a
            // real jump in world tide time. Do NOT defer collision recovery
            // to the next rendered Tick: companions/enemies can otherwise
            // remain inside flooded shoals throughout the post-rest dialogue
            // and before the next saved snapshot.
            TickTideTransition();
            CancelAction();
            attackCooldown = dashCooldown = skillCooldown = dashTime = 0;
            companionCooldown = companionAttack = 0;
            companionAction = null;
            ClearFrost();
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = projectiles[i];
                p.life -= elapsed;
                if (p.life <= 0) projectiles.RemoveAt(i);
            }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect effect = effects[i];
                effect.life -= elapsed;
                if (effect.life <= 0) effects.RemoveAt(i);
            }
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                Hazard h = hazards[i];
                h.timer -= elapsed;
                if (h.timer <= 0) hazards.RemoveAt(i);
            }
            combo = 0; comboTimer = riposteTime = 0;
            guardTime = playerHitTime = hitStop = 0;
            invulnerable = dodgeEscapeTime = playerFlash = impactShake = 0;
            perfectDodgeArmed = false; dashOrigin = new Point();
        }
        private bool FieldWorkPreventsRest
        {
            get { return SwordLessonActive || FrostSignalActive || MineConvoyActive || FarCourierActive || CivicResponseActive || AfterlightWatchActive || OuterPatrolActive || stormTuning ||
                afterlightChargeRegion != 0 || blackoutActive || reliefMissionRoom != 0 ||
                TideSalvageActive || FloodReliefActive || LastDirectiveWave > 0 || CivicConduitActive ||
                // A lull between scripted enemy waves is not a break in the
                // physical trial. Resting there would skip its capture clock,
                // red warnings and challenge phases for free.
                GlasswindSite != 0 || TemperActive || TriadDefenseActive ||
                witnessDefenseRoom != 0 || commonsRelayRoom != 0 ||
                RoadwardenTrialRoom != 0 || RootHarmonyRoom != 0 ||
                SkywatchRoom != 0 || AtlasEchoRoom != 0; }
        }
        public void Choose(int index)
        {
            if (mode != "dialog" || conversation == null || index < 0 || index >= conversation.choices.Length) return;
            if(Dying){Close();return;}
            string action = conversation.choices[index].action; Close();
            if (ChooseSwordLesson(action)) return;
            if (ChooseFrostSignal(action)) return;
            if (ChooseMineConvoy(action)) return;
            if (ChooseFloodArchive(action)) return;
            if (ChooseRootsong(action)) return;
            if (ChoosePolish(action)) return;
            if (ChooseExpansion(action)) return;
            if (ChooseLastDirective(action)) return;
            if (ChooseCivicConduit(action)) return;
            if (action == "close") return;
            if (action.StartsWith("goto")) { Enter(int.Parse(action.Substring(4))); return; }
            if (action == "routes") { Talk(data.maps[0].objects[5]); return; }
            if (action == "rest")
            {
                if (FieldWorkPreventsRest || !SafeTravel)
                {
                    Notify("请先完成现场任务并清除周围敌人或攻击，再安全休息。",
                        "Finish active fieldwork and clear nearby threats before resting.");
                    return;
                }
                save.health = save.maxHealth; save.energy = 100;
                ResolveRestTimeSkip(45f);
                save.flags.RemoveAll(f => f.StartsWith("node_")); save.heat = save.cold = 0; Notify("炉火温暖。生命与耐力已恢复。", "Warm by the fire. Health and energy restored."); revision++; return;
            }
            if (action == "scout" && save.kills[1] >= 4 && !Has("scout_saved")) { Flag("scout_saved"); CreditCoins(20); Notify("侦察员安全离开。星核入口已开启。", "Scout rescued. The altar path is open."); }
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
                // Ready crops do not disappear when either herb or seed stack
                // is full. Keep plantedAt so the same mature garden can be
                // harvested after the player spends some materials.
                if (!HasInventoryRoom(2, harvest) || !HasInventoryRoom(7, 2)) return;
                save.inventory[2] += harvest; save.inventory[7] += 2; save.plantedAt = -1; save.coins = Math.Min(999999, save.coins + 8); revision++; sound = "reward"; Notify("收获：月光草 ×" + harvest + "，种子 ×2，硬币 +8。", "Harvest: " + harvest + " Moonherbs, 2 seeds, 8 coins."); return;
            }
            if (action == "seeds" && HasInventoryRoom(7, 3) && Spend(0, 0, 0, 0, 0, 5)) { save.inventory[7] += 3; Notify("种子 ×3", "Seeds ×3"); }
            if (action == "upgrade")
            {
                if (save.upgrades >= 5) { Notify("装备已经满级。", "Your equipment is fully upgraded."); return; }
                if (Spend(TemperUpgradeTimber, TemperUpgradeCrystal, 0, 0, TemperUpgradeParts)) { save.upgrades++; save.maxHealth += 20; save.health = save.maxHealth; Notify("强化：最大生命 +20，攻击 +4。", "Upgraded: +20 maximum health, +4 damage."); sound = "reward"; }
            }
            if (action == "bow" || action == "staff")
            {
                int w = action == "bow" ? 1 : 2;
                if (Has("weapon_" + w)) { Equip(w); Notify("已装备。", "Equipped."); return; }
                if (Spend(w == 1 ? 4 : 0, w == 2 ? 4 : 0, 0, 0, w == 1 ? 2 : 3)) { Flag("weapon_" + w); Equip(w); sound = "reward"; Notify("制作成功！背包 I 可以切换武器。", "Crafted! Switch weapons in your inventory, I."); }
            }
            if (action == "cook" && HasInventoryRoom(6, 1) && Spend(0, 0, 2, 1, 0)) { save.inventory[6]++; sound = "reward"; Notify("暖潮炖汤 ×1", "Tide stew ×1"); }
            if (action == "buymeal" && HasInventoryRoom(6, 1) && Spend(0, 0, 0, 0, 0, ShopStewCost)) { save.inventory[6]++; Notify("暖潮炖汤 ×1", "Tide stew ×1"); }
            if (action == "buywood" && HasInventoryRoom(0, 4) && Spend(0, 0, 0, 0, 0, ShopTimberCost)) { save.inventory[0] += 4; Notify("木材 ×4", "Timber ×4"); }
            if (action == "fish" && HasInventoryRoom(3, 1))
            { fishTime = fishCursor = 0; fishHits = fishMisses = 0; mode = "fish"; }
            if (action == "deliver" && save.stage < 3 && Has("core_" + (save.stage + 1)))
            { save.stage++; CreditCoins(40); CreditInventory(6, 1); CreditInventory(0, 3); CreditInventory(1, 2); revision++; sound = "reward"; Notify("新章节开启！获得硬币、材料与一份炖汤。", "New chapter! Coins, materials and a warm stew received."); }
            if (action.StartsWith("ally"))
            {
                int id = int.Parse(action.Substring(4));
                if (Has("ally_" + id)) return;
                if (Spend(0, 0, id == 1 ? 6 : 0, id == 2 ? 3 : 0, id == 3 ? 5 : 0))
                { Flag("ally_" + id); CreditCoins(40); CreditInventory(6, 1); CreditSkillPoints(1); sound = "reward"; Notify("支线完成！获得同伴支持、40 硬币和 1 技能点。", "Commission complete! An ally, 40 coins and 1 skill point."); }
            }
            if (action == "boss")
            {
                if (blackoutActive) return;
                if (save.room == 0 || !GateOpen || GateBarrierActive || TriadDefenseActive || enemies.Exists(e => e.boss) || Has("boss_" + save.room)) return;
                BossData b = data.bosses[save.room - 1];
                float bossHealth = save.room == 4 && Has("triad_complete") ? b.hp * .85f : b.hp;
                enemies.Add(CreatePlacedEnemy(++enemyId, 3, new Point(29, 9), bossHealth, true));
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
        {
            if (weapon < 0 || weapon > 2 || (weapon > 0 && !Has("weapon_" + weapon)))
                return;
            // Selecting the already-equipped icon must not discard an active
            // attack animation or the player's buffered Q input.
            if (save.weapon == weapon) return;
            // Combos belong to a single uninterrupted weapon sequence.
            // Changing loadouts cancels its current action AND any stored
            // third-hit bonus; reselecting the same weapon above remains inert.
            CancelAction(); combo = 0; comboTimer = 0;
            save.weapon = weapon; revision++;
        }
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
            if (fishHits >= 3)
            {
                // Normal play enters this minigame only when a fish slot was
                // available. Keep the final reward safe if a developer or
                // future UI changes the inventory during the reel sequence.
                save.inventory[3] = Math.Min(9999, save.inventory[3] + 1);
                save.coins = Math.Min(999999, save.coins + 4);
                BountyEvent(2, 1); Close(); revision++;
                Notify("钓到银鳍鱼 ×1！", "Caught a Silverfin!");
            }
            else if (fishMisses >= 3) { Close(); Notify("鱼游走了。没有损失，再试一次吧。", "The fish slipped away. Nothing lost; try again."); }
        }
        private void FinalConversation()
        {
            List<Choice> choices = new List<Choice> { C("归还星核 · 让大地恢复", "Return the cores · restore the land", "ending1"), C("保留屏障 · 守住堡垒", "Keep the shield · secure the citadel", "ending2") };
            if (Allies == 3) choices.Add(C("重建边境 · 与居民一起改变", "Rebuild the frontier · unite the towns", "ending3"));
            choices.Add(C("先完成镇民的委托", "Finish the town commissions first", "goto0"));
            Dialog("厄洛 · 星卫统领", "Orrin · Starwarden",
                (Has("blackout_complete") ?
                    (Has("blackout_peace") ?
                        "厄洛没有被击倒。他看到莱娜、伊恩和奥姆安全回到灯下的名单，主动解除了旧屏障的军用指令。\n\n" :
                        "莱娜、伊恩和奥姆在那一夜被你亲自带回避难所。厄洛如今知道，还有人愿意亲手让旧城安然过渡。\n\n") : "") +
                (AllWitnessesFiled ? "莫芽、阿菱和维恩的三份署名证词已摆到厄洛面前。那些活生生的人不再是能量账上看不见的数字。\n\n" : "") +
                (Has("triad_complete") ? "莉雅公开的三核审计摆在两人之间。守护孩子与守护城外的村庄，不能再被假装成彼此无关的数字。\n\n" : "") + "厄洛放下武器。三枚星核曾经调谐星根，让河流、田地与灯火彼此有余地。现在城墙的安全来自城外不断失去的生机。\n\n归还星核能让土地复苏，但危险需要所有人共同承担；保留屏障能守住眼前的人，也要正视外界的代价。" + "\n\n" + (Allies < 3 ? "杏、老潮和诺尔若愿意共同送药、摆渡、修水泵，第三条路也许才会成为现实。你可以先回镇上。" : "杏已准备送药，老潮愿意摆渡补给，诺尔带来了共同维护的水泵。另一条路已经有人愿意亲手开始建设。"), (Has("blackout_complete") ?
                    (Has("blackout_peace") ?
                        "Orrin was not struck down. He saw Lena, Ian and Orme safe on the evacuation roster and retired the obsolete shield command himself.\n\n" :
                        "You led Lena, Ian and Orme home during the blackout. Orrin knows someone else is willing to carry the risk of a safer transition.\n\n") : "") +
                (AllWitnessesFiled ? "Moya, Arin and Venn signed their own testimony. Their lives can no longer be erased as blank numbers in the energy accounts.\n\n" : "") +
                (Has("triad_complete") ? "Lyra’s public three-core audit stands between you. Protecting children inside the wall can no longer erase people beyond it.\n\n" : "") + "Orrin lowers his weapon. Three cores once balanced the roots between rivers, fields and lights. Now the shield's safety is drawn from lives beyond its wall.\n\nReturning the cores may heal the land, though its people must face danger together. Keeping the shield protects those inside but continues to exact a price outside." + "\n\n" + (Allies < 3 ? "With An, Old Tide and Noll on your side, a third path might become practical. You may return to town first." : "An brings medicine, Old Tide brings ferry supplies and Noll has prepared public pumps. The alternative already has people willing to build it."), choices.ToArray());
        }
        public void Epilogue()
        {
            string[] zh = { "", "《重新生长》\n\n你把星核重新接回森林、河流和矿脉的调谐锚点。堡垒屏障熄灭，夜空不再只有一道由别人支付代价的光。河水缓缓复流，荆棘间长出第一条新路。\n\n危机并未一夜消失。莉雅在公会地图上标出公共避难所，杏带药箱走访每个村庄。厄洛离开城墙，第一次和居民一起种下防风树。\n\n边境学会了带着风险生活，而不是把风险推给看不见的人。", "《高墙之内》\n\n你保留屏障，也迫使堡垒向那些曾被算作“外围损失”的人打开城门。更多孩子有屋顶过夜，更多老人能在灯下安心休息。\n\n但城外的河水继续变浅，村庄还在等待下一批救援。莉雅开始公布每次屏障加压的真实资源账目，老潮继续摆渡不愿离开故土的人。\n\n这份安全是真实的，却仍有未结清的代价。改变的第一步，是承认谁在付钱。", "《我们的边境》\n\n杏把药物送到不在地图上的村庄，老潮把渡船改成补给船，诺尔将一部分堡垒装置改成公共水泵。星核的力量逐渐回到居民共同维护的道路上。\n\n厄洛拆下第一段城墙，和昔日的守卫一道修桥。他没有忘记那些被屏障救下的人，也不再独自替城外人决定命运。\n\n世界没有突然完美，但每个人终于有机会在地图上画出自己的归途。\n\n冒险者，欢迎回家。" };
            string[] en = { "", "GROWING AGAIN\n\nYou return the cores to the forest, river and mine anchors. The citadel shield fades; the sky no longer belongs to a single light paid for by others. Rivers start to flow, and a new road grows through the thorns.\n\nThe crisis does not disappear overnight. Lyra marks shared shelters on the guild maps. An carries medicine between villages. Orrin walks beyond his wall and helps plant windbreak trees.\n\nThe frontier learns to face danger without passing its cost to people it cannot see.", "WITHIN THE WALLS\n\nYou preserve the shield and force the citadel to open its gates to people once counted as losses outside. More children sleep under shelter; more elders can rest beneath its lights.\n\nBut the outer rivers still fall, and villages continue waiting for aid. Lyra publishes the price of every shield surge. Old Tide keeps ferrying those who refuse to abandon their homes.\n\nThis protection is real. So is the unsettled debt. The first change is admitting who pays.", "OUR FRONTIER\n\nAn carries medicine to villages missing from official maps. Old Tide turns his ferry into a supply boat. Noll rebuilds part of the citadel's machinery as public pumps. The power of the cores returns gradually to roads maintained by the people who use them.\n\nOrrin takes down the first section of wall and joins the former guards in repairing bridges. He has not forgotten those he saved, but no longer decides the future of everyone outside.\n\nThe world is not suddenly perfect. Everyone finally has a chance to draw a road home.\n\nWelcome home, traveler." };
            int ending = (int)Clamp(save.ending, 1, 3);
            // The farshore covenant governs infrastructure; the main ending
            // independently decides how the cores power the region.
            if (save.frontierEnding > 0)
            {
                string[] farZh = { "", "与此同时，远岸的共同水渠把水送向曾经被遗漏的田地。", "远岸灯网开始由三方轮流值守，每次供能都有记录。", "远岸开放星路由三方共同治理，能源账目交由居民审阅。" };
                string[] farEn = { "", "Meanwhile, shared farshore canals deliver water to formerly neglected fields.", "Farshore navigation lights now run on a recorded rotation shared by all three communities.", "The farshore's open starroads are governed jointly, with public energy accounts." };
                zh[ending] += "\n\n" + farZh[save.frontierEnding];
                en[ending] += "\n\n" + farEn[save.frontierEnding];
            }
            if (AllWitnessesFiled)
            {
                zh[ending] += "\n\n密林的莫芽、遗迹的阿菱和矿场的维恩把亲笔署名的证词留给了公会；新的公共档案终于记得谁在修复土地、谁在等候过河、谁应该安全下班。";
                en[ending] += "\n\nMoya of Thornwood, Arin of the ruins and Venn of Ironreach left signed testimony with the guild. Public records now remember who healed the soil, who waited to cross and who deserved to go home after work.";
            }
            if(Has("storm_orrin_open")||Has("storm_orrin_guard"))
            {
                zh[ending] += Has("storm_orrin_open")?
                    "\n\n厄洛在第一次星雨后公开签署，将剩余公用储备开放给余烬镇的避难疗养网络；城墙的光终于照向城外。":
                    "\n\n厄洛在第一次星雨后公开签署，将剩余公用储备留作堡垒平民的应急照明，而不再让军方单独掌控灯源。";
                en[ending] += Has("storm_orrin_open")?
                    "\n\nAfter the first storm, Orrin signed the unused civic reserve into Emberfall's open refuge network. Light finally reached outside the old walls.":
                    "\n\nAfter the first storm, Orrin signed the unused reserve into civilian emergency lighting inside the citadel rather than military control.";
            }
            if (StormReliefAll)
            {
                zh[ending] += "\n\n在星雨的寒夜，莫芽、阿菱和维恩又将你亲自捐出的三份热汤送给最需要的居民，公用避难所因此能继续照顾后来的旅人。";
                en[ending] += "\n\nDuring the freezing storm, Moya, Arin and Venn delivered all three real meals you donated. The public shelter can now care for travelers who arrive after the emergency.";
            }
            if (Has("storm_started"))
            {
                zh[ending] += "\n\n第六章的第一场星雨让公共能源第一次经受真实检验：八单位储备已由三地亲自决定分配 "+StormBudgetSpent+" 单位。" +
                    (Has("storm_complete")?
                        "余烬镇的三座公共灯站已由你亲自按顺序调谐，公约经受住风暴考验，剩余的 "+StormBudgetLeft+" 单位仍供城镇应急。" :
                        "真正的公约尚待完成，街道上的星雨不能只凭宣言就平息。");
                en[ending] += "\n\nThe first starstorm tested civic energy governance: the living regions themselves assigned "+StormBudgetSpent+" out of eight reserve units." +
                    (Has("storm_complete")?
                        " You physically tuned three town lamps in the order determined by your original ending; the charter survived, with "+StormBudgetLeft+" units still available for Emberfall emergencies." :
                        " The first-storm charter is not complete, and promises alone cannot calm the live storm.");
            }
            if(HearingOrrinResponsibility!=0)
            {
                zh[ending]+=HearingOrrinResponsibility==1?
                    "\n\n厄洛最后亲自将两份热汤交给公开医护，让守卫的后勤变成任何人都能使用的避难补给。":
                    "\n\n厄洛最后亲自交出两份实用零件，让昔日守卫成为公开巡路志愿队。";
                en[ending]+=HearingOrrinResponsibility==1?
                    "\n\nOrrin personally handed over two real meals to civilian medics, turning former military logistics into public relief.":
                    "\n\nOrrin personally transferred two usable parts to volunteer roadwardens, sharing responsibility for future journeys.";
            }
            if(HearingComplete)
            {
                zh[ending]+="\n\n第七章的听证不再由厄洛或公会替居民宣布结论。莫芽、阿菱和维恩各自签下不同誓言，三地的现场碑文成为可读的公共记录。"+
                    (HearingVerdict==1?"你最终选择开放公民医疗服务；余烬镇新设的公共医护灯真的开始照顾受伤者。":
                        "你最终选择由民众保有应急能源；余烬镇公民耐力灯为下一次救援保存体力。");
                en[ending]+="\n\nChapter Seven did not allow Orrin or the guild to vote for others. Moya, Arin and Venn signed independent regional oaths; their words remain visible at the original worksites. "+
                    (HearingVerdict==1?"You ratified open civic medical access; Emberfall's public healing lantern actually operates.":
                        "You ratified publicly held emergency reserves; Emberfall's civic energy lantern supports future rescue crews.");
            }
            if (Has("afterlight_started"))
            {
                zh[ending] += "\n\n余光重建已经启动，三地已有 "+AfterlightRestored+"/3 座公共站恢复运行。" +
                    (Has("afterlight_complete") ? "公会公布了三地居民共建的维护公约；你的结局真正改变了接下来的生活。" :
                        "原来的结局依然有效，但重建不是瞬间完成的神迹。");
                en[ending] += "\n\nAfterlight reconstruction has begun: "+AfterlightRestored+"/3 regional public sites now operate." +
                    (Has("afterlight_complete") ? " The guild published the residents' own charter for maintaining them; your ending now changes daily life." :
                        " The original ending stands, but lasting repair is not an overnight miracle.");
            }
            if (Has("blackout_complete"))
            {
                zh[ending] += "\n\n那场长夜不再只是一份电力事故报告：莱娜、伊恩和奥姆的避难名单仍挂在堡垒的公共灯下。" +
                    (Has("blackout_peace") ? "厄洛因为亲眼看见可行的撤离和镇民承诺而主动放下武器，不必让另一个孩子在决斗里失去亲人。" : "不论经历了战斗还是谈判，居民的生命都先得到了保障。");
                en[ending] += "\n\nThe blackout is no longer just a power-failure statistic. Lena, Ian and Orme's names remain on the public shelter roster." +
                    (Has("blackout_peace") ? " Orrin saw a workable evacuation backed by actual residents and stood down without another family losing someone in a duel." : " Whatever followed, the civilians were brought to safety before anyone chose the cores' fate.");
            }
            if (Has("triad_complete"))
            {
                zh[ending] += "\n\n三核审计的原始记录存放在公会和居民档案中。无论你选择如何调配星核，今后的每份供能账都必须记下谁获益、谁承担代价。";
                en[ending] += "\n\nThe original three-core audit is now public. Whatever you decided for the cores, future energy records must name both those protected and those paying the price.";
            }
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
