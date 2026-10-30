using System;
using System.Collections.Generic;

namespace Starfall
{
    // A genuinely playable second act through the shipped regional maps.
    // All milestones are existing SaveData v2 flags; existing completed
    // four-chapter saves remain complete and their ending is never rewritten.
    public sealed partial class GameCore
    {
        public bool MainSagaActive { get { return Has("saga_act_two"); } }
        public int MainSagaVoices
        {
            get { int total=0;for(int i=5;i<=7;i++)
                if(Has("saga_voice_"+i))total++;return total; }
        }
        public int MainSagaTraces
        {
            get { int total=0;for(int i=5;i<=7;i++)
                if(Has("saga_trace_"+i))total++;return total; }
        }
        public bool MainSagaCouncilReady
        {
            get { return MainSagaActive&&Has("boss_4")&&
                MainSagaVoices==3&&MainSagaTraces==3&&
                Proofs==3&&save.frontierEnding>0; }
        }
        public bool MainSagaCanConclude
        {
            get { return MainSagaCouncilReady&&Has("saga_lastgate_complete"); }
        }
        public int MainSagaLastGateStep
        {
            get {
                if(!MainSagaCouncilReady)return 0;
                if(!Has("saga_lastgate_ledger"))return 1;
                if(!Has("saga_lastgate_relay"))return 2;
                if(!Has("saga_lastgate_complete"))return 3;
                return 4;
            }
        }
        public Point MainSagaLastGateTarget
        {
            get {
                int next=Math.Min(3,Math.Max(1,MainSagaLastGateStep));
                WorldObject node=MainSagaLastGateNode(next);
                if(MainSagaRelayActive)
                {
                    foreach(Enemy foe in sagaRelayFoes)
                        if(foe!=null&&foe.hp>0&&enemies.Contains(foe))
                            return foe.Position;
                }
                return node!=null?new Point(node.x,node.y):Position;
            }
        }
        private WorldObject MainSagaLastGateNode(int index)
        {
            if(index<1||index>3)return null;
            foreach(WorldObject o in data.maps[4].objects)
                if(o!=null&&o.id=="triadmemory"+index&&
                    o.kind=="triadmemory"+index)return o;
            return null;
        }

        // The last bypass is a live battle, not an instant stamina receipt.
        // This encounter is session-only: an interrupted fight never writes
        // progress, while a previously earned v2 relay flag remains valid.
        private int sagaRelayRoom=-1;
        private SaveData sagaRelaySave;
        private MapData sagaRelayMap;
        private WorldObject sagaRelayStation;
        private float sagaRelayClock;
        private bool sagaRelayLowTimeWarned,sagaRelayVentUsed;
        private float sagaRelayHeat;
        private readonly List<Enemy> sagaRelayFoes=new List<Enemy>();
        private readonly HashSet<Enemy> sagaRelayDefeated=new HashSet<Enemy>();
        public bool MainSagaRelayActive
        {
            get { return sagaRelayRoom==4&&save.room==4&&
                Object.ReferenceEquals(sagaRelaySave,save)&&
                Object.ReferenceEquals(sagaRelayMap,Map)&&
                Object.ReferenceEquals(sagaRelayStation,MainSagaLastGateNode(2))&&
                SourceBelongsToActiveMap(sagaRelayStation); }
        }
        public int MainSagaRelayRemaining
        {
            get { int total=0;
                if(MainSagaRelayActive)foreach(Enemy e in sagaRelayFoes)
                    if(e!=null&&e.hp>0&&enemies.Contains(e))total++;
                return total;
            }
        }
        public float MainSagaRelaySeconds
        { get { return MainSagaRelayActive?sagaRelayClock:0f; } }
        public int MainSagaRelayIntegrity
        { get { return MainSagaRelayActive?Math.Max(0,100-(int)Math.Ceiling(sagaRelayHeat)):100; } }
        public bool MainSagaRelayVentReady
        { get { return MainSagaRelayActive&&!sagaRelayVentUsed&&
            sagaRelayHeat>=8f; } }
        private void ResetMainSagaRelay()
        {
            foreach(Enemy e in sagaRelayFoes)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);
                regionalAmbientEnemies.Remove(e);
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,e));
            }
            sagaRelayRoom=-1;sagaRelaySave=null;sagaRelayMap=null;
            sagaRelayStation=null;sagaRelayClock=0f;
            sagaRelayLowTimeWarned=false;sagaRelayVentUsed=false;
            sagaRelayHeat=0f;
            sagaRelayFoes.Clear();sagaRelayDefeated.Clear();
        }
        private bool MainSagaRelayFootFree(Point foot,List<Point> placed)
        {
            if(!CanStand(foot)||(foot-Position).Length<4f||
                NavigationPathFrom(Position,foot,true).Count==0)return false;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&(foot-new Point(o.x,o.y)).Length<2.3f)return false;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0&&(e.Position-foot).Length<2.8f)return false;
            foreach(Point p in placed)
                if((p-foot).Length<4.5f)return false;
            return true;
        }
        private bool StartMainSagaRelay(WorldObject station)
        {
            if(MainSagaRelayActive||station==null)return false;
            var feet=new List<Point>();
            int cx=(int)Math.Floor(station.x),cy=(int)Math.Floor(station.y);
            // Deterministically choose TWO actual walkable approaches in the
            // shipped Citadel; no teleported kills or fabricated casualties.
            for(int radius=6;radius<=19&&feet.Count<2;radius++)
            for(int dy=-radius;dy<=radius&&feet.Count<2;dy++)
            for(int dx=-radius;dx<=radius&&feet.Count<2;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point foot=new Point(cx+dx+.5f,cy+dy+.5f);
                if(MainSagaRelayFootFree(foot,feet))feet.Add(foot);
            }
            if(feet.Count!=2)return false;
            sagaRelayRoom=4;sagaRelayMap=Map;sagaRelaySave=save;
            sagaRelayStation=station;sagaRelayClock=100f;
            sagaRelayHeat=0f;sagaRelayVentUsed=false;sagaRelayLowTimeWarned=false;
            for(int i=0;i<feet.Count;i++)
            {
                int kind=i==0?1:2;
                Enemy foe=CreatePlacedEnemy(++enemyId,kind,feet[i],78f+i*24f);
                foe.stun=Math.Max(foe.stun,.65f);
                sagaRelayFoes.Add(foe);enemies.Add(foe);
                effects.Add(new Effect("enemyRelease",foe.Position,.6f)
                    {variant=kind});
            }
            return true;
        }
        private void OnMainSagaRelayEnemyDefeated(Enemy enemy)
        {
            if(MainSagaRelayActive&&sagaRelayFoes.Contains(enemy))
                sagaRelayDefeated.Add(enemy);
        }
        private void TickMainSagaRelay(float dt)
        {
            if(sagaRelayRoom<0)return;
            if(!MainSagaRelayActive||Dying||practicing||
                !MainSagaCouncilReady||save.ending>0||
                Has("saga_lastgate_relay"))
            { ResetMainSagaRelay();return; }
            // The defeat hook belongs to real Hit() combat. Removing enemies,
            // reducing HP outside combat, or crossing a scene is not victory.
            foreach(Enemy e in sagaRelayFoes)
                if(e==null||(!sagaRelayDefeated.Contains(e)&&
                    (!enemies.Contains(e)||e.hp<=0)))
                {
                    ResetMainSagaRelay();revision++;
                    Notify("破坏者异常离场，旁路未能稳定。重新调谐后再守住线路。",
                        "An unverified saboteur vanished. Restart the bypass defense.");
                    return;
                }
            bool clear=sagaRelayFoes.Count==2;
            foreach(Enemy e in sagaRelayFoes)
                if(!sagaRelayDefeated.Contains(e)||e.hp>0||
                    enemies.Contains(e)){clear=false;break;}
            if(clear)
            {
                ResetMainSagaRelay();
                Flag("saga_lastgate_relay");
                RecordMoment("终章 · 亲自重接堡垒应急旁路，并在真实战斗中击退了两名破坏供能的追兵。",
                    "Finale: retuned the citadel bypass and defeated two real saboteurs before the civilian circuit held.");
                effects.Add(new Effect("burst",new Point(23.5f,6.5f),.7f)
                    {radius=3f,variant=2});
                revision++;sound="quest";
                Notify("两名破坏者已被击退，旁路稳定！前往最后的居民签名台。",
                    "Both saboteurs defeated. The bypass holds! Reach the final signing station.");
                return;
            }
            if(dt<=0f||mode!="world")return;
            if((Position-new Point(sagaRelayStation.x,sagaRelayStation.y)).Length>28f)
            {
                ResetMainSagaRelay();revision++;
                Notify("你离开了供能防线，旁路保护已撤销。可返回重新启动。",
                    "You left the bypass perimeter. Return to restart the defense.");
                return;
            }
            // Saboteurs must physically contest the circuit. An attacker
            // that is dead, stunned, walled off or too far away cannot heat it.
            // Threat pressure changes only in actual world time. The small
            // slice also bounds a dropped frame instead of instantly wiping
            // out a player's entire relay defense.
            int nearby=0;
            Point circuit=new Point(sagaRelayStation.x,sagaRelayStation.y);
            foreach(Enemy e in sagaRelayFoes)
                if(e!=null&&e.hp>0&&enemies.Contains(e)&&
                    e.stun<=0f&&e.stagger<=0f&&e.hitTime<=0f&&
                    CanStand(e.Position)&&
                    (e.Position-circuit).Length<=5.5f&&
                    ClearAttackPath(e.Position,circuit))nearby++;
            float slice=Math.Min(.1f,dt);
            sagaRelayHeat=Math.Max(0f,Math.Min(100f,sagaRelayHeat+
                slice*(nearby>0?nearby*8f:-3.5f)));
            if(sagaRelayHeat>=100f)
            {
                ResetMainSagaRelay();revision++;sound="alert";
                Notify("破坏者逼近供能线圈，旁路过热中断！重新启动后可以再次防守。",
                    "The saboteurs overloaded the coils! Restart the bypass defense.");
                return;
            }
            sagaRelayClock=Math.Max(0f,sagaRelayClock-dt);
            if(sagaRelayClock<=0f)
            {
                ResetMainSagaRelay();revision++;
                Notify("线圈过热，旁路未能保持稳定。冷却后可重新挑战。",
                    "The coils overheated. Restart the bypass defense when ready.");
                return;
            }
            if(sagaRelayClock<=30f&&!sagaRelayLowTimeWarned)
            {
                sagaRelayLowTimeWarned=true;
                effects.Add(new Effect("number",
                    new Point(sagaRelayStation.x,sagaRelayStation.y),1.2f,
                    L("旁路即将过热！","BYPASS OVERHEATING!")));
                revision++;sound="alert";
                Notify("应急旁路只剩30秒！立即击退破坏者。",
                    "Bypass has only 30 seconds left! Defeat the saboteurs now.");
            }
        }

        private bool TryTalkMainSagaLastGate(WorldObject o)
        {
            if(!MainSagaCouncilReady||save.ending>0||save.room!=4||
                o==null||!SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o))return false;
            int stage=MainSagaLastGateStep;
            if(stage>3)return false; // Original memory sites retain their purpose.
            WorldObject needed=MainSagaLastGateNode(stage);
            if(needed==null||!Object.ReferenceEquals(needed,o))return false;
            if(stage==1)
                Dialog(o.name,o.en,
                    "堡垒留下三道相互矛盾的供能注记。第一道写着“先保墙内”；第二道说“守护通路”；第三道竟没有记录任何接受决定的居民签名。\n\n你将三份远岸证词和九年前被擦去的命令放在一起，才看清这并非一位将军留下的错误，而是一套从未允许外环居民反对的制度。\n\n莉雅问：“我们能不能先把签名缺失这一栏保留给真正受影响的人？”",
                    "Three contradictory supply notes remain. The first says PROTECT THE WALL; the second PRESERVE THE ROUTES. The third contains no resident signature at all.\n\nSet beside the farshore testimonies, they reveal not one commander's mistake but a system that never allowed outer-ring residents to object.\n\nLyra asks: “Can we reserve that blank line for the people who actually bear the cost?”",
                    C("核对三份供词并封存旧命令","Verify the accounts and secure the order","saga_lastgate_1"),
                    C("返回查看","Return later","close"));
            else if(stage==2)
                if(MainSagaRelayActive)
                    Dialog(o.name,o.en,
                        "旁路调谐中！仍有"+MainSagaRelayRemaining+
                        "名破坏者，线圈可维持"+(int)Math.Ceiling(MainSagaRelaySeconds)+
                        "秒，稳定度"+MainSagaRelayIntegrity+
                        "%。敌人靠近线路会使线圈过热。"+(sagaRelayVentUsed?"应急泄压已用过。":"可在危险时手动泄压一次。"),
                        "Bypass under attack! "+MainSagaRelayRemaining+
                        " saboteurs remain, "+(int)Math.Ceiling(MainSagaRelaySeconds)+
                        " seconds, integrity "+MainSagaRelayIntegrity+
                        "%. Raiders beside the circuit overheat the coils."+
                        (sagaRelayVentUsed?" The emergency vent has been used.":" You may vent once."),
                        MainSagaRelayVentReady?
                            C("应急泄压 · 耐力12，降低过热","Vent coils · 12 stamina, reduce heat","saga_lastgate_vent"):
                            C("继续守住旁路","Hold the bypass","close"),
                        C("继续战斗","Return to battle","close"));
                else
                    Dialog(o.name,o.en,
                        "损坏的分流台仍把星核能量优先送往军用屏障。你必须消耗22耐力启动紧急旁路，再亲自击退两名破坏者；100秒内守住线路。敌人靠近装置会使供能线圈过热，你可以消耗12耐力应急泄压一次。\n\n诺尔说：“别让新的命令只停留在纸上。”",
                        "The broken diverter still prioritizes the military shield. Spend 22 stamina to start the bypass, then defeat two real saboteurs within 100 seconds. Enemies near the circuit overheat the coils; you may spend 12 stamina to vent them once.\n\nNoll says, “Don't let the new order remain on paper.”",
                        C("启动旁路并守卫 · 耐力22","Start and defend bypass · 22 stamina","saga_lastgate_2"),
                        C("稍后再试","Return when ready","close"));
            else
                Dialog(o.name,o.en,
                    "第三份空白命令只剩两个不完美的方案：公开所有调度记录，让每一笔能源消耗都能被质疑；或让受难家属先核对身份，把敏感的安置地点从公开名册中遮住。\n\n公开可以防止新的秘密配额，却可能暴露正在躲避追捕的人。保护隐私能让幸存者安心，却要求议会承担监督责任。\n\n你不会替所有人决定如何生活，但必须决定这张新的表格如何开始。",
                    "The last unsigned order admits no perfect solution. Publish the complete energy ledger for public challenge, or let affected families check their identities first while shielding the locations of people still in danger.\n\nTransparency may stop the next secret ration but expose hidden survivors. Privacy protects them, yet places greater scrutiny on the council.\n\nYou cannot choose a future for everyone. You must choose how its first record will be kept.",
                    C("公开供能账本，允许居民监督","Publish energy accounts for public challenge","saga_lastgate_public"),
                    C("先保护幸存者身份，由居民议会审核","Protect survivors; entrust review to residents","saga_lastgate_private"),
                    C("我还需要想一想","I need to think","close"));
            return true;
        }
        private bool ChooseMainSagaLastGate(string action)
        {
            if(string.IsNullOrEmpty(action)||!action.StartsWith("saga_lastgate_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            int stage=action=="saga_lastgate_1"?1:
                action=="saga_lastgate_2"||action=="saga_lastgate_vent"?2:
                action=="saga_lastgate_public"||action=="saga_lastgate_private"?3:0;
            if(stage==0||!MainSagaCouncilReady||save.ending>0||Dying||
                practicing||save.room!=4||mode!="world"||
                stage!=MainSagaLastGateStep||source==null||
                !Object.ReferenceEquals(source,MainSagaLastGateNode(stage))||
                pendingDialogueSourceRoom!=4||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source)||!CanStand(Position))
                return true;
            if(stage==2)
            {
                if(action=="saga_lastgate_vent")
                {
                    if(!MainSagaRelayVentReady)return true;
                    if(save.energy<12f)
                    {
                        Notify("应急泄压需要12耐力。当前能量不足，仍可直接击退敌人。",
                            "Emergency vent needs 12 stamina. Defeat attackers instead.");
                        return true;
                    }
                    save.energy-=12f;
                    sagaRelayHeat=Math.Max(0f,sagaRelayHeat-45f);
                    sagaRelayVentUsed=true;
                    effects.Add(new Effect("burst",
                        new Point(source.x,source.y),.6f){radius=2f,variant=2});
                    revision++;sound="guard";
                    Dialog(source.name,source.en,
                        "你用自己的力量扳动泄压阀。线圈降温，备用阀门已锁死；这次保卫战不能再重复泄压。",
                        "You force open the emergency vent. The coils cool, but the backup valve locks. You cannot vent again this attempt.",
                        C("返回守卫","Return to defense","close"));
                    return true;
                }
                if(save.energy<22f)
                {
                    Notify("旁路阀门需要22耐力。先安全恢复，再回来操作。",
                        "The bypass needs 22 stamina. Recover safely and return.");
                    return true;
                }
                if(MainSagaRelayActive)return true;
                if(!StartMainSagaRelay(source))
                {
                    Notify("附近找不到安全且可到达的破坏者进路。请清理道路后重试，不消耗耐力。",
                        "No safe reachable saboteur route. Clear the approach and retry; no stamina spent.");
                    return true;
                }
                save.energy-=22f;
            }
            else if(stage==1)
            {
                Flag("saga_lastgate_ledger");
                RecordMoment("终章 · 将三份原始证词和九年前的旧配额放在同一张桌上。决策者无法再藏在空白签名之后。",
                    "Finale: the original accounts confront the nine-year-old ration order. Decision makers can no longer hide behind an unsigned line.");
            }
            else
            {
                Flag(action=="saga_lastgate_public"?
                    "saga_lastgate_open":"saga_lastgate_private");
                Flag("saga_lastgate_complete");
                CreditShards(3);
                RecordMoment(action=="saga_lastgate_public"?
                    "终章 · 居民议会签署公开账本，任何人都可以质疑每次调度。":
                    "终章 · 居民议会先保护受难者的安置位置，并设立可追责的私人审阅程序。",
                    action=="saga_lastgate_public"?
                    "Finale: the council publishes an inspectable power ledger.":
                    "Finale: the council protects refugee locations while opening accountable private review.");
            }
            revision++;sound="reward";
            Dialog(source.name,source.en,
                stage==1?"三份记录已核对。走向堡垒东面的旧分流台，让新协定真正接管供能线路。":
                stage==2?"旁路已启动，破坏者逼近！只有亲自击退两名敌人，才可前往最后的签名台。":
                "最后一道命令已由居民共同见证。回到厄洛所在的祭坛，决定星核的最终归宿。",
                stage==1?"The accounts are verified. Reach the eastern diverter to make the covenant physically enforceable.":
                stage==2?"The bypass is charging and saboteurs are closing in! Defeat both attackers before visiting the final signing station.":
                "The last order has been witnessed by the residents. Return to Orrin's shrine and choose the cores' fate.",
                C("继续主线","Continue the main story","close"));
            return true;
        }

        private static string SagaPerson(int room)
        { return room==5?"captain":room==6?"tinker":room==7?"keeper":""; }
        private static string SagaPersonId(int room)
        { return room==5?"mira":room==6?"ivo":room==7?"sen":""; }
        private static string SagaReport(int room)
        { return room==5?"harbor_report":room==6?"desert_report":
            room==7?"mountain_report":""; }
        private static string SagaEvidenceId(int room)
        { return room==5?"passengers":room==6?"receiver":
            room==7?"bell2":""; }
        public Point MainSagaEvidencePoint(int room)
        {
            if(room<5||room>7)return Position;
            foreach(WorldObject objectAt in data.maps[room].objects)
                if(objectAt!=null&&objectAt.id==SagaEvidenceId(room))
                    return new Point(objectAt.x,objectAt.y);
            return new Point(data.maps[room].spawnX,data.maps[room].spawnY);
        }
        private static string SagaOuterId(int room)
        { return room>=5&&room<=7?"outer_"+room+"_0":""; }
        public Point MainSagaOuterPoint(int room)
        {
            if(room<5||room>7)return Position;
            foreach(WorldObject o in data.maps[room].objects)
                if(o!=null&&o.id==SagaOuterId(room))
                    return new Point(o.x,o.y);
            return new Point(data.maps[room].spawnX,data.maps[room].spawnY);
        }
        private bool SagaValidSpeaker(WorldObject source,int room)
        {
            return room>=5&&room<=7&&source!=null&&
                save.room==room&&source.id==SagaPersonId(room)&&
                source.kind==SagaPerson(room)&&
                pendingDialogueSourceRoom==room&&
                SourceBelongsToActiveMap(source)&&
                HasPhysicalInteractionReach(source)&&CanStand(Position)&&
                !Dying&&!practicing&&mode=="world";
        }
        // A real world-object interaction at each ORIGINAL evidence site.
        // Physical path, current room and named source are verified BEFORE
        // assigning the durable trace flag. Nothing can be forged via journal.
        private bool TryReadMainSagaTrace(WorldObject o)
        {
            if(!MainSagaActive||save.ending>0||save.stage<3||
                !Has("boss_4")||save.room<5||save.room>7||
                !Has(SagaReport(save.room))||o==null||
                !SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position)||
                Dying||practicing)return false;
            int room=save.room;
            bool first=!Has("saga_trace_"+room)&&
                o.id==SagaEvidenceId(room);
            bool distant=Has("saga_trace_"+room)&&
                !Has("saga_outer_"+room)&&!Has("saga_voice_"+room)&&
                o.id==SagaOuterId(room);
            if(!first&&!distant)return false;
            if(distant)
            {
                string[] outerZh={"","","","","",
                    "港口外侧的旧船难手记有两种笔迹。一名获救的孩子写下：米拉让我们躲在她弟弟的船里。另一名没有登船的渡工则写：我们等到天黑，也没人再来。\n\n两份笔迹都是真的。保护一艘船并没有让另一艘船自动靠岸；救援从来不该只记被救到的那批人。",
                    "沙海分水笔记没有英雄的署名，只有挨个抄写水量的人。外环居民把剩余的半桶水先分给了孩子，又偷偷在管道上画了回流箭头。伊沃后来声称那是自己的发明。\n\n真正让人活下来的，不只有他当年犯错后画出的改线图，还有那些默默修井的人。",
                    "雪原求救纸条被寒气粘在木架上。一面是孩子写给守钟人的感谢，另一面是未能入门者的求援：“我们听不见钟，便以为你们已经走了。”\n\n森那一夜救了人，也误了人。现在这两种声音都被你带回去。"};
                string[] outerEn={"","","","","",
                    "Two hands fill the old wreck log beyond the harbor. A rescued child writes: Mira hid us on her brother's boat. A ferryman left ashore writes: we waited until night, and nobody returned.\n\nBoth are true. Saving one boat did not mean the other arrived. Rescue must be counted with the people left behind.",
                    "The desert diversion journal bears no hero's name, only neighbors recording water by the cup. Outer-ring residents split their last half bucket among children, then drew return-flow arrows on the pipes. Ivo later claimed the design as his own.\n\nWhat kept people alive was not only his apology or blueprint, but those who quietly repaired the wells.",
                    "A snowbound distress note carries two voices. A child thanks the bell keeper for shelter. On its reverse an evacuee writes: we could not hear the bell, so we thought you had already left.\n\nSen saved some and stranded others. You now carry both voices back."};
                // A second account is a human problem, not a free quest flag.
                // Both outcomes preserve the testimony, but helping people
                // still at the site requires a genuine inventory item.
                string[] aidZh={"","","","","",
                    "用一份木材修补登船跳板：让仍在岸上的渡工今天就能靠船。",
                    "用一块矿石固定回流阀：让等水的居民先接到半桶水。",
                    "留下一份药草救治避难棚中的伤者：先让他撑过这个雪夜。"};
                string[] aidEn={"","","","","",
                    "Spend 1 wood repairing the rescue plank for people stranded ashore",
                    "Spend 1 ore bracing the return-flow valve for thirsty families",
                    "Spend 1 herb treating the injured evacuee in the snow shelter"};
                string[] recordZh={"","","","","",
                    "封存两面的原始船难手记，不能再让失踪者的名字从账上消失。",
                    "先保全九年前的原始供水记录，让后来修井的人能追究责任。",
                    "先保护互相矛盾的求救纸条，让下一个巡山人知道哪里还需要药。"};
                string[] recordEn={"","","","","",
                    "Secure both original wreck accounts before more names disappear",
                    "Preserve the nine-year-old flow record for future repair teams",
                    "Protect both distress notes and leave a clear call for medicine"};
                Dialog(o.name,o.en,
                    outerZh[room]+"\n\n你已经知道了真相，但现场仍有人需要帮助。留下实际物资可以先处理眼前的危险；保全原始记录则能让家属与下一班救援者真正查清发生了什么。两条路都不能抹去没有被救到的人。",
                    outerEn[room]+"\n\nKnowing what happened does not settle what you do here. Real supplies can help those still waiting, while the originals may be lost to the next storm or false report. Either choice carries both testimonies forward; neither erases the people left behind.",
                    C(aidZh[room],aidEn[room],"saga_field_aid_"+room),
                    C(recordZh[room],recordEn[room],"saga_field_record_"+room),
                    C("我先准备一下","I need to prepare","close"));
                return true;
            }
            string[] zh={
                "","", "", "", "",
                "名册的最后一页压着两层不同颜色的墨。上层是港务处的“事故损失”，下层却写着米拉亲笔抄下的救援船号。她没有向任何人说过这第二份名单。\n\n北岸不是所有乘客的归途：有人没有等到船。现在你知道为何米拉每次说“全员”都会突然停下来。",
                "蓄光池背面的散热铭牌不是制造商的标志，而是一串改写过的供水优先级。最老的编号指向九年前，一行字被手工刮掉：先供儿童避暑棚。\n\n伊沃在自己的设计图上改写了它。他曾经知道这座机器会牺牲谁，却没能把真相告诉家人。",
                "第三座旧钟的背后嵌着一枚没有刻完的名牌。钟锤内侧缠着染血的绳。那天的钟声不是机器坏了，是有人亲手捂住警报，让一群孩子避开了即将关闭的山门。\n\n森也在撤离名单上。他不是旁观者。"
            };
            string[] en={
                "","", "", "", "",
                "Two inks stain the last manifest page. The upper ledger calls the passengers 'losses'. Beneath it, Mira copied the names of a rescue boat in her own hand. She has never told anyone about that second list.\n\nThe north bank was not everyone's way home. Some never reached the boat. Now you know why Mira stops whenever she says 'all passengers'.",
                "Behind the light reservoir, a cooling plate carries nine-year-old supply priorities. One line is scraped almost clean: CHILDREN'S SHADE SHELTER FIRST.\n\nIvo rewrote the mechanism himself. He knew whom it would deprive of water. His family never heard him explain why.",
                "A half-engraved nameplate is wedged behind the third chime. Blood darkens a rope wound around its hammer. The alarm did not break: somebody muffled it to keep children out of a closing mountain gate.\n\nSen appears on the evacuation roll. He was never only a witness."
            };
            Flag("saga_trace_"+room);
            RecordMoment("第二幕 · "+new[]{"","","","","","港口第二份名单","沙海被刮去的供水令","高原钟锤中的名牌"}[room]+
                "：在真实现场发现了尚未解释的证据。",
                "Act Two: recovered physical evidence of an untold decision in "+
                new[]{"","","","","","Mistport","the Glass Desert","Frostchime"}[room]+".");
            revision++;sound="quest";
            Dialog(o.name,o.en,zh[room],en[room],
                C("带证据回去听当事人讲完","Ask the witness what happened","close"));
            return true;
        }
        private void AppendMainSagaChoice(WorldObject source)
        {
            if(conversation==null||conversation.choices==null||mode!="dialog"||
                source==null||save.ending>0||save.stage<3)return;
            string action=null,zh=null,en=null;
            if(source.kind=="guild"&&save.room==0)
            {
                action=MainSagaActive?"saga_debrief":"saga_begin";
                zh=MainSagaActive?"主线 · 与莉雅追查旧供能命令":
                    "主线 · 追查第四章背后的真相";
                en=MainSagaActive?"Main story · investigate the old orders":
                    "Main story · uncover the truth beyond Chapter Four";
            }
            else if(MainSagaActive&&Has("boss_4")&&save.room>=5&&
                save.room<=7&&source.id==SagaPersonId(save.room)&&
                source.kind==SagaPerson(save.room)&&
                Has(SagaReport(save.room)))
            {
                action="saga_account_"+save.room;
                zh=Has("saga_voice_"+save.room)?"重访我们签下的证词":
                    "主线 · 听完未曾公开的证词";
                en=Has("saga_voice_"+save.room)?"Revisit our signed testimony":
                    "Main story · hear the unspoken testimony";
            }
            if(action==null)return;
            Choice[] before=conversation.choices;
            foreach(Choice c in before)if(c!=null&&c.action==action)return;
            int insert=before.Length;
            if(insert>0&&before[insert-1]!=null&&
                before[insert-1].action=="close")insert--;
            var after=new Choice[before.Length+1];
            Array.Copy(before,0,after,0,insert);
            after[insert]=C(zh,en,action);
            Array.Copy(before,insert,after,insert+1,before.Length-insert);
            conversation.choices=after;
        }
        // After the credits, real people still live in the three regions.
        // Do not invent a fresh obligatory chapter: keep all original NPC
        // menus and append a purely optional, first-hand continuation.
        private void AppendMainSagaAfterwordChoice(WorldObject o)
        {
            if(!MainSagaActive||save.ending<=0||o==null||
                conversation==null||conversation.choices==null||mode!="dialog"||
                save.room<5||save.room>7||o.id!=SagaPersonId(save.room)||
                o.kind!=SagaPerson(save.room)||!Has("saga_voice_"+save.room))
                return;
            string action="saga_afterword_"+save.room;
            foreach(Choice choice in conversation.choices)
                if(choice!=null&&choice.action==action)return;
            Choice[] before=conversation.choices;
            int offset=before.Length;
            if(offset>0&&before[offset-1]!=null&&
                before[offset-1].action=="close")offset--;
            var after=new Choice[before.Length+1];
            Array.Copy(before,0,after,0,offset);
            after[offset]=C("结局之后 · 听听这里的人怎么生活",
                "After the ending · how people live now",action);
            Array.Copy(before,offset,after,offset+1,before.Length-offset);
            conversation.choices=after;
        }
        private void MainSagaAfterwordDialogue(WorldObject host,int room)
        {
            bool publicVoice=Has("saga_choice_"+room+"_a");
            string zh,en;
            if(room==5)
            {
                zh=publicVoice?
                    "米拉推开一本已经被翻得卷边的公共船册。她自己的签名就写在第一行。今天又有一户家属来质问她，为什么当年没有等到返航船。她没有逃开。\n\n“他们可以恨我，但不能再有人因为没拿到通行证就从纸上消失。”她把一只空药箱放在码头边。“守灯人会在这里给上岸的人换药。路过时也来歇歇。”":
                    "米拉将船册交给了失踪者的家属。公示版遮去了仍躲在外岸的孩子的藏身位置，原件则在议会的共同保管箱里。\n\n“有个母亲说她不愿意孩子的名字再被任何人拿去交换安全。”米拉望向灯塔。“所以我们把这里留成了真正的补给站。至少返航的人不必再空着肚子赶路。”";
                en=publicVoice?
                    "Mira opens a well-worn public manifest. Her own signature leads the page. Another family came today to demand why she never returned for their loved one. She did not hide.\n\n“They may hate me. But nobody disappears from a ledger for lacking a permit again.” She opens a field-aid chest beside the dock. “The watch will keep it stocked. Rest here when you pass.”":
                    "Mira gave the original manifest to the missing families. The public copy masks the hiding places of children still sheltering across the water; the council keeps the originals under joint custody.\n\n“A mother said she never wants her child's name traded for safety again,” Mira says. “So we've made this a real waystation. Returning travelers shouldn't cross this shore hungry.”";
            }
            else if(room==6)
            {
                zh=publicVoice?
                    "伊沃在旧供水牌旁亲手刻下九年前的日期，下面列出所有被切断的井。他每天都能听见有人抱怨，也能看见新的报修单一张张挂起来。\n\n“以前我只画能过审的线路图。现在任何人都能看见这根管子通向哪里。”他掀开遮阳帆：“站到这里歇歇，沙风吹不过来。”":
                    "伊沃把自己的维修图交给各井的居民委员会，没人需要再等他一个人的签字才能开阀。城内的人依旧担心供水不足，外环也没有一夜变绿。\n\n“至少有新的修井人会来跟我争论。”他笑了一下，指向刚搭好的遮阴棚。“先喝水，等太阳低一点再赶路。”";
                en=publicVoice?
                    "Ivo engraved the date of his diversion on the old water plate. Beneath it are the wells he cut off. Every day someone argues with him, and another repair request appears.\n\n“I used to draw only the routes the authorities would approve. Now anybody can see where a pipe leads.” He lifts the shade canopy. “Rest here out of the sandwind.”":
                    "Ivo distributed his repair drawings to the well committees. Nobody needs his signature alone to open a valve. The city still fears shortages; the outer ring has not turned green overnight.\n\n“At least the next repairer can argue with me.” He points to a new shade canopy. “Drink here before crossing the dunes.”";
            }
            else
            {
                zh=publicVoice?
                    "森把旧钟停在正午。他亲自站在山门前讲述自己当年为何捂住警报，以及谁因此没能回家。有人听完默默离开，也有人留下来刻完那块名牌。\n\n“今天钟声不是命令谁立刻走。”森添了几块柴。“只是告诉路过的人，暖棚还有火。”":
                    "森让当年获救的孩子和后来赶到的撤离者一起重新写下那夜的经过。两份说法不再被装进两本互不相干的档案。\n\n“我没法让所有人原谅我。”他守着新搭起的暖棚。“但以后听不见钟的人，也能沿着这里的火光找到路。”";
                en=publicVoice?
                    "Sen stops the chime at noon and tells the waiting crowd why he muffled the alarm, and who never came home. Some leave without speaking; others remain to finish the nameplate.\n\n“Today the bell orders nobody to run,” he says, adding firewood. “It means there is still warmth here.”":
                    "Sen gathered the children he saved and the evacuees who arrived too late. Together they rewrote the history of that night, refusing to hide one set of names in a different book.\n\n“I can't make everyone forgive me,” Sen says beside the new shelter. “But someone who cannot hear the bell can still follow this fire home.”";
            }
            Dialog(host.name,host.en,zh,en,
                C("谢谢，我继续上路","Thank you. I'll keep travelling","close"));
        }
        // A consequence of a signed story choice, not another chores
        // checklist. Existing, physical NPCs sustain different safe rests
        // after the ending; no remote healing, combat invulnerability,
        // reward farming or new SaveData schema.
        private void TickMainSagaAfterword(float dt)
        {
            if(dt<=0f||!MainSagaActive||save.ending<=0||mode!="world"||
                Dying||practicing||FieldWorkPreventsRest||!SafeTravel||
                save.room<5||save.room>7||
                !Has("saga_voice_"+save.room))return;
            WorldObject host=null;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&o.id==SagaPersonId(save.room)&&
                    o.kind==SagaPerson(save.room)){host=o;break;}
            if(host==null||!HasPhysicalInteractionReach(host))return;
            bool publicVoice=Has("saga_choice_"+save.room+"_a");
            if(save.room==5)
            {
                save.health=Math.Min(save.maxHealth,save.health+
                    (publicVoice?1.25f:.65f)*dt);
                save.energy=Math.Min(100f,save.energy+
                    (publicVoice?1.1f:2.1f)*dt);
            }
            else if(save.room==6)
            {
                save.heat=Math.Max(0f,save.heat-
                    (publicVoice?3.4f:1.7f)*dt);
                save.energy=Math.Min(100f,save.energy+
                    (publicVoice?.8f:2.4f)*dt);
            }
            else
            {
                save.cold=Math.Max(0f,save.cold-
                    (publicVoice?3.4f:1.8f)*dt);
                save.health=Math.Min(save.maxHealth,save.health+
                    (publicVoice?.7f:1.5f)*dt);
            }
        }
        private string MainSagaCouncilHearing(bool english)
        {
            if(!MainSagaActive||MainSagaVoices<3)return "";
            bool mira=Has("saga_choice_5_a"),ivo=Has("saga_choice_6_a"),
                sen=Has("saga_choice_7_a");
            return english?
                "\n\nMira speaks first. "+(mira?
                    "“My name is on the complete public manifest. The missing families may challenge me, even if I cannot bring their loved ones home.”":
                    "“The missing families read the original manifest first. Children still hiding across the water must not have their shelter exposed.”")+
                "\n\nIvo answers. "+(ivo?
                    "“I signed the order that drained the outer wells. The date and the pipe I changed are now public.”":
                    "“Every well has the plans to undo my diversion. Nobody needs to trust my signature alone to get water.”")+
                "\n\nSen sets down the broken chime. "+(sen?
                    "“I'll tell the public hearing why I silenced the alarm. Those who arrived too late deserve to hear it from me.”":
                    "“The rescued children and late evacuees will speak together. No one should erase either account.”")+
                "\n\nThe lamplighters object: “Public names may expose hidden children.” The ferrymen answer: “Hidden lists can erase families forever.” The mountain delegates ask who may stop the next dangerous machine. Nobody gets a veto. Each region must vote on a system it is willing to maintain.":
                "\n\n米拉先开口。"+(mira?
                    "“完整的乘客名册上有我的签名。失踪者家属可以质问我，即使我永远没法把他们要的人还回来。”":
                    "“原件先交给失踪者家属。那些还躲在外岸的孩子不能因为公开名单就暴露安置地点。”")+
                "\n\n伊沃接着说。"+(ivo?
                    "“九年前抽干外环水井的命令是我签的。日期、管道和手印都已经公开。”":
                    "“每座井站都拿到了恢复供水的图纸。以后谁要开阀，不必再指望我一个人的勇气。”")+
                "\n\n森把旧钟牌放在桌上。"+(sen?
                    "“当年是我捂住的警报。来迟的人有权亲耳听我解释。”":
                    "“被救下的孩子和没能进门的人应该一起作证，谁也不能抹去另一边的经历。”")+
                "\n\n守灯会质疑：“公开的名册会暴露躲起来的孩子。”摆渡盟反驳：“封存的名册也能让家属永远找不到人。”山野议会问：下一台机器失控时，究竟谁能下令停机？没有谁的供词自动成为否决票。三地必须选择自己愿意维护的制度。";
        }
        private string MainSagaFieldHearing(bool english)
        {
            if(!MainSagaActive||MainSagaVoices<3)return "";
            string result="";
            for(int room=5;room<=7;room++)
            {
                bool aid=Has("saga_field_aid_done_"+room);
                bool record=Has("saga_field_record_done_"+room);
                if(!aid&&!record)continue; // old v1/v2 route, don't fabricate
                if(english)
                    result+=room==5?(aid?
                        "\n\nA ferryman reports that the repaired boarding plank carried stranded families to the next boat. The old manifest still names those who never boarded.":
                        "\n\nThe unaltered wreck ledger is safe, the harbor delegation says, but the boarding plank still awaits carpenters."):
                        room==6?(aid?
                        "\n\nThe desert delegates bring a cup of water from the repaired return valve. Its original stolen allocation is entered beside the names of the well workers.":
                        "\n\nThe original water readings have survived. Ivo accepts responsibility for sending workers to repair the leaking valve."):
                        aid?
                        "\n\nA survivor from the warming shelter is well enough to send a letter. The mountain delegation refuses to hide the other stranded names.":
                        "\n\nThe two snowbound distress letters have been preserved. Sen confirms a new medicine run, but cannot claim the patients were already safe.";
                else
                    result+=room==5?(aid?
                        "\n\n摆渡盟带来登船跳板修复的报告：有人得以登船，旧名册上未曾登船的人也没有被划掉。":
                        "\n\n摆渡盟宣布船难原件已经安全封存，但损坏的跳板仍在等待木匠。"):
                        room==6?(aid?
                        "\n\n沙海代表捧来一小杯从回流阀接出的水，居民维修者的名字和被挪用的旧配额一起写进账本。":
                        "\n\n九年前的真实水量已经留存，伊沃承担派人修复漏水阀的责任。"):
                        aid?
                        "\n\n暖棚的一名幸存者已经能写信，可山野议会坚持把没能找到棚子的人一同记下。":
                        "\n\n两份雪原求救信已被保存。森安排了新的送药路线，却不敢称那些伤者已经安全。";
            }
            return result;
        }
        private string MainSagaCouncilVoteResult(int covenant,bool english)
        {
            if(!MainSagaActive||MainSagaVoices<3||covenant<1||covenant>3)return "";
            return english?
                covenant==1?
                    "\n\nThe shared water order reserves a measured share for outer settlements. Mira's ferries publish actual shortages; Ivo must deliver daily flow readings to each local well committee.":
                covenant==2?
                    "\n\nEvery lantern shift now has two named civilian keepers, one from the port and one from the highlands. Sen insists that even a deliberately silent bell be recorded as a failure.":
                    "\n\nNo settlement may secretly alter the root network. Each change must show a public route, named witnesses and time for residents to challenge it.":
                covenant==1?
                    "\n\n第一份共同供水令为外环村庄留出明确配额。米拉的渡船必须公布真正的缺水情况，伊沃每天要将实测水量交给各井的居民委员会。":
                covenant==2?
                    "\n\n每次换灯都必须有两位平民负责人，一位来自港口、一位来自高原。森坚持：即使是主动按住的警钟，也必须如实记录为没能发出的警报。":
                    "\n\n任何城镇都不能私自改动星根网络。每次调度都需要公开路线、署名见证与居民提出异议的期限。";
        }
        private void MainSagaDebrief(WorldObject guild)
        {
            Dialog(guild.name,guild.en,
                "莉雅从三份星核收据中抽出同一张红色复写单。上面没有厄洛的签名，只有一道被划去的命令：“外环停供，保全中央。”\n\n“我过去也以为一切都从堡垒开始。可雾港的失踪日期更早，沙海的配给单则早了九年。有人在灾难发生前就重新排序了谁能分到水和光。”\n\n“先听米拉、伊沃和森自己讲。去他们的真实现场，再带着证据回来。别让一个新的英雄替他们决定什么可以公开。”",
                "Lyra draws a red carbon copy from the three starcore receipts. Orrin's signature is missing; one crossed-out order reads: CUT THE OUTER RING. SAVE THE CENTER.\n\n“I thought everything began at the citadel. But Mistport's disappearances came earlier, and the desert ration order is nine years older. Someone ranked the people who would receive water and light before the disaster.”\n\n“Hear Mira, Ivo and Sen in their own words. Visit their actual evidence sites, then ask them. We cannot put a new hero in charge of which truth may be told.”",
                C("追踪真正的第二幕主线","Track the second act","saga_track"),
                C("我会带他们的回答回来","I'll bring their answers","close"));
        }
        // Chapter Five-Seven witnesses must survive two DISTINCT on-map
        // incursions before they can safely countersign the original records.
        // This is transient encounter state, not a SaveData format change.
        // Already signed v2/v1-origin saves keep their existing milestone.
        private int sagaGuardRoom=-1,sagaGuardWave;
        private float sagaGuardClock;
        // Transient contested-evidence pressure: owned raiders must actually
        // reach the original speaker while able to fight. No v2 save fields.
        private float sagaGuardBreach;
        private bool sagaGuardBreachWarned, sagaGuardBodyBlocking;
        private bool sagaGuardDeviceUsed;
        private MapData sagaGuardMap;
        private SaveData sagaGuardSave;
        private WorldObject sagaGuardWitness;
        private readonly List<Enemy> sagaGuardFoes=new List<Enemy>();
        private readonly HashSet<Enemy> sagaGuardDefeated=new HashSet<Enemy>();
        public bool SagaGuardActive
        { get { return sagaGuardRoom>=5&&sagaGuardRoom<=7&&
            save.room==sagaGuardRoom&&
            Object.ReferenceEquals(sagaGuardSave,save)&&
            Object.ReferenceEquals(sagaGuardMap,Map)&&
            sagaGuardWitness!=null&&
            SourceBelongsToActiveMap(sagaGuardWitness); } }
        public int SagaGuardWave { get { return SagaGuardActive?sagaGuardWave:0; } }
        public int SagaGuardRemaining
        { get { int n=0;if(SagaGuardActive)
            foreach(Enemy e in sagaGuardFoes)
                if(e!=null&&e.hp>0&&enemies.Contains(e))n++;
            return n; } }
        public float SagaGuardClock {get{return SagaGuardActive?sagaGuardClock:0f;}}
        public int SagaGuardEvidenceSafety
        {get{return SagaGuardActive?Math.Max(0,100-(int)Math.Ceiling(sagaGuardBreach)):100;}}
        private Point SagaGuardWitnessFoot
        {get{return SagaGuardActive?
            new Point(sagaGuardWitness.x,sagaGuardWitness.y):Position;}}
        public bool SagaGuardBodyBlocking {get{return SagaGuardActive&&sagaGuardBodyBlocking;}}
        public bool SagaGuardDeviceUsed {get{return SagaGuardActive&&sagaGuardDeviceUsed;}}
        public bool SagaGuardDeviceReady
        {get{return SagaGuardActive&&!sagaGuardDeviceUsed&&SagaGuardRemaining>0;}}
        private static string SagaGuardDeviceZh(int room)
        {
            return room==5?"拉响雾港雾笛 · 消耗木材1，驱散追兵并恢复耐力":
                room==6?"打开沙海散热阀 · 耐力18，散热并打断攻势":
                "点燃高原急救篝火 · 消耗药草1，驱寒疗伤并打断攻势";
        }
        private static string SagaGuardDeviceEn(int room)
        {
            return room==5?"Sound the harbor foghorn · 1 timber, stagger raiders and restore stamina":
                room==6?"Open the desert heat vent · 18 stamina, cool the field and interrupt attackers":
                "Light the alpine rescue brazier · 1 herb, warm the line and interrupt attackers";
        }
        public Point SagaGuardWitnessPosition
        {get{return SagaGuardWitnessFoot;}}
        public Point SagaGuardGuideTarget
        {
            get {
                if(!SagaGuardActive)return Position;
                Point speaker=SagaGuardWitnessFoot;
                Enemy priority=null;
                float score=float.MaxValue;
                foreach(Enemy e in sagaGuardFoes)
                {
                    if(e==null||e.hp<=0||!enemies.Contains(e))continue;
                    float distance=(e.Position-speaker).Length;
                    // Guide to the immediate breach before the backline.
                    // Otherwise prioritize the raider storming the records
                    // over the archer harassing the player from afar.
                    bool breaching=e.stun<=0f&&e.stagger<=0f&&e.hitTime<=0f&&
                        distance<=7.5f&&CanStand(e.Position)&&
                        ClearAttackPath(e.Position,speaker);
                    float urgency=distance+(e.kind==1?10f:0f)-
                        (breaching?25f:0f);
                    if(urgency>=score)continue;
                    priority=e;score=urgency;
                }
                return priority!=null?priority.Position:speaker;
            }
        }
        private bool IsSagaGuardEnemy(Enemy e)
        {return e!=null&&SagaGuardActive&&sagaGuardFoes.Contains(e);}
        private void OnSagaGuardEnemyDefeated(Enemy e)
        {if(IsSagaGuardEnemy(e))sagaGuardDefeated.Add(e);}
        private void ResetSagaGuard()
        {
            // Retire only ally shots committed to the abandoned witness.
            projectiles.RemoveAll(p=>p.sagaGuardCoverTarget!=null&&
                sagaGuardFoes.Contains(p.sagaGuardCoverTarget)&&
                Object.ReferenceEquals(p.sagaGuardCoverSave,save)&&
                Object.ReferenceEquals(p.sagaGuardCoverMap,Map));
            foreach(Enemy e in sagaGuardFoes)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);regionalAmbientEnemies.Remove(e);
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,e));
            }
            sagaGuardRoom=-1;sagaGuardWave=0;sagaGuardClock=0f;
            sagaGuardBreach=0f;sagaGuardBreachWarned=false;
            sagaGuardBodyBlocking=false;sagaGuardDeviceUsed=false;
            sagaGuardMap=null;sagaGuardSave=null;sagaGuardWitness=null;
            sagaGuardFoes.Clear();sagaGuardDefeated.Clear();
        }
        private bool SagaGuardFootFree(Point foot,List<Point> chosen)
        {
            if(!CanStand(foot)||(foot-Position).Length<4.5f||
                NavigationPathFrom(Position,foot,true).Count==0)return false;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&(foot-new Point(o.x,o.y)).Length<2.9f)return false;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0&&(e.Position-foot).Length<3.5f)return false;
            foreach(Point p in chosen)
                if((p-foot).Length<5f)return false;
            return true;
        }
        private bool SpawnSagaGuardWave(int wave)
        {
            if(!SagaGuardActive||wave<1||wave>2)return false;
            Point origin=new Point(sagaGuardWitness.x,sagaGuardWitness.y);
            var feet=new List<Point>();
            int cx=(int)Math.Floor(origin.x),cy=(int)Math.Floor(origin.y);
            // Search the actual connected walkable approach; the second
            // wave attacks from the opposite flank, never through a wall.
            // The first incursion prefers the east approach; the second
            // prefers the WEST. Some original docks and highland gates
            // physically close a flank. If that flank has too few legal
            // feet, expand through genuine connected alternatives rather
            // than silently cancel an otherwise completed first wave.
            for(int pass=0;pass<2&&feet.Count<2;pass++)
            for(int radius=6;radius<=18&&feet.Count<2;radius++)
            for(int dy=-radius;dy<=radius&&feet.Count<2;dy++)
            for(int dx=-radius;dx<=radius&&feet.Count<2;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                    Math.Abs(dx)+Math.Abs(dy)>24)continue;
                if(pass==0&&
                    ((wave==1&&dx<0)||(wave==2&&dx>0)))continue;
                Point foot=new Point(cx+dx+.5f,cy+dy+.5f);
                if(SagaGuardFootFree(foot,feet))feet.Add(foot);
            }
            if(feet.Count!=2)return false;
            sagaGuardFoes.Clear();sagaGuardDefeated.Clear();
            int[,] styles={{1,0,0,1},{2,0,2,1},{1,2,0,2}};
            for(int i=0;i<2;i++)
            {
                int kind=styles[sagaGuardRoom-5,(wave-1)*2+i];
                Enemy e=CreatePlacedEnemy(++enemyId,kind,feet[i],
                    49f+6f*(sagaGuardRoom-5)+8f*(wave-1));
                // A preserved original gives civilians an actual warning of
                // the attackers' approach; both waves still fight for real.
                if(Has("saga_field_record_done_"+sagaGuardRoom))
                {
                    e.stun=Math.Max(e.stun,1.05f);
                    e.armorOpenTime=Math.Max(e.armorOpenTime,.85f);
                }
                sagaGuardFoes.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.6f)
                    {variant=kind});
            }
            sagaGuardWave=wave;
            sagaGuardBodyBlocking=false;
            return true;
        }
        // Only a real committed enemy contact may deliver the EXTRA
        // documents hit. Existing ordinary siege pressure still applies,
        // but the 17/25 integrity loss is tied to a telegraphed attack.
        private void StrikeSagaGuardEvidence(Enemy e,Point lockedTarget)
        {
            if(!IsSagaGuardEnemy(e)||e.hp<=0||!enemies.Contains(e)||
                !CanStand(e.Position)||
                (lockedTarget-SagaGuardWitnessFoot).Length>.6f||
                (e.Position-lockedTarget).Length>2.3f||
                !EnemyObjectiveContactLaneClear(e,lockedTarget)||
                SagaGuardHeroInterposes(e,lockedTarget))return;
            int loss=e.kind==2?25:17;
            sagaGuardBreach=Math.Min(100f,sagaGuardBreach+loss);
            Point site=SagaGuardWitnessFoot;
            effects.Add(new Effect("burst",site,.28f)
                {radius=e.kind==2?1.1f:.65f,variant=save.room});
            effects.Add(new Effect("number",site+new Point(0,-.85f),
                .9f,L("原件受损 -"+loss,"RECORDS HIT -"+loss)));
            sound="alert";revision++;
            if(sagaGuardBreach>=100f)
            {
                ResetSagaGuard();
                Notify("追兵冲破防线，原始证据被夺走！重组护卫后才能继续签署。",
                    "Raiders breached the line and seized the originals! Regroup before countersigning.");
            }
        }
        // A bodyguard must physically stand IN the lane from an acting
        // raider to the witness, not simply idle anywhere in the radius.
        // World collision and true contact geometry apply on both sides.
        private bool SagaGuardHeroInterposes(Enemy attacker,Point witness)
        {
            if(attacker==null||!CanStand(Position))return false;
            Point lane=witness-attacker.Position;
            float len2=lane.x*lane.x+lane.y*lane.y;
            if(len2<.25f)return false;
            Point relative=Position-attacker.Position;
            float t=(relative.x*lane.x+relative.y*lane.y)/len2;
            if(t<=.12f||t>=.88f)return false;
            Point closest=attacker.Position+lane*t;
            return (closest-Position).Length<=.72f&&
                ClearAttackPath(attacker.Position,Position)&&
                ClearAttackPath(Position,witness);
        }
        private void TickSagaGuard(float dt)
        {
            if(sagaGuardRoom<0)return;
            if(!SagaGuardActive||Dying||practicing)
                {ResetSagaGuard();return;}
            // No invisible defeat or arbitrary enemy-list edits may advance
            // the chapter. Real combat death must call the production hit hook.
            foreach(Enemy e in sagaGuardFoes)
                if(e==null||(!sagaGuardDefeated.Contains(e)&&
                    (!enemies.Contains(e)||e.hp<=0)))
                {
                    ResetSagaGuard();revision++;
                    Notify("证词护卫中有敌人未经确认便消失。回到当事者处重新组织。",
                        "An unverified attacker vanished. Return to the witness and regroup.");
                    return;
                }
            bool cleared=sagaGuardFoes.Count==2;
            foreach(Enemy e in sagaGuardFoes)
                if(!sagaGuardDefeated.Contains(e)||e.hp>0||
                    enemies.Contains(e)){cleared=false;break;}
            if(cleared)
            {
                int completed=sagaGuardWave;
                if(completed==1)
                {
                    if(!SpawnSagaGuardWave(2))
                    {
                        ResetSagaGuard();revision++;
                        Notify("第二波追兵暂时找不到可到达的进路。撤回防线后再试。",
                            "The second wave has no safe accessible route. Regroup and retry.");
                        return;
                    }
                    sound="alert";revision++;
                    Notify("第一波袭击已被挡住。真正的追兵从另一侧赶来！",
                        "The first assault is defeated. A second wave is approaching from the other flank!");
                    return;
                }
                int room=sagaGuardRoom;
                // The integrity actually defended across BOTH hostile waves
                // matters to what the speaker can credibly certify later.
                // These are ordinary v2 mission flags, not a save migration.
                bool unscarred=SagaGuardEvidenceSafety>=80;
                ResetSagaGuard();
                Flag("saga_guard_"+room);
                Flag((unscarred?"saga_guard_pristine_":"saga_guard_scarred_")+room);
                CreditCoins(16+room*3+(unscarred?6:0));
                AddXp(24+room*7+(unscarred?8:0));
                revision++;sound="quest";
                RecordMoment("第二幕 · "+new[]{"","","","","","雾港名册","沙海配额图","高原警钟"}[room]+
                    (unscarred?"：两波袭击后原件仍清晰完整，可由当事者直接核对。":
                        "：原件虽在，边角在袭击中受损；签名时必须注明破损。"),
                    unscarred?
                        "Act Two: the original remained legible across two real assaults; the witness may check it directly.":
                        "Act Two: the original survived two real assaults with visible battle damage; its condition must be annotated.");
                Notify(unscarred?
                    "两波追兵已被击退，原件清晰完整！回到证人处核对签名。":
                    "两波追兵已被击退，但原件留下损伤。回到证人处注明破损再签名。",
                    unscarred?
                    "Both waves are defeated. The originals remain fully legible; return for the signature.":
                    "Both waves are defeated, but the originals are battle-scarred. Annotate their condition before signing.");
                return;
            }
            if(dt<=0f||mode!="world")return;
            if((Position-new Point(sagaGuardWitness.x,sagaGuardWitness.y)).Length>31f)
            {
                ResetSagaGuard();revision++;
                Notify("你离开了证人防线。守卫撤回，必须重新集结。",
                    "You left the witness perimeter. The defenders withdrew; regroup at the original speaker.");
                return;
            }
            sagaGuardClock=Math.Max(0f,sagaGuardClock-dt);
            if(sagaGuardClock<=0f)
            {
                ResetSagaGuard();revision++;
                Notify("袭击拖得太久，证人带着证据转移了。重新组织才能再签。",
                    "The defense ran out of time. The witness relocated the evidence; regroup before signing.");
                return;
            }
            // The enemies contest the actual witness, not an abstract kill
            // quota. Only living, present, unstunned owned raiders inside
            // the speaker's perimeter erode the original evidence. Pull
            // them off the site, stagger them or use the regional device.
            // Bound the time slice so a paused Unity frame cannot instantly
            // destroy the documents. Pressure persists across both waves.
            float slice=Math.Min(.1f,dt);
            int raidersAtWitness=0;
            Point witnessFoot=new Point(sagaGuardWitness.x,sagaGuardWitness.y);
            bool previouslyBlocked=sagaGuardBodyBlocking;
            sagaGuardBodyBlocking=false;
            foreach(Enemy e in sagaGuardFoes)
            {
                if(e==null||e.hp<=0||!enemies.Contains(e)||
                    e.stun>0f||e.stagger>0f||e.hitTime>0f||
                    !CanStand(e.Position)||
                    (e.Position-witnessFoot).Length>7.5f||
                    !ClearAttackPath(e.Position,witnessFoot))continue;
                if(SagaGuardHeroInterposes(e,witnessFoot))
                    sagaGuardBodyBlocking=true;
                else
                    raidersAtWitness++;
            }
            if(sagaGuardBodyBlocking&&!previouslyBlocked)
            {
                effects.Add(new Effect("number",Position+new Point(0,-.6f),
                    .8f,L("舍身拦截！","BODYGUARD!")));
                sound="guard";revision++;
            }
            sagaGuardBreach=Math.Max(0f,Math.Min(100f,
                sagaGuardBreach+slice*(raidersAtWitness>0?
                    raidersAtWitness*10f:-6f)));
            if(sagaGuardBreach>=100f)
            {
                int lostRoom=sagaGuardRoom;
                ResetSagaGuard();revision++;sound="alert";
                Notify("第"+(lostRoom-4)+"处证人的原始证据被追兵夺走了！重新组织防守；原有调查不会被抹去。",
                    "Raiders seized the witness's original evidence! Regroup; previously earned investigation remains intact.");
                return;
            }
            if(sagaGuardBreach>=55f&&!sagaGuardBreachWarned)
            {
                sagaGuardBreachWarned=true;revision++;sound="alert";
                effects.Add(new Effect("number",witnessFoot+new Point(0,-1f),
                    1.35f,L("原件正在被抢夺！","EVIDENCE UNDER ATTACK!")));
                Notify("证据正受到围攻！冲向证人击退追兵，或启用当地机关缓解危机。",
                    "The records are under attack! Intercept raiders near the witness or operate the local device.");
            }
            if(sagaGuardBreach<30f)sagaGuardBreachWarned=false;
            // Three genuinely different local risks in the combat pocket.
            if((Position-witnessFoot).Length<17f)
            {
                if(sagaGuardRoom==5&&!LowTide)
                    save.energy=Math.Max(0f,save.energy-dt*
                        (Has("saga_field_aid_done_5")?.28f:.45f));
                if(sagaGuardRoom==6)
                    save.heat=Math.Min(100f,save.heat+dt*
                        (Has("saga_field_aid_done_6")?1.1f:1.7f));
                if(sagaGuardRoom==7)
                    save.cold=Math.Min(100f,save.cold+dt*
                        (Has("saga_field_aid_done_7")?1.1f:1.7f));
            }
        }
        // Reuse durable civilian rescue choices as farshore witness support.
        private bool EarlyRescueEcho(int room)
        { return room>=5&&room<=7&&Has("chapter_road_diverted_"+(room-3)); }
        private string EarlyRescueVoice(int room,bool english)
        {
            if(!EarlyRescueEcho(room))return "";
            return english?
                room==5?
                    "Arin's younger brother recognizes your old empty raft. He carried the missing night ledger over the flood. “There still had to be someone carrying the names,” he tells Mira.\n\n":
                    room==6?
                    "The mine apprentice brings the pressure gauge you once jammed. Ivo runs a finger over its scratched glass. “He shut down the engine without waiting for my signature,” he says.\n\n":
                    "The refugee cart driver from the citadel has reached this mountain road. Sen listens to how a guard recognized his sister's scarf and lowered his spear rather than stop the wagon.\n\n":
                room==5?
                    "阿菱的弟弟认出了你曾放出的空木筏。他带着漏报的值夜簿过了潮线：“终究还得有人把那些名字送过来。”\n\n":
                    room==6?
                    "矿场学徒带来你当年卡住的压力表。伊沃摸着玻璃上的伤痕：“他没等我的签字，自己把机器停了。”\n\n":
                    "从堡垒赶来的运货车夫走到高原，告诉森那个认出妹妹围巾、最终放下长矛的守卫。森听完许久没说话。\n\n";
        }
        // The later witness has a different answer to what the player did,
        // not simply a different color of completion notification.
        private string MainSagaFieldConsequence(int room,bool english)
        {
            if(room<5||room>7)return "";
            bool aid=Has("saga_field_aid_done_"+room);
            if(!aid&&!Has("saga_field_record_done_"+room))return ""; // older v2
            if(english)
                return room==5?(aid?
                    "“You rebuilt the boarding plank,” Mira says. “The next boat brought two people ashore. Their names weren't in my official roll. I'll add them in my own hand.”\n\n":
                    "“You protected both originals,” Mira says. “My brother is still gathering wood for that broken plank. I'll go there when we finish speaking.”\n\n"):
                    room==6?(aid?
                    "“The old valve flows again,” Ivo says. “The families at that well made the first repairs. I took their drawings and called them mine. Not again.”\n\n":
                    "“You kept the original measurements,” Ivo says. “The pipe is still broken, and I owe those neighbors the repair myself.”\n\n"):
                    aid?
                    "“Someone at the shelter is still breathing because you stopped with medicine,” Sen says. “That matters more than a bell I could not bring myself to ring.”\n\n":
                    "“You kept both distress letters,” Sen says. “I sent medicine up the pass after reading them. I should have asked sooner.”\n\n";
            return room==5?(aid?
                "“你补了登船跳板。”米拉说，“下一艘船又接上来两个人。官方的名册没有他们，我要亲手补上。”\n\n":
                "“你保存了两份原件。”米拉说，“弟弟还在给坏跳板找木材。等我们谈完，我会自己过去。”\n\n"):
                room==6?(aid?
                "“旧阀重新流水了。”伊沃说，“原本就是井边居民先找到修法，我却把图纸写成自己的功劳。这次不会了。”\n\n":
                "“你保住了当年的水量。”伊沃说，“可破管还在那里。我欠居民一回自己动手的修复。”\n\n"):
                aid?
                "“那个暖棚里的人还在呼吸，因为你带了药。”森说，“这比我当年没敢敲的钟重要。”\n\n":
                "“你保住了两封求救信。”森说，“我看完已经派人送药上去了。我本应该更早问一声。”\n\n";
        }
        private string MainSagaBattleRecordCondition(int room,bool english)
        {
            if(!Has("saga_guard_"+room))return "";
            if(Has("saga_guard_scarred_"+room))
                return english?
                    "The original survived, but its edges bear fresh battle damage. The speaker marks those damaged margins before making any claim about what the page proves.\n\n":
                    "原件保住了，但页边留下刚才的伤痕。证人先在受损处做标记，再说明哪些内容仍可辨认，绝不把破损处当作完整记录。\n\n";
            if(Has("saga_guard_pristine_"+room))
                return english?
                    "The original remains legible after both assaults. The speaker verifies every surviving line against the earlier copy before signing.\n\n":
                    "原件经过两波袭击仍字迹清晰。证人逐行拿旧副本核对，确认未被替换，才重新拿起笔。\n\n";
            return ""; // Older completed v1/v2 saves had no integrity grade.
        }
        private void MainSagaAccount(WorldObject host,int room)
        {
            if(!Has("saga_trace_"+room))
            {
                string[] sitesZh={"","","","","","西北码头的旧乘客名册","蓄光池东侧的接收台","高原第三座旧钟"};
                string[] sitesEn={"","","","","","the old northwest passenger manifest","the eastern reservoir receiver","the third mountain chime"};
                Dialog(host.name,host.en,
                    "“你带来了公开的调查记录，但我还有一件不能藏在报告里的事。先去看"+sitesZh[room]+"。不看见现场，我说什么都只像是在替自己开脱。”",
                    "“You've read the public report. There is something I left out. First go see "+
                    sitesEn[room]+". Without that evidence, everything I say will sound like an excuse.”",
                    C("亲自去调查","Investigate in person","close"));
                return;
            }
            if(!Has("saga_outer_"+room)&&!Has("saga_voice_"+room))
            {
                string[] places={"","","","","",
                    "外海的船难手记","远处的沙海分水笔记","雪原深处的求救纸条"};
                string[] placesEn={"","","","","",
                    "the outlying wreck journal","the distant desert diversion journal",
                    "the snowfield distress note"};
                Dialog(host.name,host.en,
                    "“你找到了我想隐瞒的第一份证据。但那里还有一个人写过另一种结局。去找"+places[room]+"。你回来时，我会把两种声音放在同一份供词里。”",
                    "“You found the first evidence I hid. Someone farther away wrote a different ending. Find "+
                    placesEn[room]+". When you return, I'll put both voices in my account.”",
                    C("沿原道路继续追查","Follow the original road","close"));
                return;
            }
            if(Has("saga_voice_"+room))
            {
                string note=Has("saga_choice_"+room+"_a")?
                    "那份原始供词已经公开。居民可以追问，也可以不同意。":
                    "你尊重了证人的保留与说明方式，但没有抹去真实的事件。";
                string noteEn=Has("saga_choice_"+room+"_a")?
                    "The first-hand account is public. Residents may question it or disagree.":
                    "You respected the witness's right to explain, without erasing what happened.";
                Dialog(host.name,host.en,note,noteEn,
                    C("带上这份证词继续前行","Carry the testimony onward","close"));
                return;
            }
            string[] zh={"","","","","",
                "米拉终于把手从船舵上移开。“我确实在北岸藏过一艘救援船。我想保住船上几个没有通行证的孩子，就把他们的名字从官方名册里删掉了。后来有人死在另一条航路上。我不敢把两种事放在同一页。”\n\n“你可以把我的签名也附在公开名单上，让别人知道是谁撒了谎。也可以先让失踪者家属拿到原件，我自己去向他们解释。你来作见证，不要替我说‘没有代价’。”",
                "伊沃把设计图翻到背面。“九年前，我把救急用水改送到了城里。我的儿子当时就躺在避暑棚，我以为只是一夜。第二天外环的井就干了。”\n\n“我画过一份改回去的线路图，却不敢盖自己的印。公开我当年的决定，或者把完整的改造方案交给所有井站，让它们不再依赖我的勇气。无论选哪条，我都得站在这里承认是我做的。”",
                "森取下那枚断裂的钟牌。“当年我阻止了警报。若守军听见，就会封闭北门，把躲进去的孩子赶出来。我救了他们，但后面的撤离者因此错过钟声。”\n\n“我不想让死去的人替我背负‘钟坏了’的谎。我能当众讲出自己的名字；也能先带你去找那群孩子，让他们说那一夜是什么样。不要把这两种人写成敌人。”"};
            string[] en={"","","","","",
                "Mira releases the ship's wheel. “I hid a rescue boat on the north bank. To protect children without travel permits, I struck their names from the official roll. Others died on a different crossing. I could not bring myself to put both truths on one page.”\n\n“You can attach my signature to the public manifest. Or give the originals to the missing families first, and I'll explain it to them myself. Witness what happened. Do not call it a rescue without a cost.”",
                "Ivo turns over his diagrams. “Nine years ago I diverted emergency water toward the city. My son was lying in the shade shelter. I thought it would last one night. By morning the outer wells were dry.”\n\n“I drew a route to reverse the diversion but never signed it. Publish my decision, or distribute the complete repair plans to every well station so no one needs to depend on my courage again. Either way I will admit what I did.”",
                "Sen removes the broken chime tag. “I stopped the alarm. If the guards had heard it, they would have sealed the north gate and expelled the children sheltering inside. I saved them. The evacuees behind them missed the signal.”\n\n“I won't let the dead carry the lie that the bell simply broke. I can name myself in public. Or we can find those children first and let them describe that night. Please don't turn these two groups into enemies.”"};
            string[] first={"","","","","",
                "公开米拉署名的完整名册",
                "公布伊沃亲自改写的配额决定",
                "请森公开承认敲钟之夜的决定"};
            string[] firstEn={"","","","","",
                "Publish the complete manifest under Mira's name",
                "Publish Ivo's signed ration decision",
                "Ask Sen to testify publicly about the silenced bell"};
            string[] second={"","","","","",
                "让失踪者家属先读原件，再共同公开",
                "把修复图交给所有井站并保存他的供词",
                "先听获救孩子和迟到的撤离者一起讲述"};
            string[] secondEn={"","","","","",
                "Give the originals to families first, then publish together",
                "Distribute repair diagrams and preserve his confession",
                "Hear survivors and late evacuees together first"};
            if(!Has("saga_guard_"+room))
            {
                Dialog(host.name,host.en,EarlyRescueVoice(room,false)+MainSagaFieldConsequence(room,false)+zh[room]+
                    "\n\n有人试图截走原始证据。"+(room==5?
                        "先守住渡口上的名册，直到第二批追兵撤退。":
                        room==6?"先守住井站的供水图和签字人，直到两波破坏者落败。":
                        "先守住高原钟牌和幸存者，击退两波封口的追兵。"),
                    EarlyRescueVoice(room,true)+MainSagaFieldConsequence(room,true)+en[room]+
                    "\n\nSomeone is trying to seize this original evidence. Defend the genuine witness and records against two separate attacks before signing.",
                    SagaGuardActive?
                        C("守住防线 · 剩余"+SagaGuardRemaining+"名 · 安全度"+SagaGuardEvidenceSafety+"%",
                            "Defend · "+SagaGuardRemaining+" raiders · evidence "+SagaGuardEvidenceSafety+"%","close"):
                        C("守护证人与证据 · 两波实际战斗",
                            "Defend witness and evidence · two live waves","saga_guard_begin"),
                    SagaGuardActive&&SagaGuardDeviceReady?
                        C(SagaGuardDeviceZh(room),SagaGuardDeviceEn(room),"saga_guard_device"):
                        C("继续保护证人","Keep defending the witness","close"),
                    SagaGuardActive?
                        C("撤回护卫 · 重新组织","Withdraw the defense","saga_guard_abort"):
                        C("先备好物资","Prepare supplies","close"));
                return;
            }
            Dialog(host.name,host.en,EarlyRescueVoice(room,false)+MainSagaFieldConsequence(room,false)+
                    MainSagaBattleRecordCondition(room,false)+zh[room],
                EarlyRescueVoice(room,true)+MainSagaFieldConsequence(room,true)+
                    MainSagaBattleRecordCondition(room,true)+en[room],
                C(first[room],firstEn[room],"saga_sign_"+room+"_a"),
                C(second[room],secondEn[room],"saga_sign_"+room+"_b"),
                C("再想一想","I need to think","close"));
        }
        private string MainSagaFinalReveal(bool english)
        {
            if(!MainSagaCanConclude)return "";
            return english?
                "\n\nOrrin unfolds the old supply order beside the new covenant. Under the scraped-off military heading lie three older civilian routing stamps. The first outer-ring diversion predates his command by nine years.\n\n“I signed orders that hurt people,” he says. “But the machine I inherited made every bad choice look like nobody's choice. Mira hid names to save one boat. Ivo diverted water to save his child. Sen silenced a bell to protect children at a gate. Others paid for each act. None is a monster; none may vanish from the record.”\n\nLyra lays down the witnesses' own signatures. The question now is not who rules three cores, but which risks the people themselves will agree to carry.":
                "\n\n厄洛把旧供能令摊在新的协定旁。刮去的军方标题下，有三个更早的民用调度印。外环第一次被牺牲，比他接管堡垒早九年。\n\n“伤人的命令，我确实签过。但我继承的也是一部把每个坏决定变成‘没人负责’的机器。米拉为了救一艘船藏起名字，伊沃为了儿子改了供水，森为了保住孩子捂住警钟。代价却落在另一群人身上。他们谁都不是怪物，也谁都不能从记录里消失。”\n\n莉雅放下三份当事者亲签的证词。现在决定的不再是三枚星核归哪个统治者，而是这些居民愿意共同承担怎样的风险。";
        }
        private string MainSagaEndingAfterword(bool english)
        {
            if(!MainSagaActive||MainSagaVoices<3||MainSagaTraces<3)return "";
            return english?
                "\n\nA second volume of first-hand accounts enters the public archive. "+
                (Has("saga_choice_5_a")?"Mira signs the open manifest. ":"Mira lets families read it first. ")+
                (Has("saga_choice_6_a")?"Ivo publishes his old ration decision. ":"Every well receives Ivo's repair plan. ")+
                (Has("saga_choice_7_a")?"Sen names his choice at the public hearing. ":"Rescued children and late evacuees tell their story together. ")+
                (Has("saga_lastgate_open")?"The energy accounts become a public ledger, open to challenge by any affected citizen. ":"Survivor locations remain protected, but a resident panel reviews every energy transfer. ")+
                "No signature erases a death, but people can now challenge the next secret order before it takes effect.":
                "\n\n边境公共档案终于添上第二册。"+
                (Has("saga_choice_5_a")?"米拉在公开的名册上亲笔署名。":"米拉先让失踪者家属读完原件。")+
                (Has("saga_choice_6_a")?"伊沃公开了自己九年前改写的配额。":"每座井站都收到了伊沃的维修图。")+
                (Has("saga_choice_7_a")?"森在听证会上说出自己的选择。":"获救的孩子与错过撤离的人一起讲完那夜。")+
                (Has("saga_lastgate_open")?"每次供能调度都记录在公开账本里。":"幸存者安置地点受到保护，每次调度仍要接受居民代表的封闭审查。")+
                "任何签名都无法抹平伤亡，但居民终于能在下一道秘密命令生效前提出质疑。";
        }
        private void MainSagaGate()
        {
            string state=MainSagaVoices<3?
                "三位当事人的证词还没有全部带来。":
                save.frontierEnding==0?
                "三地证词已经在手，但边境的共同供能方案尚未由三方议会签署。":
                MainSagaCouncilReady?
                "三方协定已经签好。去堡垒的三处旧记忆台重新接通最后的应急供能线路，才能决定星核归属。":
                "你仍需带回各地的原始调查记录。";
            string enState=MainSagaVoices<3?
                "The three first-hand accounts are not yet complete.":
                save.frontierEnding==0?
                "The testimonies are here, but the three-party council has not signed an energy covenant.":
                MainSagaCouncilReady?
                "The covenant is signed. Complete the three physical citadel memory stations and reconnect the last emergency circuit before choosing the cores' fate.":
                "The original regional investigations are still incomplete.";
            Dialog("厄洛 · 星卫统领","Orrin · Starwarden",
                "厄洛看着堡垒墙上的旧供能图。“我愿意为自己的选择负责。但你从远岸带回的证据说明，牺牲外环的决定比我的命令还要早。不能只在这里决定一次，就让所有人以为事情已经结束。”\n\n"+state+
                "\n\n“去问那些曾经被算作数字的人。等他们也有权决定，才谈星核归属。”",
                "Orrin studies the old supply map. “I will answer for my choices. But your farshore evidence shows the outer ring was sacrificed long before my command. We cannot settle this with one decision here and pretend that is the end.”\n\n"+enState+
                "\n\n“Ask those once reduced to numbers. When they can take part in the decision, come back for the starcores.”",
                C("查看主线下一步","Review the next main chapter","saga_track"),
                C("回镇上找莉雅","Return to Lyra","goto0"),
                C("继续调查","Continue investigating","close"));
        }
        private bool ChooseMainSaga(string action)
        {
            if(string.IsNullOrEmpty(action)||
               !action.StartsWith("saga_",StringComparison.Ordinal))return false;
            WorldObject host=pendingDialogueSource;
            if(action.StartsWith("saga_afterword_",StringComparison.Ordinal))
            {
                int room;
                if(int.TryParse(action.Substring(15),out room)&&
                    MainSagaActive&&save.ending>0&&
                    Has("saga_voice_"+room)&&SagaValidSpeaker(host,room))
                    MainSagaAfterwordDialogue(host,room);
                return true;
            }
            if(ChooseMainSagaLastGate(action))return true;
            if(action=="saga_guard_begin"||action=="saga_guard_abort"||
                action=="saga_guard_device")
            {
                int room=save.room;
                if(!MainSagaActive||save.ending>0||
                    !Has("boss_4")||!Has("saga_trace_"+room)||
                    !Has("saga_outer_"+room)||!Has(SagaReport(room))||
                    Has("saga_voice_"+room)||Has("saga_guard_"+room)||
                    !SagaValidSpeaker(host,room))return true;
                if(action=="saga_guard_abort")
                {
                    if(SagaGuardActive)ResetSagaGuard();
                    revision++;sound="ui";return true;
                }
                if(action=="saga_guard_device")
                {
                    if(!SagaGuardDeviceReady)return true;
                    int timber=room==5?1:0,herb=room==7?1:0;
                    int stamina=room==6?18:0;
                    if(save.inventory[0]<timber||save.inventory[2]<herb||
                        save.energy<stamina)
                    {
                        Notify("发动这个实际现场装置，还缺少木材、药草或耐力。",
                            "The live regional mechanism needs its actual timber, herb or stamina cost.");
                        return true;
                    }
                    save.inventory[0]-=timber;
                    save.inventory[2]-=herb;save.energy-=stamina;
                    sagaGuardDeviceUsed=true;
                    // Foghorn disperses the most pressure; heat vent and
                    // brazier buy time as well as interrupting real enemies.
                    sagaGuardBreach=Math.Max(0f,sagaGuardBreach-
                        (room==5?42f:room==6?30f:36f));
                    if(sagaGuardBreach<30f)sagaGuardBreachWarned=false;
                    Point center=new Point(host.x,host.y);
                    int interrupted=0;
                    foreach(Enemy e in sagaGuardFoes)
                        if(e!=null&&e.hp>0&&enemies.Contains(e)&&
                            (e.Position-center).Length<=20f)
                        {
                            ApplyEnemyControl(e,1.35f);
                            e.armorOpenTime=Math.Max(e.armorOpenTime,.9f);
                            interrupted++;
                        }
                    if(room==5)save.energy=Math.Min(100f,save.energy+8f);
                    if(room==6)save.heat=Math.Max(0f,save.heat-18f);
                    if(room==7)
                    {
                        save.cold=Math.Max(0f,save.cold-18f);
                        save.health=Math.Min(save.maxHealth,save.health+10f);
                    }
                    effects.Add(new Effect("burst",center,.7f)
                        {radius=3.2f,variant=room-5});
                    sound="reward";revision++;
                    Notify("现场装置已启动：真实打断"+interrupted+"名袭击者，本轮无法再用。",
                        "The real device interrupts "+interrupted+" raiders; it cannot be reused during this defense.");
                    return true;
                }
                if(SagaGuardActive||FieldWorkPreventsRest||!SafeTravel)return true;
                sagaGuardRoom=room;sagaGuardWave=0;
                sagaGuardBreach=0f;sagaGuardBreachWarned=false;
                sagaGuardMap=Map;sagaGuardSave=save;
                sagaGuardWitness=host;sagaGuardClock=(room==5?175f:room==6?185f:200f)+
                    (EarlyRescueEcho(room)?25f:0f)+
                    // Residents helped on the distant route return and give
                    // the defender twenty more real-world seconds.
                    (Has("saga_field_aid_done_"+room)?20f:0f);
                if(!SpawnSagaGuardWave(1))
                {
                    ResetSagaGuard();
                    Notify("证人附近没有两条安全的真实敌人进路。调整位置后重试。",
                        "The original witness site has no two safely reachable raider feet. Reposition and retry.");
                    return true;
                }
                sound="alert";revision++;
                Notify("两波追兵正在逼近证人！敌人进入证人附近会降低证据安全度；击退、打断或启用机关阻止抢夺。",
                    "Two waves approach! Raiders near the witness lower evidence safety. Fight, stagger or use the local device to prevent seizure.");
                return true;
            }
            if(action.StartsWith("saga_field_aid_",StringComparison.Ordinal)||
               action.StartsWith("saga_field_record_",StringComparison.Ordinal))
            {
                bool aid=action.StartsWith("saga_field_aid_",StringComparison.Ordinal);
                string prefix=aid?"saga_field_aid_":"saga_field_record_";
                int room;
                if(!int.TryParse(action.Substring(prefix.Length),out room)||
                    room<5||room>7||!MainSagaActive||save.ending>0||
                    !Has("boss_4")||save.room!=room||
                    !Has(SagaReport(room))||!Has("saga_trace_"+room)||
                    Has("saga_outer_"+room)||Dying||practicing||
                    mode!="world"||host==null||
                    host.id!=SagaOuterId(room)||
                    pendingDialogueSourceRoom!=room||
                    !SourceBelongsToActiveMap(host)||
                    !HasPhysicalInteractionReach(host)||!CanStand(Position))
                    return true;
                int resource=room==5?0:room==6?4:2;
                if(aid&&save.inventory[resource]<1)
                {
                    Notify("缺少对应补给。可以返回补充，也可以先封存原始记录。",
                        "You lack the supply. Return with it, or preserve the original record.");
                    return true;
                }
                if(aid)save.inventory[resource]--;
                Flag("saga_outer_"+room);
                Flag((aid?"saga_field_aid_done_":"saga_field_record_done_")+room);
                RecordMoment(aid?
                    "第二幕 · 在边远证词现场留下真正的救援补给，并保留了双方的笔迹。":
                    "第二幕 · 保存了原始证词，也明确留下了尚未解决的现场求援。",
                    aid?
                    "Act Two: supplied concrete help to those at the remote scene and kept both sides of the account.":
                    "Act Two: secured the original testimony and left a clear request for further relief.");
                revision++;sound="quest";
                string[] aidZh={"","","","","",
                    "木板终于搭上船舷。一个孩子悄悄把自己弟弟的木牌塞进你的手里：“不要再把他算成货物。”",
                    "半桶回流水先递给了孩子。拄拐的老人指着自己的手：“这里每道疤，都是我们先修井留下的。”",
                    "伤者服药后才敢合眼。门框背后还有一列名字，写的都是没能听到钟声的人。"};
                string[] recordZh={"","","","","",
                    "你把两份笔迹展平，记下那些没有上船的人。坏跳板仍在响，旁边的渡工只能等下一趟。",
                    "你把九年前的数字抄在防水纸上。回流管仍旧漏着，身后的居民没有假装已经得救。",
                    "你包好两封求救信，给后来的人留下醒目的求药路标。伤者仍在咳嗽，等待下一位赶路者。"};
                string[] aidEn={"","","","","",
                    "The plank reaches the boat. A child presses his brother's wooden token into your hand: “Don't count him as cargo again.”",
                    "Half a bucket goes to the children first. An elder points to scarred hands: “We repaired these wells long before he claimed the plan.”",
                    "The patient closes their eyes after the medicine. Another list is carved behind the door: names of those who never heard the bell."};
                string[] recordEn={"","","","","",
                    "You flatten both sets of names. The plank still rattles, and a ferryman watches the water for the next boat.",
                    "You copy the old figures on waterproof paper. The return pipe still leaks. The waiting people do not pretend otherwise.",
                    "You wrap both distress notes and leave a clear medicine request on the trail. Someone in the shelter is still coughing."};
                Dialog(host.name,host.en,
                    (aid?aidZh[room]:recordZh[room])+
                    "\n\n将两种声音带回去。证人必须听见你在这里做出的选择。",
                    (aid?aidEn[room]:recordEn[room])+
                    "\n\nCarry both voices back. The witness deserves to hear what you chose.",
                    C("回去找当事人","Return to the witness","close"));
                return true;
            }
            if(action=="saga_begin"||action=="saga_debrief")
            {
                if(host==null||host.kind!="guild"||
                    save.room!=0||pendingDialogueSourceRoom!=0||
                    !SourceBelongsToActiveMap(host)||
                    !HasPhysicalInteractionReach(host)||!CanStand(Position)||
                    Dying||practicing||save.stage<3||save.ending>0)return true;
                if(!MainSagaActive)
                {
                    Flag("saga_act_two");
                    TrackQuest("main");revision++;sound="quest";
                }
                MainSagaDebrief(host);
                return true;
            }
            if(action=="saga_track")
            {
                // A previously validated physical guild/citadel conversation
                // can track the real main quest; no milestones are fabricated.
                if(!MainSagaActive||save.ending>0)return true;
                TrackQuest("main");
                journalFocusRequest="main";mode="journal";
                return true;
            }
            if(action.StartsWith("saga_account_",StringComparison.Ordinal))
            {
                int room;
                if(!int.TryParse(action.Substring(13),out room)||
                    !MainSagaActive||save.ending>0||
                    !Has("boss_4")||!SagaValidSpeaker(host,room)||
                    !Has(SagaReport(room)))return true;
                MainSagaAccount(host,room);
                return true;
            }
            if(action.StartsWith("saga_sign_",StringComparison.Ordinal))
            {
                string[] bits=action.Substring(10).Split('_');
                int room;
                if(bits.Length!=2||!int.TryParse(bits[0],out room)||
                    (bits[1]!="a"&&bits[1]!="b")||
                    !MainSagaActive||save.ending>0||
                    !Has("boss_4")||!SagaValidSpeaker(host,room)||
                    !Has(SagaReport(room))||!Has("saga_trace_"+room)||
                    !Has("saga_outer_"+room)||
                    !Has("saga_guard_"+room)||
                    Has("saga_voice_"+room))return true;
                Flag("saga_voice_"+room);
                Flag("saga_choice_"+room+"_"+bits[1]);
                RecordMoment("第二幕 · "+host.name+
                    "在亲眼查验的证据前签下证词。"+
                    (bits[1]=="a"?"原件与责任一同公开。":"先让当事者参与决定证词的发布。"),
                    "Act Two: "+host.en+
                    " signed a first-hand account against inspected evidence. "+
                    (bits[1]=="a"?"The original record and responsibility were made public.":
                    "The affected people helped decide how to release the record."));
                revision++;sound="quest";
                Dialog(host.name,host.en,
                    "“谢谢你没有只在公会账上给我画一个勾。我的名字会跟着这份供词，不会因为星核终于归位就消失。”\n\n"+
                    "三份证词进度："+MainSagaVoices+"/3。",
                    "“Thank you for not treating me as one more checked box in a guild ledger. My name stays with this account, even when the starcores find their place.”\n\n"+
                    "First-hand accounts: "+MainSagaVoices+"/3.",
                    C("继续第二幕","Continue Act Two","close"));
                return true;
            }
            return true;
        }
    }
}
