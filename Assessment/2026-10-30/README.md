# RayNeo pre-merge assessment

Read [RESULTS.md](RESULTS.md) for the current outcome. Optional screenshot generation has been removed at the user's request, and the latest full suite passes 196/196. A separate intermittent halo assertion observed during validation is recorded for review.

## Current outcome

- All 96 first-party scripts inventoried, with four individual cleanup candidates and separate connected legacy subsystems.
- Latest full PlayMode suite: **196 passed, 0 failed, 0 skipped/inconclusive**. The previous rendering failure is resolved; a prior intermittent halo failure is preserved.
- Main scene/prefab inspection: **zero missing scripts, unresolved-reference warnings, or inspection blockers**.
- Fresh script probe: **13/13 expected MonoBehaviour types match**.
- Android build: **succeeded, zero errors, two warnings**. APK verified, installed in place, checksum matched, and cold-launched through the RayNeo XR activity.
- User reports: **“I tested the app on the glasses. Everything is working as expected.”**
- The user-authorized change affects **one Editor test file**; its 55 assertion lines and metadata are preserved. The other **1,816 source input files and 444 external SDK files are unchanged**. The two pre-existing modified URP settings assets remain intact.

The source snapshot is `rayNeoPort` at `60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19` plus those uncommitted URP changes and the subsequent screenshot-removal test edit. Runtime/device results predate that test-only edit and remain applicable. No branch switch, staging, commit, asset-file deletion, merge, tag, or push occurred.

## Evidence

| Artifact | Purpose |
| --- | --- |
| `RESULTS.md` | Current assessment result and next steps |
| `code-inventory.csv`, `scan-summary.json` | Reproducible inventory of all 96 first-party scripts |
| `legacy-code-review.md`, `runtime-code-review.md` | Usage evidence and coordinated cleanup groups |
| `configuration-review.md` | Android/XR configuration, test limits, device checklist, stale docs |
| `playmode-results.xml`, `screenshot-removal-validation.json` | Passing full-suite result and all follow-up test attempts |
| `playmode-before-preview-removal.xml`, `failure-analysis.md` | Preserved original rendering failure and diagnosis |
| `screenshot-removal-integrity.json` | Only the intended test changed; assertions, metadata, other inputs and historical preview preserved |
| `build-summary.json`, `scene-inspection.json` | Successful build and enabled-scene inspection |
| `apk-artifact.json`, `apk-signatures.json`, `device-results.json` | APK verification, signing match, device launch, wearer acceptance |
| `baseline.json`, `source-integrity.json`, `initial-working-tree.patch` | Input capture, final source verification, preserved original changes |
| `external-sdk.json`, `external-sdk-manifest.json` | Read-only fingerprint of RayNeo SDK 1.1.2 |
| `snapshot-drift-final.json` | Unity import migration and Editor helpers only in the isolated copy |
| `gradle-tls-check.json` | Successful actual Gradle preflight using temporary public certificates |

Raw logs, APKs, the previous installed APK backup, and device capture are under ignored `Temp/PreMergeAssessment-20261030`. The full source hash manifest is there too; its SHA-256 is recorded in `baseline.json`.

## Validation tooling

`capture_baseline.py` copies and fingerprints inputs and verifies source/SDK integrity. `inventory_code.py` regenerates lexical reference evidence and reviewed classifications; it is not a deletion tool or semantic C# analyzer. `run_validation.ps1` runs Unity 2022.3.36f1 only against the isolated snapshot. The two C# helpers are templates outside source Assets; they were copied only into the isolated project's Assets/Editor.

The current snapshot location is recorded in `canonical-path-project.json` and `short-path-project.json`. Use its canonical Windows path. Earlier DOS8.3-path imports produced false missing-script/type-association results; historical artifacts are preserved separately and superseded by the fresh canonical import. No asset/script repairs were needed.

Unity changed PC_Renderer.asset automatically in the copy. Build processors temporarily moved simulation assets and restored them. No generated Unity cache was hand-edited, and no isolated-project changes were copied back.

## Reproduction on this machine

The capture and temporary trust store already exist. These commands verify the source and rerun against the existing isolated copy; choose fresh attempt names to preserve earlier logs:

```powershell
# Original baseline hashes predate the authorized test edit; verify reports that one expected difference.
python Assessment/2026-10-30/capture_baseline.py verify
python Assessment/2026-10-30/capture_baseline.py fingerprint-sdk
powershell -NoProfile -File Assessment/2026-10-30/run_validation.ps1 -Stage PlayMode -Attempt playmode-next -EnableGitLongPaths
powershell -NoProfile -File Assessment/2026-10-30/run_validation.ps1 -Stage Build -Attempt android-next -EnableGitLongPaths -UseWindowsTrustStore
```

Git long-path support and Java trust options are process-local. This machine's Gradle required a temporary PKCS12 copy of its existing Windows public root certificates; no private keys were exported and no system trust store, global Git/Java setting, TLS verification, or dependency manifest was changed. The store is outside the source project and is not committed. Unity overrides JAVA_TOOL_OPTIONS, so the runner uses Java 11 JDK_JAVA_OPTIONS.

Capture commands refuse overwrite. On a fresh run, capture before validation and make a canonical-path temporary copy; do not reuse the earlier DOS alias Library. Full-suite runs keep graphics available, run without filters, and save fresh XML. Screenshot generation has been removed; isolation still keeps Unity imports and test artifacts away from the source project.

User device acceptance is recorded in `device-results.json`. Initial logs showed a saved Redis endpoint timeout; real publisher payloads were not independently captured. This limitation does not replace or contradict the user's reported successful on-device test.

Review the recorded intermittent halo assertion and the two uncommitted URP changes before merge. Broad obsolete-code removal remains a separate later task.
