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
                Enemy[] wave=g.enemies.ToArray();
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
            // One authentic live raider crosses an armed, real paid trap.
            bool set=false;
            foreach(Point direction in new[]{new Point(1,0),new Point(0,1),
                new Point(-1,0),new Point(0,-1)})
                if(g.LayFieldSnare(direction)){set=true;break;}
            Check(set&&g.FieldSnares.Count==1,
                "Actual world allows construction of a resource-paid snare");
            var foot=g.FieldSnares[0].foot;
            first.x=foot.x;first.y=foot.y;first.hitTime=5f;
            second.hitTime=5f;
            for(int f=0;f<11;f++)g.Tick(.05f,new Point());
            Check(System.Object.ReferenceEquals(g.SnaredBindTarget,first)&&
                g.BindSnaredEnemy()&&!g.enemies.Contains(first)&&
                !g.VantageWatchReady,
                "Only exact genuine snare contact and paid restraint earns one living captive");
            hit.Invoke(g,new object[]{second,9999f,true,null,null});
            Check(g.VantageWatchReady&&g.VantageWatchRemaining==0,
                "Mixed authentic live capture and combat clears real defenders");
            int originalCoins=g.save.coins;
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
    }
}
