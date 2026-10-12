using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static SaveData CloneSave(SaveData source)
        {
            // Copy actual public save fields, including arrays, to check reconstruction independently of Unity JSON.
            var copy = new SaveData();
            foreach (FieldInfo f in typeof(SaveData).GetFields())
            {
                object value = f.GetValue(source);
                if (value is int[]) value = ((int[])value).Clone();
                else if (value is EnemyRecord[])
                {
                    var original = (EnemyRecord[])value; var records = new EnemyRecord[original.Length];
                    for (int i = 0; i < records.Length; i++) if (original[i] != null) { var r = original[i]; records[i] = new EnemyRecord { id = r.id, kind = r.kind, boss = r.boss, x = r.x, y = r.y, hp = r.hp, maxHp = r.maxHp }; }
                    value = records;
                }
                else if (value is List<string>) value = new List<string>((List<string>)value);
                f.SetValue(copy, value);
            }
            return copy;
        }
        private static GameCore Unlocked(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report", "mountain_report", "gate_5", "gate_6", "gate_7", "companion_0", "companion_1", "companion_2", "seal_5", "seal_6", "seal_7" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 1; return g;
        }
        public static void RegionalBossesSweepAlternatingLanes(Campaign data)
        {
            var g = Unlocked(data);
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Regional Boss attacks must use the live production release");
            foreach (int room in new[] { 5, 6 })
            {
                g.Enter(room);
                var boss = new Enemy(901, 3, 29, 9, 600, true) { target = new Point(20, 12) };
                for (int phase = 1; phase <= 3; phase++)
                for (int cycle = 1; cycle <= 2; cycle++)
                {
                    boss.hp = phase == 1 ? 500 : phase == 2 ? 300 : 100;
                    boss.cycle = cycle;
                    g.hazards.Clear();
                    release.Invoke(g, new object[] { boss });
                    Check(g.hazards.Count == 3, "Regional release must warn all three lane contacts");
                    bool horizontal = room == 5 ? phase == 1 || cycle % 2 == 1
                                                : phase > 1 && cycle % 2 == 0;
                    for (int i = -1; i <= 1; i++)
                    {
                        Hazard h = g.hazards[i + 1];
                        float expectedX = 20 + (horizontal ? i * 2 : 0);
                        float expectedY = 12 + (horizontal ? 0 : i * 2);
                        float expectedDelay = (room == 5 ? 1.2f : .9f) + (phase == 3 ? (i + 1) * .22f : 0);
                        Check(Math.Abs(h.position.x - expectedX) < .001f && Math.Abs(h.position.y - expectedY) < .001f,
                            "The warning lane must rotate around the locked attack target");
                        Check(Math.Abs(h.timer - expectedDelay) < .001f && Math.Abs(h.radius - (room == 5 ? 1.35f : 1.1f)) < .001f,
                            "Each phase uses visible, dodgeable, correctly timed warning circles");
                        Check(h.kind == (room == 5 ? "tide" : "glass") && Math.Abs(h.damage - (room == 5 ? 17 : 18)) < .001f,
                            "Regional hazard identity and damage remain compatible");
                    }
                    Check(phase != 3 || g.hazards[0].timer < g.hazards[1].timer && g.hazards[1].timer < g.hazards[2].timer,
                        "Final-phase sweeps release in a visible sequence");
                }
            }
        }
        public static void FrostPrelateRotatesSafeBellExit(Campaign data)
        {
            var g = Unlocked(data);
            g.Enter(7);
            g.save.x = 20; g.save.y = 12;
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Frost Prelate must use the live regional Boss attack");
            var boss = new Enemy(902, 3, 29, 9, 600, true) { target = new Point(15, 8) };
            Point[] directions = { new Point(1, 0), new Point(0, 1), new Point(-1, 0), new Point(0, -1) };
            for (int phase = 1; phase <= 3; phase++)
            for (int cycle = 1; cycle <= 4; cycle++)
            {
                boss.hp = phase == 1 ? 500 : phase == 2 ? 300 : 100;
                boss.cycle = cycle;
                g.hazards.Clear();
                release.Invoke(g, new object[] { boss });
                Check(g.hazards.Count == (phase == 1 ? 1 : 4),
                    "First phase preserves one frost mark; later phases add three bells");
                Hazard mark = g.hazards[0];
                Check(mark.kind == "rift" && Math.Abs(mark.position.x - 20) < .001f &&
                      Math.Abs(mark.position.y - 12) < .001f && Math.Abs(mark.radius - 2) < .001f &&
                      Math.Abs(mark.timer - 1.5f) < .001f && Math.Abs(mark.damage - (20 + phase * 2)) < .001f,
                    "The original frost mark and damage remain intact");
                if (phase == 1) continue;
                int safe = (cycle - 1) % 4, strike = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (i == safe) continue;
                    Hazard bell = g.hazards[++strike];
                    Point expected = new Point(20, 12) + directions[i] * 3.5f;
                    float delay = 1.75f + (phase == 3 ? (strike - 1) * .22f : 0);
                    Check(Math.Abs(bell.position.x - expected.x) < .001f &&
                          Math.Abs(bell.position.y - expected.y) < .001f,
                        "The unmarked safe exit rotates through all four directions");
                    Check(bell.kind == "bell" && GameCore.HazardTheme(bell) == 7 &&
                          Math.Abs(bell.radius - 1.1f) < .001f &&
                          Math.Abs(bell.timer - delay) < .001f && Math.Abs(bell.damage - 14) < .001f,
                        "Blue bell warnings keep their timing and damage");
                }
                if (phase == 3)
                    Check(g.hazards[1].timer < g.hazards[2].timer &&
                          g.hazards[2].timer < g.hazards[3].timer,
                        "Final phase bell strikes must ring one after another");
            }
        }
        public static void RootReaperClosesRootPincers(Campaign data)
        {
            var g = Unlocked(data);
            g.StartAbyss(0);
            g.save.abyssFloor = 6;
            g.save.x = 29; g.save.y = 9;
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Root Reaper uses the production Boss attack release");
            var boss = new Enemy(903, 3, 29, 9, 680, true) { target = new Point(29, 9) };
            for (int risk = 0; risk <= 2; risk++)
            for (int phase = 1; phase <= 3; phase++)
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                g.save.abyssRisk = risk;
                boss.hp = phase == 1 ? 680 : phase == 2 ? 340 : 120;
                boss.cycle = cycle;
                g.hazards.Clear();
                release.Invoke(g, new object[] { boss });
                Check(g.hazards.Count == (phase == 1 ? 1 : phase == 2 ? 3 : 5),
                    "The Reaper gains two outer roots and then two inward roots");
                Hazard mark = g.hazards[0];
                Check(mark.kind == "rift" && Math.Abs(mark.timer - 1.5f) < .001f &&
                      Math.Abs(mark.radius - 2) < .001f && Math.Abs(mark.damage - (20 + phase * 2)) < .001f,
                    "The existing abyss mark is preserved");
                if (phase == 1) continue;
                Point axis = cycle % 2 == 1 ? new Point(1, 0) : new Point(0, 1);
                int index = 1;
                for (int side = -1; side <= 1; side += 2)
                {
                    Point outer = new Point(29, 9) + axis * (side * 2.8f);
                    Hazard h = g.hazards[index++];
                    Check(h.kind == "root" && GameCore.HazardTheme(h) == 8 &&
                          Math.Abs(h.position.x - outer.x) < .001f && Math.Abs(h.position.y - outer.y) < .001f &&
                          Math.Abs(h.radius - 1.05f) < .001f && Math.Abs(h.timer - 1.85f) < .001f &&
                          Math.Abs(h.damage - (16 + risk * 3)) < .001f,
                        "Outer roots alternate axes with a warning and risk-scaled damage");
                    if (phase == 3)
                    {
                        Point inner = new Point(29, 9) + axis * (side * 1.5f);
                        Hazard h2 = g.hazards[index++];
                        Check(h2.kind == "root" && Math.Abs(h2.position.x - inner.x) < .001f &&
                              Math.Abs(h2.position.y - inner.y) < .001f &&
                              Math.Abs(h2.radius - .9f) < .001f && Math.Abs(h2.timer - 2.25f) < .001f &&
                              Math.Abs(h2.damage - (16 + risk * 3)) < .001f,
                            "Final-phase roots close inward after the first warning");
                    }
                }
            }
            g.save.abyssFloor = 5;
            boss.hp = 120;
            g.hazards.Clear();
            release.Invoke(g, new object[] { boss });
            Check(g.hazards.Count == 1, "Other floors never spawn final Boss root pincers");
        }
        public static void MigrationAndBuilds(Campaign data)
        {
            var old = new SaveData { version = 1, stage = 1, room = 1, x = 5, y = 14, coins = 91, health = 72, kills = new[] { 0, 6, 0, 0, 0 }, flags = new List<string> { "intro", "scout_saved", "core_1", "lore_1" } };
            var g = new GameCore(data, old);
            Check(g.save.version == 2 && g.save.coins == 91 && g.save.kills[1] == 6 && g.save.kills.Length == data.maps.Length, "Old saves preserve progress and extend kill counters");
            Check(g.Has("lore_1") && g.save.companion == -1 && g.save.relics[0] == -1, "Migration must not invent companions or relics");
            g.save.shards = 60; int before = g.save.shards; Check(!g.RefineRelics() && g.save.shards == before, "Refining needs the town workshop");
            g.Enter(0); g.save.coins = 500; for (int i = 0; i < 5; i++) g.save.inventory[i] = 100;
            Check(g.BuildProject(2), "Workshop builds in town"); Check(g.RefineRelics() && g.save.relicRank == 1 && g.save.shards == 52, "Refining has a real shard cost");
            g.save.flags.Add("relic_0"); g.save.flags.Add("relic_5"); float damage = g.Damage;
            g.EquipRelic(0, 0); Check(g.Damage == damage + 2, "Forging increases damage only while a relic is equipped");
            g.EquipRelic(1, 0); Check(g.save.relics[0] == -1 && g.save.relics[1] == 0, "A relic cannot occupy both slots");
            g.EquipRelic(0, 2); Check(g.save.relics[0] == -1, "Unknown relics cannot be equipped");
            g.save.flags.Add("technique_0"); g.save.level = 5; g.save.skillPoints = 1; g.LearnTechnique(); Check(g.save.technique == 1, "Learned technique can be switched");
            g.save.flags.Add("companion_0"); g.SelectCompanion(0); g.save.health = 20; g.mode = "world"; g.CommandCompanion();
            Check(g.save.health == 20 && g.companionCooldown > 0 && g.companionAction != null, "Companion command starts preparation before its effect");
            Advance(g, .22f); Check(g.save.health > 20, "Companion command has its original gameplay effect on release");
            float hp = g.save.health; g.CommandCompanion(); Check(g.save.health == hp, "Companion command respects cooldown");
            var bad = CloneSave(g.save); bad.relics = new[] { 5, 5 }; bad.heat = float.NaN; bad.cold = float.PositiveInfinity; bad.projects = null; bad.boons = new[] { 500, -20, 30 }; bad.flags.Add(null);
            g = new GameCore(data, bad); Check(g.save.relics[1] == -1 && g.save.heat == 0 && g.save.cold == 0 && g.save.projects.Length == 3 && !g.save.flags.Contains(null), "Expansion save fields are sanitized");
        }
        private static HashSet<int> Reachable(GameCore g)
        {
            int width = g.Map.rows[0].Length, height = g.Map.rows.Length;
            var queue = new Queue<int>(); var seen = new HashSet<int>(); int start = (int)g.save.y * width + (int)g.save.x;
            queue.Enqueue(start); seen.Add(start); int[,] steps = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), x = cell % width, y = cell / width;
                for (int i = 0; i < 4; i++)
                {
                    int xx = x + steps[i, 0], yy = y + steps[i, 1], next = yy * width + xx;
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height || g.Blocked(xx + .5f, yy + .5f) || seen.Contains(next)) continue;
                    seen.Add(next); queue.Enqueue(next);
                }
            }
            return seen;
        }
        private static void AssertObjectsReachable(GameCore g)
        {
            var seen = Reachable(g); int width = g.Map.rows[0].Length;
            foreach (WorldObject o in g.Map.objects)
            {
                bool near = false; foreach (int cell in seen) if ((new Point(cell % width + .5f, cell / width + .5f) - new Point(o.x, o.y)).Length < 1.7f) { near = true; break; }
                Check(near, "Unreachable distant object in room " + g.save.room + ": " + o.id);
            }
        }
        public static void DistantReachability(Campaign data)
        {
            var g = Unlocked(data);
            for (int room = 5; room <= 7; room++)
            {
                g.Enter(room); if (room == 5) g.save.tideHeldUntil = g.save.totalTime + 90;
                AssertObjectsReachable(g);
                foreach (Enemy e in g.enemies) Check(!g.Blocked(e.x, e.y), "Enemy spawned inside blocked terrain");
            }
            g.Enter(5); g.enemies.Clear(); g.save.tideHeldUntil = 0; g.save.totalTime = 21.99f; g.save.x = 5.5f; g.save.y = 8.5f;
            g.Tick(.02f, new Point()); Check(!g.Blocked(g.save.x, g.save.y), "Rising tide retreats safely to a bank");
            g.Enter(0);
            for (int seed = 1; seed <= 4; seed++)
            {
                g.StartAbyss(0); g.save.abyssSeed = seed; g.save.abyssFloor = seed; g = new GameCore(data, CloneSave(g.save)); g.Start();
                AssertObjectsReachable(g);
                string layout = string.Join("", g.Map.rows); var restored = new GameCore(data, CloneSave(g.save));
                Check(string.Join("", restored.Map.rows) == layout, "Seeded dungeon reconstructs the same map");
                g.ReturnToTown(); g.Choose(0);
            }
            g.StartAbyss(0); Enemy target = g.enemies[0]; g.save.x = target.x; g.save.y = target.y; g.Skill(); g.PrepareSave();
            int remaining = g.enemies.Count; var hpById = new Dictionary<int, float>(); foreach (Enemy e in g.enemies) hpById[e.id] = e.hp;
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.enemies.Count == remaining && resumed.save.runCoins == g.save.runCoins, "Partial floor resumes without respawning defeated enemies or resetting pending loot");
            foreach (Enemy e in resumed.enemies) Check(hpById.ContainsKey(e.id) && Math.Abs(e.hp - hpById[e.id]) < .01f, "Enemy health survives the checkpoint");
        }
        public static void TechniquesAndTradeoffs(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); g.save.level = 5; g.save.skillPoints = 1; g.LearnTechnique();
                Check(g.save.technique == 1 && g.save.skillPoints == 0, "New specialization spends a real point");
                g.Enter(1); g.enemies.Clear(); g.save.x = 5; g.save.y = 14; g.aim = new Point(1, 0);
                g.enemies.Add(new Enemy(20, 0, 8, 14, 300)); g.enemies.Add(new Enemy(21, 0, 10, 14, 300));
                g.projectiles.Add(new Projectile { position = new Point(6, 14), velocity = new Point(), damage = 10 });
                g.Skill();
                Advance(g, .3f);
                if (profession == 0) Check(g.enemies[0].hp < 300 && g.projectiles.Count == 0 && g.guardTime > 3.8f, "Vanguard counter staggers and clears shots");
                for (int i = 0; i < 100; i++) g.Tick(.02f, new Point());
                if (profession == 1) Check(g.enemies[0].hp < 300 && g.enemies[1].hp < 300, "Woodpiercer hits multiple targets on one trajectory");
                if (profession == 2) Check(g.enemies[0].hp < 300 && g.effects.Exists(e => e.kind == "frost"), "Frost specialization pulses real damage and control");
            }
            var rpg = Fresh(data); rpg.save.flags.Add("relic_2"); float speed = rpg.Speed; rpg.EquipRelic(0, 2); rpg.Dash(new Point(1, 0));
            Check(rpg.Speed > speed && rpg.save.energy == 75, "Wind Compass trades extra energy for speed");
            rpg.save.flags.Add("companion_0"); rpg.save.inventory[6] = 2; rpg.CompanionStory(0);
            int gift = -1; for (int i = 0; i < rpg.conversation.choices.Length; i++) if (rpg.conversation.choices[i].action == "gift0") gift = i;
            rpg.Choose(gift); rpg.CompanionStory(0); rpg.Choose(gift); Check(rpg.save.inventory[6] == 1 && rpg.save.relationships[0] == 1, "A shared meal builds a bond only once per day");
        }
        public static void ClimateAndContracts(Campaign data)
        {
            var g = Unlocked(data); g.Enter(6); g.enemies.Clear(); g.save.x = 14; g.save.y = 10;
            for (int i = 0; i < 1450; i++) g.Tick(.05f, new Point());
            Check(g.save.heat > 80 && g.save.health < g.save.maxHealth, "Unprotected desert exposure is hazardous");
            g.save.tea = 1; g.DrinkTea(); Check(g.save.heat == 0 && g.save.tea == 0 && g.save.teaUntil > g.save.totalTime, "Tea has a real climate effect");
            for (int i = 0; i < 100; i++) g.Tick(.05f, new Point()); Check(g.save.heat == 0, "Tea protects during its buff");
            g.Enter(7); g.save.teaUntil = 0; g.enemies.Clear(); g.save.x = 17; g.save.y = 14;
            for (int i = 0; i < 1400; i++) g.Tick(.05f, new Point()); Check(g.save.cold > 70, "Cold accumulates outside shelter");
            g.save.inventory[0] = 2; Action(g, "brazier", "warm:brazier0"); g.save.x = 6; g.save.y = 14;
            for (int i = 0; i < 200; i++) g.Tick(.05f, new Point()); Check(g.save.cold < 5, "Lit braziers warm the player");
            g.Enter(0); g.AcceptBounty(2); Check(!g.ClaimBounty(), "Contract cannot pay before progress");
            for (int fish = 0; fish < 3; fish++) { Action(g, "fish", "fish"); for (int hit = 0; hit < 3; hit++) { g.fishCursor = .5f; g.FishHit(); } }
            Check(g.save.bountyProgress == 3, "Actual catches advance the contract"); int coins = g.save.coins; Check(g.ClaimBounty() && g.save.coins == coins + 35, "Completed contract pays once");
            Check(!g.ClaimBounty(), "Claim cannot be repeated");
            g.Enter(6); g.enemies.Clear(); g.save.mirrors = new[] { 1, 2, 1 }; g.save.x = 14; g.save.y = 18;
            RunRace(g); Check(g.Has("race_finished") && g.save.bestRace > 0 && !g.racing, "All race checkpoints finish via movement");
            int shards = g.save.shards; coins = g.save.coins; RunRace(g); Check(g.save.shards == shards && g.save.coins == coins, "Race rewards cannot be farmed by replaying");
        }
        private static void RunRace(GameCore g)
        {
            g.save.x = 14; g.save.y = 18; Action(g, "skiff", "race"); int frames = 0;
            while (g.racing && frames++ < 4000)
            { Point toward = g.DirectionTo(g.Position, GameCore.RaceRoute[g.raceCheckpoint]); g.Dash(toward); g.Tick(.02f, toward); }
            Check(frames < 4000 && g.raceCheckpoint == GameCore.RaceRoute.Length, "Race route is completable within its timer");
        }
        private static GameCore ShoreRun(Campaign data, int profession, int pledge, int covenant)
        {
            var g = Fresh(data, profession);
            g.Enter(5); Check(g.save.room == 0, "The distant campaign needs the first starcore");
            g.Enter(1); Combat(g, false); Action(g, "scout", "scout"); g.Talk(Object(g, "lore")); g.Close(); Gather(g);
            AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "shrine", "boss"); Combat(g, true); Action(g, "shrine", "goto0"); Action(g, "guild", "deliver");
            Action(g, "envoy", "exp_begin"); Check(g.save.room == 5, "Envoy starts the expansion after chapter one"); Gather(g); Action(g, "captain", "pledge" + pledge);
            for (int i = 0; i < 3; i++) { Action(g, "relay" + i, "relay" + i); Action(g, "castaway" + i, "rescue" + i); }
            Check(g.GateOpen, "Every harbor priority reaches the lighthouse");
            g.Talk(Object(g, "manifest")); g.Close(); Action(g, "sluice", "holdtide"); Combat(g, false); g.save.x = 28; g.save.y = 10;
            AwaitArenaOpening(g); Action(g, "expAltar", "expboss"); Combat(g, true); Action(g, "captain", "harbor_report"); Check(g.Has("companion_0") && g.Has("relic_0"), "Harbor report has permanent rewards");
            Action(g, "captain", "personal0"); Check(g.Has("personal_0"), "Mira's story uses the discovered manifest");
            g.Enter(6); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea();
            g.Talk(Object(g, "mirror0")); g.Talk(Object(g, "mirror1")); Check(!g.GateOpen, "A partial light route cannot open the gate");
            g.Talk(Object(g, "mirror1")); g.Talk(Object(g, "mirror2")); Check(g.GateOpen && g.LightRays().Count == 4, "A connected beam opens the glass gate");
            Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); Combat(g, true); Action(g, "tinker", "desert" + (profession == 1 ? 2 : 1));
            Check(g.Has("companion_1") && g.CanEnter(7), "Desert decision recruits Ivo and opens the highlands"); Action(g, "tinker", "personal1"); Check(g.Has("personal_1"), "Ivo's story repairs a real machine");
            g.Enter(7); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea(); g.Talk(Object(g, "bell0")); g.Talk(Object(g, "bell0")); g.Talk(Object(g, "bell2"));
            Check(g.GateOpen, "The three tone settings open Frostchime"); Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); Combat(g, true);
            Action(g, "keeper", "mountain" + (profession == 0 ? 1 : 2)); Action(g, "keeper", "personal2"); Check(g.Proofs == 3 && g.Has("personal_2") && g.Has("relic_4"), "Three reports and personal arcs complete");
            g.Enter(0); Check(g.BuildProject(0) && g.BuildProject(0) && g.BuildProject(1), "Recovered resources can fund the shared-rule requirements");
            Check(g.CouncilPactReady, "All factions and projects support shared governance");
            g.Enter(5); Action(g, "council", "covenant" + covenant); Check(g.save.frontierEnding == covenant && g.CanEnter(8), "Each covenant opens the abyss");
            return g;
        }
        private static void Gather(GameCore g)
        { foreach (WorldObject o in g.Map.objects) if (o.kind == "wood" || o.kind == "ore" || o.kind == "herb") g.Talk(o); }
        public static void DistantCampaign(Campaign data)
        { for (int i = 0; i < 3; i++) ShoreRun(data, i, i + 1, i + 1); }
        public static void AbyssExpeditions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = ShoreRun(data, profession, 3, 3); g.Close(); g.Enter(0); g.EquipRelic(0, 0); g.EquipRelic(1, 4); g.AcceptBounty(5); g.StartAbyss(0);
                for (int floor = 1; floor <= 6; floor++)
                {
                    int coins = g.save.coins, shards = g.save.shards; Combat(g, false);
                    Check(g.save.coins == coins && g.save.shards == shards && g.save.runCoins > 0, "Abyss loot is pending until banked");
                    if (floor == 2) { g = new GameCore(data, CloneSave(g.save)); g.Start(); Check(g.enemies.Count == 0 && g.save.runCoins > 0, "A cleared floor and pending loot survive reconstruction"); }
                    if (floor < 6) Action(g, "abyssGate", "boon" + ((floor - 1) % 3)); else Action(g, "abyssGate", "abyssBank");
                    Check(g.save.coins > coins && g.save.shards > shards, "Every cleared floor can bank rewards");
                }
                Check(g.save.room == 0 && g.save.bestFloor == 6 && g.Has("abyss_complete") && g.Has("relic_5"), "All classes can complete the six-floor expedition");
                Check(g.save.bountyProgress == 2 && g.ClaimBounty(), "Banked floors complete the exploration contract");
                g.save.stage = 3; g.Enter(4); Action(g, "shrine", "parley");
                Check(g.Has("warden_stood_down") && g.Has("boss_4") && !g.enemies.Exists(e => e.boss), "The working covenant can peacefully resolve the original confrontation");
                int rewardCoins = g.save.coins; Action(g, "shrine", "ending1"); g.Choose(0); Check(g.save.coins == rewardCoins, "Peaceful resolution does not reward twice");
                int banked = g.save.shards; g.StartAbyss(2); g.enemies.Clear(); g.save.runShards = 9; g.save.runCoins = 50; g.save.runXp = 40; g.save.health = 1; g.invulnerable = 0;
                g.enemies.Add(new Enemy(900, 0, g.save.x, g.save.y, 999) { state = 1, timer = 0 }); g.Tick(.02f, new Point()); Advance(g, 1);
                Check(g.save.room == 0 && g.save.shards == banked && g.save.runShards == 0 && g.save.abyssFloor == 0, "Failure loses pending loot only and safely returns home");
            }
        }
    }
}
