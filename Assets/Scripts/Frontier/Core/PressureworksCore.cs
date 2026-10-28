using System;
namespace Starfall
{
    // A real Ironreach industrial emergency. The pressure clock, steam
    // warnings and owned guards are transient; only public end-state flags
    // inhabit the existing v2 save.
    public sealed partial class GameCore
    {
        private int pressureworksStage,pressureworksKills,pressureworksCycle;
        private float pressureworksTime,pressureworksPressure,pressureworksPulse;
        private bool pressureworksPurgeUsed;
        private Enemy pressureworksA,pressureworksB;
        public bool PressureworksComplete {get{return Has("pressureworks_workers")||
            Has("pressureworks_caravan");}}
        public bool PressureworksWorkers {get{return Has("pressureworks_workers");}}
        public bool PressureworksCaravan {get{return Has("pressureworks_caravan");}}
        public bool PressureworksActive {get{return pressureworksStage>0&&save.room==3;}}
        public int PressureworksStage {get{return PressureworksActive?pressureworksStage:0;}}
        public float PressureworksTime {get{return PressureworksActive?pressureworksTime:0f;}}
        public float PressureworksPressure {get{return PressureworksActive?pressureworksPressure:0f;}}
        public bool PressureworksPurgeAvailable
        {get{return pressureworksStage>=2&&pressureworksStage<=3&&
            !pressureworksPurgeUsed&&save.inventory[4]>=1;}}
        public int PressureworksGuardsAlive
        {get{return (PressureworksOwnedGuard(pressureworksA)&&enemies.Contains(pressureworksA)&&pressureworksA.hp>0?1:0)+
            (PressureworksOwnedGuard(pressureworksB)&&enemies.Contains(pressureworksB)&&pressureworksB.hp>0?1:0);}}
        public Point PressureworksTarget
        {get{return pressureworksStage==1?new Point(60.5f,37.5f):
            pressureworksStage==2?new Point(67.5f,36.5f):
            pressureworksStage>=3?new Point(67.5f,44.5f):
            new Point(54.5f,38.5f);}}
        public string PressureworksObjective
        {get{return pressureworksStage==1?L("扳开一号冷却阀","OPEN FIRST COOLANT VALVE"):
            pressureworksStage==2?L("等待压力回升并开启二号阀","BALANCE SECOND VALVE"):
            pressureworksStage==3?L("避开蒸汽并击败两名接管者","EVADE STEAM · DEFEAT BOTH GUARDS"):
            pressureworksStage==4?L("亲自交还工区动力控制权","RESTORE THE FACTORY'S POWER"):
            L("读取厂区事故报告","READ THE FACTORY NOTICE");}}
        public bool PressureworksOwnedGuard(Enemy e)
        {return e!=null&&pressureworksStage>=3&&save.room==3&&
            (Object.ReferenceEquals(e,pressureworksA)||Object.ReferenceEquals(e,pressureworksB));}
        private void OnPressureworksEnemyDefeated(Enemy e)
        {
            if(!PressureworksOwnedGuard(e))return;
            if(Object.ReferenceEquals(e,pressureworksA))pressureworksKills|=1;
            if(Object.ReferenceEquals(e,pressureworksB))pressureworksKills|=2;
        }
        private void ResetPressureworks()
        {
            if(pressureworksA!=null)
            {
                RemoveEnemyPendingWarning(pressureworksA);
                enemies.Remove(pressureworksA);
            }
            if(pressureworksB!=null)
            {
                RemoveEnemyPendingWarning(pressureworksB);
                enemies.Remove(pressureworksB);
            }
            hazards.RemoveAll(h=>h.pressureworksOwned);
            pressureworksStage=pressureworksKills=pressureworksCycle=0;
            pressureworksTime=pressureworksPressure=pressureworksPulse=0;
            pressureworksPurgeUsed=false;
            pressureworksA=pressureworksB=null;
        }
        // Talk() runs before pendingDialogueSourceRoom is set. The initial
        // physical encounter and the committed menu choice have distinct
        // lifecycles; validate the selected room during Choose instead.
        private bool PressureworksSource(WorldObject o,string id)
        {return save.room==3&&o!=null&&o.id==id&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);}
        private bool TryPressureworksFoot(Point center,Point other,out Point result)
        {
            Point[] delta={new Point(-3f,0),new Point(3f,0),
                new Point(0,-3f),new Point(0,3f),new Point(-3f,-2f),
                new Point(3f,-2f),new Point(-2f,3f),new Point(2f,3f),
                new Point(-4f,1f),new Point(4f,1f),new Point(0,-4f)};
            foreach(Point off in delta)
            {
                Point foot=center+off;
                if(!CanStand(foot)||!ClearAttackPath(center,foot)||
                    !BossSummonHasLocalRoute(foot,center)||
                    (foot-Position).Length<1.2f||
                    (foot-other).Length<1.6f)continue;
                bool collision=false;
                foreach(Enemy e in enemies)
                    if(e!=null&&e.hp>0&&(e.Position-foot).Length<1.3f)
                    {collision=true;break;}
                if(collision)continue;
                result=foot;return true;
            }
            result=new Point();return false;
        }
        private bool StartPressureworksGuards()
        {
            Point center=new Point(67.5f,44.5f),a,b;
            if(!TryPressureworksFoot(center,new Point(-999,-999),out a)||
                !TryPressureworksFoot(center,a,out b))return false;
            pressureworksA=CreatePlacedEnemy(++enemyId,2,a,165f);
            pressureworksB=CreatePlacedEnemy(++enemyId,0,b,110f);
            pressureworksA.timer=1.0f;pressureworksB.timer=1.25f;
            enemies.Add(pressureworksA);enemies.Add(pressureworksB);
            pressureworksKills=0;
            effects.Add(new Effect("discover",a,.7f));
            return true;
        }
        private bool TalkPressureworks(WorldObject o)
        {
            if(o==null||save.room!=3||!o.kind.StartsWith("pressureworks",
                StringComparison.Ordinal))return false;
            if(!PressureworksSource(o,o.id))return true;
            if(o.id=="pressureworks_notice")
            {
                if(!Has("boss_3"))
                {
                    Dialog(o.name,o.en,
                        "钢铁守护者仍控制旧矿道。先解除主线封锁，才能组织工人处理高压锅炉。",
                        "The original Ironreach guardian still controls the rails. Clear that chapter first.",
                        C("稍后再来","Return later","close"));return true;
                }
                if(PressureworksComplete)
                {
                    Dialog(o.name,o.en,PressureworksWorkers?
                        "被封锁的工区如今由工人自己管理，轮班名单终于不再写着失踪者的名字。":
                        "旧压力室的技术图纸已交给民用商队，矿区不再为封锁令独占动力。",
                        PressureworksWorkers?
                        "Workers now run the factory themselves; the lost are no longer numbers on a shift chart.":
                        "The pressure drawings now belong to civilian carriers, not the blockade.",
                        C("查看维修记录","Read the repair record","close"));return true;
                }
                if(pressureworksStage==0)
                {
                    Dialog(o.name,o.en,
                        "旧工区有三段互相牵制的蒸汽管。先打开北侧一号冷却阀，再等压力恢复到正确区间转动东侧二号阀。旧军队派了两名守卫占据南侧总机；高压会喷出能真正伤人的蒸汽。必须亲自处理阀门、保护工人并把总机交还居民。可从这里随时放弃重试。",
                        "The old works have three pressure-coupled steam pipes. Open the first north coolant wheel, let the system recover, then turn the east wheel at a safe reading. Two former military guards hold the southern engine. Live steam jets are lethal. Reach each wheel, defeat the real guards and return ownership of the power. Abandon here to retry.",
                        C("接管事故现场","Begin the pressureworks run","pressureworks_begin"),
                        C("暂不介入","Not now","close"));return true;
                }
                Dialog(o.name,o.en,
                    "压力室尚在运行。路过工区告示牌可主动取消本次调压；已经用掉的备件不会退还。",
                    "The chamber is live. You may abort this attempt at the notice; spent parts are not refunded.",
                    C("继续抢修","Continue repair","close"),
                    C("关闭本次调压","Abort this run","pressureworks_abort"));return true;
            }
            if(o.id=="pressureworks_valve1")
            {
                Dialog(o.name,o.en,
                    pressureworksStage==1?
                        "一号阀要在压力35—80时手动开启。旋转会把压力降低16；必须实际走到这只阀门旁边。":
                        "旧一号冷却阀保持密封。工区尚未到它的调压步骤。",
                    pressureworksStage==1?
                        "Open first wheel only at pressure 35–80. It vents 16 pressure, and requires real physical contact.":
                        "The first coolant valve is sealed until its proper stage.",
                    C("开启一号阀","Turn valve one","pressureworks_valve1"),
                    C("保持当前状态","Leave unchanged","close"));return true;
            }
            if(o.id=="pressureworks_valve2")
            {
                Dialog(o.name,o.en,
                    "二号阀需要压力恢复到26—75。启动后总机的两名旧守卫会真正投入战斗，蒸汽喷发越来越频繁。不能让压力达到100。",
                    "Second wheel needs pressure 26–75. Activating it releases two physical guards at the engine and accelerates live steam jets. Do not let pressure reach 100.",
                    C("开启二号阀","Turn valve two","pressureworks_valve2"),
                    C("暂时等待","Wait","close"));return true;
            }
            if(o.id=="pressureworks_engine")
            {
                if(pressureworksStage==4)
                {
                    Dialog(o.name,o.en,
                        "两名守卫倒下，压力终于退回安全位置。工人拿出写满名字的轮班记录：“别让这间厂房再决定谁可以活着回家。”你会让工人管理公共冷却设施，还是把动力图纸交还民用运输队？",
                        "Both guards are down. The pressure returns to safety. Workers hold up a shift list full of names. 'Don't let this factory decide who comes home.' Give workers the public cooling infrastructure, or release its schematics to civilian carriers?",
                        C("交给工人自治","Public workers' cooperative","pressureworks_workers"),
                        C("公布民用动力图","Open caravan schematics","pressureworks_caravan"));return true;
                }
                Dialog(o.name,o.en,
                    "总机还在高压运行。可以花一枚备件手动排气一次，真正降低22压力并清除本工区已经标出的蒸汽危险；本次只允许一次。",
                    "The engine is live. Spend one spare part to purge 22 pressure and clear this chamber's marked steam hazards. Once per attempt.",
                    C("备件1 · 紧急排气","1 spare part · purge steam","pressureworks_purge"),
                    C("返回现场","Return to the works","close"));return true;
            }
            return false;
        }
        private bool ChoosePressureworks(string action)
        {
            if(action==null||!action.StartsWith("pressureworks_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            if(save.room!=3||mode!="world"||Dying||practicing||
                source==null||pendingDialogueSourceRoom!=3||
                !PressureworksSource(source,source.id))return true;
            if(action=="pressureworks_begin")
            {
                if(source.id!="pressureworks_notice"||pressureworksStage!=0||
                    PressureworksComplete||MineConvoyActive||!Has("boss_3")||
                    FieldWorkPreventsRest||!SafeTravel)return true;
                pressureworksStage=1;pressureworksTime=175f;
                pressureworksPressure=42f;pressureworksPulse=4.0f;
                TrackQuest("pressureworks");revision++;sound="quest";
                Notify("高压冷却任务启动！先去北侧一号阀，注意倒计时。",
                    "Pressureworks active! Reach the first coolant wheel before the engine ruptures.");
                return true;
            }
            if(action=="pressureworks_abort")
            {
                if(source.id=="pressureworks_notice"&&PressureworksActive)
                {ResetPressureworks();revision++;
                 Notify("调压中止。可从告示牌重新组织抢修。",
                     "Repair aborted. Start again at the notice.");}
                return true;
            }
            if(action=="pressureworks_valve1")
            {
                if(source.id!="pressureworks_valve1"||pressureworksStage!=1)
                    return true;
                if(pressureworksPressure<35f||pressureworksPressure>80f)
                {Notify("一号阀压力不在35—80安全区间。","Valve one needs pressure 35–80.");return true;}
                pressureworksPressure=Math.Max(0,pressureworksPressure-16f);
                pressureworksStage=2;revision++;sound="quest";
                Notify("一号阀已开启！等压力恢复到26—75，再到东侧开启二号阀。",
                    "Valve one open! Wait for pressure 26–75, then physically turn valve two.");
                return true;
            }
            if(action=="pressureworks_valve2")
            {
                if(source.id!="pressureworks_valve2"||pressureworksStage!=2)
                    return true;
                if(pressureworksPressure<26f||pressureworksPressure>75f)
                {Notify("二号阀必须等压力进入26—75才能转动。","Valve two needs pressure 26–75.");return true;}
                if(!StartPressureworksGuards())
                {Notify("总机守卫没有安全可达的战斗落脚点，先让开通路。",
                    "No legal engine arena for both guards. Clear the route.");return true;}
                pressureworksStage=3;pressureworksPulse=1.9f;
                pressureworksPressure=Math.Max(0,pressureworksPressure-11f);
                revision++;sound="quest";
                Notify("两名真实守卫现身！避开蒸汽，击退他们再交还总机。",
                    "Two real guards approach! Evade steam and defeat them before the engine fails.");
                return true;
            }
            if(action=="pressureworks_purge")
            {
                if(source.id!="pressureworks_engine"||
                    !PressureworksPurgeAvailable||!Spend(0,0,0,0,1))
                    return true;
                pressureworksPurgeUsed=true;
                pressureworksPressure=Math.Max(0f,pressureworksPressure-22f);
                pressureworksPulse=Math.Max(pressureworksPulse,1.6f);
                hazards.RemoveAll(h=>h.pressureworksOwned);
                revision++;sound="reward";
                Notify("排气完成：压力-22，本场蒸汽预警清除！只能使用一次。",
                    "Manual purge: −22 pressure, current steam hazards cleared! One use per run.");
                return true;
            }
            if(action=="pressureworks_workers"||action=="pressureworks_caravan")
            {
                if(source.id!="pressureworks_engine"||pressureworksStage!=4||
                    pressureworksKills!=3||PressureworksComplete||
                    PressureworksGuardsAlive>0)return true;
                bool workers=action=="pressureworks_workers";
                ResetPressureworks();
                Flag(workers?"pressureworks_workers":"pressureworks_caravan");
                CreditCoins(70);CreditShards(3);AddXp(110);revision++;sound="reward";
                RecordMoment(workers?
                    "你抢修了铁炉堡的旧压力室，把控制阀和工人轮班名册一同归还给工坊。":
                    "你抢修了铁炉堡的旧压力室，将被封存的动力图纸公开给来往民用商队。",
                    workers?
                    "You restored Ironreach's pressureworks and returned the coolant controls and shift register to workers.":
                    "You restored Ironreach's pressureworks and opened the old power schematics to civilian carriers.");
                Notify(workers?
                    "工坊自治完成：70硬币、3碎片、110经验；铁炉堡实际耐力恢复+2/秒。":
                    "民用图纸公开：70硬币、3碎片、110经验；铁炉堡攻击基础伤害+3。",
                    workers?
                    "Workers' cooperative restored: +70 coins, 3 shards, 110 XP; +2 energy regeneration in Ironreach.":
                    "Civilian power map released: +70 coins, 3 shards, 110 XP; +3 base damage in Ironreach.");
                return true;
            }
            return true;
        }
        private void TickPressureworks(float dt)
        {
            if(pressureworksStage==0)return;
            if(save.room!=3||Dying||practicing)
            {ResetPressureworks();revision++;return;}
            if(pressureworksStage==4)return;
            pressureworksTime-=dt;
            pressureworksPressure=Math.Min(100f,pressureworksPressure+
                dt*(pressureworksStage==1?.45f:pressureworksStage==2?.85f:1.4f));
            if(pressureworksTime<=0||pressureworksPressure>=100f)
            {
                ResetPressureworks();revision++;
                Notify("高压设备失控，工区已紧急停机。回告示牌重新组织抢修。",
                    "Pressureworks overload! The engine shut down. Restart safely at the notice.");
                return;
            }
            if(pressureworksStage==3)
            {
                if((pressureworksKills&1)==0&&!enemies.Contains(pressureworksA)||
                   (pressureworksKills&2)==0&&!enemies.Contains(pressureworksB))
                {
                    ResetPressureworks();revision++;
                    Notify("守卫未经过真实战斗确认便离场。请重新抢修。",
                        "Guards vanished without confirmed combat defeat. Restart the repair.");
                    return;
                }
                if(pressureworksKills==3)
                {
                    pressureworksStage=4;
                    hazards.RemoveAll(h=>h.pressureworksOwned);
                    pressureworksPressure=24f;
                    revision++;sound="quest";
                    Notify("守卫已真实击败！蒸汽暂时平息，回总机决定工区归属。",
                        "Both guards genuinely defeated! Steam has settled; choose control at the engine.");
                    return;
                }
            }
            if(pressureworksStage<2)return;
            pressureworksPulse-=dt;
            if(pressureworksPulse>0f)return;
            // Real steam jets telegraph around current hero footing, leaving
            // one actual traversable sidestep. Existing Hazard simulation
            // applies their countdown/damage to the physical character.
            Point[] cardinal={new Point(1,0),new Point(0,1),
                new Point(-1,0),new Point(0,-1)};
            int preferred=pressureworksCycle++%4,safe=-1;
            for(int i=0;i<4;i++)
            {
                int n=(preferred+i)%4;
                Point foot=Position+cardinal[n]*1.55f;
                if(CanStand(foot)&&ClearAttackPath(Position,foot))
                {safe=n;break;}
            }
            pressureworksPulse=pressureworksStage==3?3.4f:5.1f;
            if(safe<0)return;
            for(int i=0;i<4;i++)
            {
                if(i==safe)continue;
                Point foot=Position+cardinal[i]*1.55f;
                if(!CanStand(foot))continue;
                hazards.Add(new Hazard {position=foot,
                    radius=.95f,timer=1.5f,
                    damage=pressureworksPressure>65f?18f:12f,
                    kind="glass",pressureworksOwned=true});
            }
            effects.Add(new Effect("number",Position,.75f,
                L("高压蒸汽 · 避开喷口","STEAM BURST · FIND OPEN EXIT")));
        }
    }
}
