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
            Check((after-before).Length>.005f&&after.x<before.x&&
                g.SilentLedgerSentryMoving(0)&&
                g.SilentLedgerSentryFootDistance(0)>.005f,
                "Sentry changes patrol motion and advances actual full-body Sprite gait only on physical footsteps");
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
            Check(nearby.playerAction!=null&&!nearby.playerAction.released,
                "An actual sword windup must be accepted before testing guard hearing");
            nearby.Tick(.05f,new Point());
            Point heardFoot=new Point(nearby.save.x,nearby.save.y);
            Check(nearby.SilentLedgerSuspicion>0f&&
                nearby.SilentLedgerSentrySearchSeconds(0)>3.5f&&
                nearby.SilentLedgerSentrySearchingSound(0)&&
                (nearby.SilentLedgerSentrySearchTarget(0)-heardFoot).Length<.01f,
                "Real close sword noise must send the guard to the moving actual sound source: suspicion="+
                nearby.SilentLedgerSuspicion+" search="+
                nearby.SilentLedgerSentrySearchSeconds(0)+" noise="+
                nearby.SilentLedgerSentrySearchingSound(0)+" actual="+
                heardFoot.x+","+heardFoot.y+" target="+
                nearby.SilentLedgerSentrySearchTarget(0).x+","+
                nearby.SilentLedgerSentrySearchTarget(0).y);

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

        public static void SilentLedgerOrdinaryPebbleReallyLuresSentriesAtItsPhysicalImpact(Campaign data)
        {
            var g=SilentLedgerStart(data);
            WorldObject notice=SilentLedgerPost(g,"silentledger_notice");
            SilentLedgerMoveTo(g,notice);
            Point origin=new Point(g.save.x,g.save.y);
            Point guard=g.SilentLedgerSentryPosition(0);
            Check((guard-origin).Length>2.5f,
                "Real starting guard cannot hear pebble release from the old archive notice");
            float energy=g.save.energy;
            g.ThrowPebble(new Point(1f,0f));
            Check(g.projectiles.Count==1&&
                g.projectiles[0].source=="quietDecoy"&&
                g.save.energy==energy-GameCore.PebbleEnergyCost&&
                g.SilentLedgerSentrySearchSeconds(0)==0f,
                "Original U-key throw spends stamina, spawns a REAL non-damaging stone and makes no distant release alarm");
            for(int i=0;i<16;i++)g.Tick(.05f,new Point());
            Point heard=g.SilentLedgerSentrySearchTarget(0);
            Check(g.projectiles.Count==0&&
                g.SilentLedgerSentrySearchSeconds(0)>0f&&
                g.SilentLedgerSentrySearchingSound(0)&&
                heard.x>origin.x+4f&&
                heard.x<origin.x+7.3f&&
                Math.Abs(heard.y-origin.y)<.15f,
                "Only the real swept pebble landing, NOT its original launch or aimed cursor, redirects the genuine sentry");
            Check(g.SilentLedgerSuspicion<50f,
                "Stone landing alone is not falsely counted as direct player detection");

            var close=SilentLedgerStart(data);
            Point launch=new Point(55.5f,39.5f);
            Check(close.CanStand(launch)&&
                (launch-close.SilentLedgerSentryPosition(0)).Length<2.5f,
                "Close arm-throw test uses real clear standable ground beside the original sentry");
            close.save.x=launch.x;close.save.y=launch.y;
            close.ThrowPebble(new Point(-1f,0f));
            Check(close.projectiles.Count==1&&
                close.SilentLedgerSentrySearchSeconds(0)>=2f&&
                close.SilentLedgerSentrySearchingSound(0)&&
                (close.SilentLedgerSentrySearchTarget(0)-launch).Length<.01f&&
                close.SilentLedgerSuspicion>=5f,
                "A nearby REAL throw is heard immediately at the launch foot even before its later landing");

            var distant=SilentLedgerStart(data);
            Point safe=new Point(44.5f,50.5f);
            Check(distant.CanStand(safe),
                "Distant no-alert stone starts on the actual west Citadel safe trail");
            distant.save.x=safe.x;distant.save.y=safe.y;
            distant.ThrowPebble(new Point(1f,0f));
            for(int i=0;i<16;i++)distant.Tick(.05f,new Point());
            Check(distant.SilentLedgerSuspicion==0f&&
                distant.SilentLedgerSentrySearchSeconds(0)==0f&&
                distant.SilentLedgerSentrySearchSeconds(1)==0f,
                "Neither the distant release nor a far physical stone collision gives guards omniscient hearing");
            distant.PrepareSave();
            var reload=new GameCore(data,CloneSave(distant.save));
            Check(reload.save.version==2&&!reload.SilentLedgerActive,
                "Ordinary stone patrol listeners add no fields to existing v2 saves");
        }

        public static void SilentLedgerSentriesSweepActualLastHeardGroundBeforeResumingPatrol(Campaign data)
        {
            var g=SilentLedgerStart(data);
            WorldObject shutter=SilentLedgerPost(g,"silentledger_shutter");
            SilentLedgerMoveTo(g,shutter);g.Talk(shutter);
            Check(SilentLedgerChoice(g,"silentledger_shutter")&&
                g.SilentLedgerSentrySearchSeconds(0)>4.5f,
                "Authentic copper latch sends the original western sentry to investigate");
            Point lastSound=new Point(shutter.x,shutter.y);
            Point safe=new Point(44.5f,50.5f);
            Check(g.CanStand(safe),"Evading an audible patrol uses real Citadel ground");
            g.save.x=safe.x;g.save.y=safe.y;
            bool reached=false;
            for(int i=0;i<82;i++)
            {
                g.Tick(.05f,new Point());
                if(g.SilentLedgerSentrySweeping(0))reached=true;
            }
            Check(reached&&g.SilentLedgerSentryScanTurns(0)>0&&
                (g.SilentLedgerSentryPosition(0)-lastSound).Length<.42f,
                "Sentry REALLY reaches the original copper latch and searches surrounding sight arcs");
            float walked=g.SilentLedgerSentryFootDistance(0);
            Point planted=g.SilentLedgerSentryPosition(0);
            Point looking=g.SilentLedgerSentryFacing(0);
            Check(g.SilentLedgerSentrySearchSeconds(0)>0f,
                "The four-second real investigation has time for meaningful on-site scanning");
            for(int k=0;k<8;k++)g.Tick(.05f,new Point());
            Check((g.SilentLedgerSentryPosition(0)-planted).Length<.01f&&
                Math.Abs(g.SilentLedgerSentryFootDistance(0)-walked)<.001f&&
                (g.SilentLedgerSentryFacing(0)-looking).Length>.5f,
                "Stationary scanner changes real vision direction WITHOUT faking walking or footfall animation");
            for(int i=0;i<35;i++)g.Tick(.05f,new Point());
            Check(g.SilentLedgerSentrySearchSeconds(0)==0f&&
                !g.SilentLedgerSentrySweeping(0),
                "Timed search ends and resumes the original active patrol rather than looping forever");
            g.PrepareSave();
            var copy=new GameCore(data,CloneSave(g.save));
            Check(!copy.SilentLedgerActive&&copy.SilentLedgerSentryScanTurns(0)==0&&
                copy.save.version==2,"Transient search sectors never alter original v2 save records");
        }

        public static void SilentLedgerPhysicalShutterTradesAudibleRiskForRealTemporaryCover(Campaign data)
        {
            var baseline=SilentLedgerStart(data);
            Point first=baseline.SilentLedgerSentryPosition(0);
            Point exposed=first+baseline.SilentLedgerSentryFacing(0)*4f;
            Check(baseline.CanStand(exposed),"Sentry forward four-tile test is actual Citadel ground");
            var sight=typeof(GameCore).GetMethod("SilentLedgerSeesHero",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(sight!=null,"Original authoritative physical sight method exists");
            baseline.save.x=exposed.x;baseline.save.y=exposed.y;
            Check((bool)sight.Invoke(baseline,new object[]{0}),
                "Fully lit original guard really SEES an unhidden four-cell forward target");
            baseline.Tick(.05f,new Point());
            Check(baseline.SilentLedgerSuspicion>0f,
                "A lit patrol's actual sight creates alert in the live world loop");

            var g=SilentLedgerStart(data);
            var shutter=SilentLedgerPost(g,"silentledger_shutter");
            Check(g.CanStand(new Point(shutter.x,shutter.y)),
                "New physical light control must sit on passable original Citadel ground");
            SilentLedgerMoveTo(g,shutter);g.Talk(shutter);
            int option=Array.FindIndex(g.conversation.choices,c=>c.action=="silentledger_shutter");
            Check(option>=0,"Light control offers an actual live environmental stealth choice");
            Point far=new Point(44.5f,50.5f);
            Check(g.CanStand(far),"Stale shutter test uses reachable Citadel ground");
            g.save.x=far.x;g.save.y=far.y;g.Choose(option);
            Check(g.SilentLedgerDarkness==0f&&!g.SilentLedgerShutterUsed,
                "Old distant shutter dialogue cannot remotely darken real guard lights");
            SilentLedgerMoveTo(g,shutter);g.Talk(shutter);
            Check(SilentLedgerChoice(g,"silentledger_shutter")&&
                g.SilentLedgerShutterUsed&&g.SilentLedgerDarkness==22f,
                "One physically reached shutter grants exactly 22 seconds of temporary concealment");
            Check(g.SilentLedgerSentrySearchSeconds(0)>4f&&
                g.SilentLedgerSentrySearchingSound(0)&&
                (g.SilentLedgerSentrySearchTarget(0)-new Point(shutter.x,shutter.y)).Length<.01f,
                "Audible copper latch actually dispatches the nearby patrolling sentry to its source");
            Check(g.SilentLedgerSuspicion>0f,
                "Opening the shutter incurs meaningful nearby guard alert instead of free invisibility");
            float during=g.SilentLedgerSuspicion;
            Point sentry=g.SilentLedgerSentryPosition(0);
            Point facing=g.SilentLedgerSentryFacing(0);
            Point cover=sentry+facing*4f;
            Check(g.CanStand(cover),"Darkness vision test has real standable forward patrol ground");
            g.save.x=cover.x;g.save.y=cover.y;
            // Query the SAME private production sight function without
            // quiet-step: otherwise a four-cell target is already outside
            // normal 3.45f crouched sight and the test proves nothing.
            Check(!(bool)sight.Invoke(g,new object[]{0}),
                "Real darkness alone blocks ordinary four-cell forward sight without relying on quiet mode");
            g.Tick(.05f,new Point(),null,false,true);
            Check(g.SilentLedgerDarkness>21f&&g.SilentLedgerSuspicion<during,
                "World-frame darkness safely reduces alert after a fully physical shutter activation");
            SilentLedgerMoveTo(g,shutter);g.Talk(shutter);
            Check(!SilentLedgerChoice(g,"silentledger_shutter")&&
                g.SilentLedgerShutterUsed,
                "Metal shutter cannot be repeatedly spammed to refresh infinite darkness");
            float pausedLight=g.SilentLedgerDarkness;
            Check(g.mode=="dialog",
                "After inspecting an exhausted shutter the real dialogue remains open");
            g.Tick(.05f,new Point());
            Check(g.SilentLedgerDarkness==pausedLight,
                "Opening a world dialogue pauses live shutter time rather than silently expiring it");
            g.Close();
            Check(g.mode=="world",
                "Explicitly finish the real conversation before advancing world-time expiry");
            g.save.x=far.x;g.save.y=far.y;
            for(int i=0;i<460;i++)g.Tick(.05f,new Point());
            Check(g.SilentLedgerStage==1&&g.SilentLedgerDarkness==0f,
                "Real world seconds restore light before mission timer expires");
            g.PrepareSave();var reload=new GameCore(data,CloneSave(g.save));
            Check(!reload.SilentLedgerActive&&reload.SilentLedgerDarkness==0f&&
                reload.save.version==2,
                "Light manipulation is transient and leaves legacy SaveData v2 unchanged");
        }

        private static void SilentLedgerVisitRealInnFromEmberfall(GameCore g)
        {
            g.Enter(0);
            WorldObject door=Object(g,"portal9");
            Check(PlaceWithinTalkReach(g,door),
                "A traveler must approach the existing Hearth Inn door on actual walkable ground");
            g.Talk(door);
            Check(g.save.room==9,
                "The genuine Emberfall innDoor portal physically enters the Hearth Inn map");
        }
        public static void SilentLedgerOutcomesChangeRealInnTravelerMemoryAfterPhysicalEvidence(Campaign data)
        {
            var untouched=Fresh(data);
            SilentLedgerVisitRealInnFromEmberfall(untouched);
            ReadInnNews(untouched,Object(untouched,"innGuest"));
            Check(!untouched.conversation.textEn.Contains("circled three times")&&
                !untouched.conversation.textEn.Contains("Let the children go first"),
                "Original inn traveler cannot claim an unearned archival outcome");

            for(int branch=0;branch<2;branch++)
            {
                var g=SilentLedgerStart(data);
                var north=SilentLedgerPost(g,"silentledger_record");
                var south=SilentLedgerPost(g,"silentledger_witness");
                var console=SilentLedgerPost(g,"silentledger_console");
                SilentLedgerMoveTo(g,north);g.Talk(north);
                Check(SilentLedgerChoice(g,"silentledger_record"),
                    "Real northern register must be recovered before local aftermath");
                SilentLedgerMoveTo(g,south);g.Talk(south);
                Check(SilentLedgerChoice(g,"silentledger_witness"),
                    "Real southern witness testimony is required before a branch");
                SilentLedgerMoveTo(g,console);g.Talk(console);
                Check(SilentLedgerChoice(g,branch==0?
                    "silentledger_public":"silentledger_routes"),
                    "Only a fully evidenced Citadel console selects the public or private civilian future");
                Check(g.SilentLedgerComplete&&!g.SilentLedgerActive,
                    "Inn aftermath starts only after the actual original mission completes");
                SilentLedgerVisitRealInnFromEmberfall(g);
                ReadInnNews(g,Object(g,"innGuest"));
                string en=g.conversation.textEn;
                string zh=g.conversation.text;
                Check(branch==0?
                    en.Contains("circled three times")&&
                    en.Contains("archive desk")&&
                    zh.Contains("孙女")&&
                    !en.Contains("Let the children go first"):
                    en.Contains("Let the children go first")&&
                    en.Contains("roadwardens")&&
                    zh.Contains("巡路队")&&
                    !en.Contains("circled three times"),
                    "The real inn guest remembers only the player-earned branch and its actual world consequences");
                g.Close();g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                loaded.Start();
                SilentLedgerVisitRealInnFromEmberfall(loaded);
                ReadInnNews(loaded,Object(loaded,"innGuest"));
                Check(loaded.save.version==2&&loaded.conversation.textEn.Contains(
                    branch==0?"circled three times":"Let the children go first"),
                    "An existing SaveData v2 slot preserves its own authentic inn aftermath after reload");
            }
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
