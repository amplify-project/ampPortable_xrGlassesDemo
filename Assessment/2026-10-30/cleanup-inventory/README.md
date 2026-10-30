# Cleanup usage inventory refresh

Step 2 completed on `cleanup/obsolete-code` at `369c06c8b873817dedaa01fe9f2cf16af21da423`, 2026-10-30. This is an inventory and review only. Step 3 (removing the first candidates) has not started. No application code, assets, tests, packages, or settings were changed; no commit or push was made.

## Coverage and verification

- Refreshed [code-inventory.csv](code-inventory.csv): all 96 first-party C# files under `Assets/AmpPortableDataViz`, independently matched against `rg --files`.
- Scanned 170 C# files under `Assets`, including 29 test files; 187 serialized assets including recovery scenes; and 1,022 metadata files. No missing/invalid script metadata or duplicate asset GUIDs were found.
- All 96 script paths, source SHA-256 values, GUIDs, and classifications match the original assessment inventory. Five rows have updated test-reference evidence; only `ArtisticElementsControlPanel` has a changed test-reference count (8 to 7). Runtime callers and serialized-reference evidence are unchanged.
- Re-read the four candidate implementations and checked their names/GUIDs across `Assets`, `Packages`, and `ProjectSettings`, including ignored/hidden files, excluding `.meta` self-definitions. Only the defining scripts and their own log messages matched.
- Also searched text C#/Java, JSON/XML, metadata, serialized scenes/prefabs/assets, and assembly definitions in the installed `Library/PackageCache` and the local RayNeo ARDK directory referenced by `Packages/manifest.json`. No candidate name/GUID matched. These directories were read only.
- Rechecked the enabled build scene, inherited particle-prefab chain, current runtime creation paths, persistent UnityEvents, Resources loading, native callbacks, and test callers as described below.
- [verification.json](verification.json) records the inventory comparison, output integrity checks, and identical before/after fingerprints of all 1,817 files under `Assets`, `Packages`, and `ProjectSettings`.

The scanner's classification rules remain explicitly tied to the original `60e2ff9` assessment; consequently `scan-summary.json` correctly says `review_commit_matches_HEAD: false`. The matching source hashes and fresh usage review above support carrying those classifications forward to this HEAD. The old reports remain historical snapshots, including their old documentation observations.

## First removal candidates, for step 3

Each has zero other C# type-name references (including tests), zero serialized GUID references, and zero recorded UnityEvent type-name references in the fresh scan. Absence of recorded usage makes these candidates; it does not establish external/manual workflow retirement.

| Script | Script GUID | Finding and remaining scope decision |
| --- | --- | --- |
| `Presentation/Visualization/FaceUserSmooth.cs` | `1e126d09f0b4ce243b941a0ab2396dc5` | Old camera-facing component; no recorded consumer. Check whether anyone still adds it manually. Its unguarded `using UnityEditor` is unchanged; the earlier successful Android build means it is not a demonstrated build failure. |
| `Presentation/Visualization/RuntimeImageBoard.cs` | `13af96730b4e4734abf8c774b56e4e5e` | Alternate quad texture/path loader. The current score board uses `RuntimePngLoader`. Check retained external scenes/manual tooling before retirement. |
| `Presentation/Visualization/URPImageBoardApplier.cs` | `b05a98cf044bed64bb943fa30397ea57` | Alternate URP image loader, including a public raw-PNG-byte API. No recorded consumer; establish whether that workflow is still intended. |
| `Presentation/Interaction/XRealControllerQuitter.cs` | `6ab818b15266638458808731a956b689` | Generic Input System hold-to-quit component despite its name. RayNeo uses `QuitApp`; decide whether controller-action quitting is still supported elsewhere. |

All paths above are relative to `Assets/AmpPortableDataViz`. When removal is authorized, each script and its matching `.meta` belong in the same small change, followed by missing-reference, compilation, and relevant Unity validation. Nothing in this inventory removes them.

## Positive dependencies that prevent speculative deletion

| Area | Fresh evidence |
| --- | --- |
| Build scene and selected visuals | `ProjectSettings/EditorBuildSettings.asset:10` enables only `Assets/Scenes/ampPortable_xrGlassesDemo.unity`. Scene lines 1908, 1935, and 1955 select particle, artist, and audience-graph prefabs. Lines 1914 and 1948 disable manual drivers and enable the selected sensor stream. |
| Inherited particle components | The selected `ParticleMeshVizRoot_RayNeo` references `ParticleMeshVizRoot` at prefab line 171; that prefab nests `ParticleMeshViz` at line 320. Base-prefab attachments remain dependencies even without a direct script GUID in the build scene. |
| Current score-image loader | The scene instantiates `ImageBoard` at line 1014 and overrides its image filename at lines 967-968. `ImageBoard.prefab:239` attaches `RuntimePngLoader`; line 252 enables startup loading. Zero external C# type-name callers does not make this loader obsolete. |
| Dynamic components | `Bootstrapper.cs:1192`, `:1204`, and `:1246` add `SensorStreamCoordinator`, `RayNeoTempleSensorStreamCycler`, and `ActiveSensorHudBinding`. Lines 2652, 2688, and 2959 add `BillboardLabel`. Redis pumps and anchor/source components are also created dynamically. Keep these dependencies and their tests. |
| Domain/fallback composition | `Bootstrapper.cs:241-250` constructs clocks, anchor/network services, factory, mapper, and session orchestration. Lines 416 and 421 add `SimpleVisualizer` and `AnchoredVisualization` for the missing-prefab fallback. Names such as Mock or Loopback do not establish obsolescence. |
| Inspector UnityEvents | Build scene lines 458-461 persist `Bootstrapper.BeginAfterEndpoint`. Recovery scene `0 (21).unity:6983` persists `VisualizationCycler.ApplyState`. These do not require ordinary C# callers. Historical `CycleVisualization` callbacks still refer to a commented-out method; record this retained-scene issue separately. |
| Resources and native callbacks | `RedisRuntimeSettings.cs:12,55` loads `Resources/RedisRuntimeSettings`. `RedisNsdDiscovery.java:170,223` sends messages to `OnRedisServiceResolved` and `OnRedisDiscoveryError`; `AndroidRedisEndpointDiscovery.cs:154,211-228` creates and preserves the receiver. Keep these string-based entry points. |
| Manual/test features | Manual drivers remain referenced by retained prefabs or typed runtime support. The ArtisticElements development scene feature and its tests remain supported. The refreshed CSV contains current test evidence, including the earlier screenshot-removal follow-up. |

## Connected groups needing a later support decision

| Group | Current dependency / decision needed before removal |
| --- | --- |
| Recovery-scene components | `VisualizationCycler` (15 scenes), `ToggleButtonColor` (18), `WristMenuFollower` (1), `HudSquareBuilder` (16), `EmotionTelemetryLogger` (17), and HUD visual/binding components (16 each) remain serialized. Counts are distinct assets. Decide whether `_Recovery` is retained before changing its component graph. |
| Wrist/hand interface | `IHandPoseProvider` is consumed by `WristMenuFollower`; no first-party implementation was found. External providers and the retained wrist scene need a coordinated decision. |
| Legacy Redis wrapper | `RedisManager` remains in 17 recovery scenes and is found/configured/enabled by Bootstrapper (lines 401-403, 527-531). It cannot be removed as an isolated unused class. |
| Optional LiveKit | Bootstrapper still declares, finds, binds, and configures `LiveKitTelemetryReceiver` (lines 28, 393, 467-470, 536-542). Service/parser/test/package relationships require a separate retirement decision. No dependency files were changed. |
| Legacy graph, plasma, HUD | `GraphBinding`, plasma bindings/visualizers, `FrameSmoother`, related mappers/contracts, and HUD mappings retain typed callers or prefab/scene consumers. `plasmaManual.unity` and optional Bootstrapper paths need a support decision as connected features. Shared contracts must be checked at declaration level before file deletion. |

## Reproduction and limits

From the repository root, regenerate the lexical inventory with:

```powershell
python Assessment/2026-10-30/inventory_code.py --output-dir Assessment/2026-10-30/cleanup-inventory
```

See [scan-summary.json](scan-summary.json) for scope, hashes, ambiguous type names, and scanner caveats. `UnityClockTests.cs` is non-UTF8 and was read with replacement for text scanning only; its bytes were not changed. Review details from the previous baseline remain in [legacy-code-review.md](../legacy-code-review.md) and [runtime-code-review.md](../runtime-code-review.md).

This is a text/serialization review, not a Unity Editor dependency audit or semantic reachability proof. Common nested type names, interpolation, conditional compilation, binary assets, external bundles/scenes, and manual workflows can evade or confuse lexical evidence. The added SDK search does not resolve those limits. No Unity session, test run, or build was started for this documentation-only step. The earlier 196/196 full-suite result and successful user device check remain the baseline; its previously recorded intermittent halo test is not reclassified as deterministic.

Stop boundary: inventory complete; no deletions, runtime changes, commits, or remote publishing. Step 3 awaits the user's instruction.
