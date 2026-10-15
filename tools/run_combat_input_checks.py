#!/usr/bin/env python3
"""Exercise production Update/OnGUI combat input with offline Unity declarations."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def main():
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-combat-input-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeCombatInputChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'combat-input.exe'
        subprocess.run(compiler + ['-main:RuntimeCombatInputChecks', '-out:' + str(exe)]
                       + [str(p) for p in sources] + [str(campaign)], check=True)
        subprocess.run([mono, str(exe)], check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
