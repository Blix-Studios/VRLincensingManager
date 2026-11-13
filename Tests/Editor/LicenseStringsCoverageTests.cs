using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.14: the whole license UI is localized. Every key of the UI table must exist in
    /// every supported language, carry the same placeholders as English, and format without
    /// throwing; the Dutch rows must keep the informal "je" of the rest of the package.
    /// </summary>
    public class LicenseStringsCoverageTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}");

        private static IEnumerable<string> KeyConstants()
        {
            foreach (FieldInfo f in typeof(LicenseStrings.Key).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) yield return (string)f.GetRawConstantValue();
        }

        [Test]
        public void EveryKeyExistsInEveryLanguage()
        {
            var missing = new List<string>();
            foreach (string key in LicenseStrings.UiKeys)
                foreach (string lang in LicenseStrings.SupportedLanguages)
                {
                    if (!LicenseStrings.TryGetUi(key, lang, out string text) || string.IsNullOrWhiteSpace(text) || text != text.Trim())
                        missing.Add(key + "/" + lang);
                }
            Assert.IsEmpty(missing, "missing or blank rows: " + string.Join(", ", missing));
            Assert.AreEqual(16, LicenseStrings.SupportedLanguages.Length, "12 + Arabic, Japanese, Korean, Urdu (pre.17)");
        }

        [Test]
        public void EveryKeyConstantIsInTheTableAndViceVersa()
        {
            var constants = new HashSet<string>(KeyConstants());
            var table = new HashSet<string>(LicenseStrings.UiKeys);
            Assert.IsTrue(constants.Count >= 70, "the pre.14 table has 78 keys");
            Assert.IsTrue(constants.SetEquals(table),
                "constants without rows: " + string.Join(", ", constants.Except(table)) +
                " — rows without constants: " + string.Join(", ", table.Except(constants)));
        }

        [Test]
        public void PlaceholdersMatchEnglishInEveryLanguage()
        {
            var bad = new List<string>();
            foreach (string key in LicenseStrings.UiKeys)
            {
                LicenseStrings.TryGetUi(key, "en", out string en);
                var expected = Sorted(Placeholder.Matches(en));
                foreach (string lang in LicenseStrings.SupportedLanguages)
                {
                    LicenseStrings.TryGetUi(key, lang, out string text);
                    if (Sorted(Placeholder.Matches(text)) != expected) bad.Add(key + "/" + lang);
                }
            }
            Assert.IsEmpty(bad, "placeholder mismatch: " + string.Join(", ", bad));
        }

        [Test]
        public void EveryRowFormatsWithoutThrowing()
        {
            object[] args = { "A", "B", "C" };
            foreach (string key in LicenseStrings.UiKeys)
                foreach (string lang in LicenseStrings.SupportedLanguages)
                {
                    LicenseStrings.TryGetUi(key, lang, out string raw);
                    string formatted = null;
                    Assert.DoesNotThrow(() => formatted = string.Format(raw, args), key + "/" + lang);
                    Assert.AreEqual(formatted, LicenseStrings.Format(key, lang, args), key + "/" + lang);
                }
        }

        [Test]
        public void DutchStaysInformal()
        {
            // "u"/"uw" are the formal forms; the package speaks "je" (upm_nl_strings.md).
            var formal = new Regex(@"\b(u|uw)\b", RegexOptions.IgnoreCase);
            var bad = new List<string>();
            foreach (string key in LicenseStrings.UiKeys)
            {
                LicenseStrings.TryGetUi(key, "nl", out string nl);
                if (formal.IsMatch(nl)) bad.Add(key + ": " + nl);
            }
            Assert.IsEmpty(bad, "formal Dutch: " + string.Join(" | ", bad));
            Assert.AreEqual("Probeer de demo", LicenseStrings.Get(LicenseStrings.Key.StartDemo, "nl"));
            Assert.AreEqual("Licentiecode invoeren", LicenseStrings.Get(LicenseStrings.Key.EnterKey, "nl"));
        }

        [Test]
        public void GetFallsBackToEnglishAndToTheKey()
        {
            Assert.AreEqual("Start Free Demo", LicenseStrings.Get(LicenseStrings.Key.StartDemo, "en"));
            Assert.AreEqual("Start Free Demo", LicenseStrings.Get(LicenseStrings.Key.StartDemo, "xx"), "unknown language → English");
            Assert.AreEqual("NoSuchKey", LicenseStrings.Get("NoSuchKey", "en"), "unknown key → the key itself");
            Assert.AreEqual("Kostenlose Demo starten", LicenseStrings.Get(LicenseStrings.Key.StartDemo, "de"));
        }

        [Test]
        public void ShortDurationAndWelcomeLine()
        {
            Assert.AreEqual("15 min", LicenseStrings.ShortDuration(900f, "en"));
            Assert.AreEqual("1 h", LicenseStrings.ShortDuration(3600f, "en"));
            Assert.AreEqual("1.5 h", LicenseStrings.ShortDuration(5400f, "en"));
            Assert.AreEqual("15 Min.", LicenseStrings.ShortDuration(900f, "de"));
            Assert.AreEqual("2 Std.", LicenseStrings.ShortDuration(7200f, "de"));
            Assert.AreEqual("15分钟", LicenseStrings.ShortDuration(900f, "zh"));
            Assert.AreEqual("1 uur", LicenseStrings.ShortDuration(3600f, "nl"));
            Assert.AreEqual("Free demo: 14 min remaining of 15 min",
                LicenseStrings.Format(LicenseStrings.Key.WelcomeDemoLine, "en",
                    LicenseStrings.ShortDuration(840f, "en"), LicenseStrings.ShortDuration(900f, "en")));
            Assert.AreEqual("Gratis demo: nog 14 min van 15 min",
                LicenseStrings.Format(LicenseStrings.Key.WelcomeDemoLine, "nl",
                    LicenseStrings.ShortDuration(840f, "nl"), LicenseStrings.ShortDuration(900f, "nl")));
            Assert.AreEqual("Demo  ·  14:59 left", LicenseStrings.Format(LicenseStrings.Key.DemoHud, "en", "14:59"));
        }

        [Test]
        public void LanguageOverrideRaisesLanguageChanged()
        {
            string before = LicenseStrings.LanguageOverride;
            int raised = 0;
            System.Action handler = () => raised++;
            LicenseStrings.LanguageChanged += handler;
            try
            {
                LicenseStrings.LanguageOverride = "nl";
                LicenseStrings.LanguageOverride = "nl"; // same value: no event
                LicenseStrings.LanguageOverride = "de";
                Assert.AreEqual(2, raised);
                Assert.AreEqual("de", LicenseStrings.CurrentLanguage);
            }
            finally
            {
                LicenseStrings.LanguageChanged -= handler;
                LicenseStrings.LanguageOverride = before;
            }
        }

        private static string Sorted(MatchCollection matches)
        {
            var list = new List<string>();
            foreach (Match m in matches) list.Add(m.Value);
            list.Sort();
            return string.Join("", list);
        }
    }

    internal static class SetExtensions
    {
        public static IEnumerable<string> Except(this HashSet<string> a, HashSet<string> b)
        {
            foreach (string s in a) if (!b.Contains(s)) yield return s;
        }
    }
}
