# Resolve the intermittent corner-halo test failure

## Confirmed cause

This is a frame-count assumption in the automated test. The renderer smooths a
new heart-rate signal over elapsed time and intentionally treats small values as
neutral. The old test measured rendered geometry after two frames, regardless of
how much time those frames represented.

The unchanged original direction/synchronization check was run under controlled
Unity frame durations before applying the fix:

| Duration per frame | Smoothed signals after two updates | Neutral band | Original result |
| --- | --- | --- | --- |
| 1 ms | approximately −0.0119283 / +0.0119283 | ±0.06 | **Failed**, active/reference radii both approximately 0.08 |
| 1/60 second | approximately −0.181269 / +0.181269 | ±0.06 | **Passed** |

The first case reproduces the exact historically recorded failure: the negative
active radius equals the neutral reference radius. At that moment both signals
are inside the intentional neutral band, so equal radii are the correct output.

The observations match the actual smoothing equation. With smoothing rate 6/s,
a unit step from zero gives an absolute response `1 − exp(−6 × elapsedSeconds)`.
It takes approximately **10.31 ms** to cross the default 0.06 neutral boundary.
Two 1 ms frames cannot satisfy that precondition; two 60 Hz frames can.

`causal-analysis.json` records measured inputs, calculated responses, and the
original failing assertion. `reproduction-before.xml` and `reproduction-source.json`
preserve the failing controlled run and its exact test source. The source check
verifies that the reproduced original case was unchanged from the baseline.

## Fix and regression coverage

The existing test identity remains. Its geometry check now waits for the negative
and positive smoothed responses to have the correct direction and at least 0.1
response magnitude beyond the neutral band, with a **five-second realtime timeout**.
It then performs the original strict inside/outside radius comparisons and all
four-corner synchronization checks, with their original values and tolerances.

The wait checks signal-response state independently of halo geometry. Incorrect
radii still fail the original assertions; missing/stalled signal updates fail the
timeout. The geometry check is shared by the original case and the new controlled
1 ms and 60 Hz frame-duration cases. Unity's previous capture duration is restored
in `finally`, including assertion failures. The readiness iterator is advanced
inside the shared check so failures unwind through that cleanup path.

No rendering settings, signal smoothing, runtime code, scene/prefab assets or
metadata were changed. The brief neutral phase immediately after a signal change
is the intended smoothing behavior and remains intact.

## Results

| Run | Result |
| --- | --- |
| Controlled reproduction before fix | **1 passed, 1 failed**; 1 ms case reproduces the historical failure |
| Fixed orthogonality/halo group | **11/11 passed**, Unity exit 0 |
| Final full PlayMode suite | **208/208 passed**, Unity exit 0 |

All **206 previous test identities** remain, and two frame-duration regressions
are added. No tests are ignored, no geometry assertions are relaxed, and no passing
full run replaces or removes the original failure records.

The latest successful Android build at `1c68388` still covers the unchanged runtime
and assets. A new APK build/install is unnecessary for this test-only change; no
on-device test is claimed. Previously retained failure records, including
`Assessment/2026-11-03/bootstrapper-session-retirement/full.xml`, remain unchanged.

## Scope and reproduction

`verify_source.py` confirms that the sole changed source input is
`Assets/Tests/PlayMode/ParticleMeshVisualizerOrthogonalityTests.cs`; all **1,688 other
original input files** are byte-identical. It also verifies the original signal
samples, geometry assertions, tolerances, cleanup and other original test/helper
bodies are preserved.

Run `python verify_source.py` for scope checks. `sync_snapshot.py <phase>` verifies
the source inputs copied into the idle isolated Unity project recorded in the
snapshot records. `run_validation.ps1 -Stage PlayMode -Attempt <unique-name>
-TestFilter ParticleMeshVisualizerOrthogonalityTests` runs the focused group; omit
the filter for the full suite. Raw logs are under ignored
`Temp/HaloTimingFix-20261103/` paths.
