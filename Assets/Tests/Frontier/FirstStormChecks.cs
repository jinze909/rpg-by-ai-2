using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore PreparedStorm(Campaign data,int ending)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=ending;
            foreach(string f in new[]{"afterlight_started","afterlight_complete","boss_4"})
                g.save.flags.Add(f);
            for(int r=1;r<=3;r++)
            {
                g.save.flags.Add("afterlight_done_"+r);
                g.save.flags.Add("afterlight_steward_"+r);
                g.save.flags.Add("afterlight_plan_"+r+"_care");
            }
            for(int x=0;x<5;x++)g.save.inventory[x]=25;
            g.save.coins=100;g.save.energy=100;g.save.health=100;
            g.Enter(0);g.enemies.Clear();return g;
        }
        private static void WalkStormNode(GameCore g,Point goal)
        {
            for(int step=0;step<2800 && (g.Position-goal).Length>.74f &&
                g.StormTuning;step++)
            {
                Point direction=g.DirectionTo(g.Position,goal);
                g.Tick(.02f,direction);
            }
            Check(g.StormTuning && (g.Position-goal).Length<1.05f,
                "Hero must physically navigate to the actual civic lamp while storm tuning runs");
        }
        public static void ChapterSixActualReserveBudgetAndPublicPoliciesSurviveLegacySaves(Campaign data)
        {
            var untouched=Fresh(data);
            Check(untouched.GetQuest("storm_commons").locked &&
                !untouched.Has("storm_started"),
                "The sixth main chapter never appears as complete before genuine first-five-chapter accomplishments");
            var g=PreparedStorm(data,1);
            WorldObject council=Object(g,"stormcouncil");
            g.save.x=council.x;g.save.y=council.y;
            Action(g,council.kind,"storm_begin");
            Check(g.Has("storm_started") && g.CurrentGuide.id=="storm_commons" &&
                g.StormBudgetSpent==0 && g.StormBudgetLeft==8,
                "Chapter Six begins from the real town council with an honest, bounded eight-unit budget");
            int[][] costs={new[]{1,0,1,0,0},new[]{1,0,1,0,0},new[]{0,2,0,0,1}};
            for(int region=1;region<=3;region++)
            {
                g.Enter(region);g.enemies.Clear();g.invulnerable=500;
                WorldObject relay=Object(g,"stormrelay"+region);
                Check(g.CanStand(new Point(relay.x,relay.y)),
                    "Each real regional relay must be placed on original traversable terrain");
                g.save.x=relay.x;g.save.y=relay.y;
                int[] before=(int[])g.save.inventory.Clone();
                if(region==3)
                {
                    Action(g,relay.kind,"storm_policy_3_shelter");
                    Check(g.StormPolicy(3)==0 && g.StormBudgetLeft==2 &&
                        g.save.inventory[0]==before[0] && g.save.inventory[2]==before[2],
                        "Cannot spend a non-existent ninth unit or charge materials for rejected shelters");
                }
                Action(g,relay.kind,"storm_policy_"+region+"_"+(region==3?"grid":"shelter"));
                Check(g.StormPolicy(region)==(region==3?2:1) &&
                    g.StormAllocations==region &&
                    g.StormBudgetSpent==(region==1?3:region==2?6:8),
                    "The real decisions require actual installation and correctly sum a finite public reserve");
                for(int i=0;i<5;i++)Check(g.save.inventory[i]==before[i]-costs[region-1][i],
                    "Every installed civic choice consumes its exact public repair ingredients");
                g.Talk(relay);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action.StartsWith("storm_policy_"))<0,
                    "Committed allocations cannot be replayed to change policies or duplicate rewards");
                g.Close();
            }
            Check(g.StormBudgetLeft==0 && !g.Has("storm_tuned") &&
                !g.Has("storm_complete") && g.save.ending==1,
                "Three policies do not falsely complete the physical town-grid finale or overwrite the old ending");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2 && loaded.StormAllocations==3 &&
                loaded.StormBudgetLeft==0 && !loaded.StormTuning &&
                loaded.save.ending==1 && !loaded.StormChapterComplete,
                "Actual civic decisions survive unchanged SaveData v2 without leaking temporary encounter state");
        }
        public static void ChapterSixStormArcsAreRealAndEachLocalAllocationEndsItsDanger(Campaign data)
        {
            var g=PreparedStorm(data,2);
            WorldObject council=Object(g,"stormcouncil");
            g.save.x=council.x;g.save.y=council.y;
            Action(g,council.kind,"storm_begin");
            g.Enter(1);g.enemies.Clear();g.invulnerable=0;
            WorldObject relay=Object(g,"stormrelay1");
            g.save.x=relay.x;g.save.y=relay.y;
            Advance(g,.1f);
            Check(g.hazards.Exists(h=>h.stormOwned && h.timer>0 && h.damage==16),
                "A genuine dangerous delayed electrical arc is born in an unsettled region");
            var environment=new Hazard {position=new Point(6,15),timer=20,radius=.4f,
                kind="environment",damage=2};
            var ambient=new Effect("discover",new Point(6,15),20);
            g.hazards.Add(environment);g.effects.Add(ambient);
            int prior=(int)g.save.health;
            Advance(g,1.5f);
            Check(g.save.health<prior,
                "Remaining inside the real warning damages the playable gold-haired hero");
            g.invulnerable=200;
            Action(g,relay.kind,"storm_policy_1_grid");
            Check(g.StormPolicy(1)==2 && 
                !g.hazards.Exists(h=>h.stormOwned) &&
                g.hazards.Contains(environment) && g.effects.Contains(ambient),
                "A physical civic installation ends only its owned active lightning hazards, never unrelated world effects");
            Advance(g,5);
            Check(!g.hazards.Exists(h=>h.stormOwned),
                "Stabilized forest receives no more new storms on its real map");
            g.Enter(2);g.enemies.Clear();
            WorldObject ruins=Object(g,"stormrelay2");
            g.save.x=ruins.x;g.save.y=ruins.y;
            Advance(g,.1f);
            Check(g.hazards.Exists(h=>h.stormOwned),
                "Danger in an unallocated second chapter persists instead of being magically fixed by the first");
        }
        public static void ChapterSixEachOriginalEndingChangesRealTownGridSequence(Campaign data)
        {
            for(int ending=1;ending<=3;ending++)
            {
                var g=PreparedStorm(data,ending);
                g.save.flags.Add("storm_started");
                g.save.flags.Add("storm_region_1_shelter");
                g.save.flags.Add("storm_region_2_grid");
                g.save.flags.Add("storm_region_3_grid");
                g.invulnerable=500;
                WorldObject council=Object(g,"stormcouncil");
                g.save.x=council.x;g.save.y=council.y;
                int initialCoins=g.save.coins,initialShards=g.save.shards;
                Action(g,council.kind,"storm_tune_start");
                Check(g.StormTuning && g.StormTuneTime>94 &&
                    g.StormTuneStep==0 && g.StormBudgetSpent==7,
                    "The original ending starts a new actually timed and navigable shared-grid climax");
                g.Enter(1);
                Check(g.save.room==0 && g.StormTuning,
                    "A live town-grid calibration cannot be bypassed by portal travel");
                int[] route=ending==1?new[]{1,2,3}:
                    ending==2?new[]{3,2,1}:new[]{2,1,3};
                int wrong=route[0]==1?2:1;
                WorldObject falseNode=Object(g,"stormnode"+wrong);
                g.save.x=falseNode.x;g.save.y=falseNode.y;
                float oldTime=g.StormTuneTime;
                Action(g,falseNode.kind,"storm_node_"+wrong);
                Check(g.StormTuneStep==0 && g.StormTuneTime<oldTime-3.9f &&
                    !g.Has("storm_tuned"),
                    "Wrong physical order actually consumes time without silently advancing an earned power line");
                for(int index=0;index<3;index++)
                {
                    WorldObject node=Object(g,"stormnode"+route[index]);
                    Point goal=new Point(node.x,node.y);
                    WalkStormNode(g,goal);
                    Check(g.Nearest()==node && g.StormTuneStep==index,
                        "Each distinct geographic node must be reached in the right original-ending-dependent order");
                    Action(g,node.kind,"storm_node_"+route[index]);
                    Check(g.StormTuneStep==index+1 || index==2 &&
                        !g.StormTuning && g.Has("storm_tuned"),
                        "A real node changes one and only one step in the public power network");
                }
                Check(g.Has("storm_tuned") && !g.Has("storm_complete") &&
                    g.StormAllocations==3 && g.save.coins==initialCoins,
                    "Three physically reached lamps do not automatically publish or mint a fake charter");
                g.save.x=council.x;g.save.y=council.y;
                Action(g,council.kind,"storm_publish");
                Check(g.StormChapterComplete && g.save.ending==ending &&
                    g.save.coins==initialCoins+110 &&
                    g.save.shards==initialShards+3 && g.GetQuest("storm_commons").complete,
                    "Real end-dependent lamp sequence reaches a one-time public ending without overwriting original result");
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
                Check(loaded.StormChapterComplete && loaded.save.ending==ending &&
                    loaded.StormBudgetLeft==1 && !loaded.StormTuning,
                    "Final built public grid and its reserve accounting survive v2 reload");
                g.Talk(council);
                Check(!Array.Exists(g.conversation.choices,c=>c.action=="storm_publish"),
                    "The first storm and its rewards cannot be completed twice");
            }
        }
        public static void ChapterSixStormTuningFailureKeepsPoliciesAndOldSaveCompatibility(Campaign data)
        {
            var g=PreparedStorm(data,3);
            g.save.flags.Add("storm_started");
            for(int i=1;i<=3;i++)g.save.flags.Add("storm_region_"+i+"_grid");
            WorldObject council=Object(g,"stormcouncil");
            g.save.x=council.x;g.save.y=council.y;
            g.invulnerable=1000;
            Action(g,council.kind,"storm_tune_start");
            Check(g.StormBudgetLeft==2 && g.StormTuning,
                "Three affordable utility choices retain two emergency units in the city");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.StormTuning && loaded.StormAllocations==3 &&
                loaded.StormBudgetLeft==2 && !loaded.Has("storm_tuned"),
                "Interrupted tuning reloads safely as a new attempt, never a completed puzzle");
            Advance(g,95.3f);
            Check(!g.StormTuning && !g.Has("storm_tuned") &&
                g.StormAllocations==3 && g.StormBudgetLeft==2 &&
                !g.hazards.Exists(h=>h.stormOwned),
                "Timeout preserves all three permanent policy choices but retires owned live arcs");
            g.save.x=council.x;g.save.y=council.y;
            Action(g,council.kind,"storm_tune_start");
            Action(g,council.kind,"storm_abort");
            Check(!g.StormTuning && g.StormTuneStep==0 &&
                g.StormAllocations==3 && !g.Has("storm_tuned"),
                "Player can safely abandon a tuning attempt at the council without losing installed regional works");
        }
    }
}
