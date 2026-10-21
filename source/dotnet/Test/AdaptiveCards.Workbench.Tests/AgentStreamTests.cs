using System.Net;
using System.Text;
using System.Text.Json;
using AdaptiveCards.Workbench.Agent;
using AdaptiveCards.Workbench.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveCards.Workbench.Tests;

[TestClass]
public class AgentStreamTests
{
    private const string Started = """data: {"type":"RUN_STARTED","threadId":"thread","runId":"run"}""";
    private const string Finished = """data: {"type":"RUN_FINISHED","threadId":"thread","runId":"run"}""";
    private static readonly AgentRequest Request = new("document", 0, CardDocument.InitialCard, "{}", "call", null);
    private static readonly CardProposal Proposal = new("document", 0, "run", "call", CardDocument.InitialCard, "{}");
    private static string Snapshot => "data: " + JsonSerializer.Serialize(new
    {
        type = "STATE_SNAPSHOT", snapshot = Proposal
    }, DemoAgent.JsonOptions);

    private static async Task<CardProposal> RunAsync(string stream, CancellationToken cancellationToken = default)
    {
        using var factory = new StreamHttpFactory(stream);
        return await new WorkbenchAgentClient(factory).ProposeAsync(
            new Uri("http://localhost/agent"), Request, "thread", "run", cancellationToken);
    }

    [TestMethod]
    public async Task RealAguiClientDecodesCompleteSseProposal()
    {
        var proposal = await RunAsync($"{Started}\n\n{Snapshot}\n\n{Finished}\n\n");
        Assert.AreEqual(Proposal, proposal);
    }

    [TestMethod]
    public async Task MissingTerminalEventDoesNotPublishProposal()
    {
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => RunAsync($"{Started}\n\n{Snapshot}\n\n"));
    }

    [TestMethod]
    public async Task DuplicateSnapshotsFailInsteadOfOverwritingState()
    {
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => RunAsync($"{Started}\n\n{Snapshot}\n\n{Snapshot}\n\n{Finished}\n\n"));
    }

    [TestMethod]
    public async Task ForeignRunCompletionIsRejected()
    {
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => RunAsync(
            $"{Started}\n\n{Snapshot}\n\n{Finished.Replace("\"run\"", "\"foreign\"")}\n\n"));
    }

    [TestMethod]
    public async Task CancelledRequestDoesNotReturnProposal()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await RunAsync($"{Started}\n\n{Snapshot}\n\n{Finished}\n\n", cancellation.Token);
            Assert.Fail("A cancelled run must not return a proposal.");
        }
        catch (OperationCanceledException) { }
    }

    private sealed class StreamHttpFactory(string stream) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            });
        }
    }
}
