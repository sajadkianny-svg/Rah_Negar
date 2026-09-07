param(
    [Parameter(Mandatory=$false)]
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'qualification-data')
)
$project = Join-Path $PSScriptRoot '..\QualificationTool\QualificationTool.csproj'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith([IO.Path]::GetFullPath($PSScriptRoot), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification output path.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
dotnet build ([IO.Path]::GetFullPath($project)) -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project ([IO.Path]::GetFullPath($project)) -c Release --no-build --no-restore -- $output
if ($LASTEXITCODE -ne 0) { throw "Qualification preparation failed with exit code $LASTEXITCODE" }
