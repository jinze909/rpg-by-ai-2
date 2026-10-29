using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void RealVigilChoice(GameCore g,string action)
        {
            Check(g.mode=="dialog"&&g.conversation!=null,
                "Emergency maintenance uses authenticated physical dialogue");
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            Check(i>=0,"Missing live emergency choice "+action);
            g.Choose(i);
        }
        public static void EightRealRegionalEngineeringEmergenciesCompleteDistinctLiveCircuits(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Real combat damage must exist before checking emergency raiders");
            int[,] expected={{2,1},{1,2},{2,1},{2,1},
                {1,2},{2,1},{1,2},{2,1}};
            for(int region=0;region<8;region++)
            {
                GameCore g=new GameCore(data);
                g.Close();g.save.room=region;g.enemies.Clear();
                g.mode="world";
                g.save.flags.Add("biome_field_done_"+region);
                for(int station=0;station<3;station++)
                    g.save.flags.Add("regional_machine_stage_"+region+"_"+station);
                g.save.flags.Add("regional_machine_done_"+region);
                g.save.inventory[0]=10;
                g.save.inventory[2]=10;
                g.save.inventory[4]=10;
                var objects=new WorldObject[3];
                for(int station=0;station<3;station++)
                {
                    string id="regional_machine_"+region+"_"+station;
                    objects[station]=Array.Find(g.Map.objects,o=>o.id==id);
                    Check(objects[station]!=null&&
                        g.CanStand(new Point(objects[station].x,objects[station].y)),
                        "Emergency objectives must be genuine physically traversable public devices");
                }
                WorldObject origin=objects[0];
                int initialCoin=g.save.coins;
                g.save.x=origin.x;g.save.y=origin.y;
                g.Talk(origin);RealVigilChoice(g,"regional_vigil_begin");
                bool combat=region==1||region==3||region==4;
                Check(g.RegionalVigilActive&&g.RegionalVigilStage==1&&
                    g.RegionalVigilNextStation==expected[region,0]&&
                    g.RegionalVigilIntegrity==3&&g.RegionalVigilSeconds==360f&&
                    g.RegionalVigilEnemyCount==(combat?2:0),
                    "Authentic region "+region+" must begin its distinct timed and combat-gated patrol");
                int beforeEnemies=g.enemies.Count;
                if(combat)
                {
                    Check(beforeEnemies==2,
                        "Three hostile patrol districts must create exactly two real placed raiders");
                    WorldObject first=objects[expected[region,0]];
                    var wave=new List<Enemy>(g.enemies);
                    foreach(Enemy e in wave)
                        Check((e.Position-new Point(first.x,first.y)).Length<12f,
                            "Real attacker approaches originate beside the actual remote engineering station");
                    g.save.x=first.x;g.save.y=first.y;
                    g.Talk(first);RealVigilChoice(g,"regional_vigil_service");
                    Check(g.RegionalVigilStage==1,
                        "An un-defeated real enemy roster cannot be bypassed by the inspection action");
                    g.Close();
                    foreach(Enemy e in wave)
                        hit.Invoke(g,new object[]{e,10000f,false,null,null});
                    g.invulnerable=1000f;
                    for(int k=0;k<5;k++)g.Tick(.05f,new Point());
                    Check(g.RegionalVigilEnemyCount==0,
                        "Actual real lethal callbacks, not missing actors, certify safety");
                }
                for(int step=0;step<2;step++)
                {
                    int targetIndex=expected[region,step];
                    WorldObject site=objects[targetIndex];
                    Check(g.RegionalVigilNextStation==targetIndex,
                        "Every emergency stage aims at a real ordered far station");
                    g.save.x=site.x;g.save.y=site.y;
                    int material=region==0||region==2||region==5?0:
                        region==6||region==7?2:4;
                    int before=g.save.inventory[material];
                    g.Talk(site);RealVigilChoice(g,"regional_vigil_service");
                    Check(g.RegionalVigilStage==step+2&&
                        g.save.inventory[material]==before-1,
                        "Only genuine physical field service consumes one region-specific real supply");
                }
                int priorShards=g.save.shards;
                Check(g.RegionalVigilNextStation==0,
                    "A completed field circuit must demand physical source return");
                g.save.x=origin.x;g.save.y=origin.y;
                g.Talk(origin);RealVigilChoice(g,"regional_vigil_report");
                Check(!g.RegionalVigilActive&&g.RegionalVigilWins==1&&
                    g.RegionalVigilDone(region)&&
                    g.save.coins==initialCoin+42&&
                    g.save.shards==priorShards+1,
                    "Authenticated distant inspections and return pay one persistent public receipt");
                g.Talk(origin);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="regional_vigil_begin")<0,
                    "Public first-clear emergency cannot be farmed at the same source");
                g.PrepareSave();
                GameCore reloaded=new GameCore(data,g.save);
                Check(reloaded.save.version==2&&
                    reloaded.RegionalVigilDone(region)&&
                    !reloaded.RegionalVigilActive,
                    "Legacy SaveData v2 retains only paid first-clear receipts, not temporary fighters");
            }
        }
        public static void UrgentEngineeringResponseHasRealTwoHitRiskAndBetterOnceOnlyReceipt(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.Close();g.mode="world";g.save.room=0;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_0");
            g.save.flags.Add("regional_machine_done_0");
            for(int s=0;s<3;s++)g.save.flags.Add("regional_machine_stage_0_"+s);
            WorldObject start=Array.Find(g.Map.objects,o=>o.id=="regional_machine_0_0");
            WorldObject first=Array.Find(g.Map.objects,o=>o.id=="regional_machine_0_2");
            WorldObject second=Array.Find(g.Map.objects,o=>o.id=="regional_machine_0_1");
            g.save.inventory[0]=10;
            g.save.x=start.x;g.save.y=start.y;
            g.Talk(start);
            int regular=Array.FindIndex(g.conversation.choices,
                c=>c.action=="regional_vigil_begin");
            int urgent=Array.FindIndex(g.conversation.choices,
                c=>c.action=="regional_vigil_rush");
            Check(regular>=0&&urgent>=0,
                "Original source offers two independent authentic safe/urgent risk strategies");
            g.Choose(urgent);
            Check(g.RegionalVigilActive&&g.RegionalVigilRushed&&
                g.RegionalVigilSeconds==240f&&g.RegionalVigilIntegrity==2,
                "Urgent player choice enforces a genuinely shorter clock and reduced cargo endurance");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"Real urgent cargo uses actual hero HP contact");
            g.invulnerable=0f;
            hurt.Invoke(g,new object[]{5f,"environment",null});
            Check(g.RegionalVigilActive&&g.RegionalVigilIntegrity==1,
                "One real post-armor hit consumes exactly one urgent cargo integrity");
            g.invulnerable=0f;
            hurt.Invoke(g,new object[]{5f,"environment",null});
            Check(!g.RegionalVigilActive&&!g.RegionalVigilDone(0)&&
                !g.Has("regional_vigil_urgent_0"),
                "Two actual hero HP contacts invalidate the urgent rescue without awards");
            g.enemies.Clear();g.mode="world";
            g.save.x=start.x;g.save.y=start.y;g.save.health=100;
            g.Talk(start);RealVigilChoice(g,"regional_vigil_rush");
            Check(g.RegionalVigilRushed&&g.RegionalVigilIntegrity==2,
                "Failed urgent route may be retried by a true source dialogue");
            int coins=g.save.coins,shards=g.save.shards;
            g.save.x=first.x;g.save.y=first.y;
            g.Talk(first);RealVigilChoice(g,"regional_vigil_service");
            g.save.x=second.x;g.save.y=second.y;
            g.Talk(second);RealVigilChoice(g,"regional_vigil_service");
            Check(g.RegionalVigilStage==3&&g.RegionalVigilRushed,
                "Both physically serviced stations preserve chosen risk through final return");
            g.save.x=start.x;g.save.y=start.y;
            g.Talk(start);RealVigilChoice(g,"regional_vigil_report");
            Check(!g.RegionalVigilActive&&g.RegionalVigilDone(0)&&
                g.Has("regional_vigil_urgent_0")&&
                g.save.coins==coins+62&&g.save.shards==shards+2,
                "Only actually walked high-risk response yields larger first-clear reward");
            g.Talk(start);
            Check(Array.FindIndex(g.conversation.choices,c=>
                c.action=="regional_vigil_rush"||
                c.action=="regional_vigil_begin")<0,
                "Fast and safe pathways share one nonfarmable civil receipt");
            g.PrepareSave();
            GameCore loaded=new GameCore(data,g.save);
            Check(loaded.save.version==2&&loaded.RegionalVigilDone(0)&&
                loaded.Has("regional_vigil_urgent_0")&&
                !loaded.RegionalVigilActive&&!loaded.RegionalVigilRushed,
                "V2 saves retain earned urgent accomplishment but not a stale live risky session");
        }

        public static void RegionalEngineeringPatrolAbortsRetireOnlyOwnedActorsAndNoRewards(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.Close();g.save.room=1;g.mode="world";g.enemies.Clear();
            g.save.flags.Add("biome_field_done_1");
            g.save.flags.Add("regional_machine_done_1");
            for(int s=0;s<3;s++)g.save.flags.Add("regional_machine_stage_1_"+s);
            WorldObject origin=Array.Find(g.Map.objects,o=>o.id=="regional_machine_1_0");
            g.save.x=origin.x;g.save.y=origin.y;
            var direct=typeof(GameCore).GetMethod("ChooseRegionalVigil",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(direct!=null,"Real field engineering watch action exists");
            direct.Invoke(g,new object[]{"regional_vigil_begin"});
            Check(!g.RegionalVigilActive&&g.enemies.Count==0,
                "Forged emergency commands without a reachable source cannot generate combat");
            g.Talk(origin);RealVigilChoice(g,"regional_vigil_begin");
            Check(g.RegionalVigilActive&&g.RegionalVigilEnemyCount==2,
                "Actual source starts a unique real raider roster");
            int coins=g.save.coins;
            g.Enter(2);
            Check(g.save.room==1&&g.RegionalVigilActive,
                "Protected emergency watch blocks changing maps or using portals");
            Enemy removed=g.enemies[0];
            g.enemies.Remove(removed);
            for(int k=0;k<5;k++)g.Tick(.05f,new Point());
            Check(!g.RegionalVigilActive&&!g.RegionalVigilDone(1)&&
                g.save.coins==coins,
                "Missing real raider without lethal combat invalidates the entire watch");
            g.enemies.Clear();g.save.x=origin.x;g.save.y=origin.y;
            g.Talk(origin);RealVigilChoice(g,"regional_vigil_begin");
            Check(g.RegionalVigilActive,"Failed watch can be legitimately retried");
            var arrow=new Projectile{
                hostileShooter=g.enemies[0],position=g.enemies[0].Position,
                velocity=new Point(1,0),life=2f,damage=6f,visual="enemyArrow"};
            var friendly=new Projectile{
                friendly=true,position=g.Position,
                velocity=new Point(1,0),life=2f,visual="friendly"};
            g.projectiles.Add(arrow);g.projectiles.Add(friendly);
            g.save.x=origin.x;g.save.y=origin.y;
            g.Talk(origin);RealVigilChoice(g,"regional_vigil_abort");
            Check(!g.RegionalVigilActive&&g.enemies.Count==0&&
                !g.projectiles.Contains(arrow)&&g.projectiles.Contains(friendly)&&
                g.save.coins==coins&&!g.RegionalVigilDone(1),
                "Withdrawal cleans only exact owned attackers and arrows, without harming player shots");
        }
        public static void RegionalWeatherEmergencyHasRealWorldPressureOnlyAndEnhancedShelter(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.Close();g.save.room=6;g.mode="world";g.enemies.Clear();
            g.save.flags.Add("biome_field_done_6");
            g.save.flags.Add("regional_machine_done_6");
            for(int s=0;s<3;s++)g.save.flags.Add("regional_machine_stage_6_"+s);
            WorldObject origin=Array.Find(g.Map.objects,o=>o.id=="regional_machine_6_0");
            g.save.x=origin.x;g.save.y=origin.y;g.save.heat=0f;
            g.Talk(origin);RealVigilChoice(g,"regional_vigil_begin");
            float before=g.RegionalVigilSeconds;
            g.mode="inventory";g.Tick(.05f,new Point());
            Check(g.RegionalVigilSeconds==before&&g.save.heat==0f,
                "Inventory/pause time cannot secretly burn the hero or expire a live weather clock");
            g.mode="world";g.invulnerable=1000f;
            g.Tick(.05f,new Point());
            Check(g.RegionalVigilSeconds<before&&g.save.heat>0f,
                "Dune emergency truly increases climate exposure only in running game world");
            g.save.x=origin.x;g.save.y=origin.y;
            g.Talk(origin);RealVigilChoice(g,"regional_vigil_abort");
            Check(!g.RegionalVigilActive,"True physical origin allows aborting a climate emergency");
            g.save.health=40;g.save.energy=20;g.save.heat=40;
            g.save.flags.Add("regional_vigil_done_6");
            var shelter=typeof(GameCore).GetMethod("TickRegionalMachineShelters",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(shelter!=null,"Existing live shelter system applies permanently increased healing");
            g.mode="world";g.enemies.Clear();
            shelter.Invoke(g,new object[]{1f});
            Check(g.save.health>42f&&g.save.energy>24f&&
                g.save.heat<40f,
                "Actual signed engineering watch increases existing safe machine healing and energy recovery");
        }
    }
}
