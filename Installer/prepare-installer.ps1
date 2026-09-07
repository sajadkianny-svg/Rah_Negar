param([switch]$BuildInstaller)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'publish'))
$delivery = [IO.Path]::GetFullPath((Join-Path $repo 'Delivery\Installer'))

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
New-Item -ItemType Directory -Path $publish -Force | Out-Null
New-Item -ItemType Directory -Path $delivery -Force | Out-Null

$publishArgs = @((Join-Path $repo 'Rah_Negar.csproj'), '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'true', '--no-restore', '-p:PublishSingleFile=false',
    '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $publish)
& dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath (Join-Path $repo 'DataFiles\AppIcon.ico') -Destination (Join-Path $publish 'AppIcon.ico') -Force

Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object {
    $_.Extension -in '.pdb','.db','.sqlite','.sys' -or
    $_.Name -match '(?i)(createdump|credential|secret|token|password|qualification)'
} | Remove-Item -Force

& (Join-Path $PSScriptRoot 'validate-installer.ps1') -PublishDirectory $publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($BuildInstaller) {
    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if (-not $iscc) { throw 'Inno Setup ISCC.exe is not installed or is not on PATH.' }
    & $iscc.Source (Join-Path $PSScriptRoot 'RahNegar.iss')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $setup = Join-Path $delivery 'RahNegar-Setup-x64.exe'
    if (-not (Test-Path -LiteralPath $setup)) { throw "Installer output was not produced: $setup" }
    (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash |
        Set-Content -LiteralPath ($setup + '.sha256') -Encoding ASCII
}

Write-Output "Prepared clean installer payload: $publish"
