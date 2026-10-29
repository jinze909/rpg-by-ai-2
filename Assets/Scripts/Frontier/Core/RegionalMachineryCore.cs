using System;
namespace Starfall
{
    // Twenty-four authentic field devices in eight terrain districts.
    // Every site is a Campaign.json WorldObject on walkable terrain.
    // An ordered triple of paid, distinct physical interactions opens
    // persistent local civilian shelter: no teleports or fake NPCs.
    public sealed partial class GameCore
    {
        private static readonly int[,] regionalMachineSequence={
            {1,0,2},{0,2,1},{2,0,1},{1,2,0},
            {2,1,0},{0,1,2},{1,0,2},{2,0,1}
        };
        private static readonly string[,] regionalMachineChoiceZh={
            {"打开堤边分洪闸","逐户核对剩余配给","让下游溢洪阀泄水"},
            {"引导主根避开猎径","剪开捕兽绳结","用断枝标出归途"},
            {"铺平旁通木板","收紧低岸缆绳","固定高水位缆栓"},
            {"开启山背排气扇","先为矿工开撤离门","等压力归零才锁炉"},
            {"松开通往民区的侧门","稳住担架下坡道","点亮护墙民用灯"},
            {"调平岸边固定灯","加固礁石上的锚点","对齐雾港后岸信标"},
            {"展开实际遮阳板","读取真实井中的水位","将镜子对齐山影"},
            {"调整背风挡雪板","给行人留下安全火盆","拉动背风低钟绳"}
        };
        private static readonly string[,] regionalMachineChoiceEn={
            {"Open the canal diverter","Check the remaining rations door by door","Release the downstream overflow"},
            {"Bend the roots away from hunter trails","Cut the trapped snare rope","Mark the return path with branches"},
            {"Lay the side-crossing planks flat","Tension the low-bank winch","Tie down the high-tide mooring"},
            {"Open the rear mountain ventilator","Evacuate workers through the service gate","Lock the furnace only after pressure falls"},
            {"Free the hinge facing civilian homes","Brace the stretcher evacuation ramp","Light the public wall lantern"},
            {"Level the stationary shore light","Secure the reef anchor","Align the rear harbor beacon"},
            {"Raise a physical shade shutter","Read actual water beneath the well","Align the mirror with the mountain shadow"},
            {"Angle the leeward snow baffle","Stock a public warming brazier","Pull the low sheltered bell"}
        };
        private static readonly string[,] regionalMachineClueZh={
            {"水尺仍涨，渠口锁住时，低街只会先被淹。","锅里不缺食物，缺的是每家到底收到多少。","只有下游泄口打开，上游才敢恢复分洪。"},
            {"这截主根是给逃难者遮住捕猎者视线的。","绳结一直通向兽夹，先断开它。","安全的回林路线曾用折断的细枝标明。"},
            {"旧桥不在了，旁通板仍可架在原来的石脚上。","低岸的钢索松垮，一涨潮便卷走担架。","高处缆栓保留着暴雨后的人手磨痕。"},
            {"后山出风口堵着矿灰，炉火因此失控。","矿工们逃出的那扇小门被运矿轨道顶死。","先看指针回零，不要把余压闷进旧炉。"},
            {"城门外最后一段铰链曾让伤员离开。","下坡道的支脚松了，担架滑向石壁。","民用壁灯不该只在守军换岗时点亮。"},
            {"雾中唯一可信的是不会随船漂流的岸灯。","礁石锚绳曾被急浪反复拖断。","后岸旧灯是给退潮后走错路的人看的。"},
            {"晴天的遮阳板不是幻影，可给赶路人挡光。","水尺必须插进真正的井，不能量海市蜃楼。","镜面若对着太阳，会把旅人指向假高塔。"},
            {"雪从迎风口灌来，背风板要反向倾斜。","有人到这里时双手已经冻僵，先留点炭。","高钟的声响被风吹走，低钟才通向有人处。"}
        };
        private static readonly string[,] regionalMachineClueEn={
            {"The canal is rising. A locked divider floods the lower street first.","Food arrived, but nobody counted what each household received.","Only an open overflow can safely receive the upstream diversion."},
            {"These roots once hid fleeing travelers from the hunters.","The knot still runs all the way to a live snare: cut it first.","Wardens marked the safe return using broken twigs."},
            {"The old bridge is gone; side planks still rest on the original footings.","A slack low-bank cable washes a stretcher away at high tide.","The high mooring carries marks left by rescuers in the storm."},
            {"Coal dust clogs the rear vent, driving furnace pressure up.","The evacuation exit is jammed against the ore rails.","Wait for the gauge to settle before sealing the old furnace."},
            {"That hinge once opened the gate to injured civilians.","The stretcher ramp slips toward the cliff without its brace.","Civilian lanterns need not wait for the garrison's shift change."},
            {"Unlike boats, a fixed lamp does not move in the fog.","Surge after surge pulled the old rope from the reef anchor.","The rear harbor light guides travelers after the tide recedes."},
            {"A shade shutter is real even when the tower in the heat is not.","Measure the real water below, not the mirage on the ridge.","Pointing into the sun would guide people toward a false tower."},
            {"Wind drives snow into the road; angle the baffle toward shelter.","Someone reaches this stop with frozen hands. Leave warm coals.","The high bell's voice blows away. The low one reaches home."}
        };
        private bool RegionalMachineSite(WorldObject o)
        {
            if(o==null||o.kind!="regionalMachine"||save.room<0||
                save.room>=8||o.id==null)return false;
            return o.id.StartsWith("regional_machine_"+save.room+"_",
                StringComparison.Ordinal);
        }
        private int RegionalMachineIndex(WorldObject o)
        {
            if(!RegionalMachineSite(o))return -1;
            int index;
            if(!int.TryParse(o.id.Substring(
                ("regional_machine_"+save.room+"_").Length),out index))
                return -1;
            return index>=0&&index<3?index:-1;
        }
        public bool RegionalMachineDone(int room)
        {
            return room>=0&&room<8&&Has("regional_machine_done_"+room);
        }
        public int RegionalMachineProgress(int room)
        {
            if(room<0||room>=8)return 0;
            int count=0;
            for(int i=0;i<3;i++)
                if(Has("regional_machine_stage_"+room+"_"+i))count++;
            return count;
        }
        public int RegionalMachineNextIndex(int room)
        {
            if(room<0||room>=8)return -1;
            int step=RegionalMachineProgress(room);
            return step==3?-1:regionalMachineSequence[room,step];
        }
        public int RegionalMachineNetworkCount
        {
            get{int n=0;for(int r=0;r<8;r++)
                if(RegionalMachineDone(r))n++;return n;}
        }
        private bool TalkRegionalMachine(WorldObject o)
        {
            if(o==null||o.kind!="regionalMachine")return false;
            int station=RegionalMachineIndex(o);
            if(station<0||!SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position))return true;
            int room=save.room;
            if(!Has("biome_field_done_"+room))
            {
                Dialog(o.name,o.en,
                    regionalMachineClueZh[room,station]+
                    "\n\n先调查本地区原有的两处探路地标，再来决定如何修复这些居民设备。",
                    regionalMachineClueEn[room,station]+
                    "\n\nWalk and verify both original regional wayfinding landmarks before repairing civilian machinery.",
                    C("留下设备等待调查","Return after fieldwork","close"));
                return true;
            }
            int stage=RegionalMachineProgress(room);
            bool tuned=Has("regional_machine_stage_"+room+"_"+station);
            int expected=RegionalMachineNextIndex(room);
            string itemZh=station==0?"木材×1":station==1?"月光草×1":"零件×1";
            string itemEn=station==0?"1 timber":station==1?"1 Moonherb":"1 gear";
            WorldObject next=null;
            if(expected>=0)
                foreach(WorldObject site in Map.objects)
                    if(site!=null&&site.id=="regional_machine_"+room+"_"+expected)
                    {next=site;break;}
            Dialog(o.name,o.en,
                regionalMachineClueZh[room,station]+
                "\n\n八区不同的旧设施必须按当地事故记录依次修复。本区"+
                (RegionalMachineDone(room)?"三处已经重新运行。":
                 "完成 "+stage+"/3；下一站："+(next==null?"待查":next.name)+"。")+
                (tuned?"\n\n这里已经完成真实修复，材料不再重复扣除。":
                 station==expected?"\n\n当前装置可以操作，消耗"+itemZh+
                     "；错误调试耗费6耐力。":
                 "\n\n先沿地面走到记录要求的下一处装置。")+
                (RegionalMachineDone(room)?
                    "\n\n三处装置如今成为安全补给点：附近无敌人时可缓慢恢复生命、耐力并缓解冷热。":""),
                regionalMachineClueEn[room,station]+
                "\n\nEvery district follows its own original accident record. "+
                (RegionalMachineDone(room)?"All three local devices are operational.":
                 "Repaired "+stage+"/3. Next: "+(next==null?"unconfirmed":next.en)+".")+
                (tuned?"\n\nAlready repaired; no repeat material charge.":
                 station==expected?"\n\nThis device requires "+itemEn+
                     ". Wrong calibration costs 6 energy.":
                 "\n\nWalk to the next physically recorded device first.")+
                (RegionalMachineDone(room)?
                    "\n\nAll three sites are now safe civilian shelters. When threats are clear, they slowly restore health and energy and ease exposure.":""),
                !tuned&&station==expected?
                    C(regionalMachineChoiceZh[room,0],
                      regionalMachineChoiceEn[room,0],"regional_machine_pick_0"):
                    C("查看下一步","Read the route","close"),
                !tuned&&station==expected?
                    C(regionalMachineChoiceZh[room,1],
                      regionalMachineChoiceEn[room,1],"regional_machine_pick_1"):
                    C("继续辨认装置","Check the apparatus","close"),
                !tuned&&station==expected?
                    C(regionalMachineChoiceZh[room,2],
                      regionalMachineChoiceEn[room,2],"regional_machine_pick_2"):
                    C("继续记录","Read the inscription","close"),
                RegionalVigilChoice(o),
                C("继续行走","Continue","close"));
            return true;
        }
        private bool ChooseRegionalMachine(string action)
        {
            if(action==null||!action.StartsWith(
                "regional_machine_pick_",StringComparison.Ordinal))return false;
            WorldObject o=pendingDialogueSource;
            int station=RegionalMachineIndex(o),room=save.room;
            if(station<0||mode!="world"||Dying||practicing||
                pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position)||
                !Has("biome_field_done_"+room)||
                RegionalMachineDone(room)||FieldWorkPreventsRest||
                !SafeTravel)return true;
            if(station!=RegionalMachineNextIndex(room)||
                Has("regional_machine_stage_"+room+"_"+station))return true;
            int selected;
            if(!int.TryParse(action.Substring(
                "regional_machine_pick_".Length),out selected)||
                selected<0||selected>2)return true;
            if(selected!=station)
            {
                if(save.energy<6f)
                {
                    Notify("耐力不足，暂时不能继续试错。",
                        "Not enough stamina for another calibration.");
                    return true;
                }
                save.energy-=6f;revision++;sound="alert";
                Notify("错误调试让装置重新卡住，损失6耐力。回看真实铭牌。",
                    "Incorrect calibration stalls the device. -6 energy. Read its original inscription.");
                return true;
            }
            // An actual physical action spends real supplies; successful
            // stages are recorded as persistent v2 flags, independent
            // from renewable resource node flags and old save slots.
            if(!Spend(station==0?1:0,0,station==1?1:0,
                0,station==2?1:0))return true;
            int stage=RegionalMachineProgress(room);
            Flag("regional_machine_stage_"+room+"_"+station);
            effects.Add(new Effect("discover",new Point(o.x,o.y),.8f));
            if(RegionalMachineProgress(room)==3)
            {
                Flag("regional_machine_done_"+room);
                CreditCoins(25);AddXp(50);
                RecordMoment("第"+(room+1)+"地区的三处损坏装置都被实地找到并按居民记录修复。没有一条凭空出现的道路，每个安全点都是真正能休息的地方。",
                    "All three physically separated devices in district "+
                    (room+1)+" were repaired in the residents' original order. The route and shelters now exist at genuine visited positions.");
                if(RegionalMachineNetworkCount==8&&
                    !Has("regional_machine_all"))
                {
                    Flag("regional_machine_all");
                    CreditSkillPoints(1);CreditShards(2);
                    RecordMoment("八地的公共设施复工。道路之间各不相同，但每一处真正修好的补给点都欢迎后来者。",
                        "All eight distinct public networks are operational. Different roads, real repaired safe stops, open to every traveler.");
                }
                Notify("本地区三处装置全部修复：硬币25、经验50。野外安全补给已开放。",
                    "Three local devices repaired: +25 coins, +50 XP. Field shelters active.");
            }
            else
            {
                revision++;sound="quest";
                Notify("第"+(stage+1)+"处实地设备修复完成。去地图标出的下一处。",
                    "Physical device "+(stage+1)+" repaired. Walk to the next mapped station.");
            }
            return true;
        }
        private void TickRegionalMachineShelters(float dt)
        {
            if(save.room<0||save.room>=8||!RegionalMachineDone(save.room)||
                dt<=0f||Dying||practicing||FieldWorkPreventsRest||
                !SafeTravel||Map.objects==null)return;
            bool close=false;
            foreach(WorldObject site in Map.objects)
                if(site!=null&&site.kind=="regionalMachine"&&
                    RegionalMachineIndex(site)>=0&&
                    (Position-new Point(site.x,site.y)).Length<=3.5f)
                {close=true;break;}
            if(!close)return;
            float multiplier=RegionalVigilDone(save.room)?1.65f:1f;
            save.health=Clamp(save.health+dt*1.5f*multiplier,0f,save.maxHealth);
            save.energy=Clamp(save.energy+dt*3f*multiplier,0f,100f);
            save.heat=Math.Max(0f,save.heat-dt*1.5f);
            save.cold=Math.Max(0f,save.cold-dt*1.5f);
        }
    }
}
