$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path $PSScriptRoot -Parent
$fixturePath = Join-Path ([System.IO.Path]::GetTempPath()) ('WorkersPlaces-release-test-' + [guid]::NewGuid().ToString('N'))
$mockBuild = @{ Fail = $false; Calls = 0 }

function Assert-True($condition, $message) {
    if (-not $condition) { throw $message }
}

function dotnet {
    $mockBuild.Calls++
    Assert-True ($args -contains '-p:DeployMod=false') 'NoDeploy must explicitly disable deployment.'
    if ($mockBuild.Fail) {
        $global:LASTEXITCODE = 1
        return
    }
    $outputPath = Join-Path (Split-Path $args[1] -Parent) 'bin/Release/net6.0'
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $outputPath 'WorkersPlaces.dll') -Value 'fixture DLL'
    $global:LASTEXITCODE = 0
}

New-Item -ItemType Directory -Path $fixturePath | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $repositoryPath 'release.ps1') -Destination $fixturePath
    $fixtureScript = Join-Path $fixturePath 'release.ps1'
    $projectPath = Join-Path $fixturePath 'WorkersPlaces.csproj'
    $modPath = Join-Path $fixturePath 'Mod.cs'
    $readmePath = Join-Path $fixturePath 'README.txt'
    Set-Content -LiteralPath $projectPath -Value '<Project><PropertyGroup><Version>0.2.0</Version></PropertyGroup></Project>'
    Set-Content -LiteralPath $modPath -Value '[assembly: MelonInfo(typeof(WorkersPlaces.Mod), "WorkersPlaces", "0.2.0", "AndreValvassori")]'
    Set-Content -LiteralPath $readmePath -Value "WorkersPlaces 0.2.0`r`nFixture instructions"

    foreach ($expectedVersion in @('0.3.0', '0.4.0')) {
        & $fixtureScript -NoDeploy | Out-Null
        [xml]$project = Get-Content -LiteralPath $projectPath -Raw
        Assert-True ($project.Project.PropertyGroup.Version -eq $expectedVersion) 'Project version did not increment.'
        Assert-True ((Get-Content -LiteralPath $modPath -Raw).Contains(('"' + $expectedVersion + '"'))) 'MelonInfo version differs.'
        Assert-True ((Get-Content -LiteralPath $readmePath -First 1) -eq "WorkersPlaces $expectedVersion") 'README version differs.'
        $archivePath = Join-Path $fixturePath "release/WorkersPlaces-$expectedVersion.zip"
        $archive = [System.IO.Compression.ZipArchive]::new([System.IO.File]::OpenRead($archivePath))
        try {
            Assert-True ($archive.Entries.Count -eq 2) 'Unexpected ZIP contents.'
            Assert-True ($null -ne $archive.GetEntry('Mods/WorkersPlaces.dll')) 'ZIP DLL is missing.'
            $reader = [System.IO.StreamReader]::new($archive.GetEntry('README.txt').Open())
            try { Assert-True ($reader.ReadLine() -eq "WorkersPlaces $expectedVersion") 'ZIP README version differs.' }
            finally { $reader.Dispose() }
        }
        finally { $archive.Dispose() }
    }

    $beforeFailure = @{}
    foreach ($path in @($projectPath, $modPath, $readmePath)) {
        $beforeFailure[$path] = (Get-FileHash -LiteralPath $path).Hash
    }
    $mockBuild.Fail = $true
    $failure = $null
    try { & $fixtureScript -NoDeploy | Out-Null }
    catch { $failure = $_ }
    Assert-True ($null -ne $failure -and $failure.ToString().Contains('dotnet build failed')) 'Expected build failure.'
    foreach ($path in $beforeFailure.Keys) {
        Assert-True ((Get-FileHash -LiteralPath $path).Hash -eq $beforeFailure[$path]) 'Failed release changed source contents.'
    }
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixturePath 'release/WorkersPlaces-0.5.0.zip'))) 'Failed build produced a release.'

    $mockBuild.Fail = $false
    Set-Content -LiteralPath $readmePath -Value 'WorkersPlaces 9.9.9'
    $previousCalls = $mockBuild.Calls
    $failure = $null
    try { & $fixtureScript -NoDeploy | Out-Null }
    catch { $failure = $_ }
    Assert-True ($null -ne $failure -and $failure.ToString().Contains('README.txt version does not match')) 'Expected version mismatch rejection.'
    Assert-True ($mockBuild.Calls -eq $previousCalls) 'Mismatched versions reached the build.'
    Assert-True ((Get-FileHash -LiteralPath $projectPath).Hash -eq $beforeFailure[$projectPath]) 'Mismatch rejection changed the project.'
    Write-Output 'PASS: consecutive minor releases, synchronized ZIP metadata, NoDeploy, build failure rollback, and mismatch rejection.'
}
finally {
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixturePath)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if ((Split-Path $resolvedFixture -Parent) -ne $expectedParent -or (Split-Path $resolvedFixture -Leaf) -notlike 'WorkersPlaces-release-test-*') {
        throw "Unexpected fixture cleanup path: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
