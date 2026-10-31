using System;
using System.Collections.Generic;
namespace Starfall
{
    // The newly accessible mid-region crossings have genuine optional
    // skirmishes, not another noticeboard or extra mandatory chapter kills.
    // All combat ownership is by actual Enemy identity, never recycled IDs.
    // Only real defeat/restraint signs the existing SaveData v2 flag receipt.
    public sealed partial class GameCore
    {
        private readonly List<Enemy> crossingWatchFoes=new List<Enemy>(2);
        private readonly HashSet<Enemy> crossingWatchDefeated=new HashSet<Enemy>();
        private readonly HashSet<Enemy> crossingWatchCaptured=new HashSet<Enemy>();
        private int crossingWatchRoom=-1;
        private MapData crossingWatchMap;
        private SaveData crossingWatchSave;
        private float crossingWatchWarningSeconds,crossingWatchRetrySeconds;
        private float crossingWatchDisengageSeconds;
        public bool CrossingWatchActive
        {
            get{return crossingWatchFoes.Count==2&&
                crossingWatchRoom==save.room&&
                Object.ReferenceEquals(crossingWatchMap,Map)&&
                Object.ReferenceEquals(crossingWatchSave,save);}
        }
        public Point CrossingWatchGroundFoot
        {get{return CrossingWatchSite(save.room);}}
        public float CrossingWatchWarningSecondsLeft
        {get{return CrossingWatchPending?crossingWatchWarningSeconds:0f;}}
        public bool CrossingWatchPending
        {get{return crossingWatchWarningSeconds>0f&&
                    crossingWatchRoom==save.room&&
                    Object.ReferenceEquals(crossingWatchMap,Map)&&
                    Object.ReferenceEquals(crossingWatchSave,save);}}
        public int CrossingWatchRemaining
        {
            get
            {
                if(!CrossingWatchActive)return 0;
                int n=0;foreach(Enemy e in crossingWatchFoes)
                    if(e!=null&&!crossingWatchDefeated.Contains(e)&&
                       !crossingWatchCaptured.Contains(e))n++;
                return n;
            }
        }
        public bool CrossingWatchSecured(int room)
        {return room==1||room==2||room==3?
            Has("crossing_watch_secured_"+room):false;}
        public bool CrossingWatchHumane(int room)
        {return CrossingWatchSecured(room)&&
            Has("crossing_watch_humane_"+room);}
        public bool IsCrossingWatchEnemy(Enemy e)
        {return e!=null&&CrossingWatchActive&&crossingWatchFoes.Contains(e);}
        private void OnCrossingWatchEnemyDefeated(Enemy e)
        {
            if(IsCrossingWatchEnemy(e))crossingWatchDefeated.Add(e);
        }
        private bool OnCrossingWatchEnemyRestrained(Enemy e)
        {
            if(!IsCrossingWatchEnemy(e)||e.hp<=0f||enemies.Contains(e))
                return false;
            crossingWatchCaptured.Add(e);
            return true;
        }
        private static Point CrossingWatchSite(int room)
        {
            if(room==1)return new Point(49.5f,34.5f);
            if(room==2)return new Point(63.5f,54.5f);
            if(room==3)return new Point(88.5f,62.5f);
            return new Point(-1000f,-1000f);
        }
        private bool CrossingWatchAvailable(int room)
        {
            if(room!=1&&room!=2&&room!=3)return false;
            if(CrossingWatchSecured(room)||!Has("boss_"+room))return false;
            return Has(room==1?"thorn_canopy_cut_0":
                       room==2?"ruins_step_0":"iron_haulage_1");
        }
        private void ResetCrossingWatch(bool retry=false)
        {
            // A real departure, load, failed encounter or death retires
            // only encounter-owned unreleased threats and projectiles.
            projectiles.RemoveAll(p=>p!=null&&p.hostileShooter!=null&&
                crossingWatchFoes.Contains(p.hostileShooter));
            foreach(Enemy foe in crossingWatchFoes)
            {
                RemoveEnemyPendingWarning(foe);
                ForgetOrdinaryPatrolSight(foe);
                enemyHitAlert.Remove(foe);
                regionalAmbientEnemies.Remove(foe);
                enemies.Remove(foe);
            }
            crossingWatchFoes.Clear();crossingWatchDefeated.Clear();
            crossingWatchCaptured.Clear();
            crossingWatchRoom=-1;crossingWatchMap=null;
            crossingWatchSave=null;crossingWatchWarningSeconds=0f;
            crossingWatchDisengageSeconds=0f;
            crossingWatchRetrySeconds=retry?6f:0f;
        }
        private bool BeginCrossingWatchFight()
        {
            int room=save.room,kindA=room==1?0:room==2?1:2;
            int kindB=room==1?1:room==2?2:0;
            Point source=CrossingWatchSite(room);
            var feet=new List<Point>(2);
            // Build the ENTIRE pair on authentic, reachable terrain,
            // before creating any enemy. This avoids invisible wall
            // spawns and preserves NPC interaction pads and visual props.
            for(int slot=0;slot<2;slot++)
            {
                bool found=false;
                Point chosen=new Point();float best=float.NegativeInfinity;
                int kind=slot==0?kindA:kindB;
                int cx=(int)Math.Floor(Position.x);
                int cy=(int)Math.Floor(Position.y);
                for(int dy=-8;dy<=8;dy++)
                for(int dx=-8;dx<=8;dx++)
                {
                    Point foot=new Point(cx+dx+.5f,cy+dy+.5f);
                    float radius=(foot-Position).Length;
                    if(radius<5f||radius>8.4f||
                        slot==0&&!ClearAttackPath(Position,foot)||
                        !RegionalScoutFootClear(foot,source,true,feet))
                        continue;
                    float target=kind==1?6.8f:kind==2?6.1f:5.6f;
                    float score=-(float)Math.Abs(radius-target)*2f;
                    if(kind==1&&ClearAttackPath(foot,Position))
                        score+=PhysicalBowLaneFromFoot(foot,Position)?8f:-5f;
                    if(slot==1)
                    {
                        Point first=(feet[0]-Position).Normalized;
                        Point second=(foot-Position).Normalized;
                        score+=(1f-first.x*second.x-first.y*second.y)*6f;
                    }
                    if(!found||score>best+.0001f)
                    {chosen=foot;best=score;found=true;}
                }
                if(!found)return false;
                feet.Add(chosen);
            }
            for(int i=0;i<2;i++)
            {
                int kind=i==0?kindA:kindB;
                Enemy foe=CreatePlacedEnemy(++enemyId,kind,feet[i],
                    42+room*11);
                crossingWatchFoes.Add(foe);
                enemies.Add(foe);
                regionalAmbientEnemies.Add(foe);
                effects.Add(new Effect("enemyRelease",foe.Position,.65f)
                    {variant=kind});
            }
            crossingWatchWarningSeconds=0f;
            crossingWatchDisengageSeconds=0f;
            revision++;sound="alert";
            Notify(room==1?"密林截击：猎手从树根两侧接近！":
                   room==3?"矿道截击：重甲守卫和游击者正在合围！":
                   "遗迹截击：弓手和甲兵控制了古代石道！",
                   room==1?"Root crossing: hunters approach from opposite flanks!":
                   room==3?"Mine crossing: a plated guard and a skirmisher converge!":
                   "Ruins crossing: an archer and armored guard hold the raised causeway!");
            return true;
        }
        private void TickCrossingWatch(float dt)
        {
            if(mode!="world"||dt<=0f)return;
            crossingWatchRetrySeconds=Math.Max(0f,crossingWatchRetrySeconds-dt);
            if(crossingWatchRoom>=0&&
                (crossingWatchRoom!=save.room||
                !Object.ReferenceEquals(crossingWatchMap,Map)||
                !Object.ReferenceEquals(crossingWatchSave,save)||Dying))
            {ResetCrossingWatch();return;}
            if(CrossingWatchActive)
            {
                foreach(Enemy e in crossingWatchFoes)
                    if(e==null||(!crossingWatchDefeated.Contains(e)&&
                       !crossingWatchCaptured.Contains(e)&&
                       (!enemies.Contains(e)||e.hp<=0f)))
                    {ResetCrossingWatch(true);return;}
                if(CrossingWatchRemaining==0)
                {
                    int room=crossingWatchRoom;
                    bool nonlethal=crossingWatchCaptured.Count==2;
                    ResetCrossingWatch();
                    if(!CrossingWatchSecured(room))
                    {
                        Flag("crossing_watch_secured_"+room);
                        // A genuinely restrained FULL pair knows the old
                        // evacuation paths and can help the local
                        // ridge-watch rebuild. The merciful result changes
                        // the actual existing crossing travel economy,
                        // not only a diary line or decorative reputation.
                        if(nonlethal)Flag("crossing_watch_humane_"+room);
                        CreditCoins(24+room*3);AddXp(28+room*3);
                        revision++;sound="reward";
                        RecordMoment(nonlethal?
                            "两名拦路者活着带回了被扣押的巡路图。他们协助居民开通本区高地路，穿行耐力降低两点。":
                            room==1?
                            "荆棘根桥上的拦路者退去，居民不用再在林道间冒险绕行。":
                            room==2?"沉没遗迹的石道重新交还渡工，归乡的人不必再缴纳路税。":
                            "矿工重新控制了通风矿道，运料车不再被拦截。",
                            nonlethal?
                            "Two living captives returned stolen route charts and helped reopen the local ridge road, reducing passage stamina by two.":
                            room==1?"The root crossing is safe for civilians again.":
                            room==2?"Ferrymen reclaim the raised ruins causeway from extortion.":
                            "Miners reclaim the haul route from armed extortion.");
                        Notify(nonlethal?"双方袭击者被活捉，交回旧地图。本区高地穿行耐力减少 2。":
                            "截击威胁解除，居民通道恢复安全。",
                            nonlethal?"Both raiders returned the stolen route charts. Local ridge passage costs 2 less stamina.":
                            "Crossing threat defeated. Civilian route secured.");
                    }
                    return;
                }
                if((Position-CrossingWatchSite(save.room)).Length>23f)
                    crossingWatchDisengageSeconds+=dt;
                else crossingWatchDisengageSeconds=0f;
                if(crossingWatchDisengageSeconds>=1.5f)
                {
                    ResetCrossingWatch(true);
                    Notify("你撤出了路口，追兵返回原地。下次可重新尝试。",
                        "You left the crossing. Raiders withdrew; return to retry.");
                }
                return;
            }
            if(crossingWatchWarningSeconds>0f)
            {
                if((Position-CrossingWatchSite(save.room)).Length>7f)
                {ResetCrossingWatch(true);return;}
                crossingWatchWarningSeconds-=dt;
                if(crossingWatchWarningSeconds>0f)return;
                if(!BeginCrossingWatchFight())ResetCrossingWatch(true);
                return;
            }
            if(crossingWatchRetrySeconds>0f||
                !CrossingWatchAvailable(save.room)||
                practicing||racing||FieldWorkPreventsRest||
                RemoteRoadEncounterActive||FieldPatrolActive||
                WildernessHuntActive||!SafeTravel||!CanStand(Position)||
                (Position-CrossingWatchSite(save.room)).Length>2.3f)
                return;
            crossingWatchRoom=save.room;crossingWatchMap=Map;
            crossingWatchSave=save;crossingWatchWarningSeconds=1.35f;
            Notify("你听到路旁的铁片碰撞声。附近有人准备截击；现在仍可撤离。",
                "Metal rattles beside the path. Raiders are preparing an ambush; you can still withdraw.");
        }
    }
}
