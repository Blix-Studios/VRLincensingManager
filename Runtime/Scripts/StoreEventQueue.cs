using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VRLicensing
{
    /// <summary>
    /// Pending store analytics events, sent to the server as the <c>p_events</c> JSON array of
    /// <c>report_device_session</c> (contract §8). Each event carries name, SKU, term and UTC time
    /// only — never a user id, device id or anything personal.
    ///
    /// v1.12.0-pre.9: the queue is persisted through the injected <c>load</c>/<c>save</c> pair
    /// (PlayerPrefs in the player) so events survive an app close before they are reported, and
    /// "immediate" events (<see cref="PlansOpened"/> and every <c>checkout_*</c>) ask the manager
    /// to report right away instead of waiting for the next session report.
    /// </summary>
    public sealed class StoreEventQueue
    {
        public const int MaxEvents = 50;

        public const string PlansOpened = "plans_opened";
        public const string CheckoutOpened = "checkout_opened";
        public const string CheckoutCompleted = "checkout_completed";
        public const string CheckoutCancelled = "checkout_cancelled";
        public const string SubscriptionVerified = "subscription_verified";

        private readonly List<string> events = new List<string>();
        private readonly Func<string> load;
        private readonly Action<string> save;

        /// <param name="load">Returns the persisted queue ("" or null when empty).</param>
        /// <param name="save">Persists the queue (one JSON object per line).</param>
        public StoreEventQueue(Func<string> load, Action<string> save)
        {
            this.load = load;
            this.save = save;
            string stored = load != null ? load() : null;
            if (string.IsNullOrEmpty(stored)) return;
            foreach (string line in stored.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 2 && trimmed[0] == '{') events.Add(trimmed);
            }
            Trim();
        }

        public int Count => events.Count;

        /// <summary>True for events that must be reported at once (plans_opened and checkout_*).</summary>
        public static bool IsImmediate(string name)
        {
            return name == PlansOpened || (name != null && name.StartsWith("checkout_", StringComparison.Ordinal));
        }

        /// <summary>Adds an event. Oldest events are dropped beyond <see cref="MaxEvents"/>.</summary>
        public void Enqueue(string name, string sku, string term, DateTime utcNow)
        {
            string at = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            events.Add("{\"name\":\"" + Escape(name) + "\",\"sku\":\"" + Escape(sku ?? "") +
                       "\",\"term\":\"" + Escape(term ?? "") + "\",\"at\":\"" + at + "\"}");
            Trim();
            Persist();
        }

        /// <summary>The pending events as a JSON array (and clears them), or null when empty.</summary>
        public string Drain()
        {
            if (events.Count == 0) return null;
            string json = "[" + string.Join(",", events) + "]";
            events.Clear();
            Persist();
            return json;
        }

        /// <summary>
        /// Puts a drained array back in front of the queue after a failed report, so the events
        /// go out with the next successful one (and survive a restart).
        /// </summary>
        public void Restore(string drainedJsonArray)
        {
            if (string.IsNullOrEmpty(drainedJsonArray)) return;
            var restored = SplitArray(drainedJsonArray);
            if (restored.Count == 0) return;
            events.InsertRange(0, restored);
            Trim();
            Persist();
        }

        /// <summary>The pending events as a JSON array without clearing them (diagnostics / tests).</summary>
        public string PeekJson() => events.Count == 0 ? null : "[" + string.Join(",", events) + "]";

        private void Trim()
        {
            while (events.Count > MaxEvents) events.RemoveAt(0);
        }

        private void Persist()
        {
            save?.Invoke(events.Count == 0 ? "" : string.Join("\n", events));
        }

        /// <summary>Splits a JSON array of flat objects (as produced by this class) back into objects.</summary>
        public static List<string> SplitArray(string jsonArray)
        {
            var list = new List<string>();
            int depth = 0, start = -1;
            bool inString = false;
            for (int i = 0; i < jsonArray.Length; i++)
            {
                char c = jsonArray[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { list.Add(jsonArray.Substring(start, i - start + 1)); start = -1; } }
            }
            return list;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
