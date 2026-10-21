using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using AdaptiveCards.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveCards.Test
{
    [TestClass]
    public sealed class TextFunctionTests
    {
        [TestMethod]
        [DataRow("en-US")]
        [DataRow("fr-FR")]
        [DataRow("tr-TR")]
        public void TextFunctionRegex_CapturesMatchLegacyPattern(string culture)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var legacy = new Regex(@"\{\{(?<func>DATE|TIME){1}\((?<date>[\d]{4}-[\d]{2}-[\d]{2}T[\d]{2}:[\d]{2}:[\d]{2}[Z+-].*?){1}(?:,\s*(?<hint>SHORT|LONG|COMPACT){1}\s*)??\)\}\}", RegexOptions.ExplicitCapture);
                var selected = (Regex)typeof(RendererUtilities).GetField("TextFunctionRegex", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                const string input = "{{DATE(2026-10-21T12:34:56Z, LONG)}} {{TIME(2026-10-21T12:34:56+02:00)}} {{DATE(2026-10-21T12:34:56Z,  SHORT )}} {{TIME(2026-10-21T12:34:56Z, COMPACT)}} {{date(2026-10-21T12:34:56Z)}} {{DATE(invalid)}}";
                var expected = legacy.Matches(input);
                var actual = selected.Matches(input);
                Assert.AreEqual(4, actual.Count);
                Assert.AreEqual(expected.Count, actual.Count);
                Assert.AreEqual(legacy.MatchTimeout, selected.MatchTimeout);
                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.AreEqual(expected[i].Index, actual[i].Index);
                    Assert.AreEqual(expected[i].Value, actual[i].Value);
                    foreach (string group in new[] { "func", "date", "hint" })
                    {
                        Assert.AreEqual(expected[i].Groups[group].Value, actual[i].Groups[group].Value);
                        Assert.AreEqual(expected[i].Groups[group].Captures.Count, actual[i].Groups[group].Captures.Count);
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        [DataRow("en-US")]
        [DataRow("fr-FR")]
        public void ApplyTextFunctions_PreservesFormattingAndInvalidTokens(string culture)
        {
            var date = DateTime.Parse("2026-10-21T12:34:56Z", CultureInfo.InvariantCulture);
            var provider = CultureInfo.GetCultureInfo(culture);
            string input = "{{DATE(2026-10-21T12:34:56Z, LONG)}} | {{DATE(2026-10-21T12:34:56Z, SHORT)}} | {{TIME(2026-10-21T12:34:56Z)}} | {{TIME(2026-10-21T12:34:56Z, LONG)}} | {{DATE(invalid)}}";
            string expected = date.ToString("D", provider) + " | " + date.ToString("ddd, MMM d, yyyy", provider) + " | " + date.ToString("t", provider) + " | {{TIME(2026-10-21T12:34:56Z, LONG)}} | {{DATE(invalid)}}";
            Assert.AreEqual(expected, RendererUtilities.ApplyTextFunctions(input, culture));
            Assert.AreEqual(string.Empty, RendererUtilities.ApplyTextFunctions(null, culture));
        }

        [TestMethod]
        public void LoadedCoreAsset_MatchesRequestedCoverage()
        {
            string expected = Environment.GetEnvironmentVariable("ADAPTIVECARDS_EXPECTED_CORE_TFM") ?? ".NETCoreApp,Version=v10.0";
            Assert.AreEqual(expected, typeof(AdaptiveCard).Assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>().FrameworkName);
        }
    }
}
