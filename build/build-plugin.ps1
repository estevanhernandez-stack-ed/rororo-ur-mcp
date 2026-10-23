#requires -Version 7.0
<#
.SYNOPSIS
    Build the RoRoRo Ur MCP plugin release artifacts:
    artifacts/plugin.zip + artifacts/manifest.sha256 + artifacts/manifest.json.

.DESCRIPTION
    Per AUTHOR_GUIDE.md, the GitHub release URL contract is:
        <release>/manifest.json
        <release>/manifest.sha256
        <release>/plugin.zip
    where manifest.sha256 is the lowercase SHA-256 of plugin.zip on a single
    line, and plugin.zip contains the runnable EXE + manifest.json + icon.png
    + all deps. RoRoRo's installer refuses to extract if the actual SHA
    doesn't match the .sha256 file.

    Runs locally + in CI. Output lands in ./artifacts/. CI uploads those
    three artifacts to the release.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Runtime
    Target runtime identifier. Default: win-x64.

.PARAMETER SelfContained
    When true, bundles the .NET runtime into the EXE. Larger plugin.zip
    (~80MB) but works on machines without .NET 10 installed system-wide.
    Default: true — Pet Sim 99 clan members can't be assumed to have the
    runtime even though RoRoRo ships with it via MSIX.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"

Write-Host "[build-plugin] root: $root"
Write-Host "[build-plugin] configuration: $Configuration | runtime: $Runtime | self-contained: $SelfContained"

# Version guard. The manifest is what RoRoRo reads (it ships inside plugin.zip), the csproj is
# what the binary reports, and the tag names the release. v0.4.0 shipped with a 0.3.0 manifest
# and a 0.4.0 binary, which would have looped the host's update prompt. All three must agree.
$manifestVersion = (Get-Content "$root/manifest.json" -Raw | ConvertFrom-Json).version
$csprojVersion = ([xml](Get-Content "$root/rororo-ur-mcp.csproj" -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($manifestVersion -ne $csprojVersion) {
    throw "Version mismatch: manifest.json says '$manifestVersion', rororo-ur-mcp.csproj says '$csprojVersion'. Bump both."
}
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$manifestVersion") {
    throw "Version mismatch: tag '$($env:GITHUB_REF_NAME)' does not match manifest version '$manifestVersion' (expected 'v$manifestVersion')."
}
Write-Host "[build-plugin] version: $manifestVersion (manifest, csproj$(if ($env:GITHUB_REF_TYPE -eq 'tag') { ', tag' }) agree)"

if (Test-Path $artifacts) {
    Write-Host "[build-plugin] clearing $artifacts"
    Remove-Item $artifacts -Recurse -Force
}
New-Item $artifacts -ItemType Directory -Force | Out-Null

Write-Host "[build-plugin] publishing..."
dotnet publish "$root/rororo-ur-mcp.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained $SelfContained `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# Drop the manifest + icon inside the zip so RoRoRo's installer finds them
# at the install-root after extraction.
Copy-Item "$root/manifest.json" "$publish/manifest.json" -Force
Copy-Item "$root/icon.png" "$publish/icon.png" -Force

# Strip the .pdb files — they're debug symbols, not needed at runtime.
Get-ChildItem -Path $publish -Filter "*.pdb" -Recurse | Remove-Item -Force

Write-Host "[build-plugin] creating plugin.zip..."
$zip = Join-Path $artifacts "plugin.zip"
Compress-Archive -Path "$publish/*" -DestinationPath $zip -Force
$zipSize = (Get-Item $zip).Length / 1MB
Write-Host ("[build-plugin] plugin.zip: {0:N2} MB" -f $zipSize)

Write-Host "[build-plugin] computing SHA-256..."
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$sha = Join-Path $artifacts "manifest.sha256"
[System.IO.File]::WriteAllText($sha, $hash)
Write-Host "[build-plugin] sha256: $hash"

Copy-Item "$root/manifest.json" (Join-Path $artifacts "manifest.json") -Force

Write-Host ""
Write-Host "[build-plugin] artifacts ready:"
Get-ChildItem $artifacts -File | ForEach-Object {
    Write-Host ("  {0}  ({1:N2} KB)" -f $_.Name, ($_.Length / 1KB))
}
