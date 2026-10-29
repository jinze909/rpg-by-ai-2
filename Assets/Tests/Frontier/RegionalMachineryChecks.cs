using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void PickRealMachineOption(GameCore g,int option)
        {
            Check(g.mode=="dialog"&&g.conversation!=null,
                "Regional engineering is a real physically sourced dialogue");
            string action="regional_machine_pick_"+option;
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action==action);
            Check(choice>=0,"Live machine must actually offer "+action);
            g.Choose(choice);
        }
        public static void EightDifferentRegionalEngineeringCircuitsRequireTwentyFourRealSites(Campaign data)
        {
            var distinct=new HashSet<string>();
            int[] expectedStart={1,0,2,1,2,0,1,2};
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);
                g.Close();g.save.room=room;g.enemies.Clear();
                MapData map=data.maps[room];
                int w=map.rows[0].Length,h=map.rows.Length;
                bool[] visited=new bool[w*h];
                var queue=new Queue<int>();
                int startX=(int)Math.Floor(map.spawnX),startY=(int)Math.Floor(map.spawnY);
                Check(g.CanStand(new Point(startX+.5f,startY+.5f)),
                    "Region entry stays walkable despite added devices");
                queue.Enqueue(startY*w+startX);visited[startY*w+startX]=true;
                int[] dx={-1,1,0,0},dy={0,0,-1,1};
                while(queue.Count>0)
                {
                    int n=queue.Dequeue(),x=n%w,y=n/w;
                    for(int k=0;k<4;k++)
                    {
                        int nx=x+dx[k],ny=y+dy[k];
                        if(nx<2||ny<2||nx>=w-2||ny>=h-2)continue;
                        int idx=ny*w+nx;
                        if(visited[idx]||
                            !g.CanStand(new Point(nx+.5f,ny+.5f)))continue;
                        visited[idx]=true;queue.Enqueue(idx);
                    }
                }
                var sites=new WorldObject[3];
                for(int station=0;station<3;station++)
                {
                    string id="regional_machine_"+room+"_"+station;
                    sites[station]=Array.Find(map.objects,o=>o.id==id);
                    WorldObject site=sites[station];
                    Check(site!=null&&site.kind=="regionalMachine"&&
                        site.name.Length>=4&&site.en.Length>9,
                        "Every district needs three meaningful original world devices");
                    int x=(int)site.x,y=(int)site.y;
                    Check(x>=2&&y>=2&&x<w-2&&y<h-2&&
                        site.x==x+.5f&&site.y==y+.5f&&
                        visited[y*w+x]&&g.CanStand(new Point(site.x,site.y)),
                        "Actual commissioned station must be reachable from its authentic regional spawn");
                    Check(distinct.Add(site.x+","+site.y),
                        "Every device has a unique physical coordinate across regions");
                }
                for(int i=0;i<3;i++)for(int k=i+1;k<3;k++)
                    Check((new Point(sites[i].x-sites[k].x,
                        sites[i].y-sites[k].y)).Length>17f,
                        "Three devices must be separated field destinations, not one stacked marker");
                g.save.x=sites[expectedStart[room]].x;
                g.save.y=sites[expectedStart[room]].y;
                g.Talk(sites[expectedStart[room]]);
                Check(g.mode=="dialog"&&Array.FindIndex(
                    g.conversation.choices,c=>
                    c.action=="regional_machine_pick_"+expectedStart[room])<0,
                    "Direct machine talk before original regional investigation cannot bypass the story");
                g.Close();g.save.flags.Add("biome_field_done_"+room);
                g.save.inventory[0]=30;g.save.inventory[2]=30;
                g.save.inventory[4]=30;g.save.energy=100f;
                Check(g.RegionalMachineNextIndex(room)==expectedStart[room],
                    "Each district has a distinct original field sequence");
                for(int step=0;step<3;step++)
                {
                    int next=g.RegionalMachineNextIndex(room);
                    Check(next>=0&&next<3&&
                        !g.Has("regional_machine_stage_"+room+"_"+next),
                        "Next true field step cannot be a previously repaired station");
                    WorldObject site=sites[next];
                    g.save.x=site.x;g.save.y=site.y;
                    g.Talk(site);
                    int materials=g.save.inventory[next==0?0:next==1?2:4];
                    float energy=g.save.energy;
                    PickRealMachineOption(g,(next+1)%3);
                    Check(g.RegionalMachineProgress(room)==step&&
                        g.save.inventory[next==0?0:next==1?2:4]==materials&&
                        g.save.energy==energy-6,
                        "Wrong physically selected apparatus calibration spends real energy but no materials or progress");
                    g.Talk(site);
                    PickRealMachineOption(g,next);
                    Check(g.RegionalMachineProgress(room)==step+1&&
                        g.Has("regional_machine_stage_"+room+"_"+next)&&
                        g.save.inventory[next==0?0:next==1?2:4]==materials-1,
                        "Correct live source calibration costs exactly one material and records only this station");
                    g.Talk(site);
                    Check(Array.FindIndex(g.conversation.choices,c=>
                        c.action=="regional_machine_pick_"+next)<0,
                        "A repaired station never sells its paid reward repeatedly");
                    g.Close();
                }
                Check(g.RegionalMachineDone(room)&&
                    g.RegionalMachineProgress(room)==3&&
                    g.RegionalMachineNextIndex(room)==-1,
                    "Three ordered real stations permanently activate the local civilian network");
                g.PrepareSave();
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&
                    restored.RegionalMachineDone(room)&&
                    restored.RegionalMachineProgress(room)==3,
                    "Existing v2 saves persist authentic staged and completed engineering progress");
            }
            Check(distinct.Count==24,
                "Twenty-four independently positioned, reachable physical field devices exist");
        }
        public static void CommissionedSafeMachinesActuallyRestoreNearbyPlayerButNotRemote(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.Close();g.save.room=0;g.enemies.Clear();
            g.save.flags.Add("regional_machine_done_0");
            WorldObject station=Array.Find(g.Map.objects,o=>
                o.id=="regional_machine_0_0");
            Check(station!=null,"Shelter is an existing authored and rendered map source");
            g.save.x=station.x;g.save.y=station.y;
            g.save.health=40;g.save.energy=20;g.save.heat=30;g.save.cold=30;
            g.mode="world";g.Tick(.05f,new Point());
            Check(g.save.health>40f&&g.save.energy>20f&&
                g.save.heat<30f&&g.save.cold<30f,
                "Actual world-time and physical station proximity restore HP, energy and exposure");
            GameCore old=new GameCore(data);
            old.Close();old.save.room=0;old.enemies.Clear();
            old.save.x=station.x;old.save.y=station.y;
            old.save.health=40;old.save.energy=20;old.mode="world";
            old.Tick(.05f,new Point());
            Check(Math.Abs(old.save.health-40f)<.001f,
                "Old save without the three repaired stations cannot gain free health");
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;g.save.health=40;
            // If the region entrance overlaps a station, use a real known
            // remote wayfinding marker many tiles from this shelter instead.
            WorldObject far=Array.Find(g.Map.objects,o=>o.id=="biomeSurvey0");
            g.save.x=far.x;g.save.y=far.y;
            g.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health-40f)<.001f,
                "Completed network never heals a traveler at a distant unrelated location");
        }
    }
}
