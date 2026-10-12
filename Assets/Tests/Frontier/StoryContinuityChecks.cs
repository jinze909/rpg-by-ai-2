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
