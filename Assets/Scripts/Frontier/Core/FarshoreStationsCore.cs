using System;
using System.Collections.Generic;

namespace Starfall
{
    // Optional world stations: the restored objects alter actual tide and climate.
    // All ownership uses existing save flags; no new serialized state is required.
    public sealed partial class GameCore
    {
        // Out-and-back environmental field surveys; live progress never enters the save schema.
        public int fieldSurveyRoom, fieldSurveyPhase;
        public float fieldSurveyTime;
        private bool fieldSurveyConditionWarned;
        private static readonly string[] FieldSurveyLandmarks = { "relay1", "receiver", "bell2" };
        public Point FieldSurveyTarget(int region)
        {
            if (region < 5 || region > 7) return new Point();
            return ObjectPoint(region, FieldSurveyLandmarks[region - 5], new Point(18, 14));
        }
        private void ResetFieldStationSurvey()
        {
            fieldSurveyRoom = fieldSurveyPhase = 0; fieldSurveyTime = 0;
            fieldSurveyConditionWarned = false;
        }
        private void TickFieldStationSurvey(float dt)
        {
            if (fieldSurveyRoom == 0) return;
            int region = fieldSurveyRoom;
            if (save.room != region || Dying || practicing) { ResetFieldStationSurvey(); return; }
            fieldSurveyTime -= dt;
            if (fieldSurveyTime <= 0)
            {
                ResetFieldStationSurvey(); revision++;
                Notify("巡测时间已到。观测数据失效，可以从站台重新出发。",
                    "Survey timed out. Return to the station to try again.");
                return;
            }
            if (fieldSurveyPhase == 1 && (Position - FieldSurveyTarget(region)).Length < 1.4f)
            {
                bool readable = region == 5 ? LowTide : region == 6 ? save.heat < 60 : save.cold < 60;
                if (!readable)
                {
                    if (!fieldSurveyConditionWarned)
                    {
                        Notify(region == 5 ? "水位过高，等待低潮再记录信号。" :
                            region == 6 ? "热浪干扰读数，先到阴凉处降温。" :
                            "寒意干扰读数，先靠近火盆或保暖站。",
                            region == 5 ? "The marker is submerged. Wait for low tide." :
                            region == 6 ? "Too much heat for a clear reading. Cool down first." :
                            "Too cold for a clear reading. Warm up first.");
                        fieldSurveyConditionWarned = true;
                    }
                    return;
                }
                fieldSurveyPhase = 2; revision++; sound = "quest";
                effects.Add(new Effect("discover", FieldSurveyTarget(region), .65f));
                Notify("远岸信号已记录！在倒计时结束前返回观测站。",
                    "Field reading secured! Return to the station before time runs out.");
            }
            if (fieldSurveyPhase != 2) return;
            foreach (WorldObject o in Map.objects)
            {
                if (o.kind != "fieldstation" + region ||
                    (Position - new Point(o.x, o.y)).Length >= 1.55f) continue;
                ResetFieldStationSurvey();
                if (Has("fieldstation_survey_" + region)) return;
                Flag("fieldstation_survey_" + region);
                save.coins += 24; save.shards += 2; AddXp(30);
                if (Has("fieldstation_survey_5") && Has("fieldstation_survey_6") &&
                    Has("fieldstation_survey_7") && !Has("fieldstation_survey_complete"))
                {
                    Flag("fieldstation_survey_complete"); save.tea += 2;
                    RecordMoment("三份风暴巡测数据已归档。远岸避风网可以预警下一场风暴。",
                        "Three field readings archived. The farshore network can forecast the next storm.");
                    Notify("三地巡测完成！获得24硬币、2碎片、30经验和额外凉茶2。",
                        "All field surveys complete! +24 coins, +2 shards, +30 XP, +2 bonus teas.");
                }
                else Notify("巡测报告已归档！获得24硬币、2碎片、30经验。",
                    "Field survey archived! +24 coins, +2 shards, +30 XP.");
                TrackQuest("station_patrol"); revision++; sound = "reward";
                return;
            }
        }
        public bool NearActiveFieldStation(int region, float radius)
        {
            if (save.room != region || !Has("fieldstation_" + region)) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "fieldstation" + region &&
                    (new Point(o.x, o.y) - Position).Length < radius) return true;
            return false;
        }
        // Relief upgrades use persistent string flags already supported by old saves.
        // A finished local survey supplies the measurements needed to site each refuge.
        public float FieldStationRefugeRadius(int region)
        {
            return region >= 6 && region <= 7 && Has("farshore_relief_" + region) ? 6.5f : 4.2f;
        }
        public int TidewatchHoldSeconds { get { return Has("farshore_relief_5") ? 90 : 45; } }
        // A timed, two-wave rescue sortie from each real civilian shelter.
        // The route and enemies are live combat; replay gives no further rewards.
        public int reliefMissionRoom, reliefMissionWave;
        public float reliefMissionTime;
        public Point reliefFollowerPosition;
        public float reliefFollowerSafety;
        public bool ReliefEscortActive { get { return reliefMissionRoom != 0 && reliefMissionWave == 3; } }
        private static bool IsReliefMissionEnemy(Enemy e)
        { return e != null && e.id >= 85000 && e.id < 85100; }
        private void ResetReliefMission()
        {
            enemies.RemoveAll(IsReliefMissionEnemy);
            projectiles.RemoveAll(p => p.reliefMissionOwned);
            reliefMissionRoom = reliefMissionWave = 0; reliefMissionTime = 0;
            reliefFollowerPosition = new Point(); reliefFollowerSafety = 0;
        }
        private bool SpawnReliefMissionWave()
        {
            Point landmark = FieldSurveyTarget(reliefMissionRoom);
            var spots = new List<Point>();
            for (int radius = 2; radius <= 6 && spots.Count < 2; radius++)
            for (int dx = -radius; dx <= radius && spots.Count < 2; dx++)
            for (int dy = -radius; dy <= radius && spots.Count < 2; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                Point candidate = landmark + new Point(dx, dy);
                if (!CanStand(candidate) || NavigationPath(candidate).Count == 0 ||
                    (candidate - landmark).Length > 6.5f ||
                    (candidate - Position).Length < 2f ||
                    enemies.Exists(e => (e.Position - candidate).Length < 1.8f) ||
                    spots.Exists(p => (p - candidate).Length < 3) ) continue;
                spots.Add(candidate);
            }
            if (spots.Count != 2) return false;
            for (int i = 0; i < 2; i++)
            {
                int kind = reliefMissionWave == 1 ? i == 0 ? 0 : 1 : i == 0 ? 2 : 0;
                var enemy = new Enemy(85000 + (reliefMissionRoom - 5) * 10 +
                    reliefMissionWave * 2 + i, kind, spots[i].x, spots[i].y,
                    65 + (reliefMissionRoom - 5) * 18 + (reliefMissionWave - 1) * 32);
                enemy.timer = .9f + .25f * i;
                enemies.Add(enemy);
                effects.Add(new Effect("discover", spots[i], .65f));
            }
            revision++; sound = "quest";
            return true;
        }
        private void BeginReliefMission()
        {
            int region = save.room;
            if (region < 5 || region > 7 || reliefMissionRoom != 0 ||
                !Has("farshore_relief_" + region) || !Has("fieldstation_survey_" + region) ||
                fieldSurveyRoom != 0 || roadTrialRoom != 0 || wayfarerEncounterRoom != 0 ||
                atlasEchoRoom != 0 || practicing || Dying || racing || !CanEnter(region)) return;
            Point post = ObjectPoint(region, "fieldstation" + region, new Point(-100, -100));
            if ((Position - post).Length > 2.2f) return;
            if (!SafeTravel)
            { Notify("先清理站台附近的巡逻敌人，才能启动救援。", "Clear patrols near the station before starting rescue."); return; }
            reliefMissionRoom = region; reliefMissionWave = 1; reliefMissionTime = 120;
            if (!SpawnReliefMissionWave())
            {
                ResetReliefMission();
                Notify("救援路线无法安全布置敌人，稍后可重试。", "Unable to set up a safe rescue route. Retry later.");
                return;
            }
            TrackQuest("relief_rescue");
            Notify("民用救援开始！120秒内赶往地图标记、击退两波敌人，再返回站台。",
                "Rescue launched! Reach the marked landmark, defeat two waves and return within 120 seconds.");
        }
        private void TickReliefMission(float dt)
        {
            if (reliefMissionRoom == 0) return;
            int region = reliefMissionRoom;
            if (save.room != region || Dying || practicing)
            { ResetReliefMission(); return; }
            reliefMissionTime -= dt;
            if (reliefMissionTime <= 0)
            {
                ResetReliefMission(); revision++;
                Notify("救援时间已过。未消耗物资，可再次行动。",
                    "Rescue timed out. No supplies lost; retry freely.");
                return;
            }
            if (enemies.Exists(IsReliefMissionEnemy)) return;
            if (reliefMissionWave == 1)
            {
                projectiles.RemoveAll(p => p.reliefMissionOwned);
                reliefMissionWave = 2;
                if (!SpawnReliefMissionWave())
                {
                    ResetReliefMission(); revision++;
                    Notify("道路发生变化，救援取消，可免费重试。",
                        "The route changed. Rescue canceled; retry for free.");
                    return;
                }
                Notify("第二波来了！重甲破坏者与突袭者正封锁撤离道路！",
                    "Second wave! An armored breaker and raider block the evacuation road!");
                return;
            }
            if (reliefMissionWave == 2)
            {
                reliefMissionWave = 3; revision++; sound = "quest";
                reliefFollowerPosition = FieldSurveyTarget(region);
                reliefFollowerSafety = 100;
                Point marker = FieldSurveyTarget(region);
                effects.Add(new Effect("discover", marker, .7f));
                Notify("掠夺者已击退！带领幸存者在剩余时间内返回避难站。",
                    "Raiders defeated! Guide the survivors back to the shelter before time expires.");
            }
            if (reliefMissionWave != 3) return;
            // Unfinished patrols can attack the people being escorted. The
            // hero can intercept by staying close but must remove the threat.
            float threat = 0;
            foreach (Enemy patrol in enemies)
                if (!IsReliefMissionEnemy(patrol) && patrol.hp > 0 &&
                    (patrol.Position - reliefFollowerPosition).Length < 2.3f &&
                    ClearAttackPath(patrol.Position, reliefFollowerPosition))
                    threat += patrol.kind == 2 ? 1.5f : 1f;
            if (threat > 0)
            {
                float cover = (Position - reliefFollowerPosition).Length < 1.8f ? .55f : 1;
                reliefFollowerSafety = Math.Max(0, reliefFollowerSafety - dt * threat * 8f * cover);
                if (reliefFollowerSafety <= 0)
                {
                    ResetReliefMission(); revision++; sound = "deny";
                    Notify("巡逻敌人击散了护送队！幸存者已撤至安全处，可以免费重试。",
                        "Patrols scattered the escort! Civilians retreated safely; retry without cost.");
                    return;
                }
            }
            // Real evacuees follow at human pace, constrained by actual
            // collision rather than teleporting to the hero or the station.
            Point follow = Position - reliefFollowerPosition;
            if (follow.Length > 1.3f)
            {
                Point advance = DirectionTo(reliefFollowerPosition, Position) * Math.Min(dt * 2.6f, follow.Length - 1.3f);
                reliefFollowerPosition = SweepActor(reliefFollowerPosition, advance);
            }
            Point station = ObjectPoint(region, "fieldstation" + region, new Point(-100, -100));
            if ((Position - station).Length >= 1.6f ||
                (reliefFollowerPosition - station).Length >= 2f) return;
            ResetReliefMission(); revision++;
            if (Has("relief_rescue_" + region))
            {
                Notify("救援重演成功！奖励只能首次领取。",
                    "Rescue replay complete! Rewards are first-clear only.");
                return;
            }
            Flag("relief_rescue_" + region);
            save.coins += 45; save.shards += 3; AddXp(50);
            RecordMoment("从第 " + (region - 4) + " 条远岸路线上救回被围困的居民。",
                "Rescued stranded civilians from farshore route " + (region - 4) + ".");
            if (Has("relief_rescue_5") && Has("relief_rescue_6") &&
                Has("relief_rescue_7") && !Has("relief_rescue_complete"))
            {
                Flag("relief_rescue_complete"); save.skillPoints++; save.tea += 2;
                Notify("三地居民全部获救！额外技能点1、凉茶2。",
                    "Civilians rescued across all three regions! Bonus talent and two teas.");
            }
            else Notify("居民平安抵达！45硬币、3碎片、50经验。",
                "Civilians safe! +45 coins, +3 shards and +50 XP.");
            TrackQuest("relief_rescue"); sound = "reward";
        }
        private bool TalkFieldStation(WorldObject o)
        {
            if (!o.kind.StartsWith("fieldstation")) return false;
            int region;
            if (!int.TryParse(o.kind.Substring("fieldstation".Length), out region) ||
                region < 5 || region > 7 || save.room != region) return true;
            if (Has("fieldstation_" + region))
            {
                string textZh = region == 5 ?
                    "潮汐观测站重新运作。这里能稳定低潮" + TidewatchHoldSeconds + "秒，让被水淹没的浅滩重新成为道路。" :
                    region == 6 ? "蓄光管开始运转。站在附近，沙海的热度会迅速消退。" :
                    "火盆热流已经送到小站。靠近站台，寒意会迅速消散。";
                string textEn = region == 5 ?
                    "Tidewatch is online. Hold low tide for " + TidewatchHoldSeconds + " seconds to reopen flooded shoals." :
                    region == 6 ? "The restored sun channel cools the surrounding desert." :
                    "The repaired frost shelter now warms nearby travelers.";
                var stationChoices = new List<Choice>();
                if (region == 5)
                    stationChoices.Add(C("固定低潮" + TidewatchHoldSeconds + "秒", "Hold low tide for " + TidewatchHoldSeconds + " seconds", "fieldstation_tide"));
                if (Has("farshore_relief_complete"))
                {
                    textZh += "\n\n平民避难路网已接通炉火旅店。附近安全时可经旧驿道返回旅店星图桌。";
                    textEn += "\n\nThe civilian refuge roads now reach the Hearth Inn. Travel back to its chart when the surrounding paths are clear.";
                    stationChoices.Add(C("返回炉火旅店", "Return to the Hearth Inn", "fieldstation_inn"));
                }
                if (Has("fieldstation_complete"))
                {
                    textZh += "\n\n三座观测站已联通。在周围安全时，可以直接前往其他两座站点。";
                    textEn += "\n\nThe three stations are linked. Travel directly to either other post when the coast is clear.";
                    string[] locationsZh = { "雾港潮汐站", "沙海降温站", "高原保暖站" };
                    string[] locationsEn = { "Mistport tidewatch", "Glass Desert cooler", "Frostchime shelter" };
                    for (int destination = 5; destination <= 7; destination++)
                        if (destination != region)
                            stationChoices.Add(C("传送至" + locationsZh[destination - 5],
                                "Travel to " + locationsEn[destination - 5],
                                "fieldstation_to_" + destination));
                }
                if (!Has("fieldstation_survey_" + region))
                {
                    if (fieldSurveyRoom == region)
                    {
                        textZh += "\n\n巡测进行中：" + (fieldSurveyPhase == 1 ? "前往环境标记" : "返回站台") + "，剩余" + (int)Math.Ceiling(fieldSurveyTime) + "秒。";
                        textEn += "\n\nSurvey active: " + (fieldSurveyPhase == 1 ? "reach the field marker" : "return to the post") + "; " + (int)Math.Ceiling(fieldSurveyTime) + "s left.";
                        stationChoices.Add(C("取消本次巡测", "Abandon this survey", "fieldstation_survey_cancel"));
                    }
                    else if (fieldSurveyRoom == 0)
                    {
                        textZh += "\n\n可选巡测：90秒内抵达区域环境标记、读取数据并返回。雾港需要低潮，沙海/高原需控制暑寒低于60。失败无损失。";
                        textEn += "\n\nOptional 90s field survey: reach the regional landmark, take a reading and return. Harbor requires low tide; desert/frost require exposure below 60. Failure costs nothing.";
                        stationChoices.Add(C("开始90秒环境巡测", "Begin 90-second field survey", "fieldstation_survey_start"));
                    }
                }
                else { textZh += "\n\n本站巡测报告已归档。"; textEn += "\n\nThis station's field survey is archived."; }
                if (save.frontierEnding > 0)
                {
                    if (Has("farshore_relief_" + region))
                    {
                        textZh += "\n\n本地应急庇护工程已完成。新的避险范围与潮汐控制立即生效；附近安全时可在站台缓慢恢复生命。";
                        textEn += "\n\nThis civilian refuge has stronger tide or climate protection. Rest here to recover health slowly while nearby roads are clear."; 
                    }
                    else if (Has("fieldstation_survey_" + region))
                    {
                        textZh += "\n\n地区巡测已确定安全施工地点。可以投入物资建设真正的平民避难通道；周围有敌人时无法施工。";
                        textEn += "\n\nField readings identified a safe construction route. Deliver supplies to build civilian shelter access; clear nearby hostiles first.";
                        string[] buildZh = { "增建救援浮桥 · 木材2 零件1", "增建绿洲遮阳道 · 星晶2 药草1", "增建防寒补给棚 · 木材1 药草2" };
                        string[] buildEn = { "Rescue pontoons · 2 timber, 1 part", "Shaded supply lane · 2 crystals, 1 herb", "Warming depot · 1 timber, 2 herbs" };
                        stationChoices.Add(C(buildZh[region - 5], buildEn[region - 5], "fieldstation_relief_" + region));
                    }
                    else
                    {
                        textZh += "\n\n新协定允许建设平民避难通道；请先完成本站的90秒环境巡测。";
                        textEn += "\n\nThe covenant funds civilian refuges. Complete this station's 90-second field survey first.";
                    }
                }
                if (Has("farshore_relief_" + region) && Has("fieldstation_survey_" + region))
                    stationChoices.Add(C("平民撤离行动", "Civilian rescue sortie", "fieldstation_rescue_menu"));
                if (Has("fieldstation_survey_complete") && !Has("farshore_relief_complete"))
                    stationChoices.Add(C("查看远岸气象预报", "View farshore weather forecast", "fieldstation_forecast"));
                stationChoices.Add(C("离开观测站", "Leave the station", "close"));
                Dialog(o.name, o.en, textZh, textEn, stationChoices.ToArray());
                return true;
            }
            string[] zh = { "潮水正在冲毁通往北岸的标记。维修需要木材1与零件1，必须在低潮时工作；修好后可以随时稳定低潮45秒。",
                "要让沙海站台降温，先把三面镜子的光送到蓄光池，再投入星晶1与零件1。之后站台周围将形成清凉区域。",
                "严寒让观测仪无法启动。先点燃西侧火盆，再提供木材1与药草1；修好后小站会持续向周围供暖。" };
            string[] en = { "The north-shore markers are washing away. Repair requires 1 timber and 1 spare part during low tide. Afterwards this post can hold low tide for 45 seconds.",
                "Channel sunlight through the three mirrors into the receiver, then contribute 1 crystal and 1 spare part. The repaired station cools nearby ground.",
                "The instrument has frozen. Light the western brazier, then provide 1 timber and 1 herb. The restored station keeps its surroundings warm." };
            string[] optionZh = { "低潮维修 · 木材1 / 零件1", "接通光路后维修 · 星晶1 / 零件1", "点燃火盆后维修 · 木材1 / 药草1" };
            string[] optionEn = { "Repair at low tide · timber 1 / part 1", "Repair after mirrors · crystal 1 / part 1", "Repair after brazier · timber 1 / herb 1" };
            Dialog(o.name, o.en, zh[region - 5], en[region - 5],
                C(optionZh[region - 5], optionEn[region - 5], "fieldstation_repair_" + region),
                C("先调查附近环境", "Explore the surroundings", "close"));
            return true;
        }
        // One collision-safe landing rule for local station links and the Inn relief chart.
        // Guard locked islands and newly spawned patrols rather than teleporting to the prop.
        private void LandNearFieldStation(int destination)
        {
            foreach (WorldObject o in Map.objects)
            {
                if (o.kind != "fieldstation" + destination) continue;
                Point anchor = new Point(o.x, o.y);
                Point[] approaches = { new Point(0, 1), new Point(0, -1),
                    new Point(1, 0), new Point(-1, 0), new Point(1, 1),
                    new Point(-1, 1), new Point(1, -1), new Point(-1, -1) };
                Point chosen = Position;
                float safety = -1; bool found = false;
                foreach (Point offset in approaches)
                {
                    Point candidate = anchor + offset;
                    // This checks destination collision and reachability from
                    // the normal map entrance; it cannot place the hero inside
                    // a locked gate or disconnected walkable island.
                    if (!CanStand(candidate) || NavigationPath(candidate).Count == 0)
                        continue;
                    float clearance = 9999;
                    foreach (Enemy enemy in enemies)
                        if (enemy.hp > 0)
                            clearance = Math.Min(clearance, (enemy.Position - candidate).Length);
                    if (found && clearance <= safety) continue;
                    chosen = candidate; safety = clearance; found = true;
                }
                if (found) { save.x = chosen.x; save.y = chosen.y; }
                break; // Safe map spawn stays the fallback.
            }
        }
        private bool ChooseFieldStation(string action)
        {
            if (action == "fieldstation_rescue_menu")
            {
                int currentRoom = save.room;
                if (currentRoom < 5 || currentRoom > 7 ||
                    !Has("farshore_relief_" + currentRoom) ||
                    !Has("fieldstation_survey_" + currentRoom)) return true;
                Point basePost = ObjectPoint(currentRoom, "fieldstation" + currentRoom, new Point(-100, -100));
                if ((Position - basePost).Length > 2.2f) return true;
                var options = new List<Choice>();
                if (reliefMissionRoom == currentRoom)
                    options.Add(C("放弃本次撤离", "Abandon this sortie", "fieldstation_rescue_cancel"));
                else options.Add(C(Has("relief_rescue_" + currentRoom) ? "重演救援（无重复奖励）" : "出发救援（120秒）",
                    Has("relief_rescue_" + currentRoom) ? "Replay rescue (no rewards)" : "Begin rescue (120 seconds)",
                    "fieldstation_rescue_start"));
                if (Has("fieldstation_survey_complete"))
                    options.Add(C("查看远岸气象预报", "Read weather forecast", "fieldstation_forecast"));
                options.Add(C("离开", "Leave", "close"));
                Dialog("远岸避难行动", "Farshore Evacuation",
                    "已有居民被困在巡测地点附近。前往标记处击退两波真实敌人，再把居民带回站台。若失败或中途离开，可免费重试。" +
                    (reliefMissionRoom == currentRoom ? "\n\n阶段 " + reliefMissionWave + "/3，剩余 " + (int)Math.Ceiling(reliefMissionTime) + " 秒。" : ""),
                    "Civilians are stranded near the surveyed landmark. Defeat two real enemy waves, then escort them back to this station. Retries are free." +
                    (reliefMissionRoom == currentRoom ? "\n\nStage " + reliefMissionWave + "/3, " + (int)Math.Ceiling(reliefMissionTime) + "s remaining." : ""),
                    options.ToArray());
                return true;
            }
            if (action == "fieldstation_rescue_cancel")
            {
                if (reliefMissionRoom != 0 && reliefMissionRoom == save.room)
                { ResetReliefMission(); revision++; Notify("撤离行动取消，可免费重试。", "Rescue canceled; retry for free."); }
                return true;
            }
            if (action == "fieldstation_rescue_start")
            { BeginReliefMission(); return true; }
            if (action == "fieldstation_inn")
            {
                if (reliefMissionRoom != 0)
                { Notify("先护送居民返回站台，或明确取消本次救援。", "Escort the civilians home or explicitly abandon rescue before traveling."); return true; }
                int sourceStation = save.room;
                if (sourceStation < 5 || sourceStation > 7 || !Has("farshore_relief_complete") ||
                    !Has("fieldstation_" + sourceStation) || Dying || practicing || racing ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0 ||
                    fieldSurveyRoom != 0) return true;
                WorldObject homePost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + sourceStation) { homePost = o; break; }
                if (homePost == null || (Position - new Point(homePost.x, homePost.y)).Length > 2.2f) return true;
                if (!SafeTravel)
                { Notify("附近敌人正在破坏补给线。暂时无法返回旅店。", "Nearby hostiles block the supply route to the inn."); return true; }
                Enter(9);
                if (save.room == 9 && CanStand(new Point(15, 15)))
                { save.x = 15; save.y = 15; }
                Notify("沿平民避难路线返回炉火旅店，物资仍在背包里。",
                    "The civilian refuge road leads back to the Hearth Inn with your supplies intact.");
                return true;
            }
            if (action.StartsWith("farshore_relief_to_"))
            {
                int reliefDestination;
                if (!int.TryParse(action.Substring("farshore_relief_to_".Length), out reliefDestination) ||
                    reliefDestination < 5 || reliefDestination > 7 || save.room != 9 ||
                    !Has("farshore_relief_complete") || !Has("fieldstation_" + reliefDestination) ||
                    !CanEnter(reliefDestination) || Dying || practicing || racing ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0 ||
                    fieldSurveyRoom != 0) return true;
                Point chart = ObjectPoint(9, "surveyDesk", new Point(-100, -100));
                if ((Position - chart).Length > 2.2f) return true;
                if (!SafeTravel)
                { Notify("旅店附近尚有敌人，不能启动运输路线。", "The route cannot open while hostiles threaten the inn."); return true; }
                Enter(reliefDestination);
                if (save.room != reliefDestination) return true;
                LandNearFieldStation(reliefDestination);
                revision++; sound = "travel";
                Notify("平民避难交通线已接通，你安全抵达远岸气象站。",
                    "Civilian relief route connected. You arrive safely beside a farshore station.");
                return true;
            }
            if (action.StartsWith("fieldstation_relief_"))
            {
                int reliefRegion;
                if (!int.TryParse(action.Substring("fieldstation_relief_".Length), out reliefRegion) ||
                    reliefRegion < 5 || reliefRegion > 7 || save.room != reliefRegion ||
                    save.frontierEnding == 0 || !Has("fieldstation_" + reliefRegion) ||
                    !Has("fieldstation_survey_" + reliefRegion) || Has("farshore_relief_" + reliefRegion) ||
                    !CanEnter(reliefRegion) || Dying || practicing || racing || fieldSurveyRoom != 0 ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0)
                    return true;
                WorldObject post = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + reliefRegion) { post = o; break; }
                if (post == null || (Position - new Point(post.x, post.y)).Length > 2.2f) return true;
                if (!SafeTravel)
                { Notify("敌人正在威胁施工路线。先清理附近的巡逻队。", "Hostiles threaten the construction route. Clear nearby patrols first."); return true; }
                if (reliefRegion == 5 && !LowTide)
                { Notify("修建浮桥需要低潮。先启动潮汐站或等待退潮。", "Pontoons must be built at low tide. Use the tidewatch or wait for the ebb."); return true; }
                if (!Spend(reliefRegion == 5 ? 2 : reliefRegion == 7 ? 1 : 0,
                    reliefRegion == 6 ? 2 : 0, reliefRegion == 6 ? 1 : reliefRegion == 7 ? 2 : 0,
                    0, reliefRegion == 5 ? 1 : 0)) return true;
                Flag("farshore_relief_" + reliefRegion);
                save.coins += 30; save.shards += 2; AddXp(35);
                effects.Add(new Effect("discover", new Point(post.x, post.y), .8f));
                RecordMoment(reliefRegion == 5 ? "新的浮桥和救援旗使低潮安全时段延长到90秒。" :
                    reliefRegion == 6 ? "绿洲遮阳道将沙海的降温庇护范围扩大到6.5格。" :
                    "防寒补给棚将高原的保暖庇护范围扩大到6.5格。",
                    reliefRegion == 5 ? "New rescue pontoons extend tidewatch access to 90 seconds." :
                    reliefRegion == 6 ? "The shaded supply route expands desert cooling to 6.5 tiles." :
                    "The warming depot expands frost shelter to 6.5 tiles.");
                if (Has("farshore_relief_5") && Has("farshore_relief_6") &&
                    Has("farshore_relief_7") && !Has("farshore_relief_complete"))
                {
                    Flag("farshore_relief_complete"); save.skillPoints++; save.tea += 2;
                    RecordMoment("三地应急避难通道建成，远岸旅人终于有了可持续的庇护网络。",
                        "All three civilian refuge routes are open. The farshore has a lasting shelter network.");
                    Notify("远岸平民庇护网完成！本站30硬币、2碎片、35经验；额外技能点1、凉茶2。",
                        "Civilian refuge network complete! +30 coins, 2 shards, 35 XP; bonus talent and 2 teas.");
                }
                else Notify("应急工程完工：30硬币、2碎片、35经验；本站气候庇护已强化。",
                    "Relief works complete: +30 coins, 2 shards, 35 XP; the local refuge is strengthened.");
                TrackQuest("farshore_relief"); revision++; sound = "reward";
                return true;
            }
            if (action == "fieldstation_forecast")
            {
                int currentRegion = save.room;
                if (currentRegion < 5 || currentRegion > 7 || !Has("fieldstation_survey_complete") ||
                    !Has("fieldstation_" + currentRegion) || Dying || practicing) return true;
                WorldObject forecastPost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + currentRegion) { forecastPost = o; break; }
                if (forecastPost == null ||
                    (Position - new Point(forecastPost.x, forecastPost.y)).Length > 2.2f) return true;
                float phase = save.totalTime % 36;
                int toEbb = (int)Math.Ceiling(phase < 22 ? 0 : 36 - phase);
                int toFlood = (int)Math.Ceiling(phase < 22 ? 22 - phase : 0);
                int held = (int)Math.Ceiling(Math.Max(0, save.tideHeldUntil - save.totalTime));
                string tideZh = phase < 22 ? "自然低潮，约" + toFlood + "秒后涨潮" :
                    "自然高潮，约" + toEbb + "秒后退潮";
                string tideEn = phase < 22 ? "natural low tide, rising in about " + toFlood + "s" :
                    "natural high tide, ebb in about " + toEbb + "s";
                if (held > 0)
                {
                    tideZh += "；潮闸仍固定低潮约" + held + "秒";
                    tideEn += "; sluice holds low tide for another " + held + "s";
                }
                Dialog("远岸风暴观测网", "Farshore Stormwatch",
                    "巡测数据已同步。\n\n雾港：" + tideZh +
                    "。\n沙海：无遮阴时热度每秒约增加1.3，达到85后会持续受伤；降温站附近" + FieldStationRefugeRadius(6).ToString("0.0") + "格内可消暑。\n高原：无火盆时寒意每秒约增加1.1，达到90后会持续受伤；保暖站附近" + FieldStationRefugeRadius(7).ToString("0.0") + "格内可驱寒。\n\n可以利用观测网传送到安全的站点，但敌人靠近时不能启动。",
                    "Field readings synchronized.\n\nMistport: " + tideEn +
                    ".\nGlass Desert: exposed heat rises about 1.3/s; 85+ causes repeated harm. The cooler protects a " + FieldStationRefugeRadius(6).ToString("0.0") + "-tile radius.\nFrostchime: exposed cold rises about 1.1/s; 90+ causes repeated harm. The shelter warms a " + FieldStationRefugeRadius(7).ToString("0.0") + "-tile radius.\n\nStation travel requires a clear area with no nearby hostiles.",
                    C("收起预报", "Close forecast", "close"));
                return true;
            }
            if (action == "fieldstation_survey_cancel")
            {
                if (fieldSurveyRoom == save.room && fieldSurveyRoom != 0)
                { ResetFieldStationSurvey(); revision++; Notify("已取消巡测，可随时重试。", "Survey abandoned; you may retry."); }
                return true;
            }
            if (action == "fieldstation_survey_start")
            {
                if (reliefMissionRoom != 0)
                { Notify("先完成当前居民救援，才能接取新巡测。", "Finish the active civilian rescue before beginning another survey."); return true; }
                int surveyRegion = save.room;
                if (surveyRegion < 5 || surveyRegion > 7 || fieldSurveyRoom != 0 ||
                    !Has("fieldstation_" + surveyRegion) || Has("fieldstation_survey_" + surveyRegion) ||
                    Dying || practicing || racing || !SafeTravel) return true;
                WorldObject surveyPost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + surveyRegion) { surveyPost = o; break; }
                if (surveyPost == null || (Position - new Point(surveyPost.x, surveyPost.y)).Length > 2.2f) return true;
                fieldSurveyRoom = surveyRegion; fieldSurveyPhase = 1; fieldSurveyTime = 90;
                fieldSurveyConditionWarned = false; TrackQuest("station_patrol");
                revision++; sound = "quest";
                Notify("巡测开始：前往地图标记并在90秒内返回观测站。",
                    "Survey started: visit the marked landmark and return within 90 seconds.");
                return true;
            }
            if (action.StartsWith("fieldstation_to_"))
            {
                if (reliefMissionRoom != 0)
                { Notify("救援进行中，不能抛下居民传送。可返回站台取消行动。", "Rescue underway. Return to the station to cancel instead of abandoning civilians."); return true; }
                // Do not silently discard a timed, unfinished field reading.
                // The player must finish or explicitly cancel it first.
                if (fieldSurveyRoom != 0)
                {
                    Notify("巡测进行中。先完成或取消本次巡测，才能使用站点传送。",
                        "Field survey underway. Finish or cancel it before station travel.");
                    return true;
                }
                int destination;
                if (!int.TryParse(action.Substring("fieldstation_to_".Length), out destination) ||
                    destination < 5 || destination > 7 || destination == save.room ||
                    save.room < 5 || save.room > 7 || !Has("fieldstation_complete") ||
                    !Has("fieldstation_" + save.room) || !Has("fieldstation_" + destination) ||
                    !CanEnter(destination) || Dying || practicing || roadTrialRoom != 0 ||
                    wayfarerEncounterRoom != 0 || racing) return true;
                WorldObject origin = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + save.room) { origin = o; break; }
                if (origin == null || (Position - new Point(origin.x, origin.y)).Length > 2.2f)
                    return true;
                if (!SafeTravel)
                {
                    Notify("敌人还在附近，观测网无法稳定传送。", "Nearby enemies disrupt station travel.");
                    return true;
                }
                Enter(destination);
                if (save.room != destination) return true;
                LandNearFieldStation(destination);
                revision++; sound = "travel";
                Notify("观测网已连接。你抵达另一座远岸安全站。",
                    "Stormwatch linked! Arrived at another farshore shelter.");
                return true;
            }
            if (action == "fieldstation_tide")
            {
                if (save.room != 5 || !Has("fieldstation_5") || Dying || practicing) return true;
                WorldObject post = null;
                foreach (WorldObject o in Map.objects) if (o.kind == "fieldstation5") { post = o; break; }
                if (post == null || (new Point(post.x, post.y) - Position).Length > 2.2f) return true;
                if (!SafeTravel) { Notify("附近仍有威胁，潮汐装置暂不可操作。", "Clear nearby hostiles before operating the tidewatch."); return true; }
                save.tideHeldUntil = Math.Max(save.tideHeldUntil, save.totalTime + TidewatchHoldSeconds);
                revision++; sound = "quest";
                effects.Add(new Effect("discover", new Point(post.x, post.y), .6f));
                Notify("潮汐稳定：低潮延长" + TidewatchHoldSeconds + "秒。", "Tide stabilized: low tide for " + TidewatchHoldSeconds + " seconds.");
                return true;
            }
            if (!action.StartsWith("fieldstation_repair_")) return false;
            int region;
            if (!int.TryParse(action.Substring("fieldstation_repair_".Length), out region) ||
                region < 5 || region > 7 || save.room != region || Dying || practicing ||
                !CanEnter(region) || Has("fieldstation_" + region)) return true;
            WorldObject station = null;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "fieldstation" + region) { station = o; break; }
            if (station == null || (new Point(station.x, station.y) - Position).Length > 2.2f) return true;
            if (!SafeTravel)
            {
                Notify("先清理观测站附近的敌人，才能安全维修。", "Clear nearby enemies before repairing the station.");
                return true;
            }
            bool condition = region == 5 ? LowTide :
                region == 6 ? Has("mirrors_lit") : Has("warm_brazier0");
            if (!condition)
            {
                Notify(region == 5 ? "需要等待低潮，或者启动港口的潮闸。" :
                    region == 6 ? "先把三面镜子的光送到东侧蓄光池。" :
                    "先用两份木材点燃西侧火盆。",
                    region == 5 ? "Wait for low tide, or operate the harbor sluice." :
                    region == 6 ? "Route the three mirrors' beam into the receiver first." :
                    "Light the western brazier using two timber first.");
                return true;
            }
            if (!Spend(region == 5 || region == 7 ? 1 : 0,
                region == 6 ? 1 : 0, region == 7 ? 1 : 0, 0,
                region <= 6 ? 1 : 0))
                return true;
            Flag("fieldstation_" + region);
            save.coins += 18; save.shards += 2; AddXp(20);
            effects.Add(new Effect("discover", new Point(station.x, station.y), .8f));
            RecordMoment(region == 5 ? "潮汐站修好了，浅滩重新有了安全航标。" :
                region == 6 ? "沙海降温站修好了，荒路上多了一片清凉之地。" :
                "霜钟保暖站修好了，雪地里的旅人有了避寒处。",
                region == 5 ? "The harbor tidewatch now marks a safe crossing." :
                region == 6 ? "The desert cooler offers a refuge from the sun." :
                "The frost shelter gives travelers a place to warm up.");
            if (Has("fieldstation_5") && Has("fieldstation_6") &&
                Has("fieldstation_7") && !Has("fieldstation_complete"))
            {
                Flag("fieldstation_complete"); save.skillPoints++; save.tea += 2;
                RecordMoment("三座观测站连成远岸避风网：技能点1、凉茶2。",
                    "Three weather stations connected: one talent point and two teas.");
                Notify("远岸观测网建成！额外获得技能点1、凉茶2。",
                    "Farshore stormwatch complete! Bonus: 1 talent point and 2 teas.");
            }
            else Notify("观测站已恢复！硬币18、碎片2、经验20。",
                "Station restored! +18 coins, +2 shards, +20 XP.");
            TrackQuest("stations"); revision++; sound = "reward";
            return true;
        }
    }
}
