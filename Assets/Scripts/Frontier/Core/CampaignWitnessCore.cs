using System;
using System.Collections.Generic;

namespace Starfall
{
    // Main-campaign testimony from three regions, witnessed in the field.
    // Old saves remain valid: only permanent v2 flags are recorded.
    public sealed partial class GameCore
    {
        private int witnessDefenseRoom;
        // Each live witness defense has exactly two authored sentinels.
        // Missing actors never count as beaten without genuine lethal Hit.
        private int witnessConfirmedKills;
        // Pair members are authentic world Enemy instances, not a numeric
        // range. Foreign actors reusing 89972..89977 are not evidence guards.
        private Enemy witnessActorA,witnessActorB;
        private float witnessDefenseTime, witnessPulse;
        private void OnWitnessEnemyDefeated(Enemy enemy)
        {
            if (enemy == null || witnessDefenseRoom != save.room ||
                !IsWitnessEnemy(enemy)) return;
            if(System.Object.ReferenceEquals(enemy,witnessActorA))
                witnessConfirmedKills |= 1;
            if(System.Object.ReferenceEquals(enemy,witnessActorB))
                witnessConfirmedKills |= 2;
        }
        public int WitnessDefenseRoom { get { return witnessDefenseRoom; } }
        public float WitnessDefenseTime { get { return witnessDefenseTime; } }
        public bool IsWitnessEnemy(Enemy e)
        {
            return e!=null&&witnessDefenseRoom==save.room&&
                witnessDefenseRoom>=1&&witnessDefenseRoom<=3&&
                (System.Object.ReferenceEquals(e,witnessActorA)||
                 System.Object.ReferenceEquals(e,witnessActorB));
        }
        public int WitnessesFiled
        {
            get { int n=0; for(int i=1;i<=3;i++) if(Has("witness_complete_"+i))n++; return n; }
        }
        public bool AllWitnessesFiled { get { return WitnessesFiled==3; } }
        private Point WitnessPoint(int room,string part)
        { return ObjectPoint(room,"witness"+part+room,new Point(-100,-100)); }
        private bool WitnessEligible(int room)
        {
            return room>=1 && room<=3 && save.stage>=room-1 &&
                (room==1?Has("scout_saved"):Has("gate_"+room));
        }
        private void ResetWitnessDefense()
        {
            enemies.RemoveAll(IsWitnessEnemy);
            projectiles.RemoveAll(p=>p.witnessOwned);
            hazards.RemoveAll(h=>h.witnessOwned);
            effects.RemoveAll(e=>e.witnessOwned);
            witnessDefenseRoom=0;witnessConfirmedKills=0;
            witnessActorA=witnessActorB=null;
            witnessDefenseTime=0;witnessPulse=0;
        }
        private bool SpawnWitnessDefense(int region)
        {
            Point center=WitnessPoint(region,"site2");
            Point[] steps={new Point(2.5f,0),new Point(-2.5f,0),
                new Point(0,2.5f),new Point(0,-2.5f),
                new Point(3,2),new Point(-3,-2),
                new Point(-3,2),new Point(3,-2)};
            var chosen=new List<Point>();
            for(int j=0;j<steps.Length && chosen.Count<2;j++)
            {
                Point at=center+steps[(j+region)%steps.Length];
                if(!CanStand(at) || !ClearAttackPath(center,at) ||
                    NavigationPath(at).Count==0 || (at-Position).Length<1.6f)
                    continue;
                if(chosen.Exists(p=>(p-at).Length<1.8f))continue;
                if(enemies.Exists(e=>(e.Position-at).Length<1.5f))continue;
                chosen.Add(at);
            }
            if(chosen.Count!=2)return false;
            Enemy first=new Enemy(89970+region*2,
                region==1?0:region==2?1:2,chosen[0].x,chosen[0].y,
                region==1?60:region==2?85:110);
            Enemy second=new Enemy(89971+region*2,
                region==1?0:region==2?1:2,chosen[1].x,chosen[1].y,
                region==1?60:region==2?85:110);
            first.timer=.8f;second.timer=1.1f;
            witnessActorA=first;witnessActorB=second;
            enemies.Add(first);enemies.Add(second);
            witnessDefenseRoom=region;witnessConfirmedKills=0;
            witnessDefenseTime=65;
            witnessPulse=3.1f;
            return true;
        }
        private bool TalkWitness(WorldObject o)
        {
            int region=save.room;
            if(o==null||region<1 || region>3)return false;
            string kind=o.kind;
            bool resident=kind=="witnessnpc"+region;
            bool first=kind=="witnesssite1"+region;
            bool second=kind=="witnesssite2"+region;
            if(!resident && !first && !second)return false;
            if(!HasPhysicalInteractionReach(o))return true;
            string[] whoZh={"","密林巡路员 · 莫芽","遗迹渡口幸存者 · 阿菱","赤铁矿工 · 维恩"};
            string[] whoEn={"","Moya · Grove Roadwarden","Arin · Sluice Survivor","Venn · Ironreach Miner"};
            if(!WitnessEligible(region))
            {
                Notify(region==1?"先救出失踪侦察员，才能核对根系记录。":
                    region==2?"先打开遗迹三碑水闸，才能调查封闭渡口。":
                    "先修好矿场升降机，才能查验矿工的休息日志。",
                    region==1?"Rescue the missing scout before investigating the roots.":
                    region==2?"Unlock the original three tablets before inspecting the sluice.":
                    "Repair the mine lift before investigating workers' records.");
                return true;
            }
            if(Has("witness_complete_"+region))
            {
                if(resident)
                {
                    Dialog(whoZh[region],whoEn[region],
                        region==1?"受伤的根系已经能够自愈。我的证词不再会被称作采掘损耗。":
                        region==2?"公用闸门重新开放，渡口不再只认公会印章。":
                        "工房一旦过热，矿工有权停止机器。公会已经保留了我们的原始日志。",
                        region==1?"The wounded roots now heal. My testimony can no longer be written off as extraction loss.":
                        region==2?"The public gate now recognizes travelers, not just guild stamps.":
                        "When the foundry overheats, workers have the right to stop the machinery. The guild preserved our original logs.",
                        C("继续前行","Continue","close"));
                }
                else
                {
                    // The site is NOT the resident. After completing the
                    // testimony, an archived machine or marker must not
                    // magically speak under the witness NPC's portrait.
                    string[] firstZh={"",
                        "曾被列为废料的疗养根系正在抽出嫩芽。伤口旁挂着莫芽留下的记录牌；证人本人还在巡路岗。",
                        "旧水闸的记录台已改成公共通行告示。闸门的钥匙归还居民；阿菱本人仍在渡口讲述这一夜。",
                        "停机牌挂在过热警戒线上。工人签过字的安全条例仍在，维恩本人留在工房向后来的矿工说明。"};
                    string[] secondZh={"",
                        "封锁卫兵的警戒灯已经熄灭。疗养根系的原始记录保存在此，署名已送往公会。",
                        "旧封锁的铁链已被取下。渡口的姓名单按原样封存在现场，不再被记作货运。",
                        "守卫的巡检记录已开放。矿工关于撤离与停机的签名留在这台档案机里。"};
                    string[] firstEn={"",
                        "New shoots grow from the healing roots once listed as waste. Moya's field marker remains here; she herself is still with the roadwardens.",
                        "The sluice station now displays public passage rules. Its key belongs to the residents; Arin is at the ferry, not inside this ledger.",
                        "The shutdown placard hangs by the overheating line. Venn's signed safety rules remain here; he speaks to workers at the foundry."};
                    string[] secondEn={"",
                        "The obsolete security light has gone out. Original healing records remain at this archive; their signed copy is with the guild.",
                        "The old lock chains are gone. Passenger names are preserved here as written, not converted into lost freight.",
                        "The guard patrol log is public. The miners' evacuation and shutdown signatures remain in the archive."};
                    Dialog(o.name,o.en,
                        first?firstZh[region]:secondZh[region],
                        first?firstEn[region]:secondEn[region],
                        C("查看已保全记录","Inspect archived record","close"));
                }
                return true;
            }
            if(resident)
            {
                if(!Has("witness_started_"+region))
                {
                    Dialog(whoZh[region],whoEn[region],
                        region==1?"采矿车切断根系后，密林守卫把一个受伤的活根叫作障碍。我藏着旧日疗养记录：只要救下它，或许能证明星核原本会让森林自行愈合。":
                        region==2?"涨潮时我带着孩子们站在桥外，公会的门锁只认货物许可证。碑序复原了，却没人问过普通人该如何过桥。":
                        "监工要求我们不停挖矿。矿机原本有温度过高自动停机的功能，但我们的工时表把休息写成怠工。",
                        region==1?"After ore carts severed the roots, the security network marked a living wound as an obstacle. Save it, and we can prove the core once healed the forest.":
                        region==2?"My children waited beyond the rising river while guild locks recognized only freight licenses. Restoring tablets is not enough if ordinary people still cannot cross.":
                        "The overseer ordered endless extraction. Our machines were built to pause when too hot, but the military ledger calls every rest period insubordination.",
                        C("加入主线调查 · 亲自取证","Join the story investigation · Witness the truth","witness_begin_"+region),
                        C("稍后再说","Later","close"));
                    return true;
                }
                if(Has("witness_sealed_"+region))
                {
                    Dialog(whoZh[region],whoEn[region],
                        "你带回了真正经过核验的记录。把它交给莉雅，她将知道第四章该向厄洛问什么。",
                        "You brought back evidence verified in the field. Lyra can now ask Orrin what the shield really costs.",
                        C("签署证词 · 送交公会","Sign testimony · File with the guild","witness_finish_"+region),
                        C("稍后","Later","close"));
                    return true;
                }
                Dialog(whoZh[region],whoEn[region],
                    "去两处真实标记调查，第一处恢复原始用途，第二处解除封锁守卫并亲自保全记录。最后再回来交付。",
                    "Investigate two real field markers. Restore the original purpose at the first, defeat the sealing guards at the second, then return to file the testimony.",
                    C("追踪证人","Track testimony","witness_track"),
                    C("继续调查","Continue","close"));
                return true;
            }
            int step=first?1:2;
            if(!Has("witness_started_"+region))
            {
                Notify("先和当地证人交谈。","Speak with the local witness first.");
                return true;
            }
            if(first)
            {
                if(Has("witness_read_"+region))
                {Notify("这份原始记录已经被核实。","This original record is already verified.");return true;}
                string[] zh={"",
                    "根系在受伤时本应吸收余量修复土壤。原矿业指令把这种修复记成浪费。应该恢复什么？",
                    "日、月、潮碑序重新联通了水闸，但通行名单仍只记货运。闸门本来应该保护谁？",
                    "矿脉升温，旧工时表却要求满负荷不停工。什么才是正确的矿机安全指令？"};
                string[] en={"",
                    "The injured roots once drew reserve energy to repair soil. Extraction orders marked this healing as waste. Which purpose do you restore?",
                    "Sun, Moon and Tide reopened the sluices, but the travel ledger recognizes only freight. Whose crossing must be protected?",
                    "The vein overheats, yet military shifts demand full power without rest. Which instruction actually protects the workers?"};
                Dialog(o.name,o.en,zh[region],en[region],
                    C(region==1?"继续抽取所有能量":region==2?"保留货运优先权":"矿机必须永不停歇",
                        region==1?"Keep extracting every surplus":region==2?"Keep freight-only priority":"Mine engines must never pause",
                        "witness_answer_"+region+"_0"),
                    C(region==1?"给根系一株月光草，恢复自愈":region==2?"只给交过桥税的人过河":"让矿机只在午夜休息",
                        region==1?"Give the wounded roots a Moonherb to restore renewal":region==2?"Let only those who paid cross":"Rest the machinery only at midnight",
                        "witness_answer_"+region+"_1"),
                    C(region==2?"撤销许可证，开放公共渡口":"设立过热停机与撤离指令",
                        region==2?"Restore the public crossing":"Allow heat shutdown and evacuation",
                        "witness_answer_"+region+"_2"),
                    C("检查完毕，暂不修改","Inspect later","close"));
                return true;
            }
            if(!Has("witness_read_"+region))
            {
                Notify("先去第一处标记核实用途，不能跳过实地证词。",
                    "Inspect the first marker before attempting to preserve testimony.");
                return true;
            }
            if(Has("witness_sealed_"+region))
            {
                Notify("证据已保全，返回证人处签署。","Evidence preserved. Return to the witness to file it.");
                return true;
            }
            if(witnessDefenseRoom==region)
            {
                Dialog(o.name,o.en,
                    "旧保密卫兵还在攻击，警戒圈将延迟爆发。必须真实击败他们才能保全日志。",
                    "Obsolete security guards are attacking. Their warning circles explode after a delay. Defeat them in real combat to preserve the log.",
                    C("继续交战","Keep fighting","close"),
                    C("中止并重试","Withdraw and retry","witness_abort_"+region));
                return true;
            }
            if(Has("witness_defeated_"+region))
            {
                Dialog(o.name,o.en,
                    "旧协议不再封锁此处。将证人亲自核对的记录保存为不能被货运网络删除的公共证据。",
                    "The obsolete protocol is quiet. Preserve the verified record as public evidence the freight network cannot erase.",
                    C("保全本章原始证据","Preserve original testimony","witness_seal_"+region),
                    C("稍后","Later","close"));
                return true;
            }
            Dialog(o.name,o.en,
                region==1?"这里原本是受伤根系的疗养点，却被矿业机关划成清除目标。卫兵将阻止这段记录传出。":
                region==2?"旧水闸的应急通道原本给所有人开放，后来却被锁成只供货运的财产。":
                "矿工集体签过过热撤离的条款，军用监工随后把它们从账本中抹去。",
                region==1?"This healing root was classified as debris. The security guards will block the evidence.":
                region==2?"The emergency crossing was meant for everyone, until the guild reclassified it as freight property.":
                "Miners signed a heat-evacuation provision. Military overseers later erased it.",
                C("启动现场保全 · 击退封锁卫兵","Start preservation · Defeat guards","witness_defend_"+region),
                C("先准备","Prepare first","close"));
            return true;
        }
        private bool ChooseWitness(string action)
        {
            if(action=="witness_track"){TrackQuest("campaign_witness");return true;}
            if(!action.StartsWith("witness_"))return false;
            string[] parts=action.Split('_');
            int region;
            if(parts.Length<3 || !int.TryParse(parts[2],out region) ||
                region<1 || region>3)return true;
            if(save.room!=region || !WitnessEligible(region) || Dying || practicing)return true;
            if(action=="witness_begin_"+region)
            {
                if(Has("witness_started_"+region) ||
                    !HasPhysicalInteractionReach(WitnessPoint(region,"npc")))return true;
                Flag("witness_started_"+region);
                TrackQuest("campaign_witness");
                RecordMoment("第"+region+"章：有人愿意亲自讲述旧星核指令的代价。",
                    "Chapter "+region+": a living witness will testify to the cost of an obsolete core order.");
                Notify("主线证词调查已启动：先找第一处标记。","Story testimony begun. Seek the first field marker.");
                return true;
            }
            if(action.StartsWith("witness_answer_"))
            {
                if(parts.Length!=4 || !Has("witness_started_"+region) ||
                    Has("witness_read_"+region) ||
                    !HasPhysicalInteractionReach(WitnessPoint(region,"site1")))return true;
                int choice;
                if(!int.TryParse(parts[3],out choice))return true;
                int correct=region==1?1:region==2?2:2;
                if(choice!=correct)
                {
                    save.energy=Math.Max(0,save.energy-8);revision++;sound="deny";
                    Notify("该推断无法解释证人经历，耐力 -8。请核查原始用途。",
                        "That interpretation contradicts the witness; energy -8. Check the original purpose.");
                    return true;
                }
                if(region==1 && !Spend(0,0,1,0,0))return true;
                if(region==3 && !Spend(0,0,0,0,1))return true;
                Flag("witness_read_"+region);
                RecordMoment(region==1?"森林的能源曾治愈断裂的根系。":
                    region==2?"水闸本该让未持证的孩子和旅人过河。":
                    "矿机停工是生命安全指令，不是怠工。",
                    region==1?"Forest energy once healed damaged roots.":
                    region==2?"The sluices were built for people without guild papers.":
                    "Mine shutdown was a life-safety protocol, not insubordination.");
                Notify("第一段证词已核实。前往第二处标记解除保密封锁。",
                    "First testimony verified. Proceed to the second marker and break the security lock.");
                return true;
            }
            if(action=="witness_defend_"+region)
            {
                if(!Has("witness_read_"+region) ||
                    Has("witness_sealed_"+region) || Has("witness_defeated_"+region) ||
                    FieldWorkPreventsRest || witnessDefenseRoom!=0 || !SafeTravel || racing ||
                    reliefMissionRoom!=0 || roadTrialRoom!=0 ||
                    wayfarerEncounterRoom!=0 || atlasEchoRoom!=0 ||
                    RootHarmonyRoom!=0 || commonsRelayRoom!=0 ||
                    TemperActive || TriadDefenseActive ||
                    !HasPhysicalInteractionReach(WitnessPoint(region,"site2")))return true;
                if(!SpawnWitnessDefense(region))
                {
                    Notify("守卫接近路线暂时受阻，调整站位后再试。",
                        "Guard deployment path obstructed; reposition and retry.");
                    return true;
                }
                Notify("两名封锁守卫已出现！65秒内击败他们，避开延迟冲击。",
                    "Two security guards are live! Defeat them in 65 seconds, dodge delayed impacts.");
                revision++;sound="quest";return true;
            }
            if(action=="witness_abort_"+region)
            {
                if(witnessDefenseRoom==region &&
                    HasPhysicalInteractionReach(WitnessPoint(region,"site2")))
                {ResetWitnessDefense();revision++;
                    Notify("保全已暂停。先前核实的证词保留，可重试。",
                        "Preservation paused. Previous verified testimony remains for retry.");}
                return true;
            }
            if(action=="witness_seal_"+region)
            {
                if(!Has("witness_defeated_"+region) ||
                    Has("witness_sealed_"+region) ||
                    !HasPhysicalInteractionReach(WitnessPoint(region,"site2")))return true;
                Flag("witness_sealed_"+region);
                Notify("现场原稿已保全，返回证人身边签署记录。",
                    "Field original preserved. Return to the witness for signature.");
                return true;
            }
            if(action=="witness_finish_"+region)
            {
                if(!Has("witness_sealed_"+region) ||
                    Has("witness_complete_"+region) ||
                    !HasPhysicalInteractionReach(WitnessPoint(region,"npc")))return true;
                Flag("witness_complete_"+region);
                CreditCoins(region*10+15);
                CreditShards(1);
                AddXp(20+region*5);
                RecordMoment("第"+region+"章：证词已由活着的居民署名，将成为第四章三核审计的直接依据。",
                    "Chapter "+region+": a living witness signed evidence for the Chapter Four audit.");
                Notify("居民证词已签署并送交公会。第四章调查将引用这份记录。",
                    "Living testimony signed and delivered to the guild. Chapter Four will cite it.");
                return true;
            }
            return true;
        }
        private void TickWitnessDefense(float dt)
        {
            if(witnessDefenseRoom==0)return;
            int region=witnessDefenseRoom;
            if(save.room!=region || Dying || practicing)
            {ResetWitnessDefense();revision++;return;}
            // Missing from the collection is not proof of a combat kill.
            // Check the exact spawned actor instead of any similarly
            // numbered world enemy. Authenticated Hit callbacks are the
            // only way to register either original guard's defeat.
            for (int i = 0; i < 2; i++)
            {
                if ((witnessConfirmedKills & (1 << i)) != 0 ||
                    enemies.Contains(i==0?witnessActorA:witnessActorB)) continue;
                ResetWitnessDefense(); revision++;
                Notify("封锁守卫异常离场，未确认击败。现场记录保留，请重新发起保全。",
                    "A guard vanished without a confirmed defeat. Your evidence remains; restart the defense.");
                return;
            }
            if (!enemies.Exists(IsWitnessEnemy))
            {
                bool verified = witnessConfirmedKills == 3;
                ResetWitnessDefense();
                if (!verified) return;
                Flag("witness_defeated_"+region);
                Notify("真正的封锁卫兵已被击退。回第二处标记保全原始证据。",
                    "Actual security guards defeated. Return to the second marker to preserve the original.");
                return;
            }
            witnessDefenseTime-=dt;
            if(witnessDefenseTime<=0)
            {
                ResetWitnessDefense();revision++;
                Notify("证词封锁程序恢复。可以免费重新挑战守卫。",
                    "The witness lock rearmed. Retry the guards for free.");
                return;
            }
            witnessPulse-=dt;
            if(witnessPulse>0)return;
            witnessPulse=4.25f;
            Point at=Position;
            if(!CanStand(at))return;
            bool exit=false;
            foreach(Point shift in new[]{new Point(1.8f,0),new Point(-1.8f,0),
                new Point(0,1.8f),new Point(0,-1.8f)})
                if(CanStand(at+shift) && ClearAttackPath(at,at+shift))
                {exit=true;break;}
            if(!exit)return;
            hazards.Add(new Hazard{position=at,timer=1.3f,radius=.96f,
                damage=10+4*region,kind=region==1?"root":region==2?"tide":"iron",
                witnessOwned=true});
            effects.Add(new Effect("warn",at,1.2f){radius=.96f,variant=region+1,witnessOwned=true});
        }
    }
}
