# SoundSwitch Tray

SoundSwitch Tray is a lightweight Windows system-tray utility for one-click switching between connected audio playback devices. Its icon identifies the active output, and its Windows startup support keeps it available after sign-in.

## Microsoft Store

**Microsoft Store (recommended):** The published Store version will be signed by Microsoft. This avoids Windows Smart App Control blocks that can affect unsigned local executables.

**Store link:** _Live Microsoft Store URL to be added once the package is approved._

The source package is unsigned until Microsoft certifies and signs it through the Store.

[Privacy Policy](https://soundswitch-privacypolicies.netlify.app/privacy-policy.html)

## Key features

- Switch connected audio playback devices with one click from the system tray.
- Recognize the active output by its tray icon color, with preferences saved locally.
- Change Windows media and communications playback defaults together.
- Refresh available outputs automatically as devices connect or disconnect.
- See the current output and connected device count in the tray tooltip.
- Start with Windows by default, with startup controlled through Windows Settings.
- Keep a single tray instance when the app is launched again.
- Run completely offline with no telemetry or personal data collection.

## Building from source

SoundSwitch Tray uses the .NET Framework included with Windows; no separate SDK download is required. From the repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

Release is the default configuration. Use `-Configuration Debug` for a debug build.

Add `-Test` to compile and run the self-check suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -Test
```

The build creates `SoundSwitch.exe` in the project root. Running unsigned source builds may require a Windows policy that permits local applications.

## Microsoft Store package

Run `.\package-msix.ps1` to rebuild Release and create an unsigned Store package under `dist\`. [AppxManifest.xml](AppxManifest.xml) is the package manifest. Microsoft signs accepted packages during Store certification.

Store listing and submission materials are available in [store-assets/](store-assets/) and [store-submission/](store-submission/).

## License

SoundSwitch Tray is licensed under the [MIT License](LICENSE).
