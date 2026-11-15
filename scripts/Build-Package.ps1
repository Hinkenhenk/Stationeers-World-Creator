<# .SYNOPSIS
Builds WorldCreator from source and packages only our DLL, metadata and installation notes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [string]$BepInExCoreDirectory,
    [string]$ExpectedTag,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository 'dist' }
if (!$BepInExCoreDirectory) { $BepInExCoreDirectory = Join-Path $GameDirectory 'BepInEx/core' }
$managed = @(Get-ChildItem -Path (Join-Path $GameDirectory '*_Data/Managed/Assembly-CSharp.dll') -File)
if ($managed.Count -ne 1) { throw 'Expected one Stationeers managed-assembly directory.' }
$managedDirectory = $managed[0].DirectoryName
foreach ($reference in @('BepInEx.dll', '0Harmony.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $BepInExCoreDirectory $reference))) { throw "Missing build reference: $reference" }
}

[xml]$about = Get-Content -LiteralPath (Join-Path $repository 'WorldCreator/About/About.xml') -Raw
$version = [string]$about.ModMetadata.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw 'Invalid mod version.' }
$buildInfo = Get-Content -LiteralPath (Join-Path $repository 'WorldCreator/BuildInfo.cs') -Raw
if ($buildInfo -notmatch ('Version\s*=\s*"' + [regex]::Escape($version) + '"')) { throw 'BuildInfo and About.xml versions differ.' }
if ($ExpectedTag -and $ExpectedTag -cne "v$version") { throw "Release tag must be v$version." }

& dotnet build (Join-Path $repository 'WorldCreator/WorldCreator.csproj') --configuration Release --nologo "-p:GameDir=$GameDirectory" "-p:GameManagedDir=$managedDirectory" "-p:BepInExCoreDir=$BepInExCoreDirectory"
if ($LASTEXITCODE -ne 0) { throw 'WorldCreator build failed; no package will be published.' }

$dll = Join-Path $repository 'WorldCreator/bin/Release/netstandard2.1/WorldCreator.dll'
$dllBytes = [IO.File]::ReadAllBytes($dll)
# Metadata strings can start on either byte alignment in a managed assembly.
if (![Text.Encoding]::Unicode.GetString($dllBytes).Contains($version) -and
    ![Text.Encoding]::Unicode.GetString($dllBytes, 1, $dllBytes.Length - 1).Contains($version)) { throw 'Built DLL does not contain the expected version.' }
$packageRoot = Join-Path $OutputDirectory "WorldCreator-$version"
$mod = Join-Path $packageRoot 'WorldCreator'
if (Test-Path -LiteralPath $packageRoot) { throw "Package staging directory already exists: $packageRoot" }
New-Item -ItemType Directory -Force -Path (Join-Path $mod 'About') | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $mod 'WorldCreator.dll')
Copy-Item -LiteralPath (Join-Path $repository 'WorldCreator/About/About.xml') -Destination (Join-Path $mod 'About/About.xml')
@"
World Creator $version

Requires Stationeers, BepInEx 5 and StationeersLaunchPad.
Close the game and extract WorldCreator into Documents\My Games\Stationeers\mods.
Enable StationeersWorldCreator in LaunchPad, then choose Create World in the main menu.

Built from source. Game assemblies, game assets and build dependencies are not included.
Source, help and known issues: https://github.com/Hinkenhenk/Stationeers-World-Creator
"@ | Set-Content -LiteralPath (Join-Path $mod 'README.txt') -Encoding utf8

$zip = Join-Path $OutputDirectory "WorldCreator-$version.zip"
Compress-Archive -LiteralPath $mod -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Output "Built $zip"
