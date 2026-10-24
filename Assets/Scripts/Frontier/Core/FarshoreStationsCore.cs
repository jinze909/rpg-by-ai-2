using System;
using System.Collections.Generic;

namespace Starfall
{
    // Optional world stations: the restored objects alter actual tide and climate.
    // All ownership uses existing save flags; no new serialized state is required.
    public sealed partial class GameCore
    {
        // Out-and-back environmental field surveys; live progress never enters the save schema.
        public int fieldSurveyRoom, fieldSurveyPhase;
        public float fieldSurveyTime;
        private bool fieldSurveyConditionWarned;
        private static readonly string[] FieldSurveyLandmarks = { "relay1", "receiver", "bell2" };
        public Point FieldSurveyTarget(int region)
        {
            if (region < 5 || region > 7) return new Point();
            return ObjectPoint(region, FieldSurveyLandmarks[region - 5], new Point(18, 14));
        }
        private void ResetFieldStationSurvey()
        {
            fieldSurveyRoom = fieldSurveyPhase = 0; fieldSurveyTime = 0;
            fieldSurveyConditionWarned = false;
        }
        private void TickFieldStationSurvey(float dt)
        {
            if (fieldSurveyRoom == 0) return;
            int region = fieldSurveyRoom;
            if (save.room != region || Dying || practicing) { ResetFieldStationSurvey(); return; }
            fieldSurveyTime -= dt;
            if (fieldSurveyTime <= 0)
            {
                ResetFieldStationSurvey(); revision++;
                Notify("巡测时间已到。观测数据失效，可以从站台重新出发。",
                    "Survey timed out. Return to the station to try again.");
                return;
            }
            if (fieldSurveyPhase == 1 && (Position - FieldSurveyTarget(region)).Length < 1.4f)
            {
                bool readable = region == 5 ? LowTide : region == 6 ? save.heat < 60 : save.cold < 60;
                if (!readable)
                {
                    if (!fieldSurveyConditionWarned)
                    {
                        Notify(region == 5 ? "水位过高，等待低潮再记录信号。" :
                            region == 6 ? "热浪干扰读数，先到阴凉处降温。" :
                            "寒意干扰读数，先靠近火盆或保暖站。",
                            region == 5 ? "The marker is submerged. Wait for low tide." :
                            region == 6 ? "Too much heat for a clear reading. Cool down first." :
                            "Too cold for a clear reading. Warm up first.");
                        fieldSurveyConditionWarned = true;
                    }
                    return;
                }
                fieldSurveyPhase = 2; revision++; sound = "quest";
                effects.Add(new Effect("discover", FieldSurveyTarget(region), .65f));
                Notify("远岸信号已记录！在倒计时结束前返回观测站。",
                    "Field reading secured! Return to the station before time runs out.");
            }
            if (fieldSurveyPhase != 2) return;
            foreach (WorldObject o in Map.objects)
            {
                if (o.kind != "fieldstation" + region ||
                    (Position - new Point(o.x, o.y)).Length >= 1.55f) continue;
                ResetFieldStationSurvey();
                if (Has("fieldstation_survey_" + region)) return;
                Flag("fieldstation_survey_" + region);
                CreditCoins(24); CreditShards(2); AddXp(30);
                if (Has("fieldstation_survey_5") && Has("fieldstation_survey_6") &&
                    Has("fieldstation_survey_7") && !Has("fieldstation_survey_complete"))
                {
                    Flag("fieldstation_survey_complete"); CreditTea(2);
                    RecordMoment("三份风暴巡测数据已归档。远岸避风网可以预警下一场风暴。",
                        "Three field readings archived. The farshore network can forecast the next storm.");
                    Notify("三地巡测完成！获得24硬币、2碎片、30经验和额外凉茶2。",
                        "All field surveys complete! +24 coins, +2 shards, +30 XP, +2 bonus teas.");
                }
                else Notify("巡测报告已归档！获得24硬币、2碎片、30经验。",
                    "Field survey archived! +24 coins, +2 shards, +30 XP.");
                TrackQuest("station_patrol"); revision++; sound = "reward";
                return;
            }
        }
        public bool NearActiveFieldStation(int region, float radius)
        {
            if (save.room != region || !Has("fieldstation_" + region)) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "fieldstation" + region &&
                    PhysicalRefugeReach(new Point(o.x, o.y),radius))
                    return true;
            return false;
        }
        // One specific repaired weather station protects only feet on its
        // real visible side of the terrain. This is also the single source
        // for accurate field-radius status in the production world renderer.
        public bool FieldStationSheltersHero(WorldObject site)
        {
            int region=save.room;
            return (region==6 || region==7) && site!=null &&
                site.kind=="fieldstation"+region &&
                Has("fieldstation_"+region) &&
                PhysicalRefugeReach(new Point(site.x,site.y),
                    FieldStationRefugeRadius(region));
        }
        // Relief upgrades use persistent string flags already supported by old saves.
        // A finished local survey supplies the measurements needed to site each refuge.
        public float FieldStationRefugeRadius(int region)
        {
            return region >= 6 && region <= 7 && Has("farshore_relief_" + region) ? 6.5f : 4.2f;
        }
        public int TidewatchHoldSeconds { get { return (Has("farshore_relief_5") ? 90 : 45) + (Has("tide_salvage_complete") ? 15 : 0); } }
        // A timed, two-wave rescue sortie from each real civilian shelter.
        // The route and enemies are live combat; replay gives no further rewards.
        public int reliefMissionRoom, reliefMissionWave;
        public float reliefMissionTime;
        public Point reliefFollowerPosition;
        public float reliefFollowerSafety;
        public float reliefCivilianHitTime;
        public bool ReliefCivilianVisible
        {
            get { return reliefMissionRoom == save.room &&
                reliefMissionWave >= 1 && reliefMissionWave <= 3; }
        }
        public float reliefWardTime;
        private bool reliefWardUsed, reliefTimeWarning, reliefSafetyWarning;
        public float ReliefCombatThreatScale
        {
            get { return save.difficulty == 0 ? .7f : save.difficulty == 2 ? 1.3f : 1f; }
        }
        public bool ReliefFieldGuardAvailable
        {
            get { return reliefMissionRoom == save.room && (reliefMissionWave == 1 ||
                reliefMissionWave == 2) && reliefWardTime <= 0 && !reliefWardUsed &&
                save.energy >= 20 && !Dying; }
        }
        // Distance-driven, transient gait data; the NPC keeps one consistent
        // authored appearance and never borrows the playable hero sprite.
        public float reliefFollowerWalkDistance;
        public bool reliefFollowerMoving, reliefFollowerFacingLeft;
        private float reliefFollowerBlockedSeconds;
        private bool reliefFollowerBlockedNotice;
        // Reuse a complete transient footpath. Never store it in SaveData.
        private readonly List<Point> reliefFollowerRoute = new List<Point>();
        private int reliefFollowerRouteIndex;
        private Point reliefFollowerRouteTarget;
        private float reliefFollowerRouteRefresh;
        private void ClearReliefFollowerRoute()
        {
            reliefFollowerRoute.Clear();
            reliefFollowerRouteIndex = 0;
            reliefFollowerRouteRefresh = 0;
        }
        // A projectile's thin center ray is not a civilian's body.
        // Sample the actual footprint before attempting direct movement or
        // trusting a cached waypoint beside a newly closed wall.
        private bool ReliefActorLineWalkable(Point from, Point to)
        {
            if (!CanStand(from) || !CanStand(to)) return false;
            Point delta = to - from;
            int samples = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
            for (int i=1; i<=samples; i++)
                if (!CanStand(from + delta * (i / (float)samples))) return false;
            return true;
        }
        public bool ReliefEscortRouteStalled
        {
            get { return ReliefEscortActive && !reliefFollowerWaiting &&
                reliefFollowerBlockedSeconds >= 1.25f; }
        }
        public bool reliefFollowerWaiting, reliefMedicalUsed;
        public float reliefClimateKitTime;
        private bool reliefClimateKitUsed;
        public bool ReliefClimateKitAvailable
        {
            get { return ReliefEscortActive && !reliefClimateKitUsed &&
                save.shards >= 1 && !Dying; }
        }
        public float reliefHurryTime;
        private bool reliefHurryUsed;
        private float reliefEvadeCooldown;
        public bool ReliefEscortEvadeAvailable
        {
            get { return ReliefEscortActive && !Dying &&
                save.energy>=15 && reliefEvadeCooldown<=0f &&
                ReliefEscortUnderAttack; }
        }
        public bool ReliefEscortHurryAvailable
        {
            get { return ReliefEscortActive && !reliefHurryUsed &&
                save.energy >= 15 && !Dying; }
        }
        public bool ReliefEscortActive { get { return reliefMissionRoom != 0 && reliefMissionWave == 3; } }
        public bool ReliefEscortGuardAvailable
        {
            get { return ReliefEscortActive && reliefWardTime <= 0 &&
                !reliefWardUsed && save.energy >= 20 && !Dying; }
        }
        public float ReliefEscortGap { get { return ReliefEscortActive ?
            (Position - reliefFollowerPosition).Length : 0; } }
        public bool ReliefEscortUnderAttack
        {
            get
            {
                if (!ReliefEscortActive || reliefMissionRoom!=save.room ||
                    reliefMissionTime<=0f || reliefFollowerSafety<=0f)
                    return false;
                // Share the real contact geometry with companion interception
                // and combat warning HUD. Stale attack flags, a backward
                // swing and a shut gate cannot freeze the actual evacuee.
                foreach (Enemy enemy in enemies)
                    if (CompanionCommittedRescueStrike(enemy)) return true;
                return false;
            }
        }
        private static bool IsReliefMissionEnemy(Enemy e)
        { return e != null && e.id >= 85000 && e.id < 85100; }
        private void ResetReliefMission()
        {
            // Explicitly discard the optional checkpoint on actual completion,
            // defeat, abandonment or a new-game reset.
            if (save != null) save.rescueCheckpoint = null;
            enemies.RemoveAll(IsReliefMissionEnemy);
            // Ordinary patrols temporarily redirect attacks toward an escort.
            // Restore their normal AI on abort or completion so no old
            // civilian-locked windup survives this rescue.
            foreach (Enemy enemy in enemies)
                if (enemy.targetingReliefCivilian)
                {
                    enemy.targetingReliefCivilian = false;
                    enemy.state = 0;
                    enemy.timer = Math.Max(enemy.timer, .45f);
                }
            projectiles.RemoveAll(p => p.reliefMissionOwned);
            effects.RemoveAll(e => e.reliefMissionOwned);
            reliefMissionRoom = reliefMissionWave = 0; reliefMissionTime = 0;
            reliefFollowerPosition = new Point(); reliefFollowerSafety = 0;
            reliefCivilianHitTime = 0;
            reliefWardTime = 0; reliefWardUsed = false;
            reliefTimeWarning = reliefSafetyWarning = false;
            reliefFollowerWalkDistance = 0; reliefFollowerMoving = false;
            reliefFollowerFacingLeft = false;
            reliefFollowerBlockedSeconds = 0; reliefFollowerBlockedNotice = false;
            ClearReliefFollowerRoute();
            reliefFollowerWaiting = reliefMedicalUsed = false;
            reliefHurryTime = 0; reliefHurryUsed = false;
            reliefEvadeCooldown = 0;
            reliefClimateKitTime = 0; reliefClimateKitUsed = false;
        }
        private void CaptureReliefCheckpoint()
        {
            // Combat waves require exact live enemy state; do not restore a
            // fake victory. Only the safely reached escort leg can be resumed.
            if (!ReliefEscortActive || save.room != reliefMissionRoom ||
                Dying || !CanStand(reliefFollowerPosition))
            { save.rescueCheckpoint = null; return; }
            save.rescueCheckpoint = new RescueCheckpoint {
                room = reliefMissionRoom, x = reliefFollowerPosition.x,
                y = reliefFollowerPosition.y,
                time = reliefMissionTime, safety = reliefFollowerSafety,
                waiting = reliefFollowerWaiting, treated = reliefMedicalUsed,
                escortGuardUsed = reliefWardUsed, hurryUsed = reliefHurryUsed,
                climateKitUsed = reliefClimateKitUsed
            };
        }
        private void RestoreReliefCheckpoint(RescueCheckpoint checkpoint)
        {
            if (checkpoint == null) return;
            // A malformed/foreign/expired checkpoint cannot inject a civilian
            // in town, an inaccessible map or a missing construction project.
            if (checkpoint.room != save.room || checkpoint.room < 5 ||
                checkpoint.room > 7 || !Has("farshore_relief_" + checkpoint.room) ||
                save.frontierEnding == 0 ||
                !Finite(checkpoint.x) || !Finite(checkpoint.y) ||
                !Finite(checkpoint.time) || checkpoint.time <= 0 ||
                checkpoint.time > 120 || !Finite(checkpoint.safety) ||
                checkpoint.safety <= 0 || checkpoint.safety > 100) return;
            Point savedFoot = new Point(checkpoint.x, checkpoint.y);
            if (!CanStand(savedFoot))
            {
                // A saved Mistport shoal can become flooded when tide locks
                // expire. Only actual authored '%' shoals may use the same
                // short-range bank recovery as live actors; arbitrary walls
                // and invalid coordinates are rejected, not teleported.
                int tx = (int)Math.Floor(savedFoot.x), ty = (int)Math.Floor(savedFoot.y);
                if (checkpoint.room != 5 || LowTide ||
                    tx < 2 || ty < 2 || tx >= Map.rows[0].Length - 2 ||
                    ty >= Map.rows.Length - 2 || Map.rows[ty][tx] != '%')
                    return;
                Point dryBank = SafeActorFoot(savedFoot);
                if ((dryBank - savedFoot).Length > 4.5f) return;
                savedFoot = dryBank;
            }
            reliefMissionRoom = checkpoint.room; reliefMissionWave = 3;
            reliefMissionTime = checkpoint.time; reliefFollowerSafety = checkpoint.safety;
            reliefFollowerPosition = savedFoot;
            reliefFollowerWaiting = checkpoint.waiting;
            reliefMedicalUsed = checkpoint.treated;
            reliefWardUsed = checkpoint.escortGuardUsed;
            reliefHurryUsed = checkpoint.hurryUsed;
            reliefClimateKitUsed = checkpoint.climateKitUsed;
            reliefClimateKitTime = 0;
            // The one-time burst stays spent after reload, but cannot be
            // extended by reloading at the end of its original duration.
            reliefHurryTime = 0;
            // Never bring a short-term shield back at full duration by
            // repeatedly saving and reloading.
            reliefWardTime = 0;
            reliefTimeWarning = reliefMissionTime <= 30;
            reliefSafetyWarning = reliefFollowerSafety <= 30;
            reliefFollowerMoving = false; reliefFollowerWalkDistance = 0;
            ClearReliefFollowerRoute();
            revision++;
        }
        private bool SpawnReliefMissionWave()
        {
            Point landmark = FieldSurveyTarget(reliefMissionRoom);
            var spots = new List<Point>();
            for (int radius = 2; radius <= 6 && spots.Count < 2; radius++)
            for (int dx = -radius; dx <= radius && spots.Count < 2; dx++)
            for (int dy = -radius; dy <= radius && spots.Count < 2; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                Point candidate = landmark + new Point(dx, dy);
                if (!CanStand(candidate) || NavigationPath(candidate).Count == 0 ||
                    (candidate - landmark).Length > 6.5f ||
                    (candidate - Position).Length < 2f ||
                    enemies.Exists(e => (e.Position - candidate).Length < 1.8f) ||
                    spots.Exists(p => (p - candidate).Length < 3) ) continue;
                spots.Add(candidate);
            }
            if (spots.Count != 2) return false;
            for (int i = 0; i < 2; i++)
            {
                int kind = reliefMissionWave == 1 ? i == 0 ? 0 : 1 : i == 0 ? 2 : 0;
                var enemy = new Enemy(85000 + (reliefMissionRoom - 5) * 10 +
                    reliefMissionWave * 2 + i, kind, spots[i].x, spots[i].y,
                    65 + (reliefMissionRoom - 5) * 18 + (reliefMissionWave - 1) * 32);
                enemy.timer = .9f + .25f * i;
                enemies.Add(enemy);
                effects.Add(new Effect("discover", spots[i], .65f) {
                    reliefMissionOwned = true });
            }
            revision++; sound = "quest";
            return true;
        }
        private void BeginReliefMission()
        {
            int region = save.room;
            if (region < 5 || region > 7 || reliefMissionRoom != 0 ||
                !Has("farshore_relief_" + region) || !Has("fieldstation_survey_" + region) ||
                FieldWorkPreventsRest || fieldSurveyRoom != 0 || roadTrialRoom != 0 || wayfarerEncounterRoom != 0 ||
                atlasEchoRoom != 0 || practicing || Dying || racing || !CanEnter(region)) return;
            Point post = ObjectPoint(region, "fieldstation" + region, new Point(-100, -100));
            if (!PhysicalRefugeReach(post,2.2001f)) return;
            if (!SafeTravel)
            { Notify("先清理站台附近的巡逻敌人，才能启动救援。", "Clear patrols near the station before starting rescue."); return; }
            // Stage zero is the outbound journey. Do not let raiders chase the
            // hero from the station before anyone reaches the stranded civilians.
            reliefMissionRoom = region; reliefMissionWave = 0; reliefMissionTime = 120;
            TrackQuest("relief_rescue"); revision++; sound = "quest";
            Notify("已接取撤离：先抵达巡测标记，找到居民后拦截伏击者；120秒内完成往返。",
                "Rescue underway: first reach the survey marker and locate civilians. Return within 120 seconds.");
        }
        // Environmental protection follows the survivor's real feet, never
        // the hero's coordinates. Repaired field shelters and the original
        // oasis shade / lit highland braziers are meaningful rescue landmarks.
        // Survivor climate protection must originate at the CIVILIAN feet.
        // Reusing the hero's NearKind/PhysicalRefugeReach would make shelter
        // follow the wrong person; radius alone grants fake warmth/cooling
        // through sealed rock. The original near-side margin lets residents
        // take cover behind a wall-mounted field station from its open side.
        private bool ReliefSurvivorCanReach(Point site,float range)
        {
            Point diff=site-reliefFollowerPosition;
            float distance=diff.Length;
            if(!Finite(distance)||distance>=range)return false;
            Point contact=distance>.55f?
                site-diff.Normalized*.55f:reliefFollowerPosition;
            return ClearAttackPath(reliefFollowerPosition,contact);
        }
        private bool ReliefSurvivorSheltered(int region)
        {
            if(reliefMissionRoom!=region || save.room!=region ||
                reliefMissionWave!=3 || Map.objects==null)return false;
            // Tide is a real regional environmental refuge even before the
            // civil station is restored. A dormant station grants nothing.
            if(region==5 && LowTide)return true;
            foreach(WorldObject o in Map.objects)
            {
                if(o==null)continue;
                Point p=new Point(o.x,o.y);
                if(o.kind=="fieldstation"+region &&
                    Has("fieldstation_"+region) &&
                    Has("farshore_relief_"+region) &&
                    ReliefSurvivorCanReach(p,region==5?4.2f:
                        FieldStationRefugeRadius(region)))return true;
                if(region==6&&o.kind=="shade"&&
                    ReliefSurvivorCanReach(p,3f))return true;
                if(region==7&&o.kind=="brazier"&&
                    Has("warm_"+o.id)&&
                    ReliefSurvivorCanReach(p,3.5f))return true;
            }
            return false;
        }
        public bool ReliefEscortSheltered
        {
            get { return ReliefEscortActive && save.room==reliefMissionRoom &&
                ReliefSurvivorSheltered(reliefMissionRoom); }
        }
        // Waiting at a genuinely reachable shelter is a tactical decision
        // during the timed rescue, not an indefinite healing cheat. Every
        // second spent here consumes the same dwindling mission timer.
        // Enemy patrols within four tiles or a committed civilian warning
        // prevent the faster calm recovery, even with a green field canopy.
        // Near a completed rescue objective, terrain may still separate
        // either living actor from the station. Show why the last handoff
        // has not succeeded instead of silently ignoring two nearby bodies.
        public bool ReliefEscortReturnObstructed
        {
            get
            {
                if(!ReliefEscortActive || reliefMissionRoom!=save.room)
                    return false;
                Point station=ObjectPoint(save.room,
                    "fieldstation"+save.room,new Point(-100,-100));
                return (Position-station).Length<1.6f &&
                    (reliefFollowerPosition-station).Length<2f &&
                    (!HasPhysicalInteractionReach(station) ||
                        !ReliefSurvivorCanReach(station,2f));
            }
        }
        public bool ReliefEscortSecureStop
        {
            get
            {
                if(!ReliefEscortSheltered || !reliefFollowerWaiting ||
                    ReliefEscortUnderAttack || Dying)return false;
                foreach(Enemy enemy in enemies)
                    if(enemy.hp>0 &&
                        (enemy.Position-reliefFollowerPosition).Length<4f)
                        return false;
                return true;
            }
        }
        // A tactical rally is an actual field command, not an inventory click
        // or free mission reset. It protects civilians for a short period, once
        // per wave, using the hero's existing combat energy.
        private void TalkReliefFieldGuard()
        {
            if (reliefMissionRoom != save.room ||
                (reliefMissionWave != 1 && reliefMissionWave != 2) ||
                ((Position - reliefFollowerPosition).Length > 1.7f ||
                 !ClearAttackPath(Position,reliefFollowerPosition))) return;
            var options = new List<Choice>();
            if (ReliefFieldGuardAvailable)
                options.Add(C("组织紧急护卫 · 耐力20", "Rally defenders · 20 energy",
                    "relief_field_guard"));
            options.Add(C("继续守护", "Keep defending", "close"));
            Dialog("平民紧急集结点", "Civilian rally point",
                "幸存者仍被围困。你可以组织一次紧急护卫，持续8秒减少他们受到的攻击与破坏伤害。每波只能使用一次，且不会免疫伤害。\n\n当前安全度 " +
                    (int)Math.Ceiling(reliefFollowerSafety) + "% · 第" + reliefMissionWave + "/2波。",
                "Civilians are still under siege. Rally a field guard for 8 seconds to reduce attacks and sabotage. Once per wave; this does not grant immunity.\n\nSafety " +
                    (int)Math.Ceiling(reliefFollowerSafety) + "% · wave " + reliefMissionWave + "/2.",
                options.ToArray());
        }
        private bool ChooseReliefFieldGuard(string action)
        {
            if (action != "relief_field_guard") return false;
            if (!ReliefFieldGuardAvailable ||
                ((Position - reliefFollowerPosition).Length > 1.7f ||
                 !ClearAttackPath(Position,reliefFollowerPosition)))
                return true;
            save.energy -= 20; reliefWardTime = 8f; reliefWardUsed = true;
            effects.Add(new Effect("discover", reliefFollowerPosition, .7f));
            sound = "guard"; revision++;
            Notify("临时护卫队就位！8秒内减轻居民安全度损失。",
                "Field guard rallied! Civilian safety damage is reduced for 8 seconds.");
            return true;
        }
        // The committed windup/release follows the existing combat timeline.
        // Only a raider that is still in reach and has line of sight can
        // injure civilians; the hero can step between them and take the blow.
        private bool InterceptReliefCivilianStrike(Enemy attacker, Point civilian)
        {
            Point lane = civilian - attacker.Position;
            float distance = lane.Length;
            if (distance < .45f) return false;
            Point blocker = Position - attacker.Position;
            float radius = attacker.kind == 2 ? EnemyAreaRadius(attacker) : EnemyMeleeRadius;
            if (blocker.Length >= radius || !ClearAttackPath(attacker.Position, Position))
                return false;
            float along = (blocker.x * lane.x + blocker.y * lane.y) / distance;
            float across = Math.Abs(blocker.x * lane.y - blocker.y * lane.x) / distance;
            if (along < .2f || along > distance + .1f || across > .75f)
                return false;
            if (attacker.kind == 2 ?
                (Position - attacker.target).Length >= radius :
                !InEnemyMeleeArc(attacker, Position)) return false;
            effects.Add(new Effect("number", Position, .7f, L("护卫拦截", "INTERCEPT")));
            effects.Add(new Effect("discover", civilian, .35f));
            return true;
        }
        private void DamageReliefCivilianArrow(Projectile shot)
        {
            if (!shot.reliefMissionOwned || reliefMissionRoom == 0 ||
                (reliefMissionWave < 1 || reliefMissionWave > 3) ||
                save.room != reliefMissionRoom) return;
            Point civilians = reliefFollowerPosition;
            float loss = 9f * ReliefCombatThreatScale *
                (reliefWardTime > 0 ? .55f : 1f);
            reliefFollowerSafety = Math.Max(0, reliefFollowerSafety - loss);
            reliefCivilianHitTime = .24f;
            effects.Add(new Effect("projectileImpact", civilians, .28f) {
                direction = shot.velocity.Normalized, variant = 1,
                height = shot.height, reliefMissionOwned = true });
            effects.Add(new Effect("number", civilians, .7f,
                "-" + (int)Math.Ceiling(loss) + L("安全", " SAFETY")) {
                    reliefMissionOwned = true });
            revision++;
            if (reliefFollowerSafety <= 0)
            {
                ResetReliefMission(); sound = "deny";
                Notify("流矢击散了被困居民！请在下一次救援中挡住射线。",
                    "Stray arrows scattered the civilians. Block their flight paths on the next attempt.");
            }
        }
        private void DamageReliefCivilian(Enemy attacker)
        {
            if (reliefMissionRoom == 0 || reliefMissionWave < 1 ||
                reliefMissionWave > 3 || save.room != reliefMissionRoom ||
                attacker.boss || attacker.kind == 1 ||
                (reliefMissionWave == 3 ? IsReliefMissionEnemy(attacker) :
                    !IsReliefMissionEnemy(attacker))) return;
            Point civilians = reliefFollowerPosition;
            if (!Finite(civilians.x)||!Finite(civilians.y)||
                !Finite(attacker.x)||!Finite(attacker.y)||
                !CanStand(attacker.Position)||!CanStand(civilians))
                return;
            // Match the EXACT contact used for the living hero: a heavy
            // damages inside its telegraphed, LOCKED impact circle, not an
            // unrelated disk around the enemy's current boots. A melee
            // raider uses its real forward arc, never an omnidirectional
            // 2.1-tile aura behind its weapon.
            if (attacker.kind==2 ?
                (!Finite(attacker.target.x)||!Finite(attacker.target.y)||
                 (civilians-attacker.target).Length>=EnemyAreaRadius(attacker)) :
                !InEnemyMeleeArc(attacker,civilians))
                return;
            if (!ClearAttackPath(attacker.Position,civilians) ||
                InterceptReliefCivilianStrike(attacker,civilians))return;
            float loss = (attacker.kind == 2 ? 22f : 14f) * ReliefCombatThreatScale;
            if (reliefWardTime > 0) loss *= .55f;
            reliefFollowerSafety = Math.Max(0, reliefFollowerSafety - loss);
            reliefCivilianHitTime = .24f;
            effects.Add(new Effect("danger", civilians, .45f) {
                radius = .7f, reliefMissionOwned = true });
            effects.Add(new Effect("number", civilians, .75f,
                "-" + (int)loss + L("安全", " SAFETY")) {
                    reliefMissionOwned = true });
            revision++;
            if (reliefFollowerSafety <= 0)
            {
                ResetReliefMission(); sound = "deny";
                Notify("掠夺者击散了被困居民！可以免费重新组织撤离。",
                    "Raiders overwhelmed the civilians! Retry the rescue for free.");
            }
            else if (reliefFollowerSafety <= 30)
                Notify("居民安全度极低！拦截敌人的下一次蓄力攻击。",
                    "Civilian safety critical! Intercept the next telegraphed strike.");
        }
        private void TickReliefMission(float dt)
        {
            if (reliefMissionRoom == 0) return;
            int region = reliefMissionRoom;
            reliefCivilianHitTime = Math.Max(0, reliefCivilianHitTime - dt);
            if (save.room != region || Dying || practicing)
            { ResetReliefMission(); return; }
            reliefMissionTime -= dt;
            if (reliefWardTime > 0)
                reliefWardTime = Math.Max(0, reliefWardTime - dt);
            if (reliefHurryTime > 0)
                reliefHurryTime = Math.Max(0, reliefHurryTime - dt);
            if (reliefEvadeCooldown > 0)
                reliefEvadeCooldown = Math.Max(0, reliefEvadeCooldown - dt);
            if (reliefClimateKitTime > 0)
                reliefClimateKitTime = Math.Max(0, reliefClimateKitTime - dt);
            if (reliefMissionTime <= 0)
            {
                ResetReliefMission(); revision++;
                Notify("救援时间已过。未消耗物资，可再次行动。",
                    "Rescue timed out. No supplies lost; retry freely.");
                return;
            }
            if (reliefMissionTime <= 30 && !reliefTimeWarning)
            {
                reliefTimeWarning = true;
                sound = "quest";
                Notify("撤离倒计时不足30秒！立即完成目标并返回站台。",
                    "Less than 30 seconds remain! Finish the rescue and return now.");
            }
            if (reliefFollowerSafety > 0 && reliefFollowerSafety <= 30 &&
                !reliefSafetyWarning && reliefMissionWave >= 1)
            {
                reliefSafetyWarning = true;
                sound = "deny";
                Point endangered = reliefFollowerPosition;
                effects.Add(new Effect("danger", endangered, .65f) { radius = .8f });
                Notify("居民安全度危险！拦截攻击、利用护卫或寻找避难处。",
                    "Civilian safety critical! Intercept attacks, rally guards or find shelter.");
            }
            if (reliefMissionWave == 0)
            {
                // Reaching the actual field site is now mandatory. A hostile
                // wave cannot be cleared remotely from the station.
                if ((Position - FieldSurveyTarget(region)).Length > 2.1f) return;
                reliefMissionWave = 1;
                // Reveal an actual rescued resident at a collision-safe spot
                // during the battle, not an invisible safety meter. The same
                // actor continues into the follow-home stage.
                reliefFollowerPosition = SafeActorFoot(FieldSurveyTarget(region));
                reliefFollowerSafety = 100;
                if (!SpawnReliefMissionWave())
                {
                    ResetReliefMission(); revision++;
                    Notify("伏击地点暂时没有足够的安全空间，可以回站台免费重试。",
                        "The ambush cannot spawn safely here. Retry from the station for free.");
                    return;
                }
                Notify("找到失联居民！第一波掠夺者逼近，保护他们并击退伏击。",
                    "Civilians located! First raider wave incoming. Defeat the ambush to reach them.");
                return;
            }
            if (reliefMissionWave == 1 || reliefMissionWave == 2)
            {
                Point civilians = reliefFollowerPosition;
                float pressure = 0;
                foreach (Enemy raider in enemies)
                    if (IsReliefMissionEnemy(raider) && raider.hp > 0 &&
                        raider.stun <= 0 && raider.hitTime <= 0 &&
                        raider.state != 1 && raider.releaseTime <= 0 &&
                        (raider.Position - civilians).Length < 1.75f &&
                        ClearAttackPath(raider.Position, civilians))
                        pressure += raider.kind == 2 ? 1.4f : 1f;
                if (pressure > 0)
                {
                    float cover = (Position - civilians).Length < 2.2f ? .65f : 1f;
                    reliefFollowerSafety = Math.Max(0,
                        reliefFollowerSafety - dt * pressure * 1.2f * cover *
                            ReliefCombatThreatScale * (reliefWardTime > 0 ? .55f : 1f));
                    if (reliefFollowerSafety <= 0)
                    {
                        ResetReliefMission(); revision++; sound = "deny";
                        effects.Add(new Effect("danger", civilians, .6f) { radius = 1 });
                        Notify("掠夺者冲散了被困居民！可以重新组织撤离，无需支付物资。",
                            "Raiders scattered the civilians! Reorganize the evacuation for free.");
                        return;
                    }
                }
            }
            if (enemies.Exists(IsReliefMissionEnemy)) return;
            if (reliefMissionWave == 1)
            {
                projectiles.RemoveAll(p => p.reliefMissionOwned);
                effects.RemoveAll(e => e.reliefMissionOwned);
                reliefMissionWave = 2;
                reliefWardTime = 0; reliefWardUsed = false;
                reliefFollowerSafety = Math.Min(100, reliefFollowerSafety + 12);
                if (!SpawnReliefMissionWave())
                {
                    ResetReliefMission(); revision++;
                    Notify("道路发生变化，救援取消，可免费重试。",
                        "The route changed. Rescue canceled; retry for free.");
                    return;
                }
                Notify("第二波来了！重甲破坏者与突袭者正封锁撤离道路！",
                    "Second wave! An armored breaker and raider block the evacuation road!");
                return;
            }
            if (reliefMissionWave == 2)
            {
                // The second ambush is over. Do not let an already-defeated
                // raider's delayed arrow hit the civilians during escort.
                // Keep unrelated world shots and effects fully intact.
                projectiles.RemoveAll(p => p.reliefMissionOwned);
                effects.RemoveAll(e => e.reliefMissionOwned);
                reliefMissionWave = 3; revision++; sound = "quest";
                // Escort is a distinct tactical leg: one additional
                // limited-duration rally opportunity after defending wave two.
                reliefWardTime = 0; reliefWardUsed = false;
                // Authored markers are prop positions, not guaranteed actor feet
                // on every tide phase. Spawn survivors on real walkable ground.
                // Keep the same visible civilian through the battle and
                // return journey. Recheck footing against any tide change.
                reliefFollowerPosition = SafeActorFoot(reliefFollowerPosition);
                reliefFollowerWalkDistance = 0; reliefFollowerMoving = false;
                reliefFollowerFacingLeft = false;
                reliefFollowerBlockedSeconds = 0; reliefFollowerBlockedNotice = false;
                ClearReliefFollowerRoute();
                reliefFollowerWaiting = reliefMedicalUsed = false;
                reliefHurryTime = 0; reliefHurryUsed = false;
                reliefClimateKitTime = 0; reliefClimateKitUsed = false;
                // Carry battle losses forward; winning does not reset civilians
                // to full protection for the dangerous walk home.
                reliefFollowerSafety = Math.Min(100, reliefFollowerSafety + 12);
                Point marker = FieldSurveyTarget(region);
                effects.Add(new Effect("discover", marker, .7f));
                Notify("掠夺者已击退！带领幸存者在剩余时间内返回避难站。",
                    "Raiders defeated! Guide the survivors back to the shelter before time expires.");
            }
            if (reliefMissionWave != 3) return;
            // Ordinary escort patrols now threaten residents through their
            // actual committed enemy windup/release and swept projectiles.
            // No invisible per-frame proximity damage. Weather continues to
            // affect civilians independently below.
            // Real evacuees follow at human pace, constrained by actual
            // collision rather than teleporting to the hero or the station.
            Point follow = Position - reliefFollowerPosition;
            Point beforeWalk = reliefFollowerPosition;
            reliefFollowerMoving = false;
            // A resident previously walked straight into every real corner.
            // A nearby hero on the OTHER side of a wall is not a safe arrival.
            bool obstructed = !ReliefActorLineWalkable(reliefFollowerPosition, Position);
            reliefFollowerRouteRefresh = Math.Max(0, reliefFollowerRouteRefresh - dt);
            if (!reliefFollowerWaiting && !ReliefEscortUnderAttack &&
                reliefCivilianHitTime <= 0 && (follow.Length > 1.3f || obstructed))
            {
                // Visible committed strikes still stop movement. Route around
                // only actual terrain/gate obstacles using the same footprint
                // pathfinder as the world navigation. Every step remains
                // swept through collision: waypoints are NOT teleports.
                float walk = follow.Length > 5.5f ? 3.5f : 2.6f;
                if (reliefHurryTime > 0) walk += 1.7f;
                Point destination = Position;
                if (obstructed)
                {
                    // Step through the entire collision-valid detour without
                    // restarting full-map search at each tile boundary.
                    while (reliefFollowerRouteIndex < reliefFollowerRoute.Count &&
                        (reliefFollowerRoute[reliefFollowerRouteIndex] -
                         reliefFollowerPosition).Length < .18f)
                        reliefFollowerRouteIndex++;
                    bool hasWaypoint = reliefFollowerRouteIndex < reliefFollowerRoute.Count;
                    bool replan = reliefFollowerRouteRefresh <= 0 ||
                        (Position - reliefFollowerRouteTarget).Length > 1.4f ||
                        (reliefFollowerRoute.Count > 0 && !hasWaypoint) ||
                        (hasWaypoint && !ReliefActorLineWalkable(reliefFollowerPosition,
                            reliefFollowerRoute[reliefFollowerRouteIndex]));
                    if (replan)
                    {
                        List<Point> route = NavigationPathFrom(reliefFollowerPosition,
                            Position, true);
                        reliefFollowerRoute.Clear();
                        reliefFollowerRoute.AddRange(route);
                        reliefFollowerRouteIndex = 0;
                        reliefFollowerRouteTarget = Position;
                        // Poll genuinely sealed areas but never freeze a
                        // removed wall or reopened gate into route state.
                        reliefFollowerRouteRefresh = route.Count > 0 ? .65f : .35f;
                    }
                    destination = reliefFollowerRouteIndex < reliefFollowerRoute.Count ?
                        reliefFollowerRoute[reliefFollowerRouteIndex] : reliefFollowerPosition;
                }
                else ClearReliefFollowerRoute();
                Point toward = destination - reliefFollowerPosition;
                float step = obstructed ? dt * walk :
                    Math.Min(dt * walk, Math.Max(0, follow.Length - 1.3f));
                Point advance = toward.Length > .001f ?
                    toward.Normalized * Math.Min(step, toward.Length) : new Point();
                reliefFollowerPosition = SweepActor(reliefFollowerPosition, advance);
            }
            Point traveled = reliefFollowerPosition - beforeWalk;
            // A wall must not make the survivor walk in place. Record only
            // physical distance, with a stable horizontal orientation.
            reliefFollowerMoving = traveled.Length > .001f;
            bool shouldFollow=!reliefFollowerWaiting && !ReliefEscortUnderAttack &&
                reliefCivilianHitTime<=0 && (follow.Length>2.3f || obstructed);
            if(shouldFollow && !reliefFollowerMoving)
            {
                reliefFollowerBlockedSeconds=Math.Min(4f,
                    reliefFollowerBlockedSeconds+dt);
                if(reliefFollowerBlockedSeconds>=1.25f &&
                    !reliefFollowerBlockedNotice)
                {
                    reliefFollowerBlockedNotice=true;
                    sound="deny";
                    Notify("居民被地形阻挡！请返回寻找能走通的路，救援计时不会暂停。",
                        "Civilian route blocked! Find an open approach; the rescue timer continues.");
                }
            }
            else
            {
                reliefFollowerBlockedSeconds=0;
                reliefFollowerBlockedNotice=false;
            }
            if (reliefFollowerMoving)
            {
                reliefFollowerWalkDistance += traveled.Length;
                if (Math.Abs(traveled.x) > .015f)
                    reliefFollowerFacingLeft = traveled.x < 0;
            }
            // The same weather that challenges the hero now affects escorted
            // travelers: shelter and shade restore safety; exposed heat, cold
            // and unsafe tides erode it gradually. This is separate from
            // direct raider pressure and cannot exceed the original 100 max.
            if (ReliefSurvivorSheltered(region))
                reliefFollowerSafety = Math.Min(100, reliefFollowerSafety +
                    dt * (ReliefEscortSecureStop ? 3.4f : 1.4f));
            else if (reliefClimateKitTime <= 0)
                reliefFollowerSafety = Math.Max(0, reliefFollowerSafety -
                    dt * (region == 5 ? 1f : 1.5f));
            if (reliefFollowerSafety <= 0)
            {
                ResetReliefMission(); revision++; sound = "deny";
                Notify("恶劣环境让居民无法继续前进！请利用安全站、树荫与火盆再试一次。",
                    "The weather overwhelmed the evacuees! Use shelters, shade and braziers on the next attempt.");
                return;
            }
            Point station = ObjectPoint(region, "fieldstation" + region, new Point(-100, -100));
            // The return objective belongs to TWO real living actors.
            // Distance alone previously allowed finishing on opposite
            // sides of a closed wall. Require unobstructed near-side
            // station contact from both the hero and escorted civilian.
            if ((Position - station).Length >= 1.6f ||
                !HasPhysicalInteractionReach(station) ||
                !ReliefSurvivorCanReach(station,2f))return;
            ResetReliefMission(); revision++;
            if (Has("relief_rescue_" + region))
            {
                Notify("救援重演成功！奖励只能首次领取。",
                    "Rescue replay complete! Rewards are first-clear only.");
                return;
            }
            Flag("relief_rescue_" + region);
            CreditCoins(45); CreditShards(3); AddXp(50);
            RecordMoment("从第 " + (region - 4) + " 条远岸路线上救回被围困的居民。",
                "Rescued stranded civilians from farshore route " + (region - 4) + ".");
            if (Has("relief_rescue_5") && Has("relief_rescue_6") &&
                Has("relief_rescue_7") && !Has("relief_rescue_complete"))
            {
                Flag("relief_rescue_complete"); CreditSkillPoints(1); CreditTea(2);
                Notify("三地居民全部获救！额外技能点1、凉茶2。",
                    "Civilians rescued across all three regions! Bonus talent and two teas.");
            }
            else Notify("居民平安抵达！45硬币、3碎片、50经验。",
                "Civilians safe! +45 coins, +3 shards and +50 XP.");
            TrackQuest("relief_rescue"); sound = "reward";
        }
        // The rescued civilian is a moving actor, not an authored Map object.
        // The existing E/gamepad interaction must reach it through its actual
        // world-space position, without creating a fake persistent WorldObject.
        private void TalkRescuedCivilian()
        {
            if (!ReliefEscortActive || (Position - reliefFollowerPosition).Length > 1.7f ||
                !ClearAttackPath(Position,reliefFollowerPosition)) return;
            var options = new List<Choice>();
            options.Add(C(reliefFollowerWaiting ? "跟上我" : "在这里等候",
                reliefFollowerWaiting ? "Follow me" : "Wait here",
                reliefFollowerWaiting ? "relief_follow" : "relief_wait"));
            if (!reliefMedicalUsed && save.inventory[2] > 0 && reliefFollowerSafety < 95)
                options.Add(C("为伤者敷药 · 药草1", "Treat wounds · 1 herb", "relief_treat"));
            if (ReliefEscortGuardAvailable)
                options.Add(C("组织随行护卫 · 耐力20",
                    "Form escort guard · 20 energy", "relief_escort_guard"));
            if (ReliefEscortHurryAvailable)
                options.Add(C("紧急赶路 · 耐力15",
                    "Hurry to shelter · 15 energy", "relief_hurry"));
            if (ReliefEscortEvadeAvailable)
                options.Add(C("躲开袭击 · 耐力15",
                    "Evade incoming strike · 15 energy", "relief_evasive_step"));
            if (ReliefClimateKitAvailable)
                options.Add(C("展开临时气候防护 · 星晶碎片1",
                    "Deploy weather canopy · 1 shard", "relief_climate_kit"));
            options.Add(C("继续前行", "Continue", "close"));
            Dialog("获救居民", "Rescued civilian",
                (reliefFollowerWaiting ? "我会留在这里，等你清理道路。记得回来接我。" :
                    "我会跟上。请不要走太远，前面还有巡逻队和恶劣天气。") +
                "\n\n居民安全度 " + (int)Math.Ceiling(reliefFollowerSafety) + "% · " +
                    (int)Math.Ceiling(reliefMissionTime) + " 秒剩余。按 E 可等待、敷药、护卫、急行或展开临时气候防护。",
                (reliefFollowerWaiting ? "I will stay here until you clear the road. Come back for me." :
                    "I'll follow. Don't leave me behind among patrols and harsh weather.") +
                "\n\nCivilian safety " + (int)Math.Ceiling(reliefFollowerSafety) + "% · " +
                    (int)Math.Ceiling(reliefMissionTime) + " seconds left. Interact to wait, heal, guard, hurry or deploy a weather canopy.",
                options.ToArray());
        }
        private bool ChooseRescuedCivilian(string action)
        {
            if (action != "relief_wait" && action != "relief_follow" &&
                action != "relief_treat" && action != "relief_escort_guard" &&
                action != "relief_hurry" && action != "relief_climate_kit" &&
                action != "relief_evasive_step") return false;
            if (!ReliefEscortActive || (Position - reliefFollowerPosition).Length > 1.7f ||
                !ClearAttackPath(Position,reliefFollowerPosition))
                return true;
            if (action == "relief_wait")
            {
                reliefFollowerWaiting = true; reliefFollowerMoving = false;
                ClearReliefFollowerRoute();
                Notify("居民会在此等候。清理道路后回来接应。", "Civilian waiting here. Return after clearing the route.");
            }
            else if (action == "relief_follow")
            {
                reliefFollowerWaiting = false;
                ClearReliefFollowerRoute();
                Notify("居民重新跟随你。", "The civilian resumes following you.");
            }
            else if (action == "relief_evasive_step")
            {
                // Only an actual committed strike can warrant a one-tile
                // command dash. SweepActor checks the whole movement; the
                // hero cannot order a dodge across rock, closed gates, or
                // an unreachable NPC dialogue. Energy is charged only after
                // a real relocation, without granting invulnerability.
                if(!ReliefEscortEvadeAvailable)return true;
                Point start=reliefFollowerPosition;
                Point[] offsets={
                    new Point(1,0),new Point(-1,0),
                    new Point(0,1),new Point(0,-1),
                    new Point(1,1).Normalized,new Point(-1,1).Normalized,
                    new Point(1,-1).Normalized,new Point(-1,-1).Normalized
                };
                Point best=start;
                float bestScore=float.NegativeInfinity;
                foreach(Point direction in offsets)
                {
                    Point desired=start+direction*1.1f;
                    if(!CanStand(desired) ||
                        !ClearAttackPath(start,desired))continue;
                    Point candidate=SweepActor(start,direction*1.1f);
                    if((candidate-start).Length<.7f)continue;
                    // Rank the WORST live threat, not the first legal
                    // adjacent tile. A step toward a charging raider is a
                    // poor escape even when each destination is equally
                    // distant from the already committed attack marker.
                    // Target miss distance matters most; attacker distance
                    // breaks ties without treating this as invulnerability.
                    float score=float.MaxValue;
                    foreach(Enemy e in enemies)
                        if(e.hp>0 && e.state==1 &&
                            e.targetingReliefCivilian)
                        {
                            float dodge=(candidate-e.target).Length;
                            float spacing=(candidate-e.Position).Length;
                            score=Math.Min(score,dodge*1.5f+spacing*.5f);
                        }
                    if(score>bestScore)
                    {best=candidate;bestScore=score;}
                }
                if((best-start).Length<.7f)
                {
                    Notify("四周通路封闭，居民无法执行侧移；没有消耗耐力。",
                        "No open path for civilian evasion. No energy spent.");
                    return true;
                }
                save.energy-=15;reliefEvadeCooldown=3.5f;
                reliefFollowerPosition=best;
                ClearReliefFollowerRoute();
                reliefFollowerWalkDistance+=(best-start).Length;
                reliefFollowerMoving=true;
                if(Math.Abs(best.x-start.x)>.015f)
                    reliefFollowerFacingLeft=best.x<start.x;
                effects.Add(new Effect("discover",best,.45f) {
                    reliefMissionOwned=true });
                sound="dash";revision++;
                Notify("居民沿开放的道路闪避！不能免疫仍会命中的攻击。",
                    "Civilian dodged onto open ground! This does not block all attacks.");
            }
            else if (action == "relief_climate_kit")
            {
                if (!ReliefClimateKitAvailable) return true;
                save.shards -= 1; reliefClimateKitUsed = true;
                reliefClimateKitTime = 14f;
                effects.Add(new Effect("discover", reliefFollowerPosition, .6f) {
                    reliefMissionOwned = true });
                sound = "guard";
                Notify("已展开临时气候防护：14秒内阻挡极端环境损害，但无法抵挡敌人攻击。",
                    "Weather canopy deployed: 14 seconds of climate protection, not enemy protection.");
            }
            else if (action == "relief_hurry")
            {
                if (!ReliefEscortHurryAvailable) return true;
                save.energy -= 15;
                reliefHurryUsed = true; reliefHurryTime = 7f;
                reliefFollowerWaiting = false;
                effects.Add(new Effect("discover", reliefFollowerPosition, .55f) {
                    reliefMissionOwned = true });
                sound = "quest";
                Notify("已组织紧急赶路！7秒内居民加快步伐；危险前摇期间仍会警戒停步。",
                    "Hurry to shelter! Civilian travel speed boosted for 7 seconds; incoming attacks still halt movement.");
            }
            else if (action == "relief_escort_guard")
            {
                if (!ReliefEscortGuardAvailable) return true;
                save.energy -= 20;
                reliefWardUsed = true; reliefWardTime = 8f;
                effects.Add(new Effect("discover", reliefFollowerPosition, .6f) {
                    reliefMissionOwned = true });
                sound = "guard";
                Notify("随行护卫已建立：8秒内减轻居民受击伤害，本次护送不能重复发动。",
                    "Escort guard active: civilian attack damage reduced for 8 seconds. Once per return trip.");
            }
            else
            {
                if (reliefMedicalUsed || save.inventory[2] < 1 || reliefFollowerSafety >= 95) return true;
                save.inventory[2]--; reliefMedicalUsed = true;
                reliefFollowerSafety = Math.Min(100, reliefFollowerSafety + 25);
                effects.Add(new Effect("discover", reliefFollowerPosition, .5f));
                Notify("为获救居民敷药：安全度 +25。本次撤离只能使用一次。", "Tended civilian wounds: +25 safety. Once per sortie.");
                sound = "reward";
            }
            revision++;
            return true;
        }
        private bool TalkFieldStation(WorldObject o)
        {
            if (!o.kind.StartsWith("fieldstation")) return false;
            int region;
            if (!int.TryParse(o.kind.Substring("fieldstation".Length), out region) ||
                region < 5 || region > 7 || save.room != region) return true;
            if(!PhysicalRefugeReach(new Point(o.x,o.y),2.2001f))return true;
            if (Has("fieldstation_" + region))
            {
                string textZh = region == 5 ?
                    "潮汐观测站重新运作。这里能稳定低潮" + TidewatchHoldSeconds + "秒，让被水淹没的浅滩重新成为道路。" :
                    region == 6 ? "蓄光管开始运转。站在附近，沙海的热度会迅速消退。" :
                    "火盆热流已经送到小站。靠近站台，寒意会迅速消散。";
                string textEn = region == 5 ?
                    "Tidewatch is online. Hold low tide for " + TidewatchHoldSeconds + " seconds to reopen flooded shoals." :
                    region == 6 ? "The restored sun channel cools the surrounding desert." :
                    "The repaired frost shelter now warms nearby travelers.";
                var stationChoices = new List<Choice>();
                if (region == 5)
                    stationChoices.Add(C("固定低潮" + TidewatchHoldSeconds + "秒", "Hold low tide for " + TidewatchHoldSeconds + " seconds", "fieldstation_tide"));
                if (Has("farshore_relief_complete"))
                {
                    textZh += "\n\n平民避难路网已接通炉火旅店。附近安全时可经旧驿道返回旅店星图桌。";
                    textEn += "\n\nThe civilian refuge roads now reach the Hearth Inn. Travel back to its chart when the surrounding paths are clear.";
                    stationChoices.Add(C("返回炉火旅店", "Return to the Hearth Inn", "fieldstation_inn"));
                }
                if (Has("fieldstation_complete"))
                {
                    textZh += "\n\n三座观测站已联通。在周围安全时，可以直接前往其他两座站点。";
                    textEn += "\n\nThe three stations are linked. Travel directly to either other post when the coast is clear.";
                    string[] locationsZh = { "雾港潮汐站", "沙海降温站", "高原保暖站" };
                    string[] locationsEn = { "Mistport tidewatch", "Glass Desert cooler", "Frostchime shelter" };
                    for (int destination = 5; destination <= 7; destination++)
                        if (destination != region)
                            stationChoices.Add(C("传送至" + locationsZh[destination - 5],
                                "Travel to " + locationsEn[destination - 5],
                                "fieldstation_to_" + destination));
                }
                if (!Has("fieldstation_survey_" + region))
                {
                    if (fieldSurveyRoom == region)
                    {
                        textZh += "\n\n巡测进行中：" + (fieldSurveyPhase == 1 ? "前往环境标记" : "返回站台") + "，剩余" + (int)Math.Ceiling(fieldSurveyTime) + "秒。";
                        textEn += "\n\nSurvey active: " + (fieldSurveyPhase == 1 ? "reach the field marker" : "return to the post") + "; " + (int)Math.Ceiling(fieldSurveyTime) + "s left.";
                        stationChoices.Add(C("取消本次巡测", "Abandon this survey", "fieldstation_survey_cancel"));
                    }
                    else if (fieldSurveyRoom == 0)
                    {
                        textZh += "\n\n可选巡测：90秒内抵达区域环境标记、读取数据并返回。雾港需要低潮，沙海/高原需控制暑寒低于60。失败无损失。";
                        textEn += "\n\nOptional 90s field survey: reach the regional landmark, take a reading and return. Harbor requires low tide; desert/frost require exposure below 60. Failure costs nothing.";
                        stationChoices.Add(C("开始90秒环境巡测", "Begin 90-second field survey", "fieldstation_survey_start"));
                    }
                }
                else { textZh += "\n\n本站巡测报告已归档。"; textEn += "\n\nThis station's field survey is archived."; }
                if (save.frontierEnding > 0)
                {
                    if (Has("farshore_relief_" + region))
                    {
                        textZh += "\n\n本地应急庇护工程已完成。新的避险范围与潮汐控制立即生效；附近安全时可在站台缓慢恢复生命。";
                        textEn += "\n\nThis civilian refuge has stronger tide or climate protection. Rest here to recover health slowly while nearby roads are clear."; 
                    }
                    else if (Has("fieldstation_survey_" + region))
                    {
                        textZh += "\n\n地区巡测已确定安全施工地点。可以投入物资建设真正的平民避难通道；周围有敌人时无法施工。";
                        textEn += "\n\nField readings identified a safe construction route. Deliver supplies to build civilian shelter access; clear nearby hostiles first.";
                        string[] buildZh = { "增建救援浮桥 · 木材2 零件1", "增建绿洲遮阳道 · 星晶2 药草1", "增建防寒补给棚 · 木材1 药草2" };
                        string[] buildEn = { "Rescue pontoons · 2 timber, 1 part", "Shaded supply lane · 2 crystals, 1 herb", "Warming depot · 1 timber, 2 herbs" };
                        stationChoices.Add(C(buildZh[region - 5], buildEn[region - 5], "fieldstation_relief_" + region));
                    }
                    else
                    {
                        textZh += "\n\n新协定允许建设平民避难通道；请先完成本站的90秒环境巡测。";
                        textEn += "\n\nThe covenant funds civilian refuges. Complete this station's 90-second field survey first.";
                    }
                }
                if (Has("relief_rescue_" + region))
                {
                    textZh += "\n\n失联居民已经从危险地段安全撤回。本地救援仍可重演，但不会重复发放奖励。";
                    textEn += "\n\nThe stranded civilians returned safely. The rescue can be replayed, with no repeated rewards.";
                }
                if (Has("farshore_relief_" + region) && Has("fieldstation_survey_" + region))
                    stationChoices.Add(C("平民撤离行动", "Civilian rescue sortie", "fieldstation_rescue_menu"));
                if (Has("fieldstation_survey_complete") && !Has("farshore_relief_complete"))
                    stationChoices.Add(C("查看远岸气象预报", "View farshore weather forecast", "fieldstation_forecast"));
                stationChoices.Add(C("离开观测站", "Leave the station", "close"));
                Dialog(o.name, o.en, textZh, textEn, stationChoices.ToArray());
                return true;
            }
            string[] zh = { "潮水正在冲毁通往北岸的标记。维修需要木材1与零件1，必须在低潮时工作；修好后可以随时稳定低潮45秒。",
                "要让沙海站台降温，先把三面镜子的光送到蓄光池，再投入星晶1与零件1。之后站台周围将形成清凉区域。",
                "严寒让观测仪无法启动。先点燃西侧火盆，再提供木材1与药草1；修好后小站会持续向周围供暖。" };
            string[] en = { "The north-shore markers are washing away. Repair requires 1 timber and 1 spare part during low tide. Afterwards this post can hold low tide for 45 seconds.",
                "Channel sunlight through the three mirrors into the receiver, then contribute 1 crystal and 1 spare part. The repaired station cools nearby ground.",
                "The instrument has frozen. Light the western brazier, then provide 1 timber and 1 herb. The restored station keeps its surroundings warm." };
            string[] optionZh = { "低潮维修 · 木材1 / 零件1", "接通光路后维修 · 星晶1 / 零件1", "点燃火盆后维修 · 木材1 / 药草1" };
            string[] optionEn = { "Repair at low tide · timber 1 / part 1", "Repair after mirrors · crystal 1 / part 1", "Repair after brazier · timber 1 / herb 1" };
            Dialog(o.name, o.en, zh[region - 5], en[region - 5],
                C(optionZh[region - 5], optionEn[region - 5], "fieldstation_repair_" + region),
                C("先调查附近环境", "Explore the surroundings", "close"));
            return true;
        }
        // One collision-safe landing rule for local station links and the Inn relief chart.
        // Guard locked islands and newly spawned patrols rather than teleporting to the prop.
        private void LandNearFieldStation(int destination)
        {
            foreach (WorldObject o in Map.objects)
            {
                if (o.kind != "fieldstation" + destination) continue;
                Point anchor = new Point(o.x, o.y);
                Point[] approaches = { new Point(0, 1), new Point(0, -1),
                    new Point(1, 0), new Point(-1, 0), new Point(1, 1),
                    new Point(-1, 1), new Point(1, -1), new Point(-1, -1) };
                Point chosen = Position;
                float safety = -1; bool found = false;
                foreach (Point offset in approaches)
                {
                    Point candidate = anchor + offset;
                    // This checks destination collision and reachability from
                    // the normal map entrance; it cannot place the hero inside
                    // a locked gate or disconnected walkable island.
                    if (!CanStand(candidate) || NavigationPath(candidate).Count == 0)
                        continue;
                    float clearance = 9999;
                    foreach (Enemy enemy in enemies)
                        if (enemy.hp > 0)
                            clearance = Math.Min(clearance, (enemy.Position - candidate).Length);
                    if (found && clearance <= safety) continue;
                    chosen = candidate; safety = clearance; found = true;
                }
                if (found) { save.x = chosen.x; save.y = chosen.y; }
                break; // Safe map spawn stays the fallback.
            }
        }
        private bool ChooseFieldStation(string action)
        {
            if (action == "fieldstation_rescue_menu")
            {
                int currentRoom = save.room;
                if (currentRoom < 5 || currentRoom > 7 ||
                    !Has("farshore_relief_" + currentRoom) ||
                    !Has("fieldstation_survey_" + currentRoom)) return true;
                Point basePost = ObjectPoint(currentRoom, "fieldstation" + currentRoom, new Point(-100, -100));
                if (!PhysicalRefugeReach(basePost,2.2001f)) return true;
                var options = new List<Choice>();
                if (reliefMissionRoom == currentRoom)
                    options.Add(C("放弃本次撤离", "Abandon this sortie", "fieldstation_rescue_cancel"));
                else options.Add(C(Has("relief_rescue_" + currentRoom) ? "重演救援（无重复奖励）" : "出发救援（120秒）",
                    Has("relief_rescue_" + currentRoom) ? "Replay rescue (no rewards)" : "Begin rescue (120 seconds)",
                    "fieldstation_rescue_start"));
                if (Has("fieldstation_survey_complete"))
                    options.Add(C("查看远岸气象预报", "Read weather forecast", "fieldstation_forecast"));
                options.Add(C("离开", "Leave", "close"));
                Dialog("远岸避难行动", "Farshore Evacuation",
                    "被困居民在巡测标记附近。掠夺者会威胁居民，贴近守护可减轻伤害。击退两波敌人后，高潮、沙海酷热与高原严寒也会影响安全度，安全站、树荫和火盆能帮助恢复。失败可免费重试。" +
                    (reliefMissionRoom == currentRoom ? "\n\n" + (reliefMissionWave == 0 ? "正在寻找巡测点的居民" :
                        reliefMissionWave == 3 ? "正在护送幸存者返回站台" : "正在击退第" + reliefMissionWave + "/2波伏击") +
                        "，剩余 " + (int)Math.Ceiling(reliefMissionTime) + " 秒。" : ""),
                    "Raiders threaten stranded civilians near the marker. Stand close to protect them, defeat two waves, then escort them home. Exposed tides, desert heat and frost also drain safety; shelters restore it. Retries are free." +
                    (reliefMissionRoom == currentRoom ? "\n\n" + (reliefMissionWave == 0 ? "Finding civilians at the field marker" :
                        reliefMissionWave == 3 ? "Escorting survivors back to this station" :
                            "Defeating ambush wave " + reliefMissionWave + "/2") +
                        ", " + (int)Math.Ceiling(reliefMissionTime) + "s remaining." : ""),
                    options.ToArray());
                return true;
            }
            if (action == "fieldstation_rescue_cancel")
            {
                if (reliefMissionRoom != 0 && reliefMissionRoom == save.room &&
                    save.room >= 5 && save.room <= 7 &&
                    PhysicalRefugeReach(ObjectPoint(save.room,"fieldstation"+save.room,new Point(-100,-100)),2.2001f))
                { ResetReliefMission(); revision++; Notify("撤离行动取消，可免费重试。", "Rescue canceled; retry for free."); }
                return true;
            }
            if (action == "fieldstation_rescue_start")
            { BeginReliefMission(); return true; }
            if (action == "fieldstation_inn")
            {
                if (reliefMissionRoom != 0)
                { Notify("先护送居民返回站台，或明确取消本次救援。", "Escort the civilians home or explicitly abandon rescue before traveling."); return true; }
                if (FieldWorkPreventsRest)
                { Notify("其它现场行动尚未结束，不能离开气象站。", "Finish the active field mission before leaving the weather station."); return true; }
                int sourceStation = save.room;
                if (sourceStation < 5 || sourceStation > 7 || !Has("farshore_relief_complete") ||
                    !Has("fieldstation_" + sourceStation) || Dying || practicing || racing ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0 ||
                    fieldSurveyRoom != 0) return true;
                WorldObject homePost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + sourceStation) { homePost = o; break; }
                if (homePost == null || !PhysicalRefugeReach(new Point(homePost.x,homePost.y),2.2001f)) return true;
                if (!SafeTravel)
                { Notify("附近敌人正在破坏补给线。暂时无法返回旅店。", "Nearby hostiles block the supply route to the inn."); return true; }
                Enter(9);
                if (save.room == 9 && CanStand(new Point(15, 15)))
                { save.x = 15; save.y = 15; }
                Notify("沿平民避难路线返回炉火旅店，物资仍在背包里。",
                    "The civilian refuge road leads back to the Hearth Inn with your supplies intact.");
                return true;
            }
            if (action.StartsWith("farshore_relief_to_"))
            {
                if (FieldWorkPreventsRest)
                { Notify("现场行动尚未结束，不能启动跨区运输。", "Finish the active field mission before starting cross-region transport."); return true; }
                int reliefDestination;
                if (!int.TryParse(action.Substring("farshore_relief_to_".Length), out reliefDestination) ||
                    reliefDestination < 5 || reliefDestination > 7 || save.room != 9 ||
                    !Has("farshore_relief_complete") || !Has("fieldstation_" + reliefDestination) ||
                    !CanEnter(reliefDestination) || Dying || practicing || racing ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0 ||
                    fieldSurveyRoom != 0) return true;
                Point chart = ObjectPoint(9, "surveyDesk", new Point(-100, -100));
                if (!PhysicalRefugeReach(chart,2.2001f)) return true;
                if (!SafeTravel)
                { Notify("旅店附近尚有敌人，不能启动运输路线。", "The route cannot open while hostiles threaten the inn."); return true; }
                Enter(reliefDestination);
                if (save.room != reliefDestination) return true;
                LandNearFieldStation(reliefDestination);
                revision++; sound = "travel";
                Notify("平民避难交通线已接通，你安全抵达远岸气象站。",
                    "Civilian relief route connected. You arrive safely beside a farshore station.");
                return true;
            }
            if (action.StartsWith("fieldstation_relief_"))
            {
                int reliefRegion;
                if (!int.TryParse(action.Substring("fieldstation_relief_".Length), out reliefRegion) ||
                    reliefRegion < 5 || reliefRegion > 7 || save.room != reliefRegion ||
                    save.frontierEnding == 0 || !Has("fieldstation_" + reliefRegion) ||
                    !Has("fieldstation_survey_" + reliefRegion) || Has("farshore_relief_" + reliefRegion) ||
                    !CanEnter(reliefRegion) || Dying || practicing || racing || fieldSurveyRoom != 0 ||
                    roadTrialRoom != 0 || wayfarerEncounterRoom != 0 || atlasEchoRoom != 0)
                    return true;
                WorldObject post = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + reliefRegion) { post = o; break; }
                if (post == null || !PhysicalRefugeReach(new Point(post.x,post.y),2.2001f)) return true;
                if (!SafeTravel)
                { Notify("敌人正在威胁施工路线。先清理附近的巡逻队。", "Hostiles threaten the construction route. Clear nearby patrols first."); return true; }
                if (reliefRegion == 5 && !LowTide)
                { Notify("修建浮桥需要低潮。先启动潮汐站或等待退潮。", "Pontoons must be built at low tide. Use the tidewatch or wait for the ebb."); return true; }
                if (!Spend(reliefRegion == 5 ? 2 : reliefRegion == 7 ? 1 : 0,
                    reliefRegion == 6 ? 2 : 0, reliefRegion == 6 ? 1 : reliefRegion == 7 ? 2 : 0,
                    0, reliefRegion == 5 ? 1 : 0)) return true;
                Flag("farshore_relief_" + reliefRegion);
                CreditCoins(30); CreditShards(2); AddXp(35);
                effects.Add(new Effect("discover", new Point(post.x, post.y), .8f));
                RecordMoment(reliefRegion == 5 ? "新的浮桥和救援旗使低潮安全时段延长到90秒。" :
                    reliefRegion == 6 ? "绿洲遮阳道将沙海的降温庇护范围扩大到6.5格。" :
                    "防寒补给棚将高原的保暖庇护范围扩大到6.5格。",
                    reliefRegion == 5 ? "New rescue pontoons extend tidewatch access to 90 seconds." :
                    reliefRegion == 6 ? "The shaded supply route expands desert cooling to 6.5 tiles." :
                    "The warming depot expands frost shelter to 6.5 tiles.");
                if (Has("farshore_relief_5") && Has("farshore_relief_6") &&
                    Has("farshore_relief_7") && !Has("farshore_relief_complete"))
                {
                    Flag("farshore_relief_complete"); CreditSkillPoints(1); CreditTea(2);
                    RecordMoment("三地应急避难通道建成，远岸旅人终于有了可持续的庇护网络。",
                        "All three civilian refuge routes are open. The farshore has a lasting shelter network.");
                    Notify("远岸平民庇护网完成！本站30硬币、2碎片、35经验；额外技能点1、凉茶2。",
                        "Civilian refuge network complete! +30 coins, 2 shards, 35 XP; bonus talent and 2 teas.");
                }
                else Notify("应急工程完工：30硬币、2碎片、35经验；本站气候庇护已强化。",
                    "Relief works complete: +30 coins, 2 shards, 35 XP; the local refuge is strengthened.");
                TrackQuest("farshore_relief"); revision++; sound = "reward";
                return true;
            }
            if (action == "fieldstation_forecast")
            {
                int currentRegion = save.room;
                if (currentRegion < 5 || currentRegion > 7 || !Has("fieldstation_survey_complete") ||
                    !Has("fieldstation_" + currentRegion) || Dying || practicing) return true;
                WorldObject forecastPost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + currentRegion) { forecastPost = o; break; }
                if (forecastPost == null ||
                    !PhysicalRefugeReach(new Point(forecastPost.x,forecastPost.y),2.2001f)) return true;
                float phase = save.totalTime % 36;
                int toEbb = (int)Math.Ceiling(phase < 22 ? 0 : 36 - phase);
                int toFlood = (int)Math.Ceiling(phase < 22 ? 22 - phase : 0);
                int held = (int)Math.Ceiling(Math.Max(0, save.tideHeldUntil - save.totalTime));
                string tideZh = phase < 22 ? "自然低潮，约" + toFlood + "秒后涨潮" :
                    "自然高潮，约" + toEbb + "秒后退潮";
                string tideEn = phase < 22 ? "natural low tide, rising in about " + toFlood + "s" :
                    "natural high tide, ebb in about " + toEbb + "s";
                if (held > 0)
                {
                    tideZh += "；潮闸仍固定低潮约" + held + "秒";
                    tideEn += "; sluice holds low tide for another " + held + "s";
                }
                Dialog("远岸风暴观测网", "Farshore Stormwatch",
                    "巡测数据已同步。\n\n雾港：" + tideZh +
                    "。\n沙海：无遮阴时热度每秒约增加1.3，达到85后会持续受伤；降温站附近" + FieldStationRefugeRadius(6).ToString("0.0") + "格内可消暑。\n高原：无火盆时寒意每秒约增加1.1，达到90后会持续受伤；保暖站附近" + FieldStationRefugeRadius(7).ToString("0.0") + "格内可驱寒。\n\n可以利用观测网传送到安全的站点，但敌人靠近时不能启动。",
                    "Field readings synchronized.\n\nMistport: " + tideEn +
                    ".\nGlass Desert: exposed heat rises about 1.3/s; 85+ causes repeated harm. The cooler protects a " + FieldStationRefugeRadius(6).ToString("0.0") + "-tile radius.\nFrostchime: exposed cold rises about 1.1/s; 90+ causes repeated harm. The shelter warms a " + FieldStationRefugeRadius(7).ToString("0.0") + "-tile radius.\n\nStation travel requires a clear area with no nearby hostiles.",
                    C("收起预报", "Close forecast", "close"));
                return true;
            }
            if (action == "fieldstation_survey_cancel")
            {
                if (fieldSurveyRoom == save.room && fieldSurveyRoom != 0 &&
                    save.room >= 5 && save.room <= 7 &&
                    PhysicalRefugeReach(ObjectPoint(save.room,"fieldstation"+save.room,new Point(-100,-100)),2.2001f))
                { ResetFieldStationSurvey(); revision++; Notify("已取消巡测，可随时重试。", "Survey abandoned; you may retry."); }
                return true;
            }
            if (action == "fieldstation_survey_start")
            {
                if (TideSalvageActive)
                { Notify("潮滩打捞尚未结束，先返回码头交付或放弃。",
                    "Finish or abandon your tide salvage before starting another survey."); return true; }
                if (reliefMissionRoom != 0)
                { Notify("先完成当前居民救援，才能接取新巡测。", "Finish the active civilian rescue before beginning another survey."); return true; }
                if (FieldWorkPreventsRest)
                { Notify("先完成当前现场任务，再启动新的环境巡测。", "Finish the active field mission before starting another survey."); return true; }
                int surveyRegion = save.room;
                if (surveyRegion < 5 || surveyRegion > 7 || fieldSurveyRoom != 0 ||
                    !Has("fieldstation_" + surveyRegion) || Has("fieldstation_survey_" + surveyRegion) ||
                    Dying || practicing || racing || !SafeTravel) return true;
                WorldObject surveyPost = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + surveyRegion) { surveyPost = o; break; }
                if (surveyPost == null || !PhysicalRefugeReach(new Point(surveyPost.x,surveyPost.y),2.2001f)) return true;
                fieldSurveyRoom = surveyRegion; fieldSurveyPhase = 1; fieldSurveyTime = 90;
                fieldSurveyConditionWarned = false; TrackQuest("station_patrol");
                revision++; sound = "quest";
                Notify("巡测开始：前往地图标记并在90秒内返回观测站。",
                    "Survey started: visit the marked landmark and return within 90 seconds.");
                return true;
            }
            if (action.StartsWith("fieldstation_to_"))
            {
                if (reliefMissionRoom != 0)
                { Notify("救援进行中，不能抛下居民传送。可返回站台取消行动。", "Rescue underway. Return to the station to cancel instead of abandoning civilians."); return true; }
                // Do not silently discard a timed, unfinished field reading.
                // The player must finish or explicitly cancel it first.
                if (fieldSurveyRoom != 0)
                {
                    Notify("巡测进行中。先完成或取消本次巡测，才能使用站点传送。",
                        "Field survey underway. Finish or cancel it before station travel.");
                    return true;
                }
                if (FieldWorkPreventsRest)
                { Notify("其它现场行动尚未结束，不能使用站点传送。", "Finish the active field mission before station travel."); return true; }
                int destination;
                if (!int.TryParse(action.Substring("fieldstation_to_".Length), out destination) ||
                    destination < 5 || destination > 7 || destination == save.room ||
                    save.room < 5 || save.room > 7 || !Has("fieldstation_complete") ||
                    !Has("fieldstation_" + save.room) || !Has("fieldstation_" + destination) ||
                    !CanEnter(destination) || Dying || practicing || roadTrialRoom != 0 ||
                    wayfarerEncounterRoom != 0 || racing) return true;
                WorldObject origin = null;
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "fieldstation" + save.room) { origin = o; break; }
                if (origin == null || !PhysicalRefugeReach(new Point(origin.x,origin.y),2.2001f))
                    return true;
                if (!SafeTravel)
                {
                    Notify("敌人还在附近，观测网无法稳定传送。", "Nearby enemies disrupt station travel.");
                    return true;
                }
                Enter(destination);
                if (save.room != destination) return true;
                LandNearFieldStation(destination);
                revision++; sound = "travel";
                Notify("观测网已连接。你抵达另一座远岸安全站。",
                    "Stormwatch linked! Arrived at another farshore shelter.");
                return true;
            }
            if (action == "fieldstation_tide")
            {
                if (save.room != 5 || !Has("fieldstation_5") || Dying || practicing) return true;
                WorldObject post = null;
                foreach (WorldObject o in Map.objects) if (o.kind == "fieldstation5") { post = o; break; }
                if (post == null || !PhysicalRefugeReach(new Point(post.x,post.y),2.2001f)) return true;
                if (!SafeTravel) { Notify("附近仍有威胁，潮汐装置暂不可操作。", "Clear nearby hostiles before operating the tidewatch."); return true; }
                save.tideHeldUntil = Math.Max(save.tideHeldUntil, save.totalTime + TidewatchHoldSeconds);
                revision++; sound = "quest";
                effects.Add(new Effect("discover", new Point(post.x, post.y), .6f));
                Notify("潮汐稳定：低潮延长" + TidewatchHoldSeconds + "秒。", "Tide stabilized: low tide for " + TidewatchHoldSeconds + " seconds.");
                return true;
            }
            if (!action.StartsWith("fieldstation_repair_")) return false;
            int region;
            if (!int.TryParse(action.Substring("fieldstation_repair_".Length), out region) ||
                region < 5 || region > 7 || save.room != region || Dying || practicing ||
                !CanEnter(region) || Has("fieldstation_" + region)) return true;
            WorldObject station = null;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "fieldstation" + region) { station = o; break; }
            if (station == null || !PhysicalRefugeReach(new Point(station.x,station.y),2.2001f)) return true;
            if (!SafeTravel)
            {
                Notify("先清理观测站附近的敌人，才能安全维修。", "Clear nearby enemies before repairing the station.");
                return true;
            }
            bool condition = region == 5 ? LowTide :
                region == 6 ? Has("mirrors_lit") : Has("warm_brazier0");
            if (!condition)
            {
                Notify(region == 5 ? "需要等待低潮，或者启动港口的潮闸。" :
                    region == 6 ? "先把三面镜子的光送到东侧蓄光池。" :
                    "先用两份木材点燃西侧火盆。",
                    region == 5 ? "Wait for low tide, or operate the harbor sluice." :
                    region == 6 ? "Route the three mirrors' beam into the receiver first." :
                    "Light the western brazier using two timber first.");
                return true;
            }
            if (!Spend(region == 5 || region == 7 ? 1 : 0,
                region == 6 ? 1 : 0, region == 7 ? 1 : 0, 0,
                region <= 6 ? 1 : 0))
                return true;
            Flag("fieldstation_" + region);
            CreditCoins(18); CreditShards(2); AddXp(20);
            effects.Add(new Effect("discover", new Point(station.x, station.y), .8f));
            RecordMoment(region == 5 ? "潮汐站修好了，浅滩重新有了安全航标。" :
                region == 6 ? "沙海降温站修好了，荒路上多了一片清凉之地。" :
                "霜钟保暖站修好了，雪地里的旅人有了避寒处。",
                region == 5 ? "The harbor tidewatch now marks a safe crossing." :
                region == 6 ? "The desert cooler offers a refuge from the sun." :
                "The frost shelter gives travelers a place to warm up.");
            if (Has("fieldstation_5") && Has("fieldstation_6") &&
                Has("fieldstation_7") && !Has("fieldstation_complete"))
            {
                Flag("fieldstation_complete"); CreditSkillPoints(1); CreditTea(2);
                RecordMoment("三座观测站连成远岸避风网：技能点1、凉茶2。",
                    "Three weather stations connected: one talent point and two teas.");
                Notify("远岸观测网建成！额外获得技能点1、凉茶2。",
                    "Farshore stormwatch complete! Bonus: 1 talent point and 2 teas.");
            }
            else Notify("观测站已恢复！硬币18、碎片2、经验20。",
                "Station restored! +18 coins, +2 shards, +20 XP.");
            TrackQuest("stations"); revision++; sound = "reward";
            return true;
        }
    }
}
