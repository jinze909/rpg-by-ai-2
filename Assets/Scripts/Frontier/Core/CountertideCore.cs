using System;
namespace Starfall
{
    // Mistport's original low/high-water lanes become an authored timed
    // signal puzzle. No teleport, synthetic tide, or new SaveData fields.
    public sealed partial class GameCore
    {
        private int countertideStage,countertideMistakes;
        private bool countertideStabilized;
        public bool CountertideStabilized {get{return CountertideActive&&countertideStabilized;}}
        public bool CountertideStabilizerAvailable
        {get{return countertideStage>=2&&countertideStage<=3&&
            !countertideStabilized&&countertideRemaining<=215f&&
            save.inventory[1]>=1;}}
        private float countertideRemaining;
        public int CountertideStage { get { return countertideStage; } }
        public bool CountertideActive { get { return countertideStage>0; } }
        public float CountertideRemaining { get { return CountertideActive?countertideRemaining:0f; } }
        public int CountertideMistakes { get { return countertideMistakes; } }
        public bool CountertideComplete { get { return Has("countertide_channels")||Has("countertide_refuge"); } }
        public bool CountertideChannels { get { return Has("countertide_channels"); } }
        public bool CountertideRefuge { get { return Has("countertide_refuge"); } }
        public int CountertideHarborEbbSeconds
        { get { return CountertideChannels?28:22; } }
        public Point CountertideTarget
        {
            get
            {
                if(countertideStage==1)return new Point(61.5f,36.5f);
                if(countertideStage==2)return new Point(43.5f,42.5f);
                return new Point(65.5f,40.5f);
            }
        }
        public string CountertideObjective
        {
            get
            {
                if(countertideStage==1)return L("低潮 · 营地铜钟：两声","EBB · CAMP BELL: TWO");
                if(countertideStage==2)return L("高潮 · 旧补给箱：三声","FLOOD · OLD CACHE: THREE");
                if(countertideStage==3)return L("低潮 · 远眺台：一声","EBB · OVERLOOK: ONE");
                return L("远眺台 · 决定航标归属","OVERLOOK · CHOOSE BEACON POLICY");
            }
        }
        private void ResetCountertide()
        {
            countertideStage=countertideMistakes=0;
            countertideRemaining=0f;countertideStabilized=false;
        }
        private bool CountertideSource(WorldObject o,string id)
        {
            return save.room==5&&o!=null&&o.id==id&&
                pendingDialogueSourceRoom==5&&
                SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);
        }
        private bool TalkCountertide(WorldObject o)
        {
            if(o==null||save.room!=5)return false;
            if(o.id!="annex_vista_5"&&o.id!="annex_camp_5"&&
               o.id!="annex_cache_5_b")return false;
            if(!CountertideActive&&(!Has("boss_5")||
               !Has("harbor_report")||CountertideComplete))
               return false;
            if(!SourceBelongsToActiveMap(o)||
               !HasPhysicalInteractionReach(o))return true;
            if(!CountertideActive)
            {
                if(o.id!="annex_vista_5")return false;
                Dialog(o.name,o.en,
                    "雾港旧远眺台的缆绳里夹着一张民用航标谱：“退潮双声、满潮三声、归港单声。”这份谱不是运货账，而是涨潮时帮助迷路小船回港的呼号。\n\n在真正的三个旧站依潮位敲响2-3-1，便能找回航标的控制权。全程260秒；错音会损失12秒，错误与等待都不改变真实海水。失败可重新尝试。",
                    "A civilian call sheet is tied into the old Mistport lookout: 'Two at ebb, three at flood, one to come home.' It guided lost families, not cargo.\n\nSound 2-3-1 at three physical stations in the correct real tide windows. The run lasts 260 seconds; a wrong note costs 12 seconds. Nothing fakes the real water. Retries are free.",
                    C("接过航标谱 · 启动逆潮信号",
                        "Take call sheet · begin countertide","countertide_begin"),
                    C("暂缓出发","Not yet","close"));
                return true;
            }
            if(o.id=="annex_camp_5"&&countertideStage>=2&&
               countertideStage<=3)
            {
                Dialog(o.name,o.en,
                    "守火人还保留着旧航标的独立星晶校准片。信号已经离开营地，若倒计时将尽，可亲手将1份星晶压入调谐架，延长45秒，并让之后每次错误钟声的惩罚从12秒减为6秒。每次现场行动只能做一次，且只有剩余215秒以下才需要加固。",
                    "The firekeeper has one separate crystal regulator. Once the signal has left camp, you may return in person and pay one crystal to gain 45 seconds and reduce future wrong-note penalties from 12 to 6 seconds. Once per attempt, available only when 215 seconds or less remain.",
                    CountertideStabilizerAvailable?
                    C("星晶×1 · 加固航标，时间+45秒",
                      "1 crystal · reinforce beacon, +45s","countertide_stabilize"):
                    C("本次无法或无需加固",
                      "Regulator unavailable or unnecessary","close"),
                    C("继续送出潮声","Keep carrying tide calls","close"));
                return true;
            }
            if(o.id=="annex_vista_5")
            {
                if(countertideStage==4)
                {
                    Dialog(o.name,o.en,
                        "三处真实航标都已对准：两声开航、三声满潮、一声归港。老摆渡人把两个不同的方案交给你。\n\n公开航道：在雾港把每轮低潮的可通行窗口延长6秒，让更多民用渡船有时间靠岸。\n\n隐蔽救援：将航标指示交给伤者和小船，雾港的陆路撤离速度永久增加0.55。二者互斥，无法重复领赏。",
                        "Three authentic signals align: two to depart, three at flood, one to return. The ferryman offers two mutually exclusive plans.\n\nPublic channel: extend Mistport's real ebb crossing by six seconds per tide.\n\nPrivate evacuation: improve actual overland movement speed in Mistport by 0.55. Both are permanent; no repeat reward.",
                        C("公开潮路 · 雾港低潮延长6秒",
                            "Publish channel · +6s Mistport ebb","countertide_channels"),
                        C("保护救援通道 · 雾港移速+0.55",
                            "Shelter boats · +0.55 Mistport speed","countertide_refuge"),
                        C("再想想","Decide later","close"));
                    return true;
                }
                if(countertideStage==3)
                {
                    CountertideSignalDialog(o);
                    return true;
                }
                Dialog(o.name,o.en,
                    "航标谱写着：退潮双声、满潮三声、归港单声。你现在必须先完成真正的前一站。",
                    "Two at ebb, three at flood, one to come home. Complete the earlier physical station first.",
                    C("查看任务指引","Review objective","close"),
                    C("放弃本次校准","Abort calibration","countertide_abort"));
                return true;
            }
            if(countertideStage==1&&o.id=="annex_camp_5"||
               countertideStage==2&&o.id=="annex_cache_5_b")
            {
                CountertideSignalDialog(o);
                return true;
            }
            Dialog(o.name,o.en,
                "这里的信号暂未轮到。民用航标必须按原来的航路传递，不能跳过涨落潮。",
                "This is not the next signal. Follow the actual ebb-and-flood sequence.",
                C("继续校准","Keep calibrating","close"));
            return true;
        }
        private void CountertideSignalDialog(WorldObject site)
        {
            bool correctTide=countertideStage==2?!LowTide:LowTide;
            string zh=countertideStage==1?
                "营地的第一口铜钟只有低潮时才能把声音送入航道。古谱写着：退潮双声。":
                countertideStage==2?
                "旧补给箱里露出沉在水下的三条铜弦。只有高潮才能让满潮回音抵达另一岸。":
                "远眺台的最后一只铜铃等着退潮，归港的呼号只需一声。";
            string en=countertideStage==1?
                "The first bell only carries over open ebb channels. The call sheet says: two at ebb.":
                countertideStage==2?
                "Three submerged wires at the old cache carry the flood signal only at high tide.":
                "The overlook's final bell calls one time to bring travelers home at ebb.";
            Dialog(site.name,site.en,
                zh+"\n\n当前"+(LowTide?"低潮":"高潮")+
                "。本次剩余"+countertideRemaining.ToString("0")+"秒。"+
                (correctTide?"信号窗口已到。":"等待真实潮位切换，不能在错误潮位透支下一阶段。"),
                en+"\n\nCurrently "+(LowTide?"LOW":"HIGH")+
                " tide, "+countertideRemaining.ToString("0")+"s remaining. "+
                (correctTide?"The physical signal window is open.":
                    "Wait for the real tide to change; a wrong phase cannot be bypassed."),
                C("敲一次","Ring once","countertide_note_1"),
                C("敲两次","Ring twice","countertide_note_2"),
                C("敲三次","Ring three times","countertide_note_3"));
        }
        private bool ChooseCountertide(string action)
        {
            if(!action.StartsWith("countertide_",StringComparison.Ordinal))
                return false;
            if(save.room!=5||Dying||practicing||mode!="world")return true;
            WorldObject source=pendingDialogueSource;
            if(source==null||!CountertideSource(source,source.id))return true;
            if(action=="countertide_begin")
            {
                if(source.id!="annex_vista_5"||CountertideActive||
                   CountertideComplete||!Has("boss_5")||
                   !Has("harbor_report")||FieldWorkPreventsRest||
                   !SafeTravel)return true;
                countertideStage=1;countertideRemaining=260f;
                countertideMistakes=0;
                TrackQuest("countertide");revision++;sound="quest";
                Notify("逆潮航标已开始。前往营地在真实低潮时敲响两声。",
                    "Countertide started. Reach camp and ring twice at the real ebb.");
                return true;
            }
            if(action=="countertide_abort")
            {
                if(source.id=="annex_vista_5"&&CountertideActive)
                {
                    ResetCountertide();revision++;
                    Notify("已撤销本次航标校准。原始谱仍留在远眺台。",
                        "Calibration withdrawn. The original sheet remains at the lookout.");
                }
                return true;
            }
            if(action=="countertide_stabilize")
            {
                if(source.id!="annex_camp_5"||
                   !CountertideStabilizerAvailable)return true;
                if(!Spend(0,1,0,0,0))return true;
                countertideStabilized=true;
                countertideRemaining=Math.Min(260f,countertideRemaining+45f);
                revision++;sound="reward";
                Notify("航标已物理加固：时间+45秒，以后错音只扣6秒，本次不能重复加固。",
                    "Beacon reinforced: +45s and future wrong notes cost only 6s. One purchase per attempt.");
                return true;
            }
            if(action.StartsWith("countertide_note_",StringComparison.Ordinal))
            {
                int expected=countertideStage==1?2:countertideStage==2?3:
                    countertideStage==3?1:0;
                string site=countertideStage==1?"annex_camp_5":
                    countertideStage==2?"annex_cache_5_b":
                    countertideStage==3?"annex_vista_5":"";
                if(expected==0||source.id!=site||countertideRemaining<=0f)
                    return true;
                bool correctTide=countertideStage==2?!LowTide:LowTide;
                if(!correctTide)
                {
                    Notify("潮位不对。必须等待真实海水变化，不能靠对话跳过。",
                        "Wrong tide. Wait for the real water; dialogue cannot bypass it.");
                    return true;
                }
                int selected=0;
                if(!int.TryParse(action.Substring("countertide_note_".Length),
                    out selected)||selected<1||selected>3)return true;
                if(selected!=expected)
                {
                    countertideMistakes++;countertideRemaining=Math.Max(
                        0f,countertideRemaining-(countertideStabilized?6f:12f));
                    revision++;
                    Notify(countertideStabilized?
                        "钟声不符！加固片吸收回音，时间-6秒。":
                        "钟声不符！时间-12秒。记住：退潮双声、满潮三声、归港单声。",
                        countertideStabilized?
                        "Wrong note! The regulator dampened it: -6 seconds.":
                        "Wrong note! -12 seconds. Remember: two at ebb, three at flood, one home.");
                    return true;
                }
                countertideStage++;
                revision++;sound="quest";
                if(countertideStage==4)
                    Notify("三处潮声已经真实对齐！在远眺台重新打开航标记录，决定公众航路。",
                        "All three authentic tides aligned. Reopen the lookout log and decide the route policy.");
                else
                    Notify("信号已送达下一处。请查看手帐的真实潮位和方位指引。",
                        "Signal advanced. Check the journal for the next physical tide and bearing.");
                return true;
            }
            if(action=="countertide_channels"||action=="countertide_refuge")
            {
                if(countertideStage!=4||source.id!="annex_vista_5"||
                   CountertideComplete||countertideRemaining<=0f)return true;
                bool publicRoute=action=="countertide_channels";
                ResetCountertide();
                Flag(publicRoute?"countertide_channels":"countertide_refuge");
                CreditCoins(65);CreditShards(2);AddXp(105);
                RecordMoment(publicRoute?
                    "三次真正的潮汐回声接通后，你把旧民用航道重新公开，让雾港每次低潮的通行时间多出六秒。":
                    "你在三次真正的潮汐回声之间找回避难路标，将航标交给渡船与伤者，雾港的陆路撤离脚程更快。",
                    publicRoute?
                    "After three genuine tide-locked signals, you published the civilian channel and extended Mistport's real ebb crossing by six seconds.":
                    "After three genuine tidal signals, you protected the refuge route and gave stranded families faster overland passage.");
                revision++;sound="reward";
                Notify(publicRoute?
                    "逆潮航标修复：雾港低潮窗口延长6秒；硬币+65、碎片+2、经验+105。":
                    "逆潮航标修复：雾港移动速度+0.55；硬币+65、碎片+2、经验+105。",
                    publicRoute?
                    "Countertide restored: +6s real Mistport ebb; +65 coins, +2 shards, +105 XP.":
                    "Countertide restored: +0.55 Mistport speed; +65 coins, +2 shards, +105 XP.");
                return true;
            }
            return true;
        }
        private void TickCountertide(float dt)
        {
            if(countertideStage==0||countertideStage==4)return;
            if(save.room!=5||Dying||practicing)
            {ResetCountertide();revision++;return;}
            countertideRemaining-=dt;
            if(countertideRemaining<=0f)
            {
                ResetCountertide();revision++;
                Notify("航标潮声已散失。原始谱留在远眺台，可再次校准。",
                    "The tidal signal faded. Retry with the original sheet at the lookout.");
            }
        }
    }
}
