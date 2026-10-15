#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace Starfall
{
    /// <summary>
    /// One complete accepted body per class, with the same blonde protagonist.
    /// Shared by production runtime capture and the Editor-only Sprite bake.
    /// The accepted whole-body originals, not generated puppet pieces, are
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
            // Full-body-only native fallback. The canonical identity is
            // already present in each accepted Ranger/Vanguard/Arcanist
            // source. NEVER erase and reattach a separate warrior head in
            // gameplay: the old movable head mask introduced a neck gap,
            // facial bobbing and class/body pose mismatch. The published
            // PNG pipeline retains approved face pixels upstream.
            // Keeping the legacy method signature preserves editor callers,
            // but registration/identity must not change the body raster.
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
