# RayNeo particle prefab cleanup and component names

`ParticleMeshVisualizer` keeps its name and is byte-identical to the starting
version. Three components now have names matching their responsibilities:

| Previous class | New class |
| --- | --- |
| AudienceSignalBinding | SensorSampleAssembler |
| AudienceDataManualDriver | SensorDataSimulator |
| AudienceSignalVisualizerBinding | SensorVisualizationBinding |

Each script and its `.meta` moved together. Script GUIDs, existing serialized
field names and configuration values are preserved; `MovedFrom` attributes record
the previous types. Callers, component menu labels and serialized class identifiers
were updated. The binding smoothing test fixture was renamed with its metadata.

## Prefab and startup changes

Unity's `PrefabUtility` removed the root XRGrabInteractable and Rigidbody, and the
unused local SensorSampleAssembler, from `ParticleMeshVizRoot_RayNeo` only. The
variant clears its visualization binding's inherited local live-source reference.
The generated YAML diff contains three component-removal overrides and one null
source override. The collider, transforms, materials and visualization tuning are
preserved. SensorDataSimulator remains available for Editor/manual development
and continues to be disabled for live Redis operation.

Bootstrapper now checks the visualization binding when selecting this abstract
view. Selected mode continues to create separate per-sensor source objects.
Retained per-device mode creates an assembler on the instance when a prefab has
only a visualization binding, configuring it while inactive before enabling it.
Shared base-prefab interaction components and development sources remain intact.

## Validation

Unity 2022.3.36f1 ran with Android as the active build target in the existing
isolated validation project, without closing the user's Editor. Source hashes
matched all 1,691 workspace input files before the final suite. Validation-only
Editor helpers live in that isolated project, not application Assets.

| Check | Result |
| --- | --- |
| Renamed bindings, sensor assembly and ArtistViz wiring | 11/11 passed |
| Prefab, lifecycle, retained graph and sensor presentation group | 20/20 passed |
| Final full PlayMode suite, including the additional real-prefab source-switching test | **219/219 passed**, exit 0, none skipped |
| Metadata and source-scope audit | Passed |

The eleven new cases check missing scripts across five related prefabs; RayNeo
component wiring and base-prefab preservation; a real physics gaze raycast without
a Rigidbody; live selected-sensor routing, latest-sample replay and disable/enable;
manual samples reaching the renderer without Redis; selected-mode spawning without
a prefab-local assembler; and per-device source creation and teardown.

All 208 previous test cases remain, accounting for the smoothing fixture rename.
The halo regressions pass unchanged; historical failure records remain untouched.

The initial sandbox launch stopped before compilation with Unity licensing exit
199. A launch with access to the installed licensing service passed. The first
prefab regression run passed 19/20: a new teardown assertion counted the retained
graph template as a leaked source. The corrected assertion tracks owned runtime
assemblers and verifies their destruction; the passing rerun and original results
are both retained.

`source-verification.json` confirms all renamed metadata is byte-identical and
1,669 other original input files are unchanged. This includes every scene,
package file, project setting and the shared root prefab. The user's pre-existing
ProjectSettings.asset changes are preserved relative to the captured baseline.

No APK build or on-device run was performed for this change. After rebuilding,
check view cycling, sensor selection, gaze-targeted scaling and vertical movement
on the glasses. No visual tuning or renderer logic changed.

## Reproduction

With the recorded isolated project idle, run:

```powershell
python Assessment/2026-11-03/rayneo-particle-prefab/sync_snapshot.py repeat
& Assessment/2026-11-03/rayneo-particle-prefab/run_validation.ps1 -Stage PlayMode -Attempt repeat
python Assessment/2026-11-03/rayneo-particle-prefab/verify_source.py
```

Use a unique attempt name to retain earlier evidence. Test XML and process records
are in this directory; raw logs are under ignored `Temp/RayNeoParticlePrefab-20261103/`.
`RayNeoParticlePrefabMigration.cs` records the Unity API migration used to produce
the variant overrides; it runs only in the isolated project.
