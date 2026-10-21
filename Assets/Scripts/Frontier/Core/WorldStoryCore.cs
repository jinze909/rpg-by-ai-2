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
            if (page == "testimonies") return Has("witness_started_1") || Has("witness_started_2") || Has("witness_started_3");
            if (page == "forest") return Has("core_1") || Has("boss_1");
            if (page == "ruins") return Has("core_2") || Has("boss_2");
            if (page == "mine") return Has("core_3") || Has("boss_3");
            if (page == "citadel") return Has("boss_4");
            if (page == "triad") return Has("triad_started") || Has("triad_complete");
            if (page == "blackout") return Has("blackout_started") || Has("blackout_complete");
            if (page == "harbor") return Has("harbor_report");
            if (page == "temper") return Has("iron_temper_started") || Has("iron_temper_complete");
            if (page == "tidebound") return Has("tide_salvage_started") || Has("tide_salvage_complete");
            if (page == "desert") return Has("desert_report");
            if (page == "mountain") return Has("mountain_report");
            if (page == "covenant") return save.frontierEnding > 0;
            if (page == "signal") return Has("survey_complete");
            if (page == "atlas") return Has("archive_complete");
            if (page == "counterpoint") return Has("root_harmony_started");
            if (page == "commons") return Has("commons_relay_started");
            if (page == "skywatch") return Has("skywatch_started");
            if (page == "glasswind") return Has("glasswind_started");
            if (page == "abyss") return save.frontierEnding > 0;
            if (page == "ending") return save.ending > 0;
            if (page == "afterlight") return Has("afterlight_started") || Has("afterlight_complete");
            if (page == "storm") return Has("storm_started") || Has("storm_complete");
            if (page == "hearing") return Has("hearing_started") || Has("hearing_complete");
            if (page == "roadcharter") return RoadCharterStarted || RoadCharterComplete;
            if (page == "supply_inquest") return CivicSupplyStarted;
            if (page == "lastdirective") return LastDirectiveStarted;
            if (page == "civicconduit") return CivicConduitStarted;
            if (page == "civic_aftermath") return CivicConduitComplete;
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
            }
            else if (section == "campaign")
            {
                titleZh = "星核四章"; titleEn = "The Four Starcores";
                descriptionZh = "从一位失踪侦察员开始，追查森林、河水和矿机被抽走的供给。只能阅读已经走到的真相。";
                descriptionEn = "The investigation begins with a missing scout. Accounts appear as their truths are discovered.";
            }
            else if (section == "farshore")
            {
                titleZh = "远岸证词"; titleEn = "Letters from Afar";
                descriptionZh = "灯照向哪里？谁喝得到水？被封在钟声下的人叫什么名字？三份记录必须放在同一张桌前。";
                descriptionEn = "Who sees the light, receives the water, and gets named beneath the bells? Three records belong at one table.";
            }
            else if (section == "after")
            {
                titleZh = "归乡与余波"; titleEn = "Homeward and After";
                descriptionZh = "并不是只有星核能改变世界。一张地图、一座信标、一条未封锁的道路，都在说明另一种防线。";
                descriptionEn = "A map, a beacon and an open road can also change the frontier.";
            }
            else { WorldChronicleMenu(); return; }

            // One canonical ordered list drives both the directory and page
            // navigation. Adding a chapter here can no longer silently omit
            // its menu entry or place its previous/next links out of order.
            foreach (string page in StoryPagesForSection(section))
            {
                if (!StoryUnlocked(page)) continue;
                Choice choice = ChronicleChoiceForPage(page);
                if (choice != null) choices.Add(choice);
            }
            string lockedZh = "下一页尚未写成。继续推进调查后回来。";
            string lockedEn = "These pages are not yet written. Return after your investigations.";
            if (choices.Count == 0) { descriptionZh += "\n\n" + lockedZh; descriptionEn += "\n\n" + lockedEn; }
            choices.Add(StoryChoice("返回编年册目录", "Back to chronicle", "story_menu"));
            Dialog(titleZh, titleEn, descriptionZh, descriptionEn, choices.ToArray());
        }
        private Choice ChronicleChoiceForPage(string page)
        {
            switch (page)
            {
                case "origin": return StoryChoice("星陨后的第七天", "Seven days after the fall", "story_read_origin");
                case "roots": return StoryChoice("星根与七锚", "Starroot and seven anchors", "story_read_roots");
                case "orders": return StoryChoice("九年前的旧指令", "The nine-year-old order", "story_read_orders");
                case "wardens": return StoryChoice("巡路队的未寄信", "Unsent roadwarden letters", "story_read_wardens");
                case "testimonies": return StoryChoice("前三章 · 未被计入的证人", "Three Chapters · The Uncounted Witnesses", "story_read_testimonies");
                case "forest": return StoryChoice("荆棘密林 · 被割断的根", "Thornwood · severed roots", "story_read_forest");
                case "ruins": return StoryChoice("沉没遗迹 · 被改写的闸", "Ruins · rewritten sluice", "story_read_ruins");
                case "mine": return StoryChoice("赤铁矿场 · 不停的机器", "Ironreach · the tireless machine", "story_read_mine");
                case "temper": return StoryChoice("余烬回火 · 能停下的炉", "Ember Temper · A Furnace That Can Rest", "story_read_temper");
                case "triad": return StoryChoice("第四章 · 三核审计", "Chapter Four · Three-Core Reckoning", "story_read_triad");
                case "blackout": return StoryChoice("第四章 · 长夜撤离", "Chapter Four · The Night the Shield Failed", "story_read_blackout");
                case "citadel": return StoryChoice("星陨堡垒 · 厄洛的屏障", "Citadel · Orrin's shield", "story_read_citadel");
                case "harbor": return StoryChoice("雾港 · 灯与救援", "Mistport · light and rescue", "story_read_harbor");
                case "tidebound": return StoryChoice("潮缚货单 · 谁被记作货损", "Tidebound Manifest · Written Off", "story_read_tidebound");
                case "desert": return StoryChoice("玻璃沙海 · 水归谁", "Glass Desert · who owns water", "story_read_desert");
                case "glasswind": return StoryChoice("玻璃风脉 · 为谁吹来的风", "Glasswind Circuit · Who Owns the Wind", "story_read_glasswind");
                case "mountain": return StoryChoice("霜钟高原 · 名字", "Frostchime · names", "story_read_mountain");
                case "covenant": return StoryChoice("三方议事厅 · 协定", "The council covenant", "story_read_covenant");
                case "signal": return StoryChoice("归乡星图与巡路信标", "Homeward chart and beacons", "story_read_signal");
                case "atlas": return StoryChoice("失落测绘师的记录", "The lost surveyor's atlas", "story_read_atlas");
                case "counterpoint": return StoryChoice("星根失谐与三段音纹", "Starroot Counterpoint · three readings", "story_read_counterpoint");
                case "commons": return StoryChoice("公共信标 · 艾拉的证词", "Citadel Commons Relay · Ayla's testimony", "story_read_commons");
                case "skywatch": return StoryChoice("霜钟夜空 · 最后一夜的光谱", "Frostchime Skywatch · last night's spectrum", "story_read_skywatch");
                case "abyss": return StoryChoice("地脉深渊", "The Root Abyss", "story_read_abyss");
                case "afterlight": return StoryChoice("第五章 · 余光重建", "Chapter Five · Afterlight Reconstruction", "story_read_afterlight");
                case "storm": return StoryChoice("第六章 · 公约第一夜", "Chapter Six · First Night of the Commons", "story_read_storm");
                case "hearing": return StoryChoice("第七章 · 众人的晨钟", "Chapter Seven · The People’s Dawn", "story_read_hearing");
                case "roadcharter": return StoryChoice("第八章 · 共同行路", "Chapter Eight · Roads for Everyone", "story_read_roadcharter");
                case "supply_inquest": return StoryChoice("第九章 · 送达之后谁守路", "Chapter Nine · Who Keeps the Routes Open", "story_read_supply_inquest");
                case "lastdirective": return StoryChoice("第十章 · 最后指令", "Chapter Ten · The Last Directive", "story_read_lastdirective");
                case "civicconduit": return StoryChoice("第十一章 · 重接公共电网", "Chapter Eleven · Reconnecting the Public Grid", "story_read_civicconduit");
                case "civic_aftermath": return StoryChoice("第十二章 · 电网之后，谁守着路", "Chapter Twelve · Who Keeps the Road After the Grid", "story_read_civic_aftermath");
                case "ending": return StoryChoice("你决定的边境", "The frontier you chose", "story_read_ending");
                default: return null;
            }
        }
        // Chapter navigation is derived entirely from existing milestone flags.
        // No new save field or reward can be introduced by browsing the book.
        private static string StorySectionForPage(string page)
        {
            if (page == "origin" || page == "roots" || page == "orders" || page == "wardens") return "origins";
            if (page == "forest" || page == "testimonies" || page == "ruins" || page == "mine" || page == "temper" || page == "triad" || page == "blackout" || page == "citadel") return "campaign";
            if (page == "harbor" || page == "tidebound" || page == "desert" || page == "glasswind" || page == "mountain" || page == "covenant") return "farshore";
            return "after";
        }
        private static string[] StoryPagesForSection(string section)
        {
            if (section == "origins") return new[] { "origin", "roots", "orders", "wardens" };
            if (section == "campaign") return new[] { "testimonies", "forest", "ruins", "mine", "temper", "triad", "blackout", "citadel" };
            if (section == "farshore") return new[] { "harbor", "tidebound", "desert", "glasswind", "mountain", "covenant" };
            // Keep the retrospective ending after the later reconstruction and civic chapters.
            // Adjacent navigation still skips chapters the player has not unlocked.
            return new[] { "signal", "atlas", "counterpoint", "commons", "skywatch", "abyss", "afterlight", "storm", "hearing", "roadcharter", "supply_inquest", "lastdirective", "civicconduit", "civic_aftermath", "ending" };
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
                case "testimonies":
                    headingZh="未被计入的人：三位活着的证人";
                    headingEn="The Uncounted: Three Living Witnesses";
                    zh="堡垒的三枚星核供能账里只有输出，没有根系伤痕、渡口等待的孩子和矿工应当结束工作的一刻。三位当事人愿意署名，但你必须亲自恢复真实用途、解除封锁守卫，才能把证据送给莉雅。\n\n"+
                        (Has("witness_complete_1")?
                            "莫芽署名：森林本来能用星核余量修补断裂根系。你亲手帮它恢复自愈。\n":
                            Has("witness_read_1")?"受伤根系的用途已核实，莫芽尚未署名。\n":
                            "密林里的莫芽仍等你查证根系的真实用途。\n")+
                        (Has("witness_complete_2")?
                            "阿菱署名：日月潮水闸原本向所有旅人开放，渡口不该被货运许可证垄断。\n":
                            Has("witness_read_2")?"公共渡口旧章程已找回，阿菱尚未署名。\n":
                            "遗迹里的阿菱仍等你找回公共渡口记录。\n")+
                        (Has("witness_complete_3")?
                            "维恩署名：过热的矿机必须允许停工和撤离，不应将其写成怠工。\n":
                            Has("witness_read_3")?"矿机休息条款已恢复，维恩尚未署名。\n":
                            "矿场里的维恩仍等你证明矿工的停工权。\n")+
                        (AllWitnessesFiled?"\n三个签名汇成不可删除的公开证据，并能帮助你破解第四章的堡垒保密审计。":"\n证据尚未全部保全；机器不能替活着的人签字。");
                    en="The three starcore accounts recorded shield output, not the roots it wounded, children waiting beyond the sluice, or miners who needed to rest. Each resident can sign only after you restore the physical record and beat the guards trying to erase it.\n\n"+
                        (Has("witness_complete_1")?
                            "Moya signed: the forest once used spare energy to heal wounded roots.\n":
                            Has("witness_read_1")?"The roots' purpose is verified, but Moya has not signed.\n":
                            "Moya still awaits the truthful forest reading.\n")+
                        (Has("witness_complete_2")?
                            "Arin signed: the tidal tablets gave public passage to all travelers, not just freight license holders.\n":
                            Has("witness_read_2")?"The public crossing charter is found, but Arin has not signed.\n":
                            "Arin still awaits the public crossing account.\n")+
                        (Has("witness_complete_3")?
                            "Venn signed: an overheated mine must let its workers shut machines down and leave.\n":
                            Has("witness_read_3")?"Workers' rest protocol was recovered, but Venn has not signed.\n":
                            "Venn still seeks the miners' heat-evacuation rule.\n")+
                        (AllWitnessesFiled?"\nThree living signatures now accompany Lyra's Chapter Four audit and weaken the guardian's security.":"\nThese accounts are incomplete. Machines cannot sign for the living.");
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
                case "commons":
                    headingZh = "公共信标：谁有权关掉灯？";
                    headingEn = "Citadel Commons Relay: Who Can Turn Out the Light?";
                    zh = "艾拉保管着星陨后无人签收的供给请求。堡垒守卫已败，粮食、医护和撤离灯线却仍按照九年前的封闭权限运行。\n\n" +
                        (Has("commons_relay_sealed") ?
                            "你亲自穿过旧守卫重连了三道民用线路。关闭粮道、病房和撤离灯线，不应该再是机器无人问责的决定。\n" :
                            "三道公共信标仍在旧协议控制下。西侧粮路、东侧医护与东南撤离灯线等待修复。\n") +
                        (Has("commons_relay_complete") ?
                            "\n艾拉已经将访问密钥公开保存在城镇。民用供给不再由单人或封闭机器决定。" :
                            "\n公开授权尚未交给艾拉，最后一份证词仍在等待署名。");
                    en = "Ayla kept the supply requests nobody acknowledged after the fall. Even after the guardian fell, food, care and evacuation light remained under private permissions from nine years ago.\n\n" +
                        (Has("commons_relay_sealed") ?
                            "You crossed real security lines to restore the three civilian circuits. Food routes, treatment and evacuation lamps must no longer be switched off without public accountability.\n" :
                            "Three public relay stations remain bound by old security: western food routes, eastern medical care and southeastern evacuation lights.\n") +
                        (Has("commons_relay_complete") ?
                            "\nAyla published the access key in the town archive, placing civilian supply under public oversight." :
                            "\nThe final testimony still awaits a public signature from Ayla.");
                    break;
                case "counterpoint":
                    headingZh = "星根失谐：和声属于谁"; headingEn = "Starroot Counterpoint: Whose Song?";
                    zh = "听纹师芮记下了九年前的一次失谐：堡垒让每处星根只向中央供能，原本彼此补偿的根、潮与铁再也听不到其他地区。\n\n" +
                        (Has("root_harmony_1") ? "密林第一道根音已重新归档。它记录的不是可开采星晶，而是活土如何让老树与溪流一同复苏。\n" : "密林根音尚未取回。\n") +
                        (Has("root_harmony_2") ? "沉没遗迹的潮音描述失去的退潮时刻：水闸应按活人的需要开启，而不是听从九年前的印章。\n" : "沉没遗迹潮音仍在沉睡。\n") +
                        (Has("root_harmony_3") ? "矿场铁音留下停工的节拍：锻造与抽取之间，本该有让人回家的间隙。" : "矿场铁音还未校准。") +
                        (Has("root_harmony_complete") ? "\n\n三道音纹终于被封存为公开读数。和声不是另一种统治工具，它证明了没有一处土地应该永远独奏。" : "");
                    en = "Rui recovered a nine-year-old dissonance: the citadel ordered every anchor to feed the center. Root, tide and iron, once balanced by listening to one another, could no longer hear distant needs.\n\n" +
                        (Has("root_harmony_1") ? "Thornwood's root reading is archived: living soil repairs roots and streams together.\n" : "The woodland root reading remains lost.\n") +
                        (Has("root_harmony_2") ? "The tide reading remembers when sluices opened for living travelers instead of old guild seals.\n" : "The drowned tide reading remains silent.\n") +
                        (Has("root_harmony_3") ? "Ironreach's rhythm preserves the pause between forging and extraction, long enough for miners to go home." : "The mine's iron reading is still uncalibrated.") +
                        (Has("root_harmony_complete") ? "\n\nAll three readings now belong to the public archive. Counterpoint is not another tool of rule: no land should have to sing alone." : "");
                    break;
                case "glasswind":
                    headingZh="玻璃风脉：为谁吹来的风";headingEn="Glasswind: Who Owns the Wind?";
                    zh="沙海的旧风压阀曾经根据货运许可证分配风与阴凉。测绘员米娅指出，机器记住了九年前的旧指令，却忘记了如今依靠这些道路生活的人。她将三座回路交给你，要求亲自重写公共通行权。\n\n" +
                        (Has("glasswind_1")?
                            "北沙阀已经解除货运锁，风路重新覆盖徒步旅人。\n":
                            "北沙阀仍在等人亲自守住控制圈。\n") +
                        (Has("glasswind_2")?
                            "东脊阀不再将遮阳只留给有通行证的车队。\n":
                            "东脊阀仍将人们挡在旧权限之外。\n") +
                        (Has("glasswind_3")?
                            "南沙阀已经打通，南部行路者也能寻到公共阴凉。":
                            "南沙阀还没有完成两轮实际防卫。") +
                        (Has("glasswind_complete")?
                            "\n\n米娅已把新风路图公开交给居民，三座风阀不再要求许可证。真正改变的是对机器下令的人。":
                            Has("glasswind_all_sealed")?
                            "\n\n三个阀门均已封存，但公共路线仍等待米娅正式归档。":
                            "\n\n这条风路尚未全部连通。");
                    en="The old wind valves once rationed shade by freight permits. Mia found that the machines still obey orders issued nine years ago, without recognizing the people who now live along these roads. She asks you to rewrite their public access by holding three real defense sites.\n\n" +
                        (Has("glasswind_1")?
                            "The northern valve now carries cool wind to walkers without freight papers.\n":
                            "The northern capture ring has not yet been held.\n") +
                        (Has("glasswind_2")?
                            "The eastern ridge no longer reserves shade for approved convoys.\n":
                            "The eastern ridge still honors the obsolete lock.\n") +
                        (Has("glasswind_3")?
                            "The southern valve offers public refuge to late travelers.":
                            "The southern valve still waits for a defended final seal.") +
                        (Has("glasswind_complete")?
                            "\n\nMia published the routes for every resident. The valves now welcome travelers without permits; what changed was who could give them orders.":
                            Has("glasswind_all_sealed")?
                            "\n\nAll relays are secured, but Mia has not yet published the public chart.":
                            "\n\nThe wind circuit remains incomplete.");
                    break;
                case "blackout":
                    headingZh="长夜撤离：墙下的人";headingEn="Blackout Evacuation: Those Beneath the Wall";
                    zh="星陨堡垒把三枚星核的供给汇进同一面屏障。一天夜里，备用灯线终于无法分担这项负荷，三片街区骤然陷入黑暗。\n\n你不只是在审计台上改一个数字，而是走入街巷，必须亲自带着每位居民从失灵的灯下回到公共避难所。电弧依旧会击倒不幸被困的人；如果你跑得太快，他们也会失去方向。\n\n"+
                        (Has("blackout_complete")?
                            "【撤离名单已公布】莱娜、伊恩与奥姆都活着回到了灯下。备用照明证明了比单一护城屏障更温柔、也更复杂的办法已经有人开始践行。\n\n"+
                            (Has("blackout_peace")?"【厄洛退让】三位证人签名、三位镇民的行动承诺和这份撤离名单，让统领选择停用旧命令而非决斗。":"【决战尚待决定】这份名单不能独自替所有人决定星核归属，但能让厄洛知道承担改变风险的不止他一人。"):
                            "【名单尚未公开】你亲眼见到了备用灯和逃生街区的风险。尚未完成三人安全交接，不能提前宣称全员获救。");
                    en="The citadel wired three starcores into one shield. One night, the backup lamps could no longer support the load, leaving three neighborhoods in darkness.\n\nChanging a number at an audit desk was not enough. You had to walk frightened neighbors home one at a time, protecting them from delayed arcs and staying close enough for them to follow.\n\n"+
                        (Has("blackout_complete")?
                            "[The roster is public] Lena, Ian and Orme returned to safety. Emergency lamps proved that people could build a more humane alternative to a single wall.\n\n"+
                            (Has("blackout_peace")?"[Orrin stood down] Three signed testimonies, town allies and this actual evacuation convinced Orrin to retire the old order rather than fight.":"[A confrontation remains] A rescue roster cannot settle the core's fate alone, but shows Orrin he is not alone in bearing risk."):
                            "[The roster is not published] You saw the dangers to civilians firsthand. Their actual safe return has not yet been proved.");
                    break;
                case "triad":
                    headingZh="第四章：三核审计";headingEn="Chapter Four: Three-Core Reckoning";
                    zh="森林、遗迹、矿场的三枚星核原本不是供城墙消耗的矿石。它们分别调节生命的再生、旅人过河的时间与矿工从机器前退下的权利。莉雅怀疑堡垒将这三项民生都计成零，才让屏障的支出表永远显得正确。\n\n"+
                        (Has("triad_evidence_1")?"你从密林印记找回了森林再生账：根系和泥土原本得到修复能源。\n":"森林供能印记仍在堡垒待查。\n")+
                        (Has("triad_evidence_2")?"你找回了遗迹通行账：水闸原先为真实的旅人留出时间。\n":"遗迹水位印记仍在堡垒待查。\n")+
                        (Has("triad_evidence_3")?"你找回了矿场作业账：过热时必须允许矿工撤离。\n":"矿场停机印记仍在堡垒待查。\n")+
                        (Has("triad_defense_clear")?"旧保密卫兵已在实际交战后停止执行封锁。\n":"中央审计台仍受保密卫兵控制。\n")+
                        (Has("triad_sealed")?"三份原始日志已在中央审计台保全。\n":"原始日志尚未完成保全。\n")+
                        (AllWitnessesFiled?"\n三位证人亲笔署名的记录削弱了旧保密卫兵，也让居民的名字不再被抹除。":"")+
                        (Has("triad_complete")?"\n莉雅将这份账目公之于众。三枚星核的故事终于合成一段完整的证据，而非三个供能战利品；厄洛必须亲自回答它揭露的代价。":"\n调查仍未公开。完成证据链后，亲自把审计交给莉雅。");
                    en="The three starcores of forest, ruins and mine were never fuel trophies. They governed renewal, the time travelers had to cross a river, and the right of miners to leave overheating machinery. Lyra suspects the shield's ledger simply valued all three at zero.\n\n"+
                        (Has("triad_evidence_1")?"You recovered the forest renewal account: roots and living soil once received the spare energy.\n":"The forest supply seal remains unread.\n")+
                        (Has("triad_evidence_2")?"The ruins account records an ebb passage open to real travelers.\n":"The sluice passage seal remains unread.\n")+
                        (Has("triad_evidence_3")?"The mine account recognizes evacuation when machines overheat.\n":"The miners' rest seal remains unread.\n")+
                        (Has("triad_defense_clear")?"The obsolete security guardian was defeated in real combat.\n":"The central audit remains under armed security.\n")+
                        (Has("triad_sealed")?"The original audit was preserved at the central console.\n":"The original central record remains unsecured.\n")+
                        (AllWitnessesFiled?"\nThree living witness signatures weaken the audit guards and can no longer be erased from public records.":"")+
                        (Has("triad_complete")?"\nLyra published the complete evidence. Three chapters become one record of the real cost of the shield, and Orrin must answer for it.":"\nThese findings remain unpublished. Preserve the ledger and carry it to Lyra.");
                    break;
                case "temper":
                    headingZh="余烬回火：能停下的炉";
                    headingEn="Ember Temper: A Furnace That Can Rest";
                    zh="赤铁矿场原有矿工掌握的换班降温规程。九年前，堡垒把它连接到军用产量表，休息被当作浪费，管线被封锁，锻炉只记得永不熄灭的命令。\n\n"+
                        (Has("iron_temper_vent_seen_1")?
                            "你亲手打开西北支管，让矿工第一次看到压力计下降。\n":
                            "西北支管的高温锁仍未解除。\n")+
                        (Has("iron_temper_vent_seen_2")?
                            "东侧散热阀接通，最后的重装监工被迫现身。":
                            "东侧军用散热锁仍未拆除。")+
                        (Has("iron_temper_complete")?
                            "\n\n你在真正的战斗中击败守卫，将工房交还矿工。诺尔公开新的回火配方：每次强化只需两木、一晶、一零件。停炉终于不再是失败，而意味着矿工能够下班。":
                            "\n\n工房尚未完成公共交接。发现管线不等于击败守卫。");
                    en="Ironreach miners once controlled their own cooling and shift changes. Nine years ago the citadel chained the furnace to military quotas. Rest was labeled wasted output, valves were locked and the forge was told never to stop.\n\n"+
                        (Has("iron_temper_vent_seen_1")?
                            "You opened the northwest pipe, letting miners see the pressure gauge finally fall.\n":
                            "The northwest high-pressure lock remains sealed.\n")+
                        (Has("iron_temper_vent_seen_2")?
                            "The eastern coolant valve is free; the armored overseer has emerged.":
                            "The eastern military coolant valve remains locked.")+
                        (Has("iron_temper_complete")?
                            "\n\nYou defeated the real overseer and returned the foundry to its workers. Noll's public upgrade recipe now costs two timber, one crystal and one part. A furnace can finally stop so its workers can go home.":
                            "\n\nThe foundry has not yet been handed over. Finding valves is not the same as defeating the guards.");
                    break;
                case "tidebound":
                    headingZh="潮缚货单：谁被记作货损";
                    headingEn="Tidebound Manifest: Who Was Written Off?";
                    zh="托兰在雾港旧货运册里发现三项被注销的民用物资。这些不是什么船上的财宝：医药是给岸上的伤者，粮食是给避难的居民，航道记录是让下一艘船安全回来。\n\n" +
                        (Has("tide_salvage_seen_1") ?
                            "西侧潮滩的药品名单已被你亲手找回，上面的病人仍等着药。\n" :
                            "西侧潮滩的药品名单仍未被找到。\n") +
                        (Has("tide_salvage_seen_2") ?
                            "内湾粮食原来一直配给给九年前便已失踪的船只。\n" :
                            "内湾的粮食货单还未核实。\n") +
                        (Has("tide_salvage_seen_3") ?
                            "南岛保留了未被篡改的潮汐模型，证明航路本可公开。" :
                            "南岛的真实潮汐模型尚未取回。") +
                        (Has("tide_salvage_complete") ?
                            "\n\n所有物资已经亲自运回码头并交给居民。托兰更新了观测站，每次稳潮比过去多十五秒。这次公开的不是海盗的战利品，而是居民本来就该拥有的东西。" :
                            "\n\n货物尚未完整交付。眼睛看到过的记录可以留在手帐里，但没有送回去的物资不能算作完成救援。");
                    en="Tolan found three civilian shipments written off in Mistport's freight ledgers. They were never treasure: medicine belonged to shore patients, food to displaced families, and a tide chart to the next vessel trying to return safely.\n\n" +
                        (Has("tide_salvage_seen_1") ?
                            "You recovered a western shoal medicine roster naming patients still waiting for care.\n" :
                            "The western medicine roster remains missing.\n") +
                        (Has("tide_salvage_seen_2") ?
                            "The inner-bay food had been allocated to ships lost nine years before.\n" :
                            "The inner-bay provisions have not been verified.\n") +
                        (Has("tide_salvage_seen_3") ?
                            "The southern isle preserved an untampered tide model and a public safe route." :
                            "The southern island's original tide model is still missing.") +
                        (Has("tide_salvage_complete") ?
                            "\n\nYou physically returned every shipment to residents. Tolan updated Tidewatch to hold the ebb fifteen seconds longer. These were not a raider's trophies: they always belonged to the people." :
                            "\n\nThe cargo is not yet delivered. What you witnessed may remain in your journal, but nothing not brought home can count as a finished rescue.");
                    break;
                case "skywatch":
                    headingZh = "霜钟夜空：从谁的视角预报寒潮？";
                    headingEn = "Frostchime Skywatch: Whose Forecast?";
                    zh = "观星师伊澜找到了一套尚未公开的旧观测记录。高原的冰光警报不只是机械故障：它把居民的求救声当作背景噪声，只留下堡垒想听的星陨预报。\n\n" +
                        (Has("skywatch_prism_1") ?
                            "北方的霜棱镜记录了天光出现前的第一场冻雨。\n" :
                            "北方的霜棱镜读数尚未取回。\n") +
                        (Has("skywatch_prism_2") ?
                            "东方的光棱镜证实，冷光并非太阳或火焰。\n" :
                            "东方的光棱镜仍未校准。\n") +
                        (Has("skywatch_prism_3") ?
                            "南方的炉火棱镜刻下了居民守着营火等候归人的夜晚。" :
                            "南方的炉火棱镜还没有发出回声。") +
                        (Has("skywatch_defeated") ?
                            "\n\n古观星守卫已经停止攻击旅人。" : "") +
                        (Has("skywatch_complete") ?
                            "\n伊澜把气候光谱公开交给所有居民。观星台旁留下永久避寒穹顶；预报不再只为堡垒服务。" :
                            "\n\n原始光谱尚未向镇民公开。") +
                        (Has("skywatch_afterglow_complete") ?
                            "\n\n旅人们又找回了北方救援足迹、东方折射误报与南方守夜的余辉。三座棱镜成为人人可用的永久避寒信标。" :
                            Has("skywatch_afterglow_1") || Has("skywatch_afterglow_2") ||
                            Has("skywatch_afterglow_3") ?
                            "\n\n一部分旅人余辉已经回收，其余记录还在霜钟高原等待。" : "");
                    en = "Yilan found a weather record hidden inside the observatory. The skyfall alarm did more than malfunction: it treated travelers' distress signals as background noise and preserved only the forecast wanted by the citadel.\n\n" +
                        (Has("skywatch_prism_1") ?
                            "The northern lens remembers freezing rain before the lights appeared.\n" :
                            "The northern frost reading is still missing.\n") +
                        (Has("skywatch_prism_2") ?
                            "The eastern lens proves that the cold beam came from neither sun nor fire.\n" :
                            "The eastern light reading remains unaligned.\n") +
                        (Has("skywatch_prism_3") ?
                            "The southern hearth lens preserved the people keeping a fire for those still missing." :
                            "The southern hearth reading remains silent.") +
                        (Has("skywatch_defeated") ?
                            "\n\nThe ancient skywatch guardian no longer attacks travelers." : "") +
                        (Has("skywatch_complete") ?
                            "\nYilan published the forecast for everyone. A permanent shelter now surrounds the observatory; weather information no longer serves only the citadel." :
                            "\n\nThe original spectrum has not yet been released to the town.") +
                        (Has("skywatch_afterglow_complete") ?
                            "\n\nThe three last traveler records reveal northern rescuers, eastern false light, and the southern night watch. Three public warming beacons now shelter all who cross the highlands." :
                            Has("skywatch_afterglow_1") || Has("skywatch_afterglow_2") ||
                            Has("skywatch_afterglow_3") ?
                            "\n\nSome travelers' afterglows have been recovered. More records remain in Frostchime." : "");
                    break;
                case "abyss":
                    headingZh = "地脉深渊：星根的伤口"; headingEn = "Root Abyss: A Wound in the Network";
                    zh = "远岸达成治理协定后，居民才允许打开深入星根的旧备用管道。六层遗迹存着祝福、危险和未结算物资。\n\n深入不是为了占有更多力量，而是检验玩家在面对高风险回报时，能否守住已有的互助约定。未能安全带回的东西，不应被记作属于自己的资源。";
                    en = "Only after a farshore covenant can travelers enter the six old reserve conduits beneath the starroot lattice. Boons, danger and unbanked supplies coexist there.\n\nThe descent tests whether the promise of shared care survives the temptation of greater extraction. Nothing is yours until it is brought back safely.";
                    break;
                case "storm":
                    headingZh="第六章：公约第一夜";
                    headingEn="Chapter Six: First Night of the Commons";
                    zh="战后第一场星雨到来时，军用调谐网不再自动决定谁能优先点灯。莉雅把仅有的八单位公共储备摆在三地居民面前，让受过星核代价的人自己决定庇护和生产的比例。\n\n"+
                        "在第一次有真正民用管控的风暴里，庇护优先消耗三单位并加强公共疗养，供能优先仅消耗两单位但可以维持路灯、渡船和矿场工作。每个地区只有亲自通电、支付真实材料才算落实。\n\n";
                    en="The first storm after the ending could not be governed by an unquestionable military grid. Lyra put eight units of public reserves before three living regions, letting people harmed by the old system choose their own balance between shelter and work.\n\n"+
                        "Shelter-first uses three reserve units for real nearby healing. Utilities-first uses only two and keeps lamps, ferries and workshops running. Every decision needs actual installation and materials.\n\n";
                    for(int region=1;region<=3;region++)
                    {
                        if(StormPolicy(region)==0)
                        {
                            zh+=StormRegionName(region,false)+"：还未真正分配储备，也没有宣称避险完成。\n";
                            en+=StormRegionName(region,true)+": allocation has not been installed; the storm still threatens the district.\n";
                        }
                        else
                        {
                            zh+=StormRegionName(region,false)+"："+(StormPolicy(region)==1?
                                "居民把三单位给庇护灯，修复了真实的疗养区域。":
                                "居民把两单位给供能线路，保留了工房与巡路功能。")+"\n";
                            en+=StormRegionName(region,true)+": "+(StormPolicy(region)==1?
                                "residents spent three units on working public shelter lamps.":
                                "residents spent two units on continuous civic lights and utilities.")+"\n";
                        }
                    }
                    zh+="\n已送达居民的星雨热汤救助："+StormReliefCount+"/3。"+
                        (StormReliefAll?"三位见证人的真实援助让镇上避难设施可以帮助更多后来者。":"这些捐赠是自愿救助，不替代地区配电。")+
                        "\n八单位公共储备已分配："+StormBudgetSpent+
                        "，余量："+StormBudgetLeft+"；"+
                        (Has("storm_complete")?
                            "三座余烬镇星灯已经按照原有结局的能源秩序被真正调谐，第一次星雨的公约已公开。":
                            Has("storm_tuned")?
                            "镇内的三个公共灯站已实际调谐，但公约仍需回议事台公开。":
                            "公约尚未真正经受全部检验，不能提前宣告成功。");
                    zh+=Has("storm_orrin_open")?
                        "\n厄洛亲自开放城市剩余供能，让镇民避难与疗养灯更明亮。":
                        Has("storm_orrin_guard")?
                        "\n厄洛亲自将城市余量保留给堡垒的民用备用灯，不再是军事独断。":"";
                    en+=Has("storm_orrin_open")?
                        "\nOrrin opened the unused power for the Emberfall public refuge.":
                        Has("storm_orrin_guard")?
                        "\nOrrin assigned the remaining supply to civilian backup lights at the Citadel.":"";
                    en+="\nReal meals delivered to storm-affected residents: "+StormReliefCount+"/3. "+
                        (StormReliefAll?"All three living witnesses delivered aid; the public shelter can sustain more late travelers.":"Optional aid never substitutes for a fair regional energy allocation.")+
                        "\nPublic reserve allocated: "+StormBudgetSpent+
                        " of eight; remaining: "+StormBudgetLeft+". "+
                        (Has("storm_complete")?
                            "Three actual town lamps were tuned according to the energy order dictated by the original ending. The first-storm charter is public.":
                            Has("storm_tuned")?
                            "The town's three public lamps have been physically tuned, but the charter has not yet been published.":
                            "The final civic-grid test has not been completed; the frontier cannot claim success yet.");
                    break;
                case "hearing":
                    headingZh="第七章：众人的晨钟";
                    headingEn="Chapter Seven: The People's Dawn";
                    zh="第一场星雨、三地公开设施与厄洛的民用余量协定没有让所有矛盾自动消失。莫芽还记得受伤的根，阿菱想起等候渡口的孩子，维恩仍然害怕下一次不允许停机的命令。\n\n"+
                        "莉雅在余烬镇召开听证：每个地区的居民自己选择一项优先承诺，而不是由单一统领、主角或者军用账本替他们投票。\n\n";
                    en="The first storm, three public works and Orrin's civilian accord did not eliminate disagreement. Moya remembers wounded roots, Arin remembers children denied crossing, and Venn remembers machines no worker could stop.\n\n"+
                        "Lyra convenes a hearing in Emberfall: each region's resident chooses an independent public obligation rather than being represented by military ledgers or even by the hero.\n\n";
                    for(int ri=1;ri<=3;ri++)
                    {
                        string[] peopleZh={"","莫芽","阿菱","维恩"};
                        string[] peopleEn={"","Moya","Arin","Venn"};
                        int opinion=HearingVoice(ri);
                        zh+=peopleZh[ri]+"："+
                            (opinion==0?"尚未亲自签署第七章的区域选择。":
                             opinion==1?new[]{"","活根疗养优先。","公共渡口对所有居民开放。","让工人自主维修与回收。"}[ri]:
                             new[]{"","夜间公共巡路灯优先。","河岸防洪庇护优先。","紧急停机与冷却优先。"}[ri])+"\n";
                        en+=peopleEn[ri]+": "+
                            (opinion==0?"Has not yet signed a Chapter Seven regional vote.":
                             opinion==1?new[]{"","Living-root care takes priority.","The crossing stays open to every traveler.","Workers control repairs and recycling."}[ri]:
                             new[]{"","Public night-road lamps take priority.","Public flood refuge takes priority.","Emergency shutdown protection takes priority."}[ri])+"\n";
                    }
                    zh+=HearingComplete?
                        "\n【公开议决已通过】三地居民独立署名，余烬镇的"+(HearingVerdict==1?"公共医疗灯":"公用应急耐力灯")+
                        "已真正运作。新誓言刻在三地现场，不会因为冒险者离开就消失。":
                        "\n【听证尚未结束】公民誓言不齐备时，公会不会替缺席者决定公约。";
                    en+=HearingComplete?
                        "\n[Public verdict ratified] All three residents signed, and Emberfall's "+
                        (HearingVerdict==1?"public medical lantern":"emergency energy lantern")+
                        " actually operates. Local pledges remain inscribed in their own regions.":
                        "\n[Hearing pending] The guild cannot decide for absent regional voices.";
                    if(HearingComplete)
                    {
                        zh+=HearingOrrinResponsibility==1?
                            "\n厄洛又将两份实际热汤交给了公共医护，堡垒内原来的避难所开始为所有归来者疗伤。":
                            HearingOrrinResponsibility==2?
                            "\n厄洛将两份零件交给志愿巡路队，堡垒旧祭坛开始提供公共耐力补给。":
                            "\n最后一份责任仍需厄洛本人回应：到星陨堡垒面对他，而不是替他说出承诺。";
                        en+=HearingOrrinResponsibility==1?
                            "\nOrrin handed over two tangible meals to civilian medics. The old citadel shelter now actually heals returning travelers.":
                            HearingOrrinResponsibility==2?
                            "\nOrrin released two real parts to volunteer roadwardens, and the old shrine now supplies public energy.":
                            "\nOrrin must personally answer for his share of the charter. The hero cannot sign on his behalf.";
                    }
                    break;
                case "roadcharter":
                {
                    headingZh="第八章：共同行路";
                    headingEn="Chapter Eight: Roads for Everyone";
                    zh="第七章的听证确认了谁有权作出公共承诺，却不能自动把人送到公共设施。莉雅因此把公会地图带回现场：荆棘密林、沉没遗迹和赤铁矿场东侧的旧路标，分别保存着被省略的通行、渡口与撤离证据。\n\n"+
                        "三处证据必须由你亲自走到并核验。道路不该只在地图上开放，也不能让居民再次成为运货清单上的数字。\n\n";
                    en="Chapter Seven's hearing established who may make public commitments, but it did not carry anyone to the services. Lyra takes the guild's maps back into the field: eastern waymarkers in Thornwood, Drowned Ruins and Ironreach preserve evidence about erased footpaths, public crossings and evacuation rights.\n\n"+
                        "You must visit and verify all three records yourself. A road is not public merely because a map says so, and residents are not cargo on a manifest.\n\n";
                    string[] charterRegionsZh={"","荆棘密林","沉没遗迹","赤铁矿场"};
                    string[] charterRegionsEn={"","Thornwood","Drowned Ruins","Ironreach"};
                    for(int charterRegion=1;charterRegion<=3;charterRegion++)
                    {
                        bool charterVisited=Has("roadcharter_report_"+charterRegion);
                        zh+=charterRegionsZh[charterRegion]+"："+
                            (charterVisited?"东部旧路标已亲自核验。":"尚未实地核验；编年册不会替你虚构证据。")+"\n";
                        en+=charterRegionsEn[charterRegion]+": "+
                            (charterVisited?"Eastern waymarker personally verified.":"Not yet surveyed; the chronicle will not invent evidence.")+"\n";
                    }
                    zh+="\n【实地进度】"+RoadCharterReports+"/3 份道路证据已收集。";
                    en+="\n[Field progress] "+RoadCharterReports+"/3 route reports collected.";
                    if(RoadCharterComplete&&Has("roadcharter_public"))
                    {
                        zh+="\n\n【行人优先】公会公开了可步行的民用路线；所有职业获得常驻步速提升。";
                        en+="\n\n[Pedestrian-first routes] Public walking routes are published, granting every profession a permanent movement-speed improvement.";
                    }
                    else if(RoadCharterComplete&&Has("roadcharter_reserve"))
                    {
                        zh+="\n\n【民用应急供能】公共救援路灯保留应急储备，所有职业获得常驻耐力恢复提升。";
                        en+="\n\n[Civic emergency reserve] Public rescue lamps retain emergency power, granting every profession improved stamina recovery.";
                    }
                    else if(RoadCharterComplete)
                    {
                        zh+="\n\n公约已完成，但这份旧存档没有记录最终服务选择。";
                        en+="\n\nThe charter is complete, but this legacy save does not record the final service choice.";
                    }
                    else
                    {
                        zh+="\n\n【公约尚未发布】证据收集后，仍须回余烬镇公会公开道路地图，并由你选择行人通行或民用应急供能的优先方案。";
                        en+="\n\n[Charter pending] After collecting the reports, return to the Emberfall guild to publish the map and choose pedestrian access or civilian emergency reserves.";
                    }
                    break;
                }
                case "civicconduit":
                    headingZh="第十一章：重接公共电网";
                    headingEn="Chapter Eleven: Reconnecting the Public Grid";
                    zh="最后的军令公开，并不意味着九年前的自动输电器就会自己停下来。西端熔断器旁留下了一份由维修工签名的告示：每一个调谐阀门都必须有人真实走到现场，在绿色的相位窗口内重新连接。失去稳定的电网会发出真正的危险电弧，而不是只有表格上的红字。\n\n";
                    en="Publishing the last military order did not repair the nine-year-old automatic power grid. A mechanic's signed note at the western fuse desk requires a person to walk to every valve and align it during its own green phase. The old conductor discharges real, dodgeable arcs—not merely warning colors on a spreadsheet.\n\n";
                    if(CivicConduitComplete)
                    {
                        zh+="【三处阀门真正调谐完毕】熔断器接通的公共网络已开始服务八地实际修好的公共避险棚。";
                        en+="[All three physical valves tuned] The reclaimed power grid now serves truly rebuilt refuges across eight regions.";
                        if(LastDirectiveChoice==1)
                        {
                            zh+="\n社区选择公开巡路与停机监督，因此安全时各地避险棚会补充额外耐力。";
                            en+="\nPublic oversight routes extra safe stamina recovery to each rebuilt refuge.";
                        }
                        if(LastDirectiveChoice==2)
                        {
                            zh+="\n社区选择无条件紧急庇护，因此安全时各地避险棚会提供额外疗伤。";
                            en+="\nUnconditional public shelter routes extra safe medical recovery to each rebuilt refuge.";
                        }
                    }
                    else
                    {
                        zh+="【仍未完成】本册不会替旅行者走完现场线路，也不会虚构三处成功的调谐记录。";
                        en+="[Not yet complete] This chronicle cannot walk the circuit or invent three successful calibrations.";
                    }
                    break;
                case "civic_aftermath":
                    headingZh="第十二章：电网之后，谁守着路";
                    headingEn="Chapter Twelve: Who Keeps the Road After the Grid";
                    zh="三处阀门已经在现场重新调谐，电网终于不再只服从九年前的旧命令。但接通线路不是结局：避险棚需要真实的补给，公共服务需要维护者，居民也必须能看见每一份资源去了哪里。\n\n"+
                        "这一页把已经发生的选择放在同一张账桌上。它只记录你亲自完成的事情，不会替尚未到访的地区补写胜利。\n\n";
                    en="All three valves have been physically retuned. The grid no longer answers only to a nine-year-old command. But reconnecting a line is not an ending: refuges need real supplies, public services need named stewards, and residents must be able to see where every resource goes.\n\n"+
                        "This page brings existing choices to one table. It records what has actually been done and will not invent victories for places you have not visited.\n\n";
                    zh+="【公共避险棚】"+FarShelterCount+"/8 处已建成。";
                    en+="[Public refuges] "+FarShelterCount+"/8 have been built.";
                    if(Has("far_refuge_atlas"))
                    {
                        zh+=" 八地星图已经公开，旅人可以按真实路线寻找避险处。";
                        en+=" The eight-region atlas is published, so travelers can find refuges along real routes.";
                    }
                    else
                    {
                        zh+=" 总图尚未发布，未建成的避险棚不会被算作可用设施。";
                        en+=" The shared atlas is not yet published; an unbuilt refuge is not counted as a working service.";
                    }
                    int[] sites={0,3,5};
                    string[] siteZh={"引水渠","赤铁矿场","雾港"};
                    string[] siteEn={"Canal","Ironreach","Mistport"};
                    zh+="\n\n【居民长期服务】"+CivicSupplyResolvedCount+"/3 处已签署选择。\n";
                    en+="\n\n[Long-term community services] "+CivicSupplyResolvedCount+"/3 regional choices are signed.\n";
                    for(int i=0;i<sites.Length;i++)
                    {
                        bool clinic=CivicSupplyClinic(sites[i]);
                        bool watch=CivicSupplyWatch(sites[i]);
                        zh+=siteZh[i]+"："+(clinic?"居民医疗点已选定。":watch?"居民守路岗已选定。":"尚未签署；编年册不会替居民投票。")+"\n";
                        en+=siteEn[i]+": "+(clinic?"Residents chose a clinic.":watch?"Residents chose a road watch.":"No vote is signed; the chronicle will not vote for the residents.")+"\n";
                    }
                    zh+=CivicSupplyComplete?
                        "公开审计已发布，三地的物资去向与永久服务均可查阅。":
                        "公开审计仍待完成；所有选择齐备后，还要回旅店亲手公布账目。";
                    en+=CivicSupplyComplete?
                        "The public audit is published; each contribution and lasting service is recorded.":
                        "The public audit is still pending; after all choices are signed, return to the Inn and publish the ledger.";
                    if(LastDirectiveChoice==1)
                    {
                        zh+="\n\n【公共监督】居民保留巡路与停机监督权；安全时避险棚额外恢复耐力。";
                        en+="\n\n[Public oversight] Residents retain road-watch and shutdown oversight; refuges restore extra energy when safe.";
                    }
                    else if(LastDirectiveChoice==2)
                    {
                        zh+="\n\n【无条件庇护】任何人都能寻求紧急庇护；安全时避险棚额外疗伤。";
                        en+="\n\n[Unconditional shelter] Anyone may seek emergency refuge; shelters provide extra healing when safe.";
                    }
                    else
                    {
                        zh+="\n\n最后指令的公共权限尚未签署。";
                        en+="\n\nThe public authority for the Last Directive has not yet been signed.";
                    }
                    zh+=Has("afterlight_complete")?
                        "\n\n三处余光设施都有居民亲自署名的维护者。":
                        "\n\n余光重建的维护责任仍待居民亲自署名；一座接通的机器不能替代长期照料。";
                    en+=Has("afterlight_complete")?
                        "\n\nAll three Afterlight facilities have named local stewards.":
                        "\n\nAfterlight still needs residents to sign for maintenance; a connected machine is not a substitute for long-term care.";
                    if(Has("civic_aftercare_published"))
                    {
                        zh+="\n\n【巡路复核已公开】引水渠、赤铁矿场与雾港的现场路标都留下了记录，公共电网不再只靠一份旧地图证明安全。";
                        en+="\n\n[Road audit published] Field markers at Canal, Ironreach and Mistport are recorded; the public grid no longer relies on an old map alone to claim its routes are safe.";
                    }
                    else if(Has("civic_aftercare_started"))
                    {
                        zh+="\n\n三地巡路复核进度 "+CivicAftercareWaypointCount+"/3。完成后回炉火旅店找夜行旅人公布记录。";
                        en+="\n\nThree-region road audit progress: "+CivicAftercareWaypointCount+"/3. Return to the night traveler at Hearth Inn to publish the record.";
                    }
                    else
                    {
                        zh+="\n\n可选后续行动：与炉火旅店的夜行旅人交谈，接取三地巡路复核，确认重连后的安全路线仍然可用。";
                        en+="\n\nOptional follow-up: speak with the night traveler at Hearth Inn to audit three old route markers and verify the restored network's safe paths.";
                    }
                    zh+="\n\n道路终于不只是把人送到堡垒。它必须让每个人都能回来，也让每个人有权问清楚：谁守着这盏灯？";
                    en+="\n\nA road should do more than bring people to the citadel. It must help everyone get home—and let everyone ask who keeps the light burning.";
                    break;
                case "lastdirective":
                    headingZh="第十章：最后指令";headingEn="Chapter Ten: The Last Directive";
                    zh="三地公共应急名册揭开了九年前最后一条无署名的军用指令。矿工首先要求停止过载抽取，摆渡人在指令下达后才发现失联者，巡路队最后为归乡者点燃炉火。\n\n";
                    en="Three public rescue reports exposed the last unsigned nine-year-old military order. Miners first demanded an overload shutdown, ferrymen then found erased names, and the roadwardens lit a final civilian hearth.\n\n";
                    string[] reportZh={"矿工停机证词","船工失联名单","巡路队避难炉火"};
                    string[] reportEn={"Miners' shutdown petition","Ferrymen's missing names","Roadwardens' refuge hearth"};
                    for(int di=0;di<3;di++)
                    {
                        bool witnessed=Has("last_directive_clue_"+di);
                        zh+=reportZh[di]+"："+(witnessed?"已亲眼验证原始记录。":"尚未到访，编年册不会替你捏造。")+"\n";
                        en+=reportEn[di]+": "+(witnessed?"Original field record verified.":"Not yet witnessed; no account is invented.")+"\n";
                    }
                    if(LastDirectiveChoice==1)
                    {
                        zh+="\n新协议让居民共同监督旧机器，并有权质疑保密停机命令。";
                        en+="\nThe new charter grants public oversight and civilian shutdown appeals.";
                    }
                    else if(LastDirectiveChoice==2)
                    {
                        zh+="\n新协议把无条件民用紧急庇护写进最终权限。";
                        en+="\nThe new charter guarantees unconditional public emergency shelter.";
                    }
                    else
                    {
                        zh+="\n保密终端的两道真实守卫还没有被最终清除。";
                        en+="\nThe secret terminal's actual guardian defenses have not been completely overcome.";
                    }
                    if(LastDirectiveComplete)
                    {
                        zh+="\n\n【指令公开】余烬镇所有居民终于能阅读完整条款。";
                        en+="\n\n[Directive public] Emberfall's residents can now read the whole order.";
                    }
                    break;
                case "supply_inquest":
                    headingZh="第九章：送达之后，谁来守路";
                    headingEn="Chapter Nine: Who Keeps the Routes Open";
                    zh="第一份居民救援包真的穿过了修好的桥面，而不是凭空出现在账本里。然而货物抵达只是开始。炉火旅店的客人提出了一个所有城墙都回避的问题：受伤的人明天由谁照料？天黑后的道路又由谁巡视？\n\n"+
                       "居民审计记录了三处地方的实地选择。药草和木材都要真实投入，公会不能代替仍在路上的人投票。\n\n";
                    en="The first civilian parcel crossed a real rebuilt bridge instead of appearing only in a ledger. But a delivery is a beginning, not an enduring service. Hearth Inn's guests ask whom the walls have ignored: who cares for the injured tomorrow, and who patrols the road after dark?\n\n"+
                       "Three communities record their independent decisions. Medicines and timber are real contributions, not abstract numbers.\n\n";
                    int[] inquestRooms={0,3,5};
                    string[] inquestPlacesZh={"引水渠","赤铁矿场","雾港"};
                    string[] inquestPlacesEn={"Canal","Ironreach","Mistport"};
                    for(int qi=0;qi<inquestRooms.Length;qi++)
                    {
                        int site=inquestRooms[qi];
                        bool clinic=CivicSupplyClinic(site),watch=CivicSupplyWatch(site);
                        zh+=inquestPlacesZh[qi]+"："+
                            (clinic?"居民投票成立医疗点，安全时在避险棚为旅人持续疗伤。":
                             watch?"居民投票成立守路岗，安全时在避险棚为旅人持续恢复耐力。":
                             "居民尚未签署长期服务的选择；尚不能替他们预先宣布结果。")+"\n";
                        en+=inquestPlacesEn[qi]+": "+
                            (clinic?"The residents funded a clinic. Its real refuge restores health when safe.":
                             watch?"The residents funded a lookout. Its real refuge restores energy when safe.":
                             "No local vote is signed yet. No distant decree can pre-empt it.")+"\n";
                    }
                    zh+="\n\n【实地服务】"+CivicResponseDoneCount+
                        "/3 处已经通过真正的医疗护送或现场守路。";
                    en+="\n\n[Field services] "+CivicResponseDoneCount+
                        "/3 services have earned certification through real medicine walks or defensive battles.";
                    for(int ri=0;ri<inquestRooms.Length;ri++)
                        if(CivicResponseDone(inquestRooms[ri]))
                        {
                            zh+="\n"+inquestPlacesZh[ri]+"：实地任务完成，公共疗养或守路服务得到增强。";
                            en+="\n"+inquestPlacesEn[ri]+
                                ": physical service verified; the refuge performs its local function more effectively.";
                        }
                    if(Has("civic_response_ledger"))
                    {
                        zh+="\n\n【应急名册公开】八地已建公共避险棚获得共同分享的安全基础疗养服务。";
                        en+="\n\n[Response ledger published] Every rebuilt regional refuge now shares basic safe recuperation.";
                    }
                    zh+=CivicSupplyComplete?
                        "\n【公开审计已签发】三地物资投入与永久服务已逐项归档。旅人可以实际使用这些避险棚。":
                        "\n【审计仍在进行】三地全部签署后，仍须回旅店亲手公布公共账目。";
                    en+=CivicSupplyComplete?
                        "\n[Public audit published] All three material contributions and enduring refuge services are recorded and operational.":
                        "\n[Audit in progress] Return to the Inn after all three regions sign; publication is not automatic.";
                    break;
                case "afterlight":
                    headingZh="第五章：把余光留给活着的人";
                    headingEn="Chapter Five: Afterlight Belongs to the Living";
                    zh="星核的最终去向已由你决定。但决定只是开始：真正的道路需要接通、维护、记录用途。莉雅与三地居民开始重新校准那些被军用旧命令遗忘的星根锚点。\n\n"+
                        (Has("afterlight_started")?
                        "你在公会接受了居民共治的三地重建计划：在原锚点选择用途，实际把稳定负载带往远端接收台，使用真实材料完成每次修复。\n\n":
                        "战后公共工程尚未正式开工。\n\n");
                    en="You decided where the starcores belong, but the decision did not reconnect a single broken road. Lyra and the people of three regions are rebuilding the forgotten public infrastructure.\n\n"+
                        (Has("afterlight_started")?
                        "You accepted the public reconstruction project. Each region requires an explicit civic choice, a real timed transfer from its anchor to a distant receiver, and genuine repair materials.\n\n":
                        "The public reconstruction works have not started.\n\n");
                    for(int ri=1;ri<=3;ri++)
                    {
                        if(!Has("afterlight_done_"+ri))
                        {
                            zh+=new[]{"","荆棘密林：疗养根系与巡路灯仍未接通。","沉没遗迹：公共渡口与救援河岸仍未接通。","赤铁矿场：安全工房与停机保护仍未接通。"}[ri]+"\n";
                            en+=new[]{"","Thornwood: root care and road lamps are not reconnected.","Ruins: public passage and river shelter are not reconnected.","Ironreach: safe workbench and cooling safeguards are not reconnected."}[ri]+"\n";
                        }
                        else
                        {
                            zh+=AfterlightLocation(ri,false)+"："+
                                AfterlightOutcomeZh(ri)+"\n";
                            en+=AfterlightLocation(ri,true)+": "+
                                AfterlightOutcomeEn(ri)+"\n";
                            zh+=Has("afterlight_steward_"+ri)?
                                new[]{"","莫芽已亲笔签下根系与林灯维护责任；余烬镇公共星灯已开放。\n","阿菱已亲笔签下渡口及旅人名单维护责任；余烬镇公共星灯已开放。\n","维恩已亲笔签下矿工安全停机与维修责任；余烬镇公共星灯已开放。\n"}[ri]:
                                "公共设施虽已接通，维护者仍未亲自署名。\n";
                            en+=Has("afterlight_steward_"+ri)?
                                new[]{"","Moya signed for roots and road lamps; Emberfall public starroad access is now open.\n","Arin signed for the crossing and travelers; Emberfall public starroad access is now open.\n","Venn signed for safe shutdown and repairs; Emberfall public starroad access is now open.\n"}[ri]:
                                "The installed facility still awaits the living steward's signature.\n";
                        }
                    }
                    zh+=Has("afterlight_complete")?
                        "\n【重建公约已发布】三处设施拥有亲自署名的维护者。旧军用地图终于被一份活着的人可以使用的地图取代。":
                        "\n【公约尚未发布】哪怕三处站点已经接通，也要返回公会公开维护责任。";
                    en+=Has("afterlight_complete")?
                        "\n[Charter published] Three useful public services now have named local stewards. Old military maps have given way to routes ordinary people can actually use.":
                        "\n[Charter pending] Once all three sites work, return to the guild to publish responsibility for their upkeep.";
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
