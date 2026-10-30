# Targeted prefab script-GUID review

Assessed `rayNeoPort` at `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19`, read-only, on 2026-10-30. Scope: `ImageBoard.prefab`, `ParticleMeshViz.prefab`, `ParticleMeshVizRoot.prefab`, and `ParticleMeshVizRoot_RayNeo.prefab`.

**Finding: no absent/deleted source-GUID target was found in this hierarchy.** All six distinct serialized `m_Script` GUIDs resolve to existing scripts in the current project or the restored XR Interaction Toolkit package. This does not dismiss the Unity inspection's missing-component reports: GUID text resolution is weaker than Unity resolving a MonoScript's local fileID and runtime class.

The parent assessment reported one missing script in the main scene's ImageBoard instance and four on each of two inspected particle prefab hierarchies, nine occurrences when inherited ancestry is counted twice. This review did not run that Unity inspection itself.

## Complete script-GUID enumeration

Paths in the source/class column are relative to `Assets/AmpPortableDataViz/Presentation/Visualization` unless they explicitly name a package.

| Serialized location | GUID | Existing source/class target |
| --- | --- | --- |
| `Assets/Prefabs/ImageBoard.prefab:239` | `b25b1974c23e8a94086a8872b4ab0834` | `RuntimePngLoader.cs:7`; matching `.cs.meta:2`. |
| `Assets/Prefabs/ParticleMeshViz.prefab:4915` | `d0bcbbab2940487c804471b35203e1f0` | `ParticleMeshVisualizer.cs:17`; matching `.cs.meta:2`. |
| `Assets/Prefabs/ParticleMeshViz.prefab:5017` | `d5189cfb48f34bd7b0687cf4b010df5d` | `AudienceSignalBinding.cs:16`; matching `.cs.meta:2`. |
| `Assets/Prefabs/ParticleMeshViz.prefab:5059` | `4fca7e35d651470b96c88ea31139fb69` | `AudienceDataManualDriver.cs:14`; matching `.cs.meta:2`. |
| `Assets/Prefabs/ParticleMeshViz.prefab:5116` | `46a6202ad8a64b2db7b22f6d0f6a9ab8` | `AudienceSignalVisualizerBinding.cs:14`; matching `.cs.meta:2`. |
| `Assets/Prefabs/ParticleMeshViz.prefab:5183` and `Assets/Prefabs/ParticleMeshVizRoot.prefab:73` | `0ad34abafad169848a38072baa96cdb2` | Restored `Library/PackageCache/com.unity.xr.interaction.toolkit@2.5.4/Runtime/Interaction/Interactables/XRGrabInteractable.cs`; matching `.cs.meta:2`. |

The five first-party source GUIDs resolve in both the original checkout's `Assets` and the fresh isolated project's `Assets`. The XRI GUID resolves in the freshly restored isolated PackageCache. A simultaneous GUID search in the configured local RayNeo SDK found no additional target for these six GUIDs.

Fresh isolated project inspected:

`C:/Users/BRYAND~1/AppData/Local/Temp/rnp1030/project`

Local SDK searched:

`C:/Users/Bryan Dunphy/Documents/RayNeoSDK/RayNeo OpenXR Unity ARDK/RayNeo OpenXR Unity ARDK`

## Inheritance and local component pointers

- `ParticleMeshVizRoot_RayNeo.prefab:171` points to `ParticleMeshVizRoot` (`74e05b7be47103748b0057db90b75d78`). The variant contains no direct `m_Script` entries and no added/removed components of its own.
- `ParticleMeshVizRoot.prefab:320` nests `ParticleMeshViz` (`ed2e2f5bc2c99764e8b09beb23ebab55`). Its own direct script entry is XRGrabInteractable. It removes two inherited native/component entries at lines 314-316; the four current first-party particle scripts remain represented in the base prefab.
- A local-YAML check matched every GameObject `m_Component` fileID against a document anchor in the same file: ImageBoard 14 references/24 documents; ParticleMeshViz 15/17; ParticleMeshVizRoot 3/7; RayNeo variant 0/1. No missing local component-document pointer was found by that check. It does not validate Unity's imported external asset/fileID resolution.
- The same four first-party particle GUIDs appear in the freshly copied base prefab, so the reported four-plus-four pattern is consistent with repeated inherited component occurrences rather than eight distinct deleted scripts. This is an inference pending the Unity probe.

## Git comparison with main and removed classes

Read-only `git show main:Assets/Prefabs/ImageBoard.prefab` and `git show main:Assets/Prefabs/ParticleMeshViz.prefab` contain the same six distinct `m_Script` GUIDs. All five first-party target `.cs.meta` files also exist on local `main` with those same GUIDs. None of these five targets was deleted by the port.

For comparison, the 16 first-party `.cs.meta` files deleted between local `main` and `rayNeoPort` belong to `RuntimeDiagnosticsLogger`, `ColliderAttacher`, `ColliderSizeManager`, `DynamicAttachPointHandler`, `GrabManager`, `PaperFollowerManager`, `PaperGridLayoutFixed`, `PaperGridManager`, `PaperPlacementMode`, `PinchToggle`, `RuntimeScoreImporter`, `ScoreLoader`, `SimpleGrid3D`, `WristMenuUlnarFollower`, `WristMenuUlnarRigid`, and `XRHandsPoseProvider`. Their GUIDs were read from `main`; none matches the six GUIDs in this reviewed hierarchy. No removed class can be identified as the missing target here from that evidence.

The five current first-party `.cs.meta` files contain only `fileFormatVersion` and `guid`, without a saved `MonoImporter` block, in both the original checkout and the fresh isolated copy. This two-line form also exists in local `main`; `git diff main...rayNeoPort` for those metadata files is empty. By contrast, the newer `ArtistVizVisualizer.cs.meta` contains a full MonoImporter block. This difference is an observation to investigate with Unity, not proof of the cause and not authorization to rewrite metadata.

## Remaining distinction for the Unity probe

For these exact source paths/GUIDs, inspect `AssetDatabase.GUIDToAssetPath`, imported asset type/importer, all local object IDs, `MonoScript.GetClass()`, and assembly-qualified class lookup, then compare those results to the prefab component references. That will distinguish a valid source file whose imported script/class cannot be resolved from an actual dangling serialized pointer. The parent assessment owns that execution.

No source, metadata, prefab, scene, package, or project setting was changed, and no Unity/test/build process was started for this review. Only this report was written.
