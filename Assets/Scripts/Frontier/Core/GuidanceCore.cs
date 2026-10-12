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
        public static readonly string[] QuestIds = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "signals", "dispatches", "vigil", "wayfarers" };
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
                q.title = L("远岸来信", "Letters from Afar"); q.category = L("远岸剧情", "DISTANT SHORES"); q.description = L("港口的水位、沙海的调度、高原的名字。把三份原始记录带到同一张桌前。", "Tide levels, allocations and names. Bring the original records to one council table."); q.locked = save.stage < 1; q.complete = save.frontierEnding > 0;
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
            q.title = L("星陨主线", "The Fallen Star"); q.description = L("找到边境的星核，追查堡垒屏障背后的代价。", "Recover the frontier's starcores and uncover the shield's cost."); q.complete = save.ending > 0;
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
