using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {

        private static void StartCivilianRescueViaRealDialogue(GameCore game, WorldObject station)
        {
            Action(game, station.kind, "fieldstation_rescue_menu");
            Check(game.conversation != null, "Rescue operations open a real submenu");
            int next = -1;
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == "fieldstation_rescue_start")
                    { next = i; break; }
            Check(next >= 0, "The actual submenu exposes the rescue start action");
            game.Choose(next);
        }

        // The live ambush must be discovered on foot at the authored landmark,
        // rather than spawning at the shelter the instant a mission is accepted.
        private static void ReachCivilianRescueMarker(GameCore game, int region)
        {
            Point marker = game.FieldSurveyTarget(region);
            Point[] candidates = { marker, marker + new Point(1, 0),
                marker + new Point(-1, 0), marker + new Point(0, 1),
                marker + new Point(0, -1) };
            bool found = false;
            foreach (Point candidate in candidates)
                if (game.CanStand(candidate) && (candidate - marker).Length < 2.1f)
                {
                    game.save.x = candidate.x; game.save.y = candidate.y;
                    found = true; break;
                }
            Check(found, "A walkable rescue approach exists at the authored regional landmark");
            game.Tick(.05f, new Point());
        }

        private static void ChooseRescueCivilianOption(GameCore g, string action)
        {
            g.Interact();
            Check(g.conversation != null, "E opens the actual nearby survivor conversation");
            int choice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { choice = i; break; }
            Check(choice >= 0, "The survivor offers the requested player command: " + action);
            g.Choose(choice);
        }
        public static void InnReliefMenuAlwaysReturnsToTheOriginalChartActor(Campaign data)
        {
            var g=Fresh(data);
            g.save.flags.Add("farshore_relief_complete");
            g.Enter(9);
            WorldObject desk=Object(g,"surveyDesk");
            g.save.x=desk.x+4;g.save.y=desk.y;
            g.Talk(desk);
            Check(g.mode=="dialog"&&
                StoryChoicePresent(g,"farshore_relief_routes"),
                "Legacy remote chart reading still exposes the familiar text without authorizing any operation");
            g.Choose(StoryChoiceIndex(g,"farshore_relief_routes"));
            Check(g.mode=="world"&&g.conversation==null,
                "A physically distant chart cannot commit a protected submenu action");
            g.Close();
            g.save.x=desk.x;g.save.y=desk.y;
            g.Talk(desk);
            Check(StoryChoicePresent(g,"farshore_relief_routes"),
                "The genuine Inn chart offers its public refuge route submenu");
            g.Choose(StoryChoiceIndex(g,"farshore_relief_routes"));
            Check(StoryChoicePresent(g,"farshore_relief_return")&&
                g.conversationSource==desk,
                "Nested relief menu remembers the actual chart as its origin");
            g.Choose(StoryChoiceIndex(g,"farshore_relief_return"));
            Check(g.mode=="dialog"&&g.conversationSource==desk &&
                StoryChoicePresent(g,"farshore_relief_routes") &&
                g.conversation.speaker==desk.name,
                "Returning from the route submenu opens the same desk, not a different actor");
            g.Close();
            g.Talk(desk);
            g.Choose(StoryChoiceIndex(g,"farshore_relief_routes"));
            g.save.x=desk.x+4;g.save.y=desk.y;
            g.Choose(StoryChoiceIndex(g,"farshore_relief_return"));
            Check(g.mode=="world"&&g.conversation==null,
                "Out-of-range stale back choice cannot resurrect another dialogue");
            g.save.x=desk.x;g.save.y=desk.y;g.Talk(desk);
            Check(g.mode=="dialog"&&g.conversation.speaker==desk.name,
                "The true chart dialogue still works normally after the stale click");
        }

        public static void FarshoreStationChoicesRequireCurrentPhysicalReach(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=10;
            foreach(string flag in new[]{"exp_intro","harbor_report","desert_report",
                "fieldstation_6","fieldstation_survey_6","farshore_relief_6"})
                g.save.flags.Add(flag);
            g.save.frontierEnding=2;g.Enter(6);g.enemies.Clear();
            WorldObject station=Object(g,"fieldstation6");
            Point outside=StandableOutsidePhysicalRadius(g,station,2.2001f);
            g.save.x=outside.x;g.save.y=outside.y;
            g.Talk(station);
            Check(g.mode!="dialog",
                "A direct Talk cannot open a field station outside physical reach");
            g.Close();
            g.save.x=station.x;g.save.y=station.y;
            g.Talk(station);
            int staleMenu=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_rescue_menu");
            Check(staleMenu>=0,"A reachable refuge post offers the rescue submenu");
            g.save.x=outside.x;g.save.y=outside.y;
            g.Choose(staleMenu);g.Close();
            Check(g.reliefMissionRoom==0&&!g.ReliefEscortActive,
                "A stale rescue-menu choice cannot open a remote sortie");

            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"fieldstation_rescue_menu");
            int staleStart=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_rescue_start");
            Check(staleStart>=0,"The station submenu offers the real rescue start");
            g.save.x=outside.x;g.save.y=outside.y;
            g.Choose(staleStart);g.Close();
            Check(g.reliefMissionRoom==0,
                "A stale rescue-start choice cannot create a timed mission remotely");
            g.save.x=station.x;g.save.y=station.y;
            StartCivilianRescueViaRealDialogue(g,station);
            Check(g.reliefMissionRoom==6,
                "The physically reachable station starts the actual rescue");

            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"fieldstation_rescue_menu");
            int staleCancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_rescue_cancel");
            Check(staleCancel>=0,"The active sortie offers a real cancel action");
            g.save.x=outside.x;g.save.y=outside.y;
            g.Choose(staleCancel);g.Close();
            Check(g.reliefMissionRoom==6,
                "A stale cancel choice cannot erase an active civilian rescue remotely");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"fieldstation_rescue_menu");
            int cancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_rescue_cancel");
            Check(cancel>=0,"Returning to the station restores the explicit cancel action");
            g.Choose(cancel);
            Check(g.reliefMissionRoom==0,
                "A nearby station can explicitly cancel the rescue without completing it");

            g.save.flags.Remove("fieldstation_survey_6");
            g.save.x=station.x;g.save.y=station.y;
            g.Talk(station);
            int staleSurveyStart=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_survey_start");
            Check(staleSurveyStart>=0,"The restored station offers a genuine field-survey start");
            g.save.x=outside.x;g.save.y=outside.y;
            g.Choose(staleSurveyStart);g.Close();
            Check(g.fieldSurveyRoom==0,
                "A stale survey-start choice cannot begin an out-and-back timer remotely");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"fieldstation_survey_start");
            Check(g.fieldSurveyRoom==6,"The station starts the real timed field survey nearby");
            g.Talk(station);
            int staleSurveyCancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="fieldstation_survey_cancel");
            Check(staleSurveyCancel>=0,"An active field survey offers an explicit cancel action");
            g.save.x=outside.x;g.save.y=outside.y;
            g.Choose(staleSurveyCancel);g.Close();
            Check(g.fieldSurveyRoom==6,
                "A stale survey-cancel choice cannot erase a live field reading remotely");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"fieldstation_survey_cancel");
            Check(g.fieldSurveyRoom==0,
                "A nearby station can explicitly cancel the active field survey");
        }

        public static void CivilianRescueCannotOverlapUnrelatedFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"exp_intro","harbor_report","desert_report",
                "fieldstation_6","fieldstation_survey_6","farshore_relief_6"})
                g.save.flags.Add(flag);
            g.save.frontierEnding=2;g.Enter(6);g.enemies.Clear();
            WorldObject station=Object(g,"fieldstation6");
            g.save.x=station.x;g.save.y=station.y;
            SetOuterPatrolLull(g,6);
            Check(g.OuterPatrolActive&&g.SafeTravel,
                "The station is locally safe while another patrol remains active");
            StartCivilianRescueViaRealDialogue(g,station);
            Check(g.reliefMissionRoom==0&&!g.ReliefEscortActive,
                "Civilian evacuation cannot start or spawn a follower during unrelated fieldwork");
            ClearOuterPatrolLull(g);
            StartCivilianRescueViaRealDialogue(g,station);
            Check(g.reliefMissionRoom==6,
                "The same physically reachable station can start evacuation once fieldwork ends");
        }

        public static void CivicResponseCannotOverlapUnrelatedFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;g.save.frontierEnding=1;
            foreach(string flag in new[]{"civic_supply_complete",
                "civic_supply_clinic_0","far_shelter_0"})
                g.save.flags.Add(flag);
            g.Enter(0);g.enemies.Clear();
            WorldObject shelter=Object(g,"farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            SetOuterPatrolLull(g,0);
            Check(g.OuterPatrolActive&&g.SafeTravel,
                "The clinic is locally safe while an unrelated patrol owns the fieldwork slot");
            Action(g,shelter.kind,"civic_response_start");
            Check(!g.CivicResponseActive&&
                !g.enemies.Exists(e=>e.id>=90200&&e.id<90216),
                "Civic medical service cannot start or spawn a second activity during fieldwork");
            ClearOuterPatrolLull(g);
            Action(g,shelter.kind,"civic_response_start");
            Check(g.CivicResponseActive&&g.CivicResponseClinicRun&&
                g.CivicResponseTimeLeft>74,
                "The real 75-second civilian delivery remains available once fieldwork ends");
            g.StartAbyss(0);
            Check(g.save.room==0&&g.CivicResponseActive&&g.save.abyssFloor==0,
                "The direct abyss start cannot bypass a protected civilian-service travel lock");
        }

        public static void RescueCivilianWaitFollowAndOneUseAidArePlayable(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive, "Two real waves lead to the interactive survivor escort");
            Point civilian = g.reliefFollowerPosition;
            g.save.x = civilian.x; g.save.y = civilian.y;
            ChooseRescueCivilianOption(g, "relief_wait");
            Check(g.reliefFollowerWaiting, "The rescued civilian accepts an actual wait command");
            Point waitingPoint = g.reliefFollowerPosition;
            g.save.x = station.x;
            Check((g.GetQuest("relief_rescue").target - waitingPoint).Length < .001f &&
                g.GetQuest("relief_rescue").next.Contains("按E"),
                "Quest guidance points back to the actual waiting civilian rather than the empty home station");
             g.save.y = station.y;
            for (int i = 0; i < 5; i++) g.Tick(.05f, new Point());
            Check((civilian - g.reliefFollowerPosition).Length < .001f &&
                !g.reliefFollowerMoving && g.ReliefEscortActive,
                "Waiting stops physical movement without aborting the rescue");
            g.save.x = civilian.x; g.save.y = civilian.y;
            g.reliefFollowerSafety = 50;
            int herbs = g.save.inventory[2];
            Check(herbs >= 1, "The test profile starts with at least one real herb");
            ChooseRescueCivilianOption(g, "relief_treat");
            Check(g.reliefMedicalUsed && g.save.inventory[2] == herbs - 1 &&
                g.reliefFollowerSafety == 75,
                "One genuine herb treats civilian safety by 25 and records a per-sortie use");
            g.Interact();
            Check(!Array.Exists(g.conversation.choices, c => c.action == "relief_treat"),
                "A civilian cannot be healed repeatedly to farm safety in one rescue");
            g.Close();
            ChooseRescueCivilianOption(g, "relief_follow");
            Check(!g.reliefFollowerWaiting, "Player can resume a paused escort using the same E interaction");
            g.save.x = station.x; g.save.y = station.y;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerMoving && g.reliefFollowerWalkDistance > 0,
                "Resuming follow restores real distance-driven movement");
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.reliefFollowerWaiting && !g.reliefMedicalUsed,
                "Failed rescues remove temporary wait and medical status, preserving old save schema");
        }

        public static void RescuedSurvivorsUseActualDistanceDrivenMovement(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive && g.reliefFollowerWalkDistance == 0,
                "A newly freed civilian is idle with a zeroed walk cycle");
            Point start = g.reliefFollowerPosition;
            g.save.x = start.x - 4; g.save.y = start.y;
            g.Tick(.05f, new Point());
            float first = (g.reliefFollowerPosition - start).Length;
            Check(g.reliefFollowerMoving && first > .001f &&
                Math.Abs(g.reliefFollowerWalkDistance - first) < .0002f &&
                g.reliefFollowerFacingLeft,
                "Actual leftward displacement, not the movement intent, drives the gait and facing");
            g.save.x = g.reliefFollowerPosition.x; g.save.y = g.reliefFollowerPosition.y;
            float beforeIdle = g.reliefFollowerWalkDistance;
            g.Tick(.05f, new Point());
            Check(!g.reliefFollowerMoving &&
                Math.Abs(g.reliefFollowerWalkDistance - beforeIdle) < .0001f,
                "Standing still freezes the sprite's walking cadence");
            g.save.x = g.reliefFollowerPosition.x + 4; g.save.y = g.reliefFollowerPosition.y;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerMoving && !g.reliefFollowerFacingLeft &&
                g.reliefFollowerWalkDistance > beforeIdle,
                "Walking right mirrors the same canonical NPC art and accumulates real traveled distance");
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.reliefFollowerMoving &&
                g.reliefFollowerWalkDistance == 0,
                "Abandoning a timed escort removes all ephemeral animation state");
        }

        public static void EvacueesUseRealClimateSheltersAndShade(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "mountain_report", "fieldstation_5", "fieldstation_6",
                "fieldstation_7", "fieldstation_complete", "fieldstation_survey_6",
                "fieldstation_survey_7", "fieldstation_survey_5", "farshore_relief_5",
                "farshore_relief_6", "farshore_relief_7" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 1;
            for (int region = 6; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject post = Object(g, "fieldstation" + region);
                g.save.x = post.x; g.save.y = post.y;
                StartCivilianRescueViaRealDialogue(g, post);
                ReachCivilianRescueMarker(g, region);
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(g.ReliefEscortActive, "A real mission reaches the environmental escort phase");
                // Tile glyphs alone do not establish valid humanoid footing.
                // Locate genuine exposed ground under current live collision.
                Point exposed = new Point(); bool found = false;
                for (int y = 3; y < g.Map.rows.Length - 3 && !found; y++)
                for (int x = 3; x < g.Map.rows[y].Length - 3 && !found; x++)
                {
                    Point candidate = new Point(x + .5f, y + .5f);
                    if (!g.CanStand(candidate) ||
                        (candidate - new Point(post.x, post.y)).Length < 8.0f) continue;
                    bool shadeNearby = false;
                    foreach (WorldObject o in g.Map.objects)
                        if (region == 6 && o.kind == "shade" &&
                            (candidate - new Point(o.x, o.y)).Length < 4.0f)
                            shadeNearby = true;
                    if (shadeNearby) continue;
                    exposed = candidate; found = true;
                }
                Check(found, "Actual regional collision contains exposed ground outside shelter and shade");
                g.reliefFollowerPosition = exposed; g.reliefFollowerSafety = 80;
                g.save.x = post.x; g.save.y = post.y;
                g.Tick(.05f, new Point());
                Check(g.reliefFollowerSafety < 80 && g.ReliefEscortActive,
                    "Real weather reduces a civilian's protection while outside restored refuges");
                g.reliefFollowerPosition = new Point(post.x, post.y);
                g.save.x = exposed.x; g.save.y = exposed.y;
                g.reliefFollowerSafety = 80;
                g.Tick(.05f, new Point());
                Check(g.reliefFollowerSafety > 80 && g.ReliefEscortActive,
                    "Returning a survivor to a rebuilt field station actively heals civilian safety");
                if (region == 6)
                {
                    WorldObject shade = Object(g, "shade");
                    g.reliefFollowerPosition = new Point(shade.x, shade.y);
                    g.reliefFollowerSafety = 80;
                    g.Tick(.05f, new Point());
                    Check(g.reliefFollowerSafety > 80,
                        "Original desert shade remains meaningful to the rescue, not just the hero");
                }
                // Finish this trial before moving to another real region.
                // Active civilian evacuations cannot disappear through portals.
                g.reliefMissionTime = .01f;
                g.Tick(.05f, new Point());
                Check(g.reliefMissionRoom == 0,
                    "Regional weather trial exits through the normal rescue timeout");
            }
        }

        public static void RescueCivilianAppearsOnSiteAndEncounterEffectsRetire(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10; g.save.stage = 3;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            Check(!g.ReliefCivilianVisible && g.reliefMissionWave == 0,
                "A stranded civilian is not present until the hero actually finds the site");
            ReachCivilianRescueMarker(g, 6);
            Check(g.ReliefCivilianVisible && g.CanStand(g.reliefFollowerPosition),
                "Finding the survivor creates a physically placed world actor during the ambush");
            Enemy attacker = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 &&
                e.kind == 0);
            Check(attacker != null, "The real combat wave includes a melee raider");
            g.enemies.RemoveAll(e => e != attacker);
            Point civilian = g.reliefFollowerPosition;
            attacker.x = civilian.x - 1.3f; attacker.y = civilian.y;
            attacker.Face(civilian - attacker.Position);
            attacker.target = civilian; attacker.targetingReliefCivilian = true;
            attacker.state = 1; attacker.timer = .01f; attacker.releaseTime = 0;
            g.save.x = station.x; g.save.y = station.y;
            g.Tick(.05f, new Point());
            Check(g.reliefCivilianHitTime > .1f && g.ReliefCivilianVisible &&
                g.effects.Exists(e => e.reliefMissionOwned &&
                    e.kind == "number"),
                "Real released contact flashes the actual civilian actor and produces owned safety damage feedback");
            var ordinary = new Effect("discover", new Point(station.x, station.y), 3);
            g.effects.Add(ordinary);
            g.effects.Add(new Effect("danger", civilian, 3) {
                radius = 1, reliefMissionOwned = true });
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.ReliefCivilianVisible &&
                g.reliefCivilianHitTime == 0 &&
                !g.effects.Exists(e => e.reliefMissionOwned) &&
                g.effects.Contains(ordinary),
                "Timeout removes special civilians and every encounter-owned warning, but preserves unrelated world effects");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            Check(g.reliefMissionRoom == 6 && g.reliefMissionWave == 0 &&
                !g.ReliefCivilianVisible,
                "Free rescue retry starts from clean staging with no ghost sprite or obsolete warnings");
        }

        public static void CivilianRescueArcherUsesReadableRealStandoff(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1, "Real mission triggers combat at the authored survey site");
            Enemy archer = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 &&
                e.kind == 1);
            Check(archer != null, "First wave must include a real combat archer");
            g.enemies.RemoveAll(e => e != archer);
            Point marker = g.reliefFollowerPosition;
            // Test the genuine spawn chosen by production pathfinding, not an
            // assumed empty tile at an arbitrary offset from the survey prop.
            g.save.x = station.x; g.save.y = station.y;
            archer.timer = 100; archer.state = 0;
            archer.stun = archer.releaseTime = archer.hitTime = 0;
            float initialDistance = (archer.Position - marker).Length;
            for (int i = 0; i < 80; i++)
                g.Tick(.05f, new Point());
            float finalDistance = (archer.Position - marker).Length;
            Check(finalDistance > 3.15f,
                "A live archer finishes at a firing distance (initial " +
                    initialDistance.ToString("0.00") + " final " +
                    finalDistance.ToString("0.00") + ")");
            archer.timer = .01f; archer.state = 0; archer.releaseTime = 0;
            g.effects.Clear();
            for (int i = 0; i < 100 && archer.state != 1; i++)
                g.Tick(.05f, new Point());
            Check(archer.state == 1 && archer.targetingReliefCivilian &&
                (archer.target - marker).Length < .001f &&
                (archer.facing - (marker - archer.Position).Normalized).Length < .1f,
                "Rescue archer locks the true civilian position with the combat facing during windup");
            Check(!g.effects.Exists(e => e.kind == "danger" && e.radius > 1),
                "Archer preparation never fakes a melee-area impact warning");
            float safety = g.reliefFollowerSafety;
            archer.timer = .01f;
            g.Tick(.05f, new Point());
            Check(g.projectiles.Exists(p => p.reliefMissionOwned && !p.friendly &&
                p.visual == "enemyArrow") && g.reliefFollowerSafety > safety - 1,
                "The archer release uses a real projectile; civilian health is not instantly hit");
        }

        public static void RescueThreatDifficultyAndOneShotWarnings(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            Enemy raider = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 && e.kind == 0);
            Check(raider != null, "A real raider is present to pressure the rescue across difficulties");
            g.enemies.RemoveAll(e => e != raider);
            Point marker = g.reliefFollowerPosition;
            raider.x = marker.x; raider.y = marker.y;
            raider.timer = 100; raider.state = 0;
            g.save.x = post.x; g.save.y = post.y;
            float[] loss = new float[3];
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                g.save.difficulty = difficulty;
                g.reliefFollowerSafety = 60;
                g.Tick(.05f, new Point());
                loss[difficulty] = 60 - g.reliefFollowerSafety;
            }
            Check(loss[0] > 0 && loss[0] < loss[1] && loss[1] < loss[2] &&
                Math.Abs(g.ReliefCombatThreatScale - 1.3f) < .001f,
                "A live raider inflicts increasing safety pressure from Easy to Normal to Hard");
            g.save.difficulty = 1;
            g.reliefFollowerSafety = 60;
            g.reliefMissionTime = 30.04f;
            g.Tick(.05f, new Point());
            Check(g.toast.Contains("倒计时") && g.reliefMissionTime < 30,
                "Entering the last 30 seconds produces a single clear bilingual urgency warning");
            string warning = g.toast;
            g.Tick(.05f, new Point());
            Check(g.toast == warning, "The urgency warning is not repeated on every frame");
            g.reliefFollowerSafety = 29;
            g.Tick(.05f, new Point());
            Check(g.toast.Contains("安全度危险"),
                "The first actual low-safety state warns players before the rescue silently fails");
            string dangerWarning = g.toast;
            g.Tick(.05f, new Point());
            Check(g.toast == dangerWarning,
                "Safety warnings appear once per rescue, without covering every attack with toast spam");
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 &&
                !g.Has("relief_rescue_6"),
                "A timed-out rescue cleans transient warnings and does not grant completion");
            StartCivilianRescueViaRealDialogue(g, post);
            Check(g.reliefMissionRoom == 6 && g.reliefMissionWave == 0 &&
                g.reliefMissionTime == 120,
                "Retry begins fresh without retaining prior difficulty warnings or partial progress");
        }

        public static void RescueArrowsUseActualFlightAndCanBeBodyBlocked(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1, "The rescue archer is tested in a live regional ambush");
            Point marker = g.reliefFollowerPosition;
            Enemy archer = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 && e.kind == 1);
            Check(archer != null, "Wave one includes a real bow raider");
            g.enemies.RemoveAll(e => e != archer);
            Point bow = new Point(marker.x - 2.5f, marker.y);
            Check(g.CanStand(bow), "Physical shot test has a valid desert bow lane");
            archer.x = bow.x; archer.y = bow.y;
            archer.target = marker; archer.Face(marker - bow);
            archer.state = 1; archer.timer = .01f; archer.releaseTime = 0;
            archer.targetingReliefCivilian = true;
            float initial = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(g.projectiles.Exists(p => p.reliefMissionOwned && !p.friendly &&
                p.visual == "enemyArrow") && g.reliefFollowerSafety > initial - 1,
                "A committed bow release creates a real hostile arrow, never hitscans the civilian");
            archer.timer = 100; archer.state = 0;
            for (int i = 0; i < 20 && g.projectiles.Count > 0; i++)
                g.Tick(.05f, new Point());
            float arrowLoss = initial - g.reliefFollowerSafety;
            Check(arrowLoss > 8.5f && arrowLoss < 11 &&
                !g.projectiles.Exists(p => p.reliefMissionOwned),
                "An unobstructed hostile arrow must physically reach civilian ground for one nine-point hit");
            // Place hero between the same bow lane and the civilian.
            g.save.x = marker.x - 1; g.save.y = marker.y;
            g.projectiles.Add(new Projectile {
                position = bow, velocity = new Point(5, 0), visual = "enemyArrow",
                source = "shot", damage = 10, reliefMissionOwned = true, life = 1
            });
            float beforeBlock = g.reliefFollowerSafety;
            for (int i = 0; i < 20 && g.projectiles.Count > 0; i++)
                g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety > beforeBlock - 1 &&
                g.projectiles.Count == 0,
                "The hero physically intercepts the arrow before it can reach the civilian");
            // Even a visually identical unrelated arrow cannot modify rescue
            // safety without an encounter ownership tag.
            g.save.x = post.x; g.save.y = post.y;
            g.projectiles.Add(new Projectile {
                position = bow, velocity = new Point(5, 0), visual = "enemyArrow",
                source = "shot", damage = 10, reliefMissionOwned = false, life = 1
            });
            float beforeOrdinary = g.reliefFollowerSafety;
            for (int i = 0; i < 20 && g.projectiles.Count > 0; i++)
                g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety > beforeOrdinary - 1,
                "Ordinary world projectiles cannot accidentally drain civilian rescue safety");
        }

        public static void FieldRallyInteractionTargetsRealCivilianFoot(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1 && g.ReliefFieldGuardAvailable,
                "The live field rescue enters the genuine first combat wave");
            Point rawMarker = g.FieldSurveyTarget(6);
            Point actual = new Point(); bool found = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !found; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !found; x++)
            {
                Point possible = new Point(x + .5f, y + .5f);
                if (!g.CanStand(possible) ||
                    (possible - rawMarker).Length <= 2.3f ||
                    (possible - rawMarker).Length > 4.5f) continue;
                actual = possible; found = true;
            }
            Check(found, "Authored desert map has collision-valid civilian footing away from the prop marker");
            // The marker is scenery; interaction must follow the same
            // physical civilian position as arrow and melee damage.
            g.reliefFollowerPosition = actual;
            g.save.x = actual.x; g.save.y = actual.y;
            g.Interact();
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices, c => c.action == "relief_field_guard"),
                "E addresses the visible civilian even when the map marker is not within interaction range");
            int choice = Array.FindIndex(g.conversation.choices,
                c => c.action == "relief_field_guard");
            float previousEnergy = g.save.energy;
            g.Choose(choice);
            Check(g.reliefWardTime > 7.5f && g.save.energy == previousEnergy - 20,
                "The real civilian receives a tactical guard through world interaction");
        }

        public static void EmergencyCivilianRallyUsesLiveEnergyAndOncePerWave(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1 && g.reliefWardTime <= 0,
                "After real field discovery the first combat wave has no free protective buff");
            float originalEnergy = g.save.energy;
            Point marker = g.reliefFollowerPosition;
            g.save.x = marker.x; g.save.y = marker.y;
            g.Interact();
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "relief_field_guard"),
                "Standing with civilians offers a real tactical rally through E input");
            int choice = Array.FindIndex(g.conversation.choices,
                c => c.action == "relief_field_guard");
            g.Choose(choice);
            Check(g.reliefWardTime > 7.5f &&
                g.save.energy == originalEnergy - 20 &&
                !g.ReliefFieldGuardAvailable,
                "Rally activates once for eight seconds and consumes exactly twenty real energy");
            g.Interact();
            Check(!Array.Exists(g.conversation.choices,
                c => c.action == "relief_field_guard"),
                "The same wave cannot repeatedly spend energy to keep a permanent rally");
            g.Close();
            Enemy attacker = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 && e.kind == 0);
            Check(attacker != null, "A real melee enemy remains to test guarded impact");
            g.enemies.RemoveAll(e => e != attacker);
            attacker.x = marker.x - 1.3f; attacker.y = marker.y;
            attacker.Face(marker - attacker.Position);
            attacker.target = marker; attacker.targetingReliefCivilian = true;
            attacker.state = 1; attacker.timer = .01f;
            g.save.x = post.x; g.save.y = post.y;
            float before = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            float guardedLoss = before - g.reliefFollowerSafety;
            Check(guardedLoss > 7 && guardedLoss < 9,
                "Actual committed melee contact is reduced, not erased, by a civilian field rally");
            attacker.releaseTime = 0; attacker.stun = attacker.hitTime = 0;
            attacker.state = 1; attacker.timer = .01f;
            attacker.targetingReliefCivilian = true; attacker.target = marker;
            g.reliefWardTime = 0;
            float beforeExposed = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            float exposedLoss = beforeExposed - g.reliefFollowerSafety;
            Check(exposedLoss > 13.5f && exposedLoss > guardedLoss,
                "When the short rally expires the same actual melee contact again fully damages civilians");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.reliefMissionWave == 2 && g.ReliefFieldGuardAvailable,
                "The next real wave refreshes the tactical rally once");
            g.reliefMissionTime = .01f; g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && g.reliefWardTime == 0,
                "Mission failure removes combat rally state from old-schema saves");
        }

        public static void CivilianDefenseStrikesMatchReleaseAndBodyguardPosition(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1, "True field discovery activates raider wave one");
            Enemy attacker = g.enemies.Find(e => e.id >= 85000 && e.id < 85100 && e.kind == 0);
            Check(attacker != null, "A real melee raider can attack the civilian");
            g.enemies.RemoveAll(e => e != attacker);
            Point civilian = g.reliefFollowerPosition;
            Point origin = new Point(civilian.x - 1.3f, civilian.y);
            Point guard = new Point(civilian.x - .6f, civilian.y);
            Check(g.CanStand(origin) && g.CanStand(guard),
                "Rescue marker offers physical raider and bodyguard lanes in the authored desert");
            attacker.x = origin.x; attacker.y = origin.y;
            attacker.target = civilian; attacker.Face(civilian - origin);
            attacker.state = 1; attacker.timer = .6f; attacker.targetingReliefCivilian = true;
            g.save.x = station.x; g.save.y = station.y;
            float initial = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety > initial - 1 &&
                attacker.state == 1 && attacker.targetingReliefCivilian,
                "Telegraphed windup does not apply the full civilian hit before contact");
            attacker.timer = .01f;
            g.Tick(.05f, new Point());
            float exposedDamage = initial - g.reliefFollowerSafety;
            Check(exposedDamage > 13.5f && exposedDamage < 15,
                "A real enemy contact releases roughly 14 safety damage once, not every frame");
            float afterStrike = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety > afterStrike - 1,
                "Recovery frames cannot repeat the same committed strike");
            // Reset combat windup, then place the hero inside the real attack
            // lane. Only physical interception may prevent the safety loss.
            attacker.x = origin.x; attacker.y = origin.y;
            attacker.target = civilian; attacker.Face(civilian - origin);
            attacker.state = 1; attacker.timer = .01f; attacker.targetingReliefCivilian = true;
            attacker.releaseTime = 0; attacker.stun = attacker.hitTime = 0;
            g.save.x = guard.x; g.save.y = guard.y;
            float beforeGuard = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(attacker.state == 0 && !attacker.targetingReliefCivilian,
                "A frontline interception resolves a committed attack once, even during player hit invulnerability");
            Check(g.reliefFollowerSafety > beforeGuard - 1,
                "A hero standing between raider and civilian intercepts the actual strike");
            // A real hero interception can hit the hero and create one
            // short hit-stop tick. Finish it before scheduling the next
            // distinct enemy release; test the actual gameplay timeline.
            g.Tick(.05f, new Point());
            // The same general proximity is insufficient from off the attack lane.
            attacker.x = origin.x; attacker.y = origin.y;
            attacker.target = civilian; attacker.Face(civilian - origin);
            attacker.state = 1; attacker.timer = .01f; attacker.targetingReliefCivilian = true;
            attacker.releaseTime = 0; attacker.stun = attacker.hitTime = 0;
            Point side = new Point(civilian.x, civilian.y + 1.1f);
            Check(g.CanStand(side), "The test has walkable ground outside the line of impact");
            g.save.x = side.x; g.save.y = side.y;
            float beforeSide = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < beforeSide - 13.5f,
                "Standing near, but outside, the attack lane cannot negate a civilian hit");
        }

        public static void RescueRaiderAIActuallyProtectsCivilians(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6", "farshore_relief_6" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1 && g.reliefFollowerSafety == 100,
                "Reaching the actual shelter marker activates the real first raider wave");
            Enemy raider = g.enemies.Find(e => e.id >= 85000 && e.id < 85100);
            Check(raider != null, "There is a live raider to threaten civilians");
            Point markerFoot = g.Position;
            raider.x = markerFoot.x; raider.y = markerFoot.y;
            raider.timer = 10; raider.state = 0;
            g.save.x = post.x; g.save.y = post.y;
            float before = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < before &&
                g.Conditions().Contains("安全 "),
                "Raider proximity damages civilian health and visibly reports safety in the actual HUD");
            float exposedLoss = before - g.reliefFollowerSafety;
            g.save.x = markerFoot.x; g.save.y = markerFoot.y;
            raider.timer = 10; raider.state = 0;
            float protectedBefore = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            float protectedLoss = protectedBefore - g.reliefFollowerSafety;
            Check(protectedLoss > 0 && protectedLoss < exposedLoss,
                "The hero standing at the civilians mitigates actual raider pressure");
            g.reliefFollowerSafety = 50;
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.reliefMissionWave == 2 &&
                g.reliefFollowerSafety < 70 && g.reliefFollowerSafety > 50,
                "Clearing wave one restores some civilian safety without fully resetting damage");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive && g.reliefFollowerSafety < 90 &&
                g.reliefFollowerSafety > 50,
                "Battle wounds survive into the real civilian follow-and-return leg");
        }

        public static void ReliefSurvivorsPhysicallyCompleteAllRegionalReturnRoutes(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "mountain_report", "fieldstation_5", "fieldstation_6",
                "fieldstation_7", "fieldstation_complete", "fieldstation_survey_5",
                "fieldstation_survey_6", "fieldstation_survey_7", "farshore_relief_5",
                "farshore_relief_6", "farshore_relief_7", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 3;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                g.save.totalTime = 0;
                WorldObject post = Object(g, "fieldstation" + region);
                g.save.x = post.x; g.save.y = post.y;
                StartCivilianRescueViaRealDialogue(g, post);
                Check(g.reliefMissionWave == 0, "Regional escort begins with an actual outbound journey");
                ReachCivilianRescueMarker(g, region);
                Check(g.reliefMissionWave == 1, "The field site starts a real ambush before survivors travel");
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(g.ReliefEscortActive && g.CanStand(g.reliefFollowerPosition),
                    "The rescued civilian is created at a physically walkable actor foot");
                Point initial = g.reliefFollowerPosition;
                Check(g.reliefFollowerWalkDistance == 0 && !g.reliefFollowerMoving,
                    "Escort starts still, not with an inherited gait from an earlier region");
                g.save.x = post.x; g.save.y = post.y;
                float elapsed = 0;
                while (g.reliefMissionRoom == region && elapsed < 75)
                {
                    g.Tick(.05f, new Point());
                    elapsed += .05f;
                    if (g.ReliefEscortActive)
                        Check(g.CanStand(g.reliefFollowerPosition),
                            "The civilian follows only valid ground, even as regional collision changes");
                }
                Check(g.Has("relief_rescue_" + region) && g.reliefMissionRoom == 0 &&
                    g.reliefFollowerWalkDistance == 0 && !g.reliefFollowerMoving,
                    "A successful escort clears its transient gait instead of leaking to another map");
                Check(g.Has("relief_rescue_" + region) && g.reliefMissionRoom == 0,
                    "A real survivor walks from the regional landmark to the station within 75s (room " +
                        region + "; elapsed " + elapsed.ToString("0.0") + "; from " +
                        initial.x.ToString("0.0") + "," + initial.y.ToString("0.0") +
                        "; to " + g.reliefFollowerPosition.x.ToString("0.0") + "," +
                        g.reliefFollowerPosition.y.ToString("0.0") + ")");
            }
        }

        public static void ActiveCivilianRescuesCannotBeLostThroughPortals(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            Check(g.reliefMissionRoom == 6 && g.reliefMissionWave == 0,
                "The rescue can be started through the actual station conversation");
            g.Enter(0);
            Check(g.save.room == 6 && g.reliefMissionRoom == 6 &&
                g.reliefMissionWave == 0,
                "Portals and scripted Enter calls cannot silently erase an outbound rescue");
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive, "Both original ambush waves lead to active escort");
            Point original = g.reliefFollowerPosition;
            g.Enter(5);
            g.ReturnToTown();
            Check(g.save.room == 6 && g.ReliefEscortActive &&
                (g.reliefFollowerPosition - original).Length < .001f,
                "All travel entry points preserve an active escort and the real survivor position");
            g.PrepareSave();
            Check(g.save.rescueCheckpoint != null,
                "Blocked travel preserves the active mission's valid save checkpoint");
            g.save.x = post.x; g.save.y = post.y;
            Action(g, post.kind, "fieldstation_rescue_menu");
            Check(g.conversation != null,
                "The actual rescue station opens its active-sortie submenu");
            int cancel = Array.FindIndex(g.conversation.choices,
                choice => choice.action == "fieldstation_rescue_cancel");
            Check(cancel >= 0, "An active sortie offers an explicit cancel choice");
            g.Choose(cancel);
            Check(g.reliefMissionRoom == 0,
                "Explicit cancellation at the constructed station remains available");
            g.Enter(0);
            Check(g.save.room == 0 && g.save.rescueCheckpoint == null,
                "A completed cancellation restores normal portal travel without stale checkpoints");
        }

        public static void MistportEscortCheckpointRecoversOnlyFloodedFooting(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "fieldstation_5", "fieldstation_survey_5", "farshore_relief_5" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(5);
            g.save.totalTime = 30; g.save.tideHeldUntil = 0;
            Check(!g.LowTide, "The authored tidal world is truly high tide for this restoration test");
            Point flooded = new Point(); bool found = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !found; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !found; x++)
            {
                if (g.Map.rows[y][x] != '%') continue;
                Point candidate = new Point(x + .5f, y + .5f);
                if (g.CanStand(candidate)) continue;
                Point[] neighbors = {
                    candidate + new Point(1, 0), candidate + new Point(-1, 0),
                    candidate + new Point(0, 1), candidate + new Point(0, -1)
                };
                foreach (Point next in neighbors)
                    if (g.CanStand(next)) { flooded = candidate; found = true; break; }
            }
            Check(found, "An actual shoal becomes underwater beside valid, dry actor footing");
            g.save.rescueCheckpoint = new RescueCheckpoint {
                room = 5, x = flooded.x, y = flooded.y,
                time = 40, safety = 65
            };
            var resumed = new GameCore(data, g.save); resumed.Start();
            Check(resumed.ReliefEscortActive &&
                resumed.CanStand(resumed.reliefFollowerPosition) &&
                (resumed.reliefFollowerPosition - flooded).Length < 4.5f &&
                Math.Abs(resumed.reliefMissionTime - 40) < .001f,
                "A high-tide load recovers the civilian onto the neighboring bank with no lost deadline");
            resumed.PrepareSave();
            Check(resumed.save.rescueCheckpoint != null &&
                resumed.save.rescueCheckpoint.room == 5 &&
                resumed.CanStand(new Point(resumed.save.rescueCheckpoint.x,
                    resumed.save.rescueCheckpoint.y)),
                "Subsequent saves preserve the newly dry real-world civilian coordinates");
            // Restoring from a wall is not a tidal recovery. Reject it.
            var invalid = Fresh(data);
            invalid.save.stage = 3; invalid.save.room = 5;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "fieldstation_5", "fieldstation_survey_5", "farshore_relief_5" })
                if (!invalid.save.flags.Contains(flag)) invalid.save.flags.Add(flag);
            invalid.save.frontierEnding = 2;
            invalid.save.x = g.Map.spawnX; invalid.save.y = g.Map.spawnY;
            invalid.save.totalTime = 30;
            invalid.save.rescueCheckpoint = new RescueCheckpoint {
                room = 5, x = 2.1f, y = 2.1f, time = 50, safety = 90
            };
            var rejected = new GameCore(data, invalid.save); rejected.Start();
            Check(!rejected.ReliefEscortActive && rejected.save.rescueCheckpoint == null,
                "A wall or arbitrary blocked map coordinate cannot exploit shore recovery");
        }

        public static void EscortCanopyProtectsFromRealClimateAndStaysSpent(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive, "Real rescue waves lead to a live climate-exposed escort");
            g.save.shards = 2;
            Point citizen = g.reliefFollowerPosition;
            g.save.x = citizen.x; g.save.y = citizen.y;
            ChooseRescueCivilianOption(g, "relief_climate_kit");
            Check(g.reliefClimateKitTime > 13.5f && !g.ReliefClimateKitAvailable &&
                g.save.shards == 1,
                "An actual world interaction spends one shard for fourteen seconds of weather protection");
            g.Interact();
            Check(!Array.Exists(g.conversation.choices,
                c => c.action == "relief_climate_kit"),
                "The same escort cannot redeploy a second free weather kit");
            g.Close();
            // Compare the same actual exposed map footing with and without
            // protection. Keep player away and survivor waiting to avoid movement.
            Point exposed = new Point(); bool found = false;
            for (int y = 3; y < g.Map.rows.Length - 3 && !found; y++)
            for (int x = 3; x < g.Map.rows[y].Length - 3 && !found; x++)
            {
                Point p = new Point(x + .5f, y + .5f);
                if (!g.CanStand(p) ||
                    (p - new Point(post.x, post.y)).Length < 8f) continue;
                bool shade = false;
                foreach (WorldObject o in g.Map.objects)
                    if (o.kind == "shade" &&
                        (p - new Point(o.x, o.y)).Length < 4f) shade = true;
                if (shade) continue;
                exposed = p; found = true;
            }
            Check(found, "Actual desert terrain contains an exposed rescue route outside shelter and shade");
            g.enemies.Clear(); g.reliefFollowerWaiting = true;
            g.reliefFollowerPosition = exposed; g.reliefFollowerSafety = 70;
            g.save.x = post.x; g.save.y = post.y;
            g.Tick(.05f, new Point());
            Check(Math.Abs(g.reliefFollowerSafety - 70f) < .001f,
                "Active canopy blocks actual desert exposure without changing civilian safety or granting healing");
            g.reliefClimateKitTime = 0; g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < 70f,
                "The same real desert footing resumes harmful weather after protection expires");
            g.PrepareSave();
            Check(g.save.rescueCheckpoint != null &&
                g.save.rescueCheckpoint.climateKitUsed && g.save.shards == 1,
                "The optional save checkpoint stores the spent weather kit and paid shard cost");
            var reload = new GameCore(data, g.save); reload.Start();
            Check(reload.ReliefEscortActive && !reload.ReliefClimateKitAvailable &&
                reload.reliefClimateKitTime == 0 && reload.save.shards == 1,
                "Reload cannot refresh a used weather shield or refund its resource");
        }

        public static void CivilianEscortHurryAddsRealSpeedAndCannotFarmReload(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive && g.ReliefEscortHurryAvailable,
                "Completing two genuine enemy waves unlocks one optional hurry command");
            Point start = g.reliefFollowerPosition;
            g.save.x = start.x; g.save.y = start.y;
            float energy = g.save.energy;
            ChooseRescueCivilianOption(g, "relief_hurry");
            Check(g.reliefHurryTime > 6.9f && !g.ReliefEscortHurryAvailable &&
                Math.Abs(g.save.energy - (energy - 15)) < .001f,
                "Hurry is a real 15-energy, seven-second one-use rescue command");
            g.Interact();
            Check(!Array.Exists(g.conversation.choices, c => c.action == "relief_hurry"),
                "The same escort cannot purchase a permanent speed boost repeatedly");
            g.Close();
            g.save.x = post.x; g.save.y = post.y;
            g.Tick(.05f, new Point());
            float hurried = (g.reliefFollowerPosition - start).Length;
            Check(hurried > .17f, "Hurry visibly increases actual displacement through world collision");
            // Compare the same actor, start coordinate, player position and
            // map; only the temporary burst should change walking speed.
            g.reliefFollowerPosition = start; g.reliefHurryTime = 0;
            g.Tick(.05f, new Point());
            float normal = (g.reliefFollowerPosition - start).Length;
            Check(hurried > normal + .045f,
                "The speed boost produces more real ground movement than ordinary following");
            g.save.x = g.reliefFollowerPosition.x;
            g.save.y = g.reliefFollowerPosition.y;
            g.PrepareSave();
            Check(g.save.rescueCheckpoint != null &&
                g.save.rescueCheckpoint.hurryUsed,
                "A checkpoint remembers the consumed command instead of resetting its charge");
            var restored = new GameCore(data, g.save);
            restored.Start();
            Check(restored.ReliefEscortActive &&
                !restored.ReliefEscortHurryAvailable &&
                restored.reliefHurryTime == 0,
                "Reloading cannot renew a spent speed charge or extend its seven-second duration");
            restored.PrepareSave();
            Check(restored.save.rescueCheckpoint != null &&
                restored.save.rescueCheckpoint.hurryUsed,
                "A second save preserves the original paid decision without refunding it");
        }

        public static void CivilianEscortReloadContinuesRealReturn(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6", "farshore_relief_6" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            g.PrepareSave();
            Check(g.save.rescueCheckpoint == null,
                "A saved unfinished ambush cannot pretend that two real waves were defeated");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive, "The real combat advances to the return escort");
            Point civilian = g.reliefFollowerPosition;
            g.save.x = civilian.x; g.save.y = civilian.y;
            ChooseRescueCivilianOption(g, "relief_wait");
            g.reliefFollowerSafety = 63; g.reliefMissionTime = 46;
            int coins = g.save.coins, herbs = g.save.inventory[2];
            g.PrepareSave();
            Check(g.save.rescueCheckpoint != null &&
                g.save.rescueCheckpoint.room == 6 &&
                g.save.rescueCheckpoint.waiting &&
                Math.Abs(g.save.rescueCheckpoint.time - 46) < .001f,
                "The saved record includes a real waiting survivor and the original timer");
            var resumed = new GameCore(data, g.save); resumed.Start();
            Check(resumed.ReliefEscortActive && resumed.reliefFollowerWaiting &&
                resumed.reliefMissionWave == 3 &&
                Math.Abs(resumed.reliefMissionTime - 46) < .001f &&
                Math.Abs(resumed.reliefFollowerSafety - 63) < .001f &&
                (resumed.reliefFollowerPosition - civilian).Length < .001f,
                "Reloading resumes the real civilian at the correct world-space location");
            Check(resumed.CanStand(resumed.reliefFollowerPosition) &&
                resumed.save.coins == coins && resumed.save.inventory[2] == herbs,
                "Restoration does not invent rewards, items or impassable actor positions");
            resumed.PrepareSave();
            Check(resumed.save.rescueCheckpoint != null,
                "A recovered escort can be saved again without losing its checkpoint");
            resumed.save.x = post.x; resumed.save.y = post.y;
            resumed.Tick(.05f, new Point());
            Check((resumed.reliefFollowerPosition - civilian).Length < .001f,
                "A saved waiting command continues to hold the survivor after loading");
            resumed.save.x = civilian.x; resumed.save.y = civilian.y;
            ChooseRescueCivilianOption(resumed, "relief_follow");
            resumed.save.x = post.x; resumed.save.y = post.y;
            for (int i = 0; i < 1000 && resumed.reliefMissionRoom != 0; i++)
                resumed.Tick(.05f, new Point());
            Check(resumed.Has("relief_rescue_6") && resumed.reliefMissionRoom == 0,
                "The resumed civilian physically arrives home and earns only the normal first-clear reward");
            resumed.PrepareSave();
            Check(resumed.save.rescueCheckpoint == null,
                "Successful rescue does not leave a stale award-generating checkpoint");
        }

        public static void OldAndCorruptCivilianCheckpointsRemainSafe(Campaign data)
        {
            var old = Fresh(data); old.PrepareSave();
            Check(old.save.version == 2 && old.save.rescueCheckpoint == null,
                "Previous save slots have no mandatory rescue checkpoint");
            var normal = new GameCore(data, old.save); normal.Start();
            Check(normal.reliefMissionRoom == 0, "Older saved games continue to load normally");
            old.save.stage = 3; old.save.room = 6;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6", "farshore_relief_6" })
                if (!old.save.flags.Contains(flag)) old.save.flags.Add(flag);
            old.save.frontierEnding = 2;
            old.save.x = data.maps[6].spawnX; old.save.y = data.maps[6].spawnY;
            old.save.rescueCheckpoint = new RescueCheckpoint {
                room = 6, x = float.NaN, y = 14, time = 50, safety = 40
            };
            var invalid = new GameCore(data, old.save); invalid.Start();
            Check(invalid.reliefMissionRoom == 0 &&
                invalid.save.rescueCheckpoint == null,
                "An invalid coordinate cannot create an invisible or off-map civilian");
            old.save.rescueCheckpoint = new RescueCheckpoint {
                room = 7, x = 16, y = 14, time = 44, safety = 65
            };
            var foreign = new GameCore(data, old.save); foreign.Start();
            Check(foreign.reliefMissionRoom == 0 &&
                foreign.save.rescueCheckpoint == null,
                "An unrelated room's checkpoint cannot alter current progression");
        }

        public static void CivilianBracesForRealEscortWindupThenResumesWalking(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Point civilian = g.reliefFollowerPosition;
            Check(g.ReliefEscortActive && !g.reliefFollowerWaiting,
                "Real completed ambush begins with an escort ready to walk");
            g.save.x = station.x; g.save.y = station.y;
            var attacker = new Enemy(590, 0, civilian.x - 1.3f, civilian.y, 50);
            attacker.target = civilian; attacker.Face(civilian - attacker.Position);
            attacker.state = 1; attacker.timer = .5f;
            attacker.targetingReliefCivilian = true;
            g.enemies.Add(attacker);
            Check(g.ReliefEscortUnderAttack,
                "A real enemy windup targeted at the moving civilian is visible as immediate danger");
            g.Tick(.05f, new Point());
            Check((g.reliefFollowerPosition - civilian).Length < .001f &&
                !g.reliefFollowerMoving && g.ReliefEscortUnderAttack,
                "The civilian braces rather than stepping into a committed hostile strike");
            attacker.state = 0; attacker.timer = 100;
            attacker.targetingReliefCivilian = false;
            g.Tick(.05f, new Point());
            Check(!g.ReliefEscortUnderAttack && g.reliefFollowerMoving &&
                (g.reliefFollowerPosition - civilian).Length > .001f,
                "Once the attack is disarmed, the same civilian resumes real collision-safe following");
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(!g.ReliefEscortUnderAttack && g.reliefMissionRoom == 0,
                "Mission exit clears incoming-attack signals without changing the persistent save");
        }

        public static void EscortGuardUsesRealEnergyAndInterceptsPatrolDamage(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive && g.ReliefEscortGuardAvailable,
                "Winning both waves offers one fresh escort formation without retaining the prior rally");
            Point citizen = g.reliefFollowerPosition;
            g.reliefFollowerWaiting = true;
            g.save.x = citizen.x; g.save.y = citizen.y;
            float energy = g.save.energy;
            ChooseRescueCivilianOption(g, "relief_escort_guard");
            Check(g.reliefWardTime > 7.5f && !g.ReliefEscortGuardAvailable &&
                Math.Abs(g.save.energy - (energy - 20)) < .001f,
                "A real interaction spends twenty energy for eight seconds of physical protection");
            g.Interact();
            Check(g.conversation != null &&
                !Array.Exists(g.conversation.choices,
                    c => c.action == "relief_escort_guard"),
                "The escort dialog no longer offers a second rally during the same return");
            g.Close();
            var patrol = new Enemy(590, 0, citizen.x - 1.3f, citizen.y, 55);
            patrol.state = 1; patrol.timer = .01f;
            patrol.targetingReliefCivilian = true;
            patrol.target = citizen; patrol.Face(citizen - patrol.Position);
            g.enemies.Add(patrol);
            g.save.x = post.x; g.save.y = post.y;
            float safety = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            float loss = safety - g.reliefFollowerSafety;
            Check(loss > 7 && loss < 9 && g.ReliefEscortActive,
                "The temporary formation reduces a real patrol melee release instead of hiding the damage");
            g.reliefWardTime = 0; patrol.releaseTime = 0; patrol.stun = patrol.hitTime = 0;
            patrol.state = 1; patrol.timer = .01f;
            patrol.targetingReliefCivilian = true; patrol.target = citizen;
            float unprotected = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Check(unprotected - g.reliefFollowerSafety > 13.5f,
                "A subsequent actual contact inflicts full danger after the short formation expires");
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && g.reliefWardTime == 0 &&
                !g.ReliefEscortGuardAvailable && !g.enemies.Exists(e => e.targetingReliefCivilian),
                "Timeout resets formation and ordinary patrol targeting with no saved combat state");
        }

        public static void EscortPatrolArrowsRequireRealFlightAndRetireOnFailure(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report",
                "desert_report", "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_6" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 2; g.Enter(6); g.enemies.Clear();
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            StartCivilianRescueViaRealDialogue(g, post);
            ReachCivilianRescueMarker(g, 6);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.ReliefEscortActive, "Patrol arrow regression starts during a real return escort");
            Point resident = g.reliefFollowerPosition;
            g.reliefFollowerWaiting = true;
            g.save.x = post.x; g.save.y = post.y;
            var archer = new Enemy(591, 1, resident.x - 2.5f, resident.y, 50);
            archer.target = resident;
            archer.Face(resident - archer.Position);
            archer.state = 1; archer.timer = .01f;
            archer.targetingReliefCivilian = true;
            g.enemies.Add(archer);
            float safety = g.reliefFollowerSafety;
            g.Tick(.05f, new Point());
            Projectile shot = g.projectiles.Find(p => p.reliefMissionOwned && !p.friendly &&
                p.visual == "enemyArrow");
            Check(shot != null && g.reliefFollowerSafety > safety - 1,
                "Ordinary patrol archer fires an encounter-owned physical arrow without hitscan injury");
            archer.state = 0; archer.timer = 100;
            shot.position = new Point(resident.x - .60f, resident.y);
            shot.velocity = new Point(6, 0); shot.life = 1;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < safety - 8.5f &&
                !g.projectiles.Contains(shot),
                "Patrol arrow must collide with the real resident before inflicting safety damage");
            g.save.x = post.x; g.save.y = post.y;
            g.projectiles.Add(new Projectile {
                position = new Point(resident.x - .6f, resident.y),
                velocity = new Point(6, 0), visual = "enemyArrow",
                source = "shot", damage = 10, reliefMissionOwned = true, life = 1
            });
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 &&
                !g.projectiles.Exists(p => p.reliefMissionOwned) &&
                !g.enemies.Exists(e => e.targetingReliefCivilian),
                "Timeout removes escort-targeted arrows and returns live ordinary patrols to hero-only AI");
        }

        public static void RescueSurvivorsRequireRealProtectionAndSafeTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_6", "farshore_relief_6", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 3; g.Enter(6); g.enemies.Clear();
            WorldObject station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            StartCivilianRescueViaRealDialogue(g, station);
            Check(g.reliefMissionRoom == 6 && g.reliefMissionWave == 0 &&
                g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 0,
                "Starting from the station never summons distant raiders before the hero visits the landmark");
            int coins = g.save.coins;
            g.ReturnToTown();
            Check(g.save.room == 6 && g.reliefMissionRoom == 6,
                "Fast return to town cannot silently abandon a civilian mission");
            Action(g, station.kind, "fieldstation_to_5");
            Check(g.save.room == 6 && g.reliefMissionRoom == 6,
                "Regional station travel preserves a live rescue rather than discarding it");
            g.save.flags.Remove("fieldstation_survey_6");
            Action(g, station.kind, "fieldstation_survey_start");
            g.save.flags.Add("fieldstation_survey_6");
            Check(g.fieldSurveyRoom == 0 && g.reliefMissionRoom == 6,
                "Another timed survey cannot replace the active rescue timer");
            ReachCivilianRescueMarker(g, 6);
            Check(g.reliefMissionWave == 1 &&
                g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 2,
                "Reaching the surveyed landmark starts the first actual ambush");
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            Check(g.reliefMissionWave == 3 && g.reliefFollowerSafety > 99.8f,
                "The evacuee begins the return nearly fully protected, with immediate real weather exposure");
            Point follower = g.reliefFollowerPosition;
            g.save.x = station.x; g.save.y = station.y;
            // Patrol strikes should be telegraphed and released, not
            // invisible recurring proximity damage during the return.
            g.reliefFollowerWaiting = true;
            var patrol = new Enemy(590, 0, follower.x - 1.3f, follower.y, 50);
            patrol.timer = .01f; g.enemies.Add(patrol);
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety > 99.8f && patrol.state == 1 &&
                patrol.targetingReliefCivilian &&
                (patrol.target - follower).Length < .001f,
                "Normal escort patrols warn before committing actual contact instead of draining by proximity");
            patrol.timer = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefFollowerSafety < 87 && g.reliefMissionRoom == 6 &&
                !g.Has("relief_rescue_6"),
                "Released normal patrol contact inflicts actual safety damage and does not grant completion");
            g.reliefFollowerSafety = .01f;
            patrol.releaseTime = patrol.stun = patrol.hitTime = 0;
            patrol.state = 1; patrol.timer = .01f;
            patrol.targetingReliefCivilian = true; patrol.target = follower;
            patrol.Face(follower - patrol.Position);
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.Has("relief_rescue_6") &&
                g.save.coins == coins,
                "Losing civilian safety cancels the escort without permanent progress or a paid reward");
        }

        public static void FarshoreEvacuationHasRealCombatAndPersistentFirstClear(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10; g.save.stage = 3;
            foreach (string flag in new[] { "intro", "exp_intro", "harbor_report", "desert_report", "mountain_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_5", "fieldstation_survey_6", "fieldstation_survey_7",
                "fieldstation_survey_complete", "farshore_relief_5", "farshore_relief_6",
                "farshore_relief_7", "farshore_relief_complete" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 3;
            Check(!g.GetQuest("relief_rescue").locked && g.GetQuest("relief_rescue").steps.Count == 4,
                "The real regional mission appears only after the playable shelter network exists");
            int coins = g.save.coins, shards = g.save.shards, talents = g.save.skillPoints;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                g.save.x = station.x; g.save.y = station.y;
                Action(g, station.kind, "fieldstation_rescue_menu");
                Check(g.conversation != null &&
                    Array.Exists(g.conversation.choices, c => c.action == "fieldstation_rescue_start"),
                    "The physical shelter exposes a live, playable rescue action");
                g.Close();
                StartCivilianRescueViaRealDialogue(g, station);
                Check(g.reliefMissionRoom == region && g.reliefMissionWave == 0 &&
                    g.reliefMissionTime > 119f && g.save.trackedQuest == "relief_rescue" &&
                    g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 0,
                    "Each region begins with an actual outbound search and no premature enemy spawning");
                Check(g.GetQuest("relief_rescue").next.Contains("巡测地标") &&
                    g.GetQuest("relief_rescue").next.Contains("120s"),
                    "The active quest points to the rescue site and displays the timed search");
                ReachCivilianRescueMarker(g, region);
                Check(g.reliefMissionWave == 1 &&
                    g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 2,
                    "Arriving at each real field marker spawns exactly two hostile raiders");
                foreach (Enemy enemy in g.enemies)
                    if (enemy.id >= 85000 && enemy.id < 85100)
                        Check(enemy.timer < 2 && enemy.hp > 0 &&
                            g.CanStand(enemy.Position) && g.NavigationPath(enemy.Position).Count > 0,
                            "Relief attackers have real health, bounded opening timers and reachable spawns");
                Check(g.GetQuest("relief_rescue").room == region &&
                    g.GetQuest("relief_rescue").next.Contains("120s"),
                    "The journal directs the player to the active rescue while displaying remaining time");
                // Clear through normal world enemy removal as though both foes were
                // defeated; this checks the production wave-and-reward state machine.
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(g.reliefMissionWave == 2 &&
                    g.enemies.FindAll(e => e.id >= 85000 && e.id < 85100).Count == 2,
                    "The first defeat opens a second real wave rather than instantly paying a reward");
                // Defeating the second wave must retire its old arrows
                // before any return-escort frame can resolve their impact.
                g.projectiles.Add(new Projectile {
                    position = new Point(station.x, station.y), velocity = new Point(),
                    life = 2, friendly = false, visual = "enemyArrow",
                    reliefMissionOwned = true, source = "shot"
                });
                var unrelatedShot = new Projectile {
                    position = new Point(station.x, station.y), velocity = new Point(),
                    life = 2, friendly = true, visual = "arrow"
                };
                g.projectiles.Add(unrelatedShot);
                g.effects.Add(new Effect("danger", g.Position, 2) {
                    radius = .7f, reliefMissionOwned = true
                });
                var unrelatedEffect = new Effect("discover", g.Position, 2);
                g.effects.Add(unrelatedEffect);
                g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
                g.Tick(.05f, new Point());
                Check(!g.projectiles.Exists(p => p.reliefMissionOwned) &&
                    !g.effects.Exists(e => e.reliefMissionOwned) &&
                    g.projectiles.Contains(unrelatedShot) &&
                    g.effects.Contains(unrelatedEffect),
                    "Completing wave two retires only defeated raiders' late arrows and warnings");
                g.projectiles.Remove(unrelatedShot);
                g.effects.Remove(unrelatedEffect);
                Check(g.reliefMissionWave == 3 && !g.Has("relief_rescue_" + region),
                    "Killing all raiders still requires actually returning survivors to the shelter");
                Point savedSurvivor = g.reliefFollowerPosition;
                g.save.x = station.x; g.save.y = station.y;
                g.Tick(.05f, new Point());
                Check(g.reliefMissionWave == 3 && g.reliefMissionRoom == region &&
                    (g.reliefFollowerPosition - savedSurvivor).Length > 0,
                    "The rescued civilian physically follows the hero and cannot teleport to the station");
                g.reliefFollowerPosition = new Point(station.x, station.y);
                g.Tick(.05f, new Point());
                Check(g.reliefMissionRoom == 0 && g.Has("relief_rescue_" + region) &&
                    g.save.coins == coins + (region - 4) * 45,
                    "Real return completes and permanently records the rescued civilians");
                string npcKind = region == 5 ? "captain" : region == 6 ? "tinker" : "keeper";
                g.Talk(Object(g, npcKind));
                Check(g.conversation.text.Contains(region == 5 ? "滩涂救回" :
                    region == 6 ? "失联行路者" : "带回的旅人") &&
                    g.conversation.textEn.Contains(region == 5 ? "pontoons truly saved lives" :
                    region == 6 ? "missing desert travelers" : "travelers you brought home"),
                    "The living regional companion directly remembers the rescue in both languages [room=" + region +
                    "; zh=" + (g.conversation == null ? "none" : g.conversation.text.Substring(0, Math.Min(180, g.conversation.text.Length))) +
                    "; en=" + (g.conversation == null ? "none" : g.conversation.textEn.Substring(0, Math.Min(180, g.conversation.textEn.Length))) + "]");
                g.Close();
            }
            Check(g.Has("relief_rescue_complete") &&
                g.save.coins == coins + 135 && g.save.shards == shards + 9 &&
                g.save.skillPoints == talents + 1 &&
                g.GetQuest("relief_rescue").complete &&
                g.WorldResponse().Contains("远岸居民撤离：3/3"),
                "Three first-clears unlock the final talent, complete journal and saved world response");
            WorldObject finalStation = Object(g, "fieldstation7");
            g.save.x = finalStation.x; g.save.y = finalStation.y;
            int rewarded = g.save.coins, rewardedShards = g.save.shards, rewardedPoints = g.save.skillPoints;
            StartCivilianRescueViaRealDialogue(g, finalStation);
            Check(g.reliefMissionRoom == 7 && g.reliefMissionWave == 0,
                "Completed civilian rescue remains replayable with the outbound phase");
            ReachCivilianRescueMarker(g, 7);
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            g.enemies.RemoveAll(e => e.id >= 85000 && e.id < 85100);
            g.Tick(.05f, new Point());
            // In the real replay both the hero and survivor must return.
            // Completing only the NPC's position cannot end an active sortie.
            g.reliefFollowerPosition = new Point(finalStation.x, finalStation.y);
            g.save.x = finalStation.x; g.save.y = finalStation.y;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0,
                "The replay ends only after both the player and evacuee reach home");
            Check(g.save.coins == rewarded && g.save.shards == rewardedShards &&
                g.save.skillPoints == rewardedPoints,
                "Replaying civilian rescue yields no coins, shards or repeat talent");
            g.save.x = finalStation.x; g.save.y = finalStation.y;
            StartCivilianRescueViaRealDialogue(g, finalStation);
            g.reliefMissionTime = .01f;
            g.Tick(.05f, new Point());
            Check(g.reliefMissionRoom == 0 && !g.enemies.Exists(e => e.id >= 85000 && e.id < 85100),
                "Timed-out rescues clean up all transient special combatants");
            StartCivilianRescueViaRealDialogue(g, finalStation);
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.reliefMissionRoom == 0 &&
                !restored.enemies.Exists(e => e.id >= 85000 && e.id < 85100) &&
                restored.GetQuest("relief_rescue").complete &&
                restored.save.coins == rewarded,
                "Old-schema save reload keeps completed rescues without restoring unfinished raider waves");
        }

        public static void CivilianReliefActuallyHealsAndProtectsOuterClimate(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "farshore_relief_5", "farshore_relief_6", "farshore_relief_7", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 1;
            int coins = g.save.coins;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                g.save.x = station.x; g.save.y = station.y; g.save.health = 40;
                for (int frame = 0; frame < 20; frame++) g.Tick(.05f, new Point());
                Check(g.save.health > 41 && g.save.health < 42 && g.save.coins == coins,
                    "Each built civilian refuge offers slow actual health recovery without passive currency");
                float beforeCombat = g.save.health;
                g.enemies.Add(new Enemy(260 + region, 0, station.x + 1, station.y, 70));
                g.Tick(.05f, new Point());
                Check(g.save.health <= beforeCombat + .0001f,
                    "Nearby combat disables field healing even while standing inside a completed shelter");
                g.enemies.Clear();
                g.save.health = 40;
                g.save.flags.Remove("farshore_relief_" + region);
                g.Tick(.05f, new Point());
                Check(g.save.health <= 40.0001f,
                    "The old station without a civilian upgrade cannot grant field hospital recovery");
                g.save.flags.Add("farshore_relief_" + region);
                g.save.health = g.save.maxHealth - .02f;
                g.Tick(.05f, new Point());
                Check(g.save.health <= g.save.maxHealth,
                    "Recovery never exceeds the saved hero's real health maximum");
            }
            for (int region = 6; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject station = Object(g, "fieldstation" + region);
                Point anchor = new Point(station.x, station.y);
                Point edge = anchor;
                bool found = false;
                Point[] offsets = { new Point(5, 0), new Point(-5, 0),
                    new Point(0, 5), new Point(0, -5),
                    new Point(3.5f, 3.5f), new Point(-3.5f, -3.5f),
                    new Point(3.5f, -3.5f), new Point(-3.5f, 3.5f) };
                foreach (Point shift in offsets)
                {
                    Point sample = anchor + shift;
                    if (!g.CanStand(sample)) continue;
                    if (region == 6)
                    {
                        bool nearOasis = false;
                        foreach (WorldObject shade in g.Map.objects)
                            if (shade.kind == "shade" &&
                                (new Point(shade.x, shade.y) - sample).Length < 3) nearOasis = true;
                        if (nearOasis) continue;
                    }
                    edge = sample; found = true; break;
                }
                Check(found && (edge - anchor).Length > 4.2f &&
                    (edge - anchor).Length < g.FieldStationRefugeRadius(region),
                    "The authored farshore map has reachable ground in the newly protected outer band");
                g.save.x = edge.x; g.save.y = edge.y;
                g.save.heat = region == 6 ? 50 : 0;
                g.save.cold = region == 7 ? 50 : 0;
                g.Tick(.05f, new Point());
                Check(region == 6 ? g.save.heat < 50 : g.save.cold < 50,
                    "The full expanded refuge radius changes the live heat/cold simulation");
                g.save.flags.Remove("farshore_relief_" + region);
                g.save.heat = region == 6 ? 50 : 0;
                g.save.cold = region == 7 ? 50 : 0;
                g.Tick(.05f, new Point());
                Check(region == 6 ? g.save.heat > 50 : g.save.cold > 50,
                    "Without the built upgrade, identical outer-band ground remains exposed");
                g.save.flags.Add("farshore_relief_" + region);
            }
        }

        public static void FarshoreReliefRoutesLinkExistingInnAndStations(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "farshore_relief_5", "farshore_relief_6", "farshore_relief_7", "farshore_relief_complete" })
                g.save.flags.Add(flag);
            g.save.frontierEnding = 2;
            int coins = g.save.coins, skill = g.save.skillPoints;
            g.Enter(9); g.enemies.Clear();
            WorldObject desk = Object(g, "surveyDesk");
            g.save.x = desk.x; g.save.y = desk.y;
            Check(!g.Has("survey_complete"),
                "Civilian refuge travel does not depend on an unrelated completed warden chart");
            Action(g, desk.kind, "farshore_relief_routes");
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices, c => c.action == "farshore_relief_to_7"),
                "The actual Hearth Inn chart opens a separate three-destination relief submenu");
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "farshore_relief_to_6")
                { g.Choose(i); break; }
            Check(g.save.room == 6 && g.CanStand(g.Position) &&
                (g.Position - new Point(Object(g, "fieldstation6").x,
                    Object(g, "fieldstation6").y)).Length <= 1.5f,
                "Inn convoy reaches the desert station at a safe walkable adjacent landing");
            WorldObject post = Object(g, "fieldstation6");
            g.save.x = post.x; g.save.y = post.y;
            g.enemies.Add(new Enemy(58, 0, post.x + 1, post.y, 50));
            Action(g, post.kind, "fieldstation_inn");
            Check(g.save.room == 6, "Live enemies prevent instant escape along civilian supply routes");
            g.enemies.Clear();
            Action(g, post.kind, "fieldstation_inn");
            Check(g.save.room == 9 && g.CanStand(g.Position) &&
                g.save.coins == coins && g.save.skillPoints == skill,
                "Relief route returns to the real Inn without awarding duplicate currency or talents");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            WorldObject savedDesk = Object(resumed, "surveyDesk");
            resumed.save.x = savedDesk.x; resumed.save.y = savedDesk.y;
            Action(resumed, savedDesk.kind, "farshore_relief_routes");
            Check(Array.Exists(resumed.conversation.choices, c => c.action == "farshore_relief_to_5"),
                "Existing save flags preserve the additional rescue routes after loading");
            var locked = Fresh(data); locked.save.stage = 1; locked.save.frontierEnding = 1;
            foreach (string flag in new[] { "farshore_relief_complete", "fieldstation_7" })
                locked.save.flags.Add(flag);
            locked.Enter(9); locked.enemies.Clear();
            WorldObject restrictedDesk = Object(locked, "surveyDesk");
            locked.save.x = restrictedDesk.x; locked.save.y = restrictedDesk.y;
            Action(locked, restrictedDesk.kind, "farshore_relief_routes");
            for (int i = 0; i < locked.conversation.choices.Length; i++)
                if (locked.conversation.choices[i].action == "farshore_relief_to_7")
                { locked.Choose(i); break; }
            Check(locked.save.room == 9 && !locked.CanEnter(7),
                "Malformed or early save flags never bypass the real Frostchime chapter gate");
        }

        public static void FarshoreReliefWorksProvidePersistentClimateSanctuaries(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10; g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete",
                "fieldstation_survey_5", "fieldstation_survey_6", "fieldstation_survey_7",
                "fieldstation_survey_complete" }) g.save.flags.Add(flag);
            Check(g.GetQuest("farshore_relief").locked && g.TidewatchHoldSeconds == 45 &&
                g.FieldStationRefugeRadius(6) == 4.2f,
                "New relief work stays locked before governance, keeping original station balance");
            g.Enter(5); g.enemies.Clear();
            WorldObject harbor = Object(g, "fieldstation5");
            g.save.x = harbor.x; g.save.y = harbor.y;
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_relief_5"),
                "Before the covenant, the harbor station does not advertise a future upgrade");
            g.Close(); g.save.frontierEnding = 2;
            Check(!g.GetQuest("farshore_relief").locked && g.GetQuest("farshore_relief").steps.Count == 4,
                "Council governance unlocks a three-region construction quest");
            g.save.inventory[0] = 6; g.save.inventory[4] = 3;
            int startWood = g.save.inventory[0], startGear = g.save.inventory[4];
            g.save.totalTime = 28;
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(!g.Has("farshore_relief_5") && g.save.inventory[0] == startWood &&
                g.save.inventory[4] == startGear,
                "Unsafe high tide blocks actual pontoon construction without taking supplies");
            g.save.totalTime = 0;
            g.enemies.Add(new Enemy(47, 0, harbor.x + 1, harbor.y, 50));
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(!g.Has("farshore_relief_5") && g.save.inventory[0] == startWood,
                "An enemy close to construction blocks the upgrade and preserves supplies");
            g.enemies.Clear();
            int coins = g.save.coins, shards = g.save.shards, talents = g.save.skillPoints;
            Action(g, harbor.kind, "fieldstation_relief_5");
            Check(g.Has("farshore_relief_5") && g.TidewatchHoldSeconds == 90 &&
                g.save.inventory[0] == startWood - 2 && g.save.inventory[4] == startGear - 1 &&
                g.save.coins == coins + 30 && g.save.shards == shards + 2,
                "Survey-backed harbor pontoons consume their cost once and double the real ebb window");
            g.save.totalTime = 30; Action(g, harbor.kind, "fieldstation_tide");
            Check(g.save.tideHeldUntil >= 119.9f && g.LowTide,
                "Upgraded tidewatch holds flooded shoals open for ninety actual game seconds");
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_relief_5"),
                "Finished harbor shelter cannot replay its construction reward");
            g.Close();
            g.Enter(6); g.enemies.Clear();
            WorldObject desert = Object(g, "fieldstation6"); g.save.x = desert.x; g.save.y = desert.y;
            g.save.inventory[1] = 4; g.save.inventory[2] = 4;
            int crystals = g.save.inventory[1], herbs = g.save.inventory[2];
            Action(g, desert.kind, "fieldstation_relief_6");
            Check(g.Has("farshore_relief_6") && g.save.inventory[1] == crystals - 2 &&
                g.save.inventory[2] == herbs - 1 && g.FieldStationRefugeRadius(6) == 6.5f,
                "Shaded desert supply lane consumes real resources and expands the heat refuge");
            g.Enter(7); g.enemies.Clear();
            WorldObject mountain = Object(g, "fieldstation7"); g.save.x = mountain.x; g.save.y = mountain.y;
            g.save.inventory[0] = 3; g.save.inventory[2] = 4;
            Action(g, mountain.kind, "fieldstation_relief_7");
            Check(g.Has("farshore_relief_7") && g.Has("farshore_relief_complete") &&
                g.FieldStationRefugeRadius(7) == 6.5f && g.save.inventory[0] == 2 &&
                g.save.inventory[2] == 2 && g.save.skillPoints == talents + 1 &&
                g.GetQuest("farshore_relief").complete,
                "Last warming depot builds the final physical shelter and pays one completion talent");
            int totalCoins = g.save.coins, totalShards = g.save.shards;
            g.Close(); g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("farshore_relief").complete &&
                restored.TidewatchHoldSeconds == 90 &&
                restored.FieldStationRefugeRadius(6) == 6.5f &&
                restored.FieldStationRefugeRadius(7) == 6.5f &&
                restored.save.coins == totalCoins && restored.save.shards == totalShards,
                "All physical climate upgrades and rewards survive old-schema save reload");
        }

        public static void FarshoreStationSurveysRequireRealOutAndBackTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7", "fieldstation_complete" })
                g.save.flags.Add(flag);
            int coins = g.save.coins, shards = g.save.shards, tea = g.save.tea;
            for (int region = 5; region <= 7; region++)
            {
                g.Enter(region); g.enemies.Clear();
                WorldObject post = Object(g, "fieldstation" + region);
                g.save.x = post.x; g.save.y = post.y;
                Check(g.GetQuest("station_patrol").steps.Count == 4, "Survey journal is available after restoration");
                SetOuterPatrolLull(g,region);
                Check(g.OuterPatrolActive&&g.SafeTravel,
                    "The station is locally safe while an unrelated patrol owns the fieldwork slot");
                Action(g, post.kind, "fieldstation_survey_start");
                Check(g.fieldSurveyRoom==0&&g.fieldSurveyPhase==0,
                    "The timed field survey cannot start over an unrelated patrol lull");
                if(region==6)
                {
                    WorldObject raceSkiff=Object(g,"skiff");
                    g.save.x=raceSkiff.x;g.save.y=raceSkiff.y;
                    Action(g,raceSkiff.kind,"race");
                    Check(!g.racing&&g.fieldSurveyRoom==0,
                        "The sand-skiff race cannot start while another field mission owns the slot");
                    g.save.x=post.x;g.save.y=post.y;
                }
                ClearOuterPatrolLull(g);
                Action(g, post.kind, "fieldstation_survey_start");
                Check(g.fieldSurveyRoom == region && g.fieldSurveyPhase == 1 &&
                    g.fieldSurveyTime > 89 && g.save.trackedQuest == "station_patrol",
                    "Each repaired station starts a tracked 90-second local field mission");
                g.save.english = true;
                Check(g.Conditions().Contains("SURVEY") && g.Conditions().Contains("90s"),
                    "Active survey countdown stays visible in the conditions HUD");
                g.save.english = false;
                // Traveling through a linked station must not erase a live
                // out-and-back survey without the player deliberately canceling.
                int otherRegion = region == 5 ? 6 : 5;
                float surveyTimeBefore = g.fieldSurveyTime;
                if (region == 6)
                {
                    int oldWaystation = g.save.projects[1];
                    g.save.projects[1] = 1;
                    TalkNear(g, "campfire");
                    Check(g.mode == "dialog" &&
                        !Array.Exists(g.conversation.choices, c => c.action == "fieldrest") &&
                        g.fieldSurveyRoom == region &&
                        Math.Abs(g.fieldSurveyTime - surveyTimeBefore) < .001f,
                        "An active 90-second survey blocks camp rest instead of pausing its deadline");
                    g.Close();
                    g.save.projects[1] = oldWaystation;
                    g.save.x = post.x; g.save.y = post.y;
                }
                Action(g, post.kind, "fieldstation_to_" + otherRegion);
                Check(g.save.room == region && g.fieldSurveyRoom == region &&
                    g.fieldSurveyPhase == 1 && g.fieldSurveyTime == surveyTimeBefore,
                    "Linked station travel cannot silently destroy an unfinished regional survey");
                if (region == 6)
                {
                    g.save.x = Object(g, "skiff").x; g.save.y = Object(g, "skiff").y;
                    Action(g, "skiff", "race");
                    Check(!g.racing && g.fieldSurveyRoom == 6,
                        "Starting the sand-skiff race cannot silently overwrite a live field survey");
                }
                Point marker = g.FieldSurveyTarget(region);
                Check(g.CanStand(marker), "Regional survey landmark is on walkable ground");
                g.save.x = marker.x; g.save.y = marker.y;
                if (region == 5)
                {
                    g.save.totalTime = 27;
                    g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Harbor reading is blocked at high tide");
                    g.save.totalTime = 0;
                }
                else if (region == 6)
                {
                    g.save.heat = 80; g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Heat-distorted desert reading cannot advance");
                    g.save.heat = 0;
                }
                else
                {
                    g.save.cold = 80; g.Tick(.05f, new Point());
                    Check(g.fieldSurveyPhase == 1, "Cold-distorted highland reading cannot advance");
                    g.save.cold = 0;
                }
                g.Tick(.05f, new Point());
                Check(g.fieldSurveyPhase == 2 && g.GetQuest("station_patrol").room == region &&
                    (g.GetQuest("station_patrol").target - new Point(post.x, post.y)).Length < .01f,
                    "Actual landmark contact and safe weather create a return-to-post objective");
                g.save.x = post.x; g.save.y = post.y;
                g.Tick(.05f, new Point());
                Check(g.Has("fieldstation_survey_" + region) && g.fieldSurveyRoom == 0 &&
                    g.save.coins == coins + 24 * (region - 4) &&
                    g.save.shards == shards + 2 * (region - 4),
                    "Returning to the real station archives one regional reading and pays once");
                g.Talk(post);
                Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_survey_start"),
                    "Completed station does not offer farmable repeat rewards");
                g.Close();
            }
            Check(g.Has("fieldstation_survey_complete") && g.GetQuest("station_patrol").complete &&
                g.save.tea == tea + 2, "All three readings grant exactly one network-wide tea bonus");

            // A rest time skip must not leave a live 30-second race clock paused
            // while the player heals at a nearby campfire.
            g.Enter(6); g.enemies.Clear();
            int oldRaceWaystation = g.save.projects[1];
            g.save.projects[1] = 1;
            WorldObject skiff = Object(g, "skiff");
            g.save.x = skiff.x; g.save.y = skiff.y;
            Action(g, "skiff", "race");
            Check(g.racing, "Unlocked sand-skiff starts a real timed race");
            if (g.racing)
            {
                TalkNear(g, "campfire");
                Check(g.mode == "dialog" &&
                    !Array.Exists(g.conversation.choices, c => c.action == "fieldrest"),
                    "Camp rest is unavailable while the sand-skiff race clock is live");
                g.Close();
            }
            g.racing = false;
            g.save.projects[1] = oldRaceWaystation;

            // Completing the full circuit unlocks an actual forecast, not just flavor text.
            g.Enter(5); g.enemies.Clear();
            WorldObject weatherPost = Object(g, "fieldstation5");
            g.save.x = weatherPost.x; g.save.y = weatherPost.y;
            g.save.totalTime = 27; // high tide; next natural ebb in 9 seconds
            Action(g, weatherPost.kind, "fieldstation_forecast");
            Check(g.mode == "dialog" && g.conversation != null &&
                g.conversation.textEn.Contains("9s") &&
                g.conversation.textEn.Contains("1.3/s") &&
                g.conversation.textEn.Contains("4.2-tile"),
                "All three field reports unlock a real tide and climate forecast at any restored station");
            g.Close();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("station_patrol").complete && restored.fieldSurveyRoom == 0 &&
                restored.save.coins == g.save.coins && restored.save.tea == g.save.tea,
                "Survey flags and one-time rewards persist without new save fields");

            var retry = Fresh(data); retry.save.stage = 3;
            retry.save.flags.Add("exp_intro"); retry.save.flags.Add("fieldstation_5");
            retry.Enter(5); retry.enemies.Clear();
            var origin = Object(retry, "fieldstation5");
            retry.save.x = origin.x; retry.save.y = origin.y;
            Action(retry, origin.kind, "fieldstation_survey_start");
            retry.fieldSurveyTime = .01f; retry.Tick(.05f, new Point());
            Check(retry.fieldSurveyRoom == 0 && !retry.Has("fieldstation_survey_5"),
                "Timed-out survey clears cleanly without granting progress");
            Action(retry, origin.kind, "fieldstation_survey_start");
            retry.Enter(0);
            Check(retry.fieldSurveyRoom == 0 && !retry.Has("fieldstation_survey_5"),
                "Leaving the map cancels transient surveys without losing completed reports");
        }

        public static void FarshoreStationNetworkProvidesSafeUnlockAwareTravel(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_5", "fieldstation_6", "fieldstation_7" })
                g.save.flags.Add(flag);
            g.Enter(5); g.enemies.Clear();
            WorldObject harbor = Object(g, "fieldstation5");
            g.save.x = harbor.x; g.save.y = harbor.y;
            g.Talk(harbor);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_6"),
                "Travel options remain locked until the entire station network is restored");
            g.Close();
            g.save.flags.Add("fieldstation_complete");
            g.Talk(harbor);
            Check(Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_6") &&
                Array.Exists(g.conversation.choices, c => c.action == "fieldstation_to_7"),
                "Completed harbor station advertises both regional connections");
            g.Close();
            g.enemies.Add(new Enemy(450, 0, g.save.x + .8f, g.save.y, 70));
            Action(g, harbor.kind, "fieldstation_to_6");
            Check(g.save.room == 5, "Nearby hostiles disable cross-region station travel");
            g.enemies.Clear();
            SetOuterPatrolLull(g,5);
            Check(g.OuterPatrolActive&&g.SafeTravel,
                "The harbor is locally safe during a live patrol lull");
            Action(g, harbor.kind, "fieldstation_to_6");
            Check(g.save.room==5,
                "Station-to-station travel cannot discard another active field mission");
            ClearOuterPatrolLull(g);
            Action(g, harbor.kind, "fieldstation_to_6");
            Check(g.save.room == 6 && g.CanStand(g.Position) &&
                (g.Position - new Point(Object(g, "fieldstation6").x,
                    Object(g, "fieldstation6").y)).Length <= 1.5f,
                "Harbor link reaches a walkable landing beside the actual desert shelter");
            foreach (Enemy enemy in g.enemies)
                Check((enemy.Position - g.Position).Length >= 2.0f,
                    "No active patrol may occupy the destination landing");

            var cooler = Object(g, "fieldstation6");
            g.save.x = cooler.x; g.save.y = cooler.y; g.enemies.Clear();
            Action(g, cooler.kind, "fieldstation_to_7");
            var heater = Object(g, "fieldstation7");
            Check(g.save.room == 7 && g.CanStand(g.Position) &&
                (g.Position - new Point(heater.x, heater.y)).Length <= 1.5f,
                "Desert network reaches a safe Frostchime station arrival");

            g.save.x = heater.x; g.save.y = heater.y; g.enemies.Clear();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.enemies.Clear();
            Action(restored, "fieldstation7", "fieldstation_to_5");
            Check(restored.save.room == 5 && restored.Has("fieldstation_complete") &&
                restored.CanStand(restored.Position),
                "Network travel persists after old-schema save/load without duplicate rewards");
        }

        public static void FarshoreWeatherStationsChangeActualClimateAndTide(Campaign data)
        {
            var g = Fresh(data); g.save.level = 10;
            Check(g.GetQuest("stations").locked, "Farshore stations must not appear as unlocked before the expedition");
            g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report" })
                g.save.flags.Add(flag);
            Check(!g.GetQuest("stations").locked, "Starting the farshore expedition unlocks station guidance");
            int oldCoins = g.save.coins, oldShards = g.save.shards, oldPoints = g.save.skillPoints;
            g.Enter(5); g.enemies.Clear();
            WorldObject tidewatch = Object(g, "fieldstation5");
            g.save.x = tidewatch.x; g.save.y = tidewatch.y;
            Check(g.CanStand(g.Position) && g.Nearest() == tidewatch,
                "Mistport tidewatch is a real reachable interaction");
            g.save.inventory[0] = 4; g.save.inventory[4] = 4;
            g.save.totalTime = 27; // high tide
            Action(g, tidewatch.kind, "fieldstation_repair_5");
            Check(!g.Has("fieldstation_5") && g.save.inventory[0] == 4 &&
                g.save.inventory[4] == 4, "High tide blocks construction without spending resources");
            g.save.totalTime = 0;
            Action(g, tidewatch.kind, "fieldstation_repair_5");
            Check(g.Has("fieldstation_5") && g.save.inventory[0] == 3 &&
                g.save.inventory[4] == 3, "Low tide repairs the harbor post at its advertised cost");
            g.save.totalTime = 27;
            Check(!g.LowTide, "The harbor would normally be at high tide");
            Action(g, tidewatch.kind, "fieldstation_tide");
            Check(g.LowTide && g.save.tideHeldUntil >= g.save.totalTime + 44.9f,
                "Repaired tidewatch really opens the shoals for the promised duration");

            g.Enter(6); g.enemies.Clear();
            WorldObject cooler = Object(g, "fieldstation6");
            g.save.x = cooler.x; g.save.y = cooler.y;
            Check(g.CanStand(g.Position) && g.Nearest() == cooler,
                "Desert cooler is a usable object and not hidden in a wall");
            g.save.inventory[1] = 3; g.save.inventory[4] = 3;
            Action(g, cooler.kind, "fieldstation_repair_6");
            Check(!g.Has("fieldstation_6") && g.save.inventory[1] == 3,
                "Unlit mirror channel prevents the desert repair without wasting a crystal");
            g.save.flags.Add("mirrors_lit");
            Action(g, cooler.kind, "fieldstation_repair_6");
            Check(g.Has("fieldstation_6") && g.save.inventory[1] == 2 &&
                g.save.inventory[4] == 2 && g.NearActiveFieldStation(6, 4.2f),
                "Completed light puzzle powers an actual local cooling refuge");
            g.save.heat = 60; g.Tick(.05f, new Point());
            Check(g.save.heat < 60, "Standing at the repaired desert cooler reduces real heat");
            g.save.x = 5; g.save.y = 14; g.save.heat = 60;
            g.Tick(.05f, new Point());
            Check(g.save.heat > 60, "Heat still accumulates in the open desert beyond the safe zone");

            g.Enter(7); g.enemies.Clear();
            WorldObject heater = Object(g, "fieldstation7");
            g.save.x = heater.x; g.save.y = heater.y;
            Check(g.CanStand(g.Position) && g.Nearest() == heater,
                "Highland shelter is reachable from the map path");
            g.save.inventory[0] = 4; g.save.inventory[2] = 2;
            Action(g, heater.kind, "fieldstation_repair_7");
            Check(!g.Has("fieldstation_7") && g.save.inventory[0] == 4,
                "The highland station needs a real lit brazier before accepting materials");
            g.save.flags.Add("warm_brazier0");
            Action(g, heater.kind, "fieldstation_repair_7");
            Check(g.Has("fieldstation_7") && g.save.inventory[0] == 3 &&
                g.save.inventory[2] == 1 && g.NearActiveFieldStation(7, 4.2f),
                "Western brazier powers a local highland shelter at exact cost");
            g.save.cold = 60; g.Tick(.05f, new Point());
            Check(g.save.cold < 60, "The repaired shelter actually reduces cold exposure");
            g.save.x = 30; g.save.y = 14; g.save.cold = 60;
            g.Tick(.05f, new Point());
            Check(g.save.cold > 60, "Highland cold still accumulates outside shelter and braziers");
            g.save.x=heater.x;g.save.y=heater.y;
            Check(g.Has("fieldstation_complete") && g.GetQuest("stations").complete &&
                g.save.coins == oldCoins + 54 && g.save.shards == oldShards + 6 &&
                g.save.skillPoints == oldPoints + 1 && g.save.tea >= 2,
                "Three stations pay unique per-site rewards and the one-time final bonus");
            int coins = g.save.coins, skills = g.save.skillPoints;
            g.Talk(heater);
            Check(g.conversation != null &&
                !Array.Exists(g.conversation.choices, choice => choice.action == "fieldstation_repair_7"),
                "Finished shelter offers information, not duplicate construction");
            g.Close(); g.save.x=30;g.save.y=14; g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.GetQuest("stations").complete && restored.Has("fieldstation_7") &&
                restored.save.coins == coins && restored.save.skillPoints == skills &&
                restored.NearActiveFieldStation(7, 4.2f) == false,
                "Old-schema save retains rebuilt stations without inventing nearby-player state");
            restored.save.x = heater.x; restored.save.y = heater.y;
            Check(restored.NearActiveFieldStation(7, 4.2f),
                "The environment shelter works again when a player revisits after loading");
        }
        public static void FarshoreStationNetworkRewardsStayWithinOriginalSaveCaps(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report" })
                g.save.flags.Add(flag);
            g.save.coins = 999995; g.save.shards = 99998;
            g.save.skillPoints = 99; g.save.tea = 998;
            g.Enter(5); g.enemies.Clear();
            var harbor = Object(g, "fieldstation5");
            g.save.x = harbor.x; g.save.y = harbor.y;
            g.save.totalTime = 0;
            g.save.inventory[0] = 4; g.save.inventory[4] = 4;
            Action(g, harbor.kind, "fieldstation_repair_5");
            Check(g.Has("fieldstation_5") && g.save.coins == 999999 &&
                g.save.shards == 99999,
                "Real Mistport repair completes without exceeding v2 coin and shard limits");

            g.Enter(6); g.enemies.Clear();
            var desert = Object(g, "fieldstation6");
            g.save.x = desert.x; g.save.y = desert.y;
            g.save.inventory[1] = 4; g.save.inventory[4] = 4;
            g.save.flags.Add("mirrors_lit");
            Action(g, desert.kind, "fieldstation_repair_6");
            Check(g.Has("fieldstation_6") && g.save.coins == 999999 &&
                g.save.shards == 99999,
                "Real desert station remains repairable at maximum saved currency");

            g.Enter(7); g.enemies.Clear();
            var highland = Object(g, "fieldstation7");
            g.save.x = highland.x; g.save.y = highland.y;
            g.save.inventory[0] = 4; g.save.inventory[2] = 4;
            g.save.flags.Add("warm_brazier0");
            Action(g, highland.kind, "fieldstation_repair_7");
            Check(g.Has("fieldstation_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.tea == 999,
                "Actual three-station network completes with bounded currency, talent points, and tea");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.Has("fieldstation_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99 && restored.save.tea == 999,
                "Fully constructed network and every capped resource survive existing v2 save reload");
        }

        public static void FarshoreReliefConstructionCapsOneTimeMilestoneBonuses(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.save.frontierEnding = 1;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report",
                "fieldstation_6", "fieldstation_survey_6",
                "farshore_relief_5", "farshore_relief_7" })
                g.save.flags.Add(flag);
            g.Enter(6); g.enemies.Clear();
            var station = Object(g, "fieldstation6");
            g.save.x = station.x; g.save.y = station.y;
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99; g.save.tea = 998;
            g.save.inventory[1] = 4; g.save.inventory[2] = 3;
            Action(g, station.kind, "fieldstation_relief_6");
            Check(g.Has("farshore_relief_6") && g.Has("farshore_relief_complete") &&
                g.save.inventory[1] == 2 && g.save.inventory[2] == 2 &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.tea == 999,
                "Real final relief construction consumes materials, upgrades the live refuge, and bounds its completion bonuses");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("farshore_relief_complete") && loaded.save.coins == 999999 &&
                loaded.save.shards == 99999 && loaded.save.skillPoints == 99 &&
                loaded.save.tea == 999,
                "Farshore civic upgrade and its cap-safe rewards persist through a full save roundtrip");
        }

    }
}
