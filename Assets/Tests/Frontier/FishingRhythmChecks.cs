using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void ReachFishingBite(GameCore game, bool inside)
        {
            int frames = 0;
            while (frames++ < 1400)
            {
                game.Tick(.02f, new Point());
                float deviation = Math.Abs(game.fishCursor - game.FishTarget);
                if (game.FishLineReady &&
                    (inside ? deviation < .055f : deviation > .30f)) return;
            }
            throw new Exception("Live float never reached the requested fish bite state");
        }

        public static void FishingBitesMoveAndCannotBeClaimedByButtonMashing(Campaign data)
        {
            var game = Fresh(data);
            int fish = game.save.inventory[3], coins = game.save.coins;
            Action(game, "fish", "fish");
            Check(game.mode == "fish" && !game.FishLineReady,
                "A fresh cast waits for an actual fish bite before accepting a reel");
            for (int i = 0; i < 20; i++) game.FishHit();
            Check(game.fishHits == 0 && game.fishMisses == 0,
                "Repeated keyboard, pad and pointer reels cannot claim an unready float");
            float initial = game.FishTarget;
            ReachFishingBite(game, true);
            Check(Math.Abs(initial - game.FishTarget) > .005f,
                "The live teal bite zone drifts rather than staying fixed at center");
            game.FishHit();
            Check(game.fishHits == 1 && !game.FishLineReady,
                "A genuine skillfully timed hook enters a between-bite recovery");
            for (int i = 0; i < 20; i++) game.FishHit();
            Check(game.fishHits == 1 && game.fishMisses == 0,
                "One physical bite cannot pay for several back-to-back presses");
            for (int i = 0; i < 2; i++)
            {
                ReachFishingBite(game, true);
                game.FishHit();
            }
            Check(game.mode == "world" && game.save.inventory[3] == fish + 1 &&
                game.save.coins == coins + 4,
                "Three separate authentic bites catch exactly one Silverfin and four coins");
            game.FishHit();
            Check(game.save.inventory[3] == fish + 1,
                "Further stale input after completion never pays a second reward");
        }

        public static void FishingMissesResetBiteAndGiveNoRewards(Campaign data)
        {
            var game = Fresh(data);
            int fish = game.save.inventory[3], coins = game.save.coins;
            Action(game, "fish", "fish");
            for (int miss = 0; miss < 3; miss++)
            {
                ReachFishingBite(game, false);
                game.FishHit();
                Check(game.fishMisses == miss + 1,
                    "The actual moving float, not a fixed center point, resolves each lost bite");
                if (miss < 2)
                {
                    Check(!game.FishLineReady,
                        "A failed reel also requires a new real bite before it can be judged again");
                    game.FishHit();
                    Check(game.fishMisses == miss + 1,
                        "Repeated miss clicks in one bite cannot instantly exhaust the cast");
                }
            }
            Check(game.mode == "world" && game.save.inventory[3] == fish &&
                game.save.coins == coins,
                "Three separately timed misses close the cast without fishing rewards");
            Action(game, "fish", "fish");
            Check(game.mode == "fish" && game.fishHits == 0 && game.fishMisses == 0 &&
                !game.FishLineReady,
                "Retrying starts a clean new bite cycle rather than inheriting the old timer");
            game.Close();
            Check(game.save.inventory[3] == fish,
                "Exiting a fishing game voluntarily cannot claim incomplete catches");
        }
    }
}
