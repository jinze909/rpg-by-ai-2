using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static bool NollChoice(GameCore g,string action)
        {
            if(g.conversation==null||g.mode!="dialog")return false;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==action){g.Choose(i);return true;}
            return false;
        }
        public static void NollThreeArrowTrainingUsesActualWarriorSkillAndOldSave(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            var noll=Object(g,"forge");
            g.save.x=noll.x;g.save.y=noll.y;
            Advance(g,1.1f);
            g.save.energy=100;
            Check(!g.GetQuest("sword_lesson").locked&&
                !g.GetQuest("sword_lesson").complete,
                "The existing warrior has access to a distinct Noll training quest");
            g.Talk(noll);
            Check(NollChoice(g,"swordlesson_start")&&g.SwordLessonActive&&
                g.SwordLessonTime==48,
                "Noll's actual existing forge launches the 48-second lesson");
            g.Enter(1);
            Check(g.save.room==0&&g.SwordLessonActive,
                "Training cannot be erased via ordinary town travel");
            g.PrepareSave();var midway=new GameCore(data,CloneSave(g.save));
            Check(!midway.SwordLessonActive&&!midway.SwordLessonComplete&&
                midway.save.version==2,
                "Mid-drill shots and clock never persist into the old v2 save format");
            int coins=g.save.coins,shards=g.save.shards;
            int reflected=0;
            for(int frame=0;frame<900&&g.SwordLessonActive;frame++)
            {
                if(g.skillCooldown<=0&&g.guardTime<.7f&&g.save.energy>=40)
                    g.Skill(new Point(1,0));
                g.Tick(.05f,new Point());
                reflected=Math.Max(reflected,g.SwordLessonHits);
            }
            Check(g.SwordLessonComplete&&!g.SwordLessonActive&&
                reflected>=2&&g.save.coins==coins+30&&g.save.shards==shards+1,
                "Three actual Q-guard contacts with simulated real arrows award exactly one successful lesson");
            Check(g.GetQuest("sword_lesson").complete,
                "The world quest journal recognizes actual achieved practice");
            g.Talk(noll);
            Check(!NollChoice(g,"swordlesson_start"),
                "Noll's original menu offers no repeatable first-clear practice reward");
            g.Close();
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.SwordLessonComplete&&loaded.save.version==2,
                "Noll's lesson first-clear flag survives unchanged v2 save reload");
            foreach(int profession in new[]{1,2})
                Check(Fresh(data,profession).GetQuest("sword_lesson").locked,
                    "Other professions do not inherit free warrior training");
        }
        public static void NollTrainingFailureRetiresOnlyOwnedRealProjectiles(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            var noll=Object(g,"forge");
            g.save.x=noll.x;g.save.y=noll.y;
            Advance(g,1.1f);
            g.invulnerable=120;
            g.Talk(noll);
            Check(NollChoice(g,"swordlesson_start"),"The first training attempt starts");
            for(int f=0;f<27;f++)g.Tick(.05f,new Point());
            Check(g.projectiles.Exists(p=>p.swordLessonOwned),
                "Noll actually fires a physically flying hostile arrow");
            var unrelated=new Projectile {position=new Point(18,14),
                velocity=new Point(),life=6,friendly=true,damage=4};
            var unrelatedFx=new Effect("spark",new Point(18,14),3);
            g.projectiles.Add(unrelated);g.effects.Add(unrelatedFx);
            g.Talk(noll);
            Check(NollChoice(g,"swordlesson_abort")&&!g.SwordLessonActive,
                "The original blacksmith physically aborts the live training");
            Check(!g.projectiles.Exists(p=>p.swordLessonOwned)&&
                !g.effects.Exists(e=>e.swordLessonOwned)&&
                g.projectiles.Contains(unrelated)&&g.effects.Contains(unrelatedFx),
                "Aborting retires only live-drill shots/effects, never unrelated world actors");
            Check(!g.SwordLessonComplete,"Canceling cannot impersonate a true three-arrow clear");
            g.Talk(noll);
            Check(NollChoice(g,"swordlesson_start"),
                "A genuine voluntary training abort can be retried");
            int cash=g.save.coins;
            // The hero faces away so no real Q reflection is possible.
            for(int i=0;i<1080&&g.SwordLessonActive;i++)g.Tick(.05f,new Point());
            Check(!g.SwordLessonActive&&!g.SwordLessonComplete&&
                g.save.coins==cash&&!g.projectiles.Exists(p=>p.swordLessonOwned),
                "Full 48s simulation timeout cannot mint rewards or leave live training arrows");
            g.PrepareSave();var saved=new GameCore(data,CloneSave(g.save));
            Check(!saved.SwordLessonActive&&saved.save.version==2,
                "Failed old-save reload never revives a spent practice timer");
        }
    }
}
