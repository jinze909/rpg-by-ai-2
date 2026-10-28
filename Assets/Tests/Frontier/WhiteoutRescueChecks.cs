using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore WhiteoutReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=9;
            g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            g.save.flags.Add("boss_7");
            g.Enter(7);
            Check(g.save.room==7,"Authorized mainline progress enters real Frostchime");
            g.enemies.Clear();g.projectiles.Clear();g.invulnerable=150f;
            return g;
        }
        private static WorldObject WhiteoutObject(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)if(o.id==id)return o;
            throw new Exception("Missing physically authored Whiteout object "+id);
        }
        private static bool WhiteoutChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(i<0)return false;
            g.Choose(i);return true;
        }
        private static void KillWhiteoutHunters(GameCore g)
        {
            var method=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(method!=null,"Real enemy-death pipeline exists");
            int count=0;
            foreach(Enemy e in g.enemies.FindAll(g.IsWhiteoutHunter))
            {
                e.hp=6f;e.stun=160f;e.timer=160f;
                method.Invoke(g,new object[]{e,9000f,false,null,null});count++;
            }
            Check(count==2,"Rescue confronts two distinct genuine enemies");
            g.Tick(.05f,new Point());
        }
        private static void WalkWhiteoutFollower(GameCore g)
        {
            Point[] steps={new Point(59.5f,42.5f),new Point(61.5f,42.5f),
                new Point(61.5f,40.5f),new Point(61.5f,38.5f),
                new Point(61.5f,36.5f)};
            foreach(Point site in steps)
            {
                Check(g.CanStand(site),"Escorted trail's waypoint occupies real walkable ground");
                g.save.x=site.x;g.save.y=site.y;
                for(int i=0;i<100&&g.WhiteoutStage==2&&
                    g.WhiteoutFollowerGap>1.18f;i++)
                    g.Tick(.05f,new Point());
                Check(g.WhiteoutFollowerGap<1.3f||g.WhiteoutStage==3,
                    "The survivor physically traverses walkable collision instead of teleporting");
            }
        }
        public static void WhiteoutRescueMovesRealSurvivorPastTwoHuntersAndBanksPublicShelter(Campaign data)
        {
            var g=WhiteoutReady(data);
            var lookout=WhiteoutObject(g,"whiteout_notice");
            var lost=WhiteoutObject(g,"whiteout_lost");
            var camp=WhiteoutObject(g,"annex_camp_7");
            Check(g.CanStand(new Point(lost.x,lost.y))&&
                g.CanStand(new Point(camp.x,camp.y)),
                "Lost traveler and public camp exist at usable authored Frostchime feet");
            g.save.x=lookout.x;g.save.y=lookout.y;g.Talk(lookout);
            Check(WhiteoutChoice(g,"whiteout_start")&&g.WhiteoutStage==1&&
                g.WhiteoutSeconds>=204f,
                "Original overlook begins an actual timed search, not an instant quest flag");
            Check((g.GetQuest("whiteout_rescue").target-
                new Point(lost.x,lost.y)).Length<.01f,
                "Journal really points to the lost traveler's physical authored position");
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            Check(!WhiteoutChoice(g,"whiteout_public"),
                "Camp cannot issue the reward before the traveler is recovered");
            g.save.x=lost.x;g.save.y=lost.y;g.Talk(lost);
            Check(WhiteoutChoice(g,"whiteout_pickup")&&g.WhiteoutStage==2&&
                g.WhiteoutFollowerVisible&&g.WhiteoutHuntersAlive==2,
                "Real traveler begins the physical escort and two independent hunters spawn");
            g.Close();g.save.x=camp.x;g.save.y=camp.y;
            g.Tick(.05f,new Point());
            Check(g.WhiteoutStage==2&&g.WhiteoutFollowerGap>2f,
                "Running straight to camp does not magically teleport the stranded traveler");
            g.save.x=lost.x;g.save.y=lost.y;
            KillWhiteoutHunters(g);
            Check(g.WhiteoutHuntersAlive==0&&g.WhiteoutStage==2,
                "Two confirmed kills alone do not remotely complete the escort");
            WalkWhiteoutFollower(g);
            Check(g.WhiteoutStage==3&&g.WhiteoutWarmth==0f&&
                (g.WhiteoutFollowerFoot-new Point(camp.x,camp.y)).Length<1.5f,
                "Protected NPC walks into original camp, banks safely and stops cold exposure");
            int coins=g.save.coins,shards=g.save.shards;
            g.Talk(camp);Check(WhiteoutChoice(g,"whiteout_public"),
                "The original camp offers a source-bound civilian decision");
            Check(g.WhiteoutComplete&&g.WhiteoutPublic&&!g.WhiteoutGuides&&
                g.save.coins==coins+70&&g.save.shards==shards+3&&
                !g.WhiteoutActive&&g.GetQuest("whiteout_rescue").complete,
                "True escort pays once and opens an exclusive public warming shelter");
            g.save.health=40f;g.save.energy=50f;
            for(int i=0;i<30;i++)g.Tick(.05f,new Point());
            Check(g.save.health>40f&&g.save.energy>50f,
                "The completed civilian choice changes real in-world safe camp recovery");
            g.PrepareSave();
            var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.save.version==2&&saved.WhiteoutComplete&&
                saved.WhiteoutPublic&&!saved.WhiteoutActive,
                "Original v2 save survives with only permanent civilian flags");
        }
        public static void WhiteoutRescueGustsMoonherbAndAlternativeTrailAreReal(Campaign data)
        {
            var g=WhiteoutReady(data);
            WorldObject lookout=WhiteoutObject(g,"whiteout_notice"),
                lost=WhiteoutObject(g,"whiteout_lost"),
                camp=WhiteoutObject(g,"annex_camp_7");
            g.save.x=lookout.x;g.save.y=lookout.y;g.Talk(lookout);
            WhiteoutChoice(g,"whiteout_start");
            g.save.x=lost.x;g.save.y=lost.y;g.Talk(lost);
            WhiteoutChoice(g,"whiteout_pickup");
            foreach(Enemy hunter in g.enemies.FindAll(g.IsWhiteoutHunter))
            {hunter.stun=180f;hunter.timer=180f;}
            float before=g.WhiteoutWarmth;
            g.save.x=lost.x+8f;g.save.y=lost.y;
            for(int i=0;i<151;i++)g.Tick(.05f,new Point());
            Check(g.WhiteoutStage==2&&g.WhiteoutWarmth<before-13f,
                "An exposed, distant survivor suffers real storm chill and an unsheltered gust");
            g.save.x=lost.x;g.save.y=lost.y;
            g.save.inventory[2]=2;
            g.Talk(lost);
            int herb=Array.FindIndex(g.conversation.choices,c=>c.action=="whiteout_herb");
            Check(herb>=0,"In-person source provides the optional field triage");
            g.save.x=lost.x+8f;g.Choose(herb);
            Check(g.save.inventory[2]==2&&g.WhiteoutShieldSeconds==0f,
                "Stale cached first-aid choice cannot consume remote supplies");
            g.save.x=lost.x;g.save.y=lost.y;
            g.Talk(lost);
            Check(WhiteoutChoice(g,"whiteout_herb")&&
                g.save.inventory[2]==1&&g.WhiteoutShieldSeconds>=11f&&
                g.WhiteoutWarmth>before-13f,
                "One physical Moonherb restores warmth and starts an actual timed windbreak");
            g.Talk(lost);
            Check(!WhiteoutChoice(g,"whiteout_herb")&&g.save.inventory[2]==1,
                "The active rescue cannot repeatedly farm immunity or spend twice");
            g.Close();
            KillWhiteoutHunters(g);
            WalkWhiteoutFollower(g);
            float beforeSpeed=g.Speed;
            g.Talk(camp);
            Check(WhiteoutChoice(g,"whiteout_guides")&&g.WhiteoutGuides&&
                !g.WhiteoutPublic&&g.Speed>beforeSpeed+.34f,
                "Alternate outcome increases genuine local travel speed, not just dialogue");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.WhiteoutGuides&&!restored.WhiteoutPublic&&
                restored.save.version==2,
                "Alternative mutually exclusive trail outcome survives v2 reload");
        }
        public static void WhiteoutRescueRejectsFakeEnemyOrUnfinishedBoss(Campaign data)
        {
            var g=WhiteoutReady(data);
            WorldObject lookout=WhiteoutObject(g,"whiteout_notice"),
                lost=WhiteoutObject(g,"whiteout_lost");
            g.save.flags.Remove("boss_7");
            g.save.x=lookout.x;g.save.y=lookout.y;g.Talk(lookout);
            Check(!WhiteoutChoice(g,"whiteout_start")&&g.WhiteoutStage==0,
                "Original regional Boss progression is never bypassed");
            g.save.flags.Add("boss_7");
            g.Talk(lookout);int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="whiteout_start");
            Check(start>=0,"Real overlook offers the rescue after Boss progress");
            g.save.x=lost.x;g.save.y=lost.y;g.Choose(start);
            Check(g.WhiteoutStage==0,
                "Walking away from the conversation source rejects a stale remote start");
            g.save.x=lookout.x;g.save.y=lookout.y;g.Talk(lookout);
            WhiteoutChoice(g,"whiteout_start");
            g.save.x=lost.x;g.save.y=lost.y;g.Talk(lost);
            WhiteoutChoice(g,"whiteout_pickup");
            Enemy real=g.enemies.Find(g.IsWhiteoutHunter);
            Enemy fake=new Enemy(real.id,0,real.x,real.y,96f);
            g.enemies.Remove(real);g.enemies.Add(fake);
            g.Tick(.05f,new Point());
            Check(!g.WhiteoutActive&&!g.WhiteoutComplete&&g.enemies.Contains(fake),
                "A numerically identical counterfeit cannot impersonate the true hunter's death");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(!loaded.WhiteoutActive&&!loaded.WhiteoutComplete,
                "Failed escort cannot leak an orphaned survivor into another save");
        }
    }
}
