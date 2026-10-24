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
