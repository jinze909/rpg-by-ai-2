using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Existing hostile archetypes populate actual fifth-ring clearings.
        // All locations are off the historical story/Boss arena and at least
        // 12 tiles from new interactive ridge endpoints. No fake silhouettes.
        // Spawn IDs persist in v2 world snapshots. Progress lives in the
        // existing string flags and does not alter the serialized schema.
        private readonly Dictionary<int,int> ridgeWatchIds = new Dictionary<int,int>();
        private int ridgeWatchRoom = -1;
        private MapData ridgeWatchMap;
        private void ResetRidgeWatch()
        {
            ridgeWatchIds.Clear();ridgeWatchRoom=-1;ridgeWatchMap=null;
        }
        public int RidgeWatchProgress(int room)
        {
            if(room<1||room>7)return 0;
            int count=0;
            for(int i=0;i<3;i++)
                if(Has("ridge_watch_"+room+"_"+i))count++;
            return count;
        }
        public bool RidgeWatchComplete(int room)
        { return room>=1&&room<=7&&Has("ridge_watch_complete_"+room); }
        private string RidgeWatchHint(int room,bool english)
        {
            if(room<1||room>7||!RidgeLinked(room)||!Has("boss_"+room))
                return "";
            if(RidgeWatchComplete(room))
                return english?"\\n\\nRidge watch: cleared. +2 energy/sec while exploring the outer high trails.":
                    "\\n\\n高地巡守：已完成。在外环高处步道探索时耐力恢复 +2/秒。";
            int available=ridgeWatchRoom==room&&
                Object.ReferenceEquals(ridgeWatchMap,Map)?ridgeWatchIds.Count:3;
            return english?"\\n\\nRidge watch: clear the real hostile patrols beyond the markers ("+
                RidgeWatchProgress(room)+"/"+available+"). First-clear supplies await.":
                "\\n\\n高地巡守：清除路标外的实际巡敌（"+
                RidgeWatchProgress(room)+"/"+available+"）。首次完成有补给奖励。";
        }
        private void OnRidgeWatchEnemyDefeated(Enemy enemy)
        {
            if(enemy==null||save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(Map,ridgeWatchMap)||
                !RidgeLinked(save.room)||!Has("boss_"+save.room))return;
            int index;
            if(!ridgeWatchIds.TryGetValue(enemy.id,out index))return;
            string flag="ridge_watch_"+save.room+"_"+index;
            if(!Has(flag))Flag(flag);
            if(RidgeWatchComplete(save.room)||ridgeWatchIds.Count==0)return;
            foreach(int place in ridgeWatchIds.Values)
                if(!Has("ridge_watch_"+save.room+"_"+place))return;
            Flag("ridge_watch_complete_"+save.room);
            CreditCoins(28);CreditShards(1);AddXp(40);
            RecordMoment("你清除了偏远巡路上威胁行人的敌人，巡路队把这条通道开放给更多居民。",
                "The hostile high-trail patrols were defeated in combat. The roadwardens reopened the passage to civilian travelers.");
            Notify("高地巡守完成：硬币 +28、碎片 +1、经验 +40；外环高路耐力恢复 +2/秒。",
                "Ridge watch complete: +28 coins, +1 shard, +40 XP; outer high-trail energy +2/sec.");
            sound="reward";revision++;
        }
        private static readonly Point[] RidgeWildlifeSpawns = {
            new Point(101.5f,61.5f),
            new Point(89.5f,67.5f),
            new Point(76.5f,66.5f)
        };
        // The last landmark and the guardian's defeat can each unlock this
        // hunt while the hero is STILL in the active scene. An idempotent
        // check avoids duplicating world enemies or replacing a mid-hunt
        // enemy identity on a repeated dialogue or combat callback.
        private void EnsureRidgeWatchPatrols()
        {
            if(save.room<1||save.room>7||!RidgeLinked(save.room)||
                !Has("boss_"+save.room))return;
            if(ridgeWatchRoom==save.room&&
                Object.ReferenceEquals(ridgeWatchMap,Map))return;
            SpawnRidgeWildlife();
        }
        private void SpawnRidgeWildlife()
        {
            ResetRidgeWatch();
            // Never spawn in the safe starting town, inn or procedural abyss.
            if(save.room<1||save.room>7||!RidgeLinked(save.room)||
                !Has("boss_"+save.room))return;
            // Late-game wildlife never expands the required combat roster
            // for an unfinished regional guardian or an untouched story gate.
            // They return only after both ridge bearings AND the original
            // region guardian have been resolved on this save slot.
            ridgeWatchRoom=save.room;ridgeWatchMap=Map;
            for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
            {
                Point position=RidgeWildlifeSpawns[i];
                if(!CanStand(position))continue;
                bool adjacentToObjective=false;
                foreach(WorldObject o in Map.objects)
                    if((new Point(o.x,o.y)-position).Length<8)
                    { adjacentToObjective=true;break; }
                if(adjacentToObjective)continue;
                Enemy scout=CreatePlacedEnemy(++enemyId,(save.room+i)%3,
                    position,30+save.room*7+i*9);
                enemies.Add(scout);
                ridgeWatchIds[scout.id]=i;
            }
        }
    }
}
