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
        public static void HighlandCivicEmberHasActualColdCombatAndPublicChoices(Campaign data)
        {
            foreach(int branch in new[]{1,2})
            {
                var g=Fresh(data);
                g.save.stage=7;g.Enter(7);g.enemies.Clear();
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
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                g.save.health=60;g.save.energy=0;g.save.cold=40;
                g.Tick(.05f,new Point());
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
    }
}
