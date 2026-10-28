using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject ThornPost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing original forest waypoint "+id);
        }
        private static bool ThornAction(GameCore g,string action)
        {
            if(g.conversation==null||g.mode!="dialog")return false;
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action==action);
            if(index<0)return false;
            g.Choose(index);return true;
        }
        private static GameCore ThornReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=9;
            g.save.flags.Add("boss_1");g.Enter(1);g.enemies.Clear();
            g.projectiles.Clear();g.invulnerable=120;
            return g;
        }
        private static void KillThornWave(GameCore g)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(hit!=null,"The established actual combat death callback exists");
            int count=0;
            foreach(Enemy foe in g.enemies.FindAll(g.IsThornwatchEnemy))
            {
                foe.hp=6f;foe.stun=150f;foe.timer=150f;
                hit.Invoke(g,new object[]{foe,9000f,false,null,null});
                count++;
            }
            Check(count==2,"The authored wave has two distinct live actors");
            g.Tick(.05f,new Point());
        }
        public static void ThornwatchTwoRealWavesAndBothPermanentOutdoorOutcomes(Campaign data)
        {
            foreach(bool tune in new[]{true,false})
            {
                var g=ThornReady(data);
                var vista=ThornPost(g,"annex_vista_1");
                var camp=ThornPost(g,"annex_camp_1");
                Check(!g.GetQuest("thornwatch").locked,
                    "Original guardian victory unlocks a distinct authored forest chapter");
                g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
                Check(ThornAction(g,"thornwatch_start")&&
                    g.ThornwatchStage==1&&g.ThornwatchSurvivors==2,
                    "Overlook physically begins the first real hunter wave");
                int coins=g.save.coins,kills=g.save.kills[1];
                KillThornWave(g);
                Check(g.ThornwatchStage==2&&g.ThornwatchSurvivors==0&&
                    g.save.kills[1]==kills&&g.save.coins==coins,
                    "Two real deaths advance to camp without granting ambient loot");
                Check((g.GetQuest("thornwatch").target-
                    new Point(camp.x,camp.y)).Length<.01f,
                    "Journal gives the actual nearby camp instead of a fictional marker");
                g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
                Check(ThornAction(g,tune?"thornwatch_tune":"thornwatch_sever")&&
                    g.ThornwatchStage==3&&g.ThornwatchSurvivors==2,
                    "Camp authoritatively selects a permanent tactic and spawns a different elite wave");
                var guards=g.enemies.FindAll(g.IsThornwatchEnemy);
                Check(guards.Exists(e=>e.kind==2&&e.maxHp>=200f)&&
                    guards.Exists(e=>e.kind==0),
                    "Second wave has an actual armored elite and ordinary protector");
                foreach(Enemy enemy in guards)
                {enemy.stun=180f;enemy.timer=180f;}
                for(int i=0;i<47;i++)g.Tick(.05f,new Point());
                Check(g.hazards.Exists(h=>h.thornwatchOwned&&
                    h.timer>0f&&h.kind=="bell"),
                    "Elite emits genuine telegraphed root-pulse hazard geometry");
                KillThornWave(g);
                Check(g.ThornwatchStage==4&&g.ThornwatchSurvivors==0&&
                    !g.hazards.Exists(h=>h.thornwatchOwned),
                    "Only two confirmed elite-wave kills open the report and retire encounter hazards");
                Check(!g.ThornwatchComplete,
                    "Combat alone cannot remotely claim the source-bound civic reward");
                g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
                Check(ThornAction(g,"thornwatch_finish")&&
                    g.ThornwatchComplete&&g.ThornwatchTuned==tune&&
                    g.ThornwatchSevered!=tune,
                    "Physical final report grants one mutually exclusive permanent ending");
                Check(g.save.coins==coins+70&&
                    g.GetQuest("thornwatch").complete,
                    "First clear grants exact one-off coins and closes the real quest");
                float expected=g.Damage;
                if(!tune)Check(g.Damage>20f,
                    "Severed roots increase actual attack damage, not just dialogue");
                g.Talk(vista);
                Check(!ThornAction(g,"thornwatch_start")&&
                    !ThornAction(g,"thornwatch_finish"),
                    "Repeat conversation has no paid combat restart or duplicate reward");
                g.Close();g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.ThornwatchComplete&&
                    loaded.ThornwatchStage==0&&loaded.ThornwatchTuned==tune&&
                    Math.Abs(loaded.Damage-expected)<.01f,
                    "Original v2 slots keep the permanent build but never a live combat wave");
            }
        }
        public static void ThornwatchRejectsStaleCampChoicesAndDisappearingActors(Campaign data)
        {
            var g=ThornReady(data);
            var vista=ThornPost(g,"annex_vista_1");
            var camp=ThornPost(g,"annex_camp_1");
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            ThornAction(g,"thornwatch_start");
            var first=g.enemies.Find(g.IsThornwatchEnemy);
            var counterfeit=new Enemy(first.id,0,first.x,first.y,95);
            g.enemies.Remove(first);g.enemies.Add(counterfeit);
            g.Tick(.05f,new Point());
            Check(g.ThornwatchStage==0&&!g.ThornwatchComplete&&
                g.enemies.Contains(counterfeit),
                "Same-ID fake combatant cannot replace a vanished original guardian");
            g.enemies.Remove(counterfeit);
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            ThornAction(g,"thornwatch_start");
            KillThornWave(g);
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="thornwatch_tune");
            Check(choice>=0,"Real camp offers the root-bell split");
            g.save.x=camp.x+10f;g.Choose(choice);
            Check(g.ThornwatchStage==2&&g.ThornwatchSurvivors==0,
                "Walking away from original camp invalidates a stale battle choice");
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            Check(ThornAction(g,"thornwatch_sever")&&
                g.ThornwatchStage==3,
                "Returning to the real camp allows the alternative combat decision");
            g.Close();g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(!loaded.ThornwatchActive&&!loaded.ThornwatchComplete,
                "A mid-fight slot never resumes orphaned scripted combat actors");
        }
        public static void ThornwatchOriginalBossStillGatesFirstWave(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            var vista=ThornPost(g,"annex_vista_1");
            g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);
            Check(!ThornAction(g,"thornwatch_start")&&
                g.ThornwatchStage==0&&g.GetQuest("thornwatch").locked,
                "Original Thornwood boss remains mandatory; optional challenge never spoofs its clearance");
        }
    }
}
