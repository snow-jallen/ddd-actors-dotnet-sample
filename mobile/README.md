# The driver app's MAUI head

This folder holds everything that needs the .NET MAUI workload (section 19.4): the app's head,
`Ohmline.Mobile`, and `Ohmline.Mobile.slnx`, which builds it together with the projects it
uses. The app's logic is not here. It is `src/Mobile/Ohmline.Mobile.Core`, a plain `net11.0`
library that builds and is tested with the rest of the sample in `Ohmline.slnx`, on any machine.

| Path | What it is |
|---|---|
| `Ohmline.Mobile.slnx` | The client contracts, `Ohmline.Mobile.Core`, its tests, and the head |
| `Directory.Build.props`, `Directory.Build.targets` | Import the root files, then clear `TargetFramework` so the head's own `TargetFrameworks` apply |
| `Ohmline.Mobile/` | `MauiProgram`, `AppShell`, the pages, platform adapters (`MauiConnectivity`, `MauiDispatcher`), `appsettings.json` |

Why a second solution, and why here: the root must hold exactly one solution, or a bare
`dotnet build` fails with MSB1011, and the root solution must not contain the head, or every
build would need the MAUI workload.

## What you need

- The .NET SDK named in `global.json`.
- The MAUI workload for Android: `dotnet workload install maui-android`.
- An Android SDK and a JDK. Either install Android Studio, or let .NET fetch both into folders
  of your choice:

  ```bash
  dotnet build mobile/Ohmline.Mobile/Ohmline.Mobile.csproj -t:InstallAndroidDependencies \
    -f net11.0-android -p:AndroidSdkDirectory=$HOME/android-sdk \
    -p:JavaSdkDirectory=$HOME/jdk -p:AcceptAndroidSDKLicenses=True
  ```

  Then export `ANDROID_HOME` (or `AndroidSdkDirectory`) and `JAVA_HOME` (or `JavaSdkDirectory`).

iOS and Mac Catalyst build only on a Mac with Xcode, and Windows only on Windows: the head adds
those target frameworks only on those hosts. On Linux its one target framework is
`net11.0-android`.

## Build

```bash
dotnet build mobile/Ohmline.Mobile.slnx -warnaserror
```

Do not pass `-f net11.0-android` to the solution build: it would force the plain `net11.0`
projects onto Android. To build only the head:

```bash
dotnet build mobile/Ohmline.Mobile/Ohmline.Mobile.csproj -f net11.0-android -warnaserror
```

Without the workload, the mobile solution, and only it, fails with:

```text
error NETSDK1208: The target platform identifier android was not recognized.
```

`tools/verify-tags.sh` builds this solution at every tag where it exists, and stops if
`ANDROID_HOME` or `JAVA_HOME` is not set. Set `OHMLINE_SKIP_MOBILE=1` to skip it on a machine
without an Android toolchain.

## Run on the Android emulator

Start the node on the development machine (`dotnet run --project src/Ohmline.Node`, port 5180
in Development, or the AppHost), start an emulator, then:

```bash
dotnet build mobile/Ohmline.Mobile/Ohmline.Mobile.csproj -f net11.0-android -t:Run
```

The emulator reaches the development machine as `10.0.2.2`, which is what
`Ohmline.Mobile/appsettings.json` names (`Ohmline:ApiBaseAddress`). Debug builds may use plain
HTTP to that address only (`Platforms/Android/Resources/xml/network_security_config.xml`);
Release builds use Android's HTTPS-only default.
