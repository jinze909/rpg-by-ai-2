using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ScoutPresenceMatchesInteractionAndSavedJourney(Campaign data)
        {
            var g=Fresh(data);WorldObject returned=Object(g,"returnscout");g.save.x=returned.x;g.save.y=returned.y;
            Check(g.Nearest()==null||g.Nearest().kind!="returnscout","The returned scout cannot become an invisible town interaction before the rescue");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"Pressing E at the absent scout cannot credit the resident tutorial");
            g.Talk(returned);Check(g.mode=="world"&&g.conversation==null&&!g.Has("scout_gift"),"Direct talk cannot open an absent returned scout or award thanks");
            g.PrepareSave();var beforeRescue=new GameCore(data,CloneSave(g.save));beforeRescue.Start();
            Check(beforeRescue.Nearest()==null||beforeRescue.Nearest().kind!="returnscout","Loading an unrescued save preserves the empty town position");
            beforeRescue.Interact();Check((beforeRescue.save.tutorialBits&2)==0&&beforeRescue.mode=="world","Loading cannot turn an absent resident into tutorial progress");
            WorldObject lyra=Object(beforeRescue,"guild");beforeRescue.save.x=lyra.x;beforeRescue.save.y=lyra.y;beforeRescue.Interact();
            Check(beforeRescue.mode=="dialog"&&beforeRescue.conversation.speaker==lyra.name&&(beforeRescue.save.tutorialBits&2)!=0,"Lyra's real conversation still completes the resident interaction tutorial");

            g.Enter(1);g.enemies.Clear();g.save.kills[1]=4;WorldObject scout=Object(g,"scout");g.save.x=scout.x;g.save.y=scout.y;
            Check(g.Nearest()==scout,"The stranded scout remains selectable before the rescue");g.Interact();
            Check(g.mode=="dialog"&&g.conversation.speaker==scout.name&&g.conversation.choices.Length==1&&g.conversation.choices[0].action=="scout","Clearing four beasts retains the original rescue conversation");
            int coins=g.save.coins;g.Choose(0);
            Check(g.Has("scout_saved")&&g.GateOpen&&g.save.coins==coins+20,"Rescue still opens the starcore path and awards the original twenty coins");
            g.save.tutorialBits&=~2;
            Check(g.Nearest()==null||g.Nearest().kind!="scout","The rescued scout cannot remain an invisible forest interaction");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"The vacated forest position cannot produce a conversation or phantom tutorial credit");
            g.Talk(scout);Check(g.mode=="world"&&g.conversation==null&&g.save.coins==coins+20,"Talking to the retired forest scout cannot repeat the rescue reward");
            g.PrepareSave();var afterRescue=new GameCore(data,CloneSave(g.save));afterRescue.Start();
            Check(afterRescue.Has("scout_saved")&&(afterRescue.Nearest()==null||afterRescue.Nearest().kind!="scout"),"Loading the rescued forest save keeps the scout absent");
            afterRescue.Interact();Check((afterRescue.save.tutorialBits&2)==0&&afterRescue.mode=="world","Resumed forest interaction respects the saved rescue state");
            afterRescue.Enter(0);returned=Object(afterRescue,"returnscout");afterRescue.save.x=returned.x;afterRescue.save.y=returned.y;
            Check(afterRescue.Nearest()==returned,"The rescued scout is selectable at the real town homecoming position");afterRescue.Interact();
            Check(afterRescue.mode=="dialog"&&afterRescue.conversation.speaker==returned.name&&afterRescue.conversation.choices[0].action=="scoutgift","The homecoming retains the original thank-you conversation");
            int stew=afterRescue.save.inventory[6];afterRescue.Choose(0);
            Check(afterRescue.Has("scout_gift")&&afterRescue.save.inventory[6]==stew+1,"The scout's thanks awards one stew");
            Action(afterRescue,"returnscout","scoutgift");Check(afterRescue.save.inventory[6]==stew+1,"Repeated homecoming conversations cannot duplicate the thank-you gift");
            afterRescue.PrepareSave();var home=new GameCore(data,CloneSave(afterRescue.save));home.Start();
            Check(home.Nearest()!=null&&home.Nearest().kind=="returnscout","Loading the homecoming save keeps the returned scout present");
            Action(home,"returnscout","scoutgift");Check(home.save.inventory[6]==stew+1,"The consumed gift remains consumed after loading");
        }
        public static void TutorialAndPresentationMigration(Campaign data)
        {
            var old = new SaveData { presentationVersion = 0, frontierFocus = true, stage = 1 }; old.flags.Add("intro"); old.flags.Add("exp_intro");
            var resumed = new GameCore(data, old); Check(resumed.save.trackedQuest == "shore" && resumed.TutorialStep == 8, "Existing players keep story focus without forced onboarding");
            var g = Fresh(data); Check(g.TutorialStep == 0, "New games start with guided movement");
            g.mode = "pause"; for (int i = 0; i < 30; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 0 && g.save.tutorialDistance == 0, "Paused movement cannot complete onboarding");
            g.Close(); for (int i = 0; i < 16; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 1 && g.Explored(0, g.save.x, g.save.y), "Actual walking advances onboarding and exploration");
            g.save.x = 18; g.save.y = 16; g.Interact(); Check((g.save.tutorialBits & 2) != 0, "Actual interaction is recorded"); g.Close();
            g.TrackQuest("main"); PracticeRun(g); g.TutorialAction(6); g.Enter(1); g.Tick(.05f, new Point()); Check(g.TutorialStep == 8, "Accepted actions and first expedition complete onboarding");
            g.save.largeText = true; g.save.effectsQuality = 0; g.Load(g.save); Check(g.save.largeText && g.save.effectsQuality == 0 && g.TutorialStep == 8, "Preferences survive load");
            string narrative = "A long line of travelers waited at the harbor.\n\n"; for (int i = 0; i < 6; i++) narrative += "钟声响起，山路上的每个名字都应该被记住。\n\n";
            var pages = DialogueLayout.Pages(narrative, true); Check(pages.Count > 1 && string.Concat(pages) == narrative, "Paged dialogue preserves all characters");
            g.RestartTutorial(); Check(g.TutorialStep == 0, "Onboarding can be replayed"); g.SkipTutorial(); Check(g.TutorialStep == 8, "Onboarding can be skipped");
        }
        public static void QuestTrackingTransitions(Campaign data)
        {
            var g = Fresh(data); var q = g.CurrentGuide; Check(q.room == 1 && g.NavigationTarget().Value.y == 3, "Main quest points to north exit");
            g.TrackQuest("shore"); Check(g.save.trackedQuest == "main", "Locked stories cannot replace tracking"); g.TrackQuest("ally1"); Check(g.CurrentGuide.room == 1, "Insufficient supplies point to gathering");
            g.save.inventory[2] = 6; q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 7, "Gathered herbs point to the herbalist"); Action(g, "garden", "ally1"); Check(g.CurrentGuide.complete, "Delivery completes tracked request");
            g.TrackQuest("main"); g.save.flags.Add("scout_saved"); Check(g.CurrentGuide.target.x == 29 && g.CurrentGuide.Progress > 0, "Rescue switches guidance to altar");
            g.save.flags.Add("boss_1"); g.save.flags.Add("core_1"); q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 17, "Core points back to Lyra"); Action(g, "guild", "deliver"); Check(g.CurrentGuide.room == 2, "Delivery switches chapter");
            g.TrackQuest("shore"); Check(g.save.frontierFocus && g.CurrentGuide.target.x == 26, "Unlocked shore story points to Lune");
            Action(g, "envoy", "exp_begin"); Check(g.save.trackedQuest == "shore" && g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 15, "Acceptance automatically tracks Mira");
            g.save.flags.Add("harbor_report"); Check(g.CurrentGuide.room == 6, "Harbor report points to desert"); g.save.flags.Add("desert_report"); Check(g.CurrentGuide.room == 7, "Desert report points to highlands");
            g.save.flags.Add("mountain_report"); Check(g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 18, "Reports point to council"); g.save.frontierEnding = 1; Check(g.CurrentGuide.complete && g.CurrentGuide.Progress == 1, "Settled covenant is complete");
            g.AcceptBounty(0); Check(g.save.trackedQuest == "bounty", "Accepting a contract automatically tracks it"); g.save.trackedQuest = "invalid"; g.save.explored = null; g.Load(g.save); Check(g.save.trackedQuest == "main" && g.save.explored.Length == data.maps.Length, "Malformed fields repaired");
        }

        public static void RoadwardenSignalsUnlockOnePersistentTravelReward(Campaign data)
        {
            var g = Fresh(data);
            float baseSpeed = g.Speed;
            var guide = g.GetQuest("signals");
            Check(!guide.locked && !guide.complete && guide.room == 1 && guide.steps.Count == 4, "The exploration quest is visible from a fresh save");
            g.TrackQuest("signals"); Check(g.save.trackedQuest == "signals", "Players can track the signal quest");
            g.Enter(9); g.enemies.Clear(); WorldObject desk = Object(g, "surveyDesk"); g.save.x = desk.x; g.save.y = desk.y;
            int startingCoins = g.save.coins, startingShards = g.save.shards, startingPoints = g.save.skillPoints;
            g.Interact(); Check(g.mode == "dialog" && g.conversation.choices[0].action == "close", "The chart cannot be claimed before finding the signals"); g.Choose(0);
            g.save.stage = 3;
            for (int room = 1; room <= 3; room++)
            {
                g.Enter(room); g.enemies.Clear();
                WorldObject beacon = Object(g, "survey" + room);
                Check(g.CanStand(new Point(beacon.x, beacon.y)), "Survey beacon must have clear footing in region " + room);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.Interact(); Check(g.Has("survey_" + room) && g.mode == "dialog", "In-world beacon interaction records the signal");
                g.Choose(0);
                int coins = g.save.coins, shards = g.save.shards;
                g.Talk(beacon); g.Choose(0);
                Check(g.save.coins == coins && g.save.shards == shards, "Re-reading a beacon cannot farm supplies");
                if (room < 3) Check(g.GetQuest("signals").room == room + 1, "Guidance advances to the next missing signal");
                if (room == 1) { g.Enter(9); g.Talk(Object(g, "innGuest")); Check(g.conversation.textEn.Contains("Once all three signals") && g.conversation.textEn.Contains("Coming home with a story"), "Traveler offers a mid-quest clue without losing original advice"); g.Close(); }
            }
            Check(g.save.coins == startingCoins + 36 && g.save.shards == startingShards + 3, "Each beacon grants its one-time supplies");
            Check(g.GetQuest("signals").room == 9 && !g.GetQuest("signals").complete, "The collected signals lead to the inn");
            g.Enter(9); g.enemies.Clear(); g.save.x = desk.x; g.save.y = desk.y;
            g.Interact(); Check(g.conversation.choices[0].action == "survey_finish", "Chart completion is an explicit player choice");
            g.Choose(0);
            Check(g.Has("survey_complete") && g.GetQuest("signals").complete, "The chart completes the tracked quest");
            g.save.english = true;
            Check(g.GetQuest("signals").next.Contains("travel to three beacons") && g.GetQuest("signals").reward.Contains("beacon travel"), "Completed quest clearly explains the unlocked travel network");
            g.save.english = false;
            Check(Math.Abs(g.Speed - baseSpeed - .25f) < .001f, "The permanent exploration reward changes actual movement");
            Check(g.save.coins == startingCoins + 96 && g.save.shards == startingShards + 8 && g.save.skillPoints >= startingPoints + 1, "Completion grants the advertised rewards");
            g.Talk(Object(g, "innGuest")); Check(g.conversation.textEn.Contains("helped three late travelers") && g.conversation.textEn.Contains("Coming home with a story"), "Inn traveler acknowledges the completed chart while preserving original guidance"); g.Close();
            int finalCoins = g.save.coins, finalShards = g.save.shards, finalPoints = g.save.skillPoints;
            g.Talk(desk); Check(g.conversation.choices[0].action == "close", "Finished chart cannot be redeemed again"); g.Choose(0);
            Check(g.save.coins == finalCoins && g.save.shards == finalShards && g.save.skillPoints == finalPoints, "Inn revisits cannot duplicate rewards");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.Has("survey_complete") && resumed.GetQuest("signals").complete && resumed.save.trackedQuest == "signals", "Existing save schema persists chart flags and tracking");
            Check(Math.Abs(resumed.Speed - baseSpeed - .25f) < .001f, "Movement bonus survives save and load");
        }
        public static void HomewardChartConnectsSafeRegionalTravel(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.Enter(9);
            var desk = Object(g, "surveyDesk");
            g.Talk(desk);
            Check(g.conversation.choices.Length == 1, "An unfinished chart cannot offer relay travel");
            g.Close();
            foreach (int region in new[] { 1, 2, 3 }) g.save.flags.Add("survey_" + region);
            g.save.flags.Add("survey_complete");
            int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Talk(desk);
                Check(g.conversation.choices.Length == 4 && g.conversation.choices[region].action == "survey_to_" + region, "Completed chart lists three distinct discovered destinations");
                g.enemies.Add(new Enemy(910 + region, 0, g.save.x, g.save.y, 100));
                g.Choose(region);
                Check(g.save.room == 9, "The relay cannot teleport the player away from nearby enemies");
                g.enemies.Clear();
                g.Talk(desk); g.Choose(region);
                Check(g.save.room == region && g.CanStand(g.Position), "Relay lands on valid walkable ground in region " + region);
                var beacon = Object(g, "survey" + region);
                Check((g.Position - new Point(beacon.x, beacon.y)).Length <= 1.75f, "Arrival is close enough to interact with the correct beacon");
                // Travel must not pick a collision-free tile that is unnecessarily
                // closer to a newly spawned enemy than another adjacent tile.
                Point[] offsets = {
                    new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0),
                    new Point(1, 1), new Point(-1, 1), new Point(1, -1), new Point(-1, -1)
                };
                float landedClearance = 9999;
                foreach (Enemy enemy in g.enemies)
                    if (enemy.hp > 0) landedClearance = Math.Min(landedClearance, (enemy.Position - g.Position).Length);
                foreach (Point offset in offsets)
                {
                    Point candidate = new Point(beacon.x, beacon.y) + offset;
                    if (!g.CanStand(candidate)) continue;
                    float clearance = 9999;
                    foreach (Enemy enemy in g.enemies)
                        if (enemy.hp > 0) clearance = Math.Min(clearance, (enemy.Position - candidate).Length);
                    Check(landedClearance + .001f >= clearance,
                        "Beacon travel chooses the safest adjacent walkable tile instead of spawning beside a patrol");
                }
                g.Interact();
                Check(g.conversation != null && g.conversation.choices.Length == 2 && g.conversation.choices[1].action == "survey_home", "Linked beacon exposes a return route");
                g.enemies.Add(new Enemy(920 + region, 0, g.save.x, g.save.y, 100));
                g.Choose(1);
                Check(g.save.room == region, "Beacon return is blocked during nearby combat");
                g.enemies.Clear();
                g.Talk(beacon); g.Choose(1);
                Check(g.save.room == 9 && g.CanStand(g.Position) && (g.Position - new Point(desk.x, desk.y)).Length <= 1.75f, "Return arrives beside the chart without crossing walls");
                Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points, "Repeated travel cannot duplicate chart or signal rewards");
            }
            g.save.stage = 0;
            g.Talk(desk); g.Choose(2);
            Check(g.save.room == 9, "A forged completion flag cannot bypass normal regional access requirements");
            g.save.stage = 3;
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.Talk(Object(restored, "surveyDesk")); restored.Choose(3);
            Check(restored.save.room == 3 && restored.CanStand(restored.Position), "Linked relay works after loading an existing-format save");
        }
        public static void RoadwardenLettersHaveFairPersistentCipherRewards(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            Check(g.GetQuest("dispatches").locked, "The letter quest requires the restored chart");
            g.TrackQuest("dispatches");
            Check(g.save.trackedQuest != "dispatches", "Locked letters cannot be tracked");
            for (int id = 6; id <= 8; id++)
            {
                g.Enter(id - 5); g.enemies.Clear();
                g.Talk(Object(g, "history" + id));
                Check(g.conversation.choices.Length == 1, "Original lore has its original reading before chart unlock");
                g.Close();
            }
            foreach (int room in new[] { 1, 2, 3 }) g.save.flags.Add("survey_" + room);
            g.save.flags.Add("survey_complete");
            Check(!g.GetQuest("dispatches").locked && g.GetQuest("dispatches").room == 1,
                "The chart unlocks a quest guiding players to the first old letter");
            g.TrackQuest("dispatches");
            // Isolate quest rewards from ordinary XP level-up talents.
            g.save.level = 10; g.save.xp = 0;
            int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            int originalStewPrice = g.ShopStewCost;
            for (int id = 6; id <= 8; id++)
            {
                g.Enter(id - 5); g.enemies.Clear();
                WorldObject letter = Object(g, "history" + id);
                g.save.x = letter.x; g.save.y = letter.y;
                g.Talk(letter);
                Check(g.conversation.choices.Length == 2 && g.conversation.choices[1].action == "dispatch_open_" + id,
                    "Existing lore stays readable with an optional cipher choice");
                g.Choose(1);
                Check(g.conversation != null && g.conversation.choices.Length == 3,
                    "Each letter offers a three-answer cipher");
                g.Choose(0);
                Check(!g.Has("dispatch_" + id) && g.save.coins == coins && g.save.shards == shards,
                    "Wrong answers do not consume supplies or grant rewards");
                Check(g.conversation.choices[0].action == "dispatch_open_" + id,
                    "Players can retry a wrong answer");
                g.Choose(0);
                g.Choose(id == 7 ? 2 : 1);
                Check(g.Has("dispatch_" + id) && g.mode == "world",
                    "Correct answer completes the regional cipher");
                coins += 20; shards++;
                if (id == 8) { coins += 40; points++; }
                Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points,
                    "Each letter and the final talent reward are one-time only");
                g.Talk(letter);
                Check(g.conversation.choices.Length == 1,
                    "Solved letters preserve lore but no longer offer rewards");
                g.Close();
                Check(g.GetQuest("dispatches").room == (id == 8 ? 9 : id - 4),
                    "Quest guidance advances to the next missing letter");
            }
            Check(g.Has("dispatch_complete") && g.GetQuest("dispatches").complete,
                "All three letters complete the exploration quest");
            Check(g.ShopStewCost == originalStewPrice - 2 &&
                g.GetQuest("dispatches").reward.Contains("热汤减价2"),
                "The promise permanently discounts inn stew and explains the benefit");
            g.Enter(9); g.enemies.Clear();
            g.Talk(Object(g, "innGuest"));
            Check(g.conversation.textEn.Contains("discounts stew by two coins"),
                "The inn traveler acknowledges the decoded promise");
            g.Close();
            g.Talk(Object(g, "surveyDesk"));
            Check(g.conversation.choices.Length == 5 && g.conversation.choices[4].action == "dispatch_recap",
                "The chart offers a permanent reread of the promise");
            g.Choose(4);
            Check(g.conversation.textEn.Contains("everyone deserves a way home"),
                "The chart retells the three letters without new rewards");
            g.Close();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.Has("dispatch_complete") && restored.GetQuest("dispatches").complete &&
                restored.save.trackedQuest == "dispatches" && restored.save.skillPoints == points,
                "Original save schema persists the decoded letters");
            restored.Enter(1); restored.enemies.Clear();
            restored.Talk(Object(restored, "history6"));
            Check(restored.conversation.choices.Length == 1 && restored.save.coins == coins,
                "Loading cannot duplicate rewards");
        }

        public static void RoadwardenVigilIsRealTwoWaveCombatWithPersistentOneTimeRewards(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            Check(g.GetQuest("vigil").locked, "Vigil is locked until the roadwarden promise is decoded");
            foreach (string flag in new[] { "survey_1", "survey_2", "survey_3", "survey_complete", "dispatch_complete" })
                g.save.flags.Add(flag);
            g.Enter(9); g.Talk(Object(g, "surveyDesk"));
            Check(g.conversation.textEn.Contains("two-wave defense drills"),
                "Completed chart tells players where to find the newly unlocked defense challenges");
            g.Close();
            var hit = typeof(GameCore).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The production hit path is available to the regression");
            int totalCoins = g.save.coins, totalShards = g.save.shards, totalPoints = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                var beacon = Object(g, "survey" + region);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.enemies.Add(new Enemy(10000 + region, 0, g.save.x, g.save.y, 100));
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(!g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Cannot start a defense while enemies threaten the beacon");
                g.enemies.Clear();
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100).Count == 2,
                    "Each beacon starts with two real reachable combatants");
                foreach (var enemy in g.enemies)
                    Check(g.CanStand(enemy.Position) && g.NavigationPath(enemy.Position).Count > 0,
                        "Trial enemies spawn on reachable walkable terrain");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(!resumed.Has("road_trial_" + region) &&
                    !resumed.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Loading mid-drill restarts cleanly without phantom saved enemies");
                int kills = g.save.kills[region], coins = g.save.coins, shards = g.save.shards;
                for (int wave = 1; wave <= 2; wave++)
                {
                    var attackers = g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100);
                    Check(attackers.Count == 2, "Each trial wave has exactly two opponents");
                    foreach (var enemy in attackers)
                        hit.Invoke(g, new object[] { enemy, 9999f, false, null, null });
                    Check(g.save.kills[region] == kills && g.save.coins == coins,
                        "Trial combat does not farm chapter kills, bounty credit or ordinary coins");
                    for (int frame = 0; frame < 8; frame++) g.Tick(.05f, new Point());
                }
                Check(g.Has("road_trial_" + region) && g.save.coins == coins + 35 &&
                    g.save.shards == shards + 2 + (region == 3 ? 3 : 0),
                    "Clearing both waves grants exactly one region reward");
                g.Talk(beacon);
                Check(!Array.Exists(g.conversation.choices, c => c.action == "road_trial_start_" + region),
                    "Completed beacon cannot repeat its reward");
                g.Close();
            }
            Check(g.Has("road_trial_complete") && g.GetQuest("vigil").complete &&
                g.save.coins == totalCoins + 105 && g.save.shards == totalShards + 9 &&
                g.save.skillPoints == totalPoints + 1,
                "All three defenses grant the one-time full-vigil talent reward");
            g.Enter(9); g.Talk(Object(g, "innGuest"));
            Check(g.conversation.textEn.Contains("All three beacons are defended") &&
                g.conversation.textEn.Contains("Coming home with a story"),
                "The inn acknowledges the vigil while preserving original traveler advice");
            g.Close();
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.Has("road_trial_complete") && loaded.GetQuest("vigil").complete &&
                loaded.save.skillPoints == totalPoints + 1,
                "Vigil completion and talent persist in the original save schema");
        }

        public static void RoadwardenVigilRegionalSurgesAreReadableAndTransient(Campaign data)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Production combat hit path is available");
            for (int region = 1; region <= 3; region++)
            {
                var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
                foreach (string flag in new[] { "survey_1", "survey_2", "survey_3",
                    "survey_complete", "dispatch_complete" }) g.save.flags.Add(flag);
                g.Enter(region); g.enemies.Clear();
                var beacon = Object(g, "survey" + region);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.Talk(beacon);
                Check(g.conversation.textEn.Contains(region == 1 ? "thorn pincers" :
                    region == 2 ? "sweeping tides" : "mine bursts"),
                    "Players are warned about their region's unique second wave");
                g.Close();
                Action(g, beacon.kind, "road_trial_start_" + region);
                foreach (Enemy foe in g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100))
                    hit.Invoke(g, new object[] { foe, 9999f, false, null, null });
                for (int frame = 0; frame < 8; frame++) g.Tick(.05f, new Point());
                Check(g.RoadwardenTrialWave == 2, "Defeating wave one starts wave two");
                foreach (Enemy foe in g.enemies) foe.stun = 12;
                for (int frame = 0; frame < 32; frame++) g.Tick(.05f, new Point());
                string kind = region == 1 ? "root" : region == 2 ? "tide" : "glass";
                var warnings = g.hazards.FindAll(h => h.kind == kind);
                Check(warnings.Count >= 1, "Each region schedules real visible warning hazards");
                foreach (Hazard warning in warnings)
                    Check(g.CanStand(warning.position) && warning.timer > 0 &&
                        warning.damage > 0 && warning.radius > 0,
                        "Hazards are telegraphed on traversable ground with real damage");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(resumed.hazards.Count == 0 && resumed.RoadwardenTrialRoom == 0,
                    "Loading during a defense cannot restore transient hazards");
                Action(g, beacon.kind, "road_trial_abandon");
                Check(g.RoadwardenTrialRoom == 0 && g.hazards.Count == 0 &&
                    !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Abandoning removes both enemies and all pending regional strikes");
            }
        }

        public static void NavigationRespectsWorldCollision(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.frontierEnding = 1;
            foreach (string flag in new[] { "scout_saved", "gate_2", "gate_3", "exp_intro", "harbor_report", "desert_report", "gate_5", "gate_6", "gate_7" }) g.save.flags.Add(flag);
            for (int room = 0; room < 8; room++)
            {
                g.save.flags.Add("seal_" + room); g.Enter(room); g.enemies.Clear(); g.save.totalTime = 0;
                foreach (var o in g.Map.objects)
                {
                    if (o.kind == "shade" || o.kind.StartsWith("castaway")) continue;
                    var target = new Point(o.x, o.y); var route = g.NavigationPath(target); Check(route.Count > 0 || (g.Position - target).Length < 1.8f, "A route must approach " + room + "/" + o.kind);
                    foreach (var p in route) Check(!g.Blocked(p.x, p.y) && !g.Blocked(p.x - .22f, p.y - .18f) && !g.Blocked(p.x + .22f, p.y + .18f), "Guide respects hero collision footprint");
                    if (route.Count > 0) Check((route[route.Count - 1] - target).Length <= 1.8f, "Guide ends in interaction range");
                }
            }
            g.Enter(5); g.save.totalTime = 24; Check(g.NavigationPath(new Point(25, 19)).Count == 0, "High-tide shoals cannot be crossed"); g.save.totalTime = 0; Check(g.NavigationPath(new Point(25, 19)).Count > 0, "Relay reachable at low tide");
            g.Enter(1); g.save.flags.Remove("scout_saved"); foreach (var p in g.NavigationPath(new Point(29, 8))) Check(!g.Blocked(p.x, p.y), "No guide crosses blocked gates");
        }
    }
}
