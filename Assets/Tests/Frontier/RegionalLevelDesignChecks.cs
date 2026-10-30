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
                Point regionSpawn=g.Position;
                foreach(Enemy enemy in g.enemies)
                {
                    Check(enemy.hp>0&&g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(regionSpawn,enemy.Position,true).Count>0,
                        "All original mandatory enemies occupy connected, physically walkable regional ground");
                    foreach(WorldObject prop in g.Map.objects)
                        Check((enemy.Position-new Point(prop.x,prop.y)).Length>=1.9f,
                            "Regional fighters do not block original NPC, resource or chapter investigation props");
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
            Check(data.maps[1].rows[8][45]=='#' &&
                data.maps[2].rows[8][45]=='~' &&
                data.maps[3].rows[5][24]=='#' &&
                data.maps[4].rows[8][45]=='#',
                "Forest roots, Ruins flood, Mine slag and Citadel barricades have physically authored distinct topology");
        }
    }
}
