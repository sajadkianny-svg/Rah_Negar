param(
    [Parameter(Mandatory=$false)]
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'qualification-data')
)
$project = Join-Path $PSScriptRoot '..\QualificationTool\QualificationTool.csproj'
dotnet run --project ([IO.Path]::GetFullPath($project)) -c Release --no-restore -- ([IO.Path]::GetFullPath($OutputDirectory))
if ($LASTEXITCODE -ne 0) { throw "Qualification preparation failed with exit code $LASTEXITCODE" }
