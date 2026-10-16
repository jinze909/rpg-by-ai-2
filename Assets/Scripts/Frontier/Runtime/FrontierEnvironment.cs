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
            if (tile == '%') return IsSurfaceWater(tile,room,Game.LowTide) ? 8 : 13;
            if (tile == '#') return room == 3 || room == 6 ? 10 : room == 7 ? 14 : 9;
            if (room == 9) return 11;
            if (tile == '=') return room == 5 ? 11 : room == 6 ? 2 : room == 3 ? 6 : room == 4 ? 12 : room == 8 ? 7 : room == 2 ? 15 : 3;
            return room == 1 ? 1 : room == 2 ? 15 : room == 3 ? 6 : room == 4 ? 12 : room == 5 ? 13 : room == 6 ? 4 : room == 7 ? 5 : room == 8 ? 7 : 0;
        }
        // All water FX must agree with the SAME actual tide state used
        // for tile visuals and traversal. Percent cells turn into walkable
        // land at low tide, so neither foam nor shimmer may appear there.
        private static bool IsSurfaceWater(char tile,int room,bool lowTide)
        {
            return tile=='~'||(tile=='%'&&room==5&&!lowTide);
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
            // VISUAL ROLLBACK: the low-detail 32px accent/foam strips
            // overpainted the finely rendered original terrain and river
            // atlas. Retain both experimental resources for future review,
            // but display the unchanged rich Terrain/TerrainVariants art.
            if(tile=='=' && Game.save.room<2)
                DrawAuthoredRoadSeam(x,y,p,seed);
        }
        // The old road boundary was a hard 2px monochrome rectangle on top
        // of the original cobblestone. Keep edge contrast but borrow pixels
        // FROM the approved TerrainVariants stone row, never a new coarse
        // strip atlas. This is static material, so reduced-motion and
        // low-effects accessibility keep the same navigable road outline.
        private void DrawAuthoredRoadSeam(int x,int y,Point p,int seed)
        {
            if(terrainVariants==null)return;
            string[] map=Game.Map.rows;
            bool left=x>0&&map[y][x-1]=='.';
            bool right=x+1<map[y].Length&&map[y][x+1]=='.';
            bool up=y>0&&map[y-1][x]=='.';
            bool down=y+1<map.Length&&map[y+1][x]=='.';
            if(!left&&!right&&!up&&!down)return;
            float col=(seed%4)*48f;
            // Third row from top, i.e. y=48..96 in bottom-origin
            // Unity UV space, contains the authored stone/road material.
            Rect horizontal=new Rect((col+7f)/192f,
                66f/192f,34f/192f,3f/192f);
            Rect vertical=new Rect((col+16f)/192f,
                56f/192f,3f/192f,32f/192f);
            Color previous=GUI.color;
            try
            {
                GUI.color=new Color(.79f,.81f,.76f,.54f*worldActorOpacity);
                if(left)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+1,3,Tile-2),
                    terrainVariants,vertical);
                if(right)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+Tile-4,p.y+1,3,Tile-2),
                    terrainVariants,vertical);
                if(up)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+1,Tile-2,3),
                    terrainVariants,horizontal);
                if(down)GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+1,p.y+Tile-4,Tile-2,3),
                    terrainVariants,horizontal);
            }
            finally {GUI.color=previous;}
        }
        // Source-preserving water animation: sample a small HAND-PAINTED
        // passage of the actual 48px water terrain variant, not a fresh 8px
        // rectangle or the rejected 32px CoastlineRemaster. The water tile
        // itself never swaps frames or shifts UV, so shoreline seams remain
        // stable. One rare light catch per seeded location; alpha changes
        // continuously, not the detailed artwork or collision.
        private void DrawAuthoredWaterShimmer(int x,int y,char tile,Point p)
        {
            if (terrainVariants == null ||
                Game.save.reducedMotion || Game.save.effectsQuality==0 ||
                !IsSurfaceWater(tile,Game.save.room,Game.LowTide))
                return;
            int seed=PreparedTileSeed(x,y);
            if(seed%6!=0)return;
            const float sourceCell=48f;
            float variant=(seed/7)%4;
            // TerrainVariants.png is 192x192; its BOTTOM row contains
            // four original richly painted cyan wave paintings. Crop only
            // existing authored pixels. No low-res overlay atlas is used.
            WorldVisualLook visual=CurrentWorldVisualLook;
            float opacity=visual.waterGlintAlpha+
                visual.waterGlintAlpha*(.5f+.5f*
                    (float)Math.Sin(clock*visual.waterGlintRate+
                        (seed%37)*.31f));
            if(Game.save.effectsQuality==1)opacity*=.64f;
            float xOffset=9f+(seed%9);
            float yOffset=6f+(seed/13)%18;
            Rect uv=new Rect((variant*sourceCell+11f)/192f,
                17f/192f,25f/192f,9f/192f);
            Color original=GUI.color;
            try
            {
                GUI.color=new Color(1.05f,1.08f,1.13f,
                    opacity*worldActorOpacity);
                GUI.DrawTextureWithTexCoords(
                    new Rect(p.x+xOffset,p.y+yOffset,25f,9f),
                    terrainVariants,uv);
            }
            finally{GUI.color=original;}
        }

        // Source-preserving shoreline animation: one subdued crest sampled
        // from the existing high-detail 48px water painting, never a coarse
        // newly drawn rectangle. Its center stays anchored to the authored
        // water tile. Side-facing crests rotate as a SINGLE complete sample.
        // Reduced-motion/low-FX modes never receive moving embellishment.
        private void DrawAuthoredShorelineWash(int x,int y,char tile,Point p)
        {
            if(terrainVariants==null || Game.save.reducedMotion ||
                Game.save.effectsQuality==0 ||
                !IsSurfaceWater(tile,Game.save.room,Game.LowTide))
                return;
            int seed=PreparedTileSeed(x,y);
            // Sparse stable shorebreak instead of a solid bright tile border.
            if((seed&1)!=0)return;
            bool north=!WaterAt(x,y-1),east=!WaterAt(x+1,y),
                south=!WaterAt(x,y+1),west=!WaterAt(x-1,y);
            if(!north&&!east&&!south&&!west)return;
            WorldVisualLook look=CurrentWorldVisualLook;
            float strength=Math.Min(.12f,look.waterGlintAlpha*.88f);
            float wave=(float)Math.Sin(clock*look.waterGlintRate+
                (seed%37)*.31f);
            float opacity=strength*(.72f+.16f*wave);
            if(Game.save.effectsQuality==1)opacity*=.64f;
            int variant=(seed/7)%4;
            Rect uv=new Rect((variant*48f+8f)/192f,
                18f/192f,24f/192f,7f/192f);
            Matrix4x4 original=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.color=new Color(1.04f,1.09f,1.14f,
                    opacity*worldActorOpacity);
                // The old first-match if/else left one edge of corner
                // riverbanks dry. Each physically exposed side now has a
                // separate authored wave sample, with one stable tile root.
                // Individual edge transforms are RESET before the next
                // sample so turns never inherit another edge's rotation.
                for(int edge=0;edge<4;edge++)
                {
                    bool exposed=edge==0?north:edge==1?east:
                        edge==2?south:west;
                    if(!exposed)continue;
                    float cx=p.x+Tile*.5f,cy=p.y+Tile*.5f;
                    bool vertical=edge==1||edge==3;
                    if(edge==0)cy=p.y+5f;
                    else if(edge==1)cx=p.x+Tile-5f;
                    else if(edge==2)cy=p.y+Tile-5f;
                    else cx=p.x+5f;
                    GUI.matrix=vertical?GuiGeometry.RotateAroundPivot(
                        original,new Vector2(cx,cy),90f):original;
                    GUI.DrawTextureWithTexCoords(
                        new Rect(cx-12f,cy-3.5f,24f,7f),
                        terrainVariants,uv);
                }
            }
            finally
            {
                GUI.matrix=original;
                GUI.color=originalColor;
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
            int row=IsSurfaceWater(tile,region,Game.LowTide)?3:
                region==7?1:region==3||region==6||tile=='='?2:
                region==5?3:region==0||region==1?0:2;
            int variant=(seed/7)%4;
            Atlas(terrainAccentRemaster,4,4,row*4+variant,
                new Rect(p.x,p.y,Tile,Tile),new Color(1,1,1,.82f));
        }

        // A shoreline is defined by a real water/land neighbor, never a
        // pseudo-random foam tile in the middle of an ocean. Use the same
        // current tide rule as SurfaceId, preserving walkability unchanged.
        private bool WaterAt(int x,int y)
        {
            if(y<0||y>=Game.Map.rows.Length)return true;
            string row=Game.Map.rows[y];
            if(x<0||x>=row.Length)return true;
            char tile=row[x];
            return IsSurfaceWater(tile,Game.save.room,Game.LowTide);
        }
        private void DrawRemasteredCoastline(int x,int y,char tile,Point p,int seed)
        {
            if(coastlineRemaster==null||
                !IsSurfaceWater(tile,Game.save.room,Game.LowTide))return;
            int variant=(seed/11)%4;
            Rect area=new Rect(p.x,p.y,Tile,Tile);
            Color tide=new Color(1,1,1,Game.save.room==7?.77f:.88f);
            // Four individually painted border orientations. The source
            // canvas and renderer are tile-anchored; no stretching a free
            // floating decoration around NPCs, buildings or actors.
            if(!WaterAt(x,y-1))
                Atlas(coastlineRemaster,4,4,variant,area,tide);
            if(!WaterAt(x+1,y))
                Atlas(coastlineRemaster,4,4,4+variant,area,tide);
            if(!WaterAt(x,y+1))
                Atlas(coastlineRemaster,4,4,8+variant,area,tide);
            if(!WaterAt(x-1,y))
                Atlas(coastlineRemaster,4,4,12+variant,area,tide);
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
            if(IsSurfaceWater(tile,region,Game.LowTide))
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
            if (detailSheet == null || Game.save.room == 9 ||
                (tile != '.' && tile != '%') ||
                IsSurfaceWater(tile,Game.save.room,Game.LowTide))
                return; // High-tide shoals are water, not a bed for dry plants.
            int seed = PreparedTileSeed(x, y);
            if (seed % 17 != 0 || GroundDetailOccluded(x, y)) return;
            Point world = new Point(x + .35f + seed % 5 * .07f, y + .65f);
            int room = Game.save.room;
            int[] options = room == 6 || room == 3 || room == 8 ? dryDetails : room == 7 ? snowDetails : room == 2 || room == 4 ? ruinDetails : greenDetails;
            int id = options[(seed / 17) % options.Length];
            if (id == 9) id = 4; // Large fallen trunks belong to fixed solid edge scenery.
            float size = id == 10 ? 33 : id == 8 ? 38 : 23;
            Point foot=ScreenPoint(world);
            bool flip=(seed & 1)==1;
            // Ground litter is not a character. Sending every tiny grass
            // tuft/rock through SpriteRenderer -> 128px RenderTexture was
            // allocating offscreen actor targets for non-actors on map
            // entry. Sample the SAME accepted Details.png cell directly.
            // Keep original outline-mask behaviour in outline review:
            // Actor() bypasses the native capture for drawingOutline.
            if(drawingOutline)
            {
                Actor(detailSheet,4,4,id,foot,size,new Color(1,1,1,.8f),flip);
                return;
            }
            Rect sourceBounds=new Rect(foot.x-size*.5f,
                foot.y-size*120f/128f,size,size);
            Matrix4x4 canvas=GUI.matrix;
            try
            {
                // Only living green tufts/flowers sway, NEVER stone, snow
                // debris or harvested paths. Rotate ONE complete authored
                // Sprite around the same 120/128 painted boot/soil pivot.
                if((id==0||id==1||id==10)&&
                    !Game.save.reducedMotion&&Game.save.effectsQuality>0)
                {
                    float phase=(seed%997)*.017f;
                    float wind=(float)(Math.Sin(clock*.91f+phase)*.73+
                        Math.Sin(clock*1.61f+phase*.67f)*.27);
                    float degrees=wind*CurrentWorldVisualLook.groundFoliageSwayDegrees;
                    GUI.matrix=GuiGeometry.RotateAroundPivot(canvas,
                        new Vector2(foot.x,foot.y),degrees);
                }
                Atlas(detailSheet,4,4,id,sourceBounds,
                    new Color(1f,1f,1f,.8f),flip);
            }
            finally { GUI.matrix=canvas; }
        }
        private void DrawRootedPlant(Decoration prop, Rect rect, Color tint)
        {
            if (worldProps == null)
            {
                PropImage(prop.kind,prop.variant,rect.x,rect.y,rect.width,rect.height,tint);
                return;
            }
            // Keep the ACCEPTED tree/palm/snowpine painting intact. The
            // previous 32 horizontal GUI slices shifted by different amounts
            // and opened 31 body seams on every windy repaint. One complete
            // original sprite means no split trunk, displaced branches,
            // interpolated pixels or 32x overdraw per tree.
            float side=Math.Min(rect.width,rect.height);
            rect=new Rect(rect.x+(rect.width-side)*.5f,
                rect.y+rect.height-side,side,side);
            int id=prop.kind=="palm"?5:prop.kind=="snowpine"?6:0;
            int seed=TileSeed((int)(prop.x*7),(int)(prop.y*7),Game.save.room);
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalTint=GUI.color;
            try
            {
                // Anchor all gentle foliage motion to the authored foot,
                // NOT the painted canvas centre. The trunk/root must remain
                // at the actual depth-sort/collision location during gusts.
                if (!Game.save.reducedMotion && Game.save.effectsQuality>0)
                {
                    float phase=(seed%997)*.01f;
                    float gust=(float)(Math.Sin(clock*.82f+phase)*.72+
                        Math.Sin(clock*1.43f+phase*1.73f)*.28);
                    WorldVisualLook visual=CurrentWorldVisualLook;
                    float strength=prop.kind=="palm"?visual.palmSwayDegrees:
                        prop.kind=="snowpine"?visual.snowpineSwayDegrees:
                        visual.treeSwayDegrees;
                    Vector2 foot=new Vector2(rect.x+rect.width*.5f,
                        rect.y+rect.height*(120f/128f));
                    GUI.matrix=GuiGeometry.RotateAroundPivot(
                        originalMatrix,foot,gust*strength);
                }
                tint.a*=worldActorOpacity;
                GUI.color=tint;
                GUI.DrawTextureWithTexCoords(rect,worldProps,
                    new Rect(id%4/4f,1f-(id/4+1)/2f,.25f,.5f));
            }
            finally
            {
                GUI.color=originalTint;
                GUI.matrix=originalMatrix;
            }
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
        // Props, furniture and planted herbs are fixed atlas scenery, NOT
        // gameplay actors. Skip native SpriteRenderer -> RenderTexture
        // capture entirely; the exact original pixel cell/foot/tint stays
        // unchanged. Only true actors run the Animator-driven pipeline.
        // Preserve the original gold contour during outline passes.
        private void DrawStaticFieldSprite(Texture2D sheet,int id,Point foot,
            float size,Color tint)
        {
            if(drawingOutline)
            {
                Actor(sheet,4,4,id,foot,size,tint);
                return;
            }
            Atlas(sheet,4,4,id,new Rect(foot.x-size*.5f,
                foot.y-size*120f/128f,size,size),tint);
        }
        private bool DrawFieldProp(string kind, int variant, float x, float y, float w, float h, Color? tint)
        {
            if (fieldProps == null) return false;
            int id = kind == "well" ? 0 : kind == "boat" ? 1 : kind == "column" ? 2 : kind == "obelisk" ? 3 : kind == "crystal" || kind == "ore" ? 4 : kind == "post" ? 5 : kind == "herb" ? 6 : kind == "wood" ? 7 : kind == "bell" ? 8 : kind == "tent" ? 9 : kind == "skiff" ? 10 : kind == "root" ? 11 : kind == "altar" || kind == "engine" ? 12 : kind == "fire" ? 13 : kind == "mirror" ? 14 : kind == "cache" ? 15 : -1;
            Point foot = new Point(x + w / 2, y + h - 8);
            if (id >= 0) { DrawStaticFieldSprite(fieldProps,id,foot,
                Math.Max(w,h),tint??Color.white); return true; }
            id = kind == "crate" ? 6 : kind == "barrel" ? 7 : kind == "fence" ? 11 : kind == "bridge" ? 12 : kind == "bed" ? 13 : kind == "bookshelf" ? 14 : kind == "table" || kind == "counter" ? 15 : -1;
            if (id >= 0 && detailSheet != null)
            {
                DrawStaticFieldSprite(detailSheet,id,foot,
                    Math.Max(w,h),tint??Color.white);
                if (kind == "counter")
                {
                    DrawStaticFieldSprite(detailSheet,15,foot+new Point(-45,0),
                        Math.Max(w,h),tint??Color.white);
                    DrawStaticFieldSprite(detailSheet,15,foot+new Point(45,0),
                        Math.Max(w,h),tint??Color.white);
                }
                return true;
            }
            if (kind == "garden")
            {
                Atlas(terrainSheet, 8, 2, 2, new Rect(x + 6, y + h - 40, w - 12, 32), Color.white);
                for(int row=0;row<3;row++)
                {
                    Box(x+8,y+h-36+row*10,w-16,1,
                        new Color(.16f,.12f,.09f,.35f));
                    if(variant>0)
                        for(int col=0;col<3;col++)
                            DrawStaticFieldSprite(fieldProps,6,
                                new Point(x+17+col*(w-24)/3,
                                    y+h-30+row*10),
                                variant==2?27:18,Color.white);
                }
                return true;
            }
            return false;
        }
    }
}
