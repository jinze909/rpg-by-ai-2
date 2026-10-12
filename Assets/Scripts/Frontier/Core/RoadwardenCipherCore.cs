using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // The original letters remain readable and keep their original rewards.
        private bool TalkRoadwardenCipher(WorldObject o)
        {
            if (!o.kind.StartsWith("history")) return false;
            int id;
            if (!int.TryParse(o.kind.Substring(7), out id) || id < 6 || id > 8 ||
                !Has("survey_complete")) return false;
            ReadHistory(id);
            if (!Has("dispatch_" + id))
            {
                var options = new List<Choice>(conversation.choices);
                options.Add(C("解读巡路暗号", "Decode roadwarden cipher", "dispatch_open_" + id));
                conversation.choices = options.ToArray();
            }
            return true;
        }

        private bool ChooseRoadwardenCipher(string action)
        {
            if (action == "dispatch_recap")
            {
                if (save.room == 9 && Has("dispatch_complete"))
                    Dialog("巡路队的誓言", "The Wardens' Promise",
                        "杏让药物送到需要的人手里；老潮让没有硬币的人用故事渡河；诺尔提醒人们，图纸握在谁手里，机器就会为谁服务。三封信留下的不是命令，而是一个约定：每个人都应该有回家的路。旅店愿意为守护这条路的人便宜两枚硬币供应热汤。",
                        "An sent medicine to those in need. Old Tide let people cross with stories instead of coins. Noll knew that the person holding a blueprint decides whom a machine serves. The letters left a promise: everyone deserves a way home. The inn now offers stew for two fewer coins to those who keep that road open.",
                        C("收起星图", "Close the chart", "close"));
                return true;
            }
            if (action.StartsWith("dispatch_open_"))
            {
                int id;
                if (!int.TryParse(action.Substring(14), out id) || !CanDecodeLetter(id)) return true;
                int n = id - 6;
                string[] zh = {
                    "密林旧信说药物不应只沿堡垒的路送。巡路队应该守护谁？",
                    "遗迹船票说没钱的人也能渡河。老摆渡人收什么作为船费？",
                    "矿场图纸上有两句互相矛盾的话。最终谁决定机器的用途？"
                };
                string[] en = {
                    "Medicine should travel beyond the citadel road. Whom should the wardens protect?",
                    "Those without money may still cross the river. What did the old ferryman accept as fare?",
                    "The mine blueprint contradicts itself. Who ultimately decides what a machine is used for?"
                };
                string[,] optionsZh = {
                    { "堡垒商队", "需要帮助的人", "上锁的仓库" },
                    { "硬币", "交出船票", "一个故事" },
                    { "机器自己", "握着图纸的人", "星核" }
                };
                string[,] optionsEn = {
                    { "Citadel caravans", "People in need", "A locked storehouse" },
                    { "Coins", "The ticket itself", "A story" },
                    { "The machine", "The person holding the plan", "The starcore" }
                };
                Dialog("巡路队的暗号", "Roadwarden Cipher", zh[n], en[n],
                    C(optionsZh[n, 0], optionsEn[n, 0], "dispatch_answer_" + id + "_0"),
                    C(optionsZh[n, 1], optionsEn[n, 1], "dispatch_answer_" + id + "_1"),
                    C(optionsZh[n, 2], optionsEn[n, 2], "dispatch_answer_" + id + "_2"));
                return true;
            }
            if (!action.StartsWith("dispatch_answer_")) return false;
            string[] parts = action.Substring(16).Split('_');
            int letter, answer;
            if (parts.Length != 2 || !int.TryParse(parts[0], out letter) ||
                !int.TryParse(parts[1], out answer) || answer < 0 || answer > 2 ||
                !CanDecodeLetter(letter)) return true;
            if (answer != (letter == 7 ? 2 : 1))
            {
                Dialog("暗号尚未解开", "Cipher Unsolved",
                    "再读一遍旧信，想想它真正关心的是谁。答错不会损失物资。",
                    "Reread the letter and think about whom it was written for. Wrong answers cost nothing.",
                    C("再试一次", "Try again", "dispatch_open_" + letter),
                    C("稍后再来", "Return later", "close"));
                return true;
            }
            Flag("dispatch_" + letter);
            save.coins += 20; save.shards++; AddXp(25); revision++; sound = "reward";
            RecordMoment("解读巡路暗号 " + (letter - 5) + "/3。", "Decoded roadwarden cipher " + (letter - 5) + "/3.");
            if (Has("dispatch_6") && Has("dispatch_7") && Has("dispatch_8") && !Has("dispatch_complete"))
            {
                Flag("dispatch_complete"); save.coins += 40; save.skillPoints++; revision++;
                RecordMoment("三封旧信拼成巡路队的誓言：让每个人都能找到归途。",
                    "Three letters reveal the wardens' promise: a way home for everyone.");
                Notify("巡路誓言完成！额外获得40硬币、技能点1。三座信标的守路试炼现已开放。",
                    "Roadwarden promise complete! +40 coins, 1 talent point. Beacon defense drills unlocked.");
            }
            else Notify("暗号解开！获得20硬币、1碎片、25经验。",
                "Cipher solved! +20 coins, 1 shard, 25 XP.");
            return true;
        }


        // Optional two-wave beacon defense. Completion uses existing save flags.
        // Leaving or loading restarts a drill; trial foes never become world patrols.
        private int roadTrialRoom, roadTrialWave;
        private bool roadTrialVeteran;
        private float roadTrialSurgeTimer;
        private int roadTrialSurgeCount;
        // The ward is a real, vulnerable objective, not a second health bar
        // persisted in saves. Leaving, losing, or loading restarts it cleanly.
        private float roadTrialWard;
        private bool roadTrialWardUnderAttack;
        private int roadTrialWardWarning;
        private int roadTrialRepairedWave;
        public bool RoadwardenWardRepairAvailable
        { get { return roadTrialRoom != 0 && roadTrialWard < 99.9f && roadTrialRepairedWave != roadTrialWave; } }
        public float RoadwardenWardIntegrity { get { return roadTrialRoom == 0 ? 0 : roadTrialWard; } }
        public bool RoadwardenWardUnderAttack { get { return roadTrialRoom != 0 && roadTrialWardUnderAttack; } }
        // Transient warnings belong to this drill, never to the persistent world.
        private readonly List<Hazard> roadTrialWarnings = new List<Hazard>();
        public int RoadwardenTrialRoom { get { return roadTrialRoom; } }
        public int RoadwardenTrialWave { get { return roadTrialWave; } }
        public int RoadwardenTrialTotalWaves { get { return roadTrialVeteran ? 3 : 2; } }
        public bool RoadwardenTrialVeteran { get { return roadTrialVeteran; } }
        private string RoadwardenVigilInnNote(bool english)
        {
            if (Has("road_trial_complete"))
                return english ? "\n\nAll three beacons are defended. Travelers can follow the wardens' lights again; I will keep their names in the inn ledger."
                    : "\n\n三座信标都守住了。旅人又能循着巡路灯火归来，我会把守路人的名字记在旅店账簿里。";
            if (Has("road_trial_1") || Has("road_trial_2") || Has("road_trial_3"))
                return english ? "\n\nI heard you defended a beacon. Keep the other routes safe too, if you can."
                    : "\n\n听说你守住了一座信标。如果可以，也请让另外两条归途平安。";
            return "";
        }

        private static bool IsRoadwardenTrialEnemy(Enemy e)
        { return e != null && e.id >= 81000 && e.id < 81100; }
        private void ResetRoadwardenTrial()
        {
            foreach (Hazard warning in roadTrialWarnings) hazards.Remove(warning);
            roadTrialWarnings.Clear();
            roadTrialRoom = roadTrialWave = 0;
            roadTrialVeteran = false;
            roadTrialSurgeTimer = 0; roadTrialSurgeCount = 0;
            roadTrialWard = 0; roadTrialWardUnderAttack = false; roadTrialWardWarning = 0;
            roadTrialRepairedWave = 0;
        }
        private string RoadwardenSurgeHint(int region, bool english)
        {
            if (region == 1) return english ? "Wave two: thorn pincers, then a delayed root underfoot. Dodge across the gap."
                : "第二波：荆棘先从两侧夹击，随后脚下生根。沿空隙闪避。";
            if (region == 2) return english ? "Wave two: sweeping tides. Step out of the marked lane."
                : "第二波：潮痕横扫，离开预警水线。";
            return english ? "Wave two: mine bursts. Leave the glowing blast circles."
                : "第二波：矿脉爆裂，避开发光的爆炸圈。";
        }
        private string RoadwardenTrialHint(int region, bool english)
        {
            if (!Has("dispatch_complete")) return "";
            if (roadTrialRoom == region)
                return english ? "\n\nDefense underway: wave " + roadTrialWave + "/" + RoadwardenTrialTotalWaves + ". Ward " + (int)Math.Ceiling(roadTrialWard) + "%. Return to this beacon to restore 25 ward for 35 energy once per wave, or abandon."
                    : "\n\n守路试炼进行中：第 " + roadTrialWave + "/" + RoadwardenTrialTotalWaves + " 波。护盾剩余 " + (int)Math.Ceiling(roadTrialWard) + "%。每波可在此花费35耐力修复25点护盾一次，或放弃试炼。";
            if (Has("road_trial_" + region))
                return english ? "\n\nOath fulfilled. Replay a harder three-wave veteran defense. Attackers drain the beacon ward if they reach it; stop them before it breaks. Rewards cannot be claimed twice."
                    : "\n\n守路誓约已完成。可再次挑战更难的三波精英守卫战。敌人靠近后会侵蚀信标护盾，务必阻止；奖励不会重复领取。";
            return english ? "\n\nOptional two-wave defense. Stop attackers before they drain the beacon ward. " + RoadwardenSurgeHint(region, true) + " Complete all three for a talent point. Leaving or loading restarts a drill."
                : "\n\n可选双波守路试炼。阻止敌人靠近并侵蚀信标护盾。" + RoadwardenSurgeHint(region, false) + " 完成三座信标可获得技能点。离开或读档需重新挑战。";
        }
        private bool ChooseRoadwardenTrial(string action)
        {
            if (action == "road_trial_repair")
            {
                if (!RoadwardenWardRepairAvailable || roadTrialRoom != save.room ||
                    (Position - ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position)).Length > 2.1f)
                    return true;
                if (save.energy < 35)
                {
                    Notify("耐力不足：修复信标需要35耐力。", "Not enough energy: repairing the ward costs 35.");
                    return true;
                }
                save.energy -= 35;
                roadTrialWard = Math.Min(100, roadTrialWard + 25);
                roadTrialRepairedWave = roadTrialWave;
                Point ward = ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position);
                effects.Add(new Effect("heal", ward, .55f)); sound = "heal"; revision++;
                Notify("信标护盾修复25点！本波不能再次修复。", "Beacon ward restored by 25! One repair per wave.");
                return true;
            }
            if (action == "road_trial_abandon")
            {
                if (roadTrialRoom != 0)
                {
                    enemies.RemoveAll(IsRoadwardenTrialEnemy);
                    ResetRoadwardenTrial(); revision++;
                    Notify("守路试炼已放弃。可再次从信标开始。", "Defense drill abandoned. Restart at the beacon.");
                }
                return true;
            }
            if (!action.StartsWith("road_trial_start_")) return false;
            int region;
            if (!int.TryParse(action.Substring(17), out region) || region < 1 || region > 3 ||
                save.room != region || !Has("dispatch_complete") || !Has("survey_" + region) ||
                roadTrialRoom != 0 || practicing || Dying)
                return true;
            Point beacon = ObjectPoint(region, "survey" + region, new Point(-100, -100));
            if ((Position - beacon).Length > 2.1f) return true;
            if (!SafeTravel)
            {
                Notify("先清理信标附近的敌人，再开始守路试炼。", "Clear nearby enemies before beginning the defense drill.");
                return true;
            }
            roadTrialRoom = region; roadTrialWave = 1;
            roadTrialVeteran = Has("road_trial_" + region);
            roadTrialSurgeTimer = 0; roadTrialSurgeCount = 0;
            roadTrialWard = 100; roadTrialWardUnderAttack = false; roadTrialWardWarning = 0;
            if (!SpawnRoadwardenTrialWave())
            {
                ResetRoadwardenTrial();
                Notify("信标周围没有足够的安全战斗空间。", "Not enough reachable space around this beacon for a drill.");
                return true;
            }
            TrackQuest("vigil");
            Notify(roadTrialVeteran ? "精英守路试炼！保护信标护盾，击退三波敌人；无重复奖励。" : "守路试炼开始！保护信标护盾，击退追猎者与射手。",
                roadTrialVeteran ? "Veteran defense! Protect the beacon ward through three waves; no repeat rewards." : "Defense drill! Protect the beacon ward from the hunter and archer.");
            return true;
        }
        private bool SpawnRoadwardenTrialWave()
        {
            Point beacon = ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, new Point(-100, -100));
            var positions = new List<Point>();
            for (int radius = 3; radius <= 6 && positions.Count < 2; radius++)
            for (int dx = -radius; dx <= radius && positions.Count < 2; dx++)
            for (int dy = -radius; dy <= radius && positions.Count < 2; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                Point candidate = new Point(beacon.x + dx, beacon.y + dy);
                if (!CanStand(candidate) || (candidate - Position).Length < 2.7f ||
                    (candidate - beacon).Length > 6.7f ||
                    enemies.Exists(e => (e.Position - candidate).Length < 2.0f) ||
                    positions.Exists(p => (p - candidate).Length < 3.5f) ||
                    NavigationPath(candidate).Count == 0) continue;
                positions.Add(candidate);
            }
            if (positions.Count != 2) return false;
            for (int slot = 0; slot < 2; slot++)
            {
                int kind = roadTrialWave == 1 ? slot :
                    roadTrialWave == 2 ? (slot == 0 ? 2 : 1) : (slot == 0 ? 2 : 0);
                int id = 81000 + roadTrialRoom * 10 + roadTrialWave * 2 + slot;
                float hp = 55 + roadTrialRoom * 15 + (roadTrialWave - 1) * 30 +
                    (roadTrialVeteran ? 20 : 0);
                enemies.Add(new Enemy(id, kind, positions[slot].x, positions[slot].y, hp));
                effects.Add(new Effect("discover", positions[slot], .65f));
            }
            revision++; sound = "quest";
            return true;
        }
        // The second wave has a distinct, telegraphed regional hazard pattern.
        // All strikes use the existing warning, dodge and damage pipeline.
        private void PlanRoadwardenSurge(List<Hazard> planned, Point position,
            float radius, float delay, float damage, string kind)
        {
            if (Fits(position))
                planned.Add(new Hazard { position = position, radius = radius,
                    timer = delay, damage = damage, kind = kind });
        }
        private void SpawnRoadwardenSurge()
        {
            Point center = Position;
            Point axis = roadTrialSurgeCount++ % 2 == 0 ? new Point(1, 0) : new Point(0, 1);
            var planned = new List<Hazard>();
            if (roadTrialRoom == 1)
            {
                // Two roots close from opposite sides; standing between them is safe.
                for (int side = -1; side <= 1; side += 2)
                    PlanRoadwardenSurge(planned, center + axis * (side * 1.6f),
                        .9f, 1.4f, 13, "root");
                // The pincer gap is safe initially, but not a permanent
                // camping spot. A delayed central root rewards repositioning.
                PlanRoadwardenSurge(planned, center, .65f, 1.9f, 12, "root");
            }
            else if (roadTrialRoom == 2)
            {
                // A sequential tide lane: dodge perpendicular, not along the wave.
                for (int step = -1; step <= 1; step++)
                    PlanRoadwardenSurge(planned, center + axis * (step * 2.1f),
                        .95f, 1.25f + (step + 1) * .2f, 14, "tide");
            }
            else if (roadTrialRoom == 3)
            {
                // Mine charges target the center, then burst out along one axis.
                PlanRoadwardenSurge(planned, center, 1.15f, 1.4f, 16, "glass");
                for (int side = -1; side <= 1; side += 2)
                    PlanRoadwardenSurge(planned, center + axis * (side * 2.3f),
                        .85f, 1.75f, 12, "glass");
            }
            if (planned.Count == 0) return;
            // Never create an unavoidable warning in a cramped or sealed space.
            Point[] exits = { center, center + new Point(1.6f, 0),
                center + new Point(-1.6f, 0), center + new Point(0, 1.6f),
                center + new Point(0, -1.6f) };
            bool escape = false;
            foreach (Point exit in exits)
            {
                if (!Fits(exit) || (exit - center).Length > .01f &&
                    NavigationPath(exit).Count == 0) continue;
                bool safe = true;
                foreach (Hazard warning in planned)
                    if ((exit - warning.position).Length <= warning.radius + .3f)
                    { safe = false; break; }
                if (safe) { escape = true; break; }
            }
            if (!escape) return;
            foreach (Hazard warning in planned)
            {
                hazards.Add(warning);
                roadTrialWarnings.Add(warning);
            }
        }
        private void TickRoadwardenTrial(float dt)
        {
            if (roadTrialRoom == 0) return;
            if (save.room != roadTrialRoom || Dying || practicing) { ResetRoadwardenTrial(); return; }
            // Attackers reaching the beacon siphon its ward until interrupted by
            // a hit, stagger, windup or death. This makes intercepting enemies
            // meaningful instead of merely kiting them around the map.
            float pressure = 0;
            Point anchor = ObjectPoint(roadTrialRoom, "survey" + roadTrialRoom, Position);
            foreach (Enemy attacker in enemies)
                if (IsRoadwardenTrialEnemy(attacker) && attacker.hp > 0 &&
                    attacker.stun <= 0 && attacker.hitTime <= 0 &&
                    attacker.state != 1 && attacker.releaseTime <= 0 &&
                    (attacker.Position - anchor).Length < 1.7f)
                    pressure += attacker.kind == 2 ? 1.3f : 1;
            roadTrialWardUnderAttack = pressure > 0;
            if (roadTrialWardUnderAttack)
            {
                roadTrialWard = Math.Max(0, roadTrialWard - dt * pressure * (roadTrialVeteran ? 7f : 5f));
                if (roadTrialWard <= 0)
                {
                    enemies.RemoveAll(IsRoadwardenTrialEnemy);
                    ResetRoadwardenTrial(); revision++; sound = "deny";
                    effects.Add(new Effect("danger", anchor, .55f) { radius = 1.2f });
                    Notify("信标护盾破碎！守路试炼失败，重整后可再次挑战。",
                        "Beacon ward shattered! Defense failed; regroup and try again.");
                    return;
                }
                int threshold = roadTrialWard <= 30 ? 2 : roadTrialWard <= 65 ? 1 : 0;
                if (threshold > roadTrialWardWarning)
                {
                    roadTrialWardWarning = threshold;
                    Notify(threshold == 2 ? "警告：信标护盾即将破碎！" : "信标受到侵蚀！击退靠近的敌人。",
                        threshold == 2 ? "Warning: beacon ward nearly broken!" : "Beacon under attack! Push back nearby enemies.");
                }
            }
            if (enemies.Exists(IsRoadwardenTrialEnemy))
            {
                if (roadTrialWave >= 2)
                {
                    roadTrialSurgeTimer -= dt;
                    if (roadTrialSurgeTimer <= 0)
                    {
                        roadTrialSurgeTimer = roadTrialWave == 3 ? 3.2f : 4.5f;
                        SpawnRoadwardenSurge();
                    }
                }
                return;
            }
            if (roadTrialWave < RoadwardenTrialTotalWaves)
            {
                roadTrialWave++;
                // A cleared wave restores a little ward, but damage sustained
                // earlier still matters during the remaining defense.
                roadTrialWard = Math.Min(100, roadTrialWard + 18);
                roadTrialWardUnderAttack = false;
                roadTrialSurgeTimer = 1.25f;
                if (roadTrialWave == 2) roadTrialSurgeCount = 0;
                if (!SpawnRoadwardenTrialWave())
                {
                    ResetRoadwardenTrial();
                    Notify("战场已改变，试炼中断；可以再次挑战。", "The arena changed; restart the drill at the beacon.");
                    return;
                }
                if (roadTrialWave == 2)
                    Notify("第二波！重甲破阵者与射手。" + RoadwardenSurgeHint(roadTrialRoom, false),
                        "Wave two! Armored breaker and archer. " + RoadwardenSurgeHint(roadTrialRoom, true));
                else
                    Notify("最终波！重甲破阵者与追猎者，区域攻击将更加频繁！",
                        "Final wave! Armored breaker and hunter; regional surges accelerate!");
                return;
            }
            int region = roadTrialRoom;
            bool veteran = roadTrialVeteran;
            ResetRoadwardenTrial();
            if (veteran)
            {
                effects.Add(new Effect("discover", Position, .8f));
                sound = "quest"; revision++;
                Notify("精英守路试炼完成！誓约奖励已领取，不会重复发放。",
                    "Veteran defense cleared! Oath rewards remain one-time only.");
                return;
            }
            if (Has("road_trial_" + region)) return;
            Flag("road_trial_" + region);
            save.coins += 35; save.shards += 2; AddXp(40); revision++;
            effects.Add(new Effect("discover", Position, .8f)); sound = "reward";
            RecordMoment("守住第 " + region + " 座巡路信标：获得35硬币、2碎片、40经验。",
                "Defended road beacon " + region + ": 35 coins, 2 shards, 40 XP.");
            if (Has("road_trial_1") && Has("road_trial_2") && Has("road_trial_3") && !Has("road_trial_complete"))
            {
                Flag("road_trial_complete"); save.skillPoints++; save.shards += 3; revision++;
                RecordMoment("三座信标的守路誓约完成：额外获得技能点1、碎片3。",
                    "All three beacon defenses complete: bonus 1 talent point and 3 shards.");
                Notify("巡路守望完成！额外获得技能点1、碎片3。", "Roadwarden vigilance complete! Bonus: 1 talent point, 3 shards.");
            }
            else Notify("信标守住了！获得35硬币、2碎片、40经验。",
                "Beacon defended! +35 coins, 2 shards, 40 XP.");
        }

        private bool CanDecodeLetter(int id)
        {
            return id >= 6 && id <= 8 && save.room == id - 5 &&
                Has("survey_complete") && Has("history_" + id) && !Has("dispatch_" + id);
        }
    }
}
