# Adaptive Cards for .NET

This subtree contains the mutable Newtonsoft.Json/XML object model, JSON-to-JSON
templating library, Windows WPF renderer and samples. It does not contain a
supported server-side HTML renderer.

The separate [Blazor/AG-UI Workbench](Samples/AdaptiveCards.Workbench/README.md)
demonstrates browser rendering and an agent proposal/action round-trip. Its web,
AI and agent-protocol dependencies are intentionally excluded from the SDK
solution, packages and required SDK CI gate.

## Build and compatibility matrix

| Component | Supported development/execution target | Retained compatibility assets |
| --- | --- | --- |
| `Library\AdaptiveCards` | `net10.0` | `netstandard2.0` |
| `Library\AdaptiveCards.Net6` (shared core source) | Compatibility build only | `net6.0`, same existing assembly name |
| `Library\AdaptiveCards.Templating` | `net10.0` | `netstandard2.0`, `net6`, `net8` (existing output directory names) |
| `Library\AdaptiveCards.Rendering.Wpf.Net6` | `net10.0-windows` | `net6.0-windows`, unchanged `AdaptiveCards.Rendering.Wpf.Net6` assembly name |
| Modern image renderer / WPF visualizer samples | `net10.0-windows` | Existing .NET 6 targets |
| Main tests | `net10.0` | Both modern and `netstandard2.0` SDK assets tested on .NET 10 |
| Templating tests | `net10.0` | Existing .NET 6/8 runners retained |

.NET 6 is out of support; .NET 8 support ends November 10, 2026. Retaining an
asset does not endorse executing an unsupported runtime. A netstandard library
has no runtime of its own: existing compatible assemblies already receive the
JIT/runtime benefits of a .NET 10 host.

Run commands **from `source\dotnet`**, so `global.json` selects stable SDK
**10.0.402** (no preview or automatic roll-forward). The local
`Directory.Build.props` and `.targets` deliberately stop ancestor-file discovery;
no ancestor `Directory.Build` files are imported or changed.
Only .NET 10 targets select stable C# 14. Shared legacy source remains compatible
with its existing language settings; templating retains its existing C# 12 /
PolySharp arrangement on older targets.

`NuGet.config` also clears inherited source mappings. The former AdaptiveCards
Azure feed returned HTTP 403 (`ReadPackages`) during validation. This subtree
uses Microsoft's public `dotnet-public` mirror and NuGet.org v2, explicitly mapped
to public package IDs. NuGet.org v3 had a local TLS failure; v2 package downloads
and the mirror worked with normal certificate validation. No TLS bypass is used.

```powershell
dotnet build AdaptiveCards.Modern.slnx -c Release
.\RunAllTests.ps1
.\RunAllTests.ps1 -IncludeRendering   # Windows + desktop runtime
```

The focused solution excludes WinRT, classic server/app-package projects, WPF
release signing, and the Workbench. Existing `.sln` files and legacy project
paths remain available; they are not implicitly upgraded by the modern gate.
The Azure pipeline uses Windows 2022 and the pinned SDK instead of Windows 2019 /
.NET 5.

`RunAllTests.ps1` runs each suite twice on .NET 10, selecting either the modern
or the `netstandard2.0` project-reference asset with `UseLegacySdkAssets`.
Tests assert the loaded assembly's target-framework attribute, not just the
test runner's target. Each run produces separate TRX files and fails on command
failure, missing results, zero executed/passing tests, or failed tests. One
pre-existing main-suite scenario serialization test remains ignored.
CI passes `-CollectCoverage` to retain the previous pipeline's code coverage;
the test script also requires a real coverage attachment rather than silently
accepting an unavailable collector. The existing component-governance detection
task remains enabled in the updated pipeline.

For a single compatibility run, set the expected attribution as well:

```powershell
$env:ADAPTIVECARDS_EXPECTED_CORE_TFM = '.NETStandard,Version=v2.0'
$env:ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM = '.NETStandard,Version=v2.0'
dotnet test Test\AdaptiveCards.Test\AdaptiveCards.Test.csproj -c Release -f net10.0 -p:UseLegacySdkAssets=true
dotnet test Test\AdaptiveCards.Templating.Test\AdaptiveCards.Templating.Test.csproj -c Release -f net10.0 -p:UseLegacySdkAssets=true
Remove-Item Env:\ADAPTIVECARDS_EXPECTED_CORE_TFM, Env:\ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM
```

## Bounded implementation changes

Only the fixed DATE/TIME matcher uses `GeneratedRegex` on .NET 10. Its pattern,
named/numbered captures, explicit-capture option and original infinite timeout
are preserved. Legacy builds retain the original regex implementation.
Formatting/parity tests include multiple cultures, timezone offsets, whitespace,
invalid tokens, unsupported TIME hints and null input. The markdown lexer and
its timeouts are unchanged.

Templating references System.Text.Json **10.0.0** on `net10.0`, retaining **8.0.5**
on older assets; the explicit nuspec groups match those choices and
AdaptiveExpressions **4.23.1**. On the measured .NET 10.0.13 host, the loaded JSON
assembly came from the serviced shared framework, not the older package binary.
PolySharp is build-only. Newtonsoft.Json, XML serialization, public mutable
registries/maps, warning contexts and reflection-based extension behavior remain
unchanged. No immutable-map rewrite, shared converter cache, new strict JSON
options, or AI dependencies were introduced.

## Windows desktop and signing

```powershell
dotnet build Samples\AdaptiveCards.Net6.Sample.ImageRender -c Release -f net10.0-windows -p:PublicBuild=true
dotnet build Samples\WPFVisualizerNet6 -c Release -f net10.0-windows -p:PublicBuild=true
dotnet run --project Samples\AdaptiveCards.Net6.Sample.ImageRender -c Release -f net10.0-windows -p:PublicBuild=true -- Test\RenderingSmoke\card.json -i -o TestResults\WPF
```

Microsoft's root `35MSSharedLib1024.snk` is not distributed in this checkout.
The **explicit** `PublicBuild=true` source-build fallback creates an **unsigned,
non-packable WPF assembly** for local/sample/CI verification. It does not invent
a key, substitute a public-key token, rename an assembly, or change the default
release signing settings. It is **not a release-identity-compatible binary and
must not be published**. Release WPF packaging still requires the authorized
original signing material and release infrastructure. Public-build WPF packages
are deliberately not produced by `PackSdk.ps1`.

The separate `NuGet\PackWpfRelease.ps1` entry point rejects unsigned or
differently signed binaries, requires a trusted expected public-key token from
the release owner, and invokes the Windows SDK strong-name verifier (`sn.exe
-vf`) to reject invalid or delay-only signatures. It checks both retained/new
WPF assets before packing, then verifies the packaged bytes against those
checked assemblies. It does not generate keys or perform publication:

```powershell
.\NuGet\TestWpfReleaseGate.ps1
# In the authorized release environment, after signing both WPF assemblies:
.\NuGet\PackWpfRelease.ps1 -ExpectedPublicKeyToken $ReleasePublicKeyToken -StrongNameVerifier $StrongNameVerifierPath
```

The first command deliberately proves that unsigned development and missing
outputs fail closed without requiring signing credentials. The second requires
the existing release owner's signed artifacts and verifier, not a newly invented
identity. Direct external `nuget pack` invocations can bypass repository scripts;
release automation must use the guarded entry point. No signed Microsoft release
package is claimed from a public/source-only checkout.

The deterministic offline rendering fixture exercises text, markdown, resource
styles, an input and an action without remote image/network dependencies. It is
a smoke gate, not a complete font/DPI/image parity certification. The image
sample now propagates render errors/empty input to its process exit code, so CI
cannot mistake a swallowed rendering failure for success.

On the validation machine, rendering the fixture on .NET 6.0.36 and .NET 10.0.13
produced byte-identical PNGs:
`C534867F35FDFA983037B5ED91917D5133DB32DA5285DCC9FAA6F0B0A07FA8B8` (SHA-256).
This is evidence for this offline card on the same Windows/font/DPI environment,
not a claim that all rendering differences across runtime versions are absent.

WPF host configuration, XAML/resources and default appearance were not changed.
WebClient/cache/resolver/cancellation changes are deferred independently:
replacing these requires preserving proxy, credentials, request-cache policy,
custom resolvers, URI/error behavior and cancellation semantics. Existing
obsolescence/platform warnings are visible rather than hidden.

## Actual package verification

```powershell
.\NuGet\PackSdk.ps1
.\NuGet\TestPackages.ps1
```

The first command builds all retained core/templating assets and packs the
**explicit release nuspecs**, not just the project-generated templating package.
It preserves package IDs, versions, license and repository metadata. The second
opens the actual `.nupkg` files, checks framework assets/groups, restores an
isolated local-package consumer, then builds/runs it on .NET 8 and .NET 10.
Exact package-ID source mappings ensure a public package with the same version
cannot hide a missing local package. This is a local verification route, not a
publication/versioning change.

The core nuspec retains pre-existing dependency-only `.NETFramework4.5.2` and
`.NETStandard1.3` groups without corresponding binaries (NU5128); this pass does
not invent/recreate those obsolete assets. WPF's modern nuspec asset/framework
group is updated, but a signed WPF package consumer remains a release-key gate.
Legacy core .NET 6 assembly version `0.0.0.0` and canonical core version
`1.0.0.0` are pre-existing build identities, intentionally not normalized here.
No general Native AOT or trimming guarantee is added; the existing templating
annotations are not a substitute for an actual application publish/run test.

## Performance harness

```powershell
.\Benchmarks\RunBenchmarks.ps1
```

This small dependency-free harness reports runtime, architecture, GC mode,
loaded assembly target/version/logical path/size, first-use latency, warmed throughput,
thread allocations and collection counts. The script runs three fresh processes
per runtime/asset case, separating (1) compatible assets on .NET 8 vs .NET 10 and
(2) compatible vs modern assets on the same .NET 10 runtime. Separately captured
pre-change snapshots provide (3) the modern DATE/TIME implementation before/after
generated regex. Parse/serialize, markdown, template construction +
expansion and repeated expansion are measured separately. Raw measurements are
under `Benchmarks\results`; the before-change snapshots omit markdown, which
was added to the final harness. Only DATE/TIME is used for before/after claims.

These are diagnostic measurements on a shared workstation, not statistically
controlled BenchmarkDotNet results or a broad performance promise. First-use
timing includes JIT, culture and regex initialization; it is not isolated regex
construction time. No span/lexer rewrite, JSON-tree reuse, public API change or
large optimization was justified by this initial evidence.

Published captures normalize only absolute checkout/installation path prefixes
to `<repo>` and `<dotnet>`; the recorded versions, sizes and measurements are
unchanged. New runs use `<app>`, `<runtime>` or `<external>` logical assembly
locations so local directory names are not emitted in shareable results.

### Measured result (October 21, 2026)

Windows 10.0.26200 x64, workstation GC, three fresh processes per case.
The compatible assets are `netstandard2.0` in **both** runtime comparisons,
not a .NET 6 asset in one run and a modern asset in the other.
Raw output records the executing assembly identities, logical locations and sizes.

Warm medians in **microseconds/operation** (all three observed values are retained
in `Benchmarks\results`; no outliers were discarded):

| Workload | .NET 8.0.32, compatible | .NET 10.0.13, compatible | .NET 10.0.13, modern |
| --- | ---: | ---: | ---: |
| DATE/TIME | 9.425 | 6.688 | 8.235 |
| Parse + serialize | 19,716.947 | 21,001.237 | 17,780.052 |
| Markdown | 81.168 | 67.160 | 64.722 |
| New template + expand | 628.207 | 493.979 | 510.396 |
| Repeated expand | 502.539 | 364.003 | 353.800 |

Do **not** interpret these as universal speedups. Other local build/test activity
overlapped some runs: e.g. compatible parse/serialize ranged **19,606–30,376**
µs on .NET 8 and **20,870–26,293** µs on .NET 10, and new-template medians do
not favor the modern asset over the compatible one. This initial harness is a
reproducible diagnostic, not a controlled latency acceptance benchmark.

The bounded regex candidate's repeatable benefit was **first-use allocation**:
the modern asset before generation allocated **52,672 bytes**, versus **33,064**
after, in every replicate (**19,608 bytes less**). Warm allocation stayed at
**4,232 bytes/op**. Core DLL size increased from **398,848** to **403,456 bytes**
(**4,608 bytes**). First-use times were **62.784–75.850 ms** before and
**42.978–44.705 ms** after, but compatible-on-.NET-10 was faster still at
**33.166–40.414 ms**; the timing results do not establish a general startup
or throughput win. The change is retained for the demonstrated cold-allocation
reduction and compile-time regex generation, at an explicit small size cost,
not a claimed end-to-end latency improvement.

Repeated expansion allocated **129,696 bytes/op** with the compatible asset
on .NET 10 versus **125,840** with the modern asset. That is asset/dependency
selection evidence, not evidence that merely retargeting any .NET library will
reduce allocations. Markdown/parser algorithms, JSON node ownership, host GC
settings and public registries were not optimized.

## Verified commands and focused regressions

Validation on Windows x64, SDK 10.0.402:

| Exact command (from this directory) | Result |
| --- | --- |
| `dotnet build AdaptiveCards.Modern.slnx -c Release --nologo -v quiet` | Passed, 0 errors |
| `.\RunAllTests.ps1 -IncludeRendering` | Passed: core **140 passed, 1 pre-existing ignored** and templating **75 passed**, for **each** of modern and netstandard assets on .NET 10; fresh PNG produced |
| `dotnet test Test\AdaptiveCards.Test\AdaptiveCards.Test.csproj -c Release -f net10.0 --filter FullyQualifiedName~TextFunctionTests --nologo -v quiet` | Passed, 6 cases |
| `dotnet test Test\AdaptiveCards.Test\AdaptiveCards.Test.csproj -c Release -f net10.0 --no-restore --filter FullyQualifiedName~TextBlockXmlRoundTrip --nologo -v quiet` | Passed, 1 case |
| `dotnet test Test\AdaptiveCards.Templating.Test\AdaptiveCards.Templating.Test.csproj -c Release -f net6.0 --nologo -v quiet` | Passed, 75 tests (compatibility runtime only) |
| `dotnet test Test\AdaptiveCards.Templating.Test\AdaptiveCards.Templating.Test.csproj -c Release -f net8.0 --nologo -v quiet` | Passed, 75 tests |
| `dotnet build Samples\AdaptiveCards.Net6.Sample.ImageRender\AdaptiveCards.Net6.Sample.ImageRender.csproj -c Release -f net10.0-windows -p:PublicBuild=true --nologo -v quiet` | Passed |
| `dotnet build Samples\WPFVisualizerNet6\AdaptiveCards.Sample.WPFVisualizer.Net6.csproj -c Release -f net10.0-windows -p:PublicBuild=true --nologo -v quiet` | Passed |
| `.\NuGet\PackSdk.ps1` | Passed, actual core 3.1.0 and templating 2.0.0 nuspec packages produced |
| `.\NuGet\TestPackages.ps1` | Passed: asset/group checks, isolated net8/net10 consumers, **4,718-entry** public core reflection-surface parity |
| `.\Benchmarks\RunBenchmarks.ps1` | Passed: 9 fresh-process measurements; exact attribution, cold/warm timings, allocations, GC counts and assembly sizes recorded |
| `dotnet run --project Samples\AdaptiveCards.Net6.Sample.ImageRender -c Release -f net6.0-windows7.0 -p:PublicBuild=true --no-launch-profile -- Test\RenderingSmoke\card.json -i -o TestResults\WpfLegacy` | Passed; PNG matches modern output hash above |
| `dotnet run --project Samples\AdaptiveCards.Net6.Sample.ImageRender -c Release -f net10.0-windows -p:PublicBuild=true --no-launch-profile -- does-not-exist.json -o TestResults\WpfNegative` | Expected failure, process exit **1**, proving invalid-input propagation |

| Requirement | Focused regression evidence |
| --- | --- |
| Preserve DATE/TIME regex captures/culture/timeout | `TextFunctionRegex_CapturesMatchLegacyPattern` |
| Preserve text formatting, invalid tokens and null behavior | `ApplyTextFunctions_PreservesFormattingAndInvalidTokens` |
| Preserve public mutable XML behavior | `TextBlockXmlRoundTrip_PreservesMutableTextAndFormatting` |
| Preserve repeat-expansion `$root`/`$data`/`$when` isolation | `RepeatedExpansion_KeepsRootAndRepeatedDataIsolated` |
| Actually exercise requested SDK assets | `LoadedCoreAsset_MatchesRequestedCoverage`, `LoadedTemplatingAsset_MatchesRequestedCoverage` |

The pipeline definition is updated but has not been executed on Azure in this
worktree. Signed WPF package publication, broad image/font/DPI parity, complete
scenario XML rehabilitation and Native AOT publishing remain separate gates.
