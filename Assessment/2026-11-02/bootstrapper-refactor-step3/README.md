# Bootstrapper refactor step 3: remove redundant helpers

Completed on `cleanup/obsolete-code`, based on `de70e30`, 2 November 2026. The user authorized removing redundant channel defaulting and obsolete graph-label offset generality, and explicitly deferred refactor step 4.

## Changes

### Channel definitions

Removed `FillAudienceChannelDefaults()` and its call from `BuildAudienceDeviceChannelDefinitions()`. Definitions enter the dictionary only after successful physio-channel parsing and assignment of the nonempty input channel. The later ordered-list pass therefore always receives a populated channel; it never needs to synthesize a default.

Two focused tests were added and passed against the original implementation before removing the helper:

- Parsed channels preserve their exact input spelling, sensor insertion order, and last-channel replacement for duplicate sensor IDs. Mixed-case and internally padded input names distinguish retained input channels from synthesized canonical defaults.
- Empty, whitespace, malformed, and obsolete/non-physio channels do not create definitions. The empty input collection also produces no definitions.

The same tests pass after removal. Parsing, filtering, ordering, duplicate handling, and configured-channel behavior remain unchanged.

### Graph-label placement

The single production caller of `AttachGraphGroupLabel()` always supplied `Array.Empty<Vector3>()`. Removed the `channelOffsets` parameter and argument, deleted `TryCalculateGraphGroupBounds()`, and replaced its empty-collection case with the existing bounds calculation directly in label placement. The unused loop over multiple channel offsets is gone.

The preserved positioning rules are:

- Clamp the extra vertical offset with `Mathf.Max(0f, GraphGroupLabelVerticalOffset)`.
- If `GraphItemBoundsSize.sqrMagnitude < 1e-6f`, use `GraphGroupLabelOffset` plus the clamped vertical offset. Padding is ignored in this small/zero-bounds fallback.
- Otherwise use `GraphItemBoundsSize + GraphGroupBoundsPadding * 2f`; place the label at half its Y size plus the clamped offset, then add `GraphGroupLabelOffset`. The center remains zero because the former offsets collection was always empty.
- Preserve signed height, X/Z offsets, label text/style, parent, layer, and BillboardLabel component. No configuration field was removed.

Seven label cases passed before and after: default bounds, custom bounds/padding, negative vertical-offset clamping, zero bounds, tiny bounds, the size threshold, and signed height. Their assertions and input/expected data are unchanged. The only test-helper adaptation removes the empty-array argument when invoking the new private signature.

## Validation

| Check | Result | Evidence |
| --- | --- | --- |
| Focused tests before runtime edits | **9 passed**, process exit 0; original Bootstrapper hash matches the baseline | [focused-before.xml](focused-before.xml), [run record](focused-before-run.json) |
| Same focused cases after simplification | **9 passed**, process exit 0 | [focused-after.xml](focused-after.xml), [run record](focused-after-run.json) |
| Final full PlayMode suite | **202 passed, 0 failed, 0 skipped/inconclusive**, process exit 0 | [full.xml](full.xml), [run record](full-run.json) |
| Test preservation | All 193 previous cases retained; nine new cases; no old test or assertion edited | [validation-summary.json](validation-summary.json) |
| Source scope | Only Bootstrapper and the new test pair changed among project inputs; all 1,701 other files remain byte-identical | [source-integrity.json](source-integrity.json) |

Source: [Bootstrapper.cs](../../../Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs). Tests: [BootstrapperChannelAndLabelTests.cs](../../../Assets/Tests/PlayMode/BootstrapperChannelAndLabelTests.cs). Its `.meta` was added with the new script. Existing scenes, prefabs, metadata, packages, visualizers, and all prior tests remain unchanged.

Bootstrapper decreased from 1,986 to 1,946 lines. Two private methods were removed. Only three retained methods changed: the definition builder, the graph-label creator, and the graph creator that calls it. The other 54 method bodies are byte-identical after normalizing line endings. All 62 top-level configuration declarations remain unchanged. Exact changed-body checks and file hashes are in the integrity record.

The [baseline](baseline.json) and [focused-test baseline](focused-test-baseline.json) preserve source/test evidence before runtime editing. [snapshot-before.json](snapshot-before.json) and [snapshot-after.json](snapshot-after.json) record each isolated validation phase. The final temporary project matches all 1,704 current source input files, plus existing assessment-only Editor helpers. No batch validation ran in the user's working Editor project, and no Unity-managed cache was hand-edited.

The [runner](run_playmode.ps1) uses Unity 2022.3.36f1 and the prepared project in [validation-snapshot.json](validation-snapshot.json). Use unique attempt names to preserve evidence; `-TestFilter` selects a diagnostic subset. Raw logs stay under ignored `Temp/BootstrapperRefactorStep3-20261102`; their hashes and results are recorded in the summary. There were no failed runs or retries in this step. The previously recorded intermittent halo assertion did not recur here and is not considered fixed.

No Android APK build or device action was performed for these behavior-preserving private-helper changes. No visual change is intended. For an Editor comparison of the affected alternate path, inspect per-device graph labels using the existing offsets/bounds, including the small/zero-size fallback; the automated cases verify those positions numerically. The saved app's selected-stream settings are unchanged.

## Stop boundary

**Refactor step 4 has not started.** No initialization order, duplicate Sine subscription, lifecycle cleanup, mode support, architecture, serialization, dependency, or responsibility-extraction changes were made. No remote push was performed.
