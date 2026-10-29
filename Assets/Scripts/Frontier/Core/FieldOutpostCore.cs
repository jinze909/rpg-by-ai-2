using System;
using System.Collections.Generic;
namespace Starfall
{
    // Player-built forward camps are real, optional follow-up content at
    // the seven recovered field kits. Live raiders exist only in this
    // Map/Save; finished infrastructure is persisted with ordinary v2 flags.
    public sealed partial class GameCore
    {
        private int outpostRoom=-1;
        private MapData outpostMap;
        private SaveData outpostSave;
        private WorldObject outpostOrigin;
        private readonly List<Enemy> outpostFoes=new List<Enemy>();
        private readonly HashSet<Enemy> outpostKills=new HashSet<Enemy>();
        private readonly HashSet<Enemy> outpostCaptures=new HashSet<Enemy>();
        public bool FieldOutpostActive
        {get{return outpostRoom>=1&&outpostRoom<=7&&outpostRoom==save.room&&
            Object.ReferenceEquals(outpostMap,Map)&&
            Object.ReferenceEquals(outpostSave,save)&&
            outpostOrigin!=null&&SourceBelongsToActiveMap(outpostOrigin);}}
        public bool FieldOutpostWard(int room)
        {return room>=1&&room<=7&&Has("field_outpost_ward_"+room);}
        public bool FieldOutpostForge(int room)
        {return room>=1&&room<=7&&Has("field_outpost_forge_"+room);}
        public bool FieldOutpostBuilt(int room)
        {return FieldOutpostWard(room)||FieldOutpostForge(room);}
        public int FieldOutpostWins
        {get{int n=0;for(int r=1;r<=7;r++)if(FieldOutpostBuilt(r))n++;return n;}}
        private bool IsFieldOutpostEnemy(Enemy e)
        {return e!=null&&FieldOutpostActive&&outpostFoes.Contains(e);}
        private void OnFieldOutpostEnemyDefeated(Enemy e)
        {if(IsFieldOutpostEnemy(e))outpostKills.Add(e);}
        private bool OnFieldOutpostEnemyRestrained(Enemy e)
        {
            if(!IsFieldOutpostEnemy(e)||e.hp<=0f||enemies.Contains(e))
                return false;
            outpostCaptures.Add(e);return true;
        }
        public int FieldOutpostRemaining
        {get{int n=0;if(FieldOutpostActive)foreach(Enemy e in outpostFoes)
            if(e!=null&&e.hp>0f&&enemies.Contains(e))n++;return n;}}
        // The current objective is always the nearest genuine live
        // attacker. After securing the road, it returns to the exact original
        // kit for the paid, persistent construction choice.
        public Point FieldOutpostGuideTarget
        {get{
            if(!FieldOutpostActive)return Position;
            Point best=new Point(outpostOrigin.x,outpostOrigin.y);
            float distance=float.MaxValue;
            foreach(Enemy e in outpostFoes)
                if(e!=null&&e.hp>0f&&enemies.Contains(e))
                {
                    float d=(e.Position-Position).Length;
                    if(d<distance){best=e.Position;distance=d;}
                }
            return best;
        }}
        public bool FieldOutpostReady
        {get{
            if(!FieldOutpostActive||outpostFoes.Count<2)return false;
            foreach(Enemy e in outpostFoes)
            {
                if(e==null||enemies.Contains(e))return false;
                if(outpostKills.Contains(e)&&e.hp<=0f)continue;
                if(outpostCaptures.Contains(e)&&e.hp>0f)continue;
                return false;
            }
            return true;
        }}
        private void ResetFieldOutpost()
        {
            projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                outpostFoes.Contains(p.hostileShooter));
            foreach(Enemy e in outpostFoes)
            {
                RemoveEnemyPendingWarning(e);
                regionalAmbientEnemies.Remove(e);
                enemies.Remove(e);
            }
            outpostRoom=-1;outpostMap=null;outpostSave=null;
            outpostOrigin=null;outpostFoes.Clear();
            outpostKills.Clear();outpostCaptures.Clear();
        }
        private void TickFieldOutpost()
        {
            if(outpostRoom<0)return;
            if(!FieldOutpostActive||Dying){ResetFieldOutpost();return;}
            foreach(Enemy e in outpostFoes)
                if(e==null||!outpostKills.Contains(e)&&
                    !outpostCaptures.Contains(e)&&
                    (!enemies.Contains(e)||e.hp<=0f))
                {
                    ResetFieldOutpost();revision++;
                    Notify("袭击者意外失联，无法登记前线安全。请重新组织防守。",
                        "An unverified raider vanished. Restart the real outpost defense.");
                    return;
                }
        }
        private bool SpawnFieldOutpostRaiders(WorldObject source)
        {
            int count=save.room==2||save.room==3||save.room==6?3:2;
            Point center=new Point(source.x,source.y);
            var feet=new List<Point>();
            int x0=(int)Math.Floor(center.x),y0=(int)Math.Floor(center.y);
            for(int radius=7;radius<=14&&feet.Count<count;radius++)
            for(int dy=-radius;dy<=radius&&feet.Count<count;dy++)
            for(int dx=-radius;dx<=radius&&feet.Count<count;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                    Math.Abs(dx)+Math.Abs(dy)>17)continue;
                Point foot=new Point(x0+dx+.5f,y0+dy+.5f);
                // Use exactly the physical patrol clearance and collision
                // graph; never create a paid wave through a closed tidegate.
                if(RegionalScoutFootClear(foot,center,true,feet))
                    feet.Add(foot);
            }
            if(feet.Count!=count)return false;
            for(int i=0;i<count;i++)
            {
                int kind=(save.room+i*2)%3;
                Enemy foe=CreatePlacedEnemy(++enemyId,kind,feet[i],
                    38f+save.room*8f);
                outpostFoes.Add(foe);enemies.Add(foe);
                effects.Add(new Effect("enemyRelease",foe.Position,.65f)
                    {variant=kind});
            }
            return true;
        }
        private bool ChooseFieldOutpost(string action)
        {
            if(action!="field_outpost_begin"&&action!="field_outpost_abort"&&
               action!="field_outpost_ward"&&action!="field_outpost_forge"&&
               action!="field_outpost_rest"&&action!="field_outpost_stock"&&
               action!="field_outpost_return")
                return false;
            int room=save.room;
            WorldObject source=pendingDialogueSource;
            if(room<1||room>7||mode!="world"||Dying||practicing||racing||
               source==null||source.kind!="fieldKit"||
               source.id!="fieldKit"+room||pendingDialogueSourceRoom!=room||
               !SourceBelongsToActiveMap(source)||
               !HasPhysicalInteractionReach(source)||!CanStand(Position)||
               !FieldKitRecovered(room))return true;
            if(action=="field_outpost_abort")
            {
                if(FieldOutpostActive&&Object.ReferenceEquals(outpostOrigin,source))
                {
                    ResetFieldOutpost();revision++;sound="ui";
                    Notify("前线防守已经撤回，没有结算建设奖励。",
                        "Outpost withdrawn. No unearned reward or infrastructure.");
                }
                return true;
            }
            if(action=="field_outpost_begin")
            {
                if(FieldOutpostActive||FieldOutpostBuilt(room)||
                   FieldWorkPreventsRest||!SafeTravel||
                   BlockTravelForProtectedFieldMission(true))
                {
                    Notify("清理附近的战斗与护送后，才能组织前线防守。",
                        "Clear combat and protected fieldwork before defending the outpost.");
                    return true;
                }
                outpostRoom=room;outpostMap=Map;
                outpostSave=save;outpostOrigin=source;
                if(!SpawnFieldOutpostRaiders(source))
                {
                    ResetFieldOutpost();
                    Notify("这里没有完整、可步行的敌人进路；先清理路障。",
                        "No complete walkable approach exists for a real ambush.");
                    return true;
                }
                revision++;sound="alert";
                Notify("袭击者逼近旧工具点！击退或活捉全部"+
                    outpostFoes.Count+"名敌人，再返回现场决定据点用途。",
                    "Raiders approach the kit! Defeat or restrain "+
                    outpostFoes.Count+" enemies and return to build.");
                return true;
            }
            if(action=="field_outpost_ward"||action=="field_outpost_forge")
            {
                if(!FieldOutpostReady||!Object.ReferenceEquals(outpostOrigin,source)||
                    FieldOutpostBuilt(room)||!SafeTravel)return true;
                if(save.inventory[0]<1||save.inventory[4]<1)
                {
                    Notify("修建据点需要木材与机械零件各1份。",
                        "Building requires 1 timber and 1 spare part.");
                    return true;
                }
                bool ward=action=="field_outpost_ward";
                save.inventory[0]--;save.inventory[4]--;
                ResetFieldOutpost();
                Flag((ward?"field_outpost_ward_":"field_outpost_forge_")+room);
                CreditCoins(20+room*3);CreditShards(1);AddXp(65+room*6);
                RecordMoment(fieldKitStoryZh[room]+
                    (ward?" 真正守住这里后，居民把器材改成了开放的前线医疗站。":
                          " 真正守住这里后，居民把旧器材改成了绊索物资工坊。"),
                    fieldKitStoryEn[room]+
                    (ward?" After real combat, the kit became a public forward aid station.":
                          " After real combat, the kit became a working trap-supply bench."));
                revision++;sound="reward";
                Notify(ward?"前线医疗站已建立，可消耗月光草进行补给。":
                            "绊索工坊已建立，可在原地购买零件和木材。",
                    ward?"Forward aid opened: Moonherbs now restore health and stamina here.":
                         "Field workshop opened: buy real snare materials at this bench.");
                return true;
            }
            if(!FieldOutpostBuilt(room)||FieldOutpostActive||
                FieldWorkPreventsRest||!SafeTravel||
                BlockTravelForProtectedFieldMission(true))return true;
            if(action=="field_outpost_return")
            {
                const int fare=4,energy=8;
                if(save.coins<fare||save.energy<energy)
                {
                    Notify("返程接驳需要4枚硬币和8点耐力。",
                        "Road courier requires 4 coins and 8 stamina.");
                    return true;
                }
                // This is a physical route, not an unrestricted teleport.
                // The ferry's full terrain/tide/gate solver must reach the
                // actual region entry pad in the current live world.
                Point depart=Position;
                Point entry=new Point(Map.spawnX,Map.spawnY);
                Point[] pads={
                    new Point(0,0),new Point(1,0),new Point(0,1),
                    new Point(-1,0),new Point(0,-1),
                    new Point(1,1),new Point(-1,-1)
                };
                Point? arrival=null;
                foreach(Point offset in pads)
                {
                    Point goal=entry+offset;
                    if(!CanStand(goal)||!CivicShuttleRoadSafe(depart,goal))
                        continue;
                    save.x=goal.x;save.y=goal.y;
                    bool safe=SafeTravel;
                    save.x=depart.x;save.y=depart.y;
                    if(safe){arrival=goal;break;}
                }
                if(!arrival.HasValue)
                {
                    Notify("当前潮水、封门或战斗切断了返程道路。未出发，也没有扣费。",
                        "The return footpath is blocked by terrain or combat. No charge.");
                    return true;
                }
                save.x=arrival.Value.x;save.y=arrival.Value.y;
                if(save.companion>=0&&!CompanionHoldingPosition&&
                   (Position-depart).Length>3f)
                {
                    Point follow=CompanionFollowTarget();
                    if(CanStand(follow))
                    {
                        ClearCompanionFollowRoute();
                        ClearCompanionCornerPlan();
                        companionAutoTarget=null;
                        companionPosition=follow;
                        companionMoveTime=0f;companionMoving=false;
                        companionPresentationGeneration++;
                    }
                }
                save.coins-=fare;save.energy-=energy;
                revision++;sound="travel";
                Notify("沿已守住的居民道路返回本区入口：硬币-4，耐力-8。",
                    "Returned to the regional entrance by a verified road. -4 coins, -8 stamina.");
                return true;
            }
            if(action=="field_outpost_rest"&&FieldOutpostWard(room))
            {
                if(save.inventory[2]<1||
                   save.health>=save.maxHealth&&save.energy>=99.5f&&
                   save.heat<=0f&&save.cold<=0f)
                {
                    Notify("需要月光草1份，且确实存在伤势、疲劳或冷热危险。",
                        "Aid requires 1 Moonherb and actual harm, fatigue or exposure.");
                    return true;
                }
                save.inventory[2]--;
                save.health=Math.Min(save.maxHealth,save.health+55f);
                save.energy=Math.Min(100f,save.energy+40f);
                save.heat=Math.Max(0f,save.heat-25f);
                save.cold=Math.Max(0f,save.cold-25f);
                revision++;sound="heal";
                Notify("前线急救：药草-1，生命+55，耐力+40，冷热危险减轻。",
                    "Field aid: -1 herb, +55 HP, +40 stamina and reduced exposure.");
                return true;
            }
            if(action=="field_outpost_stock"&&FieldOutpostForge(room))
            {
                if(save.coins<12)return true;
                if(!HasInventoryRoom(0,1)||!HasInventoryRoom(4,1))return true;
                save.coins-=12;save.inventory[0]++;save.inventory[4]++;
                revision++;sound="reward";
                Notify("绊索工坊：硬币-12，木材+1，机械零件+1。",
                    "Trap stock: -12 coins, +1 timber and +1 spare part.");
            }
            return true;
        }
    }
}
