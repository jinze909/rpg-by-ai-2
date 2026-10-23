using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // One retreat per floor: trade UNBANKED loot for survival rather
        // than receiving free heals or spending permanently banked shards.
        private string AbyssRootwellFlag
        {
            get { return "abyss_annex_rootwell_" + save.abyssFloor; }
        }

        private WorldObject AbyssRootwell()
        {
            if (!InAbyss || Map.objects == null) return null;
            return Array.Find(Map.objects, o => o.kind == "abyssRootwell");
        }

        private void TalkAbyssRootwell(WorldObject o)
        {
            if (!InAbyss || Dying || practicing || !EnsurePhysicalInteractionReach(o)) return;
            if (Has(AbyssRootwellFlag))
            {
                Dialog(o.name, o.en,
                    "复苏泉已将这一层的光流交给你。再次使用必须进入下一层。",
                    "The rootwell has shared this floor's light. Descend to find another charge.",
                    C("继续前进", "Keep exploring", "close"));
                return;
            }
            if (!SafeTravel)
            {
                Notify("附近仍有战斗或落印威胁，无法安全接触复苏泉。",
                    "Nearby combat or a live rift warning makes the rootwell unsafe.");
                return;
            }
            Dialog(o.name, o.en,
                "旧巡路人将地脉改成公共避难泉。这里的能量并非免费：你可以拿尚未结算的碎片交换疗伤，或者恢复施法与闪避的耐力。每层只能选择一次。\n\n本层待结算碎片：" +
                    save.runShards + "。永久存入背包的碎片不会被扣除。",
                "Old roadwardens turned this starroot into a public refuge. Spend UNBANKED shards to heal or replenish energy. Choose once per floor; permanent banked shards are never spent.\n\nUnbanked shards: " +
                    save.runShards + ".",
                C("疗伤 · 碎片2 · 回复40%最大生命",
                    "Mend wounds · 2 shards · heal 40% max HP", "abyssRootwellHeal"),
                C("回气 · 碎片1 · 回复55耐力",
                    "Recover energy · 1 shard · +55 energy", "abyssRootwellEnergy"),
                C("暂时不用", "Not yet", "close"));
        }

        private void UseAbyssRootwell(bool heal)
        {
            if (!InAbyss || Dying || practicing || Has(AbyssRootwellFlag)) return;
            WorldObject rootwell = AbyssRootwell();
            // Re-check the LIVE source after the modal choice. A stale or
            // fabricated remote choice must never consume resources.
            if (rootwell == null || !EnsurePhysicalInteractionReach(rootwell)) return;
            if (!SafeTravel)
            {
                Notify("敌人或落印尚未平息，无法安全完成地脉交换。",
                    "Clear nearby threats before exchanging shards at the rootwell.");
                return;
            }
            int cost = heal ? 2 : 1;
            if (save.runShards < cost)
            {
                Notify("本层待结算碎片不足。探索支洞或击败敌人后再来。",
                    "Not enough unbanked shards. Explore the galleries or defeat enemies.");
                return;
            }
            if (heal ? save.health >= save.maxHealth : save.energy >= 100)
            {
                Notify(heal ? "生命已满，无需消耗碎片。" : "耐力已满，无需消耗碎片。",
                    heal ? "Already at full health; no shards spent." :
                        "Already at full energy; no shards spent.");
                return;
            }
            save.runShards -= cost;
            if (heal)
                save.health = Math.Min(save.maxHealth, save.health + save.maxHealth * .4f);
            else
                save.energy = Math.Min(100, save.energy + 55);
            Flag(AbyssRootwellFlag);
            revision++;
            sound = "reward";
            Notify(heal ? "复苏泉缝合了创口。本层已无法再次取用。" :
                    "星根回流补足了耐力。本层已无法再次取用。",
                heal ? "The rootwell mends your wounds. This floor's charge is spent." :
                    "Starroot restores your energy. This floor's charge is spent.");
        }

        // Extra seeded galleries are optional. All loot is pending until the
        // original root gate banks the floor; exiting or dying loses it.
        private void TalkAbyssFarGallery(WorldObject o)
        {
            if(!InAbyss||Dying||practicing||
                !EnsurePhysicalInteractionReach(o))return;
            bool survey=o.kind=="abyssFarSurvey";
            bool cache=o.kind=="abyssFarCache";
            bool echo=o.kind=="abyssFarEcho";
            string proof="abyss_annex_far_survey_"+save.abyssFloor;
            if(echo&&!Has(proof))
            {
                Dialog(o.name,o.en,
                    "回声石只重复破碎的呼声。先去南侧发现真正留下这段话的人。",
                    "The echo stone repeats only fragments. Find the original testimony to the south first.",
                    C("继续探查","Seek the testimony","close"));
                return;
            }
            string flag="abyss_annex_far_"+(survey?"survey_":cache?"cache_":"echo_")+save.abyssFloor;
            bool first=!Has(flag);
            if(first)
            {
                Flag(flag);
                // The lower gallery shares the same v2 unbanked ceilings
                // as ordinary enemy defeats and near-side annex discoveries.
                // Saturate BEFORE an autosave so deep expedition progress is
                // never truncated or wraps when this floor is resumed.
                if (survey)
                    save.runXp = CappedReward(save.runXp, 25, 9999);
                else if (cache)
                {
                    save.runCoins = CappedReward(save.runCoins, 30, 9999);
                    save.runShards = CappedReward(save.runShards, 1, 9999);
                }
                else save.runXp = CappedReward(save.runXp, 20, 9999);
                revision++;sound="reward";
            }
            string zh=survey?
                "这块地脉残碑留下六次撤离的真实坐标。星根并不只供一面城墙发光，它也曾为困在地下的普通人保留回家的路。":
                cache?
                "巡路者在洞底封存的药材和原料，直到今天才重新看见微光。它们依旧属于本层未结算的远征成果。":
                "回声石终于接上地脉残碑的完整证词：原来的地下通道是为分享能源而修，而不是为封锁彼此而生。";
            string en=survey?
                "A lower root tablet records six evacuation bearings. Starroot once guided ordinary people out of the mines, not merely powered a wall.":
                cache?
                "Supplies sealed in this bottom gallery are still unbanked expedition loot until the original root gate settles the floor.":
                "The echo stone completes the testimony: these passageways were built to share energy, not close communities off from one another.";
            Dialog(o.name,o.en,zh+(first?
                    "\n\n此层待结算："+(cache?"硬币 +30、碎片 +1。":survey?"经验 +25。":"经验 +20。"):
                    "\n\n该层记录已保存，无法重复领取。"),
                en+(first?
                    "\n\nUnbanked: "+(cache?"+30 coins, +1 shard.":survey?"+25 XP.":"+20 XP."):
                    "\n\nRecorded for this floor; no duplicate loot."),
                C("收下记录","Keep the account","close"));
        }
    }
}
