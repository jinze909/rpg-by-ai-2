using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Exercises actual Chapter I–IV authored map data and production
        // dialogue/combat/receipt paths, not artificially awarded quest flags.
        public static void FourDistinctCivilianDiversionsAlterLiveCombatAndWitnessOutcome(Campaign data)
        {
            var damage=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(damage!=null,"Real combat hit hook exists");
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=room-1;g.enemies.Clear();g.save.energy=100;
                g.save.coins=100;g.save.inventory[0]=5;
                g.save.inventory[2]=5;g.save.inventory[4]=5;
                WorldObject start=Array.Find(g.Map.objects,o=>o.id=="chapterroad_"+room+"_0");
                WorldObject mid=Array.Find(g.Map.objects,o=>o.id=="chapterroad_"+room+"_1");
                WorldObject end=Array.Find(g.Map.objects,o=>o.id=="chapterroad_"+room+"_2");
                Check(start!=null&&mid!=null&&end!=null,"Original regional sites exist");
                Check(PlaceWithinTalkReach(g,start),"Walk to civilian origin");
                g.Talk(start);
                int take=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_begin");
                Check(take>=0,"Original rescue option remains available");
                g.Choose(take);
                Check(PlaceWithinTalkReach(g,mid),"Walk to original ambush midpoint");
                g.Talk(mid);
                int decoy=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_divert");
                Check(decoy>=0,"Real civilian decoy option is presented");
                int slot=room==1?2:room==2?0:4;
                int oldStock=g.save.inventory[slot],oldCoins=g.save.coins;
                g.Choose(decoy);
                Check(g.ChapterRoadStage==2&&g.ChapterRoadRemaining==(room>2?3:2)&&
                    (room==4?g.save.coins==oldCoins-14:
                        g.save.inventory[slot]==oldStock-1),
                    "The diversion spends genuine supplies and spawns all genuine attackers");
                foreach(Enemy enemy in g.enemies.ToArray())
                {
                    Check(enemy.stun>=1.39f&&enemy.armorOpenTime>=1.09f,
                        "Rescue tactics tangibly interrupt each real raider");
                    damage.Invoke(g,new object[]{enemy,9999f,true,null,null});
                }
                Check(g.ChapterRoadSecured,"All physical raider deaths secure the lane");
                Check(PlaceWithinTalkReach(g,end),"Walk to real civilian handoff");
                g.Talk(end);
                Check(g.conversation.textEn.Contains(room==1?"herb smoke":
                    room==2?"empty raft":room==3?"gauge alarm":"sister's scarf"),
                    "Witness names the distinct local consequence of player tactic");
                int finish=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_finish");
                Check(finish>=0,"Original chapter receipt remains available");
                g.Choose(finish);
                Check(g.ChapterRoadDone(room)&&
                    g.save.flags.Contains("chapter_road_diverted_"+room),
                    "Alternative approach survives as a durable v2-friendly story flag");
            }
        }
        public static void FourChapterCivilianRoadStoriesHaveRealWalkingFightsAndSafeHandoffs(Campaign data)
        {
            var damage=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(damage!=null,"Actual production combat contact exists");
            var seen=new System.Collections.Generic.HashSet<string>();
            // Verify the active rescued-civilian battle has real chapter
            // roles rather than four identical reskinned spawn waves.
            int[][] chapterRoles={
                new[]{0,1},new[]{1,0},
                new[]{2,0,2},new[]{2,1,0}
            };
            for(int chapter=1;chapter<=4;chapter++)
            {
                GameCore g=Fresh(data);
                g.mode="world";g.save.room=chapter;
                g.save.stage=chapter-1;
                g.enemies.Clear();
                g.save.inventory[0]=6;g.save.inventory[2]=6;
                g.save.inventory[4]=6;g.save.energy=100;
                WorldObject[] nodes=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int j=i;
                    nodes[i]=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+chapter+"_"+j&&
                        o.kind=="chapterroad");
                    Check(nodes[i]!=null&&
                        g.Map.rows[(int)nodes[i].y][(int)nodes[i].x]!='~',
                        "Chapter route uses three authored original land objects");
                    Check(seen.Add(nodes[i].name),
                        "Each region offers truly different named story scenery");
                }
                Check(PlaceWithinTalkReach(g,nodes[0]),
                    "Original chapter distress marker is on a physically reachable walk");
                Point origin=g.Position;
                g.Talk(nodes[0]);
                Check(g.conversation!=null,"Chapter "+chapter+
                    " initial original source must open dialogue");
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_begin");
                Check(start>=0&&g.conversation.textEn.Length>190&&
                    g.conversation.text.Contains("。"),
                    "Chapter's actual source opens a substantial, distinct human scene");
                int originalMoney=g.save.coins,originalShards=g.save.shards;
                g.Choose(start);
                Check(g.ChapterRoadActive&&g.ChapterRoadStage==1&&
                    g.ChapterRoadRemaining==0,
                    "Only the original real quest opening starts a protected chapter handoff");
                g.save.trackedQuest="main";
                Check(g.RecommendedContinuationGuide.id=="chapter_roads"&&
                    g.GuidanceHint()==g.GetQuest("chapter_roads").next,
                    "Active civilian road episode overrides old tracked HUD objectives");
                g.Enter(0);
                Check(g.save.room==chapter&&g.ChapterRoadActive,
                    "Unfinished new story cannot be turned into a teleport reward");
                Check(PlaceWithinTalkReach(g,nodes[1]),
                    "Hero must actually reach the distinct middle chapter scene");
                g.Talk(nodes[1]);
                Check(g.conversation!=null,"Chapter "+chapter+
                    " physically reached midpoint must open defense dialogue");
                int defend=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_defend");
                Check(defend>=0,"Real middle scene offers a live combat defense");
                Point middle=g.Position;
                g.Choose(defend);
                int defenders=chapter>=3?3:2;
                Check(g.ChapterRoadStage==2&&g.ChapterRoadRemaining==defenders&&
                    g.enemies.Count==defenders,
                    "All genuine enemy actors spawned on the original regional map");
                Enemy[] actual=g.enemies.ToArray();
                for(int role=0;role<actual.Length;role++)
                    Check(actual[role].kind==chapterRoles[chapter-1][role]&&
                        !actual[role].boss,
                        "The real civilian ambush uses its chapter's authored tactical roles");
                foreach(Enemy enemy in actual)
                {
                    Check(g.IsLiveChapterRoadEnemy(enemy)&&
                        g.CanStand(enemy.Position)&&
                        (enemy.Position-middle).Length>=3.9f&&
                        g.NavigationPathFrom(middle,enemy.Position,true).Count>0,
                        "Real enemy uses safe physical collision and a full original walking approach");
                    foreach(WorldObject o in g.Map.objects)
                        Check((enemy.Position-new Point(o.x,o.y)).Length>=3f,
                            "Authored civilian dialogue props cannot be covered by a spawned ambusher");
                }
                g.PrepareSave();
                Check(g.save.worldEnemies!=null&&
                    Array.Find(g.save.worldEnemies,r=>r!=null&&
                        (r.id==actual[0].id||r.id==actual[1].id))==null,
                    "Live scripted attackers do not leak into ordinary v2 world enemy snapshots");
                var inFlight=new GameCore(data,CloneSave(g.save));
                Check(!inFlight.ChapterRoadActive&&!inFlight.ChapterRoadDone(chapter),
                    "Reload never converts interrupted chapter combat into free completion");
                g.invulnerable=120;
                foreach(Enemy enemy in actual)
                    damage.Invoke(g,new object[]{enemy,9999f,true,null,null});
                Check(g.ChapterRoadSecured&&g.ChapterRoadRemaining==0&&
                    g.save.coins==originalMoney&&g.save.shards==originalShards,
                    "Only exact genuine raider defeats secure the route without ordinary loot");
                Check(PlaceWithinTalkReach(g,nodes[2]),
                    "Chapter "+chapter+" receiver must be walkable at ("+
                    nodes[2].x+","+nodes[2].y+"), from ("+
                    g.Position.x+","+g.Position.y+"), route length "+
                    g.NavigationPathFrom(g.Position,new Point(nodes[2].x,nodes[2].y),true).Count);
                for(int k=0;k<240&&!g.SafeTravel;k++)g.Tick(.05f,new Point());
                Check(g.SafeTravel&&g.ChapterRoadActive,
                    "Hero can safely sign at original survivor receiver after battle");
                g.Talk(nodes[2]);
                Check(g.conversation!=null,"Chapter "+chapter+
                    " physical survivor handoff must open its real choice dialogue");
                int finish=Array.FindIndex(g.conversation.choices,
                    c=>c.action==(chapter==1?
                        "chapterroad_care":"chapterroad_witness"));
                Check(finish>=0&&g.conversation.textEn.Length>180,
                    "Last handoff includes actual survivor reflection and a lasting choice");
                g.Choose(finish);
                Check(g.ChapterRoadDone(chapter)&&!g.ChapterRoadActive&&
                    g.save.coins==originalMoney+17+chapter*10&&
                    g.save.shards==originalShards+1&&
                    (chapter==1?g.ChapterRoadChoice(chapter)==1:
                        g.ChapterRoadChoice(chapter)==2),
                    "Real survivor handoff pays exactly once and records personal choice in v2 flags");
                g.Close();
                Check(PlaceWithinTalkReach(g,nodes[0]),
                    "Chapter "+chapter+" original distress site must remain physically revisitable");
                g.Talk(nodes[0]);
                Check(g.conversation!=null,"Chapter "+chapter+
                    " post-signature original distress record must remain readable");
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_begin")<0,
                    "Original story cannot be farmed repeatedly after local completion");
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.ChapterRoadDone(chapter)&&
                    loaded.ChapterRoadChoice(chapter)==(chapter==1?1:2)&&
                    !loaded.ChapterRoadActive,
                    "Permanent earned survivor choice persists under legacy v2 schema");
                var guide=g.GetQuest("chapter_roads");
                Check(guide.steps.Count==4&&guide.steps[chapter-1].done,
                    "New four-chapter storyline is connected to real journal progress");
                var main=g.GetQuest("main");
                Check(main.steps.Exists(s=>s.done&&s.text.Contains(
                    "civilian rescue"))||main.steps.Exists(s=>
                    s.done&&s.text.Contains("居民")),
                    "Original main chapter journal recognizes the new playable scene");
            }
        }

        public static void ChapterRoadAbortAndRealDeadlineCannotMintMainStoryRewards(Campaign data)
        {
            var timer=typeof(GameCore).GetField("chapterRoadTime",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(timer!=null,"Ruins and mine have a genuine world-time rescue clock");
            for(int chapter=1;chapter<=4;chapter++)
            {
                GameCore g=Fresh(data);g.mode="world";
                g.save.room=chapter;g.save.stage=chapter-1;g.enemies.Clear();
                g.save.energy=100;
                WorldObject start=Array.Find(g.Map.objects,
                    o=>o.id=="chapterroad_"+chapter+"_0");
                WorldObject end=Array.Find(g.Map.objects,
                    o=>o.id=="chapterroad_"+chapter+"_2");
                Check(start!=null&&end!=null&&
                    PlaceWithinTalkReach(g,start),"Authentic origin exists");
                g.Talk(end);
                Check(!g.ChapterRoadActive&&!g.ChapterRoadDone(chapter),
                    "Remote forged endpoint contact never starts or pays the chapter");
                g.Close();g.Talk(start);
                Check(g.conversation!=null,"Original source must reopen on the same real object");
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_begin");
                Check(begin>=0,"Source exposes one live beginning");
                int coins=g.save.coins,shards=g.save.shards;
                g.Choose(begin);
                Check(g.ChapterRoadActive,"Original chapter physically started");
                if(chapter==2||chapter==3)
                {
                    Check(g.ChapterRoadSeconds>50,
                        "Actual rising tide and hot manifold scenes impose a genuine deadline");
                    timer.SetValue(g,.01f);
                    g.Tick(.05f,new Point());
                    Check(!g.ChapterRoadActive&&!g.ChapterRoadDone(chapter)&&
                        g.save.coins==coins&&g.save.shards==shards,
                        "Expired fieldwork withdraws without money or retroactive fake evidence");
                }
                else
                {
                    Check(g.ChapterRoadSeconds==0f,
                        "Forest and city story scenes are exploration-paced without forced timers");
                    g.Close();g.Talk(start);
                    Check(g.conversation!=null,"Withdraw menu must be physically readable");
                    int abort=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="chapterroad_abort");
                    Check(abort>=0,"Original distress notice has an explicit withdrawal");
                    g.Choose(abort);
                    Check(!g.ChapterRoadActive&&!g.ChapterRoadDone(chapter)&&
                        g.save.coins==coins&&g.save.shards==shards,
                        "Civilian withdrawal cannot mint a phantom chapter receipt");
                }
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(!loaded.ChapterRoadDone(chapter)&&
                    !loaded.ChapterRoadActive,
                    "Unfinished live chapter never persists as a fabricated success");
            }
        }
        public static void CompletedMainStoryArchivesEarlierCivilianEpisodesWithoutFalseObjectives(Campaign data)
        {
            GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.ending=2;g.save.room=1;g.enemies.Clear();
            var guide=g.GetQuest("chapter_roads");
            Check(guide.locked&&!guide.complete&&guide.steps.Count==4&&
                guide.room==g.save.room&&
                (guide.target-g.Position).Length<.01f,
                "Completed-ending v2 save archives unplayed pre-finale episodes without claiming phantom clears");
            string originalTracked=g.save.trackedQuest;
            g.TrackQuest("chapter_roads");
            Check(g.save.trackedQuest==originalTracked&&
                g.GetQuest("main").complete,
                "An old finished main story must not acquire a new mandatory chapter target");
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="chapterroad_1_0");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "Original chapter site survives as physical history on an older ending save");
            int coins=g.save.coins,shards=g.save.shards;
            g.Talk(site);
            Check(g.conversation!=null&&g.conversation.choices.Length==1&&
                g.conversation.choices[0].action=="close"&&
                !g.ChapterRoadActive&&!g.ChapterRoadDone(1)&&
                g.save.coins==coins&&g.save.shards==shards,
                "Completed postgame characters may read old testimonies but never fight an impossible pre-finale quest");
        }
    }
}
