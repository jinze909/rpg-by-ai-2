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
        public static readonly string[] QuestIds = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "signals", "dispatches", "vigil", "wayfarers", "stations", "station_patrol", "archive_trail", "archive_echo", "root_harmony", "commons_relay", "skywatch", "skywatch_afterglow", "glasswind", "tide_salvage", "iron_temper", "farshore_relief", "relief_rescue" };
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
            int bit = 1 << Sector(save.x, save.y);
            if ((save.explored[save.room] & bit) == 0) { save.explored[save.room] |= bit; revision++; }
        }
        private static int Sector(float x, float y) { return (int)Clamp(x / 6, 0, 5) + (int)Clamp(y / 6, 0, 3) * 6; }
        public bool Explored(int room, float x, float y) { return (save.explored[room] & (1 << Sector(x, y))) != 0; }
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
                        (reliefFollowerWaiting || ReliefEscortGap > 6.5f) ?
                    reliefFollowerPosition :
                    ObjectPoint(q.room, "fieldstation" + q.room, new Point(12, 12));
                q.next = q.complete ? L("三地居民已获救，救援可无奖励重演", "Three regions safe; replay with no rewards") :
                    reliefMissionRoom != 0 ? L(reliefMissionWave == 0 ? "先到巡测地标找到失联居民" :
                            reliefMissionWave == 3 && reliefFollowerWaiting ? "回到居民身旁，按E继续护送" :
                            reliefMissionWave == 3 && ReliefEscortGap > 6.5f ? "不要走得太远，等待幸存者赶上" :
                            reliefMissionWave == 3 ? "带领居民返回气象站" : "击退巡测地点的袭击者",
                        reliefMissionWave == 0 ? "Reach the survey marker to find civilians" :
                            reliefMissionWave == 3 && reliefFollowerWaiting ? "Return to the civilian and press E to resume following" :
                            reliefMissionWave == 3 && ReliefEscortGap > 6.5f ? "Wait for your survivor to catch up" :
                            reliefMissionWave == 3 ? "Return survivors to the weather station" : "Defeat raiders near the landmark") +
                        " · " + (int)Math.Ceiling(reliefMissionTime) + "s" :
                    next == 0 ? L("先建设一处远岸避难站", "Build a farshore civilian shelter") :
                        L("前往已建成的站台接取居民救援", "Start a rescue at a finished shelter");
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
                q.description = L("密林、遗迹、矿场的三名旅人需要援助。捐出两份材料，或保护旅人安全度并击退两名掠夺者。受伤后可花一份材料紧急援助一次。",
                    "Three travelers need help in Thornwood, the Ruins and the Mine. Donate two supplies, or protect their safety meter while defeating two raiders. Spend one supply for a single emergency aid after injury.");
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
                    wayfarerEncounterRoom != 0 ? L("保护旅人安全度，击退两名伏击者", "Keep the traveler safe; defeat both raiders") :
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
                q.description = L("为镇里的居民准备一份补给，帮助边境恢复日常生活。", "Gather supplies for a resident and help the town return to everyday life."); q.complete = Has("ally_" + n);
                q.steps.Add(new QuestStep(L(GameItemsZh(item), GameItemsEn(item)) + " " + Math.Min(count, save.inventory[item]) + "/" + count, save.inventory[item] >= count || q.complete));
                q.steps.Add(new QuestStep(L("返回余烬镇交付", "Deliver in Emberfall"), q.complete)); q.reward = L("镇民支持 · 硬币与经验", "Town support · coins and experience");
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
                else q.next = L("与当地同伴交谈，提交调查", "Speak to the local companion and file the report"); return q;
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
            if (q.complete) { q.room = 0; q.target = new Point(10, 11); q.next = L("故事已完成，自由探索边境", "Story complete; explore the frontier freely"); } return q;
        }
        private string GameItemsZh(int item) { return data.items[item].name; }
        private string GameItemsEn(int item) { return data.items[item].en; }
        public Point? NavigationTarget()
        {
            if (practicing) return enemies.Count > 0 ? (Point?)enemies[0].Position : null;
            if (save.showGuidance && save.room == 0 && TutorialStep >= 3 && TutorialStep <= 5 && !practicing) return new Point(11, 14);
            if (InAbyss) return new Point(31, 14);
            QuestGuide q = CurrentGuide; if (q.complete || q.locked) return null;
            if (q.room == save.room) return q.target;
            foreach (var o in Map.objects) if (o.kind == "portal" + q.room) return new Point(o.x, o.y);
            foreach (var o in Map.objects) if (o.kind == "portal0") return new Point(o.x, o.y);
            if (save.room == 0 && q.room == 8) return ObjectPoint(0, "abyssEntry", new Point(31, 14));
            if (save.room == 0 && q.room >= 5) return new Point(26, 9); return null;
        }
        public List<Point> NavigationPath(Point target)
        {
            int w = Map.rows[0].Length, h = Map.rows.Length, sx = (int)Position.x, sy = (int)Position.y, start = sy * w + sx;
            var path = new List<Point>(); if (sx < 0 || sx >= w || sy < 0 || sy >= h) return path;
            PreparePathSearch(w * h, -2);
            int[] parent = pathParents; int head = 0, tail = 0; pathQueue[tail++] = start; parent[start] = -1; int end = start; float best = (Position - target).Length;
            while (head < tail)
            {
                int n = pathQueue[head++], x = n % w, y = n / w; float d = (new Point(x + .5f, y + .5f) - target).Length;
                if (d < best) { best = d; end = n; } if (d <= 1.2f) { end = n; break; }
                for (int i = 0; i < 4; i++) { int xx = x + guidanceSteps[i, 0], yy = y + guidanceSteps[i, 1]; if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue; int next = yy * w + xx; if (parent[next] != -2 || !Fits(new Point(xx + .5f, yy + .5f))) continue; parent[next] = n; pathQueue[tail++] = next; }
            }
            if (best > 1.8f && (new Point(end % w + .5f, end / w + .5f) - target).Length > 1.8f) return path;
            for (int n = end; n != start && n >= 0; n = parent[n]) path.Add(new Point(n % w + .5f, n / w + .5f)); path.Reverse(); return path;
        }
    }
}
