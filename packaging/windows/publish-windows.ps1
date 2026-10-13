#Requires -Version 7
<#
.SYNOPSIS
  Publishes the BASIC terminal environment for Windows and installs the
  `basic` launcher so you can start it from any terminal.

.DESCRIPTION
  Publishes Basic.Cli to dist/basic and drops in basic.cmd next to it.
  Add dist\basic to your PATH (or copy the folder somewhere permanent and add
  that) so `basic` works from any prompt.

.EXAMPLE
  pwsh -File packaging/windows/publish-windows.ps1
#>
[CmdletBinding()]
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$out = Join-Path $repoRoot "dist\basic"

Write-Host "==> Publishing Basic.Cli to $out"
dotnet publish (Join-Path $repoRoot "src\Basic.Cli\Basic.Cli.fsproj") `
    -c $Configuration -o $out -v quiet --nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The apphost embeds the managed dll name, so renaming the launcher is safe.
$exe = Join-Path $out "Basic.Cli.exe"
$basicExe = Join-Path $out "basic.exe"

if ((Test-Path $exe) -and -not (Test-Path $basicExe)) {
    Move-Item $exe $basicExe -Force
}

Copy-Item (Join-Path $PSScriptRoot "basic.cmd") $out -Force

Write-Host ""
Write-Host "To use the 'basic' command from any terminal:"
Write-Host "  1) Add this folder to your PATH:  $out"
Write-Host '     (setx PATH "$env:PATH;<path>' -NoNewline
Write-Host '"   - reopen terminals after)'
Write-Host "  2) Then just type:  basic"
Write-Host ""
Write-Host "Already published - quick test:  & '$out\basic.cmd'"