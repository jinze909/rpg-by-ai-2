using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject RootObject(GameCore g,int stage)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id=="rootsong_"+stage)return o;
            throw new Exception("Missing Thornwood root "+stage);
        }
        private static void PlayRootTone(GameCore g,int tone)
        {
            Check(g.mode=="dialog"&&g.conversation!=null,"A root note requires physical dialogue");
            string action="rootsong_"+(g.RootsongProgress+1)+"_"+tone;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==action){g.Choose(i);return;}
            throw new Exception("Root tone not in live dialogue: "+action);
        }

        public static void RestoredRootGuardInterruptsOnlyNearbyThreats(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("scout_saved");
            for(int i=1;i<=3;i++)g.save.flags.Add("rootsong_"+i);
            g.save.flags.Add("rootsong_complete");
            WorldObject root=RootObject(g,1);
            g.save.x=root.x;g.save.y=root.y;g.save.energy=100;
            var close=new Enemy(90751,2,root.x+2,root.y,250) {state=1,timer=.04f};
            g.enemies.Add(close);
            int coins=g.save.coins,shards=g.save.shards,kills=g.save.kills[1];
            g.Talk(root);
            string choice="rootsongpulse_1";int index=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==choice)index=i;
            Check(index>=0,"Restored root offers an actual combat interaction near an enemy");
            g.Choose(index);
            Check(g.save.energy==70&&g.RootsongPulseCooldown>8&&
                close.state==0&&close.stun>=1.19f&&g.effects.Exists(e=>e.text=="ROOTGUARD"||e.text=="古根护歌"),
                "Real root pulse spends energy, interrupts the windup and displays physical feedback");
            Check(close.hp==250&&g.save.coins==coins&&g.save.shards==shards&&
                g.save.kills[1]==kills,
                "Crowd control must not mint phantom kills, XP, drops or rewards");
            float remaining=g.save.energy;
            g.Talk(root);
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==choice)g.Choose(i);
            Check(g.save.energy==remaining,"Cooldown blocks repeated field-pulse charges");
            g.Close();
            g.enemies.Clear();g.save.energy=100;
            g.Talk(root);g.Close();
            Check(g.save.energy==100,"Empty roots do not drain energy or attack absent enemies");
            var boss=new Enemy(90752,3,root.x+2,root.y,500,true){state=1,timer=.02f};
            g.enemies.Add(boss);g.RootsongPulseCooldown=0;
            g.Talk(root);
            index=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==choice)index=i;
            Check(index>=0,"Original boss contact is a valid tactical choice");
            g.Choose(index);
            Check(boss.hp==500&&boss.stun>=.29f&&boss.stun<.5f,
                "Bosses receive only the brief authored interrupt, never a normal full stun");
            g.PrepareSave();g.Load(CloneSave(g.save));
            Check(g.RootsongComplete&&g.save.version==2,
                "Combat utility needs no new persistent data or companion character");
        }
        public static void ThornwoodRootsongUsesPhysicalTimingAndPersistentHarmony(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            Check(g.GetQuest("rootsong").locked,"The original scout rescue gates the new quest");
            for(int step=1;step<=3;step++)
            {
                WorldObject stone=RootObject(g,step);
                Check(g.CanStand(new Point(stone.x,stone.y)),
                    "Each added map prop is collision-safe for the canonical hero");
                g.save.x=data.maps[1].spawnX;g.save.y=data.maps[1].spawnY;
                Check(g.NavigationPath(new Point(stone.x,stone.y)).Count>0,
                    "New forest puzzle stations are physically reachable from the original entrance");
            }
            WorldObject first=RootObject(g,1);
            g.save.x=first.x;g.save.y=first.y;g.Talk(first);
            Check(g.conversation.choices.Length==1&&g.RootsongProgress==0,
                "Without the rescue, a forest root cannot be tuned");g.Close();
            g.save.flags.Add("scout_saved");
            Check(!g.GetQuest("rootsong").locked,"Rescuing the scout unlocks the documented sidequest");
            g.save.totalTime=0;
            g.Talk(first);
            Check(g.conversation.choices.Length==1&&g.RootsongProgress==0,
                "No off-phase menu may expose forged tuning choices");g.Close();
            for(int i=0;i<200&&!g.RootsongOpen(1);i++)g.Tick(.05f,new Point());
            Check(g.RootsongOpen(1),"Actual game simulation opens the green root window");
            g.save.energy=80;
            g.Talk(first);PlayRootTone(g,2);
            Check(g.RootsongProgress==0&&g.save.energy==70&&
                g.hazards.Exists(h=>h.kind=="root"&&h.radius>1.3f&&h.damage==12),
                "Wrong note costs exactly 10 energy and schedules genuine area combat contact");
            g.hazards.Clear();
            g.Talk(first);PlayRootTone(g,0);
            Check(g.RootsongProgress==1&&g.Has("rootsong_1"),
                "Correct low tone starts the physically ordered route");
            int coins=g.save.coins;
            g.Talk(first);
            Check(g.conversation.choices.Length==1,"Already tuned trees only offer rereading");g.Close();
            Check(g.save.coins==coins,"Repeated root readings never award extra coins");
            g.PrepareSave();g.Load(CloneSave(g.save));g.enemies.Clear();
            Check(g.RootsongProgress==1&&g.save.version==2,
                "Partial puzzle stages survive the unmodified legacy version-two save format");
            WorldObject third=RootObject(g,3);
            g.save.x=third.x;g.save.y=third.y;g.Talk(third);
            Check(g.conversation.choices.Length==1&&g.RootsongProgress==1,
                "Skipping the second trunk cannot complete the puzzle");g.Close();
            for(int step=2;step<=3;step++)
            {
                WorldObject o=RootObject(g,step);
                g.save.x=o.x;g.save.y=o.y;
                for(int frame=0;frame<200&&!g.RootsongOpen(step);frame++)g.Tick(.05f,new Point());
                Check(g.RootsongOpen(step),"Later roots have real reachable phase windows");
                g.Talk(o);PlayRootTone(g,step-1);
                Check(g.RootsongProgress==step,"A correct physical note advances exactly one step");
            }
            Check(g.RootsongComplete&&g.save.coins==coins+45&&g.save.shards>=2,
                "Last note awards the authentic one-time end-of-quest transaction");
            QuestGuide quest=g.GetQuest("rootsong");
            Check(quest.complete&&quest.steps.Count==3&&quest.steps.TrueForAll(s=>s.done),
                "New journal entries track all three real completed root voices");
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.energy=0;g.Tick(.05f,new Point());
            Check(g.RootsongRefuge&&g.save.energy>.75f,
                "Safe physical root refuge adds real local energy recovery");
            g.save.x=data.maps[1].spawnX;g.save.y=data.maps[1].spawnY;
            Check(!g.RootsongRefuge,"Restored song provides no remote stamina cheat");
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&reloaded.RootsongComplete&&
                reloaded.GetQuest("rootsong").complete,
                "Completion, rewards and forest service survive old-compatible save reload");
        }
    }
}
