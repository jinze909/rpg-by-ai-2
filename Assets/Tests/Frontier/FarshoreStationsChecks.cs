using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FarshoreStationNetworkProvidesSafeUnlockAwareTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7" })
                g.save.flags.Add(flag);
            g.Enter(5); g.enemies.Clear();
            WorldObject harbor = Object(g, "fieldstation5");
            g.save.x = harbor.x; g.save.y = harbor.y;
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_6"),
                "Travel options remain locked until the entire station network is restored");
            g.Close();
            g.save.flags.Add("fieldstation_complete");
            g.Talk(harbor);
            Check(Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_6") &&
                Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_7"),
                "Completed harbor station advertises both regional connections");
            g.Close();
            g.enemies.Add(new Enemy(450, 0, g.save.x + .8f, g.save.y, 70));
            Action(g, harbor.kind, "fieldstation_to_6");
            Check(g.save.room == 5, "Nearby hostiles disable cross-region station travel");
            g.enemies.Clear();
            Action(g, harbor.kind, "fieldstation_to_6");
            Check(g.save.room == 6 && g.CanStand(g.Position) &&
                (g.Position - new Point(Object(g, "fieldstation6").x,
                    Object(g, "fieldstation6").y)).Length <= 1.5f,
                "Harbor link reaches a walkable landing beside the actual desert shelter");
            foreach (Enemy enemy in g.enemies)
                Check((enemy.Position - g.Position).Length >= 2.0f,
                    "No active patrol may occupy the destination landing");

            var cooler = Object(g, "fieldstation6");
            g.save.x = cooler.x; g.save.y = cooler.y; g.enemies.Clear();
            Action(g, cooler.kind, "fieldstation_to_7");
            var heater = Object(g, "fieldstation7");
            Check(g.save.room == 7 && g.CanStand(g.Position) &&
                (g.Position - new Point(heater.x, heater.y)).Length <= 1.5f,
                "Desert network reaches a safe Frostchime station arrival");

            g.save.x = heater.x; g.save.y = heater.y; g.enemies.Clear();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.enemies.Clear();
            Action(restored, "fieldstation7", "fieldstation_to_5");
            Check(restored.save.room == 5 && restored.Has("fieldstation_complete") &&
                restored.CanStand(restored.Position),
                "Network travel persists after old-schema save/load without duplicate rewards");
        }

        public static void FarshoreWeatherStationsChangeActualClimateAndTide(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10;
            Check(g.GetQuest("stations").locked, "Farshore stations must not appear as unlocked before the expedition");
            g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report" })
                g.save.flags.Add(flag);
            Check(!g.GetQuest("stations").locked, "Starting the farshore expedition unlocks station guidance");
            int oldCoins = g.save.coins, oldShards = g.save.shards, oldPoints = g.save.skillPoints;
            g.Enter(5); g.enemies.Clear();
            WorldObject tidewatch = Object(g, "fieldstation5");
            g.save.x = tidewatch.x; g.save.y = tidewatch.y;
            Check(g.CanStand(g.Position) && g.Nearest() == tidewatch,
                "Mistport tidewatch is a real reachable interaction");
            g.save.inventory[0] = 4; g.save.inventory[4] = 4;
            g.save.totalTime = 27; // high tide
            Action(g, tidewatch.kind, "fieldstation_repair_5");
            Check(!g.Has("fieldstation_5") && g.save.inventory[0] == 4 &&
                g.save.inventory[4] == 4, "High tide blocks construction without spending resources");
            g.save.totalTime = 0;
            Action(g, tidewatch.kind, "fieldstation_repair_5");
            Check(g.Has("fieldstation_5") && g.save.inventory[0] == 3 &&
                g.save.inventory[4] == 3, "Low tide repairs the harbor post at its advertised cost");
            g.save.totalTime = 27;
            Check(!g.LowTide, "The harbor would normally be at high tide");
            Action(g, tidewatch.kind, "fieldstation_tide");
            Check(g.LowTide && g.save.tideHeldUntil >= g.save.totalTime + 44.9f,
                "Repaired tidewatch really opens the shoals for the promised duration");

            g.Enter(6); g.enemies.Clear();
            WorldObject cooler = Object(g, "fieldstation6");
            g.save.x = cooler.x; g.save.y = cooler.y;
            Check(g.CanStand(g.Position) && g.Nearest() == cooler,
                "Desert cooler is a usable object and not hidden in a wall");
            g.save.inventory[1] = 3; g.save.inventory[4] = 3;
            Action(g, cooler.kind, "fieldstation_repair_6");
            Check(!g.Has("fieldstation_6") && g.save.inventory[1] == 3,
                "Unlit mirror channel prevents the desert repair without wasting a crystal");
            g.save.flags.Add("mirrors_lit");
            Action(g, cooler.kind, "fieldstation_repair_6");
            Check(g.Has("fieldstation_6") && g.save.inventory[1] == 2 &&
                g.save.inventory[4] == 2 && g.NearActiveFieldStation(6, 4.2f),
                "Completed light puzzle powers an actual local cooling refuge");
            g.save.heat = 60; g.Tick(.05f, new Point());
            Check(g.save.heat < 60, "Standing at the repaired desert cooler reduces real heat");
            g.save.x = 5; g.save.y = 14; g.save.heat = 60;
            g.Tick(.05f, new Point());
            Check(g.save.heat > 60, "Heat still accumulates in the open desert beyond the safe zone");

            g.Enter(7); g.enemies.Clear();
            WorldObject heater = Object(g, "fieldstation7");
            g.save.x = heater.x; g.save.y = heater.y;
            Check(g.CanStand(g.Position) && g.Nearest() == heater,
                "Highland shelter is reachable from the map path");
            g.save.inventory[0] = 4; g.save.inventory[2] = 2;
            Action(g, heater.kind, "fieldstation_repair_7");
            Check(!g.Has("fieldstation_7") && g.save.inventory[0] == 4,
                "The highland station needs a real lit brazier before accepting materials");
            g.save.flags.Add("warm_brazier0");
            Action(g, heater.kind, "fieldstation_repair_7");
            Check(g.Has("fieldstation_7") && g.save.inventory[0] == 3 &&
                g.save.inventory[2] == 1 && g.NearActiveFieldStation(7, 4.2f),
                "Western brazier powers a local highland shelter at exact cost");
            g.save.cold = 60; g.Tick(.05f, new Point());
            Check(g.save.cold < 60, "The repaired shelter actually reduces cold exposure");
            g.save.x = 30; g.save.y = 14; g.save.cold = 60;
            g.Tick(.05f, new Point());
            Check(g.save.cold > 60, "Highland cold still accumulates outside shelter and braziers");
            Check(g.Has("fieldstation_complete") && g.GetQuest("stations").complete &&
                g.save.coins == oldCoins + 54 && g.save.shards == oldShards + 6 &&
                g.save.skillPoints == oldPoints + 1 && g.save.tea >= 2,
                "Three stations pay unique per-site rewards and the one-time final bonus");
            int coins = g.save.coins, skills = g.save.skillPoints;
            g.Talk(heater);
            Check(g.conversation != null &&
                !Array.Exists(g.conversation.choices, choice => choice.action == "fieldstation_repair_7"),
                "Finished shelter offers information, not duplicate construction");
            g.Close(); g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("stations").complete && restored.Has("fieldstation_7") &&
                restored.save.coins == coins && restored.save.skillPoints == skills &&
                restored.NearActiveFieldStation(7, 4.2f) == false,
                "Old-schema save retains rebuilt stations without inventing nearby-player state");
            restored.save.x = heater.x; restored.save.y = heater.y;
            Check(restored.NearActiveFieldStation(7, 4.2f),
                "The environment shelter works again when a player revisits after loading");
        }
    }
}
