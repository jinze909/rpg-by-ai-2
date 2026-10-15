#!/usr/bin/env python3
"""Compile Editor and native test source against offline declarations. NOT a Unity build."""
from pathlib import Path
import os,shutil,subprocess,json
from run_core_tests import fixture
ROOT=Path(__file__).resolve().parents[1]
def main():
    mono=os.environ.get('STARFALL_MONO',shutil.which('mono'));mcs=os.environ.get('STARFALL_MCS',shutil.which('mcs'))
    if not mono or not mcs:raise SystemExit('Mono is required for this compile-only check.')
    # This check used to be unused AND non-runnable: it referenced a
    # generated CampaignFixture.cs that no previous CI step produced.
    # Generate the fixture from the exact checked-in campaign first.
    (ROOT/'.starfall-checks').mkdir(parents=True,exist_ok=True)
    (ROOT/'.starfall-checks/CampaignFixture.cs').write_text(
        fixture(json.loads((ROOT/'Assets/Resources/Frontier/Campaign.json').read_text())),
        encoding='utf-8')
    sources=sorted((ROOT/'Assets/Scripts/Frontier').rglob('*.cs'))+sorted((ROOT/'Assets/Editor/Frontier').glob('*.cs'))+sorted((ROOT/'Assets/Tests').rglob('*.cs'))
    sources += [ROOT/'tests/UnityLayoutStubs.cs',ROOT/'tests/UnityEditorSyntaxStubs.cs',ROOT/'tests/OfflineJson.cs',ROOT/'tests/RuntimePolishChecks.cs',ROOT/'tests/VisualRuntimeChecks.cs',ROOT/'tests/RenderStateChecks.cs',ROOT/'tests/RuntimeContinuityChecks.cs',ROOT/'tests/ActorRegistrationChecks.cs',ROOT/'.starfall-checks/CampaignFixture.cs']
    compiler=[mono,mcs] if mcs.endswith('.exe') else [mcs]
    subprocess.run(compiler+['-target:library','-define:UNITY_EDITOR,UNITY_INCLUDE_TESTS','-out:'+str(ROOT/'.starfall-checks/editor-syntax.dll')]+[str(p) for p in sources],check=True)
    print('PASS Editor review lab and PlayMode test C# syntax (offline declarations; native Unity APIs unverified).')
    exe=ROOT/'.starfall-checks/editor-import-checks.exe'
    subprocess.run(compiler+['-define:UNITY_EDITOR,UNITY_INCLUDE_TESTS','-main:EditorImportChecks','-out:'+str(exe)]+[str(p) for p in sources]+[str(ROOT/'tests/EditorImportChecks.cs')],check=True)
    subprocess.run([mono,str(exe)],check=True,cwd=ROOT)
if __name__=='__main__':main()
