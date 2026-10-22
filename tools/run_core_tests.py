#!/usr/bin/env python3
"""Run the exact Unity core checks with Mono, using a fixture generated from the shipped campaign."""
from pathlib import Path
import json, os, shutil, subprocess, tempfile

ROOT = Path(__file__).resolve().parents[1]

def literal(value):
    return json.dumps(value, ensure_ascii=False)

def fixture(data):
    def obj(value, cls):
        fields = []
        for key, value in value.items():
            if isinstance(value, str): fields.append(f'{key} = {literal(value)}')
            elif isinstance(value, (int, float)): fields.append(f'{key} = {value}' + ('f' if isinstance(value, float) else ''))
        return 'new ' + cls + ' { ' + ', '.join(fields) + ' }'
    maps = []
    for m in data['maps']:
        basic = obj(m, 'MapData')[:-2]
        rows = ', '.join(literal(row) for row in m['rows'])
        objects = ', '.join(obj(o, 'WorldObject') for o in m['objects'])
        decor = ', '.join(obj(o, 'Decoration') for o in m['decor'])
        maps.append(basic + f', rows = new string[] {{ {rows} }}, objects = new WorldObject[] {{ {objects} }}, decor = new Decoration[] {{ {decor} }} }}')
    arrays = []
    for key, cls in [('items','ItemData'), ('bosses','BossData'), ('lore','LoreData')]:
        arrays.append(key + ' = new ' + cls + '[] { ' + ', '.join(obj(o,cls) for o in data[key]) + ' }')
    return 'using Starfall; public static class CampaignFixture { public static Campaign Create() { return new Campaign { maps = new MapData[] { ' + ', '.join(maps) + ' }, ' + ', '.join(arrays) + ' }; } }'

def main():
    mono, mcs = os.environ.get('STARFALL_MONO', shutil.which('mono')), os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs: raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-tests-') as folder:
        temp = Path(folder)
        (temp/'CampaignFixture.cs').write_text(fixture(json.loads((ROOT/'Assets/Resources/Frontier/Campaign.json').read_text())))
        (temp/'Harness.cs').write_text('''using System; using Starfall.Tests;
class Harness { static int Main() { var data=CampaignFixture.Create(); int failed=0;
foreach(var method in typeof(CoreChecks).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
try { method.Invoke(null,new object[]{data}); Console.WriteLine("PASS "+method.Name); }
catch(Exception e) { failed++; Console.WriteLine("FAIL "+method.Name+": "+(e.InnerException??e)); }
} return failed==0?0:1; } }''')
        sources = sorted((ROOT/'Assets/Scripts/Frontier/Core').glob('*.cs')) + sorted((ROOT/'Assets/Tests/Frontier').glob('*Checks.cs')) + [temp/'CampaignFixture.cs',temp/'Harness.cs']
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp/'checks.exe'
        subprocess.run(compiler + ['-out:'+str(exe)] + [str(p) for p in sources],check=True)
        subprocess.run([mono,str(exe)],check=True)

if __name__ == '__main__': main()
