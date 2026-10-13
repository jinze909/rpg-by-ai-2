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
