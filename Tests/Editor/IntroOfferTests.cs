using NUnit.Framework;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>Intro-offer display (spec 08 §4.5): model flag, localized durations, note format, badge untouched.</summary>
    public class IntroOfferTests
    {
        private static StoreTermPrice Plan(string term, uint cents) => new StoreTermPrice
        {
            term = term, amountInHundredths = cents, formattedPrice = "US$" + (cents / 100m).ToString("0.00"), currency = "USD"
        };

        [Test]
        public void HasIntroOffer_RequiresPriceTermAndCount()
        {
            var plan = Plan("ANNUAL", 39999);
            Assert.IsFalse(plan.HasIntroOffer, "plain plan");

            plan.introFormattedPrice = "US$299.99";
            Assert.IsFalse(plan.HasIntroOffer, "price alone is not enough");
            plan.introTerm = "ANNUAL";
            Assert.IsFalse(plan.HasIntroOffer, "count still 0");
            plan.introTermCount = 1;
            Assert.IsTrue(plan.HasIntroOffer);
        }

        [Test]
        public void TermUnit_MapsEveryMetaOfferTerm()
        {
            Assert.IsTrue(StoreSubscription.TermUnit("WEEKLY", out int c, out string u)); Assert.AreEqual((1, "week"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("BIWEEKLY", out c, out u)); Assert.AreEqual((2, "week"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("monthly", out c, out u)); Assert.AreEqual((1, "month"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("QUARTERLY", out c, out u)); Assert.AreEqual((3, "month"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("SEMIANNUAL", out c, out u)); Assert.AreEqual((6, "month"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("ANNUAL", out c, out u)); Assert.AreEqual((1, "year"), (c, u));
            Assert.IsTrue(StoreSubscription.TermUnit("BIANNUAL", out c, out u)); Assert.AreEqual((2, "year"), (c, u));
            Assert.IsFalse(StoreSubscription.TermUnit("DECADE", out c, out u));
        }

        [Test]
        public void Duration_English()
        {
            Assert.AreEqual("1 month", LicenseStrings.Duration("MONTHLY", 1, "en"));
            Assert.AreEqual("3 months", LicenseStrings.Duration("MONTHLY", 3, "en"));
            Assert.AreEqual("6 months", LicenseStrings.Duration("QUARTERLY", 2, "en"));
            Assert.AreEqual("1 year", LicenseStrings.Duration("ANNUAL", 1, "en"));
            Assert.AreEqual("2 weeks", LicenseStrings.Duration("WEEKLY", 2, "en"));
            Assert.IsNull(LicenseStrings.Duration("ANNUAL", 0, "en"));
            Assert.IsNull(LicenseStrings.Duration("DECADE", 1, "en"));
        }

        [Test]
        public void Duration_Plurals_SpanishFrenchRussianChinese()
        {
            Assert.AreEqual("1 mes", LicenseStrings.Duration("MONTHLY", 1, "es"));
            Assert.AreEqual("3 meses", LicenseStrings.Duration("MONTHLY", 3, "es"));
            Assert.AreEqual("1 an", LicenseStrings.Duration("ANNUAL", 1, "fr"));
            Assert.AreEqual("2 ans", LicenseStrings.Duration("ANNUAL", 2, "fr"));
            Assert.AreEqual("1 месяц", LicenseStrings.Duration("MONTHLY", 1, "ru"));
            Assert.AreEqual("3 месяца", LicenseStrings.Duration("MONTHLY", 3, "ru"));
            Assert.AreEqual("6 месяцев", LicenseStrings.Duration("MONTHLY", 6, "ru"));
            Assert.AreEqual("12 месяцев", LicenseStrings.Duration("MONTHLY", 12, "ru"));
            Assert.AreEqual("21 месяц", LicenseStrings.Duration("MONTHLY", 21, "ru"));
            Assert.AreEqual("1 год", LicenseStrings.Duration("ANNUAL", 1, "ru"));
            Assert.AreEqual("2 года", LicenseStrings.Duration("ANNUAL", 2, "ru"));
            Assert.AreEqual("5 лет", LicenseStrings.Duration("ANNUAL", 5, "ru"));
            Assert.AreEqual("3个月", LicenseStrings.Duration("MONTHLY", 3, "zh"));
            Assert.AreEqual("1年", LicenseStrings.Duration("ANNUAL", 1, "zh"));
        }

        [Test]
        public void Dutch_IsSupportedAndTranslated()
        {
            Assert.Contains("nl", LicenseStrings.SupportedLanguages);
            Assert.AreEqual("nl", LicenseStrings.FromSystemLanguage(UnityEngine.SystemLanguage.Dutch));
            Assert.AreEqual("3 maanden", LicenseStrings.Duration("MONTHLY", 3, "nl"));
            Assert.AreEqual("1 maand", LicenseStrings.Duration("MONTHLY", 1, "nl"));
            Assert.AreEqual("2 jaar", LicenseStrings.Duration("ANNUAL", 2, "nl"));
            Assert.AreEqual("de eerste 1 jaar, daarna € 399,99", LicenseStrings.IntroOfferNote("1 jaar", "€ 399,99", "nl"));
            Assert.AreEqual("Feedback sturen", LicenseStrings.Feedback("nl"));
            Assert.AreEqual("Abonneren", LicenseStrings.Subscribe("nl"));
            Assert.AreEqual("Jaarlijks", LicenseStrings.TermLabel("ANNUAL", "nl"));
            Assert.AreEqual("Het is voor mijn bedrijf of school", LicenseStrings.ExitReasonLabel(AppFeedback.ReasonForMyCompany, "nl"));
            Assert.AreEqual("Apparaatcode: ABCDE-12345", LicenseStrings.DeviceCodeLine("ABCDE-12345", "nl"));
        }

        [Test]
        public void Duration_UnknownLanguageFallsBackToEnglish()
        {
            Assert.AreEqual("3 months", LicenseStrings.Duration("MONTHLY", 3, "xx"));
        }

        [Test]
        public void IntroOfferNote_UsesStorePriceVerbatim()
        {
            Assert.AreEqual("for the first 3 months, then US$39.99",
                LicenseStrings.IntroOfferNote("3 months", "US$39.99", "en"));
            Assert.AreEqual("durante 1 año, después 399,99 €",
                LicenseStrings.IntroOfferNote("1 año", "399,99 €", "es"));
            Assert.AreEqual("首1年，之后¥39,999",
                LicenseStrings.IntroOfferNote("1年", "¥39,999", "zh"));
        }

        [Test]
        public void EveryLanguageHasEveryNewString()
        {
            foreach (string lang in LicenseStrings.SupportedLanguages)
            {
                Assert.IsNotEmpty(LicenseStrings.Feedback(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.RatingHint(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.TextPlaceholder(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.Send(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.Skip(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.ChooseCategory(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.Thanks(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.SendFailed(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.RateLimited(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.ExitQuestion(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.ExitThanks(lang), lang);
                Assert.IsNotEmpty(LicenseStrings.CompanyHint(lang), lang);
                foreach (string c in AppFeedback.Categories) Assert.AreNotEqual(c, LicenseStrings.CategoryLabel(c, lang), lang + "/" + c);
                foreach (string r in AppFeedback.ExitReasons) Assert.AreNotEqual(r, LicenseStrings.ExitReasonLabel(r, lang), lang + "/" + r);
                Assert.IsNotNull(LicenseStrings.Duration("MONTHLY", 3, lang), lang);
                StringAssert.Contains("X", LicenseStrings.IntroOfferNote("D", "X", lang), lang);
                StringAssert.Contains("D", LicenseStrings.IntroOfferNote("D", "X", lang), lang);
            }
        }

        [Test]
        public void CompanyHint_HasNoUrlOrPrice_StoreRule()
        {
            foreach (string lang in LicenseStrings.SupportedLanguages)
            {
                string hint = LicenseStrings.CompanyHint(lang);
                StringAssert.DoesNotContain("http", hint, lang);
                StringAssert.DoesNotContain("www", hint, lang);
                StringAssert.DoesNotContain(".com", hint, lang);
                StringAssert.DoesNotContain("€", hint, lang);
                StringAssert.DoesNotContain("$", hint, lang);
            }
        }

        [Test]
        public void BestValueBadge_IgnoresIntroOffers()
        {
            var product = new StoreProductInfo { sku = "sub_access", isSubscription = true };
            product.terms.Add(Plan("MONTHLY", 3999));
            product.terms.Add(Plan("SEMIANNUAL", 19999));
            var annual = Plan("ANNUAL", 39999);
            annual.introFormattedPrice = "US$1.00"; annual.introTerm = "ANNUAL"; annual.introTermCount = 1;
            product.terms.Add(annual);

            // 399.99/12 = 33.33 < 39.99 and < 33.33? 199.99/6 = 33.33 → not strictly below → no badge,
            // and the cheap intro price must not change that.
            Assert.IsFalse(StoreSubscription.IsAnnualBestValue(product, new[] { "MONTHLY", "SEMIANNUAL", "ANNUAL" }, out _));

            annual.amountInHundredths = 34999; annual.formattedPrice = "US$349.99";
            Assert.IsTrue(StoreSubscription.IsAnnualBestValue(product, new[] { "MONTHLY", "SEMIANNUAL", "ANNUAL" }, out _));
        }
    }
}
