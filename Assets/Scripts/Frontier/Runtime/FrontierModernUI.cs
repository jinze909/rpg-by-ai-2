using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int selectedItem, selectedQuest, selectedRegion = -1, helpTopic, dialoguePage, recordsPage, questPage;
        private Conversation lastConversation;
        private readonly string[] menuModes = { "inventory", "loadout", "talents", "companions", "journal", "map", "camp", "bounties", "help" };
        private readonly string[] menuZh = { "冒险背包", "遗物配装", "职业成长", "同行伙伴", "任务手帐", "世界地图", "城镇建设", "公会悬赏", "冒险指南" };
        private readonly string[] menuEn = { "Inventory", "Relics", "Talents", "Companions", "Journal", "World map", "Town projects", "Contracts", "Field guide" };
        private readonly string[] menuKeys = { "I", "G", "K", "C", "TAB", "M", "B", "N", "F1" };
        private void Panel(float x, float y, float w, float h, Color fill, Color edge)
        {
            Rect rect = new Rect(x,y,w,h);
            // Arrival, practice, return-to-game and enemy tells are HUD too,
            // even though they are emitted by the world presentation pass.
            if (Game.mode == "world" && !drawingHud) worldOverlayRegions.Add(rect);
            bool occluded = (drawingHud || Game.mode == "world") && ThreatOver(rect);
            if (occluded) { fill.a = Math.Min(fill.a,.12f); edge.a = .22f; }
            Box(x + 3, y + 5, w, h, new Color(.015f, .025f, .05f, occluded ? .025f : .24f)); Box(x, y, w, h, fill); Frame(x, y, w, h, edge); Box(x + 2, y + 2, w - 4, 1, new Color(.9f, .95f, 1, .035f));
        }
        private void Card(float x, float y, float w, float h, bool selected = false)
        { Panel(x, y, w, h, selected ? PixelArt.C("#293e4e") : PixelArt.C("#1c2e42"), selected ? gold : PixelArt.C("#3a4b60")); }
        private void Icon(string id, float x, float y, float size = 36, Color? tint = null)
        {
            int index = id == "sword" ? 0 : id == "bow" ? 1 : id == "staff" ? 2 : id == "shield" ? 3 : id == "wood" ? 4 : id == "crystal" ? 5 : id == "herb" ? 6 : id == "fish" ? 7 : id == "gear" ? 8 : id == "relic" ? 9 : id == "stew" ? 10 : id == "seed" ? 11 : id == "companion" ? 12 : id == "camp" ? 13 : id == "journal" || id == "map" ? 14 : id == "tea" ? 15 : -1;
            // Q sword/bow/staff and F stew use the detailed Icons.png.
            // If the atlas is temporarily missing, NEVER invent a crude
            // procedural 48px replacement for an existing authored icon.
            if (index >= 0)
            {
                if (iconSheet != null)
                    Atlas(iconSheet, 4, 4, index,
                        new Rect(x, y, size, size), tint ?? Color.white);
                else
                {
                    Frame(x, y, size, size, muted);
                    Text(x, y + size * .25f, size, size * .5f,
                        id, (int)Math.Max(9f, size * .18f), tint ?? paper,
                        TextAnchor.MiddleCenter);
                }
                return;
            }
            Image(art.Icon(id), x, y, size, size, tint);
        }
        private void Portrait(int id, float x, float y, float w, float h)
        {
            Box(x, y, w, h, PixelArt.C("#243b4c"));
            if (id >= 0 && id < 3 && playerPortraits != null) { float side = Math.Min(w, h); Atlas(playerPortraits, 3, 1, id, new Rect(x + (w - side) / 2, y + (h - side) / 2, side, side), Color.white); return; }
            // The player can speak to the moving survivor. Reuse her exact
            // in-world atlas identity in dialogue instead of a journal icon
            // or an invented second playable character portrait.
            if (id == 8 && npcSheet != null)
            {
                float survivorSize = Math.Min(w, h);
                Atlas(npcSheet, 4, 4, 14,
                    new Rect(x + (w - survivorSize) / 2, y + h - survivorSize,
                        survivorSize, survivorSize), Color.white);
                return;
            }
            if (id < 0 || portraits == null) { Icon(id < 0 ? "journal" : "companion", x + w * .2f, y + h * .25f, w * .6f); return; }
            float size = Math.Min(w, h); id = Math.Max(0, Math.Min(7, id)); GUI.DrawTextureWithTexCoords(new Rect(x + (w - size) / 2, y + h - size, size, size), portraits, new Rect(id % 4 / 4f, id < 4 ? .5f : 0, .25f, .5f));
        }
        private void OpenMenu(string mode)
        {
            // A downed player must finish the mandatory recovery animation.
            // Opening inventory, pause or journal mid-death stops the world
            // clock and previously stranded the player at zero health.
            if (Game == null || Game.Dying) return;
            if (mode == "pause" && Game.mode != "title") settingsParent = "world";
            if (mode == "saves") RefreshSlots();
            Game.mode = mode;
            if (mode == "inventory") Game.TutorialAction(6);
        }
        private void Dim() { Box(0, 0, Width, Height, new Color(.02f, .035f, .07f, .87f)); }
        private void Bar(float x, float y, float w, float value, Color color, float h = 6)
        { Box(x, y, w, h, PixelArt.C("#0d1c2c")); Box(x, y, w * Mathf.Clamp01(value), h, color); Box(x, y, w * Mathf.Clamp01(value), 1, Color.Lerp(color, paper, .3f)); }
        private void Window(string title, string subtitle = "")
        {
            Dim(); Panel(24, 24, 1232, 752, new Color(.065f, .105f, .17f, .98f), PixelArt.C("#45566b"));
            Text(48, 50, 190, 32, "STARFALL", 22, gold); Text(49, 87, 170, 27, Game.L("远岸来信", "DISTANT SHORES"), 12, muted);
            Box(246, 46, 1, 671, PixelArt.C("#34475a")); Text(276, 48, 735, 52, title, 31, paper); Text(278, 106, 738, 43, subtitle, 14, muted); Box(275, 150, 946, 1, PixelArt.C("#374c60"));
            if (Game.mode == "inventory" || Game.mode == "loadout") DrawPlayerPreview(Game.mode == "inventory" ? inspectedWeapon : Game.save.weapon, 1086, 137, 104);
            bool titleSettings = settingsParent == "title" && (Game.mode == "pause" || Game.mode == "saves");
            for (int i = 0; i < menuModes.Length; i++) if (Button(44, 160 + i * 53, 180, 42, Game.L(menuZh[i], menuEn[i]) + "  " + menuKeys[i], Game.mode == menuModes[i], !titleSettings)) OpenMenu(menuModes[i]);
            Text(48, 649, 166, 43, "LV " + Game.save.level + "  /  " + Game.save.coins + Game.L(" 硬币", " coins") + "\n" + Game.L("记忆碎片 ", "Shards ") + Game.save.shards, 13, muted);
            if (Button(44, 696, 180, 25, Game.L("存档管理", "Save manager"))) OpenMenu("saves");
            if (Button(44, 726, 180, 32, Game.L("返回  ", "Back  ") + ControlKey("ESC", "B"), true)) Back(); if (Button(1156, 52, 68, 38, ControlKey("ESC", "B"))) Back();
            Text(278, 733, 852, 24, MenuInputHint, 12, muted);
        }
        private string WeaponName(int id) { return Game.L(new[] { "短剑", "猎弓", "星火杖" }[id], new[] { "Sword", "Bow", "Starfire staff" }[id]); }
        private string SkillName()
        { return Game.L(Game.save.technique == 0 ? new[] { "护盾震波", "扇形箭雨", "星火新星" }[Game.save.profession] : new[] { "震地反击", "穿林一矢", "寒星领域" }[Game.save.profession], Game.save.technique == 0 ? new[] { "Shield pulse", "Arrow fan", "Star nova" }[Game.save.profession] : new[] { "Seismic counter", "Woodpiercer", "Frost field" }[Game.save.profession]); }
        private void DrawTitle()
        {
            for (int i = 0; i < 20; i++) Box(i * 32, 0, 32, Height, new Color(.025f, .06f, .11f, .82f - i * .035f));
            Text(64, 79, 620, 32, "STARFALL FRONTIER", 18, gold); Text(59, 123, 610, 101, Game.L("星陨边境", "STARFALL\nFRONTIER"), Game.save.english ? 43 : 68, paper);
            Box(64, 242, 64, 3, gold); Text(64, 266, 540, 91, Game.L("远岸来信\n把一封未寄出的信，带到世界的另一端。", "LETTERS FROM AFAR\nCarry an unsent letter to the edge of the world."), 18, muted);
            for (int i = 0; i < 3; i++) { float x = 64 + i * 176; Card(x, 382, 164, 123, selectedClass == i); Portrait(i, x + 46, 390, 72, 54); if (Button(x + 10, 452, 144, 39, Game.L(new[] { "先锋", "游侠", "星术师" }[i], new[] { "Vanguard", "Ranger", "Arcanist" }[i]), selectedClass == i)) selectedClass = i; }
            string[] roles = { Game.L("先锋 · 高生命，近身连击与护盾反击", "Vanguard · high health, combos and shield counters"), Game.L("游侠 · 远程游走，扇形箭雨与贯穿箭", "Ranger · ranged kiting, arrow fans and piercing shots"), Game.L("星术师 · 范围爆发，寒星控制与快速回复", "Arcanist · area bursts, frost control and fast recovery") }; Text(64, 523, 537, 50, roles[selectedClass], 15, paper);
            if (Button(64, 595, 252, 56, Game.L("开始新的冒险", "New adventure"), true)) { if (hasSave) replacePrompt = true; else BeginAdventure(); }
            if (Button(332, 595, 252, 56, Game.L("继续旅程", "Continue"), false, hasSave)) { Game.Start(); camera = CameraTarget(); resumeBrief = Game.GuidanceHint(); resumeTime = 8; }
            if (Button(332, 671, 252, 38, Game.L("设置与辅助选项", "Settings & accessibility"))) { settingsParent = "title"; Game.mode = "pause"; }
            if (Button(64, 671, 252, 38, Game.L("分步教学：", "Guided onboarding: ") + (Game.save.showGuidance ? "ON" : "OFF"))) Game.save.showGuidance = !Game.save.showGuidance;
            if (Button(1047, 32, 177, 36, Game.save.english ? "中文" : "ENGLISH")) Game.save.english = !Game.save.english;
            Text(66, 748, 950, 24, Game.L("九个地区 · 三位同行者 · 你的选择改变边境", "Nine regions · three companions · your choices shape the frontier"), 13, muted);
            if (replacePrompt) { drawingModal = true; drawnControls.Clear(); Dim(); Card(385, 282, 510, 235); Text(414, 310, 447, 43, Game.L("开启另一段旅程", "A new beginning"), 27, gold); Text(414, 370, 447, 58, Game.L("开始新冒险会替换自动存档，手动位置保留。", "Replaces the autosave. Your manual slots are retained."), 18, paper); if (Button(414, 447, 214, 43, Game.L("开始新冒险", "Start new"), true)) BeginAdventure(); if (Button(647, 447, 214, 43, Game.L("取消", "Cancel"))) replacePrompt = false; }
        }
        private void BeginAdventure() { Game.NewGame(selectedClass); camera = CameraTarget(); Save(); replacePrompt = false; }
        private void DrawExplorationHud()
        {
            bool world = Game.mode == "world";
            Panel(24, 24, 298, 104, new Color(.045f, .08f, .13f, .9f), PixelArt.C("#456074")); Portrait(Game.AppearanceWeapon, 34, 34, 60, 75);
            Text(105, 35, 200, 27, Game.L(new[] { "先锋", "游侠", "星术师" }[Game.save.profession], new[] { "Vanguard", "Ranger", "Arcanist" }[Game.save.profession]) + "  LV " + Game.save.level, 16, paper);
            Text(105, 67, 197, 21, (int)Game.save.health + " / " + (int)Game.save.maxHealth + Game.L(" · 耐力 ", " · EN ") + (int)Game.save.energy, 13, paper); Bar(105, 92, 196, Game.save.health / Game.save.maxHealth, PixelArt.C("#d28b86"), 7); Bar(105, 106, 196, Game.save.energy / 100, mint, 4);
            Text(406, 28, 468, 36, Game.L(Game.Map.name, Game.Map.en), 24, paper, TextAnchor.MiddleCenter); Text(412, 69, 456, 25, Game.L(Game.Map.subtitle, Game.Map.suben), 12, gold, TextAnchor.MiddleCenter);
            if (world)
            {
                DrawMinimap(); QuestGuide q = Game.CivicConduitActive ? Game.GetQuest("civic_conduit") : Game.LastDirectiveWave==1 || Game.LastDirectiveWave==3 ? Game.GetQuest("last_directive") : Game.FarCourierActive ? Game.GetQuest("far_couriers") :
                    Game.CivicResponseActive ? Game.GetQuest("civic_services") : Game.CurrentGuide; Panel(1008, 233, 248, 209, new Color(.045f, .08f, .13f, .88f), PixelArt.C("#405970"));
                Text(1025, 247, 214, 22, Game.practicing ? Game.L("实战教学", "COMBAT PRACTICE") : q.category + "  /  " + ControlKey("TAB", "VIEW"), 11, gold); Text(1025, 277, 214, 48, Game.practicing ? Game.L(new[] { "命中目标", "观察与闪避", "职业战技" }[Game.practiceStage], new[] { "Hit the target", "Read & evade", "Class technique" }[Game.practiceStage]) : q.title, 17, paper);
                Text(1025, 334, 214, 65, Game.practicing ? PracticeInputHint : Game.InAbyss ? Game.Objective() : TideBlocksGuide ? Game.L("潮水阻路：等待退潮，或开启控潮闸门。", "Tide blocks the route. Wait for ebb or use the sluice.") : Game.FarCourierActive ? q.next : Game.GuidanceHint(), 13, muted); Bar(1025, 422, 214, Game.practicing ? Game.practiceStage / 3f : q.Progress, gold, 3);
                if (!Game.practicing && Button(1015, 448, 234, 33, Game.save.showGuidance ? Game.L("路线指引 ON", "Route guide ON") : Game.L("路线指引 OFF", "Route guide OFF"))) { Game.save.showGuidance = !Game.save.showGuidance; Game.revision++; }
                if (Game.save.room >= 5 || Game.CivicConduitActive || Game.LastDirectiveWave==1 || Game.LastDirectiveWave==3 || Game.FarCourierActive || Game.CivicResponseActive || Game.WitnessDefenseRoom!=0 || Game.StormTuning || Game.BlackoutActive || Game.TemperActive || Game.TriadDefenseActive || Game.TriadBossInterruptReady || Game.save.teaUntil > Game.save.totalTime) { Panel(24, 145, 298, 60, new Color(.045f, .08f, .13f, .85f), PixelArt.C("#405970")); Text(39, 156, 271, 42, Game.Conditions(), 13, muted); }
                DrawTutorialPrompt(); Panel(342, 696, 596, 80, new Color(.045f, .08f, .13f, .95f), PixelArt.C("#4d6478"));
                ActionSlot(354, ControlKey("Q", "Y"), SkillName(), Game.save.profession == 0 ? "sword" : Game.save.profession == 1 ? "bow" : "staff", Game.skillCooldown, Game.save.energy >= (Game.Relic(4) ? 50 : 40), () => Game.Skill(InputAim(true)), 3.5f, Game.L("耐力不足", "Low energy"));
                ActionSlot(470, ControlKey("F", "LB"), Game.L("炖汤", "Stew") + " " + Game.save.inventory[6], "stew", 0, Game.save.inventory[6] > 0 && !Game.practicing, () => Game.Eat());
                ActionSlot(586, ControlKey("T", "PACK"), Game.L("凉茶", "Tea") + " " + Game.save.tea, "tea", 0, Game.save.tea > 0 && !Game.practicing, () => Game.DrinkTea());
                ActionSlot(702, ControlKey("Z", "RB"), Game.L("同行指令", "Command"), "companion", Game.companionCooldown, Game.save.companion >= 0 && !Game.practicing, () => Game.CommandCompanion(), Game.save.frontierEnding == 3 ? 12 : 18);
                ActionSlot(818, ControlKey("SPACE", "A"), Game.L("闪避", "Dodge"), "shield", Game.dashCooldown, Game.save.energy >= (Game.Relic(2) ? 25 : 20), () => Game.Dash(Game.aim), .75f - Game.save.talents[2] * .06f, Game.L("耐力不足", "Low energy"));
                Text(25, 725, 302, 24, Game.save.coins + Game.L(" 硬币  ·  ", " coins  ·  ") + WeaponName(Game.save.weapon), 13, paper); Text(25, 752, 302, 23, controllerUsed ? Game.L("X 攻击 · B 互动 · 凉茶在背包使用", "X attack · B interact · tea from inventory") : Game.L("J / 左键攻击 · E 互动", "J / click attack · E interact"), 12, muted);
                if (Button(972, 718, 130, 52, Game.L("手帐  ", "Journal  ") + ControlKey("TAB", "VIEW"))) OpenMenu("journal"); if (Button(1114, 718, 142, 52, Game.L("菜单  ", "Menu  ") + ControlKey("ESC", "START"))) { OpenMenu("pause"); Save(); }
            }
            Enemy boss = Game.enemies.Find(e => e.boss); if (boss != null && world) { Panel(383, 612, 514, 64, new Color(.04f, .065f, .11f, .93f), PixelArt.C("#7b666d")); Text(403, 622, 474, 28, Game.L(Game.CurrentBoss.name, Game.CurrentBoss.en) + "  /  " + boss.BossPhase, 16, gold, TextAnchor.MiddleCenter); Bar(403, 656, 474, boss.hp / boss.maxHp, PixelArt.C("#d28b86"), 5); Bar(403, 666, 474, boss.stagger / 95, mint, 3); }
            else if (world && Game.fallen.Exists(f => f.actor.boss)) { Panel(383, 612, 514, 64, new Color(.04f, .065f, .11f, .93f), gold); Text(403, 622, 474, 28, Game.L("守护者已倒下", "Guardian defeated"), 20, gold, TextAnchor.MiddleCenter); Text(403, 653, 474, 23, Game.L("战利品已收取 · 前往祭坛继续旅程", "Loot secured · continue at the altar"), 13, paper, TextAnchor.MiddleCenter); }
        }
        private void ActionSlot(float x, string key, string label, string icon, float cooldown, bool resourceAvailable, Action act, float duration = 1, string unavailable = null, bool busy = false)
        {
            bool ready = resourceAvailable && cooldown <= 0 && !busy && !Game.Dying;
            bool compact = CombatHud; float y = compact ? 732 : 699, w = compact ? 78 : 109, h = compact ? 50 : 75;
            bool hit = Button(x, y, w, h, "", false, ready);
            if (ready) { Frame(x,y,w,h,mint); Box(x+4,y+h-3,w-8,2,mint); }
            float iconX = x + (compact ? key == "SPACE" ? 52 : 41 : key == "SPACE" ? 60 : 39);
            Icon(icon,iconX,y+(compact?4:5),compact?23:31,ready?Color.white:new Color(.62f,.68f,.74f));
            string state = cooldown > 0 ? Math.Ceiling(cooldown)+"s" : busy ? Game.L("动作中","In action") : !resourceAvailable&&unavailable!=null ? unavailable : label;
            Text(x+3,y+(compact?26:41),w-6,compact?19:27,state,compact?10:11,ready?paper:muted,TextAnchor.MiddleCenter);
            Text(x+5,y+6,key=="SPACE"?45:32,19,key,compact?9:10,ready?gold:muted);
            if(cooldown>0) Bar(x+4,y+h-3,w-8,1-cooldown/Math.Max(.01f,duration),gold,2);
            if(hit) act();
        }
        private void DrawMinimap() { Panel(1008, 24, 248, 191, new Color(.045f, .08f, .13f, .94f), PixelArt.C("#486073")); MiniTerrainWindow(1024, 42); Text(1024, 191, 166, 21, Game.Night ? Game.L("夜晚", "NIGHT") : Game.L("白昼", "DAY"), 11, gold); if (Button(1190, 184, 48, 25, "M")) OpenMenu("map"); }
        // The HUD shows a readable, player-centered 24x15-tile neighborhood.
        // M still opens the separately scaled full-world strategic map.
        private void MiniTerrainWindow(float x0, float y0)
        {
            const int cellsX=24, cellsY=15;
            const float cell=9f;
            int width=Game.Map.rows[0].Length,height=Game.Map.rows.Length;
            int left=Math.Max(0,Math.Min(width-cellsX,(int)Game.save.x-cellsX/2));
            int top=Math.Max(0,Math.Min(height-cellsY,(int)Game.save.y-cellsY/2));
            Color low=PixelArt.C(Game.Map.color), water=PixelArt.C("#315c76"),
                stone=PixelArt.C("#566879"),road=PixelArt.C("#a09877");
            for(int dy=0;dy<cellsY;dy++)for(int dx=0;dx<cellsX;dx++)
            {
                int x=left+dx,y=top+dy;
                if(x<0||x>=width||y<0||y>=height)continue;
                char tile=TerrainPreviewGlyph(Game.Map,Game.save.room,x,y);
                Color c=tile=='~'?water:
                    tile=='#'?stone:tile=='='?road:low;
                Box(x0+dx*cell,y0+dy*cell,cell,cell,
                    Game.Explored(Game.save.room,x,y)?c:Color.Lerp(c,ink,.68f));
            }
            foreach(WorldObject o in Game.Map.objects)
            {
                if(o.x<left||o.x>=left+cellsX||o.y<top||o.y>=top+cellsY||
                    !Game.IsObjectPresent(o))continue;
                if(o.kind.StartsWith("portal")||Game.Explored(Game.save.room,o.x,o.y))
                    Box(x0+(o.x-left)*cell-2,y0+(o.y-top)*cell-2,5,5,
                        o.kind.StartsWith("portal")?mint:gold);
            }
            Point? goal=Game.NavigationTarget();
            if(goal.HasValue&&Game.save.showGuidance&&goal.Value.x>=left&&goal.Value.x<left+cellsX&&
                goal.Value.y>=top&&goal.Value.y<top+cellsY)
                Box(x0+(goal.Value.x-left)*cell-3,y0+(goal.Value.y-top)*cell-3,7,7,gold);
            Box(x0+(Game.save.x-left)*cell-3,y0+(Game.save.y-top)*cell-3,6,6,paper);
        }
        private void MiniTerrain(float x0, float y0, float size)
        {
            DrawTerrainPreview(Game.Map,Game.save.room,true,x0,y0,size);
            foreach (var o in Game.Map.objects) if (Game.IsObjectPresent(o) && (o.kind.StartsWith("portal") || Game.Explored(Game.save.room, o.x, o.y))) Box(x0 + o.x * size - 1, y0 + o.y * size - 1, 3, 3, o.kind.StartsWith("portal") ? mint : gold);
            Point? goal = Game.NavigationTarget(); if (goal.HasValue && Game.save.showGuidance) { Box(x0 + goal.Value.x * size - 3, y0 + goal.Value.y * size - 3, 7, 7, gold); Box(x0 + goal.Value.x * size - 1, y0 + goal.Value.y * size - 1, 3, 3, ink); } Box(x0 + Game.save.x * size - 2, y0 + Game.save.y * size - 2, 5, 5, paper);
        }
        private void DrawInventory()
        {
            Window(Game.L("冒险背包", "Inventory"), Game.L("查看物品 · 比较武器与打法 · 确认后装备", "Inspect items · compare weapons & styles · confirm to equip")); string[] ids = { "wood", "crystal", "herb", "fish", "gear", "relic", "stew", "seed" };
            for (int i = 0; i < 8; i++) { float x = 279 + i % 4 * 158, y = 177 + i / 4 * 126; Card(x, y, 145, 113, selectedItem == i); Icon(ids[i], x + 47, y + 7, 46); if (Button(x + 7, y + 59, 131, 46, Game.L(Game.data.items[i].name, Game.data.items[i].en) + "\n× " + Game.save.inventory[i], selectedItem == i)) selectedItem = i; }
            DrawWeaponComparison();
            Card(924, 177, 295, 512); Icon(ids[selectedItem], 1030, 203, 82); Text(947, 306, 250, 41, Game.L(Game.data.items[selectedItem].name, Game.data.items[selectedItem].en), 23, gold);
            string[] zh = { "建设与制作的基础材料。探索木堆，或向铁匠购买。", "用于武器、修理与记忆锻印。采集星晶矿获得。", "用于料理、救援与镇民委托。药田种植与野外采集获得。", "镇东湖边钓获。料理与老潮的委托需要它。", "矿点会提供备用零件。修理机关、制作武器与城镇建设需要它。", "冒险背包中的星核碎片。远岸记忆碎片与硬币另有独立余额，可在侧栏查看。", "恢复55生命与全部耐力，攻击增强90秒。按F使用。", "到杏的药田种植。生长60秒，成熟后收获药草与新种子。" };
            string[] en = { "A building and crafting material. Gather timber or buy it from Noll.", "For weapons, repairs and relic forging. Gather starcrystal veins.", "For cooking, rescues and town requests. Grow or gather Moonherbs.", "Catch these in the eastern lake. Used for cooking and Old Tide's request.", "Ore veins yield spare parts. Used in repairs, weapons and town projects.", "Starcore shards in your pack. Distant-shores memory shards and coins are separate balances in the sidebar.", "Restores 55 health and full energy; boosts damage for 90 seconds. Press F.", "Plant in An's garden. Harvest after 60 seconds for herbs and new seeds." };
            Text(947, 369, 246, 152, Game.L(zh[selectedItem], en[selectedItem]), 17, muted);
            if (Button(947, 546, 246, 35, Game.L("使用炖汤 × ", "Use stew × ") + Game.save.inventory[6], false, Game.save.inventory[6] > 0 && !Game.practicing)) Game.Eat();
            if (Button(947, 590, 246, 35, Game.L("使用凉茶 × ", "Use tea × ") + Game.save.tea, false, Game.save.tea > 0 && !Game.practicing)) Game.DrinkTea();
            Text(947, 642, 246, 40, Game.L("旅店休息可更新野外采集点。", "Inn rest renews resource nodes."), 12, muted);
        }
        private void DrawTalents()
        {
            Window(Game.L("职业成长", "Talents & techniques"), "LV " + Game.save.level + "  ·  XP " + (Game.save.level >= 30 ? Game.L("满级", "MAX") : Game.save.xp + "/" + (Game.save.level * 40)) + "  ·  " + Game.L("可用技能点 ", "Available points ") + Game.save.skillPoints);
            string[] names = { Game.L("锋芒", "Edge"), Game.L("坚韧", "Resolve"), Game.L("轻盈", "Agility") }, icons = { "sword", "shield", "bow" };
            string[] info = { Game.L("每级攻击+3\n暴击率+2.5%", "+3 damage per rank\n+2.5% critical chance"), Game.L("每级生命+12\n受到伤害-3.5%", "+12 health per rank\n-3.5% incoming damage"), Game.L("提高移动速度\n缩短闪避冷却", "Faster movement\nShorter dodge cooldown") };
            for (int i = 0; i < 3; i++) { float x = 279 + i * 318; Card(x, 178, 302, 338); Icon(icons[i], x + 111, 194, 80); Text(x + 23, 288, 256, 40, names[i], 27, gold, TextAnchor.MiddleCenter); Text(x + 23, 350, 256, 76, info[i], 17, muted, TextAnchor.MiddleCenter); for (int n = 0; n < 5; n++) Box(x + 62 + n * 37, 442, 26, 5, n < Game.save.talents[i] ? gold : PixelArt.C("#43566b")); if (Button(x + 18, 475, 266, 29, Game.L("投入1技能点", "Spend 1 point"), false, Game.save.skillPoints > 0 && Game.save.talents[i] < 5)) Game.Learn(i); }
            int invested = Game.save.talents[0] + Game.save.talents[1] + Game.save.talents[2];
            bool refundOverflow = invested > 0 && Game.save.skillPoints > 99 - invested;
            string respecLabel = refundOverflow ?
                Game.L("技能点将超出99点上限，请先分配技能点", "Refund exceeds 99-point cap. Spend points first") :
                Game.L("重置三项天赋：20硬币，返还全部投入点数", "Reset talents: 20 coins; refunds all invested points");
            if (Button(279, 704, 938, 25, respecLabel, false, Game.CanResetTalents)) Game.ResetTalents();
            Card(279, 540, 938, 150); Text(302, 560, 885, 35, Game.L("战技专精", "Combat technique"), 22, gold); Text(302, 603, 883, 28, Game.L("当前 Q：", "Current Q: ") + SkillName() + "  /  " + Game.L("熟练度等级 ", "Mastery rank ") + Game.MasteryRank(Game.save.weapon), 16, paper);
            bool learned = Game.Has("technique_" + Game.save.profession); if (Button(302, 646, 891, 31, learned ? Game.L("切换为另一种职业战技", "Switch to the other class technique") : Game.L("学习第二战技：等级5、技能点1", "Learn alternate technique: level 5, 1 talent point"), Game.save.technique == 1, learned || Game.save.level >= 5 && Game.save.skillPoints > 0)) Game.LearnTechnique();
        }
        private void DrawCompanions()
        {
            Window(Game.L("同行伙伴", "Companions"), Game.L("一次邀请一人 · 自动支援与 Z 战术指令 · 羁绊强化效果", "One active companion · automatic support and Z commands · stronger bonds"));
            string[] info = { Game.L("低生命时治疗。\nZ：治疗、耐力与护盾。", "Heals you when hurt.\nZ: healing, energy and guard."), Game.L("远程射击支援。\nZ：三发贯穿箭。", "Ranged support shots.\nZ: three piercing arrows."), Game.L("星火支援与冻结。\nZ：冻结与寒星领域。", "Starfire and frost support.\nZ: freezing and a frost field.") };
            for (int i = 0; i < 3; i++)
            {
                float x = 279 + i * 318; bool owned = Game.Has("companion_" + i); Card(x, 177, 302, 449, Game.save.companion == i); Portrait(i + 3, x + 57, 192, 188, 182);
                Text(x + 20, 385, 262, 35, Game.L(GameCore.CompanionNames[i], GameCore.CompanionNamesEn[i]), 25, gold, TextAnchor.MiddleCenter); Text(x + 20, 430, 262, 62, owned ? info[i] : Game.L("完成所在地区调查后加入。", "Joins after the regional report."), 15, muted, TextAnchor.MiddleCenter);
                for (int n = 0; n < 5; n++) Box(x + 72 + n * 34, 510, 20, 4, n < Game.save.relationships[i] ? mint : PixelArt.C("#43566b"));
                if (Button(x + 17, 536, 268, 34, Game.L("邀请同行", "Travel together"), Game.save.companion == i, owned && Game.SafeTravel)) Game.SelectCompanion(i);
                if (Button(x + 17, 583, 268, 32, Game.L("个人故事与赠礼", "Story & shared meal"), false, owned)) { dialogParent = "companions"; Game.CompanionStory(i); }
            }
            if (Button(279, 652, 938, 38, Game.L("独自旅行", "Travel alone"), Game.save.companion < 0, Game.SafeTravel)) Game.SelectCompanion(-1);
        }
        private void DrawLoadout()
        {
            Window(Game.L("遗物与记忆锻印", "Relics & memory forging"), Game.L("两个遗物槽 · 同一遗物不能重复装备 · 锻印 ", "Two slots · no duplicates · forge tier ") + Game.save.relicRank + "/3");
            for (int slot = 0; slot < 2; slot++) { int id = Game.save.relics[slot]; if (Button(279 + slot * 477, 178, 461, 43, Game.L("槽 ", "Slot ") + (slot + 1) + " · " + (id < 0 ? Game.L("空", "Empty") : Game.L(GameCore.RelicNames[id], GameCore.RelicNamesEn[id])), relicSlot == slot)) relicSlot = slot; }
            for (int i = 0; i < 6; i++)
            {
                float x = 279 + i % 3 * 318, y = 244 + i / 3 * 174; bool owned = Game.Has("relic_" + i); Card(x, y, 302, 157, Game.Relic(i)); Icon(i == 0 ? "tea" : i == 1 ? "seed" : i == 2 ? "map" : i == 3 ? "gear" : i == 4 ? "shield" : "relic", x + 17, y + 16, 39);
                Text(x + 67, y + 21, 218, 32, Game.L(GameCore.RelicNames[i], GameCore.RelicNamesEn[i]), 18, owned ? gold : muted); Text(x + 17, y + 67, 269, 50, Game.L(GameCore.RelicText[i], GameCore.RelicTextEn[i]), 13, muted);
                if (Button(x + 17, y + 120, 268, 27, owned ? Game.L("装备到当前槽", "Equip in selected slot") : Game.L("尚未解锁", "Not discovered"), Game.Relic(i), owned)) Game.EquipRelic(relicSlot, i);
            }
            if (Button(279, 614, 302, 39, Game.L("卸下当前槽", "Clear selected slot"))) Game.EquipRelic(relicSlot, -1);
            if (Button(597, 614, 620, 39, Game.L("锻印+1 · 碎片", "Refine +1 · shards ") + ((Game.save.relicRank + 1) * 8) + Game.L(" / 星晶2", " / 2 crystals"), true, Game.save.room == 0 && Game.save.projects[2] > 0 && Game.save.relicRank < 3)) Game.RefineRelics();
            Text(282, 666, 925, 26, Game.L("装备遗物时，每级锻印攻击+2。一级记忆工坊另收20硬币。", "+2 damage per forge tier while wearing a relic. Tier I workshop charges 20 coins."), 13, muted);
        }
        private void DrawCamp()
        {
            Window(Game.L("城镇建设", "Town projects"), Game.L("在余烬镇施工 · 建设也会提高山野议会声望", "Build in Emberfall · projects also earn Wild Assembly trust"));
            string[] names = { Game.L("水渠与药田", "Canals & gardens"), Game.L("远行驿站", "Waystation"), Game.L("记忆工坊", "Memory workshop") }, ids = { "seed", "camp", "gear" };
            string[] info = { Game.L("每级收成+2药草。\n二级赠送琥珀种遗物。", "+2 harvested herbs per tier.\nTier II grants Amber Seed."), Game.L("一级开放野外营地休整。\n二级每天赠一杯凉茶。", "Tier I enables field-camp rest.\nTier II adds daily camp tea."), Game.L("一级开放遗物锻印。\n二级免除锻印硬币费。", "Tier I enables relic refining.\nTier II removes its coin fee.") };
            for (int i = 0; i < 3; i++)
            {
                float x = 279 + i * 318; int tier = Game.save.projects[i]; Card(x, 177, 302, 449); Icon(ids[i], x + 109, 199, 84); Text(x + 20, 312, 263, 40, names[i], 23, gold, TextAnchor.MiddleCenter);
                Text(x + 21, 374, 261, 84, info[i], 16, paper); Text(x + 21, 479, 261, 68, tier == 2 ? Game.L("设施已完全升级", "Fully upgraded") : Game.L("木材 ", "Timber ") + (4 + tier * 2) + " / " + Game.L("星晶 ", "Crystals ") + (2 + tier) + "\n" + Game.L("零件 ", "Parts ") + (3 + tier) + " / " + (30 + tier * 20) + Game.L(" 硬币", " coins"), 14, muted);
                if (Button(x + 17, 570, 268, 42, Game.L("建设 ", "Build ") + tier + "/2", true, Game.save.room == 0 && tier < 2)) Game.BuildProject(i);
            }
            Text(282, 654, 925, 42, Game.L("野外营地可制作抗暑抗寒的凉茶。建设效果覆盖整个旅程。", "Field camps brew climate-resistant tea. Project benefits travel with you."), 16, muted);
        }
        private void DrawBounties()
        {
            Window(Game.L("公会悬赏", "Guild contracts"), Game.L("一次接取一项 · 接取后开始计算 · 完成后领取报酬", "One active contract · actions after acceptance count · claim completed rewards"));
            if (Game.save.bounty >= 0)
            {
                int id = Game.save.bounty; Card(279, 177, 938, 361); Icon("sword", 310, 206, 64); Text(396, 216, 772, 72, Game.L(GameCore.BountyNames[id], GameCore.BountyNamesEn[id]), 26, gold); Text(310, 333, 873, 36, Game.save.bountyProgress + " / " + GameCore.BountyTargets[id], 26, paper); Bar(310, 391, 873, Game.save.bountyProgress / (float)GameCore.BountyTargets[id], mint, 8);
                if (Button(310, 458, 271, 46, Game.L("领取报酬", "Claim reward"), true, Game.save.bountyProgress >= GameCore.BountyTargets[id])) Game.ClaimBounty();
                if (Button(602, 458, 271, 46, Game.L("追踪悬赏", "Track contract"))) Game.TrackQuest("bounty"); if (Button(895, 458, 288, 46, Game.L("放弃委托", "Abandon"))) Game.AbandonBounty();
            }
            else for (int i = 0; i < 3; i++) { int id = (Game.save.bountyCycle + i) % 6; float y = 179 + i * 118; Card(279, y, 938, 101); Icon(id == 1 ? "crystal" : id == 2 ? "fish" : "sword", 298, y + 24, 45); Text(366, y + 15, 609, 41, Game.L(GameCore.BountyNames[id], GameCore.BountyNamesEn[id]), 18, paper); Text(366, y + 60, 585, 27, Game.L("目标 ", "Target ") + GameCore.BountyTargets[id] + "  ·  " + Game.L("35硬币 / 30经验 / 碎片", "35 coins / 30 XP / shards"), 13, muted); if (Button(999, y + 28, 192, 43, Game.L("接取悬赏", "Accept"), false, id != 5 || Game.CanEnter(8))) Game.AcceptBounty(id); }
            Card(279, 572, 938, 118); Text(302, 590, 889, 32, Game.L("旅途纪录", "Journey records"), 20, gold); Text(302, 641, 889, 32, Game.L("最深结算层数 ", "Deepest banked floor ") + Game.save.bestFloor + "/6  ·  " + Game.L("沙舟纪录 ", "Sand-skiff record ") + (Game.save.bestRace == 0 ? "—" : (Game.save.bestRace / 10f).ToString("0.0") + "s"), 17, muted);
        }
        private void DrawJournal()
        {
            Window(Game.L("任务手帐", "Adventure journal"), Game.L("追踪任何一条故事 · 指引会自动切换到下一步", "Track a story · navigation follows its next unfinished step")); string[] tabs = { Game.L("任务", "Quests"), Game.L("世界记录", "Records"), Game.L("阵营协定", "Factions"), Game.L("冒险足迹", "Journey") };
            for (int i = 0; i < tabs.Length; i++) if (Button(279 + i * 238, 172, 222, 37, tabs[i], journalTab == i)) journalTab = i;
            if (journalTab == 3) { DrawJourney(); return; }
            if (journalTab == 1)
            {
                int pages = Math.Max(1, (Game.data.lore.Length + 9) / 10); recordsPage = (int)GameCore.Clamp(recordsPage, 0, pages - 1);
                for (int cell = 0; cell < 10 && recordsPage * 10 + cell < Game.data.lore.Length; cell++) { int i = recordsPage * 10 + cell; bool found = Game.Has("history_" + i) || i < 3 && Game.Has("lore_" + (i + 1)); float x = 279 + cell % 2 * 477, y = 229 + cell / 2 * 85; Card(x, y, 460, 71); Icon("journal", x + 11, y + 17, 35); if (Button(x + 62, y + 14, 382, 44, found ? Game.L(Game.data.lore[i].title, Game.data.lore[i].en) : Game.L("未发现记录 ", "Undiscovered record ") + (i + 1), false, found)) { dialogParent = "journal"; Game.OpenCodex(i); } }
                if (Button(279, 650, 221, 38, Game.L("上一页", "Previous page"), false, recordsPage > 0)) recordsPage--;
                Text(518, 657, 460, 26, Game.L("世界记录 ", "Records ") + (recordsPage + 1) + " / " + pages, 15, muted, TextAnchor.MiddleCenter);
                if (Button(997, 650, 220, 38, Game.L("下一页", "Next page"), false, recordsPage < pages - 1)) recordsPage++;
                return;
            }
            if (journalTab == 2) { for (int i = 0; i < 3; i++) { float y = 229 + i * 100; Card(279, y, 938, 83); Text(303, y + 14, 855, 31, Game.L(GameCore.FactionNames[i], GameCore.FactionNamesEn[i]) + "  " + Game.save.reputation[i] + "/10", 20, gold); Bar(303, y + 59, 888, Game.save.reputation[i] / 10f, mint); } Text(282, 553, 925, 91, Game.L("共同治理条件：三份证词、建设总等级3、两名同伴、三方声望各2。协定影响补给、旅行和同伴指令。", "Shared governance: three reports, three project tiers, two companions, trust 2 with each faction. Covenants affect supplies, travel and commands."), 18, paper); Text(282, 663, 925, 31, Game.L("证词 ", "Reports ") + Game.Proofs + "/3  ·  " + Game.L("建设 ", "Projects ") + Game.ProjectTiers + "/3  ·  " + Game.L("同伴 ", "Companions ") + Game.CompanionCount + "/2", 16, muted); return; }
            // A fixed minimum readable row height replaces the shrinking
            // 35-quest list, whose tiny buttons overlapped each other.
            const int questsPerPage = 14;
            selectedQuest = (int)GameCore.Clamp(selectedQuest, 0, GameCore.QuestIds.Length - 1);
            int questPages = Math.Max(1, (GameCore.QuestIds.Length + questsPerPage - 1) / questsPerPage);
            questPage = (int)GameCore.Clamp(questPage, 0, questPages - 1);
            int firstQuest = questPage * questsPerPage;
            for (int i = firstQuest; i < Math.Min(GameCore.QuestIds.Length, firstQuest + questsPerPage); i++)
            {
                float y = 226 + (i - firstQuest) * 29;
                QuestGuide quest = Game.GetQuest(GameCore.QuestIds[i]);
                if (Button(279, y, 321, 26,
                    (quest.complete ? "✓ " : Game.save.trackedQuest == quest.id ? "◆ " : "") + quest.title, selectedQuest == i))
                    selectedQuest = i;
            }
            if (Button(279, 644, 84, 32, Game.L("上页", "Prev"), false, questPage > 0))
            { questPage--; selectedQuest = questPage * questsPerPage; }
            Text(379, 648, 119, 25, (questPage + 1) + " / " + questPages, 14, muted, TextAnchor.MiddleCenter);
            if (Button(515, 644, 85, 32, Game.L("下页", "Next"), false, questPage < questPages - 1))
            { questPage++; selectedQuest = questPage * questsPerPage; }
            QuestGuide q = Game.GetQuest(GameCore.QuestIds[selectedQuest]); Card(625, 226, 592, 464); Text(650, 244, 541, 27, q.category + (q.complete ? Game.L(" · 已完成", " · COMPLETE") : ""), 12, gold); Text(650, 281, 541, 45, q.title, 26, paper); Text(650, 342, 541, 62, q.description, 17, muted); Text(650, 410, 541, 60, Game.L("下一步：", "Next: ") + q.next, 14, gold);
            float stepPitch = Math.Min(34f, 133f / Math.Max(1, q.steps.Count));
            for (int i = 0; i < q.steps.Count; i++)
                Text(650, 482 + i * stepPitch, 542, Math.Min(33f, stepPitch),
                    (q.steps[i].done ? "✓  " : "○  ") + q.steps[i].text,
                    15, q.steps[i].done ? mint : paper);
            Text(650, 622, 540, 22, q.reward, 12, gold); if (Button(650, 647, 258, 31, Game.L("追踪任务", "Track quest"), Game.save.trackedQuest == q.id, !q.locked && !q.complete)) Game.TrackQuest(q.id); if (Button(927, 647, 263, 31, Game.L("查看目标地图", "Show destination"), false, !q.locked)) { selectedRegion = q.room; OpenMenu("map"); }
        }
        private void DrawMap()
        {
            Window(Game.L("边境与远岸", "Frontier & distant shores"), Game.L("世界路线与本地地图 · 金色为任务目标，白色为你的位置", "World routes and local terrain · gold: objective · ivory: you")); if (selectedRegion < 0 || selectedRegion >= Game.data.maps.Length) selectedRegion = Game.save.room;
            for (int i = 0; i < Math.Min(9, Game.data.maps.Length); i++) { float x = 279 + i % 3 * 189, y = 178 + i / 3 * 154; bool open = Game.CanEnter(i); Card(x, y, 176, 140, selectedRegion == i); Box(x + 1, y + 1, 174, 35, Color.Lerp(PixelArt.C(Game.data.maps[i].color), panel, .35f)); Text(x + 11, y + 8, 152, 27, "0" + (i + 1) + "  " + (i == Game.save.room ? Game.L("当前位置", "HERE") : open ? Game.L("已开放", "OPEN") : Game.L("未开放", "LOCKED")), 10, open ? gold : muted); if (Button(x + 10, y + 46, 156, 80, Game.L(Game.data.maps[i].name, Game.data.maps[i].en), selectedRegion == i)) selectedRegion = i; }
            if (Game.save.room == 9 && Button(279, 657, 555, 31, Game.L("查看当前位置：炉火旅店", "Current room: Hearth Inn"), selectedRegion == 9)) selectedRegion = 9;
            Card(866, 178, 351, 510);
            // The Abyss uses a seeded runtime map, NOT the old 36x24 atlas stub.
            MapData m = selectedRegion==Game.save.room?Game.Map:Game.data.maps[selectedRegion];
            Text(888, 197, 306, 46, Game.L(m.name, m.en), 25, gold);
            float terrainScale = WorldStreaming.PreviewScale(m.rows[0].Length,m.rows.Length,306,207);
            if (selectedRegion == Game.save.room) MiniTerrain(888, 264, terrainScale);
            else
            {
                DrawTerrainPreview(m,selectedRegion,false,888,264,terrainScale);
                QuestGuide q = Game.CurrentGuide;
                if (q.room == selectedRegion && !q.complete)
                    Box(888 + q.target.x * terrainScale - 3,
                        264 + q.target.y * terrainScale - 3, 7, 7, gold);
            }
            Text(888, 492, 306, 101, Game.CanEnter(selectedRegion) ? Game.L(m.subtitle, m.suben) : Game.RouteHint(selectedRegion, Game.save.english), 16, muted);
            bool rescueTravelLock = Game.reliefMissionRoom != 0 && selectedRegion != Game.save.room;
            if (Button(888, 616, 306, 47,
                selectedRegion == Game.save.room ? Game.L("返回本地区", "Return to region") :
                    rescueTravelLock ? Game.L("救援中无法旅行", "Rescue in progress") :
                        Game.L("快速旅行", "Fast travel"),
                true, selectedRegion == Game.save.room ||
                    !rescueTravelLock && Game.CanEnter(selectedRegion) && Game.SafeTravel))
            {
                if (selectedRegion == Game.save.room) Game.Close();
                else { Game.Enter(selectedRegion); camera = CameraTarget(); }
            }
            Text(282, 657, 554, 45,
                Game.reliefMissionRoom != 0 ?
                    Game.L("先完成居民撤离或到站台明确取消，再前往其他地区。",
                        "Finish or explicitly cancel the rescue at its station before leaving the region.") :
                    Game.SafeTravel ? Game.L("选择地区查看路线与目标。", "Select a region to inspect its route and target.") :
                        Game.L("附近有敌人，快速旅行暂不可用。", "Enemies nearby; fast travel unavailable."),
                14, muted);
        }
        private void DrawDialogue()
        {
            if (Game.conversation == null) { Game.Close(); return; } Conversation c = Game.conversation; if (c != lastConversation) { lastConversation = c; dialoguePage = 0; }
            var pages = DialogueLayout.Pages(Game.L(c.text, c.textEn), Game.save.largeText); dialoguePage = Math.Min(dialoguePage, pages.Count - 1);
            Dim(); Card(88, 180, 1104, 506); Portrait(SpeakerPortrait(c.speaker), 112, 209, 174, 219); Text(112, 452, 174, 78, Game.L(c.speaker, c.en), 18, gold, TextAnchor.MiddleCenter); Text(320, 208, 831, 36, Game.L(c.speaker, c.en), 23, gold); Text(320, 264, 832, 228, pages[dialoguePage], 19, paper);
            if (pages.Count > 1) { Text(320, 497, 535, 26, Game.L("对话页 ", "Page ") + (dialoguePage + 1) + "/" + pages.Count + "  ·  " + ControlKey("SPACE / ENTER", "A"), 12, muted); if (Button(879, 494, 272, 34, dialoguePage < pages.Count - 1 ? Game.L("继续阅读", "Read more") : Game.L("重新阅读", "Read again"))) { if (dialoguePage < pages.Count - 1) AdvanceDialogue(); else dialoguePage = 0; } }
            if (dialoguePage == pages.Count - 1) { int count = c.choices.Length; for (int i = 0; i < count; i++) { int cols = count > 2 ? 2 : count; float w = (832 - (cols - 1) * 14) / Math.Max(1, cols), y = 539 + i / cols * 45; if (Button(320 + i % cols * (w + 14), y, w, 37, (controllerUsed ? "" : (i + 1) + "  ") + Game.L(c.choices[i].label, c.choices[i].en), i == 0)) { ChooseDialogue(i); break; } } }
            else Text(320, 552, 832, 50, Game.L("读完这一段，再作出你的选择。", "Finish reading before making your choice."), 15, muted);
        }
        private int SpeakerPortrait(string name) { if (name == "获救居民" || name == "Rescued civilian") return 8; if (name == "你" || name == "主角" || name == "Player" || name == "Hero") return Game.AppearanceWeapon; return name.Contains("米拉") ? 3 : name.Contains("伊沃") ? 4 : name.Contains("森") || name.Contains("老潮") ? 5 : name.Contains("厄洛") ? 7 : name.Contains("莉雅") || name.Contains("露恩") ? 6 : -1; }
        private void DrawFishing()
        {
            Window(Game.L("湖畔垂钓", "Fishing by the lake"), Game.L("浮标进入青绿色区时收线 · E / 空格 / 点击收线", "Reel while the marker is in the teal band · E / Space / Reel")); Card(279, 179, 938, 510); Icon("fish", 667, 202, 134); Text(307, 361, 875, 41, Game.L("成功 ", "Good pulls ") + Game.fishHits + "/3   ·   " + Game.L("失误 ", "Misses ") + Game.fishMisses + "/3", 24, paper, TextAnchor.MiddleCenter);
            Box(335, 436, 826, 37, ink); Box(335 + 826 * .32f, 436, 826 * .36f, 37, mint); Box(335 + Game.fishCursor * 826 - 3, 425, 6, 60, gold); Text(321, 520, 849, 51, Game.L("三次成功获得银鳍鱼；世界暂停，钓鱼不消耗鱼饵。", "Three good pulls earn Silverfin. World paused; no bait consumed."), 16, muted, TextAnchor.MiddleCenter); if (Button(473, 601, 543, 56, Game.L("收线  E", "Reel  E"), true)) Game.FishHit();
        }
        private void DrawPause()
        {
            Window(Game.L("暂停与设置", "Pause & settings"), Game.L("调整难度、声音与辅助选项 · 随时存读档", "Difficulty, sound and accessibility · save or load anytime"));
            for (int i = 0; i < 3; i++) if (Button(279 + i * 318, 178, 301, 40, Game.L(new[] { "轻松冒险", "标准挑战", "艰难边境" }[i], new[] { "Cozy", "Normal", "Hard" }[i]), Game.save.difficulty == i)) { Game.save.difficulty = i; Save(); }
            Text(282, 231, 930, 28, Game.L("难度影响承伤：45% / 100% / 140%，可随时调整。", "Incoming damage: 45% / 100% / 140%. Change difficulty anytime."), 14, muted);
            VolumeRow(279, 281, Game.L("音乐", "Music"), 0); VolumeRow(756, 281, Game.L("动作音效", "Effects"), 1);
            VolumeRow(279, 325, Game.L("环境声", "Ambience"), 2); if (Button(756, 317, 461, 34, Game.L("声音总开关 ", "Audio master ") + (Game.save.music ? "ON" : "OFF"))) { Game.save.music = !Game.save.music; Save(); }
            if (Button(279, 375, 460, 40, Game.L("减少动态效果 ", "Reduced motion ") + (Game.save.reducedMotion ? "ON" : "OFF"))) { Game.save.reducedMotion = !Game.save.reducedMotion; Save(); }
            if (Button(756, 375, 461, 40, Game.L("增大辅助文字 ", "Larger supporting text ") + (Game.save.largeText ? "ON" : "OFF"))) { Game.save.largeText = !Game.save.largeText; Save(); }
            if (Button(279, 430, 460, 40, Game.save.english ? "语言 / Language: ENGLISH" : "语言 / Language: 中文")) { Game.save.english = !Game.save.english; Save(); }
            if (Button(756, 430, 461, 40, Game.L("路线与教学 ", "Routes & tutorial ") + (Game.save.showGuidance ? "ON" : "OFF"))) { Game.save.showGuidance = !Game.save.showGuidance; Save(); }
            if (Button(279, 485, 460, 40, Game.L("环境特效 ", "Environment effects ") + Game.L(new[] { "简洁", "标准", "丰富" }[Game.save.effectsQuality], new[] { "Minimal", "Standard", "Rich" }[Game.save.effectsQuality]))) { Game.save.effectsQuality = (Game.save.effectsQuality + 1) % 3; Save(); }
            if (Button(756, 485, 461, 40, Game.L("存档与读取", "Save & load"))) OpenMenu("saves");
            if (Button(279, 540, 300, 43, Game.L("重看操作教学", "Replay onboarding"), false, Game.Has("intro"))) { Game.RestartTutorial(); Game.Close(); Save(); }
            if (Button(594, 540, 303, 43, Game.L("进入实战练习", "Combat practice"), false, Game.Has("intro") && Game.save.room == 0)) Game.StartPractice();
            if (Button(912, 540, 305, 43, Game.L("查看指南 F1", "Field guide F1"))) OpenMenu("help");
            Text(282, 597, 930, 31, Game.L("实战练习在余烬镇教官处重复进行，物资与生命不会损失。", "Repeat safe combat practice with Emberfall's mentor; supplies and health are protected."), 14, muted);
            if (Button(279, 642, 460, 45, Game.L("继续 / 返回", "Resume / back"), true)) Back();
            if (Button(756, 642, 461, 45, Game.L("保存并返回标题", "Save & return to title"))) { if (!Game.Has("intro") || Save()) Game.mode = "title"; }
        }
        private void DrawToast()
        {
            if (CombatHud)
            {
                string message = Game.toast.Length > (Game.save.english ? 76 : 38) ? Game.toast.Substring(0,Game.save.english ? 73 : 35) + "…" : Game.toast;
                float toastY=CombatToastY; drawingHud=true; try { Panel(20,toastY,260,62,new Color(.045f,.08f,.13f,.4f),new Color(mint.r,mint.g,mint.b,.35f)); Text(31,toastY+9,238,44,message,12,gold); } finally { drawingHud=false; }
                return;
            }
            float y=Game.mode=="world"||Game.mode=="dialog"?132:706;Panel(382,y,516,67,new Color(.045f,.08f,.13f,.96f),mint);Text(400,y+13,480,46,Game.toast,14,gold,TextAnchor.MiddleCenter);
        }
    }
}
