using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Texture2D[] playerSheets;
        private Texture2D[] companionSheets;
        private Texture2D[] walkSheets, companionWalkSheets, bossWalkSheets;
        private Texture2D monsterWalkSheet;
        // The wider canvas leaves space for weapons while keeping the same
        // logical body scale and 120/128 foot anchor in every pose.
        private static float BossCanvasSize(int room) { return room == 5 ? 264 : 240; }
        private Texture2D playerPortraits, playerRear, playerSouth, playerReactionsRear, playerReactionsSouth, enemyArcherVertical, npcSheet, monsterSheet, bossSheet, fieldProps, terrainSheet, terrainVariants, detailSheet, effectSheet, iconSheet;
        private void LoadVisuals()
        {
            string root = "Frontier/Art/";
            playerSheets = new[] { Resources.Load<Texture2D>(root + "PlayerVanguard"), Resources.Load<Texture2D>(root + "PlayerRanger"), Resources.Load<Texture2D>(root + "PlayerArcanist") };
            companionSheets = new[] { Resources.Load<Texture2D>(root + "CompanionMira"), Resources.Load<Texture2D>(root + "CompanionIvo"), Resources.Load<Texture2D>(root + "CompanionSen") };
            walkSheets = new[] { Resources.Load<Texture2D>(root + "WalkVanguard"), Resources.Load<Texture2D>(root + "WalkRanger"), Resources.Load<Texture2D>(root + "WalkArcanist") };
            companionWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkMira"), Resources.Load<Texture2D>(root + "WalkIvo"), Resources.Load<Texture2D>(root + "WalkSen") };
            bossWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkBossA"), Resources.Load<Texture2D>(root + "WalkBossB") };
            monsterWalkSheet = Resources.Load<Texture2D>(root + "WalkMonsters");
            playerPortraits = Resources.Load<Texture2D>(root + "PlayerPortraits"); npcSheet = Resources.Load<Texture2D>(root + "Npcs");
            playerRear = Resources.Load<Texture2D>(root + "PlayerRear");
            playerSouth = Resources.Load<Texture2D>(root + "PlayerSouth");
            playerReactionsRear=Resources.Load<Texture2D>(root+"PlayerReactionsRear");
            playerReactionsSouth=Resources.Load<Texture2D>(root+"PlayerReactionsSouth");
            enemyArcherVertical = Resources.Load<Texture2D>(root + "EnemyArcherVertical");
            monsterSheet = Resources.Load<Texture2D>(root + "Monsters"); bossSheet = Resources.Load<Texture2D>(root + "Bosses");
            // Load immutable GPU masks before play, rather than on a first hit.
            foreach(Texture2D sheet in playerSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in walkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in bossWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in new[]{playerRear,playerSouth,playerReactionsRear,playerReactionsSouth,enemyArcherVertical,monsterSheet,bossSheet,monsterWalkSheet})
                if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            fieldProps = Resources.Load<Texture2D>(root + "FieldProps"); terrainSheet = Resources.Load<Texture2D>(root + "Terrain");
            terrainVariants = Resources.Load<Texture2D>(root + "TerrainVariants");
            detailSheet = Resources.Load<Texture2D>(root + "Details"); effectSheet = Resources.Load<Texture2D>(root + "Effects"); iconSheet = Resources.Load<Texture2D>(root + "Icons");
        }
        private void Atlas(Texture2D sheet, int columns, int rows, int index, Rect rect, Color color, bool flip = false, float angle = 0)
        {
            if (sheet == null) return;
            index = Math.Max(0, Math.Min(columns * rows - 1, index));
            var matrix = GUI.matrix; Color previousColor = GUI.color;
            try
            {
                if (angle != 0) GUI.matrix = GuiGeometry.RotateInCanvas(matrix, rect, angle);
                color.a*=worldActorOpacity; GUI.color = color;
                GUI.DrawTextureWithTexCoords(rect, sheet, new Rect((index % columns + (flip ? 1 : 0)) / (float)columns, 1 - (index / columns + 1) / (float)rows, (flip ? -1 : 1) / (float)columns, 1f / rows));
            }
            finally { GUI.color = previousColor; GUI.matrix = matrix; }
        }
        private void Actor(Texture2D sheet, int columns, int rows, int frame, Point foot, float size, Color color, bool flip = false, float angle = 0)
        {
            if (drawingOutline && sheet != null) { sheet = art.OutlineMask(sheet); color = new Color(1,.56f,.35f,.9f); }
            Atlas(sheet, columns, rows, frame, new Rect(foot.x - size / 2, foot.y - size * 120 / 128, size, size), color, flip, angle);
        }
        private void DrawPlayerPreview(int equipment, float x, float y, float size)
        { HumanActor(playerSheets[(int)GameCore.Clamp(equipment,0,2)],4,3,0,new Point(x,y),size/192,Color.white,false,walkSheets[0],0,true); }
        private void DrawPlayerActor()
        {
            Point p = ScreenPoint(Game.Position); if(!drawingOutline)Box(p.x - 17, p.y - 3, 34, 7, new Color(.025f, .04f, .07f, .38f));
            if (Game.racing && !drawingOutline) PropImage("skiff", 0, p.x - 51, p.y - 68, 102, 118);
            int look = Game.AppearanceWeapon, pose = Game.PlayerPose;
            if (playerSheets == null || playerSheets[look] == null) { DrawLegacyHero(); return; }
            Point direction = Game.VisualFacing;
            Color tint = Game.playerFlash > 0 ? new Color(1.5f, 1.5f, 1.5f) : Color.white;
            if (Game.invulnerable > 0 && !Game.Dying && Game.playerFlash <= 0) tint.a = .65f + .35f * (float)Math.Abs(Math.Sin(clock * 16));
            Texture2D sheet = playerSheets[look]; int frame = pose;
            if (pose >= 2 && pose <= 3 && walkSheets[look] != null)
            {
                int row = GameCore.DirectionRow(direction);
                sheet = walkSheets[look]; frame = row * 4 + Game.WalkFrame;
                HumanActor(sheet,4,3,frame,p,.75f,tint,row==0&&direction.x<0,walkSheets[0],row*4,true);
                if(!drawingOutline&&Game.playerFlash>0)HumanActor(sheet,4,3,frame,p,.75f,new Color(1,1,1,Game.playerFlash/.09f*.86f),row==0&&direction.x<0,walkSheets[0],row*4,true,true);
            }
            else
            {
                int facingRow=GameCore.DirectionRow(direction);bool resting=pose<=1;
                if(resting){frame=facingRow==1?1:0;if(facingRow==2&&walkSheets[look]!=null){sheet=walkSheets[look];frame=8;}}
                bool rear = playerRear != null && direction.y < -Math.Abs(direction.x) && pose >= 2 && pose <= 7;
                if (rear) { sheet = playerRear; frame = look * 4 + (pose < 4 ? pose - 2 : pose == 4 || pose == 6 ? 2 : 3); }
                bool south = playerSouth != null && direction.y > Math.Abs(direction.x) && pose >= 4 && pose <= 7;
                if (south)
                {
                    sheet = playerSouth; frame = look * 4 + pose - 4;
                }
                if(pose>=8&&facingRow!=0){Texture2D reaction=facingRow==1?playerReactionsRear:playerReactionsSouth;if(reaction!=null){sheet=reaction;frame=look*4+(pose==8?0:pose==9?1:pose==10?2:3);}}
                bool flip=!rear&&!south&&facingRow==0&&direction.x<-.1f;
                // Attack/cast while actually translating needs the same
                // walking legs as its movement state. Compose pixels on one
                // atlas cell before drawing; never offset torso/head/weapon
                // independently, which caused a detached waist.
                Texture2D unified = !Game.Dying && Game.ActuallyMoving &&
                    pose>=4 && pose<=7 && walkSheets[look]!=null ?
                    UnifiedActionGait(sheet,4,3,frame,walkSheets[look],
                        facingRow*4+Game.WalkFrame) : null;
                HumanActor(sheet,4,3,frame,p,.75f,tint,flip,walkSheets[0],
                    facingRow*4,resting,false,Game.Dying,unified);
                if(!drawingOutline&&Game.playerFlash>0)
                    HumanActor(sheet,4,3,frame,p,.75f,
                        new Color(1,1,1,Game.playerFlash/.09f*.86f),
                        flip,walkSheets[0],facingRow*4,false,true,Game.Dying,unified);
            }
            if (!drawingOutline && Game.save.upgrades > 0 && !Game.Dying)
            { Point rune=p+new Point(direction.x<0?-24:24,-26);Atlas(effectSheet,4,4,13,new Rect(rune.x-7,rune.y-7,14,14),new Color(1,.83f,.54f,Math.Min(.8f,.3f+Game.save.upgrades*.09f))); }
            if (!drawingOutline && Game.guardTime > 0 && !Game.Dying) Ring(Game.Position, .44f, new Color(.76f, .87f, .73f, .65f), 20);
        }
        private int EnemyPose(Enemy e)
        {
            if (e.hitTime > 0) return 4;
            if (e.state == 1) return e.boss && Game.save.room == 1 && e.timer < .2f ? 3 : 2;
            if (e.releaseTime > 0) return 3;
            return e.moving ? 1 : 0;
        }
        private void DrawEnemyActor(Enemy e)
        {
            Point p = ScreenPoint(e.Position); float size = e.boss ? BossCanvasSize(Game.save.room) : e.kind == 2 ? 106 : 96;
            if(!drawingOutline)Box(p.x - size * .22f, p.y - 3, size * .44f, 7, new Color(.025f, .04f, .07f, .35f));
            // The two-wave roadwarden attackers are real combat enemies, but
            // their warm foot ring distinguishes them from roaming patrols.
            // A fixed ring is legible even with reduced-motion effects.
            if (!drawingOutline && e.id >= 81000 && e.id < 81100)
                Ring(e.Position, .52f, PixelArt.C("#e6a06c"), 16);
            if (!drawingOutline && e.id >= 82000 && e.id < 82100)
                Ring(e.Position, .52f, PixelArt.C("#f27d7d"), 16);
            if (!drawingOutline && e.id == 83001)
                Ring(e.Position, .72f, PixelArt.C("#e7b0f2"), 24);
            // Static mint outline also works with reduced-motion enabled.
            if (!drawingOutline && !Game.practicing && e.releaseTime > .001f && e.stun <= 0)
                Ring(e.Position, e.boss ? .86f : .54f, new Color(.48f, 1f, .79f, .85f), 24);
            if (Game.practicing)
            {
                // The safe training rig still demonstrates a bodily wind-up and
                // release. A static sign plus a red circle cannot teach timing.
                Actor(monsterSheet, 6, 3, 12 + EnemyPose(e), p, 106, Color.white, e.VisualFlip);
                if(!drawingOutline)Box(p.x - 16, p.y + 2, 32, 3, mint); return;
            }
            int pose = EnemyPose(e);
            float charge = e.state == 1 ? 1 - e.timer / Math.Max(.01f, e.windupDuration > 0 ? e.windupDuration : e.boss ? 1.1f : .65f) : 0;
            Point offset = new Point();
            // Recoil is swept in Core rather than sliding the drawing through walls.
            Color tint = e.flash > 0 ? new Color(1.55f, 1.55f, 1.55f) : Color.white;
            if (e.boss && Math.Abs(e.x - Game.save.x) < 1.4f && Game.save.y < e.y - .01f && Game.save.y > e.y - 2.7f) tint.a = .55f;
            if (e.boss)
            {
                int row = Math.Max(0, Game.save.room - 1), frame = pose == 2 ? 1 : pose == 3 ? 2 : 0;
                if (pose == 1 && bossWalkSheets[row / 4] != null)
                    Actor(bossWalkSheets[row / 4], 4, 4, row % 4 * 4 + GameCore.GaitFrame(e.moveTime, 2.8f), p, size * 384 / 320f, tint, e.VisualFlip);
                else Actor(bossSheet, 4, 8, row * 4 + frame, p + offset, size, tint, e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && bossSheet != null) Actor(art.FlashMask(bossSheet), 4, 8, row * 4 + frame, p + offset, size, new Color(1, 1, 1, e.flash / .09f * .86f * tint.a), e.VisualFlip);
                if (!drawingOutline && e.state == 1) Atlas(effectSheet,4,4,13,new Rect(p.x-19,p.y-GameCore.BossEmissionHeight(Game.save.room)-19,38,38),new Color(1,.79f,.49f,charge*.6f));
            }
            else
            {
                Texture2D sheet = monsterSheet; int columns = 6, rows = 3, frame = Math.Min(2, e.kind) * 6 + pose;
                if (pose == 1 && monsterWalkSheet != null) { sheet = monsterWalkSheet; columns = 4; rows = 3; int gait=GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);frame=Math.Min(2,e.kind)*4+(e.backpedaling?(4-gait)%4:gait); }
                bool vertical = e.kind == 1 && enemyArcherVertical != null && (pose == 2 || pose == 3) && Math.Abs(e.facing.y) > Math.Abs(e.facing.x);
                if (vertical) { sheet = enemyArcherVertical; columns = 3; rows = 2; frame = (e.facing.y < 0 ? 0 : 2) + pose - 2; }
                float canvas=ActorCanvas(sheet,columns,size/128);
                Actor(sheet, columns, rows, frame, p + offset, canvas, tint, !vertical && e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && sheet != null) Actor(art.FlashMask(sheet), columns, rows, frame, p + offset, canvas, new Color(1, 1, 1, e.flash / .09f * .86f), !vertical && e.VisualFlip);
            }
            // The boss already has a HUD poise meter. Give ordinary enemies a
            // tiny contextual meter only after the player has built stagger.
            if (!drawingOutline && !e.boss && e.stagger > .01f && e.staggerRecovery <= 0)
            {
                float y = p.y - size * .72f;
                Box(p.x - 17, y, 34, 5, new Color(.04f, .09f, .12f, .72f));
                Box(p.x - 16, y + 1, 32 * Mathf.Clamp01(e.stagger / 30f), 3, mint);
            }
        }
        private void DrawFallen(FallenActor body)
        {
            Point p = ScreenPoint(body.actor.Position); float progress = 1 - body.life / body.duration;
            Color tint = new Color(1, 1, 1, Math.Min(1, body.life / (body.duration * .45f)));
            if (body.actor.boss) Actor(bossSheet, 4, 8, Math.Max(0, body.room - 1) * 4 + 3, p, BossCanvasSize(body.room), tint,body.actor.VisualFlip);
            else Actor(monsterSheet, 6, 3, Math.Min(2, body.actor.kind) * 6 + 5, p, body.actor.kind == 2 ? 106 : 96, tint,body.actor.VisualFlip);
            if (body.actor.boss && progress < .6f) Atlas(effectSheet, 4, 4, 15, new Rect(p.x - 52, p.y - 80 - progress * 40, 104, 104), new Color(.8f, .92f, .84f, .7f * (1 - progress)));
        }
        private int NpcId(string kind)
        {
            if (kind == "guild") return 0; if (kind == "garden") return 1; if (kind == "forge") return 2;
            if (kind == "cook" || kind == "rest") return 3; if (kind == "fish") return 4;
            if (kind == "scout" || kind == "returnscout") return 5; if (kind == "mentor") return 6;
            if (kind == "envoy") return 7; if (kind == "captain") return 8; if (kind == "tinker") return 9;
            if (kind == "keeper") return 10; if (kind == "council") return 11;
            if (kind.StartsWith("castaway")) return 12 + (kind.EndsWith("1") ? 1 : 0);
            return kind == "innGuest" ? 15 : 14;
        }
        private void DrawNpc(string kind, Point position, int id = -1)
        {
            Point p = ScreenPoint(position);
            Box(p.x - 13, p.y - 2, 26, 5, new Color(.025f, .04f, .07f, .32f));
            bool occludes = Math.Abs(position.x - Game.save.x) < .85f && Game.save.y < position.y && Game.save.y > position.y - 1.8f;
            Actor(npcSheet, 4, 4, id < 0 ? NpcId(kind) : id, p, 96, occludes ? new Color(1, 1, 1, .45f) : Color.white);
        }
    }
}
