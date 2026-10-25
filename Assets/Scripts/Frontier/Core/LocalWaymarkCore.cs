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
            localWaymarkFoot=new Point(
                (float)Math.Floor(requested.x)+.5f,
                (float)Math.Floor(requested.y)+.5f);
            localWaymarkRoom=save.room;
            localWaymarkMap=Map;
            localWaymarkSet=true;
            localWaymarkBlocked=false;
            localWaymarkRouteRecheck=1f;
            revision++;sound="ui";
            Notify("已设置本地标记。返回世界后，跟随地图上的路线前往。",
                "Local map pin set. Return to the world and follow its route.");
            return true;
        }
        private void TickLocalWaymark(float dt)
        {
            Point? mark=LocalWaymark;
            if(!mark.HasValue)return;
            if((Position-mark.Value).Length<=.85f &&
               ReliefActorLineWalkable(Position,mark.Value))
            {
                ResetLocalWaymark();
                revision++;sound="quest";
                Notify("已到达个人标记位置。", "Personal map destination reached.");
                return;
            }
            // Local pins must survive closed tidal gates and temporary road
            // barriers. Recheck the complete physical route at a low fixed
            // rate instead of calling the 120x80 BFS every render/sim tick.
            localWaymarkRouteRecheck-=dt;
            if(localWaymarkRouteRecheck>0f)return;
            localWaymarkRouteRecheck=1.25f;
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
