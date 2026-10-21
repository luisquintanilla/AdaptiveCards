using System.Text.Json;
using AdaptiveCards.Workbench.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveCards.Workbench.Tests;

[TestClass]
public class WorkbenchTests
{
    private static CardProposal Proposal(WorkbenchSession session, string card = CardDocument.InitialCard) =>
        new(session.DocumentId, session.Revision, "run", "call", card, session.DraftData);

    [TestMethod]
    public void InvalidDraftPreservesCommittedDocument()
    {
        var session = new WorkbenchSession();
        session.Edit("{", session.DraftData);
        Assert.ThrowsException<InvalidDataException>(() => session.Commit());
        Assert.AreEqual(CardDocument.InitialCard, session.CommittedCard);
        Assert.AreEqual("{", session.DraftCard);
    }

    [TestMethod]
    public void ProposalAcceptPreservesUnknownFieldsAndCommitsAtomically()
    {
        var session = new WorkbenchSession();
        var json = CardDocument.InitialCard.Replace("\"version\": \"1.5\"", "\"version\": \"1.5\", \"x-host-extension\": 42");
        Assert.IsTrue(session.Offer(Proposal(session, json), "run", "call"));
        Assert.AreEqual(CardDocument.InitialCard, session.CommittedCard);
        session.Accept();
        Assert.AreEqual(json, session.DraftCard);
        Assert.AreEqual(42, CardDocument.ParseObject(session.CommittedCard)["x-host-extension"]!.GetValue<int>());
        Assert.IsNull(session.Proposal);
    }

    [TestMethod]
    public void RejectDoesNotChangeDraftOrCommittedCard()
    {
        var session = new WorkbenchSession();
        session.Edit(session.DraftCard, """{"name":"User draft"}""");
        session.Offer(Proposal(session), "run", "call");
        session.Reject();
        Assert.AreEqual("""{"name":"User draft"}""", session.DraftData);
        Assert.AreEqual("""{"name":"Adaptive Cards"}""", session.CommittedData);
    }

    [TestMethod]
    public void EditDuringRunRejectsStaleProposal()
    {
        var session = new WorkbenchSession();
        var proposal = Proposal(session);
        session.Edit(session.DraftCard, """{"name":"Newer edit"}""");
        Assert.ThrowsException<InvalidDataException>(() => session.Offer(proposal, "run", "call"));
        Assert.IsNull(session.Proposal);
        Assert.AreEqual("""{"name":"Newer edit"}""", session.DraftData);
    }

    [TestMethod]
    public void EditAfterProposalPreventsAccept()
    {
        var session = new WorkbenchSession();
        session.Offer(Proposal(session), "run", "call");
        session.Edit(session.DraftCard, """{"name":"Newer edit"}""");
        Assert.ThrowsException<InvalidDataException>(() => session.Accept());
        Assert.AreEqual(CardDocument.InitialCard, session.CommittedCard);
    }

    [TestMethod]
    public void DuplicateProposalIsIgnoredAndForeignCorrelationIsRejected()
    {
        var session = new WorkbenchSession();
        var proposal = Proposal(session);
        Assert.IsTrue(session.Offer(proposal, "run", "call"));
        Assert.IsFalse(session.Offer(proposal, "run", "call"));
        Assert.ThrowsException<InvalidDataException>(() => session.Offer(proposal with { DocumentId = "other" }, "run", "call"));
        Assert.ThrowsException<InvalidDataException>(() => session.Offer(proposal, "wrong-run", "call"));
    }

    [TestMethod]
    public void JsonByteDepthAndResourceSchemeLimitsAreEnforced()
    {
        Assert.ThrowsException<InvalidDataException>(() => CardDocument.ParseObject("{\"text\":\"" + new string('\u00e9', CardDocument.MaximumBytes) + "\"}"));
        Assert.ThrowsException<InvalidDataException>(() => CardDocument.ParseObject("{\"a\":" + new string('[', 40) + "0" + new string(']', 40) + "}"));
        var card = """{"type":"AdaptiveCard","version":"1.5","body":[{"type":"Image","url":"javascript:alert(1)"}]}""";
        Assert.ThrowsException<InvalidDataException>(() => CardDocument.Validate(card, "{}"));
        var template = card.Replace("javascript:alert(1)", "${url}");
        Assert.ThrowsException<InvalidDataException>(() => CardDocument.Validate(template, """{"url":"http://example.com/image.png"}"""));
    }

    [TestMethod]
    public void TemplateExpandsWithoutChangingRawSource()
    {
        var session = new WorkbenchSession();
        var validation = session.Commit();
        StringAssert.Contains(validation.ExpandedJson, "Hello Adaptive Cards");
        StringAssert.Contains(session.CommittedCard, "${name}");
    }
}
