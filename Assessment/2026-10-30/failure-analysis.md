# Diagnosis of the fresh PlayMode failures

## Follow-up: optional preview removed

The user confirmed that the preview images were not reviewed and authorized removing the screenshot step. Only `ArtisticElementsSceneTests.cs` changed: the capture call/helper and unused System.IO import were removed; all 55 assertion lines and the test metadata remain intact. No runtime code, scene, renderer, or package settings changed.

The affected test passes in isolation. The latest unfiltered suite passes **196/196**, with zero skipped/inconclusive cases (`playmode-results.xml`). The existing preview file's timestamp and checksum did not change, confirming no new image generation.

One earlier post-removal full run encountered a different halo assertion: the negative active radius equalled its neutral reference after two frames. That unchanged test passed in isolation, and the unchanged full-suite rerun passed 196/196. The intermittent result is retained in `playmode-no-preview-full.xml` and `screenshot-removal-validation.json`; no halo assertions were weakened or code changed.

The remainder of this document preserves the original assessment's failure analysis. Its pre-removal XML is now `playmode-before-preview-removal.xml`; the screenshot-related failure is resolved by the user-requested removal.

Snapshot: `rayNeoPort` at `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19`, with the pre-existing URP settings changes included in the assessed copy. This is a diagnosis only; no source, prefab, scene, metadata, assembly definitions, or tests were changed to obtain it.

## Original diagnosis: canonical import supersedes initial missing-script failures

The fresh canonical-path run at 12:36:34Z executes all 196 cases: **195 passed, one failed, zero skipped or inconclusive**. The ArtistViz prefab-wiring test and two of the three ArtisticElements scene tests now pass. The remaining scene test gets past setup discovery and fails on an unexpected URP RenderPass error during preview capture, not on a missing setup component. This supersedes the initial missing-script symptoms as evidence of an unhealthy initial validation environment rather than established authored class/asset defects.

The initial isolated project used a DOS 8.3 alias (`C:/Users/BRYAND~1/...`), while compiler/build logs used `C:/Users/Bryan Dunphy/...`. Its probes showed all 96 first-party MonoScripts returning no imported class despite all 13 checked expected MonoBehaviour types existing in Amp. Reopening that same Library canonically did not repair the associations. A separate copy imported canonically from the start resolves the previously failing asset-wiring checks without source/asset repairs. Its renewed main-scene inspection reports zero missing scripts and zero warnings. The final canonical MonoScript probe at 12:43:25Z reports 92 imported classes across 96 scripts and **13/13 expected MonoBehaviour type associations matching**, closing the initial association failure as a harness false positive. The alias/initial-Library boundary is implicated, but the precise engine mechanism is not proven.

## Initial result, retained as diagnostic history

`playmode-alias-results.xml` preserves the initial 196 discovered/executed cases: 192 passed, 4 failed, 0 skipped, 0 inconclusive. Run timestamps are 2026-10-30 12:18:23Z to 12:18:24Z. The four failures all concern serialized asset wiring, rather than the corresponding programmatically constructed components. `playmode-before-preview-removal.xml` now contains the authoritative fresh canonical 195/196 result described below.

| Failing test | Observed assertion | Evidence |
| --- | --- | --- |
| `ArtisticElementsSceneTests.Initialize_IsIdempotentAndDoesNotDuplicateControlsOrEventSystem` | Expected one setup, found zero | XML lines 535-550; helper assertion `ArtisticElementsSceneTests.cs:296` |
| `ArtisticElementsSceneTests.SavedScene_ControlsAllFiveImportedElementsAndRestoresAuthoredAppearance` | Expected one setup, found zero | XML lines 552-567; same helper assertion |
| `ArtisticElementsSceneTests.SavedScene_LengthSliderStretchesImportedModelWithoutChangingItsMesh` | Expected one setup, found zero | XML lines 569-584; same helper assertion |
| `ArtistVizAssetWiringTests.ArtistPrefab_HasVisualizerAndConfiguredBinding` | Root `ArtistVizVisualizer` component was null | XML lines 631-643; `ArtistVizAssetWiringTests.cs:35` |

The isolated Unity log, `Temp/PreMergeAssessment-20261030/playmode-shortpath.log`, emits `The referenced script (Unknown) on this Behaviour is missing!` immediately before each load of `Assets/Scenes/ArtisticElementsTest.unity` (lines 28444, 28451 and 28458), and twice during `AssetDatabase.LoadAssetAtPath` for `Assets/Prefabs/ArtistViz.prefab` (lines 28465 and 28509).

Those messages are positive evidence of unresolved serialized components in this run. The failures must not be dismissed as merely a weak test assertion or a missing class source file.

## ArtisticElements scene

- The test loads `Assets/Scenes/ArtisticElementsTest.unity` additively using `EditorSceneManager.LoadSceneAsyncInPlayMode` (`ArtisticElementsSceneTests.cs:232-238`). It confirms the scene is valid and loaded before obtaining the setup.
- `GetSetup` searches every root and all descendants, including inactive objects (`ArtisticElementsSceneTests.cs:293-307`). Moving the setup to a child or disabling its object would not explain the count of zero.
- The saved scene explicitly contains the setup component: `MonoBehaviour &2200000005` references GUID `9b2d3b1f9ce44a2c93960ed0c3c8e891` at `ArtisticElementsTest.unity:1820`. The component belongs to GameObject `2200000003`, and its parent transform `2200000004` appears in the scene root list at line 1843.
- That GUID exactly matches `Presentation/ArtisticElements/ArtisticElementsTestSetup.cs.meta:2`. The script declares a public, nonabstract `ArtisticElementsTestSetup : MonoBehaviour` in the expected namespace at `ArtisticElementsTestSetup.cs:8`.
- The setup's `elementRoots` has five references and an assigned `thicknessElement` (`ArtisticElementsTest.unity:1823-1829`). These are present in text, but a missing setup component prevents initialization from using them.
- The 16 `ArtisticElementControllerTests` pass. Those tests establish controller behavior when constructed by code, not that the saved scene can resolve its setup component.

Conclusion: the test's hierarchy search is consistent with the authored scene. The initial failure boundary was deserialization/resolution of the saved setup component. No missing source file, obvious GUID mismatch, or inactive-root explanation was found. The later canonical run successfully obtains the setup and passes two scene tests, superseding the initial missing-setup failure.

## ArtistViz prefab

- `ArtistVizAssetWiringTests.cs:30-35` loads the prefab asset, verifies the prefab exists, and queries `ArtistVizVisualizer` on its root.
- The saved root GameObject is explicitly named `ArtistViz` and lists both component IDs `1279853975181627708` and `6931765003918991246` (`ArtistViz.prefab:116-131`). The visualizer was not moved to a child.
- The visualizer component uses GUID `26217a96d814fd247b78fac48e915941` (`ArtistViz.prefab:152`), matching `ArtistVizVisualizer.cs.meta:2`. Its class is public, nonabstract, named after its file, and derives from MonoBehaviour (`ArtistVizVisualizer.cs:12`).
- The binding component uses GUID `46a6202ad8a64b2db7b22f6d0f6a9ab8` (`ArtistViz.prefab:165`), matching `AudienceSignalVisualizerBinding.cs.meta:2`. Its serialized `artistVizVisualizer` points to the root visualizer component (`ArtistViz.prefab:178`). The log's two missing-script messages are consistent with both authored components failing to resolve, though the assertion aborts at the visualizer check before independently asserting the binding.
- Three `ArtistVizBindingTests`, three `AudienceSignalVisualizerBindingSmoothingTests`, and one `BootstrapperArtistVizTests` pass. These construct the relevant classes programmatically, showing the compiled managed types exist and can participate in the tested logic.

Conclusion: root-only `GetComponent` is consistent with this prefab's authored structure. The initial failure concerned serialized component resolution, not an outdated root-versus-child assumption. The prefab is assigned to the enabled app scene (`ampPortable_xrGlassesDemo.unity:1935`). Its asset-wiring test now passes in the fresh canonical import, so the initial null visualizer is superseded as an environment-induced failure.

## Likely failure boundary and remaining uncertainty

The strongest supported diagnosis is a broad MonoScript/type-association failure in the initial isolated project. Exact engine/importer/path cause is not yet established.

Supporting checks:

- All three script GUIDs resolve to a single `.meta` in `Assets`; no copies of those GUIDs were found in either the restored short-path snapshot's `Library/PackageCache` or the machine-local RayNeo SDK. The test log has no matching GUID collision/conflict messages. No duplicate first-party class declarations or nested assembly definitions were found in these directories.
- The source declarations and file names match. The scripts belong to the `Amp` assembly, which the `Tests` assembly explicitly references. Programmatic tests can construct the types, so a general compile failure or an assembly exclusion does not explain these specific null serialized components.
- The Unity log records imports of all three scripts with their expected GUIDs (lines 12595, 12703 and 18025).
- SHA256 comparisons showed the original checkout and tested snapshot match for both failing assets and the three relevant `.cs`/`.cs.meta` pairs (eight files total). These failures were not introduced by edits to those files during assessment.
- A correlation worth recording is that both missing ArtistViz components have blank `m_EditorClassIdentifier` fields (`ArtistViz.prefab:154,167`), as does the scene setup (`ArtisticElementsTest.unity:1822`). The same binding script GUID has an explicit `Amp::AmpPortableDataViz.Presentation.Visualization.AudienceSignalVisualizerBinding` identifier in `AudienceGraphPrefab.prefab:221` and `ParticleMeshViz.prefab:5118`. Blank identifiers are also common normal serialization; this difference is not proof of invalid assets or of a fix. All three implicated asset script pointers use the usual source-script local ID `11500000`.

These initial static checks alone could not distinguish authored serialization from import/type registration. The later probe and fresh canonical full-suite run provide the additional evidence below. The initial failed result is retained even though its missing-component symptoms do not reproduce in the fresh canonical control.

## Initial-harness inspection of the current application, provisional history

The isolated `PreMergeValidation` inspection at `2026-10-30T12:22:06.5545485Z`, Unity `2022.3.36f1`, reports missing components in the enabled application's dependency graph as well. Its `scene-inspection.json` was written beside the short-path isolated project, under `C:/Users/Bryan Dunphy/AppData/Local/Temp/rnp1030`.

| Measured scope/result | Count |
| --- | --- |
| Enabled application scenes | 1: `Assets/Scenes/ampPortable_xrGlassesDemo.unity` |
| Dependencies | 84 |
| Prefabs inspected | 8 |
| Materials inspected | 15 |
| GameObjects inspected | 87 |
| Non-null components inspected | 218 |
| Missing-script component occurrences | 9 |
| Locations reporting missing scripts | 3 |
| Unresolved object references with nonzero instance ID | 0 |
| Material/shader warnings reported by this inspection | 0 |

The three recorded locations are:

| Asset | Object path | Missing-script occurrences |
| --- | --- | --- |
| `Assets/Scenes/ampPortable_xrGlassesDemo.unity` | `XR Plugin/CameraOffset/Head/ImageBoardParent/ImageBoard` | 1 |
| `Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab` | `ParticleMeshVizRoot_RayNeo/ParticleMeshViz` | 4 |
| `Assets/Prefabs/ParticleMeshVizRoot.prefab` | `ParticleMeshVizRoot/ParticleMeshViz` | 4 |

These are nine component occurrences, not nine proven distinct script defects: prefab inheritance/dependency traversal can count the same underlying component issue more than once. The zero broken-reference count excludes empty references whose instance ID is zero; it does not establish that all required Inspector assignments are present. The zero shader-warning count concerns Editor inspection, not Android shader compatibility.

Within the initial harness, the findings extended beyond the nonshipping ArtisticElements test scene: the enabled app reported an ImageBoard missing component, and its particle prefab dependency chain reported missing components. These observations came from the globally unhealthy association environment and are superseded by the fresh canonical inspection below. The initial nine occurrences must not be presented as branch asset defects. An Android build outcome cannot by itself establish that these content paths initialize correctly.

The renewed canonical-path inspection at `2026-10-30T12:37:27Z` is preserved in `Temp/PreMergeAssessment-20261030/android-canonical-trust/scene-inspection.json`. It reports:

| Inspection | Dependencies | Prefabs | Materials | GameObjects | Non-null components | Missing-script occurrences | Nonzero unresolved refs | Warnings |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Initial alias-created Library, historical | 84 | 8 | 15 | 87 | 218 | 9 | 0 | 0 |
| **Fresh canonical import, current** | **86** | **8** | **17** | **87** | **248** | **0** | **0** | **0** |

The current inspection passes its enabled-scene/dependency checks with no missing scripts or recorded blockers. This corroborates the canonical test improvement and supersedes the initial ImageBoard/particle missing-component findings. Its scope and intentional-null/runtime/device limitations remain unchanged.

## Supplementary probe prepared

`MonoScriptProbe.cs` is a validation-only template outside the original `Assets` directory. It was statically compiled successfully against the installed Unity `2022.3.36f1` managed modules. It is intended to be copied into the isolated project's `Assets/Editor` after the in-progress Android build, then invoked as `MonoScriptProbe.Inspect` in a subsequent Editor session.

The probe writes `mono-script-probe.json` beside the isolated project and records:

1. Every first-party MonoScript found through `AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/AmpPortableDataViz" })`.
2. `GUIDToAssetPath`, loaded asset path, imported GUID/local file ID, and `MonoScript.GetClass().AssemblyQualifiedName`.
3. Comparisons against the loaded `Amp` assembly's expected types for 13 known MonoBehaviour scripts, including the three initial failure types, `RuntimePngLoader`, the particle/audience types, graph visualizer, bootstrap, and endpoint prompt.
4. Present managed component types and missing/null component counts in ArtistViz, ImageBoard, particle base/root/RayNeo-root, and audience graph prefabs.

The helper performs no save, repair, forced reimport, metadata edit, or settings update. A zero probe exit code means evidence was written; it does not mean the project passed. A null GetClass result is legitimate for many contract/interface/generic files, so only the explicit MonoBehaviour comparisons should be treated as association checks. This later probe can distinguish imported type mapping from persistent prefab resolution without rewriting the failing assets.

## Probe outcomes and environmental control

| Evidence | Project path form | Imported classes / scripts | Expected MonoBehaviour types present in Amp | Known type association mismatches |
| --- | --- | --- | --- | --- |
| `mono-script-probe-alias.json`, 12:28:50Z | DOS 8.3 alias, initial Library | 0 / 96 | 13 / 13 | 13 / 13 |
| `mono-script-probe-canonical-warm.json`, 12:31:32Z | Canonical path, same physical project and Library | 0 / 96 | 13 / 13 | 13 / 13 |
| **`mono-script-probe.json`, 12:43:25Z** | **Separate fresh canonical import** | **92 / 96** | **13 / 13** | **0 / 13** |

Null GetClass results are legitimate for some contract files, but the 13 known concrete MonoBehaviour mismatches cannot be explained by that normal limitation. The initial Android log also records `TypeDB: Assembly index ... was not found` across project, package and third-party assemblies, including `Amp.dll` (`android-build.log:876`). A read-only summary of the generated Editor `TypeDb-All.json` nevertheless finds 73 assembly records and an Amp record with 176 types, including the implicated classes, using the canonical path. This is further evidence of a registration/index lookup boundary, not evidence that those types failed to compile.

The path alias is a plausible environmental trigger, not a confirmed Unity defect. Official research found no directly matching 8.3-alias issue. Unity's [2022.3 MonoScript.GetClass API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoScript.GetClass.html) describes returning the implemented type. The related [UUM-27742 issue](https://issuetracker.unity.com/issues/17806/monoscriptgetclass-returns-null-when-the-script-contains-a-struct-inside-a-namespace-or-an-interface-inside-a-namespace) concerns namespaced structs/interfaces and was fixed in 2022.2; it does not explain these concrete MonoBehaviour checks.

The separate fresh canonical-path copy has run the full suite without the initial Library or source/asset repairs. Three of the initially failed cases pass, and the fourth advances to rendering rather than setup discovery. The renewed canonical main-scene inspection passes with zero missing scripts. The final `mono-script-probe.json`, generated at 12:43:25Z, further reports 92 imported classes out of 96 scripts and zero mismatches across all 13 explicitly checked expected MonoBehaviour types. Together these close the initial type-association/missing-component findings as harness-induced false positives. The remaining four scripts without a matching imported class are not missing components merely because GetClass returns null: files containing contracts, interfaces, generic types, or helper declarations need not have a matching main class. The known-type and serialized-component checks are the relevant positive evidence.

## Original preview RenderPass size mismatch (resolved by capture removal)

Current evidence: `playmode-before-preview-removal.xml` (copied from `Temp/PreMergeAssessment-20261030/playmode-canonical.xml`), 12:36:34Z-12:36:36Z, and its matching `playmode-canonical.log`. The only failing case is:

`AmpPortableDataViz.Tests.PlayMode.ArtisticElementsSceneTests.SavedScene_ControlsAllFiveImportedElementsAndRestoresAuthoredAppearance`

The failure is an unhandled Error log from `RenderPipelineManager.DoRenderLoop_Internal`, reporting attachment size `1600 x 900` versus pass size `1280 x 720`, both `1AA`. The log also contains follow-on BeginSubPass/EndRenderPass errors outside a render pass (`playmode-canonical.log:28446-28516`). This is a dimension mismatch; the logged sample counts agree. No failed application assertion is reported for this test in this run.

The relevant test path is:

- The test invokes `CapturePreview(panel)` at `ArtisticElementsSceneTests.cs:82`.
- `CapturePreview` creates a temporary `RenderTexture(1600, 900, 24)` at line 255, assigns `camera.targetTexture` at 260, changes the canvas to ScreenSpaceCamera, and allows two frames to render at 268-269.
- It reads 1600x900 pixels at 271 and writes the preview image. It relies on frame rendering; it does not explicitly call `Camera.Render` or `RenderPipeline.SubmitRenderRequest`.
- The `finally` block restores the original camera target/aspect, canvas settings, and active RenderTexture, then releases the temporary texture. This existing cleanup does not prevent the error while the temporary target is active.

Relevant checked-in URP settings:

| Setting | Evidence |
| --- | --- |
| Mobile render scale `0.8` | `Assets/Settings/Mobile_RPAsset.asset:29` |
| Mobile Native RenderPass enabled | `Assets/Settings/Mobile_Renderer.asset:31` |
| PC render scale `1` | `Assets/Settings/PC_RPAsset.asset:29` |
| PC Native RenderPass enabled | `Assets/Settings/PC_Renderer.asset:30` |
| Android default quality selects Mobile (index 0) | `ProjectSettings/QualitySettings.asset:120` |
| Saved current quality is PC (index 1) | `ProjectSettings/QualitySettings.asset:7` |

1280x720 equals 80% of the requested 1600x900 capture size, which is consistent with the Mobile asset's scale. It is also a common screen/backbuffer size. The test result did not record the effective runtime quality level and pipeline, so this correspondence alone does not prove which target or profile supplied the smaller attachment. The supported failure boundary is the test's temporary camera target interacting with URP Native RenderPass/target sizing.

Unity's related [UUM-58611 issue](https://issuetracker.unity.com/issues/18823) reports the same attachment-size and BeginSubPass/EndRenderPass symptom family with Native RenderPass and upscaling, and lists a fix in `2022.3.37f1`. This project uses `2022.3.36f1` with URP `14.0.11`. It is a concrete investigation lead, not proof of an identical defect: that issue calls SubmitRenderRequest, whereas this test assigns camera.targetTexture and renders through the normal frame loop. No editor/package upgrade or renderer-setting change was performed for this assessment.

Recommended follow-up is a small separate rendering investigation: record the effective pipeline, quality level, render scale, camera/attachment dimensions, and Native RenderPass state during the existing capture; reproduce this exact one-case failure; then evaluate a compatible preview-rendering approach or the documented engine correction with settings restored after any diagnostic variation. Preserve the real rendering error rather than accepting it through LogAssert or weakening the test. Class deletion does not address it.

The initial assessment's canonical result was 195/196, superseding the initial 192/196. The follow-up described above removes optional capture and records a 196/196 full-suite pass. Android build/device outcomes remain recorded separately in the main assessment.
