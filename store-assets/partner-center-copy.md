# SoundSwitch Tray

## Store description

Switch between speakers, headphones, and other connected audio outputs with one click from the Windows system tray.

SoundSwitch Tray is a small utility that cycles through your connected, enabled playback devices. Its loop icon uses the active output's assigned color, so you can recognize your current device at a glance. Hover over the icon to see the active playback device and the number of available outputs. Color preferences are remembered locally across restarts and reconnects.

Windows playback defaults for media and communications change together. The device list refreshes automatically when you connect or disconnect an output or change the default in Windows. With fewer than two available outputs, switching is disabled. Applications that select their own fixed playback device may keep that selection.

Launch SoundSwitch Tray once from Start after installation. Startup with Windows is enabled by default, keeping the utility available after logon; you can turn it off in Windows Settings under Apps > Startup. Windows may initially place the tray icon in the notification-area overflow menu. Right-click the icon and choose Exit SoundSwitch to close it.

SoundSwitch Tray runs completely offline and collects, stores, and transmits zero personal data. It has no accounts, advertising, analytics, telemetry, or network requests. Its only saved settings are local audio device color preferences. It runs as the current user without requesting administrator elevation.

Requires Windows 10 version 2004 (build 19041) or later, or Windows 11. At least two connected, enabled playback devices are needed to cycle outputs.

## Features

- Switch connected audio playback devices with one click from the system tray.
- Recognize the active output by its tray icon color, with preferences saved locally.
- Change Windows media and communications playback defaults together.
- Refresh available outputs automatically as devices connect or disconnect.
- See the current output and connected device count in the tray tooltip.
- Start with Windows by default, with startup controlled through Windows Settings.
- Keep a single tray instance when the app is launched again.
- Run completely offline, with zero personal data collection and no telemetry.
- Run as the current user without requesting administrator rights.

## Restricted capability justification: runFullTrust

SoundSwitch Tray is a .NET Framework Windows Forms desktop application. It declares `runFullTrust` to enumerate enabled Windows audio playback devices through Windows Core Audio COM interfaces and change the default playback endpoint from its system tray icon. It uses `IMMDeviceEnumerator` and `IPolicyConfig.SetDefaultEndpoint` to set the console, multimedia, and communications defaults after an explicit user click. These operations require the desktop process declared with `Windows.FullTrustApplication`. The executable requests `asInvoker` with `uiAccess=false`; it does not request administrator elevation, install drivers or services, record audio, or make network requests. It stores only local device-to-color preferences.

## Notes for certification

SoundSwitch Tray declares `runFullTrust` because it enumerates and switches Windows audio playback devices from a Windows Forms system tray process. It declares the `windows.startupTask` extension, with task ID `SoundSwitchTrayStartup` and `Enabled="true"`, to keep the tray utility available after logon. The startup extension uses the same `SoundSwitch.exe` executable and `Windows.FullTrustApplication` entry point as the application. Launch the app once from Start after installation to activate the installed startup registration; Windows controls startup and users can disable it in Settings > Apps > Startup. A named mutex ensures that a later manual launch exits successfully without creating a second tray icon. There is no main window: find the loop icon in the notification area or its overflow menu, hover for device details, left-click to cycle outputs, and right-click to exit. With two or more connected, enabled playback devices, a click changes all three Windows audio playback roles; with zero or one device it changes nothing. Logon startup itself does not change playback defaults. The app runs offline as the current user with no elevation, audio recording, telemetry, accounts, or network requests. Only device color preferences persist locally in `%LOCALAPPDATA%\SoundSwitch\colors.xml`, which Windows may redirect for the packaged app. The submitted MSIX is unsigned for Microsoft Store ingestion and signing.

## Submission reference

- Package: `dist\SoundSwitchTray_1.0.0.0.msix`
- Identity Name: `MunawerHussain.SoundSwitchTray`
- Identity Publisher: `CN=EC90E38D-07A2-491F-A9BB-F8B3460E191C`
- PublisherDisplayName: `Munawer Hussain`
- Version: `1.0.0.0`; architecture: `neutral` (AnyCPU desktop executable)
- Minimum Windows version: `10.0.19041.0`
- Privacy policy: host `store-assets\privacy-policy.html` at a public HTTPS URL and enter that URL in Partner Center.
- Store app tile: `store-submission\AppTileIcon_300x300.png` (300 x 300 PNG)

Verify the three identity values against this product's Partner Center product identity before uploading. Complete the Store listing and screenshots, age ratings, pricing, and availability, then submit for certification.
