using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore GlassLedgerReady(Campaign campaign)
        {
            var g=Fresh(campaign);
            g.save.flags.Add("harbor_report");
            g.save.stage=4;
            g.Enter(6);
            Check(g.save.room==6&&g.GlassLedgerUnlocked&&
                !g.GlassLedgerStarted,"A genuine opened desert, not an arbitrary flag, unlocks Ivo's inquiry");
            g.enemies.Clear();g.invulnerable=800f;g.save.energy=100f;
            return g;
        }
        private static WorldObject LedgerSite(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing physical desert investigation source "+id);
        }
        private static bool LedgerChoose(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(i<0)return false;
            g.Choose(i);return true;
        }
        private static void BeginGlassLedger(GameCore g)
        {
            WorldObject ivo=LedgerSite(g,"ivo"),post=LedgerSite(g,"glassledger_watch");
            Check(PlaceWithinTalkReach(g,ivo),"Original desert engineer Ivo is physically reachable");
            g.Talk(ivo);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                LedgerChoose(g,"glassledger_start")&&g.GlassLedgerStarted&&
                g.save.trackedQuest=="glassledger",
                "The original Ivo, not a floating menu, starts the narrative and tracks the new quest");
            Check(g.GetQuest("glassledger").steps.Count==5,
                "Five real staged investigation outcomes appear in the shipped quest journal");
            Check(PlaceWithinTalkReach(g,post),"Actual night watch post is reachable on original Glass Desert terrain");
            g.Talk(post);
            Check(LedgerChoose(g,"glassledger_begin")&&g.GlassLedgerActive&&
                g.GlassLedgerActualGuards==2&&g.GlassLedgerAlarm==0,
                "Original walkable post launches two actual enemy actors with an active sight-based alert");
        }
        private static void CollectGlassLedgerEvidence(GameCore g)
        {
            WorldObject slips=LedgerSite(g,"glassledger_seal");
            WorldObject witness=LedgerSite(g,"glassledger_witness");
            Check(PlaceWithinTalkReach(g,slips),
                "The genuine altered ration slip has walkable contact footing");
            g.Talk(slips);
            Check(LedgerChoose(g,"glassledger_copy")&&g.GlassLedgerFirstEvidence,
                "The original altered document, not an arbitrary menu, yields signed evidence");
            Check(PlaceWithinTalkReach(g,witness),
                "The worker's testimony is beyond the original stone barrier but still physically reachable");
            g.Talk(witness);
            Check(LedgerChoose(g,"glassledger_testimony")&&g.GlassLedgerSecondEvidence,
                "The second distant actual note identifies seven workers and their children");
        }
        public static void ErasedWaterLedgerNeedsRealIvoAndKeepsOldSavesCompatible(Campaign data)
        {
            var old=Fresh(data);
            Check(!old.GlassLedgerStarted&&!old.GlassLedgerUnlocked&&
                old.GetQuest("glassledger").locked,
                "An existing v2 save neither starts nor spoils the late desert chapter");
            var g=GlassLedgerReady(data);
            WorldObject watch=LedgerSite(g,"glassledger_watch"),
                letter=LedgerSite(g,"glassledger_witness");
            Check(g.CanStand(new Point(watch.x,watch.y))&&
                g.CanStand(new Point(letter.x,letter.y)),
                "Both authored remote physical documents stand on real original traversable desert ground");
            Check(PlaceWithinTalkReach(g,watch),"The remote empty watch post is reachable before Ivo's inquiry");
            g.Talk(watch);
            Check(!LedgerChoose(g,"glassledger_begin")&&!g.GlassLedgerActive,
                "Investigating without Ivo cannot spawn fabricated sentries");
            BeginGlassLedger(g);
            var first=g.enemies.FindAll(g.IsGlassLedgerGuard);
            Check(first.Count==2&&!object.ReferenceEquals(first[0],first[1])&&
                first[0].hp>0&&first[1].hp>0,
                "Both patrol enemies are distinct, genuine, live actors and not reusable IDs");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.GlassLedgerStarted&&
                !loaded.GlassLedgerActive&&loaded.GlassLedgerActualGuards==0&&
                !loaded.GlassLedgerReconstructed,
                "Old v2 reload retains the invitation without restoring stale living guards");
        }
        public static void ErasedWaterLedgerSilentDeductionYieldsOriginalIvoEndingAndRealRelief(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            {
                var g=GlassLedgerReady(data);
                BeginGlassLedger(g);
                foreach(Enemy guard in g.enemies)
                {guard.stun=100f;guard.timer=100f;}
                CollectGlassLedgerEvidence(g);
                WorldObject witness=LedgerSite(g,"glassledger_witness");
                g.Talk(witness);
                Check(LedgerChoose(g,"glassledger_truth")&&
                    g.GlassLedgerReconstructed&&g.GlassLedgerQuietRecord&&
                    !g.GlassLedgerActive&&g.GlassLedgerActualGuards==0,
                    "A correct inference from both real sources resolves a genuinely unseen, nonlethal infiltration");
                Check(g.GetQuest("glassledger").steps[3].done&&
                    g.GetQuest("glassledger").room==6,
                    "The new quest journal follows the real physical evidence and deduction state");
                g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.GlassLedgerQuietRecord&&
                    loaded.GlassLedgerReconstructed&&!loaded.GlassLedgerComplete,
                    "Old v2 retains the genuine silent result without inventing a civic decision");
                WorldObject ivo=LedgerSite(g,"ivo");
                Check(PlaceWithinTalkReach(g,ivo),"Original engineer physically hears the reconstructed truth");
                // XP can cross a level-up boundary, so compare against a
                // matched no-stealth v2 save with identical 100-XP payoff.
                var controlSave=CloneSave(g.save);
                controlSave.flags.Remove("glassledger_clean");
                var control=new GameCore(data,controlSave);
                control.Start();control.enemies.Clear();
                WorldObject otherIvo=LedgerSite(control,"ivo");
                Check(PlaceWithinTalkReach(control,otherIvo),"Matched control can talk to the same authentic Ivo");
                control.Talk(otherIvo);
                string action=outcome==0?"glassledger_public":"glassledger_protect";
                Check(LedgerChoose(control,action)&&control.GlassLedgerComplete,
                    "Ordinary evidence completion awards baseline progression without stealth mastery");
                g.Talk(ivo);
                int coins=g.save.coins,shards=g.save.shards;
                Check(LedgerChoose(g,action)&&g.GlassLedgerComplete&&
                    (outcome==0?g.GlassLedgerPublic&&!g.GlassLedgerProtected:
                        g.GlassLedgerProtected&&!g.GlassLedgerPublic)&&
                    g.save.coins==coins+60&&g.save.shards==shards+1&&
                    g.save.level==control.save.level&&
                    g.save.skillPoints==Math.Min(99,control.save.skillPoints+1),
                    "The original Ivo settles exactly one exclusive civic outcome with +1 real silent skill beyond normal XP");
                Check(g.GetQuest("glassledger").complete,
                    "The chapter completion is actually reflected in the live tracked journal");
                // The same original Hearth Inn traveler, not a copied
                // cutscene, must remember the publicly known or hidden
                // seven workers after the genuine source-bound outcome.
                SilentLedgerVisitRealInnFromEmberfall(g);
                ReadInnNews(g,Object(g,"innGuest"));
                Check(outcome==0?
                    g.conversation.textEn.Contains("fresh water roster")&&
                    !g.conversation.textEn.Contains("folded note but no names"):
                    g.conversation.textEn.Contains("folded note but no names")&&
                    !g.conversation.textEn.Contains("fresh water roster"),
                    "The actual Hearth Inn traveler recounts only the earned civic branch");
                g.Close();g.Enter(6);
                g.Talk(ivo);
                Check(!LedgerChoose(g,action)&&g.save.coins==coins+60,
                    "Repeated conversations cannot farm the same permanent reward");
                g.Close();g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.GlassLedgerComplete&&
                    (outcome==0?restored.GlassLedgerPublic:restored.GlassLedgerProtected),
                    "Both source-bound endings remain mutually exclusive and durable across v2 save/load");
                // Real relief from the original station/oasis, NOT remote
                // generic buffs; compare identical world/timing without flag.
                WorldObject service=LedgerSite(g,outcome==0?"fieldstation6":"oasis0");
                Check(PlaceWithinTalkReach(g,service),"Earned relief has original physically accessible terrain");
                var noService=CloneSave(g.save);
                noService.flags.Remove(outcome==0?"glassledger_public":"glassledger_protected");
                var controlService=new GameCore(data,noService);
                controlService.Start();controlService.enemies.Clear();
                controlService.save.x=g.save.x;controlService.save.y=g.save.y;
                g.enemies.Clear();
                if(outcome==0){g.save.heat=controlService.save.heat=80f;}
                else {g.save.energy=controlService.save.energy=12f;}
                for(int f=0;f<25;f++)
                {g.Tick(.05f,new Point());controlService.Tick(.05f,new Point());}
                Check(outcome==0?
                    g.save.heat<controlService.save.heat-3f:
                    g.save.energy>controlService.save.energy+3.5f,
                    "The earned public/private ending changes real physical safe desert service, not just a lore flag");
            }
        }
        public static void ErasedWaterLedgerWrongDeductionLocksFilesUntilBothRealGuardsFall(Campaign data)
        {
            var g=GlassLedgerReady(data);
            BeginGlassLedger(g);
            foreach(Enemy guard in g.enemies){guard.stun=100f;guard.timer=100f;}
            CollectGlassLedgerEvidence(g);
            WorldObject witness=LedgerSite(g,"glassledger_witness");
            for(int i=0;i<3;i++)
            {
                g.Talk(witness);
                Check(LedgerChoose(g,"glassledger_storm")&&
                    g.GlassLedgerAlarm==i+1&&!g.GlassLedgerReconstructed,
                    "False inference escalates live alarm without smuggling a successful reconstruction");
            }
            g.Talk(witness);
            Check(!LedgerChoose(g,"glassledger_truth")&&!g.GlassLedgerReconstructed,
                "Alarm three physically locks records before the authentic two-guard defeat");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Genuine combat lethal callback remains available");
            var guards=g.enemies.FindAll(g.IsGlassLedgerGuard);
            Check(guards.Count==2,"Both original living guards still belong to this investigation");
            hit.Invoke(g,new object[]{guards[0],99999f,false,null,null});
            g.Talk(witness);
            Check(!LedgerChoose(g,"glassledger_truth")&&!g.GlassLedgerReconstructed,
                "A single genuine defeat does not impersonate two resolved sentry lanes");
            hit.Invoke(g,new object[]{guards[1],99999f,false,null,null});
            g.Talk(witness);
            Check(LedgerChoose(g,"glassledger_truth")&&
                g.GlassLedgerReconstructed&&!g.GlassLedgerQuietRecord,
                "Two actual lethal contacts unlock the recovered original testimony without undeserved stealth mastery");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.GlassLedgerReconstructed&&
                !loaded.GlassLedgerQuietRecord,
                "The authentic combat route persists its achieved story but no false clean-run bonus");
        }
    }
}
