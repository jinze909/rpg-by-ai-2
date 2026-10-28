using System;
namespace Starfall
{
    // Optional original Thornwood chapter: two actual combat waves separated
    // by a physical camp decision. No saved actor state, no new v2 fields.
    public sealed partial class GameCore
    {
        private int thornwatchStage,thornwatchKills,thornwatchCycle;
        private float thornwatchTime,thornwatchPulse;
        private bool thornwatchTuned;
        private Enemy thornwatchA,thornwatchB;
        public int ThornwatchStage {get{return thornwatchStage;}}
        public bool ThornwatchActive {get{return thornwatchStage>0;}}
        public float ThornwatchTime {get{return ThornwatchActive?thornwatchTime:0f;}}
        public bool ThornwatchComplete {get{return Has("thornwatch_complete");}}
        public bool ThornwatchTuned {get{return Has("thornwatch_tuned");}}
        public bool ThornwatchSevered {get{return Has("thornwatch_severed");}}
        public int ThornwatchSurvivors
        { get {int n=0;if(thornwatchA!=null&&enemies.Contains(thornwatchA)&&thornwatchA.hp>0)n++;
            if(thornwatchB!=null&&enemies.Contains(thornwatchB)&&thornwatchB.hp>0)n++;return n;} }
        public Point ThornwatchTarget
        {get{return thornwatchStage==2?new Point(61.5f,36.5f):
            new Point(65.5f,40.5f);}}
        public string ThornwatchStepText
        {get{return thornwatchStage==1?L("拦住第一波猎兵","FIRST WAVE · HOLD LINE"):
            thornwatchStage==2?L("到营地选择根钟战术","CHOOSE ROOT BELL AT CAMP"):
            thornwatchStage==3?L("击败根缚精英和护卫","DEFEAT ROOT-BOUND ELITE"):
            L("回远眺台报告战果","REPORT AT OVERLOOK");}}
        public bool IsThornwatchEnemy(Enemy e)
        {return e!=null&&thornwatchStage>0&&save.room==1&&
            (Object.ReferenceEquals(e,thornwatchA)||Object.ReferenceEquals(e,thornwatchB));}
        private void OnThornwatchEnemyDefeated(Enemy e)
        {
            if(!IsThornwatchEnemy(e))return;
            if(Object.ReferenceEquals(e,thornwatchA))thornwatchKills|=1;
            if(Object.ReferenceEquals(e,thornwatchB))thornwatchKills|=2;
        }
        private void ClearThornwatchRoster()
        {
            if(thornwatchA!=null)
            {RemoveEnemyPendingWarning(thornwatchA);enemies.Remove(thornwatchA);}
            if(thornwatchB!=null)
            {RemoveEnemyPendingWarning(thornwatchB);enemies.Remove(thornwatchB);}
            thornwatchA=thornwatchB=null;
            hazards.RemoveAll(h=>h.thornwatchOwned);
        }
        private void ResetThornwatch()
        {
            ClearThornwatchRoster();
            thornwatchStage=thornwatchKills=thornwatchCycle=0;
            thornwatchTime=thornwatchPulse=0;thornwatchTuned=false;
        }
        private bool TryThornwatchFoot(Point center,Point occupied,
            out Point result)
        {
            Point[] steps={
                new Point(-3,0),new Point(3,0),new Point(0,-3),new Point(0,3),
                new Point(-2,-2),new Point(2,-2),new Point(-2,2),new Point(2,2),
                new Point(-4,1),new Point(4,-1),new Point(-1,-4),new Point(1,4),
                new Point(-5,0),new Point(5,0),new Point(0,-5),new Point(0,5)
            };
            foreach(Point step in steps)
            {
                Point p=center+step;
                if(!CanStand(p)||!ClearAttackPath(center,p)||
                   !BossSummonHasLocalRoute(p,center)||
                   (p-Position).Length<1.1f||
                   (p-occupied).Length<1.5f||
                   (save.companion>=0&&(p-companionPosition).Length<1.2f))
                    continue;
                bool blocked=false;
                foreach(Enemy other in enemies)
                    if(other!=null&&other.hp>0&&(p-other.Position).Length<1.4f)
                    {blocked=true;break;}
                if(blocked)continue;
                result=p;return true;
            }
            result=new Point();return false;
        }
        private bool SpawnThornwatchWave(bool finalWave)
        {
            Point center=new Point(65.5f,40.5f),a,b;
            if(!TryThornwatchFoot(center,new Point(-100,-100),out a)||
                !TryThornwatchFoot(center,a,out b))return false;
            thornwatchA=CreatePlacedEnemy(++enemyId,
                finalWave?2:0,a,finalWave?240f:90f);
            thornwatchB=CreatePlacedEnemy(++enemyId,
                finalWave?0:2,b,finalWave?110f:115f);
            thornwatchA.timer=.85f;thornwatchB.timer=1.1f;
            enemies.Add(thornwatchA);enemies.Add(thornwatchB);
            thornwatchKills=0;
            effects.Add(new Effect("discover",a,.7f));
            return true;
        }
        private bool ThornwatchSource(WorldObject o,string id)
        {return o!=null&&save.room==1&&o.id==id&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);}
        private bool TalkThornwatch(WorldObject o)
        {
            if(o==null||save.room!=1)return false;
            if(o.id!="annex_vista_1"&&o.id!="annex_camp_1")return false;
            if(!ThornwatchSource(o,o.id))return true;
            if(o.id=="annex_camp_1")
            {
                if(thornwatchStage!=2)return false;
                Dialog(o.name,o.en,
                    "第一波劫掠者已经退去。守林人留下的根钟正在失声：古树把孩子回家的铃声误认为旧军队的战鼓。\n\n可以调和残存的根音，使第二轮地脉攻击慢一些；也可以斩断紧缚的根，直接对抗更密集的震荡。两条路最后会给你不同的长期能力。",
                    "The first raiders have fallen. The foresters' bell is losing its voice: the old roots mistook a homing call for a war drum.\n\nRetune the surviving bell for slower second-wave pulses, or sever the binding roots and endure a harsher fight. Each grants a different lasting ability.",
                    C("调和根钟 · 减少第二轮震荡",
                        "Retune the bell · slower ground pulses","thornwatch_tune"),
                    C("斩断根缚 · 强势突围",
                        "Sever binding roots · hard assault","thornwatch_sever"),
                    C("先观察营地","Survey the camp","close"));
                return true;
            }
            if(ThornwatchComplete)
            {
                Dialog(o.name,o.en,
                    ThornwatchTuned?
                    "根钟重新响起，守林人的孩子从此能循声回家。你在户外行动时持续获得更充足的耐力回复。首胜奖励不可重领。":
                    "纠缠的根系已经被斩开，林间道路不再吞没旅人的脚印。你在户外战斗时能稳定造成更强伤害。首胜奖励不可重领。",
                    ThornwatchTuned?
                    "The root bell rings again and lost children can follow it home. Outdoor stamina regeneration is permanently stronger. No duplicate first-clear reward.":
                    "The binding roots are cut; travelers can find the forest path again. Your outdoor attacks permanently hit harder. No duplicate first-clear reward.",
                    C("听完守林人的故事","Finish the forester's story","close"));
                return true;
            }
            if(!Has("boss_1"))
            {
                Dialog(o.name,o.en,"林地守护者仍控制着旧道。先完成荆棘密林原来的守护者战斗。",
                    "The original Thornwood guardian still holds this route. Defeat it before investigating the hidden bell.",
                    C("记住旧路","Remember the trail","close"));
                return true;
            }
            if(thornwatchStage==0)
            {
                Dialog(o.name,o.en,
                    "远眺台的树皮上刻着一条孩子寻找归家的短歌。旧军令把它改成引来猎兵的根钟。守林人请你清理两轮真实的守卫，中途返回邻近营地选择怎样处理古钟。失败可从这里重新开始。",
                    "A child's path-home song was carved in the overlook. An old order turned it into a root bell that draws armed hunters. Clear two genuine combat waves, returning to the nearby camp between them to decide the bell's fate. Failure can be retried here.",
                    C("守护归途 · 发起第一轮","Defend the trail · start first wave","thornwatch_start"),
                    C("暂不挑战","Not yet","close"));
                return true;
            }
            if(thornwatchStage==4)
            {
                Dialog(o.name,o.en,
                    thornwatchTuned?
                    "第二轮根缚精英已经被真实击败。调和后的钟声能顺着山谷送到每一个需要它的人。":
                    "第二轮根缚精英已经被真实击败。断开的树根露出了被军令掩盖的安全步道。",
                    thornwatchTuned?
                    "Both final-wave guardians fell in actual combat. The recovered bell carries homeward across the valley.":
                    "Both final-wave guardians fell in actual combat. The severed roots reveal a safe trail the old orders erased.",
                    C("交还路权 · 领取首胜奖励","Restore the trail · claim first clear",
                        "thornwatch_finish"));
                return true;
            }
            Dialog(o.name,o.en,
                "归途守望正在进行，第"+thornwatchStage+
                    "阶段，剩余"+thornwatchTime.ToString("0")+"秒。"+
                    (thornwatchStage==2?"第一轮已经获胜，请走到旁边营地决定如何敲响古钟。":
                     "真正的敌人仍在行动，必须实战击败他们。"),
                "Trail watch is active, stage "+thornwatchStage+
                    ", "+thornwatchTime.ToString("0")+" seconds remaining. "+
                    (thornwatchStage==2?"Wave one is over; return to the nearby camp to choose the bell's fate.":
                     "The enemy actors remain active; defeat them in real combat."),
                C("继续守望","Keep defending","close"),
                C("撤销本次守望","Withdraw this trial","thornwatch_abort"));
            return true;
        }
        private bool ChooseThornwatch(string action)
        {
            if(!action.StartsWith("thornwatch_",StringComparison.Ordinal))return false;
            if(save.room!=1||mode!="world"||Dying||practicing)return true;
            WorldObject source=pendingDialogueSource;
            if(source==null||pendingDialogueSourceRoom!=1||
               !SourceBelongsToActiveMap(source)||
               !HasPhysicalInteractionReach(source))return true;
            if(action=="thornwatch_start")
            {
                if(source.id!="annex_vista_1"||thornwatchStage!=0||
                   !Has("boss_1")||ThornwatchComplete||
                   FieldWorkPreventsRest||!SafeTravel)return true;
                if(!SpawnThornwatchWave(false))
                {Notify("附近没有合法的林地战场，请清理通路后重试。",
                    "No safe arena near the overlook. Clear the ground first.");return true;}
                thornwatchStage=1;thornwatchTime=135f;
                TrackQuest("thornwatch");sound="quest";revision++;
                Notify("第一轮守路猎兵袭来！实战击败两名敌人。",
                    "First wave: defeat both genuine hunters!");
                return true;
            }
            if(action=="thornwatch_abort")
            {
                if(source.id=="annex_vista_1"&&ThornwatchActive)
                {ResetThornwatch();revision++;Notify("林地守望撤销；远眺台可重新挑战。",
                    "Trail watch withdrawn. Restart at the overlook.");}
                return true;
            }
            if(action=="thornwatch_tune"||action=="thornwatch_sever")
            {
                if(source.id!="annex_camp_1"||thornwatchStage!=2||
                   thornwatchKills!=3)return true;
                bool tuned=action=="thornwatch_tune";
                if(!SpawnThornwatchWave(true))
                {Notify("根钟附近没有安全战场，暂时不能引出第二轮精英。",
                    "No safe second-wave arena. Free the paths before ringing the bell.");return true;}
                thornwatchTuned=tuned;thornwatchStage=3;
                thornwatchCycle=0;thornwatchPulse=1.8f;
                revision++;sound="quest";
                Notify(tuned?"根钟已调和：第二轮精英出场，避开根震预警。":
                    "根缚已斩断：第二轮精英出场，地脉震荡更猛烈。",
                    tuned?"Bell retuned! Avoid the marked root pulses while fighting the elite.":
                    "Bindings severed! The elite attacks with faster root tremors.");
                return true;
            }
            if(action=="thornwatch_finish")
            {
                if(source.id!="annex_vista_1"||thornwatchStage!=4||
                    thornwatchKills!=3||ThornwatchComplete||thornwatchTime<=0f)
                    return true;
                bool tuned=thornwatchTuned;
                ResetThornwatch();
                Flag("thornwatch_complete");
                Flag(tuned?"thornwatch_tuned":"thornwatch_severed");
                CreditCoins(70);CreditShards(3);AddXp(105);
                RecordMoment(tuned?
                    "你在荆棘密林抵挡两轮猎兵，调和根钟，古树终于把归家的短歌交还守林人。":
                    "你在荆棘密林抵挡两轮猎兵，斩断旧军令留在树根里的紧缚，打开新的山间安全路。",
                    tuned?
                    "You defeated two Thornwood hunter waves and retuned the root bell, returning a child's homeward song to the foresters.":
                    "You defeated two Thornwood hunter waves and severed the old war bindings, reopening a safe passage.");
                Notify(tuned?"根钟归乡：+70硬币、+3碎片、+105经验；户外耐力恢复增强。":
                    "林路解缚：+70硬币、+3碎片、+105经验；户外伤害增强。",
                    tuned?"The root bell rings: +70 coins, +3 shards, +105 XP and outdoor stamina regeneration.":
                    "The roots unbind: +70 coins, +3 shards, +105 XP and outdoor attack power.");
                revision++;sound="reward";return true;
            }
            return true;
        }
        private void TickThornwatch(float dt)
        {
            if(thornwatchStage==0)return;
            if(save.room!=1||Dying||practicing)
            {ResetThornwatch();revision++;return;}
            if(thornwatchStage==4)return;
            thornwatchTime-=dt;
            if(thornwatchTime<=0f)
            {ResetThornwatch();revision++;Notify("守望时限已到；可回远眺台重新挑战。",
                "The root bell window closed; retry at the overlook.");return;}
            if(thornwatchStage==1||thornwatchStage==3)
            {
                if((thornwatchKills&1)==0&&!enemies.Contains(thornwatchA)||
                   (thornwatchKills&2)==0&&!enemies.Contains(thornwatchB))
                {ResetThornwatch();revision++;
                 Notify("守卫异常离场，未确认胜利；可安全重试。",
                     "A guardian vanished without a confirmed defeat. Retry safely.");
                 return;}
                if(thornwatchKills==3)
                {
                    ClearThornwatchRoster();
                    thornwatchStage=thornwatchStage==1?2:4;
                    hazards.RemoveAll(h=>h.thornwatchOwned);
                    revision++;sound="quest";
                    Notify(thornwatchStage==2?
                        "第一轮已胜！到旁边真实营地选择根钟战术。":
                        "精英已真正击败！回远眺台交付归路成果。",
                        thornwatchStage==2?
                        "Wave one cleared! Choose root-bell strategy at the real nearby camp.":
                        "Elite genuinely defeated! Return to the overlook to secure the route.");
                    return;
                }
            }
            if(thornwatchStage!=3)return;
            thornwatchPulse-=dt;
            if(thornwatchPulse>0f)return;
            Point[] cardinal={new Point(1,0),new Point(0,1),
                new Point(-1,0),new Point(0,-1)};
            int preferred=thornwatchCycle++%4,safe=-1;
            for(int i=0;i<4;i++)
            {
                int idx=(preferred+i)%4;
                Point exit=Position+cardinal[idx]*1.6f;
                if(CanStand(exit)&&ClearAttackPath(Position,exit))
                {safe=idx;break;}
            }
            if(safe<0){thornwatchPulse=.75f;return;}
            float finalPhase=thornwatchA==null?1f:
                thornwatchA.hp/Math.Max(1,thornwatchA.maxHp);
            float frequency=thornwatchTuned?4.2f:3.0f;
            if(finalPhase<.5f)frequency-=.55f;
            thornwatchPulse=frequency;
            for(int i=0;i<4;i++)
            {
                if(i==safe)continue;
                Point foot=Position+cardinal[i]*1.6f;
                if(!CanStand(foot))continue;
                hazards.Add(new Hazard {position=foot,radius=.95f,
                    timer=1.45f,damage=thornwatchTuned?12f:18f,
                    kind="bell",thornwatchOwned=true});
            }
            effects.Add(new Effect("number",Position,.65f,
                L("根震！朝空白方位闪避","ROOT PULSE · OPEN EXIT")));
        }
    }
}
