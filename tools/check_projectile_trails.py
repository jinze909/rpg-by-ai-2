#!/usr/bin/env python3
"""Hard-gate eight real projectile filament PNG cells and exact production use."""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/ProjectileTrails.png"
META=Path(str(ART)+".meta")

def inspect(source):
    assert source.size==(96,48) and source.mode=="RGBA", (
        "PROJECTILE_TRAILS_NOT_EIGHT_24PX_RGBA_POSES")
    unique=[]
    for row in range(2):
        frames=[]
        for f in range(4):
            cell=list(source.crop((24*f,24*row,
                24*(f+1),24*(row+1))).getdata())
            real=[pixel for pixel in cell if pixel[3]]
            assert 20<=len(real)<=85,(
                "MISSING_OR_OPAQUE_TRAIL",row,f,len(real))
            assert len(set(real))>=5,("UNPAINTED_FLAT_TRAIL",row,f)
            assert max(p[3] for p in real)>=230,(
                "NO_AUTHORED_BRIGHT_CONTACT",row,f)
            frames.append(tuple(cell))
        assert len(set(frames))==4,("DUPLICATE_ANIMATION_FRAMES",row)
        unique+=frames
    assert len(set(unique))==8,"ARROW_AND_MAGIC_SOURCE_IDENTITY_COLLIDED"

def main():
    with Image.open(ART) as img:
        assert img.format=="PNG"
        art=img.convert("RGBA")
    inspect(art)
    painter=runpy.run_path(
        str(ROOT/"tools/generate_projectile_trails.py"))["paint"]
    assert painter().tobytes()==art.tobytes(), (
        "PROJECTILE_ART_NOT_REPRODUCIBLE_SOURCE_PIXELS")
    # Fault-injection: replacing the four magic poses with one picture
    # must make this gate fail, not silently pass on atlas dimensions.
    mutant=Image.new("RGBA",(96,48))
    mutant.paste(art.crop((0,0,96,24)),(0,0))
    for frame in range(4):
        mutant.paste(art.crop((0,24,24,48)),(frame*24,24))
    try:inspect(mutant)
    except AssertionError:pass
    else:raise AssertionError("DUPLICATE_MAGIC_ART_MUTATION_NOT_CAUGHT")
    meta=META.read_text()
    for part in ("guid: 4b90de715f1343b9957d93debb8162e5",
        "enableMipMap: 0","filterMode: 0","isReadable: 0",
        "textureCompression: 0","alphaIsTransparency: 1"):
        assert part in meta,("PROJECTILE_NATIVE_IMPORTER",part)
    render=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEffects.cs").read_text()
    lane=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierProjectileTrailArt.cs").read_text()
    tests=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert 'Atlas(authoredTail,4,2,(arrow?0:4)+paintedFrame,' in render
    assert '"Frontier/Art/ProjectileTrails"' in lane
    assert 'imported.width==96&&' in lane and 'imported.height==48' in lane
    assert 'RenderStateChecks.VerifyAuthoredProjectileTrailsAndHotImport,' in tests
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    art.resize((96*6,48*6),Image.Resampling.NEAREST).save(
        folder/"projectile-arrow-and-magic-eight-source-poses.png")
    print("PASS 8 different original pixel arrow/plasma animation poses, live projectile use, Unity GUID, reproducible exact PNG and mutation guard; NATIVE_UNVERIFIED")

if __name__=="__main__":
    main()
