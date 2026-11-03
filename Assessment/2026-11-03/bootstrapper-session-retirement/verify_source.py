"""Verify the session retirement and serialized-field migration against the baseline."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASE = json.loads((OUT / 'baseline.json').read_text())
RENAMES = json.loads((OUT / 'field-renames.json').read_text())
TARGET = 'Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs'
SCENE = 'Assets/Scenes/ampPortable_xrGlassesDemo.unity'


def original(name):
    return subprocess.check_output(['git', 'show', BASE['commit'] + ':' + name],
                                   cwd=ROOT).decode('utf-8').replace('\r\n', '\n')


def current(name):
    return (ROOT / name).read_text().replace('\r\n', '\n')


def rename(source):
    for old, new in (RENAMES['fields'] | RENAMES['methods']).items():
        source = re.sub(r'\b' + old + r'\b', new, source)
    return source


def methods(source):
    pattern = (r'^        (?:private|public) (?:static )?[\w<>\[\], ]+ '
               r'(\w+)\([^;]*?\)\s*\n        \{.*?^        \}')
    return {m[1]: m[0] for m in re.finditer(pattern, source, re.M | re.S)}


if __name__ == '__main__':
    files = {}
    for scope in ('Assets', 'Packages', 'ProjectSettings'):
        for path in (ROOT / scope).rglob('*'):
            if path.is_file():
                files[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    changed = sorted(n for n in files.keys() & BASE['files'].keys()
                     if files[n] != BASE['files'][n]['sha256'])
    removed = sorted(BASE['files'].keys() - files.keys())
    added = sorted(files.keys() - BASE['files'].keys())
    expected_changed = {TARGET, SCENE,
        'Assets/AmpPortableDataViz/Core/Interfaces.cs',
        'Assets/AmpPortableDataViz/Core/VisualizationContracts.cs',
        'Assets/Tests/PlayMode/BootstrapperArtistVizTests.cs',
        'Assets/Tests/PlayMode/BootstrapperLifecycleTests.cs',
        'Assets/Tests/PlayMode/BootstrapperRetainedGraphTests.cs',
        'Assets/Tests/PlayMode/InterfacesTests.cs',
        'Assets/Tests/PlayMode/InterfacesEdgeCaseTests.cs'}
    assert set(changed) == expected_changed, changed
    retired = json.loads((OUT / 'retired-assets.json').read_text())
    assert set(removed) == {n for item in retired for n in (item['path'], item['path'] + '.meta')} | {
        'Assets/AmpPortableDataViz/Presentation/Anchors.meta'}
    assert set(added) == {f'Assets/Tests/PlayMode/Bootstrapper{name}Tests.cs{suffix}'
                         for name in ('Placement', 'Serialization') for suffix in ('', '.meta')}
    expected_scene = original(SCENE)
    for name in ('AnchorRegistry', 'RedisSource', 'IsHost', 'VisualId', 'ProfileId', 'Seed',
                 'ActiveSignalSource', 'AnchorLabel', 'SpawnTopic', 'ParamTopic'):
        expected_scene, count = re.subn(r'^  ' + name + r':.*\n', '', expected_scene, flags=re.M)
        assert count == 1
    expected_scene, count = re.subn(r'^  MapperSettings:\n(?:    .*\n)+', '', expected_scene, flags=re.M)
    assert count == 1
    for old, new in RENAMES['fields'].items():
        expected_scene = re.sub(r'^  ' + old + ':', '  ' + new + ':', expected_scene, flags=re.M)
    assert current(SCENE) == expected_scene
    before, after = methods(rename(original(TARGET))), methods(current(TARGET))
    assert len(before) == 62 and len(after) == 52
    assert not set(after) - set(before), 'No helper extraction is authorized'
    unchanged = sorted(n for n in after if after[n] == before[n])
    for name in ('AcquirePhysioPumpForChannel', 'ReleasePhysioPump', 'ClearAudienceDeviceVisuals',
                 'ClearAudienceGraphDeviceVisuals', 'ClearSelectedSensorStreamMode',
                 'ClearSensorEngagementBackend', 'ClearManualDriverVisuals',
                 'BuildSensorEngagementConfigurationKey', 'PositionAbstractViz',
                 'PositionGraphGroup', 'PositionAtParentOrigin', 'AttachGraphGroupLabel'):
        assert name in unchanged
    pattern = r'        \[Header\("Individual Sensor Engagement"\)\].*?(?=        \[Header\("RayNeo Selected Sensor Stream"\)\])'
    assert re.search(pattern, original(TARGET), re.S)[0] == re.search(pattern, current(TARGET), re.S)[0]
    placement_before = json.loads((OUT / 'placement-baseline.json').read_text())['source']
    assert rename(placement_before) == current('Assets/Tests/PlayMode/BootstrapperPlacementTests.cs')
    for old, new in RENAMES['fields'].items():
        assert re.search(r'\[FormerlySerializedAs\("' + old + r'"\)\].*?public [^\n]*\b' + new,
                         current(TARGET), re.S)
    retired_symbols = r'\b(?:VisualizationSessionController|VisualizationSessionOptions|AnchorRegistry|IAnchorRegistry|IAnchorService|AnchorDescriptor|AnchoredVisualization|MockAnchorService|LocalLoopbackNetworkSync|UnityVisualizationFactory|IVisualizationFactory|IVisualizationInstance|VisualizationSpawnRequest|PoseSample|SimpleVisualizer|SimpleVisualParams|FloatToSimpleParams|INetworkSync|SignalSourceType)\b'
    for scope in ('Assets/AmpPortableDataViz', 'Assets/Tests'):
        for path in (ROOT / scope).rglob('*.cs'):
            assert not re.search(retired_symbols, path.read_text()), path
    guids = {item['guid'] for item in retired}
    for path in (ROOT / 'Assets').rglob('*'):
        if path.is_file() and path.suffix in ('.unity', '.prefab', '.asset', '.asmdef'):
            assert not guids & set(re.findall(r'guid: ([0-9a-f]{32})', path.read_text(errors='ignore'))), path
    record = {'base_head': BASE['commit'], 'changed_sources': changed, 'removed_sources': removed,
              'added_sources': added, 'other_original_files_byte_identical': len(BASE['files']) - len(changed) - len(removed),
              'scene_only_removes_retired_settings_and_renames_retained_fields': True,
              'engagement_configuration_unchanged': True, 'placement_test_assertions_unchanged': True,
              'retired_symbols_and_asset_guids_absent': True, 'helper_extraction_started': False,
              'methods_before': len(before), 'methods_after': len(after),
              'removed_methods': sorted(set(before) - set(after)),
              'unchanged_methods_after_renaming': unchanged,
              'changed_retained_methods': sorted(set(after) - set(unchanged)),
              'lines_before': len(original(TARGET).splitlines()), 'lines_after': len(current(TARGET).splitlines()),
              'final_sha256': {name: files[name] for name in changed + added}}
    (OUT / 'source-integrity.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
