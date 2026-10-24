using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static string[] RescueDetourArena(bool lowerGap, bool upperGap)
        {
            string[] rows = new string[18];
            for (int y = 0; y < rows.Length; y++)
            {
                char[] tiles = new string('.', 18).ToCharArray();
                for (int x = 0; x < tiles.Length; x++)
                    if (x < 2 || x >= 16 || y < 2 || y >= 16)
                        tiles[x] = '#';
                if (y >= 2 && y < 16 && !(lowerGap && y == 13) &&
                    !(upperGap && y == 4)) tiles[8] = '#';
                rows[y] = new string(tiles);
            }
            return rows;
        }
        private static GameCore RescueDetourGame(Campaign data, string[] rows)
        {
            var game = Fresh(data);
            game.save.stage = 4;
            game.save.frontierEnding = 3;
            game.save.flags.Add("exp_intro");
            game.save.flags.Add("harbor_report");
            game.save.flags.Add("farshore_relief_6");
            game.Enter(6);
            game.mode = "world";
            game.enemies.Clear();
            data.maps[6] = new MapData {
                id = 6, spawnX = 5.5f, spawnY = 8.5f,
                rows = rows, objects = new WorldObject[0],
                decor = new Decoration[0]
            };
            game.save.x = 11.5f;
            game.save.y = 8.5f;
            game.save.teaUntil = game.save.totalTime + 300;
            game.save.health = game.save.maxHealth;
            game.reliefMissionRoom = 6;
            game.reliefMissionWave = 3;
            game.reliefMissionTime = 110f;
            game.reliefFollowerPosition = new Point(5.5f, 8.5f);
            game.reliefFollowerSafety = 85f;
            return game;
        }
        public static void EscortWaitsAtClosedWallThenTraversesReopenedRealDetour(Campaign data)
        {
            MapData old = data.maps[6];
            try
            {
                string[] rows = RescueDetourArena(false, false);
                var game = RescueDetourGame(data, rows);
                Point initial = game.reliefFollowerPosition;
                for (int i=0;i<32;i++) game.Tick(.05f,new Point());
                Check(game.ReliefEscortActive && game.ReliefEscortRouteStalled &&
                      (game.reliefFollowerPosition-initial).Length < .001f,
                    "A sealed wall stalls a real civilian rather than hiding a teleport or invalid route");
                // Open a distant route at the far lower end of the dividing wall.
                char[] doorway = rows[13].ToCharArray();
                doorway[8]='.'; rows[13]=new string(doorway);
                float traveled = 0;
                int frames = 0;
                for (;frames<560 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;frames++)
                {
                    Point prior = game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next = game.reliefFollowerPosition;
                    float segment = (next-prior).Length;
                    Check(game.CanStand(next) && segment<.24f,
                        "Replanned civilian route uses legal actor feet and incremental swept walking");
                    if (next.x>8.3f)
                        Check(next.y>12.2f,
                            "Civilian physically crosses the wall only at the reopened lower passage");
                    traveled += segment;
                }
                Check(game.ReliefEscortActive &&
                      (game.Position-game.reliefFollowerPosition).Length<1.7f &&
                      !game.ReliefEscortRouteStalled && traveled>12f,
                    "Reopened route completes a genuinely longer walk and clears the blocked warning");
            }
            finally { data.maps[6]=old; }
        }

        public static void EscortReplansWhenTheOldGateClosesAndAnotherOpens(Campaign data)
        {
            MapData old = data.maps[6];
            try
            {
                string[] rows = RescueDetourArena(true, false);
                var game = RescueDetourGame(data, rows);
                for (int i=0;i<34;i++) game.Tick(.05f,new Point());
                Check(game.reliefFollowerPosition.y>8.8f,
                    "A civilian initially follows the real lower detour with a committed route");
                char[] closed=rows[13].ToCharArray(); closed[8]='#';
                rows[13]=new string(closed);
                char[] opened=rows[4].ToCharArray(); opened[8]='.';
                rows[4]=new string(opened);
                float minY=game.reliefFollowerPosition.y;
                for(int i=0;i<620 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;i++)
                {
                    Point prior=game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next=game.reliefFollowerPosition;
                    minY=Math.Min(minY,next.y);
                    Check(game.CanStand(next) && (next-prior).Length<.24f,
                        "Switching real gates never clips or teleports the escorted resident");
                    if(next.x>8.3f)
                        Check(next.y<5.9f,
                            "After a gate closes, the resident crosses only the newly opened upper passage");
                }
                Check(minY<5.8f &&
                      (game.Position-game.reliefFollowerPosition).Length<1.7f &&
                      game.ReliefEscortActive,
                    "Live gate collision invalidates old route and recovers via the new detour");
            }
            finally { data.maps[6]=old; }
        }

        public static void CivilianFullFootprintRejectsCornerGrazingCenterRays(Campaign data)
        {
            MapData old=data.maps[6];
            try
            {
                string[] rows=RescueDetourArena(true,true);
                // Only a single stone corner may catch the BODY: the thin
                // horizontal ray above it remains entirely unobstructed.
                for(int y=2;y<16;y++)
                {
                    char[] clear=rows[y].ToCharArray(); clear[8]='.';
                    rows[y]=new string(clear);
                }
                char[] wall=rows[8].ToCharArray(); wall[8]='#';
                rows[8]=new string(wall);
                var game=RescueDetourGame(data,rows);
                Point from=new Point(6.5f,7.85f),to=new Point(10.5f,7.85f);
                Check(game.CanStand(from) && game.CanStand(to),
                    "The two actor endpoints are independently safe in the corner fixture");
                var flags=System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance;
                var thin=typeof(GameCore).GetMethod("ClearAttackPath",flags);
                var full=typeof(GameCore).GetMethod("ReliefActorLineWalkable",flags);
                Check((bool)thin.Invoke(game,new object[]{from,to}) &&
                    !(bool)full.Invoke(game,new object[]{from,to}),
                    "Projectile centerline is clear but the civilian footprint genuinely hits corner stone");
                game.reliefFollowerPosition=from;
                game.save.x=to.x;game.save.y=to.y;
                float walked=0f;
                for(int i=0;i<270 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;i++)
                {
                    Point prior=game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next=game.reliefFollowerPosition;
                    Check(game.CanStand(next) && (next-prior).Length<.24f,
                        "Actual corner detour never enters a tile blocked to the whole actor");
                    walked+=(next-prior).Length;
                }
                Check(walked>3.5f && (game.Position-game.reliefFollowerPosition).Length<1.7f,
                    "Real civilian routes around a corner which only a point-like center ray could clear");
            }
            finally { data.maps[6]=old; }
        }
    }
}
