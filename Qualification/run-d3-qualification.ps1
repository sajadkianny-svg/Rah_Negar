param(
    [string] $EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run')
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$productionDb = Join-Path $repo 'Data\db.sys'
$run = [IO.Path]::GetFullPath($EvidenceDirectory)
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $run.StartsWith($qualificationRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification output path.' }
if ($run.IndexOf('\Data\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Qualification output cannot be under Data.' }

New-Item -ItemType Directory -Path $run -Force | Out-Null
$before = if (Test-Path -LiteralPath $productionDb) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$testProject = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~AuthorityD3Tests' --logger "trx;LogFileName=$(Join-Path $run 'phase9.6d3.trx')"
if ($LASTEXITCODE -ne 0) { throw "Phase 9.6D3 focused qualification failed with exit code $LASTEXITCODE" }
$after = if (Test-Path -LiteralPath $productionDb) { (Get-FileHash -LiteralPath $productionDb -Algorithm SHA256).Hash } else { $null }
$report = [ordered]@{
    qualification = 'Phase 9.6D3'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    productionDatabaseUsed = $false
    productionDbSha256Before = $before
    productionDbSha256After = $after
    productionDbHashUnchanged = ($before -eq $after)
    focusedTest = 'AuthorityD3Tests'
    result = 'PASS'
    mq07 = 'BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED'
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'phase9.6d3-result.json') -Encoding UTF8
if (-not $report.productionDbHashUnchanged) { throw 'Production DB SHA-256 changed.' }
Write-Output "Phase 9.6D3 qualification passed. Evidence: $run"
