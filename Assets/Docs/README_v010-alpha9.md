# XR Score Viewer — RayNeo X3 Pro

Unity XR proof-of-concept for viewing real-time audience data on RayNeo X3 Pro glasses. The app presents a score/image board, an abstract particle-mesh view, an objective five-channel graph view, and an ArtistViz placeholder. The active sensor and visualization can be changed with the right-temple touchpad.

This document applies to the `rayNeoPort` branch as at 31 August 2026.

## Project configuration

- Unity Editor: `2022.3.36f1`
- Build target: Android
- Main scene: `Assets/Scenes/ampPortable_xrGlassesDemo.unity`
- Product name: `XR Score Viewer`
- Android application ID: `com.TUS.RayNeoXRScoreViewer`
- Minimum and target Android API: 30
- Architecture: ARM64
- Scripting backend: IL2CPP
- Graphics API: OpenGL ES 3
- XR runtime: OpenXR with RayNeo Support and RayNeo Controller Profile enabled
- RayNeo camera attitude mode: `DOF3`

The project already contains the RayNeo launch activity and network permissions in `Assets/Plugins/Android/AndroidManifest.xml`.

## Prerequisites

1. Install Unity `2022.3.36f1` through Unity Hub.
2. Add all modules under **Android Build Support**, including Android SDK & NDK Tools and OpenJDK.
3. Install Android Platform Tools, or use the `adb` executable included with Unity's Android SDK.
4. Obtain the RayNeo OpenXR Unity ARDK version appropriate for the X3 Pro Glass OS.
5. Update the RayNeo X3 Pro to the current Glass OS before testing.

RayNeo's X3 Pro development walkthrough also uses Unity 2022.3.36 and documents the OpenXR, manifest, ADB, and Build And Run workflow: [Get Started with RayNeo X3 Pro AR Development](https://www.qualcomm.com/developer/project/get-started-with-rayneo-x3-pro-ar-development). The public package source is available at [github-for-rayneo/OpenXR-Unity-ARDK](https://github.com/github-for-rayneo/OpenXR-Unity-ARDK).

## Resolve the RayNeo ARDK package

The project currently references the ARDK as a machine-local package in `Packages/manifest.json`:

```json
"com.unity.xr.rayneo.openxr": "file:C:/Users/Bryan Dunphy/Documents/RayNeoSDK/RayNeo OpenXR Unity ARDK/RayNeo OpenXR Unity ARDK"
```

If the package is stored elsewhere on the build machine, change only this `file:` value to the directory containing the ARDK `package.json`. Do not edit `Packages/packages-lock.json` by hand; Unity will resolve it when the project opens.

Open the project and allow Unity to finish importing packages and compiling scripts before building. If prompted to enable the Input System and restart, accept the prompt.

## Verify the checked-in XR configuration

These settings are already committed, but verify them after changing SDK versions or accepting an OpenXR validation fix:

1. Open **File > Build Settings**, select **Android**, and choose **Switch Platform** if needed.
2. Confirm that only `Assets/Scenes/ampPortable_xrGlassesDemo.unity` is enabled under **Scenes In Build**.
3. Open **Edit > Project Settings > XR Plug-in Management > Android** and confirm **OpenXR** is enabled.
4. In **OpenXR > Android**, confirm **RayNeo Support** and **RayNeo Controller Profile** are enabled.
5. Confirm RayNeo Support uses `DOF3` camera attitude mode.
6. Review **OpenXR Project Validation**. Apply relevant Android/RayNeo fixes, then recheck the settings above.
7. Confirm `Assets/Plugins/Android/AndroidManifest.xml` still contains:

   - launch activity `com.rayneo.openxradapter.UnityOpenXrActivity`;
   - metadata `com.rayneo.mercury.app` set to `true`;
   - internet, network-state, Wi-Fi-state, and multicast-state permissions.

If the launch activity or RayNeo metadata is missing, the glasses can treat the build as a conventional Android application instead of a RayNeo XR application.

## Build an APK

1. Open **File > Build Settings**.
2. Select **Android** and choose **Switch Platform** if Android is not active.
3. Leave **Build App Bundle (Google Play)** disabled so Unity produces an APK for sideloading.
4. Enable **Development Build** only when development diagnostics are required.
5. Choose **Build** and save the output, for example as `Builds/RayNeo_XR_Score_Viewer.apk`.

Use **Build And Run** instead if the X3 Pro is already connected and authorized through ADB.

## Enable ADB on the RayNeo X3 Pro

Menu labels can vary slightly between Glass OS releases.

1. On the glasses, open **Settings > General > About Device** and note the Glass OS version.
2. If Developer Options are hidden, repeatedly select the build number until developer mode is enabled.
3. Open **Developer Options** and enable **USB debugging**.
4. Connect the glasses directly to the development machine with a data-capable USB-C cable.
5. Accept the USB-debugging authorization prompt on the glasses.
6. Verify the connection from PowerShell:

   ```powershell
   adb devices
   ```

The device should be listed as `device`. If it is listed as `unauthorized`, reconnect it and accept the authorization prompt. If `adb` is not on `PATH`, invoke the copy under Unity's Android SDK `platform-tools` directory.

## Install and launch

Install or update the APK:

```powershell
adb install -r ".\Builds\RayNeo_XR_Score_Viewer.apk"
```

Launch **XR Score Viewer** from the glasses' application list. It can also be launched from ADB:

```powershell
adb shell monkey -p com.TUS.RayNeoXRScoreViewer -c android.intent.category.LAUNCHER 1
```

If Android reports `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, the installed copy was signed with a different key. Uninstalling it with the following command erases the app's saved settings, including its cached Redis endpoint:

```powershell
adb uninstall com.TUS.RayNeoXRScoreViewer
```

Then repeat the install command.

## First run and live data

The glasses and Redis host must be reachable on the same network. At startup, the app checks a saved endpoint and searches for a DNS-SD service of type `_amplify-redis._tcp`. If neither path reaches Redis, the startup panel asks for a host/IP and port. The usual Redis port is `6379`.

The current per-sensor data paths are:

- `device:{id}:physio_metrics` for tonic EDA, temperature rate of change, SCR frequency, heart rate, and legacy IBI data;
- `device:{serial}:engagement` for per-sensor engagement, using payload fields `device`, `engagement`, `confirmed`, and `timestamp`.

Only tonic EDA, temperature rate of change, SCR frequency, heart rate, and engagement are mapped into the current abstract and objective visualizations. IBI, arousal, and valence may still be decoded by compatibility code but are not displayed by these views.

The sensor HUD shows `SENSOR n / total` and the active device identifier. Double-tap the right-temple touchpad to select the next discovered sensor. See `RayNeo_X3_Pro_XR_Score_Viewer_Controls.docx` for the complete control reference.

## Troubleshooting

- **RayNeo package cannot be resolved:** correct the machine-local `file:` path in `Packages/manifest.json`, then reopen Unity.
- **ADB shows no device:** use a data-capable cable, re-enable USB debugging, and check the authorization prompt on the glasses.
- **The app opens as a flat/mobile app:** recheck the RayNeo OpenXR features and the RayNeo activity/metadata in the Android manifest.
- **The startup panel remains visible:** confirm the Redis host and port are reachable. Automatic discovery also requires multicast/DNS-SD traffic to cross the local network.
- **No sensors appear:** confirm `device:{id}:physio_metrics` channels exist and are publishing. Confirm each engagement channel follows `device:{serial}:engagement` and that its payload is confirmed.
- **Touchpad controls do not respond:** confirm the build contains the RayNeo Controller Profile and that the main scene, rather than a sample scene, is enabled.
- **Visuals appear too large, small, high, or low:** use the gaze-aware horizontal scale gesture and vertical swipe controls described in the controls document.

