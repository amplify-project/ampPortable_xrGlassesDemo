# Bootstrapper refactor step 4 — Sine retirement and lifecycle cleanup

## Scope and result

The user explicitly retired Sine mode and authorized initialization/cleanup work
through **step 4 only**. Sine retirement was committed separately as `a269440` after
13 existing Bootstrapper tests passed. The second chunk fixes session lifecycle
regressions and consolidates repeated cleanup inside Bootstrapper.

No responsibilities were extracted into other classes or layers. Step 5 has not
started. Package manifests, dependency locks, visualizers, manual drivers, shared
session infrastructure, and input components are unchanged. No push, APK install,
or on-device action was performed.

## Supported behavior

- `SineWaveSource.cs` and its matching `.meta` are removed, together with its
  Bootstrapper field, enum entry, source creation, enable/disable paths, and both
  subscriptions. See [the first chunk](sine-retirement.md).
- Redis keeps serialized enum value `1` and becomes the default. The retired value
  `0` cannot initialize a session; it is not silently reassigned to Redis.
- The saved app scene changes by exactly one line: removal of its null
  `SineSource` field. All retained serialized values and object references match.
- The optional primary Redis session, selected-sensor and per-device automatic
  views, ArtisticElements, and manual visualization drivers remain supported.
- Repeated endpoint confirmation, including another confirmation during startup,
  keeps one primary session and one Redis frame subscription. Reconfiguration
  still updates the existing pump's endpoint/channel before checking startup.
- Primary session startup now requires a nonempty Redis host and, when enabled,
  endpoint confirmation. Switching to manual or automatic-view mode stops the
  primary session and detaches its subscriptions before clearing its view state.
- Disable resets the startup guard; re-enable can start a fresh session. Editor
  Play Mode configuration changes also evaluate guarded startup after applying
  configuration, provided the Bootstrapper is active and enabled.

## Cleanup order

The new helpers are private methods in Bootstrapper. Their sequence preserves the
existing ordered cleanup operations rather than moving ownership between layers.

`OnDisable`: disable the primary Redis component; stop initialization and discovery
coroutines; shut down the session; clear per-device particle views, graph groups,
selected streams/targets/HUD, the engagement backend, then manual instances.

`OnDestroy`: stop pending coroutines; dispose and release the controller; reset
startup state; repeat the same ordered view cleanup. Repeated cleanup is safe.

Manual configuration stops the primary session, disables its Redis source, clears
automatic views in that same order, then ensures manual instances. Existing manual
instances are reused when configuration is applied repeatedly. Selected-stream
configuration still clears per-device views before building selected streams.

The individual `Clear*` methods, selected-stream disconnection sequence, shared
pump reference counting, and layout/label calculations are unchanged. Stopping a
discovery coroutine retains the existing behavior of not cancelling its underlying
discovery task. Shared session/factory/anchor object ownership is unchanged.

## Validation evidence

| Run | Result |
| --- | --- |
| Sine retirement, existing Bootstrapper tests | **13/13**, exit 0 |
| Initial lifecycle baseline | 2/7; four runtime failures plus one incomplete graph test fixture |
| Corrected lifecycle baseline, runtime unchanged | 3/7; four targeted runtime failures |
| Lifecycle implementation, same corrected tests | **7/7**, exit 0 |
| Full suite with confirmations during startup and shared-pump checks | **209/209**, exit 0 |
| Final full suite with tightened disable/re-enable sequencing and manual cleanup coverage | **210/210**, exit 0 |
| Android IL2CPP APK build | **Succeeded**, 0 errors, 20 unchanged warnings |
| Saved-scene inspection | **0 missing scripts, 0 broken references, 0 blockers** |

The four demonstrated runtime failures were duplicate session initialization on
confirmation, startup before confirmation, surviving subscriptions after entering
manual mode, and accepting the retired source value for session initialization.
The initial graph fixture lacked the required `GraphVisualizer`; adding it resolved
that test setup issue before changing the lifecycle implementation. All failed and
passing run records are retained.

The final suite retains all 202 prior cases and adds eight lifecycle tests.
Coverage includes primary Redis frame delivery exactly once, subscription release
on actual component disable/destroy, restart on enable, primary-to-manual and
automatic mode transitions, selected-stream rebuilding/cleanup, manual instance
reuse and cleanup, and two per-device views sharing one pump per sensor.

Tests keep real transport objects inactive, inject recording session adapters for
the primary path, and restore runtime settings/PlayerPrefs. Selected/manual tests
invoke lifecycle callbacks on an inactive hierarchy so they need no Redis server.
They verify orchestration and owned automatic/manual resources; they do not test
real Redis reconnect timing, XR hardware, or generic session-factory object cleanup.

The build used the same runtime/scene source as the final suite. Only lifecycle
test coverage changed after the build; no runtime change required a second APK.
All 20 build warning messages exactly match the previous cleanup build. Inspection
covered 248 scene components, 8 prefabs, and 17 materials. Raw logs and the APK are
under ignored `Temp/BootstrapperRefactorStep4-20261103/` paths.

## Scope verification and reproduction

`source-integrity.json` verifies that **1,699 other original input files** under
Assets/Packages/ProjectSettings are byte-identical to the baseline. The runtime
changes affect only Bootstrapper and deletion of the Sine source. The saved scene
loses only the null field; the existing graph test loses only its Sine setup and
assertion. One new test script and matching metadata are added.

Bootstrapper changes from 1,946 to 1,907 lines. Forty-five existing method bodies
are unchanged, including every individual cleanup operation and pump release.
Twelve existing methods change, and five private cleanup helpers are added.

Use `python verify_source.py` from this directory to reproduce the scope checks.
`sync_snapshot.py <unique-phase>` verifies/synchronizes source inputs into the idle
isolated project recorded in the snapshot records, never generated caches. Run
`run_validation.ps1 -Stage PlayMode -Attempt <unique-name>` there, or pass
`-TestFilter BootstrapperLifecycleTests` for focused tests. Android build validation
uses `-Stage Build -UseWindowsTrustStore` with the existing local public-certificate
store; its Java/Git configuration is process-local.

No visual change is intended in the active Redis app. A later device check can
confirm startup, sensor/view cycling, and quit/relaunch with live Redis data; the
previously installed APK was not replaced during this step.
