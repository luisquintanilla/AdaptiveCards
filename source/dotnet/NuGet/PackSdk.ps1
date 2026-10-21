[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\packages'))

$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    foreach ($project in @('Library\AdaptiveCards\AdaptiveCards.csproj', 'Library\AdaptiveCards.Net6\AdaptiveCards.csproj', 'Library\AdaptiveCards.Templating\AdaptiveCards.Templating.csproj')) {
        dotnet build $project -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    foreach ($package in @('AdaptiveCards', 'AdaptiveCards.Templating')) {
        $project = "Library\$package\$package.csproj"
        dotnet pack $project -c Release --no-build "-p:NuspecFile=$PSScriptRoot\$package.nuspec" "-p:NuspecBasePath=$PSScriptRoot" --output $OutputDirectory --nologo
        if ($LASTEXITCODE -ne 0) { throw "Pack failed: $package" }
    }
}
finally { Pop-Location }
