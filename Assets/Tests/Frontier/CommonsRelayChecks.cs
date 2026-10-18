using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore CommonsReady(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3; g.save.level = 12;
            g.save.flags.Add("boss_4");
            g.save.x = 13.5f; g.save.y = 10.5f;
            Action(g, "commonswitness", "commons_accept");
            Check(g.Has("commons_relay_started") &&
                g.save.trackedQuest == "commons_relay",
                "Ayla's actual dialogue accepts and tracks the new commission");
            g.Enter(4); g.enemies.Clear();
            return g;
        }
        private static void CommonsActivate(GameCore g, int step)
        {
            WorldObject obj = Object(g, "commonsrelay" + step);
            g.save.x = obj.x; g.save.y = obj.y;
            Action(g, obj.kind, "commons_activate_" + step);
        }
        private static void DefeatCommonsWave(GameCore g)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Authentic combat damage hook exists");
            foreach (Enemy e in g.enemies.FindAll(g.IsCommonsRelayEnemy))
                hit.Invoke(g, new object[] { e, 99999f, false, null, null });
        }
        public static void CitadelCommonsThreeStageCombatAndPublicKey(Campaign data)
        {
            var g = CommonsReady(data);
            Check(!g.GetQuest("commons_relay").locked, "New quest appears in the real tracked journal");
            for (int i = 1; i <= 3; i++)
            {
                WorldObject o = Object(g, "commonsrelay" + i);
                Check(g.CanStand(new Point(o.x, o.y)),
                    "Citadel circuit " + i + " is physically accessible on the authored map");
            }
            int coins = g.save.coins, shards = g.save.shards;
            CommonsActivate(g, 1);
            Check(g.CommonsRelayRoom == 4 && g.CommonsRelayStep == 2 &&
                g.CommonsRelayTime > 109 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 2,
                "First food circuit starts a live timed fight with two security sentinels");
            foreach (Enemy enemy in g.enemies)
                Check(g.CanStand(enemy.Position), "Authored enemies spawn at walkable feet");
            Check(g.save.coins == coins && !g.Has("commons_relay_sealed"),
                "No first-clear loot is awarded before the real security fight is finished");
            WorldObject third = Object(g, "commonsrelay3");
            g.save.x = third.x; g.save.y = third.y;
            g.Talk(third);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_3") < 0,
                "The evacuation lamps cannot be activated out of sequence");
            g.Close();
            WorldObject second = Object(g, "commonsrelay2");
            g.save.x = second.x; g.save.y = second.y; g.Talk(second);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_2") < 0,
                "Second console is locked until actual security is defeated");
            g.Close();
            DefeatCommonsWave(g);
            CommonsActivate(g, 2);
            Check(g.CommonsRelayStep == 3 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 3,
                "Second medical circuit starts a distinct, larger live defense");
            g.save.x = third.x; g.save.y = third.y;
            g.Talk(third);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_3") < 0,
                "The last console cannot bypass the second physical fight");
            g.Close(); DefeatCommonsWave(g);
            CommonsActivate(g, 3);
            Check(g.Has("commons_relay_sealed") && g.CommonsRelayRoom == 0 &&
                g.save.coins == coins + 60 && g.save.shards == shards + 3,
                "Three stages award exactly one secure-circuit bonus");
            g.save.x = third.x; g.save.y = third.y;
            g.Talk(third);
            Check(g.conversation != null &&
                Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_3") < 0 &&
                g.save.coins == coins + 60 && g.save.shards == shards + 3,
                "Completed consoles offer no second activation or duplicate reward");
            g.Close();
            g.Enter(0); g.save.x = 13.5f; g.save.y = 10.5f;
            int talent = g.save.skillPoints;
            Action(g, "commonswitness", "commons_publish");
            Check(g.Has("commons_relay_complete") &&
                g.GetQuest("commons_relay").complete &&
                g.save.coins == coins + 140 &&
                g.save.skillPoints == talent + 1,
                "Ayla publishes the civilian key and awards one permanent talent");
            g.Talk(Object(g,"commonswitness"));
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_publish") < 0,
                "The public key cannot be published for another reward");
        }
        public static void CitadelCommonsPermanentEnergyNetworkPersistsWithoutNewSaveFields(Campaign data)
        {
            var before=Fresh(data);
            var completed=Fresh(data);
            before.save.energy=40;completed.save.energy=40;
            completed.save.flags.Add("commons_relay_complete");
            before.Tick(.05f,new Point());completed.Tick(.05f,new Point());
            Check(Math.Abs(completed.save.energy-before.save.energy-.1f)<.001f,
                "The new public circuit actually accelerates energy regeneration by two per second");
            completed.PrepareSave();
            var loaded=new GameCore(data,completed.save);loaded.Start();
            Check(loaded.Has("commons_relay_complete"),
                "Existing version-two flag saves retain the public citizen-network reward");
            float begin=loaded.save.energy;
            loaded.Tick(.05f,new Point());
            Check(loaded.save.energy>begin+.75f,
                "The permanent live energy boost still applies after loading the same old-schema slot");
        }
        public static void CitadelCommonsTimedSecurityCannotGrantFreeCompletion(Campaign data)
        {
            var g=CommonsReady(data);
            CommonsActivate(g,1);
            g.enemies.RemoveAll(g.IsCommonsRelayEnemy);
            int coins=g.save.coins,shards=g.save.shards;
            for(int i=0;i<2210 && g.CommonsRelayRoom!=0;i++)g.Tick(.05f,new Point());
            Check(g.CommonsRelayRoom==0 && !g.enemies.Exists(g.IsCommonsRelayEnemy) &&
                g.Has("commons_relay_started") &&
                !g.Has("commons_relay_sealed") &&
                g.save.coins==coins && g.save.shards==shards,
                "Actually expiring the timed defense never fabricates success or loses accepted story");
            CommonsActivate(g,1);
            Check(g.CommonsRelayRoom==4 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count==2,
                "After a genuine time expiry the physical first wave can be retried from its console");
        }

        public static void CitadelCommonsSentinelsRequireRealPlayerCombatAndCannotFarm(Campaign data)
        {
            var g = CommonsReady(data);
            CommonsActivate(g,1);
            Enemy sentinel=g.enemies.Find(g.IsCommonsRelayEnemy);
            Check(sentinel != null,"The new episode spawns a real attackable world enemy");
            // Retain every authored defender; the second sentinel is
            // stunned during the first real sword kill, not silently deleted.
            g.enemies.RemoveAll(e => !g.IsCommonsRelayEnemy(e));
            foreach (Enemy e in g.enemies) if (e != sentinel)
            { e.stun = 20; e.timer = 20; }
            int coins=g.save.coins,xp=g.save.xp,kills=g.save.kills[4];
            int[] inventory=(int[])g.save.inventory.Clone();
            // An actual hero melee hit must damage this challenge enemy,
            // then kill it through the same Contact/Hit pipeline as normal AI.
            sentinel.stun=10;sentinel.timer=10;
            g.save.x=sentinel.x-1.05f;g.save.y=sentinel.y;
            g.invulnerable=12;
            float initialHP=sentinel.hp;
            for(int turn=0;turn<12 && g.enemies.Contains(sentinel);turn++)
            {
                g.save.energy=100;
                g.Attack(new Point(1,0));
                for(int frame=0;frame<12;frame++)g.Tick(.05f,new Point());
            }
            Check(sentinel.hp<initialHP && !g.enemies.Contains(sentinel),
                "Real main-character sword attacks defeat the scripted sentinel, not test-only enemy deletion");
            Check(g.save.coins==coins && g.save.xp==xp && g.save.kills[4]==kills,
                "New defense sentinels never farm normal boss rewards, XP, bounty kills or story gates");
            for(int i=0;i<inventory.Length;i++)
                Check(g.save.inventory[i]==inventory[i],
                    "Scripted sentinels cannot mint ordinary inventory loot");
            // The first true melee kill alone is not sufficient to clear
            // the two-sentinel checkpoint. Defeat the remaining defender
            // through its same original lethal combat path.
            DefeatCommonsWave(g);
            g.save.x=Object(g,"commonsrelay2").x;g.save.y=Object(g,"commonsrelay2").y;
            CommonsActivate(g,2);
            Check(g.enemies.FindAll(g.IsCommonsRelayEnemy).Count==3,
                "Winning via physical melee opens the true second-stage defensive wave");
        }

        public static void CitadelCommonsFloorSurgesWarnDodgeAndCleanUp(Campaign data)
        {
            var g = CommonsReady(data);
            CommonsActivate(g,1); DefeatCommonsWave(g);
            CommonsActivate(g,2);
            Check(g.CommonsRelayStep == 3 && g.enemies.Exists(g.IsCommonsRelayEnemy),
                "Medical-line reconnection enters the unique second-floor security phase");
            // Remove random contact distractions: retain a genuine sentry but
            // keep it stunned while the separate telegraphed floor system runs.
            foreach (Enemy e in g.enemies)
            { e.stun = 12; e.timer = 100; e.state = 0; }
            g.invulnerable = 12;
            Point target = g.Position;
            for (int i=0;i<70;i++) g.Tick(.05f,new Point());
            Check(g.hazards.Exists(x => x.commonsRelayOwned && x.timer > 0 &&
                (x.position - target).Length < .5f),
                "The medical relay creates a visible delayed hazard on the player's real feet");
            Hazard warning = g.hazards.Find(x => x.commonsRelayOwned);
            Check(warning != null && warning.radius > 1 && warning.timer > 0,
                "The surge remains avoidable before the normal collision release");
            Point[] directions = { new Point(-2.6f,0),new Point(0,2.6f),
                new Point(0,-2.6f),new Point(2.6f,0) };
            bool escaped = false;
            foreach (Point d in directions)
            {
                Point away=target+d;
                if (!g.CanStand(away)) continue;
                bool outside = true;
                foreach (Hazard hz in g.hazards)
                    if (hz.commonsRelayOwned && (hz.position-away).Length < hz.radius+.15f)
                        outside=false;
                if (!outside)continue;
                g.save.x=away.x;g.save.y=away.y;escaped=true;break;
            }
            Check(escaped,"The actual citadel terrain offers a reachable escape from the double surge");
            float health=g.save.health;g.invulnerable=0;
            for(int i=0;i<38;i++)g.Tick(.05f,new Point());
            Check(g.save.health >= health-.01f,
                "Escaping the warned footprint avoids live hazard damage without fake invulnerability");
            DefeatCommonsWave(g);
            g.Tick(.05f,new Point());
            Check(!g.hazards.Exists(x=>x.commonsRelayOwned),
                "Defeating the last defender retires pending circuit hazards immediately");
            var ordinary = new Hazard { position=g.Position, timer=4,
                radius=.5f, damage=1, kind="rift" };
            g.hazards.Add(ordinary);
            CommonsActivate(g,3);
            Check(g.CommonsRelayRoom==0 && g.hazards.Contains(ordinary),
                "Sealing the public circuit preserves all unrelated world hazard state");
        }

        public static void CitadelCommonsChronicleRevealsTrueProgress(Campaign data)
        {
            var g=Fresh(data);
            WorldObject chronicle=Object(g,"worldchronicle");
            g.Talk(chronicle);
            int section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            Check(section>=0,"Frontier Chronicle offers its original aftermath chapter");
            g.Choose(section);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_commons")<0,
                "Ayla's testimony remains hidden until the player actually accepts the commission");
            g.Close();
            g.save.flags.Add("commons_relay_started");
            g.Talk(chronicle);
            section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            g.Choose(section);
            int page=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_commons");
            Check(page>=0,"Accepting the new quest publishes a readable bilingual story chapter");
            g.Choose(page);
            Check(g.conversation.text.Contains("三道公共信标仍在") &&
                !g.conversation.text.Contains("艾拉已经将访问密钥公开"),
                "Reading never invents security victories or public oversight before real gameplay");
            g.Close();
            g.save.flags.Add("commons_relay_sealed"); g.save.english=true;
            g.Talk(chronicle);section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            g.Choose(section);page=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_commons");
            g.Choose(page);
            Check(g.conversation.textEn.Contains("You crossed real security lines") &&
                g.conversation.textEn.Contains("awaits a public signature"),
                "Recovered circuits update the English page without fabricating Ayla's final handoff");
            g.Close();
            g.save.flags.Add("commons_relay_complete");
            g.Talk(chronicle);section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            g.Choose(section);page=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_commons");
            g.Choose(page);
            Check(g.conversation.textEn.Contains("published the access key"),
                "Ayla's true public record only appears after the last physical town conversation");
        }

        public static void CitadelCommonsStoryLocksAndWithdrawal(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            g.save.x = 13.5f; g.save.y = 10.5f;
            g.Talk(Object(g,"commonswitness"));
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_accept") < 0,
                "The optional post-citadel story cannot begin before the guardian is defeated");
            g.Close(); g.save.flags.Add("boss_4");
            Action(g, "commonswitness", "commons_accept");
            g.Enter(4); g.enemies.Clear();
            WorldObject first = Object(g,"commonsrelay1");
            g.save.x = 3; g.save.y = 14;
            g.Talk(first);
            int remote = Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_1");
            Check(remote >= 0, "The authored first circuit offers the actual player action");
            g.Choose(remote);
            Check(g.CommonsRelayRoom == 0, "Stored distant dialogue cannot activate real world defense");
            CommonsActivate(g,1);
            int coins = g.save.coins;
            g.Enter(0);
            Check(g.save.room == 4 && g.CommonsRelayRoom == 4,
                "Travel cannot silently discard an active citadel confrontation");
            g.Enter(4);
            Check(g.save.room == 4 && g.CommonsRelayRoom == 4 &&
                g.enemies.Exists(g.IsCommonsRelayEnemy),
                "Re-entering the same map cannot reset its live wave to avoid security");
            WorldObject second = Object(g,"commonsrelay2");
            g.save.x = second.x;g.save.y=second.y;
            Action(g,"commonsrelay2","commons_withdraw");
            Check(g.CommonsRelayRoom == 0 && !g.enemies.Exists(g.IsCommonsRelayEnemy) &&
                g.save.coins == coins && g.Has("commons_relay_started") &&
                !g.Has("commons_relay_sealed"),
                "Explicit withdrawal removes only encounter state and never invents victory");
            g.Enter(0);
            Check(g.save.room == 0,"Normal travel resumes after voluntary withdrawal");
            g.Enter(4);g.enemies.Clear();CommonsActivate(g,1);
            Check(g.CommonsRelayRoom == 4 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 2,
                "Restarted new content runs a fresh real fight, with no saved free enemy kills");
        }
        public static void MissingCommonsSentinelCannotSkipRealSecurityCheckpoint(Campaign data)
        {
            var g = CommonsReady(data);
            CommonsActivate(g, 1);
            Check(g.CommonsRelayStep == 2 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 2,
                "Original food security spawns exactly two real enemies");
            int coins = g.save.coins, talent = g.save.skillPoints;
            g.enemies.RemoveAll(g.IsCommonsRelayEnemy);
            CommonsActivate(g, 2);
            Check(g.CommonsRelayRoom == 0 && g.CommonsRelayStep == 0 &&
                !g.Has("commons_relay_sealed") &&
                g.save.coins == coins && g.save.skillPoints == talent,
                "A missing enemy list without registered combat does not authorize the medical circuit or its rewards");

            CommonsActivate(g, 1);
            Check(g.CommonsRelayRoom == 4 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 2,
                "An invalid wave offers a genuine fresh combat retry");
            DefeatCommonsWave(g);
            CommonsActivate(g, 2);
            Check(g.CommonsRelayStep == 3 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 3,
                "Real first-wave lethal strikes unlock the three-sentinel second wave");
            g.enemies.RemoveAll(g.IsCommonsRelayEnemy);
            CommonsActivate(g, 3);
            Check(g.CommonsRelayRoom == 0 && !g.Has("commons_relay_sealed") &&
                g.save.coins == coins,
                "Second-wave enemy disappearance cannot counterfeit a sealed civic circuit");
            CommonsActivate(g, 1);
            DefeatCommonsWave(g);
            CommonsActivate(g, 2);
            DefeatCommonsWave(g);
            CommonsActivate(g, 3);
            Check(g.Has("commons_relay_sealed") &&
                g.CommonsRelayRoom == 0 && g.save.coins == coins + 60,
                "Two fully verified waves still permit original rewarded civic completion");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("commons_relay_sealed") &&
                resumed.save.coins == g.save.coins,
                "Verified public security milestone survives legacy v2 save reload");
        }

    }
}
