using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // These are the shipped Campaign.json nodes, not an isolated mock map.
        // All forty are deliberately disjoint detours in the eight live regions.
        public static void DiverseBiomeHarvestDetoursAreReachableAndActuallyGatherable(Campaign data)
        {
            var usedPositions=new HashSet<string>();
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                GameCore core=new GameCore(data);
                core.save.room=room;
                int width=map.rows[0].Length,height=map.rows.Length;
                var visited=new bool[width*height];
                var queue=new Queue<int>();
                int sx=(int)Math.Floor(map.spawnX),sy=(int)Math.Floor(map.spawnY);
                int start=sy*width+sx;
                Check(core.CanStand(new Point(sx+.5f,sy+.5f)),
                    "The region entrance must be walkable before testing resource detours");
                visited[start]=true;queue.Enqueue(start);
                while(queue.Count>0)
                {
                    int current=queue.Dequeue(),x=current%width,y=current/width;
                    int[] dx={-1,1,0,0},dy={0,0,-1,1};
                    for(int direction=0;direction<4;direction++)
                    {
                        int nx=x+dx[direction],ny=y+dy[direction];
                        if(nx<2||ny<2||nx>=width-2||ny>=height-2)continue;
                        int next=ny*width+nx;
                        if(visited[next]||!core.CanStand(new Point(nx+.5f,ny+.5f)))continue;
                        visited[next]=true;queue.Enqueue(next);
                    }
                }
                var nodes=new List<WorldObject>();
                foreach(WorldObject o in map.objects)
                    if(o.id.StartsWith("trail_forager_"+room+"_",
                            StringComparison.Ordinal) ||
                        o.id=="waytrail_supply"+room+"a"||
                        o.id=="waytrail_supply"+room+"b")
                        nodes.Add(o);
                Check(nodes.Count==5,
                    "Every real region must contain three wilderness caches plus two distinct road supplies");
                foreach(WorldObject node in nodes)
                {
                    int x=(int)Math.Floor(node.x),y=(int)Math.Floor(node.y);
                    Check(node.x==x+.5f&&node.y==y+.5f&&
                        core.CanStand(new Point(node.x,node.y))&&
                        visited[y*width+x],
                        "Every authored wilderness detour must have an actual traversable route from its region entrance");
                    Check(node.name.Length>4&&node.en.Length>7,
                        "A pocket must tell local environmental story, not a generic numbered placeholder");
                    string place=node.x+","+node.y;
                    Check(usedPositions.Add(place),
                        "No two regions may reuse the same wilderness gathering coordinate");
                    foreach(WorldObject other in nodes)
                        if(!Object.ReferenceEquals(node,other))
                            Check((new Point(node.x-other.x,node.y-other.y)).Length>=15f,
                                "Field supplies must be genuine separated detours, not a stacked reward cluster");
                    // Use the untouched production E interaction, inventory checks,
                    // permanent flag and re-gather guard, rather than faking rewards.
                    GameCore play=new GameCore(data);
                    play.save.room=room;play.save.x=node.x;play.save.y=node.y;
                    int inventorySlot=node.kind=="herb"?2:node.kind=="wood"?0:1;
                    int before=play.save.inventory[inventorySlot];
                    int oldParts=play.save.inventory[4];
                    Check(!play.Has("node_"+node.id),
                        "Fresh SaveData v2 must not begin with the new cache claimed");
                    play.Talk(node);
                    Check(play.Has("node_"+node.id)&&
                        play.save.inventory[inventorySlot]==before+3&&
                        (node.kind!="ore"||play.save.inventory[4]==oldParts+3),
                        "Live resource interaction must produce the expected materials exactly once");
                    play.Talk(node);
                    Check(play.save.inventory[inventorySlot]==before+3,
                        "Already harvested pocket must never duplicate a reward");
                    play.PrepareSave();
                    GameCore reloaded=new GameCore(data,play.save);
                    Check(reloaded.save.version==2&&reloaded.Has("node_"+node.id),
                        "Every relocated resource retains its original persistent flag and v2 save");
                }
            }
            Check(usedPositions.Count==40,
                "All eight maps must expose distinct physically reachable resource detours");
        }
    }
}
