using System;
namespace Starfall
{
    // Frostchime's lost traveler is one live, body-guarded character,
    // not an instant quest flag or a saved/invisible NPC teleport.
    // Existing v2 flags record only the final civilian decision.
    public sealed partial class GameCore
    {
        private int whiteoutStage,whiteoutKills;
        private float whiteoutSeconds,whiteoutWarmth,whiteoutGust,whiteoutShield;
        private bool whiteoutHerbUsed,whiteoutFacingLeft;
        private Point whiteoutFollowerFoot;
        private Enemy whiteoutHunterA,whiteoutHunterB;
        public bool WhiteoutActive {get{return whiteoutStage>0&&save.room==7;}}
        public bool WhiteoutComplete {get{return Has("whiteout_complete");}}
        public bool WhiteoutPublic {get{return Has("whiteout_public");}}
        public bool WhiteoutGuides {get{return Has("whiteout_guides");}}
        public int WhiteoutStage {get{return WhiteoutActive?whiteoutStage:0;}}
        public float WhiteoutSeconds {get{return WhiteoutActive?whiteoutSeconds:0f;}}
        public float WhiteoutWarmth {get{return whiteoutStage==2?whiteoutWarmth:0f;}}
        public float WhiteoutGustWarning {get{return whiteoutStage==2&&
            whiteoutGust<=1.35f?Math.Max(.01f,whiteoutGust):0f;}}
        public float WhiteoutShieldSeconds {get{return whiteoutStage==2?whiteoutShield:0f;}}
        public bool WhiteoutFollowerVisible {get{return WhiteoutActive&&whiteoutStage>=2;}}
        public Point WhiteoutFollowerFoot {get{return whiteoutFollowerFoot;}}
        public bool WhiteoutFollowerFacingLeft {get{return whiteoutFacingLeft;}}
        public float WhiteoutFollowerGap {get{return WhiteoutFollowerVisible?
            (Position-whiteoutFollowerFoot).Length:0f;}}
        public int WhiteoutHuntersAlive
        {
            get
            {
                int n=0;
                if(IsWhiteoutHunter(whiteoutHunterA)&&enemies.Contains(whiteoutHunterA)
                    &&whiteoutHunterA.hp>0)n++;
                if(IsWhiteoutHunter(whiteoutHunterB)&&enemies.Contains(whiteoutHunterB)
                    &&whiteoutHunterB.hp>0)n++;
                return n;
            }
        }
        public Point WhiteoutTarget
        {get{return whiteoutStage==1?new Point(58.5f,42.5f):
            whiteoutStage>=2?new Point(61.5f,36.5f):
            new Point(64.5f,44.5f);}}
        public string WhiteoutObjective
        {get{return whiteoutStage==1?L("寻找失踪的北境旅人","FIND THE LOST TRAVELER"):
            whiteoutStage==2?L("护送旅人返回旧营地","ESCORT TO THE OLD CAMP"):
            whiteoutStage==3?L("在营地决定开放避险站或巡路图","CHOOSE SHELTER OR TRAIL MAP"):
            L("从真实远眺台接下寻人委托","READ THE LOOKOUT NOTICE");}}
        public bool IsWhiteoutHunter(Enemy e)
        {return e!=null&&(Object.ReferenceEquals(e,whiteoutHunterA)||
            Object.ReferenceEquals(e,whiteoutHunterB));}
        private void OnWhiteoutEnemyDefeated(Enemy e)
        {
            if(whiteoutStage!=2||save.room!=7||!IsWhiteoutHunter(e))return;
            if(Object.ReferenceEquals(e,whiteoutHunterA))whiteoutKills|=1;
            if(Object.ReferenceEquals(e,whiteoutHunterB))whiteoutKills|=2;
        }
        private void ResetWhiteout()
        {
            if(whiteoutHunterA!=null)
            {
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,whiteoutHunterA));
                RemoveEnemyPendingWarning(whiteoutHunterA);
                enemies.Remove(whiteoutHunterA);
            }
            if(whiteoutHunterB!=null)
            {
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,whiteoutHunterB));
                RemoveEnemyPendingWarning(whiteoutHunterB);
                enemies.Remove(whiteoutHunterB);
            }
            whiteoutStage=whiteoutKills=0;
            whiteoutSeconds=whiteoutWarmth=whiteoutGust=whiteoutShield=0f;
            whiteoutHerbUsed=whiteoutFacingLeft=false;
            whiteoutFollowerFoot=new Point();
            whiteoutHunterA=whiteoutHunterB=null;
        }
        private WorldObject WhiteoutSite(string id)
        {
            if(save.room!=7)return null;
            foreach(WorldObject o in Map.objects)if(o!=null&&o.id==id)return o;
            return null;
        }
        private bool WhiteoutSource(WorldObject source,string id)
        {return source!=null&&source.id==id&&save.room==7&&
            SourceBelongsToActiveMap(source)&&HasPhysicalInteractionReach(source);}
        private bool TryWhiteoutHunterFoot(Point center,Point occupied,out Point result)
        {
            Point[] offsets={new Point(-2,2),new Point(-3,0),new Point(2,2),
                new Point(3,0),new Point(0,-3),new Point(0,3),
                new Point(-4,1),new Point(4,1),new Point(-3,-2),
                new Point(3,-2),new Point(-4,3),new Point(4,-3)};
            foreach(Point offset in offsets)
            {
                Point foot=center+offset;
                if(!CanStand(foot)||!ClearAttackPath(center,foot)||
                    (foot-Position).Length<1.3f||
                    (foot-occupied).Length<1.6f)continue;
                bool occupiedFoot=false;
                foreach(Enemy e in enemies)
                    if(e!=null&&e.hp>0&&(foot-e.Position).Length<1.3f)
                    {occupiedFoot=true;break;}
                if(occupiedFoot)continue;
                result=foot;return true;
            }
            result=new Point();return false;
        }
        private bool StartWhiteoutHunters(Point traveler)
        {
            Point a,b;
            if(!TryWhiteoutHunterFoot(traveler,new Point(-999,-999),out a)||
                !TryWhiteoutHunterFoot(traveler,a,out b))return false;
            whiteoutHunterA=CreatePlacedEnemy(++enemyId,0,a,96f);
            whiteoutHunterB=CreatePlacedEnemy(++enemyId,2,b,115f);
            whiteoutHunterA.timer=.9f;whiteoutHunterB.timer=1.1f;
            enemies.Add(whiteoutHunterA);enemies.Add(whiteoutHunterB);
            whiteoutKills=0;
            effects.Add(new Effect("discover",a,.8f));
            return true;
        }
        private bool TalkWhiteout(WorldObject o)
        {
            if(o==null||save.room!=7)return false;
            bool overlook=o.id=="whiteout_notice",lost=o.id=="whiteout_lost",
                camp=o.id=="annex_camp_7";
            if(!overlook&&!lost&&!(camp&&whiteoutStage>=2))return false;
            if(overlook&&!Has("boss_7")&&!WhiteoutActive&&!WhiteoutComplete)
                return false; // Preserve original overlook before guardian.
            if(!WhiteoutSource(o,o.id))return true;
            if(overlook)
            {
                if(WhiteoutComplete)
                {
                    Dialog(o.name,o.en,WhiteoutPublic?
                        "旧营地已开放成不收费的平民避险站。雪路上的人不必再等待堡垒的放行令。":
                        "被封存的雪地路线交还了巡路队，行路人如今能避开封锁快速穿过霜钟山口。",
                        WhiteoutPublic?
                        "The old camp is now an open civilian warming station. Travelers no longer wait for citadel passes.":
                        "The unsealed mountain map belongs to roadwardens, and travelers can bypass the old barricades.",
                        C("离开","Leave","close"));return true;
                }
                if(whiteoutStage==0)
                {
                    Dialog(o.name,o.en,
                        "风雪里有人留下没寄出的家书。守路人最后看见她朝西侧旧货路走去。请到雪崖找回人，再让她亲自走到营地；两名追兵仍在搜路。你需要靠近她挡风，远远冲向营地只会让她失温。",
                        "A traveler vanished while carrying a letter home. Follow the west freight trail, find her, then escort her on foot to the old camp. Two hunters still search the road. Stay close to shield her from gusts; racing ahead will leave her exposed.",
                        C("进入风雪寻找旅人","Search the whiteout","whiteout_start"),
                        C("以后再说","Not now","close"));return true;
                }
                Dialog(o.name,o.en,
                    "风雪救援进行中。请沿雪崖旧路找到旅人并将她带回真正的营地。",
                    "The rescue is underway. Find the traveler and bring her physically to the old camp.",
                    C("继续搜救","Continue rescue","close"),
                    C("取消本次搜救","Abandon this attempt","whiteout_abort"));return true;
            }
            if(lost)
            {
                if(WhiteoutComplete)
                {
                    Dialog(o.name,o.en,"脚印已从雪地延伸到安全的营地。只剩一张归家的信封。",
                        "Footprints now lead home. Only a letter envelope remains in the snow.",
                        C("离开","Leave","close"));return true;
                }
                if(whiteoutStage==1)
                {
                    Dialog(o.name,o.en,
                        "她蜷在背风石下，手里仍攥着那封写给家人的信。远处两名猎兵正在接近。只有她亲自走回营地才能完成救援。",
                        "She is curled under a windward rock, still clutching the letter. Two hunters are closing in. The rescue is not complete until she walks to shelter.",
                        C("扶她起身，保护她返回","Help her up · begin real escort","whiteout_pickup"),
                        C("准备后再来","Prepare first","close"));return true;
                }
                if(whiteoutStage==2)
                {
                    Dialog(o.name,o.en,
                        "风雪仍在扑打旧货路。给她一株月光草能恢复35点体温并抵御接下来12秒阵风；本次只能一次。也可以节省药草，靠近她挡风。",
                        "Gusts lash the old freight trail. One Moonherb restores 35 warmth and shelters her for 12 seconds; only once this attempt. Or stay near and protect her without spending supplies.",
                        C("月光草1 · 保温12秒","Moonherb · 12s shelter","whiteout_herb"),
                        C("继续护送","Continue escort","close"));return true;
                }
                Dialog(o.name,o.en,"山崖下只剩旧营火的灰。","Only ashes of the old campfire remain.",
                    C("离开","Leave","close"));return true;
            }
            if(camp)
            {
                if(whiteoutStage==3)
                {
                    Dialog(o.name,o.en,
                        "她终于自己走到了营地，把写给家人的信交到你手中。“别再让他们用这条路上的风雪，决定谁值得被救。”你愿意把这座旧营地开放为公共暖站，还是把隐蔽安全路线交给巡路队？",
                        "She has walked into camp and puts the letter in your hand. 'Don't let the blizzard decide who deserves rescue.' Open a public warming refuge here, or give roadwardens a safe route past the old blockades?",
                        C("开放免费平民暖站","Open public warming refuge","whiteout_public"),
                        C("交还山路导览图","Return safe trail map","whiteout_guides"));return true;
                }
                if(whiteoutStage==2)
                {
                    Dialog(o.name,o.en,
                        "营地已经近了。请真正带着旅人抵达这里，并确认追兵被击败；一个人跑到营地不能代替她获救。",
                        "The camp is close. The traveler must arrive on foot, and the hunters must be defeated. Running here alone cannot rescue her.",
                        C("返回旅人身边","Return to the traveler","close"));return true;
                }
            }
            return false;
        }
        private bool ChooseWhiteout(string action)
        {
            if(action==null||!action.StartsWith("whiteout_",StringComparison.Ordinal))
                return false;
            WorldObject source=pendingDialogueSource;
            if(save.room!=7||mode!="world"||Dying||practicing||
                source==null||pendingDialogueSourceRoom!=7||
                !WhiteoutSource(source,source.id))return true;
            if(action=="whiteout_start")
            {
                if(source.id!="whiteout_notice"||whiteoutStage!=0||
                    WhiteoutComplete||!Has("boss_7")||FrostSignalActive||
                    FieldWorkPreventsRest||!SafeTravel)return true;
                whiteoutStage=1;whiteoutSeconds=205f;
                TrackQuest("whiteout_rescue");
                revision++;sound="quest";
                Notify("去西侧旧货路找失踪旅人。不要让搜救时间耗尽。",
                    "Search the western freight trail. Reach the lost traveler before time runs out.");
                return true;
            }
            if(action=="whiteout_abort")
            {
                if(source.id=="whiteout_notice"&&WhiteoutActive)
                {ResetWhiteout();revision++;Notify("搜救撤回。以后可重新搜寻。",
                    "Rescue withdrawn. You can try again later.");}
                return true;
            }
            if(action=="whiteout_pickup")
            {
                if(source.id!="whiteout_lost"||whiteoutStage!=1||
                    whiteoutSeconds<=0f||!CanStand(new Point(source.x,source.y))||
                    !StartWhiteoutHunters(new Point(source.x,source.y)))
                    return true;
                whiteoutStage=2;whiteoutSeconds=150f;
                whiteoutWarmth=88f;whiteoutGust=7.5f;
                whiteoutFollowerFoot=new Point(source.x,source.y);
                revision++;sound="quest";
                Notify("两名猎兵出现！带旅人离开风雪，近身替她挡风。",
                    "Two actual hunters arrive! Guide the traveler to camp and stay close to shield her.");
                return true;
            }
            if(action=="whiteout_herb")
            {
                if(source.id!="whiteout_lost"||whiteoutStage!=2||
                    whiteoutHerbUsed||save.inventory[2]<1)return true;
                save.inventory[2]--;whiteoutHerbUsed=true;
                whiteoutWarmth=Math.Min(100f,whiteoutWarmth+35f);
                whiteoutShield=12f;
                revision++;sound="reward";
                Notify("药草已用：体温恢复35，12秒内抵御阵风。",
                    "Moonherb used: +35 warmth and 12s of wind shielding.");
                return true;
            }
            if(action=="whiteout_public"||action=="whiteout_guides")
            {
                WorldObject camp=WhiteoutSite("annex_camp_7");
                if(source!=camp||whiteoutStage!=3||WhiteoutComplete||
                    whiteoutKills!=3||WhiteoutHuntersAlive>0||
                    (whiteoutFollowerFoot-new Point(camp.x,camp.y)).Length>1.65f)
                    return true;
                bool isPublic=action=="whiteout_public";
                ResetWhiteout();
                Flag("whiteout_complete");Flag(action);
                CreditCoins(70);CreditShards(3);AddXp(105);
                RecordMoment(isPublic?
                    "你将从风雪中护送回来的旅人亲手交给霜钟居民，旧军用营地改为不需通行证的公共暖站。":
                    "你护送失踪旅人穿过追兵和严寒后，将被封存的安全雪路交给巡路队，让回家的路不再受军令限制。",
                    isPublic?
                    "You escorted a lost traveler home and returned the old military camp as a free civilian warming refuge.":
                    "You escorted a traveler through hunters and blizzard, then gave roadwardens the sealed safe mountain route.");
                revision++;sound="reward";
                Notify(isPublic?
                    "公共暖站开放！+70硬币、+3碎片、+105经验。营地安全时可持续恢复生命与耐力。":
                    "雪地捷径开放！+70硬币、+3碎片、+105经验。霜钟高原移动速度永久+0.35。",
                    isPublic?
                    "Warming refuge opened! +70 coins, +3 shards, +105 XP; safe camp heals and restores energy.":
                    "Safe trail opened! +70 coins, +3 shards, +105 XP; permanent +0.35 Frostchime speed.");
                return true;
            }
            return true;
        }
        private void TickWhiteout(float dt)
        {
            if(whiteoutStage==0)
            {
                // The public option creates a real place-based service,
                // not a map-wide passive aura or free combat healing.
                if(WhiteoutPublic&&save.room==7&&!Dying&&SafeTravel&&
                   !FieldWorkPreventsRest&&
                   PhysicalRefugeReach(new Point(61.5f,36.5f),2.8f))
                {
                    save.health=Math.Min(save.maxHealth,save.health+3f*dt);
                    save.energy=Math.Min(100f,save.energy+4f*dt);
                }
                return;
            }
            if(save.room!=7||Dying||practicing)
            {ResetWhiteout();revision++;return;}
            if(whiteoutStage==3)return; // Person is safely in camp.
            whiteoutSeconds-=dt;
            if(whiteoutSeconds<=0f)
            {
                ResetWhiteout();revision++;
                Notify("风雪覆盖了脚印。搜救中断，可从远眺台重新开始。",
                    "The blizzard covered the trail. Retry the rescue from the overlook.");
                return;
            }
            if(whiteoutStage!=2)return;
            // Counterfeit same-ID actors cannot count as the two real guards.
            if((whiteoutKills&1)==0&&!enemies.Contains(whiteoutHunterA)||
               (whiteoutKills&2)==0&&!enemies.Contains(whiteoutHunterB))
            {
                ResetWhiteout();revision++;
                Notify("追兵的真实踪迹中断。请回远眺台重试。",
                    "The original hunters vanished; retry from the lookout.");
                return;
            }
            float gap=(Position-whiteoutFollowerFoot).Length;
            if(gap>1.12f&&gap<11f&&CanStand(whiteoutFollowerFoot)&&
                CanStand(Position))
            {
                // A follower walks through genuine collision, not through
                // walls or instantly to the player's current coordinates.
                Point toward=(Position-whiteoutFollowerFoot).Normalized;
                float step=Math.Min(gap-1.0f,dt*2.65f);
                Point[] dirs={toward,new Point(toward.x,0f),
                    new Point(0f,toward.y),
                    new Point(toward.y,-toward.x),
                    new Point(-toward.y,toward.x)};
                foreach(Point dir in dirs)
                {
                    if(dir.Length<.02f)continue;
                    Point next=whiteoutFollowerFoot+dir.Normalized*step;
                    if(!CanStand(next)||!ClearAttackPath(whiteoutFollowerFoot,next))
                        continue;
                    whiteoutFollowerFoot=next;
                    whiteoutFacingLeft=dir.x<-.03f;
                    break;
                }
            }
            gap=(Position-whiteoutFollowerFoot).Length;
            whiteoutShield=Math.Max(0f,whiteoutShield-dt);
            float pressure=.21f+(gap>5f?.72f:0f)+(gap>9f?1.2f:0f);
            foreach(Enemy hunter in enemies)
                if(IsWhiteoutHunter(hunter)&&hunter.hp>0f&&
                   (hunter.Position-whiteoutFollowerFoot).Length<4f)
                   pressure+=.62f;
            if(whiteoutShield>0f)pressure*=.2f;
            whiteoutWarmth=Math.Max(0f,whiteoutWarmth-pressure*dt);
            whiteoutGust-=dt;
            if(whiteoutGust<=0f)
            {
                bool heroShields=gap<=1.8f&&CanStand(Position)&&
                    ClearAttackPath(Position,whiteoutFollowerFoot);
                float gust=whiteoutShield>0f?1f:heroShields?3f:14f;
                whiteoutWarmth=Math.Max(0f,whiteoutWarmth-gust);
                whiteoutGust=8.1f;
                effects.Add(new Effect("number",whiteoutFollowerFoot,.7f,
                    heroShields||whiteoutShield>0?
                    L("挡住阵风","GUST SHIELDED"):
                    L("旅人失温！","TRAVELER EXPOSED!")));
            }
            if(whiteoutWarmth<=0f)
            {
                ResetWhiteout();revision++;
                Notify("旅人失温倒下。你必须靠近护送，或在原地给她用药草。",
                    "The traveler succumbed to cold. Escort closely or supply a Moonherb.");
                return;
            }
            WorldObject camp=WhiteoutSite("annex_camp_7");
            if(camp!=null&&whiteoutKills==3&&
                (whiteoutFollowerFoot-new Point(camp.x,camp.y)).Length<1.45f&&
                (Position-new Point(camp.x,camp.y)).Length<1.8f&&
                CanStand(whiteoutFollowerFoot)&&CanStand(Position))
            {
                whiteoutStage=3;revision++;sound="reward";
                Notify("旅人自己走进营地了。与营地交谈，决定这条救援线的归属。",
                    "The traveler walked into camp! Speak there to choose the route's future.");
            }
        }
    }
}
