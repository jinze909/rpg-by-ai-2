using System;
namespace Starfall
{
    // Chapter Eleven: a genuinely timed, movement-based public power circuit.
    // Every valve is on the original Citadel south road; no editor-only object,
    // fake menu timer or SaveData v3 migration is required.
    public sealed partial class GameCore
    {
        private int conduitRoom=-1,conduitStage;
        private float conduitElapsed,conduitWalked,conduitIntegrity,conduitPulseLeft;
        private Point conduitLast;
        // An actual valve can ground its own circuit once per powered stage.
        // These are per-attempt bits, not new SaveData fields.
        private int conduitGroundedMask;
        private bool conduitRepairUsed;
        public bool CivicConduitRepairAvailable
        { get { return CivicConduitActive&&!conduitRepairUsed&&
            conduitIntegrity<85&&save.inventory[4]>=1; } }
        public bool CivicConduitGroundAvailable
        { get { return CivicConduitActive &&
            (conduitGroundedMask & (1 << conduitStage)) == 0 &&
            save.energy >= 20; } }
        // Stable campaign object identity, not a hard-coded X coordinate.
        // Moving a field valve in the authored map must never change its slot.
        public int CivicConduitValveIndex(WorldObject o)
        {
            if(o==null||o.kind!="civicconduitvalve")return -1;
            if(o.id=="civicConduitValve1")return 0;
            if(o.id=="civicConduitValve2")return 1;
            if(o.id=="civicConduitValve3")return 2;
            return -1;
        }
        public Point CivicConduitTarget(int stage)
        {
            stage=Math.Max(0,Math.Min(2,stage));
            string id="civicConduitValve"+(stage+1);
            foreach(WorldObject o in data.maps[4].objects)
                if(o.kind=="civicconduitvalve"&&o.id==id)
                    return new Point(o.x,o.y);
            return new Point(stage==0?104.5f:stage==1?108.5f:115.5f,74.5f);
        }
        public bool CivicConduitStarted { get{return Has("civic_conduit_started");} }
        public bool CivicConduitComplete { get{return Has("civic_conduit_complete");} }
        public bool CivicConduitActive { get{return conduitRoom==save.room&&save.room==4&&!Dying;} }
        public int CivicConduitStage { get{return CivicConduitActive?conduitStage:0;} }
        public float CivicConduitTimeLeft {get{return CivicConduitActive?Math.Max(0,80-conduitElapsed):0;}}
        public float CivicConduitIntegrity {get{return CivicConduitActive?conduitIntegrity:0;}}
        public bool CivicConduitWindowOpen
        {
            get{return CivicConduitActive&&
                (conduitElapsed+conduitStage*2f)%6f<1.9f;}
        }
        public float CivicConduitWindowRemaining
        {
            get
            {
                if(!CivicConduitActive)return 0;
                float phase=(conduitElapsed+conduitStage*2f)%6f;
                return phase<1.9f?1.9f-phase:6f-phase;
            }
        }
        private void ResetCivicConduit()
        {
            conduitRoom=-1;conduitStage=0;conduitElapsed=conduitWalked=conduitIntegrity=conduitPulseLeft=0;
            conduitLast=new Point();conduitGroundedMask=0;conduitRepairUsed=false;
            hazards.RemoveAll(h=>h.civicConduitOwned);
            effects.RemoveAll(e=>e.civicConduitOwned);
        }
        private void DamageCivicConduit(float damage,string source)
        {
            if(!CivicConduitActive||source=="environment"||damage<=0)return;
            conduitIntegrity=Math.Max(0,conduitIntegrity-Math.Max(10,Math.Min(35,damage*1.2f)));
            if(conduitIntegrity<=0)
            {
                ResetCivicConduit();revision++;
                Notify("真正的敌人攻击击穿了临时导体！返回西端调谐站重试。",
                    "Real combat strikes burned out the live conductor. Restart at the western console.");
            }
        }
        private void TickCivicConduit(float dt)
        {
            if(conduitRoom<0)return;
            if(save.room!=4||Dying||practicing){ResetCivicConduit();return;}
            Point now=Position;
            float step=(now-conduitLast).Length;
            if(!Finite(now.x)||!Finite(now.y)||step>2.4f)
            {
                ResetCivicConduit();revision++;
                Notify("线路因异常位移断开。必须亲自沿公用道路前进。",
                    "Circuit lost through impossible displacement. Walk the public road yourself.");
                return;
            }
            conduitLast=now;conduitWalked+=step;
            conduitElapsed+=Math.Max(0,dt);
            conduitPulseLeft-=dt;
            conduitIntegrity=Math.Max(0,conduitIntegrity-dt*.19f);
            if(conduitElapsed>=80||conduitIntegrity<=0)
            {
                ResetCivicConduit();revision++;
                Notify("公共导体已经耗尽。回到西端控制台重新接通，不会重复收取已捐的熔断器。",
                    "The civic conductor expired. Restart at the western console without another fuse charge.");
                return;
            }
            if(conduitPulseLeft<=0)
            {
                conduitPulseLeft=5.6f;
                hazards.Add(new Hazard{position=Position,radius=1.25f,timer=1.05f,
                    damage=13,kind="rift",civicConduitOwned=true});
                sound="windup";revision++;
                Notify("旧网格释放了可闪避的电弧预警！离开标记圆圈。",
                    "The old grid warns of a real arc impact. Leave its marked circle!");
            }
        }
        private bool TalkCivicConduit(WorldObject o)
        {
            if(o.kind!="civicconduitmaster"&&o.kind!="civicconduitvalve")return false;
            if((Position-new Point(o.x,o.y)).Length>2.2f)return true;
            if(save.room!=4)return true;
            if(!LastDirectiveComplete)
            {
                Dialog(o.name,o.en,
                    "九年前的电网仍被最后一道秘密指令控制。请先完成堡垒真相调查与居民公示。",
                    "The grid remains under the last sealed order. Finish the citadel inquiry and publish the civilian decision first.",
                    C("返回调查","Return to inquiry","close"));
                return true;
            }
            if(CivicConduitComplete)
            {
                Dialog(o.name,o.en,
                    LastDirectiveChoice==1?
                        "公开巡路网络已经接通。八地建好的民用避险棚可在安全时持续补充耐力。":
                        "紧急庇护网络已经接通。八地建好的民用避险棚可在安全时提供额外疗养。",
                    LastDirectiveChoice==1?
                        "The public grid now offers stamina at all safely rebuilt refuges.":
                        "The emergency grid now shares extra medical care at all safely rebuilt refuges.",
                    C("查看线路","Inspect grid","close"));return true;
            }
            if(o.kind=="civicconduitmaster")
            {
                if(CivicConduitActive)
                {
                    Dialog(o.name,o.en,
                        "暂时导体正在通电。调谐阀门必须按西向东的顺序在各自绿色稳定周期内亲自操作；直接返回这里可以中止。",
                        "The mobile conductor is live. Walk east and set each valve during its green stability window. Return here to abandon the attempt.",
                        C("中止本次调谐","Abort this run","civic_conduit_abort"),
                        C("继续行进","Continue","close"));
                    return true;
                }
                Dialog(o.name,o.en,
                    "居民拿回最后指令后，发现远地电力仍依赖一个危险的自动轮询器。先捐献一枚真正的零件作为熔断器，再亲自带电走过南部道路，依次在三个不同的稳定窗口调谐阀门。80秒内完成；旧网格每数秒会释放可闪避的脉冲。失败后熔断器无需再交。",
                    "With the last command public, three unstable southern valves remain. Donate one actual spare part as a fuse once, then WALK east to tune three valves during their separate green timing windows. Finish within 80 seconds; real warned grid surges must be dodged. Failed runs reuse the fuse.",
                    C(CivicConduitStarted?"重新接通公共线路":"捐献零件并开始通电",
                        CivicConduitStarted?"Restart public circuit":"Donate fuse and energize",
                        "civic_conduit_start"),
                    C("以后再来","Later","close"));
                return true;
            }
            int slot=CivicConduitValveIndex(o);
            if(slot<0)return true;
            if(!CivicConduitActive)
            {
                Dialog(o.name,o.en,"这处阀门只有在从西端接入真实电流后才能响应。",
                    "This valve responds only to a live conductor launched at the western console.",
                    C("查看线路","Review","close"));return true;
            }
            if(slot<conduitStage)
            {
                Dialog(o.name,o.en,"这一道开关已经真实调谐，继续向东前往下一处。",
                    "This valve is genuinely tuned. Continue east to the next one.",
                    C("继续","Continue","close"));return true;
            }
            if(slot>conduitStage)
            {
                Dialog(o.name,o.en,"下一道开关必须从西向东按次序接通，不能越级跳过电路。",
                    "The network must be tuned in order from west to east; skipping breaks the sequence.",
                    C("返回上一个","Backtrack","close"));return true;
            }
            bool open=CivicConduitWindowOpen;
            // The grounding option remains visible even at zero energy, so
            // players know the resource requirement before trying it.
            string controlZh=(conduitGroundedMask&(1<<conduitStage))==0?
                " 可花20耐力在此阀门接地一次，清除本电网当前预警并延缓下一次脉冲。":
                " 此阀门的接地机会已用过。";
            string controlEn=(conduitGroundedMask&(1<<conduitStage))==0?
                " Ground this valve once for 20 energy to clear active grid arcs and delay the next pulse.":
                " This valve's grounding has already been used.";
            bool repairVisible=conduitIntegrity<85&&!conduitRepairUsed;
            if(repairVisible)
            {
                controlZh+=" 导体明显受损：可花一枚备用零件现场恢复35点完整度，每次接通仅能修补一次。";
                controlEn+=" Conductor damaged: one spare part restores 35 integrity at this valve, once per run.";
            }
            var commands=new System.Collections.Generic.List<Choice>();
            commands.Add(C(open?"执行实际调谐":"关闭面板并等待绿色周期",
                open?"Tune now":"Close panel and wait for green",
                open?"civic_conduit_tune":"close"));
            if((conduitGroundedMask&(1<<conduitStage))==0)
                commands.Add(C("手动接地 · 耐力20",
                    "Ground valve · 20 energy","civic_conduit_ground"));
            if(repairVisible)
                commands.Add(C("现场抢修 · 零件1",
                    "Repair conductor · 1 spare part","civic_conduit_repair"));
            commands.Add(C("暂时离开","Leave","close"));
            Dialog(o.name,o.en,
                (open?"当前阀门稳定！":"当前阀门处于高压闭锁期。")+
                "\n导体完好度："+(int)Math.Ceiling(conduitIntegrity)+
                "%，剩余 "+(int)Math.Ceiling(CivicConduitTimeLeft)+
                " 秒。\n"+
                (open?"绿色窗口剩余 ":"下个绿色窗口还有 ")+
                CivicConduitWindowRemaining.ToString("0.0")+" 秒。"+
                (open?"请在相位结束前确认。":"关闭面板后时间才会继续，记得躲避电弧。")+controlZh,
                (open?"Valve stable now!":"Valve pressure locked!")+
                "\nConductor: "+(int)Math.Ceiling(conduitIntegrity)+
                "%; "+(int)Math.Ceiling(CivicConduitTimeLeft)+
                " seconds left.\n"+
                (open?"Green phase remaining: ":"Next green phase in ")+
                CivicConduitWindowRemaining.ToString("0.0")+"s. "+
                (open?"Confirm while stable.":"Close dialogue to resume the clock; dodge the arcs.")+controlEn,
                commands.ToArray());
            return true;
        }
        private bool ChooseCivicConduit(string action)
        {
            if(!action.StartsWith("civic_conduit_"))return false;
            if(save.room!=4||Dying||practicing||!LastDirectiveComplete||
                LastDirectiveChoice==0)return true;
            WorldObject master=null;WorldObject valve=null;
            foreach(WorldObject o in Map.objects)
            {
                if(o.kind=="civicconduitmaster")master=o;
                if(CivicConduitValveIndex(o)==conduitStage&&
                    (Position-new Point(o.x,o.y)).Length<=2.2f)
                    valve=o;
            }
            bool nearMaster=master!=null&&
                (Position-new Point(master.x,master.y)).Length<=2.2f;
            if(action=="civic_conduit_abort")
            {
                if(CivicConduitActive&&nearMaster)
                {ResetCivicConduit();revision++;Notify("已安全断开临时线路。","Temporary circuit safely disconnected.");}
                return true;
            }
            if(action=="civic_conduit_start")
            {
                if(!nearMaster||CivicConduitComplete||CivicConduitActive||
                    FarCourierActive||CivicResponseActive||TideSalvageActive||
                    !SafeTravel)return true;
                if(!Has("civic_conduit_started"))
                {
                    if(save.inventory[4]<1)
                    {
                        Notify("需要一枚真实的备用零件作为熔断器。","One real spare part is needed for the fuse.");return true;
                    }
                    save.inventory[4]--;Flag("civic_conduit_started");
                }
                conduitRoom=4;conduitStage=0;conduitElapsed=conduitWalked=0;
                conduitIntegrity=100;conduitPulseLeft=4.7f;
                conduitGroundedMask=0;conduitRepairUsed=false;
                conduitLast=Position;revision++;sound="quest";
                TrackQuest("civic_conduit");
                Notify("公共线路已通电！沿道路向东，在三处阀门绿色周期内操作。80秒。",
                    "Public conductor live! Walk east and tune all three valves during green phases within 80s.");
                return true;
            }
            if(action=="civic_conduit_ground")
            {
                if(!CivicConduitActive||valve==null||
                    (conduitGroundedMask&(1<<conduitStage))!=0)return true;
                if(save.energy<20)
                {
                    Notify("手动接地需要20耐力；仍可以正常躲避电弧。",
                        "Grounding needs 20 energy; you can still dodge the arc normally.");
                    return true;
                }
                save.energy-=20;conduitGroundedMask|=1<<conduitStage;
                // Grounding cancels only THIS circuit's live contact. Other
                // authored encounters' warning circles remain untouched.
                hazards.RemoveAll(h=>h.civicConduitOwned);
                effects.RemoveAll(e=>e.civicConduitOwned);
                conduitPulseLeft=Math.Max(conduitPulseLeft,4.8f);
                effects.Add(new Effect("discover",Position,.6f){civicConduitOwned=true});
                revision++;sound="guard";
                Notify("阀门接地成功！当前电弧已安全消散，下一次脉冲延后。",
                    "Valve grounded! Current grid arcs canceled; the next surge is delayed.");
                return true;
            }
            if(action=="civic_conduit_repair")
            {
                if(!CivicConduitActive||valve==null||conduitRepairUsed||
                    conduitIntegrity>=85)return true;
                if(save.inventory[4]<1)
                {
                    Notify("现场抢修需要一枚真实的备用零件。也可以继续躲避电弧。",
                        "Emergency repair needs one real spare part. You can continue dodging.");
                    return true;
                }
                save.inventory[4]--;conduitRepairUsed=true;
                conduitIntegrity=Math.Min(100,conduitIntegrity+35);
                effects.Add(new Effect("discover",Position,.6f){civicConduitOwned=true});
                revision++;sound="reward";
                Notify("导体已恢复35点完整度！本次线路不再允许重复抢修。",
                    "Conductor restored by 35 integrity. Only one repair is allowed per run.");
                return true;
            }
            if(action!="civic_conduit_tune"||!CivicConduitActive||valve==null||
                !CivicConduitWindowOpen||!SafeTravel)return true;
            int slot=CivicConduitValveIndex(valve);
            float required=conduitStage==0?7.2f:conduitStage==1?3.1f:5.1f;
            if(slot!=conduitStage||conduitWalked<required)return true;
            conduitStage++;conduitWalked=0;revision++;sound="quest";
            if(conduitStage==3)
            {
                ResetCivicConduit();Flag("civic_conduit_complete");
                CreditCoins(65);CreditShards(2);AddXp(95);
                revision++;sound="reward";
                RecordMoment(LastDirectiveChoice==1?
                    "第十一章：居民亲自调谐了三处真实阀门，公开电网把耐力供给送到八地已修好的避险棚。":
                    "第十一章：居民亲自调谐了三处真实阀门，无条件的庇护电网把基础医护送往八地避险棚。",
                    LastDirectiveChoice==1?
                    "Chapter Eleven: the public energy grid now supports rebuilt refuges after three physical valve calibrations.":
                    "Chapter Eleven: the civilian care grid now supports rebuilt refuges after three physical valve calibrations.");
                Notify("第十一章完成：硬币65、碎片2、经验95。八地公共避险棚获得新服务！",
                    "Chapter Eleven complete: 65 coins, 2 shards, 95 XP. Rebuilt regional refuges gain new services.");
            }
            else
                Notify("阀门已成功调谐。继续向东亲自抵达下一处，并等待绿色周期。",
                    "Valve tuned. Walk east to the next physical valve and wait for its green phase.");
            return true;
        }
    }
}