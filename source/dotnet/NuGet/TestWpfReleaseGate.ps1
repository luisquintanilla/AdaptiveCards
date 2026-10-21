[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'PackWpfRelease.ps1'

# An arbitrary token is intentionally used only to test rejection, never to sign or publish.
try {
    & $script -ExpectedPublicKeyToken '0000000000000000' -ValidateOnly
    throw 'Unsigned development assemblies unexpectedly passed the WPF release gate.'
}
catch {
    if ($_.Exception.Message -notlike 'WPF release identity rejected:*') { throw }
}

try {
    & $script -ExpectedPublicKeyToken '0000000000000000' `
        -AssemblyDirectory (Join-Path $PSScriptRoot '..\TestResults\missing-wpf-release-assemblies') -ValidateOnly
    throw 'Missing assemblies unexpectedly passed the WPF release gate.'
}
catch {
    if ($_.Exception.Message -notlike 'Missing WPF release assembly:*') { throw }
}

Write-Output 'WPF release gate passed: unsigned development and missing assemblies are rejected before packing.'
