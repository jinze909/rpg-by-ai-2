using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FullInventoryAndWalletRemainStableThroughActualEnemyLoot(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999999;g.save.inventory[2]=9999;g.save.kills[1]=99999;
            int xp=g.save.xp;
            var raider=new Enemy(765,0,13,14,1) { stun=99 };
            g.enemies.Add(raider);
            g.Attack(new Point(1,0));
            Advance(g,.45f);
            Check(!g.enemies.Contains(raider) && g.save.xp>xp &&
                g.effects.Exists(e=>e.kind=="loot"),
                "A full bag must not suppress a real enemy defeat, XP or contact");
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Actual combat rewards cannot exceed the on-disk coin, item and kill caps");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.coins==999999 && loaded.save.inventory[2]==9999 &&
                loaded.save.kills[1]==99999,
                "A full reward state roundtrips without hidden reload-only loss");
            // Partial capacity still awards the usual loot exactly once.
            g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999998;g.save.inventory[2]=9998;g.save.kills[1]=99998;
            g.enemies.Add(new Enemy(766,0,13,14,1){stun=99});
            g.Attack(new Point(1,0));Advance(g,.45f);
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Last available resource slots are filled, never skipped or overrun");
        }

        public static void FullBagNeverConsumesGatherNodesOrReadyCrops(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            var plant=new WorldObject { id="qa-herb-stack", kind="herb", x=12, y=14, name="Moonherb", en="Moonherb" };
            g.save.inventory[2]=9999;
            g.Talk(plant);
            Check(!g.Has("node_"+plant.id) && g.save.inventory[2]==9999,
                "A full herb stack leaves the physical gathering node available");
            g.save.inventory[2]=9996;
            g.Talk(plant);
            Check(g.Has("node_"+plant.id) && g.save.inventory[2]==9999,
                "Freed capacity allows the same real node to deliver its three herbs");
            var ore=new WorldObject { id="qa-ore-stack", kind="ore", x=12, y=14, name="Ore", en="Ore" };
            g.save.inventory[1]=9996;g.save.inventory[4]=9999;
            g.save.bounty=1;g.save.bountyProgress=0;
            g.Talk(ore);
            Check(!g.Has("node_"+ore.id) && g.save.inventory[1]==9996 &&
                g.save.bountyProgress==0,
                "Multi-resource ore node cannot spend itself or bounty credit on partial capacity");
            g.save.inventory[4]=9996;g.Talk(ore);
            Check(g.Has("node_"+ore.id) && g.save.inventory[1]==9999 &&
                g.save.inventory[4]==9999 && g.save.bountyProgress==1,
                "Full ore and parts reward and actual bounty credit arrive together");
            g=Fresh(data);g.save.plantedAt=0;g.save.totalTime=61;
            g.save.inventory[2]=9999;g.save.inventory[7]=9997;
            int coins=g.save.coins;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==0 && g.save.coins==coins &&
                g.save.inventory[7]==9997,
                "Ready garden cannot consume a harvest if Moonherbs are full");
            g.save.inventory[2]=9994;g.save.inventory[7]=9999;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==0 && g.save.coins==coins &&
                g.save.inventory[2]==9994,
                "Ready garden remains available if only the seed stack is full");
            g.save.inventory[7]=9997;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==-1 && g.save.inventory[2]==9999 &&
                g.save.inventory[7]==9999 && g.save.coins==coins+8,
                "A mature saved crop pays its complete reward exactly once after freeing space");
        }

        public static void FullFishStackStopsWastedCastWithoutBreakingCatch(Campaign data)
        {
            var g=Fresh(data);
            g.save.inventory[3]=9999;g.save.bounty=2;g.save.bountyProgress=0;
            Action(g,"fish","fish");
            Check(g.mode=="world" && g.save.bountyProgress==0,
                "Full fish stack refuses minigame before any wasted cast");
            g.save.inventory[3]=9998;g.save.coins=999999;
            Action(g,"fish","fish");
            Check(g.mode=="fish","A freed stack can resume normal fishing");
            for (int i=0;i<3;i++){g.fishCursor=.5f;g.FishHit();}
            Check(g.mode=="world" && g.save.inventory[3]==9999 &&
                g.save.coins==999999 && g.save.bountyProgress==1,
                "Normal three-hit catch awards one bounded fish and real bounty progress");
        }

        public static void BoundedEnemyOpeningDelaysAndRestoredPatrols(Campaign data)
        {
            // The original early patrol rhythm must remain unchanged.
            for (int id = 1; id <= 8; id++)
            {
                Enemy early = new Enemy(id, id % 3, 14, 14, 40);
                Check(Math.Abs(early.timer - (1.3f + id * .13f)) < .0001f,
                    "Original early patrol opening cadence remains unchanged");
            }
            // IDs of snapshots and authored encounters are identifiers, not seconds.
            foreach (int id in new[] { 9, 450, 1905, 81012, 82032, 83001, 99999 })
            {
                Enemy candidate = new Enemy(id, 0, 14, 14, 50);
                Check(candidate.timer >= 1.42f && candidate.timer <= 2.35f,
                    "Large ID " + id + " must not delay its first attack for minutes or hours");
            }
            var g = Fresh(data); g.save.stage = 3; g.Enter(3);
            g.enemies.Clear(); g.PrepareSave();
            g.save.snapshotRoom = 3;
            g.save.worldEnemies = new[] {
                new EnemyRecord { id = 50000, kind = 1, x = 9.5f, y = 14.5f,
                    hp = 44, maxHp = 44, boss = false }
            };
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Enemy patrol = loaded.enemies.Find(e => e.id == 50000);
            Check(patrol != null && patrol.timer >= 1.42f && patrol.timer <= 2.35f,
                "Loading a high-ID patrol restores a playable opening attack delay");
        }

        public static void EliteVictoryRetiresWarningsBeforeNextDamageStep(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            g.save.flags.Add("archive_complete");
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(echo != null, "Atlas echo is actually spawned by the story altar");
            var echoWarning = new Hazard { position = g.Position, radius = 1.2f,
                damage = 999, timer = .01f, kind = "bell", atlasEchoOwned = true };
            var unrelated = new Hazard { position = new Point(4, 4), radius = .2f,
                damage = 1, timer = 10, kind = "root" };
            g.hazards.Add(echoWarning); g.hazards.Add(unrelated);
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hit.Invoke(g, new object[] { echo, 9999f, false, null, null });
            Check(!g.enemies.Contains(echo) &&
                !g.hazards.Exists(h => h.atlasEchoOwned) &&
                g.hazards.Contains(unrelated) &&
                g.AtlasEchoOpenTime == 0,
                "The final actual combat hit retires imminent echo hazards immediately, without erasing world hazards");
            g.save.reducedMotion = true;
            for (int frame = 0; frame < 6; frame++) g.Tick(.05f, new Point());
            Check(g.Has("archive_echo_complete"),
                "The same-frame safety cleanup still allows a proper one-time echo clear");
        }

        public static void OptionalEncountersNeverStartInsideEachOther(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            foreach (string flag in new[] { "archive_complete", "scout_saved",
                "dispatch_complete", "survey_3" }) g.save.flags.Add(flag);
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(g.AtlasEchoRoom == 3 && echo != null, "Elite challenge begins normally");
            // Move the echo away so that SafeTravel alone would allow another
            // encounter. The explicit mutex must still deny it.
            echo.x = 5; echo.y = 5;
            WorldObject beacon = Object(g, "survey3");
            g.save.x = beacon.x; g.save.y = beacon.y;
            Action(g, beacon.kind, "road_trial_start_3");
            Check(g.RoadwardenTrialRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                "Beacon defense cannot start while the atlas echo is active");
            WorldObject traveler = Object(g, "wayfarer3");
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, traveler.kind, "wayfarer_defend_3");
            Check(g.WayfarerEncounterRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Roadside ambush cannot start during an atlas echo challenge");
        }
        public static void FullStackStoryRewardsPreserveQuestProgress(Campaign data)
        {
            var g = Fresh(data);
            g.save.coins = 999995;
            g.save.inventory[0] = g.save.inventory[1] = 9998;
            g.save.inventory[6] = 9999;
            g.save.flags.Add("core_1");
            Action(g, "guild", "deliver");
            Check(g.save.stage == 1 && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999 && g.save.inventory[1] == 9999 &&
                g.save.inventory[6] == 9999,
                "Main story delivery progresses while each resource stays within its saved cap");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.save.stage == 1 && restored.save.coins == 999999 &&
                restored.save.inventory[0] == 9999 && restored.save.inventory[6] == 9999,
                "New chapter and legal inventory persist exactly across the v2 save roundtrip");

            g = Fresh(data);
            g.save.inventory[2] = 6; g.save.inventory[6] = 9999;
            g.save.coins = 999999; g.save.skillPoints = 99;
            Action(g, "garden", "ally1");
            Check(g.Has("ally_1") && g.save.inventory[2] == 0 &&
                g.save.inventory[6] == 9999 && g.save.coins == 999999 &&
                g.save.skillPoints == 99,
                "Commission consumes its materials, marks the ally and cannot overflow stew, cash or skill points");
            g = Fresh(data); g.Enter(1);
            g.save.coins = 999999; g.save.inventory[1] = 9999;
            g.Talk(Object(g, "lore"));
            Check(g.Has("treasure_1") && g.Has("lore_1") &&
                g.save.coins == 999999 && g.save.inventory[1] == 9999,
                "First lore discovery preserves its permanent flags without invalid crystal rewards");
            g.Close(); g.save.kills[1] = 4; g.save.coins = 999999;
            Action(g, "scout", "scout");
            Check(g.Has("scout_saved") && g.save.coins == 999999,
                "Scout rescue unlocks the road without overfilling the saved wallet");
        }

        public static void FullWalletBountyAndAbyssBankingDoNotOverflow(Campaign data)
        {
            var g = Fresh(data);
            g.AcceptBounty(0);
            g.save.bountyProgress = GameCore.BountyTargets[0];
            g.save.coins = 999999; g.save.shards = 99999;
            int beforeXp = g.save.xp;
            Check(g.ClaimBounty() && g.save.bounty == -1 &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.xp > beforeXp,
                "Completed bounty pays experience and clears its contract at full monetary caps");
            var abyss = Fresh(data);
            abyss.save.frontierEnding = 1;
            abyss.StartAbyss(0);
            Check(abyss.InAbyss, "Legal covenant actually starts a real procedural abyss run");
            abyss.enemies.Clear();
            abyss.save.flags.Add("abyss_clear_1");
            abyss.save.coins = 999998;
            abyss.save.shards = 99998;
            abyss.save.runCoins = int.MaxValue;
            abyss.save.runShards = int.MaxValue;
            abyss.TakeBoon(0);
            Check(abyss.save.abyssFloor == 2 && abyss.save.coins == 999999 &&
                abyss.save.shards == 99999 && abyss.save.runCoins == 0 &&
                abyss.save.runShards == 0 && abyss.save.boons[0] == 1,
                "Real Abyss floor banking saturates oversized unbanked wealth and still grants the next floor boon");
        }

        public static void StaleAbyssBankChoiceNeverDropsPendingFloorLoot(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss, "Authorized covenant must start a real floor before settlement testing");
            g.enemies.Clear();
            g.save.runCoins = 43; g.save.runShards = 2; g.save.runXp = 0;
            int originalCoins = g.save.coins, originalShards = g.save.shards;
            g.Talk(Object(g, "abyssGate"));
            Check(g.mode == "dialog" && g.Has("abyss_clear_1"),
                "The actual gate offers banking only after a cleared floor");
            int bank = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "abyssBank") bank = i;
            Check(bank >= 0, "Live Abyss gate offers the expected bank choice");
            // Simulate a hostile spawned between opening the dialogue and
            // selecting its stale bank command. BankFloor previously returned,
            // but Choose still ended the expedition and erased its resources.
            var lateHostile = new Enemy(49001, 0, 17, 14, 44);
            g.enemies.Add(lateHostile);
            g.Choose(bank);
            Check(g.InAbyss && g.save.abyssFloor == 1 &&
                g.save.runCoins == 43 && g.save.runShards == 2 &&
                g.save.coins == originalCoins && g.save.shards == originalShards,
                "Stale bank menu must not close a live floor or erase pending coins and shards");

            g.enemies.Clear();
            g.Talk(Object(g, "abyssGate"));
            bank = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "abyssBank") bank = i;
            Check(bank >= 0, "A safe cleared gate can be reopened normally");
            g.save.flags.Remove("abyss_clear_1");
            g.Choose(bank);
            Check(g.InAbyss && g.save.runCoins == 43 && g.save.runShards == 2,
                "A stale floor-clear flag cannot end an unbanked expedition either");

            // Completing the floor again legitimately banks exactly once.
            Action(g, "abyssGate", "abyssBank");
            Check(!g.InAbyss && g.save.room == 0 &&
                g.save.coins == originalCoins + 43 &&
                g.save.shards == originalShards + 2 &&
                g.save.runCoins == 0 && g.save.runShards == 0,
                "A renewed genuine root-gate decision still banks and extracts the entire reward");
        }

        public static void AbyssPendingRewardsStayWithinSaveCapsUntilCollected(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss && g.enemies.Count > 0,
                "Procedural Abyss creates live opponents for the pending-reward regression");
            g.save.runCoins = g.save.runShards = g.save.runXp = 9998;
            Enemy foe = g.enemies[0];
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hit.Invoke(g, new object[] { foe, 9999f, false, null, null });
            Check(!g.enemies.Contains(foe) &&
                g.save.runCoins == 9999 && g.save.runShards == 9999 &&
                g.save.runXp == 9999,
                "Actual Abyss enemy death never overflows any unbanked v2 resource");

            g.save.x = 47.5f; g.save.y = 28.5f;
            g.Talk(Object(g, "abyssCache"));
            Check(g.Has("abyss_annex_cache_1") &&
                g.save.runCoins == 9999 && g.save.runShards == 9999,
                "Actual side-chamber loot stays at cap while its discovery is still recorded");
            g.Close();
            g.save.x = 12.5f; g.save.y = 28.5f;
            g.Talk(Object(g, "abyssSurvey"));
            Check(g.Has("abyss_annex_survey_1") && g.save.runXp == 9999,
                "Floor's survey journal cannot overflow pending experience");
            g.Close();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.save.runCoins == 9999 &&
                restored.save.runShards == 9999 && restored.save.runXp == 9999 &&
                restored.Has("abyss_annex_cache_1"),
                "Legitimate capped unbanked resources and explored cache persist across actual v2 reload");
            restored.enemies.Clear();
            restored.save.coins = 999999; restored.save.shards = 99999;
            Action(restored, "abyssGate", "abyssBank");
            Check(!restored.InAbyss && restored.save.room == 0 &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.runCoins == 0 && restored.save.runShards == 0 &&
                restored.save.runXp == 0,
                "Reloaded capped rewards settle once without bank overflow or a stranded Abyss run");
        }

    }
}
