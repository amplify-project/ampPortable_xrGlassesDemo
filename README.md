# XR Score Viewer

XR Score Viewer is a Unity proof-of-concept for displaying live audience physiology and engagement on **RayNeo X3 Pro** glasses. It combines a score/image board, an abstract particle mesh, and five time-series graphs. These displays support performance and experimentation with graphical scores.

The current app shows **one selected sensor at a time**. Redis carries live data from an external acquisition/analysis system; the glasses display that data. A fourth view, ArtistViz, is currently a placeholder.

## Contents

- [Dependencies](#dependencies)
- [Build from source](#build-from-source)
- [Install on RayNeo X3 Pro](#install-on-rayneo-x3-pro)
- [End-user guide](#end-user-guide)
- [Live-data setup for operators](#live-data-setup-for-operators)
- [Troubleshooting](#troubleshooting)
- [Development and tests](#development-and-tests)
- [Further documentation](#further-documentation)

**Already have an APK?** Go directly to [installation](#install-on-rayneo-x3-pro), then follow the [end-user guide](#end-user-guide). Unity and the RayNeo SDK are needed to build the app; an installed copy runs without those tools.

## Dependencies

### Hardware and services

| Requirement | Purpose |
| --- | --- |
| RayNeo X3 Pro with compatible Glass OS | Runs the Android XR application. Match the Glass OS and RayNeo ARDK versions when preparing a build. |
| Computer and a data-capable USB-C cable | Builds and/or sideloads the APK through Android Debug Bridge (ADB). |
| Network connection between the glasses and Redis host | Carries live sensor data. A shared local Wi-Fi network is the simplest setup. |
| Redis server and an external sensor-data publisher | Supplies physiology and engagement. This repository does not provision Redis or implement the upstream acquisition/analysis system. |

The connection UI accepts a Redis host and port. It does not expose Redis password/ACL credentials or TLS settings, so the operator must provide a compatible network endpoint. DNS-SD discovery is optional; direct host/port entry is also supported.

### Build tools and packages

Use **Unity 2022.3.36f1** with **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK**, installed through Unity Hub. Use the tools bundled with this Editor version; see [Unity's Android environment setup](https://docs.unity3d.com/2022.3/Documentation/Manual/android-sdksetup.html).

| Dependency | Project version/source | Role |
| --- | --- | --- |
| RayNeo OpenXR Unity ARDK | **1.1.2**, bundled in `Packages/com.unity.xr.rayneo.openxr-1.1.2.tgz` | RayNeo runtime, controller input, and OpenXR integration. |
| OpenXR Plugin | `1.7.0` | XR loader and feature configuration. |
| XR Interaction Toolkit | `2.5.4` | XR interaction support. |
| AR Foundation | `5.1.6` | AR framework support. |
| Universal Render Pipeline (URP) | `14.0.11` | Rendering. |
| Input System | `1.18.0` | Input infrastructure. |
| TextMeshPro / Unity UI | `3.0.6` / `1.0.0` | Labels and connection UI. |
| Newtonsoft JSON | `3.2.1` | Redis JSON decoding. |
| NuGetForUnity | Git dependency | Restores the .NET dependencies. |
| StackExchange.Redis | NuGet `3.0.0` | Redis client. |
| System.Text.Json | NuGet `9.0.10` | JSON support. |
| Unity Test Framework | `1.4.6` | Automated tests. |

[Packages/manifest.json](Packages/manifest.json) and [Packages/packages-lock.json](Packages/packages-lock.json) define the Unity dependencies and resolved Git revisions. [Assets/packages.config](Assets/packages.config) defines the NuGet packages and their supporting dependencies. Git must be available for package restoration; allow network access to the configured Unity, GitHub, and NuGet sources during setup.

Other packages remain declared, including USD and the LiveKit Unity SDK. **LiveKit's application integration is retired**: no LiveKit server, account, or room is needed to use this app. Retain the existing dependency declarations when reproducing this baseline.

### RayNeo SDK and Glass OS

The matching **RayNeo OpenXR Unity ARDK 1.1.2** is bundled in this repository; no separate SDK download or extraction is needed to build. See [the package notes](Packages/RayNeo-ARDK.md) for provenance and archive verification. For upstream releases, use [RayNeo's SDK downloads](https://leiniao-ibg.feishu.cn/wiki/IDFfwLKR4iihBuknkVrcGiMUnic), linked by [Qualcomm's X3 Pro development walkthrough](https://www.qualcomm.com/developer/project/get-started-with-rayneo-x3-pro-ar-development). Confirm compatibility for the glasses being used before changing their OS or SDK; an existing working device does not need an OS upgrade just to follow this guide.

The [public RayNeo SDK repository](https://github.com/github-for-rayneo/OpenXR-Unity-ARDK) is a useful reference, but check its package version before using it. Its public `main` package was `1.0.1-r3` when checked for this documentation and differs from the assessed `1.1.2` package.

## Build from source

Skip this section if an operator has already supplied the APK you intend to install. APKs and Unity build output are ignored by Git.

### 1. Get the project

```powershell
git clone --branch main https://github.com/amplify-project/ampPortable_xrGlassesDemo.git
cd ampPortable_xrGlassesDemo
```

Build from `main`, which contains the current RayNeo baseline. The older XREAL Air 2 Ultra / Beam Pro project using Unity **6000.3.10f1** is preserved at the annotated tag `xreal-unity6-pre-rayneo`; that version has a different device setup.

The RayNeo ARDK is included in the checkout. The manifest references its archive using a path relative to `Packages/`:

```json
"com.unity.xr.rayneo.openxr": "file:com.unity.xr.rayneo.openxr-1.1.2.tgz"
```

Unity restores this package from the archive on each build machine. No manifest edits, user-specific SDK folders, or RayNeo Git credentials are required. This follows [Unity's local tarball dependency format](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-localpath.html). Keep the manifest and archive together when copying the project. The snippet shows one dependency entry, not a complete replacement manifest. Let Unity resolve `Packages/packages-lock.json`; do not hand-edit it or upgrade packages as part of initial setup.

### 2. Open and import

1. Add the repository root to Unity Hub and open it with **2022.3.36f1**.
2. Allow package resolution, asset import, and script compilation to finish. Resolve Console errors before building.
3. In **Edit > Preferences > External Tools**, verify that Android SDK, NDK, and JDK use the Editor-installed copies.
4. If .NET packages are missing, use **NuGet > Restore Packages** to restore the versions in `Assets/packages.config`.
5. Open `Assets/Scenes/ampPortable_xrGlassesDemo.unity`.

### 3. Verify the Android and XR settings

In **File > Build Settings**, select **Android** and choose **Switch Platform** if required. Confirm that **only** `Assets/Scenes/ampPortable_xrGlassesDemo.unity` is enabled under **Scenes In Build**.

The checked-in configuration is:

| Setting | Expected value |
| --- | --- |
| Product name | `XR Score Viewer` |
| Android application identifier | `com.TUS.RayNeoXRScoreViewer` |
| Minimum / target Android API | **30 / 30** |
| Scripting backend / architecture | **IL2CPP / ARM64** |
| Graphics API | **OpenGL ES 3** |
| Active Input Handling | **Both** |
| Android XR loader | **OpenXR**, under XR Plug-in Management |
| OpenXR features | **RayNeo Support** and **RayNeo Controller Profile** enabled |
| RayNeo camera attitude | **DOF3** |

Check **Edit > Project Settings > XR Plug-in Management > OpenXR** for Android validation issues, and recheck the settings above after applying fixes. These settings describe the existing sideloaded app baseline.

The project already includes [Assets/Plugins/Android/AndroidManifest.xml](Assets/Plugins/Android/AndroidManifest.xml), with the RayNeo launch activity `com.rayneo.openxradapter.UnityOpenXrActivity`, metadata `com.rayneo.mercury.app=true`, and internet/network/Wi-Fi/multicast permissions. Keep this configuration when building for the glasses.

### 4. Create the APK

1. In **File > Build Settings**, leave **Build App Bundle (Google Play)** disabled.
2. Enable **Development Build** when development diagnostics are needed.
3. Choose **Build** and save the APK as, for example, `Builds/RayNeo_XR_Score_Viewer.apk`.
4. Follow the installation steps below. Alternatively, connect and authorize the glasses first, select them as the run device, and choose **Build And Run**.

## Install on RayNeo X3 Pro

### 1. Prepare ADB and the glasses

1. Obtain the APK from your operator or build it using the steps above.
2. Install [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools), or use Unity's bundled ADB at `C:\Program Files\Unity\Hub\Editor\2022.3.36f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`. The commands below assume `adb` is on `PATH`; otherwise run them from the Platform Tools directory using `./adb.exe` in PowerShell.
3. On the glasses, enable developer mode and ADB/USB debugging using [RayNeo's ADB guide](https://leiniao-ibg.feishu.cn/wiki/WzQ4w5SIuip8qMk3WddcHCvynZe) for your Glass OS version. The guide may require sign-in. Developer-mode activation varies with Glass OS; use the device-specific instructions.
4. Connect the glasses to the computer with a data-capable USB-C cable. Accept the debugging authorization prompt if one appears.
5. Check the connection:

```powershell
adb devices
```

The glasses should be listed with status **`device`**. `unauthorized` means authorization is still required. If more than one Android device is connected, select the glasses with `adb -s DEVICE_SERIAL ...` for each command. See the [ADB documentation](https://developer.android.com/tools/adb) for connection and authorization details.

### 2. Install or update

From the repository root, install the example build with:

```powershell
adb install -r ".\Builds\RayNeo_XR_Score_Viewer.apk"
```

For a supplied APK stored elsewhere, replace the example path with its actual location. Wait for **`Success`**. The `-r` option replaces an existing installation while retaining its app data when the signing keys match. A supplied APK can be installed without Unity or the SDK on the installation computer.

### 3. Launch

Open **XR Score Viewer** from the glasses' application list, or launch it through ADB:

```powershell
adb shell am start -n com.TUS.RayNeoXRScoreViewer/com.rayneo.openxradapter.UnityOpenXrActivity
```

Keep the glasses connected to the network that can reach Redis. The USB connection is used for installation and diagnostics; it does not carry the app's live data by default.

## End-user guide

### Before a session

Ask the operator to start Redis and the sensor-data publishers **before launching the app**. Connect the glasses to the intended Wi-Fi network, and have the Redis server's host/IP address and port available if automatic discovery is not configured.

### Connect to live data

1. Launch **XR Score Viewer**. The initial visualization is the score/image board.
2. Allow a few seconds for startup. The app checks a previously saved Redis endpoint and searches for a DNS-SD service of type `_amplify-redis._tcp`.
3. When it reaches a server, it saves the endpoint and dismisses the connection panel automatically.
4. If the panel remains, enter the server address in **Host**, enter its port in **Port** (usually `6379`), and select **Confirm**. Use the server's address on the network; `localhost` or `127.0.0.1` refers to the glasses themselves.
5. Wait for sensor discovery. A HUD shows **`SENSOR n / total`** and the selected sensor's identifier. Check that the identifier is the intended participant/device.

The RayNeo UI uses a head-centred pointer: look toward a field or button and tap to select it. Text entry depends on the Glass OS/SDK input available on your device. The current scene disables the app's explicit touchscreen-keyboard bridge; if selecting a field provides no usable keyboard, have the operator configure automatic discovery or prepare the endpoint before the session.

If confirmation fails, the panel stays open with an error so the address can be corrected. After a successful connection, later launches can reuse the saved endpoint.

### Controls

Use the **right-temple touchpad**:

| Gesture | Action |
| --- | --- |
| Single tap with one finger | Advance to the next visualization; wraps to the first after the fourth. |
| Double tap with one finger | Select the next discovered sensor; wraps through the list. |
| Look at the visible visualization and swipe forward | Make that visualization smaller. |
| Look at the visible visualization and swipe backward | Make that visualization larger. |
| Swipe up | Raise the active visualization. |
| Swipe down | Lower the active visualization. |
| Tap once with two fingers together | Quit the app. |

Forward/backward refers to movement along the temple touchpad. Scaling uses the direction your head is facing, rather than eye tracking, and requires the visible visualization to be targeted. Vertical movement applies to the active view. Size and height have limits, so repeated swipes eventually stop changing them.

A one-finger double tap changes the sensor; the quit gesture uses two fingers together. With only one discovered sensor, cycling leaves that sensor selected. The sensor selection feeds the live mesh and graph views together; the static score board does not change with the sensor.

### Read the views

Single taps cycle through this order:

| View | What to expect |
| --- | --- |
| **Score/image board** | A bundled score image. There is currently no in-app image chooser; replacing the score requires preparing another build. |
| **Abstract particle mesh** | Physiology and engagement expressed through shape, colour, motion, particles, and halos. |
| **Objective graphs** | Five labelled traces: Tonic EDA, Temp ROC, SCR Freq, Heart Rate, and Engagement. |
| **ArtistViz** | A reserved placeholder. An empty fourth view is expected; tap again to return to the score board. |

The abstract mesh uses these visual cues:

| Signal | Visual cue |
| --- | --- |
| Tonic electrodermal activity (EDA) | Slow overall shape: positive values compress/lift the mesh; negative values expand/relax it. |
| Temperature rate of change | Warm/cool particle colours and directional trails or travelling waves. |
| Skin conductance response (SCR) frequency | Local rings, sparks, and short deformations. |
| Heart rate | Particle size, synchronized pulses, and four corner halos. Positive values place active halos outside the neutral references; negative values place them inside. |
| Engagement | Dispersion and coherence: lower values scatter the particles; higher values create a more coherent, stable mesh. |

Physiology is received as **signed z-scores**: zero is the upstream baseline, positive is above it, and negative is below it. A positive value does not by itself mean the value is currently rising. Graph labels such as Heart Rate display standardized values, rather than raw beats per minute. Engagement uses a **0–1** range.

Graphs show approximately **10 seconds** of history, with smoothing and automatically adjusted vertical ranges. Read their values and labels when comparing signals; equal trace heights need not mean equal magnitudes. Changing sensors resets graph history so traces from different sensors are not combined. Returning to the graph view after viewing another visualization also starts a new history window.

For a fuller explanation of the mesh, see the [Abstract Mapping Guide](AbstractMapping.md).

### Missing data and ending a session

- **`NO SENSOR STREAMS`** means no sensors are available to select. Ask the operator to check the publishers, then relaunch after data is flowing.
- **Neutral engagement** can mean missing or unconfirmed data: the app starts at `0.5` and returns to that value after about **10 seconds** without accepted engagement updates.
- **Fading active heart-rate halos** can indicate stale heart-rate data; the neutral reference rings remain.
- To end the session, tap the right temple once with two fingers together. Relaunch from the application list when needed.

## Live-data setup for operators

### Network and discovery

Make Redis reachable from the glasses at the configured host and port. Check server binding, routing, firewall rules, and Wi-Fi client isolation. A working computer-to-Redis connection alone does not establish that the glasses can connect.

For automatic endpoint discovery, advertise the Redis endpoint as DNS-SD service type **`_amplify-redis._tcp`**. The current scene has no required TXT-record filters. Multicast/service discovery must work on the local network; manual host/port entry does not depend on that advertisement.

After connecting, the app listens for sensor Pub/Sub messages for about **2 seconds**. It discovers IDs from `device:*:physio_metrics` and retained compatibility channels `device:*:valence_cont`, `device:*:arousal_cont`, and `device:*:hr_filtered`. Creating Redis keys is insufficient: publishers must emit messages during discovery. The current scene has no preset sensor IDs and no continuous rescan UI. Start publishers before launch, and relaunch when adding sensors after discovery.

### Message channels and payloads

Publish physiology to **`device:{id}:physio_metrics`**. For example, on `device:DEMO-001:physio_metrics`:

```json
{
  "eda_z": 0.6,
  "temperature_roc_z": -0.4,
  "scr_frequency_z": 0.8,
  "hr_z": 0.3
}
```

These are the current decoder's z-score fields, clamped to `[-3, 3]`. Missing metrics default to zero; supply all four for a complete display. `ibi_z` can also be decoded, but inter-beat interval, arousal, and valence are not displayed by the current mesh or five-graph view.

Publish engagement to **`device:{serial}:engagement`**, using the same sensor identity as physiology. For example, on `device:DEMO-001:engagement`:

```json
{
  "device": "DEMO-001",
  "engagement": 0.75,
  "confirmed": true
}
```

Engagement must be confirmed and match a known sensor. Accepted scores are clamped to `[0, 1]`. An optional `timestamp` is numeric Unix time in seconds or milliseconds; use advancing timestamps for newer observations. Engagement-only publishing does not supply the physiology-channel traffic used for sensor discovery. Keep publishing throughout the session; Redis Pub/Sub does not replay old samples to newly subscribed viewers.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Unity cannot resolve the RayNeo package | Confirm `Packages/com.unity.xr.rayneo.openxr-1.1.2.tgz` is present and the manifest references `file:com.unity.xr.rayneo.openxr-1.1.2.tgz`. Restore both from the same revision; no external SDK path is needed. |
| Package restoration or compilation fails | Check Git/network access, NuGet restoration, and Unity version. For Windows path-length errors during Git package checkout, enable long-path support for the Git process used by Unity, then retry. |
| `adb` is not recognized | Add Platform Tools to `PATH`, or run `./adb.exe` from its directory in PowerShell. |
| ADB lists no glasses or says `unauthorized` | Check the data cable, developer/ADB mode, USB debugging, and authorization on the glasses. |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | The installed app and new APK may use different signing keys. Obtain a matching build, or deliberately uninstall the old copy before reinstalling. Uninstalling deletes the saved endpoint and other app data. |
| App opens as a conventional flat Android app | Recheck the RayNeo OpenXR features, launch activity, and `com.rayneo.mercury.app` metadata. |
| Connection panel stays open | Verify Host/Port, Redis availability from the glasses, and network/firewall settings. If discovery is unavailable, use manual entry. |
| Host/Port field has no usable keyboard | The explicit app keyboard bridge is disabled in the current scene. Arrange DNS-SD discovery or operator-assisted setup for that device. |
| HUD shows `NO SENSOR STREAMS` | Start physiology publishers before launching. Verify actual messages arrive during the discovery window, then relaunch. |
| Graphs or mesh are static, incomplete, or neutral | Check the selected sensor ID, channel names, the four z-score fields, and ongoing publication. For engagement, check matching identity and `confirmed: true`. |
| The fourth view is empty | ArtistViz is a placeholder. Single tap again. |
| View is too large, small, high, or low | Use the swipe controls. Look directly at the visible visualization when scaling. |
| Temple controls do not respond | Verify the main application scene and RayNeo Controller Profile are included, and distinguish one-finger double taps from two-finger taps. |

For a signing-key mismatch, uninstall only if losing the app's saved settings is acceptable:

```powershell
adb uninstall com.TUS.RayNeoXRScoreViewer
```

Then repeat the install command. For runtime diagnostics, capture Unity and Android errors with [Logcat](https://developer.android.com/tools/logcat):

```powershell
adb logcat "Unity:D" "AndroidRuntime:E" "*:S"
```

When reporting a problem, include the Git revision/APK, Glass OS and ARDK versions, the selected sensor ID, the observed behavior, and relevant log output.

## Development and tests

### Project layout

| Path | Responsibility |
| --- | --- |
| `Assets/AmpPortableDataViz/Core` | Domain models, contracts, and ports. |
| `Assets/AmpPortableDataViz/Application` | Sensor selection and data coordination. |
| `Assets/AmpPortableDataViz/Infra` | Redis, discovery, decoding, and clocks. |
| `Assets/AmpPortableDataViz/Presentation` | Unity components, visualization bindings, rendering, input, and UI. |
| `Assets/Scenes/ampPortable_xrGlassesDemo.unity` | Main application scene. |
| `Assets/Prefabs/SensorGraphPrefab.prefab` | Current five-channel sensor graph display. |
| `Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab` | RayNeo particle-mesh display. |
| `Assets/StreamingAssets` | Bundled score images. |
| `Assets/Tests/PlayMode` | PlayMode tests. |

`Bootstrapper` coordinates configuration and lifecycle. Its helpers manage Redis streams (`RedisStreamRegistry`), visual instances and bindings (`VisualizationPresenter`), and placement/labels/HUD (`VisualizationLayout`). Bindings map incoming data to visual parameters.

Manual/procedural drivers remain available for development. Enabling **Use Manual Visualization Drivers** on Bootstrapper bypasses live Redis sources for the mesh and graphs, but the endpoint prompt must be handled separately. This path does not create the normal selected-sensor HUD or ArtistViz. It is an Editor/development workflow, rather than an end-user offline mode.

### Run tests

Use Unity **2022.3.36f1**, open **Window > General > Test Runner**, select **PlayMode**, and run the tests. For a batch run, close the Editor instance using this project first:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.36f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults "TestResults\playmode.xml"
```

Let the test runner finish and inspect its XML result. PlayMode tests cover data flow and visual behavior in Unity; device installation, temple gestures, display readability, and real network reception also need an on-device check.

Historical evidence is retained in [Assessment](Assessment). The [30 October baseline assessment](Assessment/2026-10-30/RESULTS.md) records an APK build, deployment, and on-device observation. The [graph prefab assessment](Assessment/2026-11-03/audience-graph-prefab/README.md) records a later 225-case passing PlayMode run. These reports apply to their recorded snapshots and are not a claim that every later commit has been tested on the glasses.

## Further documentation

- [Abstract Mapping Guide](AbstractMapping.md) — explanation of the particle-mesh cues.
- [RayNeo controls reference (PDF)](Assets/Docs/RayNeo_X3_Pro_XR_Score_Viewer_Controls_v010alpha9.pdf) — printable controls reference.
- [Earlier RayNeo setup guide](Assets/Docs/README_v010-alpha9.md) — historical port setup details.
- [Bootstrapper helper extraction](Assessment/2026-11-03/bootstrapper-refactor-step5/README.md) and [session retirement](Assessment/2026-11-03/bootstrapper-session-retirement/README.md) — architectural changes and their validation.

Use this README and the current scene/input code when an older guide differs. In particular, some older tutorials describe a one-finger double tap as quit; the current app uses it to change sensors.
