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
        private static WorldObject PhysicalWarningRelay(GameCore game,int room)
        {
            foreach(WorldObject o in game.Map.objects)
                if(o.id=="unsent_relay_"+room&&o.kind=="unsentrelay")
                    return o;
            throw new Exception("Missing true map-side warning relay in "+room);
        }
        private static void WalkToWarningRelay(GameCore game,WorldObject post)
        {
            Point target=new Point(post.x,post.y);
            for(int frame=0;frame<2000&&game.UnsentWarningRunActive&&
                (game.Position-target).Length>1.25f;frame++)
            {
                Point step=game.DirectionTo(game.Position,target);
                if(step.Length<.01f)break;
                game.Tick(.05f,step);
            }
            Check(game.UnsentWarningRunActive&&
                (game.Position-target).Length<1.8f&&
                game.UnsentWarningTravel>=14f,
                "A physically steered courier crosses authored terrain into the real mid-route field station");
        }
        public static void UnsentRelayRequiresRealDamageAndForbidsRemoteAndRepeatedSupplies(Campaign data)
        {
            var g=UnsentReady(data);
            UnsentOpenAtRealGuild(g);
            g.Enter(2);g.enemies.Clear();g.invulnerable=900f;
            WorldObject west=UnsentPost(g,2,false),
                relay=PhysicalWarningRelay(g,2);
            Check(g.CanStand(new Point(relay.x,relay.y))&&
                !g.UnsentWarningRelayAvailable,
                "The original Drowned map has a truly walkable field relay with no fake pre-quest supplies");
            Check(PlaceWithinTalkReach(g,relay),"A traveler can inspect the original mid-route post");
            g.Talk(relay);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,c=>c.action=="unsent_relay_repair")<0,
                "Inactive warning courier cannot take the field repair by conversation");
            g.Close();
            Check(PlaceWithinTalkReach(g,west),"The start waytrail remains reachable");
            g.Talk(west);
            Check(UnsentChoice(g,"unsent_run_2")&&g.UnsentWarningRunActive,
                "Original west waytrail launches an actual dangerous parcel");
            foreach(Enemy foe in g.enemies)
            {foe.stun=150f;foe.timer=150f;}
            int herbs=g.save.inventory[2],wood=g.save.inventory[0];
            WalkToWarningRelay(g,relay);
            Check(PlaceWithinTalkReach(g,relay),
                "A real road crossing brings the courier within source-bound relay reach");
            g.Talk(relay);
            Check(UnsentChoice(g,"unsent_relay_repair")&&
                g.UnsentWarningRelayChoice==0&&
                g.save.inventory[2]==herbs,
                "Perfectly intact warning cannot burn resources to mint fake repair credit");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"The game's actual hero-damage path exists");
            g.save.health=g.save.maxHealth;g.invulnerable=0f;
            hurt.Invoke(g,new object[]{18f,"shot",null});
            Check(g.UnsentWarningRunActive&&g.UnsentWarningIntegrity<100f,
                "A genuine hit damages the courier seal before patching");
            g.invulnerable=900f;
            float integrity=g.UnsentWarningIntegrity,
                remaining=g.UnsentWarningSeconds,energy=g.save.energy;
            g.Close();g.Talk(relay);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="unsent_relay_repair");
            Check(option>=0,"Damaged warning exposes the physical oilcloth decision");
            g.save.x=relay.x+12f;g.Choose(option);
            Check(g.UnsentWarningRelayChoice==0&&
                g.save.inventory[2]==herbs&&
                g.UnsentWarningIntegrity==integrity,
                "A stale remote relay choice cannot patch a damaged parcel");
            Check(PlaceWithinTalkReach(g,relay),
                "Real terrain permits return after stale dialogue");
            g.Talk(relay);
            Check(UnsentChoice(g,"unsent_relay_repair")&&
                g.UnsentWarningRelayChoice==1&&
                g.UnsentWarningIntegrity==Math.Min(100f,integrity+45f)&&
                Math.Abs(g.UnsentWarningSeconds-(remaining-8f))<.001f&&
                g.save.inventory[2]==herbs-1&&g.save.inventory[0]==wood&&
                Math.Abs(g.save.energy-(energy-12f))<.001f,
                "Actual oilcloth repair trades exactly one herb, 12 energy and 8 deadline seconds for +45 seal");
            g.Talk(relay);
            Check(!UnsentChoice(g,"unsent_relay_smoke")&&
                g.UnsentWarningRelayChoice==1&&
                g.save.inventory[2]==herbs-1,
                "Patch and smoke are exclusive, with no repeated material spend");
            Check(!g.Has("unsent_warning_relay_2"),
                "Only an actual finished run, not an optional field choice, can bank a durable relief receipt");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.UnsentWarningStarted&&
                !loaded.UnsentWarningRunActive&&!loaded.Has("unsent_warning_relay_2"),
                "Pending relay choice and pursuer are transient across legacy SaveData v2 reload");
        }
        public static void UnsentLivePursuerAndSmokeRequireTraversedRealTerrain(Campaign data)
        {
            var g=UnsentReady(data);UnsentOpenAtRealGuild(g);
            g.Enter(3); // The next regional task is still gated by river proof.
            g.Enter(2);g.enemies.Clear();g.invulnerable=900f;
            WorldObject west=UnsentPost(g,2,false),
                relay=PhysicalWarningRelay(g,2),
                east=UnsentPost(g,2,true);
            Check(PlaceWithinTalkReach(g,relay),"Real relay is source reachable");
            Check(!g.UnsentWarningRelayPursuerActive&&
                !g.UnsentWarningRunActive,
                "Walking to an inactive relay does not invent ambushes");
            Check(PlaceWithinTalkReach(g,west),"Walk to actual west start");
            g.Talk(west);
            Check(UnsentChoice(g,"unsent_run_2")&&g.UnsentWarningRunActive,
                "Genuine warning starts before field approach");
            foreach(Enemy foe in g.enemies)
            {foe.stun=150f;foe.timer=150f;}
            int startingActors=g.enemies.Count;
            // Position spoofing alone never earns 14 tiles of ACTUAL travel.
            g.save.x=relay.x;g.save.y=relay.y;
            g.Tick(.05f,new Point());
            Check(!g.UnsentWarningRelayPursuerActive&&
                g.enemies.Count==startingActors&&
                g.UnsentWarningTravel<14f,
                "Teleporting near the relay cannot fake a genuine patrol event");
            Check(PlaceWithinTalkReach(g,west),
                "After fake relocation, safely return to the real west start");
            WalkToWarningRelay(g,relay);
            Check(g.UnsentWarningRelayPursuerActive&&
                g.enemies.Count==startingActors+1,
                "Only actual traversed distance triggers one real new living hunter beside the relay");
            // Keep the first two fighters disabled, but the new one has its
            // original authentic AI, HP and time until the player acts.
            Enemy hunter=g.enemies[g.enemies.Count-1];
            Check(hunter.hp>0f&&g.CanStand(hunter.Position)&&!hunter.boss,
                "This threat is a real killable normal enemy, not a HUD animation or Boss reward");
            Check(PlaceWithinTalkReach(g,relay),"The relay is physically reachable beside live enemies");
            g.Talk(relay);
            int wood=g.save.inventory[0],herb=g.save.inventory[2],
                kills=g.save.kills[1],coin=g.save.coins;
            float before=g.UnsentWarningSeconds,energy=g.save.energy;
            Check(UnsentChoice(g,"unsent_relay_smoke")&&
                g.UnsentWarningRelayChoice==2&&
                g.save.inventory[0]==wood-1&&g.save.inventory[2]==herb&&
                Math.Abs(g.save.energy-(energy-12f))<.001f&&
                Math.Abs(g.UnsentWarningSeconds-(before-5f))<.001f,
                "An actual relay smokes only visible, live raiders for a real five-second deadline penalty");
            Check(hunter.stun>=2.5f&&g.enemies.Contains(hunter)&&
                g.save.kills[1]==kills&&g.save.coins==coin,
                "Smoke interrupts genuine raiders but never fabricates kills, coins or a Boss victory");
            g.Close();g.Talk(relay);
            Check(!UnsentChoice(g,"unsent_relay_repair")&&
                g.save.inventory[0]==wood-1,
                "One tactical choice per run blocks fake second-tier repair");
            g.Close();
            // Do not freeze world time: genuine end-to-end completion banks
            // the optional signal only after actually reaching the east post.
            hunter.stun=150f;hunter.timer=150f;
            UnsentWalkToEasternPost(g,east);
            g.Talk(east);
            Check(UnsentChoice(g,"unsent_deliver_2")&&g.UnsentRiverDelivered&&
                g.Has("unsent_warning_relay_2")&&
                !g.UnsentWarningRunActive,
                "A genuinely delivered warning banks the optional tactical receipt once");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.UnsentRiverDelivered&&
                loaded.Has("unsent_warning_relay_2")&&
                !loaded.UnsentWarningRunActive,
                "True relay receipt persists on SaveData v2 without resuming stale pursuer actors");
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
                    Check((room==2?!g.UnsentRiverDelivered:!g.UnsentMineDelivered)&&
                        g.UnsentWarningRunActive&&g.UnsentWarningTravel<18f,
                        "Early arrival cannot bank THIS region's warning without actual walking");
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
                // A complete player-earned branch changes the ORIGINAL
                // Hearth Inn traveler, not a newly invented NPC or speaker.
                SilentLedgerVisitRealInnFromEmberfall(g);
                ReadInnNews(g,Object(g,"innGuest"));
                Check(outcome==0?
                    g.conversation.textEn.Contains("Two soaked warnings")&&
                    !g.conversation.textEn.Contains("innkeeper leaves a blank space"):
                    g.conversation.textEn.Contains("innkeeper leaves a blank space")&&
                    !g.conversation.textEn.Contains("Two soaked warnings"),
                    "The authentic inn traveler recalls only the earned public/private warning ending");
                g.Close();
                for(int room=2;room<=3;room++)
                {
                    g.Enter(room);g.enemies.Clear();
                    WorldObject post=UnsentPost(g,room,false);
                    Check(PlaceWithinTalkReach(g,post),
                        "Earned civic services are physically tied to each existing road marker");
                    g.Talk(post);
                    Check(g.mode=="dialog"&&g.conversation!=null&&
                        (outcome==0?
                            g.conversation.textEn.Contains("public warning now staffs first aid"):
                            g.conversation.textEn.Contains("witnesses' quiet crew")),
                        "The original accessible waytrail explains the actual earned branch-specific service");
                    g.Close();
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
            // Refill between repeated genuine combat hit transactions so
            // packet failure, rather than hero death, is what this tests.
            g.save.health=g.save.maxHealth;g.invulnerable=0;
            hurt.Invoke(g,new object[]{99f,"shot",null});
            g.save.health=g.save.maxHealth;g.invulnerable=0;
            hurt.Invoke(g,new object[]{99f,"shot",null});
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
