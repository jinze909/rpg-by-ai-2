using System;
namespace Starfall.Tests
{
    // Compiled alongside all genuine GameCore files and the ORIGINAL
    // shipped map fixture. No mock boat, reward, collision or quest clock.
    public static partial class CoreChecks
    {
        private static GameCore BoatReady(Campaign data,bool completePrior)
        {
            GameCore g=MistlightReady(data);
            if(completePrior)g.save.flags.Add("mistlight_public");
            return g;
        }
        private static WorldObject BoatPost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing authentic Mistport boat source "+id);
        }
        private static void BoatAt(GameCore g,WorldObject o)
        {
            Check(g.CanStand(new Point(o.x,o.y)),
                "The survivor boat support post is real standable Mistport shoreline: "+o.id);
            g.save.x=o.x;g.save.y=o.y;
        }
        private static bool BoatChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(i<0)return false;
            g.Choose(i);return true;
        }
        private static GameCore BoatStart(Campaign data)
        {
            var g=BoatReady(data,true);
            var notice=BoatPost(g,"mistconvoy_notice");
            BoatAt(g,notice);g.Talk(notice);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.textEn.Contains("crew list"),
                "Keeper's second authentic handwritten log starts its own new story");
            Check(BoatChoice(g,"mistconvoy_start")&&
                g.MistConvoyActive&&g.MistConvoyStage==1&&
                g.MistConvoySeconds==86f&&g.MistConvoyHull==76f&&
                g.save.trackedQuest=="mistport_lastboat",
                "The actual notice starts the full 86s civilian boat escort and journal");
            Point foot=g.MistConvoyBoatFoot;
            Check(foot.x==70.5f&&foot.y==38.5f&&
                g.Map.rows[(int)foot.y][(int)foot.x]=='~',
                "Real civilian hull physically begins in the authored Mistport WATER channel");
            g.Enter(0);
            Check(g.save.room==5&&g.MistConvoyActive,
                "Active boat rescue cannot be deleted by changing region");
            g.ReturnToTown();
            Check(g.save.room==5&&g.MistConvoyActive,
                "Active boat rescue cannot be skipped by direct fast return");
            return g;
        }
        public static void MistportLastBoatRequiresOriginalLightAndRealDockPassage(Campaign data)
        {
            var locked=BoatReady(data,false);
            WorldObject source=BoatPost(locked,"mistconvoy_notice");
            BoatAt(locked,source);locked.Talk(source);
            Check(!BoatChoice(locked,"mistconvoy_start")&&
                !locked.MistConvoyActive&&locked.GetQuest("mistport_lastboat").locked,
                "The genuine earlier Last Light rescue must finish before a new crew boat is offered");

            var g=BoatStart(data);
            WorldObject horn=BoatPost(g,"mistconvoy_horn"),
                midway=BoatPost(g,"mistconvoy_mid"),
                dock=BoatPost(g,"mistconvoy_dock");
            BoatAt(g,midway);g.Talk(midway);
            Check(BoatChoice(g,"mistconvoy_mid")&&g.MistConvoyStage==1,
                "The midway relay cannot be remotely banked before a horn launch");
            BoatAt(g,horn);g.save.energy=7f;g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_horn")&&g.MistConvoyStage==1,
                "Physical horn cannot sound without its actual 8 stamina cost");
            g.save.energy=100f;g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_horn")&&g.MistConvoyStage==2&&
                g.save.energy==92f,
                "Authenticated north-bank horn starts moving boat and costs exactly 8 energy");
            Point water=g.MistConvoyBoatFoot;
            for(int i=0;i<24;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>water.y+.5f&&
                g.MistConvoyBoatFollowing,
                "The actual civilian hull MOVES over physical water every world tick only when shore-guided");
            BoatAt(g,midway);g.Talk(midway);
            Check(BoatChoice(g,"mistconvoy_mid")&&g.MistConvoyStage==2,
                "Even standing at the original relay cannot confirm a boat still upstream");
            for(int i=0;i<78;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>=40.45f&&
                g.Map.rows[(int)g.MistConvoyBoatFoot.y][(int)g.MistConvoyBoatFoot.x]=='~',
                "Survivor boat reached the real midpoint without crossing solid ground");
            g.Talk(midway);
            Check(BoatChoice(g,"mistconvoy_mid")&&g.MistConvoyStage==3&&
                (g.GetQuest("mistport_lastboat").target-new Point(dock.x,dock.y)).Length<.01f,
                "Midway confirmation genuinely requires boat arrival and routes to actual south dock");
            // Optional survivor is an actual timed, costly rescue at the
            // physical relay: no free flag for merely clicking the dialogue.
            g.save.energy=4f;g.Talk(midway);
            Check(BoatChoice(g,"mistconvoy_drifter")&&
                !g.MistConvoyDrifterAboard&&g.save.energy==4f,
                "Low stamina prevents taking the stranger aboard");
            g.save.energy=100f;g.Talk(midway);
            float beforeDetourTime=g.MistConvoySeconds,
                beforeDetourHull=g.MistConvoyHull;
            Check(BoatChoice(g,"mistconvoy_drifter")&&
                g.MistConvoyDrifterAboard&&g.save.energy==82f&&
                Math.Abs(g.MistConvoySeconds-(beforeDetourTime-12f))<.01f&&
                Math.Abs(g.MistConvoyHull-(beforeDetourHull-12f))<.01f&&
                !g.save.flags.Contains("mistconvoy_drifter"),
                "Physical drifter rescue spends energy, deadline and hull, but grants no premature save flag");
            g.Talk(midway);
            Check(!BoatChoice(g,"mistconvoy_drifter"),
                "One survivor cannot be picked up repeatedly for extra rewards");
            g.Close();
            BoatAt(g,dock);g.Talk(dock);
            Check(BoatChoice(g,"mistconvoy_public")&&g.MistConvoyStage==3,
                "A staffed dock alone cannot fake rescued crew before the hull arrives");
            for(int i=0;i<152;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>=44.4f&&g.MistConvoyHull>0f,
                "Actual waterborne survivors physically arrive beside the south dock alive");
            int coins=g.save.coins,shards=g.save.shards;
            g.Talk(dock);
            Check(BoatChoice(g,"mistconvoy_public")&&
                g.MistConvoyComplete&&g.MistConvoyPublic&&!g.MistConvoyHidden&&
                !g.MistConvoyActive&&g.save.coins==coins+110&&
                g.save.shards==shards+2&&g.GetQuest("mistport_lastboat").complete&&
                g.save.flags.Contains("mistconvoy_drifter"),
                "Public survivor reception pays actual first completion only after full moving escort");
            g.Talk(dock);
            Check(!BoatChoice(g,"mistconvoy_hidden")&&
                g.save.coins==coins+110,
                "The other branch can never be claimed after accepting a real rescue outcome");
            g.Close();
            BoatAt(g,horn);g.save.health=35f;
            var notFunded=CloneSave(g.save);
            notFunded.flags.Remove("mistconvoy_public");
            var control=new GameCore(data,notFunded);control.Start();
            Check(g.mode=="world"&&control.mode=="world"&&
                g.CanStand(g.Position)&&control.CanStand(control.Position),
                "Public survivor recovery and its unfunded control both run on the genuine safe harbor");
            for(int frame=0;frame<12;frame++)
            {g.Tick(.05f,new Point());control.Tick(.05f,new Point());}
            Check(g.save.health>control.save.health+1.2f,
                "Public safe dock rescue actually funds locally reachable wound healing rather than only changing story flags");
            SilentLedgerVisitRealInnFromEmberfall(g);
            ReadInnNews(g,Object(g,"innGuest"));
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("thirteenth mug")&&
                g.conversation.textEn.Contains("writes her own name")&&
                !g.conversation.textEn.Contains("The patrol counted twelve passengers"),
                "Only actual publicly sheltered survivors are remembered by the original Hearth Inn traveler");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.MistConvoyPublic&&
                loaded.save.flags.Contains("mistconvoy_drifter")&&
                !loaded.MistConvoyActive&&loaded.GetQuest("mistport_lastboat").complete,
                "New story outcome persists through original v2 records without phantom active boats");
        }
        // The chart can only begin after a truly landed extra passenger,
        // not a synthetic free flag or a dialogue-only boat completion.
        private static GameCore BoatBringDrifterHome(Campaign data)
        {
            GameCore g=BoatStart(data);
            WorldObject horn=BoatPost(g,"mistconvoy_horn"),
                mid=BoatPost(g,"mistconvoy_mid"),
                dock=BoatPost(g,"mistconvoy_dock");
            BoatAt(g,horn);g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_horn"),
                "Authentic north horn must launch the crowded vessel");
            BoatAt(g,mid);
            for(int i=0;i<85;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>=40.45f,
                "Actual water-channel hull arrives at the midway relay");
            g.Talk(mid);
            Check(BoatChoice(g,"mistconvoy_mid")&&g.MistConvoyStage==3,
                "True midway signal precedes additional passenger rescue");
            g.save.energy=100f;g.Talk(mid);
            Check(BoatChoice(g,"mistconvoy_drifter")&&g.MistConvoyDrifterAboard,
                "Real drifter boards after costly physical relay intervention");
            BoatAt(g,dock);
            for(int i=0;i<152;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>=44.40f&&g.MistConvoyHull>0f,
                "Drifter cannot unlock a tide chart without real safe boat landing");
            g.Talk(dock);
            Check(BoatChoice(g,"mistconvoy_public")&&g.MistConvoyComplete&&
                g.MistChartUnlocked&&!g.MistChartStarted,
                "Actual survivor reception releases future chart testimony");
            return g;
        }
        public static void MistportDriftersTideChartRequiresPhysicalSalvageAndChangesRealWorld(Campaign data)
        {
            var locked=BoatReady(data,true);
            Check(locked.GetQuest("mistport_tidechart").locked&&
                !locked.MistChartUnlocked,
                "Old saves and ordinary sailors cannot invent a rescued drifter's chart");
            for(int branch=0;branch<2;branch++)
            {
                var g=BoatBringDrifterHome(data);
                WorldObject notice=BoatPost(g,"mistconvoy_notice"),
                    mid=BoatPost(g,"mistconvoy_mid"),
                    dock=BoatPost(g,"mistconvoy_dock");
                Check(!g.GetQuest("mistport_tidechart").locked,
                    "Earned rescued drifter unlocks distinct physical follow-up quest");
                BoatAt(g,notice);g.Talk(notice);
                Check(BoatChoice(g,"mistchart_start")&&g.MistChartStarted&&
                    g.save.trackedQuest=="mistport_tidechart"&&
                    g.GetQuest("mistport_tidechart").steps.Count==3,
                    "Keeper's original crew ledger opens an actual routed chart quest");
                BoatAt(g,mid);g.Talk(mid);
                int wood=g.save.inventory[0],gear=g.save.inventory[4];
                g.save.x=55.5f;
                Check(BoatChoice(g,"mistchart_recover")&&!g.MistChartRecovered&&
                    g.save.inventory[0]==wood&&g.save.inventory[4]==gear,
                    "Moving away from real relay cancels stale salvage choice without materials");
                BoatAt(g,mid);g.save.energy=5f;g.Talk(mid);
                Check(BoatChoice(g,"mistchart_recover")&&!g.MistChartRecovered,
                    "A tired traveler cannot remotely or freely recover chart");
                g.save.energy=100f;g.save.inventory[4]=0;
                g.Talk(mid);
                Check(BoatChoice(g,"mistchart_recover")&&!g.MistChartRecovered,
                    "Chart needs an actual machine part and wooden line, not a free story flag");
                g.save.inventory[0]=3;g.save.inventory[4]=3;
                int coins=g.save.coins,xp=g.save.xp;
                g.Talk(mid);
                Check(BoatChoice(g,"mistchart_recover")&&g.MistChartRecovered&&
                    g.save.inventory[0]==2&&g.save.inventory[4]==2&&
                    Math.Abs(g.save.energy-88f)<.01f,
                    "Real physical relay salvage spends one timber, one part and twelve stamina");
                Check(g.GetQuest("mistport_tidechart").steps[1].done&&
                    (g.GetQuest("mistport_tidechart").target-
                     new Point(dock.x,dock.y)).Length<.01f,
                    "Original quest journal routes the recovered chart to the real dock");
                BoatAt(g,dock);g.Talk(dock);
                Check(BoatChoice(g,branch==0?"mistchart_public":"mistchart_secret")&&
                    g.MistChartComplete&&g.GetQuest("mistport_tidechart").complete&&
                    g.save.coins==coins+45&&g.save.xp==xp+90,
                    "Two exclusive chart resolutions award once only after genuine landing and salvage");
                Check(branch==0?g.MistChartPublic&&!g.MistChartSecret:
                    g.MistChartSecret&&!g.MistChartPublic,
                    "Public medical watch and confidential pilot supply have exclusive v2 flags");
                g.Talk(dock);
                Check(!BoatChoice(g,branch==0?"mistchart_secret":"mistchart_public")&&
                    g.save.coins==coins+45,
                    "Revisiting dock cannot claim the opposing reward or mint coins");
                g.Close();
                var controlSave=CloneSave(g.save);
                controlSave.flags.Remove(branch==0?"mistchart_public":"mistchart_secret");
                var control=new GameCore(data,controlSave);control.Start();
                if(branch==0)
                {
                    BoatAt(g,mid);
                    control.save.x=mid.x;control.save.y=mid.y;
                    g.save.health=control.save.health=25f;
                    for(int i=0;i<12;i++)
                    {g.Tick(.05f,new Point());control.Tick(.05f,new Point());}
                    Check(g.save.health>control.save.health+.8f,
                        "Published tide chart really heals travelers at staffed original relay");
                }
                else
                {
                    BoatAt(g,dock);control.save.x=dock.x;control.save.y=dock.y;
                    g.save.energy=control.save.energy=10f;
                    for(int i=0;i<12;i++)
                    {g.Tick(.05f,new Point());control.Tick(.05f,new Point());}
                    Check(g.save.energy>control.save.energy+1f,
                        "Secret tide chart really improves active safe Mistport stamina recovery");
                }
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.MistChartComplete&&
                    restored.GetQuest("mistport_tidechart").complete&&
                    (branch==0?restored.MistChartPublic:restored.MistChartSecret),
                    "Tide chart and earned practical consequences persist through original v2");
            }
        }
        public static void MistportLastBoatUnguidedHullStallsAndRealSurfCanSinkIt(Campaign data)
        {
            GameCore g=BoatStart(data);
            WorldObject horn=BoatPost(g,"mistconvoy_horn");
            BoatAt(g,horn);g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_horn")&&g.MistConvoyStage==2,
                "Actual horn launch opens boat escort for dangerous shore abandonment");
            Point origin=g.MistConvoyBoatFoot;
            float hull=g.MistConvoyHull;
            Point far=new Point(54.5f,38.5f);
            Check(g.CanStand(far),"The original west port is a physically standable unguided position");
            g.save.x=far.x;g.save.y=far.y;
            for(int i=0;i<30;i++)g.Tick(.05f,new Point());
            Check((g.MistConvoyBoatFoot-origin).Length<.001f&&
                g.MistConvoyHull<hull-3.5f&&!g.MistConvoyBoatFollowing,
                "The actual unmanned hull STOPS and loses integrity when no nearby shore guide exists");
            for(int i=0;i<800&&g.MistConvoyActive;i++)g.Tick(.05f,new Point());
            Check(!g.MistConvoyActive&&!g.MistConvoyComplete&&
                !g.save.flags.Contains("mistconvoy_drifter")&&
                !g.GetQuest("mistport_lastboat").complete,
                "Real surf and abandonment can sink civilian vessel; no fake completion or rewards");
            WorldObject notice=BoatPost(g,"mistconvoy_notice");
            BoatAt(g,notice);g.Talk(notice);
            Check(BoatChoice(g,"mistconvoy_start")&&g.MistConvoyActive&&
                g.MistConvoyHull==76f&&g.MistConvoyStage==1,
                "A failed real escort can be restarted from its original keeper log");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&!loaded.MistConvoyActive&&
                !loaded.MistConvoyComplete,
                "Unfinished storm vessel state is never fabricated from old v2 save flags");
        }
        public static void MistportLastBoatOnePartBraceAndSecretOutcomeGrantActualSafeEndurance(Campaign data)
        {
            GameCore g=BoatStart(data);
            WorldObject horn=BoatPost(g,"mistconvoy_horn"),
                mid=BoatPost(g,"mistconvoy_mid"),
                dock=BoatPost(g,"mistconvoy_dock");
            BoatAt(g,horn);g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_horn"),"Launch real boat before one-time bracing");
            g.save.inventory[4]=1;g.Talk(horn);
            Check(BoatChoice(g,"mistconvoy_brace")&&
                g.MistConvoyPartUsed&&g.save.inventory[4]==0&&
                g.MistConvoyHull==100f&&g.MistConvoyBraceSeconds==10f,
                "Part is actually spent to strengthen the real moving hull and shelter next swell");
            g.Talk(horn);
            Check(!BoatChoice(g,"mistconvoy_brace")&&g.save.inventory[4]==0,
                "One-use machine-part reinforcement cannot be duplicated");
            // No brace choice means the original dialogue remains open.
            // Close it exactly like a player before advancing WORLD time.
            g.Close();
            Check(g.mode=="world",
                "Actual live hull and storm clocks only advance outside a paused dialogue");
            BoatAt(g,mid);
            for(int i=0;i<160;i++)g.Tick(.05f,new Point());
            Check(Math.Abs(g.MistConvoyHull-100f)<.01f,
                "Real hull bracing must preserve 100 integrity through first wave; observed "+g.MistConvoyHull);
            Check(g.MistConvoyBraceSeconds>1.9f,
                "Actual 10s brace must survive 8s of gameplay; observed "+g.MistConvoyBraceSeconds);
            Check(g.MistConvoyBoatFoot.y>=40.45f,
                "Physically guided boat must reach true midway by 8s; actual "+g.MistConvoyBoatFoot.y+
                ", stage "+g.MistConvoyStage+", remaining "+g.MistConvoySeconds);
            g.Talk(mid);
            Check(BoatChoice(g,"mistconvoy_mid")&&g.MistConvoyStage==3,
                "Shielded boat still requires physical midway signaling");
            BoatAt(g,dock);
            for(int i=0;i<152;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyBoatFoot.y>=44.40f&&g.MistConvoyHull>0f,
                "Shielded survivors reach original south dock and must still disembark");
            g.Talk(dock);
            Check(BoatChoice(g,"mistconvoy_hidden")&&g.MistConvoyHidden&&
                !g.MistConvoyPublic&&g.MistConvoyComplete,
                "Real rescue can protect undisclosed night-pilot passengers");
            g.save.energy=12f;
            var unfundedSave=CloneSave(g.save);
            unfundedSave.flags.Remove("mistconvoy_hidden");
            var without=new GameCore(data,unfundedSave);
            without.Start();
            Check(g.mode=="world"&&without.mode=="world"&&
                g.save.room==5&&without.save.room==5,
                "Safe-energy comparator uses two actively running identical original Mistport worlds");
            for(int i=0;i<12;i++)
            {g.Tick(.05f,new Point());without.Tick(.05f,new Point());}
            Check(g.save.energy>without.save.energy+1.6f,
                "Night-pilot outcome changes actual world stamina regen by +3/s, not just a reward toast");
            SilentLedgerVisitRealInnFromEmberfall(g);
            ReadInnNews(g,Object(g,"innGuest"));
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("The patrol counted twelve passengers")&&
                !g.conversation.textEn.Contains("thirteenth mug"),
                "Original Hearth Inn recalls only the secret pilot's passenger count after the genuine rescue");
            g.Close();
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.MistConvoyHidden&&
                !restored.MistConvoyActive&&restored.GetQuest("mistport_lastboat").complete,
                "Secret convoy outcome remains exclusive across a genuine v2 reload");
        }
    }
}
