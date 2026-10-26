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
        // One explanatory conversation can be reached from Lyra, a living
        // traveler or the chronicle. It only reads existing quest milestones:
        // no branching choice, completed flag, reward or v2 save schema
        // is ever reset when the player asks for the story again.
        private void AppendStoryBridgeChoice()
        {
            if (conversation == null || mode != "dialog") return;
            Choice[] choices = conversation.choices;
            if (choices == null) return;
            foreach (Choice existing in choices)
                if (existing.action == "story_catchup") return;
            int insert = choices.Length;
            if (insert > 0 && choices[insert - 1].action == "close")
                insert--;
            Choice[] updated = new Choice[choices.Length + 1];
            Array.Copy(choices, 0, updated, 0, insert);
            updated[insert] = StoryChoice("我们现在查到哪儿了？",
                "What have we found so far?", "story_catchup");
            Array.Copy(choices, insert, updated, insert + 1,
                choices.Length - insert);
            conversation.choices = updated;
        }
        // Resolve who actually owns this choice. Choose() closes the old
        // page before dispatch and temporarily carries its validated live
        // world object in pendingDialogueSource. Never silently switch an
        // inn traveler or the physical chronicle into Lyra's body/voice.
        private WorldObject StoryDialogueSource
        { get { return pendingDialogueSource??conversationSource; } }

        // An old narrative summary is not enough once the public works
        // advance. The same save-backed chapter chain drives both the
        // traveler's practical road advice and the physical record's
        // unfinished-work column. No guessed flags or new quests.
        private void CurrentPublicStoryWork(out string zh,out string en)
        {
            if(!Has("afterlight_complete"))
            {
                zh="公会南面的余光工地还在等人把三处公共站接上电";
                en="three public stations need power at the Afterlight works south of the guild";
            }
            else if(!Has("storm_complete"))
            {
                zh="星雨要来了，各地得自己定好优先供电的顺序";
                en="the approaching starstorm requires each region to agree on power priorities";
            }
            else if(!Has("hearing_complete"))
            {
                zh="听证会的证人还没到齐，失联者的名字要有人当面核对";
                en="the public hearing still needs its witnesses and the missing residents' names checked";
            }
            else if(!RoadCharterComplete)
            {
                zh="公用道路的约定还没落地，旧路标得有人按实际路况修";
                en="the public road charter still needs people to reopen and repair the routes";
            }
            else if(!CivicConduitComplete)
            {
                zh="公共电网的阀门还没校准好，避险棚不能靠一张图就通电";
                en="the public grid valves still need tuning before the shelters can rely on them";
            }
            else
            {
                zh="新开的道路还得有人巡，旅店的巡路记录等着下一位归来的人";
                en="the reopened roads still need patrols, and the inn roadwarden log is ready for new return entries";
            }
        }
        private void WorldInnStoryCatchUp(WorldObject traveler)
        {
            string zh="旅人把一张磨破边的路单压在汤碗下面。\n\n“公会里管档案，旅店里管回来的人。你问我走到哪了，我先看看谁还没回家。”";
            string en="The traveler pins a worn route slip beneath a soup bowl.\n\n“Guild keeps the ledgers. Here we count who made it home. Let me see which road you're asking about.”";
            if(save.stage==0)
            {
                if(Has("core_1"))
                {
                    zh+="\n\n“你从密林带回来的东西还没交？先去公会找莉雅。别把能救人的物件压在行囊底下。”";
                    en+="\n\n“You carried something out of Thornwood? Take it to Lyra at the guild. Don't leave it buried under your travel gear.”";
                }
                else
                {
                    zh+="\n\n“密林那位巡路员还没回来。今早有车夫在北岔路看见了他的旧路标。先找人，再问祭坛上出了什么事。”";
                    en+="\n\n“The scout hasn't come home from Thornwood. A cart driver spotted his old marker by the northern fork. Find him first; then ask what happened at the altar.”";
                }
            }
            else if(save.stage<3)
            {
                zh+=save.stage==1?
                    "\n\n“沉没遗迹的摆渡船又停了。带上路图去问渡口的人，别在涨水的时候抄近路。”":
                    "\n\n“赤铁矿场的钟还在响，工人却没按时换班。先去看看升降机是不是还通着。”";
                en+=save.stage==1?
                    "\n\n“The ferry at the drowned ruins has stopped again. Ask at the crossing, and don't shortcut through the rising water.”":
                    "\n\n“The Ironreach mine bell keeps ringing, but the workers missed their shift change. Check the lift before you go in.”";
            }
            else if(save.ending==0)
            {
                zh+=save.frontierEnding==0?
                    "\n\n“三枚星核都指向堡垒。城里要守住屏障，路上的人也要用电。先听完远岸各家的话，带着他们的证词来找厄洛。”":
                    "\n\n“远岸那份协定已经签了字。带上原本的供电账，去堡垒找厄洛谈谈，别让议定书只留在纸上。”";
                en+=save.frontierEnding==0?
                    "\n\n“All three cores point to the citadel. The wall needs power, but so do people along the roads. Hear the farshore accounts before speaking to Orrin.”":
                    "\n\n“The farshore agreement has signatures now. Take the original power ledger to Orrin at the citadel. The agreement can't stay on paper.”";
            }
            else
            {
                string workZh,workEn;
                CurrentPublicStoryWork(out workZh,out workEn);
                zh+="\n\n“修好一条路，就有人往更远的地方走。眼下，"+workZh+"。你要去的话，带上手帐，回程再来告诉我。”";
                en+="\n\n“Open one road, and someone sets out farther. Right now, "+workEn+". Take your journal and tell me what you find on your way back.”";
            }
            Dialog(traveler.name,traveler.en,zh,en,
                StoryChoice("路上还有谁的消息？","Any word from the road?","story_people"),
                StoryChoice("星根到底怎么影响道路？","How does starroot affect the roads?","story_terms"),
                StoryChoice("翻看巡路记录","Read the road log","story_menu"),
                StoryChoice("还是问问旅店的事","Back to inn business","inn_topics"),
                StoryChoice("收好路单","Put away the slip","close"));
        }
        private void WorldInnStoryPeople(WorldObject traveler)
        {
            string zh="旅人抽出一张皱巴巴的货单。\n\n“杏在镇口等药，老潮在渡口等木板，诺尔在矿场等能关停机器的零件。三样东西听着不搭边，可路断了，谁都等不来。”";
            string en="The traveler unfolds a creased freight slip.\n\n“An's waiting for medicine, Old Tide for ferry planks, and Noll for a part that will stop the mine machines. Different cargo. Same broken roads.”";
            if(Has("scout_saved"))
            {
                zh+="\n\n“你救回的巡路员昨天在这里住过一夜。他靴子上有铁镐留下的木屑。密林里的麻烦恐怕不全是野兽干的。”";
                en+="\n\n“The scout you brought back slept here last night. Pickaxe splinters were caught in his boots. Beasts weren't the only trouble in those woods.”";
            }
            if(save.stage>=3)
            {
                zh+="\n\n“堡垒也有人在等物资。可我把货单给谁，得先知道是谁负责送到最后一户。”";
                en+="\n\n“The citadel has people waiting for supplies too. I just want to know who's responsible for getting them to the last house.”";
            }
            Dialog(traveler.name,traveler.en,zh,en,
                StoryChoice("再看看路单","Back to the route slip","story_catchup"),
                StoryChoice("看看巡路记录","Read the road log","story_menu"),
                StoryChoice("去忙吧","That's enough for now","close"));
        }
        private void WorldInnStoryTerms(WorldObject traveler)
        {
            Dialog(traveler.name,traveler.en,
                "旅人用手指在桌上划了条线，又在中间压上一枚硬币。\n\n“地下那条供水、供热的线叫星根。星核像闸门，把力气送往不同的地方。堡垒的屏障用得多，路边的灯和泵就分得少。”\n\n他把硬币移向你。\n\n“星晶是矿石，不是闸门。诺尔能拿它修设备。至于哪些闸该开，你得问真正住在那儿的人。”",
                "The traveler draws a line on the table and sets a coin across it.\n\n“Starroot carries water, heat, and power underground. Starcores work like gates, sending it one way or another. Give more to the wall, and the road lamps and pumps get less.”\n\nThe coin scrapes toward you.\n\n“Starcrystal is ore, not a gate. Noll uses it for repairs. Ask the people living out there which gates ought to stay open.”",
                StoryChoice("再看看路单","Back to the route slip","story_catchup"),
                StoryChoice("翻看巡路记录","Read the road log","story_menu"),
                StoryChoice("收起这张图","Put the sketch away","close"));
        }

        // The chronicle is a PHYSICAL BOOK, not a remotely summoned
        // character. Its pages carry firsthand recovered evidence and
        // no made-up omniscient narrator or future milestone spoilers.
        private void WorldArchiveCatchUp()
        {
            string zh="巡路纪事 · 尚未归档的道路\n\n雨水把一张巡路表浸得发软。页脚写着：“道路是否可通，以归来的人为准。”";
            string en="Roadwarden notes · Routes still unaccounted for\n\nRain has softened an old route sheet. Along the bottom someone wrote: “A road is open only when someone comes back along it.”";
            if(save.stage==0)
            {
                zh+=Has("core_1")?
                    "\n\n荆棘密林的星核已由巡路者带出，公会收讫栏仍空着。下一处签收地点：莉雅的登记桌。":
                    "\n\n荆棘密林的侦察员尚未在归队栏签名。最后的路标指向旧祭坛；先确认他是否平安。";
                en+=Has("core_1")?
                    "\n\nThe Thornwood core has been recovered, but the guild receipt is unsigned. Next handoff: Lyra's desk.":
                    "\n\nThe Thornwood scout has not signed the return column. His last marker points toward the old altar. Confirm he's alive first.";
            }
            else if(save.stage<3)
            {
                zh+=save.stage==1?
                    "\n\n下一份空白栏：沉没遗迹渡口。潮汐记录与星核供能记录尚未核对。":
                    "\n\n下一份空白栏：赤铁矿场换班簿。升降机和矿场供能记录尚未核对。";
                en+=save.stage==1?
                    "\n\nNext blank column: the Drowned Ruins ferry. Tide readings and core power records remain unverified.":
                    "\n\nNext blank column: Ironreach shift register. Lift access and mine power records remain unverified.";
            }
            else if(save.ending==0)
            {
                zh+=save.frontierEnding==0?
                    "\n\n三处星核都已送往堡垒。远岸的居民证词与屏障用电账仍需对照，不能只在图上圈出一个结果。":
                    "\n\n远岸协定已签署。待办交接：带各地用电证词与原始账本前往堡垒，与厄洛当面对照。";
                en+=save.frontierEnding==0?
                    "\n\nAll three cores went to the citadel. The farshore witness accounts still need to be set beside the shield's power ledger.":
                    "\n\nFarshore agreement signed. Pending handoff: bring the regional power accounts and original ledger to Orrin at the citadel.";
            }
            else
            {
                string workZh,workEn;
                CurrentPublicStoryWork(out workZh,out workEn);
                zh+="\n\n旧星核案已经落款。今日未结栏："+workZh+"。";
                en+="\n\nThe original core decision is signed. Today's unfinished entry: "+workEn+".";
            }
            Dialog("边境编年册","Frontier Chronicle",zh,en,
                StoryChoice("翻阅旅人记录","Read the travelers' entries","story_people"),
                StoryChoice("核对星根术语","Check the starroot glossary","story_terms"),
                StoryChoice("返回编年册目录","Back to the chronicle","story_menu"),
                StoryChoice("合上记录","Close the record","close"));
        }
        private void WorldArchivePeople()
        {
            string zh="巡路纪事 · 来往登记\n\n镇口药箱：杏。渡口修船木板：老潮。矿场停机阀门：诺尔。三栏都留下了收货人的名字，未签收的单子仍夹在后面。";
            string en="Roadwarden notes · Names on the cargo slips\n\nMedicinal herbs at the town crossing: An. Ferry planks: Old Tide. Mine shutdown valve: Noll. All three have a named recipient; undelivered slips remain in the folder.";
            if(Has("scout_saved"))
            {
                zh+="\n\n新增返程登记：密林侦察员已归队，带回旧祭坛附近人为砍断的树根样本。";
                en+="\n\nNew return entry: the Thornwood scout came home carrying roots cut by a tool near the altar.";
            }
            if(save.stage>=3)
            {
                zh+="\n\n堡垒供能申请已附三处原始路单。用电者与停电者的姓名须分别记录。";
                en+="\n\nThree original route slips are attached to the citadel power request. Names of those supplied and those left without power must be recorded separately.";
            }
            Dialog("边境编年册","Frontier Chronicle",zh,en,
                StoryChoice("返回待办道路","Back to the open routes","story_catchup"),
                StoryChoice("核对星根术语","Check the glossary","story_terms"),
                StoryChoice("返回目录","Back to contents","story_menu"),
                StoryChoice("合上记录","Close the record","close"));
        }
        private void WorldArchiveTerms()
        {
            Dialog("边境编年册","Frontier Chronicle",
                "星根线路说明 · 摘自工匠与巡路队的勘查页\n\n星根：地下输送水、热与能量的线路，连接七处锚点。\n\n星核：各锚点的关键部件，负责调配线路的供能。拿走一处星核，其他地方可能因此失去供给。\n\n星晶：在线路附近生成的矿石，可用于修复设备，不是星核。\n\n屏障：堡垒的防护设施，确实保护城内居民，也占用了原本可供其他地区使用的能量。",
                "Starroot glossary · From the engineers' and roadwardens' survey pages\n\nStarroot: underground lines carrying water, warmth, and power through seven anchors.\n\nStarcore: a vital component at each anchor controlling its power. Removing one may deprive another region of supply.\n\nStarcrystal: mineral found beside these lines. Useful for repairing equipment, but not a starcore.\n\nShield: the citadel's defense. It protects residents inside, while consuming power also needed elsewhere.",
                StoryChoice("返回待办道路","Back to open routes","story_catchup"),
                StoryChoice("阅读旅人登记","Read the travelers' entries","story_people"),
                StoryChoice("返回目录","Back to contents","story_menu"),
                StoryChoice("合上记录","Close the record","close"));
        }

        private void WorldStoryCatchUp()
        {
            WorldObject storyteller=StoryDialogueSource;
            if(storyteller!=null&&storyteller.kind=="worldchronicle")
            { WorldArchiveCatchUp();return; }
            if(storyteller!=null&&storyteller.kind=="innGuest")
            { WorldInnStoryCatchUp(storyteller);return; }
            // Lyra answers as a working guild steward, not as a floating
            // "plot explained" overlay. The journal remains the player's
            // source of explicit objective markers.
            string zh = "莉雅推开桌上的空杯，露出一张满是针孔的旧地图。\n\n“那天夜里，我们先听见矿场的钟。钟敲了整整一夜，没人敢去关机器。接着桥断了，林子里的路也没了。”\n\n“堡垒说，城墙上的屏障一刻也不能灭。他们要星核，就有人得从地里把它挖出来。镇上已经有两户人等不到运药的车了。”";
            string en = "Lyra pushes aside an empty cup. The map beneath it is speckled with pinholes.\n\n“The mine bell rang all through that night. No one dared turn off the machines. By morning the bridge was gone, and so was the road through the trees.”\n\n“The citadel says its shield must never go dark. It wants starcores, and someone has to dig them out. Two families here are still waiting for medicine that never came.”";
            if (save.stage == 0)
            {
                if (Has("core_1"))
                {
                    zh += "\n\n她看见你包里的星核，伸手拉开了公会的登记簿。\n\n“荆棘密林这一份，我认得。交给莉雅——就是我。把东西放桌上吧，别再往林子里跑第二趟了。”";
                    en += "\n\nShe spots the core in your pack and opens the guild ledger.\n\n“That one's from Thornwood. Hand it to me here. No sense walking back into those thorns for something you've already carried home.”";
                }
                else
                {
                    zh += "\n\n她用铅笔点了点荆棘密林的入口。\n\n“巡路队的侦察员还没回来。他上回说，黑荆巨兽守着的地方，有人把树根挖断了。先把人找到。若还能靠近祭坛，就看看那枚星核究竟被人动过什么手脚。”";
                    en += "\n\nHer pencil stops at the entrance to Thornwood.\n\n“Our scout never came back. Last time he wrote, he said someone had torn through the roots near the Briar Beast's altar. Find him first. If you can reach the altar, see what's been done to its core.”";
                }
            }
            else if (save.stage < 3)
            {
                int next=save.stage+1;
                string[] regionsZh={"","荆棘密林","沉没遗迹","赤铁矿场"};
                string[] regionsEn={"","Thornwood","Drowned Ruins","Ironreach"};
                zh += "\n\n莉雅翻过"+save.stage+"张盖着蜡印的登记页。\n\n“每一枚送回来的星核，旁边都夹着一封求援信。我原先以为只是同一场灾祸。现在看，信里写的都是同一条供能线。”";
                en += "\n\nLyra turns past "+save.stage+" wax-stamped entries.\n\n“Every core that came back had a plea for help tucked beside it. I thought they were separate disasters. The letters all point to the same supply line.”";
                if(Has("core_"+next))
                {
                    zh+="\n\n“"+regionsZh[next]+"那枚已经在你身上了？交给莉雅——放到这份登记簿旁。还有人等着我们查下一段。”";
                    en+="\n\n“You've brought back the one from "+regionsEn[next]+"? Leave it with me beside the ledger. There's another part of this line we still need to follow.”";
                }
                else
                {
                    zh+="\n\n她在"+regionsZh[next]+"的位置压下一枚图钉。\n\n“那边的信还没到。先去听听当地人怎么说，再找星核。别只带回来一块会发光的石头。”";
                    en+="\n\nShe presses a pin into "+regionsEn[next]+".\n\n“No word from that road yet. Hear what the people there have to say before you go after its core. Don't come back with nothing but a shining stone.”";
                }
            }
            else if(save.ending==0)
            {
                zh+="\n\n莉雅把三页登记摊在一起，纸角都卷起来了。\n\n“三枚，全往堡垒去了。厄洛说那边也有人要活命，我信。但河边等船的人呢？矿工呢？我想让他当面看看这些名字。”";
                en+="\n\nLyra lays three curled ledger pages side by side.\n\n“All three went to the citadel. Orrin says people inside need protecting. I believe him. But what about the people waiting at the crossing? The miners? I want him to see these names himself.”";
                zh+=save.frontierEnding==0?
                    "\n\n“远岸的灯塔、沙海、高原，还差他们自己的说法。把证词带回来，我们再去堡垒。”":
                    "\n\n“远岸的议定书已经签了。走吧，去堡垒找厄洛。纸在这里签得再漂亮，城墙上的人不点头也没用。”";
                en+=save.frontierEnding==0?
                    "\n\n“We still need the lighthouse, desert and highland accounts. Bring their testimony back. Then we'll go to the citadel.”":
                    "\n\n“The farshore agreement is signed. Time to speak with Orrin at the citadel. Ink on our side of the wall won't turn his machinery off.”";
            }
            else
            {
                zh+="\n\n窗外有人在修路，一块石头落地，响得连公会都听见。\n\n“星核的事定了。可你听见外头了吧？那是今天的活，不是庆功。”";
                en+="\n\nA paving stone drops outside the window, loud enough to hear across the guild.\n\n“The cores have been decided. Hear that out there? That's today's work. No one's celebrating just yet.”";
                string nextZh,nextEn;
                if(!Has("afterlight_complete"))
                {nextZh="公会南面的余光工地还缺人。先让三地的灯和设备通电。";nextEn="The Afterlight site south of the guild needs hands. Start with power to the three public stations.";}
                else if(!Has("storm_complete"))
                {nextZh="第一场星雨就要检验这些新线路。各地得自己定下供能顺序。";nextEn="The first starstorm will test those new lines. The regions need to settle their supply priorities.";}
                else if(!Has("hearing_complete"))
                {nextZh="听证会还等着人到齐。那些失联者的名字不能一直留在名单背面。";nextEn="The public hearing is still waiting for witnesses. Those missing names can't stay on the back of a ledger.";}
                else if(!RoadCharterComplete)
                {nextZh="路图画好了，路还没走通。去把公用道路那份约定落实。";nextEn="The routes are mapped, not yet safe. Help put the road charter into practice.";}
                else if(!CivicConduitComplete)
                {nextZh="公用电网的阀门还得逐个校准，才能把电送到避险棚。";nextEn="The public grid's valves still need tuning before the shelters can rely on them.";}
                else
                {nextZh="去旅店看看巡路队的新记录吧。电网通了，总得有人继续巡查。";nextEn="Read the roadwardens' latest log at the inn. The grid runs now; someone still has to inspect it.";}
                zh+="\n\n“"+nextZh+"”";
                en+="\n\n“"+nextEn+"”";
            }
            Dialog("莉雅 · 公会接待员","Lyra · Guild Steward",zh,en,
                StoryChoice("那些人后来怎么样了？","What happened to the others?","story_people"),
                StoryChoice("这张图上的星根是什么？","What is this starroot on your map?","story_terms"),
                StoryChoice("我想看看以前的记录","Let me read the old records","story_menu"),
                StoryChoice("我知道该去哪了","I know where to go","close"));
        }
        private void WorldStoryPeople()
        {
            WorldObject storyteller=StoryDialogueSource;
            if(storyteller!=null&&storyteller.kind=="worldchronicle")
            { WorldArchivePeople();return; }
            if(storyteller!=null&&storyteller.kind=="innGuest")
            { WorldInnStoryPeople(storyteller);return; }
            // Only acquaintances actually introduced by this save are named.
            string zh="莉雅把几封不同笔迹的信排在桌边。\n\n“杏寄来的信总带着药草味。你看，右下角又画了个小勺子。她怕伤员不肯喝药。”\n\n“老潮没什么耐心写信。三张纸全在催船板和粮食。他大概又把自己的晚饭让给等渡的人了。”\n\n“诺尔的字倒像是拿尺子量过。他说矿机要装一只人人都够得着的停机闸——这话可不会讨堡垒工程师喜欢。”";
            string en="Lyra lays out letters in three different hands.\n\n“An's always smell of herbs. Look, she's drawn another little spoon at the bottom. She's worried her patients won't take their medicine.”\n\n“Old Tide doesn't waste ink. Three letters asking for planks and grain. I imagine he's given his own supper to someone waiting for the ferry again.”\n\n“Noll writes like he measures each line with a ruler. He wants a shutdown lever any worker can reach. The citadel engineers won't like that.”";
            if(Has("scout_saved"))
            {zh+="\n\n“那个侦察员回来时，靴子里还扎着树根。他说那不是兽踩的，是铁镐留下的。”";en+="\n\n“The scout came back with splintered roots in his boots. Said those marks weren't made by beasts. They were pickaxe cuts.”";}
            if(save.stage>=3)
            {zh+="\n\n“厄洛？他守城守了一辈子。我不知道他上次走出城墙是什么时候。”";en+="\n\n“Orrin? He's spent his whole life keeping the city safe. I wonder when he last stepped beyond its walls.”";}
            if(Has("witness_started_1")||Has("witness_complete_1"))
            {zh+="\n\n“莫芽说她能认出每一棵受过伤的树。可她数不清到底倒了多少棵。”";en+="\n\n“Moya says she knows every wounded tree by sight. She can't count how many have fallen.”";}
            if(Has("witness_started_2")||Has("witness_complete_2"))
            {zh+="\n\n“阿菱把渡口失踪者的名字一笔一笔誊下来。她不肯用‘损失人数’四个字。”";en+="\n\n“Arin copies each missing traveler's name by hand. She refuses to write 'losses' where a name should be.”";}
            if(Has("witness_started_3")||Has("witness_complete_3"))
            {zh+="\n\n“维恩的手被机油染黑了。他拿来一份停机记录，最后一栏写着：‘人还在里面。’”";en+="\n\n“Venn came in with oil-black hands. On his shutdown sheet, the last column reads: 'Workers still inside.'”";}
            Dialog("莉雅 · 未寄出的信","Lyra · Unsent Letters",zh,en,
                StoryChoice("再看看地图","Back to the map","story_catchup"),
                StoryChoice("翻开编年册","Open the chronicle","story_menu"),
                StoryChoice("把信收好","Put the letters away","close"));
        }
        private void WorldStoryTerms()
        {
            WorldObject storyteller=StoryDialogueSource;
            if(storyteller!=null&&storyteller.kind=="worldchronicle")
            { WorldArchiveTerms();return; }
            if(storyteller!=null&&storyteller.kind=="innGuest")
            { WorldInnStoryTerms(storyteller);return; }
            Dialog("莉雅 · 旧线路图","Lyra · Old Circuit Map",
                "莉雅用指甲压住地图上交错的红线。\n\n“这些叫星根。地底下的水、热气，还有我们用的能量，都顺着它走。七处锚点就像河上的闸，要一处一处调。”\n\n“闸里最要紧的那块，是星核。拔出来拿去供城墙，别处的水就会少。星晶不一样——那是旁边结出的矿石，诺尔拿它修东西。”\n\n她敲了敲地图上画着城墙的黑方框。\n\n“屏障真能挡危险。我见过它救人。所以我才要让堡垒把账拿出来：谁在用，谁没得用？”\n\n“要是有人愿意在证词上署名，或把维修责任写进公约，纸就不再只是堡垒的一张命令。”",
                "Lyra pins down the crossing red lines with a fingernail.\n\n“Starroot. Water, warmth, even the power we use runs through it below ground. Seven anchors keep it in check, like sluices along a river.”\n\n“Inside each anchor is a starcore. Pull it out to feed the wall, and another place runs dry. Starcrystal's different—that's the mineral growing around the lines. Noll uses it for repairs.”\n\nShe taps the black square marking the citadel.\n\n“The shield does stop danger. I've seen it save lives. That's why I want their ledgers opened. Who gets the power? Who goes without?”\n\n“A signed witness account, or a charter naming who's responsible for repairs—those are papers the citadel can't so easily throw away.”",
                StoryChoice("回到地图","Back to the map","story_catchup"),
                StoryChoice("翻阅原始记录","Read the original records","story_menu"),
                StoryChoice("我记下了","I'll remember that","close"));
        }
        private void WorldChronicleMenu()
        {
            Dialog("边境编年册", "Frontier Chronicle",
                "厚封皮被雨泡得卷了边。第一页夹着半张渡船票，背后是巡路队手写的字：\n\n“余烬镇至旧桥，能走。涨潮时绕北坡。桥西第三户还有人在等药。”\n\n往后的纸页有水渍、矿灰和不同人的签名。你带回的记录夹在其中；没有找到的页仍是空白。",
                "The leather cover has warped from rain. Half a ferry ticket is tucked inside the first page. On its back, a roadwarden wrote:\n\n“Emberfall to Old Bridge: passable. Take the northern slope at high tide. Third house west of the bridge is still waiting for medicine.”\n\nThe remaining pages are stained by water and mine dust, signed by different hands. Only the records you brought home are filled in.",
                // Keep the original four chapter entries at the same
                // keyboard/controller positions. Context guides are extra
                // options, never an accidental remapping of old buttons.
                StoryChoice("世界与年代", "Origins and timeline", "story_section_origins"),
                StoryChoice("星核四章", "Four starcore chapters", "story_section_campaign"),
                StoryChoice("远岸与证词", "Farshore and testimony", "story_section_farshore"),
                StoryChoice("归乡支线与尾声", "Homeward stories and aftermath", "story_section_after"),
                StoryChoice("公会的下一条线索", "The guild's next lead", "story_catchup"),
                StoryChoice("那些人的来信", "Letters from the others", "story_people"),
                StoryChoice("旧星根线路图", "The old starroot diagram", "story_terms"),
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
                descriptionZh = "最早几页有烧焦的边、渡河的泥痕，还有九年前就该被划掉的调度指令。";
                descriptionEn = "The earliest pages carry scorched edges, river mud and a dispatch order that should have been crossed out nine years ago.";
            }
            else if (section == "campaign")
            {
                titleZh = "星核四章"; titleEn = "The Four Starcores";
                descriptionZh = "这几页夹着侦察员的铅笔拓图、渡口湿透的印章和矿场工时表。未找到的纸页还空着。";
                descriptionEn = "Pencil rubbings from a missing scout, a drowned ferry seal and a shift ledger are bound together here. The missing pages are still blank.";
            }
            else if (section == "farshore")
            {
                titleZh = "远岸证词"; titleEn = "Letters from Afar";
                descriptionZh = "三份笔迹各不相同。雾港记潮，沙海记水，高原记谁没有回来。纸角还留着争论时擦破的孔。";
                descriptionEn = "Three different hands: tides at Mistport, water in the desert, names of the missing in the highlands. Eraser holes mark where the writers argued.";
            }
            else if (section == "after")
            {
                titleZh = "归乡与余波"; titleEn = "Homeward and After";
                descriptionZh = "后半册的纸换了好几批，几张路图仍沾着泥。最末页是守夜人的维修签名。";
                descriptionEn = "The later sheets use several kinds of paper. Mud clings to the road maps; maintenance signatures fill the final page.";
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
                    zh += "\n\n守灯会维修领料单：你将有限的零件先交给守灯会，重新点亮外海航道。摆渡盟仍要逐个寻找失联的人；船册不会把那些等待抹掉。";
                    en += "\n\nLamplighters' repair ledger: You gave the scarce parts to the Lamplighters to reopen the sea lanes. The Ferry Union still has people to find, and the log cannot erase those left waiting.";
                }
                else if (save.harborPledge == 2)
                {
                    zh += "\n\n摆渡盟回航登记：你先让摆渡盟接回被困旅客。海上的灯列还需要修复，而获救者也终于能讲述潮水改变的那一夜。";
                    en += "\n\nFerry Union return manifest: You helped the Ferry Union bring stranded passengers home before restoring the lights. The sea lanes still need repair, but survivors can now testify about the changing tide.";
                }
                else if (save.harborPledge == 3)
                {
                    zh += "\n\n港务联署备忘：你让灯塔维修和渡船救援共用一份紧张的物资表。两边都不能假装自己的安全是唯一需要守护的。";
                    en += "\n\nJoint harbor memorandum: You split scarce supplies between lighthouse repairs and ferry rescues. Neither side could call its own safety the only kind worth protecting.";
                }
            }
            else if (page == "desert")
            {
                if (save.desertChoice == 1)
                {
                    zh += "\n\n绿洲蓄水簿：你把蓄光池的补给转向活人居住的绿洲。山野议会得到一份公开的调度账，也必须负责让供水持续运转。";
                    en += "\n\nOasis cistern log: You redirected the reservoir toward living oasis communities. The Assembly gained a public allocation ledger and the responsibility to keep the water flowing.";
                }
                else if (save.desertChoice == 2)
                {
                    zh += "\n\n外海灯网值班记录：你保留机器对远岸灯网的供能，帮助船只辨认归途；但旧城主的指令不再能代替活人的监督。";
                    en += "\n\nOffshore lightwatch log: You retained power for the navigation lights so ships could find home. The dead ruler's old instruction no longer substitutes for oversight by the living.";
                }
            }
            else if (page == "mountain")
            {
                if (save.mountainChoice == 1)
                {
                    zh += "\n\n山野议会封存簿：你将原始名单交给议会保存，作为推动修正撤离制度的证据。名字没有公开，保管的人因此更有责任作出改变。";
                    en += "\n\nCouncil sealed register: You entrusted the original names to the council as evidence for evacuation reform. The names remain protected, and their keepers are responsible for acting on them.";
                }
                else if (save.mountainChoice == 2)
                {
                    zh += "\n\n高原路碑铭文：你让失踪者的名字刻在所有人都能读到的路碑上。钟声不再只掩盖事故，也提醒后人记住每个人。";
                    en += "\n\nHighland waystone inscription: You had the missing names set where every traveler can read them. The bells no longer only conceal a disaster; they ask people to remember each life.";
                }
            }
            else if (page == "covenant")
            {
                if (save.frontierEnding == 1)
                {
                    zh += "\n\n共同水渠的施工单：摆渡盟负责管道，旧灯缆成为输水材料，沙海得到新的种子。农田会多收获药草，但远海的灯仍需要节制供能。";
                    en += "\n\nShared canals work order: The Ferry Union manages pipes made partly from old lamp cables, while new seeds reach the desert. Farms gain extra herbs; distant lights still need careful power use.";
                }
                else if (save.frontierEnding == 2)
                {
                    zh += "\n\n灯网交班簿：三地轮流负责航路灯列，并公开每次交班的耗材与风险。灯没有永远不灭的主人，只有必须有人接起的下一班。";
                    en += "\n\nRotating lightwatch handover: Three regions share navigation shifts and publish their supplies and risks. No keeper owns the lights forever; each has to hand them on.";
                }
                else if (save.frontierEnding == 3)
                {
                    zh += "\n\n开放星路签名册：米拉、伊沃与森共同管理渡船、水渠和路碑。能源账向所有人开放，同伴配合与公共委托也因此得到实际改善。";
                    en += "\n\nOpen starroads signatures: Mira, Ivo and Sen jointly care for ferries, canals and the memorial road. Energy accounts are public, and companions and community contracts benefit from their cooperation.";
                }
            }
            else if (page == "ending")
            {
                // The chosen ending is still the player's ending. This
                // retrospective only records the civic work completed after
                // it; browsing never awards progress or changes SaveData.
                zh += "\n\n余烬镇工务交班簿（当日抄录）：\n";
                zh += CivicConduitComplete ?
                    "三处阀门已重新调谐。" : "三处阀门仍待现场调谐。";
                zh += "公共避险棚已建成 " + FarShelterCount +
                    "/8 处；居民长期服务已有 " + CivicSupplyResolvedCount + "/3 处签署。";
                zh += CivicSupplyComplete ?
                    "公开审计已发布。" : "公开审计仍待发布。";
                zh += "余光设施已有 " + AfterlightRestored + "/3 处恢复运行。";
                zh += Has("afterlight_complete") ?
                    "维护公约已公布。" : "维护公约仍待居民签署。";
                zh += "页角压着一支用短了的铅笔，留给明天的值班人。";

                en += "\n\nEmberfall works ledger (today\u0027s copy):\n";
                en += CivicConduitComplete ?
                    "All three valves have been retuned." :
                    "The three valves are still waiting for physical tuning.";
                en += " Public refuges: " + FarShelterCount +
                    "/8 built; long-term regional services: " +
                    CivicSupplyResolvedCount + "/3 signed.";
                en += CivicSupplyComplete ?
                    " The public audit has been published." :
                    " The public audit is still pending.";
                en += " Afterlight sites restored: " + AfterlightRestored + "/3.";
                en += Has("afterlight_complete") ?
                    " The maintenance charter has been published." :
                    " Residents still need to sign the maintenance charter.";
                en += " A worn pencil is tucked into the margin for tomorrow\u0027s watchkeeper.";
            }
        }
        private void WorldChroniclePage(string page)
        {
            string zh = "", en = "", headingZh = "", headingEn = "";
            switch (page)
            {
                case "origin":
                    headingZh="星陨后的第七天"; headingEn="Seven Days After Starfall";
                    zh="余烬镇公会值夜簿，第七日。\n\n第二更，矿场钟又响了。守夜人说矿机自星陨以来没停过。旧桥被冲断后，西岸的车夫只能把空车赶回来。密林入口的路牌缠满了荆棘，已经看不清字。\n\n巡路员未归。随行的最后一封信已由陌生旅人送抵公会。莉雅在信封背面记了一句：“人先找回来。其他的，回来再说。”";
                    en="Emberfall Guild, night ledger. Seventh day.\n\nThe mine bell sounded again during the second watch. The machinery has not stopped since Starfall. With Old Bridge down, the ferrymen sent their empty carts home. Thorns have wrapped around the sign at the forest gate until its lettering is gone.\n\nThe roadwarden is overdue. A stranger delivered his last letter to the guild. On the back of the envelope Lyra wrote: “Bring him home first. The rest can wait.”";
                    break;
                case "roots":
                    headingZh="星根与七锚"; headingEn="Starroot and the Seven Anchors";
                    zh="旧调谐手册，封页缺失。\n\n“七处锚点互为牵引。抽走其中一处星核，应先调低其余六处的负荷。否则水位、地温、根系生长将先后出现偏差。”\n\n页边用另一种墨水补写：“堡垒征收星核，未附调谐记录。”\n\n纸角夹着一粒星晶。它只是脉络周围析出的矿物，却被旧手册反复圈了红线：不可将矿物与锚点核心混记。";
                    en="An old calibration manual, cover missing.\n\n“The seven anchors pull against one another. Before removing a starcore, reduce the load on the six remaining stations. Otherwise water levels, ground heat and root growth will drift.”\n\nA different hand has written in the margin: “The citadel requisitioned the core. No calibration record attached.”\n\nA starcrystal is pressed into the corner of the page. The manual circles the same warning twice: mineral deposits are not to be entered in the ledger as anchor cores.";
                    break;
                case "orders":
                    headingZh="九年前的命令"; headingEn="An Order Nine Years Old";
                    zh="玻璃沙海蓄光机维护记录，第九年。\n\n“城主专用蓄水槽：水位正常。西南居民槽：无维护拨款。远行供给：暂停。”\n\n值班人的字越写越小。末尾有一行被重重划掉：“城主已经死了，为什么还在给他的空屋送水？”\n\n七日前星陨后，机器仍照旧分水。最后一次维护签名停在九年前。";
                    en="Glass Desert reservoir maintenance log, year nine.\n\n“Governor's private cistern: level normal. Southwest civilian cistern: no maintenance allocation. Traveler ration: suspended.”\n\nThe watchkeeper's handwriting grows smaller down the page. A line has been scratched out: “The governor is dead. Why are we still filling his empty house?”\n\nAfter Starfall, the valves kept the same schedule. The last maintenance signature is nine years old.";
                    break;
                case "wardens":
                    headingZh="巡路队最后的信"; headingEn="The Wardens' Last Letters";
                    zh="巡路队的旧地图背面有一条被刮掉的线，从旧桥通向密林北坡。墨痕下还留着三个字：担架可行。\n\n第二张纸写着：“公会准许标矿车路线，不准标村民撤离路。我照画了两份。一份交差，一份埋在第三座信标下。”\n\n信的末尾没有署名，只有一句：“如果回来的是别人，别只把货路交给他。”";
                    en="The back of an old roadwarden map shows a route scraped nearly clean, running from Old Bridge to the northern slope of Thornwood. Three words survive beneath the ink: Stretcher route passable.\n\nOn a second sheet: “The guild approved ore-cart roads, not village escape routes. I drew two maps. One for the office; one buried beneath the third beacon.”\n\nThe letter is unsigned. Its final line reads: “If someone else brings this home, don't hand them only the freight map.”";
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
                    headingZh="荆棘密林：斧痕"; headingEn="Thornwood: Marks in the Roots";
                    zh="侦察员的现场札记。\n\n“被砍倒的两株老树，树皮内侧有整齐的铁镐痕。越往祭坛走，兽群越不肯让路。黑荆巨兽一直守着根脉裂开的地方。”\n\n树根间还嵌着小块星晶。札记到这里戛然而止，末页浸透了雨水。"+
                        (Has("core_1")?"\n\n公会后来补了一笔：祭坛已调谐，第一枚星核交由公会保管。":"\n\n祭坛封锁记录尚未完结。");
                    en="Field notes from the missing scout.\n\n“The two fallen trees show clean pickaxe marks under the bark. The closer I get to the altar, the less the animals will let me pass. The Briar Beast stays by the torn roots.”\n\nSmall starcrystals are lodged between those roots. The notes stop here, the last sheet soaked through with rain."+
                        (Has("core_1")?"\n\nA later guild entry confirms the altar was tuned and the first core taken into custody.":"\n\nThe record of the sealed altar ends here.");
                    break;
                case "ruins":
                    headingZh="沉没遗迹：闸门钥匙"; headingEn="Drowned Ruins: The Sluice Key";
                    zh="渡口旧碑的拓片，日、月、潮三个符号都磨得很浅。\n\n旁边的守卫换班簿写着：“今夜不见公会印章者，一律不准过闸。”下一页纸被水泡烂，只剩下“儿童”和“桥西”等几个字。"+
                        (Has("core_2")?"\n\n新的登记补在拓片后：碑序已经复位，溺水守卫解除，第二枚星核取出。":"\n\n水闸内侧的记录尚无后页。");
                    en="A rubbing from the old ferry tablet. The symbols for Sun, Moon and Tide are worn almost flat.\n\nBeside it, a guard's shift log reads: “No guild seal, no passage tonight.” Water has eaten the following page. Only “children” and “west bank” remain legible."+
                        (Has("core_2")?"\n\nA later entry confirms the tablet sequence restored, the drowned sentinel removed and the second core recovered.":"\n\nNo later crossing record has been recovered.");
                    break;
                case "mine":
                    headingZh="赤铁矿场：末班工时"; headingEn="Ironreach: The Last Shift";
                    zh="矿场工时表，最末页。\n\n“升降机故障。冷却阀无权手动开启。配额照旧。”\n\n一个工人把妹妹的名字写在工时格里，又用指甲刮掉了。旁边留着一幅歪歪扭扭的小灯。"+
                        (Has("core_3")?"\n\n诺尔后来在表背面补了一行：升降机修复，铸铁监工停机，第三枚星核已送出矿场。":"\n\n升降井下仍缺最后一份停机记录。");
                    en="Ironreach shift ledger, final page.\n\n“Lift broken. Cooling valves may not be opened manually. Quota unchanged.”\n\nA worker wrote his sister's name in the hours column, then scraped it away with a fingernail. Beside it is a crooked drawing of a little lamp."+
                        (Has("core_3")?"\n\nNoll later added: lift repaired, Iron Overseer shut down, third core removed from the mine.":"\n\nThe last shutdown log from below the lift is still missing.");
                    break;
                case "citadel":
                    headingZh="堡垒：屏障日志"; headingEn="Citadel: Shield Watch";
                    zh="堡垒屏障的值守日志，两页使用了不同的印章。\n\n“外环冲击，三号居民区无伤亡。屏障负荷不得低于警戒线。”\n\n第二页是莉雅送来的外地记录：密林根脉受损、河水上涌、矿工无法停机。两张纸上写着同一个日期。\n\n厄洛在页边留下批注：“若再停屏障，城内的人由谁保护？”没有人替他回答。";
                    en="Two pages from the citadel shield watch, stamped by different offices.\n\n“Outer-ring impact. No losses in residential sector three. Shield output must not fall beneath the alert threshold.”\n\nThe second page came from Lyra: broken forest roots, rising river water, miners unable to stop the machines. Both reports bear the same date.\n\nIn the margin Orrin wrote, “If I shut the shield down again, who protects the people inside?” No reply is recorded.";
                    break;
                case "harbor":
                    headingZh="雾港：两盏灯"; headingEn="Mistport: Two Lanterns";
                    zh="米拉的灯塔记录，两栏用不同颜色的墨水写成。\n\n“白灯亮：远海货船报平安。青灯灭：北岸救生艇未归。”\n\n每次堡垒要求加压，水位栏就多一道横线。米拉弟弟的名字停在第三页——他带几个孩子去北岸后，再没人替他续写那一栏。\n\n灯座下还压着一张零件领用单。白灯与救生艇申请了同一批铜线。";
                    en="Mira's lighthouse log has two columns in different ink.\n\n“White lamp lit: offshore freighter reported safe. Teal lamp dark: northern rescue skiff overdue.”\n\nEach citadel power surge adds another line to the tide column. Mira's brother's name stops on page three. He took several children north; nobody signed for his return.\n\nA parts requisition is tucked beneath the lantern bracket. The white lamp and the rescue skiff both requested the same copper wire.";
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
                    headingZh="远岸议定书"; headingEn="The Farshore Agreement";
                    zh="议事桌上留着三份沾水的记录：雾港水位、沙海配水、高原巡路名单。写字的人互相改了好几次草稿。\n\n守灯人要求保留外海白灯；摆渡人问谁来救落水的孩子；高原的代表说，不修路，连修灯的铜线也送不到。\n\n最后一页列出三套可供表决的安排：共同水渠、轮值灯网、开放星路。签名栏原本留给堡垒的总管，如今划开成三栏。";
                    en="Three damp records lie on the council table: Mistport tides, desert water rations and highland road names. The writers have corrected one another's drafts several times.\n\nThe lamplighters insist on the offshore beacon. The ferrymen ask who will pull children out of the water. The highland delegates point out that broken roads will keep the repair wire from arriving at all.\n\nThe last sheet lists three arrangements for a vote: shared canals, rotating lightwatch, open starroads. What was once one signature line for the citadel steward has been divided into three.";
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
                    headingZh="你决定的边境";headingEn="The Frontier You Chose";
                    if(save.ending==1)
                    {
                        zh="北坡锚点维护簿。\n\n“土温连续回升三日。根系已见新芽。夜间仍需派人在镇口值守。”\n\n厄洛的名字第一次出现在城外巡查栏旁。墨迹还没干。";
                        en="Northern anchor maintenance log.\n\n“Soil temperature rising for three days. New root shoots visible. Town gate still needs a night watch.”\n\nOrrin's name appears beside an inspection beyond the wall for the first time. The ink has not dried.";
                    }
                    else if(save.ending==2)
                    {
                        zh="堡垒收容登记。\n\n“新开南门，今晨入城三十七人。外河水位又降半尺。粮仓申请追加两车面粉。”\n\n值班员在空白处写了一行：“明天门外还有多少人？”";
                        en="Citadel refuge register.\n\n“South gate opened. Thirty-seven people admitted this morning. The outer river dropped another half-foot. Granary requests two more wagons of flour.”\n\nIn the margin the clerk wrote, “How many will be waiting outside tomorrow?”";
                    }
                    else
                    {
                        zh="余烬镇公共工地领料单。\n\n“杏领走草药四捆，渡口急用。老潮借用木板六片修船。诺尔取水泵阀门两套，夜里归还。”\n\n最下面一行是厄洛写的：“拆下第一段旧城墙的砖，送往北侧避险棚。”";
                        en="Emberfall public works requisition.\n\n“An: four bundles of herbs for the ferry sick. Old Tide: six planks for boat repairs. Noll: two water-pump valves, return after the night shift.”\n\nOrrin added the last line: “Send bricks from the first dismantled wall section to the northern shelter.”";
                    }
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
            if (action == "story_catchup") { WorldStoryCatchUp(); return true; }
            if (action == "story_people") { WorldStoryPeople(); return true; }
            if (action == "story_terms") { WorldStoryTerms(); return true; }
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
