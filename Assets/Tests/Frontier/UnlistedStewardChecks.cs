using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static int StewardChoice(GameCore g,string action)
        {return g.conversation==null?-1:Array.FindIndex(g.conversation.choices,c=>c.action==action);}
        private static GameCore StewardCase(Campaign data,int act,bool publicEnding)
        {
            var g=Fresh(data);g.save.room=new[]{0,3,5}[act];g.enemies.Clear();
            g.save.flags.Add("unlisted_started");
            g.save.flags.Add(publicEnding?"unlisted_public":"unlisted_protected");
            for(int i=0;i<=act;i++){g.save.flags.Add("unlisted_solved_"+i);
                g.save.flags.Add("unlisted_a_"+i);g.save.flags.Add("unlisted_b_"+i);}
            g.save.flags.Add("unlisted_relief_prepared_"+act);
            g.save.flags.Add("unlisted_relief_delivered_"+act);
            g.save.inventory[0]=12;g.save.inventory[2]=12;g.save.inventory[4]=12;
            return g;
        }
        public static void ThreeCivilianSettlementsChooseRealMedicalOrTravelOutcomes(Campaign data)
        {
            for(int ending=0;ending<2;ending++)
            for(int act=0;act<3;act++)
            for(int choice=0;choice<2;choice++)
            {
                bool clinic=choice==0;int room=new[]{0,3,5}[act];
                var g=StewardCase(data,act,ending==0);
                var station=Array.Find(g.Map.objects,o=>o.id=="annex_cache_"+room+"_b");
                string command=clinic?"unlisted_steward_clinic":"unlisted_steward_trail";
                Check(station!=null&&g.UnlistedStewardCount==0&&
                    g.UnlistedStewardObjectiveActive&&
                    (g.UnlistedStewardFieldTarget-new Point(station.x,station.y)).Length<.01f&&
                    g.UnlistedStewardFieldObjective.Length>0,
                    "Restored station offers a real, reachable post-delivery world objective");
                int baseline=ending==0?6:12;
                Check(g.RidgePassageEnergyCost(room)==baseline&&
                    g.UnlistedReliefHealing(act)==35,"Old care and travel rules remain intact");
                var handler=typeof(GameCore).GetMethod("ChooseUnlisted",BindingFlags.NonPublic|BindingFlags.Instance);
                handler.Invoke(g,new object[]{command});
                Check(g.UnlistedStewardCount==0,"Source-less commands cannot fund civilian policy");
                g.save.x=station.x;g.save.y=station.y;g.Talk(station);
                Check(StewardChoice(g,"unlisted_steward_clinic")>=0&&
                    StewardChoice(g,"unlisted_steward_trail")>=0,
                    "Physical station offers both exclusive civic plans");
                g.save.x=station.x+8;g.Choose(StewardChoice(g,command));
                Check(g.UnlistedStewardCount==0,"Stale out-of-reach policy never executes");
                g.save.x=station.x;g.save.y=station.y;g.Talk(station);
                int wood=g.save.inventory[0],herb=g.save.inventory[2],gear=g.save.inventory[4];
                g.Choose(StewardChoice(g,command));
                Check(g.UnlistedStewardCount==1&&
                    !g.UnlistedStewardObjectiveActive&&
                    g.UnlistedStewardFieldObjective==""&&
                    g.save.inventory[0]==wood-(clinic?1:2)&&
                    g.save.inventory[2]==herb-(clinic?2:0)&&
                    g.save.inventory[4]==gear-(clinic?0:1),
                    "Actual source-bound vote spends the exact alternative material recipe");
                g.Talk(station);
                Check(StewardChoice(g,"unlisted_steward_clinic")<0&&
                    StewardChoice(g,"unlisted_steward_trail")<0,
                    "One completed choice permanently suppresses its alternative");
                if(clinic)
                {
                    Check(g.UnlistedReliefHealing(act)==50&&
                        g.UnlistedReliefStamina(act)==35&&
                        g.RidgePassageEnergyCost(room)==baseline,
                        "Clinic boosts paid healing without changing remote road rules");
                    g.save.health=g.save.maxHealth-65;g.save.energy=40;
                    float hp=g.save.health;herb=g.save.inventory[2];
                    g.Choose(StewardChoice(g,"unlisted_relief_treat"));
                    Check(Math.Abs(g.save.health-hp-50)<.01f&&
                        Math.Abs(g.save.energy-75)<.01f&&
                        g.save.inventory[2]==herb-1,
                        "Real aid service charges one Moonherb and uses upgraded healing");
                }
                else
                {
                    Check(g.UnlistedReliefHealing(act)==35&&
                        g.RidgePassageEnergyCost(room)==baseline-4,
                        "Road investment reduces local travel cost, never medical output");
                    g.save.flags.Add("ridge_seen_"+room+"_0");
                    g.save.flags.Add("ridge_seen_"+room+"_1");
                    var marker=Array.Find(g.Map.objects,o=>o.id=="ridge_route_"+room+"_0");
                    Check(marker!=null,"Actual ridge marker exists in production map");
                    g.save.x=marker.x;g.save.y=marker.y;g.save.energy=100;
                    g.Talk(marker);
                    int travel=StewardChoice(g,"ridge_travel");
                    Check(travel>=0&&g.conversation.textEn.Contains((baseline-4)+" energy"),
                        "Displayed ridge price agrees with purchased local route");
                    g.Choose(travel);
                    Check(Math.Abs(g.save.energy-100+baseline-4)<.01f,
                        "Existing ridge traversal actually debits the upgraded stamina cost");
                }
                g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.UnlistedStewardCount==1&&
                    restored.UnlistedReliefHealing(act)==(clinic?50:35)&&
                    restored.RidgePassageEnergyCost(room)==(clinic?baseline:baseline-4)&&
                    restored.RidgePassageEnergyCost(7)==baseline,
                    "Local permanent outcome persists in old v2 schema and cannot affect other regions");
            }
        }
        public static void StewardshipRequiresMaterialsAndSafetyAndPaysOneNetworkReceipt(Campaign data)
        {
            var g=StewardCase(data,0,true);
            var station=Array.Find(g.Map.objects,o=>o.id=="annex_cache_0_b");
            g.save.x=station.x;g.save.y=station.y;g.Talk(station);
            g.save.inventory[2]=1;int wood=g.save.inventory[0];
            g.Choose(StewardChoice(g,"unlisted_steward_clinic"));
            Check(g.UnlistedStewardCount==0&&g.save.inventory[0]==wood,
                "An unaffordable clinic never spends other materials");
            g.Talk(station);var threat=new Enemy(845122,0,station.x+.6f,station.y,40);
            g.enemies.Add(threat);g.Choose(StewardChoice(g,"unlisted_steward_trail"));
            Check(g.UnlistedStewardCount==0&&g.save.inventory[0]==wood,
                "Actual nearby hostile prevents vulnerable construction");
            g.enemies.Remove(threat);g.Talk(station);
            g.Choose(StewardChoice(g,"unlisted_steward_trail"));
            Check(g.UnlistedStewardCount==1&&g.RidgePassageEnergyCost(0)==2,
                "Repairs activate after the real field is safe");
            g.save.flags.Add("unlisted_relief_clinic_1");
            g.save.flags.Add("unlisted_a_2");g.save.flags.Add("unlisted_b_2");
            g.save.flags.Add("unlisted_solved_2");
            g.save.flags.Add("unlisted_relief_delivered_2");
            g.save.room=5;
            station=Array.Find(g.Map.objects,o=>o.id=="annex_cache_5_b");
            g.save.x=station.x;g.save.y=station.y;
            // Each policy consumes actual materials. This test-local supply
            // represents gathering herbs between the separate regions.
            g.save.inventory[2]=6;
            Check(g.UnlistedStewardCount==2,"Two region choices are already recorded");
            int shards=g.save.shards,moments=g.save.moments.Count;
            g.Talk(station);g.Choose(StewardChoice(g,"unlisted_steward_clinic"));
            Check(g.UnlistedStewardCount==3&&g.Has("unlisted_relief_stewards_all")&&
                g.save.shards==shards+1&&g.save.moments.Count==moments+2,
                "Three unique actual local choices conclude in one shared civilian receipt");
            g.Talk(station);
            Check(StewardChoice(g,"unlisted_steward_clinic")<0&&
                StewardChoice(g,"unlisted_steward_trail")<0,
                "Shared public closure has no repeatable payout");
        }
    }
}
