using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Runs against the real Campaign.json and production collision,
        // traversal, actor placement and original chapter sources.
        public static void RegionalCombatDevicesChangeRealEncountersAndPreserveSaves(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(clear!=null,"Original world collision is used for battlefield counterplay");
            for(int room=1;room<=4;room++)
            {
                g.Enter(room);g.enemies.Clear();
                WorldObject site=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(site!=null&&PlaceWithinTalkReach(g,site),
                    "Each real chapter has a reachable, unique environmental device");
                Point origin=new Point(site.x,site.y),foot=new Point();
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point candidate=new Point(origin.x+dx,origin.y+dy);
                    float dist=(candidate-origin).Length;
                    if(dist<2.5f||dist>5.7f||!g.CanStand(candidate)||
                        !(bool)clear.Invoke(g,new object[]{origin,candidate}))continue;
                    foot=candidate;found=true;
                }
                Check(found,"The placed device has real visible, legal attacker feet in the original level");
                g.save.energy=90f;
                g.Talk(site);
                int action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"The genuine map object offers a playable interaction");
                g.Choose(action);
                Check(g.save.energy==90f&&g.FieldTacticCooldown(site)==0f&&
                    !g.Has("field_tactic_used_"+room),
                    "No enemy means no phantom cost or mastery reward");
                Enemy enemy=new Enemy(93000+room,room==3?2:room==4?1:0,
                    foot.x,foot.y,100f);
                g.enemies.Add(enemy);g.Talk(site);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==76f&&g.FieldTacticCooldown(site)>23f&&
                    g.Has("field_tactic_used_"+room)&&enemy.hp==100f,
                    "Actual line-of-sight contact spends 14 energy and persists only genuine first use");
                if(room==1)Check(enemy.stun>=2f&&enemy.stagger>=1f,
                    "Root bell provides a real long snare");
                if(room==2)Check(enemy.stun>=.9f&&g.CanStand(enemy.Position),
                    "Tidal push cannot throw enemies into blocked world");
                if(room==3)Check(enemy.armorOpenTime>=3f,
                    "Mining vent opens actual armor, not a UI label");
                if(room==4)Check(enemy.stun>=2f,
                    "Citadel breaker interrupts the living archer");
                g.Talk(site);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==76f,
                    "Cooldown rejects duplicate charge without extra costs");
            }
            Check(g.FieldTacticMasteryCount==4&&
                g.Has("field_tactics_mastered"),
                "Four distinct physical combats earn one total four-region milestone");
            int talent=g.save.skillPoints;
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.FieldTacticMasteryCount==4&&
                restored.Has("field_tactics_mastered")&&
                restored.save.skillPoints==talent,
                "Authored combat consequences persist in ordinary v2 save flags");
        }

        // A mechanism can be reachable yet useless if every actual campaign
        // soldier spawns in a different encounter district. Exercise the
        // unchanged 6/8-enemy rosters, not a hand-placed test enemy.
        public static void FourChapterTacticsOccupyLiveBattleLanes(Campaign data)
        {
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;g.Enter(room);
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(device!=null&&g.CanStand(new Point(device.x,device.y)),
                    "Each chapter tactical device stands on actual walkable ground");
                Check(g.NavigationPathFrom(g.Position,
                    new Point(device.x,device.y),true).Count>0,
                    "The original chapter entrance has a real route to its combat mechanism");
                foreach(WorldObject prop in g.Map.objects)
                    if(!System.Object.ReferenceEquals(prop,device))
                        Check((new Point(prop.x,prop.y)-
                            new Point(device.x,device.y)).Length>=3f,
                            "A usable device needs its own interaction clearing");
                Check(g.FieldTacticLiveThreatCount(device)>=1,
                    "The natural required chapter roster must provide real visible targets for the placed mechanism");
                g.enemies.Clear();
                Check(g.FieldTacticLiveThreatCount(device)==0,
                    "An empty battlefield cannot display phantom viable targets");
            }
        }

        // These are accepted quest/resource objects with real E hitboxes,
        // not background scenery. Preserve every identity and story handler
        // while separating formerly overlapping map sources.
        public static void ChapterEvidenceAndResourcesHaveDistinctInteractionPads(Campaign data)
        {
            int[] rooms={2,2,3,3,4,4};
            string[] moved={"archivefragment2","afterlightoutlet2",
                "wood3","ore3","lastDirectiveEvidence0","annex_camp_4"};
            string[] neighbor={"survey2","stormrelay2",
                "witnessnpc3","rootcounter3","ridge_route_4_1",
                "silentledger_record"};
            for(int i=0;i<moved.Length;i++)
            {
                GameCore g=Fresh(data);g.save.stage=3;g.Enter(rooms[i]);
                WorldObject a=Array.Find(g.Map.objects,o=>o.id==moved[i]);
                WorldObject b=Array.Find(g.Map.objects,o=>o.id==neighbor[i]);
                Check(a!=null&&b!=null,
                    "Existing story and resource targets retain their exact authored identities");
                Point foot=new Point(a.x,a.y),other=new Point(b.x,b.y);
                Check(g.CanStand(foot),
                    "Repositioned object must be on actual legal game terrain: "+a.id);
                Check((foot-other).Length>=2.5f,
                    "Nearby quest sources need individually readable E interaction clearings: "+a.id);
            }
        }

        // Different farshore climates must change live roles, not only
        // background colors. Retain exactly seven correctly placed combatants.
        public static void FarshoreOpeningBattlesHaveDistinctBiomeRoles(Campaign data)
        {
            int[][] roles={
                new[]{1,0,1,0,2,1,0},
                new[]{2,1,2,0,1,2,0},
                new[]{0,0,2,0,1,2,0}
            };
            var signatures=new HashSet<string>();
            for(int room=5;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);
                Check(g.enemies.Count==7,
                    "Distinct outer biome compositions never alter required seven-enemy roster");
                string identity="";
                for(int slot=0;slot<7;slot++)
                {
                    Enemy foe=g.enemies[slot];
                    Check(foe.kind==roles[room-5][slot]&&
                        foe.hp>0&&!foe.boss&&g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                        "Real room "+room+" role "+slot+" must occupy walkable old progression lane");
                    identity+=foe.kind.ToString();
                    for(int prev=0;prev<slot;prev++)
                        Check((foe.Position-g.enemies[prev].Position).Length>=2.45f,
                            "Distinct biome fighters cannot collapse onto each other");
                }
                Check(signatures.Add(identity),
                    "Three expedition terrains should produce three different combat role profiles");
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(device!=null&&g.FieldTacticLiveThreatCount(device)>0,
                    "Distinct biome formations leave real enemies in reach of their unique tactical device");
                g.PrepareSave();
                Check(g.save.version==2,
                    "Authored combat variety does not introduce a new save format");
            }
        }

        // Rendering a monster over a ruin, house, wreck or tall pine
        // looks like overlapping unfinished map props. The authored
        // regional battles must keep their unchanged live counts while
        // reserving the *visual* decoration feet, not block the tiles.
        public static void RegionalBattleFightersNeverSpawnInsideMajorDecor(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);
                int count=room<=2?6:room<=4?8:7;
                Check(g.enemies.Count==count,
                    "Visual readability cannot remove the existing mandatory actors");
                foreach(Enemy enemy in g.enemies)
                {
                    Check(g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(g.Position,enemy.Position,true).Count>0,
                        "New visual clearance must keep every foe on a genuine accessible road");
                    foreach(Decoration d in g.Map.decor)
                    {
                        float pad=0f;
                        switch(d.kind)
                        {
                            case "tree":case "snowpine":case "column":
                            case "obelisk":case "ruin":case "house":
                            case "lighthouse":case "tent":case "skiff":
                                pad=1.45f;break;
                            case "crystal":case "palm":case "boat":
                            case "barrel":pad=1.1f;break;
                        }
                        Check(pad==0f||
                            (enemy.Position-new Point(d.x,d.y)).Length>=pad,
                            "Room "+room+" battle actor must not appear inside "+d.kind+" at its physical visual foot");
                    }
                }
                Check(g.save.version==2,
                    "Decoration-aware encounter stage cannot migrate an old save");
            }
        }

        // Every region must spawn actual living actors on the original
        // walkable battlefield; only physical counterplay, Hit defeats
        // and source-bound signing may award the first-clear report.
        public static void SevenFieldTacticDrillsRequireRealCounterKillsAndReceipt(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Trials must use the original combat death pipeline");
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);g.enemies.Clear();
                WorldObject source=Array.Find(g.Map.objects,
                    o=>o.id=="field_tactic_"+room&&o.kind=="fielddevice");
                Check(source!=null&&PlaceWithinTalkReach(g,source),
                    "Region "+room+" has a physically interactable original device");
                Point hero=g.Position;
                g.Talk(source);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_begin");
                Check(begin>=0,"Region "+room+" genuinely offers its optional live combat drill");
                int coins=g.save.coins,shards=g.save.shards;
                g.Choose(begin);
                Check(g.FieldTacticTrialAt(source)&&
                    !g.FieldTacticTrialAt(new WorldObject{
                        id=source.id,kind=source.kind,x=source.x,y=source.y}),
                    "Only exact source-bound active mechanism can claim a live trial GameView status");
                Check(g.FieldTacticTrialActive&&
                    g.FieldTacticTrialRemaining==2&&g.enemies.Count==2&&
                    !g.FieldTacticTrialReady&&!g.FieldTacticTrialUsed,
                    "Region "+room+" spawns both real contracted actors");
                Enemy[] assigned=g.enemies.ToArray();
                foreach(Enemy foe in assigned)
                {
                    Check(g.IsFieldTacticTrialTarget(foe),
                        "Only authenticated living contracted actors receive the visible trial marker");
                    Check(foe.hp>0f&&!foe.boss&&g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(hero,foe.Position,true).Count>0&&
                        (foe.Position-new Point(source.x,source.y)).Length<=5.8f,
                        "Live trial attacker must be on authentic reachable counterplay ground");
                }
                g.save.energy=100f;
                Check(g.QuickActivateFieldTactic()&&g.FieldTacticTrialUsed&&
                    g.save.energy==86f&&g.FieldTacticTrialRemaining==2,
                    "Real tactical activation hits at least one assigned living raider");
                foreach(Enemy foe in assigned)
                    hit.Invoke(g,new object[]{foe,600f,false,null,null});
                Check(Array.TrueForAll(assigned,e=>!g.IsFieldTacticTrialTarget(e)),
                    "Defeated registered bodies must lose their live world target marker");
                Check(g.FieldTacticTrialReady&&
                    g.FieldTacticTrialRemaining==0&&!g.FieldTacticTrialDone(room),
                    "Real defeats earn a report opportunity, not immediate free loot");
                g.save.x=hero.x;g.save.y=hero.y;
                g.Close();g.Talk(source);
                int report=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_report");
                Check(report>=0,"The same physical source offers only its earned report");
                g.Choose(report);
                Check(!g.FieldTacticTrialAt(source),
                    "Source-bound GameView trial status clears immediately on signing");
                Check(g.FieldTacticTrialDone(room)&&
                    !g.FieldTacticTrialActive&&
                    g.save.coins>=coins+22+room*4&&
                    g.save.shards>=shards+1&&
                    g.GetQuest("tactic_trials").steps[room-1].done,
                    "Signed trial gives its one-time real reward and journal progress");
                int signedCoins=g.save.coins;
                g.Close();g.Talk(source);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_begin")<0&&
                    g.save.coins==signedCoins,
                    "Veteran trial receipt prevents repeat-farming its first clear");
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.FieldTacticTrialDone(room)&&
                    !restored.FieldTacticTrialActive&&
                    restored.GetQuest("tactic_trials").steps[room-1].done,
                    "A legacy v2 save retains only signed truth, not transient foes");
            }
        }

        // A signed field result is not a disposable modal. Protect the
        // LIVE contract during both a portal Enter and the quick town route;
        // after real settlement, normal travel remains possible.
        // A generic main-story marker cannot send a player across the
        // world while a physical local battle needs its counter, opponents
        // or signed return. Preserve their manual tracker across all steps.
        public static void FieldTacticTrialRequiresUsableEnergyAndReadyDevice(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(source!=null&&PlaceWithinTalkReach(g,source),
                "Preparation requires authentic reachable field equipment");
            g.save.energy=13f;
            g.Talk(source);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(action>=0,"The real source offers the usual trial dialogue");
            g.Choose(action);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0&&
                g.save.energy==13f&&!g.FieldTacticTrialDone(1),
                "Starting without actual counter energy spawns no raiders and spends nothing");
            g.save.energy=14f;
            g.Close();g.Talk(source);
            action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            g.Choose(action);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "At exact legal energy the real walkable drill is available");
            g.Close();g.Talk(source);
            action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(action>=0,"The physical station supports a legal trial abort");
            g.Choose(action);
            Check(!g.FieldTacticTrialActive&&!g.FieldTacticTrialDone(1),
                "Preparation and cancellation do not grant a receipt");
            GameCore prior=Fresh(data);prior.save.stage=3;prior.Enter(1);
            WorldObject device=Array.Find(prior.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(prior,device)&&
                prior.FieldTacticLiveThreatCount(device)>0,
                "Original required room roster exposes genuine enemies to the device");
            prior.save.energy=100f;
            Check(prior.QuickActivateFieldTactic()&&
                prior.FieldTacticCooldown(device)>23f,
                "A previous real battlefield activation imposes genuine source cooldown");
            prior.enemies.Clear();
            int energy=(int)prior.save.energy;
            prior.Close();prior.Talk(device);
            action=Array.FindIndex(prior.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(action>=0,"Even cooling equipment describes the trial option");
            prior.Choose(action);
            Check(!prior.FieldTacticTrialActive&&prior.enemies.Count==0&&
                prior.save.energy==energy&&!prior.FieldTacticTrialDone(1),
                "Cooldown prevents an unwinnable ambush without spending energy or signing completion");
        }

        public static void FieldTacticTrialWaypointsFollowTheRequiredBattleStage(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Battle navigation uses a real originally authored source");
            string manual=g.save.trackedQuest;
            g.Talk(device);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(start>=0,"The original source actually offers this trial");
            g.Choose(start);
            Point site=new Point(device.x,device.y);
            Check(g.FieldTacticTrialActive&&!g.FieldTacticTrialUsed&&
                (g.FieldTacticTrialGuideTarget-site).Length<.001f&&
                g.RecommendedContinuationGuide.id=="tactic_trials"&&
                (g.RecommendedContinuationGuide.target-site).Length<.001f&&
                g.save.trackedQuest==manual,
                "Before counterplay, the live guide points to the nearby device, not an arbitrary fighter");
            g.save.energy=100f;
            Check(g.QuickActivateFieldTactic()&&g.FieldTacticTrialUsed,
                "Real device activation advances the live trial stage");
            var foes=g.enemies.ToArray();
            Point nearest=foes[0].Position;
            if((foes[1].Position-g.Position).Length<
               (foes[0].Position-g.Position).Length)nearest=foes[1].Position;
            Check((g.FieldTacticTrialGuideTarget-nearest).Length<.001f&&
                (g.RecommendedContinuationGuide.target-nearest).Length<.001f,
                "After actual counterplay, navigation pursues the nearest living designated fighter");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Waypoint progression requires the real combat defeat");
            hit.Invoke(g,new object[]{foes[0],600f,false,null,null});
            Check((g.FieldTacticTrialGuideTarget-foes[1].Position).Length<.001f&&
                g.FieldTacticTrialRemaining==1,
                "After an authentic defeat the marker immediately follows the remaining fighter");
            hit.Invoke(g,new object[]{foes[1],600f,false,null,null});
            Check(g.FieldTacticTrialReady&&
                (g.FieldTacticTrialGuideTarget-site).Length<.001f&&
                (g.RecommendedContinuationGuide.target-site).Length<.001f&&
                g.save.trackedQuest==manual,
                "After clearing both actors, navigation leads to the signed return and does not hijack the manual tracker");
            g.Close();g.Talk(device);
            int sign=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_report");
            Check(sign>=0,"Actual source offers an earned return choice");
            g.Choose(sign);
            Check(!g.FieldTacticTrialActive&&
                g.save.trackedQuest==manual&&
                g.RecommendedContinuationGuide.id==manual,
                "Signing completes the temporary objective and restores the player's original quest");
        }

        public static void FieldTacticTrialsCannotBeLostToUnsignedTravel(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "Travel lock uses a genuine walkable device, not a synthetic flag");
            g.Talk(site);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"A chapter's real drill starts at its own station");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Travel test has living assigned opponents");
            g.Enter(0);
            Check(g.save.room==1&&g.FieldTacticTrialActive&&
                g.enemies.Count==2,
                "Direct region switch cannot delete live trial actors silently");
            g.ReturnToTown();
            Check(g.save.room==1&&g.FieldTacticTrialActive&&
                g.enemies.Count==2,
                "Fast-return path is equally protected from live-trial bypass");
            g.Close();g.Talk(site);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(abort>=0,"Physical device offers a voluntary release");
            g.Choose(abort);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0,
                "Explicit withdrawal truly retires both designated actors");
            g.Enter(0);
            Check(g.save.room==0&&g.save.version==2,
                "Region travel works normally after explicit cancellation");
        }

        public static void FieldTacticTrialWithoutCounterFailsCleanlyAndRestarts(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Failure test uses actual physical combat deaths");
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "Failure route starts at a real device");
            g.Talk(site);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Original source spawns a genuine two-foe trial");
            int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;
            foreach(Enemy e in g.enemies.ToArray())
                hit.Invoke(g,new object[]{e,600f,false,null,null});
            Check(!g.FieldTacticTrialUsed&&g.FieldTacticTrialRemaining==0&&
                !g.FieldTacticTrialReady,
                "Killing without using the field mechanism cannot earn a receipt");
            // Hit() deliberately freezes live world simulation for a
            // short, authored impact stop after each actual kill. Wait for
            // the resumed physical world ticks, not a fictional instant
            // cleanup during the first frozen 20ms frame.
            for(int frame=0;frame<12&&g.FieldTacticTrialActive;frame++)
                g.Tick(.05f,new Point());
            Check(!g.FieldTacticTrialActive,
                "No-counter trial must end promptly after real hit-stop resolves");
            Check(g.enemies.Count==0,
                "No-counter cleanup must retire only the two trial actors: remaining "+
                    g.enemies.Count);
            Check(!g.FieldTacticTrialDone(1),
                "Uncountered fighters cannot sign a saved trial receipt");
            Check(g.save.coins==coins,
                "Failed trial must award zero bonus coins: "+g.save.coins+" versus "+coins);
            Check(g.save.shards==shards,
                "Failed trial must award zero bonus shards: "+g.save.shards+" versus "+shards);
            Check(g.save.xp==xp,
                "Failed trial must award zero trial XP: "+g.save.xp+" versus "+xp);
            g.Close();g.Talk(site);
            begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"After clear failure, the same real trial can be restarted");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Restart produces exactly two fresh live combat opponents");
        }

        public static void FieldTacticTrialDisappearanceAndWithdrawalCannotClaimReward(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(source!=null&&PlaceWithinTalkReach(g,source),
                "An authentic chapter device supports contract cancellation");
            int coins=g.save.coins;
            g.Talk(source);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"Physical source offers a new real trial");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "The live contract owns two exact actors");
            g.enemies.RemoveAt(0);
            g.Tick(.02f,new Point());
            Check(!g.FieldTacticTrialActive&&!g.FieldTacticTrialDone(1)&&
                g.save.coins==coins,
                "Deleting an actor without genuine combat must void its claim");
            g.Close();g.Talk(source);
            begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"A failed drill may be physically retried");
            g.Choose(begin);
            g.Close();g.Talk(source);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(abort>=0,"Live trial supports voluntary source-bound withdrawal");
            g.Choose(abort);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0&&
                !g.FieldTacticTrialDone(1)&&g.save.coins==coins,
                "Withdrawal removes drill actors and yields no false rewards");
        }

        // The three outer regions are distinct optional battlefields, not
        // decoration pasted into the map. Check real story gates, original
        // seven-actor roster, E interaction and one-time saved mastery.
        public static void OuterExpeditionFieldDevicesControlRealBattleAndSaveReward(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.mode="world";
            g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            for(int room=5;room<=7;room++)
            {
                g.Enter(room);
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(g.save.room==room,"Outer region entry succeeds: "+room);
                Check(device!=null,"Outer region has its real placed device: "+room);
                Check(g.enemies.Count==7,
                    "Outer region "+room+" retains seven required fighters; got "+g.enemies.Count);
                Check(g.CanStand(new Point(device.x,device.y)),
                    "Outer region "+room+" mechanism is physically standable");
                Check(g.NavigationPathFrom(g.Position,
                    new Point(device.x,device.y),true).Count>0,
                    "Outer battlefield has real walkable approach from the story entrance");
                foreach(WorldObject other in g.Map.objects)
                    if(!System.Object.ReferenceEquals(device,other))
                        Check((new Point(other.x,other.y)-
                            new Point(device.x,device.y)).Length>=3f,
                            "Outer devices must not steal crowded NPC E interaction pads");
                Check(g.FieldTacticLiveThreatCount(device)>=1,
                    "Natural regional attackers can be genuinely caught by the device");
                g.enemies.Clear();
                Check(g.FieldTacticLiveThreatCount(device)==0&&
                    PlaceWithinTalkReach(g,device),
                    "An empty combat site stays reachable but reports no ghost threats");
                float energy=90f;g.save.energy=energy;
                g.Talk(device);
                int action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"The in-world outer device opens a real action");
                g.Choose(action);
                Check(g.save.energy==energy&&g.FieldTacticCooldown(device)==0f&&
                    !g.Has("field_tactic_used_"+room),
                    "Empty activation never consumes energy, cooldown or mastery");
                Point center=new Point(device.x,device.y),enemyFoot=new Point();
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Live projectile LOS remains available");
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point foot=center+new Point(dx,dy);
                    float distance=(foot-center).Length;
                    if(distance<2.5f||distance>5.7f||
                        !g.CanStand(foot)||
                        !(bool)clear.Invoke(g,new object[]{center,foot}))continue;
                    enemyFoot=foot;found=true;
                }
                Check(found,"Each outer device has an actual nearby walkable enemy pad");
                Enemy target=new Enemy(98000+room,room==5?1:room==6?2:0,
                    enemyFoot.x,enemyFoot.y,100f);
                g.enemies.Add(target);
                Check(g.FieldTacticLiveThreatCount(device)==1,
                    "Outer tactic only counts live physically visible attacker");
                g.Talk(device);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"Combat use offers the same real source-bound choice");
                g.Choose(action);
                Check(g.save.energy==energy-14f&&g.FieldTacticCooldown(device)>23f&&
                    g.Has("field_tactic_used_"+room)&&target.hp==100f,
                    "Physical outer counter spends energy and marks exactly one genuine use");
                if(room==5)Check(target.stun>=2.7f,
                    "Fog horn must genuinely interrupt a bowman");
                if(room==6)Check(target.armorOpenTime>=3.9f,
                    "Prism must genuinely expose armored combatants");
                if(room==7)Check(target.stun>=2.6f&&target.stagger>=1.6f,
                    "Cold bell must genuinely stop an advancing melee raider");
                g.Talk(device);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==energy-14f,
                    "An outer device cannot charge twice during cooldown");
                g.Close();
            }
            Check(g.OuterTacticMasteryCount==3&&
                g.Has("outer_field_tactics_mastered")&&
                g.save.skillPoints==1&&g.FieldTacticMasteryCount==0&&
                !g.Has("field_tactics_mastered"),
                "Separate three-region mastery never invents original chapter progress");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.OuterTacticMasteryCount==3&&
                restored.Has("outer_field_tactics_mastered")&&
                restored.save.skillPoints==1,
                "Existing v2 save flags persist expedition rewards without new fields");
        }

        // This is an actual gameplay action: Shift+G shares the E choice's
        // physical LOS, energy and cooldown transaction without a modal.
        public static void TacticalQuickUseSharesRealBattleCostsWithoutStealingOtherInteractions(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Quick tactic needs a real walkable reachable map object");
            g.enemies.Clear();g.save.energy=100f;
            Check(g.QuickActivateFieldTactic()&&g.mode=="world"&&
                g.save.energy==100f&&!g.Has("field_tactic_used_1"),
                "Quick use with no genuine foe costs nothing and cannot forge a first-use reward");
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(clear!=null,"Quick use shares the production LOS sampler");
            Point source=new Point(device.x,device.y),foot=new Point();
            bool found=false;
            for(int dy=-5;dy<=5&&!found;dy++)
            for(int dx=-5;dx<=5&&!found;dx++)
            {
                Point p=source+new Point(dx,dy);
                float length=(p-source).Length;
                if(length<2.5f||length>5.7f||!g.CanStand(p)||
                    !(bool)clear.Invoke(g,new object[]{source,p}))continue;
                foot=p;found=true;
            }
            Check(found,"Battlefield quick use can reach actual legal enemy feet");
            Enemy foe=new Enemy(99671,0,foot.x,foot.y,100f);
            g.enemies.Add(foe);
            Check(g.FieldTacticLiveThreatCount(device)==1,
                "The world prompt and quick-use target share the same real attacker");
            Check(g.QuickActivateFieldTactic()&&g.mode=="world"&&
                g.conversation==null&&g.save.energy==86f&&
                foe.stun>=2.29f&&g.FieldTacticCooldown(device)>23f&&
                g.Has("field_tactic_used_1"),
                "Quick activation genuinely ensnares the enemy without switching out of live gameplay");
            Check(g.QuickActivateFieldTactic()&&g.save.energy==86f&&
                g.FieldTacticMasteryCount==1,
                "Cooldown never double-charges or duplicates first use");
            g.save.x=source.x+12f;g.save.y=source.y;
            Check(!g.QuickActivateFieldTactic()&&g.save.energy==86f,
                "Distant input cannot operate a mechanism through a false remote target");
        }

        // Crowded NPC interaction is deliberately independent of the
        // dedicated field-device shortcut. A focused NPC must not hide an
        // authentic reachable contraption or have its E selection stolen.
        public static void QuickTacticsWorkBesideFocusedNpcWithoutHijackingE(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Tactical focus test uses live reachable field mechanism");
            Point player=g.Position;
            MapData map=g.Map;
            WorldObject[] before=map.objects;
            var neighbor=new WorldObject{
                id="test_nearby_civilian",kind="campfire",name="紧邻的居民",
                en="Nearby civilian",x=player.x,y=player.y
            };
            try
            {
                var extended=new WorldObject[before.Length+1];
                Array.Copy(before,extended,before.Length);
                extended[before.Length]=neighbor;
                map.objects=extended;
                int index=g.NearbyInteractionChoices().IndexOf(neighbor);
                Check(index>=0&&g.FocusNearbyInteraction(index)&&
                    System.Object.ReferenceEquals(g.Nearest(),neighbor),
                    "Crowded live scene preserves explicit player choice of the nearer NPC");
                g.enemies.Clear();
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Real attack LOS verifies crowded tactic enemy");
                Point center=new Point(device.x,device.y),foot=new Point();
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point p=center+new Point(dx,dy);
                    float distance=(p-center).Length;
                    if(distance<2.5f||distance>5.7f||!g.CanStand(p)||
                       !(bool)clear.Invoke(g,new object[]{center,p}))continue;
                    foot=p;found=true;
                }
                Check(found,"Focused civilian does not block genuine combat line");
                Enemy foe=new Enemy(99731,0,foot.x,foot.y,85f);
                g.enemies.Add(foe);g.save.energy=90f;
                Check(g.QuickActivateFieldTactic()&&g.save.energy==76f&&
                    foe.stun>=2.29f&&g.Has("field_tactic_used_1")&&
                    g.mode=="world",
                    "Shift+G fires real adjacent mechanism despite closer selected NPC");
                Check(System.Object.ReferenceEquals(g.Nearest(),neighbor),
                    "Quick tactics never replace the explicit NPC E interaction focus");
                int energy=(int)g.save.energy;
                Check(g.QuickActivateFieldTactic()&&g.save.energy==energy,
                    "Shift+G still honors the shared tactical cooldown");
                g.save.room=0;
                Check(!g.QuickActivateFieldTactic()&&g.save.energy==energy,
                    "Shortcut rejects foreign-room stale objects");
            }
            finally
            {
                map.objects=before;
            }
        }

        public static void FarshoreTacticsJournalRoutesOnlyThroughUnlockedRealDevices(Campaign data)
        {
            GameCore g=Fresh(data);
            QuestGuide closed=g.GetQuest("outer_tactics");
            Check(closed.id=="outer_tactics"&&closed.locked&&
                !closed.complete&&closed.steps.Count==3,
                "Unintroduced farshore adventure is available in the journal but cannot forge travel");
            g.TrackQuest("outer_tactics");
            Check(g.save.trackedQuest!="outer_tactics",
                "New game cannot pin a still-locked farshore route");
            g.save.stage=3;g.save.flags.Add("exp_intro");
            QuestGuide open=g.GetQuest("outer_tactics");
            WorldObject harbor=Array.Find(data.maps[5].objects,
                o=>o.id=="field_tactic_5");
            Check(!open.locked&&open.room==5&&harbor!=null&&
                (open.target-new Point(harbor.x,harbor.y)).Length<.001f,
                "Open harbor guides to the actual unique coastal combat object");
            g.TrackQuest("outer_tactics");
            Check(g.save.trackedQuest=="outer_tactics",
                "Player can voluntarily track real farshore devices");
            g.save.flags.Add("field_tactic_used_5");
            QuestGuide waiting=g.GetQuest("outer_tactics");
            QuestGuide story=g.GetQuest("shore");
            Check(waiting.room==story.room&&waiting.steps[0].done&&
                !waiting.steps[1].done&&!waiting.complete,
                "Locked desert sends the player through the genuine shore story instead of an invalid portal");
            g.save.flags.Add("harbor_report");
            QuestGuide desert=g.GetQuest("outer_tactics");
            WorldObject prism=Array.Find(data.maps[6].objects,
                o=>o.id=="field_tactic_6");
            Check(desert.room==6&&prism!=null&&
                (desert.target-new Point(prism.x,prism.y)).Length<.001f,
                "Harbor report opens the actual desert prism quest marker");
            g.save.flags.Add("field_tactic_used_6");
            g.save.flags.Add("desert_report");
            QuestGuide frost=g.GetQuest("outer_tactics");
            WorldObject stillbell=Array.Find(data.maps[7].objects,
                o=>o.id=="field_tactic_7");
            Check(frost.room==7&&stillbell!=null&&
                (frost.target-new Point(stillbell.x,stillbell.y)).Length<.001f,
                "Desert report opens the actual Frostchime tactical marker");
            g.save.flags.Add("field_tactic_used_7");
            QuestGuide finished=g.GetQuest("outer_tactics");
            Check(finished.complete&&!finished.locked&&
                finished.Progress==1f&&finished.steps.Count==3&&
                g.save.trackedQuest=="outer_tactics",
                "All physical field successes close the journal route without hijacking the tracked quest");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.GetQuest("outer_tactics").complete&&
                restored.save.trackedQuest=="outer_tactics",
                "The existing v2 save preserves the player's selected farshore quest and completed steps");
        }

        public static void FourDifferentCampaignLevelsRetainRealOldAndNewWalkingRoutes(Campaign data)
        {
            string[][] originalMustReach={
                new[]{"chapterroad_1_0","chapterroad_1_1","chapterroad_1_2",
                    "scout","wayfarer1","witnessnpc1","wood1","shrine1",
                    "waytrail1e","rootsong_1","outer_watch_1","far_vista_1"},
                new[]{"chapterroad_2_0","chapterroad_2_1","chapterroad_2_2",
                    "rune0","rune1","rune2","wayfarer2","shrine2",
                    "waytrail2e","floodarchive_1","floodarchive_2","outer_watch_2"},
                new[]{"chapterroad_3_0","chapterroad_3_1","chapterroad_3_2",
                    "engine","witnessnpc3","ironfoundry","shrine3",
                    "pressureworks_notice","pressureworks_valve1",
                    "pressureworks_valve2","pressureworks_engine"},
                new[]{"chapterroad_4_0","chapterroad_4_1","chapterroad_4_2",
                    "triadnexus","blackoutshelter","shrine4",
                    "waytrail4e","bridge_heart","silentledger_notice",
                    "silentledger_console","outer_watch_4"}
            };
            var levelSignatures=new HashSet<string>();
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.Enter(room);
                Check(g.save.room==room&&g.CanStand(g.Position)&&
                    g.Map.rows.Length==80&&g.Map.rows[0].Length==120,
                    "Every original chapter still enters its authored 120 by 80 game world");
                Check(g.enemies.Count==(room<=2?6:8),
                    "Original mandatory live combat roster survives authored level redesign");
                // Distinct original map geography now supports distinct
                // real combat roles, instead of the same 0/1/2 pattern.
                int[][] chapterKinds={
                    new[]{0,0,1,0,1,0},
                    new[]{1,0,1,2,1,0},
                    new[]{2,0,2,1,2,0,2,1},
                    new[]{2,1,0,2,1,0,2,1}
                };
                int rosterIndex=0;
                foreach(Enemy foe in g.enemies)
                {
                    Check(rosterIndex<chapterKinds[room-1].Length&&
                        foe.kind==chapterKinds[room-1][rosterIndex],
                        "Different terrain now produces a deliberately distinct live chapter battle composition");
                    rosterIndex++;
                }
                Check(rosterIndex==chapterKinds[room-1].Length,
                    "Biome-specific tactical variety preserves the mandatory six/eight-actor campaign roster");
                Point regionSpawn=g.Position;
                foreach(Enemy enemy in g.enemies)
                {
                    Check(enemy.hp>0&&g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(regionSpawn,enemy.Position,true).Count>0,
                        "All original mandatory enemies occupy connected, physically walkable regional ground");
                    foreach(WorldObject prop in g.Map.objects)
                    {
                        float pad=(enemy.Position-new Point(prop.x,prop.y)).Length;
                        Check(pad>=1.9f,
                            "Regional fighters do not block original NPC, resource or chapter investigation props");
                        if(prop.kind=="chapterroad")
                            Check(pad>=4.15f,
                                "Mandatory regional monsters cannot camp a chapter rescue or survivor conversation");
                    }
                }
                // The original seal/barrier prerequisites still control the
                // Boss arena. Decorative new paths must not bypass them.
                if(room<=3)
                {
                    Check(!g.GateOpen,"Reauthored world retains an authentic closed chapter gate");
                    g.save.flags.Add(room==1?"scout_saved":"gate_"+room);
                    Check(g.GateOpen,"Existing old-story solution still unlocks the same guardian");
                }
                foreach(string id in originalMustReach[room-1])
                {
                    WorldObject point=Array.Find(g.Map.objects,o=>o.id==id);
                    Check(point!=null,"No accepted original or new map object was removed: "+id);
                    Point foot=new Point(point.x,point.y);
                    Check(g.CanStand(foot),
                        "Authored "+g.Map.name+" landmark is not buried in tree, flood or slag: "+id);
                    Check(g.NavigationPathFrom(regionSpawn,foot,true).Count>0,
                        "Real navigation must connect the original region entrance to "+id);
                }
                // Every chapter rescue is a real three-location journey.
                for(int n=0;n<2;n++)
                {
                    WorldObject a=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+n);
                    WorldObject b=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+(n+1));
                    Check(a!=null&&b!=null&&
                        (new Point(a.x,a.y)-new Point(b.x,b.y)).Length>=4f,
                        "Original civilian stories visit separate, authored physical locations");
                    Check(g.NavigationPathFrom(new Point(a.x,a.y),
                        new Point(b.x,b.y),true).Count>0,
                        "New cover must not make the actual three-stop rescue impossible");
                }
                // The four places must not be the same atlas recolored.
                // Compare a small and remote zone's real terrain pattern.
                string signature="";
                for(int y=4;y<=21;y++)
                    signature+=g.Map.rows[y].Substring(4,30);
                for(int y=55;y<70;y+=3)
                    signature+=g.Map.rows[y].Substring(80,28);
                Check(levelSignatures.Add(signature),
                    "Each chapter has unique traversable geography across the early and remote world");
            }
            // Keep the thematic changes literal, not four generic biome
            // recolors: trees, flooded islands, slag, and fortress walls.
            Check(data.maps[1].rows[4][25]=='#' &&
                data.maps[2].rows[5][24]=='~' &&
                data.maps[3].rows[5][24]=='#' &&
                data.maps[4].rows[20][30]=='#',
                "Forest roots, Ruins flood, Mine slag and Citadel barricades have physically authored distinct topology");
        }
        // These are real signed, optional world contracts: spawn via
        // physical dialogue and check every living actor's actual class.
        // A static table-only assertion would not catch a disconnected
        // placement path, an unspawned third foe, or a dead world hook.
        public static void SevenFieldPatrolRegionsHaveDifferentPhysicalCombatRoles(Campaign data)
        {
            int[][] expected={
                new[]{0,0}, new[]{1,1,0}, new[]{2,0,2},
                new[]{2,1}, new[]{0,1}, new[]{0,2,1},
                new[]{1,2}
            };
            var signatures=new HashSet<string>();
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);
                g.save.room=room;g.mode="world";g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                WorldObject post=Array.Find(g.Map.objects,o=>
                    o.kind=="biomeSurvey"&&o.id=="biomeSurvey"+room);
                Check(post!=null&&PlaceWithinTalkReach(g,post),
                    "Every district retains its physically accessible survey post");
                Point hero=g.Position;
                g.Talk(post);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "The live regional post actually opens the contract");
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(begin>=0,"Every original field contract is playable");
                g.Choose(begin);
                Check(g.FieldPatrolActive&&
                    g.FieldPatrolRemaining==expected[room-1].Length&&
                    g.enemies.Count==expected[room-1].Length,
                    "The complete authored tactical wave enters the live world");
                string signature="";
                for(int slot=0;slot<g.enemies.Count;slot++)
                {
                    Enemy e=g.enemies[slot];
                    Check(e.kind==expected[room-1][slot]&&
                        !e.boss&&e.hp>0f&&g.CanStand(e.Position)&&
                        g.NavigationPathFrom(hero,e.Position,true).Count>0,
                        "A district's actual combat role must occupy walkable connected ground");
                    signature+=e.kind.ToString();
                }
                Check(signatures.Add(signature),
                    "All seven contracts must present distinct tactical role sequences");
                Check(g.save.version==2&&!g.FieldPatrolDone(room),
                    "Starting optional combat does not alter legacy saves or sign rewards");
            }
        }

        public static void ThreeOriginalFarshoreHuntsHaveRealTwoWaveCombatAndMoralOutcomes(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Live combat HP/death owns farshore hunt victories");
            int[,] expected={{1,0,2,0},{2,1,1,2},{0,2,2,1}};
            var coordinates=new HashSet<string>();
            for(int room=5;room<=7;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();g.invulnerable=200f;
                // Live farshore progression, not an impossible save with
                // privileged room coordinates but missing portal credentials.
                g.save.flags.Add("exp_intro");
                if(room>=6)g.save.flags.Add("harbor_report");
                if(room>=7)g.save.flags.Add("desert_report");
                WorldObject source=Array.Find(g.Map.objects,o=>
                    o.id=="wilderness_hunt_"+room);
                Check(source!=null&&source.kind=="wildernessHunt"&&
                    coordinates.Add(source.x+","+source.y),
                    "Each farshore map has its own newly authored physical hunt site");
                Check(g.CanStand(new Point(source.x,source.y))&&
                    g.NavigationPathFrom(new Point(g.Map.spawnX,g.Map.spawnY),
                        new Point(source.x,source.y),true).Count>0,
                    "Original playable map actually connects its distant hunt clearing");
                foreach(WorldObject old in g.Map.objects)
                    if(old!=source)
                        Check((new Point(old.x-source.x,old.y-source.y)).Length>=10f,
                            "Hunts occupy remote unused level ground, not existing quest kiosks");
                Check(PlaceWithinTalkReach(g,source),
                    "The real distant marker has a physically reachable interaction foot");
                g.Talk(source);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="wilderness_hunt_bold");
                Check(begin>=0&&!g.WildernessHuntActive&&
                    !g.Has("wilderness_hunt_fought_"+room),
                    "Resident evidence offers a voluntary hunt, not auto-completion on discovery");
                int supply=room==5?0:room==6?4:2;
                g.save.inventory[supply]=2;
                int before=g.save.inventory[supply];
                g.Choose(begin);
                Check(g.WildernessHuntActive&&g.WildernessHuntWave==1&&
                    g.WildernessHuntRemaining==2&&
                    g.save.inventory[supply]==before&&
                    !g.Has("wilderness_hunt_fought_"+room),
                    "Open pursuit spawns two real raiders without charging ambush materials or awarding unearned progress");
                for(int wave=1;wave<=2;wave++)
                {
                    Enemy[] attackers=g.enemies.ToArray();
                    Check(attackers.Length==2,
                        "Each of the three authored regional encounters has two live foes");
                    for(int i=0;i<2;i++)
                    {
                        Enemy e=attackers[i];
                        Check(e.kind==expected[room-5,(wave-1)*2+i]&&
                            !e.boss&&e.hp>0&&g.CanStand(e.Position)&&
                            g.NavigationPathFrom(new Point(source.x,source.y),
                                e.Position,true).Count>0,
                            "Biome-specific combat rosters occupy real actor-sized connected terrain");
                        hit.Invoke(g,new object[]{e,9999f,true,null,null});
                    }
                    Advance(g,.65f);
                    if(wave==1)
                        Check(g.WildernessHuntActive&&g.WildernessHuntWave==2&&
                            g.WildernessHuntRemaining==2&&
                            !g.Has("wilderness_hunt_fought_"+room),
                            "Only both real contact deaths open the second regional hunting wave");
                }
                Check(!g.WildernessHuntActive&&
                    g.Has("wilderness_hunt_fought_"+room)&&
                    !g.WildernessHuntFinished(room),
                    "Defeating four genuine attackers still requires the civilian decision at source");
                Check(PlaceWithinTalkReach(g,source),
                    "Return on walkable terrain to the same original marker to publish the aftermath");
                g.Talk(source);
                int resolve=Array.FindIndex(g.conversation.choices,
                    c=>c.action== (room==6?
                        "wilderness_hunt_bounty":"wilderness_hunt_refuge"));
                Check(resolve>=0,"Real authored post-hunt choices are reachable");
                int oldCoins=g.save.coins,oldHerbs=g.save.inventory[2];
                g.Choose(resolve);
                bool refuge=room!=6;
                Check(g.WildernessHuntFinished(room)&&
                    g.Has((refuge?"wilderness_hunt_refuge_":
                        "wilderness_hunt_bounty_")+room)&&
                    g.Has("wilderness_hunt_open_"+room)&&
                    g.save.coins>=oldCoins+(refuge?32:70)+12&&
                    (!refuge||g.save.inventory[2]==oldHerbs+2),
                    "One chosen outcome grants the correct permanent civilian or watch benefit");
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.WildernessHuntFinished(room)&&
                    !restored.WildernessHuntActive,
                    "Original SaveData v2 round-trip keeps decisions but never resurrects encounter actors");
                Check(PlaceWithinTalkReach(restored,source),
                    "The old marker remains discoverable after a completed hunt");
                restored.Talk(source);
                Check(Array.FindIndex(restored.conversation.choices,
                    c=>c.action=="wilderness_hunt_bold")<0&&
                    Array.FindIndex(restored.conversation.choices,
                    c=>c.action=="wilderness_hunt_refuge")<0,
                    "Veteran saves cannot claim a second hunt or duplicate one-time rewards");
            }
            Check(coordinates.Count==3,"Three real distinct geography-specific hunt locations exist");
        }
        public static void FarshoreHuntAmbushRequiresSupplyAndRejectsPhantomDefeats(Campaign data)
        {
            GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=6;g.enemies.Clear();g.invulnerable=200f;
            WorldObject site=Array.Find(g.Map.objects,o=>o.id=="wilderness_hunt_6");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "The original desert hunt can be reached");
            g.save.inventory[4]=0;g.Talk(site);
            int ambush=Array.FindIndex(g.conversation.choices,
                c=>c.action=="wilderness_hunt_quiet");
            Check(ambush>=0,"Real hunt offers player a tactical paid ambush");
            g.Choose(ambush);
            Check(!g.WildernessHuntActive&&g.enemies.Count==0,
                "No local supply prevents both ghost enemy spawn and free opening");
            g.save.inventory[4]=2;g.Talk(site);
            ambush=Array.FindIndex(g.conversation.choices,
                c=>c.action=="wilderness_hunt_quiet");
            g.Choose(ambush);
            Check(g.WildernessHuntActive&&g.save.inventory[4]==1&&
                !g.Has("wilderness_hunt_open_6")&&
                g.WildernessHuntRemaining==2&&
                g.enemies.TrueForAll(e=>e.stun>=1.39f&&e.armorOpenTime>=1.09f),
                "Paying real desert gears gives a real temporary advantage to the first wave");
            // Removal without authentic hit-death hook is never a victory.
            g.enemies.RemoveAt(0);
            Advance(g,.2f);
            Check(!g.WildernessHuntActive&&g.enemies.Count==0&&
                !g.Has("wilderness_hunt_fought_6")&&
                !g.WildernessHuntFinished(6),
                "Vanished opponents retire every exclusively hunt-owned raider, without forging story progress");
        }
        public static void LegacyV2SavesInsideNewAuthoringCoverRecoverWithoutLosingCampaign(Campaign data)
        {
            // These four tiles were originally walkable empty ground. They
            // now form real roots, water, slag and an outer wall. A veteran's
            // saved character may stand on any of them: repair safely rather
            // than deleting the world, resetting the plot or moving to town.
            int[,] covers={{25,4},{24,5},{24,5},{30,20}};
            char[] terrain={'#','~','#','#'};
            for(int room=1;room<=4;room++)
            {
                GameCore old=Fresh(data);old.save.stage=3;
                old.save.version=2;old.save.room=room;
                old.save.x=covers[room-1,0]+.5f;
                old.save.y=covers[room-1,1]+.5f;
                old.save.health=52f;old.save.inventory[0]=5;
                old.save.flags.Add("chapter_road_done_"+room);
                // Only old SAVES that had already earned the original
                // gated area may legitimately resume behind that gate.
                if(room<=3)
                    old.save.flags.Add(room==1?"scout_saved":"gate_"+room);
                old.save.snapshotRoom=-1;old.save.worldEnemies=null;
                Check(data.maps[room].rows[covers[room-1,1]][covers[room-1,0]]==
                    terrain[room-1],
                    "The deliberately authored new local cover is genuinely impassable");
                GameCore recovered=new GameCore(data,CloneSave(old.save));
                Check(recovered.save.version==2&&recovered.save.room==room&&
                    recovered.CanStand(recovered.Position),
                    "Old v2 world spawn inside newly sculpted terrain recovers to authentic walkable ground");
                Check(recovered.Has("chapter_road_done_"+room)&&
                    recovered.save.stage==3&&recovered.save.inventory[0]==5&&
                    recovered.save.health==52f,
                    "Level remodeling does not erase earned civilian records, supply, health or stage from an old save");
                Check((recovered.Position-new Point(old.save.x,old.save.y)).Length<8f,
                    "One newly blocked tile relocates the returning traveler nearby, never across the 120x80 world");
                // A forged save inside a STILL-CLOSED Boss arena must not
                // select the nearest foothold if the physical gate forbids
                // the path from the original regional entry.
                if(room<=3)
                {
                    SaveData locked=CloneSave(old.save);
                    locked.flags.Remove(room==1?"scout_saved":"gate_"+room);
                    locked.x=29.5f;locked.y=8.5f;
                    GameCore shut=new GameCore(data,locked);
                    Check(!shut.GateOpen&&shut.CanStand(shut.Position),
                        "Recovery never bypasses the original locked Boss arena");
                    Check(shut.NavigationPathFrom(new Point(shut.Map.spawnX,
                        shut.Map.spawnY),shut.Position,true).Count>0,
                        "Recovered foot is on the truly accessible original side of a locked chapter");
                }
            }
        }
    }
}
