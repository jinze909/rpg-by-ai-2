using System;
using System.Collections.Generic;

namespace Starfall
{
    // The Unsent Warning: a cross-region, genuinely walked courier chapter
    // joining the Drowned Ruins ferry and Ironreach miners. Both original
    // waytrail pairs remain usable for their older survey quests.
    // Persistent progress consists ONLY of existing SaveData v2 string flags.
    public sealed partial class GameCore
    {
        private int warningRunRoom=-1;
        private float warningTime,warningIntegrity,warningMeters;
        // One mutually exclusive field decision per active crossing:
        // 0 untouched, 1 waterproof repair, 2 tactical smoke decoy.
        // These are transient choices until an actual east-waymark handoff.
        private int warningRelayChoice;
        private bool warningRelayPursuerSpawned;
        public bool UnsentWarningRelayPursuerActive
        {get{return UnsentWarningRunActive&&warningRelayPursuerSpawned;}}
        public int UnsentWarningRelayChoice
        {get{return UnsentWarningRunActive?warningRelayChoice:0;}}
        public bool UnsentWarningRelayAvailable
        {get{return UnsentWarningRunActive&&warningRelayChoice==0;}}
        public Point UnsentWarningRelayFoot
        {get{
            WorldObject relay=WarningRelay(save.room);
            return relay==null?new Point():new Point(relay.x,relay.y);
        }}
        private Point warningLastFoot;
        private MapData warningRunMap;
        private SaveData warningRunSave;
        private readonly List<Enemy> warningRaiders=new List<Enemy>();

        public bool UnsentWarningUnlocked
        {get{return FloodArchiveComplete&&MineConvoyComplete&&
            CanEnter(2)&&CanEnter(3);}}
        public bool UnsentWarningStarted {get{return Has("unsent_warning_started");}}
        public bool UnsentRiverDelivered {get{return Has("unsent_warning_river");}}
        public bool UnsentMineDelivered {get{return Has("unsent_warning_mine");}}
        public bool UnsentWarningPublic {get{return Has("unsent_warning_public");}}
        public bool UnsentWarningHidden {get{return Has("unsent_warning_hidden");}}
        public bool UnsentWarningComplete
        {get{return UnsentWarningPublic||UnsentWarningHidden;}}
        // Optional double-fieldcraft challenge: only hand-delivered
        // receipts can form a durable two-relay mastery.
        public int UnsentWarningRelayCount
        {get{return (Has("unsent_warning_relay_2")?1:0)+
            (Has("unsent_warning_relay_3")?1:0);}}
        public bool UnsentWarningRunActive
        {get{return (warningRunRoom==2||warningRunRoom==3)&&
            save.room==warningRunRoom&&
            System.Object.ReferenceEquals(warningRunMap,Map)&&
            System.Object.ReferenceEquals(warningRunSave,save)&&!Dying;}}
        public float UnsentWarningSeconds
        {get{return UnsentWarningRunActive?warningTime:0f;}}
        public float UnsentWarningIntegrity
        {get{return UnsentWarningRunActive?warningIntegrity:0f;}}
        public float UnsentWarningTravel
        {get{return UnsentWarningRunActive?warningMeters:0f;}}
        private static bool WarningRegion(int room)
        {return room==2||room==3;}
        private WorldObject WarningPost(int room,bool east)
        {
            if(!WarningRegion(room))return null;
            foreach(WorldObject o in data.maps[room].objects)
                if(o.kind=="waytrail"&&
                   o.id=="waytrail"+room+(east?"e":"s"))return o;
            return null;
        }
        private WorldObject WarningRelay(int room)
        {
            if(!WarningRegion(room))return null;
            foreach(WorldObject o in data.maps[room].objects)
                if(o.kind=="unsentrelay"&&o.id=="unsent_relay_"+room)
                    return o;
            return null;
        }
        private bool WarningReach(WorldObject o)
        {return o!=null&&SourceBelongsToActiveMap(o)&&
            HasPhysicalInteractionReach(o)&&CanStand(Position);}
        public Point UnsentWarningObjective
        {
            get
            {
                if(UnsentWarningRunActive)
                {
                    WorldObject destination=WarningPost(warningRunRoom,true);
                    if(destination!=null)return new Point(destination.x,destination.y);
                }
                int room=!UnsentWarningStarted?0:
                    !UnsentRiverDelivered?2:!UnsentMineDelivered?3:0;
                if(room==0)
                {
                    WorldObject guild=ObjectPointWarningGuild();
                    return guild==null?new Point(18,17):
                        new Point(guild.x,guild.y);
                }
                WorldObject post=WarningPost(room,false);
                return post==null?new Point(14.5f,31.5f):
                    new Point(post.x,post.y);
            }
        }
        private WorldObject ObjectPointWarningGuild()
        {
            foreach(WorldObject o in data.maps[0].objects)
                if(o.kind=="guild")return o;
            return null;
        }
        public int UnsentWarningTargetRoom
        {get{return UnsentWarningRunActive?warningRunRoom:
            !UnsentWarningStarted?0:!UnsentRiverDelivered?2:
            !UnsentMineDelivered?3:0;}}
        public string UnsentWarningObjectiveText
        {get{return UnsentWarningRunActive?
            L("护住证词，赶往东侧旧路标","PROTECT THE WARNING · REACH EAST WAYMARK"):
            !UnsentWarningStarted?L("在余烬镇公会领取未送达的警报",
                "ACCEPT THE UNSENT WARNING AT EMBERFALL GUILD"):
            !UnsentRiverDelivered?L("前往沉没遗迹西路标，护送警报副本",
                "CARRY THE RIVER WARNING FROM DROWNED RUINS"):
            !UnsentMineDelivered?L("前往赤铁矿场西路标，接续矿工警报",
                "CARRY THE MINE WARNING FROM IRONREACH"):
            L("回余烬镇公会决定证词如何公开",
                "RETURN TO GUILD · DECIDE THE WITNESSES' FATE");}}
        public int UnsentWarningStepCount
        {get{return (!UnsentWarningStarted?0:1)+
             (!UnsentRiverDelivered?0:1)+(!UnsentMineDelivered?0:1)+
             (!UnsentWarningComplete?0:1);}}

        private void ResetUnsentWarning()
        {
            foreach(Enemy e in warningRaiders)
                if(e!=null)
                {
                    RemoveEnemyPendingWarning(e);
                    enemies.Remove(e);
                }
            warningRaiders.Clear();
            warningRunRoom=-1;warningTime=warningIntegrity=warningMeters=0f;
            warningRelayChoice=0;warningRelayPursuerSpawned=false;
            warningLastFoot=new Point();warningRunMap=null;warningRunSave=null;
        }
        private bool FindWarningRaiderFoot(Point origin,List<Point> taken,
            out Point location)
        {
            // These are genuine authored road locations in two original
            // 120x80 regions; try a bounded set of walkable approach lanes.
            Point[] candidates={
                new Point(29.5f,30.5f),new Point(34.5f,28.5f),
                new Point(37.5f,25.5f),new Point(31.5f,24.5f),
                new Point(42.5f,29.5f),new Point(39.5f,21.5f),
                new Point(24.5f,32.5f),new Point(43.5f,33.5f)};
            foreach(Point p in candidates)
            {
                if(!CanStand(p)||(p-origin).Length<5f||
                   (save.companion>=0&&(p-companionPosition).Length<1.8f)||
                   enemies.Exists(e=>e!=null&&e.hp>0f&&
                       (e.Position-p).Length<1.8f))continue;
                bool overlaps=false;
                foreach(Point previous in taken)
                    if((previous-p).Length<3f){overlaps=true;break;}
                if(overlaps)continue;
                location=p;return true;
            }
            location=new Point();return false;
        }
        private bool BeginWarningRun(int region)
        {
            if(!WarningRegion(region)||!UnsentWarningStarted||
               UnsentWarningComplete||!UnsentWarningUnlocked||
               region==2&&UnsentRiverDelivered||
               region==3&&(!UnsentRiverDelivered||UnsentMineDelivered)||
               UnsentWarningRunActive||warningRunRoom>=0||
               mode!="world"||Dying||practicing||racing||
               !SafeTravel||FieldWorkPreventsRest||
               save.inventory[2]<1||save.energy<10f)return false;
            var feet=new List<Point>();
            Point next;
            if(!FindWarningRaiderFoot(Position,feet,out next))return false;
            feet.Add(next);
            if(!FindWarningRaiderFoot(Position,feet,out next))return false;
            feet.Add(next);
            // No expense or timer is committed until two genuine enemy
            // lanes were found. Both actors remain normal killable enemies.
            save.inventory[2]--;save.energy-=10f;
            warningRunRoom=region;warningRunMap=Map;warningRunSave=save;
            warningTime=105f;warningIntegrity=100f;warningMeters=0f;
            warningRelayChoice=0;warningRelayPursuerSpawned=false;
            warningLastFoot=Position;
            for(int i=0;i<2;i++)
            {
                Enemy e=CreatePlacedEnemy(++enemyId,i,
                    feet[i],54f+(region==3?28f:12f));
                warningRaiders.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",feet[i],.6f)
                    {radius=.8f,variant=0});
            }
            sound="quest";revision++;
            RecordMoment(region==2?
                "沉桥边的守路人把两份遇难者警报藏进干草药油布：有人抢走了第一封，但第二封必须越过东侧旧路标。":
                "矿场的最后一记撤离警铃从未发给城里。幸存者将复制的警报交给你，路上的拦截者已经听见风声。",
                region==2?
                "At the drowned ferry, the keeper wraps two erased warnings in herb oilcloth. One was intercepted. The other must reach the eastern waymark.":
                "Ironreach's final evacuation bell never reached town. The survivors entrust its copy to you; road raiders have already heard.");
            Notify(region==2?"警报副本已封好！限时105秒送至沉桥东侧路标。":
                "矿场证词已封好！限时105秒送至东侧路标。",
                "Warning sealed. Reach the eastern waymark within 105 seconds.");
            return true;
        }
        private void DamageUnsentWarning(float damage,string source)
        {
            if(!UnsentWarningRunActive||source=="environment"||
               !Finite(damage)||damage<=0f)return;
            warningIntegrity=Math.Max(0f,
                warningIntegrity-Math.Max(12f,Math.Min(42f,damage*.85f)));
            if(warningIntegrity<=0f)
            {
                ResetUnsentWarning();revision++;
                Notify("警报信封被实战击毁。回西侧路标重新包封，已完成的递送不会丢失。",
                    "Hostile contact destroyed the warning. Restart at the west waymark; completed deliveries remain.");
            }
            else
                Notify("警报受损！护住剩余纸页，继续赶往东侧路标。",
                    "Warning damaged by a real hit! Keep the remaining pages intact.");
        }
        // A second, genuinely physical encounter begins only after the
        // courier approaches the actual mid-route relay ON FOOT. A teleport
        // to its tile with zero credited travel cannot summon the pursuer.
        // All fighters are transient, exact actor references. Reset clears
        // only this mission's living/dead enemies and warnings.
        private void TrySpawnWarningRelayPursuer()
        {
            if(!UnsentWarningRunActive||warningRelayPursuerSpawned||
               warningMeters<14f||warningTime<7f)return;
            WorldObject relay=WarningRelay(save.room);
            if(relay==null||!CanStand(Position)||
               (Position-new Point(relay.x,relay.y)).Length>=7f)return;
            Point[] positions={
                new Point(41.5f,25.5f),new Point(37.5f,26.5f),
                new Point(39.5f,24.5f),new Point(42.5f,26.5f)};
            foreach(Point p in positions)
            {
                if(!CanStand(p)||(p-Position).Length<2.2f||
                   (save.companion>=0&&(p-companionPosition).Length<1.7f)||
                   enemies.Exists(e=>e!=null&&e.hp>0f&&
                       (e.Position-p).Length<1.65f))continue;
                Enemy hunter=CreatePlacedEnemy(++enemyId,
                    save.room==3?2:1,p,90f+(save.room==3?20f:0f));
                warningRaiders.Add(hunter);enemies.Add(hunter);
                warningRelayPursuerSpawned=true;
                effects.Add(new Effect("enemyRelease",p,.55f)
                    {radius=1.1f,variant=0});
                sound="alert";revision++;
                Notify("追兵从真实路口截住警报！中继站可选一次加固或烟雾干扰。",
                    "A pursuer has blocked the actual road! The relay offers one repair or smoke tactic.");
                break;
            }
        }
        private void TickUnsentWarning(float dt)
        {
            if(warningRunRoom<0)
            {
                // Permanent civic service from an earned, branch-specific
                // cross-regional resolution; never heals active battles.
                if(!UnsentWarningComplete||!WarningRegion(save.room)||
                   !SafeTravel||FieldWorkPreventsRest||Dying)return;
                WorldObject west=WarningPost(save.room,false),
                    east=WarningPost(save.room,true);
                bool near=(west!=null&&PhysicalRefugeReach(
                    new Point(west.x,west.y),1.8f))||
                    (east!=null&&PhysicalRefugeReach(
                    new Point(east.x,east.y),1.8f));
                if(!near)return;
                if(UnsentWarningPublic)
                    save.health=Math.Min(save.maxHealth,save.health+1.7f*dt);
                else if(UnsentWarningHidden)
                    save.energy=Math.Min(100f,save.energy+3f*dt);
                return;
            }
            if(!UnsentWarningRunActive)
            {ResetUnsentWarning();return;}
            warningTime-=dt;
            Point current=Position,step=current-warningLastFoot;
            // Only bounded real-world steps earn courier distance: moving
            // save coordinates directly from one marker to the other can
            // never count as the required witnessed overland traversal.
            if(CanStand(current)&&CanStand(warningLastFoot)&&
               Finite(step.x)&&Finite(step.y)&&
               step.Length>.001f&&step.Length<=.46f)
                warningMeters+=step.Length;
            warningLastFoot=current;
            TrySpawnWarningRelayPursuer();
            if(warningTime<=0f)
            {
                ResetUnsentWarning();revision++;
                Notify("警报已过递送时限。回西侧路标重新出发。",
                    "Warning delivery timed out. Restart from the west waymark.");
            }
        }
        // Physical mid-route roadside handoff: two completely different
        // risk-management tactics, one per run. No dialogue, pause menu,
        // disconnected terrain or old-source interaction can activate it.
        private bool TalkUnsentWarningRelay(WorldObject source)
        {
            if(source==null||source.kind!="unsentrelay")return false;
            if(!WarningRegion(save.room)||WarningRelay(save.room)!=source||
                !WarningReach(source))return true;
            string riverZh=save.room==2?
                "沉桥救援员留下的油布和旧信号筒。":
                "矿工把最后一卷避难警报藏在废轨边。";
            string riverEn=save.room==2?
                "The bridge wardens left oilcloth and an old signal tube.":
                "Miners hid a final emergency signal beside the abandoned rails.";
            if(!UnsentWarningRunActive)
            {
                Dialog(source.name,source.en,
                    riverZh+"\n\n只有护送密封警报经过此地，才能使用一次接应。已完成的旧路标与地图调查不会改变。",
                    riverEn+"\n\nThis relay is usable once only while carrying a sealed warning. Original surveys remain untouched.",
                    C("继续赶路","Continue onward","close"));
                return true;
            }
            if(warningRelayChoice!=0)
            {
                Dialog(source.name,source.en,
                    warningRelayChoice==1?
                    "信封已经用油布加固，剩余时间仍在减少。不要在这里再浪费物资。":
                    "烟雾掩护已经发出，敌人正在重新组织；现在只能继续赶路。",
                    warningRelayChoice==1?
                    "The testimony is waterproofed. Time is still passing; save your supplies for the road.":
                    "The smoke diversion has fired. The raiders are regrouping; move while they can still be outrun.",
                    C("继续赶路","Continue","close"));
                return true;
            }
            Dialog(source.name,source.en,
                riverZh+"\n\n你只能在本次递送中选择一种方案。先用油布封住裂口会耗去8秒，但能修复信封；点燃诱饵烟筒会耗去5秒，并打乱附近真实敌人的行动。",
                riverEn+"\n\nChoose only one tactic for this crossing: repair the damaged seal at a cost of 8 seconds, or launch a smoke diversion for 5 seconds against genuine nearby raiders.",
                C("重新加固警报 · 草药1、耐力12、8秒 · 信封+45",
                  "Waterproof testimony · 1 herb, 12 energy, 8s · +45 seal",
                  "unsent_relay_repair"),
                C("释放诱敌烟雾 · 木材1、耐力12、5秒 · 干扰敌人",
                  "Smoke diversion · 1 timber, 12 energy, 5s · disrupt raiders",
                  "unsent_relay_smoke"),
                C("暂时不用","Save supplies","close"));
            return true;
        }
        private bool ChooseUnsentWarningRelay(string action)
        {
            if(action!="unsent_relay_repair"&&
               action!="unsent_relay_smoke")return false;
            WorldObject site=pendingDialogueSource;
            if(!UnsentWarningRelayAvailable||site==null||
               warningRunRoom!=save.room||mode!="world"||Dying||
               site!=WarningRelay(save.room)||!WarningReach(site)||
               save.energy<12f)return true;
            if(action=="unsent_relay_repair")
            {
                // An intact packet has nothing to patch. This is a resource
                // spend only after a truly damaged live seal was inspected.
                if(warningIntegrity>=100f||warningTime<=8f||
                   save.inventory[2]<1)return true;
                save.inventory[2]--;save.energy-=12f;
                warningIntegrity=Math.Min(100f,warningIntegrity+45f);
                warningTime-=8f;warningRelayChoice=1;
                sound="quest";revision++;
                Notify("原路标中继：信封修复45点，递送时间-8秒。只能选择一次。",
                    "Road relay: +45 seal, -8 seconds. One field tactic per run.");
                return true;
            }
            if(warningTime<=5f||save.inventory[0]<1)return true;
            // Only already-spawned living actors on the SAME, reachable
            // side of the actual site can be disrupted. No off-map stun,
            // Boss control, unused decoy refund or fabricated kill credit.
            Point origin=new Point(site.x,site.y);
            var vulnerable=new List<Enemy>();
            foreach(Enemy enemy in warningRaiders)
                if(enemy!=null&&enemy.hp>0f&&!enemy.boss&&
                   enemies.Contains(enemy)&&CanStand(enemy.Position)&&
                   (enemy.Position-origin).Length<9f&&
                   ClearAttackPath(origin,enemy.Position))
                    vulnerable.Add(enemy);
            if(vulnerable.Count==0)return true;
            save.inventory[0]--;save.energy-=12f;
            warningTime-=5f;warningRelayChoice=2;
            foreach(Enemy enemy in vulnerable)
            {
                ApplyEnemyControl(enemy,2.5f);
                enemy.timer=Math.Max(enemy.timer,2.5f);
                effects.Add(new Effect("burst",enemy.Position,.4f)
                    {radius=.65f,variant=1});
            }
            effects.Add(new Effect("burst",origin,.7f)
                {radius=1.2f,variant=1});
            sound="counter";revision++;
            Notify("烟筒引开 "+vulnerable.Count+" 名真实袭击者，夺得2.5秒脱离时间。限时-5秒。",
                "Smoke interrupted "+vulnerable.Count+
                " live raiders for 2.5s. The warning clock lost 5s.");
            return true;
        }
        private string UnsentWarningInnNews(bool english)
        {
            if(UnsentWarningPublic)return english?
                "\n\nTwo soaked warnings are pinned to the inn wall. The ferryman has written the children's names below the river marks; seven miners sign beside them in their own hands. 'Not lost cargo,' the traveler says. 'People who rang the bell and were left waiting.' Both old road markers now offer first aid to strangers.":
                "\n\n旅店墙上钉着两封浸湿的警报。摆渡人在水位线下补上孩子们的名字，七位矿工亲手签在旁边。守夜旅人说：‘不是丢失的货物，是敲过警钟却被留在路上的人。’两地旧路标如今都为陌生伤者备有药箱。";
            if(UnsentWarningHidden)return english?
                "\n\nThe innkeeper leaves a blank space where the witnesses' names might have been. A ferryman and a miner quietly mark two safe road crossings on a menu's back. 'Let them tell their own names when they're ready,' the traveler says. The old markers keep supplies for those walking after dark.":
                "\n\n店主在公告墙上留出一块空白，没有写下证人的名字。摆渡人和矿工却悄悄把两条安全路线画在菜单背面。守夜旅人说：‘等他们愿意自己说出来，再让人听见。’两地旧路标为夜归人留着补给。";
            return "";
        }
        private string UnsentWarningWaytrailNews(bool english)
        {
            if(UnsentWarningPublic)return english?
                "\n\nThe public warning now staffs first aid at both original waymarks. Stand here safely to recover +1.7 HP/s; combat and solid cover block the service.":
                "\n\n两地原有路标现已设立公开救护岗。安全时站在此处恢复生命每秒+1.7；战斗或隔墙不能使用。";
            if(UnsentWarningHidden)return english?
                "\n\nThe witnesses' quiet crew has left supplies at both original waymarks. Stand here safely for +3 energy/s; combat and solid cover block the service.":
                "\n\n证人们在两地旧路标旁悄悄存放了补给。安全靠近时恢复耐力每秒+3；战斗或隔墙不能使用。";
            return "";
        }
        private void AppendUnsentWarningChoice(WorldObject speaker)
        {
            if(mode!="dialog"||conversation==null||speaker==null||
               !WarningReach(speaker))return;
            if(UnsentWarningComplete&&speaker.kind=="waytrail"&&
               WarningRegion(save.room))
            {
                conversation.text+=UnsentWarningWaytrailNews(false);
                conversation.textEn+=UnsentWarningWaytrailNews(true);
                return;
            }
            if(UnsentWarningComplete&&speaker.kind=="guild"&&save.room==0)
            {
                conversation.text+=UnsentWarningInnNews(false);
                conversation.textEn+=UnsentWarningInnNews(true);
                return;
            }
            string action=null,zh=null,en=null;
            if(speaker.kind=="guild"&&save.room==0)
            {
                if(!UnsentWarningUnlocked||UnsentWarningComplete)return;
                if(!UnsentWarningStarted)
                {
                    action="unsent_start";zh="接下未送达的警报 · 沉桥与矿场";
                    en="Take the unsent warning · ferry and mine";
                }
                else if(UnsentRiverDelivered&&UnsentMineDelivered)
                {
                    // Two deliberately opposed civic endings; do not
                    // railroad this choice into a single silent branch.
                    AddUnsentChoice("unsent_publish",
                        "公开证词 · 建立两地救护岗",
                        "Publish names · Staff two first-aid watches");
                    action="unsent_protect";zh="保护证人 · 建立秘密补给线";
                    en="Protect witnesses · Supply quiet routes";
                }
            }
            else if(speaker.kind=="waytrail"&&
                    WarningRegion(save.room)&&UnsentWarningStarted&&
                    !UnsentWarningComplete)
            {
                int room=save.room;
                if(room==3&&!UnsentRiverDelivered)return;
                if(room==2&&UnsentRiverDelivered)return;
                if(speaker.id=="waytrail"+room+"s"&&
                   !UnsentWarningRunActive)
                {
                    action="unsent_run_"+room;
                    zh="包封证词 · 草药1、耐力10 · 限时递送";
                    en="Seal testimony · 1 herb / 10 energy · timed run";
                }
                if(speaker.id=="waytrail"+room+"e"&&
                   UnsentWarningRunActive&&warningRunRoom==room)
                {
                    action="unsent_deliver_"+room;
                    zh="亲手交付警报副本 · 完成实地递送";
                    en="Deliver warning · Complete physical crossing";
                }
            }
            if(action!=null)AddUnsentChoice(action,zh,en);
        }
        private void AddUnsentChoice(string action,string zh,string en)
        {
            if(conversation==null||conversation.choices==null)return;
            Choice[] old=conversation.choices;
            foreach(Choice c in old)if(c.action==action)return;
            int at=old.Length;
            if(at>0&&old[at-1].action=="close")at--;
            Choice[] revised=new Choice[old.Length+1];
            Array.Copy(old,0,revised,0,at);
            revised[at]=C(zh,en,action);
            Array.Copy(old,at,revised,at+1,old.Length-at);
            conversation.choices=revised;
        }
        private bool ChooseUnsentWarning(string action)
        {
            if(ChooseUnsentWarningRelay(action))return true;
            if(action!="unsent_start"&&action!="unsent_publish"&&
               action!="unsent_protect"&&action!="unsent_run_2"&&
               action!="unsent_run_3"&&action!="unsent_deliver_2"&&
               action!="unsent_deliver_3")return false;
            WorldObject source=pendingDialogueSource;
            if(source==null||mode!="world"||Dying||practicing||
               pendingDialogueSourceRoom!=save.room||
               !WarningReach(source))return true;
            if(action=="unsent_start")
            {
                if(source.kind!="guild"||save.room!=0||
                   UnsentWarningStarted||!UnsentWarningUnlocked)return true;
                Flag("unsent_warning_started");
                TrackQuest("unsent_warning");revision++;sound="quest";
                Notify("沉桥和矿场的两封求援警报尚未送达。先去沉没遗迹西侧旧路标。",
                    "Two warnings never reached town. Begin at the western Drowned Ruins waymark.");
                return true;
            }
            if(action=="unsent_run_2"||action=="unsent_run_3")
            {
                int room=action=="unsent_run_2"?2:3;
                if(source.id!="waytrail"+room+"s"||save.room!=room)return true;
                if(!BeginWarningRun(room))
                    Notify("必须在安全路标旁准备草药1、耐力10，并留出真实巡路通道。",
                        "Prepare one herb and ten energy at the safe waymark with clear approach lanes.");
                return true;
            }
            if(action=="unsent_deliver_2"||action=="unsent_deliver_3")
            {
                int room=action=="unsent_deliver_2"?2:3;
                if(save.room!=room||source.id!="waytrail"+room+"e"||
                   !UnsentWarningRunActive||warningRunRoom!=room)return true;
                if(warningTime<=0f||warningIntegrity<=0f||
                   warningMeters<18f)
                {
                    Notify("不能从菜单伪造递送。必须亲自在此路段行走至少18格，且信封完好。",
                        "This warning needs at least 18 tiles of real traveled footing and an intact seal.");
                    return true;
                }
                int relayUsed=warningRelayChoice;
                ResetUnsentWarning();
                Flag(room==2?"unsent_warning_river":"unsent_warning_mine");
                // The choice is durable only if the letter actually reached
                // the receiving marker; retries never bank fake evidence.
                if(relayUsed!=0)Flag("unsent_warning_relay_"+room);
                revision++;sound="reward";
                RecordMoment(room==2?
                    "湿漉漉的警报抵达沉桥东路标，守路人发现上面还有被抹去的孩子名字。":
                    "矿场警报终于穿过废轨，旧路标的背面写着那天还在值班的七位无名矿工。",
                    room==2?
                    "The wet warning crossed the bridge; the east watchkeeper found the erased children's names.":
                    "The mine warning passed the abandoned rails; seven unlisted workers were still signed on.");
                Notify(room==2?"沉桥警报已交付！下一站：赤铁矿场西路标。":
                    "矿场警报已交付！返回余烬镇公会选择归档方式。",
                    room==2?"Drowned warning delivered! Next: Ironreach west waymark.":
                    "Mine warning delivered! Return to Emberfall guild for the final choice.");
                return true;
            }
            if(source.kind!="guild"||save.room!=0||
               !UnsentWarningStarted||!UnsentRiverDelivered||
               !UnsentMineDelivered||UnsentWarningComplete)return true;
            if(save.coins>999929||save.shards>99997)
            {
                Notify("先腾出至少70硬币和2碎片容量，证词会继续保留。",
                    "Make room for 70 coins and two shards; the testimony stays safe.");
                return true;
            }
            bool publish=action=="unsent_publish";
            bool veteranRelays=UnsentWarningRelayCount==2;
            Flag(publish?"unsent_warning_public":"unsent_warning_hidden");
            CreditCoins(70);CreditShards(2);AddXp(120);
            // A third progression reward is earned only after using a
            // REAL relay on both live crossings and delivering both packets.
            // No bonus from start-only, failed, abandoned or reloaded runs.
            if(veteranRelays)CreditSkillPoints(1);
            sound="reward";revision++;
            RecordMoment(publish?
                "两封从未送达的警报终于贴在公会门口。沉桥和矿场的旧路标旁都有人架起急救岗，失名者不再被当成货物。":
                "两封警报被交给信任的守路人。证人不必向巡兵公开住处，两地旧路标旁却悄悄有了补给与体力恢复的地方。",
                publish?
                "Two unsent warnings finally reached the guild wall. Both old waymarks now staff first aid for the publicly named families.":
                "Trusted wardens keep the witnesses' identities safe. Quiet supply stops now recover travelers' strength at both waymarks.");
            if(veteranRelays)RecordMoment(
                "两处实地接应站都有人留下你的手绘路线：修补破损的纸页或用烟雾骗过追兵，原来都能让证词活着抵达。守路队把这份战术记作一项真正的新专精。",
                "Both relay keepers retained your field route: waterproofing a torn warning or distracting a live pursuer can each bring real testimony home. The wardens record one earned tactical mastery.");
            Notify((publish?
                "公开警报：硬币70、碎片2、经验120；沉桥与矿场旧路标可安全疗伤。":
                "保护证人：硬币70、碎片2、经验120；两地路标安全恢复耐力+3/秒。")+
                (veteranRelays?" 双接应站专精：技能点+1。":""),
                (publish?
                "Warnings published: +70 coins, 2 shards, 120 XP; safe waymark healing in both regions.":
                "Witnesses protected: +70 coins, 2 shards, 120 XP; +3 energy/s at both safe waymarks.")+
                (veteranRelays?" Both real relays mastered: +1 skill point.":""));
            return true;
        }
    }
}
