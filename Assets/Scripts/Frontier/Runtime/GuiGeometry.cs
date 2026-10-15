using UnityEngine;
namespace Starfall
{
    public static class GuiGeometry
    {
        // Rotate in the game's logical canvas, then apply the viewport scale
        // and letterbox offset. A screen-space pivot moves effects at any
        // resolution other than the reference canvas.
        public static Matrix4x4 RotateInCanvas(Matrix4x4 canvas, Rect rect, float angle)
        {
            Vector2 pivot = new Vector2(rect.x + rect.width / 2,
                rect.y + rect.height / 2);
            return RotateAroundPivot(canvas, pivot, angle);
        }

        // Actor/effect centre rotations and rooted vegetation share the
        // same logical canvas transform, but NOT the same centre. Applying
        // canvas last preserves the root on letterboxed 4K and ultrawide.
        public static Matrix4x4 RotateAroundPivot(Matrix4x4 canvas,
            Vector2 pivot, float angle)
        {
            Vector3 center = new Vector3(pivot.x, pivot.y, 0);
            return canvas * Matrix4x4.TRS(center,
                Quaternion.Euler(0, 0, angle), new Vector3(1, 1, 1))
                * Matrix4x4.TRS(new Vector3(-center.x, -center.y, 0),
                    Quaternion.identity, new Vector3(1, 1, 1));
        }
    }
}
