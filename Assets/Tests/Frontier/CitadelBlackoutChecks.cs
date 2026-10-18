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

        public static void BlackoutArcReallyHurtsFollowerAndMovingAwayDodgesIt(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("triad_complete");g.Enter(4);g.enemies.Clear();
            WorldObject shelter=Object(g,"blackoutshelter"),
                lena=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=lena.x;g.save.y=lena.y;
            Action(g,lena.kind,"blackout_pick_1");
            g.invulnerable=100;
            Advance(g,5.15f);
            Check(g.BlackoutActive && g.BlackoutFollowerHealth==34,
                "A real stationary escorted mother loses 14 safety HP when the delayed shield surge detonates");
            int money=g.save.coins;
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.Talk(shelter);
            Check(g.mode=="dialog" &&
                Array.Exists(g.conversation.choices,c=>c.action=="blackout_abort") &&
                !Array.Exists(g.conversation.choices,c=>c.action=="blackout_deliver_1"),
                "Shelter offers an explicit safe abort when civilian is stranded behind, never remote delivery");
            g.Close();
            Action(g,shelter.kind,"blackout_abort");
            Check(!g.BlackoutActive && g.save.coins==money &&
                !g.Has("blackout_complete") &&
                !g.hazards.Exists(h=>h.blackoutOwned),
                "Canceled escort cleans only owned hazards without inventing rescues or losing saved campaign progress");
            Action(g,shelter.kind,"blackout_start");
            g.save.x=lena.x;g.save.y=lena.y;
            Action(g,lena.kind,"blackout_pick_1");
            g.invulnerable=100;
            Advance(g,3.65f);
            Check(g.hazards.Exists(h=>h.blackoutOwned),
                "A second genuine attempt marks a warning the hero can move out of");
            for(int i=0;i<75;i++)
                g.Tick(.02f,new Point(1,0));
            Check(g.BlackoutActive && g.BlackoutFollowerHealth==48 &&
                (g.BlackoutFollowerPosition-new Point(lena.x,lena.y)).Length>1.3f,
                "Moving both hero and real follower away from the marked ground prevents any scripted civilian damage");
        }

        public static void FinishedBlackoutBuildsARealNonCombatShelterAndEpilogue(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("triad_complete");g.Enter(4);g.enemies.Clear();
            WorldObject shelter=Object(g,"blackoutshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.health=40;g.invulnerable=50;
            Advance(g,2);
            Check(g.save.health==40,
                "No healing is provided by an unaudited or incomplete evacuation");
            g.save.flags.Add("blackout_complete");
            Advance(g,2);
            Check(g.save.health>42 && g.save.health<44,
                "Real finished campaign evacuation creates modest local health support");
            g.save.health=40;
            g.save.x=shelter.x+5;g.save.y=shelter.y;
            Advance(g,2);
            Check(g.save.health==40,"Public refuge is local, never a whole-map regeneration boost");
            g.save.x=shelter.x;g.save.y=shelter.y;
            var foe=new Enemy(84444,0,shelter.x+.7f,shelter.y,50);
            foe.stun=100;foe.timer=100;g.enemies.Add(foe);g.save.health=40;
            Advance(g,2);
            Check(g.save.health==40,"Contested public shelter never heals during active combat");
            g.enemies.Clear();
            g.save.ending=2;g.Epilogue();
            Check(g.conversation.text.Contains("莱娜、伊恩和奥姆") &&
                g.conversation.textEn.Contains("Lena, Ian and Orme"),
                "Existing ending dialogue preserves actual names of civilians saved during the blackout");
            g.Close();
            g.save.flags.Add("blackout_peace");
            g.Epilogue();
            Check(g.conversation.text.Contains("主动放下武器") &&
                g.conversation.textEn.Contains("stood down"),
                "Earned civilian ceasefire also changes the original ending's actual historical account");
        }

        public static void BlackoutHandoverRequiresCivilianAtPhysicalShelter(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=300;
            WorldObject shelter=Object(g,"blackoutshelter"),
                citizen=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=citizen.x;g.save.y=citizen.y;
            Action(g,citizen.kind,"blackout_pick_1");
            Check(g.BlackoutFollowing==1&&g.BlackoutFollowerHealth>0,
                "Authentic Citadel pickup creates one living civilian");
            // The player can stand beside the shelter edge while the
            // civilian is still outside. Previously proximity to only
            // the HERO counted as a successful physical handover.
            var posField=typeof(GameCore).GetField("blackoutFollowerPosition",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(posField!=null,"Live escort maintains a genuine follower position");
            g.save.x=shelter.x+1.7f;g.save.y=shelter.y;
            posField.SetValue(g,new Point(shelter.x+3.8f,shelter.y));
            Check((g.BlackoutFollowerPosition-g.Position).Length<2.3f&&
                (g.BlackoutFollowerPosition-new Point(shelter.x,shelter.y)).Length>2.8f,
                "Player and civilian can be close despite civilian being outside the shelter");
            g.Talk(shelter);
            Check(g.conversation!=null&&!Array.Exists(g.conversation.choices,
                c=>c.action=="blackout_deliver_1"),
                "Shelter dialogue refuses a ghost handover outside the civic safety radius");
            g.Close();
            var choose=typeof(GameCore).GetMethod("ChooseBlackout",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Authoritative choice handler exists");
            choose.Invoke(g,new object[]{"blackout_deliver_1"});
            Check(g.BlackoutFollowing==1&&g.BlackoutRescued==0,
                "Direct choice dispatch cannot forge the civilian roster either");
            g.save.x=shelter.x;g.save.y=shelter.y;
            posField.SetValue(g,new Point(shelter.x+.9f,shelter.y));
            g.Talk(shelter);
            Check(g.conversation!=null&&Array.Exists(g.conversation.choices,
                c=>c.action=="blackout_deliver_1"),
                "A living civilian genuinely inside the public lamps may be handed over");
            g.Close();
            choose.Invoke(g,new object[]{"blackout_deliver_1"});
            Check(g.BlackoutFollowing==0&&g.BlackoutRescued==1&&
                !g.Has("blackout_complete"),
                "Only actual first civilian shelter arrival updates the transient rescue roster");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.Has("blackout_started")&&
                !restored.Has("blackout_complete")&&!restored.BlackoutActive,
                "Existing v2 saves still never persist a partially escorted civilian");
        }

        public static void BlackoutCivilianNavigatesAroundARealWallCorner(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=400;
            WorldObject shelter=Object(g,"blackoutshelter"),
                citizen=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=citizen.x;g.save.y=citizen.y;
            Action(g,citizen.kind,"blackout_pick_1");
            Check(g.BlackoutFollowing==1,
                "The actual Citadel citizen enters the normal timed escort");
            MapData map=data.maps[4];int xFound=-1,yFound=-1;
            for(int y=8;y<map.rows.Length-9&&xFound<0;y++)
            for(int x=8;x<map.rows[y].Length-10&&xFound<0;x++)
            {
                Point left=new Point(x+.5f,y+.5f),
                    middle=new Point(x+1.5f,y+.5f),
                    right=new Point(x+2.5f,y+.5f),
                    a=new Point(x+.5f,y-1.5f),b=new Point(x+1.5f,y-1.5f),
                    c=new Point(x+2.5f,y-1.5f);
                if(map.rows[y][x+1]!='.'||
                    !g.CanStand(left)||!g.CanStand(middle)||
                    !g.CanStand(right)||!g.CanStand(a)||
                    !g.CanStand(b)||!g.CanStand(c))continue;
                xFound=x;yFound=y;
            }
            Check(xFound>=0,"The original Citadel offers a walkable alternate corridor");
            var field=typeof(GameCore).GetField("blackoutFollowerPosition",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(field!=null,"The genuine civilian location is maintained at runtime");
            string oldRow=map.rows[yFound];
            try
            {
                char[] blocked=oldRow.ToCharArray();blocked[xFound+1]='#';
                map.rows[yFound]=new string(blocked);
                Point start=new Point(xFound+.5f,yFound+.5f),
                    finish=new Point(xFound+2.5f,yFound+.5f);
                Check(!g.CanStand(new Point(xFound+1.5f,yFound+.5f))&&
                    g.CanStand(start)&&g.CanStand(finish),
                    "An added wall blocks the direct citizen path, not either actor");
                g.save.x=finish.x;g.save.y=finish.y;
                field.SetValue(g,start);
                bool legal=true;
                for(int i=0;i<135&&g.BlackoutActive&&
                    (g.BlackoutFollowerPosition-g.Position).Length>1.4f;i++)
                {
                    g.Tick(.03f,new Point());
                    if(!g.CanStand(g.BlackoutFollowerPosition)){legal=false;break;}
                }
                Check(legal&&g.BlackoutActive&&
                    (g.BlackoutFollowerPosition-g.Position).Length<1.5f,
                    "Civilian follows the path around the wall without snapping, clipping or abandoning the mission");
                Check(g.BlackoutRescued==0&&!g.Has("blackout_complete"),
                    "Following through the alternate corridor never gives a free story completion");
            }
            finally{map.rows[yFound]=oldRow;}
            g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&!reloaded.BlackoutActive,
                "Transient path recovery does not extend old SaveData v2");
        }

        public static void BlackoutQuestCompassRecoversStrandedCivilianBeforeShelter(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=200;
            WorldObject shelter=Object(g,"blackoutshelter"),
                civilian=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=civilian.x;g.save.y=civilian.y;
            Action(g,civilian.kind,"blackout_pick_1");
            Check(g.BlackoutFollowing==1,"The named citizen must be under genuine escort");
            Point position=g.BlackoutFollowerPosition;
            g.save.x=shelter.x;g.save.y=shelter.y;
            Check((g.Position-position).Length>3f,
                "Actual shelter is distant from the picked-up civilian");
            var q=g.GetQuest("blackout");
            Check((q.target-position).Length<.01f&&
                q.next.Contains("回头"),
                "The real journal directs the player back to the stranded civilian");
            g.save.english=true;
            q=g.GetQuest("blackout");
            Check((q.target-position).Length<.01f&&
                q.next.Contains("behind"),
                "English journal and compass use the same physical rescue target");
            g.save.english=false;
            g.save.x=position.x;g.save.y=position.y;
            q=g.GetQuest("blackout");
            Check((q.target-new Point(shelter.x,shelter.y)).Length<.01f&&
                q.next.Contains("避难所"),
                "Once reunited the original authored refuge becomes the live navigation target");
            Check(g.BlackoutRescued==0&&!g.Has("blackout_complete"),
                "Navigation guidance never registers citizens or pays the chapter reward");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&!loaded.BlackoutActive,
                "Temporary rescue compass state does not enter v2 saves");
        }

        public static void BlackoutOneUseCivilianWardAbsorbsActualArcAndRespectsResources(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("triad_complete");
            g.Enter(4);g.enemies.Clear();g.invulnerable=350;
            WorldObject shelter=Object(g,"blackoutshelter"),
                lena=Object(g,"blackoutcivilian1");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            g.save.x=lena.x;g.save.y=lena.y;
            g.save.energy=24;
            g.Talk(lena);
            Check(g.conversation!=null&&
                Array.Exists(g.conversation.choices,c=>c.action=="blackout_ward_1"),
                "The actual unrescued NPC offers an optional defensive pickup");
            g.Close();
            Action(g,lena.kind,"blackout_ward_1");
            Check(g.BlackoutFollowing==0&&g.BlackoutFollowerWard==0&&
                g.save.energy==24&&g.BlackoutActive,
                "Insufficient energy neither picks up the civilian nor grants free shielding");
            g.save.energy=70;
            Action(g,lena.kind,"blackout_ward_1");
            Check(g.BlackoutFollowing==1&&g.BlackoutFollowerWard==1&&
                g.BlackoutFollowerHealth==48&&g.save.energy==45,
                "The real ward-equipped pickup consumes exactly 25 energy once");
            // The rescued follower is intentionally no longer a world
            // interaction target. Probe the authoritative action dispatcher
            // directly rather than pretending the NPC still offers dialogue.
            var choose=typeof(GameCore).GetMethod("ChooseBlackout",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(choose!=null,"Actual blackout action handler exists");
            choose.Invoke(g,new object[]{"blackout_ward_1"});
            Check(g.BlackoutFollowerWard==1&&g.save.energy==45,
                "Direct action replay cannot stack wards or double charge resources");
            g.save.english=true;
            Check(g.GetQuest("blackout").next.Contains("ward ready"),
                "The real English quest HUD announces active one-use protection");
            g.save.english=false;
            Check(g.GetQuest("blackout").next.Contains("防护灯"),
                "The same protection is visible in Chinese quest guidance");
            Advance(g,5.2f);
            Check(g.BlackoutActive&&g.BlackoutFollowerHealth==48&&
                g.BlackoutFollowerWard==0,
                "One actual delayed storm arc consumes the ward instead of hurting the escorted mother");
            Check(!g.GetQuest("blackout").next.Contains("防护灯"),
                "Spent protection no longer advertises itself as available");
            Advance(g,5.5f);
            Check(g.BlackoutActive&&g.BlackoutFollowerHealth==34,
                "A second genuine shield surge damages an unwarded civilian normally");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&!loaded.BlackoutActive&&
                loaded.BlackoutFollowerWard==0&&!loaded.Has("blackout_complete"),
                "Unfinished warded rescues never enter legacy v2 save slots");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_abort");
            Check(!g.BlackoutActive&&g.BlackoutFollowerWard==0,
                "The existing physical abort retires protection without fake rewards");
        }

        public static void BlackoutCannotOverlapLiveCitadelSecurityButStartsAfterWithdrawal(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;
            foreach(string f in new[]{"triad_complete","civic_response_ledger",
                "last_directive_started","last_directive_clue_0",
                "last_directive_clue_1","last_directive_clue_2"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();g.invulnerable=300;
            WorldObject console=Object(g,"lastdirectivecore"),
                shelter=Object(g,"blackoutshelter");
            g.save.x=console.x;g.save.y=console.y;
            Action(g,console.kind,"last_directive_code_0");
            Check(g.LastDirectiveWave==1&&g.LastDirectiveEnemies==2,
                "Actual terminal spawns two live Citadel security defenders");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.Talk(shelter);
            Check(g.conversation!=null&&
                !Array.Exists(g.conversation.choices,
                    c=>c.action=="blackout_start"),
                "Existing public shelter refuses to offer a competing rescue during security combat");
            g.Close();
            var choose=typeof(GameCore).GetMethod("ChooseBlackout",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Blackout uses an authoritative gameplay choice handler");
            choose.Invoke(g,new object[]{"blackout_start"});
            Check(g.LastDirectiveWave==1&&g.LastDirectiveEnemies==2&&
                !g.BlackoutActive&&!g.Has("blackout_started"),
                "Directly forged rescue action cannot hide or reset the live original enemies");
            g.save.x=console.x;g.save.y=console.y;
            Action(g,console.kind,"last_directive_abort");
            Check(g.LastDirectiveWave==0&&g.LastDirectiveEnemies==0,
                "Real security console withdraws its own defenders without losing chapter testimony");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,shelter.kind,"blackout_start");
            Check(g.BlackoutActive&&g.BlackoutTime==180&&
                g.Has("blackout_started"),
                "Authentic shelter resumes full 180-second rescue after prior work explicitly ends");
            g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&reloaded.Has("last_directive_started")&&
                reloaded.Has("blackout_started")&&!reloaded.BlackoutActive,
                "Both recorded story milestones survive old v2 slots without persisting competing live actors");
        }

    }
}
