using System;

namespace Starfall.Tests
{
    // Player E -> actual physical landmark dialogue -> explicit route -> E.
    // No synthetic landmarks, plot-coordinate moves or SaveData extensions.
    public static partial class CoreChecks
    {
        public static void EightFieldTrailDialogsCanGuideThroughRealBearings(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                var g=Fresh(data);g.save.room=room;g.save.stage=3;
                g.mode="world";
                string originalQuest=g.save.trackedQuest;
                int originalCoins=g.save.coins;
                WorldObject first=Array.Find(g.Map.objects,
                    o=>o.id=="trailcircuit_"+room+"_1");
                Check(first!=null,"Original first trail bearing exists: "+room);
                g.save.x=first.x;g.save.y=first.y;
                g.Talk(first);
                int follow=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="trail_circuit_follow");
                WorldObject second=g.NextTrailCircuitMarker(room);
                Check(g.TrailCircuitProgress(room)==1&&
                    follow>=0&&second!=null&&!g.HasLocalWaymark,
                    "Reading first source offers navigation but never pins automatically");
                Check(g.conversation.choices[follow].label.Contains("引导")&&
                    g.conversation.choices[follow].en.Contains("Guide"),
                    "Both languages explicitly identify the next bearing route");
                int flagCount=g.save.flags.Count;
                g.Choose(follow);
                Check(g.mode=="world"&&g.HasLocalWaymark,
                    "Field dialogue reaches genuine local navigation without opening atlas");
                Point p=g.LocalWaymark.Value;
                Check(g.CanStand(p)&&
                    (p-new Point(second.x,second.y)).Length<2f&&
                    g.InspectLocalWaymark(p)==WaymarkRouteStatus.Available,
                    "First-to-second bearing is a real verified E-approach: "+room);
                Check(g.TrailCircuitProgress(room)==1&&
                    g.save.flags.Count==flagCount&&
                    g.save.trackedQuest==originalQuest&&
                    g.save.coins==originalCoins,
                    "Opt-in guidance changes no survey, tracked quest or reward");
                g.save.x=second.x;g.save.y=second.y;
                g.Talk(second);
                Check(g.TrailCircuitProgress(room)==2,
                    "Second bearing must be physically interacted with");
                follow=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="trail_circuit_follow");
                WorldObject third=g.NextTrailCircuitMarker(room);
                Check(follow>=0&&third!=null,
                    "After second bearing, original third landmark becomes selectable");
                g.Choose(follow);
                Check(g.HasLocalWaymark&&
                    (g.LocalWaymark.Value-new Point(third.x,third.y)).Length<2f&&
                    g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                    "Second-to-third bearing uses a real reachable foot: "+room);
                g.save.x=third.x;g.save.y=third.y;
                g.Talk(third);
                Check(g.TrailCircuitComplete(room)&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="trail_circuit_follow")<0&&
                    g.save.coins==originalCoins+24,
                    "Third real bearing completes once and has no ghost fourth stage");
                g.Choose(0);g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.TrailCircuitComplete(room)&&
                    !loaded.HasLocalWaymark&&loaded.save.version==2,
                    "Old v2 slots keep survey receipts but not transient personal pins");
            }
        }

        public static void FieldTrailFollowRejectsStaleAndMissingSources(Campaign data)
        {
            var g=Fresh(data);g.save.room=1;g.save.stage=3;
            g.mode="world";
            WorldObject first=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_1");
            WorldObject second=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_2");
            WorldObject third=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_3");
            Check(first!=null&&second!=null&&third!=null,
                "Original forest path retains three physical landmarks");
            g.save.x=first.x;g.save.y=first.y;g.Talk(first);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow");
            Check(choice>=0,"Surveyed first marker offers opt-in route");
            g.Choose(choice);
            Point previous=g.LocalWaymark.Value;
            g.Talk(first);
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow");
            g.save.x=second.x;g.save.y=second.y;
            g.Choose(choice);
            Check(g.HasLocalWaymark&&
                (g.LocalWaymark.Value-previous).Length<.001f&&
                g.TrailCircuitProgress(1)==1,
                "Leaving speaking range cannot overwrite a previous valid route");
            g.Talk(second);
            Check(g.TrailCircuitProgress(1)==2,
                "Second marker is reached before testing missing next source");
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow");
            WorldObject[] original=g.Map.objects;
            try
            {
                g.Map.objects=Array.FindAll(original,
                    o=>!Object.ReferenceEquals(o,third));
                g.Choose(choice);
                Check(g.HasLocalWaymark&&
                    (g.LocalWaymark.Value-previous).Length<.001f&&
                    g.TrailCircuitProgress(1)==2,
                    "Removed next landmark cannot steal the personal pin");
            }
            finally {g.Map.objects=original;}
        }
    }
}
