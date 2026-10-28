using System;
namespace Starfall
{
    // Physical field landmarks, additive to the existing campaign and SaveData v2.
    public sealed partial class GameCore
    {
        // A prepared signal fire creates a real, localized protective perimeter
        // around an EXISTING, visited outdoor camp. It cannot be carried
        // across regions or save slots and is not a free global shield.
        private float campWardSeconds, campWardFlareClock;
        private int campWardCharges, campWardRoom=-1;
        private MapData campWardMap;
        private SaveData campWardSave;
        private WorldObject campWardCamp;
        public bool CampWardActive
        { get { return campWardSeconds>0&&campWardCharges>0&&!Dying&&
            save.room==campWardRoom&&
            Object.ReferenceEquals(Map,campWardMap)&&
            Object.ReferenceEquals(save,campWardSave)&&
            campWardCamp!=null&&
            SourceBelongsToActiveMap(campWardCamp); } }
        public float CampWardSeconds { get { return CampWardActive?campWardSeconds:0f; } }
        public int CampWardCharges { get { return CampWardActive?campWardCharges:0; } }
        // Each of the eight physical outdoor camps must be reached and its
        // real fire funded before the Hearth Inn can publish the civilian
        // network. String flags preserve all v1/v2 save payloads.
        public int CampSignalCount
        {
            get { int n=0;for(int room=0;room<8;room++)
                if(Has("annex_signal_fire_"+room))n++;
                return n; }
        }
        public bool CampSignalNetworkPublished
        { get { return Has("annex_signal_network_published"); } }
        public Point CampWardFoot
        { get { return CampWardActive?
            new Point(campWardCamp.x,campWardCamp.y):new Point(); } }
        // The world overlay uses the EXACT same terrain-visible footprint
        // as the real shot interception. Never show a glowing safety ring
        // through an intact cliff when that arc cannot stop projectiles.
        public bool CampWardVisualSample(Point sample)
        {
            return CampWardActive&&Finite(sample.x)&&Finite(sample.y)&&
                (sample-CampWardFoot).Length<=3.251f&&
                ClearAttackPath(CampWardFoot,sample);
        }
        private void ResetCampWard()
        {
            campWardSeconds=campWardFlareClock=0;
            campWardCharges=0;campWardRoom=-1;
            campWardMap=null;campWardSave=null;campWardCamp=null;
        }
        private void TickCampWard(float dt)
        {
            if(campWardSeconds<=0)return;
            if(!CampWardActive){ResetCampWard();return;}
            campWardSeconds=Math.Max(0f,campWardSeconds-dt);
            if(campWardSeconds<=0){ResetCampWard();return;}
            campWardFlareClock-=dt;
            if(campWardFlareClock<=0)
            {
                campWardFlareClock=1.25f;
                effects.Add(new Effect("release",CampWardFoot,.40f)
                    {variant=1,height=12f});
            }
        }
        // Invoked at the actual unblocked .1-tile projectile sweep sample,
        // BEFORE either hero or civilian body contact. Nothing outside the
        // ward or behind solid cover may be erased.
        private bool TryCampWardIntercept(Projectile shot)
        {
            if(!CampWardActive||shot==null||shot.friendly||
                shot.blockedAtSpawn||shot.visual=="pebble"||
                !Finite(shot.velocity.x)||!Finite(shot.velocity.y)||
                shot.velocity.Length<=.5f||
                !Finite(shot.damage)||shot.damage<=0f||
                !Finite(shot.position.x)||!Finite(shot.position.y)||
                (shot.position-CampWardFoot).Length>3.25f||
                !ClearAttackPath(CampWardFoot,shot.position))
                return false;
            Point hit=shot.position;
            campWardCharges--;
            effects.Add(new Effect("projectileImpact",hit,.30f)
                {variant=4,direction=shot.velocity.Normalized,height=16f});
            effects.Add(new Effect("number",CampWardFoot+
                new Point(0f,-.8f),.8f,
                L("守火拦截 · 剩 "+campWardCharges,
                  "FIREWARD · "+campWardCharges+" LEFT")));
            sound="counter";revision++;
            if(campWardCharges==0)ResetCampWard();
            return true;
        }
        private bool ChooseCampWard(string action)
        {
            if(action!="annex_ward_light"&&action!="annex_ward_douse")
                return false;
            WorldObject camp=pendingDialogueSource;
            if(save.room<0||save.room>7||mode!="world"||
                Dying||practicing||camp==null||camp.kind!="annexcamp"||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(camp)||
                !HasPhysicalInteractionReach(camp))return true;
            if(action=="annex_ward_douse")
            {
                if(CampWardActive&&Object.ReferenceEquals(campWardCamp,camp))
                {
                    ResetCampWard();revision++;sound="ui";
                    Notify("守火已熄灭。剩余材料不返还。",
                        "Signal fire extinguished. Spent supplies are not refunded.");
                }
                return true;
            }
            if(CampWardActive)
            {
                Notify("当前区域的守火仍在燃烧，不能叠加次数。",
                    "Your current signal fire is still burning; charges cannot stack.");
                return true;
            }
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("正在进行现场任务或仍有危险，无法布置守火。",
                    "Finish fieldwork and clear nearby danger before preparing the fire.");
                return true;
            }
            if(!Spend(2,0,1,0,0))return true;
            campWardCamp=camp;campWardRoom=save.room;
            campWardMap=Map;campWardSave=save;
            campWardSeconds=CampSignalNetworkPublished?120f:90f;
            campWardCharges=Has("afterglow_all")?5:
                CampSignalNetworkPublished?4:3;
            campWardFlareClock=0f;
            Flag("annex_signal_fire_"+save.room);
            effects.Add(new Effect("burst",CampWardFoot,.45f)
                {radius=1f,variant=1});
            revision++;sound="reward";
            Notify("守火已点燃："+(int)campWardSeconds+"秒内拦截营地周围"+
                campWardCharges+"枚敌方飞弹。信号站已记录 "+CampSignalCount+"/8。",
                "Signal fire lit: "+(int)campWardSeconds+"s, "+
                campWardCharges+" hostile shots blocked. Network sites "+
                CampSignalCount+"/8.");
            return true;
        }

        // The public network creates eight genuine two-way field dispatches.
        // A flame packet must be carried from its real camp to the region's
        // actual lookout AND back, before world time or three real hits end
        // the run. A new SaveData field would invalidate existing v1/v2 saves,
        // so only *completed* dispatches persist as existing string flags.
        private int signalRelayRoom=-1, signalRelayStage, signalRelayIntegrity;
        private float signalRelaySeconds;
        private MapData signalRelayMap;
        private SaveData signalRelaySave;
        private WorldObject signalRelayCamp,signalRelayMarker;
        private static readonly string[] relayMessageZh={
            "外渠水闸的操作员终于接到了被遗漏的低洼住户名单。警戒钟先通知人，再通知货。",
            "荆棘林的回讯来自树林深处的孩子。他们把回家的路线画在树皮上，守路人把它换成所有人都看得懂的路标。",
            "沉没遗迹的渡工确认了新的涨潮时刻。没有行会通行证的渡船，也能看到岸上的安全灯。",
            "铁脊矿工从高台传回三次急停铃的顺序，废弃铁轨从此优先为撤离者留路。",
            "堡垒旧岗的收信人不是军官，而是替夜归者留灯的居民。最后一道门终于向普通旅人打开。",
            "雾港的夜航讯号带回一艘失联救援艇的航向。岸上的火不再只照顾缴了泊位费的船。",
            "玻璃风沙脊传来的井位，标上了真实的水量；后来者不必再为虚假的商队水图绕远。",
            "霜钟崖的回声确认暖棚仍有人守夜。冻土上的火光是一封不会被封锁的求援信。"
        };
        private static readonly string[] relayMessageEn={
            "The canal-keeper receives the overlooked lowland household list. The flood bell will warn people before cargo.",
            "A message returns from children deep in Thornwood. Their bark-drawn path becomes a public sign anyone can follow home.",
            "The ruins' ferrymen confirm the next high tide. Unregistered boats can now see the same safety lights as guild craft.",
            "Ironreach workers send back the three-bell shutdown order. Empty rails will serve evacuating families first.",
            "The citadel lookout's recipient is a resident who keeps lamps for late travelers, not an officer. The final gate opens to the public.",
            "Mistport's night signal brings the heading of a missing rescue skiff. Shore fires no longer serve only fee-paying vessels.",
            "Glasswind's reply marks the real water level of an unregistered well. No traveler must trust a false merchant chart.",
            "Frostchime's echo confirms the warming shelter still has a night watch. A fire on the frozen ridge is a rescue letter no checkpoint can stop."
        };
        public bool SignalRelayActive
        { get { return signalRelayStage!=0&&signalRelaySeconds>0f&&
            signalRelayIntegrity>0&&signalRelayRoom==save.room&&
            Object.ReferenceEquals(signalRelayMap,Map)&&
            Object.ReferenceEquals(signalRelaySave,save)&&
            signalRelayCamp!=null&&signalRelayMarker!=null&&
            SourceBelongsToActiveMap(signalRelayCamp)&&
            SourceBelongsToActiveMap(signalRelayMarker); } }
        public int SignalRelayStage {get{return SignalRelayActive?signalRelayStage:0;}}
        public int SignalRelayIntegrity {get{return SignalRelayActive?signalRelayIntegrity:0;}}
        public float SignalRelaySeconds {get{return SignalRelayActive?signalRelaySeconds:0f;}}
        public Point SignalRelayTarget
        {get{WorldObject o=SignalRelayActive?
            (signalRelayStage==1?signalRelayMarker:signalRelayCamp):null;
            return o==null?new Point():new Point(o.x,o.y);}}
        public int SignalRelayDoneCount
        {get{int count=0;for(int room=0;room<8;room++)
            if(Has("annex_relay_done_"+room))count++;
            return count;}}
        private bool IsSignalRelayDestination(WorldObject marker)
        {return SignalRelayStage==1&&marker!=null&&
            Object.ReferenceEquals(marker,signalRelayMarker);}
        private void ResetSignalRelay()
        {
            signalRelayRoom=-1;signalRelayStage=signalRelayIntegrity=0;
            signalRelaySeconds=0f;signalRelayMap=null;signalRelaySave=null;
            signalRelayCamp=signalRelayMarker=null;
        }
        private void TickSignalRelay(float dt)
        {
            if(signalRelayStage==0)return;
            if(!SignalRelayActive){ResetSignalRelay();return;}
            signalRelaySeconds=Math.Max(0f,signalRelaySeconds-dt);
            if(signalRelaySeconds<=0f)
            {
                ResetSignalRelay();revision++;
                Notify("信号火种熄灭，传讯超时。回到营地可以重新出发。",
                    "The signal flame went out. Return to camp to retry the relay.");
            }
        }
        private void DamageSignalRelayCargo(float actualDamage,string source)
        {
            // Only the authoritative post-armor HP hit may harm a packet.
            // Dodged, parried, blocked and zero-damage contacts cannot do so.
            if(!SignalRelayActive||!Finite(actualDamage)||
                actualDamage<=0f)return;
            signalRelayIntegrity--;
            if(signalRelayIntegrity<=0)
            {
                ResetSignalRelay();revision++;
                Notify("信号火种在实战中损毁。请回营地重新接取任务。",
                    "The dispatch flame was destroyed by real damage. Restart at camp.");
            }
            else
            {
                effects.Add(new Effect("number",Position,.85f,
                    L("传讯火种受损 · 剩 "+signalRelayIntegrity,
                      "FLAME HIT · "+signalRelayIntegrity+" LEFT")));
                revision++;
            }
        }
        private bool ChooseSignalRelay(string action)
        {
            if(action!="annex_relay_begin"&&action!="annex_relay_deliver"&&
                action!="annex_relay_report"&&action!="annex_relay_abort")
                return false;
            WorldObject source=pendingDialogueSource;
            if(source==null||mode!="world"||Dying||practicing||
                save.room<0||save.room>7||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            int region=save.room;
            if(action=="annex_relay_begin")
            {
                if(source.kind!="annexcamp"||SignalRelayActive||
                    !CampSignalNetworkPublished||
                    !Has("annex_signal_fire_"+region)||
                    Has("annex_relay_done_"+region))return true;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("现场行动或附近攻击尚未解除，不能出发送讯。",
                        "Finish fieldwork and clear nearby attacks before dispatch.");
                    return true;
                }
                WorldObject marker=null;
                string targetId="ridge_route_"+region+"_"+(region%2);
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="ridgemarker"&&o.id==targetId)
                    {marker=o;break;}
                if(marker==null||!CanStand(new Point(marker.x,marker.y))||
                    (new Point(marker.x,marker.y)-
                     new Point(source.x,source.y)).Length<24f)
                    return true;
                signalRelayRoom=region;signalRelayMap=Map;signalRelaySave=save;
                signalRelayCamp=source;signalRelayMarker=marker;
                signalRelayStage=1;signalRelayIntegrity=3;
                signalRelaySeconds=300f;
                effects.Add(new Effect("release",new Point(source.x,source.y),
                    .55f){variant=1,height=16f});
                revision++;sound="quest";
                Notify("已接取烽火传讯：5分钟内走到远处真实高地路标交接火种，再回营地复命。三次受伤会烧毁火种。",
                    "Signal dispatch started: reach the distant real ridge marker, then return to this camp within 5 minutes. Three damaging hits destroy the packet.");
                return true;
            }
            if(!SignalRelayActive)return true;
            if(action=="annex_relay_abort")
            {
                if(source.kind!="annexcamp"||
                    !Object.ReferenceEquals(source,signalRelayCamp))return true;
                ResetSignalRelay();revision++;sound="ui";
                Notify("已取消传讯，没有提交回执。可以再次接取。",
                    "Dispatch withdrawn without credit. You may retry.");
                return true;
            }
            if(action=="annex_relay_deliver")
            {
                if(signalRelayStage!=1||source.kind!="ridgemarker"||
                    !Object.ReferenceEquals(source,signalRelayMarker))return true;
                signalRelayStage=2;revision++;sound="quest";
                effects.Add(new Effect("discover",new Point(source.x,source.y),.7f));
                Notify("高地路标已确认信号。带着回执沿真实道路返回出发营地。",
                    "Ridge marker acknowledged. Carry its reply back to your departure camp.");
                return true;
            }
            if(signalRelayStage!=2||source.kind!="annexcamp"||
                !Object.ReferenceEquals(source,signalRelayCamp))return true;
            int completedRoom=signalRelayRoom;
            ResetSignalRelay();
            if(Has("annex_relay_done_"+completedRoom))return true;
            Flag("annex_relay_done_"+completedRoom);
            CreditCoins(20);CreditShards(1);AddXp(40);
            RecordMoment(relayMessageZh[completedRoom],relayMessageEn[completedRoom]);
            if(SignalRelayDoneCount==8&&!Has("annex_relay_all"))
            {
                Flag("annex_relay_all");CreditSkillPoints(1);
                RecordMoment("八地烽火都已收到来回确认。从此没有城门、军令或关卡能独占这些普通旅人互相寻找的路线。",
                    "All eight civilian dispatches return with acknowledgements. No gate or officer can monopolize a road made of ordinary travelers finding each other.");
            }
            revision++;sound="reward";
            Notify("烽火传讯完成：硬币 +20、碎片 +1、经验 +40。已完成 "+
                SignalRelayDoneCount+"/8。",
                "Signal dispatch complete: +20 coins, +1 shard, +40 XP. "+
                SignalRelayDoneCount+"/8 verified.");
            return true;
        }

        private bool TalkAnnexExploration(WorldObject o)
        {
            string kind=o.kind;
            if(kind=="annexinnarchive" || kind=="annexinnmessage")
            {
                if(save.room!=9 || !HasPhysicalInteractionReach(o))return true;
                if(kind=="annexinnmessage")
                {
                    bool firstLetter=!Has("annex_inn_letter");
                    if(firstLetter){Flag("annex_inn_letter");AddXp(15);revision++;sound="reward";}
                    Dialog(o.name,o.en,
                        "给每一位夜归的人：如果你迷路了，沿着有人点起营火的方向走。道路不是堡垒的财产，也不是公会的财产。它是记得你还要回家的人留下的。"+
                        (firstLetter?"\n\n首次读信：经验 +15。":""),
                        "To every late traveler: when lost, follow the light of a shared campfire. A road belongs neither to a fortress nor a guild; it belongs to those who remember you are coming home."+
                        (firstLetter?"\n\nFirst reading: +15 XP.":""),
                        C("留下这封信","Keep the letter","close"));
                    return true;
                }
                int camps=0,cache=0,vistas=0,signals=CampSignalCount,dispatches=SignalRelayDoneCount,watches=AfterglowWins;
                for(int region=0;region<8;region++)
                {
                    if(Has("annex_camp_annex_camp_"+region))camps++;
                    if(Has("annex_vista_annex_vista_"+region))vistas++;
                    if(Has("annex_cache_annex_cache_"+region+"_a"))cache++;
                    if(Has("annex_cache_annex_cache_"+region+"_b"))cache++;
                }
                bool complete=Has("annex_archive_complete");
                bool ready=camps==8&&vistas==8&&cache==16;
                Dialog(o.name,o.en,
                    "这是巡路队留在炉火旅店的真正旅行档案。你的步迹必须是实地走过的，而不是地图上凭空出现的标记。"+
                    "\n\n营地 "+camps+"/8  ·  观景点 "+vistas+"/8  ·  隐藏补给 "+cache+"/16。"+
                    "\n\n守火信号 "+signals+"/8 · 来回传讯 "+dispatches+
                    "/8 · 余烬守望 "+watches+"/8。"+
                    (Has("afterglow_all")?" 八地守望完成，营火防护上限提高到5次。":"")+
                    (Has("annex_relay_archived")?
                        "\n\n《归途回声》已经存入旅店的公共史册。户外耐力恢复永久 +2/秒。":
                        Has("annex_relay_all")?
                        "\n\n八地烽火都收到回讯：技能点 +1。带着这些回执回旅店完成最后的记录。":
                        CampSignalNetworkPublished?
                        "\n\n信号网已公开。现在可在各地营地接取限时往返传讯，带回远处高地的真实回执。":"")+
                    (CampSignalNetworkPublished?
                        " 灯网公开后，守火持续120秒并可抵挡4枚来袭飞弹。":
                        signals==8?
                        " 八地守火已经实地点亮，可以把信号标准公布给所有旅人。":
                        " 在每个地区营地实际付出木材与药草点亮信号后，回来公开共用灯网。")+
                    (complete?"\n\n这份公开地图已经交给所有旅人。":
                        ready?"\n\n每一处地点都被亲自验证。可以公开最后的归乡图。":
                        "\n\n继续探索全部八个户外区域，回来归档旧路。"),
                    "The Hearth Inn holds the wardens' actual travel record. Every mark must be made by walking, never conjured on a hidden map."+
                    "\n\nCamps "+camps+"/8 · Lookouts "+vistas+"/8 · Caches "+cache+"/16."+
                    "\n\nSignal fires "+signals+"/8 · Return dispatches "+dispatches+
                    "/8 · Afterglow watches "+watches+"/8."+
                    (Has("afterglow_all")?" Eight watches secured: each prepared fire now blocks five shots.":"")+
                    (Has("annex_relay_archived")?
                        "\n\nHomeward Echoes is now part of the public chronicle. Outdoor stamina recovery is permanently +2/s.":
                        Has("annex_relay_all")?
                        "\n\nAll eight dispatches confirmed: +1 talent point. Return the replies to the Inn for a final public account.":
                        CampSignalNetworkPublished?
                        "\n\nThe public network now offers timed two-way dispatches at the eight real outdoor camps.":"")+
                    (CampSignalNetworkPublished?
                        " The public watch now protects 4 shots for 120 seconds per fire.":
                        signals==8?
                        " All eight paid field signals are verified. Publish a shared civilian fire code.":
                        " Prepare a paid signal fire at each region's real camp to make a public safety network.")+
                    (complete?"\n\nYour route chart is now public.":ready?
                        "\n\nAll positions verified. The final homeward chart can be published.":
                        "\n\nExplore all eight outdoor regions and return to publish their chart."),
                    C(complete?"重读归乡图":"完成归档 · 公共星图",
                        complete?"Reread the chart":"Publish the public chart","annex_archive_file"),
                    signals==8&&!CampSignalNetworkPublished?
                        C("公开八地守火信号","Publish eight signal fires","annex_signal_publish"):
                        C("继续核对守火","Keep tracing signals","close"),
                    Has("annex_relay_all")&&SignalRelayDoneCount==8&&
                        !Has("annex_relay_archived")?
                        C("提交八地回执 · 归途回声","File eight replies · Homeward Echoes","annex_relay_archive"):
                        C("查看公开回执","Read public replies","close"),
                    C("继续旅程","Keep exploring","close"));
                return true;
            }
            if(kind!="annexcache"&&kind!="annexcamp"&&kind!="annexvista")return false;
            if(save.room<0||save.room>7||!HasPhysicalInteractionReach(o))return true;
            string key=(kind=="annexcache"?"annex_cache_":kind=="annexvista"?"annex_vista_":"annex_camp_")+o.id;
            bool first=!Has(key);
            int cacheMaterial = save.room==1 || save.room==7 ? 2 :
                save.room==3 || save.room==6 ? 1 : 0;
            if(first && kind=="annexcache" &&
                (save.coins > 999992 || save.inventory[cacheMaterial] > 9997))
            {
                Notify("探索补给仍可领取。请先留出7硬币和2份材料的容量。",
                    "Exploration supplies remain here. Free room for 7 coins and 2 materials.");
                return true;
            }
            if(first)
            {
                Flag(key);sound="reward";
                if(kind=="annexcache")
                {
                    CreditCoins(7);
                    CreditInventory(cacheMaterial, 2);
                }
                else if(kind=="annexvista")
                {
                    AddXp(12);
                    int cx=(int)o.x/6,cy=(int)o.y/6;
                    int columns=(Map.rows[0].Length+5)/6;
                    int rows=(Map.rows.Length+5)/6;
                    for(int x=cx-1;x<=cx+1;x++)for(int y=cy-1;y<=cy+1;y++)
                        if(x>=0&&x<columns&&y>=0&&y<rows&&(x*6>=36||y*6>=24))
                            Flag("annex_seen_"+save.room+"_"+x+"_"+y);
                }
                else
                {
                    CreditCoins(5);
                    bool all=true;
                    for(int i=0;i<8;i++)if(!Has("annex_camp_annex_camp_"+i))all=false;
                    if(all&&!Has("annex_camps_complete")){Flag("annex_camps_complete");CreditSkillPoints(1);}
                }
                revision++;
            }
            string[] zh={"外渠的雨脚印一直指向镇里。","荆棘远眺台的树根曾为旅客挡风。",
                "沉桥后湾里有不在公会账本上的船板。","荒矿旧道留下先撤人再开机的划痕。",
                "堡垒外墙的石阶原本供难民歇脚。","灯湾旧船铃是为错过退潮的人准备的。",
                "折光沙脊后藏着商队不曾登记的井。","霜钟背风崖的箭头指向最近的火盆。"};
            string[] en={"Rain-worn prints along the outer canal lead home.","Roots at the Thornwood overlook once sheltered travelers.",
                "Unregistered ferry planks remain in the backwater.","Mine marks command evacuation before restart.",
                "Outer citadel steps once sheltered displaced families.","The old ship bell still guides stranded travelers.",
                "A hidden well lies beyond the dunes.","A frost cliff sign points to the nearest hearth."};
            string noteZh=zh[save.room],noteEn=en[save.room];
            if(kind=="annexcache")
            {
                Dialog(o.name,o.en,noteZh+(first?"\n\n遗失补给：硬币 +7、材料 +2。":"\n\n物资已被取走，记录仍在。"),
                    noteEn+(first?"\n\nRecovered supplies: +7 coins, +2 materials.":"\n\nSupplies already recovered."),
                    C("继续巡路","Continue","close"));
                return true;
            }
            if(kind=="annexvista")
            {
                Dialog(o.name,o.en,noteZh+(first?"\n\n附近九块测绘区已记入星图。经验 +12。":"\n\n附近路线已在星图上。"),
                    noteEn+(first?"\n\nCharted nearby terrain sectors. +12 XP.":"\n\nNearby bearings already charted."),
                    C("收起星图","Fold chart","close"));
                return true;
            }
            Choice watch=AfterglowActive&&
                Object.ReferenceEquals(afterglowCamp,o)?
                (AfterglowPhase==2?
                    C("掩护避难者 · 常规二人阵","Screen shelters · two raiders","afterglow_screen"):
                    C("中止余烬守望","Abort afterglow watch","afterglow_abort")):
                Has("annex_relay_archived")&&Has("annex_relay_done_"+save.room)&&
                !Has("afterglow_done_"+save.room)?
                    C("余烬守望 · 双波夜袭","Afterglow watch · two-wave raid","afterglow_begin"):
                    C("查看守望记录","Read firewatch record","close");
            Choice watchCut=AfterglowActive&&AfterglowPhase==2&&
                Object.ReferenceEquals(afterglowCamp,o)?
                    C("主动截击 · 重甲三人阵","Intercept · armored trio","afterglow_cut"):
                    C("继续巡路","Keep walking","close");
            Choice dispatch=SignalRelayActive&&
                Object.ReferenceEquals(signalRelayCamp,o)?
                    (SignalRelayStage==2?
                        C("交回高地回执","Return the ridge reply","annex_relay_report"):
                        C("放弃本次传讯","Withdraw dispatch","annex_relay_abort")):
                CampSignalNetworkPublished&&!Has("annex_relay_done_"+save.room)?
                    C("出发烽火传讯 · 往返挑战","Carry the signal · round trip","annex_relay_begin"):
                    C("查看传讯记录","Review dispatch record","close");
            Dialog(o.name,o.en,noteZh+
                    "\n\n只有没有敌人追击、没有进行中的现场任务时才能安全生火。消耗木材 ×1，恢复生命 40 和全部耐力。"+
                    (SignalRelayActive?"\n\n当前传讯："+SignalRelayStage+"/2段，火种完整度 "+
                        SignalRelayIntegrity+"/3，剩 "+(int)SignalRelaySeconds+" 秒。":
                        Has("annex_relay_done_"+save.room)?
                        "\n\n本地区的来回传讯已完成，回执已公开。":"")+
                    (AfterglowActive?"\n\n余烬守望 "+AfterglowPhase+"/3，敌人 "+
                        AfterglowRemaining+"，剩 "+(int)AfterglowTime+" 秒。":
                        Has("afterglow_done_"+save.room)?"\n\n本地余烬守望已胜利。":"")+
                    (first?"\n\n首次发现营地，硬币 +5。":"")+
                    (Has("annex_camps_complete")?"\n\n八处营地均已记载！技能点 +1。":""),
                    noteEn+"\n\nRest only when no enemies or active field operations remain. 1 timber restores 40 HP and full energy."+
                    (SignalRelayActive?"\n\nDispatch stage "+SignalRelayStage+"/2 · flame "+
                        SignalRelayIntegrity+"/3 · "+(int)SignalRelaySeconds+"s left.":
                        Has("annex_relay_done_"+save.room)?
                        "\n\nThis region's return dispatch is already in the public archive.":"")+
                    (AfterglowActive?"\n\nAfterglow "+
                        AfterglowPhase+"/3 · foes "+AfterglowRemaining+
                        " · "+(int)AfterglowTime+"s.":
                        Has("afterglow_done_"+save.room)?"\n\nThis afterglow watch is cleared.":"")+
                    (first?"\n\nFirst discovery: +5 coins.":"")+
                    (Has("annex_camps_complete")?"\n\nAll eight camps mapped: +1 talent point.":""),
                    !FieldWorkPreventsRest&&SafeTravel?
                        C("添柴休息 · 木材 ×1","Rest · 1 timber","annex_rest"):
                        C("先完成现场行动","Finish fieldwork / clear threats","close"),
                    CampWardActive&&Object.ReferenceEquals(campWardCamp,o)?
                        C("熄灭守火","Douse signal fire","annex_ward_douse"):
                        C("布设守火 · 木材×2、月光草×1",
                          "Signal fire · 2 timber, 1 Moonherb","annex_ward_light"),
                    dispatch,watch,watchCut,
                    C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseAnnexExploration(string action)
        {
            if(action=="annex_relay_archive")
            {
                WorldObject archive=pendingDialogueSource;
                if(save.room!=9||mode!="world"||Dying||practicing||
                    !Has("annex_relay_all")||SignalRelayDoneCount!=8||
                    Has("annex_relay_archived")||
                    archive==null||archive.kind!="annexinnarchive"||
                    pendingDialogueSourceRoom!=9||
                    !SourceBelongsToActiveMap(archive)||
                    !HasPhysicalInteractionReach(archive))return true;
                Flag("annex_relay_archived");
                CreditCoins(80);CreditShards(3);
                RecordMoment(
                    "炉火旅店里，值夜的记录员把八张浸湿的回执压平。有人问：那些还在路上的人都回家了吗？她没有敷衍，只把一盏小灯移到未写完的那一栏，说：还没有，但现在我们知道该往哪里去接他们。",
                    "At the Hearth Inn the night recorder smooths eight rain-soaked replies. Someone asks if every traveler is home. She moves a lamp beside the unfinished names: Not yet. But now we know where to meet them.");
                revision++;sound="reward";
                Dialog("归途回声 · 公共史册","Homeward Echoes · Public Chronicle",
                    "窗外的雨落在旧路牌上。有人轻轻擦净上面的名字，谁也没有喊口号。八地的水尺、风铃、矿铃和灯火，终于指向同一件事：还没回来的人，不该从地图上消失。"+
                    "\n\n记录员在最后一页盖上公共印记，又把纸递给门边的旅人，让他也抄一份。"+
                    "\n\n终章奖励：硬币 +80、碎片 +3。往后在八个户外地区，耐力恢复永久 +2/秒。",
                    "Rain taps on the old wayfinding signs. Someone carefully wipes a name clean. Nobody makes a speech. Eight gauges, chimes, mine bells and fires now serve one promise: people still on the road must not disappear from a map."+
                    "\n\nThe recorder stamps the public copy, then hands the page to a traveler by the door so he can copy it too."+
                    "\n\nFinal reward: +80 coins, +3 shards. Stamina now regenerates +2/s throughout the eight outdoor regions.",
                    C("收起公共史册","Close the public chronicle","close"));
                return true;
            }
            if(action=="annex_signal_publish")
            {
                // Publication is a real in-person conversation with the
                // surviving archive, not an action replayed from another NPC.
                WorldObject archive=pendingDialogueSource;
                if(save.room!=9||Dying||practicing||CampSignalNetworkPublished||
                    archive==null||archive.kind!="annexinnarchive"||
                    pendingDialogueSourceRoom!=9||
                    !SourceBelongsToActiveMap(archive)||
                    !HasPhysicalInteractionReach(archive))return true;
                if(CampSignalCount!=8)
                {
                    Notify("仍有没点亮守火的地区。公共信号网不能只在地图上凭空画出来。",
                        "The network needs eight real field signals, not imagined map marks.");
                    return true;
                }
                Flag("annex_signal_network_published");
                CreditCoins(40);CreditShards(2);AddXp(90);
                RecordMoment("炉火旅店收到了八地信号的原始记录。守路人把火光如何避开断墙、保护晚归的行人，写成任何人都能使用的公用守火准则。火光不再只照亮有通行证的道路。",
                    "The Hearth Inn publishes the eight proven fire signals as a civilian ward code. Their lights protect anyone returning at night, not merely travelers carrying a citadel permit.");
                revision++;sound="reward";
                Notify("八地守火信号网已公开！奖励硬币40、碎片2、经验90；今后守火可持续120秒、拦截4枚飞弹。",
                    "The eight-signal network is public! +40 coins, +2 shards, +90 XP. Fires now last 120 seconds and block 4 shots.");
                return true;
            }
            if(action=="annex_archive_file")
            {
                if(save.room!=9||Dying||practicing||Has("annex_archive_complete"))
                    return true;
                WorldObject archive=null;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="annexinnarchive"){archive=o;break;}
                if(archive==null||!HasPhysicalInteractionReach(archive))
                    return true;
                for(int region=0;region<8;region++)
                    if(!Has("annex_camp_annex_camp_"+region)||
                        !Has("annex_vista_annex_vista_"+region)||
                        !Has("annex_cache_annex_cache_"+region+"_a")||
                        !Has("annex_cache_annex_cache_"+region+"_b"))
                    {
                        Notify("星图还缺少实际走过的地点。","Some routes have yet to be visited.");
                        return true;
                    }
                Flag("annex_archive_complete");
                CreditCoins(60);CreditShards(3);CreditSkillPoints(1);
                RecordMoment("八地营地、观景点与补给旧路均已核实。炉火旅店的公开星图不再遗漏任何普通旅人。",
                    "Eight regions of camps, lookouts and recovered supplies are now public on the Hearth Inn's atlas.");
                revision++;sound="reward";
                Notify("归乡图已公开：硬币 +60，碎片 +3，技能点 +1。",
                    "Public chart complete: +60 coins, +3 shards, +1 talent point.");
                return true;
            }
            if(ChooseAfterglow(action))return true;
            if(ChooseSignalRelay(action))return true;
            if(ChooseCampWard(action))return true;
            if(action!="annex_rest")return false;
            if(save.room<0||save.room>7||mode!="world"||Dying||practicing)return true;
            // Only the exact camp the player spoke to can spend supplies.
            // A stale/foreign dialogue must not silently consume timber at
            // whichever camp happens to be first in the room's object list.
            WorldObject camp=pendingDialogueSource;
            if(camp==null||camp.kind!="annexcamp"||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(camp)||
                !HasPhysicalInteractionReach(camp))return true;
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("现场行动或敌人的攻击尚未结束，不能安全休息。",
                    "Finish fieldwork and clear nearby attacks before resting.");
                return true;
            }
            if(save.health>=save.maxHealth&&save.energy>=100f)
            {
                Notify("生命和耐力已满，保留木材下次使用。",
                    "Health and energy are full; save your timber for later.");
                return true;
            }
            if(!Spend(1,0,0,0,0))return true;
            save.health=Clamp(save.health+40,0,save.maxHealth);save.energy=100;
            revision++;sound="reward";
            Notify("营火补给：生命恢复 40，耐力全满。","Campfire: +40 HP and full stamina.");
            return true;
        }
    }
}
