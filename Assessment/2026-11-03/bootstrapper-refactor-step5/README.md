# Bootstrapper step 5 — focused Presentation helpers

Completed the three authorized extractions in separate, behavior-preserving batches.
Step 6 has not started. Bootstrapper now has **524 lines**, down from **1,698**.

| Batch | Helper | Responsibility | Validation |
| --- | --- | --- | --- |
| 1, `e10718e` | `RedisStreamRegistry` | Sensor-channel definitions, shared physiological pumps, acquisition/reference counts/release, engagement backend and routing | **203/203** full tests |
| 2, `b770931` | `VisualizationPresenter` | Automatic/manual instance creation, source/visualizer binding, selected-stream presentation and owned-instance cleanup | **203/203** full tests |
| 3 | `VisualizationLayout` | Parent-local/world positioning, rows/spacing, device/group labels and sensor HUD creation/style | **205/205** full tests |

All three helpers live in `Assets/AmpPortableDataViz/Presentation/Bootstrap`. They
are ordinary internal C# classes, not scene components, and do not reference the
Bootstrapper type. Unity creation and presentation operations stay in Presentation;
Infra services and Application/Core contracts are unchanged.

Bootstrapper retains every Inspector declaration, default, migration attribute,
scene reference, endpoint/discovery setting, mode decision, dependency construction
and startup/shutdown coordination. It supplies transient configuration values to
the helpers. No new Inspector fields, packages, serialized scene components or
feature modes were introduced.

The presenter uses the registry for streams and the layout helper for placement
and labels. It owns the created view/source instances, coordinator/cycler wiring
and HUD lifetime. The registry owns pump sharing and the engagement backend.
Bootstrapper preserves the teardown sequence: stop discovery, clear per-device
abstract instances, graph groups, selected streams/targets/HUD, engagement backend,
then manual instances. Individual cleanup operations and driver precedence remain
the same.

## Validation

- Batch 1: **203/203** full PlayMode tests, exit 0.
- Batch 2: **203/203** full PlayMode tests, exit 0.
- Batch 3: **205/205** full PlayMode tests, exit 0.
- Android IL2CPP APK: **Succeeded**, **0 errors**, **20 unchanged warning messages**.
- Saved-scene inspection: **0 missing scripts**, **0 broken references**, **0 blockers**;
  248 scene components, 8 prefabs and 17 materials inspected.

All **199 prior test identities are retained**. Existing tests invoke the new
owners while keeping their assertions/case data. Four registry cases add coverage
for final-consumer release, raw channel identity, endpoint reconfiguration and
engagement backend reuse/replacement/cleanup. Two HUD cases verify camera-parent
placement, style/binding and minimum scale/font fallbacks. No test failures or
retries occurred during this step; the previously recorded halo flake was not
changed or suppressed.

Tests use inactive real transport components and do not require a Redis server.
They verify lifecycle/orchestration, binding, serialization and placement behavior;
they do not substitute for a live Redis or wearer/device test. The APK was built
but not installed. No remote push was performed.

## Source and scene preservation

`verify_source.py` compares the final source to the recorded baseline and verifies:

- All **52 original method bodies** remain equivalent after explicitly substituting
  the new owners and configuration fields. The existing HUD guard stays with its
  owner; the unchanged creation operations move into the layout helper.
- Inspector declarations/defaults/migration attributes and the serialized engagement
  override type are unchanged. Existing serialization migration tests still pass.
- No original source input was removed. **1,673 other original files** under
  Assets/Packages/ProjectSettings are byte-identical, including all scene/prefab
  assets, packages, visualizers, input and other layers.
- The three new runtime files are the Presentation helpers, each with matching
  metadata; the additional new test script also has matching metadata.

The checkout already contained a scene edit renaming the parent GameObject from
EmotionVisualParent to AbstractVizParent. That edit is byte-preserved, included in
validation snapshots, and excluded from all three commits. Its initial diff is
recorded in `existing-scene-edit.diff`. This work makes no additional scene changes.

The first two batch notes are [streams.md](streams.md) and [visuals.md](visuals.md).
`source-integrity.json` records ownership/method equivalence and final file hashes.
`validation-summary.json`, XML results and run records retain validation evidence.

## Reproduction and device inspection

Run `python verify_source.py` for scope/equivalence checks. The snapshot tool copies
and verifies source inputs into the idle isolated Unity project recorded in the
snapshot records; generated caches are left to Unity. `run_validation.ps1 -Stage
PlayMode -Attempt <unique-name>` runs tests. Android validation uses `-Stage Build
-UseWindowsTrustStore`, retaining process-local Git/Java settings. Raw logs and APK
output are under ignored `Temp/BootstrapperRefactorStep5-20261103/` paths.

No visual output change is intended. A later Editor/device inspection should show
the same prefab assignments, initial placement, labels/HUD, sensor/view cycling,
manual tools and quit/relaunch behavior. The original step 6 remains deferred.
