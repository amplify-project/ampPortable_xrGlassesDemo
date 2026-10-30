# Pre-merge assessment results

Assessed on 30 October 2026: `rayNeoPort` at `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19`, **including the two existing uncommitted URP settings changes**. In a subsequent user-authorized follow-up, optional screenshot generation was removed from `ArtisticElementsSceneTests.cs`. No runtime code, scenes, prefabs, materials, dependencies, or settings were changed. Nothing was staged, committed, merged, tagged, or pushed.

**The screenshot-related blocker is resolved; the latest full suite passes 196/196.** The Android APK builds, installs, and launches successfully. The user tested the app on the glasses and reports that everything is working as expected. A separate intermittent halo assertion seen during follow-up validation is recorded below.

| Assessment chunk | Result | Evidence |
| --- | --- | --- |
| Input preservation | 1,817 source files copied and hash-verified; original URP patch preserved and applicability checked | `baseline.json`, `initial-working-tree.patch`, `source-integrity.json` |
| Code inventory | All 96 first-party scripts reviewed; count independently verified against rg | `code-inventory.csv`, `scan-summary.json`, two code reviews |
| Cleanup candidates | Four individual no-reference candidates; connected legacy subsystems require separate decisions | `legacy-code-review.md`, `runtime-code-review.md` |
| Full Editor PlayMode suite | **196 passed, 0 failed, 0 skipped/inconclusive**, 196 total; earlier intermittent halo failure retained | `playmode-results.xml`, `screenshot-removal-validation.json` |
| Enabled scene and dependencies | **0 missing scripts, 0 detected broken references, 0 inspection warnings**; 248 components, 8 prefabs, 17 materials | `scene-inspection.json` |
| Imported script associations | All 13 explicitly checked expected MonoBehaviour types match their imported scripts | `mono-script-probe.json` |
| Android APK | **Succeeded: 0 errors, 2 warnings**, Unity 2022.3.36f1, ARM64/IL2CPP/OpenGLES3 | `build-summary.json`, `apk-artifact.json` |
| APK verification | Manifest, DEX, Unity and IL2CPP ARM64 libraries present; app ID, launcher, SDK levels verified | `apk-artifact.json`, `apk-signatures.json` |
| Device install/startup | In-place install succeeded; installed APK checksum matches; RayNeo activity cold-launched and OpenXR became focused | `device-results.json` |
| Visual startup | Device mirror capture shows the notation PNG in both stereo views | `Temp/PreMergeAssessment-20261030/device-screen.png` |
| Live Redis data | User reports app working; initial startup logged a saved-endpoint timeout. Real publisher payloads were not independently captured | `device-results.json`, captured app log |
| Gestures, all views, movement comfort | **Pass reported by user** in response to the acceptance checklist | `device-results.json` |

## Screenshot removal and follow-up validation

The user confirmed the diagnostic preview PNG was not being reviewed and authorized removing it. The capture call, unused capture helper, and unused `System.IO` import were removed. All 55 assertion lines remain unchanged. Tests no longer switch the camera to a temporary RenderTexture or write `artistic-panel-preview.png`; the existing historical image was not deleted or modified. The affected test passes in isolation, and the final full-suite run passes all 196 cases.

The first post-change full run passed the previously failing scene test but encountered `CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside`: its negative active radius equalled the neutral radius rather than being smaller. That unchanged halo test passed in isolation, and one unchanged full-suite rerun passed 196/196. This intermittent result is retained in `playmode-no-preview-full.xml`; no halo code/assertion was altered or suppressed. See `screenshot-removal-validation.json`.

The earlier 195/196 preview-rendering failure is preserved in `playmode-before-preview-removal.xml` and explained historically in `failure-analysis.md`. This follow-up changes only the Editor test; the other 1,816 captured input files and the test `.meta` remain unchanged (`screenshot-removal-integrity.json`). Runtime build/device results remain applicable.

## Later cleanup inventory

The four initial candidates are `FaceUserSmooth`, `RuntimeImageBoard`, `URPImageBoardApplier`, and `XRealControllerQuitter`. No other scanned C# callers or serialized GUID references were found. This is not proof of safe deletion: manual/external workflows still need review.

Recovery scenes, wrist-menu support, RedisManager, optional LiveKit, and legacy plasma/HUD paths form connected cleanup groups. Current runtime-created components, callbacks, Resources settings, fallback dependencies, manual tools, and ArtisticElements functionality have positive usage evidence and must not be treated as dead solely from scene-reference counts.

## Build and device artifact

APK: `Temp/PreMergeAssessment-20261030/android-file-trust/RayNeo-assessment.apk` (167,639,837 bytes).

SHA-256: `ea7c8fa11cef7df446d8642a66e2a8240c99d32b1d5a60c8bec380d8a207af19`.

The APK is installed on the connected X3 Pro as `com.TUS.RayNeoXRScoreViewer`. Signing certificates match the previous installation. No uninstall or data clear was performed. The previous APK is backed up at `Temp/PreMergeAssessment-20261030/installed-before.apk` and fingerprinted in `device-baseline.json`.

## Validation environment and limits

Use the fresh **canonical Windows path** in `canonical-path-project.json`. An earlier DOS8.3-path import produced false missing-script results; those artifacts are preserved separately and superseded by the healthy canonical import. No scene/script repairs were needed.

The local RayNeo SDK 1.1.2 is separately fingerprinted (444 files). Git long-path support was enabled only for validation processes. Gradle required a temporary PKCS12 copy of the existing Windows public root certificates because of local TLS interception/trust configuration; the actual Gradle preflight passed. Java options apply only to launched processes, with certificate and hostname validation enabled. No system trust stores or global settings were changed. See `gradle-tls-check.json` and `run_validation.ps1`.

Unity automatically migrated `PC_Renderer.asset` in the isolated project; build processors temporarily moved simulation resources and restored them. Only validation-only Editor helpers were added to that copy. These changes were not copied back; `snapshot-drift-final.json` records the final differences. Historical test results in the source project were not reused or overwritten.

The build warns that Active Input Handling `Both` is unsupported on Android, and that USD plugins are excluded from non-desktop builds. Neither setting/package was changed. Stale README/control instructions are recorded in `configuration-review.md`.

The user's acceptance statement is: “I tested the app on the glasses. Everything is working as expected.” A subsequent PID check found the app stopped, consistent with testing Quit; this was not classified as a crash. The user report supplies the physical-device acceptance; automated startup evidence alone would not establish it.

## Before merging

Review the recorded intermittent halo assertion and settle whether the preserved uncommitted URP changes belong in the merge. Ensure merge inputs include the tested Editor-test change. Broad obsolete-code removal remains a separate cleanup task. Merging also makes RayNeo/Unity 2022 the main baseline; local main was 43 commits behind at capture, and remote refs were not fetched.
