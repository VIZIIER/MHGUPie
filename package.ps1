# Builds MHGUPie and makes the zip to send to friends: dist\MHGUPie-<version>.zip
#   powershell -ExecutionPolicy Bypass -File package.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1')

$version = (Select-String -Path (Join-Path $root 'src\App.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist "MHGUPie-$version"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force (Join-Path $stage 'MHGUPie') | Out-Null

# the overlay: exe + address map + empty name files (no settings.ini, so every friend starts fresh)
Copy-Item (Join-Path $root 'bin\MHGUPie.exe') (Join-Path $stage 'MHGUPie')
Copy-Item -Recurse (Join-Path $root 'config') (Join-Path $stage 'MHGUPie\config')

# launcher + guide, with Windows line endings
foreach ($f in 'Play MHGU with MHGUPie.bat', 'HOW TO INSTALL.txt') {
    $text = [IO.File]::ReadAllText((Join-Path $root "package\$f")) -replace "`r?`n", "`r`n"
    [IO.File]::WriteAllText((Join-Path $stage $f), $text, (New-Object Text.UTF8Encoding $false))
}
Copy-Item (Join-Path $root 'package\HOW TO INSTALL.txt') (Join-Path $stage 'MHGUPie\HOW TO INSTALL.txt')

$zip = Join-Path $dist "MHGUPie-$version.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in Get-ChildItem -Recurse -File $stage) {
        $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')   # standard zip paths
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name)
    }
} finally { $archive.Dispose() }
Write-Host "Package: $zip"
