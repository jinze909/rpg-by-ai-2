using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Every scenario begins at the original authored farlookout, uses
        // actual world combat and signs its durable project in the same room.
        public static void EightRealFarLookoutsOfferDistinctPhysicalDefenseAndPermanentServices(Campaign data)
        {
            // The production wave chooser rotates eight compass approaches;
            // raw row-major order formerly crowded the same edge of a lookout.
            var octant=typeof(GameCore).GetMethod("WatchApproachOctant",
                BindingFlags.NonPublic|BindingFlags.Static);
            Check(octant!=null,"Lookout waves have a compass-aware spawn chooser");
            int[,] directions={{1,0},{1,1},{0,1},{-1,1},
                {-1,0},{-1,-1},{0,-1},{1,-1}};
            for(int i=0;i<8;i++)
                Check((int)octant.Invoke(null,new object[]{
                    directions[i,0],directions[i,1]})==i,
                    "Compass-sector assignment preserves all eight genuine approach directions: "+i);
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(hit!=null,"Production melee/projectile damage transaction is present");
            string[] stories=new string[8];
            for(int room=0;room<8;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.enemies.Clear();
                WorldObject lookout=Array.Find(g.Map.objects,
                    x=>x.kind=="farlookout"&&x.id=="far_vista_"+room);
                Check(lookout!=null&&PlaceWithinTalkReach(g,lookout),
                    "Each actual overworld region has a reachable far lookout: "+room);
                Point start=g.Position;
                g.save.inventory[0]=5;g.save.inventory[4]=5;
                g.save.energy=100;
                g.Talk(lookout);
                Check(g.Has("far_vista_"+room)&&g.conversation!=null,
                    "Ordinary live lookout still charts the original nearby region");
                stories[room]=g.conversation.textEn;
                int begin=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="vantage_begin");
                Check(begin>=0,"Original lookout exposes new physical defense option: "+room);
                int oldCoins=g.save.coins,oldShards=g.save.shards,
                    oldWood=g.save.inventory[0],oldParts=g.save.inventory[4];
                g.Choose(begin);
                int count=room==2||room==3||room==6?3:2;
                Check(g.VantageWatchActive&&!g.VantageWatchReady&&
                    g.VantageWatchRemaining==count&&g.enemies.Count==count,
                    "Authentic live full defense roster spawned at original map source "+room);
                g.Talk(lookout);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="vantage_alarm")>=0,
                    "All eight authentic lookouts provide a player-triggered defensive bell");
                g.Close();
                Enemy[] wave=g.enemies.ToArray();
                // Move only the test hero's transient foot to the farthest
                // roster entry: the compass must favor the actual nearest
                // living fighter, not a stale spawn-array index.
                Enemy last=wave[wave.Length-1];
                g.save.x=last.x;g.save.y=last.y;
                Check((g.VantageWatchGuideTarget-last.Position).Length<.01f&&
                    (g.NavigationTarget().Value-last.Position).Length<.01f,
                    "During a spread watch, the world compass follows the closest real live raider");
                g.save.x=start.x;g.save.y=start.y;
                for(int a=0;a<wave.Length;a++)
                    for(int b=a+1;b<wave.Length;b++)
                        Check((wave[a].Position-wave[b].Position).Length>=5.95f,
                            "A directional lookout wave must not stack hostile bodies on one spawn pad");
                foreach(Enemy foe in wave)
                {
                    Check(g.CanStand(foe.Position)&&
                        (foe.Position-start).Length>=5f&&
                        g.NavigationPathFrom(start,foe.Position,true).Count>0,
                        "Every defender reaches original lookout on real collision graph: "+room);
                    foreach(WorldObject other in g.Map.objects)
                        Check((foe.Position-new Point(other.x,other.y)).Length>=3.1f,
                            "A raider must not block a real quest prop or NPC");
                }
                g.Enter(0);
                Check(g.save.room==room&&g.VantageWatchActive,
                    "A protected live civilian watch cannot be bypassed by travel");
                foreach(Enemy foe in wave)
                    hit.Invoke(g,new object[]{foe,9999f,true,null,null});
                Check(g.VantageWatchReady&&g.VantageWatchRemaining==0&&
                    g.save.coins==oldCoins&&g.save.shards==oldShards,
                    "Only real combat kills finish a wave; event hostiles drop no farmable regular loot");
                g.save.x=start.x;g.save.y=start.y;
                Check(PlaceWithinTalkReach(g,lookout),
                    "After combat the hero must physically return to the original lookout");
                g.Talk(lookout);
                bool care=room%2==0;
                string action=care?"vantage_aid":"vantage_signal";
                int choice=Array.FindIndex(g.conversation.choices,
                    c=>c.action==action);
                Check(choice>=0,"Real report offers both paid civilian project choices");
                g.Choose(choice);
                Check(!g.VantageWatchActive&&g.VantageWatchBuilt(room)&&
                    g.VantageWatchAid(room)==care&&
                    g.VantageWatchSignal(room)!=care&&
                    g.save.inventory[0]==oldWood-1&&
                    g.save.inventory[4]==oldParts-1&&
                    g.save.coins==oldCoins+20+room*3&&
                    g.save.shards==oldShards+1,
                    "Actual first clearance spends wood and machine parts and pays once");
                g.Close();g.Talk(lookout);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="vantage_begin")<0,
                    "Signed lookout is not a respawnable money exploit");
                // Actual world-mode ticks, not paused dialogue repaint,
                // must own the permanent local service.
                g.Close();
                // Real combat alert/recovery temporarily suspends public
                // services. Measure the health and weather effect only
                // after the simulation confirms a safe ceasefire.
                for(int f=0;f<240&&!g.SafeTravel;f++)
                    g.Tick(.05f,new Point());
                Check(g.SafeTravel,"Public services cannot be tested during active combat");
                if(care)
                {
                    g.save.health=30;g.save.heat=20;g.save.cold=15;
                    float hp=g.save.health,hot=g.save.heat,cold=g.save.cold;
                    for(int f=0;f<12;f++)g.Tick(.05f,new Point());
                    Check(g.save.health>hp&&g.save.heat<hot&&g.save.cold<cold,
                        "Authentic built medical lookout treats a safe nearby hero's HP and climate");
                }
                else
                {
                    g.save.energy=40f;
                    float energy=g.save.energy;
                    for(int f=0;f<12;f++)g.Tick(.05f,new Point());
                    Check(g.save.energy>energy,
                        "Authentic built signal lookout restores safe local stamina");
                }
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.VantageWatchBuilt(room)&&
                    !restored.VantageWatchActive,
                    "Only earned local build choice survives existing v2 saves");
            }
            for(int room=0;room<8;room++)
                for(int other=room+1;other<8;other++)
                    Check(stories[room]!=stories[other],
                        "Eight observation stories are region-specific, not duplicated boilerplate");
        }

        public static void VantageWatchRejectsGhostIdentityAndCanUseRealNonlethalSnare(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.room=1;g.enemies.Clear();
            var lookout=Array.Find(g.Map.objects,x=>x.kind=="farlookout");
            Check(lookout!=null&&PlaceWithinTalkReach(g,lookout),
                "Original Thornwood far lookout is physically reachable");
            g.save.inventory[0]=9;g.save.inventory[4]=9;
            g.save.energy=100;
            g.Talk(lookout);
            g.Choose(Array.FindIndex(g.conversation.choices,x=>x.action=="vantage_begin"));
            Check(g.VantageWatchActive&&g.VantageWatchRemaining==2,
                "Original Thornwood live raiders spawned");
            Enemy first=g.enemies[0],second=g.enemies[1];
            // An ambient fighter with the SAME numerical ID cannot steal
            // the expedition's exact actor identity or its first-clear fee.
            var impostor=new Enemy(first.id,first.kind,g.Position.x+9,
                g.Position.y,35);
            g.enemies.Add(impostor);
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            hit.Invoke(g,new object[]{impostor,9999f,true,null,null});
            Check(!g.VantageWatchReady&&g.VantageWatchRemaining==2,
                "Killing an impostor with the same ID cannot sign a lookout wave");
            // Real sword hit-stop is not an instant opportunity to
            // build traps. Wait for released combat contact and deliberately
            // find a standable original terrain pad for the stake.
            first.hitTime=second.hitTime=12f;
            g.invulnerable=12f;
            for(int f=0;f<25;f++)g.Tick(.05f,new Point());
            Point foot=PutRealSnare(g);
            Check(g.FieldSnares.Count==1,
                "Actual terrain and live inventory allow a paid field snare");
            first.x=foot.x;first.y=foot.y;first.hitTime=8f;
            second.hitTime=8f;
            for(int f=0;f<11;f++)g.Tick(.05f,new Point());
            Check(System.Object.ReferenceEquals(g.SnaredBindTarget,first)&&
                g.BindSnaredEnemy()&&!g.enemies.Contains(first)&&
                !g.VantageWatchReady,
                "Only exact genuine snare contact and paid restraint earns one living captive");
            hit.Invoke(g,new object[]{second,9999f,true,null,null});
            Check(g.VantageWatchReady&&g.VantageWatchRemaining==0,
                "Mixed authentic live capture and combat clears real defenders");
            int originalCoins=g.save.coins;
            for(int f=0;f<240&&!g.SafeTravel;f++)
                g.Tick(.05f,new Point());
            Check(g.SafeTravel&&PlaceWithinTalkReach(g,lookout),
                "After real combat hero must return safely to original lookout");
            g.Talk(lookout);
            int aid=Array.FindIndex(g.conversation.choices,
                x=>x.action=="vantage_aid");
            Check(aid>=0,"Both real actor resolutions unlock the report at its original station");
            g.Choose(aid);
            Check(g.VantageWatchAid(1)&&g.save.coins==originalCoins+23,
                "Mixed living-captive watch pays one first-clear receipt, not duplicate ID bounty");
            g.PrepareSave();
            Check(g.save.worldEnemies!=null&&
                Array.Find(g.save.worldEnemies,x=>x!=null&&
                    (x.id==first.id||x.id==second.id))==null,
                "Live mission raiders never leak into normal serialized world patrols");
        }

        public static void LookoutWarningBellIsPaidPhysicalAndOneUse(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.room=1;g.enemies.Clear();
            WorldObject lookout=Array.Find(g.Map.objects,
                o=>o.kind=="farlookout"&&o.id=="far_vista_1");
            Check(lookout!=null&&PlaceWithinTalkReach(g,lookout),
                "Real Thornwood bell has a physical and reachable lookout");
            g.save.energy=100f;
            g.Talk(lookout);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="vantage_begin"));
            Check(g.VantageWatchActive&&g.enemies.Count==2,
                "A warning bell belongs to one genuine live enemy encounter");
            Enemy target=g.enemies[0];
            // Choose a real unblocked standable foot close to the source.
            // Never create a phantom unit or let the horn act through walls.
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(clear!=null,"Real attack cover geometry is exposed to the watch");
            bool found=false;
            for(int y=-6;y<=6&&!found;y++)
            for(int x=-6;x<=6&&!found;x++)
            {
                Point foot=new Point((float)Math.Floor(g.Position.x)+x+.5f,
                    (float)Math.Floor(g.Position.y)+y+.5f);
                float distance=(foot-g.Position).Length;
                if(distance<2.5f||distance>6.5f||!g.CanStand(foot)||
                   !(bool)clear.Invoke(g,new object[]{g.Position,foot}))
                    continue;
                target.x=foot.x;target.y=foot.y;found=true;
            }
            Check(found,"An actual reachable line-of-sight foot is available for bell interruption");
            Enemy impostor=new Enemy(target.id,target.kind,target.x,target.y,25f);
            g.enemies.Add(impostor);
            target.state=1;target.timer=.3f;target.stun=0;
            impostor.state=1;impostor.timer=.3f;impostor.stun=0;
            // A rejected action cannot spend resources, mark the bell used,
            // or cancel either actor's telegraph.
            g.save.energy=5f;g.Talk(lookout);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="vantage_alarm");
            Check(option>=0,"The action is shown before its first real use");
            g.Choose(option);
            Check(!g.VantageWatchAlarmSpent&&g.save.energy==5f&&
                target.state==1&&impostor.state==1,
                "Insufficient energy preserves both threat and spend state");
            g.save.energy=100f;g.Talk(lookout);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="vantage_alarm"));
            Check(g.VantageWatchAlarmSpent&&g.save.energy==82f&&
                target.state==0&&target.stun>=1.15f&&target.hp>0f&&
                impostor.state==1&&impostor.stun==0f&&
                impostor.hp==25f&&g.VantageWatchRemaining==2,
                "One paid in-range bell interrupts only the real roster, never same-ID impostors or HP");
            g.Talk(lookout);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="vantage_alarm")<0,
                "Bell disappears on the same active watch after its genuine pulse");
            g.Close();g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(!restored.VantageWatchActive&&!restored.VantageWatchAlarmSpent&&
                !restored.VantageWatchBuilt(1)&&restored.save.version==2,
                "Interrupted watch cannot leak its horn state or phantom rewards into v2 slots");
        }

        public static void FatalLookoutWatchNeverSurvivesSlotReloadOrPaysPhantomReward(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.room=1;g.enemies.Clear();
            WorldObject lookout=Array.Find(g.Map.objects,
                o=>o.kind=="farlookout");
            Check(lookout!=null&&PlaceWithinTalkReach(g,lookout),
                "Real map contains the original Thornwood lookout");
            g.Talk(lookout);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="vantage_begin");
            Check(begin>=0,"Existing lookout offers a real watch");
            g.Choose(begin);
            Check(g.VantageWatchActive&&g.enemies.Count==2,
                "Live watch creates exactly two original actual enemy actors");
            var guide=g.GetQuest("vantage_watches");
            Enemy closest=g.enemies[0];
            foreach(Enemy candidate in g.enemies)
                if((candidate.Position-g.Position).Length<
                   (closest.Position-g.Position).Length)
                    closest=candidate;
            Check(guide.steps.Count==8&&guide.room==g.save.room&&
                (guide.target-closest.Position).Length<.01f&&
                (g.NavigationTarget().Value-guide.target).Length<.01f,
                "Real journal and combat compass guide to an original living fighter");
            var hostile=g.enemies.ToArray();
            g.PrepareSave();
            Check(g.save.worldEnemies!=null&&
                Array.Find(g.save.worldEnemies,
                    e=>e!=null&&(e.id==hostile[0].id||e.id==hostile[1].id))==null,
                "An in-flight optional watch cannot be serialized as ordinary enemies");
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                !restored.VantageWatchActive&&!restored.VantageWatchBuilt(1),
                "Loading an old v2 save cannot resume a phantom transient watch");
            // BeginDeath is the shipped fatal transition: merely writing
            // zero HP does not create the genuine death animation clock.
            var beginDeath=typeof(GameCore).GetMethod("BeginDeath",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(beginDeath!=null,"Actual lethal transition exists");
            g.save.health=0f;
            beginDeath.Invoke(g,null);
            Check(g.Dying&&g.VantageWatchActive,
                "Real fatal animation begins with a living watched battlefield");
            g.Tick(.05f,new Point());
            Check(!g.VantageWatchActive&&!g.VantageWatchBuilt(1)&&
                g.VantageWatchWins==0,
                "Lethal world interruption destroys all original watch ownership without earning a reward");
        }
    }
}
