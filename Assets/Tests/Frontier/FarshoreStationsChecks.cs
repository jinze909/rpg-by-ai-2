using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {

        private static void StartCivilianRescueViaRealDialogue(GameCore game, WorldObject station)
        {
            Action(game, station.kind, "fieldstation_rescue_menu");
            Check(game.conversation != null, "Rescue operations open a real submenu");
            int next = -1;
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == "fieldstation_rescue_start")
                    { next = i; break; }
            Check(next >= 0, "The actual submenu exposes the rescue start action");
            game.Choose(next);
        }

        public static void RescueSurvivorsRequireRealProtectionAndSafeTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_6", "farshore_relief_6", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 3; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            Check(g.reliefMissionRoom == 6, "Rescue starts from the real rebuilt station");
            int coins = g.save.coins;
            g.ReturnToTown();
            Check(g.save.room == 6 && g.reliefMissionRoom == 6,
                "Fast return to town cannot silently abandon a civilian mission");
            Action(g, station.kind, "fieldstation_to_5");
            Check(g.save.room == 6 && g.reliefMissionRoom == 6,
                "Regional station travel preserves a live rescue rather than discarding it");
            g.save.flags.Remove("fieldstation_survey_6");
            Action(g, station.kind, "fieldstation_survey_start");
            g.save.flags.Add("fieldstation_survey_6");
            Check(g.fieldSurveyRoom == 0 && g.reliefMissionRoom == 6,
                "Another timed survey cannot replace the active rescue timer");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.reliefMissionWave == 3 && g.reliefFollowerSafety == 100,
                "Civilians start the physical return with full temporary safety");
            Point follower = g.reliefFollowerPosition;
            g.save.x = station.x; g.save.y = station.y;
            g.enemies.Add(new Enemy(590, 0, follower.x, follower.y, 50));
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < 100 && g.reliefMissionRoom == 6 &&
                !g.Has("relief_rescue_6"),
                "Actual surviving patrols threaten the rescued civilian rather than generating passive completion");
            g.reliefFollowerSafety = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.Has("relief_rescue_6") &&
                g.save.coins == coins,
                "Losing civilian safety cancels the escort without permanent progress or a paid reward");
        }

        public static void FarshoreEvacuationHasRealCombatAndPersistentFirstClear(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10; g.save.stage = 3;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_5", "fieldstation_survey_6", "fieldstation_survey_7",
                "fieldstation_survey_complete", "farshore_relief_5", "farshore_relief_6",
                "farshore_relief_7", "farshore_relief_complete" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 3;
            Check(!g.GetQuest("relief_rescue").locked && g.GetQuest("relief_rescue").steps.Count == 4,
                "The real regional mission appears only after the playable shelter network exists");
            int coins = g.save.coins, shards = g.save.shards, talents = g.save.skillPoints;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                g.save.x = station.x; g.save.y = station.y;
                Action(g, station.kind, "fieldstation_rescue_menu");
                Check(g.conversation != null &&
                    Array.Exists(g.conversation.choices, c => c.action == "fieldstation_rescue_start"),
                    "The physical shelter exposes a live, playable rescue action");
                g.Close();
                StartCivilianRescueViaRealDialogue(g, station);
                Check(g.reliefMissionRoom == region && g.reliefMissionWave == 1 &&
                    g.reliefMissionTime > 119f && g.save.trackedQuest == "relief_rescue" &&
                    g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 2,
                    "Each region spawns two genuine AI combatants and a 120-second objective");
                foreach (Enemy enemy in g.enemies)
                    if (enemy.id >= 85000 && enemy.id < 85100)
                        Check(enemy.timer < 2 && enemy.hp > 0 &&
                            g.CanStand(enemy.Position) && g.NavigationPath(enemy.Position).Count > 0,
                            "Relief attackers have real health, bounded opening timers and reachable spawns");
                Check(g.GetQuest("relief_rescue").room == region &&
                    g.GetQuest("relief_rescue").next.Contains("120s"),
                    "The journal directs the player to the active rescue while displaying remaining time");
                // Clear through normal world enemy removal as though both foes were
                // defeated; this checks the production wave-and-reward state machine.
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(g.reliefMissionWave == 2 &&
                    g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 2,
                    "The first defeat opens a second real wave rather than instantly paying a reward");
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(g.reliefMissionWave == 3 && !g.Has("relief_rescue_" + region),
                    "Killing all raiders still requires actually returning survivors to the shelter");
                Point savedSurvivor = g.reliefFollowerPosition;
                g.save.x = station.x; g.save.y = station.y;
                g.Tick(.05f, new Point());
                Check(g.reliefMissionWave == 3 && g.reliefMissionRoom == region &&
                    (g.reliefFollowerPosition - savedSurvivor).Length > 0,
                    "The rescued civilian physically follows the hero and cannot teleport to the station");
                g.reliefFollowerPosition = new Point(station.x, station.y);
                g.Tick(.05f, new Point());
                Check(g.reliefMissionRoom == 0 && g.Has("relief_rescue_" + region) &&
                    g.save.coins == coins + (region - 4) * 45,
                    "Real return completes and permanently records the rescued civilians");
            }
            Check(g.Has("relief_rescue_complete") &&
                g.save.coins == coins + 135 && g.save.shards == shards + 9 &&
                g.save.skillPoints == talents + 1 &&
                g.GetQuest("relief_rescue").complete,
                "Three non-repeatable local clear rewards unlock the final civic rescue bonus");
            WorldObject finalStation = Object(g, "fieldstation7");
            g.save.x = finalStation.x; g.save.y = finalStation.y;
            int rewarded = g.save.coins, rewardedShards = g.save.shards, rewardedPoints = g.save.skillPoints;
            StartCivilianRescueViaRealDialogue(g, finalStation);
            Check(g.reliefMissionRoom == 7, "Completed civilian rescue remains replayable as live combat");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point()); g.reliefFollowerPosition = new Point(finalStation.x, finalStation.y);
            g.Tick(.05f, new Point());
            Check(g.save.coins == rewarded && g.save.shards == rewardedShards &&
                g.save.skillPoints == rewardedPoints,
                "Replaying civilian rescue yields no coins, shards or repeat talent");
            StartCivilianRescueViaRealDialogue(g, finalStation);
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.enemies.Exists(e => e.id >= 85000 && e.id < 85100),
                "Timed-out rescues clean up all transient special combatants");
            StartCivilianRescueViaRealDialogue(g, finalStation);
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.reliefMissionRoom == 0 &&
                !restored.enemies.Exists(e => e.id >= 85000 && e.id < 85100) &&
                restored.GetQuest("relief_rescue").complete &&
                restored.save.coins == rewarded,
                "Old-schema save reload keeps completed rescues without restoring unfinished raider waves");
        }

        public static void CivilianReliefActuallyHealsAndProtectsOuterClimate(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "farshore_relief_5", "farshore_relief_6", "farshore_relief_7", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 1;
            int coins = g.save.coins;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                g.save.x = station.x; g.save.y = station.y; g.save.health = 40;
                for (int frame = 0; frame < 20; frame++) g.Tick(.05f, new Point());
                Check(g.save.health > 41 && g.save.health < 42 && g.save.coins == coins,
                    "Each built civilian refuge offers slow actual health recovery without passive currency");
                float beforeCombat = g.save.health;
                g.enemies.Add(new Enemy(260 + region, 0, station.x + 1, station.y, 70));
                g.Tick(.05f, new Point());
                Check(g.save.health <= beforeCombat + .0001f,
                    "Nearby combat disables field healing even while standing inside a completed shelter");
                g.enemies.Clear();
                g.save.health = 40;
                g.save.flags.Remove("farshore_relief_" + region);
                g.Tick(.05f, new Point());
                Check(g.save.health <= 40.0001f,
                    "The old station without a civilian upgrade cannot grant field hospital recovery");
                g.save.flags.Add("farshore_relief_" + region);
                g.save.health = g.save.maxHealth - .02f;
                g.Tick(.05f, new Point());
                Check(g.save.health <= g.save.maxHealth,
                    "Recovery never exceeds the saved hero's real health maximum");
            }
            for (int region = 6; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                Point anchor = new Point(station.x, station.y);
                Point edge = anchor;
                bool found = false;
                Point[] offsets = { new Point(5, 0), new Point(-5, 0),
                    new Point(0, 5), new Point(0, -5),
                    new Point(3.5f, 3.5f), new Point(-3.5f, -3.5f),
                    new Point(3.5f, -3.5f), new Point(-3.5f, 3.5f) };
                foreach (Point shift in offsets)
                {
                    Point sample = anchor + shift;
                    if (!g.CanStand(sample)) continue;
                    if (region == 6)
                    {
                        bool nearOasis = false;
                        foreach (WorldObject shade in g.Map.objects)
                            if (shade.kind == "shade" &&
                                (new Point(shade.x, shade.y) - sample).Length < 3) nearOasis = true;
                        if (nearOasis) continue;
                    }
                    edge = sample; found = true; break;
                }
                Check(found && (edge - anchor).Length > 4.2f &&
                    (edge - anchor).Length < g.FieldStationRefugeRadius(region),
                    "The authored farshore map has reachable ground in the newly protected outer band");
                g.save.x = edge.x; g.save.y = edge.y;
                g.save.heat = region == 6 ? 50 : 0;
                g.save.cold = region == 7 ? 50 : 0;
                g.Tick(.05f, new Point());
                Check(region == 6 ? g.save.heat < 50 : g.save.cold < 50,
                    "The full expanded refuge radius changes the live heat/cold simulation");
                g.save.flags.Remove("farshore_relief_" + region);
                g.save.heat = region == 6 ? 50 : 0;
                g.save.cold = region == 7 ? 50 : 0;
                g.Tick(.05f, new Point());
                Check(region == 6 ? g.save.heat > 50 : g.save.cold > 50,
                    "Without the built upgrade, identical outer-band ground remains exposed");
                g.save.flags.Add("farshore_relief_" + region);
            }
        }

        public static void FarshoreReliefRoutesLinkExistingInnAndStations(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "farshore_relief_5", "farshore_relief_6", "farshore_relief_7", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 2;
            int coins = g.save.coins, skill = g.save.skillPoints;
            g.Enter(9); g.enemies.Clear();
            WorldObject desk = Object(g, "surveyDesk");
            g.save.x = desk.x; g.save.y = desk.y;
            Check(!g.Has("survey_complete"),
                "Civilian refuge travel does not depend on an unrelated completed warden chart");
            Action(g, desk.kind, "farshore_relief_routes");
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices, c => c.action == "farshore_relief_to_7"),
                "The actual Hearth Inn chart opens a separate three-destination relief submenu");
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "farshore_relief_to_6")
                { g.Choose(i); break; }
            Check(g.save.room == 6 && g.CanStand(g.Position) &&
                (g.Position - new Point(Object(g, "fieldstation6").x,
                    Object(g, "fieldstation6").y)).Length <= 1.5f,
                "Inn convoy reaches the desert station at a safe walkable adjacent landing");
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            g.enemies.Add(new Enemy(58, 0, post.x + 1, post.y, 50));
            Action(g, post.kind, "fieldstation_inn");
            Check(g.save.room == 6, "Live enemies prevent instant escape along civilian supply routes");
            g.enemies.Clear();
            Action(g, post.kind, "fieldstation_inn");
            Check(g.save.room == 9 && g.CanStand(g.Position) &&
                g.save.coins == coins && g.save.skillPoints == skill,
                "Relief route returns to the real Inn without awarding duplicate currency or talents");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            WorldObject savedDesk = Object(resumed, "surveyDesk");
            resumed.save.x = savedDesk.x; resumed.save.y = savedDesk.y;
            Action(resumed, savedDesk.kind, "farshore_relief_routes");
            Check(Array.Exists(resumed.conversation.choices, c => c.action == "farshore_relief_to_5"),
                "Existing save flags preserve the additional rescue routes after loading");
            var locked = Fresh(data); locked.save.stage = 1; locked.save.frontierEnding = 1;
            foreach (string flag in new[] { "farshore_relief_complete", "fieldstation_7" })
                locked.save.flags.Add(flag);
            locked.Enter(9); locked.enemies.Clear();
            WorldObject restrictedDesk = Object(locked, "surveyDesk");
            locked.save.x = restrictedDesk.x; locked.save.y = restrictedDesk.y;
            Action(locked, restrictedDesk.kind, "farshore_relief_routes");
            for (int i = 0; i < locked.conversation.choices.Length; i++)
                if (locked.conversation.choices[i].action == "farshore_relief_to_7")
                { locked.Choose(i); break; }
            Check(locked.save.room == 9 && !locked.CanEnter(7),
                "Malformed or early save flags never bypass the real Frostchime chapter gate");
        }

        public static void FarshoreReliefWorksProvidePersistentClimateSanctuaries(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10; g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_5", "fieldstation_survey_6", "fieldstation_survey_7",
                "fieldstation_survey_complete" }) g.save.flags.Add(flag);
            Check(g.GetQuest("farshore_relief").locked && g.TidewatchHoldSeconds == 45 &&
                g.FieldStationRefugeRadius(6) == 4.2f,
                "New relief work stays locked before governance, keeping original station balance");
            g.Enter(5); g.enemies.Clear();
            WorldObject harbor = Object(g, "fieldstation5");
            g.save.x = harbor.x; g.save.y = harbor.y;
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_relief_5"),
                "Before the covenant, the harbor station does not advertise a future upgrade");
            g.Close(); g.save.frontierEnding = 2;
            Check(!g.GetQuest("farshore_relief").locked && g.GetQuest("farshore_relief").steps.Count == 4,
                "Council governance unlocks a three-region construction quest");
            g.save.inventory[0] = 6; g.save.inventory[4] = 3;
            int startWood = g.save.inventory[0], startGear = g.save.inventory[4];
            g.save.totalTime = 28;
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(!g.Has("farshore_relief_5") && g.save.inventory[0] == startWood &&
                g.save.inventory[4] == startGear,
                "Unsafe high tide blocks actual pontoon construction without taking supplies");
            g.save.totalTime = 0;
            g.enemies.Add(new Enemy(47, 0, harbor.x + 1, harbor.y, 50));
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(!g.Has("farshore_relief_5") && g.save.inventory[0] == startWood,
                "An enemy close to construction blocks the upgrade and preserves supplies");
            g.enemies.Clear();
            int coins = g.save.coins, shards = g.save.shards, talents = g.save.skillPoints;
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(g.Has("farshore_relief_5") && g.TidewatchHoldSeconds == 90 &&
                g.save.inventory[0] == startWood - 2 && g.save.inventory[4] == startGear - 1 &&
                g.save.coins == coins + 30 && g.save.shards == shards + 2,
                "Survey-backed harbor pontoons consume their cost once and double the real ebb window");
            g.save.totalTime = 30; Action(g, harbor.kind, "fieldstation_tide");
            Check(g.save.tideHeldUntil >= 119.9f && g.LowTide,
                "Upgraded tidewatch holds flooded shoals open for ninety actual game seconds");
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_relief_5"),
                "Finished harbor shelter cannot replay its construction reward");
            g.Close();
            g.Enter(6); g.enemies.Clear();
            WorldObject desert = Object(g, "fieldstation6"); g.save.x = desert.x; g.save.y = desert.y;
            g.save.inventory[1] = 4; g.save.inventory[2] = 4;
            int crystals = g.save.inventory[1], herbs = g.save.inventory[2];
            Action(g, desert.kind, "fieldstation_relief_6");
            Check(g.Has("farshore_relief_6") && g.save.inventory[1] == crystals - 2 &&
                g.save.inventory[2] == herbs - 1 && g.FieldStationRefugeRadius(6) == 6.5f,
                "Shaded desert supply lane consumes real resources and expands the heat refuge");
            g.Enter(7); g.enemies.Clear();
            WorldObject mountain = Object(g, "fieldstation7"); g.save.x = mountain.x; g.save.y = mountain.y;
            g.save.inventory[0] = 3; g.save.inventory[2] = 4;
            Action(g, mountain.kind, "fieldstation_relief_7");
            Check(g.Has("farshore_relief_7") && g.Has("farshore_relief_complete") &&
                g.FieldStationRefugeRadius(7) == 6.5f && g.save.inventory[0] == 2 &&
                g.save.inventory[2] == 2 && g.save.skillPoints == talents + 1 &&
                g.GetQuest("farshore_relief").complete,
                "Last warming depot builds the final physical shelter and pays one completion talent");
            int totalCoins = g.save.coins, totalShards = g.save.shards;
            g.Close(); g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("farshore_relief").complete &&
                restored.TidewatchHoldSeconds == 90 &&
                restored.FieldStationRefugeRadius(6) == 6.5f &&
                restored.FieldStationRefugeRadius(7) == 6.5f &&
                restored.save.coins == totalCoins && restored.save.shards == totalShards,
                "All physical climate upgrades and rewards survive old-schema save reload");
        }

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
                // Traveling through a linked station must not erase a live
                // out-and-back survey without the player deliberately canceling.
                int otherRegion = region == 5 ? 6 : 5;
                float surveyTimeBefore = g.fieldSurveyTime;
                Action(g, post.kind, "fieldstation_to_" + otherRegion);
                Check(g.save.room == region && g.fieldSurveyRoom == region &&
                    g.fieldSurveyPhase == 1 && g.fieldSurveyTime == surveyTimeBefore,
                    "Linked station travel cannot silently destroy an unfinished regional survey");
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
            // Completing the full circuit unlocks an actual forecast, not just flavor text.
            g.Enter(5); g.enemies.Clear();
            WorldObject weatherPost = Object(g, "fieldstation5");
            g.save.x = weatherPost.x; g.save.y = weatherPost.y;
            g.save.totalTime = 27; // high tide; next natural ebb in 9 seconds
            Action(g, weatherPost.kind, "fieldstation_forecast");
            Check(g.mode == "dialog" && g.conversation != null &&
                g.conversation.textEn.Contains("9s") &&
                g.conversation.textEn.Contains("1.3/s") &&
                g.conversation.textEn.Contains("4.2-tile"),
                "All three field reports unlock a real tide and climate forecast at any restored station");
            g.Close();
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
