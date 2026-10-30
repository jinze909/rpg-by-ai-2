using System;
namespace Starfall
{
    // Silent Ledger: a real non-combat infiltration with moving sentries,
    // directional line of sight, listening range, transient suspicion and
    // a one-use physical diversion. No SaveData v2 schema changes.
    public sealed partial class GameCore
    {
        private int silentLedgerStage,silentLedgerSweep;
        private float silentLedgerTime,silentLedgerSuspicion,silentLedgerDiversion,silentLedgerDarkness;
        private bool silentLedgerShutterUsed;
        private bool silentLedgerDecoyUsed;
        private Point silentLedgerLastHero;
        private Point[] silentLedgerSentries=new Point[2];
        private Point[] silentLedgerFacing=new Point[2];
        private float[] silentLedgerFootDistance=new float[2];
        private float[] silentLedgerFootStride=new float[2];
        private float[] silentLedgerScanClock=new float[2];
        private int[] silentLedgerScanTurns=new int[2];
        private bool[] silentLedgerOutbound=new bool[2];
        private Point[] silentLedgerSearchTarget=new Point[2];
        private float[] silentLedgerSearchSeconds=new float[2];
        private bool[] silentLedgerSoundSearch=new bool[2];
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
        public float SilentLedgerDarkness {get{return SilentLedgerActive?silentLedgerDarkness:0;}}
        public bool SilentLedgerShutterUsed {get{return SilentLedgerActive&&silentLedgerShutterUsed;}}
        public bool SilentLedgerCanDivert
        {get{return SilentLedgerActive&&!silentLedgerDecoyUsed&&save.inventory[4]>0;}}
        public Point SilentLedgerSentryPosition(int index)
        {return index>=0&&index<2?silentLedgerSentries[index]:new Point();}
        public Point SilentLedgerSentryFacing(int index)
        {return index>=0&&index<2?silentLedgerFacing[index]:new Point();}
        public float SilentLedgerSentryFootDistance(int index)
        {return SilentLedgerActive&&index>=0&&index<2?silentLedgerFootDistance[index]:0f;}
        public bool SilentLedgerSentryMoving(int index)
        {return SilentLedgerActive&&index>=0&&index<2&&silentLedgerFootStride[index]>.001f;}
        public float SilentLedgerSentrySearchSeconds(int index)
        {return SilentLedgerActive&&index>=0&&index<2?
            silentLedgerSearchSeconds[index]:0f;}
        public int SilentLedgerSentryScanTurns(int index)
        {return SilentLedgerActive&&index>=0&&index<2?
            silentLedgerScanTurns[index]:0;}
        public bool SilentLedgerSentrySweeping(int index)
        {return SilentLedgerActive&&index>=0&&index<2&&
            silentLedgerDiversion<=0f&&silentLedgerSearchSeconds[index]>0f&&
            (silentLedgerSentries[index]-silentLedgerSearchTarget[index]).Length<.42f;}
        public Point SilentLedgerSentrySearchTarget(int index)
        {return SilentLedgerActive&&index>=0&&index<2?
            silentLedgerSearchTarget[index]:new Point();}
        public bool SilentLedgerSentrySearchingSound(int index)
        {return SilentLedgerActive&&index>=0&&index<2&&
            silentLedgerSearchSeconds[index]>0f&&silentLedgerSoundSearch[index];}
        public Point SilentLedgerTarget
        {get{return silentLedgerStage==1?
            ObjectPoint(4,"silentledgerrecord",new Point(58.5f,33.5f)):
            silentLedgerStage==2?new Point(63.5f,43.5f):
            silentLedgerStage>=3?new Point(66.5f,38.5f):
            new Point(54.5f,38.5f);}}
        public string SilentLedgerObjective
        {get{return silentLedgerStage==1?L("取回第一份失踪平民档案","RECOVER FIRST LOST REGISTER"):
            silentLedgerStage==2?L("沿暗道取回证人口供","RECOVER THE WITNESS TESTIMONY"):
            silentLedgerStage==3?L("避开巡逻，抵达旧封存台","REACH THE SEALED CITADEL CONSOLE"):
            L("从城墙下的档案告示牌进入","READ THE CIVIC ARCHIVE NOTICE");}}
        // Reached from the EXISTING inn guest's real "local news" choice.
        // This is aftermath testimony, not a repeatable quest or reward.
        // The branch facts are authoritative v2 outcome flags.
        private string SilentLedgerInnAftermath(bool english)
        {
            if(SilentLedgerPublic)
                return english?
                    "\n\nThe traveler spreads the copied Citadel register beside a bowl of cooling stew. One child's name has been circled three times. An older woman from the hearth comes over, still holding her untouched soup. 'That's my granddaughter,' she says. 'At least now I know where to ask.' The reopened archive desk gives families a place to rest and buys time for the next evacuation.":
                    "\n\n旅人把堡垒新誊的名册摊在快凉的汤碗旁。有个孩子的名字被圈了三次。炉边一位老人端着没喝的汤走过来，低声说：‘这是我孙女。总算知道该去哪里问了。’档案台如今能安置回乡的人，也给下一次撤离多争取了一点时间。";
            if(SilentLedgerRoutes)
                return english?
                    "\n\nThe traveler keeps the restored route chart beneath his supper plate, away from the patrol at the next table. A thin line crosses the old culvert. Beside it someone wrote: 'Let the children go first.' The roadwardens now protect that crossing; walkers leaving the Citadel can move faster, and the families behind them no longer have to run just to keep up.":
                    "\n\n旅人把恢复的归乡路线图压在餐盘底下，不让隔桌巡兵看见。旧涵洞旁有一道细线，旁边写着：‘让孩子先过。’现在守护这条路的是巡路队。离开堡垒的人走得更快了，跟在后面的家人也不必一路追赶。";
            return "";
        }
        public bool SilentLedgerFamilyStarted { get { return Has("silentledger_family_started"); } }
        public bool SilentLedgerFamilySigned { get { return Has("silentledger_family_signed"); } }
        public bool SilentLedgerFamilyDone { get { return Has("silentledger_family_done"); } }
        private bool SilentLedgerFamilySource(WorldObject o,string id,int room)
        {return o!=null&&o.id==id&&save.room==room&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);}
        private void ResetSilentLedger()
        {
            silentLedgerStage=silentLedgerSweep=0;
            silentLedgerTime=silentLedgerSuspicion=silentLedgerDiversion=silentLedgerDarkness=0f;
            silentLedgerDecoyUsed=silentLedgerShutterUsed=false;
            silentLedgerSentries[0]=silentLedgerA[0];
            silentLedgerSentries[1]=silentLedgerB[0];
            silentLedgerFacing[0]=new Point(1,0);
            silentLedgerFacing[1]=new Point(0,1);
            silentLedgerFootDistance[0]=silentLedgerFootDistance[1]=0f;
            silentLedgerFootStride[0]=silentLedgerFootStride[1]=0f;
            silentLedgerScanClock[0]=silentLedgerScanClock[1]=0f;
            silentLedgerScanTurns[0]=silentLedgerScanTurns[1]=0;
            silentLedgerOutbound[0]=silentLedgerOutbound[1]=true;
            silentLedgerSearchSeconds[0]=silentLedgerSearchSeconds[1]=0f;
            silentLedgerSoundSearch[0]=silentLedgerSoundSearch[1]=false;
            silentLedgerSearchTarget[0]=silentLedgerA[0];
            silentLedgerSearchTarget[1]=silentLedgerB[0];
            silentLedgerLastHero=new Point();
        }
        private bool SilentLedgerSource(WorldObject o,string id)
        {return o!=null&&o.id==id&&save.room==4&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);}
        private bool TalkSilentLedger(WorldObject o)
        {
            if(o==null||!o.kind.StartsWith("silentledger",
                StringComparison.Ordinal))return false;
            if(save.room==0&&o.id=="silentledger_aid")
            {
                if(!SilentLedgerFamilySource(o,"silentledger_aid",0))return true;
                if(SilentLedgerFamilyDone)
                {
                    Dialog(o.name,o.en,
                        SilentLedgerPublic?
                            "十七户返乡接应已建立。安全的援助台会为靠近的人持续处理伤口。":
                            "十七户的秘密引路桩已立好。安全的援助台可以让路人缓一缓，重新积蓄体力。",
                        SilentLedgerPublic?
                            "The seventeen-household relief has opened. The safe aid desk now helps nearby travelers recover wounds.":
                            "The discreet waymarks are installed. At the safe aid desk, nearby travelers can regain stamina for the road.",
                        C("继续赶路","Continue","close"));return true;
                }
                if(!SilentLedgerFamilySigned)
                {
                    Dialog(o.name,o.en,
                        "公会旁的民间援助台还没有收到夜旅人确认的家庭名单。先从堡垒封存台领取封件，送到炉火旅店交给守夜旅人签收。",
                        "The community aid desk still awaits a household packet signed by the night traveler. Collect it from the Citadel console and take it to Hearth Inn first.",
                        C("离开","Leave","close"));return true;
                }
                Dialog(o.name,o.en,
                    SilentLedgerPublic?
                        "旅人已核对十七户公开名册。公会现在需要两份月光草给返乡的伤者配药；补给到位后才能兑现援助。":
                        "旅人已签收不公开的归乡路线。公会需要两份木材标记安全涵洞，不能让巡兵发现通路。",
                    SilentLedgerPublic?
                        "The traveler matched the seventeen public family entries. The guild needs two Moonherbs for returning wounded relatives before the relief can be funded.":
                        "The traveler signed for the confidential homeward route. The guild needs two Timber to mark the safe culvert without alerting the patrol.",
                    C(SilentLedgerPublic?"月光草2 · 交付药材":"木材2 · 标记涵洞",
                        SilentLedgerPublic?"Deliver 2 Moonherbs":"Deliver 2 Timber",
                        "silentledger_family_finish"),
                    C("先去收集材料","Gather supplies first","close"));return true;
            }
            if(save.room!=4)return false;
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
                        "旧堡垒的名册刻意漏记了撤离的家庭。两名夜巡守卫在外墙来回走动，视线会被真实墙壁遮住。悄步移动能减轻被发现的速度；冲刺声会吸引他们。按 U 投石，石子真实落地处会引开守卫，近处的投掷动作却也会暴露位置。东墙照明闸能暂时压低夜巡灯光，却会发出引来近处守卫的金属声。到北边取走被涂改的登记，再去南侧找目击者口供，最后潜入封存台公开真相。若被发现或倒计时结束，行动会撤回，可以免费重试。",
                        "The old citadel erased evacuated families from the register. Two night sentries patrol the outer wall and see only through actual unblocked sightlines. Quiet walking reduces exposure; sprinting attracts them. Press U to throw a pebble: its real landing can lure a patrol, but a nearby sentry can also hear your arm throw. The east-wall lighting shutter briefly dims patrol lanterns, but its metal latch can be heard by nearby sentries. Take the tampered north register, recover the southern eyewitness testimony, then reach the sealed console. Detection or timeout cancels this attempt; retries are free.",
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
                if(SilentLedgerComplete)
                {
                    if(!SilentLedgerFamilyStarted)
                        Dialog(o.name,o.en,
                            "两份证据已在这里归档。十七户人家的归乡联络还缺最后一趟路：将封存台的家庭联络包亲自送到炉火旅店，请守夜旅人核验，再把援助需求交给余烬镇公会旁的援助台。",
                            "Both proofs are archived here, but seventeen households still need someone to carry their homeward packet. Take it to the night traveler at Hearth Inn for verification, then fund the civilian aid desk beside Emberfall's guild.",
                            C("领取家庭联络包","Carry the family dispatch","silentledger_family_start"),
                            C("稍后再来","Return later","close"));
                    else
                        Dialog(o.name,o.en,
                            SilentLedgerFamilyDone?
                                "家人们的联络包已经送达，最后的补给也有了着落。":
                                "家庭联络包已经领走。炉火旅店的守夜旅人会辨认归乡路径，再由余烬镇援助台接手。",
                            SilentLedgerFamilyDone?
                                "The household dispatch and its final relief supplies have reached their destination.":
                                "The family packet is in transit. Speak with Hearth Inn's night traveler, then fund the Emberfall aid desk.",
                            C("离开","Leave","close"));
                    return true;
                }
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
            if(o.id=="silentledger_shutter")
            {
                if(SilentLedgerActive&&!silentLedgerShutterUsed)
                {
                    Dialog(o.name,o.en,
                        "旧铜闸控制外墙巡灯。扳下后照明会暗22秒，夜巡看不远，却仍能听见脚步；金属闩声会让附近守卫赶来。每次行动只能操作一次。",
                        "An old copper shutter powers the outer patrol lamps. Pulling it dims the lamps for 22 seconds; sentries lose distant sight but still hear footsteps. The loud latch draws any guard close enough to hear it. One use per infiltration.",
                        C("扳下照明闸","Cut the patrol lamps","silentledger_shutter"),
                        C("保留照明","Leave the lamps","close"));return true;
                }
                Dialog(o.name,o.en,
                    "铜闸已落下，黑暗终究会过去。档案和巡路仍在等着人。",
                    "The shutter is already down. The darkness will pass, and the records still need recovering.",
                    C("离开","Leave","close"));return true;
            }
            if(o.id=="silentledger_decoy")
            {
                if(SilentLedgerActive&&!silentLedgerDecoyUsed)
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
            if(action.StartsWith("silentledger_family_",StringComparison.Ordinal))
            {
                if(source==null||mode!="world"||Dying||practicing||
                    pendingDialogueSourceRoom!=save.room||
                    !SourceBelongsToActiveMap(source)||
                    !HasPhysicalInteractionReach(source))return true;
                if(action=="silentledger_family_start")
                {
                    if(!SilentLedgerFamilySource(source,"silentledger_console",4)||
                        !SilentLedgerComplete||SilentLedgerFamilyStarted)return true;
                    Flag("silentledger_family_started");
                    TrackQuest("family_dispatch");revision++;sound="quest";
                    Notify("十七户家庭联络包已领取。送到炉火旅店给守夜旅人签收。",
                        "Family dispatch collected. Take it to the night traveler at Hearth Inn.");
                    return true;
                }
                if(action=="silentledger_family_handoff")
                {
                    if(!SilentLedgerFamilySource(source,"innTraveler",9)||
                        !SilentLedgerFamilyStarted||SilentLedgerFamilySigned)return true;
                    Flag("silentledger_family_signed");revision++;sound="quest";
                    Dialog(source.name,source.en,
                        SilentLedgerPublic?
                            "旅人逐一念过名单，指尖停在最后一行。‘这些人不是统计数字。请让莉雅拿到药材，别让他们到家时还在流血。’他签下十七户的核验单。":
                            "旅人只扫了一眼背面的涵洞标记，就把信封压回你手里。‘这条路别写在公告上。给莉雅送两份木材，她知道该把木桩立在哪里。’他在封口处签了名。",
                        SilentLedgerPublic?
                            "The traveler reads each name. His finger stops at the last line. 'These are people, not figures. Bring Lyra the herbs before anyone bleeds on the way home.' He signs for all seventeen households.":
                            "The traveler glimpses the culvert mark and presses the envelope back into your palm. 'Keep this road off the public board. Bring Lyra two timber; she knows where the markers belong.' He signs the seal.",
                        C("去公会旁的援助台","Visit Emberfall aid desk","close"));
                    return true;
                }
                if(action=="silentledger_family_finish")
                {
                    if(!SilentLedgerFamilySource(source,"silentledger_aid",0)||
                        !SilentLedgerFamilySigned||SilentLedgerFamilyDone)return true;
                    int item=SilentLedgerPublic?2:0;
                    if(save.inventory[item]<2)
                    {
                        Notify(SilentLedgerPublic?
                            "返乡药材不足：需月光草2。":"标路木材不足：需木材2。",
                            SilentLedgerPublic?
                            "Need 2 Moonherbs to treat the returning families.":
                            "Need 2 Timber to mark the protected homeward crossing.");
                        return true;
                    }
                    save.inventory[item]-=2;
                    Flag("silentledger_family_done");
                    CreditCoins(45);CreditShards(1);AddXp(70);
                    RecordMoment(SilentLedgerPublic?
                        "你将两份月光草送到余烬镇民间援助台，十七户失踪家庭的归乡医疗接应终于落实。":
                        "你为十七户家庭的隐蔽涵洞送去了两份木材，巡路队沿着不公开的路线立起了夜间引路标。",
                        SilentLedgerPublic?
                        "You funded medical relief for all seventeen restored family entries with two Moonherbs.":
                        "You supplied two Timber for discreet waymarks along the protected culvert route.");
                    revision++;sound="reward";
                    Notify(SilentLedgerPublic?
                        "十七户归乡援助完成！硬币+45、碎片+1、经验+70；余烬镇援助台提供安全伤口恢复。":
                        "秘密归乡引路桩已建立！硬币+45、碎片+1、经验+70；余烬镇援助台提供安全耐力恢复。",
                        SilentLedgerPublic?
                        "Seventeen-family relief complete! +45 coins, +1 shard, +70 XP; safe Emberfall aid desk now heals wounds.":
                        "Protected homeward waymarks installed! +45 coins, +1 shard, +70 XP; safe Emberfall aid desk now restores stamina.");
                    return true;
                }
                return true;
            }
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
            if(action=="silentledger_shutter")
            {
                if(source.id!="silentledger_shutter"||!SilentLedgerActive||
                    silentLedgerShutterUsed)return true;
                silentLedgerShutterUsed=true;silentLedgerDarkness=22f;
                // A loud physical latch: only sentries within sound range
                // and a real unobstructed line can investigate its foot.
                // Do not give the guards knowledge through distant walls.
                bool latchHeard=false;
                Point latch=new Point(source.x,source.y);
                for(int i=0;i<2;i++)
                    if((silentLedgerSentries[i]-latch).Length<7f&&
                        ClearAttackPath(silentLedgerSentries[i],latch))
                    {
                        latchHeard=true;
                        if(silentLedgerDiversion<=0f)
                        {
                            silentLedgerSearchTarget[i]=latch;
                            silentLedgerSearchSeconds[i]=4.6f;
                            silentLedgerSoundSearch[i]=true;
                        }
                    }
                if(latchHeard)
                    silentLedgerSuspicion=Math.Min(100f,silentLedgerSuspicion+8f);
                revision++;sound="quest";
                Notify("巡灯熄暗22秒！附近守卫听到闸声正在搜查；黑暗不会遮掩脚步声。",
                    "Patrol lights dim for 22 seconds! Nearby guards heard the latch; darkness cannot hide footsteps.");
                return true;
            }
            if(action=="silentledger_decoy")
            {
                if(source.id!="silentledger_decoy"||!SilentLedgerActive||
                    silentLedgerDecoyUsed||save.inventory[4]<1)return true;
                save.inventory[4]--;silentLedgerDecoyUsed=true;
                silentLedgerDiversion=12f;
                silentLedgerSearchSeconds[0]=silentLedgerSearchSeconds[1]=0f;
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
                    "失踪者名册公开！硬币+65、碎片+2、经验+105；安全档案台附近可恢复生命与耐力，长夜救援真实延长15秒。":
                    "归乡路线获救！硬币+65、碎片+2、经验+105；堡垒移动速度永久+0.35，长夜救援的居民步速由5.8提高到6.5。",
                    publicRecord?
                    "Missing-person register published! +65 coins, +2 shards, +105 XP; safe archive console grants local recovery and 15 real extra seconds in the Blackout evacuation.":
                    "Safe homeward routes restored! +65 coins, +2 shards, +105 XP; permanent +0.35 Citadel speed and faster on-foot Blackout civilian escorts (5.8 to 6.5).");
                return true;
            }
            return true;
        }
        // The game's ordinary U-key pebble is real world physics. Listen
        // to the genuine launch and the later contact foot; never to an
        // intended mouse target, a UI click, or a fabricated projectile.
        // A confirmed last sighting outranks a distraction. The old patrol
        // bell retains priority while it is active.
        private void SilentLedgerHearPebble(Point foot,float range,
            float searchSeconds,bool launch)
        {
            if(!SilentLedgerActive||Dying||practicing||
                silentLedgerDiversion>0f||!CanStand(foot))return;
            bool heard=false;
            for(int i=0;i<2;i++)
            {
                Point patrol=silentLedgerSentries[i];
                if((patrol-foot).Length>range||!CanStand(patrol)||
                    !ClearAttackPath(patrol,foot)||SilentLedgerSeesHero(i)||
                    (silentLedgerSearchSeconds[i]>0f&&!silentLedgerSoundSearch[i]))
                    continue;
                silentLedgerSearchTarget[i]=foot;
                silentLedgerSearchSeconds[i]=Math.Max(
                    silentLedgerSearchSeconds[i],searchSeconds);
                silentLedgerSoundSearch[i]=true;
                heard=true;
            }
            // A stone hitting the floor is a possible environmental noise,
            // not proof that the hero is there. A very nearby audible arm
            // throw, in contrast, gives away the true launch foot.
            if(heard&&launch)
                silentLedgerSuspicion=Math.Min(100f,silentLedgerSuspicion+5f);
        }
        private void StepSilentLedgerSentry(int index,float dt)
        {
            silentLedgerFootStride[index]=0f;
            Point[] ends=index==0?silentLedgerA:silentLedgerB;
            Point destination=ends[silentLedgerOutbound[index]?1:0];
            if(silentLedgerDiversion>0f)
                destination=new Point(54.5f,38.5f);
            else if(silentLedgerSearchSeconds[index]>0f)
                destination=silentLedgerSearchTarget[index];
            Point current=silentLedgerSentries[index];
            Point direction=DirectionTo(current,destination);
            if((destination-current).Length<.42f)
            {
                // A sentry waits at the genuinely reached last-seen foot.
                // It cannot continue chasing a target through a sealed wall.
                if(silentLedgerDiversion<=0f&&silentLedgerSearchSeconds[index]>0f)
                {
                    // When the guard actually reaches the last heard/seen
                    // foot, search the surrounding arcs instead of staring
                    // forever at a single bearing. The REAL sightline and
                    // wall collision test still decide whether it sees you.
                    silentLedgerScanClock[index]+=dt;
                    if(silentLedgerScanClock[index]>=.55f)
                    {
                        silentLedgerScanClock[index]-=.55f;
                        Point face=silentLedgerFacing[index];
                        silentLedgerFacing[index]=new Point(-face.y,face.x);
                        silentLedgerScanTurns[index]++;
                    }
                    return;
                }
                silentLedgerScanClock[index]=0f;
                silentLedgerOutbound[index]=!silentLedgerOutbound[index];
                return;
            }
            if(direction.Length<.001f)return;
            float step=Math.Min(dt*(silentLedgerDiversion>0f?2.2f:1.65f),
                (destination-current).Length);
            Point candidate=current+direction*step;
            if(CanStand(candidate)&&ClearAttackPath(current,candidate))
            {
                silentLedgerSentries[index]=candidate;
                silentLedgerFacing[index]=direction;
                silentLedgerScanClock[index]=0f;
                // Only real, collision-cleared physical translations advance
                // guard Sprite gait. Standing at a sound target is not walking.
                silentLedgerFootStride[index]=(candidate-current).Length;
                silentLedgerFootDistance[index]+=silentLedgerFootStride[index];
            }
        }
        private bool SilentLedgerSeesHero(int index)
        {
            Point sentry=silentLedgerSentries[index];
            Point offset=Position-sentry;
            float distance=offset.Length;
            if(distance<.001f)return true;
            float sightRange=silentLedgerDarkness>0f?
                (QuietStepping?2f:2.8f):(QuietStepping?3.45f:6.2f);
            if(distance>sightRange||
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
                // The funded AFTERMATH is an actual, localized persistent
                // service, not a repeated reward. Recovery only happens at
                // the original civilian desk on safe walkable ground,
                // outside any active timed field mission or threat.
                if(SilentLedgerFamilyDone&&save.room==0&&!Dying&&
                    !practicing&&SafeTravel&&!FieldWorkPreventsRest&&
                    PhysicalRefugeReach(new Point(15.5f,16.5f),2.5f))
                {
                    if(SilentLedgerPublic)
                        save.health=Math.Min(save.maxHealth,save.health+2f*dt);
                    else if(SilentLedgerRoutes)
                        save.energy=Math.Min(100f,save.energy+4f*dt);
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
            if(silentLedgerDarkness>0f)
                silentLedgerDarkness=Math.Max(0f,silentLedgerDarkness-dt);
            for(int i=0;i<2;i++)
            {
                silentLedgerSearchSeconds[i]=Math.Max(0f,
                    silentLedgerSearchSeconds[i]-dt);
                StepSilentLedgerSentry(i,dt);
            }
            bool seen=false;
            bool[] seenBy=new bool[2];
            for(int i=0;i<2;i++)
            {
                seenBy[i]=SilentLedgerSeesHero(i);
                if(seenBy[i])
                {
                    seen=true;
                    if(silentLedgerDiversion<=0f)
                    {
                        silentLedgerSearchTarget[i]=Position;
                        silentLedgerSearchSeconds[i]=3.1f;
                        silentLedgerSoundSearch[i]=false;
                    }
                }
            }
            // Actual translated feet, not a held key, produce sound.
            // Attacking makes more noise; crouch reduces visible range and
            // the rate of a real alarm without granting invisibility.
            bool loudStep=moved>dt*5.2f&&!QuietStepping;
            bool attackNoise=playerAction!=null&&!playerAction.released;
            // A weapon is audible to REAL nearby sentries, not to the whole
            // Citadel. The sound gives a physical last-heard foot to search.
            // Stone blocks both hearing and the shortcut to the target.
            bool attackHeard=false;
            if(attackNoise)
                for(int i=0;i<2;i++)
                    if((silentLedgerSentries[i]-Position).Length<8f&&
                        ClearAttackPath(silentLedgerSentries[i],Position))
                    {
                        attackHeard=true;
                        if(silentLedgerDiversion<=0f&&!seenBy[i])
                        {
                            // Actual eyewitness evidence outranks the
                            // simultaneous clang of the hero's attack.
                            // A seen attacker is chased as LAST SEEN,
                            // never mislabeled as an anonymous noise.
                            silentLedgerSearchTarget[i]=Position;
                            silentLedgerSearchSeconds[i]=Math.Max(
                                silentLedgerSearchSeconds[i],4.2f);
                            silentLedgerSoundSearch[i]=true;
                        }
                    }
            if(seen)
                silentLedgerSuspicion+=dt*(QuietStepping?11f:25f);
            else
            {
                bool heardStep=false;
                if(loudStep)
                    for(int i=0;i<2;i++)
                        if((silentLedgerSentries[i]-Position).Length<5f&&
                            ClearAttackPath(silentLedgerSentries[i],Position))
                        {
                            heardStep=true;
                            if(silentLedgerDiversion<=0f)
                            {
                                silentLedgerSearchTarget[i]=Position;
                                silentLedgerSearchSeconds[i]=Math.Max(
                                    silentLedgerSearchSeconds[i],4.2f);
                                silentLedgerSoundSearch[i]=true;
                            }
                        }
                if(heardStep)silentLedgerSuspicion+=dt*19f;
                else if(!attackHeard)silentLedgerSuspicion-=dt*9f;
            }
            if(attackHeard)silentLedgerSuspicion+=dt*24f;
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
