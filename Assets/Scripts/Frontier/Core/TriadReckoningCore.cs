using System;

namespace Starfall
{
    // The story's missing fourth-chapter bridge: investigate the real cost of
    // each of the three collected cores, defeat the shield's audit defense,
    // and return the evidence to Lyra before confronting Orrin.
    // Persistent state uses only existing SaveData v2 string flags.
    public sealed partial class GameCore
    {
        public float TriadTime { get; private set; }
        public bool TriadDefenseActive { get; private set; }
        private float triadSurgeTime;
        private bool triadBossBreakUsed;
        public bool TriadBossInterruptReady
        { get { return save.room==4 && Has("triad_complete") &&
            !triadBossBreakUsed && !Has("boss_4") &&
            enemies.Exists(e=>e.boss && e.hp>0); } }
        public bool IsTriadEnemy(Enemy e)
        { return e!=null && e.id==89960; }
        private bool TriadEligible
        { get { return save.stage>=3 && Has("core_1") && Has("core_2") && Has("core_3"); } }
        private Point TriadPoint(string kind)
        { return ObjectPoint(4,kind,new Point(-100,-100)); }
        public int TriadEvidence
        { get { int n=0;for(int i=1;i<=3;i++)if(Has("triad_evidence_"+i))n++;return n; } }

        private void ResetTriadDefense()
        {
            enemies.RemoveAll(IsTriadEnemy);
            projectiles.RemoveAll(p=>p.triadOwned);
            hazards.RemoveAll(h=>h.triadOwned);
            effects.RemoveAll(e=>e.triadOwned);
            TriadDefenseActive=false;TriadTime=0;triadSurgeTime=0;
            triadBossBreakUsed=false;
        }
        private bool TalkTriadGuild(WorldObject o)
        {
            if(o.kind!="guild" || !TriadEligible || Has("boss_4"))return false;
            if(Has("triad_complete"))
            {
                Dialog(o.name,o.en,
                    "三核审计已交给居民。我们有证据证明屏障拿走了哪三份供给，也知道让旧系统停机不会毁掉所有星根。厄洛仍掌握城门；是否改变他，必须由你亲自面对。",
                    "The three-core audit is public. We can prove which three supplies the shield has taken, and that retiring an obsolete order need not destroy Starroot. Orrin still holds the citadel; confront him yourself.",
                    C("前往堡垒决战","Confront the warden","routes"),
                    C("继续准备","Keep preparing","close"));
                return true;
            }
            if(!Has("triad_started"))
            {
                Dialog(o.name,o.en,
                    "莉雅：三枚星核送到了这里，我第一次看见完整的支出表。森林失去再生力，遗迹水闸忘记了行人，矿场连停机都不被允许。你不能只拿着三个战利品去说服厄洛。\n\n我发现堡垒还有一座供能审计台和三枚记忆印记。亲自读取它们，找出究竟是谁把居民变成屏障的燃料；那里还有一套仍在执行旧命令的保密卫兵。这会成为第四章的关键证据。旧有的通路不会因此被封闭。",
                    "Lyra: Three cores are here, and at last I can read the full ledger. The forest lost renewal, sluices forgot travelers and miners were forbidden to stop. Three trophies cannot persuade Orrin by themselves.\n\nThe citadel holds a power-audit console and three memory seals. Read them to identify who paid for the shield; an obsolete security protocol still guards the records. This investigation ties our first three chapters to the confrontation. Existing routes stay open.",
                    C("开始第四章 · 三核审计","Begin Chapter Four · Three-Core Audit","triad_accept"),
                    C("先准备一下","Prepare first","close"));
                return true;
            }
            if(Has("triad_sealed"))
            {
                Dialog(o.name,o.en,
                    "莉雅：你的三份原始记录揭露了一个危险的事实——旧审计协议只计算屏障输出，却把森林、河道和工人的损失归为零。我要把这些证据公开，让厄洛无法再说他不知道代价。\n\n明白了，现在行动的不再只有你一个人。",
                    "Lyra: Your original readings reveal the flaw: the old audit counted shield output while assigning zero value to forests, rivers and workers. Let us publish this proof so Orrin can no longer claim ignorance.\n\nYou are no longer acting alone.",
                    C("公开三核真相 · 完成审计","Publish the findings · Complete the audit","triad_publish"),
                    C("再考虑一下","Return later","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "莉雅：去星陨堡垒找到三枚记忆印记。依次比较森林再生、水闸通行与矿工休息。三份证据齐全后，中央审计台的警卫程序会现身。击败它，手动保全日志，再带回来。",
                "Lyra: Find three memory seals in the citadel: forest renewal, passage through the sluices and miners' right to rest. Once all three are read, defeat the security protocol at the central audit desk, seal the logs and bring them home.",
                C("追踪第四章主线","Track Chapter Four","triad_track"),
                C("我去调查","I will investigate","close"));
            return true;
        }
        private bool TalkTriad(WorldObject o)
        {
            if(!o.kind.StartsWith("triadmemory") && o.kind!="triadnexus")return false;
            if(save.room!=4)return true;
            if(!TriadEligible || !Has("triad_started"))
            {
                Notify("旧屏障日志已加密。先把三枚星核送交莉雅，听取她的调查任务。",
                    "The shield ledger is encrypted. Deliver three cores to Lyra and accept her investigation.");
                return true;
            }
            if(o.kind=="triadnexus")
            {
                if(TriadBossInterruptReady)
                {
                    Dialog(o.name,o.en,
                        "三核审计已经成为公开的证据。厄洛正在使用旧屏障，审计台尚有一项合法但早被封存的指令：暂停下一轮护盾供能。需要25耐力，每场决战只能使用一次；它只能制造破绽，不能直接击败统领。",
                        "The public three-core audit authorizes an emergency shield pause. Orrin is still channeling the old defense. Spend 25 energy to interrupt his next attack and create an opening, once per boss attempt. This cannot defeat him on its own.",
                        C("执行公开审计 · 中断屏障25耐力",
                            "Invoke public audit · interrupt shield (25 energy)","triad_boss_interrupt"),
                        C("继续战斗","Keep fighting","close"));
                    return true;
                }
                if(triadBossBreakUsed && enemies.Exists(e=>e.boss))
                {
                    Notify("本场决战已经使用过公开审计中断。继续凭真正的战斗击败厄洛。",
                        "Public audit interruption already used this attempt. Defeat Orrin through real combat.");
                    return true;
                }
                if(Has("triad_sealed") || Has("triad_complete"))
                {
                    Dialog(o.name,o.en,
                        "你留下的审计副本仍能证明，安全不该只记录城墙里面的人。",
                        "Your audit copy still proves that safety must include those outside the walls.",
                        C("收起记录","Close records","close"));return true;
                }
                if(TriadEvidence<3)
                {
                    Dialog(o.name,o.en,
                        "中央审计台仍在执行九年前的密钥：没有森林、遗迹与矿场三份真实供能账目，任何人都不能关闭屏障耗能记录。",
                        "The central audit is locked by a nine-year-old key. Recover the forest, ruins and mine ledgers before opening the shield expenditure log.",
                        C("返回调查","Continue collecting","close"));return true;
                }
                if(TriadDefenseActive)
                {
                    Dialog(o.name,o.en,
                        "最后的旧安全协议已启动。它会用延迟脉冲封锁你的当前位置。观察警戒圈，击败装甲守卫，然后再保全记录。",
                        "The obsolete security protocol is active. Delayed shield pulses target your current position. Dodge the warning, defeat the armored guard, then preserve the ledger.",
                        C("继续战斗","Continue battle","close"),
                        C("中止本轮破解","Abort the attempt","triad_abort"));return true;
                }
                if(Has("triad_defense_clear"))
                {
                    Dialog(o.name,o.en,
                        "最后的保密程序停止了，原始审计日志终于能读：三枚星核曾各自调节土地、水位和矿机；堡垒把三条供给全部改写成统一屏障，连居民的损失都不计入。",
                        "The final security protocol is down. The original audit is clear: three cores once balanced soil, water and machinery. The citadel rewired all three to a single shield, and erased the human cost.",
                        C("保全原始日志","Preserve the original log","triad_seal"),
                        C("返回查看","Inspect later","close"));return true;
                }
                Dialog(o.name,o.en,
                    "三个记忆印记已经互相印证。启动旧审计台将召来210生命的装甲保密守卫。你有85秒完成实际战斗，地面会出现可以闪避的延迟护盾脉冲。失败可以重试，无法靠读取日志直接获胜。",
                    "The three memories corroborate one another. Opening the audit summons a 210-HP armored security guardian. Defeat it within 85 seconds while dodging delayed shield pulses; retries are free. Reading a ledger is not a victory by itself.",
                    C("启动关键事件 · 打破保密协议","Begin key event · Break secrecy","triad_defense_start"),
                    C("先准备","Prepare first","close"));return true;
            }
            int site;
            if(!int.TryParse(o.kind.Substring("triadmemory".Length),out site) ||
                site<1 || site>3)return true;
            if(Has("triad_evidence_"+site))
            {
                Notify("这份星核账目已经记录在第四章手帐中。",
                    "This core ledger is already in Chapter Four's journal.");
                return true;
            }
            string[] titlesZh={"","森林 · 能量的第一处缺口","遗迹 · 被改写的水闸","矿场 · 不准停止的机器"};
            string[] titlesEn={"","Forest · First Missing Balance","Ruins · Sluice Without Passage","Mine · A Machine Forbidden to Stop"};
            string[] storiesZh={"",
                "旧记录里，荆棘密林曾吸收多余的潮水，滋养受损根系。堡垒将该份余量改为持续供给屏障。该恢复哪一种原有用途？",
                "石碑的古调谐网曾在水位变化时给行人留出通过时间；后来的公会把通行权改为私有许可。该恢复哪个读数？",
                "矿机原先在矿脉过热时自动暂停，让工人安全撤离；军用配额却把每一秒停机判作错误。审计应该承认什么？"};
            string[] storiesEn={"",
                "Thornwood once used surplus energy to renew damaged roots and shelter wet soil. The citadel rerouted it into continuous shield output. Which purpose belongs in the original ledger?",
                "The sluices once left time for travelers between tides. Guild credentials later privatized passage. Which value belongs in the original ledger?",
                "The mining engines originally paused when the veins overheated so workers could escape. Military quotas turned every pause into an error. What must the audit recognize?"};
            Dialog(titlesZh[site],titlesEn[site],storiesZh[site],storiesEn[site],
                C(site==1?"把能源全部供给屏障":site==2?"只允许持证商队通过":"保持矿机全速运转",
                    site==1?"Feed all surplus to the shield":site==2?"Allow licensed freight only":"Run extraction without pause",
                    "triad_answer_"+site+"_0"),
                C(site==1?"把余量还给根系再生":site==2?"还原公共低潮通行":"仅在夜间停工",
                    site==1?"Restore root renewal":site==2?"Restore public ebb passage":"Pause only overnight",
                    "triad_answer_"+site+"_1"),
                C(site==1?"永久关闭整片森林":site==2?"彻底封死全部水闸":"允许过热停机与居民撤离",
                    site==1?"Close the entire forest":site==2?"Seal all sluices":"Permit heat shutdown and worker evacuation",
                    "triad_answer_"+site+"_2"),
                C("先检查日志","Review later","close"));
            return true;
        }
        private bool ChooseTriad(string action)
        {
            if(action=="triad_track"){TrackQuest("triad_reckoning");return true;}
            if(action=="triad_boss_interrupt")
            {
                if(!TriadBossInterruptReady ||
                    (Position-TriadPoint("triadnexus")).Length>2.2f ||
                    Dying || practicing)return true;
                if(save.energy<25)
                {
                    Notify("需要25耐力，不能免费中断屏障。",
                        "25 energy is required; the shield cannot be interrupted for free.");
                    return true;
                }
                Enemy orrin=enemies.Find(e=>e.boss && e.hp>0);
                if(orrin==null)return true;
                save.energy-=25;triadBossBreakUsed=true;
                orrin.stun=Math.Max(orrin.stun,2.35f);
                orrin.releaseTime=0;orrin.state=0;
                orrin.timer=Math.Max(orrin.timer,2.1f);
                effects.Add(new Effect("break",orrin.Position,.72f,
                    L("审计中断","AUDIT INTERRUPT")));
                revision++;sound="guard";
                Notify("审计中断成功！厄洛的下一次护盾攻击被推迟，趁破绽反击。",
                    "Public audit interrupt succeeded! Orrin's next shield attack is delayed; counter during the opening.");
                return true;
            }
            if(action=="triad_accept")
            {
                if(save.room!=0 || !TriadEligible || Has("triad_started") ||
                    Has("boss_4") || (Position-ObjectPoint(0,"guild",new Point(-100,-100))).Length>2.2f)return true;
                Flag("triad_started");TrackQuest("triad_reckoning");revision++;sound="quest";
                RecordMoment("第四章开启：莉雅发现三枚星核被改写成一条屏障供能账，要求亲自前往堡垒核查。",
                    "Chapter Four investigation: Lyra finds three starcores were rewired to one shield and asks for direct proof.");
                Notify("第四章关键调查开启：前往堡垒读取三枚星核记忆。",
                    "Chapter Four investigation begun: recover three core memories in the citadel.");
                return true;
            }
            if(action.StartsWith("triad_answer_"))
            {
                int site,answer;string[] parts=action.Split('_');
                if(parts.Length!=4 || !int.TryParse(parts[2],out site) ||
                    !int.TryParse(parts[3],out answer) || site<1 || site>3 ||
                    answer<0 || answer>2 || save.room!=4 ||
                    !Has("triad_started") || !TriadEligible ||
                    (Position-TriadPoint("triadmemory"+site)).Length>2.2f ||
                    Has("triad_evidence_"+site))return true;
                int correct=site==3?2:1;
                if(answer!=correct)
                {
                    save.energy=Math.Max(0,save.energy-12);
                    revision++;sound="deny";
                    Notify("推导与真实历史冲突，审计台拒绝写入。能量 -12；可重新核对。",
                        "That claim contradicts the original records. Energy -12; reconsider the evidence.");
                    return true;
                }
                Flag("triad_evidence_"+site);
                effects.Add(new Effect("discover",Position,.65f));
                RecordMoment(site==1?"第一核：归还森林再生的供给。":site==2?
                    "第二核：水闸属于实际走过的人。":"第三核：工人有权让矿机停止。",
                    site==1?"First core: restore forest renewal.":site==2?
                    "Second core: sluices serve real travelers.":"Third core: miners must be allowed to stop.");
                revision++;sound="quest";
                Notify("原始证据 "+TriadEvidence+"/3 已保全。前往下一座印记或中央审计台。",
                    "Original evidence "+TriadEvidence+"/3 preserved. Proceed to the next seal or central audit.");
                return true;
            }
            if(action=="triad_defense_start")
            {
                if(save.room!=4 || !TriadEligible || !Has("triad_started") ||
                    TriadEvidence!=3 || TriadDefenseActive || Has("triad_defense_clear") ||
                    Has("boss_4") || practicing || Dying || !SafeTravel ||
                    commonsRelayRoom!=0 || RootHarmonyRoom!=0 ||
                    atlasEchoRoom!=0 || reliefMissionRoom!=0 ||
                    (Position-TriadPoint("triadnexus")).Length>2.2f)return true;
                Point foe=new Point(21.5f,11.5f);
                if(!CanStand(foe) || !ClearAttackPath(Position,foe) ||
                    NavigationPath(foe).Count==0)return true;
                Enemy guardian=new Enemy(89960,2,foe.x,foe.y,210);
                guardian.timer=.9f;
                enemies.Add(guardian);
                TriadDefenseActive=true;TriadTime=85;triadSurgeTime=3.2f;
                revision++;sound="quest";
                Notify("旧保密协议启动！在85秒内击败守卫，并闪避延迟护盾脉冲。",
                    "Audit security activated! Defeat the guard within 85 seconds; dodge delayed shield pulses.");
                return true;
            }
            if(action=="triad_abort")
            {
                if(TriadDefenseActive && save.room==4 &&
                    (Position-TriadPoint("triadnexus")).Length<=2.2f)
                {
                    ResetTriadDefense();revision++;
                    Notify("审计防卫已停用，原始三份读数保留。可以再次挑战。",
                        "Audit defense stopped. The three real readings remain; retry whenever ready.");
                }
                return true;
            }
            if(action=="triad_seal")
            {
                if(save.room!=4 || !TriadEligible || !Has("triad_defense_clear") ||
                    Has("triad_sealed") || TriadDefenseActive ||
                    (Position-TriadPoint("triadnexus")).Length>2.2f)return true;
                Flag("triad_sealed");revision++;sound="quest";
                RecordMoment("中央审计日志保全：屏障曾把三座星根锚点的生机当作零成本供能。",
                    "Central audit preserved: the shield valued three living anchors as zero-cost fuel.");
                Notify("原始审计日志已封存，返回余烬镇将真相交给莉雅。",
                    "Original audit secured. Return to Lyra in Emberfall to publish the truth.");
                return true;
            }
            if(action=="triad_publish")
            {
                if(save.room!=0 || !TriadEligible || !Has("triad_sealed") ||
                    Has("triad_complete") || Has("boss_4") ||
                    (Position-ObjectPoint(0,"guild",new Point(-100,-100))).Length>2.2f)return true;
                Flag("triad_complete");
                save.coins+=55;save.shards+=2;AddXp(65);
                revision++;sound="reward";
                RecordMoment("莉雅公开了屏障的真实支出。厄洛将不得不面对森林、河水、矿工三章的证词。",
                    "Lyra published the shield's true ledger. Orrin must confront the forest, river and miner testimony.");
                Notify("主线第四章证据已公开！55硬币、2碎片、65经验；最终守卫屏障供能削弱。",
                    "Chapter Four evidence published! +55 coins, +2 shards, +65 XP; the warden's shield is weakened.");
                return true;
            }
            return false;
        }
        private void TickTriad(float dt)
        {
            if(!TriadDefenseActive)return;
            if(save.room!=4 || Dying || practicing)
            {ResetTriadDefense();revision++;return;}
            if(!enemies.Exists(IsTriadEnemy))
            {
                ResetTriadDefense();Flag("triad_defense_clear");
                revision++;sound="quest";
                Notify("保密守卫已被击败！返回中央审计台保全原始日志。",
                    "Audit guardian defeated! Return to the central console to preserve the original log.");
                return;
            }
            TriadTime-=dt;
            if(TriadTime<=0)
            {
                ResetTriadDefense();revision++;sound="deny";
                Notify("保密系统重新上锁。三份记忆保留，可重试实战突破。",
                    "Security relocked. Three memories remain; retry the combat challenge.");
                return;
            }
            triadSurgeTime-=dt;
            if(triadSurgeTime>0)return;
            triadSurgeTime=4.2f;
            Point at=Position;
            if(!CanStand(at))return;
            bool escape=false;
            foreach(Point step in new[]{new Point(1.75f,0),new Point(-1.75f,0),
                new Point(0,1.75f),new Point(0,-1.75f)})
                if(CanStand(at+step) && ClearAttackPath(at,at+step))
                {escape=true;break;}
            if(!escape)return;
            hazards.Add(new Hazard{position=at,radius=.93f,timer=1.35f,
                damage=18,kind="rift",triadOwned=true});
            effects.Add(new Effect("warn",at,1.2f){radius=.93f,variant=3,triadOwned=true});
        }
    }
}
