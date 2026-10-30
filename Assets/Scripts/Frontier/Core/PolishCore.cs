using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        public bool practicing;
        public readonly Queue<string> sounds = new Queue<string>();
        private string lastSound = "";
        public string sound { get { return lastSound; } set { lastSound = value; if (!string.IsNullOrEmpty(value)) { if (sounds.Count >= 24) sounds.Dequeue(); sounds.Enqueue(value); } } }
        public int practiceStage;
        private Point practiceReturn;
        private string previousGuide;
        private int guideRevision = -1, guideStage = -1, guideRoom = -1, guideRunes = -1;
        private string guideTracked;
        private float actionHintTimer;
        public int Style { get { return save.weaponStyles[save.weapon]; } }
        public bool StylesUnlocked { get { return Has("core_1") || MasteryRank(save.weapon) >= 1; } }
        public bool Ability(int id) { return id >= 1 && id <= 3 && Has("core_" + id); }
        public int ShopStewCost { get { return (save.reputation[1] >= 3 ? 15 : 18) - (Has("dispatch_complete") ? 2 : 0); } }
        public int ShopTimberCost { get { return save.reputation[1] >= 3 ? 8 : 10; } }
        public void SanitizePolish()
        {
            if (save.polishVersion == 0) { save.musicVolume = .65f; save.effectsVolume = .8f; save.ambienceVolume = .5f; save.polishVersion = 1; }
            save.musicVolume = Finite(save.musicVolume) ? Clamp(save.musicVolume, 0, 1) : .65f;
            save.effectsVolume = Finite(save.effectsVolume) ? Clamp(save.effectsVolume, 0, 1) : .8f;
            save.ambienceVolume = Finite(save.ambienceVolume) ? Clamp(save.ambienceVolume, 0, 1) : .5f;
            save.weaponStyles = BoundedArray(save.weaponStyles, 3, 0, 2);
            save.practiceBits = (int)Clamp(save.practiceBits, 0, 7); save.deaths = (int)Clamp(save.deaths, 0, 99999);
            save.savedRunes = (int)Clamp(save.savedRunes, 0, 2);
            if (save.moments == null) save.moments = new List<JournalMoment>();
            save.moments.RemoveAll(m => m == null || m.zh == null || m.en == null);
            while (save.moments.Count > 8) save.moments.RemoveAt(0);
            if (save.worldEnemies != null && save.worldEnemies.Length > 24) save.worldEnemies = null;
            practicing = false; previousGuide = null; guideRevision = guideStage = guideRoom = guideRunes = -1; guideTracked = null; actionHintTimer = 0;
        }
        public WeaponProfile Profile(int weapon, int style)
        {
            weapon = (int)Clamp(weapon, 0, 2); style = (int)Clamp(style, 0, 2);
            float damage = Damage + (MasteryRank(weapon) - MasteryRank(save.weapon)) * 2;
            var p = new WeaponProfile { damage = damage * (weapon == 1 ? .9f : weapon == 2 ? 1.25f : 1), interval = weapon == 0 ? .34f : weapon == 1 ? .44f : .62f, range = weapon == 0 ? 1.85f : weapon == 1 ? 12 : 8 };
            p.benefit = L(weapon == 0 ? "第三击伤害与范围增加" : weapon == 1 ? "远射；第三箭穿透并增伤" : "星火范围爆炸；第三击减速并增伤", weapon == 0 ? "Third hit gains damage and reach" : weapon == 1 ? "Long reach; third shot pierces and hits harder" : "Starfire splash; third cast slows and hits harder");
            p.cost = L(weapon == 0 ? "需要靠近敌人" : weapon == 1 ? "单体伤害，注意射线遮挡" : "出手较慢，适合敌人聚集", weapon == 0 ? "Fight within melee reach" : weapon == 1 ? "Single target; walls block shots" : "Slower attacks; excels against groups");
            if (style == 1) { p.damage *= 1.4f; p.interval *= 1.3f; p.energy = 4; p.benefit = L(weapon == 1 ? "贯穿射击，适合一线敌人" : "单次伤害+40%", weapon == 1 ? "Pierces enemies in a line" : "+40% damage per attack"); p.cost = L("出手慢30%，每击耐力4", "30% slower; 4 energy per attack"); }
            if (style == 2) { p.damage *= .8f; p.interval *= .8f; p.energy = 3; p.benefit = L(weapon == 0 ? "破势积累+40%，出手更快" : weapon == 1 ? "三连散射，覆盖更广" : "命中附加短暂冻结", weapon == 0 ? "+40% stagger; faster attacks" : weapon == 1 ? "Three-shot spread for coverage" : "Hits briefly freeze enemies"); p.cost = L("伤害-20%，每击耐力3", "-20% damage; 3 energy per attack"); }
            return p;
        }
        public void SetStyle(int style)
        {
            if (style < 0 || style > 2 || style > 0 && !StylesUnlocked) return;
            // Do not interrupt live input, requeue autosaves or play equip
            // sounds when the user reselects the style already in use.
            if (Style == style) return;
            // A different attack profile starts a new combo sequence, just
            // like changing the weapon itself.
            CancelAction(); combo = 0; comboTimer = 0;
            save.weaponStyles[save.weapon] = style; revision++; sound = "equip";
        }
        public bool CanResetTalents
        {
            get
            {
                int invested = save.talents[0] + save.talents[1] + save.talents[2];
                return invested > 0 && save.coins >= 20 && save.skillPoints <= 99 - invested;
            }
        }
        public void ResetTalents()
        {
            if (save.coins < 20) return;
            int points = save.talents[0] + save.talents[1] + save.talents[2];
            if (points == 0) return;
            // SaveData v2 intentionally caps unspent skills at 99. A large
            // respec used to charge 20 coins, refund above 99, and silently
            // discard the excess when that save was reloaded.
            if (save.skillPoints > 99 - points)
            {
                Notify("可用技能点接近99点上限。先使用部分技能点，再重置天赋。",
                    "Too many unspent points for a full refund (99 max). Spend some before resetting.");
                return;
            }
            save.coins -= 20; save.skillPoints += points; Array.Clear(save.talents, 0, 3);
            save.maxHealth = 100 + save.upgrades * 20 + (save.profession == 0 ? 20 : 0); save.health = Math.Min(save.health, save.maxHealth);
            revision++; RecordMoment("重新分配成长方向：返还 " + points + " 技能点。", "Build reconsidered: refunded " + points + " talent points.");
        }
        public void RecordMoment(string zh, string en)
        { save.moments.Add(new JournalMoment { zh = zh, en = en, day = Day }); while (save.moments.Count > 8) save.moments.RemoveAt(0); revision++; sound = "quest"; }
        public string WorldResponse()
        {
            // This is the player's lived world state, not a spoiler-filled
            // setting encyclopedia. Keep it compact for the existing journal.
            string s = L("选择留下的变化", "Changes you made");
            s += "\n" + (Has("scout_saved") ?
                L("密林侦察员已平安归来。", "The woodland scout came home safely.") :
                L("密林侦察员仍在等待救援。", "The woodland scout still needs rescue."));
            s += "\n" + L("摆渡盟声望3后：炖汤15、木材8硬币。",
                "Ferry trust 3: stew 15 coins, timber 8 coins.");
            if(PublicFishKitchenStocked)
                s += "\n" + L("湖畔热汤已送往远地避险棚：",
                    "Hot meals delivered to far shelters: ") +
                    SoupRouteDeliveredCount+"/3."+
                    (SoupRouteComplete?
                        L("密林、雾港和霜钟的守棚人都能在安全时照料晚归旅人。",
                          "Keepers in Thornwood, Mistport and Frostchime now offer warm recovery when the road is safe."):
                        "");
            if(PublicFishKitchenStocked)
                s += "\n" + L("湖畔公共厨房恢复开火：用两条银鳍鱼换一碗炖汤；炖汤恢复65生命，攻击增益延长至105秒。",
                    "The public lakeside kitchen is stocked: two Silverfin make a bowl of stew; stew heals 65 HP and boosts attacks for 105 seconds.");
            if (save.harborPledge > 0)
                s += "\n" + L("雾港优先：", "Mistport priority: ") +
                    L(new[] { "", "守灯", "救援", "协调" }[save.harborPledge],
                      new[] { "", "lights", "rescue", "coordination" }[save.harborPledge]);
            if (save.desertChoice > 0)
                s += "\n" + L(save.desertChoice == 1 ? "沙海供水优先绿洲。" :
                    "沙海供能优先灯网.", save.desertChoice == 1 ?
                    "Desert supply prioritizes oasis water." : "Desert supply prioritizes navigation lights.");
            if (save.mountainChoice > 0)
                s += "\n" + L(save.mountainChoice == 1 ? "高原证词由议会保存。" :
                    "高原遇难者名字已经公开。", save.mountainChoice == 1 ?
                    "Mountain evidence preserved for reform." : "The mountain names are public.");
            if (Has("survey_complete") || Has("road_trial_complete") || Has("wayfarer_complete"))
                s += "\n" + L("归乡网络：", "Homeward network: ") +
                    L((Has("survey_complete") ? "星图 " : "") +
                      (Has("road_trial_complete") ? "守路信标 " : "") +
                      (Has("wayfarer_complete") ? "旅人获救" : ""),
                      (Has("survey_complete") ? "chart " : "") +
                      (Has("road_trial_complete") ? "beacons " : "") +
                      (Has("wayfarer_complete") ? "travelers rescued" : ""));
            if (Has("archive_complete") || Has("archive_echo_complete"))
                s += "\n" + L("测绘师遗产：", "Surveyor legacy: ") +
                    L(Has("archive_echo_complete") ? "旧罗盘已校准。" : "撤离星图已归档。",
                      Has("archive_echo_complete") ? "compass restored." : "escape atlas archived.");
            if (Has("fieldstation_complete") || Has("fieldstation_survey_complete"))
                s += "\n" + L("远岸观测：", "Farshore weather: ") +
                    L(Has("fieldstation_survey_complete") ? "三地巡测完成。" : "三地避风站连通。",
                      Has("fieldstation_survey_complete") ? "three surveys filed." : "three shelters linked.");
            if (Has("relief_rescue_5") || Has("relief_rescue_6") || Has("relief_rescue_7"))
            {
                int rescued = 0;
                for (int region = 5; region <= 7; region++)
                    if (Has("relief_rescue_" + region)) rescued++;
                s += "\n" + L("远岸居民撤离：", "Civilian evacuations: ") +
                    rescued + "/3" +
                    (Has("relief_rescue_complete") ?
                        L("；三地补给与避难路线均已守护。", "; all three shelter roads are secured.") : "");
            }
            if (save.ending > 0)
                s += "\n" + L("星核归宿：", "Starcore outcome: ") +
                    L(new[] { "", "还给土地", "保留屏障", "居民共管" }[save.ending],
                      new[] { "", "returned to land", "shield retained", "shared by communities" }[save.ending]);
            if (save.frontierEnding > 0)
                s += "\n" + L("远岸治理：", "Farshore governance: ") +
                    L(new[] { "", "共同水渠", "轮值灯网", "开放星路" }[save.frontierEnding],
                      new[] { "", "shared canals", "rotating lightwatch", "open starroads" }[save.frontierEnding]);
            return s;
        }
        private void TickPolish(float dt)
        {
            TickRoadwardenTrial(dt);
            TickWayfarerEncounter(dt);
            TickReliefMission(dt);
            TickAtlasEcho(dt);
            TickRootHarmonyTrial(dt);
            TickCommonsRelayTrial(dt);
            TickSkywatch(dt);
            TickGlasswind(dt);
            TickTideSalvage(dt);
            TickTemper(dt);
            TickTriad(dt);
            TickWitnessDefense(dt);
            TickBlackout(dt);
            TickAfterlight(dt);
            TickStorm(dt);
            actionHintTimer = Math.Max(0, actionHintTimer - dt);
            // Permanent quest mutations increment revision; tablet progress is
            // transient. Do not rebuild two language journals every idle frame.
            // CurrentGuide remains live, including moving enemies/resource targets.
            if (guideRevision == revision && guideStage == save.stage && guideRoom == save.room && guideRunes == runeProgress && guideTracked == save.trackedQuest) return;
            bool english = save.english; QuestGuide zh, en;
            try { save.english = false; zh = CurrentGuide; save.english = true; en = CurrentGuide; }
            finally { save.english = english; }
            string signature = en.id + "/" + en.room + "/" + en.next + "/" + en.complete;
            if (previousGuide != null && previousGuide != signature)
                RecordMoment(zh.complete ? "完成：" + zh.title : "下一步：" + zh.next, en.complete ? "Complete: " + en.title : "Next: " + en.next);
            previousGuide = signature;
            guideRevision = revision; guideStage = save.stage; guideRoom = save.room; guideRunes = runeProgress; guideTracked = save.trackedQuest;
        }
        private void ActionUnavailable(float cooldown, float cost)
        { if (actionHintTimer > 0) return; actionHintTimer = 1.5f; Notify(cooldown > 0 ? "正在冷却，剩余 " + Math.Ceiling(cooldown) + " 秒。" : "耐力不足，需要 " + (int)cost + "。", cooldown > 0 ? "Cooling down: " + Math.Ceiling(cooldown) + " seconds." : "Need " + (int)cost + " energy."); sound = "deny"; }
        public string EnemyHint(Enemy e)
        {
            // The live contextual enemy inspection card reflects the real
            // battle clock, rather than promising an always-open guard.
            if(e!=null&&!e.boss&&e.kind==2&&e.armorOpenTime>.001f)
                return L("重甲已破 · 还有 "+(int)Math.Ceiling(e.armorOpenTime)+
                    " 秒可从正面追击。抓住这次侧袭机会。",
                    "ARMOR OPEN · "+(int)Math.Ceiling(e.armorOpenTime)+
                    "s to strike from the front. Press the flank advantage.");
            return e.boss ? L("闪开红圈；薄荷色环出现时反击，伤害提升", "Evade the red mark; counter during the mint-ring opening") : e.kind == 1 ? L("侧移躲箭，借墙掩护；射后薄荷色环是破绽", "Sidestep arrows, use walls; the mint ring after firing marks an opening") : e.kind == 2 ? L("重甲正面减伤；绕背侧袭，或抓住抡击后的薄荷色环", "Heavy armor guards the front; flank or punish its mint-ring recovery") : L("闪过扑击；薄荷色环出现时抓住反击窗口", "Dodge the lunge; punish the mint-ring recovery"); }
        public void StartPractice()
        {
            if (Dying) return;
            if (save.room != 0 || mode == "title") { Notify("回到余烬镇，在训练场重新练习。", "Return to Emberfall to use the practice yard."); return; }
            if(practicing||SpecialistDrillActive)
            {
                if(SpecialistDrillActive)Notify("先结束本次Q考核，再进行基础训练。",
                    "Finish or abort this Q proficiency before starting the basic course.");
                return;
            }
            Close(); ResetCombatPresentation(); practicing = true; practiceStage = 0; practiceReturn = Position;
            enemies.Clear(); projectiles.Clear(); hazards.Clear(); save.x = 14; save.y = 13; invulnerable = 0; save.energy = 100;
            attackCooldown = dashCooldown = dashTime = skillCooldown = guardTime = 0; ClearFrost();
            PracticeEnemy(); Notify("训练场：先命中练习木桩。练习不会损失生命或物资。", "Practice yard: hit the dummy. Health and supplies are safe.");
        }
        private void PracticeEnemy()
        { enemies.Clear(); projectiles.Clear(); var e = new Enemy(90001, practiceStage == 1 ? 2 : 0, 16, 13, practiceStage == 2 ? 15 : 9999); e.timer = practiceStage == 1 ? .3f : 9999; e.facing = new Point(-1, 0); enemies.Add(e); }
        private void PracticeHit()
        {
            if (!practicing) return;
            if (practiceStage == 0) { save.practiceBits |= 1; TutorialAction(3); practiceStage = 1; PracticeEnemy(); Notify("红圈出现后，向圈外闪避；或在命中前闪避穿过。", "When the red circle appears, dodge out or through the hit."); }
        }
        private void PracticeDodge()
        { if (!practicing || practiceStage != 1) return; save.practiceBits |= 2; TutorialAction(4); practiceStage = 2; save.energy = 100; skillCooldown = 0; PracticeEnemy(); Notify("现在用 Q 职业技能命中目标，完成综合练习。", "Now hit the target with Q to finish your combat practice."); }
        private void PracticeSkill()
        {
            if (!practicing || practiceStage != 2) return;
            save.practiceBits = 7; TutorialAction(5); bool first = !Has("practice_reward"); EndPractice();
            if (first) { Flag("practice_reward"); CreditSkillPoints(1); CreditCoins(15); }
            RecordMoment("完成训练场：攻击、观察预告、闪避与职业战技。", "Practice complete: attack, read a tell, dodge and use your technique.");
            Notify(first ? "练习完成：技能点1、硬币15！可以出发去密林了。" : "练习完成！随时可以再来。", first ? "Practice complete: 1 point, 15 coins. Ready for Thornwood." : "Practice complete! Return anytime.");
        }
        public void EndPractice()
        {
            if (!practicing) return;
            practicing = false;
            CancelAction(); companionAction = null;
            movementDirection = new Point(); companionMoving = false;
            // The practice dummy can own timed strikes. They must not survive
            // in a playable town after the safe tutorial is aborted/completed.
            enemies.Clear(); projectiles.Clear(); effects.Clear(); hazards.Clear();
            save.x = practiceReturn.x; save.y = practiceReturn.y; save.energy = 100;
            attackCooldown = dashCooldown = dashTime = skillCooldown = guardTime = 0;
            combo = 0; comboTimer = riposteTime = 0;
            perfectDodgeArmed = false; dodgeEscapeTime = 0; dashOrigin = new Point();
            hitStop = playerHitTime = playerFlash = impactShake = 0;
            ClearFrost(); invulnerable = 1; revision++;
        }
        public string PracticeHint { get { return L(new[] { "J / 左键：命中木桩", "观察红圈，空格闪避攻击", "Q：命中目标，完成练习" }[practiceStage], new[] { "J / click: hit the dummy", "Read the red circle; Space to dodge", "Q: hit the target to finish" }[practiceStage]); } }
        public void UseExplorationAbility()
        {
            if (mode != "world" || Dying || practicing) return;
            WorldObject near = null; float distance = 3;
            foreach (WorldObject o in Map.objects)
            {
                if (!o.kind.StartsWith("seal")) continue;
                Point objective = new Point(o.x, o.y);
                float d = (Position - objective).Length;
                if (d >= distance) continue;
                // R and E both require a physically reachable contact.
                // Authored seals sit ON a blocked route tile, so test the
                // player-facing reach just before the fixture, rather than
                // invalidating every legitimate near-side activation.
                Point contact = d > .55f ?
                    objective - (objective - Position).Normalized * .55f :
                    Position;
                if (!ClearAttackPath(Position, contact)) continue;
                near = o; distance = d;
            }
            if (near == null) { Notify("靠近封印入口按 R；探索能力来自前三枚星核。", "Press R near a sealed route; the first three cores grant exploration powers."); return; }
            int id = int.Parse(near.kind.Substring(4));
            if (!Ability(id)) { Notify("需要第 " + id + " 枚星核的探索能力。", "Requires exploration power from core " + id + "."); return; }
            string flag = "seal_" + save.room; if (Has(flag)) return;
            Flag(flag); effects.Add(new Effect("discover", new Point(near.x, near.y), .6f));
            if (id == 2 && save.room == 5) save.tideHeldUntil = save.totalTime + 60;
            RecordMoment("打开隐藏路线：" + near.name + "。", "Hidden route opened: " + near.en + ".");
            Notify("封印解除！走入支路，寻找旧日补给。", "Seal released! Explore the side route for forgotten supplies.");
        }
        // The Inn chart shares its existing small dialogue panel with the relief route.
        // Add one submenu entry, never seven or more clipped choices.
        private void AddFarshoreReliefChartChoice()
        {
            if (save.room != 9 || !Has("farshore_relief_complete") || conversation == null) return;
            var choices = new List<Choice>(conversation.choices);
            choices.Add(C("远岸平民避难路网", "Farshore civilian relief routes", "farshore_relief_routes"));
            conversation.choices = choices.ToArray();
        }
        private bool TalkPolish(WorldObject o)
        {
            if (TalkWorldChronicle(o)) return true;
            if (TalkAtlasEcho(o)) return true;
            if (TalkArchiveTrail(o)) return true;
            if (TalkRootHarmony(o)) return true;
            if (TalkCommonsRelay(o)) return true;
            if (TalkSkywatch(o)) return true;
            if (TalkGlasswind(o)) return true;
            if (TalkTideSalvage(o)) return true;
            if (TalkTemper(o)) return true;
            if (TalkTriad(o)) return true;
            if (TalkAfterlight(o)) return true;
            if (TalkHearing(o)) return true;
            if (TalkStorm(o)) return true;
            if (TalkWitness(o)) return true;
            if (TalkBlackout(o)) return true;
            if (TalkWayfarer(o)) return true;
            if (TalkRoadwardenCipher(o)) return true;

            if (o.kind.StartsWith("survey"))
            {
                if (o.kind == "surveyDesk")
                {
                    // A distant direct-reference reader may inspect the
                    // original text, but no operational choice can commit
                    // unless physically present at its source.
                    int found = 0;
                    for (int i = 1; i <= 3; i++) if (Has("survey_" + i)) found++;
                    if (Has("survey_complete"))
                    {
                        Dialog(o.name, o.en, "三段信号已经绘成一条新路。旧巡路队留下的不是宝藏坐标，而是能让人平安回家的路。你的步伐也因此更轻快。\n\n信标已经连通：只要附近没有敌人，就能从星图桌前往三座信标，或从信标返回旅店。" + (Has("dispatch_complete") ? "\n\n巡路誓言已解开。三座信标现在开放双波守路试炼，守住全部信标还能获得技能点。" : "\n\n密林、遗迹和矿场各有一封未寄出的旧信。重读它们，解开巡路队最后的誓言。"), "The three signals form a safe road home, not a treasure map. The wardens left a route for anyone who needed shelter. You now travel a little faster.\n\nThe beacons are linked. When enemies are clear, travel from this chart to any of the three beacons, or return to the inn from a beacon." + (Has("dispatch_complete") ? "\n\nThe promise is decoded. All three beacons now offer two-wave defense drills. Defend them all for a talent point." : "\n\nThree unsent letters in those regions hide the wardens' final promise. Reread them to decode it."), C("收好地图", "Keep the map", "close"), C("前往苔根信标 · 密林", "Travel to Mossroot · Thornwood", "survey_to_1"), C("前往潮痕信标 · 遗迹", "Travel to Tidemark · Ruins", "survey_to_2"), C("前往矿灯信标 · 矿场", "Travel to Minelight · Mine", "survey_to_3"));
                        if (Has("dispatch_complete"))
                        {
                            var choices = new List<Choice>(conversation.choices);
                            choices.Add(C("重读巡路队的誓言", "Reread the wardens\u0027 promise", "dispatch_recap"));
                            conversation.choices = choices.ToArray();
                        }
                        AddFarshoreReliefChartChoice();
                        return true;
                    }
                    if (found < 3)
                    {
                        Dialog(o.name, o.en, "桌上的星图缺少 " + (3 - found) + " 段巡路信号。密林、沉没遗迹和矿场各有一座旧信标。靠近并按 E，听完留下的讯息。", "The chart still lacks " + (3 - found) + " road signals. Find the old beacons in Thornwood, the Drowned Ruins and Ironreach. Approach each one and press E.", C("继续寻找", "Keep searching", "close"));
                        AddFarshoreReliefChartChoice();
                        return true;
                    }
                    Dialog(o.name, o.en, "三段讯息合起来是一条避开塌桥、洪水和矿坑的安全归途。把坐标抄给旅店，所有晚归的人都能借这张图回家。", "Together, the three signals chart a safe return past broken bridges, floods and mine shafts. Leave a copy at the inn so every late traveler can find the way home.", C("完成巡路图 · 永久移速提升", "Complete chart · permanent travel speed", "survey_finish"), C("稍后再来", "Return later", "close"));
                    AddFarshoreReliefChartChoice();
                    return true;
                }
                if (!EnsurePhysicalInteractionReach(o)) return true;
                int signal = int.Parse(o.kind.Substring(6));
                if (signal < 1 || signal > 3) return true;
                string[] notesZh = { "", "密林记录：树根在夜里替迷路的人指路。公会要我们标出矿脉，却不准画出村庄。", "遗迹记录：水涨之前，旧桥的信号被改成了指向堡垒。有人偷偷保留了安全的退路。", "矿场记录：最后一班巡路员把归途藏在三段信号里，托付给任何愿意带人回家的人。" };
                string[] notesEn = { "", "Forest signal: The roots guided lost travelers at night. The guild ordered us to chart ore veins, but not the villages.", "Ruins signal: Before the flood, the bridge marker was redirected toward the citadel. Someone kept a safe return route.", "Mine signal: The last wardens split the way home into three signals, trusting anyone willing to bring others back." };
                bool first = !Has("survey_" + signal);
                if (first)
                {
                    Flag("survey_" + signal); CreditCoins(12); CreditShards(1); AddXp(15); revision++; sound = "reward";
                    RecordMoment("恢复巡路信号 " + signal + "/3：" + o.name + "。", "Recovered road signal " + signal + "/3: " + o.en + ".");
                }
                var signalChoices = new List<Choice> { C("记住这条路", "Remember the road", "close") };
                if (Has("survey_complete") && roadTrialRoom != signal) signalChoices.Add(C("沿巡路信号返回炉火旅店", "Follow the signal back to the Hearth Inn", "survey_home"));
                if (roadTrialRoom == signal)
                {
                    if (RoadwardenWardRepairAvailable)
                        signalChoices.Add(C("花费35耐力 · 修复护盾25点", "Spend 35 energy · restore 25 ward", "road_trial_repair"));
                    signalChoices.Add(C("放弃守路试炼", "Abandon defense drill", "road_trial_abandon"));
                }
                else if (Has("dispatch_complete"))
                    signalChoices.Add(Has("road_trial_" + signal)
                        ? C("再战三波精英守路试炼（无重复奖励）", "Replay three-wave veteran defense (no repeat reward)", "road_trial_start_" + signal)
                        : C("挑战双波守路试炼", "Begin two-wave defense", "road_trial_start_" + signal));
                Dialog(o.name, o.en, notesZh[signal] + (first ? "\n\n信号已记录：12硬币、1碎片、15经验。到炉火旅店拼合三段路线。" : Has("survey_complete") ? "\n\n归乡星图已连通。附近安全时，可以沿信号返回旅店。" : "\n\n讯息已记录，可在旅店的星图桌查看进度。") + RoadwardenTrialHint(signal, false), notesEn[signal] + (first ? "\n\nSignal recorded: 12 coins, 1 shard, 15 XP. Assemble all three at the Hearth Inn." : Has("survey_complete") ? "\n\nThe homeward chart is linked. Return to the inn when nearby enemies are clear." : "\n\nSignal already recorded. Check the chart table at the Hearth Inn.") + RoadwardenTrialHint(signal, true), signalChoices.ToArray());
                return true;
            }

            if (o.kind == "mentor")
            {
                var options=new System.Collections.Generic.List<Choice>();
                if(!SpecialistDrillActive)
                    options.Add(C("基础战斗训练","Basic combat course","practice"));
                if(save.profession>0)
                    options.Add(SpecialistDrillActive?
                        C("结束Q实战考核","Abort Q proficiency","specialist_drill_abort"):
                        C(SpecialistDrillComplete?"Q实战复练 · 无重复奖励":"Q实战考核 · 首次永久节省5耐力",
                            SpecialistDrillComplete?"Replay Q proficiency · no duplicate reward":
                            "Q proficiency · permanently save 5 energy","specialist_drill_start"));
                options.Add(C("以后再来","Later","close"));
                Dialog(o.name,o.en,
                    "木桩训练能教会你攻防。游侠和法师还有一场更难的考核：得靠三次分别释放的Q真的打中靶子，一轮箭雨不能算成三次。",
                    "The basic course teaches strikes and dodges. Rangers and arcanists can take a harder trial: land three separate genuine Q contacts, not three arrows from one volley.",
                    options.ToArray());return true;
            }
            if (o.kind == "returnscout")
            { if (!Has("scout_saved")) return true; Dialog(o.name, o.en, "我回来了。你替我打开的是回家的路。公会的记录里现在有我的名字；也请记住，被星核伤害的土地上还住着人。", "I'm home. You opened my way back. My name is in the guild records now. Remember: people live on the land the cores are draining.", C(Has("scout_gift") ? "一路平安" : "收下谢礼：炖汤1", Has("scout_gift") ? "Stay safe" : "Accept thanks: 1 stew", "scoutgift")); return true; }
            if (o.kind.StartsWith("seal"))
            {
                if (!EnsurePhysicalInteractionReach(o)) return true;
                UseExplorationAbility(); return true;
            }
            if (o.kind.StartsWith("cache"))
            {
                if (!EnsurePhysicalInteractionReach(o)) return true;
                if (!Has("seal_" + save.room)) { Notify("先解除入口的封印。", "Release the entrance seal first."); return true; }
                string flag = "cache_" + save.room; bool first = !Has(flag);
                if (first) { Flag(flag); CreditCoins(30); CreditShards(3); CreditInventory(6, 1); save.mastery[save.weapon] = Math.Min(100, save.mastery[save.weapon] + 8);
                    RecordMoment("发现旧日补给：" + Map.name + "。硬币30、碎片3、炖汤1、熟练度8。", "Found a forgotten cache in " + Map.en + ": 30 coins, 3 shards, 1 stew, 8 mastery."); }
                LoreData note = o.lore >= 0 && o.lore < data.lore.Length ? data.lore[o.lore] : null;
                if (note != null) Flag("history_" + o.lore);
                string zh = note == null ? "旧巡路队的补给已经找回。答应带回来的人，要记得带回来。" : note.zh;
                string en = note == null ? "The road wardens' supplies are recovered. Remember the people you promised to bring home." : note.text;
                if (first) { zh += "\n\n获得硬币30、碎片3、炖汤1、熟练度8。记录可在手帐重读。"; en += "\n\nGained 30 coins, 3 shards, 1 stew and 8 mastery. Reread this note in your journal."; }
                Dialog(note == null ? "边境旧日补给" : note.title, note == null ? "Forgotten frontier cache" : note.en, zh, en, C(first ? "记录发现" : "收起记录", first ? "Record discovery" : "Close record", "close")); return true;
            }
            return false;
        }
        private bool ChoosePolish(string action)
        {
            if (ChooseRootHarmony(action)) return true;
            if (ChooseCommonsRelay(action)) return true;
            if (ChooseSkywatch(action)) return true;
            if (ChooseGlasswind(action)) return true;
            if (ChooseTideSalvage(action)) return true;
            if (ChooseTemper(action)) return true;
            if (ChooseTriad(action)) return true;
            if (ChooseWitness(action)) return true;
            if (ChooseBlackout(action)) return true;
            if (ChooseAfterlight(action)) return true;
            if (ChooseHearing(action)) return true;
            if (ChooseRoadCharter(action)) return true;
            if (ChooseUnlisted(action)) return true;
            if (ChooseAnnexExploration(action)) return true;
            if (ChooseOuterRescue(action)) return true;
            if (ChooseOuterPatrol(action)) return true;
            if (ChooseRidge(action)) return true;
            if (ChooseFarStation(action)) return true;
            if (ChooseStorm(action)) return true;
            if (action == "farshore_relief_routes")
            {
                if (save.room != 9 || !Has("farshore_relief_complete")) return true;
                Point desk = ObjectPoint(9, "surveyDesk", new Point(-100, -100));
                if ((Position - desk).Length > 2.2f) return true;
                Dialog("远岸平民避难路网", "Farshore Civilian Relief Network",
                    "三处工程开放后，巡路员与地方居民共同标注了可通行的避难补给道。路况危险时不能传送；原来的章节关卡仍然有效。",
                    "The wardens and three communities have charted supply roads between the Inn and their rebuilt refuges. Hostile roads remain unsafe and campaign gates still apply.",
                    C("前往雾港救援浮桥", "Travel to Mistport pontoons", "farshore_relief_to_5"),
                    C("前往沙海遮阳道", "Travel to the desert shade lane", "farshore_relief_to_6"),
                    C("前往高原保暖棚", "Travel to Frostchime shelter", "farshore_relief_to_7"),
                    C("返回归乡星图", "Back to the homeward chart", "farshore_relief_return"),
                    C("离开", "Leave", "close"));
                return true;
            }
            if (action == "farshore_relief_return")
            {
                // Continue on the EXACT source that opened this menu.
                // Scanning the map for an arbitrary replacement desk could
                // switch the NPC window or make a removed table clickable.
                WorldObject original=pendingDialogueSource;
                if(save.room==9 && original!=null &&
                    original.kind=="surveyDesk" &&
                    SourceBelongsToActiveMap(original) &&
                    HasPhysicalInteractionReach(original))
                    Talk(original);
                else
                    Notify("请回到原先的星图桌现场重新打开路网。",
                        "Return to the original physical chart to open its routes.");
                return true;
            }
            if (ChooseWorldChronicle(action)) return true;
            if (ChooseAtlasEcho(action)) return true;
            if (ChooseArchiveTrail(action)) return true;
            if (ChooseWayfarer(action)) return true;
            if (ChooseRoadwardenCipher(action)) return true;
            if (ChooseRoadwardenTrial(action)) return true;
            if (action == "survey_home" || action.StartsWith("survey_to_"))
            {
                // A completed chart opens travel between existing, already discovered
                // landmarks; it never bypasses region progression or nearby combat.
                if (!Has("survey_complete") || roadTrialRoom != 0 || reliefMissionRoom != 0 || practicing || Dying || !SafeTravel)
                {
                    Notify("敌人尚未退去，无法稳定巡路信号。先脱离战斗。", "Enemies disrupt the road signal. Leave combat before traveling.");
                    return true;
                }
                if (action == "survey_home")
                {
                    if (save.room < 1 || save.room > 3 || !Has("survey_" + save.room)) return true;
                    Enter(9);
                    Point nearDesk = new Point(15, 15);
                    if (save.room == 9 && CanStand(nearDesk)) { save.x = nearDesk.x; save.y = nearDesk.y; }
                    Notify("归乡信号将你带回炉火旅店。", "The homeward signal guides you back to the Hearth Inn.");
                    return true;
                }
                int destination;
                if (save.room != 9 || !int.TryParse(action.Substring(10), out destination) || destination < 1 || destination > 3 || !Has("survey_" + destination)) return true;
                if (!CanEnter(destination)) { Notify(RouteHint(destination, false), RouteHint(destination, true)); return true; }
                Enter(destination);
                if (save.room != destination) return true;
                foreach (var beacon in Map.objects)
                {
                    if (beacon.kind != "survey" + destination) continue;
                    Point anchor = new Point(beacon.x, beacon.y);
                    // Place the hero beside the beacon, never on an impassable
                    // tile or across a sealed path; the normal map spawn remains
                    // a safe fallback if future level art changes this clearing.
                    Point[] approaches = {
                        new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0),
                        new Point(1, 1), new Point(-1, 1), new Point(1, -1), new Point(-1, -1)
                    };
                    // A valid tile may still sit directly beside a freshly
                    // spawned patrol. Prefer the clearest adjacent landing while
                    // keeping the beacon within immediate interaction range.
                    Point safest = Position; float bestClearance = -1; bool found = false;
                    foreach (Point offset in approaches)
                    {
                        Point candidate = anchor + offset;
                        if (!CanStand(candidate)) continue;
                        float clearance = 9999;
                        foreach (Enemy enemy in enemies)
                            if (enemy.hp > 0) clearance = Math.Min(clearance, (enemy.Position - candidate).Length);
                        if (found && clearance <= bestClearance) continue;
                        safest = candidate; bestClearance = clearance; found = true;
                    }
                    // A future terrain edit may close every cardinal/diagonal
                    // approach. The beacon tile itself is validated as walkable
                    // by the campaign's map checks, so use it as a last-resort
                    // landing rather than silently leaving the hero at Enter()'s
                    // unrelated spawn (which may not be walkable after map edits).
                    if (!found && CanStand(anchor))
                    {
                        safest = anchor;
                        found = true;
                    }
                    if (found)
                    {
                        save.x = safest.x;
                        save.y = safest.y;
                    }
                    else if (!CanStand(Position))
                    {
                        Notify("信标附近没有可通行的落点，请返回旅店后重试。",
                            "No walkable landing is available near this beacon. Return to the inn and try again.");
                        return true;
                    }
                    break;
                }
                Notify("巡路信号接通。你抵达了已恢复的信标。", "Road signal connected. You arrive beside a restored beacon.");
                return true;
            }
            if (action == "survey_finish")
            {
                if (!Has("survey_complete") && Has("survey_1") && Has("survey_2") && Has("survey_3"))
                {
                    Flag("survey_complete"); CreditCoins(60); CreditShards(5); CreditSkillPoints(1); AddXp(50); revision++; sound = "reward";
                    RecordMoment("三段巡路信号拼成了归乡星图：永久移动速度+0.25，技能点1。", "The road signals became a homeward chart: +0.25 permanent movement speed and 1 talent point.");
                    Notify("巡路图完成！移速+0.25、技能点+1，三座信标快速往返已解锁！", "Chart complete! +0.25 speed, +1 talent point; fast travel to all three beacons unlocked!");
                }
                return true;
            }
            if (action == "practice") { StartPractice(); return true; }
            if (action == "scoutgift")
            {
                // A one-time gift is not collected until the stew is stored.
                // Leaving the gift unclaimed lets the player clear a stack
                // and return without permanently losing it on the next load.
                if (Has("scout_saved") && !Has("scout_gift") && HasInventoryRoom(6, 1))
                {
                    CreditInventory(6, 1); Flag("scout_gift");
                    RecordMoment("侦察员回家了，并送来一份热汤。", "The scout made it home and brought you a warm stew.");
                }
                return true;
            }
            return false;
        }
        private void CaptureWorld()
        {
            save.savedRunes = runeProgress; if (practicing || InAbyss) { save.snapshotRoom = -1; save.worldEnemies = null; return; }
            save.snapshotRoom = save.room;
            var records = new List<EnemyRecord>();
            foreach (var e in enemies)
            {
                // Temporary trial attackers restart after loading; never save
                // them as ordinary world patrols.
                if (IsRoadwardenTrialEnemy(e) || IsWayfarerEnemy(e) || IsAtlasEchoEnemy(e) ||
                    IsReliefMissionEnemy(e) || IsRootHarmonyEnemy(e) ||
                    IsCommonsRelayEnemy(e) || IsSkywatchEnemy(e) || IsAfterlightWatchEnemy(e) || IsFarCourierRaider(e) || IsCivicResponseEnemy(e) || IsLastDirectiveEnemy(e) || IsGlasswindEnemy(e) || IsTideSalvageEnemy(e) || IsTemperEnemy(e) || IsMineConvoyEnemy(e) || IsFrostSignalEnemy(e) || IsTriadEnemy(e) || IsWitnessEnemy(e) ||
                    IsCivicCourierGuard(e) ||
                    // The contract itself is transient. Never write its
                    // reference-owned raiders into an ordinary v2 world
                    // snapshot that would resurrect them without a mission.
                    IsFieldPatrolEnemy(e) || IsVantageWatchEnemy(e) ||
                    IsChapterRoadEnemy(e) || IsNightShiftRaider(e)) continue;
                records.Add(new EnemyRecord { id = e.id, kind = e.kind, boss = e.boss, x = e.x, y = e.y, hp = e.hp, maxHp = e.maxHp });
            }
            save.worldEnemies = records.ToArray();
        }
        private void RestoreWorld()
        {
            runeProgress = save.savedRunes; if (InAbyss || save.snapshotRoom != save.room || save.worldEnemies == null) return;
            enemies.Clear(); var ids = new HashSet<int>();
            // The old v2 loader deliberately rescues an isolated enemy whose
            // saved coordinates are damaged. Prefer a valid position if a
            // duplicate ID also exists; only fall back to spawn when *no*
            // legitimate-position entry represents that original enemy.
            var positionedIds = new HashSet<int>();
            foreach (EnemyRecord candidate in save.worldEnemies)
                if (candidate != null && candidate.id > 0 && candidate.id <= 99999 &&
                    Finite(candidate.x) && Finite(candidate.y) &&
                    candidate.x >= 0 && candidate.y >= 0 &&
                    candidate.x < Map.rows[0].Length && candidate.y < Map.rows.Length &&
                    Finite(candidate.hp) && Finite(candidate.maxHp) &&
                    candidate.hp > 0 && candidate.maxHp > 0)
                    positionedIds.Add(candidate.id);
            foreach (var r in save.worldEnemies)
            {
                if (r == null || r.id < 1 || r.id > 99999 ||
                    (r.id >= 88000 && r.id < 88100) ||
                    (r.id >= 89500 && r.id < 89600) ||
                    r.id == 83001 || r.id == 89701 ||
                    (r.id >= 89810 && r.id < 89900) ||
                    (r.id >= 89920 && r.id < 89930) ||
                    (r.id >= 89940 && r.id < 89950) ||
                    r.id == 89960 ||
                    (r.id >= 89972 && r.id <= 89977) ||
                    (r.id >= 90100 && r.id < 90116) ||
                    (r.id >= 90200 && r.id < 90216) ||
                    (r.id >= 90300 && r.id <= 90304) ||
                    (!Finite(r.x) || !Finite(r.y) || r.x < 0 || r.y < 0 ||
                     r.x >= Map.rows[0].Length || r.y >= Map.rows.Length) &&
                        positionedIds.Contains(r.id) ||
                    !Finite(r.hp) || !Finite(r.maxHp) || r.hp <= 0 ||
                    r.boss && Has("boss_" + save.room) || !ids.Add(r.id)) continue;
                // Isolated damaged positions recover through SafeActorFoot as
                // before; malformed duplicates cannot preempt a valid actor.
                Point p = SafeActorFoot(new Point(r.x, r.y));
                var e = new Enemy(r.id, (int)Clamp(r.kind, 0, 3), p.x, p.y, Clamp(r.maxHp, 1, 5000), r.boss); e.hp = Clamp(r.hp, .1f, e.maxHp); enemies.Add(e); enemyId = Math.Max(enemyId, r.id);
            }
            // SpawnRoom instances were replaced by reconstructed world
            // actors. Rebind the true patrols after restore completes.
            RebindRidgeWatchAfterWorldRestore();
            // The ordinary snapshot intentionally excludes mission-owned
            // actors. Its reconstruction replaced the SpawnRoom list, so
            // reconnect a NEW real medical-route archer using surviving V2
            // contract flags instead of pointing at a discarded instance.
            ResetCivicCourierGuard();
            SpawnCivicCourierGuard();
        }
    }
}
