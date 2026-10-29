using System;
using System.Collections.Generic;
namespace Starfall
{
    // Optional engineering aftermath. Eight regions have different physical
    // inspection orders, live danger and weather pressure. Each route walks
    // two original repaired stations then returns a receipt to the first.
    // All actors and timers are transient; first-clear records are V2 flags.
    public sealed partial class GameCore
    {
        private static readonly int[,] vigilOrder={
            {2,1},{1,2},{2,1},{2,1},
            {1,2},{2,1},{1,2},{2,1}
        };
        private static readonly string[] vigilZh={
            "洪水回卷：沿下游阀与旧街配给盘查明闸门泄压，别让低街再被淹没。",
            "捕猎队又在林中设套。沿避猎根桥走到陷阱架，再去枝标核对回路。",
            "涨潮卷走了临时渡船的绳头。先看高水位缆栓，再到低岸收紧缆索。",
            "不明武装闯入停产矿区。先检查旧炉减压表，再确认矿工撤离门。",
            "堡垒守卫拦住了担架队。确认下坡疏散路，再检查民用护墙灯。",
            "雾港灯标被重新调乱。先去后岸灯，再到礁石加固锚点。",
            "热风里有人追着假高塔迷路。先核实绿洲井水，再校正山影。",
            "雪路上的行人失去联系。先找低钟绳，再为避雪火盆添煤。"
        };
        private static readonly string[] vigilEn={
            "A flood returns. Inspect the downstream overflow, then the street ration dial before the low road floods again.",
            "Hunters laid fresh snares. Walk to the snare frame and branch marker; defeat real pursuers on the route.",
            "A high tide took the ferry line. Check the high mooring, then recover tension at the lower-bank winch.",
            "Armed saboteurs reached the silent mine. Check the furnace gauge, then the workers' evacuation gate.",
            "Citadel soldiers stopped the stretcher convoy. Check the escape ramp and then the civilian wall lantern.",
            "Fog has shifted the lamps again. Inspect the rear beacon, then secure the reef anchor.",
            "Travelers chased a mirage into the hot dunes. Measure actual oasis water, then align the mountain shadow.",
            "A snowbound group has gone silent. Find the sheltered low bell, then renew the warming basket."
        };
        private int vigilRoom=-1,vigilStage,vigilIntegrity;
        private float vigilSeconds;
        private bool vigilRush;
        public bool RegionalVigilRushed {get{return RegionalVigilActive&&vigilRush;}}
        private MapData vigilMap;
        private SaveData vigilSave;
        private WorldObject vigilOrigin;
        private readonly List<Enemy> vigilHostiles=new List<Enemy>();
        private readonly HashSet<Enemy> vigilKills=new HashSet<Enemy>();
        public bool RegionalVigilActive
        {
            get{return vigilStage>0&&vigilRoom==save.room&&
                Object.ReferenceEquals(vigilMap,Map)&&
                Object.ReferenceEquals(vigilSave,save)&&
                vigilOrigin!=null&&SourceBelongsToActiveMap(vigilOrigin);}
        }
        public int RegionalVigilStage {get{return RegionalVigilActive?vigilStage:0;}}
        public float RegionalVigilSeconds {get{return RegionalVigilActive?vigilSeconds:0f;}}
        public int RegionalVigilIntegrity {get{return RegionalVigilActive?vigilIntegrity:0;}}
        public int RegionalVigilEnemyCount
        {get{int n=0;if(RegionalVigilActive)foreach(Enemy e in vigilHostiles)
            if(enemies.Contains(e)&&!vigilKills.Contains(e))n++;return n;}}
        public int RegionalVigilWins
        {get{int n=0;for(int r=0;r<8;r++)if(Has("regional_vigil_done_"+r))n++;return n;}}
        public bool RegionalVigilDone(int room)
        {return room>=0&&room<8&&Has("regional_vigil_done_"+room);}
        public Point RegionalVigilTarget
        {
            get
            {
                int station=RegionalVigilNextStation;
                if(station<0)return new Point();
                foreach(WorldObject o in Map.objects)
                    if(o!=null&&o.id=="regional_machine_"+save.room+"_"+station)
                        return new Point(o.x,o.y);
                return new Point();
            }
        }
        public int RegionalVigilNextStation
        {
            get{return !RegionalVigilActive?-1:vigilStage==3?0:
                vigilOrder[save.room,vigilStage-1];}
        }
        private bool IsRegionalVigilEnemy(Enemy e)
        {return e!=null&&vigilStage>0&&vigilHostiles.Contains(e);}
        private void OnRegionalVigilEnemyDefeated(Enemy e)
        {if(IsRegionalVigilEnemy(e))vigilKills.Add(e);}
        private void ResetRegionalVigil()
        {
            if(vigilStage>0)
            {
                projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                    vigilHostiles.Contains(p.hostileShooter));
                for(int i=enemies.Count-1;i>=0;i--)
                    if(vigilHostiles.Contains(enemies[i]))
                    {RemoveEnemyPendingWarning(enemies[i]);enemies.RemoveAt(i);}
            }
            vigilRoom=-1;vigilStage=0;vigilIntegrity=0;
            vigilSeconds=0f;vigilRush=false;
            vigilMap=null;vigilSave=null;vigilOrigin=null;
            vigilHostiles.Clear();vigilKills.Clear();
        }
        private bool SpawnRegionalVigilRaiders(WorldObject remote)
        {
            Point center=new Point(remote.x,remote.y);
            Point[] offsets={new Point(-5,0),new Point(5,0),
                new Point(0,-5),new Point(0,5),new Point(-4,4),
                new Point(4,-4),new Point(-7,2),new Point(7,-2),
                new Point(3,6),new Point(-3,-6),new Point(6,3),
                new Point(-6,-3),new Point(0,8),new Point(8,0)};
            var pads=new List<Point>();
            foreach(Point off in offsets)
            {
                Point p=center+off;
                if(!CanStand(p)||(p-Position).Length<2f||
                    (save.companion>=0&&(p-companionPosition).Length<2f)||
                    enemies.Exists(e=>e!=null&&e.hp>0f&&
                        (e.Position-p).Length<2f)||
                    pads.Exists(q=>(q-p).Length<2.8f)||
                    !ActorHasLocalRoute(p,center,16))continue;
                pads.Add(p);if(pads.Count==2)break;
            }
            if(pads.Count!=2)
            {
                Notify("工程巡护的远端警戒线没有真实可用的进路，清理危险后再试。",
                    "No genuine raider approach lanes near the far device. Clear the route and retry.");
                return false;
            }
            for(int i=0;i<2;i++)
            {
                int kind=vigilRoom==1?i:vigilRoom==3?(i==0?2:1):2;
                Enemy e=CreatePlacedEnemy(++enemyId,kind,pads[i],48+vigilRoom*7);
                vigilHostiles.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",pads[i],.6f){variant=kind});
            }
            return true;
        }
        private bool RegionalVigilRaidersDefeated()
        {
            if(vigilHostiles.Count==0)return true;
            foreach(Enemy e in vigilHostiles)
                if(!vigilKills.Contains(e)||enemies.Contains(e))return false;
            return true;
        }
        private void TickRegionalVigil(float dt)
        {
            if(vigilStage==0)return;
            if(!RegionalVigilActive||Dying){ResetRegionalVigil();return;}
            if(dt<=0f)return;
            vigilSeconds=Math.Max(0,vigilSeconds-dt);
            if(vigilSeconds<=0f)
            {
                ResetRegionalVigil();revision++;
                Notify("公共工程巡护超时。可以从原装置重新组织出发。",
                    "Civilian engineering watch expired. Return to the original device to retry.");
                return;
            }
            foreach(Enemy e in vigilHostiles)
                if(!enemies.Contains(e)&&!vigilKills.Contains(e))
                {
                    ResetRegionalVigil();revision++;
                    Notify("巡护袭击者未经真正战斗击倒，无法取得居民的安全回执。",
                        "An escort raider vanished without a real defeat. No signed safety record.");
                    return;
                }
            // Different climate burdens are assessed only in genuine world
            // time, and only in the four outdoor noncombat crisis districts.
            if(vigilRoom==0||vigilRoom==2||vigilRoom==5)
                save.energy=Math.Max(0f,save.energy-
                    dt*(vigilRoom==2?.42f:vigilRoom==5?.30f:.24f));
            if(vigilRoom==6)
            {
                save.heat=Clamp(save.heat+dt*.50f,0,100);
                save.energy=Math.Max(0f,save.energy-dt*.42f);
            }
            if(vigilRoom==7)
            {
                save.cold=Clamp(save.cold+dt*.55f,0,100);
                save.energy=Math.Max(0f,save.energy-dt*.28f);
            }
        }
        private void DamageRegionalVigilCargo(float actualDamage)
        {
            if(!RegionalVigilActive||!Finite(actualDamage)||actualDamage<=0f)
                return;
            vigilIntegrity--;
            if(vigilIntegrity<=0)
            {
                ResetRegionalVigil();revision++;
                Notify(vigilRush?
                    "紧急快线物资受损两次，巡护失败。可从起点重新选择方案。":
                    "工程应急物资受损三次，巡护失败。请从源站重试。",
                    vigilRush?
                    "Two real hits destroyed the urgent dispatch. Retry either route from the origin.":
                    "Three actual combat hits destroyed the engineering packet. Restart at the origin.");
            }
            else
            {
                revision++;
                Notify("工程物资被击中，剩余完整度 "+vigilIntegrity+
                    (vigilRush?"/2":"/3")+"。",
                    "Engineering packet damaged: "+vigilIntegrity+
                    (vigilRush?"/2":"/3")+" integrity.");
            }
        }
        // Add a contextual option to the EXISTING device dialogue. The action
        // handler validates that identical, reachable source again.
        private Choice RegionalVigilChoice(WorldObject site)
        {
            int station=RegionalMachineIndex(site),room=save.room;
            if(station<0||!RegionalMachineDone(room))return
                C("继续查看原装置","Inspect the machinery","close");
            if(RegionalVigilDone(room))return
                C("安全回执已登记","Safety receipt signed","close");
            if(RegionalVigilActive)
            {
                if(RegionalVigilNextStation==station)
                    return C(vigilStage==3?"送回巡护签收单":"处理当前应急故障",
                        vigilStage==3?"File the signed watch receipt":
                        "Stabilize the emergency station",
                        vigilStage==3?"regional_vigil_report":"regional_vigil_service");
                if(station==0)
                    return C("取消工程巡护","Withdraw engineering watch",
                        "regional_vigil_abort");
                return C("前往真正的目标装置","Walk to the active destination","close");
            }
            return station==0?
                C("启动实地工程应急巡护","Begin live engineering emergency watch",
                  "regional_vigil_begin"):
                C("到起点装置接取工程巡护","Begin watch at the first station","close");
        }
        private Choice RegionalVigilRushChoice(WorldObject site)
        {
            int station=RegionalMachineIndex(site),room=save.room;
            if(station!=0||!RegionalMachineDone(room)||
                RegionalVigilDone(room)||RegionalVigilActive)
                return C("继续维护巡路设备","Keep checking the devices","close");
            return C("抢修快线 · 240秒 / 2次受伤失败 / 奖励更高",
                "Urgent response · 240s / 2 hits / higher reward",
                "regional_vigil_rush");
        }
        private bool ChooseRegionalVigil(string action)
        {
            if(action!="regional_vigil_begin"&&
                action!="regional_vigil_rush"&&
                action!="regional_vigil_service"&&
                action!="regional_vigil_report"&&action!="regional_vigil_abort")
                return false;
            WorldObject site=pendingDialogueSource;
            int room=save.room,station=RegionalMachineIndex(site);
            if(room<0||room>=8||station<0||mode!="world"||
                Dying||practicing||pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(site)||!HasPhysicalInteractionReach(site)||
                !CanStand(Position)||!RegionalMachineDone(room)||
                RegionalVigilDone(room))return true;
            if(action=="regional_vigil_begin"||action=="regional_vigil_rush")
            {
                if(station!=0||RegionalVigilActive||
                    FieldWorkPreventsRest||!SafeTravel||racing||
                    BlockTravelForProtectedFieldMission(true))return true;
                WorldObject target=null;
                foreach(WorldObject o in Map.objects)
                    if(o!=null&&o.kind=="regionalMachine"&&
                        o.id=="regional_machine_"+room+"_"+vigilOrder[room,0])
                    {target=o;break;}
                if(target==null||!CanStand(new Point(target.x,target.y))||
                    (Position-new Point(target.x,target.y)).Length<18f)
                    return true;
                vigilRoom=room;vigilMap=Map;vigilSave=save;
                vigilOrigin=site;vigilStage=1;
                vigilRush=action=="regional_vigil_rush";
                vigilSeconds=vigilRush?240f:360f;
                vigilIntegrity=vigilRush?2:3;
                if((room==1||room==3||room==4)&&
                    !SpawnRegionalVigilRaiders(target))
                {ResetRegionalVigil();return true;}
                revision++;sound="quest";
                Notify(vigilZh[room]+(vigilRush?
                    " 你选择抢修快线：四分钟内走完两站并返回，两次真实受伤会导致失败。":
                    " 你选择稳妥巡护：六分钟内走完两站并返回，三次真实受伤会导致失败。"),
                    vigilEn[room]+(vigilRush?
                    " Urgent response: four minutes, two real hits destroy the packet. A greater genuine reward awaits.":
                    " Safe response: six minutes, three real hits destroy the packet."));
                return true;
            }
            if(!RegionalVigilActive||room!=vigilRoom)return true;
            if(action=="regional_vigil_abort")
            {
                if(station!=0||!Object.ReferenceEquals(site,vigilOrigin))
                    return true;
                ResetRegionalVigil();revision++;sound="ui";
                Notify("主动取消巡护。所有临时敌人撤离，物资没有重复结算。",
                    "Watch withdrawn. Temporary raiders leave; no first-clear reward granted.");
                return true;
            }
            if(station!=RegionalVigilNextStation)return true;
            if(action=="regional_vigil_service")
            {
                if(vigilStage>=3||!RegionalVigilRaidersDefeated()||!SafeTravel)
                {
                    Notify("必须先清除真实袭击者与附近的威胁，才能安全维护。",
                        "Defeat actual raiders and nearby danger before servicing the station.");
                    return true;
                }
                int material=room==0||room==2||room==5?0:
                    room==6||room==7?2:4;
                if(!HasInventoryRoom(material,0))return true;
                // One material per physical station. Prevent free remote
                // signatures and reward-only UI dialogue from advancing.
                if(!Spend(material==0?1:0,0,material==2?1:0,0,
                    material==4?1:0))return true;
                vigilStage++;
                effects.Add(new Effect("discover",new Point(site.x,site.y),.7f));
                revision++;sound="quest";
                Notify(vigilStage==3?"两处维护站已核验，带真实签收单返回出发装置。":
                    "已完成第一处真正的工程维护，继续到下一个远端装置。",
                    vigilStage==3?"Both real work sites signed. Return to the originating machine.":
                    "First genuine emergency station serviced. Travel to the second.");
                return true;
            }
            if(action!="regional_vigil_report"||vigilStage!=3||
                !Object.ReferenceEquals(site,vigilOrigin))return true;
            if(!RegionalVigilRaidersDefeated()||!SafeTravel)return true;
            int completed=vigilRoom;
            bool rushed=vigilRush;
            ResetRegionalVigil();
            if(RegionalVigilDone(completed))return true;
            Flag("regional_vigil_done_"+completed);
            if(rushed)Flag("regional_vigil_urgent_"+completed);
            CreditCoins(rushed?62:42);CreditShards(rushed?2:1);
            AddXp(rushed?100:75);
            RecordMoment(vigilZh[completed]+
                " 两处真实公共设施在限时巡护中完成复核，签收单带回了出发地标。"+
                "此后当地安全补给的恢复效果提高。",
                vigilEn[completed]+
                " Two distant, real public stations are now certified by an actual timed patrol; their receipt returned to its origin. Local shelter recovery improves.");
            if(RegionalVigilWins==8&&!Has("regional_vigil_all"))
            {
                Flag("regional_vigil_all");CreditSkillPoints(1);CreditShards(2);
                RecordMoment("八区公共工程都经受了真实路途上的紧急复查。救援不再是地图上的八个勾，而是每处居民实际收回的签收单。",
                    "All eight civilian networks passed genuine local emergencies, with signed receipts for each walked and defended route.");
            }
            revision++;sound="reward";
            Notify(rushed?
                    "抢修快线完成：硬币+62、碎片+2、经验+100，已完成 ":
                    "稳妥巡护完成：硬币+42、碎片+1、经验+75，已完成 "+
                RegionalVigilWins+"/8地区。",
                (rushed?
                    "Urgent field watch: +62 coins, +2 shards, +100 XP. ":
                    "Safe field watch: +42 coins, +1 shard, +75 XP. ")+
                RegionalVigilWins+"/8 districts certified.");
            return true;
        }
    }
}
