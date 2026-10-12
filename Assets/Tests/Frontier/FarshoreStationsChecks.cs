using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {

        public static void FarshoreStationSurveysRequireRealOutAndBackTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete" })
                g.save.flags.Add(flag);
            int coins = g.save.coins, shards = g.save.shards, tea = g.save.tea;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject post = Object(g, "fieldstation" + region);
                g.save.x = post.x; g.save.y = post.y;
                Check(g.GetQuest("station_patrol").steps.Count == 4, "Survey journal is available after restoration");
                Action(g, post.kind, "fieldstation_survey_start");
                Check(g.fieldSurveyRoom == region && g.fieldSurveyPhase == 1 &&
                    g.fieldSurveyTime > 89 && g.save.trackedQuest == "station_patrol",
                    "Each repaired station starts a tracked 90-second local field mission");
                g.save.english = true;
                Check(g.Conditions().Contains("SURVEY") && g.Conditions().Contains("90s"),
                    "Active survey countdown stays visible in the conditions HUD");
                g.save.english = false;
                if (region == 6)
                {
                    g.save.x = Object(g, "skiff").x; g.save.y = Object(g, "skiff").y;
                    Action(g, "skiff", "race");
                    Check(!g.racing && g.fieldSurveyRoom == 6,
                        "Starting the sand-skiff race cannot silently overwrite a live field survey");
                }
                Point marker = g.FieldSurveyTarget(region);
                Check(g.CanStand(marker), "Regional survey landmark is on walkable ground");
                g.save.x = marker.x; g.save.y = marker.y;
                if (region == 5)
                {
                    g.save.totalTime = 27;
                    g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Harbor reading is blocked at high tide");
                    g.save.totalTime = 0;
                }
                else if (region == 6)
                {
                    g.save.heat = 80; g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Heat-distorted desert reading cannot advance");
                    g.save.heat = 0;
                }
                else
                {
                    g.save.cold = 80; g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Cold-distorted highland reading cannot advance");
                    g.save.cold = 0;
                }
                g.Tick(.05f, new Point());
                Check(g.fieldSurveyPhase == 2 && g.GetQuest("station_patrol").room == region &&
                    (g.GetQuest("station_patrol").target - new Point(post.x, post.y)).Length < .01f,
                    "Actual landmark contact and safe weather create a return-to-post objective");
                g.save.x = post.x; g.save.y = post.y;
                g.Tick(.05f, new Point());
                Check(g.Has("fieldstation_survey_" + region) && g.fieldSurveyRoom == 0 &&
                    g.save.coins == coins + 24 * (region - 4) &&
                    g.save.shards == shards + 2 * (region - 4),
                    "Returning to the real station archives one regional reading and pays once");
                g.Talk(post);
                Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_survey_start"),
                    "Completed station does not offer farmable repeat rewards");
                g.Close();
            }
            Check(g.Has("fieldstation_survey_complete") && g.GetQuest("station_patrol").complete &&
                g.save.tea == tea + 2, "All three readings grant exactly one network-wide tea bonus");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("station_patrol").complete && restored.fieldSurveyRoom == 0 &&
                restored.save.coins == g.save.coins && restored.save.tea == g.save.tea,
                "Survey flags and one-time rewards persist without new save fields");

            var retry = Fresh(data); retry.save.stage = 3;
            retry.save.flags.Add("exp_intro"); retry.save.flags.Add("fieldstation_5");
            retry.Enter(5); retry.enemies.Clear();
            var origin = Object(retry, "fieldstation5");
            retry.save.x = origin.x; retry.save.y = origin.y;
            Action(retry, origin.kind, "fieldstation_survey_start");
            retry.fieldSurveyTime = .01f; retry.Tick(.05f, new Point());
            Check(retry.fieldSurveyRoom == 0 && !retry.Has("fieldstation_survey_5"),
                "Timed-out survey clears cleanly without granting progress");
            Action(retry, origin.kind, "fieldstation_survey_start");
            retry.Enter(0);
            Check(retry.fieldSurveyRoom == 0 && !retry.Has("fieldstation_survey_5"),
                "Leaving the map cancels transient surveys without losing completed reports");
        }

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
