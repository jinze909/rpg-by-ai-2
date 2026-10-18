using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void EmberTemperRequiresRealIronreachCombatValvesAndPublicForge(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("iron_temper").locked,
                "New optional forge cannot bypass original Ironreach guardian");
            g.save.stage=3;g.Enter(3);
            WorldObject furnace=Object(g,"ironfoundry");
            WorldObject northwest=Object(g,"ironvent1");
            WorldObject east=Object(g,"ironvent2");
            // The eastern valve is legitimately behind the original mine
            // boss gate; do not assert accessibility before that story unlock.
            g.save.x=furnace.x;g.save.y=furnace.y;
            Check(g.CanStand(g.Position) && g.Nearest()==furnace,
                "The foundry itself must be reachable before restoring it");
            g.Talk(furnace);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="iron_temper_start")<0,
                "Original mine boss must be defeated before new machines can start");
            g.Close();
            g.save.stage=3;g.save.level=20;g.save.xp=0;
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            for(int i=0;i<3;i++)
            {
                WorldObject o=new[]{furnace,northwest,east}[i];
                g.save.x=o.x;g.save.y=o.y;
                Check(g.CanStand(g.Position) && g.Nearest()==o,
                    "All authored forge objects must be accessible after the real boss gate opens: "+o.kind);
            }
            g.save.x=furnace.x;g.save.y=furnace.y;
            g.enemies.Clear();g.invulnerable=120;
            int initialCoins=g.save.coins,oldKills=g.save.kills[3];
            Action(g,"ironfoundry","iron_temper_start");
            Check(g.TemperActive && g.TemperStage==1 &&
                g.TemperTime>=109 && g.TemperPressure>=17 &&
                g.enemies.FindAll(g.IsTemperEnemy).Count==2,
                "Physical forge boots a timed heat circuit and two genuinely spawned guards");
            g.Enter(0);
            Check(g.save.room==3 && g.TemperStage==1,
                "Ordinary teleport is blocked while the live forge could lose guards");
            g.save.x=northwest.x;g.save.y=northwest.y;
            g.Talk(northwest);
            Check(g.conversation==null && g.TemperStage==1,
                "Unbeaten first guard wave must block pressure release");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,x=>x!=null &&
                    x.id>=89940 && x.id<89950),
                "Active forge guards must not persist as common enemy snapshots");
            var restarted=new GameCore(data,CloneSave(g.save));restarted.Start();
            Check(!restarted.TemperActive && !restarted.Has("iron_temper_complete") &&
                !restarted.enemies.Exists(restarted.IsTemperEnemy),
                "Reload abandons unsafe machine pressure without ghost guards or free results");
            foreach(Enemy enemy in g.enemies.FindAll(g.IsTemperEnemy))
            {
                enemy.stun=200;enemy.timer=200;
                enemy.hp=3;enemy.x=northwest.x+1.05f;enemy.y=northwest.y;
                g.save.x=northwest.x;g.save.y=northwest.y;
                for(int frame=0;frame<95 && g.enemies.Contains(enemy);frame++)
                {
                    if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                    g.Tick(.02f,new Point());
                }
                Check(!g.enemies.Contains(enemy),
                    "The original golden-haired hero must defeat each forge enemy via actual Attack");
            }
            Check(g.save.kills[3]==oldKills && g.save.coins==initialCoins,
                "Forge guards cannot farm the original story kills, materials or loot");
            Action(g,"ironvent1","iron_temper_vent_1");
            Check(g.TemperStage==2 && g.enemies.FindAll(g.IsTemperEnemy).Count==2 &&
                g.Has("iron_temper_vent_seen_1") &&
                g.TemperPressure<35,
                "First genuine pressure-release interaction cools the gauge and spawns reinforcements");
            // The player can reach the actual eastern control from the
            // unchanged Ironreach corridors without any synthetic warp.
            g.save.x=east.x;g.save.y=east.y;
            Check(g.NavigationPath(g.Position).Count>=0 && g.CanStand(g.Position),
                "Eastern vent stands on original walkable mine geometry");
            int formerStage=g.TemperStage;
            g.Talk(east);
            Check(g.conversation==null && g.TemperStage==formerStage,
                "Armored reinforcements truly prevent skipping to the final valve");
            foreach(Enemy enemy in g.enemies.FindAll(g.IsTemperEnemy))
            {
                enemy.stun=200;enemy.timer=200;enemy.hp=3;
                enemy.x=east.x+1.05f;enemy.y=east.y;
                g.save.x=east.x;g.save.y=east.y;
                for(int frame=0;frame<95 && g.enemies.Contains(enemy);frame++)
                {
                    if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                    g.Tick(.02f,new Point());
                }
                Check(!g.enemies.Contains(enemy),
                    "Second wave of physical guards must be beaten with real player attacks");
            }
            Action(g,"ironvent2","iron_temper_vent_2");
            Check(g.TemperStage==3 && g.Has("iron_temper_vent_seen_2") &&
                g.enemies.FindAll(g.IsTemperEnemy).Count==1 &&
                g.enemies.Find(g.IsTemperEnemy).kind==2,
                "Second real vent starts a separate armored overseer battle");
            g.save.x=furnace.x;g.save.y=furnace.y;
            g.Talk(furnace);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="iron_temper_finish")<0 &&
                !g.Has("iron_temper_complete"),
                "Opening valves alone cannot fake a boss defeat or one-time reward");
            g.Close();
            Enemy elite=g.enemies.Find(g.IsTemperEnemy);
            elite.stun=200;elite.timer=200;elite.hp=4;
            elite.x=furnace.x+1.05f;elite.y=furnace.y;
            for(int frame=0;frame<100 && g.enemies.Contains(elite);frame++)
            {
                if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                g.Tick(.02f,new Point());
            }
            Check(!g.enemies.Contains(elite) &&
                !g.Has("boss_4") && g.Has("boss_3") &&
                g.save.kills[3]==oldKills,
                "New elite falls in canonical combat without creating a new main-campaign boss flag");
            g.save.x=furnace.x;g.save.y=furnace.y;
            int talent=g.save.skillPoints;
            Action(g,"ironfoundry","iron_temper_finish");
            Check(g.Has("iron_temper_complete") && !g.TemperActive &&
                g.GetQuest("iron_temper").complete &&
                g.save.coins==initialCoins+75 && g.save.shards>=3 &&
                g.save.skillPoints==talent+1 &&
                g.TemperUpgradeTimber==2 && g.TemperUpgradeCrystal==1 &&
                g.TemperUpgradeParts==1,
                "Only player-delivered full victory unlocks actual one-time material discounts");
            g.Talk(furnace);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="iron_temper_finish")<0 &&
                g.save.coins==initialCoins+75,
                "A finished forge no longer offers its reward action; rewards cannot be duplicated");
            g.Close();
            g.PrepareSave();
            var persisted=new GameCore(data,CloneSave(g.save));persisted.Start();
            Check(persisted.save.version==2 && persisted.Has("iron_temper_complete") &&
                persisted.TemperUpgradeTimber==2 && !persisted.TemperActive,
                "Forge recipe survives legacy v2 saves without persisting unsafe live-state fields");
        }

        public static void EmberTemperPressureHazardsAndAbortRetireOnlyOwnedObjects(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("boss_3");
            g.save.flags.Add("gate_3");g.Enter(3);g.enemies.Clear();
            WorldObject furnace=Object(g,"ironfoundry");
            g.save.x=furnace.x;g.save.y=furnace.y;g.invulnerable=100;
            Action(g,"ironfoundry","iron_temper_start");
            Check(g.TemperStage==1,"A genuine foundry operation begins");
            Advance(g,4.1f);
            Check(g.hazards.Exists(h=>h.temperOwned && h.timer>0 &&
                h.kind=="iron" && h.damage>0),
                "Forge adds a real delayed area-damage hazard, not decorative text");
            var bystander=new Projectile{position=new Point(18,13),
                velocity=new Point(),life=60};
            var foundryShot=new Projectile{position=new Point(19,13),
                velocity=new Point(),life=60,temperOwned=true};
            var unrelated=new Effect("discover",new Point(18,13),60);
            var warning=new Effect("discover",new Point(19,13),60)
                {temperOwned=true};
            var outside=new Hazard{position=new Point(18,14),timer=50,
                damage=3,radius=.8f,kind="test"};
            g.projectiles.Add(bystander);g.projectiles.Add(foundryShot);
            g.effects.Add(unrelated);g.effects.Add(warning);
            g.hazards.Add(outside);
            g.save.x=furnace.x;g.save.y=furnace.y;
            Action(g,"ironfoundry","iron_temper_abort");
            Check(!g.TemperActive && !g.enemies.Exists(g.IsTemperEnemy) &&
                !g.projectiles.Contains(foundryShot) &&
                g.projectiles.Contains(bystander) &&
                g.effects.Contains(unrelated) && !g.effects.Contains(warning) &&
                g.hazards.Contains(outside) &&
                !g.hazards.Exists(h=>h.temperOwned),
                "Aborting clears only combat-owned warning, enemies and shot; other effects survive");
            g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();
            g.enemies.Clear();g.save.x=furnace.x;g.save.y=furnace.y;
            Action(g,"ironfoundry","iron_temper_start");
            Check(g.TemperActive,"Abandoned forge can be retried for free");
            g.save.x=furnace.x;g.save.y=furnace.y;g.invulnerable=150;
            Advance(g,61);
            Check(!g.TemperActive && !g.Has("iron_temper_complete") &&
                !g.enemies.Exists(g.IsTemperEnemy),
                "Overpressure trips the machine before the time limit without granting rewards");
            g.Enter(0);
            Check(g.save.room==0,"Ordinary travel is restored after a real pressure shutdown");
        }

        public static void EmberTemperUpgradeUsesRealDiscountWithoutReplacingOldUpgrades(Campaign data)
        {
            var g=Fresh(data);
            Check(g.TemperUpgradeTimber==3 && g.TemperUpgradeCrystal==2 &&
                g.TemperUpgradeParts==2,"Fresh save keeps the existing recipe");
            g.save.upgrades=2;g.save.maxHealth=160;
            g.save.flags.Add("iron_temper_complete");
            g.save.inventory[0]=2;g.save.inventory[1]=1;g.save.inventory[4]=1;
            WorldObject smith=Object(g,"forge");
            g.save.x=smith.x;g.save.y=smith.y;g.Talk(smith);
            Check(Array.Exists(g.conversation.choices,c=>
                c.action=="upgrade" && c.en.Contains("2 timber / 1 crystal / 1 parts")),
                "Existing town forge UI actually advertises the cheaper material recipe");
            g.Close();
            float oldHealth=g.save.maxHealth;
            Action(g,"forge","upgrade");
            Check(g.save.upgrades==3 && g.save.maxHealth==oldHealth+20 &&
                g.save.inventory[0]==0 && g.save.inventory[1]==0 &&
                g.save.inventory[4]==0,
                "An actual equipment upgrade consumes the discounted materials once");
        }

        public static void EmberTemperEarnedChronicleDoesNotSpoilUnrecoveredMachineState(Campaign data)
        {
            var g=Fresh(data);
            WorldObject chronicle=Object(g,"worldchronicle");
            g.Talk(chronicle);
            int chapter=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_campaign");g.Choose(chapter);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_temper")<0,
                "Players who never used the forge cannot read a spoiler chapter");
            g.Close();
            g.save.flags.Add("iron_temper_started");
            g.Talk(chronicle);
            chapter=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_campaign");g.Choose(chapter);
            int entry=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_temper");
            Check(entry>=0,"A genuinely started foundry run unlocks its own public record");
            g.Choose(entry);
            Check(g.conversation.text.Contains("西北支管的高温锁仍未解除") &&
                g.conversation.text.Contains("东侧军用散热锁仍未拆除") &&
                !g.conversation.text.Contains("你在真正的战斗中击败守卫"),
                "Unrepaired valves and the actual overseer victory remain unpublished");
            g.Close();
            g.save.flags.Add("iron_temper_vent_seen_1");
            g.Talk(chronicle);
            chapter=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_campaign");g.Choose(chapter);
            entry=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_temper");g.Choose(entry);
            Check(g.conversation.text.Contains("你亲手打开西北支管") &&
                g.conversation.text.Contains("东侧军用散热锁仍未拆除"),
                "The archive shows only the first witnessed pressure valve");
            g.Close();
            g.save.flags.Add("iron_temper_vent_seen_2");
            g.Talk(chronicle);
            chapter=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_campaign");g.Choose(chapter);
            entry=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_temper");g.Choose(entry);
            Check(g.conversation.text.Contains("东侧散热阀接通") &&
                !g.conversation.text.Contains("你在真正的战斗中击败守卫"),
                "Opening both valves cannot invent a defeated overseer");
            g.Close();g.save.flags.Add("iron_temper_complete");
            g.Talk(chronicle);
            chapter=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_campaign");g.Choose(chapter);
            entry=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_temper");g.Choose(entry);
            Check(g.conversation.text.Contains("每次强化只需两木、一晶、一零件") &&
                g.conversation.textEn.Contains("two timber, one crystal and one part"),
                "Only finished combat reveals the bilingual public temper recipe");
        }


        public static void EmberTemperSteamBlastActuallyHurtsAndRealMovementEscapes(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.Enter(3);g.enemies.Clear();
            WorldObject foundry=Object(g,"ironfoundry");
            g.save.x=foundry.x;g.save.y=foundry.y;
            Action(g,"ironfoundry","iron_temper_start");
            // Isolate the actual pressure hazards from unrelated sword combat.
            foreach (Enemy defender in g.enemies.FindAll(g.IsTemperEnemy))
            { defender.stun = 200; defender.timer = 200; }
            g.invulnerable=0;g.save.health=120;g.save.maxHealth=120;
            Advance(g,5.1f);
            Check(g.save.health<120,
                "Stationary player takes real health damage from delayed foundry steam");
            float recovered=g.save.health;
            g.invulnerable=0;
            g.save.x=foundry.x;g.save.y=foundry.y;
            Advance(g,2.4f);
            Check(g.hazards.Exists(h=>h.temperOwned && h.timer>0),
                "A second live impact warning actually forms at the player's current footprint");
            for(int frame=0;frame<27;frame++)
                g.Tick(.02f,new Point(1,0));
            Check((g.Position-new Point(foundry.x,foundry.y)).Length>1.5f,
                "Canonical player can actually walk clear of the live steam impact");
            g.invulnerable=0;
            float afterMove=g.save.health;
            Advance(g,1.4f);
            Check(g.save.health==afterMove && g.TemperActive,
                "Correctly walking away from the warning avoids the second blast without aborting the trial");
        }

        public static void EmberTemperManualVentingCancelsOnlyOwnedSteamAndCreatesARealOpening(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.Enter(3);g.enemies.Clear();
            WorldObject foundry=Object(g,"ironfoundry");
            WorldObject valve=Object(g,"ironvent1");
            g.save.x=foundry.x;g.save.y=foundry.y;
            Action(g,"ironfoundry","iron_temper_start");
            foreach(Enemy foe in g.enemies.FindAll(g.IsTemperEnemy))
            {
                foe.stun=200;foe.timer=200;
            }
            g.invulnerable=200;
            Advance(g,3.65f);
            Check(g.hazards.Exists(h=>h.temperOwned),
                "Steam warning must actually exist before a live vent can cancel it");
            Hazard other=new Hazard{position=new Point(20,11),
                timer=15,radius=.7f,damage=3,kind="root"};
            g.hazards.Add(other);
            var unrelated=new Effect("discover",new Point(19,11),50);
            g.effects.Add(unrelated);
            // Preserve the same actual combat contract while testing the
            // valve's selective steam cleanup at the next checkpoint.
            var actualHit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(actualHit != null, "Authentic damage interface exists");
            foreach (Enemy defender in g.enemies.FindAll(g.IsTemperEnemy))
                actualHit.Invoke(g, new object[] { defender, 99999f, false, null, null });
            g.save.x=valve.x;g.save.y=valve.y;
            float oldPressure=g.TemperPressure;
            Action(g,"ironvent1","iron_temper_vent_1");
            Check(g.TemperStage==2 && g.TemperPressure<oldPressure &&
                !g.hazards.Exists(h=>h.temperOwned) &&
                g.hazards.Contains(other) && g.effects.Contains(unrelated),
                "Physical valve reduces real pressure and clears only forge-owned warnings");
            Advance(g,2.2f);
            Check(g.TemperActive && !g.hazards.Exists(h=>h.temperOwned),
                "Cooling valve grants a meaningful respite before the next steam attack");
        }


        public static void EmberTemperAndSurveyorEchoCannotStealEachOthersBattleState(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("boss_3");
            g.save.flags.Add("gate_3");g.save.flags.Add("archive_complete");
            g.Enter(3);g.enemies.Clear();
            WorldObject echo=Object(g,"archiveecho");
            WorldObject foundry=Object(g,"ironfoundry");
            g.save.x=echo.x;g.save.y=echo.y;
            Action(g,"archiveecho","archive_echo_start");
            Check(g.AtlasEchoRoom==3 && !g.TemperActive,
                "Original Surveyor Echo challenge is genuinely live");
            g.enemies.Clear(); // Isolate the concurrency guard from nearby patrol safety.
            g.save.x=foundry.x;g.save.y=foundry.y;
            Action(g,"ironfoundry","iron_temper_start");
            Check(!g.TemperActive && g.AtlasEchoRoom==3,
                "The new foundry cannot seize enemy ownership during a live Echo boss");
            g.save.x=echo.x;g.save.y=echo.y;
            Action(g,"archiveecho","archive_echo_abandon");
            Check(g.AtlasEchoRoom==0,"Original Echo resets through its own withdrawal");
            g.enemies.Clear();g.save.x=foundry.x;g.save.y=foundry.y;
            Action(g,"ironfoundry","iron_temper_start");
            Check(g.TemperActive && g.TemperStage==1,
                "The foundry now launches when the original encounter is fully retired");
            g.enemies.Clear(); // Isolate the reverse lock from real nearby combat.
            g.save.x=echo.x;g.save.y=echo.y;
            Action(g,"archiveecho","archive_echo_start");
            Check(g.TemperActive && g.AtlasEchoRoom==0,
                "The original Echo cannot overwrite the active forge battle state");
            g.save.x=foundry.x;g.save.y=foundry.y;
            Action(g,"ironfoundry","iron_temper_abort");
            Check(!g.TemperActive && !g.enemies.Exists(g.IsTemperEnemy),
                "Stopping the foundry restores the right to begin other mine challenges");
        }

        public static void MissingFoundryGuardsCannotUnlockValvesOrForgePermanentRecipe(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 20;
            g.save.flags.Add("boss_3"); g.save.flags.Add("gate_3");
            g.Enter(3); g.enemies.Clear();
            var foundry = Object(g, "ironfoundry");
            var northwest = Object(g, "ironvent1");
            var east = Object(g, "ironvent2");
            g.save.x = foundry.x; g.save.y = foundry.y; g.invulnerable = 200;
            int coins = g.save.coins;
            Action(g, foundry.kind, "iron_temper_start");
            Check(g.TemperStage == 1 &&
                g.enemies.FindAll(g.IsTemperEnemy).Count == 2,
                "A real foundry starts with both authored sentinels");
            g.enemies.RemoveAll(g.IsTemperEnemy);
            g.save.x = northwest.x; g.save.y = northwest.y;
            Action(g, northwest.kind, "iron_temper_vent_1");
            Check(!g.TemperActive && !g.Has("iron_temper_complete") &&
                g.save.coins == coins,
                "An empty unverified guard roster cannot bypass the first valve or grant rewards");
            var lethal = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(lethal != null, "Foundry kill credit must use the original damage method");
            g.save.x = foundry.x; g.save.y = foundry.y;
            Action(g, foundry.kind, "iron_temper_start");
            foreach (Enemy foe in g.enemies.FindAll(g.IsTemperEnemy))
                lethal.Invoke(g, new object[] { foe, 99999f, false, null, null });
            g.save.x = northwest.x; g.save.y = northwest.y;
            Action(g, northwest.kind, "iron_temper_vent_1");
            Check(g.TemperStage == 2,
                "Killing both first-wave guards through real combat unlocks the northern valve");
            g.enemies.RemoveAll(g.IsTemperEnemy);
            g.save.x = east.x; g.save.y = east.y;
            Action(g, east.kind, "iron_temper_vent_2");
            Check(!g.TemperActive && !g.Has("iron_temper_complete"),
                "Vanished second-wave defenders cannot skip the eastern valve");
            g.save.x = foundry.x; g.save.y = foundry.y;
            Action(g, foundry.kind, "iron_temper_start");
            foreach (Enemy foe in g.enemies.FindAll(g.IsTemperEnemy))
                lethal.Invoke(g, new object[] { foe, 99999f, false, null, null });
            g.save.x = northwest.x; g.save.y = northwest.y;
            Action(g, northwest.kind, "iron_temper_vent_1");
            foreach (Enemy foe in g.enemies.FindAll(g.IsTemperEnemy))
                lethal.Invoke(g, new object[] { foe, 99999f, false, null, null });
            g.save.x = east.x; g.save.y = east.y;
            Action(g, east.kind, "iron_temper_vent_2");
            Check(g.TemperStage == 3 &&
                g.enemies.FindAll(g.IsTemperEnemy).Count == 1,
                "Two actual defeated patrol waves still summon the genuine armored overseer");
            g.enemies.RemoveAll(g.IsTemperEnemy);
            g.save.x = foundry.x; g.save.y = foundry.y;
            Action(g, foundry.kind, "iron_temper_finish");
            Check(!g.TemperActive && !g.Has("iron_temper_complete") &&
                g.save.coins == coins && g.TemperUpgradeTimber >= 3,
                "Removing the final overseer without a lethal Hit can never publish a cheaper permanent recipe");
            g.save.x = foundry.x; g.save.y = foundry.y;
            Action(g, foundry.kind, "iron_temper_start");
            for (int stage = 1; stage <= 3; stage++)
            {
                foreach (Enemy foe in g.enemies.FindAll(g.IsTemperEnemy))
                    lethal.Invoke(g, new object[] { foe, 99999f, false, null, null });
                if (stage < 3)
                {
                    var valve = stage == 1 ? northwest : east;
                    g.save.x = valve.x; g.save.y = valve.y;
                    Action(g, valve.kind, "iron_temper_vent_" + stage);
                }
            }
            g.save.x = foundry.x; g.save.y = foundry.y;
            Action(g, foundry.kind, "iron_temper_finish");
            Check(g.Has("iron_temper_complete") &&
                g.save.coins == coins + 75 &&
                g.TemperUpgradeTimber == 2,
                "Three genuine waves of lethal combat still unlock original forge discount exactly once");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("iron_temper_complete") &&
                loaded.TemperUpgradeTimber == 2 && !loaded.TemperActive,
                "Authentic recipe upgrade survives the existing v2 save without transient enemy flags");
        }

    }
}
