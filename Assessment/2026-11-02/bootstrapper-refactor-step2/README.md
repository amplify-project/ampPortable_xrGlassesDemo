# Bootstrapper refactor step 2: remove unused runtime fields

Completed on `cleanup/obsolete-code`, based on `b9b92bc`, 2 November 2026. The user authorized refactor step 2 and explicitly deferred step 3. This implements only the field-removal batch identified after the [execution-path review](../bootstrapper-refactor-step1/README.md).

Removed six fields that were assigned during runtime bookkeeping and never subsequently read in the checked project:

| Private nested runtime class | Removed fields | Retained state |
| --- | --- | --- |
| `AudienceDeviceInstance` | `DeviceId`, `PhysioPump` | Visual instance, binding, physio channel. |
| `AudienceGraphDeviceGroupInstance` | `DeviceId`, `DeviceIndex`, `PhysioPump` | Group root, binding, physio channel. |
| `SharedPhysioPumpHandle` | `ChannelName` | Host, port, pump, reference count. The dictionary key already identifies the channel. |

The six matching initializer assignments were removed in the same change. Two initializer trailing commas were adjusted after removing their final assignments. The initializer values were copies of existing fields, a local pump reference, an index, or a channel parameter; removing the copies introduces no new calls or changes to their evaluation order elsewhere.

These classes are private runtime records, not MonoBehaviours or serialized configuration classes. Their owning lists/dictionary are private runtime state without serialization attributes. No Inspector field, scene, prefab, asset metadata, package, or test was changed. Other actively used `DeviceId` and `ChannelName` fields, including channel definitions and serialized engagement overrides, remain.

Only [Bootstrapper.cs](../../../Assets/AmpPortableDataViz/Presentation/Bootstrap/Bootstrapper.cs) changed among project inputs. Its line count fell from 1,998 to 1,986. All 59 method declarations and 62 top-level configuration fields remain. Pump acquisition, sharing, reconnection, reference counting, release, and consumer binding still use their existing references/channels. Layout, labels, callbacks, session initialization, and mode selection are unchanged.

## Verification

- Read the three runtime records, their construction sites, and their collection consumers. Searches under `Assets` found no reads of the removed members, external references to these private types, or matching reflected access in the Bootstrapper tests.
- Verified the source diff contains exactly the six declarations and six assignments removed, with only the two required comma adjustments. Verified the remaining record declarations/initializers and unchanged method/configuration declarations.
- Fingerprinted all project inputs under `Assets`, `Packages`, and `ProjectSettings`: all **1,701 other files are byte-identical**. Bootstrapper's `.meta`, scenes, prefabs, all existing tests, and package files are included in that unchanged set. See [source-integrity.json](source-integrity.json).
- Synchronized the existing canonical isolated Unity project to the current 1,702 input files. Only Bootstrapper needed copying. Unity-managed caches and the user's open working Editor were left alone. Snapshot source hashes match the checkout after testing; see [validation-snapshot.json](validation-snapshot.json).

| Existing PlayMode run, Unity 2022.3.36f1 | Result | Evidence |
| --- | --- | --- |
| First full suite | **192 passed, 1 failed** on the previously recorded halo assertion | [full.xml](full.xml), [run record](full-run.json) |
| Failing halo test in isolation | **1 passed**, process exit 0 | [halo-focused.xml](halo-focused.xml), [run record](halo-focused-run.json) |
| One unchanged full-suite rerun | **193 passed, 0 failed, 0 skipped/inconclusive**, process exit 0 | [full-repeat.xml](full-repeat.xml), [run record](full-repeat-run.json) |

The intermittent case is `CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside`: negative active radius equalled neutral radius (`0.0800000057`) rather than being smaller. This exact assertion was recorded during earlier assessments. It passed in isolation and in the final full run. No assertion, halo implementation, fixture, or test selection was changed between the two full runs. This field cleanup does not fix the underlying intermittent issue. All results and log hashes are preserved in [validation-summary.json](validation-summary.json).

No new tests were written for removed bookkeeping fields, and no existing test was removed. The existing suite, source-diff checks, and input-integrity checks cover this focused change. An Android APK build and device test were not repeated because there are no serialized, platform-specific, dependency, or intended behavioral changes.

The [PlayMode runner](run_playmode.ps1) requires the prepared isolated project recorded in the snapshot. Use a new attempt name to preserve earlier evidence; optional `-TestFilter` runs a diagnostic subset. Git long-path configuration applies only to the launched process. Raw logs remain under ignored `Temp/BootstrapperRefactorStep2-20261102`; the XML results are preserved here. Test results establish only the behavior exercised; text searches do not rule out arbitrary external reflection or independently distributed code.

Stop boundary: **refactor step 3 has not started**. `FillAudienceChannelDefaults`, graph bounds/label helpers, duplicate Sine subscription behavior, lifecycle cleanup, and responsibility extraction are untouched. No remote push was performed.
