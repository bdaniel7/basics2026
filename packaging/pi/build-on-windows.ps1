#Requires -Version 7
<#
.SYNOPSIS
  Builds a Raspberry Pi (arm64) bundle for the BASIC Spectrum shell.

.DESCRIPTION
  Cross-publishes Basic.Pi self-contained for linux-arm64, drops in the native
  raylib 6.0 library for arm64 (Raylib-cs ships none for this architecture),
  copies the sample programs and the installer, and produces a tarball you
  copy to the Pi.

.EXAMPLE
  pwsh -File packaging/pi/build-on-windows.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RaylibVersion = "6.0",
    [switch]$SkipRaylib
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$distRoot = Join-Path $repoRoot "dist"
$bundle = Join-Path $distRoot "pi"
$appDir = Join-Path $bundle "app"
$tarball = Join-Path $distRoot "basic-pi-arm64.tar.gz"

Write-Host "==> Cleaning $bundle"
if (Test-Path $bundle) { Remove-Item -Recurse -Force $bundle }
New-Item -ItemType Directory -Force -Path $appDir | Out-Null

Write-Host "==> Publishing Basic.Pi (self-contained, linux-arm64)"
dotnet publish (Join-Path $repoRoot "src/Basic.Pi/Basic.Pi.fsproj") `
    -c $Configuration -r linux-arm64 --self-contained true `
    -p:PublishSingleFile=false -o $appDir -v quiet --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Naming the app 'basic' (consistent with the Windows build)"
# The apphost embeds the managed dll name (Basic.Pi.dll), so renaming the
# launcher itself is safe.
$apphost = Join-Path $appDir "Basic.Pi"
if (Test-Path $apphost) { Move-Item -Force $apphost (Join-Path $appDir "basic") }
else { throw "Expected apphost Basic.Pi was not produced by publish" }

if (-not $SkipRaylib) {
    Write-Host "==> Fetching native raylib $RaylibVersion (linux-arm64)"
    $soPath = Join-Path $appDir "libraylib.so"
    $url = "https://github.com/raysan5/raylib/releases/download/$RaylibVersion/raylib-${RaylibVersion}_linux_arm64.tar.gz"
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) "raylib-${RaylibVersion}_linux_arm64.tar.gz"
    Invoke-WebRequest -Uri $url -OutFile $tmp

    $extract = Join-Path ([System.IO.Path]::GetTempPath()) "raylib-arm64-extract"
    if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
    New-Item -ItemType Directory -Force -Path $extract | Out-Null

    # The versioned regular file is the real library (the un-suffixed names
    # are symlinks that Windows tar cannot create). Pick a regular-file entry.
    $member = tar -tvzf $tmp | Where-Object { $_ -match '^-.*libraylib\.so[^\s]*$' } |
        ForEach-Object { ($_ -split '\s+')[-1] } |
        Select-Object -First 1
    if (-not $member) { throw "Could not find the raylib shared library in the archive" }
    tar -xzf $tmp -C $extract $member

    $so = Join-Path $extract $member
    if (-not (Test-Path $so)) { throw "Extraction produced no libraylib.so" }
    Copy-Item $so $soPath -Force
    Write-Host "    -> $soPath ($([math]::Round((Get-Item $soPath).Length/1MB,2)) MB)"
}

Write-Host "==> Adding sample programs and installer"
Copy-Item (Join-Path $repoRoot "programs") $bundle -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot "install.sh") $bundle -Force
Copy-Item (Join-Path $PSScriptRoot "README.md") $bundle -Force

Write-Host "==> Creating $tarball"
tar -czf $tarball -C $bundle app programs install.sh README.md
if ($LASTEXITCODE -ne 0) { throw "tar failed" }

$size = [math]::Round((Get-Item $tarball).Length / 1MB, 1)
Write-Host ""
Write-Host "Done. Bundle: $tarball ($size MB)"
Write-Host "Copy it to the Pi, then:  tar -xzf basic-pi-arm64.tar.gz && sudo bash install.sh"
