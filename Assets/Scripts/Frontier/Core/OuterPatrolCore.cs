using System;
namespace Starfall
{
    // Voluntary, authentic combat challenges placed in the new map ring.
    // The fight is transient. Only a legitimate clear earns a persistent v2 flag.
    public sealed partial class GameCore
    {
        private int outerPatrolRoom=-1;
        private int outerPatrolEnemyA=-1,outerPatrolEnemyB=-1;
        private bool outerPatrolADead,outerPatrolBDead,outerPatrolPatched;
        private float outerPatrolTime,outerPatrolStock;
        private Point outerPatrolPost;
        public float OuterPatrolIntegrity {get{return OuterPatrolActive?outerPatrolStock:0f;}}
        public float OuterPatrolTime {get{return OuterPatrolActive?outerPatrolTime:0f;}}
        public bool OuterPatrolPatchAvailable
        {get{return OuterPatrolActive&&!outerPatrolPatched&&
            outerPatrolStock>0f&&outerPatrolStock<100f&&
            save.inventory[0]>0&&save.inventory[2]>0;}}
        public bool OuterPatrolActive { get { return outerPatrolRoom==save.room; } }
        public bool OuterPatrolDone(int room)
        { return room>=0&&room<8&&Has("outer_patrol_done_"+room); }
        public int OuterPatrolWins
        {
            get { int n=0;for(int i=0;i<8;i++)if(OuterPatrolDone(i))n++;return n; }
        }
        private void ResetOuterPatrol()
        {
            // An interrupted defense must retire its actual attackers, not
            // merely forget their IDs and leave orphan combat in the world.
            // Also retire their already released arrows and pending cues;
            // unrelated wilderness patrols and player attacks survive.
            if(outerPatrolRoom>=0)
                for(int i=enemies.Count-1;i>=0;i--)
                    if(enemies[i].id==outerPatrolEnemyA||
                       enemies[i].id==outerPatrolEnemyB)
                    {
                        RemoveEnemyPendingWarning(enemies[i]);
                        enemies.RemoveAt(i);
                    }
            projectiles.RemoveAll(p=>p.outerPatrolOwned);
            effects.RemoveAll(e=>e.outerPatrolOwned);
            outerPatrolRoom=outerPatrolEnemyA=outerPatrolEnemyB=-1;
            outerPatrolADead=outerPatrolBDead=outerPatrolPatched=false;
            outerPatrolStock=outerPatrolTime=0f;
            outerPatrolPost=new Point();
        }
        private bool IsOuterPatrolEnemy(Enemy e)
        {
            return outerPatrolRoom==save.room &&
                (e.id==outerPatrolEnemyA||e.id==outerPatrolEnemyB);
        }
        private void OnOuterPatrolEnemyDefeated(Enemy e)
        {
            // Invoked *only* after real Hit() reduces HP to zero. A list clear,
            // transition, despawn or save load can never forge a victory.
            if(!IsOuterPatrolEnemy(e))return;
            if(e.id==outerPatrolEnemyA)outerPatrolADead=true;
            if(e.id==outerPatrolEnemyB)outerPatrolBDead=true;
        }
        // Only a genuine released melee swing can damage this physical cache.
        // No passive radius drain, through-wall attrition, or fake projectile hits.
        private void DamageOuterPatrolStock(Enemy attacker)
        {
            if(!OuterPatrolActive||attacker==null||!IsOuterPatrolEnemy(attacker)||
                !attacker.targetingOuterPatrolStock||attacker.hp<=0||attacker.kind==1)return;
            if((attacker.Position-outerPatrolPost).Length>2.35f||
                !CanStand(attacker.Position)||
                !ClearAttackPath(attacker.Position,outerPatrolPost))return;
            float damage=attacker.kind==2?22f:14f;
            outerPatrolStock=Math.Max(0f,outerPatrolStock-damage);
            effects.Add(new Effect("number",outerPatrolPost,.75f,
                L("物资受损 -"+(int)damage,"SUPPLIES -"+(int)damage))
                {outerPatrolOwned=true});
            revision++;sound="hit";
            if(outerPatrolStock<=0f)
            {
                ResetOuterPatrol();revision++;
                Notify("救援物资在真实袭击中失守！探索记录仍在，可重新组织防卫。",
                    "Raiders destroyed the relief defense! The original supplies and exploration record remain; retry anytime.");
            }
            else Notify("救援物资遭到袭击，剩余耐久 "+Math.Ceiling(outerPatrolStock)+"%。",
                "Relief supplies under attack: "+Math.Ceiling(outerPatrolStock)+"% integrity left.");
        }
        private bool TalkOuterPatrol(WorldObject o)
        {
            if(o.kind!="outerwatch")return false;
            int room=save.room;
            if(room<0||room>=8||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            if(OuterPatrolDone(room))
            {
                Dialog(o.name,o.en,
                    "你保护过的救援物资正在交给真实需要它的人。这里的哨岗仍然对平民开放。",
                    "The supplies you defended now reach the people who need them. This outpost remains open to civilians.",
                    C("继续巡路","Keep exploring","close"));
                return true;
            }
            if(!OuterCacheRecovered(room))
            {
                Dialog(o.name,o.en,
                    "此处还没有可以守护的物资。先调查南部记录，修复求援灯，找回东南方的救援箱。",
                    "No supplies have arrived yet. Recover the southern log, repair the eastern relay, then retrieve the relief cache.",
                    C("查找信号","Find the signal","close"));
                return true;
            }
            if(OuterPatrolActive)
            {
                Dialog(o.name,o.en,
                    "盗取补给的暴走兽就在附近。守住物资直到两名袭击者落败：剩余 "+Math.Ceiling(outerPatrolTime)+" 秒，物资耐久 "+Math.Ceiling(outerPatrolStock)+"%。可以使用木材和草药抢修一次。",
                    "Raiders are attacking the stores! Defeat both before the "+Math.Ceiling(outerPatrolTime)+"s deadline; stock integrity "+Math.Ceiling(outerPatrolStock)+"%. Timber and Moonherb can patch it once.",
                    C("临时加固 · 木材和草药各1","Emergency patch · timber + Moonherb","outer_watch_patch"),
                    C("主动撤守","Withdraw defense","outer_watch_abort"),
                    C("继续防卫","Continue the defense","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "救援信号引来了需要帮助的人，也引来了追逐补给的暴走兽。主动组织一场 95 秒的防卫：近战袭击者会冲向物资，贴身阻截才能吸引它。物资耐久归零、超时、远离或倒下均需重试。\n\n此前记录与物资不会消失。",
                "The relit beacon draws raiders. Defend for up to 95 seconds: melee attackers damage the supplies unless intercepted at close range. Losing the cache, timing out, leaving or falling cancels the attempt.\n\nExisting exploration and relief supplies remain safe.",
                C("守护物资 · 开始战斗","Defend supplies · begin","outer_watch_start"),
                C("以后再来","Not now","close"));
            return true;
        }
        private bool ChooseOuterPatrol(string action)
        {
            if(action=="outer_watch_patch"||action=="outer_watch_abort")
            {
                if(!OuterPatrolActive||Dying||practicing)return true;
                WorldObject cache=null;
                foreach(WorldObject candidate in Map.objects)
                    if(candidate.kind=="outerwatch"){cache=candidate;break;}
                if(cache==null||!PhysicalRefugeReach(
                    new Point(cache.x,cache.y),2.2001f))return true;
                if(action=="outer_watch_abort")
                {
                    ResetOuterPatrol();revision++;
                    Notify("本次防卫已结束。救援记录和物资仍然保留。",
                        "Defense withdrawn. Relief records and supplies are intact.");
                    return true;
                }
                if(!OuterPatrolPatchAvailable||!Spend(1,0,1,0,0))return true;
                outerPatrolStock=Math.Min(100f,outerPatrolStock+30f);
                outerPatrolPatched=true;revision++;sound="reward";
                Notify("临时加固完成：木材、月光草各 -1，物资耐久 +30。",
                    "Emergency patch complete: -1 timber, -1 Moonherb, +30 stock integrity.");
                return true;
            }
            if(action!="outer_watch_start")return false;
            int room=save.room;
            if(room<0||room>=8||OuterPatrolActive||OuterPatrolDone(room)||
                !OuterCacheRecovered(room)||Dying||practicing)return true;
            WorldObject post=null;
            foreach(WorldObject o in Map.objects)if(o.kind=="outerwatch"){post=o;break;}
            if(post==null||!PhysicalRefugeReach(
                new Point(post.x,post.y),2.2001f))return true;
            if(!SafeTravel||FieldWorkPreventsRest)
            {
                Notify("先结束其它现场任务并清理附近追击者。","Finish other active field missions and clear nearby pursuers first.");
                return true;
            }
            Point[] feet;
            if(!TryOuterPatrolFeet(post,out feet))
            {
                Notify("救援岗暂时找不到两处可达且不被占用的安全战斗落点。清出场地后可重试。",
                    "Two reachable, unoccupied raider roots are unavailable. Clear the outpost and retry.");
                return true;
            }
            outerPatrolRoom=room;
            outerPatrolTime=95f;outerPatrolStock=100f;outerPatrolPatched=false;
            outerPatrolPost=new Point(post.x,post.y);
            Enemy one=CreatePlacedEnemy(++enemyId,0,feet[0],48+room*10);
            Enemy two=CreatePlacedEnemy(++enemyId,1,feet[1],42+room*10);
            outerPatrolEnemyA=one.id;outerPatrolEnemyB=two.id;
            outerPatrolADead=outerPatrolBDead=false;
            enemies.Add(one);enemies.Add(two);
            revision++;sound="quest";
            Notify("救援防卫开始：击退两名真实敌人！",
                "Relief defense started: defeat both attackers!");
            return true;
        }
        private bool TryOuterPatrolFeet(WorldObject post,out Point[] result)
        {
            result=null;
            Point center=new Point(post.x,post.y);
            if(!CanStand(center))return false;
            // Preserve original dramatic approaches first, but do not put
            // attackers in blocked pockets, inside a friend, or overlapping
            // a pre-existing living enemy. Every chosen foot must have a
            // complete route into the actual storehouse being defended.
            Point[] offsets={new Point(-2f,0),new Point(1f,-1f),
                new Point(-3f,1f),new Point(2f,1f),
                new Point(-2f,-2f),new Point(3f,0),
                new Point(0,-3f),new Point(0,3f),
                new Point(-3f,-2f),new Point(3f,2f),
                new Point(3f,-2f),new Point(-2f,3f)};
            var feet=new System.Collections.Generic.List<Point>();
            foreach(Point offset in offsets)
            {
                Point p=center+offset;
                if(!CanStand(p)||
                    (p-Position).Length<1.55f||
                    (save.companion>=0&&
                     (p-companionPosition).Length<1.55f)||
                    feet.Exists(other=>(other-p).Length<2.7f)||
                    enemies.Exists(e=>e.hp>0&&
                        (e.Position-p).Length<1.8f)||
                    !ActorHasLocalRoute(p,center,10))
                    continue;
                feet.Add(p);
                if(feet.Count==2)break;
            }
            if(feet.Count!=2)return false;
            result=feet.ToArray();
            return true;
        }
        private void TickOuterPatrol(float dt)
        {
            if(outerPatrolRoom<0)return;
            if(outerPatrolRoom!=save.room||Dying)
            {
                ResetOuterPatrol();return;
            }
            if((Position-outerPatrolPost).Length>12f)
            {
                ResetOuterPatrol();revision++;
                Notify("离开救援岗防区，防卫结束。物资不会永久丢失。",
                    "Defense perimeter abandoned. Relief supplies are still available for another attempt.");
                return;
            }
            outerPatrolTime=Math.Max(0f,outerPatrolTime-dt);
            if(outerPatrolTime<=0f)
            {
                ResetOuterPatrol();revision++;
                Notify("防卫时间结束，救援岗尚未守住。可以重新挑战。",
                    "Relief defense timed out. Retry the outpost when ready.");
                return;
            }
            // Any unexpected removal aborts the attempt. Only registered
            // lethal damage against BOTH owned attackers can settle it.
            bool presentA=enemies.Exists(e=>e.id==outerPatrolEnemyA);
            bool presentB=enemies.Exists(e=>e.id==outerPatrolEnemyB);
            if((!presentA&&!outerPatrolADead)||(!presentB&&!outerPatrolBDead))
            {
                ResetOuterPatrol();
                Notify("守卫战中断；只有击退两只敌人才算成功。",
                    "Defense interrupted. Only actual defeated attackers count.");
                return;
            }
            if(!outerPatrolADead||!outerPatrolBDead||presentA||presentB)return;
            int completedRoom=outerPatrolRoom;
            ResetOuterPatrol();
            if(OuterPatrolDone(completedRoom))return;
            Flag("outer_patrol_done_"+completedRoom);
            CreditCoins(14);CreditShards(1);
            AddXp(28);
            if(OuterPatrolWins==8&&!Has("outer_patrol_all"))
            { Flag("outer_patrol_all");CreditSkillPoints(1); }
            revision++;sound="reward";
            RecordMoment("八地救援岗中的第 "+(completedRoom+1)+" 处已经守住：真正的危险被击退，居民不再只能等待。",
                "A relief outpost in region "+(completedRoom+1)+
                " has been defended against actual attackers. The supplies can reach residents.");
            Notify("救援岗已守住：硬币 +14、碎片 +1、经验 +28。",
                "Relief post defended: +14 coins, +1 shard, +28 XP.");
        }
    }
}
