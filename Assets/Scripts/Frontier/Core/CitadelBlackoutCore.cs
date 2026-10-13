using System;
namespace Starfall
{
    // Chapter Four climax before Orrin: physically lead three civilians through
    // a malfunctioning shield, not another enemy-wave defense or dialog shortcut.
    // The emergency is transient; v2 only receives a finished milestone.
    public sealed partial class GameCore
    {
        private float blackoutTime, blackoutPulse, blackoutSurgeTime, blackoutFollowerHealth;
        private int blackoutFollowing, blackoutRescued;
        private Point blackoutFollowerPosition, blackoutSurgePosition;
        private bool blackoutActive;
        public bool BlackoutActive { get { return blackoutActive; } }
        public float BlackoutTime { get { return blackoutTime; } }
        public int BlackoutFollowing { get { return blackoutFollowing; } }
        public int BlackoutRescued { get { return blackoutRescued; } }
        public Point BlackoutFollowerPosition { get { return blackoutFollowerPosition; } }
        public float BlackoutFollowerHealth { get { return blackoutFollowerHealth; } }
        public bool LocalCeasefireEligible
        {
            get { return save.room==4 && Has("blackout_complete") &&
                Has("triad_complete") && AllWitnessesFiled &&
                Allies==3 && !Has("boss_4") && save.ending==0; }
        }
        private bool BlackoutEligible
        { get { return save.room==4 && Has("triad_complete") && !Has("boss_4") && save.ending==0; } }
        private Point BlackoutPoint(string kind)
        { return ObjectPoint(4,kind,new Point(-100,-100)); }
        private void ResetBlackout()
        {
            hazards.RemoveAll(h=>h.blackoutOwned);
            effects.RemoveAll(e=>e.blackoutOwned);
            blackoutTime=blackoutPulse=blackoutSurgeTime=0;
            blackoutFollowing=blackoutRescued=0;
            blackoutFollowerHealth=0;blackoutFollowerPosition=new Point();
            blackoutActive=false;
        }
        private bool BlackoutCitizenSaved(int id)
        { return (blackoutRescued & (1<<(id-1)))!=0; }
        public bool BlackoutCivilianVisible(int id)
        { return save.room==4 && !BlackoutCitizenSaved(id) && blackoutFollowing!=id; }
        private bool TalkBlackout(WorldObject o)
        {
            bool shelter=o.kind=="blackoutshelter";
            if(!shelter && !o.kind.StartsWith("blackoutcivilian"))return false;
            if(save.room!=4)return true;
            if(shelter)
            {
                if(Has("blackout_complete"))
                {
                    Dialog(o.name,o.en,
                        "你在长夜里让三个普通家庭安全回到了灯下。记下每个人的名字，比屏障今日的输出高低更重要。厄洛现在无法再说，没有人肯承担改变的责任。",
                        "You led three families safely back to shelter during the blackout. Their names matter more than today's shield output. Orrin can no longer claim that nobody will help bear the cost of change.",
                        C("继续前进","Continue","close"));return true;
                }
                if(!BlackoutEligible)
                {
                    Dialog(o.name,o.en,
                        "这座避难所的备用灯还在等待可公开核验的星核供能记录。先完成莉雅的三核审计，并在旧统领落败之前确认安全方案。",
                        "The refuge needs a publicly verified energy account. Complete Lyra's three-core audit before the confrontation is over.",
                        C("了解","Understood","close"));return true;
                }
                if(!blackoutActive)
                {
                    Dialog(o.name,o.en,
                        "堡垒警报：三座星核被同一屏障占用，备用灯闪烁，平民被困在三条街。厄洛坚持屏障一旦关掉就会有人死。你可以亲自证明不用牺牲他们。\n\n在180秒内逐一找到三人，按E带上一个人，带回这座避难所按E交接。护送对象会沿真实路线跟随，跟不上时需要回头；延迟电弧会伤害你和身边的居民。失败可以免费重试，原有城堡决战不会因此锁死。",
                        "Citadel alarm: three cores have been diverted into one shield. Emergency lamps are failing, and three civilians are trapped on different streets. Orrin insists that shutting down the shield means somebody must die.\n\nWithin 180 seconds, find each citizen, press E to escort, and physically return them one by one to this shelter. They really follow you, can fall behind, and delayed arcs can hurt both of you. Retries are free. The original finale route stays available.",
                        C("启动第四章关键事件 · 长夜救援","Begin Chapter Four event · Blackout Evacuation","blackout_start"),
                        C("先准备一下","Prepare","close"));return true;
                }
                if(blackoutFollowing!=0)
                {
                    int id=blackoutFollowing;
                    if((blackoutFollowerPosition-Position).Length>2.3f)
                    {
                        Notify("居民还没跟上！回去接应后再交给避难所。",
                            "Your civilian is too far behind. Rejoin them before delivering to shelter.");
                        return true;
                    }
                    Dialog(o.name,o.en,
                        "居民已走到备用灯下。登记姓名，交由避难所照顾；然后出发寻找其余失联者。",
                        "The civilian reached the emergency lamps. Register their name at the shelter before searching for the others.",
                        C("安全交接这名居民","Safely hand over civilian","blackout_deliver_"+id),
                        C("继续同行","Keep escorting","close"));return true;
                }
                if(blackoutRescued==7)
                {
                    Dialog(o.name,o.en,
                        "三名居民都平安回到了避难所。备用照明不再依赖未经审查的旧屏障协议。记下他们的名字，让厄洛看到公开记录不是空口承诺。",
                        "All three civilians made it to safety. The backup lamps no longer rely on the secret shield protocol. Register their names so Orrin sees what public accountability can accomplish.",
                        C("公布撤离记录 · 完成关键事件","Publish evacuation roster · Complete story event","blackout_complete"),
                        C("稍后","Later","close"));return true;
                }
                Dialog(o.name,o.en,
                    "救回 "+BlackoutCount+"/3 人。还有人被困在北侧和东侧街区；必须亲自把他们送到这座避难所。",
                    "Rescued "+BlackoutCount+"/3 people. Others remain beyond the north and east streets. Walk them safely to this shelter.",
                    C("追踪剩余居民","Track missing civilians","blackout_track"),
                    C("明确取消本轮救援","Abandon this attempt","blackout_abort"),
                    C("继续搜寻","Search on","close"));
                return true;
            }
            int site;
            if(!int.TryParse(o.kind.Substring("blackoutcivilian".Length),out site) ||
                site<1 || site>3)return true;
            if(!blackoutActive)
            {Notify("先在避难所接取屏障故障紧急救援。","Begin the emergency at the shelter first.");return true;}
            if(BlackoutCitizenSaved(site))
            {Notify("这名居民已经获救。","This civilian has been rescued.");return true;}
            if(blackoutFollowing!=0)
            {Notify("你正在护送另一人；先送到避难所再来接应。","Escort your current civilian back before collecting another.");return true;}
            string[] namesZh={"","街角的母亲 · 莱娜","桥畔的孩子 · 伊恩","北塔的守灯人 · 奥姆"};
            string[] namesEn={"","Lena · The Mother","Ian · The Bridge Child","Orme · The Lampkeeper"};
            Dialog(namesZh[site],namesEn[site],
                site==1?"莱娜：电弧爬上了家门。我不是说不要屏障——但如果灯光只剩照在城墙上，住在墙下的我们又该去哪？":
                site==2?"伊恩：桥上的大灯突然灭了。我想回家。我会跟着你走，可是你跑得太快，我就找不到路了。":
                "奥姆：备用线路还可以打开，只是旧军令不许我们切换。我有灯塔的钥匙，可我不能再把同伴留在这里。",
                site==1?"Lena: Arcs crawled across our doorway. I never asked for no shield. But where do we go if all the light shines only on the wall?":
                site==2?"Ian: The bridge lamp went dark. I want to go home. I'll follow, but if you run too far I lose the way.":
                "Orme: The backup lamps still work. Old orders forbid us from switching them on. I have the key, but I cannot leave another person behind.",
                C("跟我走，去公共避难所","Follow me to the public shelter","blackout_pick_"+site),
                C("先看看路线","Check the route","close"));
            return true;
        }
        public int BlackoutCount
        { get { int n=0;for(int i=1;i<=3;i++)if(BlackoutCitizenSaved(i))n++;return n; } }
        private bool ChooseBlackout(string action)
        {
            if(action=="blackout_track"){TrackQuest("blackout");return true;}
            if(action=="blackout_abort")
            {
                if(blackoutActive && save.room==4 &&
                    (Position-BlackoutPoint("blackoutshelter")).Length<2.2f)
                {ResetBlackout();revision++;Notify("本轮救援结束。居民安全记录未伪造；可从避难所重试。",
                    "Attempt ended. No fake civilian rescues were filed; retry at the shelter.");}
                return true;
            }
            if(action=="blackout_start")
            {
                if(!BlackoutEligible || Has("blackout_complete") || blackoutActive ||
                    practicing || Dying || !SafeTravel || TriadDefenseActive ||
                    commonsRelayRoom!=0 || RootHarmonyRoom!=0 || atlasEchoRoom!=0 ||
                    reliefMissionRoom!=0 || witnessDefenseRoom!=0 ||
                    (Position-BlackoutPoint("blackoutshelter")).Length>2.2f)return true;
                blackoutActive=true;blackoutTime=180;blackoutPulse=3.5f;
                blackoutFollowing=blackoutRescued=0;
                blackoutFollowerHealth=0;
                Flag("blackout_started");TrackQuest("blackout");
                RecordMoment("第四章：堡垒备用灯熄灭。你决定先救下街区里的居民，再去回答厄洛的质问。",
                    "Chapter Four: the citadel's backup lamps went dark. You chose to rescue people before debating Orrin's shield.");
                Notify("长夜撤离开始：180秒救回三人。E交接，每次只能护送一人。",
                    "Blackout evacuation: 180 seconds for three people. Press E to deliver; escort one at a time.");
                return true;
            }
            if(action.StartsWith("blackout_pick_"))
            {
                int id;
                if(!int.TryParse(action.Substring(14),out id) || id<1 || id>3 ||
                    !blackoutActive || blackoutFollowing!=0 || BlackoutCitizenSaved(id) ||
                    (Position-BlackoutPoint("blackoutcivilian"+id)).Length>2.2f)return true;
                blackoutFollowing=id;
                blackoutFollowerPosition=BlackoutPoint("blackoutcivilian"+id);
                blackoutFollowerHealth=48;
                revision++;
                Notify("居民开始跟随你。别让他落在预警电弧里，带到公共避难所。",
                    "Civilian following. Keep them clear of warning arcs and walk to the shelter.");
                return true;
            }
            if(action.StartsWith("blackout_deliver_"))
            {
                int id;
                if(!int.TryParse(action.Substring(17),out id) || id!=blackoutFollowing ||
                    !blackoutActive || (Position-BlackoutPoint("blackoutshelter")).Length>2.2f ||
                    (blackoutFollowerPosition-Position).Length>2.3f)return true;
                blackoutRescued|=1<<(id-1);
                blackoutFollowing=0;blackoutFollowerHealth=0;
                revision++;sound="quest";
                Notify("居民安全抵达！已登记 "+BlackoutCount+"/3。",
                    "Civilian safely delivered! Registered "+BlackoutCount+"/3.");
                return true;
            }
            if(action=="blackout_complete")
            {
                if(!blackoutActive || blackoutFollowing!=0 || blackoutRescued!=7 ||
                    Has("blackout_complete") ||
                    (Position-BlackoutPoint("blackoutshelter")).Length>2.2f)return true;
                ResetBlackout();
                Flag("blackout_complete");save.coins+=80;save.shards+=3;
                AddXp(90);revision++;sound="reward";
                RecordMoment("堡垒停电的长夜里，莱娜、伊恩与奥姆回到了灯下。公开的避难名单证明有人愿意为改变承担责任。",
                    "During the shield blackout, Lena, Ian and Orme reached shelter. The public rescue roster proves ordinary people can shoulder change.");
                Notify("长夜救援成功！80硬币、3碎片、90经验。满足条件可在厄洛面前提出民用停战方案。",
                    "Blackout evacuation completed! 80 coins, 3 shards, 90 XP. A civilian ceasefire becomes possible with enough support.");
                return true;
            }
            if(action=="blackout_ceasefire")
            {
                if(!LocalCeasefireEligible || blackoutActive || enemies.Exists(e=>e.boss))return true;
                // Identical irreversible ending gate as the original covenant parley,
                // but earned by real evacuation plus signed testimonies and all allies.
                enemies.Clear();projectiles.Clear();hazards.Clear();
                Flag("boss_4");Flag("warden_stood_down");
                Flag("blackout_peace");save.coins+=70;AddXp(80);revision++;
                FinalConversation();return true;
            }
            return false;
        }
        private void TickBlackout(float dt)
        {
            if(!blackoutActive)return;
            if(save.room!=4 || Dying || practicing)
            {ResetBlackout();revision++;return;}
            blackoutTime-=dt;
            if(blackoutTime<=0)
            {
                ResetBlackout();revision++;sound="deny";
                Notify("备用灯彻底熄灭，本轮撤离失败。回避难所可以重新尝试。",
                    "The backup lamps failed. This evacuation attempt ended; retry at the shelter.");
                return;
            }
            if(blackoutFollowing!=0)
            {
                Point to=Position-blackoutFollowerPosition;
                float gap=to.Length;
                if(gap>1.35f)
                {
                    Point step=to.Normalized*Math.Min((gap-1.1f),dt*5.8f);
                    Point next=blackoutFollowerPosition+step;
                    if(CanStand(next) && ClearAttackPath(blackoutFollowerPosition,next))
                        blackoutFollowerPosition=next;
                }
            }
            if(blackoutSurgeTime>0)
            {
                blackoutSurgeTime-=dt;
                if(blackoutSurgeTime<=0 && blackoutFollowing!=0 &&
                    (blackoutFollowerPosition-blackoutSurgePosition).Length<1.2f)
                {
                    blackoutFollowerHealth-=14;
                    if(blackoutFollowerHealth<=0)
                    {
                        ResetBlackout();revision++;sound="deny";
                        Notify("护送的居民在电弧中倒下。本轮失败，重新救援不会丢失以前主线成果。",
                            "Your civilian fell to an arc. Attempt failed; earlier campaign progress is safe.");
                        return;
                    }
                    effects.Add(new Effect("hurt",blackoutFollowerPosition,.5f){blackoutOwned=true});
                    Notify("居民被电弧击中！快改变路线，避免下一次冲击。",
                        "Civilian struck by an arc! Change routes before the next surge.");
                }
            }
            blackoutPulse-=dt;
            if(blackoutPulse>0)return;
            blackoutPulse=5.4f;
            if((Position-BlackoutPoint("blackoutshelter")).Length<2.8f)
                return;
            Point target=Position;
            bool canEscape=false;
            foreach(Point shift in new[]{new Point(1.7f,0),new Point(-1.7f,0),
                new Point(0,1.7f),new Point(0,-1.7f)})
                if(CanStand(target+shift) && ClearAttackPath(target,target+shift))
                {canEscape=true;break;}
            if(!canEscape)return;
            blackoutSurgePosition=target;
            blackoutSurgeTime=1.35f;
            hazards.Add(new Hazard{position=target,timer=1.35f,radius=1.2f,
                damage=13,kind="rift",blackoutOwned=true});
            effects.Add(new Effect("warn",target,1.25f){radius=1.2f,
                blackoutOwned=true});
        }
    }
}
