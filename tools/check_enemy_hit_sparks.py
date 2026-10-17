#!/usr/bin/env python3
"""Adversarial source/pixel and live-runtime gate for 12 original hit reaction cells."""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/EnemyHitSparks.png"
META=Path(str(ART)+".meta")
SOURCE=ROOT/"tools/generate_enemy_hit_sparks.py"
RENDER=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnemyHitSparks.cs"
ACTORS=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs"

def audit(sprite):
    assert sprite.mode=="RGBA" and sprite.size==(96,72),(
        "HIT_SPARK_DIMENSIONS_NOT_SOURCE",sprite.size)
    poses=[]
    for material in range(3):
        local=[]
        for frame in range(4):
            pose=sprite.crop((frame*24,material*24,
                              (frame+1)*24,(material+1)*24))
            box=pose.getchannel("A").getbbox()
            count=sum(px[3]>0 for px in pose.getdata())
            assert box is not None and 19<=count<=44,(
                "HIT_SPARKS_MISSING_SOURCE_POSE",material,frame,count)
            assert box[0]>=2 and box[2]<=22 and box[3]<=23,(
                "HIT_SPARKS_SOURCE_CROPS_PAST_PIXELS",material,frame,box)
            assert len(set(px for px in pose.getdata() if px[3]))>=3,(
                "HIT_SPARKS_MISSING_MATERIAL_SHADING",material,frame)
            local.append(pose.tobytes())
        assert len(set(local))==4,("HIT_SPARKS_CLONED_ANIMATION",material)
        poses.extend(local)
    assert len(set(poses))==12,"HIT_SPARKS_BIOME_PALETTE_REUSED"

def main():
    assert ART.is_file() and META.is_file() and SOURCE.is_file()
    with Image.open(ART) as image:
        assert image.format=="PNG", "HIT_SPARK_NOT_PNG"
        actual=image.convert("RGBA")
    audit(actual)
    expected=runpy.run_path(str(SOURCE))["build"]()
    assert expected.tobytes()==actual.tobytes(),(
        "HIT_SPARKS_SHIPPED_PIXELS_DIFFER_FROM_SOURCE_PAINTER")
    falsified=actual.copy()
    falsified.paste(actual.crop((0,0,24,24)),(24,0))
    try:audit(falsified)
    except AssertionError as error:
        assert "HIT_SPARKS_CLONED_ANIMATION" in str(error),error
    else:raise AssertionError("HIT_SPARK_DUPLICATE_MUTATION_UNDETECTED")
    meta=META.read_text()
    for key in ("guid: 8f5cf7c96ee542cc8f9b81f50d214691",
                "enableMipMap: 0","filterMode: 0",
                "alphaIsTransparency: 1","textureCompression: 0"):
        assert key in meta,("HIT_SPARK_UNITY_PIXEL_IMPORT_CHANGED",key)
    code=RENDER.read_text()
    for token in ('"Frontier/Art/EnemyHitSparks"',
                  "enemy.hitTime<=.001f","enemy.releaseTime>.001f",
                  "Game.save.reducedMotion","Game.save.effectsQuality<2",
                  "enemyHitSparkAlpha","enemyHitSparkSeam",
                  "incoming.width==96&&","incoming.height==72",
                  "Atlas(enemyHitSparksSheet,4,3,first+seam-1,area,",
                  "Atlas(enemyHitSparksSheet,4,3,first+seam,area,",
                  "Atlas(enemyHitSparksSheet,4,3,first+frame,area,"):
        assert token in code,("HIT_SPARKS_NOT_IN_LIVE_SOURCE",token)
    actor=ACTORS.read_text()
    assert "DrawAuthoredEnemyHitSparks(e,p,e.boss?" in actor
    assert "if (NativeActorEligible(sheet, columns, rows))" in actor
    assert "Atlas(sheet, columns, rows, frame," in actor
    test=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "ActorRegistrationChecks.VerifyAuthoredEnemyHitSparks," in test
    flow=(ROOT/".github/workflows/core-tests.yml").read_text()
    assert "python3 tools/check_enemy_hit_sparks.py" in flow
    output=ROOT/".starfall-checks/sprite-qa"
    output.mkdir(parents=True,exist_ok=True)
    actual.resize((576,432),Image.Resampling.NEAREST).save(
        output/"review-real-hit-reaction-12-pixel-cells.png")
    print("PASS 12 original hit reactions: unique moss/ember/arcane pixels, safe import, live actor source binding, verified mutation defense")
if __name__=="__main__":main()
