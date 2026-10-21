using UnityEngine;

namespace Starfall
{
    // Runtime imports may be lower-resolution than source PNGs, but every
    // frame must remain a complete square cell in the approved grid. CI
    // separately pins each shipped source PNG to its approved full dimensions.
    // No UNITY_5_3_OR_NEWER guard: all harnesses compile the same rules.
    internal static class ActorAtlasGeometry
    {
        private static bool HasSquareGrid(Texture2D sheet, int columns, int rows)
        {
            return sheet != null && columns > 0 && rows > 0 &&
                sheet.width >= columns * 32 && sheet.height >= rows * 32 &&
                sheet.width % columns == 0 && sheet.height % rows == 0 &&
                sheet.width / columns == sheet.height / rows;
        }

        internal static bool IsAcceptedMonstersAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 6, 3);
        }

        internal static bool IsAcceptedBossesAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 8);
        }

        internal static bool IsAcceptedBriarBeastAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 18);
        }

        internal static bool IsAcceptedMossboundRaiderAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 6);
        }

        internal static bool IsAcceptedCinderstoneGuardAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 6);
        }

        internal static bool IsAcceptedNpcAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 4);
        }

        internal static bool IsAcceptedCompanionAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 3, 4);
        }

        internal static bool IsAcceptedWalkMonstersAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 3);
        }

        internal static bool IsAcceptedRegionalBossWalkAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 4, 4);
        }

        internal static bool IsAcceptedVerticalArcherAtlas(Texture2D sheet)
        {
            return HasSquareGrid(sheet, 3, 2);
        }
    }
}
