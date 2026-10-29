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
        private bool RegionalBattleFootFree(Point foot,bool[] connected)
        {
            int x=(int)Math.Floor(foot.x),y=(int)Math.Floor(foot.y);
            int width=Map.rows[0].Length;
            if(x<4||x>33||y<3||y>21||
                x>=width||y>=Map.rows.Length||
                !connected[y*width+x]||!Fits(foot)||
                (foot-Position).Length<4.5f)return false;
            // Leave conversations, resources, exits and quest machinery
            // readable rather than turning their interaction pads into
            // unavoidable body-blocking piles.
            foreach(WorldObject obj in Map.objects)
                if(obj!=null&&(foot-new Point(obj.x,obj.y)).Length<2f)
                    return false;
            foreach(Enemy other in enemies)
                if(other!=null&&other.hp>0&&
                    (foot-other.Position).Length<2.5f)return false;
            return true;
        }
        private Point RegionalBattleFoot(int rank,bool[] connected)
        {
            int room=save.room;
            int ax=regionalBattleAnchors[room-1,rank,0],
                ay=regionalBattleAnchors[room-1,rank,1];
            for(int radius=0;radius<=6;radius++)
            for(int dy=-radius;dy<=radius;dy++)
            for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point p=new Point(ax+dx+.5f,ay+dy+.5f);
                if(RegionalBattleFootFree(p,connected))return p;
            }
            // A future modded map can remove an entire opening lane. Keep
            // the legacy repair behavior rather than dropping mandatory
            // enemy count or mutating the campaign/save format.
            int[,] original={
                {11,12},{15,17},{21,15},{24,6},
                {9,7},{30,16},{23,19},{14,9}
            };
            return new Point(original[rank,0],original[rank,1]);
        }
        private void SpawnRegionalBaselineEnemies(int count,float health)
        {
            bool[] connected=RegionalBattleReachableTiles();
            for(int i=0;i<count;i++)
                enemies.Add(CreatePlacedEnemy(++enemyId,i%3,
                    RegionalBattleFoot(i,connected),health));
        }

        // Only the baseline authored overworld roster sleeps when the
        // player is in a DIFFERENT local encounter district. Special
        // escorts, patrols, Bosses, alert sound searches and event raiders
        // are never tagged. Exact actor identity is session-only.
        private readonly HashSet<Enemy> regionalAmbientEnemies=
            new HashSet<Enemy>();
        private bool RegionalAmbientEncounterDormant(Enemy e)
        {
            return e!=null&&!e.boss&&regionalAmbientEnemies.Contains(e)&&
                save.room>=1&&save.room<=7&&
                (e.Position-Position).Length>24f&&
                e.state==0&&e.hitTime<=0f&&e.stun<=0f&&
                e.releaseTime<=0f&&
                !IsEnemyInvestigatingImpact(e)&&
                !IsEnemyReturningFromImpact(e);
        }
        // Distant wilderness patrols are OPTIONAL local challenges invoked
        // only by the actual field-survey transaction. They must not move
        // the old campaign's mandatory room roster out of reach, nor turn
        // a distant core-Boss gate into a whole-map hunting chore.
        private int SpawnRegionalScoutEncounter(WorldObject site,bool requireFull=false)
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
                bool found=false;
                for(int radius=7;radius<=12&&!found;radius++)
                for(int shift=0;shift<8&&!found;shift++)
                {
                    int direction=(room*2+i*3+shift)%8;
                    float dx=directions[direction,0],dy=directions[direction,1];
                    Point p=new Point((float)Math.Floor(source.x)+.5f+dx*radius,
                        (float)Math.Floor(source.y)+.5f+dy*radius);
                    if(!CanStand(p)||(requireFull?
                        !ActorHasLocalRoute(Position,p,16):
                        !ClearAttackPath(source,p))||
                        (p-Position).Length<5f)continue;
                    bool obstructed=false;
                    foreach(WorldObject o in Map.objects)
                        if((new Point(o.x,o.y)-p).Length<4.5f)
                        {obstructed=true;break;}
                    foreach(Enemy e in enemies)
                        if(e!=null&&e.hp>0&&(e.Position-p).Length<5f)
                        {obstructed=true;break;}
                    if(obstructed||pads.Exists(q=>(q-p).Length<5f))
                        continue;
                    pads.Add(p);found=true;
                }
            }
            // Paid-off patrol contracts require a FULL wave. Check all feet
            // before touching enemies or actor IDs; no partial free reward.
            if(requireFull&&pads.Count!=count)return 0;
            foreach(Point p in pads)
            {
                int slot=pads.IndexOf(p);
                Enemy patrol=CreatePlacedEnemy(++enemyId,
                    (room+slot*2)%3,p,30+room*10);
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
        public bool FieldPatrolActive
        {get{return fieldPatrolRoom>=1&&fieldPatrolRoom<=7&&
            fieldPatrolRoom==save.room&&
            Object.ReferenceEquals(fieldPatrolMap,Map)&&
            Object.ReferenceEquals(fieldPatrolSave,save)&&
            fieldPatrolOrigin!=null&&
            SourceBelongsToActiveMap(fieldPatrolOrigin);}}
        public bool FieldPatrolDone(int room)
        {return room>=1&&room<=7&&Has("biome_patrol_done_"+room);}
        public int FieldPatrolWins
        {get{int count=0;for(int i=1;i<=7;i++)
            if(FieldPatrolDone(i))count++;return count;}}
        public int FieldPatrolRemaining
        {get{int count=0;if(FieldPatrolActive)
            foreach(Enemy e in fieldPatrolEnemies)
                if(!fieldPatrolKills.Contains(e)&&
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
                if(!fieldPatrolKills.Contains(e)||e.hp>0f)return false;
            return true;}}
        private bool IsFieldPatrolEnemy(Enemy e)
        {return e!=null&&fieldPatrolEnemies.Contains(e);}
        private void OnFieldPatrolEnemyDefeated(Enemy e)
        {
            if(FieldPatrolActive&&fieldPatrolEnemies.Contains(e))
                fieldPatrolKills.Add(e);
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
            fieldPatrolRoom=-1;fieldPatrolMap=null;
            fieldPatrolSave=null;fieldPatrolOrigin=null;
            fieldPatrolEnemies.Clear();fieldPatrolKills.Clear();
        }
        private Choice FieldPatrolChoice(WorldObject site)
        {
            int room=save.room;
            if(room<1||room>7||site.kind!="biomeSurvey")
                return C("返回原调查地标领取巡逻委托",
                    "Visit the original survey post for patrol work","close");
            if(FieldPatrolDone(room))
                return C("本地巡逻安全回执已登记",
                    "Patrol safety receipt filed","close");
            if(FieldPatrolActive)
                return FieldPatrolReportReady?
                    C("提交真正击退巡兵的现场回执",
                      "File verified raider defeat receipt",
                      "biome_patrol_report"):
                    C("巡兵仍在现场 · 尚余"+FieldPatrolRemaining+"名",
                      "Raiders remain · "+FieldPatrolRemaining+" active",
                      "close");
            return C("巡路实战 · 消除本区盗匪威胁",
                "Field patrol · clear the genuine raider threat",
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
                journalFocusRequest="field_patrols";revision++;
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
            if(FieldPatrolDone(room))return true;
            if(action=="biome_patrol_begin")
            {
                if(FieldPatrolActive||FieldWorkPreventsRest||!SafeTravel||
                   racing||BlockTravelForProtectedFieldMission(true))
                {
                    Notify("先完成正在进行的战斗和护送，再接巡路委托。",
                        "Finish active combat and protected field missions before starting a patrol.");
                    return true;
                }
                int before=enemies.Count;
                int count=SpawnRegionalScoutEncounter(site,true);
                int expected=room==2||room==3||room==6?3:2;
                if(count!=expected)
                {
                    Notify("附近还没有完整可步行的巡兵进路。清理现场后再接委托。",
                        "No complete walkable raider approach remains. Clear the site and retry.");
                    return true;
                }
                fieldPatrolRoom=room;fieldPatrolMap=Map;
                fieldPatrolSave=save;fieldPatrolOrigin=site;
                fieldPatrolEnemies.Clear();fieldPatrolKills.Clear();
                for(int i=before;i<enemies.Count;i++)
                {
                    Enemy enemy=enemies[i];
                    fieldPatrolEnemies.Add(enemy);
                    effects.Add(new Effect("enemyRelease",enemy.Position,.6f)
                        {variant=enemy.kind});
                }
                revision++;sound="quest";
                Notify(scoutPatrolZh[room]+
                    " 击退"+count+"名真正的巡兵后，回这里交还现场回执。",
                    scoutPatrolEn[room]+
                    " Defeat "+count+" real raiders, then return to this post with the receipt.");
                return true;
            }
            if(!FieldPatrolActive||
               !Object.ReferenceEquals(fieldPatrolOrigin,site))return true;
            if(!FieldPatrolReportReady||!SafeTravel)
            {
                Notify("只有全部巡兵经真实战斗击倒且周围安全，才能交回委托。",
                    "Defeat every assigned raider in real combat and clear nearby danger before reporting.");
                return true;
            }
            int cleared=room;
            ResetFieldPatrol();
            if(FieldPatrolDone(cleared))return true;
            Flag("biome_patrol_done_"+cleared);
            CreditCoins(26+cleared*2);CreditShards(1);AddXp(65+cleared*5);
            RecordMoment(scoutPatrolZh[cleared]+
                " 巡逻队经过真实交锋被击退，原调查地标收到安全回执。",
                scoutPatrolEn[cleared]+
                " The patrol was defeated in actual combat; the signed safety report reached the original survey post.");
            revision++;sound="reward";
            Notify("巡路委托结算：硬币"+(26+cleared*2)+
                "、碎片1、经验"+(65+cleared*5)+"。已登记"+FieldPatrolWins+"/7地区。",
                "Patrol signed: "+(26+cleared*2)+" coins, 1 shard, "+
                (65+cleared*5)+" XP. Verified districts: "+FieldPatrolWins+"/7.");
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
