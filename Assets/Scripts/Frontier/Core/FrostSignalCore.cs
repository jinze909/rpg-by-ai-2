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
        private int frostSignalConfirmedKills;
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
        public bool FrostSignalComplete { get { return Has("frostsignal_complete"); } }
        public int FrostSignalOutcome
        { get { return Has("frostsignal_clinic")?1:Has("frostsignal_guides")?2:0; } }
        public bool IsFrostSignalEnemy(Enemy e)
        { return e!=null&&e.id>=91400&&e.id<91410; }
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
            new Point(1.5f,-1.5f)
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
            Point[] posts={first,second};
            for(int i=0;i<posts.Length;i++)
            {
                Enemy e=new Enemy(91400+i,i==0?0:2,
                    posts[i].x,posts[i].y,i==0?95:112);
                e.timer=.7f+i*.25f;enemies.Add(e);
            }
            frostSignalConfirmedKills=0;
            return true;
        }
        private void TickFrostSignal(float dt)
        {
            if(!FrostSignalActive)return;
            if(save.room!=7||Dying||practicing)
            {ResetFrostSignal();revision++;return;}
            if (FrostSignalStage == 2)
            {
                for (int index = 0; index < 2; index++)
                {
                    if ((frostSignalConfirmedKills & (1 << index)) != 0 ||
                        enemies.Exists(e => e.id == 91400 + index)) continue;
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
                        (enemy.Position-Position).Length<5f)drain+=.58f;
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
        private bool TalkFrostSignal(WorldObject o)
        {
            if(o.kind!="frostsignal")return false;
            int at=0;for(int i=1;i<=3;i++)if(o.id=="frostsignal_"+i)at=i;
            if(at==0||save.room!=7||
                (Position-new Point(o.x,o.y)).Length>1.8f)return true;
            if(!Has("boss_7"))
            {
                Dialog(o.name,o.en,
                    "旧霜钟守护者仍控制山路火讯。先完成高原主线，居民才能接管。",
                    "The old Frostchime guardian still controls the signal fires. Complete the original guardian challenge first.",
                    C("以后再来","Return later","close"));return true;
            }
            if(FrostSignalComplete)
            {
                Dialog(o.name,o.en,
                    FrostSignalOutcome==1?
                    "这里的公共火种现在由居民维护，暖站附近安全时能为伤者恢复生命。":
                    "这里的灯火归巡路队维护，暖站附近安全时能为远行者恢复耐力。",
                    FrostSignalOutcome==1?
                    "Civilians now tend this public fire, with safe healing near the hearth.":
                    "Roadwardens now tend the signal flames, restoring stamina near safe hearths.",
                    C("阅读居民记录","Read the civic record","close"));return true;
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
                    "从西部岗取得余火，100秒内送到南侧暖站。真实寒冷会使火种衰减，旧转折台有两名巡防敌人；战斗时余火耗得更快。击败守卫后，可以选择花一株药草补充火种、花耐力节约药草、或者直接冒险前进。失败可重新尝试。",
                    "Carry a live ember from the west to the southern civilian hearth within 100s. Real cold erodes the flame and two actual raiders contest the switchyard; fighting nearby burns it faster. After defeating the guards, spend a Moonherb or energy to replenish it, or continue without supplies. Failed runs may be retried.",
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
            if(action=="frostsignal_start")
            {
                if(FrostSignalActive||FrostSignalComplete||!Has("boss_7")||
                    FieldWorkPreventsRest||!SafeTravel||
                    (Position-new Point(west.x,west.y)).Length>1.8f)return true;
                FrostSignalStage=1;FrostSignalTime=100;FrostSignalFlame=85;
                Flag("frostsignal_started");revision++;sound="quest";
                Notify("火种已领取。沿旧山道前往转折台，击败守卫。",
                    "Ember secured. Walk to the switchyard and defeat the guards.");
                return true;
            }
            if(action=="frostsignal_abort")
            {
                if(!FrostSignalActive||
                    (Position-new Point(west.x,west.y)).Length>1.8f)return true;
                ResetFrostSignal();revision++;
                Notify("余火归还。可以重新取火。",
                    "The ember is returned. Another attempt is available.");
                return true;
            }
            if(action=="frostsignal_herb"||action=="frostsignal_energy"||
                action=="frostsignal_windbreak"||action=="frostsignal_free")
            {
                if(FrostSignalStage!=2||enemies.Exists(IsFrostSignalEnemy)||
                    (Position-new Point(mid.x,mid.y)).Length>1.8f)return true;
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
                    (Position-new Point(end.x,end.y)).Length>1.8f)return true;
                float quality=FrostSignalFlame;
                ResetFrostSignal();
                Flag(action);Flag("frostsignal_complete");
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
                        (Position-new Point(o.x,o.y)).Length<3.2f)return true;
                return false;
            }
        }
    }
}
