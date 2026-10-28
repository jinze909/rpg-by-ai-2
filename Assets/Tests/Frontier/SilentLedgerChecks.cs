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
                g.SilentLedgerSentrySearchSeconds(0)>0f&&
                !g.SilentLedgerSentrySearchingSound(0),
                "A real forward sighting is tracked as visual pursuit, never mislabeled as noise");
            float seen=g.SilentLedgerSuspicion;
            // The target is now outside sight and audible range, so the
            // same suspicion must dissipate rather than accumulating forever.
            Point outOfSight=new Point(44.5f,50.5f);
            Check(g.CanStand(outOfSight)&&
                (outOfSight-g.SilentLedgerSentryPosition(0)).Length>10f&&
                (outOfSight-g.SilentLedgerSentryPosition(1)).Length>10f,
                "Hiding test uses real reachable-world ground beyond BOTH actual moving patrols");
            g.save.x=outOfSight.x;g.save.y=outOfSight.y;
            for(int i=0;i<25;i++)g.Tick(.05f,new Point());
            Check(g.SilentLedgerStage==1&&g.SilentLedgerSuspicion<seen,
                "After genuinely leaving BOTH patrol sightlines, alarm recovers: initial="+
                seen+" remaining="+g.SilentLedgerSuspicion);
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
        public static void SilentLedgerWeaponNoiseOnlyAlertsPhysicallyHearingPatrols(Campaign data)
        {
            var nearby=SilentLedgerStart(data);
            Point sentry=nearby.SilentLedgerSentryPosition(0);
            Point hero=new Point(nearby.save.x,nearby.save.y);
            Check((hero-sentry).Length<8f&&hero.x<sentry.x,
                "Original Citadel notice is behind a real close patrolling sentry");
            nearby.Attack(new Point(-1f,0f));
            nearby.Tick(.05f,new Point());
            Check(nearby.SilentLedgerSuspicion>0f&&
                nearby.SilentLedgerSentrySearchSeconds(0)>3.5f&&
                nearby.SilentLedgerSentrySearchingSound(0)&&
                (nearby.SilentLedgerSentrySearchTarget(0)-hero).Length<.01f,
                "Real close weapon swing raises suspicion and sends a guard to its actual sound source");

            var distant=SilentLedgerStart(data);
            Point isolated=new Point(44.5f,50.5f);
            Check(distant.CanStand(isolated)&&
                (isolated-distant.SilentLedgerSentryPosition(0)).Length>10f&&
                (isolated-distant.SilentLedgerSentryPosition(1)).Length>10f,
                "Distant sword noise test uses reachable ground beyond both real searchers");
            distant.save.x=isolated.x;distant.save.y=isolated.y;
            distant.Tick(.05f,new Point());
            Check(distant.SilentLedgerSuspicion==0f&&
                distant.SilentLedgerSentrySearchSeconds(0)==0f&&
                distant.SilentLedgerSentrySearchSeconds(1)==0f,
                "Moving outside both patrols' actual hearing must not freeze or raise alert");
            distant.Attack(new Point(1f,0f));
            distant.Tick(.05f,new Point());
            Check(distant.SilentLedgerSuspicion==0f&&
                distant.SilentLedgerSentrySearchSeconds(0)==0f&&
                distant.SilentLedgerSentrySearchSeconds(1)==0f,
                "Distant sword attack cannot summon sentries or trigger a map-wide stealth alarm");
        }

        public static void SilentLedgerRestorationImprovesRealChapterFourCivilianEvacuation(Campaign data)
        {
            float[] followDistance=new float[3];
            for(int branch=0;branch<3;branch++)
            {
                var g=SilentLedgerReady(data);
                g.save.flags.Add("triad_complete");
                if(branch==1)g.save.flags.Add("silentledger_public");
                if(branch==2)g.save.flags.Add("silentledger_routes");
                WorldObject refuge=Object(g,"blackoutshelter");
                g.save.x=refuge.x;g.save.y=refuge.y;
                Action(g,refuge.kind,"blackout_start");
                Check(g.BlackoutActive&&
                    Math.Abs(g.BlackoutTime-(branch==1?195f:180f))<.01f,
                    "Publicly restored missing-person names extend the actual original Blackout countdown by 15 seconds; legacy slots remain 180");
                WorldObject mother=Object(g,"blackoutcivilian1");
                g.save.x=mother.x;g.save.y=mother.y;
                Action(g,mother.kind,"blackout_pick_1");
                Check(g.BlackoutFollowing==1&&g.BlackoutFollowerHealth==48,
                    "Original endangered mother must be picked up physically; new ledger never auto-rescues her");
                Point walkTo=new Point(mother.x+2.5f,mother.y);
                Check(g.CanStand(walkTo),
                    "Actual chapter-four on-foot evacuation step is physically standable");
                g.save.x=walkTo.x;g.save.y=walkTo.y;
                Point before=g.BlackoutFollowerPosition;
                g.Tick(.05f,new Point());
                followDistance[branch]=(g.BlackoutFollowerPosition-before).Length;
                Check(g.BlackoutActive&&g.BlackoutFollowing==1&&
                    g.CanStand(g.BlackoutFollowerPosition),
                    "Original live follower still obeys map collisions with a durable ledger outcome");
            }
            Check(followDistance[0]>.26f&&followDistance[0]<.31f&&
                Math.Abs(followDistance[1]-followDistance[0])<.015f&&
                followDistance[2]>followDistance[0]+.025f,
                "Civilian route chart genuinely increases one 50ms walking step from 5.8 to 6.5 while public names change only the live clock");
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
