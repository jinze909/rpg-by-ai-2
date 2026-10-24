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
        public int HearingOrrinResponsibility
        { get { return Has("hearing_orrin_aid")?1:Has("hearing_orrin_patrol")?2:0; } }
        private Point HearingDais
        { get { return ObjectPoint(0,"hearingdais",new Point(-100,-100)); } }
        public bool HearingPlaqueVisible(int region)
        { return HearingComplete && region>=1&&region<=3 && HearingVoice(region)!=0; }
        private bool TalkHearing(WorldObject o)
        {
            bool dais=save.room==0 && o.kind=="hearingdais";
            bool orrin=save.room==4 && o.kind=="shrine" && HearingComplete;
            int region=save.room;
            bool voice=region>=1 && region<=3 && o.kind=="witnessnpc"+region;
            bool plaque=region>=1&&region<=3 && o.kind=="hearingplaque"+region;
            if(!dais && !voice && !plaque && !orrin)return false;
            if(!HasPhysicalInteractionReach(o))return true;
            if(orrin)
            {
                if(HearingOrrinResponsibility!=0)
                {
                    Dialog("厄洛 · 民用责任协议","Orrin · Civilian Responsibility Pact",
                        HearingOrrinResponsibility==1?
                            "厄洛：医护已经在城墙外搭起炉子。那两份热汤送到时，刚好有个孩子烧得浑身发抖。今晚他们能在避难所睡个暖觉了。":
                            "厄洛：巡路队今早就出发了。我把两份零件交给修灯的维恩；他们不必再到军械库门口求人。祭坛旁也留了补给，走远路的人用得着。",
                        HearingOrrinResponsibility==1?
                            "Orrin: The medics have a stove outside the wall now. The two hot meals arrived just as a feverish child was carried in. At least they'll sleep warm tonight.":
                            "Orrin: The road patrol left at dawn. I gave Venn two spare parts for the lamps; no one has to beg at the armory door now. There's a supply point by the shrine for anyone walking the long road.",
                        C("重读旧民用余量协议","Review the earlier civilian reserve accord","hearing_orrin_review"),
                        C("继续前行","Continue","close"));
                    return true;
                }
                Dialog("厄洛 · 民用责任协议","Orrin · Civilian Responsibility Pact",
                    "厄洛把三张署名纸压在石台上。\n\n“当年有人命令我守住这堵墙。”他望向墙外的烟火，“可人都在墙外。我究竟守的是什么？”\n\n"+
                    (HearingVerdict==1?
                        "你们最终开放公共医疗。堡垒仍存着能给孩子们取暖的热汤，我愿意让医护先去城外帮助真正需要的人。":
                        "你们最终保留民用应急供能。我愿意派昔日的守卫维护星路，并把真正用得上的零件交出去。")+
                    "\n\n“两件事我都欠他们，但这批人手和物资只能先办一件。你替我带个准信。”",
                    "Orrin lays the three signed sheets on the stone.\n\n'I was ordered to hold this wall,' he says, watching smoke rise beyond it. 'But the people are out there. What was I supposed to be guarding?'\n\n"+
                    (HearingVerdict==1?
                        "You opened civic medical access. The citadel still has meals that could warm children, and I can send our medics beyond the wall.":
                        "You maintained civilian emergency reserves. My former guards can now patrol the shared starroads and release genuine machine parts.")+
                    "\n\n'I owe them both. But I only have enough hands and stores to do one first. Tell me where to send them.'",
                    C("移交医护补给 · 公共避难所","Send medical stores to the public shelter","hearing_orrin_aid"),
                    C("派遣志愿巡路队 · 公开维修","Send volunteer wardens to public roads","hearing_orrin_patrol"),
                    C("重读原有余量协定","Review the existing reserve accord","hearing_orrin_review"),
                    C("稍后决定","Not yet","close"));
                return true;
            }
            if(plaque)
            {
                if(!HearingPlaqueVisible(region))return true;
                string[] plaqueZh={"","密林的新誓言","遗迹的新誓言","矿场的新誓言"};
                string[] plaqueEn={"","Thornwood's Public Oath","The Ruins' Public Oath","Ironreach's Public Oath"};
                Dialog(plaqueZh[region],plaqueEn[region],
                    (HearingVoice(region)==1?
                        new[]{"","莫芽要求根系疗养优先，让树林能够自己选择下一次生长。","阿菱要求渡口永远记得没有许可证的旅人。","维恩要求矿工有权自己维修和关闭机器。"}[region]:
                        new[]{"","莫芽让夜路灯为迟归的人留一点光，也保留树林修复的余量。","阿菱要求渡口的应急河岸能容纳下次涨潮的家庭。","维恩要求任何生产开始前都先检查停机装置。"}[region])+
                    "\n\n碑脚新凿的字还没有被雨冲淡。夜里有人来添灯油，清晨有人清理路面。",
                    (HearingVoice(region)==1?
                        new[]{"","Moya places healing roots first, so the forest can decide how to grow again.","Arin guarantees passage even for travelers without freight papers.","Venn insists the workers maintain and shut down machines themselves."}[region]:
                        new[]{"","Moya preserves lanterns for late travelers without stripping roots of healing energy.","Arin provides refuge for families threatened by the next high tide.","Venn requires an emergency shutdown check before work can resume."}[region])+
                    "\n\nFresh chisel dust clings to the base. Someone trims the lamp at dusk and sweeps the path at dawn.",
                    C("阅读碑文","Read the pledge","close"));
                return true;
            }
            if(dais)
            {
                if(!HearingReady)
                {
                    Dialog(o.name,o.en,
                        "讲台上还压着几张空白的署名纸。莉雅在背面写了句：等工事修好、第一场星雨过去，厄洛也交代清楚余量，再请三地的人来。",
                        "Blank signature sheets lie on the dais. Lyra has written on the back: wait until the works are repaired, the first storm has passed, and Orrin accounts for the remaining reserves.",
                        C("继续旅程","Continue","close"));return true;
                }
                if(HearingComplete)
                {
                    Dialog(o.name,o.en,
                        HearingVerdict==1?
                        "公会门前多了一盏医疗灯。三地的签名纸钉在讲台上，纸角卷了边，仍有人每天来读。":
                        "讲台上挂着三地签名。公会的备用供能灯彻夜亮着，巡路人临出门都会停下来补足力气。",
                        HearingVerdict==1?
                        "A healing lamp burns outside the guild. Three signed sheets are nailed to the dais, their corners curling from all the hands that have turned them.":
                        "Three signed sheets hang by the dais. The guild's reserve lamp stays lit all night; roadwardens stop there before setting off.",
                        C("查看已公开的议事档案","Review public hearing","hearing_read"),
                        C("继续旅程","Continue","close"));return true;
                }
                if(!Has("hearing_started"))
                {
                    Dialog(o.name,o.en,
                        "莉雅把三封信摊在讲台上，手指按住被风掀起的纸角。\n\n“莫芽说林根还疼；阿菱写河水又涨了；维恩说矿机昨天差点烫伤人。他们都肯签字，条件是没人替他们写。”\n\n“去见见他们吧。等三个人的意见都带回来，我们再决定镇里的灯先留给医护，还是留作灾时的应急供能。”",
                        "Lyra spreads three letters across the dais, holding down the corners against the wind.\n\n'Moya says the roots still hurt. Arin says the river is rising again. Venn says a miner nearly burned his hands yesterday. They'll sign, but not to words someone else puts in their mouths.'\n\n'Go and hear them. Once all three send their answers, we can decide whether the town lamps serve the medics first or stay ready for the next evacuation.'",
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
                    "莉雅把三张签名纸并排钉好。\n\n“他们都同意让镇里的灯归大家用。可是今晚先给谁？”\n\n“给医护，没钱买药的旅人也能在公会歇口气、养好伤。留作应急供能，巡路队就能在下一场星雨前多跑几趟。”\n\n“做决定吧。我会把署名和结果一起贴出去。”",
                    "Lyra pins the three signed sheets side by side.\n\n'Everyone agrees the town lamps must serve the people. But who needs them tonight?'\n\n'Give them to the medics, and injured travelers can rest at the guild. Keep the emergency reserve, and our roadwardens can make another run before the next storm.'\n\n'Make the call. I'll post the signatures beside it.'",
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
                "莫芽扒开泥土，露出一道没长合的根。“灯倒是修好了。你看这个，下面的伤口还在淌汁。下一批晶石往哪儿送，先问问住在林子里的人。”",
                "阿菱把湿透的渡船名册拍在桌上。“上次涨水，守卫先要通行证。那对母子根本没证，水已经淹到小孩脖子了。下次我先放船，再填表。”",
                "维恩亮出烫红的手套。“新零件有了，老机器还在冒火。上次工头没等我们喊停就开闸。以后停机钥匙得留在工人手上。”"};
            string[] en={"",
                "Moya pulls back the wet soil to show a root that hasn't healed. 'The lamps are fixed, sure. Look underneath. It's still bleeding sap. Ask the forest folk where the next crystals go.'",
                "Arin slaps a soaked ferry register onto the table. 'Last flood, the guards asked for papers first. A mother and her boy had none. Water was up to his neck. Next time, I launch the boat and fill out the forms later.'",
                "Venn holds up a glove scorched at the fingertips. 'We've got spare parts, and the machine still spits fire. Last time the foreman opened the valve before we called clear. The shutdown key stays with the workers now.'"};
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
            if(action=="hearing_orrin_review")
            {
                if(save.room==4 && HearingComplete &&
                    HasPhysicalInteractionReach(ObjectPoint(4,"shrine",new Point(-100,-100))))
                {
                    foreach(WorldObject shrine in Map.objects)
                        if(shrine.kind=="shrine"){TalkStorm(shrine);break;}
                }
                return true;
            }
            if(action=="hearing_orrin_aid" || action=="hearing_orrin_patrol")
            {
                if(!HearingReady || !HearingComplete ||
                    HearingOrrinResponsibility!=0 || save.room!=4 ||
                    Dying || practicing ||
                    !HasPhysicalInteractionReach(ObjectPoint(4,"shrine",new Point(-100,-100))))
                    return true;
                bool aid=action=="hearing_orrin_aid";
                // Each responsibility is a one-time transfer. Do not mark
                // it fulfilled until BOTH physical supplies actually fit.
                if(!HasInventoryRoom(aid ? 6 : 4, 2)) return true;
                CreditInventory(aid ? 6 : 4, 2);
                Flag(action);
                revision++;sound="quest";
                RecordMoment(aid?
                    "厄洛兑现战后责任：两份真实热汤交给公共医护，堡垒平民避难所获得长期治疗支持。":
                    "厄洛兑现战后责任：两份真实零件交给巡路志愿队，堡垒守护者开放持续耐力补给。",
                    aid?
                    "Orrin made good on his promise: two real meals reached civilian medics, and the citadel shelter gained public healing support.":
                    "Orrin made good on his promise: two real repair parts went to volunteer roadwardens, and the citadel opened an emergency energy point.");
                Notify(aid?
                    "厄洛已将两份热汤转为公共医护物资。避难所安全时将持续疗养。":
                    "厄洛已移交两份维修零件。堡垒祭坛成为安全的公用耐力补给点。",
                    aid?
                    "Two real meals transferred. Public shelter healing is now available outside combat.":
                    "Two genuine parts transferred. Citadel shrine energy support now works outside combat.");
                return true;
            }
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
                    !HasPhysicalInteractionReach(HearingDais))return true;
                Flag("hearing_started");TrackQuest("hearing");
                RecordMoment("莉雅把三张空白署名纸交给你，等密林、渡口和矿场的回信。",
                    "Lyra hands you three blank signature sheets, one each for the forest, the crossing and the mine.");
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
                    !HasPhysicalInteractionReach(ObjectPoint(region,"witnessnpc"+region,
                        new Point(-100,-100))))return true;
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
                    !HasPhysicalInteractionReach(HearingDais))return true;
                bool open=action=="hearing_verdict_public";
                Flag(open?"hearing_public":"hearing_reserve");
                Flag("hearing_complete");
                // One-time civic outcomes are permanent even at a full
                // wallet. The published v2 save must not silently truncate
                // chapter-seven currency when reopened.
                CreditCoins(85); CreditShards(2); AddXp(100);
                revision++;sound="reward";
                RecordMoment(open?
                    "三位居民的选择经公开听证后生效：余烬镇公民医疗灯开放，三地的碑文和照护责任永久保留。":
                    "三位居民的选择经公开听证后生效：余烬镇保留公共紧急耐力供给，地区照护方案不再由军队独断。",
                    open?
                    "Three residents' independent decisions were ratified. Emberfall's open medical lantern operates, and all three local pledges remain binding.":
                    "Three residents' independent decisions were ratified. Emberfall retains emergency energy reserves under public, not military, control.");
                Notify("听证结束。莉雅把结果贴在公会门外。硬币 +85、碎片 +2、经验 +100。",
                    "The hearing is over. Lyra posts the verdict outside the guild. +85 coins, +2 shards, +100 XP.");
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
