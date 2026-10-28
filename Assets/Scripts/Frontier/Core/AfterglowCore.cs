using System;
using System.Collections.Generic;
namespace Starfall
{
    // Post-chronicle two-wave combat, instanced from the real outdoor camp.
    // All actor ownership is by reference and remains transient to SaveData v2.
    public sealed partial class GameCore
    {
        private int afterglowRoom=-1,afterglowPhase,afterglowPlan;
        private float afterglowTime;
        private MapData afterglowMap;
        private SaveData afterglowSave;
        private WorldObject afterglowCamp;
        private readonly List<Enemy> afterglowActors=new List<Enemy>();
        private readonly HashSet<Enemy> afterglowKills=new HashSet<Enemy>();
        public bool AfterglowActive
        {get{return afterglowPhase>0&&afterglowRoom==save.room&&
            Object.ReferenceEquals(afterglowSave,save)&&
            Object.ReferenceEquals(afterglowMap,Map)&&
            afterglowCamp!=null&&SourceBelongsToActiveMap(afterglowCamp);}}
        public int AfterglowPhase {get{return AfterglowActive?afterglowPhase:0;}}
        public float AfterglowTime {get{return AfterglowActive?afterglowTime:0f;}}
        public Point AfterglowCampFoot {get{return AfterglowActive?
            new Point(afterglowCamp.x,afterglowCamp.y):new Point();}}
        public int AfterglowRemaining
        {get{int n=0;if(AfterglowActive)foreach(Enemy e in afterglowActors)
            if(enemies.Contains(e)&&!afterglowKills.Contains(e))n++;
            return n;}}
        public int AfterglowWins {get{int n=0;for(int room=0;room<8;room++)
            if(Has("afterglow_done_"+room))n++;return n;}}
        private bool IsAfterglowEnemy(Enemy e)
        {return e!=null&&afterglowPhase>0&&afterglowRoom==save.room&&
            afterglowActors.Contains(e);}
        private void OnAfterglowEnemyDefeated(Enemy e)
        {if(IsAfterglowEnemy(e))afterglowKills.Add(e);}
        private void ResetAfterglow()
        {
            if(afterglowPhase>0)
            {
                projectiles.RemoveAll(p=>p.afterglowOwned||
                    (p.hostileShooter!=null&&afterglowActors.Contains(p.hostileShooter)));
                effects.RemoveAll(e=>e.afterglowOwned);
                for(int i=enemies.Count-1;i>=0;i--)
                    if(afterglowActors.Contains(enemies[i]))
                    {RemoveEnemyPendingWarning(enemies[i]);enemies.RemoveAt(i);}
            }
            afterglowRoom=-1;afterglowPhase=afterglowPlan=0;
            afterglowTime=0f;afterglowMap=null;afterglowSave=null;afterglowCamp=null;
            afterglowActors.Clear();afterglowKills.Clear();
        }
        private bool SpawnAfterglow(int[] kinds,int wave)
        {
            Point center=new Point(afterglowCamp.x,afterglowCamp.y);
            Point[] offsets={
                new Point(-5,0),new Point(5,0),new Point(0,-5),
                new Point(0,5),new Point(-4,-4),new Point(4,-4),
                new Point(4,4),new Point(-4,4),new Point(-7,0),
                new Point(7,0),new Point(0,7),new Point(0,-7),
                new Point(-6,3),new Point(6,3)};
            var feet=new List<Point>();
            foreach(Point offset in offsets)
            {
                Point p=center+offset;
                if(!CanStand(p)||(p-Position).Length<1.7f||
                    (save.companion>=0&&(p-companionPosition).Length<1.7f)||
                    feet.Exists(q=>(q-p).Length<2.8f)||
                    enemies.Exists(e=>e!=null&&e.hp>0f&&
                        (e.Position-p).Length<1.8f)||
                    !ActorHasLocalRoute(p,center,14))continue;
                feet.Add(p);
                if(feet.Count==kinds.Length)break;
            }
            if(feet.Count!=kinds.Length)
            {
                Notify("营地附近没有足够的真实袭击通道。清理周边敌人后再试。",
                    "Insufficient walkable approach lanes. Clear nearby enemies first.");
                return false;
            }
            afterglowActors.Clear();afterglowKills.Clear();
            for(int i=0;i<kinds.Length;i++)
            {
                Enemy e=CreatePlacedEnemy(++enemyId,kinds[i],feet[i],
                    54f+save.room*7f+(wave==2?20f:0f));
                afterglowActors.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",feet[i],.6f)
                    {variant=kinds[i],afterglowOwned=true});
            }
            afterglowTime=wave==1?75f:95f;
            revision++;sound="quest";
            Notify(wave==1?"余烬守望：第一波夜袭开始！":
                "余烬守望：第二波战术交锋开始！",
                wave==1?"Afterglow watch: first real raid!":
                "Afterglow watch: tactical final wave!");
            return true;
        }
        private void TickAfterglow(float dt)
        {
            if(afterglowPhase==0)return;
            if(!AfterglowActive||Dying){ResetAfterglow();return;}
            if((Position-AfterglowCampFoot).Length>14f)
            {
                ResetAfterglow();revision++;
                Notify("已离开守火防区，守望失败。","You left the fire perimeter. Watch failed.");
                return;
            }
            if(afterglowPhase==2)return;
            foreach(Enemy e in afterglowActors)
                if(!enemies.Contains(e)&&!afterglowKills.Contains(e))
                {
                    ResetAfterglow();revision++;
                    Notify("袭击者未被实战击倒，不能获得守望胜利。",
                        "A raider vanished without a real kill. No victory awarded.");
                    return;
                }
            bool won=afterglowActors.Count>0;
            foreach(Enemy e in afterglowActors)
                if(!afterglowKills.Contains(e)||enemies.Contains(e))won=false;
            if(won)
            {
                if(afterglowPhase==1)
                {
                    afterglowPhase=2;afterglowTime=0f;
                    afterglowActors.Clear();afterglowKills.Clear();
                    revision++;sound="quest";
                    Notify("第一波已击退！回营地选择掩护居民或主动截击。",
                        "First wave defeated. Return to camp and choose a tactical response.");
                    return;
                }
                int room=afterglowRoom,plan=afterglowPlan;
                ResetAfterglow();
                if(Has("afterglow_done_"+room))return;
                Flag("afterglow_done_"+room);
                CreditCoins(plan==2?72:38);
                CreditShards(plan==2?2:1);AddXp(plan==2?95:60);
                RecordMoment(plan==2?
                    "第 "+(room+1)+" 地的守火人主动追击重甲袭击者。最后一盏灯始终留在身后，替晚归的人守住方向。":
                    "第 "+(room+1)+" 地的守火队先掩护居民撤退，再把夜袭者从避难通道赶走。共享的道路仍然开放。",
                    plan==2?
                    "Watch "+(room+1)+": wardens intercepted armored raiders while the last signal light held behind them.":
                    "Watch "+(room+1)+": wardens screened fleeing residents, clearing the raiders from the public shelter route.");
                if(AfterglowWins==8&&!Has("afterglow_all"))
                {
                    Flag("afterglow_all");CreditSkillPoints(1);
                    RecordMoment("八地火光都经过真正夜袭的考验。路不必被封锁才能得到保护。",
                        "All eight civilian fires survived genuine raids. Roads can be protected without closing them.");
                }
                revision++;sound="reward";
                Notify("余烬守望胜利！已经守护 "+AfterglowWins+"/8 处营火。",
                    "Afterglow cleared! "+AfterglowWins+"/8 fires defended.");
                return;
            }
            afterglowTime=Math.Max(0f,afterglowTime-dt);
            if(afterglowTime<=0f)
            {
                ResetAfterglow();revision++;
                Notify("守望超时，仍有真实敌人未被击退。","Watch timed out with live raiders still standing.");
            }
        }
        private bool ChooseAfterglow(string action)
        {
            if(action!="afterglow_begin"&&action!="afterglow_screen"&&
                action!="afterglow_cut"&&action!="afterglow_abort")return false;
            WorldObject camp=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||save.room<0||
                save.room>7||camp==null||camp.kind!="annexcamp"||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(camp)||
                !HasPhysicalInteractionReach(camp))return true;
            if(action=="afterglow_begin")
            {
                if(afterglowPhase!=0||!Has("annex_relay_archived")||
                    !Has("annex_relay_done_"+save.room)||
                    Has("afterglow_done_"+save.room))return true;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("还有现场任务或附近敌人。先清理威胁。",
                        "Clear nearby enemies and live fieldwork before starting watch.");
                    return true;
                }
                afterglowRoom=save.room;afterglowMap=Map;
                afterglowSave=save;afterglowCamp=camp;afterglowPhase=1;
                if(!SpawnAfterglow(new int[]{0,1},1))ResetAfterglow();
                return true;
            }
            if(!AfterglowActive||
                !Object.ReferenceEquals(camp,afterglowCamp))return true;
            if(action=="afterglow_abort")
            {
                ResetAfterglow();revision++;sound="ui";
                Notify("已中止守望，可从此营地重新开始。",
                    "Watch withdrawn. You can restart at this camp.");
                return true;
            }
            if(afterglowPhase!=2)return true;
            if(!SafeTravel)
            {
                Notify("周围敌人未清除，暂时无法开始第二波。",
                    "Clear nearby enemies before resuming the next wave.");
                return true;
            }
            int plan=action=="afterglow_cut"?2:1;
            if(!SpawnAfterglow(plan==2?new int[]{2,1,0}:
                new int[]{0,1},2))return true;
            afterglowPlan=plan;afterglowPhase=3;
            return true;
        }
    }
}
