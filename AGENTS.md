# AGENTS.md

## Project Overview

This is a Unity XR glasses proof-of-concept for real-time audience data visualization. Visualizations act as abstract graphical scores and engagement/emotion displays.

Unity version: `6000.3.10f1`

Primary project code lives in:

- `Assets/AmpPortableDataViz/Core`
- `Assets/AmpPortableDataViz/Application`
- `Assets/AmpPortableDataViz/Infra`
- `Assets/AmpPortableDataViz/Presentation`

## Architecture

Follow the existing layer structure:

- `Core`: domain models, contracts, ports, shared interfaces.
- `Application`: session orchestration and visualization message flow.
- `Infra`: concrete services such as Redis, LiveKit, clocks, device discovery.
- `Presentation`: Unity `MonoBehaviour` components, visualizers, bindings, input, XR interaction, UI, debug tools.

Avoid moving responsibilities between layers unless explicitly requested.

## Unity Rules

- Do not hand-edit generated Unity files such as `Library/`, `Temp/`, `obj/`, generated `.csproj` files, or generated solution files unless specifically requested.
- Preserve Unity `.meta` files when adding, moving, or deleting assets.
- Prefer serialized private fields over public mutable fields for `MonoBehaviour` configuration.
- Keep runtime code compatible with Unity serialization.
- Avoid blocking calls on the Unity main thread.
- Be careful with scene, prefab, material, and asset changes; explain them clearly.

## Coding Style

- Match the existing C# style in nearby files.
- Keep changes focused and avoid unrelated refactors.
- Prefer clear domain names over generic names.
- Use explicit null checks where Unity object lifetime may be involved.
- Keep visualization update code allocation-conscious where it runs every frame.
- Do not introduce new packages unless requested or clearly necessary.

## Visualization Area

The current active visualization work is around:

- `Assets/AmpPortableDataViz/Presentation/Visualization/EngagementHudBinding.cs`
- `Assets/AmpPortableDataViz/Presentation/Visualization/EmotionRayPlasmaManualDriver.cs`
- `Assets/AmpPortableDataViz/Presentation/Visualization/EmotionRayPlasmaVisualizer.cs`
- `Assets/AmpPortableDataViz/Presentation/Visualization/EmotionRayPlasmaBinding.cs`

For bindings, preserve the pattern of mapping incoming data into visualizer parameters rather than mixing data acquisition directly into visualizer components.

## Dependencies

Important packages include:

- Unity XR Interaction Toolkit
- Unity XR Hands
- AR Foundation
- OpenXR
- URP
- LiveKit Unity SDK
- NuGetForUnity
- XREAL SDK via local package path

Do not alter `Packages/manifest.json` or `Packages/packages-lock.json` unless dependency work is explicitly requested.

## Tests

PlayMode tests live in:

- `Assets/Tests/PlayMode`

Use Unity Test Runner when possible.

Example Windows CLI command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults\playmode.xml -quit
```

If Unity is not available from that path, report that tests could not be run and explain what was checked instead.

## Agent Behavior

- Read nearby files before editing.
- Prefer small, reviewable changes.
- Do not revert user changes.
- Explain any assumptions that affect Unity scenes, prefabs, runtime behavior, or XR device behavior.
- When changing visual output, describe what should be inspected in the Unity Editor or on-device.
