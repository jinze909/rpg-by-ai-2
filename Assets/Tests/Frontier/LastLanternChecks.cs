using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject LanternPost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)if(o.id==id)return o;
            throw new Exception("Missing original Citadel station "+id);
        }
        private static bool LanternChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(index<0)return false;
            g.Choose(index);return true;
        }
        private static GameCore LanternReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("boss_4");g.Enter(4);
            g.enemies.Clear();g.projectiles.Clear();
            g.invulnerable=190f;
            return g;
        }
        public static void LastLanternLiveThreePostChapterHasTwoPermanentCombatPolicies(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(hit!=null,"Actual enemy lethal callback must exist");
            foreach(bool publicCall in new[]{true,false})
            {
                var g=LanternReady(data);
                WorldObject cache=LanternPost(g,"annex_cache_4_a"),
                    relay=LanternPost(g,"outer_4_1"),
                    watch=LanternPost(g,"outer_watch_4");
                Check(!g.GetQuest("last_lantern").locked&&
                    !g.GetQuest("last_lantern").complete,
                    "Defeating original Citadel guardian unlocks the new three-site chapter");
                g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);
                Check(LanternChoice(g,"lastlantern_begin")&&
                    g.LastLanternStage==1&&g.LastLanternSeconds>160f,
                    "Real original cache starts the carried distress letter");
                var firstGuide=g.GetQuest("last_lantern");
                Check(firstGuide.room==4&&
                    (firstGuide.target-new Point(relay.x,relay.y)).Length<.01f,
                    "Genuine journal leads to the existing eastern relay");
                // Choose must not allow remotely confirming a different
                // station after the player walks away.
                g.Talk(relay);
                Check(g.LastLanternStage==1,
                    "Remote relay interaction has no active world effect");
                g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
                Check(LanternChoice(g,"lastlantern_fight")&&g.LastLanternStage==2,
                    "Physical relay initiates an actual two-actor encounter");
                var foes=g.enemies.FindAll(g.IsLastLanternEnemy);
                Check(foes.Count==2&&foes[0]!=foes[1]&&
                    g.CanStand(foes[0].Position)&&g.CanStand(foes[1].Position),
                    "Two unique live interceptors spawn on walkable Citadel ground");
                var impostor=new Enemy(foes[0].id,0,foes[0].x,foes[0].y,20);
                Check(!g.IsLastLanternEnemy(impostor),
                    "A forged same-ID enemy cannot steal authored combat ownership");
                // Actor kills must be real Hit callbacks, never a counter,
                // list deletion or a fabricated successful dialogue option.
                g.Close();g.Talk(relay);
                Check(!LanternChoice(g,"lastlantern_forward"),
                    "A guarded relay cannot forward before real enemy deaths");
                g.Close();
                foreach(Enemy e in foes)
                {
                    e.stun=200f;e.timer=200f;e.hp=9f;
                    hit.Invoke(g,new object[]{e,99999f,false,null,null});
                }
                Check(g.enemies.FindAll(g.IsLastLanternEnemy).Count==0,
                    "Original player damage pipeline defeated both owned interceptors");
                g.Talk(relay);
                Check(LanternChoice(g,"lastlantern_forward")&&
                    g.LastLanternStage==3,
                    "Only confirmed live enemy defeats allow forwarding");
                var finalGuide=g.GetQuest("last_lantern");
                Check((finalGuide.target-new Point(watch.x,watch.y)).Length<.01f,
                    "Actual watchkeeper is the final quest navigation foot");
                g.save.x=watch.x;g.save.y=watch.y;g.Talk(watch);
                int beforeCoins=g.save.coins;
                Check(LanternChoice(g,publicCall?
                    "lastlantern_public":"lastlantern_silent"),
                    "Watchkeeper makes a genuine two-way final ethical choice");
                Check(!g.LastLanternActive&&g.LastLanternComplete&&
                    g.LastLanternPublic==publicCall&&g.LastLanternSilent!=publicCall&&
                    g.save.coins==beforeCoins+55,
                    "The unique end grants actual rewards and one exclusive policy");
                Check(Math.Abs(g.LastLanternPebbleCooldown-
                    (publicCall?3.5f:5f))<.01f&&
                    g.LastLanternSnareGearCost==(publicCall?1:0),
                    "Choice immediately changes the real stealth or fieldcraft costs");
                g.Talk(cache);
                Check(!LanternChoice(g,"lastlantern_begin"),
                    "Completed signal cannot be replayed for duplicate rewards");
                g.Close();g.PrepareSave();
                var reloaded=new GameCore(data,CloneSave(g.save));
                Check(reloaded.save.version==2&&reloaded.LastLanternComplete&&
                    !reloaded.LastLanternActive&&
                    reloaded.LastLanternPublic==publicCall,
                    "Older SaveData v2 resumes the chosen permanent combat outcome but no transient actors");
            }
        }
        public static void LastLanternCannotBeAbortedRemotelyOrCompletedByMissingRaiders(Campaign data)
        {
            var g=LanternReady(data);
            var cache=LanternPost(g,"annex_cache_4_a");
            var relay=LanternPost(g,"outer_4_1");
            g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);
            Check(LanternChoice(g,"lastlantern_begin"),
                "Signal begins at the proper public outer-wall cache");
            g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
            Check(LanternChoice(g,"lastlantern_fight"),
                "Original eastern lamp creates the real conflict");
            var foes=g.enemies.FindAll(g.IsLastLanternEnemy);
            Check(foes.Count==2,"Both actors initially have genuine ownership");
            var forged=new Enemy(foes[0].id,0,foes[0].x,foes[0].y,90);
            g.enemies.Add(forged);g.enemies.Remove(foes[0]);
            g.Tick(.05f,new Point());
            Check(!g.LastLanternActive&&!g.LastLanternComplete&&
                g.enemies.Contains(forged)&&!g.enemies.Contains(foes[1]),
                "Lost owned actor aborts only its own quest enemies and never grants a false victory");
            g.save.x=cache.x;g.save.y=cache.y;
            g.Talk(cache);
            Check(LanternChoice(g,"lastlantern_begin")&&g.LastLanternStage==1,
                "The same original letter can be retried after an interrupted encounter");
            g.Talk(cache);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="lastlantern_abort");
            Check(abort>=0,"The actual original source offers voluntary withdrawal");
            g.save.x=cache.x+9f;g.Choose(abort);
            Check(g.LastLanternActive,
                "A stale cached withdrawal cannot abort from across the map");
            g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);
            Check(LanternChoice(g,"lastlantern_abort")&&!g.LastLanternActive,
                "Only an authentic physically reached original cache can cancel");
        }
        public static void LastLanternTimerAndExistingV2QuestLockBehave(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(4);
            Check(g.GetQuest("last_lantern").locked,
                "Post-guardian chapter is unavailable before the original victory");
            g.save.flags.Add("boss_4");g.enemies.Clear();
            var cache=LanternPost(g,"annex_cache_4_a");
            g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);
            LanternChoice(g,"lastlantern_begin");
            Check(g.LastLanternStage==1,"After guardian clear the authentic source is ready");
            g.TrackQuest("last_lantern");
            Check(g.save.trackedQuest=="last_lantern","The standard quest journal accepts the authored chapter");
            typeof(GameCore).GetField("lastLanternSeconds",
                BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,.01f);
            g.Tick(.05f,new Point());
            Check(!g.LastLanternActive&&!g.LastLanternComplete,
                "A true expired field timer cancels the run without inventing its ending");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.trackedQuest=="last_lantern"&&
                !loaded.LastLanternActive&&!loaded.LastLanternComplete,
                "An old v2 slot retains the tracked quest but resets unsaved encounter state");
        }
    }
}
