using System;
namespace Starfall
{
    // Silent Ledger: a real non-combat infiltration with moving sentries,
    // directional line of sight, listening range, transient suspicion and
    // a one-use physical diversion. No SaveData v2 schema changes.
    public sealed partial class GameCore
    {
        private int silentLedgerStage,silentLedgerSweep;
        private float silentLedgerTime,silentLedgerSuspicion,silentLedgerDiversion;
        private bool silentLedgerDecoyUsed;
        private Point silentLedgerLastHero;
        private Point[] silentLedgerSentries=new Point[2];
        private Point[] silentLedgerFacing=new Point[2];
        private bool[] silentLedgerOutbound=new bool[2];
        private static readonly Point[] silentLedgerA={
            new Point(57.5f,39.5f),new Point(64.5f,39.5f)};
        private static readonly Point[] silentLedgerB={
            new Point(65.5f,36.5f),new Point(65.5f,42.5f)};
        public bool SilentLedgerActive {get{return silentLedgerStage>0&&save.room==4;}}
        public bool SilentLedgerComplete {get{return Has("silentledger_public")||Has("silentledger_routes");}}
        public bool SilentLedgerPublic {get{return Has("silentledger_public");}}
        public bool SilentLedgerRoutes {get{return Has("silentledger_routes");}}
        public int SilentLedgerStage {get{return SilentLedgerActive?silentLedgerStage:0;}}
        public float SilentLedgerTime {get{return SilentLedgerActive?silentLedgerTime:0;}}
        public float SilentLedgerSuspicion {get{return SilentLedgerActive?silentLedgerSuspicion:0;}}
        public float SilentLedgerDiversion {get{return SilentLedgerActive?silentLedgerDiversion:0;}}
        public bool SilentLedgerCanDivert
        {get{return SilentLedgerActive&&!silentLedgerDecoyUsed&&save.inventory[4]>0;}}
        public Point SilentLedgerSentryPosition(int index)
        {return index>=0&&index<2?silentLedgerSentries[index]:new Point();}
        public Point SilentLedgerSentryFacing(int index)
        {return index>=0&&index<2?silentLedgerFacing[index]:new Point();}
        public Point SilentLedgerTarget
        {get{return silentLedgerStage==1?new Point(60.5f,36.5f):
            silentLedgerStage==2?new Point(63.5f,43.5f):
            silentLedgerStage>=3?new Point(66.5f,38.5f):
            new Point(54.5f,38.5f);}}
        public string SilentLedgerObjective
        {get{return silentLedgerStage==1?L("取回第一份失踪平民档案","RECOVER FIRST LOST REGISTER"):
            silentLedgerStage==2?L("沿暗道取回证人口供","RECOVER THE WITNESS TESTIMONY"):
            silentLedgerStage==3?L("避开巡逻，抵达旧封存台","REACH THE SEALED CITADEL CONSOLE"):
            L("从城墙下的档案告示牌进入","READ THE CIVIC ARCHIVE NOTICE");}}
        private void ResetSilentLedger()
        {
            silentLedgerStage=silentLedgerSweep=0;
            silentLedgerTime=silentLedgerSuspicion=silentLedgerDiversion=0f;
            silentLedgerDecoyUsed=false;
            silentLedgerSentries[0]=silentLedgerA[0];
            silentLedgerSentries[1]=silentLedgerB[0];
            silentLedgerFacing[0]=new Point(1,0);
            silentLedgerFacing[1]=new Point(0,1);
            silentLedgerOutbound[0]=silentLedgerOutbound[1]=true;
            silentLedgerLastHero=new Point();
        }
        private bool SilentLedgerSource(WorldObject o,string id)
        {return o!=null&&o.id==id&&save.room==4&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);}
        private bool TalkSilentLedger(WorldObject o)
        {
            if(o==null||save.room!=4||!o.kind.StartsWith("silentledger",
                StringComparison.Ordinal))return false;
            if(!SilentLedgerSource(o,o.id))return true;
            if(o.id=="silentledger_notice")
            {
                if(!Has("boss_3"))
                {
                    Dialog(o.name,o.en,
                        "档案馆仍归旧工业驻军管理。先解决铁炉堡守护者，才能进入要塞外围。",
                        "The sealed register still answers to Ironreach's old command. Clear the original guardian first.",
                        C("了解","Understood","close"));return true;
                }
                if(SilentLedgerComplete)
                {
                    Dialog(o.name,o.en,SilentLedgerPublic?
                        "被从官方名册上擦去的平民名字已经重新公布。每个人都有权找到家人。":
                        "被改写的路籍图纸如今属于巡路居民，夜巡不再有权封锁归乡的通道。",
                        SilentLedgerPublic?
                        "The names erased from official records are publicly restored. Every family may look for their loved ones.":
                        "The altered civilian trail charts now belong to the wardens, not the night patrol.",
                        C("离开","Leave","close"));return true;
                }
                if(silentLedgerStage==0)
                {
                    Dialog(o.name,o.en,
                        "旧堡垒的名册刻意漏记了撤离的家庭。两名夜巡守卫在外墙来回走动，视线会被真实墙壁遮住。悄步移动能减轻被发现的速度；冲刺声会吸引他们。到北边取走被涂改的登记，再去南侧找目击者口供，最后潜入封存台公开真相。若被发现或倒计时结束，行动会撤回，可以免费重试。",
                        "The old citadel erased evacuated families from the register. Two night sentries patrol the outer wall and see only through actual unblocked sightlines. Quiet walking reduces exposure; sprinting attracts them. Take the tampered north register, recover the southern eyewitness testimony, then reach the sealed console. Detection or timeout cancels this attempt; retries are free.",
                        C("潜入夺回名册","Infiltrate the sealed register","silentledger_start"),
                        C("稍后再说","Not yet","close"));return true;
                }
                Dialog(o.name,o.en,
                    "巡逻仍未停下。请完成潜入或回此告示牌主动撤销；一旦消耗干扰备件不会退还。",
                    "The sentries are still patrolling. Finish the infiltration or withdraw at this notice; spent diversion gear is not refunded.",
                    C("继续潜入","Continue infiltration","close"),
                    C("撤销本次行动","Abort the attempt","silentledger_abort"));return true;
            }
            if(o.id=="silentledger_record")
            {
                Dialog(o.name,o.en,
                    silentLedgerStage==1?
                        "墙角的原始登记写着十七户被标注为「并未存在」。这些人的姓名和来处不是帝国的秘密。":
                        "纸页留下新刮擦的痕迹。你必须先从告示牌接取任务，才能取回与夜巡有关的证词。",
                    silentLedgerStage==1?
                        "The original register marks seventeen families as 'never existed.' Their names are not the citadel's property.":
                        "Freshly scraped pages remain. Start the mission at the notice first.",
                    C("收好原始名册","Secure original register","silentledger_record"),
                    C("放下纸页","Leave the papers","close"));return true;
            }
            if(o.id=="silentledger_witness")
            {
                Dialog(o.name,o.en,
                    "旧城排水口下压着目击者留下的供词。要同时持有登记与供词，才能让封存台承认事实。",
                    "An eyewitness statement is tucked beneath the old city drain. Both register and testimony are needed to unseal the truth.",
                    C("取走目击供词","Take witness testimony","silentledger_witness"),
                    C("继续观察","Observe patrol","close"));return true;
            }
            if(o.id=="silentledger_console")
            {
                if(silentLedgerStage==3)
                {
                    Dialog(o.name,o.en,
                        "封存台的铁印已经失效。登记与供词在这里重新对上了：被涂掉的不是数字，而是回家的人。你会当众开放这份平民名册，还是把未标记的安全归乡路线交给巡路队？",
                        "The console seal gives way. The register matches the eyewitness account: what the official deleted was not numbers, but people trying to get home. Publish every erased civilian name, or return the unmarked safe routes to the wardens?",
                        C("公开失踪者姓名","Open the missing-person register","silentledger_public"),
                        C("交还隐蔽归乡路线","Restore safe civilian paths","silentledger_routes"));return true;
                }
                Dialog(o.name,o.en,
                    "封存台的两枚证物槽还未全部亮起。必须亲自拿到名册与供词才能解锁。",
                    "Both physical evidence slots are not yet filled. Recover the register and testimony in person.",
                    C("检查封存状态","Inspect seal","close"));return true;
            }
            if(o.id=="silentledger_decoy")
            {
                if(silentLedgerActive&&!silentLedgerDecoyUsed)
                {
                    Dialog(o.name,o.en,
                        "废弃岗哨还连着旧警铃。用一份备用零件能把声音引向这里12秒。守卫会真正改变巡路方向，但不会凭空消失，也不能借此跳过取证。",
                        "The abandoned watch bell still works. A spare part can distract both physical patrols toward this station for 12 seconds. Their routes really change, but the evidence cannot be skipped.",
                        C("备件1 · 引开守卫","1 spare part · divert patrols","silentledger_decoy"),
                        C("保留零件","Keep supplies","close"));return true;
                }
                Dialog(o.name,o.en,
                    "旧岗哨的铜铃静了下来，只有墙砖上的旧日期还清晰可读。",
                    "The old bell has fallen silent. Only dates scratched into the wall remain.",
                    C("离开","Leave","close"));return true;
            }
            return false;
        }
        private bool ChooseSilentLedger(string action)
        {
            if(action==null||!action.StartsWith("silentledger_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            if(save.room!=4||mode!="world"||Dying||practicing||
                source==null||pendingDialogueSourceRoom!=4||
                !SilentLedgerSource(source,source.id))return true;
            if(action=="silentledger_start")
            {
                if(source.id!="silentledger_notice"||silentLedgerStage!=0||
                    SilentLedgerComplete||!Has("boss_3")||
                    FieldWorkPreventsRest||!SafeTravel)return true;
                ResetSilentLedger();
                silentLedgerStage=1;silentLedgerTime=155f;
                silentLedgerLastHero=Position;
                TrackQuest("silent_ledger");
                revision++;sound="quest";
                Notify("潜入行动开始。贴近墙壁，悄步穿过守卫真正的视线。",
                    "Infiltration begun. Quiet-step through the sentries' real sightlines.");
                return true;
            }
            if(action=="silentledger_abort")
            {
                if(source.id=="silentledger_notice"&&SilentLedgerActive)
                {ResetSilentLedger();revision++;
                 Notify("行动安全撤回。证物需要重新取得。",
                    "Mission withdrawn. Evidence must be recovered again.");}
                return true;
            }
            if(action=="silentledger_record")
            {
                if(source.id!="silentledger_record"||silentLedgerStage!=1)return true;
                silentLedgerStage=2;revision++;sound="quest";
                Notify("原始名册已取得。去南侧寻找见证人的手写供词。",
                    "Original register secured. Reach the handwritten eyewitness account to the south.");
                return true;
            }
            if(action=="silentledger_witness")
            {
                if(source.id!="silentledger_witness"||silentLedgerStage!=2)return true;
                silentLedgerStage=3;revision++;sound="quest";
                Notify("两份证据已经吻合。潜入东侧控制台公开记录。",
                    "The two accounts agree. Reach the east console before the sentries detect you.");
                return true;
            }
            if(action=="silentledger_decoy")
            {
                if(source.id!="silentledger_decoy"||!SilentLedgerActive||
                    silentLedgerDecoyUsed||save.inventory[4]<1)return true;
                save.inventory[4]--;silentLedgerDecoyUsed=true;
                silentLedgerDiversion=12f;
                silentLedgerSuspicion=Math.Max(0f,silentLedgerSuspicion-22f);
                revision++;sound="quest";
                Notify("岗哨铃声响起！守卫正转向声音，12秒内可以借机移动。",
                    "Watch bell rings! Sentries are turning toward it for 12 seconds.");
                return true;
            }
            if(action=="silentledger_public"||action=="silentledger_routes")
            {
                if(source.id!="silentledger_console"||silentLedgerStage!=3||
                    SilentLedgerComplete)return true;
                bool publicRecord=action=="silentledger_public";
                ResetSilentLedger();
                Flag(publicRecord?"silentledger_public":"silentledger_routes");
                CreditCoins(65);CreditShards(2);AddXp(105);revision++;sound="reward";
                RecordMoment(publicRecord?
                    "你在旧堡垒封存台公布了十七户失踪家庭的姓名，名册首次向普通居民开放。":
                    "你将旧堡垒封存台上被藏起的民用归乡路线交还巡路队，保护需要避开军巡的人。",
                    publicRecord?
                    "You published seventeen missing families' names in the sealed citadel archive.":
                    "You returned the hidden civilian homeward routes to the roadwardens.");
                Notify(publicRecord?
                    "失踪者名册公开！硬币+65、碎片+2、经验+105；安全档案台附近可恢复生命与耐力。":
                    "归乡路线获救！硬币+65、碎片+2、经验+105；堡垒移动速度永久+0.35。",
                    publicRecord?
                    "Missing-person register published! +65 coins, +2 shards, +105 XP; safe archive console grants local recovery.":
                    "Safe homeward routes restored! +65 coins, +2 shards, +105 XP; permanent +0.35 Citadel speed.");
                return true;
            }
            return true;
        }
        private void StepSilentLedgerSentry(int index,float dt)
        {
            Point[] ends=index==0?silentLedgerA:silentLedgerB;
            Point destination=ends[silentLedgerOutbound[index]?1:0];
            if(silentLedgerDiversion>0f)
                destination=new Point(54.5f,38.5f);
            Point current=silentLedgerSentries[index];
            Point direction=DirectionTo(current,destination);
            if((destination-current).Length<.42f)
            {silentLedgerOutbound[index]=!silentLedgerOutbound[index];return;}
            if(direction.Length<.001f)return;
            float step=Math.Min(dt*(silentLedgerDiversion>0f?2.2f:1.65f),
                (destination-current).Length);
            Point candidate=current+direction*step;
            if(CanStand(candidate)&&ClearAttackPath(current,candidate))
            {
                silentLedgerSentries[index]=candidate;
                silentLedgerFacing[index]=direction;
            }
        }
        private bool SilentLedgerSeesHero(int index)
        {
            Point sentry=silentLedgerSentries[index];
            Point offset=Position-sentry;
            float distance=offset.Length;
            if(distance<.001f)return true;
            if(distance>(QuietStepping?3.45f:6.2f)||
                !CanStand(sentry)||!CanStand(Position)||
                !ClearAttackPath(sentry,Position))return false;
            Point facing=silentLedgerFacing[index];
            return distance<=1.1f||
                (facing.x*offset.x+facing.y*offset.y)/distance>.31f;
        }
        private void TickSilentLedger(float dt)
        {
            if(silentLedgerStage==0)
            {
                if(SilentLedgerPublic&&save.room==4&&!Dying&&SafeTravel&&
                    !FieldWorkPreventsRest&&
                    PhysicalRefugeReach(new Point(66.5f,38.5f),2.7f))
                {
                    save.health=Math.Min(save.maxHealth,save.health+2.3f*dt);
                    save.energy=Math.Min(100f,save.energy+3.2f*dt);
                }
                return;
            }
            if(save.room!=4||Dying||practicing)
            {ResetSilentLedger();revision++;return;}
            silentLedgerTime-=dt;
            if(silentLedgerTime<=0f)
            {
                ResetSilentLedger();revision++;
                Notify("夜巡锁闭了整份名册。行动超时，可免费重新潜入。",
                    "Night patrol locked the register. Time expired; retry the infiltration.");
                return;
            }
            float moved=(Position-silentLedgerLastHero).Length;
            silentLedgerLastHero=Position;
            if(silentLedgerDiversion>0f)
                silentLedgerDiversion=Math.Max(0f,silentLedgerDiversion-dt);
            for(int i=0;i<2;i++)StepSilentLedgerSentry(i,dt);
            bool seen=false;
            for(int i=0;i<2;i++)
                if(SilentLedgerSeesHero(i))seen=true;
            // Actual translated feet, not a held key, produce sound.
            // Attacking makes more noise; crouch reduces visible range and
            // the rate of a real alarm without granting invisibility.
            bool loudStep=moved>dt*5.2f&&!QuietStepping;
            bool attackNoise=playerAction!=null&&!playerAction.released;
            if(seen)
                silentLedgerSuspicion+=dt*(QuietStepping?11f:25f);
            else if(loudStep)
            {
                bool heard=false;
                for(int i=0;i<2;i++)
                    if((silentLedgerSentries[i]-Position).Length<5f&&
                       ClearAttackPath(silentLedgerSentries[i],Position))
                        heard=true;
                silentLedgerSuspicion+=dt*(heard?19f:0f);
            }
            else silentLedgerSuspicion-=dt*9f;
            if(attackNoise)silentLedgerSuspicion+=dt*24f;
            silentLedgerSuspicion=Math.Max(0f,Math.Min(100f,silentLedgerSuspicion));
            if(silentLedgerSuspicion>=100f)
            {
                ResetSilentLedger();revision++;sound="alarm";
                Notify("被夜巡发现！证物未能带出，重新从告示牌潜入。",
                    "Night sentries detected you! Evidence lost; retry from the notice.");
            }
        }
    }
}
