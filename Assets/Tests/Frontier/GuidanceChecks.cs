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
            }
            Check(g.save.coins == startingCoins + 36 && g.save.shards == startingShards + 3, "Each beacon grants its one-time supplies");
            Check(g.GetQuest("signals").room == 9 && !g.GetQuest("signals").complete, "The collected signals lead to the inn");
            g.Enter(9); g.enemies.Clear(); g.save.x = desk.x; g.save.y = desk.y;
            g.Interact(); Check(g.conversation.choices[0].action == "survey_finish", "Chart completion is an explicit player choice");
            g.Choose(0);
            Check(g.Has("survey_complete") && g.GetQuest("signals").complete, "The chart completes the tracked quest");
            Check(Math.Abs(g.Speed - baseSpeed - .25f) < .001f, "The permanent exploration reward changes actual movement");
            Check(g.save.coins == startingCoins + 96 && g.save.shards == startingShards + 8 && g.save.skillPoints >= startingPoints + 1, "Completion grants the advertised rewards");
            int finalCoins = g.save.coins, finalShards = g.save.shards, finalPoints = g.save.skillPoints;
            g.Talk(desk); Check(g.conversation.choices[0].action == "close", "Finished chart cannot be redeemed again"); g.Choose(0);
            Check(g.save.coins == finalCoins && g.save.shards == finalShards && g.save.skillPoints == finalPoints, "Inn revisits cannot duplicate rewards");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.Has("survey_complete") && resumed.GetQuest("signals").complete && resumed.save.trackedQuest == "signals", "Existing save schema persists chart flags and tracking");
            Check(Math.Abs(resumed.Speed - baseSpeed - .25f) < .001f, "Movement bonus survives save and load");
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
