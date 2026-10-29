using System;
namespace Starfall
{
    // Physical field landmarks, additive to the existing campaign and SaveData v2.
    public sealed partial class GameCore
    {
        // Two real landmarks per overworld biome form a short field
        // investigation with region-specific reasoning rather than another
        // generic chest, fake combat actor or unearned map-menu reward.
        // All eight outcomes use normal v2 flags. The six-stance choice
        // costs actual stamina on a mistaken field interpretation.
        private static readonly string[] biomeOpeningZh={
            "街角的老邻居没数车轮，只数每天夜里还能亮灯的窗户。货车曾满载，却有几户人家始终没拿到食物。",
            "枝条朝山谷外侧折断，树皮上留下避开猎人营火的划痕。那些人不是沿最近的路逃走。",
            "石阶上有三层古老水线；中央深渠冲走了木桥，两侧低台却露出磨亮的踏痕。",
            "烧黑的工牌写着：炉压失控时，矿工先让风从山体背面流出，才敢重新点火。",
            "石墙刻下的最后一笔不是攻击阵列，而是一扇有人扶着伤者穿过的旧侧门。",
            "雾中游动的渔火无法作为航标。岸边两盏固定灯曾在暴风雨后救回一船孩子。",
            "沙海里远处的高塔一到正午就消失。旧测影石留下的是阴影的方向，而不是光塔的位置。",
            "霜风越猛，钟声越容易被吹离悬崖外的山道。守钟人把听风孔凿在背风侧。"
        };
        private static readonly string[] biomeOpeningEn={
            "Neighbors counted lit windows, not wagons. A convoy arrived full, yet several homes never received supper.",
            "Branches snapped toward the far valley. Scratches mark a route away from hunters' fires, not the shortest escape.",
            "Three old waterlines cross these steps. The central channel swallowed its bridge, but polished footmarks survive on the lower side ledges.",
            "A scorched workboard says the miners opened the mountain's rear vents before reigniting an unstable furnace.",
            "The citadel's final carving depicts a side door held open for the injured, not a line of attackers.",
            "Drifting fishing lights are useless in fog. Two stationary shore lamps once guided a boat of children home.",
            "A distant tower vanishes at noon. The old gauge records the shadow's direction, not the false tower's location.",
            "Wind carried the bell's sound off the cliff path. Wardens bored a listening hole on the leeward side."
        };
        private static readonly string[] biomeFinalZh={
            "账册旁压着八户收不到口粮的名单。远街缺的不是车，是亲手把食物送到门口的人。",
            "两道足印在此交叉。一道靠近猎人陷阱，另一道沿断枝绕过巢穴。",
            "残桥底下是急流，侧面的旧石阶仍在涨潮时露出最高一层。",
            "旧阀门留有两种手印：急着点火的人烫伤了手，打开排气口的人救下同伴。",
            "医护名册被藏在防御图背面。城墙最后一道命令是护送平民。",
            "远端灯塔的海图标出两盏不会随船漂移的岸灯，风暴夜的航线由此确定。",
            "这里能看到同一座山的真实背光面。那座正午的光塔只是热浪折出的影。",
            "三枚钟舌都有划痕，唯独背风面的低钟留下行人用木棒敲击的印记。"
        };
        private static readonly string[] biomeFinalEn={
            "Eight families went hungry despite full wagons. The missing piece was a person to carry food to their doors.",
            "Two tracks cross here: one leads into hunters' traps, the other follows broken branches clear of the nests.",
            "The lost bridge hangs above a current. The old side stair still reaches above the tide at its highest step.",
            "Two handprints mark this valve. One worker burned a hand relighting the forge; the one who opened the vent saved the others.",
            "A patient list is hidden behind the tactical chart. The garrison's final order was to escort civilians.",
            "A far harbor chart marks two unmoving coastal lamps. They, not wandering boats, fixed the storm route.",
            "The true mountain's shadow is visible from here. The tower at noon was nothing but heat shimmer.",
            "Of three scarred bells, the low leeward bell bears the mark of travelers tapping it with a walking staff."
        };
        private static readonly string[,] biomeAnswersZh={
            {"按抵港车数分配物资","沿街逐户核对并补送口粮","把多余食物锁回仓库"},
            {"绕开猎人营火，沿折枝安全路走","直穿有陷阱的近道","只追最深的脚印"},
            {"涉水穿过深渠","等海水冲净所有石阶","利用侧面的高阶而非断桥"},
            {"先打开排气口再恢复炉火","先加煤压过异响","封死通风口保温"},
            {"封闭侧门防止平民入城","把药品调去前线阵地","打开侧门，沿旧线转移伤员"},
            {"跟着海上移动的渔火","用岸边两盏不动的灯定位","熄灭所有岸灯"},
            {"对准热浪里最亮的高塔","对齐真实山影，避开海市蜃楼","转动镜面对准太阳本身"},
            {"在背风低钟回应行人","敲迎风高钟让声音散入雪原","不敲钟，原地等到天黑"}
        };
        private static readonly string[,] biomeAnswersEn={
            {"Count incoming wagons","Visit every doorstep and deliver the missing food","Lock surplus rations back in storage"},
            {"Follow snapped branches away from hunters' fires","Take the trap-lined shortcut","Follow only the deepest footprints"},
            {"Wade into the deep channel","Wait until the steps wash away","Use the high side stairs, not the broken bridge"},
            {"Vent the furnace before relighting","Add coal to drown out the warning","Seal all vents to retain heat"},
            {"Lock the side door against civilians","Move medicine to the front line","Open the side door and evacuate the injured"},
            {"Follow drifting fishing lights","Navigate by two fixed shore lamps","Extinguish the shore beacons"},
            {"Aim toward the brightest shimmering tower","Align with the real mountain's shadow","Point the mirror directly at the sun"},
            {"Answer at the low leeward bell","Ring the high windward bell into the blizzard","Remain still until night"}
        };
        private static readonly int[] biomeCorrect={1,0,2,0,2,1,1,0};
        public int BiomeFieldNotes
        {
            get{int total=0;for(int i=0;i<8;i++)
                if(Has("biome_field_done_"+i))total++;
                return total;}
        }
        public bool BiomeFieldDone
        {get{return save.room>=0&&save.room<8&&
            Has("biome_field_done_"+save.room);}}
        public bool BiomeFieldStarted
        {get{return save.room>=0&&save.room<8&&
            Has("biome_field_read_"+save.room);}}
        public Point BiomeFieldNextTarget
        {
            get
            {
                if(save.room<0||save.room>=8)return new Point();
                string id="biome"+(BiomeFieldStarted?"Answer":"Survey")+save.room;
                foreach(WorldObject o in Map.objects)
                    if(o.id==id)return new Point(o.x,o.y);
                return new Point();
            }
        }
        private bool TalkBiomeField(WorldObject o)
        {
            if(o==null||(o.kind!="biomeSurvey"&&o.kind!="biomeAnswer"))
                return false;
            if(save.room<0||save.room>=8||!SourceBelongsToActiveMap(o)||
               !HasPhysicalInteractionReach(o)||!CanStand(Position))return true;
            int region=save.room;
            if(o.id!="biome"+(o.kind=="biomeSurvey"?"Survey":"Answer")+region)
                return true;
            if(BiomeFieldDone)
            {
                Dialog(o.name,o.en,
                    "你已经把这片土地的两份证据写进了公共探路册。路线由后来人自己选择，不再只有一条生硬的地图直线。",
                    "Both field testimonies are now in the shared wayfinder journal. Future travelers may choose their own safe route. The two inspected markers can now ferry you across this region for 3 coins and 12 stamina, but never through a closed road or live danger.",
                    C("使用地区接驳 · 硬币3 / 耐力12",
                      "Ride field relay · 3 coins / 12 stamina","biome_relay"),
                    C("收起记录","Close the journal","close"));
                return true;
            }
            if(o.kind=="biomeSurvey")
            {
                Dialog(o.name,o.en,
                    biomeOpeningZh[region]+"\n\n亲眼看过这处标记后，才能到远端比对真正的答案。",
                    biomeOpeningEn[region]+"\n\nRead this marker in person, then travel across the region to test its meaning.",
                    BiomeFieldStarted?C("已经记下","Already recorded","close"):
                        C("抄下当地线索","Copy local field evidence","biome_note"),
                    C("以后再来","Not yet","close"));
                return true;
            }
            if(!BiomeFieldStarted)
            {
                Dialog(o.name,o.en,
                    "远处的答案不能替代亲眼看过的第一处现场。先找到本地区的旧探路标记。",
                    "The final answer is no substitute for examining the first landmark in person. Find the original field post.",
                    C("返回寻找","Search the first site","close"));
                return true;
            }
            Dialog(o.name,o.en,
                biomeFinalZh[region]+"\n\n把前后两处证据放在一起，下一位旅人该怎样行动？答错会消耗6点耐力重新核对现场。",
                biomeFinalEn[region]+"\n\nCompare both original sites. What should the next traveler do? A wrong interpretation costs 6 stamina to reassess.",
                C(biomeAnswersZh[region,0],biomeAnswersEn[region,0],"biome_pick_0"),
                C(biomeAnswersZh[region,1],biomeAnswersEn[region,1],"biome_pick_1"),
                C(biomeAnswersZh[region,2],biomeAnswersEn[region,2],"biome_pick_2"),
                C("暂时不判断","Consider later","close"));
            return true;
        }
        private bool ChooseBiomeField(string action)
        {
            if(action!="biome_relay"&&action!="biome_note"&&
               !action.StartsWith("biome_pick_",StringComparison.Ordinal))
                return false;
            WorldObject site=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||save.room<0||
               save.room>=8||site==null||
               !SourceBelongsToActiveMap(site)||
               !HasPhysicalInteractionReach(site)||!CanStand(Position))
                return true;
            int room=save.room;
            if(action=="biome_relay")
            {
                // A completed investigation certifies BOTH actual endpoints.
                // Never allow a forged or stale dialogue to teleport from a
                // remote NPC, a different region, or before walking the route.
                if(!BiomeFieldDone||pendingDialogueSourceRoom!=room||
                   (site.kind!="biomeSurvey"&&site.kind!="biomeAnswer")||
                   site.id!="biome"+
                       (site.kind=="biomeSurvey"?"Survey":"Answer")+room)
                    return true;
                WorldObject destination=null;
                string destinationId="biome"+
                    (site.kind=="biomeSurvey"?"Answer":"Survey")+room;
                foreach(WorldObject other in Map.objects)
                    if(other!=null&&other.id==destinationId)
                    {destination=other;break;}
                if(destination==null||!SourceBelongsToActiveMap(destination)||
                   FieldWorkPreventsRest||!SafeTravel||racing||
                   BlockTravelForProtectedFieldMission(true))
                {
                    Notify("接驳暂停：当前现场行动、战斗或危险尚未结束。",
                        "Field relay unavailable during a protected mission, combat or live danger.");
                    return true;
                }
                if(save.coins<3||save.energy<12f)
                {
                    Notify("地区接驳需要3枚硬币与12点耐力。",
                        "Field relay requires 3 coins and 12 stamina.");
                    return true;
                }
                // Use an actual nearby standing pad; never land inside a
                // water tile, cliff, locked seal, house or live enemy.
                Point marker=new Point(destination.x,destination.y);
                Point[] pads={
                    new Point(0f,1f),new Point(1f,0f),
                    new Point(-1f,0f),new Point(0f,-1f),
                    new Point(1f,1f),new Point(-1f,-1f),
                    new Point(1f,-1f),new Point(-1f,1f)
                };
                Point? arrival=null;
                Point departure=Position;
                foreach(Point pad in pads)
                {
                    Point candidate=marker+pad;
                    if(!CanStand(candidate)||
                       !CivicShuttleRoadSafe(departure,candidate))continue;
                    save.x=candidate.x;save.y=candidate.y;
                    bool safe=SafeTravel&&HasPhysicalInteractionReach(destination);
                    save.x=departure.x;save.y=departure.y;
                    if(safe){arrival=candidate;break;}
                }
                if(!arrival.HasValue)
                {
                    Notify("当前路线受潮汐、封印或敌人阻断。接驳未出发，不扣费。",
                        "The walking route is cut off by a gate, tide or enemies. No fare charged.");
                    return true;
                }
                save.x=arrival.Value.x;save.y=arrival.Value.y;
                if(save.companion>=0&&!CompanionHoldingPosition&&
                   (Position-departure).Length>3f)
                {
                    Point follow=CompanionFollowTarget();
                    if(CanStand(follow))
                    {
                        ClearCompanionFollowRoute();
                        ClearCompanionCornerPlan();
                        companionAutoTarget=null;
                        companionPosition=follow;
                        companionMoveTime=0f;companionMoving=false;
                        companionPresentationGeneration++;
                    }
                }
                save.coins-=3;save.energy-=12f;
                revision++;sound="travel";
                Notify("沿已核实的地区接驳线抵达另一端：硬币-3，耐力-12。",
                    "Arrived at the other verified field marker. -3 coins, -12 stamina.");
                return true;
            }
            if(action=="biome_note")
            {
                if(site.kind!="biomeSurvey"||site.id!="biomeSurvey"+room||
                   BiomeFieldStarted||BiomeFieldDone)return true;
                Flag("biome_field_read_"+room);
                RecordMoment(biomeOpeningZh[room],biomeOpeningEn[room]);
                // Only visiting a new real field site can awaken its
                // additional local encounter; passive map browsing or
                // unrelated story progression never spawns extra monsters.
                SpawnRegionalScoutEncounter(site);
                revision++;sound="quest";
                Notify("第一份实地线索已记录。去本地区远端寻找对应的证据。",
                    "First field clue recorded. Explore the distant matching site.");
                return true;
            }
            if(site.kind!="biomeAnswer"||site.id!="biomeAnswer"+room||
               !BiomeFieldStarted||BiomeFieldDone)return true;
            int selected;
            if(!int.TryParse(action.Substring("biome_pick_".Length),out selected)||
               selected<0||selected>2)return true;
            if(selected!=biomeCorrect[room])
            {
                if(save.energy<6f)
                {
                    Notify("现在没有足够的耐力重新核对。休息后再判断。",
                        "Not enough stamina to reassess. Rest before trying again.");
                    return true;
                }
                save.energy-=6f;revision++;sound="alert";
                Notify("两处实地记录并不支持这个做法。耗费6耐力重新读一遍。",
                    "That answer contradicts the two physical records. Spend 6 stamina and reconsider.");
                return true;
            }
            Flag("biome_field_done_"+room);
            CreditCoins(24);CreditShards(1);AddXp(40);
            RecordMoment(biomeFinalZh[room]+" "+biomeAnswersZh[room,selected]+"。",
                biomeFinalEn[room]+" "+biomeAnswersEn[room,selected]+".");
            revision++;sound="reward";
            Notify("本地区实地调查完成：硬币24、碎片1、经验40。全部八地区可汇成公共探路册。",
                "Regional fieldwork complete: 24 coins, 1 shard, 40 XP. All eight regions build the public wayfinder atlas.");
            if(BiomeFieldNotes==8&&!Has("biome_field_atlas"))
            {
                Flag("biome_field_atlas");CreditSkillPoints(1);CreditShards(3);
                RecordMoment(
                    "八条不同的道路终于汇成一册共同的地图。每一条线都有人走过、证实过，而不是只用同样的方格复制。",
                    "Eight distinct, walked and checked routes form a shared public atlas—not eight copies of the same empty grid.");
                Notify("八地区公共探路册完成！额外技能点1、碎片3。",
                    "All-region atlas complete! Bonus 1 talent point and 3 shards.");
            }
            return true;
        }
        // A prepared signal fire creates a real, localized protective perimeter
        // around an EXISTING, visited outdoor camp. It cannot be carried
        // across regions or save slots and is not a free global shield.
        private float campWardSeconds, campWardFlareClock;
        private int campWardCharges, campWardRoom=-1;
        private MapData campWardMap;
        private SaveData campWardSave;
        private WorldObject campWardCamp;
        public bool CampWardActive
        { get { return campWardSeconds>0&&campWardCharges>0&&!Dying&&
            save.room==campWardRoom&&
            Object.ReferenceEquals(Map,campWardMap)&&
            Object.ReferenceEquals(save,campWardSave)&&
            campWardCamp!=null&&
            SourceBelongsToActiveMap(campWardCamp); } }
        public float CampWardSeconds { get { return CampWardActive?campWardSeconds:0f; } }
        public int CampWardCharges { get { return CampWardActive?campWardCharges:0; } }
        // Each of the eight physical outdoor camps must be reached and its
        // real fire funded before the Hearth Inn can publish the civilian
        // network. String flags preserve all v1/v2 save payloads.
        public int CampSignalCount
        {
            get { int n=0;for(int room=0;room<8;room++)
                if(Has("annex_signal_fire_"+room))n++;
                return n; }
        }
        public bool CampSignalNetworkPublished
        { get { return Has("annex_signal_network_published"); } }
        public Point CampWardFoot
        { get { return CampWardActive?
            new Point(campWardCamp.x,campWardCamp.y):new Point(); } }
        // The world overlay uses the EXACT same terrain-visible footprint
        // as the real shot interception. Never show a glowing safety ring
        // through an intact cliff when that arc cannot stop projectiles.
        public bool CampWardVisualSample(Point sample)
        {
            return CampWardActive&&Finite(sample.x)&&Finite(sample.y)&&
                (sample-CampWardFoot).Length<=3.251f&&
                ClearAttackPath(CampWardFoot,sample);
        }
        private void ResetCampWard()
        {
            campWardSeconds=campWardFlareClock=0;
            campWardCharges=0;campWardRoom=-1;
            campWardMap=null;campWardSave=null;campWardCamp=null;
        }
        private void TickCampWard(float dt)
        {
            if(campWardSeconds<=0)return;
            if(!CampWardActive){ResetCampWard();return;}
            campWardSeconds=Math.Max(0f,campWardSeconds-dt);
            if(campWardSeconds<=0){ResetCampWard();return;}
            campWardFlareClock-=dt;
            if(campWardFlareClock<=0)
            {
                campWardFlareClock=1.25f;
                effects.Add(new Effect("release",CampWardFoot,.40f)
                    {variant=1,height=12f});
            }
        }
        // Invoked at the actual unblocked .1-tile projectile sweep sample,
        // BEFORE either hero or civilian body contact. Nothing outside the
        // ward or behind solid cover may be erased.
        private bool TryCampWardIntercept(Projectile shot)
        {
            if(!CampWardActive||shot==null||shot.friendly||
                shot.blockedAtSpawn||shot.visual=="pebble"||
                !Finite(shot.velocity.x)||!Finite(shot.velocity.y)||
                shot.velocity.Length<=.5f||
                !Finite(shot.damage)||shot.damage<=0f||
                !Finite(shot.position.x)||!Finite(shot.position.y)||
                (shot.position-CampWardFoot).Length>3.25f||
                !ClearAttackPath(CampWardFoot,shot.position))
                return false;
            Point hit=shot.position;
            campWardCharges--;
            effects.Add(new Effect("projectileImpact",hit,.30f)
                {variant=4,direction=shot.velocity.Normalized,height=16f});
            effects.Add(new Effect("number",CampWardFoot+
                new Point(0f,-.8f),.8f,
                L("守火拦截 · 剩 "+campWardCharges,
                  "FIREWARD · "+campWardCharges+" LEFT")));
            sound="counter";revision++;
            if(campWardCharges==0)ResetCampWard();
            return true;
        }
        private bool ChooseCampWard(string action)
        {
            if(action!="annex_ward_light"&&action!="annex_ward_douse")
                return false;
            WorldObject camp=pendingDialogueSource;
            if(save.room<0||save.room>7||mode!="world"||
                Dying||practicing||camp==null||camp.kind!="annexcamp"||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(camp)||
                !HasPhysicalInteractionReach(camp))return true;
            if(action=="annex_ward_douse")
            {
                if(CampWardActive&&Object.ReferenceEquals(campWardCamp,camp))
                {
                    ResetCampWard();revision++;sound="ui";
                    Notify("守火已熄灭。剩余材料不返还。",
                        "Signal fire extinguished. Spent supplies are not refunded.");
                }
                return true;
            }
            if(CampWardActive)
            {
                Notify("当前区域的守火仍在燃烧，不能叠加次数。",
                    "Your current signal fire is still burning; charges cannot stack.");
                return true;
            }
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("正在进行现场任务或仍有危险，无法布置守火。",
                    "Finish fieldwork and clear nearby danger before preparing the fire.");
                return true;
            }
            if(!Spend(2,0,1,0,0))return true;
            campWardCamp=camp;campWardRoom=save.room;
            campWardMap=Map;campWardSave=save;
            campWardSeconds=Has("afterglow_ending_watch")?150f:
                CampSignalNetworkPublished?120f:90f;
            campWardCharges=Has("afterglow_all")?5:
                CampSignalNetworkPublished?4:3;
            campWardFlareClock=0f;
            Flag("annex_signal_fire_"+save.room);
            effects.Add(new Effect("burst",CampWardFoot,.45f)
                {radius=1f,variant=1});
            revision++;sound="reward";
            Notify("守火已点燃："+(int)campWardSeconds+"秒内拦截营地周围"+
                campWardCharges+"枚敌方飞弹。信号站已记录 "+CampSignalCount+"/8。",
                "Signal fire lit: "+(int)campWardSeconds+"s, "+
                campWardCharges+" hostile shots blocked. Network sites "+
                CampSignalCount+"/8.");
            return true;
        }

        // The public network creates eight genuine two-way field dispatches.
        // A flame packet must be carried from its real camp to the region's
        // actual lookout AND back, before world time or three real hits end
        // the run. A new SaveData field would invalidate existing v1/v2 saves,
        // so only *completed* dispatches persist as existing string flags.
        private int signalRelayRoom=-1, signalRelayStage, signalRelayIntegrity;
        private float signalRelaySeconds;
        private MapData signalRelayMap;
        private SaveData signalRelaySave;
        private WorldObject signalRelayCamp,signalRelayMarker;
        private static readonly string[] relayMessageZh={
            "外渠水闸的操作员终于接到了被遗漏的低洼住户名单。警戒钟先通知人，再通知货。",
            "荆棘林的回讯来自树林深处的孩子。他们把回家的路线画在树皮上，守路人把它换成所有人都看得懂的路标。",
            "沉没遗迹的渡工确认了新的涨潮时刻。没有行会通行证的渡船，也能看到岸上的安全灯。",
            "铁脊矿工从高台传回三次急停铃的顺序，废弃铁轨从此优先为撤离者留路。",
            "堡垒旧岗的收信人不是军官，而是替夜归者留灯的居民。最后一道门终于向普通旅人打开。",
            "雾港的夜航讯号带回一艘失联救援艇的航向。岸上的火不再只照顾缴了泊位费的船。",
            "玻璃风沙脊传来的井位，标上了真实的水量；后来者不必再为虚假的商队水图绕远。",
            "霜钟崖的回声确认暖棚仍有人守夜。冻土上的火光是一封不会被封锁的求援信。"
        };
        private static readonly string[] relayMessageEn={
            "The canal-keeper receives the overlooked lowland household list. The flood bell will warn people before cargo.",
            "A message returns from children deep in Thornwood. Their bark-drawn path becomes a public sign anyone can follow home.",
            "The ruins' ferrymen confirm the next high tide. Unregistered boats can now see the same safety lights as guild craft.",
            "Ironreach workers send back the three-bell shutdown order. Empty rails will serve evacuating families first.",
            "The citadel lookout's recipient is a resident who keeps lamps for late travelers, not an officer. The final gate opens to the public.",
            "Mistport's night signal brings the heading of a missing rescue skiff. Shore fires no longer serve only fee-paying vessels.",
            "Glasswind's reply marks the real water level of an unregistered well. No traveler must trust a false merchant chart.",
            "Frostchime's echo confirms the warming shelter still has a night watch. A fire on the frozen ridge is a rescue letter no checkpoint can stop."
        };
        public bool SignalRelayActive
        { get { return signalRelayStage!=0&&signalRelaySeconds>0f&&
            signalRelayIntegrity>0&&signalRelayRoom==save.room&&
            Object.ReferenceEquals(signalRelayMap,Map)&&
            Object.ReferenceEquals(signalRelaySave,save)&&
            signalRelayCamp!=null&&signalRelayMarker!=null&&
            SourceBelongsToActiveMap(signalRelayCamp)&&
            SourceBelongsToActiveMap(signalRelayMarker); } }
        public int SignalRelayStage {get{return SignalRelayActive?signalRelayStage:0;}}
        public int SignalRelayIntegrity {get{return SignalRelayActive?signalRelayIntegrity:0;}}
        public float SignalRelaySeconds {get{return SignalRelayActive?signalRelaySeconds:0f;}}
        public Point SignalRelayTarget
        {get{WorldObject o=SignalRelayActive?
            (signalRelayStage==1?signalRelayMarker:signalRelayCamp):null;
            return o==null?new Point():new Point(o.x,o.y);}}
        public int SignalRelayDoneCount
        {get{int count=0;for(int room=0;room<8;room++)
            if(Has("annex_relay_done_"+room))count++;
            return count;}}
        private bool IsSignalRelayDestination(WorldObject marker)
        {return SignalRelayStage==1&&marker!=null&&
            Object.ReferenceEquals(marker,signalRelayMarker);}
        private void ResetSignalRelay()
        {
            signalRelayRoom=-1;signalRelayStage=signalRelayIntegrity=0;
            signalRelaySeconds=0f;signalRelayMap=null;signalRelaySave=null;
            signalRelayCamp=signalRelayMarker=null;
        }
        private void TickSignalRelay(float dt)
        {
            if(signalRelayStage==0)return;
            if(!SignalRelayActive){ResetSignalRelay();return;}
            signalRelaySeconds=Math.Max(0f,signalRelaySeconds-dt);
            if(signalRelaySeconds<=0f)
            {
                ResetSignalRelay();revision++;
                Notify("信号火种熄灭，传讯超时。回到营地可以重新出发。",
                    "The signal flame went out. Return to camp to retry the relay.");
            }
        }
        private void DamageSignalRelayCargo(float actualDamage,string source)
        {
            // Only the authoritative post-armor HP hit may harm a packet.
            // Dodged, parried, blocked and zero-damage contacts cannot do so.
            if(!SignalRelayActive||!Finite(actualDamage)||
                actualDamage<=0f)return;
            signalRelayIntegrity--;
            if(signalRelayIntegrity<=0)
            {
                ResetSignalRelay();revision++;
                Notify("信号火种在实战中损毁。请回营地重新接取任务。",
                    "The dispatch flame was destroyed by real damage. Restart at camp.");
            }
            else
            {
                effects.Add(new Effect("number",Position,.85f,
                    L("传讯火种受损 · 剩 "+signalRelayIntegrity,
                      "FLAME HIT · "+signalRelayIntegrity+" LEFT")));
                revision++;
            }
        }
        private bool ChooseSignalRelay(string action)
        {
            if(action!="annex_relay_begin"&&action!="annex_relay_deliver"&&
                action!="annex_relay_report"&&action!="annex_relay_abort")
                return false;
            WorldObject source=pendingDialogueSource;
            if(source==null||mode!="world"||Dying||practicing||
                save.room<0||save.room>7||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            int region=save.room;
            if(action=="annex_relay_begin")
            {
                if(source.kind!="annexcamp"||SignalRelayActive||
                    !CampSignalNetworkPublished||
                    !Has("annex_signal_fire_"+region)||
                    Has("annex_relay_done_"+region))return true;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("现场行动或附近攻击尚未解除，不能出发送讯。",
                        "Finish fieldwork and clear nearby attacks before dispatch.");
                    return true;
                }
                WorldObject marker=null;
                string targetId="ridge_route_"+region+"_"+(region%2);
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="ridgemarker"&&o.id==targetId)
                    {marker=o;break;}
                if(marker==null||!CanStand(new Point(marker.x,marker.y))||
                    (new Point(marker.x,marker.y)-
                     new Point(source.x,source.y)).Length<24f)
                    return true;
                signalRelayRoom=region;signalRelayMap=Map;signalRelaySave=save;
                signalRelayCamp=source;signalRelayMarker=marker;
                signalRelayStage=1;signalRelayIntegrity=3;
                signalRelaySeconds=300f;
                effects.Add(new Effect("release",new Point(source.x,source.y),
                    .55f){variant=1,height=16f});
                revision++;sound="quest";
                Notify("已接取烽火传讯：5分钟内走到远处真实高地路标交接火种，再回营地复命。三次受伤会烧毁火种。",
                    "Signal dispatch started: reach the distant real ridge marker, then return to this camp within 5 minutes. Three damaging hits destroy the packet.");
                return true;
            }
            if(!SignalRelayActive)return true;
            if(action=="annex_relay_abort")
            {
                if(source.kind!="annexcamp"||
                    !Object.ReferenceEquals(source,signalRelayCamp))return true;
                ResetSignalRelay();revision++;sound="ui";
                Notify("已取消传讯，没有提交回执。可以再次接取。",
                    "Dispatch withdrawn without credit. You may retry.");
                return true;
            }
            if(action=="annex_relay_deliver")
            {
                if(signalRelayStage!=1||source.kind!="ridgemarker"||
                    !Object.ReferenceEquals(source,signalRelayMarker))return true;
                signalRelayStage=2;revision++;sound="quest";
                effects.Add(new Effect("discover",new Point(source.x,source.y),.7f));
                Notify("高地路标已确认信号。带着回执沿真实道路返回出发营地。",
                    "Ridge marker acknowledged. Carry its reply back to your departure camp.");
                return true;
            }
            if(signalRelayStage!=2||source.kind!="annexcamp"||
                !Object.ReferenceEquals(source,signalRelayCamp))return true;
            int completedRoom=signalRelayRoom;
            ResetSignalRelay();
            if(Has("annex_relay_done_"+completedRoom))return true;
            Flag("annex_relay_done_"+completedRoom);
            CreditCoins(20);CreditShards(1);AddXp(40);
            RecordMoment(relayMessageZh[completedRoom],relayMessageEn[completedRoom]);
            if(SignalRelayDoneCount==8&&!Has("annex_relay_all"))
            {
                Flag("annex_relay_all");CreditSkillPoints(1);
                RecordMoment("八地烽火都已收到来回确认。从此没有城门、军令或关卡能独占这些普通旅人互相寻找的路线。",
                    "All eight civilian dispatches return with acknowledgements. No gate or officer can monopolize a road made of ordinary travelers finding each other.");
            }
            revision++;sound="reward";
            Notify("烽火传讯完成：硬币 +20、碎片 +1、经验 +40。已完成 "+
                SignalRelayDoneCount+"/8。",
                "Signal dispatch complete: +20 coins, +1 shard, +40 XP. "+
                SignalRelayDoneCount+"/8 verified.");
            return true;
        }

        private bool TalkAnnexExploration(WorldObject o)
        {
            string kind=o.kind;
            if(kind=="annexinnarchive" || kind=="annexinnmessage")
            {
                if(save.room!=9 || !HasPhysicalInteractionReach(o))return true;
                if(kind=="annexinnmessage")
                {
                    bool firstLetter=!Has("annex_inn_letter");
                    if(firstLetter){Flag("annex_inn_letter");AddXp(15);revision++;sound="reward";}
                    Dialog(o.name,o.en,
                        "给每一位夜归的人：如果你迷路了，沿着有人点起营火的方向走。道路不是堡垒的财产，也不是公会的财产。它是记得你还要回家的人留下的。"+
                        (firstLetter?"\n\n首次读信：经验 +15。":""),
                        "To every late traveler: when lost, follow the light of a shared campfire. A road belongs neither to a fortress nor a guild; it belongs to those who remember you are coming home."+
                        (firstLetter?"\n\nFirst reading: +15 XP.":""),
                        C("留下这封信","Keep the letter","close"));
                    return true;
                }
                int camps=0,cache=0,vistas=0,signals=CampSignalCount,dispatches=SignalRelayDoneCount,watches=AfterglowWins;
                for(int region=0;region<8;region++)
                {
                    if(Has("annex_camp_annex_camp_"+region))camps++;
                    if(Has("annex_vista_annex_vista_"+region))vistas++;
                    if(Has("annex_cache_annex_cache_"+region+"_a"))cache++;
                    if(Has("annex_cache_annex_cache_"+region+"_b"))cache++;
                }
                bool complete=Has("annex_archive_complete");
                bool ready=camps==8&&vistas==8&&cache==16;
                Dialog(o.name,o.en,
                    "这是巡路队留在炉火旅店的真正旅行档案。你的步迹必须是实地走过的，而不是地图上凭空出现的标记。"+
                    "\n\n营地 "+camps+"/8  ·  观景点 "+vistas+"/8  ·  隐藏补给 "+cache+"/16。"+
                    "\n\n守火信号 "+signals+"/8 · 来回传讯 "+dispatches+
                    "/8 · 余烬守望 "+watches+"/8。"+
                    (Has("afterglow_all")?" 八地守望完成，营火防护上限提高到5次；其中主动截击 "+AfterglowCutWins+"/8。":"")+
                    (Has("afterglow_ending_shelter")?
                        " 余烬议事已选择公用避难所，营地休息恢复生命60。":
                        Has("afterglow_ending_watch")?
                        " 余烬议事已选择远程警戒，守火持续150秒。":"")+
                    (Has("annex_relay_archived")?
                        "\n\n《归途回声》已经存入旅店的公共史册。户外耐力恢复永久 +2/秒。":
                        Has("annex_relay_all")?
                        "\n\n八地烽火都收到回讯：技能点 +1。带着这些回执回旅店完成最后的记录。":
                        CampSignalNetworkPublished?
                        "\n\n信号网已公开。现在可在各地营地接取限时往返传讯，带回远处高地的真实回执。":"")+
                    (CampSignalNetworkPublished?
                        " 灯网公开后，守火持续120秒并可抵挡4枚来袭飞弹。":
                        signals==8?
                        " 八地守火已经实地点亮，可以把信号标准公布给所有旅人。":
                        " 在每个地区营地实际付出木材与药草点亮信号后，回来公开共用灯网。")+
                    UnlistedArchiveZh+
                    (complete?"\n\n这份公开地图已经交给所有旅人。":
                        ready?"\n\n每一处地点都被亲自验证。可以公开最后的归乡图。":
                        "\n\n继续探索全部八个户外区域，回来归档旧路。"),
                    "The Hearth Inn holds the wardens' actual travel record. Every mark must be made by walking, never conjured on a hidden map."+
                    "\n\nCamps "+camps+"/8 · Lookouts "+vistas+"/8 · Caches "+cache+"/16."+
                    "\n\nSignal fires "+signals+"/8 · Return dispatches "+dispatches+
                    "/8 · Afterglow watches "+watches+"/8."+
                    (Has("afterglow_all")?" Eight watches secured, "+AfterglowCutWins+
                        "/8 aggressive interceptions. Each fire now blocks five shots.":"")+
                    (Has("afterglow_ending_shelter")?
                        " Public shelters now restore 60 HP per camp rest.":
                        Has("afterglow_ending_watch")?
                        " Public early-warning signals now last 150 seconds.":"")+
                    (Has("annex_relay_archived")?
                        "\n\nHomeward Echoes is now part of the public chronicle. Outdoor stamina recovery is permanently +2/s.":
                        Has("annex_relay_all")?
                        "\n\nAll eight dispatches confirmed: +1 talent point. Return the replies to the Inn for a final public account.":
                        CampSignalNetworkPublished?
                        "\n\nThe public network now offers timed two-way dispatches at the eight real outdoor camps.":"")+
                    (CampSignalNetworkPublished?
                        " The public watch now protects 4 shots for 120 seconds per fire.":
                        signals==8?
                        " All eight paid field signals are verified. Publish a shared civilian fire code.":
                        " Prepare a paid signal fire at each region's real camp to make a public safety network.")+
                    UnlistedArchiveEn+
                    (complete?"\n\nYour route chart is now public.":ready?
                        "\n\nAll positions verified. The final homeward chart can be published.":
                        "\n\nExplore all eight outdoor regions and return to publish their chart."),
                    C(complete?"重读归乡图":"完成归档 · 公共星图",
                        complete?"Reread the chart":"Publish the public chart","annex_archive_file"),
                    signals==8&&!CampSignalNetworkPublished?
                        C("公开八地守火信号","Publish eight signal fires","annex_signal_publish"):
                        C("继续核对守火","Keep tracing signals","close"),
                    Has("annex_relay_all")&&SignalRelayDoneCount==8&&
                        !Has("annex_relay_archived")?
                        C("提交八地回执 · 归途回声","File eight replies · Homeward Echoes","annex_relay_archive"):
                        C("查看公开回执","Read public replies","close"),
                    Has("afterglow_all")&&AfterglowWins==8&&
                        !Has("afterglow_ending_shelter")&&
                        !Has("afterglow_ending_watch")?
                        C("余烬议事 · 决定公共安全方案",
                            "Afterglow Council · choose public policy","afterglow_epilogue"):
                        C("继续整理史册","Keep cataloguing","close"),
                    UnlistedAct<0?
                        C("调查《未列名者》","Investigate The Unlisted","unlisted_accept"):
                        UnlistedAct==3&&!UnlistedDone?
                        C("公开全本名册 · 开放通路","Publish the full registry · open routes","unlisted_public"):
                        C("查看未列名者进度","Review Unlisted progress","close"),
                    UnlistedAct==3&&!UnlistedDone?
                        C("保护姓名 · 秘密交还家属","Protect the names · return privately","unlisted_protect"):
                        C("继续整理","Keep cataloguing","close"),
                    C("继续旅程","Keep exploring","close"));
                return true;
            }
            if(kind!="annexcache"&&kind!="annexcamp"&&kind!="annexvista")return false;
            if(save.room<0||save.room>7||!HasPhysicalInteractionReach(o))return true;
            string key=(kind=="annexcache"?"annex_cache_":kind=="annexvista"?"annex_vista_":"annex_camp_")+o.id;
            bool first=!Has(key);
            int cacheMaterial = save.room==1 || save.room==7 ? 2 :
                save.room==3 || save.room==6 ? 1 : 0;
            if(first && kind=="annexcache" &&
                (save.coins > 999992 || save.inventory[cacheMaterial] > 9997))
            {
                Notify("探索补给仍可领取。请先留出7硬币和2份材料的容量。",
                    "Exploration supplies remain here. Free room for 7 coins and 2 materials.");
                return true;
            }
            if(first)
            {
                Flag(key);sound="reward";
                if(kind=="annexcache")
                {
                    CreditCoins(7);
                    CreditInventory(cacheMaterial, 2);
                }
                else if(kind=="annexvista")
                {
                    AddXp(12);
                    int cx=(int)o.x/6,cy=(int)o.y/6;
                    int columns=(Map.rows[0].Length+5)/6;
                    int rows=(Map.rows.Length+5)/6;
                    for(int x=cx-1;x<=cx+1;x++)for(int y=cy-1;y<=cy+1;y++)
                        if(x>=0&&x<columns&&y>=0&&y<rows&&(x*6>=36||y*6>=24))
                            Flag("annex_seen_"+save.room+"_"+x+"_"+y);
                }
                else
                {
                    CreditCoins(5);
                    bool all=true;
                    for(int i=0;i<8;i++)if(!Has("annex_camp_annex_camp_"+i))all=false;
                    if(all&&!Has("annex_camps_complete")){Flag("annex_camps_complete");CreditSkillPoints(1);}
                }
                revision++;
            }
            string[] zh={"外渠的雨脚印一直指向镇里。","荆棘远眺台的树根曾为旅客挡风。",
                "沉桥后湾里有不在公会账本上的船板。","荒矿旧道留下先撤人再开机的划痕。",
                "堡垒外墙的石阶原本供难民歇脚。","灯湾旧船铃是为错过退潮的人准备的。",
                "折光沙脊后藏着商队不曾登记的井。","霜钟背风崖的箭头指向最近的火盆。"};
            string[] en={"Rain-worn prints along the outer canal lead home.","Roots at the Thornwood overlook once sheltered travelers.",
                "Unregistered ferry planks remain in the backwater.","Mine marks command evacuation before restart.",
                "Outer citadel steps once sheltered displaced families.","The old ship bell still guides stranded travelers.",
                "A hidden well lies beyond the dunes.","A frost cliff sign points to the nearest hearth."};
            string noteZh=zh[save.room],noteEn=en[save.room];
            if(kind=="annexcache")
            {
                int part=UnlistedCachePart(o),act=UnlistedAct;
                bool evidence=part>=0&&act>=0&&act<3&&
                    !Has((part==0?"unlisted_a_":"unlisted_b_")+act);
                string relief=UnlistedReliefChoice(o);
                int reliefRegion=save.room==0?0:save.room==3?1:save.room==5?2:-1;
                Dialog(o.name,o.en,noteZh+(first?"\n\n遗失补给：硬币 +7、材料 +2。":"\n\n物资已被取走，记录仍在。")+
                    (evidence?"\n\n这处旧补给点保留着《未列名者》的原始记录。请亲手核验。":"")+
                    UnlistedReliefCacheNote(false,reliefRegion,relief)+
                    (save.room==4&&o.id=="annex_cache_4_a"?
                        LastLanternReturnTale(false):"")+
                    (save.room==4&&Has("boss_4")&&!LastLanternComplete?
                     "\n\n外墙旧补给箱底夹着一封未发的民用求救信。去东侧旧灯，再到南侧守望岗，找到传递这封信的理由。":
                     ""),
                    noteEn+(first?"\n\nRecovered supplies: +7 coins, +2 materials.":"\n\nSupplies already recovered.")+
                    (evidence?"\n\nThis original cache holds a missing registry clue. Verify it here.":"")+
                    UnlistedReliefCacheNote(true,reliefRegion,relief)+
                    (save.room==4&&o.id=="annex_cache_4_a"?
                        LastLanternReturnTale(true):"")+
                    (save.room==4&&Has("boss_4")&&!LastLanternComplete?
                     "\n\n外墙旧补给箱底夹着一封未发的民用求救信。真实的传递路线要先经过东侧旧求援灯，再到南侧守望岗。":
                     ""),
                    evidence?C("核验未列名者原始证据","Verify Unlisted evidence",
                        part==0?"unlisted_clue_a":"unlisted_clue_b"):
                        relief=="unlisted_relief_prepare"?
                            C("打包民用救助物资 · 木材×1、月光草×1",
                              "Pack civilian relief · 1 timber, 1 Moonherb",relief):
                        relief=="unlisted_relief_deliver"?
                            C("交付救助包 · 开通服务",
                              "Deliver aid kit · open station",relief):
                        relief=="unlisted_relief_treat"?
                            C("救助站治疗 · 月光草×1",
                              "Relief care · 1 Moonherb",relief):
                        C("继续巡路","Continue","close"),
                    UnlistedStewardReady(o)?
                        C("扩建社区诊所 · 木材×1、月光草×2",
                          "Build community clinic · 1 timber, 2 Moonherbs",
                          "unlisted_steward_clinic"):
                        C("继续巡路","Keep surveying","close"),
                    UnlistedStewardReady(o)?
                        C("修复本地高地 · 木材×2、零件×1",
                          "Repair local ridge · 2 timber, 1 gear",
                          "unlisted_steward_trail"):
                        C("继续整理","Keep cataloguing","close"),
                    // A new chapter branches from the real outer Citadel
                    // cache, without replacing the existing cache contents.
                    save.room==4&&Has("boss_4")&&!LastLanternComplete?
                        C(LastLanternActive?
                          "最后的灯讯 · 中止传送":"最后的灯讯 · 接受民用警报",
                          LastLanternActive?
                          "Last Lantern · withdraw":"Last Lantern · carry distress call",
                          LastLanternActive?"lastlantern_abort":"lastlantern_begin"):
                        C("继续探索","Keep exploring","close"));
                return true;
            }
            if(kind=="annexvista")
            {
                Dialog(o.name,o.en,noteZh+(first?"\n\n附近九块测绘区已记入星图。经验 +12。":"\n\n附近路线已在星图上。"),
                    noteEn+(first?"\n\nCharted nearby terrain sectors. +12 XP.":"\n\nNearby bearings already charted."),
                    C("收起星图","Fold chart","close"));
                return true;
            }
            Choice watch=AfterglowActive&&
                Object.ReferenceEquals(afterglowCamp,o)?
                (AfterglowPhase==2?
                    C("掩护避难者 · 常规二人阵","Screen shelters · two raiders","afterglow_screen"):
                    C("中止余烬守望","Abort afterglow watch","afterglow_abort")):
                Has("annex_relay_archived")&&Has("annex_relay_done_"+save.room)&&
                !Has("afterglow_done_"+save.room)?
                    C("余烬守望 · 双波夜袭","Afterglow watch · two-wave raid","afterglow_begin"):
                    C("查看守望记录","Read firewatch record","close");
            Choice watchCut=AfterglowActive&&AfterglowPhase==2&&
                Object.ReferenceEquals(afterglowCamp,o)?
                    C("主动截击 · 重甲三人阵","Intercept · armored trio","afterglow_cut"):
                    C("继续巡路","Keep walking","close");
            Choice dispatch=SignalRelayActive&&
                Object.ReferenceEquals(signalRelayCamp,o)?
                    (SignalRelayStage==2?
                        C("交回高地回执","Return the ridge reply","annex_relay_report"):
                        C("放弃本次传讯","Withdraw dispatch","annex_relay_abort")):
                CampSignalNetworkPublished&&!Has("annex_relay_done_"+save.room)?
                    C("出发烽火传讯 · 往返挑战","Carry the signal · round trip","annex_relay_begin"):
                    C("查看传讯记录","Review dispatch record","close");
            Dialog(o.name,o.en,noteZh+
                    "\n\n只有没有敌人追击、没有进行中的现场任务时才能安全生火。消耗木材 ×1，恢复生命 "+
                    (Has("afterglow_ending_shelter")?60:40)+" 和全部耐力。"+
                    (SignalRelayActive?"\n\n当前传讯："+SignalRelayStage+"/2段，火种完整度 "+
                        SignalRelayIntegrity+"/3，剩 "+(int)SignalRelaySeconds+" 秒。":
                        Has("annex_relay_done_"+save.room)?
                        "\n\n本地区的来回传讯已完成，回执已公开。":"")+
                    (AfterglowActive?"\n\n余烬守望 "+AfterglowPhase+"/3，敌人 "+
                        AfterglowRemaining+"，剩 "+(int)AfterglowTime+" 秒。":
                        Has("afterglow_done_"+save.room)?"\n\n本地余烬守望已胜利。":"")+
                    (first?"\n\n首次发现营地，硬币 +5。":"")+
                    (Has("annex_camps_complete")?"\n\n八处营地均已记载！技能点 +1。":""),
                    noteEn+"\n\nRest only when no enemies or active field operations remain. 1 timber restores "+
                    (Has("afterglow_ending_shelter")?60:40)+" HP and full energy."+
                    (SignalRelayActive?"\n\nDispatch stage "+SignalRelayStage+"/2 · flame "+
                        SignalRelayIntegrity+"/3 · "+(int)SignalRelaySeconds+"s left.":
                        Has("annex_relay_done_"+save.room)?
                        "\n\nThis region's return dispatch is already in the public archive.":"")+
                    (AfterglowActive?"\n\nAfterglow "+
                        AfterglowPhase+"/3 · foes "+AfterglowRemaining+
                        " · "+(int)AfterglowTime+"s.":
                        Has("afterglow_done_"+save.room)?"\n\nThis afterglow watch is cleared.":"")+
                    (first?"\n\nFirst discovery: +5 coins.":"")+
                    (Has("annex_camps_complete")?"\n\nAll eight camps mapped: +1 talent point.":""),
                    !FieldWorkPreventsRest&&SafeTravel?
                        C("添柴休息 · 木材 ×1","Rest · 1 timber","annex_rest"):
                        C("先完成现场行动","Finish fieldwork / clear threats","close"),
                    CampWardActive&&Object.ReferenceEquals(campWardCamp,o)?
                        C("熄灭守火","Douse signal fire","annex_ward_douse"):
                        C("布设守火 · 木材×2、月光草×1",
                          "Signal fire · 2 timber, 1 Moonherb","annex_ward_light"),
                    dispatch,watch,watchCut,
                    C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseAnnexExploration(string action)
        {
            if(action=="afterglow_epilogue"||
               action=="afterglow_ending_shelter"||
               action=="afterglow_ending_watch")
            {
                WorldObject archive=pendingDialogueSource;
                if(save.room!=9||mode!="world"||Dying||practicing||
                    !Has("afterglow_all")||AfterglowWins!=8||
                    Has("afterglow_ending_shelter")||
                    Has("afterglow_ending_watch")||
                    archive==null||archive.kind!="annexinnarchive"||
                    pendingDialogueSourceRoom!=9||
                    !SourceBelongsToActiveMap(archive)||
                    !HasPhysicalInteractionReach(archive))return true;
                if(action=="afterglow_epilogue")
                {
                    Dialog("余烬议事 · 归路常明","Afterglow Council · The Road Stays Lit",
                        "值夜人摊开八地战报。"+AfterglowCutWins+
                        "次主动截击，另外 "+(8-AfterglowCutWins)+
                        "次优先护送。每个营地都有同一个问题：下一次听见求救时，先保护谁？"+
                        "\n\n守路队没有宣布唯一正确的答案。他们请你把今后共同使用的安全方案写到公共记录里。"+
                        "\n\n选择后永久有效，不能重复领取结算。",
                        "The night keeper spreads eight reports across the inn table. "+
                        AfterglowCutWins+" aggressive interceptions and "+
                        (8-AfterglowCutWins)+" shelter screens. Each camp asks the same question: whom do we protect first next time?"+
                        "\n\nNo officer declares a single correct answer. Write the new public safety practice for everyone to share."+
                        "\n\nThis choice is permanent; the final award is one-time.",
                        C("优先开放避难所 · 休息生命 +20",
                            "Open civilian shelters · +20 camp healing","afterglow_ending_shelter"),
                        C("完善警戒网络 · 守火延长30秒",
                            "Extend warning network · +30s fireward","afterglow_ending_watch"),
                        C("暂不决定","Decide later","close"));
                    return true;
                }
                bool shelter=action=="afterglow_ending_shelter";
                Flag(shelter?"afterglow_ending_shelter":"afterglow_ending_watch");
                CreditCoins(55);CreditShards(2);
                RecordMoment(shelter?
                    "炉火旅店把守火胜利的一部分拨给每处公共避难所。夜归者可以先暖过来，再告诉大家自己看到了什么。":
                    "炉火旅店把守火胜利的一部分拨给长夜警戒。每一次多亮半分钟的火光，都是给迟归的人留出的时间。",
                    shelter?
                    "The Hearth Inn devotes the watches' reward to open shelters: travelers may recover first, then tell their stories.":
                    "The Hearth Inn devotes the watches' reward to an early-warning network, leaving an extra half-minute of light for travelers still returning.");
                revision++;sound="reward";
                Dialog("归路常明 · 公共决定","The Road Stays Lit · Public Decision",
                    shelter?
                    "记录员没有盖上军印，而是在纸边留下一个空栏，允许后来者写下新的避难地址。"+
                    "\n\n避难所方案生效：户外营地每次休息恢复60点生命，耐力仍全部恢复。"+
                    "\n\n硬币 +55，碎片 +2。":
                    "修钟匠把几枚旧齿轮装进共同的信号计时器。他说：只要有人还没回来，就别急着熄灯。"+
                    "\n\n警戒方案生效：每次付费守火可持续150秒，五次拦截规则不变。"+
                    "\n\n硬币 +55，碎片 +2。",
                    shelter?
                    "The keeper leaves room in the margin for tomorrow's shelter addresses."+
                    "\n\nShelter policy: every outdoor camp rest restores 60 HP and full stamina."+
                    "\n\n+55 coins, +2 shards.":
                    "A clockmaker reuses old cogs to keep public warning lights burning longer."+
                    "\n\nWatch policy: paid signal fires last 150 seconds with the same five contact charges."+
                    "\n\n+55 coins, +2 shards.",
                    C("收起议事记录","Close council record","close"));
                return true;
            }
            if(action=="annex_relay_archive")
            {
                WorldObject archive=pendingDialogueSource;
                if(save.room!=9||mode!="world"||Dying||practicing||
                    !Has("annex_relay_all")||SignalRelayDoneCount!=8||
                    Has("annex_relay_archived")||
                    archive==null||archive.kind!="annexinnarchive"||
                    pendingDialogueSourceRoom!=9||
                    !SourceBelongsToActiveMap(archive)||
                    !HasPhysicalInteractionReach(archive))return true;
                Flag("annex_relay_archived");
                CreditCoins(80);CreditShards(3);
                RecordMoment(
                    "炉火旅店里，值夜的记录员把八张浸湿的回执压平。有人问：那些还在路上的人都回家了吗？她没有敷衍，只把一盏小灯移到未写完的那一栏，说：还没有，但现在我们知道该往哪里去接他们。",
                    "At the Hearth Inn the night recorder smooths eight rain-soaked replies. Someone asks if every traveler is home. She moves a lamp beside the unfinished names: Not yet. But now we know where to meet them.");
                revision++;sound="reward";
                Dialog("归途回声 · 公共史册","Homeward Echoes · Public Chronicle",
                    "窗外的雨落在旧路牌上。有人轻轻擦净上面的名字，谁也没有喊口号。八地的水尺、风铃、矿铃和灯火，终于指向同一件事：还没回来的人，不该从地图上消失。"+
                    "\n\n记录员在最后一页盖上公共印记，又把纸递给门边的旅人，让他也抄一份。"+
                    "\n\n终章奖励：硬币 +80、碎片 +3。往后在八个户外地区，耐力恢复永久 +2/秒。",
                    "Rain taps on the old wayfinding signs. Someone carefully wipes a name clean. Nobody makes a speech. Eight gauges, chimes, mine bells and fires now serve one promise: people still on the road must not disappear from a map."+
                    "\n\nThe recorder stamps the public copy, then hands the page to a traveler by the door so he can copy it too."+
                    "\n\nFinal reward: +80 coins, +3 shards. Stamina now regenerates +2/s throughout the eight outdoor regions.",
                    C("收起公共史册","Close the public chronicle","close"));
                return true;
            }
            if(action=="annex_signal_publish")
            {
                // Publication is a real in-person conversation with the
                // surviving archive, not an action replayed from another NPC.
                WorldObject archive=pendingDialogueSource;
                if(save.room!=9||Dying||practicing||CampSignalNetworkPublished||
                    archive==null||archive.kind!="annexinnarchive"||
                    pendingDialogueSourceRoom!=9||
                    !SourceBelongsToActiveMap(archive)||
                    !HasPhysicalInteractionReach(archive))return true;
                if(CampSignalCount!=8)
                {
                    Notify("仍有没点亮守火的地区。公共信号网不能只在地图上凭空画出来。",
                        "The network needs eight real field signals, not imagined map marks.");
                    return true;
                }
                Flag("annex_signal_network_published");
                CreditCoins(40);CreditShards(2);AddXp(90);
                RecordMoment("炉火旅店收到了八地信号的原始记录。守路人把火光如何避开断墙、保护晚归的行人，写成任何人都能使用的公用守火准则。火光不再只照亮有通行证的道路。",
                    "The Hearth Inn publishes the eight proven fire signals as a civilian ward code. Their lights protect anyone returning at night, not merely travelers carrying a citadel permit.");
                revision++;sound="reward";
                Notify("八地守火信号网已公开！奖励硬币40、碎片2、经验90；今后守火可持续120秒、拦截4枚飞弹。",
                    "The eight-signal network is public! +40 coins, +2 shards, +90 XP. Fires now last 120 seconds and block 4 shots.");
                return true;
            }
            if(action=="annex_archive_file")
            {
                if(save.room!=9||Dying||practicing||Has("annex_archive_complete"))
                    return true;
                WorldObject archive=null;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="annexinnarchive"){archive=o;break;}
                if(archive==null||!HasPhysicalInteractionReach(archive))
                    return true;
                for(int region=0;region<8;region++)
                    if(!Has("annex_camp_annex_camp_"+region)||
                        !Has("annex_vista_annex_vista_"+region)||
                        !Has("annex_cache_annex_cache_"+region+"_a")||
                        !Has("annex_cache_annex_cache_"+region+"_b"))
                    {
                        Notify("星图还缺少实际走过的地点。","Some routes have yet to be visited.");
                        return true;
                    }
                Flag("annex_archive_complete");
                CreditCoins(60);CreditShards(3);CreditSkillPoints(1);
                RecordMoment("八地营地、观景点与补给旧路均已核实。炉火旅店的公开星图不再遗漏任何普通旅人。",
                    "Eight regions of camps, lookouts and recovered supplies are now public on the Hearth Inn's atlas.");
                revision++;sound="reward";
                Notify("归乡图已公开：硬币 +60，碎片 +3，技能点 +1。",
                    "Public chart complete: +60 coins, +3 shards, +1 talent point.");
                return true;
            }
            if(ChooseAfterglow(action))return true;
            if(ChooseSignalRelay(action))return true;
            if(ChooseCampWard(action))return true;
            if(action!="annex_rest")return false;
            if(save.room<0||save.room>7||mode!="world"||Dying||practicing)return true;
            // Only the exact camp the player spoke to can spend supplies.
            // A stale/foreign dialogue must not silently consume timber at
            // whichever camp happens to be first in the room's object list.
            WorldObject camp=pendingDialogueSource;
            if(camp==null||camp.kind!="annexcamp"||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(camp)||
                !HasPhysicalInteractionReach(camp))return true;
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("现场行动或敌人的攻击尚未结束，不能安全休息。",
                    "Finish fieldwork and clear nearby attacks before resting.");
                return true;
            }
            if(save.health>=save.maxHealth&&save.energy>=100f)
            {
                Notify("生命和耐力已满，保留木材下次使用。",
                    "Health and energy are full; save your timber for later.");
                return true;
            }
            if(!Spend(1,0,0,0,0))return true;
            int healing=Has("afterglow_ending_shelter")?60:40;
            save.health=Clamp(save.health+healing,0,save.maxHealth);save.energy=100;
            revision++;sound="reward";
            Notify("营火补给：生命恢复 "+healing+"，耐力全满。",
                "Campfire: +"+healing+" HP and full stamina.");
            return true;
        }
    }
}
