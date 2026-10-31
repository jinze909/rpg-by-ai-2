using System;

namespace Starfall.Tests
{
    // Player E -> actual physical landmark dialogue -> explicit route -> E.
    // No synthetic landmarks, plot-coordinate moves or SaveData extensions.
    public static partial class CoreChecks
    {
        public static void GuidedTrailSourceWinsAmbiguousNearbyEWithoutHijackingManualFocus(Campaign data)
        {
            var g=Fresh(data);g.save.room=1;g.save.stage=3;g.mode="world";
            WorldObject first=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_1");
            WorldObject second=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_2");
            g.save.x=first.x;g.save.y=first.y;g.Talk(first);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow"));
            Check(g.TrailCircuitAutoFollowActive&&
                g.TrailCircuitIsCurrentFollowTarget(second),
                "Only exact authentic next E source is the accepted trail target");
            WorldObject decoy=new WorldObject{
                id="overlapping_prop",kind="herb",
                name="Local herbs",en="Local herbs",
                x=second.x,y=second.y};
            WorldObject[] original=g.Map.objects;
            try
            {
                var crowded=new WorldObject[original.Length+1];
                crowded[0]=decoy;
                Array.Copy(original,0,crowded,1,original.Length);
                g.Map.objects=crowded;
                g.save.x=second.x;g.save.y=second.y;
                Check(System.Object.ReferenceEquals(g.Nearest(),second),
                    "On arrival, explicit trail follow chooses original route source, not same-foot prop");
                int selected=g.NearbyInteractionChoices().FindIndex(
                    x=>System.Object.ReferenceEquals(x,decoy));
                Check(selected>=0&&g.FocusNearbyInteraction(selected)&&
                    System.Object.ReferenceEquals(g.Nearest(),decoy),
                    "Explicit user O/click selection always outranks trail shortcut");
                // Enter the same genuine stage via direct source-bound Talk;
                // no synthetic herb is allowed to advance the receipt.
                g.Talk(second);
                Check(g.TrailCircuitProgress(1)==2&&
                    g.TrailCircuitAutoFollowActive,
                    "Real source interaction still advances continuous walk");
            }
            finally{g.Map.objects=original;}
        }

        public static void EightRealTrailExpeditionsAutoAdvanceOnlyAfterPhysicalESurvey(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                var g=Fresh(data);g.save.stage=3;g.save.room=room;
                g.mode="world";
                string originalTracked=g.save.trackedQuest;
                WorldObject[] stages=new WorldObject[3];
                for(int i=0;i<3;i++)
                    stages[i]=Array.Find(g.Map.objects,
                        o=>o.id=="trailcircuit_"+room+"_"+(i+1));
                Check(stages[0]!=null&&stages[1]!=null&&stages[2]!=null,
                    "All original in-world bearings preserved");
                g.save.x=stages[0].x;g.save.y=stages[0].y;
                g.Talk(stages[0]);
                int follow=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="trail_circuit_follow");
                Check(follow>=0&&g.TrailCircuitProgress(room)==1,
                    "Physical first bearing signs only one real survey stage");
                g.Choose(follow);
                Check(g.TrailCircuitAutoFollowActive&&g.HasLocalWaymark&&
                    (g.LocalWaymark.Value-new Point(stages[1].x,stages[1].y)).Length<2f,
                    "Explicit opt-in plans the second actual E approach");
                // Simply arriving at the destination foot never awards
                // a survey receipt or auto-skips the original interaction.
                Point foot=g.LocalWaymark.Value;
                g.save.x=foot.x;g.save.y=foot.y;
                g.Tick(.02f,new Point());
                Check(g.TrailCircuitProgress(room)==1&&
                    g.TrailCircuitAutoFollowActive&&g.HasLocalWaymark,
                    "At the physical E foot, keep source pin until actual interaction");
                g.save.x=stages[1].x;g.save.y=stages[1].y;
                g.Talk(stages[1]);
                Check(g.TrailCircuitProgress(room)==2&&
                    g.TrailCircuitAutoFollowActive&&
                    g.HasLocalWaymark&&
                    (g.LocalWaymark.Value-
                        new Point(stages[2].x,stages[2].y)).Length<2f,
                    "After authentic second E, auto-guide the real third bearing: "+room);
                g.save.x=stages[2].x;g.save.y=stages[2].y;
                g.Talk(stages[2]);
                Check(g.TrailCircuitComplete(room)&&
                    !g.TrailCircuitAutoFollowActive&&!g.HasLocalWaymark&&
                    g.save.trackedQuest==originalTracked,
                    "Third original E completes the survey and retires only the trail-owned pin");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.TrailCircuitComplete(room)&&
                    !restored.TrailCircuitAutoFollowActive&&
                    !restored.HasLocalWaymark,
                    "Old save slots retain actual receipts, never transient guide ownership");
            }
        }

        public static void TrailAutoFollowYieldsToPersonalPinsAndManualCancellation(Campaign data)
        {
            for(int cancel=0;cancel<2;cancel++)
            {
                var g=Fresh(data);g.save.stage=3;g.save.room=1;
                g.mode="world";
                WorldObject first=Array.Find(g.Map.objects,
                    o=>o.id=="trailcircuit_1_1");
                WorldObject second=Array.Find(g.Map.objects,
                    o=>o.id=="trailcircuit_1_2");
                g.save.x=first.x;g.save.y=first.y;g.Talk(first);
                g.Choose(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="trail_circuit_follow"));
                Check(g.TrailCircuitAutoFollowActive,"Original owner accepted route");
                if(cancel==0)
                {
                    g.mode="map";
                    Check(g.PlaceLocalWaymark(new Point(first.x,first.y)),
                        "A valid manual map pin replaces the trail-owned marker");
                    g.mode="world";
                    Check(g.HasLocalWaymark&&!g.TrailCircuitAutoFollowActive,
                        "Manual pin owns navigation; old expedition is canceled");
                }
                else
                {
                    Check(g.ClearLocalWaymark()&&
                        !g.TrailCircuitAutoFollowActive&&!g.HasLocalWaymark,
                        "Explicit user cancel revokes the entire continuous trail");
                }
                Point? preserved=g.LocalWaymark;
                g.save.x=second.x;g.save.y=second.y;g.Talk(second);
                Check(g.TrailCircuitProgress(1)==2&&
                    !g.TrailCircuitAutoFollowActive&&
                    g.HasLocalWaymark==preserved.HasValue&&
                    (!preserved.HasValue||
                        (g.LocalWaymark.Value-preserved.Value).Length<.001f),
                    "Later E never steals a manual pin or restarts a canceled guide");
            }
        }

        public static void BlockedThirdTrailStopEndsAutoFollowWithoutFakeCompletion(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.room=1;g.mode="world";
            WorldObject first=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_1");
            WorldObject second=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_2");
            WorldObject third=Array.Find(g.Map.objects,
                o=>o.id=="trailcircuit_1_3");
            g.save.x=first.x;g.save.y=first.y;g.Talk(first);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow"));
            WorldObject[] original=g.Map.objects;
            try
            {
                g.Map.objects=Array.FindAll(original,
                    o=>!System.Object.ReferenceEquals(o,third));
                g.save.x=second.x;g.save.y=second.y;g.Talk(second);
                Check(g.TrailCircuitProgress(1)==2&&
                    !g.TrailCircuitComplete(1)&&
                    !g.TrailCircuitAutoFollowActive&&!g.HasLocalWaymark,
                    "Missing next physical landmark cancels only obsolete auto-pin; no ghost third receipt");
            }
            finally {g.Map.objects=original;}
            g.save.x=third.x;g.save.y=third.y;g.Talk(third);
            Check(g.TrailCircuitComplete(1)&&!g.HasLocalWaymark,
                "When road reopens, original physical third E still grants its intended survey");
        }

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
            Check(g.TrailCircuitProgress(1)==2&&
                g.TrailCircuitAutoFollowActive,
                "Second physical E automatically hands off to its verified third bearing");
            Point nextPin=g.LocalWaymark.Value;
            Check((nextPin-new Point(third.x,third.y)).Length<2f,
                "The new route belongs to the genuine third source, not the completed second");
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="trail_circuit_follow");
            WorldObject[] original=g.Map.objects;
            try
            {
                g.Map.objects=Array.FindAll(original,
                    o=>!System.Object.ReferenceEquals(o,third));
                g.Choose(choice);
                Check(g.HasLocalWaymark&&
                    (g.LocalWaymark.Value-nextPin).Length<.001f&&
                    g.TrailCircuitProgress(1)==2,
                    "Stale choice with removed next landmark cannot rewrite an existing route");
            }
            finally {g.Map.objects=original;}
        }
    }
}
