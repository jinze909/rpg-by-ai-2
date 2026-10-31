using System;
namespace Starfall
{
    // Walkable physical third-ring landmarks. No new SaveData fields: survey
    // stages are permanent string flags, including on legacy migrated slots.
    public sealed partial class GameCore
    {
        // Explicit, temporary ownership of a connected three-marker
        // walking expedition. No new SaveData field or automatic objective
        // receipt: only original source-bound E interactions advance it.
        private WorldObject trailFollowSource;
        private MapData trailFollowMap;
        private int trailFollowRoom=-1;
        private Point trailFollowFoot;
        private bool trailFollowArrivalNotified;

        public bool TrailCircuitAutoFollowActive
        { get { return TrailFollowOwnsCurrentPin(); } }

        private void CancelTrailCircuitAutoFollow()
        {
            trailFollowSource=null;trailFollowMap=null;
            trailFollowRoom=-1;trailFollowFoot=new Point();
            trailFollowArrivalNotified=false;
        }

        private bool TrailFollowOwnsCurrentPin()
        {
            Point? pin=LocalWaymark;
            return trailFollowSource!=null&&trailFollowRoom==save.room&&
                System.Object.ReferenceEquals(trailFollowMap,Map)&&
                SourceBelongsToActiveMap(trailFollowSource)&&
                IsObjectPresent(trailFollowSource)&&
                trailFollowSource.kind=="trailcircuit"&&
                trailFollowSource.id=="trailcircuit_"+save.room+"_"+
                    (TrailCircuitProgress(save.room)+1)&&
                pin.HasValue&&
                (pin.Value-trailFollowFoot).Length<.001f;
        }

        private void OwnTrailCircuitPin(WorldObject source,Point contact)
        {
            trailFollowSource=source;
            trailFollowMap=Map;trailFollowRoom=save.room;
            trailFollowFoot=new Point(
                (float)Math.Floor(contact.x)+.5f,
                (float)Math.Floor(contact.y)+.5f);
            trailFollowArrivalNotified=false;
        }

        public bool TrailCircuitIsCurrentFollowTarget(WorldObject source)
        {
            return TrailFollowOwnsCurrentPin()&&
                System.Object.ReferenceEquals(trailFollowSource,source);
        }

        private WorldObject ArrivedTrailInteractionSource()
        {
            // Deliberate E guidance may disambiguate a crowded roadside,
            // but NEVER override the player's own O/click focus or a
            // higher-priority live quest approach. The source remains the
            // actual physical map member and must be within genuine reach.
            return mode=="world"&&!Dying&&SafeTravel&&
                TrailCircuitAtFollowPin()&&
                HasPhysicalInteractionReach(trailFollowSource)?
                trailFollowSource:null;
        }

        private bool TrailCircuitAtFollowPin()
        {
            return TrailFollowOwnsCurrentPin()&&
                (Position-trailFollowFoot).Length<=.85f&&
                ReliefActorLineWalkable(Position,trailFollowFoot);
        }

        private bool HoldTrailCircuitApproachForInteraction()
        {
            if(!TrailCircuitAtFollowPin())return false;
            if(!trailFollowArrivalNotified)
            {
                trailFollowArrivalNotified=true;revision++;sound="quest";
                Notify("已抵达下一处真实路碑，请按 E 核对。核对后将自动规划下一段。",
                    "At the real trail bearing. Press E to survey; your next leg will be planned automatically.");
            }
            return true;
        }

        private void AdvanceOwnedTrailCircuit(WorldObject reached,bool complete)
        {
            // TalkTrailCircuit authenticated ownership BEFORE recording
            // this stage. After Flag(), progress already points at the NEXT
            // source; repeating the old-stage test here would reject every
            // legitimate automatic handoff.
            Point? committed=LocalWaymark;
            if(!System.Object.ReferenceEquals(trailFollowSource,reached)||
                trailFollowRoom!=save.room||
                !System.Object.ReferenceEquals(trailFollowMap,Map)||
                !committed.HasValue||
                (committed.Value-trailFollowFoot).Length>.001f)
                return;
            if(complete)
            {
                ResetLocalWaymark();revision++;
                Notify("三处真实路碑都已核对，连续巡路导航已结束。",
                    "All three real bearings surveyed. Continuous trail guidance complete.");
                return;
            }
            WorldObject next=NextTrailCircuitMarker(save.room);
            Point contact;
            if(next==null||!SourceBelongsToActiveMap(next)||
                !IsObjectPresent(next)||
                !TryQuestApproachFoot(new Point(next.x,next.y),out contact))
            {
                // The previous pin is now a completed step, not a route
                // into a locked tide/gate. Retire ONLY our own obsolete pin.
                ResetLocalWaymark();revision++;
                Notify("下一段真实路线暂时不可达，自动引导已停止；任务记录仍保留。",
                    "The next physical bearing is blocked. Auto-follow stopped; survey receipts remain.");
                return;
            }
            CommitLocalWaymark(contact,null);
            OwnTrailCircuitPin(next,contact);
            Notify("路碑已核对：下一段真实可步行路线已自动规划。",
                "Bearing recorded. A verified walking route to the next stop is ready.");
        }

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
        // Resolve the NEXT authored stop by its actual living world source,
        // rather than hardcoding a hint that points to stale scenery.
        // The same reader serves dialogue, atlas and explicit navigation.
        // Existing v1/v2 trail flags remain the sole authoritative progress.
        public WorldObject NextTrailCircuitMarker(int room)
        {
            if(room<0||room>7||data==null||data.maps==null||
               room>=data.maps.Length)return null;
            MapData map=room==save.room?Map:data.maps[room];
            if(map==null||map.objects==null||
               TrailCircuitComplete(room))return null;
            string id="trailcircuit_"+room+"_"+
                (TrailCircuitProgress(room)+1);
            foreach(WorldObject o in map.objects)
                if(o!=null&&o.kind=="trailcircuit"&&o.id==id&&
                   Finite(o.x)&&Finite(o.y))return o;
            return null;
        }
        // Called only by an explicit atlas button; inspecting a marker or
        // reading an account never takes the player's earlier personal pin.
        // Reuse the tested transactional map-pin and real E-approach solver:
        // a flood, quest gate or malformed source may reject this request.
        public bool FollowNextTrailCircuitMarker()
        {
            if(mode!="map"||Dying||practicing||
               save.room<0||save.room>7||!CanStand(Position))
                return false;
            WorldObject next=NextTrailCircuitMarker(save.room);
            if(next==null||!SourceBelongsToActiveMap(next)||
               !IsObjectPresent(next))return false;
            Point contact;
            if(!TryQuestApproachFoot(new Point(next.x,next.y),
                out contact))
            {
                Notify("下一处巡路碑目前没有可验证的步行接近路线，保留原导航。",
                    "No verified walk to the next trail bearing. Your existing pin is preserved.");
                return false;
            }
            if(!PlaceLocalWaymark(contact))return false;
            OwnTrailCircuitPin(next,contact);
            return true;
        }
        // State-dependent, bilingual directions use authentic map sources.
        // Telling a player only to "find the previous marker" makes three
        // mandatory-ordered visits feel like missing quest instructions.
        private string TrailCircuitNextBearingHint(int room,bool english)
        {
            WorldObject next=NextTrailCircuitMarker(room);
            if(next==null)return "";
            string coordinates=next.x.ToString("0.0",
                System.Globalization.CultureInfo.InvariantCulture)+","+
                next.y.ToString("0.0",
                System.Globalization.CultureInfo.InvariantCulture);
            return english?
                "\n\nNext real road marker: "+(TrailCircuitProgress(room)+1)+
                "/3 at ("+coordinates+"). Choose a verified walking route here, or pin it from the local map.":
                "\n\n下一处真实路碑："+(TrailCircuitProgress(room)+1)+
                "/3，坐标（"+coordinates+"）。可在此选择真实步行引导，也可从本地区地图设定路线。";
        }
        public int TrailCircuitCount
        {
            get { int total=0;for(int i=0;i<8;i++)if(TrailCircuitComplete(i))total++;return total; }
        }
        // Authentic on-site bearings, not relocated quest props, own the
        // optional route. Reading lore never steals an existing personal pin.
        private Choice[] TrailCircuitChoices(int room,string closeZh,string closeEn)
        {
            Choice leave=C(closeZh,closeEn,"close");
            if(NextTrailCircuitMarker(room)==null)return new Choice[]{leave};
            return new Choice[]{
                C("连续引导后续路碑 · 可随时取消",
                  "Guide remaining real markers automatically · cancel anytime","trail_circuit_follow"),
                leave
            };
        }
        private bool ChooseTrailCircuit(string action)
        {
            if(action!="trail_circuit_follow")return false;
            // Choose() captures the source BEFORE closing its dialogue.
            // Recheck that same physical actor, map and reach before changing
            // navigation. Never trust a stale, removed or mismatched speaker.
            int room=save.room;
            WorldObject source=pendingDialogueSource;
            if(mode!="world"||Dying||practicing||
               room<0||room>7||source==null||
               pendingDialogueSourceRoom!=room||
               source.kind!="trailcircuit"||
               !SourceBelongsToActiveMap(source)||
               !IsObjectPresent(source)||
               !HasPhysicalInteractionReach(source)||
               !CanStand(Position))return true;
            bool authentic=false;
            for(int stage=1;stage<=3;stage++)
                if(source.id=="trailcircuit_"+room+"_"+stage)
                {authentic=true;break;}
            if(!authentic)return true;
            WorldObject next=NextTrailCircuitMarker(room);
            if(next==null||!SourceBelongsToActiveMap(next)||
               !IsObjectPresent(next))return true;
            Point contact;
            if(!TryQuestApproachFoot(new Point(next.x,next.y),
                out contact))
            {
                Notify("下一处路碑没有可验证的步行路线，原个人导航保留。",
                    "The next marker has no verified walking approach. Your previous pin stays.");
                return true;
            }
            CommitLocalWaymark(contact,null);
            OwnTrailCircuitPin(next,contact);
            Notify("已开启连续实地巡路引导。抵达下一处真实路碑后按 E 核对，将自动规划下一段。",
                "Continuous walking guide started. Press E at each real bearing; the next leg updates automatically.");
            return true;
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
            // Ownership is checked BEFORE the source's real survey receipt
            // changes the next-stage identity. No silent route replacement
            // is allowed for a manual pin, different source or stale map.
            bool autoAdvance=TrailFollowOwnsCurrentPin()&&
                System.Object.ReferenceEquals(trailFollowSource,o);
            string[] routeZh={"渠外归乡路","林地共生路","沉桥渡口路","矿工安全路",
                "堡垒公共外环","雾港回航路","沙海补水路","霜钟归火路"};
            string[] routeEn={"Canal Homeward Route","Living Thornwood Trail","Open River Crossing",
                "Workers' Safe Line","Citadel Civilian Ring","Mistport Homebound Way",
                "Glasswind Water Line","Frostchime Hearth Trail"};
            if(progress>=stage)
            {
                Dialog(o.name,o.en,trailEvidenceZh[room,stage-1]+
                    "\n\n这里已经记下"+routeZh[room]+"的第 "+stage+" 个真实坐标，可以随时重读。"+
                    TrailCircuitNextBearingHint(room,false),
                    trailEvidenceEn[room,stage-1]+
                    "\n\nBearing "+stage+" of "+routeEn[room]+
                    " is filed and may be reread."+
                    TrailCircuitNextBearingHint(room,true),
                    TrailCircuitChoices(room,"继续行路","Continue"));
                return true;
            }
            if(stage!=progress+1)
            {
                Dialog(o.name,o.en,"路碑上的信息必须结合前面的记录才能辨认。必须依次记录；已完成 "+progress+"/3。"+
                    TrailCircuitNextBearingHint(room,false),
                    "This marker relies on earlier field bearings. Visit the stops in order ("+progress+"/3)."+
                    TrailCircuitNextBearingHint(room,true),
                    TrailCircuitChoices(room,"寻找上一处路标","Find the earlier bearing"));
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
            if(autoAdvance)AdvanceOwnedTrailCircuit(o,done);
            Dialog(o.name,o.en,trailEvidenceZh[room,stage-1]+
                "\n\n你亲自核对"+routeZh[room]+"的第 "+stage+"/3 个路标。"+
                (done?"\n\n完整线路确认：硬币 +24、碎片 +2、经验 +35。本区域的新探索地形额外恢复耐力 +2/秒。":
                "\n\n还有下一段路需要抵达。经验 +8。"+
                    TrailCircuitNextBearingHint(room,false)),
                trailEvidenceEn[room,stage-1]+
                "\n\nPersonally surveyed bearing "+stage+"/3 of "+routeEn[room]+"."+
                (done?"\n\nFull circuit complete: +24 coins, +2 shards and +35 XP. New paths in this region now restore an extra 2 stamina per second.":
                "\n\nContinue along the route. +8 XP."+
                    TrailCircuitNextBearingHint(room,true)),
                TrailCircuitChoices(room,"记下新坐标","Record the new bearing"));
            return true;
        }
    }
}
