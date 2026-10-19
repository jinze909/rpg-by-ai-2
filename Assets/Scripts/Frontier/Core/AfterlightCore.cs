using System;
using System.Collections.Generic;
namespace Starfall
{
    // Chapter Five: three original regions visibly rebuilt AFTER the original
    // main ending. Permanent state uses only version-2 string flags.
    // Each repair is a physical two-site load transfer, not a free flag.
    public sealed partial class GameCore
    {
        private int afterlightChargeRegion;
        private float afterlightChargeLeft,afterlightChargeIntegrity;
        private bool afterlightStableCharge,afterlightReceiverRepaired;
        // A low-power receiver needs a viable pulse, not a 1% remnant.
        public const float AfterlightReceiverThreshold=35f;
        public bool AfterlightReceiverRepairAvailable
        {get{return afterlightChargeRegion!=0&&!afterlightReceiverRepaired&&
            afterlightChargeIntegrity>0&&
            afterlightChargeIntegrity<AfterlightReceiverThreshold&&
            afterlightChargeLeft>8&&save.inventory[2]>=1;}}
        public float AfterlightChargeIntegrity
        { get {return afterlightChargeRegion!=0?afterlightChargeIntegrity:0;} }
        public bool AfterlightStableCharge { get {return afterlightChargeRegion!=0&&afterlightStableCharge;} }
        public int AfterlightChargeRegion { get { return afterlightChargeRegion; } }
        public float AfterlightChargeLeft { get { return afterlightChargeLeft; } }
        public int AfterlightRestored
        {
            get { int n=0;for(int i=1;i<=3;i++)if(Has("afterlight_done_"+i))n++;return n; }
        }
        public bool AfterlightComplete { get { return Has("afterlight_complete"); } }
        // Optional earned improvements retain the established SaveData v2 flag schema.
        public bool AfterlightReserve(int region)
        { return region>=1&&region<=3&&Has("afterlight_reserve_"+region); }
        public int AfterlightReserves
        { get { int n=0;for(int r=1;r<=3;r++)if(AfterlightReserve(r))n++;return n; } }
        // Two-wave public-defense drills have one earned clearance per region,
        // but trial foes, clock and wave IDs must NEVER enter v2 saved state.
        private int afterlightWatchRoom,afterlightWatchWave,afterlightWatchA,afterlightWatchB;
        private bool afterlightWatchADead,afterlightWatchBDead,afterlightWatchPatched;
        // After authentic wave-one kills a crowded receiver must defer
        // reinforcements, not erase the player's already-won defense.
        private bool afterlightWatchWaveDeferred;
        private float afterlightWatchTime,afterlightWatchStock;
        public float AfterlightWatchStock
        {get{return AfterlightWatchActive?afterlightWatchStock:0f;}}
        public bool AfterlightWatchPatchAvailable
        {get{return AfterlightWatchActive&&!afterlightWatchPatched&&
            afterlightWatchStock>0&&afterlightWatchStock<100f&&
            save.inventory[0]>=1&&save.inventory[2]>=1;}}
        public bool AfterlightWatchActive {get{return afterlightWatchRoom>=1&&afterlightWatchRoom==save.room;}}
        public int AfterlightWatchWave {get{return AfterlightWatchActive?afterlightWatchWave:0;}}
        public float AfterlightWatchTime {get{return AfterlightWatchActive?afterlightWatchTime:0;}}
        public bool AfterlightWatchDone(int room)
        {return room>=1&&room<=3&&Has("afterlight_watch_done_"+room);}
        public int AfterlightWatchesCleared
        {get{int n=0;for(int i=1;i<=3;i++)if(AfterlightWatchDone(i))n++;return n;}}

        private bool IsAfterlightWatchEnemy(Enemy e)
        {return e!=null&&AfterlightWatchActive&&(e.id==afterlightWatchA||e.id==afterlightWatchB);}
        private void OnAfterlightWatchEnemyDefeated(Enemy e)
        {
            if(!IsAfterlightWatchEnemy(e))return;
            if(e.id==afterlightWatchA)afterlightWatchADead=true;
            if(e.id==afterlightWatchB)afterlightWatchBDead=true;
        }
        private void ResetAfterlightWatch()
        {
            // Retire ONLY attackers owned by this local drill; ordinary patrols,
            // boss state, permanent repairs and unrelated warnings survive.
            if(afterlightWatchRoom!=0)
            {
                for(int i=enemies.Count-1;i>=0;i--)
                    if(enemies[i].id==afterlightWatchA||enemies[i].id==afterlightWatchB)
                    { RemoveEnemyPendingWarning(enemies[i]);enemies.RemoveAt(i); }
                // Even arrows released before an abort are part of this duel,
                // and may otherwise hit the hero AFTER the trial disappears.
                // Keep all unrelated hostile shots and field mission visuals.
                projectiles.RemoveAll(p=>p.afterlightWatchOwned);
                effects.RemoveAll(e=>e.afterlightWatchOwned);
            }
            afterlightWatchRoom=afterlightWatchWave=afterlightWatchA=afterlightWatchB=0;
            afterlightWatchADead=afterlightWatchBDead=afterlightWatchPatched=false;
            afterlightWatchWaveDeferred=false;
            afterlightWatchTime=afterlightWatchStock=0;
        }
        // Siege raiders damage an actual local facility at the end of their
        // existing windup, not on spawn, cosmetic warning or forged actions.
        // Nearby bodyguarding redirects their aggression toward the hero.
        private void DamageAfterlightWatchStock(Enemy attacker)
        {
            if(!AfterlightWatchActive||attacker==null||!IsAfterlightWatchEnemy(attacker)||
                !attacker.targetingAfterlightStock||attacker.hp<=0)return;
            Point station=AfterlightPoint(afterlightWatchRoom,"outlet");
            if((attacker.Position-station).Length>2.35f||
                !ClearAttackPath(attacker.Position,station))return;
            float damage=attacker.kind==2?24f:14f;
            afterlightWatchStock=Math.Max(0,afterlightWatchStock-damage);
            revision++;sound="hit";
            effects.Add(new Effect("number",station,.75f,
                L("储备受损 -"+(int)damage,"STOCK -"+(int)damage))
                {afterlightWatchOwned=true});
            if(afterlightWatchStock<=0)
            {
                ResetAfterlightWatch();revision++;
                Notify("接收站应急储备被袭击者击溃！居民设施与既有成果未丢失；可以重新组织防卫。",
                    "Raiders broke the local reserve defense! Permanent public infrastructure remains. You may retry.");
            }
            else Notify("袭击者破坏了储备！接收站剩余耐久 "+Math.Ceiling(afterlightWatchStock)+"%。",
                "Raiders damaged the reserve! Receiver integrity "+Math.Ceiling(afterlightWatchStock)+"%.");
        }
        private bool AfterlightWatchSpawnPoint(int ordinal,int wave,Point avoid,out Point spot)
        {
            Point center=AfterlightPoint(save.room,"outlet");
            Point[] offsets={
                new Point(-3f,0),new Point(3f,0),new Point(0,-3f),new Point(0,3f),
                new Point(-3f,-2f),new Point(3f,-2f),new Point(-3f,2f),new Point(3f,2f),
                new Point(-4f,1f),new Point(4f,1f),new Point(1f,-4f),new Point(1f,4f),
                new Point(-2f,-4f),new Point(2f,4f),new Point(4f,-2f),new Point(-4f,2f)};
            for(int i=0;i<offsets.Length;i++)
            {
                int slot=(i+ordinal*5+(wave-1)*3)%offsets.Length;
                Point p=center+offsets[slot];
                if(!CanStand(p)||(p-Position).Length<=1.7f ||
                    (p-avoid).Length<=2f ||
                    (save.companion>=0 &&
                     (p-companionPosition).Length<1.55f))
                    continue;
                // Partial NavigationPath fallbacks may stop at a shoreline
                // frontier without ever reaching the actual receiver.
                // Require the full enemy-foot graph to connect to this
                // authored defense objective, while permitting real turns
                // around near-side walls.
                if(!ActorHasLocalRoute(p,center,9))continue;
                bool occupied=false;
                foreach(Enemy living in enemies)
                    if(living.hp>0&&(p-living.Position).Length<1.55f)
                    {occupied=true;break;}
                if(occupied)continue;
                spot=p;return true;
            }
            spot=new Point(-100,-100);return false;
        }
        private bool SpawnAfterlightWatchWave(int wave)
        {
            Point first,second;
            if(!AfterlightWatchSpawnPoint(0,wave,new Point(-100,-100),out first)||
                !AfterlightWatchSpawnPoint(1,wave,first,out second))return false;
            int region=afterlightWatchRoom;
            // Regional threats differ tactically: forest flank + bow;
            // ruined quay archers; mine armored raiders. Wave two advances
            // to armored pressure instead of spawning cosmetic substitutes.
            // Existing production AI, stagger, attacks and telegraphs apply.
            int kindA=wave==1?(region==1?0:region==2?1:2):2;
            int kindB=wave==1?(region==1?1:0):(region==1?0:1);
            Enemy a=CreatePlacedEnemy(++enemyId,kindA,first,
                44+region*10+(wave-1)*22);
            Enemy b=CreatePlacedEnemy(++enemyId,kindB,second,
                42+region*10+(wave-1)*18);
            afterlightWatchA=a.id;afterlightWatchB=b.id;
            afterlightWatchADead=afterlightWatchBDead=false;
            afterlightWatchWaveDeferred=false;
            afterlightWatchWave=wave;
            enemies.Add(a);enemies.Add(b);
            revision++;sound="quest";
            Notify("公共设施防卫 第"+wave+"/2波：真正击退两名袭击者！",
                "Civic defense wave "+wave+"/2: defeat both actual attackers.");
            return true;
        }
        private bool ChooseAfterlightWatch(string action)
        {
            bool begin=action.StartsWith("afterlight_watch_start_");
            bool abort=action.StartsWith("afterlight_watch_abort_");
            bool patch=action.StartsWith("afterlight_watch_patch_");
            if(!begin&&!abort&&!patch)return false;
            int room;
            string prefix=begin?"afterlight_watch_start_":
                abort?"afterlight_watch_abort_":"afterlight_watch_patch_";
            if(!int.TryParse(action.Substring(prefix.Length),out room)||
                room<1||room>3||save.room!=room||
                // A wall may close AFTER a watch/patch/withdraw
                // dialogue is opened. Commit only when the same
                // physical receiver is still reachable from this foot.
                !PhysicalRefugeReach(AfterlightPoint(room,"outlet"),
                    2.2001f))return true;
            if(abort)
            {
                if(AfterlightWatchActive)
                {
                    ResetAfterlightWatch();revision++;
                    Notify("本次公共防卫已安全终止，已建立的储备不受影响。",
                        "Civic defense withdrawn. All permanent emergency stocks remain.");
                }
                return true;
            }
            if(patch)
            {
                if(!AfterlightWatchActive||afterlightWatchPatched||
                    afterlightWatchStock<=0||afterlightWatchStock>=100||
                    Dying||practicing||racing)return true;
                if(!Spend(1,0,1,0,0))return true;
                afterlightWatchStock=Math.Min(100f,afterlightWatchStock+30f);
                afterlightWatchPatched=true;revision++;sound="reward";
                Notify("临时抢修完成：木材 -1、月光草 -1；储备恢复30点（每次防卫仅限一次）。",
                    "Emergency patch: -1 timber, -1 Moonherb; restore 30 stock integrity (once per defense).");
                return true;
            }
            if(AfterlightWatchActive||!AfterlightComplete||
                !AfterlightReserve(room)||!Has("afterlight_steward_"+room)||
                Dying||practicing||racing||save.health<=15)return true;
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("先结束其它现场任务并清理真正的追击者，再开启公共防卫。",
                    "Finish the other live field mission and clear nearby attackers before starting this civic defense.");
                return true;
            }
            afterlightWatchRoom=room;afterlightWatchTime=100f;
            afterlightWatchStock=100f;
            if(!SpawnAfterlightWatchWave(1))
            {
                ResetAfterlightWatch();
                Notify("该设施周围暂无可站立的防卫位置，请稍后再试。",
                    "No safe footing for this civic defense. Retry later.");
            }
            return true;
        }
        private void TickAfterlightWatch(float dt)
        {
            if(!AfterlightWatchActive)return;
            int room=afterlightWatchRoom;
            if(Dying||practicing||save.room!=room||
                (Position-AfterlightPoint(room,"outlet")).Length>11.5f)
            {
                ResetAfterlightWatch();revision++;
                Notify("离开防区：演练中止，永久储备与已赢得的记录保留。",
                    "You left the defense perimeter. Permanent stocks and cleared records remain.");
                return;
            }
            afterlightWatchTime-=dt;
            if(afterlightWatchTime<=0)
            {
                ResetAfterlightWatch();revision++;
                Notify("防卫时限结束，袭击者已退散。本次未获得胜利记录。",
                    "Defense timed out; attackers dispersed. No victory credit awarded.");
                return;
            }
            bool presentA=enemies.Exists(e=>e.id==afterlightWatchA);
            bool presentB=enemies.Exists(e=>e.id==afterlightWatchB);
            if((!presentA&&!afterlightWatchADead)||(!presentB&&!afterlightWatchBDead))
            {
                ResetAfterlightWatch();revision++;
                Notify("袭击者被异常移除，防卫中断；不能以清空列表伪造胜利。",
                    "Defense interrupted by a missing attacker. Only actual lethal contacts count.");
                return;
            }
            if(!afterlightWatchADead||!afterlightWatchBDead||presentA||presentB)return;
            if(afterlightWatchWave==1)
            {
                if(!SpawnAfterlightWatchWave(2))
                {
                    // Keep wave-one lethal acknowledgements and remaining
                    // watch time. Only two available physical feet can
                    // advance this scene; repeat the attempt each world
                    // tick until space opens or the original clock expires.
                    if(!afterlightWatchWaveDeferred)
                    {
                        afterlightWatchWaveDeferred=true;revision++;
                        Notify("增援暂时缺少安全落点。第一波战果已保留，清出战场后继续防卫。",
                            "No safe foothold for reinforcements yet. Wave-one victories are retained; clear the arena to continue.");
                    }
                }
                return;
            }
            ResetAfterlightWatch();
            if(!AfterlightWatchDone(room))
            {
                Flag("afterlight_watch_done_"+room);
                CreditCoins(25+room*5);CreditShards(1);AddXp(35);
                if(AfterlightWatchesCleared==3&&!Has("afterlight_watch_all"))
                {Flag("afterlight_watch_all");CreditSkillPoints(1);}
                RecordMoment(AfterlightLocation(room,false)+"的居民应急储备通过两波真实袭击检验，巡护人员开始定期守护公共设施。",
                    "The civic reserve at "+AfterlightLocation(room,true)+
                    " survived two real attacker waves; public wardens now protect it.");
                Notify("公共防卫首通：硬币、碎片与经验已发放（"+AfterlightWatchesCleared+"/3）。",
                    "First civic defense clear: coins, a shard and XP awarded ("+AfterlightWatchesCleared+"/3).");
            }
            else Notify("公共防卫复练通过！永久储备仍在；本次不重复发奖。",
                "Civic defense replay cleared! Permanent stocks remain; no repeat rewards.");
            revision++;sound="reward";
        }
        public int AfterlightStewards
        {
            get { int n=0;for(int i=1;i<=3;i++)if(Has("afterlight_steward_"+i))n++;return n; }
        }
        public int AfterlightMode(int region)
        { return Has("afterlight_plan_"+region+"_care")?1:Has("afterlight_plan_"+region+"_watch")?2:0; }
        private Point AfterlightPoint(int region,string type)
        { return ObjectPoint(region,"afterlight"+type+region,new Point(-100,-100)); }
        private void ResetAfterlightCharge()
        { afterlightChargeRegion=0;afterlightChargeLeft=afterlightChargeIntegrity=0;
          afterlightStableCharge=afterlightReceiverRepaired=false; }
        private void DamageAfterlightCharge(float damage,string source)
        {
            if(afterlightChargeRegion==0||source=="environment"||damage<=0)return;
            float shock=Math.Max(10,Math.Min(30,damage*.9f));
            if(afterlightStableCharge)shock*=.5f;
            afterlightChargeIntegrity=Math.Max(0,afterlightChargeIntegrity-shock);
            if(afterlightChargeIntegrity<=0)
            {
                ResetAfterlightCharge();revision++;
                Notify("传送的余光在真实袭击中完全散失！返回原锚点重新启动。",
                    "Real combat destroyed the carried civic charge! Restart at the source anchor.");
            }
            else Notify("负载在袭击中受损：保持生命和载荷安全，再到接收台安装。",
                "The carried power was damaged by combat; protect it and reach the receiver.");
        }
        private string AfterlightLocation(int region,bool english)
        {
            string[] zh={"","荆棘密林 · 根苗圃","沉没遗迹 · 开放渡口","赤铁矿场 · 安全工房"};
            string[] en={"","Thornwood · Root Nursery","Drowned Ruins · Open Crossing","Ironreach · Safe Foundry"};
            return english?en[region]:zh[region];
        }
        private bool AfterlightEligible {get{return save.ending>=1 && save.ending<=3;}}
        private string AfterlightOutcomeZh(int region)
        {
            int mode=AfterlightMode(region);
            if(region==1)return mode==1?
                "被砍断的根系终于接回疗养苗圃。这里能在战斗结束后为行路者恢复生命。":
                "旧林道改成夜间防护灯线。庇护灯为经过者补充耐力，但不会强迫根系继续输送给堡垒。";
            if(region==2)return mode==1?
                "渡口恢复向行人开放。靠近旧闸门时，可在无敌人追击的情况下搭乘小舟穿过水道。":
                "遗迹的旧水道被划为公共避险区。渡口仍属于每个人，桥畔的疗养灯会在非战斗时恢复生命。";
            return mode==1?
                "矿工掌握了维修台的零件回收配方。使用真实材料，将一部分多余星晶变成修桥所需的零件。":
                "停机避险条例写入工房控制器。这里的公共冷却灯会在安全时为行路者回复生命。";
        }
        private string AfterlightOutcomeEn(int region)
        {
            int mode=AfterlightMode(region);
            if(region==1)return mode==1?
                "The severed roots are rejoined at a public nursery. It now heals travelers outside combat.":
                "Old forest roads carry protective lamps. The light recharges travelers' energy without drawing from the citadel.";
            if(region==2)return mode==1?
                "The crossing is open to travelers. Outside combat, the public boat carries anyone between both old sluice terminals.":
                "The ruins now hold a publicly accessible refuge. Lamps heal people on the banks while they rest safely.";
            return mode==1?
                "Workers hold the repair recipe. Spend real timber and crystal at this workbench to craft replacement machine parts.":
                "Workers now control the emergency shutdown. The public cooling lamps restore health only when the machinery is safe.";
        }
        private bool TalkAfterlight(WorldObject o)
        {
            string kind=o.kind;
            bool board=kind=="afterlightcouncil";
            int room=save.room;
            bool waypoint=room==0 && kind.StartsWith("afterlightwaypoint");
            if(waypoint)
            {
                int region;
                if(!int.TryParse(kind.Substring("afterlightwaypoint".Length),out region) ||
                    region<1||region>3 || !Has("afterlight_done_"+region) ||
                    !Has("afterlight_steward_"+region))return true;
                Dialog(o.name,o.en,
                    "这是居民亲自签署维护责任后，余烬镇新增的公共星灯线路。\n\n"+
                    "线路已经直连"+AfterlightLocation(region,false)+"的修好设施。附近没有敌人时才能乘坐；它不会替你完成尚未接通的其它地区主线。",
                    "This public starroad opened only after the actual regional steward signed the maintenance record.\n\n"+
                    "It leads directly to the repaired receiver at "+AfterlightLocation(region,true)+". Nearby combat blocks travel; other unfinished story regions remain unfinished.",
                    C("沿公共星灯抵达修好的设施","Travel directly to the repaired site","afterlight_route_"+region),
                    C("先不出发","Stay","close"));return true;
            }
            // Reuse the EXISTING living characters of Acts I–III rather than
            // cloning them as new NPCs or bypassing their signed commitments.
            if(room>=1&&room<=3 && kind=="witnessnpc"+room &&
                AfterlightEligible && Has("afterlight_started") &&
                Has("afterlight_done_"+room) && !Has("afterlight_steward_"+room))
            {
                string[] whoZh={"","莫芽 · 密林巡路员","阿菱 · 遗迹渡口幸存者","维恩 · 赤铁矿工"};
                string[] whoEn={"","Moya · Grove Roadwarden","Arin · Sluice Survivor","Venn · Ironreach Miner"};
                string[] pledgeZh={"",
                    "莫芽：我会让受伤的根系与巡路灯的维修计划都公开。以后每棵树能不能恢复生长，由守护森林的人一起讨论，不再由军用货车决定。",
                    "阿菱：渡口的名单要记下行人、孩子、船夫，还有那些没钱交过桥费的人。这座设施的维护日志我来签。",
                    "维恩：机器维修应当轮班，过热就停，谁也不许把工人的撤离写成偷懒。我要把这条写入每一次交接清单。"};
                string[] pledgeEn={"",
                    "Moya: We will publish repair logs for the wounded roots and road lamps. Those who live with this forest must decide how it heals, not military ore carts.",
                    "Arin: The crossing ledger must name travelers, children, boatkeepers, even those who cannot pay. I will sign its maintenance record.",
                    "Venn: Machines require safe shifts. When they overheat, work stops, and evacuation is not laziness. I will put that into every handover."};
                Dialog(whoZh[room],whoEn[room],
                    pledgeZh[room]+"\n\n真正可用的公共设施需要有人署名维护。若我签了字，公会可以公开我的责任，而不是只在战后庆典里把我算作一个数字。",
                    pledgeEn[room]+"\n\nA working public service needs a named steward. If I sign, the guild can publish my responsibilities rather than counting me as another number at the victory ceremony.",
                    C("请居民亲自签下维护承诺","Have the resident sign stewardship","afterlight_steward_"+room),
                    C("再考虑一下","Not yet","close"));
                return true;
            }
            bool anchor=room>=1&&room<=3&&kind=="afterlightanchor"+room;
            bool outlet=room>=1&&room<=3&&kind=="afterlightoutlet"+room;
            if(!board&&!anchor&&!outlet)return false;
            if(board)
            {
                if(!AfterlightEligible)
                {
                    Dialog(o.name,o.en,
                        "公会正在讨论战后安排，但厄洛与星核的去向尚未决定。地图上的重建计划会在你真正完成主线后开放。",
                        "The guild has plans for reconstruction, but Orrin and the starcores have not yet been resolved. This project opens only after a genuine ending.",
                        C("先完成主线","Finish the main story first","close"));return true;
                }
                if(AfterlightComplete)
                {
                    Dialog(o.name,o.en,
                        "重建议事厅的地图不再只有矿脉与军用货路。莫芽、阿菱、维恩与镇民共同维护的三座设施已经开始运行，公会每周公开下一轮修复名单。\n\n"+ 
                        "你的结局没有让所有损失凭空消失。真正改变世界的是有人继续照看这些路。\n\n"+
                        (AfterlightReserves==3?"三地应急储备均已建立，今后的疗养、巡路灯、渡口和工房有了真正的备用物资。":
                        "应急储备已建立 "+AfterlightReserves+"/3。亲自前往三座修复后的地区接收台，用真实材料建立永久性的公共应急储备。")+
                        "\n\n公共防卫首通："+AfterlightWatchesCleared+"/3，三地全胜额外奖励技能点1。",
                        "The new map records living residents instead of mining lines alone. Three repaired public sites now run under Moya, Arin, Venn and their neighbors. The guild publishes each weekly repair roster.\n\n"+
                        "Your ending did not erase every hardship. Continuing to care for the roads is what changed this world.\n\n"+
                        (AfterlightReserves==3?"All three emergency reserves are stocked. Rebuilt public services have dependable supplies.":
                        "Emergency reserves stocked: "+AfterlightReserves+"/3. Visit each rebuilt regional receiver in person and invest real supplies in permanent upgrades.")+
                        "\n\nCivic defenses cleared: "+AfterlightWatchesCleared+"/3; all three first clears award one talent point.",
                        C("回顾旅程","Review the journey","epilogue"),C("继续旅行","Travel on","close"));return true;
                }
                if(!Has("afterlight_started"))
                {
                    string choices=save.ending==1?
                        "你把星核还给了土地，根系开始活过来，但村庄还没有安全的公共线路。":
                        save.ending==2?
                        "你保留了屏障，城里得到保护，城外却必须继续支付能源代价。旧机器需要新的公开约束。":
                        "你选择让居民共同维护星核，然而分享权力意味着要真正修复三条彼此断开的道路。";
                    string choicesEn=save.ending==1?
                        "You returned the cores to the land. The roots are waking, but villages still lack safe public connections.":
                        save.ending==2?
                        "You kept the shield. Citadel children are protected, but outer communities still pay its energy costs. The old machines need public oversight.":
                        "You gave the communities shared stewardship. That promise still demands real work along three broken routes.";
                    Dialog(o.name,o.en,
                        "莉雅：终章并非终点。"+choices+"\n\n"+
                        "森林、遗迹、矿场各有一座旧供能锚点和一座断开的公共接收台。请亲自在各地选择公共用途，启动节点，并在90秒内把仍然稳定的余光带往接收台，使用真实物资接通。每个地区有自己的永久功能；三处完成后回来公布重建名单。",
                        "Lyra: The finale was not the end. "+choicesEn+"\n\n"+
                        "Each of Thornwood, the Ruins and Ironreach has a source anchor and a disconnected public receiver. Choose how the public should use that local power, activate its charge and carry it physically to the receiver in 90 seconds. Commit actual materials there. Each repaired area gains a real permanent service. Return after all three are rebuilt.",
                        C("接下第五章 · 余光重建","Begin Chapter Five · Afterlight Reconstruction","afterlight_begin"),
                        C("先做好准备","Prepare first","close"));return true;
                }
                Dialog(o.name,o.en,
                    "莉雅：已经接通 "+AfterlightRestored+"/3 座公共站。每一座都是由居民签名认可、在地图上真正能使用的设施。"+
                    (AfterlightRestored==3 && AfterlightStewards==3?"\n\n三地设施和三份居民维护签名全部齐备。现在可以正式公布有具体责任人的战后公约。":
                     AfterlightRestored==3?"\n\n三座设施已修复，但仍有"+(3-AfterlightStewards)+"位地区维护者尚未签字。回到原来的三位证人身边，当面确认维护责任。":"\n\n请先到还未修复的地区，再回公会汇报。"),
                    "Lyra: "+AfterlightRestored+"/3 public sites are operational, signed by local residents and usable in the world."+
                    (AfterlightRestored==3 && AfterlightStewards==3?"\n\nAll three services and three signed local maintenance pledges are ready. Publish the charter now.":
                     AfterlightRestored==3?"\n\nAll sites work, but "+(3-AfterlightStewards)+" original regional witnesses still need to sign their maintenance obligations. Return to them in person.":"\n\nFinish the remaining regions before returning for publication."),
                    AfterlightRestored==3 && AfterlightStewards==3?
                        C("公布三地重建公约 · 完成第五章","Publish the three-region charter · Finish Chapter Five","afterlight_publish"):
                        C("追踪战后重建","Track reconstruction","afterlight_track"),
                    C("继续准备","Prepare","close"));
                return true;
            }
            if(!AfterlightEligible)
            {
                Notify("原有星核结局尚未决定，不能提前接通战后供能。",
                    "The starcore ending has not happened yet; reconstruction is not available.");
                return true;
            }
            if(!Has("afterlight_started"))
            {
                Notify("先到余烬镇公会的战后议事台，正式接下第五章。",
                    "First accept Chapter Five at the Emberfall reconstruction council.");
                return true;
            }
            if(Has("afterlight_done_"+room))
            {
                string descriptionZh=AfterlightOutcomeZh(room);
                string descriptionEn=AfterlightOutcomeEn(room);
                var options=new List<Choice>();
                if(room==2 && AfterlightMode(2)==1)
                {
                    if(outlet)
                    {
                        descriptionZh+="\n\n公用渡船在无战斗时可前往原水闸另一岸。";
                        descriptionEn+="\n\nThe public ferry travels to the original sluice terminal only outside combat.";
                        options.Add(C("搭乘公共渡船","Ride public ferry","afterlight_ferry_out"));
                    }
                    else
                        options.Add(C("搭乘公共渡船","Ride public ferry","afterlight_ferry_back"));
                }
                if(outlet && room==3 && AfterlightMode(3)==1)
                {
                    int parts=2+(HearingComplete&&HearingVoice(3)==1?1:0)+
                        (AfterlightReserve(3)?1:0)+(AfterlightWatchDone(3)?1:0);
                    descriptionZh+="\n\n真实成本：木材 ×1，星晶 ×"+(StormPolicy(3)==2?1:2)+"；加工零件 ×"+parts+"。";
                    descriptionEn+="\n\nReal cost: 1 timber, "+(StormPolicy(3)==2?1:2)+" starcrystal, gaining "+parts+" machine parts.";
                    options.Add(C("制作可修桥的零件 ×"+parts,"Craft "+parts+" repair parts","afterlight_craft"));
                }
                if(outlet && AfterlightComplete && Has("afterlight_steward_"+room))
                {
                    if(!AfterlightReserve(room))
                    {
                        string costZh=room==1?"木材 ×2、月光草 ×1":
                            room==2?"木材 ×1、月光草 ×2":"星晶 ×2、零件 ×1";
                        string costEn=room==1?"2 timber, 1 Moonherb":
                            room==2?"1 timber, 2 Moonherb":"2 starcrystal, 1 machine part";
                        descriptionZh+="\n\n公约签署后，可再为这座设施建立一次永久应急储备。真实成本："+costZh+"。";
                        descriptionEn+="\n\nAfter the charter, stock a permanent emergency reserve at this receiver. Real cost: "+costEn+".";
                        options.Add(C("投入物资 · 建立本地应急储备",
                            "Invest supplies · Stock local emergency reserve","afterlight_reserve_"+room));
                    }
                    else
                    {
                        descriptionZh+="\n\n本地应急储备已经建立：公共服务效能提升。";
                        descriptionEn+="\n\nThis service has a stocked emergency reserve and a lasting practical improvement.";
                    }
                }
                if(outlet && AfterlightComplete && AfterlightReserve(room))
                {
                    if(AfterlightWatchActive)
                    {
                        descriptionZh+="\n\n公共防卫：第"+AfterlightWatchWave+"/2波，剩余"+Math.Ceiling(AfterlightWatchTime)+
                            "秒；储备耐久 "+Math.Ceiling(AfterlightWatchStock)+"%。近战袭击者会优先靠近并破坏接收站，贴身阻截可吸引攻击。";
                        descriptionEn+="\n\nCivic defense wave "+AfterlightWatchWave+"/2; "+
                            Math.Ceiling(AfterlightWatchTime)+"s left, stock integrity "+
                            Math.Ceiling(AfterlightWatchStock)+"%. Melee raiders attack the receiver unless you intercept them in close combat.";
                        if(afterlightWatchWaveDeferred)
                        {
                            // These actors have already died in authentic
                            // combat: do not tell the player to fight ghosts.
                            // Time and stock are still live; an available
                            // pair of reachable feet starts wave two.
                            descriptionZh+="\n\n第一波已击退！第二波增援等待安全落点；清出接收台周围空间即可继续，原倒计时仍在继续。";
                            descriptionEn+="\n\nFirst wave defeated! Reinforcements await clear, reachable ground near the receiver. Make room to continue; the original countdown is still running.";
                        }
                        if(!afterlightWatchPatched&&afterlightWatchStock<100f)
                            options.Add(C("抢修储备 · 木材1、月光草1、恢复30耐久（每趟一次）",
                                "Patch stock · 1 timber + 1 Moonherb, +30 (once)",
                                "afterlight_watch_patch_"+room));
                        options.Add(C("安全终止本次公共防卫","Withdraw from current civic defense",
                            "afterlight_watch_abort_"+room));
                    }
                    else
                    {
                        descriptionZh+=AfterlightWatchDone(room)?
                            "\n\n巡护已通过实战：本地疗养站额外恢复 1 生命/秒、林灯额外恢复 1.5 耐力/秒，或矿场工房每批多产 1 零件（按当地既有服务适用）。可无奖励复练。":
                            "\n\n应急储备需要通过实战检验：挑战两波实际袭击者，首次胜利计入地区巡护记录，并改善当地公共服务。";
                        descriptionEn+=AfterlightWatchDone(room)?
                            "\n\nDefense passed: +1 HP/s at this region's safe refuge, +1.5 energy/s at the forest lamps, or +1 part per real mine workshop batch, as applicable. Replays carry no extra rewards.":
                            "\n\nDefend the receiver against two real waves for a permanent local service improvement and first-clear credit.";
                        options.Add(C(AfterlightWatchDone(room)?"复练两波防卫 · 无奖励":"守护公共储备 · 两波实战",
                            AfterlightWatchDone(room)?"Replay defense · no rewards":"Defend civic supplies · two waves",
                            "afterlight_watch_start_"+room));
                    }
                }
                if(outlet && Has("afterlight_steward_"+room))
                    options.Add(C("沿公共星灯返回余烬镇","Return to Emberfall via public starroad",
                        "afterlight_return_"+room));
                options.Add(C("继续旅程","Continue","close"));
                Dialog(o.name,o.en,descriptionZh,descriptionEn,options.ToArray());
                return true;
            }
            if(anchor)
            {
                if(AfterlightMode(room)==0)
                {
                    string[] primaryZh={"","为受伤森林恢复活根苗圃","开放所有人的公共渡船","让矿工共享零件回收工房"};
                    string[] primaryEn={"","Restore public healing root nursery","Open the crossing to all travelers","Build a shared recycling workbench"};
                    string[] altZh={"","点亮巡路员使用的夜间林灯","修复应急疗养灯与避险河岸","把矿工的停机条款接入公共冷却灯"};
                    string[] altEn={"","Light the wardens' safe night-road lamps","Restore emergency healing lamps on the river","Wire safety shutdown into the public cooling lamps"};
                    Dialog(o.name,o.en,
                        "旧军令让这座星根锚点只为军用供能。你可以让居民决定它往后服务什么。\n\n"+
                        "两种公共方案都能够运作，但本地区最终建成的服务不同。选择一经确认，会记入战后公约。通电之后才能尝试负载传输。",
                        "The old directive reserved this anchor for military output. Its living neighbors can finally decide what it powers.\n\n"+
                        "Both civic designs function, but offer different permanent local services. Your choice becomes part of the final public charter.",
                        C(primaryZh[room],primaryEn[room],"afterlight_plan_"+room+"_care"),
                        C(altZh[room],altEn[room],"afterlight_plan_"+room+"_watch"),
                        C("再想想","Think about it","close"));return true;
                }
                if(afterlightChargeRegion==room)
                {
                    Dialog(o.name,o.en,
                        "余光仍在传输，剩余 "+Math.Ceiling(afterlightChargeLeft)+" 秒；负载完整度 "+
                        Math.Ceiling(afterlightChargeIntegrity)+"%。遭真实攻击会受损；请沿地图抵达另一端。",
                        "Afterlight load: "+Math.Ceiling(afterlightChargeLeft)+"s remaining, integrity "+
                        Math.Ceiling(afterlightChargeIntegrity)+"%. Real combat hits damage the charge. Travel to the distant receiver.",
                        C("继续送往接收端","Continue to receiver","close"),
                        C("取消本次传输 · 保留方案","Cancel transfer · Keep plan","afterlight_abort"));return true;
                }
                Dialog(o.name,o.en,
                    "原始供能已经校准成居民选择的用途。普通运输免费、限时90秒；也可先花1枚真实星晶稳态供能120秒。稳定所用星晶在启动时扣除，失败或取消不返还；接收端的安装材料仅在成功接通时消耗。",
                    "The civic power is calibrated. Standard transport is free and lasts 90 seconds; invest one real starcrystal for a 120-second stable load. The optional crystal is spent on activation and is not refunded on failure or abort. Installation materials are charged only on a successful connection.",
                    C("激活90秒负载传输 · 免费","Activate 90-second transfer · free","afterlight_charge_"+room),
                    C("稳态120秒传输 · 星晶1","Stabilize 120-second transfer · 1 crystal","afterlight_stable_"+room),
                    C("先准备","Prepare","close"));return true;
            }
            if(afterlightChargeRegion==room)
            {
                var receiverChoices=new List<Choice>();
                receiverChoices.Add(C("安装并接通公共设施",
                    "Install and reconnect public service","afterlight_install_"+room));
                if(afterlightChargeIntegrity<AfterlightReceiverThreshold&&!afterlightReceiverRepaired)
                    receiverChoices.Add(C("现场修复负载 · 月光草1、时间8秒",
                        "Repair power · 1 Moonherb and 8 seconds","afterlight_repair_"+room));
                receiverChoices.Add(C("放弃受损负载 · 原方案保留",
                    "Abandon load · keep civic design","afterlight_discard_"+room));
                receiverChoices.Add(C("保留传输，先备料","Check materials","close"));
                Dialog(o.name,o.en,
                    "余光已抵达接收端，剩余 "+Math.Ceiling(afterlightChargeLeft)+" 秒；完整度 "+
                        Math.Ceiling(afterlightChargeIntegrity)+"%，安装至少需要35%。"+
                    (afterlightChargeIntegrity<AfterlightReceiverThreshold?
                        "\n负载过弱！可消耗月光草1现场恢复45完整度（每趟一次，花费8秒），或在这里主动释放载荷后回锚点重试。":"")+
                    "\n\n接通所需真实材料："+(AfterlightMode(room)==1?"木材 ×2、月光草 ×2":"星晶 ×2、零件 ×1")+
                    "。安装完成后将永久改变这片地区的设施功能。",
                    "Afterlight receiver: "+Math.Ceiling(afterlightChargeLeft)+
                        "s and "+Math.Ceiling(afterlightChargeIntegrity)+"% load integrity. "+
                    "Installation requires at least 35%."+
                    (afterlightChargeIntegrity<AfterlightReceiverThreshold?
                        " The load is too weak. Spend one Moonherb and 8 seconds to restore 45 integrity, or release the cargo here and retry at the source.":"")+
                    "\n\nMaterial cost: "+(AfterlightMode(room)==1?"2 timber and 2 Moonherb":"2 starcrystal and 1 machine part")+
                    ". The service becomes a permanent local facility once built.",
                    receiverChoices.ToArray());return true;
            }
            Dialog(o.name,o.en,
                "这里仍是一座未接电的旧公用终端。先到本地区另一端的星根锚点校准用途，启动真实传输后再回这里。",
                "This civic receiver is disconnected. Choose a public design and activate the original starroot anchor across this region first.",
                C("记住位置","Remember location","close"));return true;
        }
        // Preflight destination geometry without resetting the current world.
        private bool AfterlightDestinationWalkable(int region,Point landing)
        {
            if(region<0||region>=data.maps.Length)return false;
            int current=save.room;
            try { save.room=region; return CanStand(landing); }
            finally { save.room=current; }
        }
        private bool ChooseAfterlight(string action)
        {
            if(action=="afterlight_track"){TrackQuest("afterlight");return true;}
            if(!action.StartsWith("afterlight_"))return false;
            if(action.StartsWith("afterlight_watch_"))return ChooseAfterlightWatch(action);
            if(action=="afterlight_begin")
            {
                if(save.room!=0 || !AfterlightEligible || Has("afterlight_started") ||
                    (Position-ObjectPoint(0,"afterlightcouncil",new Point(-100,-100))).Length>2.2f)return true;
                Flag("afterlight_started");TrackQuest("afterlight");
                RecordMoment("第五章：结局之后，莉雅和居民开始把三处星根锚点改成真正可运行的公共设施。",
                    "Chapter Five: after your ending, Lyra and the townspeople begin rebuilding three working public starroot sites.");
                Notify("第五章开启：三地锚点必须亲自接线，再回公会公开公约。",
                    "Chapter Five begins. Physically reconnect three regions, then publish the civic charter.");
                return true;
            }
            if(action=="afterlight_publish")
            {
                if(save.room!=0 || !Has("afterlight_started") || AfterlightRestored!=3 || AfterlightStewards!=3 ||
                    Has("afterlight_complete") ||
                    (Position-ObjectPoint(0,"afterlightcouncil",new Point(-100,-100))).Length>2.2f)return true;
                Flag("afterlight_complete");
                CreditCoins(120);CreditShards(4);CreditSkillPoints(1);
                AddXp(100);revision++;sound="reward";
                RecordMoment("第五章：三地余光重建公约公开，居民获得可使用的疗养、渡路与安全工房。城墙不再是唯一的地图。",
                    "Chapter Five: the three-region afterlight charter is public. Root care, crossings and safe work now belong to people instead of the wall alone.");
                Notify("第五章完成：120硬币、4碎片、100经验、技能点1。",
                    "Chapter Five complete: 120 coins, 4 shards, 100 XP and one skill point.");
                return true;
            }
            if(action=="afterlight_abort")
            {
                if(afterlightChargeRegion!=0 && save.room==afterlightChargeRegion &&
                    (Position-AfterlightPoint(save.room,"anchor")).Length<2.2f)
                {ResetAfterlightCharge();revision++;Notify("本次传输取消。选择的公共方案仍保留。",
                    "Load transfer canceled. Your chosen civic design remains.");}
                return true;
            }
            if(action=="afterlight_ferry_out" || action=="afterlight_ferry_back")
            {
                if(save.room!=2 || !Has("afterlight_done_2") || AfterlightMode(2)!=1 ||
                    FieldWorkPreventsRest || !SafeTravel || Dying || practicing)return true;
                Point from=AfterlightPoint(2,action.EndsWith("out")?"outlet":"anchor");
                Point to=AfterlightPoint(2,action.EndsWith("out")?"anchor":"outlet");
                if((Position-from).Length>2.2f || !SafeRegionalLanding(to))return true;
                save.x=to.x;save.y=to.y;revision++;sound="travel";
                Notify("公共渡船已抵达对岸。不是公会独占的水路了。",
                    "Public ferry reached the other bank. Passage no longer belongs only to the guild.");
                return true;
            }
            if(action.StartsWith("afterlight_route_") ||
                action.StartsWith("afterlight_return_"))
            {
                bool outbound=action.StartsWith("afterlight_route_");
                int region;
                if(!int.TryParse(action.Substring(outbound?
                    "afterlight_route_".Length:"afterlight_return_".Length),out region) ||
                    region<1||region>3 || !Has("afterlight_started") ||
                    !Has("afterlight_done_"+region) || !Has("afterlight_steward_"+region) ||
                    FieldWorkPreventsRest || !SafeTravel || Dying || practicing ||
                    afterlightChargeRegion!=0)
                    return true;
                Point from=outbound?
                    ObjectPoint(0,"afterlightwaypoint"+region,new Point(-100,-100)):
                    AfterlightPoint(region,"outlet");
                Point destination=outbound?AfterlightPoint(region,"outlet"):
                    ObjectPoint(0,"afterlightwaypoint"+region,new Point(-100,-100));
                if(save.room!=(outbound?0:region) ||
                    (Position-from).Length>2.2f)
                    return true;
                // A failed arrival should never reset the source room.
                int targetRoom=outbound?region:0;
                if(!CanEnter(targetRoom)||
                    !AfterlightDestinationWalkable(targetRoom,destination))
                {
                    Notify("星灯出口被阻断；当前地图保持不变。",
                        "Starroad destination blocked; current region preserved.");
                    return true;
                }
                Enter(targetRoom);
                // Preserve the original afterlight receiver as an arrival
                // destination. Ordinary region spawns may patrol near it;
                // Enter() supplies the normal travel grace period.
                if(save.room==targetRoom && CanStand(destination))
                {
                    save.x=destination.x;save.y=destination.y;revision++;sound="travel";
                    Notify("公共星灯线路已接通。居民和冒险者都能使用这条新道路。",
                        "Public starroad now links town and civic services for every traveler.");
                }
                else if(save.room==targetRoom)
                    Notify("出口附近有敌人或真实危险，已停靠在普通安全入口。",
                        "Arrival is threatened; you remain at the ordinary regional entrance.");
                return true;
            }
            if(action.StartsWith("afterlight_steward_"))
            {
                int r;
                if(!int.TryParse(action.Substring("afterlight_steward_".Length),out r) ||
                    r<1||r>3 || save.room!=r || !Has("afterlight_started") ||
                    !Has("afterlight_done_"+r) || Has("afterlight_steward_"+r) ||
                    (Position-ObjectPoint(r,"witnessnpc"+r,new Point(-100,-100))).Length>2.2f)
                    return true;
                Flag("afterlight_steward_"+r);revision++;sound="quest";
                RecordMoment(AfterlightLocation(r,false)+"的当地居民已签下公共设施的维护责任。",
                    "Local residents at "+AfterlightLocation(r,true)+" signed responsibility for their working public infrastructure.");
                Notify("居民维护承诺已签署："+AfterlightStewards+"/3。公约必须由活着的人亲自承担。",
                    "Resident stewardship signed: "+AfterlightStewards+"/3. A charter needs real people taking responsibility.");
                return true;
            }
            if(action=="afterlight_craft")
            {
                if(save.room!=3 || !Has("afterlight_done_3") || AfterlightMode(3)!=1 ||
                    Dying || practicing || racing ||
                    (Position-AfterlightPoint(3,"outlet")).Length>2.2f)return true;
                if(FieldWorkPreventsRest || !SafeTravel || afterlightChargeRegion!=0)
                {
                    Notify("工房暂时停工：附近仍有战斗或尚未完成的现场任务。",
                        "Workshop paused: nearby combat or another live field mission must finish before crafting.");
                    return true;
                }
                int output = 2 + (HearingComplete && HearingVoice(3) == 1 ? 1 : 0)
                    + (AfterlightReserve(3) ? 1 : 0) +
                    (AfterlightWatchDone(3) ? 1 : 0);
                // Repeatable public workshop recipes must reject a full parts
                // stack BEFORE charging genuine timber and starcrystal.
                if(HasInventoryRoom(4, output) &&
                    Spend(1,StormPolicy(3)==2?1:2,0,0,0))
                {CreditInventory(4, output);revision++;sound="reward";
                    Notify("工房回收成功：木材 -1、星晶 -"+(StormPolicy(3)==2?1:2)+"，零件 +"+output+"。",
                        "Public recycling: -1 timber, -"+(StormPolicy(3)==2?1:2)+" starcrystal, +"+output+" actual machine parts.");}
                return true;
            }
            if(action.StartsWith("afterlight_reserve_"))
            {
                int region;
                if(!int.TryParse(action.Substring("afterlight_reserve_".Length),out region) ||
                    region<1||region>3 || save.room!=region || !AfterlightComplete ||
                    !Has("afterlight_done_"+region) || !Has("afterlight_steward_"+region) ||
                    AfterlightReserve(region) || Dying || practicing || racing ||
                    FieldWorkPreventsRest || !SafeTravel || afterlightChargeRegion!=0 ||
                    (Position-AfterlightPoint(region,"outlet")).Length>2.2f)
                    return true;
                int wood=region==1?2:region==2?1:0;
                int crystal=region==3?2:0;
                int herb=region==1?1:region==2?2:0;
                int gear=region==3?1:0;
                // Spend validates all ingredients before removing any. Forged,
                // distant, repeat and unaffordable actions must be harmless.
                if(!Spend(wood,crystal,herb,0,gear))return true;
                Flag("afterlight_reserve_"+region);revision++;sound="reward";
                RecordMoment(AfterlightLocation(region,false)+"建立了居民应急储备，公共设施获得永久服务升级。",
                    "A permanent emergency reserve strengthened local service at "+AfterlightLocation(region,true)+".");
                Notify("公共应急储备已投入！本地服务永久升级（"+AfterlightReserves+"/3）。",
                    "Civic emergency reserve stocked. Local service upgraded permanently ("+AfterlightReserves+"/3).");
                return true;
            }
            string[] pieces=action.Split('_');
            int room;
            if(pieces.Length<3 || !int.TryParse(pieces[2],out room) || room<1 || room>3 ||
                save.room!=room || !AfterlightEligible || !Has("afterlight_started") ||
                Has("afterlight_done_"+room) || Dying || practicing)return true;
            if(pieces[1]=="plan")
            {
                if(pieces.Length!=4 || AfterlightMode(room)!=0 ||
                    (Position-AfterlightPoint(room,"anchor")).Length>2.2f)return true;
                if(pieces[3]!="care"&&pieces[3]!="watch")return true;
                Flag(action);TrackQuest("afterlight");
                Notify("居民用途方案已署名；下一步启动90秒余光输送。",
                    "Civic plan signed. Next: start the 90-second afterlight transfer.");
                return true;
            }
            if(pieces[1]=="charge"||pieces[1]=="stable")
            {
                bool stable=pieces[1]=="stable";
                if(AfterlightMode(room)==0 || afterlightChargeRegion!=0 ||
                    FieldWorkPreventsRest || !SafeTravel ||
                    (Position-AfterlightPoint(room,"anchor")).Length>2.2f ||
                    racing)return true;
                if(stable&&save.inventory[1]<1)
                {
                    Notify("稳态传输需要1枚真实星晶，也可以选择免费90秒传输。",
                        "Stable transport needs one real starcrystal. The 90-second route remains free.");
                    return true;
                }
                if(stable)save.inventory[1]--;
                afterlightChargeRegion=room;
                afterlightStableCharge=stable;
                afterlightChargeLeft=stable?120:90;afterlightChargeIntegrity=100;
                afterlightReceiverRepaired=false;
                revision++;sound="quest";
                Notify(stable?
                    "已投入星晶，120秒稳态传输开始！亲自前往另一端的公共接收台。":
                    "90秒传输开始！赶往地图另一端的公共接收台。战斗不会自动替你完成重建。",
                    stable?
                    "One crystal invested: 120-second stable load started. Walk physically to the distant public receiver.":
                    "90-second load transfer started! Reach the real receiver across the map. Combat cannot substitute for reconstruction.");
                return true;
            }
            if(pieces[1]=="discard")
            {
                // A true physical receiver withdrawal lets an underpowered
                // courier retreat without walking the full route back or
                // spending unrelated crafting inputs just to clear the task.
                if(afterlightChargeRegion!=room||
                    (Position-AfterlightPoint(room,"outlet")).Length>2.2f)
                    return true;
                bool wasStable=afterlightStableCharge;
                ResetAfterlightCharge();revision++;sound="quest";
                Notify("本次远端负载已安全释放。居民用途方案与旧重建进度仍在；可回原锚点重新运输。",
                    "Receiver discharged. Civic design and prior achievements remain; retry the transport at its source anchor."+
                    (wasStable?" The previously spent stabilizing crystal is not refunded.":""));
                return true;
            }
            if(pieces[1]=="repair")
            {
                if(afterlightChargeRegion!=room||
                    afterlightReceiverRepaired||afterlightChargeIntegrity<=0||
                    afterlightChargeIntegrity>=AfterlightReceiverThreshold||
                    (Position-AfterlightPoint(room,"outlet")).Length>2.2f||
                    !SafeTravel)return true;
                if(afterlightChargeLeft<=8)
                {
                    Notify("运输时间不足8秒，无法在本次载荷上修复。可直接在此放弃并重新组织。",
                        "Less than eight seconds remain. Discharge the load at this receiver and retry.");
                    return true;
                }
                if(save.inventory[2]<1)
                {
                    Notify("现场修复需要一株真正的月光草，可返回原锚点重新运输。",
                        "Repair requires one real Moonherb. You can retry from the source anchor.");
                    return true;
                }
                save.inventory[2]--;afterlightReceiverRepaired=true;
                afterlightChargeLeft-=8;
                afterlightChargeIntegrity=Math.Min(100,afterlightChargeIntegrity+45);
                revision++;sound="reward";
                Notify("接收台修复完成！月光草-1、运输时间-8秒；负载重新达到安装阈值。",
                    "Receiver restored: -1 Moonherb and -8 seconds; the power load is usable again.");
                return true;
            }
            if(pieces[1]=="install")
            {
                if(afterlightChargeRegion!=room || afterlightChargeLeft<=0 ||
                    (Position-AfterlightPoint(room,"outlet")).Length>2.2f ||
                    !SafeTravel)return true;
                if(afterlightChargeIntegrity<AfterlightReceiverThreshold)
                {
                    Notify("负载不足35%：请在接收台使用月光草抢修，或释放载荷回原锚点重试。",
                        "Power below 35%. Repair with one Moonherb here or discharge and retry from the anchor.");
                    return true;
                }
                bool care=AfterlightMode(room)==1;
                if(!Spend(care?2:0,care?0:2,care?2:0,0,care?0:1))return true;
                ResetAfterlightCharge();
                Flag("afterlight_done_"+room);
                CreditCoins(20+room*10);AddXp(20+room*10);revision++;
                RecordMoment(AfterlightLocation(room,false)+"的公共供能已接通："+AfterlightOutcomeZh(room),
                    "Public power restored at "+AfterlightLocation(room,true)+": "+AfterlightOutcomeEn(room));
                Notify("真实接收端已通电！本地永久设施开放。返回公会或继续下一地区。",
                    "Physical receiver connected! Permanent local service unlocked. Visit the guild or proceed to the next region.");
                return true;
            }
            return true;
        }
        private void TickAfterlight(float dt)
        {
            if(afterlightChargeRegion==0)return;
            if(save.room!=afterlightChargeRegion || Dying || practicing)
            {ResetAfterlightCharge();revision++;return;}
            afterlightChargeLeft-=dt;
            if(afterlightChargeLeft<=0)
            {
                ResetAfterlightCharge();revision++;
                Notify("余光在抵达前散失。上次签署的方案与材料保留，可回锚点免费重启。",
                    "Afterlight dissipated before arrival. Your plan and materials remain; restart from the anchor for free.");
            }
        }
        public bool AfterlightRefuge(int region)
        {return region>=1&&region<=3 && Has("afterlight_done_"+region) &&
            (region==1&&AfterlightMode(1)==1 ||
             region==2&&AfterlightMode(2)==2 ||
             region==3&&AfterlightMode(3)==2);}
        public bool AfterlightEnergyPost
        { get { return save.room==1 && Has("afterlight_done_1") && AfterlightMode(1)==2; } }
    }
}
