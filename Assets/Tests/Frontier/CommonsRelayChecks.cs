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
            g.enemies.RemoveAll(g.IsCommonsRelayEnemy);
            CommonsActivate(g, 2);
            Check(g.CommonsRelayStep == 3 &&
                g.enemies.FindAll(g.IsCommonsRelayEnemy).Count == 3,
                "Second medical circuit starts a distinct, larger live defense");
            g.save.x = third.x; g.save.y = third.y;
            g.Talk(third);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="commons_activate_3") < 0,
                "The last console cannot bypass the second physical fight");
            g.Close(); g.enemies.RemoveAll(g.IsCommonsRelayEnemy);
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
    }
}
