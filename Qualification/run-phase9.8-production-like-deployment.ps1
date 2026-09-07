param(
    [string]$DeploymentDirectory = (Join-Path $PSScriptRoot 'qualification-run\phase9.8-production-like-deployment')
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$deployment = [IO.Path]::GetFullPath($DeploymentDirectory)
$app = Join-Path $deployment 'App'
$data = Join-Path $deployment 'Data'
$dataFiles = Join-Path $deployment 'DataFiles'
$evidence = Join-Path $deployment 'Evidence'
$backup = Join-Path $deployment 'Backup'
$restore = Join-Path $deployment 'Restore'
$audit = Join-Path $deployment 'Audit'
$deploymentDb = Join-Path $data 'db.sys'
$runtimeDb = Join-Path $app 'Data\db.sys'
$appDataFiles = Join-Path $app 'DataFiles'
$probeProject = Join-Path $repo 'QualificationTool\Phase98Probe\Phase98Probe.csproj'
$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'

if (-not $deployment.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $deployment.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
    $deployment.Equals($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe qualification deployment path.'
}
if (Test-Path -LiteralPath $deploymentDb) {
    throw "Refusing to overwrite existing qualification database: $deploymentDb"
}

foreach ($directory in @($deployment, $app, $data, $dataFiles, $evidence, $backup, $restore, $audit)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    $log = Join-Path $evidence ($Name + '.log')
    & $Action 2>&1 | Tee-Object -FilePath $log | Out-Host
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { throw "$Name failed with exit code $exitCode." }
    return [ordered]@{ name = $Name; exitCode = $exitCode; log = $log; result = 'PASS' }
}

function Get-FileEvidence([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        return [ordered]@{ path = $full; exists = $false }
    }
    $item = Get-Item -LiteralPath $full
    return [ordered]@{
        path = $full
        exists = $true
        sizeBytes = $item.Length
        sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
        lastWriteUtc = $item.LastWriteTimeUtc.ToString('o')
    }
}

$steps = [System.Collections.Generic.List[object]]::new()
$steps.Add((Invoke-Step 'normal-build' { dotnet build (Join-Path $repo 'Rah_Negar.sln') -c Release --no-restore }))
$steps.Add((Invoke-Step 'publish-release-application' { dotnet publish (Join-Path $repo 'Rah_Negar.csproj') -c Release --no-restore -o $app }))
$steps.Add((Invoke-Step 'build-phase98-probe' { dotnet build $probeProject -c Release --no-restore }))
$steps.Add((Invoke-Step 'initialize-isolated-deployment' {
    dotnet run --project $probeProject -c Release --no-build -- initialize $deploymentDb $evidence $app
}))

New-Item -ItemType Directory -Path (Split-Path -Parent $runtimeDb) -Force | Out-Null
Copy-Item -LiteralPath $deploymentDb -Destination $runtimeDb -Force
foreach ($suffix in @('-wal', '-shm')) {
    $sourceSidecar = $deploymentDb + $suffix
    if (Test-Path -LiteralPath $sourceSidecar) { Copy-Item -LiteralPath $sourceSidecar -Destination ($runtimeDb + $suffix) -Force }
}
Copy-Item -LiteralPath (Join-Path $appDataFiles '*') -Destination $dataFiles -Recurse -Force

$steps.Add((Invoke-Step 'authority-routing-readback' {
    dotnet run --project $probeProject -c Release --no-build -- startup $appDataFiles $evidence
}))
$steps.Add((Invoke-Step 'fence-write-drain-qualification' {
    dotnet run --project $probeProject -c Release --no-build -- fence (Join-Path $audit 'writer-fence.lock') $evidence
}))
$steps.Add((Invoke-Step 'backup-restore-qualification' {
    dotnet run --project $probeProject -c Release --no-build -- restore $runtimeDb $evidence $backup $restore
}))
$steps.Add((Invoke-Step 'audit-retention-tamper-qualification' {
    dotnet run --project $probeProject -c Release --no-build -- audit (Join-Path $appDataFiles 'authority-audit.jsonl') $evidence (Join-Path $audit 'authority-audit-tampered.jsonl')
}))

$focusedFilter = 'FullyQualifiedName~Phase97ProductionExecutionTests|FullyQualifiedName~Phase96ERehearsalTests|FullyQualifiedName~Phase95B6ProductionMigrationExecutorTests|FullyQualifiedName~QualificationEnvironmentTests'
$steps.Add((Invoke-Step 'focused-qualification-tests' {
    dotnet test $testProject -c Release --no-restore --filter $focusedFilter --logger "trx;LogFileName=$(Join-Path $evidence 'phase9.8-focused.trx')"
}))
$steps.Add((Invoke-Step 'full-test-suite' {
    dotnet test (Join-Path $repo 'Rah_Negar.sln') -c Release --no-restore --logger "trx;LogFileName=$(Join-Path $evidence 'phase9.8-full.trx')"
}))
$steps.Add((Invoke-Step 'diff-check' { cmd.exe /d /c "git diff --check 2>NUL" }))

$assemblyName = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $app 'Rah_Negar.dll'))
$applicationEvidence = [ordered]@{
    executable = Get-FileEvidence (Join-Path $app 'Rah_Negar.exe')
    dll = Get-FileEvidence (Join-Path $app 'Rah_Negar.dll')
    assemblyVersion = $assemblyName.Version.ToString()
    targetFramework = 'net8.0-windows'
    sourceHead = (git rev-parse HEAD).Trim()
    sourceBranch = (git branch --show-current).Trim()
    publishedAppDirectory = [IO.Path]::GetFullPath($app)
}
$applicationEvidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'application-evidence.json') -Encoding UTF8

$deploymentManifest = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    classification = 'PRODUCTION-LIKE QUALIFICATION DEPLOYMENT'
    production = $false
    authoritative = $false
    targetRoutingEnabled = $false
    productionActivationAuthorized = $false
    productionCutoverAuthorized = $false
    realProductionInstallationEvidence = 'NOT AVAILABLE'
    paths = [ordered]@{
        deploymentRoot = [IO.Path]::GetFullPath($deployment)
        appDirectory = [IO.Path]::GetFullPath($app)
        packageDatabase = [IO.Path]::GetFullPath($deploymentDb)
        runtimeDatabase = [IO.Path]::GetFullPath($runtimeDb)
        configuredAuthorityState = [IO.Path]::GetFullPath((Join-Path $appDataFiles 'authority-state.json'))
        configuredAuthorityTransition = [IO.Path]::GetFullPath((Join-Path $appDataFiles 'authority-transition.json'))
        configuredAudit = [IO.Path]::GetFullPath((Join-Path $appDataFiles 'authority-audit.jsonl'))
        evidenceDirectory = [IO.Path]::GetFullPath($evidence)
        backupDirectory = [IO.Path]::GetFullPath($backup)
        restoreDirectory = [IO.Path]::GetFullPath($restore)
        auditDirectory = [IO.Path]::GetFullPath($audit)
    }
    database = [ordered]@{
        package = Get-FileEvidence $deploymentDb
        runtime = Get-FileEvidence $runtimeDb
        packageAndRuntimeHashesMatch = (Get-FileHash -LiteralPath $deploymentDb -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $runtimeDb -Algorithm SHA256).Hash
    }
    application = $applicationEvidence
    steps = $steps
    noActivationAuthorizationArtifact = (@(Get-ChildItem -Path $appDataFiles -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'authorization|activation' }).Count -eq 0)
    aggregate = 'NOT_ELIGIBLE_FOR_ACTIVATION_DECISION'
}
$deploymentManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $evidence 'deployment-manifest.json') -Encoding UTF8

Write-Output "Phase 9.8 production-like qualification deployment passed. Evidence: $deployment"
