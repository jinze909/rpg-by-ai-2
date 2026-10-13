using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void MoveWithBlackoutEscort(GameCore g, Point dest)
        {
            for(int i=0;i<1200 && (g.Position-dest).Length>.75f && g.BlackoutActive;i++)
            {
                Point delta=dest-g.Position;
                if(delta.Length<.04f)break;
                g.Tick(.02f,delta.Normalized);
            }
            Check((g.Position-dest).Length<.9f,
                "The gold-haired hero must be able to walk to the next route waypoint");
        }

        public static void BlackoutEvacuationRequiresActualThreePhysicalEscorts(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=500;
            WorldObject shelter=Object(g,"blackoutshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.Talk(shelter);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="blackout_start"),
                "Completed audit opens a real Chapter Four blackout evacuation at a reachable shelter");
            g.Close();
            Action(g,shelter.kind,"blackout_start");
            Check(g.BlackoutActive && g.BlackoutTime>179 &&
                g.BlackoutRescued==0 && g.CurrentGuide.id=="blackout",
                "Starting main story emergency initializes a real countdown and tracked objective");
            g.Enter(0);
            Check(g.save.room==4 && g.BlackoutActive,
                "Cannot use a travel portal to erase an active civilian evacuation");
            g.PrepareSave();
            var fresh=new GameCore(data,CloneSave(g.save));fresh.Start();
            Check(!fresh.BlackoutActive && fresh.Has("blackout_started") &&
                !fresh.Has("blackout_complete"),
                "SaveData v2 reload never fabricates a completed or half-escorted civilian");
            Point[][] routes={
                new[]{new Point(12.5f,6.5f),new Point(12.5f,16.5f),new Point(shelter.x,shelter.y)},
                new[]{new Point(25.5f,17.5f),new Point(22.5f,16.5f),new Point(15.5f,16.5f),new Point(shelter.x,shelter.y)},
                new[]{new Point(22.5f,5.5f),new Point(15.5f,5.5f),new Point(15.5f,16.5f),new Point(shelter.x,shelter.y)}
            };
            for(int i=1;i<=3;i++)
            {
                WorldObject citizen=Object(g,"blackoutcivilian"+i);
                Check(g.CanStand(new Point(citizen.x,citizen.y)),
                    "All three story civilians must occupy walkable original Citadel map coordinates");
                g.save.x=citizen.x;g.save.y=citizen.y;
                Action(g,citizen.kind,"blackout_pick_"+i);
                Check(g.BlackoutFollowing==i &&
                    !g.IsObjectPresent(citizen) &&
                    g.BlackoutFollowerHealth==48 &&
                    g.BlackoutRescued==((1<<(i-1))-1),
                    "Collecting a unique named civilian creates one vulnerable follower, never instant rescue");
                g.save.x=shelter.x;g.save.y=shelter.y;
                g.Talk(shelter);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="blackout_deliver_"+i)<0 &&
                    g.BlackoutFollowing==i,
                    "Teleporting alone to the shelter cannot forge a civilian handover");
                g.Close();
                // Return to the actual pickup, then WALK through physical
                // waypoints with regular player locomotion and escort AI.
                g.save.x=citizen.x;g.save.y=citizen.y;
                foreach(Point route in routes[i-1])
                    MoveWithBlackoutEscort(g,route);
                Check(g.BlackoutActive && 
                    (g.BlackoutFollowerPosition-g.Position).Length<2.3f,
                    "Real following and movement bring the named civilian home while countdown remains active");
                Action(g,shelter.kind,"blackout_deliver_"+i);
                Check(g.BlackoutFollowing==0 &&
                    g.BlackoutCount==i && g.BlackoutRescued==(1<<i)-1 &&
                    !g.Has("blackout_complete"),
                    "Only a physical shelter handover increments real evacuation roster, never premature victory");
            }
            Check(g.BlackoutRescued==7 && g.BlackoutTime>0,
                "All three rescue routes finish the 180-second playable climax");
            int money=g.save.coins;
            Action(g,shelter.kind,"blackout_complete");
            Check(g.Has("blackout_complete") && !g.BlackoutActive &&
                g.save.coins==money+80 && g.BlackoutCount==0,
                "Only fully registered civilians award the one-time permanent ending milestone");
            g.Talk(shelter);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="blackout_complete")<0,
                "Completed main rescue cannot be replayed for additional rewards");
            g.Close();
        }

        public static void BlackoutCeasefireRequiresActualPublicSafetyAndAlliedSupport(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string f in new[]{"triad_complete","core_1","core_2","core_3"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();
            WorldObject shrine=Object(g,"shrine");
            g.save.x=shrine.x;g.save.y=shrine.y;
            g.Talk(shrine);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="blackout_ceasefire"),
                "Original unrescued campaign cannot claim an unearned public ceasefire");
            g.Close();
            g.save.flags.Add("blackout_complete");
            g.save.flags.Add("witness_complete_1");
            g.save.flags.Add("witness_complete_2");
            g.save.flags.Add("witness_complete_3");
            g.Talk(shrine);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="blackout_ceasefire"),
                "Evacuation and ledger alone cannot stand down Orrin without all living allies");
            g.Close();
            foreach(string f in new[]{"ally_1","ally_2","ally_3"})g.save.flags.Add(f);
            g.Talk(shrine);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="blackout_ceasefire") &&
                Array.Exists(g.conversation.choices,c=>c.action=="boss"),
                "Local peace becomes an actual choice while the original warden boss route remains available");
            g.Close();
            Action(g,"shrine","blackout_ceasefire");
            Check(g.Has("boss_4") && g.Has("warden_stood_down") &&
                g.Has("blackout_peace") && g.save.ending==0 &&
                g.mode=="dialog" && g.conversation.text.Contains("莫芽"),
                "The earned ceasefire reaches the real original ending decision without fake combat or skipping core choice");
            int coins=g.save.coins;
            g.Close();
            g.Talk(shrine);
            Check(g.save.coins==coins,"Peaceful outcome is never repeatable for ordinary rewards");
        }

        public static void BlackoutEmergencyArcsCanHitButAbortAndTimeoutPreserveOtherWorld(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=500;
            WorldObject shelter=Object(g,"blackoutshelter"),
                citizen=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=citizen.x;g.save.y=citizen.y;
            Action(g,citizen.kind,"blackout_pick_1");
            Advance(g,3.6f);
            Check(g.BlackoutActive &&
                g.hazards.Exists(h=>h.blackoutOwned && h.damage>0 && h.timer>0),
                "Blackout spawns actual delayed damage hazards in a live evacuation");
            var normal=new Hazard{position=new Point(6,13),timer=250,kind="root",
                radius=.6f,damage=4};
            var normalFx=new Effect("discover",new Point(6,13),250);
            g.hazards.Add(normal);g.effects.Add(normalFx);
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.Talk(shelter);
            Check(g.conversation==null ||
                Array.FindIndex(g.conversation.choices,c=>c.action=="blackout_deliver_1")<0,
                "Leaving a civilian in danger cannot instantly deliver them from across the map");
            g.Close();
            // Active emergency cannot be canceled remotely through a dialogue
            // action; the real shelter interaction is required.
            g.save.x=citizen.x;g.save.y=citizen.y;
            Advance(g,1.5f);
            Check(g.BlackoutFollowerHealth<48 || 
                g.hazards.Exists(h=>h.blackoutOwned),
                "The civilian is exposed to active delayed arcs unless the player changes position");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.Choose(0); // not in dialogue; must have no side effects
            Check(g.BlackoutActive,"No stray action can invent rescue completion");
            // Explicit abort is available only if not holding a follower:
            // failed timed operation cleans all temporary arc warnings.
            g.invulnerable=500;
            Advance(g,180.1f);
            Check(!g.BlackoutActive && !g.Has("blackout_complete") &&
                g.hazards.Contains(normal) && g.effects.Contains(normalFx) &&
                !g.hazards.Exists(h=>h.blackoutOwned),
                "Timed failure cleans only owned hazards/effects and cannot affect original world objects");
        }
    }
}
