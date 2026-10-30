using System;
using System.Collections.Generic;

namespace Starfall
{
    // Read-only journal projection from actual permanent campaign milestones.
    // It does not award progression, invent dialogue or alter SaveData v2.
    public sealed class StoryBeat
    {
        public readonly int chapter;
        public readonly string title, summary, next, questId;
        public readonly bool complete;
        public StoryBeat(int chapter, string title, string summary,
            string next, string questId, bool complete)
        {
            this.chapter=chapter;this.title=title;this.summary=summary;
            this.next=next;this.questId=questId;this.complete=complete;
        }
    }

    public sealed partial class GameCore
    {
        private void AddRecapBeat(List<StoryBeat> beats,int chapter,
            string nameZh,string nameEn,string summaryZh,string summaryEn,
            string questId,bool complete,string finishedZh,string finishedEn)
        {
            // Look up the actual quest at the time the player reads this page,
            // rather than snapshotting a stale hint when the journal opened.
            QuestGuide quest=GetQuest(questId);
            string next=complete?L(finishedZh,finishedEn):quest.next;
            beats.Add(new StoryBeat(chapter,L(nameZh,nameEn),
                L(summaryZh,summaryEn),next,questId,complete));
        }

        // Only after the original finale may the chronicle name WHICH ending
        // was chosen. This reflects the actual Epilogue, not a generic
        // "victory" that erases its distinct ecological and civic effects.
        private string EndingRecapSuffix()
        {
            if(save.ending==1)return L(
                " 你归还星核，堡垒屏障逐渐熄灭，河流与土地开始恢复；居民建立公共避难所承担新的风险。",
                " You returned the cores. The citadel shield faded as rivers recovered; residents built shared shelters to face the remaining risks.");
            if(save.ending==2)return L(
                " 你保留了屏障，也要求堡垒对城外人开放庇护；外部河流仍在变浅，莉雅公开屏障耗能的账目。",
                " You kept the shield but opened its refuge to outsiders. Rivers beyond the wall still decline, so Lyra publishes its energy cost.");
            if(save.ending==3)return L(
                " 你和同伴把星核能源逐步改为公共水泵、渡船与道路，并由居民共同维护，不再由堡垒独自决定。",
                " You and your allies turned core power toward public pumps, ferries and roads under resident stewardship, not the citadel alone.");
            return "";
        }

        // Resolve the Journey panel's real clickable continuation. A
        // completed old chapter stays historical, except for Chapter Twelve:
        // residents may still need to staff and provision the actual
        // waystations after publishing the initial road audit. Do not
        // re-track a finished audit or invent an unlocked quest.
        public string StoryRecapTrackableQuestId(StoryBeat beat)
        {
            if(beat==null)return null;
            if(!beat.complete)
            {
                QuestGuide current=GetQuest(beat.questId);
                return current.locked||current.complete?null:beat.questId;
            }
            if((beat.chapter==12||beat.chapter==16)&&beat.questId=="civic_aftercare")
            {
                QuestGuide next=GetQuest("civic_waystations");
                if(!next.locked&&!next.complete)
                    return next.id;
            }
            return null;
        }

        // Order is historical, visibility is NOT: the journal may only recap
        // chapters already reached or genuinely unlocked in this save slot.
        // No downstream chapter name, choice or outcome leaks on a new game.
        public List<StoryBeat> StoryRecap()
        {
            var beats=new List<StoryBeat>(12);
            AddRecapBeat(beats,1,"第一章 · 密林的星根",
                "Chapter One · The Forest Roots",
                Has("scout_saved")?
                    "侦察员已经找到被屏障抽取能量的地下星根。你要查清巨兽守护的祭坛出了什么问题，再把星核证据交给莉雅。":
                    "镇外侦察员在密林失踪。先找到他并调查异常的祭坛，才能弄清森林为什么枯萎。",
                Has("scout_saved")?
                    "The rescued scout found living roots drained to power the shield. Investigate the guarded altar and take the core evidence to Lyra.":
                    "A scout is missing in Thornwood. Find him and investigate the strange altar to learn why the forest is withering.",
                "main",save.stage>=1,
                "第一枚星核已交到公会，森林失去的能量仍需要追查。",
                "The first core reached the guild; the forest's missing energy still matters.");

            if(save.stage>=1)
                AddRecapBeat(beats,2,"第二章 · 遗迹与渡口",
                    "Chapter Two · The Ruins and the Crossing",
                    "第一枚星核带来新的疑问：旧遗迹的石碑和水路为何受到封锁？沿线索打开遗迹，寻找第二枚星核。",
                    "The first core raises new questions. Why were the old tablets and crossings sealed? Follow the records through the ruins to the second core.",
                    "main",save.stage>=2,
                    "遗迹的记录已找到，第二枚星核交回公会。",
                    "The ruins were investigated and the second core returned to the guild.");

            if(save.stage>=2)
                AddRecapBeat(beats,3,"第三章 · 矿场的代价",
                    "Chapter Three · The Cost of the Mines",
                    "矿场机器仍在运转，矿工却没有安全停机的权利。修好升降机，查明设备在为谁输送星核能量。",
                    "The mines keep running without giving workers a safe shutdown. Repair the lift and investigate who receives the starcore power.",
                    "main",save.stage>=3,
                    "第三枚星核已交回公会，三片地区的损失指向同一道屏障。",
                    "The third core reached the guild; the three regions' losses point to the same shield.");

            if(save.stage>=3)
                AddRecapBeat(beats,4,"第四章 · 三核真相",
                    "Chapter Four · The Three-Core Truth",
                    (Has("triad_complete")?
                        "莉雅已经公开森林、水路与矿工的原始记录。它们都指向同一取舍：屏障保护城内，却让城外居民付出代价。":
                        "三枚星核都被送往堡垒。前往与厄洛对峙之前，可以帮莉雅核对森林、水路和矿工的记录，让决定建立在真实证词上。") +
                        (save.english ? "" : EndingRecapSuffix()),
                    (Has("triad_complete")?
                        "Lyra published the records of forests, crossings and miners. The shield protects the citadel while people outside pay its cost.":
                        "All three cores feed the citadel. Before confronting Orrin, help Lyra verify the forests', crossings' and miners' testimony.") +
                        (save.english ? EndingRecapSuffix() : ""),
                    "main",save.ending>0,
                    "你已作出星核抉择；世界将继续回应这项决定。",
                    "You chose the cores' future; the world continues to respond.");

            // This is the SAME campaign's middle and late acts on saves
            // that really entered the expanded main narrative. Do not leak
            // the remote witnesses to a fresh chapter-one player, and do
            // not reclassify old finished saves as unfinished campaigns.
            if(MainSagaActive&&Has("boss_4"))
            {
                AddRecapBeat(beats,5,"第五章 · 失踪的船名",
                    "Chapter Five · Names Missing at Sea",
                    Has("saga_trace_5")?
                        "北岸的第二份名单揭开了米拉不敢讲的救援抉择。现在只有她自己能决定怎样将完整真相交给失踪者家属。":
                        "港口调查不只是修好灯塔：乘客名册的原件还保留一条失踪的救援航路，找到它再问米拉。",
                    Has("saga_trace_5")?
                        "The hidden rescue manifest reveals Mira's burden. Only she can say how to give families the full truth.":
                        "The harbor crisis is more than a broken lighthouse. Inspect the original manifest to uncover a hidden rescue route.",
                    "main",Has("saga_voice_5"),
                    "米拉亲口留下了有关救援船与失踪者的证词。",
                    "Mira signed her account of the rescue boat and those still missing.");
                if(Has("saga_voice_5")||Has("desert_report"))
                    AddRecapBeat(beats,6,"第六章 · 九年前的水",
                        "Chapter Six · Nine Years Without Water",
                        Has("saga_trace_6")?
                            "蓄光池的散热铭牌还留着被刮掉的儿童避暑棚指令。听伊沃解释他做过的选择。":
                            "沙海的供水命令比当前危机更早。到原蓄光接收台查找是谁改写了配给顺序。",
                        Has("saga_trace_6")?
                            "The scraped reservoir plate still bears the old shade-shelter order. Hear Ivo's decision firsthand.":
                            "The desert rationing predates the present crisis. Inspect the original receiver for the altered priorities.",
                        "main",Has("saga_voice_6"),
                        "伊沃在自己的改线图旁签下了九年前的供水供词。",
                        "Ivo signed the account of his nine-year-old diversion.");
                if(Has("saga_voice_6")||Has("mountain_report"))
                    AddRecapBeat(beats,7,"第七章 · 没有敲响的钟",
                        "Chapter Seven · The Bell That Never Rang",
                        Has("saga_trace_7")?
                            "第三座旧钟上不是机械损伤，而是有人捂住警报留下的绳痕。森必须自己解释那一夜。":
                            "高原的撤离记录只留下死者姓名。去第三座旧钟调查，找回没有被写进报告的声音。",
                        Has("saga_trace_7")?
                            "The third chime shows a deliberately muffled warning. Sen must explain that night himself.":
                            "The mountain record lists the dead but hides the silenced alarm. Inspect the third chime.",
                        "main",Has("saga_voice_7"),
                        "森的证词让获救的人和失去归途的人同时留在记录里。",
                        "Sen's account names both the rescued and those who never made it back.");
                if(MainSagaVoices==3||save.frontierEnding>0)
                    AddRecapBeat(beats,8,"第八章 · 人人有权作证",
                        "Chapter Eight · Everyone Has a Voice",
                        save.frontierEnding>0?
                            "三方协定已由各方签署。带着所有人的供词回到堡垒，星核最终归属才真正有意义。":
                            "三份当事人亲签的证词终于齐全。到雾港议事厅讨论各地愿意共同承担的供能风险。",
                        save.frontierEnding>0?
                            "Each delegation signed the covenant. Return to the citadel with their accounts before deciding the fate of the cores.":
                            "All three accounts are signed. At Mistport the council must decide who can govern the supply lines together.",
                        "main",save.ending>0,
                        "决定已经作出，各地继续依自己签署的共同协定生活。",
                        "The final decision was made; the communities live with their signed covenant.");
            }
            if(save.ending<=0) return beats;
            AddUnlockedRecap(beats,5,"afterlight",
                "后日谈 · 余光重建","Afterstory · Afterlight Reconstruction",
                "星核事件之后，恢复土地不能只靠一次决定。重连三地公共设施，并让当地居民承担后续维护。",
                "After the core decision, recovery takes more than one promise. Reconnect three public sites with local residents as their stewards.",
                "重建公约已经公布，三地居民共同维护设施。",
                "The reconstruction charter is public and residents maintain the sites.");
            AddUnlockedRecap(beats,6,"storm_commons",
                "后日谈 · 公约第一夜","Afterstory · First Night of the Commons",
                "战后第一场星雨考验新公约：公共储备有限，避险与供能必须由各地共同协调。",
                "The first postwar starstorm tests the new charter. Limited public reserves must balance shelter and essential power.",
                "第一次公共星雨应对已经完成。",
                "The first shared starstorm response is complete.");
            AddUnlockedRecap(beats,7,"hearing",
                "后日谈 · 众人的晨钟","Afterstory · The People's Dawn",
                "设施恢复之后，居民仍要决定自己的未来。听取密林、渡口与矿场三位证人的选择，再公布公开公约。",
                "Restoring infrastructure is not enough: residents must choose its future. Hear the forest, ferry and mining witnesses before publishing the civic verdict.",
                "居民听证与三地公共公约已经生效。",
                "The residents' hearing and three-region civic accord are complete.");
            AddUnlockedRecap(beats,8,"roadcharter",
                "后日谈 · 共同行路","Afterstory · Roads for Everyone",
                "旧地图只记录获准通行的人。重新踏查三地支路，把居民真实走过的路写进公共地图。",
                "Old maps recorded authorized travelers, not every resident. Survey three lived-in roads and put real civilian routes on the public map.",
                "三地道路证据已汇总，公共道路公约已经公布。",
                "Three regions' road evidence has become a public charter.");
            AddUnlockedRecap(beats,9,"civic_supply",
                "后日谈 · 居民补给审计","Afterstory · People's Supply Inquest",
                "补给不能只记在账本上：先把救援包实际送到居民手中，再让社区选择医疗点或守路岗。",
                "Relief cannot exist only on paper. Deliver real parcels, then let each community choose between clinics and road lookouts.",
                "三地补给与服务选择已公开，居民可核对账目。",
                "Three communities' supply and service choices are on public record.");
            AddUnlockedRecap(beats,10,"last_directive",
                "后日谈 · 最后指令","Afterstory · The Last Directive",
                "公开账目之后，还有一道无人署名的旧军令。寻找原始证词，解除封锁，决定哪些权利应该归还居民。",
                "A final unsigned military order remains after the supply ledger. Recover first-hand testimony, break its locks and restore civilian rights.",
                "最后指令已经公开，居民获得持续有效的民用权限。",
                "The last directive is public, with lasting civic rights.");
            AddUnlockedRecap(beats,11,"civic_conduit",
                "后日谈 · 公共电网调谐","Afterstory · The Civic Conduit",
                "新权利需要真正运行的电网。亲自校准三处阀门，避开电弧，让公共能源送达各地避险棚。",
                "New civic rights need a working grid. Tune three physical valves, avoid live arcs and restore power to public refuges.",
                "三个阀门已经校准，公共电网持续服务远岸居民。",
                "Three tuned valves keep the public grid serving the farshore.");
            // The town's chronicle ALREADY has Chapter Twelve; the tracked
            // story recap must not strand a player after the grid ending.
            // Reuse the real road-audit quest for both unlock and next foot,
            // so none of the three regional checks can be fabricated here.
            AddUnlockedRecap(beats,12,"civic_aftercare",
                "后日谈 · 电网之后，谁守着路",
                "Afterstory · Who Keeps the Road After the Grid",
                Has("civic_aftercare_started")?
                    "电网重新送来了光。夜行旅人摊开巡路簿，等着引水渠、赤铁矿场和雾港的旧路标留下亲自核验的记号。已确认的路能带人回家，未到过的路仍是空白。":
                    "星核的抉择早有结局，三道阀门也重新通电。后来的生活由居民自己维护：你可以直接走访引水渠、赤铁矿场与雾港的真实路碑，愿意就留下路况记录，不必再领取下一场救世任务。",
                Has("civic_aftercare_started")?
                    "The grid carries light again. At Hearth Inn the night traveler has spread out the road ledger, waiting for first-hand marks from Canal, Ironreach and Mistport. A checked road brings people home; the unwalked routes remain blank.":
                    "The starcore choice is settled and all three valves carry current. Residents now maintain their own roads; visit authentic markers in Canal, Ironreach and Mistport if you wish, without accepting another world-saving quest.",
                "三地巡路实录已经公开，守路人会在核验路标旁留下药箱，照顾后来的人。",
                "The three-region road ledger is published. Its surveyed markers now keep herbal field kits for those who come after.");
            // Chapter Twelve can be historically complete while residents
            // still lack actual lumber and medicine. Make its NEXT-step
            // text agree with the Journey panel's actionable follow-up
            // instead of leaving the hero stranded on a disabled button.
            if(beats.Count>0)
            {
                int last=beats.Count-1;
                StoryBeat page=beats[last];
                string nextId=StoryRecapTrackableQuestId(page);
                if((page.chapter==12||page.chapter==16)&&page.complete&&
                   nextId=="civic_waystations")
                {
                    QuestGuide nextQuest=GetQuest(nextId);
                    beats[last]=new StoryBeat(page.chapter,page.title,
                        page.summary,L("巡路簿已经公开。下一步补给路站：",
                            "Road ledger published. Next, stock the waystations: ")+
                        nextQuest.next,page.questId,page.complete);
                }
            }
            return beats;
        }


        // Optional cross-region story: the official casualty lists counted
        // machines but not the workers who stopped them. Four newly authored
        // physical campaign props; no phantom map click or new save schema.
        // Three distinct field inspections must happen in their actual
        // regions and order before a conscious final decision at the town
        // desk. Old saves simply see an available quest.
        public bool NightShiftComplete
        { get {return Has("nightshift_public")||Has("nightshift_private");} }
        public int NightShiftEvidenceCount
        {
            get
            {
                int n=0;
                for(int r=1;r<=3;r++)if(Has("nightshift_trace_"+r))n++;
                return n;
            }
        }
        private WorldObject NightShiftSite(int region)
        {
            if(region<0||region>=data.maps.Length)return null;
            MapData map=data.maps[region];
            if(map==null||map.objects==null)return null;
            foreach(WorldObject o in map.objects)
                if(o!=null&&o.kind=="nightledger"&&
                   o.id=="nightledger"+region)return o;
            return null;
        }
        public QuestGuide BuildNightShiftGuide(QuestGuide q)
        {
            q.title=L("夜班名册 · 被删去的回家路",
                "Night-Shift Roll · The Road Left Out");
            q.category=L("跨区调查 · 可选居民故事",
                "CROSS-REGION INVESTIGATION · OPTIONAL STORY");
            q.description=L(
                "一份没有寄出的夜班名单，把密林的巡路人、沉没遗迹的渡工和矿场停机工连在了一起。对照真实现场留下的时间，查出是谁把他们从撤离记录中删掉，再决定公开姓名，还是保护仍在工作的证人。",
                "An unsent shift roll links the forest roadwardens, flood-season ferrymen and furnace workers. Compare physical timestamps to uncover who erased their evacuation record. Decide whether to publish their names or protect witnesses still employed.");
            q.reward=L("硬币40、经验35；公开名单可改善三地实际巡路速度，保密保护证人会永久增加生命上限8",
                "40 coins, 35 XP; publishing improves travel speed in three regions, protecting witnesses permanently grants +8 max HP");
            q.locked=false;q.complete=NightShiftComplete;
            q.steps.Add(new QuestStep(L("阅读余烬镇尚未寄出的名册",
                "Read the unsent roll in Emberfall"),Has("nightshift_started")));
            for(int r=1;r<=3;r++)
                q.steps.Add(new QuestStep(
                    r==1?L("核实密林夜间巡路班表",
                          "Verify the forest night watch"):
                    r==2?L("以退潮钟印纠正官方渡口时间",
                          "Cross-check the low-tide clock"):
                         L("用零件开封，或步行启动废轨泄压站再限时返回",
                           "Open with a part, or vent the rail machinery and return before cooling expires"),
                    Has("nightshift_trace_"+r)));
            q.steps.Add(new QuestStep(L("回余烬镇决定证词如何保存",
                "Return to town and choose what to protect"),q.complete));
            int next=0;
            if(Has("nightshift_started"))
                for(int r=1;r<=3;r++)
                    if(!Has("nightshift_trace_"+r)){next=r;break;}
            if(q.complete||!Has("nightshift_started")||next==0)
            {
                q.room=0;
                WorldObject desk=NightShiftSite(0);
                q.target=desk==null?new Point(25.5f,24.5f):
                    new Point(desk.x,desk.y);
                q.next=q.complete?
                    Has("nightshift_public")?
                        L("居民已经拿到可核对的名单；三地旧路重新有人维护。",
                          "Residents now share the corrected roll and maintain the three old roads."):
                        L("原始名单已封存，证人的名字只有本人能决定是否公开。",
                          "The original roll is sealed; living witnesses control their names."):
                    Has("nightshift_started")?
                        L("证据已齐，回镇上把原始名单交给等消息的人。",
                          "Evidence secured. Return to the people waiting at the town desk."):
                        L("在余烬镇找到尚未寄出的夜班名册，选择亲自调查。",
                          "Find the unsent night-shift roll in Emberfall and accept the case.");
                return q;
            }
            q.room=next;
            WorldObject source=NightShiftSite(next);
            q.target=source==null?new Point(18.5f,15.5f):
                new Point(source.x,source.y);
            q.next=next==1&&NightShiftRaidActive?
                (NightShiftRaidReady?
                    L("林道追兵已被击退，回到班表核验原件。",
                      "Trackers defeated. Return to the original card."):
                    L("真实林道战斗中：击退两名追兵。",
                      "Live forest encounter: defeat both trackers.")):
                next==1?
                L("沿密林中路到半张旧班表，查清谁真正值了夜班。",
                  "Find the torn shift card in the forest and verify who was on duty."):
                next==2?
                (LowTide?
                    L("沉没遗迹退潮窗口已开放：到石阶钟印对照原件。",
                      "Low tide is open. Inspect the original clock mark on the ruin stairs."):
                    L("沉没遗迹正在涨潮：等真正退潮，或带木材1搭支架测量。",
                      "The ruins are flooded. Wait for ebb or bring one timber for a real gauge.")):
                NightShiftVentSeconds>0f?
                L("泄压还有"+(int)NightShiftVentSeconds+"秒：沿原路赶回矿场卷轴，免费开封。",
                  "Vent open for "+(int)NightShiftVentSeconds+"s: return to the original spool and recover it without a part."):
                NightShiftVentSeeking?
                L("沿废轨前往手动泄压站，花耐力启动冷却再返回卷轴。",
                  "Find the remote rail pressure relief, spend energy to cool the seal, then return."):
                save.inventory[4]<1?
                L("无零件：可去矿点采集，或在卷轴处选择废轨泄压路线。",
                  "No parts: collect a real mine part, or choose the physical relief-route alternative at the spool."):
                L("矿场卷轴：消耗零件直接开封，或挑战废轨泄压折返路线。",
                  "Mine spool: spend a part to open immediately or attempt the remote pressure-relief run.");
            if(next==1&&NightShiftRaidActive)q.target=NightShiftRaidTarget;
            if(next==3&&save.room==3&&NightShiftVentSeeking)
            {
                WorldObject station=NightShiftVentSite();
                if(station!=null)q.target=new Point(station.x,station.y);
            }
            else if(next==3&&save.room==3&&save.inventory[4]<1&&
                    NightShiftVentSeconds<=0f)
                q.target=ResourcePoint(3,"ore");
            if(!CanEnter(next))
            {
                QuestGuide unlock=GetQuest("main");
                q.room=unlock.room;
                q.target=unlock.target;
                q.next=L("先通过原有主线开放", "First unlock ")+
                    L(data.maps[next].name,data.maps[next].en)+
                    L("的通路；无需重做已找到的线索。 ",
                      " through the original story. Earlier evidence stays secured. ")+
                    unlock.next;
            }
            return q;
        }

        // Optional physical confrontation at the original forest evidence.
        // Session-owned actors cannot be restored into ordinary world saves.
        private int nightShiftRaidRoom=-1;
        private SaveData nightShiftRaidSave;
        private MapData nightShiftRaidMap;
        private readonly List<Enemy> nightShiftRaiders=new List<Enemy>();
        private readonly HashSet<Enemy> nightShiftRaidKills=new HashSet<Enemy>();
        public bool NightShiftRaidActive
        {get{return nightShiftRaidRoom==1&&save.room==1&&
            System.Object.ReferenceEquals(nightShiftRaidMap,Map)&&
            System.Object.ReferenceEquals(nightShiftRaidSave,save)&&
            !Has("nightshift_trace_1");}}
        public bool IsNightShiftRaider(Enemy e)
        {return e!=null&&NightShiftRaidActive&&nightShiftRaiders.Contains(e);}
        public int NightShiftRaidRemaining
        {get{int n=0;if(NightShiftRaidActive)foreach(Enemy e in nightShiftRaiders)
            if(e!=null&&e.hp>0f&&enemies.Contains(e))n++;return n;}}
        public bool NightShiftRaidReady
        {get{
            if(!NightShiftRaidActive||nightShiftRaiders.Count!=2)return false;
            foreach(Enemy e in nightShiftRaiders)
                if(e==null||!nightShiftRaidKills.Contains(e)||
                   e.hp>0f||enemies.Contains(e))return false;
            return true;
        }}
        public Point NightShiftRaidTarget
        {get{
            WorldObject site=NightShiftSite(1);
            Point result=site==null?Position:new Point(site.x,site.y);
            float nearest=float.MaxValue;
            if(NightShiftRaidActive)foreach(Enemy e in nightShiftRaiders)
                if(e!=null&&e.hp>0f&&enemies.Contains(e))
                {
                    float d=(Position-e.Position).Length;
                    if(d<nearest){nearest=d;result=e.Position;}
                }
            return result;
        }}
        private void ResetNightShiftRaid()
        {
            projectiles.RemoveAll(p=>p!=null&&p.hostileShooter!=null&&
                nightShiftRaiders.Contains(p.hostileShooter));
            foreach(Enemy e in nightShiftRaiders)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);regionalAmbientEnemies.Remove(e);
            }
            nightShiftRaidRoom=-1;nightShiftRaidSave=null;nightShiftRaidMap=null;
            nightShiftRaiders.Clear();nightShiftRaidKills.Clear();
        }
        private void OnNightShiftRaiderDefeated(Enemy e)
        {if(IsNightShiftRaider(e))nightShiftRaidKills.Add(e);}
        private void TickNightShiftRaid()
        {
            if(nightShiftRaidRoom<0)return;
            if(!NightShiftRaidActive||Dying)
            {ResetNightShiftRaid();return;}
            foreach(Enemy e in nightShiftRaiders)
                if(e==null||!nightShiftRaidKills.Contains(e)&&
                   (!enemies.Contains(e)||e.hp<=0f))
                {
                    ResetNightShiftRaid();revision++;
                    Notify("现场追兵失联，证据尚未核实。回到原始班表重新调查。",
                        "A raider vanished without a verified defeat. Revisit the original shift card.");
                    return;
                }
        }
        private bool BeginNightShiftRaid(WorldObject site)
        {
            if(!NightShiftSource(site,1)||NightShiftRaidActive||
               !Has("nightshift_started")||NightShiftEvidenceCount!=0||
               Dying||practicing||!SafeTravel||FieldWorkPreventsRest)
                return false;
            int x=(int)Math.Floor(site.x),y=(int)Math.Floor(site.y);
            Point center=new Point(site.x,site.y);
            var pads=new List<Point>();
            // Two separate forest approaches; only full, reachable waves
            // can be activated, never incomplete/through-wall raiders.
            int[,] dirs={{-1,0},{1,1}};
            for(int i=0;i<2;i++)
            {
                int dx=dirs[i,0],dy=dirs[i,1];
                bool accepted=false;
                for(int radius=6;radius<=13&&!accepted;radius++)
                for(int side=-3;side<=3&&!accepted;side++)
                {
                    Point p=new Point(x+dx*radius-dy*side+.5f,
                        y+dy*radius+dx*side+.5f);
                    if(RegionalScoutFootClear(p,center,true,pads))
                    {pads.Add(p);accepted=true;}
                }
            }
            for(int radius=6;radius<=14&&pads.Count<2;radius++)
            for(int dy=-radius;dy<=radius&&pads.Count<2;dy++)
            for(int dx=-radius;dx<=radius&&pads.Count<2;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                   Math.Abs(dx)+Math.Abs(dy)>18)continue;
                Point p=new Point(x+dx+.5f,y+dy+.5f);
                if(RegionalScoutFootClear(p,center,true,pads))pads.Add(p);
            }
            if(pads.Count!=2)return false;
            nightShiftRaidRoom=1;nightShiftRaidSave=save;nightShiftRaidMap=Map;
            nightShiftRaiders.Clear();nightShiftRaidKills.Clear();
            for(int i=0;i<2;i++)
            {
                Enemy e=CreatePlacedEnemy(++enemyId,i,pads[i],56f);
                nightShiftRaiders.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.6f)
                    {variant=i});
            }
            revision++;sound="alert";
            Notify("撕下班表引出巡路追踪者！击退两名真实追兵，再回来核对原始记录。",
                "Two real road trackers answer the disturbed roll. Defeat both and return to verify the evidence.");
            return true;
        }

        // A genuinely separate, navigable mine solution to the sealed
        // evidence spool. Optional 85-second cooling run: the existing
        // direct 1-part opening remains available to legacy players.
        // Session-local only. No save migration, teleport or fake machine.
        private bool nightShiftSeekingVent;
        private float nightShiftVentUntil;
        private SaveData nightShiftVentSave;
        private MapData nightShiftVentMap;
        private void ResetNightShiftVent()
        {
            nightShiftSeekingVent=false;nightShiftVentUntil=0f;
            nightShiftVentSave=null;nightShiftVentMap=null;
        }
        public float NightShiftVentSeconds
        {
            get
            {
                if(save.room!=3||!System.Object.ReferenceEquals(nightShiftVentSave,save)||
                   !System.Object.ReferenceEquals(nightShiftVentMap,Map)||
                   !Has("nightshift_started")||Has("nightshift_trace_3"))
                    return 0f;
                return Math.Max(0f,nightShiftVentUntil-save.totalTime);
            }
        }
        public bool NightShiftVentSeeking
        {get{return nightShiftSeekingVent&&save.room==3&&
            !Has("nightshift_trace_3")&&NightShiftVentSeconds<=0f;}}
        private WorldObject NightShiftVentSite()
        {
            if(data.maps.Length<=3)return null;
            foreach(WorldObject o in data.maps[3].objects)
                if(o!=null&&o.kind=="nightvent"&&o.id=="nightshift_vent_3")
                    return o;
            return null;
        }
        private bool NightShiftVentSource(WorldObject o)
        {
            return o!=null&&save.room==3&&o.kind=="nightvent"&&
                System.Object.ReferenceEquals(o,NightShiftVentSite())&&
                SourceBelongsToActiveMap(o)&&IsObjectPresent(o)&&
                HasPhysicalInteractionReach(o);
        }
        private bool NightShiftSource(WorldObject o,int region)
        {
            return o!=null&&o.kind=="nightledger"&&
                save.room==region&&o.id=="nightledger"+region&&
                System.Object.ReferenceEquals(o,NightShiftSite(region))&&
                SourceBelongsToActiveMap(o)&&IsObjectPresent(o)&&
                HasPhysicalInteractionReach(o);
        }
        private bool TalkNightShift(WorldObject o)
        {
            if(o==null)return false;
            if(o.kind=="nightvent")
            {
                if(!NightShiftVentSource(o))return true;
                if(!Has("nightshift_started")||NightShiftEvidenceCount!=2||
                   NightShiftComplete)
                    Dialog(o.name,o.en,
                        "旧手轮的铭牌还在，油压早已离开规定区间。这里连着下方的停机记录。",
                        "The old handwheel is rusted. Its pipe still links to the sealed shutdown roll.",
                        C("暂不操作","Leave machinery","close"));
                else if(NightShiftVentSeconds>0f)
                    Dialog(o.name,o.en,
                        "泄压阀已经打开。冷却气正在沿旧轨向封存卷轴流动，还剩"+(int)NightShiftVentSeconds+"秒。必须亲自返回现场。",
                        "The physical valve is open, cooling the old spool for "+(int)NightShiftVentSeconds+" seconds. Return to the original record on foot.",
                        C("赶回卷轴","Return to the spool","close"));
                else
                    Dialog(o.name,o.en,
                        "手轮后的老管线还通着卷轴铁筒。启动它要消耗18点耐力：冷却只能坚持85秒。"
                        +"如果中途离开矿场或超时，就得重新打开阀门。",
                        "The old pipe reaches the sealed spool. Turning it costs 18 energy and cools the lock for 85 seconds."
                        +" If you leave the region or miss the window, this physical reset must be repeated.",
                        C("耐力18 · 启动85秒泄压","18 energy · activate 85s relief","nightshift_vent_start"),
                        C("先准备好再回来","Prepare first","close"));
                return true;
            }
            if(o.kind!="nightledger")return false;
            int room=save.room;
            if(!NightShiftSource(o,room))return true;
            if(room==0)
            {
                if(NightShiftComplete)
                {
                    Dialog(o.name,o.en,
                        Has("nightshift_public")?
                            "公会没有替那三个人发言。名单在墙上，名字后面还有一块空白，留给他们自己补完。":
                            "名册锁在抽屉里。莉雅留下了三把钥匙，分别寄给仍在世的当事人；镇上的空椅子却从没被撤走。",
                        Has("nightshift_public")?
                            "The guild spoke for no one. The roll hangs in public, with blank space beside the names for their owners to finish it.":
                            "The roll is locked away. Lyra sent three keys to the living witnesses, but never removed the empty chairs.",
                        C("合上名册","Close the roll","close"));
                }
                else if(NightShiftEvidenceCount==3)
                {
                    Dialog(o.name,o.en,
                        "莉雅把三张湿透的纸并排压好。\n\n“根系、退潮、炉火。三处的钟都比官方名册早停了半小时。人不是没回来，是有人不肯让他们回来。现在还剩最后一件事：这些名字究竟由谁说出口？”",
                        "Lyra weighs down three weather-damaged pages.\n\n“Roots, ebb tide, furnace. All three clocks stopped half an hour before the official roll. Those people weren't missing. Someone refused to count their return. Who gets to speak their names now?”",
                        C("公开更正名单 · 修复巡路","Publish a corrected roll · safer roads",
                          "nightshift_public"),
                        C("封存原件并保护证人 · 生命上限+8",
                          "Seal the original · protect witnesses · +8 max HP",
                          "nightshift_private"),
                        C("还没想好","Decide later","close"));
                }
                else if(!Has("nightshift_started"))
                    Dialog(o.name,o.en,
                        "旧账箱里有一张沾了雨的纸，抬头写着“夜班正常交接”。下面却有三种笔迹：巡林人在空白处画了折枝，渡工写了退潮时刻，矿工只留下一个“停”字。\n\n莉雅说：“不是每一份没送到的信都应该替人拆开。你愿意亲自走一遍他们的路吗？”",
                        "A rain-stained roll says “normal night handover.” Three hands disagree: a broken branch drawn by a roadwarden, a low-tide time from a ferryman, and one word from a miner: STOP.\n\nLyra asks, “Not every unsent letter is ours to open. Will you walk their roads before we decide?”",
                        C("接过名册 · 追查三处现场","Take the roll · investigate three sites",
                          "nightshift_begin"),
                        C("暂时留下","Leave it for now","close"));
                else
                    Dialog(o.name,o.en,
                        "莉雅把剩下的纸放进防潮袋。\n\n“先别猜结论。拿回来的是时间、地点和说话的人，不是公会想听的答案。”",
                        "Lyra wraps the remaining pages against the damp.\n\n“Don't guess the conclusion. Bring back times, places and people, not the answer the guild wants.”",
                        C("继续沿线调查","Continue the investigation","close"));
                return true;
            }
            if(room<1||room>3)return true;
            if(!Has("nightshift_started"))
            {
                Dialog(o.name,o.en,
                    "这里留着一小截被雨泡软的夜班登记纸，右下角有余烬镇公会的回信地址。",
                    "A softened scrap of shift paper still carries the Emberfall guild's return address.",
                    C("记下地址","Remember the return address","close"));
                return true;
            }
            if(NightShiftComplete)
            {
                bool delivered=Has("nightshift_followup_"+room);
                bool published=Has("nightshift_public");
                string storyZh=published?
                    room==1?
                        "巡林人把重新绘出的归乡路挂到树上。原来被藏起的路不再只供军队使用，药草箱也留给了路过的伤者。":
                    room==2?
                        "渡工重新标出石阶退潮时刻，把修桥木料留在公共记录柱旁：船不再只听堡垒的时钟。":
                        "矿工用旧卷轴做成公开的停机证明。工会把多余机械零件放在这里，供后来者修理路牌。":
                    room==1?
                        "树洞里留着一只没有署名的小药包。纸条写着：别替我们说姓名，先让仍受伤的人安全离开树林。":
                    room==2?
                        "退潮钟印下藏着一份不署名的歇脚指引。有人清理了石阶上的碎玻璃，留下一处安全补给角。":
                        "矿场工人没有留下姓名，但在停机卷轴旁放了防热布和一小包草药；他们希望后来的证人都能平安离开。";
                string storyEn=published?
                    room==1?
                        "Roadwardens have put the restored route back on the trees. Anyone may follow it home now; public herbs wait for injured travelers.":
                    room==2?
                        "Ferrymen rebuilt the tidal stair timetable and left repair timber by the public record. Boats no longer obey only a citadel clock.":
                        "The workers turned the shutdown roll into a public stop-work record. Spare repair parts are left for those keeping the old road open.":
                    room==1?
                        "An unsigned satchel rests inside the tree hollow: let wounded travelers leave safely before you tell the world our names.":
                    room==2?
                        "The old tide mark conceals an unsigned shelter guide. Someone cleared the glass from the stairs and left a place to recover.":
                        "No miners have signed this gift, but a heat cloth and herbs wait beside the original spool for the next witness.";
                Dialog(o.name,o.en,storyZh+(
                    delivered?"\n\n这处现场的补给已领取；原始证词仍可供查验。":
                    "\n\n他们留下的一次性现场援助仍在。"),
                    storyEn+(delivered?
                        "\n\nThe field cache has been collected. The original evidence remains readable.":
                        "\n\nA one-time on-site aid package is still available."),
                    delivered?
                        C("合上现场记录","Close field record","close"):
                        C(published?"领取公共巡路物资":"领取匿名现场援助",
                          published?"Collect public route supplies":"Receive anonymous field aid",
                          "nightshift_followup_"+room),
                    C("以后再来","Return later","close"));
                return true;
            }
            if(Has("nightshift_trace_"+room))
            {
                Dialog(o.name,o.en,
                    room==1?"两条巡林路线已经对上。那晚有人走出了树线，却没有写进送往堡垒的名单。":
                    room==2?"潮位刻度仍在，刻痕不会跟着一纸命令改动。":
                            "卷轴上的停机手印还很清楚。工人至少曾有机会把机器关掉。",
                    room==1?"Both forest routes agree. Someone emerged from the trees that night and was never entered on the citadel roll.":
                    room==2?"The tide mark remains. A decree cannot move a groove cut in stone.":
                            "The stop-work thumbprint is still legible. Someone really tried to shut the furnace down.",
                    C("收起原件","Keep the record","close"));
                return true;
            }
            if(NightShiftEvidenceCount!=room-1)
            {
                Dialog(o.name,o.en,
                    "这里的日期只有一半。没有上一处现场的原始时间，连是谁改动过记录都无法确定。",
                    "The date here is incomplete. Without the previous physical timestamp, nobody can tell who altered it.",
                    C("先找上一处证据","Find the earlier evidence first","close"));
                return true;
            }
            if(room==1)
            {
                if(NightShiftRaidActive)
                    Dialog(o.name,o.en,
                        NightShiftRaidReady?
                            "追兵已经被击退。树洞内的纸张幸存，可以取下原件核对失踪者的真正归路。":
                            "两名追兵还在这里搜寻。纸张留在树洞里，先击退敌人，免得它落回军方手中。",
                        NightShiftRaidReady?
                            "The trackers have fallen. Recover the original card from the tree hollow.":
                            "Two trackers are still searching here. Defeat them before the card is seized.",
                        NightShiftRaidReady?
                            C("现场安全 · 核验原件",
                              "Area secured · verify evidence","nightshift_trace_1"):
                            C("追兵尚存 · "+NightShiftRaidRemaining,
                              "Trackers remaining · "+NightShiftRaidRemaining,"close"));
                else
                    Dialog(o.name,o.en,
                        "半张纸钉在折枝背面：“三名工人于天亮前被护送到渡口。”新鲜脚印说明追兵仍在附近。你可以燃烧药草压住气味，或把追兵引到真实林道交战。",
                        "The torn card reads: “Three workers escorted to the ferry before dawn.” Fresh prints show trackers nearby. Burn an herb to conceal your scent or draw the real raiders into combat.",
                        C("草药1、耐力30 · 无声取证",
                          "1 herb + 30 stamina · quiet recovery","nightshift_quiet"),
                        C("诱出追兵 · 两人实战",
                          "Draw out two trackers · actual combat","nightshift_fight"),
                        C("暂不冒险","Leave the roll","close"));
            }
            else if(room==2)
                Dialog(o.name,o.en,
                    "旧石阶上的退潮钟印比公文时间早半日。潮水目前"+(LowTide?
                        "已经落下，露出了完整的石匠刻痕。":
                        "正在漫过刻痕。你可以等水自然退去，或用一块真实木料搭出临时观测支架。")+"\\n\\n堡垒抄本却写着日出后才放船。你信哪一种时间？",
                    "The mason's real low-tide groove predates the citadel's report. "+(LowTide?
                        "The ebb has exposed the complete old mark.":
                        "The rising water covers the mark. Wait for the real ebb, or spend one timber to brace a temporary measuring platform.")+"\\n\\nThe official copy claims boats crossed after sunrise. Which clock can be trusted?",
                    LowTide?
                        C("退潮已露刻痕 · 核实原始时间",
                          "Low tide · verify original time","nightshift_trace_2"):
                        C("木材1 · 搭架测量漫水刻痕",
                          "1 timber · brace a real tide gauge","nightshift_tidebrace"),
                    C("照抄公文上的日出时间",
                      "Copy the official sunrise date","nightshift_false"),
                    C("暂不取证","Wait for better conditions","close"));
            else
                Dialog(o.name,o.en,
                    "最后一页被装进带螺丝的铁筒。筒盖上写着“未获批准不得停炉”，底下却有工人按下的停机手印。\n\n你可以耗费零件直接撬开锁环；也可以步行到旧轨深处的手动泄压站，启动冷却再在85秒内赶回来。",
                    "The last page is sealed in a screwed iron spool. “No shutdown without authorization,” says the lid, beside a worker's stop-work thumbprint.\n\nSpend a real part to loosen the collar, or hike to the abandoned rail pressure relief and return within its 85-second cooling window.",
                    C("耗费零件1 · 直接打开卷轴",
                      "Spend 1 part · open the spool","nightshift_trace_3"),
                    NightShiftVentSeconds>0f?
                        C("泄压尚有"+(int)NightShiftVentSeconds+"秒 · 免费取证",
                          "Cooling "+(int)NightShiftVentSeconds+"s · open free",
                          "nightshift_vent_open"):
                        C("前往废轨泄压站 · 步行往返",
                          "Find the rail relief · physical route",
                          "nightshift_vent_route"),
                    C("暂不拆封","Leave the seal intact","close"));
            return true;
        }
        private bool ChooseNightShift(string action)
        {
            if(action==null||!action.StartsWith("nightshift_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            int room=pendingDialogueSourceRoom;
            if(room<0||room>3||
               !NightShiftSource(source,room)&&!NightShiftVentSource(source))
                return true;
            if(action.StartsWith("nightshift_followup_",StringComparison.Ordinal))
            {
                int station;
                if(room<1||room>3||
                   !int.TryParse(action.Substring("nightshift_followup_".Length),out station)||
                   station!=room||!NightShiftSource(source,station)||
                   !NightShiftComplete||!Has("nightshift_trace_"+station)||
                   Has("nightshift_followup_"+station))
                    return true;
                // Credit only exact visit at the original field record.
                // The outcome changes the type of service, but never
                // creates permanent respawning loot or new SaveData fields.
                if(Has("nightshift_public"))
                {
                    int resource=station==1?2:station==2?0:4;
                    save.inventory[resource]=Math.Min(9999,
                        save.inventory[resource]+(station==3?1:2));
                }
                else
                {
                    if(station==2)
                        save.energy=Math.Min(100f,save.energy+35f);
                    else
                        save.health=Math.Min(save.maxHealth,
                            save.health+(station==1?24f:18f));
                }
                Flag("nightshift_followup_"+station);
                RecordMoment(Has("nightshift_public")?
                    "夜班名册 · 你亲自重访了"+new[]{"","密林","沉没遗迹","赤铁矿场"}[station]+
                        "的归乡路标；居民已将修路补给交给实际赶路者。":
                    "夜班名册 · 你亲自重访了"+new[]{"","密林","沉没遗迹","赤铁矿场"}[station]+
                        "的无名记录点；证人留下一份不必公开身份的实地援助。",
                    Has("nightshift_public")?
                        "Night-Shift Roll: revisited "+new[]{"","the forest","the ruins","the mine"}[station]+
                        " to recover public supplies for the restored civilian road.":
                        "Night-Shift Roll: revisited "+new[]{"","the forest","the ruins","the mine"}[station]+
                        " and received anonymous aid without revealing witness names.");
                revision++;sound="quest";
                Notify("现场援助已领取，原始证词仍留在此处。",
                    "Field aid collected. The original evidence remains in place.");
                return true;
            }
            if(action=="nightshift_vent_route")
            {
                if(!NightShiftSource(source,3)||NightShiftComplete||
                   !Has("nightshift_started")||NightShiftEvidenceCount!=2)
                    return true;
                nightShiftSeekingVent=true;revision++;sound="quest";
                Notify("目标更新：沿真实矿道前往废轨泄压站；不会传送或自动打开卷轴。",
                    "Objective updated: walk to the rail relief. No teleport or free spool opening.");
                return true;
            }
            if(action=="nightshift_vent_start")
            {
                if(!NightShiftVentSource(source)||NightShiftComplete||
                   !Has("nightshift_started")||NightShiftEvidenceCount!=2)
                    return true;
                if(!SafeTravel||Dying||practicing||save.energy<18f)
                {
                    Notify("泄压手轮需要18点耐力，并且周围必须安全。",
                        "The pressure wheel needs 18 energy and a safe area.");
                    return true;
                }
                save.energy-=18f;
                nightShiftVentUntil=save.totalTime+85f;
                nightShiftVentSave=save;nightShiftVentMap=Map;
                nightShiftSeekingVent=false;
                revision++;sound="quest";
                effects.Add(new Effect("burst",new Point(source.x,source.y),.6f)
                    {radius=1.6f,variant=2});
                Notify("冷却阀已开！85秒内步行返回矿场卷轴可免零件开封。",
                    "Relief valve open. Walk back to the original spool within 85s to save a part.");
                return true;
            }
            if(action=="nightshift_fight")
            {
                if(!BeginNightShiftRaid(source))
                    Notify("周边地形或敌人尚不安全，无法布置完整战斗。",
                        "The terrain is unsafe for a complete encounter.");
                return true;
            }
            if(action=="nightshift_quiet")
            {
                if(room!=1||NightShiftRaidActive||
                   !Has("nightshift_started")||
                   NightShiftEvidenceCount!=0||NightShiftComplete)return true;
                if(!SafeTravel||save.inventory[2]<1||save.energy<30f)
                {
                    Notify("潜行取证需要草药1、耐力30并脱离危险。",
                        "Quiet recovery costs one herb, 30 stamina and requires safety.");
                    return true;
                }
                save.inventory[2]--;save.energy-=30f;
                Flag("nightshift_trace_1");
                RecordMoment(
                    "夜班名册 · 用草药掩盖气味，消耗耐力悄悄取回密林原始记录，没有惊动附近追兵。",
                    "Night-Shift Roll: spent a genuine herb and 30 stamina to retrieve the forest original without alerting the patrol.");
                revision++;sound="quest";
                Notify("无声取证完成：去沉没遗迹核实退潮钟印。",
                    "Quiet recovery complete. Check the tide clock in the ruins.");
                return true;
            }
            if(action=="nightshift_false")
            {
                if(room!=2||!Has("nightshift_trace_1")||
                   Has("nightshift_trace_2"))return true;
                Dialog(source.name,source.en,
                    "你照着日出日期试算：那时石阶已被水盖住，渡工不可能把船划到这里。公文里的时间不是证据。",
                    "You test the sunrise entry. The stairs would already be underwater; no ferryman could land here. The copied hour is not evidence.",
                    C("重新查看石阶","Re-examine the stairs","close"));
                return true;
            }
            if(action=="nightshift_begin"&&room==0&&
                !Has("nightshift_started")&&!NightShiftComplete)
            {
                Flag("nightshift_started");
                RecordMoment(
                    "夜班名册 · 从余烬镇接过未寄出的名单。巡林人、渡工和矿工写下的时间不一致；不能仅凭堡垒的交接记录断定他们失踪。",
                    "Night-Shift Roll: accepted the unsent list in Emberfall. The roadwarden, ferryman and miner gave conflicting hours; the citadel handover cannot be trusted alone.");
                TrackQuest("night_shift");
                Notify("名册已接下：先去密林中路查看真正的夜班记录。",
                    "Roll accepted. Find the real night-watch card in Thornwood.");
                return true;
            }
            int stage=-1;
            if(action=="nightshift_trace_1")stage=1;
            if(action=="nightshift_trace_2"||
               action=="nightshift_tidebrace")stage=2;
            if(action=="nightshift_trace_3"||
               action=="nightshift_vent_open")stage=3;
            if(stage>=1)
            {
                if(room!=stage||NightShiftComplete||
                   !Has("nightshift_started")||
                   Has("nightshift_trace_"+stage)||
                   NightShiftEvidenceCount!=stage-1)return true;
                if(stage==1&&!NightShiftRaidReady)return true;
                if(stage==2)
                {
                    if(action=="nightshift_trace_2"&&!LowTide)
                    {
                        Notify("潮水盖住了原始刻度；需要真实退潮窗口。",
                            "The original clock mark is submerged. Wait for a genuine ebb.");
                        return true;
                    }
                    if(action=="nightshift_tidebrace")
                    {
                        if(LowTide||!SafeTravel||save.inventory[0]<1)
                        {
                            Notify("支架必须在涨潮时用一块木材安全搭设。",
                                "The temporary gauge needs one timber during high tide, out of combat.");
                            return true;
                        }
                        save.inventory[0]--;
                    }
                }
                if(stage==3)
                {
                    if(action=="nightshift_vent_open")
                    {
                        if(NightShiftVentSeconds<=0f)
                        {
                            Notify("管道已经重新升温。请回废轨泄压站再次冷却，或用零件开封。",
                                "The pipe has reheated. Revisit the rail wheel or spend one part.");
                            return true;
                        }
                    }
                    else
                    {
                        if(save.inventory[4]<1)
                        {
                            Notify("矿场铁筒缺一枚零件：或尝试远处的泄压站。",
                                "The mine spool needs one real part or the remote pressure relief.");
                            return true;
                        }
                        save.inventory[4]--;
                    }
                    ResetNightShiftVent();
                }
                Flag("nightshift_trace_"+stage);
                if(stage==1)ResetNightShiftRaid();
                RecordMoment(
                    stage==1?
                        "夜班名册 · 密林半张班表证实三名工人在天亮前被巡路人护送至渡口。":
                        stage==2?
                        action=="nightshift_tidebrace"?
                        "夜班名册 · 涨潮时耗费一块木材搭设临时支架，测得真正的退潮钟印，推翻堡垒抄本。":
                        "夜班名册 · 沉没遗迹真正的退潮钟印否定了堡垒抄本中的日出时间。":
                        action=="nightshift_vent_open"?
                        "夜班名册 · 穿过废轨启动真实泄压手轮，在85秒内赶回矿场并无损打开停机卷轴，保全了工人的手印。":
                        "夜班名册 · 在赤铁矿场消耗一枚零件，打开封存的停机卷轴并保全工人的手印。",
                    stage==1?
                        "Night-Shift Roll: the torn forest card confirms three workers reached the ferry before dawn.":
                        stage==2?
                        action=="nightshift_tidebrace"?
                        "Night-Shift Roll: spent one timber on a real tide gauge during floodwater and disproved the citadel's sunrise timestamp.":
                        "Night-Shift Roll: the real low-tide mark disproves the citadel's sunrise crossing time.":
                        action=="nightshift_vent_open"?
                        "Night-Shift Roll: activated the remote rail pressure wheel and returned within the real cooling window, opening the spool without a part.":
                        "Night-Shift Roll: spent one genuine part to open the mine spool and preserve the stop-work signature.");
                sound="quest";revision++;
                Notify(stage==1?"巡林班表已收录：下一处到沉没遗迹核对退潮钟印。":
                    stage==2?"退潮钟印已核实：前往赤铁矿场找最后的停机记录。":
                        "真实停机手印已保全：带着完整名册回余烬镇。",
                    stage==1?"Forest card secured. Check the low-tide bell in the ruins.":
                    stage==2?"Tide verified. Find the final shutdown roll in Ironreach.":
                        "Shutdown signature secured. Return the complete roll to town.");
                return true;
            }
            if((action=="nightshift_public"||action=="nightshift_private")&&
                room==0&&!NightShiftComplete&&
                Has("nightshift_started")&&NightShiftEvidenceCount==3)
            {
                bool publish=action=="nightshift_public";
                Flag(publish?"nightshift_public":"nightshift_private");
                RecordMoment(publish?
                    "夜班名册 · 余烬镇公开补正三地被删去的回家道路；当事者仍保有自己说明姓名的权利。密林、遗迹与矿场的公共巡路速度提升。":
                    "夜班名册 · 余烬镇将原始名册交由当事者保管，而不是强迫仍在工作的证人公开身份。你获得了他们留给守护者的长久保护。",
                    publish?
                    "Night-Shift Roll: the town restored the erased road to its public chart without claiming ownership of the workers' names. Travel improves across the three regions.":
                    "Night-Shift Roll: the original record stays with its living owners rather than exposing employed witnesses. Their protection permanently bolsters your health.");
                save.coins=Math.Min(999999,save.coins+40);
                GrantXp(35,true);
                if(!publish)
                {
                    save.maxHealth=Math.Min(9999,save.maxHealth+8);
                    save.health=Math.Min(save.maxHealth,save.health+8);
                }
                // The original story is genuinely complete. Hand the
                // tracked objective to the separate, optional, physical
                // civilian aftercare instead of leaving a finished
                // quest compass on an already-signed desk.
                TrackQuest("night_shift_aftercare");
                sound="quest";revision++;
                Dialog(source.name,source.en,
                    publish?
                    "莉雅没有代替任何人宣布死亡。她把被删掉的道路画回公开的巡路图，只贴出三人自己留下的班表。\n\n“现在有人能沿这条路回家。”":
                    "莉雅收起名册，把三把钥匙寄给名单上仍然活着的人。镇上的灯照样亮着，等他们愿意说的时候再开门。\n\n“保护一个人，也可以是不替他说话。”",
                    publish?
                    "Lyra announces no deaths on anyone's behalf. She restores the erased road to the public chart and posts only the workers' own shift card.\n\n“Someone can follow it home now.”":
                    "Lyra seals the roll and sends three keys to its living owners. The town lamps stay lit until they choose to speak.\n\n“Protecting someone can mean refusing to speak for them.”",
                    C("继续旅程","Return to the road","close"));
                return true;
            }
            return true;
        }


        // Chapter epilogue is a real separate three-stop task, not an
        // invisible aftercredit cache: the journal, compass and map point
        // to whichever distinct original field site still owes its actual
        // survivor handoff. Existing completed main-story flags stay put.
        public QuestGuide BuildNightShiftAftercareGuide(QuestGuide q)
        {
            q.title=L("夜班名册 · 归途上的三处回响",
                "Night-Shift Roll · Three Roads Home");
            q.category=L("居民故事后续 · 实地回访",
                "STORY AFTERCARE · FIELD REVISITS");
            q.description=Has("nightshift_public")?
                L("公民在三地把被删去的路线恢复为公共道路。亲自重访原来查过的记录点，确认真实物资能送到路人手中。",
                  "Residents have reopened three erased civilian roads. Return to the actual evidence sites and verify that supplies reach travelers on foot."):
                L("证人只愿意私下支援仍在路上的人。不要公布姓名；亲自重访三处旧记录点，接收他们留下的无名援助。",
                  "The living witnesses choose private support, not public names. Revisit each original site to receive the anonymous provisions they left.");
            q.reward=Has("nightshift_public")?
                L("真实物资 · 密林药草2、遗迹木材2、矿场零件1",
                  "Real supplies: forest herbs x2, ruins timber x2, mine part x1"):
                L("无名援助 · 密林生命24、遗迹耐力35、矿场生命18",
                  "Anonymous support: forest +24 health, ruins +35 energy, mine +18 health");
            q.locked=!NightShiftComplete;
            int next=0;
            for(int room=1;room<=3;room++)
            {
                bool done=Has("nightshift_followup_"+room);
                q.steps.Add(new QuestStep(
                    room==1?L("回密林原始班表处核对巡路援助",
                              "Revisit the original forest card for field aid"):
                    room==2?L("回沉没遗迹石阶领受渡工补给",
                              "Return to the tidal stair record for aid"):
                            L("回赤铁矿场停机卷轴领受矿工援助",
                              "Return to the mine spool for worker aid"),done));
                if(next==0&&!done)next=room;
            }
            q.complete=!q.locked&&next==0;
            if(q.locked)
            {
                q.room=0;
                WorldObject start=NightShiftSite(0);
                q.target=start==null?new Point(25.5f,24.5f):
                    new Point(start.x,start.y);
                q.next=L("先完成《夜班名册》，并决定是否公开居民姓名。",
                    "First resolve the original Night-Shift Roll and its witness-privacy choice.");
                return q;
            }
            if(q.complete)
            {
                q.room=0;
                WorldObject desk=NightShiftSite(0);
                q.target=desk==null?new Point(25.5f,24.5f):
                    new Point(desk.x,desk.y);
                q.next=L("三处实际回访都完成；这条路如今属于愿意维护它的人。",
                    "All three real field revisits are signed. The road belongs to those who keep it open.");
                return q;
            }
            q.room=next;
            WorldObject site=NightShiftSite(next);
            q.target=site==null?new Point(18.5f,15.5f):
                new Point(site.x,site.y);
            q.next=(next==1?
                L("到密林树洞，找回被遗忘的巡路补给。",
                  "Return to the original forest hollow for the roadwarden's supplies."):
                next==2?
                L("到沉没遗迹旧石阶，查收渡工留下的现场援助。",
                  "Return to the actual tidal stairs for the ferryman's field aid."):
                L("到矿场停机卷轴，接收工人留下的无名援助。",
                  "Return to the mine spool for the workers' aid."));
            if(!CanEnter(next))
            {
                QuestGuide unlock=GetQuest("main");
                q.room=unlock.room;q.target=unlock.target;
                q.next=L("先解锁原有矿道，再回访旧证据：",
                    "Unlock the actual chapter route before revisiting the old evidence: ")+
                    unlock.next;
            }
            return q;
        }

        private void AddUnlockedRecap(List<StoryBeat> beats,int chapter,string questId,
            string nameZh,string nameEn,string summaryZh,string summaryEn,
            string finishedZh,string finishedEn)
        {
            QuestGuide quest=GetQuest(questId);
            if(quest.locked)return;
            // Legacy finished campaigns retain the historical 5–12 index.
            // Expanded campaigns have four additional main-story chapters,
            // so their true postcredits aftermath begins at Chapter Nine.
            int displayed=MainSagaActive?chapter+4:chapter;
            beats.Add(new StoryBeat(displayed,L(nameZh,nameEn),L(summaryZh,summaryEn),
                quest.complete?L(finishedZh,finishedEn):quest.next,
                questId,quest.complete));
        }
    }
}
