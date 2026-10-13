using System;

namespace Starfall
{
    // Chapter Seven: the post-storm hearing requires testimony from the actual
    // three regional residents. Public authority follows lived choices.
    // Existing SaveData v2 strings hold all permanent state.
    public sealed partial class GameCore
    {
        public bool HearingReady
        {
            get { return StormChapterComplete && AfterlightComplete &&
                (Has("storm_orrin_open") || Has("storm_orrin_guard")); }
        }
        public int HearingVoice(int region)
        {
            return Has("hearing_voice_"+region+"_care")?1:
                Has("hearing_voice_"+region+"_watch")?2:0;
        }
        public int HearingSignatures
        {
            get { int n=0;for(int i=1;i<=3;i++)if(HearingVoice(i)!=0)n++;return n; }
        }
        public bool HearingComplete { get { return Has("hearing_complete"); } }
        public int HearingVerdict
        { get { return Has("hearing_public")?1:Has("hearing_reserve")?2:0; } }
        private Point HearingDais
        { get { return ObjectPoint(0,"hearingdais",new Point(-100,-100)); } }
        public bool HearingPlaqueVisible(int region)
        { return HearingComplete && region>=1&&region<=3 && HearingVoice(region)!=0; }
        private bool TalkHearing(WorldObject o)
        {
            bool dais=save.room==0 && o.kind=="hearingdais";
            int region=save.room;
            bool voice=region>=1 && region<=3 && o.kind=="witnessnpc"+region;
            bool plaque=region>=1&&region<=3 && o.kind=="hearingplaque"+region;
            if(!dais && !voice && !plaque)return false;
            if(plaque)
            {
                if(!HearingPlaqueVisible(region))return true;
                string[] plaqueZh={"","密林的新誓言","遗迹的新誓言","矿场的新誓言"};
                string[] plaqueEn={"","Thornwood's Public Oath","The Ruins' Public Oath","Ironreach's Public Oath"};
                Dialog(plaqueZh[region],plaqueEn[region],
                    (HearingVoice(region)==1?
                        new[]{"","莫芽要求根系疗养优先，让树林能够自己选择下一次生长。","阿菱要求渡口永远记得没有许可证的旅人。","维恩要求矿工有权自己维修和关闭机器。"}[region]:
                        new[]{"","莫芽让夜路灯为迟归的人留一点光，也保留树林修复的余量。","阿菱要求渡口的应急河岸能容纳下次涨潮的家庭。","维恩要求任何生产开始前都先检查停机装置。"}[region])+
                    "\n\n居民亲手立下的公共承诺，已经在现场变成真正运作的设施。",
                    (HearingVoice(region)==1?
                        new[]{"","Moya places healing roots first, so the forest can decide how to grow again.","Arin guarantees passage even for travelers without freight papers.","Venn insists the workers maintain and shut down machines themselves."}[region]:
                        new[]{"","Moya preserves lanterns for late travelers without stripping roots of healing energy.","Arin provides refuge for families threatened by the next high tide.","Venn requires an emergency shutdown check before work can resume."}[region])+
                    "\n\nThe resident's signed public commitment now powers a real local service.",
                    C("阅读碑文","Read the pledge","close"));
                return true;
            }
            if(dais)
            {
                if(!HearingReady)
                {
                    Dialog(o.name,o.en,
                        "星雨尚未完成公开分配，或厄洛仍未说明最后的民用余量去向。请先完成第五章设施、第一场星雨与战后供能协定。",
                        "The storm's energy use and Orrin's remaining civilian reserves have not all been settled. Finish Chapter Five, the first storm and his post-storm accord before the public hearing.",
                        C("继续旅程","Continue","close"));return true;
                }
                if(HearingComplete)
                {
                    Dialog(o.name,o.en,
                        HearingVerdict==1?
                        "三地签名已经成了人人可阅读的公共档案。你选择把设施留给每一位居民；公会大厅的公共医护灯也因此开放。":
                        "三地签名已经进入公共档案。你选择让每一处公共线路都保留应急储备；公会大厅的备用耐力灯持续运作。",
                        HearingVerdict==1?
                        "Three regional commitments are now public records. You chose open civic access, and the guild's public healing lamp is active.":
                        "Three regional commitments are now public records. You retained shared emergency reserves, and the guild's spare-energy lamps remain active.",
                        C("查看已公开的议事档案","Review public hearing","hearing_read"),
                        C("继续旅程","Continue","close"));return true;
                }
                if(!Has("hearing_started"))
                {
                    Dialog(o.name,o.en,
                        "莉雅：我们已经修复三处公共设施，熬过第一场星雨，还让厄洛签字放弃旧军用独占。但这不是“从今以后大家都同意”的童话。\n\n莫芽希望森林能恢复，阿菱担心下一次涨潮，维恩仍不相信未经签名的停机保证。必须亲自找到他们，让每个人在自己熟悉的地区提出一份独立方案。\n\n三位居民签字后，我会主持公开听证。你必须选择公共医疗优先，或统一应急储备；两种结果将在真正的游戏世界生效。",
                        "Lyra: We rebuilt the public works, endured our first storm and obtained Orrin's signed civilian accord. But that is not the end of disagreement.\n\nMoya wants the forests to heal. Arin fears another flood. Venn cannot trust an unsigned shutdown rule. Visit them in their own regions and allow each to make a real choice. When all three sign, hold a public hearing: open medical access, or shared emergency reserves. Either verdict will have lasting effects in the game world.",
                        C("开启第七章 · 众人的晨钟","Begin Chapter Seven · The People's Dawn","hearing_begin"),
                        C("暂时离开","Not yet","close"));return true;
                }
                if(HearingSignatures<3)
                {
                    Dialog(o.name,o.en,
                        "已经有 "+HearingSignatures+"/3 位地区居民亲自署名。你不能替缺席者投票；请回到密林、渡口、矿场，分别听他们自己说完。",
                        HearingSignatures+"/3 living residents signed their own positions. You cannot vote for anyone who is not here. Return to Thornwood, the crossing and the mine.",
                        C("追踪尚未签字的居民","Track remaining witnesses","hearing_track"),
                        C("稍后再来","Later","close"));return true;
                }
                Dialog(o.name,o.en,
                    "莉雅：莫芽、阿菱、维恩已签署三份互不相同的公开承诺。现在决定这份议会公约接下来如何服务普通人。\n\n【公共开放】镇内医疗灯在安全时恢复生命，减少居民对旧军用设施的依赖。\n【应急储备】镇内公用耐力灯优先给巡路者补充体力，以便下一场灾难及时撤离。\n\n两种公约都承认三位居民的独立选择；这是一次真正的政治取舍，而不是谁对谁错。",
                    "Lyra: Moya, Arin and Venn signed distinct public commitments. Decide how the common charter serves the people now.\n\n[Open Access] The town's public medical lantern heals travelers outside combat, reducing dependence on military infrastructure.\n[Emergency Reserve] A shared energy lantern recharges roadwardens for the next disaster.\n\nBoth verdicts honor the regional residents. This is a real tradeoff, not a trick answer.",
                    C("公布公约 · 开放公共医疗","Publish charter · Open medical access","hearing_verdict_public"),
                    C("公布公约 · 保留应急供能","Publish charter · Maintain emergency reserve","hearing_verdict_reserve"),
                    C("与居民再商量","Consult the residents","close"));
                return true;
            }
            if(!HearingReady || !Has("hearing_started") ||
                HearingComplete || HearingVoice(region)!=0)return false;
            string[] zhWho={"","莫芽 · 森林的名字","阿菱 · 沉没遗迹的渡口","维恩 · 赤铁矿工"};
            string[] enWho={"","Moya · A Forest's Name","Arin · The Ruins' Crossing","Venn · An Ironreach Miner"};
            string[] zh={"",
                "莫芽：暴风雨过后，新的林灯指向城墙，根系却还没有完全愈合。我愿意为下一次修复署名，但不能再由远处的官员替森林决定它需要什么。",
                "阿菱：桥下水位开始上涨的时候，不是每个人都有公会货运证。我宁可一边修桥一边让大家上船，也不愿让孩子在水里等许可。",
                "维恩：我们终于有零件了，可机器随时会过热。我不想再听一份抽象的“会保护工人”；我们的停机协议必须由我们自己执行。"};
            string[] en={"",
                "Moya: Storm lanterns point back to the citadel, but our roots are not yet healed. I will sign the next plan, if forest residents—not distant officials—decide what it does.",
                "Arin: Water rises before anyone can ask for a freight permit. I'd rather keep the crossing open and bring everybody to safety than leave children waiting.",
                "Venn: We finally have spare parts, but these machines still run hot. I want workers themselves to enforce shutdown, not another abstract promise of protection."};
            string[] careZh={"","优先修复根系疗养苗圃","维护所有人的自由渡口","让工人掌控修复与零件"};
            string[] careEn={"","Prioritize healing living roots","Keep the public crossing open","Let workers control real repairs"};
            string[] watchZh={"","优先维护夜路照明","优先保留河岸防洪灯","优先确保紧急停机"};
            string[] watchEn={"","Maintain night-road emergency lights","Keep flood shelters ready","Guarantee emergency shutdown first"};
            Dialog(zhWho[region],enWho[region],zh[region],en[region],
                C(careZh[region],careEn[region],"hearing_voice_"+region+"_care"),
                C(watchZh[region],watchEn[region],"hearing_voice_"+region+"_watch"),
                C("暂时不决定","Not yet","close"));
            return true;
        }
        private bool ChooseHearing(string action)
        {
            if(action=="hearing_read")
            {
                if(HearingComplete)WorldChroniclePage("hearing");
                return true;
            }
            if(action=="hearing_track")
            {
                if(Has("hearing_started"))TrackQuest("hearing");
                return true;
            }
            if(action=="hearing_begin")
            {
                if(!HearingReady || Has("hearing_started") ||
                    save.room!=0 || Dying || practicing ||
                    (Position-HearingDais).Length>2.2f)return true;
                Flag("hearing_started");TrackQuest("hearing");
                RecordMoment("第七章：莉雅在余烬镇召开真正的居民听证。每个曾受星核影响的人必须能亲自决定公共设施的未来。",
                    "Chapter Seven: Lyra convenes a genuine public hearing. People who lived through the cores' cost must each speak for themselves.");
                Notify("公开听证开始。分别拜访莫芽、阿菱、维恩，听取并记录三地的真实选择。",
                    "Public hearing opened. Visit Moya, Arin and Venn for three independently signed civic choices.");
                return true;
            }
            if(action.StartsWith("hearing_voice_"))
            {
                string[] parts=action.Split('_');
                int region;
                if(parts.Length!=4 || !int.TryParse(parts[2],out region) ||
                    region<1 || region>3 ||
                    (parts[3]!="care" && parts[3]!="watch") ||
                    !HearingReady || !Has("hearing_started") ||
                    HearingComplete || HearingVoice(region)!=0 ||
                    save.room!=region || Dying || practicing ||
                    (Position-ObjectPoint(region,"witnessnpc"+region,
                        new Point(-100,-100))).Length>2.2f)return true;
                Flag(action);revision++;sound="quest";
                RecordMoment(
                    new[]{"","莫芽","阿菱","维恩"}[region]+"亲自签署了自己的第七章公民选择。不是主角代替其投票。",
                    new[]{"","Moya","Arin","Venn"}[region]+
                    " personally signed a Chapter Seven decision rather than being voted for by the hero.");
                Notify("真实地区选择已记录："+HearingSignatures+"/3。可继续拜访另一位居民。",
                    "Living regional choice registered: "+HearingSignatures+"/3. Visit another resident.");
                return true;
            }
            if(action=="hearing_verdict_public"||action=="hearing_verdict_reserve")
            {
                if(!HearingReady || !Has("hearing_started") ||
                    HearingComplete || HearingSignatures!=3 ||
                    save.room!=0 || Dying || practicing ||
                    (Position-HearingDais).Length>2.2f)return true;
                bool open=action=="hearing_verdict_public";
                Flag(open?"hearing_public":"hearing_reserve");
                Flag("hearing_complete");
                save.coins+=85;save.shards+=2;AddXp(100);
                revision++;sound="reward";
                RecordMoment(open?
                    "三位居民的选择经公开听证后生效：余烬镇公民医疗灯开放，三地的碑文和照护责任永久保留。":
                    "三位居民的选择经公开听证后生效：余烬镇保留公共紧急耐力供给，地区照护方案不再由军队独断。",
                    open?
                    "Three residents' independent decisions were ratified. Emberfall's open medical lantern operates, and all three local pledges remain binding.":
                    "Three residents' independent decisions were ratified. Emberfall retains emergency energy reserves under public, not military, control.");
                Notify("第七章完成：真实公民公约已生效，硬币 +85、碎片 +2、经验 +100。",
                    "Chapter Seven complete. Real civic verdict enacted; +85 coins, +2 shards and +100 XP.");
                return true;
            }
            return false;
        }
        public bool HearingHealthSite
        {
            get { return HearingComplete && save.room>=1 && save.room<=3 &&
                (save.room==3 ? HearingVoice(3)==2 : HearingVoice(save.room)==1); }
        }
        public bool HearingEnergySite
        {
            get { return HearingComplete && save.room>=1 && save.room<=2 &&
                HearingVoice(save.room)==2; }
        }
    }
}
