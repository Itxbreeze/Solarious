[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'EclipsedClient/EclipsedClient.csproj'
$output = Join-Path $root 'artifacts/publish'

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:Version=$Version" -o $output
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

$files = @(Get-ChildItem -LiteralPath $output -File -Recurse)
if ($files.Count -ne 1 -or $files[0].Name -ne 'EclipsedClient.exe') {
    throw "Expected one standalone EclipsedClient.exe in $output."
}
$files[0]
