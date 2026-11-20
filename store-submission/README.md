# Microsoft Store submission

Upload `dist\SoundSwitchTray_1.0.0.0.msix` in Partner Center. This package is unsigned; Microsoft Store signs accepted packages during certification. Paste the description, features, restricted capability justification, and certification notes from [partner-center-copy.md](../store-assets/partner-center-copy.md). Upload `AppTileIcon_300x300.png` for the Store app tile. Host [privacy-policy.html](../store-assets/privacy-policy.html) at a public HTTPS URL and enter that URL in the privacy policy field. Verify that Identity Name, Identity Publisher, and PublisherDisplayName in the source `AppxManifest.xml` match this product's Partner Center identity. Complete any remaining Partner Center listing, screenshot, age rating, availability, and pricing fields before submitting.

Rebuild from the project directory with `.\package-msix.ps1`. The script invokes `build.ps1 -Configuration Release`, regenerates the icons, copies the source `AppxManifest.xml`, packs with Microsoft MakeAppx, and checks the archive contents, preserved identity, startup task, `runFullTrust`, PNG headers and dimensions, Release metadata, and executable hash. The package filename follows the manifest's four-part version; the fourth part must be zero for Store submission. SelfTest remains a separate `build.ps1 -Configuration Release -Test` option; packaging does not run audio switching tests.

If direct script execution is disabled, this inline invocation runs the same file without changing execution policy or Windows security settings:

```powershell
$packageFile = Join-Path (Get-Location).Path 'package-msix.ps1'
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($packageFile, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors[0] }
& $ast.GetScriptBlock()
```

MakeAppx is located first under `C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe`, then under `.build\windows-sdk\bin\*\x64\makeappx.exe` for workspace-local official Microsoft SDK BuildTools. If neither is available, install the Windows SDK with:

```powershell
winget install --id Microsoft.WindowsSDK.10.0.26100 --exact --source winget
```

This workspace includes official `Microsoft.Windows.SDK.BuildTools` version `10.0.26100.9457` extracted into `.build\windows-sdk`. MakeAppx's Microsoft Corporation signature was verified. These workspace-local tools were used because the system-wide Windows SDK is absent; no system-wide SDK installation was needed. The exact source and download SHA-256 are recorded in `.build\windows-sdk\SDK-SOURCE.txt`.

The script creates only `SoundSwitch.exe`, `AppxManifest.xml`, and the three package PNGs in `.build\msix-stage`; MakeAppx adds standard MSIX metadata. Submission documents, the 300px tile, SelfTest, and source files are kept outside the package.

The packaged app declares `SoundSwitchTrayStartup`, enabled by default, to start the tray utility after Windows logon. Launch the installed app once from Start. Users control startup in **Settings > Apps > Startup**. The app uses a named mutex to prevent a second tray instance and does not register legacy startup entries.

Validation for this build: Release compilation succeeded with warnings treated as errors. All 69 self-checks passed, including launching the actual EXE while its named mutex exists. The tray smoke check confirmed startup without a main window and a clean exit; the read-only audio probe detected two connected outputs. MakeAppx validation and archive checks passed, including the startup declaration, non-empty payloads, PNG signatures/dimensions, and the Release EXE hash and metadata. Archive inspection created no temporary extraction files. Playback defaults were not changed during these checks. Signed package installation, logon startup, and Store certification were not exercised locally.
