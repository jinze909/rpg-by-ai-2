using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void ChooseFieldOption(GameCore g,string choice)
        {
            Check(g.mode=="dialog"&&g.conversation!=null,
                "Real field supply choice must be shown in an actual dialogue");
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action==choice);
            Check(index>=0,"Missing authentic field supply dialogue choice "+choice);
            g.Choose(index);
        }
        public static void CivilianSupplyChoicesChangeActualTravelAndCampRest(Campaign data)
        {
            GameCore road=new GameCore(data);
            road.Close();road.enemies.Clear();
            WorldObject start=Array.Find(data.maps[0].objects,
                o=>o.id=="biomeSurvey0");
            WorldObject finish=Array.Find(data.maps[0].objects,
                o=>o.id=="biomeAnswer0");
            Check(start!=null&&finish!=null,
                "Paid road policy must reuse the two real field monuments");
            road.save.flags.Add("biome_field_done_0");
            road.save.flags.Add("biome_supply_road_0");
            road.save.x=start.x;road.save.y=start.y;
            road.save.coins=65;road.save.energy=80f;
            road.Talk(start);
            int beforeCoins=road.save.coins;
            float beforeEnergy=road.save.energy;
            // The world control, not a fabricated direct method invocation,
            // must carry its origin and its new real costs.
            ChooseFieldOption(road,"biome_relay");
            Check((road.Position-new Point(finish.x,finish.y)).Length<2f&&
                road.save.coins==beforeCoins-1&&
                road.save.energy==beforeEnergy-6f,
                "Real paid crossing uses the routeworks 1-coin / 6-energy fare, not just a changed label");
            GameCore stale=new GameCore(data);
            stale.Close();stale.enemies.Clear();
            stale.save.flags.Add("biome_field_done_0");
            stale.save.flags.Add("biome_supply_road_0");
            stale.save.x=start.x;stale.save.y=start.y;
            stale.Talk(start);
            beforeCoins=stale.save.coins;beforeEnergy=stale.save.energy;
            stale.save.x=finish.x;stale.save.y=finish.y;
            ChooseFieldOption(stale,"biome_relay");
            Check(stale.save.coins==beforeCoins&&
                stale.save.energy==beforeEnergy&&
                (stale.Position-new Point(finish.x,finish.y)).Length<.001f,
                "Leaving the spoken source rejects stale transfers without paying or teleporting");
            WorldObject camp=Array.Find(data.maps[0].objects,
                o=>o.id=="annex_camp_0");
            Check(camp!=null,"Medicine must change the original playable public campsite");
            GameCore medical=new GameCore(data);
            medical.Close();medical.enemies.Clear();
            medical.save.flags.Add("biome_supply_medicine_0");
            medical.save.x=camp.x;medical.save.y=camp.y;
            medical.save.health=10f;medical.save.energy=20f;
            medical.save.inventory[0]=5;
            medical.Talk(camp);ChooseFieldOption(medical,"annex_rest");
            Check(medical.save.health==70f&&medical.save.energy==100f&&
                medical.save.inventory[0]==4,
                "The actual existing paid camp action heals +60 with medicine, versus its original +40");
            GameCore unchanged=new GameCore(data);
            unchanged.Close();unchanged.enemies.Clear();
            unchanged.save.x=camp.x;unchanged.save.y=camp.y;
            unchanged.save.health=10f;unchanged.save.energy=20f;
            unchanged.save.inventory[0]=5;
            unchanged.Talk(camp);ChooseFieldOption(unchanged,"annex_rest");
            Check(unchanged.save.health==50f&&
                unchanged.save.inventory[0]==4,
                "Old saves without field medicine retain their original 40-HP camp rest");
        }

        public static void EightFieldSupplyPoliciesRequireRealDistantHarvestsAndPreserveV2(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);
                g.Close();g.save.room=room;g.enemies.Clear();
                WorldObject marker=Array.Find(data.maps[room].objects,
                    o=>o.id=="biomeSurvey"+room);
                WorldObject[] caches=new WorldObject[2];
                caches[0]=Array.Find(data.maps[room].objects,
                    o=>o.id=="trail_forager_"+room+"_0");
                caches[1]=Array.Find(data.maps[room].objects,
                    o=>o.id=="trail_forager_"+room+"_2");
                Check(marker!=null&&caches[0]!=null&&caches[1]!=null,
                    "Field stock belongs to three actual authored remote sites");
                g.save.x=marker.x;g.save.y=marker.y;
                g.Talk(marker);
                Check(g.conversation!=null&&Array.FindIndex(
                    g.conversation.choices,c=>c.action=="biome_supply")<0,
                    "Unsolved field evidence cannot skip straight to civic logistics");
                g.Close();
                g.save.flags.Add("biome_field_done_"+room);
                g.Talk(marker);ChooseFieldOption(g,"biome_supply");
                Check(g.BiomeSupplySampleCount(room)==0,
                    "Uncollected remote cache notes never count as gathered resources");
                int beforeCoins=g.save.coins;
                ChooseFieldOption(g,"biome_supply_road");
                Check(!g.BiomeSupplyBuilt(room)&&g.save.coins==beforeCoins,
                    "Premature allocation is rejected without inventory loss or reward");
                for(int i=0;i<2;i++)
                {
                    WorldObject cache=caches[i];
                    g.save.x=cache.x;g.save.y=cache.y;
                    int slot=cache.kind=="herb"?2:cache.kind=="wood"?0:1;
                    int before=g.save.inventory[slot];
                    g.Talk(cache);
                    Check(g.Has("node_"+cache.id)&&
                        g.save.inventory[slot]==before+3,
                        "Each required evidence sample is gathered from the real world");
                }
                Check(g.BiomeSupplySampleCount(room)==2&&
                    g.Has("biome_supply_sample_"+room+"_0")&&
                    g.Has("biome_supply_sample_"+room+"_2"),
                    "Two genuine resource transactions permanently record distinct survey evidence");
                // The shipped town-rest refresh clears renewable node_*.
                // It must NEVER reset this distinct, publicly reported
                // overland survey milestone or require redoing a completed
                // forty-tile journey just to claim a civilian policy.
                g.save.flags.RemoveAll(flag=>flag.StartsWith("node_"));
                Check(g.BiomeSupplySampleCount(room)==2&&
                    !g.Has("node_"+caches[0].id)&&
                    !g.Has("node_"+caches[1].id),
                    "Renewed resources leave permanent field logistics evidence intact");
                g.save.x=marker.x;g.save.y=marker.y;
                g.save.inventory[0]=20;g.save.inventory[2]=20;
                g.save.inventory[4]=10;
                g.Talk(marker);ChooseFieldOption(g,"biome_supply");
                bool medicine=room%2==0;
                int costMaterials=g.save.inventory[medicine?2:0];
                int gears=g.save.inventory[4];
                int coins=g.save.coins;
                ChooseFieldOption(g,medicine?
                    "biome_supply_medicine":"biome_supply_road");
                Check(g.BiomeSupplyBuilt(room)&&
                    g.BiomeSupplyMedicineBuilt(room)==medicine&&
                    g.BiomeSupplyRoadBuilt(room)==!medicine&&
                    g.save.inventory[medicine?2:0]==costMaterials-2&&
                    g.save.inventory[4]==gears-1&&g.save.coins==coins+22,
                    "Exactly one paid real policy commits with matching materials and reward");
                g.Talk(marker);ChooseFieldOption(g,"biome_supply");
                g.Choose(0);
                Check(g.save.coins==coins+22,
                    "Repeat policy conversation cannot claim a second payout");
                if(medicine)
                {
                    g.Close();g.save.x=marker.x;g.save.y=marker.y;
                    g.save.health=Math.Max(1f,g.save.maxHealth-80f);
                    g.save.energy=50f;g.save.heat=30f;g.save.cold=30f;
                    int herbs=g.save.inventory[2];
                    g.Talk(marker);ChooseFieldOption(g,"biome_supply");
                    ChooseFieldOption(g,"biome_supply_aid");
                    Check(g.save.inventory[2]==herbs-1&&
                        g.save.health==g.save.maxHealth-20f&&
                        g.save.energy==75f&&
                        g.save.heat==5f&&g.save.cold==5f,
                        "Medicine policy produces a real paid healing and climate-relief transaction");
                }
                else Check(g.BiomeSupplyRoadBuilt(room),
                    "Road policy persists for actual lower-fare relay");
                g.PrepareSave();
                GameCore reloaded=new GameCore(data,g.save);
                Check(reloaded.save.version==2&&
                    reloaded.BiomeSupplyBuilt(room)&&
                    reloaded.BiomeSupplyMedicineBuilt(room)==medicine,
                    "Original SaveData v2 survives a loaded policy with intact route flags");
            }
            var global=new GameCore(data);
            global.save.flags.Add("biome_supply_all");
            for(int room=0;room<8;room++)
                global.save.flags.Add("biome_supply_medicine_"+room);
            Check(global.BiomeSupplyNetworkCount==8,
                "Eight independently funded regional policies form the final civic network");
        }
    }
}
