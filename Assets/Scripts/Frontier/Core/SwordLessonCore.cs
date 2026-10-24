using System;
namespace Starfall
{
    // Noll's genuine on-map archery range teaches actual Q-guard contacts.
    // Challenge state is temporary; only the first clear uses a v2 flag.
    public sealed partial class GameCore
    {
        public bool SwordLessonActive { get; private set; }
        public int SwordLessonHits { get; private set; }
        public float SwordLessonTime { get; private set; }
        private float swordLessonShotTimer;
        private bool swordLessonVeteran;
        public int SwordLessonTarget { get { return swordLessonVeteran?5:3; } }
        public bool SwordLessonVeteran { get { return SwordLessonActive&&swordLessonVeteran; } }
        public bool SwordLessonVeteranComplete { get { return Has("swordlesson_veteran"); } }
        public bool SwordLessonComplete { get { return Has("swordlesson_complete"); } }
        public static readonly Point SwordLessonLauncher=new Point(11.5f,8.5f);
        private Point NollPoint()
        {
            foreach(WorldObject o in data.maps[0].objects)
                if(o.kind=="forge")return new Point(o.x,o.y);
            return new Point(7,8);
        }

        // Three SEPARATE genuine Q contacts on an authored practice-yard
        // dummy; a Ranger volley never scores five for one physical cast.
        public bool SpecialistDrillActive{get;private set;}
        public int SpecialistDrillHits{get;private set;}
        public float SpecialistDrillTime{get;private set;}
        private int specialistDrillProfession=-1;
        private Enemy specialistDrillTarget;
        private ImpactProfile specialistDrillLastImpact;
        public bool SpecialistDrillComplete
        {get{return save.profession>0&&Has("specialist_drill_"+save.profession);}}
        public float ClassSkillEnergyCost
        {get{return (Relic(4)?50f:40f)-(SpecialistDrillComplete?5f:0f);}}
        private Point SpecialistMentorFoot()
        {
            foreach(WorldObject o in data.maps[0].objects)
                if(o.kind=="mentor")return new Point(o.x,o.y);
            return new Point(11,14);
        }
        private void ResetSpecialistDrill()
        {
            if(specialistDrillTarget!=null)enemies.Remove(specialistDrillTarget);
            specialistDrillTarget=null;specialistDrillLastImpact=null;
            SpecialistDrillActive=false;SpecialistDrillHits=0;
            SpecialistDrillTime=0;specialistDrillProfession=-1;
        }
        private bool ChooseSpecialistDrill(string action)
        {
            if(action!="specialist_drill_start"&&action!="specialist_drill_abort")return false;
            if(save.room!=0||Dying||practicing||
                (Position-SpecialistMentorFoot()).Length>1.75f)return true;
            if(action=="specialist_drill_abort")
            {
                if(SpecialistDrillActive)
                {
                    ResetSpecialistDrill();revision++;
                    Notify("艾妲收起练习靶。随时可以再试。",
                        "Ada retires the practice target. Retry whenever ready.");
                }
                return true;
            }
            if(save.profession<=0||SpecialistDrillActive||SwordLessonActive||
                FieldWorkPreventsRest||!SafeTravel||
                save.health<=10||save.energy<ClassSkillEnergyCost)return true;
            if(!CanStand(new Point(14,13))||!CanStand(new Point(16,13)))return true;
            SpecialistDrillActive=true;SpecialistDrillTime=42;
            SpecialistDrillHits=0;specialistDrillProfession=save.profession;
            specialistDrillLastImpact=null;
            save.x=14;save.y=13;skillCooldown=0;save.energy=100;
            specialistDrillTarget=new Enemy(98810+save.profession,0,16,13,20000)
                {stun=120,timer=120};
            enemies.Add(specialistDrillTarget);
            TrackQuest("specialist_drill");revision++;sound="quest";
            Notify(save.profession==1?
                "游侠：让三次分别释放的Q箭雨真正命中练习靶。":
                "法师：用三次独立施放的Q星火命中练习靶。",
                save.profession==1?
                "Ranger: land three distinct Q volleys, not five arrows from one cast.":
                "Arcanist: land three separately cast Q bursts on the target.");
            return true;
        }
        private void TickSpecialistDrill(float dt)
        {
            if(!SpecialistDrillActive)return;
            if(save.room!=0||Dying||practicing||
                save.profession!=specialistDrillProfession||
                (Position-SpecialistMentorFoot()).Length>7.5f||
                specialistDrillTarget==null||!enemies.Contains(specialistDrillTarget)||
                specialistDrillTarget.hp<=0)
            {
                ResetSpecialistDrill();revision++;
                Notify("已离开训练场，考核结束。",
                    "Leaving the practice yard ends the drill safely.");
                return;
            }
            SpecialistDrillTime-=dt;
            if(SpecialistDrillTime<=0)
            {
                ResetSpecialistDrill();revision++;
                Notify("时间到了。三次Q都必须真正打中靶子。",
                    "Time expired. All three Q casts must really hit.");
            }
        }
        private void OnSpecialistDrillHit(Enemy target,ImpactProfile impact,bool technique)
        {
            if(!SpecialistDrillActive||!technique||impact==null||
                !impact.playerOwned||
                impact.profession!=specialistDrillProfession||
                !Object.ReferenceEquals(target,specialistDrillTarget)||
                Object.ReferenceEquals(impact,specialistDrillLastImpact))return;
            specialistDrillLastImpact=impact;SpecialistDrillHits++;revision++;
            if(SpecialistDrillHits<3)
            {
                Notify("实际命中 "+SpecialistDrillHits+"/3，等待下一次Q。",
                    "Real contacts "+SpecialistDrillHits+"/3. Ready the next Q.");
                return;
            }
            int profession=specialistDrillProfession;
            bool first=!Has("specialist_drill_"+profession);
            ResetSpecialistDrill();
            if(first)
            {
                Flag("specialist_drill_"+profession);CreditCoins(35);AddXp(60);
                RecordMoment(profession==1?
                    "游侠在艾妲的靶场用三次箭雨真正命中目标，改良了战技的耐力消耗。":
                    "法师在艾妲的靶场用三次星火真正命中目标，改良了战技的耐力消耗。",
                    profession==1?
                    "Three real volleys struck Ada's practice target, refining the ranger's energy use.":
                    "Three real arcane bursts struck Ada's target, refining the arcanist's energy use.");
                Notify("职业考核通过：硬币+35、经验+60，Q永久节省5耐力。",
                    "Class trial cleared: +35 coins, +60 XP and permanent Q cost -5.");
            }
            else Notify("实战复练完成！不重复领取奖励。",
                "Proficiency replay complete. No duplicate reward.");
            sound="reward";revision++;
        }
        private void ResetSwordLesson()
        {
            SwordLessonActive=false;SwordLessonHits=0;SwordLessonTime=0;
            swordLessonShotTimer=0;swordLessonVeteran=false;
            projectiles.RemoveAll(p=>p.swordLessonOwned);
            effects.RemoveAll(e=>e.swordLessonOwned);
        }
        private bool ChooseSwordLesson(string action)
        {
            if(action!="swordlesson_start"&&action!="swordlesson_veteran_start"&&
                action!="swordlesson_abort")return false;
            if(save.room!=0||Dying||(Position-NollPoint()).Length>1.8f)return true;
            if(action=="swordlesson_abort")
            {
                if(SwordLessonActive)
                {
                    ResetSwordLesson();revision++;
                    Notify("诺尔已经收起练习箭。随时可以重新开始。",
                        "Noll has put away the arrows. Retry whenever ready.");
                }
                return true;
            }
            bool veteran=action=="swordlesson_veteran_start";
            if(SwordLessonActive||(!veteran&&SwordLessonComplete)||
                (veteran&&!SwordLessonComplete)||save.profession!=0||
                save.weapon!=0||practicing||FieldWorkPreventsRest||
                !SafeTravel||save.energy<40||save.health<=15)return true;
            SwordLessonActive=true;SwordLessonHits=0;swordLessonVeteran=veteran;
            SwordLessonTime=veteran?42:48;swordLessonShotTimer=1.1f;
            revision++;sound="quest";TrackQuest("sword_lesson");
            Notify(veteran?
                "五箭精英考核开始！42秒内正面弹回五支真正的箭矢。":
                "诺尔的三箭试炼：面对东侧发箭器，按Q举剑盾，弹回三支真正的箭。",
                veteran?
                "Advanced course: reflect five actual arrows within 42 seconds.":
                "Noll's drill: face the eastern launcher and Q-guard to reflect three real arrows.");
            return true;
        }
        private void TickSwordLesson(float dt)
        {
            if(!SwordLessonActive)return;
            if(save.room!=0||Dying||practicing||
                save.profession!=0||save.weapon!=0||
                (Position-NollPoint()).Length>6.5f)
            {
                ResetSwordLesson();revision++;
                Notify("已经离开练习范围。试炼已安全结束。",
                    "You left Noll's practice area. The drill ended safely.");
                return;
            }
            SwordLessonTime-=dt;swordLessonShotTimer-=dt;
            if(SwordLessonTime<=0)
            {
                ResetSwordLesson();revision++;
                Notify("三箭试炼未通过。调整Q技能的时机与朝向后可免费重试。",
                    "Time expired. Adjust your Q timing and facing; retries are free.");
                return;
            }
            if(swordLessonShotTimer>0)return;
            swordLessonShotTimer=swordLessonVeteran?2.35f:2.8f;
            Point launcher=SwordLessonLauncher;
            Point toward=(Position-launcher).Normalized;
            if(toward.Length<.1f)return;
            projectiles.Add(new Projectile{
                position=launcher,velocity=toward*6.5f,life=2.5f,
                damage=5,friendly=false,source="shot",
                visual="enemyArrow",swordLessonOwned=true
            });
            effects.Add(new Effect("release",launcher,.2f)
                {variant=1,direction=toward,swordLessonOwned=true});
            sound="arrow";revision++;
        }
        private void CountSwordLessonDeflection(Projectile shot)
        {
            if(!SwordLessonActive||shot==null||!shot.swordLessonOwned||
                (SwordLessonComplete&&!swordLessonVeteran))return;
            SwordLessonHits++;
            if(SwordLessonHits<SwordLessonTarget)
            {
                revision++;
                Notify("正面弹反成功 "+SwordLessonHits+"/"+SwordLessonTarget+"，继续看准来箭。",
                    "Frontal reflection "+SwordLessonHits+"/"+SwordLessonTarget+". Watch the next arrow.");
                return;
            }
            bool veteran=swordLessonVeteran;
            ResetSwordLesson();
            if(veteran)
            {
                if(!SwordLessonVeteranComplete)
                {
                    Flag("swordlesson_veteran");
                    CreditSkillPoints(1);AddXp(75);
                    RecordMoment("诺尔的五箭精英考核完成。金发冒险者连续正面弹回五支真正的练习箭，剑盾技术被写入公共训练手册。",
                        "The hero reflected five real arrows in Noll's advanced course, preserving the technique for public sword-guard training.");
                    Notify("五箭精英训练首通：技能点+1、经验+75！今后可复练，但不重复奖励。",
                        "Five-arrow first clear: +1 talent, +75 XP! Replays award nothing.");
                }
                else Notify("五箭精英复练完成！本次没有重复奖励。",
                    "Advanced drill replay complete! No additional reward.");
                revision++;sound="reward";return;
            }
            Flag("swordlesson_complete");
            CreditCoins(30);CreditShards(1);AddXp(50);
            RecordMoment("诺尔的三箭剑盾试炼完成。金发冒险者把三支真实飞来的箭矢安全弹回，铁匠把新技法写进城镇训练手册。",
                "The hero reflected three real arrows at Noll's forge. The sword-guard technique enters the town's training handbook.");
            revision++;sound="reward";
            Notify("三箭弹反通过：硬币+30、碎片+1、经验+50。以后不会重复领取奖励。",
                "Three-arrow lesson cleared: +30 coins, +1 shard, +50 XP. First-clear reward only.");
        }
    }
}
