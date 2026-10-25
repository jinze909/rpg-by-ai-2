using System;
using System.Collections.Generic;
namespace Starfall
{
    // Voluntary, authentic combat challenges placed in the new map ring.
    // The fight is transient. Only a legitimate clear earns a persistent v2 flag.
    public sealed partial class GameCore
    {
        private int outerPatrolRoom=-1;
        private int outerPatrolEnemyA=-1,outerPatrolEnemyB=-1,outerPatrolEnemyC=-1;
        private bool outerPatrolADead,outerPatrolBDead,outerPatrolCDead,outerPatrolPatched;
        private bool outerPatrolVeteran,outerPatrolBraceBuilt,outerPatrolCompanionGuard,
            outerPatrolGuardMedicRescue;
        private Point outerPatrolCompanionGuardFoot;
        private float outerPatrolGuardRouteCheck;
        private int outerPatrolGuardBlocksLeft;
        private bool outerPatrolGuardBlockedStock;
        private float outerPatrolTime,outerPatrolStock,outerPatrolBrace,outerPatrolBraceFlash;
        private Point outerPatrolPost;
        public bool OuterPatrolCompanionGuarding
        {get{return OuterPatrolActive&&outerPatrolCompanionGuard&&save.companion>=0;}}
        public bool OuterPatrolMedicRallying
        {get{return OuterPatrolCompanionGuarding&&save.companion==0&&
            outerPatrolGuardMedicRescue;}}
        public int OuterPatrolGuardBlocksLeft
        {get{return OuterPatrolActive?outerPatrolGuardBlocksLeft:0;}}
        public bool OuterPatrolGuardReady
        {get{return OuterPatrolCompanionGuarding&&
            !OuterPatrolMedicRallying&&outerPatrolGuardBlocksLeft>0&&
            Fits(companionPosition)&&
            (companionPosition-outerPatrolCompanionGuardFoot).Length<=.65f;}}
        // Unlike an inexhaustible proximity aura, a stationed physical
        // companion gets only two authentic emergency bodyguard contacts
        // in the entire attempt. Switching allies or issuing Y again can
        // never refill this finite local supply of guards.
        private bool OuterPatrolCompanionCanIntercept(Enemy attacker)
        {
            return attacker!=null&&OuterPatrolGuardReady&&
                (companionPosition-attacker.Position).Length<=2.2f&&
                ClearAttackPath(attacker.Position,companionPosition);
        }
        // Combat target selection, warning and actual stock damage use
        // identical physical coverage: prioritize the raider who can
        // STILL injure supplies despite the hero/guard frontline.
        private bool OuterPatrolUncoveredStockStrike(Enemy foe)
        {
            return OuterPatrolActive&&foe!=null&&foe.hp>0&&
                IsOuterPatrolEnemy(foe)&&foe.kind!=1&&
                foe.state==1&&foe.targetingOuterPatrolStock&&
                !float.IsNaN(foe.timer)&&!float.IsInfinity(foe.timer)&&
                foe.stun<=0f&&foe.hitTime<=0f&&
                (foe.Position-outerPatrolPost).Length<=2.35f&&
                CanStand(foe.Position)&&
                ClearAttackPath(foe.Position,outerPatrolPost)&&
                !OuterPatrolHeroIntercepts(foe)&&
                !OuterPatrolCompanionCanIntercept(foe);
        }
        public Point OuterPatrolCompanionGuardPosition
        {get{return OuterPatrolCompanionGuarding?outerPatrolCompanionGuardFoot:companionPosition;}}
        public float OuterPatrolIntegrity {get{return OuterPatrolActive?outerPatrolStock:0f;}}
        public float OuterPatrolTime {get{return OuterPatrolActive?outerPatrolTime:0f;}}
        // The same LIVE frontline interception must govern the actual
        // physical stock hit and the predicted warning. A hero who arrived
        // AFTER the hostile windup still blocks its stock damage on release;
        // do not display a contradictory unavoidable-hit HUD alert.
        private bool OuterPatrolHeroIntercepts(Enemy foe)
        {
            return foe!=null&&!Dying&&CanStand(Position)&&
                (Position-foe.Position).Length<=2.2f&&
                ClearAttackPath(foe.Position,Position);
        }
        // UI follows the same authentic released-hit requirements as
        // DamageOuterPatrolStock: never warn about a removed, stunned,
        // blocked or behind-wall enemy, or an ordinary archer aimed at the
        // player rather than protected supplies.
        public Enemy OuterPatrolIncomingStockThreat
        {
            get
            {
                if(!OuterPatrolActive)return null;
                Enemy closest=null;
                float bestSeconds=float.PositiveInfinity;
                float bestDistance=float.PositiveInfinity;
                foreach(Enemy foe in enemies)
                {
                    if(!OuterPatrolUncoveredStockStrike(foe))
                        continue;
                    // A non-finite clock has no meaningful arrival deadline.
                    // In particular, NaN makes every later "timer < best"
                    // comparison false and can hide a real, finite incoming hit.
                    if(float.IsNaN(foe.timer)||float.IsInfinity(foe.timer))
                        continue;
                    // Sort by the raw finite deadline: a negative timer
                    // is already overdue and must outrank a small positive
                    // timer. Only the HUD-facing seconds property clamps to 0.
                    float seconds=foe.timer;
                    Point delta=foe.Position-outerPatrolPost;
                    float distance=delta.x*delta.x+delta.y*delta.y;
                    bool sameDeadline=closest!=null&&seconds==bestSeconds;
                    bool closer=distance<bestDistance;
                    bool sameDistance=distance==bestDistance;
                    bool preferHeavy=sameDistance&&foe.kind==2&&
                        closest!=null&&closest.kind!=2;
                    if(closest==null||seconds<bestSeconds||
                        (sameDeadline&&(closer||preferHeavy)))
                    {
                        closest=foe;
                        bestSeconds=seconds;
                        bestDistance=distance;
                    }
                }
                return closest;
            }
        }
        public float OuterPatrolIncomingStockSeconds
        {get{Enemy foe=OuterPatrolIncomingStockThreat;
            return foe==null?-1f:Math.Max(0f,foe.timer);}}
        public bool OuterPatrolIncomingStockHeavy
        {get{Enemy foe=OuterPatrolIncomingStockThreat;
            return foe!=null&&foe.kind==2;}}
        public float OuterPatrolBraceIntegrity
        {get{return OuterPatrolVeteranActive?outerPatrolBrace:0f;}}
        // These draw signals are generated by actual attacks and disappear
        // when the physical temporary defensive structure is destroyed.
        public bool OuterPatrolBraceStanding
        {get{return OuterPatrolVeteranActive&&outerPatrolBrace>0f;}}
        public float OuterPatrolBraceHitFlash
        {get{return OuterPatrolBraceStanding?outerPatrolBraceFlash:0f;}}
        public bool OuterPatrolBraceAvailable
        {get{return OuterPatrolVeteranActive&&!outerPatrolBraceBuilt&&
            !Dying&&!practicing&&save.inventory[0]>=2&&
            save.inventory[4]>=1;}}
        public bool OuterPatrolPatchAvailable
        {get{return OuterPatrolActive&&!outerPatrolPatched&&
            outerPatrolStock>0f&&outerPatrolStock<100f&&
            save.inventory[0]>0&&save.inventory[2]>0;}}
        public bool OuterPatrolActive { get { return outerPatrolRoom==save.room; } }
        public int OuterPatrolRaidersRemaining
        {
            get
            {
                if(!OuterPatrolActive)return 0;
                return (outerPatrolADead?0:1)+(outerPatrolBDead?0:1)+
                    (outerPatrolVeteran&&!outerPatrolCDead?1:0);
            }
        }
        public bool OuterPatrolVeteranActive
        { get { return OuterPatrolActive&&outerPatrolVeteran; } }
        public bool OuterPatrolVeteranDone(int room)
        { return room>=0&&room<8&&Has("outer_patrol_veteran_"+room); }
        // A genuine flawless victory requires the whole real three-raider
        // defense with at least 90% stock and no free-form repair or cover.
        // Unlike the first-clear payout, this badge CAN be earned on replay.
        public bool OuterPatrolFlawlessEligible
        { get {return OuterPatrolVeteranActive&&outerPatrolStock>=90f&&
            !outerPatrolPatched&&!outerPatrolBraceBuilt&&
            !outerPatrolGuardBlockedStock;}}
        public bool OuterPatrolFlawlessDone(int room)
        {return room>=0&&room<8&&Has("outer_patrol_flawless_"+room);}
        public int OuterPatrolFlawlessWins
        {get{int n=0;for(int i=0;i<8;i++)if(OuterPatrolFlawlessDone(i))n++;return n;}}
        public int OuterPatrolVeteranWins
        { get { int total=0;for(int i=0;i<8;i++)if(OuterPatrolVeteranDone(i))total++;return total; } }
        public bool OuterPatrolDone(int room)
        { return room>=0&&room<8&&Has("outer_patrol_done_"+room); }
        // Victory changes the actual world, not only the quest journal.
        // Only the local, already-defended post can replenish living visitors.
        // Re-check the physical interaction lane so sealed gates and shorelines
        // cannot project healing through terrain or into a live siege.
        public bool OuterPatrolRefuge
        {
            get
            {
                if(save.room<0||save.room>=8||!OuterPatrolDone(save.room)||
                    OuterPatrolActive||Dying||practicing||racing||!SafeTravel)
                    return false;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="outerwatch"&&
                        PhysicalRefugeReach(new Point(o.x,o.y),3.15f))
                        return true;
                return false;
            }
        }
        public int OuterPatrolWins
        {
            get { int n=0;for(int i=0;i<8;i++)if(OuterPatrolDone(i))n++;return n; }
        }
        private void ResetOuterPatrol()
        {
            // An interrupted defense must retire its actual attackers, not
            // merely forget their IDs and leave orphan combat in the world.
            // Also retire their already released arrows and pending cues;
            // unrelated wilderness patrols and player attacks survive.
            if(outerPatrolRoom>=0)
                for(int i=enemies.Count-1;i>=0;i--)
                    if(enemies[i].id==outerPatrolEnemyA||
                       enemies[i].id==outerPatrolEnemyB||
                       enemies[i].id==outerPatrolEnemyC)
                    {
                        RemoveEnemyPendingWarning(enemies[i]);
                        enemies.RemoveAt(i);
                    }
            projectiles.RemoveAll(p=>p.outerPatrolOwned);
            effects.RemoveAll(e=>e.outerPatrolOwned);
            outerPatrolRoom=outerPatrolEnemyA=outerPatrolEnemyB=
                outerPatrolEnemyC=-1;
            outerPatrolADead=outerPatrolBDead=outerPatrolCDead=
                outerPatrolPatched=outerPatrolVeteran=
                outerPatrolBraceBuilt=outerPatrolCompanionGuard=
                outerPatrolGuardMedicRescue=false;
            outerPatrolCompanionGuardFoot=new Point();
            outerPatrolGuardRouteCheck=0f;
            outerPatrolGuardBlocksLeft=0;
            outerPatrolGuardBlockedStock=false;
            outerPatrolStock=outerPatrolTime=outerPatrolBrace=
                outerPatrolBraceFlash=0f;
            outerPatrolPost=new Point();
        }
        private bool IsOuterPatrolEnemy(Enemy e)
        {
            return outerPatrolRoom==save.room &&
                (e.id==outerPatrolEnemyA||e.id==outerPatrolEnemyB||
                 e.id==outerPatrolEnemyC);
        }
        private void OnOuterPatrolEnemyDefeated(Enemy e)
        {
            // Invoked *only* after real Hit() reduces HP to zero. A list clear,
            // transition, despawn or save load can never forge a victory.
            if(!IsOuterPatrolEnemy(e))return;
            if(e.id==outerPatrolEnemyA)outerPatrolADead=true;
            if(e.id==outerPatrolEnemyB)outerPatrolBDead=true;
            if(e.id==outerPatrolEnemyC)outerPatrolCDead=true;
        }
        // Only a genuine released melee swing can damage this physical cache.
        // No passive radius drain, through-wall attrition, or fake projectile hits.
        private void DamageOuterPatrolStock(Enemy attacker)
        {
            if(!OuterPatrolActive||attacker==null||!IsOuterPatrolEnemy(attacker)||
                !attacker.targetingOuterPatrolStock||attacker.hp<=0||attacker.kind==1)return;
            if((attacker.Position-outerPatrolPost).Length>2.35f||
                !CanStand(attacker.Position)||
                !ClearAttackPath(attacker.Position,outerPatrolPost))return;
            // The attack was honestly telegraphed toward the supplies, but
            // the defender may step into the raider's reach BEFORE contact.
            // Do not resolve an obsolete target lock into an invisible cache
            // hit when the living hero now intercepts the very same swing.
            // The normal melee/AoE release above still tests actual contact
            // with the defender; only the extra stock hit is withheld.
            if(OuterPatrolHeroIntercepts(attacker))
            {
                effects.Add(new Effect("number",outerPatrolPost,.7f,
                    L("拦截成功","INTERCEPTED")){outerPatrolOwned=true});
                revision++;sound="guard";
                return;
            }
            if(OuterPatrolCompanionCanIntercept(attacker))
            {
                --outerPatrolGuardBlocksLeft;
                outerPatrolGuardBlockedStock=true;
                effects.Add(new Effect("number",companionPosition,.8f,
                    L("同伴护卫 -1","ALLY GUARD -1"))
                    {outerPatrolOwned=true});
                revision++;sound="guard";
                return;
            }
            float damage=attacker.kind==2?22f:14f;
            // A real player's one-time timber/gear fortification absorbs
            // only genuine released, unobstructed saboteur contacts.
            // It is NOT a new NPC, passive radius drain, or global shield.
            if(outerPatrolBrace>0f)
            {
                float absorbed=Math.Min(outerPatrolBrace,damage);
                outerPatrolBrace=Math.Max(0f,outerPatrolBrace-absorbed);
                outerPatrolBraceFlash=.28f;
                damage-=absorbed;
                effects.Add(new Effect("number",outerPatrolPost,.8f,
                    L("路障承受 -"+(int)absorbed,
                      "BARRICADE -"+(int)absorbed))
                    {outerPatrolOwned=true});
                if(outerPatrolBrace<=0f)
                    Notify("临时路障被击破！继续贴身保护物资。",
                        "Barricade broken! Intercept raiders to save the cache.");
            }
            if(damage<=0f){revision++;sound="hit";return;}
            outerPatrolStock=Math.Max(0f,outerPatrolStock-damage);
            effects.Add(new Effect("number",outerPatrolPost,.75f,
                L("物资受损 -"+(int)damage,"SUPPLIES -"+(int)damage))
                {outerPatrolOwned=true});
            revision++;sound="hit";
            if(outerPatrolStock<=0f)
            {
                ResetOuterPatrol();revision++;
                Notify("救援物资在真实袭击中失守！探索记录仍在，可重新组织防卫。",
                    "Raiders destroyed the relief defense! The original supplies and exploration record remain; retry anytime.");
            }
            else Notify("救援物资遭到袭击，剩余耐久 "+Math.Ceiling(outerPatrolStock)+"%。",
                "Relief supplies under attack: "+Math.Ceiling(outerPatrolStock)+"% integrity left.");
        }
        private bool TalkOuterPatrol(WorldObject o)
        {
            if(o.kind!="outerwatch")return false;
            int room=save.room;
            if(room<0||room>=8||!HasPhysicalInteractionReach(o))return true;
            if(OuterPatrolActive)
            {
                var choices=new List<Choice> {
                    C("临时加固 · 木材和草药各1",
                      "Emergency patch · timber + Moonherb","outer_watch_patch")
                };
                // A normal raid cannot build veteran cover, and an already
                // deployed or unaffordable barricade is never an actionable
                // choice. Keep abort visible regardless of combat resources.
                if(OuterPatrolBraceAvailable)
                    choices.Add(C("部署路障 · 木材2、零件1",
                        "Deploy barricade · 2 timber + 1 spare part",
                        "outer_watch_brace"));
                if(save.companion>=0)
                    choices.Add(OuterPatrolCompanionGuarding?
                        C("同伴恢复跟随","Companion: follow me","outer_watch_guard"):
                        C("同伴驻守哨岗","Companion: guard the cache","outer_watch_guard"));
                choices.Add(C("主动撤守","Withdraw defense","outer_watch_abort"));
                choices.Add(C("继续防卫","Continue the defense","close"));
                Dialog(o.name,o.en,
                    (outerPatrolVeteran?"精英突袭正在威胁已守住的救援岗。三名敌人包括重甲撞击手。":"盗取物资的袭击者逼近哨岗。")+
                    (outerPatrolVeteran?" 路障耐久 "+Math.Ceiling(outerPatrolBrace)+"。":"")+
                    "剩余 "+Math.Ceiling(outerPatrolTime)+" 秒，物资耐久 "+
                    Math.Ceiling(outerPatrolStock)+"%。木材和月光草可抢修一次。",
                    (outerPatrolVeteran?"A veteran three-raider assault, including a heavy slammer, is underway.":"Raiders are attacking the relief stock.")+
                    " Time left "+Math.Ceiling(outerPatrolTime)+"s; integrity "+
                    Math.Ceiling(outerPatrolStock)+"%. Timber and Moonherb patch it once.",
                    choices.ToArray());
                return true;
            }
            if(OuterPatrolDone(room))
            {
                Dialog(o.name,o.en,
                    "居民已接管此处的物资与补给道路，安全时可恢复生命和耐力。"+
                    (OuterPatrolFlawlessDone(room)?
                        "本地已获得无损守卫徽章，安全时享受最高级医疗补给。":
                        "精英战无路障、无抢修并守住90%以上物资，可争取无损徽章并强化现场医疗。")+
                    (OuterPatrolVeteranDone(room)?
                        "你击退过精英三人组，此处医疗与补给更充足。可重演训练，但不会重复获得奖品。":
                        "可发起 110 秒精英防卫：真正的重甲敌人会砸毁物资。首次获胜可强化补给点。"),
                    "Residents operate this defended supply route. Recover here when safe. "+
                    (OuterPatrolFlawlessDone(room)?
                        "Flawless watch medal earned: improved local healing and energy support. ":
                        "Clear a veteran siege with >=90% stock and no barricade or patch for a flawless medal. ")+
                    (OuterPatrolVeteranDone(room)?
                        "Veteran defense cleared: stronger local care is available. Replay yields no rewards.":
                        "A 110s veteran defense adds a real heavy cache saboteur. First clear improves this refuge."),
                    C(OuterPatrolVeteranDone(room)?"再战精英防卫 · 无重复奖励":"挑战精英防卫 · 强化补给岗",
                        OuterPatrolVeteranDone(room)?"Replay veteran defense · no repeat loot":"Veteran defense · upgrade this refuge",
                        "outer_watch_veteran"),
                    C("查看已守护的补给道路","Open protected supply routes","outer_watch_routes"),
                    C("继续巡路","Keep exploring","close"));
                return true;
            }
            if(!OuterCacheRecovered(room))
            {
                Dialog(o.name,o.en,
                    "此处还没有可以守护的物资。先调查南部记录，修复求援灯，找回东南方的救援箱。",
                    "No supplies have arrived yet. Recover the southern log, repair the eastern relay, then retrieve the relief cache.",
                    C("查找信号","Find the signal","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "救援信号引来了需要帮助的人，也引来了追逐补给的暴走兽。主动组织一场 95 秒的防卫：近战袭击者会冲向物资，贴身阻截才能吸引它。物资耐久归零、超时、远离或倒下均需重试。\n\n此前记录与物资不会消失。",
                "The relit beacon draws raiders. Defend for up to 95 seconds: melee attackers damage the supplies unless intercepted at close range. Losing the cache, timing out, leaving or falling cancels the attempt.\n\nExisting exploration and relief supplies remain safe.",
                C("守护物资 · 开始战斗","Defend supplies · begin","outer_watch_start"),
                C("以后再来","Not now","close"));
            return true;
        }
        // A protected post is a genuinely connected field station. Its
        // travel board is not a global "warp to locked content" menu: check
        // the *current* physical station and threats again on every cached
        // choice, including the final destination action.
        private bool CanUseOuterPatrolRoutes()
        {
            if(save.room<0||save.room>=8||!OuterPatrolDone(save.room)||
                Dying||practicing||racing||FieldWorkPreventsRest||!SafeTravel)
                return false;
            foreach(WorldObject site in Map.objects)
                if(site.kind=="outerwatch"&&HasPhysicalInteractionReach(site))
                    return true;
            return false;
        }
        private static readonly string[] OuterWatchNamesZh={
            "引水渠","荆棘密林","沉没遗迹","赤铁矿场",
            "堡垒外环","雾港","玻璃沙海","霜钟高原"
        };
        private static readonly string[] OuterWatchNamesEn={
            "Canal","Thornwood","Drowned Ruins","Ironreach",
            "Citadel","Mistport","Glasswind","Frostchime"
        };
        private bool ChooseOuterPatrolRoutes(string action)
        {
            bool main=action=="outer_watch_routes";
            bool ring=action=="outer_watch_ring_0"||action=="outer_watch_ring_1";
            bool destinationAction=action.StartsWith("outer_watch_to_");
            if(!main&&!ring&&!destinationAction)return false;
            if(!CanUseOuterPatrolRoutes())
            {
                Notify("必须在已守住且安全可达的救援岗操作道路图；战斗或其它现场任务期间无法旅行。",
                    "Use a defended, physically reachable outpost while safe; combat or live fieldwork blocks travel.");
                return true;
            }
            if(main)
            {
                Dialog("八地救援补给路网","Protected Relief Road Network",
                    "八地补给线路已由居民接管。只能在已守护并解锁的哨岗之间旅行；正在进行的救援、敌袭与尚未开启的章节关卡都不会被跳过。",
                    "Residents now maintain the protected supply roads. Travel only between defeated, unlocked outposts; live rescues, pursuers and campaign gates still apply.",
                    C("西部四地 · 查看路线","Western four regions · routes","outer_watch_ring_0"),
                    C("远岸四地 · 查看路线","Farshore four regions · routes","outer_watch_ring_1"),
                    C("返回哨岗","Back to post","close"));
                return true;
            }
            if(ring)
            {
                int first=action=="outer_watch_ring_0"?0:4;
                var choices=new List<Choice>();
                for(int i=first;i<first+4;i++)
                    if(i!=save.room&&OuterPatrolDone(i)&&CanEnter(i))
                        choices.Add(C("前往 "+OuterWatchNamesZh[i],
                            "Travel to "+OuterWatchNamesEn[i],
                            "outer_watch_to_"+i));
                choices.Add(C("返回路线图","Back to route map","outer_watch_routes"));
                choices.Add(C("留在这里","Remain at this post","close"));
                Dialog("救援道路 · "+(first==0?"西部":"远岸"),
                    "Relief Roads · "+(first==0?"West":"Farshore"),
                    choices.Count==2?"当前尚无其它已守住且已解锁的目的地。":
                        "选择一个已实际守住的哨岗。行军前再次检查附近敌人和活动任务。",
                    choices.Count==2?"No other defended and unlocked outpost is available yet.":
                        "Choose an outpost you actually defended; live threats and missions are checked again before departure.",
                    choices.ToArray());
                return true;
            }
            int room;
            if(!int.TryParse(action.Substring("outer_watch_to_".Length),out room)||
                room<0||room>=8||room==save.room||
                !OuterPatrolDone(room))return true;
            if(!CanEnter(room))
            {
                Notify(RouteHint(room,false),RouteHint(room,true));
                return true;
            }
            int origin=save.room;
            Enter(room); // Preserve all original region, mission and save rules.
            if(save.room!=room||save.room==origin)return true;
            // Share the physical, local-route and companion-clearance
            // algorithm with restored signal-courier travel. When a
            // destination is crowded, Enter's safe map spawn remains.
            bool landed=TryLandTravelPartyAtPost("outerwatch");
            revision++;sound="travel";
            Notify(landed?"已抵达实际守护过的救援岗。":
                    "前方补给点暂时拥挤，已在本地区安全入口落脚。",
                landed?"You arrive beside the defended relief post.":
                    "The outpost approach is crowded; you arrived at the normal region entrance.");
            return true;
        }
        // Guarding is a physical tactical order, not a teleport or a
        // remote perk. Choose a valid and connected tile adjacent to the
        // real protected supply post, outside the defender's occupied foot.
        // A placed ally walks there via its normal SweepActor movement.
        // Compute usable bow/staff lines, not just straight-line LOS.
        // A legal foot behind a high wall or a muzzle-burying corner is
        // physically safe but cannot defend supplies from genuine raiders.
        // This is a PURE probe: WeaponShot returns the launch record; no
        // fake projectile or cooldown is published to the battlefield.
        private int OuterPatrolGuardFiringLanes(Point candidate)
        {
            if(!OuterPatrolActive||save.companion<0||
                !Fits(candidate))return 0;
            int weapon=save.companion==1?1:2,score=0;
            foreach(Enemy foe in enemies)
            {
                if(foe==null||foe.hp<=0||
                    !IsOuterPatrolEnemy(foe))continue;
                Point aim=foe.Position-candidate;
                if(aim.Length<=.1f||aim.Length>=7f||
                    !ClearAttackPath(candidate,foe.Position))continue;
                if(WeaponShot(candidate,weapon,aim.Normalized,9f,0f,
                    foe.Position).blockedAtSpawn)continue;
                // Prefer an actual telegraphed cache hit over a distant
                // shot, but never trade away all valid firing lanes.
                score+=foe.kind!=1?
                    (foe.state==1&&foe.targetingOuterPatrolStock?4:2):1;
            }
            return score;
        }
        private bool TryOuterPatrolCompanionGuardFoot(out Point result)
        {
            result=new Point();
            if(!OuterPatrolActive||save.companion<0||
                !Fits(companionPosition))return false;
            Point[] offsets={
                new Point(0f,2.5f),new Point(-2.5f,0f),
                new Point(2.5f,0f),new Point(0f,-2.5f),
                new Point(-1.7f,1.7f),new Point(1.7f,1.7f),
                new Point(-1.7f,-1.7f),new Point(1.7f,-1.7f)
            };
            float best=float.MaxValue;
            int bestCoverage=-1;
            foreach(Point offset in offsets)
            {
                Point candidate=outerPatrolPost+offset;
                if(!Fits(candidate)||
                    (candidate-Position).Length<1.15f||
                    enemies.Exists(e=>e.hp>0&&
                        (e.Position-candidate).Length<1.2f)||
                    !ActorHasLocalRoute(companionPosition,candidate,10))
                    continue;
                float distance=(candidate-companionPosition).Length;
                int coverage=OuterPatrolGuardFiringLanes(candidate);
                if(coverage<bestCoverage||
                    (coverage==bestCoverage&&distance>=best))continue;
                best=distance;bestCoverage=coverage;result=candidate;
            }
            return best<float.MaxValue;
        }
        private bool ReplanOuterPatrolGuardFoot()
        {
            Point alternate;
            if(TryOuterPatrolCompanionGuardFoot(out alternate))
            {
                // Reroute only the target. Real ally position, muzzle,
                // animation, current attack and movement remain untouched.
                outerPatrolCompanionGuardFoot=alternate;
                revision++;
                return true;
            }
            outerPatrolCompanionGuard=outerPatrolGuardMedicRescue=false;
            outerPatrolGuardRouteCheck=0f;
            Notify("哨岗附近的安全驻守路线已封闭，同伴恢复跟随。",
                "No accessible cache guard lane remains; companion follows normally.");
            return false;
        }
        private Point CompanionOutpostGuardTarget()
        {
            if(!OuterPatrolCompanionGuarding)return CompanionFollowTarget();
            // Tide, gates and local map changes can invalidate a planted
            // guard foot. Fall back to normal following rather than walking
            // through a suddenly blocked prop, retaining the chosen stance
            // only while a real guard position remains available.
            if(!Fits(outerPatrolCompanionGuardFoot)&&
                !ReplanOuterPatrolGuardFoot())
                return CompanionFollowTarget();
            // Mira can protect a relief site without abandoning a critically
            // injured hero beyond her PHYSICAL six-unit heal reach. Rally on
            // foot at <35% HP; do not bounce back after the first small heal.
            // Her real command/auto heal still requires range, standability
            // and LOS, and her guard point remains planted until recovery.
            if(save.companion!=0)outerPatrolGuardMedicRescue=false;
            else if(save.health>0&&save.health<save.maxHealth*.35f)
                outerPatrolGuardMedicRescue=true;
            else if(save.health>=save.maxHealth*.55f)
                outerPatrolGuardMedicRescue=false;
            if(outerPatrolGuardMedicRescue)
                return CompanionFollowTarget();
            // A dynamic flood, raised gate or reconfigured corridor can
            // strand an otherwise valid original guard foot. Test actual
            // local connectivity at a bounded 0.85s cadence, never 50
            // flood-fill searches per second. Reposition to another safe,
            // shootable reachable foot; do not teleport the ally itself.
            if(outerPatrolGuardRouteCheck<=0f)
            {
                outerPatrolGuardRouteCheck=.85f;
                if((companionPosition-outerPatrolCompanionGuardFoot).Length>.4f&&
                    !ActorHasLocalRoute(companionPosition,
                        outerPatrolCompanionGuardFoot,10))
                {
                    if(!ReplanOuterPatrolGuardFoot())
                        return CompanionFollowTarget();
                }
                else if(save.companion!=0&&
                    (companionPosition-outerPatrolCompanionGuardFoot).Length<.5f&&
                    OuterPatrolGuardFiringLanes(outerPatrolCompanionGuardFoot)==0)
                {
                    // After a raider moves out of the shot lane, the old
                    // planted stance may stay walkable but tactically dead.
                    // Re-evaluate at the same bounded route-check cadence.
                    // Change post only if a NEW location can actually fire;
                    // do not roam forever when no targets are shootable.
                    Point alternate;
                    if(TryOuterPatrolCompanionGuardFoot(out alternate)&&
                        (alternate-outerPatrolCompanionGuardFoot).Length>.5f&&
                        OuterPatrolGuardFiringLanes(alternate)>0)
                    {
                        outerPatrolCompanionGuardFoot=alternate;
                        revision++;
                    }
                }
            }
            return outerPatrolCompanionGuardFoot;
        }
        private bool ToggleOuterPatrolCompanionGuard()
        {
            if(!OuterPatrolActive||save.companion<0||Dying||practicing)
                return false;
            // Calling a defender back is NOT a remote placement or warp.
            // It is a reversible command to use normal movement again, and
            // remains usable anywhere inside this actual defense perimeter.
            // Establishing a NEW planted guard still requires physical
            // interaction beside the original cache.
            if(outerPatrolCompanionGuard)
            {
                if((Position-outerPatrolPost).Length>12f)return false;
                outerPatrolCompanionGuard=outerPatrolGuardMedicRescue=false;
                outerPatrolGuardRouteCheck=0f;
                revision++;sound="quest";
                Notify("同伴收到战场召回指令，将步行恢复跟随。",
                    "Companion recalled on foot to resume normal following.");
                return true;
            }
            WorldObject watch=null;
            foreach(WorldObject candidate in Map.objects)
                if(candidate.kind=="outerwatch"){watch=candidate;break;}
            if(watch==null||!HasPhysicalInteractionReach(watch))
                return false;
            Point standing;
            if(!TryOuterPatrolCompanionGuardFoot(out standing))
            {
                Notify("附近没有同伴可步行抵达的安全驻守位置。",
                    "No safe, walkable guard foot is reachable near the supplies.");
                return true;
            }
            outerPatrolCompanionGuardFoot=standing;
            outerPatrolCompanionGuard=true;
            outerPatrolGuardMedicRescue=false;
            outerPatrolGuardRouteCheck=.85f;
            revision++;sound="quest";
            Notify("同伴正在步行前往补给岗守卫位置。",
                "Companion moving on foot to defend the supplies.");
            return true;
        }
        // The timed veteran raid is playable without pausing to open
        // a dialog: a single physical, already validated button uses the
        // SAME guarded resource transaction as the dialogue choice.
        // No cover is spawned while distant, dead, paused or replaying a
        // normal two-raider siege.
        public void QuickBraceOuterPatrol()
        {
            if(mode!="world"||!OuterPatrolBraceAvailable)return;
            ChooseOuterPatrol("outer_watch_brace");
        }
        public void QuickPatchOuterPatrol()
        {
            // X in-world does not open/stop the real siege clock. Spend the
            // SAME wood/herb transaction as the E dialogue: true damaged
            // stock, real nearby contact, exactly once per attempt.
            if(mode!="world"||!OuterPatrolPatchAvailable)return;
            ChooseOuterPatrol("outer_watch_patch");
        }
        public void QuickToggleOuterPatrolCompanionGuard()
        {
            // Keyboard Y can recall an ALREADY-deployed guard from within
            // the actual siege perimeter. Initial guard placement still
            // requires the same physical post as dialogue, never a remote
            // teleport or a paused-menu bypass.
            if(mode!="world"||!OuterPatrolActive||save.companion<0)
                return;
            ToggleOuterPatrolCompanionGuard();
        }
        private bool ChooseOuterPatrol(string action)
        {
            if(ChooseOuterPatrolRoutes(action))return true;
            if(action=="outer_watch_guard")
            {
                ToggleOuterPatrolCompanionGuard();
                return true;
            }
            if(action=="outer_watch_patch"||action=="outer_watch_abort"||
                action=="outer_watch_brace")
            {
                if(!OuterPatrolActive||Dying||practicing)return true;
                WorldObject cache=null;
                foreach(WorldObject candidate in Map.objects)
                    if(candidate.kind=="outerwatch"){cache=candidate;break;}
                if(cache==null||!HasPhysicalInteractionReach(cache))return true;
                if(action=="outer_watch_abort")
                {
                    ResetOuterPatrol();revision++;
                    Notify("本次防卫已结束。救援记录和物资仍然保留。",
                        "Defense withdrawn. Relief records and supplies are intact.");
                    return true;
                }
                if(action=="outer_watch_brace")
                {
                    if(!OuterPatrolBraceAvailable||
                        !Spend(2,0,0,0,1))return true;
                    outerPatrolBraceBuilt=true;outerPatrolBrace=48f;
                    outerPatrolBraceFlash=0f;
                    revision++;sound="quest";
                    effects.Add(new Effect("number",outerPatrolPost,.9f,
                        L("路障 +48","BARRICADE +48"))
                        {outerPatrolOwned=true});
                    Notify("路障部署完成：木材 -2、零件 -1。48点真实攻击防护，用尽不可重复建造。",
                        "Barricade deployed: -2 timber, -1 part. 48 real hit protection; once per siege.");
                    return true;
                }
                if(!OuterPatrolPatchAvailable||!Spend(1,0,1,0,0))return true;
                outerPatrolStock=Math.Min(100f,outerPatrolStock+30f);
                outerPatrolPatched=true;revision++;sound="reward";
                Notify("临时加固完成：木材、月光草各 -1，物资耐久 +30。",
                    "Emergency patch complete: -1 timber, -1 Moonherb, +30 stock integrity.");
                return true;
            }
            bool veteran=action=="outer_watch_veteran";
            if(!veteran&&action!="outer_watch_start")return false;
            int room=save.room;
            if(room<0||room>=8||OuterPatrolActive||
                (veteran&&!OuterPatrolDone(room))||
                (!veteran&&OuterPatrolDone(room))||
                !OuterCacheRecovered(room)||Dying||practicing)return true;
            WorldObject post=null;
            foreach(WorldObject o in Map.objects)if(o.kind=="outerwatch"){post=o;break;}
            if(post==null||!HasPhysicalInteractionReach(post))return true;
            if(!SafeTravel||FieldWorkPreventsRest)
            {
                Notify("先结束其它现场任务并清理附近追击者。","Finish other active field missions and clear nearby pursuers first.");
                return true;
            }
            Point[] feet;
            if(!TryOuterPatrolFeetWithCount(post,out feet,veteran?3:2))
            {
                Notify("救援岗没有足够的可达战斗落点，请清理拥挤的场地后重试。",
                    "Insufficient walkable, reachable raider roots. Clear the outpost and retry.");
                return true;
            }
            outerPatrolRoom=room;outerPatrolVeteran=veteran;
            outerPatrolTime=veteran?110f:95f;
            outerPatrolStock=100f;outerPatrolPatched=false;
            outerPatrolCompanionGuard=outerPatrolGuardMedicRescue=false;
            outerPatrolCompanionGuardFoot=new Point();
            outerPatrolGuardRouteCheck=0f;
            outerPatrolGuardBlocksLeft=2;
            outerPatrolGuardBlockedStock=false;
            outerPatrolBrace=outerPatrolBraceFlash=0f;
            outerPatrolBraceBuilt=false;
            outerPatrolPost=new Point(post.x,post.y);
            Enemy one=CreatePlacedEnemy(++enemyId,0,feet[0],
                (veteran?70:48)+room*10);
            Enemy two=CreatePlacedEnemy(++enemyId,1,feet[1],
                (veteran?65:42)+room*10);
            outerPatrolEnemyA=one.id;outerPatrolEnemyB=two.id;
            outerPatrolADead=outerPatrolBDead=outerPatrolCDead=false;
            enemies.Add(one);enemies.Add(two);
            if(veteran)
            {
                // Third enemy is a real heavy actor with a timed slamming
                // release. Its 22-point stock hit reuses actual hit/LOS logic.
                Enemy heavy=CreatePlacedEnemy(++enemyId,2,feet[2],88+room*12);
                outerPatrolEnemyC=heavy.id;enemies.Add(heavy);
            }
            revision++;sound="quest";
            Notify(veteran?"精英防卫开始：守住物资，击退三名实战袭击者！":
                    "救援防卫开始：击退两名真实敌人！",
                veteran?"Veteran siege started: protect supplies from all three live raiders!":
                    "Relief defense started: defeat both attackers!");
            return true;
        }
        // Keep the exact original two-argument reflection and call
        // surface for older regional geometry regression checks.
        private bool TryOuterPatrolFeet(WorldObject post,out Point[] result)
        { return TryOuterPatrolFeetWithCount(post,out result,2); }
        private bool TryOuterPatrolFeetWithCount(WorldObject post,
            out Point[] result,int required)
        {
            result=null;
            Point center=new Point(post.x,post.y);
            if(!CanStand(center))return false;
            // Preserve original dramatic approaches first, but do not put
            // attackers in blocked pockets, inside a friend, or overlapping
            // a pre-existing living enemy. Every chosen foot must have a
            // complete route into the actual storehouse being defended.
            Point[] offsets={new Point(-2f,0),new Point(1f,-1f),
                new Point(-3f,1f),new Point(2f,1f),
                new Point(-2f,-2f),new Point(3f,0),
                new Point(0,-3f),new Point(0,3f),
                new Point(-3f,-2f),new Point(3f,2f),
                new Point(3f,-2f),new Point(-2f,3f)};
            var feet=new System.Collections.Generic.List<Point>();
            foreach(Point offset in offsets)
            {
                Point p=center+offset;
                if(!CanStand(p)||
                    (p-Position).Length<1.55f||
                    (save.companion>=0&&
                     (p-companionPosition).Length<1.55f)||
                    feet.Exists(other=>(other-p).Length<2.7f)||
                    enemies.Exists(e=>e.hp>0&&
                        (e.Position-p).Length<1.8f)||
                    !ActorHasLocalRoute(p,center,10))
                    continue;
                feet.Add(p);
                if(feet.Count==required)break;
            }
            if(feet.Count!=required)return false;
            result=feet.ToArray();
            return true;
        }
        private void TickOuterPatrol(float dt)
        {
            if(outerPatrolRoom<0)return;
            outerPatrolBraceFlash=Math.Max(0f,outerPatrolBraceFlash-dt);
            outerPatrolGuardRouteCheck=Math.Max(0f,
                outerPatrolGuardRouteCheck-dt);
            if(outerPatrolRoom!=save.room||Dying)
            {
                ResetOuterPatrol();return;
            }
            if((Position-outerPatrolPost).Length>12f)
            {
                ResetOuterPatrol();revision++;
                Notify("离开救援岗防区，防卫结束。物资不会永久丢失。",
                    "Defense perimeter abandoned. Relief supplies are still available for another attempt.");
                return;
            }
            outerPatrolTime=Math.Max(0f,outerPatrolTime-dt);
            if(outerPatrolTime<=0f)
            {
                ResetOuterPatrol();revision++;
                Notify("防卫时间结束，救援岗尚未守住。可以重新挑战。",
                    "Relief defense timed out. Retry the outpost when ready.");
                return;
            }
            // Any unexpected removal aborts the attempt. Only registered
            // lethal damage against BOTH owned attackers can settle it.
            bool presentA=enemies.Exists(e=>e.id==outerPatrolEnemyA);
            bool presentB=enemies.Exists(e=>e.id==outerPatrolEnemyB);
            bool presentC=outerPatrolVeteran&&
                enemies.Exists(e=>e.id==outerPatrolEnemyC);
            if((!presentA&&!outerPatrolADead)||
                (!presentB&&!outerPatrolBDead)||
                (outerPatrolVeteran&&!presentC&&!outerPatrolCDead))
            {
                ResetOuterPatrol();
                Notify("守卫战中断：敌人无故消失不算击退。",
                    "Defense interrupted: a missing enemy is never an earned kill.");
                return;
            }
            if(!outerPatrolADead||!outerPatrolBDead||presentA||presentB||
                (outerPatrolVeteran&&(!outerPatrolCDead||presentC)))return;
            int completedRoom=outerPatrolRoom;
            bool veteranWin=outerPatrolVeteran;
            bool flawless=veteranWin&&outerPatrolStock>=90f&&
                !outerPatrolPatched&&!outerPatrolBraceBuilt&&
                !outerPatrolGuardBlockedStock;
            ResetOuterPatrol();
            if(veteranWin)
            {
                bool newMedal=flawless&&!OuterPatrolFlawlessDone(completedRoom);
                if(newMedal)
                {
                    Flag("outer_patrol_flawless_"+completedRoom);
                    if(OuterPatrolFlawlessWins==8&&
                        !Has("outer_patrol_flawless_all"))
                    {
                        Flag("outer_patrol_flawless_all");
                        CreditSkillPoints(1);
                    }
                    RecordMoment("精英救援岗取得真正的无损防卫：未使用路障和抢修，物资保持90%以上。",
                        "Earned flawless relief watch: three raiders defeated, no barricade or patch, >=90% real stock.");
                }
                if(OuterPatrolVeteranDone(completedRoom))
                {
                    revision++;sound="reward";
                    Notify(newMedal?
                            "重战精英三人组成功，获得本地无损守卫徽章！":
                            "精英三人组已被实际击退，重演无重复奖励。",
                        newMedal?
                            "Flawless defense medal earned on real replay!":
                            "All three veteran raiders defeated; replay offers no repeat loot.");
                    return;
                }
                Flag("outer_patrol_veteran_"+completedRoom);
                CreditCoins(26);CreditShards(2);AddXp(60);
                if(OuterPatrolVeteranWins==8&&!Has("outer_patrol_veteran_all"))
                { Flag("outer_patrol_veteran_all");CreditSkillPoints(1); }
                revision++;sound="reward";
                RecordMoment("精英补给守卫战已胜利：重甲袭击者确实被击退，哨岗获得强化。",
                    "Veteran cache defense cleared in live combat; field refuge staffing improved.");
                Notify("精英防卫完成：硬币 +26、碎片 +2、经验 +60；本地医疗强化。"+
                        (newMedal?" 无损守卫徽章 +1。":""),
                    "Veteran defense cleared: +26 coins, +2 shards, +60 XP; refuge upgraded."+
                        (newMedal?" Flawless watch medal earned.":""));
                return;
            }
            if(OuterPatrolDone(completedRoom))return;
            Flag("outer_patrol_done_"+completedRoom);
            CreditCoins(14);CreditShards(1);
            AddXp(28);
            if(OuterPatrolWins==8&&!Has("outer_patrol_all"))
            { Flag("outer_patrol_all");CreditSkillPoints(1); }
            revision++;sound="reward";
            RecordMoment("八地救援岗中的第 "+(completedRoom+1)+" 处已经守住：真正的危险被击退，居民不再只能等待。",
                "A relief outpost in region "+(completedRoom+1)+
                " has been defended against actual attackers. The supplies can reach residents.");
            Notify("救援岗已守住：硬币 +14、碎片 +1、经验 +28。",
                "Relief post defended: +14 coins, +1 shard, +28 XP.");
        }
    }
}
