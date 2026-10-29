using System;
using System.Collections.Generic;
namespace Starfall
{
    // Civilian last-mile rescue: two genuine field endpoints, two actual
    // defenders, an in-person material handoff and a return receipt.
    // Owned hostiles/timers are TRANSIENT; the one-time public result is a
    // native SaveData v2 string flag. No new actor or save schema.
    public sealed partial class GameCore
    {
        private int biomeCrisisStage,biomeCrisisRegion=-1,biomeCrisisIntegrity;
        private float biomeCrisisSeconds;
        private MapData biomeCrisisMap;
        private SaveData biomeCrisisSave;
        private WorldObject biomeCrisisOrigin,biomeCrisisDestination;
        private readonly List<Enemy> biomeCrisisRaiders=new List<Enemy>();
        private readonly HashSet<Enemy> biomeCrisisKills=new HashSet<Enemy>();
        public bool BiomeCrisisActive
        {
            get{return biomeCrisisStage>0&&biomeCrisisRegion==save.room&&
                Object.ReferenceEquals(biomeCrisisMap,Map)&&
                Object.ReferenceEquals(biomeCrisisSave,save)&&
                SourceBelongsToActiveMap(biomeCrisisOrigin)&&
                SourceBelongsToActiveMap(biomeCrisisDestination);}
        }
        public int BiomeCrisisStage {get{return BiomeCrisisActive?biomeCrisisStage:0;}}
        public int BiomeCrisisIntegrity {get{return BiomeCrisisActive?biomeCrisisIntegrity:0;}}
        public float BiomeCrisisSeconds {get{return BiomeCrisisActive?biomeCrisisSeconds:0f;}}
        public Point BiomeCrisisTarget
        {
            get
            {
                WorldObject site=BiomeCrisisActive?
                    (biomeCrisisStage==3?biomeCrisisOrigin:biomeCrisisDestination):null;
                return site==null?new Point():new Point(site.x,site.y);
            }
        }
        public int BiomeCrisisRemaining
        {get{int n=0;if(BiomeCrisisActive)foreach(Enemy e in biomeCrisisRaiders)
            if(enemies.Contains(e)&&!biomeCrisisKills.Contains(e))n++;return n;}}
        public int BiomeCrisisWins
        {get{int n=0;for(int i=0;i<8;i++)if(Has("biome_crisis_done_"+i))n++;
            return n;}}
        private bool IsBiomeCrisisEnemy(Enemy e)
        {return e!=null&&biomeCrisisStage>0&&
            biomeCrisisRaiders.Contains(e);}
        private void OnBiomeCrisisEnemyDefeated(Enemy e)
        {if(IsBiomeCrisisEnemy(e))biomeCrisisKills.Add(e);}
        private void ResetBiomeCrisis()
        {
            if(biomeCrisisStage>0)
            {
                projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                    biomeCrisisRaiders.Contains(p.hostileShooter));
                for(int i=enemies.Count-1;i>=0;i--)
                    if(biomeCrisisRaiders.Contains(enemies[i]))
                    {RemoveEnemyPendingWarning(enemies[i]);enemies.RemoveAt(i);}
            }
            biomeCrisisRegion=-1;biomeCrisisStage=biomeCrisisIntegrity=0;
            biomeCrisisSeconds=0f;biomeCrisisMap=null;biomeCrisisSave=null;
            biomeCrisisOrigin=biomeCrisisDestination=null;
            biomeCrisisRaiders.Clear();biomeCrisisKills.Clear();
        }
        private bool SpawnBiomeCrisisRaiders(WorldObject dest)
        {
            Point center=new Point(dest.x,dest.y);
            Point[] offsets={
                new Point(-5,0),new Point(5,0),new Point(0,5),new Point(0,-5),
                new Point(-7,2),new Point(7,-2),new Point(-5,-5),
                new Point(5,5),new Point(7,4),new Point(-7,-4),
                new Point(0,8),new Point(8,0),new Point(-8,0),
                new Point(0,-8),new Point(-4,8),new Point(4,-8)};
            var sites=new List<Point>();
            foreach(Point offset in offsets)
            {
                Point foot=center+offset;
                if(!CanStand(foot)||(foot-Position).Length<2f||
                    (save.companion>=0&&(foot-companionPosition).Length<2f)||
                    enemies.Exists(e=>e!=null&&e.hp>0f&&
                        (e.Position-foot).Length<2f)||
                    sites.Exists(p=>(p-foot).Length<2.6f)||
                    !ActorHasLocalRoute(foot,center,16))continue;
                sites.Add(foot);if(sites.Count==2)break;
            }
            if(sites.Count<2)
            {
                Notify("远端救援点缺少可供袭击者真正接近的道路。清理敌人后再试。",
                    "The remote handoff has too few genuine approach lanes. Clear hostiles and retry.");
                return false;
            }
            for(int i=0;i<2;i++)
            {
                int kind=(biomeCrisisRegion+i)%3;
                Enemy e=CreatePlacedEnemy(++enemyId,kind,sites[i],
                    48f+biomeCrisisRegion*6f);
                biomeCrisisRaiders.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",sites[i],.65f){variant=kind});
            }
            return true;
        }
        private void TickBiomeCrisis(float dt)
        {
            if(biomeCrisisStage==0)return;
            if(!BiomeCrisisActive||Dying){ResetBiomeCrisis();return;}
            // Time advances only during actual world simulation, not menus,
            // resting, or the storage/quest UI.
            biomeCrisisSeconds=Math.Max(0f,biomeCrisisSeconds-dt);
            if(biomeCrisisSeconds<=0f)
            {
                ResetBiomeCrisis();revision++;
                Notify("居民物资未能在规定时间送达。可以重新接取。",
                    "Last-mile rescue timed out. You can retry from the starting post.");
                return;
            }
            if(biomeCrisisStage!=1)return;
            foreach(Enemy e in biomeCrisisRaiders)
                if(!enemies.Contains(e)&&!biomeCrisisKills.Contains(e))
                {
                    ResetBiomeCrisis();revision++;
                    Notify("袭击者未被实战击倒，无法确认安全交接。",
                        "A raider vanished without a real defeat. The handoff is unverified.");
                    return;
                }
            bool secured=biomeCrisisRaiders.Count==2;
            foreach(Enemy e in biomeCrisisRaiders)
                if(enemies.Contains(e)||!biomeCrisisKills.Contains(e))secured=false;
            if(!secured)return;
            // Never let an old arrow carry into the cleared handoff phase.
            projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                biomeCrisisRaiders.Contains(p.hostileShooter));
            biomeCrisisStage=2;revision++;sound="quest";
            Notify("两名袭击者已真正击退！去远端调查碑交付物资，再返回出发地标。",
                "Both real raiders defeated! Deliver supplies at the far monument, then return to the departure post.");
        }
        private void DamageBiomeCrisisCargo(float actualDamage)
        {
            if(!BiomeCrisisActive||!Finite(actualDamage)||actualDamage<=0f)return;
            biomeCrisisIntegrity--;
            if(biomeCrisisIntegrity<=0)
            {
                ResetBiomeCrisis();revision++;
                Notify("救援物资连续受损，任务失败。可以重新接取。",
                    "Repeated real hits destroyed the relief cargo. Retry from the origin.");
            }
            else
            {
                revision++;
                Notify("救援包受到真实伤害，完整度剩余"+biomeCrisisIntegrity+"。",
                    "Real combat damaged the supply packet: "+biomeCrisisIntegrity+" integrity left.");
            }
        }
        // The eighth authentic delivery unlocks the civilian council at
        // the ONE surviving physical Hearth Inn archive. This is a genuine
        // two-policy ending, not a map click or a repeatable reward receipt.
        private bool ChooseBiomeCrisisCouncil(string action)
        {
            if(action!="biome_crisis_council"&&
                action!="biome_crisis_council_shelters"&&
                action!="biome_crisis_council_roads")return false;
            WorldObject archive=pendingDialogueSource;
            if(save.room!=9||mode!="world"||Dying||practicing||
                !Has("biome_crisis_all")||BiomeCrisisWins!=8||
                Has("biome_crisis_council_shelters")||
                Has("biome_crisis_council_roads")||
                archive==null||archive.kind!="annexinnarchive"||
                pendingDialogueSourceRoom!=9||
                !SourceBelongsToActiveMap(archive)||
                !HasPhysicalInteractionReach(archive))return true;
            if(action=="biome_crisis_council")
            {
                Dialog("八地救援 · 最后一页","Eight Deliveries · The Final Page",
                    "值夜的记录员将八张沾着泥的签收单逐一摊平，没有人为这八趟路写下庆功词。"+
                    "\n\n第一张是洪水里的药，第四张沾着矿灰，最后一张纸边结了霜。"+
                    "有人问：“下次警报响起时，这些多出来的物资应该先放在哪里？”"+
                    "\n\n她请你替沿路的居民作出一个公开决定，两种安排只能选择其一。"+
                    "这份记录不会重新发放八地护送的奖励。",
                    "The night recorder lays eight mud-stained receipts beside the hearth. Nobody writes a victory anthem. "+
                    "The first is a wet medicine order, the fourth bears coal dust and the last is rimed with frost."+
                    "\n\n'Where should our remaining supplies go before the next alarm?' "+
                    "The households ask for one shared policy. Pick one. No second payout can be claimed.",
                    C("开放沿路避难补给 · 所有户外营地+10生命",
                        "Public shelters · +10 HP at every outdoor camp",
                        "biome_crisis_council_shelters"),
                    C("维护所有居民通路 · 本地行走提速，接驳-2耐力",
                        "Public roads · outdoor speed and -2 relay energy",
                        "biome_crisis_council_roads"),
                    C("先把签收单收好","Keep the receipts for now","close"));
                return true;
            }
            bool shelters=action=="biome_crisis_council_shelters";
            Flag(shelters?"biome_crisis_council_shelters":
                "biome_crisis_council_roads");
            CreditCoins(60);CreditShards(2);
            RecordMoment(shelters?
                "八地签收归档后，旅店没有把救援物资交回公会库房，而是分给每个户外营地的公共避难箱。今后任何旅人都能先休息，再讲述路上遇见了什么。":
                "八地签收归档后，修路工把剩下的零件送去所有公开接驳点。路更好走，接驳更省力，没有人需要出示军令。",
                shelters?
                "After eight signed relief deliveries, spare medicine is distributed among every outdoor camp, open to travelers before anyone asks their business.":
                "After eight signed relief deliveries, the mechanics repair the public approaches to all local field stations, making travel easier for everyone.");
            revision++;sound="reward";
            Dialog("最后一页 · 居民的决定","The Final Page · A Civilian Decision",
                shelters?
                "各地避难箱都添上一份药材。此后八地营地每次安全休息额外恢复10生命。"+
                "\n\n签收的姓名仍留在原册上。硬币+60，碎片+2。":
                "从湿地旧栈桥到霜钟背风路，每一处公开地标都有了可用的修复材料。"+
                "此后户外移速额外+0.1，所有居民接驳额外节约2耐力（最低1）。"+
                "\n\n硬币+60，碎片+2。",
                shelters?
                "Public shelter stock now adds +10 HP to safe outdoor camp rest in all eight regions. "+
                "The original names remain on the record. +60 coins, +2 shards.":
                "Public roads now grant +0.1 outdoor speed and save 2 energy on all field relays (minimum 1). "+
                "+60 coins, +2 shards.",
                C("归档最后一页","File the final page","close"));
            return true;
        }

        private bool ChooseBiomeCrisis(string action)
        {
            if(ChooseBiomeCrisisCouncil(action))return true;
            if(action!="biome_crisis_begin"&&action!="biome_crisis_deliver"&&
                action!="biome_crisis_report"&&action!="biome_crisis_abort")
                return false;
            WorldObject site=pendingDialogueSource;
            int room=save.room;
            if(room<0||room>7||mode!="world"||Dying||practicing||
                site==null||pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(site)||!HasPhysicalInteractionReach(site)||
                !CanStand(Position)||!Has("biome_field_done_"+room)||
                !BiomeSupplyBuilt(room)||
                (site.kind!="biomeSurvey"&&site.kind!="biomeAnswer")||
                site.id!="biome"+
                    (site.kind=="biomeSurvey"?"Survey":"Answer")+room)return true;
            if(action=="biome_crisis_begin")
            {
                if(site.kind!="biomeSurvey"||biomeCrisisStage!=0||
                    Has("biome_crisis_done_"+room))return true;
                if(FieldWorkPreventsRest||!SafeTravel||racing||
                    BlockTravelForProtectedFieldMission(true))
                {
                    Notify("附近还有敌人或现场任务。先处理完再护送公共补给。",
                        "Clear nearby danger and other fieldwork before starting public rescue.");
                    return true;
                }
                WorldObject dest=null;
                foreach(WorldObject obj in Map.objects)
                    if(obj!=null&&obj.id=="biomeAnswer"+room&&
                        obj.kind=="biomeAnswer"){dest=obj;break;}
                if(dest==null||!CanStand(new Point(dest.x,dest.y))||
                    (new Point(dest.x,dest.y)-Position).Length<24f)
                    return true;
                biomeCrisisRegion=room;biomeCrisisMap=Map;
                biomeCrisisSave=save;biomeCrisisOrigin=site;
                biomeCrisisDestination=dest;
                biomeCrisisStage=1;biomeCrisisIntegrity=3;
                biomeCrisisSeconds=300f;
                if(!SpawnBiomeCrisisRaiders(dest)){ResetBiomeCrisis();return true;}
                revision++;sound="quest";
                Notify("紧急物资护送：五分钟内步行穿过真实地区，在远端击退两名袭击者、交付物资并返回。三次受伤将毁掉救援包。",
                    "Emergency dispatch: in five minutes reach the far monument on foot, defeat two real raiders, deliver materials and return. Three damaging hits destroy cargo.");
                return true;
            }
            if(!BiomeCrisisActive)return true;
            if(action=="biome_crisis_abort")
            {
                if(!Object.ReferenceEquals(site,biomeCrisisOrigin))return true;
                ResetBiomeCrisis();revision++;sound="ui";
                Notify("已经取消居民救援。没有扣除交付物资，也没有领取奖励。",
                    "Relief withdrawn. No handoff materials charged and no reward granted.");
                return true;
            }
            if(action=="biome_crisis_deliver")
            {
                if(biomeCrisisStage!=2||
                    !Object.ReferenceEquals(site,biomeCrisisDestination))return true;
                if(!SafeTravel)
                {
                    Notify("远端仍有正在攻击的敌人，无法安全交接。",
                        "The remote handoff is still under live attack.");
                    return true;
                }
                bool medical=BiomeSupplyMedicineBuilt(room);
                // One genuine herb or timber has to reach the endangered
                // household. No free phase transition if the inventory is
                // empty or full mission progress has been faked.
                if(!Spend(medical?0:1,0,medical?1:0,0,0))return true;
                biomeCrisisStage=3;revision++;sound="quest";
                effects.Add(new Effect("discover",new Point(site.x,site.y),.75f));
                Notify(medical?
                    "伤者已拿到月光草，远端救助成功。携回签收单。":
                    "居民已拿到木材，旧路恢复通行。携回签收单。",
                    medical?
                    "Moonherb delivered to the injured household. Return with its signed receipt.":
                    "Timber delivered and the crossing reopened. Return with the signed receipt.");
                return true;
            }
            if(biomeCrisisStage!=3||
                !Object.ReferenceEquals(site,biomeCrisisOrigin))return true;
            int completed=biomeCrisisRegion;
            ResetBiomeCrisis();
            if(Has("biome_crisis_done_"+completed))return true;
            Flag("biome_crisis_done_"+completed);
            CreditCoins(38);CreditShards(1);AddXp(65);
            bool helped=BiomeSupplyMedicineBuilt(completed);
            RecordMoment(helped?
                "第"+(completed+1)+"地区的巡路员击退抢药人，亲手交付了救命的药草，又把居民的签收带回起点。公共诊所不再只写在帐本里。":
                "第"+(completed+1)+"地区的巡路员穿过真正被袭击的道路，交付木材，又把修复完成的消息带回起点。道路不再只存在于公会的地图上。",
                helped?
                "In region "+(completed+1)+", the rescuer defeated medicine thieves, delivered real medicine and brought home a signed civilian receipt.":
                "In region "+(completed+1)+", the rescuer fought through a genuine road ambush, delivered repair timber and brought home proof of an open civilian crossing.");
            if(BiomeCrisisWins==8&&!Has("biome_crisis_all"))
            {
                Flag("biome_crisis_all");CreditSkillPoints(1);
                RecordMoment("八地的公用补给不是一张盖章证明，而是八次真实交付。人们在册子上写下了收到物资的名字。",
                    "Eight districts earned their public supplies through eight real deliveries. The register now bears the recipients' names.");
            }
            revision++;sound="reward";
            Notify("居民紧急护送完成！硬币+38、碎片+1、经验+65，已救援"+
                BiomeCrisisWins+"/8地区。",
                "Emergency relief delivered: +38 coins, +1 shard, +65 XP. "+
                BiomeCrisisWins+"/8 districts secured.");
            return true;
        }
    }
}
