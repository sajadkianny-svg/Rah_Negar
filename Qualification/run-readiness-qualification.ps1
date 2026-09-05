param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'qualification-data'),
    [string] $EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-evidence'),
    [switch] $SkipPreparation
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)

function Assert-IsolatedPath([string] $path) {
    $full = [IO.Path]::GetFullPath($path)
    if ($full.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        [IO.Path]::GetFileName($full).Equals('Data', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing qualification path under an application Data directory: $full"
    }
}

Assert-IsolatedPath $output
Assert-IsolatedPath $evidence
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

if (-not $SkipPreparation) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'prepare-qualification.ps1') -OutputDirectory $output
    if ($LASTEXITCODE -ne 0) { throw "Fixture preparation failed with exit code $LASTEXITCODE" }
}

$filters = [ordered]@{
    'MQ-01' = 'ManagedSqliteBackupRestoreBoundaryTests'
    'MQ-02' = 'Phase95B4SecurityCompositionTests'
    'MQ-03' = 'Phase95B5ProvisioningTests'
    'MQ-04' = 'Phase95B6ProductionMigrationExecutorTests'
    'MQ-05' = 'Phase95B7ActivationBoundaryTests'
}
$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
$records = [System.Collections.Generic.List[object]]::new()
foreach ($item in $filters.Keys) {
    $trx = Join-Path $evidence "$item.trx"
    & dotnet test $testProject -c Release --no-restore --filter "FullyQualifiedName~$($filters[$item])" --logger "trx;LogFileName=$trx"
    $exitCode = $LASTEXITCODE
    $records.Add([pscustomobject]@{
        qualificationId = $item
        classification = 'service-level qualification support'
        automatedResult = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        manualResult = 'READY TO EXECUTE NOW'
        evidence = (Split-Path $trx -Leaf)
        note = 'Automated support does not constitute manual PASS; operator/reviewer must inspect the sanitized TRX and sign off.'
    })
    if ($exitCode -ne 0) { throw "$item support suite failed with exit code $exitCode" }
}

$manifest = [pscustomobject]@{
    schema = 'phase9.5c2-qualification-readiness-v1'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    stations = @('Rasht', 'Ramsar')
    fixtureDirectory = $output
    evidenceDirectory = $evidence
    productionDatabaseUsed = $false
    productionAuthorityChanged = $false
    targetAuthorityActivated = $false
    items = $records
    uiItems = @('MQ-06','MQ-07','MQ-08','MQ-09','MQ-10','MQ-11','MQ-12')
    uiStatus = 'READY TO EXECUTE NOW - human desktop observation and screenshots required'
    credentials = 'Synthetic qualification profile only; no credential material is emitted.'
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'readiness-manifest.json') -Encoding UTF8
Write-Output "Qualification readiness prepared. Evidence: $evidence"
