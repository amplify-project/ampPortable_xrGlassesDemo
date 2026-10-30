# XR Score Viewer — High-Level Changes Since v010-alpha7

This is a high-level summary of the `rayNeoPort` branch compared with the XREAL Air 2 Ultra / Beam Pro version documented by `README__v010-alpha7.md` and `AbstractMapping_guide_v010-alpha7.pdf`. It describes user-visible and architectural changes, not every commit or asset adjustment.

Documentation snapshot: 31 August 2026.

## Platform and toolchain

- The target hardware moved from XREAL Air 2 Ultra with Beam Pro to the standalone RayNeo X3 Pro.
- The project moved from Unity `6000.3.10f1` to Unity `2022.3.36f1` to match the RayNeo OpenXR Unity ARDK toolchain.
- XREAL XR configuration was replaced with OpenXR plus RayNeo Support and the RayNeo Controller Profile.
- The Android entry point now uses RayNeo's `UnityOpenXrActivity` and `com.rayneo.mercury.app` metadata.
- The Android build is configured for API 30, ARM64, IL2CPP, and OpenGL ES 3 under application ID `com.TUS.RayNeoXRScoreViewer`.

## Interaction and presentation

- Hand tracking, wrist menus, grabbing, and hand-based placement from the XREAL scene were removed from the active RayNeo experience.
- Visualizations are now head-anchored HUD elements that stay in the wearer's field of view.
- The right-temple touchpad is the primary control surface:

  - single tap cycles visualizations;
  - double tap cycles the active sensor;
  - forward/backward swipes resize the gazed visualization;
  - up/down swipes reposition the active visualization vertically;
  - a two-finger tap quits the app.

- Scaling is gaze-aware and constrained to a safe range. Height movement is also bounded.
- A sensor HUD identifies the selected device and its position in the discovered sensor list.

## Visualization set

The main scene now cycles through four visualization roots:

1. score/image board;
2. abstract particle mesh;
3. objective five-channel graphs;
4. ArtistViz.

ArtistViz currently has its data contracts, mapper, binding, prefab, and scene root in place, but its visible output is a placeholder for a future graphical score.

The score image was recropped and its runtime loader now preserves aspect ratio so more of the notation fits within the X3 Pro field of view.

## Five-stream data model

The active abstract and objective views were reduced to five mapped streams:

1. tonic electrodermal activity standard deviation;
2. temperature rate-of-change standard deviation;
3. skin conductance response frequency standard deviation;
4. heart-rate standard deviation;
5. engagement.

Arousal, valence, and inter-beat interval were removed from these two visualizations. Some compatibility contracts and decoders still retain those fields, but they do not drive the current particle mesh or five-graph display.

## Abstract mapping redesign

The particle-mesh mappings were separated so that each stream owns a distinct visual channel:

- tonic EDA controls slow whole-mesh compression/expansion and centre lift;
- temperature rate of change controls warm/cool particle colour waves and directional trails;
- SCR frequency controls short-lived local rings, sparks, and deformations;
- heart rate controls global particle size, synchronized pulse, and four corner semicircular halos;
- engagement controls particle dispersion and mesh coherence.

Heart-rate halos moved from one global halo to four corner halos. Each corner has a neutral reference semicircle and an active semicircle that stays outside the reference for positive values and inside it for negative values. Heart-rate data also fades to a neutral state when it becomes stale.

Temperature trails were recoloured red/orange for positive values and blue for negative values. Their vertical offset now makes positive trails appear above the particles and negative trails below them.

## Objective graph updates

- The objective view now shows the same five active streams as the abstract view.
- Physiological graph ranges adapt dynamically to recent data, with faster zoom-out and slower zoom-in behaviour.
- Temporal smoothing was added to improve readability.
- Graph colours, line widths, label sizes, panels, glow materials, and global bloom were adjusted for legibility on the X3 Pro displays.

## Sensor routing and engagement

- The app discovers available physiological sensor streams and maintains one shared active selection.
- A double tap selects the next sensor; the abstract, objective, and ArtistViz data paths follow the same selection.
- Engagement changed from an audience-wide source to per-sensor routing.
- The configured engagement channel format is `device:{serial}:engagement`.
- Engagement payload decoding supports `device`, `engagement`, `confirmed`, and `timestamp` fields, with timeout handling and a neutral fallback.

## Redis startup and network discovery

- The Android build can discover Redis through DNS-SD using `_amplify-redis._tcp`.
- On startup, a reachable saved endpoint and automatic discovery race in parallel; the first valid connection is used and cached.
- If automatic resolution fails, the wearer can still enter and verify the Redis host and port manually.
- Android network, Wi-Fi, and multicast permissions were added for Redis connectivity and service discovery.

## Architecture and verification

- Sensor selection, selected-source projection, engagement routing, endpoint resolution, and ArtistViz contracts were added within the existing Core/Application/Infra/Presentation layers.
- The RayNeo scene and bootstrapping path were simplified around touchpad interaction and runtime-spawned per-sensor visualizations.
- PlayMode coverage was expanded for Redis endpoint discovery, sensor selection, per-sensor engagement, graph ranges/smoothing, heart-rate responses, ArtistViz mapping/binding, and particle-mesh mapping behaviour.

## Known current limitation

ArtistViz is wired into the runtime cycle and data architecture, but the final graphical-score visual treatment has not yet been implemented; the current glasses build shows a placeholder.

