using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Runs against the real Campaign.json and production collision,
        // traversal, actor placement and original chapter sources.
        public static void FourDifferentCampaignLevelsRetainRealOldAndNewWalkingRoutes(Campaign data)
        {
            string[][] originalMustReach={
                new[]{"chapterroad_1_0","chapterroad_1_1","chapterroad_1_2",
                    "scout","wayfarer1","witnessnpc1","wood1","shrine1",
                    "waytrail1e","rootsong_1","outer_watch_1","far_vista_1"},
                new[]{"chapterroad_2_0","chapterroad_2_1","chapterroad_2_2",
                    "rune0","rune1","rune2","wayfarer2","shrine2",
                    "waytrail2e","floodarchive_1","floodarchive_2","outer_watch_2"},
                new[]{"chapterroad_3_0","chapterroad_3_1","chapterroad_3_2",
                    "engine","witnessnpc3","ironfoundry","shrine3",
                    "pressureworks_notice","pressureworks_valve1",
                    "pressureworks_valve2","pressureworks_engine"},
                new[]{"chapterroad_4_0","chapterroad_4_1","chapterroad_4_2",
                    "triadnexus","blackoutshelter","shrine4",
                    "waytrail4e","bridge_heart","silentledger_notice",
                    "silentledger_console","outer_watch_4"}
            };
            var levelSignatures=new HashSet<string>();
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.Enter(room);
                Check(g.save.room==room&&g.CanStand(g.Position)&&
                    g.Map.rows.Length==80&&g.Map.rows[0].Length==120,
                    "Every original chapter still enters its authored 120 by 80 game world");
                Check(g.enemies.Count==(room<=2?6:8),
                    "Original mandatory live combat roster survives authored level redesign");
                // Distinct original map geography now supports distinct
                // real combat roles, instead of the same 0/1/2 pattern.
                int[][] chapterKinds={
                    new[]{0,0,1,0,1,0},
                    new[]{1,0,1,2,1,0},
                    new[]{2,0,2,1,2,0,2,1},
                    new[]{2,1,0,2,1,0,2,1}
                };
                int rosterIndex=0;
                foreach(Enemy foe in g.enemies)
                {
                    Check(rosterIndex<chapterKinds[room-1].Length&&
                        foe.kind==chapterKinds[room-1][rosterIndex],
                        "Different terrain now produces a deliberately distinct live chapter battle composition");
                    rosterIndex++;
                }
                Check(rosterIndex==chapterKinds[room-1].Length,
                    "Biome-specific tactical variety preserves the mandatory six/eight-actor campaign roster");
                Point regionSpawn=g.Position;
                foreach(Enemy enemy in g.enemies)
                {
                    Check(enemy.hp>0&&g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(regionSpawn,enemy.Position,true).Count>0,
                        "All original mandatory enemies occupy connected, physically walkable regional ground");
                    foreach(WorldObject prop in g.Map.objects)
                    {
                        float pad=(enemy.Position-new Point(prop.x,prop.y)).Length;
                        Check(pad>=1.9f,
                            "Regional fighters do not block original NPC, resource or chapter investigation props");
                        if(prop.kind=="chapterroad")
                            Check(pad>=4.15f,
                                "Mandatory regional monsters cannot camp a chapter rescue or survivor conversation");
                    }
                }
                // The original seal/barrier prerequisites still control the
                // Boss arena. Decorative new paths must not bypass them.
                if(room<=3)
                {
                    Check(!g.GateOpen,"Reauthored world retains an authentic closed chapter gate");
                    g.save.flags.Add(room==1?"scout_saved":"gate_"+room);
                    Check(g.GateOpen,"Existing old-story solution still unlocks the same guardian");
                }
                foreach(string id in originalMustReach[room-1])
                {
                    WorldObject point=Array.Find(g.Map.objects,o=>o.id==id);
                    Check(point!=null,"No accepted original or new map object was removed: "+id);
                    Point foot=new Point(point.x,point.y);
                    Check(g.CanStand(foot),
                        "Authored "+g.Map.name+" landmark is not buried in tree, flood or slag: "+id);
                    Check(g.NavigationPathFrom(regionSpawn,foot,true).Count>0,
                        "Real navigation must connect the original region entrance to "+id);
                }
                // Every chapter rescue is a real three-location journey.
                for(int n=0;n<2;n++)
                {
                    WorldObject a=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+n);
                    WorldObject b=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+(n+1));
                    Check(a!=null&&b!=null&&
                        (new Point(a.x,a.y)-new Point(b.x,b.y)).Length>=4f,
                        "Original civilian stories visit separate, authored physical locations");
                    Check(g.NavigationPathFrom(new Point(a.x,a.y),
                        new Point(b.x,b.y),true).Count>0,
                        "New cover must not make the actual three-stop rescue impossible");
                }
                // The four places must not be the same atlas recolored.
                // Compare a small and remote zone's real terrain pattern.
                string signature="";
                for(int y=4;y<=21;y++)
                    signature+=g.Map.rows[y].Substring(4,30);
                for(int y=55;y<70;y+=3)
                    signature+=g.Map.rows[y].Substring(80,28);
                Check(levelSignatures.Add(signature),
                    "Each chapter has unique traversable geography across the early and remote world");
            }
            // Keep the thematic changes literal, not four generic biome
            // recolors: trees, flooded islands, slag, and fortress walls.
            Check(data.maps[1].rows[4][25]=='#' &&
                data.maps[2].rows[5][24]=='~' &&
                data.maps[3].rows[5][24]=='#' &&
                data.maps[4].rows[20][30]=='#',
                "Forest roots, Ruins flood, Mine slag and Citadel barricades have physically authored distinct topology");
        }
        public static void LegacyV2SavesInsideNewAuthoringCoverRecoverWithoutLosingCampaign(Campaign data)
        {
            // These four tiles were originally walkable empty ground. They
            // now form real roots, water, slag and an outer wall. A veteran's
            // saved character may stand on any of them: repair safely rather
            // than deleting the world, resetting the plot or moving to town.
            int[,] covers={{25,4},{24,5},{24,5},{30,20}};
            char[] terrain={'#','~','#','#'};
            for(int room=1;room<=4;room++)
            {
                GameCore old=Fresh(data);old.save.stage=3;
                old.save.version=2;old.save.room=room;
                old.save.x=covers[room-1,0]+.5f;
                old.save.y=covers[room-1,1]+.5f;
                old.save.health=52f;old.save.inventory[0]=5;
                old.save.flags.Add("chapter_road_done_"+room);
                old.save.snapshotRoom=-1;old.save.worldEnemies=null;
                Check(data.maps[room].rows[covers[room-1,1]][covers[room-1,0]]==
                    terrain[room-1],
                    "The deliberately authored new local cover is genuinely impassable");
                GameCore recovered=new GameCore(data,CloneSave(old.save));
                Check(recovered.save.version==2&&recovered.save.room==room&&
                    recovered.CanStand(recovered.Position),
                    "Old v2 world spawn inside newly sculpted terrain recovers to authentic walkable ground");
                Check(recovered.Has("chapter_road_done_"+room)&&
                    recovered.save.stage==3&&recovered.save.inventory[0]==5&&
                    recovered.save.health==52f,
                    "Level remodeling does not erase earned civilian records, supply, health or stage from an old save");
                Check((recovered.Position-new Point(old.save.x,old.save.y)).Length<8f,
                    "One newly blocked tile relocates the returning traveler nearby, never across the 120x80 world");
            }
        }
    }
}
