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
                if (Button(869, y + 23, 144, 55, Game.L("保存到这里", "Save here"), false, slot >= 0 && settingsParent != "title" && Game.Has("intro"))) { confirmationSlot = slot; if (SaveStore.Exists(slot)) saveConfirmation = "write"; else WriteSlot(slot); }
                if (Button(1029, y + 23, 161, 55, Game.L("读取旅程", "Load journey"), false, s != null)) { confirmationSlot = slot; saveConfirmation = "read"; }
            }
            if (saveConfirmation != null)
            {
                drawingModal = true; drawnControls.Clear(); Dim(); Card(365, 275, 550, 244); Text(390, 294, 500, 43, Game.L(saveConfirmation == "write" ? "覆盖这个手动存档？" : "读取这个旅程？", saveConfirmation == "write" ? "Replace this manual save?" : "Load this journey?"), 25, gold);
                Text(390, 354, 500, 65, Game.L(saveConfirmation == "write" ? "其他存档保持独立，旧版本将保留为备份。" : settingsParent == "title" ? "从此旅程开始游戏；自动存档与其他位置不受影响。" : "当前旅程会先自动保存，然后读取所选进度。", saveConfirmation == "write" ? "Other slots stay independent. The previous version becomes a backup." : settingsParent == "title" ? "Start this journey; your autosave and other slots stay unchanged." : "Your current journey will autosave before the selected progress loads."), 18, paper);
                if (Button(390, 441, 242, 49, Game.L("确认", "Confirm"), true)) { string action = saveConfirmation; saveConfirmation = null; if (action == "write") WriteSlot(confirmationSlot); else LoadSlot(confirmationSlot); }
                if (Button(649, 441, 241, 49, Game.L("取消", "Cancel"))) saveConfirmation = null;
            }
        }
        private void WriteSlot(int slot)
        { if (settingsParent == "title") { Game.Notify("请先进入游戏，才能保存到手动存档。", "Start playing before writing a manual save."); return; } if (VisualReviewActive) { Game.Notify("视觉测试场不会写入存档。", "The visual review lab does not write saves."); return; } Game.PrepareSave(); bool ok = SaveStore.Write(Game.save, slot); RefreshSlots(); Game.Notify(ok ? "手动存档完成。" : "保存失败，旧存档仍保留。", ok ? "Manual save complete." : "Save failed; the previous save is retained."); Game.sound = ok ? "save" : "deny"; }
        private void LoadSlot(int slot)
        {
            if (VisualReviewActive) { Game.Notify("请先退出视觉测试场。", "Exit the visual review lab first."); return; }
            string warning; SaveData saved = SaveStore.Read(slot, out warning); if (saved == null) { Game.Notify("无法读取这个存档。", "This save cannot be read."); return; }
            if (settingsParent != "title" && Game.Has("intro") && !Save()) { Game.Notify("当前旅程保存失败，读取已取消。", "Current journey could not save; loading canceled."); return; }
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
            string finisher = inspectedWeapon == 0 ?
                Game.L("第三击：+25%伤害，增加挥砍范围", "Third hit: +25% damage and extended sword reach") :
                inspectedWeapon == 1 ?
                Game.L("第三箭：+15%伤害；均衡流派穿透", "Third shot: +15% damage; Balanced pierces") :
                Game.L("第三发：+12%伤害，短暂减速", "Third cast: +12% damage and a brief slow");
            Text(297, 577, 389, 59, stats + "\n" + finisher, 12, paper);
            bool equipped = owned && inspectedWeapon == Game.save.weapon && inspectedStyle == Game.Style;
            if (Button(710, 580, 174, 52, equipped ?
                Game.L("当前已装备", "Already equipped") : Game.L("装备此打法", "Equip style"),
                equipped, owned && unlocked && !equipped))
            { Game.Equip(inspectedWeapon); Game.SetStyle(inspectedStyle); }
            Text(297, 642, 583, 41, !owned ? Game.L("先去铁匠处制作武器。", "Craft this weapon at the blacksmith first.") : !unlocked ? Game.L("第一星核或本武器熟练度1解锁打法。", "First core or weapon mastery rank 1 unlocks styles.") : candidate.benefit + " · " + candidate.cost, 12, muted);
        }
        private bool journeyShowRecap=true;
        private int journeyRecapIndex=-1;
        private void DrawJourney()
        {
            // One continuous story at a time is actually readable in the
            // existing 429px panel. The original world-response record and
            // the six real journal memories remain available alongside it.
            Card(279,226,429,464);
            Text(301,245,382,34,Game.L("冒险足迹","Journey & story"),22,gold);
            if(Button(297,285,189,33,Game.L("剧情回顾","Story so far"),
                journeyShowRecap))journeyShowRecap=true;
            if(Button(497,285,193,33,Game.L("世界变化","World response"),
                !journeyShowRecap))journeyShowRecap=false;
            if(journeyShowRecap)
            {
                var beats=Game.StoryRecap();
                if(journeyRecapIndex<0||journeyRecapIndex>=beats.Count)
                    journeyRecapIndex=beats.Count-1;
                StoryBeat beat=beats[journeyRecapIndex];
                Text(301,333,384,48,beat.title,21,gold);
                Text(301,385,382,28,
                    (beat.complete?Game.L("✓ 已完成","✓ Complete"):
                        Game.L("◆ 当前进度","◆ In progress"))+
                    "  ·  "+(journeyRecapIndex+1)+"/"+beats.Count,
                    13,beat.complete?mint:paper);
                Text(301,423,384,119,beat.summary,16,paper);
                Text(301,546,380,97,
                    Game.L("下一步：","Next: ")+beat.next,14,muted);
                if(Button(297,652,83,30,Game.L("上章","Prev"),
                    false,journeyRecapIndex>0))journeyRecapIndex--;
                if(Button(389,652,83,30,Game.L("下章","Next"),
                    false,journeyRecapIndex<beats.Count-1))
                    journeyRecapIndex++;
                // Earlier chapters are historical and must not change
                // progression when selected. Only track a live, unlocked
                // quest in the existing navigation system.
                QuestGuide quest=Game.GetQuest(beat.questId);
                if(Button(480,652,210,30,
                    beat.complete?Game.L("已载入历史","In the record"):
                        Game.L("追踪本章任务","Track this chapter"),
                    false,!beat.complete&&!quest.locked))
                    Game.TrackQuest(beat.questId);
            }
            else
                Text(301,333,382,326,Game.WorldResponse(),14,paper);
            for(int i=0;i<Math.Min(6,Game.save.moments.Count);i++)
            {
                var moment=Game.save.moments[Game.save.moments.Count-1-i];
                float y=226+i*78;
                Card(728,y,489,72);
                Text(745,y+6,450,17,
                    Game.L("第 ","Day ")+moment.day+Game.L(" 天",""),11,mint);
                Text(745,y+26,450,42,
                    Game.L(moment.zh,moment.en),12,paper);
            }
            if(Game.save.moments.Count==0)
                Text(755,287,422,119,Game.L(
                    "完成任务、发现旧路、改变地方决定后，关键足迹会保存在这里。",
                    "Quest updates, discoveries and regional decisions leave a record here."),
                    18,muted);
        }
        // An original 4x2 Sprite Sheet, NOT a reused GUI circle or a
        // second actor body. Painted behind the unchanged detailed beacon
        // column in the production World depth pass. Health and pressure are
        // GameCore-owned; this renderer never guesses them from enemy range.
        private Texture2D roadwardenWardSheet;
        private float roadwardenWardLastCheck=float.NegativeInfinity;
        private float roadwardenWardNextCheck=float.NegativeInfinity;
        private void DrawAuthoredRoadwardenWard(Point plantedFoot,
            float integrity,bool underAttack,int region)
        {
            if(drawingOutline||Game==null||Game.mode!="world")return;
            if(clock<roadwardenWardLastCheck||
                clock>=roadwardenWardNextCheck)
            {
                roadwardenWardLastCheck=clock;
                roadwardenWardNextCheck=clock+2.5f;
                Texture2D candidate=Resources.Load<Texture2D>(
                    "Frontier/Art/RoadwardenWard");
                // Partial Editor import MUST NOT paint chopped rings or
                // displace the original body. Hold the last complete sheet;
                // a first-run missing resource keeps the existing ring/bar.
                if(candidate!=null&&candidate.width==256&&
                    candidate.height==128)
                    roadwardenWardSheet=candidate;
            }
            if(roadwardenWardSheet==null)return;
            int row=integrity<=35f||underAttack?1:0;
            // Low FX and reduced-motion retain a planted, source-painted
            // warning cell: accessibility must not hide the objective state.
            int phase=Game.save.reducedMotion||
                Game.save.effectsQuality==0?0:
                ((int)Math.Floor(clock*3.3f+region*.37f)&3);
            Atlas(roadwardenWardSheet,4,2,row*4+phase,
                new Rect(plantedFoot.x-44f,plantedFoot.y-84f,88f,88f),
                Color.white);
        }

        private bool DrawPolishObject(WorldObject o)
        {
            Point p = ScreenPoint(new Point(o.x, o.y));


            if (o.kind == "rootlistener")
            {
                DrawNpc(o.kind, new Point(o.x, o.y));
                Color hue = Game.Has("root_harmony_complete") ? mint : gold;
                Ring(new Point(o.x, o.y), .42f, hue, 16);
                Text(p.x - 54, p.y - 84, 108, 24,
                    Game.L("星根听纹", "ROOT SONG"), 11, hue, TextAnchor.MiddleCenter);
                return true;
            }
            if (o.kind.StartsWith("rootcounter"))
            {
                int region;
                if (!int.TryParse(o.kind.Substring("rootcounter".Length), out region)) return false;
                bool sealedNode = Game.Has("root_harmony_" + region);
                bool inTrial = Game.RootHarmonyRoom == region;
                Color color = sealedNode ? mint : inTrial ? PixelArt.C("#f3a57a") : gold;
                PropImage("column", 0, p.x - 31, p.y - 69, 62, 74);
                Ring(new Point(o.x, o.y), .48f, color, 20);
                Text(p.x - 50, p.y - 80, 100, 22,
                    sealedNode ? Game.RootHarmonyRestoring ?
                        Game.L("安全休整", "RESTORING") : Game.L("已归档", "SEALED") :
                        inTrial ? Game.L("守卫 " , "DEFEND ") +
                            (int)Math.Ceiling(Game.RootHarmonyTime) + "s" :
                            Game.L("聆听音纹", "LISTEN"),
                    11, color, TextAnchor.MiddleCenter);
                return true;
            }
            if (o.kind == "worldchronicle")
            {
                PropImage("bookshelf", 0, p.x - 29, p.y - 64, 58, 68, Color.white);
                // A small persistent location cue, with no large text HUD.
                Ring(new Point(o.x, o.y), .43f, mint, 16);
                Text(p.x - 52, p.y - 84, 104, 21,
                    Game.L("世界档案", "WORLD LORE"), 11, mint, TextAnchor.MiddleCenter);
                return true;
            }

            if (o.kind.StartsWith("wayfarer"))
            {
                int region;
                if (!int.TryParse(o.kind.Substring(8), out region) || region < 1 || region > 3) return false;
                bool done = Game.Has("wayfarer_" + region), active = Game.WayfarerEncounterRoom == region;
                DrawNpc(o.kind, new Point(o.x, o.y), 11 + region);
                Color accent = done ? mint : active ? PixelArt.C("#f3a57a") : gold;
                Ring(new Point(o.x, o.y), .38f, accent, 16);
                Text(p.x - 44, p.y - 91, 88, 24, done ? "✓" :
                    active ? Game.L("保护！", "DEFEND!") : "!", 15, accent, TextAnchor.MiddleCenter);
                if (active)
                {
                    Color safety = Game.WayfarerIntegrity <= 35 ? PixelArt.C("#f27d7d") : mint;
                    // Readable beside the actual NPC, without a full-screen HUD.
                    Bar(p.x - 35, p.y + 11, 70, Game.WayfarerIntegrity / 100f, safety, 6);
                    Text(p.x - 42, p.y + 17, 84, 18, Game.L("安全 ", "SAFE ") +
                        (int)Math.Ceiling(Game.WayfarerIntegrity) + "%", 10, safety, TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (o.kind.StartsWith("survey"))
            {
                bool desk = o.kind == "surveyDesk";
                bool found = desk ? Game.Has("survey_complete") : Game.Has("survey_" + o.kind.Substring(6));
                bool defending = !desk && Game.RoadwardenTrialRoom == Game.save.room;
                bool defended = !desk && Game.Has("road_trial_" + o.kind.Substring(6));
                Color accent = defending ? (Game.RoadwardenTrialVeteran ? PixelArt.C("#b9a4ed") : PixelArt.C("#e69d83")) : defended ? gold : found ? mint : gold;
                if(defending)
                    DrawAuthoredRoadwardenWard(p,Game.RoadwardenWardIntegrity,
                        Game.RoadwardenWardUnderAttack,Game.save.room);
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
        // Roadwarden ward pressure is no longer an invisible drain hidden
        // behind a distant HUD. Link precisely the GameCore-confirmed
        // siphoning enemies to the world beacon, using the existing lean
        // pixel-line language. This is not an independently guessed enemy
        // proximity query: a real stun, death or stone wall instantly retires
        // its pressure and next simulation tick retires its visible tether.
        // In reduced-motion/low-FX modes the fixed line remains legible;
        // only the little traveling highlight is removed.
        private void DrawRoadwardenWardSiphons()
        {
            if(Game == null || Game.mode != "world" ||
                Game.RoadwardenTrialRoom == 0 ||
                Game.RoadwardenTrialRoom != Game.save.room ||
                drawingOutline)return;
            Point ward=Game.RoadwardenWardSiphonTarget;
            int count=Game.RoadwardenWardSiphonCount;
            for(int i=0;i<count;i++)
            {
                Point source=Game.RoadwardenWardSiphonSource(i);
                Point delta=ward-source;
                if(delta.Length<.01f || delta.Length>4f)continue;
                Color tether=PixelArt.C(Game.RoadwardenTrialVeteran ?
                    "#bc9ced" : "#efab73");
                tether.a=.64f;
                DrawRay(source,ward,tether);
                if(Game.save.reducedMotion||Game.save.effectsQuality==0)
                    continue;
                float phase=clock*1.7f+i*.37f;
                phase-=(float)Math.Floor(phase);
                Point glint=ScreenPoint(source+delta*phase);
                Box(glint.x-3f,glint.y-3f,6f,6f,
                    PixelArt.C("#ffe6b2"));
            }
        }
        private void DrawPolishWorld()
        {
            if (Game.mode != "world") return;
            if (Game.Dying) { Panel(426, 536, 428, 77, panel, gold); Text(442, 551, 395, 46, Game.L("你倒下了 · 镇民正在带你回家", "You fell · your neighbors are bringing you home"), 18, paper, TextAnchor.MiddleCenter); return; }
            Enemy near = Game.enemies.Find(e => e.state == 1 && (e.Position - Game.Position).Length < 7);
            if (near != null && !Game.practicing) { Panel(24, 228, 298, 90, new Color(.06f, .08f, .13f, .88f), PixelArt.C("#b37c6a")); Text(40, 241, 266, 68, Game.EnemyHint(near), 13, paper, TextAnchor.MiddleCenter); }
            if (Game.practicing) { Panel(348, 190, 584, 104, panel, mint); Text(365, 206, 549, 34, Game.L("安全练习 ", "Safe practice ") + (Game.practiceStage + 1) + "/3", 20, gold); Text(365, 250, 390, 31, PracticeInputHint, 16, paper); if (Button(785, 246, 130, 34, Game.L("结束练习", "End practice"))) Game.EndPractice(); }
            if (resumeTime > 0 && !Game.practicing) { Panel(352, 205, 576, 98, panel, gold); Text(369, 219, 542, 27, Game.L("欢迎回来 · 接下来", "Welcome back · next step"), 19, gold); Text(369, 255, 542, 40, resumeBrief, 14, paper); }
            // Keep the unmarked escape bearing legible even in reduced-motion
            // mode, using a small in-world ring rather than another HUD panel.
            if (Game.AtlasEchoRoom == Game.save.room && Game.AtlasEchoOpenTime > 0)
            {
                Point openBearing = Game.AtlasEchoOpenBearing;
                Point screenBearing = ScreenPoint(openBearing);
                Ring(openBearing, .55f, mint, 18);
                Text(screenBearing.x - 48, screenBearing.y - 57, 96, 25,
                    Game.L("空方位", "OPEN BEARING"), 11, mint, TextAnchor.MiddleCenter);
            }
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
