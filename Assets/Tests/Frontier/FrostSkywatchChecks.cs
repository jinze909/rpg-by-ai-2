using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FrostSkywatchIsPlayableOrderedMysteryWithRealBoss(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("skywatch").locked,
                "Original early saves discover the skywatch without bypassing story gates");
            WorldObject yilan=Object(g,"skywatcher");
            g.save.x=yilan.x;g.save.y=yilan.y;
            Check(g.CanStand(g.Position) && g.Nearest()==yilan,
                "The new astronomer occupies a real reachable town location");
            g.Talk(yilan);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_accept")<0,
                "The ancient highland mission does not bypass the original mountain report");
            g.Close();
            g.save.stage=3;g.save.level=12;g.save.xp=0;
            g.save.flags.Add("mountain_report");
            // Preserve the original farshore chapter gate: Frostchime opens
            // only after the prior desert report, not from a new side quest.
            g.save.flags.Add("desert_report");
            g.save.x=yilan.x;g.save.y=yilan.y;
            Action(g,"skywatcher","skywatch_accept");
            Check(g.Has("skywatch_started") && !g.GetQuest("skywatch").locked &&
                g.save.trackedQuest=="skywatch",
                "Accepting Yilan's dialogue starts and tracks the real new quest");
            g.Enter(7);g.enemies.Clear();
            for(int i=1;i<=3;i++)
            {
                WorldObject lens=Object(g,"skyprism"+i);
                g.save.x=lens.x;g.save.y=lens.y;
                Check(g.CanStand(g.Position) && g.Nearest()==lens,
                    "Each unique authored lens is reachable and physically interactable");
            }
            WorldObject second=Object(g,"skyprism2");
            g.save.x=second.x;g.save.y=second.y;
            g.Talk(second);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_align_2_2")<0,
                "The east lens cannot bypass the unsolved northern reading");
            g.Close();
            WorldObject first=Object(g,"skyprism1");
            g.save.x=first.x;g.save.y=first.y;
            float energy=g.save.energy;
            Action(g,"skyprism1","skywatch_align_1_2");
            Check(g.save.energy==energy-8 && !g.Has("skywatch_prism_1"),
                "A wrong physical puzzle choice spends real energy without awarding progress");
            Action(g,"skyprism1","skywatch_align_1_1");
            Check(g.Has("skywatch_prism_1"),"Correct frost reading persists");
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"skyprism2","skywatch_align_2_2");
            WorldObject third=Object(g,"skyprism3");
            g.save.x=third.x;g.save.y=third.y;
            Action(g,"skyprism3","skywatch_align_3_3");
            Check(g.Has("skywatch_prism_1") && g.Has("skywatch_prism_2") &&
                g.Has("skywatch_prism_3") && !g.GetQuest("skywatch").complete,
                "Three real calibrated prisms unlock the observatory but not its reward");
            WorldObject altar=Object(g,"skyobservatory");
            g.save.x=altar.x;g.save.y=altar.y;g.enemies.Clear();
            Check(g.CanStand(g.Position) && g.Nearest()==altar,
                "The new observatory exists on real accessible Frostchime ground");
            int coins=g.save.coins, shards=g.save.shards, talent=g.save.skillPoints;
            int kills=g.save.kills[4];
            Action(g,"skyobservatory","skywatch_start");
            Enemy boss=g.enemies.Find(g.IsSkywatchEnemy);
            Check(g.SkywatchRoom==7 && boss!=null && boss.maxHp==240 &&
                boss.boss && g.SkywatchTime>84 &&
                g.CanStand(boss.Position) &&
                g.NavigationPath(boss.Position).Count>0,
                "The skywatch trial starts a genuine reachable boss with normal combat AI");
            Check(g.save.coins==coins && g.save.shards==shards &&
                !g.Has("skywatch_defeated"),
                "Starting the fight never pays unearned first-clear rewards");
            // Defeat with the actual golden-haired hero's melee input. This
            // also catches combat-hit integration and anti-farming regressions.
            boss.stun=120;boss.timer=120;
            g.save.x=boss.x-1.05f;g.save.y=boss.y;
            g.save.maxHealth=3000;g.save.health=3000;g.invulnerable=120;
            float originalHp=boss.hp;
            for(int turn=0;turn<45 && g.enemies.Contains(boss);turn++)
            {
                g.save.energy=100;
                g.Attack(new Point(1,0));
                for(int tick=0;tick<12;tick++) g.Tick(.05f,new Point());
            }
            Check(boss.hp<originalHp && !g.enemies.Contains(boss),
                "Real playable melee inputs, not test-only enemy removal, defeat the new elite");
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(g.Has("skywatch_defeated") && g.SkywatchRoom==0 &&
                !g.hazards.Exists(h=>h.skywatchOwned) &&
                g.save.coins==coins && g.save.kills[4]==kills,
                "Combat clear has no normal loot, chapter kill or lingering hazard");
            g.Enter(0);g.save.x=yilan.x;g.save.y=yilan.y;
            Action(g,"skywatcher","skywatch_publish");
            Check(g.Has("skywatch_complete") && g.GetQuest("skywatch").complete &&
                g.save.coins==coins+95 && g.save.shards==shards+3 &&
                g.save.skillPoints>=talent+1,
                "Public spectrum archives exactly one final permanent reward");
            g.Talk(yilan);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_publish")<0,
                "Completed story cannot pay out another first-clear prize");
            g.Close();g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.Has("skywatch_complete") && loaded.GetQuest("skywatch").complete &&
                loaded.save.coins==g.save.coins,
                "The new ending survives old-format save/reload without schema migration");
            loaded.Enter(7);loaded.enemies.Clear();
            loaded.save.x=altar.x;loaded.save.y=altar.y;
            Check(loaded.SkywatchRefuge,"Completed observatory opens persistent cold refuge");
            loaded.save.cold=40;
            loaded.Tick(.05f,new Point());
            Check(loaded.save.cold<40,
                "Refuge changes actual Frostchime cold exposure, not only the icon");
        }
        public static void FrostSkywatchChronicleOnlyRevealsObservedEvidence(Campaign data)
        {
            var g=Fresh(data);
            WorldObject book=Object(g,"worldchronicle");
            g.Talk(book);
            int section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");
            Check(section>=0,"Original chronicle has an aftermath section");
            g.Choose(section);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch")<0,
                "The new observatory chapter is hidden before Yilan's commission");
            g.Close();g.save.flags.Add("skywatch_started");
            g.Talk(book);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");
            g.Choose(section);
            int page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");
            Check(page>=0,"Accepting the real quest unlocks a playable-chronicle chapter");
            g.Choose(page);
            Check(g.conversation.text.Contains("读数尚未取回") &&
                !g.conversation.text.Contains("观星台旁留下永久避寒穹顶"),
                "The book does not spoil readings or claim an unearned final refuge");
            g.Close();
            g.save.flags.Add("skywatch_prism_1");
            g.save.flags.Add("skywatch_prism_2");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("第一场冻雨") &&
                g.conversation.text.Contains("并非太阳") &&
                !g.conversation.text.Contains("已经停止攻击旅人"),
                "Only actually recovered prism clues appear in the bilingual story archive");
            g.Close();g.save.flags.Add("skywatch_prism_3");
            g.save.flags.Add("skywatch_defeated");g.save.flags.Add("skywatch_complete");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("永久避寒穹顶") &&
                g.conversation.textEn.Contains("permanent shelter"),
                "Only a truly published observatory displays the public weather ending");
        }

        public static void FrostSkywatchHazardsAreDodgeableAndWithdrawalIsSafe(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=12;
            foreach(string flag in new[]{"mountain_report","desert_report","skywatch_started",
                "skywatch_prism_1","skywatch_prism_2","skywatch_prism_3"})
                g.save.flags.Add(flag);
            g.Enter(7);g.enemies.Clear();
            WorldObject altar=Object(g,"skyobservatory");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_start");
            Enemy boss=g.enemies.Find(g.IsSkywatchEnemy);
            Check(boss!=null,"A live guardian owns timed environmental hazards");
            boss.stun=90;boss.timer=90;g.invulnerable=90;
            Point original=g.Position;
            for(int tick=0;tick<65;tick++)g.Tick(.05f,new Point());
            Hazard warning=g.hazards.Find(h=>h.skywatchOwned);
            Check(warning!=null && (warning.position-original).Length<.3f &&
                warning.timer>0 && warning.radius>=.9f,
                "Guardian warns of an actual delayed contact at the hero's feet");
            Point[] escapes={new Point(2.1f,0),new Point(-2.1f,0),
                new Point(0,2.1f),new Point(0,-2.1f)};
            bool escaped=false;
            foreach(Point delta in escapes)
                if(g.CanStand(original+delta) &&
                    (original+delta-warning.position).Length>warning.radius+.5f)
                { g.save.x=original.x+delta.x;g.save.y=original.y+delta.y;escaped=true;break; }
            Check(escaped,"The authentic highland arena offers a reachable dodge exit");
            float hp=g.save.health;g.invulnerable=0;
            for(int i=0;i<35;i++)g.Tick(.05f,new Point());
            Check(g.save.health>=hp-.01f,
                "Moving out of the warned footprint avoids the ordinary damaging hazard");
            var ordinary=new Hazard{position=g.Position,timer=4,radius=.5f,
                kind="rift",damage=2};
            g.hazards.Add(ordinary);
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_withdraw");
            Check(g.SkywatchRoom==0 && !g.enemies.Exists(g.IsSkywatchEnemy) &&
                !g.hazards.Exists(h=>h.skywatchOwned) &&
                g.hazards.Contains(ordinary) && !g.Has("skywatch_defeated"),
                "Voluntary withdrawal removes only the skywatch encounter and never gives rewards");
            g.hazards.Remove(ordinary);g.enemies.Clear();
            Action(g,"skyobservatory","skywatch_start");
            Check(g.SkywatchRoom==7,"The observatory supports a real free retry");
            typeof(GameCore).GetProperty("SkywatchTime")
                .GetSetMethod(true).Invoke(g,new object[]{.01f});
            g.Tick(.05f,new Point());
            Check(g.SkywatchRoom==0 && !g.Has("skywatch_defeated") &&
                !g.enemies.Exists(g.IsSkywatchEnemy) &&
                !g.hazards.Exists(h=>h.skywatchOwned),
                "Time expiry never grants a fake boss clear or leaves invisible combat behind");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.Has("skywatch_prism_3") && !loaded.Has("skywatch_defeated") &&
                loaded.SkywatchRoom==0,
                "Reload preserves completed exploration but not an unfinished boss fight");
        }
    }
}
