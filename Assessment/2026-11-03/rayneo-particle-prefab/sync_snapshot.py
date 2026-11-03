"""Copy assessment inputs into the idle, isolated Unity project; never copy caches."""
from pathlib import Path
import hashlib
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
PROJECT = Path.home() / 'AppData/Local/Temp/rnc1030/project'
SCOPES = ('Assets', 'Packages', 'ProjectSettings')
EXTRAS = {
    'Assets/Editor.meta', 'Assets/Editor/MonoScriptProbe.cs',
    'Assets/Editor/MonoScriptProbe.cs.meta', 'Assets/Editor/PreMergeValidation.cs',
    'Assets/Editor/PreMergeValidation.cs.meta',
    'Assets/Editor/RayNeoParticlePrefabMigration.cs',
    'Assets/Editor/RayNeoParticlePrefabMigration.cs.meta',
}


def scan(root):
    result = {}
    for scope in SCOPES:
        for path in sorted((root / scope).rglob('*')):
            if path.is_file():
                data = path.read_bytes()
                result[path.relative_to(root).as_posix()] = {
                    'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
    return result


if __name__ == '__main__':
    phase = sys.argv[1]
    assert not (PROJECT / 'Temp/UnityLockfile').exists(), 'Snapshot Editor is running'
    before = json.loads((OUT / 'baseline.json').read_text())['files']
    source = scan(ROOT)
    snapshot = scan(PROJECT)
    removed = []
    for name in sorted(snapshot.keys() - source.keys() - EXTRAS):
        assert name in before, f'Unexpected snapshot input: {name}'
        target = (PROJECT / name).resolve()
        assert PROJECT.resolve() in target.parents
        target.unlink()
        removed.append(name)
    copied = []
    for name, signature in source.items():
        if snapshot.get(name) != signature:
            target = PROJECT / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / name, target)
            copied.append(name)
    after = scan(PROJECT)
    assert all(after.get(name) == signature for name, signature in source.items())
    assert set(after) - set(source) == EXTRAS
    record = {'phase': phase, 'project': str(PROJECT), 'source_files': len(source),
              'all_source_bytes_match': True, 'copied': copied, 'removed': removed,
              'validation_only_extras': sorted(EXTRAS)}
    (OUT / f'snapshot-{phase}.json').write_text(json.dumps(record, indent=2) + '\n')
    (OUT / 'validation-snapshot.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
