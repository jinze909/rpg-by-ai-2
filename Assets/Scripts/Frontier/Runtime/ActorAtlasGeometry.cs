using UnityEngine;

namespace Starfall
{
    // Shared production geometry contract for whole-body actor atlases.
    // This file intentionally has no UNITY_5_3_OR_NEWER guard: gameplay
    // source checks and native renderer checks must compile the same rules.
    internal static class ActorAtlasGeometry
    {
        internal static bool IsAcceptedWalkMonstersAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 768 && sheet.height == 576;
        }

        internal static bool IsAcceptedRegionalBossWalkAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 1536 && sheet.height == 1536;
        }

        internal static bool IsAcceptedVerticalArcherAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 576 && sheet.height == 384;
        }
    }
}
