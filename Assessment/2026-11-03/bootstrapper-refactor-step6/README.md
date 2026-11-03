# Bootstrapper step 6 — configuration visibility and saved values

Completed the remaining configuration cleanup. The earlier sensor/AbstractViz/
spawn-field renames and all eleven `FormerlySerializedAs` mappings were already
present and remain unchanged. Step 7 has not started.

## Consumer audit and implementation

`consumer-audit.json` inventories all **50 top-level configuration fields**, their
types and direct external consumers. The only consumers are six PlayMode test
files; production code does not read or write these fields from outside
Bootstrapper. Runtime reflection consumers were checked, and the saved scene's
UnityEvent calls the retained public `BeginAfterEndpoint()` method.

All fifty fields are now **`[SerializeField] private`**. Their field names, types,
initializers, declaration order, Inspector names, headers/ranges/tooltips and
migration aliases remain identical. The configuration stays flat on Bootstrapper.
No field is moved into a serialized settings object, and no replacement public
mutable properties or test-only runtime setters are introduced.

The public inline `SensorEngagementChannelOverride` value type and its row members
retain their existing representation. The helpers' internal transient settings
records remain ordinary runtime data rather than serialized scene configuration.

Three presenter diagnostic messages now use the Inspector's AbstractViz/Graph
Prefab names instead of leaking the helper's `_settings` prefix. No presenter
branch, binding or lifecycle operation changes.

The tests configure fields through the new test-only `BootstrapperTestConfiguration`
reflection utility. Existing test values, cases and assertions are retained. No
runtime API is exposed merely to support tests.

## Serialization and validation

The new `CurrentPrefab_RoundTripsEveryConfigurationField` test supplies nondefault
values to every one of the fifty fields, saves a prefab, then imports its YAML as
a fresh asset. It verifies every field remains at the same flat serialized path,
including prefab/transform/camera asset references, arrays, engagement overrides,
JSON-format settings, colours and placement values. Unique temporary fixture
prefabs are removed in `finally`.

The existing legacy-prefab test still verifies all eleven former field names and
their references. The same test sources were run before and after the visibility
change; `focused-before-source.json` records their pre-change fingerprints.

- Before visibility change: **26/26 focused Bootstrapper tests passed**, exit 0.
- After visibility change: **206/206 full PlayMode tests passed**, exit 0.
- All 205 previous test identities are retained; one serialization test is added.
- Android IL2CPP APK: **Succeeded**, **0 errors**, **18 previously seen warnings**.
  No new warning messages occurred. Two earlier transient ShowFps missing-script
  warnings were not re-emitted in this incremental build; this step does not claim
  to fix them.
- Saved-scene inspection: **0 missing scripts, 0 broken references, 0 blockers**;
  248 scene components, 8 prefabs and 17 materials inspected.

The machine-readable final run/build/scene results are recorded in
`validation-summary.json`. Logs and APK output are under ignored
`Temp/BootstrapperRefactorStep6-20261103/` paths. The APK is not installed as part
of this step, and no on-device test is claimed.

## Scope verification

`verify_source.py` verifies that reversing only the fifty visibility declarations
restores the original Bootstrapper source exactly. This proves its runtime method
bodies, field defaults, retained names/types, migration attributes and flat layout
are unchanged. Presenter changes are restricted to the three diagnostic strings.

Every scene, prefab, existing metadata file, package and ProjectSettings file is
byte-identical to the baseline, preserving all saved field values, object references
and endpoint-event wiring. **1,679 other original source input files are unchanged**.
One test utility script and matching metadata are added; no assets are removed.

Run `python verify_source.py` to reproduce scope checks. `sync_snapshot.py <phase>`
copies/verifies source inputs into the idle isolated Unity project recorded in the
snapshot records without editing generated caches. `run_validation.ps1 -Stage
PlayMode -Attempt <unique-name>` runs tests; Android checks use `-Stage Build
-UseWindowsTrustStore` with the existing process-local Java/Git configuration.

This is step 6 validation only. No further refactor, package cleanup, merge,
deployment or step 7 work is included. The requested commit/push targets only the
current `cleanup/obsolete-code` branch. Inspector fields remain editable in Unity;
no active visual output or XR-device behavior change is intended.
