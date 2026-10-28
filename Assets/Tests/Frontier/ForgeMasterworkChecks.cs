using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static int ForgeOption(GameCore g,string id)
        {
            return g.conversation==null?-1:Array.FindIndex(
                g.conversation.choices,c=>c.action==id);
        }
        private static GameCore PreparedForge(Campaign data,int profession)
        {
            var g=Fresh(data,profession);g.enemies.Clear();
            g.save.flags.Add("ally_3");g.save.stage=2;
            g.save.inventory[0]=15;g.save.inventory[1]=15;
            g.save.inventory[4]=15;g.save.energy=100;
            WorldObject noll=Object(g,"forge");
            g.save.x=noll.x;g.save.y=noll.y;
            return g;
        }
        public static void ForgeMasterworkChargesRealMaterialsAndChangesEachClassSkill(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            for(int variant=0;variant<2;variant++)
            {
                var g=PreparedForge(data,profession);
                WorldObject noll=Object(g,"forge");
                string command=variant==0?"forge_skill_quick":"forge_skill_weight";
                Check(g.ForgeMasterworkUnlocked&&g.ForgeMasterwork==0,
                    "A completed town pump and two delivered cores make a smith upgrade available");
                var direct=typeof(GameCore).GetMethod("ChooseForgeMasterwork",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                direct.Invoke(g,new object[]{command});
                Check(g.ForgeMasterwork==0,
                    "A source-less forged command never buys class skill power");
                g.Talk(noll);
                Check(ForgeOption(g,"forge_skill_quick")>=0&&
                    ForgeOption(g,"forge_skill_weight")>=0,
                    "Real original blacksmith offers two exclusive calibrations");
                int wood=g.save.inventory[0],crystal=g.save.inventory[1],
                    parts=g.save.inventory[4];
                int choice=ForgeOption(g,command);
                g.save.x=noll.x+8f;g.Choose(choice);
                Check(g.ForgeMasterwork==0&&g.save.inventory[1]==crystal,
                    "Walking away from Noll prevents distant spending");
                g.save.x=noll.x;g.save.y=noll.y;g.Talk(noll);
                g.Choose(ForgeOption(g,command));
                Check(g.ForgeMasterwork==variant+1&&
                    g.save.inventory[0]==wood-(variant==0?2:1)&&
                    g.save.inventory[1]==crystal-(variant==0?2:4)&&
                    g.save.inventory[4]==parts-(variant==0?2:3),
                    "Actual choice spends one exact, asymmetric materials recipe");
                g.Talk(noll);
                Check(ForgeOption(g,"forge_skill_quick")<0&&
                    ForgeOption(g,"forge_skill_weight")<0,
                    "Only one permanent calibration can be purchased");
                g.Close();g.save.energy=100;g.skillCooldown=0;
                g.Skill(new Point(1,0));
                Check(g.playerAction!=null&&
                    Math.Abs(g.skillCooldown-(variant==0?2.65f:4.2f))<.01f,
                    "Original Q skill uses the new real combat cooldown");
                var unmodified=PreparedForge(data,profession);
                unmodified.Skill(new Point(1,0));
                Check(unmodified.playerAction!=null&&
                    Math.Abs(g.playerAction.damage-
                        unmodified.playerAction.damage*(variant==0?1f:1.3f))<.01f,
                    "Warrior, ranger and mage capture the upgraded skill damage at release");
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.ForgeMasterwork==variant+1&&
                    Math.Abs(loaded.ForgeSkillCooldown-(variant==0?2.65f:4.2f))<.01f&&
                    Math.Abs(loaded.ForgeSkillPower-(variant==0?1f:1.3f))<.01f,
                    "Original v2 save slot retains the calibration, no schema extension");
            }
        }
        public static void ForgeMasterworkRejectsPrematureUnpaidAndUnsafeCrafting(Campaign data)
        {
            var g=PreparedForge(data,0);var noll=Object(g,"forge");
            g.save.stage=1;g.Talk(noll);
            Check(ForgeOption(g,"forge_skill_quick")<0,
                "Story progress gates advanced class skill calibration");
            g.save.stage=2;g.Talk(noll);
            int c=ForgeOption(g,"forge_skill_quick");
            Check(c>=0,"An eligible actual smith offers a quick regulator");
            g.save.inventory[1]=1;int wood=g.save.inventory[0];
            g.Choose(c);
            Check(g.ForgeMasterwork==0&&g.save.inventory[0]==wood&&
                g.save.inventory[1]==1,
                "Lacking crystals preserves wood and the exclusive decision");
            g.save.inventory[1]=15;g.Talk(noll);
            var enemy=new Enemy(97991,0,noll.x+.5f,noll.y,65);
            g.enemies.Add(enemy);
            g.Choose(ForgeOption(g,"forge_skill_weight"));
            Check(g.ForgeMasterwork==0&&g.save.inventory[0]==wood,
                "Local combat prevents unsafe forge construction");
            g.enemies.Remove(enemy);g.Talk(noll);
            g.Choose(ForgeOption(g,"forge_skill_weight"));
            Check(g.ForgeMasterwork==2,
                "Once safe, original speaker permits funded real combat upgrade");
        }
        public static void ForgeMasterworkQuestIsGroundedInActualTownAndTracksTheBuild(Campaign data)
        {
            var g=Fresh(data);
            var locked=g.GetQuest("forge_masterwork");
            Check(locked.locked&&!locked.complete&&locked.steps.Count==3,
                "Novice save sees prerequisites without a fabricated craft unlock");
            g.save.flags.Add("ally_3");
            g.save.stage=2;g.save.inventory[0]=10;g.save.inventory[1]=10;
            g.save.inventory[4]=10;
            var noll=Object(g,"forge");
            var available=g.GetQuest("forge_masterwork");
            Check(!available.locked&&!available.complete&&available.room==0&&
                (available.target-new Point(noll.x,noll.y)).Length<.01f,
                "Unlocked journal routes to Noll's original physical forge, not a fake mission marker");
            g.TrackQuest("forge_masterwork");
            Check(g.save.trackedQuest=="forge_masterwork",
                "The original quest tracking system accepts the actual blacksmith task");
            g.save.x=noll.x;g.save.y=noll.y;g.Talk(noll);
            g.Choose(ForgeOption(g,"forge_skill_quick"));
            var finished=g.GetQuest("forge_masterwork");
            Check(finished.complete&&finished.Progress==1f&&
                finished.next.Contains("2.65"),
                "A physical paid regulator marks the journal complete with the real cooldown");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.trackedQuest=="forge_masterwork"&&
                restored.GetQuest("forge_masterwork").complete,
                "Old v2 save reload retains the tracked blacksmith quest and final selected outcome");
        }
    }
}
