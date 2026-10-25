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
        // Transient origin of a quest-generated waypoint. Manual pins must
        // NEVER be retargeted by story progress; neither value is serialized.
        private string localWaymarkQuestId;
        private Point localWaymarkQuestTarget;
        private bool localWaymarkQuestArrived;
        public bool QuestApproachNavigationActive
        { get { return LocalWaymark.HasValue &&
            !string.IsNullOrEmpty(localWaymarkQuestId); } }
        public bool QuestApproachAwaitingInteraction
        { get { return QuestApproachNavigationActive && localWaymarkQuestArrived; } }
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
        public bool PersonalNavigationActive
        {
            get
            {
                Point? mark=LocalWaymark;
                Point? target=mark.HasValue?NavigationTarget():null;
                return mark.HasValue&&target.HasValue&&
                    (target.Value-mark.Value).Length<.001f;
            }
        }
        private void ResetLocalWaymark()
        {
            localWaymarkSet=false;
            localWaymarkRoom=-1;
            localWaymarkMap=null;
            localWaymarkFoot=new Point();
            localWaymarkBlocked=false;
            localWaymarkRouteRecheck=0f;
            localWaymarkQuestId=null;
            localWaymarkQuestTarget=new Point();
            localWaymarkQuestArrived=false;
        }
        public bool ClearLocalWaymark()
        {
            if(!localWaymarkSet)return false;
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
            localWaymarkQuestId=questId;
            localWaymarkQuestTarget=string.IsNullOrEmpty(questId)?
                new Point():GetQuest(questId).target;
            localWaymarkQuestArrived=false;
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
        public bool PlaceQuestApproachWaymark(string questId)
        {
            if((mode!="map"&&mode!="journal")||Dying||practicing||
                string.IsNullOrEmpty(questId)||
                Array.IndexOf(QuestIds,questId)<0)return false;
            QuestGuide quest=GetQuest(questId);
            if(quest==null||quest.locked||quest.complete||
                quest.room!=save.room)return false;
            Point approach;
            if(!TryQuestApproachFoot(quest.target,out approach))
            {
                Notify("没有通往此任务的完整步行接近路线。旧标记未被替换。",
                    "No verified walking approach to this quest. Your previous pin is retained.");
                return false;
            }
            return CommitLocalWaymark(approach,questId);
        }
        private void TickLocalWaymark(float dt)
        {
            Point? mark=LocalWaymark;
            if(!mark.HasValue)return;
            bool fromQuest=!string.IsNullOrEmpty(localWaymarkQuestId);
            if((Position-mark.Value).Length<=.85f &&
               ReliefActorLineWalkable(Position,mark.Value))
            {
                if(!fromQuest)
                {
                    ResetLocalWaymark();
                    revision++;sound="quest";
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
                if(current.locked||current.complete||current.room!=save.room)
                {
                    ResetLocalWaymark();revision++;
                    Notify("任务已完成或转移至其他地区，已清除旧任务导航。",
                        "Quest completed or moved to another region; old quest navigation cleared.");
                    return;
                }
                if((current.target-localWaymarkQuestTarget).Length>.001f)
                {
                    Point newFoot;
                    if(TryQuestApproachFoot(current.target,out newFoot))
                    {
                        // An authoritative step transition must never use
                        // the stale cached pin's obstacle state. Revalidate
                        // before replacing it, just like a deliberate click.
                        localWaymarkFoot=newFoot;
                        localWaymarkQuestTarget=current.target;
                        localWaymarkQuestArrived=false;
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
                        if(!localWaymarkBlocked)
                        {
                            localWaymarkBlocked=true;revision++;
                            Notify("任务目标已更新，但新路线暂时不通。原有标记已保留。",
                                "Quest objective changed, but its route is blocked. Previous pin retained.");
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
