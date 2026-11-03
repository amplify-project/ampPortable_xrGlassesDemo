"""Check extraction scope, Inspector compatibility and moved method bodies."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASE = json.loads((OUT / 'baseline.json').read_text())
FOLDER = 'Assets/AmpPortableDataViz/Presentation/Bootstrap/'
TARGET = FOLDER + 'Bootstrapper.cs'
STREAMS = ['EnsureSensorEngagementBackend', 'BuildSensorEngagementRoutes',
           'ResolveSensorEngagementChannel', 'BuildSensorEngagementConfigurationKey',
           'ResolveSensorEngagementSource', 'ClearSensorEngagementBackend',
           'AcquirePhysioPumpForChannel', 'ReleasePhysioPump',
           'BuildAudienceDeviceChannelDefinitions', 'GetOrCreateAudienceDefinition',
           'FilterAudienceToConfiguredDevices']
VISUALS = ['EnsureManualDriverVisuals', 'SpawnManualDriverPrefab',
           'ConfigureManualDriverInstance', 'EnsureSelectedSensorStreamMode',
           'SpawnSelectedSensorVisual', 'EnsureSensorStreamPresentation', 'EnsureSensorHud',
           'AddVisualizerBindings', 'EnsureAudienceDeviceVisuals',
           'EnsureAudienceGraphDeviceVisuals', 'TryConfigureAudienceBinding',
           'DisableManualDrivers', 'ClearAudienceDeviceVisuals',
           'ClearAudienceGraphDeviceVisuals', 'ClearSelectedSensorStreamMode',
           'ClearManualDriverVisuals']
LAYOUT = ['PositionAbstractViz', 'PositionGraphGroup', 'PositionAtParentOrigin',
          'AttachDeviceLabel', 'AttachGraphGroupLabel', 'FormatGraphGroupLabel',
          'FormatDeviceLabel', 'ExtractTrailingDigits', 'ResolveDeviceLabelParent']


def original(name):
    return subprocess.check_output(['git', 'show', BASE['commit'] + ':' + name],
                                   cwd=ROOT).decode('utf-8').replace('\r\n', '\n')


def current(name):
    return (ROOT / name).read_text().replace('\r\n', '\n')


def methods(source):
    pattern = (r'^        (?:private|internal|public) (?:static )?[\w<>\[\], ]+ '
               r'(\w+)\([^;]*?\)\s*\n        \{.*?^        \}')
    return {m[1]: m[0][m[0].index('        {'):] for m in re.finditer(pattern, source, re.M | re.S)}


def normalize(body):
    body = body.replace('_settings.', '').replace('UnityEngine.Object.', '')
    body = body.replace('_owner.transform', 'transform').replace('_owner.GetComponent', 'GetComponent')
    body = body.replace('SetParent(_owner, false)', 'SetParent(transform, false)')
    body = body.replace('_owner.AddComponent', 'gameObject.AddComponent').replace('                    _owner);', '                    this);')
    for prefix in ('_streams.', '_layout.', 'Streams.', 'Visuals.', 'RedisStreamRegistry.'):
        body = body.replace(prefix, '')
    body = body.replace('SensorEngagementChannelRoute channelOverride', 'SensorEngagementChannelOverride channelOverride')
    return body.replace('            ConfigureStreams();\n', '')


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
    expected_changed = {TARGET} | {f'Assets/Tests/PlayMode/Bootstrapper{name}Tests.cs'
        for name in ('ArtistViz', 'ChannelAndLabel', 'Lifecycle', 'Placement', 'RetainedGraph')}
    assert set(changed) == expected_changed, changed
    assert not removed
    assert set(added) == {FOLDER + name + '.cs' + suffix
                          for name in ('RedisStreamRegistry', 'VisualizationPresenter', 'VisualizationLayout')
                          for suffix in ('', '.meta')} | {
        'Assets/Tests/PlayMode/RedisStreamRegistryTests.cs' + suffix for suffix in ('', '.meta')}
    old = original(TARGET)
    new = current(TARGET)
    header_start = '        [Header("Scene Dependencies")]'
    assert old[old.index(header_start):old.index('        private ')] == new[new.index(header_start):new.index('        private ')]
    override = r'^        \[Serializable\]\n        public struct SensorEngagementChannelOverride.*?^        \}'
    assert re.search(override, old, re.M | re.S)[0] == re.search(override, new, re.M | re.S)[0]
    source = {'Bootstrapper': new}
    for name in ('RedisStreamRegistry', 'VisualizationPresenter', 'VisualizationLayout'):
        source[name] = current(FOLDER + name + '.cs')
        assert not re.search(r'\bBootstrapper\b(?!:)', source[name]), name
        assert 'MonoBehaviour' not in source[name]
    before = methods(old)
    after = {name: methods(text) for name, text in source.items()}
    assert len(before) == 52
    owners = {name: 'RedisStreamRegistry' for name in STREAMS}
    owners.update({name: 'VisualizationPresenter' for name in VISUALS})
    owners.update({name: 'VisualizationLayout' for name in LAYOUT})
    checked = []
    for name, body in before.items():
        owner = owners.get(name, 'Bootstrapper')
        actual = normalize(after[owner][name])
        if name == 'EnsureSensorHud':
            # The existing guard stays with the owner; creation moves into Layout.
            prefix = body[:body.index('            Camera hudCamera')]
            assert actual == prefix + '            _sensorHudObject = CreateSensorHud(_sensorStreamCoordinator);\n        }'
            creation = normalize(after['VisualizationLayout']['CreateSensorHud'])
            creation = creation.replace('hudObject', '_sensorHudObject').replace('coordinator', '_sensorStreamCoordinator')
            creation = creation.replace('var _sensorHudObject =', '_sensorHudObject =')
            creation = creation.replace('return null;', 'return;').replace('            return _sensorHudObject;\n', '')
            actual = prefix + creation[len('        {\n'):]
        assert actual == body, (name, owner)
        checked.append({'method': name, 'owner': owner})
    scene = 'Assets/Scenes/ampPortable_xrGlassesDemo.unity'
    assert files[scene] == BASE['files'][scene]['sha256']
    record = {'base_head': BASE['commit'], 'changed_sources': changed, 'added_sources': added,
              'removed_sources': removed, 'other_original_files_byte_identical': len(BASE['files']) - len(changed),
              'inspector_declarations_defaults_and_migration_attributes_unchanged': True,
              'existing_user_scene_edit_byte_preserved_and_excluded_from_commits': True,
              'all_original_method_bodies_equivalent_after_explicit_owner_settings_substitution': checked,
              'lines_before': len(old.splitlines()), 'lines_after': len(new.splitlines()),
              'runtime_helper_layers': 'Presentation', 'step6_started': False,
              'final_sha256': {name: files[name] for name in changed + added}}
    (OUT / 'source-integrity.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
