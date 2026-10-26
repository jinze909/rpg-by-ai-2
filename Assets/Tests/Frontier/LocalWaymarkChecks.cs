using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore WaymarkFixture(Campaign data)
        {
            var rows=new string[34];
            for(int y=0;y<rows.Length;y++)
            {
                char[] cells=new string('#',34).ToCharArray();
                if(y>=2&&y<32)
                    for(int x=2;x<32;x++)cells[x]='.';
                rows[y]=new string(cells);
            }
            data.maps[1]=new MapData {id=1,spawnX=8.5f,spawnY=8.5f,
                rows=rows,objects=new WorldObject[0],
                decor=new Decoration[0]};
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            g.save.x=8.5f;g.save.y=8.5f;
            g.save.trackedQuest="main";g.save.showGuidance=false;
            return g;
        }

        private static Point ExplorationMarker(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return new Point(o.x,o.y);
            throw new Exception("Missing genuine exploration map object "+id);
        }


        // The three optional exploration loops contain independent tasks in
        // each region. The map pin/compass must select a LIVE local object
        // instead of repeatedly routing a player away from available work.
        public static void ThreeExplorationGuidesPrioritizeRealUnfinishedLocalSites(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            Check(g.save.room==1&&g.save.version==2,
                "The original unlocked Thornwood is a valid live test region");
            string[] quests={"trail_circuits","waytrails","annex_explorer"};
            string[] first={"trailcircuit_1_1","waytrail1s","annex_cache_1_a"};
            for(int i=0;i<quests.Length;i++)
            {
                g.save.trackedQuest=quests[i];
                var q=g.GetQuest(quests[i]);
                Check(!q.locked&&!q.complete&&q.room==1&&
                    (q.target-ExplorationMarker(g,first[i])).Length<.001f,
                    "The "+quests[i]+" guide must use a physical local landmark instead of early-region backtracking");
                Check((g.NavigationTarget().Value-q.target).Length<.001f,
                    "The world compass must follow the actual selected local quest marker");
            }
            g.save.flags.Add("trail_circuit_1_1");
            var route=g.GetQuest("trail_circuits");
            Check(route.room==1&&
                (route.target-ExplorationMarker(g,"trailcircuit_1_2")).Length<.001f,
                "After the first local bearing the second genuine map object is next");
            g.save.flags.Add("trail_circuit_1_2");
            route=g.GetQuest("trail_circuits");
            Check(route.room==1&&
                (route.target-ExplorationMarker(g,"trailcircuit_1_3")).Length<.001f,
                "Route stages remain sequential inside the current region");
            g.save.flags.Add("trail_circuit_1_3");
            Check(g.GetQuest("trail_circuits").room==0,
                "A completed local circuit resumes the first unfinished other region");
            g.save.flags.Add("waytrail_waytrail1s");
            var way=g.GetQuest("waytrails");
            Check(way.room==1&&(way.target-ExplorationMarker(g,"waytrail1e")).Length<.001f,
                "Second local historic bearing becomes the live target");
            g.save.flags.Add("waytrail_waytrail1e");
            Check(g.GetQuest("waytrails").room==0,
                "After both local historical bearings, broader exploration resumes");
            string[] annexFlags={"annex_cache_annex_cache_1_a",
                "annex_cache_annex_cache_1_b",
                "annex_vista_annex_vista_1",
                "annex_camp_annex_camp_1"};
            string[] remaining={"annex_cache_1_b","annex_vista_1","annex_camp_1"};
            for(int i=0;i<annexFlags.Length;i++)
            {
                g.save.flags.Add(annexFlags[i]);
                var atlas=g.GetQuest("annex_explorer");
                if(i<remaining.Length)
                    Check(atlas.room==1&&
                        (atlas.target-ExplorationMarker(g,remaining[i])).Length<.001f,
                        "The completed local cache/lookout/camp retargets to the next physical site");
                else
                    Check(atlas.room==0,
                        "Only when all four local sites are done does the atlas route away");
            }
            Check(g.save.version==2&&g.save.trackedQuest=="annex_explorer",
                "Exploration routing is computed from original flags; no new save schema or forced quest swap");
        }


        private static void CreditAllOutdoorSurveyFlags(GameCore g,int region)
        {
            for(int stage=1;stage<=3;stage++)
                g.save.flags.Add("trail_circuit_"+region+"_"+stage);
            g.save.flags.Add("waytrail_waytrail"+region+"s");
            g.save.flags.Add("waytrail_waytrail"+region+"e");
            g.save.flags.Add("annex_cache_annex_cache_"+region+"_a");
            g.save.flags.Add("annex_cache_annex_cache_"+region+"_b");
            g.save.flags.Add("annex_vista_annex_vista_"+region);
            g.save.flags.Add("annex_camp_annex_camp_"+region);
        }
        public static void LockedOutdoorSurveyRoutesToRealStoryBeforeAtlasPublication(Campaign data)
        {
            GameCore g=Fresh(data);
            if(!g.Has("intro"))g.save.flags.Add("intro");
            g.save.stage=0;
            CreditAllOutdoorSurveyFlags(g,0);
            CreditAllOutdoorSurveyFlags(g,1);
            Check(!g.CanEnter(2),"The ruins require the first story core");
            string[] quests={"trail_circuits","waytrails","annex_explorer"};
            QuestGuide main=g.GetQuest("main");
            foreach(string id in quests)
            {
                QuestGuide guide=g.GetQuest(id);
                Check(!guide.complete&&guide.room==main.room&&
                    (guide.target-main.target).Length<.001f&&
                    guide.next.Contains(data.maps[2].name),
                    "A locked inner region must use real campaign guidance, not a false atlas objective");
            }
            g.save.english=true;
            foreach(string id in quests)
                Check(g.GetQuest(id).next.Contains(data.maps[2].en),
                    "English side journal names the actual inaccessible region");
            g.save.stage=3;
            for(int room=2;room<=4;room++)CreditAllOutdoorSurveyFlags(g,room);
            Check(!g.CanEnter(5),"The distant shores need their own story invitation");
            QuestGuide shore=g.GetQuest("shore");
            foreach(string id in quests)
            {
                QuestGuide guide=g.GetQuest(id);
                Check(guide.room==shore.room&&
                    (guide.target-shore.target).Length<.001f&&
                    guide.next.Contains(data.maps[5].en),
                    "Before Lune's introduction, the side quest guides to the original farshore unlock");
            }
            g.save.flags.Add("exp_intro");
            CreditAllOutdoorSurveyFlags(g,5);
            Check(g.CanEnter(5)&&!g.CanEnter(6),
                "Harbor investigation still gates the desert region");
            shore=g.GetQuest("shore");
            foreach(string id in quests)
            {
                QuestGuide guide=g.GetQuest(id);
                Check(guide.room==shore.room&&
                    (guide.target-shore.target).Length<.001f&&
                    guide.next.Contains(data.maps[6].en),
                    "Unfinished desert exploration points through genuine harbor reporting");
            }
            CreditAllOutdoorSurveyFlags(g,6);
            CreditAllOutdoorSurveyFlags(g,7);
            QuestGuide atlas=g.GetQuest("annex_explorer");
            Point archive=new Point(18.5f,27.5f);
            foreach(WorldObject site in data.maps[9].objects)
                if(site.kind=="annexinnarchive")
                    archive=new Point(site.x,site.y);
            Check(atlas.room==9&&(atlas.target-archive).Length<.001f,
                "Only after all visited field sites are accounted for may the atlas direct publication");
            Check(g.save.version==2&&g.save.trackedQuest=="main",
                "World exploration unlock guidance neither changes SaveData v2 nor replaces the active quest");
        }


        public static void KeyboardAndGamepadMapCursorUsesRealPhysicalPinRules(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                GameCore g=WaymarkFixture(data);
                var cursor=new MapTileCursor();
                cursor.Begin(g.Position,g.Map.rows[0].Length,g.Map.rows.Length);
                Check(cursor.Active&&(cursor.Destination-g.Position).Length<.001f,
                    "Controller cursor begins at the actual hero tile, not a fake UI waypoint");
                cursor.Move(6,0);
                Check((cursor.Destination-new Point(14.5f,8.5f)).Length<.001f,
                    "Six directional presses select a precise, physically representable map cell");
                g.mode="map";
                Check(g.PlaceLocalWaymark(cursor.Destination)&&g.HasLocalWaymark,
                    "Confirm uses the original walking-reachable GameCore pin transaction");
                cursor.Move(1000000,-1000000);
                Check((cursor.Destination-new Point(33.5f,.5f)).Length<.001f &&
                    !g.PlaceLocalWaymark(cursor.Destination) &&
                    (g.LocalWaymark.Value-new Point(14.5f,8.5f)).Length<.001f,
                    "Controller cursor is bounded, but blocked border tiles cannot overwrite a valid pin");
                cursor.Cancel();
                Check(!cursor.Active,"Cancellation exits tile editing without erasing the actual player pin");
                cursor.Begin(new Point(float.NaN,1),34,34);
                Check(!cursor.Active,"Invalid input never creates a phantom editor cursor");
                g.Close();
                Check(!g.PlaceLocalWaymark(new Point(16.5f,8.5f)) &&
                    g.HasLocalWaymark && g.save.version==2,
                    "Map mode is required for all input types, with original SaveData unchanged");
            }
            finally{data.maps[1]=old;}
        }



        public static void HeldMapAxisAcceleratesWithoutSkippingNewTargets(Campaign data)
        {
            // Production FrontierInput feeds this exact cursor once per frame
            // for both manual walking pins and companion hold-post editing.
            var cursor=new MapTileCursor();
            cursor.Begin(new Point(8.5f,8.5f),120,80);
            Check(cursor.MoveAxis(1,0,.02f,true)&&
                (cursor.Destination-new Point(9.5f,8.5f)).Length<.001f,
                "A new analog direction moves exactly one tile for accurate nearby pin placement");
            for(int i=0;i<35;i++)
                Check(!cursor.MoveAxis(1,0,.02f,false),
                    "Debounced held frames never move a second tile or accept an accidental waypoint");
            Check(cursor.MoveAxis(1,0,.02f,true)&&
                (cursor.Destination-new Point(15.5f,8.5f)).Length<.001f,
                "Sustained axis travel jumps six tiles on large 120-wide maps");
            Check(!cursor.MoveAxis(0,0,.02f,false)&&
                cursor.MoveAxis(-1,0,.02f,true)&&
                (cursor.Destination-new Point(14.5f,8.5f)).Length<.001f,
                "Releasing and reversing immediately restores single-cell precision");
            for(int i=0;i<38;i++)cursor.MoveAxis(-1,0,.02f,false);
            Check(cursor.MoveAxis(-1,0,.02f,true)&&
                (cursor.Destination-new Point(8.5f,8.5f)).Length<.001f,
                "Held reverse direction independently earns accelerated travel");
            cursor.Cancel();
            cursor.Begin(new Point(119.5f,79.5f),120,80);
            Check(!cursor.MoveAxis(1,0,.8f,true)&&
                (cursor.Destination-new Point(119.5f,79.5f)).Length<.001f,
                "Fast axis input is clamped at the real map edge without false movement");
            cursor.Begin(new Point(8.5f,8.5f),120,80);
            Check(cursor.MoveAxis(0,-1,.02f,true)&&
                (cursor.Destination-new Point(8.5f,7.5f)).Length<.001f,
                "Reopening a different map edit never inherits a previous long hold");
            cursor.Cancel();
            Check(!cursor.MoveAxis(1,0,.02f,true)&&!cursor.Active,
                "Inactive map selection cannot silently mutate a pin or hold-post target");
            cursor.Begin(new Point(50.5f,40.5f),120,80);
            Check(cursor.MoveAxis(1,0,.02f,true)&&
                (cursor.Destination-new Point(51.5f,40.5f)).Length<.001f,
                "A cancelled companion editor never leaks held acceleration into a fresh personal pin");
            cursor.MoveAxis(0,0,float.NaN,false);
            Check(cursor.MoveAxis(0,1,.02f,true)&&
                (cursor.Destination-new Point(51.5f,41.5f)).Length<.001f,
                "Neutral or malformed frame input clears repeat momentum without skipping a map row");
        }

        public static void WaymarkPreviewUsesSameFullBodyRouteAndNeverCommitsGameplay(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                GameCore g=WaymarkFixture(data);g.mode="map";
                var points=new System.Collections.Generic.List<Point>();
                Point available=new Point(16.8f,8.1f);
                int flags=g.save.flags.Count;
                Check(g.InspectLocalWaymark(available,points)==WaymarkRouteStatus.Available &&
                    points.Count>0 &&
                    (points[points.Count-1]-new Point(16.5f,8.5f)).Length<.001f &&
                    !g.HasLocalWaymark && flags==g.save.flags.Count,
                    "Read-only route preview reports real physical finish, preserving original quest and pin state");
                Check(g.PlaceLocalWaymark(available)&&g.LocalWaymark.HasValue,
                    "The same inspected point can be committed via the physical pin API");
                Point pin=g.LocalWaymark.Value;
                Point wall=new Point(0.5f,8.5f);
                Check(g.InspectLocalWaymark(wall,points)==WaymarkRouteStatus.DestinationBlocked &&
                    points.Count==0&&(g.LocalWaymark.Value-pin).Length<.001f,
                    "A visible wall may not masquerade as an available controller route");
                for(int y=2;y<32;y++)
                {
                    char[] row=g.Map.rows[y].ToCharArray();
                    row[13]='#';g.Map.rows[y]=new string(row);
                }
                Check(g.CanStand(new Point(18.5f,8.5f))&&
                    g.InspectLocalWaymark(new Point(18.5f,8.5f),points)==WaymarkRouteStatus.NoWalkingRoute &&
                    points.Count==0&&(g.LocalWaymark.Value-pin).Length<.001f,
                    "A separately standable island is reported as unreachable before confirmation; preview never erases the pin");
                Check(g.InspectLocalWaymark(new Point(float.NaN,2),points)==WaymarkRouteStatus.InvalidRequest&&
                    g.InspectLocalWaymark(new Point(-1,2),points)==WaymarkRouteStatus.InvalidRequest,
                    "Corrupt or out-of-map inputs never fabricate preview coordinates");
                char[] home=g.Map.rows[8].ToCharArray();home[8]='#';
                g.Map.rows[8]=new string(home);
                Check(g.InspectLocalWaymark(new Point(11.5f,8.5f),points)==WaymarkRouteStatus.HeroFootingBlocked&&
                    points.Count==0,"Sealed hero footing rejects routes before BFS");
                home[8]='.';g.Map.rows[8]=new string(home);
                Check(g.InspectLocalWaymark(new Point(11.5f,8.5f),points)==WaymarkRouteStatus.Available,
                    "Reopening hero footing restores physically reachable local predictions");
                Check(g.save.version==2&&g.save.trackedQuest=="main"&&
                    g.save.flags.Count==flags,
                    "Inspecting and retesting physical map routes never changes v2 save schema or narrative flags");
            }
            finally{data.maps[1]=old;}
        }

        public static void PlayerWaymarkIsRealReachableGoalAndClearsOnWalkingArrival(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);var point=new Point(17.5f,8.5f);
                Check(!g.PlaceLocalWaymark(point)&&!g.HasLocalWaymark,
                    "Walking gameplay cannot secretly place a map-only pin");
                g.mode="map";
                Check(g.PlaceLocalWaymark(new Point(17.19f,8.72f))&&
                    g.HasLocalWaymark &&
                    (g.LocalWaymark.Value-point).Length<.001f,
                    "Map click snaps to walkable tile center and validates a physical route");
                Check(g.save.trackedQuest=="main"&&g.save.version==2,
                    "One personal pin cannot replace the journal's tracked quest or save schema");
                g.Close();
                Check(g.PersonalNavigationActive&&
                    (g.NavigationTarget().Value-point).Length<.001f,
                    "Pinned route wins over normal quest navigation even when quest guidance is off");
                var path=g.NavigationPath(g.LocalWaymark.Value);
                Check(path.Count>0&&
                    (path[path.Count-1]-point).Length<1.25f,
                    "Existing collision-aware BFS guides the actual hero toward the selected cell");
                int count=0;
                while(g.HasLocalWaymark&&count++<90)
                    g.Tick(.05f,new Point(1,0));
                Check(!g.HasLocalWaymark&&!g.PersonalNavigationActive&&
                    g.save.trackedQuest=="main"&&count<90,
                    "Walking the real physical path removes the pin once, without completing a story quest");
            }
            finally{data.maps[1]=old;}
        }
        public static void WaymarkRejectsWallsIslandsInvalidNumbersAndPreservesOldPin(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);g.mode="map";
                Check(g.PlaceLocalWaymark(new Point(13.5f,8.5f)),
                    "Original reachable local pin is accepted");
                Point before=g.LocalWaymark.Value;
                char[] wall=g.Map.rows[8].ToCharArray();
                wall[18]='#';g.Map.rows[8]=new string(wall);
                Check(!g.PlaceLocalWaymark(new Point(18.2f,8.2f))&&
                    (g.LocalWaymark.Value-before).Length<.001f,
                    "Blocked map tile cannot erase a previously valid personal pin");
                for(int x=20;x<=26;x++)
                {
                    char[] north=g.Map.rows[20].ToCharArray();north[x]='#';
                    g.Map.rows[20]=new string(north);
                    char[] south=g.Map.rows[26].ToCharArray();south[x]='#';
                    g.Map.rows[26]=new string(south);
                }
                for(int y=20;y<=26;y++)
                {
                    char[] border=g.Map.rows[y].ToCharArray();
                    border[20]='#';border[26]='#';
                    g.Map.rows[y]=new string(border);
                }
                Point island=new Point(23.5f,23.5f);
                Check(g.CanStand(island)&&
                    g.NavigationPathFrom(g.Position,island,true).Count==0,
                    "Sealed courtyard has legitimate local footing but no continuous physical route");
                Check(!g.PlaceLocalWaymark(island)&&
                    (g.LocalWaymark.Value-before).Length<.001f,
                    "A surrounded standing tile cannot be selected as a fictitious path target");
                foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,
                    float.NegativeInfinity,-100f,100000f})
                    Check(!g.PlaceLocalWaymark(new Point(invalid,5))&&
                        (g.LocalWaymark.Value-before).Length<.001f,
                        "Corrupt or out-of-map click cannot overwrite the last valid destination");
                Check(g.ClearLocalWaymark()&&!g.ClearLocalWaymark()&&
                    !g.HasLocalWaymark,
                    "Clear action is idempotent, owns no quest flags or inventory");
            }
            finally{data.maps[1]=old;}
        }
        public static void RealCivilianDefenseOutranksPersonalPinWithoutErasingIt(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            WorldObject traveler=Object(g,"wayfarer1");
            g.save.x=traveler.x;g.save.y=traveler.y;
            g.save.showGuidance=false;g.save.trackedQuest="wayfarers";
            Point chosen=new Point();bool found=false;
            for(int y=4;y<g.Map.rows.Length-4&&!found;y++)
            for(int x=4;x<g.Map.rows[y].Length-4&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if((p-new Point(traveler.x,traveler.y)).Length<7f||
                    !g.CanStand(p)||
                    g.NavigationPathFrom(g.Position,p,true).Count==0)continue;
                chosen=p;found=true;
            }
            Check(found,"The original live rescue region offers a reachable waypoint away from the traveler");
            g.mode="map";
            Check(g.PlaceLocalWaymark(chosen),
                "Original regional collision validates a real personal foot target");
            g.Close();
            Check(g.PersonalNavigationActive &&
                (g.NavigationTarget().Value-chosen).Length<.001f,
                "Personal pin temporarily outranks nonurgent saved journal tracking");
            Action(g,traveler.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1&&
                g.enemies.FindAll(e=>e.id>=82010&&e.id<=82011).Count==2,
                "Authentic civilian combat activates with both live attackers");
            Point? urgent=g.NavigationTarget();
            Check(g.HasLocalWaymark&&!g.PersonalNavigationActive&&
                urgent.HasValue&&(urgent.Value-chosen).Length>2f,
                "Timed real-world defense takes priority without deleting the player's own pin or saved quest");
            g.Talk(traveler);
            int abandon=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="wayfarer_abandon")
                {abandon=i;break;}
            Check(abandon>=0,"The actual civilian provides voluntary withdrawal");
            g.Choose(abandon);
            Check(g.WayfarerEncounterRoom==0&&g.PersonalNavigationActive&&
                (g.NavigationTarget().Value-chosen).Length<.001f&&
                g.save.trackedQuest=="wayfarers",
                "After the safe physical withdrawal, original personal walking guidance resumes");
        }


        public static void LocalWaymarkSurvivesClosedRoadAndResumesWhenPassageReopens(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);
                Point goal=new Point(18.5f,8.5f);
                g.mode="map";
                Check(g.PlaceLocalWaymark(goal),"A walkable original destination accepts a physical pin");
                g.Close();
                // Seal every walkable row of the middle corridor. The goal
                // remains a real, standable tile, but it cannot be reached.
                for(int y=2;y<32;y++)
                {
                    char[] row=g.Map.rows[y].ToCharArray();
                    row[13]='#';
                    g.Map.rows[y]=new string(row);
                }
                for(int i=0;i<30;i++)g.Tick(.05f,new Point());
                Check(g.LocalWaymarkRouteBlocked && g.HasLocalWaymark &&
                    (g.LocalWaymark.Value-goal).Length<.001f &&
                    g.NavigationPathFrom(g.Position,goal,true).Count==0,
                    "A newly sealed route is visibly suspended without discarding the player's pin");
                // Reopen one physically traversable gate. The same original
                // destination becomes navigable; no user re-pin is required.
                char[] opened=g.Map.rows[8].ToCharArray();opened[13]='.';
                g.Map.rows[8]=new string(opened);
                for(int i=0;i<30;i++)g.Tick(.05f,new Point());
                Check(!g.LocalWaymarkRouteBlocked && g.HasLocalWaymark &&
                    g.PersonalNavigationActive &&
                    g.NavigationPathFrom(g.Position,goal,true).Count>0,
                    "A reopened corridor reactivates the existing pin and the real walking route");
                g.mode="map";
                Check(g.ClearLocalWaymark()&&!g.LocalWaymarkRouteBlocked,
                    "Clearing a recovered pin also clears transient blocked-route state");
            }
            finally{data.maps[1]=old;}
        }
        public static void LocalWaymarkRetainsBlockedDestinationUntilTideOrGateChanges(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);
                Point goal=new Point(15.5f,8.5f);
                g.mode="map";
                Check(g.PlaceLocalWaymark(goal),"Original target is walkable before dynamic obstacle");
                g.Close();
                char[] blocked=g.Map.rows[8].ToCharArray();blocked[15]='#';
                g.Map.rows[8]=new string(blocked);
                for(int i=0;i<30;i++)g.Tick(.05f,new Point());
                Check(g.HasLocalWaymark && g.LocalWaymarkRouteBlocked &&
                    !g.CanStand(goal),
                    "Blocked destination is retained without inventing a reachable walking route");
                blocked[15]='.';g.Map.rows[8]=new string(blocked);
                for(int i=0;i<30;i++)g.Tick(.05f,new Point());
                Check(g.HasLocalWaymark&&!g.LocalWaymarkRouteBlocked &&
                    g.CanStand(goal),
                    "When destination footing reopens, the original waypoint becomes active again");
            }
            finally{data.maps[1]=old;}
        }


        public static void SealedFootingCannotForgeAPhysicalNavigationRoute(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);
                Point destination=new Point(16.5f,8.5f);
                Check(g.NavigationPathFrom(g.Position,destination,true).Count>0,
                    "The original walkable corridor has a real route");
                char[] row=g.Map.rows[8].ToCharArray();
                row[8]='#';g.Map.rows[8]=new string(row);
                Check(!g.CanStand(g.Position) &&
                    g.NavigationPathFrom(g.Position,destination,true).Count==0 &&
                    g.NavigationPathFrom(g.Position,destination,false).Count==0,
                    "Both strict escorts and guidance refuse routes originating under sealed footing");
                g.mode="map";
                Check(!g.PlaceLocalWaymark(destination)&&!g.HasLocalWaymark,
                    "A player sealed into terrain cannot forge a new route through the map UI");
                row[8]='.';g.Map.rows[8]=new string(row);
                Check(g.CanStand(g.Position)&&
                    g.PlaceLocalWaymark(destination) &&
                    g.NavigationPathFrom(g.Position,destination,true).Count>0,
                    "Restoring walkable feet restores legitimate route planning without a save migration");
            }
            finally{data.maps[1]=old;}
        }

        public static void WaymarkNeverPersistsAcrossRegionTravelOrSelectedSave(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var g=WaymarkFixture(data);g.mode="map";
                Check(g.PlaceLocalWaymark(new Point(13.5f,8.5f)),
                    "A legal world map pin exists before save/load boundary");
                var save=CloneSave(g.save);g.PrepareSave();
                var reloaded=new GameCore(data,save);
                reloaded.Start();
                Check(!reloaded.HasLocalWaymark&&
                    reloaded.save.version==2&&
                    reloaded.save.trackedQuest=="main",
                    "Old v2 save remains unchanged and transient map destinations never reappear");
                g.Close();
                g.Enter(0);
                Check(!g.HasLocalWaymark&&
                    g.save.room==0&&g.save.trackedQuest=="main",
                    "Changing regions destroys the old local marker without altering journal tracking");
                g.Enter(1);
                Check(!g.HasLocalWaymark&&!g.PersonalNavigationActive&&
                    g.save.version==2&&g.save.trackedQuest=="main",
                    "Returning to the same region does not resurrect a stale navigation command");
            }
            finally{data.maps[1]=old;}
        }
    }
}
