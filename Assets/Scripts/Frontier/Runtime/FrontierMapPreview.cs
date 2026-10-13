using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // A single menu-map texture replaces thousands of individual IMGUI
        // Box calls per repaint on the now 120x80 outdoor maps. Markers and
        // current position remain live overlays, not baked into the raster.
        private Texture2D terrainPreview;
        private GameCore previewOwner;
        private MapData previewMap;
        private int previewRegion=-1,previewDiscoveryStamp;
        private bool previewFog,previewLowTide;
        // Hash only the fog state belonging to this map, not Game.revision.
        // Combat, movement and HUD updates must not rebuild a 120x80 texture.
        private int PreviewDiscoveryStamp(int region,bool fog)
        {
            int code=Game.FarBridgeOpen(region)?41:17;
            if(!fog)return code;
            string prefix=region==8?
                "abyss_annex_seen_"+Game.save.abyssSeed+"_"+Game.save.abyssFloor+"_":
                "annex_seen_"+region+"_";
            unchecked
            {
                code=code*31+Game.save.explored[region];
                // Commutative: changes to flag-list ordering are not changes
                // to the actual explored terrain.
                int seen=0;
                foreach(string flag in Game.save.flags)
                    if(flag!=null&&flag.StartsWith(prefix,StringComparison.Ordinal))
                        seen^=flag.GetHashCode();
                return code*31+seen;
            }
        }
        private void ReleaseTerrainPreview()
        {
            if(terrainPreview!=null)UnityEngine.Object.Destroy(terrainPreview);
            terrainPreview=null;previewOwner=null;previewMap=null;
            previewRegion=-1;previewDiscoveryStamp=0;
        }
        private void DrawTerrainPreview(MapData map,int region,bool fog,float x,float y,float scale)
        {
            if(map==null||map.rows==null||map.rows.Length==0)return;
            int width=map.rows[0].Length,height=map.rows.Length;
            if(width<=0||height<=0||scale<=0)return;
            bool tide=Game.LowTide;
            int discoveryStamp=PreviewDiscoveryStamp(region,fog);
            if(terrainPreview==null||!object.ReferenceEquals(previewMap,map)||
                !object.ReferenceEquals(previewOwner,Game)||
                previewRegion!=region||previewFog!=fog||
                previewDiscoveryStamp!=discoveryStamp||
                previewLowTide!=tide)
            {
                ReleaseTerrainPreview();
                terrainPreview=new Texture2D(width,height,TextureFormat.RGBA32,false);
                terrainPreview.filterMode=FilterMode.Point;
                terrainPreview.wrapMode=TextureWrapMode.Clamp;
                terrainPreview.name="Starfall explored terrain preview";
                Color ground=PixelArt.C(map.color);
                Color water=PixelArt.C("#315c76"),stone=PixelArt.C("#566879");
                Color road=PixelArt.C("#a09877");
                // Fog is stored in 6x6-tile discovery sectors. Resolve
                // each sector once per rebuild rather than creating thousands
                // of annex flag strings and repeated list searches per pixel.
                int sectorsWide=(width+5)/6;
                bool[] sectorSeen=null;
                if(fog)
                {
                    int sectorsHigh=(height+5)/6;
                    sectorSeen=new bool[sectorsWide*sectorsHigh];
                    for(int sy=0;sy<sectorsHigh;sy++)
                        for(int sx=0;sx<sectorsWide;sx++)
                            sectorSeen[sy*sectorsWide+sx]=Game.Explored(region,sx*6,sy*6);
                }
                Color[] pixels=new Color[width*height];
                for(int yy=0;yy<height;yy++)
                {
                    string row=map.rows[yy];
                    for(int xx=0;xx<width;xx++)
                    {
                        char tile=row[xx];
                        if(tile=='?')tile=Game.FarBridgeOpen(region)?'=':'#';
                        Color color=tile=='~'||tile=='%'&&!tide?water:
                            tile=='#'?stone:tile=='='?road:ground;
                        if(fog&&!sectorSeen[(yy/6)*sectorsWide+xx/6])
                            color=Color.Lerp(color,ink,.68f);
                        // Unity raster origin is bottom-left; map north is GUI top.
                        pixels[(height-1-yy)*width+xx]=color;
                    }
                }
                terrainPreview.SetPixels(pixels);
                terrainPreview.Apply(false,false);
                previewOwner=Game;previewMap=map;previewRegion=region;
                previewFog=fog;previewDiscoveryStamp=discoveryStamp;
                previewLowTide=tide;
            }
            Image(terrainPreview,x,y,width*scale,height*scale);
        }
    }
}
