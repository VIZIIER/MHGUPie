# Builds MHGUPie with the C# compiler that ships with Windows (.NET Framework 4.x). Nothing to install.
#   powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$wpf = Join-Path $fw 'WPF'
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$refs = @(
    "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll",
    "/r:$fw\System.Xaml.dll", '/r:System.dll', '/r:System.Core.dll', '/r:System.Xml.dll'
)
$src = Get-ChildItem -Recurse (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName

& $csc /nologo /target:winexe /platform:x64 /optimize+ /out:"$out\MHGUPie.exe" $refs $src
if ($LASTEXITCODE -ne 0) { throw 'MHGUPie build failed' }

& $csc /nologo /target:exe /platform:x64 /optimize+ /out:"$out\MHGUPieSimulator.exe" (Join-Path $root 'sim\Simulator.cs')
if ($LASTEXITCODE -ne 0) { throw 'Simulator build failed' }

Copy-Item -Recurse -Force (Join-Path $root 'config') $out
Write-Host "Built: $out\MHGUPie.exe (+ MHGUPieSimulator.exe for testing without the game)"
