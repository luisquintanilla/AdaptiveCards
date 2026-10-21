using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AdaptiveCards;
using AdaptiveCards.Rendering;
using AdaptiveCards.Templating;

const string cardJson = """{"type":"AdaptiveCard","version":"1.3","body":[{"type":"TextBlock","text":"Hello **world**"},{"type":"Input.Text","id":"name","value":"Ada"}]}""";
const string templateJson = """{"type":"AdaptiveCard","version":"1.3","body":[{"$data":"${items}","$when":"${visible}","type":"TextBlock","text":"${$root.title}: ${name}"}]}""";
const string data = """{"title":"Team","items":[{"name":"Ada","visible":true},{"name":"Grace","visible":false},{"name":"Linus","visible":true}]}""";
const string functions = "Due {{DATE(2026-10-21T12:34:56Z, LONG)}} at {{TIME(2026-10-21T12:34:56Z)}}";

Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}; OS={RuntimeInformation.OSDescription}; Architecture={RuntimeInformation.ProcessArchitecture}; ServerGC={GCSettings.IsServerGC}");
foreach (Assembly assembly in new[] { typeof(AdaptiveCard).Assembly, typeof(AdaptiveCardTemplate).Assembly, typeof(System.Text.Json.Nodes.JsonNode).Assembly, typeof(Newtonsoft.Json.JsonConvert).Assembly })
{
    Console.WriteLine($"Asset={assembly.GetName().Name}; Version={assembly.GetName().Version}; TFM={assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName}; Bytes={new FileInfo(assembly.Location).Length}; Path={DisplayAssemblyPath(assembly)}");
}

// Cold samples include first-use JIT/initialization; launch a fresh process for each replicate.
Measure("text-functions", () => RendererUtilities.ApplyTextFunctions(functions, "en-US"), 5000);
Measure("parse-serialize", () => AdaptiveCard.FromJson(cardJson).Card.ToJson(), 2000);
var markdown = new Microsoft.MarkedNet.Marked();
Measure("markdown", () => markdown.Parse("# Team\n\nHello **world**, [link](https://example.org).\n\n- One\n- Two\n\n> Quote with `code`"), 2000);
Measure("template-new-expand", () => new AdaptiveCardTemplate(templateJson).Expand(data), 500);
var template = new AdaptiveCardTemplate(templateJson);
Measure("template-repeated-expand", () => template.Expand(data), 1000);

static string DisplayAssemblyPath(Assembly assembly)
{
    foreach (var (root, label) in new[]
    {
        (AppContext.BaseDirectory, "<app>"),
        (RuntimeEnvironment.GetRuntimeDirectory(), "<runtime>")
    })
    {
        var relative = Path.GetRelativePath(root, assembly.Location);
        if (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            return Path.Combine(label, relative);
    }
    return Path.Combine("<external>", Path.GetFileName(assembly.Location));
}

static void Measure(string name, Func<string> action, int iterations)
{
    long startBytes = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    string result = action();
    stopwatch.Stop();
    Console.WriteLine($"{name}; cold-ms={stopwatch.Elapsed.TotalMilliseconds:F3}; cold-bytes={GC.GetAllocatedBytesForCurrentThread() - startBytes}");
    for (int i = 0; i < 200; i++) result = action();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    int[] collections = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
    startBytes = GC.GetAllocatedBytesForCurrentThread();
    stopwatch.Restart();
    for (int i = 0; i < iterations; i++) result = action();
    stopwatch.Stop();
    Console.WriteLine($"{name}; iterations={iterations}; us/op={stopwatch.Elapsed.TotalMicroseconds / iterations:F3}; bytes/op={(GC.GetAllocatedBytesForCurrentThread() - startBytes) / iterations}; gen0={GC.CollectionCount(0) - collections[0]}; gen1={GC.CollectionCount(1) - collections[1]}; gen2={GC.CollectionCount(2) - collections[2]}; result-length={result.Length}");
    GC.KeepAlive(result);
}
