using System;
using System.Collections.Generic;
namespace Starfall
{
    // Each existing far lookout can become an ACTUAL local civilian haven.
    // The authored object, current map, save, and exact fighter instances
    // own combat progress; only its final human choice enters v2 flags.
    public sealed partial class GameCore
    {
        private static readonly string[] watchZh={
            "余烬镇的南望台还留着护送孩童时的旧火把。有人又在山道设伏。",
            "密林鸟群突然飞散。搜刮草药的人正在破坏原来的归途。",
            "遗迹涨潮时，渡船的照明被人故意熄灭；守路人请求恢复瞭望。",
            "矿道的报时铃被拆，逃出来的工人再也看不到安全信号。",
            "堡垒墙外的撤离口被巡逻者误当成军事封锁线。",
            "雾港最后一盏导灯下，落难船员正在寻找能够避开劫匪的岸路。",
            "沙海的水井守夜人发现掠夺者借海市蜃楼隐藏脚步。",
            "高原的风雪将求援钟声吹散，伤者迟迟等不到带路的人。"};
        private static readonly string[] watchEn={
            "The Emberfall south watch still holds a torch left for evacuated children. Raiders block the road again.",
            "Birds scatter in the briars. Medicine gatherers have lost their safe way home.",
            "At high tide someone extinguishes the ruin ferry lights; wardens need a real lookout.",
            "The mine shift bell was stolen. Evacuating workers can no longer see the safety signal.",
            "Outside the citadel, patrols mistake a civilian escape route for a military cordon.",
            "At Mistport's last lamp, shipwrecked crews need a shore road free from raiders.",
            "Glasswind well keepers report raiders hiding their approach behind the mirage.",
            "The snowstorm scatters the rescue bell; injured travelers still need a guide."};
        private int vantageRoom=-1;
        private MapData vantageMap;
        private SaveData vantageSave;
        private WorldObject vantageSource;
        private readonly List<Enemy> vantageFoes=new List<Enemy>();
        private readonly HashSet<Enemy> vantageKills=new HashSet<Enemy>();
        private readonly HashSet<Enemy> vantageCaptures=new HashSet<Enemy>();
        public bool VantageWatchAid(int r)
        {return r>=0&&r<8&&Has("vantage_watch_aid_"+r);}
        public bool VantageWatchSignal(int r)
        {return r>=0&&r<8&&Has("vantage_watch_signal_"+r);}
        public bool VantageWatchBuilt(int r)
        {return VantageWatchAid(r)||VantageWatchSignal(r);}
        public int VantageWatchWins
        {get{int n=0;for(int i=0;i<8;i++)if(VantageWatchBuilt(i))n++;return n;}}
        public bool VantageWatchActive
        {get{return vantageRoom==save.room&&vantageRoom>=0&&vantageRoom<8&&
            Object.ReferenceEquals(vantageMap,Map)&&
            Object.ReferenceEquals(vantageSave,save)&&vantageSource!=null&&
            SourceBelongsToActiveMap(vantageSource);}}
        private bool IsVantageWatchEnemy(Enemy e)
        {return e!=null&&VantageWatchActive&&vantageFoes.Contains(e);}
        public bool IsLiveVantageWatchEnemy(Enemy e)
        {return IsVantageWatchEnemy(e)&&e.hp>0&&enemies.Contains(e);}
        private void OnVantageWatchEnemyDefeated(Enemy e)
        {if(IsVantageWatchEnemy(e))vantageKills.Add(e);}
        private bool OnVantageWatchEnemyRestrained(Enemy e)
        {
            if(!IsVantageWatchEnemy(e)||e.hp<=0||enemies.Contains(e))return false;
            vantageCaptures.Add(e);return true;
        }
        public int VantageWatchRemaining
        {get{int n=0;if(VantageWatchActive)foreach(Enemy e in vantageFoes)
            if(e!=null&&e.hp>0&&enemies.Contains(e))n++;return n;}}
        public bool VantageWatchReady
        {get{
            if(!VantageWatchActive||vantageFoes.Count<2)return false;
            foreach(Enemy e in vantageFoes)
            {
                if(e==null||enemies.Contains(e))return false;
                if(vantageKills.Contains(e)&&e.hp<=0)continue;
                if(vantageCaptures.Contains(e)&&e.hp>0)continue;
                return false;
            }
            return true;
        }}
        private void ResetVantageWatch()
        {
            projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                vantageFoes.Contains(p.hostileShooter));
            foreach(Enemy e in vantageFoes)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);regionalAmbientEnemies.Remove(e);
            }
            vantageRoom=-1;vantageMap=null;vantageSave=null;
            vantageSource=null;vantageFoes.Clear();
            vantageKills.Clear();vantageCaptures.Clear();
        }
        private bool SpawnVantageWatchWave(WorldObject source)
        {
            int count=save.room==2||save.room==3||save.room==6?3:2;
            Point start=Position;
            int sx=(int)Math.Floor(source.x),sy=(int)Math.Floor(source.y);
            var feet=new List<Point>();
            // Diverse angles around the actual original lookout, not one
            // row of enemies or an inaccessible island behind a tidegate.
            for(int radius=7;radius<=14&&feet.Count<count;radius++)
            for(int dy=-radius;dy<=radius&&feet.Count<count;dy++)
            for(int dx=-radius;dx<=radius&&feet.Count<count;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                    Math.Abs(dx)+Math.Abs(dy)>20)continue;
                Point foot=new Point(sx+dx+.5f,sy+dy+.5f);
                if(!CanStand(foot)||(foot-start).Length<5f)continue;
                bool clear=true;
                foreach(Point earlier in feet)
                    if((foot-earlier).Length<6f){clear=false;break;}
                if(!clear)continue;
                foreach(Enemy e in enemies)
                    if(e.hp>0&&(e.Position-foot).Length<3.5f)
                        {clear=false;break;}
                if(!clear)continue;
                foreach(WorldObject o in Map.objects)
                    if(o!=null&&(new Point(o.x,o.y)-foot).Length<3.2f)
                        {clear=false;break;}
                if(!clear||NavigationPathFrom(start,foot,true).Count==0)
                    continue;
                feet.Add(foot);
            }
            if(feet.Count!=count)return false;
            for(int i=0;i<count;i++)
            {
                int kind=(save.room+i*2+1)%3;
                Enemy e=CreatePlacedEnemy(++enemyId,kind,feet[i],
                    34f+save.room*9f);
                vantageFoes.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.65f)
                    {variant=kind});
            }
            return true;
        }
        private void TickVantageWatch(float dt)
        {
            if(vantageRoom>=0)
            {
                if(!VantageWatchActive||Dying||practicing)
                    {ResetVantageWatch();return;}
                foreach(Enemy e in vantageFoes)
                    if(e==null||
                       !vantageKills.Contains(e)&&!vantageCaptures.Contains(e)&&
                       (!enemies.Contains(e)||e.hp<=0))
                    {
                        ResetVantageWatch();revision++;
                        Notify("守望中有袭击者未经确认便消失。请回原观测站重组防线。",
                            "An unverified lookout raider vanished. Restart at the original station.");
                        return;
                    }
            }
            // Permanent physical field service, never a global healing cheat.
            if(mode!="world"||Dying||practicing||racing||
                FieldWorkPreventsRest||!SafeTravel||
                !NearKind("farlookout",3.2f))return;
            if(VantageWatchAid(save.room)&&save.health>0)
            {
                save.health=Math.Min(save.maxHealth,save.health+dt*1.25f);
                save.cold=Math.Max(0,save.cold-dt*1.5f);
                save.heat=Math.Max(0,save.heat-dt*1.5f);
            }
            if(VantageWatchSignal(save.room))
                save.energy=Math.Min(100,save.energy+dt*4f);
        }
        private void AppendVantageWatchChoice(WorldObject o)
        {
            int room=save.room;
            if(o==null||o.kind!="farlookout"||room<0||room>=8||
                !Has("far_vista_"+room)||conversation==null||
                mode!="dialog"||!SourceBelongsToActiveMap(o))return;
            var list=new List<Choice>(conversation.choices);
            int at=list.Count;
            if(at>0&&list[at-1].action=="close")at--;
            if(VantageWatchBuilt(room))
            {
                conversation.text+="\n\n"+watchZh[room]+"\n\n居民已经守住这处望台；你的决定仍在为旅人服务。";
                conversation.textEn+="\n\n"+watchEn[room]+"\n\nResidents still use the secured lookout. Your choice remains in effect.";
            }
            else if(VantageWatchActive)
            {
                conversation.text+="\n\n守路者："+
                    (VantageWatchReady?"前线已安全，回来选择医疗站或信号台。":
                        "真正的袭击者剩余 "+VantageWatchRemaining+" 名；先守住道路。");
                conversation.textEn+="\n\nWardens: "+
                    (VantageWatchReady?"The watch is secured. Choose a medical or signal service.":
                        VantageWatchRemaining+" genuine raiders remain. Hold the road.");
                if(VantageWatchReady)
                {
                    list.Insert(at++,C("建公共医疗望台 · 木1 零件1",
                        "Build aid lookout · 1 timber 1 part","vantage_aid"));
                    list.Insert(at++,C("建公共信号望台 · 木1 零件1",
                        "Build signal lookout · 1 timber 1 part","vantage_signal"));
                }
                list.Insert(at,C("撤回守望 · 无奖励","Withdraw watch · no reward","vantage_abort"));
            }
            else
            {
                conversation.text+="\n\n"+watchZh[room];
                conversation.textEn+="\n\n"+watchEn[room];
                list.Insert(at,C("守住远端观测站 · 真实战斗",
                    "Defend this lookout · live combat","vantage_begin"));
            }
            conversation.choices=list.ToArray();
        }
        private bool ChooseVantageWatch(string action)
        {
            if(action!="vantage_begin"&&action!="vantage_abort"&&
               action!="vantage_aid"&&action!="vantage_signal")return false;
            int room=save.room;
            WorldObject source=pendingDialogueSource;
            if(room<0||room>=8||Dying||mode!="world"||practicing||racing||
                source==null||source.kind!="farlookout"||
                source.id!="far_vista_"+room||
                pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source)||!CanStand(Position)||
                !Has("far_vista_"+room))return true;
            if(action=="vantage_abort")
            {
                if(VantageWatchActive&&Object.ReferenceEquals(vantageSource,source))
                {
                    ResetVantageWatch();revision++;sound="ui";
                    Notify("居民守望已撤回，未签收任何奖励。",
                        "Watch withdrawn; no unearned payout.");
                }
                return true;
            }
            if(action=="vantage_begin")
            {
                if(VantageWatchBuilt(room)||VantageWatchActive||
                    FieldWorkPreventsRest||!SafeTravel||
                    BlockTravelForProtectedFieldMission(true))
                {
                    Notify("附近有战斗或现场任务；先恢复安全再部署守望。",
                        "Clear nearby danger and field missions before starting the watch.");
                    return true;
                }
                vantageRoom=room;vantageMap=Map;vantageSave=save;
                vantageSource=source;
                if(!SpawnVantageWatchWave(source))
                {
                    ResetVantageWatch();
                    Notify("观测台附近没有足够的、步行可达的袭击者进路。",
                        "No complete walkable enemy approach exists near this lookout.");
                    return true;
                }
                revision++;sound="alert";
                Notify(watchZh[room]+" 击倒或用真正绊索活捉"+
                    vantageFoes.Count+"名袭击者，再回望台建设公共服务。",
                    watchEn[room]+" Defeat or genuinely restrain "+
                    vantageFoes.Count+" raiders, then return to build a public service.");
                return true;
            }
            if(!VantageWatchActive||
                !Object.ReferenceEquals(vantageSource,source)||
                !VantageWatchReady||!SafeTravel||VantageWatchBuilt(room))return true;
            if(save.inventory[0]<1||save.inventory[4]<1)
            {
                Notify("公共望台需要木材与机械零件各一份。",
                    "Public lookout needs one timber and one spare part.");return true;
            }
            bool aid=action=="vantage_aid";
            bool allLiving=vantageCaptures.Count==vantageFoes.Count&&
                vantageKills.Count==0;
            save.inventory[0]--;save.inventory[4]--;
            ResetVantageWatch();
            Flag((aid?"vantage_watch_aid_":"vantage_watch_signal_")+room);
            CreditCoins(20+room*3);CreditShards(allLiving?2:1);
            AddXp(52+room*5);
            RecordMoment(watchZh[room]+
                (aid?" 居民把望台改成了实地治疗与御寒的避护点。":
                    " 居民把望台改成了实地耐力补给信号站。")+
                (allLiving?" 全部袭击者被非致命活捉，留下了路线证词。":""),
                watchEn[room]+
                (aid?" Residents built a live medical and climate refuge.":
                    " Residents built a live stamina-restoring signal post.")+
                (allLiving?" All raiders were restrained alive.":""));
            if(VantageWatchWins==8&&!Has("vantage_watch_all"))
            {
                Flag("vantage_watch_all");CreditSkillPoints(1);
            }
            revision++;sound="reward";
            Notify(aid?"公共医疗望台建成！安全驻足可恢复生命并缓解冷热。":
                "公共信号望台建成！安全驻足可持续恢复耐力。",
                aid?"Lookout clinic built! Safe standing restores health and eases heat and cold.":
                    "Lookout signal built! Safe standing restores stamina.");
            return true;
        }
    }
}
