[CmdletBinding()]
param(
    [int]$Replicates = 3,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'results')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
Push-Location (Join-Path $PSScriptRoot '..')
try {
    foreach ($case in @(
        @{ Framework = 'net8.0'; Legacy = 'true'; Name = 'runtime8-legacy' },
        @{ Framework = 'net10.0'; Legacy = 'true'; Name = 'runtime10-legacy' },
        @{ Framework = 'net10.0'; Legacy = 'false'; Name = 'runtime10-modern' }
    )) {
        dotnet build Benchmarks\AdaptiveCards.Performance\AdaptiveCards.Performance.csproj -c Release -f $case.Framework "-p:UseLegacySdkAssets=$($case.Legacy)" --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Benchmark build failed: $($case.Name)" }
        for ($i = 1; $i -le $Replicates; $i++) {
            $output = & dotnet "Benchmarks\AdaptiveCards.Performance\bin\Release\$($case.Framework)\AdaptiveCards.Performance.dll"
            if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $($case.Name)" }
            $output | Set-Content (Join-Path $OutputDirectory "$($case.Name)-$i.txt")
            $output
        }
    }
}
finally { Pop-Location }
