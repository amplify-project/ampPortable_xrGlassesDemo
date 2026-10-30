# Presentation legacy-code review

Assessment date: 2026-10-30. Branch snapshot: `rayNeoPort`, commit `60e2ff9`.

This review covers the 39 C# files directly in `Presentation/Visualization`, `Presentation/Interaction`, and `Presentation/Debug`. It records cleanup candidates without deleting or modifying scripts, tests, scenes, prefabs, packages, or project settings. No Unity session, tests, or build were started by this review.

## Method and limits

- Read component implementations, Bootstrapper creation/configuration paths, the visualization factory, current build-scene YAML, relevant prefab YAML, tests, and documentation.
- For each of the 39 scripts, read its `.meta` GUID and search `Assets` for GUID references in `.unity`, `.prefab`, `.asset`, `.controller`, `.overrideController`, and `.anim` files. Search C# files under `Assets` for the script/type name, excluding the defining file.
- For the four candidates with no matches, also search their names and GUIDs across non-meta files under `Assets`, `ProjectSettings`, and `Packages`. Only their own definitions/log strings matched.
- Check persistent UnityEvent method/type names and generic runtime `AddComponent` paths. A search of project runtime C# found no `Type.GetType`, string-based `AddComponent`/`GetComponent`, `SendMessage`, `BroadcastMessage`, `SerializeReference`, or Addressables references. `Resources.Load` is used for Redis runtime settings, not these candidate components.
- These are text-based findings, not a Unity dependency/serialization audit. Ignored files, external SDK code/assets, asset bundles outside the checkout, binary assets, Inspector workflows, and future/optional configurations can escape this review. No candidate is certified safe to delete.

## Four likely-obsolete candidates for a later cleanup chunk

All four have **zero other C# type-name references and zero matching serialized GUID references** in the searched checkout. Their `.meta` GUID is included to make the finding reproducible.

| Script | GUID | Evidence and remaining decision |
| --- | --- | --- |
| `Presentation/Visualization/FaceUserSmooth.cs` | `1e126d09f0b4ce243b941a0ab2396dc5` | Class at line 5; old camera-facing behavior in `Start`/`Update`, lines 10 and 17. No recorded scene/prefab or code consumer. Confirm this is not a component the team still adds manually in the Editor. |
| `Presentation/Visualization/RuntimeImageBoard.cs` | `13af96730b4e4734abf8c774b56e4e5e` | Class at line 6; alternate quad texture loader. The shipped scene uses the separate `RuntimePngLoader` path described below. Confirm no retained external scene requires this older API. |
| `Presentation/Visualization/URPImageBoardApplier.cs` | `b05a98cf044bed64bb943fa30397ea57` | Class at line 6; alternate URP texture/PNG loader. No recorded consumer. Confirm its raw-byte API is no longer a planned workflow. |
| `Presentation/Interaction/XRealControllerQuitter.cs` | `6ab818b15266638458808731a956b689` | Class at line 8; Input System action/hold-to-quit component. RayNeo uses `QuitApp`, but the older component is generic enough to support another controller. Decide whether the post-merge app retains that capability. |

`FaceUserSmooth.cs:2` contains an unguarded `using UnityEditor;` inside the runtime `Amp` assembly. `AmpPortableDataViz.asmdef` has unrestricted platforms. This is a player-compilation concern to check during the Android baseline, not a confirmed build failure from this static review. Do not silently remove/fix it as part of inventory work.

## Recovery-scene-only attachments: retain until the scene policy is decided

These scripts are not isolated unreferenced files: their GUIDs occur in retained `_Recovery` scenes. Counts below are distinct files with matches, not component counts. The current enabled build scene is `Assets/Scenes/ampPortable_xrGlassesDemo.unity` (`ProjectSettings/EditorBuildSettings.asset:10`). `_Recovery` scenes are not enabled build scenes in that file.

| Script/group | Serialized references | Other evidence / cleanup constraint |
| --- | --- | --- |
| `VisualizationCycler` | 15 `_Recovery` scenes | `Assets/_Recovery/0 (21).unity:6056` attaches it; lines 6981 and 6983 persist a UnityEvent to `ApplyState`. The method exists at `Interaction/VisualizationCycler.cs:40`. A C# caller search alone would miss this usage. |
| `ToggleButtonColor` | 18 `_Recovery` scenes | `Assets/_Recovery/0.unity:281` and `0 (21).unity:395` attach it. The component registers its own `Button.onClick` listener in `Awake`, so external callers are unnecessary. |
| `WristMenuFollower` | `Assets/_Recovery/0.unity` | Attachment at line 2223; behavior consumes the assigned `IHandPoseProvider`. Review with the retained hand/wrist-menu workflow rather than deleting only the follower. |
| `HudSquareBuilder` | 16 `_Recovery` scenes | Example attachment: `Assets/_Recovery/0 (21).unity:167`. It builds configured child lines from `Awake`; no external caller is required. |
| `EmotionTelemetryLogger` | 17 `_Recovery` scenes | Example attachment: `Assets/_Recovery/0 (21).unity:4729`. Event subscriptions start from `OnEnable`. It may remain useful as a debugging tool even though it is absent from the current build scene. |
| `ColourChangingHudSide` + `EngagementHudBinding` | Each appears in 16 `_Recovery` scenes | Example attachments: `Assets/_Recovery/0 (21).unity:1811` and `:1827`. `EngagementHudBinding.cs:8` requires `ColourChangingHudSide`; Bootstrapper still searches/configures HUD bindings at lines 2187 and 2204. Removing this group also requires a deliberate decision about those supported paths. |

There are already stale persistent calls in the historical scenes: for example `Assets/_Recovery/0 (9).unity:6814` names `CycleVisualization`, but that method is commented out at `Interaction/VisualizationCycler.cs:34`. This is a retained-scene defect to record; it is not evidence that the component itself has no users, nor a demonstrated failure in the enabled build scene.

## Manual/development tooling: explicit support, not automatically obsolete

| Script | Evidence | Assessment |
| --- | --- | --- |
| `AudienceDataManualDriver` | GUID `4fca7e35d651470b96c88ea31139fb69`; attached to `ParticleMeshViz.prefab:5059` and `AudienceGraphPrefab.prefab:162`. `AudienceSignalVisualizerBinding.cs:42` has a serialized manual-source field; lines 154 and 200 configure/resolve it. | Retained simulation workflow. The active particle prefab inherits from the base prefab, so this is also part of the shipped prefab dependency chain even when disabled for live operation. |
| `ParticleMeshManualDriver` | GUID `81fe5631a3ac4dfdb89dacdca10129cd`; direct attachment found only in `Assets/_Recovery/0 (16).unity`. `ParticleMeshVisualizer.cs:24`, `:295`, and `:539` retain a field, event subscription, and component lookup. | Older/manual workflow still has runtime type dependencies. Removing only its file would break compilation. Review the workflow and callers together later. |
| `GraphManualDriver` | GUID `ccaa11f435aa0b04fa528d31dc19737e`; attached, disabled, in `GraphPrefab.prefab:175` and `:177`. Bootstrapper recognizes it at lines 782 and 874. | Retained legacy graph preview/manual workflow, not used as proof of current device behavior. |
| `EmotionPlasmaManualDriver` | GUID `a9b63c4f5f500b24b805fe7711d68027`; attached, disabled, in `EmotionPlasma.prefab:180` and `:182`. `Assets/Scenes/plasmaManual.unity:374` instantiates that prefab. | Retained manual scene. Decide whether to preserve that scene before removing its component. |
| `EmotionRayPlasmaManualDriver` | GUID `27b57e1e63f87ce498cd627ceab41433`; attached, enabled, in `EmotionRaymarchPlasma.prefab:172` and `:174`. `Assets/Scenes/plasmaManual.unity:588` instantiates the prefab. | Retained manual scene. Its `RequireComponent` also needs the matching visualizer. |

Bootstrapper expressly supports manual mode (`Bootstrapper.cs:483`, `:729`, `:761`, `:808`) and selects manual drivers while disabling competing live bindings. Live creation disables audience/particle/graph manual drivers (`Bootstrapper.cs:1948`; `Infra/UnityVisualizationFactory.cs:33`). The saved build scene sets `UseManualVisualizationDrivers: 0` at line 1914. This combination means manual tools can be shipped dependencies while inactive in the default app mode.

## Runtime support and alternate visualization paths to keep during readiness work

Eight older-looking files are still referenced by explicit runtime support or retained prefabs: `AnchoredVisualization`, `SimpleVisualizer`, `GraphBinding`, `EmotionPlasmaBinding`, `EmotionPlasmaVisualizer`, `EmotionRayPlasmaBinding`, `EmotionRayPlasmaVisualizer`, and `FrameSmoother`.

- `Bootstrapper.cs:414` and `:419` dynamically add `SimpleVisualizer`/`AnchoredVisualization` for a missing-prefab fallback. `UnityVisualizationFactory.cs:73` and `:74` resolve these components for factory-created visuals.
- `GraphBinding` is attached to `GraphPrefab.prefab:207` and is still supported by `Bootstrapper.cs:1385`, `:2316`, and the legacy graph creation path. The prefab GUID appears in ten `_Recovery` scenes, although it is not the saved build scene's selected graph prefab.
- `EmotionPlasmaBinding`/`EmotionRayPlasmaBinding` remain supported at `Bootstrapper.cs:914`, `:915`, `:957`, `:1878`, `:1884`, `:1894`, and `:1900`. Their associated visualizers occur on retained prefabs; `plasmaManual.unity` instantiates the plasma and raymarch prefabs.
- Both emotion bindings construct `FrameSmoother<T>` (`EmotionPlasmaBinding.cs:100`, `EmotionRayPlasmaBinding.cs:320`). It is a normal C# helper, so the absence of scene GUID references is expected.
- `EmoPlasmaRay.prefab` and `PlasmaVisual.prefab` had no incoming GUID references in the searched serialized files, but their component classes still have the other consumers above. An unreferenced prefab does not establish that every script on it is obsolete.

Whether to retire these alternate/fallback paths is a product/support decision after the baseline, not a pre-merge deletion.

## Fifteen active/runtime-linked files: exclusion from the cleanup candidate list

`ActiveSensorHudBinding`, `ArtistVizVisualizer`, `AudienceSignalBinding`, `AudienceSignalVisualizerBinding`, `BillboardLabel`, `GraphVisualizer`, `ParticleMeshCornerIndices`, `ParticleMeshVisualizer`, `RuntimePngLoader`, `SensorStreamCoordinator`, `QuitApp`, `RayNeoTempleSensorStreamCycler`, `RayNeoTempleVisualizationScaler`, `RayNeoTempleVisualizationToggle`, and `RayNeoTempleVisualizationVerticalMover` all have concrete current references.

Key evidence guards against common false positives:

- Saved scene `ampPortable_xrGlassesDemo.unity:1908` selects `ParticleMeshVizRoot_RayNeo`; its `m_SourcePrefab` at `ParticleMeshVizRoot_RayNeo.prefab:171` resolves to `ParticleMeshVizRoot`, which nests `ParticleMeshViz` at `ParticleMeshVizRoot.prefab:320`. Base prefab script attachments therefore matter even if the script GUID is absent from the scene and variant.
- The same scene selects `AudienceGraphPrefab` at line 1955 and `ArtistViz` at line 1935; these keep `GraphVisualizer`, artist rendering, and audience bindings in use. Selected-sensor mode is saved at scene line 1948 and implemented in `Bootstrapper.cs:545` onward.
- `SensorStreamCoordinator`, `RayNeoTempleSensorStreamCycler`, and `ActiveSensorHudBinding` have no serialized script-GUID references but are dynamically added in `Bootstrapper.cs:1192`, `:1204`, and `:1246`. Tests also exercise these types. They are not unreferenced code.
- `BillboardLabel` is dynamically added in `Bootstrapper.cs:2652`, `:2688`, and `:2959`; `GraphVisualizer` also uses it. `ParticleMeshCornerIndices` is a code helper used by the particle visualizer and its tests.
- The saved scene instantiates `ImageBoard.prefab` at line 1014 and overrides its `pathOrFileName` at lines 967-968. `ImageBoard.prefab:239` attaches `RuntimePngLoader`; line 252 enables startup loading. `RuntimePngLoader.cs:44` starts loading directly, without an external C# caller. This loader is active even though its external type-name reference count is zero.
- Current RayNeo controls are serialized in the saved scene and subscribe to SDK events directly: `RayNeoTempleVisualizationToggle.cs:117` (single tap), `RayNeoTempleSensorStreamCycler.cs:68` (double tap), and `QuitApp.cs:17` (double-finger tap).

## Documentation mismatches recorded without editing existing documentation

- `README.md:3`, `:9`, `:13`, `:14`, and `:18` still describe XREAL/Beam Pro, Unity `6000.3.10f1`, and the old SDK path. Current `ProjectSettings/ProjectVersion.txt:1` records `2022.3.36f1`. Use the actual RayNeo project settings/package configuration for this baseline.
- `RayNeo_AbstractViz_Test_Tutorial.md:34` and `:46` say double tap quits. Actual SDK subscriptions currently use double tap for sensor cycling and double-finger tap for quit (code references above). Verify the physical SDK gestures on device before updating performer instructions.
- The tutorial at lines 14-15 describes toggling between two visualizations. The saved scene has four roots beginning at `ampPortable_xrGlassesDemo.unity:1187`; `ArtistVizAssetWiringTests.cs:42` also verifies cycling four roots. Documentation should describe the shipped configuration after device validation.
- `XRScoreViewer_RayNeo_Architecture.md` is a proposed architecture (its own text uses “could”, “should”, and “suggested”), including position-based sensor zones. It should not be used to assert that zone calibration/hysteresis is implemented. The current control path explicitly supports manual sensor cycling.

## Suggested later cleanup chunks

1. Validate the port without deleting runtime/asset files; resolve actual compile or runtime blockers separately if the baseline finds them.
2. Review the four no-reference candidates with the maintainer's intended workflows. If retired, remove each script with its `.meta`, check the Unity dependency/Inspector view, and rerun the relevant baseline.
3. Decide whether `_Recovery` scenes and `plasmaManual.unity` are retained development artifacts. Assess their scripts, UnityEvents, prefabs, and hand/HUD dependencies as a connected group.
4. Assess alternate Bootstrapper modes/fallbacks separately. Removing one class while keeping typed callers or prefab attachments is not a complete change.
5. Keep current visualizers, bindings, dynamic sensor/HUD components, and the active PNG-loading path out of speculative cleanup.

Only this report is authored by this review. No deletion-safe claim is made, and hardware behavior remains to be measured on the RayNeo device.
