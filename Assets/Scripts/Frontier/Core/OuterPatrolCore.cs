using System;
namespace Starfall
{
    // Voluntary, authentic combat challenges placed in the new map ring.
    // The fight is transient. Only a legitimate clear earns a persistent v2 flag.
    public sealed partial class GameCore
    {
        private int outerPatrolRoom=-1;
        private int outerPatrolEnemyA=-1,outerPatrolEnemyB=-1;
        private bool outerPatrolADead,outerPatrolBDead;
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
            outerPatrolADead=outerPatrolBDead=false;
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
                    "盗取补给的暴走兽就在附近。先完成实际战斗，才能报告救援成功。",
                    "Corrupted beasts are approaching the stores. Defeat them in real combat before claiming a defended route.",
                    C("继续防卫","Continue the defense","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "救援信号引来了需要帮助的人，也引来了追逐补给的暴走兽。你可以在这里主动组织一场短途防卫：两只敌人会实际攻击，只有击退它们才算保护成功。\n\n失败或离开会取消本次尝试；此前记录与物资不会消失。",
                "The relit beacon helps lost travelers, but also draws beasts toward the supplies. Start a voluntary two-enemy defense here. Only real defeated attackers count.\n\nLeaving or falling ends this attempt, without deleting earlier exploration progress.",
                C("守护物资 · 开始战斗","Defend supplies · begin","outer_watch_start"),
                C("以后再来","Not now","close"));
            return true;
        }
        private bool ChooseOuterPatrol(string action)
        {
            if(action!="outer_watch_start")return false;
            int room=save.room;
            if(room<0||room>=8||OuterPatrolActive||OuterPatrolDone(room)||
                !OuterCacheRecovered(room)||Dying||practicing)return true;
            WorldObject post=null;
            foreach(WorldObject o in Map.objects)if(o.kind=="outerwatch"){post=o;break;}
            if(post==null||!PhysicalRefugeReach(
                new Point(post.x,post.y),2.2001f))return true;
            if(!SafeTravel)
            {
                Notify("先清理当前追击你的敌人。","Clear the nearby pursuers first.");
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
        private void TickOuterPatrol()
        {
            if(outerPatrolRoom<0)return;
            if(outerPatrolRoom!=save.room||Dying)
            {
                ResetOuterPatrol();return;
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
