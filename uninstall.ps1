[CmdletBinding()]
param([string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\mcterm'))

$ErrorActionPreference = 'Stop'
$resolvedInstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($null -eq $userPath) { $userPath = '' }
$entries = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$remaining = $entries | Where-Object {
    try { -not [IO.Path]::GetFullPath($_).TrimEnd('\').Equals($resolvedInstallDir, [StringComparison]::OrdinalIgnoreCase) } catch { $true }
}
[Environment]::SetEnvironmentVariable('Path', ($remaining -join ';'), 'User')

if (Test-Path -LiteralPath $resolvedInstallDir -PathType Container) {
    $currentDirectory = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')
    if ($currentDirectory.StartsWith($resolvedInstallDir, [StringComparison]::OrdinalIgnoreCase)) { Set-Location $env:TEMP }
    Remove-Item -LiteralPath $resolvedInstallDir -Recurse -Force
}

Write-Host 'mcterm was uninstalled. Open a new terminal to refresh PATH.' -ForegroundColor Green
