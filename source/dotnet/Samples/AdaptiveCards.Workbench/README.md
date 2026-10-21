# Adaptive Cards .NET 10 workbench

A bounded Interactive Server Blazor sample that combines the .NET object model and templating library, the existing Adaptive Cards JavaScript renderer, and the stable AG-UI .NET client. It is an editing/preview/proposal/action loop, not a new renderer or a general chat application.

## Run

Requires a .NET 10 SDK and Node.js 22 or newer. From this directory:

```powershell
node scripts\copy-renderer.mjs
dotnet run --launch-profile http
```

Open `http://localhost:5210`. No model, Azure account, preview SDK or API key is needed. The deterministic `/agent` endpoint emits actual AG-UI `RUN_STARTED`, `STATE_SNAPSHOT` and `RUN_FINISHED` events over SSE. A server-side `AGUIChatClient` consumes that stream; proposal buttons do not bypass the protocol.

The asset preparation script downloads the published `adaptivecards@3.0.5` bundle and MIT license from jsDelivr, verifies pinned SHA-256 hashes, and serves them locally. This explicitly pins the released renderer rather than claiming parity with this repository's unreleased 4.0 source. There are no runtime CDN requests for scripts or npm restore requirements. Rebuild these assets before publishing. Updating the renderer requires reviewing its version, hashes, API compatibility and browser tests.

## Workflow and contract

Edit the raw card and template data, then select **Validate and preview**. Invalid JSON keeps the draft and last good committed preview. Raw JSON remains the authoring source: the sample never normalizes it through the .NET model and silently loses unknown fields. Template expansion and .NET parser warnings are separate from browser renderer diagnostics.

**Request proposal** sends the current draft and data to the local agent, not the last committed version. A complete validated snapshot appears in a separate preview. Accept commits it; reject changes neither the draft nor committed card. Any intervening card/data edit increments the revision and makes an older proposal ineligible for acceptance.

The application envelope contains `documentId`, `baseRevision`, `runId`, `callId`, `cardJson` and `dataJson`. These are application-level contracts, not AG-UI concurrency guarantees. AG-UI thread/run IDs correlate transport; they are not authorization credentials. Incomplete streams, duplicate snapshots and mismatched terminal events fail instead of publishing partial state.

The initial card demonstrates a single inert `demo.submit` operation. Adaptive Cards prepares `Action.Submit` data by combining action data and validated associated input values; the wrapper forwards that processed data. The server also accepts `Action.Execute` only with the explicit `demo.submit` verb. Both routes produce a proposed card showing the received data, not an external side effect. Action IDs and proposal call IDs are deduplicated within the session. A displayed “approve” button cannot authorize any real tool. ShowCard/ToggleVisibility remain renderer-local, links are inert, and actions in the proposal preview do not execute.

Blazor owns the surrounding interface; each JavaScript instance owns only its card DOM island and event handlers. Card instances are not rebuilt on editor keystrokes or stream events. On an actual committed rerender, unchanged input IDs/types retain their values. Component disposal releases handlers and interop references. Browser/server disconnection ends interaction; it does not imply durable agent execution.

## Limits and non-goals

- Card and template data: each at most 64 KiB UTF-8, JSON depth 32. Expanded cards must also fit these bounds. Submitted action data: 8 KiB. HTTP and SignalR message limits: 192 KiB. Runs time out after 30 seconds and can be cancelled.
- Resource URLs must be absolute HTTPS URLs after template expansion. Links do not navigate. There is no arbitrary HTML/Markdown renderer callback. HTTPS image URLs can still contact their external host; don't put private tokens in card resources.
- State is scoped to one Blazor circuit. Refresh/restart loses drafts, input values, proposals and deduplication history. **Export draft** downloads raw card JSON; **Reload card** validates a selected JSON file. Template data must be saved separately.
- The unauthenticated agent endpoint is loopback-only. Run the sample on localhost; do not expose it as a production or multi-tenant service. It has no durable persistence, real tool approval, model endpoint configuration, patch/merge engine, cloud credentials or deployment infrastructure.
- Library packaging and SDK CI do not depend on this application. AG-UI, Blazor and Microsoft.Extensions.AI are sample-only dependencies.

## Verification

From the repository root:

```powershell
dotnet test source\dotnet\Test\AdaptiveCards.Workbench.Tests\AdaptiveCards.Workbench.Tests.csproj
node --test source\dotnet\Samples\AdaptiveCards.Workbench\tests\browser.test.mjs
```

The browser test requires a running sample on `http://localhost:5210` and uses installed Microsoft Edge through the DevTools protocol, without downloading a second browser or test framework. Override `WORKBENCH_URL` and `BROWSER_PATH` for another localhost port or Chromium executable. It exercises template/data edits, real SSE proposals, accept/reject, retained inputs, associated-input submission, invalid drafts, stale proposals and cancellation. It reports observed first-preview latency; this is a smoke observation, not a controlled performance benchmark or cross-runtime speedup claim.

The MSTest suite covers atomic state changes, foreign/duplicate/stale proposals, unknown-field preservation, byte/depth/URL limits, template expansion, and actual AG-UI client decoding with complete, truncated, duplicate, foreign-run and cancelled streams.

## Separate evaluation: experimental Blazor AI components

The [Blazor AI components article](https://devblogs.microsoft.com/dotnet/build-agentic-ui-blazor/) is directly relevant to the workbench shell, not to the core Adaptive Cards object model:

| Experimental surface | Workbench counterpart | Potential benefit |
| --- | --- | --- |
| `UIAgent<TState>` and `StateMapper` | Circuit-scoped state plus AG-UI client | Observable streamed state and content blocks |
| Generated `ToolBlock` / `BlockRenderer` | Explicit application-owned proposal component | Typed `propose_adaptive_card` rendering and tool/result binding |
| Predictive state accept/reject | Distinct proposal and committed documents | Less custom proposal shell code |
| `FunctionApprovalBlock` / `UIActionBlock` | Explicit inert action allowlist | UI primitives for a future authenticated approval workflow |

Neither the article's Razor weather card nor the inspected components supply an Adaptive Cards renderer. A future `propose_adaptive_card` block would reuse `CardPreview` and the same document/action contract. Observable or predictive state would not replace revision checks, draft preservation, deduplication or authorization.

The inspected published `Microsoft.AspNetCore.Components.AI` version **0.1.0-preview.1.26466.103** has a **net11.0** dependency group and references `Microsoft.AspNetCore.Components.Web` **11.0.0-rc.1.26425.128**. The [linked sample](https://github.com/danroth27/AgenticUI) also targets net11.0 and pins a preview SDK. [ASP.NET Core's .NET 11 documentation](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-11.0?view=aspnetcore-11.0) explicitly describes the package as experimental, including after framework GA.

**Decision:** keep the working LTS sample on .NET 10; do not force the experimental shell into the SDK or install .NET 11 automatically. This implementation is a source/API/package compatibility evaluation, not a claim that a .NET 11 comparison was compiled or benchmarked. A later isolated comparison should prove equivalent draft/proposal/action behavior and quantify code reduction before adopting it. .NET 11 components would not themselves improve parsing, templating or WPF performance.

Primary protocol references: [AG-UI .NET SDK](https://devblogs.microsoft.com/dotnet/ag-ui-dotnet-sdk/), [trusted frontend/server guidance](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/getting-started?pivots=programming-language-csharp), [Blazor .NET 10](https://learn.microsoft.com/aspnet/core/blazor/?view=aspnetcore-10.0), and [published experimental package metadata](https://www.nuget.org/api/v2/Packages(Id='Microsoft.AspNetCore.Components.AI',Version='0.1.0-preview.1.26466.103')).
