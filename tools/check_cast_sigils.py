#!/usr/bin/env python3
"""Hard gate all eight source-authored Boss and elite attack target glyphs."""
from pathlib import Path
from PIL import Image
import runpy
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/CastSigils.png"
META=Path(str(ART)+".meta")
def inspect(art):
    assert art.mode=="RGBA" and art.size==(128,64),(
        "SIGIL_ATLAS_NOT_EIGHT_32PX_CELLS")
    faces=[]
    for row in range(2):
        poses=[]
        for frame in range(4):
            pixels=list(art.crop((32*frame,32*row,
                32*(frame+1),32*(row+1))).getdata())
            occupied=[p for p in pixels if p[3]>0]
            assert 50<=len(occupied)<=115,(
                "SIGIL_POSE_MISSING_OR_SOLID",row,frame,len(occupied))
            assert len(set(occupied))>=3,(
                "SIGIL_REPLACED_BY_SINGLE_FLAT_COLOR",row,frame)
            assert any(p[3]>=240 for p in occupied),(
                "MISSING_ORIGINAL_BRIGHT_CENTER",row,frame)
            assert all(p[3] in (0,230,240,255) for p in pixels),(
                "SIGIL_SOURCE_ALPHA_PALETTE_CHANGED",row,frame)
            poses.append(tuple(pixels))
        assert len(set(poses))==4,("DUPLICATE_TELEGRAPH_POSES",row)
        faces+=poses
    assert len(set(faces))==8,"BOSS_AND_ELITE_SOURCE_ARE_NOT_DISTINCT"
def main():
    with Image.open(ART) as imported:
        assert imported.format=="PNG"
        art=imported.convert("RGBA")
    inspect(art)
    rebuilt=runpy.run_path(str(ROOT/"tools/generate_cast_sigils.py"))["build"]()
    assert art.tobytes()==rebuilt.tobytes(),(
        "REPRODUCIBLE_CAST_GLYPH_PAINTER_DIFFERS_FROM_SHIPPED_PNG")
    sabotaged=Image.new("RGBA",(128,64))
    for row in range(2):
        source=art.crop((0,row*32,32,(row+1)*32))
        for f in range(4):sabotaged.paste(source,(f*32,row*32))
    try:inspect(sabotaged)
    except AssertionError:pass
    else:raise AssertionError("DUPLICATE_SIGIL_FRAME_MUTATION_WAS_NOT_CAUGHT")
    m=META.read_text()
    for value in ("guid: bcdedb52168740d5bf4a0ef596410835",
        "enableMipMap: 0","filterMode: 0","textureCompression: 0",
        "alphaIsTransparency: 1"):
        assert value in m,("SIGIL_UNITY_IMPORTER",value)
    renderer=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEffects.cs").read_text()
    runtime=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierCastSigilArt.cs").read_text()
    for v in ("DrawCastSigil(enemy,progress);",
        "DrawCoverAwareEnemyAreaArc(enemy,radius,",
        "DrawCoverAwareEnemyAreaArc(enemy,innerRadius,",
        "Game.EnemyAreaWarningSpokeReachable(enemy,sample)"):
        assert v in renderer,("SOURCE_GLYPH_NOT_REAL_WARNING_PATH",v)
    gamecore=(ROOT/"Assets/Scripts/Frontier/Core/CombatPresentation.cs").read_text()
    assert "public bool EnemyAreaWarningSpokeReachable(Enemy enemy,Point point)" in gamecore
    assert "ClearAttackPath(enemy.Position,point)" in gamecore,(
        "BOSS_WARNING_NOT_LINKED_TO_PHYSICAL_DAMAGE_COVER")
    assert "RenderStateChecks.VerifyEnemyAreaWarningHonorsRealWallCover," in (
        ROOT/"tests/RuntimeCombatInputChecks.cs").read_text(),(
        "BOSS_WARNING_WALL_VISIBILITY_REGRESSION_NOT_EXECUTED")
    for v in ('"Frontier/Art/CastSigils"', "imported.width==128&&",
        "imported.height==64", "Atlas(sheet,4,2,row*4+frame,canvas,",
        "enemy.target", "Game.save.reducedMotion", "Game.save.effectsQuality==0"):
        assert v in runtime,("SIGIL_NOT_IN_GAME_RENDERER",v)
    run=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "RenderStateChecks.VerifyCastSigilSourceAndCombatWarning," in run,(
        "NEW_REAL_WARNING_QA_NOT_EXECUTED_IN_CI")
    output=ROOT/".starfall-checks/sprite-qa"
    output.mkdir(parents=True,exist_ok=True)
    art.resize((128*6,64*6),Image.Resampling.NEAREST).save(
        output/"cast-sigils-eight-original-attack-warning-frames.png")
    print("PASS 8 distinct Boss/elite authored 32px glyphs, reproducible original pixels, Unity import, actual target renderer and adversarial frame-copy mutation; NATIVE_UNVERIFIED")
if __name__=="__main__":main()
