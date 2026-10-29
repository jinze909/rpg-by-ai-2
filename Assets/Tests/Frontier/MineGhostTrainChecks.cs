using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void GhostChoice(GameCore g,string action)
        {
            Check(g.conversation!=null,
                "The Silent Seventeenth Train must use actual spoken field dialogue");
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            Check(i>=0,"Original real-source conversation missing "+action);
            g.Choose(i);
        }
        private static WorldObject GhostSite(GameCore g,string id)
        {
            WorldObject site=Array.Find(g.Map.objects,o=>o.id=="mine_ghost_"+id);
            Check(site!=null&&g.CanStand(new Point(site.x,site.y))&&
                site.name.Length>=5&&site.en.Length>=12,
                "Each narrative site has a distinct original accessible mine landmark");
            return site;
        }
        private static GameCore PreparedGhostStory(Campaign data)
        {
            GameCore g=new GameCore(data);g.Close();g.save.room=3;g.mode="world";
            g.enemies.Clear();g.save.inventory[2]=12;g.save.inventory[4]=12;
            g.save.flags.Add("biome_field_done_3");
            g.save.flags.Add("regional_machine_done_3");
            for(int i=0;i<3;i++)
                g.save.flags.Add("regional_machine_stage_3_"+i);
            return g;
        }
        private static void GhostWalk(GameCore g,WorldObject o)
        {
            g.save.x=o.x;g.save.y=o.y;
            g.Talk(o);
            Check(g.conversation!=null&&g.conversation.speaker==o.name,
                "Every narrative stage requires the correct original physical speaker");
        }
        private static void SecureTrueGhostSwitch(GameCore g,WorldObject switcher)
        {
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ambush");
            Check(g.GhostTrainBattleActive&&g.GhostTrainEnemiesLeft==3&&
                g.GhostTrainSeconds==180f&&
                (g.GhostTrainTarget-new Point(switcher.x,switcher.y)).Length<.01f,
                "New chapter launches an actual timed three-soldier rail blockade in its real locale");
            var wave=new List<Enemy>(g.enemies);
            Check(wave.Count==3,"The authored ambush owns exactly three independent living actors");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Only real combat lethal callbacks may clear the switchyard");
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_switch_secure");
            Check(g.GhostTrainChapter==2,
                "A player at the original lever cannot bypass any remaining live soldiers");
            g.Close();
            foreach(Enemy soldier in wave)
            {
                Check((soldier.Position-new Point(switcher.x,switcher.y)).Length<12,
                    "Three real rail guards spawn beside the original mined switch, not a fake marker");
                hit.Invoke(g,new object[]{soldier,10000f,false,null,null});
            }
            g.invulnerable=1000f;
            for(int k=0;k<5;k++)g.Tick(.05f,new Point());
            Check(g.GhostTrainEnemiesLeft==0,"Actual combat defeats retire the three real rail guards");
            int oldParts=g.save.inventory[4];
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_switch_secure");
            Check(!g.GhostTrainBattleActive&&g.GhostTrainChapter==3&&
                g.save.inventory[4]==oldParts-2,
                "Winning real battle and paying two gears opens only the true emergency railway");
        }
        public static void SilentSeventeenthTrainHasFourTrueWalkedSitesAndTwoPermanentStoryOutcomes(Campaign data)
        {
            for(int choice=0;choice<2;choice++)
            {
                GameCore g=PreparedGhostStory(data);
                WorldObject dispatch=GhostSite(g,"dispatch");
                WorldObject roster=GhostSite(g,"manifest");
                WorldObject switcher=GhostSite(g,"switch");
                WorldObject clinic=GhostSite(g,"medbay");
                WorldObject[] sites={dispatch,roster,switcher,clinic};
                for(int i=0;i<4;i++)for(int j=i+1;j<4;j++)
                    Check((new Point(sites[i].x-sites[j].x,
                        sites[i].y-sites[j].y)).Length>25f,
                        "Four chapter sites must be genuinely far-flung non-repeating map expeditions");
                Check(g.GhostTrainChapter==0&&!g.GhostTrainComplete,
                    "Old slots cannot silently pre-complete an optional chapter");
                GhostWalk(g,roster);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="ghost_train_manifest")<0,
                    "Finding a remote document before the actual commission cannot grant free signatures");
                GhostWalk(g,dispatch);GhostChoice(g,"ghost_train_start");
                Check(g.GhostTrainChapter==1&&
                    (g.GhostTrainTarget-new Point(roster.x,roster.y)).Length<.01f,
                    "Only original dispatch authority starts and aims the physical archive route");
                g.save.energy=100;
                GhostWalk(g,roster);GhostChoice(g,"ghost_train_false_manifest");
                Check(g.GhostTrainChapter==1&&g.save.energy==90,
                    "Authentic forged manifest wastes ten stamina without yielding plot flags");
                GhostWalk(g,roster);GhostChoice(g,"ghost_train_manifest");
                Check(g.GhostTrainChapter==2&&
                    (g.GhostTrainTarget-new Point(switcher.x,switcher.y)).Length<.01f,
                    "True seventeen-worker roster updates the actual destination");
                SecureTrueGhostSwitch(g,switcher);
                int herbs=g.save.inventory[2];
                GhostWalk(g,clinic);GhostChoice(g,"ghost_train_medbay");
                Check(g.GhostTrainChapter==4&&
                    g.save.inventory[2]==herbs-2&&
                    (g.GhostTrainTarget-new Point(dispatch.x,dispatch.y)).Length<.01f,
                    "Two real herbs at the remote physical infirmary authenticate the survivors");
                int coins=g.save.coins,shards=g.save.shards;
                GhostWalk(g,dispatch);
                GhostChoice(g,choice==0?"ghost_train_public":"ghost_train_freight");
                Check(g.GhostTrainChapter==5&&g.GhostTrainComplete&&
                    g.GhostTrainPublic==(choice==0)&&
                    g.GhostTrainFreight==(choice==1)&&
                    g.save.coins==coins+(choice==0?85:110)&&
                    g.save.shards==shards+(choice==0?3:2),
                    "Player's final public-justice versus practical passenger-rail choice has distinct real rewards");
                GhostWalk(g,dispatch);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="ghost_train_public"||
                    c.action=="ghost_train_freight")<0,
                    "The final public accountability decision cannot be reversed or farmed");
                g.PrepareSave();GameCore reloaded=new GameCore(data,g.save);
                Check(reloaded.save.version==2&&reloaded.GhostTrainComplete&&
                    reloaded.GhostTrainPublic==(choice==0)&&
                    reloaded.GhostTrainFreight==(choice==1)&&
                    !reloaded.GhostTrainBattleActive,
                    "Both original narrative endings persist without altering old v2 save structure");
            }
        }
        public static void FourTrainStorySitesAreWalkableAlongOriginalMineMapWithoutRemovingRidgeWildlife(Campaign data)
        {
            GameCore g=PreparedGhostStory(data);
            MapData map=g.Map;
            int width=map.rows[0].Length,height=map.rows.Length;
            var seen=new bool[width*height];
            var queue=new Queue<int>();
            int sx=(int)Math.Floor(map.spawnX),sy=(int)Math.Floor(map.spawnY);
            Check(g.CanStand(new Point(sx+.5f,sy+.5f)),
                "The original mine entrance must remain walkable");
            seen[sy*width+sx]=true;queue.Enqueue(sy*width+sx);
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%width,y=current/width;
                int[] dx={-1,1,0,0},dy={0,0,-1,1};
                for(int k=0;k<4;k++)
                {
                    int nx=x+dx[k],ny=y+dy[k];
                    if(nx<2||ny<2||nx>=width-2||ny>=height-2)continue;
                    int n=ny*width+nx;
                    if(seen[n]||!g.CanStand(new Point(nx+.5f,ny+.5f)))
                        continue;
                    seen[n]=true;queue.Enqueue(n);
                }
            }
            var historical=new Point[]{
                new Point(101.5f,61.5f),
                new Point(89.5f,67.5f),
                new Point(76.5f,66.5f)
            };
            foreach(string name in new string[]{"dispatch","manifest","switch","medbay"})
            {
                WorldObject site=GhostSite(g,name);
                int x=(int)site.x,y=(int)site.y;
                Check(seen[y*width+x],
                    "Each distant bespoke narrative landmark has a real contiguous walkable route from the original mine entrance: "+name);
                foreach(Point foot in historical)
                    Check((new Point(site.x,site.y)-foot).Length>=8f,
                        "The new chapter cannot suppress a preexisting dangerous-region wildlife patrol anchor");
            }
        }
        public static void GhostTrainHasRealJournalEntryCrossRoomNavigationAndSafeCombatResume(Campaign data)
        {
            Check(Array.IndexOf(GameCore.QuestIds,"ghost_train")>=0,
                "Seventeenth Train is selectable in the shipped journal, not only a floating HUD notice");
            GameCore g=PreparedGhostStory(data);
            WorldObject dispatch=GhostSite(g,"dispatch");
            WorldObject roster=GhostSite(g,"manifest");
            WorldObject switcher=GhostSite(g,"switch");
            QuestGuide journal=g.GetQuest("ghost_train");
            Check(!journal.locked&&!journal.complete&&journal.steps.Count==5&&
                (journal.target-new Point(dispatch.x,dispatch.y)).Length<.01f,
                "Restored original mine opens a complete journal route at real dispatch");
            g.TrackQuest("ghost_train");
            Check(g.save.trackedQuest=="ghost_train"&&
                g.NavigationTarget().HasValue&&
                (g.NavigationTarget().Value-journal.target).Length<.01f,
                "Tracking the new quest drives the true in-world compass");
            g.save.room=0;
            Check((g.GetQuest("ghost_train").target-new Point(dispatch.x,dispatch.y)).Length<.01f,
                "An off-region journal must retain original mine coordinates instead of showing zero");
            g.save.room=3;
            GhostWalk(g,dispatch);GhostChoice(g,"ghost_train_start");
            journal=g.GetQuest("ghost_train");
            Check(journal.steps[0].done&&!journal.steps[1].done&&
                (journal.target-new Point(roster.x,roster.y)).Length<.01f,
                "Accepting the real commission advances journal steps and objective");
            GhostWalk(g,roster);GhostChoice(g,"ghost_train_manifest");
            journal=g.GetQuest("ghost_train");
            Check(journal.steps[1].done&&!journal.steps[2].done&&
                (journal.target-new Point(switcher.x,switcher.y)).Length<.01f,
                "Recovering actual signatures routes the journal to the live switchyard");
            g.TrackQuest("main");
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ambush");
            Check(g.GhostTrainBattleActive&&
                g.NavigationTarget().HasValue&&
                (g.NavigationTarget().Value-new Point(switcher.x,switcher.y)).Length<.01f&&
                g.save.trackedQuest=="main",
                "Timed combat temporarily owns the compass without rewriting an older pinned quest");
            g.PrepareSave();
            GameCore restored=new GameCore(data,g.save);
            QuestGuide resumed=restored.GetQuest("ghost_train");
            Check(!restored.GhostTrainBattleActive&&
                restored.GhostTrainChapter==2&&!resumed.complete&&
                resumed.steps[1].done&&!resumed.steps[2].done&&
                (resumed.target-new Point(switcher.x,switcher.y)).Length<.01f&&
                restored.save.version==2&&restored.save.trackedQuest=="main",
                "Reloading an unfinished transient fight resumes its durable real-site quest without storing phantom enemies or changing save v2");
        }

        public static void LiveInnTravelerAcknowledgesTrainEvidenceAndRespectsBothSurvivorOutcomes(Campaign data)
        {
            GameCore g=PreparedGhostStory(data);
            // The actual Hearth Inn is region 9, not the town exterior 0.
            // Use the original inn map and its physical guest as other core
            // dialogue regressions do; do not fabricate an off-map witness.
            g.save.room=9;
            WorldObject traveler=Array.Find(g.Map.objects,o=>o.kind=="innGuest");
            Check(traveler!=null,"Existing Hearth Inn traveler must remain on the real map");
            ReadInnNews(g,traveler);
            Check(!g.conversation.text.Contains("十七份签字")&&
                !g.conversation.textEn.Contains("Seventeen signatures"),
                "Unstarted chapter cannot leak a false historical confession into the existing inn");
            g.Close();g.save.flags.Add("ghost_train_started");
            ReadInnNews(g,traveler);
            Check(g.conversation.speaker==traveler.name&&
                g.conversation.text.Contains("十七份签字")&&
                g.conversation.textEn.Contains("Seventeen signatures"),
                "Only commissioned investigation earns a rumor from the original spoken traveler");
            g.Close();g.save.flags.Add("ghost_train_manifest");
            g.save.flags.Add("ghost_train_switch");
            g.save.flags.Add("ghost_train_medbay");
            ReadInnNews(g,traveler);
            Check(g.conversation.text.Contains("矿医站")&&
                g.conversation.textEn.Contains("infirmary")&&
                !g.conversation.text.Contains("矿场补给站多了药"),
                "Actual sealed infirmary testimony changes spoken news without announcing a premature ending");
            g.Close();g.save.flags.Add("ghost_train_public");
            g.save.flags.Add("ghost_train_complete");
            ReadInnNews(g,traveler);
            Check(g.conversation.speaker==traveler.name&&
                g.conversation.text.Contains("矿场补给站多了药")&&
                g.conversation.textEn.Contains("mine shelters have more medicine")&&
                !g.conversation.text.Contains("修好的载人侧线"),
                "Publication earns an original epilogue about the real improved healing, not a swapped ending");
            g.Close();g.save.flags.Remove("ghost_train_public");
            g.save.flags.Add("ghost_train_freight");
            ReadInnNews(g,traveler);
            Check(g.conversation.text.Contains("修好的载人侧线")&&
                g.conversation.textEn.Contains("repaired passenger siding")&&
                !g.conversation.text.Contains("矿场补给站多了药"),
                "Passenger rail outcome has different natural NPC testimony instead of public-healing claims");
        }

        public static void FourPostTrainPlacesTellDistinctSourceAuthenticBilingualEndings(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            {
                GameCore g=PreparedGhostStory(data);
                g.save.flags.Add("ghost_train_complete");
                g.save.flags.Add(outcome==0?"ghost_train_public":"ghost_train_freight");
                var texts=new HashSet<string>();
                var english=new HashSet<string>();
                int coins=g.save.coins,flags=g.save.flags.Count;
                foreach(string name in new string[]{"dispatch","manifest","switch","medbay"})
                {
                    WorldObject source=GhostSite(g,name);
                    GhostWalk(g,source);
                    Check(g.conversation.speaker==source.name&&
                        g.conversation.en==source.en,
                        "Aftermath must retain the correct original on-site speaker");
                    Check(texts.Add(g.conversation.text)&&
                        english.Add(g.conversation.textEn)&&
                        g.conversation.text.Length>45&&
                        g.conversation.textEn.Length>65,
                        "Every real mine field record must have its own substantial original bilingual aftermath");
                    int rides=0;
                    foreach(Choice choice in g.conversation.choices)
                        if(choice.action.StartsWith("ghost_train_ride_",
                            StringComparison.Ordinal))rides++;
                    Check(rides==(outcome==1&&name!="manifest"?2:0),
                        "Only the three physical restored railway platforms should offer two real carriages");
                    g.Close();
                }
                Check(texts.Count==4&&english.Count==4&&
                    g.save.coins==coins&&g.save.flags.Count==flags,
                    "Re-reading all four genuine epilogues changes no durable reward or save state");
            }
        }

        public static void SilentTrainDecisionsChangeRealLocalSpeedOrExistingCivilianShelterHealing(Campaign data)
        {
            GameCore baseline=PreparedGhostStory(data);
            GameCore publicMine=PreparedGhostStory(data);
            GameCore railway=PreparedGhostStory(data);
            publicMine.save.flags.Add("ghost_train_public");
            publicMine.save.flags.Add("ghost_train_complete");
            railway.save.flags.Add("ghost_train_freight");
            railway.save.flags.Add("ghost_train_complete");
            Check(Math.Abs((railway.Speed-baseline.Speed)-.35f)<.0001f&&
                Math.Abs(publicMine.Speed-baseline.Speed)<.0001f,
                "Original player choice for passenger rail has a tangible mine movement advantage, not a narrative-only promise");
            WorldObject original=Array.Find(baseline.Map.objects,
                o=>o.id=="regional_machine_3_0");
            var shelter=typeof(GameCore).GetMethod("TickRegionalMachineShelters",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(shelter!=null,"Real existing field shelter runs after the original chapter outcome");
            foreach(GameCore g in new GameCore[]{baseline,publicMine,railway})
            {
                g.mode="world";g.enemies.Clear();
                g.save.x=original.x;g.save.y=original.y;
                g.save.health=30;g.save.energy=20;
                shelter.Invoke(g,new object[]{1f});
            }
            Check(publicMine.save.health>baseline.save.health+.5f&&
                Math.Abs(railway.save.health-baseline.save.health)<.01f&&
                Math.Abs(publicMine.save.energy-baseline.save.energy)<.01f,
                "Restoring public worker testimony improves precisely physical healing, not free energy; railway instead improves real movement speed");
        }

        public static void CompletedRailChoiceOperatesThreeRealSafePassengerStopsWithoutBypassingCombat(Campaign data)
        {
            GameCore g=PreparedGhostStory(data);
            WorldObject dispatch=GhostSite(g,"dispatch");
            WorldObject switcher=GhostSite(g,"switch");
            WorldObject infirmary=GhostSite(g,"medbay");
            g.save.coins=20;g.save.energy=90;
            // Old saves, unfinished chapters and publication-first endings
            // cannot falsely acquire the passenger rail service.
            GhostWalk(g,dispatch);
            Check(Array.FindIndex(g.conversation.choices,c=>
                c.action=="ghost_train_ride_medbay")<0,
                "Old incomplete v2 game cannot board a passenger service");
            g.Close();g.save.flags.Add("ghost_train_complete");
            g.save.flags.Add("ghost_train_public");
            GhostWalk(g,dispatch);
            Check(Array.FindIndex(g.conversation.choices,c=>
                c.action=="ghost_train_ride_medbay")<0,
                "Published-worker ending retains healing but does not quietly gain the alternate train");
            g.Close();g.save.flags.Remove("ghost_train_public");
            g.save.flags.Add("ghost_train_freight");
            GhostWalk(g,dispatch);
            Check(Array.FindIndex(g.conversation.choices,c=>
                c.action=="ghost_train_ride_medbay")>=0&&
                Array.FindIndex(g.conversation.choices,c=>
                c.action=="ghost_train_ride_switch")>=0,
                "Freight-first outcome unlocks two named actual destinations at real dispatch");
            int beforeCoins=g.save.coins;float beforeEnergy=g.save.energy;
            GhostChoice(g,"ghost_train_ride_medbay");
            Check(g.sound=="railDepart",
                "A successful verified passenger ride dispatches native train audio");
            Check(g.save.room==3&&
                (g.Position-new Point(infirmary.x,infirmary.y)).Length<1.8f&&
                g.save.coins==beforeCoins-2&&g.save.energy==beforeEnergy-10f,
                "Real local passenger carriage follows a safe original traversable road and charges only upon arrival");
            GhostWalk(g,infirmary);GhostChoice(g,"ghost_train_ride_switch");
            Check((g.Position-new Point(switcher.x,switcher.y)).Length<1.8f,
                "Passenger service actually links the distant north and south terminals");
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ride_dispatch");
            Check((g.Position-new Point(dispatch.x,dispatch.y)).Length<1.8f,
                "Rail transit is bidirectional and returns physically to dispatch");
            // Check position AFTER walking to the actual boarding object.
            // A scripted walk in this fixture must not count as a free ride.
            g.save.coins=1;
            GhostWalk(g,dispatch);
            Point stay=g.Position;int coins=g.save.coins;
            GhostChoice(g,"ghost_train_ride_switch");
            Check((g.Position-stay).Length<.01f&&g.save.coins==coins,
                "Cannot ride without real coins: no teleport and no hidden fee");
            g.save.coins=20;g.save.energy=9f;
            GhostWalk(g,dispatch);stay=g.Position;
            GhostChoice(g,"ghost_train_ride_medbay");
            Check((g.Position-stay).Length<.01f&&
                g.save.energy==9f&&g.save.coins==20,
                "Insufficient stamina likewise cannot move the rider or charge a ticket");
            // Combat safety is source-bound, including a live hostile actor.
            g.save.energy=90;
            g.enemies.Add(new Enemy(123456,0,dispatch.x+1.5f,
                dispatch.y,80f));
            GhostWalk(g,dispatch);stay=g.Position;
            GhostChoice(g,"ghost_train_ride_switch");
            Check((g.Position-stay).Length<.01f&&g.save.coins==20,
                "Nearby authentic combat prevents fast rail travel and charges nothing");
            g.enemies.Clear();
            // A closed actual arrival platform must never strand a rider
            // inside rock, charge a fare or vandalize shared map terrain.
            MapData mine=g.Map;
            int cx=(int)infirmary.x,cy=(int)infirmary.y;
            var backups=new Dictionary<int,string>();
            try
            {
                for(int y=cy-2;y<=cy+2;y++)
                {
                    backups[y]=mine.rows[y];
                    char[] cells=mine.rows[y].ToCharArray();
                    for(int x=cx-2;x<=cx+2;x++)cells[x]='#';
                    mine.rows[y]=new string(cells);
                }
                GhostWalk(g,dispatch);stay=g.Position;
                GhostChoice(g,"ghost_train_ride_medbay");
                Check((g.Position-stay).Length<.01f&&g.save.coins==20,
                    "A physically sealed mine terminal causes no route teleport or ticket charge");
            }
            finally
            {
                foreach(var pair in backups)mine.rows[pair.Key]=pair.Value;
            }
            g.PrepareSave();
            GameCore loaded=new GameCore(data,g.save);
            Check(loaded.save.version==2&&loaded.GhostTrainFreight&&
                loaded.GhostTrainComplete,
                "Passenger service survives unchanged v2 slots without serialized runtime objects");
        }

        public static void RealPassengerRailHasDistinctNativeReadyOriginalTrainAudio(Campaign data)
        {
            Check(Array.IndexOf(AudioSynthesis.Events,"railDepart")>=0,
                "The native scene audio registry owns its bespoke passenger-train event");
            float[] sound=AudioSynthesis.Event("railDepart");
            float[] repeated=AudioSynthesis.Event("railDepart");
            float[] travel=AudioSynthesis.Event("travel");
            Check(sound.Length>AudioSynthesis.Rate/2&&
                sound.Length!=travel.Length&&sound.Length==repeated.Length,
                "The carriage gets its own full steam-and-wheel event, not generic travel SFX");
            float peak=0f;
            for(int i=0;i<sound.Length;i++)
            {
                Check(!float.IsNaN(sound[i])&&!float.IsInfinity(sound[i])&&
                    Math.Abs(sound[i])<.95f&&sound[i]==repeated[i],
                    "Original passenger PCM must be deterministic, finite and safe for Unity AudioClip");
                peak=Math.Max(peak,Math.Abs(sound[i]));
            }
            Check(peak>.06f&&Math.Abs(sound[0])<.001f&&
                Math.Abs(sound[sound.Length-1])<.001f,
                "Steam whistle and wheel contacts must be audible and release cleanly at boundaries");
        }

        public static void SilentTrainCombatRejectsForgedActionsAndCleansTimeoutAndOwnedActors(Campaign data)
        {
            GameCore g=PreparedGhostStory(data);
            WorldObject dispatch=GhostSite(g,"dispatch");
            WorldObject roster=GhostSite(g,"manifest");
            WorldObject switcher=GhostSite(g,"switch");
            var direct=typeof(GameCore).GetMethod("ChooseGhostTrain",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(direct!=null,"Real chapter command exists");
            direct.Invoke(g,new object[]{"ghost_train_start"});
            Check(g.GhostTrainChapter==0,
                "Direct action without authenticated physically reached dispatch cannot forge stage one");
            GhostWalk(g,dispatch);GhostChoice(g,"ghost_train_start");
            GhostWalk(g,roster);GhostChoice(g,"ghost_train_manifest");
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ambush");
            Check(g.GhostTrainBattleActive&&g.GhostTrainEnemiesLeft==3,
                "A unique exact roster guards the physical train switch");
            int coins=g.save.coins,room=g.save.room;
            g.Enter(4);
            Check(g.save.room==room&&g.GhostTrainBattleActive,
                "Portal and fast travel cannot bypass an active timed story fight");
            Enemy removed=g.enemies[0];
            g.enemies.Remove(removed);
            for(int i=0;i<3;i++)g.Tick(.05f,new Point());
            Check(!g.GhostTrainBattleActive&&g.GhostTrainChapter==2&&
                g.save.coins==coins,
                "An enemy disappearing without a true lethal callback invalidates the record");
            g.enemies.Clear();
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ambush");
            Check(g.GhostTrainBattleActive,"Aborted wave may be started again on-site");
            Enemy culprit=g.enemies[0];
            var ownArrow=new Projectile{
                hostileShooter=culprit,position=culprit.Position,
                velocity=new Point(1,0),life=2f,damage=5f,visual="enemyArrow"};
            var ourShot=new Projectile{
                friendly=true,position=g.Position,
                velocity=new Point(1,0),life=2f,visual="friendly"};
            g.projectiles.Add(ownArrow);g.projectiles.Add(ourShot);
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_abort");
            Check(!g.GhostTrainBattleActive&&g.enemies.Count==0&&
                !g.projectiles.Contains(ownArrow)&&g.projectiles.Contains(ourShot),
                "Source withdrawal removes exactly mission-owned foes/projectiles and keeps unrelated shots");
            g.projectiles.Clear();
            GhostWalk(g,switcher);GhostChoice(g,"ghost_train_ambush");
            g.mode="inventory";float clock=g.GhostTrainSeconds;
            g.Tick(.05f,new Point());
            Check(g.GhostTrainSeconds==clock,
                "Inventory pause cannot expire a world-time-only emergency combat clock");
            g.mode="world";g.invulnerable=1000f;
            // Drive genuine world frames; no private clock edits.
            for(int i=0;i<3602&&g.GhostTrainBattleActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.GhostTrainBattleActive&&g.GhostTrainChapter==2&&
                !g.GhostTrainComplete&&g.save.coins==coins,
                "Real 180-second deadline retires a failed fight without creating fake rewards");
        }
    }
}
