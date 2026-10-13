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
            // Reward tests isolate the quest from ordinary XP level-up grants.
            g.save.level = 10; g.save.xp = 0;
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

        public static void StarrootCounterpointIsNewPlayableCrossRegionStory(Campaign data)
        {
            var g = Fresh(data);
            Check(g.GetQuest("root_harmony").locked &&
                !g.Has("root_harmony_started"),
                "The counterpoint is independently discoverable and gated before its NPC accepts it");
            WorldObject rui = Object(g, "rootlistener");
            g.save.x = rui.x; g.save.y = rui.y;
            Check(g.CanStand(g.Position), "Rui occupies real walkable town terrain");
            g.Talk(rui);
            Check(!Array.Exists(g.conversation.choices,
                c => c.action == "root_harmony_accept"),
                "The player must first rescue the scout, preserving the opening story");
            g.Close(); g.save.flags.Add("scout_saved");
            Action(g, "rootlistener", "root_harmony_accept");
            Check(g.Has("root_harmony_started") &&
                !g.GetQuest("root_harmony").locked &&
                g.save.trackedQuest == "root_harmony",
                "Rui's actual dialogue starts a new tracked quest");
            // Unlock the original chapter travel gates for isolated optional
            // content coverage, without awarding story-boss completions.
            g.save.stage = 3; g.save.level = 10;
            g.Enter(2); g.enemies.Clear();
            WorldObject premature = Object(g, "rootcounter2");
            g.save.x = premature.x; g.save.y = premature.y;
            g.Talk(premature);
            Check(!Array.Exists(g.conversation.choices,
                c => c.action.StartsWith("root_harmony_tune_")),
                "The second region cannot skip the woodland reading");
            g.Close();
            int coins = g.save.coins, shards = g.save.shards,
                talent = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject node = Object(g, "rootcounter" + region);
                g.save.x = node.x; g.save.y = node.y;
                Check(g.CanStand(g.Position) && g.Nearest() == node,
                    "Each brand-new listening stone is a physical, reachable in-world object");
                Check(g.GetQuest("root_harmony").room == region,
                    "The live quest guide points to the next unfinished region");
                int right = region == 1 ? 2 : region == 2 ? 0 : 1;
                int wrong = (right + 1) % 3;
                float energy = g.save.energy;
                Action(g, node.kind, "root_harmony_tune_" + region + "_" + wrong);
                Check(!g.Has("root_harmony_" + region) &&
                    g.RootHarmonyRoom == 0 && Math.Abs(g.save.energy - (energy - 8)) < .001f,
                    "An incorrect landscape note costs real energy without auto-completing a region");
                if (region == 1)
                {
                    Action(g, node.kind, "root_harmony_tune_1_2");
                    Check(g.RootHarmonyRoom == 1 &&
                        g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3,
                        "Tuning creates three real sentinels with a live 40-second encounter");
                    // A save taken during combat may preserve normal patrols,
                    // but must never resurrect transient quest sentinels as
                    // ordinary loot-bearing enemies or fake a completion.
                    g.PrepareSave();
                    Check(g.save.worldEnemies != null &&
                        !Array.Exists(g.save.worldEnemies, e =>
                            e.id >= 88000 && e.id < 88100),
                        "Encounter sentinels are excluded from persistent ordinary enemy snapshots");
                    var restarted = new GameCore(data, CloneSave(g.save));
                    restarted.Start();
                    Check(restarted.RootHarmonyRoom == 0 &&
                        !restarted.Has("root_harmony_1") &&
                        !restarted.enemies.Exists(restarted.IsRootHarmonyEnemy),
                        "Loading during battle can retry but cannot skip or duplicate special enemies");
                    // Expire the encounter through real world ticks; do
                    // not expose a public setter for quest timers just for tests.
                    foreach (Enemy sentinel in g.enemies)
                        if (g.IsRootHarmonyEnemy(sentinel)) sentinel.stun = 60f;
                    for (int frame = 0; frame < 810 && g.RootHarmonyRoom != 0; frame++)
                        g.Tick(.05f, new Point());
                    Check(g.RootHarmonyRoom == 0 &&
                        !g.enemies.Exists(g.IsRootHarmonyEnemy) &&
                        !g.Has("root_harmony_1"),
                        "Timer failure retires encounter enemies with no progress or bonus rewards");
                }
                Action(g, node.kind, "root_harmony_tune_" + region + "_" + right);
                Check(g.RootHarmonyRoom == region && g.RootHarmonyTime > 39 &&
                    g.enemies.FindAll(g.IsRootHarmonyEnemy).Count == 3 &&
                    !g.Has("root_harmony_" + region),
                    "The correct note starts real timed combat, not instant flag collection");
                g.Talk(node);
                Check(!Array.Exists(g.conversation.choices, c =>
                    c.action == "root_harmony_seal_" + region),
                    "A live enemy must be defeated before reading can be sealed");
                g.Close();
                // The original combat engine owns collision, health, death
                // and attack windows. Isolate one actual sword strike.
                if (region == 1)
                {
                    Enemy sentinel = g.enemies.Find(g.IsRootHarmonyEnemy);
                    Point stand = new Point(sentinel.x - .65f, sentinel.y);
                    if (g.CanStand(stand))
                    {
                        g.save.x = stand.x; g.save.y = stand.y;
                        sentinel.stun = 5;
                        float hp = sentinel.hp;
                        g.Attack((sentinel.Position - g.Position).Normalized);
                        for (int i = 0; i < 13; i++) g.Tick(.05f, new Point());
                        Check(sentinel.hp < hp || !g.enemies.Contains(sentinel),
                            "A real player attack damages the spawned counterpoint enemy");
                    }
                }
                // For the multi-region reward contract, equivalent defeated
                // enemy state is supplied after the actual combat integration
                // above has been verified.
                g.enemies.RemoveAll(g.IsRootHarmonyEnemy);
                g.save.x = node.x; g.save.y = node.y;
                Action(g, node.kind, "root_harmony_seal_" + region);
                Check(g.Has("root_harmony_" + region) && g.RootHarmonyRoom == 0 &&
                    g.save.coins == coins + 25 * region &&
                    g.save.shards == shards + 2 * region,
                    "The completed combat objective grants a strictly once-per-region reward");
                g.Talk(node);
                Check(!Array.Exists(g.conversation.choices,
                    c => c.action == "root_harmony_seal_" + region ||
                        c.action.StartsWith("root_harmony_tune_")),
                    "A sealed resonance node cannot spawn repeat paid combat or claim loot again");
                g.Close();
            }
            Check(!g.GetQuest("root_harmony").complete &&
                g.GetQuest("root_harmony").room == 0,
                "All three readings require a real return to the new town NPC");
            g.Enter(0); g.save.x = rui.x; g.save.y = rui.y;
            Action(g, "rootlistener", "root_harmony_finish");
            Check(g.Has("root_harmony_complete") &&
                g.GetQuest("root_harmony").complete &&
                g.save.skillPoints == talent + 1 &&
                g.save.coins == coins + 150 && g.save.shards == shards + 6,
                "Rui archives three physical readings and grants only the final talent reward");
            g.Talk(rui);
            Check(!Array.Exists(g.conversation.choices, c =>
                c.action == "root_harmony_finish"),
                "Completed counterpoint cannot be repeatedly turned in");
            g.Close(); g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.GetQuest("root_harmony").complete &&
                loaded.save.coins == g.save.coins &&
                loaded.save.skillPoints == g.save.skillPoints,
                "The new entire quest persists through existing version-two saves");
        }
    }
}
