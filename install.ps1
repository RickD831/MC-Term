[CmdletBinding()]
param([string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\mcterm'))

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'mcterm is supported only on Windows.' }

$sourceExe = Join-Path $PSScriptRoot 'mcterm.exe'
$sourceUninstaller = Join-Path $PSScriptRoot 'uninstall.ps1'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "mcterm.exe was not found beside install.ps1: $sourceExe" }

$resolvedInstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
New-Item -ItemType Directory -Force -Path $resolvedInstallDir | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $resolvedInstallDir 'mcterm.exe') -Force
if (Test-Path -LiteralPath $sourceUninstaller -PathType Leaf) { Copy-Item -LiteralPath $sourceUninstaller -Destination (Join-Path $resolvedInstallDir 'uninstall.ps1') -Force }

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($null -eq $userPath) { $userPath = '' }
$entries = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$alreadyPresent = $entries | Where-Object {
    try { [IO.Path]::GetFullPath($_).TrimEnd('\').Equals($resolvedInstallDir, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
}
if (-not $alreadyPresent) { [Environment]::SetEnvironmentVariable('Path', ((@($entries) + $resolvedInstallDir) -join ';'), 'User') }

Write-Host "mcterm installed to $resolvedInstallDir" -ForegroundColor Green
Write-Host 'Open a new terminal, then run: mcterm'
