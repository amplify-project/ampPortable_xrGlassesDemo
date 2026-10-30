# Retire historical scenes and their exclusive helpers

Step 4 on `cleanup/obsolete-code`, based on `f3148ba`, 2026-10-30. The user explicitly chose: **"Retire both recovery scenes and the plasma manual scene."** Step 5 is outside this change.

Removed all 22 scenes under `Assets/_Recovery`, `Assets/Scenes/plasmaManual.unity`, their metadata, and the empty recovery folder's metadata. Git history at `f3148ba` retains the original scenes. No enabled build scene was removed or edited.

After checking scene GUIDs, serialized script references, C# consumers, tests, and installed package/SDK references, removed six helpers whose consumers were confined to the retired recovery group:

- `Infra/IHandPoseProvider.cs`
- `Presentation/Debug/EmotionTelemetryLogger.cs`
- `Presentation/Interaction/VisualizationCycler.cs`
- `Presentation/Visualization/HudSquareBuilder.cs`
- `Presentation/Visualization/ToggleButtonColor.cs`
- `Presentation/Visualization/WristMenuFollower.cs`

Paths above are relative to `Assets/AmpPortableDataViz`; each matching `.meta` was removed in the same change. The wrist follower and its interface were removed together. The retired scenes also contained the previously recorded stale `CycleVisualization` UnityEvents, so no repair to those historical callbacks is needed in the retained app.

## Scope and checks

The change removes 59 project input files: 23 scenes and their 23 metadata files, one folder metadata file, and six scripts with six metadata files. Root `README.md` now describes the cleanup status and links here. No retained project source, test, scene, prefab, material, package, or setting was edited.

1. Captured hashes of all 1,809 pre-step-4 input files. Confirmed the retiring scene/folder GUIDs had no incoming references elsewhere in the checked project text. Re-read the helper implementations and checked their references in the project, installed packages, and local RayNeo SDK.
2. Removed the scenes and their metadata first. Verified exactly 47 expected removals, no retired scene/folder GUID references, and unchanged bytes for the remaining 1,762 inputs.
3. Removed the six exclusive helpers and their metadata. Regenerated the inventory and checked all retired names/GUIDs again. The remaining 86 scripts have unchanged source hashes, metadata GUIDs, and test callers. All 1,750 retained project input files remain byte-identical.
4. Synchronized the canonical isolated validation project to those exact inputs and ran PlayMode, scene inspection, and the Android build. No batch validation ran in the user's open working project.

See [retirement-review.json](retirement-review.json) for the exact removal set and prior consumers, [source-integrity.json](source-integrity.json) for byte/reference checks, and [code-inventory.csv](code-inventory.csv) with [scan-summary.json](scan-summary.json) for the refreshed scan. Eleven retained inventory rows have expected reference-evidence changes after the group retirement. The scanner's fixed classification labels still originate from the historical assessment; the retention decisions below describe current usage.

## Validation results

| Check | Result | Evidence |
| --- | --- | --- |
| Latest cleanup full PlayMode suite | **196 passed, 0 failed, 0 skipped/inconclusive**, process exit 0 | [playmode-after-baseline.xml](playmode-after-baseline.xml), [run record](playmode-after-baseline-run.json) |
| Earlier cleanup full suite and rerun | Each **195 passed, 1 failed** on the previously recorded halo-radius assertion | [first run](playmode.xml), [rerun](playmode-repeat.xml) |
| Halo test in isolation | **1 passed** | [halo-focused.xml](halo-focused.xml) |
| Controlled pre-step-4 parent comparison | **196 passed** with all 1,809 parent input hashes verified in the temporary project | [parent-baseline.xml](parent-baseline.xml), [baseline snapshot](parent-baseline-snapshot.json) |
| Enabled scene and dependencies | **0 missing scripts, 0 detected broken references, 0 inspection warnings**; 248 components, 8 prefabs, 17 materials | [android-scene-inspection.json](android-scene-inspection.json) |
| Android ARM64 / IL2CPP / OpenGLES3 build | **Succeeded, 0 errors, 22 warnings**, process exit 0 | [android-build-summary.json](android-build-summary.json), [run record](android-run.json) |

The failing test was `CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside`: the negative active radius equalled the neutral radius, `0.0800000057`, rather than being smaller. This same intermittent assertion was recorded before cleanup. It passed alone. After the unchanged parent passed, the temporary project was restored to the cleanup state, all 1,750 input hashes were verified again, and the full suite passed. These results demonstrate variability in the cleanup state; they do not diagnose or fix the underlying cause. No assertions, fixtures, or retained visualizer code were changed. All failed and successful results are preserved in [validation-summary.json](validation-summary.json).

The Android warning details are identical to step 3: existing compiler warnings in retained code, Active Input Handling `Both`, USD platform support, and two `ShowFps` warnings in RayNeo SDK Resources prefabs. The SDK warnings remain distinct from the clean enabled-scene inspection. No warning-related fixes or dependency changes were made.

APK: `Temp/CleanupStep4-20261030/android/RayNeo-assessment.apk`, 167,629,213 bytes; SHA-256 `6a78c0ee99122caeae9ebfca2baf7a41f625244f03533c7400dbc362b4931ca5`. It was not installed on the glasses. No new device acceptance or visual change is claimed.

## Retained dependencies and stop boundary

| Retained area | Reason it remains |
| --- | --- |
| `RedisManager` | Recovery attachments are gone, but Bootstrapper still finds, configures, and manages the wrapper. Removing it requires coordinated runtime edits. |
| LiveKit receiver, service, tests, and package references | The serialized recovery attachment is gone; optional Bootstrapper source support and tests remain. No decision to retire this source was included in the scene retirement. |
| HUD binding, visual, and mapper | Recovery attachments are gone; Bootstrapper still configures `EngagementHudBinding`, which depends on `ColourChangingHudSide` and its mapper. |
| Plasma/raymarch components, manual drivers, prefabs, materials, shaders, and contracts | The dedicated manual scene is retired; optional Bootstrapper support and retained prefab components still depend on this code. Their separate runtime/asset retirement has not started. |
| Legacy graph and particle manual support | Typed Bootstrapper, factory, visualizer, or prefab dependencies remain. Current graph/audience contracts are also shared. |
| Current RayNeo app and development features | Main scene, image loader, selected-sensor presentation, current particle/graph/artist visuals, endpoint/native callbacks, Resources settings, ArtisticElements scene/tests, and fallback/session composition are retained. |

[validation-snapshot.json](validation-snapshot.json) records the synchronized temporary project and assessment-only Editor helpers. It was restored to the cleanup inputs after the parent comparison. Raw logs and the APK remain under ignored `Temp/CleanupStep4-20261030`; the reports include their hashes. The [runner](run_validation.ps1) requires that prepared snapshot and accepts unique attempt names, plus an optional `-TestFilter` for PlayMode. Git and Java trust options apply only to launched processes; no generated cache or global setting was hand-edited.

The usual text-scan and Editor-inspection limits still apply: external/binary/manual consumers cannot be fully ruled out, intentional null fields cannot always be distinguished from unresolved references, and Editor/build results do not establish physical-device behavior.

Step 4 ends with the authorized scenes and six exclusive helpers retired. **Step 5 has not started.** No remote push was performed.
