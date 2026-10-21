#!/usr/bin/env python3
"""Hard-gate twelve distinct, authentic 48x24 in-world combat-release dust poses.

These are visual-only floor contact adornments, not substitute actor art or
damage telegraphs. CPU pixel and Mono tests cannot certify Unity GameView.
"""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
SPRITE=ROOT/"Assets/Resources/Frontier/Art/EnemyReleaseDust.png"
META=Path(str(SPRITE)+".meta")
SOURCE=ROOT/"tools/generate_enemy_release_dust.py"
ACTORS=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs"
NATIVE=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnemyReleaseDust.cs"
RUNNER=ROOT/"tests/RuntimeCombatInputChecks.cs"
WORKFLOW=ROOT/".github/workflows/core-tests.yml"

def audit(image):
    assert image.mode=="RGBA" and image.size==(192,72),(
        "RELEASE_DUST_SOURCE_DIMENSIONS",image.size)
    frames=[]
    for row in range(3):
        row_signatures=[]
        for frame in range(4):
            cell=image.crop((frame*48,row*24,(frame+1)*48,(row+1)*24))
            nonblank=[px for px in cell.getdata() if px[3]>0]
            box=cell.getchannel("A").getbbox()
            assert 58<=len(nonblank)<=112 and box is not None,(
                "RELEASE_DUST_NOT_PAINTED",row,frame,len(nonblank))
            assert box[0]>=2 and box[1]>=2 and box[2]<=46 and box[3]<=23,(
                "RELEASE_DUST_CROPPED_OR_UNGROUNDED",row,frame,box)
            assert len(set(nonblank))>=7,("NO_MATERIAL_SHADING",row,frame)
            row_signatures.append(cell.tobytes())
        assert len(set(row_signatures))==4,("RELEASE_FRAMES_DUPLICATED",row)
        frames.extend(row_signatures)
    assert len(set(frames))==12,"RELEASE_MATERIALS_REUSED_BETWEEN_BIOMES"

def main():
    assert SPRITE.is_file() and META.is_file() and SOURCE.is_file()
    with Image.open(SPRITE) as im:
        assert im.format=="PNG","RELEASE_DUST_NOT_LOSSLESS_PNG"
        source=im.convert("RGBA")
    audit(source)
    painter=runpy.run_path(str(SOURCE))["build"]
    assert painter().tobytes()==source.tobytes(),(
        "SHIPPED_ENEMY_DUST_DIFFERS_FROM_ORIGINAL_SOURCE")
    # The detector must catch copy-paste fake pose art.
    mutated=source.copy()
    mutated.paste(source.crop((0,0,48,24)),(48,0))
    try:audit(mutated)
    except AssertionError as error:
        assert "RELEASE_FRAMES_DUPLICATED" in str(error),error
    else:raise AssertionError("RELEASE_DUST_MUTATION_DETECTOR_INACTIVE")
    meta=META.read_text()
    for text in ("guid: 1e0953f54e4d48b6b4b0a89288b04c3f",
            "enableMipMap: 0","filterMode: 0","alphaIsTransparency: 1",
            "textureCompression: 0"):
        assert text in meta,("RELEASE_PIXEL_IMPORTER_CHANGED",text)
    code=NATIVE.read_text()
    for required in ('"Frontier/Art/EnemyReleaseDust"',
            "incoming.width==192&&","incoming.height==72",
            "enemy.releaseTime<=.001f","enemy.state==1",
            "Game.save.reducedMotion","Game.save.effectsQuality<2",
            "enemy.boss?.38f:.24f",
            "Atlas(enemyReleaseDustSheet,4,3,first+seam-1,",
            "Atlas(enemyReleaseDustSheet,4,3,first+seam,",
            "Atlas(enemyReleaseDustSheet,4,3,first+frame,",
            "enemyReleaseDustAlpha","enemyReleaseDustSeam"):
        assert required in code,("RELEASE_NOT_IN_PRODUCTION_RENDERER",required)
    actor=ACTORS.read_text()
    assert "DrawAuthoredEnemyReleaseDust(e,p,size," in actor
    assert "e.VisualFlip&&!uprightArcher,coverAlpha" in actor
    assert "Mathf.Clamp01(bodyAlpha)" in code
    assert "opacity*(1f-blend)),mirrored)" in code
    assert "opacity*blend),mirrored)" in code
    assert "opacity),mirrored)" in code
    assert actor.index("DrawAuthoredEnemyReleaseDust(e,p,size,")<actor.index(
        "DrawActorContactShadow(p,size*.57f"),(
        "RELEASE_CONTACT_OVERPAINTS_ORIGINAL_FOOT_OR_WEAPON")
    assert "ActorRegistrationChecks.VerifyOriginalEnemyReleaseDust," in RUNNER.read_text()
    assert "python3 tools/check_enemy_release_dust.py" in WORKFLOW.read_text()
    art=ROOT/".starfall-checks/sprite-qa"
    art.mkdir(parents=True,exist_ok=True)
    source.resize((192*6,72*6),Image.Resampling.NEAREST).save(
        art/"review-enemy-release-ground-contact-12-original.png")
    print("PASS 12 independent moss/cinder/arcane release ground poses, Unity source GUID, real combat release path, mutation QA and executed C#")
if __name__=="__main__":main()
