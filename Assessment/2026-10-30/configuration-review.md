# RayNeo configuration and validation review

Assessment date: 2026-10-30. Source snapshot: `rayNeoPort`, commit `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19`, plus the two pre-existing modified URP settings assets recorded by the parent assessment. This report is a static inspection; execution results belong in the main assessment report.

## Checked-in build configuration

| Check | Observation | Evidence |
| --- | --- | --- |
| Editor | Unity `2022.3.36f1` | `ProjectSettings/ProjectVersion.txt:1` |
| Build scene | Only `Assets/Scenes/ampPortable_xrGlassesDemo.unity` is enabled | `ProjectSettings/EditorBuildSettings.asset:12` |
| Other scenes | SampleScene, old XRI 3.2.1 HandsDemoScene, and rayNeo_abstractViz_test are disabled; ArtisticElementsTest is not listed | `ProjectSettings/EditorBuildSettings.asset:8` |
| App identity | Product `XR Score Viewer`; Android ID `com.TUS.RayNeoXRScoreViewer` | `ProjectSettings/ProjectSettings.asset:16`, `:165` |
| Android APIs | Minimum and target both 30 | `ProjectSettings/ProjectSettings.asset:176` |
| CPU/backend | ARM64 (`AndroidTargetArchitectures: 2`), IL2CPP (`Android: 1`) | `ProjectSettings/ProjectSettings.asset:266`, `:818` |
| Graphics | Android uses explicit GLES3 (`0b000000`), automatic API selection disabled | `ProjectSettings/ProjectSettings.asset:525` |
| XR loader | Android settings initialize XR on start and reference OpenXRLoader GUID `8b142a20b3d722a4589cdf863112e5cb` | `Assets/XR/XRGeneralSettingsPerBuildTarget.asset:34`, `:80`; `Assets/XR/Loaders/OpenXRLoader.asset.meta:2` |
| Android OpenXR features | RayNeo Support and RayNeo Controller Profile are enabled and present in the Android features list | `Assets/XR/Settings/OpenXRPackageSettings.asset:53`, `:55`, `:658`, `:805`, `:807` |
| Camera attitude | `CameraAttitudeType: 4097` = `DOF3` | `Assets/XR/Settings/OpenXRPackageSettings.asset:65`; local SDK `OXR/Runtime/Scripts/OpenXR/RayNeoSupportFeature.cs:195` defines `DOF3 = 0x1001` |
| Manifest | RayNeo launcher `com.rayneo.openxradapter.UnityOpenXrActivity`, RayNeo app metadata, internet/network/Wi-Fi/multicast-state permissions present | `Assets/Plugins/Android/AndroidManifest.xml:6`, `:12`, `:21` |
| SDK portability | RayNeo package uses an absolute local `file:` path; builds on other machines require the matching package to be available | `Packages/manifest.json:22` |

Important interpretation: OpenXR feature objects contain both `m_Enabled` and `m_enabled`. Actual feature enablement was checked using the latter, case-sensitive field. The two Android features enabled in this file are the RayNeo support and controller features. Standalone features are a separate configuration and are not evidence of device behavior.

The old, disabled HandsDemoScene entry references XRI 3.2.1 while the installed sample is 2.5.4. It is a configuration cleanup candidate, not evidence that the enabled application build fails. Do not enable this scene to establish the shipped-app baseline.

## Actual application scene wiring

- The active `InteractionManager` has a visualization toggle, vertical mover, and gaze scaler. All three components are enabled. The toggle has four nonzero root references and starts at index zero: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:1154`, `:1187`, `:1192`, `:1206`, `:1223`.
- Movement and scale components reference the same toggle. The gaze scaler has an assigned camera. Their serialized clamps are Y -2 to +2, scale 0.35 to 2.5: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:1209`, `:1228`.
- The enabled `QuitApp` GameObject includes the `QuitApp` component: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:921`, `:936`.
- The active Bootstrapper is configured for the Redis signal source, manual drivers disabled, per-sensor engagement enabled, and a single selected sensor stream: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:1913`, `:1914`, `:1937`, `:1948`. The HUD camera and ArtistViz/Graph prefabs are assigned. Static assignment checks do not prove successful Unity object resolution or live connectivity.
- Sensor cycling is added dynamically by `Bootstrapper.EnsureSensorStreamPresentation`; absence of its script GUID in the scene does not make it unused: `Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs:1183`, `:1204`.

## Automated baseline limits and side effects

- Run the full Editor PlayMode suite, with no test-name filter, retaining fresh XML and logs and recording discovered/passed/failed/skipped/inconclusive counts. A process exit code alone does not prove test execution.
- The test assembly includes Editor and Android, requires `UNITY_INCLUDE_TESTS`, and references both UnityEngine and UnityEditor test runners: `Assets/Tests/PlayMode/Tests.asmdef:4`, `:13`, `:26`.
- No explicit `[Ignore]`, `[Explicit]`, `Assert.Ignore`, or `Assert.Inconclusive` uses were found in current PlayMode C# sources. Conditional compilation still changes coverage.
- `ArtisticElementsSceneTests.cs:1` is entirely `#if UNITY_EDITOR`; the prefab asset test in `ArtistVizAssetWiringTests.cs:26` is also Editor-only. Android test coverage cannot replace this Editor baseline.
- `ArtisticElementsSceneTests.cs:273` creates `TestResults` and overwrites `artistic-panel-preview.png`. Run against the isolated validation copy. Keep graphics available: this test captures a RenderTexture screenshot (`:254`) and a `-nographics` run would not be equivalent evidence.
- `LiveKitTelemetryServiceTests.cs:11` supplies a dummy connection factory and exercises JSON processing. `RedisSubscriberEngagementTests.cs:16` exercises parsing. `SensorEngagementIntegrationTests.cs:16` uses fake sources. These tests do not establish real network or headset connectivity.
- Existing `TestResults/*.xml` files are historical artifacts. Do not overwrite them or count their pass totals as fresh results.

## Documentation inconsistencies

| Document | Conflict | Assessment handling |
| --- | --- | --- |
| `README.md:3`, `:9`, `:14`, `:18`, `:25` | Describes XREAL, Unity 6000, an XREAL package archive, and Beam Pro deployment | Treat as obsolete setup guidance for this branch; do not follow it to build RayNeo |
| `RayNeo_AbstractViz_Test_Tutorial.md:13` | Describes switching between two views; main scene assigns four roots | Check all four configured roots and wraparound |
| `RayNeo_AbstractViz_Test_Tutorial.md:32`, `:46` | Says double-tap quits | Actual sensor cycler listens to `OnDoubleTap` (`RayNeoTempleSensorStreamCycler.cs:68`); quit listens to `OnDoubleFingerTap` (`QuitApp.cs:17`) |
| `QuitApp.cs:16` comment | Calls the event double-tap, although API is `OnDoubleFingerTap` | Use actual event binding and device confirmation; do not infer physical gesture semantics only from this comment |

`Assets/Docs/README_v010-alpha9.md` matches the main RayNeo project settings and records double-tap sensor selection (`:123`). Its suggestion to update Glass OS should not be followed during baseline collection; record the existing OS/SDK version and avoid introducing an unrelated platform change.

## Confirmed device fixtures

- The main scene's ImageBoard prefab override selects `Corridinho do Algarve - Soprano Sax_singleImgCropped.png`: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:967`. `Assets/Prefabs/ImageBoard.prefab:248` sets `pathKind: 2` (StreamingAssets) and `:252` enables loading on Start. The exact-named PNG is present under `Assets/StreamingAssets`. Confirm that this image is packaged in the APK and actually displays on the glasses; `AssetDatabase.GetDependencies` cannot establish successful loading of string-named files. `RuntimePngLoader.cs:131` handles the Android StreamingAssets URI path.
- The current physiological stream is `device:{id}:physio_metrics`. Use the same exact device identifier consistently across the channel, engagement payload, selected sensor, and HUD. The parser expects normalized z-score properties `eda_z`, `temperature_roc_z`, `scr_frequency_z`, and `hr_z`: `Assets/AmpPortableDataViz/Presentation/Sources/RedisPhysioMetricsPump.cs:201`. `ibi_z` is a legacy optional field. At least one recognized z-score property is required; absent recognized properties default to zero, so a visual validation fixture should send all four current properties. Finite values are clamped to [-3, 3]. A payload containing only older standard-deviation property names does not exercise this path.
- A preparation example for a dedicated test sensor's physio channel is `{"eda_z":0.5,"temperature_roc_z":-0.25,"scr_frequency_z":0.75,"hr_z":1.0}`. These are normalized input values, not physical-unit readings. Vary one property at a time when confirming the mapping and use distinct values for multiple test sensors to detect cross-sensor mixing.
- The scene configures per-sensor engagement channels as `device:{serial}:engagement`, with JSON properties `device`, `engagement`, `confirmed`, and `timestamp`: `Assets/Scenes/ampPortable_xrGlassesDemo.unity:1938`. For the matching dedicated test sensor, an example body is `{"device":"assessment-a","engagement":0.8,"confirmed":true}` on `device:assessment-a:engagement`. Use a current Unix timestamp in **seconds** if including `timestamp`; the field is optional, but when present it is decoded as seconds, not milliseconds (`SensorEngagementJsonDecoder.cs:149`, `:265`). A provided device identifier must match the expected channel sensor (case-insensitive comparison); an absent identifier can fall back to the expected sensor (`:174`). Use explicit matching identities to make validation evidence unambiguous.
- Verify both `confirmed:true` and `confirmed:false` cases. The decoder accepts a Boolean confirmation and also numeric/string 0 or 1; the positive fixture should use the JSON Boolean `true`. Confirmed-state routing and timeout behavior need a running app and real or controlled test publisher; parser tests alone do not establish them. The current scene's engagement timeout is 10 seconds (`Assets/Scenes/ampPortable_xrGlassesDemo.unity:1940`).
- The installed local RayNeo package identifies itself as `com.unity.xr.rayneo.openxr` version `1.1.2` in its `package.json`. The parent assessment records the external SDK fingerprint separately from the project-owned input manifest.

These fixtures describe expected inputs and files; they are not evidence that messages have been published, the PNG loaded, or the assessed APK passed a device check.

## Device acceptance checklist

Every item below starts **unverified**. ADB subsequently detected an X3 Pro during the parent assessment; this confirms a device connection only. Record the APK checksum, commit/snapshot, Glass OS version, SDK version, network setup, observer, and outcome when exercised. Real sensor data and wearer observations are separate from parser/unit tests.

| Check | Expected observation |
| --- | --- |
| Install and cold launch | Exact assessed APK installs; XR launch succeeds rather than opening as a flat Android view; no startup exception |
| Redis startup | Saved endpoint, DNS-SD discovery, and manual endpoint entry behave as configured; unavailable service presents usable feedback |
| Live streams | Physio data and confirmed per-sensor engagement reach the selected sensor; HUD identifier agrees with the data source |
| Sensor cycle | Double-tap advances one sensor at a time and wraps; no duplicated gesture effect or cross-sensor data mixing |
| Visualization cycle | Single tap cycles all four configured roots and wraps; exactly one root visible at each step |
| Visual output | Score/image, particle mesh, graphs, and ArtistViz content render; no missing/pink materials or empty configured content |
| Scale | Gaze-aware horizontal swipe changes the intended active visual within configured bounds; one gesture has one intended effect |
| Vertical position | Vertical swipes move the active visual within configured bounds without affecting other roots |
| Stability and comfort | Wearer verifies visibility, comfortable placement, and behavior during normal and fast head movement |
| Stream loss/recovery | Interrupt and restore the test publisher/network in a controlled test environment; UI and data freshness behavior remain understandable; recovery has no crash or duplicate subscriptions |
| Quit/relaunch | Confirm the physical gesture that emits `OnDoubleFingerTap`; app exits and relaunches without stale/duplicate handlers |
| Logs | Capture Unity/OpenXR/Android errors for the exact session and distinguish expected unavailable-service messages from faults |

Uninstalling an existing device build would erase saved settings. Normal assessment should try an in-place install first; a signing mismatch should be reported rather than silently uninstalling the app.

No runtime source, scene, prefab, material, package, or project setting was changed to produce this review. This review does not certify the merge; the parent report must incorporate fresh test/build execution and explicitly retain any device or network checks that remain unverified.
