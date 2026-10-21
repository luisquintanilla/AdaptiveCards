using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdaptiveCards.Templating;

namespace AdaptiveCards.Workbench.State;

public static class CardDocument
{
    public const int MaximumBytes = 64 * 1024;
    public const int MaximumDepth = 32;
    public const string InitialCard = """
        {
          "type": "AdaptiveCard",
          "version": "1.5",
          "body": [
            { "type": "TextBlock", "text": "Hello ${name}", "size": "Large", "weight": "Bolder" },
            { "type": "Input.Text", "id": "comment", "label": "Your feedback", "placeholder": "Type before requesting a proposal" }
          ],
          "actions": [
            { "type": "Action.Submit", "title": "Send demo feedback", "data": { "operation": "demo.submit" } }
          ]
        }
        """;

    public static JsonObject ParseObject(string json, int maximumBytes = MaximumBytes)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("JSON must not be empty.");
        if (Encoding.UTF8.GetByteCount(json) > maximumBytes)
            throw new InvalidDataException($"JSON exceeds the {maximumBytes}-byte limit.");

        try
        {
            return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = MaximumDepth }) as JsonObject
                ?? throw new InvalidDataException("Expected a JSON object.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Invalid JSON: {error.Message}", error);
        }
    }

    public static CardValidation Validate(string cardJson, string dataJson)
    {
        var card = ParseObject(cardJson);
        if (card["type"]?.GetValue<string>() != "AdaptiveCard")
            throw new InvalidDataException("The document must have type AdaptiveCard.");
        ParseObject(dataJson);
        var expanded = new AdaptiveCardTemplate(cardJson).Expand(dataJson);
        var expandedCard = ParseObject(expanded);
        ValidateUrls(expandedCard);
        var parsed = AdaptiveCard.FromJson(expanded);
        return new(expanded, parsed.Warnings.Select(w => w.Message).ToArray());
    }

    private static void ValidateUrls(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Key is "url" or "iconUrl" or "backgroundImage" &&
                    property.Value is JsonValue value && value.TryGetValue<string>(out var url) &&
                    !string.IsNullOrWhiteSpace(url) &&
                    (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                    throw new InvalidDataException("Preview resources must use absolute HTTPS URLs.");
                ValidateUrls(property.Value);
            }
        }
        else if (node is JsonArray array)
            foreach (var item in array) ValidateUrls(item);
    }
}

public sealed record CardValidation(string ExpandedJson, string[] Warnings);
public sealed record CardProposal(string DocumentId, long BaseRevision, string RunId, string CallId, string CardJson, string DataJson);
public sealed record AgentRequest(string DocumentId, long BaseRevision, string CardJson, string DataJson, string CallId, JsonElement? Action);
