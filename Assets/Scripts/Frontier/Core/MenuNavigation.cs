using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed class FocusControl
    {
        public string id; public float x, y, w, h; public bool enabled;
        public Point Center { get { return new Point(x + w / 2, y + h / 2); } }
    }
    // Geometry belongs to the view; focus and navigation rules can be tested without Unity.

    // A gamepad/keyboard map picker has actual tile coordinates, never GUI
    // pixel coordinates or an automatically accepted pin. Confirmation still
    // uses GameCore.PlaceLocalWaymark's real walkability and route checks.
    public sealed class MapTileCursor
    {
        private int tileX,tileY,width,height;
        private int axisX,axisY;
        private float axisHeld;
        public bool Active { get; private set; }
        public Point Destination { get { return new Point(tileX+.5f,tileY+.5f); } }
        public void Begin(Point position,int mapWidth,int mapHeight)
        {
            Cancel();
            if(mapWidth<=0||mapHeight<=0||
                float.IsNaN(position.x)||float.IsNaN(position.y)||
                float.IsInfinity(position.x)||float.IsInfinity(position.y))return;
            width=mapWidth;height=mapHeight;
            tileX=(int)Math.Max(0,Math.Min(width-1,Math.Floor(position.x)));
            tileY=(int)Math.Max(0,Math.Min(height-1,Math.Floor(position.y)));
            Active=true;
        }
        public void Move(int dx,int dy)
        {
            if(!Active)return;
            tileX=(int)Math.Max(0L,Math.Min((long)width-1,(long)tileX+dx));
            tileY=(int)Math.Max(0L,Math.Min((long)height-1,(long)tileY+dy));
        }
        // D-pad/stick travel is initially precise. Repeating the SAME held
        // direction for .65s accelerates across the 120x80 field without
        // accelerating a fresh tap or a direction reversal. The caller owns
        // the actual repeat cadence; this state only chooses distance.
        // Both personal waypoint and companion-post editors use this cursor.
        public bool MoveAxis(int dx,int dy,float dt,bool repeatReady)
        {
            if(!Active)return false;
            if(dx==0&&dy==0)
            {axisX=axisY=0;axisHeld=0f;return false;}
            if(dx!=0){dx=Math.Sign(dx);dy=0;}
            else dy=Math.Sign(dy);
            if(dx!=axisX||dy!=axisY)
            {axisX=dx;axisY=dy;axisHeld=0f;}
            else if(!float.IsNaN(dt)&&!float.IsInfinity(dt))
                axisHeld=Math.Min(2f,axisHeld+Math.Max(0f,dt));
            if(!repeatReady)return false;
            Point before=Destination;
            int stride=axisHeld>=.65f?6:1;
            Move(dx*stride,dy*stride);
            return (Destination-before).Length>.001f;
        }
        public void Cancel()
        {
            Active=false;tileX=tileY=width=height=0;
            axisX=axisY=0;axisHeld=0f;
        }
    }
    public sealed class MenuNavigation
    {
        private readonly List<FocusControl> controls = new List<FocusControl>();
        private readonly Dictionary<string, string> remembered = new Dictionary<string, string>();
        public string scope = "", focused = "";
        public void Scope(string value)
        {
            if (scope == value) return;
            // Conversations carry a fresh runtime object identity in their scope.
            // Remembering those one-use pages grows forever after NPC dialogue,
            // and a recycled hash can resurrect focus from an unrelated speaker.
            // Only durable menus need remembered focus across visits.
            if (!string.IsNullOrEmpty(scope) &&
                !scope.StartsWith("dialog/", StringComparison.Ordinal))
                remembered[scope] = focused;
            scope = value;
            controls.Clear();
            focused = remembered.ContainsKey(value) ? remembered[value] : "";
        }
        public void SetControls(IEnumerable<FocusControl> values)
        {
            controls.Clear(); foreach (var c in values) if (c.enabled) controls.Add(c);
            if (!controls.Exists(c => c.id == focused)) focused = controls.Count > 0 ? controls[0].id : "";
        }
        public void Select(string id) { if (controls.Exists(c => c.id == id)) focused = id; }
        public void Cycle(int direction)
        { if (controls.Count == 0) return; int i = controls.FindIndex(c => c.id == focused); focused = controls[(i + direction + controls.Count) % controls.Count].id; }
        public void Move(Point direction)
        {
            var current = controls.Find(c => c.id == focused); if (current == null) { Cycle(1); return; }
            FocusControl best = null; float score = float.MaxValue;
            foreach (var c in controls)
            {
                Point delta = c.Center - current.Center; float forward = delta.x * direction.x + delta.y * direction.y;
                if (forward < 1) continue; float side = Math.Abs(delta.x * direction.y - delta.y * direction.x);
                float candidate = forward + side * 4 + side * side / Math.Max(20, forward);
                if (candidate < score) { score = candidate; best = c; }
            }
            if (best != null) focused = best.id;
        }
        public bool CanActivate(string id) { return controls.Exists(c => c.id == id && c.enabled); }
    }
}
