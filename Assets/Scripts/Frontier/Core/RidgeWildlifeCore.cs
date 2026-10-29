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
        // The alarm reports the physical source of an actual injury:
        // hero for player-owned attacks, companion for ally-owned contacts.
        // Hearing a whistle never reveals the hero through sealed terrain.
        private Point ridgeWatchReportedContactFoot;
        // Each recruited original enemy searches the PHYSICAL reported
        // contact vicinity, only for the life of the genuine 6s alarm.
        // Nothing is serialized and no numeric patrol ID owns the memory.
        private readonly Dictionary<Enemy,int> ridgeWatchSweepSteps=
            new Dictionary<Enemy,int>();
        private readonly Dictionary<Enemy,Point> ridgeWatchSweepGoals=
            new Dictionary<Enemy,Point>();
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
        // Report pursuit and world presentation must make the same real
        // visual-sighting decision. A newly SEEN hero outranks an older
        // blocked contact, but quiet rear travel does not reveal itself.
        private bool RidgeWatchSeesLiveHero(Enemy enemy,Point heroFoot)
        {
            if(enemy==null||!CanStand(enemy.Position)||
                !CanStand(heroFoot))return false;
            float heroRange=(enemy.Position-heroFoot).Length;
            if(!Finite(heroRange)||heroRange>8f||
                !ClearAttackPath(enemy.Position,heroFoot))return false;
            return !QuietStepping||
                ((heroFoot-Position).Length<.01f&&
                 heroRange<=4f&&ImpactSearchGuardSeesHero(enemy));
        }
        // Only an exact surviving, actually recruited actor can show this
        // tactical cue. Live visible contact overrides a sealed old source,
        // and reopening a gate resumes the remembered investigation.
        public bool IsRidgeWatchReportRouteSealed(Enemy enemy)
        {
            return enemy!=null&&RidgeWatchRespondsToAlarm(enemy)&&
                !RidgeWatchSeesLiveHero(enemy,Position)&&
                !SoundSweepFootReachable(enemy.Position,
                    ridgeWatchReportedContactFoot);
        }
        public bool IsRidgeWatchSweepingReport(Enemy enemy)
        {
            int step;
            return RidgeWatchRespondsToAlarm(enemy)&&
                !RidgeWatchSeesLiveHero(enemy,Position)&&
                !IsRidgeWatchReportRouteSealed(enemy)&&
                ridgeWatchSweepSteps.TryGetValue(enemy,out step)&&
                step>=0&&step<4;
        }
        // The issued whistle gives a last PHYSICALLY reported source,
        // not endless remote target updates. Arriving at that original
        // foot leads to a four-point, bounded real-ground investigation.
        private Point RidgeWatchLocalReportSweep(Enemy enemy,Point report)
        {
            int step;
            if(!ridgeWatchSweepSteps.TryGetValue(enemy,out step))
            {
                if((enemy.Position-report).Length>1.12f)return report;
                // First CONFIRM the exact new reported injury foot on the
                // arrival frame. A brand-new hit beside the caller must
                // retain that precise combat information before a later
                // AI frame starts local investigation. Preserve existing
                // Hit attribution and companion-source contracts.
                ridgeWatchSweepSteps[enemy]=-1;
                return report;
            }
            if(step<0)step=0;
            else
            {
                Point previous;
                if(ridgeWatchSweepGoals.TryGetValue(enemy,out previous)&&
                    (enemy.Position-previous).Length<.34f)step++;
            }
            for(;step<4;step++)
            {
                Point offset=step==0?new Point(-1.4f,.9f):
                    step==1?new Point(-1.4f,-.9f):
                    step==2?new Point(1.4f,.9f):
                    new Point(1.4f,-.9f);
                Point next=report+offset;
                if(!SoundSweepFootReachable(enemy.Position,next))continue;
                ridgeWatchSweepSteps[enemy]=step;
                ridgeWatchSweepGoals[enemy]=next;
                return next;
            }
            ridgeWatchSweepSteps[enemy]=4;
            ridgeWatchSweepGoals.Remove(enemy);
            return enemy.Position;
        }
        private Point RidgeWatchPursuitFoot(Enemy enemy,Point heroFoot)
        {
            if(!RidgeWatchRespondsToAlarm(enemy))return heroFoot;
            // Normal close/visible aggro resumes naturally. Otherwise the
            // responder searches the ACTUAL reported strike location,
            // never a live coordinate behind sealed terrain or across town.
            // A squad whistle reports a real last witnessed *contact*
            // foot, not automatic 360-degree vision of a quietly moving
            // hero. Normal exposed sight still works at eight tiles.
            // During quiet movement use the same forward-arc / touch
            // detection as ordinary investigating sentries, and never
            // resolve a synthetic caller coordinate as the live hero.
            if(RidgeWatchSeesLiveHero(enemy,heroFoot))
            {
                ridgeWatchSweepSteps.Remove(enemy);
                ridgeWatchSweepGoals.Remove(enemy);
                return heroFoot;
            }
            // A gate can shut AFTER a real whistle recruited teammates.
            // Keep the original report and pause physical searches until
            // safe footing reopens. Never turn into a hidden-hero tracker.
            if(!SoundSweepFootReachable(enemy.Position,
                ridgeWatchReportedContactFoot))
                return enemy.Position;
            return RidgeWatchLocalReportSweep(enemy,
                ridgeWatchReportedContactFoot);
        }
        private void AlertRidgeWatchOnRealHit(Enemy struck,Point sourceFoot)
        {
            if(!IsActiveRidgeWatchPatrol(struck))return;
            // An invalid, changed or fallen source is not a long-distance
            // clairvoyance beacon. The real injured patrol foot remains
            // physically grounded evidence of where the impact happened.
            if(!Finite(sourceFoot.x)||!Finite(sourceFoot.y)||
                !CanStand(sourceFoot))sourceFoot=struck.Position;
            if(RidgeWatchAlarmActive)
            {
                // A NEW genuine incoming hit supplies fresh information.
                // Hidden movement alone never updates the squad's sighting.
                ridgeWatchReportedContactFoot=sourceFoot;
                // A NEW real injury is new information; restart the local
                // search rather than sweeping obsolete, already-checked feet.
                ridgeWatchSweepSteps.Clear();
                ridgeWatchSweepGoals.Clear();
                ridgeWatchAlarmSeconds=Math.Max(ridgeWatchAlarmSeconds,3f);
                return;
            }
            // Do not allow rapid-hit spam to renew the original .85s call.
            if(ridgeWatchCaller!=null||struck.stun>.0001f)return;
            ridgeWatchCaller=struck;
            ridgeWatchCallSeconds=.85f;
            ridgeWatchReportedContactFoot=sourceFoot;
            ridgeWatchSweepSteps.Clear();
            ridgeWatchSweepGoals.Clear();
            effects.Add(new Effect("number",struck.Position,.85f,
                L("巡敌正在呼援！","PATROL CALLING!")));
            revision++;
        }
        // Two ridge sentries can appear three tiles apart across a huge
        // stone maze. A whistle is heard through a real short acoustic
        // corridor, not simply any eventually connected walking island.
        // This checks the exact live actor instances, not reused IDs.
        private bool RidgeWatchWhistleReaches(Enemy caller,Enemy listener)
        {
            if(caller==null||listener==null||
                Object.ReferenceEquals(caller,listener)||
                !IsActiveRidgeWatchPatrol(caller)||
                !IsActiveRidgeWatchPatrol(listener)||
                !CanStand(caller.Position)||!CanStand(listener.Position))
                return false;
            return AudibleAlongWorld(caller.Position,listener.Position,17f);
        }
        private void ReleaseRidgeWatchAlarm(Enemy struck)
        {
            if(!IsActiveRidgeWatchPatrol(struck))return;
            ridgeWatchAlarmSeconds=6f;
            ridgeWatchResponders.Clear();
            ridgeWatchSweepSteps.Clear();
            ridgeWatchSweepGoals.Clear();
            foreach(Enemy ally in enemies)
            {
                if(!IsActiveRidgeWatchPatrol(ally))continue;
                if(System.Object.ReferenceEquals(ally,struck))
                {ridgeWatchResponders.Add(ally);continue;}
                // Reuse the real corridor-distance hearing rule: a long
                // winding maze cannot carry the short squad whistle even
                // when the two sprites appear close across a thin wall.
                if(RidgeWatchWhistleReaches(struck,ally))
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
            ridgeWatchSweepSteps.Clear();ridgeWatchSweepGoals.Clear();
            ridgeWatchCaller=null;ridgeWatchCallSeconds=0f;
            ridgeWatchReportedContactFoot=new Point();
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
            for(int i=0;i<RidgeWatchSiteCount;i++)
                if(!Has("ridge_watch_"+room+"_"+i)&&
                    !ridgeWatchIds.ContainsValue(i))count++;
            return count;
        }
        private Point? RidgeWatchNextMissingFoot(int room)
        {
            if(room!=ridgeWatchRoom||save.room!=room||
                !Object.ReferenceEquals(ridgeWatchMap,Map))return null;
            for(int i=0;i<RidgeWatchSiteCount;i++)
                if(!Has("ridge_watch_"+room+"_"+i)&&
                    !ridgeWatchIds.ContainsValue(i))
                    return RidgeWatchSite(room,i);
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
                    for(int i=0;i<RidgeWatchSiteCount;i++)
                        if(!ridgeWatchIds.ContainsValue(i)&&
                           body.kind==(save.room+i)%3&&
                           Math.Abs(body.maxHp-(30+save.room*7+i*9))<.01f&&
                           RidgeWatchSavedFootMatches(save.room,i,body.Position))
                        {index=i;break;}
                }
                if(index<0||index>=RidgeWatchSiteCount||
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
            ridgeWatchSweepSteps.Remove(enemy);
            ridgeWatchSweepGoals.Remove(enemy);
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
        // Seven genuinely different physical patrol routes. The old three
        // shared southeast coordinates made all outdoor regions repeat the
        // same formation, leaving their northern and eastern roads empty.
        // Each biome has an audible two-guard squad and a distant lone
        // lookout. Real corridor hearing still governs reinforcements;
        // every authored foot stays clear of existing story interactions.
        private const int RidgeWatchSiteCount=3;
        private static readonly Point[,] RegionalRidgeWatchSites={
            {new Point(103.5f,67.5f),new Point(93.5f,66.5f),new Point(76.5f,16.5f)}, // Thornwood: ravine duo, distant canopy
            {new Point(103.5f,64.5f),new Point(90.5f,66.5f),new Point(76.5f,16.5f)}, // Ruins: downstream duo, upper floodwall
            {new Point(112.5f,31.5f),new Point(114.5f,21.5f),new Point(73.5f,58.5f)}, // Ironreach: eastern-rail duo, south quarry
            {new Point(112.5f,34.5f),new Point(112.5f,20.5f),new Point(67.5f,7.5f)}, // Citadel: east-sentry duo, north battlement
            {new Point(103.5f,67.5f),new Point(93.5f,67.5f),new Point(88.5f,13.5f)}, // Mistport: marsh duo, high lighthouse
            {new Point(103.5f,67.5f),new Point(93.5f,66.5f),new Point(79.5f,16.5f)}, // Glasswind: basin duo, north dune
            {new Point(103.5f,64.5f),new Point(92.5f,66.5f),new Point(76.5f,16.5f)} // Frostbell: east-pass duo, upper shelf
        };
        private static readonly Point[] LegacyRidgeWatchSites={
            new Point(101.5f,61.5f),
            new Point(89.5f,67.5f),
            new Point(76.5f,66.5f)
        };
        private static Point RidgeWatchSite(int room,int index)
        {
            return room>=1&&room<=7&&index>=0&&index<RidgeWatchSiteCount?
                RegionalRidgeWatchSites[room-1,index]:
                LegacyRidgeWatchSites[Math.Max(0,Math.Min(2,index))];
        }
        // Expose immutable authored districts to the actual in-game atlas.
        // The map marks physical patrol territories (not an invented enemy
        // or a free ability to locate hidden moving combat actors).
        public Point RidgeWatchDistrict(int room,int index)
        {
            return RidgeWatchSite(room,index);
        }
        // Saved v2 world snapshots can still hold real living patrols at
        // the historical shared coordinates. Recognize those bodies by
        // existing archetype, HP, and physical foot before assigning identity.
        // Do NOT teleport an ongoing patrol or silently award a kill.
        private static bool RidgeWatchSavedFootMatches(int room,int index,Point foot)
        {
            return (foot-RidgeWatchSite(room,index)).Length<=4.25f||
                (foot-LegacyRidgeWatchSites[index]).Length<=4.25f;
        }
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
            if(ridgeWatchAlarmSeconds<=0f)
            {
                ridgeWatchResponders.Clear();
                ridgeWatchSweepSteps.Clear();
                ridgeWatchSweepGoals.Clear();
            }
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
                    for(int i=0;i<RidgeWatchSiteCount;i++)
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
                Point anchor=RidgeWatchSite(save.room,index);
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
            if(index<0||index>=RidgeWatchSiteCount||
                save.room!=ridgeWatchRoom||
                !Object.ReferenceEquals(Map,ridgeWatchMap)||
                ridgeWatchIds.ContainsValue(index))return false;
            Point anchor=RidgeWatchSite(save.room,index);
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
            for(int i=0;i<RidgeWatchSiteCount;i++)
                TrySpawnRidgeWatchPatrol(i);
            ridgeWatchRespawnCheck=1.5f;
        }

        // Seven original three-site wilderness missions. These are not
        // journal-only markers: all 21 field terminals exist in Campaign.json
        // at verified walkable feet, far from every preexisting story site.
        // Survey flags are durable v2-compatible progress. The timed relay
        // is transient and can NEVER resume or pay a reward after loading.
        private static readonly string[] echoRegionsZh={
            "密林回声","遗迹水尺","矿区急报","城墙传令","雾港航灯","沙海井信","霜钟归路"
        };
        private static readonly string[] echoRegionsEn={
            "Canopy Echo","Flood Gauge","Mine Emergency Wire","Citadel Messenger",
            "Harbor Lantern","Oasis Well Signal","Frostbell Return"
        };
        private static readonly string[] echoEvidenceZh={
            "树上的小铜铃被风吹向西边。旧路书写着：找回家的人，要先听低处的铃，不要追高处的军号。",
            "潮水留下的盐线比官方渡船名册更早。旧纸条说：信退潮后的水尺，不信写死的航时。",
            "矿车经过时，钢轨震了三次。工人刻在铁板上：先把伤者人数传出去，再报告货物。",
            "旧墙角有居民偷偷画下的逃生台阶。岗哨批文说先关城门；家人留下的记号却指向先开避难门。",
            "雾里两盏灯相互错开。渔民的旧绳结写着：先朝岸上的固定灯回应，不追移动船火。",
            "沙丘上的假井会随热浪摇晃。木牌刻着：先核对真实井沿的刻度，再传递补水路线。",
            "雪后的风铃高低相接。暖棚记录写明：先送低处的求救声，别让瞭望哨的高音盖住它。"
        };
        private static readonly string[] echoEvidenceEn={
            "Wind has bent the copper chime west. The trail book says to trust the low civilian bell, not the elevated military horn.",
            "The salt line predates the official ferry timetable. The note says to trust the ebb gauge, not a fixed departure hour.",
            "The rails rang three times. A worker etched: send the wounded headcount first, freight manifests second.",
            "Families scratched an emergency stair into the old wall. Their marks say open the refuge gate before the military seal.",
            "Two lights cross in the fog. The sailors' old knots say reply to the fixed shore lamp, not a moving boat flame.",
            "A mirage well shifts in the heat. The sign says measure the real stone rim before marking a water route.",
            "Two frost chimes answer one another. The shelter ledger says carry the lower distress tone before the watchtower note."
        };
        private static readonly string[] echoTrueZh={
            "保留居民的低铃音","照退潮水尺校准","先记录十七名伤者","先开居民避难门",
            "回应岸上固定航灯","核对真井石沿刻度","传递背风处低钟声"
        };
        private static readonly string[] echoFalseZh={
            "按旧军号转发","按官定时刻转发","只报告货车重量","先封死居民门",
            "追随雾中移动船火","标记摇晃的海市蜃楼","先传瞭望哨高钟声"
        };
        private static readonly string[] echoTrueEn={
            "Record the low civilian bell","Trust the ebb gauge","Send the wounded headcount first",
            "Open the civilian refuge gate","Answer the fixed shore lantern",
            "Check the real well-rim markings","Carry the sheltered low chime"
        };
        private static readonly string[] echoFalseEn={
            "Repeat the military horn","Use the fixed official ferry hour",
            "Report cargo weight alone","Seal the civilians' gate",
            "Follow the moving boat light","Mark the shifting mirage well",
            "Send the watchtower's high chime"
        };
        private static readonly string[] echoStopNamesZh={"原始信号","传令起点","远端民用补给台"};
        private static readonly string[] echoStopNamesEn={"Original signal","Relay dispatch","Distant civilian supply"};
        private int echoRunRoom=-1;
        private float echoRunClock;
        private MapData echoRunMap;
        private SaveData echoRunSave;
        private WorldObject echoRunOrigin;

        private static string EchoFlag(int room,string suffix)
        {return "ridge_echo_"+room+"_"+suffix;}
        public bool RidgeEchoComplete(int room)
        {return room>=1&&room<=7&&Has(EchoFlag(room,"done"));}
        public bool RidgeEchoSurveyed(int room)
        {return room>=1&&room<=7&&Has(EchoFlag(room,"survey"));}
        public int RidgeEchoCompleteCount
        {get{int total=0;for(int r=1;r<=7;r++)if(RidgeEchoComplete(r))total++;return total;}}
        public bool RidgeEchoRunActive
        {
            get{return echoRunRoom>=1&&echoRunRoom<=7&&
                echoRunRoom==save.room&&echoRunClock>0f&&
                Object.ReferenceEquals(echoRunMap,Map)&&
                Object.ReferenceEquals(echoRunSave,save)&&
                echoRunOrigin!=null&&SourceBelongsToActiveMap(echoRunOrigin);}
        }
        public int RidgeEchoSeconds {get{return RidgeEchoRunActive?(int)Math.Ceiling(echoRunClock):0;}}
        public Point RidgeEchoObjective(int room)
        {
            if(room<1||room>7||data==null||data.maps==null||
                room>=data.maps.Length)return new Point();
            int stage=RidgeEchoComplete(room)?2:
                !RidgeEchoSurveyed(room)?0:
                RidgeEchoRunActive&&room==save.room?2:1;
            string id="ridge_echo_"+room+"_"+stage;
            foreach(WorldObject o in data.maps[room].objects)
                if(o!=null&&o.id==id&&o.kind=="ridgeEcho")
                    return new Point(o.x,o.y);
            return new Point();
        }
        private static int RidgeEchoSiteIndex(WorldObject site,int room)
        {
            if(site==null||site.kind!="ridgeEcho"||room<1||room>7||
                site.id==null)return -1;
            for(int i=0;i<3;i++)
                if(site.id=="ridge_echo_"+room+"_"+i)return i;
            return -1;
        }
        private static int RidgeEchoTimeLimit(int room)
        {return room==6||room==7?92:room==3?100:112;}
        private static string RidgeEchoMaterialZh(int room)
        {
            return room==1?"2份木材、1株月光草":
                room==2?"1份水晶、1株月光草":
                room==3?"2份机械零件":
                room==4?"1木材、1水晶、1机械零件":
                room==5?"1木材、1条鱼":
                room==6?"2水晶、1株月光草":
                "1木材、2株月光草";
        }
        private static string RidgeEchoMaterialEn(int room)
        {
            return room==1?"2 timber and 1 Moonherb":
                room==2?"1 crystal and 1 Moonherb":
                room==3?"2 gears":
                room==4?"1 timber, 1 crystal and 1 gear":
                room==5?"1 timber and 1 fish":
                room==6?"2 crystals and 1 Moonherb":
                "1 timber and 2 Moonherbs";
        }
        private bool SpendRidgeEchoMaterials(int room)
        {
            switch(room)
            {
                case 1:return Spend(2,0,1,0,0);
                case 2:return Spend(0,1,1,0,0);
                case 3:return Spend(0,0,0,0,2);
                case 4:return Spend(1,1,0,0,1);
                case 5:return Spend(1,0,0,1,0);
                case 6:return Spend(0,2,1,0,0);
                case 7:return Spend(1,0,2,0,0);
                default:return false;
            }
        }
        private void ResetRidgeEcho()
        {
            echoRunRoom=-1;echoRunClock=0f;echoRunMap=null;
            echoRunSave=null;echoRunOrigin=null;
        }
        private void TickRidgeEcho(float dt)
        {
            if(echoRunRoom>=0)
            {
                if(!RidgeEchoRunActive||Dying||practicing)
                    ResetRidgeEcho();
                else
                {
                    echoRunClock=Math.Max(0f,echoRunClock-dt);
                    if(echoRunClock<=0f)
                    {
                        ResetRidgeEcho();
                        Notify("传令错过了约定的窗口。回到本地区中继起点重试；原始线索仍然保留。",
                            "The relay window closed. Return to the original dispatch site; the survey evidence remains safe.");
                    }
                }
            }
            // The repaired FAR end is an actual public refuge, not a
            // map-only reward. Ordinary combat and protected rescue tasks
            // take priority. Do not heal anywhere else in the region.
            if(dt<=0f||save.room<1||save.room>7||
                !RidgeEchoComplete(save.room)||Dying||practicing||
                FieldWorkPreventsRest||!SafeTravel)return;
            Point foot=RidgeEchoObjective(save.room);
            if((Position-foot).Length>4.0f)return;
            save.health=Clamp(save.health+dt*1.8f,0f,save.maxHealth);
            save.energy=Clamp(save.energy+dt*3f,0f,100f);
            if(save.room==6)save.heat=Math.Max(0f,save.heat-dt*2f);
            if(save.room==7)save.cold=Math.Max(0f,save.cold-dt*2f);
        }
        private bool TalkRidgeEcho(WorldObject site)
        {
            if(site==null||site.kind!="ridgeEcho")return false;
            int room=save.room,index=RidgeEchoSiteIndex(site,room);
            if(index<0||!SourceBelongsToActiveMap(site)||
                !HasPhysicalInteractionReach(site)||!CanStand(Position))
                return true;
            string title=site.name,english=site.en;
            if(!Has("biome_field_done_"+room))
            {
                Dialog(title,english,
                    "野外旧台上留着一条没有发完的居民讯号。先亲自核实本地区两处探路地标，再来接手这条路线。",
                    "An unfinished civilian transmission rests here. Survey both original biome landmarks in person before rebuilding the field line.",
                    C("记下位置","Remember this place","close"));
                return true;
            }
            if(RidgeEchoComplete(room))
            {
                Dialog(title,english,
                    "“"+echoRegionsZh[room-1]+"”已经传到三处旧台。远端补给处可以安全恢复生命和耐力；它不是新的传送入口。",
                    "The "+echoRegionsEn[room-1]+" line now reaches all three field sites. The remote refuge restores real health and stamina outside combat; it is not a teleport.",
                    C("继续巡路","Keep exploring","close"));
                return true;
            }
            if(index==0)
            {
                Dialog(title,english,
                    echoEvidenceZh[room-1]+"\n\n请选择保留哪一份真正的居民证据。",
                    echoEvidenceEn[room-1]+"\n\nWhich original civilian reading belongs in the relay?",
                    (room%2==0)?
                        C(echoFalseZh[room-1],echoFalseEn[room-1],"ridge_echo_false"):
                        C(echoTrueZh[room-1],echoTrueEn[room-1],"ridge_echo_true"),
                    (room%2==0)?
                        C(echoTrueZh[room-1],echoTrueEn[room-1],"ridge_echo_true"):
                        C(echoFalseZh[room-1],echoFalseEn[room-1],"ridge_echo_false"),
                    C("以后再核对","Leave this record","close"));
                return true;
            }
            if(index==1)
            {
                Dialog(title,english,
                    !RidgeEchoSurveyed(room)?
                        "这里缺了原始读数。先走到真正的第一处旧台，确认居民的原始证据。":
                        "信号已核对。现在以8点耐力发送实地传令，"+RidgeEchoTimeLimit(room)+
                        "秒内亲自走到第三处远端补给台。重新发信会重新计时，但不会凭空完成路程。",
                    !RidgeEchoSurveyed(room)?
                        "The original reading is missing. Go to the first actual field terminal to recover it.":
                        "The evidence is verified. Spend 8 stamina to start a "+
                        RidgeEchoTimeLimit(room)+"-second physical delivery to the far civilian refuge. Restarting a run only resets its clock.",
                    RidgeEchoSurveyed(room)?
                        C("开始真实限时传令","Start timed overland relay","ridge_echo_launch"):
                        C("返回寻找原件","Return for the evidence","close"),
                    RidgeEchoRunActive?
                        C("中止本次传令","Withdraw this timed delivery","ridge_echo_abort"):
                        C("暂不行动","Not now","close"),
                    C("暂时离开","Leave","close"));
                return true;
            }
            Dialog(title,english,
                RidgeEchoRunActive?
                    "你带着刚从中继起点发出的真实讯号，尚余"+RidgeEchoSeconds+
                    "秒。交付"+RidgeEchoMaterialZh(room)+"，才能让这处偏远营地真的开始供应居民。":
                    "最后的补给台没有收到有效传令。请先在第二处现场启动限时任务，再沿真实道路把讯号送来。",
                RidgeEchoRunActive?
                    "Your real relay has "+RidgeEchoSeconds+
                    " seconds remaining. Deliver "+RidgeEchoMaterialEn(room)+
                    " to staff this remote public refuge permanently.":
                    "No verified message has reached this refuge. Start the timed run at the second physical station, then carry it here over real terrain.",
                RidgeEchoRunActive?
                    C("交付材料并完成民用路线","Deliver supplies and reopen the route","ridge_echo_finish"):
                    C("查看未到达的讯号","Check missing signal","close"),
                C("稍后再说","Leave","close"));
            return true;
        }
        private bool ChooseRidgeEcho(string action)
        {
            if(action==null||!action.StartsWith("ridge_echo_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            int room=save.room,index=RidgeEchoSiteIndex(source,room);
            if(index<0||mode!="world"||pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(source)||!HasPhysicalInteractionReach(source)||
                !CanStand(Position)||Dying||practicing||
                !Has("biome_field_done_"+room))return true;
            if(action=="ridge_echo_true"&&index==0&&!RidgeEchoSurveyed(room))
            {
                Flag(EchoFlag(room,"survey"));revision++;sound="quest";
                RecordMoment("你在"+echoRegionsZh[room-1]+"保留了第一处野外旧台的原始居民讯号，向远处的传令起点出发。",
                    "You preserved the original civilian reading for "+echoRegionsEn[room-1]+" and set out for the distant field relay.");
                Notify("原件已核实。前往第二处中继起点，开启有时间限制的实地传令。",
                    "Original reading verified. Reach the second terminal to start the timed physical relay.");
                return true;
            }
            if(action=="ridge_echo_false"&&index==0&&!RidgeEchoSurveyed(room))
            {
                save.energy=Math.Max(0f,save.energy-6f);revision++;sound="deny";
                Notify("这份读数与现场痕迹不符。查验误读耗费6耐力；线索尚未结案。",
                    "The false reading contradicts the site. -6 stamina; the evidence remains unresolved.");
                return true;
            }
            if(action=="ridge_echo_abort"&&index==1&&RidgeEchoRunActive)
            {
                ResetRidgeEcho();revision++;sound="deny";
                Notify("传令已在原起点明确撤回。原始线索和旧存档的进度保留，材料尚未消耗。",
                    "This delivery was explicitly withdrawn at its true source. Original evidence is safe; no material was spent.");
                return true;
            }
            if(action=="ridge_echo_launch"&&index==1&&RidgeEchoSurveyed(room))
            {
                if((FieldWorkPreventsRest&&!RidgeEchoRunActive)||!SafeTravel)
                {
                    Notify("周围尚有战斗或受保护现场工作。先完成它们再发信。",
                        "Combat or a protected field mission is active. Finish it before dispatching the relay.");
                    return true;
                }
                if(save.energy<8f)
                {Notify("发出传令需要8点耐力。","Sending this relay costs 8 energy.");return true;}
                save.energy-=8f;
                echoRunRoom=room;echoRunClock=RidgeEchoTimeLimit(room);
                echoRunMap=Map;echoRunSave=save;echoRunOrigin=source;
                revision++;sound="quest";
                Notify("限时传令开始！请在"+RidgeEchoTimeLimit(room)+"秒内真正抵达第三处补给台。",
                    "The timed relay is live! Reach the third physical field terminal within "+
                    RidgeEchoTimeLimit(room)+" seconds.");
                return true;
            }
            if(action=="ridge_echo_finish"&&index==2&&RidgeEchoRunActive&&
                !RidgeEchoComplete(room))
            {
                // Our OWN live relay is expected. Another field mission
                // cannot start while the courier is active because the
                // shared fieldwork gate above now protects this delivery.
                if(!SafeTravel)
                {
                    Notify("补给台周围仍有危险，必须先清理实际威胁。" ,
                        "Real threats remain at the supply station. Clear them before delivering.");
                    return true;
                }
                if(!SpendRidgeEchoMaterials(room))return true;
                ResetRidgeEcho();Flag(EchoFlag(room,"done"));
                CreditCoins(30+room*3);CreditShards(1);AddXp(55);
                RecordMoment("你在"+echoRegionsZh[room-1]+"的第三处真实补给台完成了限时传令。原本空着的偏远路段如今有了药品、饮水和真正的民用讯号。",
                    "A timed overland message reached the third real field station of "+echoRegionsEn[room-1]+". The previously empty route now has civilian supplies, water and a usable refuge.");
                if(RidgeEchoCompleteCount==7&&!Has("ridge_echo_all"))
                {Flag("ridge_echo_all");CreditSkillPoints(1);}
                revision++;sound="reward";
                Notify("偏远民用补给台正式启用：硬币+"+(30+room*3)+
                    "、碎片+1、经验+55。可在此地安全恢复生命和耐力。",
                    "Remote civilian refuge opened: +"+(30+room*3)+
                    " coins, 1 shard, 55 XP. Rest here outside combat for actual healing and stamina.");
                return true;
            }
            return true;
        }
    }
}
