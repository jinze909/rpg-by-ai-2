using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore BridgeReady(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;
            g.save.flags.Add("last_directive_complete");
            g.Enter(4);g.enemies.Clear();
            g.invulnerable=800f;g.save.energy=100f;
            Check(g.CanEnter(4)&&g.BridgeOfChordsUnlocked&&
                !g.BridgeOfChordsDefeated&&!g.BridgeOfChordsComplete,
                "Actual post-directive Citadel route unlocks its own late optional elite");
            return g;
        }
        private static WorldObject BridgeSite(GameCore g,string id)
        {
            foreach(WorldObject obj in g.Map.objects)
                if(obj.id==id)return obj;
            throw new Exception("Missing source-authored old-bridge site: "+id);
        }
        private static bool BridgeChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(index<0)return false;
            g.Choose(index);return true;
        }
        private static Enemy BeginBridgeAtOriginalCore(GameCore g)
        {
            WorldObject heart=BridgeSite(g,"bridge_heart");
            Check(PlaceWithinTalkReach(g,heart),
                "Actual Echo Core is reachable on original Citadel bridge terrain");
            g.Talk(heart);
            Check(BridgeChoice(g,"bridge_chords_start")&&
                g.BridgeOfChordsActive&&g.BridgeOfChordsSeconds==125f,
                "Physical original Echo Core launches a 125-second live elite");
            Enemy elite=g.enemies.Find(g.IsBridgeChordsElite);
            Check(elite!=null&&elite.hp>0f&&elite.boss&&
                g.CanStand(elite.Position)&&
                g.LiveBossName(elite).Contains("余震精英")&&
                g.save.trackedQuest=="bridge_chords"&&
                g.GetQuest("bridge_chords").steps.Count==3,
                "Genuine Boss-class actor appears on walkable connected terrain and tracked journal");
            return elite;
        }
        private static void GroundBridgeFork(GameCore g,string id,
            string action,int expected)
        {
            WorldObject station=BridgeSite(g,id);
            Check(PlaceWithinTalkReach(g,station),
                "The physical bridge fork must have walkable source-verified contact");
            g.Talk(station);
            float originalEnergy=g.save.energy;
            Check(BridgeChoice(g,action)&&
                g.BridgeOfChordsActive&&
                g.BridgeOfChordsResonators==expected&&
                Math.Abs(g.save.energy-(originalEnergy-12f))<.001f,
                "Actual on-site fork spends exactly 12 energy and changes the live elite phase");
        }
        private static void DefeatTrueBridgeElite(GameCore g,Enemy elite)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null&&g.enemies.Contains(elite),
                "Boss victory must originate in original live Hit combat entrypoint");
            hit.Invoke(g,new object[]{elite,99999f,false,null,null});
            g.Tick(.05f,new Point());
            Check(g.BridgeOfChordsDefeated&&
                !g.BridgeOfChordsActive&&!g.Has("boss_4"),
                "True bridge elite kill banks its own optional victory WITHOUT forging the main Citadel Boss/core flag");
        }
        public static void BrokenChordBridgeRequiresPriorOriginalStoryAndGenuineBoss(Campaign data)
        {
            var older=Fresh(data);
            Check(older.GetQuest("bridge_chords").locked&&
                !older.BridgeOfChordsUnlocked&&!older.BridgeOfChordsDefeated,
                "Unmodified SaveData v2 neither auto-unlocks this late chapter nor spoils old final Boss");
            older.save.stage=3;older.Enter(4);
            WorldObject heart=BridgeSite(older,"bridge_heart");
            Check(PlaceWithinTalkReach(older,heart),
                "Legacy Citadel still allows inspecting the new physical prop");
            older.Talk(heart);
            Check(!BridgeChoice(older,"bridge_chords_start"),
                "No legitimate Boss trial begins before the Last Directive concludes");
            var g=BridgeReady(data);
            Enemy elite=BeginBridgeAtOriginalCore(g);
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                !restored.BridgeOfChordsActive&&!restored.BridgeOfChordsDefeated,
                "Save/reload never invents transient Boss/hazards in original version-two schema");
            // The authored old bridge is narrow: second-phase pulses
            // are forbidden if the extra blast would occupy its final
            // legal original-world escape lane.
            var paired=typeof(GameCore).GetMethod("BridgeChordsPairedEscape",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(paired!=null,"Two-stage Boss telegraph owns a paired-exit safety check");
            Point bridgeFoot=new Point(48.5f,38.5f);
            bool currentEscape=(bool)paired.Invoke(g,new object[]{
                bridgeFoot,bridgeFoot+new Point(2f,0f),.82f});
            var clearLine=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(clearLine!=null&&g.CanStand(bridgeFoot),
                "Physical original terrain and protected combat sight helper are available");
            // The actual combat obstacle trace is deliberately private:
            // invoke that SAME runtime method instead of inventing a
            // different path clearance rule in this regression.
            bool left=g.CanStand(bridgeFoot+new Point(-2f,0f))&&
                (bool)clearLine.Invoke(g,new object[]{
                    bridgeFoot,bridgeFoot+new Point(-2f,0f)});
            bool up=g.CanStand(bridgeFoot+new Point(0f,2f))&&
                (bool)clearLine.Invoke(g,new object[]{
                    bridgeFoot,bridgeFoot+new Point(0f,2f)});
            bool down=g.CanStand(bridgeFoot+new Point(0f,-2f))&&
                (bool)clearLine.Invoke(g,new object[]{
                    bridgeFoot,bridgeFoot+new Point(0f,-2f)});
            Check(currentEscape==(left||up||down),
                "Paired pulse requires a real unblocked opposite/vertical dodge lane, never the covered east side");
            for(int n=0;n<63;n++)g.Tick(.05f,new Point());
            Check(g.BridgeOfChordsActive&&
                g.hazards.Exists(h=>object.ReferenceEquals(h.bossOwner,elite)),
                "The original delayed ground-hazard pipeline actually telegraphs elite-owned strikes");
            DefeatTrueBridgeElite(g,elite);
            Check(!g.hazards.Exists(h=>object.ReferenceEquals(h.bossOwner,elite)),
                "Real victory retires ONLY this dead elite's outstanding ground warnings");
        }
        public static void BrokenChordBridgePhysicalTwoForkFightAndTwoPermanentCivicEndings(Campaign data)
        {
            for(int ending=0;ending<2;ending++)
            {
                var g=BridgeReady(data);
                Enemy elite=BeginBridgeAtOriginalCore(g);
                int level=g.save.level;
                GroundBridgeFork(g,"bridge_low","bridge_chords_low",1);
                Check(elite.stun>=3.2f&&g.BridgeOfChordsPulse>=3.4f,
                    "First live grounded fork interrupts the REAL elite's attack and pulse clock");
                GroundBridgeFork(g,"bridge_high","bridge_chords_high",2);
                Check(g.BridgeOfChordsResonators==2&&
                    !g.BridgeOfChordsCalibrated,
                    "Real fork decisions alone cannot forge earned final mastery before lethal victory");
                g.Talk(BridgeSite(g,"bridge_low"));
                Check(!BridgeChoice(g,"bridge_chords_low")&&
                    g.BridgeOfChordsResonators==2,
                    "Returning to used fork cannot spend again or double-charge stamina");
                DefeatTrueBridgeElite(g,elite);
                Check(g.BridgeOfChordsCalibrated&&
                    g.GetQuest("bridge_chords").steps[1].done,
                    "Only two physically grounded stations plus genuine elite death bank mastery");
                WorldObject heart=BridgeSite(g,"bridge_heart");
                Check(PlaceWithinTalkReach(g,heart),
                    "Return physically to original Echo Core for final allocation");
                int coins=g.save.coins,shards=g.save.shards;
                // Compare identical post-kill v2 state without the optional
                // two-fork receipt: normal 130 XP may grant level-up skill.
                var matched=CloneSave(g.save);
                matched.flags.Remove("bridge_chords_calibrated");
                var control=new GameCore(data,matched);
                control.Start();control.enemies.Clear();
                Check(PlaceWithinTalkReach(control,BridgeSite(control,"bridge_heart")),
                    "Matched no-fork control can reach same original allocation source");
                control.Talk(BridgeSite(control,"bridge_heart"));
                string action=ending==0?"bridge_chords_shelter":"bridge_chords_grid";
                Check(BridgeChoice(control,action)&&control.BridgeOfChordsComplete,
                    "No-fork Boss kill can still receive the ordinary civic ending");
                g.Talk(heart);
                Check(BridgeChoice(g,action)&&g.BridgeOfChordsComplete&&
                    (ending==0?g.BridgeOfChordsShelter&&!g.BridgeOfChordsGrid:
                        g.BridgeOfChordsGrid&&!g.BridgeOfChordsShelter)&&
                    g.save.coins==coins+90&&g.save.shards==shards+2&&
                    g.save.level==control.save.level&&
                    g.save.skillPoints==Math.Min(99,control.save.skillPoints+1),
                    "Two real tuning forks earn exactly one additional talent beyond identical XP and level-up credits");
                g.Talk(heart);
                Check(!BridgeChoice(g,ending==0?"bridge_chords_grid":"bridge_chords_shelter")&&
                    g.save.coins==coins+90,
                    "No second branch, duplicate skill or coin farming after final allocation");
                g.Close();g.PrepareSave();
                var reloaded=new GameCore(data,CloneSave(g.save));
                Check(reloaded.save.version==2&&reloaded.BridgeOfChordsComplete&&
                    reloaded.BridgeOfChordsCalibrated&&
                    reloaded.GetQuest("bridge_chords").complete,
                    "Earned physical elite, both forks and chosen civic service persist through old save v2");
                // The actual Hearth Inn traveler remembers the true
                // post-fight decision, not a copied mission-only cutscene.
                SilentLedgerVisitRealInnFromEmberfall(g);
                ReadInnNews(g,Object(g,"innGuest"));
                Check(ending==0?
                    g.conversation.textEn.Contains("bridge carpenter brings a brass resonance fork")&&
                    !g.conversation.textEn.Contains("old bridge's power is back"):
                    g.conversation.textEn.Contains("old bridge's power is back")&&
                    !g.conversation.textEn.Contains("bridge carpenter brings a brass resonance fork"),
                    "Original Hearth Inn traveler recounts exactly the earned real bridge outcome");
                g.Close();g.Enter(4);g.enemies.Clear();
                heart=BridgeSite(g,"bridge_heart");
                // Compare ordinary safe resource regeneration against the
                // exact same Citadel coordinates without its outcome flag.
                Check(PlaceWithinTalkReach(g,heart),"Final core provides physically reachable recovery");
                var noRelief=CloneSave(g.save);
                noRelief.flags.Remove(ending==0?"bridge_chords_shelter":"bridge_chords_grid");
                var placebo=new GameCore(data,noRelief);
                placebo.Start();placebo.enemies.Clear();
                placebo.save.x=g.save.x;placebo.save.y=g.save.y;
                g.enemies.Clear();
                if(ending==0)g.save.health=placebo.save.health=20f;
                else g.save.energy=placebo.save.energy=20f;
                for(int t=0;t<26;t++)
                {g.Tick(.05f,new Point());placebo.Tick(.05f,new Point());}
                Check(ending==0?
                    g.save.health>placebo.save.health+1.5f:
                    g.save.energy>placebo.save.energy+2.8f,
                    "The earned branching outcome changes actual safe terrain recovery instead of a fake lore flag");
            }
        }
        public static void BrokenChordBridgeGroundedForkCanDischargeOnlyRealWarnedStrikes(Campaign data)
        {
            var g=BridgeReady(data);
            Enemy elite=BeginBridgeAtOriginalCore(g);
            GroundBridgeFork(g,"bridge_low","bridge_chords_low",1);
            WorldObject low=BridgeSite(g,"bridge_low");
            g.Talk(low);
            Check(!BridgeChoice(g,"bridge_chords_dampen")&&
                !g.BridgeOfChordsCanDischarge,
                "A used fork cannot charge stamina or block attacks before a real warning exists");
            g.Close();
            Point pulseFoot=new Point(48.5f,38.5f);
            Check(g.CanStand(pulseFoot),"True old bridge contains a walkable pulse landing");
            g.save.x=pulseFoot.x;g.save.y=pulseFoot.y;
            for(int n=0;n<160&&!g.BridgeOfChordsCanDischarge;n++)
                g.Tick(.05f,new Point());
            Check(g.BridgeOfChordsActive&&g.BridgeOfChordsCanDischarge&&
                g.hazards.Exists(h=>h!=null&&h.kind=="rift"&&
                    object.ReferenceEquals(h.bossOwner,elite)&&h.timer>0f),
                "A live elite actually telegraphs a cancellable delayed strike after low-fork grounding");
            var otherOwner=new Enemy(9501,0,3,3,30f);
            var foreign=new Hazard{kind="rift",timer=5f,
                position=pulseFoot,bossOwner=otherOwner,radius=1f,damage=30f};
            var ambient=new Hazard{kind="rift",timer=5f,
                position=pulseFoot,radius=1f,damage=30f};
            g.hazards.Add(foreign);g.hazards.Add(ambient);
            float energy=g.save.energy;
            int resonators=g.BridgeOfChordsResonators;
            Check(PlaceWithinTalkReach(g,low),"The same grounded original fork remains reachable");
            g.Talk(low);
            Check(BridgeChoice(g,"bridge_chords_dampen")&&
                Math.Abs(g.save.energy-(energy-8f))<.001f&&
                g.BridgeOfChordsResonators==resonators&&
                !g.hazards.Exists(h=>h!=null&&h.kind=="rift"&&
                    object.ReferenceEquals(h.bossOwner,elite))&&
                g.hazards.Contains(foreign)&&g.hazards.Contains(ambient),
                "Real on-site counterplay spends exactly 8 stamina and retracts ONLY current elite-owned rifts");
            g.Talk(low);
            Check(!BridgeChoice(g,"bridge_chords_dampen")&&
                Math.Abs(g.save.energy-(energy-8f))<.001f,
                "A used warning cannot be discharged twice or farm stamina");
            g.Close();
            Check(!g.BridgeOfChordsDefeated&&!g.BridgeOfChordsCalibrated&&
                g.save.version==2,
                "Counterplay changes combat danger without faking Boss death, mastery or save schema");
        }
        public static void BrokenChordBridgeUncalibratedAndTimeoutDoNotForgeBossVictory(Campaign data)
        {
            var g=BridgeReady(data);Enemy elite=BeginBridgeAtOriginalCore(g);
            WorldObject high=BridgeSite(g,"bridge_high"),heart=BridgeSite(g,"bridge_heart");
            Check(PlaceWithinTalkReach(g,high),"High ground is physically reachable on the bridge");
            g.Talk(high);
            Check(!BridgeChoice(g,"bridge_chords_high")&&
                g.BridgeOfChordsResonators==0,
                "High fork alone cannot skip the actual low-then-high alignment");
            g.Close();DefeatTrueBridgeElite(g,elite);
            Check(!g.BridgeOfChordsCalibrated,
                "Direct victory remains valid but never fabricates both optional fork masteries");
            var retry=BridgeReady(data);
            BeginBridgeAtOriginalCore(retry);
            for(int n=0;n<2600&&retry.BridgeOfChordsActive;n++)
                retry.Tick(.05f,new Point());
            Check(!retry.BridgeOfChordsActive&&
                !retry.BridgeOfChordsDefeated&&
                !retry.Has("boss_4"),
                "Actual world time expires a transient challenge without forging regional Boss credit");
            Check(PlaceWithinTalkReach(retry,BridgeSite(retry,"bridge_heart")),
                "Expired challenge can be retried at the same real original site");
            retry.Talk(BridgeSite(retry,"bridge_heart"));
            Check(BridgeChoice(retry,"bridge_chords_start")&&retry.BridgeOfChordsActive,
                "Time-out resets the true elite and permits a free safe new encounter");
        }
    }
}
