using System;
using System.Collections.Generic;
namespace Starfall
{
    // A bespoke, non-repeating four-site story chapter in the Iron Mine.
    // The missing nightshift, erased manifests and false "empty" train
    // are investigated on the actual map; the battle is a real ambush.
    // Durable stages use only optional v2 flags, while the combat wave
    // and its emergency deadline have session-only state.
    public sealed partial class GameCore
    {
        private int ghostTrainRoom=-1;
        private float ghostTrainClock;
        private WorldObject ghostTrainSwitch;
        private MapData ghostTrainMap;
        private SaveData ghostTrainSave;
        private readonly List<Enemy> ghostTrainGuards=new List<Enemy>();
        private readonly HashSet<Enemy> ghostTrainDefeated=new HashSet<Enemy>();
        public bool GhostTrainComplete {get{return Has("ghost_train_complete");}}
        public bool GhostTrainPublic {get{return Has("ghost_train_public");}}
        public bool GhostTrainFreight {get{return Has("ghost_train_freight");}}
        public int GhostTrainChapter
        {
            get
            {
                if(GhostTrainComplete)return 5;
                if(Has("ghost_train_medbay"))return 4;
                if(Has("ghost_train_switch"))return 3;
                if(Has("ghost_train_manifest"))return 2;
                if(Has("ghost_train_started"))return 1;
                return 0;
            }
        }
        public bool GhostTrainBattleActive
        {
            get{return ghostTrainRoom==3&&ghostTrainClock>0f&&
                save.room==3&&Object.ReferenceEquals(ghostTrainMap,Map)&&
                Object.ReferenceEquals(ghostTrainSave,save)&&
                ghostTrainSwitch!=null&&
                SourceBelongsToActiveMap(ghostTrainSwitch);}
        }
        public int GhostTrainEnemiesLeft
        {
            get{int n=0;if(GhostTrainBattleActive)
                foreach(Enemy e in ghostTrainGuards)
                    if(!ghostTrainDefeated.Contains(e)&&enemies.Contains(e))
                        n++;
                return n;}
        }
        public float GhostTrainSeconds
        {get{return GhostTrainBattleActive?ghostTrainClock:0f;}}
        public Point GhostTrainTarget
        {
            get
            {
                string id=GhostTrainBattleActive?"mine_ghost_switch":
                    GhostTrainChapter==1?"mine_ghost_manifest":
                    GhostTrainChapter==2?"mine_ghost_switch":
                    GhostTrainChapter==3?"mine_ghost_medbay":
                    GhostTrainChapter==4?"mine_ghost_dispatch":"";
                if(save.room!=3||id=="")return new Point();
                foreach(WorldObject o in Map.objects)
                    if(o.id==id)return new Point(o.x,o.y);
                return new Point();
            }
        }
        private void ResetGhostTrain()
        {
            if(ghostTrainRoom>=0)
            {
                projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                    ghostTrainGuards.Contains(p.hostileShooter));
                for(int i=enemies.Count-1;i>=0;i--)
                    if(ghostTrainGuards.Contains(enemies[i]))
                    {RemoveEnemyPendingWarning(enemies[i]);enemies.RemoveAt(i);}
            }
            ghostTrainGuards.Clear();ghostTrainDefeated.Clear();
            ghostTrainRoom=-1;ghostTrainClock=0f;
            ghostTrainMap=null;ghostTrainSave=null;
            ghostTrainSwitch=null;
        }
        private bool IsGhostTrainEnemy(Enemy e)
        {return e!=null&&ghostTrainRoom>=0&&ghostTrainGuards.Contains(e);}
        private void OnGhostTrainEnemyDefeated(Enemy e)
        {if(IsGhostTrainEnemy(e))ghostTrainDefeated.Add(e);}
        private bool GhostTrainGuardsReallyDefeated()
        {
            if(!GhostTrainBattleActive||ghostTrainGuards.Count!=3)return false;
            foreach(Enemy e in ghostTrainGuards)
                if(!ghostTrainDefeated.Contains(e)||enemies.Contains(e))
                    return false;
            return true;
        }
        private void TickGhostTrain(float dt)
        {
            if(ghostTrainRoom<0)return;
            if(!GhostTrainBattleActive||Dying)
            {ResetGhostTrain();return;}
            if(dt<=0f)return;
            ghostTrainClock=Math.Max(0,ghostTrainClock-dt);
            if(ghostTrainClock<=0)
            {
                ResetGhostTrain();revision++;
                Notify("夜班列车的封锁已经重新合拢。没有取得签收，回转辙台重新行动。",
                    "The nightshift switch sealed again. No signed evidence. Retry at the physical lever.");
                return;
            }
            foreach(Enemy e in ghostTrainGuards)
                if(!ghostTrainDefeated.Contains(e)&&!enemies.Contains(e))
                {
                    ResetGhostTrain();revision++;
                    Notify("一名守卫未经真实战斗就消失。证据保全失效，必须重新行动。",
                        "A guard vanished without a real combat defeat. Evidence cannot be certified.");
                    return;
                }
        }
        private bool BeginGhostTrainBattle(WorldObject source)
        {
            var offsets=new Point[]{
                new Point(-5,1),new Point(5,1),new Point(0,-5),
                new Point(-7,0),new Point(7,0),new Point(2,6),
                new Point(-3,-7),new Point(7,4),new Point(-6,4),
                new Point(0,8),new Point(8,-2),new Point(4,-7)};
            Point pivot=new Point(source.x,source.y);
            var pads=new List<Point>();
            foreach(Point delta in offsets)
            {
                Point p=pivot+delta;
                if(!CanStand(p)||(p-Position).Length<2||
                    (save.companion>=0&&(p-companionPosition).Length<2f)||
                    enemies.Exists(e=>e!=null&&e.hp>0f&&
                        (e.Position-p).Length<2f)||
                    pads.Exists(q=>(q-p).Length<3f)||
                    !ActorHasLocalRoute(p,pivot,16))continue;
                pads.Add(p);
                if(pads.Count==3)break;
            }
            if(pads.Count!=3)
            {
                Notify("转辙台周围没有三条真实可通行的守卫进路。清理附近危险后重试。",
                    "Not enough valid combat approach lanes near the switch. Clear local threats and retry.");
                return false;
            }
            ghostTrainRoom=3;ghostTrainClock=180;
            ghostTrainSwitch=source;ghostTrainMap=Map;ghostTrainSave=save;
            for(int i=0;i<3;i++)
            {
                Enemy e=CreatePlacedEnemy(++enemyId,i==0?1:i==1?2:0,
                    pads[i],64+i*18);
                ghostTrainGuards.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",pads[i],.6f){variant=i});
            }
            revision++;sound="alert";
            Notify("伪装成矿车护卫的三人封锁了转辙台。180秒内真实击败全部守卫，才能放出被藏起的夜班车厢。",
                "Three false railway guards seal the switch. Defeat all three real combat actors within 180 seconds to release the missing nightshift wagon.");
            return true;
        }
        private bool GhostTrainObject(WorldObject o)
        {
            return o!=null&&o.kind=="mineGhostTrain"&&save.room==3&&
                (o.id=="mine_ghost_dispatch"||o.id=="mine_ghost_manifest"||
                 o.id=="mine_ghost_switch"||o.id=="mine_ghost_medbay");
        }
        private bool TalkGhostTrain(WorldObject o)
        {
            if(o==null||o.kind!="mineGhostTrain")return false;
            if(!GhostTrainObject(o)||!SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position))return true;
            int stage=GhostTrainChapter;
            if(!RegionalMachineDone(3))
            {
                Dialog(o.name,o.en,
                    "夜班登记纸缺了一半。先修复赤铁矿场的三处民用工程装置，铁路信号才会恢复。",
                    "Half the nightshift records have been destroyed. First repair all three civil engineering mechanisms in the Iron Mine.",
                    C("记下线索","Remember the clue","close"));
                return true;
            }
            if(stage==5)
            {
                Dialog(o.name,o.en,GhostTrainPublic?
                    "那份写着17名夜班矿工姓名的原始名册如今钉在公示牌上。调度员终于肯承认，所谓空车厢里一直有人。":
                    "你保住了唯一能载伤员下山的侧线。幸存者留下的姓名依然交给他们本人决定何时公开。",
                    GhostTrainPublic?
                    "The names of seventeen missing nightshift workers are now public. Dispatch can no longer pretend the wagon was empty.":
                    "The workers' rescue track is operating again. The survivors keep the right to decide when their names are public.",
                    C("向矿工道别","Leave the workers","close"));
                return true;
            }
            if(o.id=="mine_ghost_dispatch")
            {
                Dialog(o.name,o.en,
                    stage==0?
                    "调度员诺尔掀开一块积灰的旧班牌。\n\n“第十七趟矿车说是空车，夜里却有人敲窗。那天之后，十七个工人的名字也从交班簿消失了。别替我猜真相，把原件找回来。”":
                    stage==4?
                    "诺尔把三份实地拿回的凭证并排放好。\n\n“人救回来了。现在是把这十七个名字当众贴出来，还是先修通他们回家的载人线？这次决定不能由调度室偷偷改。”":
                    "诺尔用铅笔在旧图上指了三个相距很远的现场：失踪名册、被封转辙台和医护站。\n\n“路不是抄写出来的。每一处你都亲自走到，再来讲结论。”",
                    stage==0?
                    "Noll brushes dust from the old roster.\n\n“Train seventeen was listed as empty. People heard knocking from its windows. By dawn seventeen workers had vanished from the shift book. Don't guess on my behalf; bring me the originals.”":
                    stage==4?
                    "Noll lays out the recovered records.\n\n“The workers are safe. Do we publicly post all seventeen names, or first reopen their only safe passenger track? Dispatch will not secretly make that choice again.”":
                    "Noll points to three far-flung real places on the route: the lost roster, a sealed switch and an infirmary.\n\n“Go there yourself. Then we can talk.”",
                    stage==0?
                        C("接下第十七趟列车调查","Investigate nightshift train seventeen","ghost_train_start"):
                    stage==4?
                        C("公开完整失踪名册","Publish the unaltered workers' names","ghost_train_public"):
                        C("查看接下来的线索","Review the route","close"),
                    stage==4?
                        C("先修复民用载人支线","Restore the safe passenger rail first","ghost_train_freight"):
                        C("离开调度台","Leave dispatch","close"),
                    C("暂时离开","Leave","close"));
                return true;
            }
            if(o.id=="mine_ghost_manifest")
            {
                Dialog(o.name,o.en,
                    "夜班名册被摁在上锁的铁柜底下，最上面的一页写着“空车”。但十八个班牌钩子里，恰好有十七个被取走。\n\n"+
                    (stage==1?"诺尔的字条提醒你：要保留工人最后签字的原件，而非誊抄后的空车票。":
                    "带走的原件已经留下副本；这一册纸不会再次变成奖励。"),
                    "A locked cabinet hides the original nightshift roster beneath a falsified EMPTY stamp. Seventeen of eighteen worker tags are missing.\n\n"+
                    (stage==1?"Noll's note: recover the original signed roster, not a copied empty carriage docket.":
                    "The original evidence was copied and sealed. This record cannot be collected twice."),
                    stage==1?
                        C("取出17人原始签字名册","Recover seventeen signed worker records","ghost_train_manifest"):
                        C("查看纸上的擦痕","Read the scratched paper","close"),
                    stage==1?
                        C("只抄空车货运单","Copy the false empty-freight docket","ghost_train_false_manifest"):
                        C("翻看旧班次","Review the roster","close"),
                    C("继续赶路","Keep moving","close"));
                return true;
            }
            if(o.id=="mine_ghost_switch")
            {
                string s=stage==2?
                    GhostTrainBattleActive?
                    GhostTrainEnemiesLeft>0?
                    "三名伪装护卫还在守住现场，必须用真正的战斗夺下转辙台。":
                    "三个守卫已经被真实击倒。现在可以花两份零件修复可通往医护站的民用转辙。":
                    "封住的转辙机还留着三个人的脚印。启动会引来真正的守卫。180秒内解决现场，再花两份零件抢通医护侧线。":
                    "旧转辙台的封锁已解除，伤员能从这条侧线抵达医护站。";
                string en=stage==2?
                    GhostTrainBattleActive?
                    GhostTrainEnemiesLeft>0?
                    "Three false railway guards remain. Only actual combat defeats will secure the lever.":
                    "All three real guards are down. Spend two gears to route the hidden passenger wagon toward the infirmary.":
                    "Three fresh bootprints surround the sealed lever. Three genuine enemies will respond; defeat them within 180 seconds before spending two gears to open the rescue siding.":
                    "The sealed switch now directs the hidden wagon to the workers' medical refuge.";
                Dialog(o.name,o.en,s,en,
                    stage==2?
                        C(GhostTrainBattleActive?"抢通救援侧线 · 零件×2":"启动转辙台伏击",
                          GhostTrainBattleActive?"Open rescue track · 2 gears":"Confront the real railway guards",
                          GhostTrainBattleActive?"ghost_train_switch_secure":"ghost_train_ambush"):
                        C("查看重开的侧线","Inspect repaired railway","close"),
                    stage==2&&GhostTrainBattleActive?
                        C("中止行动并撤离守卫","Abort combat and withdraw guards","ghost_train_abort"):
                        C("查看列车轮痕","Inspect wheel marks","close"),
                    C("继续前进","Continue","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "矿医杏留下的灯没有熄。两副用过的担架贴着墙，一封没寄出的信写着：\n\n“我们不是车上的货。请把名字还给家人。”\n\n"+
                (stage==3?"将两份月光草交给医护站，确保证词能跟伤员一起活着回去。":"病历仍然等着有人来核对。"),
                "An's infirmary lamp is still burning. Used stretchers lean against the wall. An unsent letter says:\n\n“We were not cargo. Give our families our names back.”\n\n"+
                (stage==3?"Deliver two real Moonherbs so the injured can survive long enough to give their testimony.":"The patient records remain for visitors to verify."),
                stage==3?
                    C("交付两份月光草并封存伤员证词","Deliver 2 Moonherbs and certify survivor testimony","ghost_train_medbay"):
                    C("查看担架记录","Inspect stretcher notes","close"),
                C("沿实地路线返回调度室","Return along the real route","close"));
            return true;
        }
        private bool ChooseGhostTrain(string action)
        {
            if(action==null||!action.StartsWith("ghost_train_",StringComparison.Ordinal))
                return false;
            WorldObject o=pendingDialogueSource;
            if(!GhostTrainObject(o)||mode!="world"||
                pendingDialogueSourceRoom!=3||!SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position)||
                Dying||practicing||!RegionalMachineDone(3))return true;
            int stage=GhostTrainChapter;
            if(action=="ghost_train_start"&&stage==0&&
                o.id=="mine_ghost_dispatch")
            {
                Flag("ghost_train_started");
                RecordMoment("旧调度桌上的第十七趟列车被写成空车，夜班却失踪了十七个人。你决定找回原始班牌，而不替沉默者猜测。",
                    "Nightshift train seventeen was marked empty, but seventeen workers disappeared. Recover primary evidence rather than guessing for the missing.");
                Notify("新章：失声的第十七趟列车。前往东北远端取回真实夜班名册。",
                    "New chapter: The Silent Seventeenth Train. Travel northeast to the original shift ledger.");
                return true;
            }
            if(action=="ghost_train_false_manifest"&&stage==1&&
                o.id=="mine_ghost_manifest")
            {
                if(save.energy<10)return true;
                save.energy-=10;revision++;sound="alert";
                Notify("假货单没有一个工人的签字。你花了10耐力查错方向。",
                    "The false docket carries no worker signatures. -10 energy verifying the wrong paper.");
                return true;
            }
            if(action=="ghost_train_manifest"&&stage==1&&
                o.id=="mine_ghost_manifest")
            {
                Flag("ghost_train_manifest");CreditCoins(5);
                Notify("十七份签字找到。前往东南封锁转辙台，查明那趟车真正去了哪里。",
                    "Seventeen original signatures secured. Follow the tracks southeast to the sealed switch.");
                return true;
            }
            if(action=="ghost_train_ambush"&&stage==2&&
                o.id=="mine_ghost_switch"&&!GhostTrainBattleActive&&
                !FieldWorkPreventsRest&&SafeTravel)
            {BeginGhostTrainBattle(o);return true;}
            if(action=="ghost_train_abort"&&stage==2&&
                o.id=="mine_ghost_switch"&&GhostTrainBattleActive&&
                Object.ReferenceEquals(o,ghostTrainSwitch))
            {
                ResetGhostTrain();revision++;
                Notify("你撤回了伏击战。下次必须重新击退三名守卫，不能保留半场胜利。",
                    "Switchyard encounter withdrawn. All three genuine guards must be defeated again.");
                return true;
            }
            if(action=="ghost_train_switch_secure"&&stage==2&&
                o.id=="mine_ghost_switch"&&GhostTrainBattleActive&&
                Object.ReferenceEquals(o,ghostTrainSwitch))
            {
                if(!GhostTrainGuardsReallyDefeated()||!SafeTravel)
                {
                    Notify("三名真正的守卫或流弹尚未清除，不能凭空拨动转辙台。",
                        "Three true guards and all nearby threats must be cleared first.");
                    return true;
                }
                if(!Spend(0,0,0,0,2))return true;
                ResetGhostTrain();Flag("ghost_train_switch");
                RecordMoment("转辙台被三名伪装成铁路守卫的人武装守住。你在真实战斗中击倒他们，用两份零件复位道岔，放出了载着伤员的第十七趟矿车。",
                    "Three disguised railway guards were defeated in genuine combat; two real gears reopened the emergency track for train seventeen's wounded passengers.");
                Notify("救援侧线已经在实地重开。沿铁路往西北走，找到医护站并交付治疗药草。",
                    "Rescue track genuinely opened. Travel northwest to the infirmary with medical herbs.");
                return true;
            }
            if(action=="ghost_train_medbay"&&stage==3&&
                o.id=="mine_ghost_medbay")
            {
                if(!Spend(0,0,2,0,0))return true;
                Flag("ghost_train_medbay");
                RecordMoment("你把两份月光草亲手交到夜班医护站，伤员的原始证词和十七份姓名从此有了实物封存。",
                    "Two real herbs reached the nightshift infirmary in person. Survivors and their original signed testimony are preserved.");
                Notify("伤员已获救，证词封存。走回西侧的调度桌，由居民自己决定铁路的未来。",
                    "Survivors treated. Return on foot to the western dispatch desk for a public decision.");
                return true;
            }
            if((action=="ghost_train_public"||action=="ghost_train_freight")&&
                stage==4&&o.id=="mine_ghost_dispatch"&&
                !GhostTrainComplete)
            {
                bool publish=action=="ghost_train_public";
                Flag(publish?"ghost_train_public":"ghost_train_freight");
                Flag("ghost_train_complete");
                CreditCoins(85);CreditShards(2);AddXp(125);
                CreditSkillPoints(1);
                if(publish)
                {
                    CreditShards(1);
                    RecordMoment("调度员把十七个被抹去的名字当众张贴。矿场今后不能再以“空车”为借口消失一批活人。公开证词也让本地公共补给处的医疗恢复更强。",
                        "Noll publicly posted all seventeen erased workers' names. No empty-freight classification can silently erase them again. Public shelters recover additional health.");
                }
                else
                {
                    CreditCoins(25);
                    RecordMoment("你优先修复夜班伤者回家的民用铁路支线；工人保留自己决定何时公开姓名的权利。安全铁路使矿区通行更快。",
                        "You restored the workers' passenger siding first and let survivors decide when to publish their names. Safe rail travel now improves local movement.");
                }
                Notify(publish?
                    "第十七趟列车：原始姓名公开。矿区公共补给治疗增强，经验+125。":
                    "第十七趟列车：民用铁路重开。矿区移动速度永久提升，经验+125。",
                    publish?
                    "Train seventeen: names restored to public memory. Better mine shelters and +125 XP.":
                    "Train seventeen: passenger railway restored. Permanent local travel speed and +125 XP.");
                return true;
            }
            return true;
        }
    }
}
