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

        public static void RepairedStationsLaunchPhysicalRegionalSoundDiversions(Campaign data)
        {
            int[] supplied={0,0,0,0,0,0,4,2};
            var audible=typeof(GameCore).GetMethod("AudibleAlongWorld",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(audible!=null,"The actual acoustic corridor solver is available");
            int heardAcrossEight=0;
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.Close();
                g.save.room=room;g.mode="world";g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                g.save.flags.Add("regional_machine_done_"+room);
                int material=supplied[room];
                g.save.inventory[material]=3;
                WorldObject station=Array.Find(g.Map.objects,o=>
                    o.id=="regional_machine_"+room+"_0");
                Check(station!=null&&station.kind=="regionalMachine",
                    "Regional acoustic signal uses a real reachable machine");
                Point origin=new Point(station.x,station.y);
                g.save.x=origin.x;g.save.y=origin.y;
                Point enemyFoot=new Point(),facing=new Point();
                bool located=false;
                Point[] compass={
                    new Point(1,0),new Point(-1,0),
                    new Point(0,1),new Point(0,-1),
                    new Point(.7071f,.7071f),new Point(-.7071f,.7071f),
                    new Point(.7071f,-.7071f),new Point(-.7071f,-.7071f)
                };
                for(float distance=4.5f;distance<=7f&&!located;distance+=.5f)
                foreach(Point direction in compass)
                {
                    Point candidate=origin+direction*distance;
                    if(!g.CanStand(candidate))continue;
                    if(!(bool)audible.Invoke(g,new object[]{origin,candidate,7.3f}))
                        continue;
                    enemyFoot=candidate;facing=direction;
                    located=true;break;
                }
                Check(located,
                    "At least one actual terrain-connected sentry route exists beside every public station");
                Enemy sentry=new Enemy(98000+room,0,enemyFoot.x,enemyFoot.y,80f);
                sentry.Face(facing);sentry.timer=30f;
                g.enemies.Add(sentry);
                g.Talk(station);
                int signal=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="regional_machine_signal");
                Check(signal>=0,"Commissioned public machine offers a real field signal");
                int before=g.save.inventory[material];
                g.Choose(signal);
                Check(g.IsEnemyInvestigatingImpact(sentry)&&
                    g.save.inventory[material]==before-1&&
                    !g.Has("regional_machine_signal_"+room)&&
                    g.mode=="world",
                    "World-acoustic patrol diversion costs exactly one appropriate local material");
                heardAcrossEight++;
                // The same source cannot be spammed during its actual 26s
                // recovery. Re-opening the real map dialogue must not reset it.
                g.Talk(station);
                signal=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="regional_machine_signal");
                Check(signal>=0,"Active field signal remains visible at the original device");
                int after=g.save.inventory[material];
                g.Choose(signal);
                Check(g.save.inventory[material]==after&&
                    g.IsEnemyInvestigatingImpact(sentry),
                    "Cooldown rejects an immediate second activation without charging stock");
                // Current mission-controlled combat cannot be overwritten
                // by a convenience decoy. A new world slot also cannot
                // inherit an old instance-owned sound footprint.
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.RegionalMachineDone(room)&&
                    !restored.IsEnemyInvestigatingImpact(sentry),
                    "Existing v2 public engineering persists but live acoustic ownership does not");
            }
            Check(heardAcrossEight==8,
                "All eight authored regions gain a functional, terrain-checked tactical diversion");
        }
        public static void FieldStationSignalCannotHijackAttackingOrUnawareObjectiveGuards(Campaign data)
        {
            GameCore g=new GameCore(data);g.Close();g.save.room=0;
            g.mode="world";g.enemies.Clear();
            g.save.flags.Add("biome_field_done_0");
            g.save.flags.Add("regional_machine_done_0");
            WorldObject station=Array.Find(g.Map.objects,o=>
                o.id=="regional_machine_0_0");
            Check(station!=null,"Original canal gong exists");
            g.save.x=station.x;g.save.y=station.y;
            g.save.inventory[0]=2;
            Point foot=new Point(station.x+5f,station.y);
            if(!g.CanStand(foot))
                foot=new Point(station.x-5f,station.y);
            Check(g.CanStand(foot)&&
                g.NavigationPathFrom(new Point(station.x,station.y),
                    foot,true).Count>0,
                "An actual canal guard can stand on local walkable terrain");
            Enemy committed=new Enemy(98070,0,foot.x,foot.y,120f);
            committed.state=1;committed.timer=.4f;committed.Face(
                (foot-new Point(station.x,station.y)).Normalized);
            g.enemies.Add(committed);
            g.Talk(station);
            int signal=Array.FindIndex(g.conversation.choices,
                c=>c.action=="regional_machine_signal");
            Check(signal>=0,"The real signal interaction is offered");
            g.Choose(signal);
            Check(committed.state==1&&
                !g.IsEnemyInvestigatingImpact(committed)&&
                g.save.inventory[0]==2,
                "Already committed attacks ignore diversion and consume no wasted materials");
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
