using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void SurveyorEchoCombatHasWarningsAndSafeRewards(Campaign data)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Echo fight uses the production damage pipeline");
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            g.Enter(3); g.enemies.Clear();
            WorldObject compass = Object(g, "archiveecho");
            g.save.x = compass.x; g.save.y = compass.y;
            Check(g.CanStand(g.Position) && g.Nearest() == compass,
                "The new echo altar is a real reachable interaction");
            Check(g.GetQuest("archive_echo").locked,
                "Echo combat is gated behind the full atlas story");
            g.Talk(compass);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_echo_start"),
                "Echo cannot start before the atlas is restored");
            g.Close(); g.save.flags.Add("archive_complete");
            g.enemies.Add(new Enemy(450, 0, g.save.x + .8f, g.save.y, 40));
            Action(g, compass.kind, "archive_echo_start");
            Check(g.AtlasEchoRoom == 0 && !g.enemies.Exists(e => e.id == 83001),
                "Nearby live patrols block the optional encounter");
            g.enemies.Clear();
            int coins = g.save.coins, shards = g.save.shards, kills = g.save.kills[3];
            g.save.maxHealth = 2000; g.save.health = 2000;
            Action(g, compass.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(g.AtlasEchoRoom == 3 && echo != null && echo.maxHp == 220 &&
                echo.timer > 0 && echo.timer < 2 && g.CanStand(echo.Position) &&
                g.NavigationPath(echo.Position).Count > 0,
                "A real armored elite starts in normal attack time on navigable ground");
            bool sawWarnings = false;
            for (int frame = 0; frame < 70; frame++)
            {
                g.Tick(.05f, new Point());
                if (g.hazards.Exists(h => h.atlasEchoOwned)) sawWarnings = true;
            }
            Check(sawWarnings, "Echo repeatedly emits live delayed three-direction attack warnings");
            g.save.x = compass.x; g.save.y = compass.y;
            echo.hp = 78;
            typeof(GameCore).GetField("atlasEchoPulseTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(g, .01f);
            g.Tick(.05f, new Point());
            Check(g.hazards.Exists(h => h.atlasEchoOwned && h.kind == "rift" &&
                h.timer > 2f), "Low-health echo warns a delayed center pulse");
            Check(!g.Has("archive_echo_complete"),
                "The encounter must not grant rewards before victory");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.AtlasEchoRoom == 0 &&
                !resumed.enemies.Exists(e => e.id == 83001) &&
                !resumed.Has("archive_echo_complete"),
                "Saving during battle cannot persist phantom elite enemies");
            g.hazards.Add(new Hazard { position = new Point(4, 4),
                radius = .3f, timer = 4, damage = 1, kind = "root" });
            hit.Invoke(g, new object[] { echo, 9999f, false, null, null });
            Check(g.save.kills[3] == kills && g.save.coins == coins,
                "The encounter cannot farm normal chapter kills or drops");
            for (int frame = 0; frame < 12; frame++) g.Tick(.05f, new Point());
            Check(g.AtlasEchoRoom == 0 && g.Has("archive_echo_complete") &&
                g.GetQuest("archive_echo").complete &&
                g.save.coins == coins + 85 && g.save.shards == shards + 5,
                "First victory grants exactly one authored reward");
            Check(!g.hazards.Exists(h => h.atlasEchoOwned) &&
                g.hazards.Exists(h => h.kind == "root"),
                "Victory clears only echo hazards, not unrelated environmental danger");
            g.Talk(compass);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_echo_start"),
                "Completed elite cannot be used to farm first-clear prizes");
            g.Close(); g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.GetQuest("archive_echo").complete &&
                loaded.save.coins == g.save.coins,
                "Elite clear and payout persist in the existing save format");

            var retry = Fresh(data); retry.save.stage = 3; retry.save.flags.Add("archive_complete");
            retry.Enter(3); retry.enemies.Clear();
            var marker = Object(retry, "archiveecho");
            retry.save.x = marker.x; retry.save.y = marker.y;
            Action(retry, marker.kind, "archive_echo_start");
            Check(retry.AtlasEchoRoom == 3, "Trial can be started on a fresh saved atlas");
            retry.hazards.Add(new Hazard { position = retry.Position, radius = .5f,
                timer = 2, damage = 5, kind = "bell", atlasEchoOwned = true });
            Action(retry, marker.kind, "archive_echo_abandon");
            Check(retry.AtlasEchoRoom == 0 && !retry.Has("archive_echo_complete") &&
                !retry.enemies.Exists(e => e.id == 83001) &&
                !retry.hazards.Exists(h => h.atlasEchoOwned),
                "Abandonment removes owned hazards and enemies without prizes");
            Action(retry, marker.kind, "archive_echo_start");
            Check(retry.AtlasEchoRoom == 3, "Free retry remains possible after abandonment");
        }
    }
}
