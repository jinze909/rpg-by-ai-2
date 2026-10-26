using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore StartWayfarerCoverFixture(Campaign data,WorldObject npc)
        {
            var rows=new string[30];
            for(int y=0;y<rows.Length;y++)
            {
                char[] cells=new string('#',30).ToCharArray();
                if(y>=2&&y<=27)
                    for(int x=2;x<=27;x++)cells[x]='.';
                rows[y]=new string(cells);
            }
            data.maps[1]=new MapData {
                id=1,spawnX=10.5f,spawnY=10.5f,rows=rows,
                objects=new[]{npc},decor=new Decoration[0]
            };
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            g.invulnerable=300f;g.save.energy=70;
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"wayfarer_defend_1");
            Check(g.WayfarerEncounterRoom==1&&g.enemies.FindAll(
                e=>e.id>=82010&&e.id<82012).Count==2,
                "Live traveler defense begins with two original real raiders");
            return g;
        }
        private static int WayfarerChoiceIndex(GameCore g,string action)
        {
            if(g.conversation==null)return -1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==action)return i;
            return -1;
        }
        public static void WayfarerMeleeWarningAppearsBeforeRealDamageAndTracksMovingCover(Campaign data)
        {
            MapData previous=data.maps[1];
            try
            {
                var npc=new WorldObject {id="wayfarer1",kind="wayfarer1",
                    name="Traveler",en="Traveler",x=10.5f,y=10.5f};
                var g=StartWayfarerCoverFixture(data,npc);
                var attacker=g.enemies.Find(e=>e.kind==0);
                var guard=g.enemies.Find(e=>e.kind==2);
                Check(attacker!=null&&guard!=null,
                    "The original traveler defense contains both living raiders");
                g.save.x=20.5f;g.save.y=19.5f;
                attacker.x=11.6f;attacker.y=10.5f;
                attacker.state=0;attacker.timer=.001f;
                attacker.stun=attacker.hitTime=attacker.releaseTime=0f;
                guard.stun=100f;guard.timer=100f;
                // This is the original physical civilian objective, not a
                // scripted UI-only illustration: the actual enemy Tick
                // decides whether the traveler is the locked target.
                g.Tick(.05f,new Point());
                Check(attacker.state==1&&attacker.targetingWayfarer&&
                    (attacker.target-new Point(npc.x,npc.y)).Length<.01f,
                    "The ordinary enemy really commits a timed strike against the correct traveler");
                Check(g.effects.Exists(fx=>fx.kind=="danger"&&
                    fx.wayfarerOwned&&fx.radius>0f&&
                    (fx.position-new Point(npc.x,npc.y)).Length<.01f),
                    "The traveler gets a visible owned ground warning BEFORE the actual melee damage frame");
                Check(g.sound=="windup",
                    "Distant physical traveler threats now use the existing audible windup cue");
                float before=g.WayfarerIntegrity;
                guard.x=11.03f;guard.y=10.5f;
                for(int tick=0;tick<16;tick++)g.Tick(.05f,new Point());
                Check(g.WayfarerEncounterRoom==1&&
                    g.WayfarerIntegrity==before&&attacker.state==0,
                    "A live second raider crossing the warned lane prevents the real contact without aborting rescue");
                Check(g.effects.Exists(fx=>fx.kind=="number"&&
                    fx.wayfarerOwned&&
                    fx.text==g.L("队友挡路","ALLY BLOCK")),
                    "Actual interception provides one original encounter-owned combat feedback, not a counterfeit traveler hit");
                // The very same raider becomes dangerous again when the
                // living defender clears its foot and a fresh timed attack
                // is earned normally.
                guard.x=18.5f;guard.y=18.5f;
                attacker.state=0;attacker.timer=.001f;
                attacker.stun=attacker.hitTime=attacker.releaseTime=0f;
                g.Tick(.05f,new Point());
                Check(attacker.state==1&&
                    g.effects.Exists(fx=>fx.kind=="danger"&&fx.wayfarerOwned),
                    "A newly clear physical lane genuinely reissues its readable pre-contact warning");
                for(int tick=0;tick<16;tick++)g.Tick(.05f,new Point());
                Check(g.WayfarerIntegrity<before&&
                    g.WayfarerEncounterRoom==1&&g.save.version==2,
                    "An unblocked warned strike finally damages real traveler safety without migrating existing save slots");
            }
            finally{data.maps[1]=previous;}
        }

        public static void WayfarerCoverNeedsLiveReachAndOneActualEnergyPayment(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var npc=new WorldObject { id="wayfarer1",kind="wayfarer1",
                    name="Traveler",en="Traveler",x=10.5f,y=10.5f };
                var g=StartWayfarerCoverFixture(data,npc);
                g.Talk(npc);
                int cover=WayfarerChoiceIndex(g,"wayfarer_cover");
                Check(cover>=0&&g.WayfarerCoverAvailable,
                    "The genuine active civilian offers one timed energy-paid cover command");
                int money=g.save.coins,inv=g.save.inventory[2];
                float energy=g.save.energy;
                char[] cells=g.Map.rows[10].ToCharArray();
                cells[11]='#';g.Map.rows[10]=new string(cells);
                g.save.x=12.25f;g.save.y=10.5f;
                Check(g.CanStand(g.Position)&&g.CanStand(new Point(npc.x,npc.y)),
                    "Stale cover dialogue remains reachable only through real NPC collision");
                g.Choose(cover);
                Check(g.WayfarerCoverAvailable&&g.WayfarerCoverTime==0&&
                    g.save.energy==energy&&g.save.inventory[2]==inv,
                    "A sealed wall prevents remote cover without spending energy or supplies");
                cells[11]='.';g.Map.rows[10]=new string(cells);
                g.Talk(npc);
                cover=WayfarerChoiceIndex(g,"wayfarer_cover");
                Check(cover>=0,"Reopened physical approach reoffers the genuine support command");
                g.Choose(cover);
                Check(!g.WayfarerCoverAvailable&&g.WayfarerCoverTime==8f&&
                    Math.Abs(g.save.energy-(energy-20f))<.001f&&
                    g.save.coins==money&&g.save.inventory[2]==inv &&
                    g.effects.Exists(e=>e.wayfarerOwned&&e.kind=="discover"),
                    "Cover costs exactly 20 energy, creates live field effect and never awards loot");
                g.Talk(npc);
                Check(WayfarerChoiceIndex(g,"wayfarer_cover")<0,
                    "The once-per-ambush cover action disappears after genuine deployment");
                g.Close();
                foreach(Enemy e in g.enemies){e.stun=99f;e.timer=99f;}
                for(int i=0;i<170;i++)g.Tick(.05f,new Point());
                Check(g.WayfarerCoverTime<=.0001f&&
                    !g.WayfarerCoverAvailable&&g.WayfarerEncounterRoom==1,
                    "Cover expires during real combat time and cannot be purchased twice");
                g.save.x=npc.x;g.save.y=npc.y;
                g.Talk(npc);
                Check(WayfarerChoiceIndex(g,"wayfarer_cover")<0,
                    "An expired cover still stays spent for the same encounter");
                int abandon=WayfarerChoiceIndex(g,"wayfarer_abandon");
                Check(abandon>=0,"Existing physical withdraw remains available");
                g.Choose(abandon);
                Check(g.WayfarerEncounterRoom==0&&g.WayfarerCoverTime==0&&
                    g.save.version==2&&!g.Has("wayfarer_1"),
                    "Voluntary withdrawal clears transient defense without inventing v2 progress");
                Action(g,npc.kind,"wayfarer_defend_1");
                Check(g.WayfarerEncounterRoom==1&&g.WayfarerCoverAvailable,
                    "A fresh pair of raiders allows a genuinely new rally attempt");
            }
            finally{data.maps[1]=old;}
        }

        public static void WayfarerCoverHalvesActualReleasedEnemyContactsOnlyWhileActive(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var npc=new WorldObject {id="wayfarer1",kind="wayfarer1",
                    name="Traveler",en="Traveler",x=10.5f,y=10.5f};
                var g=StartWayfarerCoverFixture(data,npc);
                g.Talk(npc);
                int cover=WayfarerChoiceIndex(g,"wayfarer_cover");
                Check(cover>=0,"Physical support option appears during genuine rescue");
                g.Choose(cover);
                Enemy attacker=g.enemies.Find(e=>e.id==82010);
                Enemy other=g.enemies.Find(e=>e.id==82011);
                Check(attacker!=null&&other!=null&&attacker.kind==0,
                    "The actual small-raider attack owner is present and identity stable");
                other.stun=100;other.timer=100;
                g.save.x=23.5f;g.save.y=10.5f;
                attacker.x=npc.x+1.5f;attacker.y=npc.y;
                attacker.state=1;attacker.timer=.01f;
                attacker.stun=0;attacker.releaseTime=0;
                attacker.target=new Point(npc.x,npc.y);
                attacker.targetingWayfarer=true;
                attacker.Face(attacker.target-attacker.Position);
                float before=g.WayfarerIntegrity;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.WayfarerIntegrity-(before-9f))<.001f &&
                    g.effects.Exists(e=>e.wayfarerOwned&&e.kind=="number"&&
                        e.text.Contains("掩护减伤")),
                    "A real released 18-damage raider contact deals 9 with visible civilian cover feedback");
                attacker.state=0;attacker.stun=100f;attacker.timer=100f;
                for(int i=0;i<164;i++)g.Tick(.05f,new Point());
                Check(g.WayfarerCoverTime<=.0001f,
                    "Eight seconds of actual world simulation expire the shield");
                attacker.state=1;attacker.timer=.01f;
                attacker.stun=0;attacker.releaseTime=0;attacker.hitTime=0;
                attacker.target=new Point(npc.x,npc.y);
                attacker.targetingWayfarer=true;
                attacker.Face(attacker.target-attacker.Position);
                before=g.WayfarerIntegrity;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.WayfarerIntegrity-(before-18f))<.001f &&
                    g.WayfarerEncounterRoom==1&&!g.Has("wayfarer_1"),
                    "The same real raider hits for its full original damage once timed cover expires");
            }
            finally{data.maps[1]=old;}
        }

        public static void WayfarerCoverWorksAtAllThreeRealRegionalTravelers(Campaign data)
        {
            for(int room=1;room<=3;room++)
            {
                var g=Fresh(data);g.save.stage=3;g.save.level=10;
                g.save.flags.Add("scout_saved");
                g.Enter(room);g.enemies.Clear();g.invulnerable=120f;
                WorldObject npc=Object(g,"wayfarer"+room);
                g.save.x=npc.x;g.save.y=npc.y;g.save.energy=45f;
                Action(g,npc.kind,"wayfarer_defend_"+room);
                Check(g.WayfarerEncounterRoom==room,
                    "Each original regional civilian launches its own real two-raider encounter");
                g.Talk(npc);
                int cover=WayfarerChoiceIndex(g,"wayfarer_cover");
                Check(cover>=0&&g.WayfarerCoverAvailable,
                    "Real regional traveler offers an energy-paid one-use protective rally: "+room);
                g.Choose(cover);
                Check(g.WayfarerCoverTime==8f&&g.save.energy==25f&&
                    g.WayfarerEncounterRoom==room&&!g.Has("wayfarer_"+room),
                    "Each true map uses the same live cover without an unearned quest reward: "+room);
                g.Talk(npc);
                Check(WayfarerChoiceIndex(g,"wayfarer_cover")<0,
                    "All three authentic NPCs hide their spent command until a new rescue: "+room);
            }
        }

        public static void WayfarerCoverHalvesHeavyContactWithoutReplacingBodyguard(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var npc=new WorldObject{id="wayfarer1",kind="wayfarer1",
                    name="Traveler",en="Traveler",x=10.5f,y=10.5f};
                var g=StartWayfarerCoverFixture(data,npc);
                g.Talk(npc);
                g.Choose(WayfarerChoiceIndex(g,"wayfarer_cover"));
                Enemy heavy=g.enemies.Find(e=>e.id==82011);
                Enemy hunter=g.enemies.Find(e=>e.id==82010);
                Check(heavy!=null&&heavy.kind==2&&hunter!=null,
                    "Real second ambusher is the original area-impact armored breaker");
                hunter.stun=100f;hunter.timer=100f;
                heavy.x=npc.x+1.5f;heavy.y=npc.y;
                heavy.state=1;heavy.timer=.01f;heavy.stun=0f;
                heavy.releaseTime=0f;heavy.hitTime=0f;
                heavy.target=new Point(npc.x,npc.y);
                heavy.targetingWayfarer=true;
                heavy.Face(heavy.target-heavy.Position);
                g.save.x=22.5f;g.save.y=10.5f;
                float before=g.WayfarerIntegrity;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.WayfarerIntegrity-(before-12f))<.001f,
                    "An actual 24-damage heavy raider strike causes only 12 through cover");
                // In the NEXT real enemy windup the hero physically takes
                // the lane. Full interception has precedence over the
                // residual 12-damage cover calculation.
                heavy.x=npc.x+1.5f;heavy.y=npc.y;
                heavy.state=1;heavy.timer=.01f;heavy.stun=0f;
                heavy.releaseTime=0f;heavy.hitTime=0f;
                heavy.target=new Point(npc.x,npc.y);
                heavy.targetingWayfarer=true;
                heavy.Face(heavy.target-heavy.Position);
                g.save.x=npc.x+.75f;g.save.y=npc.y;
                g.invulnerable=120f;
                before=g.WayfarerIntegrity;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.WayfarerIntegrity-before)<.001f &&
                    g.effects.Exists(e=>e.kind=="number"&&
                        e.wayfarerOwned&&e.text=="护卫拦截"),
                    "A true heavy contact intercepted by the hero grants no phantom civilian injury despite cover");
            }
            finally{data.maps[1]=old;}
        }

        public static void WayfarerCoverRequiresSufficientEnergyAndNeverSurvivesAbandon(Campaign data)
        {
            MapData old=data.maps[1];
            try
            {
                var npc=new WorldObject {id="wayfarer1",kind="wayfarer1",
                    name="Traveler",en="Traveler",x=10.5f,y=10.5f};
                var g=StartWayfarerCoverFixture(data,npc);
                g.save.energy=19.5f;
                g.Talk(npc);
                Check(!g.WayfarerCoverAvailable&&
                    WayfarerChoiceIndex(g,"wayfarer_cover")<0,
                    "Insufficient energy hides cover without hiding medicine or withdraw");
                g.save.energy=20f;
                g.Talk(npc);
                int index=WayfarerChoiceIndex(g,"wayfarer_cover");
                Check(index>=0,"Exactly 20 energy offers the command");
                g.save.energy=19f;
                g.Choose(index);
                Check(g.WayfarerCoverTime==0&&g.save.energy==19f,
                    "Energy spent by another action invalidates the previously displayed command");
                g.save.energy=20f;g.Talk(npc);
                index=WayfarerChoiceIndex(g,"wayfarer_cover");
                g.Choose(index);
                Check(g.save.energy==0&&g.WayfarerCoverTime==8f,
                    "Exactly 20 actual energy is sufficient and never overdraws the resource");
                g.Talk(npc);
                int abandon=WayfarerChoiceIndex(g,"wayfarer_abandon");
                g.Choose(abandon);
                Check(g.WayfarerEncounterRoom==0&&!g.WayfarerCoverAvailable&&
                    g.WayfarerCoverTime==0,
                    "Aborting combat clears the timed shield and active equipment benefits");
                g.PrepareSave();
                var reload=new GameCore(data,CloneSave(g.save));
                reload.Start();
                Check(reload.WayfarerEncounterRoom==0&&
                    reload.WayfarerCoverTime==0&&
                    !reload.Has("wayfarer_1")&&reload.save.version==2,
                    "Real old-version save data does not store transient cover or forge rescue rewards");
            }
            finally{data.maps[1]=old;}
        }
    }
}
