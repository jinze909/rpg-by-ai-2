using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void LostSurveyorAtlasHasRealSequenceAndOneTimeRewards(Campaign data)
        {
            var g = Fresh(data);
            Check(g.GetQuest("archive_trail").locked,
                "The lost atlas must start as a discoverable quest");
            WorldObject keeper = Object(g, "archivekeeper");
            g.save.x = keeper.x; g.save.y = keeper.y;
            Check(g.CanStand(g.Position) && g.Nearest() == keeper,
                "The new cartographer exists on walkable town ground");
            Action(g, keeper.kind, "archive_accept");
            Check(g.Has("archive_started") && !g.GetQuest("archive_trail").locked &&
                g.save.trackedQuest == "archive_trail",
                "Talking to Lin accepts and tracks the atlas quest");
            // Chapters are still locked by the original main quest: the atlas
            // must neither teleport nor grant premature access to region 2.
            g.Enter(2);
            Check(g.save.room == 0, "Atlas commission cannot bypass chapter gate to ruins");
            // This harness now advances the original campaign gates before
            // inspecting its three optional, ordered artifacts.
            g.save.stage = 3;
            int coins = g.save.coins, shards = g.save.shards, skill = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject artifact = Object(g, "archivefragment" + region);
                g.save.x = artifact.x; g.save.y = artifact.y;
                Check(g.CanStand(g.Position) && g.Nearest() == artifact,
                    "Each regional atlas artifact is a real reachable map interaction");
                if (region == 2)
                {
                    g.save.flags.Remove("archive_fragment_1");
                    g.Talk(artifact);
                    Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_claim_2"),
                        "The tide inscription is unreadable before the woodland chart");
                    g.Close(); g.save.flags.Add("archive_fragment_1");
                }
                Action(g, artifact.kind, "archive_claim_" + region);
                Check(g.Has("archive_fragment_" + region) &&
                    g.save.coins == coins + region * 20 &&
                    g.save.shards == shards + region * 2,
                    "Each recovered artifact pays its one-time regional reward");
                g.Talk(artifact);
                Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_claim_" + region),
                    "Collected artifacts cannot be repeatedly claimed");
                g.Close();
            }
            Check(!g.GetQuest("archive_trail").complete && g.GetQuest("archive_trail").room == 0,
                "Collecting all records directs the player back to Lin");
            g.Enter(0); g.save.x = keeper.x; g.save.y = keeper.y;
            Action(g, keeper.kind, "archive_complete");
            Check(g.Has("archive_complete") && g.GetQuest("archive_trail").complete &&
                g.save.coins == coins + 120 && g.save.shards == shards + 6 &&
                g.save.skillPoints == skill + 1,
                "Restoring the atlas grants one final talent and the exact stated rewards");
            g.Talk(keeper);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "archive_complete"),
                "Completed town dialogue cannot pay a second final reward");
            g.Close(); g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.GetQuest("archive_trail").complete &&
                loaded.save.coins == g.save.coins &&
                loaded.save.skillPoints == g.save.skillPoints,
                "Atlas quest and its rewards persist through the existing save format");
        }
    }
}
