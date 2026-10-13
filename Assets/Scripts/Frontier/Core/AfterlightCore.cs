using System;
using System.Collections.Generic;
namespace Starfall
{
    // Chapter Five: three original regions visibly rebuilt AFTER the original
    // main ending. Permanent state uses only version-2 string flags.
    // Each repair is a physical two-site load transfer, not a free flag.
    public sealed partial class GameCore
    {
        private int afterlightChargeRegion;
        private float afterlightChargeLeft;
        public int AfterlightChargeRegion { get { return afterlightChargeRegion; } }
        public float AfterlightChargeLeft { get { return afterlightChargeLeft; } }
        public int AfterlightRestored
        {
            get { int n=0;for(int i=1;i<=3;i++)if(Has("afterlight_done_"+i))n++;return n; }
        }
        public bool AfterlightComplete { get { return Has("afterlight_complete"); } }
        public int AfterlightStewards
        {
            get { int n=0;for(int i=1;i<=3;i++)if(Has("afterlight_steward_"+i))n++;return n; }
        }
        public int AfterlightMode(int region)
        { return Has("afterlight_plan_"+region+"_care")?1:Has("afterlight_plan_"+region+"_watch")?2:0; }
        private Point AfterlightPoint(int region,string type)
        { return ObjectPoint(region,"afterlight"+type+region,new Point(-100,-100)); }
        private void ResetAfterlightCharge()
        { afterlightChargeRegion=0;afterlightChargeLeft=0; }
        private string AfterlightLocation(int region,bool english)
        {
            string[] zh={"","荆棘密林 · 根苗圃","沉没遗迹 · 开放渡口","赤铁矿场 · 安全工房"};
            string[] en={"","Thornwood · Root Nursery","Drowned Ruins · Open Crossing","Ironreach · Safe Foundry"};
            return english?en[region]:zh[region];
        }
        private bool AfterlightEligible {get{return save.ending>=1 && save.ending<=3;}}
        private string AfterlightOutcomeZh(int region)
        {
            int mode=AfterlightMode(region);
            if(region==1)return mode==1?
                "被砍断的根系终于接回疗养苗圃。这里能在战斗结束后为行路者恢复生命。":
                "旧林道改成夜间防护灯线。庇护灯为经过者补充耐力，但不会强迫根系继续输送给堡垒。";
            if(region==2)return mode==1?
                "渡口恢复向行人开放。靠近旧闸门时，可在无敌人追击的情况下搭乘小舟穿过水道。":
                "遗迹的旧水道被划为公共避险区。渡口仍属于每个人，桥畔的疗养灯会在非战斗时恢复生命。";
            return mode==1?
                "矿工掌握了维修台的零件回收配方。使用真实材料，将一部分多余星晶变成修桥所需的零件。":
                "停机避险条例写入工房控制器。这里的公共冷却灯会在安全时为行路者回复生命。";
        }
        private string AfterlightOutcomeEn(int region)
        {
            int mode=AfterlightMode(region);
            if(region==1)return mode==1?
                "The severed roots are rejoined at a public nursery. It now heals travelers outside combat.":
                "Old forest roads carry protective lamps. The light recharges travelers' energy without drawing from the citadel.";
            if(region==2)return mode==1?
                "The crossing is open to travelers. Outside combat, the public boat carries anyone between both old sluice terminals.":
                "The ruins now hold a publicly accessible refuge. Lamps heal people on the banks while they rest safely.";
            return mode==1?
                "Workers hold the repair recipe. Spend real timber and crystal at this workbench to craft replacement machine parts.":
                "Workers now control the emergency shutdown. The public cooling lamps restore health only when the machinery is safe.";
        }
        private bool TalkAfterlight(WorldObject o)
        {
            string kind=o.kind;
            bool board=kind=="afterlightcouncil";
            int room=save.room;
            bool waypoint=room==0 && kind.StartsWith("afterlightwaypoint");
            if(waypoint)
            {
                int region;
                if(!int.TryParse(kind.Substring("afterlightwaypoint".Length),out region) ||
                    region<1||region>3 || !Has("afterlight_done_"+region) ||
                    !Has("afterlight_steward_"+region))return true;
                Dialog(o.name,o.en,
                    "这是居民亲自签署维护责任后，余烬镇新增的公共星灯线路。\n\n"+
                    "线路已经直连"+AfterlightLocation(region,false)+"的修好设施。附近没有敌人时才能乘坐；它不会替你完成尚未接通的其它地区主线。",
                    "This public starroad opened only after the actual regional steward signed the maintenance record.\n\n"+
                    "It leads directly to the repaired receiver at "+AfterlightLocation(region,true)+". Nearby combat blocks travel; other unfinished story regions remain unfinished.",
                    C("沿公共星灯抵达修好的设施","Travel directly to the repaired site","afterlight_route_"+region),
                    C("先不出发","Stay","close"));return true;
            }
            // Reuse the EXISTING living characters of Acts I–III rather than
            // cloning them as new NPCs or bypassing their signed commitments.
            if(room>=1&&room<=3 && kind=="witnessnpc"+room &&
                AfterlightEligible && Has("afterlight_started") &&
                Has("afterlight_done_"+room) && !Has("afterlight_steward_"+room))
            {
                string[] whoZh={"","莫芽 · 密林巡路员","阿菱 · 遗迹渡口幸存者","维恩 · 赤铁矿工"};
                string[] whoEn={"","Moya · Grove Roadwarden","Arin · Sluice Survivor","Venn · Ironreach Miner"};
                string[] pledgeZh={"",
                    "莫芽：我会让受伤的根系与巡路灯的维修计划都公开。以后每棵树能不能恢复生长，由守护森林的人一起讨论，不再由军用货车决定。",
                    "阿菱：渡口的名单要记下行人、孩子、船夫，还有那些没钱交过桥费的人。这座设施的维护日志我来签。",
                    "维恩：机器维修应当轮班，过热就停，谁也不许把工人的撤离写成偷懒。我要把这条写入每一次交接清单。"};
                string[] pledgeEn={"",
                    "Moya: We will publish repair logs for the wounded roots and road lamps. Those who live with this forest must decide how it heals, not military ore carts.",
                    "Arin: The crossing ledger must name travelers, children, boatkeepers, even those who cannot pay. I will sign its maintenance record.",
                    "Venn: Machines require safe shifts. When they overheat, work stops, and evacuation is not laziness. I will put that into every handover."};
                Dialog(whoZh[room],whoEn[room],
                    pledgeZh[room]+"\n\n真正可用的公共设施需要有人署名维护。若我签了字，公会可以公开我的责任，而不是只在战后庆典里把我算作一个数字。",
                    pledgeEn[room]+"\n\nA working public service needs a named steward. If I sign, the guild can publish my responsibilities rather than counting me as another number at the victory ceremony.",
                    C("请居民亲自签下维护承诺","Have the resident sign stewardship","afterlight_steward_"+room),
                    C("再考虑一下","Not yet","close"));
                return true;
            }
            bool anchor=room>=1&&room<=3&&kind=="afterlightanchor"+room;
            bool outlet=room>=1&&room<=3&&kind=="afterlightoutlet"+room;
            if(!board&&!anchor&&!outlet)return false;
            if(board)
            {
                if(!AfterlightEligible)
                {
                    Dialog(o.name,o.en,
                        "公会正在讨论战后安排，但厄洛与星核的去向尚未决定。地图上的重建计划会在你真正完成主线后开放。",
                        "The guild has plans for reconstruction, but Orrin and the starcores have not yet been resolved. This project opens only after a genuine ending.",
                        C("先完成主线","Finish the main story first","close"));return true;
                }
                if(AfterlightComplete)
                {
                    Dialog(o.name,o.en,
                        "重建议事厅的地图不再只有矿脉与军用货路。莫芽、阿菱、维恩与镇民共同维护的三座设施已经开始运行，公会每周公开下一轮修复名单。\n\n"+ 
                        "你的结局没有让所有损失凭空消失。真正改变世界的是有人继续照看这些路。",
                        "The new map records living residents instead of mining lines alone. Three repaired public sites now run under Moya, Arin, Venn and their neighbors. The guild publishes each weekly repair roster.\n\n"+
                        "Your ending did not erase every hardship. Continuing to care for the roads is what changed this world.",
                        C("回顾旅程","Review the journey","epilogue"),C("继续旅行","Travel on","close"));return true;
                }
                if(!Has("afterlight_started"))
                {
                    string choices=save.ending==1?
                        "你把星核还给了土地，根系开始活过来，但村庄还没有安全的公共线路。":
                        save.ending==2?
                        "你保留了屏障，城里得到保护，城外却必须继续支付能源代价。旧机器需要新的公开约束。":
                        "你选择让居民共同维护星核，然而分享权力意味着要真正修复三条彼此断开的道路。";
                    string choicesEn=save.ending==1?
                        "You returned the cores to the land. The roots are waking, but villages still lack safe public connections.":
                        save.ending==2?
                        "You kept the shield. Citadel children are protected, but outer communities still pay its energy costs. The old machines need public oversight.":
                        "You gave the communities shared stewardship. That promise still demands real work along three broken routes.";
                    Dialog(o.name,o.en,
                        "莉雅：终章并非终点。"+choices+"\n\n"+
                        "森林、遗迹、矿场各有一座旧供能锚点和一座断开的公共接收台。请亲自在各地选择公共用途，启动节点，并在90秒内把仍然稳定的余光带往接收台，使用真实物资接通。每个地区有自己的永久功能；三处完成后回来公布重建名单。",
                        "Lyra: The finale was not the end. "+choicesEn+"\n\n"+
                        "Each of Thornwood, the Ruins and Ironreach has a source anchor and a disconnected public receiver. Choose how the public should use that local power, activate its charge and carry it physically to the receiver in 90 seconds. Commit actual materials there. Each repaired area gains a real permanent service. Return after all three are rebuilt.",
                        C("接下第五章 · 余光重建","Begin Chapter Five · Afterlight Reconstruction","afterlight_begin"),
                        C("先做好准备","Prepare first","close"));return true;
                }
                Dialog(o.name,o.en,
                    "莉雅：已经接通 "+AfterlightRestored+"/3 座公共站。每一座都是由居民签名认可、在地图上真正能使用的设施。"+
                    (AfterlightRestored==3 && AfterlightStewards==3?"\n\n三地设施和三份居民维护签名全部齐备。现在可以正式公布有具体责任人的战后公约。":
                     AfterlightRestored==3?"\n\n三座设施已修复，但仍有"+(3-AfterlightStewards)+"位地区维护者尚未签字。回到原来的三位证人身边，当面确认维护责任。":"\n\n请先到还未修复的地区，再回公会汇报。"),
                    "Lyra: "+AfterlightRestored+"/3 public sites are operational, signed by local residents and usable in the world."+
                    (AfterlightRestored==3 && AfterlightStewards==3?"\n\nAll three services and three signed local maintenance pledges are ready. Publish the charter now.":
                     AfterlightRestored==3?"\n\nAll sites work, but "+(3-AfterlightStewards)+" original regional witnesses still need to sign their maintenance obligations. Return to them in person.":"\n\nFinish the remaining regions before returning for publication."),
                    AfterlightRestored==3 && AfterlightStewards==3?
                        C("公布三地重建公约 · 完成第五章","Publish the three-region charter · Finish Chapter Five","afterlight_publish"):
                        C("追踪战后重建","Track reconstruction","afterlight_track"),
                    C("继续准备","Prepare","close"));
                return true;
            }
            if(!AfterlightEligible)
            {
                Notify("原有星核结局尚未决定，不能提前接通战后供能。",
                    "The starcore ending has not happened yet; reconstruction is not available.");
                return true;
            }
            if(!Has("afterlight_started"))
            {
                Notify("先到余烬镇公会的战后议事台，正式接下第五章。",
                    "First accept Chapter Five at the Emberfall reconstruction council.");
                return true;
            }
            if(Has("afterlight_done_"+room))
            {
                string descriptionZh=AfterlightOutcomeZh(room);
                string descriptionEn=AfterlightOutcomeEn(room);
                var options=new List<Choice>();
                if(room==2 && AfterlightMode(2)==1)
                {
                    if(outlet)
                    {
                        descriptionZh+="\n\n公用渡船在无战斗时可前往原水闸另一岸。";
                        descriptionEn+="\n\nThe public ferry travels to the original sluice terminal only outside combat.";
                        options.Add(C("搭乘公共渡船","Ride public ferry","afterlight_ferry_out"));
                    }
                    else
                        options.Add(C("搭乘公共渡船","Ride public ferry","afterlight_ferry_back"));
                }
                if(outlet && room==3 && AfterlightMode(3)==1)
                {
                    descriptionZh+="\n\n真实成本：木材 ×1，星晶 ×"+(StormPolicy(3)==2?1:2)+"；加工零件 ×2。";
                    descriptionEn+="\n\nReal cost: 1 timber, "+(StormPolicy(3)==2?1:2)+" starcrystal, gaining 2 machine parts.";
                    options.Add(C("制作可修桥的零件 ×2","Craft 2 repair parts","afterlight_craft"));
                }
                if(outlet && Has("afterlight_steward_"+room))
                    options.Add(C("沿公共星灯返回余烬镇","Return to Emberfall via public starroad",
                        "afterlight_return_"+room));
                options.Add(C("继续旅程","Continue","close"));
                Dialog(o.name,o.en,descriptionZh,descriptionEn,options.ToArray());
                return true;
            }
            if(anchor)
            {
                if(AfterlightMode(room)==0)
                {
                    string[] primaryZh={"","为受伤森林恢复活根苗圃","开放所有人的公共渡船","让矿工共享零件回收工房"};
                    string[] primaryEn={"","Restore public healing root nursery","Open the crossing to all travelers","Build a shared recycling workbench"};
                    string[] altZh={"","点亮巡路员使用的夜间林灯","修复应急疗养灯与避险河岸","把矿工的停机条款接入公共冷却灯"};
                    string[] altEn={"","Light the wardens' safe night-road lamps","Restore emergency healing lamps on the river","Wire safety shutdown into the public cooling lamps"};
                    Dialog(o.name,o.en,
                        "旧军令让这座星根锚点只为军用供能。你可以让居民决定它往后服务什么。\n\n"+
                        "两种公共方案都能够运作，但本地区最终建成的服务不同。选择一经确认，会记入战后公约。通电之后才能尝试负载传输。",
                        "The old directive reserved this anchor for military output. Its living neighbors can finally decide what it powers.\n\n"+
                        "Both civic designs function, but offer different permanent local services. Your choice becomes part of the final public charter.",
                        C(primaryZh[room],primaryEn[room],"afterlight_plan_"+room+"_care"),
                        C(altZh[room],altEn[room],"afterlight_plan_"+room+"_watch"),
                        C("再想想","Think about it","close"));return true;
                }
                if(afterlightChargeRegion==room)
                {
                    Dialog(o.name,o.en,
                        "稳定余光还在传输中，剩余 "+Math.Ceiling(afterlightChargeLeft)+" 秒。真正的接收端不在这里；沿地图去另一座公共节点。",
                        "Afterlight charge remains active for "+Math.Ceiling(afterlightChargeLeft)+" seconds. Travel physically to the distant public receiver, not this source.",
                        C("继续送往接收端","Continue to receiver","close"),
                        C("取消本次传输 · 保留方案","Cancel transfer · Keep plan","afterlight_abort"));return true;
                }
                Dialog(o.name,o.en,
                    "原始供能已经校准成居民选择的用途。启动后只有90秒把余光带到另一端；超时只需重试，不收取材料。成功接通时才会消耗资源。",
                    "The original core feed has been calibrated to your civic design. Once started, carry it to the distant receiver in 90 seconds. A failed run costs no materials; actual crafting inputs are spent only on successful installation.",
                    C("激活90秒负载传输","Activate 90-second transfer","afterlight_charge_"+room),
                    C("先准备","Prepare","close"));return true;
            }
            if(afterlightChargeRegion==room)
            {
                Dialog(o.name,o.en,
                    "余光已经抵达公共接收端，时间还剩 "+Math.Ceiling(afterlightChargeLeft)+" 秒。\n\n"+
                    "接通所需真实材料："+(AfterlightMode(room)==1?"木材 ×2、月光草 ×2":"星晶 ×2、零件 ×1")+
                    "。安装完成后将永久改变这片地区的设施功能。",
                    "Afterlight reached its real receiver with "+Math.Ceiling(afterlightChargeLeft)+" seconds remaining.\n\n"+
                    "Material cost: "+(AfterlightMode(room)==1?"2 timber and 2 Moonherb":"2 starcrystal and 1 machine part")+
                    ". The service becomes a permanent local facility once built.",
                    C("安装并接通公共设施","Install and reconnect public service","afterlight_install_"+room),
                    C("保留传输，先备料","Check materials","close"));return true;
            }
            Dialog(o.name,o.en,
                "这里仍是一座未接电的旧公用终端。先到本地区另一端的星根锚点校准用途，启动真实传输后再回这里。",
                "This civic receiver is disconnected. Choose a public design and activate the original starroot anchor across this region first.",
                C("记住位置","Remember location","close"));return true;
        }
        private bool ChooseAfterlight(string action)
        {
            if(action=="afterlight_track"){TrackQuest("afterlight");return true;}
            if(!action.StartsWith("afterlight_"))return false;
            if(action=="afterlight_begin")
            {
                if(save.room!=0 || !AfterlightEligible || Has("afterlight_started") ||
                    (Position-ObjectPoint(0,"afterlightcouncil",new Point(-100,-100))).Length>2.2f)return true;
                Flag("afterlight_started");TrackQuest("afterlight");
                RecordMoment("第五章：结局之后，莉雅和居民开始把三处星根锚点改成真正可运行的公共设施。",
                    "Chapter Five: after your ending, Lyra and the townspeople begin rebuilding three working public starroot sites.");
                Notify("第五章开启：三地锚点必须亲自接线，再回公会公开公约。",
                    "Chapter Five begins. Physically reconnect three regions, then publish the civic charter.");
                return true;
            }
            if(action=="afterlight_publish")
            {
                if(save.room!=0 || !Has("afterlight_started") || AfterlightRestored!=3 || AfterlightStewards!=3 ||
                    Has("afterlight_complete") ||
                    (Position-ObjectPoint(0,"afterlightcouncil",new Point(-100,-100))).Length>2.2f)return true;
                Flag("afterlight_complete");
                save.coins+=120;save.shards+=4;save.skillPoints++;
                AddXp(100);revision++;sound="reward";
                RecordMoment("第五章：三地余光重建公约公开，居民获得可使用的疗养、渡路与安全工房。城墙不再是唯一的地图。",
                    "Chapter Five: the three-region afterlight charter is public. Root care, crossings and safe work now belong to people instead of the wall alone.");
                Notify("第五章完成：120硬币、4碎片、100经验、技能点1。",
                    "Chapter Five complete: 120 coins, 4 shards, 100 XP and one skill point.");
                return true;
            }
            if(action=="afterlight_abort")
            {
                if(afterlightChargeRegion!=0 && save.room==afterlightChargeRegion &&
                    (Position-AfterlightPoint(save.room,"anchor")).Length<2.2f)
                {ResetAfterlightCharge();revision++;Notify("本次传输取消。选择的公共方案仍保留。",
                    "Load transfer canceled. Your chosen civic design remains.");}
                return true;
            }
            if(action=="afterlight_ferry_out" || action=="afterlight_ferry_back")
            {
                if(save.room!=2 || !Has("afterlight_done_2") || AfterlightMode(2)!=1 ||
                    !SafeTravel || Dying || practicing)return true;
                Point from=AfterlightPoint(2,action.EndsWith("out")?"outlet":"anchor");
                Point to=AfterlightPoint(2,action.EndsWith("out")?"anchor":"outlet");
                if((Position-from).Length>2.2f || !CanStand(to))return true;
                save.x=to.x;save.y=to.y;revision++;sound="travel";
                Notify("公共渡船已抵达对岸。不是公会独占的水路了。",
                    "Public ferry reached the other bank. Passage no longer belongs only to the guild.");
                return true;
            }
            if(action.StartsWith("afterlight_route_") ||
                action.StartsWith("afterlight_return_"))
            {
                bool outbound=action.StartsWith("afterlight_route_");
                int region;
                if(!int.TryParse(action.Substring(outbound?
                    "afterlight_route_".Length:"afterlight_return_".Length),out region) ||
                    region<1||region>3 || !Has("afterlight_started") ||
                    !Has("afterlight_done_"+region) || !Has("afterlight_steward_"+region) ||
                    !SafeTravel || Dying || practicing || afterlightChargeRegion!=0)
                    return true;
                Point from=outbound?
                    ObjectPoint(0,"afterlightwaypoint"+region,new Point(-100,-100)):
                    AfterlightPoint(region,"outlet");
                Point destination=outbound?AfterlightPoint(region,"outlet"):
                    ObjectPoint(0,"afterlightwaypoint"+region,new Point(-100,-100));
                if(save.room!=(outbound?0:region) ||
                    (Position-from).Length>2.2f)
                    return true;
                // Collision MUST be checked on the destination map after Enter:
                // CanStand uses the current Map, not the target region's map.
                Enter(outbound?region:0);
                if(save.room==(outbound?region:0) && CanStand(destination))
                {
                    save.x=destination.x;save.y=destination.y;revision++;sound="travel";
                    Notify("公共星灯线路已接通。居民和冒险者都能使用这条新道路。",
                        "Public starroad now links town and civic services for every traveler.");
                }
                return true;
            }
            if(action.StartsWith("afterlight_steward_"))
            {
                int r;
                if(!int.TryParse(action.Substring("afterlight_steward_".Length),out r) ||
                    r<1||r>3 || save.room!=r || !Has("afterlight_started") ||
                    !Has("afterlight_done_"+r) || Has("afterlight_steward_"+r) ||
                    (Position-ObjectPoint(r,"witnessnpc"+r,new Point(-100,-100))).Length>2.2f)
                    return true;
                Flag("afterlight_steward_"+r);revision++;sound="quest";
                RecordMoment(AfterlightLocation(r,false)+"的当地居民已签下公共设施的维护责任。",
                    "Local residents at "+AfterlightLocation(r,true)+" signed responsibility for their working public infrastructure.");
                Notify("居民维护承诺已签署："+AfterlightStewards+"/3。公约必须由活着的人亲自承担。",
                    "Resident stewardship signed: "+AfterlightStewards+"/3. A charter needs real people taking responsibility.");
                return true;
            }
            if(action=="afterlight_craft")
            {
                if(save.room!=3 || !Has("afterlight_done_3") || AfterlightMode(3)!=1 ||
                    Dying || practicing ||
                    (Position-AfterlightPoint(3,"outlet")).Length>2.2f)return true;
                if(Spend(1,StormPolicy(3)==2?1:2,0,0,0))
                {save.inventory[4]+=2+(HearingComplete && HearingVoice(3)==1?1:0);revision++;sound="reward";
                    Notify("工房回收成功：木材 -1、星晶 -"+(StormPolicy(3)==2?1:2)+"，零件 +"+(HearingComplete && HearingVoice(3)==1?"3":"2")+"。",
                        "Public recycling: -1 timber, -"+(StormPolicy(3)==2?1:2)+" starcrystal, +"+(HearingComplete && HearingVoice(3)==1?"3":"2")+" actual machine parts.");}
                return true;
            }
            string[] pieces=action.Split('_');
            int room;
            if(pieces.Length<3 || !int.TryParse(pieces[2],out room) || room<1 || room>3 ||
                save.room!=room || !AfterlightEligible || !Has("afterlight_started") ||
                Has("afterlight_done_"+room) || Dying || practicing)return true;
            if(pieces[1]=="plan")
            {
                if(pieces.Length!=4 || AfterlightMode(room)!=0 ||
                    (Position-AfterlightPoint(room,"anchor")).Length>2.2f)return true;
                if(pieces[3]!="care"&&pieces[3]!="watch")return true;
                Flag(action);TrackQuest("afterlight");
                Notify("居民用途方案已署名；下一步启动90秒余光输送。",
                    "Civic plan signed. Next: start the 90-second afterlight transfer.");
                return true;
            }
            if(pieces[1]=="charge")
            {
                if(AfterlightMode(room)==0 || afterlightChargeRegion!=0 || !SafeTravel ||
                    (Position-AfterlightPoint(room,"anchor")).Length>2.2f ||
                    blackoutActive || TriadDefenseActive || witnessDefenseRoom!=0 ||
                    TemperActive || TideSalvageActive || commonsRelayRoom!=0 ||
                    reliefMissionRoom!=0 || GlasswindSite!=0 || racing)return true;
                afterlightChargeRegion=room;afterlightChargeLeft=90;
                revision++;sound="quest";
                Notify("90秒传输开始！赶往地图另一端的公共接收台。战斗不会自动替你完成重建。",
                    "90-second load transfer started! Reach the real receiver across the map. Combat cannot substitute for reconstruction.");
                return true;
            }
            if(pieces[1]=="install")
            {
                if(afterlightChargeRegion!=room || afterlightChargeLeft<=0 ||
                    (Position-AfterlightPoint(room,"outlet")).Length>2.2f ||
                    !SafeTravel)return true;
                bool care=AfterlightMode(room)==1;
                if(!Spend(care?2:0,care?0:2,care?2:0,0,care?0:1))return true;
                ResetAfterlightCharge();
                Flag("afterlight_done_"+room);
                save.coins+=20+room*10;AddXp(20+room*10);revision++;
                RecordMoment(AfterlightLocation(room,false)+"的公共供能已接通："+AfterlightOutcomeZh(room),
                    "Public power restored at "+AfterlightLocation(room,true)+": "+AfterlightOutcomeEn(room));
                Notify("真实接收端已通电！本地永久设施开放。返回公会或继续下一地区。",
                    "Physical receiver connected! Permanent local service unlocked. Visit the guild or proceed to the next region.");
                return true;
            }
            return true;
        }
        private void TickAfterlight(float dt)
        {
            if(afterlightChargeRegion==0)return;
            if(save.room!=afterlightChargeRegion || Dying || practicing)
            {ResetAfterlightCharge();revision++;return;}
            afterlightChargeLeft-=dt;
            if(afterlightChargeLeft<=0)
            {
                ResetAfterlightCharge();revision++;
                Notify("余光在抵达前散失。上次签署的方案与材料保留，可回锚点免费重启。",
                    "Afterlight dissipated before arrival. Your plan and materials remain; restart from the anchor for free.");
            }
        }
        public bool AfterlightRefuge(int region)
        {return region>=1&&region<=3 && Has("afterlight_done_"+region) &&
            (region==1&&AfterlightMode(1)==1 ||
             region==2&&AfterlightMode(2)==2 ||
             region==3&&AfterlightMode(3)==2);}
        public bool AfterlightEnergyPost
        { get { return save.room==1 && Has("afterlight_done_1") && AfterlightMode(1)==2; } }
    }
}
