using System;
namespace Starfall
{
    // A live carried ember connecting three old highland settlements.
    // Transient mission and guard state never changes SaveData v2.
    public sealed partial class GameCore
    {
        public int FrostSignalStage { get; private set; }
        public float FrostSignalTime { get; private set; }
        public float FrostSignalFlame { get; private set; }
        // Short-lived protection, intentionally absent from v1/v2 SaveData.
        public float FrostSignalWindbreakTime { get; private set; }
        // Real final-hearth arrival banks the transported fire before the
        // player chooses its permanent civic use. This is live mission state,
        // never a new SaveData field or a remotely claimable reward.
        public bool FrostSignalAtHearth { get; private set; }
        // Exact real hearth anchor captured when the transported flame lands.
        // The live renderer can use it without constructing the entire quest
        // journal on every frame; it is reset with the transient mission.
        public Point FrostSignalBankedFoot { get; private set; }
        private int frostSignalConfirmedKills;
        // Exactly two original actors own this physical ember encounter.
        // An unrelated creature with a reused 91400/91401 numeric ID
        // cannot fake a guard kill, drain flame or keep missing raiders
        // registered through the old ID-only liveness check.
        private Enemy frostSignalActorA,frostSignalActorB;
        private void OnFrostSignalEnemyDefeated(Enemy enemy)
        {
            if (FrostSignalStage != 2 || save.room != 7 ||
                !IsFrostSignalEnemy(enemy)) return;
            int index = enemy.id - 91400;
            if (index >= 0 && index < 2)
                frostSignalConfirmedKills |= 1 << index;
        }
        private bool FrostSignalGuardsDefeated
        { get { return frostSignalConfirmedKills == 3; } }
        public bool FrostSignalActive { get { return FrostSignalStage>0; } }
        // A recruited companion can physically screen ONE guard's wind lane
        // while walking alongside the person carrying the fire. Neither
        // distant party membership nor a wall grants a passive flame aura.
        private bool FrostCompanionBlocksGuard(Enemy guard)
        {
            if(guard==null||guard.hp<=0||FrostSignalStage!=2||
                save.room!=7||save.companion<0||save.companion>=3||
                !Has("companion_"+save.companion)||Dying ||
                !CanStand(companionPosition)||!CanStand(Position))return false;
            Point lane=Position-guard.Position;
            float length=lane.Length;
            if(length<2f ||
                (companionPosition-Position).Length>3f)return false;
            Point offset=companionPosition-guard.Position;
            float forward=(offset.x*lane.x+offset.y*lane.y)/length;
            float lateral=Math.Abs(offset.x*lane.y-offset.y*lane.x)/length;
            return forward>.7f&&forward<length-.5f&&lateral<.7f &&
                ClearAttackPath(guard.Position,companionPosition)&&
                ClearAttackPath(companionPosition,Position);
        }
        public int FrostSignalGuardsScreened
        {
            get
            {
                if(FrostSignalStage!=2)return 0;
                int count=0;
                foreach(Enemy guard in enemies)
                    if(IsFrostSignalEnemy(guard)&&guard.hp>0&&
                        (guard.Position-Position).Length<5f &&
                        ClearAttackPath(guard.Position,Position) &&
                        FrostCompanionBlocksGuard(guard))count++;
                return count;
            }
        }
        public bool FrostSignalComplete { get { return Has("frostsignal_complete"); } }
        public int FrostSignalOutcome
        { get { return Has("frostsignal_clinic")?1:Has("frostsignal_guides")?2:0; } }
        // A quality delivery or one honest later repair strengthens both
        // public-service variants. Existing completed v2 saves lack this flag
        // and retain their original service until voluntarily upgraded.
        public bool FrostSignalHearthBright
        { get { return FrostSignalComplete && Has("frostsignal_bright"); } }
        public bool IsFrostSignalEnemy(Enemy e)
        {
            return e!=null&&FrostSignalStage==2&&save.room==7&&
                (System.Object.ReferenceEquals(e,frostSignalActorA)||
                 System.Object.ReferenceEquals(e,frostSignalActorB));
        }
        private WorldObject FrostSignalPost(int number)
        {
            if(number<1||number>3)return null;
            foreach(WorldObject o in data.maps[7].objects)
                if(o.kind=="frostsignal"&&o.id=="frostsignal_"+number)return o;
            return null;
        }
        private void ResetFrostSignal()
        {
            enemies.RemoveAll(IsFrostSignalEnemy);
            projectiles.RemoveAll(p=>p.frostSignalOwned);
            hazards.RemoveAll(h=>h.frostSignalOwned);
            effects.RemoveAll(e=>e.frostSignalOwned);
            FrostSignalStage=0;frostSignalConfirmedKills=0;
            frostSignalActorA=frostSignalActorB=null;
            FrostSignalAtHearth=false;FrostSignalBankedFoot=new Point();
            FrostSignalTime=FrostSignalFlame=FrostSignalWindbreakTime=0;
        }
        // The authored switchyard positions are preferred, not permission to
        // materialize a raider inside the player, companion or a living enemy.
        // Keep alternates local to the real ambush rather than teleporting a
        // quest guard to a disconnected location elsewhere on the map.
        private static readonly Point[] frostSignalGuardOffsets={
            new Point(0f,0f),new Point(0f,1.5f),
            new Point(-1.5f,0f),new Point(1.5f,0f),
            new Point(0f,-1.5f),new Point(-1.5f,1.5f),
            new Point(1.5f,1.5f),new Point(-1.5f,-1.5f),
            new Point(1.5f,-1.5f),
            // A solid one-tile moat around an otherwise walkable pocket
            // makes every 1.5-tile fallback touch the surrounding wall.
            // Local 2.5-tile candidates can still reach the active battle
            // without spawning guards at a distant unrelated world location.
            new Point(0f,2.5f),new Point(0f,-2.5f),
            new Point(-2.5f,0f),new Point(2.5f,0f)
        };
        private bool TryFrostSignalGuardFoot(Point origin,Point other,
            bool hasOther,out Point result)
        {
            foreach(Point offset in frostSignalGuardOffsets)
            {
                Point foot=origin+offset;
                if(!CanStand(foot) ||
                    (foot-Position).Length<1.25f ||
                    (save.companion>=0 &&
                     (foot-companionPosition).Length<1.25f) ||
                    (hasOther && (foot-other).Length<1.25f) ||
                    // A legal isolated tile behind solid scenery makes the
                    // timed two-guard fight impossible. Require a genuine
                    // local four-neighbor route from guard to player, just
                    // as Boss reinforcements do, before committing either ID.
                    !BossSummonHasLocalRoute(foot,Position))continue;
                bool occupied=false;
                foreach(Enemy e in enemies)
                    if(e.hp>0&&(foot-e.Position).Length<1.25f)
                    {occupied=true;break;}
                if(occupied)continue;
                result=foot;return true;
            }
            result=new Point();return false;
        }
        private bool SpawnFrostSignalGuards()
        {
            Point[] desired={new Point(62.5f,41.5f),new Point(65.5f,42.5f)};
            Point first,second;
            // Validate BOTH feet before adding EITHER guard. A crowded
            // junction remains retryable, without a half-spawned encounter.
            if(!TryFrostSignalGuardFoot(desired[0],new Point(),false,out first) ||
               !TryFrostSignalGuardFoot(desired[1],first,true,out second))
                return false;
            // Validate both physical feet before atomically registering
            // the exact two actors whose real deaths will unlock the road.
            Enemy firstGuard=new Enemy(91400,0,first.x,first.y,95);
            Enemy secondGuard=new Enemy(91401,2,second.x,second.y,112);
            firstGuard.timer=.7f;secondGuard.timer=.95f;
            frostSignalActorA=firstGuard;frostSignalActorB=secondGuard;
            enemies.Add(firstGuard);enemies.Add(secondGuard);
            frostSignalConfirmedKills=0;
            return true;
        }
        private void TickFrostSignal(float dt)
        {
            if(!FrostSignalActive)return;
            if(save.room!=7||Dying||practicing)
            {ResetFrostSignal();revision++;return;}
            // Reaching the real southern public hearth secures the live
            // fire. On that instant its carried timer and cold loss stop;
            // the player still must interact on the same physical side to
            // choose the clinic or road beacon. Unlike simply standing
            // within a radial marker, PhysicalRefugeReach rejects walls.
            WorldObject southHearth=FrostSignalStage==3 && !FrostSignalAtHearth ?
                FrostSignalPost(3):null;
            if(southHearth!=null &&
                FrostSignalTime>0 && FrostSignalFlame>0 &&
                CanReachFrostSignalPost(southHearth))
            {
                FrostSignalAtHearth=true;
                FrostSignalBankedFoot=new Point(southHearth.x,southHearth.y);
                revision++;sound="quest";
                Notify("民用余火已安全送到暖站。请在这里决定建立疗养站还是巡路灯网。",
                    "Civic ember delivered safely. Choose clinic or road beacons at the hearth.");
            }
            if(FrostSignalAtHearth)return;
            if (FrostSignalStage == 2)
            {
                for (int index = 0; index < 2; index++)
                {
                    if ((frostSignalConfirmedKills & (1 << index)) != 0 ||
                        enemies.Contains(index==0?frostSignalActorA:frostSignalActorB)) continue;
                    ResetFrostSignal(); revision++;
                    Notify("巡防守卫异常离场，未确认击败。火种返回西部站，可重新尝试。",
                        "A guard vanished without defeat. The ember was returned to the west post; retry for free.");
                    return;
                }
            }
            FrostSignalTime-=dt;
            float severeCold=save.cold>=60?.65f:0f;
            float drain=.65f+severeCold;
            if(FrostSignalStage==2)
                foreach(Enemy enemy in enemies)
                    if(IsFrostSignalEnemy(enemy)&&enemy.hp>0&&
                        (enemy.Position-Position).Length<5f&&
                        // The carried fire is threatened by real adjacent
                        // combat, not a guard sealed behind solid stone.
                        ClearAttackPath(enemy.Position,Position) &&
                        !FrostCompanionBlocksGuard(enemy))
                        drain+=.58f;
            // Shield only the remaining fraction of this simulation frame.
            float protectedDt=Math.Min(dt,FrostSignalWindbreakTime);
            FrostSignalWindbreakTime=Math.Max(0,FrostSignalWindbreakTime-dt);
            FrostSignalFlame=Math.Max(0,FrostSignalFlame-dt*drain+
                protectedDt*severeCold*.8f);
            if(FrostSignalTime<=0||FrostSignalFlame<=0)
            {
                ResetFrostSignal();revision++;
                Notify("余火已熄灭。可回到原站重新出发，之前的材料不退还。",
                    "The ember burned out. Return to the source to retry; already used resources are not refunded.");
            }
        }
        // The live ember and one-time civilian outcome belong to
        // physical mountain posts. Revalidate the near-side path when an
        // opened dialogue is actually committed after terrain changes.
        private bool CanReachFrostSignalPost(WorldObject site)
        { return site!=null && PhysicalRefugeReach(
            new Point(site.x,site.y),1.8001f); }
        private bool TalkFrostSignal(WorldObject o)
        {
            if(o.kind!="frostsignal")return false;
            int at=0;for(int i=1;i<=3;i++)if(o.id=="frostsignal_"+i)at=i;
            if(at==0||save.room!=7||
                !CanReachFrostSignalPost(o))return true;
            if(!Has("boss_7"))
            {
                Dialog(o.name,o.en,
                    "旧霜钟守护者仍控制山路火讯。先完成高原主线，居民才能接管。",
                    "The old Frostchime guardian still controls the signal fires. Complete the original guardian challenge first.",
                    C("以后再来","Return later","close"));return true;
            }
            if(FrostSignalComplete)
            {
                string zh=FrostSignalOutcome==1?
                    "居民在三处旧火讯站照料伤者，只有路上安全时才提供救护。":
                    "巡路队在三处旧火讯站为远行者补充耐力，只有路上安全时才开放。";
                string en=FrostSignalOutcome==1?
                    "Residents treat the injured at three old signal posts, when the road is safe.":
                    "Road crews restore travelers' energy at three signal posts, when the road is safe.";
                if(FrostSignalHearthBright)
                {
                    zh+="\n\n火焰稳定，暖站的照护比初建时更可靠。";
                    en+="\n\nThe banked flame now supports stronger public care.";
                    Dialog(o.name,o.en,zh,en,
                        C("阅读居民记录","Read the civic record","close"));
                }
                else if(at==3)
                {
                    zh+="\n\n火苗仍弱。交来两株月光草，我们就能把公共炉火养稳。";
                    en+="\n\nThe flame is still weak. Two Moonherbs can steady the public fire.";
                    Dialog(o.name,o.en,zh,en,
                        C("月光草2 · 养稳炉火","Tend flame · 2 Moonherbs","frostsignal_tend"),
                        C("暂不修复","Not now","close"));
                }
                else Dialog(o.name,o.en,zh,en,
                    C("阅读居民记录","Read the civic record","close"));
                return true;
            }
            if(!FrostSignalActive)
            {
                if(at!=1)
                {
                    Dialog(o.name,o.en,"火种仍在西部守灯岗，必须从那里亲自取出。",
                        "The ember remains at the western station. Collect it there in person.",
                        C("返回火种源","Return to source","close"));return true;
                }
                Dialog(o.name,o.en,
                    "从西部岗取火，100秒内送到南侧暖站。严寒会耗损火种，转折台有两名守卫，战斗时火种烧得更快。击败他们后可用药草或耐力补火，也可以直接赶路。送达时余温达到70，暖站会更可靠；火苗不足也能日后用两株月光草养稳。失败可重新取火。",
                    "Carry a live ember from the west to the southern civilian hearth within 100s. Real cold erodes the flame and two actual raiders contest the switchyard; fighting nearby burns it faster. After defeating the guards, choose supplies or continue. Deliver 70+ flame for stronger public care, or tend the hearth later with two Moonherbs. Failed runs can be retried.",
                    C("领取余火","Carry the ember","frostsignal_start"),
                    C("暂缓","Not yet","close"));return true;
            }
            if(at==1)
            {
                Dialog(o.name,o.en,
                    "余火剩余 "+FrostSignalFlame.ToString("0")+"；限时 "+FrostSignalTime.ToString("0")+" 秒。可在这里明确放弃并重试。",
                    "Ember "+FrostSignalFlame.ToString("0")+"; "+FrostSignalTime.ToString("0")+"s left. You may abort here.",
                    C("继续","Continue","close"),
                    C("放回火种","Return the ember","frostsignal_abort"));return true;
            }
            if(at==2)
            {
                if(FrostSignalStage==1)
                {
                    if(!SpawnFrostSignalGuards())
                    {
                        Notify("守卫通路暂时受阻。","The guard approach is obstructed.");return true;
                    }
                    FrostSignalStage=2;revision++;
                }
                if(FrostSignalStage!=2)
                {
                    Notify("此处已完成交接，沿原路继续。",
                        "This junction is complete. Continue along the road.");return true;
                }
                if(enemies.Exists(IsFrostSignalEnemy) ||
                    !FrostSignalGuardsDefeated)
                {
                    Dialog(o.name,o.en,
                        "转折台仍被两名旧守卫占据。保护身上余火，在真实战斗中解决守卫。",
                        "Two obsolete guards still contest the switch. Defeat both in actual combat while the ember keeps cooling.",
                        C("继续战斗","Fight the guards","close"));return true;
                }
                Dialog(o.name,o.en,
                    "余温 "+FrostSignalFlame.ToString("0")+"。月光草可回升35，20耐力可回升22；或消耗20耐力加固火种，接下来25秒抵御80%的严寒额外损耗。也可保留补给。",
                    "Ember "+FrostSignalFlame.ToString("0")+". Moonherb adds 35 flame, 20 energy adds 22, or spend 20 energy to shield 80% of severe-cold loss for the next 25s. You may conserve supplies.",
                    C("月光草1 · 余温+35","Moonherb · +35 flame","frostsignal_herb"),
                    C("耐力20 · 余温+22","20 energy · +22 flame","frostsignal_energy"),
                    C("耐力20 · 防风护火25秒","20 energy · 25s windbreak","frostsignal_windbreak"),
                    C("不补充，继续","Conserve supplies","frostsignal_free"));return true;
            }
            if(at==3&&FrostSignalStage==3)
            {
                Dialog(o.name,o.en,
                    "最后一簇火已经抵达居民暖站。你希望优先为寒地伤者提供救护，还是为巡路队提供照明和补给？",
                    "The live ember has reached the civilian hearth. Will it primarily support injured residents or equip night road crews?",
                    C("建立平民疗养站","Public clinic · healing","frostsignal_clinic"),
                    C("建立巡路补给灯网","Road beacon · stamina","frostsignal_guides"));return true;
            }
            Notify("先在转折台清除守卫，再送火种到南侧暖站。",
                "Clear the switchyard guards before delivering the fire south.");
            return true;
        }
        private bool ChooseFrostSignal(string action)
        {
            if(!action.StartsWith("frostsignal_",StringComparison.Ordinal))return false;
            if(save.room!=7||Dying||practicing)return true;
            WorldObject west=FrostSignalPost(1),mid=FrostSignalPost(2),end=FrostSignalPost(3);
            if(west==null||mid==null||end==null)return true;
            if(action=="frostsignal_tend")
            {
                // Commit must recheck the physical hearth side, live safety
                // and actual inventory. This also offers legacy v2 saves a
                // one-time voluntary upgrade without migrating their schema.
                if(!FrostSignalComplete||FrostSignalHearthBright||
                    !CanReachFrostSignalPost(end)||!SafeTravel||
                    FieldWorkPreventsRest)return true;
                if(save.inventory[2]<2)
                {
                    Notify("需要两株月光草才能把公共炉火养稳。",
                        "Bring two Moonherbs to tend the public flame.");
                    return true;
                }
                save.inventory[2]-=2;Flag("frostsignal_bright");
                revision++;sound="reward";
                RecordMoment("居民用月光草养稳了霜钟暖站的火，三处公共火讯站的照护更加可靠。",
                    "Residents tended Frostchime's public fire with Moonherb, strengthening all three signal posts.");
                Notify("暖站已修复。三处公共火讯站现在提供更强的安全照护。",
                    "Hearth restored. The three public posts now offer stronger care when safe.");
                return true;
            }
            if(action=="frostsignal_start")
            {
                if(FrostSignalActive||FrostSignalComplete||!Has("boss_7")||
                    FieldWorkPreventsRest||!SafeTravel||
                    !CanReachFrostSignalPost(west))return true;
                FrostSignalStage=1;FrostSignalTime=100;FrostSignalFlame=85;
                FrostSignalAtHearth=false;FrostSignalBankedFoot=new Point();
                Flag("frostsignal_started");revision++;sound="quest";
                Notify("火种已领取。沿旧山道前往转折台，击败守卫。",
                    "Ember secured. Walk to the switchyard and defeat the guards.");
                return true;
            }
            if(action=="frostsignal_abort")
            {
                if(!FrostSignalActive||
                    !CanReachFrostSignalPost(west))return true;
                ResetFrostSignal();revision++;
                Notify("余火归还。可以重新取火。",
                    "The ember is returned. Another attempt is available.");
                return true;
            }
            if(action=="frostsignal_herb"||action=="frostsignal_energy"||
                action=="frostsignal_windbreak"||action=="frostsignal_free")
            {
                if(FrostSignalStage!=2||enemies.Exists(IsFrostSignalEnemy)||
                    !CanReachFrostSignalPost(mid))return true;
                if (!FrostSignalGuardsDefeated)
                {
                    ResetFrostSignal(); revision++;
                    Notify("转折台守卫未被确认击败。回西部岗重新取火。",
                        "Switchyard guards were not confirmed defeated. Restart from the west station.");
                    return true;
                }
                if(action=="frostsignal_herb")
                {
                    if(save.inventory[2]<1){Notify("月光草不足。","You need one Moonherb.");return true;}
                    save.inventory[2]--;FrostSignalFlame=Math.Min(100,FrostSignalFlame+35);
                }
                if(action=="frostsignal_energy")
                {
                    if(save.energy<20){Notify("需要20耐力。","You need 20 energy.");return true;}
                    save.energy-=20;FrostSignalFlame=Math.Min(100,FrostSignalFlame+22);
                }
                if(action=="frostsignal_windbreak")
                {
                    if(save.energy<20){Notify("需要20耐力来加固火种。","Need 20 energy to bank the flame.");return true;}
                    save.energy-=20;
                    FrostSignalWindbreakTime=25f;
                }
                FrostSignalStage=3;revision++;sound="reward";
                Notify(action=="frostsignal_windbreak"?
                    "火种已防风加固25秒！沿山道前往南侧暖站。":"转折台开放，携带余火去南侧暖站。",
                    action=="frostsignal_windbreak"?
                    "The ember is sheltered for 25s! Carry it south.":"Junction cleared. Carry the ember south to the civilian hearth.");
                return true;
            }
            if(action=="frostsignal_clinic"||action=="frostsignal_guides")
            {
                if(FrostSignalStage!=3||FrostSignalComplete||
                    !CanReachFrostSignalPost(end))return true;
                float quality=FrostSignalFlame;
                ResetFrostSignal();
                Flag(action);Flag("frostsignal_complete");
                if(quality>=70f)Flag("frostsignal_bright");
                CreditCoins(quality>=50?65:55);CreditShards(2);AddXp(85);
                revision++;sound="reward";
                RecordMoment(action=="frostsignal_clinic"?
                    "霜钟高原居民重新点燃了公共火种，让寒夜里受伤的人找到疗养处。":
                    "霜钟高原巡路队获得了公共讯号火种，夜行者终于有了亮灯的安全山道。",
                    action=="frostsignal_clinic"?
                    "Frostchime villagers relit a public hearth to care for the injured.":
                    "The Frostchime roadwardens restored public signal lights for night travelers.");
                Notify("民用火种重燃！硬币+"+(quality>=50?65:55)+"、碎片+2、经验+85；两种暖站服务择一永久开启。",
                    "The public fire is restored! +"+(quality>=50?65:55)+" coins, +2 shards, +85 XP; one permanent hearth service is unlocked.");
                return true;
            }
            return true;
        }
        public bool FrostSignalRefuge
        {
            get
            {
                if(!FrostSignalComplete||save.room!=7)return false;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="frostsignal"&&
                        PhysicalRefugeReach(new Point(o.x,o.y),3.2f))return true;
                return false;
            }
        }
    }
}
