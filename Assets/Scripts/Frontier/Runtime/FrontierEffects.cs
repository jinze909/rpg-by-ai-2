using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private void DrawProjectileVisual(Projectile projectile)
        {
            Point p = ScreenPoint(projectile.position); string visual = projectile.visual ?? (projectile.friendly ? projectile.explosive ? "starfire" : "arrow" : "enemyOrb");
            float height = projectile.height; p.y -= height;
            bool arrow = visual == "arrow" || visual == "enemyArrow";
            int id = arrow ? 8 : visual == "starfire" ? 9 : 10;
            float angle = (float)(Math.Atan2(projectile.velocity.y, projectile.velocity.x) * 180 / Math.PI);
            float size = arrow ? 30 : projectile.explosive ? 28 : 22;
            Color tint = projectile.friendly ? Color.white : BossEffectColor(projectile.theme);
            if (Game.save.effectsQuality > 0)
                for (int i = Math.Min(3, (int)(projectile.age * projectile.velocity.Length / .18f)); i > 0; i--)
                {
                    Point tail = ScreenPoint(projectile.position - projectile.velocity.Normalized * i * .18f);
                    tail.y -= height;
                    Atlas(effectSheet, 4, 4, arrow ? 11 : 12, new Rect(tail.x - size / 2, tail.y - size / 2, size, size), new Color(tint.r, tint.g, tint.b, .42f / i), false, angle);
                }
            if (effectSheet != null) Atlas(effectSheet, 4, 4, id, new Rect(p.x - size / 2, p.y - size / 2, size, size), tint, false, arrow ? angle : 0);
            else { Box(p.x - 4, p.y - 4, 8, 8, projectile.friendly ? gold : PixelArt.C("#e39382")); }
        }
        private bool DrawAuthoredEffect(Effect effect)
        {
            if (effectSheet == null || effect.kind == "number" || effect.kind == "break") return false;
            Point p = ScreenPoint(effect.position); float progress = Mathf.Clamp01(1 - effect.life / Math.Max(.001f, effect.maxLife));
            if (effect.kind == "loot")
            {
                int item = effect.variant == 5 ? 9 : effect.variant == 4 ? 8 : effect.variant == 1 ? 5 : 6;
                float lift = (float)Math.Sin(progress * Math.PI) * 25;
                Atlas(iconSheet, 4, 4, item, new Rect(p.x + 12, p.y - 15 - lift, 24, 24), new Color(1, 1, 1, Math.Min(1, (1 - progress) * 2)));
                return true;
            }
            int id; float size = 45, angle = 0;
            switch (effect.kind)
            {
                case "slash": id = 0; size = effect.variant == 2 ? 100 : 78; angle = (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI); break;
                case "spark": id = 3; size = 42; break;
                case "impact": case "hurt": id = 2; size = 38; break;
                case "projectileImpact": id = 4; size = 35; break;
                case "projectileFade": id = effect.variant; size = 26; angle = (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI); break;
                case "magicImpact": id = 5; size = effect.radius > 0 ? effect.radius * Tile * 2 : 67; break;
                case "release": id = 13; size = 33; break;
                case "frost": id = 6; size = 4 * Tile * 2; break;
                case "burst": id = effect.variant == 0 ? 7 : 5; size = effect.radius > 0 ? effect.radius * Tile * 2 : 145; break;
                case "heal": id = 13; size = 65; break;
                case "defeat": id = 15; size = effect.variant == 1 ? 90 : 42; break;
                case "discover": id = 13; size = 56; break;
                case "danger": id = BossEffectFrame(effect.variant); size = effect.radius > 0 ? effect.radius * Tile * 2 : 116; break;
                case "enemyRelease":
                    int room = effect.variant >= 10 ? effect.variant - 10 : 0;
                    id = room > 0 ? BossEffectFrame(room) : effect.variant == 2 ? 7 : effect.variant == 1 ? 13 : 0;
                    size = room > 0 ? 96 : effect.variant == 2 ? 94 : 48;
                    angle = id == 0 || id == 1 || id == 12 ? (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI) : 0; break;
                case "dash": id = 11; size = 38; angle = (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI); break;
                default: id = 14; size = 52; break;
            }
            // Discrete authored texture plus a short, restrained expansion and fade.
            size *= .8f + progress * .35f;
            Color color = new Color(1, 1, 1, Math.Min(1, (1 - progress) * 1.7f));
            if (effect.kind == "danger" || effect.kind == "enemyRelease" && effect.variant >= 10)
            { Color theme = BossEffectColor(effect.kind == "danger" ? effect.variant : effect.variant - 10); color.r = theme.r; color.g = theme.g; color.b = theme.b; }
            if (effect.kind == "projectileFade") color.a *= .65f;
            if (effect.kind != "slash" && effect.kind != "impact" && effect.kind != "spark" && effect.kind != "projectileImpact") color.a *= Math.Min(1, progress * 12 + .15f);
            if (IsGroundEffect(effect)) color.a *= effect.kind == "frost" ? .42f : .62f;
            if (effect.kind == "heal") color = new Color(.65f, 1, .8f, color.a);
            float height = effect.kind == "impact" || effect.kind == "hurt" || effect.kind == "spark" || effect.kind == "slash" || effect.kind == "projectileImpact" || effect.kind == "magicImpact" || effect.kind == "release" || effect.kind == "heal" ? 30 : effect.kind == "defeat" ? 18 : effect.kind == "enemyRelease" && id != 7 ? 38 : 0;
            if (effect.height >= 0) height = effect.height;
            Atlas(effectSheet, 4, 4, id, new Rect(p.x - size / 2, p.y - size / 2 - height, size, size), color, false, angle);
            if (effect.kind == "danger" || effect.kind == "frost" || effect.kind == "burst" || effect.kind == "magicImpact" && effect.radius > 0)
            {
                float radius = effect.kind == "frost" ? 4 : effect.kind == "burst" || effect.kind == "magicImpact" ? effect.radius : effect.radius > 0 ? effect.radius : .4f + progress * 1.3f;
                Color ring = effect.kind == "frost" || effect.kind == "magicImpact" || effect.kind == "burst" && effect.variant == 2 ? mint : effect.kind == "danger" ? PixelArt.C("#df8a68") : gold;
                if (radius > 0) Ring(effect.position, radius, new Color(ring.r, ring.g, ring.b, .48f * (1 - progress)), 32);
            }
            return true;
        }
        private static bool IsGroundEffect(Effect effect)
        { return effect.kind == "danger" || effect.kind == "frost" || effect.kind == "burst"; }
        private static int BossEffectFrame(int room)
        { return room == 1 ? 1 : room == 2 ? 12 : room == 3 ? 7 : room == 4 ? 5 : room == 5 ? 12 : room == 6 ? 7 : room == 7 ? 6 : room == 8 ? 5 : 7; }
        private static Color BossEffectColor(int room)
        { return room == 2 || room == 5 ? new Color(.7f, .9f, .82f) : room == 7 ? new Color(.75f, .87f, 1) : room == 4 || room == 8 ? new Color(.87f, .81f, 1) : room == 6 ? new Color(1, .83f, .6f) : new Color(1, .9f, .78f); }
        private void DrawTelegraph(Enemy enemy)
        {
            float radius = GameCore.EnemyAreaRadius(enemy);
            float duration = enemy.windupDuration > 0 ? enemy.windupDuration : enemy.boss ? 1.1f : .65f;
            float progress = Mathf.Clamp01(1 - enemy.timer / duration);
            Color warning = PixelArt.C("#df8a68");
            if (enemy.boss && (enemy.target - enemy.Position).Length > 3) DrawRay(enemy.Position, enemy.target, new Color(warning.r, warning.g, warning.b, .18f + progress * .15f));
            Ring(enemy.target, radius, new Color(warning.r, warning.g, warning.b, .85f), 48);
            Ring(enemy.target, radius * (.2f + progress * .8f), new Color(warning.r, warning.g, warning.b, .35f + progress * .35f), 32);
            Point p = ScreenPoint(enemy.target);
            Atlas(effectSheet, 4, 4, 14, new Rect(p.x - radius * Tile, p.y - radius * Tile, radius * Tile * 2, radius * Tile * 2), new Color(warning.r, warning.g, warning.b, .1f + progress * .13f));
        }
        private void DrawEnemyWarning(Enemy enemy)
        {
            if(enemy.state!=1)return;
            // The amber tether marks the exact traveler-targeted strike lane.
            // Keep it static for reduced-motion mode and behind the existing
            // melee arc/area warning so players can plan a bodyguard intercept.
            if (enemy.targetingWayfarer && Game.WayfarerEncounterRoom == Game.save.room)
            {
                float progress = Mathf.Clamp01(1 - enemy.timer / Math.Max(.01f, enemy.windupDuration));
                Color tether = PixelArt.C("#f4b58b");
                float opacity = Game.save.reducedMotion ? .4f : .24f + progress * .25f;
                DrawRay(enemy.Position, enemy.target, new Color(tether.r, tether.g, tether.b, opacity));
                Ring(enemy.target, .34f, new Color(tether.r, tether.g, tether.b, .65f), 20);
            }
            if(enemy.boss||enemy.kind==2){DrawTelegraph(enemy);return;}
            float progress=Mathf.Clamp01(1-enemy.timer/Math.Max(.01f,enemy.windupDuration));
            Color color=new Color(.875f,.54f,.408f,.35f+progress*.4f);
            if(enemy.kind==1)
            {
                Point muzzle=GameCore.WeaponOrigin(enemy.Position,1,enemy.facing),lift=new Point(0,GameCore.WeaponHeight(1,enemy.facing)/Tile);
                DrawRay(muzzle-lift,enemy.target-lift,color);return;
            }
            // Readability uses the same center, radius and angular bound as damage.
            // This thin open sector supplements the claw wind-up without covering it.
            float angle=(float)Math.Atan2(enemy.facing.y,enemy.facing.x),half=(float)Math.Acos(GameCore.EnemyMeleeCosine);
            Point center=ScreenPoint(enemy.Position);float radius=GameCore.EnemyMeleeRadius*Tile;
            for(int i=0;i<=32;i++)
            {
                float a=angle-half+half*2*i/32;
                Box(center.x+(float)Math.Cos(a)*radius-1,center.y+(float)Math.Sin(a)*radius-1,2,2,color);
            }
            for(int side=-1;side<=1;side+=2)
            {
                float a=angle+half*side;
                Point edge=enemy.Position+new Point((float)Math.Cos(a),(float)Math.Sin(a))*GameCore.EnemyMeleeRadius;
                DrawRay(enemy.Position,edge,new Color(color.r,color.g,color.b,color.a*.45f));
            }
        }
    }
}
