[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{16}$')]
    [string]$ExpectedPublicKeyToken,
    [string]$AssemblyDirectory = (Join-Path $PSScriptRoot '..\Library\AdaptiveCards.Rendering.Wpf.Net6\bin\Release'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\wpf-release'),
    [string]$StrongNameVerifier = 'sn.exe',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$files = foreach ($tfm in @('net6.0-windows', 'net10.0-windows')) {
    $path = Join-Path $AssemblyDirectory "$tfm\AdaptiveCards.Rendering.Wpf.Net6.dll"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing WPF release assembly: $path"
    }
    $identity = [Reflection.AssemblyName]::GetAssemblyName($path)
    $token = [Convert]::ToHexString($identity.GetPublicKeyToken())
    if ($identity.Name -ne 'AdaptiveCards.Rendering.Wpf.Net6' -or
        $token -ne $ExpectedPublicKeyToken) {
        throw "WPF release identity rejected: $path. Unsigned or differently signed development builds cannot be packaged."
    }
    [PSCustomObject]@{
        Path = (Resolve-Path -LiteralPath $path).Path
        Framework = $tfm
        Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}

$verifier = Get-Command $StrongNameVerifier -CommandType Application -ErrorAction Stop
foreach ($file in $files) {
    & $verifier.Source -vf $file.Path
    if ($LASTEXITCODE -ne 0) {
        throw "WPF strong-name signature verification failed: $($file.Path)"
    }
}
if ($ValidateOnly) { return }

$defaultAssemblies = (Resolve-Path (Join-Path $PSScriptRoot '..\Library\AdaptiveCards.Rendering.Wpf.Net6\bin\Release')).Path
if ((Resolve-Path -LiteralPath $AssemblyDirectory).Path -ne $defaultAssemblies) {
    throw 'Packaging requires the release output paths referenced by the WPF nuspec. Use ValidateOnly for other directories.'
}

dotnet pack (Join-Path $PSScriptRoot '..\Library\AdaptiveCards.Rendering.Wpf.Net6\AdaptiveCards.Rendering.Wpf.Net6.csproj') `
    -c Release --no-build --no-restore "-p:NuspecFile=$PSScriptRoot\AdaptiveCards.Rendering.Wpf.nuspec" `
    "-p:NuspecBasePath=$PSScriptRoot" --output $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'WPF release packaging failed' }

[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AdaptiveCards.Rendering.Wpf.nuspec')
$package = Join-Path $OutputDirectory "$($manifest.package.metadata.id).$($manifest.package.metadata.version).nupkg"
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($file in $files) {
        $entryPath = "lib/$($file.Framework)7.0/AdaptiveCards.Rendering.Wpf.Net6.dll"
        $entry = $zip.GetEntry($entryPath)
        if ($null -eq $entry) { throw "WPF package is missing $entryPath" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -ne $file.Hash) { throw "WPF package differs from the verified release assembly: $entryPath" }
    }
}
catch {
    $zip.Dispose()
    Remove-Item -LiteralPath $package
    throw
}
finally { $zip.Dispose() }
