using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly int[] dryDetails = { 2, 4, 5, 8 }, snowDetails = { 2, 4, 9 }, ruinDetails = { 2, 3, 4, 8 }, greenDetails = { 0, 1, 3, 4, 10 };
        private int SurfaceId(char tile)
        {
            int room = Game.save.room;
            if (tile == '~') return 8;
            if (tile == '%') return Game.save.room == 5 && !Game.LowTide ? 8 : 13;
            if (tile == '#') return room == 3 || room == 6 ? 10 : room == 7 ? 14 : 9;
            if (room == 9) return 11;
            if (tile == '=') return room == 5 ? 11 : room == 6 ? 2 : room == 3 ? 6 : room == 4 ? 12 : room == 8 ? 7 : room == 2 ? 15 : 3;
            return room == 1 ? 1 : room == 2 ? 15 : room == 3 ? 6 : room == 4 ? 12 : room == 5 ? 13 : room == 6 ? 4 : room == 7 ? 5 : room == 8 ? 7 : 0;
        }
        private static int TileSeed(int x, int y, int room)
        { unchecked { int n = x * 73856093 ^ y * 19349663 ^ (room + 1) * 83492791; return n & 0x7fffffff; } }
        private void DrawSurface(int x, int y, char tile, Point p)
        {
            if (terrainSheet == null) { Image(art.Tile(Game.save.room, tile, (x * 7 + y * 11) % 4, PixelArt.C(Game.Map.color)), p.x, p.y, Tile + .5f, Tile + .5f); return; }
            int seed = PreparedTileSeed(x, y), id = SurfaceId(tile);
            float value = .97f + seed % 5 * .014f;
            int row = id == 0 ? 0 : id == 1 ? 1 : id == 3 ? 2 : id == 8 ? 3 : -1;
            if (row >= 0 && terrainVariants != null) Atlas(terrainVariants, 4, 4, row * 4 + seed % 4, new Rect(p.x, p.y, Tile, Tile), new Color(value, value, value), (seed & 4) == 0);
            else Atlas(terrainSheet, 8, 2, id, new Rect(p.x, p.y, Tile, Tile), new Color(value, value, value), id != 11 && id != 9 && (seed & 1) == 1);
            DrawRemasteredMaterial(x,y,tile,p,seed);
            if (tile == '=' && Game.save.room < 2)
            {
                // Dark, narrow soil edges make the road shape read above busy grass.
                if (x > 0 && Game.Map.rows[y][x - 1] == '.') Box(p.x, p.y, 2, Tile, new Color(.17f, .22f, .15f, .28f));
                if (x + 1 < Game.Map.rows[y].Length && Game.Map.rows[y][x + 1] == '.') Box(p.x + Tile - 2, p.y, 2, Tile, new Color(.17f, .22f, .15f, .28f));
                if (y > 0 && Game.Map.rows[y - 1][x] == '.') Box(p.x, p.y, Tile, 2, new Color(.17f, .22f, .15f, .28f));
                if (y + 1 < Game.Map.rows.Length && Game.Map.rows[y + 1][x] == '.') Box(p.x, p.y + Tile - 2, Tile, 2, new Color(.17f, .22f, .15f, .28f));
            }
        }
        // Pixel-authored material accents from TerrainAccentRemaster.png:
        // additional vegetation clusters, snow crust, gravel and actual
        // water highlights. These overlay the original mature terrain atlas
        // rather than recoloring its authored roads or altering collision.
        private void DrawRemasteredMaterial(int x,int y,char tile,Point p,int seed)
        {
            if(terrainAccentRemaster==null||tile=='#'||tile=='f'||
                tile=='v'||tile=='w'||tile=='b'||seed%7!=0)return;
            int region=Game.save.room;
            int row=tile=='~'||tile=='%'&&region==5&&!Game.LowTide?3:
                region==7?1:region==3||region==6||tile=='='?2:
                region==5?3:region==0||region==1?0:2;
            int variant=(seed/7)%4;
            Atlas(terrainAccentRemaster,4,4,row*4+variant,
                new Rect(p.x,p.y,Tile,Tile),new Color(1,1,1,.82f));
        }

        // Hand-authored regions now carry subtle clusters of moss, gravel,
        // salt, snow and wind-worn sediment across the NEW atlas only. These
        // overlays never alter tile collisions or original campaign artwork.
        // A coarse deterministic field gives continuous 5-tile patches rather
        // than noisy single-tile polygons that resemble loading pop-in.
        private void DrawAnnexBiomeLayers(int x, int y, char tile, Point p)
        {
            int region=Game.save.room;
            if(region<0||region>7||(x<36&&y<24))return;
            if(tile=='#'||tile=='f'||tile=='v'||tile=='w'||tile=='b')return;
            int patch=TileSeed(x/5,y/5,region),individual=PreparedTileSeed(x,y);
            if(tile=='~'||tile=='%')
            {
                // A deterministic waterline shimmer is optional animation,
                // without any changing collision or procedural tile creation.
                if(individual%6!=0)return;
                float wave=Game.save.reducedMotion||Game.save.effectsQuality==0?0:
                    (float)Math.Sin(clock*1.4f+(patch%17)*.42f)*2;
                Box(p.x+7,p.y+Tile*.54f+wave,Tile*.43f,1.5f,
                    new Color(.75f,.87f,.93f,.15f));
                return;
            }
            // Adjacent authored tiles share a continuous gradient, rather
            // than the hard 5x5 material squares of the old static overlay.
            float biome=PreparedBiomeVariation(x,y);
            if(biome<.38f)return;
            float opacity=.04f+.16f*WorldStreaming.Smooth01(
                (biome-.38f)/.62f);
            Color earth=region==0?new Color(.35f,.37f,.19f,opacity):
                region==1?new Color(.13f,.32f,.19f,opacity):
                region==2?new Color(.35f,.45f,.47f,opacity):
                region==3?new Color(.48f,.23f,.17f,opacity):
                region==4?new Color(.45f,.44f,.38f,opacity):
                region==5?new Color(.18f,.38f,.45f,opacity):
                region==6?new Color(.64f,.43f,.20f,opacity):
                new Color(.72f,.84f,.93f,opacity);
            Box(p.x+2,p.y+2,Tile-4,Tile-4,earth);
            if(individual%11==0)
            {
                Color vein=region==7?new Color(.87f,.94f,1f,.22f):
                    region==6?new Color(.47f,.30f,.12f,.24f):
                    region==1?new Color(.24f,.42f,.17f,.23f):
                    new Color(.66f,.65f,.52f,.17f);
                Box(p.x+5+individual%8,p.y+Tile*.67f,Tile*.25f,1.5f,vein);
                if(tile=='=')
                    Box(p.x+Tile*.54f,p.y+Tile*.3f,Tile*.19f,1f,vein);
            }
        }

        private void DrawGroundDetail(int x, int y, char tile, Point p)
        {
            if (detailSheet == null || Game.save.room == 9 || tile != '.' && tile != '%') return;
            int seed = PreparedTileSeed(x, y);
            if (seed % 17 != 0 || GroundDetailOccluded(x, y)) return;
            Point world = new Point(x + .35f + seed % 5 * .07f, y + .65f);
            int room = Game.save.room;
            int[] options = room == 6 || room == 3 || room == 8 ? dryDetails : room == 7 ? snowDetails : room == 2 || room == 4 ? ruinDetails : greenDetails;
            int id = options[(seed / 17) % options.Length];
            if (id == 9) id = 4; // Large fallen trunks belong to fixed solid edge scenery.
            float size = id == 10 ? 33 : id == 8 ? 38 : 23;
            Actor(detailSheet, 4, 4, id, ScreenPoint(world), size, new Color(1, 1, 1, .8f), (seed & 1) == 1);
        }
        private void DrawRootedPlant(Decoration prop, Rect rect, Color tint)
        {
            if (worldProps == null) { PropImage(prop.kind, prop.variant, rect.x, rect.y, rect.width, rect.height, tint); return; }
            float side=Math.Min(rect.width,rect.height); rect=new Rect(rect.x+(rect.width-side)/2,rect.y+rect.height-side,side,side);
            int id = prop.kind == "palm" ? 5 : prop.kind == "snowpine" ? 6 : 0;
            int seed = TileSeed((int)(prop.x * 7), (int)(prop.y * 7), Game.save.room);
            float phase = seed % 997 * .01f, speed = .8f + seed % 7 * .07f, amplitude = .65f + seed % 5 * .12f;
            Color previous = GUI.color;
            try
            {
                tint.a*=worldActorOpacity;
                GUI.color = tint;
                const int strips = 32;
                for (int i = 0; i < strips; i++)
                {
                    float y = i / (float)strips, next = (i + 1) / (float)strips;
                    float weight = y < .65f ? (1 - y / .65f) * (1 - y / .65f) : 0;
                    float wind = Game.save.reducedMotion || Game.save.effectsQuality == 0 ? 0 : (float)Math.Sin(clock * speed + phase + y * 2) * amplitude * weight;
                    GUI.DrawTextureWithTexCoords(new Rect(rect.x + wind, rect.y + rect.height * y, rect.width, rect.height / strips), worldProps,
                        new Rect(id % 4 / 4f, 1 - (id / 4 + next) / 2f, .25f, (next - y) / 2));
                }
            }
            finally { GUI.color = previous; }
        }
        private void DrawArenaBoundary()
        {
            if (!Game.GateBarrierActive) return;
            float opacity = !Game.GateOpen ? 1 : Math.Max(.12f, Game.gateOpeningTime / .55f);
            for (int y = 2; y < 14; y++) DrawBarrierCell(26, y, opacity);
            for (int x = 27; x < 34; x++) DrawBarrierCell(x, 13, opacity);
        }
        private void DrawBarrierCell(int x, int y, float opacity)
        {
            Point p = ScreenPoint(new Point(x, y));
            Color stone = Game.save.room == 1 ? new Color(.17f, .28f, .2f, opacity) : new Color(.22f, .3f, .34f, opacity);
            Box(p.x, p.y, Tile, Tile, stone); Frame(p.x + 1, p.y + 1, Tile - 2, Tile - 2, new Color(.6f, .65f, .43f, opacity * .65f));
            // The visible boundary has the same full tile extent as collision.
            Atlas(fieldProps, 4, 4, Game.save.room == 1 ? 11 : 4, new Rect(p.x - 2, p.y - 9, Tile + 4, Tile + 12), new Color(1, 1, 1, opacity));
            if ((x + y) % 3 == 0) Diamond(p.x + Tile / 2, p.y + Tile / 2, 3, new Color(gold.r, gold.g, gold.b, opacity));
        }
        private bool DrawFieldProp(string kind, int variant, float x, float y, float w, float h, Color? tint)
        {
            if (fieldProps == null) return false;
            int id = kind == "well" ? 0 : kind == "boat" ? 1 : kind == "column" ? 2 : kind == "obelisk" ? 3 : kind == "crystal" || kind == "ore" ? 4 : kind == "post" ? 5 : kind == "herb" ? 6 : kind == "wood" ? 7 : kind == "bell" ? 8 : kind == "tent" ? 9 : kind == "skiff" ? 10 : kind == "root" ? 11 : kind == "altar" || kind == "engine" ? 12 : kind == "fire" ? 13 : kind == "mirror" ? 14 : kind == "cache" ? 15 : -1;
            Point foot = new Point(x + w / 2, y + h - 8);
            if (id >= 0) { Actor(fieldProps, 4, 4, id, foot, Math.Max(w, h), tint ?? Color.white); return true; }
            id = kind == "crate" ? 6 : kind == "barrel" ? 7 : kind == "fence" ? 11 : kind == "bridge" ? 12 : kind == "bed" ? 13 : kind == "bookshelf" ? 14 : kind == "table" || kind == "counter" ? 15 : -1;
            if (id >= 0 && detailSheet != null)
            {
                Actor(detailSheet, 4, 4, id, foot, Math.Max(w, h), tint ?? Color.white);
                if (kind == "counter") { Actor(detailSheet, 4, 4, 15, foot + new Point(-45, 0), Math.Max(w, h), tint ?? Color.white); Actor(detailSheet, 4, 4, 15, foot + new Point(45, 0), Math.Max(w, h), tint ?? Color.white); }
                return true;
            }
            if (kind == "garden")
            {
                Atlas(terrainSheet, 8, 2, 2, new Rect(x + 6, y + h - 40, w - 12, 32), Color.white);
                for (int row = 0; row < 3; row++) { Box(x + 8, y + h - 36 + row * 10, w - 16, 1, new Color(.16f, .12f, .09f, .35f)); if (variant > 0) for (int col = 0; col < 3; col++) Actor(fieldProps, 4, 4, 6, new Point(x + 17 + col * (w - 24) / 3, y + h - 30 + row * 10), variant == 2 ? 27 : 18, Color.white); }
                return true;
            }
            return false;
        }
    }
}
