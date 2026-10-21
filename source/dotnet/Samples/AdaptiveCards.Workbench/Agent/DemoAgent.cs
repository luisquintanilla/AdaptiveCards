using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGUI.Abstractions;
using AdaptiveCards.Workbench.State;

namespace AdaptiveCards.Workbench.Agent;

public static class DemoAgent
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = CardDocument.MaximumDepth
    };

    public static void MapDemoAgent(this WebApplication app)
    {
        app.MapPost("/agent", (RunAgentInput input, HttpContext context) =>
        {
            if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
                return Results.Problem("This unauthenticated demo endpoint is loopback-only.", statusCode: 403);
            try
            {
                var request = input.State?.Deserialize<AgentRequest>(JsonOptions)
                    ?? throw new InvalidDataException("Missing workbench state.");
                ValidateRequest(input, request);
                return Results.ServerSentEvents(Stream(input, request, context.RequestAborted));
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException or AdaptiveSerializationException)
            {
                return Results.Problem(error.Message, statusCode: 400);
            }
        });
    }

    private static void ValidateRequest(RunAgentInput input, AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(input.ThreadId) || input.ThreadId.Length > 64 ||
            string.IsNullOrWhiteSpace(input.RunId) || input.RunId.Length > 64 ||
            string.IsNullOrWhiteSpace(request.DocumentId) || request.DocumentId.Length > 64 ||
            string.IsNullOrWhiteSpace(request.CallId) || request.CallId.Length > 64 || request.BaseRevision < 0)
            throw new InvalidDataException("Invalid document/run/call correlation.");
        CardDocument.Validate(request.CardJson, request.DataJson);
        if (request.Action is { } action)
        {
            var data = CardDocument.ParseObject(action.GetRawText(), 8 * 1024);
            if (data["operation"]?.GetValue<string>() != "demo.submit")
                throw new InvalidDataException("Only the inert demo.submit action is allowed.");
        }
    }

    private static async IAsyncEnumerable<BaseEvent> Stream(
        RunAgentInput input, AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new RunStartedEvent { ThreadId = input.ThreadId, RunId = input.RunId };
        await Task.Delay(350, cancellationToken);
        var card = CardDocument.ParseObject(request.CardJson);
        var body = card["body"] as JsonArray;
        if (body is null) card["body"] = body = [];
        var text = request.Action is { } action
            ? $"Demo received associated inputs: {action.GetRawText()}"
            : "Agent proposal: this complete card arrived through an AG-UI state snapshot.";
        body.Add(new JsonObject { ["type"] = "TextBlock", ["text"] = text, ["wrap"] = true });
        var proposal = new CardProposal(request.DocumentId, request.BaseRevision, input.RunId,
            request.CallId, card.ToJsonString(), request.DataJson);
        CardDocument.Validate(proposal.CardJson, proposal.DataJson);
        yield return new StateSnapshotEvent { Snapshot = JsonSerializer.SerializeToElement(proposal, JsonOptions) };
        yield return new RunFinishedEvent { ThreadId = input.ThreadId, RunId = input.RunId };
    }
}
