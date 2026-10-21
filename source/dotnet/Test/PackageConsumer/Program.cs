using System.Reflection;
using System.Runtime.Versioning;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using AdaptiveCards;
using AdaptiveCards.Templating;

if (args.Length == 2)
{
    var contexts = new[] { new AssemblyLoadContext("legacy", true), new AssemblyLoadContext("modern", true) };
    string[][] surfaces = new string[2][];
    for (int i = 0; i < 2; i++)
    {
        var assembly = contexts[i].LoadFromAssemblyPath(Path.GetFullPath(args[i]));
        surfaces[i] = assembly.GetExportedTypes().SelectMany(type =>
            new[] { type.FullName + ":" + type.BaseType?.FullName }.Concat(
                type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(member => type.FullName + ":" + member.ToString())))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
    if (!surfaces[0].SequenceEqual(surfaces[1]))
    {
        Console.Error.WriteLine("Legacy only: " + string.Join("\n", surfaces[0].Except(surfaces[1])));
        Console.Error.WriteLine("Modern only: " + string.Join("\n", surfaces[1].Except(surfaces[0])));
        throw new InvalidOperationException("Public core reflection surface differs.");
    }
    Console.WriteLine($"Core public reflection surface parity passed: {surfaces[0].Length} entries.");
}

var core = typeof(AdaptiveCard).Assembly;
var templating = typeof(AdaptiveCardTemplate).Assembly;
string coreTarget = core.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
string templateTarget = templating.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
#if NET10_0_OR_GREATER
if (coreTarget != ".NETCoreApp,Version=v10.0" || templateTarget != ".NETCoreApp,Version=v10.0")
    throw new InvalidOperationException("The net10.0 package assets were not selected.");
#else
if (coreTarget != ".NETCoreApp,Version=v6.0" || templateTarget != ".NETCoreApp,Version=v8.0")
    throw new InvalidOperationException("The retained package assets were not selected.");
#endif
string expanded = new AdaptiveCardTemplate("""{"type":"AdaptiveCard","version":"1.3","body":[{"type":"TextBlock","text":"${name}"}]}""").Expand("""{"name":"Package smoke"}""");
var card = AdaptiveCard.FromJson(expanded).Card;
if (((AdaptiveTextBlock)card.Body[0]).Text != "Package smoke")
    throw new InvalidOperationException("Package expansion/parse failed.");
Console.WriteLine($"Core={core.FullName}; TFM={coreTarget}; Templating={templateTarget}; JSON={typeof(JsonNode).Assembly.FullName}");
Console.WriteLine("Package consumer passed.");
