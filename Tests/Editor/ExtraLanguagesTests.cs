using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.17: Arabic, Japanese, Korean and Urdu. Coverage of the UI table is checked by
    /// LicenseStringsCoverageTests for every supported language; here: the older per-topic
    /// tables, the plural rules, Arabic shaping for TextMeshPro, and labels that must never end
    /// up empty when a font cannot draw a text.
    /// </summary>
    public class ExtraLanguagesTests
    {
        private static readonly string[] Extra = { "ar", "ja", "ko", "ur" };

        [Test]
        public void SixteenLanguages()
        {
            Assert.AreEqual(16, LicenseStrings.SupportedLanguages.Length);
            CollectionAssert.IsSubsetOf(Extra, LicenseStrings.SupportedLanguages);
            CollectionAssert.AreEqual(Extra, LicenseStrings.ExtraLanguages);
        }

        [Test]
        public void EveryOlderStringIsTranslated_NotEnglish()
        {
            foreach (string lang in Extra)
            {
                var texts = new Dictionary<string, string>
                {
                    { "DeviceCode", LicenseStrings.DeviceCodeLabel(lang) },
                    { "BestValue", LicenseStrings.BestValue(lang) },
                    { "Benefits", LicenseStrings.Benefits(lang) },
                    { "Subscribe", LicenseStrings.Subscribe(lang) },
                    { "Back", LicenseStrings.Back(lang) },
                    { "Feedback", LicenseStrings.Feedback(lang) },
                    { "RatingHint", LicenseStrings.RatingHint(lang) },
                    { "TextPlaceholder", LicenseStrings.TextPlaceholder(lang) },
                    { "Send", LicenseStrings.Send(lang) },
                    { "Skip", LicenseStrings.Skip(lang) },
                    { "ChooseCategory", LicenseStrings.ChooseCategory(lang) },
                    { "Thanks", LicenseStrings.Thanks(lang) },
                    { "SendFailed", LicenseStrings.SendFailed(lang) },
                    { "RateLimited", LicenseStrings.RateLimited(lang) },
                    { "ExitQuestion", LicenseStrings.ExitQuestion(lang) },
                    { "ExitThanks", LicenseStrings.ExitThanks(lang) },
                    { "CompanyHint", LicenseStrings.CompanyHint(lang) },
                    { "IntroNote", LicenseStrings.IntroOfferNote("X", "Y", lang) },
                };
                foreach (string term in new[] { "MONTHLY", "SEMIANNUAL", "ANNUAL", "WEEKLY", "QUARTERLY" })
                    texts["Term " + term] = LicenseStrings.TermLabel(term, lang);
                foreach (string c in new[] { AppFeedback.CategoryBug, AppFeedback.CategoryPerformance, AppFeedback.CategoryContent, AppFeedback.CategorySuggestion })
                    texts["Category " + c] = LicenseStrings.CategoryLabel(c, lang);
                foreach (string r in new[] { AppFeedback.ReasonPrice, AppFeedback.ReasonForMyCompany, AppFeedback.ReasonNotWhatIExpected, AppFeedback.ReasonOther })
                    texts["Reason " + r] = LicenseStrings.ExitReasonLabel(r, lang);

                foreach (var pair in texts)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Value), lang + " " + pair.Key);
                    Assert.IsTrue(pair.Value.Any(ch => ch > 0x24F), $"{lang} {pair.Key} is not translated: '{pair.Value}'");
                }
            }
        }

        [Test]
        public void EveryUiRowOfTheNewLanguagesIsTranslated()
        {
            // Rows that are legitimately the same in every language would need an exception here; there are none.
            foreach (string key in LicenseStrings.UiKeys)
                foreach (string lang in Extra)
                {
                    Assert.IsTrue(LicenseStrings.TryGetUi(key, lang, out string text), key + "/" + lang);
                    Assert.IsTrue(text.Any(ch => ch > 0x24F), $"{key}/{lang} is not translated: '{text}'");
                }
        }

        [Test]
        public void LanguageSelection()
        {
            Assert.AreEqual("ar", LicenseStrings.FromSystemLanguage(SystemLanguage.Arabic));
            Assert.AreEqual("ja", LicenseStrings.FromSystemLanguage(SystemLanguage.Japanese));
            Assert.AreEqual("ko", LicenseStrings.FromSystemLanguage(SystemLanguage.Korean));

            string before = LicenseStrings.LanguageOverride;
            try
            {
                LicenseStrings.LanguageOverride = "ur";
                Assert.AreEqual("ur", LicenseStrings.CurrentLanguage, "Urdu comes from the host's selector");
                LicenseStrings.LanguageOverride = "ar-SA";
                Assert.AreEqual("ar", LicenseStrings.CurrentLanguage);
            }
            finally
            {
                LicenseStrings.LanguageOverride = before;
            }

            Assert.IsTrue(LicenseStrings.IsRightToLeft("ar"));
            Assert.IsTrue(LicenseStrings.IsRightToLeft("ur"));
            Assert.IsFalse(LicenseStrings.IsRightToLeft("ja"));
            Assert.IsFalse(LicenseStrings.IsRightToLeft("en"));
        }

        [Test]
        public void Durations()
        {
            Assert.AreEqual("3か月", LicenseStrings.Duration("MONTHLY", 3, "ja"));
            Assert.AreEqual("1年", LicenseStrings.Duration("ANNUAL", 1, "ja"));
            Assert.AreEqual("3개월", LicenseStrings.Duration("MONTHLY", 3, "ko"));
            Assert.AreEqual("2주", LicenseStrings.Duration("WEEKLY", 2, "ko"));
            Assert.AreEqual("3 ماہ", LicenseStrings.Duration("MONTHLY", 3, "ur"));
            Assert.AreEqual("2 ہفتے", LicenseStrings.Duration("WEEKLY", 2, "ur"));

            // Arabic: 1 singular, 2 dual without the number, 3-10 plural, 11+ singular.
            Assert.AreEqual("1 شهر", LicenseStrings.Duration("MONTHLY", 1, "ar"));
            Assert.AreEqual("شهرين", LicenseStrings.Duration("MONTHLY", 2, "ar"));
            Assert.AreEqual("3 أشهر", LicenseStrings.Duration("MONTHLY", 3, "ar"));
            Assert.AreEqual("6 أشهر", LicenseStrings.Duration("SEMIANNUAL", 1, "ar"));
            Assert.AreEqual("12 شهرًا", LicenseStrings.Duration("MONTHLY", 12, "ar"));
            Assert.AreEqual("1 سنة", LicenseStrings.Duration("ANNUAL", 1, "ar"));

            Assert.AreEqual("15分", LicenseStrings.ShortDuration(900f, "ja"));
            Assert.AreEqual("15분", LicenseStrings.ShortDuration(900f, "ko"));
            Assert.AreEqual("15 دقيقة", LicenseStrings.ShortDuration(900f, "ar"));
        }

        // ───────────── Arabic shaping ─────────────

        private static string Codes(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

        [Test]
        public void TextWithoutArabic_IsUntouched()
        {
            const string latin = "Start Free Demo (15 min)";
            Assert.AreSame(latin, RtlText.ForRtlLayout(latin));
            Assert.AreSame("日本語", RtlText.ForRtlLayout("日本語"));
            Assert.IsNull(RtlText.ForRtlLayout(null));
            Assert.IsFalse(RtlText.IsRtl(latin));
        }

        [Test]
        public void Shaping_ContextualForms()
        {
            // سلام: seen (initial) + lam-alef ligature (final) + meem (isolated: alef does not join forward)
            Assert.AreEqual("FEB3 FEFC FEE1", Codes(RtlText.ForRtlLayout("سلام")));
            // بيت: beh initial, yeh medial, teh final
            Assert.AreEqual("FE91 FEF4 FE96", Codes(RtlText.ForRtlLayout("بيت")));
            // دار: dal, alef and reh never join forward → all isolated except alef? dal(iso) alef(iso) reh(iso)
            Assert.AreEqual("FEA9 FE8D FEAD", Codes(RtlText.ForRtlLayout("دار")));
            // a single letter is isolated
            Assert.AreEqual("FE8F", Codes(RtlText.ForRtlLayout("ب")));
        }

        [Test]
        public void Shaping_LamAlef()
        {
            Assert.AreEqual("FEFB", Codes(RtlText.ForRtlLayout("لا")));                 // isolated ligature
            Assert.AreEqual("FE91 FEFC", Codes(RtlText.ForRtlLayout("بلا")));           // beh initial + final ligature
            Assert.AreEqual("FEF7", Codes(RtlText.ForRtlLayout("لأ")));                 // lam + alef with hamza above
        }

        [Test]
        public void Shaping_WordsDoNotJoinAcrossSpaces()
        {
            // "من هنا": noon is final in the first word, heh initial in the second.
            string shaped = RtlText.ForRtlLayout("من هنا");
            Assert.AreEqual("FEE3 FEE6 0020 FEEB FEE8 FE8E", Codes(shaped));
        }

        [Test]
        public void Shaping_HarakatDoNotBreakJoining()
        {
            // شكرًا: sheen initial, kaf medial, reh final, fathatan stays, alef isolated (reh does not join forward)
            Assert.AreEqual("FEB7 FEDC FEAE 064B FE8D", Codes(RtlText.ForRtlLayout("شكرًا")));
        }

        [Test]
        public void Shaping_UrduLetters()
        {
            // کی: keheh initial + farsi yeh final
            Assert.AreEqual("FB90 FBFD", Codes(RtlText.ForRtlLayout("کی")));
            // ہے: heh goal initial + yeh barree final
            Assert.AreEqual("FBA8 FBAF", Codes(RtlText.ForRtlLayout("ہے")));
            // ڈیمو: ddal (isolated, does not join forward) + farsi yeh initial + meem medial + waw final
            Assert.AreEqual("FB88 FBFE FEE4 FEEE", Codes(RtlText.ForRtlLayout("ڈیمو")));
        }

        [Test]
        public void LatinAndDigits_AreReversedSoTheyStillReadLeftToRight()
        {
            // In RTL layout each character goes to the left of the previous one.
            string shaped = RtlText.ForRtlLayout("رمز QR");
            Assert.IsTrue(shaped.EndsWith("RQ"), Codes(shaped));

            string minutes = RtlText.ForRtlLayout("15 دقيقة");
            Assert.IsTrue(minutes.StartsWith("51 "), Codes(minutes));

            string store = RtlText.ForRtlLayout("من Meta Horizon Store هنا");
            StringAssert.Contains("erotS noziroH ateM", store);
        }

        [Test]
        public void KeyFormat_AndBrackets()
        {
            string shaped = RtlText.ForRtlLayout("المفتاح (XXXX-XXXX-XXXX-XXXX)");
            // the brackets around the run are mirrored, the run itself reads the same reversed
            StringAssert.EndsWith(")XXXX-XXXX-XXXX-XXXX(", shaped);

            // a bracket opened inside a Latin run closes inside it
            string inner = RtlText.ForRtlLayout("نص abc (def) نص");
            StringAssert.Contains(")fed( cba", inner);
        }

        [Test]
        public void RichTextTags_AreLeftAlone()
        {
            string shaped = RtlText.ForRtlLayout("الرمز  <b>SAVE50</b>");
            StringAssert.Contains("<b>05EVAS</b>", shaped);
            string sized = RtlText.ForRtlLayout("سنوي\n<size=72%>لمدة 3 أشهر</size>");
            StringAssert.Contains("<size=72%>", sized);
            StringAssert.Contains("</size>", sized);
            Assert.AreEqual(2, sized.Split('\n').Length, "lines stay lines");
        }

        [Test]
        public void TextAlreadyShapedBySomeoneElse_IsOnlyTurnedAround()
        {
            // A host app's Arabic fixer writes visual order with presentation forms (ﻡﻼﺳ for سلام).
            string visual = "ﻡﻼﺳ";
            Assert.AreEqual("FEB3 FEFC FEE1", Codes(RtlText.ForRtlLayout(visual)));
            Assert.IsTrue(RtlText.IsRtl(visual));
        }

        [Test]
        public void EveryArabicAndUrduString_ShapesCompletely()
        {
            // No base letter may survive shaping (it would be drawn disconnected), and nothing may be lost.
            foreach (string lang in new[] { "ar", "ur" })
                foreach (string key in LicenseStrings.UiKeys)
                {
                    string logical = LicenseStrings.Format(key, lang, "A", "B");
                    string shaped = RtlText.ForRtlLayout(logical);
                    Assert.IsFalse(RtlText.HasArabicLetters(shaped), $"{key}/{lang}: unshaped letters in {Codes(shaped)}");
                    Assert.IsTrue(RtlText.HasPresentationForms(shaped), key + "/" + lang);
                    Assert.LessOrEqual(shaped.Length, logical.Length, "only lam-alef pairs shrink the text");
                    Assert.GreaterOrEqual(shaped.Length, logical.Length - logical.Count(c => c == 'ل'));
                }
        }

        // ───────────── labels ─────────────

        private GameObject root;
        private LicenseConfig config;
        private string languageBefore;

        [SetUp]
        public void SetUp()
        {
            languageBefore = LicenseStrings.LanguageOverride;
        }

        [TearDown]
        public void TearDown()
        {
            LicenseStrings.LanguageOverride = languageBefore;
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }

        private LicenseUIBuilder Build()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            config = ScriptableObject.CreateInstance<LicenseConfig>();
            root = new GameObject("ExtraLanguagesTests UI");
            var builder = root.AddComponent<LicenseUIBuilder>();
            typeof(LicenseUIBuilder).GetField("config", flags).SetValue(builder, config);
            typeof(LicenseUIBuilder).GetMethod("ApplyStoreSettings", flags).Invoke(builder, new object[] { StorePlatform.None });
            typeof(LicenseUIBuilder).GetMethod("BuildUI", flags).Invoke(builder, null);
            return builder;
        }

        [Test]
        public void EveryLabelShapesArabic_AndFollowsItsTextIntoRtlMode()
        {
            var builder = Build();
            var labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            Assert.Greater(labels.Length, 30);

            // Input texts are typed by the user (license keys): left alone.
            var typed = root.GetComponentsInChildren<TMP_InputField>(true).Select(f => f.textComponent).ToList();
            foreach (var label in labels.Where(l => !typed.Contains(l)))
                Assert.AreSame(RtlText.Preprocessor.Instance, label.textPreprocessor, label.name);

            var title = labels.First(l => l.name == "WelcomeSubtitle");
            var sync = typeof(LicenseUIBuilder).GetMethod("SyncRtl", BindingFlags.Instance | BindingFlags.NonPublic);
            title.text = "اختر خيارًا للمتابعة";
            sync.Invoke(builder, null);
            Assert.IsTrue(title.isRightToLeftText);

            title.text = "Select an option to continue";
            sync.Invoke(builder, null);
            Assert.IsFalse(title.isRightToLeftText, "back to left-to-right with Latin text");
        }

        // ───────────── never an empty label ─────────────

        // Fake fonts: the set of scripts each one can draw.
        private static bool Draws(string font, string text)
        {
            foreach (char c in text)
            {
                if (c == ' ') continue;
                bool arabic = c >= (char)0x0600;
                if (arabic && !font.Contains("arabic")) return false;
                if (!arabic && !font.Contains("latin")) return false;
            }
            return true;
        }

        private static (bool ok, string text, string font) Choose(string localized, params string[] fonts)
        {
            bool ok = LabelFallback.Choose(fonts, localized, "Start", Draws, out string text, out string font);
            return (ok, text, font);
        }

        [Test]
        public void Label_LocalizedTextWithTheFontThatCanDrawIt()
        {
            Assert.AreEqual((true, "ابدأ", "arabic"), Choose("ابدأ", "arabic"));
            Assert.AreEqual((true, "ابدأ", "arabic"), Choose("ابدأ", "latin", "arabic"),
                "the label moves to the font that has the script");
        }

        [Test]
        public void Label_EnglishWhenNoFontHasTheLanguage()
        {
            Assert.AreEqual((true, "Start", "latin"), Choose("ابدأ", "latin"));
        }

        [Test]
        public void Label_HostSwappedInAFontWithoutLatin_EnglishStillShows()
        {
            // VR-Forklift batch 1: the default font became an Arabic face with no Latin glyphs
            // while the package had no Arabic strings. The label was English text in a font that
            // could not draw it: empty. Now it goes back to the font the UI was built with.
            var mixed = Choose("ابدأ Meta", "arabic", "latin");
            Assert.AreEqual((true, "Start", "latin"), mixed, "neither font draws Arabic + Latin together");

            bool ok = LabelFallback.Choose(new[] { "arabic", "latin" }, null, "Start", Draws, out string text, out string font);
            Assert.IsTrue(ok);
            Assert.AreEqual("Start", text);
            Assert.AreEqual("latin", font);
        }

        [Test]
        public void Label_NoFontDrawsAnything_KeepsEnglishAndItsFont()
        {
            // "x" and "y" draw nothing: nothing better exists, so English stays on the label's own font.
            Assert.AreEqual((false, "Start", "x"), Choose("ابدأ", "x", "y"));
        }

        [Test]
        public void BuiltUi_InArabic_HasNoEmptyLabel()
        {
            LicenseStrings.LanguageOverride = "ar";
            Build();
            var subtitle = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(l => l.name == "WelcomeSubtitle");
            Assert.IsFalse(string.IsNullOrEmpty(subtitle.text));

            string arabic = LicenseStrings.Get(LicenseStrings.Key.WelcomeSubtitle, "ar");
            bool fontDrawsArabic = subtitle.font == null ||
                                   subtitle.font.HasCharacters(RtlText.ForRtlLayout(arabic), out uint[] _, true, true);
            Assert.AreEqual(fontDrawsArabic ? arabic : "Select an option to continue", subtitle.text);
        }
    }
}
