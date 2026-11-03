"""Verify visibility-only serialization changes, saved assets and consumer scope."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASE = json.loads((OUT / 'baseline.json').read_text())
AUDIT = json.loads((OUT / 'consumer-audit.json').read_text())
TARGET = 'Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs'
PRESENTER = 'Assets/AmpPortableDataViz/Presentation/Bootstrap/VisualizationPresenter.cs'


def original(name):
    return subprocess.check_output(['git', 'show', BASE['commit'] + ':' + name],
                                   cwd=ROOT).decode('utf-8').replace('\r\n', '\n')


def current(name):
    return (ROOT / name).read_text().replace('\r\n', '\n')


if __name__ == '__main__':
    files = {}
    for scope in ('Assets', 'Packages', 'ProjectSettings'):
        for path in (ROOT / scope).rglob('*'):
            if path.is_file():
                files[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    changed = sorted(n for n in files.keys() & BASE['files'].keys()
                     if files[n] != BASE['files'][n]['sha256'])
    added = sorted(files.keys() - BASE['files'].keys())
    removed = sorted(BASE['files'].keys() - files.keys())
    assert not removed
    assert set(changed) == {TARGET, PRESENTER} | {
        f'Assets/Tests/PlayMode/Bootstrapper{name}Tests.cs' for name in
        ('ArtistViz', 'ChannelAndLabel', 'Lifecycle', 'Placement', 'RetainedGraph', 'Serialization')}
    assert set(added) == {'Assets/Tests/PlayMode/BootstrapperTestConfiguration.cs' + suffix
                          for suffix in ('', '.meta')}
    source = current(TARGET)
    reversed_source = source
    for field in AUDIT['fields']:
        declaration = '        [SerializeField] private ' + field['type'] + ' ' + field['name']
        assert len(re.findall(re.escape(declaration) + r'\b', source)) == 1, field['name']
        reversed_source = reversed_source.replace(declaration, '        public ' + field['type'] + ' ' + field['name'], 1)
    assert reversed_source == original(TARGET), 'Only the 50 visibility/serialization declarations may change'
    assert '        public void BeginAfterEndpoint()' in source
    assert not re.search(r'^        public [\w\[\]]+ \w+\s*(?:=|;)', source, re.M)
    diagnostics = json.loads((OUT / 'diagnostic-renames.json').read_text())
    presenter = original(PRESENTER)
    for old, new in diagnostics.items():
        assert presenter.count(old) == 1
        presenter = presenter.replace(old, new)
    assert presenter == current(PRESENTER), 'Only diagnostic setting names may change in the presenter'
    before_tests = json.loads((OUT / 'focused-before-source.json').read_text())
    assert all(files[name] == digest for name, digest in before_tests.items()), 'Tests changed after the baseline run'
    for scope in ('Assets/Scenes/', 'Assets/Prefabs/', 'Packages/', 'ProjectSettings/'):
        assert not any(name.startswith(scope) for name in changed + added + removed)
    # This includes all saved setting values, object references and UnityEvent wiring.
    scene = 'Assets/Scenes/ampPortable_xrGlassesDemo.unity'
    assert files[scene] == BASE['files'][scene]['sha256']
    record = {'base_head': BASE['commit'], 'serialized_private_field_count': len(AUDIT['fields']),
              'field_names_types_defaults_attributes_and_flat_layout_preserved': True,
              'bootstrapper_runtime_method_bodies_unchanged': True,
              'presenter_only_diagnostic_setting_names_changed': True,
              'diagnostic_messages_renamed': len(diagnostics),
              'existing_and_new_tests_identical_before_after_visibility_change': True,
              'all_scene_prefab_package_projectsettings_bytes_unchanged': True,
              'public_endpoint_unityevent_method_preserved': True,
              'changed_sources': changed, 'added_sources': added, 'removed_sources': removed,
              'other_original_files_byte_identical': len(BASE['files']) - len(changed),
              'step7_started': False,
              'final_sha256': {name: files[name] for name in changed + added}}
    (OUT / 'source-integrity.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))
