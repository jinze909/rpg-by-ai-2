using System;
using System.Collections.Generic;
namespace Starfall
{
    // The Erased Water Ledger: a distinct, live-world investigative stealth
    // chapter in the original Glass Desert. Two actual guard actors, two
    // contradictory records, a physical deduction and a lasting civic choice.
    // All persistent state remains ordinary SaveData v2 string flags.
    public sealed partial class GameCore
    {
        private MapData glassLedgerMap;
        private SaveData glassLedgerSave;
        private bool glassLedgerRunning,glassLedgerQuiet;
        private float glassLedgerSuspicionClock;
        private int glassLedgerAlarm,glassLedgerKills;
        private readonly Enemy[] glassLedgerGuards=new Enemy[2];
        public bool GlassLedgerStarted {get{return Has("glassledger_started");}}
        public bool GlassLedgerFirstEvidence {get{return Has("glassledger_seal");}}
        public bool GlassLedgerSecondEvidence {get{return Has("glassledger_witness");}}
        public bool GlassLedgerReconstructed {get{return Has("glassledger_reconstructed");}}
        public bool GlassLedgerPublic {get{return Has("glassledger_public");}}
        public bool GlassLedgerProtected {get{return Has("glassledger_protected");}}
        public bool GlassLedgerComplete {get{return GlassLedgerPublic||GlassLedgerProtected;}}
        public bool GlassLedgerQuietRecord {get{return Has("glassledger_clean");}}
        public bool GlassLedgerUnlocked {get{return CanEnter(6)&&Has("harbor_report");}}
        public bool GlassLedgerActive {get{return glassLedgerRunning&&
            save.room==6&&System.Object.ReferenceEquals(glassLedgerMap,Map)&&
            System.Object.ReferenceEquals(glassLedgerSave,save)&&!Dying;}}
        public int GlassLedgerAlarm {get{return GlassLedgerActive?glassLedgerAlarm:0;}}
        public int GlassLedgerActualGuards
        {
            get
            {
                if(!GlassLedgerActive)return 0;
                int count=0;
                foreach(Enemy e in glassLedgerGuards)
                    if(e!=null&&e.hp>0f&&enemies.Contains(e))count++;
                return count;
            }
        }
        public Point GlassLedgerTarget
        {
            get
            {
                string id=!GlassLedgerStarted?"ivo":
                    !GlassLedgerActive&&!GlassLedgerReconstructed?"glassledger_watch":
                    !GlassLedgerFirstEvidence?"glassledger_seal":
                    !GlassLedgerSecondEvidence?"glassledger_witness":
                    !GlassLedgerReconstructed?"glassledger_witness":"ivo";
                foreach(WorldObject o in data.maps[6].objects)
                    if(o.id==id)return new Point(o.x,o.y);
                return new Point(22,18);
            }
        }
        public string GlassLedgerObjective
        {
            get
            {
                if(!GlassLedgerStarted)return L("找伊沃调查沙海消失的水单","ASK IVO ABOUT THE MISSING WATER");
                if(!GlassLedgerActive&&!GlassLedgerReconstructed)
                    return L("在远岸守夜柱开启实地调查","START THE NIGHT WATCH AT THE DISTANT POST");
                if(!GlassLedgerFirstEvidence)
                    return L("在实地巡逻中读取被改动的配水单","READ THE ALTERED WATER RECORD");
                if(!GlassLedgerSecondEvidence)
                    return L("绕过巡兵找到无名矿工的证词","FIND THE UNLISTED WORKER'S TESTIMONY");
                if(!GlassLedgerReconstructed)
                    return L("在矿工便条旁重建真正的水账","RECONSTRUCT THE TRUE WATER LEDGER");
                return L("把证据交还给原来的伊沃，决定如何保护居民",
                    "RETURN THE EVIDENCE TO IVO FOR A CIVIC DECISION");
            }
        }
        private WorldObject GlassLedgerObject(string id)
        {
            foreach(WorldObject o in data.maps[6].objects)
                if(o.id==id)return o;
            return null;
        }
        private bool GlassLedgerReach(WorldObject source)
        {return source!=null&&save.room==6&&
            SourceBelongsToActiveMap(source)&&HasPhysicalInteractionReach(source)&&
            CanStand(Position);}
        public bool IsGlassLedgerGuard(Enemy enemy)
        {return GlassLedgerActive&&enemy!=null&&
            (System.Object.ReferenceEquals(enemy,glassLedgerGuards[0])||
             System.Object.ReferenceEquals(enemy,glassLedgerGuards[1]));}
        private void OnGlassLedgerGuardDefeated(Enemy enemy)
        {
            if(!IsGlassLedgerGuard(enemy))return;
            glassLedgerQuiet=false;
            glassLedgerKills|=System.Object.ReferenceEquals(enemy,glassLedgerGuards[0])?1:2;
        }
        private bool GlassLedgerSecured
        {get{return GlassLedgerActive&&
            (glassLedgerAlarm<3||glassLedgerKills==3);}}
        private void ResetGlassLedger()
        {
            foreach(Enemy guard in glassLedgerGuards)
                if(guard!=null)
                {
                    RemoveEnemyPendingWarning(guard);
                    enemies.Remove(guard);
                }
            glassLedgerGuards[0]=glassLedgerGuards[1]=null;
            glassLedgerRunning=false;glassLedgerQuiet=false;
            glassLedgerMap=null;glassLedgerSave=null;
            glassLedgerAlarm=glassLedgerKills=0;glassLedgerSuspicionClock=0f;
        }
        private bool StartGlassLedgerWatch()
        {
            if(save.room!=6||!GlassLedgerStarted||GlassLedgerReconstructed||
               GlassLedgerActive||glassLedgerRunning||Dying||practicing||racing||
               mode!="world")return false;
            Point[] feet={new Point(62.5f,38.5f),new Point(75.5f,39.5f)};
            for(int i=0;i<feet.Length;i++)
                if(!CanStand(feet[i])||
                   (feet[i]-Position).Length<2.5f||
                   enemies.Exists(e=>e!=null&&e.hp>0f&&
                       (e.Position-feet[i]).Length<1.7f))return false;
            glassLedgerRunning=true;glassLedgerQuiet=true;
            glassLedgerMap=Map;glassLedgerSave=save;
            glassLedgerAlarm=glassLedgerKills=0;glassLedgerSuspicionClock=0;
            for(int i=0;i<2;i++)
            {
                Enemy guard=CreatePlacedEnemy(++enemyId,i,feet[i],110f);
                glassLedgerGuards[i]=guard;
                enemies.Add(guard);
                effects.Add(new Effect("enemyRelease",feet[i],.55f)
                    {radius=.75f,variant=0});
            }
            revision++;sound="quest";
            RecordMoment(
                "伊沃的水账写着‘无领水人’。沙海远岸的守夜柱响起钟声，两名守卫正看护那份被改写的配水单。",
                "Ivo's water ledger says 'no recipients.' A bell rings at the desert's outer watch; two living guards keep the altered ration records.");
            Notify("潜入调查开始。绕开真实守卫的视线，寻找两份证词；也可以战斗突围。",
                "Investigation begun. Avoid real guards' sight and read both records, or fight your way through.");
            return true;
        }
        private void TickGlassLedger(float dt)
        {
            if(!GlassLedgerActive)
            {
                if(glassLedgerRunning)ResetGlassLedger();
                if(!GlassLedgerComplete||save.room!=6||Dying||
                   !SafeTravel||FieldWorkPreventsRest)return;
                WorldObject service=GlassLedgerObject("fieldstation6");
                WorldObject shade=GlassLedgerObject("oasis0");
                bool atStation=service!=null&&PhysicalRefugeReach(
                    new Point(service.x,service.y),1.9f);
                bool atShade=shade!=null&&PhysicalRefugeReach(
                    new Point(shade.x,shade.y),1.9f);
                if(GlassLedgerPublic&&atStation)
                    save.heat=Math.Max(0f,save.heat-8f*dt);
                if(GlassLedgerProtected&&atShade)
                    save.energy=Math.Min(100f,save.energy+4f*dt);
                return;
            }
            // Observe the SAME living actor references used by combat, not
            // any NPC with an arbitrary recycled numeric ID. Sight must be
            // real, unobstructed and within patrol radius; quiet stepping
            // reduces the effective acquisition cone/range.
            bool visible=false;
            foreach(Enemy guard in glassLedgerGuards)
            {
                if(!IsGlassLedgerGuard(guard)||guard.hp<=0f||
                   !enemies.Contains(guard)||guard.stun>0f)continue;
                float range=(guard.Position-Position).Length;
                if(range>(QuietStepping?3.6f:7.2f)||!CanStand(guard.Position)||
                   !CanStand(Position)||!ClearAttackPath(guard.Position,Position))
                    continue;
                if(QuietStepping&&!ImpactSearchGuardSeesHero(guard))continue;
                visible=true;break;
            }
            if(!visible)
            {glassLedgerSuspicionClock=Math.Max(0f,glassLedgerSuspicionClock-dt*.75f);return;}
            glassLedgerSuspicionClock+=dt;
            if(glassLedgerSuspicionClock<1f)return;
            glassLedgerSuspicionClock=0f;glassLedgerQuiet=false;
            if(glassLedgerAlarm<3)
            {
                glassLedgerAlarm++;revision++;sound="alert";
                Notify("守卫识破了潜行，沙海档案警戒 "+glassLedgerAlarm+"/3。掩蔽视线或击退两名守卫。",
                    "Patrol spotted you: archive alert "+glassLedgerAlarm+
                    "/3. Break sight, or defeat both real guards.");
            }
        }
        private bool TalkGlassLedger(WorldObject site)
        {
            if(site==null||!site.kind.StartsWith("glassledger",StringComparison.Ordinal))
                return false;
            if(!GlassLedgerReach(site))return true;
            if(!GlassLedgerStarted||!GlassLedgerUnlocked)
            {
                Dialog(site.name,site.en,
                    "旧水站上只有划掉的领水人姓名。想看清字迹，先去找伊沃询问。",
                    "Names are crossed out at this water post. Ask Ivo before inspecting the original evidence.",
                    C("继续前进","Continue","close"));
                return true;
            }
            if(GlassLedgerComplete)
            {
                Dialog(site.name,site.en,
                    GlassLedgerPublic?
                    "新配水牌已经立起来。每个人都可以查到自己被登记的领水份额。":
                    "证人姓名没有被公开；棚屋里仍有人悄悄为过路者留下一碗水。",
                    GlassLedgerPublic?
                    "The new public water roster lets everyone check their actual ration.":
                    "The witnesses' names stay private. A quiet shelter leaves water for travelers.",
                    C("离开","Leave","close"));
                return true;
            }
            if(site.id=="glassledger_watch")
            {
                Dialog(site.name,site.en,GlassLedgerActive?
                    "你已经接下夜班。守卫和两份记录都是真实存在的；警报达到三次将锁住水账，除非击退两名守卫。":
                    "伊沃留下了一只旧灯罩。揭开它，夜班守卫便会巡查远岸。潜行者能在无人伤亡时带走证词；也可以选择战斗。",
                    GlassLedgerActive?
                    "The watch has begun. At alert three the files lock until both actual guards are defeated.":
                    "Ivo left a shuttered lamp. Opening it starts a real night watch. Evade the guards or fight through.",
                    GlassLedgerActive?C("继续调查","Continue investigation","close"):
                    C("开启守夜调查","Begin the night watch","glassledger_begin"),
                    C("离开","Leave","close"));
                return true;
            }
            if(!GlassLedgerActive)
            {
                Dialog(site.name,site.en,
                    "档案员已封闭桌面。回到远岸守夜柱重新启动调查，已有的证据不会被抹去。",
                    "The desk is closed. Restart the investigation at the night watch post; any earned evidence remains.",
                    C("返回守夜柱","Return to watch","close"));
                return true;
            }
            if(!GlassLedgerSecured)
            {
                Dialog(site.name,site.en,
                    "警铃大作。两名真实守卫还在封锁证词，只有击退他们才能继续抄录。",
                    "The alarm sounds. Both live guards block the testimony until actually defeated.",
                    C("另找办法","Find another way","close"));
                return true;
            }
            if(site.id=="glassledger_seal")
            {
                Dialog(site.name,site.en,
                    GlassLedgerFirstEvidence?
                    "配水单写着：‘失踪工人七人，用水份额改为砂车损耗’。下方依旧留着他们的指印。":
                    "账册表面有两层墨迹：七个人的领水份额被改写成七桶‘砂车损耗’。",
                    GlassLedgerFirstEvidence?
                    "The sheet lists seven missing workers as 'cart loss,' but their thumbprints remain.":
                    "Two inks overlap: seven people's rations were rewritten as seven barrels of 'cart loss.'",
                    GlassLedgerFirstEvidence?C("记住线索","Keep the clue","close"):
                    C("抄录涂改配水单","Copy the altered ration slip","glassledger_copy"),
                    C("离开","Leave","close"));
                return true;
            }
            if(site.id=="glassledger_witness")
            {
                if(!GlassLedgerFirstEvidence)
                {
                    Dialog(site.name,site.en,
                        "矿工的便条只写了半个名字。先找到原始配水单，才能辨认其它指印。",
                        "The miner's note is fragmentary. Find the ration slip first.",
                        C("继续寻找","Continue","close"));
                    return true;
                }
                if(!GlassLedgerSecondEvidence)
                {
                    Dialog(site.name,site.en,
                        "‘我们不是沙车。我们在井下修管道，七个人把水送给避难的孩子，姓名却被删掉。’",
                        "'We are not carts. Seven of us repaired the well for the children. Someone erased our names.'",
                        C("抄录无名矿工的证词","Copy the worker's testimony","glassledger_testimony"),
                        C("离开","Leave","close"));
                    return true;
                }
                Dialog(site.name,site.en,
                    "纸页互相印证。究竟是谁偷走了水？是沙暴损毁了水桶，还是账房把七个人写成了货物？",
                    "Both records agree. Was the water lost in a storm, or were seven people written down as cargo?",
                    C("沙暴吞掉了七桶水","Seven barrels lost in a sandstorm","glassledger_storm"),
                    C("七名工人的份额被伪装成损耗","Seven workers' rations were falsely logged as loss","glassledger_truth"),
                    C("稍后判断","Decide later","close"));
                return true;
            }
            return true;
        }
        private bool ChooseGlassLedger(string action)
        {
            if(action!="glassledger_begin"&&action!="glassledger_copy"&&
               action!="glassledger_testimony"&&action!="glassledger_storm"&&
               action!="glassledger_truth"&&action!="glassledger_start"&&
               action!="glassledger_public"&&action!="glassledger_protect")return false;
            WorldObject site=pendingDialogueSource;
            if(site==null||pendingDialogueSourceRoom!=save.room||
               mode!="world"||Dying||practicing||!HasPhysicalInteractionReach(site)||
               !SourceBelongsToActiveMap(site))return true;
            if(action=="glassledger_start")
            {
                if(site.kind!="tinker"||save.room!=6||
                   !GlassLedgerUnlocked||GlassLedgerStarted)return true;
                Flag("glassledger_started");TrackQuest("glassledger");
                revision++;sound="quest";
                Notify("伊沃托你查清无名工人被抹去的配水份额。去远岸旧水站守夜柱。",
                    "Ivo asks you to investigate the erased water rations. Visit the outer night watch.");
                return true;
            }
            if(action=="glassledger_public"||action=="glassledger_protect")
            {
                if(site.kind!="tinker"||save.room!=6||
                   !GlassLedgerReconstructed||GlassLedgerComplete)return true;
                if(save.coins>999939||save.shards>99998)return true;
                bool publish=action=="glassledger_public";
                Flag(publish?"glassledger_public":"glassledger_protected");
                CreditCoins(60);CreditShards(1);AddXp(100);
                if(GlassLedgerQuietRecord)CreditSkillPoints(1);
                ResetGlassLedger();sound="reward";revision++;
                RecordMoment(publish?
                    "伊沃把七名修井工人的名字写上公共配水牌。再没有人能将他们的口粮伪装成沙车损耗。":
                    "伊沃没有公开证人居所。他留下秘密补水标记，让躲避追捕的人仍能喝到水。",
                    publish?
                    "Ivo posted the seven water workers' names for every family to see. Their rations can no longer be disguised as lost cargo.":
                    "Ivo kept the witnesses' addresses private and marked quiet water caches for the displaced.");
                Notify(publish?
                    "公开水账：硬币60、碎片1、经验100。原野外站安全散热加快。":
                    "保护证人：硬币60、碎片1、经验100。原绿洲安全恢复耐力。",
                    publish?
                    "Public roster: +60 coins, 1 shard, 100 XP. Safe cooling at the original field station.":
                    "Witness protection: +60 coins, 1 shard, 100 XP. Safe stamina at the original oasis.");
                return true;
            }
            if(action=="glassledger_begin")
            {
                if(site.id=="glassledger_watch"&&GlassLedgerStarted&&
                   !GlassLedgerReconstructed&&!GlassLedgerActive)
                    if(!StartGlassLedgerWatch())
                        Notify("守夜柱周围有活跃战斗或站位受阻。清理道路后再调查。",
                            "Real guard feet are occupied. Secure the path and retry.");
                return true;
            }
            if(!GlassLedgerActive||!GlassLedgerSecured)return true;
            if(action=="glassledger_copy")
            {
                if(site.id!="glassledger_seal"||GlassLedgerFirstEvidence)return true;
                Flag("glassledger_seal");revision++;sound="quest";
                Notify("真证据一：七人的领水份额被写成‘沙车损耗’。",
                    "Evidence one: seven workers' water was recoded as cart loss.");
                return true;
            }
            if(action=="glassledger_testimony")
            {
                if(site.id!="glassledger_witness"||!GlassLedgerFirstEvidence||
                   GlassLedgerSecondEvidence)return true;
                Flag("glassledger_witness");revision++;sound="quest";
                Notify("真证据二：失踪工人曾主动修井，为孩子送水。",
                    "Evidence two: the missing workers repaired wells for children.");
                return true;
            }
            if(site.id!="glassledger_witness"||!GlassLedgerFirstEvidence||
               !GlassLedgerSecondEvidence||GlassLedgerReconstructed)return true;
            if(action=="glassledger_storm")
            {
                glassLedgerAlarm=Math.Min(3,glassLedgerAlarm+1);
                glassLedgerQuiet=false;revision++;sound="alert";
                Notify("错误推论！字迹和指印证明并非沙暴损毁。巡兵加紧检查。",
                    "Wrong inference: the handwriting proves this was no sandstorm. Guards tighten the search.");
                return true;
            }
            if(action=="glassledger_truth")
            {
                Flag("glassledger_reconstructed");
                if(glassLedgerQuiet&&glassLedgerAlarm==0&&glassLedgerKills==0)
                    Flag("glassledger_clean");
                ResetGlassLedger();revision++;sound="reward";
                RecordMoment(
                    "沙海水账终于被重建：七名修井工人没有偷走水，他们的名字被当成‘砂车损耗’抹掉，以掩盖救援粮水的流向。",
                    "The erased ledger is reconstructed: seven workers did not steal water. Their names became 'cart loss' to hide the relief rations.");
                Notify("真实水账已复原。把两份证词送给伊沃，决定公开还是保护工人。",
                    "The true water ledger is reconstructed. Bring both statements to Ivo for a final choice.");
                return true;
            }
            return true;
        }
        private void AppendGlassLedgerTinkerChoice(WorldObject tinker)
        {
            if(tinker==null||tinker.kind!="tinker"||save.room!=6||
               mode!="dialog"||conversation==null||
               !GlassLedgerReach(tinker)||!GlassLedgerUnlocked)return;
            if(GlassLedgerComplete)
            {
                conversation.text+=GlassLedgerPublic?
                    "\n\n七名修井工人的名字如今写在公共水牌上。他们的功劳没人再能划掉。":
                    "\n\n证人得以匿名，避难者则沿着秘密补水标记安全返回绿洲。";
                conversation.textEn+=GlassLedgerPublic?
                    "\n\nSeven well workers now appear on the public roster. Nobody can erase their work again.":
                    "\n\nWitnesses remain anonymous as displaced families follow safe marks toward water.";
                return;
            }
            if(!GlassLedgerStarted)
            {
                AddGlassLedgerChoice("glassledger_start",
                    "调查远岸被抹去的水账","Investigate the erased desert water ledger");
                return;
            }
            if(!GlassLedgerReconstructed)return;
            AddGlassLedgerChoice("glassledger_public",
                "公布七名修井工人与真实口粮","Publish seven workers and their true rations");
            AddGlassLedgerChoice("glassledger_protect",
                "保护证人 · 建立隐蔽补水标记","Protect witnesses · Mark quiet water caches");
        }
        private void AddGlassLedgerChoice(string action,string zh,string en)
        {
            if(conversation==null||conversation.choices==null)return;
            Choice[] previous=conversation.choices;
            foreach(Choice choice in previous)if(choice.action==action)return;
            int position=previous.Length;
            if(position>0&&previous[position-1].action=="close")position--;
            Choice[] next=new Choice[previous.Length+1];
            Array.Copy(previous,0,next,0,position);
            next[position]=C(zh,en,action);
            Array.Copy(previous,position,next,position+1,
                previous.Length-position);
            conversation.choices=next;
        }
    }
}
