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

        public static void FishingBiteCueFiresOnlyOnRealSimulationBoundary(Campaign data)
        {
            var game = Fresh(data);
            Action(game, "fish", "fish");
            game.sounds.Clear();
            Check(Array.IndexOf(AudioSynthesis.Events, "fishBite") >= 0,
                "Real FrontierAudio will build the new bite sound during scene startup");
            float[] cue = AudioSynthesis.Event("fishBite");
            Check(cue.Length > AudioSynthesis.Rate / 8 &&
                cue.Length < AudioSynthesis.Rate / 2,
                "The original synthesized water cue is a brief valid live audio clip");
            float peak = 0;
            foreach(float sample in cue)
            {
                Check(!float.IsNaN(sample) && !float.IsInfinity(sample),
                    "Bite samples must remain finite for Unity AudioClip.SetData");
                peak = Math.Max(peak, Math.Abs(sample));
            }
            Check(peak > .03f && peak < .9f,
                "The water-plop sound is audible without clipping");
            for(int i=0;i<10;i++)game.Tick(.02f,new Point());
            Check(game.sounds.Count==0 && !game.FishLineReady,
                "No early water cue before a real bite is ready");
            for(int i=0;i<10;i++)game.Tick(.02f,new Point());
            Check(game.FishLineReady && game.sounds.Count==1 &&
                game.sounds.Peek()=="fishBite",
                "At first simulated bite exactly one real water cue is queued");
            for(int i=0;i<100;i++)game.Tick(.02f,new Point());
            Check(game.sounds.Count==1,
                "Waiting in the ready state does not spam new audio each frame");
            game.FishHit();
            int accepted=game.fishHits+game.fishMisses;
            Check(accepted==1 && !game.FishLineReady,
                "A resolved bite enters the next transient cooldown");
            game.sounds.Clear();
            for(int i=0;i<30;i++)game.Tick(.02f,new Point());
            Check(game.sounds.Count==1 && game.sounds.Peek()=="fishBite",
                "Next actual bite emits a fresh cue without altering the reward state");
        }


        public static void FishingUnansweredBitesReallyExpireAndEndTheCast(Campaign data)
        {
            var g=Fresh(data);
            int fish=g.save.inventory[3],coins=g.save.coins;
            Action(g,"fish","fish");
            Check(g.mode=="fish"&&!g.FishLineReady,
                "The initial float must begin before any live bite");
            for(int missed=0;missed<3;missed++)
            {
                int count=0;
                while(!g.FishLineReady&&g.mode=="fish"&&count++<90)
                    g.Tick(.05f,new Point());
                Check(g.FishLineReady&&
                    g.FishBiteTimeRemaining>GameCore.FishBiteWindow-.08f,
                    "A fresh live bite opens a measurable full reaction window");
                float previous=g.FishBiteTimeRemaining;
                g.Tick(.05f,new Point());
                Check(g.FishBiteTimeRemaining<previous,
                    "The live float drains its visible reaction time each simulation tick");
                count=0;
                while(g.FishLineReady&&count++<90)
                    g.Tick(.05f,new Point());
                Check(g.fishMisses==missed+1&&g.fishHits==0,
                    "Missing the live bite is exactly one real miss, not an endless free catch");
                if(missed<2)
                    Check(g.mode=="fish"&&!g.FishLineReady,
                        "Between bites the next opening requires a new audible bite");
            }
            Check(g.mode=="world"&&g.save.inventory[3]==fish&&
                g.save.coins==coins,
                "Three expired bites end the real cast without fish, coins or bounty rewards");
            g.FishHit();
            Check(g.fishMisses==3&&g.save.inventory[3]==fish,
                "Replaying reel input after a timed-out cast cannot create a fish");
        }

        public static void FishingLateInputCannotStealNextBiteOrExtendExpiry(Campaign data)
        {
            var g=Fresh(data);Action(g,"fish","fish");
            ReachFishingBite(g,true);
            float remaining=g.FishBiteTimeRemaining;
            for(int i=0;i<30;i++)g.FishHit();
            Check(g.fishHits==1&&g.fishMisses==0&&
                !g.FishLineReady&&g.FishBiteTimeRemaining==0f,
                "Only one real reel resolves a bite and closes its reaction window");
            for(int i=0;i<10;i++)g.FishHit();
            Check(g.fishHits==1&&g.fishMisses==0,
                "Button mashing between bites cannot shift the next expiry or double-credit");
            int ticks=0;
            while(!g.FishLineReady&&ticks++<90)g.Tick(.05f,new Point());
            Check(g.FishLineReady&&g.FishBiteTimeRemaining>remaining*.4f,
                "The next fish creates a fresh, independent reaction opportunity");
            while(g.FishLineReady&&ticks++<200)g.Tick(.05f,new Point());
            Check(g.fishHits==1&&g.fishMisses==1,
                "The subsequent unanswered bite expires once without retracting the earlier success");
        }


        public static void FishingReelShowsRealDirectionalFeedbackAndResetsAtNextBite(Campaign data)
        {
            var g=Fresh(data);
            Action(g,"fish","fish");
            ReachFishingBite(g,false);
            bool left=g.fishCursor<g.FishTarget;
            g.FishHit();
            Check(g.fishMisses==1&&
                g.FishFeedbackZh.Contains(left?"偏左":"偏右")&&
                g.FishFeedbackEn.Contains(left?"left":"right"),
                "A bad physical reel explains which side of the moving float missed");
            ReachFishingBite(g,true);
            Check(g.FishFeedbackZh.Length==0&&g.FishFeedbackEn.Length==0,
                "A new real fish bite clears stale directional advice");
            g.FishHit();
            Check(g.fishHits==1&&g.FishFeedbackZh.Contains("绷紧")&&
                g.FishFeedbackEn.Contains("Good hook"),
                "A good pull gives different feedback without granting an extra item");
            int steps=0;
            while(!g.FishLineReady&&steps++<100)g.Tick(.05f,new Point());
            Check(g.FishLineReady&&g.FishFeedbackZh.Length==0,
                "The following live bite starts with a clean presentation state");
            while(g.FishLineReady&&steps++<200)g.Tick(.05f,new Point());
            Check(g.fishHits==1&&g.fishMisses==2&&
                g.FishFeedbackZh.Contains("及时")&&
                g.FishFeedbackEn.Contains("promptly"),
                "An unanswered bite receives explicit timing feedback distinct from a mistimed reel");
        }

        public static void ReducedMotionFishingKeepsReadableSteadyBite(Campaign data)
        {
            var game=Fresh(data);
            game.save.reducedMotion=true;
            Action(game,"fish","fish");
            Check(game.mode=="fish"&&Math.Abs(game.FishTarget-.5f)<.0001f,
                "Accessibility mode anchors the bite target at the original center");
            float minFloat=float.MaxValue,maxFloat=float.MinValue;
            for(int i=0;i<90;i++)
            {
                game.Tick(.02f,new Point());
                minFloat=Math.Min(minFloat,game.fishCursor);
                maxFloat=Math.Max(maxFloat,game.fishCursor);
            }
            Check(Math.Abs(game.FishTarget-.5f)<.0001f &&
                maxFloat-minFloat>.6f,
                "Reduced motion anchors the target but keeps a genuinely active timing float");
            ReachFishingBite(game,true);game.FishHit();
            Check(game.fishHits==1&&!game.FishLineReady &&
                Math.Abs(game.FishTarget-.5f)<.0001f,
                "A real first hook still obeys cooldown and keeps the steady accessibility target");
            for(int i=0;i<2;i++)
            {
                ReachFishingBite(game,true);game.FishHit();
            }
            Check(game.mode=="world" && game.save.inventory[3]==1,
                "Three independent reduced-motion bites earn the same original fish reward");
        }
    }
}
