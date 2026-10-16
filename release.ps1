param(
    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'WorkersPlaces.csproj'
$modPath = Join-Path $PSScriptRoot 'Mod.cs'
$readmePath = Join-Path $PSScriptRoot 'README.txt'
$releasePath = Join-Path $PSScriptRoot 'release'
$releaseModsPath = Join-Path $releasePath 'Mods'
$releaseDllPath = Join-Path $releaseModsPath 'WorkersPlaces.dll'
$releaseReadmePath = Join-Path $releasePath 'README.txt'

$projectSource = Get-Content -LiteralPath $projectPath -Raw
[xml]$project = $projectSource
$version = [string](($project.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version)
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'WorkersPlaces.csproj has no Version property.'
}
if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "Project version '$version' must have the format major.minor.patch."
}
$currentVersion = [version]$version
$nextVersion = '{0}.{1}.0' -f $currentVersion.Major, ($currentVersion.Minor + 1)

$modSource = Get-Content -LiteralPath $modPath -Raw
if ($modSource -notmatch ('MelonInfo\([^\r\n]*"' + [regex]::Escape($version) + '"')) {
    throw "Mod.cs MelonInfo version does not match project version $version."
}

$readmeSource = Get-Content -LiteralPath $readmePath -Raw
if ($readmeSource -notmatch ('(?m)^WorkersPlaces ' + [regex]::Escape($version) + '\s*$')) {
    throw "README.txt version does not match project version $version."
}

if (-not $NoDeploy -and (Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue)) {
    throw 'Close Schedule I before deploying the mod.'
}

$originalSources = @{
    $projectPath = $projectSource
    $modPath = $modSource
    $readmePath = $readmeSource
}
$utf8 = [System.Text.UTF8Encoding]::new($false)
try {
    [System.IO.File]::WriteAllText($projectPath, $projectSource.Replace("<Version>$version</Version>", "<Version>$nextVersion</Version>"), $utf8)
    $modVersionPattern = '(MelonInfo\([^\r\n]*")' + [regex]::Escape($version) + '(")'
    [System.IO.File]::WriteAllText($modPath, [regex]::Replace($modSource, $modVersionPattern, ('${1}' + $nextVersion + '${2}')), $utf8)
    [System.IO.File]::WriteAllText($readmePath, $readmeSource.Replace("WorkersPlaces $version", "WorkersPlaces $nextVersion"), $utf8)
    Write-Output "Release version: $version -> $nextVersion"
    $version = $nextVersion

    $buildArguments = @('build', $projectPath, '-c', 'Release', "-p:DeployMod=$((-not $NoDeploy).ToString().ToLowerInvariant())")

    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    $builtDllPath = Join-Path $PSScriptRoot 'bin/Release/net6.0/WorkersPlaces.dll'
    if (-not (Test-Path -LiteralPath $builtDllPath -PathType Leaf)) {
        throw "Built DLL was not found at $builtDllPath."
    }

    New-Item -ItemType Directory -Path $releaseModsPath -Force | Out-Null
    Copy-Item -LiteralPath $builtDllPath -Destination $releaseDllPath -Force
    Copy-Item -LiteralPath $readmePath -Destination $releaseReadmePath -Force

    $archivePath = Join-Path $releasePath "WorkersPlaces-$version.zip"
    $temporaryArchivePath = Join-Path $releasePath (".WorkersPlaces-$version-$([guid]::NewGuid().ToString('N')).tmp")

    Add-Type -AssemblyName System.IO.Compression
    try {
        $stream = [System.IO.File]::Open($temporaryArchivePath, [System.IO.FileMode]::CreateNew)
        try {
            $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
            try {
                foreach ($file in @(
                    @{ Source = $releaseDllPath; Entry = 'Mods/WorkersPlaces.dll' },
                    @{ Source = $releaseReadmePath; Entry = 'README.txt' }
                )) {
                    $entry = $archive.CreateEntry($file.Entry, [System.IO.Compression.CompressionLevel]::Optimal)
                    $entryStream = $entry.Open()
                    $sourceStream = [System.IO.File]::OpenRead($file.Source)
                    try {
                        $sourceStream.CopyTo($entryStream)
                    }
                    finally {
                        $sourceStream.Dispose()
                        $entryStream.Dispose()
                    }
                }
            }
            finally {
                $archive.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        if (Test-Path -LiteralPath $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }
        Move-Item -LiteralPath $temporaryArchivePath -Destination $archivePath
    }
    finally {
        if (Test-Path -LiteralPath $temporaryArchivePath) {
            Remove-Item -LiteralPath $temporaryArchivePath -Force
        }
    }

    Write-Output "Release ZIP: $archivePath"
    Write-Output "Standalone DLL: $releaseDllPath"
    Write-Output "Plain-text README: $releaseReadmePath"
    if ($NoDeploy) {
        Write-Output 'Game deployment skipped.'
    }
    else {
        Write-Output 'Mod deployed. Restart Schedule I to load this version.'
    }
}
catch {
    foreach ($sourcePath in $originalSources.Keys) {
        [System.IO.File]::WriteAllText($sourcePath, $originalSources[$sourcePath], $utf8)
    }
    throw
}
