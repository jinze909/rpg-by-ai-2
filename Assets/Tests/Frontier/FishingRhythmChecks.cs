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



        public static void PublicFishKitchenCreatesRealRepeatableStewAndUpgradedFood(Campaign data)
        {
            var g=Fresh(data);WorldObject pond=Object(g,"fish");
            g.enemies.Clear();g.save.x=pond.x;g.save.y=pond.y;
            g.save.inventory[3]=5;g.save.inventory[6]=0;
            g.Talk(pond);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen")<0,
                "Public kitchen is genuinely locked behind Old Tide's original three-fish commission");
            int deliver=Array.FindIndex(g.conversation.choices,c=>c.action=="ally2");
            Check(deliver>=0,"Original fish commission remains a reachable NPC choice");
            g.Choose(deliver);
            Check(g.Has("ally_2")&&g.save.inventory[3]==2&&
                !g.PublicFishKitchenStocked&&g.StewHealing==55&&g.StewBuffSeconds==90f,
                "Historic three-fish reward is preserved and cannot accidentally unlock the kitchen");
            // The historic three-fish commission itself gifts one Tide
            // stew. The new two-fish exchange adds ONE MORE, not a total
            // of one, and must never erase that established reward.
            int initialStew=g.save.inventory[6];
            Check(initialStew==1,"Original commission still provides its existing free stew");
            int beforeMoments=g.save.moments.Count;
            g.Talk(pond);
            int kitchen=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen");
            Check(kitchen>=0&&g.conversation.choices[kitchen].en.Contains("2 fish"),
                "Real lake fisherman now offers a new physical two-fish supply trade");
            g.Choose(kitchen);
            Check(g.PublicFishKitchenStocked&&g.save.inventory[3]==0&&
                g.save.inventory[6]==initialStew+1&&g.StewHealing==65&&
                g.StewBuffSeconds==105f&&
                g.save.moments.Count==Math.Min(8,beforeMoments+1)&&
                g.save.moments.Exists(m=>m.en.Contains("public kitchen")),
                "First real fish delivery stocks the kitchen, produces one edible stew, records its civic impact and upgrades food; observed stocked="+g.PublicFishKitchenStocked+
                ", fish="+g.save.inventory[3]+", stew="+g.save.inventory[6]+
                ", heal="+g.StewHealing+", duration="+g.StewBuffSeconds+
                ", moments="+g.save.moments.Count+", previous="+beforeMoments+
                ", hasMoment="+g.save.moments.Exists(m=>m.en.Contains("public kitchen"))+
                ", mode="+g.mode+", toast="+g.toast);
            g.save.health=10;g.save.energy=20;g.save.totalTime=40;
            g.Eat();
            Check(g.save.health==75&&g.save.energy==100f&&
                g.save.foodUntil==145f&&g.save.inventory[6]==initialStew,
                "The earned permanent kitchen upgrade changes actual healing and combat buff time");
            g.save.inventory[3]=4;g.Talk(pond);
            kitchen=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen");
            g.Choose(kitchen);
            Check(g.save.inventory[3]==2&&g.save.inventory[6]==initialStew+1&&
                g.save.moments.Count==Math.Min(8,beforeMoments+1),
                "Repeatable two-fish trade issues one real consumable and never duplicates the one-time chronicle");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.PublicFishKitchenStocked&&restored.StewHealing==65&&
                restored.StewBuffSeconds==105f&&restored.save.inventory[6]==initialStew+1&&
                restored.save.version==2&&
                restored.WorldResponse().Contains("湖畔公共厨房"),
                "Kitchen benefit, remaining edible stock and visible civic world consequence survive unchanged v2 save/reload");
        }

        public static void PublicFishKitchenRejectsStaleChoicesAndInventoryOverflow(Campaign data)
        {
            var g=Fresh(data);WorldObject pond=Object(g,"fish");
            g.enemies.Clear();g.save.x=pond.x;g.save.y=pond.y;
            g.save.flags.Add("ally_2");g.save.inventory[3]=2;
            g.save.inventory[6]=9999;
            g.Talk(pond);
            int trade=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen");
            Check(trade>=0&&g.conversation.choices[trade].en.Contains("need 2 fish"),
                "Full stew stack is explained at original real fishing conversation");
            int moments=g.save.moments.Count;
            g.Choose(trade);
            Check(g.save.inventory[3]==2&&g.save.inventory[6]==9999&&
                !g.PublicFishKitchenStocked&&g.save.moments.Count==moments,
                "Full stew stack never consumes catch or prematurely grants a permanent upgrade");
            g.save.inventory[6]=0;g.save.inventory[3]=1;
            g.Talk(pond);trade=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen");
            g.Choose(trade);
            Check(g.save.inventory[3]==1&&g.save.inventory[6]==0&&
                !g.PublicFishKitchenStocked,
                "Insufficient fish can never create a free stew or claim kitchen progress");
            g.save.inventory[3]=2;g.Talk(pond);
            trade=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen");
            g.save.x=pond.x+15f;g.save.y=pond.y+15f;
            g.Choose(trade);
            Check(g.save.inventory[3]==2&&g.save.inventory[6]==0&&
                !g.PublicFishKitchenStocked,
                "Cached kitchen choices cannot trade fish from outside real NPC interaction range");
            g.save.x=pond.x;g.save.y=pond.y;
            g.save.flags.Remove("ally_2");g.Talk(pond);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="fish_kitchen")<0,
                "Legacy players without the completed civic commission cannot use kitchen supply");
            g.Close();
            g.save.health=10;g.save.energy=20;g.save.inventory[6]=1;
            g.Eat();
            Check(g.save.health==65&&
                Math.Abs(g.save.foodUntil-g.save.totalTime-90f)<.001f,
                "Unupgraded legacy stew retains original 55 HP and 90-second boost");
        }
        public static void MistportTidalFishingHasPhysicalEbbGateLiveBraceAndRealThreeFishHaul(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("exp_intro");
            // Use the real post-introduction unlock. Enter() correctly
            // rejects locked Mistport and would leave this test in town.
            g.Enter(5);g.enemies.Clear();g.save.totalTime=36f;
            Check(g.save.room==5,"True chapter progression opened the real harbor");
            WorldObject pier=Object(g,"tidefishery");
            g.save.x=pier.x;g.save.y=pier.y;
            Check(g.CanStand(g.Position)&&g.LowTide,
                "The new fishing pier is real standable ground on the actual low-tide harbor map");
            g.Talk(pier);
            int action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            Check(action>=0,"The actual new dock exposes its original player-facing tidal cast");
            int parts=g.save.inventory[4],fish=g.save.inventory[3],coins=g.save.coins;
            g.Choose(action);
            Check(g.mode=="world"&&!g.FishTidalCast&&g.save.inventory[4]==parts,
                "Sealed harbor cannot begin a fake tide cast or charge parts");
            g.save.flags.Add("harbor_report");
            g.save.totalTime=33f;g.Talk(pier);
            action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(!g.FishTidalCast&&g.save.inventory[4]==parts&&!g.LowTide,
                "A rising tide blocks rigging without consuming the original item");
            g.save.totalTime=36f;g.Talk(pier);
            action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.save.x=pier.x+8f;g.Choose(action);
            Check(g.mode=="world"&&g.save.inventory[4]==parts,
                "Cached dock choice cannot spend items across the physically reachable channel");
            g.save.x=pier.x;g.save.y=pier.y;
            g.save.inventory[3]=9997;
            g.Talk(pier);action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(g.mode=="world"&&g.save.inventory[4]==parts,
                "Three-fish tidal catches cannot be launched into a two-slot inventory");
            g.save.inventory[3]=fish;
            g.Talk(pier);action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(g.mode=="fish"&&g.FishTidalCast&&!g.FishExpertCast&&
                g.FishCatchReward==3&&g.save.inventory[4]==parts-1&&
                Math.Abs(g.FishBiteWindowSeconds-2.05f)<.0001f&&
                Math.Abs(g.FishWindowRadius-.09f)<.0001f,
                "New physical low-tide challenge spends exactly one real spare part");
            Check(!g.BraceTidalLine(),"Line cannot be braced before a genuine bite");
            for(int i=0;i<20&&!g.FishLineReady;i++)g.Tick(.02f,new Point());
            Check(g.FishLineReady&&g.CanBraceTidalLine,"Bracing requires a live bite with time remaining");
            float originalRemaining=g.FishBiteTimeRemaining;
            Check(g.BraceTidalLine()&&g.FishTidalBraced&&!g.CanBraceTidalLine&&
                Math.Abs(g.FishBiteTimeRemaining-originalRemaining+.35f)<.02f,
                "A single brace freezes this bite but actually consumes 0.35 seconds of opportunity");
            float lockedTarget=g.FishTarget;
            g.Tick(.1f,new Point());
            Check(Math.Abs(g.FishTarget-lockedTarget)<.0001f&&!g.BraceTidalLine(),
                "Target truly holds through simulation; no duplicated brace per bite");
            g.Close();Check(!g.FishTidalCast&&g.mode=="world",
                "Canceling a cast releases its temporary double-current state");
            g.Talk(pier);action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(g.mode=="fish"&&g.save.inventory[4]==parts-2,
                "A fresh cast starts a new challenge with a real additional rigging cost");
            int initialMoments=g.save.moments.Count;
            for(int i=0;i<3;i++)
            {
                ReachFishingBite(g,true);g.FishHit();
                if(i<2)Check(g.mode=="fish"&&g.fishHits==i+1&&!g.FishLineReady,
                    "One real wave contact cannot advance two hooks at once");
            }
            Check(g.mode=="world"&&!g.FishTidalCast&&
                g.save.inventory[3]==fish+3&&g.save.coins==coins+12&&
                g.Has("mistport_tidal_first")&&
                g.save.moments.Count==Math.Min(8,initialMoments+1),
                "Three distinct changing current contacts pay actual three fish, twelve coins and one chronicle");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.Has("mistport_tidal_first")&&
                !restored.FishTidalCast&&restored.save.inventory[3]==fish+3,
                "Original v2 save retains the first dock landing but not temporary line-control state");
        }

        public static void MistportTidalFishingReducedMotionKeepsRealBitesAndNeverDuplicatesFirstReward(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");
            g.Enter(5);g.enemies.Clear();g.save.totalTime=36f;
            WorldObject pier=Object(g,"tidefishery");
            g.save.x=pier.x;g.save.y=pier.y;
            g.save.reducedMotion=true;g.save.inventory[3]=0;
            g.save.inventory[4]=2;
            g.Talk(pier);
            int action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(g.mode=="fish"&&g.FishTidalCast&&g.FishTarget==.5f,
                "Accessible tide challenge has a steady zone but retains the original physical cast");
            for(int n=0;n<18;n++)g.Tick(.02f,new Point());
            float time=g.FishBiteTimeRemaining;
            Check(g.FishLineReady&&!g.CanBraceTidalLine&&!g.BraceTidalLine()&&
                Math.Abs(g.FishBiteTimeRemaining-time)<.0001f,
                "Reduced motion never offers a deceptive time-wasting brace");
            int coins=g.save.coins,shards=g.save.shards;
            for(int i=0;i<3;i++){ReachFishingBite(g,true);g.FishHit();}
            Check(g.mode=="world"&&g.save.inventory[3]==3&&
                g.save.coins==coins+12&&g.save.shards==shards+1,
                "Steady accessible mode still requires and rewards three actual hooked bites");
            int momentCount=g.save.moments.Count;
            g.Talk(pier);
            action=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_tide");
            g.Choose(action);
            Check(g.mode=="fish"&&g.save.inventory[4]==0,
                "A new low-tide cast costs a second real gear component");
            for(int i=0;i<3;i++){ReachFishingBite(g,true);g.FishHit();}
            Check(g.save.inventory[3]==6&&g.save.coins==coins+24&&
                g.save.shards==shards+1&&g.save.moments.Count==momentCount,
                "Repeat catches remain useful but cannot duplicate the unique shard or civic chronicle");
        }

        public static void PrecisionLakeFishingPaysTwoOnlyForThreeGenuineTightHooks(Campaign data)
        {
            var g=Fresh(data);
            WorldObject pond=Object(g,"fish");
            g.save.x=pond.x;g.save.y=pond.y;
            g.Talk(pond);
            int expertIndex=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fish_expert");
            Check(expertIndex>=0&&g.conversation.choices[expertIndex].en.Contains("two fish"),
                "Actual fisherman offers the optional expert cast in the real world conversation");
            g.Choose(expertIndex);
            Check(g.mode=="fish"&&g.FishExpertCast&&g.FishCatchReward==2&&
                Math.Abs(g.FishWindowRadius-.10f)<.0001f,
                "Precision reel is actually narrower than the historical beginner fishing window");
            int fish=g.save.inventory[3],coins=g.save.coins;
            for(int i=0;i<3;i++)
            {
                ReachFishingBite(g,true);
                Check(g.FishLineReady&&g.FishWindowRadius<=.10f,
                    "Each expert hook still requires a live bite and a narrow physical cursor contact");
                g.FishHit();
                if(i<2)
                    Check(g.mode=="fish"&&!g.FishLineReady&&g.fishHits==i+1,
                        "A single expert bite never grants multiple fish or bypasses the recovery gap");
            }
            Check(g.mode=="world"&&!g.FishExpertCast&&
                g.save.inventory[3]==fish+2&&g.save.coins==coins+8&&
                g.save.version==2,
                "Three genuine expert hooks pay two physical Silverfin and eight coins without schema changes");
            g.FishHit();
            Check(g.save.inventory[3]==fish+2,
                "Replayed reel input after success cannot double the expert payout");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(!loaded.FishExpertCast&&loaded.save.inventory[3]==fish+2&&
                loaded.save.version==2,
                "The earned fish survives v2 reload but the transient expert challenge does not");
        }

        public static void PrecisionLakeFishingRespectsBagFailuresAndOrdinaryCasting(Campaign data)
        {
            var g=Fresh(data);g.save.inventory[3]=9998;
            WorldObject pond=Object(g,"fish");
            g.save.x=pond.x;g.save.y=pond.y;
            g.Talk(pond);
            int expert=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fish_expert");
            Check(expert>=0&&g.conversation.choices[expert].en.Contains("two slots"),
                "One free fish slot disables the two-fish challenge honestly in the fisherman menu");
            g.Choose(expert);
            Check(g.mode=="world"&&g.save.inventory[3]==9998&&
                !g.FishExpertCast,
                "A cached expert button cannot start with insufficient physical inventory space");
            g.Talk(pond);int ordinary=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fish");
            Check(ordinary>=0,"Ordinary original lake gameplay remains selectable");
            g.Choose(ordinary);
            Check(g.mode=="fish"&&!g.FishExpertCast&&g.FishCatchReward==1&&
                Math.Abs(g.FishWindowRadius-.18f)<.0001f,
                "Existing normal reel retains exactly its historic timing window and one-fish reward");
            g.Close();
            Check(!g.FishExpertCast,"Leaving a cast clears transient expert status");
            g.save.inventory[3]=0;g.Talk(pond);
            expert=Array.FindIndex(g.conversation.choices,c=>c.action=="fish_expert");
            g.Choose(expert);
            Check(g.FishExpertCast,"Second cast may legitimately elect precision mode");
            for(int i=0;i<3;i++)
            {
                ReachFishingBite(g,false);g.FishHit();
            }
            Check(g.mode=="world"&&!g.FishExpertCast&&
                g.save.inventory[3]==0,
                "Three real missed precision pulls lose the challenge without minting items");
            g.Talk(pond);
            ordinary=Array.FindIndex(g.conversation.choices,c=>c.action=="fish");
            g.Choose(ordinary);
            Check(!g.FishExpertCast&&g.mode=="fish"&&
                Math.Abs(g.FishWindowRadius-.18f)<.0001f,
                "Expert failure never makes the subsequent ordinary fishing cast harder");
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


        public static void LakeFisherDialogueHonorsLiveCommissionAndFullBag(Campaign data)
        {
            var g=Fresh(data);
            WorldObject pond=Object(g,"fish");
            g.enemies.Clear();
            g.save.x=pond.x;g.save.y=pond.y;
            QuestGuide empty=g.GetQuest("ally2");
            Check(!empty.complete&&empty.description.Contains("及时收线")&&
                empty.reward.Contains("40硬币")&&empty.reward.Contains("技能点1"),
                "The tracked town request explains the real bite clock and exact old one-time rewards");
            g.Talk(pond);
            Check(g.conversation!=null &&
                g.conversation.text.Contains("三条银鳍鱼")&&
                g.conversation.textEn.Contains("three Silverfin")&&
                g.conversation.text.Contains("听见鱼咬钩")&&
                !g.conversation.textEn.Contains("take your time"),
                "The actual lake fisher gives story-grounded timed-bite advice before the commission");
            g.Close();
            g.save.inventory[3]=9999;
            g.Talk(pond);
            Check(g.conversation.choices[0].action=="fish" &&
                g.conversation.choices[0].en.Contains("full") &&
                g.conversation.text.Contains("三条都齐了"),
                "A full fish bag preserves the cast action and still offers the existing three-fish delivery");
            g.Close();
            g.save.inventory[3]=3;
            QuestGuide filled=g.GetQuest("ally2");
            Check(filled.description.Contains("三条银鳍鱼")&&
                filled.next.Contains("交付"),
                "Quest journal recognizes that three physical fish are ready for delivery");
            g.Talk(pond);
            Check(g.conversation.text.Contains("都齐了") &&
                g.conversation.textEn.Contains("All three?"),
                "Bringing the requested fish changes the real fisher's dialogue before handoff");
            int turnIn=Array.FindIndex(g.conversation.choices,c=>c.action=="ally2");
            Check(turnIn>=0,"The original companion/aid commission remains physically available");
            int beforeCoins=g.save.coins;
            g.Choose(turnIn);
            Check(g.Has("ally_2")&&g.save.inventory[3]==0&&
                g.save.coins==beforeCoins+40,
                "Existing fish commission pays exactly once and consumes exactly three real catches");
            QuestGuide done=g.GetQuest("ally2");
            Check(done.complete&&done.description.Contains("吃上了一顿晚饭"),
                "Finished fish commission journal reflects the actual fulfilled shelter meal");
            g.Close();g.Talk(pond);
            Check(g.conversation.text.Contains("孩子们昨晚") &&
                g.conversation.textEn.Contains("fish stew last night")&&
                Array.FindIndex(g.conversation.choices,c=>c.action=="ally2")<0,
                "Completed commission acknowledges the actual children and cannot be paid twice");
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
