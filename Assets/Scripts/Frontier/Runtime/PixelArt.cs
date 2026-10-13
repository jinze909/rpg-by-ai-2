using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    // Cached UI primitives and optional fallback art; authored actors are loaded separately.
    public sealed partial class PixelArt : IDisposable
    {
        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> owned = new List<Texture2D>();
        private Color[] pixels;
        private int width, height;
        private void Begin(int w, int h) { width = w; height = h; pixels = new Color[w * h]; }
        private void Box(int x, int y, int w, int h, Color c)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(height, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(width, x + w); xx++) pixels[(height - 1 - yy) * width + xx] = c;
        }
        private void Disc(int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                    if ((x - cx) * (x - cx) / (float)(rx * rx) + (y - cy) * (y - cy) / (float)(ry * ry) < 1) Box(x, y, 1, 1, c);
        }
        public static Color C(string hex)
        { Color c; return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white; }
        private Texture2D End(string key)
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
            t.SetPixels(pixels); t.Apply(false, true); cache[key] = t; owned.Add(t); pixels = null; return t;
        }
        private bool PreparedMask(Texture2D source, string kind, string key, out Texture2D mask)
        {
            string leaf = source.name; int slash = leaf.LastIndexOf('/');
            if (slash >= 0) leaf = leaf.Substring(slash + 1);
            mask = Resources.Load<Texture2D>("Frontier/Art/ActorMasks/" + kind + "_" + leaf);
            if (mask == null || mask.width != source.width || mask.height != source.height) return false;
            cache[key] = mask; return true;
        }
        public Texture2D FlashMask(Texture2D source)
        {
            string key = "flash_" + source.name.Replace("/", "_").Replace(":", "_"); Texture2D t;
            if (cache.TryGetValue(key, out t)) return t;
            if (PreparedMask(source, "flash", key, out t)) return t;
            Begin(source.width, source.height); Color[] original = source.GetPixels();
            for (int i = 0; i < original.Length; i++) pixels[i] = new Color(1, 1, 1, original[i].a);
            return End(key);
        }
        public Texture2D OutlineMask(Texture2D source)
        {
            string key = "outline_" + source.name.Replace("/", "_").Replace(":", "_"); Texture2D t;
            if (cache.TryGetValue(key, out t)) return t;
            if (PreparedMask(source, "outline", key, out t)) return t;
            Begin(source.width, source.height); Color[] original = source.GetPixels();
            int[] neighbours = { -2, 2, -source.width * 2, source.width * 2, -source.width - 1, -source.width + 1, source.width - 1, source.width + 1 };
            for (int y = 2; y < source.height - 2; y++) for (int x = 2; x < source.width - 2; x++)
            {
                int index = y * source.width + x; float edge = 0;
                if (original[index].a > .6f) continue;
                foreach (int offset in neighbours)
                { edge = Math.Max(edge, original[index + offset].a - original[index].a); if (edge > .4f) break; }
                pixels[index] = new Color(1,1,1,edge > .4f ? .9f : 0);
            }
            return End(key);
        }
        public Texture2D Prop(string kind, int variant)
        {
            string key = kind + variant; Texture2D t;
            if (cache.TryGetValue(key, out t)) return t;
            Begin(96, 112);
            Color shadow = new Color(.05f, .12f, .17f, .35f), cream = C("#e5d7b3"), timber = C("#806955");
            Disc(48, 104, 35, 6, shadow);
            if (kind == "tree")
            {
                Box(42, 55, 12, 48, C("#5a5546")); Box(43, 62, 4, 33, C("#897658"));
                Disc(48, 46, 35, 30, C("#254a49")); Disc(35, 40, 24, 25, C("#2f6457"));
                Disc(62, 34, 22, 26, C("#437963")); Disc(41, 20, 21, 16, C("#578e6c"));
                for (int i = 0; i < 9; i++) Box(18 + i * 7, 25 + i % 3 * 12, 5, 2, C("#77a981"));
            }
            else if (kind == "house")
            {
                Color roof = C(variant == 0 ? "#b86658" : variant == 1 ? "#537e90" : "#9d8454");
                Box(8, 47, 80, 56, C("#c6b997")); Box(8, 53, 6, 50, timber); Box(82, 53, 6, 50, timber);
                Box(8, 86, 80, 7, timber); Box(44, 70, 17, 33, C("#4c5355")); Box(46, 72, 13, 29, C("#696751"));
                Box(21, 61, 14, 19, C("#3c5964")); Box(21, 63, 14, 1, cream); Box(27, 61, 2, 19, cream); Box(21, 72, 14, 2, cream);
                Box(68, 61, 10, 17, C("#e6c27f")); Box(71, 60, 2, 19, timber);
                for (int y = 0; y < 42; y++) Box(48 - (y + 10), 7 + y, (y + 10) * 2, 1, roof);
                for (int y = 10; y < 48; y += 7) Box(36 - y / 2, y, 24 + y, 2, Color.Lerp(roof, C("#482e3a"), .25f));
                Box(68, 10, 9, 25, C("#807667")); Box(67, 8, 11, 4, C("#aba18c"));
            }
            else if (kind == "garden")
            {
                Box(8, 66, 80, 35, C("#614f40"));
                for (int y = 68; y < 100; y += 8)
                {
                    Box(10, y, 77, 2, C("#877052"));
                    if (variant == 0) continue;
                    for (int x = 17; x < 85; x += 14)
                    { Box(x, y - 4, 3, 5, C("#a1b97c")); Box(x - 3, y - 2, 8, 2, C("#82a46e")); if (variant == 2) Box(x, y - 6, 3, 3, C("#edd7a4")); }
                }
            }
            else if (kind == "lamp")
            { Box(46, 40, 4, 65, C("#37434b")); Box(40, 40, 16, 4, timber); Box(39, 22, 18, 19, C("#424b48")); Box(42, 25, 12, 12, C("#ffe2a0")); Box(37, 20, 22, 4, cream); }
            else if (kind == "well")
            { Disc(48, 91, 24, 12, C("#7c8c8b")); Disc(48, 86, 19, 10, C("#283f51")); Box(27, 47, 4, 45, timber); Box(64, 47, 4, 45, timber); Box(25, 44, 45, 5, timber); Box(46, 49, 1, 32, cream); }
            else if (kind == "boat")
            { Disc(48, 90, 33, 11, timber); Box(23, 87, 50, 6, C("#be9770")); Box(47, 30, 3, 57, cream); for (int y = 0; y < 42; y++) Box(50, 35 + y, 28 - y / 2, 1, C("#d0c0a2")); }
            else if (kind == "ruin")
            { Box(10, 81, 78, 25, C("#606e7b")); Box(15, 75, 17, 31, C("#8c9694")); Box(65, 75, 16, 31, C("#8c9694")); Box(33, 87, 31, 19, C("#314854")); Disc(48, 67, 24, 16, C("#788593")); Disc(48, 67, 14, 12, C("#364b63")); Box(45, 59, 6, 16, C("#89c9c8")); }
            else if (kind == "cache")
            {
                // A real illustrated waterworn cargo chest, rather than the
                // generic fallback tile; independent of imported sprites.
                Color plank=C("#836752"), iron=C("#536e73"), edge=C("#c4a37a");
                Box(19,49,58,51,C("#354751"));
                Box(22,54,52,43,plank);
                for(int row=0;row<3;row++)
                {
                    int y=57+row*12;
                    Box(24,y,48,8,C(row==1?"#9b7959":"#765d4d"));
                    Box(24,y+1,45,2,edge);
                    Box(25,y+7,47,2,C("#4f443d"));
                }
                Box(17,44,62,13,iron);
                Box(20,39,56,11,plank);
                Box(22,41,52,2,edge);
                Box(16,48,7,53,iron);Box(74,48,6,53,iron);
                Box(18,89,60,6,iron);
                Box(45,56,8,35,iron);Box(43,69,12,15,edge);
                Box(46,71,6,10,C("#233d49"));
                Box(24,53,3,3,edge);Box(68,53,3,3,edge);
                Box(24,88,3,3,edge);Box(68,88,3,3,edge);
                Box(34,29,28,9,new Color(.3f,.75f,.81f,.22f));
            }
            else if (kind == "witnessledger")
            {
                // An actual public ledger bound with thread, brass corners and
                // three colored provenance seals; independent of asset imports.
                Color leather=C("#68534c"),page=C("#e6d2a6"),
                    brass=C("#bd9b65"),ink=C("#4b5860");
                Box(20,27,58,77,C("#304653"));
                Box(23,24,53,76,leather);
                Box(29,31,42,60,page);
                for(int j=0;j<6;j++)
                    Box(35,40+j*7,31-j%3*3,2,ink);
                Box(26,25,6,67,brass);Box(25,29,4,4,ink);
                for(int i=0;i<3;i++)
                    Disc(44+i*10,86,4,4,
                        C(i==0?"#83b89c":i==1?"#7aaed4":"#dc9c75"));
                Box(23,22,53,5,brass);Box(23,96,53,5,brass);
            }
            else if (kind == "afterlightanchor")
            {
                // Fractured old-starroot pylons, joined with green civic wiring.
                Color stone=C("#637781"),shade=C("#304754"),metal=C("#bbad7e"),
                    light=C("#9fe4c2"),dark=C("#405e66");
                Box(30,64,36,40,stone);Box(23,88,50,18,shade);
                Box(40,20,18,71,dark);Box(44,12,9,76,metal);
                Disc(48,35,16,17,shade);Disc(48,35,11,12,light);
                Box(47,48,4,39,light);
                for(int i=0;i<3;i++)
                {
                    int x=22+i*27;
                    Disc(x,83,7,4,metal);
                    Box(x-2,77,4,17,light);
                    Disc(x,75,6,7,C(i==0?"#93cba7":i==1?"#91c4de":"#e6b891"));
                }
                Box(23,95,50,4,metal);
            }
            else if (kind == "afterlightoutlet")
            {
                // Constructed public receiver: timber bridge/workbench and
                // lanterns with a visible power conduit; no imported sprite.
                Color plankWood=C("#826954"),panel=C("#617985"),
                    brass=C("#d6bd86"),lantern=C("#f6e1ad"),
                    leaf=C("#8bcaad"),dark=C("#2d4551");
                Box(15,91,68,16,dark);
                Box(18,59,62,41,panel);
                Box(24,63,50,32,plankWood);
                Box(23,66,53,5,brass);
                Box(21,38,8,57,panel);Box(67,38,8,57,panel);
                Box(28,44,40,9,plankWood);
                for(int i=0;i<3;i++)
                {
                    int x=30+i*17;
                    Disc(x,27,9,12,lantern);
                    Box(x-4,22,9,11,brass);
                    Box(x-2,26,5,7,lantern);
                    Box(x-1,38,3,20,leaf);
                }
                Box(42,73,12,14,C("#2e4e59"));
                Disc(48,78,5,5,leaf);
                Box(21,96,54,4,brass);
            }
            else if (kind == "blackoutrefuge")
            {
                // Three emergency lamps and shared sleeping cots under a
                // repaired canopy. Deliberately distinct from boss altar/ruins.
                Color brass=C("#d6b87d"),wood=C("#776557"),
                    cloth=C("#7e9fac"),roof=C("#4a6373"),
                    glow=C("#f8da94"),dark=C("#2e4554");
                Box(10,86,78,20,dark);
                Box(15,78,68,24,wood);
                Box(7,40,82,11,roof);
                Box(12,29,71,18,cloth);
                for(int i=0;i<3;i++)
                {
                    int x=22+i*27;
                    Box(x,39,4,47,wood);
                    Box(x-5,40,14,4,brass);
                    Disc(x+2,50,7,9,glow);
                    Box(x+1,45,3,13,C("#fff1c1"));
                }
                Box(23,70,50,19,C("#465b65"));
                Box(27,73,19,9,C("#c9d4bf"));
                Box(50,73,19,9,C("#c9d4bf"));
                Box(25,88,48,4,brass);
                Box(43,17,11,13,glow);
                Box(46,12,5,21,C("#ffe9b9"));
                Box(36,20,26,5,brass);
                for(int i=0;i<3;i++)
                    Disc(25+i*23,92,4,4,C(i==0?"#86c7ac":i==1?"#9ac9d6":"#eab88b"));
            }
            else if (kind == "foundry")
            {
                // Riveted industrial iron furnace with a distinctive active firebox.
                Color metal=C("#61717b"),steel=C("#a1a7a4"),dark=C("#283b48"),
                    ember=C("#f0a35f"),flame=C("#ffe0a0");
                Box(16,65,64,39,dark);Box(20,55,56,46,metal);
                Box(12,87,72,13,C("#42525c"));Box(15,95,66,8,steel);
                for(int i=0;i<3;i++)
                {
                    int x=25+i*19;
                    Box(x,26,11,39,dark);Box(x+2,18,7,39,metal);
                    Box(x+2,15,7,5,steel);
                }
                Box(32,62,32,37,dark);Box(36,67,24,26,C("#8a443c"));
                Box(38,70,20,18,ember);Box(41,72,14,12,flame);
                Box(20,63,5,35,steel);Box(71,63,5,35,steel);
                for(int y=0;y<2;y++)for(int x=0;x<4;x++)
                    Disc(23+x*17,76+y*19,2,2,C("#b8b1a0"));
                Box(26,51,44,7,steel);
            }
            else if (kind == "pressurevent")
            {
                // Manual handwheel and cooling pipe; visually not a checkpoint post.
                Color iron=C("#627781"),rim=C("#c4ae86"),dark=C("#304754");
                Box(45,48,10,55,iron);
                Box(22,88,54,13,dark);Box(23,91,51,5,iron);
                Disc(48,55,26,25,rim);Disc(48,55,20,19,dark);
                Disc(48,55,15,14,iron);
                Box(44,36,8,38,rim);Box(29,51,38,8,rim);
                Disc(48,55,7,7,C("#f1cf8a"));
                Box(46,26,4,12,rim);Box(46,74,4,10,rim);
                Box(17,54,10,3,rim);Box(68,54,11,3,rim);
            }
            else if (kind == "column" || kind == "obelisk")
            { Box(35, 35, 26, 66, C(kind == "column" ? "#798b96" : "#646078")); Box(30, 31, 36, 8, C("#9aa2a2")); Box(31, 99, 35, 6, C("#8a969b")); Box(41, 48, 3, 40, C("#aec4b4")); Box(51, 57, 6, 12, C("#90cfc7")); }
            else if (kind == "crystal" || kind == "ore")
            { for (int i = 0; i < 3; i++) { int x = 22 + i * 18; for (int y = 0; y < 43; y++) Box(x - Math.Min(y / 4, 8), 48 + i % 2 * 14 + y, Math.Min(y / 2, 16) + 1, 1, C(i == 1 ? "#d7b478" : "#6faead")); } }
            else if (kind == "post")
            { Box(43, 58, 10, 45, timber); Box(23, 54, 50, 17, C("#c9b98f")); Box(25, 56, 46, 12, C("#667869")); }
            else if (kind == "herb")
            { for (int i = 0; i < 5; i++) { int x = 28 + i * 10; Box(x, 80 - i % 2 * 6, 3, 23, C("#8dc4aa")); Disc(x, 80 - i % 2 * 6, 5, 4, C("#eee0ae")); } }
            else if (kind == "wood")
            { Box(21, 86, 53, 15, timber); Disc(73, 93, 9, 9, C("#b39269")); Disc(73, 93, 5, 5, C("#8b7054")); Box(23, 89, 42, 2, C("#c3a675")); }
            else if (kind == "lighthouse")
            { Box(30, 38, 37, 66, C("#c2bca1")); Box(27, 84, 43, 10, C("#a36c5a")); Box(29, 59, 39, 7, C("#a36c5a")); Box(25, 32, 46, 10, C("#667d81")); Box(31, 12, 34, 21, C("#3b525c")); Box(36, 17, 24, 11, C("#edd29a")); Box(22, 8, 52, 6, C("#65767a")); Box(41, 87, 15, 18, C("#415963")); }
            else if (kind == "palm")
            { Box(43, 43, 9, 61, C("#b59366")); for (int i = 0; i < 6; i++) Box(42, 50 + i * 8, 11, 2, C("#776848")); Disc(48, 36, 30, 11, C("#628465")); Disc(26, 46, 20, 10, C("#7e9a69")); Disc(70, 42, 20, 12, C("#7d9461")); Box(43, 34, 9, 14, C("#d0ba84")); }
            else if (kind == "snowpine")
            { Box(43, 54, 10, 48, C("#706758")); for (int tier = 0; tier < 4; tier++) for (int y = 0; y < 28; y++) { int spread = y + 6 + tier * 3; Box(48 - spread, 8 + tier * 17 + y, spread * 2, 1, C(y < 7 ? "#d6ded5" : "#587679")); } }
            else if (kind == "bell")
            { Box(23, 32, 6, 74, timber); Box(68, 32, 6, 74, timber); Box(20, 29, 57, 8, cream); Box(44, 38, 8, 9, C("#c5aa77")); for (int y = 0; y < 28; y++) Box(36 - y / 5, 44 + y, 25 + y / 2, 1, C("#b89e6c")); Box(29, 70, 40, 7, C("#d8c095")); Box(46, 76, 5, 9, timber); }
            else if (kind == "tent")
            { for (int y = 0; y < 55; y++) { int spread = 5 + y * 2 / 3; Box(48 - spread, 38 + y, spread * 2, 1, C(y % 9 < 2 ? "#937b61" : "#c6b388")); } Box(39, 70, 20, 31, C("#3e5260")); Box(47, 35, 3, 65, timber); Box(10, 99, 76, 5, timber); }
            else if (kind == "skiff")
            { Disc(48, 92, 36, 10, C("#776d62")); Box(18, 85, 61, 10, C("#b69a72")); Box(47, 23, 3, 62, cream); for (int y = 0; y < 54; y++) Box(50, 27 + y, Math.Max(1, 34 - y / 2), 1, C(y % 13 < 3 ? "#8ea398" : "#e2cf9e")); Box(20, 99, 57, 4, C("#536676")); }
            else if (kind == "root")
            { Box(40, 43, 14, 60, C("#475d60")); for (int i = 0; i < 4; i++) { Box(19 + i * 15, 65 - i % 2 * 14, 7, 39, C("#647e7b")); Box(15 + i * 15, 99, 16, 6, C("#758d7e")); } Disc(48, 44, 22, 25, C("#394e64")); Disc(48, 44, 11, 17, C("#a8c3ac")); Box(45, 27, 6, 34, C("#d6d0ae")); }
            else if (kind == "enemy0")
            { Disc(48, 89, 22, 16, C("#657866")); Disc(48, 83, 22, 15, C("#98b98a")); Box(38, 83, 3, 4, C("#243e3e")); Box(54, 83, 3, 4, C("#243e3e")); Box(44, 91, 8, 2, C("#526a54")); }
            else if (kind == "enemy1")
            { Disc(48, 70, 15, 18, C("#624657")); Box(35, 72, 27, 27, C("#996477")); Box(40, 72, 16, 8, C("#c9c0ae")); Box(44, 73, 3, 3, C("#382c38")); Box(50, 73, 3, 3, C("#382c38")); Box(33, 94, 11, 10, C("#4d4051")); Box(53, 94, 11, 10, C("#4d4051")); Box(66, 70, 3, 30, timber); }
            else if (kind == "enemy2")
            { Box(24, 59, 48, 41, C("#7c8582")); Box(30, 55, 36, 8, C("#a4a795")); Box(17, 67, 10, 25, C("#6c766d")); Box(70, 67, 10, 25, C("#6c766d")); Box(30, 94, 12, 13, timber); Box(56, 94, 12, 13, timber); Box(34, 71, 7, 5, C("#ecc889")); Box(54, 71, 7, 5, C("#ecc889")); }
            else if (kind == "boss")
            { Color body = C(variant == 0 ? "#776d57" : variant == 1 ? "#4e8191" : variant == 2 ? "#9c7958" : "#807294"); Box(15, 34, 66, 62, body); Box(8, 44, 12, 44, Color.Lerp(body, Color.black, .15f)); Box(77, 44, 12, 44, Color.Lerp(body, Color.black, .15f)); Box(19, 88, 20, 20, C("#4b4d53")); Box(58, 88, 20, 20, C("#4b4d53")); Disc(48, 34, 28, 22, body); Box(30, 28, 10, 7, C("#efb87d")); Box(56, 28, 10, 7, C("#efb87d")); Box(39, 52, 18, 18, C("#d9c698")); Box(44, 56, 8, 10, C("#77c0bc")); Box(26, 8, 5, 17, C("#b6a48d")); Box(65, 8, 5, 17, C("#b6a48d")); }
            else // NPC or sign
            { Disc(48, 58, 11, 12, cream); Box(35, 66, 26, 30, C(variant == 3 ? "#628995" : variant == 4 ? "#b5935f" : variant == 5 ? "#9495ad" : variant % 3 == 0 ? "#bd805e" : variant % 3 == 1 ? "#83a29a" : "#aca179")); Box(41, 92, 6, 13, C("#4d535c")); Box(50, 92, 6, 13, C("#4d535c")); Box(35, 46, 26, 7, timber); Box(40, 42, 15, 6, variant >= 3 ? C("#dbd4b7") : timber); Box(42, 57, 2, 3, C("#4b5052")); Box(52, 57, 2, 3, C("#4b5052")); }
            PolishProp(kind, variant); return End(key);
        }
        public void Dispose() { foreach (Texture2D t in owned) UnityEngine.Object.Destroy(t); owned.Clear(); cache.Clear(); pixels = null; }
    }
}
