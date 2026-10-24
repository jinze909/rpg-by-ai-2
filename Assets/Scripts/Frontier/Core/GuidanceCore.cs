using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed class QuestStep
    {
        public string text; public bool done;
        public QuestStep(string text, bool done) { this.text = text; this.done = done; }
    }
    public sealed class QuestGuide
    {
        public string id, title, category, description, next, reward;
        public int room; public Point target; public bool complete, locked;
        public List<QuestStep> steps = new List<QuestStep>();
        public float Progress { get { if (complete) return 1; int n = 0; foreach (var s in steps) if (s.done) n++; return steps.Count == 0 ? 0 : n / (float)steps.Count; } }
    }
    public sealed partial class GameCore
    {
        public static readonly string[] QuestIds = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "signals", "dispatches", "vigil", "wayfarers", "stations", "station_patrol", "archive_trail", "archive_echo", "root_harmony", "commons_relay", "skywatch", "skywatch_afterglow", "glasswind", "tide_salvage", "triad_reckoning", "blackout", "afterlight", "storm_commons", "hearing", "campaign_witness", "iron_temper", "farshore_relief", "relief_rescue", "waytrails", "roadcharter", "annex_explorer", "trail_circuits", "outer_rescue", "outer_watch", "outer_watch_veteran", "outer_watch_flawless", "ridge_routes", "ridge_watch", "far_refuges", "far_bridges", "far_couriers", "civic_supply", "civic_services", "last_directive", "civic_conduit", "rootsong", "flood_archive", "ferry_relief", "mine_convoy", "frost_signal", "sword_lesson", "civic_aftercare" };
        private void SanitizeGuidance()
        {
            if (save.presentationVersion == 0)
            { save.trackedQuest = save.frontierFocus ? "shore" : "main"; save.tutorialBits = Has("intro") ? 255 : 0; save.showGuidance = true; save.effectsQuality = 2; save.presentationVersion = 1; }
            if (Array.IndexOf(QuestIds, save.trackedQuest) < 0) save.trackedQuest = "main";
            save.explored = BoundedArray(save.explored, data.maps.Length, 0, 16777215);
            save.tutorialBits = (int)Clamp(save.tutorialBits, 0, 255); save.effectsQuality = (int)Clamp(save.effectsQuality, 0, 2);
            save.tutorialDistance = Finite(save.tutorialDistance) ? Clamp(save.tutorialDistance, 0, 4) : 0;
        }
        public int TutorialStep { get { for (int i = 0; i < 8; i++) if ((save.tutorialBits & (1 << i)) == 0) return i; return 8; } }
        public void TutorialAction(int step)
        { if (step < 0 || step > 7 || (save.tutorialBits & (1 << step)) != 0) return; save.tutorialBits |= 1 << step; revision++; }
        public void RestartTutorial() { save.tutorialBits = 0; save.tutorialDistance = 0; save.showGuidance = true; revision++; }
        public void SkipTutorial() { save.tutorialBits = 255; revision++; }
        private void TickGuidance(Point before)
        {
            save.tutorialDistance = Math.Min(4, save.tutorialDistance + (Position - before).Length);
            if (save.tutorialDistance >= 3) TutorialAction(0); if (save.room == 1) TutorialAction(7);
            // Legacy sectors track the original 36x24 area only.
            if (save.x < 36 && save.y < 24)
            {
                int bit = 1 << Sector(save.x, save.y);
                if ((save.explored[save.room] & bit) == 0) { save.explored[save.room] |= bit; revision++; }
            }
            // Preserve all legacy 36x24 fog bits. Annex discoveries use the
            // existing string flags instead of widening the SaveData v2 schema.
            if (save.room < 10 && (save.x >= 36 || save.y >= 24))
            {
                string key = AnnexSectorKey(save.room, save.x, save.y);
                if (!Has(key)) { save.flags.Add(key); revision++; }
            }
        }
        private static int Sector(float x, float y) { return (int)Clamp(x / 6, 0, 5) + (int)Clamp(y / 6, 0, 3) * 6; }
        private string AnnexSectorKey(int room, float x, float y)
        {
            // The procedural abyss must not leak discovery across different
            // seeds or floors; outdoor flags retain their legacy v2 IDs.
            string prefix=room==8?"abyss_annex_seen_"+save.abyssSeed+"_"+save.abyssFloor+"_":
                "annex_seen_"+room+"_";
            return prefix+(int)Math.Floor(x/6)+"_"+(int)Math.Floor(y/6);
        }
        public bool Explored(int room, float x, float y)
        {
            if (room >= 0 && room < 10 && (x >= 36 || y >= 24))
                return Has(AnnexSectorKey(room, x, y));
            return (save.explored[room] & (1 << Sector(x, y))) != 0;
        }
        private static readonly int[] CivicAftercareRegions={0,3,5};
        public bool CivicAftercareActive
        { get { return CivicConduitComplete&&Has("civic_aftercare_started")&&!Has("civic_aftercare_published"); } }
        public int CivicAftercareWaypointCount
        {
            get
            {
                int count=0;
                foreach(int room in CivicAftercareRegions)
                    if(Has("civic_aftercare_waytrail_"+room))count++;
                return count;
            }
        }
        private bool RecordCivicAftercareWaypoint(WorldObject o)
        {
            if(o==null||o.kind!="waytrail"||!CivicAftercareActive||
                !HasPhysicalInteractionReach(o))return false;
            int room=save.room;
            if(room!=0&&room!=3&&room!=5)return false;
            string flag="civic_aftercare_waytrail_"+room;
            if(Has(flag))return false;
            Flag(flag);
            int count=CivicAftercareWaypointCount;
            sound="quest";revision++;
            Notify(count>=3?
                "三地巡路复核已齐备。回炉火旅店公布记录。":
                "巡路复核已记录 "+count+"/3。继续核验另外两地。",
                count>=3?
                "All three route checks are recorded. Return to Hearth Inn to publish the audit.":
                "Road audit recorded "+count+"/3. Verify the other regions.");
            return true;
        }
        private string CivicAftercareInnNote(bool en)
        {
            if(Has("civic_aftercare_published"))
                return en?
                    "\n\nThe three-region road audit is now on the public record. Travelers can compare the field-checked markers with the restored grid instead of relying on the old chart alone.":
                    "\n\n三地巡路复核已列入公开记录。旅人现在可以用实地核验的路标对照重接后的电网，不必只依赖旧路线图。";
            if(Has("civic_aftercare_started"))
                return en?
                    "\n\nThe road audit is still underway: "+CivicAftercareWaypointCount+"/3 regional markers verified. Bring the remaining field notes back here to publish the record.":
                    "\n\n三地巡路复核仍在进行："+CivicAftercareWaypointCount+"/3 个地区路标已核验。把剩余现场记录带回来，就能在这里公开归档。";
            if(CivicConduitComplete)
                return en?
                    "\n\nThe public grid is running again, but the old route chart still needs field verification. I can register the optional three-region audit when you're ready.":
                    "\n\n公共电网已经重新运行，但旧路线图仍需要实地核验。准备好后，我可以登记这项可选的三地巡路复核。";
            return "";
        }
        private Choice[] CivicAftercareInnChoices()
        {
            var choices=new List<Choice>();
            choices.Add(C("坐一会儿，记住这句话","Remember the advice","close"));
            if(!CivicConduitComplete)return choices.ToArray();
            if(Has("civic_aftercare_published"))
                choices.Add(C("查看公开巡路复核记录","Review the published road audit","civic_aftercare_status"));
            else if(!Has("civic_aftercare_started"))
                choices.Add(C("开始三地巡路复核","Begin the three-region road audit","civic_aftercare_begin"));
            else if(CivicAftercareWaypointCount>=3)
                choices.Add(C("公开三地巡路复核记录","Publish the three-region road audit","civic_aftercare_publish"));
            else
                choices.Add(C("查看巡路复核进度","Review road audit progress","civic_aftercare_status"));
            return choices.ToArray();
        }
        private bool AtCivicAftercareInn()
        {
            if(save.room!=9)return false;
            foreach(WorldObject o in Map.objects)
                if(o.kind=="innGuest"&&o.id=="innTraveler"&&
                    HasPhysicalInteractionReach(o))
                    return true;
            return false;
        }
        private bool ChooseCivicAftercare(string action)
        {
            bool civicAction=action=="civic_aftercare_begin"||
                action=="civic_aftercare_status"||
                action=="civic_aftercare_publish";
            if(!civicAction)return false;
            if(!AtCivicAftercareInn())
            {
                Notify("请回炉火旅店，与守夜的旅人交谈后再办理巡路复核。",
                    "Return to Hearth Inn and speak with the night traveler to manage the road audit.");
                return true;
            }
            if(action=="civic_aftercare_begin")
            {
                if(!CivicConduitComplete)
                {Notify("先完成公共电网的三处现场调谐。","Finish the three physical grid valves first.");return true;}
                if(Has("civic_aftercare_published"))
                {Notify("巡路复核已经公开，不能重复领取奖励。","The road audit is already published; its reward cannot be claimed twice.");return true;}
                if(!Has("civic_aftercare_started"))
                {
                    Flag("civic_aftercare_started");
                    RecordMoment("三地巡路复核已接取：回访引水渠、赤铁矿场与雾港的旧路标。","Accepted the three-region road audit: revisit the old route markers at Canal, Ironreach and Mistport.");
                }
                TrackQuest("civic_aftercare");
                sound="quest";revision++;
                Notify("已追踪三地巡路复核。与每地的旧路标交互，完成后回旅店公布。","Three-region road audit tracked. Interact with one old route marker in each region, then publish at the Inn.");
                return true;
            }
            if(action=="civic_aftercare_status")
            {
                int count=CivicAftercareWaypointCount;
                string zh=Has("civic_aftercare_published")?
                    "三地巡路复核已公开。每地的现场标记只计入一次，首次奖励已经结算。":
                    "当前进度 "+count+"/3。复核区域：";
                string en=Has("civic_aftercare_published")?
                    "The three-region road audit is public. Each field marker counted once and the first-clear reward has been settled.":
                    "Current progress "+count+"/3. Field regions: ";
                string[] namesZh={"引水渠","赤铁矿场","雾港"};
                string[] namesEn={"Canal","Ironreach","Mistport"};
                for(int i=0;i<CivicAftercareRegions.Length;i++)
                {
                    int room=CivicAftercareRegions[i];
                    bool done=Has("civic_aftercare_waytrail_"+room);
                    zh+="\n"+namesZh[i]+"："+(done?"已复核":"待复核");
                    en+="\n"+namesEn[i]+": "+(done?"verified":"pending");
                }
                if(!Has("civic_aftercare_published")&&count>=3)
                {
                    zh+="\n三处已齐备，选择“公开三地巡路复核记录”即可归档。";
                    en+="\nAll three are verified; choose Publish the three-region road audit to file the record.";
                }
                Dialog("三地巡路复核","Three-Region Road Audit",zh,en,C("返回","Back","close"));
                return true;
            }
            if(action=="civic_aftercare_publish")
            {
                if(Has("civic_aftercare_published"))
                {Notify("巡路复核已经公开，奖励不会重复发放。","The audit is already public; no duplicate reward is granted.");return true;}
                if(!CivicAftercareActive||CivicAftercareWaypointCount<3)
                {Notify("必须先在引水渠、赤铁矿场和雾港各核验一处旧路标。","Verify one old route marker each at Canal, Ironreach and Mistport first.");return true;}
                Flag("civic_aftercare_published");
                CreditCoins(35);CreditShards(1);AddXp(45);
                RecordMoment("三地巡路复核已公开，公共电网的安全路线有了现场记录。","Published the three-region road audit, giving the restored public grid a field-verified route record.");
                sound="reward";revision++;
                Notify("三地巡路复核已公开！硬币 +35、碎片 +1、经验 +45。","Three-region road audit published! +35 coins, +1 shard, +45 XP.");
                return true;
            }
            return false;
        }
        public void TrackQuest(string id)
        { if (Array.IndexOf(QuestIds, id) < 0 || GetQuest(id).locked) return; save.trackedQuest = id; save.frontierFocus = id == "shore"; TutorialAction(2); revision++; }
        private Point ObjectPoint(int room, string kind, Point fallback)
        { foreach (var o in data.maps[room].objects) if (o.kind == kind) return new Point(o.x, o.y); return fallback; }
        private Point ResourcePoint(int room, string kind)
        {
            Point result = ObjectPoint(room, kind, new Point(18, 14)); float best = float.MaxValue;
            foreach (var o in data.maps[room].objects) if (o.kind == kind && !Has("node_" + o.id))
            { float d = room == save.room ? (Position - new Point(o.x, o.y)).Length : 0; if (d < best) { best = d; result = new Point(o.x, o.y); } }
            return result;
        }
        public QuestGuide CurrentGuide { get { return GetQuest(save.trackedQuest); } }
        public string GuidanceHint()
        { QuestGuide q = CurrentGuide; return q.room != save.room && !q.complete && !q.locked ? L("前往", "Travel to ") + L(data.maps[q.room].name, data.maps[q.room].en) + " · " + q.next : q.next; }
        public QuestGuide GetQuest(string id)
        {
            if (Array.IndexOf(QuestIds, id) < 0) id = "main";
            QuestGuide q = new QuestGuide { id = id, room = 0, target = new Point(18, 17), category = L("主线", "STORY"), reward = L("硬币、经验与新的路线", "Coins, experience and new routes") };




            if(id=="sword_lesson")
            {
                q.title=L("余烬镇 · 诺尔的剑盾考核","Emberfall · Noll's Sword Guard Trials");
                q.category=L("剑盾训练 · 实弹弹反","SWORD-GUARD TRAINING · LIVE ARROWS");
                q.description=L(
                    "回城向铁匠诺尔学习战士剑盾技术。领取48秒练习，面对东方发箭器，使用Q技能正面接触并反弹三支实际飞来的箭。需要保持耐力与正确的技能时机；失败可重新练习，奖励只能获得一次。",
                    "Practice with Noll at the original town smithy: a 48s live-arrow drill. Face the eastern launcher and use the warrior's Q guard to physically reflect three arrows. Manage timing and energy; retries are free and the reward is one-time.");
                q.room=0;q.target=ObjectPoint(0,"forge",new Point(7,8));
                q.locked=save.profession!=0;
                // Preserve the original three-arrow quest completion on
                // every old save. Veteran is a distinct optional milestone.
                q.complete=SwordLessonComplete;
                q.steps.Add(new QuestStep(L("在诺尔铁匠铺启动试炼","Start at Noll's blacksmith shop"),
                    q.complete||SwordLessonActive));
                q.steps.Add(new QuestStep(L("正面弹回三支实际飞来的箭矢",
                    "Face and reflect three actual hostile arrows"),SwordLessonComplete));
                q.next=q.locked?L("此训练属于战士职业","The drill is for the warrior class"):
                    SwordLessonActive?L("弹反 "+SwordLessonHits+"/"+SwordLessonTarget+" · 剩余 "+
                        SwordLessonTime.ToString("0")+" 秒",
                        "Reflections "+SwordLessonHits+"/"+SwordLessonTarget+" · "+
                        SwordLessonTime.ToString("0")+"s left"):
                    SwordLessonVeteranComplete?L("已完成双阶段考核；五箭精英训练仍可复练",
                        "Both trials complete; five-arrow replay is available"):
                    SwordLessonComplete?L("返回诺尔铁匠铺，开始五箭精英考核",
                        "Return to Noll for the advanced five-arrow drill"):
                    L("回城与诺尔对话，开始三箭实战练习","Talk to Noll to start three real-arrow reflections");
                q.reward=L("三箭首次30硬币、1碎片、50经验；五箭精英首次技能点1、经验75，实际箭矢弹反耐力7→5、近战破招9→7；可无奖励复练",
                    "Three-arrow first clear: 30 coins, 1 shard, 50 XP. Five-arrow first clear: 1 talent, 75 XP and lower real sword-guard costs (arrows 7→5, melee 9→7); free replay without rewards.");
                return q;
            }
            if(id=="frost_signal")
            {
                q.title=L("霜钟高原 · 公共余火","Frostchime · The Civic Ember");
                q.category=L("护火实战 · 严寒生存","EMBER RUN · COLD SURVIVAL");
                q.description=L(
                    "原有高原守护者失去控制后，将民用火种由西岗送往南侧暖站。严寒使余温衰减，转折台仍有旧守卫。击败守卫后，可用月光草补火、耐力补火、耐力防风护火或不使用补给；最后选择居民疗养或巡路补给。",
                    "After the original highland guardian falls, carry the civic ember from west to south in real cold. Defeat both guards, then choose a Moonherb refill, energy refill, timed windbreak, or no supplies before commissioning a clinic or road-crew fire.");
                q.room=7;q.locked=!Has("boss_7")||!CanEnter(7);
                q.complete=FrostSignalComplete;
                // "Started sometime in an earlier attempt" is a saved
                // historical fact, not proof that this run still carries
                // the ember after abort, expiry or a v2 reload.
                q.steps.Add(new QuestStep(L("在西部领取余火","Collect the original western ember"),
                    FrostSignalActive||q.complete));
                q.steps.Add(new QuestStep(L("守卫实战与补给抉择","Overcome the middle guards and select supplies"),
                    q.complete||FrostSignalStage>=3));
                q.steps.Add(new QuestStep(L("将余火安全送达南侧公共暖站",
                    "Deliver the living ember to the southern public hearth"),
                    q.complete||FrostSignalAtHearth));
                q.steps.Add(new QuestStep(L("在暖站决定疗养或巡路服务",
                    "Commission a clinic or road beacon in person"),q.complete));
                // Stage 1 means ember in hand: the NEXT objective is the
                // guard junction. Sending the active HUD marker back to the
                // western pickup post makes the 100-second timed delivery
                // appear to go backward. Stage 2 stays at that junction
                // until two actual guards fall and supplies are chosen.
                int post=FrostSignalStage==3?3:
                    FrostSignalStage>=1?2:1;
                foreach(WorldObject o in data.maps[7].objects)
                    if(o.id=="frostsignal_"+post){q.target=new Point(o.x,o.y);break;}
                q.next=q.locked?L("先击败高原守护者","First defeat the original Frostchime guardian"):
                    q.complete?L("民用火堆已重燃","Public hearth restored"):
                    FrostSignalAtHearth?L("余火已安置于南侧暖站。请到现场决定疗养站或巡路灯网。",
                        "Ember safely deposited. Choose the public clinic or road beacons at the hearth."):
                    FrostSignalActive?L("余温 "+FrostSignalFlame.ToString("0")+
                        " · 剩余 "+FrostSignalTime.ToString("0")+" 秒"+
                        (FrostSignalWindbreakTime>0?" · 防风 "+FrostSignalWindbreakTime.ToString("0")+" 秒":""),
                        "Ember "+FrostSignalFlame.ToString("0")+
                        " · "+FrostSignalTime.ToString("0")+"s remain"+
                        (FrostSignalWindbreakTime>0?" · Windbreak "+FrostSignalWindbreakTime.ToString("0")+"s":"")):
                    L("前往西部守灯岗领取火种","Collect ember at the west signal");
                q.reward=L("首次55硬币、2碎片、85经验；余温50以上额外10硬币；永久暖站服务",
                    "First clear: 55 coins, 2 shards, 85 XP; 10 extra coins at 50+ flame; permanent local shelter service");
                return q;
            }
            if(id=="mine_convoy")
            {
                q.title=L("矿工的最后一班车","The Miners' Last Shift");
                q.category=L("护送、战斗与资源选择","ESCORT, COMBAT & RESOURCE CHOICE");
                q.description=L(
                    "矿场监工倒台后，从西北矿车岗亲自护送最后一班矿工。始终跟在矿车附近；在转轨台击退两名袭击者，选择消耗零件修复或保留材料。避开沿途蒸汽预警，将矿车送到南侧矿棚。失败可免费重试。",
                    "After defeating the overseer, physically escort the workers' cart from western dispatch. Stay nearby, defeat two real raiders at the switchyard, choose spare-part reinforcement or conserve resources, dodge steam and deliver to southern shelter. Retries are free.");
                q.room=3;q.locked=!Has("boss_3")||!CanEnter(3);
                q.complete=MineConvoyComplete;
                q.steps.Add(new QuestStep(L("在西北发车岗启动护送","Start the western cart dispatch"),
                    Has("mineconvoy_started")));
                q.steps.Add(new QuestStep(L("击退转轨台袭击者并重新放行",
                    "Defeat switchyard raiders and release the cart"),
                    q.complete||MineConvoyStage>=3));
                q.steps.Add(new QuestStep(L("将最后一班矿工送到南侧矿棚",
                    "Deliver the workers at southern shelter"),
                    q.complete||MineConvoyStage==4));
                // Cart arrival and official registration are deliberately
                // different physical milestones. Old completed v2 saves
                // show both done, while an in-progress arrival stays 3/4.
                q.steps.Add(new QuestStep(L("在南侧矿棚登记撤离并领取奖励",
                    "Register the evacuation at the southern depot"),q.complete));
                int target=MineConvoyStage==2?2:MineConvoyStage>=3?3:1;
                foreach(WorldObject post in data.maps[3].objects)
                    if(post.id=="mineconvoy_"+target)
                    {q.target=new Point(post.x,post.y);break;}
                q.next=q.complete?L("撤离完成，南侧矿棚提供安全休息","Evacuation complete; the shelter now offers safe rest"):
                    MineConvoyStage==4?L("矿车已安全抵达！前往南侧矿棚登记撤离，领取一次性奖励。",
                        "Cart safely delivered! Register the evacuation in person at the southern depot."):
                    q.locked?L("先完成矿场的原始守护者战斗","Defeat Ironreach's original guardian"):
                    MineConvoyActive?L("剩余"+MineConvoyTime.ToString("0")+"秒 · 车厢"+
                        MineConvoyIntegrity.ToString("0")+"/80 · "+MineConvoyStage+"/4",
                        MineConvoyTime.ToString("0")+"s · Hull "+MineConvoyIntegrity.ToString("0")+
                        "/80 · "+MineConvoyStage+"/4"):
                    L("前往西北矿车岗启动撤离","Begin evacuation at western dispatch");
                q.reward=L("首次60硬币、2碎片、80经验、矿棚安全疗养、强化木料成本永久-1",
                    "First clear: 60 coins, 2 shards, 80 XP, safe depot healing and -1 timber per future upgrade");
                return q;
            }

            if(id=="ferry_relief")
            {
                q.title=L("沉没遗迹 · 低潮医疗护送",
                    "Drowned Ruins · Emergency Tide Medicine");
                q.category=L("民用护航 · 真正渡河与战斗",
                    "CIVILIAN FERRY · REAL CROSSING AND COMBAT");
                q.room=2;
                q.locked=!FloodArchiveComplete;
                q.complete=FloodReliefComplete;
                q.description=L(
                    "先找回沉桥两份民用证据并恢复公共渡船。低潮时在西岸领取65秒急救包，亲自支付14耐力乘渡船到东岸，击败两名真正的拦截者后交付。实际命中会损坏药品，东岸可花20耐力开启一次18秒的药品护盾，或花一份草药修补最多40点完整度（均每趟一次）；敌人异常消失或超时不算完成。",
                    "Restore the public ferry first. At low tide, carry urgent medicine from the west dock within 65 seconds, pay 14 energy to cross the actual ferry and defeat two real raiders before delivery on the east shore. Real hits damage medicine; at the east dock, spend 20 energy once on an 18-second cargo ward, or one herb once to repair up to 40 integrity. Missing enemies and timeouts never count as wins.");
                q.steps.Add(new QuestStep(L("恢复公共渡船并调查民用档案",
                    "Restore the public ferry and civic archives"),FloodArchiveComplete));
                q.steps.Add(new QuestStep(L("西岸装载急救包并亲自渡河",
                    "Collect the west-dock medicine and physically ferry across"),
                    FloodReliefCrossed||FloodReliefComplete));
                q.steps.Add(new QuestStep(L("确实击退两名拦截者并在东岸交付",
                    "Defeat both real raiders and hand medicine to the east-bank families"),
                    FloodReliefComplete));
                q.target=FloodReliefCrossed||q.complete?
                    new Point(79.5f,35.5f):new Point(59.5f,37.5f);
                q.next=q.complete?L("东岸急救补给已经完成交接",
                    "The eastern medicine has been genuinely delivered"):
                    q.locked?L("先完成沉桥民用档案，恢复低潮渡船",
                        "Restore the public tide ferry through the archive"):
                    FloodReliefCrossed?(FloodReliefSecured?
                        L("两名拦截者确已击败！立刻在东岸交付医疗包 · 剩余"+
                            (int)Math.Ceiling(FloodReliefTime)+"秒",
                          "Both real raiders defeated! Deliver the medicine at east dock · "+
                            (int)Math.Ceiling(FloodReliefTime)+"s left"):
                        L("击败拦截者 "+FloodReliefDefeated+"/2，保护药品封条 · 剩余"+
                            (int)Math.Ceiling(FloodReliefTime)+"秒",
                          "Defeat raiders "+FloodReliefDefeated+"/2 and guard cargo · "+
                            (int)Math.Ceiling(FloodReliefTime)+"s left")):
                    FloodReliefActive?L("携带药品搭乘西岸低潮渡船 · 剩余"+
                        (int)Math.Ceiling(FloodReliefTime)+"秒",
                        "Take the west-side low-tide ferry with medicine · "+
                        (int)Math.Ceiling(FloodReliefTime)+"s left"):
                    !LowTide?L("前往西岸旧水尺，等待退潮后领取药品",
                        "Reach the western gauge and wait for low tide"):
                    L("在西岸水尺领取急救包，开始真实护送",
                        "Collect medicine at the west gauge to begin escort");
                q.reward=L("首次送达硬币45、碎片1、经验65；公共渡船消耗永久由14降至10耐力；拦截者不提供可刷的战利品",
                    "First delivery: 45 coins, 1 shard, 65 XP. Public ferry permanently costs 10 instead of 14 energy, with no farmable raider loot.");
                return q;
            }

            if(id=="flood_archive")
            {
                q.title=L("沉没遗迹 · 被抹去的渡船","Drowned Ruins · The Erased Ferries");
                q.category=L("潮汐探索 · 民用抉择","TIDE EXPLORATION · CIVILIAN CHOICE");
                q.description=L(
                    "失踪侦察员归来后，调查沉桥深处真实存在的高水尺与低潮名册。依潮位实地记录两份证据，在公议台决定优先救治伤者还是培训摆渡人；两种决定改变潮标附近的安全补给。",
                    "After the scout returns, recover a high-water gauge and a low-tide roster at actual river landmarks. Reconcile the records at the public desk and decide whether to treat survivors or train ferry pilots. Each choice changes local safe refuge services.");
                q.room=2;q.locked=!Has("scout_saved")||!CanEnter(2);q.complete=FloodArchiveComplete;
                string[] labelsZh={"涨潮时抄录旧桥水尺","退潮时找回摆渡名册","在公共渡口决定档案用途"};
                string[] labelsEn={"Read the bridge gauge at high tide","Recover the ferry roster at low tide","Decide the archive's use at the public ferry"};
                int next=FloodArchiveProgress+1;
                for(int i=1;i<=3;i++)
                {
                    q.steps.Add(new QuestStep(L(labelsZh[i-1],labelsEn[i-1]),i<=FloodArchiveProgress));
                    if(i==next)
                        foreach(WorldObject site in data.maps[2].objects)
                            if(site.id=="floodarchive_"+i)
                            {q.target=new Point(site.x,site.y);break;}
                }
                if(q.complete)q.target=new Point(79.5f,35.5f);
                q.next=q.locked?(!Has("scout_saved")?
                    L("先救回荆棘密林的侦察员","First rescue the Thornwood scout"):
                    L("先交回第一枚星核，解锁沉没遗迹","Deliver the first starcore to unlock Drowned Ruins")):
                    q.complete?FloodArchiveChoice==1?
                    L("伤者名册已公开，安全潮标提供疗养","Public roster published; safe tideposts provide recovery"):
                    L("原始水图已保留，潮标培训渡船","Original tide charts saved; pilots now train at tideposts"):
                    next==1?L("寻找旧桥水尺，等待涨潮","Find the upper gauge and wait for high tide"):
                    next==2?L("寻找摆渡名册，等待退潮","Find the submerged roster at low tide"):
                    L("抵达议事台，决定如何使用证据","Choose the outcome at the public ferry desk");
                q.reward=L("两份证据各12经验；终点50硬币、2碎片、80经验，以及地区安全补给",
                    "Two records: 12 XP each; final 50 coins, 2 shards, 80 XP and local safe support");
                return q;
            }

            if(id=="rootsong")
            {
                q.title=L("荆棘密林 · 古根三声","Thornwood · Three Root Voices");
                q.category=L("林地实地解谜","EXPLORATION · TIMED ROOT CIPHER");
                q.description=L("救出侦察员，深入密林寻找三根古树。根据庇护、摆渡与归乡三段历史，依次奏出低、中、高音调；必须等待各自绿光窗口。",
                    "Rescue the scout and find three ancient trunks. Follow the history of shelter, ferry and homecoming with Low, Middle and High tones in the green windows.");
                q.room=1;q.target=new Point(62.5f,34.5f);
                q.locked=!Has("scout_saved");q.complete=RootsongComplete;
                string[] namesZh={"庇护之根 · 低音","摆渡之根 · 中音","归乡之根 · 高音"};
                string[] namesEn={"Shelter Root · Low","Ferryman Root · Middle","Homeward Root · High"};
                for(int i=1;i<=3;i++)
                {
                    q.steps.Add(new QuestStep(L(namesZh[i-1],namesEn[i-1]),Has("rootsong_"+i)));
                    if(i==RootsongProgress+1)
                        foreach(WorldObject stone in data.maps[1].objects)
                            if(stone.id=="rootsong_"+i){q.target=new Point(stone.x,stone.y);break;}
                }
                q.next=q.locked?L("先救回密林侦察员","First rescue the Thornwood scout"):
                    q.complete?L("古根三声已经归一","All three root voices are restored"):
                    L("前往下一根古树，观察绿光并选择正确音调",
                      "Reach the next root, wait for green and play its correct tone");
                q.reward=L("每段15经验；终点45硬币、2碎片、75经验，安全树旁额外耐力恢复",
                    "15 XP per earlier note; final 45 coins, 2 shards, 75 XP and safe energy refuge");
                return q;
            }

            if(id=="civic_conduit")
            {
                q.title=L("第十一章 · 公共电网调谐","Chapter Eleven · The Civic Conduit");
                q.category=L("战后电路 · 行走、相位与避让","POSTWAR GRID · MOVEMENT, TIMING & DODGING");
                q.description=L("最后军令公开后，在堡垒西端捐献熔断器，80秒内步行调谐三个阀门。各阀门须等待自己的绿色相位；遇电弧可花20耐力在当前阀门接地一次，导体受损超过15点时可花一枚零件现场抢修35点（每趟一次）。完成后，八地避险棚获得对应服务。",
                    "After the Last Directive, donate a fuse and walk three timed valves within 80s. Wait for each green phase. Ground each active valve once for 20 energy to cancel live arcs, or use one spare part to repair 35 integrity when the conductor is significantly damaged (once per run). All rebuilt refuges gain their civic service.");
                q.locked=!LastDirectiveComplete;
                q.complete=CivicConduitComplete;
                q.steps.Add(new QuestStep(L("在西端控制台捐献熔断器并开始调谐",
                    "Donate a fuse at the western terminal and begin the run"),CivicConduitStarted));
                for(int i=0;i<3;i++)
                {
                    bool done=CivicConduitComplete || CivicConduitActive&&CivicConduitStage>i;
                    q.steps.Add(new QuestStep(L("实地调谐第 "+(i+1)+" 处稳定阀门",
                        "Physically tune valve "+(i+1)),done));
                }
                if(CivicConduitActive)
                {
                    q.room=4;
                    q.target=CivicConduitTarget(CivicConduitStage);
                    q.next=L("第 "+(CivicConduitStage+1)+"/3 阀门 · "+
                        (CivicConduitWindowOpen?"绿色窗口！":"等待相位变绿")+" · "+
                        (int)Math.Ceiling(CivicConduitTimeLeft)+"秒 · 导体"+
                        (int)Math.Ceiling(CivicConduitIntegrity)+"%",
                        "Valve "+(CivicConduitStage+1)+"/3 · "+
                        (CivicConduitWindowOpen?"GREEN NOW":"Wait for green")+
                        " · "+(int)Math.Ceiling(CivicConduitTimeLeft)+"s · "+
                        (int)Math.Ceiling(CivicConduitIntegrity)+"% charge");
                }
                else
                {
                    q.room=4;q.target=ObjectPoint(4,"civicconduitmaster",new Point(95.5f,74.5f));
                    q.next=q.complete?L("三处开关已经校准，各地公共供能持续生效",
                        "Three valves are tuned; the public network now supports regional refuges"):
                        q.locked?L("先完成第十章的公开民用决议",
                            "Publish Chapter Ten's civic decision first"):
                        L("前往堡垒南侧西端熔断台，真实启动电网",
                            "Go to the western citadel fuse desk to energize the grid");
                }
                q.reward=L("熔断器一枚仅首次消耗；真正完成后硬币65、碎片2、经验95；八地修复避险棚新增永久民用服务",
                    "Fuse charged only once; first clear 65 coins, 2 shards, 95 XP, and a lasting new service at every rebuilt regional refuge");
                return q;
            }

            if(id=="last_directive")
            {
                q.title=L("第十章 · 最后指令","Chapter Ten · The Last Directive");
                q.category=L("主线余波 · 原始证词、破译与守卫战",
                    "STORY AFTERMATH · TESTIMONY, CIPHER & COMBAT");
                q.description=L("三地公共服务名册公开后，在余烬镇接到最后一道未署名的军用指令调查。亲自前往堡垒外围寻找矿工、船工和巡路队的原始证词，依真实时间顺序解除封锁，击败两波实体守卫，选定居民权利，再回城公布。现有主线与结局不会被覆盖。",
                    "After publishing the regional service ledger, investigate an unsigned citadel order. Read three genuine clues across the far citadel, solve their historical sequence, defeat two real guardian waves, choose civic rights, then publish the final order in town. Original endings remain intact.");
                q.locked=!Has("civic_response_ledger");
                q.complete=LastDirectiveComplete;
                q.steps.Add(new QuestStep(L("到余烬镇居民议事台开启调查",
                    "Open the inquiry at Emberfall's public council"),LastDirectiveStarted));
                string[] clueZh={"矿工的停机请求","船工的失联名单","巡路队的最后炉火"};
                string[] clueEn={"Miners' shutdown petition","Ferrymen's lost names","Roadwardens' last hearth"};
                for(int i=0;i<3;i++)
                    q.steps.Add(new QuestStep(L(clueZh[i],clueEn[i]),
                        Has("last_directive_clue_"+i)));
                q.steps.Add(new QuestStep(L("破解终端并击退两道实体封锁",
                    "Decode the terminal and defeat both real security waves"),
                    LastDirectiveChoice>0));
                q.steps.Add(new QuestStep(L("向余烬镇公开最后指令",
                    "Publish the final order in Emberfall"),LastDirectiveComplete));
                if(!LastDirectiveStarted||LastDirectiveChoice>0)
                {
                    q.room=0;q.target=ObjectPoint(0,"lastdirectiveboard",new Point(19.5f,15.5f));
                    q.next=q.complete?L("最后指令已经公开；永久民用权限持续有效",
                        "Last directive published; permanent civilian rights are active"):
                        !LastDirectiveStarted?L("在余烬镇居民议事台接取调查",
                            "Begin the inquiry at Emberfall's public council"):
                            L("返回议事台把新权限公开给所有居民",
                                "Return to the public council and publish the new rights");
                }
                else
                {
                    q.room=4;
                    int next=0;while(next<3&&Has("last_directive_clue_"+next))next++;
                    string obj=next<3?"lastdirectiveclue"+next:"lastdirectivecore";
                    q.target=ObjectPoint(4,obj,new Point(100.5f,73.5f));
                    q.next=next<3?L("亲自访问下一处堡垒外环证词","Reach the next physical citadel testimony"):
                        LastDirectiveWave==1||LastDirectiveWave==3?
                            L("击败守卫，实际战斗不可跳过","Defeat real guardians in combat"):
                        LastDirectiveWave==2?L("回终端投入25耐力开启二级封锁",
                            "At the terminal spend 25 energy to release the second lock"):
                        LastDirectiveWave==4?L("在终端确定最后一道公共权利",
                            "Choose lasting public rights at the terminal"):
                            L("根据证词时间顺序破解终端","Decode the terminal from the historical testimony");
                }
                q.reward=L("原始证词各经验14；最终公开硬币95、碎片5、技能点1、经验140，选择永久监督恢复耐力或避难疗伤",
                    "Each testimony +14 XP; publication +95 coins, 5 shards, 1 talent, 140 XP, plus a lasting choice between public energy or emergency healing");
                return q;
            }

            if(id=="civic_services")
            {
                q.title=L("第九章续篇 · 三地公共服务实地考验",
                    "Chapter Nine Aftermath · Live Civic Service Trials");
                q.category=L("医疗护送与守路防卫","MEDICAL TRANSPORT & REAL DEFENSE");
                q.description=L("三地审计公开后，每座居民选择的公共避险棚仍有真正的工作：医疗点需要从避险棚护送药包到北方检查台；守路岗必须真实击退两名袭击者。完成提高服务恢复效率。每地区只能领取一次首次完成奖励。",
                    "After the public inquest, clinics need a real timed medicine walk to the northern checkpoint; lookout stations must defeat two real raiders. Each earned trial improves the region's refuge recovery; rewards are once only.");
                q.locked=!CivicSupplyComplete;
                q.complete=Has("civic_response_ledger");
                int[] sites={0,3,5};
                int next=-1;
                string[] placeZh={"引水渠","赤铁矿场","雾港"};
                string[] placeEn={"Canal","Ironreach","Mistport"};
                for(int i=0;i<sites.Length;i++)
                {
                    int room=sites[i],done=CivicResponseDone(room)?1:0;
                    q.steps.Add(new QuestStep(L(placeZh[i],placeEn[i])+
                        (done==1?" ✓":" · 0/1"),done==1));
                    if(next<0&&done==0&&CanEnter(room))next=room;
                }
                q.steps.Add(new QuestStep(L("炉火旅店公开三地应急名册",
                    "Publish the response ledger at Hearth Inn"),
                    Has("civic_response_ledger")));
                if(CivicResponseActive)
                {
                    q.room=save.room;
                    q.target=CivicResponseClinicRun?
                        ObjectPoint(save.room,"farcheck",new Point(111.5f,61.5f)):
                        ObjectPoint(save.room,"farshelter",new Point(112.5f,73.5f));
                    q.next=CivicResponseClinicRun?
                        L("亲自把药包送到检查台，剩余 "+(int)CivicResponseTimeLeft+" 秒",
                          "Deliver medicine to checkpoint · "+(int)CivicResponseTimeLeft+"s left"):
                        CivicResponseEnemiesAlive>0?
                        L("击败守路岗前两名真实袭击者","Defeat both real lookout raiders"):
                        L("返回避险棚签署守路记录","Return to refuge and certify defense");
                }
                else
                {
                    q.room=next<0?9:next;
                    q.target=next<0?
                        ObjectPoint(9,"farrefugerecord",new Point(22.5f,33.5f)):
                        ObjectPoint(q.room,"farshelter",new Point(112.5f,73.5f));
                    q.next=q.complete?L("三处公共服务已经实地完成","All three civilian services verified"):
                        q.locked?L("先公开第九章的三地救援审计",
                            "First publish the Chapter Nine civic inquest"):
                        next<0?L("三地救援已实地验证，回旅店公开公共应急名册",
                            "Return to Hearth Inn's guest registry and publish the response ledger"):
                        CivicSupplyClinic(next)?
                            L("到南端避险棚领取医药并步行送到北方检查站",
                                "Collect medical supplies at southern refuge; walk to checkpoint"):
                            L("在南端避险棚启动守路防卫并击败实际敌人",
                                "Start the lookout defense and defeat real attackers");
                }
                q.reward=L("每地硬币24、碎片1、经验42、服务升级；三地完成技能点1；名册公开硬币40、碎片2、经验55并解锁八地基础疗养",
                    "Each region 24 coins, 1 shard, 42 XP, improved service; all three +1 talent; ledger publication +40 coins, 2 shards, 55 XP and basic care in every rebuilt refuge");
                return q;
            }

            if(id=="civic_aftercare")
            {
                q.title=L("第十二章后续 · 三地巡路复核",
                    "Chapter Twelve Aftermath · Three-Region Road Audit");
                q.category=L("开放世界 · 公共设施维护",
                    "OPEN WORLD · CIVIC MAINTENANCE");
                q.description=L(
                    "公共电网重新接通后，居民需要确认安全路线仍然能走。先在炉火旅店接取复核，再回访引水渠、赤铁矿场与雾港各一处旧路标，最后回旅店公开现场记录。每个地区只计一次；公开后只发放一次奖励。",
                    "After the public grid is restored, residents need to verify that its safe routes remain usable. Accept the audit at Hearth Inn, revisit one old route marker each at Canal, Ironreach and Mistport, then publish the field record at the Inn. Each region counts once and the reward is one-time.");
                q.locked=!CivicConduitComplete;
                q.complete=Has("civic_aftercare_published");
                q.steps.Add(new QuestStep(L("在炉火旅店接取复核",
                    "Accept the audit at Hearth Inn"),
                    Has("civic_aftercare_started")||q.complete));
                int next=-1;
                string[] namesZh={"引水渠","赤铁矿场","雾港"};
                string[] namesEn={"Canal","Ironreach","Mistport"};
                for(int i=0;i<CivicAftercareRegions.Length;i++)
                {
                    int room=CivicAftercareRegions[i];
                    bool done=Has("civic_aftercare_waytrail_"+room);
                    q.steps.Add(new QuestStep(L(namesZh[i],namesEn[i]),done));
                    if(next<0&&!done)next=room;
                }
                q.steps.Add(new QuestStep(L("回旅店公布记录并领取首次奖励",
                    "Publish the record at Hearth Inn for the first-clear reward"),q.complete));
                if(q.complete)
                {
                    q.room=9;
                    q.target=ObjectPoint(9,"innGuest",new Point(20,12));
                    q.next=L("三地巡路复核已公开","Three-region road audit published");
                }
                else if(q.locked||!Has("civic_aftercare_started"))
                {
                    q.room=9;
                    q.target=ObjectPoint(9,"innGuest",new Point(20,12));
                    q.next=q.locked?
                        L("先完成公共电网的三处现场调谐","First complete the three physical grid valves"):
                        L("与炉火旅店的夜行旅人交谈，接取复核",
                          "Talk to the night traveler at Hearth Inn to begin");
                }
                else if(next<0)
                {
                    q.room=9;
                    q.target=ObjectPoint(9,"innGuest",new Point(20,12));
                    q.next=L("三处信标已复核，回旅店公布记录",
                        "All three markers verified; return to the Inn to publish");
                }
                else
                {
                    q.room=next;
                    q.target=ObjectPoint(next,"waytrail",new Point(18,15));
                    q.next=!CanEnter(next)?
                        L("继续主线以开放该地区","Advance the main story to unlock this region"):
                        L("与本地区任一旧路标交互，记录现场复核",
                          "Interact with an old route marker in this region");
                }
                q.reward=L("首次公开：硬币35、碎片1、经验45；每地区只计一次，公开后不可重复领取",
                    "First publication: 35 coins, 1 shard, 45 XP; each region counts once, with no repeat reward.");
                return q;
            }

            if(id=="civic_supply")
            {
                q.title=L("第九章 · 居民补给审计","Chapter Nine · The People's Supply Inquest");
                q.category=L("后日谈 · 分支抉择与持续服务",
                    "AFTERMATH · COMMUNITY CHOICES");
                q.description=L(
                    "真正送达第一份救援包后，重访炉火旅店的居民住客名单。在引水渠、赤铁矿场和雾港的检查台，为已收货的居民选择医疗点或守路岗。材料只能投入其中一项，避险棚的实际恢复能力随选择改变。三地决定完毕后，返回旅店公开账目。",
                    "After your first authentic parcel delivery, revisit Hearth Inn's guest registry. At the canal, Ironreach and Mistport checkpoints, let communities choose medical care or route watching. Both cost real materials and change their refuge's live recovery. Return to the Inn to publish all three decisions.");
                q.locked=FarCourierDeliveredCount==0;
                q.complete=CivicSupplyComplete;
                q.steps.Add(new QuestStep(L("从旅店居民名单开启审计",
                    "Open the inquest at the guest registry"),CivicSupplyStarted));
                string[] regionsZh={"引水渠","赤铁矿场","雾港"};
                string[] regionsEn={"Canal","Ironreach","Mistport"};
                int[] sites={0,3,5};
                int next=-1;
                for(int i=0;i<sites.Length;i++)
                {
                    int room=sites[i];bool done=CivicSupplyResolved(room);
                    string chosen=done?CivicSupplyClinic(room)?
                        L(" · 医疗点"," · Clinic"):L(" · 守路岗"," · Lookout"):"";
                    q.steps.Add(new QuestStep(L(regionsZh[i],regionsEn[i])+chosen,done));
                    if(next<0&&!done)next=room;
                }
                q.steps.Add(new QuestStep(L("返回旅店公开三地共同账目",
                    "Publish the three-region public accounts"),CivicSupplyComplete));
                if(!CivicSupplyStarted || next<0)
                {
                    q.room=9;
                    q.target=ObjectPoint(9,"farrefugerecord",new Point(22.5f,33.5f));
                    q.next=q.complete?L("三地社区继续维持各自选择的救援服务",
                        "All three communities maintain their chosen refuge services"):
                        !CivicSupplyStarted?L("重访炉火旅店的居民名单，接取三地审计",
                            "Revisit Hearth Inn's guest registry to begin"):
                            L("三地决定齐备，回旅店居民名单签发审计公报",
                                "Return to the guest registry and publish the inquest");
                }
                else
                {
                    q.room=next;
                    q.target=ObjectPoint(next,
                        FarCourierDelivered(next)?"farcheck":"farbridge",
                        new Point(111.5f,61.5f));
                    q.next=!CanEnter(next)?
                        L("继续主线以开放下一片地区","Advance the story to unlock this region"):
                        !FarCourierDelivered(next)?
                        L("先在该地区修桥、送货，再与居民决定公共维护方案",
                            "Rebuild its bridge, deliver a parcel, then choose local services"):
                        L("到居民检查站选择医疗或守路，并付出真实材料",
                            "Choose a clinic or lookout at the checkpoint using real materials");
                }
                q.reward=L("三地服务各获独立永久效果；每地经验 +22；公开后硬币48、碎片2、技能点1、经验70",
                    "Three lasting local service upgrades, +22 XP per site; publication: 48 coins, 2 shards, 1 talent and 70 XP");
                return q;
            }

            if(id=="far_couriers")
            {
                q.title=L("归乡送达 · 八地居民救援包","Homeward Deliveries · Eight Civilian Parcels");
                q.category=L("真实步行运输 · 修桥后的生活","ACTUAL WALKING · LIFE AFTER REPAIR");
                q.description=L("先修好桥与避险棚，95秒内背包步行过桥。桥南有两名真正出击的劫匪：可迂回正常交付，也可亲手击败两人并送达，获得每地一次安全护送奖金。检查站可消耗零件修补或花30耐力开启护送结界。传送不能代替亲手交付。",
                    "Repair bridge and refuge, then deliver on foot within 95s. Evade the two real raiders for normal delivery, or defeat BOTH in combat and complete the run for a one-time secure-route bonus per region. Repair cargo or buy a temporary ward at the checkpoint; teleporting never qualifies.");
                q.locked=!Has("intro");q.complete=FarCourierDeliveredCount==8;
                int region=-1;
                for(int i=0;i<8;i++)
                {
                    bool done=FarCourierDelivered(i);
                    q.steps.Add(new QuestStep(L(data.maps[i].name,data.maps[i].en),done));
                    if(region<0&&!done&&CanEnter(i)&&FarBridgeOpen(i)&&FarShelterBuilt(i))region=i;
                }
                if(FarCourierActive)
                {
                    q.room=save.room;q.target=!FarCourierCrossed?new Point(111.5f,53.5f):
                        !FarCourierStamped?new Point(111.5f,61.5f):new Point(112.5f,73.5f);
                    if(FarCourierStamped)foreach(WorldObject obj in Map.objects)
                        if(obj.kind=="farshelter"){q.target=new Point(obj.x,obj.y);break;}
                    q.next=!FarCourierCrossed?L("真正背包从北向南过桥","Cross the real bridge on foot"):
                        !FarCourierStamped?L("在中途居民检查台核验封条","Verify the parcel at the checkpoint"):
                        L("前往避险棚，剩余 "+(int)FarCourierTimeLeft+" 秒",
                            "Deliver at refuge · "+(int)FarCourierTimeLeft+"s left");
                }
                else
                {
                    q.room=region<0?0:region;q.target=new Point(111.5f,49.5f);
                    q.next=q.complete?L("八地全部实地送达","All eight parcels delivered on foot"):
                        region<0?L("先修好至少一处桥和远端避险棚","Repair a bridge and distant refuge first"):
                        L("到修好的桥头背上居民救援包","Collect a parcel at the rebuilt bridge");
                }
                q.steps.Add(new QuestStep(L("在炉火旅店签发公开补给公约","Publish the public supply charter at the Hearth Inn"),
                    Has("far_courier_charter")));
                q.complete=Has("far_courier_charter");
                if(FarCourierDeliveredCount==8&&!Has("far_courier_charter"))
                {
                    q.room=9;q.target=new Point(22.5f,36.5f);
                    q.next=L("前往旅店制图室公开八份补给回执","Publish the signed manifests at the Hearth Inn");
                }
                q.reward=L("每地送达硬币25、碎片1、经验38；真实击败两名劫匪并交付额外硬币10、经验15；八地技能点1；公开公约后强化避险棚",
                    "Each delivery: 25 coins, 1 shard, 38 XP. Defeat both raiders and deliver for +10 coins, +15 XP once per region. All eight: +1 talent; the charter upgrades refuges.");
                return q;
            }

            if(id=="far_bridges")
            {
                q.title=L("八地民用补给桥 · 恢复实际步行近路","Eight Civilian Bridges · Reopen Real Trails");
                q.category=L("实地建设 · 永久改变地形","FIELD CONSTRUCTION · PERMANENT TERRAIN");
                q.description=L("在八个第六圈地图的北侧断桥处，消耗木材2与零件1修桥。修复会将原本无法站立的桥面变成真正可行走的地形；之前的绕路仍可用。",
                    "At each remote sixth-ring crossing, spend 2 timber and 1 spare part. Repair turns genuinely blocked bridge tiles into a permanent walkable shortcut, while preserving the old detour.");
                q.locked=!Has("intro");q.complete=FarBridgeCount==8;
                int target=-1;
                for(int room=0;room<8;room++)
                {
                    bool opened=FarBridgeOpen(room);
                    q.steps.Add(new QuestStep(L(data.maps[room].name,data.maps[room].en),opened));
                    if(target<0&&!opened&&CanEnter(room))target=room;
                }
                q.room=target<0?0:target;q.target=new Point(18,15);
                if(target>=0)
                    foreach(WorldObject station in data.maps[target].objects)
                        if(station.kind=="farbridge")
                        {q.target=new Point(station.x,station.y);break;}
                q.next=q.complete?L("所有民用桥已重新开放","All civilian crossings reopened"):
                    target<0?L("先解锁其余地区","Unlock the remaining regions"):
                    L("寻找北侧真实断桥修复点","Find the actual northern repair post");
                q.reward=L("每桥经验24；修完八桥再获得技能点1，实际缩短路线",
                    "24 XP per bridge and 1 talent after all eight; shorter walkable routes");
                return q;
            }

            if(id=="far_refuges")
            {
                q.title=L("远地气象与共用避险棚","Far Weather Logs · Shared Refuges");
                q.category=L("实地谜题 · 可用营地","FIELD PUZZLES · LIVE SHELTERS");
                q.description=L("第六圈每片地区有三处真正可到访的站点：读东部气象记录得到符号，在南部校准盘选择正确符号，再消耗木材和零件修复公共避险棚。修好后可反复消耗木材恢复生命、耐力并驱寒。",
                    "Each sixth-ring region has three physical sites. Read the eastern weather record, choose its correct glyph at the southern dial, then spend timber and a spare part to rebuild a public refuge. An open shelter provides repeatable rest.");
                q.locked=!Has("intro");q.complete=Has("far_refuge_atlas");
                int next=-1,step=0;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场","堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Ruins","Ironreach","Citadel","Mistport","Glasswind","Frostchime"};
                for(int i=0;i<8;i++)
                {
                    int found=FarLogFound(i)?1:0;
                    if(FarDialReady(i))found++;
                    if(FarShelterBuilt(i))found++;
                    q.steps.Add(new QuestStep(L(zh[i],en[i])+"  "+found+"/3",found==3));
                    if(next<0&&found<3&&CanEnter(i)){next=i;step=found;}
                }
                q.steps.Add(new QuestStep(L("回旅店档案室签发公共总图","Publish the civic atlas at the Inn"),
                    Has("far_refuge_atlas")));
                q.room=next<0?9:next;q.target=next<0?new Point(22.5f,36.5f):new Point(18,15);
                if(next>=0)foreach(WorldObject station in data.maps[next].objects)
                    if(station.id=="farsignal_"+next+"_"+step)
                    {q.target=new Point(station.x,station.y);break;}
                q.next=q.complete?L("八座避险棚与公共总图全部完成","All eight shelters and public atlas complete"):
                    next<0?L("前往旅店新制图档案室签发公共总图",
                        "Publish the civic atlas in the expanded Inn archive"):
                    step==0?L("实地寻找最东侧气象手记","Find the eastern weather log"):
                    step==1?L("去南侧校准盘核对记录符号","Match the reading at the southern dial"):
                    L("去最远端建成开放的公共避险棚","Restore the outermost civilian shelter");
                q.reward=L("每地硬币22、碎片1、经验60与永久暖棚；八地技能点1，旅店归档再得硬币80、碎片3、经验50",
                    "Each: 22 coins, 1 shard, 60 XP and refuge; eight: +1 talent. Inn publication: 80 coins, 3 shards, 50 XP.");
                return q;
            }


            if(id=="ridge_watch")
            {
                q.title=L("高地巡守 · 民用通道","Ridge Watch · Civilian High Trails");
                q.category=L("开放地图 · 巡路战斗","OPEN WORLD · PATROL COMBAT");
                q.description=L("两端高地路标已联通、地区守护者已经击败后，真正清理第五圈路外巡敌，为居民恢复偏远高路。每处击杀记录可以延续到旧存档，只有原有敌人的实际战斗死亡才算。",
                    "After both ridge markers are linked and the regional guardian has fallen, defeat the actual high-trail hostiles so residents can use the far approaches. Each real combat kill is saved in existing progress flags.");
                q.reward=L("每地首次清除巡敌：硬币28、碎片1、经验40；该区高处道路耐力恢复+2/秒；不重复结算",
                    "First cleared patrol per region: 28 coins, 1 shard, 40 XP; +2 energy/sec on that region's high trails; never farmable");
                q.locked=true;q.complete=true;
                int next=-1;
                for(int i=1;i<=7;i++)
                {
                    bool ready=RidgeLinked(i)&&Has("boss_"+i);
                    bool cleared=RidgeWatchComplete(i);
                    q.steps.Add(new QuestStep(L(data.maps[i].name,data.maps[i].en)+
                        "  "+RidgeWatchProgress(i)+"/3",cleared));
                    if(!cleared)q.complete=false;
                    if(ready)q.locked=false;
                    if(ready&&!cleared&&CanEnter(i)&&
                        (next<0||i==save.room))next=i;
                }
                q.room=next<0?save.room:next;
                q.target=Position;
                if(next>=0)
                {
                    q.target=ObjectPoint(next,"ridgemarker",new Point(80.5f,66.5f));
                    // In the actual region point at a currently alive, uncredited
                    // spawned patrol instead of a static marker or a corpse.
                    if(next==save.room&&ridgeWatchRoom==save.room&&
                        Object.ReferenceEquals(ridgeWatchMap,Map))
                    {
                        float nearest=float.MaxValue;
                        foreach(Enemy e in enemies)
                        {
                            int patrolIndex;
                            if(!ridgeWatchIds.TryGetValue(e.id,out patrolIndex)||
                                Has("ridge_watch_"+next+"_"+patrolIndex)||e.hp<=0f)
                                continue;
                            float distance=(e.Position-Position).Length;
                            if(distance>=nearest)continue;
                            nearest=distance;
                            q.target=e.Position;
                        }
                    }
                }
                q.next=q.complete?L("全部七处野外巡路已经安全","All seven high-trail watches are clear"):
                    next<0?L("先连通高地两端路标，并击败地区守护者",
                        "Link both ridge markers and defeat a regional guardian first"):
                    next==save.room?L("前往仍在活动的真实巡敌处，清理剩余危险",
                        "Reach the living hostile patrols and clear the remaining danger"):
                    L("前往已联通高地的地区，清理居民通道",
                        "Travel to the linked highland region and clear its civilian passage");
                return q;
            }

            if(id=="ridge_routes")
            {
                q.title=L("高地联络线 · 远端重连","Outer Ridge Links · Survey and Reconnect");
                q.category=L("开放地图 · 安全捷径","OPEN WORLD · SAFE SHORTCUTS");
                q.description=L("第五圈的高地与南部河湾各有一座旧地标。必须亲自走到两个端点，联通居民的安全巡路。联通后可在不受追击时消耗耐力往返。",
                    "The new ridge and southern basin each hold a forgotten marker. Visit both physically to reopen a civilian crossing. Afterward, spend stamina to travel between them outside combat.");
                q.locked=!Has("intro");q.complete=RidgeLinkedCount==8;
                int target=-1,side=0;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场","堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Drowned Ruins","Ironreach","Citadel","Mistport","Glasswind","Frostchime"};
                for(int i=0;i<8;i++)
                {
                    bool first=RidgeSeen(i,0),second=RidgeSeen(i,1);
                    q.steps.Add(new QuestStep(L(zh[i],en[i])+
                        "  "+((first?1:0)+(second?1:0))+"/2",first&&second));
                    if(target<0&&!RidgeLinked(i)&&CanEnter(i))
                    {
                        target=i;side=first?1:0;
                    }
                }
                q.room=target<0?0:target;q.target=new Point(18,15);
                if(target>=0)
                {
                    string idTarget="ridge_route_"+target+"_"+side;
                    foreach(WorldObject o in data.maps[target].objects)
                        if(o.id==idTarget){q.target=new Point(o.x,o.y);break;}
                }
                q.next=q.complete?L("八处高地联络线全部开通","All eight outer ridge links restored"):
                    target<0?L("解锁其余地区后继续巡路","Unlock remaining regions"):
                    L("亲自到访另一端的高地地标","Walk to the next distant ridge marker");
                q.reward=L("各地奖励硬币20、碎片1、经验42；可使用双端安全捷径；八地技能点+1",
                    "Per region: 20 coins, 1 shard, 42 XP and fast crossing; all eight: +1 talent");
                return q;
            }

            if(id=="outer_watch")
            {
                q.title=L("八地守望 · 救援物资保卫战","Eight Relief Watches · Secure the Supplies");
                q.category=L("开放地图 · 真实战斗","EXPLORATION · FIELD COMBAT");
                q.description=L("找回第四圈的救援箱后，在附近的哨岗主动开始防卫。击退实际出现的两只敌人，保护运往居民避难所的物资。离开或战败不会消耗未完成的挑战。",
                    "After recovering a fourth-ring relief chest, choose to defend its nearby watchpost. Defeat two actual enemies. Leaving or losing cancels that attempt without destroying earlier progress.");
                q.locked=OuterRescueCount==0;
                q.complete=OuterPatrolWins==8;
                int target=-1;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场","堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Ruins","Ironreach","Citadel","Mistport","Glasswind","Frostchime"};
                for(int room=0;room<8;room++)
                {
                    q.steps.Add(new QuestStep(L(zh[room],en[room])+
                        (OuterPatrolDone(room)?" ✓":" · 0/1"),OuterPatrolDone(room)));
                    if(target<0&&!OuterPatrolDone(room)&&
                        OuterCacheRecovered(room)&&CanEnter(room))target=room;
                }
                q.room=target<0?0:target;
                q.target=new Point(18,15);
                if(target>=0)
                    foreach(WorldObject o in data.maps[target].objects)
                        if(o.kind=="outerwatch")
                        {q.target=new Point(o.x,o.y);break;}
                q.next=q.complete?L("全部八处救援岗已守住","All eight relief outposts defended"):
                    target<0?L("先找回至少一处外环救援箱","Recover an outer-ring rescue chest first"):
                    L("走到救援岗主动发起防卫","Reach the watchpost and start the defense");
                q.reward=L("每岗硬币 +14、碎片 +1、经验 +28；八岗全守住再得1技能点",
                    "Each defense: +14 coins, +1 shard, +28 XP; all eight: +1 talent point");
                return q;
            }

            if(id=="outer_watch_flawless")
            {
                q.title=L("无损守卫 · 八地荣誉挑战",
                    "Flawless Watches · Eight Honor Challenges");
                q.category=L("精英挑战 · 战斗精通",
                    "VETERAN CHALLENGE · COMBAT MASTERY");
                q.description=L(
                    "在每处真正的精英三人防卫中击退全部敌人，物资剩余至少90%，全过程不使用抢修或路障。可重新挑战已完成的精英防卫争取徽章；旧奖励不会重复发放。",
                    "Defeat all three live veteran raiders with >=90% cache integrity, no repairs and no barricade. Replay cleared veteran battles to earn medals without duplicating earlier loot.");
                q.locked=OuterPatrolVeteranWins==0;
                q.complete=OuterPatrolFlawlessWins==8;
                int target=-1;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场",
                    "堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Ruins","Ironreach",
                    "Citadel","Mistport","Glasswind","Frostchime"};
                for(int room=0;room<8;room++)
                {
                    bool medal=OuterPatrolFlawlessDone(room);
                    bool unlocked=OuterPatrolDone(room);
                    q.steps.Add(new QuestStep(L(zh[room],en[room])+
                        (medal?" ✓":unlocked?" · 可挑战无损防卫":" · 先守住普通救援岗"),
                        medal));
                    if(target<0&&!medal&&unlocked&&CanEnter(room))
                        target=room;
                }
                q.room=target<0?0:target;
                q.target=new Point(18,15);
                if(target>=0)
                    foreach(WorldObject o in data.maps[target].objects)
                        if(o.kind=="outerwatch")
                        {q.target=new Point(o.x,o.y);break;}
                q.next=q.complete?L("八地无损防卫已全部获得徽章",
                    "All eight flawless watch medals earned"):
                    target<0?L("先解锁救援岗和精英挑战",
                        "Unlock a defended veteran outpost first"):
                    L("在标记哨岗完成无路障、无抢修的真实精英战",
                        "Defend the marked cache without repairs or a barricade");
                q.reward=L("每枚徽章强化当地安全医疗恢复；八地全部完成再得技能点 +1",
                    "Each medal improves care at that safe outpost; all eight: +1 talent point");
                return q;
            }

            if(id=="outer_watch_veteran")
            {
                q.title=L("精英巡防 · 八地补给岗","Veteran Patrol · Eight Relief Outposts");
                q.category=L("精英挑战 · 战斗与野外补给",
                    "VETERAN CHALLENGE · DEFENSE & FIELD CARE");
                q.description=L(
                    "每处救援岗的普通战斗通关后，仍可主动开启 110 秒进阶防卫。近战、游击射手与重甲撞击手会真实攻击你或补给箱。击退三人可永久强化当地的医疗与补给。再次挑战不重复领奖。",
                    "After clearing normal defense, challenge the 110-second veteran team at each outpost. Melee, archer and heavy slammer attack you and the real cache. Defeat all three to improve local treatment. Replays award no duplicate loot.");
                q.locked=OuterPatrolWins==0;
                q.complete=OuterPatrolVeteranWins==8;
                int target=-1;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场",
                    "堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Ruins","Ironreach",
                    "Citadel","Mistport","Glasswind","Frostchime"};
                for(int room=0;room<8;room++)
                {
                    bool done=OuterPatrolVeteranDone(room);
                    bool available=OuterPatrolDone(room);
                    q.steps.Add(new QuestStep(L(zh[room],en[room])+
                        (done?" ✓":available?" · 精英挑战已解锁":" · 先守住普通补给"),
                        done));
                    if(target<0&&!done&&available&&CanEnter(room))
                        target=room;
                }
                q.room=target<0?0:target;
                q.target=new Point(18,15);
                if(target>=0)
                    foreach(WorldObject o in data.maps[target].objects)
                        if(o.kind=="outerwatch")
                        {q.target=new Point(o.x,o.y);break;}
                q.next=q.complete?L("全部八处精英补给岗已强化",
                    "All eight veteran outposts upgraded"):
                    target<0?L("先完成一处普通物资防卫，开启进阶挑战",
                        "Defend one standard supply post to unlock veteran challenges"):
                    L("到已守住的救援岗发起三人精英防卫",
                        "Challenge three live raiders at a defended outpost");
                q.reward=L("每处首次获胜：硬币26、碎片2、经验60，强化安全恢复；八处再得技能点1",
                    "First clear each: 26 coins, 2 shards, 60 XP, better local care; all eight: +1 talent");
                return q;
            }

            if(id=="outer_rescue")
            {
                q.title=L("边境求援灯 · 遗失的信号","Frontier Rescue Lights · Lost Signals");
                q.category=L("探索支线 · 野外修复","EXPLORATION · FIELD REPAIRS");
                q.description=L("每片边境的第四圈都有南侧的遇险记录、东部失明的信号灯和救援物资。先调查记录，消耗一枚零件修复信号，再领取未送出的救援箱。",
                    "The outer fourth ring holds a lost log, a dark eastern relay and a relief cache. Recover the log, repair the relay with one spare part, then retrieve its relief supplies.");
                q.locked=!Has("intro");q.complete=OuterRescueCount==8;
                int room=-1,phase=0;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场","堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Ruins","Ironreach","Citadel","Mistport","Glasswind","Frostchime"};
                for(int i=0;i<8;i++)
                {
                    int stage=OuterCacheRecovered(i)?3:OuterRelayRestored(i)?2:OuterLogFound(i)?1:0;
                    q.steps.Add(new QuestStep(L(zh[i],en[i])+"  "+stage+"/3",stage==3));
                    if(room<0 && stage!=3 && CanEnter(i)){room=i;phase=stage;}
                }
                q.room=room<0?0:room;
                q.target=new Point(18,15);
                if(room>=0)
                    foreach(WorldObject point in data.maps[room].objects)
                        if(point.id=="outer_"+room+"_"+phase)
                        {q.target=new Point(point.x,point.y);break;}
                q.next=q.complete?L("八座求援灯与救援箱全部确认","Eight rescue stations restored"):
                    room<0?L("继续解锁地区后调查余下的旧信号","Unlock the remaining regions to continue"):
                    phase==0?L("前往南部外环寻找书面证据","Find the southern outer-ring log"):
                    phase==1?L("抵达东侧信号灯并使用一枚零件","Repair the eastern beacon using one part"):
                    L("寻找东南救援箱，领取救援物资","Recover the southeastern relief cache");
                q.reward=L("各地硬币18、材料2、经验22；八地全修复获得额外技能点",
                    "Each region: 18 coins, 2 materials, 22 XP; all eight: +1 talent point");
                return q;
            }

            if (id == "trail_circuits")
            {
                q.title=L("巡路队 · 八地长路","Roadwardens · Eight Long Routes");
                q.category=L("开放地图 · 实地探路","EXPLORATION · FIELD ROUTES");
                q.description=L("八处室外地区各有三个分散的旧路标，必须按编号实际走到。记录失落的公共路线，恢复新支路的耐力补给。",
                    "Eight outdoor regions each have three separated ancient waymarks. Walk them in order to restore the lost civilian routes and their local stamina support.");
                int next=-1;
                string[] zh={"引水渠","荆棘密林","沉没遗迹","赤铁矿场","堡垒外环","雾港","玻璃沙海","霜钟高原"};
                string[] en={"Canal","Thornwood","Drowned Ruins","Ironreach","Citadel","Mistport","Glass Desert","Frostchime"};
                for(int i=0;i<8;i++)
                {
                    q.steps.Add(new QuestStep(L(zh[i],en[i])+"  "+TrailCircuitProgress(i)+"/3",TrailCircuitComplete(i)));
                    if(next<0&&!TrailCircuitComplete(i)&&CanEnter(i))next=i;
                }
                q.locked=!Has("intro");q.complete=TrailCircuitCount==8;
                q.room=next<0?0:next;
                q.target=new Point(18,15);
                int stage=next<0?3:TrailCircuitProgress(next)+1;
                if(next>=0)foreach(WorldObject o in data.maps[next].objects)
                    if(o.id=="trailcircuit_"+next+"_"+stage)
                    {q.target=new Point(o.x,o.y);break;}
                q.next=q.complete?L("八条实地长路已全部测绘","All eight physical routes surveyed"):
                    next<0?L("先解锁剩余区域，再补完长路","Unlock the remaining regions"):
                    L("依照指引前往下一个编号旧路标","Follow the next numbered waymark");
                q.reward=L("每地硬币 +24、碎片 +2、经验 +35、区域耐力补给；全八地技能点 +1",
                    "Each: +24 coins, +2 shards, +35 XP, local energy recovery; all eight: +1 talent point");
                return q;
            }

            if (id == "annex_explorer")
            {
                q.title=L("边境探路者 · 归乡星图","Frontier Pathfinder · Homeward Atlas");
                q.category=L("探索收集 · 真实足迹","EXPLORATION · LIVING ROUTES");
                q.locked=!Has("intro");q.complete=Has("annex_archive_complete");
                q.description=L("走访八个室外地区的新支路，打开遗失补给、登上观景点并造访营火，再去炉火旅店的扩建档案室发布真正的公共星图。",
                    "Find both supply caches, the outlook and the camp in each of eight outdoor regions. Return to the expanded Hearth Inn archive to publish your living atlas.");
                int camps=0,caches=0,vistas=0,targetRoom=-1;
                WorldObject next=null;
                for(int region=0;region<8;region++)
                {
                    string[] ids={"annex_cache_"+region+"_a","annex_cache_"+region+"_b","annex_vista_"+region,"annex_camp_"+region};
                    for(int j=0;j<4;j++)
                    {
                        string kind=j<2?"annex_cache_":j==2?"annex_vista_":"annex_camp_";
                        if(Has(kind+ids[j]))
                        {if(j<2)caches++;else if(j==2)vistas++;else camps++;}
                        else if(targetRoom<0&&CanEnter(region))
                        {
                            targetRoom=region;
                            foreach(WorldObject o in data.maps[region].objects)
                                if(o.id==ids[j]){next=o;break;}
                        }
                    }
                }
                q.steps.Add(new QuestStep(L("找到全部营火 ","Visit all camps ")+camps+"/8",camps==8));
                q.steps.Add(new QuestStep(L("确认所有观景点 ","Survey all lookouts ")+vistas+"/8",vistas==8));
                q.steps.Add(new QuestStep(L("找回所有补给 ","Recover all caches ")+caches+"/16",caches==16));
                q.steps.Add(new QuestStep(L("前往旅店公开归乡星图","Publish the atlas at Hearth Inn"),q.complete));
                q.room=targetRoom<0?9:targetRoom;
                q.target=next==null?ObjectPoint(9,"annexinnarchive",new Point(18.5f,27.5f)):
                    new Point(next.x,next.y);
                q.next=q.complete?L("归乡图已公开；所有地区的旧路均已记录",
                    "Public atlas complete; old trails are permanently recorded"):
                    targetRoom<0?L("前往炉火旅店的新档案室，按 E 公开星图",
                        "Visit the Hearth Inn's new archive and publish the chart"):
                    L("实地到访新支路里的营火、观景台或隐藏补给",
                        "Reach the new trail campsite, lookout or hidden supplies");
                q.reward=L("归档奖励：硬币 +60、碎片 +3、技能点 +1",
                    "Atlas reward: +60 coins, +3 shards, +1 talent point");
                return q;
            }

            if (id == "roadcharter")
            {
                q.title = L("第八章 · 共同行路", "Chapter Eight · Roads for Everyone");
                q.category = L("主线 · 战后公共道路", "STORY · CIVILIAN ROADS");
                q.description = L(
                    "公开听证之后，真正的公共道路必须从居民走过的路径里重新绘制。带回密林、遗迹、矿场三份东部支路证据，再与莉雅决定未来的交通优先级。",
                    "After the public hearing, public roads must be charted from paths residents actually use. Survey three eastern branches and bring the field evidence to Lyra for a lasting civic choice.");
                q.locked = !RoadCharterReady;
                q.complete = RoadCharterComplete;
                q.steps.Add(new QuestStep(L("与莉雅开启道路实地测绘", "Begin Lyra's road survey"), RoadCharterStarted));
                for (int region = 1; region <= 3; region++)
                    q.steps.Add(new QuestStep(L(new[] { "", "核对密林东部路标", "核对沉没遗迹东部路标", "核对矿场东部路标" }[region],
                        new[] { "", "Verify Thornwood's eastern route", "Verify the Ruins' eastern route", "Verify Ironreach's eastern route" }[region]),
                        Has("roadcharter_report_" + region)));
                q.steps.Add(new QuestStep(L("公会公布真实道路公约", "Enact the guild's public road charter"),
                    RoadCharterComplete));
                int nextRegion = -1;
                if (RoadCharterStarted)
                    for (int region = 1; region <= 3; region++)
                        if (!Has("roadcharter_report_" + region)) { nextRegion = region; break; }
                q.room = nextRegion < 0 ? 0 : nextRegion;
                q.target = nextRegion < 0 ? ObjectPoint(0, "guild", new Point(18, 17)) :
                    ObjectPoint(nextRegion, "waytrail", new Point(47.5f, 23.5f));
                // Multiple branch objects share "waytrail"; select the east marker by ID.
                if (nextRegion > 0)
                    foreach (WorldObject marker in data.maps[nextRegion].objects)
                        if (marker.id == "waytrail" + nextRegion + "e")
                            { q.target = new Point(marker.x, marker.y); break; }
                q.next = q.locked ? L("先完成第七章听证和厄洛的民用责任协议",
                    "Finish Chapter Seven and Orrin's civilian responsibility pledge") :
                    q.complete ? L("旧路公约公开；所有原有结局和地区选择保留",
                        "Road charter published; previous endings and regional choices remain") :
                    !RoadCharterStarted ? L("返回余烬镇，与公会莉雅谈第八章",
                        "Speak to Lyra in Emberfall about Chapter Eight") :
                    nextRegion > 0 ? L("实地走到东部新支路，阅读原始测绘石碑",
                        "Explore the eastern annex and read the old survey marker") :
                        L("三份实地证据齐备；返回公会与莉雅签署公约",
                            "All three reports collected; return to Lyra to enact the charter");
                q.reward = L("完成获得45硬币、3碎片、60经验，并选择永久步速或耐力恢复",
                    "45 coins, 3 shards, 60 XP and a permanent walking-speed or stamina-regeneration benefit");
                return q;
            }

            if (id == "waytrails")
            {
                q.title = L("巡路队的十六道回声", "Sixteen Echoes of the Roadwardens");
                q.category = L("探索地图 · 旧路", "EXPLORATION · OLD ROADS");
                q.description = L(
                    "每片室外区域有南部与东部两处旧路石碑。寻找路标、读懂留下的证词；所有发现会保存在原有的旅行记录中。",
                    "Every outdoor region hides one southern trace and one eastern lookout. Discover the roadwardens' testimony; each survey is kept in your existing travel record.");
                q.complete = Has("waytrail_complete");
                q.locked = !Has("intro");
                int inner = 0, outer = 0, destination = -1;
                WorldObject nextMarker = null;
                for (int room = 0; room < 8; room++)
                {
                    string suffix = "";
                    for (int branch = 0; branch < 2; branch++)
                    {
                        string mark = "waytrail" + room + (branch == 0 ? "s" : "e");
                        if (Has("waytrail_" + mark))
                        {
                            if (room < 4) inner++; else outer++;
                        }
                        else if (destination < 0 && CanEnter(room))
                        {
                            destination = room;
                            foreach (WorldObject o in data.maps[room].objects)
                                if (o.id == mark) { nextMarker = o; break; }
                        }
                    }
                }
                q.steps.Add(new QuestStep(L("近境路标 ", "Inner routes ") + inner + "/8", inner == 8));
                q.steps.Add(new QuestStep(L("远岸路标 ", "Farshore routes ") + outer + "/8", outer == 8));
                q.steps.Add(new QuestStep(L("修复十六道坐标", "Restore all sixteen bearings"), q.complete));
                q.room = destination < 0 ? 0 : destination;
                q.target = nextMarker == null ? new Point(14.5f, 31.5f)
                    : new Point(nextMarker.x, nextMarker.y);
                q.next = q.complete ? L("十六道旧路均已记录；可返回任何石碑重读",
                    "All sixteen routes charted; revisit any marker to reread") :
                    destination < 0 ? L("完成前置地区的调查，解锁余下路线",
                        "Complete earlier regional stories to unlock the remaining routes") :
                    L("前往新支路，调查尚未记录的旧路石碑",
                        "Explore a new branch and read its uncharted waypoint");
                q.reward = L("每处首次发现：8硬币、1碎片；全16处再得1技能点",
                    "Each first discovery: 8 coins, 1 shard; all 16: +1 talent point");
                return q;
            }

            if (id == "relief_rescue")
            {
                q.title = L("远岸居民撤离", "Farshore Civilian Rescue");
                q.category = L("限时战斗救援", "TIMED RESCUE COMBAT");
                q.locked = !Has("farshore_relief_5") && !Has("farshore_relief_6") &&
                    !Has("farshore_relief_7");
                q.description = L("从建成的民用避难站出发，在120秒内抵达巡测点、击退两波敌人并返回站台。战斗敌人不计主线击杀或普通战利品，失败可免费重试。",
                    "From a rebuilt shelter, reach the surveyed landmark, defeat two enemy waves and return in 120 seconds. No ordinary loot or story kill farming; retries cost nothing.");
                int next = 0;
                for (int region = 5; region <= 7; region++)
                {
                    bool done = Has("relief_rescue_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "救出雾港滩涂居民", "救出沙海行路者", "救出霜钟雪道旅人" }[region - 5],
                        new[] { "Evacuate Mistport travelers", "Rescue desert wanderers", "Escort Frostchime travelers" }[region - 5]), done));
                    if (!done && next == 0 && Has("farshore_relief_" + region)) next = region;
                }
                q.complete = Has("relief_rescue_complete");
                q.steps.Add(new QuestStep(L("全三地居民安全抵达", "Bring all civilians home"), q.complete));
                q.room = reliefMissionRoom != 0 ? reliefMissionRoom : next == 0 ? 5 : next;
                q.target = reliefMissionRoom != 0 && reliefMissionWave <= 2 ?
                    FieldSurveyTarget(reliefMissionRoom) :
                    reliefMissionRoom != 0 && reliefMissionWave == 3 &&
                        (reliefFollowerWaiting || ReliefEscortRouteStalled ||
                         ReliefEscortGap > 6.5f) ?
                    reliefFollowerPosition :
                    ObjectPoint(q.room, "fieldstation" + q.room, new Point(12, 12));
                // Keep the journal, HUD and compass on ONE authoritative
                // tactical destination. The rescue-only cover detour remains
                // temporary; do not modify trackedQuest or the return-stage
                // completion criteria.
                Point? urgentCover=reliefMissionRoom!=0 &&
                    reliefMissionWave==3 ?
                    CriticalEscortShelterTarget():null;
                if(urgentCover.HasValue)q.target=urgentCover.Value;
                q.next = q.complete ? L("三地居民已获救，救援可无奖励重演", "Three regions safe; replay with no rewards") :
                    reliefMissionRoom != 0 ? L(reliefMissionWave == 0 ? "先到巡测地标找到失联居民" :
                            reliefMissionWave == 3 && reliefFollowerWaiting ? "回到居民身旁，按E继续护送" :
                            reliefMissionWave == 3 && ReliefEscortRouteStalled ? "居民被地形阻挡！返回寻找开放的通路" :
                            reliefMissionWave == 3 && ReliefEscortGap > 6.5f ? "不要走得太远，等待幸存者赶上" :
                            reliefMissionWave == 3 ? "带领居民返回气象站" : "击退巡测地点的袭击者",
                        reliefMissionWave == 0 ? "Reach the survey marker to find civilians" :
                            reliefMissionWave == 3 && reliefFollowerWaiting ? "Return to the civilian and press E to resume following" :
                            reliefMissionWave == 3 && ReliefEscortRouteStalled ? "Civilian path blocked: find an open approach" :
                            reliefMissionWave == 3 && ReliefEscortGap > 6.5f ? "Wait for your survivor to catch up" :
                            reliefMissionWave == 3 ? "Return survivors to the weather station" : "Defeat raiders near the landmark") +
                        " · " + (int)Math.Ceiling(reliefMissionTime) + "s" :
                    next == 0 ? L("先建设一处远岸避难站", "Build a farshore civilian shelter") :
                        L("前往已建成的站台接取居民救援", "Start a rescue at a finished shelter");
                if(urgentCover.HasValue)
                    q.next=L("居民安全度危急！先带他们进入附近的可达避难所",
                        "Civilian safety critical! Escort them to reachable shelter first")+
                        " · "+(int)Math.Ceiling(reliefMissionTime)+"s";
                q.reward = L("每地首次45硬币、3碎片、50经验；三地额外技能点1、凉茶2",
                    "First clear each: 45 coins, 3 shards, 50 XP; all three: one talent and two teas");
                return q;
            }

            if (id == "farshore_relief")
            {
                q.title = L("远岸平民避难网", "Civilian Farshore Refuges");
                q.category = L("灾后重建", "CIVILIAN RELIEF");
                q.locked = save.frontierEnding == 0;
                q.description = L("三方协定已经签署。先修复远岸观测站，完成当地90秒环境巡测，随后投入物资建设浮桥、遮阳道和防寒补给棚。工程将永久延长低潮或扩大气候庇护范围。",
                    "After the council covenant, repair regional stations and complete their 90-second field surveys. Then supply rescue pontoons, a shaded lane and a warming depot for lasting tide and climate protection.");
                int next = 0;
                for (int region = 5; region <= 7; region++)
                {
                    bool complete = Has("farshore_relief_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "建设雾港救援浮桥", "建设沙海遮阳道", "建设高原防寒补给棚" }[region - 5],
                        new[] { "Build Mistport rescue pontoons", "Build the desert shaded lane", "Build Frostchime's warming depot" }[region - 5]), complete));
                    if (!complete && next == 0) next = region;
                }
                q.complete = Has("farshore_relief_complete");
                q.steps.Add(new QuestStep(L("贯通三地平民避难网", "Connect all civilian shelters"), q.complete));
                q.room = next == 0 ? 5 : next;
                q.target = ObjectPoint(q.room, "fieldstation" + q.room, new Point(10, 14));
                q.next = q.complete ? L("三地站台可缓慢疗伤，并通过炉火旅店星图桌安全往返", "Shelters heal outside combat and link safely to the Hearth Inn chart") :
                    !Has("fieldstation_" + q.room) ? L("先维修本地气象站", "Repair this region's weather station first") :
                    !Has("fieldstation_survey_" + q.room) ? L("先完成本站90秒环境巡测", "Complete this station's 90-second survey first") :
                    !CanEnter(q.room) ? L("先完成前一地区主线", "Complete the preceding region first") :
                    L("带物资到安全的站台建设平民避难通道", "Bring supplies to the safe station and build the civilian refuge");
                q.reward = L("每处30硬币、2碎片、35经验；三处另得技能点1、凉茶2，并永久增强安全区",
                    "Each: 30 coins, 2 shards, 35 XP. All three: 1 talent, 2 teas and permanent stronger refuges");
                return q;
            }

            if (id == "archive_echo")
            {
                q.title = L("测绘师的回声", "The Surveyor's Echo");
                q.category = L("精英战斗", "ELITE COMBAT");
                q.locked = !Has("archive_complete");
                q.description = L("星图指向矿场的旧方位校准台。与回声守卫交战时会出现三条紫色范围预警，留下一条空白方位。观察预警闪避，守卫虚弱后注意延迟中心爆发。",
                    "The completed atlas reveals an echo guardian in Rediron. Three purple area tells leave one clear cardinal exit. Dodge toward it; at low health the center also erupts after a delay.");
                q.steps.Add(new QuestStep(L("修复失落测绘师的星图", "Restore the Lost Surveyor's Atlas"),
                    Has("archive_complete")));
                q.complete = Has("archive_echo_complete");
                q.steps.Add(new QuestStep(L("击败回声守卫", "Defeat the echo guardian"), q.complete));
                q.room = 3; q.target = ObjectPoint(3, "archiveecho", new Point(23, 11));
                q.next = q.locked ? L("先完成琳的星图委托", "Complete Lin's atlas commission first") :
                    q.complete ? L("回声已平息，矿场方位重新校准", "Echo silenced and the compass is aligned") :
                    atlasEchoRoom == 3 ? L("观察紫圈，前往绿色空方位再反击", "Read violet strikes, move toward the green bearing, then counterattack") :
                    L("前往赤铁矿场的旧校准台", "Visit the calibrator in Rediron Mine");
                q.reward = L("首次胜利85硬币、5碎片、90经验；不可重复领取",
                    "First clear: 85 coins, 5 shards and 90 XP; cannot repeat");
                return q;
            }



            if(id=="hearing")
            {
                q.title=L("第七章 · 众人的晨钟","Chapter Seven · The People's Dawn");
                q.category=L("战后主线 · 公开听证与独立公民誓言",
                    "POST-FINALE MAIN STORY · PUBLIC HEARING");
                q.locked=!HearingReady;
                q.complete=HearingComplete;
                q.description=L("莉雅准备将城外三地的照明与补给写进公开协议。莫芽要修复林根，阿菱怕涨潮时又有人被拦在渡口，维恩坚持让工人自己保管停机钥匙。去密林、遗迹和矿场听他们亲口说，再回余烬镇决定医疗灯和应急供能该先照顾谁。",
                    "Lyra is drawing up a public agreement for the roads beyond Emberfall. Moya needs the forest roots tended, Arin fears another flood at the crossing, and Venn insists the workers keep the shutdown key. Hear each of them in person, then return to decide whether the town lamps serve the medics or the next evacuation.");
                q.steps.Add(new QuestStep(L("接受余烬镇公开听证","Open the Emberfall public hearing"),
                    Has("hearing_started")));
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(
                        L(new[]{"由莫芽决定林道的未来","由阿菱决定渡口的未来","由维恩决定矿机的未来"}[i-1],
                          new[]{"Moya chooses the grove's future","Arin chooses the public crossing","Venn chooses workers' safety"}[i-1]),
                        HearingVoice(i)!=0));
                q.steps.Add(new QuestStep(L("公布公民公约并启用永久设施",
                    "Publish a civic verdict and bring public services online"),
                    HearingComplete));
                q.room=0;q.target=ObjectPoint(0,"hearingdais",new Point(10.5f,7.5f));
                q.next=HearingComplete?
                    L("第七章完成：三地誓言和公民公约已经生效","Chapter Seven complete: regional oaths and civic verdict are live"):
                    !HearingReady?
                    L("先完成第六章并在堡垒与厄洛签署民用余量协议",
                        "Finish Chapter Six and Orrin's civilian reserve accord"):
                    !Has("hearing_started")?
                    L("去余烬镇东北方公开听证台接取主线",
                        "Start the public hearing at the Emberfall dais"):
                    HearingSignatures==3?
                    L("返回余烬镇，公布医疗开放或公用储备公约",
                        "Return to choose open access or emergency reserve"):
                    L("亲自访问未签字的地区居民","Visit an unsigned regional resident in person");
                if(Has("hearing_started") && HearingSignatures<3)
                {
                    int next=0;
                    if(save.room>=1&&save.room<=3 && HearingVoice(save.room)==0)
                        next=save.room;
                    else for(int i=1;i<=3;i++)
                        if(HearingVoice(i)==0){next=i;break;}
                    if(next!=0)
                    {
                        q.room=next;
                        q.target=ObjectPoint(next,"witnessnpc"+next,new Point(12,14));
                    }
                }
                q.reward=L("85硬币、2碎片、100经验；各地区永久服务与镇内医疗或应急耐力站",
                    "85 coins, 2 shards, 100 XP; real regional services and a civic health or energy lantern");
                return q;
            }

            if(id=="storm_commons")
            {
                q.title=L("第六章 · 公约第一夜","Chapter Six · First Night of the Commons");
                q.category=L("战后主线 · 星雨与公共能源分配",
                    "POST-FINALE STORY · PUBLIC GRID DURING THE STARSTORM");
                q.locked=!StormReady;
                q.complete=Has("storm_complete");
                q.description=L("战后第一场星雨迫近，八单位公共储备必须供三个地区分配。庇护优先3单位，基础供能2单位，实际使用木材、月光草、星晶和零件。没稳定的地区会受到可闪避的星雨冲击。三地修复后回镇，依你原先的结局顺序亲自走到三座分散的星灯处完成95秒调谐，再回议事台公布公约。各地区的选择会永久影响疗养、耐力与工房制作成本。",
                    "A real starstorm strikes the restored frontier. Allocate eight civic reserve units among three living districts: shelter-first uses three, essential utilities use two and each consumes actual repair materials. Unstable regions are struck by dodgeable arcs. After every region is stabilized, physically tune three dispersed town lamps in the order determined by your original ending, within 95 seconds. Finish at the council to publish the charter. Shelter, stamina and crafting costs change permanently according to your public decisions.");
                q.steps.Add(new QuestStep(
                    L("在公约议事台正式迎接第一场星雨",
                        "Begin the first starstorm at the public council"),
                    Has("storm_started")));
                for(int i=1;i<=3;i++)
                {
                    q.steps.Add(new QuestStep(L(
                        new[]{"密林 · 为受伤根系与行路者分配电力","遗迹 · 决定渡口与避难人群的供能","矿场 · 建立公开的安全工房配额"}[i-1],
                        new[]{"Thornwood · roots and roads reserve","Ruins · crossing and emergency reserve","Ironreach · safe working reserves"}[i-1]),
                        StormPolicy(i)!=0));
                }
                q.steps.Add(new QuestStep(L("在城镇依次激活三处公共星灯",
                    "Physically tune three dispersed town public lamps"),Has("storm_tuned")));
                q.steps.Add(new QuestStep(L("返回议事台公开真正经受星雨检验的公约",
                    "Return to publish the storm-tested public charter"),q.complete));
                int target=0;
                if(!Has("storm_started") || q.complete || StormAllocations>=3)
                    target=0;
                else if(save.room>=1&&save.room<=3 && StormPolicy(save.room)==0)
                    target=save.room;
                else for(int i=1;i<=3;i++)
                    if(StormPolicy(i)==0){target=i;break;}
                q.room=target;
                if(q.complete || !Has("storm_started") || StormAllocations<3 ||
                    Has("storm_tuned") || !stormTuning)
                    q.target=target==0?StormPoint(0,"stormcouncil"):
                        StormPoint(target,"stormrelay"+target);
                else
                    q.target=StormPoint(0,"stormnode"+StormNextNode);
                q.next=q.complete?L("六章主线结束 · 公开星雨配电公约已经生效",
                    "Chapter Six complete · The first-storm charter is in effect"):
                    q.locked?L("先完成第五章的公共设施与居民维护承诺",
                    "First complete Chapter Five, public facilities and resident stewardship"):
                    !Has("storm_started")?L("在余烬镇公共议事台召开星雨会议",
                    "Begin the starstorm council at Emberfall"):
                    StormAllocations<3?L("前往未稳定地区，分配8单位公共储备中的真实额度",
                    "Visit an unstable region and allocate part of the real eight-unit civic reserve"):
                    Has("storm_tuned")?L("三座镇内星灯已调谐，返回议事台公布公约",
                    "All three town lamps are tuned; return and publish the civic charter"):
                    stormTuning?L("限时依序操作下一座镇内星灯："+StormOrder(false),
                    "Physically tune the next lamp in order: "+StormOrder(true)):
                    L("回议事台启动95秒镇内公共星灯调谐",
                    "Return to the council and begin 95-second civic lamp tuning");
                q.reward=L("星雨后的三地永久设施升级；完成后110硬币、3碎片、110经验、1技能点",
                    "Three permanent post-storm public services; 110 coins, 3 shards, 110 XP and 1 skill point");
                return q;
            }

            if(id=="afterlight")
            {
                q.title=L("第五章 · 余光重建","Chapter Five · Afterlight Reconstruction");
                q.category=L("战后主线 · 三地公共设施","POST-FINALE MAIN STORY · THREE PUBLIC SITES");
                q.locked=save.ending==0;
                q.complete=Has("afterlight_complete");
                q.description=L("星核结局之后，在三地区校准锚点并决定公共用途。可以免费用90秒运输负载，或付1枚星晶购买120秒稳态负载，后者能承受更多真实战斗冲击。必须亲自携带完整载荷前往接收端，支付真实安装材料，三地建设完毕后返回公会公布公约。",
                    "After the original ending, choose local civic designs and calibrate three regional anchors. Carry a free 90-second load, or spend a real starcrystal on a 120-second stabilized load that better resists combat hits. Physically deliver intact power to the original receiver and spend real installation materials before publishing the public charter.");
                q.steps.Add(new QuestStep(L("到余烬镇公会加入重建议事","Join the Emberfall reconstruction council"),Has("afterlight_started")));
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(
                        L(new[]{"密林 · 恢复活根与安全林道","遗迹 · 让渡口属于活着的人","矿场 · 将安全停机写入机器"}[i-1],
                          new[]{"Thornwood · living roots and safe roads","Ruins · public crossing and water safety","Ironreach · workers' control of machinery"}[i-1]),
                        Has("afterlight_done_"+i)));
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(
                        L(new[]{"请莫芽签署苗圃或林灯维护责任","请阿菱签署公共渡口维护责任","请维恩签署安全工房维护责任"}[i-1],
                          new[]{"Moya signs root or lantern stewardship","Arin signs crossing stewardship","Venn signs foundry stewardship"}[i-1]),
                        Has("afterlight_steward_"+i)));
                q.steps.Add(new QuestStep(L("返回公会，发布三地重建公约","Publish the three-region civic charter"),q.complete));
                int next=0;
                if(AfterlightChargeRegion!=0)next=AfterlightChargeRegion;
                else if(save.room>=1&&save.room<=3 && Has("afterlight_started") &&
                    (!Has("afterlight_done_"+save.room)||!Has("afterlight_steward_"+save.room)))next=save.room;
                else for(int i=1;i<=3;i++)
                    if(!Has("afterlight_done_"+i)||!Has("afterlight_steward_"+i)){next=i;break;}
                q.room=!Has("afterlight_started")||q.complete||next==0?0:next;
                string kind=q.room==0?"afterlightcouncil":
                    AfterlightChargeRegion==next?"afterlightoutlet"+next:
                    Has("afterlight_done_"+next) && !Has("afterlight_steward_"+next)?"witnessnpc"+next:
                    "afterlightanchor"+next;
                q.target=ObjectPoint(q.room,kind,new Point(16,18));
                q.next=q.complete?L("第五章完成 · 重建公约已公开","Chapter Five complete · Charter published"):
                    q.locked?L("先完成厄洛的星核主线结局","Finish the original starcore ending first"):
                    !Has("afterlight_started")?L("去公会重建议事台接受战后任务","Accept the reconstruction mission at the guild council"):
                    AfterlightChargeRegion!=0?L(
                        "送往远端接收台：剩余"+(int)Math.Ceiling(AfterlightChargeLeft)+
                        "秒 · 载荷"+(int)Math.Ceiling(AfterlightChargeIntegrity)+"%"+
                        (AfterlightChargeIntegrity<35?" · 先在接收台使用月光草修复至35%":""),
                        "Carry to distant receiver: "+(int)Math.Ceiling(AfterlightChargeLeft)+
                        "s · load integrity "+(int)Math.Ceiling(AfterlightChargeIntegrity)+"%"+
                        (AfterlightChargeIntegrity<35?" · repair to 35% at receiver with Moonherb":"")):
                    next==0?L("回公会正式发布三地公约","Publish the public charter back at the guild"):
                    Has("afterlight_done_"+next) && !Has("afterlight_steward_"+next)?
                        L("回到当地证人身边，亲自签署设施维护责任","Return to the living witness to sign regional stewardship"):
                    AfterlightMode(next)==0?L("在当地旧锚点选择不可反悔的公共用途","Choose a lasting civic purpose at the regional anchor"):
                    L("到当地锚点启动负载传输","Start the physical transfer at the local anchor");
                q.reward=L("各地区真实恢复设施功能；章节完结120硬币、4碎片、100经验、1技能点",
                    "Three functional public sites; final reward 120 coins, 4 shards, 100 XP and one skill point");
                return q;
            }

            if(id=="blackout")
            {
                q.title=L("第四章关键事件 · 长夜撤离","Chapter Four · The Night the Shield Failed");
                q.category=L("主线抉择 · 真实居民护送","MAIN STORY · CIVILIAN EVACUATION");
                q.locked=!Has("triad_complete");
                q.complete=Has("blackout_complete");
                q.room=4;
                q.description=L("三枚星核把堡垒供能压成一条不可中断的屏障，备用照明故障。亲自在180秒内找到莱娜、伊恩、奥姆，一次护送一人走回公共避难所，避开真实电弧与沿途敌人。完成后，若三位旧章节证人和镇民盟友都支持，你可以凭实际救援方案说服厄洛停战。旧档原有 Boss 战和协定对话仍开放。",
                    "The single shield feed has blacked out the citadel's civilian lights. Physically escort Lena, Ian and Orme one by one to public shelter within 180 seconds, dodging real arcs and existing enemies. With three signed witnesses and three town allies, your rescue can support a local ceasefire. Original boss and treaty routes remain available.");
                q.steps.Add(new QuestStep(L("完成三核审计并到堡垒公共避难所","Complete audit and reach the citadel shelter"),
                    Has("triad_complete") && Has("blackout_started")));
                for(int i=1;i<=3;i++)q.steps.Add(new QuestStep(
                    L(new[]{"安全带回莱娜","安全带回伊恩","安全带回奥姆"}[i-1],
                      new[]{"Escort Lena home","Escort Ian home","Escort Orme home"}[i-1]),
                    q.complete || BlackoutActive && (blackoutRescued & (1<<(i-1)))!=0));
                q.steps.Add(new QuestStep(L("公布避难名单","Publish evacuation roster"),q.complete));
                q.target=BlackoutPoint("blackoutshelter");
                bool civilianBehind=BlackoutActive&&blackoutFollowing!=0&&
                    (blackoutFollowerPosition-Position).Length>3f;
                if(BlackoutActive && blackoutFollowing!=0)
                    q.target=civilianBehind?blackoutFollowerPosition:
                        BlackoutPoint("blackoutshelter");
                else if(BlackoutActive)
                {
                    for(int i=1;i<=3;i++)
                        if((blackoutRescued&(1<<(i-1)))==0)
                        {q.target=BlackoutPoint("blackoutcivilian"+i);break;}
                    if(blackoutRescued==7)q.target=BlackoutPoint("blackoutshelter");
                }
                q.next=q.complete?L("救援成功；居民名单可成为民用停战依据","Rescue complete; the roster can support civilian ceasefire"):
                    q.locked?L("先完成并公开莉雅的第四章三核审计","First complete and publish Lyra's Chapter Four audit"):
                    BlackoutActive && blackoutFollowing!=0?
                        (civilianBehind?
                            L("居民落在后面！沿指引回头接应，再一起前往避难所",
                              "Your civilian is behind! Follow the marker back before returning to shelter"):
                            (blackoutFollowerWard>0?
                                L("护送居民前往避难所 · 防护灯剩余1次",
                                  "Escort to shelter · civilian arc ward ready"):
                                L("把正在跟随的居民亲自送到公共避难所",
                                  "Escort your following civilian to public shelter"))):
                    BlackoutActive && blackoutRescued==7?
                        L("返回避难所公布三名居民名单","Return to publish the roster at the shelter"):
                    BlackoutActive?L("找到下一名居民，按E让其跟随","Find the next citizen and press E to escort"):
                    L("在堡垒西南侧公共避难所启动长夜救援","Begin the evacuation at the citadel's southwest shelter");
                q.reward=L("80硬币、3碎片、90经验；可获得民用停战剧情路线",
                    "80 coins, 3 shards, 90 XP; potential civilian ceasefire story route");
                return q;
            }

            if (id == "campaign_witness")
            {
                q.title=L("主线证词 · 未被计入的人","Main Story · The Uncounted Witnesses");
                q.category=L("前三章关键人物与实战","ACT I–III · LIVING TESTIMONY");
                q.description=L("三位居民亲自见证旧星核命令的损失。密林的疗养根系、遗迹的公共渡口与矿场的过热停机记录都被当成损耗。每章先正确恢复现场记录，再保护另一处证据免受真实守卫封锁，返回证人签署。这些证词会实质帮助第四章的三核审计。旧章节与旧存档的原有进度仍有效。",
                    "Three living witnesses document what obsolete starcore orders erased: healing roots, public sluices and miners' emergency shutdown. Restore a first field record, physically defeat security at the second site, then return to its witness for a signed account. All three make Chapter Four's audit fight easier. Original campaign gates remain intact.");
                int next=0;
                for(int i=1;i<=3;i++)
                {
                    bool complete=Has("witness_complete_"+i);
                    q.steps.Add(new QuestStep(L(new[]{"莫芽 · 密林里的根","阿菱 · 沉没遗迹的渡口","维恩 · 矿场的停机"}[i-1],
                        new[]{"Moya · injured grove roots","Arin · a public river crossing","Venn · workers' right to stop"}[i-1]),complete));
                    if(!complete && next==0)next=i;
                }
                q.complete=next==0;
                q.locked=!Has("scout_saved");
                // The chapter actively accepted by the player takes priority
                // over earlier unfiled accounts. Never navigate a new witness
                // backward to a quest the player has not begun yet.
                if(!q.complete)
                {
                    int here=save.room;
                    if(here>=1 && here<=3 && !Has("witness_complete_"+here) &&
                        Has("witness_started_"+here))next=here;
                    else
                        for(int i=3;i>=1;i--)
                            if(Has("witness_started_"+i) &&
                                !Has("witness_complete_"+i))
                            {next=i;break;}
                }
                q.room=q.complete?0:next;
                string kind="witnessnpc"+next;
                if(!q.complete && Has("witness_started_"+next))
                    kind=Has("witness_sealed_"+next)?"witnessnpc"+next:
                        !Has("witness_read_"+next)?"witnesssite1"+next:
                        "witnesssite2"+next;
                q.target=q.complete?ObjectPoint(0,"guild",new Point(18,17)):
                    ObjectPoint(next,kind,new Point(11,12));
                q.next=q.complete?L("三位证人均已署名；第四章审计卫兵防卫减弱",
                        "All witnesses signed; Chapter Four's audit guardian is weakened"):
                    q.locked?L("先救出荆棘密林的失踪侦察员",
                        "First rescue the missing Thornwood scout"):
                    !WitnessEligible(next)?L("先开启下一章的原有故事道路与机关",
                        "First open the next chapter's original story gate"):
                    !Has("witness_started_"+next)?L("在本地区找到活着的证人",
                        "Find the living witness in this region"):
                    !Has("witness_read_"+next)?L("调查第一处原始记录并恢复它的用途",
                        "Read the first site and restore its true purpose"):
                    !Has("witness_sealed_"+next)?L("击退第二处现场封锁守卫并保全证据",
                        "Defeat the guards and preserve the second site"):
                    L("返回证人身边亲自签署记录",
                        "Return for the witness's signed testimony");
                q.reward=L("每章一次性硬币25/35/45、碎片1、经验25/30/35；三章可削弱第四章审计守卫",
                    "Per chapter: 25/35/45 coins, 1 shard, 25/30/35 XP; all three weaken Chapter Four audit security");
                return q;
            }

            if (id == "triad_reckoning")
            {
                q.title=L("主线第四章 · 三核审计","Chapter Four · Three-Core Reckoning");
                q.category=L("星陨主线 · 关键剧情事件","MAIN STORY · KEY EVENT");
                q.locked=!TriadEligible;
                q.description=L("莉雅要求查明三枚星核改供堡垒屏障前的用途。进入星陨堡垒核对三份实地记录，击败中央保密守卫，亲手保全审计，然后把真相带回公会。此事改变厄洛的最终战，但不会锁死旧档。",
                    "Lyra asks you to reconstruct the original uses of the three cores before the citadel rewired them. Recover three physical ledgers in the citadel, defeat actual audit security, preserve the log, then publish it with the guild. It affects Orrin, without locking older saves.");
                q.steps.Add(new QuestStep(L("到公会接受莉雅的第四章调查",
                    "Accept Lyra's Chapter Four investigation"),Has("triad_started")));
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(L(new[]{"森林再生记录","遗迹水位记录","矿工作业记录"}[i-1],
                        new[]{"Forest renewal","Ruins tide passages","Mine rest protocol"}[i-1]),
                        Has("triad_evidence_"+i)));
                q.steps.Add(new QuestStep(L("击败保密卫兵并保全日志",
                    "Defeat the guard and preserve the audit"),Has("triad_sealed")));
                q.complete=Has("triad_complete");
                q.steps.Add(new QuestStep(L("将审计结果公开交给莉雅",
                    "Publish the audit with Lyra"),q.complete));
                q.room=q.locked || !Has("triad_started") || Has("triad_sealed")?0:4;
                string kind=TriadEvidence<3?
                    !Has("triad_evidence_1")?"triadmemory1":
                    !Has("triad_evidence_2")?"triadmemory2":"triadmemory3":"triadnexus";
                q.target=ObjectPoint(q.room,q.room==0?"guild":kind,new Point(18,11));
                q.next=q.complete?L("公开的星核支出将改变厄洛的决战",
                    "The public ledger changes Orrin's confrontation"):
                    q.locked?L("先取回三枚原始星核并交给莉雅",
                        "First deliver three original starcores to Lyra"):
                    !Has("triad_started")?L("去余烬镇公会找莉雅接受调查",
                        "Visit Lyra at the Emberfall guild"):
                    TriadEvidence<3?L("到堡垒读取下一份原始供能账",
                        "Read the next original supply ledger in the citadel"):
                    !Has("triad_sealed")?L("挑战中央审计守卫，保全日志",
                        "Defeat central audit security and preserve the log"):
                    L("将保全的三核证据送回公会",
                        "Deliver the three-core evidence to the guild");
                q.reward=L("一次性55硬币、2碎片、65经验；堡垒统领生命降低15%",
                    "One-time 55 coins, 2 shards, 65 XP; Orrin's HP reduced 15%");
                return q;
            }

            if (id == "iron_temper")
            {
                q.title=L("余烬回火","Ember Temper");
                q.category=L("矿场高温管线 · 真实战斗与压力控制",
                    "FOUNDRY PRESSURE · LIVE COMBAT & VENTS");
                q.locked=!Has("boss_3");
                q.description=L("矿场的军用锻炉将矿工锁在永不停止的热压循环中。击退真实守卫，在110秒与95压力上限内打开西北及东侧两座泄压阀，躲避延迟蒸汽冲击，战胜重装监工，返回主炉公开回火配方。",
                    "The military furnace trapped miners in an endless pressure cycle. Clear real enemy waves, open both vents below 95 pressure in 110 seconds, dodge delayed steam bursts, defeat the armored overseer, then return to publish the temper recipe.");
                q.steps.Add(new QuestStep(L("排除锻炉首批守卫，打开西北阀",
                    "Defeat guards and release northwest vent"),
                    Has("iron_temper_complete") || TemperStage>=2));
                q.steps.Add(new QuestStep(L("击退增援，打开东侧泄压阀",
                    "Defeat reinforcements and open east vent"),
                    Has("iron_temper_complete") || TemperStage>=3));
                q.complete=Has("iron_temper_complete");
                q.steps.Add(new QuestStep(L("击败重装监工并向工房交付配方",
                    "Defeat armored overseer and publish the recipe"),q.complete));
                q.room=3;
                q.target=TemperPoint(TemperStage==1?"ironvent1":TemperStage==2?
                    "ironvent2":"ironfoundry");
                q.next=q.complete?L("公共回火技术已传给诺尔，强化材料永久减少",
                    "Noll now uses the cheaper public temper recipe"):
                    q.locked?L("先击败矿场原来的星核守卫",
                    "Defeat the original Ironreach starcore guardian first"):
                    !TemperActive?L("到赤铁矿场旧工房启动回火",
                    "Begin the operation at Ironreach's old foundry"):
                    TemperStage==3?L("击退重装监工，返回工房结束军用指令",
                    "Defeat the armored overseer; return to the foundry"):
                    L("击败守卫后操作下一处泄压阀，注意热压与范围预告",
                    "Clear guards, operate the next vent and dodge pressure warnings");
                q.reward=L("首次完成75硬币、3碎片、1天赋点、85经验；锻造升级永久减耗",
                    "First clear: 75 coins, 3 shards, 1 talent, 85 XP and permanent upgrade-material savings");
                return q;
            }

            if (id == "glasswind")
            {
                q.title=L("玻璃风脉","Glasswind Circuit");
                q.category=L("沙海争夺 · 限时守点","DESERT CONTROL · TIMED DEFENSE");
                q.locked=!Has("glasswind_started");
                q.description=L("沙海三座风压阀被旧货运网独占。靠近风圈完成八秒校准，击退两波守卫，再手动封存中继。",
                    "The old freight network controls three desert wind valves. Hold the capture ring for eight seconds, defeat two security waves, then manually seal each relay.");
                int next=0;
                for(int i=1;i<=3;i++)
                {
                    bool done=Has("glasswind_"+i);
                    q.steps.Add(new QuestStep(L("恢复第 "+i+" 座风脉","Restore wind relay "+i),done));
                    if(!done && next==0)next=i;
                }
                q.complete=Has("glasswind_complete");
                q.steps.Add(new QuestStep(L("向米娅公开风路图","Publish the wind chart with Mia"),q.complete));
                q.room=q.locked || Has("glasswind_all_sealed")?0:6;
                q.target=ObjectPoint(q.room,q.room==0?"glasswindkeeper":
                    "glasswindrelay"+(GlasswindSite>0?GlasswindSite:Math.Max(1,next)),new Point(18,12));
                q.next=q.complete?L("全部中继已变成公共避暑地","All relays provide public shade"):
                    q.locked?L("完成沙海调查后找测绘员米娅","Finish the desert report and find Mia"):
                    Has("glasswind_all_sealed")?L("返回余烬镇公开风路图","Publish the chart in Emberfall"):
                    GlasswindSite!=0?L("守住风圈击退两波敌人，再按E封存","Hold the ring, defeat both waves and press E to seal"):
                    L("前往沙海中继站开始守点","Start a desert relay defense");
                q.reward=L("每站15硬币、1碎片、20经验与避暑圈；最终65硬币、2碎片、1天赋点、70经验",
                    "Each: 15 coins, 1 shard, 20 XP and shade; final: 65 coins, 2 shards, 1 talent, 70 XP");
                return q;
            }
            if (id == "tide_salvage")
            {
                q.title=L("潮缚货单","Tidebound Manifest");
                q.category=L("潮汐探险 · 限时打捞","TIDE EXPEDITION · TIMED SALVAGE");
                q.locked=!Has("harbor_report");
                q.description=L("托兰要将被旧物流网注销的药品、粮食和航海图交还居民。利用低潮走到三处真实潮滩，依序收回货单，途中击退真实掠夺者，再在100秒内回到码头。",
                    "Tolan asks you to restore medicine, food and passage records erased by the freight network. Explore three real shoals at low tide in order, fight live raiders, and return to the dock within 100 seconds.");
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(L(new[]{"西侧药品货单","内湾粮食货单","南岛潮汐模型"}[i-1],
                        new[]{"West shoal medicine","Inner bay provisions","Southern tide model"}[i-1]),
                        Has("tide_salvage_complete") || TideSalvagePhase>i));
                q.complete=Has("tide_salvage_complete");
                q.steps.Add(new QuestStep(L("回码头交付公开货单","Return cargo to the dock"),q.complete));
                q.room=5;
                q.target=ObjectPoint(5,!TideSalvageActive || TideSalvagePhase==4?
                    "tidedock":"tidecargo"+TideSalvagePhase,new Point(10.5f,14.5f));
                q.next=q.complete?L("公共稳潮模型已安装，观测站的控潮延长15秒","Public tide model extends Tidewatch holds by 15 seconds"):
                    q.locked?L("先完成雾港事故调查并提交米拉","First finish and file Mistport's tide investigation"):
                    !TideSalvageActive?L("到渡船码头趁低潮发起打捞","Launch at the ferry dock during low tide"):
                    TideSalvagePhase==4?L("击退掠夺者后回码头交付全部货物","Defeat the raiders and return all cargo"):
                    L("前往下一处潮滩货箱，等待低潮后按E打捞","Reach the next crate; recover it at low tide");
                q.reward=L("一次性60硬币、3碎片、1天赋点、75经验；永久稳潮+15秒",
                    "One-time 60 coins, 3 shards, 1 talent, 75 XP and +15s Tidewatch holds");
                return q;
            }
            if (id == "skywatch")
            {
                q.title=L("霜钟夜空","Frostchime Skywatch");
                q.category=L("高原观测 · 星象谜题与精英战","HIGHLAND OBSERVATION · PUZZLE AND ELITE");
                q.locked=!Has("skywatch_started");
                q.description=L(
                    "观星师伊澜请你读取北方霜、东方光、南方炉火的三枚古棱镜，再停止仍将暴风雪当作敌袭的自动守卫，把真实天气预报公之于众。",
                    "Yilan asks you to calibrate the northern frost, eastern light and southern hearth prisms, defeat the obsolete skywatch guardian and publish an honest weather forecast.");
                for(int i=1;i<=3;i++)
                    q.steps.Add(new QuestStep(
                        L(new[]{"北方霜棱镜","东方光棱镜","南方炉火棱镜"}[i-1],
                            new[]{"North frost prism","East light prism","South hearth prism"}[i-1]),
                        Has("skywatch_prism_"+i)));
                q.steps.Add(new QuestStep(L("击败古观星守卫","Defeat the observatory guardian"),
                    Has("skywatch_defeated")));
                q.complete=Has("skywatch_complete");
                q.steps.Add(new QuestStep(L("返回余烬镇公开观测记录","Publish the forecast in Emberfall"),q.complete));
                int next=1;
                while(next<=3 && Has("skywatch_prism_"+next))next++;
                q.room=q.locked || Has("skywatch_defeated")?0:7;
                q.target=ObjectPoint(q.room,q.room==0?"skywatcher":
                    next<=3?"skyprism"+next:"skyobservatory",new Point(18,12));
                q.next=q.complete?L("观星台已对旅人开放避寒穹顶","The observatory now shelters travelers"):
                    q.locked?L("完成高原记录后在镇里寻找观星师伊澜","Finish the mountain report, then speak to Yilan"):
                    Has("skywatch_defeated")?L("回镇向伊澜交付真实光谱","Return the spectrum to Yilan"):
                    next<=3?L("前往下一座棱镜读取地貌线索","Read and tune the next prism"):
                    SkywatchRoom==7?L("观察冰光预警并击败守卫","Dodge the frost warnings and defeat the guardian"):
                    L("前往东南的古观星台启动守卫试炼","Activate the southeastern observatory");
                q.reward=L("首次归档：95硬币、3碎片、1天赋点、100经验；高原避寒穹顶",
                    "First clear: 95 coins, 3 shards, 1 talent, 100 XP, and a sheltered observatory");
                return q;
            }
            if (id == "skywatch_afterglow")
            {
                q.title=L("三枚余辉","Three Afterglows");
                q.category=L("战后调查 · 高原避寒网络",
                    "AFTERMATH · HIGHLAND REFUGE NETWORK");
                q.locked=!Has("skywatch_complete");
                q.description=L(
                    "观星师伊澜公开了气候光谱，但北、东、南三座棱镜仍保存最后的旅人记录。回收后在观星台归档，为夜归者点亮三处避寒信标。",
                    "After Yilan publishes the spectrum, return to the north, east and south lenses to recover lost travelers' last records. Archive them at the observatory to activate warming beacons.");
                int next=1;
                for(int i=1;i<=3;i++)
                {
                    bool done=Has("skywatch_afterglow_"+i);
                    q.steps.Add(new QuestStep(
                        L(new[]{"北方余辉","东方余辉","南方余辉"}[i-1],
                            new[]{"Northern afterglow","Eastern afterglow","Southern afterglow"}[i-1]),done));
                    if(done && next==i)next++;
                }
                q.complete=Has("skywatch_afterglow_complete");
                q.steps.Add(new QuestStep(L("在古观星台公开最终余辉",
                    "Archive the last afterglows at the observatory"),q.complete));
                q.room=7;
                q.target=ObjectPoint(7,next<=3?"skyprism"+next:"skyobservatory",
                    new Point(25.5f,12.5f));
                q.next=q.locked?L("先完成霜钟夜空的公开观测委托",
                        "Publish Yilan's original skywatch spectrum first"):
                    q.complete?L("三处棱镜已成为永久避寒信标",
                        "All three lenses are now permanent warming refuges"):
                    next<=3?L("调查下一枚棱镜中的余辉",
                        "Recover the next lens afterglow"):
                        L("回东南古观星台归档三道余辉",
                          "Archive the three echoes at the southeastern observatory");
                q.reward=L("一次性：45硬币、2碎片、60经验；三处永久避寒地点",
                    "One time: 45 coins, 2 shards, 60 XP and three permanent warming sites");
                return q;
            }
            if (id == "commons_relay")
            {
                q.title = L("堡垒公共信标", "Citadel Commons Relay");
                q.category = L("后日谈 · 战斗重连", "AFTERMATH · COMBAT RESTORATION");
                q.locked = !Has("commons_relay_started");
                q.description = L(
                    "堡垒旧调度仍把粮路、医护与撤离灯线分作三套封闭权限。跟随证词保管员艾拉调查堡垒，亲自连通三座信标，打退自动防御，再将公开授权交回城镇。",
                    "The citadel still keeps food, medical care and evacuation lamps behind separate private permissions. Reconnect three real consoles, defeat their obsolete security, then return the public key to Ayla.");
                for (int i = 1; i <= 3; i++)
                    q.steps.Add(new QuestStep(
                        L(new[] { "粮路通道", "医护通道", "撤离灯线" }[i-1],
                          new[] { "Food routes", "Medical channel", "Evacuation lamps" }[i-1]),
                        Has("commons_relay_sealed") || commonsRelayRoom == 4 && commonsRelayStep > i));
                q.complete = Has("commons_relay_complete");
                q.steps.Add(new QuestStep(L("公开移交通道密钥", "Return the public access key"), q.complete));
                q.room = q.locked || Has("commons_relay_sealed") ? 0 : 4;
                int next = commonsRelayRoom == 4 ? Math.Min(3, commonsRelayStep) : 1;
                q.target = ObjectPoint(q.room, q.room == 0 ? "commonswitness" :
                    "commonsrelay" + next, new Point(18, 12));
                q.next = q.complete ? L("民用线路已经开放，守护所有人共享的归路",
                                          "Civilian circuits remain available to every traveler") :
                    q.locked ? L("击败堡垒守卫后，找镇里的艾拉查阅公共供给记录",
                                 "Defeat the citadel guardian, then talk to Ayla in town") :
                    Has("commons_relay_sealed") ?
                        L("回余烬镇向艾拉公开移交信标密钥",
                          "Return to Emberfall and publish the relay key with Ayla") :
                    commonsRelayRoom != 0 && enemies.Exists(IsCommonsRelayEnemy) ?
                        L("击退本段信标的自动防御守卫", "Defeat this circuit's active security") :
                    L("前往堡垒下一个信标，按 E 重新连通",
                      "Visit the next citadel relay and press E to reconnect it");
                q.reward = L("线路重连60硬币、3碎片、75经验；归档再获80硬币、1技能点、90经验，永久耐力恢复+2/秒",
                    "Reconnect: 60 coins, 3 shards and 75 XP; publication: 80 coins, 1 talent, 90 XP and +2 energy/sec permanently");
                return q;
            }
            if (id == "root_harmony")
            {
                q.title = L("星根失谐", "Starroot Counterpoint");
                q.category = L("调谐谜题 · 实战", "RESONANCE PUZZLE · COMBAT");
                q.locked = !Has("root_harmony_started");
                q.description = L("听纹师芮请你调谐密林、遗迹和矿场的三处星根共鸣石。阅读地貌线索选择对应音节，击败三名真实失谐守卫，再返回封存数据。挑战失败可重新尝试，完成奖励不可刷取。",
                    "Rui asks you to tune three starroot listening stones. Read the landscape clues, choose the correct note, defeat three dissonant sentinels, then seal the reading. Retries are free; rewards are once only.");
                for (int region = 1; region <= 3; region++)
                    q.steps.Add(new QuestStep(L(new[] { "密林的根音", "遗迹的潮音", "矿场的铁音" }[region-1],
                        new[] { "Root note in Thornwood", "Tide note in the Ruins", "Iron note in Ironreach" }[region-1]),
                        Has("root_harmony_" + region)));
                q.complete = Has("root_harmony_complete");
                q.steps.Add(new QuestStep(L("向芮归档三段和声", "Archive all three readings with Rui"), q.complete));
                int next = RootHarmonyNext();
                q.room = q.locked || next == 0 ? 0 : next;
                q.target = ObjectPoint(q.room,
                    next == 0 || q.locked ? "rootlistener" : "rootcounter" + next,
                    new Point(20, 12));
                q.next = q.complete ? L("三道和声已经归档", "The three harmonies are archived") :
                    q.locked ? L("救回密林侦察员后在余烬镇寻找听纹师芮", "Rescue the forest scout, then speak to Rui in Emberfall") :
                    next == 0 ? L("返回余烬镇，把三段读数交给芮", "Return to Emberfall and report to Rui") :
                    RootHarmonyRoom == next ?
                        enemies.Exists(IsRootHarmonyEnemy) ?
                            L("击退共鸣石附近的失谐守卫", "Defeat the sentinels guarding the node") :
                            L("靠近共鸣石按 E 封存读数", "Press E at the node to seal the reading") :
                        !CanEnter(next) ? L("先解锁下一章的地区", "Unlock the next campaign region") :
                            L("前往共鸣石，依据地貌选择音纹并守住阵地",
                              "Visit the stone, tune its landscape note and defend the site");
                q.reward = L("每区25硬币、2碎片、35经验与永久休整点；完成另得75硬币、1技能点、70经验",
                    "Each node: 25 coins, 2 shards, 35 XP and permanent safe rest; final: 75 coins, 1 talent, 70 XP");
                return q;
            }

            if (id == "archive_trail")
            {
                q.title = L("失落测绘师的星图", "The Lost Surveyor's Atlas");
                q.category = L("遗迹调查", "LOST CARTOGRAPHY");
                q.locked = !Has("archive_started");
                q.description = L("镇上的修复师琳发现三份被拆散的远征测绘记录。先在密林找残图，再按残图线索去沉没遗迹，最后回收矿场的测距仪，将记录交回镇上。",
                    "Lin needs three lost expedition records. Find the woodland chart, follow its clue into the ruins, then recover the mine rangefinder and return to town.");
                for (int i = 1; i <= 3; i++)
                    q.steps.Add(new QuestStep(L(new[] { "找回林地航图", "读取遗迹潮刻", "回收矿场测距仪" }[i - 1],
                        new[] { "Recover the woodland chart", "Read the ruins' tide inscription", "Retrieve the mine rangefinder" }[i - 1]), Has("archive_fragment_" + i)));
                q.complete = Has("archive_complete");
                q.steps.Add(new QuestStep(L("回到余烬镇交付完整星图", "Return the restored atlas to Lin"), q.complete));
                int next = !Has("archive_fragment_1") ? 1 : !Has("archive_fragment_2") ? 2 :
                    !Has("archive_fragment_3") ? 3 : 0;
                q.room = next; q.target = next == 0 ? ObjectPoint(0, "archivekeeper", new Point(14, 11)) :
                    ObjectPoint(next, "archivefragment" + next, new Point(18, 11));
                q.next = q.complete ? L("星图已归档，测绘师的故事终于完整", "Atlas archived; the surveyor's story is complete") :
                    q.locked ? L("与余烬镇的修复师琳交谈", "Speak with Lin in Emberfall") :
                    next == 0 ? L("回镇上把三份记录交给琳", "Deliver all three records to Lin") :
                    !CanEnter(next) ? L("先完成当前章节，解锁下一片地区", "Progress the story to unlock the next region") :
                    L("调查地图上的失落测绘物件", "Investigate the marked lost survey artifact");
                q.reward = L("每份记录20硬币、2碎片、20经验；归档再得技能点1、硬币60",
                    "Each artifact: 20 coins, 2 shards, 20 XP; completion: 1 talent point and 60 coins");
                return q;
            }

            if (id == "station_patrol")
            {
                q.title = L("远岸环境巡测", "Farshore Field Surveys");
                q.category = L("限时探索", "TIMED EXPLORATION");
                q.locked = !CanEnter(5) || (!Has("fieldstation_5") && !Has("fieldstation_6") && !Has("fieldstation_7"));
                q.description = L("在修好的观测站接下90秒巡测：跑到当地标记记录潮汐、热度或寒意，然后及时返回。失败无损失。",
                    "Start a 90-second survey at a restored station. Read the tide, heat or cold at the landmark, then return before time expires. Retries are free.");
                int next = 0;
                for (int region = 5; region <= 7; region++)
                {
                    bool done = Has("fieldstation_survey_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "雾港低潮巡测", "沙海热度巡测", "高原寒意巡测" }[region - 5],
                        new[] { "Harbor low-tide reading", "Desert heat reading", "Highland cold reading" }[region - 5]), done));
                    if (!done && next == 0 && Has("fieldstation_" + region)) next = region;
                }
                q.complete = Has("fieldstation_survey_complete");
                q.steps.Add(new QuestStep(L("归档三份巡测数据", "Archive all three readings"), q.complete));
                q.room = fieldSurveyRoom != 0 ? fieldSurveyRoom : next != 0 ? next : 5;
                q.target = fieldSurveyRoom != 0 && fieldSurveyPhase == 1 ? FieldSurveyTarget(fieldSurveyRoom) :
                    ObjectPoint(q.room, "fieldstation" + q.room, new Point(10, 14));
                q.next = q.complete ? L("三地巡测完成，观测报告已归档", "All field surveys archived") :
                    fieldSurveyRoom != 0 ?
                        L(fieldSurveyPhase == 1 ? "前往地图标记，读取气象信号" : "返回观测站交付读数",
                          fieldSurveyPhase == 1 ? "Reach the landmark to take a reading" : "Return to the station to deliver the reading") +
                        " · " + (int)Math.Ceiling(fieldSurveyTime) + "s" :
                    next != 0 ? L("前往已修复的站台，接取90秒巡测", "Visit a repaired station to start a 90s survey") :
                        L("先维修任意远岸观测站", "Restore a farshore station first");
                q.reward = L("每站24硬币、2碎片、30经验；三站额外凉茶2",
                    "Each: 24 coins, 2 shards, 30 XP; all three: +2 tea");
                return q;
            }

            if (id == "stations")
            {
                q.title = L("远岸风暴观测网", "The Farshore Stormwatch");
                q.category = L("环境探索", "ENVIRONMENTAL EXPLORATION");
                q.locked = !CanEnter(5);
                q.description = L("修复雾港潮汐站、沙海降温站与高原保暖站。利用各地的潮汐、镜面光路和火盆，建立安全的远岸补给路线。",
                    "Restore a tidewatch, cooling and warming station. Read the tides, route the desert mirrors and kindle a highland brazier.");
                int next = 0;
                for (int region = 5; region <= 7; region++)
                {
                    bool done = Has("fieldstation_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "启动雾港潮汐站", "启动沙海降温站", "启动高原保暖站" }[region - 5],
                        new[] { "Repair the harbor tidewatch", "Repair the desert cooler", "Repair the frost shelter" }[region - 5]), done));
                    if (!done && next == 0) next = region;
                }
                q.complete = Has("fieldstation_complete");
                q.steps.Add(new QuestStep(L("建立三地安全观测网", "Connect all three weather stations"), q.complete));
                q.room = next == 0 ? 5 : next;
                q.target = ObjectPoint(q.room, "fieldstation" + q.room, new Point(10, 14));
                q.next = q.locked ? L("推进主线，开启远岸雾港", "Advance the story and unlock Mistport") :
                    q.complete ? L("三地避险站已连通，可在站点之间快速旅行", "All three refuges linked for direct station-to-station travel") :
                    !CanEnter(q.room) ? L("先完成上一地区的调查报告", "Complete the previous regional report") :
                    q.room == 5 ? L("在低潮时维修，之后可随时固定45秒低潮", "Repair at low tide; then hold it for 45 seconds") :
                    q.room == 6 ? L("先接通三面镜子的光路，再维修降温站", "Connect the mirror light channel before repairing") :
                    L("点燃西侧火盆后维修保暖站", "Light the western brazier before repairing");
                q.reward = L("每站18硬币、2碎片、20经验；全修额外技能点1、凉茶2",
                    "Each: 18 coins, 2 shards, 20 XP; all three: +1 talent point, +2 tea");
                return q;
            }

            if (id == "wayfarers")
            {
                q.title = L("失联旅人的归途", "The Missing Travelers");
                q.category = L("旅途支线", "ROADSIDE STORY");
                q.locked = !Has("scout_saved");
                q.description = L("密林、遗迹、矿场的三名旅人需要援助。捐出两份材料，或保护旅人安全度并击退两名掠夺者。可站位拦截真正的攻击，靠近旅人花20耐力启动一次8秒掩护，或在受伤后消耗一份材料紧急援助。",
                    "Three travelers need help across Thornwood, the Ruins and the Mine. Donate two supplies, or defend against two real raiders. Physically intercept strikes, rally one eight-second cover beside the traveler for 20 energy, or use one supply for emergency aid after injury.");
                int next = 0;
                for (int region = 1; region <= 3; region++)
                {
                    bool done = Has("wayfarer_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "", "帮助林间药师", "帮助遗迹摆渡人", "帮助矿道修理匠" }[region],
                        new[] { "", "Help the woodland healer", "Help the ferryman", "Help the mine mechanic" }[region]), done));
                    if (!done && next == 0) next = region;
                }
                q.complete = Has("wayfarer_complete");
                q.steps.Add(new QuestStep(L("三名旅人重归旅途", "All three travelers back on the road"), q.complete));
                q.room = wayfarerEncounterRoom != 0 ? wayfarerEncounterRoom : next == 0 ? 1 : next;
                q.target = ObjectPoint(q.room, "wayfarer" + q.room, new Point(15, 14));
                q.next = q.locked ? L("先救回密林的公会侦察员", "Rescue the guild scout first") :
                    q.complete ? L("三名旅人已经平安出发", "All three travelers are safe") :
                    wayfarerEncounterRoom != 0 ? (WayfarerCoverTime>0 ?
                        L("掩护还剩 "+(int)Math.Ceiling(WayfarerCoverTime)+"秒 · 击退两名伏击者",
                          "Cover "+(int)Math.Ceiling(WayfarerCoverTime)+"s · defeat both raiders") :
                        WayfarerCoverAvailable ?
                            L("走近旅人架设一次减伤掩护，或站在前方拦截掠夺者",
                              "Rally cover beside the traveler or intercept raiders in the lane") :
                            L("保护旅人安全度，击退两名伏击者",
                              "Keep the traveler safe; defeat both raiders")) :
                    L("找旅人交谈：物资援助或战斗守护", "Talk to the traveler: donate supplies or defend");
                q.reward = L("每人20硬币、1碎片、25经验；全部完成额外技能点1、炖汤1",
                    "Each: 20 coins, 1 shard, 25 XP; all three: 1 talent point and 1 stew");
                return q;
            }

            if (id == "vigil")
            {
                q.title = L("巡路信标守望", "Roadwarden Vigil");
                q.category = L("战斗挑战", "COMBAT CHALLENGE");
                q.locked = !Has("dispatch_complete");
                q.description = L("巡路誓言不只是地图：在密林、遗迹与矿场保护信标护盾，阻止敌人靠近侵蚀，并守住两波袭击。",
                    "Protect each beacon ward from attackers who drain it up close; hold two waves in each region to keep the homeward road safe.");
                int next = 0;
                for (int region = 1; region <= 3; region++)
                {
                    bool done = Has("road_trial_" + region);
                    q.steps.Add(new QuestStep(L(new[] { "", "守住苔根信标", "守住潮痕信标", "守住矿灯信标" }[region],
                        new[] { "", "Defend Mossroot beacon", "Defend Tidemark beacon", "Defend Minelight beacon" }[region]), done));
                    if (!done && next == 0) next = region;
                }
                q.complete = Has("road_trial_complete");
                q.room = roadTrialRoom != 0 ? roadTrialRoom : next == 0 ? 9 : next;
                q.target = ObjectPoint(q.room, roadTrialRoom != 0 ? "survey" + roadTrialRoom :
                    next == 0 ? "surveyDesk" : "survey" + next, new Point(15, 14));
                q.next = q.locked ? L("先解读三封巡路旧信", "Decode the three roadwarden letters first") :
                    roadTrialRoom != 0 ? L("击退第 " + roadTrialWave + "/" + RoadwardenTrialTotalWaves + " 波敌人", "Defeat wave " + roadTrialWave + "/" + RoadwardenTrialTotalWaves) :
                    q.complete ? L("三座信标已守住；信标可重玩三波精英试炼", "All beacons defended; replay three-wave veteran challenges") :
                    L("在信标处开始双波守路试炼", "Begin the two-wave defense at the beacon");
                q.reward = L("每座35硬币、2碎片、40经验；全部完成额外技能点1、碎片3",
                    "Each: 35 coins, 2 shards, 40 XP; all three: bonus 1 talent point, 3 shards");
                return q;
            }

            if (id == "dispatches")
            {
                q.title = L("巡路队的未寄之信", "The Wardens' Unsent Letters");
                q.category = L("巡路探索", "ROADWARDEN DISCOVERY");
                q.locked = !Has("survey_complete");
                q.description = L("归乡星图指向密林、遗迹和矿场的三封未寄之信。重读旧信，解开隐藏的巡路誓言。",
                    "The homeward chart points to three unsent letters in Thornwood, the Drowned Ruins and Ironreach. Reread them and decode the wardens' promise.");
                int next = 0;
                for (int idLetter = 6; idLetter <= 8; idLetter++)
                {
                    bool done = Has("dispatch_" + idLetter);
                    q.steps.Add(new QuestStep(L(new[] { "解读密林旧信", "解读遗迹船票", "解读矿场图纸" }[idLetter - 6],
                        new[] { "Decode forest letter", "Decode ferry ticket", "Decode mine blueprint" }[idLetter - 6]), done));
                    if (!done && next == 0) next = idLetter;
                }
                q.complete = Has("dispatch_complete");
                q.steps.Add(new QuestStep(L("拼合巡路队的誓言", "Reassemble the wardens' promise"), q.complete));
                q.room = next == 0 ? 9 : next - 5;
                q.target = ObjectPoint(q.room, next == 0 ? "surveyDesk" : "history" + next, new Point(15, 14));
                q.next = q.locked ? L("先恢复三座信标并完成归乡星图", "Restore the beacons and complete the chart") :
                    q.complete ? L("巡路队的誓言已传承", "The wardens' promise lives on") :
                    L("重读旧信并解读暗号", "Reread the letter and decode its cipher");
                q.reward = L("每封20硬币、1碎片、25经验；全解额外40硬币、技能点1、热汤减价2",
                    "Each: 20 coins, 1 shard, 25 XP; all three: 40 coins, 1 talent point, stew -2 coins");
                return q;
            }
            if (id == "signals")
            {
                q.title = L("归乡的三段信号", "Three Signals Home"); q.category = L("巡路探索", "ROADWARDEN DISCOVERY");
                q.description = L("密林、沉没遗迹和矿场留下了三段失联的巡路信号。将它们带回炉火旅店，拼出一条真正属于旅人的安全归途。", "Recover three lost roadwarden signals across Thornwood, the Drowned Ruins and Ironreach. Assemble them at the Hearth Inn to chart a safe way home.");
                int next = 0;
                for (int i = 1; i <= 3; i++)
                {
                    bool done = Has("survey_" + i);
                    q.steps.Add(new QuestStep(L(new[] { "", "恢复密林信号", "恢复遗迹信号", "恢复矿场信号" }[i], new[] { "", "Restore forest signal", "Restore ruins signal", "Restore mine signal" }[i]), done));
                    if (!done && next == 0) next = i;
                }
                q.complete = Has("survey_complete"); q.steps.Add(new QuestStep(L("在炉火旅店拼合归乡星图", "Assemble the chart at the Hearth Inn"), q.complete));
                q.room = next == 0 ? 9 : next; q.target = ObjectPoint(q.room, next == 0 ? "surveyDesk" : "survey" + next, new Point(18, 14));
                q.next = q.complete ? L("在炉火旅店星图桌快速前往三座信标；靠近信标可返回旅店", "Use the Hearth Inn chart to travel to three beacons; return from any beacon") : next == 0 ? L("三段信号已齐，去炉火旅店的星图桌", "All signals found; visit the chart table in the Hearth Inn") : L("调查第 ", "Investigate signal ") + next + L(" 座旧信标", " at the old beacon");
                q.reward = L("每座12硬币/1碎片；完工60硬币、5碎片、技能点1、移速+0.25、信标往返", "Each: 12 coins/1 shard; final: 60 coins, 5 shards, 1 point, +0.25 speed and beacon travel");
                return q;
            }
            if (id == "secrets")
            {
                q.title = L("旧路的新光", "Light on Old Roads"); q.category = L("探索发现", "DISCOVERY"); q.description = L("巡路队的补给留在六条封闭支路里。用星核能力重访旧地，把他们的故事带回来。", "Six sealed paths hold road-warden caches. Return with core powers and recover their stories.");
                int[] rooms = { 1, 2, 3, 5, 6, 7 }, powers = { 1, 2, 3, 2, 1, 3 }; int count = 0, next = -1;
                for (int i = 0; i < rooms.Length; i++) { if (Has("cache_" + rooms[i])) count++; else if (next < 0 && Ability(powers[i]) && CanEnter(rooms[i])) next = i; }
                q.complete = count == 6; q.locked = !Ability(1); q.room = next < 0 ? save.stage + 1 : rooms[next]; q.room = Math.Min(7, q.room);
                q.target = next < 0 ? new Point(29, 8) : Has("seal_" + q.room) ? new Point(4.5f, 19.5f) : new Point(6.5f, 19.5f);
                q.next = q.complete ? L("六份巡路记忆已找回", "All six road memories recovered") : next < 0 ? L("推进主线或地区调查，解锁更多能力与路线", "Advance the story to gain powers and routes") : Has("seal_" + q.room) ? L("走入西南支路，打开旧日补给", "Enter the southwest path and open the cache") : L("在西南封印附近按 R 或 E", "Press R or E near the southwest seal");
                q.steps.Add(new QuestStep(L("找回补给 ", "Caches recovered ") + count + "/6", count == 6));
                q.steps.Add(new QuestStep(L("获得前三枚星核的能力", "Gain the first three core powers"), Ability(1) && Ability(2) && Ability(3)));
                q.reward = L("每箱：硬币30 / 碎片3 / 炖汤1 / 熟练度8", "Per cache: 30 coins / 3 shards / 1 stew / 8 mastery"); return q;
            }
            if (id.StartsWith("ally"))
            {
                int n = int.Parse(id.Substring(4)), item = n == 1 ? 2 : n == 2 ? 3 : 4, count = n == 1 ? 6 : n == 2 ? 3 : 5;
                q.category = L("镇民委托", "TOWN REQUEST"); q.title = new[] { "", L("杏的药草", "An's Moonherbs"), L("老潮的鲜鱼", "Old Tide's Silverfin"), L("诺尔的零件", "Noll's Spare Parts") }[n];
                q.complete = Has("ally_" + n);
                q.description = n == 2 ?
                    q.complete ?
                        L("老潮已经把三条银鳍鱼熬成热汤，避难所的孩子们终于吃上了一顿晚饭。",
                          "Old Tide cooked the three Silverfin for the shelter children. Their supper is finally warm.") :
                    save.inventory[3] >= 3 ?
                        L("三条银鳍鱼已装进鱼袋。老潮正在东湖边等着，趁汤锅还热，送去给避难所的孩子们。",
                          "Three Silverfin are in your bag. Old Tide is waiting by the east lake with the pot on.") :
                        L("老潮答应为避难所的孩子们煮汤。去镇东湖边等鱼咬钩；听到水声就及时收线，别等鱼儿松开钩。",
                          "Old Tide promised the shelter children fish stew. Cast at the east lake; reel promptly when you hear the splash, before the fish lets go.") :
                    L("为镇里的居民准备一份补给，帮助边境恢复日常生活。",
                      "Gather supplies for a resident and help the town return to everyday life.");
                q.steps.Add(new QuestStep(L(GameItemsZh(item), GameItemsEn(item)) + " " + Math.Min(count, save.inventory[item]) + "/" + count, save.inventory[item] >= count || q.complete));
                q.steps.Add(new QuestStep(L("返回余烬镇交付", "Deliver in Emberfall"), q.complete)); q.reward = L("镇民支持 · 40硬币 · 技能点1", "Town support · 40 coins · 1 skill point");
                q.target = ObjectPoint(0, n == 1 ? "garden" : n == 2 ? "fish" : "forge", new Point(18, 14));
                if (!q.complete && save.inventory[item] < count && n != 2) { q.room = save.room > 0 && save.room < 8 ? save.room : 1; q.target = ResourcePoint(q.room, n == 1 ? "herb" : "ore"); }
                q.next = q.complete ? L("委托已完成", "Request complete") : save.inventory[item] >= count ? L("返回镇民处交付材料", "Deliver your supplies in town") : n == 2 ? L("到镇东湖边钓鱼", "Fish at the eastern lake") : L("采集材料；旅店休息可更新采集点", "Gather supplies; inn rest renews nodes"); return q;
            }
            if (id.StartsWith("personal"))
            {
                int n = int.Parse(id.Substring(8)); q.room = n + 5; q.locked = !Has("companion_" + n); q.complete = Has("personal_" + n);
                q.category = L("同伴故事", "COMPANION"); q.title = new[] { L("未寄出的回信", "The Unsent Reply"), L("没有主人的机器", "A Machine Without a Master"), L("钟声下的名字", "Names Beneath the Bells") }[n];
                q.description = L("倾听同行者的故事，完成一件属于他们自己的事。", "Listen to your companion and finish something that belongs to them.");
                bool ready = n == 0 ? Has("manifest_found") : n == 1 ? save.inventory[4] >= 4 && save.inventory[1] >= 4 : Has("lore_1") && save.inventory[2] >= 4;
                q.target = ObjectPoint(q.room, n == 0 && !ready ? "manifest" : n == 0 ? "captain" : n == 1 ? "tinker" : "keeper", new Point(18, 14));
                if (n == 2 && !Has("lore_1")) { q.room = 1; q.target = ObjectPoint(1, "lore", new Point(12, 5)); }
                q.steps.Add(new QuestStep(new[] { L("找回乘客名册", "Recover the passenger manifest"), L("零件4与星晶4", "4 parts and 4 crystals"), L("密林记录与药草4", "Forest record and 4 herbs") }[n], ready || q.complete));
                q.steps.Add(new QuestStep(L("与同伴交谈并交付", "Talk to your companion and deliver"), q.complete)); q.reward = L("羁绊+2 · 35硬币 · 碎片3 · 技能点1", "+2 bond · 35 coins · 3 shards · 1 point");
                q.next = q.complete ? L("同伴故事已完成", "Personal story complete") : q.locked ? L("先完成地区调查，招募同伴", "Finish the regional report to recruit them") : q.steps[ready ? 1 : 0].text; return q;
            }
            if (id == "bounty")
            {
                q.category = L("公会悬赏", "CONTRACT"); q.locked = save.bounty < 0; int n = Math.Max(0, save.bounty);
                q.title = q.locked ? L("暂无悬赏", "No active contract") : L(BountyNames[n], BountyNamesEn[n]); q.description = L("接取后才计算进度，完成后在委托簿领取报酬。", "Only actions after acceptance count. Claim rewards in Contracts.");
                bool done = !q.locked && save.bountyProgress >= BountyTargets[n]; q.steps.Add(new QuestStep(save.bountyProgress + "/" + BountyTargets[n], done));
                q.room = done || n == 2 ? 0 : n == 5 ? 8 : save.room > 0 && save.room < 8 ? save.room : 1;
                q.target = ObjectPoint(q.room, done ? "contract" : n == 2 ? "fish" : n == 1 ? "ore" : n == 5 ? "abyssExit" : "altar", new Point(21, 19));
                q.next = done ? L("打开 N 委托簿领取报酬", "Open N Contracts to claim") : q.locked ? L("在 N 委托簿选择一项悬赏", "Accept a contract in N Contracts") : q.title; q.reward = L("35硬币 · 30经验 · 碎片", "35 coins · 30 XP · shards"); return q;
            }
            if (id == "shore")
            {
                q.title = L("远岸来信", "Letters from Afar"); q.category = L("远岸剧情", "DISTANT SHORES"); q.description = L("星根失衡不止发生在堡垒附近。雾港的水位、沙海九年前的配给命令与高原被封存的遇难者名单，记录了谁在为屏障付出代价。把三份原始证词带到议事厅，让每一方亲自参与修复。", "The shield's cost reaches beyond the citadel: falsified harbor tides, a nine-year-old desert allocation order and names sealed beneath the mountain bells. Bring the three original records to the council so every community can decide what to repair."); q.locked = save.stage < 1; q.complete = save.frontierEnding > 0;
                foreach (string flag in new[] { "harbor_report", "desert_report", "mountain_report", "frontier_settled" }) q.steps.Add(new QuestStep(L(new[] { "雾港调查", "沙海调查", "高原调查", "达成三方协定" }[q.steps.Count], new[] { "Mistport report", "Desert report", "Mountain report", "Council covenant" }[q.steps.Count]), Has(flag)));
                if (q.complete) { q.next = L("协定已达成，自由探索远岸", "Covenant settled; explore the distant shores"); return q; }
                if (!Has("exp_intro")) { q.target = new Point(26, 9); q.next = L("找露恩接取调查；先交回第一枚星核", "Meet Lune; deliver the first starcore first"); return q; }
                if (Proofs == 3) { q.room = 5; q.target = new Point(18, 10); q.next = L("去雾港议事厅达成协定", "Settle the covenant at Mistport's council"); return q; }
                q.room = !Has("harbor_report") ? 5 : !Has("desert_report") ? 6 : 7; int region = q.room;
                q.target = ObjectPoint(region, region == 5 ? "captain" : region == 6 ? "tinker" : "keeper", new Point(18, 14));
                if (!Has("gate_" + region))
                {
                    if (region == 5 && save.harborPledge > 0)
                    {
                        bool rescue = save.harborPledge == 2 || save.harborPledge == 3 && RescueCount < 2;
                        for (int i = 0; i < 3; i++) if (!Has((rescue ? "rescue_" : "relay_") + i)) { q.target = ObjectPoint(region, (rescue ? "castaway" : "relay") + i, q.target); break; }
                        q.next = rescue ? L("救援乘客：每人木材1、药草1", "Rescue passengers: 1 timber and 1 herb each") : L("修复信号塔：每座零件1", "Repair relays: 1 part each");
                    }
                    else if (region == 6) { int[] wanted = { 1, 2, 1 }; int i = 0; while (i < 2 && save.mirrors[i] == wanted[i]) i++; q.target = ObjectPoint(region, "mirror" + i, q.target); q.next = L("旋转镜面，让光线抵达东侧蓄光池", "Rotate mirrors to reach the eastern receiver"); }
                    else if (region == 7) { int[] wanted = { 2, 0, 1 }; int i = 0; while (i < 2 && save.bells[i] == wanted[i]) i++; q.target = ObjectPoint(region, "bell" + i, q.target); q.next = L("调查钟阵；注意寒意，靠近火盆取暖", "Investigate the bells; warm up by braziers"); }
                    else q.next = L("先与米拉商定港口方案", "Agree on a harbor plan with Mira");
                }
                else if (!Has("boss_" + region)) { q.target = new Point(30, 8); q.next = L("前往祭坛挑战地区守护者", "Challenge the regional guardian at the altar"); }
                else
                {
                    // The same field record can be filed at the cleared altar
                    // or in person with the original regional companion.
                    // After a victory the altar is often much closer than
                    // that companion: guide to the useful nearby handoff
                    // rather than sending the player unnecessarily across
                    // the whole room just to finish an earned report.
                    Point companion=q.target;
                    Point altar=ObjectPoint(region,"expAltar",new Point(30,8));
                    bool nearRelay=save.room==region &&
                        (Position-altar).Length+.3f<(Position-companion).Length;
                    if(nearRelay)q.target=altar;
                    q.next=nearRelay?
                        L("守护者已击败：到祭坛传讯登记证据并选择结果，或亲自找同伴",
                            "Guardian defeated: file at the altar relay or speak to the companion"):
                        L("与当地同伴交谈提交调查；也可在已净化的祭坛传讯登记",
                            "File with the local companion, or use the cleared altar relay");
                }
                return q;
            }
            q.title = L("星陨主线", "The Fallen Star"); q.description = L(
                save.stage == 0 ? "星陨第七天，巡路队失踪，密林里的侦察员仍在等待救援。救回他，找到第一枚星核，同时查清森林为何开始封路。" :
                save.stage == 1 ? "第一枚星核已由公会保管，森林的伤口却没有愈合。遗迹水闸被旧命令困住，解开日、月、潮的机关，寻找下一份供能真相。" :
                save.stage == 2 ? "两枚星核被汇入堡垒，矿场却没有停止抽取。修复升降机，查清不停运转的机器究竟为谁工作。" :
                "三枚星核已汇集到堡垒。厄洛的屏障保护了城内，也抽走城外的生机。对峙后决定力量的归属；远岸证词可能证明还有替代之路。",
                save.stage == 0 ? "Day seven after Starfall: the roadwardens are missing and the forest scout waits for rescue. Find him, reach the first core and learn why the roots barricaded the road." :
                save.stage == 1 ? "The first core is in guild custody, but the forest is not healed. A forgotten order locks the ruined sluices; restore Sun, Moon and Tide to expose its cost." :
                save.stage == 2 ? "Two cores now feed the citadel, and the mines refuse to stop. Repair the elevator and discover whom the tireless machines serve." :
                "Three cores feed Orrin's shield, protecting those inside while draining the outer land. Confront him and choose where their power belongs. Farshore testimony can reveal another path.");
            q.complete = save.ending > 0;
            int chapter = save.stage + 1; q.room = chapter; q.target = new Point(29, 8);
            bool gateOpen = chapter == 4 || chapter == 1 && Has("scout_saved") || Has("gate_" + chapter);
            q.steps.Add(new QuestStep(L(chapter == 1 ? "解救侦察员" : chapter == 2 ? "破解遗迹石碑" : chapter == 3 ? "修复矿场升降机" : "抵达统领所在祭坛", chapter == 1 ? "Rescue the scout" : chapter == 2 ? "Solve the tablets" : chapter == 3 ? "Repair the elevator" : "Reach the warden's altar"), gateOpen));
            if(chapter<=3)
                q.steps.Add(new QuestStep(L("可选：留下本章居民的真实证词",
                    "Optional: obtain living testimony this chapter"),
                    Has("witness_complete_"+chapter)));
            q.steps.Add(new QuestStep(L("击败守护者或达成和平解决", "Defeat the guardian or negotiate peace"), Has("boss_" + chapter)));
            q.steps.Add(new QuestStep(L(chapter == 4 ? "决定边境的未来" : "回收星核并交回公会", chapter == 4 ? "Choose the frontier's future" : "Recover the core and deliver to the guild"), q.complete));
            if (!gateOpen)
            {
                q.target = ObjectPoint(chapter, chapter == 1 ? "scout" : chapter == 2 ? !Has("lore_2") ? "lore" : "rune" + new[] { 0, 2, 1 }[Math.Min(2, runeProgress)] : "engine", q.target);
                q.next = L(chapter == 1 ? "清理敌人，再解救侦察员" : chapter == 2 ? "调查记录，依线索触碰石碑" : "收集零件3，修复升降机", chapter == 1 ? "Clear enemies, then rescue the scout" : chapter == 2 ? "Read the record, then touch the tablets" : "Gather 3 parts and repair the elevator");
                if (chapter == 1 && save.room == 1 && save.kills[1] < 4) { Enemy e = enemies.Find(a => !a.boss); if (e != null) q.target = e.Position; q.next += " " + save.kills[1] + "/4"; }
                if (chapter == 3 && save.inventory[4] < 3) q.target = ResourcePoint(3, "ore");
            }
            else if (Has("core_" + chapter) && chapter < 4) { q.room = 0; q.target = new Point(18, 17); q.next = L("把星核交给莉雅", "Deliver the starcore to Lyra"); }
            else q.next = Has("boss_" + chapter) ? L("与祭坛互动，完成本章", "Interact with the altar to finish this chapter") : L("前往祭坛，准备守护者挑战", "Approach the altar and prepare for the guardian");
            if (chapter == 4 && !Has("boss_4"))
            {
                q.steps.Insert(0,new QuestStep(L("莉雅的第四章调查 · 三核审计",
                    "Lyra's Chapter Four investigation"),Has("triad_started")));
                for(int site=1;site<=3;site++)
                    q.steps.Insert(site,new QuestStep(L(new[]{"森林的再生账","遗迹的通行账","矿场的休息账"}[site-1],
                        new[]{"Forest renewal ledger","Ruins passage ledger","Mine workers' rest ledger"}[site-1]),
                        Has("triad_evidence_"+site)));
                q.steps.Insert(4,new QuestStep(L("击败堡垒保密卫兵并保全日志",
                    "Defeat audit security and preserve records"),Has("triad_sealed")));
                q.steps.Insert(5,new QuestStep(L("返回莉雅身边，公开三核真相",
                    "Publish the three-core truth with Lyra"),Has("triad_complete")));
                q.description+=L("\\n\\n第四章加入三核审计：莉雅将在公会提供一条可以主动追踪、会改变最终对峙的主线调查。旧存档仍可直接完成原有决战。",
                    "\\n\\nChapter Four now includes Lyra's three-core audit, a tracked campaign investigation that changes the confrontation. Existing saves retain the original finale route.");
            }
            if (q.complete) { q.room = 0; q.target = new Point(10, 11); q.next = L("故事已完成，自由探索边境", "Story complete; explore the frontier freely"); } return q;
        }
        private string GameItemsZh(int item) { return data.items[item].name; }
        private string GameItemsEn(int item) { return data.items[item].en; }

        // At dangerously high alpine cold the normal cave/quest compass
        // becomes a survival route. Use the SAME collision-aware BFS as the
        // actual navigation overlay, ranking by WALKED cells instead of a
        // misleading straight-line distance through a sealed rock wall.
        // Already-lit reachable fires outrank unlit ones; if no fire is lit,
        // a traveler carrying two timber can still find one to ignite.
        private Point? EmergencyBrazierGuideTarget()
        {
            if(save.room!=7 || save.cold<85f || Dying || practicing ||
                FieldWorkPreventsRest || Map.objects==null ||
                save.teaUntil>save.totalTime || LitBrazierRefuge ||
                FrostSignalRefuge || SkywatchRefuge ||
                SkywatchEchoRefuge || OuterPatrolRefuge ||
                NearActiveFieldStation(7,FieldStationRefugeRadius(7)))
                return null;
            Point? lit=null,unlit=null;
            int litSteps=int.MaxValue,unlitSteps=int.MaxValue;
            foreach(WorldObject fire in Map.objects)
            {
                if(fire==null)continue;
                bool station=fire.kind=="fieldstation7";
                if(!station && fire.kind!="brazier")continue;
                // An already restored weather station is a genuine warm
                // destination and needs no extra fuel. An unbuilt station
                // must not impersonate an affordable unlit brazier.
                bool burning=station?Has("fieldstation_7"):Has("warm_"+fire.id);
                if(!burning && (station || save.inventory[0]<2))continue;
                Point site=new Point(fire.x,fire.y);
                bool directlyReachable=HasPhysicalInteractionReach(site);
                List<Point> route=NavigationPath(site);
                if(!directlyReachable)
                {
                    if(route.Count==0)continue;
                    Point last=route[route.Count-1];
                    float d=(site-last).Length;
                    Point contact=d>.55f?
                        site-(site-last).Normalized*.55f:last;
                    if(d>=1.75f || !ClearAttackPath(last,contact))continue;
                }
                int walked=directlyReachable?0:route.Count;
                if(burning && walked<litSteps)
                {lit=site;litSteps=walked;}
                if(!burning && walked<unlitSteps)
                {unlit=site;unlitSteps=walked;}
            }
            return lit??unlit;
        }

        // Heat survival uses the actual two authored desert shelters, not
        // quest flags or a straight-line arrow through solid dunes. Player
        // quest tracking is unchanged: this is an urgent, reversible override
        // while exposed to dangerous desert heat.
        private Point? EmergencyShadeGuideTarget()
        {
            if(save.room!=6 || save.heat<85f || Dying || practicing ||
                racing || FieldWorkPreventsRest || Map.objects==null ||
                save.teaUntil>save.totalTime || NearKind("shade",3f) ||
                NearActiveFieldStation(6,FieldStationRefugeRadius(6)) ||
                GlasswindRefuge || OuterPatrolRefuge)
                return null;
            Point? nearest=null;
            int walkingSteps=int.MaxValue;
            foreach(WorldObject shade in Map.objects)
            {
                if(shade==null)continue;
                bool station=shade.kind=="fieldstation6";
                if(shade.kind!="shade"&&!station)continue;
                // A player-funded cooling station competes on WALKED
                // distance with natural oasis shade, never world distance.
                if(station&&!Has("fieldstation_6"))continue;
                Point site=new Point(shade.x,shade.y);
                bool near=HasPhysicalInteractionReach(site);
                List<Point> route=NavigationPath(site);
                if(!near)
                {
                    if(route.Count==0)continue;
                    Point end=route[route.Count-1];
                    float d=(site-end).Length;
                    Point contact=d>.55f?
                        site-(site-end).Normalized*.55f:end;
                    if(d>=1.75f || !ClearAttackPath(end,contact))
                        continue;
                }
                int distance=near?0:route.Count;
                if(distance<walkingSteps)
                {walkingSteps=distance;nearest=site;}
            }
            return nearest;
        }

        // Escort safety takes precedence over the ordinary return marker
        // only when the ACTUAL civilian is badly exposed and enough of the
        // timed journey remains to reach existing physical cover. This
        // reuses the live minimap/compass pathfinder; it does not teleport
        // the survivor or mutate the player's tracked journal quest.
        public Point? ReliefEscortShelterWaypoint
        {
            get { return CriticalEscortShelterTarget(); }
        }
        private Point? CriticalEscortShelterTarget()
        {
            if(!ReliefEscortActive || reliefMissionRoom!=save.room ||
                reliefFollowerSafety>35f || reliefMissionTime<=22f ||
                reliefFollowerWaiting || ReliefEscortGap>6.5f ||
                reliefClimateKitTime>0 || ReliefEscortSheltered ||
                Map.objects==null)return null;
            int region=save.room;
            Point? best=null;int bestWalk=int.MaxValue;
            foreach(WorldObject refuge in Map.objects)
            {
                if(refuge==null)continue;
                bool activeStation=refuge.kind=="fieldstation"+region &&
                    Has("fieldstation_"+region) &&
                    Has("farshore_relief_"+region);
                bool natural=(region==6&&refuge.kind=="shade") ||
                    (region==7&&refuge.kind=="brazier"&&
                        Has("warm_"+refuge.id));
                if(!activeStation&&!natural)continue;
                Point site=new Point(refuge.x,refuge.y);
                bool near=HasPhysicalInteractionReach(site);
                List<Point> route=NavigationPath(site);
                if(!near)
                {
                    if(route.Count==0)continue;
                    Point end=route[route.Count-1];
                    float d=(site-end).Length;
                    Point contact=d>.55f?
                        site-(site-end).Normalized*.55f:end;
                    if(d>=1.75f||!ClearAttackPath(end,contact))
                        continue;
                }
                int heroSteps=near?0:route.Count;
                // A destination the hero can reach may be across a sealed
                // barricade from the actual rescue civilian. The survivor
                // must have a genuine independently walkable route; never
                // route a near-failing escort into an impossible detour.
                Point survivor=reliefFollowerPosition;
                float gap=(site-survivor).Length;
                Point survivorContact=gap>.55f?
                    site-(site-survivor).Normalized*.55f:survivor;
                bool survivorNear=gap<1.75f &&
                    ClearAttackPath(survivor,survivorContact);
                int civilianSteps=0;
                if(!survivorNear)
                {
                    List<Point> civilianRoute=NavigationPathFrom(survivor,site);
                    if(civilianRoute.Count==0)continue;
                    Point end=civilianRoute[civilianRoute.Count-1];
                    float remaining=(site-end).Length;
                    Point contact=remaining>.55f?
                        site-(site-end).Normalized*.55f:end;
                    if(remaining>=1.75f ||
                        !ClearAttackPath(end,contact))continue;
                    civilianSteps=civilianRoute.Count;
                }
                // Count the longer REAL walk, not only the hero's optimistic
                // route. A vast civilian detour burns the timed rescue clock.
                int steps=Math.Max(heroSteps,civilianSteps);
                if(steps>20 || steps>=bestWalk)continue;
                bestWalk=steps;best=site;
            }
            return best;
        }

        public Point? NavigationTarget()
        {
            if (practicing) return enemies.Count > 0 ? (Point?)enemies[0].Position : null;
            // During an actual timed supply defense the world's objective
            // arrow points back to the vulnerable physical cache, never
            // away to an unrelated stale journal waypoint. Original tracked
            // quest selection is left untouched after combat.
            if(OuterPatrolActive)return outerPatrolPost;
            // While actually carrying civilian aid, the live crossing, seal
            // check and shelter must outrank an unrelated pinned quest. The
            // player's original tracked quest is never modified.
            if(SwordLessonActive)return SwordLessonLauncher;
            if(FrostSignalActive)return GetQuest("frost_signal").target;
            if(MineConvoyActive)return MineConvoyStage==2||
                MineConvoyStage==4?GetQuest("mine_convoy").target:MineCartPosition;
            if(FarCourierActive) return GetQuest("far_couriers").target;
            if(CivicResponseActive) return GetQuest("civic_services").target;
            if(CivicConduitActive) return GetQuest("civic_conduit").target;
            // A living, timed civilian rescue is a mandatory world objective,
            // not the user's pinned journal quest. Reuse its AUTHORITATIVE
            // stage-dependent waypoint so the normal compass follows the
            // ambush, actual waiting resident or return station without
            // rewriting the saved quest selection.
            if(reliefMissionRoom!=0 && reliefMissionRoom==save.room)
                return GetQuest("relief_rescue").target;
            if (save.showGuidance && save.room == 0 && TutorialStep >= 3 && TutorialStep <= 5 && !practicing) return new Point(11, 14);
            if (InAbyss) return AbyssCircuitGuideTarget() ??
                AbyssTestimonyGuideTarget() ??
                AbyssCircuitPuzzleTarget() ??
                AbyssAffordableWardTarget() ?? new Point(31, 14);
            Point? emergencyFire=EmergencyBrazierGuideTarget();
            if(emergencyFire.HasValue)return emergencyFire;
            Point? emergencyShade=EmergencyShadeGuideTarget();
            if(emergencyShade.HasValue)return emergencyShade;
            // A personal pin temporarily takes over ordinary quest routing
            // but NEVER redirects an active timed escort, defense, rescue or
            // emergency. The player's original tracked quest is untouched.
            Point? personal=LocalWaymark;
            if(personal.HasValue&&!FieldWorkPreventsRest)return personal;
            QuestGuide q = CurrentGuide; if (q.complete || q.locked) return null;
            if (q.room == save.room) return q.target;
            foreach (var o in Map.objects) if (o.kind == "portal" + q.room) return new Point(o.x, o.y);
            foreach (var o in Map.objects) if (o.kind == "portal0") return new Point(o.x, o.y);
            if (save.room == 0 && q.room == 8) return ObjectPoint(0, "abyssEntry", new Point(31, 14));
            if (save.room == 0 && q.room >= 5) return new Point(26, 9); return null;
        }
        public List<Point> NavigationPath(Point target)
        {
            return NavigationPathFrom(Position,target);
        }
        // The same live collision/path-search implementation can now
        // evaluate a civilian's actual feet. It never teleports the hero,
        // rewrites coordinates or mutates the persisted quest destination.
        // The shared scratch buffers are reused sequentially, not cached
        // across different actors or mutable world collisions.
        public List<Point> NavigationPathFrom(Point origin,Point target)
        {
            // Compass/UI callers intentionally accept the closest reachable
            // frontier even if the target is beyond a sealed wall.
            return NavigationPathFrom(origin,target,false);
        }
        public List<Point> NavigationPathFrom(Point origin,Point target,bool requirePhysicalFinish)
        {
            var path = new List<Point>();
            // Never convert corrupt transient feet or quest coordinates into
            // integer tile indices; casts from NaN / infinities can alias a
            // valid BFS start and produce fictitious escort navigation.
            if (!Finite(origin.x) || !Finite(origin.y) ||
                !Finite(target.x) || !Finite(target.y)) return path;
            // A sealed floor cannot be the origin of a genuine walk. The
            // old BFS seeded its blocked tile as visited, then silently
            // stepped into legal neighbors and drew a fictitious escape
            // route across flood seals/raised arena gates.
            if (Map == null || Map.rows == null || Map.rows.Length == 0 ||
                Map.rows[0] == null || !CanStand(origin)) return path;
            int w = Map.rows[0].Length, h = Map.rows.Length;
            int sx = (int)Math.Floor(origin.x), sy = (int)Math.Floor(origin.y);
            if (sx < 0 || sx >= w || sy < 0 || sy >= h) return path;
            int start = sy * w + sx;
            PreparePathSearch(w * h);
            int[] parent = pathParents; int head = 0, tail = 0; pathQueue[tail++] = start; VisitPath(start, -1); int end = start;
            float best = requirePhysicalFinish ? float.MaxValue : (origin - target).Length;
            while (head < tail)
            {
                int n = pathQueue[head++], x = n % w, y = n / w;
                Point foot = new Point(x + .5f, y + .5f);
                float d = (foot - target).Length;
                // An escort must reach the hero's PHYSICAL side of the
                // obstacle. A nearest tile 1.7 units away across a locked
                // wall is useful compass guidance but not a walkable escort
                // destination. Check only candidates near the goal.
                bool finish = !requirePhysicalFinish ||
                    (d <= 1.8f && ReliefActorLineWalkable(foot,target));
                if (finish)
                {
                    if (d < best) { best = d; end = n; }
                    if (d <= 1.2f) { end = n; break; }
                }
                for (int i = 0; i < 4; i++) { int xx = x + guidanceSteps[i, 0], yy = y + guidanceSteps[i, 1]; if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue; int next = yy * w + xx; if (PathVisited(next) || !Fits(new Point(xx + .5f, yy + .5f))) continue; VisitPath(next, n); pathQueue[tail++] = next; }
            }
            if (requirePhysicalFinish && best == float.MaxValue) return path;
            if (best > 1.8f && (new Point(end % w + .5f, end / w + .5f) - target).Length > 1.8f) return path;
            for (int n = end; n != start && n >= 0; n = parent[n]) path.Add(new Point(n % w + .5f, n / w + .5f)); path.Reverse(); return path;
        }
    }
}
