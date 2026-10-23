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

        public static void OpeningConversationsExplainTheSameMotivationInBothLanguages(Campaign data)
        {
            var g=new GameCore(data);
            g.NewGame(0);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("七天前") &&
                g.conversation.text.Contains("星核") &&
                g.conversation.text.Contains("侦察员") &&
                g.conversation.textEn.Contains("scout") &&
                g.conversation.textEn.Contains("starcore") &&
                g.conversation.textEn.Contains("citadel"),
                "Both openings explain the danger, the source of the conflict and the first actionable mission");
            g.Close();g.Enter(1);g.enemies.Clear();
            WorldObject scout=null;
            foreach(WorldObject o in g.Map.objects)
                if(o.kind=="scout"){scout=o;break;}
            Check(scout!=null,"Authored forest still contains its living lost scout");
            g.save.x=scout.x;g.save.y=scout.y;
            g.save.kills[1]=4;g.Talk(scout);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("堡垒") &&
                g.conversation.text.Contains("莉雅") &&
                g.conversation.textEn.Contains("Lyra") &&
                g.conversation.textEn.Contains("altar"),
                "Scout's actual encounter leads coherently to Lyra's evidence and the next starcore");
        }

        public static void StoryDialogueRejectsStaleSourceAcrossRoomsAndRemovedNpcs(Campaign data)
        {
            var g=Fresh(data);
            WorldObject guild=Object(g,"guild");
            g.save.x=guild.x;g.save.y=guild.y;g.Talk(guild);
            int recap=StoryChoiceIndex(g,"story_catchup");
            Check(recap>=0&&g.conversationSource==guild,
                "Live guild recap binds the original visible guild desk");
            // Simulate a serialized UI callback racing a scene/room swap.
            // A story-only choice previously bypassed the ordinary proximity
            // guard even when its owning NPC lived in another map.
            g.save.room=1;
            g.Choose(recap);
            Check(g.mode=="world"&&g.conversation==null&&
                g.toast.Contains("Return to the target")==false,
                "A foreign-room storyteller cannot resurrect a stale dialogue");
            g.save.room=0;g.save.x=guild.x;g.save.y=guild.y;
            g.Talk(guild);
            Check(StoryChoicePresent(g,"story_catchup"),
                "Player can reopen a fresh dialogue in the correct room");
            // The same room may remove or replace a temporary NPC. Requiring
            // a live source prevents old conversation buttons from acting
            // on a different character whose feet match exactly.
            WorldObject[] originals=g.Map.objects;
            try
            {
                var without=new System.Collections.Generic.List<WorldObject>(originals);
                Check(without.Remove(guild),"The original guild NPC is in the real object list");
                g.Map.objects=without.ToArray();
                ChooseStoryChoice(g,"story_catchup");
                Check(g.mode=="world"&&g.conversation==null,
                    "Detached NPC source closes rather than performing the old choice");
            }
            finally { g.Map.objects=originals; }
            g.Talk(guild);ChooseStoryChoice(g,"story_catchup");
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.text.Contains("星根"),
                "Fresh original NPC story choices remain playable after a rejected stale click");
        }
        public static void LivingStoryGuideConnectsPeopleCausesAndNextStepWithoutChangingProgress(Campaign data)
        {
            var g=Fresh(data);
            WorldObject guild=Object(g,"guild");
            g.save.x=guild.x;g.save.y=guild.y;
            g.Talk(guild);
            Check(StoryChoicePresent(g,"story_catchup"),
                "Lyra can explain the whole narrative without removing original quest choices");
            int flags=g.save.flags.Count,coins=g.save.coins;
            ChooseStoryChoice(g,"story_catchup");
            Check(g.conversation.text.Contains("荆棘密林") &&
                !g.conversation.text.Contains("【厄洛】") &&
                StoryChoicePresent(g,"story_people") &&
                StoryChoicePresent(g,"story_terms"),
                "New players get simple, spoiler-conscious explanations and their real next destination");
            ChooseStoryChoice(g,"story_people");
            Check(g.conversation.text.Contains("【杏】") &&
                !g.conversation.text.Contains("【厄洛】") &&
                !g.conversation.text.Contains("【莫芽】"),
                "People only appear in the guide after their original introduction");
            ChooseStoryChoice(g,"story_catchup");
            ChooseStoryChoice(g,"story_terms");
            Check(g.conversation.text.Contains("【星核】") &&
                g.conversation.text.Contains("【星晶】") &&
                g.conversation.text.Contains("【屏障】"),
                "World jargon is explained through in-world dialogue rather than a new lore flag");
            Check(g.save.flags.Count==flags&&g.save.coins==coins,
                "Story explanations do not alter rewards or quest progress");
            g.Close();
            g.save.flags.Add("core_1");
            g.Talk(guild);ChooseStoryChoice(g,"story_catchup");
            Check(g.conversation.text.Contains("交给莉雅"),
                "Recovered but undelivered starcore gives the correct practical handoff direction");
            g.Close();
            g.save.stage=3;g.save.frontierEnding=2;
            g.Talk(guild);ChooseStoryChoice(g,"story_catchup");
            Check(g.conversation.text.Contains("厄洛"),
                "Citadel tension is explained only when the campaign has reached it");
            g.Close();
            g.save.ending=1;
            g.Talk(guild);ChooseStoryChoice(g,"story_catchup");
            Check(g.conversation.text.Contains("余光重建"),
                "Post-ending explanation points to rebuilding, rather than resetting the ending");
            g.Close();
            g.save.flags.Add("afterlight_complete");
            g.save.flags.Add("storm_complete");
            g.save.flags.Add("hearing_complete");
            g.save.flags.Add("roadcharter_complete");
            g.save.flags.Add("civic_conduit_complete");
            g.Talk(guild);ChooseStoryChoice(g,"story_catchup");
            Check(g.conversation.text.Contains("巡路复核"),
                "Late chapter connections identify the actually remaining local work");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.save.ending==1&&
                loaded.Has("civic_conduit_complete"),
                "Restyled story conversations remain compatible with existing v2 progress");
        }
        public static void ChronicleMenusFollowOneCanonicalPageSequence(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3; g.save.frontierEnding = 3; g.save.ending = 3;
            foreach (string flag in new[] {
                "scout_saved", "witness_started_1", "core_1", "core_2", "core_3",
                "iron_temper_started", "triad_started", "blackout_started", "boss_4",
                "harbor_report", "tide_salvage_started", "desert_report", "glasswind_started",
                "mountain_report", "survey_complete", "archive_complete", "root_harmony_started",
                "commons_relay_started", "skywatch_started", "afterlight_started", "storm_started",
                "hearing_started", "roadcharter_started", "civic_supply_started",
                "last_directive_started", "civic_conduit_started", "civic_conduit_complete" })
                g.save.flags.Add(flag);
            WorldObject book = Object(g, "worldchronicle");
            string[][] sections = {
                new[] { "story_section_origins", "story_read_origin", "story_read_roots",
                    "story_read_orders", "story_read_wardens" },
                new[] { "story_section_campaign", "story_read_testimonies", "story_read_forest",
                    "story_read_ruins", "story_read_mine", "story_read_temper", "story_read_triad",
                    "story_read_blackout", "story_read_citadel" },
                new[] { "story_section_farshore", "story_read_harbor", "story_read_tidebound",
                    "story_read_desert", "story_read_glasswind", "story_read_mountain",
                    "story_read_covenant" },
                new[] { "story_section_after", "story_read_signal", "story_read_atlas",
                    "story_read_counterpoint", "story_read_commons", "story_read_skywatch",
                    "story_read_abyss", "story_read_afterlight", "story_read_storm",
                    "story_read_hearing", "story_read_roadcharter", "story_read_supply_inquest",
                    "story_read_lastdirective", "story_read_civicconduit",
                    "story_read_civic_aftermath", "story_read_ending" }
            };
            g.Talk(book);
            foreach (string[] section in sections)
            {
                ChooseStoryChoice(g, section[0]);
                int previous = -1;
                for (int i = 1; i < section.Length; i++)
                {
                    int index = StoryChoiceIndex(g, section[i]);
                    Check(index > previous,
                        "Every earned chronicle page appears in the canonical section order: " + section[i]);
                    previous = index;
                }
                ChooseStoryChoice(g, "story_menu");
            }
        }

        public static void ChapterEightRoadCharterUsesRealFieldEvidenceAndPersistentOutcome(Campaign data)
        {
            foreach (bool publicRoute in new[] { true, false })
            {
                var g = Fresh(data);
                g.save.stage = 3; g.save.ending = 2;
                WorldObject guild = Object(g, "guild");
                g.save.x = guild.x; g.save.y = guild.y;
                g.Talk(guild);
                Check(!StoryChoicePresent(g, "roadcharter_guild"),
                    "Chapter Eight cannot be offered before the hearing and Orrin's real responsibility");
                g.Close();
                foreach (string flag in new[] { "hearing_complete", "hearing_public",
                    "hearing_orrin_aid", "scout_saved", "gate_1", "gate_2", "gate_3" })
                    g.save.flags.Add(flag);
                Check(g.RoadCharterReady && !g.RoadCharterStarted,
                    "The existing post-hearing and Orrin flags unlock Chapter Eight without pre-completing it");
                Action(g, "guild", "roadcharter_guild");
                ChooseStoryChoice(g, "roadcharter_begin");
                Check(g.RoadCharterStarted && g.GetQuest("roadcharter").id == "roadcharter",
                    "The chapter starts through Lyra's actual guild dialogue and appears in the tracked quest journal");
                g.Close();

                for (int region = 1; region <= 3; region++)
                {
                    g.save.room = region;
                    WorldObject marker = null;
                    foreach (WorldObject candidate in g.Map.objects)
                        if (candidate.id == "waytrail" + region + "e") marker = candidate;
                    Check(marker != null &&
                        g.CanStand(new Point(marker.x, marker.y)),
                        "Chapter Eight's eastern field marker is present on standable authored terrain: " + region);
                    g.save.x = marker.x; g.save.y = marker.y;
                    g.Talk(marker);
                    Check(g.Has("roadcharter_report_" + region) &&
                        g.conversation != null &&
                        g.conversation.text.Contains("第八章的公开证据") &&
                        g.conversation.textEn.Contains("Chapter Eight field evidence"),
                        "Interacting with the real eastern marker files bilingual Chapter Eight evidence: " + region);
                    g.Close();
                }
                Check(g.RoadCharterReports == 3,
                    "Three actual region interactions—not a menu shortcut—complete the field evidence set");

                g.save.room = 0;
                guild = Object(g, "guild");
                g.save.x = guild.x; g.save.y = guild.y;
                int coinsBefore = g.save.coins, shardsBefore = g.save.shards;
                float speedBefore = g.Speed;
                g.Talk(guild); ChooseStoryChoice(g, "roadcharter_guild");
                string selected = publicRoute ? "roadcharter_public" : "roadcharter_reserve";
                string rejected = publicRoute ? "roadcharter_reserve" : "roadcharter_public";
                Check(StoryChoicePresent(g, selected) && StoryChoicePresent(g, rejected),
                    "The completed field survey presents both distinct civic outcomes");
                ChooseStoryChoice(g, selected);
                Check(g.RoadCharterComplete && g.Has(selected) && !g.Has(rejected) &&
                    g.save.coins == coinsBefore + 45 && g.save.shards == shardsBefore + 3,
                    "The selected civic outcome is saved once with its authored reward and without granting the rejected outcome");
                Check(Math.Abs(g.Speed - (speedBefore + (publicRoute ? .25f : 0f))) < .001f,
                    "Pedestrian-first choice applies the real permanent movement bonus and the reserve choice does not");
                g.Close();
                g.save.energy = 10;
                g.save.x = data.maps[0].spawnX; g.save.y = data.maps[0].spawnY;
                Advance(g, 1f);
                float regeneration = g.save.energy - 10;
                Check(Math.Abs(regeneration - (publicRoute ? 14f : 17f)) < .2f,
                    "Civilian reserve choice adds real stamina regeneration while the public-route choice preserves the baseline");

                int rewardedCoins = g.save.coins, rewardedShards = g.save.shards;
                g.save.x = guild.x; g.save.y = guild.y;
                g.Talk(guild); ChooseStoryChoice(g, "roadcharter_guild");
                Check(!StoryChoicePresent(g, "roadcharter_public") &&
                    !StoryChoicePresent(g, "roadcharter_reserve") &&
                    g.save.coins == rewardedCoins && g.save.shards == rewardedShards,
                    "Reopening a completed Chapter Eight cannot repeat the reward or reselect its permanent outcome");
                g.Close(); g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(resumed.save.version == 2 && resumed.RoadCharterComplete &&
                    resumed.Has(selected) &&
                    Math.Abs(resumed.Speed - (speedBefore + (publicRoute ? .25f : 0f))) < .001f,
                    "Both Chapter Eight outcomes survive a real SaveData v2 reload with their gameplay effect intact");
            }
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
            ChooseStoryChoice(g, "story_read_roadcharter");
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
            Check(g.conversation.textEn.Contains("[After the ending, public promises still need caretakers]") &&
                g.conversation.textEn.Contains("All three valves have been retuned.") &&
                g.conversation.textEn.Contains("The public audit is still pending."),
                "The ending summary distinguishes a completed grid from pending civic services");
            Check(g.conversation.textEn.Contains("Public refuges: " + g.FarShelterCount +
                "/8 built; long-term regional services: " + g.CivicSupplyResolvedCount + "/3 signed.") &&
                g.conversation.textEn.Contains("Afterlight sites restored: " + g.AfterlightRestored + "/3.") &&
                g.conversation.textEn.Contains("Residents still need to sign the maintenance charter."),
                "The ending summary uses the real refuge, service and maintenance counters");
            Check(g.save.coins == coins && g.save.shards == shards &&
                g.save.flags.Count == flags,
                "Correcting chronicle navigation does not mutate save progress or grant rewards");

            var pending = Fresh(data); pending.NewGame(0); pending.Close();
            pending.save.frontierEnding = 3;
            pending.save.ending = 3;
            WorldObject pendingBook = Object(pending, "worldchronicle");
            int pendingCoins = pending.save.coins, pendingShards = pending.save.shards;
            int pendingFlags = pending.save.flags.Count;
            pending.Talk(pendingBook);
            ChooseStoryChoice(pending, "story_section_after");
            ChooseStoryChoice(pending, "story_read_ending");
            Check(pending.conversation.textEn.Contains("The three valves are still waiting for physical tuning.") &&
                pending.conversation.textEn.Contains("The public audit is still pending.") &&
                pending.conversation.textEn.Contains("Residents still need to sign the maintenance charter."),
                "An ending with unfinished civic work explicitly leaves it pending");
            Check(pending.save.coins == pendingCoins &&
                pending.save.shards == pendingShards &&
                pending.save.flags.Count == pendingFlags,
                "Reading the ending never rewards or mutates a save with unfinished civic work");

            var completed = Fresh(data); completed.NewGame(0); completed.Close();
            completed.save.frontierEnding = 3;
            completed.save.ending = 3;
            completed.save.flags.Add("civic_conduit_complete");
            completed.save.flags.Add("civic_supply_complete");
            completed.save.flags.Add("civic_supply_clinic_0");
            completed.save.flags.Add("civic_supply_watch_3");
            completed.save.flags.Add("civic_supply_clinic_5");
            for (int room = 0; room < 8; room++)
                completed.save.flags.Add("far_shelter_" + room);
            for (int site = 1; site <= 3; site++)
                completed.save.flags.Add("afterlight_done_" + site);
            completed.save.flags.Add("afterlight_complete");
            WorldObject completedBook = Object(completed, "worldchronicle");
            int completedCoins = completed.save.coins, completedShards = completed.save.shards;
            int completedFlags = completed.save.flags.Count;
            completed.Talk(completedBook);
            ChooseStoryChoice(completed, "story_section_after");
            ChooseStoryChoice(completed, "story_read_ending");
            Check(completed.conversation.textEn.Contains("All three valves have been retuned.") &&
                completed.conversation.textEn.Contains("Public refuges: 8/8 built; long-term regional services: 3/3 signed.") &&
                completed.conversation.textEn.Contains("The public audit has been published.") &&
                completed.conversation.textEn.Contains("Afterlight sites restored: 3/3.") &&
                completed.conversation.textEn.Contains("The maintenance charter has been published."),
                "The ending account recognizes fully completed civic work without inventing or omitting outcomes");
            Check(completed.save.coins == completedCoins &&
                completed.save.shards == completedShards &&
                completed.save.flags.Count == completedFlags,
                "Reading a fully completed ending remains reward-neutral and save-neutral");
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
            g.save.x=compass.x;g.save.y=compass.y;
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

        public static void ChapterElevenFirstValveHasAReachableGreenWindow(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.save.room = 4;
            g.save.flags.Add("last_directive_complete");
            g.save.flags.Add("last_directive_open");
            WorldObject master = Object(g, "civicconduitmaster");
            WorldObject valve = null;
            foreach (WorldObject o in g.Map.objects)
                if (o.id == "civicConduitValve1") valve = o;
            Check(valve != null && g.CanStand(new Point(valve.x, valve.y)),
                "The first civic valve must be a real reachable object on the authored route");
            g.save.x = master.x;
            g.save.y = master.y;
            g.save.inventory[4] = 1;
            g.enemies.Clear();
            g.invulnerable = 120f;
            Action(g, "civicconduitmaster", "civic_conduit_start");

            Point start = g.Position;
            int frames = 0;
            while ((g.Position - start).Length < 7.25f && frames < 100)
            {
                g.Tick(.02f, new Point(1, 0));
                frames++;
            }
            Check(frames < 100 &&
                (g.Position - new Point(valve.x, valve.y)).Length <= 2.2f,
                "A normal-speed eastward approach reaches the first valve after its required real walking distance");
            Check(g.CivicConduitWindowOpen && g.CivicConduitWindowRemaining >= .85f,
                "The first green window must leave a usable interaction margin after the 7.2-unit approach");

            g.Talk(valve);
            int tune = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "civic_conduit_tune") tune = i;
            Check(tune >= 0, "The reachable first green window offers the real tuning action");
            g.Choose(tune);
            Check(g.CivicConduitActive && g.CivicConduitStage == 1,
                "The first valve can be completed during its opening green window without waiting a full cycle");
        }

        public static void ChapterElevenPublicGridUsesOrderedPhysicalValvesAndPersistsItsChoice(Campaign data)
        {
            foreach (int choice in new[] { 1, 2 })
            {
                var g = Fresh(data);
                g.save.stage = 3;
                g.save.room = 4;
                g.save.flags.Add("last_directive_complete");
                g.save.flags.Add(choice == 1 ? "last_directive_open" : "last_directive_shelter");
                WorldObject master = Object(g, "civicconduitmaster");
                g.save.x = master.x;
                g.save.y = master.y;
                WorldObject[] valves = new WorldObject[3];
                foreach (WorldObject o in g.Map.objects)
                {
                    int index = g.CivicConduitValveIndex(o);
                    if (index >= 0) valves[index] = o;
                }
                for (int i = 0; i < valves.Length; i++)
                    Check(valves[i] != null && g.CanStand(new Point(valves[i].x, valves[i].y)),
                        "Chapter Eleven valve " + (i + 1) + " must remain a real, reachable authored object");

                g.save.inventory[4] = 2;
                g.save.energy = 100;
                g.enemies.Clear();
                g.invulnerable = 200f;
                int coinsBefore = g.save.coins;
                int shardsBefore = g.save.shards;
                Action(g, "civicconduitmaster", "civic_conduit_start");
                Check(g.CivicConduitStarted && g.CivicConduitActive &&
                    g.save.inventory[4] == 1 && g.CivicConduitTimeLeft > 79f,
                    "Starting the live circuit consumes one real fuse exactly once and starts its actual timer");

                Action<WorldObject> walkTo = delegate(WorldObject target)
                {
                    int steps = 0;
                    Point destination = new Point(target.x, target.y);
                    while ((g.Position - destination).Length > .45f && steps < 2000)
                    {
                        Point delta = destination - g.Position;
                        g.Tick(.02f, delta.Length > .001f ? delta.Normalized : new Point());
                        steps++;
                    }
                    Check((g.Position - destination).Length <= .7f,
                        "The player must physically reach the next valve instead of remotely completing it: " + target.id);
                    Check(g.CivicConduitActive && g.CivicConduitTimeLeft > 0,
                        "Walking between valves must preserve a live, unexpired circuit");
                };

                if (choice == 1)
                {
                    walkTo(valves[2]);
                    g.Talk(valves[2]);
                    Check(g.mode == "dialog" && g.conversation.textEn.Contains("west to east"),
                        "A later valve explains the required west-to-east sequence instead of accepting a skip");
                    Check(g.CivicConduitStage == 0,
                        "Touching the third valve first cannot advance the first stage");
                    g.Close();
                }

                for (int i = 0; i < valves.Length; i++)
                {
                    walkTo(valves[i]);
                    int waited = 0;
                    while (g.CivicConduitActive && !g.CivicConduitWindowOpen && waited < 450)
                    {
                        g.Tick(.02f, new Point());
                        waited++;
                    }
                    Check(g.CivicConduitActive && g.CivicConduitWindowOpen &&
                        g.CivicConduitTimeLeft > 0,
                        "Each ordered valve must expose a reachable green timing window before the circuit expires");
                    g.Talk(valves[i]);
                    Check(g.mode == "dialog", "The current physical valve must open its real interaction panel");
                    int tune = -1;
                    for (int c = 0; c < g.conversation.choices.Length; c++)
                        if (g.conversation.choices[c].action == "civic_conduit_tune") tune = c;
                    Check(tune >= 0, "A stable, correctly reached valve must offer its real tune action");
                    g.Choose(tune);
                    if (i < valves.Length - 1)
                        Check(g.CivicConduitActive && g.CivicConduitStage == i + 1,
                            "A tuned valve advances exactly one stage and requires the next physical valve");
                }

                Check(g.CivicConduitComplete && !g.CivicConduitActive &&
                    g.GetQuest("civic_conduit").complete &&
                    g.save.coins == coinsBefore + 65 && g.save.shards == shardsBefore + 2 &&
                    g.save.inventory[4] == 1,
                    "Three real valve contacts award the Chapter Eleven reward once and preserve the unused fuse");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save));
                resumed.Start();
                Check(resumed.save.version == 2 && resumed.CivicConduitComplete &&
                    resumed.LastDirectiveChoice == choice && resumed.save.inventory[4] == 1,
                    "The completed grid and the chosen public service survive a normal SaveData v2 reload");

                var withoutGrid = new GameCore(data, CloneSave(resumed.save));
                withoutGrid.save.flags.Remove("civic_conduit_complete");
                withoutGrid.Start();
                resumed.save.room = 0;
                withoutGrid.save.room = 0;
                WorldObject shelter = Object(resumed, "farshelter");
                WorldObject controlShelter = Object(withoutGrid, "farshelter");
                resumed.save.flags.Add("far_shelter_0");
                withoutGrid.save.flags.Add("far_shelter_0");
                resumed.save.x = shelter.x;
                resumed.save.y = shelter.y;
                withoutGrid.save.x = controlShelter.x;
                withoutGrid.save.y = controlShelter.y;
                resumed.enemies.Clear();
                withoutGrid.enemies.Clear();
                resumed.invulnerable = 200f;
                withoutGrid.invulnerable = 200f;
                resumed.save.health = withoutGrid.save.health = 50;
                resumed.save.energy = withoutGrid.save.energy = 10;
                Advance(resumed, 1f);
                Advance(withoutGrid, 1f);
                float energyService = resumed.save.energy - withoutGrid.save.energy;
                float healthService = resumed.save.health - withoutGrid.save.health;
                if (choice == 1)
                    Check(energyService > 2.5f && energyService < 2.9f &&
                        Math.Abs(healthService) < .01f,
                        "The public-grid choice adds its real local stamina service at a rebuilt refuge");
                else
                    Check(Math.Abs(energyService) < .01f &&
                        healthService > .7f && healthService < 1.0f,
                        "The emergency-shelter choice adds its distinct real local healing service");
            }
        }

    }
}
