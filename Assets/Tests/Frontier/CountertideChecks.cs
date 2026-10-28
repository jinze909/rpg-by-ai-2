using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject CountertidePost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing original Mistport channel station "+id);
        }
        private static bool CountertideChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action==action);
            if(index<0)return false;
            g.Choose(index);
            return true;
        }
        private static GameCore CountertideReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("boss_5");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("exp_intro");g.save.flags.Add("gate_5");
            g.Enter(5);g.enemies.Clear();g.projectiles.Clear();
            g.save.tideHeldUntil=0f;
            return g;
        }
        private static void PlayCountertidePhysicalScore(GameCore g)
        {
            WorldObject vista=CountertidePost(g,"annex_vista_5"),
                camp=CountertidePost(g,"annex_camp_5"),
                cache=CountertidePost(g,"annex_cache_5_b");
            g.save.totalTime=1f;
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            Check(CountertideChoice(g,"countertide_begin")&&
                g.CountertideStage==1&&g.CountertideRemaining>=259f,
                "Existing lookout physically begins the real timed signal");
            Check((g.GetQuest("countertide").target-
                new Point(camp.x,camp.y)).Length<.01f,
                "Canonical journal navigates to the authored camp first");
            g.save.x=camp.x;g.save.y=camp.y;
            Check(g.CanStand(g.Position)&&g.LowTide,
                "First signal is accessible on real walkable ground at ebb");
            g.Talk(camp);
            Check(CountertideChoice(g,"countertide_note_2")&&
                g.CountertideStage==2,
                "Exact two-bell code at physically reached low-tide camp advances");
            g.save.x=cache.x;g.save.y=cache.y;
            g.save.totalTime=30f;
            Check(g.CanStand(g.Position)&&!g.LowTide,
                "Original old cache is walkable during the real high-tide phase");
            g.Talk(cache);
            Check(CountertideChoice(g,"countertide_note_3")&&
                g.CountertideStage==3,
                "Exact three-bell call at the high-tide cache advances");
            g.save.x=vista.x;g.save.y=vista.y;
            g.save.totalTime=37f;
            Check(g.LowTide&&g.CanStand(g.Position),
                "The physical lookout remains accessible for the next ebb");
            g.Talk(vista);
            Check(CountertideChoice(g,"countertide_note_1")&&
                g.CountertideStage==4,
                "Final one-bell homing call requires a genuine low-tide lookout visit");
            Check(!g.CountertideComplete,
                "Tuning all three signals does not auto-claim a civilian policy");
        }
        public static void CountertideRealThreeStationCodeGrantsBothDistinctMapRules(Campaign data)
        {
            foreach(bool publish in new[]{true,false})
            {
                GameCore g=CountertideReady(data);
                float speed=g.Speed;
                PlayCountertidePhysicalScore(g);
                WorldObject vista=CountertidePost(g,"annex_vista_5");
                g.Talk(vista);
                int coins=g.save.coins;
                Check(CountertideChoice(g,publish?
                    "countertide_channels":"countertide_refuge"),
                    "Civilian policy must be selected from the genuine original lookout");
                Check(g.CountertideComplete&&g.CountertideChannels==publish&&
                    g.CountertideRefuge!=publish&&g.CountertideStage==0&&
                    g.save.coins==coins+65,
                    "One exclusive policy, a genuinely closed mission and one first-clear award");
                g.save.totalTime=24f;g.save.tideHeldUntil=0f;
                Check(g.LowTide==publish,
                    "Published channel extends the REAL water collision window from 22s to 28s");
                g.save.totalTime=29f;
                Check(!g.LowTide,
                    "Even a public channel does not turn the entire flood cycle into permanent ebb");
                Check(Math.Abs(g.Speed-speed-(publish?0f:.55f))<.001f,
                    "Refuge choice changes actual Mistport locomotion, not only dialogue");
                g.Talk(vista);
                Check(!CountertideChoice(g,"countertide_begin")&&
                    !CountertideChoice(g,"countertide_refuge")&&
                    !CountertideChoice(g,"countertide_channels"),
                    "Completed civilians cannot replay the signal or duplicate a competing ending");
                g.Close();g.PrepareSave();
                GameCore reload=new GameCore(data,CloneSave(g.save));
                Check(reload.save.version==2&&reload.CountertideComplete&&
                    reload.CountertideStage==0&&
                    reload.CountertideChannels==publish&&
                    reload.CountertideRefuge!=publish,
                    "Old v2 slots preserve public or private policy while active calibration resets");
            }
        }
        public static void CountertideRejectsWrongTideWrongNotesAndRemoteDialogue(Campaign data)
        {
            GameCore g=CountertideReady(data);
            WorldObject vista=CountertidePost(g,"annex_vista_5"),
                camp=CountertidePost(g,"annex_camp_5"),
                cache=CountertidePost(g,"annex_cache_5_b");
            g.save.x=vista.x;g.save.y=vista.y;
            g.Talk(vista);CountertideChoice(g,"countertide_begin");
            g.save.x=camp.x;g.save.y=camp.y;g.save.totalTime=30f;
            Check(!g.LowTide,"Camp is at the wrong real tide");
            g.Talk(camp);CountertideChoice(g,"countertide_note_2");
            Check(g.CountertideStage==1,
                "Correct frequency alone cannot bypass an incorrect tide");
            g.save.totalTime=1f;
            g.Talk(camp);
            int stale=Array.FindIndex(g.conversation.choices,
                c=>c.action=="countertide_note_2");
            Check(stale>=0,"Original camp offers physically grounded tuning");
            g.save.x=camp.x+11f;g.Choose(stale);
            Check(g.CountertideStage==1,
                "A cached menu cannot send a signal from eleven tiles away");
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            float before=g.CountertideRemaining;
            CountertideChoice(g,"countertide_note_3");
            Check(g.CountertideStage==1&&g.CountertideMistakes==1&&
                Math.Abs(g.CountertideRemaining-(before-12f))<.01f,
                "A wrong note costs actual mission time without advancing a nonexistent station");
            g.Talk(camp);CountertideChoice(g,"countertide_note_2");
            g.save.x=cache.x;g.save.y=cache.y;
            g.save.totalTime=5f;
            Check(g.LowTide,"Old cache is still at ebb");
            g.Talk(cache);CountertideChoice(g,"countertide_note_3");
            Check(g.CountertideStage==2,
                "High-water signal cannot be faked at low water");
            g.save.totalTime=31f;g.Talk(cache);
            Check(CountertideChoice(g,"countertide_note_3")&&
                g.CountertideStage==3,
                "A genuinely high tide now permits the next signal");
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            CountertideChoice(g,"countertide_note_1");
            Check(g.CountertideStage==3,
                "The last code still requires a fresh, actual ebb");
        }
        public static void CountertideRejectsLockedStartAndResetsExpiredV2Runs(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("exp_intro");g.save.flags.Add("gate_5");
            g.Enter(5);
            g.enemies.Clear();
            WorldObject vista=CountertidePost(g,"annex_vista_5");
            g.save.x=vista.x;g.save.y=vista.y;
            g.Talk(vista);
            Check(!CountertideChoice(g,"countertide_begin")&&
                g.GetQuest("countertide").locked,
                "Original boss and harbor-report prerequisites remain meaningful");
            g.Close();g.save.flags.Add("boss_5");g.save.flags.Add("harbor_report");
            g.Talk(vista);
            Check(CountertideChoice(g,"countertide_begin"),
                "Restored prerequisites enable civilian field calibration");
            float before=g.CountertideRemaining;
            g.Tick(.05f,new Point());
            Check(g.CountertideRemaining<before,
                "Clock advances on genuine live world frames, not dialog clicks");
            g.TrackQuest("countertide");
            g.PrepareSave();
            GameCore reload=new GameCore(data,CloneSave(g.save));
            Check(reload.save.version==2&&
                reload.save.trackedQuest=="countertide"&&
                !reload.CountertideActive,
                "Tracked chapter survives an old save, but temporary signal never does");
            // Real ticking and a bounded mission clock cancel without award.
            var timer=typeof(GameCore).GetField("countertideRemaining",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            timer.SetValue(g,.02f);
            g.Tick(.05f,new Point());
            Check(!g.CountertideActive&&!g.CountertideComplete,
                "A truly expired signal cannot create a permanent policy");
        }
        public static void CountertidePhysicalCrystalReinforcementTradesSuppliesForRealTime(Campaign data)
        {
            GameCore g=CountertideReady(data);
            WorldObject vista=CountertidePost(g,"annex_vista_5");
            WorldObject camp=CountertidePost(g,"annex_camp_5");
            g.save.totalTime=2f;g.save.x=vista.x;g.save.y=vista.y;
            g.Talk(vista);CountertideChoice(g,"countertide_begin");
            g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);CountertideChoice(g,"countertide_note_2");
            Check(g.CountertideStage==2,
                "A real first low-tide camp call is required before reinforcement");
            var timer=typeof(GameCore).GetField("countertideRemaining",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            timer.SetValue(g,150f);
            int crystal=g.save.inventory[1];
            g.Talk(camp);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="countertide_stabilize");
            if(crystal<1)
            {
                g.Close();g.save.inventory[1]=2;crystal=2;
                g.Talk(camp);
                option=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="countertide_stabilize");
            }
            Check(option>=0,"Physical camp offers a bounded material-paid extension");
            g.save.x=camp.x+9f;g.Choose(option);
            Check(!g.CountertideStabilized&&
                Math.Abs(g.CountertideRemaining-150f)<.01f&&
                g.save.inventory[1]==crystal,
                "Cached camp dialogue may not buy a remote signal extension");
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            Check(CountertideChoice(g,"countertide_stabilize")&&
                g.CountertideStabilized&&
                Math.Abs(g.CountertideRemaining-195f)<.01f&&
                g.save.inventory[1]==crystal-1,
                "One physically grounded crystal purchase extends the authentic timer exactly 45s");
            g.Talk(camp);
            Check(!CountertideChoice(g,"countertide_stabilize"),
                "A regulator cannot be bought twice in one live run");
            WorldObject cache=CountertidePost(g,"annex_cache_5_b");
            g.save.x=cache.x;g.save.y=cache.y;
            g.save.totalTime=30f;g.Talk(cache);
            float remaining=g.CountertideRemaining;
            CountertideChoice(g,"countertide_note_2");
            Check(g.CountertideMistakes==1&&
                Math.Abs(g.CountertideRemaining-(remaining-6f))<.01f,
                "Paid crystal protection halves future wrong-note time penalties");
            g.Close();g.PrepareSave();
            GameCore reload=new GameCore(data,CloneSave(g.save));
            Check(reload.save.version==2&&
                !reload.CountertideActive&&!reload.CountertideStabilized,
                "Temporary paid regulator is not serialized into old save slots");
        }
        public static void CountertideActualTideTurnTelegraphsAvoidableSurgeAndCrystalGroundsIt(Campaign data)
        {
            GameCore g=CountertideReady(data);
            WorldObject vista=CountertidePost(g,"annex_vista_5");
            WorldObject camp=CountertidePost(g,"annex_camp_5");
            g.save.totalTime=21.90f;g.save.x=vista.x;g.save.y=vista.y;
            Check(g.LowTide&&g.CanStand(g.Position),
                "Authentic lookout is a walkable low-tide source");
            g.Talk(vista);
            Check(CountertideChoice(g,"countertide_begin"),
                "Physical beacon starts before real flood change");
            Check(!g.hazards.Exists(h=>h.countertideOwned),
                "Starting an instrument cannot fake a tide-contact hazard");
            g.save.totalTime=21.99f;
            g.Tick(.05f,new Point());
            var surge=g.hazards.Find(h=>h.countertideOwned);
            Check(!g.LowTide&&surge!=null&&
                surge.kind=="tide"&&surge.timer>1.2f&&
                surge.damage==10f&&
                (surge.position-g.Position).Length<.01f,
                "The actual low-to-high transition leaves a delayed physical warning at hero feet");
            // The tide warning has room to expire and should not corrupt
            // the chapter or kill another encounter's independent hazards.
            g.save.totalTime=37f;g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);
            Check(CountertideChoice(g,"countertide_note_2")&&
                g.CountertideStage==2,
                "Moving to first authentic ebb camp can still advance the multi-stage puzzle");
            var timer=typeof(GameCore).GetField("countertideRemaining",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            timer.SetValue(g,150f);
            g.save.inventory[1]=2;
            g.Talk(camp);
            Check(CountertideChoice(g,"countertide_stabilize")&&
                g.CountertideStabilized&&g.save.inventory[1]==1&&
                !g.hazards.Exists(h=>h.countertideOwned),
                "Only an authentic paid camp intervention clears a dangerous tide warning");
            g.Tick(.05f,new Point());
            g.save.totalTime=57.99f;
            g.Tick(.05f,new Point());
            Check(!g.LowTide&&!g.hazards.Exists(h=>h.countertideOwned),
                "Paid crystal dampens a later REAL tide transition instead of spawning remote hazards");
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            Check(CountertideChoice(g,"countertide_abort")&&
                !g.CountertideActive&&!g.hazards.Exists(h=>h.countertideOwned),
                "Physically withdrawing from the original lookout retires only owned transient surges");
        }
    }
}
