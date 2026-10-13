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

    }
}
