param([string]$EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run\phase9.6f'))

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$productionDb = [IO.Path]::GetFullPath((Join-Path $repo 'Data\db.sys'))
$run = [IO.Path]::GetFullPath($EvidenceDirectory)
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $run.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification output path.' }
if ($run.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Qualification output cannot be under Data.' }

function Get-ProductionState {
    $exists = Test-Path -LiteralPath $productionDb
    $item = if ($exists) { Get-Item -LiteralPath $productionDb } else { $null }
    [ordered]@{
        exists = $exists
        sha256 = if ($exists) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
        size = if ($item) { $item.Length } else { $null }
        lastWriteUtc = if ($item) { $item.LastWriteTimeUtc.ToString('o') } else { $null }
    }
}

function Invoke-QualificationStep([string]$Name, [scriptblock]$Action) {
    $log = Join-Path $run "$Name.log"
    & $Action 2>&1 | Tee-Object -FilePath $log | Out-Host
    $exit = $LASTEXITCODE
    [ordered]@{ name = $Name; exitCode = $exit; log = $log; result = if ($exit -eq 0) { 'PASS' } else { 'FAIL' } }
    if ($exit -ne 0) { throw "$Name failed with exit code $exit." }
}

New-Item -ItemType Directory -Path $run -Force | Out-Null
$before = Get-ProductionState
$steps = [System.Collections.Generic.List[object]]::new()
$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
$filter = 'FullyQualifiedName~Phase96ERehearsalTests|FullyQualifiedName~AuthorityD3Tests|FullyQualifiedName~Phase95B7ActivationBoundaryTests|FullyQualifiedName~ProductionSecurityReadinessFoundationTests|FullyQualifiedName~QualificationEnvironmentTests|FullyQualifiedName~ReportingProjectionCoreTests|FullyQualifiedName~EventStateTransitionTests|FullyQualifiedName~RuntimeDomainFoundationTests'

try {
    $steps.Add((Invoke-QualificationStep 'phase9.6f-focused-tests' {
        dotnet test $testProject -c Release --no-restore --filter $filter --logger "trx;LogFileName=$(Join-Path $run 'phase9.6f-focused.trx')"
    }))
    $steps.Add((Invoke-QualificationStep 'phase9.6e-rehearsal' {
        & (Join-Path $PSScriptRoot 'run-phase9.6e-rehearsal.ps1') -EvidenceDirectory (Join-Path $run 'phase9.6e')
    }))
    $steps.Add((Invoke-QualificationStep 'full-suite' {
        dotnet test (Join-Path $repo 'Rah_Negar.sln') -c Release --no-restore --logger "trx;LogFileName=$(Join-Path $run 'full-suite.trx')"
    }))
    $steps.Add((Invoke-QualificationStep 'solution-build' {
        dotnet build (Join-Path $repo 'Rah_Negar.sln') -c Release --no-restore
    }))
    $steps.Add((Invoke-QualificationStep 'diff-check' {
        $stdout = Join-Path $run 'diff-check.stdout'
        $stderr = Join-Path $run 'diff-check.stderr'
        cmd.exe /d /c "git diff --check 1>""$stdout"" 2>""$stderr"""
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }))
}
finally {
    $after = Get-ProductionState
    $isolation = ($before.exists -eq $after.exists) -and (-not $before.exists -or
        ($before.sha256 -eq $after.sha256 -and $before.size -eq $after.size -and $before.lastWriteUtc -eq $after.lastWriteUtc))
    $result = [ordered]@{
        qualification = 'Phase 9.6F Activation Qualification'
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        branch = (git branch --show-current)
        head = (git rev-parse HEAD)
        productionDb = $productionDb
        productionPreState = $before
        productionPostState = $after
        productionIsolationUnchanged = $isolation
        productionDatabaseUsedAsWritableInput = $false
        targetRoutingEnabled = $false
        productionActivationAuthorized = $false
        productionCutoverAuthorized = $false
        mq07 = 'BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED'
        steps = $steps
        result = if ($isolation -and $steps.Count -eq 5 -and ($steps | Where-Object result -ne PASS).Count -eq 0) { 'PASS' } else { 'FAIL' }
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'phase9.6f-result.json') -Encoding UTF8
    if (-not $isolation) { throw 'Production DB isolation check failed.' }
}

if ($steps.Count -ne 5 -or ($steps | Where-Object result -ne PASS).Count -gt 0) { exit 1 }
Write-Output "Phase 9.6F qualification harness passed. Evidence: $run"
