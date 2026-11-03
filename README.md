# XR Score Viewer

Unity XR proof-of-concept for viewing real-time audience data on RayNeo X3 Pro glasses. The app presents a score/image board, an abstract particle mesh, objective graphs, and an ArtistViz placeholder. Redis supplies live data; manual visualization drivers remain available for development. The original sine-wave demo source has been retired.

## Current baseline

- Primary branch: `main`; port development branch: `rayNeoPort`.
- Unity Editor: **2022.3.36f1**, with Android Build Support, SDK/NDK tools, and OpenJDK.
- Main build scene: `Assets/Scenes/ampPortable_xrGlassesDemo.unity`.
- Android application ID: `com.TUS.RayNeoXRScoreViewer`.
- Android minimum/target API: 30; ARM64; IL2CPP; OpenGL ES 3.
- XR runtime: OpenXR with RayNeo Support and RayNeo Controller Profile; DOF3 camera attitude.
- The assessed machine-local RayNeo ARDK package is version 1.1.2. Its existing `file:` dependency in `Packages/manifest.json` must resolve on the build machine.

The previous XREAL Air 2 Ultra / Beam Pro project, using Unity **6000.3.10f1**, is preserved at the annotated checkpoint tag **`xreal-unity6-pre-rayneo`**. Use that checkpoint's setup instructions when working on the XREAL version.

## Build and deploy

See the [RayNeo setup and deployment guide](Assets/Docs/README_v010-alpha9.md) for package resolution, OpenXR configuration, APK creation, USB debugging, and installation. Its RayNeo settings also apply to the merged main baseline.

1. Open this project in Unity **2022.3.36f1** and allow package resolution and compilation to finish.
2. In **File > Build Settings**, select Android and confirm that only the main application scene is enabled.
3. Keep **Build App Bundle** disabled to create an APK, then build to an ignored `Builds/` directory.
4. Connect and authorize the RayNeo X3 Pro through ADB. Install with `adb install -r path/to/XRScoreViewer.apk`, or use Unity's **Build And Run**.

At startup the app checks a saved Redis endpoint and DNS-SD discovery, then offers manual endpoint entry if required. The Redis host and glasses must be reachable on the network. Current sensor streams use `device:{id}:physio_metrics` and per-sensor `device:{serial}:engagement`.

Right-temple single tap cycles visualizations; double tap cycles sensors. Horizontal and vertical swipes control scaling and placement. Quit listens to the SDK's `OnDoubleFingerTap` event. Use the actual event mapping and current device behavior when consulting older controls/tutorial documents.

## Tests and assessment

PlayMode tests live in `Assets/Tests/PlayMode`. Run them with Unity Test Runner using the project Editor version. Optional diagnostic screenshot generation has been removed; all behavioral assertions remain.

The [30 October 2026 pre-merge assessment](Assessment/2026-10-30/RESULTS.md) records the latest **196/196** passing full-suite run, successful APK build and deployment, and the user's successful on-device test. It also records an intermittent halo-radius assertion from an earlier run and two Android build warnings. Raw local logs and APKs remain under ignored `Temp/` paths.

## Project structure

`Assets/AmpPortableDataViz` separates domain contracts in `Core`, sensor selection and data coordination in `Application`, concrete services in `Infra`, and Unity visualizers, bindings, input, and UI in `Presentation`.

Bootstrapper retains Inspector configuration, mode selection and lifecycle coordination. Its Presentation helpers own Redis streams (`RedisStreamRegistry`), visualization instances and bindings (`VisualizationPresenter`), and positioning/labels/HUD creation (`VisualizationLayout`). The [helper extraction report](Assessment/2026-11-03/bootstrapper-refactor-step5/README.md) records the three batches and validation.

Bootstrapper configuration uses private serialized fields with the existing flat field names and migration aliases. The [configuration visibility report](Assessment/2026-11-03/bootstrapper-refactor-step6/README.md) records the consumer audit and saved-value verification.

Obsolete-code cleanup is underway on `cleanup/obsolete-code`. Historical recovery/manual scenes and the optional LiveKit, plasma, legacy HUD/graph, and RedisManager application paths have been retired. The [optional-feature retirement report](Assessment/2026-10-30/cleanup-step5/README.md) records those changes. The original visualization session and spatial-anchor implementation have also been removed; see the [session retirement and Inspector migration report](Assessment/2026-11-03/bootstrapper-session-retirement/README.md). Current RayNeo views, ArtisticElements, audience/particle/graph manual tools, and shared graph contracts remain. Package-manifest and unrelated asset pruning are deferred.
