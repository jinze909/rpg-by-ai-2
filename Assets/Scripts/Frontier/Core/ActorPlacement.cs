using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Use the same complete foot clearance as movement, including active gates and tides.
        // Preserve valid authored/saved positions; only repair positions that actually overlap.
        private Point SafeActorFoot(Point proposed)
        {
            if (!Finite(proposed.x) || !Finite(proposed.y) || proposed.x < 0 || proposed.y < 0 || proposed.x >= Map.rows[0].Length || proposed.y >= Map.rows.Length)
                proposed = new Point(Map.spawnX, Map.spawnY);
            if (Fits(proposed)) return proposed;
            Point result = new Point(); float nearest = float.MaxValue;
            for (int y = 2; y < Map.rows.Length - 2; y++)
            for (int x = 2; x < Map.rows[y].Length - 2; x++)
            {
                Point candidate = new Point(x + .5f, y + .5f), delta = candidate - proposed;
                float squared = delta.x * delta.x + delta.y * delta.y;
                if (squared >= nearest || !Fits(candidate)) continue;
                result = candidate; nearest = squared;
            }
            if (nearest == float.MaxValue) throw new InvalidOperationException("The region has no valid actor footing.");
            return result;
        }
        // Several authored spawn points can collapse to the same safe
        // footing after a tide/bridge change, and interrupted Abyss saves
        // may contain two living enemies on one cell. Keep every valid
        // non-overlapping original foot unchanged. Only separate genuinely
        // overlapping actors, into a nearby tile in the SAME reachable
        // pocket; never move one through a wall to fill a different island.
        // Recovering a wide guardian or placing a recruit beside one must
        // respect BOTH bodies. Keep the original .95 ordinary/ordinary
        // authored spacing; do not shrink or reroute existing safe spawns.
        private static float SpawnEnemyClearance(Enemy existing,bool wideNewBody)
        {
            return existing.boss ? (wideNewBody?1.8f:1.4f) :
                (wideNewBody?1.4f:.95f);
        }
        private Point UncrowdedEnemyFoot(Point proposed,
            Enemy relocatingEnemy=null,bool relocatingCompanion=false,
            bool placingBoss=false)
        {
            Point safe=SafeActorFoot(proposed);
            bool wide=placingBoss||
                (relocatingEnemy!=null&&relocatingEnemy.boss);
            // A newly spawned foe must not materialize INSIDE the hero's
            // or an active companion's hurtbox after a save restoration.
            // This applies only to overlapping feet, not normal nearby
            // enemies; legitimate close-range battles stay unchanged.
            bool occupied=(safe-Position).Length<.95f||
                save.companion>=0&&!relocatingCompanion&&
                (safe-companionPosition).Length<.95f||
                ReliefEscortActive&&reliefMissionRoom==save.room&&
                (safe-reliefFollowerPosition).Length<.95f;
            foreach(Enemy other in enemies)
                if(other!=null&&other!=relocatingEnemy&&other.hp>0&&
                    (other.Position-safe).Length<
                        SpawnEnemyClearance(other,wide))
                {occupied=true;break;}
            if(!occupied)return safe;
            for(int radius=1;radius<=4;radius++)
            for(int dy=-radius;dy<=radius;dy++)
            for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point option=new Point(
                    (float)Math.Floor(safe.x)+dx+.5f,
                    (float)Math.Floor(safe.y)+dy+.5f);
                if(!Fits(option)||!ActorHasLocalRoute(safe,option,4)||
                    (option-Position).Length<1.05f||
                    save.companion>=0&&!relocatingCompanion&&
                    (option-companionPosition).Length<1.05f||
                    ReliefEscortActive&&reliefMissionRoom==save.room&&
                    (option-reliefFollowerPosition).Length<1.05f)
                    continue;
                bool clear=true;
                foreach(Enemy other in enemies)
                    if(other!=null&&other!=relocatingEnemy&&other.hp>0&&
                       (other.Position-option).Length<
                           SpawnEnemyClearance(other,wide))
                    {clear=false;break;}
                if(clear)return option;
            }
            // The first four rings are normally enough for combat groups,
            // but a narrow connected corridor can be occupied for five or
            // more cells during a legacy load or repeated evacuation. Extend
            // ONLY the crowded-case search using the same stamped 4-way
            // collision graph as enemy movement, bounded to twelve physical
            // walking steps. Never jump through a wall or search the entire
            // 120x80 world looking for a distant spawn.
            int w=Map.rows[0].Length,h=Map.rows.Length;
            int sx=(int)Math.Floor(safe.x),sy=(int)Math.Floor(safe.y);
            if(sx>=0&&sx<w&&sy>=0&&sy<h)
            {
                PreparePathSearch(w*h);
                int begin=sy*w+sx,head=0,tail=0;
                pathQueue[tail++]=begin;
                VisitPath(begin,0); // parent buffer stores BFS depth here.
                while(head<tail)
                {
                    int node=pathQueue[head++],x=node%w,y=node/w;
                    int depth=pathParents[node];
                    if(depth>=5)
                    {
                        Point p=new Point(x+.5f,y+.5f);
                        bool free=Fits(p)&&
                            (p-Position).Length>=1.05f&&
                            (save.companion<0||relocatingCompanion||
                                (p-companionPosition).Length>=1.05f)&&
                            (!ReliefEscortActive||reliefMissionRoom!=save.room||
                                (p-reliefFollowerPosition).Length>=1.05f);
                        if(free)
                            foreach(Enemy foe in enemies)
                                if(foe!=null&&foe!=relocatingEnemy&&
                                    foe.hp>0&&(foe.Position-p).Length<
                                        SpawnEnemyClearance(foe,wide))
                                {free=false;break;}
                        if(free)return p;
                    }
                    if(depth>=12)continue;
                    for(int i=0;i<4;i++)
                    {
                        int nx=x+pursuitSteps[i,0],ny=y+pursuitSteps[i,1];
                        if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int next=ny*w+nx;
                        if(PathVisited(next)||
                            !Fits(new Point(nx+.5f,ny+.5f)))continue;
                        VisitPath(next,depth+1);
                        pathQueue[tail++]=next;
                    }
                }
            }
            // An entirely packed/sealed refuge must not silently delete a
            // saved monster or transport it through a closed flood gate.
            return safe;
        }
        // The opening battle is still a compact story encounter, not a
        // mandatory hunt across the full 120x80 region. Each region uses its
        // own authored sightlines, flanks and road shoulders. All recruits
        // remain on the same real, currently walkable component as the hero.
        // Anchors are deliberately not saved: SaveData v1/v2 keeps its exact
        // historical combat serialization and restored enemies take priority.
        private static readonly int[,,] regionalBattleAnchors = {
            { {11,10},{14,17},{21,11},{25,18},{28,7},{30,16},{15,7},{26,20} },
            { {11,12},{15,16},{22,12},{26,10},{28,16},{30,20},{15,20},{24,18} },
            { {11,10},{15,17},{23,12},{28,8},{26,18},{10,20},{16,7},{30,19} },
            { {12,9},{15,17},{23,9},{29,15},{27,19},{12,20},{19,7},{30,6} },
            { {11,11},{16,15},{23,11},{28,10},{19,20},{30,15},{14,6},{28,20} },
            { {12,11},{15,16},{25,8},{29,12},{25,20},{13,20},{22,6},{30,19} },
            { {11,12},{14,16},{24,11},{30,10},{28,20},{11,20},{26,7},{30,16} }
        };
        private bool[] RegionalBattleReachableTiles()
        {
            int width=Map.rows[0].Length,height=Map.rows.Length;
            var reachable=new bool[width*height];
            // Use the identical 4-neighbor, full-foot Fits terrain test
            // used by real movement; no line-of-sight or tile-glyph shortcut.
            if(!Fits(Position))return reachable;
            var queue=new int[width*height];
            int start=(int)Math.Floor(Position.y)*width+
                (int)Math.Floor(Position.x),head=0,tail=0;
            reachable[start]=true;queue[tail++]=start;
            while(head<tail)
            {
                int node=queue[head++],x=node%width,y=node/width;
                for(int d=0;d<4;d++)
                {
                    int xx=x+pursuitSteps[d,0],yy=y+pursuitSteps[d,1];
                    if(xx<0||xx>=width||yy<0||yy>=height)continue;
                    int next=yy*width+xx;
                    if(reachable[next]||
                        !Fits(new Point(xx+.5f,yy+.5f)))continue;
                    reachable[next]=true;queue[tail++]=next;
                }
            }
            return reachable;
        }
        // Major authored decoration uses a larger visual body than a single
        // terrain pixel. A legal tile under a column, pine or wreck still
        // makes a baseline monster appear embedded inside that prop in
        // GameView. Reserve the prop's visual foot when *staging* enemies;
        // leave existing player movement, saved actors and landmarks alone.
        private static float RegionalDecorationEnemyPad(Decoration d)
        {
            if(d==null)return 0f;
            switch(d.kind)
            {
                case "tree":case "snowpine":case "column":
                case "obelisk":case "ruin":case "house":
                case "lighthouse":case "tent":case "skiff":
                    return 1.45f;
                case "crystal":case "palm":case "boat":
                case "barrel":return 1.1f;
                default:return 0f;
            }
        }
        private bool RegionalBattleFootFree(Point foot,bool[] connected,
            bool expanded=false)
        {
            int x=(int)Math.Floor(foot.x),y=(int)Math.Floor(foot.y);
            int width=Map.rows[0].Length;
            if(x<0||y<0||x>=width||y>=Map.rows.Length||
                (!expanded&&(x<4||x>33||y<3||y>21))||
                !connected[y*width+x]||!Fits(foot)||
                (foot-Position).Length<4.5f)return false;
            // Leave conversations, resources, exits and quest machinery
            // readable rather than turning their interaction pads into
            // unavoidable body-blocking piles.
            foreach(WorldObject obj in Map.objects)
                if(obj!=null)
                {
                    float distance=(foot-new Point(obj.x,obj.y)).Length;
                    // The first four chapters now have meaningful live
                    // civilian rescues at three separate stations. Preserve
                    // a real safe conversation pad around these stations,
                    // rather than camping their new arrival trail with the
                    // mandatory regional battle roster. Regular NPCs, the
                    // Boss and other late-world objectives retain their
                    // existing 2-unit collision clearance.
                    float clearance=save.room>=1&&save.room<=4&&
                        obj.kind=="chapterroad"?4.2f:2f;
                    if(distance<clearance)return false;
                }
            // Decoration is visual cover, not a new hard wall: never
            // alter a quest walk or stored enemy foot to fix this.
            if(Map.decor!=null)
                foreach(Decoration visual in Map.decor)
                {
                    float pad=RegionalDecorationEnemyPad(visual);
                    if(pad>0f&&
                        (foot-new Point(visual.x,visual.y)).Length<pad)
                        return false;
                }
            foreach(Enemy other in enemies)
                if(other!=null&&other.hp>0&&
                    (foot-other.Position).Length<2.5f)return false;
            return true;
        }
        // The mandatory opening roster still has the same authored anchor
        // and headcount, but a ranged slot can no longer be assigned a
        // sealed sightline merely because that was the first legal tile.
        // Rank ONLY nearby, hero-reachable, decoration-clear battle feet.
        private float RegionalOpeningPadScore(Point foot,Point anchor,int kind)
        {
            float anchorDrift=(foot-anchor).Length;
            float heroDistance=(foot-Position).Length;
            bool clearLine=ClearAttackPath(foot,Position);
            float desired=kind==1?11f:kind==2?7.8f:6.8f;
            float score=40f-anchorDrift*3f-
                Math.Abs(heroDistance-desired)*(kind==1?1.3f:.8f);
            // The attack's swept arrow radius is wider than the center
            // sight ray. Do not give an opening bowman a premier station
            // whose first honest projectile clips a ruin sidewall.
            if(kind==1)score+=!clearLine?-14f:
                PhysicalBowLaneFromFoot(foot,Position)?22f:-12f;
            else score+=clearLine?4f:0f;
            foreach(Enemy existing in enemies)
            {
                if(existing==null||existing.hp<=0f)continue;
                Point previous=existing.Position-Position;
                Point approach=foot-Position;
                float cosine=(approach.x*previous.x+
                    approach.y*previous.y)/
                    Math.Max(.001f,approach.Length*previous.Length);
                score+=(1f-cosine)*(kind==1?4f:2f);
            }
            return score;
        }
        private Point? RegionalBattleFoot(int rank,bool[] connected)
        {
            int room=save.room;
            int kind=room>=1&&room<=4?
                chapterBattleKinds[room-1,rank]:
                room>=5&&room<=7?
                farshoreBattleKinds[room-5,rank]:rank%3;
            int ax=regionalBattleAnchors[room-1,rank,0],
                ay=regionalBattleAnchors[room-1,rank,1];
            Point anchor=new Point(ax+.5f,ay+.5f);
            Point? preferredPad=null;
            float bestScore=float.MinValue;
            int viable=0;
            for(int radius=0;radius<=6&&viable<18;radius++)
            for(int dy=-radius;dy<=radius&&viable<18;dy++)
            for(int dx=-radius;dx<=radius&&viable<18;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point p=new Point(ax+dx+.5f,ay+dy+.5f);
                if(!RegionalBattleFootFree(p,connected))continue;
                viable++;
                float score=RegionalOpeningPadScore(p,anchor,kind);
                if(score>bestScore){bestScore=score;preferredPad=p;}
            }
            if(preferredPad.HasValue)return preferredPad;
            // A changed gate or authored map can eliminate every favored
            // battle pad. The former final fallback used unvalidated legacy
            // coordinates, potentially placing a mandatory enemy IN a wall
            // or on the far side of a closed crossing. Keep those historical
            // positions only if the same live physical checks accept them.
            int[,] original={
                {11,12},{15,17},{21,15},{24,6},
                {9,7},{30,16},{23,19},{14,9}
            };
            Point legacy=new Point(original[rank,0],original[rank,1]);
            if(RegionalBattleFootFree(legacy,connected))return legacy;
            // Last resort: look beyond the old compact arena, but ONLY on
            // the hero-reachable component and with the same collision,
            // object and enemy clearances. This path runs on exceptional
            // blocked layouts, never every combat tick or normal spawn.
            Point nearest=new Point();float best=float.MaxValue;
            int width=Map.rows[0].Length;
            for(int y=2;y<Map.rows.Length-2;y++)
            for(int x=2;x<width-2;x++)
            {
                int index=y*width+x;
                if(!connected[index])continue;
                Point candidate=new Point(x+.5f,y+.5f);
                float dx=candidate.x-(ax+.5f),dy=candidate.y-(ay+.5f);
                float squared=dx*dx+dy*dy;
                if(squared>=best||
                    !RegionalBattleFootFree(candidate,connected,true))continue;
                nearest=candidate;best=squared;
            }
            if(best<float.MaxValue)return nearest;
            // A genuinely sealed pocket has no legal encounter position.
            // Leave it empty instead of manufacturing a wall-spawn, and do
            // not throw from Enter() during a valid closed-gate scenario.
            return null;
        }
        // Chapter I–IV enemy compositions follow their REAL level design.
        // Forest: charging stalkers and a pair of flank archers.
        // Ruins: bow pressure along the tidal causeways.
        // Mine: armored foremen with two genuine ranged threats.
        // Citadel: staggered combined-arms defensive lines.
        // Do NOT change the old 6/8 required roster or enemy hit points.
        // The three later chapters keep their seven actors, gate flow and
        // actual placement, but now present DIFFERENT opening enemy roles.
        // This data is transient spawn content, not SaveData v2 state.
        private static readonly int[,] chapterBattleKinds={
            {0,0,1,0,1,0,0,1},
            {1,0,1,2,1,0,1,2},
            {2,0,2,1,2,0,2,1},
            {2,1,0,2,1,0,2,1}
        };
        // Farshore's required encounters used to cycle 0,1,2 in every
        // single region, producing three indistinguishable approaches.
        // Roles now follow the actual biome counterplay and authoring:
        // Mistport: bowmen press the causeway and foghorn catches arrows;
        // Glass Desert: plated sentries make the prism armor-break useful;
        // Frostchime: melee rushers force use of the stillbell's long stun.
        // Slot counts, historic map spawn anchors, enemy HP and all save
        // fields stay untouched; voluntary patrols and Boss fights differ.
        private static readonly int[,] farshoreBattleKinds={
            {1,0,1,0,2,1,0},
            {2,1,2,0,1,2,0},
            {0,0,2,0,1,2,0}
        };
        private void SpawnRegionalBaselineEnemies(int count,float health)
        {
            bool[] connected=RegionalBattleReachableTiles();
            for(int i=0;i<count;i++)
            {
                int kind=save.room>=1&&save.room<=4?
                    chapterBattleKinds[save.room-1,i]:
                    save.room>=5&&save.room<=7?
                    farshoreBattleKinds[save.room-5,i]:i%3;
                Point? foot=RegionalBattleFoot(i,connected);
                if(!foot.HasValue)break;
                Enemy staged=CreatePlacedEnemy(++enemyId,kind,
                    foot.Value,health);
                enemies.Add(staged);
                // Original required chapter roster stays on its authored
                // accessible near-side route. Distant unalerted squads sleep
                // until the player physically approaches their district.
                regionalAmbientEnemies.Add(staged);
            }
        }


        // Remote roads occupy most of the actual 120x80 map, while the
        // mandatory story roster stays near the western entrance. Two
        // optional, non-quest ambushes at existing outer ridge markers give
        // those long walks genuine biome-specific combat. They only wake
        // when a real player reaches their district, without multiplying
        // the mandatory story kill counter or demanding extra boss clears.
        // Only the once-cleared district flag persists in SaveData v2.
        private readonly HashSet<int> remoteRoadVisited=new HashSet<int>();
        private readonly List<Enemy> remoteRoadFoes=new List<Enemy>();
        private readonly HashSet<Enemy> remoteRoadDefeated=new HashSet<Enemy>();
        private readonly HashSet<Enemy> remoteRoadCaptured=new HashSet<Enemy>();
        private int remoteRoadRoom=-1,remoteRoadDistrict=-1;
        // A voluntarily signed post-clear watch uses the exact same
        // physical actor/death/capture contract as the first road battle.
        // It can never revoke the original permanently safe crossing.
        private bool remoteRoadVeteran;
        // The original ridge marker offers one honest, physical terrain
        // counter per ACTIVE fight. It is never serialized or farmable.
        private bool remoteRoadSignalUsed;
        // Exact actor and live-world follow-up timer; never serialized.
        private Enemy remoteRoadCounterTarget;
        private float remoteRoadCounterUntil;
        private bool remoteRoadCounterRewarded;
        public bool RemoteRoadSignalUsed
        {get{return RemoteRoadEncounterActive&&remoteRoadSignalUsed;}}
        // A blocked tidal route can make a complete two-foe wave impossible.
        // Retry at most once every two world seconds, never every frame.
        private float remoteRoadRetrySeconds;
        private float remoteRoadDisengageSeconds;
        private WorldObject remoteRoadSource;
        // A paid, preexisting SAFE ridge transfer must never arrive as
        // an involuntary trigger for an OPTIONAL road battle. This is
        // ephemeral per-map/session state, not a save-v2 clearance flag.
        // Protection expires only on actually walking out of the crossing.
        private WorldObject remoteRoadSafeArrival;
        private MapData remoteRoadSafeArrivalMap;
        private SaveData remoteRoadSafeArrivalSave;
        private MapData remoteRoadMap;
        private SaveData remoteRoadSave;
        private static readonly int[,] remoteRoadKinds={
            {0,1, 2,0}, // Forest: root stalker and bow hunter; heavy deep-wood scout
            {1,1, 0,2}, // Ruins: exposed longbow crossing; armored flood guard
            {2,0, 1,2}, // Mine: heavy linebreakers; remote sentry at the rails
            {2,1, 0,2}, // Citadel: shield/bow screen; outrider in the far ring
            {1,0, 1,2}, // Harbor: long-range fog shots and armored pier guard
            {2,1, 0,2}, // Desert: plated heatwatch with distant bow pressure
            {0,2, 1,0}  // Frost: fast flank and weighty bellwatch
        };
        private static readonly int[,] remoteRoadVeteranKinds={
            {2,1,1,2}, // Forest: plated wardens and distant lookouts
            {2,0,2,1}, // Ruins: armored flood guard and skirmishers
            {1,2,2,0}, // Mine: crossfire behind a heavy breach line
            {1,2,0,1}, // Citadel: elite bow screen and mobile guards
            {2,1,2,0}, // Mistport: thick pier guard and fog archers
            {1,2,2,1}, // Desert: shielded heatwatch and ranged fire
            {2,1,1,2}  // Frost: disciplined bellwatch with archers
        };
        public bool RemoteRoadVeteranDone(int region,int district)
        { return region>=1&&region<=7&&district>=0&&district<2&&
            Has("remote_road_veteran_"+region+"_"+district); }
        public int RemoteRoadVeteranWins
        { get {
            int total=0;
            for(int r=1;r<=7;r++)
            for(int d=0;d<2;d++)
                if(RemoteRoadVeteranDone(r,d))total++;
            return total;
        }}
        public bool RemoteRoadVeteranActive
        {get{return RemoteRoadEncounterActive&&remoteRoadVeteran;}}
        public bool RemoteRoadEncounterActive
        { get { return remoteRoadRoom==save.room&&
            remoteRoadDistrict>=0&&
            Object.ReferenceEquals(remoteRoadMap,Map)&&
            Object.ReferenceEquals(remoteRoadSave,save)&&
            remoteRoadFoes.Count==2; } }
        public int RemoteRoadActiveDistrict
        { get { return RemoteRoadEncounterActive?remoteRoadDistrict:-1; } }
        public int RemoteRoadRemaining
        { get {
            int count=0;
            if(RemoteRoadEncounterActive)
                foreach(Enemy e in remoteRoadFoes)
                    if(e!=null&&!remoteRoadDefeated.Contains(e)&&
                       !remoteRoadCaptured.Contains(e)&&
                       e.hp>0f&&enemies.Contains(e))count++;
            return count;
        }}
        public bool RemoteRoadCleared(int region,int district)
        { return region>=1&&region<=7&&district>=0&&district<2&&
            Has("remote_road_clear_"+region+"_"+district); }
        public int RemoteRoadClearedCount
        { get {
            int count=0;
            for(int room=1;room<=7;room++)
            for(int district=0;district<2;district++)
                if(RemoteRoadCleared(room,district))count++;
            return count;
        }}
        // A genuine encounter points toward the nearest surviving foe.
        // Source markers guide the optional journal when no fight is live;
        // no persistent enemy ID or fabricated destination is introduced.
        public Point RemoteRoadGuideTarget
        { get {
            if(!RemoteRoadEncounterActive)return Position;
            Point best=new Point(remoteRoadSource.x,remoteRoadSource.y);
            float distance=float.MaxValue;
            foreach(Enemy foe in remoteRoadFoes)
            {
                if(!IsRemoteRoadThreat(foe))continue;
                float next=(Position-foe.Position).Length;
                if(next<distance){best=foe.Position;distance=next;}
            }
            return best;
        }}
        private bool IsRemoteRoadEnemy(Enemy e)
        { return e!=null&&RemoteRoadEncounterActive&&remoteRoadFoes.Contains(e); }
        // Presentation reads only EXACT live actors: duplicate numeric IDs
        // cannot paint an unrelated main-story monster as a road ambusher.
        public bool IsRemoteRoadThreat(Enemy e)
        { return IsRemoteRoadEnemy(e)&&e.hp>0f&&enemies.Contains(e); }
        private void OnRemoteRoadEnemyDefeated(Enemy e)
        {
            if(IsRemoteRoadEnemy(e))remoteRoadDefeated.Add(e);
        }
        private bool OnRemoteRoadEnemyRestrained(Enemy e)
        {
            if(!IsRemoteRoadEnemy(e)||e.hp<=0f||enemies.Contains(e))
                return false;
            remoteRoadCaptured.Add(e);
            return true;
        }
        // A cancelled or lost encounter can be fought again without
        // teleporting away or awarding an unearned world-state receipt.
        private void SetRemoteRoadSafeArrival(WorldObject marker)
        {
            remoteRoadSafeArrival=save.room>=1&&save.room<=7&&
                marker!=null&&marker.kind=="ridgemarker"&&
                SourceBelongsToActiveMap(marker)?marker:null;
            remoteRoadSafeArrivalMap=remoteRoadSafeArrival==null?null:Map;
            remoteRoadSafeArrivalSave=remoteRoadSafeArrival==null?null:save;
        }
        // Saving at a recently transported or still-dangerous crossing
        // must not reload directly into an unannounced ambush on the very
        // first simulated frame. Recover only local spawn safety; this does
        // NOT persist a fake battle clear or lock out later on-foot combat.
        private void RestoreRoadLandingFromSavedFoot()
        {
            if(save.room<1||save.room>7||Map.objects==null||
                !CanStand(Position))return;
            foreach(WorldObject marker in Map.objects)
            {
                if(marker==null||marker.kind!="ridgemarker"||
                    (marker.id!="ridge_route_"+save.room+"_0"&&
                     marker.id!="ridge_route_"+save.room+"_1"))continue;
                Point site=new Point(marker.x,marker.y);
                if((Position-site).Length<=1.8f)
                {
                    SetRemoteRoadSafeArrival(marker);
                    return;
                }
            }
        }
        // Actual foot movement beyond the landing neighborhood rearms
        // ordinary discovery. A quest pin or scene-independent coordinate
        // cannot grant this temporary immunity.
        private bool RemoteRoadArrivalProtected(int district)
        {
            if(remoteRoadSafeArrival==null)return false;
            if(!System.Object.ReferenceEquals(remoteRoadSafeArrivalMap,Map)||
                !System.Object.ReferenceEquals(remoteRoadSafeArrivalSave,save)||
                !SourceBelongsToActiveMap(remoteRoadSafeArrival)||
                (Position-new Point(remoteRoadSafeArrival.x,
                    remoteRoadSafeArrival.y)).Length>10.5f)
            {
                SetRemoteRoadSafeArrival(null);
                return false;
            }
            return remoteRoadSafeArrival.id==
                "ridge_route_"+save.room+"_"+district;
        }
        public int RemoteRoadProtectedDistrict
        { get {
            for(int district=0;district<2;district++)
                if(RemoteRoadArrivalProtected(district))return district;
            return -1;
        }}
        public bool RemoteRoadSafeArrivalActive
        { get { return RemoteRoadProtectedDistrict>=0; } }
        private void ResetRemoteRoadEncounter(bool resetDistricts=false,
            bool retry=false)
        {
            if(retry&&remoteRoadDistrict>=0)
            {
                remoteRoadVisited.Remove(remoteRoadDistrict);
                remoteRoadRetrySeconds=2f;
            }
            projectiles.RemoveAll(p=>p!=null&&p.hostileShooter!=null&&
                remoteRoadFoes.Contains(p.hostileShooter));
            foreach(Enemy e in remoteRoadFoes)
            {
                RemoveEnemyPendingWarning(e);
                ForgetOrdinaryPatrolSight(e);
                enemyHitAlert.Remove(e);
                regionalAmbientEnemies.Remove(e);
                enemies.Remove(e);
            }
            remoteRoadFoes.Clear();
            remoteRoadDefeated.Clear();
            remoteRoadCaptured.Clear();
            remoteRoadRoom=-1;remoteRoadDistrict=-1;
            remoteRoadVeteran=false;
            remoteRoadSignalUsed=false;
            remoteRoadCounterTarget=null;remoteRoadCounterUntil=0f;
            remoteRoadCounterRewarded=false;
            remoteRoadSource=null;remoteRoadDisengageSeconds=0f;
            remoteRoadMap=null;remoteRoadSave=null;
            if(resetDistricts)
            {
                remoteRoadVisited.Clear();
                remoteRoadRetrySeconds=0f;
                SetRemoteRoadSafeArrival(null);
            }
        }
        // The first raider must be visible, and BOTH must stand inside
        // the real eight-tile ordinary enemy aggro range. Cover may shield
        // the second flanker, but never invent a distant unseen ambush.
        private bool RemoteRoadCombatFootClear(Point foot,Point source,
            int slot,List<Point> selected)
        {
            float range=(foot-Position).Length;
            return range>=5f&&range<=8f&&
                (slot!=0||ClearAttackPath(Position,foot))&&
                RegionalScoutFootClear(foot,source,true,selected);
        }
        // Rank only physically valid positions. A ranged scout pressures
        // from a readable 6-7 tile shooting lane while plated infantry takes
        // a shorter approach; a second opponent seeks an opposing route.
        // Both can exploit the authentic ridge's counter LOS when the
        // real terrain allows it. Never create a spawn via score alone.
        private float RemoteRoadPadScore(Point foot,Point marker,
            int kind,int slot,List<Point> already)
        {
            float distance=(foot-Position).Length;
            float preferred=kind==1?6.75f:kind==2?5.8f:5.3f;
            float score=-Math.Abs(distance-preferred)*1.65f;
            if((foot-marker).Length<=8.6f&&
                ClearAttackPath(marker,foot))score+=2.75f;
            // Both slots can contain a bow. Favor a real unobstructed
            // arrow sweep, not a visually open centerline whose muzzle
            // or flight radius hits nearby masonry.
            if(kind==1&&ClearAttackPath(foot,Position))
                score+=PhysicalBowLaneFromFoot(foot,Position)?12f:-10f;
            if(slot==1&&already.Count>0)
            {
                Point first=(already[0]-Position).Normalized;
                Point second=(foot-Position).Normalized;
                float dot=first.x*second.x+first.y*second.y;
                score+=(1f-dot)*10f+
                    Math.Min(16f,(foot-already[0]).Length)*.15f;
                // A visible archer can establish crossfire; a hidden one
                // may still walk out from physically reachable cover.
                if(kind==1&&ClearAttackPath(foot,Position))score+=1.8f;
            }
            return score;
        }
        // These are authentic map objects, not fabricated markers or an
        // automatic world teleport. A whole two-enemy wave must fit BEFORE
        // either opponent is created. Both candidates must have a complete
        // walking route, avoid active allies/quest props and retain spacing.
        private bool BeginRemoteRoadEncounter(WorldObject marker,int district,
            bool veteran=false)
        {
            if(marker==null||save.room<1||save.room>7||
               !SourceBelongsToActiveMap(marker)||district<0||district>1||
               (veteran&&!RemoteRoadCleared(save.room,district))||
               (!veteran&&RemoteRoadCleared(save.room,district)))
                return false;
            Point center=new Point(marker.x,marker.y);
            var pads=new List<Point>();
            int[,] dirs={{1,0},{0,1},{-1,0},{0,-1},
                         {1,1},{-1,1},{-1,-1},{1,-1}};
            // Score the FULL nearby physical ring rather than taking the
            // first valid compass ray. Formation is selected as a WHOLE,
            // before either raider is created. Hero approach, walls, allies,
            // quest props and 16-tile walkability still veto each candidate.
            for(int slot=0;slot<2;slot++)
            {
                int kind=veteran?
                    remoteRoadVeteranKinds[save.room-1,district*2+slot]:
                    remoteRoadKinds[save.room-1,district*2+slot];
                Point bestFoot=new Point();
                float bestScore=float.NegativeInfinity;
                bool found=false;
                for(int radius=5;radius<=8;radius++)
                for(int offset=0;offset<8;offset++)
                {
                    int d=(save.room*3+district*5+slot*4+offset)%8;
                    Point foot=new Point(
                        (float)Math.Floor(marker.x)+.5f+
                            dirs[d,0]*radius,
                        (float)Math.Floor(marker.y)+.5f+
                            dirs[d,1]*radius);
                    if(!RemoteRoadCombatFootClear(foot,center,slot,pads))
                        continue;
                    float score=RemoteRoadPadScore(foot,center,
                        kind,slot,pads);
                    if(!found||score>bestScore+.0001f)
                    {bestFoot=foot;bestScore=score;found=true;}
                }
                if(found)pads.Add(bestFoot);
                else break;
            }
            // Odd original map corridors may lack the main compass pads.
            // Keep the exhaustive 5-9-tile bounded fallback, but pick the
            // actual best legal approach, not the first x/y scan tile.
            // A blocked second actor cancels the ENTIRE optional encounter.
            for(int slot=pads.Count;slot<2;slot++)
            {
                int kind=veteran?
                    remoteRoadVeteranKinds[save.room-1,district*2+slot]:
                    remoteRoadKinds[save.room-1,district*2+slot];
                int sx=(int)Math.Floor(marker.x),sy=(int)Math.Floor(marker.y);
                Point bestFoot=new Point();
                float bestScore=float.NegativeInfinity;
                bool found=false;
                for(int radius=5;radius<=9;radius++)
                for(int dy=-radius;dy<=radius;dy++)
                for(int dx=-radius;dx<=radius;dx++)
                {
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                       Math.Abs(dx)+Math.Abs(dy)>18)continue;
                    Point foot=new Point(sx+dx+.5f,sy+dy+.5f);
                    if(!RemoteRoadCombatFootClear(foot,center,slot,pads))
                        continue;
                    float score=RemoteRoadPadScore(foot,center,
                        kind,slot,pads);
                    if(!found||score>bestScore+.0001f)
                    {bestFoot=foot;bestScore=score;found=true;}
                }
                if(!found)break;
                pads.Add(bestFoot);
            }
            if(pads.Count!=2)return false;
            remoteRoadRoom=save.room;remoteRoadDistrict=district;
            remoteRoadVeteran=veteran;
            remoteRoadSignalUsed=false;
            remoteRoadCounterTarget=null;remoteRoadCounterUntil=0f;
            remoteRoadCounterRewarded=false;
            remoteRoadSource=marker;remoteRoadDisengageSeconds=0f;
            remoteRoadMap=Map;remoteRoadSave=save;
            for(int slot=0;slot<2;slot++)
            {
                Enemy e=CreatePlacedEnemy(++enemyId,
                    veteran?
                        remoteRoadVeteranKinds[save.room-1,district*2+slot]:
                        remoteRoadKinds[save.room-1,district*2+slot],
                    pads[slot],
                    veteran?68+save.room*16+district*12:
                        40+save.room*11+district*8);
                remoteRoadFoes.Add(e);
                enemies.Add(e);
                regionalAmbientEnemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.6f)
                    {variant=e.kind});
            }
            revision++;sound="alert";
            Notify(veteran?
                "进阶巡护：两名精锐应约来到旧路口。击败或活捉后可领取首次回执；旧路继续安全。":
                district==0?
                "外环巡路：前方出现两名拦路者。战斗或活捉均可解除威胁。":
                "高地旧路：另一队巡哨发现了你，守住这条实际通路。",
                veteran?
                "Veteran roadwatch: two elite challengers accepted your call. Defeat or restrain them for a first-clear receipt. Civilian passage stays safe.":
                district==0?
                "Outer road: two raiders hold this crossing. Defeat or restrain them.":
                "Far ridge: another patrol has spotted you. Hold the real crossing.");
            return true;
        }
        private void TickRemoteRoadEncounters(float dt)
        {
            if(remoteRoadRetrySeconds>0f)
                remoteRoadRetrySeconds=Math.Max(0f,remoteRoadRetrySeconds-dt);
            if(remoteRoadDistrict>=0)
            {
                if(!RemoteRoadEncounterActive||Dying||
                    remoteRoadSource==null||
                    !SourceBelongsToActiveMap(remoteRoadSource))
                { ResetRemoteRoadEncounter(false,true);return; }
                // Disappeared enemies and scripted removals are not earned
                // victories; only the actual death / snare transaction signs.
                foreach(Enemy e in remoteRoadFoes)
                    if(e==null||(!remoteRoadDefeated.Contains(e)&&
                        !remoteRoadCaptured.Contains(e)&&
                        (!enemies.Contains(e)||e.hp<=0f)))
                    { ResetRemoteRoadEncounter(false,true);return; }
                // A voluntary crossing battle is not an unbounded pursuit:
                // running far from its authentic ridge for 1.6 seconds
                // retires these exact actors without any completion credit.
                Point crossing=new Point(remoteRoadSource.x,remoteRoadSource.y);
                if((Position-crossing).Length>26f)
                    remoteRoadDisengageSeconds+=dt;
                else remoteRoadDisengageSeconds=0f;
                if(remoteRoadDisengageSeconds>=1.6f)
                {
                    ResetRemoteRoadEncounter(false,true);
                    revision++;
                    Notify("你已离开伏击地带。敌人撤回路口，返回后可以再次迎战。",
                        "You escaped the ambush. Return to the crossing to try again.");
                    return;
                }
                if(remoteRoadDefeated.Count+remoteRoadCaptured.Count<2)
                    return;
                int district=remoteRoadDistrict,region=remoteRoadRoom;
                bool veteran=remoteRoadVeteran;
                bool humane=remoteRoadCaptured.Count==2;
                ResetRemoteRoadEncounter();
                if(veteran)
                {
                    // Repeatable physical challenge, ONE legitimate signed
                    // reward. Replays cannot farm coins, XP, shards or
                    // another mastery point. No existing road clear changes.
                    if(!RemoteRoadCleared(region,district)||
                       RemoteRoadVeteranDone(region,district))
                    {
                        revision++;sound="quest";
                        Notify("进阶巡护复训完成。原路仍安全；重复挑战不发放奖励。",
                            "Veteran roadwatch rematch complete. Crossing stays safe; no repeat rewards.");
                        return;
                    }
                    Flag("remote_road_veteran_"+region+"_"+district);
                    CreditCoins(28+region*4);
                    GrantXp(55+region*8,true);
                    if(humane)CreditShards(2);
                    RecordMoment("进阶远路巡护 · "+data.maps[region].name+
                        "第"+(district+1)+"处路口精锐已真实击退"+
                        (humane?"，全员活捉。":"。"),
                        "Veteran roadwatch: defeated the real elite squad at crossing "+
                        (district+1)+" in "+data.maps[region].en+
                        (humane?", capturing both alive.":"."));
                    if(RemoteRoadVeteranWins==14&&
                       !Has("remote_road_veteran_mastered"))
                    {
                        Flag("remote_road_veteran_mastered");
                        CreditSkillPoints(1);
                        Notify("十四处进阶远路巡护完成，获得技能点1。",
                            "All fourteen veteran roadwatches completed: +1 talent point.");
                    }
                    else Notify("进阶巡护回执 "+RemoteRoadVeteranWins+
                        "/14：领取首次硬币、经验"+
                        (humane?"与非致命碎片奖励。":"奖励。"),
                        "Veteran roadwatch "+RemoteRoadVeteranWins+
                        "/14: first-clear coins and XP"+
                        (humane?", plus mercy shards.":"."));
                    revision++;sound="reward";
                    return;
                }
                if(RemoteRoadCleared(region,district))return;
                Flag("remote_road_clear_"+region+"_"+district);
                CreditCoins(18+region*3);
                GrantXp(32+region*5,true);
                if(humane)CreditShards(1);
                RecordMoment("远路巡护 · 在"+data.maps[region].name+
                    "徒步清理第"+(district+1)+"处真实外环伏击"+
                    (humane?"，两名袭击者都被活捉。":"。"),
                    "Far-road watch: cleared the "+(district+1)+
                    " real outer crossing in "+data.maps[region].en+
                    (humane?", with both raiders alive.":"."));
                revision++;sound="quest";
                Notify("远路安全 "+(district+1)+"/2 · 获得硬币"+
                    (18+region*3)+"、经验"+(32+region*5)+
                    (humane?"、非致命巡护碎片1。":"。")+
                    " 原路标现可自愿发起精锐进阶巡护。",
                    "Remote crossing secured "+(district+1)+"/2: "+
                    (18+region*3)+" coins, "+(32+region*5)+
                    " XP"+(humane?", +1 mercy shard.":".")+
                    " Veteran roadwatch is now available voluntarily at this ridge marker.");
                return;
            }
            if(mode!="world"||Dying||practicing||racing||
               save.room<1||save.room>7||Map.objects==null||
               !CanStand(Position)||!SafeTravel||
               // A brief quiet gap in a signed patrol, rescue or timed
               // field investigation is NOT consent to start extra combat.
               FieldWorkPreventsRest||ActionTransitionBlocked||
               remoteRoadRetrySeconds>0f||
               FieldPatrolActive||NightShiftRaidActive||
               NightShiftVentSeconds>0f||RidgeWatchCallPending||
               RidgeWatchAlarmActive||CivicCourierActive||
               (RemoteRoadCleared(save.room,0)&&
                RemoteRoadCleared(save.room,1)))
                return;
            for(int district=0;district<2;district++)
            {
                if(remoteRoadVisited.Contains(district)||
                   RemoteRoadCleared(save.room,district)||
                   RemoteRoadArrivalProtected(district))continue;
                WorldObject marker=null;
                string id="ridge_route_"+save.room+"_"+district;
                foreach(WorldObject o in Map.objects)
                    if(o!=null&&o.kind=="ridgemarker"&&o.id==id)
                    {marker=o;break;}
                if(marker==null||!IsObjectPresent(marker)||
                   (Position-new Point(marker.x,marker.y)).Length>8.5f)
                    continue;
                if(BeginRemoteRoadEncounter(marker,district))
                    remoteRoadVisited.Add(district);
                else
                    remoteRoadRetrySeconds=2f;
                break;
            }
        }

        // Only the baseline authored overworld roster sleeps when the
        // player is in a DIFFERENT local encounter district. Special
        // escorts, patrols, Bosses, alert sound searches and event raiders
        // are never tagged. Exact actor identity is session-only.
        private readonly HashSet<Enemy> regionalAmbientEnemies=
            new HashSet<Enemy>();

        // The large original overworld rosters (and optional live survey
        // patrols) need a readable two-lane attack rhythm, not six
        // simultaneous contact warnings at one foot. A single close-range
        // raider and a single archer may commit at once. Other actors keep
        // walking, flanking and exposing themselves to real player attacks.
        // Bosses, civilians, scripted objectives and independently spawned
        // battle actors retain their existing attack contracts.
        //
        // This is an actor-IDENTITY check: reused numeric spawn IDs do not
        // own a combat lane and there is no new persistent SaveData state.
        private bool RegionalVolleySlotAvailable(Enemy candidate)
        {
            if(candidate==null||candidate.boss||
                !regionalAmbientEnemies.Contains(candidate))return true;
            bool bow=candidate.kind==1;
            int active=0;
            foreach(Enemy other in enemies)
            {
                if(other==null||System.Object.ReferenceEquals(other,candidate)||
                   !regionalAmbientEnemies.Contains(other)||other.hp<=0f||
                   other.boss||other.state!=1||
                   !CanStand(other.Position)||
                   (other.Position-Position).Length>10f||
                   (other.target-Position).Length>2.75f||
                   other.targetingWayfarer||other.targetingReliefCivilian||
                   other.targetingAfterlightStock||
                   other.targetingOuterPatrolStock||
                   other.targetingSagaGuardEvidence)continue;
                // At most one close-range ring/sword lane and one ranged
                // pressure lane can warn the same hero in this locality.
                if((other.kind==1)==bow)return false;
                active++;
                if(active>=2)return false;
            }
            return true;
        }
        private bool RegionalAmbientEncounterDormant(Enemy e)
        {
            return e!=null&&!e.boss&&regionalAmbientEnemies.Contains(e)&&
                // A paid, ACTIVE patrol is a current player contract, not
                // background ambient scenery. Its authentic cooldowns and
                // threatened actor simulation continue when the hero moves
                // behind a distant post; it still obeys normal perception,
                // attack lanes and the 8-tile non-alerted chase boundary.
                !(FieldPatrolActive&&fieldPatrolEnemies.Contains(e))&&
                save.room>=1&&save.room<=7&&
                (e.Position-Position).Length>13f&&
                e.state==0&&e.hitTime<=0f&&e.stun<=0f&&
                e.releaseTime<=0f&&
                !IsEnemyInvestigatingImpact(e)&&
                !IsEnemyReturningFromImpact(e);
        }
        // Distant wilderness patrols are OPTIONAL local challenges invoked
        // only by the actual field-survey transaction. They must not move
        // the old campaign's mandatory room roster out of reach, nor turn
        // a distant core-Boss gate into a whole-map hunting chore.
        // Preserve the region's deliberate first-choice patrol approaches.
        // For a paid contract on a winding road, an open WALKING route is
        // stronger evidence than eight straight compass rays. Never spawn
        // inside another actor, beside a quest prop or through a closed gate.
        private bool RegionalScoutFootClear(Point p,Point source,
            bool requireFull,List<Point> selected)
        {
            if(!Finite(p.x)||!Finite(p.y)||!CanStand(p)||
                (p-Position).Length<5f)return false;
            // A field patrol cannot appear under the feet of a REAL
            // following companion or escorted civilian while the player
            // signs a survey post. Preserve the existing player/enemy
            // clearances and require an alternate real physical approach.
            if(save.companion>=0&&
                (p-companionPosition).Length<1.45f)return false;
            if(ReliefEscortActive&&reliefMissionRoom==save.room&&
                (p-reliefFollowerPosition).Length<1.45f)return false;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&IsObjectPresent(o)&&
                    (new Point(o.x,o.y)-p).Length<4.5f)
                    return false;
            // Trees, houses and ruined columns are painted taller than
            // their terrain tiles. Reuse the authored visual-foot radius
            // of normal chapter battles; never harden them into new walls.
            if(Map.decor!=null)
                foreach(Decoration visual in Map.decor)
                {
                    float radius=RegionalDecorationEnemyPad(visual);
                    if(radius>0f&&
                        (p-new Point(visual.x,visual.y)).Length<radius+.25f)
                        return false;
                }
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0&&(e.Position-p).Length<5f)
                    return false;
            if(selected.Exists(q=>(q-p).Length<5f))return false;
            // A clear projectile line from the survey board does not mean
            // that a raider can actually walk to the player. Flood islands
            // and sealed quarry pockets must never spawn unreachable melee.
            // Keep the old field-contract LOS rule for optional patrols,
            // but require a genuine local walking approach in both modes.
            return ActorHasLocalRoute(Position,p,16)&&
                (requireFull||ClearAttackPath(source,p));
        }
        // Different field contracts should play differently on the actual
        // regional ground, rather than reusing a rotating generic roster.
        // Kind 0 closes on foot, 1 creates genuine arrow lanes, and 2
        // threatens slower armored area contacts. Rank determines which
        // real traversable approach pad receives each role; no extra
        // mandatory story enemies and no saved enemy-schema changes.
        private static readonly int[,] fieldPatrolKinds={
            {0,0,0}, // Forest: two aggressive hunters through root cover
            {1,1,0}, // Flooded ruins: twin bows screened by a close raider
            {2,0,2}, // Mine: heavy foremen guarding a mobile skirmisher
            {2,1,0}, // Citadel: armored front and long-range sentry
            {0,1,0}, // Harbor: melee pressure with a shore archer
            {0,2,1}, // Desert: fast pursuit, heavy slam and bow fire
            {1,2,0}  // Highlands: elevated bow and armor cross-pressure
        };
        // Elite patrols retain the original reachable physical pads and actors.
        private static readonly int[,] veteranPatrolKinds={
            {1,2,0},{2,0,1},{1,2,0},{1,2,0},
            {2,1,0},{1,0,2},{2,0,1}
        };
        // Field contracts previously chose the FIRST available compass
        // coordinate, independent of the enemy actually assigned there.
        // Score only the original fully authenticated, real walking pads:
        // bows seek open firing lanes, plated brutes hold the center, and
        // fast melee raiders try a separate approach from earlier squadmates.
        // The fallbacks still use precisely the same physical clearance
        // rules; no teleport, phantom attack or additional saved enemy.
        private float RegionalScoutTacticalPadScore(Point foot,
            Point source,int kind,List<Point> assigned,int rank)
        {
            float distance=(foot-source).Length;
            float preferred=kind==1?10.0f:kind==2?8.2f:7.6f;
            float score=28f-Math.Abs(distance-preferred)*2f;
            bool sourceLine=ClearAttackPath(foot,source);
            bool playerLine=ClearAttackPath(foot,Position);
            if(kind==1)
            {
                // The survey marker is an anchor, not a damageable actor.
                // Preserve its route visibility, but reward a real firing
                // lane to the player, including projectile width and muzzle.
                // An obstructed candidate is still legal fallback ground;
                // no enemy is deleted when every approach has bad cover.
                score+=sourceLine?11f:-8f;
                score+=playerLine?
                    (PhysicalBowLaneFromFoot(foot,Position)?23f:-21f):
                    -13f;
            }
            else if(kind==2)
            {
                score+=sourceLine?8f:0f;
                score+=playerLine?4f:0f;
            }
            else
            {
                score+=sourceLine?5f:0f;
                score+=playerLine?5f:0f;
            }
            Point approach=foot-source;
            for(int i=0;i<assigned.Count;i++)
            {
                Point previous=assigned[i]-source;
                float separation=(foot-assigned[i]).Length;
                float cosine=(approach.x*previous.x+
                    approach.y*previous.y)/
                    Math.Max(.001f,approach.Length*previous.Length);
                // Distinct genuine attack lanes are much more readable
                // than three bodies sharing a diagonal from the survey post.
                score+=(1f-cosine)*(kind==1?13f:9f);
                if(separation<6.3f)score-=13f;
            }
            // Deterministic, tiny rank bias avoids flapping when two
            // otherwise equal physical tiles trade position after a map
            // edit. No random seed or SaveData field is introduced.
            return score-rank*.0001f;
        }
        private int SpawnRegionalScoutEncounter(WorldObject site,
            bool requireFull=false,bool veteran=false)
        {
            if(site==null||site.kind!="biomeSurvey"||save.room<1||
               save.room>7||!SourceBelongsToActiveMap(site)||
               !HasPhysicalInteractionReach(site))return 0;
            int room=save.room;
            int count=(room==2||room==3||room==6)?3:2;
            int[,] directions={
                {1,0},{0,1},{-1,0},{0,-1},
                {1,1},{-1,1},{-1,-1},{1,-1}
            };
            var pads=new List<Point>();
            Point source=new Point(site.x,site.y);
            for(int i=0;i<count;i++)
            {
                int kind=veteran?veteranPatrolKinds[room-1,i]:
                    fieldPatrolKinds[room-1,i];
                Point best=new Point();
                float bestScore=float.MinValue;
                int viable=0,rank=0;
                // Preserve the original near-site eight-bearing roster
                // search, but compare genuine attack lanes instead of
                // committing the first trivially empty compass tile.
                // Evaluate the full 48-bearing ring before ranking actual
                // combat feet. The former 14-candidate cutoff favored the
                // first authored directions and repeatedly hid better
                // flanking and bow lanes on later bearings.
                for(int radius=7;radius<=12;radius++)
                for(int shift=0;shift<8;shift++)
                {
                    int direction=(room*2+i*3+shift)%8;
                    float dx=directions[direction,0],dy=directions[direction,1];
                    Point p=new Point((float)Math.Floor(source.x)+.5f+dx*radius,
                        (float)Math.Floor(source.y)+.5f+dy*radius);
                    if(!RegionalScoutFootClear(p,source,requireFull,pads))
                        continue;
                    viable++;
                    float score=RegionalScoutTacticalPadScore(
                        p,source,kind,pads,rank++);
                    if(score>bestScore){bestScore=score;best=p;}
                }
                // A few handcrafted bearing rays can land inside a flood,
                // under painted cover or beside a mission NPC. Search
                // alternate legal 4-way terrain pads, collecting enough
                // real alternatives to rank a useful local tactical lane.
                // Even eight clear compass rays can all lead along the
                // same exposed road. Compare nearby traversable perimeter
                // feet as well, so flood channels, quarry corners and
                // ridge cover produce different actual battle approaches.
                // The bounded candidate budget still limits route checks.
                if(viable<32)
                {
                    int sx=(int)Math.Floor(source.x),
                        sy=(int)Math.Floor(source.y);
                    // Sample more than the first few legal perimeter
                    // feet. Floods, gates and painted cover create narrow
                    // corridors; an early-14 cutoff repeatedly biased
                    // every patrol toward one side of that corridor.
                    // Bound this fallback to 32 authenticated candidates.
                    for(int radius=6;radius<=13&&viable<32;radius++)
                    for(int dy=-radius;dy<=radius&&viable<32;dy++)
                    for(int dx=-radius;dx<=radius&&viable<32;dx++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                            Math.Abs(dx)+Math.Abs(dy)>16)continue;
                        Point p=new Point(sx+dx+.5f,sy+dy+.5f);
                        if(!RegionalScoutFootClear(p,source,requireFull,pads))
                            continue;
                        viable++;
                        float score=RegionalScoutTacticalPadScore(
                            p,source,kind,pads,rank++);
                        if(score>bestScore){bestScore=score;best=p;}
                    }
                }
                if(viable>0)pads.Add(best);
            }
            // A greedy high-value bow pad can occupy the ONLY valid
            // supporting melee approach in a narrow ruined lane. In that
            // case do not quietly delete the rest of a real player-signed
            // squad. Retry the entire unchanged historical first-fit
            // placement, with the SAME actual collision/route restrictions.
            // No enemies or actor IDs were created by the ranking phase.
            if(pads.Count<count)
            {
                pads.Clear();
                for(int i=0;i<count;i++)
                {
                    bool found=false;
                    for(int radius=7;radius<=12&&!found;radius++)
                    for(int shift=0;shift<8&&!found;shift++)
                    {
                        int direction=(room*2+i*3+shift)%8;
                        Point p=new Point(
                            (float)Math.Floor(source.x)+.5f+
                                directions[direction,0]*radius,
                            (float)Math.Floor(source.y)+.5f+
                                directions[direction,1]*radius);
                        if(!RegionalScoutFootClear(
                            p,source,requireFull,pads))continue;
                        pads.Add(p);found=true;
                    }
                }
                if(pads.Count<count)
                {
                    int sx=(int)Math.Floor(source.x),
                        sy=(int)Math.Floor(source.y);
                    for(int radius=6;radius<=13&&pads.Count<count;radius++)
                    for(int dy=-radius;dy<=radius&&pads.Count<count;dy++)
                    for(int dx=-radius;dx<=radius&&pads.Count<count;dx++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                            Math.Abs(dx)+Math.Abs(dy)>16)continue;
                        Point p=new Point(sx+dx+.5f,sy+dy+.5f);
                        if(RegionalScoutFootClear(
                            p,source,requireFull,pads))pads.Add(p);
                    }
                }
            }
            // Paid-off patrol contracts require a FULL wave. Check all feet
            // before touching enemies or actor IDs; no partial free reward.
            if(requireFull&&pads.Count!=count)return 0;
            foreach(Point p in pads)
            {
                int slot=pads.IndexOf(p);
                Enemy patrol=CreatePlacedEnemy(++enemyId,
                    veteran?veteranPatrolKinds[room-1,slot]:
                        fieldPatrolKinds[room-1,slot],p,
                    veteran?50+room*12:30+room*10);
                if(veteran)patrol.stun=.5f;
                enemies.Add(patrol);
                regionalAmbientEnemies.Add(patrol);
            }
            int spawned=pads.Count;
            if(spawned>0)
            {
                revision++;sound="alert";
                Notify("远端地标的动静惊动了本地巡逻敌人。它们只守这片区域，不会横穿整张地图追击。",
                    "Your fieldwork draws a local patrol. These raiders hold this district rather than chasing you across the whole region.");
            }
            return spawned;
        }
        // Seven optional civilian field-patrol contracts reuse the actual
        // surveyed landmarks and real enemies. All active identity belongs
        // to this play session; only signed first-clear receipts use v2 flags.
        private static readonly string[] scoutPatrolZh={
            "","断枝旁的猎人正在抄近路。守住居民的避猎线，别让陷阱再封住回程。",
            "护渠人发现石阶上的巡兵。清理旧路，才能让抬担架的人通过。",
            "矿工被勒令替陌生武装守炉。取回炉门旁丢失的撤离名册。",
            "堡垒的军用口令仍在驱赶平民。击退堵在后门的武装巡守。",
            "港口的流灯引来了趁雾抢粮的人。夺回真正岸标的使用权。",
            "巡兵追着假光塔逼人走进热沙。保住实际山影指向的安全路。",
            "高地的假钟声引走了救援队。拦住埋伏者，再沿背风钟回报。"
        };
        private static readonly string[] scoutPatrolEn={
            "","Hunters are cutting across the broken branches. Defend the travelers' safe bypass.",
            "Raiders block the old high-water stair. Reopen it for the stretcher teams.",
            "Armed strangers commandeered the forge watch. Recover the miners' lost evacuation roll.",
            "Military challenges are still keeping civilians from the citadel side door. Clear the rear guard.",
            "Looters follow wandering harbor lights to steal the food boats. Secure the true coast signal.",
            "A false bright tower lures patrols into the dunes. Protect the route marked by a real mountain shadow.",
            "False bell calls have led rescue teams into a highland ambush. Stop the hunters and report by the sheltered bell."
        };
        private int fieldPatrolRoom=-1;
        private MapData fieldPatrolMap;
        private SaveData fieldPatrolSave;
        private WorldObject fieldPatrolOrigin;
        private readonly List<Enemy> fieldPatrolEnemies=new List<Enemy>();
        private readonly HashSet<Enemy> fieldPatrolKills=new HashSet<Enemy>();
        // Actual live snare restraints are distinct from lethal defeats.
        // Transient exact actors only: no new v2 serialized flag or field.
        private readonly HashSet<Enemy> fieldPatrolCaptures=new HashSet<Enemy>();
        private bool fieldPatrolVeteranActive;
        public bool FieldPatrolVeteranActive
        {get{return FieldPatrolActive&&fieldPatrolVeteranActive;}}
        public bool FieldPatrolVeteranDone(int room)
        {return room>=1&&room<=7&&Has("biome_patrol_veteran_"+room);}
        public bool FieldPatrolVeteranAllLiving(int room)
        {return FieldPatrolVeteranDone(room)&&
            Has("biome_patrol_veteran_spared_"+room);}
        public int FieldPatrolVeteranWins
        {get{int n=0;for(int r=1;r<=7;r++)
            if(FieldPatrolVeteranDone(r))n++;return n;}}

        public bool FieldPatrolActive
        {get{return fieldPatrolRoom>=1&&fieldPatrolRoom<=7&&
            fieldPatrolRoom==save.room&&
            Object.ReferenceEquals(fieldPatrolMap,Map)&&
            Object.ReferenceEquals(fieldPatrolSave,save)&&
            fieldPatrolOrigin!=null&&
            SourceBelongsToActiveMap(fieldPatrolOrigin);}}
        public bool FieldPatrolDone(int room)
        {return room>=1&&room<=7&&Has("biome_patrol_done_"+room);}
        // A genuinely all-living resolution is signed only when the same
        // exact-actor contract is filed at its original field marker.
        // Old v1/v2 patrol receipts remain ordinary completed receipts.
        public bool FieldPatrolSpared(int room)
        {return FieldPatrolDone(room)&&Has("biome_patrol_spared_"+room);}
        public int FieldPatrolSparedCount
        {get{int total=0;for(int r=1;r<=7;r++)
            if(FieldPatrolSpared(r))total++;return total;}}
        public int FieldPatrolWins
        {get{int count=0;for(int i=1;i<=7;i++)
            if(FieldPatrolDone(i))count++;return count;}}
        public int FieldPatrolRemaining
        {get{int count=0;if(FieldPatrolActive)
            foreach(Enemy e in fieldPatrolEnemies)
                if(!fieldPatrolKills.Contains(e)&&
                    !fieldPatrolCaptures.Contains(e)&&
                    enemies.Contains(e)&&e.hp>0f)count++;
            return count;}}
        // The HUD compass follows the nearest genuine surviving opponent.
        // Once all registered deaths are confirmed it points back to the
        // original sign, not an old corpse or a fabricated map location.
        public Point FieldPatrolGuideTarget
        {
            get
            {
                if(!FieldPatrolActive)return Position;
                Point best=new Point(fieldPatrolOrigin.x,fieldPatrolOrigin.y);
                float closest=float.MaxValue;
                foreach(Enemy e in fieldPatrolEnemies)
                {
                    if(e==null||fieldPatrolKills.Contains(e)||
                       fieldPatrolCaptures.Contains(e)||
                       !enemies.Contains(e)||e.hp<=0f)continue;
                    float dist=(e.Position-Position).Length;
                    if(dist<closest){closest=dist;best=e.Position;}
                }
                return best;
            }
        }
        private void TickFieldPatrol()
        {
            if(fieldPatrolRoom<0)return;
            if(!FieldPatrolActive){ResetFieldPatrol();return;}
            // A map edit, script cleanup or invalidated opponent may remove
            // a live actor without a real Hit death. Never turn a missing
            // enemy into a fraudulent civilian safety certificate.
            foreach(Enemy e in fieldPatrolEnemies)
                if(e==null||(!fieldPatrolKills.Contains(e)&&
                   !fieldPatrolCaptures.Contains(e)&&
                   (!enemies.Contains(e)||e.hp<=0f)))
                {
                    ResetFieldPatrol();revision++;
                    Notify("巡逻目标意外失去联络，现场委托已作废；请从原地标重新接取。",
                        "A contracted raider vanished without a real defeat. Return to the post to restart.");
                    return;
                }
        }
        public bool FieldPatrolReportReady
        {get{if(!FieldPatrolActive||fieldPatrolEnemies.Count<2)
            return false;
            foreach(Enemy e in fieldPatrolEnemies)
            {
                if(e==null)return false;
                if(fieldPatrolKills.Contains(e)&&e.hp<=0f)continue;
                if(fieldPatrolCaptures.Contains(e)&&e.hp>0f&&
                    !enemies.Contains(e))continue;
                return false;
            }
            return true;}}
        private bool IsFieldPatrolEnemy(Enemy e)
        {return e!=null&&fieldPatrolEnemies.Contains(e);}
        private void OnFieldPatrolEnemyDefeated(Enemy e)
        {
            if(FieldPatrolActive&&fieldPatrolEnemies.Contains(e))
                fieldPatrolKills.Add(e);
        }
        // Only the paid, validated BindSnaredEnemy() transaction can
        // register the removed living actor; a missing enemy remains invalid.
        private bool OnFieldPatrolEnemyRestrained(Enemy e)
        {
            if(!FieldPatrolActive||e==null||e.hp<=0f||
                !fieldPatrolEnemies.Contains(e)||enemies.Contains(e))
                return false;
            fieldPatrolCaptures.Add(e);
            return true;
        }
        private void ResetFieldPatrol()
        {
            projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                fieldPatrolEnemies.Contains(p.hostileShooter));
            foreach(Enemy e in fieldPatrolEnemies)
            {
                RemoveEnemyPendingWarning(e);
                regionalAmbientEnemies.Remove(e);
                enemies.Remove(e);
            }
            fieldPatrolVeteranActive=false;
            fieldPatrolRoom=-1;fieldPatrolMap=null;
            fieldPatrolSave=null;fieldPatrolOrigin=null;
            fieldPatrolEnemies.Clear();fieldPatrolKills.Clear();
                fieldPatrolCaptures.Clear();
        }
        private Choice FieldPatrolChoice(WorldObject site)
        {
            int room=save.room;
            if(room<1||room>7||site.kind!="biomeSurvey")
                return C("返回原调查地标领取巡逻委托",
                    "Visit the original survey post for patrol work","close");
            if(FieldPatrolActive)
                return FieldPatrolReportReady?
                    C(fieldPatrolVeteranActive?"提交进阶巡护回执":"提交击退或活捉巡兵的现场回执",
                      fieldPatrolVeteranActive?"File veteran patrol report":"File verified raider defeat or restraint receipt",
                      "biome_patrol_report"):
                    C("巡兵仍在现场 · 尚余"+FieldPatrolRemaining+"名",
                      "Raiders remain · "+FieldPatrolRemaining+" active","close");
            if(FieldPatrolVeteranDone(room))
                return C("进阶巡护回执已登记 · 安全线守住了",
                    "Veteran defense filed · route secured","close");
            if(FieldPatrolDone(room))
                return C("进阶巡护 · 精锐混合编队（可活捉）",
                    "Veteran patrol · elite mixed-arms challenge",
                    "biome_patrol_veteran");
            return C("巡路实战 · 击退或绊索活捉盗匪",
                "Field patrol · defeat or snare raiders",
                "biome_patrol_begin");
        }
        private Choice FieldPatrolCancelChoice(WorldObject site)
        {
            return FieldPatrolActive&&site.kind=="biomeSurvey"?
                C("撤回本次巡逻委托","Withdraw this patrol contract",
                  "biome_patrol_abort"):
                C("查看巡逻任务日志","Review patrols in journal",
                  "biome_patrol_journal");
        }
        private bool ChooseFieldPatrol(string action)
        {
            if(action!="biome_patrol_begin"&&
               action!="biome_patrol_veteran"&&
               action!="biome_patrol_report"&&
               action!="biome_patrol_abort"&&
               action!="biome_patrol_journal")return false;
            WorldObject site=pendingDialogueSource;
            int room=save.room;
            if(mode!="world"||Dying||practicing||room<1||room>7||
               site==null||site.kind!="biomeSurvey"||
               site.id!="biomeSurvey"+room||
               pendingDialogueSourceRoom!=room||
               !SourceBelongsToActiveMap(site)||
               !HasPhysicalInteractionReach(site)||!CanStand(Position)||
               !BiomeFieldDone)return true;
            if(action=="biome_patrol_journal")
            {
                journalFocusRequest=FieldPatrolVeteranActive||
                    FieldPatrolDone(room)?"veteran_patrols":"field_patrols";
                revision++;
                return true;
            }
            if(action=="biome_patrol_abort")
            {
                if(!FieldPatrolActive||
                   !Object.ReferenceEquals(fieldPatrolOrigin,site))
                    return true;
                ResetFieldPatrol();revision++;sound="ui";
                Notify("巡路队伍已撤回；没有结算任何额外巡护奖励。",
                    "Patrol withdrawn; no unearned safety reward granted.");
                return true;
            }
            bool veteran=action=="biome_patrol_veteran";
            if((action=="biome_patrol_begin"&&FieldPatrolDone(room))||
               (veteran&&(!FieldPatrolDone(room)||
                           FieldPatrolVeteranDone(room))))return true;
            if(action=="biome_patrol_begin"||veteran)
            {
                if(FieldPatrolActive||FieldWorkPreventsRest||!SafeTravel||
                   racing||BlockTravelForProtectedFieldMission(true))
                {
                    Notify("先完成正在进行的战斗和护送，再接巡路委托。",
                        "Finish active combat and protected field missions before starting a patrol.");
                    return true;
                }
                int before=enemies.Count;
                int count=SpawnRegionalScoutEncounter(site,true,veteran);
                int expected=room==2||room==3||room==6?3:2;
                if(count!=expected)
                {
                    Notify("附近还没有完整可步行的巡兵进路。清理现场后再接委托。",
                        "No complete walkable raider approach remains. Clear the site and retry.");
                    return true;
                }
                fieldPatrolVeteranActive=veteran;
                fieldPatrolRoom=room;fieldPatrolMap=Map;
                fieldPatrolSave=save;fieldPatrolOrigin=site;
                fieldPatrolEnemies.Clear();fieldPatrolKills.Clear();
                fieldPatrolCaptures.Clear();
                for(int i=before;i<enemies.Count;i++)
                {
                    Enemy enemy=enemies[i];
                    fieldPatrolEnemies.Add(enemy);
                    effects.Add(new Effect("enemyRelease",enemy.Position,.6f)
                        {variant=enemy.kind});
                }
                if(veteran)TrackQuest("veteran_patrols");
                revision++;sound="quest";
                if(veteran)
                {
                    Notify("进阶巡护：精锐混合编队在真实道路上就位。击退或绊索活捉整队，返回原地标签领一次性回执。",
                        "Veteran patrol: a stronger mixed-arms squad holds the real road. Defeat or restrain every raider, then report at this post.");
                    return true;
                }
                Notify(scoutPatrolZh[room]+
                    " 击退或用真实绊索活捉"+count+"名巡兵后，回这里交还现场回执。",
                    scoutPatrolEn[room]+
                    " Defeat or genuinely snare and restrain "+count+
                    " raiders, then return to file the report.");
                return true;
            }
            if(!FieldPatrolActive||
               !Object.ReferenceEquals(fieldPatrolOrigin,site))return true;
            if(!FieldPatrolReportReady||!SafeTravel)
            {
                Notify("只有全部巡兵经真实战斗击倒或绊索活捉，且周围安全，才能交回委托。",
                    "Defeat or genuinely restrain each assigned raider, and clear nearby danger before reporting.");
                return true;
            }
            int cleared=room;
            // Capture means actual living actors were removed by the paid
            // grounded snare mechanic. This decision is made BEFORE reset
            // and before awarding any completed/one-time save flags.
            bool allLiving=fieldPatrolEnemies.Count>=2&&
                fieldPatrolCaptures.Count==fieldPatrolEnemies.Count&&
                fieldPatrolKills.Count==0;
            bool mixed=fieldPatrolCaptures.Count>0&&!allLiving;
            bool veteranReport=fieldPatrolVeteranActive;
            ResetFieldPatrol();
            if(veteranReport)
            {
                if(!FieldPatrolDone(cleared)||
                   FieldPatrolVeteranDone(cleared))return true;
                Flag("biome_patrol_veteran_"+cleared);
                if(allLiving)Flag("biome_patrol_veteran_spared_"+cleared);
                CreditCoins(40+cleared*5);
                CreditShards(allLiving?3:2);
                AddXp(100+cleared*12);
                RecordMoment(
                    "进阶巡护 · 在"+data.maps[cleared].name+
                    "的原勘察地标签收精锐巡兵"+
                    (allLiving?"全员活捉的安全证词。":"真实战斗回执。"),
                    "Veteran patrol: filed an elite squad report at the original "+
                    data.maps[cleared].en+" survey post, "+
                    (allLiving?"with every raider restrained alive.":
                        "after a genuine field battle."));
                revision++;sound="reward";
                Notify("进阶巡护已登记 "+FieldPatrolVeteranWins+
                    "/7区：硬币"+(40+cleared*5)+"、碎片"+
                    (allLiving?3:2)+"、经验"+(100+cleared*12)+"。",
                    "Veteran defense signed "+FieldPatrolVeteranWins+
                    "/7: "+(40+cleared*5)+" coins, "+
                    (allLiving?3:2)+" shards, "+(100+cleared*12)+" XP.");
                if(FieldPatrolVeteranWins==7&&
                   !Has("biome_patrol_veteran_complete"))
                {
                    Flag("biome_patrol_veteran_complete");
                    CreditSkillPoints(1);
                    RecordMoment("七处进阶巡护全部完成；危险路口重新有了可信的守路人。",
                        "All seven veteran patrols secured; reliable guards now hold the dangerous roads.");
                    Notify("七区进阶巡护完成：额外技能点1。",
                        "Seven veteran routes defended: +1 talent point.");
                }
                return true;
            }
            if(FieldPatrolDone(cleared))return true;
            Flag("biome_patrol_done_"+cleared);
            if(allLiving)Flag("biome_patrol_spared_"+cleared);
            CreditCoins(26+cleared*2);CreditShards(1);AddXp(65+cleared*5);
            if(allLiving)
            {
                // Prisoners' actual route testimony yields a modest,
                // persistent public travel benefit in this district.
                // The second shard is awarded exactly once on signing.
                CreditShards(1);
                RecordMoment(scoutPatrolZh[cleared]+
                    " 巡路人用绊索活捉全队，查明了较安全的回程地面。居民接驳永久再省3耐力。",
                    scoutPatrolEn[cleared]+
                    " Every raider was restrained alive. Their testimony revealed safer ground, permanently saving 3 more relay stamina.");
            }
            else
                RecordMoment(scoutPatrolZh[cleared]+
                    (mixed?" 部分巡兵被活捉，其余经真实战斗击退；居民登记了每名失踪者的去向。":
                      " 巡逻队经过真实交锋被击退，原调查地标收到安全回执。"),
                    scoutPatrolEn[cleared]+
                    (mixed?" Some raiders were restrained alive and others defeated. The signed report accounts for each one.":
                      " The patrol was defeated in actual combat; the signed safety report reached the original survey post."));
            revision++;sound="reward";
            Notify("巡路委托结算：硬币"+(26+cleared*2)+
                "、碎片"+(allLiving?2:1)+"、经验"+(65+cleared*5)+
                (allLiving?"。活捉证词：本区接驳额外省3耐力。":"。")+
                "已登记"+FieldPatrolWins+"/7地区。",
                "Patrol signed: "+(26+cleared*2)+" coins, "+
                (allLiving?2:1)+" shards, "+(65+cleared*5)+
                " XP."+(allLiving?" Living testimony: 3 extra relay stamina saved.":"")+
                " Verified districts: "+FieldPatrolWins+"/7.");
            if(FieldPatrolWins==7&&!Has("biome_patrol_seals_complete"))
            {
                Flag("biome_patrol_seals_complete");
                CreditSkillPoints(1);CreditShards(3);
                RecordMoment("七张安全回执拼成了真正走过、打通过的居民巡路图。",
                    "Seven signed reports now form a walkable, fought-for civilian patrol map.");
                Notify("七地区巡路全部完成：额外技能点1、碎片3。",
                    "All seven patrols secured: bonus talent point and 3 shards.");
            }
            return true;
        }
        private Enemy CreatePlacedEnemy(int id, int kind, Point proposed, float health, bool boss = false)
        {
            Point p = UncrowdedEnemyFoot(proposed,null,false,boss);
            return new Enemy(id, kind, p.x, p.y, health, boss);
        }
        // LOS alone is not a walkable route: a slit in a wall, tidewater
        // or a sealed gate can let an effect be visible while a summoned
        // melee enemy can NEVER reach the current fight. Use the SAME
        // four-neighbor footprint graph as real enemy pursuit, bounded to
        // the actual local summoning arena. Shared generation-stamped path
        // workspace avoids allocations on repeated Boss phases.
        private bool BossSummonHasLocalRoute(Point from, Point target)
        { return ActorHasLocalRoute(from,target,5); }
        // Keep the established two-argument boss interface (used by
        // existing Unity reflection checks) while allowing public ferry
        // encounters to check a wider pier path without partial routing.
        private bool ActorHasLocalRoute(Point from,Point target,int radius)
        {
            if(!CanStand(from)||!CanStand(target))return false;
            int width=Map.rows[0].Length,height=Map.rows.Length;
            int sx=(int)Math.Floor(from.x),sy=(int)Math.Floor(from.y);
            int tx=(int)Math.Floor(target.x),ty=(int)Math.Floor(target.y);
            if(sx<0||sy<0||tx<0||ty<0||
                sx>=width||tx>=width||sy>=height||ty>=height||
                Math.Abs(tx-sx)>radius||Math.Abs(ty-sy)>radius)return false;
            int start=sy*width+sx,goal=ty*width+tx;
            PreparePathSearch(width*height);
            int head=0,tail=0;
            pathQueue[tail++]=start;
            VisitPath(start,start);
            while(head<tail)
            {
                int node=pathQueue[head++],x=node%width,y=node/width;
                if(node==goal)return true;
                for(int i=0;i<4;i++)
                {
                    int nx=x+pursuitSteps[i,0],ny=y+pursuitSteps[i,1];
                    if(nx<0||ny<0||nx>=width||ny>=height||
                        Math.Abs(nx-sx)>radius||Math.Abs(ny-sy)>radius)continue;
                    int next=ny*width+nx;
                    if(PathVisited(next)||!Fits(new Point(nx+.5f,ny+.5f)))continue;
                    VisitPath(next,node);pathQueue[tail++]=next;
                }
            }
            return false;
        }
        // A living Boss's reinforcements MUST emerge in the current local
        // arena. CreatePlacedEnemy is deliberately broader (recovery of valid
        // old saves, authored actors) and can relocate an invalid summon to
        // another side of the map; that is not valid gameplay telegraphing.
        // Try the legacy near-left tile first, then nearby reachable ring tiles.
        private bool TrySpawnBossReinforcement(Enemy boss)
        {
            if (boss==null || !boss.boss || boss.hp<=0 || !enemies.Contains(boss)
                || enemies.Count>=9)return false;
            // The summon must reinforce a fight that physically connects
            // to the hero, not fill an isolated pocket on the other side
            // of a closed floodgate or a wall. The older local boss-to-spawn
            // check alone could greenlight an unreachable entire arena.
            // A FULL physical finish is required; a closest-reachable BFS
            // frontier does not prove an enemy can ever engage the hero.
            if(!CanStand(Position)||!CanStand(boss.Position))
                return false;
            // A nearby fighter is NOT proof of an open arena: two feet
            // 1.5 tiles apart can stand on opposite sides of a sealed wall.
            // Zero path steps are legitimate only for a genuine nearby
            // connected footprint, never for a thin-wall summon exploit.
            bool heroAccessible=(boss.Position-Position).Length<=1.8f?
                ActorHasLocalRoute(boss.Position,Position,5):
                NavigationPathFrom(boss.Position,Position,true).Count>0;
            if(!heroAccessible)return false;
            Point[] candidates = {
                new Point(-2f,1f), new Point(2f,1f), new Point(-2f,-1f),
                new Point(2f,-1f), new Point(0f,2.5f), new Point(0f,-2.5f),
                new Point(-3f,0f), new Point(3f,0f),
                new Point(-3f,2f), new Point(3f,2f),
                new Point(-3f,-2f), new Point(3f,-2f),
                new Point(-1f,3f), new Point(1f,3f),
                new Point(-1f,-3f), new Point(1f,-3f)
            };
            foreach(Point offset in candidates)
            {
                Point foot=boss.Position+offset;
                if (!CanStand(foot) || (foot-Position).Length<1.25f ||
                    (foot-boss.Position).Length<1.4f ||
                    (save.companion>=0 &&
                        (foot-companionPosition).Length<1.2f) ||
                    (ReliefEscortActive&&reliefMissionRoom==save.room&&
                        (foot-reliefFollowerPosition).Length<1.25f) ||
                    !ClearAttackPath(boss.Position,foot) ||
                    !BossSummonHasLocalRoute(boss.Position,foot))continue;
                bool occupied=false;
                foreach(Enemy existing in enemies)
                    if((foot-existing.Position).Length<
                        (existing.boss?1.4f:1.15f))
                    {occupied=true;break;}
                if(occupied)continue;
                // The first authorized third-cycle summon remains the old
                // melee troop. In later cycles, alternating *real* bow units
                // creates flank pressure instead of endlessly duplicating
                // identical close-range mobs. Citadel keeps its ranged guard.
                int recruitKind=save.room==4?1:
                    (boss.cycle>=6&&(boss.cycle/3)%2==0?1:0);
                Enemy recruit=new Enemy(++enemyId,recruitKind,
                    foot.x,foot.y,38+save.room*3);
                recruit.Face(Position-foot);
                // Give a genuine audible and visual entrance: summoning a
                // standing combatant inside a swing should not hit instantly.
                recruit.stun=.35f;
                enemies.Add(recruit);
                effects.Add(new Effect("burst",foot,.35f)
                    {radius=.65f,variant=0});
                sound="spell";revision++;
                return true;
            }
            // No legal nearby arena foot: skip only this summon, not the
            // Boss attack, normal fight or any existing opponent.
            return false;
        }
        private void TickTideTransition()
        {
            if (save.room != 5 || previousLowTide == LowTide) return;
            previousLowTide = LowTide;
            if (!Fits(Position))
            {
                Point p = SafeActorFoot(Position); save.x = p.x; save.y = p.y;
                CancelAction(); dashTime = 0; playerRecoil = new Point();
                // The escape to shore is a safety teleport, not traveled
                // footsteps. Restart the next physical walk from its first
                // coherent planted sprite pose, without adding/replaying
                // footstep events already counted earlier in the journey.
                walkDistance = 0f;
                // An involuntary shoreline relocation is not a player
                // dodge. A dash started on the outgoing shoal must not
                // claim a counter from a later warning after its actual
                // traveled movement was canceled by the tide.
                perfectDodgeArmed = false;
                dodgeEscapeTime = 0f;
                dashOrigin = Position;
                Notify("潮水涨起，你退回了最近的岸边。闸门可固定低潮。", "The tide rises; you retreat to the nearest bank. The sluice can hold low tide.");
            }
            RecoverTideActors();
        }
        private void RecoverTideActors()
        {
            foreach (Enemy enemy in enemies) if (!Fits(enemy.Position))
            {
                // The water canceled the attack itself. Retract its
                // attacker-identity warning at the same instant: otherwise
                // the old danger circle remains visible after the forced
                // retreat despite no strike ever being released.
                RemoveEnemyPendingWarning(enemy);
                // The water may strand several living enemies on the same
                // former shoal; evacuate each to a distinct connected dry
                // foot without counting its own invalid old position.
                Point p = UncrowdedEnemyFoot(enemy.Position,enemy);
                enemy.x = p.x; enemy.y = p.y;
                enemy.moving = enemy.backpedaling = false; enemy.state = 0; enemy.releaseTime = 0;
                enemy.targetingWayfarer = enemy.targetingReliefCivilian =
                    enemy.targetingAfterlightStock = enemy.targetingOuterPatrolStock = false;
                enemy.timer = Math.Max(.35f, enemy.timer);
            }
            if (save.companion >= 0 && !Fits(companionPosition))
            {
                // Tide-forced evacuation cancels a planted ally's
                // prepared action. If the player never received its
                // physical contact, do not charge the complete manual
                // command cooldown for a spell the world canceled.
                // A volley/heal/frost that ALREADY released remains paid.
                bool unspentCommand = companionAction != null &&
                    companionAction.kind == "command" &&
                    !companionAction.released;
                bool unspentAuto = companionAction != null &&
                    companionAction.kind != "command" &&
                    !companionAction.released;
                // Evacuate last, after enemies have claimed real dry feet.
                // Ignore only the ally's OWN submerged former position;
                // still avoid the hero and all live combatants.
                companionPosition = UncrowdedEnemyFoot(
                    companionPosition,null,true);
                companionAction = null; companionAutoTarget = null;
                companionMoving = false; companionMoveTime = 0f;
                // Forced surf relocation is not a step along the previous
                // path: stale queue/corner goals can otherwise walk the ally
                // straight back into the shoal on the following tick.
                // Preserve actual hold orders and the party identity.
                ClearCompanionFollowRoute();
                ClearCompanionCornerPlan();
                companionUnreachableGoal=false;
                companionBlockedRouteRetry=0f;
                if (unspentCommand) companionCooldown = 0f;
                if (unspentAuto)
                    companionAttack = Math.Min(companionAttack, .22f);
            }
            // The real evacuee is a world actor too. Without this, the
            // outgoing tide can trap her in newly flooded shoals while the
            // hero and enemies recover to the nearest walkable bank.
            if (ReliefEscortActive && !Fits(reliefFollowerPosition))
            {
                // Civilians also occupy real collision space. An evacuation
                // into the same dry-bank tile as a living enemy or an ally
                // would strand the resident inside an active combat body.
                // Reuse the physically connected, uncrowded relocation;
                // this is NOT walking, a reward or a teleport to the hub.
                reliefFollowerPosition =
                    UncrowdedEnemyFoot(reliefFollowerPosition);
                // This shoreline relocation is not a walked animation
                // frame. Drop old gait distance and movement state while
                // preserving the civilian's tactical WAIT/FOLLOW choice,
                // safety, mission time and saved rescue checkpoint format.
                reliefFollowerMoving = false;
                reliefFollowerWalkDistance = 0f;
                // The survivor's pre-flood route and stalled-route timer
                // are no longer anchored at the current physical feet.
                // Recompute next tick, but retain explicit WAIT/FOLLOW and
                // the exact protected mission clock and safety.
                ClearReliefFollowerRoute();
                reliefFollowerBlockedSeconds=0f;
                reliefFollowerBlockedNotice=false;
            }
        }
    }
}