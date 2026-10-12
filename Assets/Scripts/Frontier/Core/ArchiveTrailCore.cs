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
                Flag("archive_complete"); save.coins += 60; save.skillPoints++;
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
            Flag("archive_fragment_" + region); save.coins += 20; save.shards += 2;
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
    }
}
