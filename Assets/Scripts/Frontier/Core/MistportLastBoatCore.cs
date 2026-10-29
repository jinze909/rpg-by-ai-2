using System;
namespace Starfall
{
    // The Last Boat Home: a moving real-world civilian vessel, never a
    // dialogue-only completion. The hull travels on original Mistport
    // water, can be lost to surf or abandonment, and needs two physically
    // attended shore signals before safe harbor. No SaveData v2 changes.
    public sealed partial class GameCore
    {
        private int mistConvoyStage;
        private float mistConvoyClock,mistConvoyHull,mistConvoySwell,mistConvoyBrace;
        private Point mistConvoyBoat;
        private bool mistConvoyPartUsed,mistConvoyDrifterRescued;
        public bool MistConvoyActive {get{return mistConvoyStage>0&&save.room==5;}}
        public bool MistConvoyComplete {get{return Has("mistconvoy_public")||Has("mistconvoy_hidden");}}
        public bool MistConvoyPublic {get{return Has("mistconvoy_public");}}
        public bool MistConvoyHidden {get{return Has("mistconvoy_hidden");}}
        public int MistConvoyStage {get{return MistConvoyActive?mistConvoyStage:0;}}
        public float MistConvoySeconds {get{return MistConvoyActive?mistConvoyClock:0f;}}
        public float MistConvoyHull {get{return MistConvoyActive?mistConvoyHull:0f;}}
        public float MistConvoySwell {get{return MistConvoyActive?mistConvoySwell:0f;}}
        public float MistConvoyBraceSeconds {get{return MistConvoyActive?mistConvoyBrace:0f;}}
        public bool MistConvoyPartUsed {get{return MistConvoyActive&&mistConvoyPartUsed;}}
        public bool MistConvoyDrifterAboard {get{return MistConvoyActive&&mistConvoyDrifterRescued;}}
        public Point MistConvoyBoatFoot {get{return MistConvoyActive?mistConvoyBoat:new Point();}}
        public bool MistConvoyBoatFollowing {get{return MistConvoyActive&&mistConvoyStage>=2&&
            (Position-mistConvoyBoat).Length<=6.5f;}}
        public Point MistConvoyTarget {get{return mistConvoyStage==1?
            new Point(68.5f,38.5f):mistConvoyStage==2?
            new Point(69.5f,40.5f):mistConvoyStage>=3?
            new Point(69.5f,44.5f):new Point(66.5f,37.5f);}}
        public string MistConvoyObjective {get{return mistConvoyStage==1?
            L("在北岸发出实际引航号角","SOUND THE NORTH BANK HORN"):
            mistConvoyStage==2?L("跟船沿岸前进，在中继台确认位置","WALK WITH THE BOAT · CONFIRM MIDWAY"):
            mistConvoyStage==3?L("接应驶抵南码头的载人船","RECEIVE SURVIVORS AT THE SOUTH DOCK"):
            L("读取雾港求援航线记录","READ THE HARBOR RESCUE LOG");}}
        // Heard from the ORIGINAL Hearth Inn night traveler after the
        // actual moving-hull reception. These memories are branch-specific
        // civic consequences of v2 flags, not another free quest reward.
        private string MistConvoyInnNews(bool english)
        {
            string drifterNews=Has("mistconvoy_drifter")?
                (english?" The woman pulled from the relay water asks the keeper for paper. For the first time in years, she writes her own name.":
                    " 从中继台救起的女子向店主借了纸，许多年来第一次亲手写下自己的名字。"):"";
            if(MistConvoyPublic)
                return english?
                    "\n\nThe night traveler has set twelve chipped mugs on the inn table. A sailor with salt still on her sleeves keeps counting them. 'There were thirteen of us,' she says. 'The youngest stayed aboard until everybody else got off. Then he jumped.' The innkeeper brings a thirteenth mug. At the harbor, the new refuge keeps a light burning for the next unnamed boat."+drifterNews:
                    "\n\n守夜旅人在桌上摆了十二只磕口的杯子。一个袖口还沾盐的水手反复数着，低声说：‘我们明明有十三个人。最小的那个非要等大家都上岸才跳船。’店主默默送来第十三只杯子。港口新开的避风站已经点起灯，等着下一艘没有名字的船。"+drifterNews;
            if(MistConvoyHidden)
                return english?
                    "\n\nThe traveler folds the wet mooring rope into a neat coil beneath his chair. 'The patrol counted twelve passengers,' he says. 'I counted thirteen.' He neither asks a name nor writes it down. Across the room a quiet pilot dries a child's scarf at the hearth. Outside, the sheltered channel carries another boat past the watchtower without a bell."+drifterNews:
                    "\n\n守夜旅人把湿漉漉的缆绳卷好，塞到椅子底下。‘巡兵说船上十二个人，’他说，‘我数出来十三个。’他没问多出的那个人叫什么，更没有记在纸上。炉边的女引航员正替一个孩子烘干围巾。塔楼外的暗水道又悄悄送过一艘船，钟一次也没响。"+drifterNews;
            return "";
        }
        // Drifter's Tide Chart: a new physically earned follow-up after
        // recovering the passenger. Existing SaveData v2 string flags carry
        // permanent progress, with no fabricated crew or rerun escort.
        public bool MistChartUnlocked {get{return Has("mistconvoy_drifter");}}
        public bool MistChartStarted {get{return Has("mistchart_started");}}
        public bool MistChartRecovered {get{return Has("mistchart_recovered");}}
        public bool MistChartPublic {get{return Has("mistchart_public");}}
        public bool MistChartSecret {get{return Has("mistchart_secret");}}
        public bool MistChartComplete {get{return MistChartPublic||MistChartSecret;}}
        public bool MistChartActive {get{return MistChartStarted&&!MistChartComplete;}}
        public Point MistChartTarget {get{return !MistChartStarted?
            new Point(66.5f,37.5f):!MistChartRecovered?
            new Point(69.5f,40.5f):new Point(69.5f,44.5f);}}
        public string MistChartObjective {get{return !MistChartStarted?
            L("从船员名单寻找遗失航图","FOLLOW THE CREW'S LOST TIDE CHART"):
            !MistChartRecovered?L("在中继台打捞潮汐航图","SALVAGE THE RELAY TIDE CHART"):
            L("在南码头决定航图去向","DECIDE THE CHART'S FATE AT SOUTH DOCK");}}
        private bool ChooseMistChart(string action)
        {
            if(action!="mistchart_start"&&action!="mistchart_recover"&&
               action!="mistchart_public"&&action!="mistchart_secret")
                return false;
            WorldObject site=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||!MistChartUnlocked||
                !MistConvoyComplete||site==null||
                pendingDialogueSourceRoom!=5||
                !MistConvoySource(site,site.id)||MistChartComplete)return true;
            if(action=="mistchart_start")
            {
                if(site.id!="mistconvoy_notice"||MistChartStarted)return true;
                Flag("mistchart_started");TrackQuest("mistport_tidechart");
                RecordMoment("落水者记得遗失在雾港中继台下的潮汐航图。地图上的暗礁能让下一艘载人船避开风暴。",
                    "The rescued drifter remembers a tide chart caught beneath Mistport's relay. Its shoal marks could save the next crowded boat.");
                revision++;sound="quest";
                Notify("沿原有东岸中继台寻找潮汐航图。","Seek the tide chart at the original east-bank relay.");
                return true;
            }
            if(action=="mistchart_recover")
            {
                if(site.id!="mistconvoy_mid"||!MistChartStarted||
                    MistChartRecovered||!SafeTravel||FieldWorkPreventsRest)return true;
                if(save.energy<12f)
                {Notify("打捞需要12耐力。","Salvage requires 12 energy.");return true;}
                if(save.inventory[0]<1||save.inventory[4]<1)
                {Notify("需要木材1和零件1固定打捞绳。","Bring one timber and one machine part to rig the line.");return true;}
                save.energy-=12f;save.inventory[0]--;save.inventory[4]--;
                Flag("mistchart_recovered");revision++;sound="quest";
                Notify("潮汐航图从水中取出！送去南码头决定如何使用。",
                    "Tide chart recovered from real water. Bring it to the south dock.");
                return true;
            }
            if(site.id!="mistconvoy_dock"||!MistChartRecovered||
                !SafeTravel||FieldWorkPreventsRest)return true;
            bool publicChart=action=="mistchart_public";
            Flag(publicChart?"mistchart_public":"mistchart_secret");
            CreditCoins(45);AddXp(90);
            RecordMoment(publicChart?
                "无名落水者的航图标出了一条向所有船只开放的安全浅滩线。中继台旁如今有人为受伤的水手守灯。":
                "航图被交给守夜引航员，暗礁位置仅在可信任的船员之间传递。东岸秘密航路的后勤终于稳定。",
                publicChart?
                "The rescued drifter's chart marked shoals for every vessel; a public first-aid watch now staffs the old relay.":
                "Night pilots safeguard the drifter's chart, keeping the dangerous hidden shoals known only to trusted crews.");
            revision++;sound="reward";
            Notify(publicChart?
                "航图公开：硬币+45、经验+90；安全中继台可恢复生命。":
                "航图保密：硬币+45、经验+90；雾港安全区域耐力恢复+2。",
                publicChart?
                "Chart shared: +45 coins, +90 XP; safe relay grants wound healing.":
                "Chart protected: +45 coins, +90 XP; +2 energy recovery in safe Mistport.");
            return true;
        }
        private void ResetMistConvoy()
        {
            mistConvoyStage=0;mistConvoyClock=mistConvoyHull=mistConvoySwell=mistConvoyBrace=0f;
            mistConvoyBoat=new Point();mistConvoyPartUsed=false;mistConvoyDrifterRescued=false;
        }
        private bool MistConvoySource(WorldObject o,string id)
        {
            return o!=null&&save.room==5&&o.id==id&&
                SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);
        }
        // Occupancy of the authored ~ water channel must hold for every
        // physical frame. The civilians' boat never uses hero ground
        // collision or magically crosses stone and solid docks.
        private bool MistConvoyInWater(Point p)
        {
            if(save.room!=5||p.x<0||p.y<0||
                p.x>=Map.rows[0].Length||p.y>=Map.rows.Length)return false;
            return Map.rows[(int)p.y][(int)p.x]=='~';
        }
        private bool TalkMistConvoy(WorldObject o)
        {
            if(o==null||save.room!=5||!o.kind.StartsWith("mistconvoy",
                StringComparison.Ordinal))return false;
            if(!MistConvoySource(o,o.id))return true;
            if(o.id=="mistconvoy_notice")
            {
                if(!MistlightComplete)
                {
                    Dialog(o.name,o.en,
                        "这是旧港留下的最后一份船员记录。风暴里的灯还没恢复，远处的船也不会靠近岸线。",
                        "This is the final crew ledger. The lost beacon still needs restoring before any ship can follow the shoreline.",
                        C("离开","Leave","close"));return true;
                }
                if(MistConvoyComplete)
                {
                    if(MistChartUnlocked&&!MistChartStarted)
                    {
                        Dialog(o.name,o.en,
                            "落水者趴在船员名单上指出一处被潮水淹没的中继木桩。她把航图藏在那里，标出了风暴中无人敢走的浅滩。能不能把它取回来，让下一艘船不再迷路？",
                            "The drifter points to a submerged relay piling on the crew list. She hid a tide chart there, marking shoals no captain would dare cross in a storm. Can you recover it before the next vessel is lost?",
                            C("寻找落水者的潮汐航图","Find the drifter's tide chart","mistchart_start"),
                            C("等准备好再说","Not yet","close"));return true;
                    }
                    Dialog(o.name,o.en,MistConvoyPublic?
                        "你救回的船员如今掌管公开避风驿站。灯熄后，也有人会替陌生船点上第二盏。":
                        "返乡船员把暗水道记在心里，没有交给巡兵。有人专门守着秘密引航的讯号。",
                        MistConvoyPublic?
                        "The crew you rescued now tend the public refuge. Someone lights a second beacon even when the first goes out.":
                        "The returning crew kept the hidden channel out of the patrol's hands. Someone watches for its quiet signal.",
                        MistChartComplete?
                            C("查看航图后续","Review the chart's outcome","close"):
                            C("继续前行","Continue","close"));return true;
                }
                if(!MistConvoyActive)
                {
                    Dialog(o.name,o.en,
                        "守灯人留下的第二页并非航图，而是船员名单。载着人的小船还在外侧暗水道里打转。先从北岸吹响号角，随后沿着东岸跟船前进，到中继台确认船身真的通过，再到南码头接他们上岸。浪头每8秒冲击船体，离船太远时小船会失去引航；备件可在号角台加固一次。船沉或时间耗尽可以重试。",
                        "The keeper's second page is not a chart but a crew list. A crowded boat is still circling the outer channel. Sound the north-bank horn, walk alongside the REAL moving hull, confirm its actual passage at the midway relay, then receive everyone at the south dock. Swells strike every 8s; leave the boat too far behind and it loses your guidance. One spare part can brace it at the horn. A lost boat or timeout allows another rescue attempt.",
                        C("出发接应最后一船","Launch the final boat rescue","mistconvoy_start"),
                        C("以后再来","Not now","close"));return true;
                }
                Dialog(o.name,o.en,
                    "远处的船还在摇晃。完成靠岸接应，或回到这里明确放弃这次行动。",
                    "The rescue boat is still in the surf. Finish the landing or explicitly withdraw at this log.",
                    C("继续护航","Keep escorting","close"),
                    C("撤销这次护航","Abort escort","mistconvoy_abort"));return true;
            }
            if(o.id=="mistconvoy_horn")
            {
                if(mistConvoyActiveAtStage(1))
                {
                    Dialog(o.name,o.en,
                        "雾中没有回声，只有断续的船桨声。吹响引航号角需要8点耐力；船一旦听见，就会开始沿真实水道移动。",
                        "There is no echo in the fog, only the sound of oars. The true guidance horn takes 8 energy. Once the boat hears it, it begins to move in the real channel.",
                        C("耐力8 · 发出引航号角","8 energy · Sound rescue horn","mistconvoy_horn"),
                        C("再观察一下","Watch the waves","close"));return true;
                }
                if(MistConvoyActive&&!mistConvoyPartUsed)
                {
                    Dialog(o.name,o.en,
                        "号角台还留着一段备用肋骨支架。消耗1零件可修好船体25点耐久，并挡住接下来10秒的浪头。每次护航只能加固一次。",
                        "The horn post still has a spare hull brace. Spend 1 machine part to repair 25 hull and fend off swells for 10s. Once per crossing.",
                        C("备件1 · 加固船体","1 part · Reinforce hull","mistconvoy_brace"),
                        C("保留备件","Keep part","close"));return true;
                }
                Dialog(o.name,o.en,
                    "号角已响起，船员正沿着你的灯火往南划。",
                    "The horn has sounded. The crew are rowing south along your signal.",
                    C("离开","Leave","close"));return true;
            }
            if(o.id=="mistconvoy_mid")
            {
                if(MistChartActive&&!MistChartRecovered)
                {
                    Dialog(o.name,o.en,
                        "桩脚下有一张油布包着的潮汐航图。浪还在打转；要用一根木材和一个零件搭打捞绳，再耗费12耐力拉起它。没有人能从岸上凭空拿走这份记录。",
                        "An oilskin tide chart is wedged beneath the real relay piling. Rig a salvage line with one timber and one machine part, then spend 12 energy hauling it up. It cannot be claimed from across the harbor.",
                        C("支付物资 · 打捞潮汐航图","Spend materials · Recover chart","mistchart_recover"),
                        C("先准备物资","Gather supplies first","close"));return true;
                }
                // The relay is a physical, short-lived rescue window. The
                // drifting passenger cannot be recovered from a remote menu
                // or after the guided boat has left this stretch of water.
                if(mistConvoyStage==3&&!mistConvoyDrifterRescued&&
                    mistConvoyBoat.y>=40.45f&&mistConvoyBoat.y<=41.25f)
                {
                    Dialog(o.name,o.en,
                        "回声已经传向南港。一个湿透的水手却抓着中继台下的木桩：她不是船员，名字也不在名单上。让小船折回接她，需要18点耐力，还会损失12秒和12点船体耐久。现在冒险，还是先把船上的人安全送走？",
                        "The relay signal has reached the south dock. But a drenched stranger clings to a piling below. She is not on the crew list. Drawing the boat close enough to take her aboard costs 18 energy, 12 seconds and 12 hull integrity. Risk the crossing, or bring the crew home first?",
                        C("冒险拉她上船 · 耐力18","Pull her aboard · 18 energy","mistconvoy_drifter"),
                        C("先护送现有船员","Protect the crew first","close"));return true;
                }
                Dialog(o.name,o.en,
                    mistConvoyStage==2?
                        "中继台的铜片震动着。只有船身真正驶到这段水道，才能发出下一个方向的回声。":
                        "中继台上的铜片已经安静下来。",
                    mistConvoyStage==2?
                        "The relay plate trembles. It can direct the survivors only after their hull physically reaches this stretch of the channel.":
                        "The relay plate is quiet now.",
                    C("确认小船位置","Confirm boat passage","mistconvoy_mid"),
                    C("再等一会儿","Wait here","close"));return true;
            }
            if(o.id=="mistconvoy_dock")
            {
                if(MistChartActive&&MistChartRecovered)
                {
                    Dialog(o.name,o.en,
                        "那张潮汐航图终于晾干。落水者说：‘危险的不是海，是没人知道船可以走哪儿。’你可以在中继台为每条船公开安全浅滩线、组织疗伤；也可以把暗礁标记交给守夜引航员，守住秘密的归航能力。",
                        "The tide chart has dried. The drifter says, 'The sea was never the danger. Not knowing where we could go was.' Publish the safe shoals and staff a first-aid watch, or entrust the chart to night pilots for a protected hidden supply route?",
                        C("公开浅滩线 · 建立救护岗","Publish shoals · Staff first aid","mistchart_public"),
                        C("保护暗航图 · 支援引航员","Keep chart secret · Supply pilots","mistchart_secret"));return true;
                }
                if(mistConvoyStage==3)
                {
                    Dialog(o.name,o.en,
                        (mistConvoyDrifterRescued?"中继台下救起的女子靠着船桨喘息。":"")+"南码头的绳索已备好。船上的人仍在紧抓船沿。你要把他们送往公开避风站，让每位失去船籍的人都有地方休息，还是安排守夜引航员接走，保护那些不能在巡防名单上出现的乘客？",
                        (mistConvoyDrifterRescued?"The woman hauled from the relay clings to an oar, breathing hard. ":"")+"The south dock's lines are ready. People still cling to the rails. Bring them to the public refuge so every unregistered sailor can rest, or hand them to the night pilots who protect passengers unable to appear on patrol lists?",
                        C("安置在公共避风站","Open the civilian refuge","mistconvoy_public"),
                        C("交给秘密守夜引航员","Protect the night-pilot route","mistconvoy_hidden"));return true;
                }
                Dialog(o.name,o.en,
                    MistConvoyComplete?"船员已平安上岸。下一艘船也会知道哪里能找到灯火。":
                        "码头的绳索还没系上任何船。先去北岸接应，并沿岸护送小船真正靠近。",
                    MistConvoyComplete?"The rescued crew have come ashore. The next boat will know where to find the light.":
                        "No boat has made the dock yet. First guide the survivors from the north and stay alongside them.",
                    C("离开","Leave","close"));return true;
            }
            return false;
        }
        private bool mistConvoyActiveAtStage(int stage)
        {return MistConvoyActive&&mistConvoyStage==stage;}
        private bool ChooseMistConvoy(string action)
        {
            if(action==null||!action.StartsWith("mistconvoy_",StringComparison.Ordinal))return false;
            WorldObject site=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||site==null||
                pendingDialogueSourceRoom!=5||!MistConvoySource(site,site.id))return true;
            if(action=="mistconvoy_start")
            {
                if(site.id!="mistconvoy_notice"||MistConvoyActive||
                    MistConvoyComplete||!MistlightComplete||
                    FieldWorkPreventsRest||!SafeTravel)return true;
                ResetMistConvoy();mistConvoyStage=1;mistConvoyClock=86f;
                mistConvoyHull=76f;mistConvoySwell=8f;
                mistConvoyBoat=new Point(70.5f,38.5f);
                if(!MistConvoyInWater(mistConvoyBoat))
                {ResetMistConvoy();Notify("暗水道已被阻塞。","The channel is physically blocked.");return true;}
                TrackQuest("mistport_lastboat");revision++;sound="quest";
                Notify("船还在北岸外等你的号角。靠近东岸跟上真实船身；时间86秒。",
                    "Boat awaiting north horn. Stay near its actual water position; 86s to land.");
                return true;
            }
            if(action=="mistconvoy_abort")
            {
                if(site.id=="mistconvoy_notice"&&MistConvoyActive)
                {ResetMistConvoy();revision++;Notify("本次救援已撤销，可重新接应。","Escort withdrawn. Another attempt is available.");}
                return true;
            }
            if(!MistConvoyActive)return true;
            if(action=="mistconvoy_horn")
            {
                if(site.id!="mistconvoy_horn"||mistConvoyStage!=1)return true;
                if(save.energy<8f)
                {Notify("引航号角需要8点耐力。","The horn requires 8 energy.");return true;}
                save.energy-=8f;mistConvoyStage=2;revision++;sound="quest";
                Notify("小船开始移动！沿东岸跟船，不要让水道里的幸存者失去你的灯。",
                    "The real boat is moving. Walk the bank alongside it; do not leave its crew unguided.");
                return true;
            }
            if(action=="mistconvoy_brace")
            {
                if(site.id!="mistconvoy_horn"||mistConvoyPartUsed)return true;
                if(save.inventory[4]<1)
                {Notify("没有备用零件来加固船体。","No spare machine part to reinforce the hull.");return true;}
                save.inventory[4]--;mistConvoyPartUsed=true;
                mistConvoyHull=Math.Min(100f,mistConvoyHull+25f);
                mistConvoyBrace=10f;revision++;sound="quest";
                Notify("船体已加固：耐久+25，10秒内浪头不会击穿船板。",
                    "Hull reinforced: +25 integrity and 10 seconds of swell cover.");
                return true;
            }
            if(action=="mistconvoy_drifter")
            {
                if(site.id!="mistconvoy_mid"||mistConvoyStage!=3||
                    mistConvoyDrifterRescued||mistConvoyBoat.y<40.45f||
                    mistConvoyBoat.y>41.25f)return true;
                // A risky voluntary detour, not a free collectible. Success
                // becomes durable only if the whole boat actually lands.
                if(save.energy<18f)
                {Notify("体力不足，无法把落水者拉上来。","Not enough energy to reach the drifter.");return true;}
                if(mistConvoyClock<=14f||mistConvoyHull<=20f)
                {Notify("船体或剩余时间已不足以安全折返。","Too little time or hull left for a safe detour.");return true;}
                save.energy-=18f;mistConvoyClock-=12f;mistConvoyHull-=12f;
                mistConvoyDrifterRescued=true;revision++;sound="quest";
                Notify("落水者攀上了船沿！继续引航；只有全船靠岸，她才真正获救。",
                    "The drifter is aboard! Keep guiding; only a real landing saves her.");
                return true;
            }
            if(action=="mistconvoy_mid")
            {
                if(site.id!="mistconvoy_mid"||mistConvoyStage!=2)return true;
                if(mistConvoyBoat.y<40.45f)
                {Notify("船还没划到中继台。继续沿岸引航。","The boat has not reached the relay. Keep guiding it.");return true;}
                mistConvoyStage=3;revision++;sound="quest";
                Notify("中继台已确认船身经过！南码头正在等你接应。",
                    "Boat physically verified at midway! Continue to the south dock.");
                return true;
            }
            if(action=="mistconvoy_public"||action=="mistconvoy_hidden")
            {
                if(site.id!="mistconvoy_dock"||mistConvoyStage!=3||
                    MistConvoyComplete)return true;
                if(mistConvoyBoat.y<44.40f)
                {Notify("船还没靠到码头，不能替他们签收。","The boat has not reached the dock yet.");return true;}
                bool publicShelter=action=="mistconvoy_public";
                bool drifterAboard=mistConvoyDrifterRescued;
                ResetMistConvoy();Flag(publicShelter?"mistconvoy_public":"mistconvoy_hidden");
                CreditCoins(75);CreditShards(2);AddXp(125);
                if(drifterAboard)
                {
                    Flag("mistconvoy_drifter");CreditCoins(35);AddXp(60);
                    RecordMoment("中继台下无名的落水者被冒险带上船，并与最后一船的船员一同抵达了南港。",
                        "The unnamed drifter from beneath the relay made it ashore with the final boat.");
                }
                RecordMoment(publicShelter?
                    "你沿雾港东岸把最后一艘载人船平安护送至码头，并建立对无船籍者开放的夜间避风站。":
                    "你沿雾港东岸将最后一艘船领回码头，把无名乘客托付给守夜引航员，暗航道得以保全。",
                    publicShelter?
                    "You personally escorted the final survivor boat into port and opened the night refuge to unlisted sailors.":
                    "You personally brought the last boat ashore and entrusted undocumented passengers to trusted night pilots.");
                revision++;sound="reward";
                string rewardCoins=drifterAboard?"110":"75",
                    rewardXp=drifterAboard?"185":"125";
                Notify(publicShelter?
                    "最后一船获救！硬币+"+rewardCoins+"、碎片+2、经验+"+rewardXp+"；安全北岸引航台可恢复生命。":
                    "无名乘客安全上岸！硬币+"+rewardCoins+"、碎片+2、经验+"+rewardXp+"；雾港安全区域耐力恢复额外+3。",
                    publicShelter?
                    "Last boat saved! +"+rewardCoins+" coins, +2 shards, +"+rewardXp+" XP; safe north bank horn grants healing.":
                    "Undocumented passengers safely landed! +"+rewardCoins+" coins, +2 shards, +"+rewardXp+" XP; +3 energy recovery in safe Mistport.");
                return true;
            }
            return true;
        }
        private void TickMistConvoy(float dt)
        {
            if(mistConvoyStage==0)
            {
                if(MistChartPublic&&save.room==5&&!Dying&&!practicing&&
                    SafeTravel&&!FieldWorkPreventsRest&&
                    PhysicalRefugeReach(new Point(69.5f,40.5f),1.65f))
                    save.health=Math.Min(save.maxHealth,save.health+1.8f*dt);
                if(MistConvoyPublic&&save.room==5&&!Dying&&!practicing&&
                    SafeTravel&&!FieldWorkPreventsRest&&
                    PhysicalRefugeReach(new Point(68.5f,38.5f),2.6f))
                    save.health=Math.Min(save.maxHealth,save.health+2.4f*dt);
                return;
            }
            if(save.room!=5||Dying||practicing)
            {ResetMistConvoy();revision++;return;}
            mistConvoyClock-=dt;
            mistConvoySwell-=dt;
            if(mistConvoyBrace>0f)mistConvoyBrace=Math.Max(0f,mistConvoyBrace-dt);
            // Even a real ship already at the relay must wait for the shore
            // crew's physical confirmation, and a hull at the dock remains
            // subject to swells until civilians are actually received.
            if(mistConvoyStage>=2)
            {
                bool guided=(Position-mistConvoyBoat).Length<=6.5f;
                float limit=mistConvoyStage==2?40.5f:44.5f;
                if(guided&&mistConvoyBoat.y<limit)
                {
                    Point next=new Point(mistConvoyBoat.x,
                        Math.Min(limit,mistConvoyBoat.y+dt*.56f));
                    if(MistConvoyInWater(next))mistConvoyBoat=next;
                }
                if(!guided)mistConvoyHull=Math.Max(0f,mistConvoyHull-dt*2.8f);
            }
            if(mistConvoySwell<=0f)
            {
                mistConvoySwell+=8f;
                if(mistConvoyBrace<=0f)
                {
                    float waveDamage=MistlightPrivate?14f:18f;
                    mistConvoyHull=Math.Max(0f,mistConvoyHull-waveDamage);
                    Notify("暗水道浪头撞上小船！船体耐久下降。",
                        "Surf strikes the moving boat. Hull integrity falls.");
                    sound="alarm";
                }
                else
                {
                    Notify("加固船板挡住了浪头。","Braced hull withstands the surge.");sound="quest";
                }
            }
            if(mistConvoyClock<=0f||mistConvoyHull<=0f)
            {
                bool sunk=mistConvoyHull<=0f;
                ResetMistConvoy();revision++;
                Notify(sunk?"船体被浪头击穿。重新从岸边接应。":
                    "迷雾吞没了船的灯。未能在规定时间护送到港，可重新救援。",
                    sunk?"The hull failed under the surf. Retry from shore.":
                    "The boat disappeared into the fog before landing. You may retry.");
            }
        }
    }
}
