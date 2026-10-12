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
        private bool TalkFieldStation(WorldObject o)
        {
            if (!o.kind.StartsWith("fieldstation")) return false;
            int region;
            if (!int.TryParse(o.kind.Substring("fieldstation".Length), out region) ||
                region < 5 || region > 7 || save.room != region) return true;
            if (Has("fieldstation_" + region))
            {
                string textZh = region == 5 ?
                    "潮汐观测站重新运作。这里能稳定低潮45秒，让被水淹没的浅滩重新成为道路。" :
                    region == 6 ? "蓄光管开始运转。站在附近，沙海的热度会迅速消退。" :
                    "火盆热流已经送到小站。靠近站台，寒意会迅速消散。";
                string textEn = region == 5 ?
                    "Tidewatch is online. Hold low tide for 45 seconds to reopen flooded shoals." :
                    region == 6 ? "The restored sun channel cools the surrounding desert." :
                    "The repaired frost shelter now warms nearby travelers.";
                var stationChoices = new List<Choice>();
                if (region == 5)
                    stationChoices.Add(C("固定低潮45秒", "Hold low tide for 45 seconds", "fieldstation_tide"));
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
                if (Has("fieldstation_survey_complete"))
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
        private bool ChooseFieldStation(string action)
        {
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
                    "。\n沙海：无遮阴时热度每秒约增加1.3，达到85后会持续受伤；降温站附近4.2格内可消暑。\n高原：无火盆时寒意每秒约增加1.1，达到90后会持续受伤；保暖站附近4.2格内可驱寒。\n\n可以利用观测网传送到安全的站点，但敌人靠近时不能启动。",
                    "Field readings synchronized.\n\nMistport: " + tideEn +
                    ".\nGlass Desert: exposed heat rises about 1.3/s; 85+ causes repeated harm. The cooler protects a 4.2-tile radius.\nFrostchime: exposed cold rises about 1.1/s; 90+ causes repeated harm. The shelter warms a 4.2-tile radius.\n\nStation travel requires a clear area with no nearby hostiles.",
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
                save.tideHeldUntil = Math.Max(save.tideHeldUntil, save.totalTime + 45);
                revision++; sound = "quest";
                effects.Add(new Effect("discover", new Point(post.x, post.y), .6f));
                Notify("潮汐稳定：低潮延长45秒。", "Tide stabilized: low tide for 45 seconds.");
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
