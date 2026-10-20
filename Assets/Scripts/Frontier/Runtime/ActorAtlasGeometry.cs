using UnityEngine;

namespace Starfall
{
    // Shared production geometry contract for whole-body actor atlases.
    // This file intentionally has no UNITY_5_3_OR_NEWER guard: gameplay
    // source checks and native renderer checks must compile the same rules.
    internal static class ActorAtlasGeometry
    {
        internal static bool IsAcceptedMonstersAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 768 && sheet.height == 384;
        }

        internal static bool IsAcceptedBossesAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 1280 && sheet.height == 2560;
        }

        internal static bool IsAcceptedBriarBeastAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 1280 && sheet.height == 5760;
        }

        internal static bool IsAcceptedMossboundRaiderAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 512 && sheet.height == 768;
        }

        internal static bool IsAcceptedCinderstoneGuardAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 512 && sheet.height == 768;
        }

        internal static bool IsAcceptedNpcAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 512 && sheet.height == 512;
        }

        internal static bool IsAcceptedCompanionAtlas(Texture2D sheet)
        {
            return sheet != null && sheet.width == 768 && sheet.height == 1024;
        }

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
