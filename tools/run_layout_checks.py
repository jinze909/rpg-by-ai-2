#!/usr/bin/env python3
"""Compile the production runtime with offline stubs and record its GUI. Not a Unity build."""
from pathlib import Path
import json, os, shutil, subprocess, sys
from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]

def main():
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    folder = ROOT / '.starfall-checks'
    folder.mkdir(exist_ok=True)
    from PIL import Image
    pixels=folder/'resourcepixels';pixels.mkdir(exist_ok=True)
    actors=json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())['atlases']
    for spec in actors:
        name=spec['name'];path=ROOT/('Assets/Resources/Frontier/Art/'+name+'.png')
        (pixels/(name+'.rgba')).write_bytes(Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes())
    campaign = folder / 'CampaignFixture.cs'
    campaign.write_text(fixture(json.loads((ROOT/'Assets/Resources/Frontier/Campaign.json').read_text())))
    sources = sorted((ROOT/'Assets/Scripts/Frontier').rglob('*.cs')) + [ROOT/'tests/UnityLayoutStubs.cs', ROOT/'tests/OfflineJson.cs', ROOT/'tests/RuntimePolishChecks.cs', ROOT/'tests/VisualRuntimeChecks.cs', ROOT/'tests/RenderStateChecks.cs', ROOT/'tests/RuntimeContinuityChecks.cs', ROOT/'tests/ActorRegistrationChecks.cs', campaign]
    compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
    focused = '--continuity-only' in sys.argv
    resident = '--resident-only' in sys.argv
    exe = folder/('resident-layout.exe' if resident else
                  'continuity-layout.exe' if focused else 'layout.exe')
    options = ['-define:RESIDENT_ONLY'] if resident else (
        ['-define:CONTINUITY_ONLY'] if focused else [])
    subprocess.run(compiler + options + ['-out:'+str(exe)] + [str(p) for p in sources], check=True)
    layout = folder/'layout'
    layout.mkdir(exist_ok=True)
    if not focused:
        for path in layout.glob('*.json'): path.unlink()
    subprocess.run([mono, str(exe), str(layout)], check=True, cwd=ROOT)
    if resident:
        print('PASS production NPC draw path and prewarmed raider masks (offline)')
        print('NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: no Unity Editor or GameView')
        return
    from check_layout_metrics import check
    report = check()
    if report['text_overflows']: raise SystemExit('Potential clipped text; inspect .starfall-checks/layout-report.json')
    names = sorted(p.stem for p in layout.glob('*.json') if not p.stem.startswith('qa-') and (not focused or 'continuity' in p.stem))
    subprocess.run(['python3', str(ROOT/'tools/render_layout.py')] + names, check=True)

if __name__ == '__main__':
    main()
