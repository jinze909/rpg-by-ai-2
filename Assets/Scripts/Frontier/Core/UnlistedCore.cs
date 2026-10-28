using System;

namespace Starfall
{
    // A three-act investigative RPG chapter, not another combat watch.
    // Actual field documents from two distinct caches in each region must
    // be read before the player may reason at its distant original ridge
    // marker. All completion state uses existing SaveData v2 string flags.
    public sealed partial class GameCore
    {
        private static readonly int[] unlistedRegions={0,3,5};
        private static readonly int[] unlistedRidgeSides={0,1,0};
        private static readonly string[] unlistedTitlesZh={
            "第一卷 · 水位以下的名字","第二卷 · 没有车票的矿工",
            "第三卷 · 未返航的灯船"
        };
        private static readonly string[] unlistedTitlesEn={
            "I · Names Below the Floodline","II · Workers Without Tickets",
            "III · The Missing Lantern Boat"
        };
        private static readonly string[] unlistedAzh={
            "一张被水浸过的名单。最下面的两行不是货物，而是被划去的住户名。",
            "矿灯编号比正式上工人数多了七盏；有些人的姓名没有录入班表。",
            "一份港口领粮表里，救援艇的船员始终没有领到补给配额。"
        };
        private static readonly string[] unlistedAen={
            "A flood-soaked register. Two lines marked as freight were actually erased households.",
            "Seven more mine lamps than registered workers. Their names never reached the shift ledger.",
            "The rescue boat's crew never received its rations in the harbor manifest."
        };
        private static readonly string[] unlistedBzh={
            "桥头的避难床位登记反倒保留着同一批住户的指印。",
            "旧轨道边的疏散铃记录了那些没有工牌的人曾拉响警钟。",
            "岸上的孩子把失联小艇最后一次闪灯的方向画在灯塔砖上。"
        };
        private static readonly string[] unlistedBen={
            "The bridge shelter ledger still preserves those households' thumbprints.",
            "The evacuation bell was pulled by workers who were never given badges.",
            "A child chalked the missing skiff's last light bearing onto the lighthouse bricks."
        };
        private static readonly string[] unlistedReasonZh={
            "两份证据为什么互相矛盾？是水淹坏了文件，还是有人把居民改记成货物？",
            "为什么疏散铃响过，却没有官方伤亡记录？是设备误报，还是无工牌的工人根本没被统计？",
            "为什么港口说船员已返航，灯塔却记得求救闪光？是天气造成的误判，还是名单刻意漏了救援艇？"
        };
        private static readonly string[] unlistedReasonEn={
            "Why do the two flood records disagree? Damaged paper, or residents filed as cargo?",
            "Why did the mine bell ring with no official losses? A false alarm, or invisible unbadged workers?",
            "Why does the harbor claim a return while the lighthouse saw distress? Weather, or an erased rescue boat?"
        };
        public int UnlistedAct
        {get{if(!Has("unlisted_started"))return -1;
            for(int a=0;a<3;a++)if(!Has("unlisted_solved_"+a))return a;
            return 3;}}
        public bool UnlistedDone
        {get{return Has("unlisted_public")||Has("unlisted_protected");}}
        public string UnlistedArchiveZh
        {get{int act=UnlistedAct;
            if(act<0)return "\n\n未列名者：有一本没有登记号的旧名册等人调查。";
            if(UnlistedDone)return "\n\n《未列名者》已归档，你的选择将继续影响边境的公共道路。";
            if(act==3)return "\n\n三卷证据已经现场核实。回来决定如何保存名单上的人。";
            return "\n\n《未列名者》 "+unlistedTitlesZh[act]+
                " · 前往第 "+(unlistedRegions[act]+1)+" 地调查两份旧补给记录"+
                "（"+(Has("unlisted_a_"+act)?"甲已找到":"甲未找")+"，"+
                (Has("unlisted_b_"+act)?"乙已找到":"乙未找")+"），再到原有远端高地路标推理。";}}
        public string UnlistedArchiveEn
        {get{int act=UnlistedAct;
            if(act<0)return "\n\nThe Unlisted: an uncatalogued register awaits an investigator.";
            if(UnlistedDone)return "\n\nThe Unlisted has been filed. Your decision now shapes the roads.";
            if(act==3)return "\n\nAll three chapters were proven in the field. Decide how to preserve their names.";
            return "\n\nThe Unlisted: "+unlistedTitlesEn[act]+
                ". Search both original caches in region "+(unlistedRegions[act]+1)+
                " (A "+(Has("unlisted_a_"+act)?"found":"missing")+
                ", B "+(Has("unlisted_b_"+act)?"found":"missing")+
                "), then reason at the distant original ridge marker.";}}
        public bool UnlistedFieldObjectiveActive
        {get{int act=UnlistedAct;return act>=0&&act<3&&
            save.room==unlistedRegions[act]&&!UnlistedDone;}}
        public string UnlistedFieldObjective
        {get{if(!UnlistedFieldObjectiveActive)return "";
            int act=UnlistedAct;
            if(!Has("unlisted_a_"+act))
                return L("核验第一份补给记录","VERIFY FIRST CACHE");
            if(!Has("unlisted_b_"+act))
                return L("核验第二份补给记录","VERIFY SECOND CACHE");
            return L("前往高地路标推理","DEDUCE AT RIDGE MARKER");}}
        public Point UnlistedFieldTarget
        {get{
            int act=UnlistedAct;
            if(!UnlistedFieldObjectiveActive)return new Point();
            string id=!Has("unlisted_a_"+act)?
                "annex_cache_"+save.room+"_a":
                !Has("unlisted_b_"+act)?
                "annex_cache_"+save.room+"_b":
                "ridge_route_"+save.room+"_"+unlistedRidgeSides[act];
            foreach(WorldObject site in Map.objects)
                if(site!=null&&site.id==id)
                    return new Point(site.x,site.y);
            return new Point();
        }}
        private int UnlistedCachePart(WorldObject o)
        {
            int act=UnlistedAct;
            if(act<0||act>=3||save.room!=unlistedRegions[act]||
                o==null||o.kind!="annexcache")return -1;
            string prefix="annex_cache_"+save.room+"_";
            return o.id==prefix+"a"?0:o.id==prefix+"b"?1:-1;
        }
        private bool UnlistedRidgeTarget(WorldObject o)
        {
            int act=UnlistedAct;
            return act>=0&&act<3&&
                save.room==unlistedRegions[act]&&
                o!=null&&o.kind=="ridgemarker"&&
                o.id=="ridge_route_"+save.room+"_"+unlistedRidgeSides[act]&&
                Has("unlisted_a_"+act)&&Has("unlisted_b_"+act);
        }
        private bool TalkUnlistedRidge(WorldObject o)
        {
            if(!UnlistedRidgeTarget(o)||!HasPhysicalInteractionReach(o))
                return false;
            int act=UnlistedAct;
            Dialog(o.name,o.en,
                unlistedTitlesZh[act]+"\n\n"+unlistedReasonZh[act]+
                "\n\n甲："+unlistedAzh[act]+"\n乙："+unlistedBzh[act]+
                "\n\n选择最能同时解释两份原始记录的结论。",
                unlistedTitlesEn[act]+"\n\n"+unlistedReasonEn[act]+
                "\n\nA: "+unlistedAen[act]+"\nB: "+unlistedBen[act]+
                "\n\nWhich explanation accounts for both physical records?",
                act==1?
                    C("很可能只是设备误报","Probably only a false alarm","unlisted_wrong"):
                    C("文件很可能损毁","Perhaps the papers were merely damaged","unlisted_wrong"),
                C(act==0?"居民被错记成货物":act==1?
                    "没有工牌的人被遗漏":"救援艇从名册中被抹去",
                    act==0?"Residents were misfiled as cargo":act==1?
                    "Unbadged workers were erased":"The rescue skiff was omitted",
                    "unlisted_reason"),
                C("再查看附近记录","Examine the records again","close"));
            return true;
        }
        private bool ChooseUnlisted(string action)
        {
            bool accepted=action=="unlisted_accept",clueA=action=="unlisted_clue_a",
                clueB=action=="unlisted_clue_b",reason=action=="unlisted_reason",
                wrong=action=="unlisted_wrong",publicEnd=action=="unlisted_public",
                privateEnd=action=="unlisted_protect";
            if(!accepted&&!clueA&&!clueB&&!reason&&!wrong&&!publicEnd&&!privateEnd)
                return false;
            WorldObject source=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||source==null||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            int act=UnlistedAct;
            if(accepted)
            {
                if(source.kind!="annexinnarchive"||save.room!=9||act!=-1)return true;
                Flag("unlisted_started");revision++;sound="quest";
                RecordMoment("旅店里一页没有编号的旧名册从书脊里滑出来。三地有人被写进货物、矿灯与船单，却没有姓名。要找回证据，先走到外渠的两处原始补给点。",
                    "An unnumbered ledger falls from the inn archive: citizens listed as cargo, mine lamps and boat rations but never by name. Begin at the two real canal caches.");
                Notify("《未列名者》已开启：外渠 → 赤铁矿场 → 雾港。走访现场，核对证据。",
                    "The Unlisted begins: Canal, Ironreach, Mistport. Investigate the original field records.");
                return true;
            }
            if(clueA||clueB)
            {
                int part=UnlistedCachePart(source);
                if(act<0||act>=3||part!=(clueA?0:1))return true;
                string flag=(part==0?"unlisted_a_":"unlisted_b_")+act;
                if(Has(flag))return true;
                Flag(flag);revision++;sound="quest";
                Notify(part==0?unlistedAzh[act]:unlistedBzh[act],
                    part==0?unlistedAen[act]:unlistedBen[act]);
                return true;
            }
            if(reason||wrong)
            {
                if(!UnlistedRidgeTarget(source)||act<0||act>=3)return true;
                if(wrong)
                {
                    // A falsifiable deduction has a small, bounded effort
                    // cost, but never blocks an exhausted hero from retrying.
                    save.energy=Math.Max(0f,save.energy-8f);
                    revision++;sound="ui";
                    Notify("这一解释不能同时说明两份原始记录。重新核对，耐力 -8。",
                        "That theory cannot explain both documents. Recheck the evidence. -8 energy.");
                    return true;
                }
                Flag("unlisted_solved_"+act);
                AddXp(45+act*15);CreditCoins(15+act*5);
                revision++;sound="reward";
                RecordMoment(
                    act==0?"外渠的名字不是被洪水冲走的，而是有人把住户当作货物抹去。":
                    act==1?"矿场里拉响撤离铃的人并非无名。他们只是没有得到允许被写进工牌册。":
                    "灯塔闪过的最后求救并没有被误看。救援艇的返航记录从来就不是真的。",
                    act==0?"The canal names were erased by treating residents as freight, not by the flood.":
                    act==1?"The mine bell's rescuers were people denied badges, not unnamed shadows.":
                    "The lighthouse distress call was real; the supposed return of the rescue skiff was not.");
                Notify(act==2?"三卷《未列名者》已核实。回炉火旅店决定公开方式。":
                    "现场结论已记录。下一卷的原始证据已开放。",
                    act==2?"All three books verified. Return to Hearth Inn to choose their fate.":
                    "Field deduction recorded. Continue with the next region's original evidence.");
                return true;
            }
            if(save.room!=9||source.kind!="annexinnarchive"||
                act!=3||UnlistedDone)return true;
            if(publicEnd)
            {
                Flag("unlisted_public");
                RecordMoment("旅店的印刷机连夜排出《未列名者》的公共版本。身份、危险路段与求援地点一起公示，每一座高地路标都能让同行者少走弯路。",
                    "The inn prints a public edition of The Unlisted. Hazard sites and rescue paths are shared along every ridge route.");
            }
            else
            {
                Flag("unlisted_protected");
                // The persistent bonus is recomputed from the existing
                // saved flag on every Load(), not an extra SaveData field.
                save.maxHealth=Math.Min(99999f,save.maxHealth+25f);
                save.health=Math.Min(save.maxHealth,save.health+25f);
                RecordMoment("旅店的记录员把姓名封进私人信封，只向家属交付副本。那些被追赶的人不必为获得援助，再向所有人解释自己是谁。",
                    "The keeper seals the names for their families. Survivors need not expose their identities to strangers to receive help.");
            }
            CreditCoins(55);CreditShards(2);CreditSkillPoints(1);
            revision++;sound="reward";
            Dialog("未列名者 · 最后一页","The Unlisted · Last Page",
                publicEnd?
                "一本原本没有名字的册子，终于有了能让所有人看见的页码。新制作的高地路标也因此更容易通行。"+
                "\n\n公共版奖励：高地穿行耐力消耗由12降低至6。硬币 +55、碎片 +2、技能点 +1。":
                "旧名册被重新缝好。只留下求援地点，不留下受害者姓名。夜归者可以保留隐私，也能得到治疗。"+
                "\n\n隐私版奖励：永久最大生命 +25，立即治疗 25。硬币 +55、碎片 +2、技能点 +1。",
                publicEnd?
                "The public edition makes every restored ridge crossing easier for fellow travelers."+
                "\n\nPublic reward: ridge travel now costs 6 instead of 12 energy; +55 coins, +2 shards, +1 talent.":
                "The sealed edition shares aid routes without exposing the families."+
                "\n\nPrivate reward: permanent +25 maximum HP and immediate +25 healing; +55 coins, +2 shards, +1 talent.",
                C("合上最后一页","Close the final page","close"));
            return true;
        }
    }
}
