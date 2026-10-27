using System;
namespace Starfall
{
    // A real minecart escort on existing Ironreach ground. The escort and
    // ambushers are temporary: only permanent v2 string flags are saved.
    public sealed partial class GameCore
    {
        public int MineConvoyStage { get; private set; }
        public float MineConvoyTime { get; private set; }
        public float MineConvoyIntegrity { get; private set; }
        public Point MineCartPosition { get; private set; }
        private float mineConvoySteam;
        private bool mineConvoyBraced;
        private float mineConvoyBlockNoticeTime;
        // Active interception is based on physical standing positions, not
        // equipment or a saved perk. A hero must actually occupy the raider's
        // sabotage lane, with an unbroken path to both attacker and cart.
        private bool MineConvoyActivePressureLane(Enemy e)
        { return IsMineConvoyEnemy(e)&&e.hp>0&&
            (e.Position-MineCartPosition).Length<5.3f &&
            ClearAttackPath(e.Position,MineCartPosition); }
        // Blocking one real approach never grants invulnerability to
        // the whole carriage: the other attacker remains independent.
        public int MineConvoyThreatLanes
        {
            get
            {
                if(MineConvoyStage!=2||save.room!=3||Dying)return 0;
                int count=0;
                foreach(Enemy foe in enemies)
                    if(MineConvoyActivePressureLane(foe))count++;
                return count;
            }
        }
        public int MineConvoyProtectedLanes
        {
            get
            {
                if(MineConvoyStage!=2||save.room!=3||Dying)return 0;
                int count=0;
                foreach(Enemy foe in enemies)
                    if(MineConvoyActivePressureLane(foe)&&
                        BlocksMineConvoySabotage(foe))count++;
                return count;
            }
        }
        public bool MineConvoyBodyguarding
        {get{return MineConvoyProtectedLanes>0;}}
        // A missing enemy is not a player victory; only Hit's lethal callback
        // can record the two actually defeated switchyard attackers.
        private int mineConvoyConfirmedKills;
        private bool MineConvoyGuardsDefeated { get { return mineConvoyConfirmedKills==3; } }
        private void OnMineConvoyEnemyDefeated(Enemy enemy)
        {
            if(MineConvoyStage!=2||save.room!=3||!IsMineConvoyEnemy(enemy))return;
            int index=enemy.id-91300;
            if(index>=0&&index<2)mineConvoyConfirmedKills|=1<<index;
        }
        public bool MineConvoyActive { get { return MineConvoyStage>0; } }
        public bool MineConvoyComplete { get { return Has("mineconvoy_complete"); } }
        public bool IsMineConvoyEnemy(Enemy e)
        { return e!=null&&e.id>=91300&&e.id<91310; }
        private WorldObject MineCartPost(int stage)
        {
            string id=stage==1?"mineconvoy_1":stage==2?"mineconvoy_2":"mineconvoy_3";
            foreach(WorldObject o in data.maps[3].objects)
                if(o.id==id&&o.kind=="mineconvoy")return o;
            return null;
        }
        private void ResetMineConvoy()
        {
            enemies.RemoveAll(IsMineConvoyEnemy);
            projectiles.RemoveAll(p=>p.mineConvoyOwned);
            hazards.RemoveAll(h=>h.mineConvoyOwned);
            effects.RemoveAll(e=>e.mineConvoyOwned);
            MineConvoyStage=0;MineConvoyTime=MineConvoyIntegrity=0;
            MineCartPosition=new Point();mineConvoySteam=0;
            mineConvoyBraced=false;mineConvoyConfirmedKills=0;
            mineConvoyBlockNoticeTime=0;
        }
        // Keep the authored two-guard confrontation, but do not summon a
        // fighter inside the real cart, hero, companion, existing enemy or
        // an unreachable island. A crowded junction may choose nearby floor.
        private static readonly Point[] mineConvoyAmbushOffsets={
            new Point(0f,0f),new Point(0f,1.5f),
            new Point(-1.5f,0f),new Point(1.5f,0f),
            new Point(0f,-1.5f),new Point(-1.5f,1.5f),
            new Point(1.5f,1.5f),new Point(-1.5f,-1.5f),
            new Point(1.5f,-1.5f),new Point(0f,2.5f),
            new Point(0f,-2.5f),new Point(-2.5f,0f),
            new Point(2.5f,0f)
        };
        private bool TryMineConvoyAmbushFoot(Point origin,Point other,
            bool hasOther,out Point result)
        {
            foreach(Point offset in mineConvoyAmbushOffsets)
            {
                Point foot=origin+offset;
                if(!CanStand(foot) ||
                    (foot-Position).Length<1.25f ||
                    (foot-MineCartPosition).Length<1.25f ||
                    (save.companion>=0 &&
                     (foot-companionPosition).Length<1.25f) ||
                    (hasOther && (foot-other).Length<1.25f) ||
                    !ClearAttackPath(MineCartPosition,foot) ||
                    !BossSummonHasLocalRoute(foot,MineCartPosition))
                    continue;
                bool occupied=false;
                foreach(Enemy existing in enemies)
                    if(existing.hp>0&&(foot-existing.Position).Length<1.25f)
                    {occupied=true;break;}
                if(occupied)continue;
                result=foot;return true;
            }
            result=new Point();return false;
        }
        private bool SpawnMineConvoyAmbush()
        {
            Point first,second;
            // Validate the entire pair before changing mission kills or
            // inserting actors. If the local arena is genuinely full, the
            // cart stays at the junction and retries on the next live tick.
            if(!TryMineConvoyAmbushFoot(new Point(62.5f,40.5f),
                new Point(),false,out first) ||
               !TryMineConvoyAmbushFoot(new Point(65.5f,42.5f),
                first,true,out second))return false;
            Point[] points={first,second};
            for(int i=0;i<points.Length;i++)
            {
                Enemy guard=new Enemy(91300+i,i==0?0:2,points[i].x,points[i].y,
                    i==0?90:115);
                guard.timer=.75f+i*.25f;
                enemies.Add(guard);
            }
            mineConvoyConfirmedKills=0;
            return true;
        }
        // Standing between the active saboteur and the civilian car takes
        // real positioning, not a passive shield aura. An offset hero, a
        // sealed wall or an already-lost lane must never intercept.
        private bool BlocksMineConvoySabotage(Enemy guard)
        {
            if(guard==null||guard.hp<=0||Dying||
                !CanStand(Position)||!CanStand(MineCartPosition))return false;
            Point lane=MineCartPosition-guard.Position;
            float length=lane.Length;
            if(length<1.7f)return false;
            Point blocker=Position-guard.Position;
            float forward=(blocker.x*lane.x+blocker.y*lane.y)/length;
            float lateral=Math.Abs(blocker.x*lane.y-blocker.y*lane.x)/length;
            return forward>.55f&&forward<length-.4f&&lateral<.68f &&
                (Position-MineCartPosition).Length<2.4f &&
                ClearAttackPath(guard.Position,Position) &&
                ClearAttackPath(Position,MineCartPosition);
        }
        private static float ActiveMineConvoyPressureTime(Enemy guard,float dt)
        {
            // Enemy AI consumes hit/stun/release locks before the cart is
            // updated this same world frame. Reconstruct the locked slice
            // (including a stun expiring midway through this dt), so a
            // staggered guard never drains full cart HP while disabled.
            // A fresh hit applied later this frame stays locked completely.
            float locked=Math.Max(0f,Math.Max(guard.stun,
                Math.Max(guard.hitTime,guard.releaseTime))+dt);
            return Math.Max(0f,dt-Math.Min(dt,locked));
        }
        // Arrival and the subsequent signed paperwork are distinct real
        // actions. Once the cart physically enters the depot contact circle,
        // bank it immediately so another simulation tick (or its timeout)
        // is not required just to recognize a journey already completed.
        private void SecureMineConvoyDepot(WorldObject depot)
        {
            MineCartPosition=new Point(depot.x,depot.y);
            MineConvoyStage=4;
            hazards.RemoveAll(h=>h.mineConvoyOwned);
            effects.RemoveAll(e=>e.mineConvoyOwned);
            revision++;
            Notify("矿车已抵达安全矿棚。走到交接台，与矿工办理救援登记。",
                "Cart reached shelter. Walk to the dispatch desk and record the rescue.");
        }
        private void TickMineConvoy(float dt)
        {
            if(!MineConvoyActive)return;
            if(save.room!=3||Dying||practicing){ResetMineConvoy();revision++;return;}
            // Arrival is physically completed before the paperwork. Once
            // the actual cart reaches the secure depot, its workers must
            // not suddenly vanish because the hero needs a few seconds to
            // walk to the registry. Keep the no-travel field mission active:
            // rewards still require live interaction at the shelter desk.
            if(MineConvoyStage==4&&MineConvoyIntegrity>0)return;
            // A physical final step on the preceding world frame must not
            // lose the workers' rescue to the next frame's paperwork timer.
            // The depot is authored map geometry; proximity to the hero
            // without the real cart at that depot never counts.
            if(MineConvoyStage==3&&MineConvoyIntegrity>0)
            {
                WorldObject depot=MineCartPost(3);
                if(depot!=null&&
                    (MineCartPosition-new Point(depot.x,depot.y)).Length<=.16f&&
                    CanStand(MineCartPosition))
                {
                    SecureMineConvoyDepot(depot);
                    return;
                }
            }
            MineConvoyTime-=dt;
            if(MineConvoyTime<=0||MineConvoyIntegrity<=0)
            {
                ResetMineConvoy();revision++;
                Notify("矿车未能安全撤离。救援失效，可从工房外重新启程。",
                    "The cart could not evacuate. Restart the rescue at the original mine post.");
                return;
            }
            if(MineConvoyStage==2)
            {
                mineConvoyBlockNoticeTime=Math.Max(0,mineConvoyBlockNoticeTime-dt);
                for(int i=0;i<2;i++)
                    if((mineConvoyConfirmedKills&(1<<i))==0 &&
                        !enemies.Exists(e=>e.id==91300+i))
                    {
                        ResetMineConvoy();revision++;
                        Notify("袭击者异常离场，战斗未完成。矿车已返回发车岗，可免费重试。",
                            "An unconfirmed raider vanished; the cart returned to dispatch. Retry for free.");
                        return;
                    }
                foreach(Enemy guard in enemies)
                    // The HUD and physical damage must agree on which
                    // enemy has a real unobstructed cart pressure lane.
                    if(MineConvoyActivePressureLane(guard))
                    {
                        float pressure=ActiveMineConvoyPressureTime(guard,dt);
                        if(pressure<=0)continue;
                        if(BlocksMineConvoySabotage(guard))
                        {
                            // This cancels only the intercepted raider's
                            // damage. The hero still has to survive the
                            // ordinary enemy's real attacks and the other
                            // saboteur can still destroy an unguarded cart.
                            if(mineConvoyBlockNoticeTime<=0)
                            {
                                effects.Add(new Effect("number",Position,.55f,
                                    L("挡住破坏","CART GUARD"))
                                    {mineConvoyOwned=true});
                                mineConvoyBlockNoticeTime=.65f;
                                sound="guard";
                            }
                        }
                        else MineConvoyIntegrity-=pressure*3.3f;
                    }
                // Real combat is the necessary route to stage three;
                // no scripted button can despawn surviving miners' attackers.
                return;
            }
            if(MineConvoyStage!=1&&MineConvoyStage!=3)return;
            // A committed steam plume is dangerous to the actual cart body.
            // This resolves only our own expiring hazard; ordinary enemy
            // projectiles and other quest warnings are not changed.
            foreach(Hazard steam in hazards)
                if(steam.mineConvoyOwned&&steam.timer>0&&steam.timer<=dt&&
                    (steam.position-MineCartPosition).Length<=steam.radius+.4f)
                    MineConvoyIntegrity-=mineConvoyBraced?6:12;
            if(MineConvoyIntegrity<=0)return;
            mineConvoySteam-=dt;
            if(MineConvoyStage==3&&mineConvoySteam<=0)
            {
                mineConvoySteam=5.1f;
                hazards.Add(new Hazard{position=MineCartPosition,
                    kind="iron",radius=1.15f,timer=1.12f,damage=12,
                    mineConvoyOwned=true});
                effects.Add(new Effect("warn",MineCartPosition,1.12f)
                    {radius=1.15f,variant=4,mineConvoyOwned=true});
            }
            WorldObject target=MineCartPost(MineConvoyStage==1?2:3);
            if(target==null)return;
            Point destination=new Point(target.x,target.y);
            Point delta=destination-MineCartPosition;
            if(delta.Length<=.16f)
            {
                MineCartPosition=destination;
                if(MineConvoyStage==1)
                {
                    if(!SpawnMineConvoyAmbush())return;
                    MineConvoyStage=2;
                    Notify("矿车到达转轨台！先击退真正的袭击者，再决定是否消耗零件加固。",
                        "Cart at the switchyard! Defeat the real raiders before choosing a reinforcement.");
                }
                else
                {
                    SecureMineConvoyDepot(target);
                    return;
                }
                revision++;return;
            }
            // The hero must remain on foot beside this single physical cart.
            // No remote dispatch, camera hop, menu tick or instant teleport.
            if((Position-MineCartPosition).Length>3.15f||
                !SafeTravel)return;
            float step=Math.Min(delta.Length,dt*1.65f);
            Point moved=MineCartPosition+delta.Normalized*step;
            if(!CanStand(moved))return;
            MineCartPosition=moved;
            // A genuine last step may cross into the depot's .16-unit
            // arrival radius with only a few milliseconds left. Do not
            // demand an extra idle frame and risk an undeserved timeout.
            if(MineConvoyStage==3&&(destination-moved).Length<=.16f)
            {
                SecureMineConvoyDepot(target);
                return;
            }
            revision++;
        }
        // The rescue cart, live reinforcement purchase and final
        // non-repeatable reward are physical railway interactions.
        // Buttons opened before a wall closes may not skip real contact.
        private bool CanReachMineCartPost(WorldObject site)
        { return site!=null&&PhysicalRefugeReach(
            new Point(site.x,site.y),1.8001f); }
        private bool TalkMineConvoy(WorldObject o)
        {
            if(o.kind!="mineconvoy")return false;
            int stage=0;for(int i=1;i<=3;i++)if(o.id=="mineconvoy_"+i)stage=i;
            if(stage==0||save.room!=3||
                !CanReachMineCartPost(o))return true;
            if(!Has("boss_3"))
            {
                Dialog(o.name,o.en,
                    "矿道仍属于军用监工。先击败赤铁矿场原有的守护者，工人才有权决定如何撤离。",
                    "The overseer still controls the mine. Defeat Ironreach's original guardian before the workers can dispatch a civilian cart.",
                    C("记录位置","Mark location","close"));
                return true;
            }
            if(MineConvoyComplete)
            {
                Dialog(o.name,o.en,
                    "矿工最后一班撤离已成功。"+(Has("mineconvoy_braced")?
                        "你花零件加固了车厢，后半程蒸汽损伤减半；工人已经把这套保护结构记录下来。":
                        "你保留了备用零件，以驾驶和战斗技术将车厢护送至安全矿棚。")+
                        "\n\n安全矿棚已可供旅人休整，不能重复领取奖励。",
                    "The workers' last shift reached shelter."+(Has("mineconvoy_braced")?
                        " You spent a spare part to halve steam damage to the carriage.":
                        " You saved the spare parts and relied on escort tactics.")+
                        "\n\nThe depot now offers safe rest, never duplicate rewards.",
                    C("阅读工人留言","Read workers' note","close"));
                return true;
            }
            if(!MineConvoyActive)
            {
                if(stage!=1)
                {
                    Dialog(o.name,o.en,"矿车仍停在西北发车岗，请先从那里亲自出发。",
                        "The cart is still parked at the western dispatch. Start there in person.",
                        C("沿轨道返回","Return to dispatch","close"));
                    return true;
                }
                Dialog(o.name,o.en,
                    "旧军用运输单只计入矿石，没有计算工人的安全。护送这辆民用矿车从西北出发，经转轨台到南侧矿棚。保持在车旁，击退中途两名袭击者。可选择消耗1份零件加固车厢，也可保留材料。矿车生命80，任务限时110秒；途中蒸汽预警可以闪避。失败可免费重试。",
                    "Military transport orders counted ore but not the workers. Escort this civilian cart from western dispatch through the switchyard to the southern shelter. Stay near the cart and defeat two raiders. Spend one spare part to brace the cart, or conserve supplies. Hull 80, 110-second limit. Dodge the warned steam vents; retries are free.",
                    C("启动矿车护送","Begin cart escort","mineconvoy_start"),
                    C("暂缓行动","Not now","close"));
                return true;
            }
            if(stage==1)
            {
                Dialog(o.name,o.en,
                    "矿车正在移动。剩余"+MineConvoyTime.ToString("0")+"秒，车厢完整度"+MineConvoyIntegrity.ToString("0")+"/80。请沿原路跟紧矿车；也可以在这里主动结束行动。",
                    "Cart en route: "+MineConvoyTime.ToString("0")+"s and "+MineConvoyIntegrity.ToString("0")+"/80 hull. Stay near the cart or explicitly abort here.",
                    C("继续护送","Keep escorting","close"),
                    C("取消本次护送","Abort escort","mineconvoy_abort"));
                return true;
            }
            if(stage==2)
            {
                if(MineConvoyStage!=2)
                {
                    Notify("让矿车到达转轨台，再进行加固或放行。",
                        "Wait until the cart reaches the switchyard.");
                    return true;
                }
                if(enemies.Exists(IsMineConvoyEnemy)||!MineConvoyGuardsDefeated)
                {
                    Dialog(o.name,o.en,
                        "袭击者正试图破坏矿车！站在他们与车厢之间可以挡住那一路的破坏，但另一名劫匪仍会动手。亲自击败两人才能继续运送。",
                        "Saboteurs threaten the cart! Stand between each raider and the carriage to block that attack lane. The other raider can still damage the hull. Defeat both to dispatch.",
                        C("继续战斗","Defend cart","close"));return true;
                }
                Dialog(o.name,o.en,
                    "矿工已经重新握住转轨手柄。车厢完整度"+MineConvoyIntegrity.ToString("0")+"/80。消耗1份零件可修补28完整度，并让后半程蒸汽对车厢的伤害减半；或保留材料直接冒险出发。",
                    "The worker has secured the switch. Hull "+MineConvoyIntegrity.ToString("0")+"/80. Spend one part to restore 28 hull AND halve later steam damage to the cart, or keep your spare and take the risk.",
                    C("消耗零件修车 · +28","Repair with 1 part · +28","mineconvoy_brace"),
                    C("不消耗材料，直接放行","Conserve supplies · dispatch","mineconvoy_go"));
                return true;
            }
            if(stage==3&&MineConvoyStage==4)
            {
                Dialog(o.name,o.en,
                    "真实的矿车已经到站，车厢完整度"+MineConvoyIntegrity.ToString("0")+"/80。工人终于可以安全从矿坑撤离。",
                    "The physical cart is at the shelter with "+MineConvoyIntegrity.ToString("0")+"/80 hull. The shift can finally return home.",
                    C("登记撤离并领取奖励","Register civilian rescue","mineconvoy_finish"));return true;
            }
            Dialog(o.name,o.en,
                "矿车当前在"+MineConvoyStage+"/4阶段，剩余"+MineConvoyTime.ToString("0")+"秒。请回到车旁，继续护送。",
                "Cart phase "+MineConvoyStage+"/4; "+MineConvoyTime.ToString("0")+"s left. Return to the cart and escort it.",
                C("继续前进","Continue","close"));
            return true;
        }
        private bool ChooseMineConvoy(string action)
        {
            if(!action.StartsWith("mineconvoy_",StringComparison.Ordinal))return false;
            if(save.room!=3||Dying||practicing)return true;
            if(action=="mineconvoy_start")
            {
                WorldObject start=MineCartPost(1);
                if(start==null||MineConvoyActive||MineConvoyComplete||
                    !Has("boss_3")||FieldWorkPreventsRest||!SafeTravel||
                    !CanReachMineCartPost(start))return true;
                MineCartPosition=new Point(start.x,start.y);
                mineConvoyBraced=false;MineConvoyStage=1;MineConvoyTime=110;MineConvoyIntegrity=80;
                mineConvoySteam=5;
                Flag("mineconvoy_started");revision++;sound="quest";
                Notify("矿车出发！保持在矿车3格以内，靠近转轨台击退袭击者。",
                    "Cart dispatched! Keep within three tiles and defeat raiders at the switchyard.");
                return true;
            }
            if(action=="mineconvoy_abort")
            {
                WorldObject start=MineCartPost(1);
                if(start==null||!MineConvoyActive||
                    !CanReachMineCartPost(start))return true;
                ResetMineConvoy();revision++;
                Notify("矿车安全返回待命，任务可以免费重试。",
                    "Cart safely withdrawn. The rescue can be retried for free.");
                return true;
            }
            if(action=="mineconvoy_go"||action=="mineconvoy_brace")
            {
                WorldObject relay=MineCartPost(2);
                if(MineConvoyStage!=2||relay==null||
                    !CanReachMineCartPost(relay)||
                    enemies.Exists(IsMineConvoyEnemy)||!MineConvoyGuardsDefeated)return true;
                if(action=="mineconvoy_brace")
                {
                    if(save.inventory[4]<1)
                    {
                        Notify("需要1份零件才能加固矿车。可选择直接放行。",
                            "One spare part is required. You may proceed without repair.");
                        return true;
                    }
                    save.inventory[4]--;MineConvoyIntegrity=Math.Min(80,MineConvoyIntegrity+28);
                    mineConvoyBraced=true;
                }
                MineConvoyStage=3;mineConvoySteam=5.2f;
                revision++;sound="reward";
                Notify("转轨完成，护送矿车抵达南侧矿棚。注意持续涌出的蒸汽！",
                    "Track switch secured. Escort the cart south and dodge the recurring steam.");
                return true;
            }
            if(action=="mineconvoy_finish")
            {
                WorldObject end=MineCartPost(3);
                if(MineConvoyStage!=4||end==null||MineConvoyComplete||
                    !CanReachMineCartPost(end))return true;
                bool braceEarned=mineConvoyBraced;
                ResetMineConvoy();
                if(braceEarned)Flag("mineconvoy_braced");
                Flag("mineconvoy_complete");
                CreditCoins(60);CreditShards(2);AddXp(80);
                revision++;sound="reward";
                RecordMoment("赤铁矿场的最后一班工人搭乘真正穿过旧矿道的矿车抵达南侧安全矿棚。工人决定以后优先撤离人，而不是运送矿石。",
                    "Ironreach workers escaped in a physical railcart to southern shelter. Workers now evacuate people before ore.");
                Notify("矿工安全撤离！硬币+60、碎片+2、经验+80。矿棚可安全恢复生命，诺尔强化装备额外省1份木料。",
                    "Workers rescued! +60 coins, +2 shards, +80 XP. The safe depot heals, and Noll's upgrades cost one less timber.");
                return true;
            }
            return true;
        }
        public bool MineConvoyRefuge
        {
            get
            {
                if(!MineConvoyComplete||save.room!=3)return false;
                WorldObject shelter=MineCartPost(3);
                return shelter!=null&&
                    PhysicalRefugeReach(new Point(shelter.x,shelter.y),3.2f);
            }
        }
    }
}
