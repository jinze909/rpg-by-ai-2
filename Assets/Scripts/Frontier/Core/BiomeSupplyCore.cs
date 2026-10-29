using System;
namespace Starfall
{
    // The public field journal grows into a practical logistics decision.
    // These are TWO distinct, physically harvested remote wilderness samples
    // plus the original completed two-site deduction, not new invisible pickups.
    // All outcomes are original SaveData v2 flags, never new serialized fields.
    public sealed partial class GameCore
    {
        private static readonly string[] supplyZh={
            "街外的空碗摆在堤边。记录员说，送达的车数从来不能代替逐户敲门。",
            "猎人火堆旁的路太短。真正能把药送出去的，是绕过巢穴的那条断枝小道。",
            "潮水退后，侧廊的木板能支起临时担架。没人应该再被要求走那座断桥。",
            "矿工留下两把钥匙：一把锁炉，一把开后门。先救人再修机器，才算补给。",
            "堡垒侧门的医护名单没有军衔。守路人争取的是门内门外都能用的物资。",
            "浮灯会骗人。岸上的固定灯不会，夜里渡船需要的是能看见的物资交接。",
            "热浪中的高塔不存在。水和药必须走真实山影指向的背阴路。",
            "敲迎风高钟没人听得见。背风低钟旁的药包却真有人拿去救过命。"
        };
        private static readonly string[] supplyEn={
            "The empty bowls beyond the levee are counted by doorstep, not by arriving wagon.",
            "A shortcut beside the hunters' fires cannot carry medicine safely past the nests.",
            "At low tide, salvaged side-gallery boards can support a stretcher without using the lost bridge.",
            "Miners kept one key for the furnace and one for the rear exit. People come before machines.",
            "The evacuation roster bears no military ranks. Supplies must serve both sides of the wall.",
            "Drifting lights lie; the two fixed shore lamps mark where rescue cargo can be handed over.",
            "The shining desert tower is imaginary. Real water follows the mountain's shaded bearing.",
            "The windward bell was never heard. Someone carried medicine from the low leeward chime."
        };
        private string BiomeSupplyFlag(string kind,int room)
        {return "biome_supply_"+kind+"_"+room;}
        public bool BiomeSupplyMedicineBuilt(int room)
        {return room>=0&&room<8&&Has(BiomeSupplyFlag("medicine",room));}
        public bool BiomeSupplyRoadBuilt(int room)
        {return room>=0&&room<8&&Has(BiomeSupplyFlag("road",room));}
        public bool BiomeSupplyBuilt(int room)
        {return BiomeSupplyMedicineBuilt(room)||BiomeSupplyRoadBuilt(room);}
        public int BiomeSupplySampleCount(int room)
        {
            if(room<0||room>=8)return 0;
            // Rest renews node_* for material harvest, but it must not
            // erase proof that the player has already physically surveyed
            // a remote route. Old in-progress saves with live node flags
            // are accepted even before their first renewed harvest.
            return (Has("biome_supply_sample_"+room+"_0")||
                    Has("node_trail_forager_"+room+"_0")?1:0)+
                (Has("biome_supply_sample_"+room+"_2")||
                 Has("node_trail_forager_"+room+"_2")?1:0);
        }
        private void RegisterBiomeSupplyGather(WorldObject o)
        {
            int region=save.room;
            if(o==null||region<0||region>=8||
                !SourceBelongsToActiveMap(o))return;
            if(o.id=="trail_forager_"+region+"_0")
                Flag("biome_supply_sample_"+region+"_0");
            else if(o.id=="trail_forager_"+region+"_2")
                Flag("biome_supply_sample_"+region+"_2");
        }
        public int BiomeSupplyNetworkCount
        {
            get{int count=0;for(int i=0;i<8;i++)
                if(BiomeSupplyBuilt(i))count++;return count;}
        }
        private string BiomeSupplySummary(bool english,int room)
        {
            if(BiomeSupplyMedicineBuilt(room))
                return english?
                    "\n\nPublic medicine stock: camp rest heals +20 HP. At either original evidence marker, one Moonherb can treat 60 HP, 25 energy, and 25 heat/cold.":
                    "\n\n公共药箱已建：营地休息额外恢复20生命。两处现场地标可用一株月光草治疗60生命、25耐力，并缓解冷热各25。";
            if(BiomeSupplyRoadBuilt(room))
                return english?
                    "\n\nCivilian road stock: the verified field relay now costs only 1 coin and 6 energy, without bypassing live danger.":
                    "\n\n居民通路已修复：实地接驳降为1硬币、6耐力；正在发生的战斗与阻断仍不能跳过。";
            return english?
                "\n\nRemote logistics: recover both far wilderness samples ("+
                BiomeSupplySampleCount(room)+"/2), then fund ONE lasting local policy.":
                "\n\n远路补给：真正采集两处远端物资（"+
                BiomeSupplySampleCount(room)+"/2），再选择一种长期执行的居民方案。";
        }
        private bool ChooseBiomeSupply(string action)
        {
            if(action!="biome_supply"&&action!="biome_supply_medicine"&&
                action!="biome_supply_road"&&action!="biome_supply_aid")
                return false;
            int room=save.room;
            WorldObject site=pendingDialogueSource;
            if(room<0||room>=8||mode!="world"||Dying||practicing||
                site==null||pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(site)||
                !HasPhysicalInteractionReach(site)||
                !CanStand(Position)||!Has("biome_field_done_"+room)||
                (site.kind!="biomeSurvey"&&site.kind!="biomeAnswer")||
                site.id!="biome"+
                    (site.kind=="biomeSurvey"?"Survey":"Answer")+room)
                return true;
            if(action=="biome_supply")
            {
                bool built=BiomeSupplyBuilt(room);
                Dialog(site.name,site.en,supplyZh[room]+
                    BiomeSupplySummary(false,room)+
                    (built?"\n\n公共方案已登记，不能把居民交出的材料又领回或重复领取奖励。":
                        "\n\n医学方案消耗月光草×2、零件×1；道路方案消耗木材×2、零件×1。两种方案只能选其一。"),
                    supplyEn[room]+BiomeSupplySummary(true,room)+
                    (built?"\n\nThe public policy is already filed; there is no second payout.":
                        "\n\nMedicine needs 2 Moonherbs + 1 gear; road works need 2 timber + 1 gear. Choose only one."),
                    built&&BiomeSupplyMedicineBuilt(room)?
                        C("现场医疗 · 月光草×1","Field triage · 1 Moonherb",
                          "biome_supply_aid"):
                        !built?C("建立居民药箱 · 月光草×2 / 零件×1",
                          "Build civilian medicine stock · 2 herbs / 1 gear",
                          "biome_supply_medicine"):
                        C("修复的居民通路","Repaired civilian crossing","close"),
                    !built?C("修复共同通路 · 木材×2 / 零件×1",
                        "Restore shared road · 2 timber / 1 gear",
                        "biome_supply_road"):
                        C("查看本地记录","Read local account","close"),
                    C("暂不决定","Decide later","close"));
                return true;
            }
            if(action=="biome_supply_aid")
            {
                if(!BiomeSupplyMedicineBuilt(room))return true;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("危险尚未结束，现场医疗不能穿过敌人的攻击。",
                        "Live threats prevent safe field triage.");
                    return true;
                }
                if(save.health>=save.maxHealth&&save.energy>=100f&&
                    save.heat<=0f&&save.cold<=0f)
                {
                    Notify("无需治疗，保留药草下次使用。",
                        "No treatment needed. Keep the Moonherb.");
                    return true;
                }
                if(!Spend(0,0,1,0,0))return true;
                save.health=Math.Min(save.maxHealth,save.health+60f);
                save.energy=Math.Min(100f,save.energy+25f);
                save.heat=Math.Max(0f,save.heat-25f);
                save.cold=Math.Max(0f,save.cold-25f);
                revision++;sound="reward";
                Notify("公共药箱：生命+60、耐力+25，缓解冷热各25。",
                    "Public triage: +60 HP, +25 energy, -25 heat and cold.");
                return true;
            }
            // The actual two wilderness pickups prove this route. A player
            // cannot fund either policy with generic inventory alone.
            if(BiomeSupplyBuilt(room)||BiomeSupplySampleCount(room)<2)
            {
                Notify("必须亲自采集远端两处物资，才能建立公共补给方案。",
                    "Harvest BOTH distant wilderness caches before opening a public stock.");
                return true;
            }
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("先结束现场任务和战斗，才能交付公共补给。",
                    "Finish live fieldwork and combat before allocating civilian stock.");
                return true;
            }
            bool medicine=action=="biome_supply_medicine";
            if(!Spend(medicine?0:2,0,medicine?2:0,0,1))return true;
            Flag(BiomeSupplyFlag(medicine?"medicine":"road",room));
            CreditCoins(22);AddXp(35);
            RecordMoment(supplyZh[room]+(medicine?
                    "居民把多出的药留在公开地标。任何伤者都可在安全时接受救助，营地也添上了药箱。":
                    "居民用工料修好了两端接驳的损耗路段。往返不再依赖堡垒的高额路费。"),
                supplyEn[room]+(medicine?
                    " Residents leave medicine at public markers and stock the camp's healing shelf.":
                    " Residents repair both ends of the civilian crossing without depending on citadel fares."));
            if(BiomeSupplyNetworkCount==8&&!Has("biome_supply_all"))
            {
                Flag("biome_supply_all");CreditShards(3);
                RecordMoment(
                    "八地各自选择了需要的物资方案。公共补给不是一种模子，而是人们亲自把药和道路留在家门口。",
                    "Eight regions chose different practical provisions. A shared network does not require identical needs or identical routes.");
            }
            revision++;sound="reward";
            Notify(medicine?
                    "居民药箱已建：营地休息+20生命；现场治疗开放。硬币+22、经验+35。":
                    "居民道路已修复：接驳降为1硬币、6耐力。硬币+22、经验+35。",
                medicine?
                    "Medicine stock built: +20 camp healing and field triage. +22 coins, +35 XP.":
                    "Civilian road repaired: relay now costs 1 coin and 6 stamina. +22 coins, +35 XP.");
            return true;
        }
    }
}
