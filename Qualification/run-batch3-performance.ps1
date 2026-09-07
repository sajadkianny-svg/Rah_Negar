param(
    [string] $EvidenceDirectory = (Join-Path $PSScriptRoot 'qualification-run\batch3-performance')
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qualificationRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (-not $evidence.StartsWith($qualificationRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe performance evidence path.'
}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

$tool = Join-Path $repo 'QualificationTool\QualificationTool.csproj'
$test = Join-Path $repo 'Rah_Negar.Tests\Rah_Negar.Tests.csproj'
$probePath = Join-Path $evidence 'grid-cache-probe.json'
$start = [DateTime]::UtcNow
& dotnet run --project $tool -c Release --no-build --no-restore -- --performance $probePath 2>&1 |
    Tee-Object -FilePath (Join-Path $evidence 'grid-cache-probe.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Grid cache probe failed: $LASTEXITCODE" }
$probe = Get-Content -Raw $probePath | ConvertFrom-Json

$testStart = [Diagnostics.Stopwatch]::StartNew()
& dotnet test $test -c Release --no-restore --filter 'FullyQualifiedName~Batch3CompletionTests' --logger "trx;LogFileName=$(Join-Path $evidence 'batch3-focused.trx')" 2>&1 |
    Tee-Object -FilePath (Join-Path $evidence 'batch3-focused-tests.log') | Out-Host
$testExit = $LASTEXITCODE
$testStart.Stop()

$result = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    elapsedMilliseconds = [int64]([DateTime]::UtcNow - $start).TotalMilliseconds
    focusedTestMilliseconds = [int64]$testStart.Elapsed.TotalMilliseconds
    focusedTestExitCode = $testExit
    gridCache = $probe
    result = if ($testExit -eq 0 -and $probe.result -eq 'PASS') { 'PASS' } else { 'FAIL' }
    limitations = @('This evidence measures the source-level cache invariant and focused regression suite.', 'Native startup/form/PDF measurements still require the operator workstation.')
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'batch3-performance-result.json') -Encoding UTF8
if ($testExit -ne 0 -or $result.result -ne 'PASS') { exit 1 }
Write-Output "Batch 3 performance evidence passed: $evidence"
