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

        public int AbyssWardCharterProgress
        {
            get
            {
                int count = 0;
                for (int floor = 3; floor <= 6; floor++)
                    if (Has("abyss_annex_ward_" + floor)) count++;
                return count;
            }
        }

        // A strategic answer to floor-three-plus rift storms: pay for
        // sustained mitigation with loot from this SAME unbanked floor.
        public bool AbyssWardActive
        {
            get { return InAbyss && save.abyssFloor >= 3 &&
                Has("abyss_annex_ward_" + save.abyssFloor); }
        }

        private WorldObject AbyssWardObject()
        {
            if (!InAbyss || save.abyssFloor < 3 || Map.objects == null) return null;
            return Array.Find(Map.objects, o => o.kind == "abyssRiftWard");
        }

        private void TalkAbyssWard(WorldObject o)
        {
            if (!InAbyss || save.abyssFloor < 3 || Dying || practicing ||
                !EnsurePhysicalInteractionReach(o)) return;
            if (AbyssWardActive)
            {
                Dialog(o.name, o.en,
                    "镇流碑已经与本层地脉共振。新的落印间隔延长至10秒，预警更明显，落印范围与伤害也减轻。",
                    "The ward is tuned for this floor: new rifts arrive every 10 seconds with longer warnings, smaller circles and reduced damage.",
                    C("继续探索", "Keep exploring", "close"));
                return;
            }
            Dialog(o.name, o.en,
                "地下巡路人曾用镇流碑保护救援路线。你可以消耗本层待结算碎片3枚，让本层落印从每6秒一次放缓到10秒一次，并减小其伤害与范围。只有这一层有效；原本已存入背包的碎片不会消耗。\\n\\n当前本层碎片：" +
                    save.runShards + "。",
                "Roadwardens used this pylon to protect evacuation routes. Trade 3 UNBANKED shards to slow this floor's rift pulses from 6 to 10 seconds, shrink their radius and reduce damage. Resets next floor; banked shards are untouched.\\n\\nCurrent unbanked shards: " +
                    save.runShards + ".",
                C("镇流 · 待结算碎片3", "Tune ward · 3 unbanked shards", "abyssWardTune"),
                C("保留战利品", "Keep the loot", "close"));
        }

        private void TuneAbyssWard()
        {
            if (!InAbyss || save.abyssFloor < 3 || Dying || practicing ||
                AbyssWardActive || Has("abyss_clear_" + save.abyssFloor)) return;
            WorldObject ward = AbyssWardObject();
            if (ward == null || !EnsurePhysicalInteractionReach(ward)) return;
            if (save.runShards < 3)
            {
                Notify("本层未结算碎片不足3枚。可先击败敌人或搜寻支洞补给。",
                    "You need 3 unbanked shards. Defeat enemies or search the side galleries.");
                return;
            }
            save.runShards -= 3;
            Flag("abyss_annex_ward_" + save.abyssFloor);
            // Do not advance a pending telegraph or invent an extra hit.
            // Newly scheduled floor hazards use the slower ten-second clock.
            stormTimer = Math.Max(stormTimer, 10f);
            revision++;
            sound = "reward";
            Notify("镇流碑已稳定：本层落印每10秒一次，范围和伤害降低。",
                "Ward tuned: this floor's rifts arrive every 10 seconds with smaller, weaker strikes.");
        }

        // Three distant, physically visited starroot relays form an optional
        // real route puzzle. Progress and one-time rewards live in EXISTING
        // SaveData v2 flags; no new serialized fields or transient shortcuts.
        private static readonly int[,] AbyssCircuitOrders = {
            { 0, 1, 2 }, { 0, 2, 1 }, { 1, 0, 2 },
            { 1, 2, 0 }, { 2, 0, 1 }, { 2, 1, 0 }
        };
        private string AbyssCircuitStepFlag(int step)
        {
            return "abyss_annex_circuit_" + save.abyssFloor + "_" + step;
        }
        private string AbyssCircuitFinishFlag
        {
            get { return "abyss_annex_circuit_complete_" + save.abyssFloor; }
        }
        public bool AbyssCircuitComplete
        {
            get { return InAbyss && Has(AbyssCircuitFinishFlag); }
        }
        public int AbyssCircuitProgress
        {
            get
            {
                if (!InAbyss) return 0;
                if (AbyssCircuitComplete) return 3;
                int count = 0;
                while (count < 3 && Has(AbyssCircuitStepFlag(count))) count++;
                // An interrupted or malformed old v2 flags list may contain
                // all step marks but not the completion transaction. Do not
                // index a nonexistent fourth relay from Objective()/HUD.
                // Requiring the final physical relay again is the safe path.
                return Math.Min(2, count);
            }
        }
        private int AbyssCircuitTarget(int step)
        {
            // Long arithmetic preserves every valid positive v2 seed.
            int variation = (int)(((long)save.abyssSeed +
                save.abyssFloor * 13L) % 6L);
            return AbyssCircuitOrders[variation, step];
        }
        public int AbyssCircuitNextNode
        {
            get { return InAbyss && !AbyssCircuitComplete ?
                AbyssCircuitTarget(AbyssCircuitProgress) : -1; }
        }
        private static string AbyssCircuitSymbol(int node)
        {
            return node == 0 ? "A" : node == 1 ? "B" : "C";
        }
        public bool AbyssCircuitVisited(int node)
        {
            if (!InAbyss || node < 0 || node > 2) return false;
            for (int i = 0; i < AbyssCircuitProgress; i++)
                if (AbyssCircuitTarget(i) == node) return true;
            return false;
        }
        // Linked field relays form a small tactical refuge, not an
        // invulnerability toggle. The benefit applies only to actual Rift
        // ground contacts near a visited relay with an unobstructed route.
        // Flag-backed v2 progress makes the shelter survive reloads.
        public const float AbyssCircuitShelterRadius = 3.25f;
        public bool AbyssCircuitSheltered
        {
            get
            {
                if (!InAbyss || save.abyssFloor < 3 ||
                    Map.objects == null) return false;
                float range2 = AbyssCircuitShelterRadius * AbyssCircuitShelterRadius;
                foreach (WorldObject relay in Map.objects)
                {
                    if (!relay.kind.StartsWith("abyssCircuit")) continue;
                    int node;
                    if (!int.TryParse(relay.kind.Substring("abyssCircuit".Length),
                        out node) || !AbyssCircuitVisited(node)) continue;
                    float dx = relay.x - save.x, dy = relay.y - save.y;
                    if (dx * dx + dy * dy <= range2 &&
                        ClearAttackPath(new Point(relay.x, relay.y), Position))
                        return true;
                }
                return false;
            }
        }
        private float AbyssRiftContactDamage(Hazard h)
        {
            // All other hazards retain their existing balance, windups,
            // parries and real line-of-sight collision contracts.
            return h.kind == "rift" && AbyssCircuitSheltered ?
                h.damage * .45f : h.damage;
        }

        private bool AbyssCircuitRouteSafe()
        {
            return InAbyss && AbyssCircuitComplete && !Dying && !practicing &&
                Has("abyss_clear_" + save.abyssFloor) &&
                enemies.Count == 0 && hazards.Count == 0 &&
                projectiles.Count == 0 && SafeTravel;
        }
        private bool NearLiveAbyssCircuit()
        {
            if (!InAbyss || Map.objects == null) return false;
            foreach (WorldObject relay in Map.objects)
                if (relay.kind.StartsWith("abyssCircuit") &&
                    HasPhysicalInteractionReach(relay)) return true;
            return false;
        }
        private void ReturnViaAbyssCircuit()
        {
            if (!AbyssCircuitRouteSafe() || !NearLiveAbyssCircuit())
            {
                Notify("折返光路不可用：请靠近已接通信标，并清除敌人、落印和飞行中的攻击。",
                    "Route unavailable. Reach a linked relay and clear all enemies, warnings and flying attacks.");
                return;
            }
            Point arrival = new Point(30.5f, 14.5f);
            if (!CanStand(arrival))
            {
                Notify("地脉门前的落脚处暂时无法抵达。",
                    "The safe arrival point by the root gate is blocked.");
                return;
            }
            // This is same-floor movement, NOT a new expedition or settlement:
            // do not call Enter/ResetTransient/BankFloor or mutate v2 rewards.
            save.x = arrival.x; save.y = arrival.y;
            revision++;
            sound = "travel";
            Notify("顺着三处星根光路返回东门。战利品仍需手动结算。",
                "Returned to the east root gate. Bank your loot manually.");
        }

        private void TalkAbyssCircuit(WorldObject o)
        {
            if (!InAbyss || Dying || practicing ||
                !EnsurePhysicalInteractionReach(o)) return;
            int node;
            if (!int.TryParse(o.kind.Substring("abyssCircuit".Length),
                out node) || node < 0 || node > 2) return;
            if (AbyssCircuitComplete)
            {
                if (!AbyssCircuitRouteSafe())
                {
                    Notify("回路已接通，但本层敌人或落印仍未平息。清除危险后，信标可送你返回东门。",
                        "Circuit complete, but hostiles or rift contacts remain. Clear this floor before traveling from a relay.");
                    return;
                }
                Dialog(o.name, o.en,
                    "星根回路已恢复旧时的巡路轨道。东侧地脉门在回应三个信标。现在可以安全折返，但战利品仍须在东门手动结算。",
                    "The three relays have reconnected the old evacuation route. You can return safely to the east root gate; unbanked rewards must still be settled there.",
                    C("沿光路折返东门", "Take the starroot route to the east gate", "abyssCircuitReturn"),
                    C("继续探索", "Keep exploring", "close"));
                return;
            }
            int progress = AbyssCircuitProgress;
            int expected = AbyssCircuitTarget(progress);
            if (AbyssCircuitVisited(node))
            {
                Notify("这处信标已经接入回路。下一处为 " +
                    AbyssCircuitSymbol(expected) + "。",
                    "This relay is already connected. Seek node " +
                    AbyssCircuitSymbol(expected) + " next.");
                return;
            }
            if (node != expected)
            {
                for (int i = 0; i < 3; i++)
                    save.flags.Remove(AbyssCircuitStepFlag(i));
                save.energy = Math.Max(0, save.energy - 12);
                effects.Add(new Effect("spark", new Point(o.x, o.y), .45f));
                revision++;
                Notify("接线顺序错误：回路断开，损失12耐力。重新从 " +
                    AbyssCircuitSymbol(AbyssCircuitTarget(0)) + " 开始。",
                    "Wrong relay order: circuit reset, -12 energy. Restart at " +
                    AbyssCircuitSymbol(AbyssCircuitTarget(0)) + ".");
                return;
            }
            Flag(AbyssCircuitStepFlag(progress));
            effects.Add(new Effect("spark", new Point(o.x, o.y), .6f));
            sound = "reward";
            if (progress < 2)
            {
                Notify("星根回路 " + (progress + 1) + "/3 已接通。下一信标：" +
                    AbyssCircuitSymbol(AbyssCircuitTarget(progress + 1)) + "。",
                    "Starroot circuit " + (progress + 1) +
                    "/3 connected. Next relay: " +
                    AbyssCircuitSymbol(AbyssCircuitTarget(progress + 1)) + ".");
                return;
            }
            Flag(AbyssCircuitFinishFlag);
            // Same saturating, UNBANKED balances as ordinary side galleries.
            // Death or extraction still discards rewards until floor banking.
            save.runCoins = CappedReward(save.runCoins, 40, 9999);
            save.runShards = CappedReward(save.runShards, 4, 9999);
            save.runXp = CappedReward(save.runXp, 30, 9999);
            revision++;
            Notify("三处星根回路恢复了巡路灯！本层待结算：硬币40、碎片4、经验30。",
                "Three starroot relays light the road! Unbanked: 40 coins, 4 shards, 30 XP.");
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
