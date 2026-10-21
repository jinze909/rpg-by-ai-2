using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void ChooseStoryChoice(GameCore g, string action)
        {
            Check(g.conversation != null && g.mode == "dialog",
                "Story archive choices require a real in-game conversation");
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing unlocked story choice: " + action);
        }
        private static bool StoryChoicePresent(GameCore g, string action)
        {
            if (g.conversation == null) return false;
            foreach (Choice c in g.conversation.choices)
                if (c.action == action) return true;
            return false;
        }
        private static int StoryChoiceIndex(GameCore g, string action)
        {
            if (g.conversation == null) return -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) return i;
            return -1;
        }

        public static void ChronicleCampaignNavigationMatchesDirectoryOrder(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0); g.Close();
            g.save.flags.Add("witness_started_1");
            g.save.flags.Add("core_1");
            WorldObject book = Object(g, "worldchronicle");
            g.Talk(book); ChooseStoryChoice(g, "story_section_campaign");
            int testimonies = StoryChoiceIndex(g, "story_read_testimonies");
            int forest = StoryChoiceIndex(g, "story_read_forest");
            Check(testimonies >= 0 && forest > testimonies,
                "Campaign page navigation must preserve the testimony-before-Thornwood order shown in the directory");
            ChooseStoryChoice(g, "story_read_testimonies");
            Check(StoryChoicePresent(g, "story_read_forest") &&
                !StoryChoicePresent(g, "story_read_ruins"),
                "The unlocked testimony page advances to the next displayed campaign chapter without revealing Drowned Ruins");
            ChooseStoryChoice(g, "story_read_forest");
            Check(StoryChoicePresent(g, "story_read_testimonies") &&
                !StoryChoicePresent(g, "story_read_ruins"),
                "Thornwood returns to the testimony page rather than reversing the directory's chronology");
        }

        public static void ChronicleRestoresChapterEightBetweenChaptersSevenAndNine(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0); g.Close();
            WorldObject book = Object(g, "worldchronicle");
            g.Talk(book); ChooseStoryChoice(g, "story_section_after");
            Check(!StoryChoicePresent(g, "story_read_roadcharter"),
                "Chapter Eight remains hidden until its existing quest is started or completed");
            g.Close();
            g.save.flags.Add("hearing_started");
            g.save.flags.Add("roadcharter_started");
            g.save.flags.Add("roadcharter_report_1");
            g.save.flags.Add("roadcharter_report_3");
            g.save.flags.Add("civic_supply_started");
            int coins = g.save.coins, shards = g.save.shards;
            int skillPoints = g.save.skillPoints, flags = g.save.flags.Count;

            g.Talk(book); ChooseStoryChoice(g, "story_section_after");
            int hearing = StoryChoiceIndex(g, "story_read_hearing");
            int chapterEight = StoryChoiceIndex(g, "story_read_roadcharter");
            int chapterNine = StoryChoiceIndex(g, "story_read_supply_inquest");
            Check(hearing >= 0 && chapterEight > hearing && chapterNine > chapterEight,
                "The directory inserts the implemented Chapter Eight between the Seventh and Ninth chapters");
            ChooseStoryChoice(g, "story_read_roadcharter");
            Check(g.conversation.speaker.Contains("第八章") &&
                g.conversation.en.Contains("Chapter Eight") &&
                g.conversation.text.Contains("2/3") &&
                g.conversation.textEn.Contains("2/3"),
                "Chapter Eight's bilingual chronicle reports the real two-of-three field evidence state");
            Check(StoryChoicePresent(g, "story_read_hearing") &&
                StoryChoicePresent(g, "story_read_supply_inquest"),
                "Chapter Eight links backward to the hearing and forward to the Ninth Chapter");
            ChooseStoryChoice(g, "story_read_supply_inquest");
            Check(StoryChoicePresent(g, "story_read_roadcharter"),
                "The Ninth Chapter links back to the missing Chapter Eight");
            ChooseStoryChoice(g, "story_read_hearing");
            Check(StoryChoicePresent(g, "story_read_roadcharter"),
                "The Seventh Chapter links forward to the restored Chapter Eight");
            Check(g.save.coins == coins && g.save.shards == shards &&
                g.save.skillPoints == skillPoints && g.save.flags.Count == flags,
                "Reading the restored chapter never grants rewards or mutates existing v2 save progress");
        }

        public static void ChronicleEndingFollowsPostGridAccounts(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0); g.Close();
            g.save.frontierEnding = 3;
            g.save.ending = 3;
            g.save.flags.Add("civic_conduit_complete");
            WorldObject book = Object(g, "worldchronicle");
            int coins = g.save.coins, shards = g.save.shards, flags = g.save.flags.Count;

            g.Talk(book);
            ChooseStoryChoice(g, "story_section_after");
            Check(StoryChoicePresent(g, "story_read_civic_aftermath") &&
                StoryChoicePresent(g, "story_read_ending"),
                "A completed public grid exposes both the Chapter Twelve account and the chosen ending");
            ChooseStoryChoice(g, "story_read_civic_aftermath");
            Check(g.conversation.speaker.Contains("第十二章") &&
                g.conversation.en.Contains("Chapter Twelve") &&
                StoryChoicePresent(g, "story_read_ending"),
                "Chapter Twelve now links forward to the retrospective ending instead of becoming a dead end");
            ChooseStoryChoice(g, "story_read_ending");
            Check(g.conversation.speaker.Contains("你决定的边境") &&
                g.conversation.en.Contains("The Frontier You Chose") &&
                StoryChoicePresent(g, "story_read_civic_aftermath"),
                "The ending links backward to Chapter Twelve in the same chronological order");
            Check(g.save.coins == coins && g.save.shards == shards &&
                g.save.flags.Count == flags,
                "Correcting chronicle navigation does not mutate save progress or grant rewards");
        }

        public static void ChronicleChapterNavigationNeverLeaksFutureEvents(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0); g.Close();
            WorldObject book = Object(g, "worldchronicle");
            g.Talk(book); ChooseStoryChoice(g, "story_section_origins");
            ChooseStoryChoice(g, "story_read_origin");
            Check(StoryChoicePresent(g, "story_read_roots") &&
                !StoryChoicePresent(g, "story_read_wardens") &&
                StoryChoicePresent(g, "story_section_origins"),
                "A new player can read the next unlocked origin and return to the current chapter");
            ChooseStoryChoice(g, "story_read_roots");
            Check(StoryChoicePresent(g, "story_read_origin") &&
                StoryChoicePresent(g, "story_read_orders") &&
                !StoryChoicePresent(g, "story_read_wardens"),
                "Previous and next navigation skip all undiscovered pages");
            ChooseStoryChoice(g, "story_read_orders");
            Check(!StoryChoicePresent(g, "story_read_wardens"),
                "Last discovered page has no next-page spoiler");
            g.Close(); g.save.flags.Add("scout_saved");
            g.Talk(book); ChooseStoryChoice(g, "story_section_origins");
            ChooseStoryChoice(g, "story_read_orders");
            Check(StoryChoicePresent(g, "story_read_wardens"),
                "Rescuing the scout enables a genuine next-page action");
            ChooseStoryChoice(g, "story_read_wardens");
            Check(g.conversation.text.Contains("巡路") &&
                StoryChoicePresent(g, "story_read_orders"),
                "Next-page action opens the newly unlocked patrol account");
            ChooseStoryChoice(g, "story_section_origins");
            Check(StoryChoicePresent(g, "story_read_wardens"),
                "Back to this chapter retains the right section, not the global index");
            g.Close(); g.save.flags.Add("harbor_report");
            g.save.flags.Add("mountain_report");
            g.Talk(book); ChooseStoryChoice(g, "story_section_farshore");
            ChooseStoryChoice(g, "story_read_harbor");
            Check(StoryChoicePresent(g, "story_read_mountain") &&
                !StoryChoicePresent(g, "story_read_desert"),
                "Navigation skips undiscovered middle chapters rather than exposing them");
            ChooseStoryChoice(g, "story_read_mountain");
            Check(StoryChoicePresent(g, "story_read_harbor") &&
                !StoryChoicePresent(g, "story_read_covenant"),
                "Farshore evidence navigation respects individual report gates");
            int coins = g.save.coins, shards = g.save.shards, flags = g.save.flags.Count;
            g.Close(); g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            resumed.Talk(Object(resumed, "worldchronicle"));
            ChooseStoryChoice(resumed, "story_section_farshore");
            ChooseStoryChoice(resumed, "story_read_harbor");
            Check(StoryChoicePresent(resumed, "story_read_mountain") &&
                resumed.save.coins == coins && resumed.save.shards == shards &&
                resumed.save.flags.Count == flags,
                "Chapter navigation survives saving without currency or progress mutation");
        }

        public static void ChronicleRetellsRegionalChoicesAndSurvivesReload(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0); g.Close();
            WorldObject archive = Object(g, "worldchronicle");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            g.save.flags.Add("mountain_report");
            string[] harborZh = { "", "守灯优先", "救援优先", "协同方案" };
            string[] harborEn = { "", "Lights first", "Rescue first", "Shared priority" };
            string[] desertZh = { "", "绿洲优先", "航路优先" };
            string[] desertEn = { "", "Oasis first", "Routes first" };
            string[] mountainZh = { "", "证据封存", "公开名单" };
            string[] mountainEn = { "", "Evidence preserved", "Names made public" };
            string[] councilZh = { "", "共同水渠", "轮值灯网", "开放星路" };
            string[] councilEn = { "", "Shared canals", "Rotating lightwatch", "Open starroads" };
            int coins = g.save.coins, shards = g.save.shards, talent = g.save.skillPoints;
            int flags = g.save.flags.Count;
            for (int variant = 1; variant <= 3; variant++)
            {
                int regional = Math.Min(2, variant);
                g.save.harborPledge = variant;
                g.save.desertChoice = regional;
                g.save.mountainChoice = regional;
                g.save.frontierEnding = variant;
                g.Talk(archive);
                ChooseStoryChoice(g, "story_section_farshore");
                ChooseStoryChoice(g, "story_read_harbor");
                Check(g.conversation.text.Contains(harborZh[variant]) &&
                    g.conversation.textEn.Contains(harborEn[variant]),
                    "The harbor archive follows each saved lighthouse/rescue priority in both languages");
                ChooseStoryChoice(g, "story_section_farshore");
                ChooseStoryChoice(g, "story_read_desert");
                Check(g.conversation.text.Contains(desertZh[regional]) &&
                    g.conversation.textEn.Contains(desertEn[regional]),
                    "The desert archive distinguishes actual oasis and navigation choices");
                ChooseStoryChoice(g, "story_section_farshore");
                ChooseStoryChoice(g, "story_read_mountain");
                Check(g.conversation.text.Contains(mountainZh[regional]) &&
                    g.conversation.textEn.Contains(mountainEn[regional]),
                    "The mountain archive remembers sealed versus public names");
                ChooseStoryChoice(g, "story_section_farshore");
                ChooseStoryChoice(g, "story_read_covenant");
                Check(g.conversation.text.Contains(councilZh[variant]) &&
                    g.conversation.textEn.Contains(councilEn[variant]),
                    "All three farshore governance outcomes receive distinct, bilingual aftermath");
                Check(g.save.coins == coins && g.save.shards == shards &&
                    g.save.skillPoints == talent && g.save.flags.Count == flags,
                    "Reading recorded choices never changes currency, flags or talents");
                g.Close();
            }
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.Talk(Object(restored, "worldchronicle"));
            ChooseStoryChoice(restored, "story_section_farshore");
            ChooseStoryChoice(restored, "story_read_covenant");
            Check(restored.conversation.text.Contains("开放星路") &&
                restored.conversation.textEn.Contains("Open starroads") &&
                restored.save.coins == coins && restored.save.flags.Count == flags,
                "An existing-schema save reload retains the precise covenant account");
        }

        public static void WorldChronicleRespectsChapterProgressAndRealInteractions(Campaign data)
        {
            var g = Fresh(data); g.NewGame(0);
            Check(g.conversation != null &&
                g.conversation.text.Contains("七天前") &&
                g.conversation.text.Contains("星根") &&
                g.conversation.textEn.Contains("Seven days") &&
                g.conversation.textEn.Contains("roadwarden"),
                "The opening establishes the seven-day disaster, root network and lost patrol in both languages");
            g.Close();
            WorldObject archive = Object(g, "worldchronicle");
            g.save.x = archive.x; g.save.y = archive.y;
            Check(g.CanStand(g.Position) && g.Nearest() == archive,
                "The town's new world archive is a reachable production interaction");
            int coins = g.save.coins, shards = g.save.shards;
            int originalFlags = g.save.flags.Count;
            g.Talk(archive);
            Check(StoryChoicePresent(g, "story_section_origins") &&
                StoryChoicePresent(g, "story_section_campaign") &&
                StoryChoicePresent(g, "story_section_farshore") &&
                StoryChoicePresent(g, "story_section_after"),
                "World archive offers four navigable story sections");
            ChooseStoryChoice(g, "story_section_origins");
            Check(StoryChoicePresent(g, "story_read_origin") &&
                StoryChoicePresent(g, "story_read_roots") &&
                StoryChoicePresent(g, "story_read_orders") &&
                !StoryChoicePresent(g, "story_read_wardens"),
                "Foundational lore is visible from the start, missing patrol secrets are not");
            ChooseStoryChoice(g, "story_read_roots");
            Check(g.conversation.text.Contains("七") && g.conversation.textEn.Contains("Starroot"),
                "The live story page explains the seven anchors bilingually");
            ChooseStoryChoice(g, "story_menu");
            ChooseStoryChoice(g, "story_section_campaign");
            Check(!StoryChoicePresent(g, "story_read_citadel") &&
                !StoryChoicePresent(g, "story_read_forest") &&
                !StoryChoicePresent(g, "story_read_mine"),
                "The new archive never spoils chapters the player has not reached");
            ChooseStoryChoice(g, "story_menu");
            ChooseStoryChoice(g, "story_section_after");
            Check(!StoryChoicePresent(g, "story_read_ending") &&
                !StoryChoicePresent(g, "story_read_abyss") &&
                !StoryChoicePresent(g, "story_read_atlas"),
                "Endings and Root Abyss content stay hidden on a new save");
            Check(g.save.coins == coins && g.save.shards == shards &&
                g.save.flags.Count == originalFlags,
                "Reading the chronicle grants no currencies, flags, or hidden story progress");
            g.Close();
            g.save.flags.Add("scout_saved"); g.save.flags.Add("core_1");
            g.Talk(archive); ChooseStoryChoice(g, "story_section_origins");
            Check(StoryChoicePresent(g, "story_read_wardens"),
                "Rescuing the scout reveals the once-hidden patrol account");
            ChooseStoryChoice(g, "story_menu");
            ChooseStoryChoice(g, "story_section_campaign");
            Check(StoryChoicePresent(g, "story_read_forest") &&
                !StoryChoicePresent(g, "story_read_ruins"),
                "Recovering only the first core reveals only the first chapter's truth");
            g.Close();
            g.save.flags.Add("core_2"); g.save.flags.Add("core_3"); g.save.flags.Add("boss_4");
            g.Talk(archive); ChooseStoryChoice(g, "story_section_campaign");
            Check(StoryChoicePresent(g, "story_read_forest") &&
                StoryChoicePresent(g, "story_read_ruins") &&
                StoryChoicePresent(g, "story_read_mine") &&
                StoryChoicePresent(g, "story_read_citadel"),
                "All four campaign accounts unlock according to actual earned milestones");
            ChooseStoryChoice(g, "story_read_citadel");
            Check(g.conversation.text.Contains("厄洛") &&
                g.conversation.textEn.Contains("Orrin"),
                "Orrin's readable account acknowledges his real motive in both languages");
            g.Close();
            g.save.flags.Add("harbor_report"); g.save.flags.Add("desert_report");
            g.save.flags.Add("mountain_report"); g.save.flags.Add("exp_intro");
            g.save.flags.Add("survey_complete"); g.save.flags.Add("archive_complete");
            g.save.stage = 3;
            g.save.frontierEnding = 3;
            g.Talk(archive); ChooseStoryChoice(g, "story_section_farshore");
            Check(StoryChoicePresent(g, "story_read_harbor") &&
                StoryChoicePresent(g, "story_read_desert") &&
                StoryChoicePresent(g, "story_read_mountain") &&
                StoryChoicePresent(g, "story_read_covenant"),
                "All farshore chapters become readable after their real reports and council resolution");
            g.Close();
            g.Talk(archive); ChooseStoryChoice(g, "story_section_after");
            Check(StoryChoicePresent(g, "story_read_signal") &&
                StoryChoicePresent(g, "story_read_atlas") &&
                StoryChoicePresent(g, "story_read_abyss") &&
                !StoryChoicePresent(g, "story_read_ending"),
                "Side-story and abyss records are visible, but the campaign ending remains gated");
            g.Close(); g.save.ending = 3;
            g.Talk(archive); ChooseStoryChoice(g, "story_section_after");
            Check(StoryChoicePresent(g, "story_read_ending"),
                "A real resolved starcore ending unlocks the retrospective final page");
            ChooseStoryChoice(g, "story_read_ending");
            Check(g.conversation.text.Contains("诺尔") &&
                g.conversation.textEn.Contains("Noll"),
                "The player's selected shared-frontier ending is reflected by the chronicle");
            g.Close();
            g.Enter(9);
            WorldObject innCopy = Object(g, "worldchronicle");
            g.save.x = innCopy.x; g.save.y = innCopy.y;
            Check(g.CanStand(g.Position) && g.Nearest() == innCopy,
                "The inn copy of the chronicle is a second genuine reachable map interaction");
            g.Talk(innCopy);
            Check(StoryChoicePresent(g, "story_section_origins"),
                "Both copies open the same progression-aware archive without another unlock");
            g.Close();
            int postCoins = g.save.coins;
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            WorldObject resumedArchive = Object(resumed, "worldchronicle");
            resumed.save.x = resumedArchive.x; resumed.save.y = resumedArchive.y;
            resumed.Talk(resumedArchive);
            ChooseStoryChoice(resumed, "story_section_after");
            Check(StoryChoicePresent(resumed, "story_read_ending") &&
                resumed.save.coins == postCoins,
                "Old-schema saves preserve narrative access without duplicating rewards");
        }

        public static void LivingWorldLedgerAndAtlasEchoShareOneStory(Campaign data)
        {
            var g = Fresh(data); g.save.level = 12;
            Check(!g.WorldResponse().Contains("星核归宿") &&
                !g.WorldResponse().Contains("远岸治理") &&
                !g.WorldResponse().Contains("测绘师遗产"),
                "The world-response panel must not spoil unresolved endings or quests");
            int coins = g.save.coins, skill = g.save.skillPoints;
            foreach (string flag in new[] { "scout_saved", "survey_complete",
                "road_trial_complete", "wayfarer_complete", "archive_complete",
                "archive_echo_complete", "fieldstation_complete",
                "fieldstation_survey_complete" }) g.save.flags.Add(flag);
            g.save.harborPledge = 3; g.save.desertChoice = 1;
            g.save.mountainChoice = 2; g.save.ending = 2; g.save.frontierEnding = 3;
            string zh = g.WorldResponse();
            Check(zh.Contains("归乡网络") && zh.Contains("旅人获救") &&
                zh.Contains("测绘师遗产") && zh.Contains("旧罗盘") &&
                zh.Contains("远岸观测") && zh.Contains("星核归宿：保留屏障") &&
                zh.Contains("远岸治理：开放星路"),
                "The actual world-response UI reconciles completed quests and two independent endings");
            g.save.english = true;
            string en = g.WorldResponse();
            Check(en.Contains("Homeward network") && en.Contains("travelers rescued") &&
                en.Contains("compass restored") && en.Contains("Farshore weather") &&
                en.Contains("Starcore outcome: shield retained") &&
                en.Contains("Farshore governance: open starroads"),
                "Every lived-world summary is available in English without changing state");
            Check(g.save.coins == coins && g.save.skillPoints == skill,
                "Reading the dynamic world-response panel never issues rewards");
            g.Close(); g.save.stage = 3; g.Enter(3); g.enemies.Clear();
            WorldObject compass = Object(g, "archiveecho");
            // Both pre-fight and cleared versions must explain that this
            // is an old machine protocol rather than the surveyor's ghost.
            g.save.flags.Remove("archive_echo_complete");
            g.Talk(compass);
            Check(g.conversation.text.Contains("自动防御协议") &&
                g.conversation.text.Contains("不是测绘师的幽灵") &&
                g.conversation.textEn.Contains("defense protocol") &&
                StoryChoicePresent(g, "archive_echo_start"),
                "Before a fight, the real calibrator explains the guardian's historical origin");
            g.Close();
            g.save.flags.Add("archive_echo_complete");
            g.Talk(compass);
            Check(g.conversation.text.Contains("守卫协议") &&
                g.conversation.textEn.Contains("defense protocol") &&
                !StoryChoicePresent(g, "archive_echo_start"),
                "After victory, the same calibrator stays retired with no repeatable first-clear fight");
            g.Close(); g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.save.english = false;
            Check(restored.WorldResponse().Contains("星核归宿：保留屏障") &&
                restored.WorldResponse().Contains("远岸治理：开放星路"),
                "The living-world consequences remain readable from old-schema save flags");
        }

        public static void IndependentEndingsAndCampaignLoreRemainConsistent(Campaign data)
        {
            Check(data.maps.Length >= 10 && data.bosses.Length >= 7 &&
                data.maps[0].objects.Length > 0,
                "Rewritten lore preserves the full original campaign and all bosses");
            string[] expected = { "余烬镇", "荆棘密林", "沉没遗迹", "赤铁矿场",
                "星陨堡垒", "雾港", "玻璃沙海", "霜钟高原", "地脉深渊", "炉火旅店" };
            for (int i = 0; i < expected.Length; i++)
                Check(data.maps[i].name == expected[i] &&
                    !string.IsNullOrEmpty(data.maps[i].subtitle) &&
                    !string.IsNullOrEmpty(data.maps[i].suben),
                    "All original regions retain identity and complete bilingual atmosphere");
            foreach (LoreData note in data.lore)
                Check(!string.IsNullOrEmpty(note.zh) && !string.IsNullOrEmpty(note.text),
                    "All original readable relic entries remain bilingual");
            var g = Fresh(data); g.save.level = 12;
            g.save.stage = 1;
            Check(g.GetQuest("main").description.Contains("水闸") &&
                g.GetQuest("main").room == 2,
                "Main quest guidance explains the ruins without changing the original chapter gate");
            g.save.stage = 3;
            Check(g.GetQuest("main").description.Contains("厄洛") &&
                g.GetQuest("main").room == 4,
                "Final chapter guidance leads to the correct citadel showdown");
            g.save.ending = 2; g.save.frontierEnding = 3;
            int coins = g.save.coins, points = g.save.skillPoints;
            g.Epilogue();
            Check(g.conversation.text.Contains("《高墙之内》") &&
                g.conversation.text.Contains("远岸") &&
                g.conversation.textEn.Contains("WITHIN THE WALLS") &&
                g.conversation.textEn.Contains("farshore"),
                "A kept-shield campaign ending must coexist with the independent open-farshore covenant");
            Check(g.save.ending == 2 && g.save.frontierEnding == 3 &&
                g.save.coins == coins && g.save.skillPoints == points,
                "Revisiting the epilogue does not modify chosen endings or repeat payouts");
            g.Close();
            g.save.flags.Add("exp_intro"); g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report"); g.save.flags.Add("mountain_report");
            g.Enter(5); g.enemies.Clear();
            WorldObject hall = Object(g, "council");
            g.Talk(hall);
            Check(g.conversation.text.Contains("《没有主人的明天》") &&
                g.conversation.text.Contains("堡垒屏障") &&
                g.conversation.textEn.Contains("A TOMORROW WITHOUT A MASTER"),
                "The farshore epilogue recognizes the independent citadel shield still being kept");
            g.Close();
            g.save.ending = 1; g.save.frontierEnding = 1;
            g.Epilogue();
            Check(g.conversation.text.Contains("《重新生长》") &&
                g.conversation.text.Contains("共同水渠"),
                "Returning the cores remains compatible with the water-first farshore covenant");
        }
    }
}
