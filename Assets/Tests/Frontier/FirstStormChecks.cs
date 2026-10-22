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

        public static void ChapterSixOriginalWitnessesDeliverRealMealsWithoutBlockingOldStories(Campaign data)
        {
            var g=PreparedStorm(data,3);
            foreach(string f in new[]{"storm_started","scout_saved","gate_2","gate_3"})
                g.save.flags.Add(f);
            for(int i=1;i<=3;i++)g.save.flags.Add("storm_region_"+i+"_grid");
            int oldXp=g.save.xp;
            for(int region=1;region<=3;region++)
            {
                g.Enter(region);g.enemies.Clear();
                WorldObject witness=Object(g,"witnessnpc"+region);
                g.save.x=witness.x;g.save.y=witness.y;
                g.save.inventory[6]=0;
                Action(g,witness.kind,"storm_relief_"+region);
                Check(!g.Has("storm_relief_"+region) &&
                    g.StormReliefCount==region-1,
                    "Real civilian aid cannot occur without an actual purchased or cooked stew");
                g.save.inventory[6]=1;
                Action(g,witness.kind,"storm_relief_"+region);
                Check(g.Has("storm_relief_"+region) &&
                    g.save.inventory[6]==0 && g.StormReliefCount==region,
                    "Each named witness distributes exactly one genuinely consumed hot meal");
                g.Talk(witness);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="storm_relief_"+region)<0 &&
                    Array.FindIndex(g.conversation.choices,c=>
                    c.action=="storm_oldstory_"+region)>=0,
                    "A completed local relief action neither repeats its reward nor hides older witness testimony");
                int oldStory=Array.FindIndex(g.conversation.choices,c=>
                    c.action=="storm_oldstory_"+region);
                g.Choose(oldStory);
                Check(g.mode=="dialog" &&
                    (Array.FindIndex(g.conversation.choices,c=>
                        c.action=="witness_begin_"+region)>=0 ||
                     Array.FindIndex(g.conversation.choices,c=>
                        c.action=="witness_track")>=0),
                    "Original Acts I–III story conversations remain genuinely interactable after new chapter relief");
                g.Close();
            }
            Check(g.StormReliefAll && g.StormReliefCount==3,
                "All three region-specific civic relief deliveries can be earned independently");
            g.save.flags.Add("storm_tuned");g.save.flags.Add("storm_complete");
            g.Enter(0);g.enemies.Clear();
            var council=Object(g,"stormcouncil");
            g.save.x=council.x;g.save.y=council.y;
            g.save.health=40;
            Advance(g,2);
            Check(g.save.health>44,
                "Three actually delivered meals make the permanent town public refuge measurably stronger");
            g.Epilogue();
            Check(g.conversation.text.Contains("三份热汤") &&
                g.conversation.textEn.Contains("three real meals"),
                "Earned post-storm humanitarian aid appears in the original ending's living aftermath");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2 && loaded.StormReliefAll &&
                loaded.StormChapterComplete && loaded.save.ending==3,
                "Optional humanitarian choices and the original ending persist safely in unchanged SaveData v2");
        }


        public static void ChapterSixOrrinDebatesRemainingReserveAndActuallyChangesCivilianShelters(Campaign data)
        {
            var town=PreparedStorm(data,1);
            foreach(string f in new[]{"storm_started","storm_tuned","storm_complete",
                "storm_region_1_shelter","storm_region_2_grid","storm_region_3_grid"})
                town.save.flags.Add(f);
            Check(town.StormBudgetLeft==1 && town.StormChapterComplete,
                "Orrin's after-storm question uses genuine remaining units from the three real policy choices");
            town.Enter(4);town.enemies.Clear();town.invulnerable=100;
            WorldObject shrine=Object(town,"shrine");
            town.save.x=shrine.x;town.save.y=shrine.y;
            town.Talk(shrine);
            Check(Array.Exists(town.conversation.choices,c=>c.action=="storm_orrin_open") &&
                Array.Exists(town.conversation.choices,c=>c.action=="storm_orrin_guard") &&
                !Array.Exists(town.conversation.choices,c=>c.action=="boss"),
                "The former Warden genuinely returns for a peace dialogue, not a revived original boss fight");
            town.Close();
            town.save.x=shrine.x-4;town.save.y=shrine.y;
            town.Talk(shrine);
            Check(!town.Has("storm_orrin_open") && town.mode=="world",
                "Neither the story menu nor a remote action can forge a signed reserve treaty");
            town.save.x=shrine.x;town.save.y=shrine.y;
            Action(town,shrine.kind,"storm_orrin_open");
            Check(town.Has("storm_orrin_open") &&
                !town.Has("storm_orrin_guard") && town.save.ending==1,
                "Player can explicitly assign the actual remainder to an open public town refuge without replacing their original ending");
            town.Talk(shrine);
            Check(!Array.Exists(town.conversation.choices,c=>c.action=="storm_orrin_guard"),
                "The post-storm civic power allocation cannot be double-spent or reversed to farm rewards");
            town.Close();
            town.Enter(0);town.enemies.Clear();town.invulnerable=100;
            WorldObject council=Object(town,"stormcouncil");
            town.save.x=council.x;town.save.y=council.y;
            town.save.health=40;
            Advance(town,2);
            Check(town.save.health>44,
                "The actually signed open-reserve policy powers measurable public town healing, not just epilogue prose");
            town.Epilogue();
            Check(town.conversation.text.Contains("厄洛在第一次星雨后") &&
                town.conversation.textEn.Contains("Orrin signed"),
                "Original ending genuinely reflects post-storm Orrin power allocation");
            town.Close();
            town.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(town.save));reloaded.Start();
            Check(reloaded.save.version==2 && reloaded.Has("storm_orrin_open") &&
                reloaded.StormChapterComplete && reloaded.save.ending==1,
                "Permanent post-storm diplomacy persists in unchanged original SaveData v2");

            var citadel=PreparedStorm(data,2);
            foreach(string f in new[]{"storm_started","storm_tuned","storm_complete",
                "storm_region_1_shelter","storm_region_2_grid","storm_region_3_grid"})
                citadel.save.flags.Add(f);
            citadel.Enter(4);citadel.enemies.Clear();
            shrine=Object(citadel,"shrine");
            citadel.save.x=shrine.x;citadel.save.y=shrine.y;
            Action(citadel,shrine.kind,"storm_orrin_guard");
            Check(citadel.Has("storm_orrin_guard") && !citadel.Has("storm_orrin_open"),
                "The alternative choice preserves civic backup lighting in the citadel instead of a free duplicate");
            WorldObject refuge=Object(citadel,"blackoutshelter");
            citadel.save.x=refuge.x;citadel.save.y=refuge.y;
            citadel.save.health=40;citadel.invulnerable=100;
            Advance(citadel,2);
            Check(citadel.save.health>42 && !citadel.Has("blackout_complete"),
                "Civilian emergency lamps really heal at the old Citadel refuge even on original endings without optional blackout success");

            var noSurplus=PreparedStorm(data,3);
            foreach(string f in new[]{"storm_started","storm_tuned","storm_complete",
                "storm_region_1_shelter","storm_region_2_shelter","storm_region_3_grid"})
                noSurplus.save.flags.Add(f);
            Check(noSurplus.StormBudgetLeft==0,"All eight units may be legitimately assigned to the three communities");
            noSurplus.Enter(4);noSurplus.enemies.Clear();
            shrine=Object(noSurplus,"shrine");
            noSurplus.save.x=shrine.x;noSurplus.save.y=shrine.y;
            noSurplus.Talk(shrine);
            Check(!Array.Exists(noSurplus.conversation.choices,c=>c.action=="storm_orrin_open") &&
                !Array.Exists(noSurplus.conversation.choices,c=>c.action=="storm_orrin_guard"),
                "Zero remaining public reserve cannot be magically signed away by the former Warden");
        }

    }
}
