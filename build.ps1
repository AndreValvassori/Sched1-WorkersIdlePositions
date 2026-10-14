$ErrorActionPreference = 'Stop'

try {
    $projectPath = Join-Path $PSScriptRoot 'WorkersPlaces.csproj'
    & dotnet build $projectPath -c Release -p:DeployMod=true
    exit $LASTEXITCODE
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
