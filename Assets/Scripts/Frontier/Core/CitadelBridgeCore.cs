using System;
using System.Collections.Generic;

namespace Starfall
{
    // Chapter: The Bridge of Broken Chords. A genuine optional Citadel
    // elite, a walkable two-station counter-frequency choice, live delayed
    // attacks and one permanent civic destination for the recovered power.
    // Only original SaveData v2 flag strings outlive the physical encounter.
    public sealed partial class GameCore
    {
        private Enemy bridgeGuardian;
        private MapData bridgeWorld;
        private SaveData bridgeOwner;
        private float bridgeTime,bridgePulse;
        private int bridgePulseCycle,bridgeResonators;
        private bool bridgeKillConfirmed;
        public bool BridgeOfChordsUnlocked
        {get{return LastDirectiveComplete&&CanEnter(4);}}
        public bool BridgeOfChordsDefeated {get{return Has("bridge_chords_defeated");}}
        public bool BridgeOfChordsShelter {get{return Has("bridge_chords_shelter");}}
        public bool BridgeOfChordsGrid {get{return Has("bridge_chords_grid");}}
        public bool BridgeOfChordsComplete
        {get{return BridgeOfChordsShelter||BridgeOfChordsGrid;}}
        public bool BridgeOfChordsCalibrated {get{return Has("bridge_chords_calibrated");}}
        public bool BridgeOfChordsActive
        {get{return bridgeGuardian!=null&&
            save.room==4&&System.Object.ReferenceEquals(bridgeWorld,Map)&&
            System.Object.ReferenceEquals(bridgeOwner,save)&&!Dying;}}
        public bool IsBridgeChordsElite(Enemy e)
        {return e!=null&&bridgeGuardian!=null&&save.room==4&&
            System.Object.ReferenceEquals(e,bridgeGuardian);}
        public float BridgeOfChordsSeconds
        {get{return BridgeOfChordsActive?bridgeTime:0f;}}
        public int BridgeOfChordsResonators
        {get{return BridgeOfChordsActive?bridgeResonators:0;}}
        public float BridgeOfChordsPulse
        {get{return BridgeOfChordsActive?bridgePulse:0f;}}
        public Point BridgeOfChordsTarget
        {
            get
            {
                string name=BridgeOfChordsDefeated?"bridge_heart":
                    BridgeOfChordsActive&&bridgeResonators==0?"bridge_low":
                    BridgeOfChordsActive&&bridgeResonators==1?"bridge_high":
                    "bridge_heart";
                WorldObject o=BridgeChordsSite(name);
                return o==null?new Point(45.5f,37.5f):new Point(o.x,o.y);
            }
        }
        public string BridgeOfChordsObjective
        {get{return BridgeOfChordsComplete?
            BridgeOfChordsShelter?
                L("断弦桥民间避难灯已接通","PUBLIC BRIDGE REFUGE POWERED"):
                L("断弦桥道路电网已恢复","SAFE BRIDGE GRID RESTORED"):
            BridgeOfChordsDefeated?L("返回余震核心，决定电力归属",
                "RETURN TO THE ECHO CORE · ALLOCATE POWER"):
            BridgeOfChordsActive?
                bridgeResonators==0?L("压下低频音叉，或直接击败真身",
                    "GROUND LOW FORK OR FIGHT THE TRUE ELITE"):
                bridgeResonators==1?L("穿过断桥激活高频接地桩，或直接击败精英",
                    "REACH HIGH GROUND OR DEFEAT THE ELITE"):
                L("共鸣已经切断，击败真正的桥上精英",
                    "RESONANCE BROKEN · DEFEAT THE LIVING ELITE"):
            L("探索堡垒旧桥，触碰余震核心","ENTER CITADEL OLD BRIDGE · EXAMINE ECHO CORE");}}
        private WorldObject BridgeChordsSite(string id)
        {
            foreach(WorldObject o in data.maps[4].objects)
                if(o.id==id)return o;
            return null;
        }
        private bool BridgeChordsSource(WorldObject o)
        {return o!=null&&save.room==4&&
            SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o)&&
            CanStand(Position);}
        private void OnBridgeChordsEnemyDefeated(Enemy enemy)
        {if(BridgeOfChordsActive&&IsBridgeChordsElite(enemy))
            bridgeKillConfirmed=true;}
        private void ResetBridgeOfChords()
        {
            if(bridgeGuardian!=null)
            {
                RemoveEnemyPendingWarning(bridgeGuardian);
                enemies.Remove(bridgeGuardian);
                hazards.RemoveAll(h=>System.Object.ReferenceEquals(
                    h.bossOwner,bridgeGuardian));
            }
            bridgeGuardian=null;bridgeWorld=null;bridgeOwner=null;
            bridgeTime=bridgePulse=0f;bridgePulseCycle=bridgeResonators=0;
            bridgeKillConfirmed=false;
        }
        private bool StartBridgeOfChords()
        {
            Point spawn=new Point(49.5f,39.5f);
            if(!BridgeOfChordsUnlocked||BridgeOfChordsDefeated||
               BridgeOfChordsActive||bridgeGuardian!=null||
               save.room!=4||mode!="world"||Dying||practicing||racing||
               !CanStand(Position)||!CanStand(spawn)||
               (Position-spawn).Length<2.2f||
               enemies.Exists(e=>e!=null&&e.hp>0f&&
                   (e.Position-spawn).Length<1.8f))return false;
            bridgeWorld=Map;bridgeOwner=save;
            bridgeTime=125f;bridgePulse=3.0f;bridgePulseCycle=0;
            bridgeResonators=0;bridgeKillConfirmed=false;
            bridgeGuardian=CreatePlacedEnemy(++enemyId,2,spawn,320f,true);
            bridgeGuardian.timer=1.0f;
            enemies.Add(bridgeGuardian);
            TrackQuest("bridge_chords");
            sound="windup";revision++;
            effects.Add(new Effect("enemyRelease",spawn,.6f)
                {radius=1.2f,variant=0});
            RecordMoment(
                "星陨堡垒旧桥上，一台被废弃的余震核心仍把过路人的脚步识别成入侵；两枚散落的音叉可以削弱它的反击，但无人必须冒险去碰。",
                "On the Citadel's broken bridge, an abandoned echo engine still mistakes footfalls for intruders. Two physical tuning forks can damp its strikes, but nobody has to risk reaching them.");
            Notify("断弦桥精英已苏醒！观察延迟脉冲，或亲身靠近两座音叉接地削弱它。",
                "Bridge elite awakened! Dodge delayed pulses, or ground the two actual forks to suppress it.");
            return true;
        }
        private bool BridgeChordsEscapeAvailable(Point target)
        {
            Point[] exits={new Point(2f,0),new Point(-2f,0),
                new Point(0,2f),new Point(0,-2f)};
            foreach(Point off in exits)
                if(CanStand(target+off)&&ClearAttackPath(target,target+off))
                    return true;
            return false;
        }
        // A half-health paired pulse can only be scheduled when a genuine
        // walkable dodge exit remains clear of BOTH danger circles. A cell
        // that is merely walkable but covered by the second blast is not
        // an honest escape route on the original narrow Citadel bridge.
        private bool BridgeChordsPairedEscape(Point target,Point second,
            float secondaryRadius)
        {
            Point[] paths={new Point(2f,0f),new Point(-2f,0f),
                new Point(0f,2f),new Point(0f,-2f)};
            foreach(Point delta in paths)
            {
                Point exit=target+delta;
                if(CanStand(exit)&&ClearAttackPath(target,exit)&&
                   (exit-second).Length>secondaryRadius+.45f)
                    return true;
            }
            return false;
        }
        private void TickBridgeOfChords(float dt)
        {
            if(bridgeGuardian==null)
            {
                if(!BridgeOfChordsComplete||save.room!=4||Dying||
                   !SafeTravel||FieldWorkPreventsRest)return;
                Point heart=BridgeOfChordsTarget;
                if(!PhysicalRefugeReach(heart,2.1f))return;
                if(BridgeOfChordsShelter)
                    save.health=Math.Min(save.maxHealth,save.health+2.0f*dt);
                else if(BridgeOfChordsGrid)
                    save.energy=Math.Min(100f,save.energy+3.5f*dt);
                return;
            }
            if(!BridgeOfChordsActive||practicing)
            {ResetBridgeOfChords();return;}
            if(!enemies.Contains(bridgeGuardian)||bridgeGuardian.hp<=0f)
            {
                bool genuine=bridgeKillConfirmed;
                int grounded=bridgeResonators;
                ResetBridgeOfChords();
                if(!genuine)
                {
                    Notify("断弦桥守卫异常离场，没有确认真实击败；可免费重试。",
                        "Elite vanished without a real lethal combat hit. Retry without false credit.");
                    return;
                }
                Flag("bridge_chords_defeated");
                if(grounded==2)Flag("bridge_chords_calibrated");
                revision++;sound="reward";
                RecordMoment(
                    "断弦桥的最后一声金属颤音终于停止。旧防卫机把每个过桥者当成敌人，但音叉记下了人们曾试图让路畅通。",
                    "The bridge's last metallic chord falls silent. Its defense engine treated every crossing as a threat; the forks record attempts to open the road instead.");
                Notify("真正的桥上精英已被击败！返回余震核心决定能量留给谁。",
                    "The real bridge elite fell. Return to the core to choose where the power goes.");
                return;
            }
            bridgeTime-=dt;
            if(bridgeTime<=0f)
            {
                ResetBridgeOfChords();revision++;
                Notify("旧桥共鸣反冲重新封闭。精英仍在，可重新触发战斗。",
                    "The bridge engine resealed. The elite can be challenged again.");
                return;
            }
            bridgePulse-=dt;
            if(bridgePulse>0f)return;
            Point target=Position;
            if(BridgeChordsEscapeAvailable(target))
            {
                hazards.Add(new Hazard {
                    position=target,radius=1.06f,timer=1.3f,
                    damage=bridgeGuardian.BossPhase>=2?20:14,
                    kind="rift",bossOwner=bridgeGuardian
                });
                if(bridgeGuardian.BossPhase>=2&&bridgeResonators<2)
                {
                    Point side=target+new Point(
                        bridgePulseCycle%2==0?2.4f:-2.4f,0f);
                    if(CanStand(side)&&BridgeChordsEscapeAvailable(side)&&
                       BridgeChordsPairedEscape(target,side,.82f))
                        hazards.Add(new Hazard {
                            position=side,radius=.82f,timer=1.55f,
                            damage=12,kind="rift",bossOwner=bridgeGuardian
                        });
                }
                sound="windup";revision++;
                Notify("真实桥面脉冲已标出落点！离开红圈，或在命中前闪避。",
                    "Live bridge surge telegraphed! Leave the red circle or dodge before impact.");
            }
            bridgePulseCycle++;
            bridgePulse=bridgeResonators==2?7.6f:
                bridgeResonators==1?5.6f:
                bridgeGuardian.BossPhase>=2?3.3f:4.4f;
        }
        private string BridgeOfChordsInnNews(bool english)
        {
            if(BridgeOfChordsShelter)return english?
                "\n\nA bridge carpenter brings a brass resonance fork to the Hearth Inn, still warm from the old crossing. 'The machine used to strike every living footstep,' she says. 'Now it keeps a lamp burning for anybody too hurt to finish the road.' The innkeeper leaves blankets beside the notice.":
                "\n\n一位修桥木匠把还带余温的铜音叉放在炉火旅店。‘以前机器听见脚步就打人，’她说，‘现在它给走不动的伤者留一盏灯。’店主在公告旁添了几条毛毯。";
            if(BridgeOfChordsGrid)return english?
                "\n\nThe old bridge's power is back. A road worker draws a fresh crossing map on the Hearth Inn table. 'Nobody needs permission from a dead sentry to return home,' he says. The recovered grid has put warm current into the safety rails.":
                "\n\n断弦桥的供电终于恢复。修路工在炉火旅店桌上重新画出归乡地图：‘回家不该再向一台死去的守卫申请许可。’桥上的扶手也重新有了暖流。";
            return "";
        }
        private bool TalkBridgeOfChords(WorldObject site)
        {
            if(site==null||!(site.kind=="bridgeheart"||
                site.kind=="bridgelow"||site.kind=="bridgehigh"))return false;
            if(!BridgeChordsSource(site))return true;
            if(!BridgeOfChordsUnlocked)
            {
                Dialog(site.name,site.en,
                    "星陨堡垒深处的最后电网还在保密封锁中。先完成旧堡垒的最终指令。",
                    "The final Citadel grid remains locked. Finish the Last Directive first.",
                    C("离开","Leave","close"));
                return true;
            }
            if(BridgeOfChordsComplete)
            {
                Dialog(site.name,site.en,
                    BridgeOfChordsShelter?
                        "公开避难灯已经亮起。过桥的人终于不必再在红圈里辨认敌我。":
                        "道路电网恢复了，工人能安全修好断桥上的供能线路。",
                    BridgeOfChordsShelter?
                        "The public refuge lamp is lit. Travelers need no longer be mistaken for intruders.":
                        "The bridge grid is repaired; workers can restore power to the crossing.",
                    C("离开","Leave","close"));
                return true;
            }
            if(site.kind=="bridgeheart")
            {
                if(BridgeOfChordsDefeated)
                    Dialog(site.name,site.en,
                        "余震核心安静了。那些被系统标记为入侵者的旅人，需要的是一盏公开的避难灯，还是一条不断电的回乡道路？",
                        "The core is quiet. Should the recovered power serve a public refuge lamp or restore the homeward road grid?",
                        C("建立公共避难灯 · 安全时疗伤",
                            "Power public refuge · heal safely","bridge_chords_shelter"),
                        C("修复道路电网 · 安全时回气",
                            "Restore road grid · regain stamina safely","bridge_chords_grid"),
                        C("稍后再决定","Decide later","close"));
                else if(BridgeOfChordsActive)
                    Dialog(site.name,site.en,
                        "余震核心还在作战。桥上两枚真实音叉能改变警报频率；先低频，后高频，或者直接击败精英。",
                        "The echo core is fighting. Real forks can retune its pulses: low first, then high—or defeat the elite directly.",
                        C("返回战场","Return to the bridge","close"));
                else
                    Dialog(site.name,site.en,
                        "旧桥的自动守卫误把桥上行人当成入侵者。你可以直接迎战，也可以穿越桥面，在战斗中依次接地低频和高频音叉，降低脉冲频率。",
                        "The bridge's defense engine misidentifies travelers. Fight its live elite outright, or cross the arena to ground two physical forks during combat and slow its pulses.",
                        C("启动断弦桥遭遇","Awaken the bridge sentinel","bridge_chords_start"),
                        C("稍后探索","Later","close"));
                return true;
            }
            if(!BridgeOfChordsActive)
            {
                Dialog(site.name,site.en,
                    "音叉未通电，只有启动余震核心后才能在战斗中接地。",
                    "The fork is unpowered until the echo core awakens.",
                    C("离开","Leave","close"));
                return true;
            }
            bool low=site.kind=="bridgelow";
            bool ready=low?bridgeResonators==0:bridgeResonators==1;
            Dialog(site.name,site.en,
                ready?(low?
                    "低音叉振出三个短音。当前可花费12耐力接地，打断精英并减慢之后的脉冲。":
                    "高频桩补上最后一声长音。当前可花费12耐力接地，进一步拖长精英脉冲的间隔。"):
                    "必须先让低频接地，再补高频；已接地的音叉不会重复收取耐力。",
                ready?(low?
                    "Three short notes from the low fork. Spend 12 stamina to disrupt the elite and slow future pulses.":
                    "One long note from the high ground. Spend 12 stamina to lengthen the live surge interval again."):
                    "Ground the low fork before the high ground; already-used stations never charge twice.",
                ready?C(low?"接地低频 · 耐力12":"接地高频 · 耐力12",
                    low?"Ground low · 12 stamina":"Ground high · 12 stamina",
                    low?"bridge_chords_low":"bridge_chords_high"):
                    C("暂时继续战斗","Keep fighting","close"),
                C("离开","Leave","close"));
            return true;
        }
        private bool ChooseBridgeOfChords(string action)
        {
            if(action!="bridge_chords_start"&&action!="bridge_chords_low"&&
               action!="bridge_chords_high"&&action!="bridge_chords_shelter"&&
               action!="bridge_chords_grid")return false;
            WorldObject source=pendingDialogueSource;
            if(source==null||pendingDialogueSourceRoom!=save.room||
               mode!="world"||Dying||practicing||racing||
               !BridgeChordsSource(source))return true;
            if(action=="bridge_chords_start")
            {
                if(source.id=="bridge_heart"&&
                   !StartBridgeOfChords())
                    Notify("核心周围仍有战斗或真实守卫站位冲突；先腾出安全场地。",
                        "The true arena or spawn feet are occupied. Secure the bridge and retry.");
                return true;
            }
            if(action=="bridge_chords_low"||action=="bridge_chords_high")
            {
                int need=action=="bridge_chords_low"?0:1;
                if(!BridgeOfChordsActive||bridgeResonators!=need||
                   source.id!=(need==0?"bridge_low":"bridge_high")||
                   save.energy<12f||bridgeTime<=2f||
                   bridgeGuardian.hp<=0f||!enemies.Contains(bridgeGuardian))
                    return true;
                save.energy-=12f;
                bridgeResonators++;bridgePulse=Math.Max(bridgePulse,3.4f);
                ApplyEnemyControl(bridgeGuardian,3.2f);
                effects.Add(new Effect("discover",new Point(source.x,source.y),.65f)
                    {radius=.85f});
                sound="reward";revision++;
                Notify(need==0?
                    "低频共鸣已接地：真正的精英被打断，桥面脉冲减慢！":
                    "高频共鸣接地完成：精英暂时停机，后续落点间隔更长。",
                    need==0?
                    "Low fork grounded: true elite staggered and pulse rate reduced!":
                    "High fork grounded: the elite stalls and further pulses slow.");
                return true;
            }
            if(source.id!="bridge_heart"||!BridgeOfChordsDefeated||
               BridgeOfChordsComplete||bridgeGuardian!=null)return true;
            if(save.coins>999909||save.shards>99997)return true;
            bool shelter=action=="bridge_chords_shelter";
            Flag(shelter?"bridge_chords_shelter":"bridge_chords_grid");
            CreditCoins(90);CreditShards(2);AddXp(130);
            if(BridgeOfChordsCalibrated)CreditSkillPoints(1);
            sound="reward";revision++;
            RecordMoment(shelter?
                "断弦桥的旧防卫机不再锁定行人。旅人可以在核心旁的公共灯下疗伤，夜间也不必向机器证明自己不是敌人。":
                "断弦桥的共鸣被重新分配给道路电网。修桥的工匠把回乡的路接通，让走远路的人在核心旁安全喘息。",
                shelter?
                "The echo engine no longer targets bridge travelers. Everyone can rest beneath a public lamp without proving themselves harmless.":
                "The echo energy now powers the homeward crossing. Bridge workers keep the road grid alive for the exhausted.");
            Notify((shelter?"断弦桥公共避难灯已点亮；核心安全区域生命恢复。":
                    "断弦桥供能线路已恢复；核心安全区域耐力恢复。")+
                    (BridgeOfChordsCalibrated?" 双音叉专精额外技能点+1。":""),
                (shelter?"Public bridge refuge: safe healing at the core.":
                    "Bridge road grid: safe stamina at the core.")+
                    (BridgeOfChordsCalibrated?" Two forks mastered: +1 talent.":""));
            return true;
        }
    }
}
