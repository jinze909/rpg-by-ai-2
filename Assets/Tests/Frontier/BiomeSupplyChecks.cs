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
                Check(g.BiomeSupplySampleCount(room)==2,
                    "Two independently harvested remote samples certify a real route");
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
