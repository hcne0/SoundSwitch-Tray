Inspect this SoundSwitch project (C# source, AppxManifest.xml, assets, and packaging script) and prepare a complete, Microsoft Store-ready MSIX build with Windows auto-start enabled. Do the following end-to-end:

1. Update AppxManifest.xml for auto-start:
   - Add `xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"` to the root `<Package>` tag and include `desktop` in `IgnorableNamespaces`.
   - Inside `<Application>`, add a `<Extensions>` block containing:
     `<desktop:Extension Category="windows.startupTask" Executable="..." EntryPoint="Windows.FullTrustApplication">` (matching the exact `Executable` path already used on `<Application>`).
   - Inside that extension, add `<desktop:StartupTask TaskId="SoundSwitchTrayStartup" Enabled="true" DisplayName="SoundSwitch Tray" />`.
   - Do NOT add `rescap5:ImmediateRegistration`.
   - Sanity-check the rest of `AppxManifest.xml` for Store rules: valid 4-part version (e.g. `1.0.0.0`), `rescap:runFullTrust` capability present, `MinVersion` higher than `10.0.17134.0`, and verify all referenced icon assets are genuine PNG files (check file headers, not just extensions).
   - Inspect `Identity/Name`, `Identity/Publisher`, and `PublisherDisplayName`—do not overwrite real Partner Center values, but explicitly warn me in the final summary if any of them still look like dummy/placeholder values.

2. Clean up the C# source:
   - Remove any legacy registry startup code (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` or Startup folder shortcuts) since MSIX virtualizes those.
   - Ensure single-instance protection using a named `Mutex` in `Program.cs` so that if Windows auto-starts the app at logon and the user opens it manually later, it exits cleanly instead of spawning a second tray icon.

3. Rebuild and verify the MSIX:
   - Build the project in **Release** configuration (not Debug).
   - Run the existing MSIX packaging script (or `makeappx`) to regenerate the `.msix` inside `dist\`.
   - Inspect the generated `.msix` archive contents (without leaving temp junk behind) and confirm the Release `.exe`, updated `AppxManifest.xml`, and all PNG icons are inside and non-empty.

4. Generate Store submission files in a new `store-assets\` folder:
   - `store-assets\privacy-policy.html`: A clean, standalone HTML privacy policy page ready to upload to GitHub Pages or Netlify stating that SoundSwitch Tray runs completely offline and collects, stores, or transmits zero personal data.
   - `store-assets\partner-center-copy.md`: Ready-to-paste Store listing description, bulleted features, and a "Notes for certification" paragraph explaining why the app declares `runFullTrust` (enumerating and switching Windows audio playback devices from the system tray) and `windows.startupTask` (keeping the tray utility available after logon).

5. Final report:
   - Summarize every file changed, confirm the rebuilt `.msix` path and size, list what is inside the `.msix`, and tell me only the remaining manual actions I need to take in Partner Center.