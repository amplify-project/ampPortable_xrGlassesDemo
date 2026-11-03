# Audience graph prefab cleanup

`AudienceGraphPrefab` no longer carries XRGrabInteractable, Rigidbody or a local
SensorSampleAssembler. The current RayNeo controls use transforms and gaze
raycasts. Selected-sensor mode uses independent runtime data sources, so these
components do not belong on its display prefab.

Bootstrapper now checks for SensorVisualizationBinding and GraphVisualizer when
deciding whether to spawn the graph view. Retained per-device mode creates an
assembler on each instance when needed and configures it before activation.
The prefab's stale liveSource reference is cleared. Its simulator and manual
workflow remain supported.

Unity's PrefabUtility performed the asset migration in an isolated project. An
object-by-object audit confirms exactly three component blocks and their root
component-list entries were removed. The liveSource field is the sole change to
any retained component. All five graph targets, metric settings, smoothing and
range settings, simulator values, transforms, labels, materials and colliders are
preserved. No script metadata, scene, dependency or project setting changed.
The user's existing ProjectSettings.asset edits remain outside these commits.

## Validation

Unity 2022.3.36f1 ran with Android as the active target. The full suite used an
isolated project whose 1,693 input files matched the workspace byte for byte.
Validation-only Editor helpers were kept outside the application's Assets.

| Check | Result |
| --- | --- |
| Focused graph, lifecycle, retained graph and smoothing group | 17/17 passed |
| Final full PlayMode suite | **225/225 passed**, exit 0, none skipped |
| Exact prefab preservation and source-scope audit | Passed; 1,687 other original input files unchanged |

Six new cases cover:

- The five graph targets and simulator remaining correctly wired without grab
  physics or a local assembler, with no missing scripts.
- A real physics gaze raycast against the retained collider without a Rigidbody.
- Selected-sensor data reaching all five graphs exactly once per frame, ignoring
  other sensors, resetting history on switching, and replaying the latest sample
  after disable/enable.
- Manual samples reaching all five graphs without Redis.
- Selected-mode spawning, switching, endpoint reconfirmation and disable/re-enable
  without a prefab-local assembler.
- Per-device source creation, a transition to selected mode, and cleanup of the
  owned assemblers and Redis pumps.

The initial focused run passed 16/17. The manual test initially counted history
before its first explicit sample, missing the simulator's existing name-based
sensor identity: the spawned prefab is renamed after instantiation, and the next
sample correctly resets history for that changed ID. Test setup now establishes
the runtime identity before asserting the subsequent append. Both runs are
retained; no runtime workaround was introduced.

The previous 219 test cases remain. Historical halo failure evidence and the
timing regression tests remain unchanged. No new APK build or on-device run is
claimed. After rebuilding, inspect graph updates, sensor switching, gaze-targeted
scaling and vertical movement on the glasses.

## Reproduction

With the recorded isolated project idle, run:

```powershell
python -B Assessment/2026-11-03/audience-graph-prefab/sync_snapshot.py repeat
& Assessment/2026-11-03/audience-graph-prefab/run_validation.ps1 -Stage PlayMode -Attempt repeat
python -B Assessment/2026-11-03/audience-graph-prefab/verify_source.py
```

Use a unique attempt name. XML and process records are retained here; raw logs
are under ignored `Temp/AudienceGraphPrefab-20261103/`. The migration helper runs
only in the isolated project and saves only AudienceGraphPrefab.
