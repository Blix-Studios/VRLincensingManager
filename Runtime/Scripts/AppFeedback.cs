using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VRLicensing
{
    /// <summary>
    /// In-app feedback and demo exit survey (v1.12.0-pre.9, spec 08 §5, contract §9). Plain C#:
    /// the request body of <c>submit_app_feedback</c>, its result, the input limits and the
    /// "ask the exit survey again?" rule. Nothing here touches Unity so it is unit-tested.
    ///
    /// Data rules: the body carries the device id (as every RPC does), the product, the answer
    /// and the UI language — never a user name, e-mail or the Meta user id.
    /// </summary>
    public static class AppFeedback
    {
        public const string KindFeedback = "feedback";
        public const string KindDemoExit = "demo_exit_reason";

        public const string CategoryBug = "bug";
        public const string CategoryPerformance = "performance";
        public const string CategoryContent = "content";
        public const string CategorySuggestion = "suggestion";
        public static readonly string[] Categories = { CategoryBug, CategoryPerformance, CategoryContent, CategorySuggestion };

        public const string ReasonPrice = "price";
        public const string ReasonForMyCompany = "for_my_company";
        public const string ReasonNotWhatIExpected = "not_what_i_expected";
        public const string ReasonOther = "other";
        public static readonly string[] ExitReasons = { ReasonPrice, ReasonForMyCompany, ReasonNotWhatIExpected, ReasonOther };

        public const int MaxTextLength = 500;
        public const int MaxAppVersionLength = 40;
        public const int MaxLocaleLength = 10;
        public const int MinRating = 1;
        public const int MaxRating = 5;

        // Error codes returned by the server (contract §9).
        public const string ErrorInvalidDevice = "invalid_device";
        public const string ErrorInvalidKind = "invalid_kind";
        public const string ErrorInvalidRating = "invalid_rating";
        public const string ErrorInvalidCategory = "invalid_category";
        public const string ErrorInvalidReason = "invalid_reason";
        public const string ErrorTextTooLong = "text_too_long";
        public const string ErrorRateLimited = "rate_limited";
        /// <summary>Client-side code for a transport failure (no server answer).</summary>
        public const string ErrorNetwork = "network";

        /// <summary>Server answer: <c>{"ok":true,"id":"…"}</c> or <c>{"ok":false,"error":"code"}</c>.</summary>
        public sealed class Result
        {
            public bool ok;
            public string id;
            public string error;

            public static Result Failure(string error) => new Result { ok = false, error = error };
            public static Result Success(string id) => new Result { ok = true, id = id };
        }

        /// <summary>
        /// EDITOR/test hook: when set, the manager answers feedback locally (argument: the kind)
        /// instead of calling the server — the editor store stub sets it so Play mode never
        /// writes real rows. Never set in players.
        /// </summary>
        public static Func<string, Result> StubSubmitter;

        public static bool IsValidCategory(string category) => Array.IndexOf(Categories, category) >= 0;
        public static bool IsValidReason(string reason) => Array.IndexOf(ExitReasons, reason) >= 0;
        public static bool IsValidRating(int rating) => rating >= MinRating && rating <= MaxRating;

        /// <summary>Trimmed free text capped at <see cref="MaxTextLength"/>; null when empty.</summary>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.Trim();
            if (t.Length == 0) return null;
            return t.Length > MaxTextLength ? t.Substring(0, MaxTextLength) : t;
        }

        /// <summary>Short BCP-47 tag as the UI shows it ("es", "pt-BR"), capped at 10 characters.</summary>
        public static string NormalizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return null;
            string l = locale.Trim().Replace('_', '-');
            if (l.Length > MaxLocaleLength) l = l.Substring(0, MaxLocaleLength);
            return l;
        }

        public static string NormalizeAppVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            string v = version.Trim();
            return v.Length > MaxAppVersionLength ? v.Substring(0, MaxAppVersionLength) : v;
        }

        /// <summary>
        /// JSON body of <c>submit_app_feedback</c>. Optional members are omitted (not null) so the
        /// RPC's DEFAULT NULL applies; <paramref name="rating"/> 0 means "no rating".
        /// Throws <see cref="ArgumentException"/> for a body the server would reject anyway.
        /// </summary>
        public static string BuildRequestBody(string deviceId, int productId, string kind, int rating,
            string category, string reason, string text, string appVersion, string locale)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) throw new ArgumentException("device id required", nameof(deviceId));
            if (kind != KindFeedback && kind != KindDemoExit) throw new ArgumentException("unknown kind", nameof(kind));
            if (kind == KindFeedback && !IsValidCategory(category)) throw new ArgumentException("category required", nameof(category));
            if (kind == KindDemoExit && !IsValidReason(reason)) throw new ArgumentException("reason required", nameof(reason));
            if (rating != 0 && (kind == KindDemoExit || !IsValidRating(rating))) throw new ArgumentException("rating 1-5 (feedback only)", nameof(rating));

            var members = new List<string>
            {
                "\"p_device_id\":\"" + JsonText.Escape(deviceId) + "\"",
                "\"p_product_id\":" + productId.ToString(CultureInfo.InvariantCulture),
                "\"p_kind\":\"" + kind + "\"",
            };
            if (kind == KindFeedback)
            {
                if (rating != 0) members.Add("\"p_rating\":" + rating.ToString(CultureInfo.InvariantCulture));
                members.Add("\"p_category\":\"" + category + "\"");
            }
            else
            {
                members.Add("\"p_reason\":\"" + reason + "\"");
            }
            Add(members, JsonText.Member("p_text", NormalizeText(text)));
            Add(members, JsonText.Member("p_app_version", NormalizeAppVersion(appVersion)));
            Add(members, JsonText.Member("p_locale", NormalizeLocale(locale)));
            return "{" + string.Join(",", members) + "}";
        }

        private static void Add(List<string> members, string member)
        {
            if (member != null) members.Add(member);
        }

        private static readonly Regex OkRx = new Regex("\"ok\"\\s*:\\s*(true|false)", RegexOptions.Compiled);
        private static readonly Regex IdRx = new Regex("\"id\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex ErrorRx = new Regex("\"error\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);

        /// <summary>Parses the RPC answer; anything unreadable is a failure with code "invalid_response".</summary>
        public static Result ParseResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Result.Failure("invalid_response");
            var ok = OkRx.Match(json);
            if (!ok.Success) return Result.Failure("invalid_response");
            if (ok.Groups[1].Value == "true")
            {
                var id = IdRx.Match(json);
                return Result.Success(id.Success ? id.Groups[1].Value : "");
            }
            var err = ErrorRx.Match(json);
            return Result.Failure(err.Success && err.Groups[1].Value.Length > 0 ? err.Groups[1].Value : "unknown");
        }

        /// <summary>
        /// Exit-survey rule (contract §9): ask when nothing was answered yet; after an answer or a
        /// skip, ask again only when the server's <c>demo_reset_at</c> is later than the stored
        /// answer time. Unparsable dates never re-ask.
        /// </summary>
        public static bool ShouldAskExitSurvey(string answeredAtIso, string demoResetAtIso)
        {
            if (string.IsNullOrEmpty(answeredAtIso)) return true;
            if (string.IsNullOrEmpty(demoResetAtIso)) return false;
            if (!TryParseUtc(answeredAtIso, out DateTime answered)) return false;
            if (!TryParseUtc(demoResetAtIso, out DateTime reset)) return false;
            return reset > answered;
        }

        public static bool TryParseUtc(string iso, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(iso)) return false;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
                return false;
            utc = parsed;
            return true;
        }

        /// <summary>ISO-8601 UTC stamp stored as the exit-survey answer time.</summary>
        public static string Stamp(DateTime utcNow) => utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }
}
