# Retire optional application features

Step 5 on `cleanup/obsolete-code`, based on `3a7106d`, 2026-10-30. The user authorized retiring LiveKit, legacy plasma/HUD/graph modes, and RedisManager, while stopping before step 6.

## Resulting behavior and scope

- LiveKit is no longer an application source option. Removed its receiver/service, six exclusive test cases, Bootstrapper settings/branches, and Amp assembly reference. Existing serialized source values remain explicit: `Sine = 0`, `Redis = 1`. The checked-in application scene still selects Redis.
- Removed RedisManager and its Bootstrapper lifecycle/configuration paths. The active Redis endpoint, discovery, physio-metrics, per-sensor engagement, and selected-stream paths remain.
- Removed the older color-changing HUD visual/binding/mapper and its global pump setup. The current sensor-selection HUD (`ActiveSensorHudBinding`) remains.
- Removed plasma/raymarch visualizers, bindings, their two dedicated manual drivers, four exclusive mappers, affect contracts, and the binding-only frame smoother. Removed four obsolete plasma prefabs with their metadata so they cannot retain missing components.
- Removed the legacy scalar Redis graph binding, graph spawning/channel layout/panel paths, and their obsolete fields. Current audience graphs, graph contracts/mappers, and GraphVisualizer remain. `GraphPrefab.prefab` remains a manual tool: only its obsolete GraphBinding component and component-list entry were removed; GraphManualDriver and GraphVisualizer are preserved.

The checked-in application scene changed only by removing 35 retired serialized fields. Every other scene line is identical. Current particle, audience graph, artist, image-board, RayNeo control, and ArtisticElements assets remain unchanged. No change to the current four-view visual design is intended.

The project-input change is **50 file deletions, four modified inputs, and two new test files**. The four modified inputs are Bootstrapper, its assembly definition, the application scene, and the manual GraphPrefab. The 50 deletions comprise 19 runtime scripts, two exclusive test scripts, four plasma prefabs, and all 25 matching metadata files. Of the 1,750 original project inputs, 1,696 remain byte-identical. There are now **67 first-party runtime scripts**. Root README and AGENTS documentation describe the current status.

ArtisticElements, AudienceDataManualDriver, ParticleMeshManualDriver, GraphManualDriver, current shared graph contracts, session/fallback composition, and native Redis callbacks are preserved. The two plasma manual drivers are deliberately retired as part of the requested plasma feature retirement.

## Small implementation and validation chunks

1. Retired LiveKit, RedisManager, and the legacy HUD together with their composition paths. Removed only LiveKit's six tests. This chunk compiled and ran 190 cases: 189 passed, with the previously recorded intermittent halo assertion failing. That result is preserved.
2. Retired plasma and legacy Redis graph paths. Checked prefab consumers and removed only the four exclusive plasma prefabs. Preserved the manual graph prefab after removing its obsolete binding. Removed no shared graph declarations or active visualizer implementations.
3. Added three meaningful regression tests: manual graph rendering without a live source; manual mode with an unconfirmed Redis endpoint; and current selected-sensor graph wiring with local sources disabled. All three passed in isolation.
4. Ran the full suite on the final source, then enabled-scene dependency inspection and an Android player build in the existing isolated canonical Unity project. No batch validation ran in the user's open working project.

| Check | Result | Evidence |
| --- | --- | --- |
| Final full PlayMode suite | **193 passed, 0 failed, 0 skipped/inconclusive**, process exit 0 | [full.xml](full.xml), [run record](full-run.json) |
| Focused retained-graph regressions | **3 passed**, process exit 0 | [retained-graphs.xml](retained-graphs.xml) |
| Earlier source/HUD chunk | **189 passed, 1 failed** on the known halo-radius assertion | [sources-hud.xml](sources-hud.xml) |
| Enabled scene/dependencies | **0 missing scripts, 0 detected broken references, 0 inspection warnings**; 248 components, 8 prefabs, 17 materials | [android-scene-inspection.json](android-scene-inspection.json) |
| Android ARM64 / IL2CPP / OpenGLES3 | **Succeeded, 0 errors, 20 warnings**, process exit 0 | [android-build-summary.json](android-build-summary.json), [run record](android-run.json) |
| Reference/metadata checks | No remaining deleted GUIDs in project or installed package/SDK asset text; no missing metadata or duplicate asset GUIDs | [source-integrity.json](source-integrity.json), [asset-and-scope-review.json](asset-and-scope-review.json), [scan-summary.json](scan-summary.json) |

The final total is **196 previous cases - 6 LiveKit cases + 3 new regressions = 193**. All other 190 test names remain, and their source/assertions were not edited. See [test-scope-review.json](test-scope-review.json).

The earlier failed case was `CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside`: negative active radius equalled neutral radius rather than being smaller. The same intermittent assertion was recorded before this change. It passed in the final full run. No halo code or test was changed or suppressed; this work does not fix its underlying cause. All results and process exits remain in [validation-summary.json](validation-summary.json).

There are no new Android warning details compared with step 4. The two duplicate-import warnings from the retired RedisManager disappeared, reducing the count from 22 to 20. The remaining compiler warnings, Active Input Handling `Both`, USD platform warning, and RayNeo SDK `ShowFps` warnings are preserved without unrelated fixes.

## Evidence and retained behavior

[source-integrity.json](source-integrity.json) records exact input hashes, removals, additions, and method comparisons. Forty-seven retained Bootstrapper method bodies are unchanged after normalizing line endings, including endpoint confirmation, selected-stream orchestration, audience particle creation, physio pumps, sensor engagement routes, sensor cycling presentation, and current HUD creation. Twelve retained methods have the coordinated caller/configuration changes for the retired modes. [removed-bootstrap-members.json](removed-bootstrap-members.json) lists the plasma/legacy graph member retirement; HUD and LiveKit changes are recorded in the source diff.

[serialized-field-retirement.json](serialized-field-retirement.json) and [asset-and-scope-review.json](asset-and-scope-review.json) document the scene and prefab changes. The fresh [code-inventory.csv](code-inventory.csv) still uses the scanner's historical classification rules; current removal decisions are recorded here separately. Usual lexical-scan limitations apply, including external/binary/manual consumers and ambiguous names. Editor inspection cannot distinguish every unresolved serialized field from an intentional null and does not establish on-device behavior.

[validation-snapshot.json](validation-snapshot.json) identifies the temporary project; numbered snapshot records preserve the successive chunks. Its final source inputs match the checkout byte for byte. Assessment-only Editor helpers and Unity-managed caches remain isolated. The [runner](run_validation.ps1) requires that prepared snapshot and unique attempt names; it supports an optional PlayMode `-TestFilter`. Process-local Git long-path and Java public-certificate trust settings leave global settings unchanged. No generated cache was hand-edited.

APK: `Temp/CleanupStep5-20261030/android/RayNeo-assessment.apk`, 167,542,037 bytes; SHA-256 `6ed374ffd8a47cc20ae6112678379b68b588f9156a16a0d527996d3a2d1fa579`. It was not installed on the glasses. For a subsequent on-device check, inspect the same four views, sensor cycling/label, scaling and vertical movement, live graph/particle response, and quit/relaunch. The retained manual GraphPrefab can also be checked in the Editor by enabling its GraphManualDriver. No new device acceptance is claimed.

## Stop boundary

**Step 6 has not started.** Package manifests and lock files are unchanged. The LiveKit SDK package, imported samples, and unrelated/orphaned material/shader assets remain for separately scoped dependency/asset pruning; the application integration is retired. Generic pose/session interfaces remain because they belong to retained contracts and tests. No additional source/scene retirement or package cleanup was performed. No remote push was made.
