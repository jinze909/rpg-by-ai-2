using System;
namespace Starfall
{
    // Voluntary, authentic combat challenges placed in the new map ring.
    // The fight is transient. Only a legitimate clear earns a persistent v2 flag.
    public sealed partial class GameCore
    {
        private int outerPatrolRoom=-1;
        private int outerPatrolEnemyA=-1,outerPatrolEnemyB=-1;
        public bool OuterPatrolActive { get { return outerPatrolRoom==save.room; } }
        public bool OuterPatrolDone(int room)
        { return room>=0&&room<8&&Has("outer_patrol_done_"+room); }
        public int OuterPatrolWins
        {
            get { int n=0;for(int i=0;i<8;i++)if(OuterPatrolDone(i))n++;return n; }
        }
        private void ResetOuterPatrol()
        { outerPatrolRoom=outerPatrolEnemyA=outerPatrolEnemyB=-1; }
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
            if(post==null||(Position-new Point(post.x,post.y)).Length>2.2f)return true;
            if(!SafeTravel)
            {
                Notify("先清理当前追击你的敌人。","Clear the nearby pursuers first.");
                return true;
            }
            Point left=new Point(post.x-2f,post.y);
            Point right=new Point(post.x+1f,post.y-1f);
            if(!CanStand(left)||!CanStand(right))return true;
            outerPatrolRoom=room;
            Enemy one=CreatePlacedEnemy(++enemyId,0,left,48+room*10);
            Enemy two=CreatePlacedEnemy(++enemyId,1,right,42+room*10);
            outerPatrolEnemyA=one.id;outerPatrolEnemyB=two.id;
            enemies.Add(one);enemies.Add(two);
            revision++;sound="quest";
            Notify("救援防卫开始：击退两名真实敌人！",
                "Relief defense started: defeat both attackers!");
            return true;
        }
        private void TickOuterPatrol()
        {
            if(outerPatrolRoom<0)return;
            if(outerPatrolRoom!=save.room||Dying)
            {
                ResetOuterPatrol();return;
            }
            foreach(Enemy e in enemies)
                if(e.id==outerPatrolEnemyA||e.id==outerPatrolEnemyB)return;
            int completedRoom=outerPatrolRoom;
            ResetOuterPatrol();
            if(OuterPatrolDone(completedRoom))return;
            Flag("outer_patrol_done_"+completedRoom);
            save.coins+=14;save.shards++;
            AddXp(28);
            if(OuterPatrolWins==8&&!Has("outer_patrol_all"))
            { Flag("outer_patrol_all");save.skillPoints++; }
            revision++;sound="reward";
            RecordMoment("八地救援岗中的第 "+(completedRoom+1)+" 处已经守住：真正的危险被击退，居民不再只能等待。",
                "A relief outpost in region "+(completedRoom+1)+
                " has been defended against actual attackers. The supplies can reach residents.");
            Notify("救援岗已守住：硬币 +14、碎片 +1、经验 +28。",
                "Relief post defended: +14 coins, +1 shard, +28 XP.");
        }
    }
}
