#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace Starfall
{
    /// <summary>
    /// One approved blonde identity for all three equipment/profession atlases.
    /// Shared by production runtime capture and the Editor-only Sprite bake.
    /// The approved original sprites, not the rejected generated sheets, are
    /// the only pixel input. All frames share a 256-square canvas, 128 PPU and
    /// one bottom-origin (128,16) foot pivot.
    /// </summary>
    public static class CanonicalHeroSpriteFactory
    {
        // The accepted playable blonde hero is ONE identity in all classes.
        // Every renderer and generated animation uses this pixel specification.
        public const int Cell = 256;
        public const float PixelsPerUnit = 128f;
        public const int FootX = 128;
        public const int FootTop = 240;
        public const int IdentityHeadX = 100;
        public const int IdentityHeadTop = 140;
        public const int IdentityHeadSize = 56;
        public const float GuiDensity = .75f;
        public static readonly Vector2 FootPivot =
            new Vector2(FootX / (float)Cell, (Cell - FootTop) / (float)Cell);

        public static Sprite Bake(Texture2D source, int index, Texture2D registration,
            int registrationIndex, int facing, bool locomotion, bool death, Texture2D identity)
        {
            if (source == null || source.width % Cell != 0 || source.height % Cell != 0)
                return null;
            int cols = source.width / Cell;
            if (index < 0 || index >= cols * (source.height / Cell)) return null;
            Color[] colors = source.GetPixels(index % cols * Cell,
                source.height - (index / cols + 1) * Cell, Cell, Cell);
            if (!death && identity != null && registration != null)
            {
                Rect head, stable;
                if (ActorFrameRegistration.TryHead(registration, registrationIndex, out head))
                {
                    if (locomotion && ActorFrameRegistration.TryStableHead(registration,
                        registrationIndex / 4, out stable)) head = stable;
                    Rect reference;
                    if (ActorFrameRegistration.TryStableHead(identity, facing, out reference))
                    {
                        // The registered golden hair/face must stay the same
                        // size and source on all three equipment variants.
                        // Reject unexpected reimports instead of resizing it.
                        if (Mathf.RoundToInt(reference.x) != IdentityHeadX ||
                            Mathf.RoundToInt(reference.y) != IdentityHeadTop ||
                            Mathf.RoundToInt(reference.width) != IdentityHeadSize ||
                            Mathf.RoundToInt(reference.height) != IdentityHeadSize)
                            return null;
                        int hx = Mathf.RoundToInt(head.x), hy = Mathf.RoundToInt(head.y);
                        int hw = Mathf.RoundToInt(head.width), hh = Mathf.RoundToInt(head.height);
                        if (hx < 0 || hy < 0 || hx + hw > Cell || hy + hh > Cell)
                            return null;
                        for (int row = hy; row < hy + hh; row++)
                            for (int col = hx; col < hx + hw; col++)
                                colors[(Cell - 1 - row) * Cell + col] = Color.clear;

                        int w = Mathf.RoundToInt(reference.width);
                        int h = Mathf.RoundToInt(reference.height);
                        int canonicalIndex = facing * 4;
                        Color[] hair = identity.GetPixels((canonicalIndex % 4) * Cell
                            + Mathf.RoundToInt(reference.x),
                            identity.height - (canonicalIndex / 4 + 1) * Cell
                            + Cell - Mathf.RoundToInt(reference.y + reference.height), w, h);
                        int destX = locomotion ? (Cell - w) / 2 :
                            Mathf.RoundToInt(head.x + head.width / 2f - w / 2f);
                        int destY = locomotion ? Mathf.RoundToInt(reference.y) :
                            Mathf.RoundToInt(head.y + head.height - h);
                        for (int row = 0; row < h; row++)
                            for (int col = 0; col < w; col++)
                            {
                                int px = destX + col, py = destY + row;
                                if (px < 0 || px >= Cell || py < 0 || py >= Cell) continue;
                                Color upper = hair[(h - 1 - row) * w + col];
                                if (upper.a <= 0f) continue;
                                int pos = (Cell - 1 - py) * Cell + px;
                                Color lower = colors[pos];
                                float alpha = upper.a + lower.a * (1 - upper.a);
                                colors[pos] = alpha > 0
                                    ? new Color((upper.r * upper.a + lower.r * lower.a * (1 - upper.a)) / alpha,
                                        (upper.g * upper.a + lower.g * lower.a * (1 - upper.a)) / alpha,
                                        (upper.b * upper.a + lower.b * lower.a * (1 - upper.a)) / alpha, alpha)
                                    : Color.clear;
                            }
                    }
                }
            }

            var texture = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(colors);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, Cell, Cell),
                FootPivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
#endif
