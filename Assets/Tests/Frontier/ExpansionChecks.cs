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
        private static Point StandableOutsideInteractionReach(GameCore g, WorldObject target)
        {
            for (int ix = -8; ix <= 8; ix++)
            for (int iy = -8; iy <= 8; iy++)
            {
                float dx = ix * .25f, dy = iy * .25f;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance < 1.8f || distance > 2.15f) continue;
                Point candidate = new Point(target.x + dx, target.y + dy);
                if (g.CanStand(candidate)) return candidate;
            }
            throw new InvalidOperationException("No standable out-of-range test position was found.");
        }

        private static bool TryFindWallBlockedApproach(GameCore g, WorldObject target,
            out Point playerPosition)
        {
            string[] rows = g.Map.rows;
            int height = rows.Length, width = rows[0].Length;
            for (int y = 3; y < height - 3; y++)
            for (int x = 4; x < width - 3; x++)
            {
                // A solid tile between two otherwise standable cells gives a
                // deterministic line-of-sight fixture without changing terrain.
                if (rows[y][x - 1] != '#') continue;
                Point targetPoint = new Point(x + .3f, y + .5f);
                Point approach = new Point(x - 1.25f, y + .5f);
                if (!g.CanStand(targetPoint) || !g.CanStand(approach) ||
                    (targetPoint - approach).Length >= 1.75f) continue;
                target.x = targetPoint.x;
                target.y = targetPoint.y;
                playerPosition = approach;
                return true;
            }
            playerPosition = new Point();
            return false;
        }

        public static void AbyssOptionalInteractionsRequirePhysicalReach(Campaign data)
        {
            var g = new GameCore(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            WorldObject survey = Array.Find(g.Map.objects, o => o.kind == "abyssSurvey");
            WorldObject farCache = Array.Find(g.Map.objects, o => o.kind == "abyssFarCache");
            WorldObject gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            WorldObject exit = Array.Find(g.Map.objects, o => o.kind == "abyssExit");
            Check(survey != null && farCache != null && gate != null && exit != null,
                "Abyss survey, lower-gallery cache, gate and exit are real world interactions");

            // A cleared floor still needs a physical visit to its gate. Opening
            // its remote dialogue must not set the bankable-clear flag as a side effect.
            g.enemies.Clear();
            g.save.x = 5; g.save.y = 14;
            g.Talk(gate);
            Check(!g.Has("abyss_clear_1") && g.mode == "world" && g.toast.Contains("走近"),
                "The root gate cannot mark a floor cleared before physical arrival");
            g.Talk(exit);
            Check(g.mode == "world" && g.toast.Contains("走近"),
                "The abyss exit cannot open an escape interaction from across the floor");
            TalkNear(g, "abyssGate");
            Check(g.Has("abyss_clear_1") && g.mode == "dialog",
                "A reachable root gate still opens the existing boon and banking choices");
            g.Close();

            int startXp = g.save.runXp;
            Point remoteSurvey = StandableOutsideInteractionReach(g, survey);
            g.save.x = remoteSurvey.x;
            g.save.y = remoteSurvey.y;
            g.Talk(survey);
            Check(!g.Has("abyss_annex_survey_1") && g.save.runXp == startXp &&
                g.mode == "world" && g.toast.Contains("走近"),
                "Abyss survey cannot record or award XP outside physical interaction reach");

            float oldX = farCache.x, oldY = farCache.y;
            Point blockedApproach;
            bool foundBlockedApproach = TryFindWallBlockedApproach(g, farCache,
                out blockedApproach);
            Check(foundBlockedApproach,
                "The generated abyss contains a standable point separated from a gallery target by a wall");
            if (foundBlockedApproach)
            {
                int coins = g.save.runCoins, shards = g.save.runShards;
                g.save.x = blockedApproach.x;
                g.save.y = blockedApproach.y;
                g.Talk(farCache);
                Check(!g.Has("abyss_annex_far_cache_1") &&
                    g.save.runCoins == coins && g.save.runShards == shards &&
                    g.mode == "world" && g.toast.Contains("走近"),
                    "Lower-gallery cache cannot award loot through a blocking wall");
            }
            farCache.x = oldX;
            farCache.y = oldY;

            g.save.x = survey.x;
            g.save.y = survey.y;
            g.Talk(survey);
            Check(g.Has("abyss_annex_survey_1") && g.save.runXp == startXp + 15,
                "A physically reachable abyss survey retains its intended reward");
            g.Close();

            int beforeCoins = g.save.runCoins, beforeShards = g.save.runShards;
            g.save.x = farCache.x;
            g.save.y = farCache.y;
            g.Talk(farCache);
            Check(g.Has("abyss_annex_far_cache_1") &&
                g.save.runCoins == beforeCoins + 30 &&
                g.save.runShards == beforeShards + 1,
                "A physically reachable lower-gallery cache retains its intended reward");
        }

        public static void CivicConduitCannotOverlapActiveFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"gate_4","core_1","core_2","core_3",
                "last_directive_complete","last_directive_open"})
                g.save.flags.Add(flag);
            g.save.inventory[4]=1;
            g.Enter(4);g.enemies.Clear();
            WorldObject master=Object(g,"civicconduitmaster");
            g.save.x=master.x;g.save.y=master.y;
            SetOuterPatrolLull(g,4);
            Check(g.OuterPatrolActive&&g.SafeTravel&&g.LastDirectiveComplete&&
                g.LastDirectiveChoice==1,
                "The western conductor is available during a locally safe patrol lull");
            Action(g,master.kind,"civic_conduit_start");
            Check(!g.CivicConduitActive&&g.save.inventory[4]==1,
                "The timed civic conductor cannot overlap patrol fieldwork or consume its fuse");
            ClearOuterPatrolLull(g);
        }

        public static void LastDirectiveCannotStartDuringActiveFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"gate_4","core_1","core_2","core_3",
                "last_directive_started","last_directive_clue_0",
                "last_directive_clue_1","last_directive_clue_2"})
                g.save.flags.Add(flag);
            g.Enter(4);g.enemies.Clear();
            WorldObject core=Object(g,"lastdirectivecore");
            g.save.x=core.x;g.save.y=core.y;
            SetOuterPatrolLull(g,4);
            Check(g.LastDirectiveReady&&g.OuterPatrolActive&&g.SafeTravel,
                "The directive console is safe locally while another patrol still owns the fieldwork slot");
            Action(g,core.kind,"last_directive_code_0");
            Check(g.LastDirectiveWave==0&&
                !g.enemies.Exists(e=>e.id>=90300&&e.id<=90304),
                "The first directive wave cannot start during an active patrol lull");
            ClearOuterPatrolLull(g);
            Action(g,core.kind,"last_directive_code_0");
            Check(g.LastDirectiveWave==1&&
                g.enemies.FindAll(e=>e.id>=90300&&e.id<=90304).Count==2,
                "The directive's real first wave remains available once the other fieldwork ends");
        }

        public static void RoadwardenDefenseCannotOverlapUnrelatedFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"dispatch_complete","survey_1"})
                g.save.flags.Add(flag);
            g.Enter(1);g.enemies.Clear();
            WorldObject beacon=Object(g,"survey1");
            g.save.x=beacon.x;g.save.y=beacon.y;
            SetOuterPatrolLull(g,1);
            Check(g.OuterPatrolActive&&g.SafeTravel,
                "The beacon is locally safe during an active patrol lull");
            Action(g,beacon.kind,"road_trial_start_1");
            Check(g.RoadwardenTrialRoom==0&&
                !g.enemies.Exists(e=>e.id>=81000&&e.id<81100),
                "A roadwarden defense cannot spawn a wave over unrelated fieldwork");
            ClearOuterPatrolLull(g);
            Action(g,beacon.kind,"road_trial_start_1");
            Check(g.RoadwardenTrialRoom==1&&
                g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                "The real two-wave defense remains startable after the competing mission ends");
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
        public static void CompanionPartySwapStartsFromPlantedOriginalGait(Campaign data)
        {
            var g = Unlocked(data);
            g.SelectCompanion(0);
            Check(g.save.companion == 0,
                "Unlocked starting follower must be selectable");
            g.companionMoveTime = 4.25f;
            g.companionMoving = true;
            var pending = new ActorAction {
                kind = "command", weapon = 0, duration = .42f,
                contact = .16f, direction = new Point(1,0)
            };
            g.companionAction = pending;
            int firstGeneration=g.companionPresentationGeneration;
            g.SelectCompanion(0); // Existing command must never be canceled.
            Check(g.companionAction == pending &&
                Math.Abs(g.companionMoveTime-4.25f)<.00001f &&
                g.companionMoving &&
                g.companionPresentationGeneration==firstGeneration,
                "Reselecting the SAME ally may not rewind feet, invalidate pose or cancel commands");
            g.SelectCompanion(1);
            Check(g.save.companion == 1 &&
                g.companionPresentationGeneration==firstGeneration+1 &&
                g.companionAction == null && !g.companionMoving &&
                g.companionMoveTime == 0f &&
                g.companionAim.Length > .9f,
                "A real follower swap must not inherit the old outfit's airborne stride");
            g.companionMoveTime = 2.4f;
            g.companionMoving = true;
            g.SelectCompanion(-1);
            Check(g.save.companion == -1 &&
                g.companionPresentationGeneration==firstGeneration+2 &&
                !g.companionMoving && g.companionMoveTime == 0f,
                "Dismissed follower may not retain phantom walking animation");
            // A same-ID return separated by an unseen dismissal must not
            // inherit the old movement phase merely because the name matches.
            g.SelectCompanion(0);
            Check(g.save.companion == 0 &&
                g.companionPresentationGeneration==firstGeneration+3 &&
                g.companionMoveTime==0f,
                "Recruited-again original companion requires a fresh presentation generation");
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
        public static void ClimatePulsesKeepTheirRealWorldSecondAtVariableFPS(Campaign data)
        {
            // A stable unshaded desert exposure already exists at this
            // playable coordinate. Drive the entire real world Tick path;
            // no fake timer call or manual damage application.
            foreach(float dt in new[]{.05f,.027f})
            {
                var g=Unlocked(data);g.Enter(6);g.enemies.Clear();
                g.save.x=14;g.save.y=10;
                g.save.teaUntil=0;
                g.save.health=g.save.maxHealth=1000;
                g.save.heat=100;
                g.save.reducedMotion=true;
                int frames=dt>.04f?427:791;
                for(int frame=0;frame<frames;frame++)
                    g.Tick(dt,new Point());
                float elapsed=frames*dt;
                float expected=1f-(elapsed%1f);
                var timer=typeof(GameCore).GetField("climateTimer",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                float remaining=(float)timer.GetValue(g);
                Check(Math.Abs(remaining-expected)<.015f,
                    "Heat pulse remaining time must conserve every frame's fractional overshoot at "+dt);
                Check(Math.Abs(g.save.health-916f)<.001f,
                    "Unprotected desert must apply exactly 21 physical heat contacts in 21.35s at "+dt);
            }
        }
        public static void AbyssRiftsKeepTheirSixSecondWorldClock(Campaign data)
        {
            foreach(float dt in new[]{.05f,.027f})
            {
                var g=Unlocked(data);g.StartAbyss(0);
                Check(g.InAbyss,"Test needs the actual running abyss dungeon");
                g.enemies.Clear();g.hazards.Clear();g.effects.Clear();
                // Test the real floor-three storm rather than creating
                // fake hazards. Floor/seed changes are test-local only.
                g.save.abyssFloor=3;g.save.health=g.save.maxHealth=1000;
                g.save.reducedMotion=true;
                var seen=new HashSet<Hazard>();
                int frames=dt>.04f?817:1513;
                for(int frame=0;frame<frames;frame++)
                {
                    g.Tick(dt,new Point());
                    foreach(Hazard h in g.hazards)
                        if(h.kind=="rift")seen.Add(h);
                }
                float elapsed=frames*dt;
                float expected=6f-(elapsed%6f);
                var timer=typeof(GameCore).GetField("stormTimer",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                float remaining=(float)timer.GetValue(g);
                Check(seen.Count==6,
                    "Deep abyss must schedule six actual rift warnings over forty seconds at "+dt);
                Check(Math.Abs(remaining-expected)<.02f,
                    "Deep abyss must conserve fractional six-second storm boundaries at "+dt);
            }
        }

        public static void RiftWarningRetainsItsFullVisibleDodgeWindow(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            Check(g.InAbyss,"Rift warning test must run in the real abyss");
            g.enemies.Clear();g.hazards.Clear();g.effects.Clear();
            g.save.abyssFloor=3;
            g.save.health=g.save.maxHealth=9999;
            g.save.reducedMotion=true;
            Hazard warning=null;
            for(int frame=0;frame<130 && warning==null;frame++)
            {
                g.Tick(.05f,new Point());
                warning=g.hazards.Find(h=>h.kind=="rift");
            }
            Check(warning!=null&&
                Math.Abs(warning.timer-1.4f)<.001f,
                "New abyss rift must give its full 1.4s visible warning after spawning, not immediately lose a frame");
            float health=g.save.health;
            for(int frame=0;frame<27;frame++)
                g.Tick(.05f,new Point());
            Check(g.hazards.Contains(warning)&&
                Math.Abs(g.save.health-health)<.001f,
                "A stationary hero must not take rift damage during the announced reaction window");
            g.Tick(.05f,new Point());g.Tick(.02f,new Point());
            Check(!g.hazards.Contains(warning)&&
                g.effects.Exists(e=>e.kind=="danger"&&
                    Math.Abs(e.radius-1.4f)<.001f)&&
                g.save.health<health,
                "The exact same real rift must finally release one dangerous contact after its warning expires");
        }

        public static void ThermalDangerFeedbackRequiresRealExposureAndDamage(Campaign data)
        {
            var g=Unlocked(data);g.Enter(6);g.enemies.Clear();
            g.save.x=14;g.save.y=10;g.save.teaUntil=0;
            g.save.heat=84.99f;g.save.cold=0;
            g.save.english=true;
            g.save.reducedMotion=true;
            g.save.health=g.save.maxHealth=1000;
            g.Tick(.05f,new Point());
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.text=="HEAT DANGER")&&
                g.toast.Contains("Heat danger"),
                "A first genuine desert overheat crossing needs visible and actionable English feedback");
            g.Tick(.05f,new Point());
            Check(g.effects.FindAll(e=>e.kind=="number"&&
                e.text=="HEAT DANGER").Count==1,
                "Sustained desert exposure cannot spam warnings every frame");
            g.save.heat=79.5f;g.Tick(.05f,new Point());
            g.save.heat=84.99f;g.Tick(.05f,new Point());
            Check(g.effects.FindAll(e=>e.kind=="number"&&
                e.text=="HEAT DANGER").Count==2,
                "Cooling below 80 and heating back to 85 must genuinely re-arm the single crossing alert");
            var timer=typeof(GameCore).GetField("climateTimer",
                BindingFlags.NonPublic|BindingFlags.Instance);
            timer.SetValue(g,.02f);g.invulnerable=0;
            float hp=g.save.health;
            g.Tick(.05f,new Point());
            Check(g.save.health<hp &&
                g.effects.FindAll(e=>e.kind=="number"&&
                    e.text=="BURN").Count==1,
                "Only actual desert HP damage makes the authored BURN contact label");
            hp=g.save.health;
            timer.SetValue(g,.02f);
            g.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health-hp)<.001f &&
                g.effects.FindAll(e=>e.kind=="number"&&
                    e.text=="BURN").Count==1,
                "I-frame protected climate pulses cannot show phantom second damage numbers");

            g.Enter(7);g.enemies.Clear();
            g.save.x=17;g.save.y=14;
            g.save.teaUntil=0;g.save.cold=89.99f;
            g.save.heat=0;g.invulnerable=0;
            g.Tick(.05f,new Point());
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.text=="FROST DANGER") &&
                g.toast.Contains("Freezing danger"),
                "A real Frostchime cold threshold needs biome-specific shelter guidance");
            timer.SetValue(g,.02f);g.invulnerable=0;
            hp=g.save.health;
            g.Tick(.05f,new Point());
            Check(g.save.health<hp &&
                g.effects.Exists(e=>e.kind=="number"&&
                    e.text=="FROST"),
                "Actual frozen-region HP loss displays frost injury, not the desert burn");
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
            g.Enter(1); Combat(g, false); Action(g, "scout", "scout"); TalkNear(g, "lore"); g.Close(); Gather(g);
            AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "shrine", "boss"); Combat(g, true); Action(g, "shrine", "goto0"); Action(g, "guild", "deliver");
            Action(g, "envoy", "exp_begin"); Check(g.save.room == 5, "Envoy starts the expansion after chapter one"); Gather(g); Action(g, "captain", "pledge" + pledge);
            for (int i = 0; i < 3; i++) { Action(g, "relay" + i, "relay" + i); Action(g, "castaway" + i, "rescue" + i); }
            Check(g.GateOpen, "Every harbor priority reaches the lighthouse");
            TalkNear(g, "manifest"); g.Close(); Action(g, "sluice", "holdtide"); Combat(g, false); g.save.x = 28; g.save.y = 10;
            AwaitArenaOpening(g); Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true); Action(g, "captain", "harbor_report"); Check(g.Has("companion_0") && g.Has("relic_0"), "Harbor report has permanent rewards");
            Action(g, "captain", "personal0"); Check(g.Has("personal_0"), "Mira's story uses the discovered manifest");
            g.Enter(6); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea();
            TalkNear(g, "mirror0"); TalkNear(g, "mirror1"); Check(!g.GateOpen, "A partial light route cannot open the gate");
            TalkNear(g, "mirror1"); TalkNear(g, "mirror2"); Check(g.GateOpen && g.LightRays().Count == 4, "A connected beam opens the glass gate");
            Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true); Action(g, "tinker", "desert" + (profession == 1 ? 2 : 1));
            Check(g.Has("companion_1") && g.CanEnter(7), "Desert decision recruits Ivo and opens the highlands"); Action(g, "tinker", "personal1"); Check(g.Has("personal_1"), "Ivo's story repairs a real machine");
            g.Enter(7); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea(); TalkNear(g, "bell0"); TalkNear(g, "bell0"); TalkNear(g, "bell2");
            Check(g.GateOpen, "The three tone settings open Frostchime"); Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true);
            Action(g, "keeper", "mountain" + (profession == 0 ? 1 : 2)); Action(g, "keeper", "personal2"); Check(g.Proofs == 3 && g.Has("personal_2") && g.Has("relic_4"), "Three reports and personal arcs complete");
            g.Enter(0); Check(g.BuildProject(0) && g.BuildProject(0) && g.BuildProject(1), "Recovered resources can fund the shared-rule requirements");
            Check(g.CouncilPactReady, "All factions and projects support shared governance");
            g.Enter(5); Action(g, "council", "covenant" + covenant); Check(g.save.frontierEnding == covenant && g.CanEnter(8), "Each covenant opens the abyss");
            return g;
        }
        private static void Gather(GameCore g)
        {
            foreach (WorldObject o in g.Map.objects)
                if (o.kind == "wood" || o.kind == "ore" || o.kind == "herb")
                    TalkNearObject(g, o);
        }
        public static void DistantCampaign(Campaign data)
        { for (int i = 0; i < 3; i++) ShoreRun(data, i, i + 1, i + 1); }

        public static void ExpandedRootAbyssHasRealDetoursAndUnbankedRewards(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.frontierEnding=1;
            g.StartAbyss(0);
            Check(g.InAbyss&&g.Map.rows.Length==44&&g.Map.rows[0].Length==66,
                "Every newly generated abyss floor has expanded playable galleries");
            WorldObject cache=null,survey=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.kind=="abyssCache")cache=o;
                if(o.kind=="abyssSurvey")survey=o;
            }
            Check(cache!=null&&survey!=null&&g.CanStand(new Point(cache.x,cache.y))&&
                g.CanStand(new Point(survey.x,survey.y)),
                "Both real annex points are reachable standable world objects");
            int startCoins=g.save.coins,startShards=g.save.shards;
            g.save.x=survey.x;g.save.y=survey.y;
            int startRunXp=g.save.runXp;g.Talk(survey);
            Check(g.save.runXp==startRunXp+15,"Survey awards unbanked experience only");
            g.Close();g.Talk(survey);g.Close();
            Check(g.save.runXp==startRunXp+15,"Abyss survey cannot mint XP twice");
            Check(!g.Explored(8,cache.x,cache.y),
                "Procedural annex begins unknown before visiting the side corridor");
            g.save.x=cache.x;g.save.y=cache.y;
            g.Tick(.02f,new Point());
            Check(g.Explored(8,cache.x,cache.y)&&!g.Explored(8,12,28),
                "Abyss fog tracks actual distant rooms separately");
            int runCoins=g.save.runCoins,runShards=g.save.runShards;
            g.Talk(cache);
            Check(g.save.runCoins==runCoins+18&&g.save.runShards==runShards+2&&
                g.save.coins==startCoins&&g.save.shards==startShards,
                "New cache follows the actual expedition banking rules");
            g.Close();
            GameCore reloaded=new GameCore(data,g.save);
            Check(reloaded.InAbyss&&reloaded.save.version==2&&
                reloaded.Map.rows.Length==44&&reloaded.Map.rows[0].Length==66&&
                reloaded.Has("abyss_annex_cache_1")&&
                reloaded.Explored(8,cache.x,cache.y)&&reloaded.save.runCoins==g.save.runCoins,
                "Legacy v2 mid-expedition save restores expanded geometry and pending loot");
            reloaded.StartAbyss(1);
            Check(reloaded.save.runCoins==g.save.runCoins,
                "Cannot restart another expedition while already in the abyss");
        }

        public static void ExpandedLowerAbyssGalleriesHaveRealBandedLootAndSafeV2Reload(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.frontierEnding=1;g.StartAbyss(0);
            Check(g.InAbyss&&g.Map.rows.Length==44&&g.Map.rows[0].Length==66,
                "Procedural abyss expands to 66x44 without moving its original boss route");
            var cells=Reachable(g);int width=g.Map.rows[0].Length;
            WorldObject survey=Array.Find(g.Map.objects,o=>o.kind=="abyssFarSurvey");
            WorldObject cache=Array.Find(g.Map.objects,o=>o.kind=="abyssFarCache");
            WorldObject echo=Array.Find(g.Map.objects,o=>o.kind=="abyssFarEcho");
            Check(survey!=null&&cache!=null&&echo!=null,
                "Abyss has three actual new optional lower-gallery interactions");
            foreach(WorldObject point in new[]{survey,cache,echo})
                Check(cells.Contains((int)point.y*width+(int)point.x)&&
                    g.CanStand(new Point(point.x,point.y)),
                    "Real abyss collisions allow the hero to reach every new gallery");
            int coins=g.save.coins,shards=g.save.shards,oldXP=g.save.runXp;
            g.save.x=echo.x;g.save.y=echo.y;g.Talk(echo);g.Close();
            Check(g.save.runXp==oldXP&&
                !g.Has("abyss_annex_far_echo_1"),
                "Echo cannot be opened without actually finding lower testimony");
            g.save.x=survey.x;g.save.y=survey.y;g.Talk(survey);g.Close();
            Check(g.save.runXp==oldXP+25&&
                g.Has("abyss_annex_far_survey_1"),"Physical testimony grants pending XP");
            g.Talk(survey);g.Close();
            Check(g.save.runXp==oldXP+25,"Reading the same tablet never duplicates loot");
            g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);g.Close();
            Check(g.save.runCoins>=30&&g.save.runShards>=1&&
                g.save.coins==coins&&g.save.shards==shards,
                "Lower gallery supplies follow existing unbanked expedition rules");
            g.save.x=echo.x;g.save.y=echo.y;g.Talk(echo);g.Close();
            Check(g.save.runXp==oldXP+45,"Echo grants pending XP only after prior physical survey");
            g.PrepareSave();
            GameCore restored=new GameCore(data,g.save);
            Check(restored.InAbyss&&restored.save.version==2&&
                restored.Map.rows.Length==44&&restored.Map.rows[0].Length==66&&
                restored.Has("abyss_annex_far_survey_1")&&
                restored.Has("abyss_annex_far_echo_1")&&
                restored.save.runXp==oldXP+45,
                "Mid-floor old-format save restores new gallery geometry and pending loot");
        }

        public static void AbyssWardExtractionAndReentryCannotMintCharter(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            g.save.abyssFloor = 6; regenerate.Invoke(g, null);
            for (int floor = 3; floor <= 6; floor++)
                g.save.flags.Add("abyss_annex_ward_" + floor);
            g.enemies.Clear();
            g.save.runShards = 9;
            g.save.x = 3; g.save.y = 14;
            WorldObject exit = Array.Find(g.Map.objects, o => o.kind == "abyssExit");
            int skills = g.save.skillPoints, shards = g.save.shards;
            g.Talk(exit);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 2,
                "Early-extraction fixture opens the real leave-or-stay dialogue");
            g.Choose(0);
            Check(!g.InAbyss && !g.Has("abyss_ward_charter_complete") &&
                g.save.skillPoints == skills && g.save.shards == shards &&
                g.save.runShards == 0,
                "Leaving a perfect-but-UNBANKED sixth floor forfeits its charter reward");

            var restored = Unlocked(data); restored.StartAbyss(1);
            restored.save.abyssFloor = 3;
            regenerate.Invoke(restored, null);
            WorldObject ward = Array.Find(restored.Map.objects,
                o => o.kind == "abyssRiftWard");
            restored.enemies.Clear(); restored.save.x = ward.x; restored.save.y = ward.y;
            restored.save.runShards = 3;
            restored.Talk(ward); restored.Choose(0);
            Check(restored.AbyssWardActive, "The physical ward was purchased before saving");
            // Clearing the floor preserves legitimate unbanked state when
            // reconstructing this v2 snapshot without an enemy roster.
            restored.save.flags.Add("abyss_clear_3");
            restored.PrepareSave();
            var reloaded = new GameCore(data, CloneSave(restored.save));
            var field = typeof(GameCore).GetField("stormTimer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(reloaded.AbyssWardActive &&
                Math.Abs((float)field.GetValue(reloaded) - 10f) < .001f,
                "Reloaded ward initializes its own TEN-second clock, not an unwarded six");
            // A newly constructed core still owns the title screen. The
            // loaded scene enters world mode before its simulation ticks.
            reloaded.mode = "world";
            reloaded.hazards.Clear(); reloaded.effects.Clear();
            reloaded.save.flags.Remove("abyss_clear_3");
            reloaded.save.health = reloaded.save.maxHealth = 9999;
            reloaded.save.reducedMotion = true;
            for (int i = 0; i < 130; i++) reloaded.Tick(.05f, new Point());
            Check(reloaded.hazards.Find(h => h.kind == "rift") == null,
                "No too-early rift is generated after the loaded six-second mark");
            for (int i = 0; i < 80; i++) reloaded.Tick(.05f, new Point());
            Check(reloaded.hazards.Find(h => h.kind == "rift") != null,
                "The ward resumes real rift spawning after ten seconds on reload");

            var gate = Unlocked(data); gate.StartAbyss(0);
            gate.save.abyssFloor = 6; regenerate.Invoke(gate, null);
            gate.enemies.Clear(); gate.save.x = 31; gate.save.y = 14;
            WorldObject finalGate = Array.Find(gate.Map.objects,
                o => o.kind == "abyssGate");
            gate.Talk(finalGate);
            Check(gate.mode == "dialog" &&
                gate.conversation.text.Contains("\n\n镇流巡路") &&
                gate.conversation.textEn.Contains("\n\nWard charter"),
                "Ward charter progress renders as real separate paragraphs in both languages");
        }

        public static void AbyssFourFloorWardCharterPaysOnlyOnLegitimateFinalBank(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            // Model a replaying adventurer: isolate the NEW charter payout
            // from the older first-time six-floor completion award.
            g.save.flags.Add("abyss_complete");
            g.save.abyssFloor = 3;
            regenerate.Invoke(g, null);
            int beforeSkills = g.save.skillPoints, beforeShards = g.save.shards;
            for (int floor = 3; floor <= 6; floor++)
            {
                Check(g.InAbyss && g.save.abyssFloor == floor,
                    "Each charter leg begins in the actual next abyss floor");
                var ward = Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard");
                Check(ward != null, "A current-floor pylon is available");
                g.enemies.Clear(); g.hazards.Clear();
                g.save.x = ward.x; g.save.y = ward.y;
                g.save.runShards = 3;
                g.Talk(ward); g.Choose(0);
                Check(g.AbyssWardActive && g.AbyssWardCharterProgress == floor - 2,
                    "A physical paid tune records one additional unique floor");
                g.save.flags.Add("abyss_clear_" + floor);
                if (floor < 6)
                {
                    g.TakeBoon(0); // The real descent commits each bank.
                    Check(!g.Has("abyss_ward_charter_complete") &&
                        g.save.skillPoints == beforeSkills,
                        "Early-floor banking never grants the permanent final reward");
                }
                else
                {
                    Check(!g.Has("abyss_ward_charter_complete") &&
                        g.save.skillPoints == beforeSkills,
                        "Four ward activations alone cannot bypass the floor-six bank");
                    g.save.x = 31; g.save.y = 14;
                    WorldObject gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
                    g.Talk(gate);
                    Check(g.mode == "dialog" &&
                        g.conversation.choices[g.conversation.choices.Length-1].action ==
                            "abyssBank",
                        "The sixth-floor gate keeps the final banking choice available");
                    g.Choose(g.conversation.choices.Length-1);
                }
            }
            Check(g.save.room == 0 && g.Has("abyss_ward_charter_complete") &&
                g.save.skillPoints == beforeSkills + 1 &&
                g.save.shards == beforeShards + 8,
                "The last-floor settlement permanently banks the one-time charter award");
            g.StartAbyss(0);
            Check(g.InAbyss && g.AbyssWardCharterProgress == 0 &&
                g.Has("abyss_ward_charter_complete"),
                "A new expedition resets temporary pylon charges but retains the permanent award");
            g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            for (int floor = 3; floor <= 6; floor++)
                g.save.flags.Add("abyss_annex_ward_" + floor);
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_6");
            g.save.x = 31; g.save.y = 14;
            int awardedSkills = g.save.skillPoints, awardedShards = g.save.shards;
            WorldObject repeatGate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.Talk(repeatGate); g.Choose(0);
            Check(g.save.skillPoints == awardedSkills && g.save.shards == awardedShards,
                "Replaying even a perfect charter cannot mint a second permanent reward");

            var incomplete = Unlocked(data); incomplete.StartAbyss(0);
            incomplete.save.flags.Add("abyss_complete");
            incomplete.save.abyssFloor = 6;
            regenerate.Invoke(incomplete, null);
            for (int floor = 3; floor <= 5; floor++)
                incomplete.save.flags.Add("abyss_annex_ward_" + floor);
            incomplete.enemies.Clear(); incomplete.save.flags.Add("abyss_clear_6");
            incomplete.save.x = 31; incomplete.save.y = 14;
            int skills = incomplete.save.skillPoints;
            WorldObject lastGate = Array.Find(incomplete.Map.objects, o => o.kind == "abyssGate");
            incomplete.Talk(lastGate); incomplete.Choose(0);
            Check(!incomplete.Has("abyss_ward_charter_complete") &&
                incomplete.save.skillPoints == skills,
                "A three-of-four circuit cannot claim the charter by extracting from six");
        }

        public static void AbyssRiftWardRequiresExpeditionLootAndActuallyChangesStorms(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            Check(Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard") == null,
                "Early abyss floors do not display a useless rift pylon");
            g.save.abyssFloor = 3;
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            regenerate.Invoke(g, null);
            WorldObject ward = Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard");
            Check(ward != null && g.CanStand(new Point(ward.x, ward.y)) &&
                Reachable(g).Contains((int)ward.y * g.Map.rows[0].Length + (int)ward.x),
                "The ward is a real reachable side-route object on floor three");
            g.enemies.Clear(); g.hazards.Clear(); g.effects.Clear();
            g.save.runShards = 3; g.save.shards = 37;
            int priorRevision = g.revision;
            g.Talk(ward);
            Check(g.mode == "world" && !g.AbyssWardActive,
                "A distant interaction cannot remotely activate the ward");
            g.save.x = ward.x; g.save.y = ward.y;
            g.Talk(ward);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 2,
                "The pylon offers a paid defensive option and a decline");
            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.runShards == 3 && !g.AbyssWardActive,
                "A stale choice cannot activate the pylon after moving away");

            g.save.x = ward.x; g.save.y = ward.y;
            g.Talk(ward); g.Choose(0);
            Check(g.AbyssWardActive && g.save.runShards == 0 &&
                g.save.shards == 37 && g.revision > priorRevision &&
                g.Conditions().Contains("镇流生效"),
                "Tuning spends exactly three unbanked shards and updates the HUD");
            g.Talk(ward);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 1,
                "The floor's existing pylon cannot be bought again");
            g.Close();
            g.save.health = g.save.maxHealth = 9999; g.save.reducedMotion = true;
            var seen = new HashSet<Hazard>();
            for (int n = 0; n < 790; n++)
            {
                g.Tick(.05f, new Point());
                foreach (Hazard h in g.hazards)
                    if (h.kind == "rift") seen.Add(h);
            }
            Check(seen.Count == 3,
                "Ward slows live floor rift births to ten-second intervals");
            foreach (Hazard h in seen)
                Check(Math.Abs(h.damage - 6f) < .01f &&
                    Math.Abs(h.radius - 1.05f) < .01f,
                    "Ward reduces real warning damage and collision radius");
            g.save.flags.Add("abyss_clear_3");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssWardActive &&
                restored.save.runShards == 0 &&
                restored.Conditions().Contains("镇流生效"),
                "The defensive decision survives SaveData v2 reconstruction");

            var noLoot = Unlocked(data); noLoot.StartAbyss(2);
            noLoot.save.abyssFloor = 3;
            regenerate.Invoke(noLoot, null);
            WorldObject unpaid = Array.Find(noLoot.Map.objects,
                o => o.kind == "abyssRiftWard");
            noLoot.save.x = unpaid.x; noLoot.save.y = unpaid.y;
            noLoot.save.runShards = 0; noLoot.save.shards = 200;
            noLoot.Talk(unpaid); noLoot.Choose(0);
            Check(!noLoot.AbyssWardActive && noLoot.save.shards == 200,
                "Pylon cannot spend already banked shards or activate for free");
        }

        public static void AbyssWardPylonIsSeededAndTraversableOnAllHazardFloors(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int floor = 3; floor <= 6; floor++)
            for (int seed = 100; seed < 103; seed++)
            {
                g.save.abyssFloor = floor;
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                WorldObject ward = Array.Find(g.Map.objects,
                    o => o.kind == "abyssRiftWard");
                Check(ward != null && g.CanStand(new Point(ward.x, ward.y)) &&
                    Reachable(g).Contains((int)ward.y * g.Map.rows[0].Length + (int)ward.x),
                    "Every late-floor seeded pylon has a physically traversable location");
                var recreated = new GameCore(data, CloneSave(g.save));
                WorldObject replay = Array.Find(recreated.Map.objects,
                    o => o.kind == "abyssRiftWard");
                Check(replay != null && replay.x == ward.x && replay.y == ward.y,
                    "SaveData v2 recreates exact ward positions on each deep floor");
            }
            g.save.abyssSeed = int.MaxValue; g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            WorldObject maxSeed = Array.Find(g.Map.objects,
                o => o.kind == "abyssRiftWard");
            Check(maxSeed != null && g.CanStand(new Point(maxSeed.x, maxSeed.y)),
                "The maximum legal v2 seed does not overflow the ward site index");
        }

        public static void AbyssRootwellSeededRoutesStayReachableAndSaveStable(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(regenerate != null, "Original procedural Abyss generator remains installed");
            var positions = new HashSet<string>();
            for (int seed = 100; seed < 103; seed++)
            {
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                WorldObject well = Array.Find(g.Map.objects,
                    o => o.kind == "abyssRootwell");
                Check(well != null && g.CanStand(new Point(well.x, well.y)),
                    "Every seeded recovery site is physically standable");
                int width = g.Map.rows[0].Length;
                Check(Reachable(g).Contains((int)well.y * width + (int)well.x),
                    "Every seeded recovery site is walkably connected to the entry corridor");
                positions.Add(well.x + "," + well.y);
                GameCore reloaded = new GameCore(data, CloneSave(g.save));
                WorldObject restored = Array.Find(reloaded.Map.objects,
                    o => o.kind == "abyssRootwell");
                Check(restored != null &&
                    restored.x == well.x && restored.y == well.y &&
                    reloaded.save.abyssSeed == seed,
                    "SaveData v2 regenerates the SAME rootwell site without changing the seed");
            }
            Check(positions.Count == 3,
                "Three successive deterministic seeds place the rootwell in distinct reachable branches");
            // The v2 sanitizer permits the full positive signed seed range.
            // An overflowed negative remainder must never produce a bogus
            // rootwell index or break a deep legacy expedition.
            g.save.abyssSeed = int.MaxValue;
            g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            WorldObject extreme = Array.Find(g.Map.objects, o => o.kind == "abyssRootwell");
            Check(extreme != null && g.CanStand(new Point(extreme.x, extreme.y)) &&
                Reachable(g).Contains((int)extreme.y * g.Map.rows[0].Length + (int)extreme.x),
                "Maximum legal v2 seed still places a reachable final-floor rootwell");
            GameCore extremeReload = new GameCore(data, CloneSave(g.save));
            WorldObject extremeRestored = Array.Find(extremeReload.Map.objects,
                o => o.kind == "abyssRootwell");
            Check(extremeRestored != null && extremeRestored.x == extreme.x &&
                extremeRestored.y == extreme.y,
                "The maximum legal seed and sixth-floor rootwell survive SaveData v2 reload");
        }

        public static void RoadwardenFieldRecordMarkersRespectRealProofAndMastery(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            WorldObject tablet=Array.Find(g.Map.objects,
                o=>o.kind=="abyssSurvey");
            WorldObject southern=Array.Find(g.Map.objects,
                o=>o.kind=="abyssFarSurvey");
            WorldObject echo=Array.Find(g.Map.objects,
                o=>o.kind=="abyssFarEcho");
            WorldObject exit=Array.Find(g.Map.objects,
                o=>o.kind=="abyssExit");
            Check(g.AbyssTestimonyRecordState(tablet)==1 &&
                g.AbyssTestimonyRecordState(echo)==3 &&
                g.AbyssTestimonyRecordState(exit)==0,
                "World marker state distinguishes unvisited proof, locked echo and unrelated exit");
            g.save.x=echo.x;g.save.y=echo.y;
            g.Talk(echo);g.Close();
            Check(g.AbyssTestimonyRecordState(echo)==3,
                "Premature echo interaction cannot falsely display a collected marker");
            g.save.x=southern.x;g.save.y=southern.y;
            g.Talk(southern);g.Close();
            Check(g.AbyssTestimonyRecordState(southern)==2 &&
                g.AbyssTestimonyRecordState(echo)==1,
                "Reading the real southern tablet marks it complete and enables the echo");
            g.save.x=echo.x;g.save.y=echo.y;
            g.Talk(echo);g.Close();
            Check(g.AbyssTestimonyRecordState(echo)==2,
                "The echo's world marker changes only after its actual story interaction");
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            WorldObject restoredEcho=Array.Find(restored.Map.objects,
                o=>o.kind=="abyssFarEcho");
            Check(restored.AbyssTestimonyRecordState(restoredEcho)==2,
                "The visual record state survives SaveData v2 reload without new fields");
            restored.save.flags.Add("abyss_testimony_codex_complete");
            Check(restored.AbyssTestimonyLegacyComplete &&
                restored.AbyssTestimonyRecordState(
                    Array.Find(restored.Map.objects,o=>o.kind=="abyssSurvey"))==1,
                "Archive mastery reveals real outstanding notes but never auto-collects them");
            restored.save.room=0;
            Check(restored.AbyssTestimonyRecordState(restoredEcho)==0,
                "No Abyss proof highlight leaks into unrelated overworld scenes");
        }

        public static void CompletedArchiveExtendsRealRiftWindupsWithoutChangingDamage(Campaign data)
        {
            var ordinary=Unlocked(data);ordinary.StartAbyss(2);
            var archivist=Unlocked(data);
            archivist.save.flags.Add("abyss_testimony_codex_complete");
            archivist.StartAbyss(2);
            var generate=typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(GameCore g in new[]{ordinary,archivist})
            {
                g.save.abyssFloor=3;
                g.save.abyssSeed=137;
                generate.Invoke(g,null);
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                g.save.x=5;g.save.y=14;
                g.save.maxHealth=g.save.health=9999;
                g.save.reducedMotion=true;
            }
            Check(!ordinary.AbyssArchiveForesight &&
                archivist.AbyssArchiveForesight &&
                archivist.Objective().Contains("巡路预知"),
                "Only the permanently completed archive unlocks its visible Rift foresight");
            for(int i=0;i<120;i++)
            {
                ordinary.Tick(.05f,new Point());
                archivist.Tick(.05f,new Point());
            }
            Hazard a=ordinary.hazards.Find(h=>h.kind=="rift");
            Hazard b=archivist.hazards.Find(h=>h.kind=="rift");
            Check(a!=null&&b!=null&&
                Math.Abs((b.timer-a.timer)-.4f)<.01f &&
                Math.Abs(b.damage-a.damage)<.01f &&
                Math.Abs(b.radius-a.radius)<.01f,
                "Actual six-second spawned rifts gain .4s of warning, not reduced damage or area");
            float life=ordinary.save.health, protectedLife=archivist.save.health;
            for(int i=0;i<29;i++)
            {
                ordinary.Tick(.05f,new Point());
                archivist.Tick(.05f,new Point());
            }
            Check(ordinary.save.health<life &&
                archivist.save.health==protectedLife,
                "An unarchived hero suffers the same real Rift before the archivist's longer warning ends");
            for(int i=0;i<10;i++)archivist.Tick(.05f,new Point());
            Check(archivist.save.health<protectedLife,
                "Foresight delays but never erases an undodged Rift contact");
            archivist.save.flags.Remove("abyss_testimony_codex_complete");
            Check(!archivist.AbyssArchiveForesight,
                "Without a permanent proof there is no free Rift warning buff");

            var early=Unlocked(data);early.StartAbyss(2);
            early.save.flags.Add("abyss_testimony_codex_complete");
            early.save.abyssFloor=3;
            generate.Invoke(early,null);
            early.enemies.Clear();early.hazards.Clear();
            int expected=early.AbyssCircuitNextNode;
            WorldObject wrong=Array.Find(early.Map.objects,
                o=>o.kind=="abyssCircuit"+((expected+1)%3));
            early.save.x=wrong.x;early.save.y=wrong.y;
            early.Talk(wrong);
            Hazard backlash=early.hazards.Find(h=>h.kind=="rift");
            Check(backlash!=null&&Math.Abs(backlash.timer-2f)<.001f&&
                Math.Abs(backlash.damage-18f)<.001f,
                "The same permanent foresight also adds .4s to live miswire backlash warnings");

            early.save.flags.Add("abyss_clear_3");
            early.PrepareSave();
            var reload=new GameCore(data,CloneSave(early.save));
            Check(reload.InAbyss&&reload.AbyssArchiveForesight,
                "The unlocked combat advantage survives the existing SaveData v2 round trip");
            var city=Unlocked(data);
            city.save.flags.Add("abyss_testimony_codex_complete");
            Check(!city.AbyssArchiveForesight,
                "The passive cannot affect ordinary surface hazards outside the Abyss");
        }

        public static void SixFloorRoadwardenArchiveBanksOnceAfterFullPhysicalExpedition(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.save.flags.Add("abyss_complete"); // Isolate the new reward.
            var sites = new [] {
                "abyssSurvey","abyssCache","abyssFarCache",
                "abyssFarSurvey","abyssFarEcho"
            };
            for (int floor = 1; floor <= 6; floor++)
            {
                Check(g.InAbyss && g.save.abyssFloor == floor &&
                    g.AbyssTestimonyFloorsFiled == floor-1,
                    "Each trial leg enters the correct REAL seeded abyss floor");
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                foreach (string kind in sites)
                {
                    WorldObject obj = Array.Find(g.Map.objects, o => o.kind == kind);
                    Check(obj != null && g.CanStand(new Point(obj.x,obj.y)),
                        "The " + kind + " proof is physically present on floor " + floor);
                    g.save.x=obj.x;g.save.y=obj.y;
                    g.Talk(obj);g.Close();
                }
                Check(g.AbyssTestimonySealed &&
                    g.AbyssTestimonyFloorsFiled == floor,
                    "Five real site visits complete the current floor's separate ledger");
                g.save.flags.Add("abyss_clear_" + floor);
                if (floor < 6)
                {
                    g.TakeBoon(0);
                    // Every bank awards real XP, which may level up the hero
                    // and legitimately grant unrelated skill points. Check
                    // the dedicated permanent milestone, not total points.
                    Check(!g.AbyssTestimonyLegacyComplete,
                        "Intermediate floor banking cannot grant the permanent six-floor award");
                }
                else
                {
                    Check(!g.AbyssTestimonyLegacyComplete,
                        "Six sets of recovered notes alone cannot bypass final gate settlement");
                    g.save.x=30.5f;g.save.y=14.5f;
                    WorldObject gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
                    g.Talk(gate);
                    Check(g.mode=="dialog" &&
                        g.conversation.text.Contains("六层证词归档 6/6"),
                        "Real last-floor gate advertises fully earned archival milestone");
                    int oldShards=g.save.shards, unbanked=g.save.runShards;
                    int oldSkills = g.save.skillPoints;
                    g.Choose(g.conversation.choices.Length-1);
                    Check(g.save.room==0 &&
                        g.AbyssTestimonyLegacyComplete &&
                        g.save.skillPoints>=oldSkills+2 &&
                        g.save.shards>=oldShards+unbanked+12,
                        "Only legal final banking converts the full expedition into permanent rewards");
                }
            }
            var loaded = new GameCore(data,CloneSave(g.save));
            Check(loaded.AbyssTestimonyLegacyComplete &&
                !loaded.InAbyss,
                "The one-time permanent milestone survives ordinary SaveData v2 reload");
            loaded.StartAbyss(0);
            Check(loaded.AbyssTestimonyLegacyComplete &&
                loaded.AbyssTestimonyFloorsFiled==0,
                "A second expedition clears per-floor proofs but preserves permanent archive");
            loaded.save.abyssFloor=6;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loaded,null);
            for(int floor=1;floor<=6;floor++)
                loaded.save.flags.Add("abyss_annex_testimony_complete_"+floor);
            loaded.enemies.Clear();loaded.hazards.Clear();loaded.projectiles.Clear();
            loaded.save.flags.Add("abyss_clear_6");
            loaded.save.x=30.5f;loaded.save.y=14.5f;
            int earned=loaded.save.skillPoints;
            WorldObject secondGate=Array.Find(loaded.Map.objects,
                o=>o.kind=="abyssGate");
            loaded.Talk(secondGate);
            loaded.Choose(loaded.conversation.choices.Length-1);
            Check(loaded.save.skillPoints==earned &&
                loaded.AbyssTestimonyLegacyComplete,
                "Even a perfect replay cannot claim permanent codex skill points twice");

            var partial=Unlocked(data);partial.StartAbyss(0);
            partial.save.flags.Add("abyss_complete");
            for(int floor=1;floor<=5;floor++)
                partial.save.flags.Add("abyss_annex_testimony_complete_"+floor);
            partial.save.abyssFloor=6;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(partial,null);
            partial.enemies.Clear();partial.save.flags.Add("abyss_clear_6");
            partial.save.x=30.5f;partial.save.y=14.5f;
            int previous=partial.save.skillPoints;
            WorldObject partialGate=Array.Find(partial.Map.objects,
                o=>o.kind=="abyssGate");
            partial.Talk(partialGate);
            partial.Choose(partial.conversation.choices.Length-1);
            Check(!partial.AbyssTestimonyLegacyComplete &&
                partial.save.skillPoints==previous,
                "Five completed floors and an unfinished sixth cannot claim the permanent archive");
        }




        public static void AbyssSixChoiceGateKeepsBankAndCircuitTransfersAfterSurvey(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runCoins=24;g.save.runShards=7;
            int coins=g.save.coins,shards=g.save.shards;
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            var relay=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit1");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            Check(g.conversation.choices.Length==6 &&
                g.conversation.choices[3].action=="abyssTestimonyTrack" &&
                g.conversation.choices[4].action=="abyssCircuitRoutes" &&
                g.conversation.choices[5].action=="abyssBank",
                "Real cleared-floor dialog exposes six distinct safe actions: blessings, testimony, routes and bank");
            g.Choose(3);
            Check(g.AbyssTestimonyGuidedSite!=null && g.save.shards==shards,
                "Choice four deliberately starts fieldwork without settlement");
            g.Talk(gate);
            g.Choose(4);
            Check(g.mode=="dialog" &&
                g.conversation.choices[1].action=="abyssCircuitTo1",
                "Six-choice gate still opens the original physical circuit network");
            g.Choose(1);
            Check(Math.Abs(g.save.x-relay.x)<.01f &&
                Math.Abs(g.save.y-relay.y)<.01f &&
                g.AbyssTestimonyGuidedSite==null &&
                g.save.runShards==7 && g.save.shards==shards,
                "Explicit relay travel supersedes testimony intent without awarding loot");
            g.Talk(relay);
            g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f,
                "Return via the real relay circuit reaches the unchanged east gate");
            g.Talk(gate);
            g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room==0 && g.save.coins==coins+24 &&
                g.save.shards==shards+7,
                "After both optional routes the original final choice is the only bank");
        }

        public static void AbyssGateTestimonyTrackingCanBeCancelledAndReenabled(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            int start = Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack");
            int initialCoins=g.save.coins, initialShards=g.save.shards;
            g.Choose(start);
            var tracked=g.AbyssTestimonyGuidedSite;
            Check(tracked!=null,
                "Safe gate explicitly activates a real recoverable field record");
            g.Talk(gate);
            int stop = Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyUntrack");
            Check(stop>=0 &&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="abyssTestimonyTrack")<0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssBank",
                "Live gate offers one toggle rather than hiding the original bank");
            // Source-distance validation also protects deliberate cancellation.
            g.save.x=5;g.save.y=14;g.Choose(stop);
            Check(g.AbyssTestimonyGuidedSite==tracked,
                "Stale distant menu cannot cancel the current field route");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            stop=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyUntrack");
            g.Choose(stop);
            Check(g.AbyssTestimonyGuidedSite==null &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f &&
                g.save.room==8 && g.save.coins==initialCoins &&
                g.save.shards==initialShards && g.AbyssTestimonyProgress==0,
                "Stopping guidance restores the original exit target without rewards");
            g.Talk(gate);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack")>=0,
                "After cancelling, the same physical gate permits a fresh survey");
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack"));
            Check(g.AbyssTestimonyGuidedSite==tracked,
                "Player can resume the same unfinished record without resetting proof flags");
        }

        public static void AbyssGateTestimonyTrackingLeadsThroughRealSitesWithoutBanking(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            Check(gate != null && g.AbyssTestimonyProgress == 0 &&
                g.AbyssTestimonyNextSite != null,
                "A fresh floor contains the five genuine field records");
            g.save.x = gate.x; g.save.y = gate.y;
            g.Talk(gate);
            int choice = Array.FindIndex(g.conversation.choices,
                c => c.action == "abyssTestimonyTrack");
            Check(choice >= 0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "East gate offers explicit exploration before its original banking option");
            var first = g.AbyssTestimonyNextSite;
            var actualStart = g.NavigationTarget().Value;
            int banked = g.save.shards, pending = g.save.runShards;
            g.Choose(choice);
            Check(g.AbyssTestimonyGuidedSite == first &&
                g.Objective().Contains(first.name) &&
                (g.NavigationTarget().Value - new Point(first.x,first.y)).Length<.001f &&
                (actualStart-g.NavigationTarget().Value).Length>1f &&
                g.save.shards==banked && g.save.runShards==pending &&
                g.save.room==8,
                "Explicit gate choice changes only live objective, never location or loot");

            // Follow the actual physical five-record chain; the echo unlock
            // remains governed by its southern source testimony.
            for (int i=0;i<5;i++)
            {
                WorldObject target=g.AbyssTestimonyGuidedSite;
                Check(target!=null && g.AbyssTestimonyRecordState(target)==1 &&
                    (g.NavigationTarget().Value-new Point(target.x,target.y)).Length<.001f,
                    "Compass remains pinned to the valid currently recoverable object");
                g.save.x=target.x;g.save.y=target.y;
                g.Talk(target);
                Check(g.Has("abyss_clear_1") && g.AbyssTestimonyProgress==i+1,
                    "Physical testimony interaction advances one real SaveData v2 proof");
                g.Close();
                Check(!System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,target),
                    "A recorded field site immediately leaves the compass");
            }
            Check(g.AbyssTestimonySealed && g.AbyssTestimonyGuidedSite==null &&
                !g.Objective().Contains("正在追踪巡路证词") &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f &&
                g.save.shards==banked && g.save.room==8,
                "Complete five-site investigation returns navigation to real banking gate");

            g.PrepareSave();
            var restored = new GameCore(data,CloneSave(g.save));
            Check(restored.AbyssTestimonySealed &&
                restored.AbyssTestimonyGuidedSite==null &&
                (restored.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Existing save format remembers proofs, not temporary compass intent");
        }

        public static void AbyssTestimonyTrackingRejectsStaleGateAndResetsOnFloorChange(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack");
            g.save.x=5;g.save.y=14;g.Choose(index);
            Check(g.AbyssTestimonyGuidedSite==null &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Walking away before menu confirmation cannot activate remote tracking");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack"));
            Check(g.AbyssTestimonyGuidedSite!=null,
                "Tracking legitimately activates at a physically reachable gate");
            g.save.abyssFloor=2;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
            Check(g.AbyssTestimonyGuidedSite==null &&
                g.save.abyssFloor==2 &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Floor transition cannot reuse a stale physical testimony or marker");
        }

        public static void AbyssTestimonyCompassFollowsRealUnvisitedSources(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.save.x=5;g.save.y=14;
            WorldObject near = g.AbyssTestimonyNextSite;
            Check(near != null && near.kind == "abyssSurvey" &&
                g.Objective().Contains(near.name),
                "The actual closest unsolved record appears in the mission guidance");
            WorldObject farEcho = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarEcho");
            g.save.x = farEcho.x;g.save.y = farEcho.y;
            Check(g.AbyssTestimonyNextSite != farEcho,
                "An inaccessible echo transcript is never suggested before finding its source");
            g.save.flags.Add("abyss_annex_far_survey_1");
            Check(g.AbyssTestimonyNextSite == farEcho,
                "Decoding the source tablet unlocks the real echo as a compass candidate");
            g.save.flags.Add("abyss_annex_far_echo_1");
            Check(g.AbyssTestimonyNextSite != farEcho,
                "A collected echo is immediately removed from compass targets");
            g.save.flags.Add("abyss_annex_survey_1");
            g.save.flags.Add("abyss_annex_cache_1");
            g.save.flags.Add("abyss_annex_far_cache_1");
            Check(g.AbyssTestimonyProgress == 5,
                "The compass reads all five existing v2 proofs");
            g.save.flags.Add("abyss_annex_testimony_complete_1");
            Check(g.AbyssTestimonyNextSite == null,
                "Completed testimony trail never points to a nonexistent bonus target");
            g.enemies.Clear();g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.AbyssTestimonyNextSite == null &&
                restored.AbyssTestimonySealed,
                "SaveData v2 reload preserves completed compass targeting");
            restored.save.abyssFloor=2;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(restored,null);
            Check(restored.AbyssTestimonyNextSite != null &&
                restored.AbyssTestimonyProgress == 0,
                "A new seeded floor regenerates useful next-site navigation");
        }

        public static void AbyssTestimonyTrailRequiresFivePhysicalRecordsAndBanksOnlyAtGate(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            string[] kinds = {
                "abyssSurvey", "abyssCache", "abyssFarCache",
                "abyssFarSurvey", "abyssFarEcho"
            };
            WorldObject[] sites = new WorldObject[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                sites[i] = Array.Find(g.Map.objects, o => o.kind == kinds[i]);
                Check(sites[i] != null &&
                    g.CanStand(new Point(sites[i].x, sites[i].y)),
                    "Roadwarden record " + i + " is a physical walkable gallery object");
            }
            int coins = g.save.coins, shards = g.save.shards;
            g.save.x = sites[4].x;g.save.y = sites[4].y;
            g.Talk(sites[4]);g.Close();
            Check(g.AbyssTestimonyProgress == 0 && !g.AbyssTestimonySealed,
                "The echo stone refuses to decode before finding original southern testimony");

            for (int i = 0; i < kinds.Length; i++)
            {
                g.save.x = sites[i].x;g.save.y = sites[i].y;
                g.Talk(sites[i]);
                Check(g.AbyssTestimonyProgress == i + 1,
                    "Actual record visits advance a five-part exploration challenge");
                if (i < kinds.Length-1)
                    Check(!g.AbyssTestimonySealed,
                        "Partial trail progress never awards the final bonus");
                g.Close();
            }
            Check(g.AbyssTestimonySealed &&
                g.save.runCoins == 78 && g.save.runShards == 5 &&
                g.save.runXp == 85 &&
                g.save.coins == coins && g.save.shards == shards &&
                !g.Objective().Contains("巡路证词"),
                "All five physical records award exactly one unbanked completion bonus");
            g.save.x = sites[0].x;g.save.y = sites[0].y;
            g.Talk(sites[0]);g.Close();
            g.save.x = sites[4].x;g.save.y = sites[4].y;
            g.Talk(sites[4]);g.Close();
            Check(g.save.runCoins == 78 && g.save.runShards == 5 &&
                g.save.runXp == 85,
                "A repeated record visit cannot mint duplicate bonus loot");
            g.enemies.Clear();g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.InAbyss && loaded.AbyssTestimonyProgress == 5 &&
                loaded.AbyssTestimonySealed &&
                loaded.save.runCoins == 78 && loaded.save.runShards == 5 &&
                loaded.save.runXp == 85,
                "SaveData v2 restores all five records and the exact pending reward");
            loaded.mode = "world";
            WorldObject exit = Array.Find(loaded.Map.objects,
                o => o.kind == "abyssExit");
            loaded.save.x = exit.x;loaded.save.y = exit.y;
            loaded.Talk(exit);loaded.Choose(0);
            Check(!loaded.InAbyss && loaded.save.runCoins == 0 &&
                loaded.save.runShards == 0 && loaded.save.runXp == 0 &&
                loaded.save.coins == coins && loaded.save.shards == shards,
                "Early extraction forfeits all five-site testimony bonus earnings");
            loaded.StartAbyss(0);
            Check(loaded.InAbyss && loaded.AbyssTestimonyProgress == 0 &&
                !loaded.AbyssTestimonySealed,
                "A new expedition resets only temporary trail proof flags");
        }

        public static void AbyssStarrootCircuitRequiresThreeRealSeededRelayVisits(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            int width = g.Map.rows[0].Length;
            var reachable = Reachable(g);
            WorldObject[] relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
            {
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                Check(relays[i] != null && g.CanStand(
                    new Point(relays[i].x, relays[i].y)) &&
                    reachable.Contains((int)relays[i].y * width + (int)relays[i].x),
                    "Each new starroot relay has a real traversable branch location");
            }
            g.enemies.Clear();
            g.save.runCoins = 5; g.save.runShards = 2; g.save.runXp = 3;
            g.save.shards = 99; g.save.energy = 100;
            int first = g.AbyssCircuitNextNode;
            Check(first >= 0 && first < 3 && g.AbyssCircuitProgress == 0 &&
                g.Objective().Contains("信标 " + (first == 0 ? "A" : first == 1 ? "B" : "C")),
                "The main task cue gives the actual first seeded target");
            g.save.x = 5; g.save.y = 14;
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 0 && g.save.runCoins == 5,
                "Remote E interaction cannot fake a circuit node visit");
            int wrong = (first + 1) % 3;
            g.save.x = relays[wrong].x; g.save.y = relays[wrong].y;
            g.Talk(relays[wrong]);
            Check(g.AbyssCircuitProgress == 0 && g.save.energy == 88 &&
                g.save.runShards == 2,
                "Wrong seeded relay costs real energy and leaves rewards untouched");

            g.save.x = relays[first].x; g.save.y = relays[first].y;
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 1 && g.AbyssCircuitVisited(first),
                "The first physically activated relay records progress");
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 1,
                "Repeated interaction with an already-linked relay is harmless");
            // Flag-only partial progress must reconstruct in the existing v2
            // save schema without a new serialized field or new game mode.
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssCircuitProgress == 1 &&
                restored.AbyssCircuitNextNode == g.AbyssCircuitNextNode,
                "Mid-circuit SaveData v2 reload preserves the exact next seed target");
            restored.mode = "world";
            int step1 = restored.AbyssCircuitNextNode;
            WorldObject second = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + step1);
            restored.save.x = second.x; restored.save.y = second.y;
            restored.Talk(second);
            Check(restored.AbyssCircuitProgress == 2,
                "Second physical relay continues from the loaded partial path");
            int step2 = restored.AbyssCircuitNextNode;
            WorldObject third = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + step2);
            restored.save.x = third.x; restored.save.y = third.y;
            restored.Talk(third);
            Check(restored.AbyssCircuitComplete && restored.AbyssCircuitProgress == 3 &&
                restored.save.runCoins == 45 && restored.save.runShards == 6 &&
                restored.save.runXp == 33 && restored.save.shards == 99,
                "Three correct seeded visits award ONLY pending capped coins, shards and XP");
            restored.Talk(third);
            Check(restored.save.runCoins == 45 && restored.save.runShards == 6 &&
                restored.save.runXp == 33,
                "The final relay cannot mint duplicate pending treasure");

            // Extracting early must discard the optional loot, just like every
            // other genuine Abyss branch reward.
            restored.save.x = 3; restored.save.y = 14;
            WorldObject exit = Array.Find(restored.Map.objects,
                o => o.kind == "abyssExit");
            restored.Talk(exit); restored.Choose(0);
            Check(!restored.InAbyss && restored.save.runShards == 0 &&
                restored.save.shards == 99,
                "Early extraction loses pending circuit earnings, never permanent shards");
        }

        public static void LongAbyssCircuitReturnSnapsCameraWithoutBreakingWalkingSmoothing(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit2");
            g.save.x = relay.x; g.save.y = relay.y;
            Point before = g.Position;
            g.Talk(relay); g.Choose(0);
            Point target = g.Position;
            Check(WorldStreaming.LargeCameraJump(
                target.x-before.x,target.y-before.y),
                "Real long-gallery circuit return is classified as a room-scale camera jump");
            Point teleported = WorldStreaming.FollowCamera(before,target,.016f,false);
            Check(teleported.x == target.x && teleported.y == target.y &&
                WorldStreaming.LargeCameraJump(
                    teleported.x-before.x,teleported.y-before.y),
                "Production camera reaches the hero instantly and triggers terrain prewarm on teleport");
            Point beforeWalk = new Point(12,12);
            Point nextWalk = new Point(13,12.5f);
            Point smooth = WorldStreaming.FollowCamera(beforeWalk,nextWalk,.016f,false);
            Check(smooth.x > beforeWalk.x && smooth.x < nextWalk.x &&
                smooth.y > beforeWalk.y && smooth.y < nextWalk.y,
                "Normal walking still uses progressive 12Hz exponential camera tracking");
            Point still = WorldStreaming.FollowCamera(beforeWalk,nextWalk,0,false);
            Check(still.x == beforeWalk.x && still.y == beforeWalk.y,
                "A paused zero-delta frame does not drift the camera");
            Point accessibility = WorldStreaming.FollowCamera(beforeWalk,nextWalk,.016f,true);
            Check(accessibility.x == nextWalk.x && accessibility.y == nextWalk.y,
                "Reduced-motion camera retains immediate accessibility tracking");
        }

        public static void AbyssCircuitRouteRequiresClearedFloorAndPhysicalRelay(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit1");
            g.save.x = relay.x; g.save.y = relay.y;
            int initialRoom = g.save.room, initialShards = g.save.shards;
            g.save.runShards = 7;
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "Circuit alone cannot skip live enemies and offer an evacuation shortcut");
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            g.hazards.Add(new Hazard {
                position = new Point(relay.x, relay.y), timer = 1.5f,
                radius = 1.4f, damage = 10, kind = "rift"
            });
            g.Talk(relay);
            Check(g.mode == "dialog" &&
                g.conversation.choices.Length == 2 &&
                g.conversation.choices[0].action == "abyssCircuitGround" &&
                g.save.x == relay.x && g.save.runShards == 7,
                "A queued rift can offer emergency grounding but NEVER bypass the safe-return travel lock");
            g.Close();
            g.hazards.Clear();
            g.Talk(relay);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5 &&
                g.conversation.choices[0].action == "abyssCircuitReturn",
                "A cleared relay offers the east gate and three live branch destinations");

            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 7,
                "Walking away revokes a previously opened relay shortcut choice");
            g.save.x = relay.x; g.save.y = relay.y;
            g.Talk(relay); g.Choose(0);
            Check(g.mode == "world" && g.save.room == initialRoom &&
                Math.Abs(g.save.x - 30.5f) < .01f &&
                Math.Abs(g.save.y - 14.5f) < .01f &&
                g.save.runShards == 7 && g.save.shards == initialShards,
                "Fast return moves within the same dungeon but does not bank or alter any loot");
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            g.Talk(gate);
            Check(g.mode == "dialog" && g.conversation.choices.Length > 0,
                "A relay shortcut reaches the REAL settlement gate interaction");
            // Choose the final 'bank/extract' option; no automatic rewards at
            // teleport time, only the existing protected BankFloor transaction.
            g.Choose(g.conversation.choices.Length - 1);
            Check(g.save.room == 0 && g.save.shards == initialShards + 7,
                "All pending shards bank only after personally using the root gate");
        }

        public static void StarrootRouteMenusCountUnvisitedLiveSideSites(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            int[] counts = new int[3];
            int total = 0;
            for (int i = 0; i < 3; i++)
            {
                counts[i] = g.AbyssCircuitUnvisitedSites(i);
                total += counts[i];
            }
            Check(total == 6,
                "The six real optional floor-one survey/cache/echo/rootwell world sites have branch assignments");
            // An existing SaveData v2 flag, not a new UI counter, is the
            // only authoritative source of a branch's remaining content.
            g.save.flags.Add("abyss_annex_survey_1");
            int reduced = 0;
            for (int i = 0; i < 3; i++)
                reduced += g.AbyssCircuitUnvisitedSites(i);
            Check(reduced == total - 1,
                "Actually recording a real survey immediately removes one route hint");

            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o=>o.kind=="abyssCircuit0");
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);
            Check(g.mode=="dialog" && g.conversation.choices.Length==5,
                "The linked physical relay offers its original five route choices");
            for (int i=0;i<3;i++)
            {
                int sites = g.AbyssCircuitUnvisitedSites(i);
                Check(g.conversation.choices[i+1].label.Contains("待探索 " + sites) &&
                    g.conversation.choices[i+1].en.Contains(sites + " sites left"),
                    "Branch " + i + " labels show actual pending sites, not invented rewards");
            }
            g.Close();
            var gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=30.5f;g.save.y=14.5f;
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.mode=="dialog" && g.conversation.choices.Length==4,
                "The real east gate submenu exposes the same live unvisited counters");
            for (int i=0;i<3;i++)
                Check(g.conversation.choices[i].label.Contains("待探索 " +
                    g.AbyssCircuitUnvisitedSites(i)),
                    "East gate and field relays derive branch counters from identical world objects");
            g.Close();g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.InAbyss && reloaded.AbyssCircuitComplete,
                "Optional route hints remain available after a legitimate v2 reload");
            for (int i=0;i<3;i++)
                Check(reloaded.AbyssCircuitUnvisitedSites(i)==
                    g.AbyssCircuitUnvisitedSites(i),
                    "Every branch's unvisited counter remains stable on v2 reload");

            var deep = Unlocked(data);deep.StartAbyss(0);
            deep.save.abyssFloor=3;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic|BindingFlags.Instance).Invoke(deep,null);
            int deepTotal=0;
            for (int i=0;i<3;i++) deepTotal+=deep.AbyssCircuitUnvisitedSites(i);
            Check(deepTotal==7,
                "Deep floors add one real unvisited rift ward site to six optional sources");
            deep.save.flags.Add("abyss_annex_ward_3");
            int tuned=0;
            for (int i=0;i<3;i++) tuned+=deep.AbyssCircuitUnvisitedSites(i);
            Check(tuned==deepTotal-1,
                "Purchasing the actual deep rift ward removes its site from the live route tally");
        }



        public static void StarrootPuzzleWaypointsTrackRealRelaysAcrossMistakesAndReloads(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            Point east = new Point(31, 14);
            Check((g.NavigationTarget().Value - east).Length < .001f &&
                g.AbyssCircuitGuidedNextRelay == -1,
                "Untouched optional circuit preserves the original east-gate guide");

            var relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
            Check(Array.TrueForAll(relays, o => o != null),
                "The puzzle uses the three actually placed game-world relays");
            int first = g.AbyssCircuitNextNode;
            int mistake = (first + 1) % 3;
            g.save.x = relays[mistake].x; g.save.y = relays[mistake].y;
            float startingEnergy = g.save.energy;
            g.Talk(relays[mistake]);
            Check(g.AbyssCircuitProgress == 0 &&
                g.AbyssCircuitGuidedNextRelay == first &&
                (g.NavigationTarget().Value -
                    new Point(relays[first].x, relays[first].y)).Length < .001f &&
                g.save.energy == Math.Max(0, startingEnergy - 12),
                "Wrong first relay charges the original energy penalty and guides the correct restart");

            g.save.x = relays[first].x; g.save.y = relays[first].y;
            g.Talk(relays[first]);
            int second = g.AbyssCircuitNextNode;
            Check(g.AbyssCircuitProgress == 1 &&
                second != first && g.AbyssCircuitGuidedNextRelay == second &&
                (g.NavigationTarget().Value -
                    new Point(relays[second].x, relays[second].y)).Length < .001f,
                "First correct physical interaction guides the next actual relay");

            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssCircuitProgress == 1 &&
                restored.AbyssCircuitGuidedNextRelay == second &&
                (restored.NavigationTarget().Value -
                    new Point(relays[second].x, relays[second].y)).Length < .001f,
                "Old SaveData v2 step flags resume the puzzle guide without a new field");

            for (int progress = 1; progress < 3; progress++)
            {
                int target = g.AbyssCircuitNextNode;
                g.save.x = relays[target].x; g.save.y = relays[target].y;
                g.Talk(relays[target]);
            }
            Check(g.AbyssCircuitComplete &&
                g.AbyssCircuitGuidedNextRelay == -1 &&
                (g.NavigationTarget().Value - east).Length < .001f &&
                g.save.runShards >= 4,
                "Real three-step completion returns to the gate and awards only its existing unbanked loot");
        }


        public static void StarrootGalleryGuideResolvesLockedEchoPrerequisite(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject echo = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarEcho");
            WorldObject source = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarSurvey");
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            Check(echo != null && source != null &&
                g.AbyssTestimonyRecordState(echo) == 3,
                "The real optional echo begins locked behind its actual source tablet");

            string[] others = { "survey", "cache", "far_cache",
                "rootwell", "ward" };
            foreach (string key in others)
                g.save.flags.Add("abyss_annex_" + key + "_1");
            int branch = -1;
            float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                WorldObject relay = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                float dx = relay.x - echo.x, dy = relay.y - echo.y;
                float length2 = dx * dx + dy * dy;
                if (length2 < best) { best = length2; branch = i; }
            }
            int banked = g.save.shards;
            int pending = g.save.runShards;
            g.save.x = gate.x; g.save.y = gate.y;
            g.Talk(gate); g.Choose(g.conversation.choices.Length - 2);
            g.Choose(branch);
            Point target = g.NavigationTarget().Value;
            Check(g.AbyssCircuitGuideNeedsSource &&
                (target - new Point(source.x, source.y)).Length < .001f &&
                (target - new Point(echo.x, echo.y)).Length > .001f &&
                g.save.shards == banked && g.save.runShards == pending,
                "Locked echo routes to the physically placed source without granting rewards");

            g.save.flags.Add("abyss_annex_far_survey_1");
            Check(!g.AbyssCircuitGuideNeedsSource &&
                (g.NavigationTarget().Value -
                    new Point(echo.x, echo.y)).Length < .001f &&
                g.AbyssTestimonyRecordState(echo) == 1,
                "After actual source progress the live guide returns to now-unlocked echo");
            g.save.flags.Add("abyss_annex_far_echo_1");
            Check(g.AbyssCircuitGuidedBranch == -1 &&
                (g.NavigationTarget().Value - new Point(31, 14)).Length < .001f,
                "Collecting echo completes the selected branch and restores exit guidance");
        }

        public static void StarrootSelectedRouteGuidesRealUnvisitedSites(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            Point exit = new Point(31, 14);
            g.save.x = gate.x; g.save.y = gate.y;
            Check((g.NavigationTarget().Value - exit).Length < .001f,
                "Before route selection the original gate remains the waypoint");
            int branch = -1;
            for (int i = 0; i < 3; i++)
                if (g.AbyssCircuitUnvisitedSites(i) >= 2) { branch = i; break; }
            Check(branch >= 0, "One floor-one branch has at least two real optional sites");
            int remaining = g.AbyssCircuitUnvisitedSites(branch);
            g.Talk(gate);
            g.Choose(g.conversation.choices.Length - 2);
            g.Choose(branch);
            Point first = g.NavigationTarget().Value;
            Check(g.AbyssCircuitGuidedBranch == branch,
                "The selected physical relay exposes its live branch ID to the world renderer");
            WorldObject site = Array.Find(g.Map.objects,
                o => (new Point(o.x, o.y) - first).Length < .001f);
            Check(site != null && (first - exit).Length > 1f &&
                g.AbyssCircuitUnvisitedSites(branch) == remaining,
                "Actual route travel points to a real gallery site without granting any loot");
            string suffix = site.kind == "abyssSurvey" ? "survey" :
                site.kind == "abyssCache" ? "cache" :
                site.kind == "abyssFarSurvey" ? "far_survey" :
                site.kind == "abyssFarCache" ? "far_cache" :
                site.kind == "abyssFarEcho" ? "far_echo" :
                site.kind == "abyssRootwell" ? "rootwell" :
                site.kind == "abyssRiftWard" ? "ward" : null;
            Check(suffix != null, "The target has a genuine existing optional-site flag");
            g.save.flags.Add("abyss_annex_" + suffix + "_1");
            Check(g.AbyssCircuitUnvisitedSites(branch) == remaining - 1 &&
                (g.NavigationTarget().Value - first).Length > .001f,
                "Claiming the site's existing v2 flag reroutes the waypoint immediately");
            string[] keys = { "survey", "cache", "far_survey", "far_cache",
                "far_echo", "rootwell", "ward" };
            foreach (string key in keys)
                g.save.flags.Add("abyss_annex_" + key + "_1");
            Check(g.AbyssCircuitUnvisitedSites(branch) == 0 &&
                g.AbyssCircuitGuidedBranch == -1 &&
                (g.NavigationTarget().Value - exit).Length < .001f,
                "A cleared branch falls back to the original gate, not an invented marker");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit" + branch);
            g.Talk(relay); g.Choose(0);
            Check((g.NavigationTarget().Value - exit).Length < .001f,
                "Travel back through the actual route clears the local gallery target");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss &&
                (restored.NavigationTarget().Value - exit).Length < .001f,
                "Existing v2 flags survive reload; transient guide intent does not persist");
        }

        public static void AbyssCircuitTravelNeverRelocatesUnreleasedCombatActions(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit2");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.save.x=relay.x;g.save.y=relay.y;
            g.save.energy=100;g.mode="world";
            g.Attack(new Point(1,0));
            Check(g.playerAction != null && !g.playerAction.released,
                "A true player attack has an unreleased live contact");
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "Active player attack cannot open a free relocation menu");
            g.playerAction=null;
            g.companionAction=new ActorAction {
                kind="command",weapon=0,contact=.2f,duration=.4f
            };
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "An unreleased companion command cannot travel across the map");
            g.companionAction=null;
            g.Talk(relay);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5,
                "Finishing all combat contacts unlocks the legitimate completed-circuit routes");
            // An action may become pending AFTER the route menu was opened;
            // the choice must revalidate current release state.
            g.playerAction=new ActorAction {
                kind="attack",weapon=0,contact=.2f,duration=.4f
            };
            g.Choose(0);
            Check(Math.Abs(g.save.x-relay.x)<.01f &&
                Math.Abs(g.save.y-relay.y)<.01f &&
                g.mode=="world",
                "Choice-time gate check prevents an old menu from moving live windup attacks");
            g.playerAction=null;
            g.Talk(relay);g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f,
                "Once attacks end, the exact same legal route becomes usable");
            g.playerAction=new ActorAction {
                kind="attack",weapon=0,contact=.2f,duration=.4f
            };
            g.Talk(gate);
            Check(g.mode=="dialog" &&
                !Array.Exists(g.conversation.choices,
                    c=>c.action=="abyssCircuitRoutes"),
                "The east gate does not advertise a circuit transfer during live combat animation");
            g.Close();
            g.playerAction=null;
            g.Talk(gate);
            Check(g.mode=="dialog" &&
                Array.Exists(g.conversation.choices,
                    c=>c.action=="abyssCircuitRoutes"),
                "The protected final banking gate resumes normal network travel after recovery");
        }

        public static void StarrootCircuitRecallsHeldCompanionOnlyOnExplicitSafeChoice(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var a=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit0");
            var c=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit2");
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.mode="world";g.save.x=a.x;g.save.y=a.y;
            g.SelectCompanion(1);
            g.companionCooldown=9f;
            g.ToggleCompanionHold();
            Point anchor=g.companionPosition;
            Check(g.CompanionHoldingPosition,"Original companion receives a real hold order");
            g.Talk(a);g.Choose(3); // Hero travels to relay C.
            Check((g.companionPosition-anchor).Length<.001f&&
                g.CompanionHoldingPosition,
                "The hold remains physical and intact after solo hero transfer");
            g.Talk(c);
            Check(g.conversation.choices.Length==6 &&
                g.conversation.choices[4].action=="abyssCircuitRecall",
                "A held companion adds exactly one explicit recall choice to relay menu");
            g.save.x=5;g.save.y=14;g.Choose(4);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-anchor).Length<.001f,
                "Old remote menu cannot teleport a held companion to the hero");
            g.save.x=c.x;g.save.y=c.y;g.Talk(c);
            int generation=g.companionPresentationGeneration;
            g.Choose(4);
            Check(!g.CompanionHoldingPosition &&
                g.companionPresentationGeneration==generation+1 &&
                (g.companionPosition-g.Position).Length<4f &&
                (g.companionPosition-g.Position).Length>.55f &&
                g.CanStand(g.companionPosition)&&
                Math.Abs(g.companionCooldown-9f)<.001f &&
                g.companionMoveTime==0f&&!g.companionMoving,
                "Explicit relay recall rejoins a real legal foot and rebases gait without skill refresh");

            // Also expose recall at the actual settlement GATE, without
            // ever moving the hero or automatically banking their loot.
            g.ToggleCompanionHold();
            Point heldAtC=g.companionPosition;
            g.Talk(c);g.Choose(0);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-heldAtC).Length<.001f &&
                Math.Abs(g.save.x-30.5f)<.01f,
                "A second deliberate hold survives a different long route");
            g.save.runShards=6;g.save.runCoins=17;
            int banked=g.save.shards;
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.conversation.choices.Length==5 &&
                g.conversation.choices[3].action=="abyssCircuitRecall" &&
                g.conversation.choices[4].action=="abyssCircuitGateMenu",
                "Gate submenu offers recall only while a real ally remains stationed");
            // A hostile that arrives after opening the route UI must still
            // block both ally recalls and hero transfers at choice-time.
            g.enemies.Add(new Enemy(938,0,31.5f,14.5f,25f));
            g.Choose(3);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-heldAtC).Length<.001f &&
                g.save.runShards==6,
                "Combat threat arising after menu-open cannot recall a stationed ally");
            g.enemies.Clear();
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            g.Choose(3);
            Check(!g.CompanionHoldingPosition &&
                (g.companionPosition-g.Position).Length<4f &&
                g.CanStand(g.companionPosition) &&
                g.save.runCoins==17&&g.save.runShards==6&&
                g.save.shards==banked,
                "Safe gate recall restores formation without creating or banking loot");
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.conversation.choices.Length==4 &&
                g.conversation.choices[3].action=="abyssCircuitGateMenu",
                "After recall, the extra choice disappears and original route options remain");
        }

        public static void StarrootFastTravelBringsUnheldCompanionWithoutFakeGait(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var a=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit0");
            var b=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit1");
            var c=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit2");
            Check(a!=null&&b!=null&&c!=null,
                "Party co-travel uses actual three world-placed relay stops");
            g.mode="world";g.save.x=a.x;g.save.y=a.y;
            g.SelectCompanion(0);
            Check(g.save.companion==0 &&
                (g.companionPosition-g.Position).Length<4f,
                "Recruited companion begins in the real original formation");
            g.companionCooldown=8f;
            g.companionMoveTime=7.2f;
            g.companionMoving=true;
            int generation=g.companionPresentationGeneration;
            g.Talk(a);g.Choose(3); // The third branch destination is C.
            Check(Math.Abs(g.save.x-c.x)<.01f &&
                Math.Abs(g.save.y-c.y)<.01f &&
                g.CanStand(g.companionPosition) &&
                (g.companionPosition-g.Position).Length>.55f &&
                (g.companionPosition-g.Position).Length<4f,
                "Relay A->C physically rejoins the companion at the new safe hero foot");
            Check(g.companionPresentationGeneration==generation+1 &&
                g.companionMoveTime==0f && !g.companionMoving &&
                Math.Abs(g.companionCooldown-8f)<.001f,
                "Fast party transfer rebases genuine full-body gait without refreshing skills");

            // Explicit companion hold is a real player order: the hero may
            // still travel, but the planted ally must NOT get dragged away.
            g.ToggleCompanionHold();
            Check(g.CompanionHoldingPosition,
                "A party member can receive a physical hold order at relay C");
            Point holdFoot=g.companionPosition;
            int holdGeneration=g.companionPresentationGeneration;
            g.Talk(c);g.Choose(2); // C->B
            Check(Math.Abs(g.save.x-b.x)<.01f &&
                Math.Abs(g.save.y-b.y)<.01f &&
                (g.companionPosition-holdFoot).Length<.001f &&
                g.CompanionHoldingPosition &&
                g.companionPresentationGeneration==holdGeneration,
                "Fast travel preserves deliberate hold and original planted companion gait");
            g.ToggleCompanionHold();
            Check(!g.CompanionHoldingPosition,
                "Explicit resume-follow cancels the held stance");
            g.Talk(b);g.Choose(1); // B->A
            Check(Math.Abs(g.save.x-a.x)<.01f &&
                (g.companionPosition-g.Position).Length<4f &&
                g.companionPresentationGeneration==holdGeneration+1 &&
                g.companionMoveTime==0f,
                "Next legal long transfer rejoins a formerly stationed party member");

            // Companions are optional. Fast travel must not fabricate a
            // body or advance the animation generation without an ally.
            g.SelectCompanion(-1);
            Check(g.save.companion==-1,"Companion can be dismissed normally");
            int soloGeneration=g.companionPresentationGeneration;
            g.Talk(a);g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                g.companionPresentationGeneration==soloGeneration,
                "Solo relay transfer preserves no-companion presentation state");
            g.SelectCompanion(2);
            Check(g.save.companion==2,"The party can choose another valid ally");
            g.save.runShards=4;
            g.PrepareSave();
            var restored = new GameCore(data,CloneSave(g.save));
            Check(restored.InAbyss && restored.save.companion==2 &&
                (restored.companionPosition-restored.Position).Length<4f &&
                restored.save.runShards==4,
                "SaveData v2 reload puts the chosen ally beside the real arrival without changing loot");
        }

        public static void CompletedAbyssCircuitAllowsSafeGateToGalleryRoundTrip(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runCoins = 43; g.save.runShards = 11;
            g.save.runXp = 24;
            int coins = g.save.coins, shards = g.save.shards;
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            WorldObject[] relays = new WorldObject[3];
            for (int i=0;i<3;i++)
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
            g.save.x=30.5f;g.save.y=14.5f;
            g.Talk(gate);
            Check(g.mode == "dialog" &&
                g.conversation.choices[g.conversation.choices.Length-2].action ==
                    "abyssCircuitRoutes" &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "Original bank action stays LAST when east gate exposes extra circuit route");
            g.Choose(g.conversation.choices.Length-2);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 4 &&
                g.conversation.choices[0].action == "abyssCircuitTo0" &&
                g.conversation.choices[3].action == "abyssCircuitGateMenu",
                "East gate opens a physically sourced three-destination route sub-menu");
            g.save.x=5;g.save.y=14;
            g.Choose(2);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 11,
                "Leaving the gate revokes its route sub-menu and prevents remote transfer");

            for (int destination=0;destination<3;destination++)
            {
                g.save.x=30.5f;g.save.y=14.5f;g.Talk(gate);
                g.Choose(g.conversation.choices.Length-2);
                g.Choose(destination);
                Check(g.save.room == 8 &&
                    Math.Abs(g.save.x-relays[destination].x)<.01f &&
                    Math.Abs(g.save.y-relays[destination].y)<.01f &&
                    g.save.runCoins == 43 && g.save.runShards == 11 &&
                    g.save.runXp == 24 && g.save.coins == coins &&
                    g.save.shards == shards,
                    "The real east gate reaches gallery " + destination +
                    " while preserving unbanked and permanent economy");
                g.Talk(relays[destination]);g.Choose(0);
                Check(Math.Abs(g.save.x-30.5f)<.01f &&
                    Math.Abs(g.save.y-14.5f)<.01f,
                    "Route from gallery " + destination +
                    " returns to the same original settlement gate");
            }
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            // A hostile can enter after the player has opened the route UI.
            // Re-evaluate threat state when they actually choose the stop.
            var enemy = new Enemy(910, 0, 31.5f,14.5f,28f);
            g.enemies.Add(enemy);
            g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                g.save.runShards == 11,
                "A newly spawned enemy blocks travel even from an already-open gate route menu");
            g.enemies.Clear();
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            g.Choose(3);
            Check(g.mode == "dialog" &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "Route sub-menu returns safely to the original settlement dialogue");
            g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room==0 && g.save.shards==shards+11 &&
                g.save.coins==coins+43,
                "Only the original east gate bank action makes final loot permanent");
        }

        public static void ClearedAbyssCircuitRoutesAmongPhysicalRelaysWithoutBanking(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(2);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runShards = 8; g.save.runCoins = 31; g.save.runXp = 27;
            int coins = g.save.coins, shards = g.save.shards;
            WorldObject[] relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
            {
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                Check(relays[i] != null &&
                    g.CanStand(new Point(relays[i].x, relays[i].y)),
                    "Branch destination " + i + " is a real standable world object");
            }
            g.save.x = relays[0].x; g.save.y = relays[0].y;
            g.Talk(relays[0]);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5,
                "Completed safe circuit exposes all three navigable branches and the gate");

            g.save.x = 5; g.save.y = 14; g.Choose(3);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 8,
                "A remote stale destination choice cannot teleport the hero");
            for (int target = 0; target < 3; target++)
            {
                int from = (target + 2) % 3;
                g.save.x = relays[from].x; g.save.y = relays[from].y;
                g.Talk(relays[from]); g.Choose(target + 1);
                Check(g.save.room == 8 &&
                    Math.Abs(g.save.x-relays[target].x) < .01f &&
                    Math.Abs(g.save.y-relays[target].y) < .01f &&
                    g.save.runShards == 8 && g.save.runCoins == 31 &&
                    g.save.runXp == 27 && g.save.shards == shards &&
                    g.save.coins == coins,
                    "Travel to branch " + target +
                    " preserves room, all pending loot and permanent economy");
            }
            g.save.x = relays[0].x; g.save.y = relays[0].y;
            g.Talk(relays[0]); g.Choose(2);
            Check(Math.Abs(g.save.x-relays[1].x)<.01f,
                "The middle relay remains a repeatable physical destination");
            g.projectiles.Add(new Projectile {
                friendly = false, position = new Point(relays[1].x,relays[1].y),
                velocity = new Point(1,0), life = 1, damage = 5
            });
            g.Talk(relays[1]);
            Check(g.mode == "world" &&
                Math.Abs(g.save.x-relays[1].x)<.01f,
                "Hostile projectiles block both the branch network and the gate route");
            g.projectiles.Clear();
            g.Talk(relays[1]); g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f &&
                g.save.shards == shards && g.save.runShards == 8,
                "A linked relay still takes the hero to the real gate without banking");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.Talk(gate); g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room == 0 && g.save.shards == shards + 8,
                "Only the original gate banking transaction makes circuit loot permanent");
        }

        public static void CompletedAbyssCircuitCanGroundOnlyLocalRiftsForPendingLoot(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runShards=3;g.save.shards=19;
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit0");
            Hazard local = new Hazard {
                position = new Point(relay.x,relay.y),radius=1.4f,
                timer=1.5f,damage=15,kind="rift"
            };
            Hazard other = new Hazard {
                position = new Point(relay.x,relay.y),radius=1.4f,
                timer=1.5f,damage=15,kind="bell"
            };
            Hazard distant = new Hazard {
                position = new Point(5,14),radius=1.4f,
                timer=1.5f,damage=15,kind="rift"
            };
            g.hazards.Add(local);g.hazards.Add(other);g.hazards.Add(distant);
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);
            Check(g.mode=="dialog"&&g.conversation.choices.Length==2,
                "A completed relay presents a resource-funded emergency grounding during live warnings");
            g.save.x=5;g.save.y=14;
            g.Choose(0);
            Check(g.save.runShards==3&&g.hazards.Count==3,
                "Leaving relay reach voids the old menu without spending loot or erasing attacks");
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);g.Choose(0);
            Check(g.Has("abyss_annex_circuit_ground_1") &&
                g.save.runShards==1&&g.save.shards==19&&
                !g.hazards.Contains(local)&&g.hazards.Contains(other)&&
                g.hazards.Contains(distant),
                "Grounding spends exactly two unbanked shards and retires ONLY local genuine rift warnings");
            int revision=g.revision;
            g.Talk(relay);
            Check(g.mode=="world"&&g.save.runShards==1&&
                g.hazards.Contains(distant)&&g.revision==revision,
                "An already-spent grounding charge cannot erase a second rift");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.InAbyss&&restored.Has("abyss_annex_circuit_ground_1")&&
                restored.save.runShards==1,
                "Spent emergency grounding persists across a standard SaveData v2 reload");

            var empty=Unlocked(data);empty.StartAbyss(0);
            empty.enemies.Clear();empty.hazards.Clear();
            empty.save.flags.Add("abyss_clear_1");
            empty.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject emptyRelay=Array.Find(empty.Map.objects,
                o=>o.kind=="abyssCircuit0");
            empty.save.x=emptyRelay.x;empty.save.y=emptyRelay.y;
            empty.save.runShards=0;empty.save.shards=200;
            empty.hazards.Add(new Hazard{
                position=new Point(emptyRelay.x,emptyRelay.y),
                timer=1.5f,radius=1.4f,damage=15,kind="rift"
            });
            empty.Talk(emptyRelay);
            Check(empty.mode=="world"&&empty.hazards.Count==1&&
                empty.save.shards==200&&
                !empty.Has("abyss_annex_circuit_ground_1"),
                "Permanent banked shards cannot finance a grounding without unbanked loot");
        }

        public static void MiswiredDeepAbyssCircuitCreatesDodgeableRealBacklash(Campaign data)
        {
            var deep = Unlocked(data); deep.StartAbyss(2);
            var generator = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deep.save.abyssFloor = 3;
            generator.Invoke(deep,null);
            deep.enemies.Clear(); deep.hazards.Clear();
            int expected = deep.AbyssCircuitNextNode;
            int wrongNode = (expected + 1) % 3;
            WorldObject wrong = Array.Find(deep.Map.objects,
                o => o.kind == "abyssCircuit" + wrongNode);
            deep.save.x = wrong.x; deep.save.y = wrong.y;
            deep.save.energy = 100;
            deep.Talk(wrong);
            Hazard backlash = deep.hazards.Find(h => h.kind == "rift");
            Check(backlash != null && deep.save.energy == 88 &&
                deep.AbyssCircuitProgress == 0 &&
                Math.Abs(backlash.timer - 1.6f) < .001f &&
                Math.Abs(backlash.radius - 1.35f) < .001f &&
                Math.Abs(backlash.damage - 18f) < .001f,
                "Incorrect deep relay uses a real 1.6s telegraphed rift and original energy reset");

            deep.invulnerable = 0; deep.save.health = deep.save.maxHealth;
            float before = deep.save.health;
            for (int i = 0; i < 20; i++) deep.Tick(.05f,new Point());
            Check(deep.save.health == before && deep.hazards.Contains(backlash),
                "Early reaction frames do not hit before the real rift warning completes");
            for (int i = 0; i < 15; i++) deep.Tick(.05f,new Point());
            Check(deep.save.health < before && !deep.hazards.Contains(backlash),
                "An undodged wrong-order rift delivers real HP damage and retires its warning");

            var dodge = Unlocked(data); dodge.StartAbyss(0);
            dodge.save.abyssFloor = 3; generator.Invoke(dodge,null);
            dodge.enemies.Clear(); dodge.hazards.Clear();
            WorldObject dnode = Array.Find(dodge.Map.objects,
                o => o.kind == "abyssCircuit" + ((dodge.AbyssCircuitNextNode + 1) % 3));
            dodge.save.x = dnode.x; dodge.save.y = dnode.y;
            dodge.Talk(dnode);
            Check(dodge.hazards.Exists(h => h.kind == "rift"),
                "Wrong node creates the normal visible warning in a second run");
            dodge.save.x = 5; dodge.save.y = 14;
            dodge.save.health = dodge.save.maxHealth;
            dodge.invulnerable = 0;
            for (int i = 0; i < 40; i++) dodge.Tick(.05f,new Point());
            Check(dodge.save.health == dodge.save.maxHealth,
                "Leaving the marked circle before contact avoids ALL backlash damage");

            var early = Unlocked(data); early.StartAbyss(0);
            early.enemies.Clear();early.hazards.Clear();
            WorldObject earlyWrong = Array.Find(early.Map.objects,
                o => o.kind == "abyssCircuit" + ((early.AbyssCircuitNextNode + 1) % 3));
            early.save.x = earlyWrong.x; early.save.y = earlyWrong.y;
            early.save.energy = 100; early.Talk(earlyWrong);
            Check(early.save.energy == 88 && early.hazards.Count == 0,
                "Accessible floor-one puzzle retains energy-only teaching penalty");
        }

        public static void LinkedAbyssRelaysShelterAgainstRealRiftContacts(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var u = Unlocked(data); u.StartAbyss(0);
            var generate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (GameCore hero in new [] { g, u })
            {
                hero.save.abyssFloor = 3; hero.save.abyssSeed = 139;
                generate.Invoke(hero, null);
                hero.enemies.Clear(); hero.hazards.Clear();
                hero.save.flags.Add("abyss_clear_3");
                hero.mode = "world"; hero.invulnerable = 0;
                hero.save.health = hero.save.maxHealth;
            }
            int node = g.AbyssCircuitNextNode;
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit" + node);
            Check(relay != null && g.CanStand(new Point(relay.x,relay.y)),
                "Shelter fixture uses a genuine standable seeded relay");
            g.save.x = u.save.x = relay.x;
            g.save.y = u.save.y = relay.y;
            Check(!g.AbyssCircuitSheltered && !u.AbyssCircuitSheltered,
                "Unconnected shrines do not provide a free defensive aura");
            g.save.flags.Add("abyss_annex_circuit_3_0");
            Check(g.AbyssCircuitSheltered && !u.AbyssCircuitSheltered &&
                g.Conditions().Contains("信标庇护"),
                "Physically linked circuit relay provides a visible tactical shelter");
            float hp = g.save.health;
            foreach (GameCore hero in new [] { g, u })
                hero.hazards.Add(new Hazard {
                    position = new Point(relay.x,relay.y),
                    radius = 1.2f, timer = .02f, damage = 20,kind = "rift"
                });
            g.Tick(.05f,new Point()); u.Tick(.05f,new Point());
            float shelteredDamage = hp - g.save.health;
            float exposedDamage = hp - u.save.health;
            Check(shelteredDamage > 0 && exposedDamage > shelteredDamage &&
                Math.Abs(shelteredDamage / exposedDamage - .45f) < .01f,
                "A REAL ground rift contact deals exactly 55% less damage within the connected shelter");
            Check(g.hazards.Count == 0 && u.hazards.Count == 0,
                "Both actual hazards complete their ordinary contact and retirement");

            // A different kind of real ground contact must never benefit.
            g.invulnerable = u.invulnerable = 0;
            g.save.health = u.save.health = hp;
            foreach (GameCore hero in new [] { g, u })
                hero.hazards.Add(new Hazard {
                    position = new Point(relay.x,relay.y),
                    radius = 1.2f,timer = .02f,damage = 20,kind = "bell"
                });
            g.Tick(.05f,new Point()); u.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health - u.save.health) < .001f,
                "The circuit protects against Rift contacts only, never unrelated Bell or boss hazards");

            // Old v2 data stores flags but no new serialized object state.
            g.PrepareSave();
            var reload = new GameCore(data,CloneSave(g.save));
            Check(reload.InAbyss && reload.AbyssCircuitSheltered,
                "Shelter is reconstructed from the ordinary v2 partial-circuit flag and position");
            g.save.room = 0;
            Check(!g.AbyssCircuitSheltered,
                "Travel out of the Abyss cannot retain a remote shield");
        }

        public static void AbyssCircuitIncompleteV2FlagsRecoverWithoutFreeRewards(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            g.save.runCoins = 1; g.save.runShards = 1; g.save.runXp = 1;
            // Simulate a partially written flags list: three step markers
            // persist, but the final completion transaction did not.
            for (int step = 0; step < 3; step++)
                g.save.flags.Add("abyss_annex_circuit_1_" + step);
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && !restored.AbyssCircuitComplete &&
                restored.AbyssCircuitProgress == 2 &&
                restored.AbyssCircuitNextNode >= 0 &&
                restored.AbyssCircuitNextNode <= 2 &&
                restored.Conditions().Contains("回路 2/3") &&
                restored.Objective().Contains("信标"),
                "An interrupted v2 completion never indexes relay four or crashes the HUD");
            int thirdId = restored.AbyssCircuitNextNode;
            WorldObject third = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + thirdId);
            restored.mode = "world";
            restored.save.x = third.x; restored.save.y = third.y;
            restored.Talk(third);
            Check(restored.AbyssCircuitComplete &&
                restored.save.runCoins == 41 &&
                restored.save.runShards == 5 &&
                restored.save.runXp == 31,
                "Revisiting the actual final relay repairs completion and credits only one normal payout");
            restored.Talk(third);
            Check(restored.save.runCoins == 41 && restored.save.runShards == 5,
                "Repaired legacy circuit cannot be claimed a second time");
        }

        public static void AbyssCircuitSeedPermutationAndResetAreDeterministic(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var starts = new HashSet<int>();
            for (int seed = 100; seed < 106; seed++)
            {
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                starts.Add(g.AbyssCircuitNextNode);
                Check(g.AbyssCircuitProgress == 0,
                    "Changing the seed does not grant unsolved circuit progress");
                int a = g.AbyssCircuitNextNode;
                // Each valid seed defines a genuine permutation: targets
                // exhaust A/B/C exactly once without repetition.
                g.save.flags.Add("abyss_annex_circuit_1_0");
                int b = g.AbyssCircuitNextNode;
                g.save.flags.Add("abyss_annex_circuit_1_1");
                int c = g.AbyssCircuitNextNode;
                Check(a != b && a != c && b != c,
                    "Each seeded circuit has three distinct relay targets");
                g.save.flags.Remove("abyss_annex_circuit_1_0");
                g.save.flags.Remove("abyss_annex_circuit_1_1");
            }
            Check(starts.Count == 3, "Six seed variants distribute starting nodes across all three caves");
            g.save.abyssSeed = int.MaxValue;
            regenerate.Invoke(g, null);
            Check(g.AbyssCircuitNextNode >= 0 && g.AbyssCircuitNextNode <= 2,
                "A maximum legal legacy seed cannot overflow the circuit permutation");
        }

        public static void AbyssRootwellTradesPendingLootForSurvival(Campaign data)
        {
            var g = Unlocked(data);
            g.StartAbyss(1);
            WorldObject well = Array.Find(g.Map.objects, o => o.kind == "abyssRootwell");
            Check(well != null && g.CanStand(new Point(well.x, well.y)),
                "The optional rootwell is a real standable object in the Abyss");
            var reachable = Reachable(g);
            int width = g.Map.rows[0].Length;
            Check(reachable.Contains((int)well.y * width + (int)well.x),
                "The new rootwell is walkably connected to the original entrance");

            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.health = 20; g.save.energy = 25;
            g.save.runShards = 3; g.save.shards = 40;
            Check(g.Objective().Contains("复苏泉"),
                "Low-health delvers with pending shards see the optional rootwell route");
            int coins = g.save.coins, revision = g.revision;
            float health = g.save.health;
            g.Talk(well);
            Check(g.mode == "world" && g.toast.Contains("走近") &&
                g.save.runShards == 3 && g.save.health == health,
                "Remote interaction cannot open a rootwell restoration menu");

            g.save.x = well.x; g.save.y = well.y;
            g.Talk(well);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 3,
                "The rootwell offers two distinct survival trades or decline");
            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.runShards == 3 && g.save.health == health &&
                g.mode == "world",
                "A stale restoration choice cannot spend shards after walking away");

            g.save.x = well.x; g.save.y = well.y;
            g.Talk(well); g.Choose(0);
            Check(g.Has("abyss_annex_rootwell_1") &&
                g.save.runShards == 1 && g.save.shards == 40 &&
                g.save.coins == coins &&
                Math.Abs(g.save.health - Math.Min(g.save.maxHealth,
                    health + .4f * g.save.maxHealth)) < .001f &&
                g.save.energy == 25 && g.revision > revision,
                "Health recovery trades exactly two pending shards for 40% max HP");
            g.Talk(well);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 1,
                "An exhausted font cannot be purchased again on the same floor");
            g.Close();
            Check(!g.Objective().Contains("复苏泉"),
                "The optional rootwell hint disappears after its charge is spent");
            // Real enemy-clear progression sets this flag at the final kill;
            // an empty unfinished floor intentionally invalidates unbanked
            // loot when reconstructed and cannot model a legitimate save.
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.Has("abyss_clear_1") &&
                restored.Has("abyss_annex_rootwell_1") &&
                restored.save.runShards == 1 &&
                Math.Abs(restored.save.health - g.save.health) < .001f,
                "SaveData v2 restores spent charge and precisely bankable rewards");

            var energy = Unlocked(data); energy.StartAbyss(0);
            var energyWell = Array.Find(energy.Map.objects, o => o.kind == "abyssRootwell");
            energy.enemies.Clear(); energy.hazards.Clear(); energy.projectiles.Clear();
            energy.save.x = energyWell.x; energy.save.y = energyWell.y;
            energy.save.runShards = 1; energy.save.shards = 19;
            energy.save.energy = 15; energy.save.health = 38;
            energy.Talk(energyWell); energy.Choose(1);
            Check(energy.save.energy == 70 && energy.save.health == 38 &&
                energy.save.runShards == 0 && energy.save.shards == 19 &&
                energy.Has("abyss_annex_rootwell_1"),
                "Energy recovery trades one pending shard for 55 energy");

            var empty = Unlocked(data); empty.StartAbyss(2);
            var emptyWell = Array.Find(empty.Map.objects, o => o.kind == "abyssRootwell");
            empty.enemies.Clear(); empty.hazards.Clear(); empty.projectiles.Clear();
            empty.save.x = emptyWell.x; empty.save.y = emptyWell.y;
            empty.save.runShards = 0; empty.save.shards = 200;
            empty.save.health = 12;
            empty.Talk(emptyWell); empty.Choose(0);
            Check(empty.save.health == 12 && empty.save.shards == 200 &&
                empty.save.runShards == 0 && !empty.Has("abyss_annex_rootwell_1"),
                "Permanent shards cannot bypass the unbanked loot cost");
            empty.save.runShards = 2; empty.save.health = empty.save.maxHealth;
            empty.Talk(emptyWell); empty.Choose(0);
            Check(empty.save.runShards == 2 && !empty.Has("abyss_annex_rootwell_1"),
                "Full health cannot consume shards or the floor's only charge");
            empty.save.health = 20;
            empty.enemies.Add(new Enemy(902, 0, emptyWell.x + 1, emptyWell.y, 35));
            empty.Talk(emptyWell);
            Check(empty.mode == "world" && empty.save.runShards == 2 &&
                !empty.Has("abyss_annex_rootwell_1"),
                "Live nearby combat forbids using the rootwell as an instant heal");
        }

        public static void AbyssHonorsProtectedMissionTravelLocks(Campaign data)
        {
            var locked = Unlocked(data);
            locked.Enter(6);
            Check(locked.save.room == 6, "The protected-mission fixture starts in the unlocked highland region");

            FieldInfo tuning = typeof(GameCore).GetField("stormTuning",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(tuning != null, "The civic-grid tuning state remains an explicit runtime field");
            tuning.SetValue(locked, true);

            int room = locked.save.room, floor = locked.save.abyssFloor;
            int seed = locked.save.abyssSeed, coins = locked.save.runCoins;
            int shards = locked.save.runShards, xp = locked.save.runXp;
            int revision = locked.revision;
            locked.Enter(0);
            Check(locked.save.room == room && (bool)tuning.GetValue(locked) &&
                locked.toast.Contains("调谐"),
                "Portal/map travel preserves protected civic-grid tuning and explains the lock");
            locked.ReturnToTown();
            Check(locked.save.room == room && (bool)tuning.GetValue(locked) &&
                locked.toast.Contains("调谐"),
                "Quick return preserves protected civic-grid tuning and explains the lock");
            locked.StartAbyss(1);
            Check(locked.save.room == room && locked.save.abyssFloor == floor &&
                locked.save.abyssSeed == seed && locked.save.runCoins == coins &&
                locked.save.runShards == shards && locked.save.runXp == xp &&
                (bool)tuning.GetValue(locked) && locked.revision == revision,
                "Protected civic-grid tuning blocks Abyss entry without mutating the live mission or expedition save");
            Check(locked.toast.Contains("调谐"),
                "Blocked Abyss entry identifies the live civic-grid tuning mission");

            var cancelable = Unlocked(data);
            cancelable.Enter(6);
            cancelable.racing = true;
            cancelable.StartAbyss(1);
            Check(cancelable.InAbyss && cancelable.save.room == 8 &&
                cancelable.save.abyssFloor == 1 && cancelable.save.abyssRisk == 1 &&
                !cancelable.racing,
                "A skiff race remains cancelable by travel and does not block a valid Abyss expedition");

            var cancelableMap = Unlocked(data);
            cancelableMap.Enter(6);
            cancelableMap.racing = true;
            cancelableMap.raceTime = 12f;
            cancelableMap.raceCheckpoint = 2;
            int mapCoins = cancelableMap.save.coins;
            cancelableMap.Enter(0);
            Check(cancelableMap.save.room == 0 && !cancelableMap.racing &&
                cancelableMap.raceTime == 0 && cancelableMap.raceCheckpoint == 0 &&
                cancelableMap.save.coins == mapCoins,
                "Ordinary map travel cancels an unfinished skiff race without awarding its completion reward");

            var cancelableFastReturn = Unlocked(data);
            cancelableFastReturn.Enter(6);
            cancelableFastReturn.enemies.Clear();
            cancelableFastReturn.projectiles.Clear();
            cancelableFastReturn.hazards.Clear();
            cancelableFastReturn.racing = true;
            cancelableFastReturn.raceTime = 12f;
            cancelableFastReturn.raceCheckpoint = 2;
            int returnCoins = cancelableFastReturn.save.coins;
            Check(cancelableFastReturn.SafeTravel,
                "The fast-return fixture has no nearby enemy, hostile projectile or live hazard");
            cancelableFastReturn.ReturnToTown();
            Check(cancelableFastReturn.save.room == 0 && !cancelableFastReturn.racing &&
                cancelableFastReturn.raceTime == 0 && cancelableFastReturn.raceCheckpoint == 0 &&
                cancelableFastReturn.save.coins == returnCoins,
                "Quick return cancels an unfinished skiff race without awarding its completion reward");
        }

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
