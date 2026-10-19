using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject FrostPost(GameCore g,int i)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id=="frostsignal_"+i)return o;
            throw new Exception("Missing highland post "+i);
        }
        private static bool FrostChoice(GameCore g,string a)
        {
            if(g.conversation==null||g.mode!="dialog")return false;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==a){g.Choose(i);return true;}
            return false;
        }
        public static void HighlandGuardsSpawnOnDistinctUnoccupiedCombatFeet(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("desert_report");
            g.Enter(7);g.enemies.Clear();g.save.flags.Add("boss_7");
            g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            WorldObject west=FrostPost(g,1),junction=FrostPost(g,2);
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(FrostChoice(g,"frostsignal_start"),
                "The genuine highland escort begins at its authored west station");
            // Both fixed spawn positions are already occupied by living world
            // actors; the mission must select safe nearby feet or defer.
            var standing=new Enemy(91911,0,62.5f,41.5f,300f)
                {stun=100f,timer=1000f};
            g.enemies.Add(standing);
            g.companionPosition=new Point(65.5f,42.5f);
            g.save.x=junction.x;g.save.y=junction.y;
            g.Talk(junction);
            var guards=g.enemies.FindAll(g.IsFrostSignalEnemy);
            Check(g.FrostSignalStage==2&&guards.Count==2&&
                g.enemies.Contains(standing)&&
                (standing.Position-new Point(62.5f,41.5f)).Length<.001f,
                "The switchyard starts two genuine guards without deleting or moving the pre-existing fighter");
            Point[] authored={new Point(62.5f,41.5f),
                              new Point(65.5f,42.5f)};
            for(int i=0;i<guards.Count;i++)
            {
                Point foot=guards[i].Position;
                Check(g.CanStand(foot)&&
                    (foot-authored[i]).Length<=2.2f &&
                    (foot-standing.Position).Length>=1.25f &&
                    (foot-g.companionPosition).Length>=1.25f &&
                    (foot-g.Position).Length>=1.25f,
                    "A live guard must materialize on real nearby free ground without overlapping hero, ally or another foe");
            }
            Check((guards[0].Position-guards[1].Position).Length>=1.25f &&
                guards[0].id==91400&&guards[1].id==91401,
                "Two uniquely identified defenders occupy separate feet and preserve their actual quest-kill identities");
        }

        public static void LiveFrostSignalNavigationTracksActualCarriedEmber(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("desert_report");g.Enter(7);g.enemies.Clear();
            g.save.flags.Add("boss_7");g.invulnerable=250;
            WorldObject west=FrostPost(g,1),mid=FrostPost(g,2),south=FrostPost(g,3);
            Point source=new Point(west.x,west.y);
            Point junction=new Point(mid.x,mid.y);
            Point hearth=new Point(south.x,south.y);
            var initial=g.GetQuest("frost_signal");
            Check(!initial.locked&&!initial.complete&&
                !initial.steps[0].done&&(initial.target-source).Length<.001f,
                "The real highland quest starts at western pickup before carrying an ember");
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(FrostChoice(g,"frostsignal_start")&&g.FrostSignalStage==1,
                "West post starts the actual timed flame delivery");
            var carried=g.GetQuest("frost_signal");
            Check(carried.steps[0].done&&!carried.steps[1].done&&
                (carried.target-junction).Length<.001f&&
                g.NavigationTarget().HasValue&&
                (g.NavigationTarget().Value-junction).Length<.001f,
                "Immediately after pickup, the world navigation marker and journal route FORWARD to the guard junction");
            g.Talk(west);Check(FrostChoice(g,"frostsignal_abort"),
                "Returning the carried ember is a genuine cancel action");
            var aborted=g.GetQuest("frost_signal");
            Check(!aborted.steps[0].done&&aborted.Progress==0&&
                (aborted.target-source).Length<.001f&&
                g.Has("frostsignal_started"),
                "A historical started flag survives, but an aborted attempt must show no currently carried ember");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            var restored=loaded.GetQuest("frost_signal");
            Check(loaded.save.version==2&&!restored.steps[0].done&&
                (restored.target-source).Length<.001f,
                "Original v2 slots that once carried an ember restart navigation at the actual pickup post");
            g.Talk(west);Check(FrostChoice(g,"frostsignal_start"),
                "An aborted delivery can really restart");
            g.save.x=mid.x;g.save.y=mid.y;g.Talk(mid);
            Check(g.FrostSignalStage==2&&
                (g.GetQuest("frost_signal").target-junction).Length<.001f,
                "With live defenders, the marker remains at their real junction");
            g.Close();
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"The real gameplay kill path must be present");
            foreach(Enemy guard in g.enemies.FindAll(g.IsFrostSignalEnemy))
                hit.Invoke(g,new object[]{guard,99999f,false,null,null});
            g.Talk(mid);
            Check(FrostChoice(g,"frostsignal_free")&&g.FrostSignalStage==3,
                "Only genuine guard defeat unlocks the southbound delivery");
            var delivered=g.GetQuest("frost_signal");
            Check(delivered.steps[0].done&&delivered.steps[1].done&&
                (delivered.target-hearth).Length<.001f&&
                g.NavigationTarget().HasValue&&
                (g.NavigationTarget().Value-hearth).Length<.001f,
                "After the mid-quest choice, both live guidance and the journal lead to the civilian hearth");
            g.save.x=south.x;g.save.y=south.y;g.Talk(south);
            Check(FrostChoice(g,"frostsignal_guides")&&
                g.GetQuest("frost_signal").complete&&
                g.GetQuest("frost_signal").Progress==1f,
                "Permanent civilian completion remains recorded as finished independently of transient active mission state");
        }

        public static void HighlandSignalGuardsCannotSpawnInIsolatedWalkablePocket(Campaign data)
        {
            // An isolated tile is legal standing ground but a melee guard
            // spawned inside it can never reach the live timed encounter.
            // Reuse the actual authored post coordinates and real path graph.
            MapData old=data.maps[7];
            try
            {
                var rows=new string[80];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] cells=new string('#',120).ToCharArray();
                    if(y>=2&&y<78)
                        for(int x=2;x<118;x++)cells[x]='.';
                    // Seal the original first-guard tile in its own 1x1
                    // legal pocket without filling the center itself.
                    if(y>=40&&y<=42)
                        for(int x=61;x<=63;x++)
                            if(x!=62||y!=41)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[7]=new MapData{id=7,spawnX=58.5f,
                    spawnY=35.5f,rows=rows,
                    objects=old.objects,decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("desert_report");g.Enter(7);g.enemies.Clear();
                g.save.flags.Add("boss_7");g.invulnerable=120;
                Point trapped=new Point(62.5f,41.5f);
                WorldObject west=FrostPost(g,1),mid=FrostPost(g,2);
                g.save.x=mid.x;g.save.y=mid.y;
                Check(g.CanStand(trapped)&&g.NavigationPath(trapped).Count==0,
                    "The sealed first spawn pocket has legal floor but no genuine reachable route to the junction");
                g.save.x=west.x;g.save.y=west.y;
                g.Talk(west);
                Check(FrostChoice(g,"frostsignal_start"),
                    "The real timed highland mission starts from the old western signal");
                g.save.x=mid.x;g.save.y=mid.y;g.Talk(mid);
                var guards=g.enemies.FindAll(g.IsFrostSignalEnemy);
                Check(g.FrostSignalStage==2&&guards.Count==2,
                    "An accessible alternate local guard pair begins the genuine mid-quest battle");
                foreach(Enemy guard in guards)
                {
                    Check((guard.Position-trapped).Length>.1f&&
                        g.NavigationPath(guard.Position).Count>0&&
                        g.CanStand(guard.Position),
                        "No live guard is marooned in the disconnected authored pocket: each has a real walking route to the player");
                }
            }
            finally{data.maps[7]=old;}
        }

        public static void CivicEmberAbortAndFailurePreserveUnrelatedWorldEffects(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("desert_report");g.Enter(7);g.enemies.Clear();
            g.save.flags.Add("boss_7");g.invulnerable=400;
            WorldObject source=FrostPost(g,1),switchyard=FrostPost(g,2);
            g.save.x=source.x;g.save.y=source.y;
            int beforeMoney=g.save.coins,beforeShards=g.save.shards;
            g.Talk(source);
            Check(FrostChoice(g,"frostsignal_start")&&g.FrostSignalStage==1,
                "Authentic west signal begins the new public trip");
            var unrelated=new Hazard{position=new Point(22,14),damage=8,radius=1,timer=10};
            var owned=new Hazard{position=new Point(24,14),damage=8,radius=1,timer=10,frostSignalOwned=true};
            var unrelatedShot=new Projectile{position=new Point(22,14),damage=7,life=3};
            var ownedShot=new Projectile{position=new Point(24,14),damage=7,life=3,frostSignalOwned=true};
            g.hazards.Add(unrelated);g.hazards.Add(owned);
            g.projectiles.Add(unrelatedShot);g.projectiles.Add(ownedShot);
            g.Talk(source);
            Check(FrostChoice(g,"frostsignal_abort")&&!g.FrostSignalActive,
                "Physical west post explicitly cancels the entire old signal run");
            Check(g.hazards.Contains(unrelated)&&!g.hazards.Contains(owned)&&
                g.projectiles.Contains(unrelatedShot)&&!g.projectiles.Contains(ownedShot),
                "Retiring one civil rescue removes only mission-owned hazards and missiles");
            Check(g.save.coins==beforeMoney&&g.save.shards==beforeShards&&
                !g.FrostSignalComplete,
                "Voluntary return never awards fictional residents' success");
            g.Talk(source);
            Check(FrostChoice(g,"frostsignal_start"),"Returned signal can be retried");
            g.save.x=switchyard.x;g.save.y=switchyard.y;
            g.Talk(switchyard);
            Check(g.FrostSignalStage==2&&g.enemies.Exists(g.IsFrostSignalEnemy),
                "The guard checkpoint has real combat participants during retry");
            g.Close();
            // Keep the authentic guard roster alive but safely immobilized
            // so the separate real ember expiry can be measured without
            // manufacturing a false defender disappearance.
            foreach (Enemy guard in g.enemies.FindAll(g.IsFrostSignalEnemy))
            { guard.stun = 200; guard.timer = 200; }
            g.save.x=source.x;g.save.y=source.y;
            for(int i=0;i<2200&&g.FrostSignalActive;i++)g.Tick(.05f,new Point());
            Check(!g.FrostSignalActive&&!g.FrostSignalComplete&&
                g.save.coins==beforeMoney&&g.save.shards==beforeShards,
                "A real expiring ember resets safely with no repeatable reward");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(!restored.FrostSignalActive&&restored.save.version==2,
                "Expired temporary highland missions do not leak into old v2 saves");
        }

        public static void HighlandCivicEmberHasActualColdCombatAndPublicChoices(Campaign data)
        {
            foreach(int branch in new[]{1,2})
            {
                var g=Fresh(data);
                g.save.flags.Add("desert_report");
                g.save.stage=3;g.Enter(7);g.enemies.Clear();
                Check(g.save.room==7,"Existing desert report legitimately unlocks original Frostchime area");
                Check(g.GetQuest("frost_signal").locked,"Highland civilian task respects the original boss gate");
                WorldObject west=FrostPost(g,1),mid=FrostPost(g,2),south=FrostPost(g,3);
                foreach(WorldObject post in new[]{west,mid,south})
                {
                    Check(g.CanStand(new Point(post.x,post.y)),
                        "Each authored signal exists on old walkable geometry");
                    g.save.x=data.maps[7].spawnX;g.save.y=data.maps[7].spawnY;
                    Check(g.NavigationPath(new Point(post.x,post.y)).Count>0,
                        "An old region spawn can reach every actual highland post");
                }
                g.save.x=west.x;g.save.y=west.y;g.Talk(west);
                Check(g.conversation.choices.Length==1,"Cannot start before original guardian");g.Close();
                g.save.flags.Add("boss_7");
                int startCoins=g.save.coins,startKills=g.save.kills[7];
                g.invulnerable=120;
                g.Talk(west);
                Check(FrostChoice(g,"frostsignal_start")&&g.FrostSignalStage==1&&
                    g.FrostSignalTime==100&&g.FrostSignalFlame==85,
                    "Original physical post starts a real 100s carried ember");
                g.Enter(0);Check(g.save.room==7&&g.FrostSignalActive,
                    "An active real ember cannot disappear through region travel");
                g.PrepareSave();var halfway=new GameCore(data,CloneSave(g.save));
                Check(!halfway.FrostSignalActive&&halfway.save.version==2,
                    "Unfinished fires never persist into old v2 slots");
                float before=g.FrostSignalFlame;g.save.cold=65;
                g.Tick(.05f,new Point());
                Check(g.FrostSignalFlame<before-.05f,"Severe real cold drains active fire");
                g.save.x=south.x;g.save.y=south.y;g.Talk(south);
                Check(g.conversation==null&&g.FrostSignalStage==1,
                    "Remote finale cannot skip the original junction");
                g.save.x=mid.x;g.save.y=mid.y;g.Talk(mid);
                Check(g.FrostSignalStage==2&&g.enemies.FindAll(g.IsFrostSignalEnemy).Count==2,
                    "Actual middle post spawns two real hostile actors");
                g.Close();g.Talk(mid);
                Check(!FrostChoice(g,"frostsignal_herb"),
                    "Unbeaten enemies cannot grant a free checkpoint");
                g.Close();
                g.PrepareSave();Check(!Array.Exists(g.save.worldEnemies,e=>
                    e!=null&&e.id>=91400&&e.id<91410),
                    "Owned temporary guards never contaminate ordinary world saves");
                foreach(Enemy guard in g.enemies.FindAll(g.IsFrostSignalEnemy))
                {
                    guard.stun=200;guard.timer=200;guard.hp=3;
                    guard.x=mid.x+1.05f;guard.y=mid.y;
                    g.save.x=mid.x;g.save.y=mid.y;
                    for(int frame=0;frame<130&&g.enemies.Contains(guard);frame++)
                    {
                        if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                        g.Tick(.02f,new Point());
                    }
                    Check(!g.enemies.Contains(guard),"Actual hero combat clears the authored switch guards");
                }
                Check(g.save.kills[7]==startKills&&g.save.coins==startCoins,
                    "Special combat does not mint regular story kills or drops");
                g.save.cold=40;g.save.inventory[2]=1;g.save.energy=100;
                g.Talk(mid);string resource=branch==1?"frostsignal_herb":"frostsignal_energy";
                Check(FrostChoice(g,resource)&&g.FrostSignalStage==3,
                    "Real player chooses herbal or stamina-based flame recovery");
                Check(branch==1?g.save.inventory[2]==0:g.save.energy==80,
                    "Actual inventory or stamina is charged by the selected route");
                g.save.x=south.x;g.save.y=south.y;g.Talk(south);
                Check(g.conversation!=null&&g.conversation.choices.Length==2,
                    "Only completed combat and resource stages enable a public ending");
                string finale=branch==1?"frostsignal_clinic":"frostsignal_guides";
                Check(FrostChoice(g,finale)&&g.FrostSignalComplete&&
                    g.FrostSignalOutcome==branch&&g.save.coins>=startCoins+55,
                    "The physical last post awards one permanent civilian choice");
                g.Talk(south);Check(g.conversation.choices.Length==1,
                    "No duplicate final choice or repeatable compensation");g.Close();
                // Sen's original regional NPC genuinely observes this outcome
                // without replacing his original quest or companion choices.
                g.save.flags.Add("mountain_report");
                WorldObject keeper=null;
                foreach(WorldObject o in g.Map.objects)
                    if(o.kind=="keeper"){keeper=o;break;}
                Check(keeper!=null,"The old frost NPC still exists");
                g.save.x=keeper.x;g.save.y=keeper.y;g.Talk(keeper);
                Check(g.conversation!=null&&
                    (branch==1?g.conversation.text.Contains("伤者"):
                               g.conversation.text.Contains("巡路队")),
                    "Sen's existing dialogue responds to the selected civilian service");
                g.Close();
                // After checking Sen in the original settlement, return to
                // the physically restored southern hearth to measure its benefit.
                g.save.x=south.x;g.save.y=south.y;
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                g.save.health=60;g.save.energy=0;g.save.cold=40;
                // Resolve residual attack hit-stop in real simulation before
                // evaluating environment and safe-service ticks.
                for(int settle=0;settle<6;settle++)g.Tick(.05f,new Point());
                Check(g.FrostSignalRefuge&&g.save.cold<40&&
                    (branch==1?g.save.health>60.05f:g.save.energy>.75f),
                    "Each outcome truly creates its own safe local refuge benefit");
                g.save.x=data.maps[7].spawnX;g.save.y=data.maps[7].spawnY;
                Check(!g.FrostSignalRefuge,"No remote free regeneration");
                g.PrepareSave();var saved=new GameCore(data,CloneSave(g.save));
                Check(saved.save.version==2&&saved.FrostSignalComplete&&
                    saved.FrostSignalOutcome==branch&&saved.GetQuest("frost_signal").complete,
                    "Old version-two saves retain each independently chosen public outcome");
            }
        }
        public static void HighlandEmberNeedsActualTwoGuardDefeatBeforeCivilianRestoration(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            g.save.flags.Add("desert_report"); g.Enter(7); g.enemies.Clear();
            g.save.flags.Add("boss_7"); g.invulnerable = 200;
            var west = FrostPost(g, 1); var middle = FrostPost(g, 2);
            var south = FrostPost(g, 3);
            int coins = g.save.coins, shards = g.save.shards;
            g.save.x = west.x; g.save.y = west.y;
            Action(g, west.kind, "frostsignal_start");
            g.save.x = middle.x; g.save.y = middle.y;
            g.Talk(middle); g.Close();
            Check(g.FrostSignalStage == 2 &&
                g.enemies.FindAll(g.IsFrostSignalEnemy).Count == 2,
                "Original highland switchyard deploys both genuine security actors");
            g.enemies.RemoveAll(g.IsFrostSignalEnemy);
            g.Tick(.05f, new Point());
            Check(!g.FrostSignalActive && !g.FrostSignalComplete &&
                g.save.coins == coins && g.save.shards == shards,
                "No civilian service or permanent money can be earned by deleting an unconfirmed guard roster");
            g.save.x = west.x; g.save.y = west.y;
            Action(g, west.kind, "frostsignal_start");
            g.save.x = middle.x; g.save.y = middle.y;
            g.Talk(middle); g.Close();
            Check(g.FrostSignalStage == 2, "Abandoned encounter restarts normally");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The original player damage path exists");
            foreach (Enemy guard in g.enemies.FindAll(g.IsFrostSignalEnemy))
                hit.Invoke(g, new object[] { guard, 99999f, false, null, null });
            g.save.x = middle.x; g.save.y = middle.y;
            g.Talk(middle);
            Check(FrostChoice(g, "frostsignal_free"),
                "Original middle highland post offers free flame conservation only after verified kills");
            Check(g.FrostSignalStage == 3 &&
                !g.FrostSignalComplete && g.FrostSignalFlame > 0,
                "Actually defeating two enemies opens optional free flame conservation");
            g.save.x = south.x; g.save.y = south.y;
            g.Talk(south);
            Check(FrostChoice(g, "frostsignal_clinic"),
                "Original southern hearth presents the public clinic decision after authentic switchyard combat");
            Check(g.FrostSignalComplete && g.FrostSignalOutcome == 1 &&
                g.save.coins >= coins + 55 && g.save.shards == shards + 2,
                "The true real-combat ember trip still permanently opens its authored public clinic");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.FrostSignalComplete && loaded.FrostSignalOutcome == 1 &&
                loaded.save.version == 2 && !loaded.FrostSignalActive,
                "Authentic highland clinic remains valid in original v2 save format");
        }

        public static void HighlandEmberWindbreakPaysOnlyAfterCombatAndExpires(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("desert_report");g.Enter(7);g.enemies.Clear();
            g.save.flags.Add("boss_7");g.invulnerable=200;
            WorldObject west=FrostPost(g,1),mid=FrostPost(g,2),south=FrostPost(g,3);
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(FrostChoice(g,"frostsignal_start")&&g.FrostSignalStage==1,
                "Windbreak route begins with an authentic western ember");
            g.save.x=mid.x;g.save.y=mid.y;g.Talk(mid);
            Check(g.FrostSignalStage==2&&
                g.enemies.FindAll(g.IsFrostSignalEnemy).Count==2,
                "Windbreak still requires the real two-guard encounter");
            Check(!FrostChoice(g,"frostsignal_windbreak")&&
                g.FrostSignalWindbreakTime==0,
                "Live guards cannot expose the protected option");
            g.Close();
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Authoritative real-combat lethal callback must exist");
            foreach(Enemy guard in g.enemies.FindAll(g.IsFrostSignalEnemy))
                hit.Invoke(g,new object[]{guard,99999f,false,null,null});
            g.save.energy=19;g.Talk(mid);
            Check(FrostChoice(g,"frostsignal_windbreak")&&
                g.FrostSignalStage==2&&g.FrostSignalWindbreakTime==0&&
                g.save.energy==19,
                "Insufficient energy cannot award windbreak or advance the mission");
            g.save.energy=100;g.Talk(mid);
            Check(FrostChoice(g,"frostsignal_windbreak")&&
                g.FrostSignalStage==3&&g.FrostSignalWindbreakTime==25f&&
                g.save.energy==80,
                "Earned escort shelter starts for 25s and deducts exactly 20 energy");
            g.Talk(mid);
            Check(g.conversation==null&&g.FrostSignalWindbreakTime==25f,
                "Completed switchyard cannot recharge or repeat resource payments");
            g.save.cold=90;float warmth=g.FrostSignalFlame;
            g.Tick(.05f,new Point());
            Check(g.FrostSignalWindbreakTime>24.9f&&
                g.FrostSignalFlame>warmth-.05f&&g.FrostSignalFlame<warmth,
                "Actual severe-cold Tick burns flame more slowly while shielded");
            Check(g.GetQuest("frost_signal").next.Contains("防风"),
                "Real objective HUD shows timed shelter in Chinese");
            g.save.english=true;
            Check(g.GetQuest("frost_signal").next.Contains("Windbreak"),
                "Real objective HUD shows timed shelter in English");
            g.save.english=false;
            for(int i=0;i<500&&g.FrostSignalActive;i++)
                g.Tick(.05f,new Point());
            Check(g.FrostSignalStage==3&&g.FrostSignalWindbreakTime==0f,
                "The actual escort timer expires the windbreak without cancelling the ember");
            g.save.cold=90;warmth=g.FrostSignalFlame;
            g.Tick(.05f,new Point());
            Check(warmth-g.FrostSignalFlame>.06f,
                "After expiry the original harsh-cold loss returns");
            g.save.x=south.x;g.save.y=south.y;g.Talk(south);
            Check(FrostChoice(g,"frostsignal_guides")&&g.FrostSignalComplete&&
                !g.FrostSignalActive&&g.FrostSignalWindbreakTime==0,
                "Timed shelter does not block the physical public-hearth finale");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FrostSignalOutcome==2&&
                restored.FrostSignalWindbreakTime==0,
                "Old v2 save keeps the permanent roadwarden choice, never transient windbreak");
        }

    }
}
