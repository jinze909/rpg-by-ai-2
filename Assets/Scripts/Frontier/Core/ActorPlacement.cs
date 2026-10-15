using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Use the same complete foot clearance as movement, including active gates and tides.
        // Preserve valid authored/saved positions; only repair positions that actually overlap.
        private Point SafeActorFoot(Point proposed)
        {
            if (!Finite(proposed.x) || !Finite(proposed.y) || proposed.x < 0 || proposed.y < 0 || proposed.x >= Map.rows[0].Length || proposed.y >= Map.rows.Length)
                proposed = new Point(Map.spawnX, Map.spawnY);
            if (Fits(proposed)) return proposed;
            Point result = new Point(); float nearest = float.MaxValue;
            for (int y = 2; y < Map.rows.Length - 2; y++)
            for (int x = 2; x < Map.rows[y].Length - 2; x++)
            {
                Point candidate = new Point(x + .5f, y + .5f), delta = candidate - proposed;
                float squared = delta.x * delta.x + delta.y * delta.y;
                if (squared >= nearest || !Fits(candidate)) continue;
                result = candidate; nearest = squared;
            }
            if (nearest == float.MaxValue) throw new InvalidOperationException("The region has no valid actor footing.");
            return result;
        }
        private Enemy CreatePlacedEnemy(int id, int kind, Point proposed, float health, bool boss = false)
        {
            Point p = SafeActorFoot(proposed);
            return new Enemy(id, kind, p.x, p.y, health, boss);
        }
        private void TickTideTransition()
        {
            if (save.room != 5 || previousLowTide == LowTide) return;
            previousLowTide = LowTide;
            if (!Fits(Position))
            {
                Point p = SafeActorFoot(Position); save.x = p.x; save.y = p.y;
                CancelAction(); dashTime = 0; playerRecoil = new Point();
                Notify("潮水涨起，你退回了最近的岸边。闸门可固定低潮。", "The tide rises; you retreat to the nearest bank. The sluice can hold low tide.");
            }
            RecoverTideActors();
        }
        private void RecoverTideActors()
        {
            foreach (Enemy enemy in enemies) if (!Fits(enemy.Position))
            {
                Point p = SafeActorFoot(enemy.Position); enemy.x = p.x; enemy.y = p.y;
                enemy.moving = enemy.backpedaling = false; enemy.state = 0; enemy.releaseTime = 0;
                enemy.timer = Math.Max(.35f, enemy.timer);
            }
            if (save.companion >= 0 && !Fits(companionPosition))
            {
                companionPosition = SafeActorFoot(companionPosition);
                companionAction = null; companionMoving = false;
            }
            // The real evacuee is a world actor too. Without this, the
            // outgoing tide can trap her in newly flooded shoals while the
            // hero and enemies recover to the nearest walkable bank.
            if (ReliefEscortActive && !Fits(reliefFollowerPosition))
                reliefFollowerPosition = SafeActorFoot(reliefFollowerPosition);
        }
    }
}
