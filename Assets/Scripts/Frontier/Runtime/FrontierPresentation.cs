using System;
using System.Collections.Generic;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private List<Point> route = new List<Point>(); private Point? routeGoal;
        private float routeTimer, arrivalTime, shakeTime, shakeX, shakeY;
        private int previousRoom = -1, previousFloor = -1, previousTutorial = -1;
        private readonly string[] tutorialTitlesZh = { "走进余烬镇", "与世界互动", "追踪第一份委托", "命中第一击", "躲过一次攻击", "释放职业战技", "整理你的背包", "出发去密林" };
        private readonly string[] tutorialTitlesEn = { "Walk into Emberfall", "Interact with the world", "Track your first quest", "Your first attack", "Learn to dodge", "Use your technique", "Inspect your inventory", "Leave for Thornwood" };
        private readonly string[] tutorialTextZh = { "使用 WASD 或方向键移动。实际走过三格后完成。", "靠近居民或发光物，按 E。试着和南侧公会的莉雅交谈。", "按 Tab 打开手帐。选择一条任务，点击追踪；金色标记会指向下一步。", "去镇内教官处练习，或命中真实敌人。J / 左键攻击；空挥不会完成。", "在教官处练习。红圈出现后按空格避开攻击，成功躲过才完成。", "按 Q 命中目标。通常耗费40耐力，需要等待冷却；可以在训练场练习。", "按 I 查看背包。点击物品看用途与来源，也可以切换已拥有的武器。", "向北走到密林出口，或按 M 选择荆棘密林。找到侦察员开始第一章。" };
        private readonly string[] tutorialTextEn = { "Move with WASD or arrows. Walk three tiles to complete this step.", "Approach a resident or glowing object and press E. Try Lyra at the southern guild hall.", "Press Tab. Select a quest and Track; gold markers point to its next unfinished step.", "Hit a practice dummy or a real foe with J / left click. Empty swings do not count.", "Practice with the mentor. Dodge a red warning with Space; successfully avoiding it counts.", "Land Q on a target. It costs 40 energy and has a cooldown. Practice safely with the mentor.", "Press I. Inspect an item's use and source, or switch between weapons you own.", "Go north to the forest exit, or press M and select Thornwood. Find the scout to begin chapter one." };
        private readonly string[] helpTitlesZh = { "操作与互动", "战斗与反击", "任务与导航", "补给与存档", "成长与配装", "同行与羁绊", "城镇与委托", "遗迹石碑", "雾港潮汐", "沙海光路", "高原寒意", "地脉远征" };
        private readonly string[] helpTitlesEn = { "Controls", "Combat & counters", "Quests & navigation", "Supplies & saves", "Builds & growth", "Companions", "Town & contracts", "Ruins tablets", "Harbor tides", "Desert light", "Mountain cold", "Abyss expeditions" };
        private readonly string[] helpZh = {
            "WASD / 方向键：移动\nJ / 鼠标左键：攻击\n空格：闪避  ·  Q：职业技能\nE：交谈、采集与机关\nF：炖汤  ·  T：凉茶  ·  Z：同伴指令\n\nI背包、G遗物、K成长、C同伴、Tab任务、M地图、B建设、N悬赏、F1指南。Esc关闭菜单或暂停。长对话可用空格或Enter翻页。",
            "红色落点表示即将命中的攻击。先走出预告范围，或用闪避的无敌时间避开。\n\n重甲正面能抵挡普通攻击，绕后或在抡击时反击。射手会后撤，射线锁定后侧移可避开。\n\n完美闪避返还耐力，增强下一次攻击。短剑第三击更强；攻击积累破势，敌人破势后短暂无法行动。\n\n鼠标决定 J / 左键与 Q 的瞄准方向。拉弓与聚能时短暂站稳，普通射击释放后可继续移动；大型战技站定到收招。近战在挥击中向前踏步，墙体会挡住位移。护盾震波是环形攻击。Z同伴指令独立冷却。",
            "在手帐选择主线、镇民委托、同伴故事或悬赏，点击追踪。详情显示步骤、奖励与目标地图。\n\n金色路线通往可以交互的位置，无法通行的潮滩不会被当作捷径。地图金色点是目标，白色点是你。完成一步后指引更新。\n\n在设置中关闭路线也能自由探索。",
            "炖汤恢复55生命、全部耐力，并增强攻击90秒。凉茶暂时抵御高温与寒意。\n\n旅店休息恢复状态、更新采集点。采集、种植与钓鱼可以补充物资。\n\n世界自动保存，暂停与退出也保存。存档管理提供三个手动位置；损坏时可恢复备份。倒下返回城镇并损失少量硬币；深渊只丢当前层未结算收益。",
            "升级获得技能点。攻击、坚韧与轻盈三条分支各有五级。\n\n击败敌人提升武器熟练度。角色第五级可用一个技能点学习第二Q战技，学习后自由切换。\n\n背包可比较均衡、蓄势、流转三种打法。第一星核或武器熟练度1解锁。三枚星核还可解除藤封、浅水与断桥，按R或E开启旧路。\n\n双槽遗物改变技能、闪避或暴击，也可能增加耗能与承伤。记忆工坊可以锻印遗物。",
            "完成地区调查后，米拉、伊沃与森会加入。一次邀请一人；他们自动支援，Z发出战术指令。\n\n米拉治疗与保护，伊沃射出贯穿箭，森冻结与控制。\n\n每日分享炖汤、完成个人故事提升羁绊。同伴界面与任务手帐都能查看故事。",
            "在余烬镇建设水渠、驿站与工坊，每项最多两级。效果覆盖种植、野外休整和遗物锻印。\n\n一次接取一项悬赏，接取后计算进度。完成后打开N领取报酬。\n\n共同治理需要三份证词、建设总等级3、两名同伴，三方声望各达到2。",
            "遗迹有三座石碑。先调查西北记录，再按线索触碰石碑。顺序错误会重置进度。\n\n手帐保留已发现记录，可以随时重读。任务标记指向当前需要调查的石碑。",
            "潮汐每36秒循环，前22秒为低潮。高水位时潮滩无法通行。控潮闸门消耗两个星晶暂时保持低潮。\n\n先找米拉决定救援与守灯优先级，再修塔或救人。塔需要零件；救援需要木材与药草。\n\n目标无法到达时，等待退潮或开闸。",
            "转动三座镜面，观察地面的光线，让光路连接东侧蓄光池。\n\n远离阴影会积累暑热。在绿洲阴影停留或喝凉茶可以降低危险。\n\n沙舟竞速需要按次序经过风旗。冲刺与移动由你控制，倒计时结束会中止挑战。",
            "北、东、南三座钟可以调整音高。调查当地记录，找到和谐的排列。\n\n寒意过高会降低移动与耐力回复，继续升高会伤害生命。靠近火盆或喝凉茶。\n\n点燃火盆需要两份木材。是否公开记录会改变阵营支持。",
            "每次远征六层，地图由保存的种子生成。清理敌人后去东门结算，再选择祝福或撤离。\n\n本层硬币、碎片与经验在结算前属于待结算收益。倒下或提前离开只丢这部分；此前结算保留。\n\n第三层起出现地脉落印，第六层有守护者。读取存档会恢复存活敌人、生命、祝福与进度。"
        };
        private readonly string[] helpEn = {
            "WASD / arrows: move\nJ / left click: attack\nSpace: dodge  ·  Q: class technique\nE: talk, gather and operate\nF: stew  ·  T: tea  ·  Z: companion command\n\nI inventory, G relics, K talents, C companions, Tab journal, M map, B projects, N contracts, F1 guide. Esc closes menus or pauses. Space or Enter turns dialogue pages.",
            "Red areas mark an incoming strike. Leave the warning or dodge through it.\n\nHeavy armor guards its front: flank it or attack its wind-up. Archers retreat; sidestep after their shot locks.\n\nA perfect dodge refunds energy and strengthens your next attack. The third sword hit is stronger. Attacks build stagger; a broken enemy briefly cannot act.\n\nMouse aims J / click and Q. Plant your feet to draw or cast; basic shots allow movement after release, heavy techniques remain planted. Sword strikes step forward and respect walls. Shield pulse is radial. Z has a separate cooldown.",
            "Select a story, request, companion quest or contract in the journal and Track it. Details show steps, rewards and destination.\n\nGold routes lead into interaction range. They do not cross walls or high-tide shoals. Gold is your map goal; ivory is you. Guides update with each step.\n\nTurn guides off in settings to explore freely.",
            "Stew restores 55 health, all energy and boosts damage for 90 seconds. Tea protects against heat and cold.\n\nInn rest restores you and renews resource nodes. Gathering, gardening and fishing replenish supplies.\n\nAutosaves run during play, pause and exit. Save manager has three manual slots and backup recovery. Falling returns you to town with a small coin cost. Abyss losses affect only unbanked loot.",
            "Levels grant talent points. Edge, Resolve and Agility each have five ranks.\n\nDefeating foes trains your current weapon. At character level five, spend one point to learn your alternate Q technique; then switch freely.\n\nCompare Balanced, Focused and Flow styles in Inventory. Core one or weapon mastery one unlocks them. Three cores also open grove, tidal and bridge routes: press R or E near seals.\n\nTwo relic slots change healing, energy use or critical hits. Some increase costs or damage taken. The workshop refines relics.",
            "Mira, Ivo and Sen join after their regional reports. Invite one at a time. They support you automatically; Z issues a tactical command.\n\nMira heals and protects, Ivo fires piercing arrows, Sen freezes and controls enemies.\n\nShare a stew once a day and finish personal stories to grow bonds. Track stories in your journal.",
            "Build canals, a waystation and a workshop in Emberfall, up to two tiers each. They improve crops, field rest and relic refining.\n\nAccept one contract at a time. Later actions count; claim rewards in N Contracts.\n\nShared governance needs three reports, three project tiers, two companions and trust 2 with every faction.",
            "The ruins hold three tablets. Read the northwest record, then touch tablets in the hinted order. A wrong choice resets the sequence.\n\nFound records remain in your journal. Quest markers point to the next tablet to investigate.",
            "A tide cycle lasts 36 seconds; the first 22 are low tide. High water blocks shoals. Spend two crystals at the sluice to hold low tide.\n\nAgree on a plan with Mira, then repair relays or rescue people. Relays need parts; rescues need timber and herbs.\n\nWait for ebb or use the sluice if a target is inaccessible.",
            "Rotate three mirrors and follow the visible beams to connect the eastern receiver.\n\nHeat grows away from shade. Cool down in oasis shade or drink tea.\n\nThe sand-skiff race follows numbered flags. Move and dodge yourself; the deadline ends the attempt.",
            "Tune the north, east and south bells. Investigate the record for a harmonious sequence.\n\nHigh cold slows movement and energy recovery; extreme cold hurts you. Warm up near braziers or drink tea.\n\nTwo timber lights a brazier. Publishing or withholding the record changes faction trust.",
            "Six seeded floors per expedition. Clear enemies, bank at the east gate, then choose a boon or extract.\n\nCurrent-floor coins, shards and XP are unbanked until settlement. Falling or leaving loses only those; earlier settlements stay.\n\nRift marks begin on floor three, the guardian waits on six. Saves restore surviving enemies, health, boons and progress."
        };
        private void DrawHelp()
        {
            Window(Game.L("冒险指南", "Field guide"), Game.L("按玩法查阅 · 随时重读 · 操作教学可在设置中重新开始", "Browse by topic · reread anytime · restart onboarding in Settings"));
            for (int i = 0; i < helpZh.Length; i++) if (Button(279 + i % 2 * 174, 177 + i / 2 * 79, 160, 66, Game.L(helpTitlesZh[i], helpTitlesEn[i]), helpTopic == i)) helpTopic = i;
            Card(647, 177, 570, 512); Text(673, 200, 518, 48, Game.L(helpTitlesZh[helpTopic], helpTitlesEn[helpTopic]), 28, gold);
            string body = controllerUsed && helpTopic == 0 ? Game.L("左摇杆：移动\nX：攻击 · A：闪避 · Y：战技\nB：交谈、采集与机关\nLB：炖汤 · RB：同伴指令\nVIEW：手帐 · START：暂停\n\n菜单内用摇杆或方向键移动焦点，A确认、B返回，LB/RB切换菜单。\n\n炖汤与凉茶也可在背包使用。靠近封印按B开启旧路。长对话用A翻页，再确认选项。", "Left stick: move\nX: attack · A: dodge · Y: technique\nB: talk, gather and operate\nLB: stew · RB: companion command\nVIEW: journal · START: pause\n\nIn menus: stick / D-pad moves focus, A selects, B returns, LB/RB switches menus.\n\nUse stew and tea in Inventory. Press B at a sealed route. A turns dialogue pages, then selects a choice.") : Game.L(helpZh[helpTopic], helpEn[helpTopic]);
            Text(673, 269, 518, 392, body, 18, paper);
        }
        private void DrawTutorialPrompt()
        {
            int step = Game.TutorialStep; if (Game.enemies.Exists(e => (e.Position - Game.Position).Length < 6) && !Game.practicing) return; if (!Game.save.showGuidance || step >= 8 || Game.practicing) return; Panel(24, 474, 298, 204, new Color(.05f, .095f, .15f, .96f), mint);
            Text(40, 488, 265, 23, Game.L("冒险入门 ", "ONBOARDING ") + (step + 1) + "/8", 11, mint); Text(40, 522, 265, 36, Game.L(tutorialTitlesZh[step], tutorialTitlesEn[step]), 19, paper); Text(40, 565, 265, 65, TutorialInputHint(step), 13, muted);
            if (Button(40, 641, 124, 25, Game.L("查看指南", "Field guide"))) OpenMenu("help"); if (Button(181, 641, 125, 25, Game.L("跳过教学", "Skip tutorial"))) Game.SkipTutorial();
        }
        private void UpdatePresentation(float dt)
        {
            if (Game.mode == "world") { resumeTime = Math.Max(0,resumeTime-dt); combatHudTime = Game.enemies.Exists(e=>e.boss||(e.Position-Game.Position).Length<8) || Game.playerAction != null || Game.playerHitTime > 0 ? 3 : Math.Max(0,combatHudTime-dt); }
            if (Game.mode == "inventory") Game.TutorialAction(6);
            if (previousRoom != Game.save.room || previousFloor != Game.save.abyssFloor) { arrivalTime = 4; camera = CameraTarget(); previousRoom = Game.save.room; previousFloor = Game.save.abyssFloor; routeTimer = 0; selectedRegion = Game.save.room; }
            if (Game.mode == "world") arrivalTime = Math.Max(0, arrivalTime - dt);
            shakeTime = Math.Max(Game.impactShake, Math.Max(0, shakeTime - dt)); shakeX = shakeY = 0;
            if (!Game.save.reducedMotion && Game.save.effectsQuality > 0 && Game.mode == "world") { shakeX = (float)Math.Sin(clock * 91) * shakeTime * 19; shakeY = (float)Math.Cos(clock * 73) * shakeTime * 12; }
            if (previousTutorial != Game.TutorialStep) { int old = previousTutorial; previousTutorial = Game.TutorialStep; if (old >= 0 && old < 8 && Game.mode == "world" && Game.toastTime <= 0) Game.Notify(Game.TutorialStep == 8 ? "入门完成！F1可随时查看指南。" : "教学进度已更新。", Game.TutorialStep == 8 ? "Onboarding complete! F1 opens your field guide." : "Onboarding progress updated."); }
            routeTimer -= dt; Point? goal = Game.NavigationTarget(); if (Game.mode == "world" && Game.save.showGuidance && (routeTimer <= 0 || !SameGoal(goal, routeGoal))) { routeGoal = goal; route = goal.HasValue ? Game.NavigationPath(goal.Value) : new List<Point>(); routeTimer = .65f; }
        }
        private bool SameGoal(Point? a, Point? b) { return a.HasValue == b.HasValue && (!a.HasValue || (a.Value - b.Value).Length < .6f); }
        private bool PointerOverInterface(Point mouse)
        {
            if (mouse.x < 0 || mouse.x >= Width || mouse.y < 0 || mouse.y >= Height) return true;
            foreach (Rect rect in HudPointerRegions()) if (Contains(rect, mouse)) return true;
            return Game.practicing && Contains(new Rect(785,246,130,34), mouse);
        }
        private bool TideBlocksGuide { get { return Game.save.room == 5 && !Game.LowTide && routeGoal.HasValue && (routeGoal.Value - Game.Position).Length > 1.8f && route.Count == 0; } }
        private bool DialogueChoicesVisible() { return Game.conversation != null && Game.conversation == lastConversation && dialoguePage >= DialogueLayout.Pages(Game.L(Game.conversation.text, Game.conversation.textEn), Game.save.largeText).Count - 1; }
        private void AdvanceDialogue() { if (Game.conversation == null) return; if (lastConversation != Game.conversation) { lastConversation = Game.conversation; dialoguePage = 0; } dialoguePage = Math.Min(DialogueLayout.Pages(Game.L(Game.conversation.text, Game.conversation.textEn), Game.save.largeText).Count - 1, dialoguePage + 1); }
        private void Diamond(float x, float y, float r, Color c) { for (int i = -(int)r; i <= (int)r; i++) { float w = r - Math.Abs(i); Box(x - w, y + i, w * 2 + 1, 1, c); } }
        private void DrawNavigation()
        {
            if (Game.mode != "world" || !Game.save.showGuidance) return; Point? target = Game.NavigationTarget(); if (!target.HasValue) return;
            if (Game.SafeTravel && route.Count > 0) for (int i = 0; i < Math.Min(route.Count, 22); i += 2) { Point p = ScreenPoint(route[i]); Box(p.x - 2, p.y - 1, 4, 3, new Color(.94f, .77f, .48f, .55f)); }
            Point goal = ScreenPoint(target.Value); if (goal.x > 34 && goal.x < 978 && goal.y > 132 && goal.y < 675) { float bob = Game.save.reducedMotion ? 0 : (float)Math.Sin(clock * 3) * 3; Diamond(goal.x, goal.y - 38 + bob, 8, gold); Diamond(goal.x, goal.y - 38 + bob, 3, ink); }
            else { Point center = ScreenPoint(Game.Position), delta = goal - center; float kx = delta.x == 0 ? 999 : (delta.x > 0 ? 966 - center.x : 40 - center.x) / delta.x, ky = delta.y == 0 ? 999 : (delta.y > 0 ? 665 - center.y : 150 - center.y) / delta.y; float k = Math.Min(1, Math.Max(0, Math.Min(kx, ky))); Point edge = center + delta * k; Diamond(edge.x, edge.y, 9, gold); Text(edge.x - 80, edge.y + 13, 160, 40, Game.CurrentGuide.room == Game.save.room ? Game.L("任务目标", "OBJECTIVE") : Game.L("区域出口", "REGION EXIT"), 11, gold, TextAnchor.MiddleCenter); }
        }
        private void DrawTerrainEdge(int x, int y, char tile, Point p)
        {
            if (tile == '#')
            {
                // A shaded vertical lip distinguishes solid raised terrain from
                // walkable stone paths without changing the collision cells.
                if (y + 1 < Game.Map.rows.Length && Game.Map.rows[y + 1][x] != '#')
                {
                    Box(p.x, p.y + Tile * .73f, Tile, Tile * .27f, new Color(.05f, .095f, .14f, .57f));
                    Box(p.x, p.y + Tile * .73f, Tile, 1, new Color(.79f, .77f, .59f, .42f));
                    Box(p.x, p.y + Tile, Tile, 6, new Color(.04f, .08f, .13f, .22f));
                }
                if (x > 0 && Game.Map.rows[y][x - 1] != '#') Box(p.x, p.y, 2, Tile, new Color(.06f, .09f, .13f, .42f));
                if (x + 1 < Game.Map.rows[y].Length && Game.Map.rows[y][x + 1] != '#') Box(p.x + Tile - 2, p.y, 2, Tile, new Color(.06f, .09f, .13f, .42f));
                return;
            }
            if (!WaterSurface(tile) || terrainVariants == null) return;
            // The previous flat 4px dark bars + 1px lime foam painted over
            // every rich water tile, even when motion FX were disabled.
            // Use only ORIGINAL water material pixels from the published
            // TerrainVariants.png bottom row. The static edge remains
            // visible with reduced motion and at every effects quality.
            int variant=(PreparedTileSeed(x,y)/7)%4;
            bool north=y>0 && !WaterSurface(Game.Map.rows[y-1][x]);
            bool south=y+1<Game.Map.rows.Length &&
                !WaterSurface(Game.Map.rows[y+1][x]);
            bool west=x>0 && !WaterSurface(Game.Map.rows[y][x-1]);
            bool east=x+1<Game.Map.rows[y].Length &&
                !WaterSurface(Game.Map.rows[y][x+1]);
            if (!north&&!south&&!west&&!east) return;
            float sourceX=variant*48f;
            Color old=GUI.color;
            try
            {
                // Cropped 48px hand-painted wave highlights instead of a
                // new coarse material. Source UVs remain pixel-aligned.
                GUI.color=new Color(.87f,.96f,1f,.54f*worldActorOpacity);
                Rect horizontal=new Rect(
                    (sourceX+7f)/192f,17f/192f,34f/192f,4f/192f);
                Rect vertical=new Rect(
                    (sourceX+20f)/192f,7f/192f,4f/192f,30f/192f);
                if(north)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+1,Tile-2,4),terrainVariants,horizontal);
                if(south)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+Tile-5,Tile-2,4),terrainVariants,horizontal);
                if(west)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+1,4,Tile-2),terrainVariants,vertical);
                if(east)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+Tile-5,p.y+1,4,Tile-2),terrainVariants,vertical);
            }
            finally { GUI.color=old; }
        }
        private bool WaterSurface(char tile) { return tile == '~' || tile == '%' && Game.save.room == 5 && !Game.LowTide; }
        private void PropImage(string kind, int variant, float x, float y, float w, float h, Color? tint = null)
        {
            if (worldProps == null) RecoverPublishedVisualAtlases();
            if (DrawFieldProp(kind, variant, x, y, w, h, tint)) return;
            int id = kind == "tree" ? 0 : kind == "house" ? (variant == 1 ? 2 : 1) : kind == "ruin" ? 3 : kind == "lamp" ? 4 : kind == "palm" ? 5 : kind == "snowpine" ? 6 : kind == "lighthouse" ? 7 : -1;
            if (worldProps == null || id < 0) { Image(art.Prop(kind, variant), x, y, w, h, tint); return; }
            float side = Math.Min(w, h); Color previous = GUI.color;
            try { Color actual=tint??Color.white;actual.a*=worldActorOpacity;
                GUI.color=actual; GUI.DrawTextureWithTexCoords(new Rect(x + (w - side) / 2, y + h - side, side, side), worldProps, new Rect(id % 4 / 4f, id < 4 ? .5f : 0, .25f, .5f)); }
            finally { GUI.color = previous; }
        }
        private void Light(Point world, float size, Color c) { Point p = ScreenPoint(world); Image(art.Glow(), p.x - size / 2, p.y - size / 2, size, size, c); }

        // Original WorldProps lamps, lanterns, crystals and lighthouse must
        // read as actual emissive objects, not the old single fixed glow
        // centred one whole floor tile above EVERY prop. The dual-radius
        // illumination follows the authored world attachment and original
        // soft pixel-art glow; never replaces any detailed Sprite.
        private void DrawWorldEmitter(Point anchor,string kind,
            float opacity,int stableSeed)
        {
            if (Game.save.effectsQuality==0 || opacity<=.001f) return;
            bool signal=kind=="outerrelay";
            bool cool=signal||kind=="crystal"||kind=="root";
            float height=kind=="lighthouse"?1.95f:
                kind=="lamp"?1.12f:
                kind=="crystal"?.65f:
                kind=="root"?.45f:
                kind=="fire"||kind=="campfire"||kind=="brazier"?.28f:.52f;
            Point flame=anchor-new Point(0,height);
            float radius=kind=="lighthouse"?178:
                kind=="lamp"?CurrentWorldVisualLook.lampRadius:
                cool?106:96;
            // Continuous, world-seeded flame rhythm. Animation never moves
            // the light's attachment, so neighbouring actors do not strobe
            // and camera pan/repaint cannot change its appearance.
            float flicker=1f;
            if (!Game.save.reducedMotion)
                flicker=.965f+
                    (float)Math.Sin(clock*2.3f+stableSeed*.071f)*.026f+
                    (float)Math.Sin(clock*.79f+stableSeed*.137f)*.009f;
            Color halo=cool?new Color(.38f,.79f,.84f,1f):
                new Color(1f,.73f,.4f,1f);
            if(signal)halo=new Color(.52f,.94f,.73f,1f);
            float alpha=(Game.Night?CurrentWorldVisualLook.nightLightAlpha:
                CurrentWorldVisualLook.dayLightAlpha)*opacity*flicker;
            if(signal)alpha=.27f*opacity*flicker;
            halo.a=alpha;
            Light(flame,radius,halo);
            if(Game.save.effectsQuality>=2)
            {
                // Compact warm/cool core preserves hand-painted metal/stone
                // texture. Secondary light never expands into a huge HUD wash.
                halo.a=alpha*.38f;
                Light(flame,radius*.43f,halo);
            }
        }
        private void DrawAtmosphere()
        {
            Color ambient = Game.save.room == 6 ? new Color(.7f, .48f, .2f, .035f) : Game.save.room == 7 ? new Color(.36f, .55f, .75f, .06f) : Game.save.room == 8 ? new Color(.13f, .18f, .35f, .15f) : Game.Night ? new Color(.06f, .1f, .26f, .24f) : new Color(.7f, .77f, .49f, .025f); Box(0, 0, Width, Height, ambient);
            if (Game.save.effectsQuality > 0)
            {
                foreach (var d in Game.Map.decor)
                    if ((d.kind=="lamp"||d.kind=="lighthouse"||d.kind=="crystal"||
                         d.kind=="root"||d.kind=="fire") && WorldDrawable(d.x,d.y,180,180))
                    {
                        DrawWorldEmitter(new Point(d.x,d.y),d.kind,
                            FadeWorldProp(d.x,d.y),
                            (int)(d.x*31f+d.y*71f));
                    }
                foreach (var o in Game.Map.objects)
                {
                    bool warm=o.kind=="campfire"||o.kind=="brazier"&&Game.Has("warm_"+o.id);
                    bool signal=o.kind=="outerrelay"&&Game.OuterRelayRestored(Game.save.room);
                    if(!(warm||signal)||!WorldDrawable(o.x,o.y,180,180))continue;
                    DrawWorldEmitter(new Point(o.x,o.y),
                        signal?"outerrelay":o.kind,
                        FadeWorldProp(o.x,o.y),
                        (int)(o.x*31f+o.y*71f));
                }
                if (Game.save.room == 5 && Game.save.effectsQuality == 2) for (int i = 0; i < 4; i++) Box(0, 150 + i * 116, Width, 24, new Color(.8f, .9f, .9f, .025f));
                for (int i = 0; i < 10; i++) { float alpha = (10 - i) * .0025f; Box(i * 4, i * 4, Width - i * 8, 4, new Color(.02f, .04f, .08f, alpha)); Box(i * 4, Height - i * 4 - 4, Width - i * 8, 4, new Color(.02f, .04f, .08f, alpha)); }
            }
            DrawNavigation(); if (Game.mode == "world" && arrivalTime > 0 && !Game.practicing) { float a = Math.Min(1, arrivalTime); Panel(414, 111, 452, 56, new Color(.04f, .07f, .12f, .87f * a), new Color(gold.r, gold.g, gold.b, a)); Text(429, 121, 420, 39, Game.L(Game.Map.name, Game.Map.en) + (Game.InAbyss ? "  " + Game.save.abyssFloor + "/6" : ""), 23, paper, TextAnchor.MiddleCenter); }
        }
        private void DrawCombatEffect(Effect e)
        {
            if (DrawAuthoredEffect(e)) return;
            Point p = ScreenPoint(e.position); float progress = 1 - e.life / e.maxLife;
            if (e.kind == "number" || e.kind == "break") { Text(p.x - 59, p.y - 54 - progress * 22, 120, 32, e.text, 19, ink, TextAnchor.MiddleCenter); Text(p.x - 60, p.y - 55 - progress * 22, 120, 32, e.text, 19, gold, TextAnchor.MiddleCenter); }
            else if (e.kind == "slash") { double angle = Math.Atan2(e.direction.y, e.direction.x); for (int i = 0; i < 18; i++) { double a = angle - 1.1 + i * .13; Box(p.x + (float)Math.Cos(a) * (30 + progress * 32), p.y + (float)Math.Sin(a) * (30 + progress * 32), 4, 3, new Color(1, .85f, .59f, 1 - progress)); } }
            else if (e.kind == "hurt") Ring(e.position, .6f + progress * .5f, PixelArt.C("#ebaa8a"), 16);
            else if (e.kind == "frost") { Ring(e.position, 4, new Color(.6f, .8f, .9f, .7f), 48); if (Game.save.effectsQuality > 0) Light(e.position, 230, new Color(.45f, .75f, .88f, .13f * (1 - progress))); }
            else { Ring(e.position, e.radius > 0 ? e.radius : e.kind == "burst" ? .4f + progress * 3 : .3f + progress * .7f, e.kind == "danger" ? PixelArt.C("#e69d83") : mint, 24); if (Game.save.effectsQuality > 0 && e.kind == "burst") Light(e.position, 210, new Color(.5f, .9f, .8f, .3f * (1 - progress))); }
        }
    }
}
