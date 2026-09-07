param([switch]$KeepArtifacts)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$setup = [IO.Path]::GetFullPath((Join-Path $repo 'Delivery\Installer\RahNegar-Setup-x64.exe'))
$run = [IO.Path]::GetFullPath((Join-Path $repo 'Qualification\installer-validation'))
$install = Join-Path $run 'App'
$programData = Join-Path $env:ProgramData 'RahNegar'
$marker = Join-Path $programData 'Data\batch2-lifecycle-marker.txt'
if (-not (Test-Path -LiteralPath $setup)) { throw "Installer not found: $setup" }
if (Test-Path -LiteralPath $run) { Remove-Item -LiteralPath $run -Recurse -Force }
New-Item -ItemType Directory -Path $run -Force | Out-Null

function Invoke-Setup([string[]]$arguments) {
    $process = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer failed with exit code $($process.ExitCode)." }
}
function Assert-Exists([string]$path) { if (-not (Test-Path -LiteralPath $path)) { throw "Expected path is absent: $path" } }

Invoke-Setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=$install")
Assert-Exists (Join-Path $install 'Rah_Negar.exe')
$startMenuLinks = @(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\RahNegar\RahNegar.lnk'),
    (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\RahNegar\RahNegar.lnk')
)
if (-not ($startMenuLinks | Where-Object { Test-Path -LiteralPath $_ })) {
    throw "Expected Start Menu shortcut is absent: $($startMenuLinks -join '; ')"
}
New-Item -ItemType Directory -Path (Split-Path $marker) -Force | Out-Null
Set-Content -LiteralPath $marker -Value 'batch2-preservation-marker' -Encoding ASCII

$app = Start-Process -FilePath (Join-Path $install 'Rah_Negar.exe') -WorkingDirectory $install -PassThru
Start-Sleep -Seconds 3
$createdDb = Test-Path -LiteralPath (Join-Path $programData 'Data\db.sys')
if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }
if (-not $createdDb) { throw 'First run did not create the canonical database.' }

Invoke-Setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=$install")
if ((Get-Content -LiteralPath $marker -Raw) -ne "batch2-preservation-marker`r`n") { throw 'Upgrade changed operational data.' }
Invoke-Setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=$install")
Assert-Exists (Join-Path $programData 'Data\db.sys')

$uninstaller = Join-Path $install 'unins000.exe'
Assert-Exists $uninstaller
$uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
if ($uninstall.ExitCode -ne 0) { throw "Uninstaller failed with exit code $($uninstall.ExitCode)." }
if (Test-Path -LiteralPath $install) { throw 'Uninstall did not remove application binaries.' }
Assert-Exists $marker
Assert-Exists (Join-Path $programData 'Data\db.sys')
Write-Output "Installer lifecycle PASS: fresh first-run, upgrade, reinstall, uninstall, data preservation ($run)"
if (-not $KeepArtifacts -and (Test-Path -LiteralPath $run)) { Remove-Item -LiteralPath $run -Recurse -Force }
