using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Terrain and ordinary props stay in the authored map. Repeated
        // movement/camera/enemy sweeps need only solid house footprints, not
        // every decorative tree, lamp, marker and newly added prop.
        private MapData cachedColliderMap;
        private Decoration[] cachedColliderSource;
        private Decoration[] cachedHouses = new Decoration[0];
        private Decoration[] StaticHouseColliders()
        {
            MapData map = Map;
            if (!Object.ReferenceEquals(cachedColliderMap, map) ||
                !Object.ReferenceEquals(cachedColliderSource, map.decor))
            {
                var houses = new List<Decoration>();
                foreach (Decoration decor in map.decor)
                    if (decor.kind == "house") houses.Add(decor);
                cachedHouses = houses.ToArray();
                cachedColliderMap = map;
                cachedColliderSource = map.decor;
            }
            return cachedHouses;
        }
    }
}
