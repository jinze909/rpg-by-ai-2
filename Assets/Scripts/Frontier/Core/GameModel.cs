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
        // Ephemeral combat intent: roadside raiders may target the protected traveler.
        public bool targetingWayfarer, targetingReliefCivilian;
        public float x, y, hp, maxHp, timer, stun, flash, stagger, staggerRecovery;
        public Point target, attackOrigin, attackDestination;
        public Point facing = new Point(1, 0);
        public float windupDuration, releaseTime, moveTime, hitTime;
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
        public bool roadTrialOwned, reliefMissionOwned, rootHarmonyOwned, commonsRelayOwned, tideSalvageOwned, temperOwned, triadOwned;
        public string source, visual;
        public float age, height = 32, launchHeight = -1, flightHeight = -1;
        public ImpactProfile impact;
        public float Radius { get { return visual == "arrow" || visual == "enemyArrow" ? .14f : .28f; } }
        public int theme;
        public List<int> hits = new List<int>();
    }
    public class Effect
    {
        public Point position;
        public string kind, text;
        public float life, maxLife, radius, height = -1;
        public Point direction;
        public int variant;
        // Only encounter-owned warnings retire when an optional rescue ends.
        // Ordinary combat effects are preserved.
        public bool reliefMissionOwned, rootHarmonyOwned, commonsRelayOwned, glasswindOwned, tideSalvageOwned, temperOwned, triadOwned;
        public Effect(string kind, Point position, float life, string text = "")
        { this.kind = kind; this.position = position; this.life = maxLife = life; this.text = text; }
    }
    public class Hazard
    {
        public Point position;
        public float timer, radius, damage;
        public string kind;
        // Transient ownership prevents encounter hazards surviving retirement.
        public bool atlasEchoOwned, rootHarmonyOwned, commonsRelayOwned, skywatchOwned, glasswindOwned, temperOwned, triadOwned;
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
