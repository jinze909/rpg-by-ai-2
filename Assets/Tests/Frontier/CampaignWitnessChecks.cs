using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void RegionalFieldSurveysActuallySpawnMultipleLocalReadableEncounters(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("scout_saved");
            g.invulnerable=900f;
            for(int room=1;room<=7;room++)
            {
                g.mode="world";g.enemies.Clear();
                g.save.room=room;
                WorldObject survey=Array.Find(g.Map.objects,
                    o=>o.id=="biomeSurvey"+room);
                Check(survey!=null&&g.CanStand(new Point(survey.x,survey.y)),
                    "Every outdoor biome has an authentic accessible first landmark");
                g.save.x=survey.x;g.save.y=survey.y;
                g.Talk(survey);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Physical regional survey opens its OWN evidence conversation");
                int note=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_note");
                Check(note>=0,"Fresh new regional route accepts the first clue");
                g.Choose(note);
                Check(g.BiomeFieldStarted&&g.enemies.Count>=2&&
                    g.enemies.Count<=3,
                    "Every visited far-side biome actually offers at least two separate supplementary battles");
                for(int i=0;i<g.enemies.Count;i++)
                {
                    Enemy e=g.enemies[i];
                    Check(g.CanStand(e.Position)&&
                        (e.Position-new Point(survey.x,survey.y)).Length>6f,
                        "Region-specific scout really stands in open flankable terrain away from its field source");
                    foreach(WorldObject objectSite in g.Map.objects)
                        Check((e.Position-new Point(objectSite.x,objectSite.y)).Length>=4.2f,
                            "No side patrol materializes on original quest interactions");
                    for(int k=0;k<i;k++)
                        Check((e.Position-g.enemies[k].Position).Length>=4.5f,
                            "Spawned region patrols do not stack on one poor encounter point");
                }
            }
        }
        public static void NewOverworldBiomesKeepPhysicalQuestPathsAndHaveDifferentRemoteSilhouettes(Campaign data)
        {
            // Source maps have 120x80 cells, but a different-looking ASCII
            // mask is not enough. Check all 16 earned field sites are
            // actually reachable using GameCore's production actor feet.
            var g=Fresh(data);
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                g.save.room=room;
                g.save.x=map.spawnX;g.save.y=map.spawnY;
                int w=map.rows[0].Length,h=map.rows.Length;
                Check(w==120&&h==80&&g.CanStand(g.Position),
                    "Every reworked region retains the original standable arrival foot");
                int[] queue=new int[w*h],dirsX={1,-1,0,0},dirsY={0,0,1,-1};
                bool[] visited=new bool[w*h];
                int start=(int)map.spawnY*w+(int)map.spawnX,front=0,end=0;
                queue[end++]=start;visited[start]=true;
                while(front<end)
                {
                    int node=queue[front++],x=node%w,y=node/w;
                    for(int direction=0;direction<4;direction++)
                    {
                        int xx=x+dirsX[direction],yy=y+dirsY[direction];
                        if(xx<2||yy<2||xx>=w-2||yy>=h-2)continue;
                        int next=yy*w+xx;
                        if(visited[next]||!g.CanStand(new Point(xx+.5f,yy+.5f)))
                            continue;
                        visited[next]=true;queue[end++]=next;
                    }
                }
                Check(end>3500,
                    "The new biome's safe walking region remains sizable, not a sealed grid");
                foreach(WorldObject source in map.objects)
                {
                    if(source==null||source.kind!="biomeSurvey"&&
                       source.kind!="biomeAnswer")continue;
                    int x=(int)source.x,y=(int)source.y;
                    Check(x>=2&&y>=2&&x<w-2&&y<h-2&&
                        visited[y*w+x]&&g.CanStand(new Point(source.x,source.y)),
                        "All two-site biome missions remain reachable through actual game collision, not map glyphs");
                }
            }
            // Match only the old, materially similar distant regions:
            // a fresh forest, mine, desert and snowfield must now present
            // hundreds of genuinely different walkable/blocked terrain cells.
            int[] compare={1,3,6,7};
            for(int i=0;i<compare.Length;i++)
            for(int j=i+1;j<compare.Length;j++)
            {
                int differences=0;
                MapData a=data.maps[compare[i]],b=data.maps[compare[j]];
                for(int y=27;y<75;y++)
                for(int x=37;x<116;x++)
                    if(a.rows[y][x]!=b.rows[y][x])differences++;
                Check(differences>500,
                    "Remote forest, mine, desert and ice terrain must not remain recolored copies");
            }
        }
        public static void EarnedRegionalRelaysRequireRealCompletedWalkAndSafePaidTwoWayTravel(Campaign data)
        {
            var g=Fresh(data);
            g.enemies.Clear();g.invulnerable=900f;
            MapData town=g.Map;
            WorldObject first=Array.Find(town.objects,o=>o.id=="biomeSurvey0");
            WorldObject last=Array.Find(town.objects,o=>o.id=="biomeAnswer0");
            Check(first!=null&&last!=null,
                "The exact two authored town evidence stones still exist");
            Check(PlaceWithinTalkReach(g,first),
                "An unearned source must be physically reachable");
            g.Talk(first);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_relay")<0,
                "Uncompleted field evidence never unlocks a free regional ferry");
            g.Close();
            // Earlier dedicated investigations check the genuine two-site
            // reasoning and reward; this fixture validates the persistent
            // v2 completion gate independently from walking speed.
            g.save.flags.Add("biome_field_read_0");
            g.save.flags.Add("biome_field_done_0");
            g.Talk(first);
            int forward=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_relay");
            Check(forward>=0,
                "A completed original field marker now offers a real two-way service");
            g.save.coins=60;g.save.energy=100f;
            int previousCoins=g.save.coins;
            g.Choose(forward);
            Check(g.mode=="world"&&
                (g.Position-new Point(last.x,last.y)).Length<2f&&
                g.save.coins==previousCoins-3&&
                Math.Abs(g.save.energy-88f)<.001f,
                "An earned ride moves onto genuine open far-side ground and charges once");
            Check(g.CanStand(g.Position),
                "Physical relay never places the hero inside water, cliff or building");
            Check(PlaceWithinTalkReach(g,last),
                "Far endpoint offers a matching real return station");
            g.Talk(last);
            int back=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_relay");
            Check(back>=0,"Reverse travel starts only at the true remote monument");
            g.Choose(back);
            Check((g.Position-new Point(first.x,first.y)).Length<2f&&
                g.save.coins==previousCoins-6&&
                Math.Abs(g.save.energy-76f)<.001f,
                "Returning from the other end uses the same physical and economic rules");
            Check(PlaceWithinTalkReach(g,first),"Return source remains reachable");
            g.Talk(first);
            int threat=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_relay");
            Point departure=g.Position;
            g.enemies.Add(new Enemy(997,0,departure.x+1f,
                departure.y,40f));
            int unsafeCoins=g.save.coins;float unsafeEnergy=g.save.energy;
            g.Choose(threat);
            Check((g.Position-departure).Length<.001f&&
                g.save.coins==unsafeCoins&&
                Math.Abs(g.save.energy-unsafeEnergy)<.001f,
                "Live enemy pressure blocks a protected field relay without spending a coin");
            g.enemies.Clear();
            g.Close();
            g.Talk(first);
            int underfunded=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_relay");
            g.save.coins=0;g.save.energy=5f;
            g.Choose(underfunded);
            Check((g.Position-departure).Length<.001f&&
                g.save.coins==0&&Math.Abs(g.save.energy-5f)<.001f,
                "Underfunded replay cannot get a free full-map shortcut");
            g.Close();g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.Has("biome_field_done_0")&&
                restored.save.version==2,
                "Relay access derives solely from existing persistent fieldwork flags");
        }
        public static void ReworkedBiomesHaveDistinctDistantFieldChallengesAndOneTimeAtlas(Campaign data)
        {
            var g=Fresh(data);
            g.save.energy=100f;
            int[] answers={1,0,2,0,2,1,1,0};
            int baseCoins=g.save.coins;
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                Check(map.rows.Length==80&&map.rows[0].Length==120&&
                    map.decor.Length>=200,
                    "Each giant biome has actual substantial authored furnishings and unique terrain");
                WorldObject a=Array.Find(map.objects,
                    o=>o.id=="biomeSurvey"+room);
                WorldObject b=Array.Find(map.objects,
                    o=>o.id=="biomeAnswer"+room);
                Check(a!=null&&b!=null&&a.kind=="biomeSurvey"&&
                    b.kind=="biomeAnswer"&&
                    (new Point(a.x,a.y)-new Point(b.x,b.y)).Length>35f,
                    "Region "+room+" has two ORIGINAL far-separated authored evidence sites");
                g.Close();
                g.save.room=room;
                g.save.x=map.spawnX;g.save.y=map.spawnY;
                g.enemies.Clear();
                Check(!g.BiomeFieldStarted&&!g.BiomeFieldDone,
                    "Unvisited area cannot inherit another biome's completion");
                Check(PlaceWithinTalkReach(g,b),
                    "Remote interpretation monument has genuine reachable physical footing");
                g.Talk(b);
                Check(g.mode=="dialog"&&g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="biome_pick_"+answers[room])<0,
                    "Remote evidence cannot be guessed before visiting first site");
                g.Close();
                Check(PlaceWithinTalkReach(g,a),
                    "First field monument has genuine reachable physical footing");
                g.Talk(a);
                int note=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_note");
                Check(note>=0,"Physical first landmark permits field notes");
                g.Choose(note);
                Check(g.BiomeFieldStarted&&!g.BiomeFieldDone&&
                    (g.BiomeFieldNextTarget-new Point(b.x,b.y)).Length<.001f,
                    "Live quest target switches to distant original object only after physical inspection");
                Check(PlaceWithinTalkReach(g,b),
                    "Second monument has a valid original on-site interaction");
                g.Talk(b);
                int wrong=(answers[room]+1)%3;
                int wrongChoice=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_pick_"+wrong);
                Check(wrongChoice>=0,"Actual remote interpretation offers multiple distinct choices");
                g.save.energy=100f;
                g.Choose(wrongChoice);
                Check(!g.BiomeFieldDone&&
                    Math.Abs(g.save.energy-94f)<.001f,
                    "Wrong regional interpretation costs exactly 6 real energy and cannot earn treasure");
                g.Talk(b);
                int right=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_pick_"+answers[room]);
                int coins=g.save.coins;
                Check(right>=0,"The authentic distant witness offers a correct interpretation");
                g.Choose(right);
                Check(g.BiomeFieldDone&&g.BiomeFieldNotes==room+1&&
                    g.save.coins==coins+24,
                    "Regional puzzle has a single physically earned v2 reward with no fabricated victory");
                g.Talk(b);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="biome_pick_"+answers[room])<0&&
                    g.save.coins==coins+24,
                    "Revisiting a resolved site cannot duplicate field rewards");
                g.Close();
            }
            Check(g.BiomeFieldNotes==8&&g.Has("biome_field_atlas")&&
                g.save.coins==baseCoins+8*24,
                "Eight different lived-in region routes complete ONE shared atlas");
            g.enemies.Clear();g.save.room=0;g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.BiomeFieldNotes==8&&
                loaded.Has("biome_field_atlas"),
                "Full regional atlas remains durable in unchanged legacy SaveData v2");
        }
        public static void RegionalEncounterWakeRangePreservesDistantLocalCombat(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();g.invulnerable=900f;
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="biomeSurvey1");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "An original distant field survey can be reached in person");
            g.Talk(site);
            int note=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_note");
            Check(note>=0,"Optional remote fieldwork has a real sourced start");
            g.Choose(note);
            Check(g.BiomeFieldStarted&&g.enemies.Count>=1&&
                g.enemies.Count<=3,
                "Explicit remote fieldwork spawns a small real local patrol, not an automatic map-wide mandatory wave");
            Enemy patrol=g.enemies[0];
            Point foot=patrol.Position;
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;
            g.mode="world";
            for(int t=0;t<30;t++)g.Tick(.05f,new Point());
            Check((patrol.Position-foot).Length<.001f&&
                !patrol.moving&&patrol.state==0,
                "Remote OPTIONAL raider holds its regional territory instead of pursuing a distant hero");
            g.save.x=site.x;g.save.y=site.y;
            for(int t=0;t<60;t++)g.Tick(.05f,new Point());
            Check(patrol.state!=0||(patrol.Position-foot).Length>.01f,
                "The SAME optional physical raider wakes into ordinary combat when the hero returns locally");
        }
        public static void ReworkedRegionalEnemiesSpreadAcrossReachableCombatSectors(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("SpawnRoom",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(spawn!=null,"Use real production room-spawn transaction");
            for(int room=1;room<=7;room++)
            {
                var g=new GameCore(data);g.save.room=room;
                spawn.Invoke(g,null);
                int required=room<=2?6:room<=4?8:7;
                int originalCount=0;
                foreach(Enemy e in g.enemies)
                {
                    if(e.id>required)continue;
                    originalCount++;
                    Check(g.CanStand(e.Position)&&
                        e.Position.x<40f&&e.Position.y<28f,
                        "The original critical-path wave stays playable at its real near-side combat source");
                }
                Check(originalCount==required,
                    "Outdoor room "+room+" retains its COMPLETE original mandatory wave");
                Check(Array.Exists(g.Map.objects,
                    o=>o.id=="biomeSurvey"+room)&&
                    Array.Exists(g.Map.objects,
                    o=>o.id=="biomeAnswer"+room),
                    "The same region now has separate authored far-side optional encounters");
            }
        }
        public static void CampaignWitnessesArePhysicalThreeChapterStoryNotFreeFlags(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("campaign_witness").locked &&
                g.WitnessesFiled==0,"An untouched save must not grant unseen citizens a testimony");
            for(int region=1;region<=3;region++)
            {
                // Mirror chapter unlocks; never pretend the previous chapter
                // was completed by the new optional story interaction.
                g.save.stage=region-1;
                if(region==1)g.save.flags.Add("scout_saved");
                else g.save.flags.Add("gate_"+region);
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                WorldObject npc=Object(g,"witnessnpc"+region),
                    first=Object(g,"witnesssite1"+region),
                    second=Object(g,"witnesssite2"+region);
                foreach(WorldObject o in new[]{npc,first,second})
                {
                    g.save.x=o.x;g.save.y=o.y;
                    Check(g.CanStand(g.Position) && g.Nearest()==o,
                        "Every newly authored chapter witness site is physically usable on original terrain: "+o.kind);
                }
                g.save.x=second.x;g.save.y=second.y;
                g.Talk(second);
                Check(g.conversation==null &&
                    !g.Has("witness_sealed_"+region),
                    "A field marker cannot fabricate a witness record before meeting its owner");
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                Check(g.Has("witness_started_"+region) &&
                    g.GetQuest("campaign_witness").id=="campaign_witness",
                    "A real named NPC opens an actual tracked main story investigation");
                g.save.x=first.x;g.save.y=first.y;
                Check(g.Nearest()==first,"First independent record must be interactable");
                g.Talk(first);
                string pricedAction="witness_answer_"+region+"_"+(region==1?1:2);
                Check(g.conversation!=null &&
                    Array.Exists(g.conversation.choices,c=>
                        c.action==pricedAction &&
                        (region==2 || c.en.Contains("cost: 1"))),
                    "Real field choices disclose the required Moonherb or Machine part before consuming it");
                g.Close();
                if(region==1 || region==3)
                {
                    int material=region==1?2:4, savedCount=g.save.inventory[material];
                    string sourceKind=region==1?"herb":"ore";
                    g.save.inventory[material]=0;
                    // A closer-looking herb behind a real wall must NOT
                    // steal the objective from a reachable original node.
                    // Build only a test-time sealed pocket near the archive,
                    // then restore all authored map objects and tiles.
                    if(region==1)
                    {
                        MapData local=g.Map;
                        string[] oldRows=local.rows;
                        WorldObject[] oldObjects=local.objects;
                        var fake=new WorldObject{id="test-sealed-herb",
                            kind="herb",name="Inaccessible herb",
                            en="Inaccessible herb",x=first.x-2f,y=first.y+3f};
                        var added=new WorldObject[oldObjects.Length+1];
                        Array.Copy(oldObjects,added,oldObjects.Length);
                        added[oldObjects.Length]=fake;
                        string[] blocked=(string[])oldRows.Clone();
                        for(int row=18;row<=20;row++)
                        {
                            char[] tiles=blocked[row].ToCharArray();
                            for(int col=11;col<=13;col++)tiles[col]='#';
                            blocked[row]=new string(tiles);
                        }
                        local.rows=blocked;
                        local.objects=added;
                        try
                        {
                            var candidate=g.GetQuest("campaign_witness");
                            Check(candidate.room==region &&
                                (candidate.target-new Point(fake.x,fake.y)).Length>.01f &&
                                candidate.next.Contains("月光草"),
                                "A nearby resource behind a sealed wall cannot hijack live witness guidance");
                            var harvested=new System.Collections.Generic.List<string>();
                            foreach(WorldObject original in oldObjects)
                                if(original!=null&&original.kind=="herb" &&
                                    !g.Has("node_"+original.id))
                                {
                                    string key="node_"+original.id;
                                    g.save.flags.Add(key);
                                    harvested.Add(key);
                                }
                            var sealedRoute=g.GetQuest("campaign_witness");
                            Check(sealedRoute.room==region &&
                                (sealedRoute.target-g.Position).Length<.01f &&
                                sealedRoute.next.Contains("暂时无法到达"),
                                "A real but unreachable resource must not be mislabeled exhausted or shown as a false waypoint");
                            foreach(string key in harvested)g.save.flags.Remove(key);
                        }
                        finally
                        {
                            local.rows=oldRows;
                            local.objects=oldObjects;
                        }
                    }
                    var gatherQuest=g.GetQuest("campaign_witness");
                    Check(gatherQuest.room==region &&
                        Array.Exists(g.Map.objects,o=>o!=null &&
                            o.kind==sourceKind && !g.Has("node_"+o.id) &&
                            (gatherQuest.target-new Point(o.x,o.y)).Length<.01f) &&
                        (region==1?gatherQuest.next.Contains("月光草"):
                            gatherQuest.next.Contains("零件")),
                        "Missing genuine witness supplies redirect the live compass to harvestable regional nodes");
                    var marked=new System.Collections.Generic.List<string>();
                    foreach(WorldObject node in g.Map.objects)
                        if(node!=null&&node.kind==sourceKind&&!g.Has("node_"+node.id))
                        {
                            string flag="node_"+node.id;
                            g.save.flags.Add(flag);marked.Add(flag);
                        }
                    var exhausted=g.GetQuest("campaign_witness");
                    WorldObject rest=Array.Find(data.maps[0].objects,o=>o.kind=="rest");
                    Check(rest!=null&&exhausted.room==0 &&
                        (exhausted.target-new Point(rest.x,rest.y)).Length<.01f &&
                        exhausted.next.Contains("休息"),
                        "Exhausted resource nodes navigate to the real town bench that renews them");
                    foreach(string flag in marked)g.save.flags.Remove(flag);
                    g.save.inventory[material]=savedCount;
                    var resumed=g.GetQuest("campaign_witness");
                    Check(resumed.room==region &&
                        (resumed.target-new Point(first.x,first.y)).Length<.01f,
                        "Replenished supplies restore the original first-site quest target without persistent state");
                }
                float initialEnergy=g.save.energy;
                int wrong=region==1?0:1;
                if(region==1)
                {
                    g.save.x=first.x;g.save.y=first.y;g.Talk(first);
                    int stale=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="witness_answer_"+region+"_"+wrong);
                    Check(stale>=0,"The physical first record exposes a real interpretation choice");
                    Point outside=StandableOutsideTalkReach(g,first);
                    float energyBeforeStale=g.save.energy;
                    g.save.x=outside.x;g.save.y=outside.y;g.Choose(stale);g.Close();
                    Check(!g.Has("witness_read_"+region)&&g.save.energy==energyBeforeStale,
                        "A stale witness-answer dialog cannot change evidence or drain energy remotely");
                    g.save.x=first.x;g.save.y=first.y;
                }
                Action(g,first.kind,"witness_answer_"+region+"_"+wrong);
                Check(g.save.energy==initialEnergy-8 &&
                    !g.Has("witness_read_"+region),
                    "Wrong historical reasoning costs energy, never invents original evidence");
                // Supply a real field medicine/part where the plot requires one.
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                int item=region==1?2:region==3?4:-1;
                int prior=item>=0?g.save.inventory[item]:0;
                Action(g,first.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                Check(g.Has("witness_read_"+region) && 
                    (item<0 || g.save.inventory[item]==prior-1),
                    "Right action physically restores the first record with genuine chapter-specific materials");
                g.save.x=second.x;g.save.y=second.y;
                Check(g.Nearest()==second,"Final evidence seal must be usable on real map");
                int oldCoins=g.save.coins,oldKills=g.save.kills[region];
                SetOuterPatrolLull(g,region);
                Check(g.OuterPatrolActive&&g.SafeTravel,
                    "The testimony marker is locally safe during an unrelated patrol lull");
                Action(g,second.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==0&&!g.enemies.Exists(g.IsWitnessEnemy),
                    "Witness security cannot start over another active field mission");
                ClearOuterPatrolLull(g);
                Action(g,second.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region &&
                    g.WitnessDefenseTime>=64 &&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Live chapter defense spawns two actual patrol guards within 65 seconds");
                g.Enter(0);
                Check(g.save.room==region && g.WitnessDefenseRoom==region,
                    "A story combat cannot be evaded by opening fast travel");
                g.PrepareSave();
                Check(!Array.Exists(g.save.worldEnemies,x=>x!=null && 
                    x.id>=89972 && x.id<=89977),
                    "Field-story encounter guards do not pollute ordinary enemy saves");
                var reloaded=new GameCore(data,CloneSave(g.save));reloaded.Start();
                Check(reloaded.WitnessDefenseRoom==0 &&
                    !reloaded.enemies.Exists(reloaded.IsWitnessEnemy) &&
                    reloaded.Has("witness_read_"+region) &&
                    !reloaded.Has("witness_defeated_"+region),
                    "Old v2 serialization preserves verified memory but never teleports ghost guards into a new session");
                g.save.x=second.x;g.save.y=second.y;
                g.Talk(second);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="witness_seal_"+region)<0 &&
                    !g.Has("witness_defeated_"+region),
                    "Reading and accepting a quest cannot skip the combat objective");
                g.Close();
                foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {
                    e.stun=100;e.timer=100;e.hp=3;
                    e.x=second.x+1.05f;e.y=second.y;
                    g.save.x=second.x;g.save.y=second.y;
                    for(int tick=0;tick<100 && g.enemies.Contains(e);tick++)
                    {
                        if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                        g.Tick(.02f,new Point());
                    }
                    Check(!g.enemies.Contains(e),
                        "Canonical gold-haired hero must physically defeat each chapter security guard");
                }
                g.Tick(.02f,new Point());
                Check(g.WitnessDefenseRoom==0 &&
                    g.Has("witness_defeated_"+region) &&
                    !g.Has("witness_complete_"+region) &&
                    g.save.coins==oldCoins && g.save.kills[region]==oldKills,
                    "Only combat clears security; no premature story rewards or farming chapter kills");
                g.save.x=second.x;g.save.y=second.y;
                Action(g,second.kind,"witness_seal_"+region);
                Check(g.Has("witness_sealed_"+region) &&
                    !g.Has("witness_complete_"+region),
                    "A separate field archive action is needed after the genuine fight");
                g.save.x=npc.x;g.save.y=npc.y;
                if(region==1)
                {
                    g.Talk(npc);
                    int finish=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="witness_finish_"+region);
                    Check(finish>=0,"The living witness offers a real signature choice after field preservation");
                    Point outsideNpc=StandableOutsideTalkReach(g,npc);
                    int beforeCoins=g.save.coins,beforeXp=g.save.xp;
                    g.save.x=outsideNpc.x;g.save.y=outsideNpc.y;g.Choose(finish);g.Close();
                    Check(!g.Has("witness_complete_"+region)&&g.save.coins==beforeCoins&&
                        g.save.xp==beforeXp,
                        "A stale witness-signature dialog cannot award chapter rewards remotely");
                    g.save.x=npc.x;g.save.y=npc.y;
                }
                Action(g,npc.kind,"witness_finish_"+region);
                Check(g.Has("witness_complete_"+region) &&
                    g.WitnessesFiled==region &&
                    g.save.coins==oldCoins+15+region*10,
                    "Actual living witness signs one truthful testimony and receives one chapter reward");
                g.Talk(npc);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="witness_finish_"+region)<0,
                    "A completed witness cannot duplicate the chapter reward");
                g.Close();
            }
            Check(g.AllWitnessesFiled &&
                g.GetQuest("campaign_witness").complete,
                "Three fully experienced chapters culminate in three publicly signed accounts");
            g.PrepareSave();
            var persisted=new GameCore(data,CloneSave(g.save));persisted.Start();
            Check(persisted.save.version==2 && persisted.AllWitnessesFiled,
                "Full three-witness main story survives SaveData v2 serialization without a schema change");
        }

        public static void CampaignWitnessesWeakenOnlyRealChapterFourSecurity(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string f in new[]{"core_1","core_2","core_3","triad_started",
                "triad_evidence_1","triad_evidence_2","triad_evidence_3"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();
            WorldObject desk=Object(g,"triadnexus");
            g.save.x=desk.x;g.save.y=desk.y;
            Action(g,desk.kind,"triad_defense_start");
            Enemy original=g.enemies.Find(g.IsTriadEnemy);
            Check(original!=null && Math.Abs(original.maxHp-210)<.1f,
                "Original untouched saves retain the established 210-HP security boss");
            g.save.x=desk.x;g.save.y=desk.y;
            Action(g,desk.kind,"triad_abort");g.enemies.Clear();
            for(int i=1;i<=3;i++)g.save.flags.Add("witness_complete_"+i);
            Action(g,desk.kind,"triad_defense_start");
            Enemy reduced=g.enemies.Find(g.IsTriadEnemy);
            Check(reduced!=null && Math.Abs(reduced.maxHp-165)<.1f &&
                reduced.hp>0 && !g.Has("triad_defense_clear"),
                "Three physical testimony completions weaken real security from 210 to 165 without a victory skip");
        }

        public static void CampaignWitnessesAbortTimeoutAndChroniclePreserveOnlyEarnedHistory(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("scout_saved");g.Enter(1);
            g.enemies.Clear();WorldObject npc=Object(g,"witnessnpc1"),
                first=Object(g,"witnesssite11"),second=Object(g,"witnesssite21");
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"witness_begin_1");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,first.kind,"witness_answer_1_1");
            g.save.x=second.x;g.save.y=second.y;g.invulnerable=120;
            Action(g,second.kind,"witness_defend_1");
            foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {e.stun=200;e.timer=200;}
            Advance(g,3.5f);
            Check(g.hazards.Exists(h=>h.witnessOwned && h.timer>0 && h.damage>0),
                "A real delayed forest field hazard appears and would hurt a stationary hero");
            var other=new Hazard{position=new Point(12,14),timer=90,
                radius=.6f,damage=2,kind="environment"};
            var effect=new Effect("discover",new Point(11,14),90);
            g.hazards.Add(other);g.effects.Add(effect);
            g.save.x=second.x;g.save.y=second.y;
            Action(g,second.kind,"witness_abort_1");
            Check(g.WitnessDefenseRoom==0 && !g.enemies.Exists(g.IsWitnessEnemy) &&
                !g.hazards.Exists(h=>h.witnessOwned) &&
                g.hazards.Contains(other) && g.effects.Contains(effect) &&
                g.Has("witness_read_1") && !g.Has("witness_defeated_1"),
                "Field withdrawal retires only its owned combat resources, never unrelated hazards or legitimate evidence");
            g.hazards.Clear();g.effects.Clear();g.enemies.Clear();
            Action(g,second.kind,"witness_defend_1");
            Check(g.WitnessDefenseRoom==1,"A real free retry can start");
            foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {e.stun=200;e.timer=200;}
            g.invulnerable=200;Advance(g,65.3f);
            Check(g.WitnessDefenseRoom==0 && !g.Has("witness_defeated_1") &&
                !g.enemies.Exists(g.IsWitnessEnemy),
                "Timeout retires its enemy state without inventing a witness victory");
            g.Enter(0);g.save.x=13.5f;g.save.y=9.5f;
            WorldObject book=Object(g,"worldchronicle");
            g.Talk(book);
            int section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_campaign");
            g.Choose(section);
            int entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_testimonies");
            Check(entry>=0,"The chronicle unlocks only after genuine interaction with a witness");
            g.Choose(entry);
            Check(g.conversation.text.Contains("莫芽尚未署名") &&
                !g.conversation.text.Contains("莫芽署名："),
                "Unfinished evidence must not be described as a living resident's signature");
            g.Close();
            g.save.flags.Add("witness_complete_1");
            g.Talk(book);
            section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_campaign");
            g.Choose(section);
            entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_testimonies");
            g.Choose(entry);
            Check(g.conversation.text.Contains("莫芽署名：") &&
                !g.conversation.text.Contains("阿菱署名："),
                "Bilingual campaign archive publishes only truly signed chapter witnesses");
        }

        public static void SignedCampaignWitnessesOpenOnlySafeOnSitePublicRefuges(Campaign data)
        {
            var g=Fresh(data);
            for(int region=1;region<=3;region++)
            {
                g.save.stage=region-1;
                if(region==1)g.save.flags.Add("scout_saved");
                else g.save.flags.Add("gate_"+region);
                g.Enter(region);g.enemies.Clear();
                WorldObject marker=Object(g,"witnesssite2"+region);
                g.save.x=marker.x;g.save.y=marker.y;
                g.save.health=40;g.invulnerable=50;
                Advance(g,2);
                Check(g.save.health==40,
                    "An unfiled witness cannot secretly create a public healing refuge");
                g.save.flags.Add("witness_complete_"+region);
                Advance(g,2);
                Check(g.save.health>42 && g.save.health<45,
                    "A genuinely signed chapter story turns its actual field seal into modest out-of-combat recovery");
                g.save.health=40;
                g.save.x=marker.x+5f;g.save.y=marker.y;
                Advance(g,2);
                Check(g.save.health==40,
                    "Witness field refuge has real radius limits, never heals the entire map");
                g.save.x=marker.x;g.save.y=marker.y;
                Enemy attacker=new Enemy(88123+region,0,marker.x+.7f,marker.y,20);
                attacker.stun=200;attacker.timer=200;g.enemies.Add(attacker);
                g.save.health=40;
                Advance(g,2);
                Check(g.save.health==40,
                    "A public story refuge cannot erase combat risk while an enemy contests its location");
                g.enemies.Clear();
            }
        }

        public static void CampaignWitnessSignaturesActuallyChangeLyraAndFinaleSpeech(Campaign data)
        {
            var g=Fresh(data);
            WorldObject lyra=Object(g,"guild");
            g.save.x=lyra.x;g.save.y=lyra.y;
            g.save.flags.Add("core_1");
            g.Talk(lyra);
            Check(!g.conversation.text.Contains("莫芽的证词"),
                "First core delivery must not claim unearned resident testimony");
            g.Close();
            g.save.flags.Add("witness_complete_1");
            g.Talk(lyra);
            Check(g.conversation.text.Contains("莫芽的证词") &&
                g.conversation.textEn.Contains("Moya's testimony"),
                "Lyra acknowledges the first chapter's actual signed account during original core delivery");
            g.Close();
            g.save.stage=3;
            foreach(string f in new[]{"core_2","core_3","witness_complete_2","witness_complete_3"})
                g.save.flags.Add(f);
            g.Talk(lyra);
            Check(g.conversation.text.Contains("三位证人") &&
                g.conversation.textEn.Contains("signed their original field testimonies"),
                "The new living witnesses are named when Lyra proposes the true Chapter Four audit");
            g.Close();
            g.save.flags.Add("boss_4");
            g.Enter(4);g.enemies.Clear();
            WorldObject shrine=Object(g,"shrine");
            g.save.x=shrine.x;g.save.y=shrine.y;
            g.Talk(shrine);
            Check(g.conversation.text.Contains("莫芽、阿菱和维恩") &&
                g.conversation.textEn.Contains("Moya, Arin and Venn"),
                "Final confrontation includes the real people found earlier in the game");
            g.Close();
            g.save.ending=1;g.Epilogue();
            Check(g.conversation.text.Contains("密林的莫芽") &&
                g.conversation.textEn.Contains("Moya of Thornwood"),
                "Original ending remains intact and reflects real first-three-chapter survivors");
        }


        public static void ActiveChapterWitnessGuidanceTracksTheActualCurrentCharacter(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=2;
            foreach(string f in new[]{"scout_saved","gate_2","gate_3"})
                g.save.flags.Add(f);
            g.Enter(2);g.enemies.Clear();
            WorldObject arin=Object(g,"witnessnpc2"),
                first=Object(g,"witnesssite12"),second=Object(g,"witnesssite22");
            g.save.x=arin.x;g.save.y=arin.y;
            Action(g,arin.kind,"witness_begin_2");
            QuestGuide quest=g.CurrentGuide;
            Check(quest.id=="campaign_witness" && quest.room==2 &&
                (quest.target-new Point(first.x,first.y)).Length<.01f,
                "Starting Arin's live chapter must navigate to her first site, not an earlier unfinished Moya quest");
            g.save.flags.Add("witness_read_2");
            quest=g.CurrentGuide;
            Check(quest.room==2 &&
                (quest.target-new Point(second.x,second.y)).Length<.01f,
                "After authentic field evidence, live guidance points to the physical defense location");
            g.save.flags.Add("witness_sealed_2");
            quest=g.CurrentGuide;
            Check(quest.room==2 &&
                (quest.target-new Point(arin.x,arin.y)).Length<.01f,
                "A preserved testimony correctly routes back to the actual living survivor for signature");
            g.save.flags.Add("witness_complete_2");
            quest=g.CurrentGuide;
            Check(quest.room==1 && !quest.complete,
                "Finishing a middle chapter leaves earlier and later stories available without fabricating completion");
            g.Enter(3);g.enemies.Clear();
            g.save.flags.Add("witness_started_3");
            quest=g.CurrentGuide;
            Check(quest.room==3 &&
                quest.next.Contains("第一处原始记录"),
                "A newly undertaken miner investigation outranks incomplete earlier optional testimony");
        }





        public static void WitnessSecurityNeverSpawnsInsidePhysicalCompanion(Campaign data)
        {
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.save.flags.Add("companion_1");
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                var npc=Object(g,"witnessnpc"+region);
                var source=Object(g,"witnesssite1"+region);
                var archive=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=source.x;g.save.y=source.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,source.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=archive.x;g.save.y=archive.y;
                g.save.companion=-1;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "The real witness encounter can place its two original connected guards");
                Point authored=g.enemies.Find(g.IsWitnessEnemy).Position;
                foreach(Enemy guard in g.enemies.FindAll(g.IsWitnessEnemy))
                    foreach(WorldObject o in g.Map.objects)
                        if(g.IsObjectPresent(o))
                            Check((guard.Position-new Point(o.x,o.y)).Length>=1.35f,
                                "A guard must not occupy the foot of a visible resident or field device");
                Action(g,archive.kind,"witness_abort_"+region);
                Check(g.WitnessDefenseRoom==0,"Original archive can safely abort the first encounter");
                // Put a live field object at a previously legal guard foot.
                // Redeployment must reroute two guards or fail without a partial spawn.
                MapData fieldMap=g.Map;
                WorldObject[] originalObjects=fieldMap.objects;
                WorldObject visitor=new WorldObject{id="test-witness-visitor",
                    kind="history",name="Visiting resident",en="Visiting resident",
                    x=authored.x,y=authored.y};
                var crowded=new WorldObject[originalObjects.Length+1];
                Array.Copy(originalObjects,crowded,originalObjects.Length);
                crowded[originalObjects.Length]=visitor;
                fieldMap.objects=crowded;
                try
                {
                    Action(g,archive.kind,"witness_defend_"+region);
                    if(g.WitnessDefenseRoom==region)
                    {
                        Check(g.enemies.FindAll(g.IsWitnessEnemy).Count==2 &&
                            g.enemies.FindAll(g.IsWitnessEnemy).TrueForAll(e=>
                                (e.Position-authored).Length>=1.35f),
                            "Live interactable in deployment lane forces both original guards onto different feet");
                        Action(g,archive.kind,"witness_abort_"+region);
                    }
                    else
                        Check(g.enemies.FindAll(g.IsWitnessEnemy).Count==0,
                            "Crowded interactive terrain aborts deployment atomically");
                    Check(fieldMap.objects[fieldMap.objects.Length-1]==visitor,
                        "Witness combat cannot delete an unrelated world object");
                }
                finally { fieldMap.objects=originalObjects; }
                g.save.companion=1;
                g.companionPosition=authored;
                Action(g,archive.kind,"witness_defend_"+region);
                if(g.WitnessDefenseRoom==region)
                {
                    var guards=g.enemies.FindAll(g.IsWitnessEnemy);
                    Check(guards.Count==2&&
                        (guards[0].Position-guards[1].Position).Length>=1.8f,
                        "Both genuine guards have distinct original deployment feet");
                    foreach(Enemy e in guards)
                        Check((e.Position-g.companionPosition).Length>=1.6f&&
                            g.CanStand(e.Position)&&
                            (e.Position-g.Position).Length>=1.6f,
                            "A physically stationed recruited ally can never share a newly spawned hostile foot");
                    Action(g,archive.kind,"witness_abort_"+region);
                }
                else
                {
                    Check(g.enemies.FindAll(e=>e.id>=89972&&e.id<=89977).Count==0,
                        "If an ally occupies the only safe formation, no partially spawned guardians remain");
                }
                g.companionPosition=g.Position;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Repositioning the ally restores the authored two-guardian challenge with no lost evidence");
            }
        }

        public static void WitnessAbortRetiresOnlyActualGuardsAfterRegionTransition(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            WorldObject npc=Object(g,"witnessnpc1");
            WorldObject first=Object(g,"witnesssite11");
            WorldObject archive=Object(g,"witnesssite21");
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"witness_begin_1");
            g.save.x=first.x;g.save.y=first.y;
            g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
            Action(g,first.kind,"witness_answer_1_1");
            g.save.x=archive.x;g.save.y=archive.y;
            Action(g,archive.kind,"witness_defend_1");
            var owned=g.enemies.FindAll(g.IsWitnessEnemy);
            Check(owned.Count==2,"Original forest witness defense registers two real live actors");
            var other=new Enemy(owned[0].id,owned[0].kind,
                owned[0].x,owned[0].y,65);
            g.enemies.Add(other);
            var hazard=new Hazard{position=g.Position,damage=5,
                radius=1f,timer=7f,kind="root",witnessOwned=true};
            var unrelated=new Hazard{position=g.Position,damage=3,
                radius=.8f,timer=7f,kind="environment"};
            g.hazards.Add(hazard);g.hazards.Add(unrelated);
            // A faulted transition can change the active map before the
            // source encounter is reset. Nothing from that encounter may
            // be carried forward, and the unrelated actor must survive.
            g.save.room=0;
            var reset=typeof(GameCore).GetMethod("ResetWitnessDefense",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(reset!=null,"Use the original mission teardown path");
            reset.Invoke(g,null);
            Check(g.WitnessDefenseRoom==0&&
                !g.enemies.Contains(owned[0])&&!g.enemies.Contains(owned[1])&&
                g.enemies.Contains(other)&&!g.hazards.Contains(hazard)&&
                g.hazards.Contains(unrelated),
                "Teardown after room change removes only genuinely registered guards and owned warnings, not a foreign same-ID actor");
            Check(g.save.version==2&&!g.Has("witness_defeated_1"),
                "Canceled evidence combat never changes persistent signed history or save format");
        }

        public static void SignedWitnessFieldObjectsNeverSpeakAsRemoteCitizens(Campaign data)
        {
            string[] npcSpeakers={"","密林巡路员 · 莫芽",
                "遗迹渡口幸存者 · 阿菱","赤铁矿工 · 维恩"};
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.save.flags.Add("witness_started_"+region);
                g.save.flags.Add("witness_read_"+region);
                g.save.flags.Add("witness_sealed_"+region);
                g.save.flags.Add("witness_complete_"+region);
                g.Enter(region);g.enemies.Clear();
                WorldObject resident=Object(g,"witnessnpc"+region);
                WorldObject first=Object(g,"witnesssite1"+region);
                WorldObject archive=Object(g,"witnesssite2"+region);
                g.save.x=resident.x;g.save.y=resident.y;
                g.Talk(resident);
                Check(g.conversation!=null&&
                    g.conversation.speaker==npcSpeakers[region],
                    "Original completed living NPC retains genuine dialogue instead of an anonymous object speaker");
                g.Close();
                foreach(WorldObject site in new[]{first,archive})
                {
                    g.save.x=site.x;g.save.y=site.y;
                    g.Talk(site);
                    Check(g.conversation!=null&&
                        g.conversation.speaker==site.name&&
                        g.conversation.en==site.en&&
                        g.conversation.speaker!=npcSpeakers[region]&&
                        g.conversation.choices.Length==1&&
                        g.conversation.choices[0].action=="close",
                        "Reading a completed original scene keeps that archive's actual speaker, not a remote civilian NPC portrait");
                    Check(site==first?
                        g.conversation.textEn.Contains("Moya")||
                        g.conversation.textEn.Contains("Arin")||
                        g.conversation.textEn.Contains("Venn"):
                        g.conversation.textEn.Contains("archive")||
                        g.conversation.textEn.Contains("preserved"),
                        "Site-specific archive prose truthfully describes this original field evidence and its continuing civic effect");
                    g.Close();
                }
                Check(g.Has("witness_complete_"+region)&&
                    g.save.version==2,
                    "Revisiting physical witness sites cannot reset real signed progress or add save fields");
            }
        }

        public static void ActiveWitnessSecurityGuidesActualTimerAndOwnedGuardCount(Campaign data)
        {
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                WorldObject npc=Object(g,"witnessnpc"+region);
                WorldObject first=Object(g,"witnesssite1"+region);
                WorldObject site=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=first.x;g.save.y=first.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,first.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=site.x;g.save.y=site.y;
                Action(g,site.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region,
                    "Real second witness site starts the authentic chapter security timer");
                var quest=g.GetQuest("campaign_witness");
                Check(quest.room==region&&
                    (quest.target-new Point(site.x,site.y)).Length<.01f&&
                    quest.next.Contains("守卫 2/2")&&quest.next.Contains("65秒"),
                    "In-progress witness mission sends hero to its real site with live 65s bilingual guidance");
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Check(hit!=null,"Actual lethal attack path is testable");
                Enemy firstGuard=g.enemies.Find(g.IsWitnessEnemy);
                hit.Invoke(g,new object[]{firstGuard,99999f,false,null,null});
                Check(g.GetQuest("campaign_witness").next.Contains("守卫 1/2"),
                    "Only actual owned guard death updates the real-time chapter objective");
                g.save.english=true;
                Check(g.GetQuest("campaign_witness").next.Contains("GUARDS 1/2")&&
                    g.GetQuest("campaign_witness").next.Contains("WITNESS DEFENSE"),
                    "English quest overlay agrees with the actual original combat roster");
                var secondGuard=g.enemies.Find(g.IsWitnessEnemy);
                hit.Invoke(g,new object[]{secondGuard,99999f,false,null,null});
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    !g.GetQuest("campaign_witness").next.Contains("WITNESS DEFENSE"),
                    "After true combat settlement the original quest guidance returns to physical evidence sealing");
            }
        }

        public static void ThreeRegionWitnessDefenseRejectsForeignMatchingIds(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Witness battles use real lethal combat contacts");
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;g.invulnerable=200;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.Enter(region);g.enemies.Clear();
                var npc=Object(g,"witnessnpc"+region);
                var source=Object(g,"witnesssite1"+region);
                var archive=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=source.x;g.save.y=source.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,source.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=archive.x;g.save.y=archive.y;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Each original real regional testimony places exactly two true guardian instances");
                var genuine=g.enemies.FindAll(g.IsWitnessEnemy);
                Enemy forged=new Enemy(genuine[0].id,genuine[0].kind,
                    genuine[0].x,genuine[0].y,30){stun=150f,timer=150f};
                g.enemies.Add(forged);
                Check(!g.IsWitnessEnemy(forged)&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "An attacker with an equal 89972-series ID cannot acquire signed witness ownership");
                int coins=g.save.coins,shards=g.save.shards;
                hit.Invoke(g,new object[]{forged,99999f,false,null,null});
                // Foreign live enemies keep their ordinary 6-coin loot.
                // Only mission-owned enemies are excluded from normal
                // combat economy and may record witness guard deaths.
                int afterOrdinaryKill=g.save.coins;
                Check(afterOrdinaryKill==coins+6,
                    "The forged-ID world enemy is an ordinary foe and still pays its normal unrelated combat reward");
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2&&
                    !g.Has("witness_defeated_"+region),
                    "A counterfeit enemy's genuine death never contributes to an original guard seal");
                // A replacement with a matching ID cannot conceal a real
                // defender removed without a legitimate damage callback.
                g.enemies.Remove(genuine[0]);
                Enemy replacement=new Enemy(genuine[0].id,genuine[0].kind,
                    genuine[0].x,genuine[0].y,40){stun=150f};
                g.enemies.Add(replacement);
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    !g.Has("witness_defeated_"+region)&&
                    !g.Has("witness_sealed_"+region)&&
                    g.enemies.Contains(replacement)&&
                    !g.enemies.Contains(genuine[1])&&
                    g.save.coins==afterOrdinaryKill&&g.save.shards==shards,
                    "Real guardian disappearance resets only its owned fight, without deleting an unrelated same-ID enemy or minting evidence");
                g.enemies.Clear();
                g.save.x=archive.x;g.save.y=archive.y;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Free retry after source-authenticated cancellation restores the actual two-actor fight");
                foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                    hit.Invoke(g,new object[]{e,99999f,false,null,null});
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    g.Has("witness_defeated_"+region)&&
                    !g.Has("witness_complete_"+region),
                    "Exactly two real damage-confirmed kills permit authentic evidence sealing, not remote reward");
                Action(g,archive.kind,"witness_seal_"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_finish_"+region);
                Check(g.Has("witness_complete_"+region)&&
                    g.save.coins==afterOrdinaryKill+region*10+15&&
                    g.save.shards==shards+1&&g.save.version==2,
                    "Original living regional witness still signs one irreversible story reward under unchanged v2 save");
            }
        }

        public static void UnconfirmedWitnessGuardDisappearanceCannotForgeLivingTestimony(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 0;
            g.save.flags.Add("scout_saved");
            g.Enter(1); g.enemies.Clear(); g.invulnerable = 100;
            var witness = Object(g, "witnessnpc1");
            var source = Object(g, "witnesssite11");
            var archive = Object(g, "witnesssite21");
            g.save.x = witness.x; g.save.y = witness.y;
            Action(g, witness.kind, "witness_begin_1");
            g.save.x = source.x; g.save.y = source.y;
            g.save.inventory[2] = Math.Max(1, g.save.inventory[2]);
            Action(g, source.kind, "witness_answer_1_1");
            g.save.x = archive.x; g.save.y = archive.y;
            Action(g, archive.kind, "witness_defend_1");
            Check(g.WitnessDefenseRoom == 1 &&
                g.enemies.FindAll(g.IsWitnessEnemy).Count == 2,
                "A signed testimony requires the original live two-guard security encounter");
            int coins = g.save.coins, shards = g.save.shards;
            // A rogue cleanup is not a player kill. The pending record
            // must not be marked secured merely because its guards vanished.
            g.enemies.RemoveAll(g.IsWitnessEnemy);
            g.Tick(.05f, new Point());
            Check(g.WitnessDefenseRoom == 0 &&
                !g.Has("witness_defeated_1") &&
                !g.Has("witness_sealed_1") &&
                !g.Has("witness_complete_1") &&
                g.save.coins == coins && g.save.shards == shards,
                "An unconfirmed enemy-list disappearance resets the actual witness defense without forging evidence or rewards");
            g.Talk(archive);
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "witness_defend_1") &&
                !Array.Exists(g.conversation.choices,
                    c => c.action == "witness_seal_1"),
                "The existing original field marker offers a free retry instead of a counterfeit sealed record");
            g.Close();
            Action(g, archive.kind, "witness_defend_1");
            Check(g.WitnessDefenseRoom == 1 &&
                g.enemies.FindAll(g.IsWitnessEnemy).Count == 2,
                "A fresh legitimate battle remains playable with both authored security enemies");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Witness defeat uses the original live combat damage path");
            foreach (Enemy guard in g.enemies.FindAll(g.IsWitnessEnemy))
                hit.Invoke(g, new object[] { guard, 99999f, false, null, null });
            g.Tick(.05f, new Point());
            Check(g.WitnessDefenseRoom == 0 && g.Has("witness_defeated_1") &&
                g.save.coins == coins && g.save.shards == shards,
                "Both authentic lethal contacts earn the security unlock without paying before signature");
            Action(g, archive.kind, "witness_seal_1");
            g.save.x = witness.x; g.save.y = witness.y;
            Action(g, witness.kind, "witness_finish_1");
            Check(g.Has("witness_sealed_1") &&
                g.Has("witness_complete_1") &&
                g.save.coins == coins + 25,
                "Verified defense still yields the original genuine signed first-chapter testimony and once-only reward");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("witness_complete_1") &&
                loaded.save.coins == g.save.coins,
                "Signed and verified civilian testimony survives unchanged v2 save reload");
        }

    }
}
