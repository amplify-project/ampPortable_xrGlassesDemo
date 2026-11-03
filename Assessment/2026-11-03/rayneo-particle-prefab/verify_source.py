"""Verify rename metadata, preserved inputs and retained full-suite coverage."""
from pathlib import Path
import json
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
from sync_snapshot import ROOT, OUT, scan

before = json.loads((OUT / 'baseline.json').read_text())['files']
after = scan(ROOT)
folder = 'Assets/AmpPortableDataViz/Presentation/Visualization/'
renames = {
    'AudienceSignalBinding': 'SensorSampleAssembler',
    'AudienceDataManualDriver': 'SensorDataSimulator',
    'AudienceSignalVisualizerBinding': 'SensorVisualizationBinding',
}
paths = {}
for old, new in renames.items():
    for suffix in ('.cs', '.cs.meta'):
        paths[folder + old + suffix] = folder + new + suffix
for suffix in ('.cs', '.cs.meta'):
    paths['Assets/Tests/PlayMode/AudienceSignalVisualizerBindingSmoothingTests' + suffix] = (
        'Assets/Tests/PlayMode/SensorVisualizationBindingSmoothingTests' + suffix)

new_tests = {
    'Assets/Tests/PlayMode/RayNeoParticlePrefabTests.cs',
    'Assets/Tests/PlayMode/RayNeoParticlePrefabTests.cs.meta',
}
assert set(before) - set(after) == set(paths)
assert set(after) - set(before) == set(paths.values()) | new_tests
for old, new in paths.items():
    if old.endswith('.meta'):
        assert before[old] == after[new], f'Rename metadata changed: {old}'
    else:
        assert not (ROOT / old).exists()

unchanged = sorted(name for name in set(before) & set(after) if before[name] == after[name])
changed = sorted(name for name in set(before) & set(after) if before[name] != after[name])
expected_changes = {
    'Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs',
    'Assets/AmpPortableDataViz/Presentation/Bootstrap/VisualizationPresenter.cs',
    folder + 'SensorStreamCoordinator.cs',
    'Assets/Prefabs/AudienceGraphPrefab.prefab',
    'Assets/Prefabs/ParticleMeshViz.prefab',
    'Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab',
    'Assets/Tests/PlayMode/ArtistVizAssetWiringTests.cs',
    'Assets/Tests/PlayMode/ArtistVizBindingTests.cs',
    'Assets/Tests/PlayMode/BootstrapperArtistVizTests.cs',
    'Assets/Tests/PlayMode/BootstrapperLifecycleTests.cs',
    'Assets/Tests/PlayMode/BootstrapperRetainedGraphTests.cs',
    'Assets/Tests/PlayMode/SensorEngagementIntegrationTests.cs',
}
assert set(changed) == expected_changes, changed
assert folder + 'ParticleMeshVisualizer.cs' in unchanged
assert 'ProjectSettings/ProjectSettings.asset' in unchanged
assert all(before[name] == after[name] for name in before if name.startswith(('Packages/', 'ProjectSettings/')))
assert all(before[name] == after[name] for name in before if name.startswith('Assets/Scenes/'))
assert 'Assets/Prefabs/ParticleMeshVizRoot.prefab' in unchanged

previous = ET.parse(OUT.parent / 'halo-timing-fix/full.xml').getroot()
current = ET.parse(OUT / 'full-final.xml').getroot()
previous_names = {
    item.get('fullname').replace('AudienceSignalVisualizerBindingSmoothingTests', 'SensorVisualizationBindingSmoothingTests')
    for item in previous.findall('.//test-case')
}
current_names = {item.get('fullname') for item in current.findall('.//test-case')}
assert previous_names <= current_names
assert current.get('failed') == '0' and current.get('skipped') == '0'
assert int(current.get('passed')) == len(current_names)

record = {
    'renames': paths,
    'all_renamed_metadata_byte_identical': True,
    'unchanged_original_inputs': len(unchanged),
    'changed_existing_paths': changed,
    'new_test_paths': sorted(new_tests),
    'particle_mesh_renderer_unchanged': True,
    'shared_root_prefab_unchanged': True,
    'all_scenes_packages_project_settings_unchanged': True,
    'existing_user_project_settings_changes_preserved': True,
    'previous_test_count': len(previous_names),
    'all_previous_tests_retained_with_fixture_rename': True,
    'new_tests': sorted(current_names - previous_names),
    'final_full_suite': {key: current.get(key) for key in ('result', 'total', 'passed', 'failed', 'skipped')},
}
(OUT / 'source-verification.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2))
