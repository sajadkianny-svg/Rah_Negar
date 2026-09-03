param(
    [ValidateSet('Rasht','Ramsar')]
    [string] $Station = 'Rasht',
    [string] $QualificationDirectory = (Join-Path $PSScriptRoot 'qualification-data')
)
$release = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Release\net8.0-windows'))
$db = [IO.Path]::GetFullPath((Join-Path $QualificationDirectory "$Station\db.sys"))
if (-not (Test-Path -LiteralPath (Join-Path $release 'Rah_Negar.exe'))) { throw "Build Release first: $release" }
if (-not (Test-Path -LiteralPath $db)) { throw "Prepare qualification data first: $db" }
$run = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'qualification-run'))
if (-not $run.StartsWith([IO.Path]::GetFullPath($PSScriptRoot), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe qualification run path.' }
if (Test-Path -LiteralPath $run) { Remove-Item -LiteralPath $run -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $run 'Data') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $release '*') -Destination $run -Recurse -Force
Copy-Item -LiteralPath $db -Destination (Join-Path $run 'Data\db.sys') -Force
Start-Process -FilePath (Join-Path $run 'Rah_Negar.exe') -WorkingDirectory $run -Wait
