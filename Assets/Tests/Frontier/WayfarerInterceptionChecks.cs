using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void TravelerFightRejectsMissingSecondRaiderWhileFirstStillLives(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=10;
            g.save.flags.Add("scout_saved");g.Enter(1);
            g.enemies.Clear();g.invulnerable=120f;
            WorldObject traveler=Object(g,"wayfarer1");
            g.save.x=traveler.x;g.save.y=traveler.y;
            Action(g,traveler.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1&&
                g.enemies.FindAll(e=>e.id>=82000&&e.id<82100).Count==2,
                "Actual defense spawns both original identity-verified attackers");
            Enemy survivor=g.enemies.Find(e=>e.id==82010);
            Enemy missing=g.enemies.Find(e=>e.id==82011);
            Check(survivor!=null&&missing!=null,
                "Both standard traveler combat IDs must remain unchanged");
            survivor.stun=100f;survivor.timer=100f;
            g.enemies.Remove(missing);
            Check(g.enemies.Contains(survivor)&&g.WayfarerEncounterRoom==1,
                "Only the second real attacker disappears without a lethal callback");
            int coins=g.save.coins,shards=g.save.shards;
            g.Tick(.05f,new Point());
            Check(g.WayfarerEncounterRoom==0&&
                !g.enemies.Contains(survivor)&&
                !g.Has("wayfarer_1")&&
                g.save.coins==coins&&g.save.shards==shards,
                "Still-living first raider cannot mask missing second enemy or lock the rescue into an endless trial");
            Action(g,traveler.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1&&
                g.enemies.FindAll(e=>e.id>=82000&&e.id<82100).Count==2&&
                g.save.version==2,
                "Interrupted encounter can restart free with two real raiders and unmodified v2 save format");
        }

        public static void WayfarerAmbushFeetRejectIsolatedPocketAndOccupiedRealCompanion(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[40];
                for(int y=0;y<40;y++)
                {
                    char[] tiles=new string('#',40).ToCharArray();
                    if(y>=2&&y<=37)
                        for(int x=2;x<=37;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                // The first former radius-three candidate (-3,-3) is
                // apparently standable but enclosed inside genuine walls.
                for(int y=15;y<=17;y++)
                {
                    char[] tiles=rows[y].ToCharArray();
                    for(int x=15;x<=17;x++)
                        if(x!=16||y!=16)tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[1]=new MapData{id=1,
                    spawnX=19.5f,spawnY=19.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(1);
                g.enemies.Clear();g.save.x=19.5f;g.save.y=19.5f;
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                g.companionPosition=new Point(16.5f,19.5f);
                Point isolated=new Point(16.5f,16.5f);
                var existing=new Enemy(92711,0,19.5f,16.5f,100f)
                    {stun=200f,timer=1000f};
                g.enemies.Add(existing);
                var method=typeof(GameCore).GetMethod("TryWayfarerAmbushFeet",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(method!=null&&g.CanStand(isolated)&&
                    g.CanStand(g.companionPosition),
                    "The actual rescue selector sees an isolated walkable room and a live companion foot");
                object[] args={g.Position,null};
                Check((bool)method.Invoke(g,args),
                    "Ambush still has enough alternative combat floor with one sealed pocket, a friend and a bystander");
                Point[] feet=(Point[])args[1];
                Check(feet!=null&&feet.Length==2,
                    "Two complete authored ambush feet preflight before any combat actor is inserted");
                foreach(Point foot in feet)
                    Check(g.CanStand(foot)&&
                        (foot-isolated).Length>1.5f&&
                        (foot-g.companionPosition).Length>=1.55f&&
                        (foot-existing.Position).Length>=2.1f&&
                        (foot-g.Position).Length>=2.7f,
                        "Real spawn cannot overlap canonical hero, companion, ambient enemy or unreachable floor");
                Check((feet[0]-feet[1]).Length>=3f&&
                    g.enemies.Count==1&&!g.Has("wayfarer_1")&&
                    g.WayfarerEncounterRoom==0&&g.save.version==2,
                    "Preflight never materializes only one fighter, spends supplies, forges an award or changes the save");
            }
            finally{data.maps[1]=original;}
        }

        public static void TravelerDonationAndSharedStewRequireLiveUnblockedNPCContact(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[30];
                for(int y=0;y<30;y++)
                {
                    char[] cells=new string('#',30).ToCharArray();
                    if(y>=2&&y<=27)
                        for(int x=2;x<=27;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                var traveler=new WorldObject{
                    id="wayfarer1",kind="wayfarer1",
                    name="Stranded civilian",en="Stranded civilian",
                    x=10.5f,y=10.5f};
                data.maps[1]=new MapData{id=1,
                    spawnX=12.25f,spawnY=10.5f,rows=rows,
                    objects=new[]{traveler},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("scout_saved");
                g.save.flags.Add("wayfarer_2");
                g.save.flags.Add("wayfarer_3");
                g.Enter(1);g.enemies.Clear();
                g.save.x=12.25f;g.save.y=10.5f;
                g.save.inventory[2]=4;g.save.inventory[6]=9999;
                g.Talk(traveler);
                Check(g.mode=="dialog"&&g.conversation!=null&&
                    g.CanStand(g.Position),
                    "Genuine rescued-scout roadside NPC offers a two-Moonherb physical donation");
                int donate=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="wayfarer_supply_1")
                    {donate=i;break;}
                Check(donate>=0,"The real two-herb rescue action is visible");
                char[] closed=rows[10].ToCharArray();
                closed[11]='#';rows[10]=new string(closed);
                Check(g.CanStand(new Point(traveler.x,traveler.y))&&
                    g.CanStand(g.Position)&&g.Blocked(11.5f,10.5f),
                    "A live blocking wall separates standable hero and civilian after dialogue opens");
                g.Choose(donate);
                Check(g.save.inventory[2]==4&&!g.Has("wayfarer_1")&&
                    !g.Has("wayfarer_complete"),
                    "Stale donation cannot spend two actual herbs or award a permanent rescue through stone");
                closed[11]='.';rows[10]=new string(closed);
                g.Talk(traveler);donate=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="wayfarer_supply_1")
                    {donate=i;break;}
                Check(donate>=0,"Opening the same path restores the original donation");
                g.Choose(donate);
                Check(g.save.inventory[2]==2&&g.Has("wayfarer_1")&&
                    !g.Has("wayfarer_complete"),
                    "A genuinely reachable civilian accepts exactly the two real Moonherbs and defers a full-stack stew reward");
                g.save.inventory[6]=9998;int beforeSkill=g.save.skillPoints;
                g.Talk(traveler);
                int collect=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="wayfarer_reward")
                    {collect=i;break;}
                Check(collect>=0,"All three saved rescues expose the actual once-only shared stew handover");
                closed[11]='#';rows[10]=new string(closed);
                g.Choose(collect);
                Check(g.save.inventory[6]==9998&&
                    g.save.skillPoints==beforeSkill&&
                    !g.Has("wayfarer_complete"),
                    "A stale handover button cannot mint stew or a talent point across the closed wall");
                closed[11]='.';rows[10]=new string(closed);
                g.Talk(traveler);collect=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="wayfarer_reward")
                    {collect=i;break;}
                Check(collect>=0,"An open rescue NPC still offers the legitimate pending final reward");
                g.Choose(collect);
                Check(g.Has("wayfarer_complete")&&
                    g.save.inventory[6]==9999&&
                    g.save.skillPoints==beforeSkill+1&&
                    g.save.version==2,
                    "One reachable handover creates exactly one lasting stew/talent reward with original v2 saves");
            }
            finally{data.maps[1]=original;}
        }

        public static void LateWayfarerBodyguardInterceptionIsRealCombat(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            g.save.flags.Add("scout_saved"); g.Enter(1); g.enemies.Clear();
            WorldObject npc = Object(g, "wayfarer1");
            Point traveler = new Point(npc.x, npc.y);
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, npc.kind, "wayfarer_defend_1");
            Enemy raider = g.enemies.Find(e => e.id >= 82000 && e.id < 82100 && e.kind == 0);
            Enemy other = g.enemies.Find(e => e != raider && e.id >= 82000 && e.id < 82100);
            Check(raider != null && other != null, "Bodyguard requires actual ambush enemies");
            other.stun = 100;
            Point raiderStart = new Point(), blockPoint = new Point();
            bool laneFound = false;
            Point[] offsets = { new Point(2, 0), new Point(-2, 0),
                new Point(0, 2), new Point(0, -2) };
            foreach (Point offset in offsets)
            {
                Point start = traveler + offset, middle = traveler + offset * .5f;
                if (!g.CanStand(start) || !g.CanStand(middle) ||
                    g.NavigationPath(start).Count == 0) continue;
                raiderStart = start; blockPoint = middle; laneFound = true; break;
            }
            Check(laneFound, "The actual roadside traveler has a reachable interception lane");
            Point distant = new Point();
            bool farFound = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !farFound; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !farFound; x++)
            {
                Point p = new Point(x + .5f, y + .5f);
                if (g.CanStand(p) && (p - traveler).Length > 8 && (p - raiderStart).Length > 8)
                { distant = p; farFound = true; }
            }
            Check(farFound, "The encounter map offers a distant player position");
            g.save.x = distant.x; g.save.y = distant.y;
            raider.x = raiderStart.x; raider.y = raiderStart.y;
            raider.timer = 0; raider.stun = 0;
            g.Tick(.02f, new Point());
            Check(raider.state == 1 && raider.targetingWayfarer,
                "Raider commits its telegraph to the traveler before the hero intervenes");
            g.save.x = blockPoint.x; g.save.y = blockPoint.y;
            g.invulnerable = 0;
            float heroHealth = g.save.health;
            Advance(g, .8f);
            Check(g.WayfarerIntegrity == 100 && g.save.health < heroHealth &&
                g.effects.Exists(e => e.kind == "number" && e.text == "护卫拦截"),
                "Last-second lane interception protects the NPC but resolves a real hit on the hero");
            g.save.x = distant.x; g.save.y = distant.y;
            raider.x = raiderStart.x; raider.y = raiderStart.y;
            raider.state = 1; raider.timer = .02f; raider.stun = 0; raider.releaseTime = 0;
            raider.target = traveler; raider.targetingWayfarer = true;
            raider.Face(traveler - raider.Position);
            g.Tick(.05f, new Point());
            Check(g.WayfarerIntegrity < 100 && g.WayfarerEncounterRoom == 1,
                "A strike not intercepted by the player still damages the traveler");
        }

        public static void WayfarerRescueChoicesPersistAsDistinctWorldReactions(Campaign data)
        {
            var supplied = Fresh(data); supplied.save.stage = 3;
            supplied.save.flags.Add("scout_saved"); supplied.Enter(1); supplied.enemies.Clear();
            WorldObject npc = Object(supplied, "wayfarer1");
            supplied.save.x = npc.x; supplied.save.y = npc.y;
            supplied.save.inventory[2] = 2;
            Action(supplied, npc.kind, "wayfarer_supply_1");
            Check(supplied.Has("wayfarer_1") && supplied.Has("wayfarer_supplied_1") &&
                !supplied.Has("wayfarer_defended_1"), "Supply rescue records its method exactly once");
            supplied.Talk(npc);
            Check(supplied.conversation.textEn.Contains("Moonherbs"),
                "Traveler acknowledges the actual supplies after a donation");
            supplied.Close(); supplied.PrepareSave();
            var loaded = new GameCore(data, CloneSave(supplied.save)); loaded.Start();
            Check(loaded.Has("wayfarer_supplied_1") && loaded.Has("wayfarer_1"),
                "Choice-specific dialogue survives the existing save format");
            loaded.Talk(npc);
            Check(loaded.conversation.textEn.Contains("Moonherbs"),
                "A loaded save keeps the same traveler reaction");

            var defended = Fresh(data); defended.save.stage = 3; defended.save.level = 10;
            defended.save.flags.Add("scout_saved"); defended.Enter(1); defended.enemies.Clear();
            WorldObject survivor = Object(defended, "wayfarer1");
            defended.save.x = survivor.x; defended.save.y = survivor.y;
            Action(defended, survivor.kind, "wayfarer_defend_1");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The real combat hit method is available");
            foreach (Enemy foe in defended.enemies.FindAll(e => e.id >= 82000 && e.id < 82100))
                hit.Invoke(defended, new object[] { foe, 9999f, false, null, null });
            Advance(defended, .35f);
            Check(defended.Has("wayfarer_1") && defended.Has("wayfarer_defended_1") &&
                !defended.Has("wayfarer_supplied_1"), "Combat rescue records a different lasting choice");
            defended.Talk(survivor);
            Check(defended.conversation.textEn.Contains("held off the raiders"),
                "Traveler remembers being defended rather than donated to");
            // Older saves already marked rescued must retain a safe generic response.
            var oldSave = CloneSave(defended.save);
            oldSave.flags.Remove("wayfarer_defended_1");
            var legacy = new GameCore(data, oldSave); legacy.Start();
            legacy.Talk(survivor);
            Check(legacy.conversation.textEn.Contains("Thanks to you"),
                "Older rescued saves remain readable without a migration");
        }
        public static void WayfarerThreeRescuesReservePhysicalBonusUntilStewFits(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("wayfarer_2");
            g.save.flags.Add("wayfarer_3");
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99; g.save.inventory[6] = 9999;
            g.Enter(1); g.enemies.Clear();
            var traveler = Object(g, "wayfarer1");
            g.save.x = traveler.x; g.save.y = traveler.y;
            g.save.inventory[2] = 5;
            Action(g, traveler.kind, "wayfarer_supply_1");
            Check(g.Has("wayfarer_1") && g.Has("wayfarer_supplied_1") &&
                !g.Has("wayfarer_complete") && g.save.inventory[2] == 3 &&
                g.save.inventory[6] == 9999 && g.save.skillPoints == 99 &&
                g.save.coins == 999999 && g.save.shards == 99999,
                "Real third rescue spends its supply, preserves achievement and bounded money, but never consumes the one-time stew reward at a full stack");
            g.Talk(traveler);
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "wayfarer_reward"),
                "Already rescued traveler actually offers collection of the deferred physical award");
            g.Close();
            Action(g, traveler.kind, "wayfarer_reward");
            Check(!g.Has("wayfarer_complete") && g.save.inventory[6] == 9999,
                "Attempting to claim while full leaves the original one-time reward pending");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("wayfarer_1") && resumed.Has("wayfarer_2") &&
                resumed.Has("wayfarer_3") && !resumed.Has("wayfarer_complete"),
                "Pending final reward and all three genuine rescues survive an original v2 save reload");
            resumed.enemies.Clear();
            resumed.save.x = traveler.x; resumed.save.y = traveler.y;
            resumed.save.inventory[6] = 9998;
            Action(resumed, traveler.kind, "wayfarer_reward");
            Check(resumed.Has("wayfarer_complete") &&
                resumed.save.inventory[6] == 9999 &&
                resumed.save.skillPoints == 99,
                "One free slot lets the original NPC award exactly one stew and bounded talent point");
            resumed.Talk(traveler);
            Check(resumed.conversation != null &&
                !Array.Exists(resumed.conversation.choices,
                    c => c.action == "wayfarer_reward"),
                "Already collected final supplies cannot be farmed by repeated dialogue");
            resumed.PrepareSave();
            var after = new GameCore(data, CloneSave(resumed.save));
            Check(after.Has("wayfarer_complete") && after.save.inventory[6] == 9999 &&
                after.save.coins == 999999 && after.save.shards == 99999,
                "Full rescue and exact bounded physical reward persist without v3 migration");
        }

        public static void TravelerAmbushContactVisualsDoNotFakeFutureDangerOrSurviveAbort(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            g.save.flags.Add("scout_saved");
            g.Enter(1); g.enemies.Clear(); g.effects.Clear();
            var npc = Object(g, "wayfarer1");
            Point traveler = new Point(npc.x, npc.y);
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, npc.kind, "wayfarer_defend_1");
            Enemy raider = g.enemies.Find(e => e.id >= 82000 &&
                e.id < 82100 && e.kind == 0);
            Check(raider != null && g.effects.Exists(e =>
                e.wayfarerOwned && e.kind == "discover"),
                "Actual physical ambush spawn effects belong to their encounter");
            var unowned = new Effect("discover", new Point(12, 14), 3f);
            g.effects.Add(unowned);
            Point[] offsets = { new Point(2, 0), new Point(-2, 0),
                new Point(0, 2), new Point(0, -2) };
            bool laneFound = false;
            foreach (Point offset in offsets)
            {
                Point at = traveler + offset;
                if (!g.CanStand(at)) continue;
                raider.x = at.x; raider.y = at.y; laneFound = true; break;
            }
            Check(laneFound, "Original traveler map provides a safe enemy contact lane");
            Point distant = new Point();
            bool found = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !found; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !found; x++)
            {
                var at = new Point(x + .5f, y + .5f);
                if (!g.CanStand(at) || (at - traveler).Length < 8) continue;
                distant = at; found = true;
            }
            Check(found, "The ambush offers a legal faraway hero position");
            g.save.x = distant.x; g.save.y = distant.y;
            raider.state = 1; raider.timer = .005f;
            raider.target = traveler; raider.targetingWayfarer = true;
            raider.Face(traveler - raider.Position);
            g.invulnerable = 100;
            g.Tick(.05f, new Point());
            Check(g.WayfarerIntegrity < 100 &&
                g.effects.Exists(e => e.kind == "burst" && e.wayfarerOwned) &&
                !g.effects.Exists(e => e.kind == "danger" &&
                    e.wayfarerOwned && e.life <= .35f),
                "Real raider damages the unguarded civilian with owned contact flash rather than a new fake post-hit danger warning");
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, npc.kind, "wayfarer_abandon");
            Check(g.WayfarerEncounterRoom == 0 &&
                !g.effects.Exists(e => e.wayfarerOwned) &&
                g.effects.Contains(unowned),
                "Abandoning an actual wounded traveler cleans only encounter flashes and preserves unrelated world visuals");
        }

        public static void WayfarerMustDefeatBothRaidersBeforeAwardingRescue(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=10;
            g.save.flags.Add("scout_saved");g.Enter(1);g.enemies.Clear();
            WorldObject npc=Object(g,"wayfarer1");
            g.save.x=npc.x;g.save.y=npc.y;g.invulnerable=100;
            int money=g.save.coins,shards=g.save.shards,mastery=g.save.mastery[g.save.weapon];
            Action(g,npc.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1&&
                g.enemies.FindAll(e=>e.id>=82010&&e.id<=82011).Count==2,
                "The original road encounter spawns two distinct live raiders");
            var first=g.enemies.Find(e=>e.id==82010);
            g.enemies.Remove(first);
            g.Tick(.05f,new Point());
            Check(g.WayfarerEncounterRoom==0&&
                !g.Has("wayfarer_1")&&!g.Has("wayfarer_defended_1")&&
                g.save.coins==money&&g.save.shards==shards&&
                g.save.mastery[g.save.weapon]==mastery&&
                !g.enemies.Exists(e=>e.id>=82010&&e.id<=82011),
                "A vanished raider resets without rewarding the NPC or fake weapon mastery");
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1,
                "The real traveler offers a legitimate free retry");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Authoritative lethal combat path is available");
            Enemy a=g.enemies.Find(e=>e.id==82010),b=g.enemies.Find(e=>e.id==82011);
            hit.Invoke(g,new object[]{a,99999f,false,null,null});
            Check(g.enemies.Exists(e=>e.id==82011)&&!g.Has("wayfarer_1"),
                "A single confirmed lethal hit cannot complete the two-guard rescue");
            hit.Invoke(g,new object[]{b,99999f,false,null,null});
            g.Tick(.05f,new Point());
            Check(g.WayfarerEncounterRoom==0&&g.Has("wayfarer_1")&&
                g.Has("wayfarer_defended_1")&&!g.Has("wayfarer_supplied_1")&&
                g.save.coins==money+20&&g.save.shards==shards+1&&
                g.save.mastery[g.save.weapon]==mastery+4,
                "Two real confirmed kills award the original unique rescue and mastery once");
            g.Talk(npc);
            Check(g.conversation!=null&&g.conversation.textEn.Contains("held off the raiders"),
                "The actual rescued traveler acknowledges the combat, not a supply donation");
            g.Close();g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.save.version==2&&restored.Has("wayfarer_defended_1")&&
                restored.WayfarerEncounterRoom==0,
                "Old SaveData v2 keeps genuine rescue choices without persisting kill counters");
        }

    }
}
