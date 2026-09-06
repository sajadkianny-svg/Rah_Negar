param([string]$EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run\phase9.7-final'))

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$run = [IO.Path]::GetFullPath($EvidenceDirectory)
$productionDb = [IO.Path]::GetFullPath((Join-Path $repo 'Data\db.sys'))
$productionMetadata = @(
    [IO.Path]::GetFullPath((Join-Path $repo 'DataFiles\authority-state.json')),
    [IO.Path]::GetFullPath((Join-Path $repo 'DataFiles\authority-transition.json')),
    [IO.Path]::GetFullPath((Join-Path $repo 'DataFiles\authority-audit.jsonl')),
    [IO.Path]::GetFullPath((Join-Path $repo 'DataFiles\activation-audit.jsonl'))
)
if (-not $run.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $run.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Unsafe qualification output path.' }
if ($run.Equals($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence directory cannot be Qualification itself.' }
New-Item -ItemType Directory -Path $run -Force | Out-Null

function Get-FileEvidence([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return [ordered]@{ path = $path; exists = $false } }
    $item = Get-Item -LiteralPath $path
    return [ordered]@{ path = $path; exists = $true; size = $item.Length; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; lastWriteUtc = $item.LastWriteTimeUtc.ToString('o') }
}
function Get-ProductionState {
    [ordered]@{
        capturedUtc = [DateTime]::UtcNow.ToString('o')
        database = Get-FileEvidence $productionDb
        authorityMetadata = @($productionMetadata | ForEach-Object { Get-FileEvidence $_ })
    }
}
function Invoke-Step([string]$name, [scriptblock]$action) {
    $log = Join-Path $run ($name + '.log')
    & $action 2>&1 | Tee-Object -FilePath $log | Out-Host
    $exit = $LASTEXITCODE
    if ($exit -ne 0) { throw "$name failed with exit code $exit." }
    return [ordered]@{ name = $name; exitCode = $exit; log = $log; result = 'PASS' }
}

$before = Get-ProductionState
$before | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'production-pre-state.json') -Encoding UTF8
$steps = [System.Collections.Generic.List[object]]::new()
$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
$qualificationData = Join-Path $run 'databases'
try {
    $steps.Add((Invoke-Step 'qualification-environment' { dotnet run --project (Join-Path $repo 'QualificationTool\QualificationTool.csproj') -c Release --no-restore -- $qualificationData }))
    $steps.Add((Invoke-Step 'phase9.7-focused-tests' { dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~Phase97ProductionExecutionTests|FullyQualifiedName~AuthorityD3Tests|FullyQualifiedName~Phase96ERehearsalTests' --logger "trx;LogFileName=$(Join-Path $run 'phase9.7-focused.trx')" }))
    $steps.Add((Invoke-Step 'production-rejection-and-isolation-tests' { dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~ProductionActivation|FullyQualifiedName~QualificationEnvironmentTests|FullyQualifiedName~ProductionSecurityReadinessFoundationTests' --logger "trx;LogFileName=$(Join-Path $run 'phase9.7-rejection.trx')" }))
}
finally {
    $after = Get-ProductionState
    $beforeDatabase = $before.database | ConvertTo-Json -Depth 8 -Compress
    $afterDatabase = $after.database | ConvertTo-Json -Depth 8 -Compress
    $beforeMetadata = $before.authorityMetadata | ConvertTo-Json -Depth 8 -Compress
    $afterMetadata = $after.authorityMetadata | ConvertTo-Json -Depth 8 -Compress
    $isolated = $beforeDatabase -eq $afterDatabase -and $beforeMetadata -eq $afterMetadata
    $databaseInputs = @(Get-ChildItem -Path (Join-Path $qualificationData '*\db.sys') -File -ErrorAction SilentlyContinue)
    $disposableOnly = $databaseInputs.Count -ge 2 -and (@($databaseInputs | Where-Object { $_.FullName -eq $productionDb }).Count -eq 0)
    $after | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'production-post-state.json') -Encoding UTF8
    $result = [ordered]@{
        qualification = 'Phase 9.7 Final Activation Blocker Qualification'
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        productionPreState = $before
        productionPostState = $after
        productionIsolationUnchanged = $isolated
        disposableDatabaseInputsOnly = $disposableOnly
        productionDatabaseUsedAsWritableInput = $false
        targetRoutingEnabled = $false
        productionActivationAuthorized = $false
        productionCutoverAuthorized = $false
        mq07 = 'BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED'
        independentHumanReview = 'NOT PERFORMED / UNAVAILABLE'
        steps = $steps
        result = if ($isolated -and $disposableOnly -and $steps.Count -eq 3 -and ($steps | Where-Object result -ne PASS).Count -eq 0) { 'PASS' } else { 'FAIL' }
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $run 'phase9.7-result.json') -Encoding UTF8
    if (-not $isolated) { throw 'Production DB, authority, transition, or audit metadata changed.' }
    if (-not $disposableOnly) { throw 'Qualification used a non-disposable database input.' }
}
if ($steps.Count -ne 3 -or ($steps | Where-Object result -ne PASS).Count -gt 0) { exit 1 }
Write-Output "Phase 9.7 final qualification passed. Evidence: $run"
