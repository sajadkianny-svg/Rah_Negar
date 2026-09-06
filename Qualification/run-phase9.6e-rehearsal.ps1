param([string]$EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run'))

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$productionDb = [IO.Path]::GetFullPath((Join-Path $repo 'Data\db.sys'))
$run = [IO.Path]::GetFullPath($EvidenceDirectory)
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $run.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification output path.' }
if ($run.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Qualification output cannot be under Data.' }

New-Item -ItemType Directory -Path $run -Force | Out-Null
$beforeExists = Test-Path -LiteralPath $productionDb
$beforeItem = if ($beforeExists) { Get-Item -LiteralPath $productionDb } else { $null }
$beforeHash = if ($beforeExists) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$beforeLength = if ($beforeItem) { $beforeItem.Length } else { $null }
$beforeWrite = if ($beforeItem) { $beforeItem.LastWriteTimeUtc.ToString('o') } else { $null }

$qualificationData = Join-Path $run 'databases'
& dotnet run --project (Join-Path $repo 'QualificationTool\QualificationTool.csproj') -c Release --no-restore -- $qualificationData | Tee-Object -FilePath (Join-Path $run 'environment.log')
if ($LASTEXITCODE -ne 0) { throw "Qualification environment preparation failed: $LASTEXITCODE" }

$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
& dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~Phase96ERehearsalTests' --logger "trx;LogFileName=$(Join-Path $run 'phase9.6e.trx')" 2>&1 | Tee-Object -FilePath (Join-Path $run 'tests.log')
$testExit = $LASTEXITCODE

$afterExists = Test-Path -LiteralPath $productionDb
$afterItem = if ($afterExists) { Get-Item -LiteralPath $productionDb } else { $null }
$afterHash = if ($afterExists) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$isolation = (-not $beforeExists -and -not $afterExists) -or ($beforeExists -and $afterExists -and $beforeHash -eq $afterHash -and $beforeLength -eq $afterItem.Length -and $beforeWrite -eq $afterItem.LastWriteTimeUtc.ToString('o'))
$result = [ordered]@{
    qualification = 'Phase 9.6E Production-like Rehearsal'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    productionDbPath = $productionDb
    productionDbExistedBefore = $beforeExists
    productionDbSha256Before = $beforeHash
    productionDbSha256After = $afterHash
    productionDbSizeBefore = $beforeLength
    productionDbSizeAfter = if ($afterItem) { $afterItem.Length } else { $null }
    productionDbLastWriteUtcBefore = $beforeWrite
    productionDbLastWriteUtcAfter = if ($afterItem) { $afterItem.LastWriteTimeUtc.ToString('o') } else { $null }
    productionIsolationUnchanged = $isolation
    rehearsalDataDirectory = $qualificationData
    productionDatabaseUsedAsWritableInput = $false
    focusedTest = 'Phase96ERehearsalTests'
    focusedTestExitCode = $testExit
    result = if ($testExit -eq 0 -and $isolation) { 'PASS' } else { 'FAIL' }
    mq07 = 'BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED'
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'phase9.6e-result.json') -Encoding UTF8
if (-not $isolation) { throw 'Production DB isolation check failed.' }
if ($testExit -ne 0) { throw "Phase 9.6E focused qualification failed: $testExit" }
Write-Output "Phase 9.6E qualification passed. Evidence: $run"
