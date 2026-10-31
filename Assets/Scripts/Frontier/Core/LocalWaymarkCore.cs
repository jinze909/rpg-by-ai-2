using System;

namespace Starfall
{
    // Read-only status for a prospective player pin. A reachable tile is
    // distinct from a merely standable tile on an isolated/closed island.
    public enum WaymarkRouteStatus
    {
        Available, InvalidRequest, HeroFootingBlocked, DestinationBlocked, NoWalkingRoute
    }

    // Player-placed local map destination. Deliberately transient: reading
    // a map must never create a saved quest, story outcome or teleport.
    public sealed partial class GameCore
    {
        private bool localWaymarkSet;
        private Point localWaymarkFoot;
        private int localWaymarkRoom = -1;
        private MapData localWaymarkMap;
        private bool localWaymarkBlocked;
        private float localWaymarkRouteRecheck;
        // Accepted distant job's optional, unsaved navigation handoff.
        // Never inherit it across a loaded save or override a manual pin.
        private string localWaymarkTravelQuestId;
        private bool localWaymarkTravelArrivalBlocked;
        public bool QuestApproachTravelHandoffPending
        { get { return !string.IsNullOrEmpty(localWaymarkTravelQuestId); } }
        public bool QuestApproachTravelArrivalBlocked
        { get { return QuestApproachTravelHandoffPending&&localWaymarkTravelArrivalBlocked; } }
        // A route through several physical regions needs to tell the player
        // the NEXT actual stop, not merely the final quest region. Derive
        // labels from the exact source object used by the real compass.
        public string RemoteQuestNextStop
        {
            get
            {
                if(!QuestApproachTravelHandoffPending)return "";
                if(QuestApproachTravelArrivalBlocked)
                    return L("本地区道路受阻","LOCAL APPROACH BLOCKED");
                QuestGuide quest=GetQuest(localWaymarkTravelQuestId);
                if(quest.locked||quest.complete)return "";
                if(quest.room==save.room)
                    return L("本地区接近任务点","LOCAL QUEST APPROACH");
                Point? exit=QuestRegionExitTarget(quest);
                if(!exit.HasValue)return L("尚无可用的地区通道","NO AVAILABLE CROSSING");
                foreach(WorldObject o in Map.objects)
                {
                    if((new Point(o.x,o.y)-exit.Value).Length>.001f)continue;
                    if(save.room==0&&o.kind=="envoy")
                        return L("下一步：与露恩交谈","NEXT: SPEAK WITH LUNE");
                    if(o.kind=="abyssEntry")
                        return L("下一步：进入深渊入口","NEXT: ABYSS ENTRANCE");
                    if(o.kind.StartsWith("portal",StringComparison.Ordinal))
                    {
                        int leg;
                        if(int.TryParse(o.kind.Substring(6),out leg)&&
                            leg>=0&&leg<data.maps.Length)
                            return L("下一站：","NEXT: ")+
                                L(data.maps[leg].name,data.maps[leg].en);
                    }
                }
                return L("接近当前任务通道","APPROACH QUEST CROSSING");
            }
        }
        // Zero BFS steps can mean we already stand beside the real portal.
        // Expose the precise physical-side test for the world HUD without
        // letting a closed wall masquerade as a completed route.
        // Only the authored outbound portal is entitled to strict
        // destination validation. A live timed defense, rescue or practice
        // target outranks travel and keeps its own routing semantics.
        public bool PlayerRequestedRemoteExitTarget(Point target)
        {
            if(!QuestApproachTravelHandoffPending||
                QuestApproachTravelArrivalBlocked)return false;
            QuestGuide current=GetQuest(localWaymarkTravelQuestId);
            Point? exit=QuestRegionExitTarget(current);
            return exit.HasValue&&(exit.Value-target).Length<.001f;
        }
        public bool PlayerRouteExitWithinInteractionReach(Point target)
        {
            return QuestApproachTravelHandoffPending&&
                CanStand(Position)&&(Position-target).Length<=1.8f&&
                ReliefActorLineWalkable(Position,target);
        }
        // Transient origin of a quest-generated waypoint. Manual pins must
        // NEVER be retargeted by story progress; neither value is serialized.
        private string localWaymarkQuestId;
        private Point localWaymarkQuestTarget;
        private bool localWaymarkQuestArrived;
        // A new quest stage may be physically inaccessible behind a sealed
        // gate. Retain the previous pin as a recoverable fallback but NEVER
        // present it as the active stage's interaction target.
        private bool localWaymarkQuestStageRoutePending;
        public bool QuestApproachNextStageBlocked
        { get { return QuestApproachNavigationActive &&
            localWaymarkQuestStageRoutePending; } }
        public bool QuestApproachNavigationActive
        { get { return LocalWaymark.HasValue &&
            !string.IsNullOrEmpty(localWaymarkQuestId); } }
        // Current quest identity and live contact geometry are part of an
        // arrival, not merely the old marker's cached proximity. Dynamic
        // gates, objective relocation and new stages can change between the
        // 1.25s full-BFS rechecks; never advertise a stale interaction.
        private bool CurrentQuestApproachContactValid()
        {
            if(!QuestApproachNavigationActive||
               localWaymarkQuestStageRoutePending||!CanStand(Position))
                return false;
            QuestGuide current=GetQuest(localWaymarkQuestId);
            return !current.locked&&!current.complete&&
                current.room==save.room&&
                (current.target-localWaymarkQuestTarget).Length<.001f&&
                (Position-current.target).Length<=1.8f&&
                ReliefActorLineWalkable(Position,current.target);
        }
        public bool QuestApproachAwaitingInteraction
        { get { return localWaymarkQuestArrived&&
            CurrentQuestApproachContactValid(); } }
        // Cheap read-only interaction-side probe for the actual map UI.
        // Full distant-route BFS remains cached in FrontierMapPreview;
        // a quest already in interaction reach should not be disabled
        // merely because zero walking steps are required.
        public bool QuestApproachWithinInteractionReach(string questId)
        {
            if(string.IsNullOrEmpty(questId)||
                Array.IndexOf(QuestIds,questId)<0)return false;
            QuestGuide q=GetQuest(questId);
            return !q.locked&&!q.complete&&q.room==save.room&&
                (Position-q.target).Length<=1.8f&&CanStand(Position)&&
                ReliefActorLineWalkable(Position,q.target);
        }
        public bool LocalWaymarkRouteBlocked
        { get { return LocalWaymark.HasValue && localWaymarkBlocked; } }
        public Point? LocalWaymark
        {
            get
            {
                return localWaymarkSet && localWaymarkRoom==save.room &&
                    Object.ReferenceEquals(localWaymarkMap,Map) ?
                    (Point?)localWaymarkFoot : null;
            }
        }
        public bool HasLocalWaymark { get { return LocalWaymark.HasValue; } }
        // A deliberately accepted distant quest route remains visible
        // even when general story guidance was turned off by the player.
        public bool PlayerRequestedNavigationActive
        { get { return PersonalNavigationActive||QuestApproachTravelHandoffPending; } }
        public bool PersonalNavigationActive
        {
            get
            {
                Point? mark=LocalWaymark;
                Point? target=mark.HasValue?NavigationTarget():null;
                if(mark.HasValue&&!string.IsNullOrEmpty(localWaymarkQuestId))
                {
                    QuestGuide current=GetQuest(localWaymarkQuestId);
                    // The next authored objective already changed, but
                    // the bounded BFS has not updated its old marker yet.
                    // Do not paint an obsolete route as active guidance.
                    if(current.locked||current.complete||
                       current.room!=save.room||
                       (current.target-localWaymarkQuestTarget).Length>.001f)
                        return false;
                }
                return mark.HasValue&&target.HasValue&&
                    (target.Value-mark.Value).Length<.001f;
            }
        }
        private void ResetLocalWaymark()
        {
            CancelTrailCircuitAutoFollow();
            ResetAtlasSurvey();
            localWaymarkSet=false;
            localWaymarkRoom=-1;
            localWaymarkMap=null;
            localWaymarkFoot=new Point();
            localWaymarkBlocked=false;
            localWaymarkRouteRecheck=0f;
            localWaymarkTravelQuestId=null;
            localWaymarkTravelArrivalBlocked=false;
            localWaymarkQuestId=null;
            localWaymarkQuestTarget=new Point();
            localWaymarkQuestArrived=false;
            localWaymarkQuestStageRoutePending=false;
        }
        public bool ClearLocalWaymark()
        {
            if(!localWaymarkSet && !QuestApproachTravelHandoffPending &&
               !AtlasSurveyHandoffPending)return false;
            ResetLocalWaymark();
            revision++;
            return true;
        }
        // Predict a pin using the EXACT walking rules that commit it. This
        // never changes a save, quest, notification, current pin or global
        // route: it is suitable for live map selection and accessibility UI.
        // If requested, route receives physical step centers plus the
        // destination itself, so a preview cannot stop short at a wall.
        public WaymarkRouteStatus InspectLocalWaymark(Point requested,
            System.Collections.Generic.List<Point> route=null)
        {
            if(route!=null)route.Clear();
            if(!Finite(requested.x)||!Finite(requested.y))
                return WaymarkRouteStatus.InvalidRequest;
            MapData map=Map;
            if(map==null||map.rows==null||map.rows.Length==0||
               map.rows[0]==null||requested.x<0||requested.y<0||
               requested.x>=map.rows[0].Length||requested.y>=map.rows.Length)
                return WaymarkRouteStatus.InvalidRequest;
            Point destination=new Point(
                (float)Math.Floor(requested.x)+.5f,
                (float)Math.Floor(requested.y)+.5f);
            if(!CanStand(Position))return WaymarkRouteStatus.HeroFootingBlocked;
            if(!CanStand(destination))return WaymarkRouteStatus.DestinationBlocked;
            bool adjacent=(Position-destination).Length<=1.2f &&
                ReliefActorLineWalkable(Position,destination);
            if(!adjacent)
            {
                var walking=NavigationPathFrom(Position,destination,true);
                if(walking.Count==0)return WaymarkRouteStatus.NoWalkingRoute;
                if(route!=null)route.AddRange(walking);
            }
            if(route!=null)route.Add(destination);
            return WaymarkRouteStatus.Available;
        }
        // Only the current opened local map accepts clicks. Both mouse and
        // keyboard/handpad must commit the identical inspected tile.
        // The two authored terrain crossings in each outdoor region are
        // actual walkable roads, not inferred quest markers. Keep their
        // coordinates alongside the collision validation so the world atlas
        // cannot advertise a removed/sealed/stale bridge as a shortcut.
        // No new MapData objects, quest flags or SaveData v2 fields.
        private static readonly int[,,] regionalShortcutRoads={
            {{60,46},{82,52}}, // Thornwood: middle ravine / east ridge gap
            {{60,46},{87,62}}, // Flooded Ruins: midspan / southern ferry bank
            {{61,46},{76,54}}, // Ironreach: quarry / low rail crossing
            {{64,46},{82,45}}, // Citadel: civilian bridge / east curtain
            {{63,46},{75,54}}, // Mistport: canal / outer pier link
            {{59,46},{25,54}}, // Glass Desert: gorge / west dune cut
            {{61,46},{88,62}}  // Frostchime: saddle / distant bell pass
        };
        public Point? RegionalShortcutFoot(int region,int index)
        {
            if(region<1||region>7||index<0||index>=2||
               data==null||data.maps==null||
               region>=data.maps.Length)return null;
            MapData target=data.maps[region];
            if(target==null||target.rows==null)return null;
            int x=regionalShortcutRoads[region-1,index,0];
            int y=regionalShortcutRoads[region-1,index,1];
            if(y<1||y+2>=target.rows.Length||
               target.rows[y-1]==null||
               target.rows[y]==null||target.rows[y+1]==null||
               target.rows[y+2]==null)return null;
            for(int yy=y;yy<=y+1;yy++)
                if(x<1||x+1>=target.rows[yy].Length||
                   target.rows[yy][x]!='='||
                   target.rows[yy][x+1]!='=')return null;
            // Only the current region may offer a walking pin. Terrain
            // glyphs alone do not prove a dynamic gate/tide can be crossed.
            Point foot=new Point(x+.5f,y+.5f);
            if(region==save.room&&!CanStand(foot))return null;
            return foot;
        }
        public bool FollowRegionalShortcut(int index)
        {
            if(mode!="map"||Dying||practicing||save.room<1||
               save.room>7||!CanStand(Position))return false;
            Point? foot=RegionalShortcutFoot(save.room,index);
            // The original PlaceLocalWaymark transaction preserves an
            // existing marker on any invalid or disconnected path.
            return foot.HasValue&&PlaceLocalWaymark(foot.Value);
        }

        public bool PlaceLocalWaymark(Point requested)
        {
            if(mode!="map"||Dying||practicing)return false;
            WaymarkRouteStatus status=InspectLocalWaymark(requested);
            if(status==WaymarkRouteStatus.InvalidRequest)return false;
            if(status==WaymarkRouteStatus.HeroFootingBlocked)
            {
                Notify("脚下道路已被封锁。请先回到可站立的位置再设标记。",
                    "Your footing is blocked. Reach safe ground before setting a pin.");
                return false;
            }
            if(status==WaymarkRouteStatus.DestinationBlocked)
            {
                Notify("此处不能站立。请在可通行的道路上设置标记。",
                    "That tile is blocked. Pin a walkable destination.");
                return false;
            }
            if(status==WaymarkRouteStatus.NoWalkingRoute)
            {
                Notify("这处地点当前无法步行抵达；请换一处可通行的位置。",
                    "No physical walking route to that pin. Choose a reachable tile.");
                return false;
            }
            return CommitLocalWaymark(requested,null);
        }
        // Shared commit point for both explicit map pins and the journal's
        // quest approach button. Neither caller can enter here before a
        // fresh physical-route check. This function never writes SaveData.
        private bool CommitLocalWaymark(Point requested,string questId)
        {
            // Any new owner explicitly cancels a previous continuous trail
            // guide. Its caller may reclaim ownership AFTER a valid commit.
            CancelTrailCircuitAutoFollow();
            // Any fresh manual or quest pin revokes the atlas's previous
            // on-site route. Atlas-origin callers take ownership explicitly
            // only after a successful commit.
            ResetAtlasSurvey();
            localWaymarkTravelQuestId=null;
            localWaymarkQuestId=questId;
            localWaymarkQuestTarget=string.IsNullOrEmpty(questId)?
                new Point():GetQuest(questId).target;
            localWaymarkQuestArrived=false;
            localWaymarkQuestStageRoutePending=false;
            localWaymarkFoot=new Point(
                (float)Math.Floor(requested.x)+.5f,
                (float)Math.Floor(requested.y)+.5f);
            localWaymarkRoom=save.room;
            localWaymarkMap=Map;
            localWaymarkSet=true;
            localWaymarkBlocked=false;
            localWaymarkRouteRecheck=1f;
            revision++;sound="ui";
            if(!string.IsNullOrEmpty(questId))
                Notify("已规划通往任务的步行接近点。返回世界后跟随个人导航，到达后与目标交互。",
                    "Quest walking approach pinned. Follow your personal route in the world, then interact with the objective.");
            else Notify("已设置本地标记。返回世界后，跟随地图上的路线前往。",
                "Local map pin set. Return to the world and follow its route.");
            return true;
        }
        // The journal may explicitly request a local quest's physically
        // walkable approach without temporarily spoofing map mode or
        // changing trackedQuest. An existing personal pin survives every
        // invalid, locked, unreachable or changed objective.
        // Resolve a physically verified interaction-side footing. A hero
        // already standing next to the objective has no BFS steps, but is
        // ALREADY at the valid interaction side rather than unreachable.
        private bool TryQuestApproachFoot(Point objective,out Point foot)
        {
            foot=new Point();
            var route=NavigationPathFrom(Position,objective,true);
            if(route.Count>0) foot=route[route.Count-1];
            else if((Position-objective).Length<=1.8f &&
                CanStand(Position)&&ReliefActorLineWalkable(Position,objective))
                foot=new Point((float)Math.Floor(Position.x)+.5f,
                    (float)Math.Floor(Position.y)+.5f);
            else return false;
            return InspectLocalWaymark(foot)==WaymarkRouteStatus.Available;
        }
        // The world atlas can explicitly accept and follow a currently
        // UNTRACKED remote quest in one deliberate action. This is not
        // triggered by browsing, cycling, or inspecting a map marker.
        // Validate everything BEFORE changing saved tracking or taking a
        // previous personal pin; a rejected request is non-destructive.

        // World-HUD follow is a deliberate player action, never an
        // automatic rewrite when the recommendation changes. Verify the
        // local route BEFORE replacing the saved tracked ID or manual pin.
        // Cross-region intent uses the same portal/arrival handoff as the
        // existing journal/map; no teleport or new save field is introduced.
        // An enemy-free pause between real fieldwork waves is not consent
        // to discard that mission in favor of a new journal destination.
        public bool CanFollowRecommendedAdventure
        {
            get
            {
                if((mode!="world"&&mode!="journal")||Dying||practicing||
                   FieldWorkPreventsRest||CivicCourierActive||WhiteoutActive||
                   ScoutDefenseActive||MainSagaRelayActive||
                   witnessDefenseRoom!=0||InAbyss||
                   !SafeTravel||!CanStand(Position))return false;
                QuestGuide q=RecommendedContinuationGuide;
                return q!=null&&!q.locked&&!q.complete&&
                    q.room>=0&&q.room<data.maps.Length&&CanEnter(q.room);
            }
        }
        // "Region unlocked" is not the same as "there is a usable
        // crossing FROM HERE". Check the next authentic door/courier
        // using a strict walking finish before an explicit follow action
        // destroys a manual pin or rewrites the tracked quest. A newly
        // flooded gate may reopen later; this is a request-time preflight,
        // not a persistent failed-quest flag or a teleport.
        private bool QuestRemoteExitWalkable(QuestGuide quest)
        {
            if(quest==null||quest.locked||quest.complete||
               quest.room==save.room||!CanStand(Position))return false;
            Point? exit=QuestRegionExitTarget(quest);
            if(!exit.HasValue)return false;
            if(HasPhysicalInteractionReach(exit.Value))return true;
            return NavigationPathFrom(Position,exit.Value,true).Count>0;
        }

        public bool FollowRecommendedAdventure()
        {
            if(!CanFollowRecommendedAdventure)return false;
            QuestGuide next=RecommendedContinuationGuide;
            if(next.room==save.room)
            {
                Point approach;
                if(!TryQuestApproachFoot(next.target,out approach))
                {
                    Notify("推荐目标暂时没有可走通的接近路线，原导航保留。",
                        "The next objective has no verified walking approach; your previous route is retained.");
                    return false;
                }
                TrackQuest(next.id);
                CommitLocalWaymark(approach,next.id);
                Notify("已追踪下一段冒险，并标出真实可走的任务接近路线。",
                    "Following the next adventure with a verified walking approach.");
                return true;
            }
            if(!QuestRemoteExitWalkable(next))
            {
                Notify("下一处真实地区入口暂时无法步行抵达。原有任务和手动导航已保留。",
                    "The next real crossing cannot be reached on foot. Your previous quest and pin are preserved.");
                return false;
            }
            TrackQuest(next.id);
            QueueTravelQuestApproach(next.id);
            Notify("已追踪下一段冒险。先到开放的地区通道，抵达后会验证当地步行路线。",
                "Following the next adventure. Travel through an open crossing; its walking approach will be checked on arrival.");
            return true;
        }
        public bool TrackAndFollowRemoteQuest(string questId)
        {
            if(mode!="map"||Dying||practicing||
                string.IsNullOrEmpty(questId)||
                Array.IndexOf(QuestIds,questId)<0)return false;
            QuestGuide candidate=GetQuest(questId);
            if(candidate.locked||candidate.complete||
                candidate.room==save.room||candidate.room<0||
                candidate.room>=data.maps.Length||
                !CanEnter(candidate.room))return false;
            // This combined TRACK+FOLLOW action must be atomic. Test the
            // real source-side crossing BEFORE TrackQuest mutates SaveData.
            if(!QuestRemoteExitWalkable(candidate))
            {
                Notify("任务所在地区虽已开放，但下一处路口目前无法步行抵达；原任务与地图标记不变。",
                    "The region is unlocked but its next crossing is unreachable. Current tracking and pin are unchanged.");
                return false;
            }
            TrackQuest(questId);
            // Route selection below uses the exact same authenticated
            // quest ID and physical arrival checks as journal routing.
            return PlaceQuestApproachWaymark(questId);
        }
        public bool PlaceQuestApproachWaymark(string questId)
        {
            if((mode!="map"&&mode!="journal")||Dying||practicing||
                string.IsNullOrEmpty(questId)||
                Array.IndexOf(QuestIds,questId)<0)return false;
            QuestGuide quest=GetQuest(questId);
            if(quest==null||quest.locked||quest.complete)return false;
            if(quest.room!=save.room)
            {
                // The journal OR the authenticated, inspected world map
                // may request a distant follow. Only a currently tracked
                // unlocked quest can take over the route, and the actual
                // button must be explicitly pressed: browsing never does.
                if(save.trackedQuest!=questId||
                    quest.room<0||quest.room>=data.maps.Length||
                    !CanEnter(quest.room))return false;
                if(!QuestRemoteExitWalkable(quest))
                {
                    Notify("下一处地区路口暂时不通。原有个人标记不会被清除。",
                        "The next regional crossing is blocked. Your existing map pin is preserved.");
                    return false;
                }
                QueueTravelQuestApproach(questId);
                Notify("已接续跨地区任务路线。先经过开放的路口，抵达目标地区后再核验步行接近点。",
                    "Remote quest route selected. Take an open crossing; a walkable approach will be checked on arrival.");
                return true;
            }
            Point approach;
            if(!TryQuestApproachFoot(quest.target,out approach))
            {
                Notify("没有通往此任务的完整步行接近路线。旧标记未被替换。",
                    "No verified walking approach to this quest. Your previous pin is retained.");
                return false;
            }
            return CommitLocalWaymark(approach,questId);
        }
        // Choosing distant work is not an immediate teleport or an invented
        // map pin. Retain only the player's explicit routing intent until a
        // real portal brings them into the target region.
        private void QueueTravelQuestApproach(string questId)
        {
            ClearLocalWaymark();
            localWaymarkTravelQuestId=questId;
            localWaymarkTravelArrivalBlocked=false;
            localWaymarkRouteRecheck=0f;
            revision++;
        }
        private void TryResumeTravelQuestApproach()
        {
            string id=localWaymarkTravelQuestId;
            if(string.IsNullOrEmpty(id))return;
            if(save.trackedQuest!=id||Array.IndexOf(QuestIds,id)<0)
            {
                localWaymarkTravelQuestId=null;
                localWaymarkTravelArrivalBlocked=false;
                revision++;
                return;
            }
            QuestGuide quest=GetQuest(id);
            if(quest.locked||quest.complete)
            {
                localWaymarkTravelQuestId=null;
                localWaymarkTravelArrivalBlocked=false;
                revision++;
                return;
            }
            if(quest.room!=save.room)
            {
                // If a dynamic quest stage relocates while we wait, resume
                // following that region without showing obsolete warnings.
                if(localWaymarkTravelArrivalBlocked)
                {
                    localWaymarkTravelArrivalBlocked=false;
                    revision++;
                }
                return;
            }
            Point foot;
            // Sealed arrivals must not advertise an invented walking route.
            // A single warning is enough; retry at the ordinary bounded rate.
            if(!TryQuestApproachFoot(quest.target,out foot))
            {
                if(!localWaymarkTravelArrivalBlocked)
                {
                    localWaymarkTravelArrivalBlocked=true;
                    revision++;
                    Notify("已抵达任务地区，但前往目标的路暂时不通。可等待道路开放或在地图中取消自动导航。",
                        "In the quest region, but the walking approach is blocked. Wait for the road or cancel auto-follow on the map.");
                }
                return;
            }
            CommitLocalWaymark(foot,id);
        }
        private void TickLocalWaymark(float dt)
        {
            Point? mark=LocalWaymark;
            if(!mark.HasValue && QuestApproachTravelHandoffPending)
            {
                localWaymarkRouteRecheck-=dt;
                if(localWaymarkRouteRecheck<=0f)
                {
                    localWaymarkRouteRecheck=1.25f;
                    TryResumeTravelQuestApproach();
                }
                return;
            }
            if(!mark.HasValue)
            {
                if(AtlasSurveyHandoffPending)TickPendingAtlasSurvey(dt);
                return;
            }
            bool fromQuest=!string.IsNullOrEmpty(localWaymarkQuestId);
            if(!localWaymarkQuestStageRoutePending &&
                (Position-mark.Value).Length<=.85f &&
               ReliefActorLineWalkable(Position,mark.Value)&&
               (!fromQuest||CurrentQuestApproachContactValid()))
            {
                if(!fromQuest)
                {
                    if(AtlasSurveyAtDestination())
                    {
                        if(!atlasSurveyArrived)
                        {
                            atlasSurveyArrived=true;
                            revision++;sound="quest";
                            Notify("已抵达星图上的旧路标，请按 E 实地拓印，完成后可继续下一条路。",
                                "At the atlas marker. Interact to survey it and follow the next road.");
                        }
                        // Do not erase the survey's source ownership simply
                        // because the hero reached the feet BEFORE pressing E.
                        return;
                    }
                    // A player-owned connected trail retains its real
                    // approach until the genuine source's E survey. The
                    // next leg cannot be awarded from mere proximity.
                    if(HoldTrailCircuitApproachForInteraction())return;
                    // A trail shortcut is a destination, not a new quest.
                    // Give the player an actionable north/south crossing
                    // instruction instead of a generic "pin reached" toast.
                    int passage=-1;
                    for(int site=0;site<2;site++)
                    {
                        Point? road=RegionalShortcutFoot(save.room,site);
                        if(road.HasValue&&
                           (road.Value-mark.Value).Length<.01f)
                        { passage=site;break; }
                    }
                    ResetLocalWaymark();
                    revision++;sound="quest";
                    if(passage>=0)
                        Notify(passage==0?
                            "中央近路已抵达。沿真实路面向另一侧穿过峡口，继续探索，不需要重新接任务。":
                            "外环通路已抵达。从这里可以徒步穿过远端地形障碍，继续探索另一条路。",
                            passage==0?
                            "Central shortcut reached. Cross the actual pass to the other bank; no quest re-acceptance required.":
                            "Outer shortcut reached. Follow the walkable pass through to the other side of the district.");
                    else
                        Notify("已到达个人标记位置。", "Personal map destination reached.");
                    return;
                }
                // A quest's approached marker is not the quest itself.
                // Keep it through interaction so completing this stage
                // can send the player to the NEXT actual objective.
                if(!localWaymarkQuestArrived)
                {
                    localWaymarkQuestArrived=true;
                    revision++;sound="quest";
                    Notify("已到任务接近点，请与目标交互；下一阶段会自动更新导航。",
                        "At the quest approach. Interact with the objective; navigation will follow the next stage.");
                }
            }
            else if(fromQuest) localWaymarkQuestArrived=false;
            // Local pins must survive closed tidal gates and temporary road
            // barriers. Recheck the complete physical route at a low fixed
            // rate instead of calling the 120x80 BFS every render/sim tick.
            localWaymarkRouteRecheck-=dt;
            if(localWaymarkRouteRecheck>0f)return;
            localWaymarkRouteRecheck=1.25f;
            if(fromQuest)
            {
                QuestGuide current=GetQuest(localWaymarkQuestId);
                if(current.locked||current.complete)
                {
                    ResetLocalWaymark();revision++;
                    Notify("任务已完成，旧任务导航已清除。",
                        "Quest complete; its old navigation route has been cleared.");
                    return;
                }
                if(current.room!=save.room)
                {
                    // A tracked quest can legitimately finish a stage here
                    // and continue in another open region. Carry the same
                    // explicit quest-follow ownership through real portals,
                    // then inspect a NEW physical approach on arrival.
                    // An independently pinned untracked quest never silently
                    // becomes the active cross-region compass.
                    if(save.trackedQuest==localWaymarkQuestId&&
                        current.room>=0&&current.room<data.maps.Length&&
                        CanEnter(current.room))
                    {
                        string continuingQuest=localWaymarkQuestId;
                        QueueTravelQuestApproach(continuingQuest);
                        Notify("当前阶段已经结束。沿开放的地区入口继续任务，抵达后将重新规划可行走路线。",
                            "This quest stage is complete. Take an open crossing; its walking approach will update on arrival.");
                    }
                    else
                    {
                        ResetLocalWaymark();revision++;
                        Notify("任务已转移到其他地区，旧的本地接近点已取消。",
                            "Quest moved to another region; old local approach cleared.");
                    }
                    return;
                }
                // Timed missions can return to a previous physical
                // objective after a failed leg/retry. If a pending new-stage
                // destination has moved back to the old coordinates, that
                // does not mean the road magically reopened: revalidate its
                // complete interaction-side physical approach before lifting
                // the blocked HUD and restoring live guidance.
                if(localWaymarkQuestStageRoutePending&&
                    (current.target-localWaymarkQuestTarget).Length<=.001f)
                {
                    Point restoredFoot;
                    if(!TryQuestApproachFoot(current.target,out restoredFoot))
                        return;
                    localWaymarkFoot=restoredFoot;
                    localWaymarkQuestArrived=false;
                    localWaymarkQuestStageRoutePending=false;
                    localWaymarkBlocked=false;
                    mark=LocalWaymark;
                    revision++;sound="ui";
                    Notify("任务重新回到可达目标，步行引导已经恢复。",
                        "The quest returned to a reachable objective. Walking guidance has resumed.");
                }
                if((current.target-localWaymarkQuestTarget).Length>.001f)
                {
                    // A successfully completed earlier interaction no
                    // longer applies to this next stage, even if its new
                    // approach is blocked and the fallback pin stays put.
                    localWaymarkQuestArrived=false;
                    Point newFoot;
                    if(TryQuestApproachFoot(current.target,out newFoot))
                    {
                        // An authoritative step transition must never use
                        // the stale cached pin's obstacle state. Revalidate
                        // before replacing it, just like a deliberate click.
                        localWaymarkFoot=newFoot;
                        localWaymarkQuestTarget=current.target;
                        localWaymarkQuestArrived=false;
                        localWaymarkQuestStageRoutePending=false;
                        localWaymarkBlocked=false;
                        revision++;sound="ui";
                        Notify("任务进入新阶段，个人导航已更新到下一个可达目标。",
                            "Quest advanced. Your personal route now follows the next reachable objective.");
                        mark=LocalWaymark;
                    }
                    else
                    {
                        // Do not navigate toward the previous quest step
                        // when the new objective is behind a closed gate.
                        if(!localWaymarkQuestStageRoutePending)
                        {
                            localWaymarkQuestStageRoutePending=true;
                            localWaymarkBlocked=true;revision++;
                            Notify("下一阶段道路暂未开放；旧导航仅作记录，不再指向当前任务目标。",
                                "The next stage is inaccessible. The old marker is retained for reference, not as the active quest destination.");
                        }
                        return;
                    }
                }
            }
            bool direct=(Position-mark.Value).Length<=1.8f &&
                ReliefActorLineWalkable(Position,mark.Value);
            bool reachable=CanStand(Position) && CanStand(mark.Value) &&
                (direct || NavigationPathFrom(Position,mark.Value,true).Count>0);
            if(reachable==!localWaymarkBlocked)return;
            localWaymarkBlocked=!reachable;
            revision++;
            if(localWaymarkBlocked)
                Notify("前往个人标记的道路暂时不通。标记已保留，等道路开放后会恢复引导。",
                    "The path to your pin is blocked. The pin stays until the road reopens.");
            else
                Notify("前往个人标记的道路恢复畅通。",
                    "The route to your personal pin is open again.");
        }
    }
}
