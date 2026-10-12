using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int inspectedWeapon, inspectedStyle, confirmationSlot;
        private string saveConfirmation, resumeBrief;
        private SaveData[] slotData;
        private string[] slotWarnings;
        private float resumeTime;
        private void RefreshSlots()
        { slotData = new SaveData[4]; slotWarnings = new string[4]; for (int i = 0; i < 4; i++) slotData[i] = SaveStore.Read(i - 1, out slotWarnings[i]); }
        private void DrawSaves()
        {
            Window(Game.L("旅程存档", "Journey saves"), Game.L("自动存档独立保留 · 三个手动位置 · 读取前确认", "Separate autosave · three manual slots · confirm before loading")); if (slotData == null) RefreshSlots();
            for (int i = 0; i < 4; i++)
            {
                int slot = i - 1; var s = slotData[i]; float y = 179 + i * 124; Card(279, y, 938, 108);
                Text(299, y + 11, 550, 28, slot < 0 ? Game.L("自动存档", "Autosave") : Game.L("手动存档 ", "Manual slot ") + (slot + 1), 20, gold);
                string description = s == null ? slotWarnings[i] != null ? Game.L("存档损坏，无法读取", "Damaged save; cannot load") : Game.L("空位置", "Empty slot") : Game.L(Game.data.maps[(int)GameCore.Clamp(s.room, 0, Game.data.maps.Length - 1)].name, Game.data.maps[(int)GameCore.Clamp(s.room, 0, Game.data.maps.Length - 1)].en) + " · LV " + s.level + " · " + Game.L("章节 ", "Chapter ") + (s.stage + 1);
                Text(299, y + 46, 548, 26, description, 15, paper);
                DateTime time; string stamp = s != null && DateTime.TryParse(s.savedAtUtc, out time) ? time.ToLocalTime().ToString("yyyy/MM/dd HH:mm") : "—";
                Text(299, y + 79, 548, 20, stamp + (s != null && slotWarnings[i] != null ? Game.L(" · 将恢复备份", " · backup recovery") : ""), 12, muted);
                if (Button(869, y + 23, 144, 55, Game.L("保存到这里", "Save here"), false, slot >= 0 && Game.Has("intro"))) { confirmationSlot = slot; if (SaveStore.Exists(slot)) saveConfirmation = "write"; else WriteSlot(slot); }
                if (Button(1029, y + 23, 161, 55, Game.L("读取旅程", "Load journey"), false, s != null)) { confirmationSlot = slot; saveConfirmation = "read"; }
            }
            if (saveConfirmation != null)
            {
                drawingModal = true; drawnControls.Clear(); Dim(); Card(365, 275, 550, 244); Text(390, 294, 500, 43, Game.L(saveConfirmation == "write" ? "覆盖这个手动存档？" : "读取这个旅程？", saveConfirmation == "write" ? "Replace this manual save?" : "Load this journey?"), 25, gold);
                Text(390, 354, 500, 65, Game.L(saveConfirmation == "write" ? "其他存档保持独立，旧版本将保留为备份。" : "当前旅程会先自动保存，然后读取所选进度。", saveConfirmation == "write" ? "Other slots stay independent. The previous version becomes a backup." : "Your current journey will autosave before the selected progress loads."), 18, paper);
                if (Button(390, 441, 242, 49, Game.L("确认", "Confirm"), true)) { string action = saveConfirmation; saveConfirmation = null; if (action == "write") WriteSlot(confirmationSlot); else LoadSlot(confirmationSlot); }
                if (Button(649, 441, 241, 49, Game.L("取消", "Cancel"))) saveConfirmation = null;
            }
        }
        private void WriteSlot(int slot)
        { if (VisualReviewActive) { Game.Notify("视觉测试场不会写入存档。", "The visual review lab does not write saves."); return; } Game.PrepareSave(); bool ok = SaveStore.Write(Game.save, slot); RefreshSlots(); Game.Notify(ok ? "手动存档完成。" : "保存失败，旧存档仍保留。", ok ? "Manual save complete." : "Save failed; the previous save is retained."); Game.sound = ok ? "save" : "deny"; }
        private void LoadSlot(int slot)
        {
            if (VisualReviewActive) { Game.Notify("请先退出视觉测试场。", "Exit the visual review lab first."); return; }
            string warning; SaveData saved = SaveStore.Read(slot, out warning); if (saved == null) { Game.Notify("无法读取这个存档。", "This save cannot be read."); return; }
            if (Game.Has("intro") && !Save()) { Game.Notify("当前旅程保存失败，读取已取消。", "Current journey could not save; loading canceled."); return; }
            Game.Load(saved); Game.Start(); camera = CameraTarget(); dialogParent = null; settingsParent = "world"; resumeBrief = Game.GuidanceHint(); resumeTime = 8; Game.sound = "travel";
            if (warning != null) Game.Notify("已恢复备份进度。", "Backup progress recovered.");
        }
        private void DrawWeaponComparison()
        {
            inspectedWeapon = (int)GameCore.Clamp(inspectedWeapon, 0, 2); inspectedStyle = (int)GameCore.Clamp(inspectedStyle, 0, 2);
            WeaponProfile current = Game.Profile(Game.save.weapon, Game.Style), candidate = Game.Profile(inspectedWeapon, inspectedStyle);
            Card(279, 448, 619, 243); Text(297, 459, 583, 27, Game.L("武器与打法比较", "Weapon & style comparison"), 18, gold);
            for (int i = 0; i < 3; i++) if (Button(294 + i * 199, 497, 187, 30, WeaponName(i), inspectedWeapon == i)) { inspectedWeapon = i; inspectedStyle = Game.save.weaponStyles[i]; }
            for (int i = 0; i < 3; i++) if (Button(294 + i * 199, 539, 187, 28, Game.L(new[] { "均衡", "蓄势", "流转" }[i], new[] { "Balanced", "Focused", "Flow" }[i]), inspectedStyle == i)) inspectedStyle = i;
            bool owned = inspectedWeapon == 0 || Game.Has("weapon_" + inspectedWeapon), unlocked = inspectedStyle == 0 || Game.Has("core_1") || Game.MasteryRank(inspectedWeapon) >= 1;
            string stats = Game.L("伤害 / 间隔 / 耗能：", "Damage / interval / energy: ") + candidate.damage.ToString("0") + " / " + candidate.interval.ToString("0.00") + "s / " + candidate.energy + "\n" + Game.L("当前：", "Equipped: ") + current.damage.ToString("0") + " / " + current.interval.ToString("0.00") + "s / " + current.energy;
            Text(297, 577, 389, 59, stats, 13, paper);
            if (Button(710, 580, 174, 52, Game.L("装备此打法", "Equip style"), true, owned && unlocked)) { Game.Equip(inspectedWeapon); Game.SetStyle(inspectedStyle); }
            Text(297, 642, 583, 41, !owned ? Game.L("先去铁匠处制作武器。", "Craft this weapon at the blacksmith first.") : !unlocked ? Game.L("第一星核或本武器熟练度1解锁打法。", "First core or weapon mastery rank 1 unlocks styles.") : candidate.benefit + " · " + candidate.cost, 12, muted);
        }
        private void DrawJourney()
        {
            Card(279, 226, 429, 464); Text(301, 245, 382, 34, Game.L("世界记得你的选择", "The world remembers"), 22, gold); Text(301, 296, 382, 366, Game.WorldResponse(), 15, paper);
            for (int i = 0; i < Math.Min(6, Game.save.moments.Count); i++) { var m = Game.save.moments[Game.save.moments.Count - 1 - i]; float y = 226 + i * 78; Card(728, y, 489, 72); Text(745, y + 6, 450, 17, Game.L("第 ", "Day ") + m.day + Game.L(" 天", ""), 11, mint); Text(745, y + 26, 450, 42, Game.L(m.zh, m.en), 12, paper); }
            if (Game.save.moments.Count == 0) Text(755, 287, 422, 119, Game.L("完成任务、发现旧路、改变地方决定后，关键足迹会保存在这里。", "Quest updates, discoveries and regional decisions leave a record here."), 18, muted);
        }
        private bool DrawPolishObject(WorldObject o)
        {
            Point p = ScreenPoint(new Point(o.x, o.y));

            if (o.kind.StartsWith("survey"))
            {
                bool desk = o.kind == "surveyDesk";
                bool found = desk ? Game.Has("survey_complete") : Game.Has("survey_" + o.kind.Substring(6));
                bool defending = !desk && Game.RoadwardenTrialRoom == Game.save.room;
                bool defended = !desk && Game.Has("road_trial_" + o.kind.Substring(6));
                Color accent = defending ? (Game.RoadwardenTrialVeteran ? PixelArt.C("#b9a4ed") : PixelArt.C("#e69d83")) : defended ? gold : found ? mint : gold;
                PropImage(desk ? "bookshelf" : "column", 0, p.x - 27, p.y - 62, 54, 68, found ? muted : Color.white);
                float pulse = found || Game.save.reducedMotion ? 0 : Mathf.Sin(clock * 2.6f) * .05f;
                Ring(new Point(o.x, o.y), .36f + pulse, accent, 16);
                string label = desk ? Game.L("归乡星图", "ROAD CHART") :
                    defending ? Game.L(Game.RoadwardenTrialVeteran ? "精英 " : "守望 ", Game.RoadwardenTrialVeteran ? "ELITE " : "WAVE ") + Game.RoadwardenTrialWave + "/" + Game.RoadwardenTrialTotalWaves :
                    defended ? Game.L("已守住", "GUARDED") : found ? "✓" : o.kind.Substring(6);
                Text(p.x - 46, p.y - 72, 92, 24, label, 12, accent, TextAnchor.MiddleCenter);
                if (defending)
                {
                    Color wardColor = Game.RoadwardenWardIntegrity < 30 ? PixelArt.C("#f27d7d") :
                        Game.RoadwardenWardUnderAttack ? PixelArt.C("#f4b16c") : mint;
                    // Fixed, motion-independent integrity readout at the actual
                    // protected beacon; no extra overlay across the combat arena.
                    Bar(p.x - 35, p.y + 11, 70, Game.RoadwardenWardIntegrity / 100f, wardColor, 6);
                    Text(p.x - 42, p.y + 17, 84, 18, Game.L("护盾 ", "WARD ") +
                        (int)Math.Ceiling(Game.RoadwardenWardIntegrity) + "%", 10, wardColor, TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (o.kind == "mentor" || o.kind == "returnscout") { DrawNpc(o.kind, new Point(o.x, o.y)); Text(p.x - 55, p.y - 89, 110, 24, o.kind == "mentor" ? Game.L("训练场", "PRACTICE") : Game.L("已归来", "HOME"), 12, mint, TextAnchor.MiddleCenter); return true; }
            if (o.kind.StartsWith("seal")) { bool open = Game.Has("seal_" + Game.save.room); Ring(new Point(o.x, o.y), .4f, open ? mint : gold, 16); if (!open) { Icon("relic", p.x - 18, p.y - 38, 36); Text(p.x - 44, p.y + 14, 88, 23, ControlKey("R / E", "B"), 12, gold, TextAnchor.MiddleCenter); } return true; }
            if (o.kind.StartsWith("cache")) { bool found = Game.Has("cache_" + Game.save.room); PropImage("cache", 0, p.x - 26, p.y - 42, 52, 61, found ? muted : Color.white); return true; }
            return false;
        }
        private void DrawPolishWorld()
        {
            if (Game.mode != "world") return;
            if (Game.Dying) { Panel(426, 536, 428, 77, panel, gold); Text(442, 551, 395, 46, Game.L("你倒下了 · 镇民正在带你回家", "You fell · your neighbors are bringing you home"), 18, paper, TextAnchor.MiddleCenter); return; }
            Enemy near = Game.enemies.Find(e => e.state == 1 && (e.Position - Game.Position).Length < 7);
            if (near != null && !Game.practicing) { Panel(24, 228, 298, 90, new Color(.06f, .08f, .13f, .88f), PixelArt.C("#b37c6a")); Text(40, 241, 266, 68, Game.EnemyHint(near), 13, paper, TextAnchor.MiddleCenter); }
            if (Game.practicing) { Panel(348, 190, 584, 104, panel, mint); Text(365, 206, 549, 34, Game.L("安全练习 ", "Safe practice ") + (Game.practiceStage + 1) + "/3", 20, gold); Text(365, 250, 390, 31, PracticeInputHint, 16, paper); if (Button(785, 246, 130, 34, Game.L("结束练习", "End practice"))) Game.EndPractice(); }
            if (resumeTime > 0 && !Game.practicing) { Panel(352, 205, 576, 98, panel, gold); Text(369, 219, 542, 27, Game.L("欢迎回来 · 接下来", "Welcome back · next step"), 19, gold); Text(369, 255, 542, 40, resumeBrief, 14, paper); }
            if (Game.save.reducedMotion || Game.save.effectsQuality == 0) return;
            if (Game.save.room == 5) for (int i = 0; i < 24; i++) { float x = (i * 71 + clock * 27) % Width, y = (i * 53 + clock * 135) % Height; Box(x, y, 1, 6, new Color(.71f, .82f, .91f, .22f)); }
        }
        private void VolumeRow(float x, float y, string label, int channel)
        {
            float value = channel == 0 ? Game.save.musicVolume : channel == 1 ? Game.save.effectsVolume : Game.save.ambienceVolume;
            Text(x, y, 120, 24, label + " " + Math.Round(value * 100) + "%", 14, paper); Bar(x + 132, y + 9, 230, value, mint, 5);
            if (Button(x + 379, y - 3, 34, 28, "−")) value = Math.Max(0, value - .1f); if (Button(x + 423, y - 3, 34, 28, "+")) value = Math.Min(1, value + .1f);
            if (channel == 0) Game.save.musicVolume = value; else if (channel == 1) Game.save.effectsVolume = value; else Game.save.ambienceVolume = value;
        }
    }
}
