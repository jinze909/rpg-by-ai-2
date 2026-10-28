using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Use actual in-scene cache objects and the ordinary Talk/Choose
        // pipeline. Two distinct sources, materials, handoff, save reload,
        // threat safety and non-farmable unlocks are all observable gameplay.
        public static void UnlistedReliefCircuitLinksThreeRealFieldStations(Campaign data)
        {
            for(int ending=0;ending<2;ending++)
            {
                var g=Fresh(data);
                g.save.room=9;g.enemies.Clear();
                g.save.flags.Add("unlisted_started");
                for(int act=0;act<3;act++)
                {
                    g.save.flags.Add("unlisted_a_"+act);
                    g.save.flags.Add("unlisted_b_"+act);
                    g.save.flags.Add("unlisted_solved_"+act);
                }
                WorldObject archive=Array.Find(g.Map.objects,
                    o=>o.kind=="annexinnarchive");
                Check(archive!=null,"The relief follow-up needs the real inn archive");
                g.save.x=archive.x;g.save.y=archive.y;g.Talk(archive);
                string resolution=ending==0?"unlisted_public":"unlisted_protect";
                int finalChoice=Array.FindIndex(g.conversation.choices,
                    c=>c.action==resolution);
                Check(finalChoice>=0,"The physical archive exposes its real ethical ending");
                g.Choose(finalChoice);g.Close();
                Check(g.UnlistedDone&&g.UnlistedReliefCount==0,
                    "An ending starts optional relief work, not three free stations");
                int initialTalent=g.save.skillPoints;
                // The fixture represents gathering enough field Moonherb to
                // test all three paid treatments, not free station stock.
                g.save.inventory[2]=12;
                var handler=typeof(GameCore).GetMethod("ChooseUnlisted",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                for(int act=0;act<3;act++)
                {
                    int room=new[]{0,3,5}[act];
                    g.save.room=room;g.enemies.Clear();
                    WorldObject cacheA=Array.Find(g.Map.objects,
                        o=>o.id=="annex_cache_"+room+"_a");
                    WorldObject cacheB=Array.Find(g.Map.objects,
                        o=>o.id=="annex_cache_"+room+"_b");
                    Check(cacheA!=null&&cacheB!=null&&
                        (new Point(cacheA.x-cacheB.x,cacheA.y-cacheB.y)).Length>4f,
                        "The aid circuit uses distant existing physical caches");
                    Check(g.UnlistedFieldObjectiveActive&&
                        (g.UnlistedFieldTarget-new Point(cacheA.x,cacheA.y)).Length<.01f,
                        "Live world objective initially points to the first real cache");
                    handler.Invoke(g,new object[]{"unlisted_relief_prepare"});
                    Check(!g.Has("unlisted_relief_prepared_"+act),
                        "Source-less commands cannot create a remote shipment");
                    g.save.x=cacheA.x;g.save.y=cacheA.y;g.Talk(cacheA);
                    int prepare=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_prepare");
                    Check(prepare>=0,"Original first cache offers a funded supply-packing action");
                    int wood=g.save.inventory[0],herb=g.save.inventory[2];
                    g.save.x=cacheA.x+8f;g.Choose(prepare);
                    Check(!g.Has("unlisted_relief_prepared_"+act)&&
                        g.save.inventory[0]==wood&&g.save.inventory[2]==herb,
                        "Walking away invalidates the original source and spends nothing");
                    g.save.x=cacheA.x;g.save.y=cacheA.y;g.Talk(cacheA);
                    g.Choose(Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_prepare"));
                    Check(g.Has("unlisted_relief_prepared_"+act)&&
                        g.save.inventory[0]==wood-1&&g.save.inventory[2]==herb-1&&
                        (g.UnlistedFieldTarget-new Point(cacheB.x,cacheB.y)).Length<.01f,
                        "Packing costs real resources and changes the in-world target");
                    g.save.x=cacheB.x;g.save.y=cacheB.y;g.Talk(cacheB);
                    int deliver=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_deliver");
                    Check(deliver>=0,"Distinct second cache offers a real delivery handoff");
                    int beforeCoins=g.save.coins;
                    g.Choose(deliver);
                    Check(g.Has("unlisted_relief_delivered_"+act)&&
                        g.UnlistedReliefCount==act+1&&
                        g.save.coins==beforeCoins+18+act*4&&
                        !g.UnlistedFieldObjectiveActive,
                        "Actual field handoff gives its one-time reward and ends navigation");
                    g.Talk(cacheB);
                    Check(Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_deliver")<0,
                        "A completed station cannot repeat its delivery payout");
                    int care=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_treat");
                    Check(care>=0,"The rebuilt aid station offers a durable game service");
                    g.save.health=Math.Max(1,g.save.maxHealth-60);
                    g.save.energy=40f;
                    herb=g.save.inventory[2];
                    float hp=g.save.health;
                    g.Choose(care);
                    Check(Math.Abs(g.save.health-hp-35f)<.01f&&
                        Math.Abs(g.save.energy-65f)<.01f&&
                        g.save.inventory[2]==herb-1,
                        "One herb buys 35 actual health and 25 actual stamina");
                    g.save.health=g.save.maxHealth;g.save.energy=100f;
                    g.Talk(cacheB);care=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="unlisted_relief_treat");
                    herb=g.save.inventory[2];g.Choose(care);
                    Check(g.save.inventory[2]==herb,
                        "Fully healthy, rested heroes keep their paid remedy");
                }
                Check(g.UnlistedReliefCount==3&&g.Has("unlisted_relief_network")&&
                    g.save.skillPoints>=initialTalent+1,
                    "Three real deliveries unlock the network and at least one additional talent");
                int finishedTalents=g.save.skillPoints;
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.UnlistedReliefCount==3&&
                    loaded.Has("unlisted_relief_network")&&loaded.UnlistedDone&&
                    loaded.save.skillPoints==finishedTalents,
                    "Field stations and earned talents survive unchanged v2 save reload");
                loaded.save.room=0;loaded.enemies.Clear();
                WorldObject firstB=Array.Find(loaded.Map.objects,
                    o=>o.id=="annex_cache_0_b");
                loaded.save.x=firstB.x;loaded.save.y=firstB.y;
                loaded.Talk(firstB);
                Check(Array.FindIndex(loaded.conversation.choices,
                    c=>c.action=="unlisted_relief_treat")>=0,
                    "Previously rebuilt station is still playable after reload");
            }
        }

        public static void UnlistedReliefRejectsForgedDeliveryAndLiveThreats(Campaign data)
        {
            var g=Fresh(data);g.save.room=0;g.enemies.Clear();
            g.save.flags.Add("unlisted_started");
            g.save.flags.Add("unlisted_public");
            g.save.flags.Add("unlisted_a_0");
            g.save.flags.Add("unlisted_b_0");
            g.save.flags.Add("unlisted_solved_0");
            WorldObject a=Array.Find(g.Map.objects,o=>o.id=="annex_cache_0_a");
            WorldObject b=Array.Find(g.Map.objects,o=>o.id=="annex_cache_0_b");
            var handler=typeof(GameCore).GetMethod("ChooseUnlisted",
                BindingFlags.NonPublic|BindingFlags.Instance);
            g.save.x=b.x;g.save.y=b.y;g.Talk(b);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_relief_deliver")<0,
                "A found cache cannot deliver a package that was never prepared");
            g.Close();handler.Invoke(g,new object[]{"unlisted_relief_deliver"});
            Check(!g.Has("unlisted_relief_delivered_0"),
                "Synthetic delivery cannot impersonate a source-bound cache");
            g.save.x=a.x;g.save.y=a.y;g.Talk(a);
            int packing=Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_relief_prepare");
            Check(packing>=0,"Prepare choice is available at real first cache");
            g.save.inventory[0]=0;
            int herbs=g.save.inventory[2];
            g.Choose(packing);
            Check(!g.Has("unlisted_relief_prepared_0")&&
                g.save.inventory[2]==herbs,"Insufficient wood leaves herbs and quest untouched");
            g.save.inventory[0]=2;g.Talk(a);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_relief_prepare"));
            Check(g.Has("unlisted_relief_prepared_0"),
                "Paid shipment can resume once resources are restored");
            g.save.x=b.x;g.save.y=b.y;
            Enemy threat=new Enemy(840999,0,b.x+.5f,b.y,30);
            g.enemies.Add(threat);
            g.Talk(b);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_relief_deliver");
            Check(option>=0,"Delivery exists but nearby enemies make it unsafe");
            int coins=g.save.coins;g.Choose(option);
            Check(!g.Has("unlisted_relief_delivered_0")&&g.save.coins==coins,
                "Live hostile prevents an unsafe aid handoff");
            g.enemies.Remove(threat);g.Talk(b);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_relief_deliver"));
            Check(g.Has("unlisted_relief_delivered_0"),
                "Real delivery resumes after the field is cleared");
        }
    }
}
