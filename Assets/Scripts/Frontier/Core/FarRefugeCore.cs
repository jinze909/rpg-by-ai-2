using System;
namespace Starfall
{
    // Three authentic walk-to sites per exterior. A field log exposes the
    // correct local dial setting; a calibrated dial unlocks an actual refuge.
    // Only old SaveData v2 string flags persist; world/canon remain unchanged.
    public sealed partial class GameCore
    {
        private static readonly int[] FarDialAnswers={1,2,0,2,1,0,1,2};
        private static readonly string[] DialSignsZh={"琥珀","潮蓝","苔绿"};
        private static readonly string[] DialSignsEn={"Amber","Tideblue","Mossgreen"};
        private static readonly string[] FarWitnessZh={
            "镇外的洪水划痕比官方堤图更深，南边的逃生道曾由居民亲手修复。",
            "老根下的庇护记录提到，森林在最冷的时候仍把风挡在孩子身后。",
            "船工把真正的涨潮时刻刻在桩上；这份记录没有进入任何货单。",
            "矿工签署的停工清册写着先护送受伤者，再关闭晶矿抽取机。",
            "紧急疏散日志留下城外难民与守门卫兵共同打开侧门的时刻。",
            "雾港的航路修订来自水手们的口述，而不是过时的海关账簿。",
            "未登记水井旁的石片写着绿洲儿童的用水份额，后来被风沙盖住。",
            "避寒手册记下每个小棚的火种来源，钟声应当引导回家的人。"
        };
        private static readonly string[] FarWitnessEn={
            "The flood scar lies higher than official canal charts; residents rebuilt the southern escape walk themselves.",
            "Records under the old roots say the forest sheltered children through the harshest winds.",
            "Ferrymen scratched the real tide onto a post; the shipping manifest omitted it.",
            "The miners' signed shutdown roll orders evacuation of the injured before restarting the crystal engines.",
            "The evacuation log names refugees and guards who opened the same side gate together.",
            "Mistport's safer course came from sailors' testimony, not a stale customs ledger.",
            "A stone by an unregistered well preserves children's water shares, later buried by the dunes.",
            "Each warming hut's fire is traced in a highland notebook. Bells should lead people home."
        };
        public bool FarLogFound(int room)
        {return room>=0&&room<8&&Has("far_log_"+room);}
        public bool FarDialReady(int room)
        {return room>=0&&room<8&&Has("far_dial_"+room);}
        public bool FarShelterBuilt(int room)
        {return room>=0&&room<8&&Has("far_shelter_"+room);}
        public int FarShelterCount
        {get {int count=0;for(int i=0;i<8;i++)if(FarShelterBuilt(i))count++;return count;}}
        private bool TalkFarSecrets(WorldObject o)
        {
            if(o.kind!="farhidden"&&o.kind!="farlookout")return false;
            int room=save.room;
            if(room<0||room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            bool vista=o.kind=="farlookout";
            string flag=(vista?"far_vista_":"far_secret_")+room;
            bool first=!Has(flag);
            if(first && !vista &&
                (save.coins > 999990 || save.inventory[room%3] > 9997))
            {
                Notify("隐藏补给箱仍在原处。留出9硬币和2份材料的容量后回来。",
                    "Hidden supplies remain here. Free room for 9 coins and 2 materials, then return.");
                return true;
            }
            if(first)
            {
                Flag(flag);sound="reward";
                if(vista)
                {
                    AddXp(16);
                    int cx=(int)o.x/6,cy=(int)o.y/6;
                    int maxX=(Map.rows[0].Length+5)/6,maxY=(Map.rows.Length+5)/6;
                    for(int x=Math.Max(0,cx-2);x<=Math.Min(maxX-1,cx+2);x++)
                        for(int y=Math.Max(0,cy-2);y<=Math.Min(maxY-1,cy+2);y++)
                            if(x*6>=36||y*6>=24)
                                Flag("annex_seen_"+room+"_"+x+"_"+y);
                }
                else
                {
                    CreditCoins(9);
                    CreditInventory(room % 3, 2);
                }
                revision++;
            }
            Dialog(o.name,o.en,vista?
                    "高处的旧观测站能看见其他人曾走过的路。它记录附近五乘五块地形，而不代替你真正进入还没探索的远处区域。"+
                    (first?"\n\n经验 +16，周边地貌已记载。":"\n\n此处地图已经测绘。"):
                    "遗失的救援物资没有被登记进公会名单。守路者后来把它们藏在了第六圈的死角，留给真正需要的行人。"+
                    (first?"\n\n硬币 +9、材料 +2。":"\n\n补给已被取回。"),
                vista?
                    "From this high lookout the old wardens could see forgotten local routes. The nearby 5x5 sectors are charted, but the distant wilderness remains yours to walk."+
                    (first?"\n\n+16 XP; surrounding terrain surveyed.":"\n\nAlready charted."):
                    "These lost supplies never entered the guild's official count. Wardens hid them in the far frontier for travelers in need."+
                    (first?"\n\n+9 coins and 2 materials.":"\n\nSupplies already recovered."),
                C("继续探索","Continue exploring","close"));
            return true;
        }
        public bool FarBridgeOpen(int room){return room>=0&&room<8&&Has("far_bridge_"+room);}
        public int FarBridgeCount
        {get{int n=0;for(int i=0;i<8;i++)if(FarBridgeOpen(i))n++;return n;}}
        private bool TalkFarBridge(WorldObject o)
        {
            if(o.kind!="farbridge")return false;
            int room=save.room;
            if(room<0||room>=8||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            bool repaired=FarBridgeOpen(room);
            Dialog(o.name,o.en,repaired?
                "居民已重新铺好山脊桥面。北部气象站到南部避险棚的近路永久开放；原长路仍可走。":
                "这条居民补给栈桥已断裂。提供木材2与零件1可铺设真正的可通行桥面，缩短长距离绕路。",
                repaired?"The civilian shortcut from the weather log to the outer refuge is permanently walkable. The old detour remains open.":
                    "Spend 2 timber and 1 spare part to rebuild the collapsed civilian bridge, opening physical collision tiles and shortening the supply trail.",
                C(repaired?"检查桥面":"修复 · 木材2、零件1",
                    repaired?"Inspect bridge":"Repair · 2 timber, 1 part",
                    repaired?"close":"far_bridge_repair"),
                C("继续步行","Keep walking","close"));
            return true;
        }
        private bool ChooseFarBridgeRepair()
        {
            int room=save.room;
            if(room<0||room>=8||Dying||practicing||FarBridgeOpen(room))return true;
            WorldObject bridge=null;
            foreach(WorldObject item in Map.objects)if(item.kind=="farbridge"){bridge=item;break;}
            if(bridge==null||(Position-new Point(bridge.x,bridge.y)).Length>2.2f)return true;
            if(!SafeTravel)
            {Notify("敌人太近，无法安全铺桥。","Hostiles are too close to repair the crossing.");return true;}
            if(!Spend(2,0,0,0,1))return true;
            Flag("far_bridge_"+room);AddXp(24);revision++;sound="reward";
            RecordMoment("第 "+(room+1)+" 处平民补给桥重新开放，救援物资可以通过真正的山脊近路运往避险棚。",
                "The civilian bridge in region "+(room+1)+" now physically connects the outer weather trail to the public refuge.");
            if(FarBridgeCount==8&&!Has("far_bridges_all"))
            {Flag("far_bridges_all");CreditSkillPoints(1);revision++;}
            Notify("桥面重新开放！现在可以直接步行通过，经验 +24。",
                "Crossing reopened! The new physical shortcut is walkable. +24 XP.");
            return true;
        }

        // Chapter of civilian oversight unlocked by authentic parcel delivery:
        // the guest registry is revisited, three delivered regions vote at
        // their physical inspection posts, and the records return to the inn.
        // Persistent decisions use only existing SaveData v2 string flags.
        private static readonly int[] CivicSupplyRegions={0,3,5};
        public bool CivicSupplyStarted {get{return Has("civic_supply_started");}}
        public bool CivicSupplyComplete {get{return Has("civic_supply_complete");}}
        public bool CivicSupplyClinic(int room)
        {return room>=0&&room<8&&Has("civic_supply_clinic_"+room);}
        public bool CivicSupplyWatch(int room)
        {return room>=0&&room<8&&Has("civic_supply_watch_"+room);}
        public bool CivicSupplyResolved(int room)
        {return CivicSupplyClinic(room)||CivicSupplyWatch(room);}
        public int CivicSupplyResolvedCount
        {
            get
            {
                int n=0;foreach(int room in CivicSupplyRegions)
                    if(CivicSupplyResolved(room))n++;
                return n;
            }
        }
        private bool CivicSupplySite(int room)
        {return Array.IndexOf(CivicSupplyRegions,room)>=0;}
        // Region-specific, physically completed service emergencies. All
        // in-progress state is transient; only earned outcomes enter v2 flags.
        private int civicResponseRoom=-1,civicResponseType;
        private Point civicResponseLast;
        private float civicResponseElapsed,civicResponseDistance,civicResponseCargo;
        public float CivicResponseIntegrity
        {get{return CivicResponseClinicRun?civicResponseCargo:0;}}
        // Only real post-defense player contact can damage carried medicine;
        // weather effects, dodged attacks and idle timers do not.
        private void DamageCivicResponseMedicine(float damage,string source)
        {
            if(!CivicResponseClinicRun||source=="environment"||damage<=0)return;
            civicResponseCargo=Math.Max(0,civicResponseCargo-
                Math.Max(12,Math.Min(40,damage*1.1f)));
            if(civicResponseCargo<=0)
            {
                ResetCivicResponse();revision++;
                Notify("真正的敌人攻击打碎了急救药箱，请回避险棚重新领取。",
                    "A real enemy strike destroyed the medical kit. Recollect it at the refuge.");
            }
            else if(civicResponseCargo<=40)
                Notify("医疗包受损！谨慎闪避，再把剩余药品亲手送到检查站。",
                    "Medicine damaged! Dodge carefully and deliver the remaining kit to the checkpoint.");
        }
        private bool civicResponseKillA,civicResponseKillB;
        public bool CivicResponseActive
        {get{return civicResponseRoom==save.room&&!Dying;}}
        public bool CivicResponseClinicRun
        {get{return CivicResponseActive&&civicResponseType==1;}}
        public bool CivicResponseWatchRun
        {get{return CivicResponseActive&&civicResponseType==2;}}
        public float CivicResponseTimeLeft
        {get{return CivicResponseActive?Math.Max(0,75-civicResponseElapsed):0;}}
        public bool CivicResponseDone(int room)
        {return CivicSupplySite(room)&&Has("civic_response_done_"+room);}
        public int CivicResponseDoneCount
        {
            get
            {
                int n=0;foreach(int room in CivicSupplyRegions)
                    if(CivicResponseDone(room))n++;
                return n;
            }
        }
        private bool IsCivicResponseEnemy(Enemy e)
        {return e!=null&&e.id>=90200&&e.id<90216;}
        public int CivicResponseEnemiesAlive
        {
            get
            {
                if(!CivicResponseActive)return 0;
                // Rendered HUD + journal can query this every frame.
                // Count directly rather than allocating a filtered List.
                int count=0;
                foreach(Enemy foe in enemies)if(IsCivicResponseEnemy(foe))count++;
                return count;
            }
        }
        private void ResetCivicResponse()
        {
            enemies.RemoveAll(IsCivicResponseEnemy);
            projectiles.RemoveAll(p=>p.civicResponseOwned);
            effects.RemoveAll(e=>e.civicResponseOwned);
            civicResponseRoom=-1;civicResponseType=0;
            civicResponseLast=new Point();
            civicResponseElapsed=civicResponseDistance=civicResponseCargo=0;
            civicResponseKillA=civicResponseKillB=false;
        }
        private void OnCivicResponseEnemyDefeated(Enemy e)
        {
            if(!CivicResponseWatchRun||!IsCivicResponseEnemy(e))return;
            if(e.id==90200+save.room*2)civicResponseKillA=true;
            if(e.id==90201+save.room*2)civicResponseKillB=true;
        }
        private void TickCivicResponse(float dt)
        {
            if(civicResponseRoom<0)return;
            if(civicResponseRoom!=save.room||Dying||practicing)
            {ResetCivicResponse();return;}
            Point current=Position;
            float delta=(current-civicResponseLast).Length;
            if(!Finite(current.x)||!Finite(current.y)||delta>2.4f)
            {
                ResetCivicResponse();revision++;
                Notify("社区服务队已中断；必须亲自沿道路护送或守卫。",
                    "Civilian service interrupted: walk or defend the actual site.");
                return;
            }
            civicResponseElapsed+=Math.Max(0,dt);
            civicResponseDistance+=delta;
            civicResponseLast=current;
            if(civicResponseElapsed>=75)
            {
                ResetCivicResponse();revision++;
                Notify("居民服务行动超时；可以重新从避险棚组织队伍。",
                    "Civilian service timed out; regroup at the real refuge.");
                return;
            }
            if(civicResponseType==2)
            {
                int a=90200+save.room*2,b=a+1;
                bool presentA=enemies.Exists(e=>e.id==a),presentB=enemies.Exists(e=>e.id==b);
                if((!presentA&&!civicResponseKillA)||(!presentB&&!civicResponseKillB))
                {
                    ResetCivicResponse();revision++;
                    Notify("守路岗的袭击者异常离场，必须重新开始真实守卫战。",
                        "Lookout attackers vanished without defeat. Restart a real defense.");
                }
            }
        }
        private void FinishCivicResponse()
        {
            if(!CivicResponseActive||CivicResponseDone(save.room))return;
            int room=save.room;
            ResetCivicResponse();
            Flag("civic_response_done_"+room);
            CreditCoins(24);CreditShards(1);
            AddXp(42);revision++;sound="reward";
            if(CivicResponseDoneCount==3&&!Has("civic_response_all"))
            {Flag("civic_response_all");CreditSkillPoints(1);revision++;}
            RecordMoment("第 "+(room+1)+" 地居民的"+(CivicSupplyClinic(room)?
                "医疗护送":"守路实战")+"已由真正行走与战斗完成，并入公开社区服务清册。",
                "Region "+(room+1)+" completed a real "+(CivicSupplyClinic(room)?
                "medical field delivery":"route watch defense")+
                " as a public, accountable civilian service.");
            Notify("社区服务完成：硬币 +24、碎片 +1、经验 +42。当地服务效能提升！",
                "Community service complete: +24 coins, +1 shard, +42 XP. Local refuge improved!");
        }
        private bool TalkCivicResponse(WorldObject o)
        {
            bool shelter=o.kind=="farshelter",checkpoint=o.kind=="farcheck";
            if(!shelter&&!checkpoint)return false;
            int room=save.room;
            if(!CivicSupplySite(room)||!CivicSupplyComplete||
                !CivicSupplyResolved(room)||!FarShelterBuilt(room)||
                (Position-new Point(o.x,o.y)).Length>2.2f)return false;
            if(checkpoint)
            {
                if(!CivicResponseClinicRun)return false;
                bool valid=civicResponseDistance>=9.5f&&civicResponseElapsed<75&&
                    (Position-civicResponseLast).Length<.65f;
                Dialog(o.name,o.en,valid?
                    "医药真正走过了避险棚与检查站之间的山道。病人的姓名与补给数量都在名单上，不会被写成没有人的数字。":
                    "居民正在等你把医药亲手从南端避险棚送来；不能通过瞬间传送取代真实步行。",
                    valid?"You physically carried medicine from the southern refuge to the checkpoint. The patients are named in the public registry.":
                    "Carry the medicine all the way from the refuge on foot. Teleporting cannot substitute for a real journey.",
                    C(valid?"交接医疗包":"继续沿路护送",
                        valid?"Deliver medical supplies":"Keep walking",
                        valid?"civic_response_deliver":"close"),
                    C("暂时离开","Leave","close"));
                return true;
            }
            if(CivicResponseActive)
            {
                bool cleared=CivicResponseWatchRun&&civicResponseKillA&&
                    civicResponseKillB&&CivicResponseEnemiesAlive==0;
                Dialog(o.name,o.en,cleared?
                    "两名袭击者被真正击败了，夜间守路岗终于可以安全把灯交接给下一班居民。":
                    CivicResponseClinicRun?
                    "药箱已经出发：75秒内步行向北到居民检查站交付。":
                    "守路战正在进行。击败两名真实敌人后回到这里确认守卫。",
                    cleared?"Both authentic attackers were defeated; certify the civilian road watch.":
                    CivicResponseClinicRun?
                    "Medical supplies are in transit. Reach the northern checkpoint within 75 seconds on foot.":
                    "The route defense is active. Defeat both real enemies and return to certify it.",
                    C(cleared?"签署守卫记录":"检查进度",
                        cleared?"Certify defense":"Review progress",
                        cleared?"civic_response_claim":"close"),
                    C("取消这次行动","Abort this attempt","civic_response_abort"));
                return true;
            }
            if(CivicResponseDone(room))
            {
                Dialog(o.name,o.en,"本地居民服务已经真正完成，医疗与守路效能永久提高；不能反复领取委托奖励。",
                    "This region's public service is complete. The real refuge improvement persists; rewards cannot be farmed.",
                    C("继续旅行","Continue","close"));return true;
            }
            Dialog(o.name,o.en,CivicSupplyClinic(room)?
                "医疗点已经建成，但有患者还在北面的检查站等待药箱。请背起医药，75秒内沿现有道路真实走到检查站，不能传送。":
                "守路岗已开放，但桥南有两名袭击者试图抢走夜间物资。请亲自击退他们，再回此地确认守卫。",
                CivicSupplyClinic(room)?
                "The clinic is built, but patients at the northern checkpoint still need medicine. Carry it there on foot within 75 seconds.":
                "The lookout exists, but two raiders threaten the southern supply trail. Defeat both actual attackers, then return to sign the watch ledger.",
                C(CivicSupplyClinic(room)?"背起医药开始护送":"开始居民守卫战",
                    CivicSupplyClinic(room)?"Begin medical delivery":"Begin road defense",
                    "civic_response_start"),
                C("稍后再来","Later","close"));
            return true;
        }
        private bool ChooseCivicResponse(string action)
        {
            if(!action.StartsWith("civic_response_"))return false;
            if(action=="civic_response_ledger")
            {
                if(Dying||practicing||save.room!=9||!CivicSupplyStarted||
                    !CivicSupplyComplete||CivicResponseDoneCount!=3||
                    Has("civic_response_ledger"))
                    return true;
                WorldObject registry=null;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="farrefugerecord"){registry=o;break;}
                if(registry==null||
                    (Position-new Point(registry.x,registry.y)).Length>2.2f)return true;
                Flag("civic_response_ledger");
                CreditCoins(40);CreditShards(2);
                AddXp(55);revision++;sound="reward";
                RecordMoment("三处居民服务真正完成后，炉火旅店公开了有签名的医疗与守路应急日志。八地所有已建好的公共避险棚开始分享基础疗养配方。",
                    "The original guest registry publishes three signed, physically proven clinic and lookout reports. Every rebuilt regional refuge now shares a small safe recuperation service.");
                Notify("公共应急名册已公开：硬币40、碎片2、经验55；八地避险棚共享基础疗养。",
                    "Public response ledger published: +40 coins, +2 shards, +55 XP; all regional refuges gain safe recovery.");
                return true;
            }
            if(Dying||practicing||!CivicSupplySite(save.room)||
                !CivicSupplyComplete||!CivicSupplyResolved(save.room))return true;
            bool start=action=="civic_response_start",
                deliver=action=="civic_response_deliver",
                claim=action=="civic_response_claim",
                abort=action=="civic_response_abort";
            if(!start&&!deliver&&!claim&&!abort)return true;
            WorldObject shelter=null,checkpoint=null;
            foreach(WorldObject o in Map.objects)
            {
                if(o.kind=="farshelter")shelter=o;
                if(o.kind=="farcheck")checkpoint=o;
            }
            if(shelter==null||checkpoint==null)return true;
            bool nearShelter=(Position-new Point(shelter.x,shelter.y)).Length<=2.2f;
            bool nearCheck=(Position-new Point(checkpoint.x,checkpoint.y)).Length<=2.2f;
            if(abort)
            {
                if(CivicResponseActive&&nearShelter)
                {ResetCivicResponse();revision++;Notify("社区行动已经安全取消。","Community run canceled.");}
                return true;
            }
            if(start)
            {
                if(CivicResponseActive||CivicResponseDone(save.room)||!nearShelter)return true;
                if(FarCourierActive||TideSalvageActive)
                {
                    Notify("先完成或取消当前救援包与潮滩打捞任务，再开启居民服务。",
                        "Complete or cancel your parcel or tide salvage before starting civilian service.");
                    return true;
                }
                if(!SafeTravel)return true;
                int type=CivicSupplyClinic(save.room)?1:2;
                if(type==2)
                {
                    Point first=new Point(110.5f,70.5f),
                        second=new Point(114.5f,69.5f);
                    if(!CanStand(first)||!CanStand(second))
                    {
                        Notify("守路岗附近仍有障碍，无法安全组织真实防卫。",
                            "Blocked ground prevents real patrol deployment.");return true;
                    }
                    int id=90200+save.room*2;
                    enemies.Add(CreatePlacedEnemy(id,0,first,46+save.room*5));
                    enemies.Add(CreatePlacedEnemy(id+1,1,second,38+save.room*5));
                    effects.Add(new Effect("danger",first,.65f){
                        radius=.75f,civicResponseOwned=true});
                    effects.Add(new Effect("danger",second,.65f){
                        radius=.75f,civicResponseOwned=true});
                }
                civicResponseRoom=save.room;civicResponseType=type;
                civicResponseLast=Position;civicResponseElapsed=civicResponseDistance=0;
                civicResponseCargo=type==1?100:0;
                civicResponseKillA=civicResponseKillB=false;
                sound="quest";revision++;
                Notify(type==1?"医药护送开始：75秒内亲自走到北侧居民检查站！":
                    "居民守卫战开始：击败真实的近战劫匪与弓箭手！",
                    type==1?"Medical delivery started: walk to the northern checkpoint within 75s!":
                    "Real road defense started: defeat both the raider and archer!");
                return true;
            }
            if(deliver&&CivicResponseClinicRun&&nearCheck&&
                civicResponseDistance>=9.5f&&civicResponseElapsed<75)
            {FinishCivicResponse();return true;}
            if(claim&&CivicResponseWatchRun&&nearShelter&&
                civicResponseKillA&&civicResponseKillB&&
                CivicResponseEnemiesAlive==0)
            {FinishCivicResponse();return true;}
            return true;
        }

        private bool TalkCivicSupply(WorldObject o)
        {
            if(o.kind=="farrefugerecord" && save.room==9 &&
                Has("far_refuge_guest_record")&&FarCourierDeliveredCount>0)
            {
                if((Position-new Point(o.x,o.y)).Length>2.2f)return true;
                if(!CivicSupplyStarted)
                    Dialog(o.name,o.en,
                        "住客名单末尾多了三封写给普通旅人的求援信：引水渠的医药、赤铁矿场的夜间守望、雾港的临时床位。\n\n新的救援包已经真正抵达，但谁来维护这些地方，还没有答案。请在三地的居民检查台收集选择，最后回旅店公开账目。",
                        "Three letters were added to the guest registry: medicine for the canal, night watches for Ironreach, and emergency beds at Mistport.\n\nThe first parcel truly arrived, but each community must decide how its refuge is maintained. Ask the residents at three real checkpoints, then publish their decisions here.",
                        C("接受三地居民审计","Begin the civilian inquest","civic_supply_begin"),
                        C("稍后再谈","Return later","close"));
                else
                {
                    if(CivicSupplyComplete&&CivicResponseDoneCount==3&&
                        !Has("civic_response_ledger"))
                    {
                        Dialog(o.name,o.en,
                            "三封原始求援信终于有了真实结尾：有人背着药箱走完路，也有人在夜间亲自守住了桥。居民把每一份交接、每个击退的袭击者和志愿者的姓名写在同一张新账上。\n\n公开应急名册后，八地已建的公共避险棚都能分享一部分基础疗养经验。",
                            "All three original letters now have real endings: medicine carried by hand, roads defended under attack, and named local volunteers. The register records each real journey and verified victory.\n\nPublish the response ledger so every rebuilt public refuge can share basic recuperation.",
                            C("签发公共应急名册","Publish response ledger",
                                "civic_response_ledger"),
                            C("以后再来","Later","close"));
                        return true;
                    }
                    bool ready=CivicSupplyResolvedCount==3;
                    Dialog(o.name,o.en,
                        "三地居民审计已记录 "+CivicSupplyResolvedCount+"/3 份决定。"+(CivicSupplyComplete?
                            "\n\n公开账目已经送到各地，每处避险棚的维持方式都由实地决定。":
                            ready?"\n\n证词齐备。现在可以签发公开审计公报。":
                            "\n\n回到已交付救援包的地区检查站，选择医疗补给或道路守望。"),
                        "The inquest records "+CivicSupplyResolvedCount+"/3 local decisions."+
                            (CivicSupplyComplete?
                            "\n\nThe published accounts preserve each community's chosen refuge service.":
                            ready?"\n\nAll decisions are recorded. Publish the public inquest report.":
                            "\n\nVisit each delivered region's checkpoint to choose medical care or route watching."),
                        C(!CivicSupplyComplete&&ready?"公开三地救援审计":"查看现有记录",
                            !CivicSupplyComplete&&ready?"Publish inquest":"Review decisions",
                            !CivicSupplyComplete&&ready?"civic_supply_publish":"close"),
                        C("继续行路","Continue","close"));
                }
                return true;
            }
            if(o.kind!="farcheck"||!CivicSupplyStarted||!CivicSupplySite(save.room)||
                FarCourierActive)return false;
            if((Position-new Point(o.x,o.y)).Length>2.2f)return true;
            int room=save.room;
            if(!FarCourierDelivered(room))
            {
                Dialog(o.name,o.en,
                    "只有真正收到穿桥救援包后，居民才愿意讨论长期维护。请先把本地补给亲手送到避险棚。",
                    "Residents will only plan long-term services after receiving a real bridge delivery. First bring this region's parcel to its refuge.",
                    C("继续运送","Keep delivering","close"));
                return true;
            }
            if(CivicSupplyResolved(room))
            {
                Dialog(o.name,o.en,
                    CivicSupplyClinic(room)?
                        "大家选择先保障受伤者。南边的公共避险棚已经安排志愿医护，安全时可持续恢复生命。":
                        "大家选择先守护归途。南边的公共避险棚已经安排路标守望，安全时可持续恢复耐力。",
                    CivicSupplyClinic(room)?
                        "Local residents chose medical care. Volunteers at the southern refuge restore health while the area is safe.":
                        "Local residents chose route watching. Scouts at the southern refuge restore energy while the area is safe.",
                    C("查看签名","Review signatures","close"));
                return true;
            }
            Dialog(o.name,o.en,
                room==0?"水道边的老人为药草与干净床铺投票，守桥人却担心下一波洪水时无人巡逻。这里没有只靠英雄解决的问题。\n\n请替居民投入一项真实的长期服务。":
                room==3?"矿工说负伤者需要治疗，夜班的运货队却仍害怕有人在新桥背后伏击。资源只能先修一处。":
                    "雾港的船医想在避险棚开诊，守望者则希望在雾中建立持续的路灯巡查。由居民留下的名单决定，不能让公会悄悄代选。",
                room==0?"Canal elders ask for medicines and clean beds; bridgekeepers warn the next flood may arrive without a night patrol. Neither need is a simple heroic victory.\n\nFund one enduring civic service.":
                room==3?"Miners need treatment for the injured, but the night convoy fears ambush behind the rebuilt crossing. Only one service can be equipped first.":
                    "Mistport's ship medics propose a field clinic; route watchers want dependable fog patrols. These choices belong to people on the manifest, not a distant guild.",
                C("建立医疗点 · 药草2 零件1",
                    "Fund clinic · 2 herbs, 1 part","civic_supply_clinic"),
                C("设立守路岗 · 木材2 零件1",
                    "Fund lookout · 2 timber, 1 part","civic_supply_watch"),
                C("听完再决定","Decide later","close"));
            return true;
        }
        private bool ChooseCivicSupply(string action)
        {
            if(!action.StartsWith("civic_supply_"))return false;
            if(Dying||practicing)return true;
            if(action=="civic_supply_begin"||action=="civic_supply_publish")
            {
                if(save.room!=9||!Has("far_refuge_guest_record")||
                    FarCourierDeliveredCount==0)return true;
                WorldObject registry=null;
                foreach(WorldObject o in Map.objects)if(o.kind=="farrefugerecord")
                    {registry=o;break;}
                if(registry==null||(Position-new Point(registry.x,registry.y)).Length>2.2f)
                    return true;
                if(action=="civic_supply_begin")
                {
                    if(CivicSupplyStarted)return true;
                    Flag("civic_supply_started");
                    TrackQuest("civic_supply");sound="quest";revision++;
                    RecordMoment("炉火旅店里的三封民间来信开启救援物资审计：为每地居民留下一项真正可用的长久服务。",
                        "Three letters at Hearth Inn begin an independent audit of lasting services in the regions actually receiving relief.");
                    Notify("三地居民审计开始：走访引水渠、矿场和雾港的实地检查台。",
                        "Civilian inquest begun: visit the physical checkpoints in Canal, Ironreach and Mistport.");
                }
                else
                {
                    if(!CivicSupplyStarted||CivicSupplyComplete||
                        CivicSupplyResolvedCount!=3)return true;
                    Flag("civic_supply_complete");
                    CreditCoins(48);CreditShards(2);CreditSkillPoints(1);
                    AddXp(70);sound="reward";revision++;
                    RecordMoment("三地签署各自的医疗与守路选择。炉火旅店公开了每一笔木材、药草和零件去向，公共避险棚开始真正发挥作用。",
                        "Three communities sign their own clinic or lookout allocations. Hearth Inn makes every timber, herb and part public, and the refuges serve travelers.");
                    Notify("居民救援审计完成：硬币 +48、碎片 +2、技能点 +1、经验 +70。",
                        "Civilian inquest complete: +48 coins, +2 shards, +1 talent, +70 XP.");
                }
                return true;
            }
            if(action!="civic_supply_clinic"&&action!="civic_supply_watch")return true;
            int room=save.room;
            if(!CivicSupplyStarted||CivicSupplyComplete||!CivicSupplySite(room)||
                !FarCourierDelivered(room)||!FarShelterBuilt(room)||FarCourierActive)return true;
            WorldObject checkpoint=null;
            foreach(WorldObject o in Map.objects)if(o.kind=="farcheck"){checkpoint=o;break;}
            if(checkpoint==null||(Position-new Point(checkpoint.x,checkpoint.y)).Length>2.2f||
                CivicSupplyResolved(room))return true;
            if(!SafeTravel)
            {
                Notify("先清除居民检查站旁的敌人，才能安全设立服务。",
                    "Clear hostiles by the civilian checkpoint before funding a permanent service.");
                return true;
            }
            bool clinic=action=="civic_supply_clinic";
            if(!Spend(clinic?0:2,0,clinic?2:0,0,1))return true;
            Flag(action+"_"+room);AddXp(22);sound="reward";revision++;
            RecordMoment("第 "+(room+1)+" 地居民选择优先"+(clinic?"医疗照护":"守路巡查")+
                "，由亲自运送过补给包的社区签字，而不是远处的命令。",
                "Region "+(room+1)+" votes for "+(clinic?"medical care":"route watching")+
                " after receiving the physical relief parcel, rather than a distant order.");
            Notify(clinic?"医疗服务已设立：避险棚安全时持续恢复生命。":
                "守路岗已设立：避险棚安全时持续恢复耐力。",
                clinic?"Clinic funded: safe refuge now restores health over time.":
                "Lookout funded: safe refuge now restores energy over time.");
            return true;
        }

        private bool TalkFarStation(WorldObject o)
        {
            if(TalkCivicResponse(o))return true;
            if(TalkCivicSupply(o))return true;
            if(TalkFarCourier(o))return true;
            if(TalkFarBridge(o))return true;
            if(TalkFarSecrets(o))return true;
            if(o.kind=="faratlasdesk"&&save.room==9&&
                (Position-new Point(o.x,o.y)).Length<=2.2f&&
                Has("far_refuge_atlas")&&FarCourierDeliveredCount==8&&
                !Has("far_courier_charter"))
            {
                Dialog(o.name,o.en,
                    "八地补给包全部由真人越过民用桥送达，居民封条与炉火旅店的八份回执终于一一对应。\n\n这些道路不应只让公会得利。发布公开补给公约后，所有已修建的公共避险棚都将配备更好的草药与御寒用品。",
                    "Every relief parcel reached the shelters by real foot travel, bearing a community inspector's seal.\n\nPublish the open supply charter so every rebuilt refuge, not just the guild, receives better medical and winter supplies.",
                    C("公布八地公共补给公约","Publish the eight-region supply charter","far_courier_charter"),
                    C("稍后再来","Later","close"));
                return true;
            }
            if(o.kind=="faratlasdesk" || o.kind=="farrefugerecord")
            {
                if(save.room!=9||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
                if(o.kind=="farrefugerecord")
                {
                    bool first=!Has("far_refuge_guest_record");
                    if(first){Flag("far_refuge_guest_record");AddXp(10);sound="reward";}
                    Dialog(o.name,o.en,
                        "这份旅店住客名单记录了未被计入官方救援数字的老人、矿工和孩子。新的避险棚应为所有走到这里的人敞开。"+
                        (first?"\n\n首次记录：经验 +10。":""),
                        "This guest list remembers elders, miners and children missing from official relief totals. Every new refuge must remain open to people who actually reach it."+
                        (first?"\n\nFirst reading: +10 XP.":""),
                        C("记下姓名","Remember their names","close"));
                    return true;
                }
                bool ready=FarShelterCount==8,complete=Has("far_refuge_atlas");
                Dialog(o.name,o.en,
                    "炉火旅店的新制图室收集八个地区的气象记录与避险棚实地图。只有全部实地修复后，才能把开放的公共路线印给每一个旅人。"+
                    "\n\n已建成公共避险棚："+FarShelterCount+"/8。"+
                    (complete?"\n\n总图已经公开，旧名字不会再次消失。":ready?
                        "\n\n真实站点全部验证完毕，可以签发公共总图。":"\n\n继续前往未完成的远地气象站。"),
                    "The Hearth Inn's new cartography wing collects weather logs and public refuges from all eight regions. Only verified built shelters can enter the open atlas."+
                    "\n\nRebuilt shelters: "+FarShelterCount+"/8."+
                    (complete?"\n\nThe public atlas is already filed.":ready?
                        "\n\nAll stations verified. The civic chart may now be published.":"\n\nExplore the remaining weather shelters."),
                    C(complete?"重读公共总图":"签发公开避险总图",
                        complete?"Reread the atlas":"Publish public refuge atlas","far_publish"),
                    C("继续巡路","Keep exploring","close"));
                return true;
            }
            if(o.kind!="farledger"&&o.kind!="farcalibrate"&&o.kind!="farshelter")return false;
            int room=save.room;
            if(room<0||room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            if(o.kind=="farledger")
            {
                bool first=!FarLogFound(room);
                if(first){Flag("far_log_"+room);AddXp(10);sound="reward";}
                Dialog(o.name,o.en,FarWitnessZh[room]+
                    "\n\n此地原来的风向校准符号是「"+DialSignsZh[FarDialAnswers[room]]+
                    "」。去南侧真实校准盘输入它，才可能找到共同避险棚。"+
                    (first?"\n\n首次实地记录：经验 +10。":""),
                    FarWitnessEn[room]+
                    "\n\nThe local calibration glyph is "+DialSignsEn[FarDialAnswers[room]]+
                    ". Enter it at the real southern dial before opening the public shelter."+
                    (first?"\n\nFirst field record: +10 XP.":""),
                    C("记下读数","Record the reading","close"));
                return true;
            }
            if(o.kind=="farcalibrate")
            {
                if(!FarLogFound(room))
                {
                    Dialog(o.name,o.en,"校验盘被风雨洗掉了说明。先沿远东岔道找到原来的气象手记。",
                        "The field dial has lost its instructions. First read the original log on the remote eastern trail.",
                        C("返回调查","Search for the log","close"));return true;
                }
                if(FarDialReady(room))
                {
                    Dialog(o.name,o.en,"气象盘已经校准，沿南部环路前往最后的公共避险棚。",
                        "The weather dial is calibrated. Follow the southern circuit to the public shelter.",
                        C("继续行路","Continue","close"));return true;
                }
                Dialog(o.name,o.en,"手记上的三种古老符号分别表示琥珀、潮蓝与苔绿。选择与原始记录一致的一项。",
                    "The old log describes three glyphs: Amber, Tideblue and Mossgreen. Match its field reading.",
                    C("琥珀刻痕","Amber glyph","far_dial_0"),
                    C("潮蓝刻痕","Tideblue glyph","far_dial_1"),
                    C("苔绿刻痕","Mossgreen glyph","far_dial_2"),
                    C("稍后再来","Return later","close"));
                return true;
            }
            if(!FarDialReady(room))
            {
                Dialog(o.name,o.en,"避险棚的炉火尚未恢复。先找到天气记录，并在南侧校验盘恢复信号。",
                    "This shelter is still unlit. Recover the survey and calibrate the southern weather dial first.",
                    C("继续查找","Continue the survey","close"));return true;
            }
            if(!FarShelterBuilt(room))
            {
                Dialog(o.name,o.en,"这里有可供普通旅人歇脚的旧棚，修复需要木材 ×1 和零件 ×1。"+
                    "\n\n修复后可消耗木材再次生火，治疗、补充耐力并减轻寒冷。",
                    "This unfinished civilian refuge needs one timber and one spare part."+
                    "\n\nOnce built, a timber-fueled rest restores health, energy and warmth.",
                    C("修复避险棚 · 木材1 零件1","Restore refuge · 1 timber 1 part","far_build"),
                    C("以后再来","Return later","close"));
                return true;
            }
            Dialog(o.name,o.en,FarWitnessZh[room]+
                (Has("far_courier_charter")?
                    "\n\n公开补给公约已生效。消耗木材1休息：生命 +55、耐力全满、寒冷 -40。":
                    "\n\n公共避险棚已经建好。消耗木材1休息：生命 +35、耐力全满、寒冷 -25。")+
                (CivicSupplyClinic(room)?
                    "\n\n【居民医疗点】安全时持续疗伤；添柴额外恢复生命 +15。":
                 CivicSupplyWatch(room)?
                    "\n\n【居民守路岗】安全时持续恢复耐力，无需消耗木材。":"")+
                (CivicConduitComplete?
                    LastDirectiveChoice==1?
                        "\n\n【第十一章公共电网】真实调谐的线路正在安全时额外恢复耐力。":
                        "\n\n【第十一章公共电网】真实调谐的线路正在安全时额外恢复生命。":""),
                FarWitnessEn[room]+
                (Has("far_courier_charter")?
                    "\n\nPublic charter: 1 timber restores +55 HP, full stamina, -40 cold.":
                    "\n\nShared refuge: 1 timber restores +35 HP, full stamina, -25 cold.")+
                (CivicSupplyClinic(room)?
                    "\n\n[Resident clinic] Heal when safe; paid rest adds 15 HP.":
                 CivicSupplyWatch(room)?
                    "\n\n[Resident lookout] Regenerate energy when safe without timber.":"")+
                (CivicConduitComplete?
                    LastDirectiveChoice==1?
                        "\n\n[Chapter Eleven public grid] Physically tuned circuits add safe stamina recovery here.":
                        "\n\n[Chapter Eleven public grid] Physically tuned circuits add safe medical recovery here.":""),
                C("添柴休息 · 木材 ×1","Rest · 1 timber","far_rest"),
                C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseFarStation(string action)
        {
            if(ChooseCivicResponse(action))return true;
            if(ChooseCivicSupply(action))return true;
            if(action=="far_courier_charter")
            {
                if(save.room!=9||Dying||practicing||Has("far_courier_charter")||
                    !Has("far_refuge_atlas")||FarCourierDeliveredCount!=8)return true;
                WorldObject atlas=null;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="faratlasdesk"){atlas=o;break;}
                if(atlas==null||(Position-new Point(atlas.x,atlas.y)).Length>2.2f)return true;
                Flag("far_courier_charter");CreditCoins(65);
                AddXp(75);revision++;sound="reward";
                RecordMoment("八地居民终于把实物交接、桥梁维护与药材分配写入公开补给公约，避险棚能提供更好的救治。",
                    "The eight communities publish an open charter matching physical deliveries with bridge stewardship and better refuge care.");
                Notify("公共补给公约发布：硬币 +65、经验 +75，所有避险棚医疗与御寒提升。",
                    "Supply charter published: +65 coins, +75 XP. All refuges now heal and warm better.");
                return true;
            }
            if(ChooseFarCourier(action))return true;
            if(action=="far_bridge_repair")return ChooseFarBridgeRepair();
            if(action=="far_publish")
            {
                if(save.room!=9||Dying||practicing||Has("far_refuge_atlas"))return true;
                WorldObject desk=null;
                foreach(WorldObject item in Map.objects)
                    if(item.kind=="faratlasdesk"){desk=item;break;}
                if(desk==null||(Position-new Point(desk.x,desk.y)).Length>2.2f)return true;
                if(FarShelterCount!=8)
                {
                    Notify("仍有避险棚没有被实地修复。","Some remote shelters have yet to be rebuilt.");
                    return true;
                }
                Flag("far_refuge_atlas");CreditCoins(80);CreditShards(3);
                AddXp(50);revision++;sound="reward";
                RecordMoment("八处公共避险棚的路线已经在炉火旅店发布，连没有登记的住客也能使用。",
                    "The shared weather atlas now charts eight physical civilian refuges, including for unregistered travelers.");
                Notify("公共避险总图发布：硬币 +80、碎片 +3、经验 +50。",
                    "Public refuge atlas published: +80 coins, +3 shards, +50 XP.");
                return true;
            }
            if(!action.StartsWith("far_dial_")&&action!="far_build"&&action!="far_rest")
                return false;
            int room=save.room;
            if(room<0||room>7||Dying||practicing)return true;
            string required=action.StartsWith("far_dial_")?"farcalibrate":"farshelter";
            WorldObject station=null;
            foreach(WorldObject o in Map.objects)
                if(o.kind==required){station=o;break;}
            if(station==null||(Position-new Point(station.x,station.y)).Length>2.2f)return true;
            if(action.StartsWith("far_dial_"))
            {
                if(!FarLogFound(room)||FarDialReady(room))return true;
                int choice=-1;
                if(!int.TryParse(action.Substring("far_dial_".Length),out choice))return true;
                if(choice!=FarDialAnswers[room])
                {
                    Notify("刻度不匹配；再次查阅原始记录。","The glyph does not match the original field log.");
                    return true;
                }
                Flag("far_dial_"+room);AddXp(15);revision++;sound="reward";
                Notify("气象盘校准完成，新的避险路段已经确认！",
                    "Weather dial calibrated. The last refuge route is now verified.");
                return true;
            }
            if(!FarDialReady(room))return true;
            if(!SafeTravel)
            {
                Notify("敌人正在追击，无法安全修复或生火。","Hostiles are too close to safely build or rest.");
                return true;
            }
            if(action=="far_build")
            {
                if(FarShelterBuilt(room)||!Spend(1,0,0,0,1))return true;
                Flag("far_shelter_"+room);CreditCoins(22);CreditShards(1);
                AddXp(35);revision++;sound="reward";
                RecordMoment("居民共同修复第 "+(room+1)+" 处远端避险棚，留下正确的气象符号与不遗漏普通人的道路。",
                    "The region's remote public refuge is rebuilt with a verified weather sign and an open civilian path.");
                if(FarShelterCount==8&&!Has("far_all_restored"))
                {Flag("far_all_restored");CreditSkillPoints(1);}
                Notify("避险棚开放：硬币 +22、碎片 +1、经验 +35。",
                    "Refuge restored: +22 coins, +1 shard, +35 XP.");
                return true;
            }
            if(!FarShelterBuilt(room)||!Spend(1,0,0,0,0))return true;
            save.health=Clamp(save.health+(Has("far_courier_charter")?55:35)+
                (CivicSupplyClinic(room)?15:0),0,save.maxHealth);
            save.energy=100;
            save.cold=Math.Max(0,save.cold-(Has("far_courier_charter")?40:25));
            revision++;sound="reward";
            int healed=(Has("far_courier_charter")?55:35)+(CivicSupplyClinic(room)?15:0);
            Notify("避险棚休息：生命 +"+healed+"，耐力全满，寒冷 -"+
                (Has("far_courier_charter")?40:25)+"。",
                "Refuge rest: +"+healed+" HP, full energy, -"+
                (Has("far_courier_charter")?40:25)+" cold.");
            return true;
        }
    }
}
