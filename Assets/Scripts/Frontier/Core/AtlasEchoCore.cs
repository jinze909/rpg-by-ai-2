using System;
namespace Starfall
{
    // Optional elite fight; all active state is transient and save-compatible.
    public sealed partial class GameCore
    {
        private int atlasEchoRoom, atlasEchoCycle;
        private float atlasEchoPulseTime;
        public int AtlasEchoRoom { get { return atlasEchoRoom; } }
        private static bool IsAtlasEchoEnemy(Enemy e) { return e != null && e.id == 83001; }
        private void ResetAtlasEcho()
        {
            enemies.RemoveAll(IsAtlasEchoEnemy);
            hazards.RemoveAll(h => h.atlasEchoOwned);
            atlasEchoRoom = atlasEchoCycle = 0; atlasEchoPulseTime = 0;
        }
        private bool TalkAtlasEcho(WorldObject o)
        {
            if (o.kind != "archiveecho") return false;
            if (save.room != 3) return true;
            if (!Has("archive_complete"))
            {
                Dialog(o.name, o.en,
                    "校准台缺少完整星图。先帮镇上的修复师琳找回测绘记录。",
                    "The calibrator needs a complete atlas. Help Lin recover the lost survey records first.",
                    C("记住地点", "Remember the location", "close"));
                return true;
            }
            if (Has("archive_echo_complete"))
            {
                Dialog(o.name, o.en,
                    "你平息了旧测绘仪的回声，四向刻度重新对齐。首胜奖励不会重复发放。",
                    "You silenced the lost compass echo. Its bearings are aligned again; the first-clear reward cannot repeat.",
                    C("查看罗盘", "Inspect the compass", "close"));
                return true;
            }
            if (atlasEchoRoom == 3)
            {
                Dialog(o.name, o.en,
                    "回声守卫仍在行动。每轮三向紫色落点依次出现，空白方位可以闪避。它虚弱时还会延迟攻击中心。",
                    "Three violet contact warnings leave one open cardinal exit. At low health, a delayed center blast follows.",
                    C("继续战斗", "Keep fighting", "close"),
                    C("撤出试炼", "Withdraw", "archive_echo_abandon"));
                return true;
            }
            Dialog(o.name, o.en,
                "完整星图唤醒重甲回声守卫。它将在脚下旋转三方位打击，留出一个安全方向。观察预警、闪避落点，并从背后破甲。失败可重试。",
                "The atlas awakens an armored echo. Three rotating impact bearings leave one safe direction. Read the tells, dodge, and flank its armor. Retries are free.",
                C("激活回声试炼", "Awaken the echo", "archive_echo_start"),
                C("先准备", "Prepare first", "close"));
            return true;
        }
        private bool ChooseAtlasEcho(string action)
        {
            if (action == "archive_echo_abandon")
            {
                if (atlasEchoRoom != 0)
                { ResetAtlasEcho(); revision++; Notify("已撤出试炼，稍后可以重试。", "Echo trial withdrawn; retry any time."); }
                return true;
            }
            if (action != "archive_echo_start") return false;
            if (save.room != 3 || !Has("archive_complete") || Has("archive_echo_complete") ||
                atlasEchoRoom != 0 || roadTrialRoom != 0 || wayfarerEncounterRoom != 0 ||
                Dying || practicing || !CanEnter(3)) return true;
            Point marker = ObjectPoint(3, "archiveecho", new Point(-100, -100));
            if ((Position - marker).Length > 2.2f) return true;
            if (!SafeTravel)
            { Notify("先清理校准台附近的巡逻敌人。", "Clear nearby patrols before calibration."); return true; }
            Point[] options = { marker + new Point(-3, 0), marker + new Point(-3, -2),
                marker + new Point(-5, 0), marker + new Point(0, 3) };
            Point spawn = new Point(); bool found = false;
            foreach (Point candidate in options)
            {
                if (!CanStand(candidate) || NavigationPath(candidate).Count == 0 ||
                    (candidate - Position).Length < 2.6f ||
                    enemies.Exists(e => (e.Position - candidate).Length < 2.1f)) continue;
                spawn = candidate; found = true; break;
            }
            if (!found)
            { Notify("校准台周围没有安全的召唤空间。", "No safe echo spawn near the calibrator."); return true; }
            atlasEchoRoom = 3; atlasEchoCycle = 0; atlasEchoPulseTime = 2.5f;
            var elite = new Enemy(83001, 2, spawn.x, spawn.y, 220);
            // Reserved high IDs must not create a multi-hour opening delay.
            elite.timer = 1.05f; enemies.Add(elite);
            effects.Add(new Effect("discover", spawn, .65f));
            TrackQuest("archive_echo"); revision++; sound = "quest";
            Notify("回声守卫已苏醒！观察三向预警，从空白方位闪避。",
                "Echo awakened! Read three bearings and dodge to the unmarked exit.");
            return true;
        }
        private void TickAtlasEcho(float dt)
        {
            if (atlasEchoRoom == 0) return;
            if (save.room != atlasEchoRoom || Dying || practicing) { ResetAtlasEcho(); return; }
            Enemy elite = enemies.Find(IsAtlasEchoEnemy);
            if (elite == null)
            {
                ResetAtlasEcho();
                if (!Has("archive_echo_complete"))
                {
                    Flag("archive_echo_complete"); save.coins += 85; save.shards += 5;
                    AddXp(90); revision++; sound = "reward";
                    RecordMoment("平息测绘师的回声，旧路罗盘终于复原。",
                        "Silenced the surveyor's echo and restored the old route compass.");
                    Notify("回声试炼完成：85硬币、5碎片、90经验。",
                        "Echo cleared: +85 coins, +5 shards, +90 XP.");
                }
                return;
            }
            atlasEchoPulseTime -= dt;
            if (atlasEchoPulseTime > 0) return;
            atlasEchoCycle++;
            float phase = elite.hp / Math.Max(1, elite.maxHp);
            atlasEchoPulseTime = phase < .4f ? 3.1f : phase < .7f ? 3.6f : 4.4f;
            Point center = Position;
            Point[] cardinal = { new Point(1, 0), new Point(0, 1),
                new Point(-1, 0), new Point(0, -1) };
            int safe = (atlasEchoCycle - 1) % cardinal.Length, marks = 0;
            for (int i = 0; i < cardinal.Length; i++)
            {
                if (i == safe) continue;
                Point contact = center + cardinal[i] * 1.6f;
                if (!CanStand(contact)) continue;
                hazards.Add(new Hazard { position = contact, radius = 1.05f, timer = 1.4f,
                    damage = 15, kind = "bell", atlasEchoOwned = true }); marks++;
            }
            if (phase < .4f && CanStand(center))
                hazards.Add(new Hazard { position = center, radius = .85f, timer = 2.15f,
                    damage = 17, kind = "rift", atlasEchoOwned = true });
            if (marks > 0)
            {
                effects.Add(new Effect("number", elite.Position, .7f, L("方位旋转", "ROTATING BEARINGS")));
                if (atlasEchoCycle == 1 || phase < .4f)
                    Notify(phase < .4f ? "末段！三向打击后，中心将延迟爆发。" :
                        "三方位预警出现！朝空白方向闪避。",
                        phase < .4f ? "Final phase! A delayed center blast follows three bearings." :
                        "Three bearings marked! Dodge toward the unmarked direction.");
            }
        }
    }
}
