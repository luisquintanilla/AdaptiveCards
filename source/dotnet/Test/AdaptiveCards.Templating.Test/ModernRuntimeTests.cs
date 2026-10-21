using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveCards.Templating.Test
{
    [TestClass]
    public sealed class ModernRuntimeTests
    {
        [TestMethod]
        public void RepeatedExpansion_KeepsRootAndRepeatedDataIsolated()
        {
            const string source = "{\"title\":\"${title}\",\"items\":[{\"$data\":\"${items}\",\"$when\":\"${visible}\",\"text\":\"${$root.title}: ${name}\"}]}";
            var template = new AdaptiveCardTemplate(source);
            string firstData = "{\"title\":\"First\",\"items\":[{\"name\":\"A\",\"visible\":true},{\"name\":\"Hidden\",\"visible\":false}]}";
            string secondData = "{\"title\":\"Second\",\"items\":[{\"name\":\"B\",\"visible\":true}]}";
            var first = JsonNode.Parse(template.Expand(firstData));
            var second = JsonNode.Parse(template.Expand(secondData));
            Assert.AreEqual("First: A", first["items"][0]["text"].GetValue<string>());
            Assert.AreEqual(1, first["items"].AsArray().Count);
            Assert.AreEqual("Second: B", second["items"][0]["text"].GetValue<string>());
            Assert.AreEqual(1, second["items"].AsArray().Count);
            Assert.AreEqual(first.ToJsonString(), JsonNode.Parse(template.Expand(firstData)).ToJsonString());
        }

        [TestMethod]
        public void LoadedTemplatingAsset_MatchesRequestedCoverage()
        {
            string expected = Environment.GetEnvironmentVariable("ADAPTIVECARDS_EXPECTED_TEMPLATING_TFM");
            if (expected == null)
            {
                expected = typeof(ModernRuntimeTests).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
            }
            Assert.AreEqual(expected, typeof(AdaptiveCardTemplate).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);
        }
    }
}
