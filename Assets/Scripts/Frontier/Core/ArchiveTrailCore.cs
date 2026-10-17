using System;

namespace Starfall
{
    // A distinct story expedition spanning the original three campaign regions.
    // Existing save flags own progress; the atlas never bypasses story gates.
    public sealed partial class GameCore
    {
        private bool TalkArchiveTrail(WorldObject o)
        {
            if (o.kind == "archivekeeper")
            {
                if (save.room != 0) return true;
                if (Has("archive_complete"))
                {
                    Dialog(o.name, o.en,
                        "你找回的不是一张藏宝图。那是测绘师为每一个后来者留下的撤离路线。如今公会把它挂在大厅里，提醒人们别把归途当作理所当然。",
                        "It was never a treasure map. The surveyor drew escape routes for the people who would follow. The guild now displays the atlas as a promise of safe passage.",
                        C("再看看修好的星图", "Admire the restored atlas", "close"));
                    return true;
                }
                if (!Has("archive_started"))
                {
                    Dialog(o.name, o.en,
                        "我叫琳，负责修复边境的旧地图。最后一位测绘师留下的星图被拆成了三份：林地的残图、遗迹里的潮刻石板、矿场的测距仪。你愿意帮我找回它们吗？\n\n先去荆棘密林东侧寻找折断的航图，余下线索会写在每件物品上。",
                        "I'm Lin, conservator of the frontier maps. The last surveyor left three records: a torn woodland chart, a tide inscription in the ruins, and a rangefinder in the mine. Find the woodland chart first; each artifact points to the next.",
                        C("接受测绘师委托", "Accept the atlas commission", "archive_accept"),
                        C("以后再来", "Maybe later", "close"));
                    return true;
                }
                if (Has("archive_fragment_3"))
                    Dialog(o.name, o.en,
                        "三份记录齐了。测绘师没有标出黄金，而是标出了被洪水切断的安全路、矿坑坍塌前的撤离坡道，以及穿越林地的夜间方向。让我把它们重新拼起来。",
                        "All three records are here. They chart safe routes through floods, collapsing mines and the night forest, not buried gold. Let me restore the atlas.",
                        C("交付三份记录", "Restore and deliver the atlas", "archive_complete"),
                        C("稍后再来", "Return later", "close"));
                else Dialog(o.name, o.en,
                    "测绘师的三份记录必须按顺序阅读。下一件藏在" +
                    (!Has("archive_fragment_1") ? "荆棘密林的东侧道路。" : "沉没遗迹靠近西侧的旧刻石。"),
                    "Read the records in sequence. The next is " +
                    (!Has("archive_fragment_1") ? "east of the Thornwood path." : "near the western tide-carved stones of the Sunken Ruins."),
                    C("查看任务手帐", "Check the journal", "archive_track"),
                    C("我继续寻找", "Keep searching", "close"));
                return true;
            }
            if (!o.kind.StartsWith("archivefragment")) return false;
            int region;
            if (!int.TryParse(o.kind.Substring("archivefragment".Length), out region) ||
                region < 1 || region > 3 || save.room != region) return true;
            if (!Has("archive_started"))
            {
                Notify("一份被风沙磨损的旧记录。镇上的修复师也许知道它的来历。",
                    "An old expedition record. A conservator in town might recognize it.");
                return true;
            }
            if (Has("archive_fragment_" + region))
            {
                Dialog(o.name, o.en, "你已把这份记录拓印进手帐。原件不必重复领取。",
                    "You already copied this record into the journal. It cannot be claimed twice.",
                    C("收起手帐", "Close journal", "close"));
                return true;
            }
            if (region > 1 && !Has("archive_fragment_" + (region - 1)))
            {
                Dialog(o.name, o.en, "密密麻麻的旧符号缺少参照。先找回上一段航图，才能读懂这里的刻痕。",
                    "The marks need an earlier reference. Find the previous chart before deciphering this record.",
                    C("先调查别处", "Search elsewhere first", "close"));
                return true;
            }
            string[] zh = {
                "航图边缘写着：海水倒灌时，去西边寻找潮刻石板。那上面记着第二段归途。",
                "潮刻石板记载着一条撤离矿场的山路。最后的测距仪被留在赤铁矿场东侧。",
                "测距仪上刻着所有工人的名字，背面写着：不要忘记给后来者留下回家的方向。"
            };
            string[] en = {
                "The chart reads: when the waters rise, seek the tide inscription on the western ruins. It carries the second route.",
                "The inscription charts a retreat from the mine. The final rangefinder lies east of the Rediron workings.",
                "Every worker's name is engraved on the rangefinder. Its reverse reads: leave a way home for those who follow."
            };
            Dialog(o.name, o.en, zh[region - 1], en[region - 1],
                C("拓印并收好记录", "Copy and secure the record", "archive_claim_" + region),
                C("暂时放下", "Leave it for now", "close"));
            return true;
        }
        private bool ChooseArchiveTrail(string action)
        {
            if (action == "archive_track")
            {
                if (Has("archive_started")) TrackQuest("archive_trail");
                return true;
            }
            if (action == "archive_accept")
            {
                if (save.room == 0 && !Has("archive_started") && !Dying)
                {
                    Flag("archive_started"); TrackQuest("archive_trail"); revision++;
                    Notify("新支线：失落测绘师的星图。先去密林寻找残图。",
                        "New quest: The Lost Surveyor's Atlas. Find the woodland chart.");
                }
                return true;
            }
            if (action == "archive_complete")
            {
                if (save.room != 0 || !Has("archive_started") || Has("archive_complete") ||
                    !Has("archive_fragment_1") || !Has("archive_fragment_2") ||
                    !Has("archive_fragment_3") || Dying) return true;
                Flag("archive_complete"); CreditCoins(60); CreditSkillPoints(1);
                AddXp(60); revision++; sound = "reward";
                RecordMoment("失落测绘师的星图已修复：获得60硬币、1技能点。归途的秘密不是宝藏，而是活下来的路。",
                    "The lost atlas is restored: +60 coins, +1 talent point. Its treasure was the road home.");
                Notify("星图已归档！获得60硬币、1技能点、60经验。",
                    "Atlas restored! +60 coins, +1 talent point, +60 XP.");
                return true;
            }
            if (!action.StartsWith("archive_claim_")) return false;
            int region;
            if (!int.TryParse(action.Substring("archive_claim_".Length), out region) ||
                region < 1 || region > 3 || save.room != region || Dying ||
                !Has("archive_started") || Has("archive_fragment_" + region) ||
                (region > 1 && !Has("archive_fragment_" + (region - 1)))) return true;
            WorldObject artifact = null;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "archivefragment" + region) { artifact = o; break; }
            if (artifact == null || (Position - new Point(artifact.x, artifact.y)).Length > 2.2f)
                return true;
            Flag("archive_fragment_" + region); CreditCoins(20); CreditShards(2);
            AddXp(20); revision++; sound = "reward";
            effects.Add(new Effect("discover", new Point(artifact.x, artifact.y), .7f));
            RecordMoment(region == 1 ? "林地残图指出遗迹里的潮刻石板。" :
                region == 2 ? "潮刻石板指向赤铁矿场的旧测距仪。" :
                "测距仪留下了矿工们的名字和回家的方向。",
                region == 1 ? "The woodland chart points to the tide inscription." :
                region == 2 ? "The tide inscription points to the mine rangefinder." :
                "The rangefinder preserves the workers' names and their route home.");
            Notify("记录已收集！硬币20、碎片2、经验20。",
                "Record secured! +20 coins, +2 shards, +20 XP.");
            return true;
        }

        // The Starroot Counterpoint: an optional, original three-region
        // investigation with distinct lore readings and real enemy defense.
        // Persistent flags are only written once a physical trial is cleared.
        public int RootHarmonyRoom { get; private set; }
        public float RootHarmonyTime { get; private set; }
        private float rootHarmonyPulseTime;
        public bool IsRootHarmonyEnemy(Enemy e)
        { return e != null && e.id >= 88000 && e.id < 88100; }
        private void ResetRootHarmonyTrial()
        {
            enemies.RemoveAll(IsRootHarmonyEnemy);
            projectiles.RemoveAll(p => p.rootHarmonyOwned);
            hazards.RemoveAll(h => h.rootHarmonyOwned);
            effects.RemoveAll(e => e.rootHarmonyOwned);
            RootHarmonyRoom = 0; RootHarmonyTime = 0;
            rootHarmonyPulseTime = 0;
        }
        public bool RootHarmonyRestoring
        {
            get {
                return save.room >= 1 && save.room <= 3 &&
                    RootHarmonyRoom == 0 && !Dying &&
                    Has("root_harmony_" + save.room) &&
                    (Position - ObjectPoint(save.room, "rootcounter" + save.room,
                        new Point(-100, -100))).Length < 2.75f && SafeTravel;
            }
        }
        private void TickRootHarmonyTrial(float dt)
        {
            if (RootHarmonyRestoring)
            {
                // A calibrated stone remains a tangible world reward.
                // Its short-radius refuge cannot out-heal a nearby battle
                // or silently supply money, items or infinite XP.
                save.health = Math.Min(save.maxHealth, save.health + dt * 1.6f);
                save.energy = Math.Min(100, save.energy + dt * 6f);
            }
            if (RootHarmonyRoom == 0) return;
            if (save.room != RootHarmonyRoom || Dying || practicing)
            { ResetRootHarmonyTrial(); return; }
            RootHarmonyTime -= dt;
            if (enemies.Exists(IsRootHarmonyEnemy))
            {
                rootHarmonyPulseTime -= dt;
                if (rootHarmonyPulseTime <= 0)
                {
                    // Each listening stone has a different hostile pulse
                    // rhythm. It targets the player's actual current feet
                    // with an ordinary world-space delayed hazard, never a
                    // scripted instant hit or opaque screen overlay.
                    Point[] cardinal = { new Point(1, 0), new Point(-1, 0),
                        new Point(0, 1), new Point(0, -1) };
                    bool escapable = false;
                    foreach (Point direction in cardinal)
                    {
                        Point escape = Position + direction * 1.7f;
                        if (CanStand(escape) && ClearAttackPath(Position, escape))
                        { escapable = true; break; }
                    }
                    if (escapable)
                        hazards.Add(new Hazard {
                            position = Position,
                            radius = RootHarmonyRoom == 3 ? 1.45f : 1.15f,
                            damage = RootHarmonyRoom == 3 ? 16f : 10f + RootHarmonyRoom,
                            timer = RootHarmonyRoom == 1 ? 1.2f : 1.4f,
                            kind = RootHarmonyRoom == 2 ? "tide" : "rift",
                            rootHarmonyOwned = true
                        });
                    rootHarmonyPulseTime = RootHarmonyRoom == 1 ? 6.2f :
                        RootHarmonyRoom == 2 ? 5.7f : 5f;
                }
            }
            else
                hazards.RemoveAll(h => h.rootHarmonyOwned);
            if (RootHarmonyTime <= 0)
            {
                ResetRootHarmonyTrial(); revision++;
                Notify("失谐守卫夺回了共鸣石。线索仍在，可以重新调谐。",
                    "The discordant sentinels reclaimed the node. Try tuning again.");
            }
        }
        private int RootHarmonyNext()
        {
            for (int i = 1; i <= 3; i++)
                if (!Has("root_harmony_" + i)) return i;
            return 0;
        }
        private bool TalkRootHarmony(WorldObject o)
        {
            if (o.kind == "rootlistener")
            {
                if (save.room != 0) return true;
                if (!Has("scout_saved"))
                {
                    Dialog(o.name, o.en,
                        "密林仍有失踪的巡路员。先去把他救回来，我才敢让你靠近地下的失谐节点。",
                        "A roadwarden remains lost in the forest. Bring him home before approaching the unstable nodes.",
                        C("我先去密林", "I'll go to Thornwood", "close"));
                    return true;
                }
                if (!Has("root_harmony_started"))
                {
                    Dialog(o.name, o.en,
                        "我是芮，负责倾听星根而非抽走它。密林的根音、遗迹的潮音和矿场的铁音已经失去节拍。九年前的调谐命令把它们拆散，星陨只是最后一声巨响。\n\n请听三处共鸣石的证词。调对音节后，失谐守卫会醒来；必须真正击退它们才能安全封存读数。",
                        "I'm Rui. I listen to starroot instead of harvesting it. Root-song in Thornwood, tidal song in the ruins and iron-song in the mine have fallen out of time. A nine-year-old tuning order divided them. Starfall was only the final crack.\n\nListen to three sites. Choosing the right note awakens dissonant sentinels. Defeat them before sealing the reading.",
                        C("接取星根失谐调查", "Investigate the Starroot Counterpoint", "root_harmony_accept"),
                        C("稍后再来", "Later", "close"));
                    return true;
                }
                if (Has("root_harmony_complete"))
                {
                    Dialog(o.name, o.en,
                        "三道音纹合成了第三条路：不再要求一座城市替另一座城市耗尽河流。你把星根的节拍还给了能听见它的人。",
                        "Three readings reveal a third path: no city need drain another city's river. You returned the rhythm to those who can hear it.",
                        C("记住和声", "Remember the counterpoint", "close"));
                    return true;
                }
                if (RootHarmonyNext() == 0)
                {
                    Dialog(o.name, o.en,
                        "根、潮、铁终于能互相听见。它们不是堡垒的燃料，而是三地共用的脉搏。把最后的读数归档吧。",
                        "Root, tide and iron can finally hear one another. They were never fuel for a wall. Archive the complete reading.",
                        C("归档星根和声", "Archive the starroot harmony", "root_harmony_finish"),
                        C("稍后", "Later", "close"));
                    return true;
                }
                int next = RootHarmonyNext();
                Dialog(o.name, o.en,
                    "下一段失谐读数在" + new[] { "", "荆棘密林", "沉没遗迹", "赤铁矿场" }[next] + "。每段唱片都记着真正的地貌：根靠泥土，潮随水道，铁与矿脉共振。错误音节会耗损你的耐力。",
                    "The next reading is in " + new[] { "", "Thornwood", "the Drowned Ruins", "Ironreach" }[next] +
                        ". Root belongs to soil, tide to water and iron to ore. False notes sap energy.",
                    C("追踪失谐读数", "Track the dissonance", "root_harmony_track"),
                    C("继续探索", "Continue exploring", "close"));
                return true;
            }
            if (!o.kind.StartsWith("rootcounter")) return false;
            int region;
            if (!int.TryParse(o.kind.Substring("rootcounter".Length), out region) ||
                region < 1 || region > 3 || save.room != region) return true;
            if (!Has("root_harmony_started"))
            { Notify("星根石仍在低鸣。余烬镇的听纹师可能读得懂它。", "The stone hums. Rui in Emberfall may understand it."); return true; }
            if (Has("root_harmony_" + region))
            {
                Dialog(o.name, o.en, "音纹已经归档。周围安全时，这块石头会短暂恢复你失去的生命与耐力。敌人接近便会中断恢复。", "The reading is sealed. In safety this stone restores health and energy nearby; approaching enemies interrupt the respite.",
                    C("离开", "Leave", "close")); return true;
            }
            if (RootHarmonyNext() != region)
            {
                Dialog(o.name, o.en, "缺少前一段节拍，石面上的音纹无法稳定。", "The earlier reading is missing. This node cannot hold its beat.",
                    C("返回调查", "Return to the investigation", "close")); return true;
            }
            if (RootHarmonyRoom == region)
            {
                if (enemies.Exists(IsRootHarmonyEnemy))
                    Dialog(o.name, o.en, "失谐守卫正在攻击。先击败真正的守卫，再封存本地读数。余下 " +
                        (int)Math.Ceiling(RootHarmonyTime) + " 秒。",
                        "Dissonant sentinels are attacking. Defeat them before sealing the reading. " +
                        (int)Math.Ceiling(RootHarmonyTime) + " seconds left.",
                        C("投入战斗", "Defend the node", "close"),
                        C("安全中止调谐", "Withdraw and reset", "root_harmony_abandon_" + region));
                else Dialog(o.name, o.en,
                    "守卫已沉默。你听清了这段音纹，封存它，免得星根再次被迫共振。",
                    "The sentinels are silent. Seal this reading before dissonance returns.",
                    C("封存调谐读数", "Seal the tuned reading", "root_harmony_seal_" + region),
                    C("稍后", "Later", "close"));
                return true;
            }
            if (RootHarmonyRoom != 0)
            { Notify("另一处共鸣石正在调谐。", "Another harmonic trial is active."); return true; }
            string[] zh = {
                "泥下的星根不会说谎。它用湿土的呼吸而不是铜管的回音报出伤口。",
                "被改写的闸门仍记得涨潮的方向，水底的声音在潮汐停歇时最清晰。",
                "矿井深处，铁轨上的锤音与原始矿脉的搏动一致，机器却忘了停下。"
            };
            string[] en = {
                "Under soil, starroot speaks through earth rather than brass.",
                "The ruined sluice still remembers which way the tide once ran.",
                "Beneath the mine, the hammer on iron follows the ore's original pulse."
            };
            Dialog(o.name, o.en,
                zh[region - 1] + "\n\n哪一段音纹属于这里？选对后需守住40秒内的真实战斗。",
                en[region - 1] + "\n\nWhich note belongs here? Tune correctly, then defeat the sentinels before forty seconds elapse.",
                C("潮音：水道", "Tide: waterways", "root_harmony_tune_" + region + "_0"),
                C("铁音：矿脉", "Iron: ore veins", "root_harmony_tune_" + region + "_1"),
                C("根音：土壤", "Root: living soil", "root_harmony_tune_" + region + "_2"),
                C("暂不调谐", "Not yet", "close"));
            return true;
        }
        private bool ChooseRootHarmony(string action)
        {
            if (action == "root_harmony_track")
            { if (Has("root_harmony_started")) TrackQuest("root_harmony"); return true; }
            if (action == "root_harmony_accept")
            {
                if (save.room == 0 && Has("scout_saved") && !Has("root_harmony_started") &&
                    (Position - ObjectPoint(0, "rootlistener", new Point(-100, -100))).Length <= 2.2f)
                {
                    Flag("root_harmony_started"); TrackQuest("root_harmony");
                    Notify("新任务：星根失谐。去荆棘密林寻找第一处音纹。",
                        "New quest: Starroot Counterpoint. Find the first listening stone in Thornwood.");
                }
                return true;
            }
            if (action == "root_harmony_finish")
            {
                if (save.room == 0 && Has("root_harmony_started") &&
                    !Has("root_harmony_complete") && RootHarmonyNext() == 0 &&
                    (Position - ObjectPoint(0, "rootlistener", new Point(-100, -100))).Length <= 2.2f)
                {
                    Flag("root_harmony_complete"); CreditCoins(75); CreditSkillPoints(1);
                    AddXp(70); revision++; sound = "reward";
                    RecordMoment("星根失谐调查已归档：根、潮、铁不再被迫为堡垒独奏。",
                        "Starroot Counterpoint archived: root, tide and iron no longer play for the citadel alone.");
                    Notify("三段音纹已归档！获得75硬币、1技能点与70经验。",
                        "Three readings archived! +75 coins, +1 talent and +70 XP.");
                }
                return true;
            }
            if (action.StartsWith("root_harmony_abandon_"))
            {
                int room;
                if (!int.TryParse(action.Substring("root_harmony_abandon_".Length), out room) ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (RootHarmonyRoom == room && save.room == room && !Dying &&
                    (Position - site).Length <= 2.2f)
                {
                    ResetRootHarmonyTrial(); revision++; sound = "quest";
                    Notify("已安全终止调谐。守卫与残留攻击已清除，可重新尝试。",
                        "Tuning withdrawn. Sentinels and encounter attacks retired; retry when ready.");
                }
                return true;
            }
            if (action.StartsWith("root_harmony_tune_"))
            {
                string[] pieces = action.Substring("root_harmony_tune_".Length).Split('_');
                int room, note;
                if (pieces.Length != 2 || !int.TryParse(pieces[0], out room) ||
                    !int.TryParse(pieces[1], out note) || note < 0 || note > 2 ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (save.room != room || !Has("root_harmony_started") ||
                    RootHarmonyNext() != room || RootHarmonyRoom != 0 ||
                    (Position - site).Length > 2.2f || Dying || practicing || racing ||
                    reliefMissionRoom != 0 || roadTrialRoom != 0 ||
                    wayfarerEncounterRoom != 0 || atlasEchoRoom != 0) return true;
                int answer = room == 1 ? 2 : room == 2 ? 0 : 1;
                if (note != answer)
                {
                    save.energy = Math.Max(0, save.energy - 8);
                    effects.Add(new Effect("danger", site, .6f) { radius = .65f });
                    sound = "deny"; revision++;
                    Notify("失谐反馈让耐力减少8点。再听听石面与当地地貌的联系。",
                        "Dissonance costs 8 energy. Listen to the landscape, then try again.");
                    return true;
                }
                RootHarmonyRoom = room; RootHarmonyTime = 40;
                rootHarmonyPulseTime = 2.5f;
                for (int i = 0; i < 3; i++)
                {
                    Point spawn = site + new Point(i == 0 ? -3 : i == 1 ? 3 : 0,
                        i == 2 ? 3 : -1);
                    enemies.Add(CreatePlacedEnemy(88000 + room * 10 + i,
                        i == 2 ? 2 : i, spawn, 35 + room * 13));
                }
                effects.Add(new Effect("discover", site, .8f) { rootHarmonyOwned = true });
                sound = "windup"; revision++;
                Notify("音纹调谐成功！真正击退三名失谐守卫，再回来封存读数。40秒倒计时开始。",
                    "Correct resonance! Defeat three real sentinels and seal the reading within 40 seconds.");
                return true;
            }
            if (action.StartsWith("root_harmony_seal_"))
            {
                int room;
                if (!int.TryParse(action.Substring("root_harmony_seal_".Length), out room) ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (save.room != room || RootHarmonyRoom != room || RootHarmonyTime <= 0 ||
                    (Position - site).Length > 2.2f || enemies.Exists(IsRootHarmonyEnemy) ||
                    Has("root_harmony_" + room)) return true;
                ResetRootHarmonyTrial(); Flag("root_harmony_" + room);
                CreditCoins(25); CreditShards(2); AddXp(35);
                sound = "reward"; revision++;
                effects.Add(new Effect("discover", site, .8f));
                RecordMoment("已封存第" + room + "段失谐音纹，失控的星根正在恢复节拍。",
                    "Sealed counterpoint reading " + room + "; starroot is recovering its rhythm.");
                Notify("读数已封存！硬币25、碎片2、经验35。共鸣石附近已可安全休整。",
                    "Reading sealed! +25 coins, +2 shards, +35 XP. The stone now offers a safe respite.");
                return true;
            }
            return false;
        }
    }
}
