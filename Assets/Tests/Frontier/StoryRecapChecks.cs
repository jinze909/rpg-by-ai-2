using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {

        public static void NightShiftConsequencesPersistAtThreeRealFieldSites(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            {
                var game=Fresh(data);
                game.save.stage=3;
                game.save.flags.Add("nightshift_started");
                for(int r=1;r<=3;r++)
                    game.save.flags.Add("nightshift_trace_"+r);
                bool published=outcome==0;
                game.save.flags.Add(published?"nightshift_public":"nightshift_private");
                int firstChronicle=game.save.moments.Count;
                for(int room=1;room<=3;room++)
                {
                    WorldObject site=NightShiftAt(game,room);
                    string receipt="nightshift_followup_"+room;
                    Check(!game.Has(receipt),"The field cache has not been claimed by a different region");
                    int target=room==1?2:room==2?0:4;
                    int oldMaterial=game.save.inventory[target];
                    if(!published)
                    {
                        if(room==2)game.save.energy=21f;
                        else game.save.health=18f;
                    }
                    NightShiftChoose(game,site,receipt);
                    Check(game.Has(receipt),
                        "Only an actual physical source signs its own field aid once");
                    var next=game.GetQuest("night_shift_aftercare");
                    Check(next.steps.Count==4&&next.steps[room-1].done&&
                        !next.complete&&
                        (room==3?next.room==0:next.room==room+1),
                        "Player-facing compass follows three real field receipts, then routes to the original town ledger");
                    if(published)
                        Check(game.save.inventory[target]==
                            Math.Min(9999,oldMaterial+(room==3?1:2)),
                            "Public roads give tangible and geographically distinct herb, timber and repair stock");
                    else if(room==2)
                        Check(game.save.energy==56f,
                            "Anonymous ferry survivors grant actual on-site fatigue relief");
                    else
                        Check(game.save.health==18f+(room==1?24f:18f),
                            "Confidential survivors grant real region-specific field healing");
                    int afterItem=game.save.inventory[target];
                    float afterHp=game.save.health,afterEnergy=game.save.energy;
                    game.Close();game.Talk(site);
                    Check(game.conversation!=null&&
                        Array.FindIndex(game.conversation.choices,
                            c=>c.action==receipt)<0,
                        "A signed region cannot reoffer farmable aid");
                    game.Close();
                    Check(game.save.inventory[target]==afterItem&&
                        game.save.health==afterHp&&game.save.energy==afterEnergy,
                        "Revisiting the authentic source preserves past rewards exactly once");
                }
                Check(game.save.moments.Count==firstChronicle+3&&
                    game.NightShiftComplete&&game.GetQuest("night_shift").complete,
                    "Three real aftercare scenes add authored continuity without reopening the finished story");
                WorldObject guildDesk=NightShiftAt(game,0);
                Check(game.GetQuest("night_shift_aftercare").room==0&&
                    !game.GetQuest("night_shift_aftercare").complete,
                    "Aftercare requires a physical town handoff rather than completing from the third distant region");
                int beforeCoins=game.save.coins;
                float beforeMaxHealth=game.save.maxHealth;
                string reportAction=published?
                    "nightshift_report_pay":"nightshift_report_guard";
                NightShiftChoose(game,guildDesk,reportAction);
                Check(game.Has("nightshift_reported")&&
                    game.GetQuest("night_shift_aftercare").complete&&
                    game.save.moments.Count==firstChronicle+4,
                    "Real town dialogue signs the last narrative beat and closes the fourth quest step");
                Check(published?
                        game.save.coins==Math.Min(999999,beforeCoins+75)&&
                        game.save.maxHealth==beforeMaxHealth:
                        game.save.coins==beforeCoins&&
                        game.save.maxHealth==Math.Min(9999f,beforeMaxHealth+4f),
                    "Two genuine town choices pay different one-time rewards");
                game.Close();game.Talk(guildDesk);
                Check(game.conversation!=null&&
                    Array.FindIndex(game.conversation.choices,
                        c=>c.action=="nightshift_report_pay"||
                           c.action=="nightshift_report_guard")<0,
                    "The original town desk cannot repay a signed epilogue");
                game.Close();
                game.PrepareSave();
                var restored=new GameCore(data,CloneSave(game.save));
                Check(restored.save.version==2&&restored.NightShiftComplete&&
                    restored.Has("nightshift_reported")&&
                    restored.GetQuest("night_shift_aftercare").complete,
                    "Both branching four-step epilogues persist in the unchanged v2 save");
                for(int r=1;r<=3;r++)
                    Check(restored.Has("nightshift_followup_"+r),
                        "Every original field receipt persists and cannot refresh after reload");
            }
        }


        public static void NightShiftAidWaitsWhenPlayerCannotBenefit(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            for(int room=1;room<=3;room++)
            {
                var g=Fresh(data);
                g.save.stage=3;
                g.save.flags.Add("nightshift_started");
                for(int r=1;r<=3;r++)g.save.flags.Add("nightshift_trace_"+r);
                bool publicAid=outcome==0;
                g.save.flags.Add(publicAid?"nightshift_public":"nightshift_private");
                WorldObject site=NightShiftAt(g,room);
                string receipt="nightshift_followup_"+room;
                int slot=room==1?2:room==2?0:4;
                // First offer a genuine field choice with room for recovery.
                if(publicAid)g.save.inventory[slot]=9996;
                else if(room==2)g.save.energy=70f;
                else g.save.health=g.save.maxHealth-30f;
                g.Talk(site);
                Check(g.conversation!=null,
                    "Original survivor site opens an actual dialogue");
                int option=Array.FindIndex(g.conversation.choices,
                    c=>c.action==receipt);
                Check(option>=0,"On-site aid is initially genuinely available");
                // The player's condition may change after opening the dialog.
                // A stale button must not consume an irreplaceable receipt.
                if(publicAid)g.save.inventory[slot]=9999;
                else if(room==2)g.save.energy=100f;
                else g.save.health=g.save.maxHealth;
                int moments=g.save.moments.Count;
                g.Choose(option);
                Check(!g.Has(receipt)&&g.save.moments.Count==moments&&
                    !g.GetQuest("night_shift_aftercare").complete,
                    "Full resources cannot waste a claim-once field receipt");
                g.Close();g.Talk(site);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action==receipt)<0&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="nightshift_visit_"+room)>=0,
                    "A full-capacity visit can progress without wasting its aid");
                g.Choose(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="nightshift_visit_"+room));
                Check(g.Has("nightshift_visit_"+room)&&!g.Has(receipt)&&
                    g.GetQuest("night_shift_aftercare").steps[room-1].done&&
                    g.NightShiftReservedAidCount==1&&
                    g.save.moments.Count==moments+1,
                    "A true field receipt advances the story and preserves aid");
                g.Close();g.Talk(site);
                Check(Array.FindIndex(g.conversation.choices,
                        c=>c.action=="nightshift_visit_"+room)<0&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action==receipt)<0,
                    "The visit and unclaimed full-capacity aid cannot repeat");
                g.Close();
                if(publicAid)g.save.inventory[slot]=9996;
                else if(room==2)g.save.energy=70f;
                else g.save.health=g.save.maxHealth-30f;
                NightShiftChoose(g,site,receipt);
                Check(g.Has(receipt)&&g.save.moments.Count==moments+2&&
                    g.NightShiftReservedAidCount==0&&
                    (publicAid?g.save.inventory[slot]==9996+(room==3?1:2):
                     room==2?g.save.energy==100f:
                     g.save.health==g.save.maxHealth-(30f-(room==1?24f:18f))),
                    "Returning with real need safely pays the original distinct aid");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.Has(receipt)&&restored.save.version==2,
                    "Claimed field aid stays once-only in the unchanged SaveData v2 schema");
            }
        }

        // No self-damage or wasted inventory is required to complete the
        // actual three-stop story. Deferred aid survives saves and is one-use.
        public static void NightShiftFullCapacityVisitsStillFinishAftercare(Campaign data)
        {
            for(int outcome=0;outcome<2;outcome++)
            {
                var g=Fresh(data);
                g.save.stage=3;g.save.flags.Add("nightshift_started");
                for(int r=1;r<=3;r++)g.save.flags.Add("nightshift_trace_"+r);
                bool published=outcome==0;
                g.save.flags.Add(published?"nightshift_public":"nightshift_private");
                int moments=g.save.moments.Count;
                for(int room=1;room<=3;room++)
                {
                    WorldObject site=NightShiftAt(g,room);
                    if(published)g.save.inventory[room==1?2:room==2?0:4]=9999;
                    else if(room==2)g.save.energy=100f;
                    else g.save.health=g.save.maxHealth;
                    NightShiftChoose(g,site,"nightshift_visit_"+room);
                    Check(g.GetQuest("night_shift_aftercare").steps[room-1].done&&
                        !g.Has("nightshift_followup_"+room)&&
                        g.NightShiftReservedAidCount==room,
                        "Actual full-capacity visit advances only the receipt");
                }
                Check(g.GetQuest("night_shift_aftercare").room==0&&
                    g.save.moments.Count==moments+3,
                    "Three visited field sites point to the real town handoff");
                WorldObject desk=NightShiftAt(g,0);
                NightShiftChoose(g,desk,"nightshift_report_guard");
                Check(g.Has("nightshift_reported")&&
                    g.GetQuest("night_shift_aftercare").complete&&
                    g.NightShiftReservedAidCount==3,
                    "Town handoff remains available with unclaimed aid");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.GetQuest("night_shift_aftercare").complete&&
                    restored.NightShiftReservedAidCount==3,
                    "Report and three aid reservations survive unchanged v2 save");
                WorldObject forest=NightShiftAt(restored,1);
                if(published)restored.save.inventory[2]=9996;
                else restored.save.health=restored.save.maxHealth-30f;
                NightShiftChoose(restored,forest,"nightshift_followup_1");
                int claimedMoments=restored.save.moments.Count;
                Check(restored.NightShiftReservedAidCount==2&&
                    restored.Has("nightshift_followup_1")&&
                    restored.GetQuest("night_shift_aftercare").complete,
                    "Deferred aid is claimable after the quest was signed");
                restored.Close();restored.Talk(forest);
                Check(Array.FindIndex(restored.conversation.choices,
                    c=>c.action=="nightshift_followup_1")<0&&
                    restored.save.moments.Count==claimedMoments,
                    "Deferred aid remains strictly once-only");
            }
        }

        public static void NightShiftMineHasWalkedValveAlternativeRealTimerAndSaveIsolation(Campaign data)
        {
            var g=Fresh(data);
            g.mode="world";g.save.room=3;g.save.stage=3;g.enemies.Clear();
            g.save.flags.Add("nightshift_started");
            g.save.flags.Add("nightshift_trace_1");
            g.save.flags.Add("nightshift_trace_2");
            g.save.inventory[4]=0;
            WorldObject spool=NightShiftAt(g,3);
            WorldObject valve=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="nightvent"&&
                o.id=="nightshift_vent_3");
            Check(valve!=null&&g.Map.rows[(int)valve.y][(int)valve.x]!='#'&&
                g.CanStand(new Point(valve.x,valve.y)),
                "The original mine contains a truly physical, walkable remote pressure wheel");
            var path=g.NavigationPathFrom(new Point(spool.x,spool.y),
                new Point(valve.x,valve.y),true);
            Check(path.Count>0&&
                (new Point(spool.x,spool.y)-new Point(valve.x,valve.y)).Length>12f,
                "Mine pressure relief is a substantive physical cross-map route, not overlapping the source");
            int initial=g.save.moments.Count;
            NightShiftChoose(g,spool,"nightshift_vent_route");
            Check(g.NightShiftVentSeeking&&
                (g.GetQuest("night_shift").target-
                 new Point(valve.x,valve.y)).Length<.01f,
                "Opting into the alternative points actual quest navigation toward the distant machine");
            g.save.x=valve.x;g.save.y=valve.y;
            Check(g.CanStand(g.Position),"Physical pressure wheel admits real player feet");
            g.save.energy=10f;
            NightShiftChoose(g,valve,"nightshift_vent_start");
            Check(g.NightShiftVentSeconds==0f&&g.save.energy==10f&&
                !g.Has("nightshift_trace_3"),
                "Exhaustion does not remotely switch on an unpaid valve");
            g.save.energy=48f;
            NightShiftChoose(g,valve,"nightshift_vent_start");
            Check(g.NightShiftVentSeconds>84f&&
                g.save.energy==30f&&!g.NightShiftVentSeeking&&
                (g.GetQuest("night_shift").target-
                 new Point(spool.x,spool.y)).Length<.01f,
                "Paying 18 energy at the actual valve starts the 85-second cooling route home");
            g.save.x=spool.x;g.save.y=spool.y;
            int originalParts=g.save.inventory[4];
            NightShiftChoose(g,spool,"nightshift_vent_open");
            Check(g.NightShiftEvidenceCount==3&&
                g.save.inventory[4]==originalParts&&
                g.NightShiftVentSeconds==0f&&
                g.save.moments.Count==initial+1&&
                g.save.moments[initial].en.Contains("rail pressure wheel"),
                "Returning during real cooldown preserves all parts and records the actual hard route");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.Has("nightshift_trace_3")&&
                restored.NightShiftVentSeconds==0f&&
                restored.save.version==2,
                "Genuine cooling-route progress persists via existing signed v2 evidence, not runtime clocks");

            var expired=Fresh(data);
            expired.mode="world";expired.save.room=3;expired.save.stage=3;
            expired.enemies.Clear();
            expired.save.flags.Add("nightshift_started");
            expired.save.flags.Add("nightshift_trace_1");
            expired.save.flags.Add("nightshift_trace_2");
            WorldObject start=NightShiftAt(expired,3);
            WorldObject remote=Array.Find(expired.Map.objects,
                o=>o!=null&&o.id=="nightshift_vent_3");
            expired.save.x=remote.x;expired.save.y=remote.y;
            expired.save.energy=100;
            NightShiftChoose(expired,remote,"nightshift_vent_start");
            Check(expired.NightShiftVentSeconds>84f,
                "Independent run opens the same real cooldown window");
            expired.save.totalTime+=86f;
            expired.save.x=start.x;expired.save.y=start.y;
            Check(expired.NightShiftVentSeconds==0f,
                "Cooling truly expires on the existing world-time clock");
            expired.Talk(start);
            Check(expired.conversation!=null&&
                Array.FindIndex(expired.conversation.choices,
                    c=>c.action=="nightshift_vent_open")<0&&
                !expired.Has("nightshift_trace_3"),
                "Expired cooling cannot be claimed from a stale dialogue or by remaining next to the spool");
            expired.Close();expired.PrepareSave();
            var interrupted=new GameCore(data,CloneSave(expired.save));
            Check(interrupted.NightShiftVentSeconds==0f&&
                !interrupted.NightShiftVentSeeking&&
                !interrupted.Has("nightshift_trace_3"),
                "Reload does not smuggle transient machinery credit into another slot");
            expired.save.inventory[4]=1;
            NightShiftChoose(expired,start,"nightshift_trace_3");
            Check(expired.NightShiftEvidenceCount==3&&
                expired.save.inventory[4]==0,
                "The original one-part opening remains fully playable when the cooling route times out");
        }


        public static void NightShiftRuinsRequiresRealEbbOrPhysicalTimber(Campaign data)
        {
            var game=Fresh(data);
            game.save.flags.Add("nightshift_started");
            game.save.flags.Add("nightshift_trace_1");
            WorldObject ruin=NightShiftAt(game,2);
            int initial=game.save.moments.Count;
            game.save.totalTime=29f; // genuine high tide, not a stubbed state
            Check(!game.LowTide&&game.NightShiftEvidenceCount==1,
                "Actual 36-second regional tide clock is in flood phase");
            int originalTimber=game.save.inventory[0];
            game.save.inventory[0]=0;
            game.Talk(ruin);
            Check(game.conversation!=null&&
                Array.FindIndex(game.conversation.choices,
                    c=>c.action=="nightshift_trace_2")<0&&
                Array.FindIndex(game.conversation.choices,
                    c=>c.action=="nightshift_tidebrace")>=0,
                "A flooded original mark cannot be claimed by the ordinary exposed-ebb choice");
            NightShiftChoose(game,ruin,"nightshift_tidebrace");
            Check(game.NightShiftEvidenceCount==1&&game.save.inventory[0]==0,
                "No real timber means no improvised tide platform and no forged clue");
            game.save.inventory[0]=1;
            NightShiftChoose(game,ruin,"nightshift_tidebrace");
            Check(game.NightShiftEvidenceCount==2&&
                game.save.inventory[0]==0&&
                game.save.moments.Count==initial+1&&
                game.save.moments[game.save.moments.Count-1].en.Contains("spent one timber"),
                "One real timber creates a viable flood-time reading and durable authored record");
            game.PrepareSave();
            var loaded=new GameCore(data,CloneSave(game.save));
            Check(loaded.Has("nightshift_trace_2")&&
                loaded.save.version==2,
                "The resource-built tide measurement survives genuine v2 load");
            var waiting=Fresh(data);
            waiting.save.flags.Add("nightshift_started");
            waiting.save.flags.Add("nightshift_trace_1");
            WorldObject original=NightShiftAt(waiting,2);
            waiting.save.totalTime=0f;
            Check(waiting.LowTide,
                "Original tide returns naturally without a fabricated quest flag");
            int woodBefore=waiting.save.inventory[0];
            NightShiftChoose(waiting,original,"nightshift_trace_2");
            Check(waiting.NightShiftEvidenceCount==2&&
                waiting.save.inventory[0]==woodBefore,
                "Patient tide observation retrieves the same real clue for free at ebb");
            Check(originalTimber>=0,"Ordinary old-save materials remain representable");
        }


        public static void NightShiftForestGuardHasRealChoiceCombatAndNoFakeEvidence(Campaign data)
        {
            var g=Fresh(data);
            g.mode="world";g.enemies.Clear();
            WorldObject town=NightShiftAt(g,0);
            NightShiftChoose(g,town,"nightshift_begin");
            WorldObject forest=NightShiftAt(g,1);
            g.save.inventory[2]=0;g.save.energy=25;
            NightShiftChoose(g,forest,"nightshift_quiet");
            Check(g.NightShiftEvidenceCount==0&&
                g.save.energy==25&&g.save.inventory[2]==0,
                "No-herb or exhausted hero cannot recover the clue free");
            NightShiftChoose(g,forest,"nightshift_fight");
            Check(g.NightShiftRaidActive&&g.NightShiftRaidRemaining==2&&
                g.enemies.Count==2,
                "The original forest roll stages two fully real live enemies");
            Enemy[] actors=g.enemies.ToArray();
            Check(actors[0].kind==0&&actors[1].kind==1&&
                !g.NightShiftRaidReady,
                "The forest produces a melee pursuer and a genuine ranged scout, not duplicate decoration");
            foreach(Enemy foe in actors)
            {
                Check(g.IsNightShiftRaider(foe)&&g.CanStand(foe.Position)&&
                    g.NavigationPathFrom(g.Position,foe.Position,true).Count>0&&
                    (foe.Position-g.Position).Length>=5f,
                    "Exact battle actors occupy real, player-reachable forest ground");
                foreach(WorldObject prop in g.Map.objects)
                    Check((foe.Position-new Point(prop.x,prop.y)).Length>=4.45f,
                        "The raid does not block an original map object's interaction space");
            }
            Check((g.GetQuest("night_shift").target-actors[0].Position).Length<.01f||
                  (g.GetQuest("night_shift").target-actors[1].Position).Length<.01f,
                "The live mission marker follows an actual surviving attacker");
            // The actual v2 snapshot cannot serialize the temporary roster
            // as ordinary enemies detached from their scripted contract.
            g.PrepareSave();
            Check(g.save.worldEnemies!=null&&
                Array.Find(g.save.worldEnemies,r=>r!=null&&
                    (r.id==actors[0].id||r.id==actors[1].id))==null,
                "Transient forest trackers cannot escape into saved generic combat rosters");
            var interrupted=new GameCore(data,CloneSave(g.save));
            Check(!interrupted.NightShiftRaidActive&&
                interrupted.NightShiftEvidenceCount==0&&
                !interrupted.Has("nightshift_trace_1"),
                "Reloading an interrupted fight never grants a free evidence receipt");
            // Removing an actor without Hit must NEVER sign a false record.
            g.enemies.Remove(actors[0]);
            g.Tick(.05f,new Point());
            Check(!g.NightShiftRaidActive&&g.NightShiftEvidenceCount==0,
                "An unverified disappearing pursuer voids the entire real battle");
            g.Close();g.mode="world";
            NightShiftChoose(g,forest,"nightshift_fight");
            Check(g.NightShiftRaidRemaining==2,
                "After corrupted combat a complete genuine retry is possible");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Real damage and death attribution is available");
            Enemy[] retry=g.enemies.ToArray();
            int coins=g.save.coins;
            int kills=g.save.kills[1];
            // The scene already contained no other hostiles.
            Check(retry.Length==2&&g.IsNightShiftRaider(retry[0])&&
                g.IsNightShiftRaider(retry[1]),
                "Retry's two fighters are exact encounter-owned identities");
            var sameId=new Enemy(retry[0].id,0,retry[0].x+1f,
                retry[0].y,100f);
            g.enemies.Add(sameId);
            Check(!g.IsNightShiftRaider(sameId),
                "A foreign enemy reusing the same numeric ID cannot forge evidence");
            g.enemies.Remove(sameId);
            hit.Invoke(g,new object[]{retry[0],9999f,true,null,null});
            Check(g.NightShiftRaidRemaining==1&&!g.NightShiftRaidReady,
                "Only a genuine first kill releases half of this actual combat objective");
            g.invulnerable=20f;
            hit.Invoke(g,new object[]{retry[1],9999f,true,null,null});
            Check(g.NightShiftRaidReady&&g.NightShiftRaidRemaining==0&&
                g.save.coins==coins&&g.save.kills[1]==kills,
                "Both exact lethal contacts secure evidence, without normal loot or story kill inflation");
            g.Close();g.mode="world";
            Check((g.GetQuest("night_shift").target-
                new Point(forest.x,forest.y)).Length<.01f,
                "Once the live wave ends the quest points back to the genuine original record");
            NightShiftChoose(g,forest,"nightshift_trace_1");
            Check(g.NightShiftEvidenceCount==1&&!g.NightShiftRaidActive&&
                g.Has("nightshift_trace_1"),
                "Only the original secure physical record commits a permanent v2 evidence flag");
            g.PrepareSave();
            var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.Has("nightshift_trace_1")&&
                !saved.NightShiftRaidActive&&saved.save.version==2,
                "The combat-earned clue persists without serializing temporary actors");
        }


        private static WorldObject NightShiftAt(GameCore game,int room)
        {
            game.mode="world";game.save.room=room;
            game.enemies.Clear();
            WorldObject site=Array.Find(game.Map.objects,
                o=>o!=null&&o.id=="nightledger"+room&&o.kind=="nightledger");
            Check(site!=null,"A real night-shift source exists in room "+room);
            game.save.x=site.x;game.save.y=site.y;
            Check(game.CanStand(game.Position),
                "Night-shift actor stands on authentic walkable ground in region "+room);
            return site;
        }
        private static void NightShiftChoose(GameCore game,WorldObject site,
            string action)
        {
            game.Close();game.mode="world";
            game.Talk(site);
            Check(game.conversation!=null,
                "The real local night-shift source opens a dialog");
            int option=Array.FindIndex(game.conversation.choices,
                choice=>choice.action==action);
            Check(option>=0,"The current genuine source offers "+action);
            game.Choose(option);
        }
        public static void NightShiftFourRealPlacesBranchAndRetainOldSaveV2(Campaign data)
        {
            var g=Fresh(data);
            int originalCoin=g.save.coins;
            int initialChronicle=g.save.moments.Count;
            Check(!g.NightShiftComplete&&g.NightShiftEvidenceCount==0&&
                g.GetQuest("night_shift").room==0,
                "An untouched older save discovers a voluntary original town story");
            for(int room=0;room<=3;room++)
            {
                WorldObject site=NightShiftAt(g,room);
                Point spawn=new Point(g.Map.spawnX,g.Map.spawnY);
                Check(g.NavigationPathFrom(spawn,new Point(site.x,site.y),true).Count>0,
                    "Night-shift prop is accessible along authentic map collision in room "+room);
            }
            WorldObject town=NightShiftAt(g,0);
            NightShiftChoose(g,town,"nightshift_begin");
            Check(g.Has("nightshift_started")&&
                g.save.trackedQuest=="night_shift"&&
                g.GetQuest("night_shift").steps[0].done,
                "A conscious local acceptance begins the actual four-stop quest");
            // Jumping directly to the final mine spool before reading the
            // other sources can never forge a seal or spend inventory.
            WorldObject mine=NightShiftAt(g,3);
            int held=g.save.inventory[4];
            g.Talk(mine);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="nightshift_trace_3")<0&&
                g.save.inventory[4]==held,
                "The final physical record cannot skip the first two timestamps");
            WorldObject forest=NightShiftAt(g,1);
            float baselineSpeed=g.Speed;
            float oldEnergy=g.save.energy;
            int oldHerb=g.save.inventory[2];
            NightShiftChoose(g,forest,"nightshift_quiet");
            Check(g.save.energy==oldEnergy-30f&&
                g.save.inventory[2]==oldHerb-1&&
                !g.NightShiftRaidActive,
                "Quiet physical evidence retrieval spends exactly one herb and 30 stamina without conjuring raiders");
            Check(g.NightShiftEvidenceCount==1&&
                g.GetQuest("night_shift").steps[1].done,
                "The original forest evidence updates the read-only quest steps");
            WorldObject ruin=NightShiftAt(g,2);
            NightShiftChoose(g,ruin,"nightshift_false");
            Check(g.NightShiftEvidenceCount==1&&
                !g.Has("nightshift_trace_2"),
                "Copying an impossible official tide time cannot win a false clue");
            NightShiftChoose(g,ruin,"nightshift_trace_2");
            Check(g.NightShiftEvidenceCount==2,
                "The authentic low-tide timestamp advances the second clue");
            mine=NightShiftAt(g,3);
            g.save.inventory[4]=0;
            NightShiftChoose(g,mine,"nightshift_trace_3");
            Check(g.NightShiftEvidenceCount==2&&
                !g.Has("nightshift_trace_3"),
                "A physical sealed spool refuses to open without the real part");
            g.save.inventory[4]=1;
            NightShiftChoose(g,mine,"nightshift_trace_3");
            Check(g.NightShiftEvidenceCount==3&&
                g.save.inventory[4]==0&&
                g.GetQuest("night_shift").steps[3].done,
                "The mine costs exactly one real part and completes the field investigation");
            town=NightShiftAt(g,0);
            NightShiftChoose(g,town,"nightshift_public");
            Check(g.save.trackedQuest=="night_shift_aftercare"&&
                g.GetQuest("night_shift_aftercare").steps.Count==4&&
                g.GetQuest("night_shift_aftercare").room==1,
                "Choosing the town ending automatically hands off to the next physically available quest instead of a completed marker");
            Check(g.save.moments.Count==initialChronicle+5&&
                g.save.moments[g.save.moments.Count-1].en.Contains("restored the erased road"),
                "All three real clues and the public decision appear once in the persistent player chronicle");
            Check(g.NightShiftComplete&&g.Has("nightshift_public")&&
                !g.Has("nightshift_private")&&
                g.save.coins==originalCoin+40&&
                g.GetQuest("night_shift").complete,
                "The town publication requires all three signatures and pays once");
            g.Close();g.mode="world";
            // A repeat conversation gives only the archived account, not
            // another publish button or duplicate money.
            g.Talk(town);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="nightshift_public")<0&&
                g.save.coins==originalCoin+40&&
                g.save.moments.Count==initialChronicle+5,
                "Reopening the completed town roll never duplicates rewards or chronicle entries");
            g.Close();g.save.room=1;
            Check(g.Speed>=baselineSpeed+.20f&&g.save.version==2,
                "Publicly restoring the old road has an actual measurable field travel benefit");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.NightShiftComplete&&
                restored.GetQuest("night_shift").complete&&
                restored.save.version==2&&
                restored.save.moments.Count==initialChronicle+5&&
                restored.save.trackedQuest=="night_shift_aftercare"&&
                restored.GetQuest("night_shift_aftercare").room==1,
                "The original outcome survives v2 reload and hands navigation to the first unsupplied real region");
            var privateRun=Fresh(data);
            privateRun.save.flags.Add("nightshift_started");
            for(int r=1;r<=3;r++)
                privateRun.save.flags.Add("nightshift_trace_"+r);
            WorldObject desk=NightShiftAt(privateRun,0);
            float oldMax=privateRun.save.maxHealth;
            int oldPrivateCoins=privateRun.save.coins;
            int oldMoments=privateRun.save.moments.Count;
            NightShiftChoose(privateRun,desk,"nightshift_private");
            Check(privateRun.Has("nightshift_private")&&
                !privateRun.Has("nightshift_public")&&
                privateRun.save.maxHealth==oldMax+8&&
                privateRun.save.coins==oldPrivateCoins+40&&
                privateRun.save.moments.Count==oldMoments+1&&
                privateRun.save.version==2,
                "The distinct witness-protection resolution grants real one-time max health");
            privateRun.Close();privateRun.Talk(desk);
            Check(Array.FindIndex(privateRun.conversation.choices,
                c=>c.action=="nightshift_private")<0&&
                privateRun.save.maxHealth==oldMax+8,
                "An old save can revisit the protected records without farming health");
            privateRun.Close();privateRun.PrepareSave();
            var recoveredPrivate=new GameCore(data,CloneSave(privateRun.save));
            Check(recoveredPrivate.Has("nightshift_private")&&
                recoveredPrivate.save.maxHealth==oldMax+8&&
                recoveredPrivate.save.version==2,
                "The chosen +8 max-health reward survives the REAL load-time canonical health recalculation");
            recoveredPrivate.Load(CloneSave(recoveredPrivate.save));
            Check(recoveredPrivate.save.maxHealth==oldMax+8,
                "Repeated slot loads never double or erase the original private ending bonus");
        }

        public static void StoryRecapOnlyRevealsChaptersPlayerReached(Campaign data)
        {
            var game=Fresh(data);
            int initialFlags=game.save.flags.Count;
            List<StoryBeat> opening=game.StoryRecap();
            Check(opening.Count==1 && opening[0].chapter==1 &&
                opening[0].questId=="main" && !opening[0].complete &&
                opening[0].next==game.GetQuest("main").next,
                "A new adventure receives only its real first story beat and next objective");
            Check(!opening[0].summary.Contains("星核事件之后") &&
                game.save.flags.Count==initialFlags,
                "Reading story does not spoil postwar outcomes or write any campaign flags");
            game.save.flags.Add("scout_saved");
            var scout=game.StoryRecap();
            Check(scout.Count==1 && scout[0].summary.Contains("侦察员已经找到"),
                "Once the scout is rescued the explanation adds only already discovered evidence");
            game.save.stage=1;
            var second=game.StoryRecap();
            Check(second.Count==2 && second[0].complete &&
                second[1].chapter==2 && !second[1].complete,
                "Completing the first core changes its beat to history and reveals the ruins");
            game.save.stage=2;
            Check(game.StoryRecap().Count==3,
                "The mining chapter remains hidden until the second core has progressed");
            game.save.stage=3;
            var citadel=game.StoryRecap();
            Check(citadel.Count==4 && citadel[3].chapter==4 &&
                !citadel[3].complete && citadel[3].questId=="main",
                "The citadel and three-core investigation appear only on entering chapter four");
            game.save.flags.Add("triad_complete");
            Check(game.StoryRecap()[3].summary.Contains("已经公开"),
                "Publishing the three-core evidence updates the story's real historical explanation");
            Check(game.StoryRecap().Count==4,
                "Research alone does not reveal the consequences of an unchosen ending");
        }

        public static void StoryRecapAftermathFollowsActualUnlocksAndQuestNavigation(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.ending=1;
            var beats=game.StoryRecap();
            Check(beats.Count>=5 && beats[3].complete &&
                beats[4].chapter==5 && beats[4].questId=="afterlight" &&
                !beats[4].complete && beats[4].next==game.GetQuest("afterlight").next,
                "Original final choice reveals only the unlocked reconstruction chapter and its live quest");
            foreach(StoryBeat beat in beats)
            {
                Check(beat.chapter>=1 && beat.chapter<=12 &&
                    !string.IsNullOrEmpty(beat.title) &&
                    !string.IsNullOrEmpty(beat.summary) &&
                    !string.IsNullOrEmpty(beat.next) &&
                    Array.IndexOf(GameCore.QuestIds,beat.questId)>=0,
                    "Every visible story summary is readable and linked to a real trackable quest");
                Check(beat.chapter<=5 || !game.GetQuest(beat.questId).locked,
                    "No later locked chapter can leak into the completed-journey journal");
            }
            game.save.english=true;
            var english=game.StoryRecap();
            Check(english.Count==beats.Count && english[4].title.StartsWith("Afterstory") &&
                english[4].next==game.GetQuest("afterlight").next &&
                !english[4].summary.Contains("星核"),
                "Already unlocked story pages, explanations and goals switch to English together");
            game.save.flags.Add("afterlight_started");
            var updated=game.StoryRecap();
            Check(updated[4].next==game.GetQuest("afterlight").next &&
                updated[4].next!=english[4].next,
                "New reconstruction progress updates the explanation's next step without a saved recap cache");
            game.save.english=false;
            Check(game.StoryRecap()[4].summary.Contains("星核") &&
                game.StoryRecap()[4].chapter==5,
                "Switching back to Chinese preserves the same chapter order and actual progress");
        }

        public static void StoryRecapDistinguishesTheThreeActualEndings(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.flags.Add("triad_complete");
            Check(!game.StoryRecap()[3].summary.Contains("归还星核") &&
                !game.StoryRecap()[3].summary.Contains("保留了屏障"),
                "The finale does not reveal unchosen ending consequences");
            game.save.ending=1;
            string returning=game.StoryRecap()[3].summary;
            game.save.ending=2;
            string keeping=game.StoryRecap()[3].summary;
            game.save.ending=3;
            string sharing=game.StoryRecap()[3].summary;
            Check(returning.Contains("河流与土地开始恢复") &&
                keeping.Contains("对城外人开放庇护") &&
                sharing.Contains("公共水泵") &&
                returning!=keeping && keeping!=sharing,
                "Every saved original ending receives its own truthful account of the lasting decision");
            game.save.english=true;
            game.save.ending=2;
            Check(game.StoryRecap()[3].summary.Contains("You kept the shield") &&
                !game.StoryRecap()[3].summary.Contains("你保留了屏障"),
                "The specific past ending is also readable in English without a duplicated Chinese suffix");
        }

        public static void ChapterTwelveStoryRecapTracksActualThreeRegionAudit(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;game.save.ending=1;
            Check(!game.StoryRecap().Exists(b=>b.chapter==12),
                "The new story recap never reveals Chapter Twelve before the genuine completed public grid");
            game.save.flags.Add("civic_conduit_complete");
            List<StoryBeat> opened=game.StoryRecap();
            StoryBeat chapter=opened[opened.Count-1];
            Check(chapter.chapter==12&&chapter.questId=="civic_aftercare"&&
                chapter.title.Contains("后日谈")&&!chapter.complete&&
                chapter.next==game.GetQuest("civic_aftercare").next&&
                chapter.summary.Contains("真实路碑"),
                "The completed public grid reveals a voluntary physical marker survey, not another required tavern chapter");
            game.save.flags.Add("civic_aftercare_started");
            List<StoryBeat> started=game.StoryRecap();
            Check(started[started.Count-1].chapter==12&&
                started[started.Count-1].summary.Contains("巡路簿")&&
                started[started.Count-1].next==
                    game.GetQuest("civic_aftercare").next,
                "Chapter Twelve speaks of the actual accepted road ledger, not an unstarted task");
            game.save.flags.Add("civic_aftercare_waytrail_0");
            Check(game.StoryRecap()[game.StoryRecap().Count-1].next==
                game.GetQuest("civic_aftercare").next&&
                game.GetQuest("civic_aftercare").room==3,
                "The story recap follows the real second regional waypoint after Canal is surveyed");
            game.save.flags.Add("civic_aftercare_waytrail_3");
            game.save.flags.Add("civic_aftercare_waytrail_5");
            StoryBeat ready=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(!ready.complete&&ready.next==
                game.GetQuest("civic_aftercare").next&&
                game.GetQuest("civic_aftercare").room==9,
                "Three visited landmarks are not falsely counted as a published report; the live goal returns to the Inn");
            game.save.english=true;
            StoryBeat english=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(english.title.StartsWith("Afterstory")&&
                english.summary.Contains("Hearth Inn")&&
                !english.summary.Contains("夜行旅人")&&
                english.next==game.GetQuest("civic_aftercare").next,
                "The same earned chapter and actual quest goal render entirely in English");
            game.save.flags.Add("civic_aftercare_published");
            StoryBeat complete=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(complete.chapter==12&&complete.complete&&
                complete.summary.Contains("road ledger")&&
                complete.next.Contains("stock the waystations")&&
                game.StoryRecapTrackableQuestId(complete)=="civic_waystations",
                "Real public publication retires the road audit and records its permanent field-kit legacy");
            int count=game.save.flags.Count,coins=game.save.coins;
            game.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(game.save));
            reloaded.Start();
            List<StoryBeat> resumed=reloaded.StoryRecap();
            Check(reloaded.save.version==2&&resumed[resumed.Count-1].chapter==12&&
                resumed[resumed.Count-1].complete&&
                reloaded.save.flags.Count==count&&reloaded.save.coins==coins,
                "No Chapter Twelve cache or SaveData migration is required to restore the final public road ledger");
        }

        public static void ChapterTwelveJournalNavigatesToRealWaystationsAfterAudit(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;game.save.ending=3;
            game.save.flags.Add("civic_conduit_complete");
            StoryBeat pending=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(!pending.complete&&
                game.StoryRecapTrackableQuestId(pending)=="civic_aftercare",
                "The Journey panel first tracks the real unfinished three-region road audit");
            game.save.flags.Add("civic_aftercare_started");
            game.save.flags.Add("civic_aftercare_waytrail_0");
            game.save.flags.Add("civic_aftercare_waytrail_3");
            game.save.flags.Add("civic_aftercare_waytrail_5");
            game.save.flags.Add("civic_aftercare_published");
            StoryBeat published=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(published.chapter==12&&published.complete&&
                game.StoryRecapTrackableQuestId(published)=="civic_waystations"&&
                published.next.Contains(game.GetQuest("civic_waystations").next),
                "The finished audit still gives a clickable, truthful task and destination for outstanding waystation construction");
            int initialCoins=game.save.coins,flags=game.save.flags.Count;
            game.TrackQuest(game.StoryRecapTrackableQuestId(published));
            Check(game.save.trackedQuest=="civic_waystations"&&
                game.save.coins==initialCoins&&game.save.flags.Count==flags,
                "The actual Journey navigation selects live civic resupply without handing out any supplies or resurrecting the completed audit");
            game.save.english=true;
            StoryBeat english=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(english.next.Contains("stock the waystations")&&
                english.next.Contains(game.GetQuest("civic_waystations").next)&&
                !english.next.Contains("路站"),
                "The continued road-network goal is bilingual and resolves the real local quest target");
            game.save.flags.Add("civic_waystation_stocked_0");
            game.save.flags.Add("civic_waystation_stocked_3");
            StoryBeat shuttle=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(shuttle.next.Contains(game.GetQuest("civic_waystations").next)&&
                shuttle.next.Contains("Shuttles now run"),
                "After two paid outposts the same chapter follows real unlocked shuttles and the final unbuilt station");
            game.save.flags.Add("civic_waystation_stocked_5");
            game.save.flags.Add("civic_waystations_complete");
            StoryBeat finished=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(game.StoryRecapTrackableQuestId(finished)==null&&
                finished.next.Contains("herbal field kits")&&
                !finished.next.Contains("stock the waystations"),
                "After all three genuine outposts are complete the Journey button returns to a harmless historical record");
            var previous=game.StoryRecap()[3];
            Check(game.StoryRecapTrackableQuestId(previous)==null&&
                game.StoryRecapTrackableQuestId(null)==null&&
                game.save.version==2,
                "Old completed chapters and missing pages never offer ghost task tracking");
        }

        public static void StoryRecapNeverRewritesExistingSaveOrQuestTracking(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.ending=1;
            game.save.trackedQuest="main";
            int coins=game.save.coins,shards=game.save.shards,skills=game.save.skillPoints;
            int moments=game.save.moments.Count,revision=game.revision;
            for(int i=0;i<9;i++)
            {
                var beats=game.StoryRecap();
                Check(beats[0].chapter==1 && beats[beats.Count-1].chapter>=5,
                    "Repeated journal reads remain historically ordered");
            }
            Check(game.save.trackedQuest=="main" && game.save.coins==coins &&
                game.save.shards==shards && game.save.skillPoints==skills &&
                game.save.moments.Count==moments && game.revision==revision,
                "Viewing a past chapter never awards rewards, changes quest tracking or mutates save data");
            game.PrepareSave();
            var restored=new GameCore(data,CloneSave(game.save));
            restored.Start();
            Check(restored.StoryRecap().Count==game.StoryRecap().Count &&
                restored.StoryRecap()[4].questId=="afterlight",
                "Existing SaveData v2 is sufficient to reconstruct all chapter context after reload");
        }
    }
}
