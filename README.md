# XR Score Viewer

Unity XR proof-of-concept for viewing real-time audience data on XREAL glasses through a XREAL Beam Pro. The app turns audience telemetry into spatial visualizations: emotion plasma, particle mesh forms, engagement HUD elements, channel graphs, and score/image boards.

Runtime data is mapped through the project layers in `Assets/AmpPortableDataViz`: domain contracts in `Core`, orchestration in `Application`, Redis/LiveKit services in `Infra`, and Unity visualizers/bindings in `Presentation`. Redis is the main live-data path; LiveKit and local/manual sources are also present for testing.

## Project Setup

- Unity version: `6000.3.10f1`
- Active scene: `Assets/Scenes/ampPortable_xrGlassesDemo.unity`
- Android app name: `XR Score Viewer`
- Android minimum SDK: API 29
- XR runtime packages include XREAL XR, OpenXR, XR Interaction Toolkit, XR Hands, AR Foundation, URP, LiveKit, and NuGetForUnity.
- The XREAL SDK is referenced from a local package archive in `Packages/manifest.json`: `C:/Users/Bryan Dunphy/Documents/XRealSDK/com.xreal.xr.tar.gz`. If another machine uses a different SDK location, update that path before opening the project.

## Build an APK

1. Open the project in Unity `6000.3.10f1` with Android Build Support installed.
2. Open `File > Build Profiles`.
3. Select or add an Android build profile, then switch to it.
4. Confirm `Assets/Scenes/ampPortable_xrGlassesDemo.unity` is the enabled scene.
5. Disable `Build App Bundle` so Unity outputs an APK for sideloading.
6. Use `Build` to create an APK, or `Build And Run` if the Beam Pro is already visible to Unity.

## Install on XREAL Beam Pro

1. On the Beam Pro, enable Developer Options by opening Android settings, going to the device/about screen, and tapping the build number repeatedly until developer mode is enabled.
2. In Developer Options, enable USB debugging.
3. Connect the Beam Pro to the development machine over USB-C and approve the debugging prompt on the device.
4. Verify ADB can see the device:

   ```powershell
   adb devices
   ```

5. Install or update the APK:

   ```powershell
   adb install -r path\to\XRScoreViewer.apk
   ```

6. Connect the XREAL glasses to the Beam Pro, then launch `XR Score Viewer` from the Beam Pro app list.

On first run, the app may prompt for a Redis endpoint. Enter the Redis host/IP and port, usually `6379`, and make sure the Beam Pro is on the same network as the Redis server. The live Redis channels currently include `engagement:scores`, `device:{id}:valence_cont`, `device:{id}:arousal_cont`, and `device:{id}:physio_metrics`.

If ADB is not available, copy the APK to the Beam Pro, open it from the file manager, and allow installation from unknown apps when Android prompts.
