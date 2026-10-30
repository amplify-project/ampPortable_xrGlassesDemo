# Runtime code review for the RayNeo pre-merge assessment

Assessed snapshot: `rayNeoPort` at `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19`, plus the two pre-existing modified URP global-settings assets recorded by the main assessment. This review is static evidence, not a deletion authorization or proof of runtime correctness. No runtime code, Unity assets, package files, or tests were edited for this review.

## Scope and method

Reviewed 55 first-party C# files: Core (10), Application (7), Infra (10), Presentation/Sources (9), Mapping (12), ArtisticElements (5), Anchors (1), and Bootstrapper (1). Checked type consumers, runtime construction, main-scene settings, GUID references in `Assets` scenes/prefabs/assets, resource loading, Inspector callbacks, platform guards, native callbacks, and the assembly's GUID dependencies. References from `_Recovery` scenes were distinguished from the enabled app scene.

The search covers the available checked-in C#/text assets and current local SDK/package metadata. Binary assets, independently distributed bundles, external user scenes, and runtime behavior cannot be ruled out by these searches. No deletion should be based only on a zero C# or GUID reference count. Several contract files declare types whose names differ from their file names; those declared types were checked directly.

## Active roots and dependency chains

- `ProjectSettings/EditorBuildSettings.asset` enables only `Assets/Scenes/ampPortable_xrGlassesDemo.unity`.
- The app scene selects Redis (`ActiveSignalSource: 1`, line 1913), disables manual-driver mode (1914), requires endpoint confirmation (1919), and enables selected-sensor mode (1948). Particle, graph, and ArtistViz prefab assignments are at 1908, 1935, and 1955.
- `RedisEndpointPrompt.OnEndpointReady` calls `Bootstrapper.BeginAfterEndpoint` through a serialized UnityEvent (`ampPortable_xrGlassesDemo.unity:459-461`). There need not be a direct C# caller of that public method.
- The endpoint path is `RedisEndpointPrompt` -> `RedisEndpointStartupResolver` / `AndroidRedisEndpointDiscovery` / `RedisDeviceDiscovery` -> `RedisRuntimeSettings` -> bootstrap initialization. `RedisRuntimeSettings.Instance` loads `Resources.Load<RedisRuntimeSettings>("RedisRuntimeSettings")` at line 55; `Assets/Resources/RedisRuntimeSettings.asset` is a real indirect root.
- `Bootstrapper.EnsureSelectedSensorStreamMode` creates per-device `AudienceSignalBinding` sources (`Bootstrapper.cs:1029-1033`). `EnsureSensorStreamPresentation` finds or adds `SensorStreamCoordinator` and `RayNeoTempleSensorStreamCycler` (1189-1204). `SensorStreamCoordinator.EnsureState` constructs `SensorStreamSelectionController` and `SelectedSensorDataSource<AudienceSignalSample>` (`SensorStreamCoordinator.cs:93-94`). Selected frames feed persistent `AudienceSignalVisualizerBinding` targets.
- `Bootstrapper.EnsureSensorEngagementBackend` creates `RedisSensorEngagementPump` and `SensorEngagementCoordinator` and constructs `SensorEngagementJsonDecoder` (`Bootstrapper.cs:1630-1637`). The coordinator constructs `SensorEngagementRouter`; its sources implement the shared data interfaces.
- Shared data pumps are runtime components: `RedisDataPump` is added at `Bootstrapper.cs:1762`, and `RedisPhysioMetricsPump` at 1802. They depend on `RedisSubscriber`, `UnityClock`, and the domain contracts. No scene/prefab GUID reference is required for these classes to be active.
- Current visual mapping uses `AudienceSignalToParticleMeshMapper`, `AudienceSignalToArtistVizMapper`, `GraphSeriesToGraphParamsMapper`, `DynamicGraphYRangeTracker`, `GraphTemporalSmoother`, and `HeartRateVisualResponseMapper`, with `AudienceSignalContracts`, `ParticleMeshContracts`, `ArtistVizContracts`, and `VisualizationContracts`.

## Scope inventory

Paths below are relative to `Assets/AmpPortableDataViz`.

| Files or subsystem | Classification | Evidence and next action |
| --- | --- | --- |
| `Core/Interfaces.cs`, `AudienceSignalContracts.cs`, `ParticleMeshContracts.cs`, `ArtistVizContracts.cs`, `VisualizationContracts.cs`, `SensorEngagementContracts.cs`, `RedisEndpointDiscoveryContracts.cs` | Active/shared runtime | Declared contracts are consumed by current pumps, routers, mappers, and visualizers. Keep. `VisualizationContracts` also contains fallback `SimpleVisualParams`; removing the file would remove active graph contracts. |
| `Core/DomainModels.cs`, `Core/Ports.cs` | Retained session/fallback dependency | `VisualizationSessionController` and `UnityVisualizationFactory` use `VisualizationSpawnRequest`, `PoseSample`, and the factory/anchor ports. Keep while those paths exist. |
| `Core/AffectContracts.cs` | Legacy/optional subsystem candidate, medium confidence | `ArousalValenceSample`, `EmotionPlasmaParams`, and `RaymarchPlasmaParams` are consumed by retained plasma bindings, visualizers, manual drivers, and their mappers. Requires a coordinated plasma subsystem decision; cannot delete alone. |
| `Application/RedisEndpointStartupResolver.cs`, `SensorStreamSelectionController.cs`, `SelectedSensorDataSource.cs`, `SensorEngagementRouter.cs` | Active runtime | Endpoint prompt, selected-sensor coordinator, and engagement coordinator construct/use them; existing tests exercise these paths. Keep. |
| `Application/VisualizationSessionController.cs`, `VisualizationSessionOptions.cs`, `VisualizationMessages.cs` | Retained session/fallback dependency | Bootstrapper constructs the controller; controller uses options and `SpawnVisualizationMsg`/`ParamDeltaMsg`. Configured audience paths can bypass session initialization, but compilation and fallback still depend on these files. Keep pending a separate session design change. |
| `Infra/RedisSubscriber.cs`, `RedisDeviceDiscovery.cs`, `AndroidRedisEndpointDiscovery.cs`, `SensorEngagementJsonDecoder.cs`, `UnityClock.cs` | Active runtime | Referenced by the active endpoint, discovery, physio, engagement, and timing paths. Keep. RedisSubscriber also contains legacy compatibility parsing; retain those portions until publishers and callers are reviewed. |
| `Infra/MockAnchorService.cs`, `LocalLoopbackNetworkSync.cs`, `UnityVisualizationFactory.cs` | Retained runtime construction | Bootstrapper `Awake` constructs all three unconditionally at lines 242-244. Their names do not make them unused. `MockAnchorService` is also used by interface tests. Keep. |
| `Infra/LiveKitTelemetryService.cs` | Optional/legacy subsystem candidate, medium confidence | Constructed by retained `LiveKitTelemetryReceiver`; JSON parsing tests use it. Amp assembly still references the LiveKit runtime package. No live connection is implied by those parser tests. Requires an explicit decision to retire LiveKit, changes to bootstrap/tests, and separately authorized dependency work. |
| `Infra/IHandPoseProvider.cs` | Likely obsolete as part of wrist-menu subsystem, medium-high confidence | Only first-party consumer found is `Presentation/Visualization/WristMenuFollower.cs`. That consumer uses a serialized `MonoBehaviour` and casts it to the interface. No first-party provider implementation remains in the reviewed project. Coordinate any later removal with the wrist follower, recovery assets, and external provider check; do not delete this interface alone. |
| `Presentation/Sources/RedisEndpointPrompt.cs`, `RedisRuntimeSettings.cs`, `RedisPhysioMetricsPump.cs`, `RedisSensorEngagementPump.cs`, `SensorEngagementCoordinator.cs`, `RedisDataPump.cs` | Active runtime | Main-scene prompt, Resources asset, and bootstrap-created pumps/coordinator as described above. `RedisDataPump` also supports retained legacy HUD/plasma/graph modes. Keep. |
| `Presentation/Sources/SineWaveSource.cs` | Retained development/fallback support | `Bootstrapper.EnsureSources` creates this component if the host has no source (lines 386-387), then enables/disables it according to the selected source (line 500). The shipped scene uses Redis, but `Awake` construction still reaches the sine source. Keep or remove only with corresponding bootstrap cleanup. |
| `Presentation/Sources/RedisManager.cs` | Likely obsolete scene-level compatibility wrapper, medium confidence | GUID references found only in 17 `_Recovery` scenes, not the enabled app scene or current prefabs. Bootstrapper still has a typed field, searches for it (403), configures it (529-530), and manages its enabled state (531). This is a candidate for a later coordinated wrapper/bootstrap/recovery cleanup, not a standalone deletion. |
| `Presentation/Sources/LiveKitTelemetryReceiver.cs` | Optional/legacy subsystem candidate, medium confidence | GUID reference found in `Assets/_Recovery/0.unity`; main scene has `LiveKitSource: {fileID: 0}` and selects Redis. Bootstrapper still exposes/finds/configures the receiver and has a LiveKit source branch. Receiver tests remain. Retire as a subsystem only if LiveKit support is intentionally discontinued. |
| `Presentation/Mapping/AudienceSignalToParticleMeshMapper.cs`, `AudienceSignalToArtistVizMapper.cs`, `GraphSeriesToGraphParamsMapper.cs`, `DynamicGraphYRangeTracker.cs`, `GraphTemporalSmoother.cs`, `HeartRateVisualResponseMapper.cs` | Active runtime | Used by current audience bindings, particle visualizer, and graph handling; relevant mapper/range/smoothing tests remain. Keep. |
| `Presentation/Mapping/FloatToSimpleParams.cs` | Retained session/fallback dependency | Constructed unconditionally by Bootstrapper at line 246 and used by session source binding. Keep while bootstrap/session paths remain. |
| `Presentation/Mapping/EngagementToHudSideMapper.cs` | Legacy HUD subsystem candidate, medium confidence | Consumed by `EngagementHudBinding`, which Bootstrapper still configures. Needs a coordinated HUD/scene support decision; no isolated deletion. |
| `Presentation/Mapping/ArousalValenceToEmotionPlasmaMapper.cs`, `ArousalValenceToRaymarchPlasmaMapper.cs`, `PlasmaAffectMapper.cs`, `RaymarchPlasmaParamsMapper.cs` | Legacy/optional plasma subsystem candidate, medium confidence | Still consumed by retained plasma bindings and manual drivers. Coordinate with `Core/AffectContracts.cs`, plasma visualizers/prefabs/materials and bootstrap paths; no isolated deletion. |
| `Presentation/ArtisticElements/ArtisticElementController.cs`, `ArtisticElementsControlPanel.cs`, `ArtisticElementsPanelLayout.cs`, `ArtisticElementsSelectionController.cs`, `ArtisticElementsTestSetup.cs` | Retained test/development feature | `ArtisticElementsTestSetup` is serialized in `Assets/Scenes/ArtisticElementsTest.unity`; it adds/configures controllers, selection and panel at runtime (`ArtisticElementsTestSetup.cs:47-63`). The scene is not enabled for the shipped app, but dedicated tests load it using EditorSceneManager. Recent feature and tests provide positive retention evidence. Keep. |
| `Presentation/Anchors/AnchorRegistry.cs` | Retained runtime construction | `Bootstrapper.EnsureAnchorRegistry` finds or adds it (370-376); passed into the session controller. No serialized GUID references are needed. Keep. |
| `Presentation/Bootstrap/Bootstrapper.cs` | Active composition root with compatibility paths | Directly serialized by the main scene and asset-wiring tests. Contains both current selected-sensor orchestration and optional legacy source/visual/session paths. A later cleanup may narrow its responsibilities within the same layer; deleting consumers first will break compilation. |

## Non-C# reference checks that affect deletion confidence

- Android discovery is a Java/C# bridge, not unused managed helpers. `Assets/Plugins/Android/RedisNsdDiscovery.java:170` calls `UnityPlayer.UnitySendMessage(..., "OnRedisServiceResolved", ...)`; line 223 similarly calls `OnRedisDiscoveryError`. The internal `[Preserve]` callback receiver is created by `AndroidRedisEndpointDiscovery.StartNativeDiscovery` at line 154. Callback methods are at lines 222 and 228. Editor-only analysis cannot prove these callbacks work on-device.
- The Resources settings asset loads by string name and may be overwritten in memory by PlayerPrefs at runtime. It must not be removed because the main scene has no direct object assignment to it.
- The app scene calls `BeginAfterEndpoint` through a UnityEvent. Method-level cleanup must preserve that serialized callback or update the scene in the same later change.
- Artistic test components are added by `ArtisticElementsTestSetup`; their absent direct scene GUID references are expected.

## Assembly and platform evidence

All six GUID references in `AmpPortableDataViz.asmdef` resolve in the current local environment:

| Assembly GUID | Resolution |
| --- | --- |
| `702f733b4deb246808c6ce84d93b5c9c` | LiveKit `livekit.unity.Runtime.asmdef` in the current PackageCache |
| `fe685ec1767f73d42b749ea8045bfe43` | XR Interaction Toolkit runtime `2.5.4` |
| `6055be8ebefd69e48b49212b09b47b2f` | TextMeshPro runtime `3.0.6` |
| `75469ad4d38634e559750d17036d5f7c` | Input System runtime `1.18.0` |
| `f98e67abced6d0942a27fe2594eed6d9` | Machine-local RayNeo `SDK/Runtime/Scripts/RayNeo.OpenXR.ARDK.asmdef` |
| `af04830330a2bdd48ba3ec4e2897996b` | Machine-local RayNeo `OXR/Runtime/Unity.XR.RayNeo.OpenXR.asmdef` |

This resolves static GUID targets only. It does not prove fresh package restoration, licensing, compilation, Android native/IL2CPP linking, SDK/device compatibility, or runtime initialization. The two RayNeo references depend on the machine-local package directory in `Packages/manifest.json`. `AndroidRedisEndpointDiscovery` has substantial `UNITY_ANDROID && !UNITY_EDITOR` branches that Editor tests do not execute.

## Recommended later cleanup batches

1. Review the wrist-menu/interface cluster with the matching legacy visualization report, including any external provider implementation and recovery-scene references.
2. Review `RedisManager` as a small scene-wrapper cleanup, updating typed bootstrap references and deciding how to retain historical recovery scenes.
3. Decide whether LiveKit remains a supported source. If retiring it, remove the receiver/service/bootstrap branches/tests together; package manifest changes remain a separate explicitly authorized dependency task.
4. Decide which plasma/HUD/legacy graph modes remain useful. Remove their bindings, contracts, mappers, prefab references, bootstrap branches and related assets as coherent batches, preserving current graph and audience types that share files.
5. Keep the ArtisticElements feature, manual/test sources, and mock/loopback/session classes unless their positive usage is deliberately removed in a later change.

No current compilation or runtime blocker was established by this static review alone. Unity tests, Android build, and physical-device checks supply the remaining evidence in the main assessment.
