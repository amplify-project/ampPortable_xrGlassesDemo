"""Verify the fix only changes the halo test's timing, not rendering or assertions."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASE = json.loads((OUT / 'baseline.json').read_text())
TARGET = 'Assets/Tests/PlayMode/ParticleMeshVisualizerOrthogonalityTests.cs'
CASE = 'CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside'


def methods(source):
    pattern = (r'^        (?:public|private) (?:static )?[\w<>\[\], ]+ '
               r'(\w+)(?:<[^>\n]*>)?\([^;]*?\)\s*\n        \{.*?^        \}')
    return {m[1]: m[0][m[0].index('        {'):] for m in re.finditer(pattern, source, re.M | re.S)}


if __name__ == '__main__':
    files = {}
    for scope in ('Assets', 'Packages', 'ProjectSettings'):
        for path in (ROOT / scope).rglob('*'):
            if path.is_file():
                files[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    changed = sorted(n for n in files.keys() & BASE['files'].keys()
                     if files[n] != BASE['files'][n]['sha256'])
    assert changed == [TARGET]
    assert files.keys() == BASE['files'].keys()
    old = subprocess.check_output(['git', 'show', BASE['commit'] + ':' + TARGET], cwd=ROOT).decode().replace('\r\n', '\n')
    new = (ROOT / TARGET).read_text().replace('\r\n', '\n')
    reproduction = json.loads((OUT / 'reproduction-source.json').read_text())['source']
    before, after = methods(old), methods(new)
    assert methods(reproduction)[CASE] == before[CASE], 'The reproduction must use the unchanged failing case'
    assert before.keys() <= after.keys()
    for name in before.keys() - {CASE}:
        assert before[name] == after[name], name
    assert after[CASE] == '        {\n            return VerifySynchronizedHaloDirections();\n        }'
    old_wait = '                yield return null;\n                yield return null;'
    new_wait = '''                IEnumerator readiness = WaitForHeartRateResponse(
                    negativeObject.GetComponent<ParticleMeshVisualizer>(),
                    positiveObject.GetComponent<ParticleMeshVisualizer>());
                // Keep failures in this iterator so its caller's clock-restoration finally runs.
                while (readiness.MoveNext())
                {
                    yield return readiness.Current;
                }'''
    assert before[CASE].count(old_wait) == 1
    assert after['VerifySynchronizedHaloDirections'] == before[CASE].replace(old_wait, new_wait)
    assert 'Time.captureDeltaTime = previousCaptureDeltaTime;' in after['RunHaloDirectionCheckWithFrameDuration']
    assert 'Time.realtimeSinceStartupAsDouble + 5d' in after['WaitForHeartRateResponse']
    assert not re.search(r'\[(?:Ignore|Explicit)\b', new)
    record = {'base_head': BASE['commit'], 'changed_sources': changed,
              'other_original_input_files_byte_identical': len(files) - 1,
              'all_runtime_scene_prefab_package_projectsettings_and_metadata_bytes_unchanged': True,
              'reproduction_uses_unchanged_original_test_body': True,
              'original_signal_samples_geometry_assertions_tolerances_and_cleanup_unchanged': True,
              'other_original_test_and_helper_bodies_unchanged': sorted(before.keys() - {CASE}),
              'original_test_identity_retained': True,
              'no_ignored_tests_added': True,
              'shared_geometry_check_only_changes_timing_precondition': True,
              'five_second_realtime_timeout_and_clock_restoration_present': True,
              'final_test_sha256': files[TARGET]}
    (OUT / 'source-integrity.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
