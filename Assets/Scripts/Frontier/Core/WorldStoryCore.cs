using System;
using System.Collections.Generic;

namespace Starfall
{
    // Read-only, progression-aware story companion. No serialized field, flag,
    // reward, chapter gate, or combat rule is introduced by reading the archive.
    public sealed partial class GameCore
    {
        private Choice StoryChoice(string zh, string en, string action) { return C(zh, en, action); }
        private bool TalkWorldChronicle(WorldObject o)
        {
            if (o.kind != "worldchronicle") return false;
            WorldChronicleMenu();
            return true;
        }
        private void WorldChronicleMenu()
        {
            Dialog("边境编年册", "Frontier Chronicle",
                "封面是一条横跨星根与河流的细线。卷首写道：\n\n“壁垒留住的是昨夜，路能带来明天。任何被牺牲的人，都应该先有说话的机会。”\n\n册中只有你亲眼见证、或已取得记录的篇章会逐渐展开。",
                "An ink line runs from roots to rivers across the cover. Its first words read:\n\n“A wall can keep last night. A road can bring tomorrow. Those asked to pay deserve to speak first.”\n\nOnly accounts you have witnessed or recovered are available.",
                StoryChoice("世界与年代", "Origins and timeline", "story_section_origins"),
                StoryChoice("星核四章", "Four starcore chapters", "story_section_campaign"),
                StoryChoice("远岸与证词", "Farshore and testimony", "story_section_farshore"),
                StoryChoice("归乡支线与尾声", "Homeward stories and aftermath", "story_section_after"),
                StoryChoice("合上编年册", "Close the chronicle", "close"));
        }
        private bool StoryUnlocked(string page)
        {
            if (page == "origin" || page == "roots" || page == "orders") return true;
            if (page == "wardens") return Has("scout_saved");
            if (page == "forest") return Has("core_1") || Has("boss_1");
            if (page == "ruins") return Has("core_2") || Has("boss_2");
            if (page == "mine") return Has("core_3") || Has("boss_3");
            if (page == "citadel") return Has("boss_4");
            if (page == "harbor") return Has("harbor_report");
            if (page == "desert") return Has("desert_report");
            if (page == "mountain") return Has("mountain_report");
            if (page == "covenant") return save.frontierEnding > 0;
            if (page == "signal") return Has("survey_complete");
            if (page == "atlas") return Has("archive_complete");
            if (page == "abyss") return save.frontierEnding > 0;
            if (page == "ending") return save.ending > 0;
            return false;
        }
        private void WorldChronicleSection(string section)
        {
            var choices = new List<Choice>();
            string titleZh = "", titleEn = "", descriptionZh = "", descriptionEn = "";
            if (section == "origins")
            {
                titleZh = "旧日编年"; titleEn = "Before the Fall";
                descriptionZh = "星陨不是第一次错误，只是多年未曾纠正的调谐，终于在七日前一起断裂。";
                descriptionEn = "The Starfall was not the first mistake. Seven days ago, years of neglected imbalances finally fractured.";
                choices.Add(StoryChoice("星陨后的第七天", "Seven days after the fall", "story_read_origin"));
                choices.Add(StoryChoice("星根与七锚", "Starroot and seven anchors", "story_read_roots"));
                choices.Add(StoryChoice("九年前的旧指令", "The nine-year-old order", "story_read_orders"));
                if (StoryUnlocked("wardens")) choices.Add(StoryChoice("巡路队的未寄信", "Unsent roadwarden letters", "story_read_wardens"));
            }
            else if (section == "campaign")
            {
                titleZh = "星核四章"; titleEn = "The Four Starcores";
                descriptionZh = "从一位失踪侦察员开始，追查森林、河水和矿机被抽走的供给。只能阅读已经走到的真相。";
                descriptionEn = "The investigation begins with a missing scout. Accounts appear as their truths are discovered.";
                if (StoryUnlocked("forest")) choices.Add(StoryChoice("荆棘密林 · 被割断的根", "Thornwood · severed roots", "story_read_forest"));
                if (StoryUnlocked("ruins")) choices.Add(StoryChoice("沉没遗迹 · 被改写的闸", "Ruins · rewritten sluice", "story_read_ruins"));
                if (StoryUnlocked("mine")) choices.Add(StoryChoice("赤铁矿场 · 不停的机器", "Ironreach · the tireless machine", "story_read_mine"));
                if (StoryUnlocked("citadel")) choices.Add(StoryChoice("星陨堡垒 · 厄洛的屏障", "Citadel · Orrin's shield", "story_read_citadel"));
            }
            else if (section == "farshore")
            {
                titleZh = "远岸证词"; titleEn = "Letters from Afar";
                descriptionZh = "灯照向哪里？谁喝得到水？被封在钟声下的人叫什么名字？三份记录必须放在同一张桌前。";
                descriptionEn = "Who sees the light, receives the water, and gets named beneath the bells? Three records belong at one table.";
                if (StoryUnlocked("harbor")) choices.Add(StoryChoice("雾港 · 灯与救援", "Mistport · light and rescue", "story_read_harbor"));
                if (StoryUnlocked("desert")) choices.Add(StoryChoice("玻璃沙海 · 水归谁", "Glass Desert · who owns water", "story_read_desert"));
                if (StoryUnlocked("mountain")) choices.Add(StoryChoice("霜钟高原 · 名字", "Frostchime · names", "story_read_mountain"));
                if (StoryUnlocked("covenant")) choices.Add(StoryChoice("三方议事厅 · 协定", "The council covenant", "story_read_covenant"));
            }
            else if (section == "after")
            {
                titleZh = "归乡与余波"; titleEn = "Homeward and After";
                descriptionZh = "并不是只有星核能改变世界。一张地图、一座信标、一条未封锁的道路，都在说明另一种防线。";
                descriptionEn = "A map, a beacon and an open road can also change the frontier.";
                if (StoryUnlocked("signal")) choices.Add(StoryChoice("归乡星图与巡路信标", "Homeward chart and beacons", "story_read_signal"));
                if (StoryUnlocked("atlas")) choices.Add(StoryChoice("失落测绘师的记录", "The lost surveyor's atlas", "story_read_atlas"));
                if (StoryUnlocked("abyss")) choices.Add(StoryChoice("地脉深渊", "The Root Abyss", "story_read_abyss"));
                if (StoryUnlocked("ending")) choices.Add(StoryChoice("你决定的边境", "The frontier you chose", "story_read_ending"));
            }
            else { WorldChronicleMenu(); return; }
            string lockedZh = "下一页尚未写成。继续推进调查后回来。";
            string lockedEn = "These pages are not yet written. Return after your investigations.";
            if (choices.Count == 0) { descriptionZh += "\n\n" + lockedZh; descriptionEn += "\n\n" + lockedEn; }
            choices.Add(StoryChoice("返回编年册目录", "Back to chronicle", "story_menu"));
            Dialog(titleZh, titleEn, descriptionZh, descriptionEn, choices.ToArray());
        }
        // Chapter navigation is derived entirely from existing milestone flags.
        // No new save field or reward can be introduced by browsing the book.
        private static string StorySectionForPage(string page)
        {
            if (page == "origin" || page == "roots" || page == "orders" || page == "wardens") return "origins";
            if (page == "forest" || page == "ruins" || page == "mine" || page == "citadel") return "campaign";
            if (page == "harbor" || page == "desert" || page == "mountain" || page == "covenant") return "farshore";
            return "after";
        }
        private static string[] StoryPagesForSection(string section)
        {
            if (section == "origins") return new[] { "origin", "roots", "orders", "wardens" };
            if (section == "campaign") return new[] { "forest", "ruins", "mine", "citadel" };
            if (section == "farshore") return new[] { "harbor", "desert", "mountain", "covenant" };
            return new[] { "signal", "atlas", "abyss", "ending" };
        }
        private string AdjacentStoryPage(string page, int direction)
        {
            string[] pages = StoryPagesForSection(StorySectionForPage(page));
            int index = Array.IndexOf(pages, page);
            if (index < 0) return null;
            for (int i = index + direction; i >= 0 && i < pages.Length; i += direction)
                if (StoryUnlocked(pages[i])) return pages[i];
            return null;
        }
        // The chronicle reflects choices already recorded in the original save
        // fields. It never commits a new decision, quest flag or reward.
        private void AppendChronicleConsequences(string page, ref string zh, ref string en)
        {
            if (page == "harbor")
            {
                if (save.harborPledge == 1)
                {
                    zh += "\n\n【守灯优先】你将有限的零件先交给守灯会，重新点亮外海航道。摆渡盟仍要逐个寻找失联的人；船册不会把那些等待抹掉。";
                    en += "\n\n[Lights first] You gave the scarce parts to the Lamplighters to reopen the sea lanes. The Ferry Union still has people to find, and the log cannot erase those left waiting.";
                }
                else if (save.harborPledge == 2)
                {
                    zh += "\n\n【救援优先】你先让摆渡盟接回被困旅客。海上的灯列还需要修复，而获救者也终于能讲述潮水改变的那一夜。";
                    en += "\n\n[Rescue first] You helped the Ferry Union bring stranded passengers home before restoring the lights. The sea lanes still need repair, but survivors can now testify about the changing tide.";
                }
                else if (save.harborPledge == 3)
                {
                    zh += "\n\n【协同方案】你让灯塔维修和渡船救援共用一份紧张的物资表。两边都不能假装自己的安全是唯一需要守护的。";
                    en += "\n\n[Shared priority] You split scarce supplies between lighthouse repairs and ferry rescues. Neither side could call its own safety the only kind worth protecting.";
                }
            }
            else if (page == "desert")
            {
                if (save.desertChoice == 1)
                {
                    zh += "\n\n【绿洲优先】你把蓄光池的补给转向活人居住的绿洲。山野议会得到一份公开的调度账，也必须负责让供水持续运转。";
                    en += "\n\n[Oasis first] You redirected the reservoir toward living oasis communities. The Assembly gained a public allocation ledger and the responsibility to keep the water flowing.";
                }
                else if (save.desertChoice == 2)
                {
                    zh += "\n\n【航路优先】你保留机器对远岸灯网的供能，帮助船只辨认归途；但旧城主的指令不再能代替活人的监督。";
                    en += "\n\n[Routes first] You retained power for the navigation lights so ships could find home. The dead ruler's old instruction no longer substitutes for oversight by the living.";
                }
            }
            else if (page == "mountain")
            {
                if (save.mountainChoice == 1)
                {
                    zh += "\n\n【证据封存】你将原始名单交给议会保存，作为推动修正撤离制度的证据。名字没有公开，保管的人因此更有责任作出改变。";
                    en += "\n\n[Evidence preserved] You entrusted the original names to the council as evidence for evacuation reform. The names remain protected, and their keepers are responsible for acting on them.";
                }
                else if (save.mountainChoice == 2)
                {
                    zh += "\n\n【公开名单】你让失踪者的名字刻在所有人都能读到的路碑上。钟声不再只掩盖事故，也提醒后人记住每个人。";
                    en += "\n\n[Names made public] You had the missing names set where every traveler can read them. The bells no longer only conceal a disaster; they ask people to remember each life.";
                }
            }
            else if (page == "covenant")
            {
                if (save.frontierEnding == 1)
                {
                    zh += "\n\n【共同水渠】摆渡盟负责管道，旧灯缆成为输水材料，沙海得到新的种子。农田会多收获药草，但远海的灯仍需要节制供能。";
                    en += "\n\n[Shared canals] The Ferry Union manages pipes made partly from old lamp cables, while new seeds reach the desert. Farms gain extra herbs; distant lights still need careful power use.";
                }
                else if (save.frontierEnding == 2)
                {
                    zh += "\n\n【轮值灯网】三地轮流负责航路灯列，并公开每次交班的耗材与风险。灯没有永远不灭的主人，只有必须有人接起的下一班。";
                    en += "\n\n[Rotating lightwatch] Three regions share navigation shifts and publish their supplies and risks. No keeper owns the lights forever; each has to hand them on.";
                }
                else if (save.frontierEnding == 3)
                {
                    zh += "\n\n【开放星路】米拉、伊沃与森共同管理渡船、水渠和路碑。能源账向所有人开放，同伴配合与公共委托也因此得到实际改善。";
                    en += "\n\n[Open starroads] Mira, Ivo and Sen jointly care for ferries, canals and the memorial road. Energy accounts are public, and companions and community contracts benefit from their cooperation.";
                }
            }
        }
        private void WorldChroniclePage(string page)
        {
            string zh = "", en = "", headingZh = "", headingEn = "";
            switch (page)
            {
                case "origin":
                    headingZh = "星陨后的第七天"; headingEn = "Day Seven after Starfall";
                    zh = "七日前，夜空和大地同时迸出星色裂光。森林生出荆棘，河道淹没石桥，矿机忘记停工。堡垒称此事为星陨，要求继续回收星核，为护城屏障供能。\n\n你带着失踪巡路员的旧信来到余烬镇。莉雅先请你救出密林侦察员。故事始于有人等不到回家的人，不是预言。";
                    en = "Seven days ago light fractured across sky and soil. Thorns blocked forests, rivers crossed ruined bridges, and mining machines forgot to stop. The citadel named the disaster Starfall and ordered more cores for its shield.\n\nYou carried an unsent roadwarden letter into Emberfall. Lyra asked you to bring the missing scout home. This story begins with people waiting, not a prophecy.";
                    break;
                case "roots":
                    headingZh = "星根与七锚"; headingEn = "Starroot and the Seven Anchors";
                    zh = "边境地下的星根是一张分配潮水、热量和生机的活脉络。先民为它建立七处调谐锚点，凝结的核心叫星核。星晶只是能量溢出形成的矿物，不是可以无限采集的恩赐。\n\n把一处锚点的能源送往堡垒，就会改变另一处水与土地的供给。所谓更强大的屏障，可能意味着更贫瘠的道路。";
                    en = "Starroot is a living network moving tides, heat and life beneath the frontier. Ancient engineers built seven balancing anchors; their condensed regulators are the starcores. Starcrystals are surplus deposits, not an unlimited blessing.\n\nFeeding one region's anchor to the citadel changes the water and soil of another. A brighter shield may mean a poorer road outside.";
                    break;
                case "orders":
                    headingZh = "九年前的命令"; headingEn = "An Order Nine Years Old";
                    zh = "玻璃沙海的旧城主死去已经九年。蓄光机仍遵守最后一条命令：每天替主人留下水与光。没有人改变命令，空城便继续得到补给，活人却走在无水的沙海。\n\n七日前的星陨使这些旧系统更加不稳。它们不是突然变坏，而是太久没有人问过：这台机器现在替谁工作？";
                    en = "The ruler of the Glass Desert's old city died nine years ago. Its light reservoir still follows one instruction: keep water and light for its master. The empty city remains supplied while living travelers cross the thirsting dunes.\n\nThe Starfall seven days ago destabilized such systems. They did not suddenly turn evil. No one had asked whom they were serving now.";
                    break;
                case "wardens":
                    headingZh = "巡路队最后的信"; headingEn = "The Wardens' Last Letters";
                    zh = "巡路员曾替公会测绘矿脉，后来发现地图上只许画堡垒的货路，不许画村庄的逃生路。有人在旧信、信标和地图里藏下另一套路线：药送给需要的人，船留给无钱渡河的人，机器应该由活着的人决定用途。\n\n他们失踪了，路线却留着。救回侦察员后，你第一次知道这封信并非只为莉雅而写。";
                    en = "The wardens mapped ore for the guild until they found that escape routes to villages had been left out. They hid another network in letters, beacons and charts: medicine for those in need, passage for those without coins, and machines answerable to living people.\n\nThe patrol disappeared, but the routes survived. The rescued scout reveals that the letter was meant for more than Lyra.";
                    break;
                case "forest":
                    headingZh = "荆棘密林：森林在守护什么"; headingEn = "Thornwood: What the Forest Defends";
                    zh = "矿工按命令切开两棵老树，根脉中的星晶让兽群陷入狂暴。黑荆巨兽守在星核旁，挡住的不是人类本身，而是继续伤害根系的采掘。\n\n救侦察员、击退巨兽并调谐祭坛后，你获得第一枚星核。它被送去公会暂管，但是否继续为堡垒供能，仍是一个未回答的问题。";
                    en = "Mining cuts severed ancient roots and starcrystal surges maddened the beasts. The Briar Beast guarded its core against further extraction, not humanity itself.\n\nYou rescued the scout, quieted the beast and took the first starcore into temporary guild custody. Whether it would keep feeding the shield was still unresolved.";
                    break;
                case "ruins":
                    headingZh = "沉没遗迹：谁有钥匙"; headingEn = "Drowned Ruins: Who Holds the Key";
                    zh = "先民用日、月、潮三个记号按次序打开水闸。后来守卫只承认公会印章；印章消失的一夜，无数人被关在涨潮的桥外。\n\n你恢复碑序，击退依旧守护旧命令的溺水守卫。第二枚星核和浅水接通能力揭示了同一个道理：机关可以记住次序，但人必须记得次序为何存在。";
                    en = "Ancient sluices opened to a Sun, Moon and Tide sequence. Later, officials replaced consent with a guild seal. When the seal vanished, people waited outside the floodgates.\n\nYou restored the tablets and defeated a sentinel still defending an obsolete order. The second core teaches that mechanisms remember sequences; people must remember their purpose.";
                    break;
                case "mine":
                    headingZh = "赤铁矿场：不许停机"; headingEn = "Ironreach: Never Stop Working";
                    zh = "自动监工仍把星晶送往堡垒。矿工缺水、断桥缺零件，机器却从未被允许停下来。矿场的一张工时表还擦掉了为妹妹点灯的工人名字。\n\n你修好升降机，击败铸铁监工，取回第三枚星核。诺尔说得对：技术不会自己选择善恶，但握着图纸的人必须作出选择。";
                    en = "The Iron Overseer still shipped crystals to the shield. Workers lacked water, broken bridges lacked parts, but no one had authorized the machines to stop. A miner's timesheet even erased the man who lent light to his sister.\n\nYou repaired the lift, defeated the overseer and recovered the third core. Technology does not choose sides by itself; the person holding the plan must.";
                    break;
                case "citadel":
                    headingZh = "厄洛与最后的城墙"; headingEn = "Orrin and the Last Wall";
                    zh = "厄洛不是谎称自己救过人。他亲眼见过城墙之外的死亡，才决定绝不能让屏障熄灭。现在他知道屏障正在从森林、河流与村庄抽走生机，却害怕停机的代价更加惨烈。\n\n你可以用战斗迫使他面对后果；如果带来了三地证词和已经运转的共同协定，也能证明“没有别的选择”并非事实。星核最终的去向仍由你决定。";
                    en = "Orrin really did save lives behind his wall. Having seen what happened beyond it, he swore the shield would never fail again. He now knows it drains forests, rivers and villages, but fears stopping it even more.\n\nYou may defeat him in battle. With the original regional evidence and a working covenant, you can also prove that another path exists. You still decide where the cores belong.";
                    break;
                case "harbor":
                    headingZh = "雾港：两种颜色的灯"; headingEn = "Mistport: Two Colors of Light";
                    zh = "白灯让远海船只找到航路，青灯告诉落水者岸上仍有人等待。堡垒每次加压，水位账目就会少一行。米拉的弟弟曾要求修灯，后来带孩子们去了北岸，没有再回来。\n\n守灯和救援可能争同一份零件。你必须在灯塔、避难乘客和潮壳海兽之间作出实际取舍，取得带着名字的水位记录。";
                    en = "White lamps guide ships offshore. Teal lamps tell people in the water that someone is waiting. Each shield surge cost the tide ledger a line. Mira's brother asked for repairs, then took children north and never returned.\n\nLighthouses and rescues compete for the same parts. Your choices preserve a log of both the living and the missing.";
                    break;
                case "desert":
                    headingZh = "玻璃沙海：为谁留水"; headingEn = "Glass Desert: Who Receives Water";
                    zh = "伊沃让三面镜子的光重新进入蓄光池，唤醒仍奉行九年前命令的玻璃守卫。旧城主已经死去，却占着活人急需的水。\n\n你让机器接受新的指令，可以优先供给绿洲，也可以维持远岸灯网。修好的降温站说明技术不仅能储存能量，也能让路上的人活下来。";
                    en = "Ivo restores the mirror channel and confronts a Glass Custodian obeying a nine-year-old instruction. Its master is gone, yet the living still go thirsty.\n\nYou redirect the reservoir toward oasis water or navigation lights. The repaired cooling station proves that machinery can shelter travelers instead of hoarding energy.";
                    break;
                case "mountain":
                    headingZh = "霜钟高原：念出每个名字"; headingEn = "Frostchime: Speak Every Name";
                    zh = "森发现钟阵的低音能止住冰裂，也能掩盖冰下的呼救。霜钟执事认为公开撤离事故会让高原崩溃；名单上的家人却有权知道真相。\n\n你解开钟阵、修复火盆并取得原始记录。可以先保存证据推动改正，也可以把名字刻进山路，让每个人看见。";
                    en = "Sen discovers the low chimes can steady cracking ice and drown out voices beneath it. The Frost Prelate fears that exposing the evacuation failure will break the community; the missing have families who deserve the truth.\n\nYou restore the bells and recover the names. You may preserve the evidence privately for reform or carve it in public.";
                    break;
                case "covenant":
                    headingZh = "三方协定：让人拥有决定权"; headingEn = "The Council: A Share in Decisions";
                    zh = "雾港的水位、沙海的调度、高原的名单必须同时上桌。守灯会承认航路也有代价，摆渡盟承认停灯会伤及远海的人，山野议会承认复苏土地仍需要技术。\n\n共同水渠、轮值灯网或开放星路是三种不同的治理协议。这份协定解决“谁来管理”，而堡垒的星核结局解决“力量往哪里去”。";
                    en = "The harbor tide ledger, desert allocation log and mountain names share one table. Lamplighters, ferrymen and villagers acknowledge what each has neglected.\n\nShared canals, rotating lightwatch and open starroads are different governance contracts. The council answers who decides. The starcore ending answers where the power goes.";
                    break;
                case "signal":
                    headingZh = "归乡星图：第二套道路"; headingEn = "Homeward Chart: Another Network";
                    zh = "巡路队留下三枚信标、三封旧信和能避开官方关卡的归乡星图。它们不是藏宝地点，而是洪水、塌桥和封锁之后仍可以使用的疏散路线。\n\n守住信标的人不仅在打败怪物，也在证明城墙之外需要真正维护、补给和看守的公共道路。";
                    en = "Three beacons, unsent ciphers and a homeward chart preserve a route beyond official gates. They do not point to treasure; they mark paths after flood, collapse and evacuation.\n\nDefending a beacon means defending the public road outside the walls.";
                    break;
                case "atlas":
                    headingZh = "失落测绘师：校准的是归途"; headingEn = "Lost Surveyor: Calibrating the Way Home";
                    zh = "琳拼好林地残图、遗迹刻石与矿场测距仪，发现旧测绘师记录的是撤离坡道而非黄金矿脉。矿场校准台仍保存一具重甲方位回声，按旧守卫协议攻击来者。\n\n战胜回声不是杀死测绘师的幽灵，而是结束一套已经失去使用目的的自动防御，让这些路线重新对活人开放。";
                    en = "Lin joins the woodland chart, tide inscription and mine rangefinder into an atlas of escape routes rather than treasure seams. An armored compass echo remains in a Rediron calibrator.\n\nSilencing that defense is not killing a surveyor's ghost. It is retiring a machine that has forgotten why the route existed.";
                    break;
                case "abyss":
                    headingZh = "地脉深渊：星根的伤口"; headingEn = "Root Abyss: A Wound in the Network";
                    zh = "远岸达成治理协定后，居民才允许打开深入星根的旧备用管道。六层遗迹存着祝福、危险和未结算物资。\n\n深入不是为了占有更多力量，而是检验玩家在面对高风险回报时，能否守住已有的互助约定。未能安全带回的东西，不应被记作属于自己的资源。";
                    en = "Only after a farshore covenant can travelers enter the six old reserve conduits beneath the starroot lattice. Boons, danger and unbanked supplies coexist there.\n\nThe descent tests whether the promise of shared care survives the temptation of greater extraction. Nothing is yours until it is brought back safely.";
                    break;
                case "ending":
                    headingZh = "你决定的边境"; headingEn = "The Frontier You Chose";
                    if (save.ending == 1)
                    { zh = "你将星核重新接回地方锚点，城墙屏障不再替所有人决定归途。土地开始复苏，风险仍在；厄洛第一次离开城墙，学着同邻居种树。"; en = "You returned the cores to their local anchors. The shield no longer decides everyone's future. The land begins to heal, though danger remains. Orrin learns to plant a tree beyond his wall."; }
                    else if (save.ending == 2)
                    { zh = "你保留屏障，也逼迫堡垒打开大门容纳更多人。短期的安全真实存在，但被抽取的土地还在承担代价。这个选择不是幸福的终点，而是一份尚未还清的账。"; en = "You kept the shield and opened the citadel to more people. Its safety is real, and so is the debt still paid by the outer land. The argument has not been settled."; }
                    else
                    { zh = "杏让药物走向村庄，老潮把食物送过河，诺尔将耗能机器改造成共用水泵。星核慢慢回到居民之间，厄洛拆下第一段城墙。没有一夜解决全部问题，终于有人愿意一起承担。"; en = "An sends medicine beyond authorized roads. Old Tide ferries food. Noll rebuilds generators as public pumps. The cores return to communities and Orrin removes the first wall. Shared work, not one victory, begins the change."; }
                    break;
                default: WorldChronicleMenu(); return;
            }
            AppendChronicleConsequences(page, ref zh, ref en);
            // Offer chapter-local navigation without jumping across locked
            // milestones. The current page is always one the player earned.
            var choices = new List<Choice>();
            string previous = AdjacentStoryPage(page, -1);
            string next = AdjacentStoryPage(page, 1);
            if (previous != null)
                choices.Add(StoryChoice("上一页", "Previous page", "story_read_" + previous));
            if (next != null)
                choices.Add(StoryChoice("下一页", "Next page", "story_read_" + next));
            choices.Add(StoryChoice("返回本章", "Back to this chapter",
                "story_section_" + StorySectionForPage(page)));
            choices.Add(StoryChoice("返回编年册目录", "Back to chronicle", "story_menu"));
            choices.Add(StoryChoice("收起记录", "Close record", "close"));
            Dialog(headingZh, headingEn, zh, en, choices.ToArray());
        }
        private bool ChooseWorldChronicle(string action)
        {
            if (action == "story_menu") { WorldChronicleMenu(); return true; }
            if (action.StartsWith("story_section_"))
            { WorldChronicleSection(action.Substring("story_section_".Length)); return true; }
            if (!action.StartsWith("story_read_")) return false;
            string page = action.Substring("story_read_".Length);
            // Re-check the gate at the moment the action is chosen. Returning
            // to a remembered dialogue choice cannot reveal future spoilers.
            if (!StoryUnlocked(page)) { WorldChronicleMenu(); return true; }
            WorldChroniclePage(page);
            return true;
        }
    }
}
