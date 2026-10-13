using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Existing hostile archetypes populate actual fifth-ring clearings.
        // All locations are off the historical story/Boss arena and at least
        // 12 tiles from new interactive ridge endpoints. No fake silhouettes.
        private static readonly Point[] RidgeWildlifeSpawns = {
            new Point(101.5f,61.5f),
            new Point(89.5f,67.5f),
            new Point(76.5f,66.5f)
        };
        private void SpawnRidgeWildlife()
        {
            // Never spawn in the safe starting town, inn or procedural abyss.
            if(save.room<1||save.room>7||!RidgeLinked(save.room)||
                !Has("boss_"+save.room))return;
            // Late-game wildlife never expands the required combat roster
            // for an unfinished regional guardian or an untouched story gate.
            // They return only after both ridge bearings AND the original
            // region guardian have been resolved on this save slot.
            for(int i=0;i<RidgeWildlifeSpawns.Length;i++)
            {
                Point position=RidgeWildlifeSpawns[i];
                if(!CanStand(position))continue;
                bool adjacentToObjective=false;
                foreach(WorldObject o in Map.objects)
                    if((new Point(o.x,o.y)-position).Length<8)
                    { adjacentToObjective=true;break; }
                if(adjacentToObjective)continue;
                enemies.Add(CreatePlacedEnemy(++enemyId,(save.room+i)%3,
                    position,30+save.room*7+i*9));
            }
        }
    }
}
