using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FullInventoryAndWalletRemainStableThroughActualEnemyLoot(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999999;g.save.inventory[2]=9999;g.save.kills[1]=99999;
            int xp=g.save.xp;
            var raider=new Enemy(765,0,13,14,1) { stun=99 };
            g.enemies.Add(raider);
            g.Attack(new Point(1,0));
            Advance(g,.45f);
            Check(!g.enemies.Contains(raider) && g.save.xp>xp &&
                g.effects.Exists(e=>e.kind=="loot"),
                "A full bag must not suppress a real enemy defeat, XP or contact");
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Actual combat rewards cannot exceed the on-disk coin, item and kill caps");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.coins==999999 && loaded.save.inventory[2]==9999 &&
                loaded.save.kills[1]==99999,
                "A full reward state roundtrips without hidden reload-only loss");
            // Partial capacity still awards the usual loot exactly once.
            g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999998;g.save.inventory[2]=9998;g.save.kills[1]=99998;
            g.enemies.Add(new Enemy(766,0,13,14,1){stun=99});
            g.Attack(new Point(1,0));Advance(g,.45f);
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Last available resource slots are filled, never skipped or overrun");
        }

        public static void BoundedEnemyOpeningDelaysAndRestoredPatrols(Campaign data)
        {
            // The original early patrol rhythm must remain unchanged.
            for (int id = 1; id <= 8; id++)
            {
                Enemy early = new Enemy(id, id % 3, 14, 14, 40);
                Check(Math.Abs(early.timer - (1.3f + id * .13f)) < .0001f,
                    "Original early patrol opening cadence remains unchanged");
            }
            // IDs of snapshots and authored encounters are identifiers, not seconds.
            foreach (int id in new[] { 9, 450, 1905, 81012, 82032, 83001, 99999 })
            {
                Enemy candidate = new Enemy(id, 0, 14, 14, 50);
                Check(candidate.timer >= 1.42f && candidate.timer <= 2.35f,
                    "Large ID " + id + " must not delay its first attack for minutes or hours");
            }
            var g = Fresh(data); g.save.stage = 3; g.Enter(3);
            g.enemies.Clear(); g.PrepareSave();
            g.save.snapshotRoom = 3;
            g.save.worldEnemies = new[] {
                new EnemyRecord { id = 50000, kind = 1, x = 9.5f, y = 14.5f,
                    hp = 44, maxHp = 44, boss = false }
            };
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Enemy patrol = loaded.enemies.Find(e => e.id == 50000);
            Check(patrol != null && patrol.timer >= 1.42f && patrol.timer <= 2.35f,
                "Loading a high-ID patrol restores a playable opening attack delay");
        }

        public static void EliteVictoryRetiresWarningsBeforeNextDamageStep(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            g.save.flags.Add("archive_complete");
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(echo != null, "Atlas echo is actually spawned by the story altar");
            var echoWarning = new Hazard { position = g.Position, radius = 1.2f,
                damage = 999, timer = .01f, kind = "bell", atlasEchoOwned = true };
            var unrelated = new Hazard { position = new Point(4, 4), radius = .2f,
                damage = 1, timer = 10, kind = "root" };
            g.hazards.Add(echoWarning); g.hazards.Add(unrelated);
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hit.Invoke(g, new object[] { echo, 9999f, false, null, null });
            Check(!g.enemies.Contains(echo) &&
                !g.hazards.Exists(h => h.atlasEchoOwned) &&
                g.hazards.Contains(unrelated) &&
                g.AtlasEchoOpenTime == 0,
                "The final actual combat hit retires imminent echo hazards immediately, without erasing world hazards");
            g.save.reducedMotion = true;
            for (int frame = 0; frame < 6; frame++) g.Tick(.05f, new Point());
            Check(g.Has("archive_echo_complete"),
                "The same-frame safety cleanup still allows a proper one-time echo clear");
        }

        public static void OptionalEncountersNeverStartInsideEachOther(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            foreach (string flag in new[] { "archive_complete", "scout_saved",
                "dispatch_complete", "survey_3" }) g.save.flags.Add(flag);
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(g.AtlasEchoRoom == 3 && echo != null, "Elite challenge begins normally");
            // Move the echo away so that SafeTravel alone would allow another
            // encounter. The explicit mutex must still deny it.
            echo.x = 5; echo.y = 5;
            WorldObject beacon = Object(g, "survey3");
            g.save.x = beacon.x; g.save.y = beacon.y;
            Action(g, beacon.kind, "road_trial_start_3");
            Check(g.RoadwardenTrialRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                "Beacon defense cannot start while the atlas echo is active");
            WorldObject traveler = Object(g, "wayfarer3");
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, traveler.kind, "wayfarer_defend_3");
            Check(g.WayfarerEncounterRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Roadside ambush cannot start during an atlas echo challenge");
        }
    }
}
