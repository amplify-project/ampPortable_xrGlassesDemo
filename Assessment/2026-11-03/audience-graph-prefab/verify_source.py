"""Check graph-only changes, exact retained prefab settings and test coverage."""
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
from sync_snapshot import ROOT, OUT, PROJECT, EXTRAS, scan

baseline = json.loads((OUT / 'baseline.json').read_text())
before = baseline['files']
after = scan(ROOT)
snapshot = scan(PROJECT)
new_tests = {'Assets/Tests/PlayMode/AudienceGraphPrefabTests.cs',
             'Assets/Tests/PlayMode/AudienceGraphPrefabTests.cs.meta'}
assert set(after) - set(before) == new_tests
assert not set(before) - set(after)
changed = sorted(name for name in before if before[name] != after[name])
assert set(changed) == {
    'Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs',
    'Assets/AmpPortableDataViz/Presentation/Bootstrap/VisualizationPresenter.cs',
    'Assets/Prefabs/AudienceGraphPrefab.prefab',
    'Assets/Tests/PlayMode/BootstrapperLifecycleTests.cs',
}
assert all(snapshot.get(name) == signature for name, signature in after.items())
assert set(snapshot) - set(after) == EXTRAS

asset = 'Assets/Prefabs/AudienceGraphPrefab.prefab'
original = subprocess.check_output(['git', 'show', baseline['base_commit'] + ':' + asset], cwd=ROOT)
original = original.decode().replace('\r\n', '\n')
current = (ROOT / asset).read_text()


def blocks(text):
    return {re.search(r' &(-?\d+)', block).group(1): block
            for block in re.split(r'(?m)(?=^--- !u!)', text)[1:]}


old_blocks, new_blocks = blocks(original), blocks(current)
removed = {'9171372730100571172', '7086274539701048422', '6503009827529151861'}
assert set(old_blocks) - set(new_blocks) == removed
assert not set(new_blocks) - set(old_blocks)
root = '606551006109817003'
expected = old_blocks[root]
for identifier in removed:
    expected = expected.replace('  - component: {fileID: ' + identifier + '}\n', '')
assert new_blocks[root] == expected
binding = '1829037997221100804'
assert new_blocks[binding] == old_blocks[binding].replace(
    '  liveSource: {fileID: 9171372730100571172}', '  liveSource: {fileID: 0}')
assert all(new_blocks[key] == old_blocks[key] for key in new_blocks if key not in (root, binding))

previous_suite = ET.parse(OUT.parent / 'rayneo-particle-prefab/full-final.xml').getroot()
full_suite = ET.parse(OUT / 'full.xml').getroot()
previous_tests = {test.get('fullname') for test in previous_suite.findall('.//test-case')}
current_tests = {test.get('fullname') for test in full_suite.findall('.//test-case')}
assert previous_tests <= current_tests
assert full_suite.get('failed') == '0' and full_suite.get('skipped') == '0'
assert int(full_suite.get('passed')) == len(current_tests)
record = {
    'base_commit': baseline['base_commit'],
    'changed_existing_inputs': changed,
    'unchanged_original_inputs': len(before) - len(changed),
    'new_test_inputs': sorted(new_tests),
    'all_workspace_inputs_match_tested_snapshot': True,
    'retained_prefab_settings_graphs_transforms_and_colliders_identical': True,
    'only_three_components_removed_and_live_source_cleared': True,
    'all_original_metadata_scenes_packages_project_settings_preserved': True,
    'existing_user_project_settings_changes_preserved': True,
    'all_previous_tests_retained': True,
    'previous_test_count': len(previous_tests),
    'new_tests': sorted(current_tests - previous_tests),
    'full_suite': {key: full_suite.get(key) for key in ('result', 'total', 'passed', 'failed', 'skipped')},
}
(OUT / 'source-verification.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2))
