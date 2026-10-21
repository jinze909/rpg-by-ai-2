using System;
namespace Starfall
{
    // Walkable physical third-ring landmarks. No new SaveData fields: survey
    // stages are permanent string flags, including on legacy migrated slots.
    public sealed partial class GameCore
    {
        private static readonly string[,] trailEvidenceZh = {
            {"旧渠刻度表明，城墙外的人曾自己修好一段灌溉支线，而堡垒的正式水图从来没有标出它。", "南侧漂来的木桨刻着三个孩子的名字；那晚送他们回家的不是巡逻队，而是无名的摆渡人。", "最后一枚铜钉把失散的水路重新接回镇外。莉雅要求以后把平民修的渠也写进官方星图。"},
            {"高台上的老根仍为躲避星雨的行人留着空洞。根系活着，不该只被当作堡垒的燃料。", "庇护名单用树叶压出手印，莫芽认得每一个暂住者，也记得哪些人在冬天没有回来。", "三处分散的树根终于标成可走的林路。森林不必再为城墙独自付账。"},
            {"断桥的水痕比运输账本高出两掌。货船会被登记，涉水逃生的人却常常不会。", "被压在泥中的船票写着没有许可证的一家人，他们后来从临时渡口平安上岸。", "两岸的旧铆钉终于对齐公共渡口。阿菱坚持雨季来时先摆渡人，再搬运货物。"},
            {"黄铜旧钟不是用来催人开工，而是矿道事故发生时要求所有人撤离的信号。", "停机令背面有工人的签字。维恩告诉你，签名比远方管理者的口头保证更值得留下。", "三条废轨接成避险路线，矿车不再挡住工人回家的路。"},
            {"旧号角曾在屏障停电那夜吹响，但外墙巡逻记录只把逃离者记为未授权通行。", "避难名单的边缘写着一位守卫偷偷留下的开门时刻。这不是叛逃，而是救人。", "外环步道在平民和曾经的守卫手中重新开放，下一次警报不必再等待谁批准。"},
            {"雾灯边的浮标记下失事前最后一个潮位，比港口账本早了整整一刻钟。", "潮汐桩号刻着米拉弟弟的船只经过的方向，那不是一条逃亡航线，而是救援孩子的路线。", "三个航标终于能相互照见；晚归者不必再依靠被涂改的名册寻找海岸。"},
            {"沙坡井边留有被擦去的取水标号。旧配水令把商队计入人口，却漏掉绿洲里生活的人。", "井图背面是伊沃重算的水量：即使保住屏障，也不该让孩子连一杯水都等不到。", "风沙下的三处水影组成新的公共补水路线，巡路者可以把水送到没有登记的人家。"},
            {"冰崖风铃的音色不同于军用钟，原来是守路者专为迷失在暴雪中的旅人留下的信号。", "背风石板用箭头指向最近能生火的人家；高原居民互相传递过这份未署名的指引。", "最后一声钟响连接起两处暖棚。钟声不再用来掩盖事故，而是让人知道还有人等他们回家。"}
        };
        private static readonly string[,] trailEvidenceEn = {
            {"A canal gauge shows that families outside the wall repaired their own irrigation branch. The official citadel charts never marked it.", "A waterlogged oar carries three children's names. An unnamed ferryman, not a military patrol, brought them home.", "The last copper rivet reconnects the lost civilian waterway. Lyra insists that community-built canals finally appear on public maps."},
            {"Old living roots still shelter travelers fleeing the Starfall. A forest is more than fuel for a distant shield.", "Handprints are pressed into the shelter roll. Moya remembers who stayed here, including those who never returned after winter.", "Three root paths become a genuine public trail. The forest no longer bears the wall's cost alone."},
            {"The ruined bridge shows floodwater higher than the freight manifests admit. Ships were counted; people wading to safety were not.", "A ticket pressed into the mud names a family without travel papers. The improvised public ferry saved them.", "Old rivets on both banks align. Arin insists the ferry carry people before cargo when the rains return."},
            {"This brass bell once ordered evacuation during mine accidents, not the start of another shift.", "Workers signed the shutdown order themselves. Venn trusts those signatures more than another distant promise.", "Three abandoned rail spurs now form an escape route. Mine carts will not block workers going home."},
            {"The outer-wall horn sounded during the blackout, but the patrol ledger called those escaping civilians trespassers.", "A guard secretly wrote down the time the shelter gates opened. It was rescue, not desertion.", "The outer circuit reopens for both residents and former guards. The next alarm need not wait for permission."},
            {"A buoy records the last tide before the wreck. It predates the official harbor log by fifteen minutes.", "The channel marker traces the boat of Mira's brother. He was rescuing children, not escaping.", "Three lamps can now see one another. Late travelers no longer depend on an altered manifest to find shore."},
            {"An erased ration mark sits beside a desert well. The old allocation counted merchants, but not the oasis families.", "Ivo recalculated the water share on the back of a well chart. No child's water should wait on the shield's needs.", "Three buried water traces become a public supply route for families who were never registered."},
            {"This wind chime differs from the military bells. Wardens rang it for travelers lost in blizzards.", "Arrows on a lee-side slab point toward the nearest hearth. The highland families passed these unsigned directions from hand to hand.", "The final chime connects two warming shelters. Bells now tell the lost that someone is waiting for them."}
        };
        public int TrailCircuitProgress(int room)
        {
            if(room<0||room>7)return 0;
            if(Has("trail_circuit_"+room+"_3"))return 3;
            if(Has("trail_circuit_"+room+"_2"))return 2;
            if(Has("trail_circuit_"+room+"_1"))return 1;
            return 0;
        }
        public bool TrailCircuitComplete(int room)
        { return TrailCircuitProgress(room)==3; }
        public int TrailCircuitCount
        {
            get { int total=0;for(int i=0;i<8;i++)if(TrailCircuitComplete(i))total++;return total; }
        }
        private bool TalkTrailCircuit(WorldObject o)
        {
            if(o.kind!="trailcircuit")return false;
            int room=save.room;
            if(room<0||room>7)return true;
            if(!HasPhysicalInteractionReach(o))
            {
                Notify("请走近路标，并确保途中没有墙体阻挡。",
                    "Move within reach of the route marker with a clear path.");
                return true;
            }
            int stage=0;
            for(int i=1;i<=3;i++)if(o.id=="trailcircuit_"+room+"_"+i)stage=i;
            if(stage==0)return true;
            int progress=TrailCircuitProgress(room);
            string[] routeZh={"渠外归乡路","林地共生路","沉桥渡口路","矿工安全路",
                "堡垒公共外环","雾港回航路","沙海补水路","霜钟归火路"};
            string[] routeEn={"Canal Homeward Route","Living Thornwood Trail","Open River Crossing",
                "Workers' Safe Line","Citadel Civilian Ring","Mistport Homebound Way",
                "Glasswind Water Line","Frostchime Hearth Trail"};
            if(progress>=stage)
            {
                Dialog(o.name,o.en,trailEvidenceZh[room,stage-1]+
                    "\n\n这里已经记下"+routeZh[room]+"的第 "+stage+" 个真实坐标，可以随时重读。",
                    trailEvidenceEn[room,stage-1]+
                    "\n\nBearing "+stage+" of "+routeEn[room]+" is filed and may be reread.",
                    C("继续行路","Continue","close"));
                return true;
            }
            if(stage!=progress+1)
            {
                Dialog(o.name,o.en,"路碑上的信息必须结合前面的记录才能辨认。先到上一处实地路标；已完成 "+progress+"/3。",
                    "This marker relies on the earlier field bearing. Find the previous physical stop first ("+progress+"/3).",
                    C("寻找上一处路标","Find the earlier bearing","close"));
                return true;
            }
            Flag("trail_circuit_"+room+"_"+stage);
            revision++;sound="reward";
            bool done=stage==3;
            if(done)
            {
                CreditCoins(24); CreditShards(2); AddXp(35);
                if(TrailCircuitCount==8&&!Has("trail_circuits_all"))
                { Flag("trail_circuits_all"); CreditSkillPoints(1); revision++; }
                RecordMoment("巡路队第 "+room+" 地的三处原始地标都经亲自抵达，开放区域的行路补给开始持续生效。",
                    "Three original roadwarden bearings were walked and verified for "+routeEn[room]+
                    "; the annex now supports sustained civilian stamina recovery.");
            }
            else AddXp(8);
            Dialog(o.name,o.en,trailEvidenceZh[room,stage-1]+
                "\n\n你亲自核对"+routeZh[room]+"的第 "+stage+"/3 个路标。"+
                (done?"\n\n完整线路确认：硬币 +24、碎片 +2、经验 +35。本区域的新探索地形额外恢复耐力 +2/秒。":
                "\n\n还有下一段路需要抵达。经验 +8。"),
                trailEvidenceEn[room,stage-1]+
                "\n\nPersonally surveyed bearing "+stage+"/3 of "+routeEn[room]+"."+
                (done?"\n\nFull circuit complete: +24 coins, +2 shards and +35 XP. New paths in this region now restore an extra 2 stamina per second.":
                "\n\nContinue along the route. +8 XP."),
                C("记下新坐标","Record the new bearing","close"));
            return true;
        }
    }
}
