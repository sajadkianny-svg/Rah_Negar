param(
    [ValidateSet(3, 4, 5)]
    [int] $UnitCount = 3,
    [ValidateSet(100, 125, 150)]
    [int] $DpiPercent = 100,
    [switch] $PrepareOnly
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$runRoot = [IO.Path]::GetFullPath((Join-Path $qualificationRoot 'qualification-run\final-ui-acceptance'))
$sessionId = "generic-{0}-units-{1}-{2}" -f $UnitCount, $DpiPercent, ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
$sessionRoot = [IO.Path]::GetFullPath((Join-Path $runRoot $sessionId))

if (-not $sessionRoot.StartsWith($qualificationRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe UI qualification session path.'
}
New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null

$release = [IO.Path]::GetFullPath((Join-Path $repo 'bin\Release\net8.0-windows'))
$exe = Join-Path $sessionRoot 'app\Rah_Negar.exe'
$fixture = Join-Path $sessionRoot "fixture\Generic-$UnitCount\db.sys"
$dataRoot = Join-Path $sessionRoot 'isolated-data'
$sessionEvidence = Join-Path $sessionRoot 'session-evidence.json'

if (-not (Test-Path -LiteralPath (Join-Path $release 'Rah_Negar.exe'))) {
    throw 'Release build is required before starting native UI acceptance.'
}

$toolProject = Join-Path $repo 'QualificationTool\QualificationTool.csproj'
$toolAssembly = Join-Path $repo 'QualificationTool\bin\Release\net8.0-windows\QualificationTool.dll'
if (-not (Test-Path -LiteralPath $toolAssembly)) {
    & dotnet build $toolProject -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw "QualificationTool build failed with exit code $LASTEXITCODE." }
}
$fixtureDirectory = Join-Path $sessionRoot 'fixture'
New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
& dotnet run --project $toolProject -c Release --no-build --no-restore -- --generic $fixtureDirectory $UnitCount
if ($LASTEXITCODE -ne 0) { throw "Qualification fixture preparation failed: $LASTEXITCODE" }

New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Data') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dataRoot 'DataFiles') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Backups') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Logs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Recovery') -Force | Out-Null
Copy-Item -LiteralPath $fixture -Destination (Join-Path $dataRoot 'Data\db.sys') -Force

$appDirectory = Join-Path $sessionRoot 'app'
New-Item -ItemType Directory -Path $appDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $release '*') -Destination $appDirectory -Recurse -Force
$exe = Join-Path $appDirectory 'Rah_Negar.exe'

$inventory = @(
    'Startup Wizard', 'Login', 'Main / Dashboard', 'Operational / Live screen',
    'FrmRecords', 'Reports', 'Settings', 'Backup', 'Restore', 'Recovery',
    'Management authorization dialogs', 'Confirmation and error dialogs'
)
$evidence = [ordered]@{
    session = $sessionId
    profileKind = 'Generic'
    profileName = "Generic Profile ($UnitCount Units)"
    unitCount = $UnitCount
    requestedDpiPercent = $DpiPercent
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    productionDataTouched = $false
    qualificationRoot = $dataRoot
    loginProfile = 'Qualification Shift'
    loginInstruction = 'Use the qualification-only credential documented in the acceptance checklist.'
    forms = @($inventory | ForEach-Object { [ordered]@{ form = $_; status = 'NOT_EVALUATED_BY_AUTOMATION' } })
    process = [ordered]@{ launched = $false; exitCode = $null; startedUtc = $null; endedUtc = $null }
}
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $sessionEvidence -Encoding UTF8

Write-Host ''
Write-Host '=== Rah_Negar native UI acceptance session ===' -ForegroundColor Cyan
Write-Host ("Profile: Generic ({0} units)   Requested Windows scale: {1}%" -f $UnitCount, $DpiPercent) -ForegroundColor Yellow
Write-Host 'Use Windows Display Settings to set the requested scale before observing the forms.'
Write-Host 'This session uses an isolated qualification database. Production data is not an input.'
Write-Host ("Evidence: {0}" -f $sessionEvidence)
Write-Host 'Qualification login: user profile = Qualification Shift; credential is listed only in the checklist.'
Write-Host ''

if ($PrepareOnly) {
    Write-Host "Prepared isolated session without launching the application: $sessionRoot"
    exit 0
}

$startUtc = [DateTime]::UtcNow
$psi = [Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $exe
$psi.WorkingDirectory = $appDirectory
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['RAH_NEGAR_QUALIFICATION_ROOT'] = $dataRoot
$psi.EnvironmentVariables['RAH_NEGAR_UI_ACCEPTANCE_DPI'] = "$DpiPercent"
$process = [Diagnostics.Process]::Start($psi)
if ($null -eq $process) { throw 'The qualification application did not start.' }
$evidence.process.launched = $true
$evidence.process.startedUtc = $startUtc.ToString('o')
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $sessionEvidence -Encoding UTF8

$process.WaitForExit()
$endUtc = [DateTime]::UtcNow
$evidence.process.exitCode = $process.ExitCode
$evidence.process.endedUtc = $endUtc.ToString('o')
$evidence.elapsedMilliseconds = [int64]($endUtc - $startUtc).TotalMilliseconds
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $sessionEvidence -Encoding UTF8

Write-Host "Session closed. Human checklist remains NOT EVALUATED until observations are recorded: $sessionEvidence"
exit $process.ExitCode
