param([string]$EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run'))

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$productionDb = [IO.Path]::GetFullPath((Join-Path $repo 'Data\db.sys'))
$run = [IO.Path]::GetFullPath($EvidenceDirectory)
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $run.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification output path.' }
if ($run.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Qualification output cannot be under Data.' }

if ($run.Equals($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence directory cannot be the Qualification directory itself.' }
if (Test-Path -LiteralPath $run) { [IO.Directory]::Delete($run, $true) }
New-Item -ItemType Directory -Path $run -Force | Out-Null
function Get-FileEvidence([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return [ordered]@{ exists = $false; path = $path } }
    $item = Get-Item -LiteralPath $path
    return [ordered]@{ exists = $true; path = $path; size = $item.Length; lastWriteUtc = $item.LastWriteTimeUtc.ToString('o'); sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
$beforeExists = Test-Path -LiteralPath $productionDb
$beforeItem = if ($beforeExists) { Get-Item -LiteralPath $productionDb } else { $null }
$beforeHash = if ($beforeExists) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$beforeLength = if ($beforeItem) { $beforeItem.Length } else { $null }
$beforeWrite = if ($beforeItem) { $beforeItem.LastWriteTimeUtc.ToString('o') } else { $null }
$productionAuthorityPath = Join-Path $repo 'DataFiles\authority-state.json'
$productionTransitionPath = Join-Path $repo 'DataFiles\authority-transition.json'
$preEvidence = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    productionDatabase = Get-FileEvidence $productionDb
    productionAuthorityMetadata = Get-FileEvidence $productionAuthorityPath
    productionTransitionMetadata = Get-FileEvidence $productionTransitionPath
}
$preEvidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'production-pre-state.json') -Encoding UTF8

$qualificationData = Join-Path $run 'databases'
& dotnet run --project (Join-Path $repo 'QualificationTool\QualificationTool.csproj') -c Release --no-restore -- $qualificationData | Tee-Object -FilePath (Join-Path $run 'environment.log')
if ($LASTEXITCODE -ne 0) { throw "Qualification environment preparation failed: $LASTEXITCODE" }

$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
& dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~Phase96ERehearsalTests|FullyQualifiedName~AuthorityD3Tests' --logger "trx;LogFileName=$(Join-Path $run 'phase9.6e.trx')" 2>&1 | Tee-Object -FilePath (Join-Path $run 'tests.log')
$testExit = $LASTEXITCODE

$afterExists = Test-Path -LiteralPath $productionDb
$afterItem = if ($afterExists) { Get-Item -LiteralPath $productionDb } else { $null }
$afterHash = if ($afterExists) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$isolation = (-not $beforeExists -and -not $afterExists) -or ($beforeExists -and $afterExists -and $beforeHash -eq $afterHash -and $beforeLength -eq $afterItem.Length -and $beforeWrite -eq $afterItem.LastWriteTimeUtc.ToString('o'))
$postAuthority = Get-FileEvidence $productionAuthorityPath
$postTransition = Get-FileEvidence $productionTransitionPath
$metadataIsolation = ($preEvidence.productionAuthorityMetadata.exists -eq $postAuthority.exists -and (-not $preEvidence.productionAuthorityMetadata.exists -or $preEvidence.productionAuthorityMetadata.sha256 -eq $postAuthority.sha256) -and $preEvidence.productionTransitionMetadata.exists -eq $postTransition.exists -and (-not $preEvidence.productionTransitionMetadata.exists -or $preEvidence.productionTransitionMetadata.sha256 -eq $postTransition.sha256))
$databaseFiles = @(Get-ChildItem -Path (Join-Path $qualificationData '*\db.sys') -File -ErrorAction SilentlyContinue | ForEach-Object { Get-FileEvidence $_.FullName })
$disposableInputsOnly = ($databaseFiles.Count -ge 2 -and ($databaseFiles | Where-Object { $_.path -eq $productionDb }).Count -eq 0)
$postEvidence = [ordered]@{ capturedUtc = [DateTime]::UtcNow.ToString('o'); productionDatabase = Get-FileEvidence $productionDb; productionAuthorityMetadata = $postAuthority; productionTransitionMetadata = $postTransition }
$postEvidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'production-post-state.json') -Encoding UTF8
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
    productionMetadataUnchanged = $metadataIsolation
    rehearsalDataDirectory = $qualificationData
    disposableDatabaseInputsOnly = $disposableInputsOnly
    productionDatabaseUsedAsWritableInput = $false
    focusedTest = 'Phase96ERehearsalTests + AuthorityD3Tests'
    focusedTestExitCode = $testExit
    result = if ($testExit -eq 0 -and $isolation -and $metadataIsolation -and $disposableInputsOnly) { 'PASS' } else { 'FAIL' }
    mq07 = 'BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED'
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'phase9.6e-result.json') -Encoding UTF8
if (-not $isolation) { throw 'Production DB isolation check failed.' }
if (-not $metadataIsolation) { throw 'Production authority metadata isolation check failed.' }
if (-not $disposableInputsOnly) { throw 'Qualification database input isolation check failed.' }
if ($testExit -ne 0) { throw "Phase 9.6E focused qualification failed: $testExit" }
Write-Output "Phase 9.6E qualification passed. Evidence: $run"
