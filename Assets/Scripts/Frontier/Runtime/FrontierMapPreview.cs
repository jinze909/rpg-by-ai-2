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
        private int previewRegion=-1,previewRevision=-1;
        private bool previewFog,previewLowTide;
        private void ReleaseTerrainPreview()
        {
            if(terrainPreview!=null)UnityEngine.Object.Destroy(terrainPreview);
            terrainPreview=null;previewOwner=null;previewMap=null;
            previewRegion=previewRevision=-1;
        }
        private void DrawTerrainPreview(MapData map,int region,bool fog,float x,float y,float scale)
        {
            if(map==null||map.rows==null||map.rows.Length==0)return;
            int width=map.rows[0].Length,height=map.rows.Length;
            if(width<=0||height<=0||scale<=0)return;
            bool tide=Game.LowTide;
            if(terrainPreview==null||!object.ReferenceEquals(previewMap,map)||
                !object.ReferenceEquals(previewOwner,Game)||
                previewRegion!=region||previewFog!=fog||
                previewRevision!=(fog?Game.revision:-1)||
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
                        if(fog&&!Game.Explored(region,xx,yy))
                            color=Color.Lerp(color,ink,.68f);
                        // Unity raster origin is bottom-left; map north is GUI top.
                        pixels[(height-1-yy)*width+xx]=color;
                    }
                }
                terrainPreview.SetPixels(pixels);
                terrainPreview.Apply(false,false);
                previewOwner=Game;previewMap=map;previewRegion=region;
                previewFog=fog;previewRevision=fog?Game.revision:-1;
                previewLowTide=tide;
            }
            Image(terrainPreview,x,y,width*scale,height*scale);
        }
    }
}
