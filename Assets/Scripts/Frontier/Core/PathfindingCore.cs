using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Searches are synchronous. Share workspace, never the returned route:
        // the HUD can keep a route while several enemies find their next step.
        private int[] pathParents = new int[0], pathQueue = new int[0], pathSeen = new int[0];
        private int pathGeneration;
        private static readonly int[,] pursuitSteps = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] guidanceSteps = { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
        private void PreparePathSearch(int cells)
        {
            if (pathParents.Length < cells)
            {
                pathParents = new int[cells];
                pathQueue = new int[cells];
                pathSeen = new int[cells];
            }
            // The 120x80 outdoors were clearing 9,600 parent slots for
            // EVERY chase and HUD path request, even when only a handful of
            // neighbors were searched. Visit stamps make setup O(1) while
            // retaining the exact BFS route order and dynamic Fits checks.
            if (pathGeneration == int.MaxValue)
            {
                Array.Clear(pathSeen, 0, pathSeen.Length);
                pathGeneration = 0;
            }
            ++pathGeneration;
        }
        private bool PathVisited(int cell) { return pathSeen[cell] == pathGeneration; }
        private void VisitPath(int cell, int parent)
        {
            pathSeen[cell] = pathGeneration;
            pathParents[cell] = parent;
        }
    }
}
