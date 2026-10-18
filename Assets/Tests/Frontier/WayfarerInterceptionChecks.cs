using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void LateWayfarerBodyguardInterceptionIsRealCombat(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            g.save.flags.Add("scout_saved"); g.Enter(1); g.enemies.Clear();
            WorldObject npc = Object(g, "wayfarer1");
            Point traveler = new Point(npc.x, npc.y);
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, npc.kind, "wayfarer_defend_1");
            Enemy raider = g.enemies.Find(e => e.id >= 82000 && e.id < 82100 && e.kind == 0);
            Enemy other = g.enemies.Find(e => e != raider && e.id >= 82000 && e.id < 82100);
            Check(raider != null && other != null, "Bodyguard requires actual ambush enemies");
            other.stun = 100;
            Point raiderStart = new Point(), blockPoint = new Point();
            bool laneFound = false;
            Point[] offsets = { new Point(2, 0), new Point(-2, 0),
                new Point(0, 2), new Point(0, -2) };
            foreach (Point offset in offsets)
            {
                Point start = traveler + offset, middle = traveler + offset * .5f;
                if (!g.CanStand(start) || !g.CanStand(middle) ||
                    g.NavigationPath(start).Count == 0) continue;
                raiderStart = start; blockPoint = middle; laneFound = true; break;
            }
            Check(laneFound, "The actual roadside traveler has a reachable interception lane");
            Point distant = new Point();
            bool farFound = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !farFound; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !farFound; x++)
            {
                Point p = new Point(x + .5f, y + .5f);
                if (g.CanStand(p) && (p - traveler).Length > 8 && (p - raiderStart).Length > 8)
                { distant = p; farFound = true; }
            }
            Check(farFound, "The encounter map offers a distant player position");
            g.save.x = distant.x; g.save.y = distant.y;
            raider.x = raiderStart.x; raider.y = raiderStart.y;
            raider.timer = 0; raider.stun = 0;
            g.Tick(.02f, new Point());
            Check(raider.state == 1 && raider.targetingWayfarer,
                "Raider commits its telegraph to the traveler before the hero intervenes");
            g.save.x = blockPoint.x; g.save.y = blockPoint.y;
            g.invulnerable = 0;
            float heroHealth = g.save.health;
            Advance(g, .8f);
            Check(g.WayfarerIntegrity == 100 && g.save.health < heroHealth &&
                g.effects.Exists(e => e.kind == "number" && e.text == "护卫拦截"),
                "Last-second lane interception protects the NPC but resolves a real hit on the hero");
            g.save.x = distant.x; g.save.y = distant.y;
            raider.x = raiderStart.x; raider.y = raiderStart.y;
            raider.state = 1; raider.timer = .02f; raider.stun = 0; raider.releaseTime = 0;
            raider.target = traveler; raider.targetingWayfarer = true;
            raider.Face(traveler - raider.Position);
            g.Tick(.05f, new Point());
            Check(g.WayfarerIntegrity < 100 && g.WayfarerEncounterRoom == 1,
                "A strike not intercepted by the player still damages the traveler");
        }

        public static void WayfarerRescueChoicesPersistAsDistinctWorldReactions(Campaign data)
        {
            var supplied = Fresh(data); supplied.save.stage = 3;
            supplied.save.flags.Add("scout_saved"); supplied.Enter(1); supplied.enemies.Clear();
            WorldObject npc = Object(supplied, "wayfarer1");
            supplied.save.x = npc.x; supplied.save.y = npc.y;
            supplied.save.inventory[2] = 2;
            Action(supplied, npc.kind, "wayfarer_supply_1");
            Check(supplied.Has("wayfarer_1") && supplied.Has("wayfarer_supplied_1") &&
                !supplied.Has("wayfarer_defended_1"), "Supply rescue records its method exactly once");
            supplied.Talk(npc);
            Check(supplied.conversation.textEn.Contains("Moonherbs"),
                "Traveler acknowledges the actual supplies after a donation");
            supplied.Close(); supplied.PrepareSave();
            var loaded = new GameCore(data, CloneSave(supplied.save)); loaded.Start();
            Check(loaded.Has("wayfarer_supplied_1") && loaded.Has("wayfarer_1"),
                "Choice-specific dialogue survives the existing save format");
            loaded.Talk(npc);
            Check(loaded.conversation.textEn.Contains("Moonherbs"),
                "A loaded save keeps the same traveler reaction");

            var defended = Fresh(data); defended.save.stage = 3; defended.save.level = 10;
            defended.save.flags.Add("scout_saved"); defended.Enter(1); defended.enemies.Clear();
            WorldObject survivor = Object(defended, "wayfarer1");
            defended.save.x = survivor.x; defended.save.y = survivor.y;
            Action(defended, survivor.kind, "wayfarer_defend_1");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The real combat hit method is available");
            foreach (Enemy foe in defended.enemies.FindAll(e => e.id >= 82000 && e.id < 82100))
                hit.Invoke(defended, new object[] { foe, 9999f, false, null, null });
            Advance(defended, .35f);
            Check(defended.Has("wayfarer_1") && defended.Has("wayfarer_defended_1") &&
                !defended.Has("wayfarer_supplied_1"), "Combat rescue records a different lasting choice");
            defended.Talk(survivor);
            Check(defended.conversation.textEn.Contains("held off the raiders"),
                "Traveler remembers being defended rather than donated to");
            // Older saves already marked rescued must retain a safe generic response.
            var oldSave = CloneSave(defended.save);
            oldSave.flags.Remove("wayfarer_defended_1");
            var legacy = new GameCore(data, oldSave); legacy.Start();
            legacy.Talk(survivor);
            Check(legacy.conversation.textEn.Contains("Thanks to you"),
                "Older rescued saves remain readable without a migration");
        }
        public static void WayfarerThreeRescuesReservePhysicalBonusUntilStewFits(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("wayfarer_2");
            g.save.flags.Add("wayfarer_3");
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99; g.save.inventory[6] = 9999;
            g.Enter(1); g.enemies.Clear();
            var traveler = Object(g, "wayfarer1");
            g.save.x = traveler.x; g.save.y = traveler.y;
            g.save.inventory[2] = 5;
            Action(g, traveler.kind, "wayfarer_supply_1");
            Check(g.Has("wayfarer_1") && g.Has("wayfarer_supplied_1") &&
                !g.Has("wayfarer_complete") && g.save.inventory[2] == 3 &&
                g.save.inventory[6] == 9999 && g.save.skillPoints == 99 &&
                g.save.coins == 999999 && g.save.shards == 99999,
                "Real third rescue spends its supply, preserves achievement and bounded money, but never consumes the one-time stew reward at a full stack");
            g.Talk(traveler);
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "wayfarer_reward"),
                "Already rescued traveler actually offers collection of the deferred physical award");
            g.Close();
            Action(g, traveler.kind, "wayfarer_reward");
            Check(!g.Has("wayfarer_complete") && g.save.inventory[6] == 9999,
                "Attempting to claim while full leaves the original one-time reward pending");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("wayfarer_1") && resumed.Has("wayfarer_2") &&
                resumed.Has("wayfarer_3") && !resumed.Has("wayfarer_complete"),
                "Pending final reward and all three genuine rescues survive an original v2 save reload");
            resumed.enemies.Clear();
            resumed.save.x = traveler.x; resumed.save.y = traveler.y;
            resumed.save.inventory[6] = 9998;
            Action(resumed, traveler.kind, "wayfarer_reward");
            Check(resumed.Has("wayfarer_complete") &&
                resumed.save.inventory[6] == 9999 &&
                resumed.save.skillPoints == 99,
                "One free slot lets the original NPC award exactly one stew and bounded talent point");
            resumed.Talk(traveler);
            Check(resumed.conversation != null &&
                !Array.Exists(resumed.conversation.choices,
                    c => c.action == "wayfarer_reward"),
                "Already collected final supplies cannot be farmed by repeated dialogue");
            resumed.PrepareSave();
            var after = new GameCore(data, CloneSave(resumed.save));
            Check(after.Has("wayfarer_complete") && after.save.inventory[6] == 9999 &&
                after.save.coins == 999999 && after.save.shards == 99999,
                "Full rescue and exact bounded physical reward persist without v3 migration");
        }

    }
}
