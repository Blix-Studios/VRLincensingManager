using System;
using NUnit.Framework;
using VRLicensing;

namespace VRLicensing.Tests
{
    /// <summary>submit_app_feedback contract (03-LICENSING-CONTRACT §9): body, limits, result, re-ask rule.</summary>
    public class AppFeedbackTests
    {
        [Test]
        public void Feedback_BodyHasKindRatingCategoryTextVersionLocale()
        {
            string body = AppFeedback.BuildRequestBody("dev-1", 1, AppFeedback.KindFeedback, 4,
                AppFeedback.CategoryBug, null, "  The saw clips through the log  ", "1.0.0 (27)", "es");

            Assert.AreEqual(
                "{\"p_device_id\":\"dev-1\",\"p_product_id\":1,\"p_kind\":\"feedback\",\"p_rating\":4," +
                "\"p_category\":\"bug\",\"p_text\":\"The saw clips through the log\",\"p_app_version\":\"1.0.0 (27)\",\"p_locale\":\"es\"}",
                body);
        }

        [Test]
        public void Feedback_OmitsOptionalMembersWhenAbsent()
        {
            string body = AppFeedback.BuildRequestBody("dev-1", 2, AppFeedback.KindFeedback, 0,
                AppFeedback.CategorySuggestion, null, "   ", null, null);

            Assert.AreEqual("{\"p_device_id\":\"dev-1\",\"p_product_id\":2,\"p_kind\":\"feedback\",\"p_category\":\"suggestion\"}", body);
            StringAssert.DoesNotContain("p_rating", body);
            StringAssert.DoesNotContain("p_reason", body);
            StringAssert.DoesNotContain("null", body);
        }

        [Test]
        public void ExitReason_BodyHasReasonAndNeverRatingOrCategory()
        {
            string body = AppFeedback.BuildRequestBody("dev-1", 1, AppFeedback.KindDemoExit, 0,
                null, AppFeedback.ReasonForMyCompany, "We have 12 operators", "1.0.0", "pt-BR");

            Assert.AreEqual(
                "{\"p_device_id\":\"dev-1\",\"p_product_id\":1,\"p_kind\":\"demo_exit_reason\",\"p_reason\":\"for_my_company\"," +
                "\"p_text\":\"We have 12 operators\",\"p_app_version\":\"1.0.0\",\"p_locale\":\"pt-BR\"}",
                body);
        }

        [Test]
        public void Body_RejectsWhatTheServerWouldReject()
        {
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("", 1, AppFeedback.KindFeedback, 0, AppFeedback.CategoryBug, null, null, null, null));
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("d", 1, "praise", 0, AppFeedback.CategoryBug, null, null, null, null));
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindFeedback, 0, "ux", null, null, null, null));
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindFeedback, 6, AppFeedback.CategoryBug, null, null, null, null));
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindDemoExit, 3, null, AppFeedback.ReasonPrice, null, null, null));
            Assert.Throws<ArgumentException>(() => AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindDemoExit, 0, null, "too_hard", null, null, null));
        }

        [Test]
        public void Limits_TextCappedAt500_VersionAt40_LocaleAt10()
        {
            string longText = new string('x', 620);
            string longVersion = new string('v', 60);
            string body = AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindFeedback, 0, AppFeedback.CategoryContent,
                null, longText, longVersion, "es-419-extra-long");

            Assert.AreEqual(500, AppFeedback.NormalizeText(longText).Length);
            StringAssert.Contains("\"p_text\":\"" + new string('x', 500) + "\"", body);
            StringAssert.Contains("\"p_app_version\":\"" + new string('v', 40) + "\"", body);
            StringAssert.Contains("\"p_locale\":\"es-419-ext\"", body);
            Assert.IsNull(AppFeedback.NormalizeText("  \n "));
        }

        [Test]
        public void Body_EscapesUserText()
        {
            string body = AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindFeedback, 0, AppFeedback.CategoryBug,
                null, "quote \" backslash \\ newline\nend", null, null);
            StringAssert.Contains("\"p_text\":\"quote \\\" backslash \\\\ newline\\nend\"", body);
        }

        [Test]
        public void Body_NeverCarriesPersonalFields()
        {
            string body = AppFeedback.BuildRequestBody("d", 1, AppFeedback.KindFeedback, 5, AppFeedback.CategoryContent, null, "ok", "1.0.0", "en");
            StringAssert.DoesNotContain("user", body);
            StringAssert.DoesNotContain("email", body);
            StringAssert.DoesNotContain("meta", body);
        }

        [Test]
        public void ParseResult_OkAndErrorShapes()
        {
            var ok = AppFeedback.ParseResult("{\"ok\": true, \"id\": \"5f1c2c1e-0000-4000-8000-000000000000\"}");
            Assert.IsTrue(ok.ok);
            Assert.AreEqual("5f1c2c1e-0000-4000-8000-000000000000", ok.id);

            var err = AppFeedback.ParseResult("{\"ok\":false,\"error\":\"rate_limited\"}");
            Assert.IsFalse(err.ok);
            Assert.AreEqual(AppFeedback.ErrorRateLimited, err.error);

            Assert.AreEqual("invalid_response", AppFeedback.ParseResult("").error);
            Assert.AreEqual("invalid_response", AppFeedback.ParseResult("<html>502</html>").error);
            Assert.AreEqual("unknown", AppFeedback.ParseResult("{\"ok\":false}").error);
        }

        [Test]
        public void ExitSurvey_AskedOnceThenOnlyAfterANewerDemoReset()
        {
            Assert.IsTrue(AppFeedback.ShouldAskExitSurvey(null, null), "never answered → ask");
            Assert.IsTrue(AppFeedback.ShouldAskExitSurvey("", "2026-10-20T10:00:00Z"), "never answered → ask");

            string answered = AppFeedback.Stamp(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc));
            Assert.IsFalse(AppFeedback.ShouldAskExitSurvey(answered, null), "answered, no reset → never again");
            Assert.IsFalse(AppFeedback.ShouldAskExitSurvey(answered, "2026-10-24T09:00:00+00:00"), "reset before the answer → no");
            Assert.IsFalse(AppFeedback.ShouldAskExitSurvey(answered, "2026-10-25T12:00:00Z"), "same instant → no");
            Assert.IsTrue(AppFeedback.ShouldAskExitSurvey(answered, "2026-10-26T08:30:00.123456+00:00"), "admin reset after the answer → ask again");
            Assert.IsTrue(AppFeedback.ShouldAskExitSurvey(answered, "2026-10-25T14:00:00+01:00"), "13:00Z > 12:00Z → ask");
            Assert.IsFalse(AppFeedback.ShouldAskExitSurvey(answered, "not a date"), "unparsable reset → no");
            Assert.IsFalse(AppFeedback.ShouldAskExitSurvey("garbage", "2026-10-26T08:30:00Z"), "unparsable answer → no");
        }

        [Test]
        public void Stamp_IsUtcIso8601()
        {
            string stamp = AppFeedback.Stamp(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual("2026-10-25T12:00:00.0000000Z", stamp);
            Assert.IsTrue(AppFeedback.TryParseUtc(stamp, out DateTime back));
            Assert.AreEqual(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), back);
        }
    }
}
