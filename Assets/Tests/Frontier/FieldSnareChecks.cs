using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static Point PutRealSnare(GameCore g)
        {
            int maxY=g.Map.rows.Length,maxX=g.Map.rows[0].Length;
            for(int y=4;y<maxY-4;y++)
            for(int x=4;x<maxX-4;x++)
            {
                g.save.x=x+.5f;g.save.y=y+.5f;
                if(!g.CanStand(g.Position))continue;
                if(g.LayFieldSnare(new Point(1,0)))
                    return g.FieldSnares[g.FieldSnares.Count-1].foot;
            }
            throw new Exception("No walkable physical snare site in production map");
        }

        public static void FieldSnareRequiresRealTerrainConsumesMaterialsAndTripsLivingActor(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            g.save.inventory[0]=5;g.save.inventory[4]=5;
            int wood=g.save.inventory[0],parts=g.save.inventory[4];
            Point foot=PutRealSnare(g);
            Check(g.FieldSnares.Count==1&&
                g.save.inventory[0]==wood-1&&g.save.inventory[4]==parts-1&&
                g.CanStand(foot),"An actual grounded trap consumes one timber and one gear");
            wood=g.save.inventory[0];parts=g.save.inventory[4];
            Check(!g.LayFieldSnare(new Point(1,0))&&
                g.save.inventory[0]==wood&&g.save.inventory[4]==parts,
                "The same ground cannot mint overlapping traps or double-spend supplies");
            var trap=g.FieldSnares[0];
            Check(trap.arming>0f&&trap.seconds>20f,
                "The new snare has a real arming delay and time-limited existence");
            var foe=new Enemy(89315,0,foot.x,foot.y,90);
            foe.hitTime=3f;g.enemies.Add(foe);
            int kills=g.save.kills[0],coins=g.save.coins;
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(g.FieldSnares.Count==0&&foe.stun>1f&&
                foe.armorOpenTime>.4f&&foe.hp==90,
                "A physically contacted ordinary enemy consumes exactly one snare and becomes briefly vulnerable");
            Check(g.save.kills[0]==kills&&g.save.coins==coins,
                "Nonlethal snare contact cannot counterfeit kill or loot rewards");
        }
        public static void FieldSnaresExpireResetAcrossRoomsAndOldSaveSlots(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.inventory[0]=8;g.save.inventory[4]=8;
            Point first=PutRealSnare(g);
            Check(g.FieldSnares.Count==1,"World contains the first paid trap");
            g.save.x=first.x+6;g.save.y=first.y+6;
            if(!g.CanStand(g.Position))
            {
                g.save.x=first.x-6;g.save.y=first.y;
            }
            // A second valid site exists in the same full production town.
            PutRealSnare(g);
            Check(g.FieldSnares.Count==2&&!g.CanLayFieldSnare,
                "Two physical placements enforce a bounded active-trap budget");
            g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&reloaded.FieldSnares.Count==0,
                "Old SaveData v2 never serializes live traps into another session");
            g.save.room=1;
            Check(g.FieldSnares.Count==0,
                "Trap rendering rejects a different active room immediately");
            g.Tick(.05f,new Point());
            Check(g.FieldSnares.Count==0,
                "Room transition retires unsaved trap bodies without contaminating the new map");
            var h=Fresh(data);h.enemies.Clear();
            h.save.inventory[0]=5;h.save.inventory[4]=5;
            PutRealSnare(h);
            // Tick caps each simulation slice; advance the genuine frame
            // clock rather than pretending a 500ms parameter all elapsed.
            for(int i=0;i<540;i++)h.Tick(.05f,new Point());
            Check(h.FieldSnares.Count==0,
                "Untriggered snare expires in genuine world seconds");
        }
        public static void FieldSnareDoesNotDisableBossesOrCreateRemoteContacts(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.inventory[0]=6;g.save.inventory[4]=6;
            Point foot=PutRealSnare(g);
            var boss=new Enemy(89991,3,foot.x,foot.y,200,true);
            boss.hitTime=5f;g.enemies.Add(boss);
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(g.FieldSnares.Count==1&&boss.stun<.01f,
                "Original Boss attacks and phase mechanics ignore ordinary snares");
            g.enemies.Remove(boss);
            var distant=new Enemy(89992,0,foot.x+4f,foot.y,90);
            distant.hitTime=5f;g.enemies.Add(distant);
            for(int i=0;i<10;i++)g.Tick(.05f,new Point());
            Check(g.FieldSnares.Count==1&&distant.stun<.01f,
                "The trap never remote-stuns an unrelated enemy beyond ground reach");
            g.enemies.Remove(distant);
            var real=new Enemy(89993,0,foot.x,foot.y,90);
            real.hitTime=5f;g.enemies.Add(real);
            g.Tick(.05f,new Point());
            Check(g.FieldSnares.Count==0&&real.stun>1f,
                "A live non-boss can still trigger an armed trap after a Boss passes it");
        }
        public static void FieldSnareCaptureMasteryRequiresThreeTrueContactsAndNoll(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("ally_3");
            g.save.inventory[0]=12;g.save.inventory[1]=12;
            g.save.inventory[4]=12;
            var smith=Object(g,"forge");
            Check(g.GetQuest("field_snare").steps.Count==4&&
                !g.GetQuest("field_snare").complete,
                "The new physical snare task starts before contact milestones");
            for(int n=1;n<=3;n++)
            {
                Point foot=PutRealSnare(g);
                var target=new Enemy(99000+n,0,foot.x,foot.y,80);
                target.hitTime=4f;g.enemies.Add(target);
                int remaining=g.FieldSnares.Count;
                for(int f=0;f<11;f++)g.Tick(.05f,new Point());
                Check(g.FieldSnares.Count==remaining-1&&
                    g.FieldSnareCaptures==n&&
                    g.Has("field_snare_capture_"+n)&&target.hp==80,
                    "A distinct grounded nonlethal contact earns exactly one real mastery mark");
                g.enemies.Remove(target);
            }
            var quest=g.GetQuest("field_snare");
            Check(quest.steps[2].done&&!quest.complete&&
                quest.room==0&&
                (quest.target-new Point(smith.x,smith.y)).Length<.01f,
                "After three real catches the original quest guides back to the town forge");
            g.save.x=smith.x;g.save.y=smith.y;
            g.Talk(smith);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_snare_masterwork");
            Check(option>=0,"A physically reachable Noll offers the earned reinforced line");
            int crystal=g.save.inventory[1],gear=g.save.inventory[4];
            g.save.x=smith.x+8f;g.Choose(option);
            Check(!g.Has("field_snare_mastered")&&
                g.save.inventory[1]==crystal&&g.save.inventory[4]==gear,
                "A walked-away forge conversation cannot buy remote mastery");
            g.save.x=smith.x;g.save.y=smith.y;
            g.Talk(smith);
            option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_snare_masterwork");
            g.Choose(option);
            Check(g.Has("field_snare_mastered")&&g.FieldSnareLimit==3&&
                g.save.inventory[1]==crystal-2&&g.save.inventory[4]==gear-2&&
                g.GetQuest("field_snare").complete,
                "One earned, source-bound payment unlocks third trap and closes the genuine journal");
            g.Talk(smith);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_snare_masterwork")<0,
                "Noll cannot farm a second mastery fee or reward");
            g.Close();
            var newFoot=PutRealSnare(g);
            Check(g.FieldSnares.Count==1&&g.FieldSnares[0].arming<=.25f+.0001f,
                "Newly constructed post-mastery equipment arms faster in the real world");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FieldSnareLimit==3&&
                restored.GetQuest("field_snare").complete&&
                restored.FieldSnares.Count==0,
                "Permanent mastery and its tracked quest survive unchanged v2 while live traps reset");
        }
    }
}
