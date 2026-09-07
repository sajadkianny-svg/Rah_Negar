param([Parameter(Mandatory=$true)][string]$PublishDirectory)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PublishDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Publish directory not found: $root" }
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File)
$forbidden = @($files | Where-Object {
    $_.Extension -in '.pdb','.db','.sqlite','.sys','.cs','.csproj' -or
    $_.Name -match '(?i)(createdump|credential|secret|token|password|qualification|test\.dll|tests\.dll)'
})
if ($forbidden.Count -ne 0) {
    $forbidden | ForEach-Object { Write-Error "Forbidden installer payload: $($_.FullName)" }
    exit 1
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'Rah_Negar.exe'))) { throw 'Rah_Negar.exe is missing from the payload.' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'AppIcon.ico'))) { throw 'AppIcon.ico is missing from the payload.' }
Write-Output "Installer payload audit passed: $($files.Count) files"
