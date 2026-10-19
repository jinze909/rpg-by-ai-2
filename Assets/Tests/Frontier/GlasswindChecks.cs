using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void GlasswindBothWavesSpawnAwayFromExistingCombatActors(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            g.save.flags.Add("gate_6");
            g.save.flags.Add("glasswind_started");
            g.Enter(6);g.enemies.Clear();
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.invulnerable=180f;
            WorldObject station=Object(g,"glasswindrelay1");
            g.save.x=station.x;g.save.y=station.y;
            // Opening an actual relay requires SafeTravel: ordinary
            // enemies cannot stand within six tiles at the START. Position
            // the companion at a favorite first-wave foot, then insert an
            // unrelated active-world bystander AFTER the allowed start, so
            // the second wave must choose a different nearby arena tile.
            Point originalFirst=new Point(station.x-3.2f,station.y);
            Point originalSecond=new Point(station.x,station.y+3.2f);
            var bystander=new Enemy(91999,0,station.x+12f,station.y+8f,900f)
                {stun=200f,timer=1000f};
            g.enemies.Add(bystander);
            g.companionPosition=originalSecond;
            Check(g.CanStand(originalFirst)&&g.CanStand(originalSecond)&&
                (g.Position-bystander.Position).Length>6f,
                "The real relay may start safely, even with an existing distant wilderness fighter");
            Action(g,station.kind,"glasswind_start_1");
            var first=g.enemies.FindAll(g.IsGlasswindEnemy);
            Check(g.GlasswindSite==1&&g.GlasswindWave==1&&first.Count==2&&
                g.enemies.Contains(bystander),
                "A real relay capture deploys its complete first wave without deleting ambient threats");
            foreach(Enemy e in first)
            {
                Check((e.Position-bystander.Position).Length>=1.55f&&
                    (e.Position-g.companionPosition).Length>=1.55f&&
                    (e.Position-g.Position).Length>=1.75f&&
                    g.CanStand(e.Position)&&g.NavigationPath(e.Position).Count>0,
                    "First-wave fighters occupy accessible ground without overlapping the hero, ally or other living enemy");
                e.stun=200f;e.timer=1000f;
            }
            Check((first[0].Position-first[1].Position).Length>=2.3f,
                "A single first wave never spawns overlapping combat roots");
            // First-wave combat is already active. An ordinary wilderness
            // actor may enter the next reinforcement's favored location.
            bystander.x=station.x+2.8f;
            bystander.y=station.y+2.8f;
            Advance(g,4.25f);
            var both=g.enemies.FindAll(g.IsGlasswindEnemy);
            Check(g.GlasswindSite==1&&g.GlasswindWave==2&&both.Count==4,
                "The real ring charge spawns a second complete wave after four seconds");
            for(int i=0;i<both.Count;i++)
            {
                Enemy current=both[i];
                Check(g.CanStand(current.Position)&&
                    (current.Position-bystander.Position).Length>=1.55f&&
                    (current.Position-g.companionPosition).Length>=1.55f&&
                    (current.Position-g.Position).Length>=1.75f,
                    "Later reinforcements cannot overlap any preserved living actor");
                for(int j=0;j<i;j++)
                    Check((current.Position-both[j].Position).Length>=1.55f,
                        "Wave-two combatants stay clear of BOTH still-living first-wave fighters");
            }
            Check(g.save.version==2&&!g.Has("glasswind_1"),
                "Additional arena checks do not unlock a reward or alter an existing v2 save");
        }

        public static void GlasswindPublicShadeOnlyProtectsPhysicallyAccessibleTravelers(Campaign data)
        {
            // An already-restored valve is a physical public refuge.
            // Simply being within three tiles on the other side of a
            // solid wall must not cool the hero without actual access.
            MapData original=data.maps[6];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='#';
                    rows[y]=new string(cells);
                }
                WorldObject relay=new WorldObject {
                    id="glasswindrelay1",kind="glasswindrelay1",
                    name="Civic wind valve",en="Civic wind valve",
                    x=12.5f,y=10.5f};
                data.maps[6]=new MapData {
                    id=6,spawnX=10.5f,spawnY=10.5f,
                    rows=rows,objects=new[]{relay},
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("harbor_report");g.Enter(6);
                g.enemies.Clear();
                g.save.flags.Add("glasswind_1");
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.heat=40f;g.save.teaUntil=0;
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(relay.x,relay.y))&&
                    g.Blocked(11.5f,10.5f),
                    "A real unbroken wall separates an otherwise nearby restored public wind valve");
                Check(!g.GlasswindRefuge,
                    "Permanent public shade must not protect the hero across sealed scenery");
                g.Tick(.05f,new Point());
                Check(g.save.heat>40.045f,
                    "Sandreach climate advances actual heat when public shade is physically inaccessible");
                char[] opened=rows[10].ToCharArray();
                opened[11]='.';rows[10]=new string(opened);
                Check(g.GlasswindRefuge,
                    "Opening the exact barrier immediately activates the existing completed valve");
                g.Tick(.05f,new Point());
                Check(g.save.heat<40f&&g.save.version==2,
                    "The same valve now cools the real hero without a save migration or duplicate quest award");
            }
            finally {data.maps[6]=original;}
        }

        public static void RelayCaptureCannotBeJammedThroughClosingRealWall(Campaign data)
        {
            // The real contest starts without nearby hostile actors, then
            // a living existing wave fighter moves next to the relay while
            // a dynamic wall physically separates their contact lanes.
            MapData original=data.maps[6];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='#';
                    rows[y]=new string(cells);
                }
                WorldObject relay=new WorldObject {
                    id="glasswindrelay1",kind="glasswindrelay1",
                    name="Public wind valve",en="Public wind valve",
                    x=10.7f,y=10.5f};
                data.maps[6]=new MapData {id=6,
                    spawnX=relay.x,spawnY=relay.y,
                    rows=rows,objects=new[]{relay},
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.save.flags.Add("gate_6");
                g.save.flags.Add("glasswind_started");
                g.Enter(6);g.enemies.Clear();
                g.save.x=relay.x;g.save.y=relay.y;
                g.invulnerable=120f;
                Check(g.CanStand(g.Position)&&g.Blocked(11.5f,10.5f),
                    "The true hero can hold the valve beside a dynamic full-wall barrier");
                Action(g,relay.kind,"glasswind_start_1");
                var guards=g.enemies.FindAll(g.IsGlasswindEnemy);
                Check(g.GlasswindSite==1&&g.GlasswindWave==1&&
                    guards.Count==2,
                    "The normal safety checks still launch two authentic timed relay defenders");
                Enemy blocked=guards[0],remote=guards[1];
                blocked.x=12.23f;blocked.y=10.5f;
                blocked.stun=120;blocked.timer=1000;
                remote.x=19.5f;remote.y=19.5f;
                remote.stun=120;remote.timer=1000;
                Check(g.CanStand(blocked.Position)&&
                    (blocked.Position-g.Position).Length<1.6f&&
                    g.Blocked(11.5f,10.5f),
                    "A live guard is within the contest ring but genuinely separated by one solid wall");
                float before=g.GlasswindCharge;
                g.Tick(.05f,new Point());
                Check(g.GlasswindCharge>before+.04f,
                    "An active relay must charge while its only nearby guard is physically unable to reach it");
                char[] opened=rows[10].ToCharArray();
                opened[11]='.';rows[10]=new string(opened);
                float charged=g.GlasswindCharge;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.GlasswindCharge-charged)<.0001f,
                    "Opening the same wall immediately lets the still-living real guard contest the capture");
                opened[11]='#';rows[10]=new string(opened);
                g.Tick(.05f,new Point());
                Check(g.GlasswindCharge>charged+.04f&&
                    g.GlasswindSite==1&&!g.Has("glasswind_1")&&
                    g.save.version==2,
                    "Sealing the path again resumes progress without despawning defenders or claiming permanent rewards");
            }
            finally {data.maps[6]=original;}
        }

        public static void CrowdedSecondGlasswindWaveWaitsWithoutDiscardingLiveDefense(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"harbor_report","desert_report",
                "gate_6","glasswind_started"})g.save.flags.Add(flag);
            g.Enter(6);g.enemies.Clear();g.invulnerable=180;
            WorldObject valve=Object(g,"glasswindrelay1");
            g.save.x=valve.x;g.save.y=valve.y;
            Action(g,valve.kind,"glasswind_start_1");
            Check(g.GlasswindSite==1&&g.GlasswindWave==1&&
                g.enemies.FindAll(g.IsGlasswindEnemy).Count==2,
                "The real northern relay starts with two original guards");
            foreach(Enemy guard in g.enemies.FindAll(g.IsGlasswindEnemy))
            {guard.stun=120;guard.timer=120;}
            // Crowd all eight authored potential reinforcement feet.
            // Existing original wave-one guards keep their actual positions.
            Point[] offsets={new Point(3.2f,0),new Point(-3.2f,0),
                new Point(0,3.2f),new Point(0,-3.2f),
                new Point(2.8f,2.8f),new Point(-2.8f,-2.8f),
                new Point(-2.8f,2.8f),new Point(2.8f,-2.8f)};
            var blockers=new System.Collections.Generic.List<Enemy>();
            foreach(Point offset in offsets)
            {
                Point spot=new Point(valve.x,valve.y)+offset;
                if(!g.CanStand(spot))continue;
                bool alreadyCovered=false;
                foreach(Enemy guard in g.enemies)
                    if(guard.hp>0&&(guard.Position-spot).Length<1.55f)
                    {alreadyCovered=true;break;}
                if(alreadyCovered)continue;
                var bystander=new Enemy(92600+blockers.Count,0,
                    spot.x,spot.y,900){stun=200,timer=1000};
                g.enemies.Add(bystander);blockers.Add(bystander);
            }
            Check(blockers.Count>=4,
                "At least four genuine remaining authored wave-two sites are blocked by living actors");
            float firstDeadline=g.GlasswindTime;
            Advance(g,4.25f);
            Check(g.GlasswindSite==1&&g.GlasswindWave==1&&
                Math.Abs(g.GlasswindCharge-4f)<.02f&&
                g.enemies.FindAll(g.IsGlasswindEnemy).Count==2&&
                g.GlasswindTime<firstDeadline-4.1f,
                "Full second-wave occupancy retains genuine first-wave guards and exact four-second progress while countdown continues");
            float held=g.GlasswindTime;
            Advance(g,.45f);
            Check(g.GlasswindSite==1&&g.GlasswindWave==1&&
                Math.Abs(g.GlasswindCharge-4f)<.02f&&
                g.GlasswindTime<held-.4f,
                "Repeated deployment failure neither restarts the trial nor freezes its real time limit");
            foreach(Enemy blocker in blockers)g.enemies.Remove(blocker);
            Advance(g,.15f);
            Check(g.GlasswindSite==1&&g.GlasswindWave==2&&
                g.enemies.FindAll(g.IsGlasswindEnemy).Count==4&&
                g.GlasswindCharge>=4f&&g.GlasswindCharge<4.2f,
                "Clearing actual reinforcement footing deploys exactly two new living fighters without losing progress");
            Advance(g,4.15f);
            Check(g.GlasswindCharge>=8f&&!g.Has("glasswind_1")&&
                g.GlasswindWave==2&&g.save.version==2,
                "The conserved charge resumes to full meter but still requires four real kills and a manual seal");
        }

        public static void GlasswindCircuitHasRealCaptureWavesAndPermanentShade(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("glasswind").locked,"Old saves cannot start the optional quest without the desert report");
            WorldObject mia=Object(g,"glasswindkeeper");
            g.save.x=mia.x;g.save.y=mia.y;
            Check(g.CanStand(g.Position) && g.Nearest()==mia,"Mia must appear on walkable town ground");
            g.Talk(mia);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_accept")<0,
                "Mia cannot bypass the original main campaign desert investigation");
            g.Close();
            g.save.stage=3;g.save.level=20;g.save.xp=0;
            g.save.flags.Add("harbor_report");g.save.flags.Add("desert_report");
            // Original desert story physically opens the protected eastern gate.
            // A fabricated late-game fixture must not leave that barrier closed.
            g.save.flags.Add("gate_6");
            Action(g,"glasswindkeeper","glasswind_accept");
            Check(g.Has("glasswind_started") && g.save.trackedQuest=="glasswind",
                "The physical NPC starts the actual tracked quest");
            g.Enter(6);g.enemies.Clear();g.invulnerable=250;
            int initialCoins=g.save.coins,initialShards=g.save.shards,
                normalKills=g.save.kills[6];
            for(int site=1;site<=3;site++)
            {
                WorldObject valve=Object(g,"glasswindrelay"+site);
                g.save.x=valve.x;g.save.y=valve.y;
                Check(g.CanStand(g.Position) && g.Nearest()==valve,
                    "Every authored valve is accessible and interactable");
                Action(g,valve.kind,"glasswind_start_"+site);
                Check(g.GlasswindSite==site && g.GlasswindWave==1 &&
                    g.enemies.FindAll(g.IsGlasswindEnemy).Count==2,
                    "First wave must actually deploy two reachable enemies");
                g.Enter(0);
                Check(g.save.room==6 && g.GlasswindSite==site,
                    "Ordinary portal/travel calls cannot erase an active capture mission");
                g.PrepareSave();
                Check(!Array.Exists(g.save.worldEnemies,r=>r!=null &&
                    r.id>=89810 && r.id<89900),
                    "Live trial enemies may not be stored as ordinary patrols");
                var interrupted=new GameCore(data,CloneSave(g.save));
                interrupted.Start();
                Check(interrupted.GlasswindSite==0 &&
                    !interrupted.enemies.Exists(interrupted.IsGlasswindEnemy) &&
                    !interrupted.Has("glasswind_"+site),
                    "Reload restarts incomplete trials without ghost enemies or fake clears");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                g.save.x=valve.x+4.5f;g.save.y=valve.y;
                Advance(g,1);
                Check(g.GlasswindCharge==0,"Waiting far from a valve cannot capture it");
                g.save.x=valve.x;g.save.y=valve.y;
                Advance(g,4.25f);
                Check(g.GlasswindWave==2 &&
                    g.enemies.FindAll(g.IsGlasswindEnemy).Count==4,
                    "Half-charged territory produces a genuine second enemy wave");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.2f);
                Check(g.GlasswindCharge>=8 &&
                    !g.Has("glasswind_"+site),
                    "A full capture meter alone cannot bypass the real combat objective");
                int coins=g.save.coins;
                Action(g,valve.kind,"glasswind_withdraw");
                Check(g.GlasswindSite==0 && !g.enemies.Exists(g.IsGlasswindEnemy) &&
                    g.save.coins==coins && !g.Has("glasswind_"+site),
                    "Withdrawal retires enemies without fake loot, flags or rewards");
                Action(g,valve.kind,"glasswind_start_"+site);
                Check(g.GlasswindSite==site,"Withdrawal allows a real free retry");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.25f);
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.2f);
                Check(g.GlasswindWave==2 && g.GlasswindCharge>=8,
                    "Replayed fight must still require both waves and full capture");
                // Use the canonical player's actual combat input to finish,
                // rather than granting quest progress by deleting enemies.
                var opponents=g.enemies.FindAll(g.IsGlasswindEnemy);
                foreach(Enemy e in opponents)
                {
                    e.x=valve.x+1.05f;e.y=valve.y;e.hp=3;
                    e.stun=200;e.timer=200;
                    g.save.x=valve.x;g.save.y=valve.y;
                    g.save.energy=100;
                    for(int frame=0;frame<100 && g.enemies.Contains(e);frame++)
                    {
                        if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                        g.Tick(.02f,new Point());
                    }
                    Check(!g.enemies.Contains(e),"Normal hero attacks must defeat trial machines");
                }
                Check(g.save.kills[6]==normalKills && g.save.coins==coins,
                    "Scripted combat cannot farm chapter kills or ordinary coins");
                Action(g,valve.kind,"glasswind_seal");
                Check(g.Has("glasswind_"+site) && g.GlasswindSite==0 &&
                    g.save.coins==coins+15 && g.save.shards==initialShards+site,
                    "Only actual physical sealing grants one site's reward");
                Check(g.GlasswindRefuge,"Sealed valves offer genuine local heat protection");
                g.save.heat=50;Advance(g,1);
                Check(g.save.heat<50,"Heat falls under a restored valve in the real climate tick");
                g.Talk(valve);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_seal")<0 &&
                    g.save.coins==coins+15,
                    "The completed valve removes its reward action instead of paying twice");
                g.Close();
            }
            Check(g.Has("glasswind_all_sealed") && !g.Has("glasswind_complete") &&
                g.save.coins==initialCoins+45,
                "Three site clears do not silently grant the final town reward");
            g.Enter(0);g.save.x=mia.x;g.save.y=mia.y;
            int finalCoins=g.save.coins,finalShards=g.save.shards,
                finalTalent=g.save.skillPoints;
            Action(g,"glasswindkeeper","glasswind_publish");
            Check(g.GetQuest("glasswind").complete &&
                g.save.coins==finalCoins+65 && g.save.shards==finalShards+2 &&
                g.save.skillPoints==finalTalent+1,
                "Public handoff grants the final resources and one talent exactly once");
            g.Talk(mia);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_publish")<0 &&
                g.save.coins==finalCoins+65,
                "Mia withdraws the one-time publication action after completion");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2 && loaded.Has("glasswind_complete") &&
                loaded.Has("glasswind_1") && loaded.Has("glasswind_3"),
                "Older v2 storage preserves all new permanent flags without a schema change");
        }

        public static void GlasswindChronicleRevealsOnlyEarnedFieldProgress(Campaign data)
        {
            var g=Fresh(data);
            WorldObject book=Object(g,"worldchronicle");
            g.Talk(book);
            int section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");
            Check(section>=0,"The old chronicle still offers its aftermath section");
            g.Choose(section);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_glasswind")<0,
                "The new page stays locked before the real commission");
            g.Close();g.save.flags.Add("glasswind_started");
            g.Talk(book);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            int page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_glasswind");
            Check(page>=0,"Accepting Mia's quest unlocks one readable page");
            g.Choose(page);
            Check(g.conversation.text.Contains("北沙阀仍在等人") &&
                g.conversation.textEn.Contains("northern capture ring has not yet been held") &&
                !g.conversation.text.Contains("米娅已把新风路图公开"),
                "The chronicle does not claim uncaptured sites or spoil rewards");
            g.Close();g.save.flags.Add("glasswind_1");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_glasswind");g.Choose(page);
            Check(g.conversation.text.Contains("北沙阀已经解除") &&
                g.conversation.text.Contains("东脊阀仍将") &&
                !g.conversation.text.Contains("米娅已把新风路图公开"),
                "The archive distinguishes one cleared relay from the remaining locked sites");
            g.Close();g.save.flags.Add("glasswind_2");
            g.save.flags.Add("glasswind_3");
            g.save.flags.Add("glasswind_all_sealed");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_glasswind");g.Choose(page);
            Check(g.conversation.text.Contains("等待米娅正式归档") &&
                !g.conversation.text.Contains("米娅已把新风路图公开"),
                "Three live site completions must not fabricate the town publication");
            g.Close();g.save.flags.Add("glasswind_complete");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_glasswind");g.Choose(page);
            Check(g.conversation.text.Contains("米娅已把新风路图公开") &&
                g.conversation.textEn.Contains("Mia published"),
                "Only real final publication reveals the bilingual epilogue");
        }


        public static void GlasswindPressureWarningsAreRealAndSafelyRetired(Campaign data)
        {
            for(int site=1;site<=3;site++)
            {
                var g=Fresh(data);
                g.save.stage=3;g.save.level=16;
                foreach(string flag in new[]{"harbor_report","desert_report",
                    "gate_6","glasswind_started"}) g.save.flags.Add(flag);
                g.Enter(6);g.enemies.Clear();
                WorldObject valve=Object(g,"glasswindrelay"+site);
                g.save.x=valve.x;g.save.y=valve.y;
                Action(g,valve.kind,"glasswind_start_"+site);
                Check(g.GlasswindSite==site,"Only the selected valve begins an active encounter");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=120;e.timer=120;}
                g.invulnerable=0;g.save.heat=0;
                int oldHp=(int)g.save.health;
                var normalHazard=new Hazard{position=new Point(18,4),
                    radius=.5f,timer=20,damage=1,kind="rift"};
                var normalEffect=new Effect("discover",new Point(18,4),20);
                g.hazards.Add(normalHazard);g.effects.Add(normalEffect);
                Advance(g,2.55f);
                var gusts=g.hazards.FindAll(h=>h.glasswindOwned);
                Check(gusts.Count==2,
                    "Every wind valve produces actual paired, tagged telegraph contacts");
                Hazard primary=gusts.Find(h=>(h.position-g.Position).Length<.1f);
                Check(primary!=null && primary.timer>.9f &&
                    primary.radius<1.2f && primary.damage>=11,
                    "First pressure strike warns the live player location before damage");
                Hazard second=gusts.Find(h=>h!=primary);
                Check(second!=null && second.timer>primary.timer &&
                    (second.position-primary.position).Length>2.8f,
                    "The companion blast is physically separate and delayed");
                if(site==1) Check(second.position.x>primary.position.x+2.8f,
                    "Northern relay alternates along its first eastward crosswind");
                if(site==2) Check(second.position.y>primary.position.y+2.9f,
                    "Eastern ridge sends the delayed pressure down its lee side");
                if(site==3) Check(second.position.x>primary.position.x+2.9f &&
                    second.timer>1.9f,
                    "Southern dunes release a distinctly later lateral contact");
                Point[] exits={new Point(2.1f,0),new Point(-2.1f,0),
                    new Point(0,2.1f),new Point(0,-2.1f)};
                bool safe=false;
                Point exit=g.Position;
                foreach(Point step in exits)
                    if(g.CanStand(g.Position+step) &&
                        g.NavigationPath(g.Position+step).Count>0 &&
                        (g.Position+step-second.position).Length>second.radius+.2f)
                    {exit=g.Position+step;safe=true;break;}
                Check(safe,
                    "An accessible safe direction must remain despite staggered blasts");
                if(site==1)
                {
                    Advance(g,1.38f);
                    Check(g.save.health<oldHp,
                        "Ignoring the real world warning takes actual HP damage");
                    Check(g.effects.Exists(e=>e.glasswindOwned),
                        "The released blast has an owned production impact effect");
                }
                else
                {
                    g.save.x=exit.x;g.save.y=exit.y;
                    Advance(g,1.38f);
                    Check(g.save.health==oldHp,
                        "Leaving the telegraph before release avoids genuine damage");
                }
                g.save.x=valve.x;g.save.y=valve.y;
                Action(g,valve.kind,"glasswind_withdraw");
                Check(g.GlasswindSite==0 &&
                    !g.hazards.Exists(h=>h.glasswindOwned) &&
                    !g.effects.Exists(e=>e.glasswindOwned) &&
                    !g.enemies.Exists(g.IsGlasswindEnemy) &&
                    g.hazards.Contains(normalHazard) &&
                    g.effects.Contains(normalEffect) &&
                    !g.Has("glasswind_"+site),
                    "Withdrawal removes only encounter-owned foes, warnings and impacts");
            }
        }


        public static void GlasswindCannotSealTheOnlyReachableDodgeExit(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"harbor_report","desert_report",
                "gate_6","glasswind_started"})g.save.flags.Add(flag);
            g.Enter(6);g.enemies.Clear();
            WorldObject valve=Object(g,"glasswindrelay2");
            g.save.x=valve.x;g.save.y=valve.y;
            Action(g,valve.kind,"glasswind_start_2");
            Check(g.GlasswindSite==2,"Real eastern-valve defense starts");
            foreach(Enemy foe in g.enemies.FindAll(g.IsGlasswindEnemy))
            {foe.stun=120;foe.timer=120;}
            // Desert northwestern wall corridor: a reachable landing exists
            // below the hero, but the secondary southern pulse would cover it.
            // Preserve the original map, gate and collision: no fake tiles.
            g.save.x=3.5f;g.save.y=3.5f;
            Check(g.CanStand(g.Position),"The narrow testing location is real walkable terrain");
            Advance(g,2.55f);
            var warnings=g.hazards.FindAll(h=>h.glasswindOwned);
            Check(warnings.Count==1 &&
                (warnings[0].position-g.Position).Length<.1f,
                "The delayed second blast is omitted if it would close the sole escape");
            Point[] exits={new Point(2.1f,0),new Point(-2.1f,0),
                new Point(0,2.1f),new Point(0,-2.1f)};
            int accessible=0;
            foreach(Point step in exits)
                if(g.CanStand(g.Position+step) &&
                    g.NavigationPath(g.Position+step).Count>0)
                    accessible++;
            Check(accessible==1,
                "The genuine wall topology, not a fabricated safety flag, has a sole exit");
            // Aborting from the far end is impossible by forged dialogue;
            // an active relay remains tied to its actual original valve.
            int before=g.GlasswindSite;
            g.Talk(valve);
            Check(g.GlasswindSite==before &&
                !g.Has("glasswind_2"),
                "Remote menu inspection cannot silently seal an active relay");
        }

        public static void GlasswindRequiresConfirmedKillsForBothWaves(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"harbor_report","desert_report",
                "gate_6","glasswind_started"})g.save.flags.Add(flag);
            g.Enter(6);g.enemies.Clear();g.invulnerable=250;
            WorldObject valve=Object(g,"glasswindrelay2");
            g.save.x=valve.x;g.save.y=valve.y;
            int coins=g.save.coins,shards=g.save.shards;
            Action(g,valve.kind,"glasswind_start_2");
            Check(g.GlasswindSite==2&&g.GlasswindWave==1&&
                g.enemies.FindAll(g.IsGlasswindEnemy).Count==2,
                "Actual relay starts with two individually identifiable guards");
            g.enemies.Remove(g.enemies.Find(g.IsGlasswindEnemy));
            g.Tick(.05f,new Point());
            Check(g.GlasswindSite==0&&!g.Has("glasswind_2")&&
                !g.enemies.Exists(g.IsGlasswindEnemy)&&
                g.save.coins==coins&&g.save.shards==shards,
                "Removing a guard without combat cannot earn the first wave or any loot");
            Action(g,valve.kind,"glasswind_start_2");
            Check(g.GlasswindSite==2,
                "Invalid guard disappearance does not permanently lock the authored relay");
            foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
            {e.stun=200;e.timer=200;}
            Advance(g,4.25f);
            Check(g.GlasswindWave==2&&
                g.enemies.FindAll(g.IsGlasswindEnemy).Count==4,
                "Authentic live capture summons the second wave");
            foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
            {e.stun=200;e.timer=200;}
            Advance(g,4.2f);
            Check(g.GlasswindCharge>=8,
                "The existing capture meter still fills in real field simulation");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Live damage callback must be available");
            var killed=g.enemies.Find(e=>g.IsGlasswindEnemy(e)&&e.id==89810+2*10+2);
            Check(killed!=null,"Expected first-wave guard has its original authored ID");
            hit.Invoke(g,new object[]{killed,99999f,false,null,null});
            // Delete every other guard without a lethal callback. A full
            // meter plus an empty roster must NEVER count as victory.
            g.enemies.RemoveAll(g.IsGlasswindEnemy);
            g.Talk(valve);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    choice=>choice.action=="glasswind_seal")<0,
                "Physical relay refuses to seal even with full charge and empty unverified roster");
            g.Close();
            g.Tick(.05f,new Point());
            Check(g.GlasswindSite==0&&!g.Has("glasswind_2")&&
                g.save.coins==coins&&g.save.shards==shards,
                "Wave-two missing combat deaths reset without publishing a civilian service");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&
                !loaded.Has("glasswind_2")&&loaded.GlasswindSite==0,
                "Unconfirmed temporary captures never pollute legacy version-two saves");
        }

    }
}
