#!/usr/bin/env python3
"""CI: verify 20 installed ground-contact paintings and actual world render wiring."""
from pathlib import Path
import runpy
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/Art/GroundContactFrames.png"
META = Path(str(ART) + ".meta")
PRODUCER = ROOT / "tools/generate_ground_contact_frames.py"
RUNTIME = ROOT / "Assets/Scripts/Frontier/Runtime/FrontierFootfallVisual.cs"
WORLD = ROOT / "Assets/Scripts/Frontier/Runtime/FrontierWorldOrder.cs"
AUDIO = ROOT / "Assets/Scripts/Frontier/Runtime/FrontierAudio.cs"

def audit(atlas):
    assert atlas.size == (128, 100) and atlas.mode == "RGBA", (
        "GROUND_CONTACT_BAD_GEOMETRY", atlas.size, atlas.mode)
    seen = set()
    for material in range(5):
        poses = []
        for frame in range(4):
            frame_pixels = atlas.crop((frame * 32, material * 20,
                                       (frame + 1) * 32, (material + 1) * 20))
            alpha = frame_pixels.getchannel("A")
            bbox = alpha.getbbox()
            pixels = list(frame_pixels.getdata())
            count = sum(p[3] != 0 for p in pixels)
            assert bbox is not None and 30 <= count <= 55, (
                "GROUND_CONTACT_MISSING_FRAME", material, frame, count)
            assert bbox[0] >= 0 and bbox[2] <= 32 and bbox[3] <= 16, (
                "GROUND_CONTACT_UNROOTED_OR_CROPPED", material, frame, bbox)
            assert len(set(p[:3] for p in pixels if p[3])) >= 3, (
                "GROUND_CONTACT_MISSING_MATERIAL_PALETTE", material, frame)
            opaque = frame_pixels.tobytes()
            poses.append(opaque)
            seen.add(opaque)
        assert len(set(poses)) == 4, (
            "GROUND_CONTACT_CLONED_STEP_FRAMES", material)
    assert len(seen) == 20, "GROUND_CONTACT_MATERIAL_REUSE"

def main():
    assert ART.is_file() and META.is_file() and PRODUCER.is_file()
    with Image.open(ART) as inp:
        assert inp.format == "PNG", "GROUND_CONTACT_NOT_PNG"
        atlas = inp.convert("RGBA")
    audit(atlas)
    source = runpy.run_path(str(PRODUCER))["build"]()
    assert source.tobytes() == atlas.tobytes(), (
        "GROUND_CONTACT_SHIPPED_PIXELS_NOT_SOURCE_AUTHORED")
    damaged = atlas.copy()
    damaged.paste(atlas.crop((0, 0, 32, 20)), (32, 0))
    try: audit(damaged)
    except AssertionError as exc:
        assert "GROUND_CONTACT_CLONED_STEP_FRAMES" in str(exc), exc
    else: raise AssertionError("GROUND_CONTACT_MUTATION_FALSE_PASS")
    meta = META.read_text(encoding="utf-8")
    for required in ("guid: 6c3e1a8b24f94dad84d1a9d13067bdb2",
                     "enableMipMap: 0", "filterMode: 0",
                     "textureCompression: 0", "alphaIsTransparency: 1"):
        assert required in meta, ("GROUND_CONTACT_UNITY_IMPORT", required)
    runtime = RUNTIME.read_text(encoding="utf-8")
    for required in ('"Frontier/Art/GroundContactFrames"',
                     "incoming.width==128&&", "incoming.height==100",
                     "GroundContactMaterial(surface)", "mark.material*4+frame",
                     "mark.alternate", "Game.save.effectsQuality==1?0",
                     "Game.save.reducedMotion", "footfallLifetime",
                     "groundContactSheet!=null", "if(effectSheet==null)return;"):
        assert required in runtime, ("GROUND_CONTACT_RUNTIME_NOT_WIRED", required)
    world = WORLD.read_text(encoding="utf-8")
    assert "case WorldDrawKind.Footfall:DrawFootfallMark(entry.footfall);" in world
    assert "foreach(FootfallMark contact in footfallMarks)" in world
    assert "if(effectSheet!=null)\n                foreach(FootfallMark" not in world
    audio = AUDIO.read_text(encoding="utf-8")
    assert "internal static string FootSurface(GameCore game,Point foot)" in audio
    tests = (ROOT / "tests/RuntimeCombatInputChecks.cs").read_text()
    assert "ActorRegistrationChecks.VerifyMaterialGroundContactFrames," in tests
    workflow = (ROOT / ".github/workflows/core-tests.yml").read_text()
    assert "python3 tools/check_ground_contact_frames.py" in workflow
    out = ROOT / ".starfall-checks/sprite-qa"
    out.mkdir(parents=True, exist_ok=True)
    atlas.resize((768, 600), Image.Resampling.NEAREST).save(
        out / "review-ground-contact-five-materials.png")
    print("PASS 20 original planted material foot contacts: byte-reproducible source, four unique phases, Unity meta, depth-sorted world, fallback and test registration")

if __name__ == "__main__": main()
