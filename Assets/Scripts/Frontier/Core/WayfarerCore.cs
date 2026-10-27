using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Rescue flags persist; abandoned or interrupted ambushes do not.
        private int wayfarerEncounterRoom;
        private float wayfarerIntegrity;
        private bool wayfarerWarned;
        private bool wayfarerAidUsed;
        // Physical command, ephemeral and non-serialized like the live ambush.
        private bool wayfarerCoverUsed;
        private float wayfarerCoverTime;
        // Real time-limited, per-encounter provocation tied to the exact
        // alive spawned enemy instances, NEVER numeric IDs across loads.
        // Combat ownership is a live Enemy INSTANCE, never a reusable ID.
        // An unrelated actor with the same numeric ID must not intercept a
        // traveler strike or falsely settle the rescue's two lethal slots.
        private readonly Enemy[] wayfarerRaiders=new Enemy[2];
        private readonly HashSet<Enemy> wayfarerDecoyTargets=
            new HashSet<Enemy>();
        private readonly Dictionary<Enemy,Point> wayfarerDecoyLastSeen=
            new Dictionary<Enemy,Point>();
        private bool wayfarerDecoyUsed;
        private float wayfarerDecoyTime;
        public float WayfarerDecoyTime
        {get{return wayfarerEncounterRoom==save.room?
            wayfarerDecoyTime:0f;}}
        public bool IsWayfarerDecoyTarget(Enemy enemy)
        {
            return enemy!=null&&wayfarerEncounterRoom==save.room&&
                wayfarerDecoyTime>0f&&enemy.hp>0&&
                enemies.Contains(enemy)&&wayfarerDecoyTargets.Contains(enemy);
        }
        public bool WayfarerDecoyCanSeeHero(Enemy enemy)
        {
            return IsWayfarerDecoyTarget(enemy)&&
                CanStand(Position)&&CanStand(enemy.Position)&&
                ClearAttackPath(enemy.Position,Position);
        }
        // A successful player shout teaches the raider where the actual
        // sound came from. Subsequent pursuit can update ONLY on real
        // clear sight; going behind a pillar preserves that last-known foot,
        // not the magically updated live hero coordinates.
        private Point WayfarerDecoyPursuitFoot(Enemy enemy)
        {
            if(!IsWayfarerDecoyTarget(enemy))return Position;
            Point last;
            if(!wayfarerDecoyLastSeen.TryGetValue(enemy,out last))
                last=Position;
            if(WayfarerDecoyCanSeeHero(enemy))
            {
                last=Position;
                wayfarerDecoyLastSeen[enemy]=last;
            }
            return last;
        }
        private List<Enemy> WayfarerAudibleRaiders()
        {
            var heard=new List<Enemy>();
            if(wayfarerEncounterRoom==0||
               wayfarerEncounterRoom!=save.room||Dying||practicing||
               !CanStand(Position))return heard;
            foreach(Enemy enemy in enemies)
                if(IsWayfarerEnemy(enemy)&&enemy.hp>0&&
                   (enemy.Position-Position).Length<=7f&&
                   ClearAttackPath(enemy.Position,Position))
                    heard.Add(enemy);
            return heard;
        }
        public bool WayfarerDecoyAvailable
        {
            get
            {
                if(wayfarerDecoyUsed||wayfarerEncounterRoom==0||
                   save.room!=wayfarerEncounterRoom||
                   save.energy<15f||Dying||practicing)return false;
                Point civilian=ObjectPoint(wayfarerEncounterRoom,
                    "wayfarer"+wayfarerEncounterRoom,
                    new Point(-100,-100));
                return CanReachWayfarer(civilian)&&
                    WayfarerAudibleRaiders().Count>0;
            }
        }
        public float WayfarerCoverTime { get { return wayfarerCoverTime; } }
        public bool WayfarerCoverAvailable
        { get { return wayfarerEncounterRoom!=0 &&
            save.room==wayfarerEncounterRoom && !wayfarerCoverUsed &&
            !Dying && !practicing && save.energy>=20f; } }
        // An empty enemy list is not evidence that both travelers' raiders
        // died in player combat. Keep confirmations transient, like the ambush.
        private int wayfarerConfirmedKills;
        private void OnWayfarerEnemyDefeated(Enemy enemy)
        {
            if(wayfarerEncounterRoom==0 || save.room!=wayfarerEncounterRoom ||
                !IsWayfarerEnemy(enemy))return;
            int index=Object.ReferenceEquals(enemy,wayfarerRaiders[0])?0:
                Object.ReferenceEquals(enemy,wayfarerRaiders[1])?1:-1;
            if(index>=0)wayfarerConfirmedKills|=1<<index;
        }
        public int WayfarerEncounterRoom { get { return wayfarerEncounterRoom; } }
        public float WayfarerIntegrity { get { return wayfarerIntegrity; } }
        private bool IsWayfarerEnemy(Enemy e)
        {
            return e!=null&&
                (Object.ReferenceEquals(e,wayfarerRaiders[0])||
                 Object.ReferenceEquals(e,wayfarerRaiders[1]));
        }
        private void ResetWayfarerEncounter()
        {
            // Retire a rescue's own raiders, inbound arrows and warnings
            // on cancellation, failure, restart and save reload.
            enemies.RemoveAll(IsWayfarerEnemy);
            wayfarerRaiders[0]=wayfarerRaiders[1]=null;
            projectiles.RemoveAll(p => p.wayfarerOwned);
            effects.RemoveAll(e => e.wayfarerOwned);
            wayfarerEncounterRoom = 0; wayfarerIntegrity = 0;
            wayfarerWarned = wayfarerAidUsed = false;
            wayfarerCoverUsed=false;wayfarerCoverTime=0f;
            wayfarerDecoyUsed=false;wayfarerDecoyTime=0f;
            wayfarerDecoyTargets.Clear();
            wayfarerDecoyLastSeen.Clear();
            wayfarerConfirmedKills = 0;
        }
        private static int WayfarerItem(int region) { return region == 1 ? 2 : region == 2 ? 1 : 4; }
        private bool WayfarerFinalRewardPending
        {
            get { return Has("wayfarer_1") && Has("wayfarer_2") &&
                Has("wayfarer_3") && !Has("wayfarer_complete"); }
        }
        private bool ClaimWayfarerFinalReward()
        {
            if (!WayfarerFinalRewardPending) return false;
            // A completed rescue is durable independently of its promised
            // tangible stew. Never mark the one-time final bonus paid before
            // there is room in the real v2 inventory.
            if (!HasInventoryRoom(6, 1)) return false;
            Flag("wayfarer_complete");
            CreditSkillPoints(1); CreditInventory(6, 1);
            RecordMoment("三段归途重归安全：技能点1、炖汤1。",
                "All three roadside journeys secured: 1 talent point and 1 stew.");
            Notify("三位旅人平安出发！额外技能点1、炖汤1。",
                "All three travelers safe! Bonus: 1 talent point and 1 stew.");
            TrackQuest("wayfarers"); sound = "reward";
            return true;
        }
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
                string[] defendedZh = { "",
                    "你替我挡住掠夺者，我终于把药袋重新扎好。伤者们已经能继续赶路。",
                    "你守住渡口时，我把信号灯重新挂回河岸。今晚的船终于有了归途。",
                    "你守住矿道，我把散落的零件装回运输车。下一班工人不必摸黑回家。" };
                string[] defendedEn = { "",
                    "You held off the raiders while I secured the medicine. The wounded can travel again.",
                    "You guarded the crossing while I relit the river signal. Tonight's boats have a way home.",
                    "You held the mine passage while I repaired the cart. The next shift can return safely." };
                string[] suppliedZh = { "",
                    "你送来的药草救了伤者，也让我能带着药袋重新上路。",
                    "你送来的星晶点亮了渡口信号，涨水时也能看清安全航道。",
                    "你送来的零件修好了运输车，矿道补给终于重新流动。" };
                string[] suppliedEn = { "",
                    "Your Moonherbs helped the wounded and put my medicine bag back on the road.",
                    "Your starcrystals lit the crossing signal, even when the river rises.",
                    "Your spare parts repaired the cart and restored the mine supply route." };
                string responseZh = Has("wayfarer_defended_" + room) ? defendedZh[room] :
                    Has("wayfarer_supplied_" + room) ? suppliedZh[room] :
                    "多亏你的帮助，我能继续赶路了。";
                string responseEn = Has("wayfarer_defended_" + room) ? defendedEn[room] :
                    Has("wayfarer_supplied_" + room) ? suppliedEn[room] :
                    "Thanks to you, I can travel again.";
                bool rewardPending = WayfarerFinalRewardPending;
                Dialog(o.name, o.en, responseZh +
                    (rewardPending ? " 三位旅人已经平安，请在背包留出一份炖汤的空间，再回来领取共同的奖励。" : "") +
                    (room==3&&MineConvoyComplete?
                        " 南侧矿棚最后一班矿工已经平安归来，矿工不再需要军用货运许可。":"")+
                    " 其他旅人也许还在等待救援。" + FloodArchiveWayfarerNote(false),
                    responseEn +
                    (room==3&&MineConvoyComplete?
                        " The last-shift miners returned safely; they no longer need a military cargo permit.":"")+
                    " Others may still be stranded." +
                    (rewardPending ? " All three travelers are safe. Free one stew slot and return to claim their shared reward." : "") +
                    FloodArchiveWayfarerNote(true),
                    rewardPending ? C("领取三人共同的补给", "Claim the travelers' shared supplies", "wayfarer_reward") :
                        C("旅途平安", "Safe travels", "close"),
                    C("稍后再见", "See you later", "close"));
                return true;
            }
            if (wayfarerEncounterRoom == room)
            {
                var options = new List<Choice> { C("继续守护", "Keep defending", "close") };
                if (wayfarerIntegrity < 100 && !wayfarerAidUsed)
                    options.Add(C("紧急援助：1份物资恢复35安全度", "Field aid: 1 supply restores 35 safety", "wayfarer_aid"));
                if(WayfarerCoverAvailable)
                    options.Add(C("布置临时掩护 · 耐力20 · 8秒减伤",
                        "Rally cover · 20 energy · 8s half damage",
                        "wayfarer_cover"));
                if(WayfarerDecoyAvailable)
                    options.Add(C("高声引开劫匪 · 耐力15 · 5秒",
                        "Draw raider attention · 15 energy · 5s",
                        "wayfarer_drawfire"));
                options.Add(C("撤离战斗", "Withdraw", "wayfarer_abandon"));
                Dialog(o.name, o.en, "伏击者袭击旅人！安全度 " + (int)Math.Ceiling(wayfarerIntegrity) + "/100。你可以站在敌人与旅人之间拦截攻击，也可靠近旅人花20耐力部署一次8秒减伤掩护。" + (wayfarerCoverTime>0?" 掩护剩余 "+(int)Math.Ceiling(wayfarerCoverTime)+" 秒。":"") + "受伤后还可用一份物资紧急援助。近身喊话可短暂引开真正听得见的劫匪。",
                    "Raiders attack the traveler! Safety " + (int)Math.Ceiling(wayfarerIntegrity) + "/100. Intercept real strikes, or rally once beside the traveler for 20 energy: 8s of half damage." + (wayfarerCoverTime>0?" Cover: "+(int)Math.Ceiling(wayfarerCoverTime)+"s left.":"") + " One supply can heal after injury. A close call can draw off raiders who genuinely hear you.",
                    options.ToArray());
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
            string[] supplyZh = { "", "药草×2 · 当地声望+1", "星晶×2 · 当地声望+1", "零件×2 · 当地声望+1" };
            string[] supplyEn = { "", "2 Moonherbs · trust +1", "2 starcrystals · trust +1", "2 spare parts · trust +1" };
            Dialog(o.name, o.en, zh[room]+FloodArchiveWayfarerNote(false), en[room]+FloodArchiveWayfarerNote(true),
                C(supplyZh[room], supplyEn[room], "wayfarer_supply_" + room),
                C("守护旅人 · 武器熟练度+4", "Defend traveler · mastery +4", "wayfarer_defend_" + room),
                C("稍后再说", "Prepare first", "close"));
            return true;
        }
        // Old dialogue choices may still be clickable after a gate
        // closes. Donations, field medicine, withdrawals and the shared
        // one-time stew are real NPC handovers, not radial telekinesis.
        private bool CanReachWayfarer(Point npc)
        { return PhysicalRefugeReach(npc,2.1001f); }
        // The same validated physical shout can be used while actually
        // fighting, without entering a paused NPC dialogue. A keypress
        // outside this encounter never spends energy, changes focus or
        // steals the surrounding world's existing skill buttons.
        public bool QuickDrawWayfarerFire()
        {
            if(mode!="world"||!WayfarerDecoyAvailable)return false;
            return ChooseWayfarer("wayfarer_drawfire");
        }
        private bool ChooseWayfarer(string action)
        {
            if(action=="wayfarer_drawfire")
            {
                if(!WayfarerDecoyAvailable)return true;
                List<Enemy> heard=WayfarerAudibleRaiders();
                if(heard.Count==0)return true;
                save.energy-=15f;
                wayfarerDecoyUsed=true;
                wayfarerDecoyTime=5f;
                wayfarerDecoyTargets.Clear();
                foreach(Enemy raider in heard)
                {
                    wayfarerDecoyTargets.Add(raider);
                    wayfarerDecoyLastSeen[raider]=Position;
                    effects.Add(new Effect("number",raider.Position,.75f,
                        L("转向玩家！","DRAWN TO HERO")){wayfarerOwned=true});
                }
                effects.Add(new Effect("discover",Position,.45f)
                    {wayfarerOwned=true});
                sound="wayfarerShout";revision++;
                Notify("成功引开 "+heard.Count+
                    " 名真正听到呼喊的劫匪！5秒内他们将追击你；已释放的攻击不会倒退取消。",
                    "Drew "+heard.Count+
                    " raiders who actually heard you! They pursue you for 5s; committed attacks still resolve.");
                return true;
            }
            if (action == "wayfarer_cover")
            {
                if(!WayfarerCoverAvailable)return true;
                Point traveler=ObjectPoint(wayfarerEncounterRoom,
                    "wayfarer"+wayfarerEncounterRoom,new Point(-100,-100));
                if(!CanReachWayfarer(traveler))return true;
                save.energy-=20f;wayfarerCoverUsed=true;
                wayfarerCoverTime=8f;
                effects.Add(new Effect("discover",traveler,.55f)
                    {wayfarerOwned=true});
                sound="guard";revision++;
                Notify("临时掩护已架起！8秒内旅人实际受击减半；近身拦截仍可完全保护旅人。",
                    "Cover rallied! Actual traveler strike damage halves for 8s. Bodyguard interception still prevents contact.");
                return true;
            }
            if (action == "wayfarer_reward")
            {
                if (!WayfarerFinalRewardPending || save.room < 1 ||
                    save.room > 3 || Dying || practicing) return true;
                Point source = ObjectPoint(save.room, "wayfarer" + save.room,
                    new Point(-100, -100));
                if (!CanReachWayfarer(source)) return true;
                ClaimWayfarerFinalReward();
                return true;
            }
            if (action == "wayfarer_aid")
            {
                if (wayfarerEncounterRoom == 0 || save.room != wayfarerEncounterRoom ||
                    wayfarerAidUsed || wayfarerIntegrity >= 100 || Dying || practicing) return true;
                Point aidTarget = ObjectPoint(wayfarerEncounterRoom, "wayfarer" + wayfarerEncounterRoom, new Point(-100, -100));
                if (!CanReachWayfarer(aidTarget)) return true;
                int item = WayfarerItem(wayfarerEncounterRoom);
                if (save.inventory[item] < 1)
                {
                    Notify("缺少紧急援助所需的物资。", "No supplies left for field aid."); return true;
                }
                save.inventory[item]--;
                wayfarerIntegrity = Math.Min(100, wayfarerIntegrity + 35);
                wayfarerAidUsed = true; sound = "reward"; revision++;
                effects.Add(new Effect("discover", aidTarget, .5f) {
                    wayfarerOwned = true });
                Notify("紧急援助成功！旅人最多恢复35点安全度（每次伏击限一次）。",
                    "Field aid applied! Restored up to 35 safety (once per ambush).");
                return true;
            }
            if (action == "wayfarer_abandon")
            {
                if (wayfarerEncounterRoom != 0 &&
                    save.room==wayfarerEncounterRoom &&
                    CanReachWayfarer(ObjectPoint(save.room,
                        "wayfarer"+save.room,new Point(-100,-100))))
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
                wayfarerEncounterRoom != 0 || roadTrialRoom != 0 ||
                atlasEchoRoom != 0 || practicing || Dying) return true;
            Point npc = ObjectPoint(room, "wayfarer" + room, new Point(-100, -100));
            if (!CanReachWayfarer(npc)) return true;
            // Do not stack a second live defense on top of a different field
            // mission during its between-wave lull, when SafeTravel may be true.
            if (fight && FieldWorkPreventsRest)
            {
                Notify("先完成当前现场任务，再开始新的旅人护卫。",
                    "Finish the active field mission before starting another traveler defense.");
                return true;
            }
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
            Point[] positions;
            if (!TryWayfarerAmbushFeet(npc,out positions))
            {
                Notify("附近没有足够安全的战斗空间；可选择物资救助。",
                    "Not enough clear space for an ambush; try donating supplies.");
                return true;
            }
            wayfarerEncounterRoom = room;
            wayfarerIntegrity = 100;
            wayfarerWarned = wayfarerAidUsed = false;
            wayfarerCoverUsed=false;wayfarerCoverTime=0f;
            wayfarerDecoyUsed=false;wayfarerDecoyTime=0f;
            wayfarerDecoyTargets.Clear();
            wayfarerDecoyLastSeen.Clear();
            wayfarerRaiders[0]=wayfarerRaiders[1]=null;
            wayfarerConfirmedKills = 0;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = new Enemy(82000 + room * 10 + slot, slot == 0 ? 0 : 2,
                    positions[slot].x, positions[slot].y, 50 + room * 16);
                // Reserved high IDs must never become multi-hour opening cooldowns.
                e.timer = .9f + slot * .3f;
                wayfarerRaiders[slot]=e;
                enemies.Add(e);
                effects.Add(new Effect("discover", positions[slot], .6f) {
                    wayfarerOwned = true });
            }
            TrackQuest("wayfarers"); sound = "quest"; revision++;
            Notify("守住旅人！可用身体拦截，或靠近旅人花20耐力部署一次8秒减伤掩护。",
                "Protect the traveler! Intercept strikes or rally one 8-second cover for 20 energy nearby.");
            return true;
        }
        // Test candidate actor roots against a FULL walkable local route to
        // the genuine civilian. NavigationPath can legally return a partial
        // fallback toward unreachable shore tiles and cannot justify a
        // real hostile spawn. Validate both raiders before changing flags.
        private bool TryWayfarerAmbushFeet(Point civilian,out Point[] result)
        {
            result=null;
            if(!CanStand(civilian))return false;
            var positions=new List<Point>();
            for(int radius=3;radius<=7&&positions.Count<2;radius++)
            for(int dx=-radius;dx<=radius&&positions.Count<2;dx++)
            for(int dy=-radius;dy<=radius&&positions.Count<2;dy++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point foot=civilian+new Point(dx,dy);
                if(!CanStand(foot)||
                   (foot-Position).Length<2.7f||
                   (save.companion>=0 &&
                    (foot-companionPosition).Length<1.55f)||
                   positions.Exists(p=>(p-foot).Length<3f)||
                   enemies.Exists(e=>e.hp>0&&
                        (e.Position-foot).Length<2.1f)||
                   !ActorHasLocalRoute(foot,civilian,12))
                    continue;
                positions.Add(foot);
            }
            if(positions.Count!=2)return false;
            result=positions.ToArray();
            return true;
        }
        // The player can physically bodyguard a committed traveler strike.
        // Interception only succeeds inside the actual enemy contact area:
        // it never makes the traveler invulnerable from behind a wall or at range.
        private bool InterceptWayfarerStrike(Enemy attacker, Point traveler)
        {
            Point lane = traveler - attacker.Position;
            Point blocker = Position - attacker.Position;
            float length = lane.Length;
            float radius = attacker.kind == 2 ? EnemyAreaRadius(attacker) : EnemyMeleeRadius;
            if (length < .7f || blocker.Length >= radius) return false;
            float forward = (blocker.x * lane.x + blocker.y * lane.y) / length;
            float lateral = Math.Abs(blocker.x * lane.y - blocker.y * lane.x) / length;
            if (forward < .3f || forward > length + .05f || lateral > .75f ||
                !ClearAttackPath(attacker.Position, Position)) return false;
            // The original release has already resolved the hero's real hit.
            // Heavy sweeps require the hero to be inside the warned area too.
            if (attacker.kind == 2
                ? (Position - attacker.target).Length >= radius
                : !InEnemyMeleeArc(attacker, Position)) return false;
            effects.Add(new Effect("number", Position, .7f, L("护卫拦截", "INTERCEPT")) {
                wayfarerOwned = true });
            effects.Add(new Effect("discover", traveler, .35f) {
                wayfarerOwned = true });
            return true;
        }
        // A traveler is a real defendable objective, not a passive kill counter.
        // Raider strikes require both proximity and an unobstructed attack path.
        private void DamageWayfarer(Enemy attacker)
        {
            if (wayfarerEncounterRoom == 0 || save.room != wayfarerEncounterRoom ||
                !IsWayfarerEnemy(attacker)) return;
            Point npc = ObjectPoint(wayfarerEncounterRoom, "wayfarer" + wayfarerEncounterRoom, new Point(-100, -100));
            if ((attacker.Position - npc).Length > 2.2f || !ClearAttackPath(attacker.Position, npc)) return;
            if (InterceptWayfarerStrike(attacker, npc)) return;
            int baseDamage=attacker.kind==2?24:18;
            int actualDamage=wayfarerCoverTime>0?(baseDamage+1)/2:baseDamage;
            wayfarerIntegrity=Math.Max(0,wayfarerIntegrity-actualDamage);
            if(wayfarerCoverTime>0)
                effects.Add(new Effect("number",npc,.45f,
                    L("掩护减伤 -"+(baseDamage-actualDamage),
                      "COVER -"+(baseDamage-actualDamage))) {wayfarerOwned=true});
            // The area warning belongs to enemy windup, before contact.
            // On an ACTUAL civilian hit show a brief impact, not a new
            // red prediction that lingers after damage has landed.
            effects.Add(new Effect("burst", npc, .24f) {
                radius = .55f, wayfarerOwned = true });
            if (wayfarerIntegrity <= 0)
            {
                enemies.RemoveAll(IsWayfarerEnemy);
                ResetWayfarerEncounter(); sound = "hurt"; revision++;
                Notify("旅人受伤，救援失败。没有损失物资，可以重新守护或改用补给。",
                    "The traveler was overwhelmed. No supplies lost; retry the defense or donate.");
            }
            else if (wayfarerIntegrity <= 35 && !wayfarerWarned)
            {
                wayfarerWarned = true;
                Notify("旅人陷入危险！迅速拦截剩余敌人。",
                    "Traveler in danger! Intercept the remaining raiders.");
            }
        }
        private void TickWayfarerEncounter(float dt)
        {
            if (wayfarerEncounterRoom == 0) return;
            if (save.room != wayfarerEncounterRoom || Dying || practicing)
            { ResetWayfarerEncounter(); return; }
            wayfarerCoverTime=Math.Max(0f,wayfarerCoverTime-dt);
            wayfarerDecoyTime=Math.Max(0f,wayfarerDecoyTime-dt);
            if(wayfarerDecoyTime<=0f)
            {
                wayfarerDecoyTargets.Clear();
                wayfarerDecoyLastSeen.Clear();
            }
            bool anyStillAlive=false;
            // Validate BOTH authored identities before waiting for either.
            // Previously a surviving first raider made this method return
            // early, letting the second quietly disappear without a real
            // lethal hit and leaving the rescue in a broken active state.
            for(int i=0;i<2;i++)
            {
                Enemy owned=wayfarerRaiders[i];
                bool present=owned!=null&&owned.hp>0&&
                    enemies.Contains(owned);
                if(!present&&(wayfarerConfirmedKills&(1<<i))==0)
                {
                    ResetWayfarerEncounter();revision++;
                    Notify("伏击者异常离场，尚未确认真正击败。救援可免费重试。",
                        "A raider vanished without defeat. Retry the traveler defense for free.");
                    return;
                }
                if(present)anyStillAlive=true;
            }
            if(anyStillAlive)return;
            int room = wayfarerEncounterRoom;
            ResetWayfarerEncounter(); CompleteWayfarer(room, true);
        }
        private void CompleteWayfarer(int room, bool defended = false)
        {
            if (Has("wayfarer_" + room)) return;
            Flag("wayfarer_" + room);
            // Existing string flags persist the chosen rescue method without
            // changing the save schema or overwriting older completed saves.
            Flag((defended ? "wayfarer_defended_" : "wayfarer_supplied_") + room);
            // Material generosity earns faction trust; fighting develops mastery.
            if (defended)
                save.mastery[save.weapon] = Math.Min(100, save.mastery[save.weapon] + 4);
            else
                Reputation(room == 1 ? 2 : room == 2 ? 1 : 0, 1);
            CreditCoins(20); CreditShards(1); AddXp(25); revision++;
            RecordMoment("帮助第 " + room + " 位失联旅人：获得20硬币、1碎片、25经验。",
                "Helped traveler " + room + ": 20 coins, 1 shard, 25 XP.");
            if (WayfarerFinalRewardPending)
            {
                if (!ClaimWayfarerFinalReward())
                    Notify("三位旅人都已获救。炖汤背包已满；腾出一格后与任一旅人交谈领取最终奖励。",
                        "All three travelers are safe. Your stew stack is full; free one slot and speak to any rescued traveler to claim the final reward.");
            }
            else Notify(defended ?
                    "守护成功！20硬币、1碎片、25经验；当前武器熟练度+4。" :
                    "补给成功！20硬币、1碎片、25经验；当地阵营声望+1。",
                defended ? "Defense complete! 20 coins, 1 shard, 25 XP; weapon mastery +4." :
                    "Supplies delivered! 20 coins, 1 shard, 25 XP; local faction trust +1.");
            TrackQuest("wayfarers"); sound = "reward";
        }
    }
}
