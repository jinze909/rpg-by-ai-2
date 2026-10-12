using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Rescue flags persist; abandoned or interrupted ambushes do not.
        private int wayfarerEncounterRoom;
        public int WayfarerEncounterRoom { get { return wayfarerEncounterRoom; } }
        private static bool IsWayfarerEnemy(Enemy e) { return e != null && e.id >= 82000 && e.id < 82100; }
        private void ResetWayfarerEncounter() { wayfarerEncounterRoom = 0; }
        private static int WayfarerItem(int region) { return region == 1 ? 2 : region == 2 ? 1 : 4; }
        private bool TalkWayfarer(WorldObject o)
        {
            if (!o.kind.StartsWith("wayfarer")) return false;
            int room;
            if (!int.TryParse(o.kind.Substring(8), out room) || room < 1 || room > 3 || room != save.room) return true;
            if (!Has("scout_saved"))
            {
                Dialog(o.name, o.en, "先救回被困密林的公会侦察员，我们才有机会重开这条道路。",
                    "Rescue the guild scout from Thornwood first; then the road can reopen.",
                    C("明白", "Understood", "close"));
                return true;
            }
            if (Has("wayfarer_" + room))
            {
                Dialog(o.name, o.en, "多亏你的帮助，我能继续赶路了。其他旅人也许还在等待救援。",
                    "Thanks to you, I can travel again. Others may still be stranded.",
                    C("旅途平安", "Safe travels", "close"));
                return true;
            }
            if (wayfarerEncounterRoom == room)
            {
                Dialog(o.name, o.en, "伏击者就在附近！请击退他们，或者暂时撤离，之后可以重新尝试。",
                    "The ambushers are here! Defeat them or withdraw and retry later.",
                    C("继续守护", "Keep defending", "close"),
                    C("撤离战斗", "Withdraw", "wayfarer_abandon"));
                return true;
            }
            string[] zh = { "",
                "我的药袋被掠夺者打散了。两份药草能帮助伤者；你也可以替我击退追兵。",
                "洪水卷走了渡口信号灯。两块星晶能点亮安全河道；你也可以帮我击退劫路者。",
                "矿道运输车坏了。两份备用零件可以修好；否则我们得先击退趁乱袭来的掠夺者。" };
            string[] en = { "",
                "Raiders scattered my medicines. Two Moonherbs can help the wounded, or you can fight the pursuers.",
                "The flood carried away our ferry lamp. Two starcrystals can restore the crossing, or we must repel raiders.",
                "The supply cart broke down. Two spare parts will repair it, or we can fight the ambushers." };
            string[] supplyZh = { "", "交出两份药草", "交出两块星晶", "交出两份零件" };
            string[] supplyEn = { "", "Give 2 Moonherbs", "Give 2 starcrystals", "Give 2 spare parts" };
            Dialog(o.name, o.en, zh[room], en[room],
                C(supplyZh[room], supplyEn[room], "wayfarer_supply_" + room),
                C("击退两名伏击者", "Defeat two ambushers", "wayfarer_defend_" + room),
                C("稍后再说", "Prepare first", "close"));
            return true;
        }
        private bool ChooseWayfarer(string action)
        {
            if (action == "wayfarer_abandon")
            {
                if (wayfarerEncounterRoom != 0)
                {
                    enemies.RemoveAll(IsWayfarerEnemy);
                    ResetWayfarerEncounter(); revision++;
                    Notify("伏击已结束，之后可以重新尝试。", "Ambush ended. You can retry.");
                }
                return true;
            }
            bool donation = action.StartsWith("wayfarer_supply_");
            bool fight = action.StartsWith("wayfarer_defend_");
            if (!donation && !fight) return false;
            int room;
            if (!int.TryParse(action.Substring(16), out room) || room < 1 || room > 3 ||
                save.room != room || !Has("scout_saved") || Has("wayfarer_" + room) ||
                wayfarerEncounterRoom != 0 || roadTrialRoom != 0 || practicing || Dying) return true;
            Point npc = ObjectPoint(room, "wayfarer" + room, new Point(-100, -100));
            if ((Position - npc).Length > 2.1f) return true;
            if (donation)
            {
                int item = WayfarerItem(room);
                if (save.inventory[item] < 2)
                {
                    Notify("缺少两份所需材料，也可以选择击退伏击者。",
                        "Not enough supplies. You can fight the ambushers instead.");
                    return true;
                }
                save.inventory[item] -= 2; CompleteWayfarer(room); return true;
            }
            if (!SafeTravel)
            {
                Notify("先清理附近的普通敌人。", "Clear nearby patrols first."); return true;
            }
            var positions = new List<Point>();
            for (int radius = 3; radius <= 7 && positions.Count < 2; radius++)
            for (int dx = -radius; dx <= radius && positions.Count < 2; dx++)
            for (int dy = -radius; dy <= radius && positions.Count < 2; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                Point p = new Point(npc.x + dx, npc.y + dy);
                if (!CanStand(p) || (p - Position).Length < 2.7f ||
                    positions.Exists(other => (other - p).Length < 3) ||
                    enemies.Exists(e => (e.Position - p).Length < 2.1f) ||
                    NavigationPath(p).Count == 0) continue;
                positions.Add(p);
            }
            if (positions.Count != 2)
            {
                Notify("附近没有足够安全的战斗空间；可选择物资救助。",
                    "Not enough clear space for an ambush; try donating supplies.");
                return true;
            }
            wayfarerEncounterRoom = room;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = new Enemy(82000 + room * 10 + slot, slot == 0 ? 0 : 2,
                    positions[slot].x, positions[slot].y, 50 + room * 16);
                // Reserved high IDs must never become multi-hour opening cooldowns.
                e.timer = .9f + slot * .3f; enemies.Add(e);
                effects.Add(new Effect("discover", positions[slot], .6f));
            }
            TrackQuest("wayfarers"); sound = "quest"; revision++;
            Notify("伏击者来了！击退两名敌人，帮助旅人重新上路。",
                "Ambush! Defeat both attackers so the traveler can continue.");
            return true;
        }
        private void TickWayfarerEncounter()
        {
            if (wayfarerEncounterRoom == 0) return;
            if (save.room != wayfarerEncounterRoom || Dying || practicing)
            { ResetWayfarerEncounter(); return; }
            if (enemies.Exists(IsWayfarerEnemy)) return;
            int room = wayfarerEncounterRoom; ResetWayfarerEncounter(); CompleteWayfarer(room);
        }
        private void CompleteWayfarer(int room)
        {
            if (Has("wayfarer_" + room)) return;
            Flag("wayfarer_" + room);
            save.coins += 20; save.shards++; AddXp(25); revision++;
            RecordMoment("帮助第 " + room + " 位失联旅人：获得20硬币、1碎片、25经验。",
                "Helped traveler " + room + ": 20 coins, 1 shard, 25 XP.");
            if (Has("wayfarer_1") && Has("wayfarer_2") && Has("wayfarer_3") && !Has("wayfarer_complete"))
            {
                Flag("wayfarer_complete"); save.skillPoints++; save.inventory[6]++;
                RecordMoment("三段归途重归安全：技能点1、炖汤1。",
                    "All three roadside journeys secured: 1 talent point and 1 stew.");
                Notify("三位旅人平安出发！额外技能点1、炖汤1。",
                    "All three travelers safe! Bonus: 1 talent point and 1 stew.");
            }
            else Notify("旅人安全出发！20硬币、1碎片、25经验。",
                "Traveler safe! +20 coins, 1 shard, 25 XP.");
            TrackQuest("wayfarers"); sound = "reward";
        }
    }
}
