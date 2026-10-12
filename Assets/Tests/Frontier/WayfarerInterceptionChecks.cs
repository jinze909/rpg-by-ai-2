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
    }
}
