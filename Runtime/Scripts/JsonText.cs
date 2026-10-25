using System.Text;

namespace VRLicensing
{
    /// <summary>
    /// Minimal JSON string escaping shared by the hand-built RPC payloads (store events, app
    /// feedback). JsonUtility cannot omit fields or emit free-form jsonb, so those bodies are
    /// assembled by hand; every user-provided value goes through <see cref="Escape"/>.
    /// </summary>
    public static class JsonText
    {
        public static string Escape(string s)
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

        /// <summary>"key":"value" (escaped), or null when the value is null/empty.</summary>
        public static string Member(string key, string value)
        {
            return string.IsNullOrEmpty(value) ? null : "\"" + key + "\":\"" + Escape(value) + "\"";
        }
    }
}
