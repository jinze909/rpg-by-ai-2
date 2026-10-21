using System;
using System.Collections.Generic;

namespace Starfall
{
    // Chapter Six, The Commons' First Night: player choices share a strictly
    // limited power budget and a live, navigable civic grid during a starstorm.
    // Original ending and all version-two save fields remain untouched.
    public sealed partial class GameCore
    {
        private bool stormTuning;
        private float stormTuneTime, stormPulse;
        private int stormTuneStep;
        public bool StormTuning { get { return stormTuning; } }
        public float StormTuneTime { get { return stormTuneTime; } }
        public int StormTuneStep { get { return stormTuneStep; } }
        public int StormAllocations
        {
            get { int n=0;for(int i=1;i<=3;i++)if(StormPolicy(i)!=0)n++;return n; }
        }
        public int StormBudgetSpent
        {
            get { int n=0;for(int i=1;i<=3;i++)n+=StormPolicy(i)==1?3:StormPolicy(i)==2?2:0;return n; }
        }
        public int StormBudgetLeft { get { return 8-StormBudgetSpent; } }
        public int StormReliefCount
        { get { int n=0;for(int i=1;i<=3;i++)if(Has("storm_relief_"+i))n++;return n; } }
        public bool StormReliefAll { get { return StormReliefCount==3; } }
        public int StormPolicy(int region)
        {
            if(region<1 || region>3)return 0;
            return Has("storm_region_"+region+"_shelter")?1:
                Has("storm_region_"+region+"_grid")?2:0;
        }
        public bool StormChapterComplete { get { return Has("storm_complete"); } }
        public int StormNextNode
        {
            get
            {
                int[] order=save.ending==1?new[]{1,2,3}:
                    save.ending==2?new[]{3,2,1}:new[]{2,1,3};
                return stormTuneStep>=3?0:order[stormTuneStep];
            }
        }
        private Point StormPoint(int room,string kind)
        { return ObjectPoint(room,kind,new Point(-100,-100)); }
        private void ResetStormTransient()
        {
            hazards.RemoveAll(h=>h.stormOwned);
            effects.RemoveAll(e=>e.stormOwned);
            stormTuning=false;stormTuneTime=0;stormTuneStep=0;stormPulse=0;
        }
        private bool StormReady
        {get{return Has("afterlight_complete")&&AfterlightRestored==3&&AfterlightStewards==3&&save.ending>0;}}
        private string StormRegionName(int region,bool en)
        {
            string[] zh={"","密林的根苗圃","遗迹的公共渡口","矿场的安全工房"};
            string[] english={"","Thornwood's public roots","The Ruins' public sluice","Ironreach's safe foundry"};
            return en?english[region]:zh[region];
        }
        private string StormOrder(bool en)
        {
            if(save.ending==1)return en?"Grove → River → Mine":"密林 → 河道 → 矿场";
            if(save.ending==2)return en?"Mine → River → Grove":"矿场 → 河道 → 密林";
            return en?"River → Grove → Mine":"河道 → 密林 → 矿场";
        }
        private bool TalkStorm(WorldObject o)
        {
            string kind=o.kind;
            bool council=kind=="stormcouncil"&&save.room==0;
            int site=0;bool regionNode=false,townNode=false;
            if(kind.StartsWith("stormrelay"))
            {
                regionNode=int.TryParse(kind.Substring("stormrelay".Length),out site)&&
                    site>=1&&site<=3&&save.room==site;
            }
            if(kind.StartsWith("stormnode"))
            {
                townNode=int.TryParse(kind.Substring("stormnode".Length),out site)&&
                    site>=1&&site<=3&&save.room==0;
            }
            // Original named residents return to the story. The older witness
            // investigation must remain reachable through its own action.
            if(save.room>=1&&save.room<=3 &&
                kind=="witnessnpc"+save.room && Has("storm_started") &&
                StormPolicy(save.room)!=0)
            {
                int region=save.room;
                bool shelter=StormPolicy(region)==1;
                string[] zhS={"",
                    "莫芽：我们让受伤的根系休养，但夜间巡路灯暂时少了光。保护活着的森林值得付出一点不便。",
                    "阿菱：孩子们在安全河岸休息，渡船夜间不得不减速。没有货运许可证的人终于也有灯了。",
                    "维恩：矿机及时停下，工友可以先躲避星雨。今天的产量少了，但没人该用生命换配额。"};
                string[] enS={"",
                    "Moya: Our wounded roots can rest, though fewer road lamps stay lit. The living forest is worth a slower night.",
                    "Arin: Children shelter on the riverbank while ferries run more slowly. Even those without freight permits have a lamp.",
                    "Venn: The machines stopped safely and workers sought refuge. Output is lower, but nobody should pay a quota with their life."};
                string[] zhG={"",
                    "莫芽：星灯让行路者平安穿林，但受伤的根系还需要有人照料。亮着的道路不能代替森林。",
                    "阿菱：渡口继续运药、运人。我们保住了船路，可不能忘记在寒夜里需要热汤的孩子。",
                    "维恩：公共工房继续运作，节省了珍贵星晶。但过热时停机的权利依然不能撤销。"};
                string[] enG={"",
                    "Moya: Lamps light the road, but the wounded roots still need tending. A lit road cannot replace a living forest.",
                    "Arin: Ferries still carry medicine and travelers, but we must remember the cold children who need hot food.",
                    "Venn: Our public workshop saves precious starcrystal, but no one may revoke the right to stop an overheating machine."};
                var options=new List<Choice>();
                if(!Has("storm_relief_"+region))
                    options.Add(C("捐出热汤1份帮助星雨受灾者",
                        "Donate one stew to storm-affected residents","storm_relief_"+region));
                options.Add(C("继续此前的居民证词",
                    "Continue the earlier resident testimony","storm_oldstory_"+region));
                options.Add(C("继续旅行","Travel on","close"));
                Dialog(o.name,o.en,
                    (shelter?zhS[region]:zhG[region])+
                    (Has("storm_relief_"+region)?"\n\n你捐出的热汤已经送到了真正需要的人手里。":
                        "\n\n如果愿意，可将背包中真实的一份热汤送给今晚受寒的居民。"),
                    (shelter?enS[region]:enG[region])+
                    (Has("storm_relief_"+region)?"\n\nYour donated stew reached the people who needed it.":
                        "\n\nIf you wish, donate one real stew from your inventory to neighbors suffering tonight."),
                    options.ToArray());
                return true;
            }
            // Orrin is no longer a boss after the original ending. Meeting him
            // again at the real Citadel shrine changes how the remaining civic
            // reserve is used instead of replaying the old fight.
            if(kind=="shrine" && save.room==4 && Has("boss_4") &&
                StormChapterComplete)
            {
                string[] zz={"","你归还了星核，我原以为城外会再次陷入黑暗。","你保住了屏障，我也不能再假装城外不在承受它的费用。","你把能源交给共同维护，我终于不必独自替每一户人作决定。"};
                string[] ee={"","You returned the cores; I feared the outer roads would go dark again.","You kept the shield, but I can no longer deny what the outer communities pay for it.","You put the cores into shared stewardship, so I need not decide the fate of every household alone."};
                if(Has("storm_orrin_open")||Has("storm_orrin_guard")||
                    StormBudgetLeft==0)
                {
                    Dialog("厄洛 · 战后守望人","Orrin · After the First Storm",
                        "厄洛："+zz[save.ending]+"\n\n"+
                        (Has("storm_orrin_open")?
                            "我已经把剩余公用余量交给余烬镇的开放避难与疗养线路。这盏灯也照向我城墙外的人。":
                            Has("storm_orrin_guard")?
                            "我将剩余公用余量保留给堡垒的民用应急照明。这一次不再为战争，而是为那些有可能再次在夜里失去归路的人。":
                            "公共储备已全部分配给三地，城内已经没有可以擅自重划的额度。我会遵守居民亲自做出的选择。"),
                        "Orrin: "+ee[save.ending]+"\n\n"+
                        (Has("storm_orrin_open")?
                            "I assigned the unspent civic reserve to Emberfall's public refuge lights. Their glow serves people outside my former walls.":
                            Has("storm_orrin_guard")?
                            "I assigned the remaining reserve to civilian emergency lamps inside the citadel, no longer as instruments of war.":
                            "All eight reserve units are allocated to the living regions. I have no hidden surplus to overrule them with."),
                        C("继续守护这条道路","Keep the road open","close"));return true;
                }
                Dialog("厄洛 · 战后守望人","Orrin · After the First Storm",
                    "厄洛："+zz[save.ending]+"\n\n"+
                    "我看见三地按自己的规则渡过了第一次星雨。还有 "+StormBudgetLeft+
                    " 单位公用余量，你可以和我共同决定如何使用。"+
                    "\n\n将它开放给余烬镇的公共避难所，那里会有更多后来的伤者得到照顾；或留作堡垒居民的应急灯源，让上一次长夜不再发生。两者都不将决定权交还给旧军令。",
                    "Orrin: "+ee[save.ending]+"\n\n"+
                    "I saw three living communities endure their first storm on their own terms. "+
                    StormBudgetLeft+" civic reserve units remain. "+
                    "We can open them to the public refuge in Emberfall, or reserve them for civilian emergency lights at the citadel so the blackout never repeats. Neither choice returns authority to the old military order.",
                    C("将剩余余量开放给镇民避难所","Open remaining power to the town refuge","storm_orrin_open"),
                    C("为堡垒居民保留民用应急照明","Keep civilian backup lamps at the citadel","storm_orrin_guard"),
                    C("暂不决定","Not yet","close"));
                return true;
            }
            if(!council&&!regionNode&&!townNode)return false;
            if(council)
            {
                if(!StormReady)
                {
                    Dialog(o.name,o.en,
                        "三地公共设施尚未全部修复并由居民亲自署名。真正的星雨分配会议必须等到第五章完成，不能提前改动你的结局。",
                        "The regional public works are not finished and signed. The first-starstorm council opens after Chapter Five, not before your original ending.",
                        C("明白","Understood","close"));return true;
                }
                if(StormChapterComplete)
                {
                    Dialog(o.name,o.en,
                        "莉雅：我们撑过了第一次没有军用独断命令的星雨。有人优先照顾庇护所，有人选择让工房不停运转；每一笔储备分配都公开了，而且每个地区亲自承担了后果。"+
                        (StormReliefAll?"莫芽、阿菱和维恩也已将你捐出的三份热汤送给受寒的居民。":"")+
                        " 公共道路将记住能源与互助。",
                        "Lyra: We endured our first storm without an unquestionable military order. Some towns prioritized shelters; others kept essential work powered. Every allocation is public, and its cost is shared."+
                        (StormReliefAll?" Moya, Arin and Venn also distributed all three hot meals you donated to people left in the cold.":"")+ 
                        " The public roads will remember both our power and our care.",
                        C("阅读本章编年册","Read Chapter Six chronicle","storm_read"),
                        C("继续探索","Continue exploring","close"));return true;
                }
                if(!Has("storm_started"))
                {
                    string consequence=save.ending==1?
                        "你让土地重新获得了星核，旧屏障的承诺因此不再足够。":
                        save.ending==2?
                        "城墙的供能仍在消耗外界，城外居民需要一个不会被屏障随时挤占的储备。":
                        "共同治理终于建成了设施，现在必须证明它们能在真正的危险中同时运转。";
                    string consequenceEn=save.ending==1?
                        "You returned the cores to the land; the old shield promise cannot protect everyone on its own.":
                        save.ending==2?
                        "The wall still draws from outer communities. They need reserves the shield cannot seize without consent.":
                        "The commons finally built public services. Now the people must prove they work together under real danger.";
                    Dialog(o.name,o.en,
                        "莉雅：星陨后第一场真正的星雨正在靠近。"+consequence+
                        "\n\n公会公共储备只有八单位，三地各要决定优先保障庇护所（三单位）还是基础供能（二单位）。你不可能替所有人选更昂贵的方案，但每片地区都至少能得到一个可用的答案。\n\n"+
                        "你必须亲自到修好的三个公共站安装方案；未稳定的地区会遭遇真实的星雨电弧。然后回到这里，依照你原先的结局所确定的供能顺序，奔走激活三个镇内星灯节点。没有自动胜利。",
                        "Lyra: The frontier's first true starstorm is approaching. "+consequenceEn+
                        "\n\nThe public reserve holds just eight units. Each district must choose shelter-first protection (three units), or continuous utilities (two units). You cannot promise the expensive plan everywhere, but every region can receive real support.\n\n"+
                        "Install a policy at all three repaired public sites. Unprotected regions suffer real starstorm arcs. Then return to tune three actual town lamp nodes in the order determined by the ending you chose. There is no automatic victory.",
                        C("开启第六章 · 公约第一夜","Begin Chapter Six · First Night of the Commons","storm_begin"),
                        C("先准备","Prepare first","close"));return true;
                }
                if(StormAllocations<3)
                {
                    Dialog(o.name,o.en,
                        "星雨已经来到。公用储备共八单位，已分配 "+StormBudgetSpent+
                        "，余量 "+StormBudgetLeft+"；已稳定 "+StormAllocations+"/3 个地区。\n\n"+
                        "请亲自到密林、遗迹、矿场的星雨配电台决定是三单位的庇护，还是两单位的公共供能。避免仍在电弧中暴露的道路。",
                        "The storm has arrived. Public reserve: eight units. Assigned: "+
                        StormBudgetSpent+"; remaining: "+StormBudgetLeft+"; stabilized: "+
                        StormAllocations+"/3.\n\n"+
                        "Visit three actual storm relays and choose three-unit shelter protection or two-unit utility continuity. Beware the live arcs on roads yet to be stabilized.",
                        C("追踪下一个地区","Track next region","storm_track"),
                        C("继续准备","Prepare","close"));return true;
                }
                if(Has("storm_tuned"))
                {
                    Dialog(o.name,o.en,
                        "三座镇内公共星灯已经依次调谐。星雨最强的夜晚过去了，三片地区各自承担了选择，没有任何人可以再偷偷重新分配他们的光。\n\n"+
                        "现在可以公布经过实际压力检验的公约。",
                        "All three town nodes are tuned. The fiercest night has passed, and the three regions endured according to their own choices. Nobody can silently take back their light.\n\n"+
                        "Publish the charter that has now survived an actual storm.",
                        C("公布第一次星雨分配结果 · 完成第六章","Publish first-storm allocations · Finish Chapter Six","storm_publish"),
                        C("稍后","Later","close"));return true;
                }
                if(stormTuning)
                {
                    Dialog(o.name,o.en,
                        "公用星灯仍在实际负载调谐中，剩余 "+Math.Ceiling(stormTuneTime)+
                        " 秒。下一个镇内节点是 "+StormRegionName(StormNextNode,false)+
                        "。按顺序亲自走近并操作，不能在这块议事牌上遥控完成。",
                        "The civic grid is live, with "+Math.Ceiling(stormTuneTime)+
                        " seconds remaining. Next town node: "+StormRegionName(StormNextNode,true)+
                        ". Walk to it and interact in person; the council cannot remotely complete the grid.",
                        C("继续调谐","Continue tuning","close"),
                        C("中止调谐 · 保留地区方案","Abort tuning · Keep allocations","storm_abort"));return true;
                }
                Dialog(o.name,o.en,
                    "三地已经按各自选择稳定，八单位公共储备中使用了 "+
                    StormBudgetSpent+"，城市留有 "+StormBudgetLeft+" 单位余量。\n\n"+
                    "现在需要让居民实际启动三条星灯线路。按照你的原有结局，顺序为 "+
                    StormOrder(false)+"。启动后有95秒，必须按序在镇上三个分散的设备处亲自按E完成调谐。失败可免费重试。",
                    "All regions are stable. Assigned "+StormBudgetSpent+
                    " of eight reserve units, leaving "+StormBudgetLeft+" for the town.\n\n"+
                    "Now physically tune the distributed town grid. Your original ending determines the order: "+
                    StormOrder(true)+". Once started you have 95 seconds to press E at three separate physical town switches, in order. Retries are free.",
                    C("启动95秒实地星灯调谐","Start 95-second civic grid tuning","storm_tune_start"),
                    C("暂缓","Later","close"));return true;
            }
            if(!Has("storm_started"))
            {
                Notify("先到余烬镇公约议事台，正式开启第一场星雨调查。",
                    "Begin the first-starstorm story at the Emberfall council before touching these nodes.");
                return true;
            }
            if(regionNode)
            {
                int chosen=StormPolicy(site);
                if(chosen!=0)
                {
                    Dialog(o.name,o.en,
                        chosen==1?
                            "当地人决定先保障最脆弱的人。庇护灯提供真正的安全恢复区；其他地区的分配不会被这座机器代替。":
                            "当地人决定先保持基础设施不断电。公共灯与维修工房会因此保有更多运行能力；这项能源用途记录不得被军方私自改写。",
                        chosen==1?
                            "The community protected vulnerable people first. Shelter lamps create real safe recovery areas and do not substitute for other regions' decisions.":
                            "The community kept essential utilities running. Public lamps and workbenches gain real power, and the military cannot rewrite the signed allocation.",
                        C("继续前行","Continue","close"));return true;
                }
                Dialog(o.name,o.en,
                    "星雨正在打击这片地区。八单位储备剩余 "+StormBudgetLeft+
                    "。当地三位证人提出两种必须真正安装的方案：\n\n"+
                    "庇护优先：消耗三单位公用储备、木材1和月光草1，提供区域公共疗养灯。\n"+
                    "供能优先：消耗两单位储备、星晶2和零件1，维持重要路灯、渡船与矿场加工。"+
                    "\n\n只要选定并支付材料就会立即稳定这片地区。每片区域必须做出自己的公开决定。",
                    "The starstorm is striking this district. Reserve available: "+StormBudgetLeft+
                    " of eight. Its residents propose two actual installations:\n\n"+
                    "Shelter first: three reserve units, one timber and one Moonherb, creating a public recovery refuge.\n"+
                    "Utilities first: two reserve units, two starcrystals and one machine part, keeping essential lights and workshops running.\n\n"+
                    "A successfully installed policy immediately stabilizes this region. Each must make its own public decision.",
                    C("落实庇护优先 · 储备3、木材1、药草1",
                        "Install shelters · 3 reserve, 1 timber, 1 herb","storm_policy_"+site+"_shelter"),
                    C("落实公共供能 · 储备2、星晶2、零件1",
                        "Install utilities · 2 reserve, 2 crystal, 1 part","storm_policy_"+site+"_grid"),
                    C("先备材料","Prepare first","close"));return true;
            }
            if(!stormTuning)
            {
                Notify("三个镇内星灯尚未开始实地调谐。先去公约议事台。",
                    "The civic grid tuning has not started. Begin it at the public council.");
                return true;
            }
            Dialog(o.name,o.en,
                "这座公共星灯必须由冒险者亲自激活。记录要求第 "+
                (stormTuneStep+1)+"/3 步："+StormRegionName(StormNextNode,false)+
                "。电弧会真实击打逗留在预警区域里的人。",
                "This lamp must be tuned by someone standing here. Required step "+
                (stormTuneStep+1)+"/3: "+StormRegionName(StormNextNode,true)+
                ". The live electrical arcs can strike someone who remains in a warning circle.",
                C("调谐这座公共星灯","Tune this physical lamp","storm_node_"+site),
                C("继续巡视","Continue exploring","close"));return true;
        }
        private bool ChooseStorm(string action)
        {
            if(action=="storm_orrin_open"||action=="storm_orrin_guard")
            {
                if(save.room!=4 || !Has("boss_4") || !StormChapterComplete ||
                    StormBudgetLeft<=0 || Dying || practicing ||
                    Has("storm_orrin_open") || Has("storm_orrin_guard") ||
                    (Position-StormPoint(4,"shrine")).Length>2.2f)return true;
                Flag(action);revision++;sound="quest";
                RecordMoment(action=="storm_orrin_open"?
                    "厄洛与主角将公用余量交给余烬镇开放疗养站。城墙外也可以有灯。":
                    "厄洛与主角保留了堡垒的民用应急照明。旧屏障不得再借安全的名义独占决策。",
                    action=="storm_orrin_open"?
                    "Orrin and the hero directed the surplus civic reserve to Emberfall's open shelter. Light now reaches beyond the old walls.":
                    "Orrin and the hero kept civilian backup lights in the citadel, without restoring military monopoly.");
                Notify("厄洛已共同签署新的公共余量用途。真实的医疗灯服务已改变。",
                    "Orrin signed the remaining civic power into a real new civilian refuge service.");
                return true;
            }
            if(action.StartsWith("storm_oldstory_"))
            {
                int region;
                if(!int.TryParse(action.Substring("storm_oldstory_".Length),out region) ||
                    region<1||region>3 || save.room!=region ||
                    StormPolicy(region)==0)return true;
                foreach(WorldObject resident in Map.objects)
                    if(resident.kind=="witnessnpc"+region)
                    {TalkWitness(resident);break;}
                return true;
            }
            if(action.StartsWith("storm_relief_"))
            {
                int region;
                if(!int.TryParse(action.Substring("storm_relief_".Length),out region) ||
                    region<1||region>3 || save.room!=region ||
                    StormPolicy(region)==0 || Has("storm_relief_"+region) ||
                    (Position-StormPoint(region,"witnessnpc"+region)).Length>2.2f)
                    return true;
                if(save.inventory==null || save.inventory.Length<=6 || save.inventory[6]<=0)
                {
                    Notify("没有热汤。回余烬镇旅店补充后再来。",
                        "No stew remains. Cook or buy a meal in Emberfall before returning.");
                    return true;
                }
                save.inventory[6]--;
                Flag("storm_relief_"+region);AddXp(12);revision++;sound="reward";
                RecordMoment(StormRegionName(region,false)+"的居民实际收到了星雨中的热汤。这份救助不取代公共配电。",
                    "Neighbors at "+StormRegionName(region,true)+" actually received hot stew during the storm; it never substitutes for a fair grid.");
                Notify("星雨救助 "+StormReliefCount+"/3 完成；真实热汤 -1，经验 +12。",
                    "Storm relief "+StormReliefCount+"/3; -1 real stew, +12 XP.");
                return true;
            }
            if(action=="storm_read")
            {
                if(Has("storm_started"))WorldChroniclePage("storm");
                return true;
            }
            if(action=="storm_track")
            { TrackQuest("storm_commons");return true; }
            if(action=="storm_begin")
            {
                if(!StormReady || Has("storm_started") || save.room!=0 ||
                    (Position-StormPoint(0,"stormcouncil")).Length>2.2f)return true;
                Flag("storm_started");TrackQuest("storm_commons");
                RecordMoment("第六章：战后第一次星雨逼近，莉雅让三地居民真正决定自己需要怎样分配八单位公共储备。",
                    "Chapter Six: the first storm arrives. Lyra trusts three regions to share eight units of civic reserves according to their actual needs.");
                Notify("第六章已开启：三地各自决定并支付真实材料，星雨预警会袭击尚未稳定的地区。",
                    "Chapter Six begun: install three genuine regional allocations; unstable regions have real incoming starstorm arcs.");
                return true;
            }
            if(action.StartsWith("storm_policy_"))
            {
                string[] p=action.Split('_');
                int region;
                if(p.Length!=4 || !int.TryParse(p[2],out region) ||
                    region<1||region>3 || save.room!=region ||
                    !Has("storm_started") || Has("storm_complete") ||
                    !Has("afterlight_done_"+region) ||
                    !Has("afterlight_steward_"+region) ||
                    (Position-StormPoint(region,"stormrelay"+region)).Length>2.2f ||
                    StormPolicy(region)!=0)return true;
                bool shelter=p[3]=="shelter";if(!shelter&&p[3]!="grid")return true;
                int needed=shelter?3:2;
                if(StormBudgetLeft<needed)
                {
                    Notify("公共储备不足。剩余 "+StormBudgetLeft+" 单位；请选实际能够供给的另一方案。",
                        "The reserve cannot support that choice. Remaining: "+StormBudgetLeft+
                        " units. Choose an affordable service.");
                    return true;
                }
                if(!Spend(shelter?1:0,shelter?0:2,shelter?1:0,0,shelter?0:1))
                    return true;
                Flag("storm_region_"+region+"_"+(shelter?"shelter":"grid"));
                hazards.RemoveAll(h=>h.stormOwned);
                effects.RemoveAll(e=>e.stormOwned);
                stormPulse=4f;
                RecordMoment(StormRegionName(region,false)+"采用了"+
                    (shelter?"庇护优先（储备3单位）":"公共供能优先（储备2单位）")+
                    "，既有设施承受了第一场星雨。",
                    StormRegionName(region,true)+" chose "+
                    (shelter?"shelter-first (3 reserve units)":"utilities-first (2 reserve units)")+
                    " and kept its public infrastructure operational in the first storm.");
                Notify("本地星雨电弧已稳定，公共储备余量 "+StormBudgetLeft+"，三地完成 "+StormAllocations+"/3。",
                    "Regional arcs stabilized. Reserve left: "+StormBudgetLeft+
                    "; regions secured: "+StormAllocations+"/3.");
                return true;
            }
            if(action=="storm_tune_start")
            {
                if(!StormReady || !Has("storm_started") || StormAllocations!=3 ||
                    Has("storm_tuned") || stormTuning || Dying || practicing ||
                    save.room!=0 || !SafeTravel ||
                    (Position-StormPoint(0,"stormcouncil")).Length>2.2f)return true;
                stormTuning=true;stormTuneStep=0;stormTuneTime=95;stormPulse=3.5f;
                revision++;sound="quest";TrackQuest("storm_commons");
                Notify("95秒实地调谐已开始。严格按镇内三处星灯的顺序操作，电弧可以闪避。",
                    "95-second real-world tuning begun. Activate three town lamps in order; dodge incoming electrical arcs.");
                return true;
            }
            if(action=="storm_abort")
            {
                if(stormTuning && save.room==0 &&
                    (Position-StormPoint(0,"stormcouncil")).Length<2.2f)
                {
                    ResetStormTransient();revision++;
                    Notify("本次实地调谐已取消。三地的选择和材料成果全部保留。",
                        "This tuning attempt ended. All region allocations and real materials remain intact.");
                }
                return true;
            }
            if(action.StartsWith("storm_node_"))
            {
                int n;
                if(!int.TryParse(action.Substring("storm_node_".Length),out n) ||
                    n<1||n>3 || !stormTuning || save.room!=0 ||
                    (Position-StormPoint(0,"stormnode"+n)).Length>2.2f)return true;
                if(n!=StormNextNode)
                {
                    save.energy=Math.Max(0,save.energy-12);
                    stormTuneTime=Math.Max(0,stormTuneTime-4);
                    revision++;sound="deny";
                    Notify("这不是当前的供能顺序：耐力 -12、调谐时间 -4 秒。请读取真实的公约提示。",
                        "Wrong sequence: -12 energy and -4 seconds. Follow the actual public-grid ordering.");
                    return true;
                }
                stormTuneStep++;
                revision++;sound="quest";
                if(stormTuneStep>=3)
                {
                    ResetStormTransient();
                    Flag("storm_tuned");
                    RecordMoment("余烬镇的三盏公共星灯经主角亲自依序调谐，三地的分配接入了一张能在星雨中运作的公用网络。",
                        "The hero personally tuned three scattered Emberfall lamps in sequence, connecting all three living public allocations into a storm-tested civic grid.");
                    Notify("三座公共星灯已经真实接通。回议事台正式公布公约。",
                        "All three physical town lamps tuned. Return to the civic council to publish the verified charter.");
                }
                else
                    Notify("星灯调谐 "+stormTuneStep+"/3；前往"+StormRegionName(StormNextNode,false)+"的镇内节点。",
                        "Public tuning "+stormTuneStep+"/3; reach the town node for "+StormRegionName(StormNextNode,true)+".");
                return true;
            }
            if(action=="storm_publish")
            {
                if(!StormReady || !Has("storm_started") || !Has("storm_tuned") ||
                    StormAllocations!=3 || Has("storm_complete") || save.room!=0 ||
                    (Position-StormPoint(0,"stormcouncil")).Length>2.2f)return true;
                Flag("storm_complete");
                CreditCoins(110);CreditShards(3);CreditSkillPoints(1);
                AddXp(110);revision++;sound="reward";
                RecordMoment("第六章：八单位公共储备中"+StormBudgetSpent+
                    "单位实际分配给三地，另有"+StormBudgetLeft+
                    "单位留给余烬镇；第一次星雨的真实保障、公开供能和星灯网络经受住了压力。",
                    "Chapter Six: "+StormBudgetSpent+
                    " of eight reserve units were distributed to three regions; "+
                    StormBudgetLeft+" remained for Emberfall. The first storm tested shelters, utilities and the public lamp network.");
                Notify("第六章完成：首次星雨公约已公开。110硬币、3碎片、110经验、1技能点。",
                    "Chapter Six complete. First-storm charter published: 110 coins, 3 shards, 110 XP, 1 skill point.");
                return true;
            }
            return false;
        }
        private void TickStorm(float dt)
        {
            if(!Has("storm_started") || Has("storm_complete"))return;
            if(stormTuning && (save.room!=0 || Dying || practicing))
            {ResetStormTransient();revision++;return;}
            if(stormTuning)
            {
                stormTuneTime-=dt;
                if(stormTuneTime<=0)
                {
                    ResetStormTransient();revision++;sound="deny";
                    Notify("95秒公共星灯调谐超时。地区分配仍然有效；可以回议事台免费重试。",
                        "Civic lamp tuning timed out. All regional allocations remain; restart at the council for free.");
                    return;
                }
            }
            if(save.room<0 || save.room>3 ||
                save.room==0&&!stormTuning ||
                save.room>=1&&StormPolicy(save.room)!=0 ||
                Dying||practicing || !SafeTravel)return;
            stormPulse-=dt;
            if(stormPulse>0)return;
            stormPulse=save.room==0?5.3f:4.7f;
            Point at=Position;
            if(!CanStand(at))return;
            bool canDodge=false;
            foreach(Point shift in new[]{new Point(1.5f,0),new Point(-1.5f,0),
                new Point(0,1.5f),new Point(0,-1.5f)})
                if(CanStand(at+shift)&&ClearAttackPath(at,at+shift)){canDodge=true;break;}
            if(!canDodge)return;
            hazards.Add(new Hazard{position=at,timer=1.4f,
                radius=1.08f,damage=save.room==0?13:16,kind="rift",
                stormOwned=true});
            effects.Add(new Effect("warn",at,1.3f){radius=1.08f,stormOwned=true});
        }
        public bool StormShelterNearby
        {
            get {return save.room>=1&&save.room<=3 &&
                StormPolicy(save.room)==1 &&
                NearKind("stormrelay"+save.room,3.1f);}
        }
        public bool StormGridNearby
        {
            get {return save.room>=1&&save.room<=3 &&
                StormPolicy(save.room)==2 &&
                NearKind("stormrelay"+save.room,3.1f);}
        }
    }
}
