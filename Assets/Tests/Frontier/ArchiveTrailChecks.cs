using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void AtlasBlockedSecondRoadResumesOnlyWhenPhysicalPathOpens(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("archive_started");
            g.save.flags.Add("archive_fragment_1");
            WorldObject page=Object(g,"archivefragment1"),south=null,east=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.id=="waytrail1s")south=o;
                if(o.id=="waytrail1e")east=o;
            }
            Check(south!=null&&east!=null,"The blocked-route scenario owns two real regional waytrails");
            g.save.x=page.x;g.save.y=page.y;
            g.Talk(page);
            int selector=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_routes_1");
            Check(selector>=0,"The original chart has a physical route selector");
            g.Choose(selector);
            int chooseSouth=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_go_1_s");
            Check(chooseSouth>=0,"The southern authored trail can be selected");
            g.Choose(chooseSouth);
            Point? foot=g.LocalWaymark;
            Check(foot.HasValue,"The south-road approach was committed before the crossing closed");

            // Seal a real 5x5 region around the ORIGINAL eastern marker.
            // The survey is allowed to finish at the southern marker but
            // must not draw an impossible line across a new physical wall.
            string[] originalRows=g.Map.rows;
            string[] sealedRows=(string[])originalRows.Clone();
            int cx=(int)Math.Floor(east.x),cy=(int)Math.Floor(east.y);
            for(int y=cy-2;y<=cy+2;y++)
            {
                char[] cells=sealedRows[y].ToCharArray();
                for(int x=cx-2;x<=cx+2;x++)cells[x]='#';
                sealedRows[y]=new string(cells);
            }
            g.Map.rows=sealedRows;
            try
            {
                g.save.x=foot.Value.x;g.save.y=foot.Value.y;
                g.Talk(south);
                Check(g.Has("waytrail_"+south.id)&&g.AtlasSurveyHandoffPending&&
                    !g.LocalWaymark.HasValue&&
                    g.AtlasSurveyNextStop.Contains("等待道路恢复"),
                    "A first actual survey waits at the genuinely sealed second road without inventing a path");
                g.Close();
                for(int i=0;i<35;i++)g.Tick(.05f,new Point());
                Check(g.AtlasSurveyHandoffPending&&!g.LocalWaymark.HasValue,
                    "Repeated retry ticks on a closed road cannot create a false compass arrow");
                // A pending atlas handoff is transient: old v2 saves remain
                // ordinary exploration saves, with only true road flags.
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(!loaded.AtlasSurveyHandoffPending&&!loaded.LocalWaymark.HasValue&&
                    loaded.Has("waytrail_"+south.id),
                    "A save reload keeps completed surveys but not unsaved blocked navigation");

                g.Map.rows=originalRows;
                for(int i=0;i<50&&!g.LocalWaymark.HasValue;i++)
                    g.Tick(.05f,new Point());
                Check(g.LocalWaymark.HasValue&&
                    (g.LocalWaymark.Value-new Point(east.x,east.y)).Length<1.81f &&
                    !g.AtlasSurveyHandoffPending&&g.AtlasSurveyNavigationActive,
                    "A reopened real crossing automatically resumes the next physically reachable old road");
            }
            finally {g.Map.rows=originalRows;}
        }

        public static void AtlasSurveyRespectsManualNavigationAndRetiresReplacedMapObjects(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("archive_started");
            g.save.flags.Add("archive_fragment_1");
            WorldObject page=Object(g,"archivefragment1"),south=null;
            foreach(WorldObject candidate in g.Map.objects)
                if(candidate.id=="waytrail1s")south=candidate;
            Check(south!=null,"A canonical southern trail exists on the actual field map");
            g.save.x=page.x;g.save.y=page.y;
            g.Talk(page);
            int select=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_routes_1");
            g.Choose(select);
            int chooseSouth=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_go_1_s");
            Check(chooseSouth>=0,"Live atlas offers the original southern marker");
            g.Choose(chooseSouth);
            Point? atlasPin=g.LocalWaymark;
            Check(atlasPin.HasValue&&g.AtlasSurveyNavigationActive &&
                !g.AtlasSurveyAwaitingInteraction &&
                g.AtlasSurveyNextStop.Contains("南侧旧路"),
                "The real atlas HUD identifies the unsurveyed southern road");

            // A new player-chosen pin must never be seized later by the
            // atlas when that abandoned marker is physically investigated.
            g.mode="map";
            Point manual=new Point(
                (float)Math.Floor(page.x)+.5f,
                (float)Math.Floor(page.y)+.5f);
            Check(g.PlaceLocalWaymark(manual),
                "A player can deliberately override an atlas pin on the real local map");
            Point? ownPin=g.LocalWaymark;
            Check(ownPin.HasValue&&
                (ownPin.Value-atlasPin.Value).Length>.1f &&
                !g.AtlasSurveyNavigationActive &&
                string.IsNullOrEmpty(g.AtlasSurveyNextStop),
                "A manual pin replaces both the atlas destination and the stale HUD label");
            g.mode="world";
            g.save.x=south.x;g.save.y=south.y;
            g.Talk(south);
            Check(g.Has("waytrail_"+south.id)&&g.LocalWaymark.HasValue &&
                (g.LocalWaymark.Value-ownPin.Value).Length<.001f,
                "A separately surveyed road cannot silently overwrite deliberate manual routing");
            g.Close();

            // A later atlas choice is bound to the same live WorldObject,
            // not an impostor replacing its ID at identical coordinates.
            g.save.flags.Remove("waytrail_"+south.id);
            g.save.x=page.x;g.save.y=page.y;
            g.Talk(page);
            select=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_routes_1");
            g.Choose(select);
            chooseSouth=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_go_1_s");
            g.Choose(chooseSouth);
            atlasPin=g.LocalWaymark;
            WorldObject[] original=g.Map.objects;
            WorldObject[] swapped=(WorldObject[])original.Clone();
            int slot=Array.FindIndex(swapped,o=>System.Object.ReferenceEquals(o,south));
            Check(slot>=0,"The survey owns the actual authored object reference");
            swapped[slot]=new WorldObject{id=south.id,kind=south.kind,
                name=south.name,en=south.en,x=south.x,y=south.y};
            g.Map.objects=swapped;
            try
            {
                g.save.x=atlasPin.Value.x;g.save.y=atlasPin.Value.y;
                g.Tick(.05f,new Point());
                Check(!g.LocalWaymark.HasValue,
                    "Replacing an active trail with a same-ID impostor invalidates the stale atlas pin");
            }
            finally {g.Map.objects=original;}
        }

        public static void AtlasRouteAdvancesOnlyAfterActuallySurveyingBothOldRoads(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("archive_started");
            g.save.flags.Add("archive_fragment_1");
            WorldObject page=Object(g,"archivefragment1");
            WorldObject south=null,east=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.id=="waytrail1s")south=o;
                if(o.id=="waytrail1e")east=o;
            }
            Check(south!=null&&east!=null,"The atlas survey uses both original physical road markers");
            g.save.x=page.x;g.save.y=page.y;
            g.Talk(page);
            int route=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_routes_1");
            Check(route>=0,"A recovered field chart opens the two-road selector");
            g.Choose(route);
            int southChoice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_go_1_s");
            Check(southChoice>=0,"The player can select the original southern road");
            g.Choose(southChoice);
            Point? originalPin=g.LocalWaymark;
            Check(originalPin.HasValue&&
                (originalPin.Value-new Point(south.x,south.y)).Length<1.81f,
                "Choosing south creates a real physical-side survey pin");
            // The authored pin lands on interaction-side WALKABLE ground,
            // not the object's visual center. Simulate arriving at the
            // actual committed foot before looking for the E prompt.
            g.save.x=originalPin.Value.x;g.save.y=originalPin.Value.y;
            g.Tick(.05f,new Point());
            Check(g.LocalWaymark.HasValue&&
                (g.LocalWaymark.Value-originalPin.Value).Length<.001f &&
                g.AtlasSurveyAwaitingInteraction &&
                g.AtlasSurveyNextStop.Contains("按 E"),
                "At the real atlas waypoint the HUD requests E, not proximity-only completion");
            int coins=g.save.coins,shards=g.save.shards;
            g.Talk(south);
            Check(g.Has("waytrail_"+south.id)&&
                g.save.coins==coins+8&&g.save.shards==shards+1,
                "Only a real physical waytrail interaction yields its original first reward");
            Point? next=g.LocalWaymark;
            Check(next.HasValue&&
                (next.Value-new Point(east.x,east.y)).Length<1.81f &&
                (next.Value-originalPin.Value).Length>.1f &&
                g.AtlasSurveyNavigationActive &&
                !g.AtlasSurveyAwaitingInteraction &&
                g.AtlasSurveyNextStop.Contains("东侧旧路") &&
                g.AtlasSurveyNextStop.Contains("1/2"),
                "Surveying the first road updates route, real destination and progress together");
            g.Close();
            g.save.x=east.x;g.save.y=east.y;
            g.Tick(.05f,new Point());
            Check(g.LocalWaymark.HasValue,
                "The second survey likewise requires physical interaction rather than proximity alone");
            coins=g.save.coins;shards=g.save.shards;
            g.Talk(east);
            Check(g.Has("waytrail_"+east.id)&&
                g.save.coins==coins+8&&g.save.shards==shards+1 &&
                !g.LocalWaymark.HasValue &&
                !g.AtlasSurveyNavigationActive &&
                string.IsNullOrEmpty(g.AtlasSurveyNextStop),
                "Both roads complete: only original rewards, no leftover atlas pin or HUD");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.Has("waytrail_"+south.id)&&
                loaded.Has("waytrail_"+east.id)&&
                !loaded.LocalWaymark.HasValue,
                "Existing v2 save retains only real road surveys, never a fabricated navigation task");
        }

        public static void LostSurveyorAtlasHasRealSequenceAndOneTimeRewards(Campaign data)
        {
            var g = Fresh(data);
            Check(g.GetQuest("archive_trail").locked,
                "The lost atlas must start as a discoverable quest");
            WorldObject keeper = Object(g, "archivekeeper");
            g.save.x = keeper.x; g.save.y = keeper.y;
            Check(g.CanStand(g.Position) && g.Nearest() == keeper,
                "The new cartographer exists on walkable town ground");
            Point outsideKeeper=StandableOutsidePhysicalRadius(g,keeper,2.2001f);
            g.save.x=outsideKeeper.x;g.save.y=outsideKeeper.y;
            g.Talk(keeper);
            Check(g.mode!="dialog",
                "A direct Talk cannot open the atlas commission outside physical reach");
            g.Close();
            g.save.x=keeper.x;g.save.y=keeper.y;
            g.Talk(keeper);
            int staleAccept=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_accept");
            Check(staleAccept>=0,"The cartographer offers the real accept action while nearby");
            g.save.x=outsideKeeper.x;g.save.y=outsideKeeper.y;
            g.Choose(staleAccept);g.Close();
            Check(!g.Has("archive_started"),
                "A stale atlas-accept choice cannot start the quest from beyond the keeper's reach");
            g.save.x=keeper.x;g.save.y=keeper.y;
            Action(g, keeper.kind, "archive_accept");
            Check(g.Has("archive_started") && !g.GetQuest("archive_trail").locked &&
                g.save.trackedQuest == "archive_trail",
                "Talking to Lin accepts and tracks the atlas quest");
            g.Talk(keeper);
            int interpret=Array.FindIndex(g.conversation.choices,c=>c.action=="archive_interpret");
            Check(interpret>=0,"The accepted commission exposes a physical evidence-reading conversation");
            g.Choose(interpret);
            Check(g.mode=="dialog" && g.conversation.speaker==keeper.name &&
                g.conversation.text.Contains("荆棘密林") &&
                !g.conversation.text.Contains("矿工把逃生坡道"),
                "Lin does not impersonate another speaker or narrate uncollected mine evidence");
            int back=Array.FindIndex(g.conversation.choices,c=>c.action=="archive_return");
            Check(back>=0,"The evidence discussion offers a genuine return to the same commission");
            g.Choose(back);
            Check(g.mode=="dialog" && g.conversation.speaker==keeper.name &&
                Array.Exists(g.conversation.choices,c=>c.action=="archive_interpret"),
                "Returning from the evidence page keeps Lin's own interactive commission");
            g.Close();
            // Chapters are still locked by the original main quest: the atlas
            // must neither teleport nor grant premature access to region 2.
            g.Enter(2);
            Check(g.save.room == 0, "Atlas commission cannot bypass chapter gate to ruins");
            // This harness now advances the original campaign gates before
            // inspecting its three optional, ordered artifacts.
            g.save.stage = 3;
            // Reward tests isolate the quest from ordinary XP level-up grants.
            g.save.level = 10; g.save.xp = 0;
            int coins = g.save.coins, shards = g.save.shards, skill = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject artifact = Object(g, "archivefragment" + region);
                g.save.x = artifact.x; g.save.y = artifact.y;
                Check(g.CanStand(g.Position) && g.Nearest() == artifact,
                    "Each regional atlas artifact is a real reachable map interaction");
                if (region == 2)
                {
                    g.save.flags.Remove("archive_fragment_1");
                    g.Talk(artifact);
                    Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_claim_2"),
                        "The tide inscription is unreadable before the woodland chart");
                    g.Close(); g.save.flags.Add("archive_fragment_1");
                }
                if(region==1)
                {
                    Point outsideArtifact=StandableOutsidePhysicalRadius(g,artifact,2.2001f);
                    g.save.x=outsideArtifact.x;g.save.y=outsideArtifact.y;
                    g.Talk(artifact);
                    Check(g.mode!="dialog",
                        "A direct Talk cannot open a regional atlas artifact outside physical reach");
                    g.Close();
                    g.save.x=artifact.x;g.save.y=artifact.y;
                    g.Talk(artifact);
                    int staleClaim=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_claim_"+region);
                    Check(staleClaim>=0,"A reachable atlas artifact offers its genuine claim action");
                    int coinsBeforeStaleClaim=g.save.coins,shardsBeforeStaleClaim=g.save.shards;
                    g.save.x=outsideArtifact.x;g.save.y=outsideArtifact.y;
                    g.Choose(staleClaim);g.Close();
                    Check(!g.Has("archive_fragment_"+region) &&
                        g.save.coins==coinsBeforeStaleClaim &&
                        g.save.shards==shardsBeforeStaleClaim,
                        "A stale atlas-claim choice cannot award records or resources outside reach");
                    g.save.x=artifact.x;g.save.y=artifact.y;
                }
                Action(g, artifact.kind, "archive_claim_" + region);
                Check(g.Has("archive_fragment_" + region) &&
                    g.save.coins == coins + region * 20 &&
                    g.save.shards == shards + region * 2,
                    "Each recovered artifact pays its one-time regional reward");
                g.Talk(artifact);
                Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_claim_" + region),
                    "Collected artifacts cannot be repeatedly claimed");
                int review=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="archive_review_"+region);
                Check(review>=0,
                    "Each real recovered atlas artifact remains an interactive field record");
                int coinsBeforeReview=g.save.coins,shardsBeforeReview=g.save.shards;
                g.Choose(review);
                Check(g.mode=="dialog" && g.conversation.speaker==artifact.name &&
                    g.conversation.text.Length>100 &&
                    !Array.Exists(g.conversation.choices,c=>c.action=="archive_claim_"+region),
                    "An on-site annotation is fully authored, keeps its object's name and cannot reopen the claim");
                int fieldReturn=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="archive_field_return_"+region);
                Check(fieldReturn>=0,
                    "The physical artifact's detailed note can return to its original dialogue");
                g.Choose(fieldReturn);
                Check(g.mode=="dialog" && g.conversation.speaker==artifact.name &&
                    g.save.coins==coinsBeforeReview && g.save.shards==shardsBeforeReview &&
                    Array.Exists(g.conversation.choices,c=>c.action=="archive_review_"+region),
                    "Reading/reopening the physical note preserves identity, rewards and choices");
                g.Close();
                if(region==1)
                {
                    // The old survey record is now a usable navigation
                    // instrument, not merely a collectible dialogue page.
                    g.Talk(artifact);
                    int pin=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_pin_1");
                    Check(pin>=0,
                        "Recovered field evidence offers actionable walking guidance to real unvisited waytrails");
                    string previousTracking=g.save.trackedQuest;
                    int coinBeforePin=g.save.coins,shardBeforePin=g.save.shards;
                    g.Choose(pin);
                    Point? mark=g.LocalWaymark;
                    Check(mark.HasValue&&g.CanStand(mark.Value)&&
                        g.save.trackedQuest==previousTracking&&
                        g.save.coins==coinBeforePin&&g.save.shards==shardBeforePin,
                        "Physical atlas route pins reachable walking ground without changing tracked quests or paying a duplicate reward");
                    bool nearRealMarker=false;
                    foreach(WorldObject o in g.Map.objects)
                        if(o.kind=="waytrail"&&!g.Has("waytrail_"+o.id)&&
                           (mark.Value-new Point(o.x,o.y)).Length<1.81f)
                           nearRealMarker=true;
                    Check(nearRealMarker,
                        "An atlas pin ends at actual interaction-side footing beside an unvisited authored road marker");
                    // Even after running out of unmapped local roads, the
                    // player must keep the previously committed personal pin.
                    foreach(WorldObject o in g.Map.objects)
                        if(o.kind=="waytrail")
                            g.save.flags.Add("waytrail_"+o.id);
                    g.Talk(artifact);
                    pin=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_pin_1");
                    g.Choose(pin);
                    Check(g.LocalWaymark.HasValue&&
                        (g.LocalWaymark.Value-mark.Value).Length<.001f&&
                        g.toast.Contains("都已实地记录"),
                        "Exhausted atlas discoveries cannot erase a previous personal route or claim phantom roads");
                    foreach(WorldObject o in g.Map.objects)
                        if(o.kind=="waytrail")
                            g.save.flags.Remove("waytrail_"+o.id);
                    g.Close();

                    g.Talk(artifact);
                    int routeMenu=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_routes_1");
                    Check(routeMenu>=0,
                        "Recovered atlas offers a genuine choice of two physical old roads");
                    g.Choose(routeMenu);
                    int east=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_go_1_e");
                    int south=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_go_1_s");
                    Check(g.conversation.speaker==artifact.name && east>=0 &&
                        south>=0,"The two route choices belong to the recovered field record");
                    int coinsBeforeRoad=g.save.coins,shardsBeforeRoad=g.save.shards;
                    g.Choose(east);
                    Point? easternPin=g.LocalWaymark;
                    WorldObject easternObject=null;
                    foreach(WorldObject candidate in g.Map.objects)
                        if(candidate.id=="waytrail1e")easternObject=candidate;
                    Check(easternObject!=null&&easternPin.HasValue&&
                        (easternPin.Value-new Point(easternObject.x,easternObject.y)).Length<1.81f &&
                        g.CanStand(easternPin.Value)&&g.save.coins==coinsBeforeRoad&&
                        g.save.shards==shardsBeforeRoad,
                        "Choosing the eastern route actually pins walkable interaction-side ground without a reward");
                    g.Talk(artifact);
                    routeMenu=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_routes_1");
                    g.Choose(routeMenu);
                    east=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="archive_go_1_e");
                    Check(east>=0,"The unsurveyed road appears in the actual choice menu");
                    g.save.flags.Add("waytrail_waytrail1e");
                    g.Choose(east);
                    Check(g.LocalWaymark.HasValue &&
                        (g.LocalWaymark.Value-easternPin.Value).Length<.001f &&
                        g.toast.Contains("已经被记录"),
                        "A stale already-surveyed road choice cannot overwrite a valid personal route");
                    g.save.flags.Remove("waytrail_waytrail1e");
                    g.Close();
                }
                // Review recovered evidence with Lin at the real town
                // workbench before proceeding to the next regional clue.
                g.Enter(0);g.enemies.Clear();
                g.save.x=keeper.x;g.save.y=keeper.y;
                g.Talk(keeper);
                string targetRegion=region==1?"沉没遗迹":
                    region==2?"赤铁矿场":"三份记录";
                Check(g.conversation.text.Contains(targetRegion),
                    "Lin's next-step narration must point to the actual next chapter site");
                interpret=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="archive_interpret");
                Check(interpret>=0,
                    "All recovered stages expose their evidence comparison dialogue");
                g.Choose(interpret);
                Check(g.mode=="dialog" && g.conversation.speaker==keeper.name &&
                    g.conversation.textEn.Length>150,
                    "Lin's comparison must remain bilingual and physically owned at each stage");
                if(region==2)
                    Check(g.conversation.text.Contains("赤铁矿场东侧") &&
                        g.conversation.text.Contains("不能替他们回答") &&
                        !g.conversation.text.Contains("把逃生坡道和同伴的名字刻进"),
                        "The second-stage scene names Rediron but does not fabricate unread miner testimony");
                g.Close();
            }
            Check(!g.GetQuest("archive_trail").complete && g.GetQuest("archive_trail").room == 0,
                "Collecting all records directs the player back to Lin");
            g.Enter(0); g.save.x = keeper.x; g.save.y = keeper.y;
            g.Talk(keeper);
            int staleComplete=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_complete");
            Check(staleComplete>=0,"The nearby cartographer offers the final atlas delivery");
            int coinsBeforeStaleComplete=g.save.coins,shardsBeforeStaleComplete=g.save.shards;
            int skillBeforeStaleComplete=g.save.skillPoints;
            g.save.x=outsideKeeper.x;g.save.y=outsideKeeper.y;
            g.Choose(staleComplete);g.Close();
            Check(!g.Has("archive_complete") &&
                g.save.coins==coinsBeforeStaleComplete &&
                g.save.shards==shardsBeforeStaleComplete &&
                g.save.skillPoints==skillBeforeStaleComplete,
                "A stale atlas-delivery choice cannot publish the quest or grant rewards remotely");
            g.save.x=keeper.x;g.save.y=keeper.y;
            Action(g, keeper.kind, "archive_complete");
            Check(g.Has("archive_complete") && g.GetQuest("archive_trail").complete &&
                g.save.coins == coins + 120 && g.save.shards == shards + 6 &&
                g.save.skillPoints == skill + 1,
                "Restoring the atlas grants one final talent and the exact stated rewards");
            g.Talk(keeper);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_complete"),
                "Completed town dialogue cannot pay a second final reward");
            interpret=Array.FindIndex(g.conversation.choices,
                c=>c.action=="archive_interpret");
            Check(interpret>=0,"A finished atlas has a real readable epilogue rather than a dead-end close");
            g.Choose(interpret);
            Check(g.conversation.speaker==keeper.name &&
                g.conversation.text.Contains("三种墨迹") &&
                g.conversation.textEn.Contains("three inks") == false &&
                g.conversation.textEn.Contains("Three inks"),
                "Finished atlas reading credits the three original crews in both languages");
            g.Close();g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.GetQuest("archive_trail").complete &&
                loaded.save.coins == g.save.coins &&
                loaded.save.skillPoints == g.save.skillPoints,
                "Atlas quest and its rewards persist through the existing save format");
        }

        public static void StarrootTrialsSupportRealSafeWithdrawal(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            g.save.flags.Add("scout_saved"); g.save.flags.Add("root_harmony_started");
            g.Enter(2); g.enemies.Clear();
            g.save.flags.Add("root_harmony_1");
            WorldObject node = Object(g, "rootcounter2");
            g.save.x = node.x; g.save.y = node.y;
            Action(g, node.kind, "root_harmony_tune_2_0");
            Check(g.RootHarmonyRoom == 2 && g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3,
                "An actual tide-frequency selection awakens live second-region sentinels");
            g.projectiles.Add(new Projectile {
                position = g.Position, velocity = new Point(0, 0),
                source = "shot", visual = "enemyArrow", life = 2, rootHarmonyOwned = true
            });
            g.hazards.Add(new Hazard {
                position = g.Position, timer = 1.3f, radius = 1,
                damage = 10, kind = "tide", rootHarmonyOwned = true
            });
            g.effects.Add(new Effect("danger", g.Position, 1.3f) {
                radius = 1, rootHarmonyOwned = true
            });
            var unrelated = new Effect("discover", g.Position, 2);
            g.effects.Add(unrelated);
            g.Talk(node);
            int abandon = Array.FindIndex(g.conversation.choices, c =>
                c.action == "root_harmony_abandon_2");
            Check(abandon >= 0, "Combat dialogue offers a real retreat command");
            g.Choose(abandon);
            Check(g.RootHarmonyRoom == 0 &&
                !g.enemies.Exists(g.IsRootHarmonyEnemy) &&
                !g.projectiles.Exists(p => p.rootHarmonyOwned) &&
                !g.hazards.Exists(h => h.rootHarmonyOwned) &&
                !g.effects.Exists(e => e.rootHarmonyOwned) &&
                g.effects.Contains(unrelated),
                "Voluntary retreat retires only temporary trial attacks, not unrelated world effects");
            Check(!g.Has("root_harmony_2") && g.GetQuest("root_harmony").room == 2,
                "Retreat neither fabricates a reading nor skips the regional chapter");
            Action(g, node.kind, "root_harmony_tune_2_0");
            Check(g.RootHarmonyRoom == 2 && g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3,
                "The same map's physical stone can start a fresh fair challenge after retreat");
        }

        public static void StarrootCombatPulsesWarnAndCanActuallyBeDodged(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("root_harmony_started");
            g.Enter(1); g.enemies.Clear();
            WorldObject stone = Object(g, "rootcounter1");
            g.save.x = stone.x; g.save.y = stone.y;
            Action(g, stone.kind, "root_harmony_tune_1_2");
            Check(g.RootHarmonyRoom == 1 &&
                g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3,
                "Only a correct physical tuning starts actual sentinel combat");
            foreach (Enemy sentinel in g.enemies)
                if (g.IsRootHarmonyEnemy(sentinel)) sentinel.stun = 60f;
            g.invulnerable = 0;
            for (int i = 0; i < 85 && !g.hazards.Exists(z => z.rootHarmonyOwned); i++)
                g.Tick(.05f, new Point());
            Hazard warning = g.hazards.Find(z => z.rootHarmonyOwned);
            Check(warning != null && warning.timer > .9f &&
                (warning.position - g.Position).Length < .001f,
                "Live sentinel trial creates a real delayed world-space warning at the hero's feet");
            float hp = g.save.health;
            // Running to an authored safe spawn before the contact pulse must
            // avoid damage without bypassing collision or changing the timer.
            g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY;
            Check(g.CanStand(g.Position) && (g.Position - warning.position).Length > warning.radius + 1,
                "A safe map foothold exists outside the visible pulse");
            for (int i = 0; i < 33; i++) g.Tick(.05f, new Point());
            Check(!g.hazards.Contains(warning) && Math.Abs(hp - g.save.health) < .001f,
                "Dodging the genuine delayed contact avoids its damage");
            // The periodic second warning should still be able to hurt if the
            // player refuses to leave the marked footprint.
            g.save.x = stone.x; g.save.y = stone.y;
            for (int i = 0; i < 160 && !g.hazards.Exists(z => z.rootHarmonyOwned); i++)
                g.Tick(.05f, new Point());
            Hazard contact = g.hazards.Find(z => z.rootHarmonyOwned);
            Check(contact != null && contact.timer > .9f,
                "The challenge repeats timed physical dissonance while sentinels survive");
            hp = g.save.health;
            for (int i = 0; i < 35; i++) g.Tick(.05f, new Point());
            Check(g.save.health < hp - 10f,
                "Ignoring the actual warning applies real player combat damage");
            g.Enter(0);
            Check(g.RootHarmonyRoom == 0 &&
                !g.hazards.Exists(z => z.rootHarmonyOwned) &&
                !g.enemies.Exists(g.IsRootHarmonyEnemy) &&
                !g.Has("root_harmony_1"),
                "Leaving the trial retires temporary hazards and enemies without awarding progress");
        }

        public static void NewCounterpointChronicleRevealsOnlyRecoveredMusic(Campaign data)
        {
            var g = Fresh(data);
            WorldObject book = Object(g, "worldchronicle");
            g.save.x = book.x; g.save.y = book.y;
            g.Talk(book);
            int section = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_section_after");
            Check(section >= 0, "Existing world chronicle provides the homeward aftermath section");
            g.Choose(section);
            Check(!Array.Exists(g.conversation.choices,
                c => c.action == "story_read_counterpoint"),
                "The new dissonance story stays sealed before accepting Rui's real quest");
            g.Close();
            g.save.flags.Add("root_harmony_started");
            g.Talk(book); section = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_section_after"); g.Choose(section);
            Check(Array.Exists(g.conversation.choices,
                c => c.action == "story_read_counterpoint"),
                "Accepting the real quest unlocks its readable chapter without revealing later regions");
            int page = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_read_counterpoint");
            g.Choose(page);
            Check(g.conversation.text.Contains("密林根音尚未取回") &&
                !g.conversation.text.Contains("三道音纹终于被封存"),
                "The journal does not fabricate recovered evidence or the final conclusion");
            g.Close();
            g.save.flags.Add("root_harmony_1");
            g.Talk(book); section = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_section_after"); g.Choose(section);
            page = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_read_counterpoint"); g.Choose(page);
            Check(g.conversation.text.Contains("密林第一道根音已重新归档") &&
                g.conversation.text.Contains("沉没遗迹潮音仍在沉睡"),
                "The new chronicle actually updates with completed regional progress");
            g.Close();
            g.save.flags.Add("root_harmony_2");
            g.save.flags.Add("root_harmony_3");
            g.save.flags.Add("root_harmony_complete");
            g.save.english = true;
            g.Talk(book); section = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_section_after"); g.Choose(section);
            page = Array.FindIndex(g.conversation.choices,
                c => c.action == "story_read_counterpoint"); g.Choose(page);
            Check(g.conversation.textEn.Contains("All three readings now belong") &&
                g.conversation.textEn.Contains("\\n") == false,
                "The bilingual final lore only unlocks after completion and uses proper formatted paragraphs");
        }

        public static void CalibratedStarrootSitesOfferSafePersistentRest(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3; g.save.level = 10;
            g.Enter(1); g.enemies.Clear();
            WorldObject stone = Object(g, "rootcounter1");
            g.save.x = stone.x; g.save.y = stone.y;
            g.save.health = g.save.maxHealth - 16;
            g.save.energy = 22;
            Check(!g.RootHarmonyRestoring,
                "An undiscovered stone cannot provide free healing before completing the quest");
            float before = g.save.health;
            g.Tick(.05f, new Point());
            Check(Math.Abs(g.save.health - before) < .001f,
                "An untuned resonance remains inert and does not modify core player health");
            g.save.flags.Add("root_harmony_1");
            Check(g.RootHarmonyRestoring,
                "Sealing the actual root reading creates a persistent safe refuge at its physical map location");
            float energy = g.save.energy; before = g.save.health;
            g.Tick(.05f, new Point());
            Check(g.save.health > before + .07f &&
                g.save.energy > energy + .9f,
                "Safe resonance actively restores both HP and energy in the existing combat simulation");
            // An enemy within the live safe-travel radius must interrupt the
            // respite; the ordinary player combat remains unchanged.
            Enemy raider = new Enemy(707, 0, stone.x + 1.5f, stone.y, 55);
            raider.stun = 5f; g.enemies.Add(raider);
            Check(!g.RootHarmonyRestoring,
                "A nearby real enemy disables the sanctuary even if immobilized");
            before = g.save.health;
            g.Tick(.05f, new Point());
            Check(Math.Abs(g.save.health - before) < .001f,
                "Combat cannot be trivialized by indefinitely standing in a healing node");
            g.enemies.Clear();
            Point far = new Point(g.Map.spawnX, g.Map.spawnY);
            g.save.x = far.x; g.save.y = far.y;
            Check(!g.RootHarmonyRestoring,
                "The permanent respite remains local rather than applying a global passive");
            before = g.save.health;
            g.Tick(.05f, new Point());
            Check(Math.Abs(g.save.health - before) < .001f,
                "Walking outside the actual resonance radius immediately ends bonus healing");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            loaded.enemies.Clear(); loaded.save.x = stone.x; loaded.save.y = stone.y;
            Check(loaded.RootHarmonyRestoring,
                "Existing persistent quest flags restore the refuge after reload without a new save schema");
        }

        public static void StarrootCounterpointIsNewPlayableCrossRegionStory(Campaign data)
        {
            var g = Fresh(data);
            Check(g.GetQuest("root_harmony").locked &&
                !g.Has("root_harmony_started"),
                "The counterpoint is independently discoverable and gated before its NPC accepts it");
            WorldObject rui = Object(g, "rootlistener");
            g.save.x = rui.x; g.save.y = rui.y;
            Check(g.CanStand(g.Position), "Rui occupies real walkable town terrain");
            Point outsideRui=StandableOutsidePhysicalRadius(g,rui,2.2001f);
            g.save.x=outsideRui.x;g.save.y=outsideRui.y;
            g.Talk(rui);
            Check(g.mode!="dialog",
                "A direct Talk cannot open the counterpoint dialogue outside physical reach");
            g.Close();
            g.save.x=rui.x;g.save.y=rui.y;
            g.Talk(rui);
            Check(!Array.Exists(g.conversation.choices,
                c => c.action == "root_harmony_accept"),
                "The player must first rescue the scout, preserving the opening story");
            g.Close(); g.save.flags.Add("scout_saved");
            g.Talk(rui);
            int staleAccept=Array.FindIndex(g.conversation.choices,
                c=>c.action=="root_harmony_accept");
            Check(staleAccept>=0,"Rui offers the real quest acceptance while nearby");
            g.save.x=outsideRui.x;g.save.y=outsideRui.y;
            g.Choose(staleAccept);g.Close();
            Check(!g.Has("root_harmony_started"),
                "A stale counterpoint-accept choice cannot start the quest remotely");
            g.save.x=rui.x;g.save.y=rui.y;
            Action(g, "rootlistener", "root_harmony_accept");
            Check(g.Has("root_harmony_started") &&
                !g.GetQuest("root_harmony").locked &&
                g.save.trackedQuest == "root_harmony",
                "Rui's actual dialogue starts a new tracked quest");
            // Unlock the original chapter travel gates for isolated optional
            // content coverage, without awarding story-boss completions.
            g.save.stage = 3; g.save.level = 10;
            g.Enter(2); g.enemies.Clear();
            WorldObject premature = Object(g, "rootcounter2");
            g.save.x = premature.x; g.save.y = premature.y;
            g.Talk(premature);
            Check(!Array.Exists(g.conversation.choices,
                c => c.action.StartsWith("root_harmony_tune_")),
                "The second region cannot skip the woodland reading");
            g.Close();
            int coins = g.save.coins, shards = g.save.shards,
                talent = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject node = Object(g, "rootcounter" + region);
                g.save.x = node.x; g.save.y = node.y;
                Check(g.CanStand(g.Position) && g.Nearest() == node,
                    "Each brand-new listening stone is a physical, reachable in-world object");
                Check(g.GetQuest("root_harmony").room == region,
                    "The live quest guide points to the next unfinished region");
                int right = region == 1 ? 2 : region == 2 ? 0 : 1;
                int wrong = (right + 1) % 3;
                Point outsideNode=new Point();
                if(region==1)
                {
                    outsideNode=StandableOutsidePhysicalRadius(g,node,2.2001f);
                    g.Talk(node);
                    int staleTune=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="root_harmony_tune_1_"+wrong);
                    Check(staleTune>=0,"A reachable first resonance node offers its true tuning choices");
                    float energyBeforeStaleTune=g.save.energy;
                    g.save.x=outsideNode.x;g.save.y=outsideNode.y;
                    g.Choose(staleTune);g.Close();
                    Check(g.RootHarmonyRoom==0 && !g.Has("root_harmony_1") &&
                        Math.Abs(g.save.energy-energyBeforeStaleTune)<.001f,
                        "A stale tuning choice cannot start combat or spend energy outside physical reach");
                    g.save.x=node.x;g.save.y=node.y;
                }
                float energy = g.save.energy;
                SetOuterPatrolLull(g,region);
                Check(g.OuterPatrolActive&&g.SafeTravel,
                    "The counterpoint node is locally safe during an unrelated patrol lull");
                Action(g,node.kind,"root_harmony_tune_"+region+"_"+right);
                Check(g.RootHarmonyRoom==0&&!g.enemies.Exists(g.IsRootHarmonyEnemy)&&
                    Math.Abs(g.save.energy-energy)<.001f,
                    "Root resonance cannot start its 40-second defense over other fieldwork");
                ClearOuterPatrolLull(g);
                Action(g, node.kind, "root_harmony_tune_" + region + "_" + wrong);
                Check(!g.Has("root_harmony_" + region) &&
                    g.RootHarmonyRoom == 0 && Math.Abs(g.save.energy - (energy - 8)) < .001f,
                    "An incorrect landscape note costs real energy without auto-completing a region");
                if (region == 1)
                {
                    Action(g, node.kind, "root_harmony_tune_1_2");
                    Check(g.RootHarmonyRoom == 1 &&
                        g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3,
                        "Tuning creates three real sentinels with a live 40-second encounter");
                    // A save taken during combat may preserve normal patrols,
                    // but must never resurrect transient quest sentinels as
                    // ordinary loot-bearing enemies or fake a completion.
                    g.PrepareSave();
                    Check(g.save.worldEnemies != null &&
                        !Array.Exists(g.save.worldEnemies, e =>
                            e.id >= 88000 && e.id < 88100),
                        "Encounter sentinels are excluded from persistent ordinary enemy snapshots");
                    var restarted = new GameCore(data, CloneSave(g.save));
                    restarted.Start();
                    Check(restarted.RootHarmonyRoom == 0 &&
                        !restarted.Has("root_harmony_1") &&
                        !restarted.enemies.Exists(restarted.IsRootHarmonyEnemy),
                        "Loading during battle can retry but cannot skip or duplicate special enemies");
                    // Expire the encounter through real world ticks; do
                    // not expose a public setter for quest timers just for tests.
                    foreach (Enemy sentinel in g.enemies)
                        if (g.IsRootHarmonyEnemy(sentinel)) sentinel.stun = 60f;
                    for (int frame = 0; frame < 810 && g.RootHarmonyRoom != 0; frame++)
                        g.Tick(.05f, new Point());
                    Check(g.RootHarmonyRoom == 0 &&
                        !g.enemies.Exists(g.IsRootHarmonyEnemy) &&
                        !g.Has("root_harmony_1"),
                        "Timer failure retires encounter enemies with no progress or bonus rewards");
                }
                Action(g, node.kind, "root_harmony_tune_" + region + "_" + right);
                Check(g.RootHarmonyRoom == region && g.RootHarmonyTime > 39 &&
                    g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3 &&
                    !g.Has("root_harmony_" + region),
                    "The correct note starts real timed combat, not instant flag collection");
                g.Talk(node);
                Check(!Array.Exists(g.conversation.choices, c =>
                    c.action == "root_harmony_seal_" + region),
                    "A live enemy must be defeated before reading can be sealed");
                g.Close();
                // The original combat engine owns collision, health, death
                // and attack windows. Isolate one actual sword strike.
                if (region == 1)
                {
                    Enemy sentinel = g.enemies.Find(g.IsRootHarmonyEnemy);
                    Point stand = new Point(sentinel.x - .65f, sentinel.y);
                    if (g.CanStand(stand))
                    {
                        g.save.x = stand.x; g.save.y = stand.y;
                        sentinel.stun = 5;
                        float hp = sentinel.hp;
                        g.Attack((sentinel.Position - g.Position).Normalized);
                        for (int i = 0; i < 13; i++) g.Tick(.05f, new Point());
                        Check(sentinel.hp < hp || !g.enemies.Contains(sentinel),
                            "A real player attack damages the spawned counterpoint enemy");
                    }
                }
                // For the multi-region reward contract, equivalent defeated
                // enemy state is supplied after the actual combat integration
                // above has been verified.
                g.enemies.RemoveAll(g.IsRootHarmonyEnemy);
                g.save.x = node.x; g.save.y = node.y;
                if(region==1)
                {
                    g.Talk(node);
                    int staleSeal=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="root_harmony_seal_"+region);
                    Check(staleSeal>=0,"A cleared node offers its genuine seal action");
                    int coinsBeforeStaleSeal=g.save.coins,shardsBeforeStaleSeal=g.save.shards;
                    g.save.x=outsideNode.x;g.save.y=outsideNode.y;
                    g.Choose(staleSeal);g.Close();
                    Check(!g.Has("root_harmony_"+region) &&
                        g.RootHarmonyRoom==region &&
                        g.save.coins==coinsBeforeStaleSeal &&
                        g.save.shards==shardsBeforeStaleSeal,
                        "A stale seal choice cannot publish a reading or reward from outside reach");
                    g.save.x=node.x;g.save.y=node.y;
                }
                Action(g, node.kind, "root_harmony_seal_" + region);
                Check(g.Has("root_harmony_" + region) && g.RootHarmonyRoom == 0 &&
                    g.save.coins == coins + 25 * region &&
                    g.save.shards == shards + 2 * region,
                    "The completed combat objective grants a strictly once-per-region reward");
                g.Talk(node);
                Check(!Array.Exists(g.conversation.choices,
                    c => c.action == "root_harmony_seal_" + region ||
                        c.action.StartsWith("root_harmony_tune_")),
                    "A sealed resonance node cannot spawn repeat paid combat or claim loot again");
                g.Close();
            }
            Check(!g.GetQuest("root_harmony").complete &&
                g.GetQuest("root_harmony").room == 0,
                "All three readings require a real return to the new town NPC");
            g.Enter(0); g.save.x = rui.x; g.save.y = rui.y;
            g.Talk(rui);
            int staleFinish=Array.FindIndex(g.conversation.choices,
                c=>c.action=="root_harmony_finish");
            Check(staleFinish>=0,"Rui offers final archiving after all three real readings");
            int coinsBeforeStaleFinish=g.save.coins,shardsBeforeStaleFinish=g.save.shards;
            int talentBeforeStaleFinish=g.save.skillPoints;
            g.save.x=outsideRui.x;g.save.y=outsideRui.y;
            g.Choose(staleFinish);g.Close();
            Check(!g.Has("root_harmony_complete") &&
                g.save.coins==coinsBeforeStaleFinish &&
                g.save.shards==shardsBeforeStaleFinish &&
                g.save.skillPoints==talentBeforeStaleFinish,
                "A stale counterpoint-finalization choice cannot grant rewards remotely");
            g.save.x=rui.x;g.save.y=rui.y;
            Action(g, "rootlistener", "root_harmony_finish");
            Check(g.Has("root_harmony_complete") &&
                g.GetQuest("root_harmony").complete &&
                g.save.skillPoints == talent + 1 &&
                g.save.coins == coins + 150 && g.save.shards == shards + 6,
                "Rui archives three physical readings and grants only the final talent reward");
            g.Talk(rui);
            Check(!Array.Exists(g.conversation.choices, c =>
                c.action == "root_harmony_finish"),
                "Completed counterpoint cannot be repeatedly turned in");
            g.Close(); g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.GetQuest("root_harmony").complete &&
                loaded.save.coins == g.save.coins &&
                loaded.save.skillPoints == g.save.skillPoints,
                "The new entire quest persists through existing version-two saves");
        }
    }
}
