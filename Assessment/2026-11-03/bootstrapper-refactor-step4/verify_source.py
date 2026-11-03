"""Check the scope and saved-scene migration against the step 4 baseline."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASELINE = json.loads((OUT / 'baseline.json').read_text())
BEFORE = BASELINE['files']
TARGET = 'Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs'
SCENE = 'Assets/Scenes/ampPortable_xrGlassesDemo.unity'
EXISTING_TEST = 'Assets/Tests/PlayMode/BootstrapperRetainedGraphTests.cs'
NEW_TEST = 'Assets/Tests/PlayMode/BootstrapperLifecycleTests.cs'
SINE = 'Assets/AmpPortableDataViz/Presentation/Sources/SineWaveSource.cs'


def original(name):
    return subprocess.check_output(['git', 'show', BASELINE['commit'] + ':' + name],
                                   cwd=ROOT).decode('utf-8').replace('\r\n', '\n')


def current(name):
    return (ROOT / name).read_text().replace('\r\n', '\n')


def methods(source):
    pattern = (r'^        (?:private|public) (?:static )?[\w<>\[\], ]+ '
               r'(\w+)\([^;]*?\)\s*\n        \{.*?^        \}')
    return {match[1]: match[0] for match in re.finditer(pattern, source, re.M | re.S)}


if __name__ == '__main__':
    changed, removed, added, unchanged = [], [], [], []
    for name, signature in BEFORE.items():
        path = ROOT / name
        if not path.exists():
            removed.append(name)
        elif hashlib.sha256(path.read_bytes()).hexdigest() != signature['sha256']:
            changed.append(name)
        else:
            unchanged.append(name)
    for scope in ('Assets', 'Packages', 'ProjectSettings'):
        for path in (ROOT / scope).rglob('*'):
            if path.is_file() and path.relative_to(ROOT).as_posix() not in BEFORE:
                added.append(path.relative_to(ROOT).as_posix())
    assert set(changed) == {TARGET, SCENE, EXISTING_TEST}
    assert set(removed) == {SINE, SINE + '.meta'}
    assert set(added) == {NEW_TEST, NEW_TEST + '.meta'}
    assert current(SCENE) == original(SCENE).replace('  SineSource: {fileID: 0}\n', '', 1)
    expected_test = original(EXISTING_TEST).replace(
        '            bootstrapper.SineSource = _testRoot.AddComponent<SineWaveSource>();\n', '', 1).replace(
        '            Assert.IsFalse(bootstrapper.SineSource.enabled);\n', '', 1)
    assert current(EXISTING_TEST) == expected_test
    old_methods, new_methods = methods(original(TARGET)), methods(current(TARGET))
    assert len(old_methods) == 57
    assert len(new_methods) == 62
    assert set(old_methods) <= set(new_methods)
    untouched_methods = sorted(name for name in old_methods if old_methods[name] == new_methods[name])
    changed_methods = sorted(set(old_methods) - set(untouched_methods))
    added_methods = sorted(set(new_methods) - set(old_methods))
    assert set(added_methods) == {'StopRoutine', 'StopPendingWork', 'StopSession',
                                  'ClearAutomaticVisuals', 'ClearRuntimeVisuals'}
    # Every shared-pump release, binding teardown and visualization/layout method is unchanged.
    for name in ('ClearAudienceDeviceVisuals', 'ClearAudienceGraphDeviceVisuals',
                 'ClearSelectedSensorStreamMode', 'ClearSensorEngagementBackend',
                 'ClearManualDriverVisuals', 'AcquirePhysioPumpForChannel', 'ReleasePhysioPump',
                 'AttachGraphGroupLabel', 'PositionEmotionVisual', 'PositionGraphGroup'):
        assert name in untouched_methods
    assert 'Redis = 1' in current(TARGET)
    assert 'SignalSourceType.Redis;' in current(TARGET)
    assert 'Sine' not in current(TARGET)
    record = {'base_head': BASELINE['commit'], 'changed_sources': changed,
              'removed_sources': removed, 'added_sources': added,
              'other_source_files_byte_identical': len(unchanged),
              'unchanged_methods': untouched_methods, 'changed_methods': changed_methods,
              'added_methods': added_methods, 'scene_only_removes_null_sine_field': True,
              'existing_test_only_removes_sine_setup_and_assertion': True,
              'source_lines_before': len(original(TARGET).splitlines()),
              'source_lines_after': len(current(TARGET).splitlines()),
              'final_sha256': {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                               for name in changed + added}}
    (OUT / 'source-integrity.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
