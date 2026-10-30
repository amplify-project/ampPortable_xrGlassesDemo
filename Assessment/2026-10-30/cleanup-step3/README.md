# First obsolete-code removal

Step 3 on `cleanup/obsolete-code`, based on `369c06c8b873817dedaa01fe9f2cf16af21da423`, 2026-10-30. The user authorized proceeding from the [step 2 inventory](../cleanup-inventory/README.md) and stopping before step 4.

Removed four standalone components and their matching `.meta` files:

- `Assets/AmpPortableDataViz/Presentation/Visualization/FaceUserSmooth.cs`
- `Assets/AmpPortableDataViz/Presentation/Visualization/RuntimeImageBoard.cs`
- `Assets/AmpPortableDataViz/Presentation/Visualization/URPImageBoardApplier.cs`
- `Assets/AmpPortableDataViz/Presentation/Interaction/XRealControllerQuitter.cs`

The application change is eight file deletions, 321 lines. No retained source, scene, prefab, material, test, package, or setting was edited. The active score-image path (`RuntimePngLoader`), camera-facing labels, and RayNeo quit control (`QuitApp`) remain. This implements retirement of these four components in the current checkout; external scenes or manual workflows outside the reviewed project would need to retrieve them from Git history if required.

## Small changes and checks

1. Re-read all four implementations and metadata, and repeated the name/GUID reference checks before deletion. Only their own definitions/log messages matched.
2. Removed the camera-facing and controller-quit components with their metadata; checked for remaining names/GUIDs across all files under `Assets`, `Packages`, and `ProjectSettings`, including ignored/hidden files. No matches remained for that pair.
3. Removed the two alternate image components with their metadata and repeated the same check. No matches remained for that pair.
4. Regenerated the inventory and verified all 92 retained inventory rows are identical to step 2. Metadata diagnostics are clear. All 1,809 retained source input files are byte-identical to their pre-removal versions, with only the eight expected paths absent.
5. Synchronized the existing canonical isolated validation project to those exact source inputs; ran the existing full PlayMode suite, enabled-scene inspection, and Android build using Unity 2022.3.36f1. The user's open Editor project was not used for batch validation.

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Source integrity | Only the four scripts and four metadata files removed; all retained input bytes unchanged | [source-integrity.json](source-integrity.json) |
| Inventory | 92 scripts; zero unreviewed rows, missing script metadata, or duplicate asset GUIDs; retained rows unchanged | [code-inventory.csv](code-inventory.csv), [scan-summary.json](scan-summary.json) |
| Post-removal references | No deleted type names or GUIDs remain under `Assets`, `Packages`, or `ProjectSettings` | [source-integrity.json](source-integrity.json) |
| Full PlayMode suite | **196 passed, 0 failed, 0 skipped/inconclusive**, process exit 0 | [playmode-licensed.xml](playmode-licensed.xml), [run record](playmode-licensed-run.json) |
| Enabled scene and dependencies | **0 missing scripts, 0 detected broken references, 0 inspection warnings**; 248 components, 8 prefabs, 17 materials | [android-scene-inspection.json](android-scene-inspection.json) |
| Android player build | **Succeeded, 0 errors, 22 warnings**, ARM64 / IL2CPP / OpenGLES3, process exit 0 | [android-build-summary.json](android-build-summary.json), [run record](android-run.json) |
| Diff checks | `git diff --check` passed | Local verification before commit |

The fresh build recompiles retained code, reporting duplicate imports, nullable-context annotations, and obsolete Redis API usage in unchanged files. Its other warnings include Active Input Handling `Both`, USD support, and two `ShowFps` warnings from RayNeo SDK Resources prefabs (`XR Plugin Gaze` and `XR Plugin Texture`). Those same `ShowFps` messages occur in the earlier pre-cleanup `android-canonical-trust.log` at lines 2131 and 2190. They are outside the enabled-scene dependency inspection and unrelated to these four removed GUIDs. No warning-related code, SDK, or settings changes were made.

The first restricted Unity launch stalled before tests while connecting to licensing; only that launched process was stopped. The rerun with access to the local licensing service passed. The Android build initially encountered a cached compilation graph listing the deleted files (`CS2001`); Unity automatically regenerated its graph and compiled successfully within the same build. The final BuildReport records zero errors. No generated cache was hand-edited. These observations and log fingerprints are retained in [validation-summary.json](validation-summary.json); the interrupted launch record remains in [playmode-full-run.json](playmode-full-run.json).

The earlier assessment's intermittent halo assertion was not reproduced in this run; its historical failure remains recorded in the original assessment. No assertions were changed or suppressed.

## Validation artifacts and reproduction

[validation-snapshot.json](validation-snapshot.json) records the canonical temporary project, exact input synchronization, and assessment-only Editor helpers. The source inputs in that project match the checkout after validation; helper scripts exist only in the validation copy. Existing historical assessment outputs were preserved. Git long-path and Java public-certificate trust configuration applied only to launched processes.

APK: `Temp/CleanupStep3-20261030/android/RayNeo-assessment.apk`, 167,640,873 bytes. SHA-256: `ad4e6d2171b0b46e26b40ae54922d7dc452e13f5d29303c999216ca43704233d`. It was built for validation and was not installed on the glasses. The earlier user device acceptance applies to the pre-cleanup baseline; no new device acceptance is claimed.

The [runner](run_validation.ps1) assumes the recorded temporary project has first been synchronized to the intended source inputs, with the existing assessment `PreMergeValidation.cs` helper installed there. With that prepared, use new attempt names to preserve prior evidence:

```powershell
& ./Assessment/2026-10-30/cleanup-step3/run_validation.ps1 -Stage PlayMode -Attempt playmode-new
& ./Assessment/2026-10-30/cleanup-step3/run_validation.ps1 -Stage Build -Attempt android-new -UseWindowsTrustStore
```

The lexical scanner classifications still originate from the earlier assessment commit; these reports record the current working-tree deletions and fresh validation separately. Neither lexical searches nor Editor inspection prove the absence of external/binary/manual consumers. Inspection cannot distinguish intentional null references from every unresolved serialized field and does not establish physical-device behavior.

Stop boundary: step 3 only. Recovery scenes, wrist/hand support, RedisManager, LiveKit, plasma/HUD/legacy graph support, shared contracts, dependencies, and all other components remain. Step 4 has not started. No remote publishing was performed.
