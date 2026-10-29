using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject UnsentPost(GameCore g,int room,bool east)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id=="waytrail"+room+(east?"e":"s"))return o;
            throw new Exception("Missing real courier waytrail source");
        }
        private static bool UnsentChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(i<0)return false;
            g.Choose(i);return true;
        }
        private static GameCore UnsentReady(Campaign data)
        {
            var g=Fresh(data,0);g.save.stage=3;
            g.save.flags.Add("floodarchive_public");
            g.save.flags.Add("mineconvoy_complete");
            g.save.inventory[2]=15;g.save.energy=100f;
            g.enemies.Clear();g.invulnerable=900f;
            Check(g.UnsentWarningUnlocked&&!g.UnsentWarningStarted,
                "Ferry and mine-cart history together unlock the new cross-region story");
            return g;
        }
        private static void UnsentOpenAtRealGuild(GameCore g)
        {
            WorldObject guild=Object(g,"guild");
            Check(PlaceWithinTalkReach(g,guild),
                "Original guild receptionist has reachable walkable footing");
            g.Talk(guild);
            Check(UnsentChoice(g,"unsent_start")&&g.UnsentWarningStarted&&
                g.save.trackedQuest=="unsent_warning"&&
                g.GetQuest("unsent_warning").steps.Count==4,
                "Source-bound Emberfall guild choice starts a new tracked four-step chapter");
        }
        // Move using GameCore's REAL terrain steering and simulation frames.
        // No teleport can credit the minimum on-foot courier distance.
        private static void UnsentWalkToEasternPost(GameCore g,WorldObject east)
        {
            Point target=new Point(east.x,east.y);
            for(int frame=0;frame<1880&&g.UnsentWarningRunActive&&
                (g.Position-target).Length>1.10f;frame++)
            {
                Point direction=g.DirectionTo(g.Position,target);
                if(direction.Length<.01f)break;
                g.Tick(.05f,direction);
            }
            Check(g.UnsentWarningRunActive&&
                (g.Position-target).Length<1.75f&&
                g.UnsentWarningTravel>=18f&&
                g.UnsentWarningSeconds>0f,
                "Genuine terrain-steered movement physically connects west and east waymarks within the live deadline");
        }
        public static void UnsentWarningRequiresTwoPriorTrueStoriesAndAuthenticGuild(Campaign data)
        {
            var locked=Fresh(data);locked.save.stage=3;
            WorldObject guild=Object(locked,"guild");
            Check(!locked.UnsentWarningUnlocked&&
                locked.GetQuest("unsent_warning").locked,
                "Old v2 saves have no fabricated cross-region warning chapter before both real stories");
            Check(PlaceWithinTalkReach(locked,guild),
                "Original guild still supports old quest dialogue");
            locked.Talk(guild);
            Check(Array.FindIndex(locked.conversation.choices,
                c=>c.action=="unsent_start")<0,
                "Guild does not offer unearned courier chapter or replace older mission choices");
            var g=UnsentReady(data);
            UnsentOpenAtRealGuild(g);
            Check(g.UnsentWarningStepCount==1&&!g.UnsentRiverDelivered&&
                g.UnsentWarningTargetRoom==2&&
                g.GetQuest("unsent_warning").room==2,
                "The journal actually routes new chapter to Drowned Ruins after guild acceptance");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.UnsentWarningStarted&&
                !restored.UnsentWarningRunActive&&
                !restored.GetQuest("unsent_warning").complete,
                "Legacy SaveData v2 persists the story state but never phantom live couriers");
        }
        public static void UnsentWarningTwoLivingRunsDeliverOnceAndChooseRealCivicServices(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            {
                var g=UnsentReady(data);
                UnsentOpenAtRealGuild(g);
                int beginHerbs=g.save.inventory[2];
                for(int room=2;room<=3;room++)
                {
                    g.Enter(room);g.enemies.Clear();g.invulnerable=900f;
                    WorldObject west=UnsentPost(g,room,false),
                        east=UnsentPost(g,room,true);
                    Check(PlaceWithinTalkReach(g,west),
                        "Authorial west waytrail is truly reachable for a live warning run");
                    g.Talk(west);
                    Check(UnsentChoice(g,"unsent_run_"+room)&&
                        g.UnsentWarningRunActive&&
                        g.UnsentWarningSeconds==105f&&
                        g.UnsentWarningIntegrity==100f&&
                        g.save.inventory[2]==beginHerbs-(room-1),
                        "Each actual sealed warning spends its own herb and starts two living enemy encounters");
                    Check(g.enemies.Count>=2,
                        "True spawned raiders rather than a dialogue-only label threaten each warning");
                    g.invulnerable=900f;
                    // We preserve the ACTUAL enemies but hold their AI for
                    // deterministic route testing; combat damage is tested
                    // independently below through the real hurt handler.
                    foreach(Enemy foe in g.enemies)
                    {foe.stun=150f;foe.timer=150f;}
                    Check(PlaceWithinTalkReach(g,east)==true,
                        "East waytrail itself is reachable on original physical ground");
                    g.Talk(east);
                    // An unopened or rejected early choice must never
                    // claim a route: the run still needs witnessed walking.
                    UnsentChoice(g,"unsent_deliver_"+room);
                    Check(!g.UnsentRiverDelivered&&!g.UnsentMineDelivered&&
                        g.UnsentWarningRunActive&&g.UnsentWarningTravel<18f,
                        "Early arrival cannot bank a warning without actual walking");
                    g.Close();
                    Check(PlaceWithinTalkReach(g,west),"Can return to the real western start");
                    UnsentWalkToEasternPost(g,east);
                    g.Talk(east);
                    Check(UnsentChoice(g,"unsent_deliver_"+room)&&
                        !g.UnsentWarningRunActive&&
                        (room==2?g.UnsentRiverDelivered:g.UnsentMineDelivered),
                        "Original east-bank object banks only a real on-foot timed courier handoff");
                    Check(g.GetQuest("unsent_warning").steps[room-1].done,
                        "Existing quest journal tracks the specific completed region");
                }
                g.Enter(0);g.enemies.Clear();
                WorldObject guild=Object(g,"guild");
                Check(PlaceWithinTalkReach(g,guild),
                    "End-of-chapter testimony requires the original guild desk");
                g.Talk(guild);
                int coins=g.save.coins,shards=g.save.shards;
                Check(UnsentChoice(g,outcome==0?"unsent_publish":"unsent_protect")&&
                    g.UnsentWarningComplete&&
                    g.GetQuest("unsent_warning").complete&&
                    g.save.coins==coins+70&&g.save.shards==shards+2,
                    "Two genuine cross-region courier runs unlock exactly one final civic outcome");
                Check(outcome==0?g.UnsentWarningPublic&&!g.UnsentWarningHidden:
                    g.UnsentWarningHidden&&!g.UnsentWarningPublic,
                    "Published names and confidential witnesses remain exclusive choices");
                g.Talk(guild);
                Check(!UnsentChoice(g,outcome==0?"unsent_protect":"unsent_publish")&&
                    g.save.coins==coins+70,
                    "Returning to guild cannot double-bank or change the settled moral choice");
                g.Close();g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.UnsentWarningComplete&&
                    restored.GetQuest("unsent_warning").complete&&
                    (outcome==0?restored.UnsentWarningPublic:restored.UnsentWarningHidden),
                    "Two region receipts and exclusive practical resolution survive unchanged SaveData v2");
                for(int room=2;room<=3;room++)
                {
                    g.Enter(room);g.enemies.Clear();
                    WorldObject post=UnsentPost(g,room,false);
                    Check(PlaceWithinTalkReach(g,post),
                        "Earned civic services are physically tied to each existing road marker");
                    var copy=CloneSave(g.save);
                    copy.flags.Remove(outcome==0?
                        "unsent_warning_public":"unsent_warning_hidden");
                    var control=new GameCore(data,copy);
                    control.Start();control.enemies.Clear();
                    control.save.x=g.save.x;control.save.y=g.save.y;
                    if(outcome==0)g.save.health=control.save.health=20f;
                    else g.save.energy=control.save.energy=15f;
                    for(int frame=0;frame<15;frame++)
                    {g.Tick(.05f,new Point());control.Tick(.05f,new Point());}
                    Check(outcome==0?g.save.health>control.save.health+.8f:
                        g.save.energy>control.save.energy+1.6f,
                        "Earned branch provides physical safe recovery in BOTH regions, not merely dialogue flags");
                }
            }
        }
        public static void UnsentWarningHonorsSourceTravelDeadlineDamageAndLegacyLoad(Campaign data)
        {
            var g=UnsentReady(data);UnsentOpenAtRealGuild(g);
            g.Enter(2);g.enemies.Clear();g.invulnerable=900f;
            WorldObject west=UnsentPost(g,2,false),east=UnsentPost(g,2,true);
            Check(PlaceWithinTalkReach(g,west),"Real west post is accessible");
            g.Talk(west);
            int herb=g.save.inventory[2];
            g.save.x=west.x+14f;
            UnsentChoice(g,"unsent_run_2");
            Check(g.save.inventory[2]==herb&&!g.UnsentWarningRunActive,
                "Abandoned remote dialogue cannot start a run or spend supplies");
            Check(PlaceWithinTalkReach(g,west),"Return to original source");
            g.Talk(west);
            Check(UnsentChoice(g,"unsent_run_2")&&g.UnsentWarningRunActive,
                "Second genuine near-side interaction starts the active run");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.UnsentWarningStarted&&
                !loaded.UnsentWarningRunActive&&
                !loaded.UnsentRiverDelivered,
                "Save/reload keeps the chapter but cannot restore stale transient enemy instances");
            g.invulnerable=0;g.save.health=g.save.maxHealth;
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"Actual GameCore combat hit handler must remain available");
            hurt.Invoke(g,new object[]{20f,"shot",null});
            Check(g.UnsentWarningRunActive&&g.UnsentWarningIntegrity<100f,
                "One genuine health-damaging shot also damages the physical packet");
            g.invulnerable=0;hurt.Invoke(g,new object[]{99f,"shot",null});
            g.invulnerable=0;hurt.Invoke(g,new object[]{99f,"shot",null});
            Check(!g.UnsentWarningRunActive&&!g.UnsentRiverDelivered,
                "Destroyed packet fails and resets without counterfeit prior region credit");
            g.save.health=g.save.maxHealth;g.invulnerable=900f;
            Check(PlaceWithinTalkReach(g,west),"After failure real west source remains");
            g.Talk(west);
            Check(UnsentChoice(g,"unsent_run_2")&&g.UnsentWarningRunActive,
                "A failed live warning can be restarted with fresh paid supplies");
            for(int i=0;i<2150&&g.UnsentWarningRunActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.UnsentWarningRunActive&&!g.UnsentRiverDelivered,
                "True world time can expire, with no free last-moment success");
            g.Talk(east);
            Check(!UnsentChoice(g,"unsent_deliver_2"),
                "No remote delivery remains after an expired run");
        }
    }
}
