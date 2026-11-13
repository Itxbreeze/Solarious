#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'EclipsedClient/EclipsedClient.csproj'
$dist = Join-Path $PSScriptRoot 'dist'
$folder = Join-Path $dist 'EclipsedClient'
$archive = Join-Path $dist 'EclipsedClient-Portable.zip'
$stage = Join-Path $dist ('.portable-' + [guid]::NewGuid().ToString('N'))
$stagedFolder = Join-Path $stage 'EclipsedClient'
$stagedArchive = Join-Path $stage 'EclipsedClient-Portable.zip'

foreach ($path in @($dist, $folder, $archive)) {
    $entry = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    if ($entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to publish through a link or junction at $path."
    }
}

New-Item -ItemType Directory -Path $stagedFolder -Force | Out-Null
try {
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:Version=$Version" -o $stagedFolder
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    foreach ($name in @('EclipsedClient.exe', 'EclipsedClient.dll', 'hostfxr.dll', 'coreclr.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $stagedFolder $name) -PathType Leaf)) {
            throw "The portable publish is missing $name."
        }
    }

    foreach ($notice in @('LICENSE', 'THIRD-PARTY-NOTICES')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination $stagedFolder
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $stagedFolder, $stagedArchive, [IO.Compression.CompressionLevel]::Optimal, $true)

    $distRoot = (Resolve-Path -LiteralPath $dist).Path.TrimEnd('\') + '\'
    if (Test-Path -LiteralPath $folder) {
        $resolvedFolder = (Resolve-Path -LiteralPath $folder).Path
        if (!$resolvedFolder.Equals($distRoot + 'EclipsedClient', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected output folder $resolvedFolder."
        }
        Remove-Item -LiteralPath $resolvedFolder -Recurse -Force
    }
    Move-Item -LiteralPath $stagedFolder -Destination $folder
    Move-Item -LiteralPath $stagedArchive -Destination $archive -Force
    Get-Item -LiteralPath $folder, $archive
}
finally {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
    $resolvedDist = (Resolve-Path -LiteralPath $dist).Path.TrimEnd('\') + '\'
    if ($resolvedStage.StartsWith($resolvedDist + '.portable-', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
