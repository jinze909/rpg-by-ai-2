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
            campWardCharges=CampSignalNetworkPublished?4:3;
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
                int camps=0,cache=0,vistas=0,signals=CampSignalCount;
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
                    "\n\n守火信号 "+signals+"/8。"+
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
                    "\n\nSignal fires "+signals+"/8."+
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
            Dialog(o.name,o.en,noteZh+
                    "\n\n只有没有敌人追击、没有进行中的现场任务时才能安全生火。消耗木材 ×1，恢复生命 40 和全部耐力。"+
                    (first?"\n\n首次发现营地，硬币 +5。":"")+
                    (Has("annex_camps_complete")?"\n\n八处营地均已记载！技能点 +1。":""),
                    noteEn+"\n\nRest only when no enemies or active field operations remain. 1 timber restores 40 HP and full energy."+
                    (first?"\n\nFirst discovery: +5 coins.":"")+
                    (Has("annex_camps_complete")?"\n\nAll eight camps mapped: +1 talent point.":""),
                    !FieldWorkPreventsRest&&SafeTravel?
                        C("添柴休息 · 木材 ×1","Rest · 1 timber","annex_rest"):
                        C("先完成现场行动","Finish fieldwork / clear threats","close"),
                    CampWardActive&&Object.ReferenceEquals(campWardCamp,o)?
                        C("熄灭守火","Douse signal fire","annex_ward_douse"):
                        C("布设守火 · 木材×2、月光草×1",
                          "Signal fire · 2 timber, 1 Moonherb","annex_ward_light"),
                    C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseAnnexExploration(string action)
        {
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
