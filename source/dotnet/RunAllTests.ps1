[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot 'TestResults'),
    [switch]$IncludeRendering,
    [switch]$CollectCoverage
)

$ErrorActionPreference = 'Stop'
$originalCore = $env:ADAPTIVECARDS_EXPECTED_CORE_TFM
$originalTemplating = $env:ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM
Push-Location $PSScriptRoot
try {
    foreach ($legacy in @($false, $true)) {
        $asset = if ($legacy) { 'legacy' } else { 'modern' }
        $expected = if ($legacy) { '.NETStandard,Version=v2.0' } else { '.NETCoreApp,Version=v10.0' }
        $env:ADAPTIVECARDS_EXPECTED_CORE_TFM = $expected
        $env:ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM = $expected
        foreach ($suite in @('AdaptiveCards.Test', 'AdaptiveCards.Templating.Test')) {
            $resultPath = Join-Path $ResultsDirectory "$asset\$suite"
            if ($CollectCoverage) { $resultPath = Join-Path $resultPath ([Guid]::NewGuid().ToString('N')) }
            New-Item -ItemType Directory -Force $resultPath | Out-Null
            $trxPath = Join-Path $resultPath 'tests.trx'
            if (Test-Path $trxPath) { Remove-Item $trxPath }
            $coverageArguments = if ($CollectCoverage) { @('--collect', 'Code Coverage') } else { @() }
            dotnet test "Test\$suite\$suite.csproj" -c $Configuration -f net10.0 "-p:UseLegacySdkAssets=$($legacy.ToString().ToLowerInvariant())" @coverageArguments --logger 'trx;LogFileName=tests.trx' --results-directory $resultPath --nologo
            if ($LASTEXITCODE -ne 0) { throw "$suite ($asset) failed with exit code $LASTEXITCODE" }
            if (!(Test-Path $trxPath)) { throw "$suite ($asset) produced no TRX results" }
            [xml]$trx = Get-Content $trxPath
            $counters = $trx.TestRun.ResultSummary.Counters
            if ([int]$counters.executed -eq 0 -or [int]$counters.passed -eq 0 -or [int]$counters.failed -ne 0) {
                throw "$suite ($asset) had zero executed/passing tests or test failures"
            }
            if ($CollectCoverage) {
                $attachments = @($trx.TestRun.ResultSummary.CollectorDataEntries.Collector.UriAttachments.UriAttachment)
                $coverageNames = @($attachments | Where-Object { $_.A.href -like '*.coverage' } |
                    ForEach-Object { [IO.Path]::GetFileName([Uri]::UnescapeDataString($_.A.href)) } |
                    Select-Object -Unique)
                $coverageFiles = @(Get-ChildItem -LiteralPath $resultPath -Filter *.coverage -Recurse -File |
                    Where-Object { $_.Length -gt 0 -and $coverageNames -contains $_.Name } |
                    Select-Object -ExpandProperty Name -Unique)
                if ($coverageNames.Count -eq 0 -or $coverageFiles.Count -ne $coverageNames.Count) {
                    throw "$suite ($asset) produced missing or empty coverage attachments"
                }
            }
        }
    }
    if ($IncludeRendering) {
        $output = Join-Path $ResultsDirectory 'WPF'
        if (Test-Path $output) { Remove-Item $output -Recurse -Force }
        dotnet run --project Samples\AdaptiveCards.Net6.Sample.ImageRender -c $Configuration -f net10.0-windows -p:PublicBuild=true -- Test\RenderingSmoke\card.json -i -o $output
        if ($LASTEXITCODE -ne 0) { throw "WPF rendering failed: $LASTEXITCODE" }
        if (@(Get-ChildItem $output -Filter *.png -Recurse).Count -eq 0) { throw 'WPF rendering produced no PNG images' }
    }
}
finally {
    $env:ADAPTIVECARDS_EXPECTED_CORE_TFM = $originalCore
    $env:ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM = $originalTemplating
    Pop-Location
}
