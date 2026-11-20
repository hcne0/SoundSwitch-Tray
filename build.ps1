param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }

$buildDirectory = Join-Path $taskRoot '.build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$common = @('/nologo', '/warn:4', '/warnaserror+', '/platform:anycpu', '/utf8output',
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Xml.dll')
if ($Configuration -eq 'Release') {
    $common += @('/optimize+', '/debug-', '/define:TRACE')
} else {
    $common += @('/optimize-', '/debug:full', '/define:DEBUG;TRACE')
}
$applicationIcon = Join-Path $taskRoot 'assets\SoundSwitch.ico'
$application = Join-Path $taskRoot 'SoundSwitch.exe'
$manifest = Join-Path $taskRoot 'app.manifest'
& $compiler @common '/target:winexe' '/main:SoundSwitch.Program' "/out:$application" "/win32icon:$applicationIcon" "/win32manifest:$manifest" @sources
if ($LASTEXITCODE -ne 0) { throw 'SoundSwitch compilation failed. Exit a running copy before rebuilding.' }
Write-Output "Built $Configuration executable: $application"

if ($Test) {
    $selfTest = Join-Path $buildDirectory 'SelfTest.exe'
    & $compiler @common '/target:exe' '/main:SoundSwitch.SelfTest' "/out:$selfTest" @sources (Join-Path $taskRoot 'tests\SelfTest.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Self-check compilation failed.' }
    & $selfTest
    if ($LASTEXITCODE -ne 0) { throw 'SoundSwitch self-checks failed.' }
}
