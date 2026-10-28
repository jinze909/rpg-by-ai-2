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
                !g.MistConvoyActive&&g.save.coins==coins+75&&
                g.save.shards==shards+2&&g.GetQuest("mistport_lastboat").complete,
                "Public survivor reception pays actual first completion only after full moving escort");
            g.Talk(dock);
            Check(!BoatChoice(g,"mistconvoy_hidden")&&
                g.save.coins==coins+75,
                "The other branch can never be claimed after accepting a real rescue outcome");
            SilentLedgerVisitRealInnFromEmberfall(g);
            ReadInnNews(g,Object(g,"innGuest"));
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("thirteenth mug")&&
                !g.conversation.textEn.Contains("The patrol counted twelve passengers"),
                "Only actual publicly sheltered survivors are remembered by the original Hearth Inn traveler");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.MistConvoyPublic&&
                !loaded.MistConvoyActive&&loaded.GetQuest("mistport_lastboat").complete,
                "New story outcome persists through original v2 records without phantom active boats");
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
            BoatAt(g,mid);
            for(int i=0;i<160;i++)g.Tick(.05f,new Point());
            Check(g.MistConvoyHull==100f&&g.MistConvoyBraceSeconds>1.9f&&
                g.MistConvoyBoatFoot.y>=40.45f,
                "After 8s real wave, physical brace protects actual hull while boat reaches relay");
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
