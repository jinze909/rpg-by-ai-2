using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ThreeCoreReckoningBridgesCampaignAndChangesRealFinalBoss(Campaign data)
        {
            var g=Fresh(data);
            WorldObject lyra=Object(g,"guild");
            g.save.x=lyra.x;g.save.y=lyra.y;
            g.Talk(lyra);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="triad_accept")<0,
                "The new Chapter Four investigation must not bypass the first three actual core deliveries");
            g.Close();
            g.save.stage=3;g.save.level=20;g.save.xp=0;
            foreach(string f in new[]{"core_1","core_2","core_3","scout_saved","gate_2","gate_3"})
                g.save.flags.Add(f);
            g.Talk(lyra);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="triad_accept"),
                "Lyra starts a substantive Chapter Four investigation after three genuine core deliveries");
            g.Close();
            Action(g,"guild","triad_accept");
            Check(g.Has("triad_started") && g.TriadEvidence==0 &&
                g.CurrentGuide.id=="triad_reckoning" &&
                g.CurrentGuide.next.Contains("堡垒") &&
                !g.Has("boss_4"),
                "Main journal points to the new playable bridge without granting boss or ending flags");
            g.Enter(4);g.enemies.Clear();g.invulnerable=300;
            for(int i=1;i<=3;i++)
            {
                WorldObject seal=Object(g,"triadmemory"+i);
                g.save.x=seal.x;g.save.y=seal.y;
                Check(g.CanStand(g.Position) && g.Nearest()==seal,
                    "Every real main-quest core-memory object is on interactable Citadel geometry");
                if(i==1)
                {
                    float energy=g.save.energy;
                    Action(g,seal.kind,"triad_answer_1_0");
                    Check(g.save.energy==energy-12 && !g.Has("triad_evidence_1"),
                        "A wrong moral inference costs real energy and cannot skip evidence");
                }
                Action(g,seal.kind,"triad_answer_"+i+"_"+(i==3?2:1));
                Check(g.Has("triad_evidence_"+i) && g.TriadEvidence==i,
                    "Correct field inference persistently records only evidence actually witnessed");
                g.PrepareSave();
                var recalled=new GameCore(data,CloneSave(g.save));recalled.Start();
                Check(recalled.save.version==2 && recalled.Has("triad_evidence_"+i),
                    "Old v2 saves retain the new main-quest evidence using existing string flags");
            }
            WorldObject nexus=Object(g,"triadnexus");
            g.save.x=nexus.x;g.save.y=nexus.y;
            Check(g.CanStand(g.Position) && g.Nearest()==nexus,
                "Central Citadel audit is a real reachable interactable machine");
            Action(g,"triadnexus","triad_defense_start");
            Check(g.TriadDefenseActive && g.TriadTime>=84 &&
                g.enemies.FindAll(g.IsTriadEnemy).Count==1 &&
                g.enemies.Find(g.IsTriadEnemy).hp>=210,
                "Original core audit summons real armored security with an 85-second deadline");
            g.Enter(0);
            Check(g.save.room==4 && g.TriadDefenseActive,
                "Travel may not abandon a live mandatory story investigation encounter");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,e=>e!=null && e.id==89960),
                "Scripted mainline security is not serialized into ordinary patrol snapshots");
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.TriadDefenseActive &&
                !loaded.enemies.Exists(loaded.IsTriadEnemy) &&
                loaded.TriadEvidence==3 && !loaded.Has("triad_defense_clear"),
                "Reload restarts unsafe combat but keeps genuine evidence, never ghost enemies or victory");
            g.save.x=nexus.x;g.save.y=nexus.y;
            g.Talk(nexus);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="triad_seal")<0,
                "Reading all three ledgers alone cannot bypass real security combat");
            g.Close();
            Enemy guard=g.enemies.Find(g.IsTriadEnemy);
            guard.hp=5;guard.stun=200;guard.timer=200;
            guard.x=nexus.x+1.05f;guard.y=nexus.y;
            int initialKills=g.save.kills[4],coins=g.save.coins;
            for(int frame=0;frame<100 && g.enemies.Contains(guard);frame++)
            {
                if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                g.Tick(.02f,new Point());
            }
            Check(!g.enemies.Contains(guard),"The canonical golden-haired hero must actually defeat the security guardian");
            g.Tick(.02f,new Point());
            Check(g.Has("triad_defense_clear") && !g.TriadDefenseActive &&
                g.save.coins==coins && g.save.kills[4]==initialKills,
                "Temporary mainline guardian cannot farm ordinary loot, chapter kills or prematurely mint rewards");
            Action(g,"triadnexus","triad_seal");
            Check(g.Has("triad_sealed") && !g.Has("triad_complete"),
                "The in-world audit archive is a separate real interaction before Lyra's publication");
            g.Enter(0);g.enemies.Clear();
            g.save.x=lyra.x;g.save.y=lyra.y;
            int rewards=g.save.coins;
            Action(g,"guild","triad_publish");
            Check(g.Has("triad_complete") && g.save.coins==rewards+55 &&
                g.save.shards>=2 && !g.Has("boss_4") &&
                g.GetQuest("main").steps.Exists(st=>st.done),
                "Publishing actual core evidence yields first-time story compensation without granting boss defeat");
            g.Talk(lyra);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="triad_publish")<0,
                "The public story milestone is strictly one-time");
            g.Close();
            g.Enter(4);g.enemies.Clear();
            WorldObject altar=Object(g,"shrine");
            g.save.x=altar.x;g.save.y=altar.y;
            g.Talk(altar);
            Check(g.conversation.text.Contains("三核审计") &&
                g.conversation.textEn.Contains("original audit"),
                "The real final confrontation reacts to evidence physically gathered in the campaign");
            g.Close();
            Action(g,"shrine","boss");
            Enemy orrin=g.enemies.Find(e=>e.boss);
            Check(orrin!=null && Math.Abs(orrin.maxHp-data.bosses[3].hp*.85f)<.15f &&
                !g.Has("boss_4"),
                "Published core audit reduces real Orrin HP by 15% without handing out combat victory");
        }

        public static void ThreeCoreReckoningSecurityPulsesDodgeAbortAndReloadSafely(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string f in new[]{"core_1","core_2","core_3","triad_started",
                "triad_evidence_1","triad_evidence_2","triad_evidence_3"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();
            WorldObject nexus=Object(g,"triadnexus");
            g.save.x=nexus.x;g.save.y=nexus.y;
            Action(g,"triadnexus","triad_defense_start");
            Check(g.TriadDefenseActive,"Core audit starts only after real field evidence");
            foreach(Enemy e in g.enemies.FindAll(g.IsTriadEnemy))
            {e.stun=100;e.timer=100;}
            g.invulnerable=250;
            Advance(g,3.5f);
            Check(g.hazards.Exists(h=>h.triadOwned && h.timer>0 && h.damage>0),
                "Security creates genuine delayed damage warnings the hero can dodge");
            var normal=new Hazard{position=new Point(8,13),timer=20,radius=.7f,
                damage=4,kind="root"};
            var effect=new Effect("discover",new Point(8,13),20);
            g.hazards.Add(normal);g.effects.Add(effect);
            g.save.x=nexus.x;g.save.y=nexus.y;
            Action(g,"triadnexus","triad_abort");
            Check(!g.TriadDefenseActive && !g.enemies.Exists(g.IsTriadEnemy) &&
                !g.hazards.Exists(h=>h.triadOwned) && g.hazards.Contains(normal) &&
                g.effects.Contains(effect) && g.TriadEvidence==3 &&
                !g.Has("triad_defense_clear"),
                "Aborting clears only audit-owned enemies, shots and warnings; unrelated world effects survive");
            g.hazards.Clear();g.effects.Clear();g.enemies.Clear();
            Action(g,"triadnexus","triad_defense_start");
            Check(g.TriadDefenseActive,"Free replay remains possible without duplicating ledger rewards");
            foreach(Enemy e in g.enemies.FindAll(g.IsTriadEnemy)){e.stun=150;e.timer=150;}
            g.invulnerable=200;
            Advance(g,85.3f);
            Check(!g.TriadDefenseActive && !g.Has("triad_defense_clear") &&
                !g.enemies.Exists(g.IsTriadEnemy),
                "Time limit restores audit lock rather than fabricating victory");
            g.Enter(0);
            Check(g.save.room==0,"Travel resumes after an audit timeout");
        }

        public static void ThreeCoreReckoningOldFinaleRouteRemainsAvailable(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string f in new[]{"core_1","core_2","core_3"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();
            WorldObject altar=Object(g,"shrine");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"shrine","boss");
            Enemy legacy=g.enemies.Find(e=>e.boss);
            Check(legacy!=null && Math.Abs(legacy.maxHp-data.bosses[3].hp)<.15f &&
                !g.Has("triad_complete"),
                "Preexisting Chapter Four saves are never retroactively locked out of Orrin or given unearned power");
        }

        public static void ThreeCoreAuditCreatesOneRealTacticalOrrinOpeningPerAttempt(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"core_1","core_2","core_3","triad_started",
                "triad_evidence_1","triad_evidence_2","triad_evidence_3",
                "triad_defense_clear","triad_sealed","triad_complete"})
                g.save.flags.Add(flag);
            g.Enter(4);g.enemies.Clear();
            WorldObject altar=Object(g,"shrine"),console=Object(g,"triadnexus");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"shrine","boss");
            Enemy orrin=g.enemies.Find(e=>e.boss);
            Check(orrin!=null && g.TriadBossInterruptReady &&
                Math.Abs(orrin.maxHp-data.bosses[3].hp*.85f)<.2f,
                "Real chapter finale begins with genuinely weakened, still-alive Orrin");
            float before=orrin.hp;
            g.save.x=console.x;g.save.y=console.y;
            Check(g.CanStand(g.Position) && g.Nearest()==console &&
                g.Conditions().Contains("中断屏障"),
                "Interactable central audit is reachable and HUD advertises actual combat counterplay");
            g.save.energy=24;
            Action(g,"triadnexus","triad_boss_interrupt");
            Check(g.save.energy==24 && g.TriadBossInterruptReady &&
                orrin.hp==before,"Energy shortage cannot give away the main-boss interrupt");
            g.save.energy=100;
            Action(g,"triadnexus","triad_boss_interrupt");
            Check(g.save.energy==75 && !g.TriadBossInterruptReady &&
                orrin.stun>=2.3f && orrin.hp==before &&
                !g.Has("boss_4") && !g.Has("warden_stood_down"),
                "Actual E/controller interaction spends 25 energy and staggers Orrin, without fake damage or victory");
            g.Talk(console);
            Check(g.mode=="world" && g.save.energy==75 &&
                orrin.hp==before,
                "One-use interrupt cannot be repeated or recharge while the same boss is alive");
            g.Enter(0);g.enemies.Clear();
            g.Enter(4);g.enemies.Clear();
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"shrine","boss");
            Check(g.TriadBossInterruptReady &&
                g.enemies.Exists(e=>e.boss && e.hp>0),
                "A legitimate new boss attempt restores exactly one interrupt opportunity");
        }

    }
}
