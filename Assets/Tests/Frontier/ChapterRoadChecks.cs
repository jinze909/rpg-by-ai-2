using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Exercises actual Chapter I–IV authored map data and production
        // dialogue/combat/receipt paths, not artificially awarded quest flags.
        public static void FourChapterCivilianRoadStoriesHaveRealWalkingFightsAndSafeHandoffs(Campaign data)
        {
            var damage=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(damage!=null,"Actual production combat contact exists");
            var seen=new System.Collections.Generic.HashSet<string>();
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
                Check(start>=0&&g.conversation.textEn.Length>80,
                    "Chapter's actual source opens a region-specific scene");
                int originalMoney=g.save.coins,originalShards=g.save.shards;
                g.Choose(start);
                Check(g.ChapterRoadActive&&g.ChapterRoadStage==1&&
                    g.ChapterRoadRemaining==0,
                    "Only the original real quest opening starts a protected chapter handoff");
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
                    "Final civilian evidence can only be submitted at its actual authored location");
                for(int k=0;k<240&&!g.SafeTravel;k++)g.Tick(.05f,new Point());
                Check(g.SafeTravel&&g.ChapterRoadActive,
                    "Hero can safely sign at original survivor receiver after battle");
                g.Talk(nodes[2]);
                Check(g.conversation!=null,"Chapter "+chapter+
                    " physical survivor handoff must open its real choice dialogue");
                int finish=Array.FindIndex(g.conversation.choices,
                    c=>c.action==(chapter==1?
                        "chapterroad_care":"chapterroad_witness"));
                Check(finish>=0,"Resident handoff offers a lasting regional choice");
                g.Choose(finish);
                Check(g.ChapterRoadDone(chapter)&&!g.ChapterRoadActive&&
                    g.save.coins==originalMoney+17+chapter*10&&
                    g.save.shards==originalShards+1&&
                    (chapter==1?g.ChapterRoadChoice(chapter)==1:
                        g.ChapterRoadChoice(chapter)==2),
                    "Real survivor handoff pays exactly once and records personal choice in v2 flags");
                g.Close();g.Talk(nodes[0]);
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
    }
}
