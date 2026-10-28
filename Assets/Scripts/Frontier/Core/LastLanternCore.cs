using System;
namespace Starfall
{
    // The Last Lantern: an original three-site Citadel chapter with timed
    // carriage of a civilian distress signal, live ambushers and two
    // mechanically different permanent endings. SaveData v2 is unchanged.
    public sealed partial class GameCore
    {
        private int lastLanternStage;
        private float lastLanternSeconds;
        private Enemy lastLanternA,lastLanternB;
        private int lastLanternKills;
        public bool LastLanternActive {get{return lastLanternStage>0;}}
        public int LastLanternStage {get{return lastLanternStage;}}
        public float LastLanternSeconds {get{return LastLanternActive?lastLanternSeconds:0f;}}
        public bool LastLanternComplete {get{return Has("lastlantern_public")||Has("lastlantern_silent");}}
        public bool LastLanternPublic {get{return Has("lastlantern_public");}}
        public bool LastLanternSilent {get{return Has("lastlantern_silent");}}
        public float LastLanternPebbleCooldown
        {get{return LastLanternPublic&&save.room<8?3.5f:PebbleRecoverySeconds;}}
        public int LastLanternSnareGearCost
        {get{return LastLanternSilent&&save.room<8?0:1;}}
        public Point LastLanternObjective
        {get{return new Point(lastLanternStage==1?89.5f:lastLanternStage==2?89.5f:90.5f,
            lastLanternStage==1?32.5f:lastLanternStage==2?32.5f:40.5f);}}
        public string LastLanternObjectiveText
        {get{return lastLanternStage==1?L("旧求援灯 · 抢救消息","OLD RELAY · CARRY SIGNAL"):
            lastLanternStage==2?L("求援灯 · 击退拦截者","RELAY · DEFEAT INTERCEPTORS"):
            L("守望岗 · 决定归路","WATCHPOST · CHOOSE ROUTE");}}
        private void ResetLastLantern()
        {
            if(lastLanternA!=null)
            {
                RemoveEnemyPendingWarning(lastLanternA);
                enemies.Remove(lastLanternA);
            }
            if(lastLanternB!=null)
            {
                RemoveEnemyPendingWarning(lastLanternB);
                enemies.Remove(lastLanternB);
            }
            lastLanternA=lastLanternB=null;
            lastLanternStage=0;lastLanternSeconds=0;lastLanternKills=0;
        }
        public bool IsLastLanternEnemy(Enemy e)
        {return lastLanternStage==2&&save.room==4&&e!=null&&
            (Object.ReferenceEquals(e,lastLanternA)||
             Object.ReferenceEquals(e,lastLanternB));}
        private void OnLastLanternEnemyDefeated(Enemy e)
        {
            if(!IsLastLanternEnemy(e))return;
            if(Object.ReferenceEquals(e,lastLanternA))lastLanternKills|=1;
            if(Object.ReferenceEquals(e,lastLanternB))lastLanternKills|=2;
        }
        private bool TryLastLanternSpawnFoot(Point relay,Point other,out Point candidate)
        {
            Point[] offsets={new Point(-2f,0),new Point(2f,0),
                new Point(0,-2f),new Point(0,2f),
                new Point(-3f,-1f),new Point(3f,1f),
                new Point(-1f,3f),new Point(2f,-3f),
                new Point(-4f,0),new Point(4f,0),
                new Point(0,-4f),new Point(0,4f),
                new Point(-3f,3f),new Point(3f,-3f)};
            foreach(Point offset in offsets)
            {
                Point foot=relay+offset;
                if(!CanStand(foot)||!ClearAttackPath(relay,foot)||
                   !BossSummonHasLocalRoute(foot,relay)||
                   (foot-Position).Length<1.3f||
                   (foot-other).Length<1.5f||
                   save.companion>=0&&(foot-companionPosition).Length<1.3f)
                    continue;
                bool occupied=false;
                foreach(Enemy e in enemies)
                    if(e!=null&&e.hp>0&&(foot-e.Position).Length<1.4f)
                    {occupied=true;break;}
                if(occupied)continue;
                candidate=foot;return true;
            }
            candidate=new Point();return false;
        }
        private bool SpawnLastLanternInterceptors(Point relay)
        {
            Point first,second;
            if(!TryLastLanternSpawnFoot(relay,new Point(-100,-100),out first)||
               !TryLastLanternSpawnFoot(relay,first,out second))return false;
            lastLanternA=CreatePlacedEnemy(++enemyId,0,first,95,false);
            lastLanternB=CreatePlacedEnemy(++enemyId,2,second,120,false);
            lastLanternA.timer=.8f;lastLanternB.timer=1.2f;
            lastLanternKills=0;
            enemies.Add(lastLanternA);enemies.Add(lastLanternB);
            return true;
        }
        private void TickLastLantern(float dt)
        {
            if(lastLanternStage==0)return;
            if(save.room!=4||Dying||practicing)
            {ResetLastLantern();revision++;return;}
            lastLanternSeconds-=dt;
            if(lastLanternSeconds<=0f||
                lastLanternStage==2&&
                ((lastLanternKills&1)==0&&!enemies.Contains(lastLanternA)||
                 (lastLanternKills&2)==0&&!enemies.Contains(lastLanternB)))
            {
                ResetLastLantern();revision++;
                Notify("最后的灯讯失去了窗口；旧补给点仍保留消息，可重新送达。",
                    "The last signal window closed. Restart at the original cache.");
            }
        }
        private bool LastLanternSource(WorldObject o,string id)
        {
            return save.room==4&&o!=null&&o.id==id&&
                SourceBelongsToActiveMap(o)&&
                HasPhysicalInteractionReach(o);
        }
        private bool TalkLastLantern(WorldObject o)
        {
            if(!LastLanternActive||save.room!=4)return false;
            if(o.id=="annex_cache_4_a")
            {
                if(!LastLanternSource(o,o.id))return true;
                Dialog(o.name,o.en,
                    "旧信封已经离开补给箱。你可以原路中止，或者尽快将未登记的警报送过两座岗哨。",
                    "The letter has left the cache. Withdraw here, or carry the unnamed warning to both outposts.",
                    C("中止这次灯讯","Withdraw this run","lastlantern_abort"),
                    C("继续赶路","Continue","close"));
                return true;
            }
            if(o.id=="outer_4_1")
            {
                if(!LastLanternSource(o,o.id))return true;
                if(lastLanternStage==1)
                    Dialog(o.name,o.en,
                        "旧求援灯的黄铜罩里有一道未发送的讯号：外墙的居民先被登记为入侵者，救援请求才被压下。两名持军令的拦截者赶到了灯下。先保护讯号，打通真正的传递道路。",
                        "A suppressed call sits inside the old relay: outer-wall families were listed as intruders before their plea was buried. Two military interceptors arrive. Defend the signal before forwarding it.",
                        C("接通旧灯 · 迎战拦截者","Connect relay · face interceptors","lastlantern_fight"),
                        C("先观察地形","Survey the ground","close"));
                else if(lastLanternStage==2)
                    Dialog(o.name,o.en,lastLanternKills==3?
                        "两名拦截者已在实战中倒下。将完整的警报送往南侧守望岗，那里有人能决定公开还是隐匿路线。":
                        "拦截者仍在附近；必须真实击退两人，不能通过重新开关菜单领取战斗成果。",
                        lastLanternKills==3?
                        "Both interceptors fell in real combat. Forward the intact message to the southern watchkeeper, who can decide whether to publish or conceal its route.":
                        "The live interceptors remain. Defeat both for real; menus cannot invent victory.",
                        lastLanternKills==3?
                            C("将原始警报送往守望岗","Forward original distress call","lastlantern_forward"):
                            C("回到战斗","Return to combat","close"));
                else Dialog(o.name,o.en,
                    "旧灯已把讯号传到南侧守望岗；你还要亲自确认怎样让居民找到归路。",
                    "The relay forwarded the message. Confirm its civic use at the watchpost in person.",
                    C("继续送信","Carry onward","close"));
                return true;
            }
            if(o.id=="outer_watch_4")
            {
                if(!LastLanternSource(o,o.id))return true;
                if(lastLanternStage<3)
                    Dialog(o.name,o.en,
                        "守望员没有收到来自旧求援灯的真实回执。请先把讯号送过那处哨所。",
                        "The watchkeeper has no authentic relay receipt. Carry the warning through the lamp first.",
                        C("返回旧灯","Return to relay","close"));
                else Dialog(o.name,o.en,
                    "守望员认出了信封上的手印。这些人当夜穿过城墙，并不是要袭击堡垒；他们只想找一条能安全带孩子回家的路。\n\n若公开警报，所有旅人会更快收到投石回应，路上的守卫也更容易听见集结信号。若保护路标细节，诺尔的绊索工匠将用更简易的木楔在户外悄悄建立撤离路障。两种方案永久互斥。",
                    "The watchkeeper recognizes the handprints: these families were trying to get their children home, not storm the citadel.\n\nPublish the distress call and travelers can deploy decoy pebbles more often. Protect the hidden paths and outdoor snares no longer need metal gears. These are permanent, mutually exclusive choices.",
                    C("公开灯讯 · 户外投石冷却3.5秒",
                        "Publish call · outdoor pebble cooldown 3.5s","lastlantern_public"),
                    C("保护秘密通路 · 户外绊索免零件",
                        "Protect routes · outdoor snares cost no parts","lastlantern_silent"),
                    C("暂不决定","Decide later","close"));
                return true;
            }
            return false;
        }
        private bool ChooseLastLantern(string action)
        {
            if(!action.StartsWith("lastlantern_",StringComparison.Ordinal))return false;
            if(mode!="world"||Dying||practicing||save.room!=4)return true;
            WorldObject source=pendingDialogueSource;
            if(source==null||pendingDialogueSourceRoom!=4||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            if(action=="lastlantern_begin"||action=="lastlantern_abort")
            {
                if(source.id!="annex_cache_4_a")return true;
                if(action=="lastlantern_abort"&&LastLanternActive)
                {
                    ResetLastLantern();revision++;
                    Notify("已撤销本次灯讯。旧补给点还保存着原信。",
                        "Signal withdrawn. The original letter remains in the cache.");
                }
                else if(action=="lastlantern_begin"&&!LastLanternActive&&
                    !LastLanternComplete&&Has("boss_4")&&
                    !FieldWorkPreventsRest&&SafeTravel)
                {
                    lastLanternStage=1;lastLanternSeconds=170f;
                    revision++;sound="quest";
                    Notify("最后的灯讯：170秒内走到东侧旧求援灯，再护送原始讯号到守望岗。",
                        "Last Lantern: reach the eastern relay and deliver the original warning to the watchpost within 170 seconds.");
                }
                return true;
            }
            if(source.id=="outer_4_1")
            {
                if(action=="lastlantern_fight"&&lastLanternStage==1)
                {
                    Point relay=new Point(source.x,source.y);
                    if(!SpawnLastLanternInterceptors(relay))
                    {
                        Notify("求援灯附近过于拥挤。清出安全战场再启动信号。",
                            "The relay is crowded. Clear a real combat arena before connecting.");
                        return true;
                    }
                    lastLanternStage=2;revision++;sound="quest";
                    Notify("两名拦截者已出现在旧灯附近。用真实战斗击退他们！",
                        "Two interceptors now hold the relay. Defeat both in live combat!");
                }
                else if(action=="lastlantern_forward"&&
                    lastLanternStage==2&&lastLanternKills==3&&
                    !enemies.Exists(IsLastLanternEnemy))
                {
                    lastLanternStage=3;revision++;sound="quest";
                    Notify("讯号已通过旧灯。亲自前往南侧守望岗决定怎样保护居民。",
                        "The relay sent the warning. Visit the southern watchpost to choose its civic use.");
                }
                return true;
            }
            if(source.id=="outer_watch_4"&&lastLanternStage==3&&
                (action=="lastlantern_public"||action=="lastlantern_silent")&&
                !LastLanternComplete&&lastLanternSeconds>0f)
            {
                bool published=action=="lastlantern_public";
                ResetLastLantern();
                Flag(published?"lastlantern_public":"lastlantern_silent");
                CreditCoins(55);CreditShards(2);AddXp(95);
                RecordMoment(published?
                    "你护送被军令抹去的警报，击败真正的拦截者，并将手印和求救时刻公开。此后户外投石回应更迅速，归路有人守望。":
                    "你护送被军令抹去的警报，击败真正的拦截者，并把脆弱的逃生路标交还居民。此后户外绊索可用轻木楔替代齿轮。",
                    published?
                    "You carried the erased distress call through live interceptors and made its handprints public. Outdoor pebble decoys now recover faster.":
                    "You carried the erased warning through live interceptors and returned vulnerable routes to their families. Outdoor snares need no spare gears.");
                revision++;sound="reward";
                Notify(published?"最后的灯讯：公开警报！户外投石冷却3.5秒。":
                    "最后的灯讯：保护路线！户外绊索仅消耗木材。",
                    published?"The Last Lantern: public signal. Outdoor pebble cooldown 3.5s.":
                    "The Last Lantern: hidden routes. Outdoor snares cost only timber.");
                return true;
            }
            return true;
        }
    }
}
