# Retire the original session path and simplify the Bootstrapper Inspector

## Authorized scope

The user retired all unused original-session and spatial-anchor functionality,
requested the Inspector removals/renames below, and explicitly deferred helper
extraction. The work is split into session removal and retained-field migration.
The first chunk is local commit `b9ddb2e`; see [its report](session-removal.md).

The active selected-sensor Redis app, per-device views, ArtistViz, manual tools,
individual engagement routing, shared physiological pumps, and current input
behavior remain. No package manifests or dependency locks changed. No helper
classes or layers were introduced. Nothing was pushed or installed on the glasses.

## Removed functionality

- Primary scalar Redis session and Bootstrapper's `RedisSource` reference.
- Host/visual/profile/seed session settings, the single-choice source dropdown,
  session message topics, and scalar-to-simple-visual mapping settings.
- Spatial-anchor registry, anchor interfaces/descriptors, mock service,
  anchored-visualization component, and associated session/factory contracts.
- Original simple visualizer, its factory/mapper and exclusive demo prefab,
  local loopback message transport, unused pose/spawn contracts and task adapter.
- Bootstrapper's automatic fallback sphere, session construction, startup and
  subscriptions, and cleanup code that existed solely for the removed session.

Thirteen runtime scripts and their matching metadata are removed, together with
the exclusive prefab and its metadata and the empty anchor folder's metadata.
Generic data/clock/mapper/visualizer contracts and the graph contracts remain.
The current physiological and engagement services were not removed or refactored.

The asset-reference inventory found that the only saved references to removed
component scripts were on the exclusive demo prefab, which itself had no saved
callers. `retired-assets.json` records the GUIDs and references. All retained saved
assets were checked for references to those GUIDs after removal.

## Retained Inspector settings

| Previous field | Current field / Inspector meaning |
| --- | --- |
| `VisualPrefab` | `AbstractVizPrefab` — **AbstractViz Prefab** |
| `AutoSpawnAudienceVisualsFromChannels` | `AutoSpawnAbstractViz` — **Auto Spawn AbstractViz** |
| `IncludeEmotionChannels` | `IncludeSensorChannels` |
| `EmotionDeviceIds` | `SensorIds` |
| `AutoDetectEmotionDeviceIds` | `AutoDetectSensorIds` |
| `EmotionVisualParent` | `AbstractVizParent` |
| `EmotionVisualOriginOffset` | `AbstractVizOriginOffset` |
| `EmotionVisualSpacing` | `AbstractVizSpacing` |
| `EmotionVisualsPerRow` | `AbstractVizPerRow` |
| `AnchorPosition` | `SpawnPosition` |
| `AnchorRotationEuler` | `SpawnRotationEuler` |

Headings now describe Sensor Discovery, AbstractViz Layout/Settings, Sensor Labels,
and Visualization Spawn Placement. Discovery and positioning methods/logs use
sensor/AbstractViz names. `field-renames.json` is the complete mapping.

Spawn position and rotation retain ordinary world-space placement when no custom
parent is assigned. With a parent, the original local layout rules still apply.
Their values have not been reset. Spatial-anchor behavior is removed; former
anchor field names survive only as serialization-migration strings and test data.

All eleven renamed fields carry `FormerlySerializedAs` attributes. The main scene's
saved field names were migrated explicitly, retaining all values and object IDs,
including its ParticleMeshVizRoot_RayNeo prefab reference. Other legacy serialized
assets can migrate when Unity imports them. Existing Redis channel strings and
protocol helpers retain their values and names; there are no newly named Emotion
settings. The entire Individual Sensor Engagement configuration block is unchanged.

## Validation

The machine-readable run totals and final build/scene results are recorded in
`validation-summary.json`. Raw XML and run records retain every attempt.

- Session removal: **16/16 focused Bootstrapper tests**, then **192/192 full tests**.
- Six placement cases passed before renaming; their assertions are identical after
  renaming. They cover abstract/graph row placement and ArtistViz parent origin,
  each with a transformed parent and with world-space spawn placement.
- Renamed Bootstrapper: **23/23 focused tests**, including real Unity prefab import
  using former field names. It verifies all eleven migrated fields, external
  prefab reference and internal parent reference with nondefault values.
- The initial final-source full run passed **198/199** and hit the previously
  recorded intermittent corner-halo assertion. The halo test passed in isolation.
  Visualization sources and that test are byte-identical to the baseline; they
  were not changed or suppressed. The unchanged full rerun is retained separately.
- The unchanged full rerun passed **199/199**, Unity exit 0.
- The Android IL2CPP APK build **succeeded**, with **0 errors and 20 unchanged
  warnings**. All warning messages match the previous step 4 build exactly.
- Saved-scene inspection found **0 missing scripts, 0 broken references and
  0 blockers**, scanning 248 scene components, 8 prefabs and 17 materials.

The prior suite contained 210 cases. Eighteen exclusively retired tests were
removed: thirteen anchor/network-contract cases and five primary-session lifecycle
cases. All 192 remaining test identities are retained. Six placement cases and one
serialization case were added, giving a final suite of 199 cases. Retained
automatic/manual lifecycle tests still check reconfiguration, shared-pump counts,
mode transitions, disable/re-enable and repeated cleanup.

Transport-independent tests use inactive real transport objects; they do not claim
to exercise a live Redis connection or XR hardware. The migration test creates
uniquely named temporary prefabs in the isolated test project and removes them in
`finally`. No test fixture assets are retained in the source checkout.

## Source verification and reproduction

`verify_source.py` checks the complete Assets/Packages/ProjectSettings source set
against the recorded baseline. It verifies the saved scene differs only by removal
of retired settings and renaming retained fields, engagement settings are unchanged,
retired symbols/GUID references are absent, metadata is preserved, and no new
runtime helpers were added. It also checks that pump acquisition/release, every
individual cleanup operation, and positioning/label math remain unchanged after
substituting the field/method renames.

Bootstrapper changes from **1,907 to 1,698 lines**, removing ten methods and adding
none. Forty-one retained method bodies match after renaming; eleven change to
remove the retired session path. **1,666 other original input files are byte-identical**.

Run `python verify_source.py` to reproduce scope checks. `sync_snapshot.py <phase>`
copies/verifies source inputs into the idle isolated Unity project recorded in the
snapshot records, never generated caches. `run_validation.ps1 -Stage PlayMode
-Attempt <unique-name>` runs tests; `-TestFilter Bootstrapper` selects focused tests.
Android validation uses `-Stage Build -UseWindowsTrustStore`, retaining the existing
process-local Java trust-store and Git settings. Raw logs and APK output are under
ignored `Temp/BootstrapperSessionRetirement-20261103/` paths.

No change to active visual appearance is intended. In the Editor, the updated
Bootstrapper should show the clearer names and retain its prefab/parent assignments.
A later on-device check can confirm live sensor/view cycling, placement and
quit/relaunch; the installed APK was not replaced during this work.
