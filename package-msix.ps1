$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot

function New-ManifestNamespaces([Xml.XmlDocument]$Document) {
    $namespaces = [Xml.XmlNamespaceManager]::new($Document.NameTable)
    $namespaces.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaces.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
    $namespaces.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
    $namespaces.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')
    return ,$namespaces
}

function Assert-StoreManifest([Xml.XmlDocument]$Document, [Xml.XmlNamespaceManager]$Namespaces) {
    $root = $Document.DocumentElement
    if ($root.NamespaceURI -cne $Namespaces.LookupNamespace('f') -or
        $root.GetAttribute('xmlns:desktop') -cne $Namespaces.LookupNamespace('desktop') -or
        ($root.GetAttribute('IgnorableNamespaces') -split '\s+') -cnotcontains 'desktop') {
        throw 'The package must declare the desktop namespace and include it in IgnorableNamespaces.'
    }
    if ($Document.SelectNodes('//@*[local-name()="ImmediateRegistration"] | //*[local-name()="ImmediateRegistration"]').Count) {
        throw 'ImmediateRegistration must not be declared in the Store manifest.'
    }
    $identity = $Document.SelectSingleNode('/f:Package/f:Identity', $Namespaces)
    if ($null -eq $identity) { throw 'Package identity is missing.' }
    $versionText = $identity.GetAttribute('Version')
    if ($versionText -cnotmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'The package version must have four numeric parts.' }
    $version = [version]$versionText
    if ($version.Major -gt 65535 -or $version.Minor -gt 65535 -or $version.Build -gt 65535 -or $version.Revision -ne 0) {
        throw 'Store package version parts must be 0-65535, with the fourth part reserved as 0.'
    }
    $family = $Document.SelectSingleNode('/f:Package/f:Dependencies/f:TargetDeviceFamily', $Namespaces)
    if ($null -eq $family -or $family.GetAttribute('Name') -cne 'Windows.Desktop' -or
        [version]$family.GetAttribute('MinVersion') -le [version]'10.0.17134.0' -or
        [version]$family.GetAttribute('MaxVersionTested') -lt [version]$family.GetAttribute('MinVersion')) {
        throw 'The desktop minimum version must exceed 10.0.17134.0 and must not exceed MaxVersionTested.'
    }
    $applications = @($Document.SelectNodes('/f:Package/f:Applications/f:Application', $Namespaces))
    if ($applications.Count -ne 1 -or $applications[0].GetAttribute('Executable') -cne 'SoundSwitch.exe' -or
        $applications[0].GetAttribute('EntryPoint') -cne 'Windows.FullTrustApplication') {
        throw 'Expected one full-trust SoundSwitch.exe application.'
    }
    $extensions = @($Document.SelectNodes('/f:Package/f:Applications/f:Application/f:Extensions/desktop:Extension', $Namespaces))
    if ($extensions.Count -ne 1 -or $extensions[0].GetAttribute('Category') -cne 'windows.startupTask' -or
        $extensions[0].GetAttribute('Executable') -cne $applications[0].GetAttribute('Executable') -or
        $extensions[0].GetAttribute('EntryPoint') -cne 'Windows.FullTrustApplication') {
        throw 'The startup extension must use the application executable and full-trust entry point.'
    }
    $startupTasks = @($extensions[0].SelectNodes('desktop:StartupTask', $Namespaces))
    if ($startupTasks.Count -ne 1 -or $startupTasks[0].GetAttribute('TaskId') -cne 'SoundSwitchTrayStartup' -or
        $startupTasks[0].GetAttribute('Enabled') -cne 'true' -or $startupTasks[0].GetAttribute('DisplayName') -cne 'SoundSwitch Tray') {
        throw 'Expected the enabled SoundSwitchTrayStartup task with display name SoundSwitch Tray.'
    }
    $capabilities = @($Document.SelectNodes('/f:Package/f:Capabilities/*', $Namespaces))
    if ($capabilities.Count -ne 1 -or $capabilities[0].NamespaceURI -cne $Namespaces.LookupNamespace('rescap') -or
        $capabilities[0].LocalName -cne 'Capability' -or $capabilities[0].GetAttribute('Name') -cne 'runFullTrust') {
        throw 'The manifest must declare only the restricted runFullTrust capability.'
    }
}

$manifestPath = Join-Path $taskRoot 'AppxManifest.xml'
[xml]$sourceManifest = [IO.File]::ReadAllText($manifestPath)
$sourceNamespaces = New-ManifestNamespaces $sourceManifest
Assert-StoreManifest $sourceManifest $sourceNamespaces
$identity = $sourceManifest.SelectSingleNode('/f:Package/f:Identity', $sourceNamespaces)
$packageVersion = $identity.GetAttribute('Version')
$publisherDisplayName = $sourceManifest.SelectSingleNode('/f:Package/f:Properties/f:PublisherDisplayName', $sourceNamespaces)
if ($null -eq $publisherDisplayName -or [string]::IsNullOrWhiteSpace($publisherDisplayName.get_InnerText()) -or
    [string]::IsNullOrWhiteSpace($identity.GetAttribute('Name')) -or [string]::IsNullOrWhiteSpace($identity.GetAttribute('Publisher'))) {
    throw 'Identity Name, Publisher, and PublisherDisplayName must all be present.'
}
foreach ($identityValue in @($identity.GetAttribute('Name'), $identity.GetAttribute('Publisher'), $publisherDisplayName.get_InnerText())) {
    if ($identityValue -match '(?i)placeholder|contoso|fabrikam|example|dummy|your[ ._-]*(company|publisher|name|app)|00000000-0000-0000-0000-000000000000') {
        Write-Warning "Possible placeholder Store identity value: $identityValue. Use the exact value from Partner Center."
    }
}

# Parsing the existing file preserves its script root without changing execution policy.
$tokens = $null
$parseErrors = $null
$build = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $taskRoot 'build.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw $parseErrors[0] }
& $build.GetScriptBlock() -Configuration Release

$stage = [IO.Path]::GetFullPath((Join-Path $taskRoot '.build\msix-stage'))
if (Test-Path -LiteralPath $stage) {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).ProviderPath
    if ($resolvedStage -ne $stage -or !$resolvedStage.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        ((Get-Item -LiteralPath $stage).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to clear a staging directory outside the project or through a reparse point.'
    }
    Remove-Item -LiteralPath $stage -Recurse -Force
}
$assets = Join-Path $stage 'Assets'
$submission = Join-Path $taskRoot 'store-submission'
New-Item -ItemType Directory -Path $assets, $submission -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'SoundSwitch.exe') -Destination (Join-Path $stage 'SoundSwitch.exe')

Add-Type -AssemblyName System.Drawing
# Read the largest PNG frame directly; .NET Framework Icon.ToBitmap cannot decode this ICO frame.
$ico = [IO.File]::ReadAllBytes((Join-Path $taskRoot 'assets\SoundSwitch.ico'))
$frame = 6 + ([BitConverter]::ToUInt16($ico, 4) - 1) * 16
$pngLength = [int][BitConverter]::ToUInt32($ico, $frame + 8)
$pngOffset = [int][BitConverter]::ToUInt32($ico, $frame + 12)
if ($pngOffset + $pngLength -gt $ico.Length -or
    [BitConverter]::ToString($ico, $pngOffset, 8) -cne '89-50-4E-47-0D-0A-1A-0A') {
    throw 'The application ICO must contain a valid PNG frame.'
}
$iconStream = [IO.MemoryStream]::new($ico, $pngOffset, $pngLength, $false)
$sourceIcon = [Drawing.Image]::FromStream($iconStream)
$icons = @(
    @{ Path = (Join-Path $assets 'StoreLogo.png'); Size = 50 },
    @{ Path = (Join-Path $assets 'Square44x44Logo.png'); Size = 44 },
    @{ Path = (Join-Path $assets 'Square150x150Logo.png'); Size = 150 },
    @{ Path = (Join-Path $submission 'AppTileIcon_300x300.png'); Size = 300 }
)
try {
    if ($sourceIcon.Width -ne 256 -or $sourceIcon.Height -ne 256) { throw 'Expected the existing 256x256 loop artwork.' }
    # shortcut: the 300px tile resizes the existing 256px artwork, use a larger master if larger assets are needed.
    foreach ($asset in $icons) {
        $bitmap = [Drawing.Bitmap]::new($asset.Size, $asset.Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceIcon, [Drawing.Rectangle]::new(0, 0, $asset.Size, $asset.Size))
            $bitmap.Save($asset.Path, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
        $pngBytes = [IO.File]::ReadAllBytes($asset.Path)
        if ($pngBytes.Length -lt 8 -or [BitConverter]::ToString($pngBytes, 0, 8) -cne '89-50-4E-47-0D-0A-1A-0A') {
            throw "Invalid PNG file header: $($asset.Path)"
        }
        $png = [Drawing.Bitmap]::new($asset.Path)
        try {
            if ($png.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid -or
                $png.Width -ne $asset.Size -or $png.Height -ne $asset.Size -or
                $png.GetPixel(0, 0).A -ne 0 -or $png.GetPixel([int]($asset.Size / 2), [int]($asset.Size / 2)).A -ne 0 -or
                $png.GetPixel([int]($asset.Size * 0.2), [int]($asset.Size / 2)).A -eq 0) {
                throw "Invalid PNG dimensions, transparency, or loop artwork: $($asset.Path)"
            }
        } finally { $png.Dispose() }
    }
} finally { $sourceIcon.Dispose(); $iconStream.Dispose() }

Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stage 'AppxManifest.xml')

$sdk = @(Get-ChildItem -Path 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' -ErrorAction SilentlyContinue)
if (!$sdk.Count) {
    $sdk = @(Get-ChildItem -Path (Join-Path $taskRoot '.build\windows-sdk\bin\*\x64\makeappx.exe') -ErrorAction SilentlyContinue)
}
if (!$sdk.Count) {
    throw 'MakeAppx is missing. Install the SDK with: winget install --id Microsoft.WindowsSDK.10.0.26100 --exact --source winget'
}
$makeappx = ($sdk | Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1).FullName
$dist = Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$package = Join-Path $dist "SoundSwitchTray_$packageVersion.msix"
Write-Output "Packing with $makeappx"
& $makeappx pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $payload = @('AppxManifest.xml', 'SoundSwitch.exe', 'Assets/StoreLogo.png', 'Assets/Square44x44Logo.png', 'Assets/Square150x150Logo.png')
    $required = $payload + @('AppxBlockMap.xml', '[Content_Types].xml')
    $allowed = $required + @('AppxMetadata/CodeIntegrity.cat')
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($entry in $entries) {
        if ($allowed -cnotcontains $entry) { throw "Unexpected archive entry (package must be unsigned): $entry" }
    }
    foreach ($entry in $required) {
        if ($entries -cnotcontains $entry) { throw "Missing archive entry: $entry" }
    }
    foreach ($entry in $archive.Entries) {
        if ($entry.Length -le 0) { throw "Empty archive entry: $($entry.FullName)" }
    }
    if ($entries.Count -ne @($entries | Select-Object -Unique).Count) { throw 'Duplicate archive entries.' }

    $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
    try { [xml]$packedManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = New-ManifestNamespaces $packedManifest
    Assert-StoreManifest $packedManifest $ns
    # Identity values come from the source manifest; packaging never replaces Partner Center values.
    $expected = @{
        '/f:Package/f:Identity/@Name' = $identity.GetAttribute('Name')
        '/f:Package/f:Identity/@Publisher' = $identity.GetAttribute('Publisher')
        '/f:Package/f:Identity/@Version' = $packageVersion
        '/f:Package/f:Identity/@ProcessorArchitecture' = 'neutral'
        '/f:Package/f:Properties/f:DisplayName' = 'SoundSwitch Tray'
        '/f:Package/f:Properties/f:PublisherDisplayName' = $publisherDisplayName.get_InnerText()
        '/f:Package/f:Properties/f:Logo' = 'Assets\StoreLogo.png'
        '/f:Package/f:Dependencies/f:TargetDeviceFamily/@Name' = 'Windows.Desktop'
        '/f:Package/f:Dependencies/f:TargetDeviceFamily/@MinVersion' = $sourceManifest.SelectSingleNode('/f:Package/f:Dependencies/f:TargetDeviceFamily/@MinVersion', $sourceNamespaces).get_InnerText()
        '/f:Package/f:Dependencies/f:TargetDeviceFamily/@MaxVersionTested' = $sourceManifest.SelectSingleNode('/f:Package/f:Dependencies/f:TargetDeviceFamily/@MaxVersionTested', $sourceNamespaces).get_InnerText()
        '/f:Package/f:Applications/f:Application/@Id' = 'SoundSwitch'
        '/f:Package/f:Applications/f:Application/@Executable' = 'SoundSwitch.exe'
        '/f:Package/f:Applications/f:Application/@EntryPoint' = 'Windows.FullTrustApplication'
        '/f:Package/f:Applications/f:Application/uap:VisualElements/@DisplayName' = 'SoundSwitch Tray'
        '/f:Package/f:Applications/f:Application/uap:VisualElements/@Square44x44Logo' = 'Assets\Square44x44Logo.png'
        '/f:Package/f:Applications/f:Application/uap:VisualElements/@Square150x150Logo' = 'Assets\Square150x150Logo.png'
    }
    foreach ($field in $expected.Keys) {
        $node = $packedManifest.SelectSingleNode($field, $ns)
        # PowerShell hides InnerText on XML attributes; call the CLR getter.
        if ($null -eq $node -or $node.get_InnerText() -cne $expected[$field]) { throw "Manifest mismatch: $field" }
    }
    $iconSizes = @{
        'Assets/StoreLogo.png' = 50
        'Assets/Square44x44Logo.png' = 44
        'Assets/Square150x150Logo.png' = 150
    }
    foreach ($iconPath in $iconSizes.Keys) {
        $iconEntry = $archive.GetEntry($iconPath)
        $packedIconStream = $iconEntry.Open()
        $memory = [IO.MemoryStream]::new()
        try {
            $packedIconStream.CopyTo($memory)
            $bytes = $memory.ToArray()
            if ($bytes.Length -lt 8 -or [BitConverter]::ToString($bytes, 0, 8) -cne '89-50-4E-47-0D-0A-1A-0A') {
                throw "Invalid packaged PNG file header: $iconPath"
            }
            $memory.Position = 0
            $packedIcon = [Drawing.Image]::FromStream($memory)
            try {
                if ($packedIcon.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid -or
                    $packedIcon.Width -ne $iconSizes[$iconPath] -or $packedIcon.Height -ne $iconSizes[$iconPath]) {
                    throw "Invalid packaged PNG format or dimensions: $iconPath"
                }
            } finally { $packedIcon.Dispose() }
        } finally { $memory.Dispose(); $packedIconStream.Dispose() }
    }
    $exeStream = $archive.GetEntry('SoundSwitch.exe').Open()
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $builtBytes = [IO.File]::ReadAllBytes((Join-Path $taskRoot 'SoundSwitch.exe'))
        $packedHash = [BitConverter]::ToString($sha256.ComputeHash($exeStream))
        $builtHash = [BitConverter]::ToString($sha256.ComputeHash($builtBytes))
        if ($packedHash -cne $builtHash) { throw 'The packaged executable does not match the fresh release build.' }
    } finally { $sha256.Dispose(); $exeStream.Dispose() }
    # Reflection-only loading reads build metadata without running or locking the executable.
    $releaseAssembly = [Reflection.Assembly]::ReflectionOnlyLoad($builtBytes)
    $configuration = @($releaseAssembly.GetCustomAttributesData() | Where-Object {
        $_.AttributeType.FullName -ceq 'System.Reflection.AssemblyConfigurationAttribute'
    })
    if ($configuration.Count -ne 1 -or $configuration[0].ConstructorArguments[0].Value -cne 'Release') {
        throw 'The packaged executable must declare the Release build configuration.'
    }
    $debugAttributes = @($releaseAssembly.GetCustomAttributesData() | Where-Object {
        $_.AttributeType.FullName -ceq 'System.Diagnostics.DebuggableAttribute'
    })
    foreach ($attribute in $debugAttributes) {
        $arguments = $attribute.ConstructorArguments
        if (($arguments.Count -eq 1 -and ([int]$arguments[0].Value -band 0x105) -ne 0) -or
            ($arguments.Count -eq 2 -and ($arguments[0].Value -or $arguments[1].Value))) {
            throw 'The packaged executable contains Debug settings or disabled optimizations.'
        }
    }
    Write-Output ('Verified unsigned archive (all entries non-empty): ' + ($entries -join ', '))
} finally { $archive.Dispose() }
Write-Output "PASS: Preserved Store identity, startup task, PNG headers/dimensions, runFullTrust, and Release payload verified."
Write-Output "Unsigned Store package: $package ($((Get-Item -LiteralPath $package).Length) bytes)"
