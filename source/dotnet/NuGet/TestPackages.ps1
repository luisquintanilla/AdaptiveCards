[CmdletBinding()]
param([string]$PackageDirectory = (Join-Path $PSScriptRoot '..\artifacts\packages'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$expectedAssets = @{
    'AdaptiveCards.3.1.0.nupkg' = @('netstandard2.0', 'net6.0', 'net10.0')
    'AdaptiveCards.Templating.2.0.0.nupkg' = @('netstandard2.0', 'net6.0', 'net8.0', 'net10.0')
}
foreach ($name in $expectedAssets.Keys) {
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory $name))
    try {
        foreach ($tfm in $expectedAssets[$name]) {
            if (@($zip.Entries | Where-Object { $_.FullName -like "lib/$tfm/*.dll" }).Count -ne 1) {
                throw "$name is missing or duplicates $tfm"
            }
        }
        $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if (@($manifest.package.metadata.dependencies.group | Where-Object { $_.targetFramework -eq 'net10.0' }).Count -ne 1) {
            throw "$name has no modern dependency group"
        }
        foreach ($dependency in $manifest.package.metadata.dependencies.group.dependency) {
            if ($dependency.id -match '^(AGUI\.|Microsoft\.Extensions\.AI|Microsoft\.Agent|Microsoft\.AspNetCore\.Components\.AI|PolySharp)') {
                throw "$name leaks a non-SDK dependency: $($dependency.id)"
            }
        }
    }
    finally { $zip.Dispose() }
}
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $cache = Join-Path $PSScriptRoot '..\artifacts\consumer-packages'
    # An isolated cache prevents an already-published package with the same version hiding our build.
    foreach ($package in @('adaptivecards\3.1.0', 'adaptivecards.templating\2.0.0')) {
        $path = Join-Path $cache $package
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }
    $config = [xml](Get-Content Test\PackageConsumer\NuGet.config)
    ($config.configuration.packageSources.add | Where-Object { $_.key -eq 'local-sdk' }).value = (Resolve-Path $PackageDirectory).Path
    $configPath = Join-Path $PSScriptRoot '..\artifacts\consumer.nuget.config'
    $config.Save($configPath)
    dotnet restore Test\PackageConsumer\PackageConsumer.csproj --packages $cache --configfile $configPath --force --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Consumer restore failed' }
    foreach ($tfm in @('net8.0', 'net10.0')) {
        dotnet run --project Test\PackageConsumer -c Release -f $tfm --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Consumer failed: $tfm" }
    }
    dotnet run --project Test\PackageConsumer -c Release -f net10.0 --no-build -- Library\AdaptiveCards\bin\Release\netstandard2.0\AdaptiveCards.dll Library\AdaptiveCards\bin\Release\net10.0\AdaptiveCards.dll
    if ($LASTEXITCODE -ne 0) { throw 'Core API surface parity failed' }
}
finally { Pop-Location }
