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
        public static void UnspentFieldSnareCanBePhysicallyReclaimedWithoutInfiniteSupplies(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            g.save.inventory[0]=5;g.save.inventory[4]=5;
            g.save.energy=100f;
            Point foot=PutRealSnare(g);
            Check(g.FieldSnares.Count==1&&g.CanRecoverFieldSnare,
                "Original world trap is recoverable only from its near standable foot");
            int timber=g.save.inventory[0],gear=g.save.inventory[4];
            float energy=g.save.energy;
            Point position=g.Position;
            g.save.x=position.x+12f;
            Check(!g.RecoverFieldSnare()&&g.FieldSnares.Count==1&&
                g.save.inventory[0]==timber&&g.save.inventory[4]==gear,
                "Distant retrieval cannot teleport traps or mint materials");
            g.save.x=position.x;g.save.y=position.y;
            g.save.energy=5f;
            Check(!g.RecoverFieldSnare()&&g.FieldSnares.Count==1&&
                g.save.inventory[0]==timber,
                "Exhausted hero cannot reclaim without stamina");
            g.save.energy=energy;
            var raider=new Enemy(71243,0,g.Position.x+.5f,g.Position.y,80f);
            raider.hitTime=5f;g.enemies.Add(raider);
            Check(!g.RecoverFieldSnare()&&g.FieldSnares.Count==1,
                "Nearby living attacker prevents reclaiming a live defensive trap");
            g.enemies.Remove(raider);
            Check(g.RecoverFieldSnare()&&g.FieldSnares.Count==0&&
                g.save.inventory[0]==timber+1&&g.save.inventory[4]==gear&&
                Math.Abs(g.save.energy-(energy-6f))<.001f,
                "Retrieve exactly one wooden stake, never consumed machine parts");
            Check(!g.RecoverFieldSnare()&&g.save.inventory[0]==timber+1&&
                g.FieldSnareCaptures==0,
                "No repeated retrieval or counterfeit snare capture credit");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FieldSnares.Count==0&&
                restored.save.inventory[0]==timber+1&&
                restored.save.inventory[4]==gear,
                "Reclaimed timber persists in v2 while transient traps reset");
        }
        public static void ActualSnaredEnemyCanBeRestrainedAliveWithoutKillOrLoot(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            g.save.inventory[0]=8;g.save.inventory[4]=8;
            Point foot=PutRealSnare(g),hero=g.Position;
            var foe=new Enemy(79333,0,foot.x,foot.y,85f);
            foe.hitTime=5f;g.enemies.Add(foe);
            Check(!g.CanBindSnaredEnemy&&!g.BindSnaredEnemy(),
                "A living enemy is not instantly removable before a real snare hits");
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(foe.hp==85f&&foe.stun>1f&&g.CanBindSnaredEnemy&&
                object.ReferenceEquals(g.SnaredBindTarget,foe),
                "The original live world trap creates an exact-actor short capture window");
            int coins=g.save.coins,kills=g.save.kills[0],
                oldWood=g.save.inventory[0],oldGear=g.save.inventory[4];
            int beforeLevel=g.save.level,beforeXp=g.save.xp;
            g.save.x=hero.x+9f;
            Check(!g.BindSnaredEnemy()&&g.enemies.Contains(foe)&&
                g.save.energy>=0f,
                "A distant hero cannot remotely eliminate the captive");
            g.save.x=hero.x;g.save.y=hero.y;
            g.save.energy=9f;
            Check(!g.BindSnaredEnemy()&&g.enemies.Contains(foe),
                "Restraint cannot bypass its ten stamina cost");
            g.save.energy=100f;
            Check(g.BindSnaredEnemy()&&!g.enemies.Contains(foe)&&
                g.save.energy==90f&&g.save.coins==coins&&
                g.save.kills[0]==kills&&g.save.inventory[0]==oldWood&&
                g.save.inventory[4]==oldGear&&
                g.Has("field_snare_mercy_first"),
                "Physical nonlethal restraint removes exactly the genuine actor without kill, loot or material gain");
            Check(!g.BindSnaredEnemy()&&g.enemies.Count==0,
                "The same captive cannot be restrained or rewarded twice");
            Check(g.save.level>beforeLevel||g.save.xp>beforeXp,
                "A first true nonlethal success grants a single meaningful mastery record");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.Has("field_snare_mercy_first")&&
                !restored.CanBindSnaredEnemy,
                "V2 preserves the earned restraint memory without serializing transient bind targets");
        }
        public static void RealPacifistCaptureOpensOneExclusiveNollDiscipline(Campaign data)
        {
            for(int branch=0;branch<2;branch++)
            {
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.inventory[0]=12;g.save.inventory[1]=12;
                g.save.inventory[4]=12;
                Point foot=PutRealSnare(g);
                var foe=new Enemy(79500+branch,0,foot.x,foot.y,80f);
                foe.hitTime=5f;g.enemies.Add(foe);
                for(int f=0;f<11;f++)g.Tick(.05f,new Point());
                Check(g.CanBindSnaredEnemy&&g.BindSnaredEnemy()&&
                    g.Has("field_snare_mercy_first"),
                    "Actual live nonlethal restraint, not a fake v2 flag, earns Noll's humane lesson");
                g.save.flags.Add("ally_3");g.enemies.Clear();
                var smith=Object(g,"forge");
                g.save.x=smith.x;g.save.y=smith.y;
                g.Talk(smith);
                string choice=branch==0?
                    "field_snare_warden_swift":"field_snare_warden_calm";
                string other=branch==0?
                    "field_snare_warden_calm":"field_snare_warden_swift";
                int selected=Array.FindIndex(g.conversation.choices,c=>c.action==choice);
                Check(selected>=0&&g.SnareMercyTrainingAvailable,
                    "The real repaired forge offers both earned, mutually exclusive humane methods");
                int wood=g.save.inventory[0],crystals=g.save.inventory[1],
                    parts=g.save.inventory[4];
                g.save.x=smith.x+8f;g.Choose(selected);
                Check(!g.Has(choice)&&g.save.inventory[1]==crystals&&
                    g.save.inventory[4]==parts,
                    "Old remote Noll dialogue cannot buy a new nonlethal combat upgrade");
                g.save.x=smith.x;g.save.y=smith.y;g.Talk(smith);
                selected=Array.FindIndex(g.conversation.choices,c=>c.action==choice);
                Check(selected>=0,"Safe original forge still offers paid training");
                g.Choose(selected);
                Check(g.Has(choice)&&!g.Has(other)&&
                    !g.SnareMercyTrainingAvailable&&
                    g.save.inventory[0]==wood-1&&
                    g.save.inventory[1]==crystals-2&&
                    g.save.inventory[4]==parts-1&&
                    g.SnareBindEnergyCost==(branch==0?6:10)&&
                    Math.Abs(g.SnareHoldBonus-(branch==1?.6f:0f))<.001f,
                    "One physically earned and paid living-captive technique changes actual combat parameters");
                g.Talk(smith);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action==other)<0&&
                    Array.FindIndex(g.conversation.choices,
                    c=>c.action==choice)<0,
                    "Once bought, neither discipline can be repurchased or stacked");
                g.Close();
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.Has(choice)&&
                    !restored.Has(other)&&restored.SnareBindEnergyCost==(branch==0?6:10),
                    "The exclusive player build survives unchanged legacy v2 saves");
                // Test the expensive option on a new genuine deployed snare,
                // not on a static numeric modifier or invented mission unit.
                var trainer=Fresh(data,0);trainer.enemies.Clear();
                trainer.save.flags.Add("field_snare_mercy_first");
                trainer.save.flags.Add(choice);
                trainer.save.inventory[0]=8;trainer.save.inventory[4]=8;
                Point trap=PutRealSnare(trainer);
                var captive=new Enemy(79510+branch,0,trap.x,trap.y,80f);
                captive.hitTime=5f;trainer.enemies.Add(captive);
                for(int frame=0;frame<11;frame++)
                    trainer.Tick(.05f,new Point());
                Check(trainer.CanBindSnaredEnemy&&
                    (branch==1?captive.stun>1.85f:captive.stun>1.1f),
                    "Earned technique still allows real physical restraint of an actor on a live trap");
                if(branch==1)
                {
                    // A real fixed 1.5 second hold becomes 2.1 seconds.
                    Check(captive.stun>1.9f&&captive.stun<2.2f,
                        "Steady wardenship extends genuine ordinary snare hold by 0.6 seconds");
                }
                else
                {
                    trainer.save.energy=6f;
                    Check(trainer.BindSnaredEnemy()&&
                        trainer.save.energy==0f&&!trainer.enemies.Contains(captive),
                        "Swift knots actually permit a six-energy live restraint");
                }
            }
        }
        public static void HumaneForgeJournalRoutesRealUnfinishedWorkAndExclusiveUpgrade(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            QuestGuide guide=g.GetQuest("merciful_warden");
            Check(!guide.locked&&!guide.complete&&guide.steps.Count==3&&
                !guide.steps[0].done&&guide.room==0&&
                guide.next.Contains("Shift+E"),
                "New humane training is a discoverable journal objective before any alleged capture");
            g.save.inventory[0]=5;g.save.inventory[4]=5;
            Point foot=PutRealSnare(g);
            var raider=new Enemy(79411,0,foot.x,foot.y,80f);
            raider.hitTime=5f;g.enemies.Add(raider);
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(g.BindSnaredEnemy(),"Actual living capture earns the first authored journal step");
            guide=g.GetQuest("merciful_warden");
            WorldObject smith=Object(g,"forge");
            Check(guide.steps[0].done&&!guide.steps[1].done&&
                guide.room==0&&
                (guide.target-new Point(smith.x,smith.y)).Length<.01f,
                "After an earned bind the journal points to the original forge, not a fake NPC");
            g.save.flags.Add("ally_3");
            guide=g.GetQuest("merciful_warden");
            Check(guide.steps[1].done&&!guide.complete&&
                guide.next.Contains("forge"),
                "Original repaired town pump genuinely unlocks the humane smith handoff");
            g.save.flags.Add("field_snare_warden_calm");
            guide=g.GetQuest("merciful_warden");
            Check(guide.complete&&guide.steps[2].done&&guide.Progress==1f&&
                guide.next.Contains("0.6"),
                "Only the paid exclusive discipline closes the tracked journal chapter");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.GetQuest("merciful_warden").complete,
                "Living-captive journal survives legacy v2 without any new schema");
        }
        public static void FieldSnareBindExpiresAndIgnoresUnrelatedStuns(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            g.save.inventory[0]=9;g.save.inventory[4]=9;
            Point foot=PutRealSnare(g);
            var unrelated=new Enemy(79334,0,foot.x+3f,foot.y,80f);
            unrelated.hitTime=5f;unrelated.stun=2f;g.enemies.Add(unrelated);
            Check(!g.CanBindSnaredEnemy&&!g.BindSnaredEnemy(),
                "An ordinary stun does not impersonate a real snare capture");
            var foe=new Enemy(79335,0,foot.x,foot.y,80f);
            foe.hitTime=5f;g.enemies.Add(foe);
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(g.CanBindSnaredEnemy&&g.enemies.Contains(foe),
                "Snare capture opens an initially usable physical window");
            g.save.x=foot.x+10f;g.save.y=foot.y;
            for(int i=0;i<70;i++)g.Tick(.05f,new Point());
            g.save.x=foot.x-1.35f;g.save.y=foot.y;
            Check(!g.CanBindSnaredEnemy&&!g.BindSnaredEnemy()&&
                g.enemies.Contains(foe),
                "A stale captured enemy cannot be restrained after the true hold expires");
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
