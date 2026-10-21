namespace AdaptiveCards.Workbench.State;

public sealed class WorkbenchSession
{
    private readonly HashSet<string> _completedCalls = [];
    public string DocumentId { get; } = Guid.NewGuid().ToString("N");
    public string ThreadId { get; } = Guid.NewGuid().ToString("N");
    public long Revision { get; private set; }
    public string DraftCard { get; private set; } = CardDocument.InitialCard;
    public string DraftData { get; private set; } = """{"name":"Adaptive Cards"}""";
    public string CommittedCard { get; private set; } = CardDocument.InitialCard;
    public string CommittedData { get; private set; } = """{"name":"Adaptive Cards"}""";
    public CardProposal? Proposal { get; private set; }

    public void Edit(string card, string data)
    {
        if (card == DraftCard && data == DraftData) return;
        DraftCard = card;
        DraftData = data;
        Revision++;
    }

    public CardValidation Commit()
    {
        var validation = CardDocument.Validate(DraftCard, DraftData);
        CommittedCard = DraftCard;
        CommittedData = DraftData;
        Revision++;
        return validation;
    }

    public bool Offer(CardProposal proposal, string runId, string callId)
    {
        if (proposal.DocumentId != DocumentId || proposal.RunId != runId || proposal.CallId != callId)
            throw new InvalidDataException("The proposal does not match this document, run and call.");
        if (_completedCalls.Contains(callId)) return false;
        if (proposal.BaseRevision != Revision)
            throw new InvalidDataException("The proposal is stale. Your draft was preserved; request a new proposal.");
        CardDocument.Validate(proposal.CardJson, proposal.DataJson);
        Proposal = proposal;
        _completedCalls.Add(callId);
        return true;
    }

    public CardValidation Accept()
    {
        var proposal = Proposal ?? throw new InvalidOperationException("There is no pending proposal.");
        if (proposal.BaseRevision != Revision)
            throw new InvalidDataException("The proposal is stale. Reject it and request a new one.");
        var validation = CardDocument.Validate(proposal.CardJson, proposal.DataJson);
        DraftCard = CommittedCard = proposal.CardJson;
        DraftData = CommittedData = proposal.DataJson;
        Revision++;
        Proposal = null;
        return validation;
    }

    public void Reject() => Proposal = null;
}
