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
        // Original local civilian accounts become public only after real
        // patrol combat and are preserved as normal v2 chronicle moments.
        private static readonly string[] ridgeWatchStoryZh = {
            "林道威胁消失后，巡路队发现了孩子们留在荆棘下的脚印。新路终于标出家人真正走过的方向，而不是只有堡垒批准的通道。",
            "遗迹高地重新开放。摆渡人把洪水季未登记的归乡航线刻在公共水尺上，谁都能靠这份记录找到渡口。",
            "矿场上方的步道恢复安全。工人把三处急停铃连成真正的回家路，下一次警报不会再让人被困在机器旁。",
            "堡垒外环的旧哨站交还居民。这里的灯会为夜归的避难者亮起，不再只为军令和封锁服务。",
            "雾港高地重新通行。夜航人把最早发现的潮位刻在灯塔下，救援小艇不必等到官方命令才出海。",
            "沙海巡敌被清理后，未登记的绿洲家庭重新竖起补水木牌。商队和村民终于共享同一条找井路线。",
            "霜钟的声音重新传到暖棚。曾被拒于哨线外的旅人，如今可以循着钟声找到一处有人守候的炉火。"
        };
        private static readonly string[] ridgeWatchStoryEn = {
            "After the Thornwood patrol falls, roadwardens find children's footprints beneath the thorns. The new trail marks where families truly walked, not only where citadel permits allowed.",
            "The ruins' high ground opens again. Ferrymen carve unregistered flood-season crossings onto a public tide gauge so every traveler can find the ferry.",
            "Workers reopen the high route above Ironreach. Three emergency bells mark a real way home, so the next alarm will not leave anyone trapped beside running machines.",
            "The closed citadel lookout returns to civilian hands. Its lamps will guide travelers home instead of enforcing military orders alone.",
            "Mistport's high trail is passable again. Sailors carve early tide readings below the lighthouse so rescue skiffs can sail before official orders arrive.",
            "With Glasswind's patrols gone, unregistered oasis families raise shared water signs so both caravans and residents can find the next well.",
            "The frost chimes reach the warming shelters again. Travelers once refused at checkpoints can now follow the bells to a hearth with someone waiting."
        };
        private readonly Dictionary<int,int> ridgeWatchIds = new Dictionary<int,int>();
        // Spawn IDs are not unique actor identity: saves and optional
        // encounters can legitimately have overlapping numeric IDs.
        private readonly Dictionary<int,Enemy> ridgeWatchActors =
            new Dictionary<int,Enemy>();
        private float ridgeWatchRespawnCheck;
        private int ridgeWatchRoom = -1;
        private MapData ridgeWatchMap;
        // Real patrols call nearby teammates after genuine HP contact.
        // Recruitment is a one-time, physical-route check at the moment
        // the alarm sounds, not a per-frame scan through sealed terrain.
        // Actor references, owner map and a short clock are transient:
        // SaveData v2, other encounters and restored enemy IDs are untouched.
        private readonly HashSet<Enemy> ridgeWatchResponders=new HashSet<Enemy>();
        private float ridgeWatchAlarmSeconds;
        private Enemy ridgeWatchCaller;
        private float ridgeWatchCallSeconds;
        // The reported sighting is an actual hero foot at the time of an
        // authentic damage contact. Hearing a whistle does not give unseen
        // patrols permanent access to the hero's changing coordinates.
        private Point ridgeWatchReportedHeroFoot;
        // An actual injured body must FINISH its telegraphed warning:
        // defeating or stunning it beforehand stops the reinforcement.
        public bool RidgeWatchCallPending
        { get { return ridgeWatchCaller!=null&&ridgeWatchCallSeconds>0f&&
            IsActiveRidgeWatchPatrol(ridgeWatchCaller); } }
        public bool IsRidgeWatchCaller(Enemy actor)
        {
            return actor!=null&&RidgeWatchCallPending&&
                System.Object.ReferenceEquals(actor,ridgeWatchCaller);
        }
        public float RidgeWatchCallSecondsLeft
        { get { return RidgeWatchCallPending?
            Math.Max(0f,ridgeWatchCallSeconds):0f; } }
        public bool RidgeWatchAlarmActive
        {
            get { return ridgeWatchAlarmSeconds>0f &&
                ridgeWatchRoom==save.room&&
                System.Object.ReferenceEquals(ridgeWatchMap,Map); }
        }
        public int RidgeWatchRespondingCount
        {
            get
            {
                if(!RidgeWatchAlarmActive)return 0;
                int count=0;
                foreach(Enemy e in ridgeWatchResponders)
                    if(IsActiveRidgeWatchPatrol(e))count++;
                return count;
            }
        }
        private bool RidgeWatchRespondsToAlarm(Enemy e)
        { return RidgeWatchAlarmActive&&ridgeWatchResponders.Contains(e)&&
            IsActiveRidgeWatchPatrol(e); }
        // The production world draw follows EXACT encounter-owned actor
        // identity. An unrelated body that reuses a numeric id cannot be
        // presented as a teammate answering the ridge watch whistle.
        public bool IsRidgeWatchResponder(Enemy e)
        { return e!=null&&RidgeWatchRespondsToAlarm(e); }
        private Point RidgeWatchPursuitFoot(Enemy enemy,Point heroFoot)
        {
            if(!RidgeWatchRespondsToAlarm(enemy))return heroFoot;
            // Normal close/visible aggro resumes naturally. Otherwise the
            // responder searches the ACTUAL reported strike location,
            // never a live coordinate behind sealed terrain or across town.
            if((enemy.Position-heroFoot).Length<=8f&&
                ClearAttackPath(enemy.Position,heroFoot))return heroFoot;
            return CanStand(ridgeWatchReportedHeroFoot)?
                ridgeWatchReportedHeroFoot:enemy.Position;
        }
        private void AlertRidgeWatchOnRealHit(Enemy struck)
        {
            if(!IsActiveRidgeWatchPatrol(struck))return;
            if(RidgeWatchAlarmActive)
            {
                // A NEW genuine incoming hit supplies fresh information.
                // Hidden movement alone never updates the squad's sighting.
                ridgeWatchReportedHeroFoot=Position;
                ridgeWatchAlarmSeconds=Math.Max(ridgeWatchAlarmSeconds,3f);
                return;
            }
            // Do not allow rapid-hit spam to renew the original .85s call.
            if(ridgeWatchCaller!=null||struck.stun>.0001f)return;
            ridgeWatchCaller=struck;
            ridgeWatchCallSeconds=.85f;
            ridgeWatchReportedHeroFoot=Position;
            effects.Add(new Effect("number",struck.Position,.85f,
                L("巡敌正在呼援！","PATROL CALLING!")));
            revision++;
        }
        private void ReleaseRidgeWatchAlarm(Enemy struck)
        {
            if(!IsActiveRidgeWatchPatrol(struck))return;
            ridgeWatchAlarmSeconds=6f;
            ridgeWatchResponders.Clear();
            foreach(Enemy ally in enemies)
            {
                if(!IsActiveRidgeWatchPatrol(ally))continue;
                if(System.Object.ReferenceEquals(ally,struck))
                {ridgeWatchResponders.Add(ally);continue;}
                if((ally.Position-struck.Position).Length>17f||
                    !CanStand(ally.Position)||!CanStand(struck.Position))
                    continue;
                // A wall, flood seal or raised gate must defeat an alarm.
                // This same genuine walking BFS guides combat pursuit.
                if(ClearAttackPath(ally.Position,struck.Position)||
                    NavigationPathFrom(ally.Position,struck.Position,true).Count>0)
                    ridgeWatchResponders.Add(ally);
            }
            if(ridgeWatchResponders.Count>1)
            {
                effects.Add(new Effect("number",struck.Position,.75f,
                    L("巡敌呼援！","PATROL ALERT!")));
                revision++;sound="quest";
            }
        }
        private void ResetRidgeWatch()
        {
            ridgeWatchIds.Clear();ridgeWatchActors.Clear();
            ridgeWatchResponders.Clear();ridgeWatchAlarmSeconds=0f;
            ridgeWatchCaller=null;ridgeWatchCallSeconds=0f;
            ridgeWatchReportedHeroFoot=new Point();
            ridgeWatchRespawnCheck=0f;ridgeWatchRoom=-1;ridgeWatchMap=null;
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
        // The actual world renderer asks GameCore, not an enemy-ID range,
        // whether this living combat body still belongs to an unfinished
        // patrol. Never paint a quest ring on unrelated or already credited
        // actors, even if old save flags are present.
        public bool IsActiveRidgeWatchPatrol(Enemy enemy)
        {
            if(enemy==null||enemy.hp<=0f||save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(ridgeWatchMap,Map)||
                !RidgeLinked(save.room)||!Has("boss_"+save.room)||
                RidgeWatchComplete(save.room))return false;
            int index;Enemy actual;
            return ridgeWatchIds.TryGetValue(enemy.id,out index)&&
                ridgeWatchActors.TryGetValue(enemy.id,out actual)&&
                Object.ReferenceEquals(actual,enemy)&&
                !Has("ridge_watch_"+save.room+"_"+index);
        }
        // A blocked live clearing must not hide an outstanding third kill.
        // The map can reopen while the hero remains in the same scene.
        private int RidgeWatchPatrolsAwaitingFooting(int room)
        {
            if(room!=ridgeWatchRoom||save.room!=room||
                !Object.ReferenceEquals(ridgeWatchMap,Map)||
                RidgeWatchComplete(room))return 0;
            int count=0;
            for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
                if(!Has("ridge_watch_"+room+"_"+i)&&
                    !ridgeWatchIds.ContainsValue(i))count++;
            return count;
        }
        private Point? RidgeWatchNextMissingFoot(int room)
        {
            if(room!=ridgeWatchRoom||save.room!=room||
                !Object.ReferenceEquals(ridgeWatchMap,Map))return null;
            for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
                if(!Has("ridge_watch_"+room+"_"+i)&&
                    !ridgeWatchIds.ContainsValue(i))
                    return RidgeWildlifeSpawns[i];
            return null;
        }
        // World snapshots reconstruct every Enemy object on Load. Retain
        // strict actor identity, but reconnect it to legitimate restored
        // survivors instead of stale pre-load instances.
        private void RebindRidgeWatchAfterWorldRestore()
        {
            if(save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(Map,ridgeWatchMap))return;
            var expected=new Dictionary<int,int>(ridgeWatchIds);
            ridgeWatchIds.Clear();ridgeWatchActors.Clear();
            foreach(Enemy body in enemies)
            {
                // A legacy world snapshot can reuse a patrol's numeric ID
                // for an unrelated ordinary enemy. An exact matching archetype
                // AND original maximum HP are required before assigning the
                // quest's combat identity. Otherwise the real patrol may
                // safely respawn nearby instead of paying a forged kill.
                if(body==null||body.hp<=0||body.boss)continue;
                int index=-1;
                int old;
                if(expected.TryGetValue(body.id,out old)&&
                    body.kind==(save.room+old)%3&&
                    Math.Abs(body.maxHp-(30+save.room*7+old*9))<.01f)
                    index=old;
                else
                {
                    // A temporarily closed path can move the fallback patrol
                    // and allocate it a different ID. Rebind only a matching
                    // archetype close to its authentic clearing.
                    for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
                        if(!ridgeWatchIds.ContainsValue(i)&&
                           body.kind==(save.room+i)%3&&
                           Math.Abs(body.maxHp-(30+save.room*7+i*9))<.01f&&
                           (body.Position-RidgeWildlifeSpawns[i]).Length<=4.25f)
                        {index=i;break;}
                }
                if(index<0||index>=RidgeWildlifeSpawns.Length||
                    ridgeWatchIds.ContainsValue(index))continue;
                ridgeWatchIds[body.id]=index;
                ridgeWatchActors[body.id]=body;
            }
            ridgeWatchRespawnCheck=1.5f;
        }
        private string RidgeWatchHint(int room,bool english)
        {
            if(room<1||room>7||!RidgeLinked(room)||!Has("boss_"+room))
                return "";
            if(RidgeWatchComplete(room))
                return english?"\n\nRidge watch cleared: +2 energy/sec on high trails. Roadwarden field aid costs one Moonherb.\n\n"+ridgeWatchStoryEn[room-1]:
                    "\n\n高地巡守已完成：外环高路耐力恢复 +2/秒；可用一株月光草换取草药补给。\n\n"+ridgeWatchStoryZh[room-1];
            int waiting=RidgeWatchPatrolsAwaitingFooting(room);
            string alert=room==save.room&&RidgeWatchCallPending?
                (english?" One patrol is calling for help; interrupt it before the signal.":
                    " 一名巡敌正在呼援，击倒或打断它能阻止增援。"):
                room==save.room&&RidgeWatchRespondingCount>1?
                (english?" The living patrol is responding to a real attack.":
                    " 巡敌听见交战，正在增援。"):"";
            return english?"\n\nRidge watch: clear all three real patrols ("+
                RidgeWatchProgress(room)+"/3)."+
                (waiting>0?" A blocked clearing is delaying "+waiting+
                    " remaining patrol; it will rejoin when the ground opens.":"")+
                alert+" First-clear supplies await.":
                "\n\n高地巡守：击败全部三名真实巡敌（"+
                RidgeWatchProgress(room)+"/3）。"+
                (waiting>0?" 有"+waiting+"名巡敌所在道路暂时封闭，通路恢复后会重新出现。":"")+
                alert+"首次完成有补给奖励。";
        }
        private void OnRidgeWatchEnemyDefeated(Enemy enemy)
        {
            if(enemy==null||save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(Map,ridgeWatchMap)||
                !RidgeLinked(save.room)||!Has("boss_"+save.room))return;
            int index;Enemy actual;
            if(!ridgeWatchIds.TryGetValue(enemy.id,out index)||
                !ridgeWatchActors.TryGetValue(enemy.id,out actual)||
                !Object.ReferenceEquals(actual,enemy))return;
            ridgeWatchResponders.Remove(enemy);
            if(System.Object.ReferenceEquals(ridgeWatchCaller,enemy))
            {ridgeWatchCaller=null;ridgeWatchCallSeconds=0f;}
            string flag="ridge_watch_"+save.room+"_"+index;
            bool first=!Has(flag);
            if(first)Flag(flag);
            if(RidgeWatchComplete(save.room))return;
            // Never pay the three-guard reward for just the subset that
            // happened to spawn on temporarily blocked terrain.
            if(RidgeWatchProgress(save.room)<3)
            {
                if(first)Notify("高地巡守进度 "+RidgeWatchProgress(save.room)+"/3。继续清理附近敌人。",
                    "Ridge watch "+RidgeWatchProgress(save.room)+"/3. Clear the remaining patrols.");
                return;
            }
            Flag("ridge_watch_complete_"+save.room);
            CreditCoins(28);CreditShards(1);AddXp(40);
            RecordMoment(ridgeWatchStoryZh[save.room-1],
                ridgeWatchStoryEn[save.room-1]);
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
        // Actual linked-ridge patrols pace a short, foot-clear corridor
        // instead of being stationary targets. This is not a teleport or
        // an alternate combat loop: regular enemy AI takes priority whenever
        // a player approaches, an attack begins, or the actor is interrupted.
        // Use the accepted moveTime / moving / facing presentation path so
        // SpriteRenderer and the existing fallback show real walking art.
        private void TickRidgeWatchPatrols(float dt)
        {
            if(mode!="world"||ridgeWatchRoom!=save.room||
                !Object.ReferenceEquals(ridgeWatchMap,Map)||dt<=0f)return;
            // A dynamic gate or another world actor can temporarily occupy
            // a patrol's authored footing. Retry missing bodies on the live
            // map instead of making the three-kill quest impossible forever.
            ridgeWatchAlarmSeconds=Math.Max(0f,ridgeWatchAlarmSeconds-dt);
            if(ridgeWatchAlarmSeconds<=0f)ridgeWatchResponders.Clear();
            if(ridgeWatchCaller!=null)
            {
                Enemy caller=ridgeWatchCaller;
                if(!IsActiveRidgeWatchPatrol(caller)||caller.stun>.0001f||
                    !CanStand(caller.Position))
                {
                    ridgeWatchCaller=null;ridgeWatchCallSeconds=0f;
                }
                else
                {
                    ridgeWatchCallSeconds-=dt;
                    if(ridgeWatchCallSeconds<=0f)
                    {
                        ridgeWatchCaller=null;ridgeWatchCallSeconds=0f;
                        ReleaseRidgeWatchAlarm(caller);
                    }
                }
            }
            ridgeWatchRespawnCheck-=dt;
            if(ridgeWatchRespawnCheck<=0)
            {
                ridgeWatchRespawnCheck=1.5f;
                if(!RidgeWatchComplete(save.room))
                    for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
                        if(!Has("ridge_watch_"+save.room+"_"+i))
                            TrySpawnRidgeWatchPatrol(i);
            }
            foreach(Enemy enemy in enemies)
            {
                int index;
                Enemy actual;
                if(!ridgeWatchIds.TryGetValue(enemy.id,out index)||
                    !ridgeWatchActors.TryGetValue(enemy.id,out actual)||
                    !Object.ReferenceEquals(actual,enemy)||
                    enemy.hp<=0f||enemy.boss||enemy.state!=0||
                    enemy.stun>.0001f||enemy.hitTime>.0001f||
                    enemy.releaseTime>.0001f||
                    RidgeWatchRespondsToAlarm(enemy)||
                    (Position-enemy.Position).Length<9f)continue;
                Point anchor=RidgeWildlifeSpawns[index];
                // Returning combat enemies cannot be silently dragged across
                // walls or snapped back to the authored anchor.
                if((enemy.Position-anchor).Length>2.5f)continue;
                float phase=save.totalTime*.45f+index*.8f;
                float offset=(float)Math.Sin(phase)*1.6f;
                Point desired=index==1?
                    new Point(anchor.x,anchor.y+offset):
                    new Point(anchor.x+offset,anchor.y);
                Point delta=desired-enemy.Position;
                if(delta.Length<.02f)continue;
                Point before=enemy.Position;
                Point stepped=SweepActor(before,delta.Normalized*
                    Math.Min(delta.Length,1.05f*dt));
                Point traveled=stepped-before;
                if(traveled.Length<.001f)continue;
                enemy.x=stepped.x;enemy.y=stepped.y;
                enemy.moving=true;
                enemy.moveTime+=traveled.Length;
                enemy.Face(traveled);
            }
        }
        // Use genuine local walking lanes, never the broad CreatePlacedEnemy
        // recovery fallback that can relocate a patrol behind a mountain.
        // The two-cell walkable continuation excludes an isolated 1x1 pocket
        // even when its center is technically a legal actor foot.
        private bool RidgeWatchFootHasExit(Point foot)
        {
            Point[] directions={new Point(1,0),new Point(-1,0),
                new Point(0,1),new Point(0,-1)};
            foreach(Point d in directions)
                if(CanStand(foot+d)&&CanStand(foot+d*2f)&&
                    ActorHasLocalRoute(foot,foot+d*2f,3))return true;
            return false;
        }
        private bool TrySpawnRidgeWatchPatrol(int index)
        {
            if(index<0||index>=RidgeWildlifeSpawns.Length||
                save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(Map,ridgeWatchMap)||
                ridgeWatchIds.ContainsValue(index))return false;
            Point anchor=RidgeWildlifeSpawns[index];
            // Patrol sites are deliberately away from historical objectives.
            foreach(WorldObject site in Map.objects)
                if((new Point(site.x,site.y)-anchor).Length<8f)return false;
            for(int radius=0;radius<=3;radius++)
                for(int dy=-radius;dy<=radius;dy++)
                    for(int dx=-radius;dx<=radius;dx++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)
                            continue;
                        Point foot=anchor+new Point(dx,dy);
                        if(!CanStand(foot)||!RidgeWatchFootHasExit(foot)||
                            (foot-Position).Length<1.6f||
                            (save.companion>=0&&
                             (foot-companionPosition).Length<1.6f))
                            continue;
                        bool occupied=false;
                        foreach(Enemy live in enemies)
                            if(live.hp>0&&(foot-live.Position).Length<1.6f)
                            {occupied=true;break;}
                        if(occupied)continue;
                        Enemy scout=new Enemy(++enemyId,(save.room+index)%3,
                            foot.x,foot.y,30+save.room*7+index*9);
                        enemies.Add(scout);
                        ridgeWatchIds[scout.id]=index;
                        ridgeWatchActors[scout.id]=scout;
                        revision++;
                        return true;
                    }
            return false;
        }
        private void SpawnRidgeWildlife()
        {
            ResetRidgeWatch();
            // Never spawn in the safe starting town, inn or procedural abyss.
            if(save.room<1||save.room>7||!RidgeLinked(save.room)||
                !Has("boss_"+save.room))return;
            // Late-game patrols require BOTH real surveyed ridge bearings and
            // the original guardian's defeat; no old campaign roster changes.
            ridgeWatchRoom=save.room;ridgeWatchMap=Map;
            for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
                TrySpawnRidgeWatchPatrol(i);
            ridgeWatchRespawnCheck=1.5f;
        }
    }
}
