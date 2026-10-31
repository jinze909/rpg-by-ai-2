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
        // Single source of truth for both player-facing label and real cost.
        public int RidgePassageEnergyCost(int room)
        {
            int baseCost=Has("unlisted_public")?6:12;
            int region=room==0?0:room==3?1:room==5?2:-1;
            int relief=region>=0&&
                Has("unlisted_relief_trail_"+region)?4:0;
            // Two ACTUALLY cleared roadside ambushes are useful beyond
            // their one-time loot: each secured outer path saves 2 energy
            // on this region's existing physical ridge crossing. Old saves
            // with neither receipt retain the precise historical cost.
            // Maintain a minimum 2-energy walking cost even when several
            // unrelated civilian route improvements stack together.
            int saferRoads=room>=1&&room<=7?
                (RemoteRoadCleared(room,0)?2:0)+
                (RemoteRoadCleared(room,1)?2:0):0;
            return Math.Max(2,baseCost-relief-saferRoads);
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
            if(RemoteRoadEncounterActive&&
               RemoteRoadActiveDistrict==side)
            {
                // The original marker is also an authentic safe EXIT from
                // the OPTIONAL fight. No fast travel, no battle receipt.
                // The hero still has to physically return to the crossing
                // and interact before the next enemy attack lands.
                Dialog(o.name,o.en,
                    zh[room]+"\n\n"+
                        (RemoteRoadVeteranActive?
                            "自愿召集的进阶巡护尚余":"这处伏击尚余")+
                        RemoteRoadRemaining+
                        "名敌人。可继续迎战，或从原路口撤回。撤离不获得回执，居民通路安全不受影响。",
                    en[room]+"\n\n"+
                        (RemoteRoadVeteranActive?
                            "Veteran roadwatch: ":"Road ambush: ")+
                        RemoteRoadRemaining+
                        " raiders remain. Fight on or withdraw at this original marker. Civilian passage stays open.",
                    C("从本路口撤离 · 不领取奖励",
                        "Withdraw from road · no reward","remote_road_withdraw"),
                    C("继续交战","Keep fighting","close"));
                return true;
            }
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
                int passageCost=RidgePassageEnergyCost(room);
                Choice crossing=C("穿越巡路 · 耐力 "+passageCost,
                    "Cross ridge · "+passageCost+" energy","ridge_travel");
                Choice depart=C("继续步行","Keep walking","close");
                // Paid linked crossings stay safe, but a player may
                // deliberately challenge their destination's real raiders
                // instead of having combat sprung on them by teleport.
                // Ordinary walking near the marker keeps its live ambush.
                bool unclearedRoad=room>=1&&room<=7&&
                    !RemoteRoadCleared(room,side)&&
                    !RemoteRoadEncounterActive;
                bool veteranRoad=room>=1&&room<=7&&
                    RemoteRoadCleared(room,side)&&
                    !RemoteRoadEncounterActive;
                bool canChallenge=unclearedRoad||veteranRoad;
                Choice confront=unclearedRoad?
                    C("主动清剿本路口伏击",
                      "Challenge this road's raiders","remote_road_challenge"):
                    C(RemoteRoadVeteranDone(room,side)?
                        "进阶巡护复训 · 不重复发放奖励":
                        "进阶巡护 · 精锐双人队（首次回执）",
                      RemoteRoadVeteranDone(room,side)?
                        "Veteran rematch · no further rewards":
                        "Veteran roadwatch · two elite raiders",
                      "remote_road_veteran");
                Choice[] choices=Has("boss_"+room)&&!RidgeWatchComplete(room)?
                    canChallenge?
                    new Choice[]{crossing,confront,
                        C("追踪高地巡守","Track ridge patrols","ridge_watch_track"),depart}:
                    new Choice[]{crossing,C("追踪高地巡守","Track ridge patrols","ridge_watch_track"),depart}:
                    RidgeWatchComplete(room)?
                    canChallenge?
                    new Choice[]{crossing,confront,
                        C("巡守补给 · 月光草 ×1","Warden field aid · 1 Moonherb",
                            "ridge_field_aid"),depart}:
                    new Choice[]{crossing,C("巡守补给 · 月光草 ×1",
                        "Warden field aid · 1 Moonherb","ridge_field_aid"),depart}:
                    canChallenge?
                    new Choice[]{crossing,confront,depart}:
                    new Choice[]{crossing,depart};
                Dialog(o.name,o.en,zh[room]+RidgeWatchHint(room,false)+
                    "\n\n两座旧标记已经由你亲自到访。现在可通过安全的高处通道前往另一端，消耗 "+passageCost+" 耐力。"+
                    (unclearedRoad?"\n\n这处伏击仍可选：付费高地通行安全抵达；可在本路标主动挑战，或徒步返回迎战。":
                      veteranRoad?"\n\n这条居民路已经安全。你可以自愿召集更强的守路精锐实战；首次完成有奖励，之后复训无奖励，不会再自动伏击。":"")+
                    (first?"\n\n这处地标首次记录：经验 +9。线路连通：硬币 +20、碎片 +1、经验 +24。":""),
                    en[room]+RidgeWatchHint(room,true)+
                    "\n\nBoth markers have been visited on foot. Use the now-safe ridge connection to reach the opposite end for "+passageCost+" energy."+
                    (unclearedRoad?"\n\nThis roadside fight remains OPTIONAL: paid ridge transit lands safely. Challenge at this marker or return on foot to face its raiders.":
                      veteranRoad?"\n\nThis civilian crossing is secure. Voluntarily summon harder roadwatch opponents; the first win pays once, later rematches give no rewards. No automatic ambush returns.":"")+
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
                action!="ridge_field_aid"&&
                action!="remote_road_challenge"&&
                action!="remote_road_veteran"&&
                action!="remote_road_withdraw")return false;
            int room=save.room;
            if(action=="ridge_travel"&&SignalRelayActive)
            {
                Notify("烽火传讯必须亲自走完整条路，不能利用高地通道跳过距离。",
                    "Carry the signal overland; ridge transfers cannot skip a live dispatch.");
                return true;
            }
            if(action=="remote_road_withdraw")
            {
                WorldObject marker=pendingDialogueSource;
                if(marker==null||room<1||room>7||
                   pendingDialogueSourceRoom!=room||
                   marker.kind!="ridgemarker"||
                   !SourceBelongsToActiveMap(marker)||
                   !HasPhysicalInteractionReach(marker)||
                   !RemoteRoadEncounterActive||Dying)return true;
                int district=marker.id=="ridge_route_"+room+"_0"?0:
                    marker.id=="ridge_route_"+room+"_1"?1:-1;
                if(district<0||RemoteRoadActiveDistrict!=district)return true;
                ResetRemoteRoadEncounter(false,true);
                // No immediate auto-rematch while still standing at the
                // very marker used to withdraw. Exit the crossing and come
                // back, or explicitly challenge again after re-opening it.
                SetRemoteRoadSafeArrival(marker);
                revision++;sound="quest";
                Notify("你撤出了远路伏击，没有战斗奖励。离开路口后可返回再战。",
                    "Withdrew without reward. Leave and return, or challenge again.");
                return true;
            }
            if(action=="remote_road_challenge"||
               action=="remote_road_veteran")
            {
                bool veteran=action=="remote_road_veteran";
                WorldObject marker=pendingDialogueSource;
                if(marker==null||room<1||room>7||
                    pendingDialogueSourceRoom!=room||
                    marker.kind!="ridgemarker"||
                    !SourceBelongsToActiveMap(marker)||
                    !HasPhysicalInteractionReach(marker)||
                    !RidgeLinked(room)||RemoteRoadEncounterActive||
                    FieldWorkPreventsRest||ActionTransitionBlocked||
                    !SafeTravel||Dying||practicing)return true;
                int side=marker.id=="ridge_route_"+room+"_0"?0:
                    marker.id=="ridge_route_"+room+"_1"?1:-1;
                if(side<0||
                   (veteran&&!RemoteRoadCleared(room,side))||
                   (!veteran&&RemoteRoadCleared(room,side)))return true;
                if(BeginRemoteRoadEncounter(marker,side,veteran))
                {
                    remoteRoadVisited.Add(side);
                    // The voluntary choice replaces SAFE arrival with
                    // actual, committed combat at this physical crossing.
                    SetRemoteRoadSafeArrival(null);
                }
                else
                    Notify("目前路口地形拥堵，无法安全展开整队交战。稍后再试。",
                        "The crossing is blocked; a complete real squad cannot deploy yet.");
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
            if(FieldWorkPreventsRest||RemoteRoadEncounterActive||
               !SafeTravel)
            {
                Notify("战斗或现场行动期间无法离开巡路。请先完成或在原地点取消任务。",
                    "Ridge transfer is blocked by combat or live fieldwork. Finish or cancel the active mission first.");
                return true;
            }
            int cost=RidgePassageEnergyCost(room);
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
            // Paid safe transport is NOT a signed kill and NOT combat
            // consent. Walking away and approaching later, or choosing
            // the explicit challenge, still triggers the full real wave.
            SetRemoteRoadSafeArrival(end);
            revision++;sound="reward";
            Notify("安全穿越已经连通的偏远高地路。","Crossed the restored ridge way.");
            return true;
        }
    }
}
