using System;

namespace Starfall
{
    // Mistport's last-light chapter is a REAL timed shoreline emergency.
    // Gusts damage a working signal in world time; no clock advances in menus.
    // Authoritative public/secret conclusions use existing SaveData v2 flags.
    public sealed partial class GameCore
    {
        private int mistlightStage;
        private float mistlightTime,mistlightIntegrity,mistlightGust,mistlightShield;
        private bool mistlightRepairUsed;
        public bool MistlightActive {get{return mistlightStage>0&&save.room==5;}}
        public bool MistlightComplete {get{return Has("mistlight_public")||Has("mistlight_private");}}
        public bool MistlightPublic {get{return Has("mistlight_public");}}
        public bool MistlightPrivate {get{return Has("mistlight_private");}}
        public int MistlightStage {get{return MistlightActive?mistlightStage:0;}}
        public float MistlightTime {get{return MistlightActive?mistlightTime:0f;}}
        public float MistlightIntegrity {get{return MistlightActive?mistlightIntegrity:0f;}}
        public float MistlightNextGust {get{return MistlightActive?mistlightGust:0f;}}
        public float MistlightShield {get{return MistlightActive?mistlightShield:0f;}}
        public bool MistlightRepairUsed {get{return MistlightActive&&mistlightRepairUsed;}}
        public Point MistlightTarget {get{return mistlightStage==1?
            new Point(60.5f,37.5f):mistlightStage==2?
            new Point(65.5f,38.5f):mistlightStage==3?
            new Point(66.5f,43.5f):new Point(54.5f,38.5f);}}
        public string MistlightObjective {get{return mistlightStage==1?
            L("点燃上岸的旧信标","IGNITE THE OLD SHORE BEACON"):mistlightStage==2?
            L("在阵风间隙校准海镜","ALIGN THE SEA MIRROR BETWEEN GUSTS"):
            mistlightStage==3?L("抵达栈桥，决定求援信号","REACH THE PIER · CHOOSE WHO SEES THE LIGHT"):
            L("调查雾港旧灯塔告示","READ THE LAST-LIGHT NOTICE");}}
        private string MistlightInnNews(bool english)
        {
            if(MistlightPublic)
                return english?
                    "\n\nThe night traveler has laid a wet chart beside the stew pot. A woman in a patched fishing coat taps the empty place for her brother's boat. 'He wasn't on the list, so nobody lit a lamp.' Outside, bells answer one another: the harbor is welcoming every returning vessel now, named or not.":
                    "\n\n守夜旅人把湿透的海图压在汤锅旁。穿补丁渔衣的女人指着港籍上的空白：‘我弟弟的船没有登记，所以没人肯替他亮灯。’窗外忽然响起一串船铃——如今雾港不再只为名册里的人点灯。";
            if(MistlightPrivate)
                return english?
                    "\n\nThe night traveler lowers his voice while folding a tide chart. 'The patrol asked why the southern light never rose. I told them the glass was broken.' A pilot at the far table smiles without looking up. Beyond the window, a low lantern glints once through the fog. Another boat has found the hidden way home.":
                    "\n\n守夜旅人收起潮汐图，压低声音说：‘巡兵问南边的灯为什么没亮，我说镜子坏了。’远桌的引航员埋头喝汤，嘴角却微微翘起。窗外浓雾中，一盏低矮的灯闪了一下。又有一条船从隐蔽航道平安回家。";
            return "";
        }
        private void ResetMistlight()
        {
            mistlightStage=0;mistlightTime=mistlightIntegrity=mistlightGust=mistlightShield=0f;
            mistlightRepairUsed=false;
        }
        private bool MistlightSource(WorldObject o,string id)
        {
            return o!=null&&save.room==5&&o.id==id&&
                SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o);
        }
        private bool TalkMistlight(WorldObject o)
        {
            if(o==null||save.room!=5||!o.kind.StartsWith("mistlight",
                StringComparison.Ordinal))return false;
            if(!MistlightSource(o,o.id))return true;
            if(o.id=="mistlight_notice")
            {
                if(MistlightComplete)
                {
                    Dialog(o.name,o.en,MistlightPublic?
                        "最后一盏灯如今朝每条渔船亮起。港口在夜里也不再把没有名册的人当作陌生人。":
                        "最后一盏灯变成了低矮信号。只有知道旧航道的人才会看懂它。一般船夫不必再独自摸黑。",
                        MistlightPublic?
                        "The last light now shines for every fishing boat. The harbor no longer treats people missing from its rolls as strangers.":
                        "The last light now signals low along the old channel. Only pilots who know that route can read it.",
                        C("离开","Leave","close"));return true;
                }
                if(mistlightStage==0)
                {
                    Dialog(o.name,o.en,
                        "潮汐把最后一座信标从底座撬松了。守灯人没能等到换班，他留下的记录只有一句：‘船上有人。’先去北边点亮旧灯，再到东墙海镜趁阵风空隙校准，最后赶往南栈桥选一条求援方式。灯芯会在每波风暴里受损；备用零件可换取一次防风保护。时间耗尽或灯芯毁坏就得重来。",
                        "The tide has pulled the last beacon loose. Its keeper never returned from his watch. His log ends with four words: 'There are people aboard.' Light the northern shore beacon, align the eastern sea mirror between gusts, and reach the southern pier to choose how to signal. Each storm damages the live lamp; one spare part can brace it. If the clock or lamp fails, retry the rescue.",
                        C("救回最后的求援灯","Restore the last light","mistlight_start"),
                        C("稍后再来","Not yet","close"));return true;
                }
                Dialog(o.name,o.en,
                    "灯塔的风声还在变急，码头外还有人在等那束光。坚持完成这次行动，或者在这里放弃并重新准备。",
                    "The storm is rising, and people are still waiting offshore. Finish this attempt or withdraw and prepare another.",
                    C("继续抢修","Keep working","close"),
                    C("撤回本次抢修","Abort attempt","mistlight_abort"));return true;
            }
            if(o.id=="mistlight_lamp")
            {
                if(mistlightStage==1)
                {
                    Dialog(o.name,o.en,
                        "你在底座找到守灯人的煤油布。再拧紧半圈，光就能穿过浅滩。",
                        "The keeper left oilcloth under the lamp. One last turn will throw light across the shallows.",
                        C("点亮旧信标 · 耐力12","Ignite beacon · 12 energy","mistlight_ignite"),
                        C("暂不点亮","Leave it","close"));return true;
                }
                if(mistlightStage>=2&&!mistlightRepairUsed)
                {
                    Dialog(o.name,o.en,
                        "铁架在风里吱嘎作响。用1份零件可以补强灯座，恢复30点灯芯完整度，并挡下一阵风；你也可以保留零件赌最后的路程。",
                        "The frame rattles in the gale. Spend 1 machine part to restore 30 lamp integrity and shelter it from the next gust, or save the part and gamble on reaching the pier.",
                        C("零件1 · 加固信标","1 part · brace beacon","mistlight_brace"),
                        C("保留零件","Keep part","close"));return true;
                }
                Dialog(o.name,o.en,
                    "灯已点亮。海镜必须接住这束光，再把它引向求援的船。",
                    "The beacon is lit. The sea mirror must catch the beam and carry it toward the boats.",
                    C("离开","Leave","close"));return true;
            }
            if(o.id=="mistlight_mirror")
            {
                Dialog(o.name,o.en,mistlightStage==2?
                    "破裂的镜框只能在风停的一瞬间转动。望见浪尖变白时，先等阵风过去。":
                    "海镜朝着栈桥，反光在潮雾里划出了一条路。",
                    mistlightStage==2?
                    "The fractured frame can turn only between gusts. When the wave tips turn white, wait for the wind to pass.":
                    "The sea mirror already points toward the pier; a beam cuts a path through the fog.",
                    C("调准海镜","Align the sea mirror","mistlight_align"),
                    C("观察风向","Watch wind","close"));return true;
            }
            if(o.id=="mistlight_pier")
            {
                if(mistlightStage==3)
                {
                    Dialog(o.name,o.en,
                        "灯终于照到了栈桥。远处的船笛只响了一声。你会打开最高的白色求援灯，让所有受困船只都看见港口，还是保持低位信号，引导那些不能被巡防发现的撤离者走旧航道？",
                        "The beam reaches the pier. Far offshore, a boat sounds one short horn. Raise the white public distress light for every stranded vessel, or keep the signal low for people escaping by a route the patrol must never see?",
                        C("公开点亮求援航标","Raise public rescue light","mistlight_public"),
                        C("守护秘密归航线","Protect the hidden passage","mistlight_private"));return true;
                }
                Dialog(o.name,o.en,
                    MistlightComplete?
                        "你听见回港的船铃，远远近近接成一片。":
                        "栈桥下的水越来越急。先点亮信标并校准海镜，空船才知道朝哪儿回来。",
                    MistlightComplete?
                        "Returning ship bells answer one another along the mist.":
                        "The tide runs faster beneath the pier. Light the beacon and align the mirror before the boats can find home.",
                    C("离开","Leave","close"));return true;
            }
            return false;
        }
        private bool ChooseMistlight(string action)
        {
            if(action==null||!action.StartsWith("mistlight_",StringComparison.Ordinal))return false;
            WorldObject site=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||site==null||
                pendingDialogueSourceRoom!=5||!MistlightSource(site,site.id))return true;
            if(action=="mistlight_start")
            {
                if(site.id!="mistlight_notice"||mistlightStage!=0||
                    MistlightComplete||FieldWorkPreventsRest||!SafeTravel)return true;
                ResetMistlight();mistlightStage=1;mistlightTime=110f;
                mistlightIntegrity=76f;mistlightGust=9f;
                TrackQuest("mistport_lastlight");revision++;sound="quest";
                Notify("最后一盏灯：110秒内抢修。每9秒的风暴会损伤信标。",
                    "Last Light: restore the signal in 110s. Gusts damage the lamp every 9s.");
                return true;
            }
            if(action=="mistlight_abort")
            {
                if(site.id=="mistlight_notice"&&MistlightActive)
                {ResetMistlight();revision++;Notify("抢修已撤回，可以从告示牌重新开始。",
                    "Attempt withdrawn. Restart at the notice when ready.");}
                return true;
            }
            if(!MistlightActive)return true;
            if(action=="mistlight_ignite")
            {
                if(site.id!="mistlight_lamp"||mistlightStage!=1)return true;
                if(save.energy<12f)
                {Notify("点灯需要12点耐力。","Igniting the light costs 12 energy.");return true;}
                save.energy-=12f;mistlightStage=2;revision++;sound="quest";
                Notify("旧信标点亮！去东墙海镜，在下一阵风来临前调准。",
                    "Beacon lit. Align the east mirror between gusts.");
                return true;
            }
            if(action=="mistlight_brace")
            {
                if(site.id!="mistlight_lamp"||mistlightStage<2||
                    mistlightRepairUsed)return true;
                if(save.inventory[4]<1)
                {Notify("没有备用零件，无法加固灯座。","No spare part to brace the lamp.");return true;}
                save.inventory[4]--;mistlightRepairUsed=true;
                mistlightIntegrity=Math.Min(100f,mistlightIntegrity+30f);
                mistlightShield=12f;revision++;sound="quest";
                Notify("灯座已加固：完整度+30，12秒内挡住阵风。",
                    "Beacon braced: +30 integrity and 12s of storm shelter.");
                return true;
            }
            if(action=="mistlight_align")
            {
                if(site.id!="mistlight_mirror"||mistlightStage!=2)return true;
                if(mistlightGust<=2f)
                {Notify("阵风压住镜框！等风过去再转动。","Gust is closing in! Wait for a calm interval.");return true;}
                mistlightStage=3;revision++;sound="quest";
                Notify("镜面接住灯火。到南侧栈桥，亲自决定求援方式。",
                    "The sea mirror caught the beam. Reach the southern pier and choose who sees it.");
                return true;
            }
            if(action=="mistlight_public"||action=="mistlight_private")
            {
                if(site.id!="mistlight_pier"||mistlightStage!=3||
                    MistlightComplete)return true;
                bool publicSignal=action=="mistlight_public";
                ResetMistlight();Flag(publicSignal?"mistlight_public":"mistlight_private");
                CreditCoins(60);CreditShards(2);AddXp(100);
                RecordMoment(publicSignal?
                    "雾港最后一盏灯升起公开求援白光，没人再因没登记在港口名册上而错过救援。":
                    "雾港最后一盏灯改为低位引航信号，走隐蔽航线的撤离者也能安全靠岸。",
                    publicSignal?
                    "The last Mistport beacon openly welcomed every stranded boat, whether listed on the harbor rolls or not.":
                    "The last Mistport beacon guided the hidden homeward route without giving it away to the patrol.");
                revision++;sound="reward";
                Notify(publicSignal?
                    "公开求援灯点亮！硬币+60、碎片+2、经验+100；安全栈桥可恢复生命。":
                    "秘密归航线得到保护！硬币+60、碎片+2、经验+100；雾港移速永久+0.25。",
                    publicSignal?
                    "Public rescue light lit! +60 coins, +2 shards, +100 XP; safe pier heals nearby travelers.":
                    "Hidden passage protected! +60 coins, +2 shards, +100 XP; permanent +0.25 speed in Mistport.");
                return true;
            }
            return true;
        }
        private void TickMistlight(float dt)
        {
            if(mistlightStage==0)
            {
                if(MistlightPublic&&save.room==5&&!Dying&&!practicing&&
                    SafeTravel&&!FieldWorkPreventsRest&&
                    PhysicalRefugeReach(new Point(66.5f,43.5f),2.7f))
                    save.health=Math.Min(save.maxHealth,save.health+2.2f*dt);
                return;
            }
            if(save.room!=5||Dying||practicing)
            {ResetMistlight();revision++;return;}
            mistlightTime-=dt;
            mistlightGust-=dt;
            if(mistlightShield>0f)mistlightShield=Math.Max(0f,mistlightShield-dt);
            if(mistlightGust<=0f)
            {
                mistlightGust+=9f;
                if(mistlightShield<=0f)
                {
                    mistlightIntegrity=Math.Max(0f,mistlightIntegrity-22f);
                    sound="alarm";
                    Notify("狂风撕扯灯芯！完整度-22，抓紧赶往栈桥。",
                        "Gale strikes the lamp: -22 integrity. Reach the pier!");
                }
                else
                {
                    sound="quest";
                    Notify("加固的灯座挡住了这一阵风。","Braced beacon withstands the gale.");
                }
            }
            if(mistlightTime<=0f||mistlightIntegrity<=0f)
            {
                bool broken=mistlightIntegrity<=0f;
                ResetMistlight();revision++;
                Notify(broken?"信标被风暴吞没，抢修失败。可以重新尝试。":
                    "船只驶入雾中，信号未能及时送出。可以重新尝试。",
                    broken?"Storm destroyed the beacon. Retry the emergency.":
                    "The boats disappeared into the mist. Rescue timed out; retry.");
            }
        }
    }
}
