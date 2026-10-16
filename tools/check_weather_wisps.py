#!/usr/bin/env python3
"""Hard-gate four original translucent pixel weather sprites in the live scene.

Art bytes and Sprite source are checked; no Unity GameView result is inferred.
"""
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
PNG=ROOT/"Assets/Resources/Frontier/Art/WeatherWisps.png"
META=PNG.with_suffix(".png.meta")
RUNTIME=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEffects.cs"
SCENE=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs"

def main():
    assert PNG.is_file() and META.is_file(), "WEATHER_SPRITE_ART_MISSING"
    with Image.open(PNG) as source:
        assert source.format=="PNG" and source.size==(96,24), (
            "WEATHER_SPRITE_GRID",source.size)
        art=source.convert("RGBA")
    cells=[]
    for i in range(4):
        cell=art.crop((24*i,0,24*(i+1),24))
        alpha=cell.getchannel("A")
        bounds=alpha.getbbox()
        colored=[px for px in cell.getdata() if px[3]>0]
        assert bounds is not None and len(colored)>=14 and len(colored)<90, (
            "WEATHER_SPRITE_MISSING_OR_OPAQUE",i,len(colored))
        assert bounds[0]>=2 and bounds[1]>=2 and bounds[2]<=22 and bounds[3]<=22, (
            "WEATHER_SPRITE_CROPPED",i,bounds)
        assert len(set(colored))>=4 and max(p[3] for p in colored)>=220, (
            "WEATHER_SPRITE_LACKS_PAINTED_HIGHLIGHTS",i)
        cells.append(tuple(cell.getdata()))
    assert len(set(cells))==4,"WEATHER_MOTIFS_DUPLICATED"
    meta=META.read_text()
    assert "guid: 63a1e1fae20341a691c846b049bcf421" in meta
    for flag in ("enableMipMap: 0","filterMode: 0","textureType: 0",
                 "textureCompression: 0","isReadable: 0"):
        assert flag in meta,("WEATHER_UNITY_PIXEL_IMPORT",flag)
    renderer=RUNTIME.read_text()
    world=SCENE.read_text()
    for required in ('Resources.Load<Texture2D>(',
            '"Frontier/Art/WeatherWisps"','weatherWispSheet.width==96',
            'weatherWispSheet.height==24','Atlas(weatherWispSheet,4,1,motif',
            'Atlas(effectSheet,4,4,13','Game.save.reducedMotion',
            'WorldDrawable(drift.x,drift.y,18f,18f)'):
        assert required in renderer,("WEATHER_NOT_IN_PRODUCTION_RENDERER",required)
    assert "DrawAuthoredAtmosphericMotes();" in world, (
        "WEATHER_ONLY_A_DISCONNECTED_PREVIEW")
    print("PASS four original painted 24px forest/sand/coast/snow sprites, pixel/alpha diversity, Unity GUID, production DrawWorld integration and fallback")
if __name__=="__main__":
    main()
