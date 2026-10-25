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
        // Optional expert tackle is a risk/reward choice for the CURRENT
        // cast only, never a SaveData v2 field or permanent unlock.
        public bool FishExpertCast { get; private set; }
        public int FishCatchReward { get { return FishExpertCast ? 2 : 1; } }
        // The public fish kitchen uses an existing v2 string flag so old
        // saves receive baseline stew unchanged until the player stocks it.
        public bool PublicFishKitchenStocked
        { get { return Has("fish_kitchen_supplied"); } }
        public int StewHealing { get { return PublicFishKitchenStocked?65:55; } }
        public float StewBuffSeconds
        { get { return PublicFishKitchenStocked?105f:90f; } }
        // A bite belongs to one simulation-time window, not every keyboard,
        // controller or pointer event in the same frame. Transient and never
        // persisted: closing the lake mini-game abandons only the active cast.
        private float fishNextBiteAt, fishBiteExpiresAt;
        // The fish lets go after one real timing opportunity. These clocks
        // belong to the current cast only; no SaveData field is introduced.
        public const float FishBiteWindow = 2.7f;
        public float FishBiteTimeRemaining { get { return FishLineReady ?
            Math.Max(0f,fishBiteExpiresAt-fishTime) : 0f; } }
        // Immediate bilingual feedback for the *actual* reel error, kept
        // only for the current mini-game; no quest/save changes.
        public string FishFeedbackZh { get; private set; } = "";
        public string FishFeedbackEn { get; private set; } = "";
        public float FishTarget { get { return save != null && save.reducedMotion ? .5f :
            .5f + .16f * (float)Math.Sin(fishTime * .67f +
                fishHits * 2.1f - fishMisses * .8f); } }
        public float FishWindowRadius { get { return FishExpertCast ?
            .10f - fishHits*.012f : .18f - fishHits*.022f; } }
        public bool FishLineReady { get { return mode == "fish" &&
            fishTime >= fishNextBiteAt && fishTime < fishBiteExpiresAt; } }
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
        // Dialogue is bound to BOTH the real source object and the map in
        // which the player originally opened it. The same world coordinates
        // in a later region or a replaced/deleted NPC are not authorization.
        private int conversationSourceRoom = -1;
        // If the NPC was a member of the current map on opening, deleting or
        // replacing that exact object invalidates all of its old buttons.
        // Transient runtime-authored actors that are not map members retain
        // the ordinary physical reach/room protections.
        private bool conversationSourceWasMapObject;
        // Choices opened from a reachable world object must be revalidated when
        // submitted; leaving its reach cannot turn an old modal into remote action.
        private bool conversationSourceWasReachable;
        // A choice may open a second dialogue. Carry the original source only
        // through that dispatch, and only attach it if no new source was chosen.
        private WorldObject pendingDialogueSource;
        private int pendingDialogueSourceRoom = -1;
        private bool pendingDialogueSourceWasMapObject;
        private bool pendingDialogueSourceWasReachable;
        private bool SourceBelongsToActiveMap(WorldObject source)
        {
            if (source == null || Map.objects == null) return false;
            foreach (WorldObject entry in Map.objects)
                if (System.Object.ReferenceEquals(entry, source)) return true;
            return false;
        }
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
            // A new save slot replaces the active play session. Transient
            // dialogue/inventory/fishing modes from the previous character
            // must not strand the new hero in a frozen non-world UI after
            // their conversations and actions have been cleared. Preserve
            // the initial TITLE when the constructor first loads a slot:
            // Start() remains responsible for entering gameplay from it.
            if (mode != "title") mode = "world";
            revision++;
        }
        private void ResetTransient()
        {
            // FrostField owns pulse clocks and exact visual references outside
            // the public effects list. Clearing effects alone leaves its
            // damage state alive, allowing an old cast to pulse after travel
            // or a save-slot replacement at the same world coordinates.
            ClearFrost();
            projectiles.Clear(); effects.Clear();
            flightReadyAtFrameStart.Clear();
            // A new journey/slot must never inherit pointer targeting, facing,
            // fishing results or HUD notifications from the previous hero.
            // None of these transient inputs belong to persisted SaveData.
            aim = dashDirection = new Point(1, 0);
            aimTarget = null;
            fishTime = fishCursor = fishNextBiteAt = fishBiteExpiresAt = 0;
            fishHits = fishMisses = 0;FishExpertCast=false;
            FishFeedbackZh=FishFeedbackEn="";
            toast = ""; toastTime = 0;
            // Runtime audio consumes a pending queue, not just the current
            // sound name. Neither old combat cues nor dialogue clicks may
            // play after loading a different character or starting fresh.
            sounds.Clear(); lastSound = "";
            attackCooldown = dashCooldown = dashTime = skillCooldown = guardTime = 0;
            invulnerable = 1; conversation = null; conversationSource = null;
            conversationSourceWasReachable = false;
            conversationSourceRoom = pendingDialogueSourceRoom = -1;
            conversationSourceWasMapObject = pendingDialogueSourceWasMapObject = false;
            pendingDialogueSource = null; pendingDialogueSourceWasReachable = false;
            runeProgress = 0;
            ResetExpansionTransient(); ResetCombatPresentation();
            RootsongPulseCooldown=0;
            ResetMineConvoy(); ResetFrostSignal(); ResetSwordGuard(); ResetSwordLesson(); ResetSpecialistDrill();
            ResetContinuity(); ResetLocalWaymark(); ResetRidgeWatch(); ResetRoadwardenTrial(); ResetWayfarerEncounter(); ResetAtlasEcho(); ResetReliefMission(); ResetRootHarmonyTrial(); ResetCommonsRelayTrial(); ResetSkywatch(); ResetGlasswind(); ResetTideSalvage(); ResetTemper(); ResetTriadDefense(); ResetWitnessDefense(); ResetBlackout(); ResetAfterlightCharge(); ResetAfterlightWatch(); ResetStormTransient(); ResetOuterPatrol(); ResetFarCourier(); ResetCivicResponse(); ResetLastDirective(); ResetCivicConduit(); ResetFloodRelief();
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
            Dialog("莉雅 · 公会接待员","Lyra · Guild Steward",
                "“信是你带来的？”\n\n莉雅把一封封口开裂的信放到桌上，没急着拆。\n\n“这封本来该由巡路员自己送回来。他走之前答应替镇上的人查一条林间小路。七天过去了，连他的靴印都找不着。”\n\n窗外的马车在泥里打滑。远处传来矿场的钟声。\n\n“星陨之后，桥塌了，密林不让人进，矿机倒是一刻没停。堡垒只派人来要星核，说是城墙上的屏障不能熄。我问他们，河对岸的药怎么办——没人回答。”\n\n她把荆棘密林的地图推向你。\n\n“替我把侦察员找回来。黑荆巨兽盘踞着那座旧祭坛；如果他还活着，也许知道祭坛为什么出事。至于星核，见到之后再决定怎么带回来。别为它丢了命。”",
                "“You're the one who brought this letter?”\n\nLyra places an envelope with a split seal on the desk. She doesn't open it yet.\n\n“The roadwarden was supposed to bring it back himself. He promised the people here he'd look for a path through the forest. Seven days. Not even a bootprint.”\n\nA cart slips in the mud outside. The mine bell sounds in the distance.\n\n“Since Starfall the bridge is down, the woods are closed, and the mine hasn't stopped for a breath. The citadel only sends men asking for starcores. Says their shield can't go dark. I asked about medicine for the far bank. No answer.”\n\nShe slides the Thornwood map toward you.\n\n“Find our scout. The Briar Beast's camped on the old altar. If he's alive, he may know what happened there. As for the core—see what you find. Don't throw your life away for it.”",
                C("收下地图，去密林找人","Take the map and find the scout","close"));
        }
        public void Notify(string zh, string en) { toast = L(zh, en); toastTime = 4; }
        private void Dialog(string speaker, string en, string text, string textEn, params Choice[] choices)
        {
            // Keep source-bound validation alive across multi-page menus such as
            // the public chronicle. A newly selected world object takes priority.
            if (conversationSource == null && pendingDialogueSourceWasReachable &&
                pendingDialogueSource != null &&
                pendingDialogueSourceRoom == save.room &&
                IsObjectPresent(pendingDialogueSource) &&
                !IsObjectFromAnotherMap(pendingDialogueSource) &&
                (!pendingDialogueSourceWasMapObject ||
                 SourceBelongsToActiveMap(pendingDialogueSource)) &&
                HasPhysicalInteractionReach(pendingDialogueSource))
            {
                conversationSource = pendingDialogueSource;
                conversationSourceRoom = pendingDialogueSourceRoom;
                conversationSourceWasMapObject = pendingDialogueSourceWasMapObject;
                conversationSourceWasReachable = true;
            }
            conversation = new Conversation(speaker, en, text, textEn, choices); mode = "dialog";
        }
        public void Close()
        {
            if(mode=="fish")FishExpertCast=false;
            conversation = null; conversationSource = null;
            conversationSourceWasReachable = false;
            conversationSourceRoom = -1;
            conversationSourceWasMapObject = false;
            mode = "world";
        }
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
        // One authoritative gate for travel that would reset a protected live mission.
        // Cancellable trials (field surveys, Root Harmony and skiff races) are
        // intentionally omitted: leaving cancels their transient progress.
        private bool BlockTravelForProtectedFieldMission(bool fastTravel, bool earlyOnly = false)
        {
            if (CivilianFieldMissionBlocksTravel()) return true;
            if (CivicConduitActive || LastDirectiveWave > 0)
            {
                if (fastTravel)
                    Notify("先完成现场任务，或在原始控制台明确中止，才能快速回城。",
                        "Finish or explicitly abort the live Citadel mission at its physical terminal before fast travel.");
                else if (CivicConduitActive)
                    Notify("导体仍在堡垒南路通电！完成阀门或回西端控制台明确中止。",
                        "Conductor active: finish the valves or explicitly abort at the western desk before travel.");
                else
                    Notify("堡垒保密终端正在实战或结算，请完成行动或亲自回终端撤销。",
                        "Citadel security remains active. Finish or withdraw at its terminal before travel.");
                return true;
            }
            if (stormTuning)
            {
                if (fastTravel)
                    Notify("公共星灯调谐仍在进行，不能使用快捷回城绕过城市节点。",
                        "Public lamp tuning is active; finish or cancel before fast travel.");
                else
                    Notify("公约星灯正在进行实地调谐，必须完成或返回议事台放弃，不能切换地图。",
                        "Live civic-grid tuning must finish or be canceled at the council before travel.");
                return true;
            }
            if (afterlightChargeRegion != 0)
            {
                if (fastTravel)
                    Notify("第五章余光传输中，不能直接快速回城。请到原锚点取消或完成通电。",
                        "A public afterlight charge is in transit. Finish at the receiver or cancel at the original anchor.");
                else
                    Notify("余光输送仍在进行。先走到本地区公共接收台，或返回原锚点主动取消。",
                        "Afterlight transfer is active. Reach the real receiver or return to the source to cancel before traveling.");
                return true;
            }
            if (AfterlightWatchActive)
            {
                if (fastTravel)
                    Notify("守护公共储备的战斗尚未结束，请返回接收台撤销或实际完成防卫。",
                        "Finish civic reserve defense or withdraw at its receiver before returning to town.");
                else
                    Notify("公共储备防卫进行中！击退袭击者，或返回原接收台主动终止。",
                        "Civic reserve defense active. Defeat the attackers or withdraw at the receiver before traveling.");
                return true;
            }
            if (blackoutActive)
            {
                if (fastTravel)
                    Notify("堡垒居民仍在撤离中。先回避难所完成或取消救援。",
                        "Citadel evacuation is active. Complete or cancel at the shelter.");
                else
                    Notify("屏障长夜撤离仍在进行。先把居民送到避难所或明确中止，不能直接换地区。",
                        "Blackout evacuation is active. Bring your civilians to shelter or abort the mission before traveling.");
                return true;
            }
            // ReturnToTown historically checks this rescue before its death/room
            // early-outs; Enter and the Abyss use the complete gate below.
            if (earlyOnly)
            {
                if (reliefMissionRoom != 0)
                {
                    Notify("正在护送居民，先返回站台或者取消行动。",
                        "You are escorting civilians. Return to the station or cancel the rescue first.");
                    return true;
                }
                return false;
            }
            if (witnessDefenseRoom != 0)
            {
                Notify("本章居民证词正在战斗保全中，请击败守卫或在证据台撤出。",
                    "A live testimony-defense is active. Defeat its guards or withdraw at the field seal before traveling.");
                return true;
            }
            if (TriadDefenseActive)
            {
                Notify("第四章审计守卫仍在行动。先返回中央控制台放弃或击败它。",
                    "Chapter Four security is still active. Defeat it or withdraw at the central console before leaving.");
                return true;
            }
            if (TemperActive)
            {
                Notify("工房仍在过热运行。先返回锻炉结束或安全停机。",
                    "The foundry is still active. Finish or safely shut it down at the furnace before traveling.");
                return true;
            }
            if (TideSalvageActive)
            {
                Notify("打捞行动还在进行，先回码头交付或放弃货物。",
                    "Salvage is underway. Deliver or abandon at the dock before traveling.");
                return true;
            }
            if (GlasswindSite != 0)
            {
                Notify("风脉中继正在争夺中。请封存或返回中继台撤出后再旅行。",
                    "Glasswind relay is contested. Seal or withdraw at its console before traveling.");
                return true;
            }
            if (commonsRelayRoom != 0)
            {
                Notify("公共信标正在重连。先完成三段线路或返回控制台结束操作。",
                    "Commons Relay is live. Complete its three circuits or withdraw at a console before traveling.");
                return true;
            }
            if (reliefMissionRoom != 0)
            {
                Notify("正在救援居民。先完成撤离或到避难站明确取消，不能直接切换地区。",
                    "Rescue in progress. Finish the evacuation or cancel it at the station before traveling.");
                return true;
            }
            return false;
        }

        public void Enter(int room)
        {
            if (Dying) return;
            if (BlockTravelForProtectedFieldMission(false)) return;
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
            if (BlockTravelForProtectedFieldMission(true, true)) return;
            if (Dying) return;
            if (save.room == 0) return;
            if (InAbyss) { AbyssExitConversation(); return; }
            if (!SafeTravel) { Notify("脱离敌人后才能快速回城。西侧出口随时可用。", "Leave combat to fast travel. The west exit is always available."); return; }
            Enter(0);
        }
        private void SpawnRoom()
        {
            enemies.Clear(); enemyId = 0; ResetRidgeWatch();
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
        private static bool IsValidTransientEffect(Effect effect)
        {
            return effect != null && !string.IsNullOrEmpty(effect.kind) &&
                Finite(effect.life) && effect.life > 0f &&
                Finite(effect.maxLife) && effect.maxLife > 0f &&
                Finite(effect.position.x) && Finite(effect.position.y);
        }

        private void RepairTransientEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect effect = effects[i];
                if (!IsValidTransientEffect(effect))
                {
                    effects.RemoveAt(i);
                    continue;
                }
                // These are presentation-only fields. Recover a safe neutral
                // fallback rather than letting a bad angle/height/radius
                // contaminate GUI matrices or make a world flash unbounded.
                if (!Finite(effect.radius)) effect.radius = 0f;
                if (!Finite(effect.height)) effect.height = -1f;
                if (!Finite(effect.direction.x) || !Finite(effect.direction.y))
                    effect.direction = new Point();
            }
        }

        private void TickEffects(float dt)
        {
            // TickGate and other world hooks may also add effects, so validate
            // once more immediately before lifetime advancement.
            RepairTransientEffects();
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].life -= dt;
                if (!Finite(effects[i].life) || effects[i].life <= 0f)
                    effects.RemoveAt(i);
            }
        }

        public void Tick(float dt, Point movement, Point? explicitAim = null,
            bool sprintHeld = false)
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
            // Invalid presentation records must be removed before any UI,
            // mission or world renderer can enumerate them. Valid effect clocks
            // remain frozen outside world mode, exactly as before.
            RepairTransientEffects();
            if (mode == "fish")
            {
                // The fish moves its bite lane while the player's float
                // swings with increasing tempo. Nothing else in the world
                // simulates during the minigame, as before.
                float previousFishTime = fishTime;
                fishTime += dt;
                // A real bite is heard once at the readiness boundary,
                // not every frame the green window remains available.
                if (previousFishTime < fishNextBiteAt && fishTime >= fishNextBiteAt)
                {
                    sound = "fishBite";
                    FishFeedbackZh = FishFeedbackEn = "";
                }
                fishCursor = .5f + .5f * (float)Math.Sin(
                    fishTime * (2.5f + fishHits * .35f + fishMisses * .08f));
                // Waiting indefinitely for perfect alignment made the lake
                // noninteractive. Expired bites are genuine missed pulls, but
                // can never charge twice or pay a catch reward.
                if (fishTime >= fishBiteExpiresAt)
                {
                    FishMissedBite(true);
                    if (fishMisses >= 3)
                    {
                        Close();
                        Notify("鱼已经游走。下次听见咬钩声，要及时收线。",
                            "The fish slipped away. Reel promptly on the next bite.");
                    }
                }
                return;
            }
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
            float dodgeReadyDelay = Math.Max(0, Math.Max(dashCooldown, playerHitTime));
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
            TickEffects(dt);
            if (Dying) { deathTime -= dt; if (deathTime <= 0) CompleteDeath(); return; }
            bool beganInPractice = practicing;
            save.totalTime += dt;
            // Resolve changed world collision before any body, missile or contact advances.
            TickTideTransition(); Point beforeMove = Position;
            // A held LMB is sampled every frame. Once the hero escapes a
            // sealed foot, permit the NEXT genuine blocked-floor episode to
            // explain itself without spamming/overwriting unrelated toasts.
            if(blockedFootingNoticeShown && CanStand(Position))
                blockedFootingNoticeShown=false;
            attackCooldown -= dt; dashCooldown -= dt; skillCooldown -= dt; invulnerable -= dt; guardTime -= dt;
            TickSwordGuard(dt);
            TickSwordLesson(dt);
            TickSpecialistDrill(dt);
            dodgeEscapeTime = Math.Max(0, dodgeEscapeTime - dt);
            save.energy = Clamp(save.energy + dt * ((save.profession == 2 ? 18 : 14) + (InAbyss ? save.boons[1] * 4 : 0) + (save.teaUntil > save.totalTime ? 6 : 0) +
                (Has("commons_relay_complete") ? 2 : 0) + (Has("roadcharter_reserve") ? 3 : 0) +
                (save.room<8 && (save.x>=36 || save.y>=24) &&
                    TrailCircuitComplete(save.room) ? 2 : 0) +
                // Cleared outer watches grant a real, permanent local
                // endurance benefit during sustained high-trail traversal.
                // No global combat buff and no new save schema field.
                (save.room>=1&&save.room<=7&&
                    save.x>=70f&&save.y>=55f&&
                    RidgeWatchComplete(save.room) ? 2 : 0) +
                (racing ? 8 : 0) - (save.cold > 70 ? 4 : 0)), 0, 100);
            if (movement.Length > 1) movement = movement.Normalized;
            ValidateActionProfile();
            TickMovement(dt, movement, explicitAim, hitLockedDt, sprintHeld);
            TickLocalWaymark(dt);
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
            TickRidgeWatchPatrols(dt);
            UpdateProjectiles(dt); if (Dying || beganInPractice && !practicing) return;
            TickOuterPatrol(dt);
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
            // Dynamic gates/tides can close over the hero between ticks.
            // A dash from buried ground cannot physically travel, and must
            // not consume stamina, mint 320ms of invulnerability or grant
            // a free perfect-dodge counter for a nonexistent dodge.
            if (!CanStand(Position))
            {
                ClearBufferedDodge();
                return;
            }
            // A hit-recovery press may be buffered only when BOTH hitstun
            // and cooldown can finish inside the original 140ms window.
            // Never grant movement/invulnerability over a visible Hit pose.
            if (playerHitTime > .00001f || hitStop > .00001f)
            {
                float readyDelay = Math.Max(hitStop, Math.Max(playerHitTime, dashCooldown));
                if (readyDelay <= DodgeBufferWindow + .00001f &&
                    save.energy >= cost)
                    QueueLateDodge(direction);
                else
                {
                    ClearBufferedDodge();
                    if (readyDelay <= DodgeBufferWindow + .00001f)
                        ActionUnavailable(dashCooldown, cost);
                }
                return;
            }
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
        // A single actionable warning per physically sealed root.
        // This is runtime feedback only; it never becomes a SaveData field.
        private bool blockedFootingNoticeShown;
        private void WarnBlockedFooting(string zh,string en)
        {
            if(blockedFootingNoticeShown)return;
            blockedFootingNoticeShown=true;
            Notify(zh,en);
        }
        public void Attack(Point direction)
        {
            if(!Finite(direction.x)||!Finite(direction.y))
                direction=new Point();
            if (mode != "world" || Dying || bufferedSkill != null ||
                bufferedDodge != null) return;
            // The original collision topology can seal the hero's feet
            // before input (tide, raised gate, collapsing bridge). Do not
            // charge an impossible strike or buffer an attack from stone.
            // A floor that closes AFTER a valid input is still handled at
            // the authoritative TickAction contact, as before.
            if(!CanStand(Position))
            {
                ClearBufferedAttack();
                WarnBlockedFooting("脚下地形已封闭，先返回可站立位置再攻击。",
                    "The ground is blocked. Move to walkable footing before attacking.");
                return;
            }
            // "Recovery" permits the next buffered Q to join a combo, but
            // it is NOT permission to replace the current whole-body Cast
            // or Attack before its authored duration finishes. A late LMB
            // gets one bounded input intent and fires after the old pose.
            if (playerAction != null || ActionTransitionBlocked ||
                attackCooldown > 0)
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
            float cost = ClassSkillEnergyCost;
            if (mode != "world" || Dying) return;
            if(!CanStand(Position))
            {
                ClearBufferedSkill();
                WarnBlockedFooting("脚下地形已封闭，无法从此处发动职业战技。",
                    "Blocked footing prevents casting a class technique here.");
                return;
            }
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
            float cost = ClassSkillEnergyCost;
            ClearBufferedSkill();
            // An accepted Q may wait 200ms for a visible previous action.
            // Recheck the physical root at DISPATCH as well: no charged
            // cooldown or fake cast if a gate closes during that buffer.
            if(!CanStand(Position))
            {
                WarnBlockedFooting("施法前地形发生变化；请先站稳再试。",
                    "Footing changed before release; find safe ground and retry.");
                return;
            }
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
                    action.blockedAtContact=true;
                    effects.Add(new Effect("projectileImpact", blocked.position, .22f)
                        { direction = action.direction, height = blocked.height });
                    sound = "guard";
                }
                return;
            }
            if (save.profession == 0) guardTime = 3;
            float range = save.profession == 2 ? 4.5f : 3.1f;
            effects.Add(new Effect("burst", Position, .6f) {
                variant = save.profession, radius = range, playerAreaSkill = true
            });
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
            // The eight-second manual focus becomes a two-body combat
            // opportunity only through genuine hero AND ally contacts.
            damage *= ResolveCompanionFocusContact(e,profile);
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
            OnSpecialistDrillHit(e,profile,technique);
            // A REAL player technique hit inherits the three professions'
            // distinct combat element, but only after the authoritative HP,
            // stagger, critical and cover checks have actually succeeded.
            // The same original contact painting remains on screen; variant
            // only tints that Sprite, never invents a second body/hit.
            // A technique belongs to its snapshotted vocation. An ordinary
            // attack instead belongs to the EQUIPPED physical weapon:
            // sword stays neutral, arrows carry green flights, staff bolts
            // carry arcane blue. A projectile landing AFTER a mid-flight
            // equipment/class change keeps its own original muzzle identity.
            // Only REAL dealt HP damage reaches this visual event.
            int contactElement=0;
            if(profile.playerOwned)
            {
                if(technique&&profile.profession>=0&&
                    profile.profession<=2)
                    contactElement=profile.profession+1;
                else if(!technique&&profile.weapon>=1&&
                    profile.weapon<=2)
                    contactElement=profile.weapon+1;
            }
            effects.Add(new Effect(e.kind == 2 || e.boss ? "spark" : "impact",
                e.Position, .22f) { direction = e.recoil,
                variant = contactElement });
            if (!save.reducedMotion) { hitStop = Math.Max(hitStop, technique ? .05f : .025f); impactShake = Math.Max(impactShake, technique ? .12f : profile.weapon == 0 && profile.combo == 2 ? .07f : 0); }
            effects.Add(new Effect("number", e.Position, .7f, (crit ? "! " : "") + ((int)damage)));
            if (e.hp > 0) return;
            fallen.Add(new FallenActor(e, save.room, e.boss ? 1.4f : .65f));
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
            OnRidgeWatchEnemyDefeated(e);
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
                EnsureRidgeWatchPatrols();
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
            enemy.targetingAfterlightStock = enemy.targetingOuterPatrolStock = false;
            RemoveEnemyPendingWarning(enemy);
        }
        // Repair transient reaction clocks before deriving lock/free time. A NaN
        // hit/stun/release clock poisons Math.Max/Min and can leave an enemy
        // permanently unable to move or attack; an invalid windup must also
        // retract its owner-specific warning before any release is evaluated.
        private bool RepairEnemyTransientClocks(Enemy enemy)
        {
            bool invalidActionClock =
                !Finite(enemy.timer) || !Finite(enemy.hitTime) ||
                !Finite(enemy.stun) || !Finite(enemy.releaseTime) ||
                !Finite(enemy.windupDuration);
            bool invalidRecoil = !Finite(enemy.recoil.x) ||
                !Finite(enemy.recoil.y);

            if (!Finite(enemy.flash)) enemy.flash = 0f;
            if (!Finite(enemy.stagger)) enemy.stagger = 0f;
            if (!Finite(enemy.staggerRecovery)) enemy.staggerRecovery = 0f;
            if (!Finite(enemy.moveTime)) enemy.moveTime = 0f;
            if (!Finite(enemy.hitTime)) enemy.hitTime = 0f;
            if (!Finite(enemy.stun)) enemy.stun = 0f;
            if (!Finite(enemy.releaseTime)) enemy.releaseTime = 0f;
            if (!Finite(enemy.windupDuration)) enemy.windupDuration = 0f;
            if (invalidRecoil)
            {
                enemy.recoil = new Point();
                enemy.hitTime = 0f;
                invalidActionClock = true;
            }

            if (!invalidActionClock) return false;
            enemy.state = 0;
            enemy.timer = .22f;
            enemy.targetingWayfarer = enemy.targetingReliefCivilian = false;
            enemy.targetingAfterlightStock = enemy.targetingOuterPatrolStock = false;
            RemoveEnemyPendingWarning(enemy);
            return true;
        }

        private void RemoveEnemyPendingWarning(Enemy enemy)
        {
            if(enemy==null)return;
            // A canceled Boss support call must retract only its own warning,
            // not a second guardian's cue or unrelated player combat effects.
            effects.RemoveAll(fx=>Object.ReferenceEquals(
                fx.bossSummonCueOwner,enemy));
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
            // A REAL hit interrupts the pending follow-up LMB, not just
            // the visible body reaction. Otherwise a last-window Q/LMB tap
            // can survive a full recoil and fire unexpectedly as the hero
            // regains control. Successful parries, dodges, invulnerable
            // contact and cosmetic zero-power warnings return above and
            // correctly preserve the deliberate buffered input.
            ClearBufferedAttack();
            // A dodge queued before an actual hit is no longer an escape.
            // Without retiring it at physical contact, its cooldown could
            // finish under recoil and automatically spend energy on a ghost
            // dodge while the hero is displaying the authored Hit pose.
            // Q deliberately retains its own hit-recovery input contract.
            ClearBufferedDodge();
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
            effect.outerPatrolOwned = IsOuterPatrolEnemy(attacker);
        }
        // Use the precise SAME 0.1-tile swept-radius shot samples that
        // UpdateProjectiles uses, not the center-only actor sight ray. A bow
        // hand can clear its immediate muzzle while an arrow clips the
        // side of a stone pillar farther along its physical flight.
        // Check the live intended target before starting AND throughout
        // the windup; enemy logic can seek a new firing lane instead of
        // wasting repeated full-length draws into cover.
        private bool EnemyBowFlightLaneClear(Enemy enemy,Point target)
        {
            return enemy!=null&&enemy.kind==1&&!enemy.boss&&
                PhysicalBowLaneFromFoot(enemy.Position,target);
        }
        // For selecting a NEW real firing foot, evaluate the same arrow
        // footprint without mutating another enemy actor's live position.
        private bool PhysicalBowLaneFromFoot(Point foot,Point target)
        {
            if(!Finite(target.x)||!Finite(target.y)||
                !CanStand(foot))return false;
            Point look=target-foot;
            if(look.Length<.001f)return false;
            Projectile shot=WeaponShot(foot,1,
                look.Normalized,5f,10f,target);
            if(shot.blockedAtSpawn||ShotBlocked(shot,shot.position))
                return false;
            Point travel=target-shot.position;
            float range=travel.Length;
            if(!Finite(range)||range>5f*shot.life)return false;
            int steps=Math.Max(1,(int)Math.Ceiling(range/.1f));
            for(int i=1;i<=steps;i++)
                if(ShotBlocked(shot,shot.position+
                    travel*(i/(float)steps)))return false;
            return true;
        }
        // A real archer's source-painted walking stride cannot look
        // intentional when the AI changes its intended firing foot on every
        // 50ms tick. Keep an instance-owned, transient lateral destination.
        // Never serialize it or share it through an enemy spawn ID: restored
        // foes and fresh waves can reuse IDs without inheriting old routes.
        private sealed class ArcherCornerPlan
        {
            internal SaveData save;
            internal int room=-1;
            internal Point target,goal;
            internal bool active;
        }
        private readonly ConditionalWeakTable<Enemy,ArcherCornerPlan>
            archerCornerPlans=new ConditionalWeakTable<Enemy,ArcherCornerPlan>();

        private bool ArcherWalkCorridorClear(Point from,Point goal)
        {
            Point travel=goal-from;
            if(!Finite(travel.x)||!Finite(travel.y)||
                travel.Length>2.25f||!CanStand(from))return false;
            int steps=Math.Max(1,(int)Math.Ceiling(travel.Length/.11f));
            for(int i=1;i<=steps;i++)
                if(!CanStand(from+travel*(i/(float)steps)))return false;
            return true;
        }

        // When the bow's physical radius scrapes a wall despite an open
        // centerline, search up to 2.2 tiles of actual walkable lateral
        // ground. Return a validated DESTINATION, not just an arrow heading
        // that may reverse itself when the enemy walks by half a tile.
        private Point ArcherPhysicalCornerGoal(Point from,Point target)
        {
            Point sight=target-from;
            if(sight.Length<.1f||!CanStand(from)||
                !ClearAttackPath(from,target))return new Point();
            Point direction=sight.Normalized;
            Projectile travel=WeaponShot(from,1,direction,5f,10f,target);
            if((target-travel.position).Length>5f*travel.life||
                PhysicalBowLaneFromFoot(from,target))return new Point();
            Point side=new Point(-direction.y,direction.x);
            bool positiveOpen=true,negativeOpen=true;
            for(int ring=1;ring<=4;ring++)
            {
                float reach=ring*.55f;
                for(int sign=0;sign<2;sign++)
                {
                    if(sign==0&&!positiveOpen||sign==1&&!negativeOpen)
                        continue;
                    Point lateral=sign==0?side:side*-1f;
                    Point foot=from+lateral*reach;
                    // Reject disconnected free destinations: the real
                    // body must fit at every intermediate physical point.
                    if(!ArcherWalkCorridorClear(from,foot))
                    {
                        if(sign==0)positiveOpen=false;
                        else negativeOpen=false;
                        continue;
                    }
                    if(ClearAttackPath(foot,target)&&
                        PhysicalBowLaneFromFoot(foot,target))
                        return foot;
                }
                if(!positiveOpen&&!negativeOpen)break;
            }
            return new Point();
        }

        // Preserve the original two-Point direct probe/test contract.
        private Point ArcherPhysicalCornerSidestep(Point from,Point target)
        {
            Point goal=ArcherPhysicalCornerGoal(from,target);
            return goal.Length<.01f||(goal-from).Length<.1f?
                new Point():(goal-from).Normalized;
        }

        private Point ArcherFollowCornerPlan(Enemy enemy,Point target)
        {
            ArcherCornerPlan plan=archerCornerPlans.GetValue(enemy,
                ignored=>new ArcherCornerPlan());
            Point from=enemy.Position;
            bool sameOwner=plan.active&&plan.room==save.room&&
                Object.ReferenceEquals(plan.save,save)&&
                (target-plan.target).Length<.48f;
            // A gate, changed target or formerly obstructed muzzle can
            // invalidate the intended destination before a shot is earned.
            if(sameOwner&&(plan.goal-from).Length>.09f&&
                ArcherWalkCorridorClear(from,plan.goal)&&
                ClearAttackPath(plan.goal,target)&&
                PhysicalBowLaneFromFoot(plan.goal,target))
                return (plan.goal-from).Normalized;
            plan.active=false;
            Point next=ArcherPhysicalCornerGoal(from,target);
            if(next.Length<.01f||(next-from).Length<.09f)
                return new Point();
            plan.save=save;
            plan.room=save.room;
            plan.target=target;
            plan.goal=next;
            plan.active=true;
            return (next-from).Normalized;
        }
        private void UpdateEnemies(float dt)
        {
            tickingEnemies.Clear();tickingEnemies.AddRange(enemies);
            foreach (Enemy e in tickingEnemies)
            {
                if (Dying) break;
                if (!enemies.Contains(e) || mode != "world") continue;
                // Sanitize ALL reaction clocks before Math.Max/Min derives the
                // amount of unlocked AI time. Otherwise one NaN can propagate
                // through freeDt and turn an ordinary foe into a frozen actor.
                if (RepairEnemyTransientClocks(e)) continue;
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
                // The defender drill contains a hunter AND a bowman.
                // Only the hunter storms the station. The bowman covers it
                // by fighting the real player at physical arrow distance;
                // following the station itself forced every longbow to
                // crowd the beacon and aim its corner-search at a prop.
                // The existing ward-pressure rule still applies if this
                // archer truly reaches the beacon under its own movement.
                bool wardBow=wardAttacker&&e.kind==1&&!e.boss;
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
                bool patrolSaboteur=IsOuterPatrolEnemy(e)&&e.kind!=1;
                Point pursuit = patrolSaboteur ? outerPatrolPost : stockRaider ? AfterlightPoint(save.room,"outlet") :
                    wardAttacker ? (wardBow ? Position :
                        ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position)) :
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
                            e.targetingAfterlightStock = e.targetingOuterPatrolStock = false;
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
                        (!ClearAttackPath(e.Position,e.target)||
                         !EnemyBowFlightLaneClear(e,e.target)))
                    {
                        e.state=0;
                        // Cancel the missed *long* draw: once cover opens,
                        // reacquire the lane on the same responsive 220ms
                        // polling cadence as companion ranged combat.
                        e.timer=.22f;
                        e.targetingWayfarer=e.targetingReliefCivilian=
                            e.targetingAfterlightStock = e.targetingOuterPatrolStock = false;
                        RemoveEnemyPendingWarning(e);
                        continue;
                    }
                    // Even a clear centerline is not sufficient for an
                    // ordinary archer. The physical bow hand sits to one
                    // side of the feet; a nearby corner can bury its muzzle
                    // without covering either living actor or center ray.
                    // Check the SAME swept attachment used by real Fire
                    // before committing attack state, sound or projectile.
                    if (!e.boss && e.kind == 1 &&
                        WeaponShot(e.Position,1,e.facing,5f,10f,e.target).blockedAtSpawn)
                    {
                        e.state=0;
                        e.timer=.22f;
                        e.targetingWayfarer=e.targetingReliefCivilian=
                            e.targetingAfterlightStock = e.targetingOuterPatrolStock = false;
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
                            else if (perfectDodgeArmed &&
                                ClearAttackPath(e.Position, dashOrigin))
                                // Dodge counters are earned by escaping a
                                // REAL strike, not by dashing outside a red
                                // ring that could never reach the original
                                // foot through a newly shut wall. Match the
                                // live path used for the actual melee/AoE hit.
                                TryRewardEscapedWarning(e.target, radius);
                            if (Dying) break;
                            if (e.boss)
                            {
                                // A released call's warning ends precisely on
                                // the real contact frame, before support appears.
                                // Retire only this actual caster's cue.
                                // Another guardian can have the same numeric
                                // spawn ID but its warning still matters.
                                effects.RemoveAll(fx=>Object.ReferenceEquals(
                                    fx.bossSummonCueOwner,e));
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
                        if(patrolSaboteur&&e.targetingOuterPatrolStock)
                            DamageOuterPatrolStock(e);
                        if (travelerAttacker && e.targetingWayfarer) DamageWayfarer(e);
                        if (reliefMissionRoom != 0 &&
                            (IsReliefMissionEnemy(e) || escortPatrol) &&
                            e.targetingReliefCivilian && e.kind != 1)
                            DamageReliefCivilian(e);
                        // Rescue archers only injure civilians where their real
                        // arrow actually flies. No hitscan at the windup release.
                        e.targetingWayfarer = e.targetingReliefCivilian =
                            e.targetingAfterlightStock = e.targetingOuterPatrolStock = false;
                        rescueWindupEffects.Remove(e);
                        e.state = 0; e.timer = e.boss ? 2.2f - e.BossPhase * .3f : 1.4f;
                    }
                    continue;
                }
                // Defense attackers cannot abandon the beacon after knockback
                // or while navigating around a distant obstruction.
                if (!wardAttacker && !travelerAttacker && !reliefAttacker &&
                    !escortPatrol && !stockRaider && !patrolSaboteur &&
                    distance > (e.boss ? 18 : 8)) continue;
                e.timer -= freeDt;
                // A rescue archer is a ranged threat, not another melee saboteur.
                // Hold a genuine firing lane outside the civilian's reach.
                float desired = wardBow ? 4.5f :
                    (reliefAttacker || escortPatrol) && e.kind == 1 ? 4.3f :
                    wardAttacker || travelerAttacker || reliefAttacker || escortPatrol || stockRaider || patrolSaboteur ? 1.05f :
                    e.boss ? 3.5f : e.kind == 1 ? 4.5f : 1.05f;
                bool visible = ClearAttackPath(e.Position, pursuit);
                if(visible&&!e.boss&&e.kind==1)
                    visible=EnemyBowFlightLaneClear(e,pursuit);
                Point before = e.Position;
                if (e.kind == 1 && !e.boss && (!wardAttacker||wardBow) &&
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
                    // Actual archer missile LOS may be shut by a corner
                    // beside an otherwise perfectly open walking ray.
                    // Evaluate alternate legal shooting feet BEFORE
                    // committing pursuit: moving directly nearer simply
                    // repeats a blocked shot or eventually backpedals.
                    Point heading=DirectionTo(e.Position,pursuit);
                    if(e.kind==1&&!e.boss&&!visible)
                    {
                        Point lateral=ArcherFollowCornerPlan(e,pursuit);
                        if(lateral.Length>.1f)heading=lateral;
                    }
                    Point p = e.Position + heading * freeDt * (e.boss ? 1.15f + e.BossPhase * .15f : e.kind == 0 ? 2.1f : 1.5f) * (InAbyss && save.abyssFloor % 3 == 2 ? 1.2f : 1);
                    Point swept = SweepActor(e.Position, p - e.Position); e.x = swept.x; e.y = swept.y;
                }
                Point travelled = e.Position - before; e.moving = travelled.Length > .001f;
                if (e.moving) { e.moveTime += travelled.Length; e.Face(e.backpedaling?Position-e.Position:travelled); }
                delta = Position - e.Position; distance = delta.Length;
                bool heroInRange = distance < (e.boss ? 12 : e.kind == 1 ? 7 : 2.5f) &&
                    ClearAttackPath(e.Position, Position) &&
                    (e.boss||e.kind!=1||
                     EnemyBowFlightLaneClear(e,Position));
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
                    ClearAttackPath(e.Position, pursuit) &&
                    (e.kind!=1||e.boss||
                     EnemyBowFlightLaneClear(e,pursuit));
                bool strikeCivilian = nearCivilian &&
                    (!heroInRange || distance > 2.2f);
                bool nearStock=stockRaider&&(pursuit-e.Position).Length<2.2f &&
                    ClearAttackPath(e.Position,pursuit);
                bool strikeStock=nearStock&&(!heroInRange||distance>2.2f);
                bool nearPatrolStock=patrolSaboteur&&
                    (pursuit-e.Position).Length<2.2f&&
                    ClearAttackPath(e.Position,pursuit);
                bool strikePatrolStock=nearPatrolStock&&(!heroInRange||distance>2.2f);
                // A still-buried enemy may recover its footing when the
                // world opens again, but must not start a new windup from an
                // invalid tile while waiting for that transition.
                if (e.timer <= 0 && CanStand(e.Position) &&
                    (heroInRange || strikeTraveler || strikeCivilian || strikeStock || strikePatrolStock))
                {
                    e.state = 1; e.timer = e.boss ? 1.1f - e.BossPhase * .08f : .65f; e.windupDuration = e.timer;
                    e.targetingWayfarer = strikeTraveler;
                    e.targetingReliefCivilian = strikeCivilian;
                    e.targetingAfterlightStock = strikeStock;
                    e.targetingOuterPatrolStock = strikePatrolStock;
                    e.target = strikeTraveler || strikeCivilian || strikeStock || strikePatrolStock ? pursuit : Position;
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
                        (strikeStock && e.kind != 1) ||
                        (strikePatrolStock && e.kind != 1))
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
                    if (e.boss || distance < 6 || strikeCivilian || strikeStock || strikePatrolStock) sound = "windup";
                    if(e.boss && enemies.Count<9 &&
                        (e.BossPhase==3||(save.room==4&&e.BossPhase>=2)) &&
                        (e.cycle+1)%3==0)
                    {
                        // Announce the ACTUAL upcoming support cycle before
                        // the enemy's existing timed attack. Staggering,
                        // freezing or defeating its owner retracts this cue.
                        effects.Add(new Effect("number",
                            e.Position+new Point(0,-1f),e.timer+.06f,
                            L("正在呼叫增援！","CALLING REINFORCEMENTS!"))
                            {bossSummonCueOwnerId=e.id,
                             bossSummonCueOwner=e});
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
        { Projectile shot=e.boss?new Projectile{position=e.Position,velocity=dir.Normalized*speed,damage=14}:WeaponShot(e.Position,1,dir,speed,10,e.target); shot.source="shot";shot.roadTrialOwned = IsRoadwardenTrialEnemy(e); shot.outerPatrolOwned = IsOuterPatrolEnemy(e);
            shot.skywatchOwned = IsSkywatchEnemy(e);
            shot.atlasEchoOwned = IsAtlasEchoEnemy(e);
            shot.wayfarerOwned = IsWayfarerEnemy(e);
            shot.courierOwned=IsFarCourierRaider(e);shot.floodReliefOwned=IsFloodReliefEnemy(e);shot.civicResponseOwned=IsCivicResponseEnemy(e);shot.lastDirectiveOwned=IsLastDirectiveEnemy(e);shot.rootHarmonyOwned = IsRootHarmonyEnemy(e);shot.reliefMissionOwned = IsReliefMissionEnemy(e) ||
                reliefMissionWave == 3 && reliefMissionRoom == save.room &&
                e.targetingReliefCivilian;shot.tideSalvageOwned=IsTideSalvageEnemy(e);shot.temperOwned=IsTemperEnemy(e);shot.mineConvoyOwned=IsMineConvoyEnemy(e);shot.frostSignalOwned=IsFrostSignalEnemy(e);shot.triadOwned=IsTriadEnemy(e);shot.witnessOwned=IsWitnessEnemy(e);
            shot.commonsRelayOwned=IsCommonsRelayEnemy(e);
            shot.glasswindOwned=IsGlasswindEnemy(e);
            shot.afterlightWatchOwned=IsAfterlightWatchEnemy(e);
            shot.visual=e.boss?"enemyOrb":"enemyArrow";shot.hostileShooter=e;shot.height=e.boss?BossEmissionHeight(save.room):WeaponHeight(1,dir);shot.launchHeight=shot.flightHeight=shot.height;shot.theme=e.boss?save.room:0;projectiles.Add(shot);sound=e.boss?"spell":"arrow"; }
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
        // A penetrating shot can make several genuine hits in the SAME
        // 0.1-tile sweep slice. Roster order is not collision order: an
        // enemy serialized later may physically stand in front of a boss
        // or another opponent. Sort once per flight roster snapshot,
        // and after a real hit refreshes that snapshot, using the signed
        // FIRST circle entry (including each actor's true hit radius).
        // Nonpiercing shots retain their existing earliest-single-target
        // selection; this does not change projectile damage or flight.
        private void OrderPiercingContacts(Projectile shot)
        {
            if(!shot.piercing||projectileTargets.Count<2)return;
            projectileTargets.Sort((left,right)=>
            {
                float leftEntry=ProjectileCircleEntry(shot.position,
                    shot.velocity,left.Position,
                    (left.boss?.9f:.5f)+shot.Radius);
                float rightEntry=ProjectileCircleEntry(shot.position,
                    shot.velocity,right.Position,
                    (right.boss?.9f:.5f)+shot.Radius);
                return leftEntry.CompareTo(rightEntry);
            });
        }

        // The final wall/contact spark uses the SAME launch-owned body
        // identity as the missile and genuine enemy hit, rather than
        // teleporting visually to a neutral white when it meets stone.
        // 0=neutral, 1=hostile, 2=bow, 3=staff, 4=sword-deflected.
        // This transient contact tag never changes damage or SaveData.
        private static int ProjectileContactLight(Projectile p)
        {
            if(!p.friendly)return 1;
            if(p.source=="deflected")return 4;
            if(p.impact!=null&&p.impact.playerOwned)
            {
                if(p.impact.weapon==1)return 2;
                if(p.impact.weapon==2)return 3;
            }
            return 0;
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
        // Share exact hero/escort first-contact and deflection rules across
        // normal flight samples and a shot's final legal sample before cover.
        // The returned body hit consumes the hostile shot unless a sword
        // guard genuinely reflected it. All damage remains in GameCore.
        private bool TryHostileProjectileBodyContact(Projectile p,
            out bool civilianHit, out bool reflected)
        {
            civilianHit=false;reflected=false;
            bool heroContact=(Position-p.position).Length<.38f;
            bool civilianContact=p.reliefMissionOwned &&
                reliefMissionRoom==save.room &&
                reliefMissionWave>=1 && reliefMissionWave<=3 &&
                (p.position-reliefFollowerPosition).Length<.45f;
            bool civilianFirst=civilianContact&&(!heroContact||
                ProjectileCircleEntry(p.position,p.velocity,
                    reliefFollowerPosition,.45f)+.00001f <
                ProjectileCircleEntry(p.position,p.velocity,Position,.38f));
            // Ordinary hostile arrows, unlike magical Boss projectiles, can
            // strike another raider's actual body before the player. This
            // creates genuine moving cover: no ally damage, kill credit or
            // synthetic reward is minted, and the shooter cannot block its
            // own muzzle. Look up actor REFERENCES, never overlapping IDs.
            // Evaluate on the SAME <=.1-tile flight samples as hero and
            // rescue contacts, and resolve the earliest circle entry even
            // if hit circles overlap on that sample.
            Enemy bodyCover=null;
            float coverEntry=float.MaxValue;
            if(p.visual=="enemyArrow"&&p.hostileShooter!=null)
                foreach(Enemy candidate in enemies)
                {
                    if(candidate==null||candidate.hp<=0f||candidate.boss||
                        Object.ReferenceEquals(candidate,p.hostileShooter)||
                        !CanStand(candidate.Position))continue;
                    float radius=.5f+p.Radius;
                    if((candidate.Position-p.position).Length>=radius||
                        !ClearAttackPath(p.position,candidate.Position))continue;
                    float entry=ProjectileCircleEntry(p.position,p.velocity,
                        candidate.Position,radius);
                    if(entry<coverEntry)
                    {coverEntry=entry;bodyCover=candidate;}
                }
            bool allyReady=CanCompanionCatchRescueArrow(p);
            float allyEntry=allyReady?
                ProjectileCircleEntry(p.position,p.velocity,
                    companionPosition,.4f):float.MaxValue;
            float heroEntry=heroContact?
                ProjectileCircleEntry(p.position,p.velocity,
                    Position,.38f):float.MaxValue;
            float civilianEntry=civilianContact?
                ProjectileCircleEntry(p.position,p.velocity,
                    reliefFollowerPosition,.45f):float.MaxValue;
            if(bodyCover!=null&&
                coverEntry+.00001f<heroEntry&&
                coverEntry+.00001f<civilianEntry&&
                coverEntry+.00001f<allyEntry)
            {
                effects.Add(new Effect("number",bodyCover.Position,.5f,
                    L("箭被前排挡下","FRONTLINE COVER")) {
                        reliefMissionOwned=p.reliefMissionOwned,
                        outerPatrolOwned=p.outerPatrolOwned });
                return true;
            }
            // Interception is a swept-body collision, never an abstract
            // mission perk. Preserve the original first-actor wins rule:
            // a planted ally cannot absorb an arrow that reached the hero
            // or civilian earlier along the SAME physical flight sample.
            if(allyReady)
            {
                bool firstToAlly=(!heroContact||allyEntry+.00001f<
                    ProjectileCircleEntry(p.position,p.velocity,
                        Position,.38f))&&
                    (!civilianContact||allyEntry+.00001f<
                    ProjectileCircleEntry(p.position,p.velocity,
                        reliefFollowerPosition,.45f));
                if(firstToAlly)
                {
                    RegisterCompanionRescueArrowBlock();
                    return true;
                }
            }
            if(civilianFirst)
            {
                DamageReliefCivilianArrow(p);
                civilianHit=true;
                return true;
            }
            if(!heroContact)return false;
            if(civilianContact)
            {
                effects.Add(new Effect("number",Position,.7f,
                    L("护卫拦截","INTERCEPT")){reliefMissionOwned=true});
                effects.Add(new Effect("discover",
                    reliefFollowerPosition,.35f){reliefMissionOwned=true});
            }
            reflected=TryDeflectHostileProjectile(p);
            if(!reflected)
                Hurt(p.damage,p.source??"shot",p.velocity.Normalized);
            return true;
        }
        // Shared by ordinary swept flight and last-legal-sample
        // wall-edge body contacts. Establish rescue eligibility BEFORE Hit:
        // successful HP damage itself sets hitTime and may interrupt state.
        private void HitFriendlyProjectileContact(Projectile shot,Enemy victim)
        {
            bool covered=RescueCoverContactEligible(shot,victim);
            Hit(victim,shot.damage,shot.technique,
                shot.velocity.Normalized,shot.impact);
            if(covered)FinishCompanionRescueCoverContact(shot,victim);
        }
        private void UpdateProjectiles(float dt)
        {
            tickingProjectiles.Clear();tickingProjectiles.AddRange(projectiles);
            foreach (Projectile p in tickingProjectiles)
            {
                if (Dying) break;
                if (!projectiles.Contains(p)) continue;
                // A malformed live projectile must not poison the swept
                // collision clock, overflow its subdivision count or draw
                // a phantom impact at a NaN world coordinate. Keep finite
                // stationary contact projectiles valid (mission mechanics
                // use them), but reject nonphysical speed and damage before
                // touching life, effects, enemies or any quest ownership.
                float speed=p.velocity.Length;
                if(!Finite(p.position.x)||!Finite(p.position.y)||
                    !Finite(p.velocity.x)||!Finite(p.velocity.y)||
                    !Finite(speed)||speed>256f||
                    !Finite(p.life)||!Finite(p.age)||
                    !Finite(p.damage)||p.damage<0f)
                {
                    projectiles.Remove(p);
                    continue;
                }
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
                if (p.friendly)
                {
                    projectileTargets.Clear();
                    projectileTargets.AddRange(enemies);
                    OrderPiercingContacts(p);
                }
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
                        // The leading edge can touch a wall AFTER the arrow
                        // is already inside one or more exposed hurt circles.
                        // Resolve those true body contacts from the last legal
                        // flight sample before consuming the bolt on stone.
                        // Cover still blocks every concealed actor by LOS.
                        if(p.friendly&&!p.explosive&&!startedBlocked)
                        {
                            Enemy firstBody=null;
                            float firstEntry=float.MaxValue;
                            // Piercing contacts were sorted by signed physical
                            // circle entry at this sample. Preserve that order
                            // and per-actor immunity even when the NEXT sample
                            // would hit cover. Ordinary arrows choose only
                            // the physically first existing body instead.
                            foreach(Enemy candidate in projectileTargets)
                            {
                                if(candidate.hp<=0||p.hitActors.Contains(candidate))
                                    continue;
                                float radius=(candidate.boss?.9f:.5f)+p.Radius;
                                if((candidate.Position-p.position).Length>=radius||
                                    !ClearAttackPath(p.position,candidate.Position))
                                    continue;
                                if(p.piercing)
                                {
                                    p.hitActors.Add(candidate);p.hits.Add(candidate.id);
                                    HitFriendlyProjectileContact(p,candidate);
                                    if(!projectiles.Contains(p))break;
                                    if(p.slowing)ApplyEnemyControl(candidate,.45f);
                                }
                                else
                                {
                                    float entry=ProjectileCircleEntry(p.position,
                                        p.velocity,candidate.Position,radius);
                                    if(entry<firstEntry)
                                    {firstEntry=entry;firstBody=candidate;}
                                }
                            }
                            if(firstBody!=null)
                            {
                                p.hitActors.Add(firstBody);
                                p.hits.Add(firstBody.id);
                                HitFriendlyProjectileContact(p,firstBody);
                                if(projectiles.Contains(p)&&p.slowing)
                                    ApplyEnemyControl(firstBody,.45f);
                                // The existing projectileImpact below is
                                // rooted at the previous unblocked sample.
                                break;
                            }
                        }
                        // Hostile shots can likewise overlap a real hero
                        // or escorted civilian BEFORE their leading edge
                        // reaches wall cover. Use the same shared body priority
                        // and sword-reflection transaction as ordinary flight.
                        if(!p.friendly&&!startedBlocked)
                        {
                            bool civilianAtWall,reflectedAtWall;
                            if(TryHostileProjectileBodyContact(p,
                                out civilianAtWall,out reflectedAtWall))
                            {
                                civilianImpactProduced=civilianAtWall;
                                if(reflectedAtWall)dead=false;
                                break;
                            }
                        }
                        // A projectile already buried at the START of this frame
                        // (including a gate/tide that sealed around an
                        // in-flight bolt) cannot mint a free area blast.
                        // A genuine unblocked bolt striking the NEXT wall
                        // sample keeps its original near-face explosion.
                        if (p.friendly && p.explosive && !startedBlocked)
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
                                if (candidate.hp <= 0 || p.hitActors.Contains(candidate))
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
                            if (p.hitActors.Contains(e) || e.hp <= 0 || (e.Position - p.position).Length >= (e.boss ? .9f : .5f) + p.Radius || !ClearAttackPath(p.position, e.Position)) continue;
                            p.hitActors.Add(e);
                            p.hits.Add(e.id);
                            hitAny = true;
                            HitFriendlyProjectileContact(p,e);
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
                            OrderPiercingContacts(p);
                        }
                    }
                    else
                    {
                        bool struckCivilian,reflected;
                        if(TryHostileProjectileBodyContact(p,
                            out struckCivilian,out reflected))
                        {
                            civilianImpactProduced=struckCivilian;
                            if(reflected)break; // keep the REAL reflected shot
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
                        variant = ProjectileContactLight(p),
                        radius = impactRadius, height = p.height,
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
            // Map records are shared by rendering, proximity selection and
            // quest interaction. A malformed kind or coordinate must be
            // ignored consistently before any StartsWith/path arithmetic.
            if (o == null || String.IsNullOrEmpty(o.kind) ||
                !Finite(o.x) || !Finite(o.y)) return false;
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
        private bool HasPhysicalInteractionReach(Point objective)
        {
            float distance=(Position-objective).Length;
            if(!Finite(distance)||distance>=1.75f)return false;
            // Check the path to interaction reach, not through the object's
            // own tile: some authored props sit inside solid decorative cells.
            Point contact=distance>.55f?
                objective-(objective-Position).Normalized*.55f:Position;
            return ClearAttackPath(Position,contact);
        }
        private bool IsObjectFromAnotherMap(WorldObject o)
        {
            if (o == null) return false;
            MapData current = Map;
            // Fast-path the overwhelmingly common case. Nearest() checks every
            // object in the active map; scanning every other region first would
            // multiply that interaction cost by the entire campaign size.
            if (current != null && current.objects != null)
                foreach (WorldObject candidate in current.objects)
                    if (System.Object.ReferenceEquals(candidate, o)) return false;
            foreach (MapData other in data.maps)
            {
                if (other == null || System.Object.ReferenceEquals(other, current) ||
                    other.objects == null) continue;
                foreach (WorldObject candidate in other.objects)
                    if (System.Object.ReferenceEquals(candidate, o)) return true;
            }
            // The abyss is a separately streamed MapData, outside data.maps.
            if (abyssMap != null && !System.Object.ReferenceEquals(abyssMap, current) &&
                abyssMap.objects != null)
                foreach (WorldObject candidate in abyssMap.objects)
                    if (System.Object.ReferenceEquals(candidate, o)) return true;
            // Some valid interactions use transient, runtime-authored objects
            // (and test fixtures); absence from a map is not proof of staleness.
            return false;
        }
        private bool HasPhysicalInteractionReach(WorldObject o)
        {
            if(o==null||!IsObjectPresent(o)||IsObjectFromAnotherMap(o))return false;
            return HasPhysicalInteractionReach(new Point(o.x,o.y));
        }
        private bool EnsurePhysicalInteractionReach(WorldObject o)
        {
            if (HasPhysicalInteractionReach(o)) return true;
            Notify("请先走近目标再交互。","Move closer to the target before interacting.");
            return false;
        }
        // A shared physical selection score for fixed map objects AND
        // temporary mission actors. The E prompt and the actual interaction
        // must resolve the same look-direction preference.
        private float InteractionTargetScore(Point target,Point facing)
        {
            Point delta=target-Position;
            float d=delta.Length;
            if(!Finite(d))return float.MaxValue;
            if(d<=.25f)return d-.45f;
            float face=(delta.x*facing.x+delta.y*facing.y)/d;
            return d-.30f*face;
        }
        public WorldObject Nearest()
        {
            WorldObject[] objects = Map.objects;
            // Draw/recovery screens may intentionally have no active world
            // objects (or a stripped decoration-only map). Nothing can
            // be interacted with, so do not query terrain collision at all.
            if (objects == null) return null;
            if (objects.Length == 0) return null;
            WorldObject best=null;
            float bestScore=float.MaxValue;
            Point facing=aim.Normalized;
            bool validatedHeroFoot=false;
            foreach(WorldObject o in objects)
            {
                // E and the on-screen E prompt use this SAME resolved source.
                // Several actual NPCs, records and interactive props stand
                // within a single 1.75-unit reach circle. Resolving equal
                // feet only by authored map-array order opened NPC B when the
                // hero was looking directly at NPC A.
                if(!IsObjectPresent(o))continue;
                // Malformed/streamed maps may contain only null or invalid
                // records and even lack optional decor. Filtering them must
                // be safe BEFORE running any terrain/house-collider query.
                // Once there is a real actor, reject E from buried feet.
                if(!validatedHeroFoot)
                {
                    if(!CanStand(Position))return null;
                    validatedHeroFoot=true;
                }
                if(!HasPhysicalInteractionReach(new Point(o.x,o.y)))continue;
                // Face preference never overrides a nearly-overlapping
                // actor; identical logic is used by moving rescue civilians.
                float score=InteractionTargetScore(new Point(o.x,o.y),facing);
                if(score<bestScore-.0001f)
                {
                    best=o;
                    bestScore=score;
                }
            }
            return best;
        }
        // A moving rescued NPC has no Map object. It may offer its own
        // actions through E only when it is actually the nearest physical
        // interaction. In earlier rescue waves the old override ignored a
        // closer authored NPC, replacing the speaker and all menu choices.
        public bool ReliefCivilianIsInteractionTarget(WorldObject nearby)
        {
            // The ordinary draw path probes this without an active rescue.
            // Do not touch collision for a decorative/noninteractive map.
            if(reliefMissionRoom!=save.room ||
                reliefMissionWave<1 || reliefMissionWave>3)return false;
            if(!CanStand(Position))return false;
            float d=(Position-reliefFollowerPosition).Length;
            if(d>=1.7f || !ClearAttackPath(Position,reliefFollowerPosition))
                return false;
            if(nearby==null)return true;
            Point nearbyPoint=new Point(nearby.x,nearby.y);
            // The civilian is deliberately staged at the SAME physical
            // location as a regional survey landmark (relay1, receiver,
            // bell2). That original map prop remains valid for exploration,
            // but must not steal an actual rescue actor's E menu on contact.
            bool sharedMissionMarker=save.room>=5&&save.room<=7 &&
                nearby.kind==FieldSurveyLandmarks[save.room-5] &&
                (new Point(nearby.x,nearby.y)-reliefFollowerPosition).Length<.35f;
            if(sharedMissionMarker && d<.9f)return true;
            // The moving civilian must compete by the SAME facing-aware
            // score as Nearest(), otherwise the E prompt may point at a
            // merchant while Interact() silently opens the rescue speaker.
            // Retain a small .1 point margin for the map object, so exact
            // overlaps remain stable except for the authored survey landmark.
            Point facing=aim.Normalized;
            float civilianScore=InteractionTargetScore(reliefFollowerPosition,facing);
            float mapScore=InteractionTargetScore(nearbyPoint,facing);
            return civilianScore<mapScore-.1f;
        }
        public void Interact()
        {
            if (mode == "fish") { FishHit(); return; }
            if (mode != "world" || Dying) return;
            // E cannot commit dialogue/quest actions while the hero's
            // planted feet occupy newly sealed ground. This mirrors the
            // existing physical guards on LMB, Q and dodge. Scripted
            // dialogue from authorized story transitions is unaffected.
            if(!CanStand(Position))
            {
                WarnBlockedFooting("脚下地形已封闭，先回到可站立位置再交谈。",
                    "Footing is blocked. Reach walkable ground before interacting.");
                return;
            }
            WorldObject o = Nearest();
            if(ReliefCivilianIsInteractionTarget(o))
            {
                if(reliefMissionWave==1 || reliefMissionWave==2)
                    TalkReliefFieldGuard();
                else
                    TalkRescuedCivilian();
                return;
            }
            if (o == null) { Notify("靠近居民、路牌或发光物，再按 E。", "Approach a resident, sign or glowing object, then press E."); return; }
            TutorialAction(1); Talk(o);
        }

        private bool TryTalkWaytrail(WorldObject o)
        {
            if (o.kind != "waytrail") return false;
            int region = save.room;
            if (region < 0 || region > 7) return false;
            // Direct/internal Talk callers must obey the same reach rule as E input.
            if (!HasPhysicalInteractionReach(o))
            {
                Notify("请靠近路标并确保路线畅通，再记录旧路。",
                    "Move within reach of the marker with a clear path before recording the road.");
                return true;
            }
            RecordCivicAftercareWaypoint(o);
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
            string aftercareZh = Has("civic_aftercare_published") &&
                Has("civic_aftercare_waytrail_"+region) ?
                "\n\n本地区的路标复核已列入公开记录，旅人可以据此核对重接后的公共路线。":
                CivicAftercareActive && (region==0||region==3||region==5) ?
                    "\n\n三地巡路复核 "+CivicAftercareWaypointCount+"/3。"+
                    (CivicAftercareWaypointCount>=3?"三处信标齐备，回炉火旅店公布记录。":"本地信标已核验；请继续检查另两地。") : "";
            string aftercareEn = Has("civic_aftercare_published") &&
                Has("civic_aftercare_waytrail_"+region) ?
                "\n\nThis region's route-marker audit is on the public record, so travelers can check the restored civic route against field evidence.":
                CivicAftercareActive && (region==0||region==3||region==5) ?
                    "\n\nThree-region road audit "+CivicAftercareWaypointCount+"/3."+
                    (CivicAftercareWaypointCount>=3?" All markers are verified; return to Hearth Inn to publish.":" This local marker is verified; check the other regions.") : "";
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
                (eastern ? eastZh[region] : southZh[region]) + closingZh + rewardZh + charterZh + aftercareZh,
                (eastern ? eastEn[region] : southEn[region]) + closingEn + rewardEn + charterEn + aftercareEn,
                C("记入旧路星图", "Record in the road chart", "close"));
            return true;
        }

        public void Talk(WorldObject o)
        {
            // A stale reference from a previous room must never impersonate
            // an interactable at coincident coordinates in the current map.
            if (!IsObjectPresent(o) || IsObjectFromAnotherMap(o)) return;
            if (practicing) { Notify("先完成或结束训练，再与居民交谈。", "Finish or end practice before talking to residents."); return; }
            Conversation previousConversation = conversation;
            conversationSource = o;
            conversationSourceRoom = save.room;
            conversationSourceWasMapObject = SourceBelongsToActiveMap(o);
            conversationSourceWasReachable = HasPhysicalInteractionReach(o);
            try
            {
                TalkCore(o);
                // Keep every authored quest branch/choice intact, while
                // letting two familiar, in-world speakers explain the chain
                // of events in plain language at any stage of the journey.
                if (mode == "dialog" && conversation != previousConversation &&
                    (o.kind == "guild" || o.kind == "innGuest"))
                    AppendStoryBridgeChoice();
                // This applies regardless of which preexisting shelter
                // conversation owns the source (civic duties included).
                if(mode=="dialog"&&conversation!=previousConversation&&
                    o.kind=="farshelter")AppendSoupRouteChoice(o);
            }
            finally
            {
                // Immediate interactions (runes, notes, etc.) must not leave a
                // stale source attached to an unrelated dialogue opened later.
                if (mode != "dialog" || conversation == previousConversation)
                {
                    conversationSource = null;
                    conversationSourceRoom = -1;
                    conversationSourceWasMapObject = false;
                    conversationSourceWasReachable = false;
                }
            }
        }

        private void OpenRoutesMap()
        {
            conversation = null;
            conversationSource = null;
            conversationSourceRoom = -1;
            conversationSourceWasMapObject = false;
            conversationSourceWasReachable = false;
            mode = "map";
        }

        private void TalkCore(WorldObject o)
        {
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
                        "\n\n堡垒已承诺无条件的紧急庇护：先让迷路的人有地方过夜，再让居民决定如何修补归途。"):"") + CivicAftercareInnNote(false), "The warden's letter finally found its recipient. I once walked the roads ahead of you.\n\nWatch the roads by day, and the lamps at night. Read a hammer's lift and an archer's bowstring. Coming home with a story matters more than traveling fast." + (Has("survey_complete") ? "\n\nThe chart you left at the inn helped three late travelers avoid the broken bridge. Thank you for keeping the wardens remembered. The chart table now links the three beacons for safe travel." : Has("survey_1") || Has("survey_2") || Has("survey_3") ? "\n\nI hear you found an old road beacon. Once all three signals are recovered, visit the chart table here at the inn." : "") + (Has("dispatch_complete") ? "\n\nYou decoded all three letters. The inn now discounts stew by two coins for those who keep the road open." : Has("dispatch_6") || Has("dispatch_7") || Has("dispatch_8") ? "\n\nYou decoded one old letter. The others may answer its questions." : "") + RoadwardenVigilInnNote(true) +
                (RootsongComplete?"\n\nThe ancient Thornwood trunks are singing again. The scout remembers their three notes guiding children home; the repaired roots now offer travelers a safe pause.":"") +
                (FloodArchiveComplete?(FloodArchiveChoice==1?
                    "\n\nThe once-erased ferry families are now on public rolls. The ferrymen tend the wounded at the restored tideposts.":
                    "\n\nThe ferrymen now teach apprentices using the original tide charts you preserved, making the ebb crossing safer."):"") +
                (LastDirectiveComplete?
                    (LastDirectiveChoice==1?
                        "\n\nThe inn displays the new public review process. Any traveler can challenge an overload order without hiding the evidence.":
                        "\n\nThe citadel has confirmed unconditional emergency refuge: shelter the stranded first, then let residents decide how to rebuild."):"") + CivicAftercareInnNote(true), CivicAftercareInnChoices()); return;
            }
            if (kind.StartsWith("portal"))
            {
                if (!EnsurePhysicalInteractionReach(o)) return;
                Enter(int.Parse(kind.Substring(6))); return;
            }
            if (kind == "routes")
            {
                if (!EnsurePhysicalInteractionReach(o)) return;
                OpenRoutesMap(); return;
            }
            if (kind == "guild")
            {
                if (TalkTriadGuild(o)) return;
                if (save.ending > 0) { Dialog(o.name, o.en,
                    (Has("storm_complete")?
                        "莉雅揉了揉泛红的眼角，桌上堆着几张湿透的供电记录。\n\n“星雨总算过去了。三个地区各有自己的用电安排——我一份也没替他们改。账都贴在告示板上，谁想查都能来。”" +
                        (StormReliefAll?"三位原来的证人还把你捐出的热汤送到了受寒的居民手中。":"") + "\n\n" :
                        Has("storm_started")?"莉雅没抬头，仍在把烛油滴到几份封条上。\n\n“外头的星雨还在下。三地的储备都摆在议事台，谁先领电，得让他们自己商量。我的印章不管这个。”\n\n":"") +
                    (Has("afterlight_complete") ?
                    "“三处公共站终于亮灯了。昨晚有个老人带着孩子从南边回来，摸着灯柱不肯走。”莉雅翻开维修簿。“名字我都记下了。坏一盏，我们就知道该找谁修。”\n\n" :
                    "“城墙那边终于安静了，城外可没闲着。”莉雅朝公会南面指了指。“余光重建的工地还缺人。灯熄了几个月，昨夜又有人摸黑走错路。”\n\n") +
                    "她把一张空白的巡路单推过来：“旅店还有热汤。路上需要什么，先去装好。没人催你，可总有人得出门。”",
                    (Has("storm_complete")?
                        "Lyra rubs her tired eyes. Damp power ledgers cover the desk.\n\n“The starstorm's over. All three regions made their own allocations. I didn't change a single one. The figures are on the noticeboard if anyone wants to check.”" +
                        (StormReliefAll?" The original three witnesses also delivered your hot meals to neighbors suffering in the cold.":"") + "\n\n" :
                        Has("storm_started")?"Lyra keeps her eyes on the wax seals she's fixing.\n\n“The starstorm hasn't passed. The reserves are on the council table. Who gets power first is for those regions to settle. My stamp doesn't decide it.”\n\n":"") +
                    (Has("afterlight_complete") ?
                    "“All three public stations have lights again. An old man came through with a child last night and wouldn't let go of the lamppost.” Lyra opens a maintenance ledger. “I have the repairers' names right here.”\n\n" :
                    "“It's quiet behind the wall for once. Out here? Not so much.” Lyra points south of the guild. “The Afterlight workers need help. The road lamps have been dark for months. Someone got lost there again last night.”\n\n") +
                    "She slides over a blank roadwarden's slip. “There's hot stew at the inn. Take what you need before you go. No one's ordering you out. The roads still need someone.”",
                    C("开始新的远征", "Another expedition", "routes"),
                    C("回顾结局", "Revisit the ending", "epilogue"),
                    C("再见", "Goodbye", "close")); AddRoadCharterGuildChoice(); return; }
                int next = save.stage + 1;
                if (save.stage < 3 && Has("core_" + next))
                    Dialog(o.name, o.en, (Has("witness_complete_"+next) ?
                        new[]{"","莫芽的证词已经到我手里：森林不是敌人，它失去了本该属于自己的自愈力。\n\n","阿菱记录的渡口孩子不再是货运损失的空白项。\n\n","维恩证明矿工停机是救命，而不是违反配额。\n\n"}[next] : "") +
                        "莉雅把星核放进衬着毛毡的铁匣，啪地锁上。\n\n“堡垒的催单比你回来的脚步还快。”她把一杯井水放到你面前，杯底有细沙。“今天才打上来这么点。下一处地方要是也出了事，把账簿和那边的人说过的话一块带回来。”", (Has("witness_complete_"+next) ?
                        new[]{"","Moya's testimony is here. The forest was wounded, not our enemy.\n\n","Arin's children at the crossing will no longer be counted as missing freight.\n\n","Venn proves the miners' shutdown was a safety measure, not misconduct.\n\n"}[next] : "") +
                        "Lyra locks the core inside a felt-lined iron case.\n\n“The citadel's requisition arrived before you did.” She sets down a glass of well water, grit visible at the bottom. “This is all we drew today. If the next region's in the same state, bring its ledger back. And tell me what its people said.”", C("交回星核 · 开启下一章", "Deliver the starcore · next chapter", "deliver"), C("稍后再来", "Later", "close"));
                else Dialog(o.name, o.en, save.stage == 3 ? "莉雅将三份旧账摊在桌上，指尖停在同一天的日期上。\n\n“这边写着堡垒屏障供能充足。旁边是断流的井、停不下来的矿机，还有河上的失踪者名单。厄洛要是说不知道，就让他自己看。”\n\n“杏有伤药，老潮还能送人过河，诺尔总能从废铁里拆出能用的零件。去堡垒之前，远岸那几份原始证词能带多少就带多少。”" : "莉雅看了一眼屋外湿漉漉的路。\n\n“荆棘密林还没信。我不敢再派第二个孩子进去找侦察员了。祭坛旁盘踞着黑荆巨兽——先找到人，听他说那里怎么了。要是能拿到星核，就带回公会，我来收着。”\n\n“桌上这封求援信还没写完。等你回来，我想把最后一行补上。”", save.stage == 3 ? "Lyra spreads three old ledgers across the desk, her finger stopping on the same date.\n\n“This one says the citadel shield was fully powered. The others say wells went dry, the mine couldn't shut down, and people vanished at the crossing. If Orrin says he didn't know, let him read them.”\n\n“An has medicine. Old Tide can still ferry people. Noll makes useful things out of junk iron. Bring as many original farshore accounts as you can before you go.”" : "Lyra glances at the rain-soaked road outside.\n\n“No word from Thornwood. I can't send another youngster in after our scout. The Briar Beast's been seen at the old altar. Find the scout first. Let him tell you what happened there. If you reach the starcore, bring it back to the guild. I'll keep it safe.”\n\n“The plea for help on my desk is unfinished. I'd like to write an answer when you return.”", C("查看冒险路线", "Choose a route", "routes"), C("我出发了", "On my way", "close"));
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
                bool roomForFish=HasInventoryRoom(3,1);
                List<Choice> list = new List<Choice> {
                    roomForFish?
                        C("开始钓鱼", "Cast the line", "fish"):
                        C("鱼袋已满 · 先腾出空间", "Fish bag full · make room", "fish"),
                    HasInventoryRoom(3,2)?
                        C("精准垂钓 · 两条鱼", "Precision cast · two fish", "fish_expert"):
                        C("精准垂钓 · 鱼袋需两格", "Precision cast · need two slots", "fish_expert")
                };
                if (!Has("ally_2")) list.Add(C("支线：交付银鳍鱼 ×3", "Commission: deliver 3 Silverfin", "ally2"));
                else list.Add(C(save.inventory[3]>=2&&save.inventory[6]<9999?
                    "公共鱼汤 · 鱼×2 → 炖汤×1":
                    "公共鱼汤 · 需要鱼×2及炖汤空位",
                    save.inventory[3]>=2&&save.inventory[6]<9999?
                    "Public kitchen · 2 fish for 1 stew":
                    "Public kitchen · need 2 fish and stew space","fish_kitchen"));
                if(PublicFishKitchenStocked)
                    list.Add(C("远地送汤 · "+SoupRouteDeliveredCount+"/3",
                        "Remote hot meals · "+SoupRouteDeliveredCount+"/3",
                        "soup_route_track"));
                list.Add(C("再见", "Goodbye", "close"));
                string fishermanZh=Has("ally_2")?
                    "岸边的锅里翻着热气。\n\n“孩子们昨晚总算喝上了鱼汤。现在你若再抛竿，听见水面扑通一响就收线，别让鱼儿叼着钩跑了。”":
                    save.inventory[3]>=3?
                    "岸边的人从锅旁起身，瞧见你带来的鱼，赶紧腾出干净的木桶。\n\n“三条都齐了？快给我吧，避难所的孩子们还等着开饭。”":
                    "岸边的锅里只漂着几片菜叶。\n\n“避难所今晚又来了孩子，我还缺三条银鳍鱼。听见鱼咬钩就快收线，浮标落进青色那段才能勾牢。稳住三次，才捞得上一条。”";
                string fishermanEn=Has("ally_2")?
                    "Steam rises from the pot on the bank.\n\n'The children finally had fish stew last night. If you cast again, reel when you hear the splash. The fish won't wait long.'":
                    save.inventory[3]>=3?
                    "The fisher looks up from the pot, sees the fish you brought and clears a clean bucket.\n\n'All three? Hand them over. The children at the shelter are waiting for supper.'":
                    "A few leaves drift in the cooking pot.\n\n'More children reached the shelter tonight. I still need three Silverfin. Reel when you hear the splash, with the float inside the teal band. Three good pulls will land one fish.'";
                fishermanZh+="\n\n“想试试树桩边那股急水？青色窗口窄得多。三次钩准，能拉上两条；没有鱼饵也能试。”";
                fishermanEn+="\n\n'Try the swift water by the stump? The teal window is narrower. Land three hooks and you can pull in two fish. No bait needed.'";
                if(Has("ally_2"))
                {
                    fishermanZh+=PublicFishKitchenStocked?
                        "\n\n“公用的汤锅又满了。你带两条鱼来，我给你留一碗炖汤；厨房每次都多分些给需要的人。”":
                        "\n\n“如果再带两条鱼，公用汤锅就能重新开火。我会给你留一碗炖汤。往后同样的食材，大家能做得更好。”";
                    fishermanEn+=PublicFishKitchenStocked?
                        "\n\n'The shared pot is simmering again. Bring me two Silverfin and I'll save you a bowl. We keep enough for those still hungry.'":
                        "\n\n'Bring two more Silverfin and we can restart the public kitchen. I'll set aside a bowl for you. Once we have a steady stock, every stew goes further.'";
                }
                if(PublicFishKitchenStocked)
                {
                    int carried=SoupRouteDeliveredCount;
                    if(SoupRouteComplete)
                    {
                        fishermanZh+="\n\n“密林、雾港和霜钟的守棚人都报了平安。远路有人能喝上热汤了。下一锅还在火上，你想送给谁就带去。”";
                        fishermanEn+="\n\n'Thornwood, Mistport and Frostchime all sent word. Their keepers have a hot pot on the road now. There's another bowl on the fire if you need it.'";
                    }
                    else
                    {
                        fishermanZh+="\n\n“我托人问过路了。三处远地避险棚，已有"+
                            carried+"处收到了汤。剩下的在密林、雾港和霜钟高原，得亲手送到棚屋才行。”";
                        fishermanEn+="\n\n'I asked the road wardens. "+carried+
                            " of three far shelters have received stew. The others are out in Thornwood, Mistport and Frostchime. Deliver each bowl to the keeper yourself.'";
                    }
                }
                Dialog(o.name,o.en,fishermanZh,fishermanEn,list.ToArray());return;
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
            if (kind == "cook") { Dialog(o.name, o.en, "暖潮炖汤：鱼1、药草2。回复"+StewHealing+"生命与耐力，增加攻击"+(int)StewBuffSeconds+"秒。F喝汤。"+(PublicFishKitchenStocked?"\n\n湖畔公共厨房已经恢复供应，所有炖汤都比过去更耐用。":"")+"\n\n星薄荷凉茶：药草2、星晶1。T喝茶，抗暑抗寒并增加耐力回复180秒。", "Stew: 1 fish, 2 herbs. Heals "+StewHealing+", refills energy and boosts damage for "+(int)StewBuffSeconds+" seconds. F drinks it."+(PublicFishKitchenStocked?"\n\nOld Tide's public kitchen is supplied again; all Tide stew goes further now.":"")+"\n\nTea: 2 herbs, 1 crystal. T drinks it for climate protection and faster energy recovery, 180 seconds.", C("制作炖汤", "Cook stew", "cook"), C("买炖汤 · " + ShopStewCost + " 硬币", "Buy stew · " + ShopStewCost + " coins", "buymeal"), C("买木材 ×4 · " + ShopTimberCost + " 硬币", "Buy timber ×4 · " + ShopTimberCost + " coins", "buywood"), C("制作凉茶", "Brew tea", "tea"), C("再见", "Goodbye", "close")); return; }
            if (kind == "scout")
            {
                if (Has("scout_saved")) { Notify("侦察员已经安全离开。", "The scout reached safety."); return; }
                if (save.kills[1] < 4) { Dialog(o.name, o.en, "侦察员攥着一根断裂的木棍，手背都是血。\n\n“路口那几只东西疯了一样撞树。我试过跑，差点没回来。你能把它们引开吗？安全了我再指给你看祭坛的路。”", "The scout grips a broken branch, blood on his knuckles.\n\n“Those beasts keep throwing themselves against the trees. I tried running. Barely made it back. Can you get them off the road? I'll show you the way to the altar when it's clear.”", C("我来解决", "I will clear the road", "close")); return; }
                Dialog(o.name, o.en, "侦察员靠着树喘了半天，才把皱巴巴的测绘纸从衣襟里摸出来。\n\n“你看这两道口子。不是兽爪，是镐头。我跟着它们一路摸到星根下面，那里原本该给树林供能，现在却有条线直通堡垒的屏障。难怪根都枯了。”\n\n他往祭坛方向看了一眼。\n\n“黑荆巨兽没追我，它一直守着伤口。莉雅得知道这件事，我回公会去告诉她。你要是进得去祭坛，把那枚星核带回来，别让人把痕迹都擦掉。”", "The scout leans against a tree for a while before pulling a crumpled survey sheet from his coat.\n\n“See these cuts? Not claws. Pickaxe marks. I followed them down to the starroot. That flow used to feed the forest. Now there's a line running straight to the citadel's shield. No wonder the roots have gone dry.”\n\nHe looks toward the altar.\n\n“The Briar Beast didn't chase me. It stayed by the wound. Lyra needs to hear this. I'll tell her at the guild. If you can get into the altar, bring back the core before someone scrapes all the evidence away.”", C("送侦察员回镇报告", "Send the scout to report", "scout")); return;
            }
            if (kind.StartsWith("rune"))
            {
                if (!EnsurePhysicalInteractionReach(o)) return;
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
                if (!EnsurePhysicalInteractionReach(o)) return;
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
                if (!EnsurePhysicalInteractionReach(o)) return;
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
                if (!EnsurePhysicalInteractionReach(o)) return;
                if (GateOpen && GateBarrierActive) { Notify("道路正在打开，等屏障消散后再进入祭坛。", "The road is opening; wait for the barrier to dissolve before entering the altar."); return; }
                if (save.room == 4 && Has("boss_4")) { FinalConversation(); return; }
                if (!GateOpen) { Notify(GateHint(false), GateHint(true)); return; }
                if (Has("boss_" + save.room))
                {
                    bool newlyFound = !Has("core_" + save.room); Flag("core_" + save.room);
                    if (newlyFound) RecordMoment("获得探索能力：" + new[] { "", "藤封共鸣", "浅水接通", "断桥修复" }[Math.Min(3, save.room)] + "。旧地区出现新的支路。", "Exploration power gained: " + new[] { "", "Grove resonance", "Tidal connection", "Bridge repair" }[Math.Min(3, save.room)] + ". Revisit old regions for sealed routes.");
                    Dialog("星核祭坛", "Starcore altar", "祭坛的齿轮一枚枚停下。星核从底座升起，表面还缠着细细的根须。刻在石台边缘的调谐数字有几处被人凿掉了。\n\n你用布裹住星核。莉雅的公会有保存旧记录的铁柜，那里至少能让这块石头和这些痕迹留到明天。", "The altar's gears slow to a stop. The starcore rises from its cradle, fine roots still clinging to its surface. Someone has chiseled several calibration figures out of the rim.\n\nYou wrap the core in cloth. Lyra keeps the old records behind an iron cabinet door. At least there it might survive the night beside the evidence.", C("返回余烬镇", "Return to Emberfall", "goto0"), C("继续探索", "Keep exploring", "close")); sound = "reward"; return;
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
            get { return SpecialistDrillActive || SwordLessonActive || FrostSignalActive || MineConvoyActive || FarCourierActive || CivicResponseActive || AfterlightWatchActive || OuterPatrolActive || stormTuning ||
                afterlightChargeRegion != 0 || blackoutActive || reliefMissionRoom != 0 ||
                // Rest jumps world time but does not advance these activity clocks.
                racing || fieldSurveyRoom != 0 ||
                TideSalvageActive || FloodReliefActive || LastDirectiveWave > 0 || CivicConduitActive ||
                // A lull between scripted enemy waves is not a break in the
                // physical trial. Resting there would skip its capture clock,
                // red warnings and challenge phases for free.
                GlasswindSite != 0 || TemperActive || TriadDefenseActive ||
                witnessDefenseRoom != 0 || commonsRelayRoom != 0 ||
                RoadwardenTrialRoom != 0 || WayfarerEncounterRoom != 0 || RootHarmonyRoom != 0 ||
                SkywatchRoom != 0 || AtlasEchoRoom != 0; }
        }

        private static bool RequiresWorldSourceReach(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            // Choices whose handlers do not independently validate their target.
            if (action.StartsWith("goto") || action == "routes" ||
                action == "rest" || action == "scout" || action == "engine" ||
                action == "plant" || action == "harvest" || action == "seeds" ||
                action == "upgrade" || action == "bow" || action == "staff" ||
                action == "cook" || action == "buymeal" || action == "buywood" ||
                (action == "fish" || action == "fish_expert" || action == "fish_kitchen" ||
                 action=="soup_route_track" || action=="soup_route_deliver") || action == "deliver" ||
                action.StartsWith("ally") || action == "boss" ||
                action.StartsWith("ending") || action == "epilogue" ||
                action == "continue" || action == "swordlesson_start" ||
                action == "swordlesson_veteran_start" || action == "specialist_drill_start" ||
                action == "specialist_drill_abort" || action == "practice" ||
                action == "scoutgift" || action == "survey_finish" ||
                action == "survey_home" || action.StartsWith("survey_to_") ||
                action == "dispatch_recap" || action == "farshore_relief_routes" ||
                action == "farshore_relief_return" ||
                action.StartsWith("farshore_relief_to_")) return true;

            // Expansion dialogue actions without their own physical target guard.
            return action == "exp_begin" || action == "camp" ||
                action == "companions" || action == "parley" ||
                action.StartsWith("pledge") ||
                action.StartsWith("rescue") || action.StartsWith("relay") ||
                action == "holdtide" || action == "harbor_report" ||
                action.StartsWith("desert") || action.StartsWith("mountain") ||
                action.StartsWith("report_") || action == "expboss" ||
                action.StartsWith("warm:") || action.StartsWith("brazierRest:") ||
                action.StartsWith("shadeRest:") ||
                action == "tea" ||
                action == "drinktea" || action == "fieldrest" || action == "race" ||
                action.StartsWith("personal") || action.StartsWith("gift") ||
                action.StartsWith("join") || action.StartsWith("covenant") ||
                action == "council_epilogue" ||
                action.StartsWith("abyssStart") || action == "abyssLeave" ||
                action == "abyssBank" || action.StartsWith("boon");
        }

        public void Choose(int index)
        {
            if (mode != "dialog" || conversation == null || index < 0 || index >= conversation.choices.Length) return;
            if(Dying){Close();return;}
            string action = conversation.choices[index].action;
            // A stale menu opened from a reachable source cannot survive leaving it.
            // For a menu first opened remotely, gate only choices without their own
            // live target validation. Re-check current reach so approaching a source
            // or opening a blocked path restores a legitimate action.
            bool sourceStillPresent = conversationSource == null ||
                (conversationSourceRoom == save.room &&
                 IsObjectPresent(conversationSource) &&
                 !IsObjectFromAnotherMap(conversationSource) &&
                 (!conversationSourceWasMapObject ||
                  SourceBelongsToActiveMap(conversationSource)));
            bool sourceReachableNow = sourceStillPresent &&
                conversationSource != null &&
                HasPhysicalInteractionReach(conversationSource);
            if (conversationSource != null && action != "close" &&
                (!sourceStillPresent ||
                 (conversationSourceWasReachable && !sourceReachableNow) ||
                 (RequiresWorldSourceReach(action) && !sourceReachableNow)))
            {
                bool initiallyReachable = conversationSourceWasReachable;
                Close();
                Notify(initiallyReachable ?
                    "你已离开交互范围，请重新靠近目标并交谈。" :
                    "请先走近目标，再选择交互操作。",
                    initiallyReachable ?
                    "You left interaction range. Return to the target and talk again." :
                    "Move closer to the target before choosing this interaction.");
                return;
            }
            WorldObject selectedSource = conversationSource;
            int selectedSourceRoom = conversationSourceRoom;
            bool selectedSourceWasMapObject = conversationSourceWasMapObject;
            bool selectedSourceWasReachable = conversationSourceWasReachable;
            Close();
            pendingDialogueSource = selectedSource;
            pendingDialogueSourceRoom = selectedSourceRoom;
            pendingDialogueSourceWasMapObject = selectedSourceWasMapObject;
            pendingDialogueSourceWasReachable = selectedSourceWasReachable;
            try { ChooseCore(action); }
            finally
            {
                pendingDialogueSource = null;
                pendingDialogueSourceRoom = -1;
                pendingDialogueSourceWasMapObject = false;
                pendingDialogueSourceWasReachable = false;
            }
        }

        private void ChooseCore(string action)
        {
            if (ChooseSwordLesson(action)) return;
            if (ChooseSpecialistDrill(action)) return;
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
            if (action == "routes") { OpenRoutesMap(); return; }
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
            if (action == "fish_kitchen")
            {
                // This is a real-world handoff at Old Tide's original
                // fishing station. Direct/forged ChooseCore calls and
                // cached choices at an unrelated NPC must not exchange fish.
                WorldObject source=pendingDialogueSource;
                if(save.room!=0||!Has("ally_2")||Dying||practicing||
                    source==null||source.kind!="fish"||
                    pendingDialogueSourceRoom!=save.room||
                    !SourceBelongsToActiveMap(source)||
                    !HasPhysicalInteractionReach(source))return;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("现场救援或附近敌人未解除，暂时无法交付鱼获。",
                        "Finish fieldwork and clear nearby danger before supplying the kitchen.");
                    return;
                }
                // Check capacity BEFORE spending any fish; a full stew
                // stack must never swallow precious earned catches.
                if(!HasInventoryRoom(6,1)||!Spend(0,0,0,2,0))return;
                save.inventory[6]++;revision++;sound="reward";
                if(!PublicFishKitchenStocked)
                {
                    Flag("fish_kitchen_supplied");
                    RecordMoment("老潮用你交来的两条银鳍鱼重新点燃了湖畔公共汤锅。镇上的炖汤从此能照顾更多赶路的人。",
                        "Old Tide reopened the lakeside public kitchen with your two Silverfin. Every bowl of stew can now sustain travelers longer.");
                    Notify("公共厨房重新开火：炖汤×1！以后炖汤恢复65生命，攻击增益持续105秒。",
                        "Public kitchen reopened: +1 stew! Future stew heals 65 HP and boosts damage for 105s.");
                }
                else Notify("交付银鳍鱼×2，换得暖潮炖汤×1。公用汤锅继续为镇民开火。",
                    "Supplied two Silverfin for one Tide stew. The public kitchen keeps feeding the town.");
                return;
            }
            if (action == "fish" || action == "fish_expert")
            {
                bool expert=action=="fish_expert";
                int required=expert?2:1;
                if(!HasInventoryRoom(3,required))
                {
                    Notify(expert?"精准垂钓需要两个鱼袋空位。":"鱼袋已满。先腾出一格，才有地方装上钩的鱼。",
                        expert?"Precision fishing needs two free fish slots.":
                               "Your fish bag is full. Make room before casting.");
                    return;
                }
                FishExpertCast=expert;
                fishTime = fishCursor = 0; fishHits = fishMisses = 0;
                fishNextBiteAt = .3f;
                fishBiteExpiresAt = fishNextBiteAt + FishBiteWindow;
                FishFeedbackZh = FishFeedbackEn = "";
                mode = "fish";
            }
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
            save.inventory[6]--;
            save.health=Clamp(save.health+StewHealing,0,save.maxHealth);
            save.energy=100;
            save.foodUntil=save.totalTime+StewBuffSeconds;
            revision++;sound="reward";
            Notify("生命 +"+StewHealing+"，耐力回满，攻击增强 "+(int)StewBuffSeconds+" 秒。",
                "+"+StewHealing+" health, full energy, stronger attacks for "+(int)StewBuffSeconds+" seconds.");
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
        private void FishMissedBite(bool timedOut = false)
        {
            if(timedOut)
            {
                FishFeedbackZh = "鱼松开了钩。下一次咬钩要及时收线。";
                FishFeedbackEn = "The fish let go. Reel promptly on the next bite.";
            }
            fishMisses++;
            sound = "hurt";
            fishNextBiteAt = fishTime + .34f;
            fishBiteExpiresAt = fishNextBiteAt + FishBiteWindow;
        }
        public void FishHit()
        {
            if (!FishLineReady) return;
            // Only an actual fish bite can resolve an input. Holding E,
            // pressing gamepad A and clicking Reel in the same frame cannot
            // fake three catches or exhaust the three misses instantly.
            bool hooked = Math.Abs(fishCursor - FishTarget) <= FishWindowRadius;
            if (hooked)
            {
                FishFeedbackZh = "鱼线绷紧了！再稳住下一次咬钩。";
                FishFeedbackEn = "Good hook! Hold steady for the next bite.";
                fishHits++; sound = "reward";
                fishNextBiteAt = fishTime + .34f;
                fishBiteExpiresAt = fishNextBiteAt + FishBiteWindow;
            }
            else
            {
                bool left=fishCursor<FishTarget;
                FishFeedbackZh=left?"浮标偏左，没能钩住。":"浮标偏右，没能钩住。";
                FishFeedbackEn=left?"The float was left of the bite.":"The float was right of the bite.";
                FishMissedBite();
            }
            if (fishHits >= 3)
            {
                // Normal play enters this minigame only when a fish slot was
                // available. Keep the final reward safe if a developer or
                // future UI changes the inventory during the reel sequence.
                bool expert=FishExpertCast;
                int caught=Math.Max(0,Math.Min(FishCatchReward,
                    9999-save.inventory[3]));
                if(caught>0)
                {
                    save.inventory[3]+=caught;
                    save.coins=Math.Min(999999,save.coins+caught*4);
                    BountyEvent(2,caught);
                }
                Close();revision++;
                Notify(caught==0?"鱼袋已满，这次钓获未能收下。":
                        "钓到银鳍鱼 ×"+caught+(expert?"！精准垂钓成功。":"！"),
                       caught==0?"Your fish bag is full; no catch was stored.":
                        "Caught "+caught+" Silverfin"+(caught==1?"":"")+"!"+
                        (expert?" Precision cast complete.":""));
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
