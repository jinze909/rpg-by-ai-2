using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Advance(GameCore g, float seconds)
        { while (seconds > .00001f) { float step = Math.Min(.02f, seconds); g.Tick(step, new Point()); seconds -= step; } }
        private static void AwaitArenaOpening(GameCore g)
        { for(int frame=0;frame<100&&g.GateBarrierActive;frame++)g.Tick(.02f,new Point());Check(!g.GateBarrierActive,"The objective gate must finish opening before its Boss challenge"); }
        private static GameCore Fresh(Campaign data, int profession = 0)
        { var g = new GameCore(data); g.NewGame(profession); g.Close(); return g; }
        private static WorldObject Object(GameCore g, string kind)
        { foreach (var o in g.Map.objects) if (o.kind == kind) return o; throw new Exception("Missing object " + kind); }
        private static void Action(GameCore g, string kind, string action)
        {
            g.Talk(Object(g, kind));
            Check(g.mode == "dialog", "Expected dialogue at " + kind);
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing action " + action);
        }
        public static void SealedPursuitRoutesToLegalFrontierAndReopensImmediately(Campaign data)
        {
            // Keep all authored campaign maps and story objects unchanged.
            // Temporarily substitute a miniature blocked arena for a real
            // GameCore to exercise the production DirectionTo + Fits path.
            MapData original = data.maps[0];
            try
            {
                string[] rows = new string[16];
                for (int y = 0; y < rows.Length; y++)
                {
                    char[] cells = new string('.', 16).ToCharArray();
                    for (int x = 0; x < 16; x++)
                        if (x < 2 || x >= 14 || y < 2 || y >= 14) cells[x] = '#';
                    if (y >= 2 && y < 14) cells[7] = '#';
                    rows[y] = new string(cells);
                }
                data.maps[0] = new MapData {
                    id = 0, spawnX = 4.5f, spawnY = 8.5f,
                    rows = rows, objects = new WorldObject[0],
                    decor = new Decoration[0]
                };
                GameCore g = new GameCore(data);
                Point target = new Point(10.5f, 8.5f);
                Point approach = g.DirectionTo(new Point(4.5f, 8.5f), target);
                Check(approach.x > .9f && Math.Abs(approach.y) < .1f,
                    "Sealed target first draws pursuit toward the nearest reachable legal frontier");
                Point waiting = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(waiting.Length < .001f,
                    "At a sealed frontier the pursuer must not walk straight into the wall or jitter");
                Check(g.NavigationPath(target).Count == 0,
                    "Navigation HUD cannot draw a physically false route through a sealed wall");

                char[] doorway = rows[12].ToCharArray(); doorway[7] = '.';
                rows[12] = new string(doorway);
                Point detour = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(detour.y > .9f && Math.Abs(detour.x) < .1f,
                    "Opening a distant legal corridor immediately makes AI seek its detour");
                for (int pass = 0; pass < 4; pass++)
                {
                    List<Point> route = g.NavigationPath(target);
                    Check(route.Count > 6 &&
                        (route[route.Count - 1] - target).Length < 1.2f,
                        "Shared HUD and combat BFS workspace must not retain stale parent links, pass " + pass);
                    Point again = g.DirectionTo(new Point(6.5f, 8.5f), target);
                    Check(again.y > .9f,
                        "Shared BFS generations must never corrupt the following enemy detour");
                }
                char[] crossing = rows[8].ToCharArray(); crossing[7] = '.';
                rows[8] = new string(crossing);
                Point direct = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(direct.x > .9f && Math.Abs(direct.y) < .1f,
                    "Opening a direct bridge immediately replaces the detour without stale navigation");
            }
            finally { data.maps[0] = original; }
        }

        public static void BufferedDodgeKeepsDirectionAndCommitsOnce(Campaign data)
        {
            GameCore g=Fresh(data);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.energy=100;
            g.dashCooldown=.10f;
            g.Dash(new Point(0,-1));
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Early valid dodge cannot spend stamina or start invulnerability");
            g.SetAim(new Point(1,0));
            g.Tick(.04f,new Point());
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Queued dodge started before actual cooldown completion");
            Advance(g,.061f);
            Check(g.dashTime>.17f&&g.invulnerable>.30f&&
                Math.Abs(g.save.energy-80f)<.001f,
                "Exactly one buffered dodge must start on cooldown readiness");
            Point savedDirection=(Point)typeof(GameCore).GetField(
                "dashDirection",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).GetValue(g);
            Check(savedDirection.y<-.99f&&Math.Abs(savedDirection.x)<.01f,
                "Deferred dodge must use original input, not later aim direction");
            g.Tick(.02f,new Point());
            Check(g.save.energy>80f&&g.save.energy<82f,
                "Buffered dodge may not double charge energy on later ticks");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null,"Dodge interrupt requires actual attack");
            g.dashCooldown=.07f;
            g.Dash(new Point(0,1));
            Check(g.playerAction!=null,
                "Queued cooldown dodge cancelled original windup too early");
            Advance(g,.071f);
            Check(g.dashTime>.17f&&g.playerAction==null,
                "Ready buffered dodge must cancel attack and begin real mobility");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(-1,0));
            g.mode="inventory";g.mode="world";
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Closing menu must retire previously buffered dodge");
            g.dashCooldown=.45f;g.Dash(new Point(-1,0));
            Advance(g,.46f);
            Check(g.dashTime<=0f,
                "Dodge input well outside last 140ms cannot execute later");
            g.save.energy=0;g.dashCooldown=.09f;
            g.Dash(new Point(-1,0));
            g.save.energy=100;Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Dodge input without enough energy cannot create a future free action");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.08f;g.Dash(new Point(0,-1));
            g.Attack(new Point(1,0));
            Check(g.playerAction==null&&g.save.energy==100,
                "Held basic attack cannot spend resources before a queued dodge");
            Advance(g,.081f);
            Check(g.dashTime>.17f&&g.save.energy>=80f&&
                g.save.energy<80.1f,
                "Waiting dodge must outrank a simultaneous ordinary attack");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.Skill(new Point(1,0));
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.save.energy==60,
                "Newer deliberate Q must override queued dodge without double payment");
            Advance(g,.10f);
            Check(g.dashTime<=0&&g.playerAction!=null&&
                g.playerAction.kind=="skill",
                "A superseded dodge cannot cancel a newer skill windup");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.ClearBufferedCombatInputs();
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100,
                "Application pause/focus must retire all queued combat inputs");
        }

        public static void BufferedSkillRepeatCannotExtendOrRetargetIntent(Campaign data)
        {
            var g=Fresh(data,1);g.enemies.Clear();
            g.save.energy=100;g.playerHitTime=.55f;
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "A Q pressed during real hit recovery must queue without paying early");
            Advance(g,.12f);
            g.SetAim(new Point(1,0));
            g.Skill(new Point(1,0));
            Check(g.SkillQueued&&g.playerAction==null,
                "Repeated Q cannot force a premature skill through hit recoil");
            Advance(g,.09f);
            Check(!g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "Repeated Q cannot extend the original 200ms input window");
            Advance(g,.4f);
            Check(g.playerAction==null&&g.skillCooldown<=0&&g.save.energy==100,
                "Expired buffered Q cannot become a free delayed spell");

            g=Fresh(data,1);g.enemies.Clear();g.save.energy=100;
            g.Dash(new Point(-1,0));
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.save.energy==80,
                "Dodge then Q must preserve energy until Q is actually accepted");
            Advance(g,.06f);
            Point latestTarget=new Point(21,17);
            g.SetAimTarget(latestTarget);
            g.Skill(new Point(1,0));
            Advance(g,.15f);
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.playerAction.direction.x>.99f&&
                Math.Abs(g.playerAction.direction.y)<.01f&&
                g.playerAction.target.HasValue&&
                (g.playerAction.target.Value-latestTarget).Length<.001f&&
                !g.SkillQueued&&g.skillCooldown>0,
                "Repeated Q must retarget the existing queue, without moving its deadline");
            Check(g.save.energy>=40&&g.save.energy<45,
                "Queued Q commits its cost once, not on every repeated press");
        }

        public static void ClassesAndPause(Campaign data)
        {
            for (int i = 0; i < 3; i++)
            {
                var g = Fresh(data, i); Check(g.save.weapon == i, "Class starter weapon");
                g.save.x = 10; g.save.y = 14; float x = g.save.x;
                g.Tick(.05f, new Point(1, 1)); Check(g.save.x > x, "Movement");
                float time = g.save.totalTime; g.mode = "inventory"; g.Tick(.05f, new Point(1, 0)); Check(g.save.totalTime == time, "Menus must pause the simulation");
                g.Close(); g.save.energy = 100; g.Skill(); Check(g.save.energy == 60 && g.skillCooldown > 0, "Class skill cost");
                g.Skill(); Check(g.save.energy == 60, "Skill cannot repeat during cooldown");
            }
        }
        public static void RepeatablePurchasesCannotConsumeResourcesPastSavedStackCaps(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.coins=500;
            g.save.inventory[0]=9997;g.save.inventory[7]=9998;
            g.save.inventory[6]=9999;g.save.inventory[2]=20;
            g.save.inventory[3]=20;g.save.inventory[1]=20;g.save.tea=999;
            int cash=g.save.coins,wood=g.save.inventory[0],
                seeds=g.save.inventory[7],herbs=g.save.inventory[2],
                fish=g.save.inventory[3],crystal=g.save.inventory[1];
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","cook");
            Action(g,"cook","tea");
            Check(g.save.coins==cash&&g.save.inventory[0]==wood&&
                  g.save.inventory[7]==seeds&&g.save.inventory[6]==9999&&
                  g.save.inventory[2]==herbs&&g.save.inventory[3]==fish&&
                  g.save.inventory[1]==crystal&&g.save.tea==999,
                "Full saved stacks reject timber, seeds, purchased/cooked stew and tea before deducting any ingredients or coins");
            g.save.inventory[0]=9995;g.save.inventory[7]=9996;
            g.save.inventory[6]=9998;g.save.tea=998;
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","tea");
            Check(g.save.inventory[0]==9999&&g.save.inventory[7]==9999&&
                  g.save.inventory[6]==9999&&g.save.tea==999&&
                  g.save.coins==cash-g.ShopTimberCost-5-g.ShopStewCost&&
                  g.save.inventory[2]==herbs-2&&g.save.inventory[1]==crystal-1,
                "Exact last available storage space still permits legitimate paid purchases with correct resource costs");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.inventory[0]==9999&&loaded.save.inventory[7]==9999&&
                  loaded.save.inventory[6]==9999&&loaded.save.tea==999,
                "Full-stack purchases survive v2 save/load without losing paid inventory");
        }
        public static void Transactions(Campaign data)
        {
            var g = Fresh(data); g.save.inventory[1] = 0;
            int wood = g.save.inventory[0], gear = g.save.inventory[4];
            Action(g, "forge", "upgrade"); Check(g.save.inventory[0] == wood && g.save.inventory[4] == gear && g.save.upgrades == 0, "Failed crafting must be atomic");
            g.save.inventory[1] = 10; g.save.inventory[4] = 10; g.save.inventory[0] = 20;
            Action(g, "forge", "upgrade"); Check(g.save.upgrades == 1 && g.save.maxHealth == 140, "Equipment upgrade");
            Action(g, "forge", "bow"); Check(g.Has("weapon_1") && g.save.weapon == 1, "Craft a bow");
            int after = g.save.inventory[0]; Action(g, "forge", "bow"); Check(g.save.inventory[0] == after, "Re-equipping a crafted weapon must be free");
            g.save.inventory[2] = 20; Action(g, "garden", "ally1");
            int coins = g.save.coins; g.Talk(Object(g, "garden"));
            foreach (var c in g.conversation.choices) Check(c.action != "ally1", "Completed quest cannot reward twice");
            Check(g.save.coins == coins && g.Allies == 1 && g.save.skillPoints == 1, "Commission rewards");
            g.Learn(1); Check(g.save.skillPoints == 0 && g.save.maxHealth == 152, "Talent allocation");
        }
        public static void DailyLife(Campaign data)
        {
            var g = Fresh(data); Action(g, "garden", "plant"); Check(g.save.plantedAt == 0 && g.save.inventory[7] == 2, "Planting");
            Action(g, "garden", "harvest"); Check(g.save.plantedAt >= 0, "Cannot harvest early");
            Action(g, "rest", "rest"); Action(g, "rest", "rest");
            int herbs = g.save.inventory[2]; Action(g, "garden", "harvest"); Check(g.save.inventory[2] == herbs + 5 && g.save.plantedAt < 0, "Harvest after elapsed time");
            for (int fish = 0; fish < 3; fish++)
            {
                Action(g, "fish", "fish");
                for (int hit = 0; hit < 3; hit++)
                {
                    // Wait through the actual timing simulation rather than patching the cursor.
                    int frames = 0; do { g.Tick(.02f, new Point()); } while ((Math.Abs(g.fishCursor - .5f) > .08f || frames++ < 5) && frames < 1000);
                    g.FishHit();
                }
                Check(g.mode == "world", "Fishing should finish after three good pulls");
            }
            Check(g.save.inventory[3] == 3, "Fishing rewards"); Action(g, "fish", "ally2"); Check(g.Has("ally_2") && g.save.inventory[3] == 0, "Fisher's commission");
            Action(g, "fish", "fish"); g.Close(); Check(g.save.inventory[3] == 0, "Cancelling fishing must not mint a reward");
            g.save.inventory[3] = 1; Action(g, "cook", "cook");
            g.save.health = 10; int meal = g.save.inventory[6]; g.Eat(); Check(g.save.health == 65 && g.save.inventory[6] == meal - 1 && g.save.foodUntil > g.save.totalTime, "Cooking and food buff");
        }
        public static void GatesAndGathering(Campaign data)
        {
            var g = Fresh(data); g.Enter(3); Check(g.save.room == 0, "Locked routes must remain locked");
            g.Enter(1); g.Talk(Object(g, "scout")); Check(!g.GateOpen, "Scout cannot open the gate before the rescue"); g.Close();
            int herbs = g.save.inventory[2]; var node = Object(g, "herb"); g.Talk(node); g.Talk(node); Check(g.save.inventory[2] == herbs + 3, "A resource node yields once per rest");
            g.save.kills[1] = 4; Action(g, "scout", "scout"); Check(g.GateOpen, "Rescue opens forest gate");
            g.save.stage = 1; g.Enter(2); g.Talk(Object(g, "rune2")); Check(g.runeProgress == 0, "Wrong rune resets puzzle");
            g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); Check(g.GateOpen, "Correct rune order opens ruins");
            g.save.stage = 2; g.Enter(3); g.save.inventory[4] = 0; Action(g, "engine", "engine"); Check(!g.GateOpen, "Mine repair requires parts");
            g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); Check(g.GateOpen && g.save.inventory[4] == 0, "Gathered parts repair elevator");
            for (int room = 0; room < 5; room++)
            {
                g.save.stage = 3; g.save.flags.Add("seal_" + room); g.Enter(room);
                var queue = new Queue<int>(); var seen = new HashSet<int>();
                int width = g.Map.rows[0].Length, height = g.Map.rows.Length;
                int start = (int)g.save.y * width + (int)g.save.x; queue.Enqueue(start); seen.Add(start);
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(), x = cell % width, y = cell / width;
                    int[,] offsets = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                    for (int i = 0; i < 4; i++)
                    {
                        int xx = x + offsets[i, 0], yy = y + offsets[i, 1], next = yy * width + xx;
                        if (xx < 0 || xx >= width || yy < 0 || yy >= height || g.Blocked(xx + .5f, yy + .5f) || seen.Contains(next)) continue;
                        seen.Add(next); queue.Enqueue(next);
                    }
                }
                foreach (var o in g.Map.objects)
                {
                    bool reachable = false;
                    foreach (int cell in seen) if ((new Point(cell % width + .5f, cell / width + .5f) - new Point(o.x, o.y)).Length < 1.7f) { reachable = true; break; }
                    Check(reachable, "Unreachable object in room " + room + ": " + o.id);
                }
            }
        }
        public static void CorruptEnemySnapshotsCannotInjectOrHideValidPatrols(Campaign data)
        {
            var old=new SaveData {
                room=1,snapshotRoom=1,
                worldEnemies=new [] {
                    new EnemyRecord { id=5,kind=0,x=float.NaN,y=14,hp=30,maxHp=30 },
                    new EnemyRecord { id=5,kind=0,x=13,y=14,hp=20,maxHp=30 },
                    new EnemyRecord { id=6,kind=1,x=9999,y=9999,hp=30,maxHp=30 },
                    new EnemyRecord { id=7,kind=2,x=14,y=14,hp=65,maxHp=80 }
                }
            };
            var restored=new GameCore(data,old);
            Check(restored.enemies.Count==3,
                "Malformed duplicate loses to a valid same-ID record while the old safe-location recovery still saves isolated off-map actors");
            Enemy valid=restored.enemies.Find(e=>e.id==5);
            Check(valid!=null&&valid.hp==20&&valid.maxHp==30&&
                  (valid.Position-new Point(13,14)).Length<.001f,
                "A malformed duplicate cannot reserve an ID before its genuine original saved entry");
            Enemy rescued=restored.enemies.Find(e=>e.id==6);
            Check(restored.enemies.Exists(e=>e.id==7)&&rescued!=null&&
                  rescued.hp==30&&restored.CanStand(rescued.Position),
                "An isolated damaged-position enemy still recovers with its saved identity and health");
            restored.PrepareSave();
            var again=new GameCore(data,CloneSave(restored.save));
            Check(again.enemies.Count==3&&again.enemies.Exists(e=>e.id==5)&&
                  again.enemies.Exists(e=>e.id==6)&&again.enemies.Exists(e=>e.id==7),
                "Deduplicated and rescued patrols persist normally across the next old-format save and reload");
        }
        public static void SaveValidation(Campaign data)
        {
            var bad = new SaveData { room = 99, stage = -1, x = float.NaN, y = -300, health = float.PositiveInfinity, coins = -100, inventory = null, talents = null, kills = null, flags = null };
            var g = new GameCore(data, bad); Check(g.save.room == 0 && g.save.x == 18 && g.save.coins == 0 && g.save.health == 120, "Damaged saves are sanitized");
            bool rejected = false; try { new GameCore(data, new SaveData { version = 900 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unsupported save schema is rejected");
            var legacyClass=new SaveData {
                profession=500,weapon=2,technique=1,
                flags=new List<string> { "weapon_2", "technique_2" }
            };
            var corrected=new GameCore(data,legacyClass);
            Check(corrected.save.profession==2&&corrected.save.weapon==2&&
                  corrected.save.technique==1,
                "A corrupted profession number is repaired before validating a legitimately unlocked alternate skill");
            var noUnlock=new SaveData { profession=500,technique=1 };
            var denied=new GameCore(data,noUnlock);
            Check(denied.save.profession==2&&denied.save.technique==0,
                "Early class correction must never unlock an absent alternate profession skill");

            var negativeXp=new GameCore(data,new SaveData{level=6,xp=-900});
            Check(negativeXp.save.level==6&&negativeXp.save.xp==0,
                "Negative XP cannot persist in a valid old v2 save");
            var levelCap=new GameCore(data,new SaveData{level=30,xp=int.MaxValue});
            Check(levelCap.save.level==30&&levelCap.save.xp==0,
                "Already-maxed legacy saves cannot preserve overflowing XP");
            var justBefore=new GameCore(data,new SaveData{level=29,xp=1159});
            Check(justBefore.save.xp==1159,
                "Ordinary near-level progress is preserved byte-for-byte on load");
            justBefore.enemies.Clear();
            var finalFoe=new Enemy(88131,0,18,14,30);
            justBefore.enemies.Add(finalFoe);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            hit.Invoke(justBefore,new object[]{finalFoe,99999f,false,null,null});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "Real combat reaching level 30 resets obsolete excess XP");
            var grant=typeof(GameCore).GetMethod("AddXp",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            grant.Invoke(justBefore,new object[]{int.MaxValue});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "At the cap huge quest/bounty payouts cannot overflow or unlock phantom levels");
            g.Enter(1); g.save.x = 2.3f; g.save.y = 14; g.Dash(new Point(-1, 0));
            for (int i = 0; i < 20; i++) g.Tick(.02f, new Point(-1, 0));
            Check(g.save.x >= 2.2f, "Dash cannot tunnel through the map boundary");
        }
        private static void Combat(GameCore g, bool bossOnly)
        {
            int frames = 0, startingRoom = g.save.room;
            while (g.enemies.Exists(e => !bossOnly || e.boss) && frames++ < 30000)
            {
                Check(g.save.room == startingRoom, "Combat bot died in room " + startingRoom);
                Enemy target = null; float distance = float.MaxValue;
                foreach (Enemy e in g.enemies)
                { if (bossOnly && !e.boss) continue; float d = (e.Position - g.Position).Length; if (d < distance) { target = e; distance = d; } }
                if (target == null) break;
                Point toward = g.DirectionTo(g.Position, target.Position), movement = distance > 1.4f ? toward : new Point();
                if (target.state == 1 && (target.boss || target.kind == 2) && (g.Position - target.target).Length < (target.boss ? 3.4f : 2))
                { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); g.Dash(movement); }
                foreach (Hazard h in g.hazards) if (h.timer < .5f && (g.Position - h.position).Length < h.radius + .5f)
                { movement = (g.Position - h.position).Length > .05f ? (g.Position - h.position).Normalized : new Point(-1, 0); g.Dash(movement); break; }
                g.Tick(.02f, movement); g.Attack(target.Position - g.Position);
                if (g.save.health < g.save.maxHealth * .8f) g.CommandCompanion();
                if (distance < 3) g.Skill();
                if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) g.Eat();
                if (g.mode == "dialog" && !g.Has("boss_4")) g.Close();
                if (startingRoom == 4 && g.Has("boss_4")) break;
            }
            string remaining = "";
            foreach (var e in g.enemies) remaining += " enemy " + e.kind + " @" + e.x + "," + e.y + " hp=" + e.hp;
            Check(frames < 30000, "Combat failed to converge in room " + g.save.room + " floor=" + g.save.abyssFloor + " profession=" + g.save.profession + " seed=" + g.save.seed + " mode=" + g.mode + " hitstop=" + g.hitStop + " death=" + g.deathTime + " direction=" + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).x:0) + "," + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).y:0) + " map=" + string.Join("/", g.Map.rows) + " player=" + g.save.x + "," + g.save.y + remaining);
        }
        public static void CompleteCampaign(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) CampaignRun(data, profession);
        }
        private static void CampaignRun(Campaign data, int profession)
        {
            var g = Fresh(data, profession); // Standard difficulty and starter equipment, with public gameplay actions.
            for (int room = 1; room <= 4; room++)
            {
                g.Enter(room); Check(g.save.room == room, "Chapter route unlock");
                Combat(g, false);
                if (room == 1) Action(g, "scout", "scout");
                if (room == 2) { g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); }
                if (room == 3) { g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); }
                Check(g.GateOpen, "Chapter gate");
                AwaitArenaOpening(g); // Hit stop pauses the same opening clock; wait for actual collision.
                // The original direct test teleport (28,10) places the
                // hero's full-body footprint partly in room 2's water
                // even though its CENTER tile is dry. Full campaign QA
                // must fight from valid ground like actual player input.
                g.save.x = 28.5f; g.save.y = 10.5f;
                Check(g.CanStand(g.Position),
                    "Complete campaign boss setup must place the real hero footprint on legal dry ground");
                Action(g, "shrine", "boss"); Combat(g, true);
                Check(g.Has("boss_" + room),
                    "Guardian defeated: room=" + room + " profession=" + profession +
                    " actualRoom=" + g.save.room + " dying=" + g.Dying +
                    " mode=" + g.mode + " health=" + g.save.health +
                    " enemies=" + g.enemies.Count + " state=" +
                    (g.enemies.Count > 0 ? g.enemies[0].id.ToString() : "empty"));
                if (room < 4) { Action(g, "shrine", "goto0"); Action(g, "guild", "deliver"); Check(g.save.stage == room, "Core delivered"); }
            }
            for (int frame=0; frame<200 && g.mode=="world"; frame++) g.Tick(.02f,new Point());
            Check(g.mode == "dialog" && !g.fallen.Exists(f=>f.actor.boss), "Final conversation follows the completed Boss death clock, including hit stop");
            int endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending1") endingChoice = i;
            g.Choose(endingChoice); Check(g.save.ending == 1 && g.Has("completed"), "First ending"); g.Choose(0); Check(g.save.room == 0, "Postgame returns to town");
            // Complete the optional commissions after the campaign and verify the earned alternative.
            g.save.inventory[2] += 6; g.save.inventory[3] += 3; g.save.inventory[4] += 5;
            Action(g, "garden", "ally1"); Action(g, "fish", "ally2"); Action(g, "forge", "ally3");
            g.Enter(4); g.Talk(Object(g, "shrine")); endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending3") endingChoice = i;
            Check(endingChoice >= 0, "Three allies unlock the alternative ending"); g.Choose(endingChoice); Check(g.save.ending == 3, "Third ending");
            g.Close(); g.Talk(Object(g, "shrine"));
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending2") { g.Choose(i); break; }
            Check(g.save.ending == 2, "Second ending");
        }


        public static void ExpandedHearthInnArchiveIsConnectedAndRewardsVerifiedExploration(Campaign data)
        {
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,"Expanded Hearth Inn preserves rooms while adding public archive wings");
            GameCore game=new GameCore(data);game.save.room=9;
            var queue=new System.Collections.Generic.Queue<int>();
            var visited=new System.Collections.Generic.HashSet<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int start=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(start);visited.Add(start);
            int[,] offsets={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int d=0;d<4;d++)
                {
                    int xx=x+offsets[d,0],yy=y+offsets[d,1],next=yy*w+xx;
                    if(xx<0||xx>=w||yy<0||yy>=h||visited.Contains(next)||
                        !game.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    visited.Add(next);queue.Enqueue(next);
                }
            }
            WorldObject archive=Array.Find(inn.objects,o=>o.kind=="annexinnarchive");
            WorldObject letter=Array.Find(inn.objects,o=>o.kind=="annexinnmessage");
            Check(archive!=null&&letter!=null&&visited.Contains((int)archive.y*w+(int)archive.x)&&
                visited.Contains((int)letter.y*w+(int)letter.x),
                "Both new inn annex rooms connect to original spawn through real collision");
            game.save.x=archive.x;game.save.y=archive.y;
            game.Talk(archive);
            int idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            Check(idx>=0,"Inn archive can be filed with a real interactable action");
            int coins=game.save.coins;game.Choose(idx);
            Check(!game.Has("annex_archive_complete")&&game.save.coins==coins,
                "Incomplete outdoor discovery cannot falsely claim the published atlas");
            for(int room=0;room<8;room++)
            {
                game.save.flags.Add("annex_camp_annex_camp_"+room);
                game.save.flags.Add("annex_vista_annex_vista_"+room);
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_a");
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_b");
            }
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.Has("annex_archive_complete")&&game.save.coins==coins+60&&
                game.GetQuest("annex_explorer").complete,
                "Physically filing the complete atlas rewards real resources and finishes the quest");
            SaveData save=game.save;
            GameCore loaded=new GameCore(data,save);
            Check(save.version==2&&loaded.Has("annex_archive_complete"),
                "Public atlas persists in old save slots");
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.save.coins==save.coins,"Published atlas cannot be farmed by rereading");
        }

        public static void NewAnnexSitesAreRealWalkableAndPersistent(Campaign data)
        {
            for(int region=0;region<8;region++)
            {
                MapData m=data.maps[region];
                GameCore g=new GameCore(data);g.save.room=region;
                g.save.flags.Add("gate_"+region);
                Check(!g.InProtectedArena(new Point(65,9)), "Boss arena must remain at its original x-coordinate");
                int count=0;foreach(WorldObject o in m.objects)
                    if(o.kind=="annexcache"||o.kind=="annexcamp"||o.kind=="annexvista")
                    {count++;Check(g.CanStand(new Point(o.x,o.y)), "New site must occupy real walkable tile: "+o.id);}
                Check(count==4,"Two caches, one camp, one vista per outdoor map");
            }
            GameCore game=new GameCore(data);
            WorldObject cache=Array.Find(data.maps[0].objects,o=>o.id=="annex_cache_0_a");
            game.save.x=cache.x;game.save.y=cache.y;
            int coins=game.save.coins;
            game.Talk(cache);Check(game.save.coins==coins+7 && game.Has("annex_cache_"+cache.id),"Looting physical cache grants once");
            game.Close();game.Talk(cache);game.Close();
            Check(game.save.coins==coins+7,"Cache cannot duplicate real loot");
            WorldObject vista=Array.Find(data.maps[0].objects,o=>o.id=="annex_vista_0");
            game.save.x=vista.x;game.save.y=vista.y;game.Talk(vista);game.Close();
            Check(game.Has("annex_vista_"+vista.id)&&game.Explored(0,65,40),"Lookout records its nearby 6x6 fog cells");
            WorldObject camp=Array.Find(data.maps[0].objects,o=>o.id=="annex_camp_0");
            game.save.x=camp.x;game.save.y=camp.y;game.Talk(camp);
            int choice=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_rest");
            Check(choice>=0,"Real camp offers rest interaction");
            int wood=game.save.inventory[0];
            game.save.health=30;game.save.energy=8;
            game.Choose(choice);
            Check(game.save.inventory[0]==wood-1&&game.save.health==70&&game.save.energy==100,
                "Camp consumes actual timber, restores HP and energy");
            GameCore reload=new GameCore(data,game.save);
            Check(reload.save.version==2&&reload.Has("annex_camp_"+camp.id)&&reload.Has("annex_cache_"+cache.id)&&reload.Has("annex_vista_"+vista.id),
                "All exploration progress remains in v2 save");
        }

        public static void EightCircuitRoutesUseActualWalkingOrderAndPersist(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Sixth-ring terrain preserves earlier exploration sites");
                GameCore g=new GameCore(data);
                g.save.room=room;g.save.flags.Add("seal_"+room);
                int width=m.rows[0].Length,height=m.rows.Length;
                var visited=new System.Collections.Generic.HashSet<int>();
                var queue=new System.Collections.Generic.Queue<int>();
                int start=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(start);visited.Add(start);
                int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int k=0;k<4;k++)
                    {
                        int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*width+xx;
                        if(xx<0||yy<0||xx>=width||yy>=height||visited.Contains(key)||
                            !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        visited.Add(key);queue.Enqueue(key);
                    }
                }
                WorldObject[] sites=new WorldObject[3];
                for(int k=1;k<=3;k++)
                {
                    sites[k-1]=Array.Find(m.objects,o=>o.id=="trailcircuit_"+room+"_"+k);
                    Check(sites[k-1]!=null &&
                        visited.Contains((int)sites[k-1].y*width+(int)sites[k-1].x),
                        "Real collision and traversal reach every landmark in room "+room);
                }
                g.save.x=sites[2].x;g.save.y=sites[2].y;int startCoins=g.save.coins;
                g.Talk(sites[2]);g.Close();
                Check(g.TrailCircuitProgress(room)==0,"Third landmark cannot bypass first two");
                for(int k=0;k<3;k++)
                {
                    g.save.x=sites[k].x;g.save.y=sites[k].y;
                    g.Talk(sites[k]);
                    Check(g.conversation!=null&&g.conversation.text.Length>55&&
                        g.conversation.textEn.Length>55,
                        "Every visited physical waypoint provides real bilingual lore");
                    g.Close();
                    Check(g.TrailCircuitProgress(room)==k+1,"Each visited stop records real ordered progress");
                }
                Check(g.TrailCircuitComplete(room)&&g.save.coins==startCoins+24,
                    "Third distinct visit grants lasting local route completion");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==startCoins+24,"Rereading completed circuit cannot farm rewards");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.TrailCircuitComplete(room),
                    "Existing SaveData v2 reload preserves circuit milestones");
            }
            GameCore all=new GameCore(data);
            for(int r=0;r<8;r++)for(int k=1;k<=3;k++)
                all.save.flags.Add("trail_circuit_"+r+"_"+k);
            Check(all.TrailCircuitCount==8&&all.GetQuest("trail_circuits").complete,
                "Independent eight-area map quest reflects real stage flags");
        }

        public static void ThirdRingResourcePocketsUseRealRenewableHarvesting(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore region=new GameCore(data);
                region.save.room=room;
                int count=0;
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.id.StartsWith("trail_forager_"+room+"_"))
                    {
                        count++;
                        Check(region.CanStand(new Point(o.x,o.y)),
                            "Every new gatherable site remains on actual playable terrain");
                    }
                Check(count==3,"Each of eight third-ring maps gets three actual field resources");
            }
            GameCore g=new GameCore(data);
            WorldObject node=Array.Find(data.maps[0].objects,o=>o.id=="trail_forager_0_0");
            int herbs=g.save.inventory[2];
            g.save.x=node.x;g.save.y=node.y;g.Talk(node);
            Check(g.Has("node_"+node.id)&&g.save.inventory[2]==herbs+3,
                "New outdoor node yields actual resource through existing gathering code");
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+3,"Second gather cannot duplicate resource");
            g.save.flags.RemoveAll(flag=>flag.StartsWith("node_"));
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+6,
                "Existing rest refresh makes the new site renewable without new save format");
        }

        public static void CachedHouseCollidersRemainCorrectAcrossMapSwitches(Campaign data)
        {
            GameCore g=new GameCore(data);
            int tested=0;
            for(int room=0;room<8;room++)
            {
                g.save.room=room;
                foreach(Decoration d in data.maps[room].decor)
                    if(d.kind=="house")
                    {
                        tested++;
                        Check(g.Blocked(d.x,d.y-.7f),
                            "Solid house footprint remains blocked after world-decoration caching");
                    }
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.kind=="trailcircuit")
                        Check(g.CanStand(new Point(o.x,o.y)),
                            "Third-ring site remains walkable when switching collision maps");
            }
            Check(tested>=4,"World collision regression exercises existing authored houses");
            g.save.room=0;
            Check(g.Blocked(7,5.3f),"Returning to town restores its original solid house colliders");
        }

        public static void FourthRingRescueSignalsRequireRealSurveyAndParts(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Expanded sixth-ring geometry retains existing rescue locations");
                GameCore g=new GameCore(data);
                g.save.room=room;
                g.save.inventory[4]=5;
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="outer_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "Every fourth-ring rescue object stands on actual accessible terrain");
                }
                int width=m.rows[0].Length,height=m.rows.Length;
                var queue=new System.Collections.Generic.Queue<int>();
                var seen=new System.Collections.Generic.HashSet<int>();
                int spawn=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(spawn);seen.Add(spawn);
                int[,] dirs={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int direction=0;direction<4;direction++)
                    {
                        int xx=x+dirs[direction,0],yy=y+dirs[direction,1];
                        if(xx<0||yy<0||xx>=width||yy>=height)continue;
                        int key=yy*width+xx;
                        if(seen.Contains(key)||!g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        seen.Add(key);queue.Enqueue(key);
                    }
                }
                foreach(WorldObject site in sites)
                    Check(seen.Contains((int)site.y*width+(int)site.x),
                        "New signal route must be reachable from original spawn using real collision");
                int coins=g.save.coins,parts=g.save.inventory[4];
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.OuterCacheRecovered(room)&&g.save.coins==coins,
                    "Relief cache cannot be taken before the signal is repaired");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);g.Close();
                Check(!g.OuterRelayRestored(room)&&g.save.inventory[4]==parts,
                    "Beacon cannot be repaired without the original field log");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.OuterLogFound(room),"Reading the southern evidence advances the real quest");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_repair");
                Check(choice>=0,"Physical repair offers a real material-consuming choice");
                g.Choose(choice);
                Check(g.OuterRelayRestored(room)&&g.save.inventory[4]==parts-1,
                    "One actual spare part is consumed when the physical beacon is repaired");
                g.Talk(sites[1]);g.Close();
                Check(g.save.inventory[4]==parts-1,"Replaying a repaired signal cannot double-charge");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(g.OuterCacheRecovered(room)&&g.save.coins==coins+18,
                    "Final physical relief chest pays the correct one-time reward");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==coins+18,"Relief chest cannot pay twice");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.OuterLogFound(room)&&
                    restored.OuterRelayRestored(room)&&restored.OuterCacheRecovered(room),
                    "All three rescue stages survive a legacy SaveData v2 reload");
            }
            GameCore all=new GameCore(data);
            for(int room=0;room<8;room++)all.save.flags.Add("outer_cache_"+room);
            Check(all.OuterRescueCount==8&&all.GetQuest("outer_rescue").complete,
                "The tracked field mission finishes only after eight genuine chests");
        }

        public static void SmoothTerrainAndPropsRemainContinuousAcrossChunkBorders(Campaign data)
        {
            Check(WorldStreaming.Smooth01(-1)==0&&WorldStreaming.Smooth01(2)==1,
                "Streaming fades clamp safely outside their range");
            Check(Math.Abs(WorldStreaming.Smooth01(.5f)-.5f)<.0001f,
                "Chunk readiness uses symmetric smoothstep");
            for(int i=0;i<100;i++)
            {
                float t=i/100f, v=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t,.45f);
                float u=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t+.0001f,.45f);
                Check(v>=0&&v<=1&&Math.Abs(u-v)<.001f,
                    "Chunk blending stays continuous without square block seams");
            }
            float old=-1;
            for(int px=-130;px<=130;px+=5)
            {
                float opacity=WorldStreaming.EdgeOpacity(px,360,1280,720);
                Check(opacity>=0&&opacity<=1&&opacity>=old,
                    "Scenery gradually fades in on approach without a viewport pop-in");
                old=opacity;
            }
            Check(WorldStreaming.EdgeOpacity(-150,360,1280,720)==0,
                "Distant scenery is completely transparent beyond its prewarm apron");
            Check(WorldStreaming.EdgeOpacity(640,360,1280,720)==1,
                "Center-screen scenery stays fully visible");
        }

        public static void OutdoorRescueWatchStartsRealCombatAndOnlyRewardsRealWins(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                WorldObject o=Array.Find(map.objects,x=>x.id=="outer_watch_"+room);
                GameCore g=new GameCore(data);g.save.room=room;
                Check(o!=null&&g.CanStand(new Point(o.x,o.y)),
                    "Every optional watchpost must be on a walkable physical tile");
                g.save.x=o.x;g.save.y=o.y;
                g.enemies.Clear();
                g.Talk(o);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Outpost cannot be started without first retrieving the relief supplies");
                g.Close();
                g.save.flags.Add("outer_cache_"+room);
                int coins=g.save.coins;
                g.Talk(o);
                int start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Completed exploration unlocks the voluntary real combat choice");
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2&&
                    !g.OuterPatrolDone(room),"Defense uses actual two spawned enemies, not a text-only battle");
                int id0=g.enemies[0].id,id1=g.enemies[1].id;
                Check(id0!=id1&&g.enemies[0].hp>0&&g.enemies[1].hp>0,
                    "Distinct attackers spawn with real combat health");
                g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.save.coins==coins,
                    "Unbeaten enemies cannot award any defense completion");
                // Regression: forcibly removing actors from the list is NOT
                // damage, and must never counterfeit a successful clear.
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&!g.OuterPatrolActive&&g.save.coins==coins,
                    "Enemy despawn must abort rather than award a counterfeit win");
                g.Talk(o);
                start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Aborted encounter can be retried safely");
                g.Choose(start);
                Check(g.enemies.Count==2,"Retry spawns two live combat actors");
                Enemy attackerA=g.enemies[0],attackerB=g.enemies[1];
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Check(hit!=null,"Exercise real combat Hit transaction, not a simulated flag");
                hit.Invoke(g,new object[]{attackerA,10000f,true,null,null});
                // Hit-stop is part of actual combat and temporarily defers
                // quest ticks. Run normal real frames, not a magic giant dt.
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.OuterPatrolActive,
                    "Defeating one real attacker is not enough to clear the defense");
                hit.Invoke(g,new object[]{attackerB,10000f,true,null,null});
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolDone(room)&&g.save.coins==coins+14,
                    "Both true lethal Hit transactions award the single defense reward");
                // Recreate the persisted v2 state as a separate slot object.
                // The loader sanitizes its OWN save in place. Passing the
                // active g.save aliases the running hero and a locked-region
                // recovery can move that hero away from this watchpost.
                var persisted = new SaveData {
                    version=g.save.version, room=g.save.room, stage=g.save.stage,
                    x=g.save.x, y=g.save.y, coins=g.save.coins,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&reload.OuterPatrolDone(room),
                    "Defended watchposts persist with original v2 flags");
                g.Talk(o);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Completed post remains readable, never starts a duplicating fight");
                g.Close();
                Check(g.save.coins==coins+14,"Repeating a defended post cannot farm rewards");
            }
        }

        public static void GuardMedicPhysicallyRalliesToCriticalHeroAndReturnsToStock(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.companion=0;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Medical guard uses a real watchpost defense");
            g.Choose(start);
            foreach(Enemy foe in g.enemies)foe.stun=20f;
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Mira can be physically assigned to watch stock");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Medical support guard is a valid active tactical order");
            Point foot=g.OuterPatrolCompanionGuardPosition;
            Point remote=new Point();bool found=false;
            for(int dx=-9;dx<=9&&!found;dx++)
            for(int dy=-9;dy<=9&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-foot).Length<7f||
                    (p-new Point(post.x,post.y)).Length>10f)
                    continue;
                remote=p;found=true;
            }
            Check(found,"Real regional defense ring has a separate walkable rescue point");
            g.save.x=remote.x;g.save.y=remote.y;
            g.save.health=g.save.maxHealth*.25f;
            var helper=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(helper!=null,"Actual escort goal is selected by live game logic");
            Point medicTarget=(Point)helper.Invoke(g,null);
            Check((medicTarget-foot).Length>1f&&
                g.OuterPatrolCompanionGuarding,
                "Critically injured remote hero draws Mira from her watch foot");
            float before=(g.companionPosition-medicTarget).Length;
            for(int i=0;i<90&&g.OuterPatrolActive;i++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-medicTarget).Length<before-.1f,
                "Medic walks toward physical rescue point, never heals globally");
            g.save.health=g.save.maxHealth;
            Point returnFoot=(Point)helper.Invoke(g,null);
            Check((returnFoot-foot).Length<.01f&&
                g.OuterPatrolCompanionGuarding,
                "After real recovery medic resumes original protected post");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolCompanionGuarding&&
                !g.OuterPatrolActive,
                "Withdrawal removes emergency medic and guard states");
        }

        public static void RealCompanionGuardsSuppliesWithoutWarpingOrPersisting(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Regional relief post exists on physical map");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            Check(g.CanStand(g.companionPosition),
                "Guard begins at a genuine unblocked companion foot");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Watchpost has a genuine active defense");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The physical watchpost offers a live companion guard order");
            Point before=g.companionPosition;
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(g.OuterPatrolCompanionGuardPosition)&&
                (g.companionPosition-before).Length<.001f,
                "Guard command picks legal foot but never teleports ally");
            Point destination=g.OuterPatrolCompanionGuardPosition;
            // The player can intercept the second raider without dragging
            // the ranged guard away from the vulnerable stock.
            g.save.x=post.x+5f;g.save.y=post.y;
            float distBefore=(g.companionPosition-destination).Length;
            for(int tick=0;tick<90&&g.OuterPatrolActive;tick++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-destination).Length<distBefore+.05f&&
                (g.companionPosition-new Point(post.x,post.y)).Length<5f,
                "Guard follows a physical local stock waypoint, not the distant hero");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Follow option exists while guard is active");
            g.Choose(guard);
            Check(!g.OuterPatrolCompanionGuarding,
                "Player can restore original ordinary companion follow behavior");
            // Independent guard order can be given again in the same siege,
            // but it must disappear on withdrawal and save round-trip.
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Guard can be reassigned by an actual valid local order");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&!g.OuterPatrolCompanionGuarding,
                "Leaving a siege retires ephemeral guard orders");
            SaveData slot=new SaveData {
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags)
            };
            GameCore reload=new GameCore(data,slot);
            Check(!reload.OuterPatrolCompanionGuarding&&
                reload.save.version==2,
                "v2 slots never persist a transient tactical guard command");
        }

        public static void CompanionPrioritizesRealOutpostSaboteursOverPassingFoes(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=post.x;g.save.y=post.y;
            g.enemies.Clear();
            g.save.flags.Add("outer_cache_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"An eligible real watchpost can start combat");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "Raider priority depends on a genuine live defense");
            Enemy saboteur=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(saboteur!=null,"A cache saboteur has a genuine world body");
            g.save.companion=1;
            g.companionPosition=new Point(post.x,post.y);
            var chooser=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(chooser!=null,"Actual companion targeting is exercised");
            Enemy picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "A visible melee stock saboteur beats a ranged raid distractor");
            // An unrelated foe even closer to Ivo should not draw the
            // bodyguard away from the cache while the defense is live.
            Enemy decoy=new Enemy(8111,0,post.x+.3f,post.y,30f);
            g.enemies.Add(decoy);
            picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "Cache defense prioritizes its actual protected objective");
            g.enemies.Remove(decoy);
            // When the actual attack is obstructed, do not aim forever
            // through a closed wall: the standard line/muzzle validation
            // must still choose a legal unrelated enemy.
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The defended physical post offers withdrawal");
            g.Choose(abort);
            g.enemies.Add(decoy);
            Check((Enemy)chooser.Invoke(g,null)==decoy,
                "After the real defense is withdrawn auto targeting remains normal");
        }

        public static void VeteranFlawlessMedalsRequireRealThreeKillsAndAreEarnableOnReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Flawless medals rely on genuine Hit transactions");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.inventory[0]=6;g.save.inventory[4]=4;
                int coins=g.save.coins,shards=g.save.shards;
                // Eight-region mastery reward is independent of initial
                // eight-veteran bonuses and ordinary XP leveling.
                if(room==7)
                    for(int i=0;i<7;i++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+i);
                        g.save.flags.Add("outer_patrol_veteran_"+i);
                        g.save.flags.Add("outer_patrol_flawless_"+i);
                    }
                int talent=g.save.skillPoints;
                g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(start>=0,"Veteran fight is physically offered");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Clean challenge is eligible at untouched 100% stock");
                g.Talk(post);
                int brace=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_brace");
                Check(brace>=0,"Veteran post allows voluntary protective cover");
                g.Choose(brace);
                Check(!g.OuterPatrolFlawlessEligible,
                    "Deploying tactical cover forfeits this run's no-assist medal");
                Enemy[] foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolFlawlessDone(room),
                    "Three genuine kills with a barricade pay base win but not a medal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Replay has a fresh honest no-assist challenge");
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shards+2,
                    "Clean real replay awards permanent medal without duplicate first-clear loot");
                if(room==7)
                    Check(g.OuterPatrolFlawlessWins==8&&
                        g.Has("outer_patrol_flawless_all")&&
                        g.save.skillPoints==talent+2,
                        "Genuine eighth veteran and eighth flawless each grant one skill point");
                else Check(g.save.skillPoints==talent,
                    "Partial flawless collection cannot farm talent points");
                SaveData persisted=new SaveData{
                    version=2,room=g.save.room,stage=g.save.stage,
                    x=g.save.x,y=g.save.y,
                    presentationVersion=g.save.presentationVersion,
                    flags=new List<string>(g.save.flags),
                    trackedQuest="outer_watch_flawless"
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&
                    reload.OuterPatrolFlawlessDone(room)&&
                    reload.save.trackedQuest=="outer_watch_flawless",
                    "New mastery medal is an ordinary legacy-compatible v2 flag and journal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    g.save.coins==coins+26&&g.save.shards==shards+2,
                    "Third full authentic clean win cannot repeat rewards");
            }
        }

        public static void FlawlessFieldCareActuallyImprovesSafeTickAndStopsDuringRaids(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_veteran_0");
            g.save.health=45f;g.save.energy=30f;
            Check(g.OuterPatrolRefuge,"Defended post is a real place of recovery");
            g.Tick(.05f,new Point());
            float veteranHp=g.save.health-45f;
            float veteranEnergy=g.save.energy-30f;
            g.save.flags.Add("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            float flawlessHp=g.save.health-45f;
            float flawlessEnergy=g.save.energy-30f;
            Check(flawlessHp>veteranHp+.025f&&
                flawlessEnergy>veteranEnergy+.05f,
                "Real 50ms game tick grants strictly better local care after medal");
            g.enemies.Add(new Enemy(9911,0,post.x+.7f,post.y,32f));
            g.save.health=45f;g.save.energy=30f;
            Check(!g.OuterPatrolRefuge,
                "Real nearby raider disables medal-powered refuge care");
            g.Tick(.05f,new Point());
            float blockedHp=g.save.health;
            float blockedEnergy=g.save.energy;
            // The hero has native combat energy regeneration even with no
            // refuge. Compare the SAME real threat with and without the medal
            // instead of incorrectly requiring all regeneration to stop.
            g.save.flags.Remove("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            Check(blockedHp<=45f&&g.save.health<=45f&&
                Math.Abs(blockedEnergy-g.save.energy)<.01f,
                "A hostile raider suppresses medal care without removing ordinary stamina recovery");
            g.save.flags.Add("outer_patrol_flawless_0");
            g.enemies.Clear();
            g.save.x=post.x+20f;g.save.y=post.y+20f;
            Check(!g.OuterPatrolRefuge,
                "Flawless reward never grants region-wide free healing");
            GameCore reloaded=new GameCore(data,new SaveData{
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags),
                presentationVersion=g.save.presentationVersion
            });
            Check(reloaded.OuterPatrolFlawlessDone(0)&&
                reloaded.OuterPatrolRefuge,
                "Old v2 save retains the field care upgrade without new fields");
        }

        public static void LateLiveInterceptionProtectsCacheOnRealEnemyRelease(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Defend an actual supply cache");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "An authentic raid has two real enemy actors");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null&&(melee.Position-new Point(post.x,post.y)).Length<=2.35f,
                "Raid melee attacker starts at genuine contact range");
            // The target lock and telegraph are already committed to stock.
            // A defender enters AFTER the windup began but BEFORE the
            // real release, so it must still be possible to block the cache.
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            float before=g.OuterPatrolIntegrity;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity==before,
                "Defender arriving during authentic attack windup intercepts its stock hit");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.outerPatrolOwned&&e.text.Contains(
                    g.save.english?"INTERCEPTED":"拦截成功")),
                "Actual blocked release gives localized, owned readable combat feedback");
            // The SAME attacker and identical release must damage an
            // unobstructed physical store once the hero leaves the lane.
            Point retreat=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>10f||
                    (p-melee.Position).Length<5f||!g.CanStand(p))continue;
                retreat=p;found=true;
            }
            Check(found,"Find genuine unoccupied map floor away from melee");
            g.save.x=retreat.x;g.save.y=retreat.y;
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "With no defender in range, an identical real contact damages stock");
            // Ordinary regional defense and old saves retain their original
            // one-time reward/failure flags; this is transient interception.
            Check(!g.OuterPatrolDone(0)&&g.save.version==2,
                "Interception is not a forged victory or a save schema migration");
        }

        public static void VeteranBarricadeSpendsRealSuppliesAndAbsorbsReleasedStockHits(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.inventory[0]=8;g.save.inventory[4]=4;
            // The ordinary first-time encounter must not pretend that a
            // veteran barricade is a usable menu command.
            GameCore ordinary=new GameCore(data);ordinary.mode="world";
            ordinary.save.room=0;ordinary.enemies.Clear();
            ordinary.save.x=post.x;ordinary.save.y=post.y;
            ordinary.save.flags.Add("outer_cache_0");
            ordinary.save.inventory[0]=8;ordinary.save.inventory[4]=4;
            ordinary.Talk(post);
            int normal=Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_start");
            ordinary.Choose(normal);
            ordinary.Talk(post);
            Check(Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_brace")<0,
                "Standard two-raider raid never advertises veteran-only cover");
            ordinary.Close();
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Completed relief post offers actual veteran raid");
            g.Choose(start);
            Check(g.OuterPatrolVeteranActive&&g.OuterPatrolBraceIntegrity==0&&
                g.OuterPatrolBraceAvailable,"Veteran assault offers an unused physical brace");
            g.Talk(post);
            int brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace>=0,"Barricade is a genuine post interaction");
            g.Choose(brace);
            Check(g.OuterPatrolBraceIntegrity==48f&&
                g.OuterPatrolBraceStanding&&
                g.OuterPatrolBraceHitFlash==0f&&
                !g.OuterPatrolBraceAvailable&&
                g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "First barricade spends two real timber and one spare part");
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"An already built barricade is not offered again");
            var invoke=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(invoke!=null,"Old cached attack menu is still defended");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "Forged cached brace choice cannot buy a second barricade");
            // A private handler call bypasses Choose(index)'s automatic
            // Close(); restore gameplay mode before advancing real AI time.
            g.Close();
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"Veteran raid owns an authentic melee stock attacker");
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<6f||!g.CanStand(p))
                    continue;
                safe=p;found=true;
            }
            Check(found,"A real walkable observation foot exists near cache");
            g.save.x=safe.x;g.save.y=safe.y;
            float stock=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1100&&g.OuterPatrolActive&&
                g.OuterPatrolBraceIntegrity==48f;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity<48f,
                "Unintercepted real raider winds up and releases into the barricade");
            Check(g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash>0f,
                "A real stock contact drives visible barricade impact feedback");
            Check(g.OuterPatrolIntegrity==stock,
                "A real stock strike first consumes physical cover, not protected cache HP");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"Broken or damaged used cover stays unavailable");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "A damaged cover cannot be re-purchased during same siege");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity==0f&&
                !g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash==0f,
                "Withdrawal retires live barricade artwork cues and attackers");
            GameCore restored=new GameCore(data,g.save);
            Check(!restored.OuterPatrolActive&&restored.OuterPatrolBraceIntegrity==0&&
                restored.save.version==2,"Old v2 saves never persist temporary cover");
        }

        public static void VeteranReliefDefenseHasThreeRealKillsOneTimeRewardAndSafeReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Use genuine combat Hit, not fake victory flags");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null,"Veteran post is authored in room "+room);
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                // Isolate the distinct veteran "eight defended" talent
                // from ordinary XP level-ups caused by four-to-six real
                // kill/reward transactions in this same integration test.
                // Level 25 begins at zero XP, far from its next threshold.
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                int coins=g.save.coins;
                // Final veteran clear must really award exactly one talent
                // after *eight* regional victories, never before.
                if(room==7)
                    for(int previous=0;previous<7;previous++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+previous);
                        g.save.flags.Add("outer_patrol_veteran_"+previous);
                    }
                int talents=g.save.skillPoints;
                g.Talk(post);
                Check(g.conversation!=null,
                    "Veteran battle is offered at a physical post in region "+room);
                int veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,
                    "Already guarded supply depot offers actual veteran combat");
                g.Choose(veteran);
                Check(g.OuterPatrolActive&&g.OuterPatrolVeteranActive&&
                    g.OuterPatrolTime>109f&&g.enemies.Count==3,
                    "Veteran raid spawns three genuine enemy bodies and a full cache clock");
                Enemy[] raiders=g.enemies.ToArray();
                Check(raiders[0].kind==0&&raiders[1].kind==1&&
                    raiders[2].kind==2&&raiders[2].hp>raiders[0].hp,
                    "Veteran squad has melee, real ranged, and tougher slammer");
                int shard=g.save.shards;
                hit.Invoke(g,new object[]{raiders[0],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                hit.Invoke(g,new object[]{raiders[1],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolVeteranDone(room)&&g.OuterPatrolActive,
                    "Two kills cannot counterfeit a three-member veteran victory");
                hit.Invoke(g,new object[]{raiders[2],10000f,true,null,null});
                for(int n=0;n<15;n++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Three genuine lethal transactions award exactly one veteran clear");
                Check(room==7?
                    g.OuterPatrolVeteranWins==8&&
                    g.Has("outer_patrol_veteran_all")&&
                    g.save.skillPoints==talents+1:
                    g.save.skillPoints==talents,
                    "Exactly the eighth genuine regional veteran win awards the extra talent");
                // Loading a NEW hero may sanitize an otherwise locked
                // region. Don't alias the current active hero's SaveData:
                // this check must not teleport the current challenger.
                SaveData persisted=new SaveData {
                    version=g.save.version, room=g.save.room,
                    stage=g.save.stage,x=g.save.x,y=g.save.y,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reloaded=new GameCore(data,persisted);
                Check(reloaded.save.version==2&&
                    reloaded.OuterPatrolVeteranDone(room)&&
                    !reloaded.OuterPatrolVeteranActive,
                    "Veteran clear persists in existing v2 flags with no transient raid leak");
                g.Talk(post);
                Check(g.conversation!=null,"Completed veteran post "+
                    room+" remains physically interactable");
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran combat remains playable after victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "Replay actually spawns combat again, not a free menu reward");
                Enemy[] replay=g.enemies.ToArray();
                foreach(Enemy raider in replay)
                {
                    hit.Invoke(g,new object[]{raider,10000f,true,null,null});
                    for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                }
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Actual lethal veteran replay produces no duplicate coins or shards");
                g.Talk(post);
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran post stays replayable after a full second victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "A third veteran attempt still creates a real three-raider fight");
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26,
                    "Interrupted subsequent attempts cannot farm a reward");
            }
        }

        public static void VeteranOutpostJournalTracksTrueRegionalFlagsAndLiveObjective(Campaign data)
        {
            GameCore g=new GameCore(data);g.mode="world";
            QuestGuide q=g.GetQuest("outer_watch_veteran");
            Check(q.locked&&!q.complete&&q.steps.Count==8,
                "New quest is dormant until a real ordinary supply defense");
            g.save.flags.Add("outer_patrol_done_0");
            q=g.GetQuest("outer_watch_veteran");
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(!q.locked&&!q.complete&&q.room==0&&
                (q.target-new Point(post.x,post.y)).Length<.1f,
                "Journal points to the actual unlocked region-zero veteran post");
            g.save.flags.Add("outer_patrol_veteran_0");
            q=g.GetQuest("outer_watch_veteran");
            Check(q.steps[0].done&&q.Progress>.12f&&
                !q.complete,"Genuine permanent veteran flag advances the tracked quest");
            for(int room=1;room<8;room++)
            {
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.flags.Add("outer_patrol_veteran_"+room);
            }
            q=g.GetQuest("outer_watch_veteran");
            Check(q.complete&&q.Progress==1f,
                "All eight persisted veteran victories complete the in-game quest");
            SaveData persisted=new SaveData {
                version=2,room=0,trackedQuest="outer_watch_veteran",
                // Preserve modern v2 journal presentation. A fresh
                // presentationVersion=0 correctly migrates a LEGACY UI
                // and resets old tracking, as the original game requires.
                presentationVersion=g.save.presentationVersion,
                flags=new List<string>(g.save.flags)
            };
            GameCore restored=new GameCore(data,persisted);
            Check(restored.save.trackedQuest=="outer_watch_veteran"&&
                restored.GetQuest("outer_watch_veteran").complete,
                "Legacy v2 journal selection survives loading a separate slot");

            // A real running siege should outrank the tracked objective
            // while retaining that tracked quest for the next safe moment.
            GameCore siege=new GameCore(data);siege.mode="world";
            siege.save.room=0;siege.enemies.Clear();
            siege.save.x=post.x;siege.save.y=post.y;
            siege.save.flags.Add("outer_cache_0");
            siege.save.trackedQuest="outer_watch_veteran";
            siege.Talk(post);
            int start=Array.FindIndex(siege.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Real outpost is combat-ready");
            siege.Choose(start);
            Point? marker=siege.NavigationTarget();
            Check(siege.OuterPatrolActive&&marker.HasValue&&
                (marker.Value-new Point(post.x,post.y)).Length<.1f&&
                siege.save.trackedQuest=="outer_watch_veteran",
                "World objective marker tracks live cache without rewriting saved quest");
        }

        public static void OuterPatrolCacheDefenseUsesRealTimedStockCombat(Campaign data)
        {
            // The existing eight-map witness proves physically playable roots.
            // Here a real melee unit must telegraph, wind up and reach the
            // actual store instead of magically draining it or only chasing
            // a distant hero. Old v2 flags stay unaltered on withdrawal.
            int room=0;
            WorldObject post=Array.Find(data.maps[room].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Canal has a real physical relief watchpost");
            GameCore g=new GameCore(data);
            g.save.room=room;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_"+room);
            g.save.inventory[0]=5;g.save.inventory[2]=5;
            int coins=g.save.coins;
            g.Talk(post);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(action>=0,"Recovered supplies permit a genuine timed siege");
            g.Choose(action);
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>=94f&&
                g.OuterPatrolIntegrity==100f,
                "New relief siege has real unsaved clock and full cache integrity");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"A real melee attacker targets the defended cache");
            // Position hero on a legal tile outside melee interception range.
            // The attacker must walk toward the post, not passively drain HP.
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int x=-radius;x<=radius&&!found;x++)
            for(int y=-radius;y<=radius&&!found;y++)
            {
                Point candidate=new Point(post.x+x,post.y+y);
                if((candidate-new Point(post.x,post.y)).Length>11f||
                    (candidate-melee.Position).Length<6f||!g.CanStand(candidate))
                    continue;
                safe=candidate;found=true;
            }
            Check(found,"Find an actual legal bodyguard withdrawal foot");
            g.save.x=safe.x;g.save.y=safe.y;
            float before=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1250&&g.OuterPatrolActive&&
                    g.OuterPatrolIntegrity==before;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "Unintercepted raider actually completes a timed attack against supplies");
            float damaged=g.OuterPatrolIntegrity;
            // Only the physically present guard can apply the once-per-run
            // emergency patch. Do not touch permanent cache flag.
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            Check(g.conversation!=null,"Live outpost can report raid integrity");
            int patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"The ongoing defense offers a player-choice repair");
            g.Choose(patch);
            Check(g.OuterPatrolIntegrity>damaged&&g.OuterPatrolIntegrity<=100f&&
                g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "First legitimate cache patch spends real supplies and repairs durability");
            g.Talk(post);
            patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"Patch choice remains visible but is single-use");
            g.Choose(patch);
            Check(g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "Repeating a cached UI action cannot spend a second repair");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The post allows explicit safe withdrawal");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(room)&&g.save.coins==coins&&
                g.OuterCacheRecovered(room),
                "Aborted physical siege retires attackers, pays no false reward and preserves v2 cache");
            GameCore reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterCacheRecovered(room)&&
                !reload.OuterPatrolDone(room),
                "Transient siege pressure never contaminates legacy save slots");
        }

        public static void OuterPatrolRealTimeoutAndPerimeterDoNotForgeWins(Campaign data)
        {
            // Timeout is a real world clock, not a local-wall-clock task.
            // Stun actual raiders only to isolate the countdown from their
            // otherwise-tested genuine melee pressure.
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);
            g.save.room=0;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            int coins=g.save.coins;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Eligible physical outpost offers a real defense");
            g.Choose(start);
            foreach(Enemy attacker in g.enemies)attacker.stun=120f;
            for(int i=0;i<1860;i++)g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>1f&&
                g.OuterPatrolTime<3f&&g.OuterPatrolIntegrity==100f,
                "Combat clock advances on genuine simulation time without phantom supply damage");
            for(int i=0;i<80&&g.OuterPatrolActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins&&
                g.OuterCacheRecovered(0),
                "Real timeout retires only its raiders with no forged reward or lost v2 cache");
            g.Talk(post);
            start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Timed-out cache defense can be started again");
            g.Choose(start);
            Point away=new Point();bool found=false;
            for(int y=1;y<g.Map.rows.Length-1&&!found;y++)
            for(int x=1;x<g.Map.rows[y].Length-1&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if(g.CanStand(p)&&(p-new Point(post.x,post.y)).Length>13f)
                {away=p;found=true;}
            }
            Check(found,"Map has legal ground beyond the outpost perimeter");
            g.save.x=away.x;g.save.y=away.y;
            g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins,
                "Leaving the physical defense radius interrupts the run without paying");
            // The v2 save must never record ephemeral siege HP or clock.
            var reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterPatrolDone(0)==false&&
                reload.OuterCacheRecovered(0),
                "Pre-existing cache survives both failure paths without schema migration");
        }

        public static void DefendedOuterCachesBecomeRealSafeFieldRefuges(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    x=>x.kind=="outerwatch");
                Check(post!=null,"Each region has an authored supply post");
                GameCore g=new GameCore(data);g.mode="world";g.save.room=room;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.save.health=Math.Min(g.save.maxHealth-25f,45f);
                g.save.energy=21f;
                Check(!g.OuterPatrolRefuge,
                    "An unprotected outpost must not give free field treatment");
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Only a legitimately defended reachable outpost becomes a refuge");
                float hp=g.save.health, energy=g.save.energy;
                g.Tick(.05f,new Point());
                Check(g.save.health>hp+.06f&&g.save.energy>energy,
                    "Defended outpost actually replenishes finite combat resources");
                // The status comes from the original v2 flag, not a
                // separate migration or untracked runtime counter.
                GameCore reload=new GameCore(data,g.save);
                reload.save.room=room;reload.enemies.Clear();
                reload.projectiles.Clear();reload.hazards.Clear();
                reload.save.x=post.x;reload.save.y=post.y;
                Check(reload.save.version==2&&reload.OuterPatrolRefuge,
                    "Reloaded legacy save retains a physically usable outpost");
                // A real pursuer forces the field medic to stand down.
                Enemy pursuer=new Enemy(9000,0,post.x+.4f,post.y+.4f,30);
                g.enemies.Add(pursuer);
                Check(!g.OuterPatrolRefuge,
                    "A nearby actual enemy suppresses the otherwise active treatment");
                g.enemies.Clear();
                g.save.flags.Remove("outer_patrol_done_"+room);
                Check(!g.OuterPatrolRefuge,
                    "Removing a victory flag immediately removes its local benefit");
            }
        }

        public static void DefendedOutpostRoutesRespectLiveProgressAndSafeLanding(Campaign data)
        {
            WorldObject[] posts=new WorldObject[8];
            for(int i=0;i<8;i++)
                posts[i]=Array.Find(data.maps[i].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=posts[0].x;g.save.y=posts[0].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.stage=3;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_done_1");
            int coin=g.save.coins;
            g.Talk(posts[0]);
            int routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            Check(routes>=0,"Defended outpost offers a usable travel board");
            g.Choose(routes);
            int west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            Check(west>=0,"Travel board has a real regional route selection");
            g.Choose(west);
            int target=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_1");
            Check(target>=0,"Another genuinely defended reachable region appears");
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_2")<0,
                "Unsecured outposts cannot be selected to skip defense");
            g.Choose(target);
            Check(g.save.room==1&&g.CanStand(g.Position)&&
                g.OuterPatrolDone(0)&&g.OuterPatrolDone(1)&&
                g.save.coins==coin,
                "Existing map Enter and walkable landing preserve rewards and flags");
            GameCore reloaded=new GameCore(data,g.save);
            Check(reloaded.save.version==2&&reloaded.OuterPatrolDone(1),
                "Fast travel introduces no new save schema or unlock reset");

            g.save.x=posts[1].x;g.save.y=posts[1].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.Talk(posts[1]);
            routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            g.Choose(routes);
            // The safe outpost menu may remain open while a real raider
            // arrives. Stale travel actions must be reauthenticated.
            Enemy attacker=new Enemy(9111,0,posts[1].x+1f,posts[1].y,40f);
            g.enemies.Add(attacker);
            west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            g.Choose(west);
            Check(g.save.room==1&&g.save.coins==coin,
                "An arriving enemy blocks cached route selections");
            g.enemies.Clear();
            g.mode="world";
            // A forged/replayed link to a mission-locked region is not
            // authorized by the old SaveData win flag alone.
            var choose=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Live gameplay owns a guarded route handler");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "Unsecured destination cannot bypass physical campaign travel");
            g.save.flags.Add("outer_patrol_done_6");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "A won-but-locked region still cannot bypass its campaign entry");
        }

        public static void DefendedDesertAndHighlandPostsShelterAgainstActualWeather(Campaign data)
        {
            for(int room=6;room<=7;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null&&post.x>70f,
                    "The field refuge is out on the real exposed regional ring");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.save.x=post.x;g.save.y=post.y;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.teaUntil=0f;
                g.save.heat=room==6?65f:0f;
                g.save.cold=room==7?65f:0f;
                float initial=room==6?g.save.heat:g.save.cold;
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Defended station offers physical local shelter");
                g.Tick(.05f,new Point());
                float cooled=room==6?g.save.heat:g.save.cold;
                Check(cooled<initial,
                    "Real safe outpost suppresses and reduces local heat/cold buildup");
                // The shelter depends on a previously won v2 combat flag.
                // Removing it re-exposes the desert/snow as before.
                g.save.flags.Remove("outer_patrol_done_"+room);
                if(room==6)g.save.heat=initial;
                else g.save.cold=initial;
                g.Tick(.05f,new Point());
                float exposed=room==6?g.save.heat:g.save.cold;
                Check(exposed>initial,
                    "Undeveloped outpost has no free regional weather immunity");
                // Existing combat-threat logic must also suppress refuge.
                g.save.flags.Add("outer_patrol_done_"+room);
                g.enemies.Add(new Enemy(8810,0,post.x+1f,post.y,30f));
                Check(!g.OuterPatrolRefuge,
                    "A true close raider reopens survival pressure even at a rescued post");
            }
        }

        public static void ContinuousBiomeNoiseAvoidsSquareMaterialBoundaries(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                float first=WorldStreaming.BiomeVariation(44,56,room);
                Check(first>=0&&first<=1&&
                    first==WorldStreaming.BiomeVariation(44,56,room),
                    "Biome tint is deterministic for a given saved map coordinate");
                for(int y=48;y<60;y++)
                    for(int x=69;x<89;x++)
                    {
                        float a=WorldStreaming.BiomeVariation(x,y,room);
                        float b=WorldStreaming.BiomeVariation(x+1,y,room);
                        float c=WorldStreaming.BiomeVariation(x,y+1,room);
                        Check(a>=0&&a<=1&&Math.Abs(a-b)<.3f&&Math.Abs(a-c)<.3f,
                            "Adjacent material tints blend gradually rather than popping at eight-tile edges");
                    }
            }
            Check(Math.Abs(WorldStreaming.BiomeVariation(54,52,1)-
                WorldStreaming.BiomeVariation(54,52,7))>.0001f,
                "Forest and frost maps do not reuse identical material clusters");
        }

        public static void EightRidgeRoutesNeedBothPhysicalLandmarksAndSafeTravel(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                MapData map=data.maps[room];
                WorldObject east=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_0");
                WorldObject south=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_1");
                Check(east!=null&&south!=null&&g.CanStand(new Point(east.x,east.y))&&
                    g.CanStand(new Point(south.x,south.y)),
                    "Fifth ring links use two real standable authored locations");
                int coins=g.save.coins,shards=g.save.shards;
                g.save.x=east.x;g.save.y=east.y;
                g.Talk(east);
                Check(g.RidgeSeen(room,0)&&!g.RidgeLinked(room),
                    "Visiting only one marker never unlocks a free crossing");
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel")<0,
                    "An incomplete survey offers no teleport");
                g.Close();
                g.save.x=south.x;g.save.y=south.y;g.Talk(south);
                Check(g.RidgeLinked(room)&&g.save.coins==coins+20&&
                    g.save.shards==shards+1,"Second distinct physical visit enables permanent safe shortcut");
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                Check(choice>=0,"Completed survey offers actionable crossing choice");
                g.save.energy=36;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f&&Math.Abs(g.save.y-east.y)<.01f&&
                    Math.Abs(g.save.energy-24)<.01f,"Crossing moves the actual player and costs real energy");
                g.Talk(east);choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                g.save.energy=3;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f,
                    "Insufficient energy cannot cross the ridge");
                g.Talk(east);g.Close();
                Check(g.save.coins==coins+20,
                    "Revisiting completed markers never duplicates quest rewards");
                GameCore saved=new GameCore(data,g.save);
                Check(saved.save.version==2&&saved.RidgeLinked(room)&&
                    saved.save.coins==coins+20,
                    "New ridge progress restores from old SaveData v2");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)
            {all.save.flags.Add("ridge_seen_"+i+"_0");all.save.flags.Add("ridge_seen_"+i+"_1");}
            Check(all.RidgeLinkedCount==8&&all.GetQuest("ridge_routes").complete,
                "All eight distinct ridge paths finish the tracked exploration quest");
        }

        public static void FifthRingWildlifeSpawnsAsActualCombatAwayFromQuestMarkers(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("SpawnRoom",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(spawn!=null,"Use the real room-spawn transaction");
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);
                g.save.room=room;
                spawn.Invoke(g,null);
                int unlinkedFar=0;
                foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)unlinkedFar++;
                Check(unlinkedFar==0,
                    "Original quest rooms retain their pre-expansion enemy requirements");
                if(room>0)
                {
                    g.save.flags.Add("ridge_seen_"+room+"_0");
                    g.save.flags.Add("ridge_seen_"+room+"_1");
                    spawn.Invoke(g,null);
                    int early=0;
                    foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)early++;
                    Check(early==0,
                        "Early ridge discovery must never expand uncleared guardian encounters");
                    g.save.flags.Add("boss_"+room);
                    spawn.Invoke(g,null);
                }
                int far=0;
                foreach(Enemy e in g.enemies)
                    if(e.x>=74&&e.y>=60)
                    {
                        far++;
                        Check(g.CanStand(e.Position),
                            "New fifth-ring wildlife has physical passable collision");
                        foreach(WorldObject o in data.maps[room].objects)
                            Check((e.Position-new Point(o.x,o.y)).Length>=8,
                                "New wildlife remains away from real interaction points");
                        Check(e.hp>0&&e.maxHp>0,
                            "New wildlife consists of actual combat actors with hitpoints");
                    }
                if(room==0)Check(far==0,"Original town remains peaceful");
                else Check(far==3,"All seven dangerous regions gain three remote encounters");
            }
        }

        public static void TerrainVeilNeverCreatesOpaqueLoadingSquares(Campaign data)
        {
            Check(WorldStreaming.TerrainVeilOpacity(0)==.3f,
                "Newly entered world chunks show their actual terrain behind a light atmospheric veil");
            Check(WorldStreaming.TerrainVeilOpacity(1)==0,
                "Fully prewarmed terrain draws without an extra mask");
            float previous=1;
            for(int i=0;i<=100;i++)
            {
                float alpha=WorldStreaming.TerrainVeilOpacity(i/100f);
                Check(alpha>=0&&alpha<=.3f&&alpha<=previous,
                    "Chunk transitions never render opaque blank tiles or increase masking over time");
                previous=alpha;
            }
            for(int region=0;region<8;region++)
                Check(WorldStreaming.BiomeVariation(98,65,region)>=0&&
                    WorldStreaming.BiomeVariation(98,65,region)<=1,
                    "Static biome cache receives legal deterministic material values");
        }

        public static void SixthRingWeatherDialUnlocksRealSheltersAndPreservesSaveV2(Campaign data)
        {
            int[] answers={1,2,0,2,1,0,1,2};
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                MapData m=data.maps[room];
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="farsignal_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "All new sixth-ring weather sites occupy actual passable map tiles");
                }
                g.save.inventory[0]=5;g.save.inventory[4]=5;
                int coins=g.save.coins;
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.FarShelterBuilt(room),"Untuned refuge cannot be rebuilt");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);g.Close();
                Check(!g.FarDialReady(room),"An unread log forbids calibration");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.FarLogFound(room),"Real east field log unlocks clue");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int wrong=(answers[room]+1)%3;
                int idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+wrong);
                Check(idx>=0,"Real three-choice puzzle displays incorrect options");
                g.Choose(idx);
                Check(!g.FarDialReady(room),"Incorrect observation cannot unlock shared refuge");
                g.Talk(sites[1]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+answers[room]);
                Check(idx>=0,"Real field log's answer exists among the choices");
                g.Choose(idx);
                Check(g.FarDialReady(room),"Correct local glyph genuinely calibrates the dial");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_build");
                Check(idx>=0,"Calibrated shelter offers actual build action");
                g.Choose(idx);
                Check(g.FarShelterBuilt(room)&&g.save.inventory[0]==4&&
                    g.save.inventory[4]==4&&g.save.coins==coins+22,
                    "Building refuge consumes real materials and pays once");
                g.save.health=Math.Max(1,g.save.maxHealth-50);g.save.energy=4;g.save.cold=40;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
                Check(idx>=0,"Completed real shelter offers reusable warmth");
                g.Choose(idx);
                Check(g.save.inventory[0]==3&&g.save.energy==100&&
                    g.save.cold==15&&g.save.health>g.save.maxHealth-50,
                    "Timber-fed warm shelter restores real player stats");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.FarShelterBuilt(room)&&
                    loaded.save.coins==coins+22,
                    "Old save v2 persists the actual field puzzle and construction");
                loaded.save.x=sites[2].x;loaded.save.y=sites[2].y;
                loaded.Talk(sites[2]);
                Check(Array.FindIndex(loaded.conversation.choices,c=>c.action=="far_build")<0,
                    "Reading a restored shelter cannot farm the one-time build reward");
            }
            GameCore full=new GameCore(data);
            for(int room=0;room<8;room++)full.save.flags.Add("far_shelter_"+room);
            Check(full.FarShelterCount==8&&!full.GetQuest("far_refuges").complete,
                "Eight restored shelters still need real public filing at the Inn");
            full.save.room=9;
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,
                "Expanded Inn adds a real southern public records wing");
            var reach=new System.Collections.Generic.HashSet<int>();
            var queue=new System.Collections.Generic.Queue<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int first=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(first);reach.Add(first);
            int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int k=0;k<4;k++)
                {
                    int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*w+xx;
                    if(xx<0||yy<0||xx>=w||yy>=h||reach.Contains(key)||
                        !full.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    reach.Add(key);queue.Enqueue(key);
                }
            }
            WorldObject atlas=Array.Find(inn.objects,o=>o.kind=="faratlasdesk");
            WorldObject guest=Array.Find(inn.objects,o=>o.kind=="farrefugerecord");
            Check(atlas!=null&&guest!=null&&
                reach.Contains((int)atlas.y*w+(int)atlas.x)&&
                reach.Contains((int)guest.y*w+(int)guest.x),
                "Both Inn annex chambers are physically connected through real collision");
            full.save.x=atlas.x;full.save.y=atlas.y;
            int startCoins=full.save.coins;
            full.Talk(atlas);
            Check(full.conversation.text.IndexOf('\n')>=0&&
                full.conversation.textEn.IndexOf('\n')>=0,
                "Public inn atlas dialogue must format paragraphs as genuine newlines");
            int publish=Array.FindIndex(full.conversation.choices,c=>c.action=="far_publish");
            Check(publish>=0,"An actual Inn desk offers civic publication");
            full.Choose(publish);
            Check(full.Has("far_refuge_atlas")&&full.GetQuest("far_refuges").complete&&
                full.save.coins==startCoins+80,
                "In-person publication finishes quest with authentic once-only reward");
            GameCore reloaded=new GameCore(data,full.save);
            Check(reloaded.save.version==2&&reloaded.Has("far_refuge_atlas"),
                "Public civic atlas persists in legacy save slots");
            full.Talk(atlas);publish=Array.FindIndex(full.conversation.choices,
                c=>c.action=="far_publish");
            full.Choose(publish);
            Check(full.save.coins==startCoins+80,"Publishing twice cannot farm another grant");
        }

        public static void CameraLeadPrewarmsMovementWithoutUnboundedMapScans(Campaign data)
        {
            Check(WorldStreaming.PredictPrewarmLead(0)==0,
                "Still camera requests no unneeded directional prewarm");
            Check(WorldStreaming.PredictPrewarmLead(3)==4,
                "Following camera prepares forward chunks before arrival");
            Check(WorldStreaming.PredictPrewarmLead(-4)==-6,
                "Backward movement prepares the trailing viewport side");
            Check(WorldStreaming.PredictPrewarmLead(1000)==8&&
                WorldStreaming.PredictPrewarmLead(-1000)==-8,
                "Teleport or huge map never launches unbounded preloading");
            Check(WorldStreaming.PredictPrewarmLead(float.NaN)==0&&
                WorldStreaming.PredictPrewarmLead(float.PositiveInfinity)==0,
                "Bad camera input cannot corrupt streamed terrain bounds");
        }

        public static void SixthRingHiddenSuppliesAndLookoutsActuallyRevealMapSectors(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                var hidden=Array.Find(data.maps[room].objects,o=>o.kind=="farhidden");
                var vista=Array.Find(data.maps[room].objects,o=>o.kind=="farlookout");
                Check(hidden!=null&&vista!=null&&
                    g.CanStand(new Point(hidden.x,hidden.y))&&
                    g.CanStand(new Point(vista.x,vista.y)),
                    "Every sixth-ring secret must be physically accessible");
                Check(!g.Explored(room,83,75)&&!g.Explored(room,115,58),
                    "Remote viewpoints and hidden cache begin unexplored");
                g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);g.Close();
                Check(g.Explored(room,83,75)&&g.Explored(room,80,70)&&
                    !g.Explored(room,115,58),
                    "Real lookout reveals a bounded surrounding region without discovering distant secrets");
                int coins=g.save.coins,resources=g.save.inventory[room%3];
                g.save.x=hidden.x;g.save.y=hidden.y;g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9&&g.save.inventory[room%3]==resources+2,
                    "Remote concealed supplies pay actual one-time materials");
                g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9,"Hidden cache cannot duplicate rewards");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.Has("far_secret_"+room)&&
                    loaded.Has("far_vista_"+room)&&loaded.Explored(room,83,75),
                    "Hidden sixth-ring exploration survives legacy v2 save reload");
            }
        }

        public static void MapPreviewScalesToRealDynamicFloorAndRegionDimensions(Campaign data)
        {
            Check(Math.Abs(WorldStreaming.PreviewScale(120,80,306,207)-2.55f)<.001f,
                "Large sixth-ring map fits its world-map preview panel");
            Check(Math.Abs(WorldStreaming.PreviewScale(66,44,306,207)-
                (306f/66f))<.001f,
                "Runtime 66x44 abyss floor uses its real dimensions instead of old stub");
            Check(WorldStreaming.PreviewScale(0,44,306,207)==0,
                "Corrupt map dimensions cannot force an infinite texture allocation");
        }

        private static int BridgeDistance(GameCore g,int sx,int sy,int gx,int gy)
        {
            int w=g.Map.rows[0].Length,h=g.Map.rows.Length;
            var distances=new Dictionary<int,int>();var queue=new Queue<int>();
            int start=sy*w+sx;distances[start]=0;queue.Enqueue(start);
            int[,] directions={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%w,y=current/w;
                if(x==gx&&y==gy)return distances[current];
                for(int k=0;k<4;k++)
                {
                    int xx=x+directions[k,0],yy=y+directions[k,1],at=yy*w+xx;
                    if(xx<2||yy<2||xx>=w-2||yy>=h-2||distances.ContainsKey(at)||
                        !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    distances[at]=distances[current]+1;queue.Enqueue(at);
                }
            }
            return -1;
        }
        public static void RepairedBridgesOpenRealTerrainAndShortenWalkingDistance(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                var g=new GameCore(data);g.save.room=room;
                var bridge=Array.Find(g.Map.objects,o=>o.id=="far_bridge_"+room);
                Check(bridge!=null&&g.CanStand(new Point(bridge.x,bridge.y)),
                    "All bridge repair posts are physically accessible");
                Check(g.Blocked(111.5f,52.5f),"Broken spans block actual collision");
                int longRoute=BridgeDistance(g,111,49,112,73);
                Check(longRoute>0,"The old long route is not destructively removed");
                g.save.inventory[0]=5;g.save.inventory[4]=5;g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                Check(choice>=0,"Bridge has a legitimate material-consuming interaction");
                g.Choose(choice);
                Check(g.FarBridgeOpen(room)&&!g.Blocked(111.5f,52.5f)&&
                    g.save.inventory[0]==3&&g.save.inventory[4]==4,
                    "One repair consumes the actual resources and unlocks actual terrain");
                int shortRoute=BridgeDistance(g,111,49,112,73);
                Check(shortRoute>0&&shortRoute<longRoute/2,
                    "Repair creates a physically shorter route, not a fake quest reward");
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair")<0,
                    "Bridge repair cannot duplicate materials or rewards");
                var reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarBridgeOpen(room),
                    "Old v2 save reload preserves the constructed bridge flag even if story gates redirect the player");
                // In an unprogressed test save the normal story gates may
                // return to the safe town; only test collision after entering
                // the matching author's region again.
                reload.save.room=room;
                Check(!reload.Blocked(111.5f,52.5f),
                    "Returning to the reconstructed region restores real walkable collision");
            }
        }

        public static void AllEightRepairedBridgesAppearInQuestTracking(Campaign data)
        {
            var g=new GameCore(data);
            Check(Array.IndexOf(GameCore.QuestIds,"far_bridges")>=0,
                "Real bridge construction has a separate selectable journal quest");
            Check(!g.GetQuest("far_bridges").complete,
                "A new save has never repaired any bridge");
            for(int room=0;room<8;room++)g.save.flags.Add("far_bridge_"+room);
            Check(g.FarBridgeCount==8&&g.GetQuest("far_bridges").complete,
                "All eight repaired routes finish the tracked journal");
        }

        public static void InstantTravelPreparesVisibleWorldWithoutColdChunkPopin(Campaign data)
        {
            Check(!WorldStreaming.LargeCameraJump(1,2),
                "Ordinary walking still uses softened chunk transitions");
            Check(WorldStreaming.LargeCameraJump(20,0)&&
                WorldStreaming.LargeCameraJump(-12,-6),
                "Cross-map and ridge jumps immediately prime visible props");
            Check(!WorldStreaming.LargeCameraJump(float.NaN,0)&&
                !WorldStreaming.LargeCameraJump(float.PositiveInfinity,2),
                "Bad camera state never triggers unbounded terrain work");
        }

        public static void BridgeParcelNeedsRealTravelAndOnlyRewardsDelivery(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                WorldObject bridge=Array.Find(g.Map.objects,o=>o.kind=="farbridge");
                WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
                Check(bridge!=null&&shelter!=null,
                    "Courier actually uses existing physical bridge and refuge");
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start")<0,
                    "Unbuilt bridge or shelter cannot create supply rewards");
                g.Close();g.save.flags.Add("far_bridge_"+room);
                g.save.flags.Add("far_shelter_"+room);
                int coins=g.save.coins,shards=g.save.shards;
                float unloadedSpeed=g.Speed;
                g.Talk(bridge);
                int choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                Check(choose>=0,"Repaired bridge offers a physical parcel trip");
                g.Choose(choose);
                Check(g.FarCourierActive&&g.Speed<unloadedSpeed,
                    "A real carried parcel visibly affects character speed");
                g.save.x=shelter.x;g.save.y=shelter.y;
                g.Talk(shelter);g.Close();
                Check(!g.FarCourierDelivered(room)&&g.save.coins==coins,
                    "Teleporting to the shelter cannot fake the foot journey");
                g.Tick(.02f,new Point());
                Check(!g.FarCourierActive&&!g.FarCourierDelivered(room),
                    "Long position jump invalidates transient cargo");
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                g.Choose(choose);
                for(int step=0;step<100&&g.save.y<61.35f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check(g.FarCourierActive&&g.FarCourierCrossed,
                    "Engine ticks record actual southbound contact with rebuilt bridge tiles");
                WorldObject post=Array.Find(g.Map.objects,o=>o.kind=="farcheck");
                Check(post!=null&&g.CanStand(new Point(post.x,post.y))&&
                    (g.Position-new Point(post.x,post.y)).Length<2.2f,
                    "Intermediate cargo inspection is a reachable real-world station");
                g.Talk(post);
                int seal=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="far_courier_stamp");
                Check(seal>=0,"Checkpoint offers a genuine physical seal action");
                g.Choose(seal);
                Check(g.FarCourierStamped,"Civilian inspector actually stamps the carried cargo");
                for(int step=0;step<110&&g.save.y<73.2f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check((g.Position-new Point(shelter.x,shelter.y)).Length<2.2f,
                    "Normal real movement reaches the distant shelter before timeout");
                g.Talk(shelter);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_deliver");
                Check(choose>=0,"Actual refuge dialogue offers parcel delivery");
                g.Choose(choose);
                Check(g.FarCourierDelivered(room)&&!g.FarCourierActive&&
                    g.save.coins==coins+25&&g.save.shards==shards+1,
                    "Only legitimate foot delivery grants one real permanent reward");
                GameCore reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarCourierDelivered(room)&&
                    !reload.FarCourierActive,
                    "Old save v2 restores delivery only, never an invented held parcel");
                g.Talk(shelter);g.Close();
                Check(g.save.coins==coins+25,"Parcel delivery rewards cannot repeat");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)all.save.flags.Add("far_courier_"+i);
            Check(!all.GetQuest("far_couriers").complete&&all.FarCourierDeliveredCount==8,
                "Eight delivered parcels still require formal publication at the existing Inn");
        }

        public static void PublicParcelCharterRequiresEveryGenuineDeliveryAndImprovesShelters(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.flags.Add("far_refuge_atlas");
            g.save.room=9;
            WorldObject desk=Array.Find(g.Map.objects,o=>o.kind=="faratlasdesk");
            Check(desk!=null,"Original inn atlas remains an actual interactable desk");
            g.save.x=desk.x;g.save.y=desk.y;
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0,
                "Cannot publish civic supply charter without physical deliveries");
            g.Close();
            for(int room=0;room<8;room++)g.save.flags.Add("far_courier_"+room);
            int coins=g.save.coins;
            g.Talk(desk);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="far_courier_charter");
            Check(option>=0,"Completing all deliveries unlocks public filing at the actual inn");
            g.Choose(option);
            Check(g.Has("far_courier_charter")&&g.save.coins==coins+65&&
                g.GetQuest("far_couriers").complete,
                "Real public filing grants a single permanent network upgrade");
            GameCore reload=new GameCore(data,g.save);
            Check(reload.save.version==2&&reload.Has("far_courier_charter"),
                "Completed charter remains in old SaveData v2 after reload");
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0&&
                g.save.coins==coins+65,
                "Publishing civic accounts twice never duplicates rewards");
            g.Close();
            g.save.room=0;g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0"); // a shelter cannot be built without its earlier real calibration
            WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.inventory[0]=3;g.save.health=Math.Max(1,g.save.maxHealth-75);
            g.save.energy=8;g.save.cold=60;
            g.Talk(shelter);
            option=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
            Check(option>=0,"Existing public refuge still offers a repeatable physical rest");
            g.Choose(option);
            Check(g.save.energy==100&&g.save.cold==20&&
                g.save.health>=g.save.maxHealth-20&&g.save.inventory[0]==2,
                "Charter materially improves real healing and cold relief for the old wood price");
        }

        public static void NewTrailRamblingsKeepSaveV2AndPermanentRewards(Campaign data)
        {
            for (int room = 0; room < 8; room++)
            {
                MapData map = data.maps[room];
                Check(map.rows.Length == 80 && map.rows[0].Length == 120,
                    "Outdoor sixth-ring dimensions must be 120x80 in room " + room);
                foreach (string row in map.rows)
                    Check(row.Length == 120, "Every terrain row remains rectangular");
                Check(map.rows[13][17] == '=' && map.rows[14][18] == '=',
                    "Original central campaign road is retained");
                foreach (WorldObject o in map.objects)
                    if (o.kind == "waytrail")
                    {
                        Check(o.x >= 14 && o.y >= 22,
                            "New exploration marker stays beyond the old central authored area");
                        int x = (int)o.x, y = (int)o.y;
                        Check(map.rows[y][x] != '#' && map.rows[y][x] != '~' &&
                            map.rows[y][x] != 'f', "Trail marker is not walled into impassable terrain");
                    }
                Check(Array.FindAll(map.objects, o => o.kind == "waytrail").Length == 2,
                    "Each outdoor region gets two distinct readable waypoints");
            }
            GameCore game = new GameCore(data);
            WorldObject landmark = Array.Find(data.maps[0].objects, o => o.id == "waytrail0s");
            int coins = game.save.coins, shards = game.save.shards;
            game.Talk(landmark);
            Check(game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Recording a real waypoint grants a one-time survey reward");
            game.Close(); game.Talk(landmark);
            Check(game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Rereading a waypoint never duplicates rewards");
            SaveData stored = game.save;
            Check(stored.version == 2, "Waypoint flags must not alter SaveData version");
            // Annex fog has independent 6x6 flags; legacy 24-bit interior
            // exploration remains unchanged after the world expands.
            game.Close();
            game.save.x = 14.5f; game.save.y = 30.5f;
            Check(!game.Explored(0, 14, 30), "Unknown annex stays fogged before walking there");
            game.Tick(.02f, new Point());
            Check(game.Explored(0, 14, 30) && !game.Explored(0, 47, 23),
                "Visiting one annex must not reveal the distant eastern branch");
            GameCore loaded = new GameCore(data, stored);
            Check(loaded.Explored(0, 14, 30) && !loaded.Explored(0, 47, 23),
                "Annex exploration persists in existing save string flags");
            Check(loaded.Has("waytrail_waytrail0s") &&
                loaded.save.coins == coins + 8 && loaded.save.shards == shards + 1,
                "Waypoint discovery and rewards survive a normal save reload");
        }

        public static void ChapterEightRoadCharterUsesNewMapsAndChangesActualProgression(Campaign data)
        {
            var g = new GameCore(data);
            g.mode = "world";
            g.save.stage = 3; g.save.ending = 1; g.save.level = 10;
            WorldObject guild = Array.Find(data.maps[0].objects, o => o.kind == "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild);
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild") < 0,
                "Chapter Eight remains locked before the Chapter Seven hearing");
            g.Close();
            g.save.flags.Add("hearing_complete");
            g.save.flags.Add("hearing_orrin_aid");
            Check(g.RoadCharterReady && !g.RoadCharterStarted, "Signed hearing unlocks the road chapter");
            g.Talk(guild);
            int idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild presents a new chapter without removing prior dialogue choices");
            g.Choose(idx);
            Check(g.mode == "dialog", "Chapter Eight introduction is playable dialogue");
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_begin");
            Check(idx >= 0, "Lyra offers real expedition acceptance");
            g.Choose(idx);
            Check(g.RoadCharterStarted && g.save.trackedQuest == "roadcharter" &&
                g.RoadCharterReports == 0, "Starting new main chapter records a trackable objective");
            int preCoins = g.save.coins, preShards = g.save.shards;
            for (int room = 1; room <= 3; room++)
            {
                WorldObject marker = Array.Find(data.maps[room].objects,
                    o => o.id == "waytrail" + room + "e");
                g.save.room = room; g.save.x = marker.x; g.save.y = marker.y;
                g.mode = "world";
                g.Talk(marker);
                Check(g.RoadCharterReports == room &&
                    g.Has("roadcharter_report_" + room),
                    "Physical eastern survey advances exactly one regional main-story report");
                g.Close();
                g.Talk(marker); g.Close();
                Check(g.RoadCharterReports == room, "Rereading one marker cannot duplicate evidence");
            }
            Check(g.GetQuest("roadcharter").room == 0 &&
                g.GetQuest("roadcharter").target.x == guild.x,
                "Three surveyed regions route player back to Lyra");
            g.save.room = 0; g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild); idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild retains Chapter Eight resolution");
            g.Choose(idx);
            float priorSpeed = g.Speed;
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_public");
            Check(idx >= 0, "Civilian priority is a real player decision");
            g.Choose(idx);
            Check(g.RoadCharterComplete && g.Has("roadcharter_public") &&
                Math.Abs(g.Speed - priorSpeed - .25f) < .001f &&
                g.save.coins == preCoins + 3 * 8 + 45 &&
                g.save.shards == preShards + 3 + 3,
                "Chapter Eight's first ending rewards once and grants a real speed upgrade");
            SaveData snapshot = g.save;
            var loaded = new GameCore(data, snapshot);
            Check(snapshot.version == 2 && loaded.RoadCharterComplete &&
                loaded.Has("roadcharter_public") && loaded.RoadCharterReports == 3,
                "Road charter and collected testimony persist without a SaveData migration");
            g.Talk(guild);
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            g.Choose(idx); g.Close();
            Check(g.save.coins == snapshot.coins && g.save.shards == snapshot.shards,
                "Rereading completed public roads cannot farm the main-story payout");

            var reserve = new GameCore(data);
            reserve.save.flags.Add("hearing_complete");
            reserve.save.flags.Add("hearing_orrin_patrol");
            reserve.save.flags.Add("roadcharter_started");
            for (int room = 1; room <= 3; room++)
                reserve.save.flags.Add("roadcharter_report_" + room);
            reserve.save.stage = 3; reserve.save.ending = 1; reserve.save.level = 10;
            reserve.save.x = guild.x; reserve.save.y = guild.y;
            reserve.mode = "world";
            float speedBefore = reserve.Speed;
            reserve.Talk(guild);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_guild");
            reserve.Choose(idx);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_reserve");
            reserve.Choose(idx);
            Check(reserve.Has("roadcharter_reserve") && reserve.RoadCharterComplete &&
                Math.Abs(reserve.Speed - speedBefore) < .001f &&
                reserve.save.version == 2,
                "Alternative ending grants a distinct choice without quietly changing movement");
            float startEnergy = 15f;
            reserve.save.energy = startEnergy;
            // The production simulation deliberately clamps each frame to
            // 50 ms. Ten ordinary frames, not one artificial 500 ms tick,
            // must accumulate half a second of real regeneration.
            for (int frame = 0; frame < 10; frame++)
                reserve.Tick(.05f, new Point());
            Check(reserve.save.energy >= startEnergy + 8.4f &&
                reserve.save.energy < startEnergy + 8.7f,
                "Emergency-road charter adds +3 per second to real frame-by-frame stamina recovery");
        }

        public static void RidgeCrossingRejectsLiveEscortAndRemoteCombatThreats(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            g.save.flags.Add("ridge_seen_0_0");
            g.save.flags.Add("ridge_seen_0_1");
            WorldObject north=Object(g,"ridgemarker");
            WorldObject east=null,west=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.id=="ridge_route_0_0")east=o;
                if(o.id=="ridge_route_0_1")west=o;
            }
            Check(north!=null&&east!=null&&west!=null&&g.RidgeLinked(0),
                "Original linked ridge uses two distinct real map marker objects");
            g.save.x=west.x;g.save.y=west.y;g.save.energy=95;
            float energy=g.save.energy;
            var choose=typeof(GameCore).GetMethod("ChooseRidge",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Original world crossing has an authoritative action handler");
            var shot=new Projectile{position=new Point(east.x,east.y),
                velocity=new Point(1,0),damage=8,life=12,friendly=false,source="shot"};
            g.projectiles.Add(shot);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "Incoming live enemy projectile at the destination forbids unsafe remote travel");
            g.projectiles.Remove(shot);
            var warning=new Hazard{position=new Point(east.x,east.y),
                radius=1.2f,timer=4f,damage=15,kind="rift"};
            g.hazards.Add(warning);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "A still-armed delayed blast at the landing also blocks ridge travel");
            g.hazards.Remove(warning);
            var enemy=new Enemy(92921,0,east.x+1,east.y,70){timer=90};
            g.enemies.Add(enemy);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "An actual hostile defender at destination blocks shortcut traversal");
            g.enemies.Clear();
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(east.x,east.y)).Length<.01f&&
                g.save.energy==energy-12,
                "Once the far end is truly safe, the original paid crossing still works");

            // A separate original Noll course is a genuine field mission,
            // even when ordinary enemies and both ridge endpoints are safe.
            var training=Fresh(data,0);training.Enter(0);training.enemies.Clear();
            training.save.flags.Add("ridge_seen_0_0");
            training.save.flags.Add("ridge_seen_0_1");
            WorldObject forge=Object(training,"forge");
            training.save.x=forge.x;training.save.y=forge.y;
            Advance(training,1.1f);training.save.energy=100;
            Action(training,"forge","swordlesson_start");
            Check(training.SwordLessonActive,
                "The actual blacksmith opens a timed class-only projectile lesson");
            training.save.x=west.x;training.save.y=west.y;
            energy=training.save.energy;
            choose.Invoke(training,new object[]{"ridge_travel"});
            Check(training.SwordLessonActive&&
                (training.Position-new Point(west.x,west.y)).Length<.01f&&
                training.save.energy==energy,
                "Even a temporarily nearby shortcut cannot bypass or erase a running live training clock");
            training.PrepareSave();var copy=new GameCore(data,CloneSave(training.save));
            Check(copy.save.version==2&&copy.RidgeLinked(0)&&!copy.SwordLessonActive,
                "SaveData v2 preserves the earned linked route without serializing timed drills");
        }

    }
}
