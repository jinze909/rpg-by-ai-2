using System;
namespace Starfall
{
    // The Drowned Ruins remember civilians whom the official cargo manifest
    // erased. This side quest uses ONLY version-two string flags.
    public sealed partial class GameCore
    {
        public bool FloodArchiveComplete
        { get { return Has("floodarchive_public")||Has("floodarchive_chart"); } }
        public int FloodArchiveChoice
        { get { return Has("floodarchive_public")?1:Has("floodarchive_chart")?2:0; } }
        public int FloodArchiveProgress
        {
            get
            {
                if(FloodArchiveComplete)return 3;
                if(Has("floodarchive_2"))return 2;
                if(Has("floodarchive_1"))return 1;
                return 0;
            }
        }
        // Post-archive civilian medicine crossing: authentic low-tide ferry,
        // two confirmed real raider defeats and an intact sealed parcel.
        // The only permanent result is an ordinary v2 quest flag.
        private bool floodReliefActive,floodReliefCrossed,floodReliefPatched,floodReliefWarded;
        private float floodReliefTime,floodReliefIntegrity,floodReliefWardTime;
        public float FloodReliefWardTime
        { get {return FloodReliefActive?floodReliefWardTime:0;} }
        private int floodReliefConfirmedKills;
        // Unsaved, true actor ownership for the two east-bank ambushers.
        // An unrelated world Enemy can reuse 92340/92341 across optional
        // encounters; IDs alone cannot authorize kills or cleanup.
        private Enemy floodReliefRaiderA,floodReliefRaiderB;
        public bool FloodReliefComplete { get { return Has("floodarchive_relief"); } }
        public int PublicFerryEnergyCost { get { return FloodReliefComplete?10:14; } }
        public bool FloodReliefActive { get { return floodReliefActive&&save.room==2&&!Dying; } }
        public bool FloodReliefCrossed { get { return FloodReliefActive&&floodReliefCrossed; } }
        public float FloodReliefTime { get { return FloodReliefActive?floodReliefTime:0; } }
        public float FloodReliefIntegrity { get { return FloodReliefActive?floodReliefIntegrity:0; } }
        public int FloodReliefRaiders
        { get {return enemies.FindAll(e=>IsFloodReliefEnemy(e)&&e.hp>0).Count;} }
        public int FloodReliefDefeated
        { get { if(!FloodReliefActive)return 0;
                return (floodReliefConfirmedKills&1)+((floodReliefConfirmedKills>>1)&1); } }
        public bool FloodReliefSecured
        { get { return FloodReliefCrossed&&floodReliefConfirmedKills==3&&
                       floodReliefIntegrity>0; } }
        public bool FloodReliefRepairAvailable
        { get {return FloodReliefCrossed&&!floodReliefPatched&&
            floodReliefIntegrity>0&&floodReliefIntegrity<100&&save.inventory[2]>=1;} }
        // This is the actual tracked objective used by the live compass,
        // quest journal and world marker. An ambush is not complete merely
        // because the hero reached the east pier: follow its two owned,
        // living fighters until both genuine combat callbacks confirm kills.
        // Neither a reused numeric spawn ID nor an unrelated world enemy
        // is allowed to steal the civilian delivery waypoint.
        public Point FloodReliefObjective
        {
            get
            {
                WorldObject west=FloodArchiveObject(1),east=FloodArchiveObject(3);
                Point westDock=west==null?new Point(59.5f,37.5f):
                    new Point(west.x,west.y);
                Point eastDock=east==null?new Point(79.5f,35.5f):
                    new Point(east.x,east.y);
                if(FloodReliefComplete)return eastDock;
                if(!FloodReliefCrossed)return westDock;
                if(FloodReliefSecured)return eastDock;
                Enemy first=(floodReliefConfirmedKills&1)==0&&
                    floodReliefRaiderA!=null&&floodReliefRaiderA.hp>0&&
                    enemies.Contains(floodReliefRaiderA)?floodReliefRaiderA:null;
                Enemy second=(floodReliefConfirmedKills&2)==0&&
                    floodReliefRaiderB!=null&&floodReliefRaiderB.hp>0&&
                    enemies.Contains(floodReliefRaiderB)?floodReliefRaiderB:null;
                if(first==null)return second==null?eastDock:second.Position;
                if(second==null)return first.Position;
                // Nearer true raider first; on equal distance, stable
                // ambush ordering avoids oscillating compass targets.
                return (first.Position-Position).Length<=
                    (second.Position-Position).Length?
                    first.Position:second.Position;
            }
        }
        private bool IsFloodReliefEnemy(Enemy e)
        {return e!=null&&(System.Object.ReferenceEquals(e,floodReliefRaiderA)||
                           System.Object.ReferenceEquals(e,floodReliefRaiderB));}
        private void ResetFloodRelief()
        {
            floodReliefActive=floodReliefCrossed=floodReliefPatched=floodReliefWarded=false;
            floodReliefTime=floodReliefIntegrity=floodReliefWardTime=0;
            floodReliefConfirmedKills=0;
            enemies.RemoveAll(IsFloodReliefEnemy);
            floodReliefRaiderA=floodReliefRaiderB=null;
            // Only this escort's launched arrows and windup/contact effects
            // may be retired. Untagged wilderness combat remains live.
            projectiles.RemoveAll(p=>p.floodReliefOwned);
            effects.RemoveAll(e=>e.floodReliefOwned);
        }
        private void OnFloodReliefEnemyDefeated(Enemy e)
        {
            if(!FloodReliefActive||!floodReliefCrossed||!IsFloodReliefEnemy(e))return;
            floodReliefConfirmedKills|=
                System.Object.ReferenceEquals(e,floodReliefRaiderA)?1:2;
        }
        private void DamageFloodReliefParcel(float damage,string source)
        {
            if(!FloodReliefActive||!floodReliefCrossed||
                source=="environment"||damage<=0)return;
            float shock=Math.Max(14,Math.Min(38,damage*1.15f));
            if(floodReliefWardTime>0)shock*=.45f;
            floodReliefIntegrity=Math.Max(0,floodReliefIntegrity-shock);
            if(floodReliefIntegrity<=0)
            {
                ResetFloodRelief();revision++;
                Notify("医疗包在拦截战中破损。可返回西岸重新领取，不会扣除原有任务奖励。",
                    "The medical parcel was destroyed by combat. Return to the west dock to try again.");
            }
            else Notify("医疗包受到真实攻击！护住剩余药品并击退拦截者。",
                "The medical parcel took real hit damage! Defend the remaining supplies.");
        }
        private void TickFloodRelief(float dt)
        {
            if(!floodReliefActive)return;
            if(save.room!=2||Dying||practicing)
            {ResetFloodRelief();revision++;return;}
            floodReliefTime-=dt;
            floodReliefWardTime=Math.Max(0,floodReliefWardTime-dt);
            if(floodReliefTime<=0)
            {
                ResetFloodRelief();revision++;
                Notify("低潮护送超时。仍可安全重试，任何已收集的历史档案不受影响。",
                    "Tide relief run timed out. The public archive remains intact; retry at the west dock.");
                return;
            }
            if(floodReliefCrossed)
                for(int i=0;i<2;i++)
                    if((floodReliefConfirmedKills&(1<<i))==0&&
                        !enemies.Contains(i==0?
                            floodReliefRaiderA:floodReliefRaiderB))
                    {
                        ResetFloodRelief();revision++;
                        Notify("一名拦截者异常离场，无法将其算作实际击败。护送可重试。",
                            "A raider vanished without a real defeat; the relief run can be retried.");
                        return;
                    }
        }
        // The ferry escorts an actual parcel into a two-raider ambush.
        // NavigationPath may legally return a partial path to the nearest
        // shoreline when the target is unreachable. That is not a combat
        // spawn: the attackers must have a complete walkable body-foot
        // route into the *real* east-bank landing, and occupy free ground.
        private bool TryFloodReliefRaiderSpots(Point anchor,out Point[] result)
        {
            result=null;
            if(!CanStand(anchor))return false;
            var spots=new System.Collections.Generic.List<Point>();
            for(int radius=3;radius<=9&&spots.Count<2;radius++)
                for(int dx=-radius;dx<=radius&&spots.Count<2;dx++)
                    for(int dy=-radius;dy<=radius&&spots.Count<2;dy++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                        Point p=anchor+new Point(dx,dy);
                        if(!CanStand(p)||(p-anchor).Length<3f||
                            (p-Position).Length<1.25f||
                            (save.companion>=0 &&
                             (p-companionPosition).Length<1.4f)||
                            spots.Exists(other=>(other-p).Length<3f)||
                            enemies.Exists(e=>e.hp>0&&
                                (e.Position-p).Length<2.2f)||
                            // Keep the real attack lane clear, not a merely
                            // reachable pocket behind a civilian wall.
                            !ClearAttackPath(anchor,p)||
                            // Radius 12 bounds the route around a nearby
                            // pier; all Boss calls keep their prior 5 tiles.
                            !ActorHasLocalRoute(p,anchor,12))
                            continue;
                        spots.Add(p);
                    }
            if(spots.Count!=2)return false;
            result=spots.ToArray();return true;
        }
        private void SpawnFloodReliefRaiders(Point[] spots)
        {
            floodReliefRaiderA=floodReliefRaiderB=null;
            // Commit ONLY the already verified physical feet; recovery
            // spawning can relocate a new enemy to another bank and must
            // never silently override the intended combat encounter.
            for(int i=0;i<2;i++)
            {
                var e=new Enemy(92340+i,i==0?0:1,
                    spots[i].x,spots[i].y,78+i*14);
                e.timer=.8f+i*.25f;enemies.Add(e);
                if(i==0)floodReliefRaiderA=e;
                else floodReliefRaiderB=e;
            }
        }
        private WorldObject FloodArchiveObject(int stage)
        {
            if(stage<1||stage>3)return null;
            foreach(WorldObject o in data.maps[2].objects)
                if(o.kind=="floodarchive"&&o.id=="floodarchive_"+stage)return o;
            return null;
        }
        // Tide period and any legitimately held low tide are the existing
        // shared simulation clocks, not a second unsaved puzzle clock.
        public float FloodArchiveWait(int stage)
        {
            if(stage==1)
                return !LowTide?0:
                    save.totalTime<save.tideHeldUntil?
                    Math.Max(0,save.tideHeldUntil-save.totalTime):
                    22f-save.totalTime%36f;
            if(stage==2)
                return LowTide?0:36f-save.totalTime%36f;
            return 0;
        }
        private void FloodArchiveDialog(WorldObject site,string zh,string en,params Choice[] choices)
        { Dialog(site.name,site.en,zh,en,choices); }
        private bool TalkFloodArchive(WorldObject site)
        {
            if(site.kind!="floodarchive")return false;
            int stage=0;
            for(int i=1;i<=3;i++)if(site.id=="floodarchive_"+i)stage=i;
            if(stage==0||save.room!=2||!AtFloodArchivePost(site))
                return true;
            if(!Has("scout_saved"))
            {
                FloodArchiveDialog(site,
                    "残存水位刻度提到荆棘密林中的失踪侦察员。先救回他们，才有人能认出这份民用名单。",
                    "The tide scale mentions the missing Thornwood scout. Rescue the scout before anyone can identify the civilian names.",
                    C("记下方位","Mark the location","close"));
                return true;
            }
            if(FloodArchiveComplete)
            {
                string endZh=FloodArchiveChoice==1?
                    "你将名字交还摆渡人。低潮时他们在这里协助伤者，公共潮标附近能安全恢复生命。":
                    "你保留了水位原稿并教会渡船计算潮道，公共潮标附近能安全恢复耐力。";
                string endEn=FloodArchiveChoice==1?
                    "The names returned to the ferrymen. Nearby public tideposts now help wounded travelers recover safely.":
                    "The original tide charts were preserved for the ferry pilots. Nearby posts now help travelers regain stamina safely.";
                bool ferryDock=stage==1||stage==3;
                var choices=new System.Collections.Generic.List<Choice>();
                choices.Add(C("重读公开记录","Read the record","close"));
                if(!FloodReliefComplete&&stage==1&&LowTide&&!floodReliefActive)
                    choices.Add(C("低潮医疗护送 · 击退拦截者",
                        "Low-tide medical convoy · defeat raiders","floodarchive_relief_start"));
                if(floodReliefActive&&stage==1&&!floodReliefCrossed)
                    choices.Add(C("取消医疗护送","Cancel medical convoy","floodarchive_relief_abort"));
                if(floodReliefActive&&stage==3&&floodReliefCrossed)
                    choices.Add(C("将剩余药品留在东岸 · 撤销护送",
                        "Leave medicine at east dock · cancel run","floodarchive_relief_abort_east"));
                if(floodReliefActive&&stage==3&&floodReliefCrossed&&!floodReliefWarded)
                    choices.Add(C("民用护盾 · 耐力20","Protect medicine · 20 energy",
                        "floodarchive_relief_ward"));
                if(floodReliefActive&&stage==3&&floodReliefCrossed&&
                    !floodReliefPatched&&floodReliefIntegrity>0&&floodReliefIntegrity<100)
                    choices.Add(C("现场草药修补 · 草药1","Patch medical seal · 1 herb",
                        "floodarchive_relief_patch"));
                if(floodReliefActive&&stage==3&&FloodReliefSecured)
                    choices.Add(C("交付完整医疗包","Deliver guarded medicine","floodarchive_relief_deliver"));
                if(ferryDock&&LowTide)
                    choices.Add(C("乘渡船 · 耐力"+PublicFerryEnergyCost,
                        "Cross by ferry · "+PublicFerryEnergyCost+" energy","floodarchive_ferry"));
                string extraZh=FloodReliefComplete?
                    "\n\n西岸医疗物资已真正送达并登记，公民航线受到保护；渡船耐力永久降至10。":
                    floodReliefActive?
                    "\n\n医疗护送进行中："+(int)Math.Ceiling(floodReliefTime)+
                    "秒，封条 "+(int)Math.Ceiling(floodReliefIntegrity)+"%。"+
                    (floodReliefCrossed?
                        "请击败两名拦截者后在东岸交付。"+
                            (floodReliefWardTime>0?"护盾剩余 "+(int)Math.Ceiling(floodReliefWardTime)+"秒。":""):
                        "请先搭乘低潮渡船前往东岸。"):
                    "\n\n西岸还可在低潮时领取急救包，渡河并击退两名拦截者后向东岸居民交付。";
                string extraEn=FloodReliefComplete?
                    "\n\nEmergency medicine was delivered. The safer ferry now costs 10 energy.":
                    floodReliefActive?
                    "\n\nMedical crossing: "+(int)Math.Ceiling(floodReliefTime)+
                    "s remaining, cargo "+(int)Math.Ceiling(floodReliefIntegrity)+"%. "+
                    (floodReliefCrossed?
                        "Defeat both raiders and deliver at this east dock."+
                            (floodReliefWardTime>0?" Ward "+(int)Math.Ceiling(floodReliefWardTime)+"s left.":""):
                        "Take the low-tide ferry to the east shore."):
                    "\n\nAn urgent medical parcel awaits at the west dock at low tide; ferry across and defeat two raiders before delivery.";
                FloodArchiveDialog(site,endZh+extraZh+
                    (ferryDock&&!LowTide?"\n\n水位过高，渡船等待退潮。":""),
                    endEn+extraEn+
                    (ferryDock&&!LowTide?"\n\nThe ferry waits for ebb tide.":""),
                    choices.ToArray());
                return true;
            }
            int progress=FloodArchiveProgress;
            if(stage<=progress)
            {
                FloodArchiveDialog(site,
                    "这里已找回原始民用记录。旧港账上漏写的不是货物，而是没有许可证的人。",
                    "The recovered civic record remembers people who had no travel papers, not simply lost cargo.",
                    C("继续寻找证据","Keep searching","close"));
                return true;
            }
            if(stage!=progress+1)
            {
                FloodArchiveDialog(site,
                    "档案缺页。必须按旧桥水尺、摆渡名册、最后公议台的顺序实地核对。",
                    "The archive is incomplete. Survey the flood gauge, ferry roster and public desk in order.",
                    C("返回上一处","Find previous evidence","close"));
                return true;
            }
            if(stage==1)
            {
                string zh="旧桥水尺记着未经许可的救援小艇。这份水线只有**涨潮**时才能辨认。";
                string en="The flood gauge records unlicensed rescue boats. Its upper watermark is legible only at HIGH tide.";
                if(LowTide)
                {
                    float wait=FloodArchiveWait(1);
                    FloodArchiveDialog(site,zh+"\n\n现在仍是低潮，约 "+wait.ToString("0")+" 秒后复查；交谈时潮汐暂停。",
                        en+"\n\nCurrently LOW tide. Return in about "+wait.ToString("0")+" seconds. Dialogue pauses time.",
                        C("等待涨潮","Wait for high tide","close"));
                }
                else FloodArchiveDialog(site,zh+"\n\n现在可以誊写隐藏的水尺刻度。",
                    en+"\n\nThe upper line is visible: copy the recovered scale.",
                    C("誊写涨潮证据","Copy high-tide marks","floodarchive_1"));
                return true;
            }
            if(stage==2)
            {
                string zh="摆渡名单藏在水下。只有**退潮**露出木板时才能读到被抹去的家庭姓名。";
                string en="The ferry roster lies submerged. Only LOW tide reveals the names of the erased families.";
                if(!LowTide)
                {
                    float wait=FloodArchiveWait(2);
                    FloodArchiveDialog(site,zh+"\n\n高潮仍在，约 "+wait.ToString("0")+" 秒后再来。",
                        en+"\n\nHIGH tide persists. Return in about "+wait.ToString("0")+" seconds.",
                        C("等待退潮","Wait for ebb","close"));
                }
                else FloodArchiveDialog(site,zh+"\n\n此时木板露出水面，可以抄录真实姓名。",
                    en+"\n\nThe planks have emerged. Recover the real names.",
                    C("抄录摆渡名册","Recover the ferry roster","floodarchive_2"));
                return true;
            }
            FloodArchiveDialog(site,
                "两份原始记录现在一致：离城者并非私运商人，而是被遗忘的平民。你要怎样修复这里的公共渡口？",
                "The two records agree: those who fled were uncounted civilians, not smugglers. How should this public crossing serve them?",
                C("公开名单 · 救治伤者","Publish names · tend the wounded","floodarchive_public"),
                C("保留水图 · 培训摆渡人","Preserve charts · train pilots","floodarchive_chart"));
            return true;
        }
        private bool SafeRegionalLanding(Point destination)
        {
            if(!CanStand(destination))return false;
            foreach(Enemy enemy in enemies)
                if(enemy.hp>0&&(enemy.Position-destination).Length<6f)return false;
            foreach(Projectile shot in projectiles)
                if(!shot.friendly&&shot.life>0&&shot.damage>0&&
                    (shot.position-destination).Length<6f)return false;
            foreach(Hazard warning in hazards)
                if(warning.damage>0&&
                    (warning.position-destination).Length<4f+Math.Max(0,warning.radius))
                    return false;
            return true;
        }
        // A cached dialogue button must revalidate physical dock
        // access. Nearby does not mean reachable if a wall closes after
        // the menu opens; preserve the same near-side interaction margin.
        private bool AtFloodArchivePost(WorldObject site)
        { return site!=null&&PhysicalRefugeReach(
            new Point(site.x,site.y),1.8001f); }
        private bool ChooseFloodArchiveFerry()
        {
            if(!FloodArchiveComplete||save.room!=2||Dying||practicing||racing)return true;
            WorldObject from=null,to=null;
            WorldObject western=FloodArchiveObject(1),eastern=FloodArchiveObject(3);
            if(western==null||eastern==null)return true;
            if(AtFloodArchivePost(western))
            {from=western;to=eastern;}
            else if(AtFloodArchivePost(eastern))
            {from=eastern;to=western;}
            if(from==null)return true;
            if(!LowTide)
            {
                Notify("水位还未退去，公共渡船暂不通航。",
                    "The ferry cannot launch until low tide.");
                return true;
            }
            // Respect all current physical mission locks, actual threats at
            // BOTH banks and real resource costs. This never resets a room.
            if((FieldWorkPreventsRest&&!floodReliefActive)||!SafeTravel||
                !SafeRegionalLanding(new Point(to.x,to.y))||
                (floodReliefActive&&(from!=western||floodReliefCrossed)))
            {
                Notify("渡口受到威胁，或仍有现场任务。请先确保两岸安全。",
                    "The crossing is unsafe or fieldwork remains active. Secure both banks.");
                return true;
            }
            if(save.energy<PublicFerryEnergyCost)
            {
                Notify("安全渡河需要"+PublicFerryEnergyCost+"耐力。",
                    "Crossing requires "+PublicFerryEnergyCost+" energy.");
                return true;
            }
            // The old code charged ferry energy and moved the parcel
            // BEFORE checking whether two reachable east-bank raiders
            // could exist. A blocked shoreline then silently canceled the
            // entire 65-second mission while leaving the player stranded
            // on the far bank. Plan the full encounter before either
            // resource or travel transaction; never commit half a pair.
            Point[] crossingRaiders=null;
            if(floodReliefActive&&from==western&&
                !TryFloodReliefRaiderSpots(new Point(to.x,to.y),
                    out crossingRaiders))
            {
                Notify("东岸拦截战没有安全落点。仍在西岸，未消耗耐力；调整战场后可继续护送。",
                    "No safe east-bank ambush feet. The ferry remains west without spending energy; clear the shore and retry.");
                return true;
            }
            save.energy-=PublicFerryEnergyCost;save.x=to.x;save.y=to.y;
            revision++;sound="travel";
            if(floodReliefActive&&from==western)
            {
                floodReliefCrossed=true;
                SpawnFloodReliefRaiders(crossingRaiders);
                Notify("真实拦截者登岸！护住医疗包并击退两人，再把药品送到东岸。",
                    "Two real raiders ambush the medicine! Defeat them and deliver at the eastern dock.");
            }
            else Notify("低潮渡船把你安全带到了河对岸。","The low-tide public ferry brought you to the opposite bank.");
            return true;
        }
        private bool ChooseFloodArchive(string action)
        {
            if(!action.StartsWith("floodarchive_",StringComparison.Ordinal))return false;
            if(action=="floodarchive_ferry")return ChooseFloodArchiveFerry();
            if(action=="floodarchive_relief_start")
            {
                WorldObject west=FloodArchiveObject(1);
                if(!FloodArchiveComplete||FloodReliefComplete||save.room!=2||
                    floodReliefActive||Dying||practicing||!LowTide||
                    FieldWorkPreventsRest||!SafeTravel||west==null||
                    !AtFloodArchivePost(west))return true;
                floodReliefActive=true;floodReliefCrossed=false;
                floodReliefPatched=floodReliefWarded=false;
                floodReliefTime=65;floodReliefIntegrity=100;floodReliefWardTime=0;
                floodReliefConfirmedKills=0;
                TrackQuest("ferry_relief");revision++;sound="quest";
                Notify("紧急药品已领取！65秒内渡河、击败两名拦截者并向东岸交付。",
                    "Urgent medicine aboard! Cross, defeat both raiders and deliver on the east bank within 65 seconds.");
                return true;
            }
            if(action=="floodarchive_relief_abort"||
                action=="floodarchive_relief_abort_east")
            {
                bool eastern=action=="floodarchive_relief_abort_east";
                WorldObject dock=FloodArchiveObject(eastern?3:1);
                if(floodReliefActive&&dock!=null&&save.room==2&&
                    floodReliefCrossed==eastern&&
                    AtFloodArchivePost(dock))
                {
                    ResetFloodRelief();revision++;
                    Notify(eastern?
                        "急救包留在东岸居民处，本次护送已撤销。可返回西岸重新组织。":
                        "医疗护送已取消，可在下一次低潮免费重试。",
                        eastern?
                        "Medicine left with eastern residents. Escort canceled; regroup at the west dock.":
                        "Medical crossing canceled. Retry at a later ebb tide.");
                }
                return true;
            }
            if(action=="floodarchive_relief_ward")
            {
                WorldObject east=FloodArchiveObject(3);
                if(!FloodReliefCrossed||floodReliefWarded||east==null||
                    save.room!=2||Dying||practicing||
                    !AtFloodArchivePost(east))return true;
                if(save.energy<20)
                {
                    Notify("居民防护法阵需要20耐力。仍可通过躲避保住药品。",
                        "The civilian cargo ward needs 20 energy. Dodging remains free.");
                    return true;
                }
                save.energy-=20;floodReliefWarded=true;floodReliefWardTime=18f;
                revision++;sound="guard";
                effects.Add(new Effect("discover",Position,.5f));
                Notify("已启用18秒药品护盾：真正命中仍伤害主角，但药品只承受45%冲击。",
                    "18-second parcel ward active: real attacks still hurt the hero, but cargo takes only 45% impact.");
                return true;
            }
            if(action=="floodarchive_relief_patch")
            {
                WorldObject east=FloodArchiveObject(3);
                if(!FloodReliefCrossed||east==null||save.room!=2||
                    Dying||practicing||floodReliefPatched||
                    floodReliefIntegrity<=0||floodReliefIntegrity>=100||
                    !AtFloodArchivePost(east))return true;
                if(save.inventory[2]<1)
                {
                    Notify("现场修补需要一份实际草药。仍可躲避追兵并完成交付。",
                        "Field repair requires one real herb. You can still evade raiders and finish delivery.");
                    return true;
                }
                save.inventory[2]--;floodReliefPatched=true;
                floodReliefIntegrity=Math.Min(100,floodReliefIntegrity+40);
                revision++;sound="reward";
                Notify("医疗包封条已恢复最多40点完整度。本次护送不能再次修补，倒计时仍在继续。",
                    "Medical seal restored by up to 40 integrity. One repair per attempt; the original clock continues.");
                return true;
            }
            if(action=="floodarchive_relief_deliver")
            {
                WorldObject east=FloodArchiveObject(3);
                if(!FloodReliefActive||!floodReliefCrossed||
                    floodReliefConfirmedKills!=3||floodReliefIntegrity<=0||
                    east==null||save.room!=2||Dying||practicing||
                    !AtFloodArchivePost(east))return true;
                ResetFloodRelief();Flag("floodarchive_relief");
                CreditCoins(45);CreditShards(1);AddXp(65);
                revision++;sound="reward";
                RecordMoment("沉桥渡口的药品在真实的低潮航程和两名拦截者的战斗后送抵东岸居民手中。",
                    "Emergency medicine reached the east shore after a real low-tide crossing and two confirmed combat victories.");
                Notify("医疗护送成功：硬币+45、碎片+1、经验+65！安全渡船永久降至10耐力。",
                    "Medical relief delivered! +45 coins, +1 shard, +65 XP; the secured ferry now costs 10 energy.");
                return true;
            }
            if(action!="floodarchive_1"&&action!="floodarchive_2"&&
                action!="floodarchive_public"&&action!="floodarchive_chart")return true;
            if(FloodArchiveComplete||save.room!=2||Dying||practicing||!Has("scout_saved"))return true;
            int stage=action=="floodarchive_1"?1:action=="floodarchive_2"?2:3;
            WorldObject site=FloodArchiveObject(stage);
            // Evidence and public-hearing rewards must use the same
            // reachable bank-side contact as the ferry. An already opened
            // dialogue is not authority after terrain closes the path.
            if(!AtFloodArchivePost(site)||FloodArchiveProgress+1!=stage)
                return true;
            if(stage==1&&LowTide||stage==2&&!LowTide)
            {
                Notify("潮位已改变，需在正确的水位才能读取档案。",
                    "The tide changed. Return at the correct level to read this evidence.");
                return true;
            }
            if(stage<=2)
            {
                Flag("floodarchive_"+stage);AddXp(12);revision++;sound="reward";
                Notify("找到第 "+stage+"/2 份被抹去的民用记录，经验 +12。",
                    "Recovered civilian record "+stage+"/2. +12 XP.");
                return true;
            }
            Flag(action);
            CreditCoins(50); CreditShards(2); AddXp(80);
            revision++;sound="reward";
            RecordMoment(action=="floodarchive_public"?
                "沉桥上从未登记的家庭终于写入公共名单，摆渡人在潮标旁照顾伤者。":
                "沉桥的原始水图留给了渡船学徒，他们从此可以安全辨识湍流与航道。",
                action=="floodarchive_public"?
                "The unregistered families were placed on public rolls, with ferrymen tending the wounded.":
                "The preserved original tide charts taught new ferry pilots to read dangerous currents.");
            Notify("公共渡口重新启用！硬币 +50、碎片 +2、经验 +80。选择将持续影响当地安全补给。",
                "Public ferry restored! +50 coins, +2 shards, +80 XP. Your decision now changes local safe provisions.");
            return true;
        }
        // The existing local ferryman reacts to the same actual
        // player-published records. Pure dialogue, no extra quest/reward.
        private string FloodArchiveWayfarerNote(bool english)
        {
            if(save.room!=2)return "";
            if(FloodArchiveChoice==1)
                return english?
                    "\n\nThe families are no longer missing from the ferry register. I can finally put names beside those we treated at the crossing.":
                    "\n\n这些家庭终于不再从渡口名册上消失。我可以在救治记录旁写下真实姓名了。";
            if(FloodArchiveChoice==2)
                return english?
                    "\n\nWith your preserved flood charts, we now teach apprentices to read the current rather than trust the citadel's false cargo timetable.":
                    "\n\n你保住的原始水图已经交到学徒手里。我们现在看水流，而不再相信堡垒失真的货运时刻。";
            if(FloodArchiveProgress==2)
                return english?
                    "\n\nYou found the lost families on both tide records. Take the evidence to the public desk; their fate still needs a decision.":
                    "\n\n你从两份潮汐记录里找到了失踪家庭。请带去公共议事台，还得有人决定怎样帮助他们。";
            if(FloodArchiveProgress==1)
                return english?
                    "\n\nThe high-water gauge confirms a rescue boat passed here. At the next ebb, the missing roster may surface.":
                    "\n\n高水尺证实这里曾有救援小艇。等下一次退潮，沉下去的名册应该就能露出。";
            return "";
        }
        public bool FloodArchiveRefuge
        {
            get
            {
                if(!FloodArchiveComplete||save.room!=2)return false;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="floodarchive"&&
                        PhysicalRefugeReach(new Point(o.x,o.y),3f))return true;
                return false;
            }
        }
    }
}
