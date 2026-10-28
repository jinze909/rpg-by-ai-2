using System;
namespace Starfall
{
    // Public highland wayfinding unlocked by first visiting both actual sites.
    // Old saves persist discoveries with preexisting v2 flag strings; neither
    // a cloned hero nor new coordinates in the original story are needed.
    public sealed partial class GameCore
    {
        public bool RidgeSeen(int room,int side)
        { return room>=0&&room<8&&side>=0&&side<=1&&Has("ridge_seen_"+room+"_"+side); }
        public bool RidgeLinked(int room)
        { return RidgeSeen(room,0)&&RidgeSeen(room,1); }
        public int RidgeLinkedCount
        {
            get { int n=0;for(int i=0;i<8;i++)if(RidgeLinked(i))n++;return n; }
        }
        private bool TalkRidge(WorldObject o)
        {
            if(o.kind!="ridgemarker")return false;
            int room=save.room;
            if(room<0||room>7)return true;
            if(!HasPhysicalInteractionReach(o))
            {
                Notify("请走近高地路标，并确保途中没有墙体阻挡。",
                    "Move within reach of the ridge marker with a clear path.");
                return true;
            }
            int side=o.id=="ridge_route_"+room+"_0"?0:
                o.id=="ridge_route_"+room+"_1"?1:-1;
            if(side<0)return true;
            bool first=!RidgeSeen(room,side);
            bool linkedBefore=RidgeLinked(room);
            if(first)
            {
                Flag("ridge_seen_"+room+"_"+side);AddXp(9);
                revision++;sound="reward";
            }
            if(!linkedBefore&&RidgeLinked(room))
            {
                CreditCoins(20);CreditShards(1);
                AddXp(24);revision++;sound="reward";
                RecordMoment("第五圈的南北两座旧标记终于被共同标记，居民可以安全地在两条偏远巡路间往返。",
                    "Both remote ridge markers were surveyed by foot, enabling a civilian route across the outer wilderness.");
                EnsureRidgeWatchPatrols();
                if(RidgeLinkedCount==8&&!Has("ridge_all_linked"))
                {
                    Flag("ridge_all_linked");CreditSkillPoints(1);revision++;
                }
            }
            string[] zh={
                "水位变化终于有了远离旧城墙的对照记录。",
                "树梢上的风铃能替误入荆棘的孩子指路。",
                "旧桥上游的水尺记录了那些未登记的民用渡船。",
                "废矿终点的铁轨被改作真正的疏散步道。",
                "外墙的应急路段由居民而不是军队维护。",
                "旧灯塔与岸边栈道终于能互相确认航向。",
                "两口无人登记的井现在连成可共用的取水路。",
                "高处的风铃指向背风暖棚，不再只报告警戒。"
            };
            string[] en={
                "Flood gauges now have a civilian reference beyond the old walls.",
                "A canopy chime guides children away from the thornwood.",
                "An upstream gauge remembers ferries absent from official manifests.",
                "Abandoned mine rails have become an evacuation footpath.",
                "Residents maintain these emergency steps, not the military.",
                "The old lighthouse and coastal walk can finally share bearings.",
                "Two previously unregistered wells now share one public path.",
                "A high chime guides travelers toward warmth instead of another alarm."
            };
            if(IsSignalRelayDestination(o))
            {
                // Keep the ordinary one-time ridge discovery above. The
                // active packet's real distant marker temporarily owns a
                // distinct handoff choice, never a near-camp fake lookout.
                Dialog(o.name,o.en,
                    zh[room]+"\n\n来自营地的守火信号必须在这里现场交接。返回原营地交回回执后，这条传讯线才能算完成。",
                    en[room]+"\n\nHand over the camp's carried flame at this distant ridge marker. Its acknowledgement must still be carried back to the starting camp.",
                    C("交接烽火信号","Deliver carried signal","annex_relay_deliver"),
                    C("继续沿路巡查","Keep surveying","close"));
                return true;
            }
            if(TalkUnlistedRidge(o))return true;
            if(RidgeLinked(room))
            {
                // Keep ordinary crossing first. An unlocked, uncleared
                // highland also lets the player track the living patrol
                // directly from this real marker without searching the log.
                Choice crossing=C(Has("unlisted_public")?
                    "穿越公共巡路 · 耐力 6":"穿越巡路 · 耐力 12",
                    Has("unlisted_public")?"Cross public ridge · 6 energy":
                        "Cross route · 12 energy","ridge_travel");
                Choice depart=C("继续步行","Keep walking","close");
                Choice[] choices=Has("boss_"+room)&&!RidgeWatchComplete(room)?
                    new Choice[]{crossing,C("追踪高地巡守","Track ridge patrols","ridge_watch_track"),depart}:
                    RidgeWatchComplete(room)?
                    new Choice[]{crossing,C("巡守补给 · 月光草 ×1",
                        "Warden field aid · 1 Moonherb","ridge_field_aid"),depart}:
                    new Choice[]{crossing,depart};
                Dialog(o.name,o.en,zh[room]+RidgeWatchHint(room,false)+
                    "\n\n两座旧标记已经由你亲自到访。现在可通过安全的高处通道前往另一端，消耗 "+(Has("unlisted_public")?6:12)+" 耐力。"+
                    (first?"\n\n这处地标首次记录：经验 +9。线路连通：硬币 +20、碎片 +1、经验 +24。":""),
                    en[room]+RidgeWatchHint(room,true)+
                    "\n\nBoth markers have been visited on foot. Use the now-safe ridge connection to reach the opposite end for "+(Has("unlisted_public")?6:12)+" energy."+
                    (first?"\n\nFirst visit: +9 XP. Route linked: +20 coins, +1 shard, +24 XP.":""),
                    choices);
                return true;
            }
            Dialog(o.name,o.en,zh[room]+
                "\n\n旧地标实地记录："+(RidgeSeen(room,0)?1:0)+"/1 北侧，"+
                (RidgeSeen(room,1)?1:0)+"/1 南侧。找到另一端后，才能恢复两处之间的安全巡路。"+
                (first?"\n\n首次到访：经验 +9。":""),
                en[room]+
                "\n\nRidge survey: "+(RidgeSeen(room,0)?1:0)+"/1 east, "+
                (RidgeSeen(room,1)?1:0)+"/1 south. Discover the other endpoint before unlocking safe travel."+
                (first?"\n\nFirst arrival: +9 XP.":""),
                C("记录地标","Record the landmark","close"));
            return true;
        }
        private bool ChooseRidge(string action)
        {
            if(action!="ridge_travel"&&action!="ridge_watch_track"&&
                action!="ridge_field_aid")return false;
            int room=save.room;
            if(action=="ridge_travel"&&SignalRelayActive)
            {
                Notify("烽火传讯必须亲自走完整条路，不能利用高地通道跳过距离。",
                    "Carry the signal overland; ridge transfers cannot skip a live dispatch.");
                return true;
            }
            if(action=="ridge_watch_track"||action=="ridge_field_aid")
            {
                // Never let an unrelated NPC, remote/stale page or a
                // synthesized choice change the tracked story objective.
                WorldObject speaker=pendingDialogueSource;
                if(speaker==null||pendingDialogueSourceRoom!=room||
                    speaker.kind!="ridgemarker"||
                    !SourceBelongsToActiveMap(speaker)||
                    !HasPhysicalInteractionReach(speaker)||
                    !RidgeLinked(room)||!Has("boss_"+room)||
                    Dying||practicing)return true;
                if(action=="ridge_watch_track")
                {
                    if(RidgeWatchComplete(room))return true;
                    TrackQuest("ridge_watch");
                    Notify("巡守任务已追踪。导航将指向仍然活动的高地敌人。",
                        "Ridge watch tracked. Navigation now follows living patrol enemies.");
                    return true;
                }
                // Securing the ridge opens a real, repeatable field service.
                // The health/energy transaction is guarded by the ORIGINAL
                // spoken marker, safe civilian fieldwork and actual materials.
                if(!RidgeWatchComplete(room))return true;
                if(FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("现场任务或敌人尚未解除，暂时无法安全领取巡守补给。",
                        "Active fieldwork or nearby hostiles prevent safe ridge aid.");
                    return true;
                }
                if(save.health>=save.maxHealth&&save.energy>=100f)
                {
                    Notify("生命和耐力已满，保留月光草下次使用。",
                        "Health and energy are full; keep the Moonherb for later.");
                    return true;
                }
                if(!Spend(0,0,1,0,0))return true;
                float oldHealth=save.health;
                save.health=Math.Min(save.maxHealth,save.health+30f);
                save.energy=Math.Min(100f,save.energy+30f);
                effects.Add(new Effect("number",Position,.75f,
                    L("巡守补给 +"+(int)(save.health-oldHealth)+" 生命",
                      "WARDEN AID +"+(int)(save.health-oldHealth)+" HP")));
                sound="reward";
                Notify("巡守草药补给：回复最多 30 生命与 30 耐力。",
                    "Warden aid restores up to 30 health and 30 energy.");
                return true;
            }
            if(room<0||room>7||!RidgeLinked(room)||Dying||practicing)return true;
            // Depart from the exact marker whose dialogue the player
            // accepted. Searching for any reachable marker could pick a
            // different source (or activate a route with no dialog at all).
            WorldObject start=pendingDialogueSource,end=null;
            if(start==null||start.kind!="ridgemarker"||
                pendingDialogueSourceRoom!=room||
                !SourceBelongsToActiveMap(start)||
                !HasPhysicalInteractionReach(start)||
                (start.id!="ridge_route_"+room+"_0"&&
                 start.id!="ridge_route_"+room+"_1"))return true;
            string targetId=start.id=="ridge_route_"+room+"_0"?
                "ridge_route_"+room+"_1":"ridge_route_"+room+"_0";
            foreach(WorldObject o in Map.objects)if(o.id==targetId){end=o;break;}
            if(end==null)return true;
            // Remote ridge travel is a physical transport just like the
            // rebuilt public ferry. It must never skip a live convoy clock,
            // rescue follower, duel wave or chapter calibration.
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("战斗或现场行动期间无法离开巡路。请先完成或在原地点取消任务。",
                    "Ridge transfer is blocked by combat or live fieldwork. Finish or cancel the active mission first.");
                return true;
            }
            int cost=Has("unlisted_public")?6:12;
            if(save.energy<cost)
            {
                Notify("需要 "+cost+" 点耐力才能走完这条高地通道。",
                    "This ridge crossing requires "+cost+" energy.");
                return true;
            }
            Point destination=new Point(end.x,end.y);
            // The same world-space safety checks prevent the player from
            // materializing inside an active projectile or warned explosion,
            // including encounter-owned hazards invisible at the departure.
            if(!SafeRegionalLanding(destination))
            {
                Notify("巡路另一端有敌人、飞行攻击或尚未释放的危险；请等待通道安全。",
                    "Enemies, live projectiles or warned impacts block the far end of the ridge crossing.");
                return true;
            }
            save.x=end.x;save.y=end.y;save.energy-=cost;
            revision++;sound="reward";
            Notify("安全穿越已经连通的偏远高地路。","Crossed the restored ridge way.");
            return true;
        }
    }
}
