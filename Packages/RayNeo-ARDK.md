# Bundled RayNeo OpenXR ARDK

`com.unity.xr.rayneo.openxr-1.1.2.tgz` contains the unmodified RayNeo OpenXR Unity ARDK **1.1.2** used by this project's existing RayNeo baseline. It was repackaged from the previously referenced local SDK into a Unity-compatible gzip tar archive with a `package/` root. All 444 files, including Unity `.meta` files, Android plugins, samples, and extensions, are preserved.

The project manifest references the archive relative to `Packages/`, so a checkout includes the SDK without depending on a developer's installation directory. Unity extracts it into its managed package cache. No SDK download, manual extraction, or manifest edit is required on another build machine.

## Provenance and license

The upstream package identifies its author as `RayNeo`, its name as `com.unity.xr.rayneo.openxr`, its version as `1.1.2`, and its license as `OpenXR RAYNEO`. The original package metadata is preserved. This third-party SDK remains subject to RayNeo's terms, separately from this project's BSD license.

Upstream downloads: [RayNeo SDK downloads](https://leiniao-ibg.feishu.cn/wiki/IDFfwLKR4iihBuknkVrcGiMUnic). The [public SDK repository](https://github.com/github-for-rayneo/OpenXR-Unity-ARDK) is a reference; do not substitute a different version as part of initial setup.

## Verification and updates

Archive SHA-256:

```text
149e9ef02bee9055551a3a2df7d63a23d6c17551ed0aadf9ac55e2160083455e
```

Verify from the repository root in PowerShell:

```powershell
Get-FileHash Packages/com.unity.xr.rayneo.openxr-1.1.2.tgz -Algorithm SHA256
tar -xOf Packages/com.unity.xr.rayneo.openxr-1.1.2.tgz package/package.json
```

An intentional SDK update must preserve the vendor files and `.meta` files, use a versioned archive filename, update the manifest and these notes, and let Unity regenerate the lockfile. Validate Glass OS compatibility and an Android build before adopting a different SDK version.
