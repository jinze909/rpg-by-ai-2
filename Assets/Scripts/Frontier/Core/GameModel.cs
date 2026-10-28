using System;
using System.Collections.Generic;

namespace Starfall
{
    [Serializable] public class Campaign
    {
        public string title, titleEn;
        public int version;
        public MapData[] maps;
        public ItemData[] items;
        public BossData[] bosses;
        public LoreData[] lore;
    }
    [Serializable] public class MapData
    {
        public int id;
        public string name, en, subtitle, suben, color;
        public float spawnX, spawnY;
        public string[] rows;
        public WorldObject[] objects;
        public Decoration[] decor;
    }
    [Serializable] public class WorldObject { public string id, name, en, kind; public float x, y; public int lore = -1; }
    [Serializable] public class Decoration { public string kind; public float x, y; public int variant; }
    [Serializable] public class ItemData { public int id; public string name, en; }
    [Serializable] public class BossData { public string name, en; public float hp; }
    [Serializable] public class LoreData { public string title, en, zh, text; }

    // Optional, version-2-compatible checkpoint for a completed rescue
    // ambush. Old saves deserialize this as null.
    [Serializable] public class RescueCheckpoint
    {
        public int room;
        public float x, y, time, safety;
        public bool waiting, treated, escortGuardUsed, hurryUsed, climateKitUsed;
    }
    [Serializable] public class SaveData
    {
        public int version = 2, room, stage, coins = 25, level = 1, xp, weapon, upgrades, ending, profession, skillPoints;
        public int difficulty = 1, seed = 417;
        public bool english, music = true, reducedMotion;
        public int presentationVersion, tutorialBits, effectsQuality = 2;
        public float tutorialDistance;
        public bool showGuidance = true, largeText;
        public string trackedQuest = "main";
        public int[] explored = new int[9];
        public float x = 18, y = 15, health = 100, energy = 100, maxHealth = 100, totalTime, plantedAt = -1, foodUntil;
        public int[] inventory = { 4, 2, 4, 0, 2, 0, 1, 3 };
        public int[] talents = { 0, 0, 0 };
        public int[] kills = { 0, 0, 0, 0, 0 };
        public List<string> flags = new List<string>();
        public bool frontierFocus;
        public int harborPledge, desertChoice, mountainChoice, frontierEnding, shards, tea, technique, relicRank;
        public int companion = -1, bounty = -1, bountyProgress, bountyCycle;
        public int[] reputation = { 0, 0, 0 }, relationships = { 0, 0, 0 };
        public int[] projects = { 0, 0, 0 }, mastery = { 0, 0, 0 }, relics = { -1, -1 };
        public int[] mirrors = { 0, 0, 0 }, bells = { 0, 0, 0 }, boons = { 0, 0, 0 };
        public float heat, cold, tideHeldUntil, teaUntil;
        public int abyssFloor, abyssRisk, abyssSeed = 137, runCoins, runShards, runXp, bestFloor, bestRace;
        public int abyssSnapshotFloor;
        public EnemyRecord[] abyssEnemies;
        public int polishVersion, practiceBits, deaths, snapshotRoom = -1;
        public float musicVolume = .65f, effectsVolume = .8f, ambienceVolume = .5f;
        public int[] weaponStyles = { 0, 0, 0 };
        public EnemyRecord[] worldEnemies;
        public RescueCheckpoint rescueCheckpoint;
        public int savedRunes;
        public string savedAtUtc, lastFailureZh, lastFailureEn;
        public List<JournalMoment> moments = new List<JournalMoment>();
    }
    [Serializable] public class JournalMoment { public string zh, en; public int day; }
    public sealed class WeaponProfile
    {
        public float damage, interval, range, energy;
        public string benefit, cost;
    }
    public struct Point
    {
        public float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
        public float Length { get { return (float)Math.Sqrt(x * x + y * y); } }
        public Point Normalized { get { float l = Length; return l > .0001f ? new Point(x / l, y / l) : new Point(); } }
        public static Point operator +(Point a, Point b) { return new Point(a.x + b.x, a.y + b.y); }
        public static Point operator -(Point a, Point b) { return new Point(a.x - b.x, a.y - b.y); }
        public static Point operator *(Point a, float f) { return new Point(a.x * f, a.y * f); }
    }
    public class Enemy
    {
        public int id, kind, state, cycle;
        public bool boss, moving;
        // Actual displacement over the most recently simulated world tick,
        // NOT facing (archers face the hero while backpedaling). Transient,
        // never written to EnemyRecord or SaveData v2. Powers physically
        // bounded leading shots rather than widening any hitboxes.
        public Point moveVelocity;
        // True only for an ordinary archer's announced, frozen foot-lead.
        // World telegraphs use this same actual projectile target, not an
        // inferred target on GUI repaint. Not an enemy save-record field.
        public bool archerLeading;
        // Ephemeral combat intent: roadside raiders may target the protected traveler.
        public bool targetingWayfarer, targetingReliefCivilian, targetingAfterlightStock, targetingOuterPatrolStock;
        public float x, y, hp, maxHp, timer, stun, flash, stagger, staggerRecovery;
        public Point target, attackOrigin, attackDestination;
        public Point facing = new Point(1, 0);
        public float windupDuration, releaseTime, moveTime, hitTime;
        // Unsaved tactical opening earned by a REAL hero flank against a
        // resting armored guard; owned by this enemy instance, not spawn ID.
        public float armorOpenTime;
        public Point recoil;
        public bool backpedaling;
        private bool lookLeft;
        public bool VisualFlip { get { return Math.Abs(facing.x)>.25f?facing.x<0:lookLeft; } }
        public void Face(Point direction)
        { if(direction.Length<.001f)return;if(Math.Abs(facing.x)>.25f)lookLeft=facing.x<0;facing=direction.Normalized;if(Math.Abs(facing.x)>.25f)lookLeft=facing.x<0; }
        public Enemy(int id, int kind, float x, float y, float hp, bool boss = false)
        {
            this.id = id; this.kind = kind; this.x = x; this.y = y;
            this.hp = maxHp = hp; this.boss = boss;
            // Spawn identity is not elapsed time. Large restored/procedural IDs
            // must not postpone the very first AI attack for minutes or hours.
            // Keep the original delays for the ordinary first eight enemies.
            int openingSlot = id > 0 ? (id - 1) % 8 + 1 : 1;
            timer = 1.3f + openingSlot * .13f;
        }
        public Point Position { get { return new Point(x, y); } }
        public int BossPhase { get { return hp > maxHp * .66f ? 1 : hp > maxHp * .33f ? 2 : 3; } }
    }
    // The world-mode companion button is tap-to-command / hold-to-station.
    // This pure transient state machine keeps keyboard/controller behavior
    // deterministic without adding input state to SaveData or mission clocks.
    public enum CompanionButtonAction { None, Command, ToggleHold }
    public sealed class CompanionButtonGesture
    {
        public const float HoldThreshold = .38f;
        private bool wasDown, holdTriggered;
        private float heldSeconds;
        public CompanionButtonAction Advance(bool isDown, float deltaTime)
        {
            if(float.IsNaN(deltaTime)||float.IsInfinity(deltaTime)||deltaTime<0)
                deltaTime=0;
            // A hitch or debugger pause must not convert a one-frame tap into
            // a hold; subsequent real frames still measure the actual gesture.
            deltaTime=Math.Min(deltaTime,.1f);
            if(isDown)
            {
                if(!wasDown){heldSeconds=0;holdTriggered=false;}
                wasDown=true;
                heldSeconds+=deltaTime;
                if(!holdTriggered&&heldSeconds>=HoldThreshold)
                {holdTriggered=true;return CompanionButtonAction.ToggleHold;}
                return CompanionButtonAction.None;
            }
            if(!wasDown){heldSeconds=0;holdTriggered=false;return CompanionButtonAction.None;}
            bool tap=!holdTriggered;
            Reset();
            return tap?CompanionButtonAction.Command:CompanionButtonAction.None;
        }
        public void Cancel(){Reset();}
        private void Reset(){wasDown=false;holdTriggered=false;heldSeconds=0;}
    }

    [Serializable] public class EnemyRecord
    {
        public int id, kind;
        public bool boss;
        public float x, y, hp, maxHp;
    }
    public class Projectile
    {
        public Point position, velocity;
        public float life = 3, damage;
        public bool friendly, explosive, piercing;
        public bool slowing, technique, blockedAtSpawn;
        // Transient ownership tag: prevent shots from outliving a stopped beacon defense.
        public bool roadTrialOwned, skywatchOwned, atlasEchoOwned, wayfarerOwned, reliefMissionOwned, rootHarmonyOwned, commonsRelayOwned, glasswindOwned, tideSalvageOwned, temperOwned, mineConvoyOwned, frostSignalOwned, swordLessonOwned, triadOwned, witnessOwned, blackoutOwned, stormOwned, courierOwned, civicResponseOwned, lastDirectiveOwned, civicConduitOwned, floodReliefOwned, afterlightWatchOwned, outerPatrolOwned;
        public string source, visual;
        // A live pebble's real throw foot, exclusively for physical party
        // overlap immunity at launch. Not a stored save or damage profile.
        public Point? decoyLaunchFoot;
        // Live shooter identity, never a reusable numeric spawn ID. Hostile
        // arrows physically meet intervening non-Boss enemy bodies; only
        // genuinely released shots from Fire() participate. Not serialized.
        public Enemy hostileShooter;
        public float age, height = 32, launchHeight = -1, flightHeight = -1;
        // Visual-only origin for the CURRENT direction's painted projectile
        // tail. When a sword guard reflects a shot, the old inbound trail
        // must not instantly appear behind the new outbound direction.
        // This field is transient; GameCore SaveData and snapshots are intact.
        public float trailResetAt;
        public ImpactProfile impact;
        // One genuine automatic ally shot may intercept ONE exact living
        // resident attacker on impact. This is deliberately transient:
        // spawn IDs can be reused and SaveData v2 never serializes shots.
        public Enemy rescueCoverTarget;
        // An automatic ally shot may physically stop ONE committed strike
        // against the hero. Never bind it to a reusable numeric spawn ID.
        // This stays transient and does not touch SaveData v2 or equipment.
        public Enemy heroCoverTarget;
        // Physical public-receiver defense: one released ally projectile
        // can interrupt only its genuine mission-owned stock attacker.
        // Never save this volatile combat claim or use a recycled actor ID.
        public Enemy stockCoverTarget;
        // Genuine relay/cache defense uses transient exact instance ownership.
        public Enemy patrolCoverTarget;
        public SaveData patrolCoverSave;
        public MapData patrolCoverMap;
        public int patrolCoverRoom = -1;
        public SaveData stockCoverSave;
        public MapData stockCoverMap;
        public int stockCoverRoom = -1;
        public SaveData heroCoverSave;
        public MapData heroCoverMap;
        public int heroCoverRoom = -1;
        public SaveData rescueCoverSave;
        public MapData rescueCoverMap;
        public int rescueCoverRoom = -1;
        public float Radius { get { return visual == "pebble" ? .12f :
            visual == "arrow" || visual == "enemyArrow" ? .14f : .28f; } }
        public int theme;
        // A projectile can cross several independently spawned actors with the
        // SAME numeric ID (wave resets, restored encounters or procedural IDs).
        // Collision deduplication therefore follows the live Enemy reference,
        // never its reusable save/spawn ID. This stays transient with the shot.
        public readonly HashSet<Enemy> hitActors = new HashSet<Enemy>();
        // Preserve the existing contact-ID trace for visual/debug consumers.
        // Different actor instances may legitimately append the same number.
        public List<int> hits = new List<int>();
    }
    public class Effect
    {
        public Point position;
        public string kind, text;
        public float life, maxLife, radius, height = -1;
        public Point direction;
        public int variant;
        // Live Vanguard/Arcanist Q footprint: authored center spark plus
        // terrain-clipped damage ring. Effects are transient and this flag
        // never touches the v1/v2 save schema or any imported Sprite.
        public bool playerAreaSkill;
        // Short-lived boss-reinforcement warnings follow their actual attack
        // owner; stun, Break, or death retracts only this pending cue.
        public int bossSummonCueOwnerId;
        // Numeric spawn IDs may repeat inside concurrent encounters. The
        // actual Enemy object owns this transient call warning and alone
        // can retract it; ordinary number effects have a null owner.
        public Enemy bossSummonCueOwner;
        // Only encounter-owned warnings retire when an optional rescue ends.
        // Ordinary combat effects are preserved.
        public bool roadTrialOwned, skywatchOwned, atlasEchoOwned, wayfarerOwned, reliefMissionOwned, rootHarmonyOwned, commonsRelayOwned, glasswindOwned, tideSalvageOwned, temperOwned, mineConvoyOwned, frostSignalOwned, swordLessonOwned, triadOwned, witnessOwned, blackoutOwned, stormOwned, courierOwned, civicResponseOwned, lastDirectiveOwned, civicConduitOwned, floodReliefOwned, afterlightWatchOwned, outerPatrolOwned;
        public Effect(string kind, Point position, float life, string text = "")
        { this.kind = kind; this.position = position; this.life = maxLife = life; this.text = text; }
    }
    public class Hazard
    {
        public Point position;
        public float timer, radius, damage;
        public string kind;
        // Only scheduled ambient Abyss storms are suppressed when tuning
        // the floor pylon; identical-looking Boss or scripted rifts persist.
        // This is transient hazard state, never a SaveData v2 field.
        public bool abyssStormOwned;
        // Ground patterns remember the exact live Boss that cast them.
        // This is combat-only identity, never part of SaveData v2.
        public Enemy bossOwner;
        // Transient ownership prevents encounter hazards surviving retirement.
        public bool atlasEchoOwned, rootHarmonyOwned, commonsRelayOwned, skywatchOwned, glasswindOwned, temperOwned, mineConvoyOwned, frostSignalOwned, swordLessonOwned, triadOwned, witnessOwned, blackoutOwned, stormOwned, lastDirectiveOwned, civicConduitOwned;
    }
    public struct LightRay
    {
        public Point from, to;
        public LightRay(Point from, Point to) { this.from = from; this.to = to; }
    }
    public class Choice
    {
        public string label, en, action;
        public Choice(string label, string en, string action) { this.label = label; this.en = en; this.action = action; }
    }
    public class Conversation
    {
        public string speaker, en, text, textEn;
        public Choice[] choices;
        public Conversation(string speaker, string en, string text, string textEn, params Choice[] choices)
        { this.speaker = speaker; this.en = en; this.text = text; this.textEn = textEn; this.choices = choices; }
    }
}
