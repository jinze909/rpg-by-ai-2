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
            if(warningTime<=0f)
            {
                ResetUnsentWarning();revision++;
                Notify("警报已过递送时限。回西侧路标重新出发。",
                    "Warning delivery timed out. Restart from the west waymark.");
            }
        }
        private void AppendUnsentWarningChoice(WorldObject speaker)
        {
            if(mode!="dialog"||conversation==null||speaker==null||
               !WarningReach(speaker))return;
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
                ResetUnsentWarning();
                Flag(room==2?"unsent_warning_river":"unsent_warning_mine");
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
            Flag(publish?"unsent_warning_public":"unsent_warning_hidden");
            CreditCoins(70);CreditShards(2);AddXp(120);
            sound="reward";revision++;
            RecordMoment(publish?
                "两封从未送达的警报终于贴在公会门口。沉桥和矿场的旧路标旁都有人架起急救岗，失名者不再被当成货物。":
                "两封警报被交给信任的守路人。证人不必向巡兵公开住处，两地旧路标旁却悄悄有了补给与体力恢复的地方。",
                publish?
                "Two unsent warnings finally reached the guild wall. Both old waymarks now staff first aid for the publicly named families.":
                "Trusted wardens keep the witnesses' identities safe. Quiet supply stops now recover travelers' strength at both waymarks.");
            Notify(publish?
                "公开警报：硬币70、碎片2、经验120；沉桥与矿场旧路标可安全疗伤。":
                "保护证人：硬币70、碎片2、经验120；两地路标安全恢复耐力+3/秒。",
                publish?
                "Warnings published: +70 coins, 2 shards, 120 XP; safe waymark healing in both regions.":
                "Witnesses protected: +70 coins, 2 shards, 120 XP; +3 energy/s at both safe waymarks.");
            return true;
        }
    }
}
