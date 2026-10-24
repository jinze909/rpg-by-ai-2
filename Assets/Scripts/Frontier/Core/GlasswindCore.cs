using System;
using System.Collections.Generic;

namespace Starfall
{
    // Three non-farmable physical desert defense encounters. Active security,
    // capture meters and timers are transient; existing v2 flags hold progress.
    public sealed partial class GameCore
    {
        public int GlasswindSite { get; private set; }
        public int GlasswindWave { get; private set; }
        public float GlasswindTime { get; private set; }
        public float GlasswindCharge { get; private set; }
        private float glasswindSurgeTimer;
        private int glasswindSurgeCycle;
        // A crowded second-wave arena is a retryable transient state,
        // not permission to discard four seconds of actual player defense.
        private bool glasswindReinforcementsDeferred;
        // Two real kills from EACH wave, never inferred from an empty list.
        private int glasswindConfirmedKills;
        // A physical, one-use pressure vent gives the defender a tactical
        // escape without bypassing either real combat wave or the 8s capture.
        // It is transient like the contested valve, never in SaveData v2.
        private bool glasswindVentSpent;
        public bool GlasswindVentSpent
        { get { return GlasswindSite!=0 && glasswindVentSpent; } }
        public const float GlasswindVentEnergy=25f;
        private bool GlasswindWavesDefeated { get { return glasswindConfirmedKills==15; } }
        private void OnGlasswindEnemyDefeated(Enemy enemy)
        {
            if(GlasswindSite==0||save.room!=6||!IsGlasswindEnemy(enemy))return;
            int index=enemy.id-(89810+GlasswindSite*10+2);
            if(index>=0&&index<GlasswindWave*2&&index<4)
                glasswindConfirmedKills|=1<<index;
        }

        public bool IsGlasswindEnemy(Enemy e)
        { return e != null && e.id >= 89810 && e.id < 89900; }

        private Point GlasswindPoint(int site)
        { return ObjectPoint(6,"glasswindrelay"+site,new Point(-100,-100)); }

        public bool GlasswindRefuge
        {
            get
            {
                if(save.room!=6 || GlasswindSite!=0 || !SafeTravel) return false;
                for(int i=1;i<=3;i++)
                    if(Has("glasswind_"+i) &&
                        PhysicalRefugeReach(GlasswindPoint(i),3.1f))
                        return true;
                return false;
            }
        }

        private void ResetGlasswind()
        {
            enemies.RemoveAll(IsGlasswindEnemy);
            // Abort also retires hostile arrows already in midair.
            projectiles.RemoveAll(p => p.glasswindOwned);
            hazards.RemoveAll(h => h.glasswindOwned);
            effects.RemoveAll(e => e.glasswindOwned);
            glasswindSurgeTimer=0;glasswindSurgeCycle=0;
            glasswindReinforcementsDeferred=false;
            glasswindConfirmedKills=0;glasswindVentSpent=false;
            GlasswindSite=GlasswindWave=0;
            GlasswindTime=GlasswindCharge=0;
        }

        private bool SpawnGlasswindWave(int wave)
        {
            Point station=GlasswindPoint(GlasswindSite);
            Point[] offsets={new Point(3.2f,0),new Point(-3.2f,0),
                new Point(0,3.2f),new Point(0,-3.2f),
                new Point(2.8f,2.8f),new Point(-2.8f,-2.8f),
                new Point(-2.8f,2.8f),new Point(2.8f,-2.8f)};
            var spots=new List<Point>();
            for(int n=0;n<offsets.Length && spots.Count<2;n++)
            {
                Point p=station+offsets[(n+(GlasswindSite-1)*2+wave)%offsets.Length];
                // A legal point still may overlap a living bystander or the
                // companion, or sit in a room disconnected from the valve.
                // Verify the real four-neighbor body-foot route rather than
                // accepting NavigationPath's fallback nearest partial path.
                if(!CanStand(p) || !CanStand(station) ||
                    !ClearAttackPath(station,p) ||
                    !BossSummonHasLocalRoute(p,station) ||
                    (p-Position).Length<1.75f ||
                    (p-station).Length<1.65f ||
                    (save.companion>=0 &&
                     (p-companionPosition).Length<1.55f))
                    continue;
                bool occupied=false;
                foreach(Enemy existing in enemies)
                    if(existing.hp>0&&(p-existing.Position).Length<1.55f)
                    { occupied=true; break; }
                if(occupied)continue;
                bool distinct=true;
                foreach(Point used in spots)
                    if((p-used).Length<2.3f) distinct=false;
                if(distinct)spots.Add(p);
            }
            if(spots.Count!=2)return false;
            for(int i=0;i<2;i++)
            {
                int kind=wave==2 && (i==1 || GlasswindSite==3)?2:0;
                float hp=wave==2?(GlasswindSite==3?148:106):72;
                var foe=new Enemy(89810+GlasswindSite*10+wave*2+i,
                    kind,spots[i].x,spots[i].y,hp);
                foe.timer=.85f+i*.24f;
                enemies.Add(foe);
            }
            GlasswindWave=wave;
            return true;
        }

        private bool TalkGlasswind(WorldObject o)
        {
            if(o.kind=="glasswindkeeper")
            {
                if(save.room!=0)return true;
                if(!Has("desert_report"))
                    Dialog(o.name,o.en,
                        "旧物流网锁住了沙海的风压图。先完成当地调查，再来帮我们修一条无需通行证的遮阴路。",
                        "The old freight network locked the desert wind chart. Finish the local report before opening a shaded road for everyone.",
                        C("稍后回来","Return later","close"));
                else if(Has("glasswind_complete"))
                    Dialog(o.name,o.en,
                        "三座风阀不再区别货车与旅人。沙海上的每个人都能进入公共的阴凉。",
                        "The three valves no longer distinguish freight from people. Every traveler can use their public shade.",
                        C("继续旅行","Keep traveling","close"));
                else if(Has("glasswind_all_sealed"))
                    Dialog(o.name,o.en,
                        "三条风压回路都已封存。请把路线公布给居民，而不是交回旧货运网。",
                        "All three circuits are sealed. Publish the map for residents, not the freight authority.",
                        C("公开公共风路图","Publish the public wind chart","glasswind_publish"),
                        C("稍后","Later","close"));
                else if(!Has("glasswind_started"))
                    Dialog(o.name,o.en,
                        "我是测绘员米娅。北沙、东脊、南沙的三座风压阀本可为旅人遮阴，却被旧货运权限占据。守住风圈完成校准、击退两波机器，再封存线路。愿意帮忙吗？",
                        "I'm Mia, a cartographer. Three valves in the north sands, eastern ridge and south dunes could shelter travelers, but freight locks seized them. Hold their rings, defeat two waves of machines and seal the lines. Will you help?",
                        C("接受玻璃风脉委托","Accept Glasswind Circuit","glasswind_accept"),
                        C("稍后","Later","close"));
                else
                    Dialog(o.name,o.en,
                        "靠近风圈会积累校准，远离会缓慢衰减。四秒时出现第二波。每座风阀都有延迟风压落点，闪到圈外再回去守点。守满八秒并清除全部守卫才能封存。超时或撤退可免费重试。",
                        "The ring charges nearby and slowly drains at a distance. Reinforcements arrive halfway through. Each relay warns of delayed pressure blasts: dodge clear and return to the ring. Hold eight seconds and defeat every guard to seal. Retries cost nothing.",
                        C("追踪风脉委托","Track the relays","glasswind_track"),
                        C("明白了","Understood","close"));
                return true;
            }
            if(!o.kind.StartsWith("glasswindrelay"))return false;
            int site;
            if(!int.TryParse(o.kind.Substring("glasswindrelay".Length),out site) ||
                site<1 || site>3 || save.room!=6)return true;
            if(!Has("glasswind_started"))
            {
                Notify("风阀仍由旧货运锁控制。去余烬镇找米娅。",
                    "Freight controls this valve. Speak with Mia in Emberfall.");
                return true;
            }
            if(Has("glasswind_"+site))
            {
                Dialog(o.name,o.en,
                    "风阀已开放。战斗结束后靠近就能令酷暑消退。",
                    "This valve is public. Approach out of combat to cool down.",
                    C("继续前行","Move on","close"));
                return true;
            }
            if(GlasswindSite!=0)
            {
                if(GlasswindSite!=site)
                {
                    Notify("另一处风阀仍在争夺中。请先完成或撤出。",
                        "Another relay is contested. Finish or withdraw there first.");
                    return true;
                }
                bool clear=GlasswindWave==2 && GlasswindCharge>=8 &&
                    GlasswindWavesDefeated && !enemies.Exists(IsGlasswindEnemy);
                Dialog(o.name,o.en,
                    "剩余 "+GlasswindTime.ToString("0")+"秒；校准 "+
                        GlasswindCharge.ToString("0.0")+"/8；第 "+
                        GlasswindWave+"/2 波。站稳、击败机器、再封存。",
                    "Time "+GlasswindTime.ToString("0")+"s; capture "+
                        GlasswindCharge.ToString("0.0")+"/8; wave "+
                        GlasswindWave+"/2. Hold, fight and seal.",
                    C(clear?"封存并开放回路":"继续守点",
                        clear?"Seal and open circuit":"Keep defending",
                        clear?"glasswind_seal":"close"),
                    !glasswindVentSpent?
                        C("手动泄压 · 25耐力 · 中断风压与近身攻击",
                          "Vent pressure · 25 energy · disrupt local attacks",
                          "glasswind_vent"):
                        C("泄压阀已用完","Pressure vent expended","close"),
                    C("撤出试炼","Withdraw","glasswind_withdraw"));
                return true;
            }
            Dialog(o.name,o.en,
                "启动后限时95秒：在风圈内累计八秒，躲避延迟风压圈，并击退两波守卫。最后手动封存；失败可重试。",
                "You have 95 seconds to hold the ring for eight seconds, dodge delayed pressure circles, and defeat two waves. Return to seal it. No farmable loot; retries are free.",
                C("开始守点","Start capture","glasswind_start_"+site),
                C("暂不开始","Not yet","close"));
            return true;
        }

        private bool ChooseGlasswind(string action)
        {
            if(action=="glasswind_accept")
            {
                if(save.room==0 && Has("desert_report") &&
                    !Has("glasswind_started") && !Dying &&
                    PhysicalRefugeReach(ObjectPoint(0,"glasswindkeeper",
                        new Point(-100,-100)),2.2f))
                {
                    Flag("glasswind_started");TrackQuest("glasswind");revision++;
                    Notify("新委托：玻璃风脉。守住沙海三座中继。",
                        "New quest: Glasswind Circuit. Defend three desert relays.");
                }
                return true;
            }
            if(action=="glasswind_track")
            {
                if(Has("glasswind_started"))TrackQuest("glasswind");
                return true;
            }
            if(action.StartsWith("glasswind_start_"))
            {
                int site;
                if(!int.TryParse(action.Substring("glasswind_start_".Length),out site) ||
                    site<1 || site>3 || save.room!=6 || !Has("glasswind_started") ||
                    Has("glasswind_"+site) || GlasswindSite!=0 || Dying ||
                    practicing || racing || FieldWorkPreventsRest || !SafeTravel ||
                    !PhysicalRefugeReach(GlasswindPoint(site),2.2001f))return true;
                GlasswindSite=site;GlasswindTime=95;GlasswindCharge=0;
                glasswindSurgeTimer=2.5f;glasswindSurgeCycle=0;
                if(!SpawnGlasswindWave(1))
                {
                    ResetGlasswind();
                    Notify("此处无法建立可通行守点区，调整位置后重试。",
                        "No reachable security arena. Reposition to retry.");
                    return true;
                }
                TrackQuest("glasswind");revision++;sound="windup";
                Notify("中继争夺开始！守住光圈，击退两轮守卫。",
                    "Relay contested! Hold the lit circle and defeat two waves.");
                return true;
            }
            if(action=="glasswind_vent")
            {
                if(save.room!=6||Dying||practicing||GlasswindSite==0||
                    glasswindVentSpent||
                    !PhysicalRefugeReach(GlasswindPoint(GlasswindSite),2.2001f))
                    return true;
                if(save.energy<GlasswindVentEnergy)
                {
                    Notify("手动泄压需要25耐力。可先恢复体力，或用闪避躲开风压。",
                        "Venting needs 25 energy. Recover first, or dodge the pressure.");
                    return true;
                }
                Point station=GlasswindPoint(GlasswindSite);
                save.energy-=GlasswindVentEnergy;
                glasswindVentSpent=true;
                // Briefly reopen a real reaction interval. Do not let the
                // normal surge timer rearm a fresh strike on the very next
                // simulation frame after the paid intervention.
                glasswindSurgeTimer=Math.Max(glasswindSurgeTimer,1.4f);
                // Wind pressure belongs to this active relay. Clearing a
                // telegraph here must not erase ambient desert hazards.
                hazards.RemoveAll(h=>h.glasswindOwned);
                effects.RemoveAll(fx=>fx.glasswindOwned && fx.kind=="danger");
                int interrupted=0;
                foreach(Enemy foe in enemies)
                {
                    if(!IsGlasswindEnemy(foe)||foe.hp<=0||
                        (foe.Position-station).Length>3.65f||
                        !ClearAttackPath(station,foe.Position))continue;
                    ApplyEnemyControl(foe,1.2f);
                    interrupted++;
                }
                effects.Add(new Effect("burst",station,.48f)
                    {radius=1.3f,glasswindOwned=true});
                sound="guard";revision++;
                Notify("风阀泄压！当前风压被驱散，近处 "+interrupted+
                       " 台守卫短暂失去攻势。仍需守满八秒并击败两波敌人。",
                    "Valve vented! Pressure cleared and "+interrupted+
                       " nearby guards disrupted. Both waves and eight seconds still count.");
                return true;
            }
            if(action=="glasswind_withdraw")
            {
                if(save.room==6 && GlasswindSite!=0 &&
                    PhysicalRefugeReach(GlasswindPoint(GlasswindSite),2.2f))
                {
                    ResetGlasswind();revision++;
                    Notify("已撤出。未封存中继可免费重试。",
                        "Withdrawn. Unsealed relays can be retried for free.");
                }
                return true;
            }
            if(action=="glasswind_seal")
            {
                int site=GlasswindSite;
                if(site==0 || save.room!=6 || Dying || GlasswindWave!=2 ||
                    GlasswindCharge<8 || !GlasswindWavesDefeated ||
                    enemies.Exists(IsGlasswindEnemy) ||
                    !PhysicalRefugeReach(GlasswindPoint(site),2.2001f) ||
                    Has("glasswind_"+site))return true;
                ResetGlasswind();Flag("glasswind_"+site);
                CreditCoins(15);CreditShards(1);AddXp(20);revision++;sound="reward";
                effects.Add(new Effect("discover",Position,.7f));
                RecordMoment("沙海风脉 "+site+"/3 已对所有旅人开放。",
                    "Glasswind relay "+site+"/3 opened to all travelers.");
                if(Has("glasswind_1") && Has("glasswind_2") && Has("glasswind_3"))
                {
                    Flag("glasswind_all_sealed");
                    Notify("三座中继已连通！回余烬镇找米娅公开风路图。",
                        "All relays restored! Publish the wind chart with Mia.");
                }
                else
                    Notify("中继封存！15硬币、1碎片、20经验与避暑圈。",
                        "Relay sealed! +15 coins, +1 shard, +20 XP and local shade.");
                return true;
            }
            if(action=="glasswind_publish")
            {
                if(save.room!=0 || Dying || !Has("glasswind_all_sealed") ||
                    Has("glasswind_complete") ||
                    !PhysicalRefugeReach(ObjectPoint(0,"glasswindkeeper",
                        new Point(-100,-100)),2.2001f))
                    return true;
                Flag("glasswind_complete");CreditCoins(65);CreditShards(2);
                CreditSkillPoints(1);AddXp(70);revision++;sound="reward";
                RecordMoment("米娅公开风脉地图。三座公共风阀为行路者遮阴。",
                    "Mia published the wind chart. Three public valves now shelter travelers.");
                Notify("玻璃风脉归档！65硬币、2碎片、1天赋点、70经验。",
                    "Glasswind published! +65 coins, +2 shards, +1 talent, +70 XP.");
                return true;
            }
            return false;
        }


        // Local warning pressure is transient and combat-facing. Every
        // contact center must have a real unobstructed two-unit escape route.
        private void PulseGlasswind()
        {
            Point target=Position;
            Point[] exits={new Point(2.1f,0),new Point(-2.1f,0),
                new Point(0,2.1f),new Point(0,-2.1f)};
            bool escape=false;
            foreach(Point step in exits)
                if(CanStand(target+step) && ClearAttackPath(target,target+step))
                {escape=true;break;}
            if(!escape || !CanStand(target))return;
            hazards.Add(new Hazard { position=target, radius=.96f,
                timer=1.35f,damage=GlasswindWave==2?17:11,
                kind="glass",glasswindOwned=true });
            // North: alternating crosswind; east: lee-side wave;
            // south: later delayed dune pulse. Keep at least one safe exit.
            Point offset=GlasswindSite==1 ?
                new Point(glasswindSurgeCycle%2==0?2.9f:-2.9f,0) :
                GlasswindSite==2 ? new Point(0,3.1f) :
                new Point(3.1f,0);
            Point second=target+offset;
            // Do not draw the staggered impact across the ONLY reachable
            // escape, especially in the narrow northern desert corridors.
            bool openExitAfterOffset=false;
            foreach(Point step in exits)
            {
                Point landing=target+step;
                if(CanStand(landing) && ClearAttackPath(target,landing) &&
                    (landing-second).Length>1.07f)
                {openExitAfterOffset=true;break;}
            }
            if(openExitAfterOffset && CanStand(second) &&
                ClearAttackPath(target,second))
            {
                hazards.Add(new Hazard { position=second,radius=.85f,
                    timer=GlasswindSite==3?2.05f:1.75f,
                    damage=GlasswindWave==2?14:9,
                    kind="glass",glasswindOwned=true });
            }
            sound="windup";
        }
        private void TickGlasswind(float dt)
        {
            if(GlasswindSite==0)return;
            if(save.room!=6 || Dying || practicing)
            {ResetGlasswind();revision++;return;}
            // An authored wave stays contested until each actual individual
            // death is confirmed in GameCore.Hit. External roster removal
            // cannot complete a capture or pay its one-time permanent reward.
            for(int i=0;i<GlasswindWave*2;i++)
            {
                int expectedId=89810+GlasswindSite*10+2+i;
                if((glasswindConfirmedKills&(1<<i))==0 &&
                    !enemies.Exists(e=>e.id==expectedId && e.hp>0))
                {
                    ResetGlasswind();revision++;
                    Notify("风阀守卫异常离场，未确认击败。守点已重置，可免费重试。",
                        "An unconfirmed relay guard vanished. Capture reset; retry for free.");
                    return;
                }
            }
            GlasswindTime-=dt;
            if(GlasswindTime<=0)
            {
                ResetGlasswind();revision++;
                Notify("风阀超时重置，可免费重试。",
                    "Capture timed out. Retry the relay for free.");
                return;
            }
            glasswindSurgeTimer-=dt;
            if(glasswindSurgeTimer<=0 && enemies.Exists(IsGlasswindEnemy))
            {
                PulseGlasswind();
                glasswindSurgeCycle++;
                glasswindSurgeTimer=GlasswindWave==2?4.15f:5.6f;
            }
            Point station=GlasswindPoint(GlasswindSite);
            float range=(Position-station).Length;
            // Physical capture cannot progress through a closing wall or
            // gate, even if the player remains inside the icon's radius.
            // Use the same near-side contact as actual relay interactions.
            bool reachableRing=PhysicalRefugeReach(station,2.65f);
            // A defender can contest only from a real attack lane.
            bool jammed=enemies.Exists(e=>IsGlasswindEnemy(e) &&
                e.hp>0 && (e.Position-station).Length<1.6f &&
                ClearAttackPath(e.Position,station));
            if(reachableRing && !jammed)
                GlasswindCharge=Math.Min(8,GlasswindCharge+dt);
            else if(range>3.2f || (range<2.65f && !reachableRing))
                // A nearby but physically isolated hero is outside the
                // reachable capture area: apply the ordinary retreat decay.
                GlasswindCharge=Math.Max(0,GlasswindCharge-dt*.6f);
            if(GlasswindWave==1 && GlasswindCharge>=4)
            {
                if(!SpawnGlasswindWave(2))
                {
                    // Do not let a temporarily occupied foot cause an
                    // unrecoverable reset. Hold at the first-wave threshold
                    // until two genuinely clear feet are available; keep
                    // the 95-second deadline and first-wave combat alive.
                    GlasswindCharge=4f;
                    if(!glasswindReinforcementsDeferred)
                    {
                        glasswindReinforcementsDeferred=true;
                        revision++;
                        Notify("增援暂缺安全落点，已保留第一波守点进度。清出位置后继续迎战。",
                            "Reinforcements need clear ground. First-wave progress is held; clear the arena to continue.");
                    }
                    return;
                }
                glasswindReinforcementsDeferred=false;
                sound="windup";revision++;
                Notify("第二波重甲守卫来袭！","The armored second wave is coming!");
            }
        }
    }
}
