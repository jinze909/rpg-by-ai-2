using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void SevenRegionalRoadSignalsAffectOnlyRealCombatActors(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();g.save.energy=100f;
                WorldObject marker=Array.Find(g.Map.objects,
                    o=>o.id=="ridge_route_"+room+"_0");
                Check(marker!=null&&g.CanStand(new Point(marker.x,marker.y)),
                    "Original wilderness crossing exists: "+room);
                g.save.x=marker.x;g.save.y=marker.y;
                g.Tick(.02f,new Point());
                Check(g.RemoteRoadEncounterActive&&
                    g.RemoteRoadRemaining==2&&g.enemies.Count==2,
                    "Arrival at original exposed marker spawns only its two genuine raiders");
                Enemy[] foes=g.enemies.ToArray();
                int coins=g.save.coins;
                Check(g.RemoteRoadSignalEnergyCost(room)==12&&
                    g.RemoteRoadSignalThreatCount(marker)>=1,
                    "Unsurveyd marker has actual visible counter targets and standard cost");
                g.Talk(marker);
                // First contact legitimately records the ORIGINAL ridge
                // discovery (+9 XP and its ridge_seen flag). Test only the
                // NEW signal's transaction, not that prior story action.
                int priorFlags=g.save.flags.Count;
                int choice=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="remote_road_signal");
                Check(choice>=0,"Existing in-combat ridge menu offers optional terrain counter");
                g.Choose(choice);
                Check(g.mode=="world"&&g.RemoteRoadSignalUsed&&
                    Math.Abs(g.save.energy-88f)<.01f&&
                    g.RemoteRoadEncounterActive&&g.RemoteRoadRemaining==2,
                    "Real field counter costs energy, not a kill or artificial clear");
                int stunned=0;
                foreach(Enemy e in foes)
                    if(e.stun>.5f)stunned++;
                Check(stunned>=1&&
                    g.IsRemoteRoadThreat(foes[0])&&
                    g.IsRemoteRoadThreat(foes[1])&&
                    g.save.coins==coins&&g.save.flags.Count==priorFlags,
                    "Terrain response interrupts authentic bodies, never grants fake loot or progress");
                g.Talk(marker);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="remote_road_signal")<0,
                    "One-use signal becomes visibly expended while fighting");
                g.Choose(0);
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&
                    !loaded.RemoteRoadEncounterActive&&!loaded.RemoteRoadSignalUsed,
                    "Old save format never persists an active counter or duplicate enemy wave");
            }
        }

        public static void SurveyedRoadSignalCostsLessAndRejectedSourcesCannotSpend(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=1;g.enemies.Clear();g.save.energy=100f;
            g.save.flags.Add("trail_circuit_1_1");
            g.save.flags.Add("trail_circuit_1_2");
            g.save.flags.Add("trail_circuit_1_3");
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o.id=="ridge_route_1_0");
            Check(marker!=null&&g.RemoteRoadSignalEnergyCost(1)==8,
                "Completing real three-stage forest trail reduces signal cost");
            g.save.x=marker.x;g.save.y=marker.y;g.Tick(.02f,new Point());
            Check(g.RemoteRoadEncounterActive,"Genuine forest combat is underway");
            g.Talk(marker);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="remote_road_signal");
            Check(choice>=0,"Authenticated physical marker presents counter");
            g.save.x+=13f;
            g.Choose(choice);
            Check(!g.RemoteRoadSignalUsed&&g.save.energy==100f,
                "A stale, out-of-reach dialogue cannot spend hero stamina");
            g.save.x=marker.x;g.save.y=marker.y;g.Talk(marker);
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="remote_road_signal");
            g.Choose(choice);
            Check(g.RemoteRoadSignalUsed&&
                Math.Abs(g.save.energy-92f)<.01f,
                "Completed civilian survey pays off in a real discounted combat action");
            // Another world object may be nearby, but only the ORIGINAL
            // ambush marker owns the action and the session's foe references.
            WorldObject fake=new WorldObject{kind="ridgemarker",
                id=marker.id,name="伪路标",en="False marker",
                x=marker.x,y=marker.y};
            Check(g.RemoteRoadSignalThreatCount(fake)==0,
                "A lookalike marker with identical coordinates has no battle control authority");
        }

        public static void RoadSignalCannotChargeWithoutLiveExposedTarget(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=1;g.enemies.Clear();g.save.energy=100f;
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o.id=="ridge_route_1_0");
            g.save.x=marker.x;g.save.y=marker.y;g.Tick(.02f,new Point());
            Check(g.RemoteRoadEncounterActive,"Real road encounter is active");
            g.enemies.Clear();
            Check(g.RemoteRoadSignalThreatCount(marker)==0,
                "Disappeared enemies leave no legitimate targets");
            g.Talk(marker);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="remote_road_signal");
            Check(choice>=0,"Original dialogue still offers explicit tactical attempt");
            g.Choose(choice);
            Check(g.save.energy==100f&&!g.RemoteRoadSignalUsed&&
                !g.RemoteRoadCleared(1,0),
                "No target means no payment, false counter, or combat receipt");
        }
    }
}
