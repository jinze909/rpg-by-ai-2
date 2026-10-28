using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SilentLedgerReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=8;
            g.Enter(4);
            Check(g.save.room==4,"Real chapter progress allows Citadel entry");
            g.enemies.Clear();g.projectiles.Clear();g.invulnerable=150f;
            g.save.flags.Add("boss_3");
            return g;
        }
        private static WorldObject SilentLedgerPost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)if(o.id==id)return o;
            throw new Exception("Missing authored Citadel silent-ledger source "+id);
        }
        private static bool SilentLedgerChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(index<0)return false;
            g.Choose(index);return true;
        }
        private static void SilentLedgerMoveTo(GameCore g,WorldObject site)
        {
            Check(g.CanStand(new Point(site.x,site.y)),
                "Stealth ledger objective physically sits on standable Citadel ground");
            g.save.x=site.x;g.save.y=site.y;
        }
        private static GameCore SilentLedgerStart(Campaign data)
        {
            var g=SilentLedgerReady(data);
            WorldObject notice=SilentLedgerPost(g,"silentledger_notice");
            SilentLedgerMoveTo(g,notice);g.Talk(notice);
            Check(SilentLedgerChoice(g,"silentledger_start")&&
                g.SilentLedgerStage==1&&g.SilentLedgerTime>150f,
                "Original physical notice starts a fully timed stealth infiltration");
            Check((g.GetQuest("silent_ledger").target-new Point(60.5f,36.5f)).Length<.01f,
                "Journal correctly targets the genuine northern register");
            g.Enter(0);
            Check(g.save.room==4&&g.SilentLedgerStage==1,
                "Active infiltration cannot silently warp and lose live sentries");
            return g;
        }
        public static void SilentLedgerThreePhysicalEvidenceSitesAndPublicArchiveBenefit(Campaign data)
        {
            var g=SilentLedgerStart(data);
            WorldObject north=SilentLedgerPost(g,"silentledger_record"),
                south=SilentLedgerPost(g,"silentledger_witness"),
                console=SilentLedgerPost(g,"silentledger_console");
            SilentLedgerMoveTo(g,console);g.Talk(console);
            Check(!SilentLedgerChoice(g,"silentledger_public")&&
                g.SilentLedgerStage==1,
                "The sealed console never fabricates both evidentiary steps");
            SilentLedgerMoveTo(g,south);g.Talk(south);
            Check(SilentLedgerChoice(g,"silentledger_witness")&&
                g.SilentLedgerStage==1,
                "Witness statement cannot be banked ahead of the true register");
            SilentLedgerMoveTo(g,north);g.Talk(north);
            Check(SilentLedgerChoice(g,"silentledger_record")&&
                g.SilentLedgerStage==2,
                "Physically touching the first original register advances stage one");
            g.Talk(north);
            Check(SilentLedgerChoice(g,"silentledger_record")&&g.SilentLedgerStage==2,
                "The initial evidence may not be duplicated for stage two");
            SilentLedgerMoveTo(g,south);g.Talk(south);
            Check(SilentLedgerChoice(g,"silentledger_witness")&&g.SilentLedgerStage==3,
                "Physically reaching second witness gives the distinct testimony");
            Check((g.GetQuest("silent_ledger").target-new Point(console.x,console.y)).Length<.01f,
                "Live quest route now leads to the actual final seal console");
            int coins=g.save.coins,shards=g.save.shards;
            SilentLedgerMoveTo(g,console);g.Talk(console);
            Check(SilentLedgerChoice(g,"silentledger_public")&&g.SilentLedgerPublic&&
                !g.SilentLedgerRoutes&&g.SilentLedgerComplete&&
                g.save.coins==coins+65&&g.save.shards==shards+2&&
                !g.SilentLedgerActive,
                "Real third interaction publicly releases the names once and clears stealth clocks");
            g.save.health=32f;g.save.energy=35f;
            for(int i=0;i<25;i++)g.Tick(.05f,new Point());
            Check(g.save.health>32f&&g.save.energy>35f,
                "Public missing-person archive now provides actual safe, physical recovery");
            g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&reloaded.SilentLedgerPublic&&
                !reloaded.SilentLedgerActive&&reloaded.GetQuest("silent_ledger").complete,
                "Original SaveData v2 round-trip persists public policy only, not transient patrol");
        }
        public static void SilentLedgerSentriesReallyDetectForwardSightAndOneUseDecoyMovesThem(Campaign data)
        {
            var g=SilentLedgerStart(data);
            var bell=SilentLedgerPost(g,"silentledger_decoy");
            Point sentry=g.SilentLedgerSentryPosition(0);
            Point face=g.SilentLedgerSentryFacing(0);
            Point visible=sentry+face*1.4f;
            Check(g.CanStand(sentry)&&g.CanStand(visible),
                "Actual starting night patrol and line-of-sight position occupy passable Citadel cells");
            g.save.x=visible.x;g.save.y=visible.y;
            for(int i=0;i<10;i++)g.Tick(.05f,new Point());
            Check(g.SilentLedgerSuspicion>0f&&
                g.SilentLedgerSentrySearchSeconds(0)>0f,
                "Real moving sentry's forward physical sight raises suspicion and starts an actual search");
            float seen=g.SilentLedgerSuspicion;
            // The target is now outside sight and audible range, so the
            // same suspicion must dissipate rather than accumulating forever.
            g.save.x=bell.x;g.save.y=bell.y;
            for(int i=0;i<15;i++)g.Tick(.05f,new Point());
            Check(g.SilentLedgerStage==1&&g.SilentLedgerSuspicion<seen,
                "Real loss of line of sight decays alert while patrol keeps moving");
            g.save.inventory[4]=2;
            SilentLedgerMoveTo(g,bell);g.Talk(bell);
            int bellChoiceIndex=Array.FindIndex(g.conversation.choices,c=>c.action=="silentledger_decoy");
            Check(bellChoiceIndex>=0,"Authored physical watch-bell provides a true gear diversion");
            g.save.x=bell.x-8f;g.Choose(bellChoiceIndex);
            Check(g.save.inventory[4]==2&&g.SilentLedgerDiversion==0,
                "Stale remote menu cannot spend gear or teleport guards");
            SilentLedgerMoveTo(g,bell);g.Talk(bell);
            Check(SilentLedgerChoice(g,"silentledger_decoy")&&
                g.save.inventory[4]==1&&g.SilentLedgerDiversion==12f&&
                g.SilentLedgerSentrySearchSeconds(0)==0f,
                "One real spare part overrides sight searches with a temporary patrol diversion");
            Point before=g.SilentLedgerSentryPosition(0);
            g.Tick(.05f,new Point());
            Point after=g.SilentLedgerSentryPosition(0);
            Check((after-before).Length>.005f&&after.x<before.x,
                "Sentry actually changes patrol motion toward the sounded western bell");
            g.Talk(bell);
            Check(!SilentLedgerChoice(g,"silentledger_decoy")&&
                g.save.inventory[4]==1,
                "One-use distraction cannot farm infinitely repeated safety");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(!loaded.SilentLedgerActive&&loaded.save.version==2,
                "Transient sentries and alarm correctly reset on old v2 save reload");
        }
        public static void SilentLedgerPatrolAlarmFailsRealRunWithoutRewardAndRoutesPersist(Campaign data)
        {
            var g=SilentLedgerReady(data);
            WorldObject notice=SilentLedgerPost(g,"silentledger_notice");
            g.save.flags.Remove("boss_3");
            SilentLedgerMoveTo(g,notice);g.Talk(notice);
            Check(!SilentLedgerChoice(g,"silentledger_start")&&
                !g.SilentLedgerActive,
                "Original guardian remains required, no stealth bypass of existing chapters");
            g.save.flags.Add("boss_3");g.Talk(notice);
            int start=Array.FindIndex(g.conversation.choices,c=>c.action=="silentledger_start");
            Check(start>=0,"Legitimate notice offers a start after original progression");
            g.save.x=notice.x+8;g.Choose(start);
            Check(!g.SilentLedgerActive,
                "Moving away after opening notice cannot start stealth via stale remote action");
            SilentLedgerMoveTo(g,notice);g.Talk(notice);
            SilentLedgerChoice(g,"silentledger_start");
            bool sawExposure=false;
            for(int frame=0;frame<140&&g.SilentLedgerActive;frame++)
            {
                Point sentry=g.SilentLedgerSentryPosition(0);
                Point facing=g.SilentLedgerSentryFacing(0);
                Point exposed=sentry+facing*.6f;
                if(!g.CanStand(exposed))exposed=sentry;
                g.save.x=exposed.x;g.save.y=exposed.y;
                g.Tick(.05f,new Point());
                sawExposure=sawExposure||g.SilentLedgerSuspicion>0f;
            }
            Check(sawExposure&&!g.SilentLedgerActive&&
                !g.SilentLedgerComplete,
                "Genuine accumulated sentry contact alarms out without granting permanent stealth rewards");
            g=SilentLedgerStart(data);
            WorldObject north=SilentLedgerPost(g,"silentledger_record"),
                south=SilentLedgerPost(g,"silentledger_witness"),
                console=SilentLedgerPost(g,"silentledger_console");
            SilentLedgerMoveTo(g,north);g.Talk(north);
            SilentLedgerChoice(g,"silentledger_record");
            SilentLedgerMoveTo(g,south);g.Talk(south);
            SilentLedgerChoice(g,"silentledger_witness");
            float before=g.Speed;
            SilentLedgerMoveTo(g,console);g.Talk(console);
            Check(SilentLedgerChoice(g,"silentledger_routes")&&
                g.SilentLedgerRoutes&&!g.SilentLedgerPublic&&
                g.Speed>before+.34f,
                "Alternative civilian route branch affects genuine local movement speed");
            g.PrepareSave();var reload=new GameCore(data,CloneSave(g.save));
            Check(reload.save.version==2&&reload.SilentLedgerRoutes&&
                !reload.SilentLedgerPublic&&!reload.SilentLedgerActive,
                "Mutually exclusive routes choice persists in original v2 flags");
        }
    }
}
