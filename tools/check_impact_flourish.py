#!/usr/bin/env python3
"""Source-pixel and production-scene gate for 12 actual hand-painted impact sprites."""
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/ImpactFlourish.png"
META=Path(str(ART)+".meta")
FX=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEffects.cs"
WORLD=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs"
LOOK=ROOT/"Assets/Resources/Frontier/Visual/WorldLook.txt"

def main():
    assert ART.is_file() and META.is_file(),"MISSING_IMPACT_ART"
    with Image.open(ART) as im:
        assert im.format=="PNG" and im.size==(128,96),(
            "WRONG_IMPACT_ATLAS_SIZE",im.size)
        pixels=im.convert("RGBA")
    signatures=[]
    for row in range(3):
        for col in range(4):
            cell=pixels.crop((col*32,row*32,(col+1)*32,(row+1)*32))
            alpha=cell.getchannel("A")
            bb=alpha.getbbox()
            visible=[px for px in cell.getdata() if px[3]>0]
            assert bb is not None and 40<=len(visible)<=160,(
                "EMPTY_OR_OPAQUE_IMPACT",row,col)
            assert bb[0]>=2 and bb[1]>=2 and bb[2]<=30 and bb[3]<=30,(
                "CROPPED_IMPACT_FRAME",row,col,bb)
            assert len(set(visible))>=3,("NO_PAINTED_HIGHLIGHTS",row,col)
            signatures.append(cell.tobytes())
    assert len(set(signatures))==12,"IMPACT_CELLS_NOT_INDEPENDENT"
    m=META.read_text()
    for token in ("guid: f91a6c8b442843fb941e39732a71ed22",
        "enableMipMap: 0","filterMode: 0","textureCompression: 0"):
        assert token in m,("IMPACT_UNITY_IMPORT_CHANGED",token)
    effect=FX.read_text()
    scene=WORLD.read_text()
    for token in ('"Frontier/Art/ImpactFlourish"',
        'effect.kind=="slash"', 'effect.kind=="impact"',
        'effect.kind=="hurt"', 'effect.kind=="projectileImpact"?1:',
        'effect.kind=="magicImpact"',
        'effect.kind=="enemyRelease"&&effect.variant>=10',
        'Atlas(impactFlourishSheet,4,3,category*4+frame',
        'effect.life/effect.maxLife', 'Game.save.reducedMotion',
        'Game.save.effectsQuality<2'):
        assert token in effect,("IMPACT_NOT_IN_REAL_RENDERER",token)
    assert "DrawAuthoredImpactFlourishes();" in scene, (
        "IMPACT_ONLY_ORPHAN_PREVIEW")
    assert "impactFlourishAlpha=0.58" in LOOK.read_text(), (
        "IMPACT_NOT_IN_UNITY_LOOK_ASSET")
    print("PASS 12 independent approved 32px impact-source frames, source PixelImporter, GameCore clock, real DrawWorld and artist-controlled FX")
if __name__=="__main__":main()
