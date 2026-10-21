using System.Text.Json;
using AGUI.Abstractions;
using AGUI.Client;
using AdaptiveCards.Workbench.State;
using Microsoft.Extensions.AI;

namespace AdaptiveCards.Workbench.Agent;

public sealed class WorkbenchAgentClient(IHttpClientFactory httpClientFactory)
{
    public async Task<CardProposal> ProposeAsync(Uri endpoint, AgentRequest request, string threadId,
        string runId, CancellationToken cancellationToken)
    {
        using var http = httpClientFactory.CreateClient();
        using var client = new AGUIChatClient(new(http, endpoint.AbsoluteUri));
        var options = new ChatOptions
        {
            RawRepresentationFactory = _ => new RunAgentInput
            {
                ThreadId = threadId,
                RunId = runId,
                State = JsonSerializer.SerializeToElement(request, DemoAgent.JsonOptions)
            }
        };
        CardProposal? proposal = null;
        var started = false;
        var finished = false;
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, request.Action is null ? "Propose a card revision." : "Record demo feedback.")],
            options, cancellationToken))
        {
            if (update.RawRepresentation is RunStartedEvent start)
            {
                if (started || start.ThreadId != threadId || start.RunId != runId)
                    throw new InvalidDataException("Unexpected AG-UI run start.");
                started = true;
            }
            else if (update.RawRepresentation is RunFinishedEvent finish)
            {
                if (!started || finished || finish.ThreadId != threadId || finish.RunId != runId)
                    throw new InvalidDataException("Unexpected AG-UI run completion.");
                finished = true;
            }
            else if (update.RawRepresentation is StateSnapshotEvent snapshot)
            {
                if (!started || finished) throw new InvalidDataException("Proposal arrived outside an active AG-UI run.");
                if (proposal is not null)
                    throw new InvalidDataException("Received more than one proposal for this run.");
                proposal = snapshot.Snapshot.Deserialize<CardProposal>(DemoAgent.JsonOptions)
                    ?? throw new InvalidDataException("The agent returned an empty proposal.");
            }
        }
        if (!finished) throw new InvalidDataException("The agent stream ended before run completion.");
        return proposal ?? throw new InvalidDataException("The agent stream ended without a complete proposal.");
    }
}
