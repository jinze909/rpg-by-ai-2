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
            Check(!g.Has("treasure_1") && g.Has("lore_1") &&
                g.save.coins == 999999 && g.save.inventory[1] == 9999,
                "First lore discovery persists narrative history without consuming a blocked physical relic reward");
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

        public static void OneTimeStorySuppliesWaitForActualStorageCapacity(Campaign data)
        {
            var g = Fresh(data);
            g.save.flags.Add("scout_saved");
            g.save.inventory[6] = 9999;
            int history = g.save.moments.Count;
            Action(g, "returnscout", "scoutgift");
            Check(!g.Has("scout_gift") && g.save.inventory[6] == 9999 &&
                g.save.moments.Count == history,
                "Full stew stack must not burn scout's one-time gift or fabricate a gift journal entry");
            g.save.inventory[6] = 9998;
            Action(g, "returnscout", "scoutgift");
            Check(g.Has("scout_gift") && g.save.inventory[6] == 9999 &&
                g.save.moments.Count == history + 1,
                "Returning after using stew grants the real gift once");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            loaded.save.inventory[6] = 9998;
            Action(loaded, "returnscout", "scoutgift");
            Check(loaded.save.inventory[6] == 9998 && loaded.Has("scout_gift"),
                "Previously received scout gift stays claimed after a v2 reload");

            g = Fresh(data);
            g.Enter(1);
            g.save.coins = 999999; g.save.inventory[1] = 9999;
            g.Talk(Object(g, "lore"));
            Check(g.Has("lore_1") && !g.Has("treasure_1") &&
                g.save.coins == 999999 && g.save.inventory[1] == 9999 &&
                g.conversation.textEn.Contains("remain"),
                "Full wallet and crystals must preserve the region's lore and leave its first loot recoverable");
            g.Close();
            g.save.coins = 999979; g.save.inventory[1] = 9998;
            g.Talk(Object(g, "lore"));
            Check(!g.Has("treasure_1") && g.save.coins == 999979 &&
                g.save.inventory[1] == 9998,
                "Partial crystal capacity may not consume an atomic two-crystal relic prize");
            g.Close();
            g.save.inventory[1] = 9997;
            g.Talk(Object(g, "lore"));
            Check(g.Has("treasure_1") && g.save.coins == 999999 &&
                g.save.inventory[1] == 9999,
                "Making exactly enough room awards all first relic resources together");
            g.Close(); g.Talk(Object(g, "lore"));
            Check(g.save.coins == 999999 && g.save.inventory[1] == 9999,
                "Repeated lore interactions do not mint duplicate rewards");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("lore_1") && resumed.Has("treasure_1") &&
                resumed.save.coins == 999999 && resumed.save.inventory[1] == 9999,
                "Both lore discovery and the legitimately collected treasure survive reload");
        }

        public static void PhysicalFrontierCachesStayClaimableWithFullStacks(Campaign data)
        {
            var g = Fresh(data);
            // The physical rescue box remains locked behind its relay.
            g.save.flags.Add("outer_relay_0");
            var cache = Object(g, "outercache");
            g.save.x = cache.x; g.save.y = cache.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(cache);
            Check(!g.OuterCacheRecovered(0) && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Full rescue materials and wallet must not consume a one-time fourth-ring relief chest");
            g.save.coins = 999981; g.save.inventory[0] = 9998;
            g.Talk(cache);
            Check(!g.OuterCacheRecovered(0) && g.save.coins == 999981,
                "One missing material slot keeps both resources waiting in the real rescue chest");
            g.save.inventory[0] = 9997; g.Talk(cache);
            Check(g.OuterCacheRecovered(0) && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Freeing all required capacity awards the actual rescue chest atomically");
            g.Close(); g.Talk(cache);
            Check(g.save.coins == 999999 && g.save.inventory[0] == 9999,
                "Claimed fourth-ring relief box cannot duplicate its reward");

            g = Fresh(data);
            var hidden = Object(g, "farhidden");
            g.save.x = hidden.x; g.save.y = hidden.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(hidden);
            Check(!g.Has("far_secret_0") && g.save.inventory[0] == 9999,
                "Remote sixth-ring cache survives its first full-stack interaction");
            g.save.coins = 999990; g.save.inventory[0] = 9997;
            g.Talk(hidden);
            Check(g.Has("far_secret_0") && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Sixth-ring cache pays all nine coins and both materials once space is available");

            g = Fresh(data);
            var annex = Object(g, "annexcache");
            g.save.x = annex.x; g.save.y = annex.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(annex);
            Check(!g.Has("annex_cache_" + annex.id),
                "Full fourth-ring annex cache remains uncollected instead of erasing resources");
            g.save.coins = 999992; g.save.inventory[0] = 9997;
            g.Talk(annex);
            Check(g.Has("annex_cache_" + annex.id) &&
                g.save.coins == 999999 && g.save.inventory[0] == 9999,
                "Actual annex exploration pickup awards both saved resources after capacity is freed");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("annex_cache_" + annex.id) &&
                loaded.save.coins == 999999 && loaded.save.inventory[0] == 9999,
                "Collected cache flags and fully accounted inventories roundtrip through v2");
        }

        public static void PublicWorkshopCannotConsumeIngredientsForOverfullParts(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.save.ending = 3; // Public post-finale workshops are unavailable before the real ending.
            g.save.flags.Add("boss_4");
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_3_care");
            g.save.flags.Add("afterlight_done_3");
            g.Enter(3); g.enemies.Clear();
            WorldObject foundry = Object(g, "afterlightoutlet3");
            g.save.x = foundry.x; g.save.y = foundry.y;
            g.save.inventory[0] = 3; g.save.inventory[1] = 5;
            g.save.inventory[4] = 9999;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 3 && g.save.inventory[1] == 5 &&
                g.save.inventory[4] == 9999,
                "Real public recycling cannot spend timber and starcrystal for parts discarded beyond the save cap");
            g.save.inventory[4] = 9998;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 3 && g.save.inventory[1] == 5,
                "Recycling also rejects partial room for an indivisible two-part batch");
            g.save.inventory[4] = 9997;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 2 && g.save.inventory[1] == 3 &&
                g.save.inventory[4] == 9999,
                "The same real workshop charges one full recipe only after both produced parts fit");
        }

        public static void OrrinPublicReliefResponsibilityWaitsForActualGoods(Campaign data)
        {
            foreach(bool aid in new[] { true, false })
            {
                var g = Fresh(data);
                g.save.stage = 3; g.save.ending = 2;
                foreach(string flag in new[] { "afterlight_complete", "storm_started",
                    "storm_complete", "storm_orrin_open", "hearing_started",
                    "hearing_complete", "hearing_public", "boss_4",
                    "hearing_voice_1_care", "hearing_voice_2_watch",
                    "hearing_voice_3_care" }) g.save.flags.Add(flag);
                g.Enter(4); g.enemies.Clear();
                WorldObject shrine = Object(g, "shrine");
                g.save.x = shrine.x; g.save.y = shrine.y;
                int slot = aid ? 6 : 4;
                string choice = aid ? "hearing_orrin_aid" : "hearing_orrin_patrol";
                g.save.inventory[slot] = 9999;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == 0 && !g.Has(choice) &&
                    g.save.inventory[slot] == 9999 && g.save.ending == 2,
                    "Full relief stack may not consume Orrin's irreversible civilian responsibility choice");
                g.save.inventory[slot] = 9998;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == 0 &&
                    g.save.inventory[slot] == 9998,
                    "Public accountability transfer remains pending if only one slot is available");
                g.save.inventory[slot] = 9997;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == (aid ? 1 : 2) &&
                    g.Has(choice) && g.save.inventory[slot] == 9999 &&
                    g.save.ending == 2,
                    "Both public supplies are transferred only when their full inventory capacity exists");
                g.PrepareSave();
                var restored = new GameCore(data, CloneSave(g.save));
                Check(restored.HearingOrrinResponsibility == (aid ? 1 : 2) &&
                    restored.save.inventory[slot] == 9999,
                    "Completed civic responsibility and tangible supplies persist in a v2 save");
            }
        }

        public static void LateCampaignPublicationsKeepRewardsSaveCompatible(Campaign data)
        {
            var atlas = Fresh(data);
            atlas.save.coins = 999999; atlas.save.skillPoints = 99;
            atlas.save.flags.Add("archive_started");
            foreach(int region in new[] { 1, 2, 3 })
                atlas.save.flags.Add("archive_fragment_" + region);
            int originalXp = atlas.save.xp;
            Action(atlas, "archivekeeper", "archive_complete");
            Check(atlas.Has("archive_complete") &&
                atlas.save.coins == 999999 && atlas.save.skillPoints == 99 &&
                atlas.save.xp > originalXp,
                "Authentic chapter-spanning atlas publication grants XP and story progress without overfilling rewards");

            var civic = Fresh(data);
            civic.save.stage = 3; civic.save.ending = 3;
            civic.save.flags.Add("boss_4");
            civic.save.flags.Add("afterlight_started");
            for(int region=1; region<=3; region++)
            {
                civic.save.flags.Add("afterlight_done_" + region);
                civic.save.flags.Add("afterlight_steward_" + region);
            }
            civic.save.coins = 999990;
            civic.save.shards = 99998;
            civic.save.skillPoints = 99;
            WorldObject council = Object(civic, "afterlightcouncil");
            civic.save.x = council.x; civic.save.y = council.y;
            Action(civic, "afterlightcouncil", "afterlight_publish");
            Check(civic.Has("afterlight_complete") &&
                civic.save.coins == 999999 && civic.save.shards == 99999 &&
                civic.save.skillPoints == 99 && civic.save.ending == 3,
                "Actually publishing the public reconstruction charter caps all persistent rewards without changing the original ending");
            civic.PrepareSave();
            var restored = new GameCore(data, CloneSave(civic.save));
            Check(restored.Has("afterlight_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "Max-cap public charter rewards roundtrip through the original v2 save model");
        }

        public static void DistantRidgeWayfindingRemainsSaveBounded(Campaign data)
        {
            var g = Fresh(data);
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99;
            var north = Array.Find(data.maps[0].objects,
                o => o.id == "ridge_route_0_0");
            var south = Array.Find(data.maps[0].objects,
                o => o.id == "ridge_route_0_1");
            Check(north != null && south != null,
                "Both actual sixth-ring navigation markers must remain authored");
            g.save.x = north.x; g.save.y = north.y; g.Talk(north);
            Check(g.RidgeSeen(0, 0) && !g.RidgeLinked(0),
                "First marker records the real route without minting its two-point completion");
            g.Close(); g.save.x = south.x; g.save.y = south.y; g.Talk(south);
            Check(g.RidgeLinked(0) &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99,
                "Completing the real ridge connection preserves a full persistent wallet and shard ledger");
            g.Close(); g.Talk(south);
            Check(g.save.coins == 999999 && g.save.shards == 99999,
                "Returning to an already surveyed route cannot duplicate its grant");
            g.PrepareSave();
            var reload = new GameCore(data, CloneSave(g.save));
            Check(reload.RidgeLinked(0) && reload.save.coins == 999999 &&
                reload.save.shards == 99999,
                "World-route completion and legal coin/shard totals persist through the original v2 save");
        }

        public static void DeepAbyssGalleryRewardsRespectRealMidrunSaveCaps(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss, "A real authorized expedition starts before gallery reward tests");
            WorldObject survey = Object(g, "abyssFarSurvey");
            WorldObject cache = Object(g, "abyssFarCache");
            WorldObject echo = Object(g, "abyssFarEcho");
            g.save.runXp = g.save.runCoins = g.save.runShards = 9998;
            g.save.x = survey.x; g.save.y = survey.y;
            g.Talk(survey); g.Close();
            Check(g.Has("abyss_annex_far_survey_1") &&
                g.save.runXp == 9999,
                "Physical lower-gallery testimony caps pending XP without erasing its discovery");
            g.save.x = cache.x; g.save.y = cache.y;
            g.Talk(cache); g.Close();
            Check(g.Has("abyss_annex_far_cache_1") &&
                g.save.runCoins == 9999 && g.save.runShards == 9999,
                "Real remote supply cache limits both unbanked physical resources to the persisted v2 bounds");
            g.save.x = echo.x; g.save.y = echo.y;
            g.Talk(echo); g.Close();
            Check(g.Has("abyss_annex_far_echo_1") && g.save.runXp == 9999,
                "Echo evidence completes the narrative without overflowing pending XP");
            g.Talk(echo); g.Close();
            Check(g.save.runXp == 9999,
                "Repeated dialogue cannot duplicate lower-gallery pending rewards");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.save.runXp == 9999 &&
                restored.save.runCoins == 9999 && restored.save.runShards == 9999 &&
                restored.Has("abyss_annex_far_cache_1") &&
                restored.Has("abyss_annex_far_echo_1"),
                "All three original-save-format unbanked resources and discovery flags survive reload");
            restored.enemies.Clear();
            int coins = restored.save.coins, shards = restored.save.shards;
            Action(restored, "abyssGate", "abyssBank");
            Check(!restored.InAbyss && restored.save.room == 0 &&
                restored.save.coins == coins + 9999 &&
                restored.save.shards == shards + 9999,
                "Authentic cleared-floor extraction banks preserved gallery loot exactly once");
        }

        public static void PublishedGlasswindRoutesRemainValidAtMaximumSavedRewards(Campaign data)
        {
            var g = Fresh(data);
            g.save.flags.Add("desert_report");
            g.save.flags.Add("glasswind_all_sealed");
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99;
            var author = Object(g, "glasswindkeeper");
            g.save.x = author.x; g.save.y = author.y;
            int xp = g.save.xp;
            g.Talk(author);
            Check(g.mode == "dialog" && g.conversation != null &&
                Array.Exists(g.conversation.choices, c => c.action == "glasswind_publish"),
                "Physical wind surveyor must offer publication after the authentic desert report and three seals; flags=" +
                g.Has("desert_report") + "/" + g.Has("glasswind_all_sealed") +
                " choices=" + (g.conversation == null ? "<none>" :
                    string.Join(",", Array.ConvertAll(g.conversation.choices, c => c.action))));
            g.Close();
            Action(g, author.kind, "glasswind_publish");
            Check(g.Has("glasswind_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.xp > xp,
                "Physically publishing the completed public wind chart records its real achievement and XP without exceeding old saved currency caps");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.Has("glasswind_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "Public wind charter and bounded one-time payout survive a genuine old-format save reload");
            restored.Talk(author);
            Check(restored.mode == "dialog" && restored.conversation != null &&
                !Array.Exists(restored.conversation.choices,
                    c => c.action == "glasswind_publish") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "After publication the real NPC removes its one-shot claim option and cannot issue another payout");
        }

        public static void TriadAuditPublicationCreditsRealStoryWithoutSaveOverflow(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            foreach (string flag in new[] { "core_1", "core_2", "core_3",
                "triad_started", "triad_sealed" }) g.save.flags.Add(flag);
            g.save.coins = 999999; g.save.shards = 99999;
            var guild = Object(g, "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            int xp = g.save.xp;
            Action(g, guild.kind, "triad_publish");
            Check(g.Has("triad_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.xp > xp,
                "Real fourth-chapter three-core audit can be published at the wallet limits without corrupting persistent totals");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("triad_complete") && loaded.save.coins == 999999 &&
                loaded.save.shards == 99999 && !loaded.Has("boss_4"),
                "Actual evidence publication and original ending gate survive the v2 save roundtrip");
        }

        public static void EightPhysicalTrailCircuitsPreserveFinalTalentAtSaveLimits(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            for (int region = 1; region <= 7; region++)
                g.save.flags.Add("trail_circuit_" + region + "_3");
            g.save.coins = 999998; g.save.shards = 99998;
            g.save.skillPoints = 99;
            Check(g.TrailCircuitCount == 7 && !g.Has("trail_circuits_all"),
                "Seven independently completed regions are not an earned full roadwarden network");
            int xp = g.save.xp;
            for (int step = 1; step <= 3; step++)
            {
                WorldObject bearing = null;
                foreach (WorldObject o in g.Map.objects)
                    if (o.id == "trailcircuit_0_" + step && o.kind == "trailcircuit")
                    { bearing = o; break; }
                Check(bearing != null, "Original Emberfall map retains authored walkable field bearing " + step);
                g.save.x = bearing.x; g.save.y = bearing.y;
                g.Talk(bearing);
                Check(g.Has("trail_circuit_0_" + step) &&
                    g.TrailCircuitProgress(0) == step,
                    "Physically reading the next original roadmarker advances this region's real ordered evidence");
                g.Close();
            }
            Check(g.TrailCircuitCount == 8 && g.Has("trail_circuits_all") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.xp > xp,
                "Eight fully surveyed routes earn the original capped currency, XP, regional rest, and final talent milestone");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.TrailCircuitCount == 8 &&
                restored.Has("trail_circuits_all") &&
                restored.save.coins == 999999 &&
                restored.save.shards == 99999 && restored.save.skillPoints == 99,
                "The complete regional walking network remains identical through legacy v2 save reload");
        }

        public static void PublishedSkywatchAndFinalDirectiveRespectV2RewardCaps(Campaign data)
        {
            var sky = Fresh(data); sky.save.stage = 3; sky.save.level = 12;
            foreach (string flag in new[] { "desert_report", "mountain_report",
                "gate_7", "skywatch_started", "skywatch_defeated",
                "skywatch_prism_1", "skywatch_prism_2", "skywatch_prism_3" })
                sky.save.flags.Add(flag);
            sky.save.coins = 999998; sky.save.shards = 99998;
            sky.save.skillPoints = 99;
            var observer = Object(sky, "skywatcher");
            sky.save.x = observer.x; sky.save.y = observer.y;
            int xp = sky.save.xp;
            Action(sky, observer.kind, "skywatch_publish");
            Check(sky.Has("skywatch_complete") &&
                sky.save.coins == 999999 && sky.save.shards == 99999 &&
                sky.save.skillPoints == 99 && sky.save.xp > xp,
                "The original public sky report completes and grants real XP without exceeding v2 currency and talent caps");
            sky.Enter(7); sky.enemies.Clear();
            var altar = Object(sky, "skyobservatory");
            sky.save.x = altar.x; sky.save.y = altar.y;
            foreach (int n in new[] {1,2,3}) sky.save.flags.Add("skywatch_afterglow_" + n);
            int afterglowXp = sky.save.xp;
            Action(sky, altar.kind, "skywatch_afterglow_publish");
            Check(sky.Has("skywatch_afterglow_complete") &&
                sky.save.coins == 999999 && sky.save.shards == 99999 &&
                sky.save.xp > afterglowXp,
                "The actual three-prism aftermath publication grants real XP and bounded rewards, not an overwritten save");
            sky.PrepareSave();
            var reloadedSky = new GameCore(data, CloneSave(sky.save));
            Check(reloadedSky.Has("skywatch_complete") &&
                reloadedSky.Has("skywatch_afterglow_complete") &&
                reloadedSky.save.coins == 999999 && reloadedSky.save.shards == 99999 &&
                reloadedSky.save.skillPoints == 99,
                "Both earned skywatch chapters persist in the original save format at maximum capacity");

            foreach (string policy in new[] {"last_directive_open","last_directive_shelter"})
            {
                var g = Fresh(data); g.save.stage = 4;
                g.save.flags.Add("civic_response_ledger"); g.save.flags.Add(policy);
                g.save.coins = 999998; g.save.shards = 99998;
                g.save.skillPoints = 99;
                var board = Object(g, "lastdirectiveboard");
                g.save.x = board.x; g.save.y = board.y;
                int beforeXp = g.save.xp;
                Action(g, board.kind, "last_directive_publish");
                Check(g.LastDirectiveComplete && g.LastDirectiveChoice ==
                    (policy == "last_directive_open" ? 1 : 2) &&
                    g.save.coins == 999999 && g.save.shards == 99999 &&
                    g.save.skillPoints == 99 && g.save.xp > beforeXp,
                    "The real civilian oversight/shelter policy is published once with bounded final rewards and original XP");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save));
                Check(resumed.LastDirectiveComplete && resumed.save.coins == 999999 &&
                    resumed.save.shards == 99999 && resumed.save.skillPoints == 99,
                    "A published chapter ten order and capped goods survive old v2 save reload");
                resumed.Talk(board);
                Check(resumed.conversation != null &&
                    !Array.Exists(resumed.conversation.choices,
                        c => c.action == "last_directive_publish"),
                    "Both already published policies remove their claim choice and cannot duplicate permanent rewards");
            }
        }

    }
}
